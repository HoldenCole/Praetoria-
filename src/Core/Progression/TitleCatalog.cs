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

    /// <summary>The rung's plain style where no catalog is at hand (a command's log line): the Republic's civic names until a throne stands,
    /// the noble ones after. Mirrors content/titles/titles.json; the catalog is the authority where one is loaded.</summary>
    public static string StyleOf(string title, string era) => era == State.World.EraImperium
        ? title switch { Knight => "Knight", Baron => "Baron", Count => "Count", Duke => "Duke", Archduke => "Archduke", Emperor => "Emperor", _ => "Landless" }
        : title switch { Knight => "Warden", Baron => "Prefect", Count => "Legate", Duke => "Proconsul", Archduke => "First Citizen", Emperor => "Dictator", _ => "Citizen" };

    /// <summary>The fixed ladder's rank of a title id (landless 0 … emperor 6); unknown titles rank 0.</summary>
    public static int RankOf(string title) => title switch { Knight => 1, Baron => 2, Count => 3, Duke => 4, Archduke => 5, Emperor => 6, _ => 0 };
}

/// <summary>
/// A title rung (GDD §13) — data: its rank on the ladder and the <see cref="LegitimacyRequirement"/>
/// needed to hold it <em>comfortably</em>. The requirement is the soft-lock (§13): you CAN hold a
/// title above your legitimacy, but it generates ongoing instability until your standing catches up.
/// </summary>
public sealed class TitleDef
{
    public string Id { get; init; } = "";
    /// <summary>What the rung is called under the Imperium, when noble titles exist: Baron, Count, Duke.</summary>
    public string ImperialName { get; init; } = "";
    /// <summary>What the same rung is called in the Republic, where there are no nobles: a civic or military style (Prefect, Legate, Proconsul).</summary>
    public string RepublicName { get; init; } = "";
    /// <summary>The rung's name in an era (docs/TITLES.md): the republic's civic style until a throne stands, the noble one after.</summary>
    public string NameIn(string era) => era == State.World.EraImperium || RepublicName.Length == 0 ? ImperialName : RepublicName;

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

    /// <summary>The display name of a title id in an era; the bare id when the catalog does not know it.</summary>
    public string NameOf(string titleId, string era) => ById(titleId)?.NameIn(era) ?? titleId;

    /// <summary>Rank of a title id (or 0 if unknown).</summary>
    public int RankOf(string titleId) => ById(titleId)?.Rank ?? 0;
}
