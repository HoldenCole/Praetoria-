using System.Text.Json;
using Praetoria.Core.State;

namespace Praetoria.Core.Data;

/// <summary>
/// A kind of venture a house can found (GDD §17 businesses). Pure data: what it costs to found and expand, what it
/// yields per level (and flat, whatever the level), what it costs to run, the charter it pays the world's holder, and
/// where it may be founded.
/// </summary>
public sealed class BusinessDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public Resources Cost { get; init; } = new();
    /// <summary>Per level, each turn.</summary>
    public Resources Yield { get; init; } = new();
    /// <summary>Each turn, whatever the level.</summary>
    public Resources FlatYield { get; init; } = new();
    /// <summary>Per level, each turn.</summary>
    public Resources Upkeep { get; init; } = new();
    public Resources ExpandCost { get; init; } = new();
    public int MaxLevel { get; init; } = 3;
    /// <summary>Credits per level paid each turn to the house holding the planet in the system, when that is another house.</summary>
    public int Charter { get; init; }
    /// <summary>A bank: loans taken by other houses route their interest here.</summary>
    public bool Lender { get; init; }

    // where it can be founded
    public bool RequiresBelt { get; init; }
    public bool RequiresPopulated { get; init; }
    public bool RequiresOwnWorld { get; init; }
    /// <summary>A strategic deposit the system must hold; the venture's yield of that resource is multiplied by the richness.</summary>
    public string? RequiresDeposit { get; init; }
    public int MinLanes { get; init; }
    public int MinTitleRank { get; init; }
    public IReadOnlyList<string> PlanetClasses { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Specializations { get; init; } = Array.Empty<string>();

    /// <summary>Years to found one, and to raise it a level (docs/ECONOMY_MARKET.md stage B): the cost is paid over them. 0 = at once.</summary>
    public int BuildTurns { get; init; }
    /// <summary>What kind of business it is, for the Exchange's sector outlook: trade, extraction, arms, shipping, finance, services.</summary>
    public string Sector { get; init; } = "trade";
    /// <summary>Credits a level it earns (or loses) for every war in the Reach, four at most: an arsenal profits, an insurer bleeds.</summary>
    public int WarCredits { get; init; }
    /// <summary>Credits a level it earns in a year with no war at all.</summary>
    public int PeaceCredits { get; init; }
    /// <summary>Credits a level it earns for every good whose price is spiking: a warehouse sells into the shortage.</summary>
    public int SpikeCredits { get; init; }

    /// <summary>What a venture of this kind at a level is worth on the books.</summary>
    public int Value(int level) => Cost.Credits + Cost.Materials + Cost.Influence * 4 + Cost.Manpower / 2 + Math.Max(0, level - 1) * (ExpandCost.Credits + ExpandCost.Materials);
}

/// <summary>Finance rules (GDD §17): the standing loan, a belt claim's price, what an investment buys.</summary>
public sealed record FinanceRules(int LoanPrincipal, int LoanRepay, int LoanInstalment, int ClaimCredits, int ClaimMaterials, int InvestCredits, int InvestProsperity)
{
    public static FinanceRules Default { get; } = new(20, 26, 4, 6, 4, 6, 15);
}

public sealed class BusinessCatalog
{
    private readonly Dictionary<string, BusinessDef> _defs;
    public BusinessCatalog(IEnumerable<BusinessDef> defs, FinanceRules? finance = null) { _defs = defs.ToDictionary(d => d.Id); Finance = finance ?? FinanceRules.Default; }
    public static BusinessCatalog Empty { get; } = new(Array.Empty<BusinessDef>());
    public FinanceRules Finance { get; }
    public BusinessDef? Def(string id) => _defs.TryGetValue(id, out var d) ? d : null;
    public IReadOnlyCollection<BusinessDef> Defs => _defs.Values.OrderBy(d => d.Id, StringComparer.Ordinal).ToList();
    public bool IsEmpty => _defs.Count == 0;
}

