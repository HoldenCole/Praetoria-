using System.Text.Json;
using Praetoria.Core.Crises;

namespace Praetoria.Core.Data;

/// <summary>Loads the Great-Crisis archetype from /content/great_crisis (the first file with a "greatCrisis" object wins).</summary>
public static class GreatCrisisLoader
{
    private static readonly JsonDocumentOptions DocOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static GreatCrisisDef? LoadFromDirectory(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "great_crisis");
        if (!Directory.Exists(dir)) return null;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).OrderBy(static p => p, StringComparer.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file), DocOptions);
                if (!doc.RootElement.TryGetProperty("greatCrisis", out var g)) continue;
                var intensity = new Dictionary<string, double>();
                if (g.TryGetProperty("intensity", out var inten) && inten.ValueKind == JsonValueKind.Object)
                    foreach (var p in inten.EnumerateObject()) if (p.Value.ValueKind == JsonValueKind.Number) intensity[p.Name] = p.Value.GetDouble();
                var stages = new List<GreatCrisisStage>();
                if (g.TryGetProperty("stages", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    foreach (var s in arr.EnumerateArray())
                        stages.Add(new GreatCrisisStage
                        {
                            Id = ConditionParser.Str(s, "id"),
                            At = ConditionParser.Int(s, "at", 0),
                            Behavior = ConditionParser.OptStr(s, "behavior", ""),
                            Effects = s.TryGetProperty("effects", out var ef) ? EffectParser.ParseList(ef) : Array.Empty<Events.IEffect>(),
                            Log = ConditionParser.OptStr(s, "log", "")
                        });
                return new GreatCrisisDef
                {
                    Id = ConditionParser.Str(g, "id"),
                    Name = ConditionParser.OptStr(g, "name", ConditionParser.Str(g, "id")),
                    StartTurn = ConditionParser.Int(g, "startTurn", 10),
                    BaseRate = ConditionParser.Int(g, "baseRate", 10),
                    Intensity = intensity.Count > 0 ? intensity : new GreatCrisisDef().Intensity,
                    Stages = stages.OrderBy(s => s.At).ToList()
                };
            }
            catch (JsonException ex) { throw new ContentException($"Invalid JSON in '{file}': {ex.Message}", ex); }
        }
        return null;
    }
}
