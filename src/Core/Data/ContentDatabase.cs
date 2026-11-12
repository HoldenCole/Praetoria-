using Praetoria.Core.Careers;
using Praetoria.Core.Creation;
using Praetoria.Core.Crises;
using Praetoria.Core.Events;
using Praetoria.Core.Progression;
using Praetoria.Core.Spheres;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Data;

/// <summary>
/// The loaded, validated content set (BuildSpec §2 /content). Events are logic; Texts are prose;
/// they are kept separate and joined only by id (GDD §15). This is what the engine reads from —
/// no content is hard-coded in Core (BuildSpec §7 "no hard-coded content in Core logic").
/// </summary>
public sealed class ContentDatabase
{
    public IReadOnlyList<EventDef> Events { get; }
    public IReadOnlyDictionary<string, EventText> Texts { get; }

    /// <summary>The domain-economy catalog (GDD §17). Empty for scenarios with no domain layer.</summary>
    public HoldingCatalog Holdings { get; }

    /// <summary>The ventures a house can found and the finance rules (content/businesses).</summary>
    public BusinessCatalog Businesses { get; }
    /// <summary>The motions the Senate hears (content/senate).</summary>
    public MotionCatalog Motions { get; }
    /// <summary>The offices of the Reach (content/senate/offices.json).</summary>
    public OfficeCatalog Offices { get; }
    /// <summary>Laws and estates (content/politics).</summary>
    public PolicyCatalog Policies { get; }
    /// <summary>The parties of the Republic (content/senate/parties.json; docs/REPUBLIC.md).</summary>
    public PartyCatalog Parties { get; }
    /// <summary>The foreign nations beyond the Reach (content/nations/nations.json; docs/FOREIGN_NATIONS.md).</summary>
    public NationCatalog Nations { get; }
    /// <summary>The Academy: its registers, subjects, Standards and lessons (content/academy/academy.json; docs/ACADEMY.md).</summary>
    public AcademyCatalog Academy { get; }

    /// <summary>The crisis definitions (GDD §16). Empty for scenarios with no crisis layer.</summary>
    public IReadOnlyList<CrisisDef> Crises { get; }

    /// <summary>The power-balance spheres (GDD §7). Empty for scenarios with no sphere layer.</summary>
    public SphereCatalog Spheres { get; }

    /// <summary>The title ladder (GDD §13). Empty for scenarios with no progression layer.</summary>
    public TitleCatalog Titles { get; }

    /// <summary>Career tracks + rank ladders (GDD §13). Empty ⇒ ranks are uncapped, unnamed integers.</summary>
    public CareerCatalog Careers { get; }

    /// <summary>The §8 vocabularies as data (nature, aptitude, ambitions) for character creation.</summary>
    public TraitCatalog Traits { get; }

    /// <summary>Character origins applied at creation (GDD §13 birth baseline).</summary>
    public BackgroundCatalog Backgrounds { get; }

    /// <summary>Fleet-layer tuning (ship cost, upkeep, readiness, intel) from /content/fleets.</summary>
    public FleetRules Fleets { get; }

    /// <summary>Hull classes (GDD §6 composition) from /content/fleets.</summary>
    public ShipClassCatalog ShipClasses { get; }

    /// <summary>Honorifics & Orders (GDD §13) from /content/honors.</summary>
    public IReadOnlyList<HonorDef> Honors { get; }

    /// <summary>Nature-trait friction (GDD §18) from /content/traits/friction.json.</summary>
    public FrictionTable Friction { get; }

    /// <summary>Given names by culture (/content/names) for children born in play.</summary>
    public NameCatalog Names { get; }

    /// <summary>The Great-Crisis archetype (GDD §16), or null when the content set has none.</summary>
    public GreatCrisisDef? GreatCrisis { get; }

    /// <summary>Counter recovery rules (crises file "recovery").</summary>
    public IReadOnlyList<RecoveryRule> Recovery { get; }

    private readonly Dictionary<string, EventDef> _eventsById;

    public ContentDatabase(
        IReadOnlyList<EventDef> events,
        IReadOnlyDictionary<string, EventText> texts,
        HoldingCatalog? holdings = null,
        IReadOnlyList<CrisisDef>? crises = null,
        SphereCatalog? spheres = null,
        TitleCatalog? titles = null,
        CareerCatalog? careers = null,
        TraitCatalog? traits = null,
        BackgroundCatalog? backgrounds = null,
        FleetRules? fleets = null,
        ShipClassCatalog? shipClasses = null,
        IReadOnlyList<HonorDef>? honors = null,
        FrictionTable? friction = null,
        NameCatalog? names = null,
        GreatCrisisDef? greatCrisis = null,
        IReadOnlyList<RecoveryRule>? recovery = null,
        BusinessCatalog? businesses = null,
        MotionCatalog? motions = null,
        OfficeCatalog? offices = null,
        PolicyCatalog? policies = null,
        PartyCatalog? parties = null,
        NationCatalog? nations = null,
        AcademyCatalog? academy = null)
    {
        Academy = academy ?? AcademyCatalog.Empty;
        Parties = parties ?? PartyCatalog.Empty;
        Nations = nations ?? NationCatalog.Empty;
        Businesses = businesses ?? BusinessCatalog.Empty;
        Motions = motions ?? MotionCatalog.Empty;
        Offices = offices ?? OfficeCatalog.Empty;
        Policies = policies ?? PolicyCatalog.Empty;
        Events = events;
        Texts = texts;
        Holdings = holdings ?? HoldingCatalog.Empty;
        Crises = crises ?? Array.Empty<CrisisDef>();
        Spheres = spheres ?? SphereCatalog.Empty;
        Titles = titles ?? TitleCatalog.Empty;
        Careers = careers ?? CareerCatalog.Empty;
        Traits = traits ?? TraitCatalog.Empty;
        Backgrounds = backgrounds ?? BackgroundCatalog.Empty;
        Fleets = fleets ?? FleetRules.Default;
        ShipClasses = shipClasses ?? ShipClassCatalog.Empty;
        Honors = honors ?? Array.Empty<HonorDef>();
        Friction = friction ?? FrictionTable.Empty;
        Names = names ?? NameCatalog.Empty;
        GreatCrisis = greatCrisis;
        Recovery = recovery ?? Array.Empty<RecoveryRule>();
        _eventsById = new Dictionary<string, EventDef>();
        foreach (var e in events) _eventsById[e.Id] = e;
    }

    public EventDef? Event(string id) => _eventsById.TryGetValue(id, out var e) ? e : null;
    public EventText? Text(string id) => Texts.TryGetValue(id, out var t) ? t : null;
}
