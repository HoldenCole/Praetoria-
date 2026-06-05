namespace Praetoria.Core.Progression;

/// <summary>Canonical title-ladder rungs (GDD §13). The dynasty ladder — persists across character
/// deaths (the House holds the title, not the person). Keyed strings; the ordered ladder + each
/// rung's legitimacy requirement live in the data catalog.</summary>
public static class Title
{
    public const string Landless = "landless";
    public const string Knight = "knight";
    public const string Baron = "baron";
    public const string Count = "count";
    public const string Duke = "duke";
    public const string Archduke = "archduke";
    public const string Emperor = "emperor";
}

/// <summary>
/// A title rung (GDD §13) — data: its rank on the ladder and the <see cref="LegitimacyRequirement"/>
/// needed to hold it <em>comfortably</em>. The requirement is the soft-lock (§13): you CAN hold a
/// title above your legitimacy, but it generates ongoing instability until your standing catches up.
/// </summary>
public sealed class TitleDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>Position on the ladder (Landless = 0 … Emperor = 6). Higher = greater.</summary>
    public int Rank { get; init; }

    /// <summary>Legitimacy needed to hold this title without instability (the §13 soft-lock).</summary>
    public int LegitimacyRequirement { get; init; }
}

/// <summary>
/// The loaded title ladder (BuildSpec §2 /content/titles, GDD §13). Ordered by rank; knows each
/// rung's legitimacy bar and how to step up. An empty catalog means a scenario has no title layer
/// and the progression system is a no-op.
/// </summary>
public sealed class TitleCatalog
{
    private readonly List<TitleDef> _byRank;
    private readonly Dictionary<string, TitleDef> _byId;

    public TitleCatalog(IEnumerable<TitleDef> defs)
    {
        _byRank = defs.OrderBy(d => d.Rank).ToList();
        _byId = _byRank.ToDictionary(d => d.Id);
    }

    public static TitleCatalog Empty { get; } = new(Array.Empty<TitleDef>());

    public IReadOnlyList<TitleDef> ByRank => _byRank;
    public TitleDef? ById(string id) => _byId.TryGetValue(id, out var d) ? d : null;
    public bool IsEmpty => _byRank.Count == 0;

    /// <summary>The next rung up from <paramref name="titleId"/>, or null if unknown / already top.</summary>
    public TitleDef? Next(string titleId)
    {
        var cur = ById(titleId);
        if (cur == null) return null;
        foreach (var d in _byRank)
            if (d.Rank == cur.Rank + 1) return d;
        return null;
    }

    /// <summary>Rank of a title id (or 0 if unknown).</summary>
    public int RankOf(string titleId) => ById(titleId)?.Rank ?? 0;
}
