using System.Text.Json;
using Praetoria.Core.Progression;

namespace Praetoria.Core.Data;

/// <summary>Loads the title ladder from /content/titles (GDD §13, BuildSpec §2). Missing dir ⇒
/// empty catalog (no title/progression layer).</summary>
public static class TitleLoader
{
    private static readonly JsonDocumentOptions DocOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static TitleCatalog LoadFromDirectory(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "titles");
        if (!Directory.Exists(dir)) return TitleCatalog.Empty;

        var defs = new List<TitleDef>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories)
                                      .OrderBy(static p => p, StringComparer.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file), DocOptions);
                if (doc.RootElement.TryGetProperty("titles", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    foreach (var el in arr.EnumerateArray())
                        defs.Add(new TitleDef
                        {
                            Id = ConditionParser.Str(el, "id"),
                            Name = ConditionParser.OptStr(el, "name", ConditionParser.Str(el, "id")),
                            Rank = ConditionParser.Int(el, "rank", 0),
                            LegitimacyRequirement = ConditionParser.Int(el, "legitimacyRequirement", 0)
                        });
            }
            catch (JsonException ex)
            {
                throw new ContentException($"Invalid JSON in '{file}': {ex.Message}", ex);
            }
        }
        return new TitleCatalog(defs);
    }
}
