using System.Text.Json;

namespace Praetoria.Core.Data;

/// <summary>The Exchange's numbers (content/market/exchange.json `rules`): see docs/ECONOMY_MARKET.md for what each does.</summary>
public sealed class ExchangeRules
{
    public int Shares { get; init; } = 100;
    public double Multiple { get; init; } = 8;
    public int BookFloorPct { get; init; } = 40;
    public int EasePct { get; init; } = 50;
    public int FlowPct { get; init; } = 60;
    public int FlowDecayPct { get; init; } = 50;
    public int ImpactPct { get; init; } = 100;
    public int WobblePct { get; init; } = 3;
    public int FloatMaxPct { get; init; } = 25;
    public int FloatDiscountPct { get; init; } = 10;
    public int BuyCapPct { get; init; } = 20;
    public int IncorporateInfluence { get; init; } = 2;
    public int GoldenInfluence { get; init; } = 3;
    public int PublicStakePct { get; init; } = 10;
    public int ConfidenceEasePct { get; init; } = 40;
    public int ConfidenceFloorPct { get; init; } = 60;
    public int ConfidenceCapPct { get; init; } = 125;
    public int WarHomePct { get; init; } = -5;
    public int WarAbroadPct { get; init; } = -6;
    public int CrisisPct { get; init; } = -5;
    public int DebtPct { get; init; } = -15;
    public int PeacePct { get; init; } = 2;
    public int PeaceCapPct { get; init; } = 12;
    public int CrashPct { get; init; } = 15;
    public int FailCash { get; init; } = -30;
    public int MaxCompanies { get; init; } = 12;
    public int HistoryKept { get; init; } = 12;
    public int BondYieldPct { get; init; } = 6;
    public int BondWarYieldPct { get; init; } = 2;
    public int BondFace { get; init; } = 20;
    public int BondCap { get; init; } = 5;
    public int SteadyCost { get; init; } = 30;
    public int SteadyPct { get; init; } = 10;
    public int MonopolyRentPct { get; init; } = 20;
}

/// <summary>How one kind of business looks at the state of the Reach: per cent on its outlook for each war, for lawless lanes, for scarce goods.</summary>
public sealed record SectorDef(string Id, string Name, string Noun, int WarPct, int LawlessPct, int ScarcityPct);

/// <summary>A great company chartered by the Senate: no ventures, a steady income of its own, widely held.</summary>
public sealed record CharteredDef(string Id, string Name, string ImperialName, string Sector, int Income, string Good, string Description)
{
    public string NameIn(bool imperium) => imperium && ImperialName.Length > 0 ? ImperialName : Name;
}

public sealed class ExchangeCatalog
{
    private readonly List<SectorDef> _sectors; private readonly List<CharteredDef> _chartered;
    public ExchangeCatalog(ExchangeRules rules, IEnumerable<SectorDef> sectors, IEnumerable<CharteredDef> chartered) { Rules = rules; _sectors = sectors.ToList(); _chartered = chartered.ToList(); }
    public static ExchangeCatalog Empty { get; } = new(new ExchangeRules(), Array.Empty<SectorDef>(), Array.Empty<CharteredDef>());
    public ExchangeRules Rules { get; }
    public IReadOnlyList<SectorDef> Sectors => _sectors;
    public IReadOnlyList<CharteredDef> Chartered => _chartered;
    public SectorDef? Sector(string id) => _sectors.FirstOrDefault(s => s.Id == id);
    public CharteredDef? Charter(string id) => _chartered.FirstOrDefault(c => c.Id == id);
    public bool IsEmpty => _sectors.Count == 0;

