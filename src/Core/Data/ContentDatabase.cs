using Praetoria.Core.Crises;
using Praetoria.Core.Events;
using Praetoria.Core.Progression;
using Praetoria.Core.Spheres;

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

    /// <summary>The crisis definitions (GDD §16). Empty for scenarios with no crisis layer.</summary>
    public IReadOnlyList<CrisisDef> Crises { get; }

    /// <summary>The power-balance spheres (GDD §7). Empty for scenarios with no sphere layer.</summary>
    public SphereCatalog Spheres { get; }

    /// <summary>The title ladder (GDD §13). Empty for scenarios with no progression layer.</summary>
    public TitleCatalog Titles { get; }

    private readonly Dictionary<string, EventDef> _eventsById;

    public ContentDatabase(
        IReadOnlyList<EventDef> events,
        IReadOnlyDictionary<string, EventText> texts,
        HoldingCatalog? holdings = null,
        IReadOnlyList<CrisisDef>? crises = null,
        SphereCatalog? spheres = null,
        TitleCatalog? titles = null)
    {
        Events = events;
        Texts = texts;
        Holdings = holdings ?? HoldingCatalog.Empty;
        Crises = crises ?? Array.Empty<CrisisDef>();
        Spheres = spheres ?? SphereCatalog.Empty;
        Titles = titles ?? TitleCatalog.Empty;
        _eventsById = new Dictionary<string, EventDef>();
        foreach (var e in events) _eventsById[e.Id] = e;
    }

    public EventDef? Event(string id) => _eventsById.TryGetValue(id, out var e) ? e : null;
    public EventText? Text(string id) => Texts.TryGetValue(id, out var t) ? t : null;
}
