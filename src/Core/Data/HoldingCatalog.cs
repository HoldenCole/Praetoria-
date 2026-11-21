using Praetoria.Core.State;

namespace Praetoria.Core.Data;

/// <summary>
/// A holding specialization (GDD §17 — agri-world, forge-world, fortress, trade-hub, …). Data, not
/// code: defines what a holding of this type yields each turn, what it costs to run, how many
/// building slots it offers, and how fast its population grows. The engine provides the mechanism
/// (accrual); data provides the instances (BuildSpec §2 "slots first, fillers later").
/// </summary>
public sealed class HoldingSpec
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>Gross per-turn production before unrest suppression (GDD §17 conversion chains).</summary>
    public Resources BaseYield { get; init; } = new();

    /// <summary>Per-turn running cost, drawn from the owner's treasury.</summary>
    public Resources Upkeep { get; init; } = new();

    /// <summary>How many buildings can be slotted here (medium-baseline depth, GDD §17).</summary>
    public int Slots { get; init; }

    /// <summary>Population added each turn (populated specializations only). 0 ⇒ unpopulated.</summary>
    public int PopGrowth { get; init; }

    /// <summary>Population ceiling for this kind of world (GDD §17 — a mining outpost is not a hive).
    /// 0 ⇒ uncapped (the pre-0.5 behaviour).</summary>
    public int MaxPopulation { get; init; }

    /// <summary>People are soldiers: every this-many population adds +1 Manpower to the holding's
    /// per-turn yield, before unrest throttles it (GDD §17 population → manpower). 0 ⇒ population is
    /// decorative for this specialization.</summary>
    public int PopManpower { get; init; }

    /// <summary>Defensive weight a holding of this kind lends fleets defending its system (0.5 = +50% power).
    /// The fortress specializations' whole point (GDD §17 "marginal in most runs, decisive in a siege").</summary>
    public double Defense { get; init; }

    public bool Populated => PopGrowth > 0;
}

/// <summary>
/// A building the player can slot into a holding (GDD §17 — "boost energy" = power plant, "boost
/// navy capacity" = shipyard). One-time <see cref="Cost"/> from the treasury buys an ongoing
/// per-turn <see cref="Yield"/> (minus <see cref="Upkeep"/>). <see cref="Requires"/> optionally
/// pins it to one specialization (a shipyard only on a forge-world). Pure data.
/// </summary>
public sealed class BuildingDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public Resources Cost { get; init; } = new();
    public Resources Yield { get; init; } = new();
    public Resources Upkeep { get; init; } = new();

    /// <summary>Specialization id this building requires, or null to allow any holding.</summary>
    public string? Requires { get; init; }

    /// <summary>A strategic resource the system must hold a deposit of (GDD §17); the building's yield of that resource is multiplied by the richness.</summary>
    public string? RequiresDeposit { get; init; }

    /// <summary>Defensive weight the building adds to fleets defending its system (bastion batteries).</summary>
    public double Defense { get; init; }

    /// <summary>Minimum title rank of the building house (0 = anyone). Megastructures gate on it (GDD §17).</summary>
    public int MinTitleRank { get; init; }

    /// <summary>A megastructure: the §12/§17 "apocalypse-scale" slot. Presentation flags it; the engine treats it as a building.</summary>
    public bool Megastructure { get; init; }

    /// <summary>Years to raise it (docs/ECONOMY_MARKET.md stage B): the cost is paid over them. 0 = at once.</summary>
    public int BuildTurns { get; init; }

    public bool FitsSpecialization(string specialization) =>
        Requires == null || Requires == specialization;
}

/// <summary>Something that makes one solar system unlike another (docs/SYSTEMS.md): great rings, a wreck field, ruins older than the
/// Reach. It is found on a body of the kinds it names, and it opens one work that can be built nowhere else.</summary>
public sealed record FeatureDef(string Id, string Name, IReadOnlyList<string> Bodies, int Weight, string Description);

/// <summary>What it takes to settle a system nobody holds (docs/SYSTEMS.md): the cost, the years, and the colony it becomes.</summary>
public sealed record ColonyRule(Resources Cost, int BuildTurns, int Population, string Specialization)
{
    public static ColonyRule Default { get; } = new(new Resources { Credits = 30, Materials = 20, Manpower = 8, Influence = 2 }, 3, 6, "frontier_colony");
}

