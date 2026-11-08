namespace Praetoria.Core.Galaxy;

/// <summary>A star system — a node on the galaxy map (GDD §19 "2D node-and-lane, curated scale").
/// Position is in map units (the UI scales); holdings reference a system by id.</summary>
public sealed class StarSystem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }

    /// <summary>The region (sector) this system belongs to (GDD §19 "regions with their own cultures, industries, loyalties").</summary>
    public string RegionId { get; set; } = "";
    /// <summary>The culture of the people here; defaults to the region's.</summary>
    public string Culture { get; set; } = "";
    /// <summary>What the primary world looks like — terran, arid, ocean, forge, frozen, gas, belt, barren — for the map and the office window.</summary>
    public string PlanetClass { get; set; } = "terran";

    /// <summary>Asteroid belt richness, 0 = none. Also the number of mining claims the belt supports (GDD §17 asteroids).</summary>
    public int Belt { get; set; }

    /// <summary>Everything that orbits the star (GDD §19 "show all the planets"): the primary world (the one a holding sits on)
    /// and its sisters, moons, giants and belts. At least one body, marked primary, matching <see cref="PlanetClass"/>.</summary>
    public List<Body> Bodies { get; set; } = new();
    public Body Primary => Bodies.FirstOrDefault(b => b.IsPrimary) ?? new Body { Name = Name, Class = PlanetClass, Size = 3, IsPrimary = true };
    public string Description { get; set; } = "";
}

/// <summary>A body in a system: a world, a moon, a gas giant, a belt. Decorative except the primary, which is the world a holding sits on.</summary>
public sealed class Body
{
    public string Name { get; set; } = "";
    public string Class { get; set; } = "barren";
    /// <summary>1 small (a moon, a rock), 2 a world, 3 the primary or a giant.</summary>
    public int Size { get; set; } = 2;
    public bool IsPrimary { get; set; }
}

/// <summary>A region of space: a handful of systems that share a culture and a character (GDD §19). Loyalties and houses
/// are derived from the holdings in it; the region itself is geography and identity.</summary>
public sealed class Region
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Culture { get; set; } = "reach";
    public string Description { get; set; } = "";

    /// <summary>Where the region sits: the <see cref="Galaxy.Ring.Core"/> (commerce, the capital, order), the <see cref="Galaxy.Ring.Middle"/>
    /// (which way it leans is the question) or the <see cref="Galaxy.Ring.Rim"/> (lawless, restless, where houses break away). GDD §19.</summary>
    public string Ring { get; set; } = Praetoria.Core.Galaxy.Ring.Middle;
}

/// <summary>The three rings of a galaxy and the rules that make them behave differently (data, from the galaxy file).</summary>
public static class Ring
{
    public const string Core = "core", Middle = "middle", Rim = "rim";
    public static readonly string[] All = { Core, Middle, Rim };
}

/// <summary>How a ring behaves (GDD §19): what it does to prosperity and unrest on its worlds, how fast lawlessness grows,
/// where allegiance to the capital settles, and what commerce it adds.</summary>
public sealed record RingRules(string Id, int Prosperity, int UnrestDrift, int LawlessnessDrift, int LawlessnessStart, int Allegiance, int TradeBonus, int CharterBonus)
{
    public static RingRules Default(string ring) => ring switch
    {
        Ring.Core => new(ring, 10, -1, -2, 5, 30, 1, 1),
        Ring.Rim => new(ring, -10, 1, 1, 40, -20, 0, 0),
        _ => new(ring, 0, 0, 0, 20, 0, 0, 0)
    };
}

/// <summary>An undirected hyperlane between two systems.</summary>
public sealed class Hyperlane
{
    public string A { get; set; } = "";
    public string B { get; set; } = "";
}

