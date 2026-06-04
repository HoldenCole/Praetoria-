using System.Text.Json;
using Praetoria.Core.Crises;

namespace Praetoria.Core.Data;

/// <summary>
/// Loads crisis definitions from /content/crises (GDD §16). Gates reuse the condition vocabulary and
/// trigger/damper effects reuse the effect vocabulary, so authoring a crisis needs no new mini-language
/// — it's the same data the event engine already speaks. Missing dir ⇒ no crises.
/// </summary>
public static class CrisisLoader
{
    private static readonly JsonDocumentOptions DocOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static IReadOnlyList<CrisisDef> LoadFromDirectory(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "crises");
        if (!Directory.Exists(dir)) return Array.Empty<CrisisDef>();

        var list = new List<CrisisDef>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories)
                                      .OrderBy(static p => p, StringComparer.Ordinal))
            list.AddRange(LoadFile(file));
        return list;
    }

    public static IReadOnlyList<CrisisDef> LoadFile(string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), DocOptions);
            var root = doc.RootElement;
            if (!root.TryGetProperty("crises", out var arr) || arr.ValueKind != JsonValueKind.Array)
                throw new ContentException("Expected a top-level 'crises' array.");

            var list = new List<CrisisDef>();
            foreach (var el in arr.EnumerateArray())
                list.Add(ParseCrisis(el));
            return list;
        }
        catch (JsonException ex)
        {
            throw new ContentException($"Invalid JSON in '{path}': {ex.Message}", ex);
        }
    }

    private static CrisisDef ParseCrisis(JsonElement el)
    {
        string id = ConditionParser.Str(el, "id");
        try
        {
            return new CrisisDef
            {
                Id = id,
                Name = ConditionParser.OptStr(el, "name", id),
                Tier = ConditionParser.OptStr(el, "tier", "regional"),
                Weight = el.TryGetProperty("weight", out var w) && w.ValueKind == JsonValueKind.Number ? w.GetDouble() : 1.0,
                Repeatable = ConditionParser.Bool(el, "repeatable", false),
                Severity = ConditionParser.Int(el, "severity", 1),
                Gates = el.TryGetProperty("gates", out var g) ? ConditionParser.ParseList(g) : Array.Empty<Events.ICondition>(),
                OnTrigger = el.TryGetProperty("onTrigger", out var t) ? EffectParser.ParseList(t) : Array.Empty<Events.IEffect>(),
                Dampers = ParseDampers(el)
            };
        }
        catch (ContentException ex)
        {
            throw new ContentException($"In crisis '{id}': {ex.Message}", ex);
        }
    }

    private static IReadOnlyList<DamperDef> ParseDampers(JsonElement el)
    {
        if (!el.TryGetProperty("dampers", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return Array.Empty<DamperDef>();

        var list = new List<DamperDef>();
        foreach (var d in arr.EnumerateArray())
            list.Add(new DamperDef
            {
                Id = ConditionParser.Str(d, "id"),
                Name = ConditionParser.OptStr(d, "name", ConditionParser.Str(d, "id")),
                Relief = ConditionParser.Int(d, "relief", 1),
                Availability = d.TryGetProperty("available", out var a) ? ConditionParser.ParseList(a) : Array.Empty<Events.ICondition>(),
                Effects = d.TryGetProperty("effects", out var e) ? EffectParser.ParseList(e) : Array.Empty<Events.IEffect>(),
                Cost = ParseCost(d)
            });
        return list;
    }

    private static IReadOnlyDictionary<string, int> ParseCost(JsonElement d)
    {
        var cost = new Dictionary<string, int>();
        if (d.TryGetProperty("cost", out var c) && c.ValueKind == JsonValueKind.Object)
            foreach (var p in c.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.Number) cost[p.Name] = p.Value.GetInt32();
        return cost;
    }
}
