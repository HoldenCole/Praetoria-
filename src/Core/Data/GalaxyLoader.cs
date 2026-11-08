using System.Text.Json;
using Praetoria.Core.Galaxy;

namespace Praetoria.Core.Data;

/// <summary>Loads a galaxy (systems + lanes) from /content/galaxy/&lt;id&gt;.json (GDD §19, BuildSpec §3).</summary>
public static class GalaxyLoader
{
    private static readonly JsonDocumentOptions DocOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static Galaxy.Galaxy LoadFromContent(string contentRoot, string galaxyId)
    {
        var path = Path.Combine(contentRoot, "galaxy", galaxyId + ".json");
        if (!File.Exists(path)) throw new ContentException($"Galaxy '{galaxyId}' not found at {path}.");
        return LoadFile(path);
    }

    public static Galaxy.Galaxy LoadFile(string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), DocOptions);
            var root = doc.RootElement;
            var g = new Galaxy.Galaxy
            {
                Id = ConditionParser.Str(root, "id"),
                Name = ConditionParser.OptStr(root, "name", ConditionParser.Str(root, "id")),
                Baseline = ConditionParser.OptStr(root, "baseline", "")
            };
            if (root.TryGetProperty("regions", out var regions) && regions.ValueKind == JsonValueKind.Array)
                foreach (var r in regions.EnumerateArray())
                    g.Regions.Add(new Region
                    {
                        Id = ConditionParser.Str(r, "id"),
                        Name = ConditionParser.OptStr(r, "name", ConditionParser.Str(r, "id")),
                        Culture = ConditionParser.OptStr(r, "culture", "reach"),
                        Description = ConditionParser.OptStr(r, "description", "")
                    });
            if (root.TryGetProperty("systems", out var systems) && systems.ValueKind == JsonValueKind.Array)
                foreach (var s in systems.EnumerateArray())
                {
                    var sys = new StarSystem
                    {
                        Id = ConditionParser.Str(s, "id"),
                        Name = ConditionParser.OptStr(s, "name", ConditionParser.Str(s, "id")),
                        X = s.TryGetProperty("x", out var x) && x.ValueKind == JsonValueKind.Number ? x.GetDouble() : 0,
                        Y = s.TryGetProperty("y", out var y) && y.ValueKind == JsonValueKind.Number ? y.GetDouble() : 0,
                        RegionId = ConditionParser.OptStr(s, "region", ""),
                        Culture = ConditionParser.OptStr(s, "culture", ""),
                        PlanetClass = ConditionParser.OptStr(s, "planet", "terran"),
                        Belt = ConditionParser.Int(s, "belt", 0),
                        Description = ConditionParser.OptStr(s, "description", "")
                    };
                    if (g.Systems.ContainsKey(sys.Id)) throw new ContentException($"Duplicate system id '{sys.Id}'.");
                    g.Systems[sys.Id] = sys;
                }
            if (root.TryGetProperty("lanes", out var lanes) && lanes.ValueKind == JsonValueKind.Array)
                foreach (var l in lanes.EnumerateArray())
                {
                    if (l.ValueKind != JsonValueKind.Array || l.GetArrayLength() != 2)
                        throw new ContentException("A lane is a two-element array [\"a\", \"b\"].");
                    g.Lanes.Add(new Hyperlane { A = l[0].GetString() ?? "", B = l[1].GetString() ?? "" });
                }
            var problems = g.Problems().ToList();
            if (problems.Count > 0) throw new ContentException($"Galaxy '{g.Id}': " + string.Join("; ", problems));
            return g;
        }
        catch (JsonException ex)
        {
            throw new ContentException($"Invalid JSON in galaxy '{path}': {ex.Message}", ex);
        }
    }
}