/// <summary>A track of a system's infrastructure (docs/BUILDINGS.md): the system's own, not any body's, and bought a level at a time.
/// Level N costs N times <see cref="Cost"/> and takes <see cref="Years"/>[N-1]. Each level does what the fields say, and adds to the
/// system's development at once and to how far it can grow (<see cref="BuildRules"/>).</summary>
public sealed class InfrastructureDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public int MaxLevel { get; init; } = 5;
    public Resources Cost { get; init; } = new();
    public IReadOnlyList<int> Years { get; init; } = Array.Empty<int>();
    public Resources Yield { get; init; } = new();
    public double Defense { get; init; }
    public int Prosperity { get; init; }
    public int Population { get; init; }
    /// <summary>Levels at which the world's unrest falls a point a year more.</summary>
    public IReadOnlyList<int> CalmAt { get; init; } = Array.Empty<int>();
    /// <summary>The level from which the system supplies fleets as a depot does; 0 = never.</summary>
    public int DepotAt { get; init; }
    public Resources CostOf(int level) { var c = new Resources(); foreach (var r in Resource.All) c.Set(r, Cost.Get(r) * Math.Max(1, level)); return c; }
    public int YearsOf(int level) => level >= 1 && level <= Years.Count ? Math.Max(1, Years[level - 1]) : 1;
}

/// <summary>How many slots a place has, how development grows, and how many great works a house may raise at once (docs/BUILDINGS.md;
/// the owner, 2026-11-20: "three things ... determine how many slots you have ... development ... rank ... and if you have more money,
/// you can potentially clear obstacles or just pay to unlock more slots"). Content: works.json, `slotRules`, `development`, `greatWorks`.</summary>
public sealed class BuildRules
{
    /// <summary>The most a home world, and any other place, can ever show: what the screen has room for.</summary>
    public int MaxWorld { get; init; } = 10; public int MaxBody { get; init; } = 5;
    /// <summary>The development levels at which a home world, and every other place, gains a slot.</summary>
    public IReadOnlyList<int> DevelopmentWorld { get; init; } = new[] { 2, 4, 6, 8, 10 }; public IReadOnlyList<int> DevelopmentBody { get; init; } = new[] { 3, 6, 9 };
    /// <summary>The title ranks of the holder at which a home world, and every other place, gains a slot.</summary>
    public IReadOnlyList<int> RankWorld { get; init; } = new[] { 2, 4, 6 }; public IReadOnlyList<int> RankBody { get; init; } = new[] { 3, 5 };
    /// <summary>How many slots may be cleared for coin on a home world and on any other place, and what the first costs (the Nth costs N times it).</summary>
    public int ClearMaxWorld { get; init; } = 3; public int ClearMaxBody { get; init; } = 2; public Resources ClearCost { get; init; } = new() { Credits = 14, Materials = 10 };
    /// <summary>The development level a building's first, second and third level needs.</summary>
    public IReadOnlyList<int> UpgradeAt { get; init; } = new[] { 0, 3, 6 };
    /// <summary>Slots more on the home world of a system no other house holds anything in.</summary>
    public int Undivided { get; init; } = 1;
    /// <summary>Development: what a level of infrastructure adds at once; the ceiling a bare system grows to, what each level of infrastructure
    /// adds to it, and how many people add a point; the prosperity at which a world grows twice as fast; what a year of siege or revolt costs.</summary>
    public int DevPerLevel { get; init; } = 3; public int DevCeiling { get; init; } = 30; public int DevCeilingPerLevel { get; init; } = 4; public int DevCeilingPerPeople { get; init; } = 3;
    public int DevProsperous { get; init; } = 65; public int DevSiege { get; init; } = -2;
    /// <summary>What a held system starts with: so much, and a point for every so many of its people (never more than its ceiling).</summary>
    public int DevSeed { get; init; } = 6; public int DevSeedPerPeople { get; init; } = 2;
    /// <summary>Great works a house may raise at once: any house, the leader of a region, a head of state, an emperor.</summary>
    public int CapacityHouse { get; init; } = 1; public int CapacityRegionLeader { get; init; } = 2; public int CapacityHeadOfState { get; init; } = 3; public int CapacityEmperor { get; init; } = 5;
    /// <summary>What a hastened great work pays each year beyond its two instalments, in hundredths of them.</summary>
    public int HastenPct { get; init; } = 50;
    /// <summary>The manpower a work gang takes each year it is on a work. The men are not lost for good: the house simply musters
    /// that many fewer while the work lasts.</summary>
    public int GangManpower { get; init; } = 5;
    public int MaxLevel => Math.Max(1, UpgradeAt.Count);
    public Resources ClearCostOf(int already) { var c = new Resources(); foreach (var r in Resource.All) c.Set(r, ClearCost.Get(r) * (already + 1)); return c; }
}

