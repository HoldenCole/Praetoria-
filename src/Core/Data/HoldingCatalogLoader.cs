using System.Text.Json;
using Praetoria.Core.State;

namespace Praetoria.Core.Data;

/// <summary>
/// Loads the domain-economy catalog from /content/holdings: specialization definitions and building
/// definitions (BuildSpec §2 — holdings are data from line one). Resource bundles are written as
/// JSON objects (e.g. <c>"yield": { "materials": 3, "credits": 1 }</c>) and parsed the same way
/// everywhere. Mirrors <see cref="ContentLoader"/>'s style; the loaded catalog rides in the
/// <see cref="ContentDatabase"/>.
/// </summary>
public static class HoldingCatalogLoader
{
    private static readonly JsonDocumentOptions DocOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>Load the catalog from a /content/holdings directory. Missing dir ⇒ empty catalog.</summary>
    public static HoldingCatalog LoadFromDirectory(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "holdings");
        if (!Directory.Exists(dir)) return HoldingCatalog.Empty;

        var specs = new List<HoldingSpec>();
        var buildings = new List<BuildingDef>();
        var rates = new List<ExchangeRate>();
        var exchangeBuildings = new List<string>();
        var taxes = new List<TaxPolicy>();
        var works = new List<WorkDef>(); var features = new List<FeatureDef>(); ColonyRule colony = ColonyRule.Default;
        var bodySlots = new Dictionary<string, int>(); int smallSlots = 1; var tracks = new List<InfrastructureDef>(); BuildRules rules = new(); ProvinceRules provinces = new();
        int contiguity = 0;

        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories)
                                      .OrderBy(static p => p, StringComparer.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file), DocOptions);
                var root = doc.RootElement;
                if (root.TryGetProperty("specializations", out var sArr) && sArr.ValueKind == JsonValueKind.Array)
                    foreach (var el in sArr.EnumerateArray()) specs.Add(ParseSpec(el));
                if (root.TryGetProperty("buildings", out var bArr) && bArr.ValueKind == JsonValueKind.Array)
                    foreach (var el in bArr.EnumerateArray()) buildings.Add(ParseBuilding(el));
                if (root.TryGetProperty("exchange", out var ex) && ex.ValueKind == JsonValueKind.Object)
                {
                    if (ex.TryGetProperty("requiresBuilding", out var rb) && rb.ValueKind == JsonValueKind.Array)
                        foreach (var s in rb.EnumerateArray()) if (s.ValueKind == JsonValueKind.String) exchangeBuildings.Add(s.GetString()!);
                    if (ex.TryGetProperty("rates", out var rArr) && rArr.ValueKind == JsonValueKind.Array)
                        foreach (var r in rArr.EnumerateArray())
                            rates.Add(new ExchangeRate(ConditionParser.Str(r, "give"), ConditionParser.Str(r, "get"),
                                ConditionParser.Int(r, "giveAmount", 1), ConditionParser.Int(r, "getAmount", 1)));
                }
                if (root.TryGetProperty("taxPolicies", out var tArr) && tArr.ValueKind == JsonValueKind.Array)
                    foreach (var t in tArr.EnumerateArray())
                        taxes.Add(new TaxPolicy(ConditionParser.Str(t, "id"), ConditionParser.OptStr(t, "name", ConditionParser.Str(t, "id")),
                            ConditionParser.Int(t, "creditsBonus", 0), ConditionParser.Int(t, "unrestDrift", 0), ConditionParser.Int(t, "legitimacyPerTurn", 0)));
                contiguity = Math.Max(contiguity, ConditionParser.Int(root, "contiguityBonus", 0));
                // what a solar system holds and what may be built on it (docs/SYSTEMS.md)
                if (root.TryGetProperty("works", out var wArr) && wArr.ValueKind == JsonValueKind.Array)
                    foreach (var el in wArr.EnumerateArray()) works.Add(ParseWork(el));
                if (root.TryGetProperty("features", out var fArr) && fArr.ValueKind == JsonValueKind.Array)
                    foreach (var el in fArr.EnumerateArray())
                        features.Add(new FeatureDef(ConditionParser.Str(el, "id"), ConditionParser.OptStr(el, "name", ConditionParser.Str(el, "id")), Strings(el, "bodies"), ConditionParser.Int(el, "weight", 1), ConditionParser.OptStr(el, "description", "")));
                if (root.TryGetProperty("slots", out var sl) && sl.ValueKind == JsonValueKind.Object)
                    foreach (var p in sl.EnumerateObject()) if (p.Value.ValueKind == JsonValueKind.Number) bodySlots[p.Name] = p.Value.GetInt32();
                smallSlots = ConditionParser.Int(root, "smallSlots", smallSlots);
                if (root.TryGetProperty("infrastructure", out var iArr) && iArr.ValueKind == JsonValueKind.Array)
                    foreach (var el in iArr.EnumerateArray())
                    {
                        static List<int> Ints(JsonElement e, string name) => e.TryGetProperty(name, out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetInt32()).ToList() : new List<int>();
                        var per = el.TryGetProperty("perLevel", out var pl) && pl.ValueKind == JsonValueKind.Object ? pl : default;
                        tracks.Add(new InfrastructureDef
                        {
                            Id = ConditionParser.Str(el, "id"), Name = ConditionParser.OptStr(el, "name", ConditionParser.Str(el, "id")), Description = ConditionParser.OptStr(el, "description", ""),
                            MaxLevel = ConditionParser.Int(el, "maxLevel", 5), Cost = ParseResources(el, "cost"), Years = Ints(el, "years"),
                            CalmAt = Ints(el, "calmAt"), DepotAt = ConditionParser.Int(el, "depotAt", 0),
                            Yield = per.ValueKind == JsonValueKind.Object ? ParseResources(per, "yield") : new Resources(), Defense = per.ValueKind == JsonValueKind.Object ? Dbl(per, "defense") : 0,
                            Prosperity = per.ValueKind == JsonValueKind.Object ? ConditionParser.Int(per, "prosperity", 0) : 0, Population = per.ValueKind == JsonValueKind.Object ? ConditionParser.Int(per, "population", 0) : 0
                        });
                    }
                if (root.TryGetProperty("focuses", out var fo) && fo.ValueKind == JsonValueKind.Array)
                {
                    static List<int> PIs(JsonElement e, string name, IReadOnlyList<int> d) => e.TryGetProperty(name, out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetInt32()).ToList() : d.ToList();
                    var ps = root.TryGetProperty("slots", out var psx) && psx.ValueKind == JsonValueKind.Object ? psx : default; var po = root.TryGetProperty("owes", out var pox) && pox.ValueKind == JsonValueKind.Object ? pox : default;
                    var p0 = new ProvinceRules(); var dm = root.TryGetProperty("demand", out var dmx) && dmx.ValueKind == JsonValueKind.Object ? dmx : root; var np = root.TryGetProperty("npc", out var npx) && npx.ValueKind == JsonValueKind.Object ? npx : root;
                    provinces = new ProvinceRules
                    {
                        Focuses = fo.EnumerateArray().Where(e => e.TryGetProperty("id", out _)).Select(e => new ProvinceFocus
                        {
                            Id = e.GetProperty("id").GetString()!, Name = ConditionParser.OptStr(e, "name", ""), Title = ConditionParser.OptStr(e, "title", "{0}"), Description = ConditionParser.OptStr(e, "description", ""),
                            Deposit = ConditionParser.OptStr(e, "deposit", ""), Military = ConditionParser.Bool(e, "military", false), Yield = ParseResources(e, "yield"), Defense = e.TryGetProperty("defense", out var pd) && pd.ValueKind == JsonValueKind.Number ? pd.GetDouble() : 0,
                            Classes = e.TryGetProperty("classes", out var pc) && pc.ValueKind == JsonValueKind.Array ? pc.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : new List<string>()
                        }).ToList(),
                        ImproveCost = root.TryGetProperty("improveCost", out _) ? ParseResources(root, "improveCost") : p0.ImproveCost,
                        DemandInfluence = ConditionParser.Int(dm, "influence", p0.DemandInfluence), DemandInfluenceWorld = ConditionParser.Int(dm, "influenceWorld", p0.DemandInfluenceWorld), YieldsAt = ConditionParser.Int(dm, "yieldsAt", p0.YieldsAt),
                        StepDownLegitimacy = ConditionParser.Int(dm, "legitimacy", p0.StepDownLegitimacy), AnswerYears = Math.Max(1, ConditionParser.Int(dm, "answerYears", p0.AnswerYears)),
                        SlotBase = ps.ValueKind == JsonValueKind.Object ? ConditionParser.Int(ps, "base", p0.SlotBase) : p0.SlotBase, SlotMax = ps.ValueKind == JsonValueKind.Object ? ConditionParser.Int(ps, "max", p0.SlotMax) : p0.SlotMax,
                        SlotDevelopment = ps.ValueKind == JsonValueKind.Object ? PIs(ps, "development", p0.SlotDevelopment) : p0.SlotDevelopment, SlotRank = ps.ValueKind == JsonValueKind.Object ? PIs(ps, "rank", p0.SlotRank) : p0.SlotRank,
                        Tithe = po.ValueKind == JsonValueKind.Object && po.TryGetProperty("tithe", out _) ? ParseResources(po, "tithe") : p0.Tithe, Levy = po.ValueKind == JsonValueKind.Object && po.TryGetProperty("levy", out _) ? ParseResources(po, "levy") : p0.Levy,
                        StepDownRanks = ConditionParser.Int(dm, "ranks", p0.StepDownRanks),
                        NpcProvinceYears = Math.Max(1, ConditionParser.Int(np, "provinceYears", p0.NpcProvinceYears)), NpcWorldYears = Math.Max(1, ConditionParser.Int(np, "worldYears", p0.NpcWorldYears))
                    };
                }
                if (root.TryGetProperty("slotRules", out var sr) && sr.ValueKind == JsonValueKind.Object)
                {
                    static List<int> Is(JsonElement e, string name, IReadOnlyList<int> d) => e.TryGetProperty(name, out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetInt32()).ToList() : d.ToList();
                    var d0 = new BuildRules(); var dv = root.TryGetProperty("development", out var dvx) && dvx.ValueKind == JsonValueKind.Object ? dvx : sr; var gw = root.TryGetProperty("greatWorks", out var gwx) && gwx.ValueKind == JsonValueKind.Object ? gwx : sr;
                    var cap = gw.TryGetProperty("capacity", out var cpx) && cpx.ValueKind == JsonValueKind.Object ? cpx : gw; var cm = sr.TryGetProperty("clearMax", out var cmx) && cmx.ValueKind == JsonValueKind.Object ? cmx : sr;
                    rules = new BuildRules
                    {
                        MaxWorld = ConditionParser.Int(sr, "maxWorld", d0.MaxWorld), MaxBody = ConditionParser.Int(sr, "maxBody", d0.MaxBody),
                        DevelopmentWorld = Is(sr, "developmentWorld", d0.DevelopmentWorld), DevelopmentBody = Is(sr, "developmentBody", d0.DevelopmentBody), RankWorld = Is(sr, "rankWorld", d0.RankWorld), RankBody = Is(sr, "rankBody", d0.RankBody),
                        ClearMaxWorld = ConditionParser.Int(cm, "world", d0.ClearMaxWorld), ClearMaxBody = ConditionParser.Int(cm, "body", d0.ClearMaxBody), ClearCost = sr.TryGetProperty("clearCost", out _) ? ParseResources(sr, "clearCost") : d0.ClearCost,
                        UpgradeAt = Is(sr, "upgradeAt", d0.UpgradeAt), Undivided = ConditionParser.Int(sr, "undivided", d0.Undivided),
                        DevPerLevel = ConditionParser.Int(dv, "perLevel", d0.DevPerLevel), DevCeiling = ConditionParser.Int(dv, "ceiling", d0.DevCeiling), DevCeilingPerLevel = ConditionParser.Int(dv, "ceilingPerLevel", d0.DevCeilingPerLevel),
                        DevCeilingPerPeople = Math.Max(1, ConditionParser.Int(dv, "ceilingPerPeople", d0.DevCeilingPerPeople)), DevProsperous = ConditionParser.Int(dv, "prosperous", d0.DevProsperous), DevSiege = ConditionParser.Int(dv, "siege", d0.DevSiege), DevSeed = ConditionParser.Int(dv, "seed", d0.DevSeed), DevSeedPerPeople = Math.Max(1, ConditionParser.Int(dv, "seedPerPeople", d0.DevSeedPerPeople)),
                        CapacityHouse = ConditionParser.Int(cap, "house", d0.CapacityHouse), CapacityRegionLeader = ConditionParser.Int(cap, "regionLeader", d0.CapacityRegionLeader), CapacityHeadOfState = ConditionParser.Int(cap, "headOfState", d0.CapacityHeadOfState),
                        CapacityEmperor = ConditionParser.Int(cap, "emperor", d0.CapacityEmperor), HastenPct = ConditionParser.Int(gw, "hastenPct", d0.HastenPct)
                    };
                }
                if (root.TryGetProperty("colony", out var col) && col.ValueKind == JsonValueKind.Object)
                    colony = new ColonyRule(ParseResources(col, "cost"), ConditionParser.Int(col, "buildTurns", 3), ConditionParser.Int(col, "population", 6), ConditionParser.OptStr(col, "specialization", "frontier_colony"));
            }
            catch (JsonException ex)
            {
                throw new ContentException($"Invalid JSON in '{file}': {ex.Message}", ex);
            }
        }

        if (works.Count != works.Select(w => w.Id).Distinct().Count()) throw new ContentException("Two works share an id (content/holdings).");
        foreach (var w in works)
        {
            if (w.Feature.Length > 0 && features.All(f => f.Id != w.Feature)) throw new ContentException($"Work '{w.Id}' wants a feature '{w.Feature}' that is not defined.");
            if (w.Requires.Length > 0 && works.All(o => o.Id != w.Requires)) throw new ContentException($"Work '{w.Id}' requires a work '{w.Requires}' that is not defined.");
        }
        return new HoldingCatalog(specs, buildings, rates, exchangeBuildings, taxes, contiguity) { Works = works, Features = features, Colony = colony, BodySlots = bodySlots, SmallSlots = smallSlots, Infrastructure = tracks, Rules = rules, Provinces = provinces };
    }

    private static List<string> Strings(JsonElement el, string name) =>
        el.TryGetProperty(name, out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? "").ToList() : new List<string>();

    private static WorkDef ParseWork(JsonElement el) => new()
    {
        Id = ConditionParser.Str(el, "id"), Name = ConditionParser.OptStr(el, "name", ConditionParser.Str(el, "id")), Category = ConditionParser.OptStr(el, "category", WorkDef.Orbital),
        Description = ConditionParser.OptStr(el, "description", ""), Bodies = Strings(el, "bodies"), Feature = ConditionParser.OptStr(el, "feature", ""), Deposit = ConditionParser.OptStr(el, "deposit", ""),
        Requires = ConditionParser.OptStr(el, "requires", ""), Limit = ConditionParser.OptStr(el, "limit", WorkDef.PerBody), Chokepoint = ConditionParser.Bool(el, "chokepoint", false),
        Cost = ParseResources(el, "cost"), Yield = ParseResources(el, "yield"), Upkeep = ParseResources(el, "upkeep"), BuildTurns = ConditionParser.Int(el, "buildTurns", 1),
        MinTitleRank = ConditionParser.Int(el, "minTitleRank", 0), IsMegastructure = ConditionParser.Bool(el, "megastructure", false), On = ConditionParser.OptStr(el, "on", ""), NotHome = ConditionParser.Bool(el, "notHome", false), Defense = Dbl(el, "defense"), Depot = ConditionParser.Bool(el, "depot", false),
        Prosperity = ConditionParser.Int(el, "prosperity", 0), Unrest = ConditionParser.Int(el, "unrest", 0), Population = ConditionParser.Int(el, "population", 0),
        Legitimacy = ConditionParser.Int(el, "legitimacy", 0), Glory = ConditionParser.Int(el, "glory", 0)
    };

    private static HoldingSpec ParseSpec(JsonElement el) => new()
    {
        Id = ConditionParser.Str(el, "id"),
        Name = ConditionParser.OptStr(el, "name", ""),
        BaseYield = ParseResources(el, "yield"),
        Upkeep = ParseResources(el, "upkeep"),
        Slots = ConditionParser.Int(el, "slots", 0),
        PopGrowth = ConditionParser.Int(el, "popGrowth", 0),
        MaxPopulation = ConditionParser.Int(el, "maxPopulation", 0),
        PopManpower = ConditionParser.Int(el, "popManpower", 0),
        Defense = Dbl(el, "defense")
    };

    private static int CostOf(JsonElement el) { var c = ParseResources(el, "cost"); return c.Credits + c.Materials; }

    private static BuildingDef ParseBuilding(JsonElement el) => new()
    {
        Id = ConditionParser.Str(el, "id"),
        Name = ConditionParser.OptStr(el, "name", ""),
        Cost = ParseResources(el, "cost"),
        Yield = ParseResources(el, "yield"),
        Upkeep = ParseResources(el, "upkeep"),
        Requires = ConditionParser.OptStrOrNull(el, "requires"),
        RequiresDeposit = ConditionParser.OptStrOrNull(el, "requiresDeposit"),
        Defense = Dbl(el, "defense"),
        MinTitleRank = ConditionParser.Int(el, "minTitleRank", 0),
        Megastructure = ConditionParser.Bool(el, "megastructure", false),
        // years to raise it (docs/ECONOMY_MARKET.md stage B). Where the content names none: a megastructure four, works on a deposit two,
        // anything costing thirty or more in credits and materials two, the rest one
        BuildTurns = ConditionParser.Int(el, "buildTurns", ConditionParser.Bool(el, "megastructure", false) ? 4 : ConditionParser.OptStrOrNull(el, "requiresDeposit") != null ? 2 : CostOf(el) >= 30 ? 2 : 1)
    };

    private static double Dbl(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0.0;

    /// <summary>Parse a resource bundle from an object property; missing ⇒ all zero.</summary>
    public static Resources ParseResources(JsonElement el, string name)
    {
        var r = new Resources();
        if (el.TryGetProperty(name, out var obj) && obj.ValueKind == JsonValueKind.Object)
            foreach (var p in obj.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.Number)
                    r.Set(p.Name, p.Value.GetInt32());
        return r;
    }
}
