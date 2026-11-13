using System.Text.Json;

namespace Praetoria.Core.Data;

/// <summary>A good the Reach trades (content/market/goods.json).</summary>
public sealed class GoodDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public double Base { get; init; } = 1;
    public double Sensitivity { get; init; } = 2;
    public int WarPct { get; init; }
    public double MinDemand { get; init; } = 4;
    public double MinStock { get; init; } = 6;
    public bool Buy { get; init; } = true;
    public bool Sell { get; init; } = true;
    /// <summary>A law effect of the Reach that must be in force for a house to buy this good; "" for none.</summary>
    public string License { get; init; } = "";
}

/// <summary>The market's numbers (the `rules` object): see the content file for what each does.</summary>
public sealed class MarketRules
{
    public int SharePct { get; init; } = 25;
    public double CoverYears { get; init; } = 3;
    public int RelaxPct { get; init; } = 20;
    public int ShortPct { get; init; } = 50;
    public int LongPct { get; init; } = 8;
    public int EasePct { get; init; } = 50;
    public int SpreadPct { get; init; } = 10;
    public int CaravanSpreadPct { get; init; } = 30;
    public int ImpactPct { get; init; } = 100;
    public int DepthPct { get; init; } = 100;
    public int DeficitPct { get; init; } = 100;
    public int FloorPct { get; init; } = 50;
    public int CapPct { get; init; } = 300;
    public int HistoryKept { get; init; } = 12;
    public int SpikePct { get; init; } = 150;
    public int SlumpPct { get; init; } = 70;
}

public sealed class MarketCatalog
{
    private readonly List<GoodDef> _goods;
    public MarketCatalog(MarketRules rules, IEnumerable<GoodDef> goods) { Rules = rules; _goods = goods.ToList(); }
    public static MarketCatalog Empty { get; } = new(new MarketRules(), Array.Empty<GoodDef>());
    public MarketRules Rules { get; }
    public IReadOnlyList<GoodDef> Goods => _goods;
    public GoodDef? Good(string id) => _goods.FirstOrDefault(g => g.Id == id);
    public bool IsEmpty => _goods.Count == 0;

    public static MarketCatalog LoadFromDirectory(string contentRoot)
    {
        var file = Path.Combine(contentRoot, "market", "goods.json"); if (!File.Exists(file)) return Empty;
        using var doc = JsonDocument.Parse(File.ReadAllText(file), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var root = doc.RootElement;
        static double D(JsonElement el, string name, double fallback) => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : fallback;
        var rules = new MarketRules();
        if (root.TryGetProperty("rules", out var r) && r.ValueKind == JsonValueKind.Object)
            rules = new MarketRules
            {
                SharePct = ConditionParser.Int(r, "sharePct", 25), CoverYears = D(r, "coverYears", 3), RelaxPct = ConditionParser.Int(r, "relaxPct", 20), ShortPct = ConditionParser.Int(r, "shortPct", 50),
                LongPct = ConditionParser.Int(r, "longPct", 8), EasePct = ConditionParser.Int(r, "easePct", 50), SpreadPct = ConditionParser.Int(r, "spreadPct", 10), CaravanSpreadPct = ConditionParser.Int(r, "caravanSpreadPct", 30),
                ImpactPct = ConditionParser.Int(r, "impactPct", 100), DepthPct = ConditionParser.Int(r, "depthPct", 100), DeficitPct = ConditionParser.Int(r, "deficitPct", 100), FloorPct = ConditionParser.Int(r, "floorPct", 50),
                CapPct = ConditionParser.Int(r, "capPct", 300), HistoryKept = ConditionParser.Int(r, "historyKept", 12), SpikePct = ConditionParser.Int(r, "spikePct", 150), SlumpPct = ConditionParser.Int(r, "slumpPct", 70)
            };
        var goods = new List<GoodDef>();
        if (root.TryGetProperty("goods", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var el in arr.EnumerateArray())
                if (el.ValueKind == JsonValueKind.Object)
                    goods.Add(new GoodDef
                    {
                        Id = ConditionParser.Str(el, "id"), Name = ConditionParser.OptStr(el, "name", ConditionParser.Str(el, "id")), Base = D(el, "base", 1), Sensitivity = D(el, "sensitivity", 2),
                        WarPct = ConditionParser.Int(el, "warPct", 0), MinDemand = D(el, "minDemand", 4), MinStock = D(el, "minStock", 6), Buy = ConditionParser.Bool(el, "buy", true), Sell = ConditionParser.Bool(el, "sell", true),
                        License = ConditionParser.OptStr(el, "license", "")
                    });
        return new MarketCatalog(rules, goods);
    }
}

/// <summary>What a trade needs to be priced: the goods and rules, the holdings catalog (which buildings are a market), the laws (tariffs, licences).</summary>
public sealed record TradeRules(MarketCatalog Market, HoldingCatalog Holdings, PolicyCatalog Policies)
{
    public static TradeRules Empty { get; } = new(MarketCatalog.Empty, HoldingCatalog.Empty, PolicyCatalog.Empty);
}