/// <summary>What a province of a world is given over to (docs/PROVINCES.md): what it yields its holder a year, what it adds to the
/// system's defence, and what kind of world it is found on. Content: provinces.json.</summary>
public sealed class ProvinceFocus
{
    public string Id { get; init; } = ""; public string Name { get; init; } = ""; public string Title { get; init; } = "{0}"; public string Description { get; init; } = "";
    public string Deposit { get; init; } = ""; public bool Military { get; init; } public IReadOnlyList<string> Classes { get; init; } = Array.Empty<string>();
    public Resources Yield { get; init; } = new(); public double Defense { get; init; }
}
/// <summary>A kind of city (docs/CITIES.md): the chief city of a province is of one kind, and the kind is what is special about
/// it. What it gives each year goes to whoever holds the province; the rest falls on the world. Pure data.</summary>
public sealed class CityKind
{
    public string Id { get; init; } = ""; public string Name { get; init; } = ""; public string What { get; init; } = "";
    /// <summary>One of the four great cities of the Reach, of which there is one each; and where it is put.</summary>
    public bool Great { get; init; } public string Place { get; init; } = "";
    public IReadOnlyList<string> Focuses { get; init; } = Array.Empty<string>(); public IReadOnlyList<string> Classes { get; init; } = Array.Empty<string>();
    public Resources Yield { get; init; } = new(); public int Prosperity { get; init; } public int Unrest { get; init; } public double Defense { get; init; } public int Muster { get; init; }
    /// <summary>The part of the world's citizens that live in the city, in thousandths; and the one building only a city of this kind takes.</summary>
    public int Share { get; init; } = 20; public string Building { get; init; } = "";
}
/// <summary>The kinds of city, and the parts their names are made of.</summary>
public sealed class CityRules
{
    public const string Capital = "capital";
    public IReadOnlyList<CityKind> Kinds { get; init; } = Array.Empty<CityKind>();
    public IReadOnlyList<string> First { get; init; } = Array.Empty<string>(); public IReadOnlyList<string> Last { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Patterns { get; init; } = new Dictionary<string, IReadOnlyList<string>>();
    /// <summary>Lesser cities and towns for each million of a world's citizens that do not live in its chief cities, in thousandths.</summary>
    public int LesserPerMillion { get; init; } = 60;
    public CityKind? Kind(string id) { foreach (var k in Kinds) if (k.Id == id) return k; return null; }
    public bool IsEmpty => Kinds.Count == 0;
}

/// <summary>The rules of provinces: the focuses, what improving one costs, and what a demand that a holder step down costs and risks.</summary>
public sealed class ProvinceRules
{
    public const string Seat = "seat";
    public IReadOnlyList<ProvinceFocus> Focuses { get; init; } = Array.Empty<ProvinceFocus>();
    public Resources ImproveCost { get; init; } = new() { Credits = 10, Materials = 8 };
    public int DemandInfluence { get; init; } = 3; public int DemandInfluenceWorld { get; init; } = 6; public int YieldsAt { get; init; } = 150; public int StepDownLegitimacy { get; init; } = -6; public int AnswerYears { get; init; } = 1;
    public int NpcProvinceYears { get; init; } = 30; public int NpcWorldYears { get; init; } = 60;
    /// <summary>The building slots of a province other than the seat: what it starts with, the development levels and the title ranks of its holder that add one, and the most.</summary>
    public int SlotBase { get; init; } = 1; public IReadOnlyList<int> SlotDevelopment { get; init; } = new[] { 3, 6, 9 }; public IReadOnlyList<int> SlotRank { get; init; } = new[] { 3, 5 }; public int SlotMax { get; init; } = 4;
    /// <summary>What a house that holds a province under another owes the holder of the world a year, for each level of the province: always, and in a year the holder of the world is at war.</summary>
    public Resources Tithe { get; init; } = new() { Credits = 1 }; public Resources Levy { get; init; } = new() { Manpower = 1 };
    /// <summary>Hulls a paying province sends, for each of its levels, while the holder of its world is at war; and how many times the player may vote against the house over it before that house asks for the province.</summary>
    public int LevySquadron { get; init; } = 1; public int Defiances { get; init; } = 2;
    /// <summary>What a house loses for each hull of a levy of its own that is destroyed, and what its head then thinks the less of the head it was sent to; how many years of tithe commute the dues, and for how long; how long a peace settles what was fought over.</summary>
    public Resources LevyLost { get; init; } = new() { Manpower = 2 }; public int LevyLostRegard { get; init; } = -8; public int CommuteYears { get; init; } = 6; public int CommuteTerm { get; init; } = 10; public int SettleYears { get; init; } = 10;
    /// <summary>Rungs of title lost by a house that steps down from a world.</summary>
    public int StepDownRanks { get; init; } = 1;
    public ProvinceFocus? Focus(string id) { foreach (var f in Focuses) if (f.Id == id) return f; return null; }
    public Resources ImproveCostOf(int level) { var c = new Resources(); foreach (var r in Resource.All) c.Set(r, ImproveCost.Get(r) * Math.Max(1, level)); return c; }
}

/// <summary>A work: what the holder of a system builds on one of its bodies (docs/SYSTEMS.md) — a colony, a mine, a base, a
/// megastructure, or a wonder that only one feature allows. Its yearly <see cref="Yield"/> less <see cref="Upkeep"/> goes to whoever
/// holds the system's world; the rest of what it does is in the fields. Pure data.</summary>
public sealed class WorkDef
{
    public const string Colony = "colony", Extraction = "extraction", Military = "military", Orbital = "orbital", Megastructure = "megastructure", Wonder = "wonder";
    public const string PerBody = "body", PerSystem = "system", PerHouse = "house", PerGalaxy = "galaxy";
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Category { get; init; } = Orbital;
    public string Description { get; init; } = "";
    /// <summary>The kinds of body it suits; empty = any but the sun.</summary>
    public IReadOnlyList<string> Bodies { get; init; } = Array.Empty<string>();
    /// <summary>The feature the body must have, or "".</summary>
    public string Feature { get; init; } = "";
    /// <summary>The deposit the body must carry, or "": the work brings up richness-many of what it yields of that good.</summary>
    public string Deposit { get; init; } = "";
    /// <summary>A work that must already stand on the same body, or "".</summary>
    public string Requires { get; init; } = "";
    /// <summary>How many may stand: one on a body (the default), one in a system, one in all a house holds, one in the galaxy.</summary>
    public string Limit { get; init; } = PerBody;
    /// <summary>Only in a system that is a key of the lanes.</summary>
    public bool Chokepoint { get; init; }
    public Resources Cost { get; init; } = new();
    public Resources Yield { get; init; } = new();
    public Resources Upkeep { get; init; } = new();
    public int BuildTurns { get; init; }
    public int MinTitleRank { get; init; }
    /// <summary>A great work (docs/BUILDINGS.md): it takes no building slot but the body's one site, costs far more, takes many years,
    /// and a house raises one at a time. Everything else is a building, and goes in a slot.</summary>
    public bool IsMegastructure { get; init; }
    /// <summary>"world" = only on the system's own world; "" = wherever its kind of body is.</summary>
    public string On { get; init; } = "";
    /// <summary>A building that is not for a home world, though its kind of body is right: an outpost, a colony.</summary>
    public bool NotHome { get; init; }
    public double Defense { get; init; }
    /// <summary>Supplies fleets as a supply depot does.</summary>
    public bool Depot { get; init; }
    /// <summary>Added to where the world's prosperity is heading.</summary>
    public int Prosperity { get; init; }
    /// <summary>Added to the world's unrest every year (negative calms it).</summary>
    public int Unrest { get; init; }
    /// <summary>Added to the most the world can hold.</summary>
    public int Population { get; init; }
    /// <summary>Given once, when it is finished: legitimacy to the house, and glory.</summary>
    public int Legitimacy { get; init; }
    public int Glory { get; init; }
    public bool Fits(string bodyClass) => Bodies.Count == 0 ? bodyClass != "star" : Bodies.Contains(bodyClass);
    /// <summary>The kind of city this is raised in and nowhere else ("" for a building any place takes): a guild hall in a city of
    /// craftsmen, the Senate's precinct in the capital of the Reach (docs/CITIES.md).</summary>
    public string City { get; init; } = "";
}

/// <summary>
/// The loaded domain-economy catalog (BuildSpec §2 /content — holdings/specializations/buildings as
/// data). Joined to live <see cref="Holding"/>s by id at runtime, exactly as events join to text.
/// An empty catalog means a scenario has no domain layer, and the economy system becomes a no-op.
/// </summary>
/// <summary>One conversion the Exchange offers (GDD §17 conversion chains).</summary>
public sealed record ExchangeRate(string Give, string Get, int GiveAmount, int GetAmount);

/// <summary>A standing tax posture (GDD §17): credits per holding per turn vs unrest drift on populated worlds.</summary>
public sealed record TaxPolicy(string Id, string Name, int CreditsBonus, int UnrestDrift, int LegitimacyPerTurn);

public sealed class HoldingCatalog
{
    private readonly Dictionary<string, HoldingSpec> _specs;
    private readonly Dictionary<string, BuildingDef> _buildings;

