using Praetoria.Core.State;

namespace Praetoria.Core.Data;

/// <summary>
/// A holding specialization (GDD §17 — agri-world, forge-world, fortress, trade-hub, …). Data, not
/// code: defines what a holding of this type yields each turn, what it costs to run, how many
/// building slots it offers, and how fast its population grows. The engine provides the mechanism
/// (accrual); data provides the instances (BuildSpec §2 "slots in V1, fillers in DLC").
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

    /// <summary>Defensive weight the building adds to fleets defending its system (bastion batteries).</summary>
    public double Defense { get; init; }

    public bool FitsSpecialization(string specialization) =>
        Requires == null || Requires == specialization;
}

/// <summary>
/// The loaded domain-economy catalog (BuildSpec §2 /content — holdings/specializations/buildings as
/// data). Joined to live <see cref="Holding"/>s by id at runtime, exactly as events join to text.
/// An empty catalog means a scenario has no domain layer, and the economy system becomes a no-op.
/// </summary>
public sealed class HoldingCatalog
{
    private readonly Dictionary<string, HoldingSpec> _specs;
    private readonly Dictionary<string, BuildingDef> _buildings;

    public HoldingCatalog(IEnumerable<HoldingSpec> specs, IEnumerable<BuildingDef> buildings)
    {
        _specs = specs.ToDictionary(s => s.Id);
        _buildings = buildings.ToDictionary(b => b.Id);
    }

    public static HoldingCatalog Empty { get; } =
        new(Array.Empty<HoldingSpec>(), Array.Empty<BuildingDef>());

    public HoldingSpec? Spec(string id) => _specs.TryGetValue(id, out var s) ? s : null;
    public BuildingDef? Building(string id) => _buildings.TryGetValue(id, out var b) ? b : null;

    public IReadOnlyCollection<HoldingSpec> Specs => _specs.Values;
    public IReadOnlyCollection<BuildingDef> Buildings => _buildings.Values;

    public bool IsEmpty => _specs.Count == 0 && _buildings.Count == 0;
}