    public static ExchangeCatalog LoadFromDirectory(string contentRoot)
    {
        var file = Path.Combine(contentRoot, "market", "exchange.json"); if (!File.Exists(file)) return Empty;
        using var doc = JsonDocument.Parse(File.ReadAllText(file), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var root = doc.RootElement; var d = new ExchangeRules();
        static double D(JsonElement el, string name, double fallback) => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : fallback;
        var rules = d;
        if (root.TryGetProperty("rules", out var r) && r.ValueKind == JsonValueKind.Object)
        {
            int I(string n, int f) => ConditionParser.Int(r, n, f);
            rules = new ExchangeRules
            {
                Shares = I("shares", d.Shares), Multiple = D(r, "multiple", d.Multiple), BookFloorPct = I("bookFloorPct", d.BookFloorPct), EasePct = I("easePct", d.EasePct), FlowPct = I("flowPct", d.FlowPct),
                FlowDecayPct = I("flowDecayPct", d.FlowDecayPct), ImpactPct = I("impactPct", d.ImpactPct), WobblePct = I("wobblePct", d.WobblePct), FloatMaxPct = I("floatMaxPct", d.FloatMaxPct),
                FloatDiscountPct = I("floatDiscountPct", d.FloatDiscountPct), BuyCapPct = I("buyCapPct", d.BuyCapPct), IncorporateInfluence = I("incorporateInfluence", d.IncorporateInfluence),
                GoldenInfluence = I("goldenInfluence", d.GoldenInfluence), PublicStakePct = I("publicStakePct", d.PublicStakePct), ConfidenceEasePct = I("confidenceEasePct", d.ConfidenceEasePct),
                ConfidenceFloorPct = I("confidenceFloorPct", d.ConfidenceFloorPct), ConfidenceCapPct = I("confidenceCapPct", d.ConfidenceCapPct), WarHomePct = I("warHomePct", d.WarHomePct),
                WarAbroadPct = I("warAbroadPct", d.WarAbroadPct), CrisisPct = I("crisisPct", d.CrisisPct), DebtPct = I("debtPct", d.DebtPct), PeacePct = I("peacePct", d.PeacePct), PeaceCapPct = I("peaceCapPct", d.PeaceCapPct),
                CrashPct = I("crashPct", d.CrashPct), FailCash = I("failCash", d.FailCash), MaxCompanies = I("maxCompanies", d.MaxCompanies), HistoryKept = I("historyKept", d.HistoryKept),
                BondYieldPct = I("bondYieldPct", d.BondYieldPct), BondWarYieldPct = I("bondWarYieldPct", d.BondWarYieldPct), BondFace = I("bondFace", d.BondFace), BondCap = I("bondCap", d.BondCap),
                SteadyCost = I("steadyCost", d.SteadyCost), SteadyPct = I("steadyPct", d.SteadyPct), MonopolyRentPct = I("monopolyRentPct", d.MonopolyRentPct)
            };
        }
        var sectors = new List<SectorDef>(); var chartered = new List<CharteredDef>();
        if (root.TryGetProperty("sectors", out var sa) && sa.ValueKind == JsonValueKind.Array)
            foreach (var el in sa.EnumerateArray())
                sectors.Add(new SectorDef(ConditionParser.Str(el, "id"), ConditionParser.OptStr(el, "name", ConditionParser.Str(el, "id")), ConditionParser.OptStr(el, "noun", "Company"),
                    ConditionParser.Int(el, "warPct", 0), ConditionParser.Int(el, "lawlessPct", 0), ConditionParser.Int(el, "scarcityPct", 0)));
        if (root.TryGetProperty("chartered", out var ca) && ca.ValueKind == JsonValueKind.Array)
            foreach (var el in ca.EnumerateArray())
                chartered.Add(new CharteredDef(ConditionParser.Str(el, "id"), ConditionParser.Str(el, "name"), ConditionParser.OptStr(el, "imperialName", ""), ConditionParser.OptStr(el, "sector", "trade"),
                    ConditionParser.Int(el, "income", 20), ConditionParser.OptStr(el, "good", ""), ConditionParser.OptStr(el, "description", "")));
        return new ExchangeCatalog(rules, sectors, chartered);
    }
}
