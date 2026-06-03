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
            }
            catch (JsonException ex)
            {
                throw new ContentException($"Invalid JSON in '{file}': {ex.Message}", ex);
            }
        }

        return new HoldingCatalog(specs, buildings);
    }

    private static HoldingSpec ParseSpec(JsonElement el) => new()
    {
        Id = ConditionParser.Str(el, "id"),
        Name = ConditionParser.OptStr(el, "name", ""),
        BaseYield = ParseResources(el, "yield"),
        Upkeep = ParseResources(el, "upkeep"),
        Slots = ConditionParser.Int(el, "slots", 0),
        PopGrowth = ConditionParser.Int(el, "popGrowth", 0)
    };

    private static BuildingDef ParseBuilding(JsonElement el) => new()
    {
        Id = ConditionParser.Str(el, "id"),
        Name = ConditionParser.OptStr(el, "name", ""),
        Cost = ParseResources(el, "cost"),
        Yield = ParseResources(el, "yield"),
        Upkeep = ParseResources(el, "upkeep"),
        Requires = ConditionParser.OptStrOrNull(el, "requires")
    };

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