/// <summary>
/// The galaxy map (BuildSpec §3 "System / Galaxy node", GDD §19). Systems + lanes are data from
/// /content/galaxy; a scenario names the galaxy it is played on and places its holdings in systems.
/// Territory is derived, never stored: a system belongs to whoever owns the holdings in it (public knowledge,
/// GDD §19 mixed visibility — fleets and strength, when they arrive, are the fogged part). Serialised with
/// the World so saves are self-contained. Plain data + pure queries; no Godot.
/// </summary>
public sealed class Galaxy
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Dictionary<string, StarSystem> Systems { get; set; } = new();
    public List<Hyperlane> Lanes { get; set; } = new();
    public List<Region> Regions { get; set; } = new();

    /// <summary>Id of the shared "minor houses" file merged into every scenario on this galaxy (empty = none).</summary>
    public string Baseline { get; set; } = "";

    /// <summary>The system the galaxy turns on (GDD §19 "where the capital is"): whoever holds its planet keeps the court.</summary>
    public string Capital { get; set; } = "";

    /// <summary>Per-ring rules; a ring not listed takes its defaults.</summary>
    public Dictionary<string, RingRules> Rings { get; set; } = new();
    public RingRules RulesFor(string ring) => Rings.TryGetValue(ring, out var r) ? r : RingRules.Default(ring);
    public string RingOf(string systemId) { var s = System(systemId); var r = s != null ? Region(s.RegionId) : null; return r?.Ring ?? Ring.Middle; }

    public StarSystem? System(string id) => Systems.TryGetValue(id, out var s) ? s : null;
    public Region? Region(string id) => Regions.FirstOrDefault(r => r.Id == id);
    public IEnumerable<StarSystem> SystemsIn(string regionId) => Systems.Values.Where(s => s.RegionId == regionId).OrderBy(s => s.Id, StringComparer.Ordinal);
    /// <summary>A system's culture: its own if set, else its region's, else "reach".</summary>
    public string CultureOf(string systemId) { var s = System(systemId); if (s == null) return "reach"; if (s.Culture.Length > 0) return s.Culture; return Region(s.RegionId)?.Culture ?? "reach"; }

    /// <summary>Systems one lane away, in stable id order.</summary>
    public IEnumerable<string> Neighbors(string systemId)
    {
        var list = new List<string>();
        foreach (var l in Lanes)
        {
            if (l.A == systemId) list.Add(l.B);
            else if (l.B == systemId) list.Add(l.A);
        }
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    /// <summary>Lane-hops between two systems by breadth-first search, or -1 if unreachable.</summary>
    public int Hops(string from, string to) => Hops(from, to, null);

    /// <summary>Lane hops between two systems, routing around systems the predicate blocks (a garrisoned enemy chokepoint, GDD §19);
    /// the destination itself is never blocked. −1 when no way remains.</summary>
    public int Hops(string from, string to, Func<string, bool>? blocked)
    {
        if (from == to) return Systems.ContainsKey(from) ? 0 : -1;
        if (!Systems.ContainsKey(from) || !Systems.ContainsKey(to)) return -1;
        var dist = new Dictionary<string, int> { [from] = 0 };
        var queue = new Queue<string>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var n in Neighbors(cur))
            {
                if (dist.ContainsKey(n)) continue;
                if (n != to && blocked != null && blocked(n)) continue;
                dist[n] = dist[cur] + 1;
                if (n == to) return dist[n];
                queue.Enqueue(n);
            }
        }
        return -1;
    }

    private HashSet<string>? _chokepoints; private int _chokeLanes = -1;

    /// <summary>The chokepoints of the lane web (GDD §19 "if you control a key system along a route"): every articulation point
    /// (cut it and the map falls apart) plus the quarter of systems the most shortest paths run through. Whoever holds a
    /// chokepoint's world takes tolls, and may bar passage to an enemy while a fleet sits there. Cached per lane set.</summary>
    public IReadOnlySet<string> Chokepoints()
    {
        if (_chokepoints != null && _chokeLanes == Lanes.Count) return _chokepoints;
        var ids = Systems.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList(); var set = new HashSet<string>();
        if (ids.Count == 0) { _chokepoints = set; _chokeLanes = Lanes.Count; return set; }
        // articulation points (Tarjan)
        var disc = new Dictionary<string, int>(); var low = new Dictionary<string, int>(); int time = 0;
        void Dfs(string u, string? parent)
        {
            disc[u] = low[u] = time++; int children = 0;
            foreach (var v in Neighbors(u))
            {
                if (v == parent) continue;
                if (!disc.ContainsKey(v)) { children++; Dfs(v, u); low[u] = Math.Min(low[u], low[v]); if (parent != null && low[v] >= disc[u]) set.Add(u); }
                else low[u] = Math.Min(low[u], disc[v]);
            }
            if (parent == null && children > 1) set.Add(u);
        }
        foreach (var id in ids) if (!disc.ContainsKey(id)) Dfs(id, null);
        // betweenness (Brandes), the top sixth
        var bc = ids.ToDictionary(i => i, _ => 0.0);
        foreach (var s in ids)
        {
            var stack = new Stack<string>(); var pred = ids.ToDictionary(i => i, _ => new List<string>()); var sigma = ids.ToDictionary(i => i, _ => 0.0); var d = ids.ToDictionary(i => i, _ => -1);
            sigma[s] = 1; d[s] = 0; var q = new Queue<string>(); q.Enqueue(s);
            while (q.Count > 0) { var v = q.Dequeue(); stack.Push(v); foreach (var w in Neighbors(v)) { if (d[w] < 0) { q.Enqueue(w); d[w] = d[v] + 1; } if (d[w] == d[v] + 1) { sigma[w] += sigma[v]; pred[w].Add(v); } } }
            var delta = ids.ToDictionary(i => i, _ => 0.0);
            while (stack.Count > 0) { var w = stack.Pop(); foreach (var v in pred[w]) delta[v] += sigma[v] / sigma[w] * (1 + delta[w]); if (w != s) bc[w] += delta[w]; }
        }
        int top = Math.Max(1, (ids.Count + 3) / 4);
        foreach (var id in bc.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(top).Select(kv => kv.Key)) set.Add(id);
        _chokepoints = set; _chokeLanes = Lanes.Count; return set;
    }
    public bool IsChokepoint(string systemId) => Chokepoints().Contains(systemId);

    /// <summary>Every id referenced by a lane must be a system; a lane may not loop.</summary>
    public IEnumerable<string> Problems()
    {
        foreach (var l in Lanes)
        {
            if (!Systems.ContainsKey(l.A)) yield return $"lane {l.A}–{l.B}: unknown system '{l.A}'";
            if (!Systems.ContainsKey(l.B)) yield return $"lane {l.A}–{l.B}: unknown system '{l.B}'";
            if (l.A == l.B) yield return $"lane {l.A}–{l.B} loops";
        }
        foreach (var s in Systems.Values)
            if (s.RegionId.Length > 0 && Region(s.RegionId) == null) yield return $"system {s.Id}: unknown region '{s.RegionId}'";
        foreach (var r in Regions)
            if (!Systems.Values.Any(s => s.RegionId == r.Id)) yield return $"region {r.Id} has no systems";
        foreach (var r in Regions) if (!Ring.All.Contains(r.Ring)) yield return $"region {r.Id}: unknown ring '{r.Ring}'";
        if (Capital.Length > 0 && !Systems.ContainsKey(Capital)) yield return $"capital '{Capital}' is not a system";
    }
}
