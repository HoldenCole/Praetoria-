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

    /// <param name="includeBaseline">False loads the scenario exactly as written, without the galaxy's minor houses (tests, tools).</param>
    public static Scenario LoadFile(string path, bool includeBaseline = true)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), DocOptions);
            var root = doc.RootElement;
            var galaxyId = ConditionParser.OptStrOrNull(root, "galaxy");
            var galaxy = galaxyId == null ? null : ResolveGalaxy(path, galaxyId);
            var scenario = new Scenario
            {
                Id = ConditionParser.Str(root, "id"),
                ProtagonistId = ConditionParser.Str(root, "protagonist"),
                Era = ConditionParser.OptStr(root, "era", "fractured_stars"),
                Houses = ParseHouses(root),
                Characters = ParseCharacters(root),
                Relationships = ParseRelationships(root),
                Holdings = ParseHoldings(root),
                Fleets = ParseFleets(root),
                Galaxy = galaxy
            };
            // The galaxy's minor houses (GDD §19 "smaller houses that control smaller things, or nothing"): merged in unless
            // the scenario names its own baseline, or "" to play on an empty map.
            string baseline = ConditionParser.OptStr(root, "baseline", galaxy?.Baseline ?? "");
            if (includeBaseline && baseline.Length > 0 && galaxy != null) scenario = MergeBaseline(scenario, LoadBaseline(path, baseline));
            return scenario;
        }
        catch (JsonException ex)
        {
            throw new ContentException($"Invalid JSON in scenario '{path}': {ex.Message}", ex);
        }
    }

    private static Scenario LoadBaseline(string scenarioPath, string baselineId)
    {
        var scenariosDir = Path.GetDirectoryName(Path.GetFullPath(scenarioPath)) ?? ".";
        var contentRoot = Path.GetDirectoryName(scenariosDir) ?? ".";
        var file = Path.Combine(contentRoot, "galaxy", baselineId + ".json");
        if (!File.Exists(file)) throw new ContentException($"Baseline '{baselineId}' not found at {file}.");
        using var doc = JsonDocument.Parse(File.ReadAllText(file), DocOptions);
        var root = doc.RootElement;
        return new Scenario
        {
            Id = baselineId,
            Houses = ParseHouses(root), Characters = ParseCharacters(root), Relationships = ParseRelationships(root),
            Holdings = ParseHoldings(root), Fleets = ParseFleets(root)
        };
    }

    /// <summary>Scenario wins every collision: a baseline house, character, holding or fleet is added only when the scenario
    /// has not defined that id, a baseline planet only where the scenario placed no planet in that system, and a baseline
    /// house's people and ships only when the house itself came from the baseline.</summary>
    public static Scenario MergeBaseline(Scenario sc, Scenario baseline)
    {
        var houseIds = sc.Houses.Select(h => h.Id).ToHashSet();
        var charIds = sc.Characters.Select(c => c.Id).ToHashSet();
        var holdingIds = sc.Holdings.Select(h => h.Id).ToHashSet();
        var fleetIds = sc.Fleets.Select(f => f.Id).ToHashSet();
        var claimedSystems = sc.Holdings.Where(h => h.IsPlanet && h.SystemId.Length > 0).Select(h => h.SystemId).ToHashSet();

        var houses = sc.Houses.ToList(); var added = new HashSet<string>();
        foreach (var h in baseline.Houses) if (!houseIds.Contains(h.Id)) { houses.Add(h); added.Add(h.Id); }
        var chars = sc.Characters.ToList();
        foreach (var c in baseline.Characters) if (!charIds.Contains(c.Id) && added.Contains(c.HouseId)) chars.Add(c);
        var charNow = chars.Select(c => c.Id).ToHashSet();
        var rels = sc.Relationships.ToList();
        foreach (var r in baseline.Relationships) if (charNow.Contains(r.FromId) && charNow.Contains(r.ToId) && !rels.Any(x => x.FromId == r.FromId && x.ToId == r.ToId)) rels.Add(r);
        var holdings = sc.Holdings.ToList();
        foreach (var h in baseline.Holdings)
        {
            if (holdingIds.Contains(h.Id) || !added.Contains(h.OwnerId)) continue;
            if (h.IsPlanet && claimedSystems.Contains(h.SystemId)) continue;
            if (h.GovernorId.Length > 0 && !charNow.Contains(h.GovernorId)) h.GovernorId = "";
            holdings.Add(h);
        }
        var fleets = sc.Fleets.ToList();
        foreach (var f in baseline.Fleets) if (!fleetIds.Contains(f.Id) && added.Contains(f.OwnerId)) fleets.Add(f);

        return new Scenario
        {
            Id = sc.Id, ProtagonistId = sc.ProtagonistId, Era = sc.Era, Galaxy = sc.Galaxy,
            Houses = houses, Characters = chars, Relationships = rels, Holdings = holdings, Fleets = fleets
        };
    }

    /// <summary>A scenario's galaxy lives in the sibling /content/galaxy directory.</summary>
    private static Galaxy.Galaxy ResolveGalaxy(string scenarioPath, string galaxyId)
    {
        var scenariosDir = Path.GetDirectoryName(Path.GetFullPath(scenarioPath)) ?? ".";
        var contentRoot = Path.GetDirectoryName(scenariosDir) ?? ".";
        return GalaxyLoader.LoadFromContent(contentRoot, galaxyId);
    }

    public static Scenario LoadFromContent(string contentRoot, string scenarioId, bool includeBaseline = true)
    {
        var path = Path.Combine(contentRoot, "scenarios", scenarioId + ".json");
        if (!File.Exists(path))
            throw new ContentException($"Scenario '{scenarioId}' not found at {path}.");
        return LoadFile(path, includeBaseline);
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
                    Culture = ConditionParser.OptStr(h, "culture", "reach"),
                    SuccessionLaw = ConditionParser.OptStr(h, "successionLaw", "primogeniture"),
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
                    Buildings = StrList(h, "buildings"),
                    Kind = ConditionParser.OptStr(h, "kind", Holding.Planet),
                    GovernorId = ConditionParser.OptStr(h, "governor", ""),
                    Culture = ConditionParser.OptStr(h, "culture", "")
                });
        return list;
    }

    private static IReadOnlyList<Fleet> ParseFleets(JsonElement root)
    {
        var list = new List<Fleet>();
        if (root.TryGetProperty("fleets", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var f in arr.EnumerateArray())
                list.Add(new Fleet
                {
                    Id = ConditionParser.Str(f, "id"),
                    Name = ConditionParser.OptStr(f, "name", ConditionParser.Str(f, "id")),
                    OwnerId = ConditionParser.Str(f, "owner"),
                    SystemId = ConditionParser.Str(f, "system"),
                    Strength = ConditionParser.Int(f, "strength", 1),
                    Readiness = ConditionParser.Int(f, "readiness", 100),
                    Office = ConditionParser.Bool(f, "office", false),
                    AdmiralId = ConditionParser.OptStr(f, "admiral", ""),
                    Ships = IntMap(f, "ships")
                });
        foreach (var f in list) f.Recount();
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
                    Ambition = ConditionParser.OptStr(c, "ambition", ""),
                    Flags = StrSet(c, "flags")
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
