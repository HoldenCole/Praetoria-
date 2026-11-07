using Praetoria.Core.State;

namespace Praetoria.Core.Creation;

/// <summary>
/// A character background (GDD §13 "birth sets a baseline legitimacy"; §18 traits): where the protagonist
/// comes from before the scenario begins. Applied once at character creation on top of the scenario's
/// protagonist and house — a <em>delta</em>, so the same scenario plays differently per origin. Data from
/// /content/backgrounds.
/// </summary>
public sealed class BackgroundDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";

    /// <summary>Added to the house's legitimacy (may be negative — the upjumped start below the bar).</summary>
    public int Legitimacy { get; init; }

    /// <summary>Added to the house treasury (negative entries are debts paid at the start).</summary>
    public Resources Treasury { get; init; } = new();

    public IReadOnlyList<string> Nature { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Aptitude { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, int> Skills { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<string> Flags { get; init; } = Array.Empty<string>();
    public int Stress { get; init; }

    /// <summary>Suggested (not forced) track and ambition the creation UI pre-selects.</summary>
    public string? TrackHint { get; init; }
    public string? AmbitionHint { get; init; }
}

public sealed class BackgroundCatalog
{
    private readonly List<BackgroundDef> _defs;
    private readonly Dictionary<string, BackgroundDef> _byId;

    public BackgroundCatalog(IEnumerable<BackgroundDef> defs)
    {
        _defs = defs.ToList();
        _byId = _defs.ToDictionary(d => d.Id);
    }

    public static BackgroundCatalog Empty { get; } = new(Array.Empty<BackgroundDef>());
    public IReadOnlyList<BackgroundDef> Defs => _defs;
    public BackgroundDef? ById(string id) => _byId.TryGetValue(id, out var d) ? d : null;
    public bool IsEmpty => _defs.Count == 0;
}