public static class BusinessLoader
{
    private static readonly JsonDocumentOptions DocOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    /// <summary>Load /content/businesses. Missing dir ⇒ an empty catalog (ventures are then off, finance rules default).</summary>
    public static BusinessCatalog LoadFromDirectory(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "businesses");
        if (!Directory.Exists(dir)) return BusinessCatalog.Empty;
        var defs = new List<BusinessDef>(); FinanceRules? finance = null;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).OrderBy(static p => p, StringComparer.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file), DocOptions); var root = doc.RootElement;
                if (root.TryGetProperty("businesses", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    foreach (var el in arr.EnumerateArray())
                    {
                        var req = el.TryGetProperty("requires", out var r) && r.ValueKind == JsonValueKind.Object ? r : default;
                        defs.Add(new BusinessDef
                        {
                            Id = ConditionParser.Str(el, "id"), Name = ConditionParser.OptStr(el, "name", ConditionParser.Str(el, "id")), Description = ConditionParser.OptStr(el, "description", ""),
                            Cost = HoldingCatalogLoader.ParseResources(el, "cost"), Yield = HoldingCatalogLoader.ParseResources(el, "yield"), FlatYield = HoldingCatalogLoader.ParseResources(el, "flat"),
                            Upkeep = HoldingCatalogLoader.ParseResources(el, "upkeep"), ExpandCost = HoldingCatalogLoader.ParseResources(el, "expandCost"),
                            MaxLevel = ConditionParser.Int(el, "maxLevel", 3), Charter = ConditionParser.Int(el, "charter", 0), Lender = ConditionParser.Bool(el, "lender", false),
                            BuildTurns = ConditionParser.Int(el, "buildTurns", 0), Sector = ConditionParser.OptStr(el, "sector", "trade"),
                            WarCredits = ConditionParser.Int(el, "warCredits", 0), PeaceCredits = ConditionParser.Int(el, "peaceCredits", 0), SpikeCredits = ConditionParser.Int(el, "spikeCredits", 0),
                            RequiresBelt = req.ValueKind == JsonValueKind.Object && ConditionParser.Bool(req, "belt", false),
                            RequiresPopulated = req.ValueKind == JsonValueKind.Object && ConditionParser.Bool(req, "populated", false),
                            RequiresOwnWorld = req.ValueKind == JsonValueKind.Object && ConditionParser.Bool(req, "ownWorld", false),
                            RequiresDeposit = req.ValueKind == JsonValueKind.Object ? ConditionParser.OptStrOrNull(req, "deposit") : null,
                            MinLanes = req.ValueKind == JsonValueKind.Object ? ConditionParser.Int(req, "minLanes", 0) : 0,
                            MinTitleRank = req.ValueKind == JsonValueKind.Object ? ConditionParser.Int(req, "minTitleRank", 0) : 0,
                            PlanetClasses = req.ValueKind == JsonValueKind.Object ? StrList(req, "planetClasses") : Array.Empty<string>(),
                            Specializations = req.ValueKind == JsonValueKind.Object ? StrList(req, "specializations") : Array.Empty<string>()
                        });
                    }
                if (root.TryGetProperty("finance", out var f) && f.ValueKind == JsonValueKind.Object)
                {
                    var d = FinanceRules.Default;
                    finance = new FinanceRules(ConditionParser.Int(f, "loanPrincipal", d.LoanPrincipal), ConditionParser.Int(f, "loanRepay", d.LoanRepay), ConditionParser.Int(f, "loanInstalment", d.LoanInstalment),
                        ConditionParser.Int(f, "claimCredits", d.ClaimCredits), ConditionParser.Int(f, "claimMaterials", d.ClaimMaterials), ConditionParser.Int(f, "investCredits", d.InvestCredits), ConditionParser.Int(f, "investProsperity", d.InvestProsperity));
                }
            }
            catch (JsonException ex) { throw new ContentException($"Invalid JSON in '{file}': {ex.Message}", ex); }
        }
        return new BusinessCatalog(defs, finance);
    }

    private static IReadOnlyList<string> StrList(JsonElement el, string name)
    {
        var list = new List<string>();
        if (el.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var s in arr.EnumerateArray()) if (s.ValueKind == JsonValueKind.String) list.Add(s.GetString()!);
        return list;
    }
}
