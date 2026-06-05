using System.Text.Json;
using Praetoria.Core.State;

namespace Praetoria.Core.Data;

/// <summary>Loads a <see cref="Scenario"/> from a JSON file under /content/scenarios.</summary>
public static class ScenarioLoader
{
    private static readonly JsonDocumentOptions DocOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static Scenario LoadFile(string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), DocOptions);
            var root = doc.RootElement;
            return new Scenario
            {
                Id = ConditionParser.Str(root, "id"),
                ProtagonistId = ConditionParser.Str(root, "protagonist"),
                Era = ConditionParser.OptStr(root, "era", "fractured_stars"),
                Houses = ParseHouses(root),
                Characters = ParseCharacters(root),
                Relationships = ParseRelationships(root),
                Holdings = ParseHoldings(root)
            };
        }
        catch (JsonException ex)
        {
            throw new ContentException($"Invalid JSON in scenario '{path}': {ex.Message}", ex);
        }
    }

    public static Scenario LoadFromContent(string contentRoot, string scenarioId)
    {
        var path = Path.Combine(contentRoot, "scenarios", scenarioId + ".json");
        if (!File.Exists(path))
            throw new ContentException($"Scenario '{scenarioId}' not found at {path}.");
        return LoadFile(path);
    }

    private static IReadOnlyList<House> ParseHouses(JsonElement root)
    {
        var list = new List<House>();
        if (root.TryGetProperty("houses", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var h in arr.EnumerateArray())
                list.Add(new House
                {
                    Id = ConditionParser.Str(h, "id"),
                    Name = ConditionParser.OptStr(h, "name", ""),
                    AccentColor = ConditionParser.OptStr(h, "accent", "#888888"),
                    Title = ConditionParser.OptStr(h, "title", "landless"),
                    Legitimacy = ConditionParser.Int(h, "legitimacy", 0),
                    Claims = new HashSet<string>(StrList(h, "claims")),
                    Treasury = HoldingCatalogLoader.ParseResources(h, "treasury")
                });
        return list;
    }

    private static IReadOnlyList<Holding> ParseHoldings(JsonElement root)
    {
        var list = new List<Holding>();
        if (root.TryGetProperty("holdings", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var h in arr.EnumerateArray())
                list.Add(new Holding
                {
                    Id = ConditionParser.Str(h, "id"),
                    OwnerId = ConditionParser.Str(h, "owner"),
                    Name = ConditionParser.OptStr(h, "name", ""),
                    Specialization = ConditionParser.Str(h, "specialization"),
                    SystemId = ConditionParser.OptStr(h, "system", ""),
                    Population = ConditionParser.Int(h, "population", 0),
                    Unrest = ConditionParser.Int(h, "unrest", 0),
                    Buildings = StrList(h, "buildings")
                });
        return list;
    }

    private static List<string> StrList(JsonElement el, string name)
    {
        var list = new List<string>();
        if (el.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var s in arr.EnumerateArray())
                if (s.ValueKind == JsonValueKind.String) list.Add(s.GetString()!);
        return list;
    }

    private static IReadOnlyList<Character> ParseCharacters(JsonElement root)
    {
        var list = new List<Character>();
        if (root.TryGetProperty("characters", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var c in arr.EnumerateArray())
                list.Add(new Character
                {
                    Id = ConditionParser.Str(c, "id"),
                    Name = ConditionParser.OptStr(c, "name", ""),
                    HouseId = ConditionParser.OptStr(c, "house", ""),
                    Age = ConditionParser.Int(c, "age", 18),
                    Sex = ConditionParser.OptStr(c, "sex", ""),
                    MotherId = ConditionParser.OptStr(c, "mother", ""),
                    FatherId = ConditionParser.OptStr(c, "father", ""),
                    CareerTrack = ConditionParser.OptStr(c, "careerTrack", "military"),
                    CareerRank = ConditionParser.Int(c, "careerRank", 0),
                    NatureTraits = StrSet(c, "nature"),
                    AptitudeTraits = StrSet(c, "aptitude"),
                    Skills = IntMap(c, "skills"),
                    Ambition = ConditionParser.OptStr(c, "ambition", "")
                });
        return list;
    }

    private static IReadOnlyList<Relationship> ParseRelationships(JsonElement root)
    {
        var list = new List<Relationship>();
        if (root.TryGetProperty("relationships", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var r in arr.EnumerateArray())
                list.Add(new Relationship
                {
                    FromId = ConditionParser.Str(r, "from"),
                    ToId = ConditionParser.Str(r, "to"),
                    Disposition = ConditionParser.Int(r, "disposition", 0),
                    Bond = ParseBond(ConditionParser.OptStr(r, "bond", "none")),
                    BondStrength = ConditionParser.Int(r, "strength", 0)
                });
        return list;
    }

    private static BondType ParseBond(string s) => s.ToLowerInvariant() switch
    {
        "blood" => BondType.Blood,
        "sworn" => BondType.Sworn,
        "marriage" => BondType.Marriage,
        _ => BondType.None
    };

    private static HashSet<string> StrSet(JsonElement el, string name)
    {
        var set = new HashSet<string>();
        if (el.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var s in arr.EnumerateArray())
                if (s.ValueKind == JsonValueKind.String) set.Add(s.GetString()!);
        return set;
    }

    private static Dictionary<string, int> IntMap(JsonElement el, string name)
    {
        var map = new Dictionary<string, int>();
        if (el.TryGetProperty(name, out var obj) && obj.ValueKind == JsonValueKind.Object)
            foreach (var p in obj.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.Number) map[p.Name] = p.Value.GetInt32();
        return map;
    }
}
