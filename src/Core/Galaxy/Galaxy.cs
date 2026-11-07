namespace Praetoria.Core.Galaxy;

/// <summary>A star system — a node on the galaxy map (GDD §19 "2D node-and-lane, curated scale").
/// Position is in map units (the UI scales); holdings reference a system by id.</summary>
public sealed class StarSystem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
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

    public StarSystem? System(string id) => Systems.TryGetValue(id, out var s) ? s : null;

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
    }
}
