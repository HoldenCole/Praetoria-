using System.Text.Json;
using Praetoria.Core.Careers;

namespace Praetoria.Core.Data;

/// <summary>Loads career tracks + rank ladders from /content/careers (GDD §13, BuildSpec §2). Missing
/// dir ⇒ empty catalog (ranks uncapped and unnamed).</summary>
public static class CareerLoader
{
    private static readonly JsonDocumentOptions DocOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static CareerCatalog LoadFromDirectory(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "careers");
        if (!Directory.Exists(dir)) return CareerCatalog.Empty;

        var defs = new List<CareerTrackDef>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories)
                                      .OrderBy(static p => p, StringComparer.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file), DocOptions);
                if (!doc.RootElement.TryGetProperty("tracks", out var arr) || arr.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (var el in arr.EnumerateArray())
                {
                    var ranks = new List<string>();
                    if (el.TryGetProperty("ranks", out var r) && r.ValueKind == JsonValueKind.Array)
                        foreach (var s in r.EnumerateArray())
                            if (s.ValueKind == JsonValueKind.String) ranks.Add(s.GetString()!);
                    if (ranks.Count == 0)
                        throw new ContentException($"Career track '{ConditionParser.Str(el, "id")}' has no ranks.");
                    defs.Add(new CareerTrackDef
                    {
                        Id = ConditionParser.Str(el, "id"),
                        Name = ConditionParser.OptStr(el, "name", ConditionParser.Str(el, "id")),
                        Ranks = ranks
                    });
                }
            }
            catch (JsonException ex)
            {
                throw new ContentException($"Invalid JSON in '{file}': {ex.Message}", ex);
            }
        }
        return new CareerCatalog(defs);
    }
}
