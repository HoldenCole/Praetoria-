using System.Text.Json.Serialization;

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

    /// <summary>GDD §14 game mode, chosen at start: <see cref="ModeIronman"/> (bloodline extinct ⇒ the run
    /// ends) or <see cref="ModeContinue"/> (a surviving sworn house takes up the banner and play goes on).</summary>
    public string Mode { get; set; } = ModeIronman;

    public const string ModeIronman = "ironman";
    public const string ModeContinue = "continue";

    /// <summary>The character the player currently inhabits — bound to the "self" role every event.</summary>
    public string ProtagonistId { get; set; } = "";

    public Dictionary<string, Character> Characters { get; set; } = new();
    public Dictionary<string, House> Houses { get; set; } = new();
    public List<Relationship> Relationships { get; set; } = new();

    /// <summary>Every controlled place, keyed by holding id (GDD §17). The domain economy reads
    /// these each turn to feed each owner's treasury. Empty in scenarios with no domain layer.</summary>
    public Dictionary<string, Holding> Holdings { get; set; } = new();

    /// <summary>Currently-active crises, keyed by crisis id (GDD §16). A crisis persists and
    /// escalates here until a damper resolves it. The gate/cascade system reads and writes these.</summary>
    public Dictionary<string, ActiveCrisis> Crises { get; set; } = new();

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

    [JsonIgnore]
    public Character? Protagonist =>
        Characters.TryGetValue(ProtagonistId, out var c) ? c : null;

    public Character? Char(string id) => Characters.TryGetValue(id, out var c) ? c : null;

    public House? House(string id) => Houses.TryGetValue(id, out var h) ? h : null;

    public Holding? Holding(string id) => Holdings.TryGetValue(id, out var h) ? h : null;

    /// <summary>The house treasury for a character's house, or null if either is missing.</summary>
    public Resources? TreasuryOf(string characterId)
    {
        var c = Char(characterId);
        return c != null && Houses.TryGetValue(c.HouseId, out var h) ? h.Treasury : null;
    }

    /// <summary>All holdings owned by a house, in stable id order (deterministic iteration).</summary>
    public IEnumerable<Holding> HoldingsOf(string houseId)
    {
        foreach (var h in Holdings.Values)
            if (h.OwnerId == houseId) yield return h;
    }

    public bool HasFlag(string flag) => WorldFlags.Contains(flag);

    /// <summary>Canonical world flag the dynasty system raises when the protagonist dies with no heir
    /// (GDD §14 — the true fail state). Front-ends stop the run on it; ironman vs continue is later.</summary>
    public const string DynastyDeadFlag = "dynasty_dead";

    /// <summary>True once no name-bearer remains to inherit the seat (GDD §14 dynasty death).</summary>
    [JsonIgnore]
    public bool IsDynastyDead => HasFlag(DynastyDeadFlag);

    /// <summary>Get-or-create the action pools for an actor (empty pools if none were seeded).</summary>
    public ActionPools PoolsFor(string actorId)
    {
        if (!Pools.TryGetValue(actorId, out var p))
            Pools[actorId] = p = new ActionPools { Cap = 0 };
        return p;
    }

    public int Counter(string key) => WorldCounters.TryGetValue(key, out var v) ? v : 0;

    public bool IsCrisisActive(string id) => Crises.ContainsKey(id);

    public ActiveCrisis? Crisis(string id) => Crises.TryGetValue(id, out var c) ? c : null;

    /// <summary>Canonical world flag marking an active crisis, so ordinary flag conditions can read it.</summary>
    public static string CrisisFlag(string id) => $"crisis:{id}:active";

    /// <summary>Canonical world flag marking a one-time (<c>once</c>) crisis as spent after it resolved.</summary>
    public static string CrisisSpentFlag(string id) => $"crisis:{id}:spent";

    // (from,to) → edge. Rebuilt lazily whenever the public list has been appended to behind our back
    // (scenario building, deserialisation), so the list stays the serialised truth and lookups stay O(1):
    // the Binder evaluates every event × every candidate × every condition each turn (BuildSpec §7
    // "the bond graph must answer cheaply").
    private Dictionary<(string, string), Relationship>? _edgeIndex;

    private Dictionary<(string, string), Relationship> EdgeIndex()
    {
        if (_edgeIndex == null || _edgeIndex.Count != Relationships.Count)
        {
            _edgeIndex = new Dictionary<(string, string), Relationship>(Relationships.Count);
            foreach (var r in Relationships) _edgeIndex[(r.FromId, r.ToId)] = r;
        }
        return _edgeIndex;
    }

    /// <summary>Get the directed edge from→to, or null. Relationships are sparse.</summary>
    public Relationship? GetRelationship(string fromId, string toId) =>
        EdgeIndex().TryGetValue((fromId, toId), out var r) ? r : null;

    /// <summary>Get-or-create the directed edge from→to so a consequence can write to it.</summary>
    public Relationship Relationship(string fromId, string toId)
    {
        var index = EdgeIndex();
        if (index.TryGetValue((fromId, toId), out var existing)) return existing;
        var rel = new Relationship { FromId = fromId, ToId = toId };
        Relationships.Add(rel);
        index[(fromId, toId)] = rel;
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
