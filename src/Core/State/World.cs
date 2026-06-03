namespace Praetoria.Core.State;

/// <summary>
/// The entire game state (BuildSpec §3, GDD §15 Layer 1 — "the spine"). The engine is a pure
/// function of this object, and a save is just this object serialised (including RNG state).
/// Holds entities, the relationship graph, world-level flags/counters, the recent-history log,
/// and the Director's suppression memory. Deliberately Godot-free.
/// </summary>
public sealed class World
{
    public int Turn { get; set; }

    /// <summary>Serialised RNG position (BuildSpec §1.5). Restored into an IRng on load.</summary>
    public ulong RngState { get; set; }

    /// <summary>"fractured_stars" (pre-Imperium) or "imperium" — drives reactive UI later (GDD §19).</summary>
    public string Era { get; set; } = "fractured_stars";

    /// <summary>The character the player currently inhabits — bound to the "self" role every event.</summary>
    public string ProtagonistId { get; set; } = "";

    public Dictionary<string, Character> Characters { get; set; } = new();
    public Dictionary<string, House> Houses { get; set; } = new();
    public List<Relationship> Relationships { get; set; } = new();

    /// <summary>Per-actor action economy (GDD §9), keyed by character id. The protagonist holds a
    /// full set; NPC houses hold abstracted budgets (GDD §3). Refilled each turn.</summary>
    public Dictionary<string, ActionPools> Pools { get; set; } = new();

    /// <summary>World-scope boolean flags / armed gates (GDD §16). Cleared flags simply aren't present.</summary>
    public HashSet<string> WorldFlags { get; set; } = new();

    /// <summary>World-scope numeric state (gate accumulators, clocks, tallies).</summary>
    public Dictionary<string, int> WorldCounters { get; set; } = new();

    public List<HistoryEntry> History { get; set; } = new();

    /// <summary>Most-recently-fired event ids (newest last). Director suppression reads this (GDD §15 L4).</summary>
    public List<string> RecentEventIds { get; set; } = new();

    // ---- Convenience accessors (not serialised behaviour, just lookups) ----

    public Character? Protagonist =>
        Characters.TryGetValue(ProtagonistId, out var c) ? c : null;

    public Character? Char(string id) => Characters.TryGetValue(id, out var c) ? c : null;

    public bool HasFlag(string flag) => WorldFlags.Contains(flag);

    /// <summary>Get-or-create the action pools for an actor (empty pools if none were seeded).</summary>
    public ActionPools PoolsFor(string actorId)
    {
        if (!Pools.TryGetValue(actorId, out var p))
            Pools[actorId] = p = new ActionPools { Cap = 0 };
        return p;
    }

    public int Counter(string key) => WorldCounters.TryGetValue(key, out var v) ? v : 0;

    /// <summary>Get the directed edge from→to, or null. Relationships are sparse.</summary>
    public Relationship? GetRelationship(string fromId, string toId)
    {
        foreach (var r in Relationships)
            if (r.FromId == fromId && r.ToId == toId) return r;
        return null;
    }

    /// <summary>Get-or-create the directed edge from→to so a consequence can write to it.</summary>
    public Relationship Relationship(string fromId, string toId)
    {
        var existing = GetRelationship(fromId, toId);
        if (existing != null) return existing;
        var rel = new Relationship { FromId = fromId, ToId = toId };
        Relationships.Add(rel);
        return rel;
    }

    /// <summary>All outgoing edges from a character — "connections of X" (BuildSpec §7).</summary>
    public IEnumerable<Relationship> ConnectionsOf(string id)
    {
        foreach (var r in Relationships)
            if (r.FromId == id) yield return r;
    }

    public IEnumerable<Character> LivingCharacters()
    {
        foreach (var c in Characters.Values)
            if (c.Alive) yield return c;
    }

    public void Log(string text, string? eventId = null) =>
        History.Add(new HistoryEntry { Turn = Turn, Text = text, EventId = eventId });
}
