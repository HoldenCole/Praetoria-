using System.Text.Json;
using Praetoria.Core.Spheres;

namespace Praetoria.Core.Data;

/// <summary>Loads sphere definitions from /content/spheres (GDD §7, BuildSpec §2). Missing dir ⇒
/// empty catalog (no power-balance layer).</summary>
public static class SphereLoader
{
    private static readonly JsonDocumentOptions DocOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static SphereCatalog LoadFromDirectory(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "spheres");
        if (!Directory.Exists(dir)) return SphereCatalog.Empty;

        var defs = new List<SphereDef>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories)
                                      .OrderBy(static p => p, StringComparer.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file), DocOptions);
                if (doc.RootElement.TryGetProperty("spheres", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    foreach (var el in arr.EnumerateArray())
                        defs.Add(new SphereDef
                        {
                            Id = ConditionParser.Str(el, "id"),
                            Name = ConditionParser.OptStr(el, "name", ConditionParser.Str(el, "id")),
                            CareerTrack = ConditionParser.Str(el, "careerTrack")
                        });
            }
            catch (JsonException ex)
            {
                throw new ContentException($"Invalid JSON in '{file}': {ex.Message}", ex);
            }
        }
        return new SphereCatalog(defs);
    }
}