    public HoldingCatalog(IEnumerable<HoldingSpec> specs, IEnumerable<BuildingDef> buildings,
        IEnumerable<ExchangeRate>? rates = null, IEnumerable<string>? exchangeBuildings = null,
        IEnumerable<TaxPolicy>? taxPolicies = null, int contiguityBonus = 0)
    {
        _specs = specs.ToDictionary(s => s.Id);
        _buildings = buildings.ToDictionary(b => b.Id);
        Rates = (rates ?? Array.Empty<ExchangeRate>()).ToList();
        ExchangeBuildings = (exchangeBuildings ?? Array.Empty<string>()).ToHashSet();
        TaxPolicies = (taxPolicies ?? Array.Empty<TaxPolicy>()).ToList();
        ContiguityBonus = contiguityBonus;
    }

    public IReadOnlyList<ExchangeRate> Rates { get; }
    public IReadOnlySet<string> ExchangeBuildings { get; }
    public IReadOnlyList<TaxPolicy> TaxPolicies { get; }
    public TaxPolicy? Tax(string id) => TaxPolicies.FirstOrDefault(t => t.Id == id);
    /// <summary>Credits a holding earns per adjacent system its house also holds (GDD §19 territory).</summary>
    public int ContiguityBonus { get; }

    public static HoldingCatalog Empty { get; } =
        new(Array.Empty<HoldingSpec>(), Array.Empty<BuildingDef>());

