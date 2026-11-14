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

    public const string EraRepublic = "fractured_stars", EraImperium = "imperium";
    /// <summary>True once a throne stands. Until then the Reach is a republic: no noble titles, only military and civic ones (docs/TITLES.md).</summary>
    [JsonIgnore] public bool IsImperium => Era == EraImperium;

    public const string ModeIronman = "ironman";
    public const string ModeContinue = "continue";

    /// <summary>GDD §14 "one knob": scales how aggressively rivals convert threat into action (coalition threshold,
    /// NPC war appetite, organic crisis odds). 1.0 = default; 0.5 forgiving; 2.0 harsh.</summary>
    public double Difficulty { get; set; } = 1.0;

    /// <summary>GDD §20 "family autonomy": 0 = deferential kin; 1.0 default; 2.0 a pressure-cooker of scheming relatives.</summary>
    public double FamilyAutonomy { get; set; } = 1.0;

    /// <summary>GDD §20 "crisis intensity": off / low / default / dominant — how fast the Great Crisis clock runs.</summary>
    public string CrisisIntensity { get; set; } = "default";

    /// <summary>The character the player currently inhabits — bound to the "self" role every event.</summary>
    public string ProtagonistId { get; set; } = "";

    public Dictionary<string, Character> Characters { get; set; } = new();
    public Dictionary<string, House> Houses { get; set; } = new();
    public List<Relationship> Relationships { get; set; } = new();

    /// <summary>The star map this run is played on (GDD §19), or null for a scenario with no galaxy (the
    /// Academy fixture). Territory is derived from holdings' <see cref="Holding.SystemId"/>.</summary>
    public Galaxy.Galaxy? Galaxy { get; set; }

    /// <summary>Every fleet, keyed by fleet id (GDD §8 office vs house fleets; §19 map markers).</summary>
    public Dictionary<string, Fleet> Fleets { get; set; } = new();

    /// <summary>Holdings under blockade, keyed by holding id (GDD §6). The siege clock toward conquest.</summary>
    public Dictionary<string, Siege> Sieges { get; set; } = new();

    /// <summary>Standing wars between houses (GDD §6). Fleets of warring houses fight where they meet.</summary>
    public List<War> Wars { get; set; } = new();

    /// <summary>Two houses are at war when a war stands between them — or when either is the lawless (raiders fight everyone).</summary>
    public bool AtWar(string a, string b) => a != b && (a == "pirates" || b == "pirates" || Wars.Any(w => w.Between(a, b)));
    public IEnumerable<string> EnemiesOf(string houseId) =>
        Wars.Where(w => w.Involves(houseId)).Select(w => w.Other(houseId)).OrderBy(h => h, StringComparer.Ordinal);

    /// <summary>The protagonist house's reported picture of rival fleets, keyed by fleet id (GDD §19 mixed visibility).</summary>
    public Dictionary<string, FleetIntel> Intel { get; set; } = new();

    /// <summary>Every controlled place, keyed by holding id (GDD §17). The domain economy reads
    /// these each turn to feed each owner's treasury. Empty in scenarios with no domain layer.</summary>
    public Dictionary<string, Holding> Holdings { get; set; } = new();

    /// <summary>Every venture (GDD §17 businesses), keyed by id. Yields to its owner each turn; pays a charter to the
    /// holder of the world it works from.</summary>
    public Dictionary<string, Business> Businesses { get; set; } = new();

    /// <summary>Per-region mood (GDD §19 rings): allegiance to the capital and lawlessness, keyed by region id. Created on
    /// first tick from the ring's defaults.</summary>
    public Dictionary<string, RegionState> Regions { get; set; } = new();

    /// <summary>Motions before the Senate and the last dozen decided (GDD §6.1).</summary>
    public List<Motion> Motions { get; set; } = new();

    /// <summary>Laws in force, house id → law id → option id (GDD §6.1); a law not named is at its default.</summary>
    public Dictionary<string, Dictionary<string, string>> Laws { get; set; } = new();

    /// <summary>Everything under construction (docs/ECONOMY_MARKET.md stage B), in the order begun.</summary>
    public List<Project> Projects { get; set; } = new();
    /// <summary>Ventures offered for sale to a named house, until they lapse.</summary>
    public List<VentureOffer> Offers { get; set; } = new();
    public IEnumerable<Project> ProjectsOf(string houseId) => Projects.Where(p => p.OwnerId == houseId);
    /// <summary>The companies on the Exchange (docs/ECONOMY_MARKET.md stage C) and the Reach's war bonds (stage D).</summary>
    /// <summary>What was said over the dead of the player's house, most recent last (docs/LIFE.md).</summary>
    public List<Funeral> Funerals { get; set; } = new();
    /// <summary>Venture id → the member of the house who began it, kept from the founding until the venture stands (docs/AMBITIONS.md).</summary>
    public Dictionary<string, string> Founders { get; set; } = new();
    /// <summary>Things a person owns that are worth leaving to someone (docs/WEALTH.md).</summary>
    public List<Heirloom> Heirlooms { get; set; } = new();
    /// <summary>Courtships under way and suits not yet answered (docs/MARRIAGE.md).</summary>
    public List<Courtship> Courtships { get; set; } = new();
    /// <summary>Every marriage, with its terms and its warmth (docs/MARRIAGE.md).</summary>
    public List<Marriage> Marriages { get; set; } = new();
    public Marriage? MarriageOf(string characterId) => Marriages.FirstOrDefault(m => m.Has(characterId));
    public List<Company> Companies { get; set; } = new();
    public List<Bond> Bonds { get; set; } = new();
    public Company? Company(string id) => Companies.FirstOrDefault(c => c.Id == id);

    /// <summary>The Reach's markets: commodity prices and the merchants' stocks (docs/ECONOMY_MARKET.md). Null until first reckoned.</summary>
    public MarketState? Market { get; set; }

    /// <summary>The Academy year, while the protagonist is a cadet and after (docs/ACADEMY.md); null in a run that began in the world.</summary>
    public AcademyState? Academy { get; set; }
    /// <summary>True while the cadet is at the Academy: the world does not turn and only the Academy's own business may be done.</summary>
    [JsonIgnore] public bool InAcademy => Academy is { Active: true };

    /// <summary>The foreign nations beyond the Reach as they stand (docs/FOREIGN_NATIONS.md): strength, regard, wars and tribute. Off the map.</summary>
    public List<Nation> Nations { get; set; } = new();
    public Nation? Nation(string id) => Nations.FirstOrDefault(n => n.Id == id);

    /// <summary>Senate factions: benches that vote together (GDD §6.1).</summary>
    public List<Faction> Factions { get; set; } = new();

    /// <summary>The offices of the Reach and who holds them (GDD §8).</summary>
    public List<OfficeHolding> Offices { get; set; } = new();

    /// <summary>Every passing of the seat, oldest first (GDD §14).</summary>
    public List<SuccessionRecord> Successions { get; set; } = new();

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

    /// <summary>The player's office and other presentation choices that belong to the saga, not the device (GDD §19 "the
    /// court is yours to furnish"): wall, floor, window, desk, banner... keyed by slot. Plain strings so any front-end can read them.</summary>
    public Dictionary<string, string> Cosmetics { get; set; } = new();

    /// <summary>Most-recently-fired event ids (newest last). Director suppression reads this (GDD §15 L4).</summary>
    public List<string> RecentEventIds { get; set; } = new();

    // ---- Convenience accessors (not serialised behaviour, just lookups) ----

    [JsonIgnore]
    public Character? Protagonist =>
        Characters.TryGetValue(ProtagonistId, out var c) ? c : null;

    public Character? Char(string id) => Characters.TryGetValue(id, out var c) ? c : null;

    public House? House(string id) => Houses.TryGetValue(id, out var h) ? h : null;

    public Holding? Holding(string id) => Holdings.TryGetValue(id, out var h) ? h : null;
    public Business? Business(string id) => Businesses.TryGetValue(id, out var b) ? b : null;
    public IEnumerable<Business> BusinessesOf(string houseId) => Businesses.Values.Where(b => b.OwnerId == houseId).OrderBy(b => b.Id, StringComparer.Ordinal);
    /// <summary>The house holding the planet in a system, or null when no one does.</summary>
    public string? PlanetHolder(string systemId)
    {
        Holding? best = null;   // the first by id, without sorting: this is asked thousands of times a turn
        foreach (var h in Holdings.Values) if (h.IsPlanet && h.SystemId == systemId && (best == null || string.CompareOrdinal(h.Id, best.Id) < 0)) best = h;
        return best?.OwnerId;
    }

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
        if (!Pools.TryGetValue(actorId, out var p))   // a character born or raised in play gets a rival's lean budget; anything else an empty one
            Pools[actorId] = p = Characters.ContainsKey(actorId) && actorId != ProtagonistId ? ActionPools.ForNpc() : new ActionPools { Cap = 0 };
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
