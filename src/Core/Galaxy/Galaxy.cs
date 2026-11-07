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
    public string Description { get; set; } = "";
}

/// <summary>A region of space: a handful of systems that share a culture and a character (GDD §19). Loyalties and houses
/// are derived from the holdings in it; the region itself is geography and identity.</summary>
public sealed class Region
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Culture { get; set; } = "reach";
    public string Description { get; set; } = "";
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
    public int Hops(string from, string to)
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
                dist[n] = dist[cur] + 1;
                if (n == to) return dist[n];
                queue.Enqueue(n);
            }
        }
        return -1;
    }

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
    }
}