    /// <summary>What may be built on the bodies of a system, what makes one system unlike another, and what a colony takes (docs/SYSTEMS.md).
    /// They ride in this catalog because everything that reckons a house's year already has it in hand.</summary>
    public IReadOnlyList<WorkDef> Works { get; init; } = Array.Empty<WorkDef>();
    public IReadOnlyList<FeatureDef> Features { get; init; } = Array.Empty<FeatureDef>();
    public ColonyRule Colony { get; init; } = ColonyRule.Default;
    /// <summary>Building slots by kind of body, the slots of a small rock or ice-dwarf, and the tracks of a system's infrastructure (docs/BUILDINGS.md).</summary>
    public IReadOnlyDictionary<string, int> BodySlots { get; init; } = new Dictionary<string, int>();
    public int SmallSlots { get; init; } = 1;
    public IReadOnlyList<InfrastructureDef> Infrastructure { get; init; } = Array.Empty<InfrastructureDef>();
    public BuildRules Rules { get; init; } = new();
    public ProvinceRules Provinces { get; init; } = new();
    public CityRules Cities { get; init; } = new();
    public InfrastructureDef? Track(string id) => Infrastructure.FirstOrDefault(t => t.Id == id);
    private Dictionary<string, WorkDef>? _works;
    public WorkDef? Work(string id) => (_works ??= Works.ToDictionary(w => w.Id)).TryGetValue(id, out var d) ? d : null;
    public FeatureDef? Feature(string id) => Features.FirstOrDefault(f => f.Id == id);

    public HoldingSpec? Spec(string id) => _specs.TryGetValue(id, out var s) ? s : null;
    public BuildingDef? Building(string id) => _buildings.TryGetValue(id, out var b) ? b : null;

    public IReadOnlyCollection<HoldingSpec> Specs => _specs.Values;
    public IReadOnlyCollection<BuildingDef> Buildings => _buildings.Values;

    public bool IsEmpty => _specs.Count == 0 && _buildings.Count == 0;
}
