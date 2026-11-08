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
            }
            catch (JsonException ex)
            {
                throw new ContentException($"Invalid JSON in '{file}': {ex.Message}", ex);
            }
        }

        return new HoldingCatalog(specs, buildings, rates, exchangeBuildings, taxes, contiguity);
    }

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
        Megastructure = ConditionParser.Bool(el, "megastructure", false)
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
