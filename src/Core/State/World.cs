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
    /// <summary>Set only inside a save made in the middle of a year: the year's reports as they stood, so that loading it opens the same
    /// year where it was left and not a fresh one (docs/PLAYTHROUGH.md, a first game). Null in a save made between years and in a
    /// world being played.</summary>
    public MidYear? MidYear { get; set; }

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
    /// <summary>The scheme of salaries on trial (<see cref="Systems.PayrollSystem"/>), or "" for none: nobody is paid, as in every game
    /// that is played. Set only by the measuring tool (docs/ECONOMY_REVIEW.md, "Salaries on trial").</summary>
    public string Payroll { get; set; } = "";

    /// <summary>How easy money is in this game (<see cref="Systems.MoneySettings"/>): "lean", "standard", "generous" or "lavish". It scales
    /// what everything that is built costs, for every house.</summary>
    public string MoneyEase { get; set; } = "standard";
    /// <summary>The boosts this game was begun with (<see cref="Systems.MoneySettings.Boosts"/>): the player's house's alone.</summary>
    public HashSet<string> Boosts { get; set; } = new();
    /// <summary>What <see cref="Boosts"/> was called for a day: read from a save made then, never written.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public HashSet<string>? Cheats { get => null; set { if (value != null) Boosts.UnionWith(value); } }

    /// <summary>GDD §20 "crisis intensity": off / low / default / dominant — how fast the Great Crisis clock runs.</summary>
    public string CrisisIntensity { get; set; } = "default";

    /// <summary>How strong the nations beyond the Reach are in this game: "gentle", "default" or "harsh" (docs/BEYOND.md).</summary>
    public string Beyond { get; set; } = "default";
    /// <summary>Whether each nation's strength is varied by this game's dice, so no two games have the same neighbours.</summary>
    public bool BeyondVaries { get; set; } = true;
    /// <summary>How readily the nations beyond the Reach attack in this game: "seldom", "default" or "often" (docs/BEYOND.md).</summary>
    public string BeyondAggression { get; set; } = "default";

    /// <summary>How big the map of this game is: "small" (the map as written), "medium", "large" or "extra_large" (docs/GALAXY_SIZE.md).</summary>
    public string GalaxySize { get; set; } = "small";
    /// <summary>The systems of this game's map in hundredths of the written map's: 100 for small.</summary>
    public int GalaxyScale { get; set; } = 100;

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
    public bool AtWar(string a, string b) => a != b && (a == "pirates" || b == "pirates" || Wars.Any(w => w.Between(a, b)) || NationWar(a, b) || NationWar(b, a));
    public IEnumerable<string> EnemiesOf(string houseId)
    {
        var list = Wars.Where(w => w.Involves(houseId)).Select(w => w.Other(houseId));
        // a nation on the map that the Reach is at war with is the enemy of every house of the Reach, and they of it, without a war for each pair (docs/BEYOND.md)
        // (unless the war is one house's own: then only that house and the nation are enemies)
        if (NationOfHouse(houseId) is { AtWar: true } theirs) list = list.Concat(Houses.Values.Where(h => OfTheReach(h.Id) && (theirs.WarOf.Length == 0 || theirs.WarOf == h.Id)).Select(h => h.Id));
        else if (OfTheReach(houseId)) list = list.Concat(Nations.Where(n => n.AtWar && n.OnMap && (n.WarOf.Length == 0 || n.WarOf == houseId)).Select(n => n.HouseId));
        return list.Distinct().OrderBy(h => h, StringComparer.Ordinal);
    }
    /// <summary>The nation a house stands for on the map, if it is one's.</summary>
    public Nation? NationOfHouse(string houseId) { foreach (var n in Nations) if (n.HouseId.Length > 0 && n.HouseId == houseId) return n; return null; }
    /// <summary>A house that is on the map and not of the Reach's world: a nation's. It has no seat, no vote and no place in the Reach's markets of marriage and office.</summary>
    public bool Foreign(string houseId) => State.Nation.IsHouse(houseId);
    /// <summary>Not a house of the Reach at all: the raiders, or the house that stands for a nation on the map. Such a house holds worlds
    /// and fleets and can be fought; it has no seat, no party, no marriages and no place on the Exchange.</summary>
    public static bool Outsider(string? houseId) => houseId == "pirates" || (houseId != null && State.Nation.IsHouse(houseId));
    /// <summary>A house of the Reach: not the raiders, not a nation's, and not gone into a state of its own.</summary>
    public bool OfTheReach(string houseId) => houseId != "pirates" && !Foreign(houseId) && House(houseId) is { PolityId.Length: 0 };
    private bool NationWar(string nationHouse, string other) => NationOfHouse(nationHouse) is { AtWar: true } n && OfTheReach(other) && (n.WarOf.Length == 0 || n.WarOf == other);

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
    /// <summary>What has been built on the bodies of the systems (docs/SYSTEMS.md).</summary>
    public List<BodyWork> BodyWorks { get; set; } = new();
    /// <summary>The infrastructure of each system (docs/BUILDINGS.md): system id → track id → level.</summary>
    public Dictionary<string, Dictionary<string, int>> Infrastructure { get; set; } = new();
    /// <summary>How developed each system is, 0 to 100 (docs/BUILDINGS.md): its level is a tenth of it. The main source of building slots.</summary>
    public Dictionary<string, int> Development { get; set; } = new();
    /// <summary>Slots cleared for coin, by place ("system#body").</summary>
    public Dictionary<string, int> ClearedSlots { get; set; } = new();
    /// <summary>Demands that a holder step down, waiting on the player's answer (docs/PROVINCES.md).</summary>
    public List<SeatDemand> SeatDemands { get; set; } = new();
    /// <summary>The battles fought, newest last: the last sixty are kept (docs/PLAYTHROUGH.md 10).</summary>
    public List<BattleRecord> Battles { get; set; } = new();
    /// <summary>The rulings of the courts that touched the player's house, newest last (docs/PLAYTHROUGH.md 12).</summary>
    public List<JudgmentRecord> Judgments { get; set; } = new();
    /// <summary>The deaths in the player's house, each with what it left empty and what passed to whom.</summary>
    public List<DeathRecord> Deaths { get; set; } = new();
    /// <summary>The count at the last election of the ladder: every name, its office and its votes.</summary>
    public List<CountRecord> Counts { get; set; } = new();
    /// <summary>What the player's house should be told (docs/PLAYTHROUGH.md 14), newest last. <see cref="NewsFrom"/> is where the
    /// year now being played began: everything after it is this year's news.</summary>
    public List<NewsItem> News { get; set; } = new();
    public int NewsSeq { get; set; }
    public int NewsFrom { get; set; }
    /// <summary>Tell the player's house something. Kept eighty deep.</summary>
    public void Tell(string tone, string text, string target, string subjectId = "")
    {
        News.Add(new NewsItem { Seq = ++NewsSeq, Turn = Turn, Tone = tone, Text = text, Target = target, SubjectId = subjectId });
        if (News.Count > 80) News.RemoveRange(0, News.Count - 80);
    }
    /// <summary>The news of the year being played.</summary>
    public IEnumerable<NewsItem> NewsNow() => News.Where(n => n.Seq > NewsFrom);
    /// <summary>Ventures offered for sale to a named house, until they lapse.</summary>
    public List<VentureOffer> Offers { get; set; } = new();
    public IEnumerable<Project> ProjectsOf(string houseId) => Projects.Where(p => p.OwnerId == houseId);
    /// <summary>The companies on the Exchange (docs/ECONOMY_MARKET.md stage C) and the Reach's war bonds (stage D).</summary>
    /// <summary>Who sits on each rung of the ladder of offices now (docs/LADDER.md). Several may hold the same rung.</summary>
    public List<OfficeHolding> Magistrates { get; set; } = new();
    /// <summary>Who of the player's house has said they will stand at the next election, and for what.</summary>
    public List<Candidacy> Candidacies { get; set; } = new();
    /// <summary>What was said over the dead of the player's house, most recent last (docs/LIFE.md).</summary>
    public List<Funeral> Funerals { get; set; } = new();
    /// <summary>This year's business for those who sit in the elected offices (docs/LADDER.md).</summary>
    public List<OfficeBusiness> OfficeWork { get; set; } = new();
    /// <summary>What magistrates did that can still be answered for (docs/LADDER.md, "The courts").</summary>
    public List<OfficeDeed> OfficeRecord { get; set; } = new();
    /// <summary>States that have broken away from the Reach (docs/BREAKAWAY.md).</summary>
    public List<Polity> Polities { get; set; } = new();
    /// <summary>Generals the Senate has ordered home (docs/CAESAR.md).</summary>
    public List<Recall> Recalls { get; set; } = new();
    /// <summary>The civil war, while a general marches on the capital (docs/CAESAR.md).</summary>
    public CivilWar? CivilWar { get; set; }
    /// <summary>The Senate's emergency powers, while a consul holds them (docs/EMERGENCY.md).</summary>
    public EmergencyPowers? Emergency { get; set; }
    /// <summary>A pretender's war for the throne, while it lasts (docs/THRONE.md).</summary>
    public ThroneRising? Rising { get; set; }
    /// <summary>Venture id → the member of the house who began it, kept from the founding until the venture stands (docs/AMBITIONS.md).</summary>
    public Dictionary<string, string> Founders { get; set; } = new();
    /// <summary>Things a person owns that are worth leaving to someone (docs/WEALTH.md).</summary>
    public List<Heirloom> Heirlooms { get; set; } = new();
    /// <summary>Blood owed between houses, standing and ended (docs/FEUD.md).</summary>
    public List<BloodFeud> Feuds { get; set; } = new();
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
    private Dictionary<string, Holding>? _planets; private int _planetsOf = -1;
    /// <summary>Forget what is remembered of which holding is each system's planet: call after holdings are removed and added in one go.</summary>
    public void HoldingsChanged() => _planets = null;
    /// <summary>The planet of a system, or null when it has none: the first by id. Remembered per set of holdings (a holding is added
    /// seldom and removed only before play), because this is asked thousands of times a turn and a large map has three times the holdings.</summary>
    public Holding? PlanetOf(string systemId)
    {
        if (_planets == null || _planetsOf != Holdings.Count)
        {
            _planets = new Dictionary<string, Holding>();
            foreach (var h in Holdings.Values) if (h.IsPlanet && (!_planets.TryGetValue(h.SystemId, out var had) || string.CompareOrdinal(h.Id, had.Id) < 0)) _planets[h.SystemId] = h;
            _planetsOf = Holdings.Count;
        }
        return _planets.TryGetValue(systemId, out var p) ? p : null;
    }
    private Dictionary<string, List<(Holding h, int i)>>? _provinces; private int _provincesOf = -1;
    /// <summary>Forget which house was given which province: call when one changes hands.</summary>
    public void ProvincesChanged() => _provinces = null;
    private static readonly List<(Holding h, int i)> NoProvinces = new();
    /// <summary>The provinces a house was given (docs/PROVINCES.md): not the seats of its own worlds, which go with the worlds.</summary>
    public IReadOnlyList<(Holding h, int i)> ProvincesOf(string houseId)
    {
        if (_provinces == null || _provincesOf != Holdings.Count)
        {
            _provinces = new Dictionary<string, List<(Holding h, int i)>>();
            foreach (var h in Holdings.Values.OrderBy(h => h.Id, StringComparer.Ordinal))
                for (int i = 1; i < h.Provinces.Count; i++)
                {
                    string id = h.Provinces[i].HolderId; if (id.Length == 0) continue;
                    if (!_provinces.TryGetValue(id, out var list)) _provinces[id] = list = new List<(Holding h, int i)>(); list.Add((h, i));
                }
            _provincesOf = Holdings.Count;
        }
        return _provinces.TryGetValue(houseId, out var mine) ? mine : NoProvinces;
    }
    /// <summary>The house holding the planet in a system, or null when no one does.</summary>
    public string? PlanetHolder(string systemId) => PlanetOf(systemId)?.OwnerId;

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

    /// <summary>The living people of a nation on the map: its ruler and the commander of its fleets. They can be met in battle and killed;
    /// they take no part in the Reach's own life, and LivingCharacters leaves them out.</summary>
    public IEnumerable<Character> Foreigners() { foreach (var c in Characters.Values) if (c.Alive && State.Nation.IsHouse(c.HouseId)) yield return c; }

    public IEnumerable<Character> LivingCharacters()
    {
        foreach (var c in Characters.Values)
            if (c.Alive && !State.Nation.IsHouse(c.HouseId)) yield return c;   // a nation's ruler and commander are people, but not of the Reach: see Foreigners
    }

    public void Log(string text, string? eventId = null) =>
        History.Add(new HistoryEntry { Turn = Turn, Text = text, EventId = eventId });
}

/// <summary>The year's reports as a save made in the middle of the year holds them.</summary>
public sealed class MidYear
{
    public List<PendingReport> Reports { get; set; } = new();
}
/// <summary>One report of the year: the event, who it was about, and the answer given if one was.</summary>
public sealed class PendingReport
{
    public string EventId { get; set; } = "";
    public Dictionary<string, string> Roles { get; set; } = new();
    public bool Resolved { get; set; }
    public string ChosenId { get; set; } = "";
}
