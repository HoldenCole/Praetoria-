using System.Text.Json;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Data;

/// <summary>Loads /content/honors (GDD §13 Honorifics & Orders) and /content/traits/friction.json (GDD §18).</summary>
public static class HonorLoader
{
    private static readonly JsonDocumentOptions DocOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static IReadOnlyList<HonorDef> LoadFromDirectory(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "honors");
        if (!Directory.Exists(dir)) return Array.Empty<HonorDef>();
        var list = new List<HonorDef>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).OrderBy(static p => p, StringComparer.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file), DocOptions);
                if (!doc.RootElement.TryGetProperty("honors", out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
                foreach (var el in arr.EnumerateArray())
                {
                    string id = ConditionParser.Str(el, "id");
                    try
                    {
                        list.Add(new HonorDef
                        {
                            Id = id,
                            Name = ConditionParser.OptStr(el, "name", id),
                            Description = ConditionParser.OptStr(el, "description", ""),
                            When = el.TryGetProperty("when", out var wh) ? ConditionParser.ParseList(wh) : Array.Empty<Events.ICondition>(),
                            Effects = el.TryGetProperty("effects", out var ef) ? EffectParser.ParseList(ef) : Array.Empty<Events.IEffect>()
                        });
                    }
                    catch (ContentException ex) { throw new ContentException($"In honor '{id}': {ex.Message}", ex); }
                }
            }
            catch (JsonException ex) { throw new ContentException($"Invalid JSON in '{file}': {ex.Message}", ex); }
        }
        return list;
    }

    public static FrictionTable LoadFriction(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "traits");
        if (!Directory.Exists(dir)) return FrictionTable.Empty;
        var table = new Dictionary<string, Dictionary<string, int>>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).OrderBy(static p => p, StringComparer.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file), DocOptions);
                if (!doc.RootElement.TryGetProperty("friction", out var f) || f.ValueKind != JsonValueKind.Object) continue;
                foreach (var act in f.EnumerateObject())
                {
                    var row = new Dictionary<string, int>();
                    if (act.Value.ValueKind == JsonValueKind.Object)
                        foreach (var t in act.Value.EnumerateObject()) if (t.Value.ValueKind == JsonValueKind.Number) row[t.Name] = t.Value.GetInt32();
                    table[act.Name] = row;
                }
            }
            catch (JsonException ex) { throw new ContentException($"Invalid JSON in '{file}': {ex.Message}", ex); }
        }
        return new FrictionTable(table);
    }
}
