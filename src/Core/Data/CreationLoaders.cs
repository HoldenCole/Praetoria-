using System.Text.Json;
using Praetoria.Core.Creation;

namespace Praetoria.Core.Data;

/// <summary>Loads /content/traits (nature, aptitude, ambitions) — the §8 vocabularies as data.</summary>
public static class TraitLoader
{
    private static readonly JsonDocumentOptions DocOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static TraitCatalog LoadFromDirectory(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "traits");
        if (!Directory.Exists(dir)) return TraitCatalog.Empty;
        var nature = new List<TraitDef>(); var aptitude = new List<TraitDef>(); var ambitions = new List<TraitDef>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).OrderBy(static p => p, StringComparer.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file), DocOptions);
                Read(doc.RootElement, "nature", nature);
                Read(doc.RootElement, "aptitude", aptitude);
                Read(doc.RootElement, "ambitions", ambitions);
            }
            catch (JsonException ex) { throw new ContentException($"Invalid JSON in '{file}': {ex.Message}", ex); }
        }
        return new TraitCatalog(nature, aptitude, ambitions);
    }

    private static void Read(JsonElement root, string section, List<TraitDef> into)
    {
        if (!root.TryGetProperty(section, out var arr) || arr.ValueKind != JsonValueKind.Array) return;
        foreach (var el in arr.EnumerateArray())
            into.Add(new TraitDef
            {
                Id = ConditionParser.Str(el, "id"),
                Name = ConditionParser.OptStr(el, "name", ConditionParser.Str(el, "id")),
                Description = ConditionParser.OptStr(el, "description", "")
            });
    }
}

/// <summary>Loads /content/names — given names by culture.</summary>
public static class NameLoader
{
    private static readonly JsonDocumentOptions DocOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static NameCatalog LoadFromDirectory(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "names");
        if (!Directory.Exists(dir)) return NameCatalog.Empty;
        var cultures = new Dictionary<string, (IReadOnlyList<string>, IReadOnlyList<string>)>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).OrderBy(static p => p, StringComparer.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file), DocOptions);
                if (!doc.RootElement.TryGetProperty("cultures", out var cs) || cs.ValueKind != JsonValueKind.Object) continue;
                foreach (var c in cs.EnumerateObject())
                    cultures[c.Name] = (Names(c.Value, "male"), Names(c.Value, "female"));
            }
            catch (JsonException ex) { throw new ContentException($"Invalid JSON in '{file}': {ex.Message}", ex); }
        }
        return new NameCatalog(cultures);
    }

    private static List<string> Names(JsonElement el, string key)
    {
        var list = new List<string>();
        if (el.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var s in arr.EnumerateArray()) if (s.ValueKind == JsonValueKind.String) list.Add(s.GetString()!);
        return list;
    }
}

/// <summary>Loads /content/backgrounds — character origins applied at creation.</summary>
public static class BackgroundLoader
{
    private static readonly JsonDocumentOptions DocOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static BackgroundCatalog LoadFromDirectory(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "backgrounds");
        if (!Directory.Exists(dir)) return BackgroundCatalog.Empty;
        var defs = new List<BackgroundDef>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).OrderBy(static p => p, StringComparer.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file), DocOptions);
                if (!doc.RootElement.TryGetProperty("backgrounds", out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
                foreach (var el in arr.EnumerateArray())
                    defs.Add(new BackgroundDef
                    {
                        Id = ConditionParser.Str(el, "id"),
                        Name = ConditionParser.OptStr(el, "name", ConditionParser.Str(el, "id")),
                        Description = ConditionParser.OptStr(el, "description", ""),
                        Legitimacy = ConditionParser.Int(el, "legitimacy", 0),
                        Treasury = HoldingCatalogLoader.ParseResources(el, "treasury"),
                        Nature = StrList(el, "nature"),
                        Aptitude = StrList(el, "aptitude"),
                        Skills = IntMap(el, "skills"),
                        Flags = StrList(el, "flags"),
                        Stress = ConditionParser.Int(el, "stress", 0),
                        TrackHint = ConditionParser.OptStrOrNull(el, "trackHint"),
                        AmbitionHint = ConditionParser.OptStrOrNull(el, "ambitionHint")
                    });
            }
            catch (JsonException ex) { throw new ContentException($"Invalid JSON in '{file}': {ex.Message}", ex); }
        }
        return new BackgroundCatalog(defs);
    }

    private static List<string> StrList(JsonElement el, string name)
    {
        var list = new List<string>();
        if (el.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var s in arr.EnumerateArray()) if (s.ValueKind == JsonValueKind.String) list.Add(s.GetString()!);
        return list;
    }

    private static Dictionary<string, int> IntMap(JsonElement el, string name)
    {
        var map = new Dictionary<string, int>();
        if (el.TryGetProperty(name, out var obj) && obj.ValueKind == JsonValueKind.Object)
            foreach (var p in obj.EnumerateObject()) if (p.Value.ValueKind == JsonValueKind.Number) map[p.Name] = p.Value.GetInt32();
        return map;
    }
}
