namespace Praetoria.Core.Careers;

/// <summary>Canonical career-track keys (GDD §13). V1 ships three; Intelligence/Church/Science are DLC slots.</summary>
public static class CareerTrack
{
    public const string Military = "military";
    public const string Stewardship = "stewardship";
    public const string Law = "law";
}

/// <summary>
/// A career track and its rank ladder (GDD §13, BuildSpec §5 "track + rank ladders from data"). Rank
/// <c>i</c> is named <c>Ranks[i]</c>; the ladder's top is <c>Ranks.Count − 1</c>, which caps
/// <c>advanceCareer</c> — a Grand Admiral has nowhere higher to be promoted. Data, not code.
/// </summary>
public sealed class CareerTrackDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    /// <summary>The ladder as the Republic names it. Its length is the ladder's length in every era.</summary>
    public IReadOnlyList<string> Ranks { get; init; } = Array.Empty<string>();
    /// <summary>The same rungs as the Imperium names them (docs/TITLES.md); a rung missing here keeps its republic name.</summary>
    public IReadOnlyList<string> ImperialRanks { get; init; } = Array.Empty<string>();
    /// <summary>The track's name under the Imperium ("the Imperial Navy"), or "" to keep <see cref="Name"/>.</summary>
    public string ImperialName { get; init; } = "";
    public string NameIn(string era) => era == State.World.EraImperium && ImperialName.Length > 0 ? ImperialName : Name;

    /// <summary>The highest rank index on this ladder (0 if the ladder is empty).</summary>
    public int MaxRank => Math.Max(0, Ranks.Count - 1);

    public string? RankName(int rank, string era) =>
        rank < 0 || rank >= Ranks.Count ? null : era == State.World.EraImperium && rank < ImperialRanks.Count && ImperialRanks[rank].Length > 0 ? ImperialRanks[rank] : Ranks[rank];
}

/// <summary>The loaded career ladders (BuildSpec §2 /content/careers). Empty ⇒ no ladder layer: ranks
/// stay bare, uncapped integers (the pre-catalog behaviour).</summary>
public sealed class CareerCatalog
{
    private readonly List<CareerTrackDef> _defs;
    private readonly Dictionary<string, CareerTrackDef> _byId;

    public CareerCatalog(IEnumerable<CareerTrackDef> defs)
    {
        _defs = defs.ToList();
        _byId = _defs.ToDictionary(d => d.Id);
    }

    public static CareerCatalog Empty { get; } = new(Array.Empty<CareerTrackDef>());

    public IReadOnlyList<CareerTrackDef> Defs => _defs;
    public CareerTrackDef? ById(string id) => _byId.TryGetValue(id, out var d) ? d : null;
    public bool IsEmpty => _defs.Count == 0;

    /// <summary>The ladder top for a track, or null when the track isn't in the catalog (no cap).</summary>
    public int? MaxRank(string track) => ById(track)?.MaxRank;

    /// <summary>"Lieutenant" for (military, 2); null if the track or rank is unknown.</summary>
    public string? RankName(string track, int rank, string era) => ById(track)?.RankName(rank, era);
}
