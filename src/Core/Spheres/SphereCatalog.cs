namespace Praetoria.Core.Spheres;

/// <summary>Canonical sphere keys (GDD §7). The estates imperial power is divided among. V1 ships
/// three; Church/Intelligence are later/DLC. Keyed strings (like <see cref="State.Resource"/>) used
/// in <see cref="State.House.SphereInfluence"/> and in the data catalog's career mapping.</summary>
public static class Sphere
{
    public const string Navy = "navy";         // fed by the military career track
    public const string Treasury = "treasury"; // fed by the stewardship career track
    public const string Senate = "senate";     // fed by the law/intrigue career track
}

/// <summary>
/// A sphere/estate definition (GDD §7) — data, not code: its display name and the career track that
/// feeds a house's influence in it. A Vega Grand Admiral spikes the family's Navy control because the
/// military track maps to the Navy sphere here.
/// </summary>
public sealed class SphereDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>The career track (<c>military</c>/<c>stewardship</c>/<c>law</c>) that feeds this sphere.</summary>
    public string CareerTrack { get; init; } = "";
}

/// <summary>
/// The loaded sphere set (BuildSpec §2 /content/spheres). Drives the §7 power-balance projection:
/// which career feeds which estate. An empty catalog means a scenario has no power-balance layer and
/// the sphere system is a no-op.
/// </summary>
public sealed class SphereCatalog
{
    private readonly List<SphereDef> _defs;
    private readonly Dictionary<string, SphereDef> _byId;
    private readonly Dictionary<string, SphereDef> _byTrack;

    public SphereCatalog(IEnumerable<SphereDef> defs)
    {
        _defs = defs.ToList();
        _byId = _defs.ToDictionary(d => d.Id);
        _byTrack = new Dictionary<string, SphereDef>();
        foreach (var d in _defs) _byTrack[d.CareerTrack] = d;
    }

    public static SphereCatalog Empty { get; } = new(Array.Empty<SphereDef>());

    public IReadOnlyList<SphereDef> Defs => _defs;
    public SphereDef? ById(string id) => _byId.TryGetValue(id, out var d) ? d : null;
    public SphereDef? ByTrack(string track) => _byTrack.TryGetValue(track, out var d) ? d : null;
    public bool IsEmpty => _defs.Count == 0;
}
