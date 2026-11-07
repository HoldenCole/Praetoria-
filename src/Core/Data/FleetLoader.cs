using System.Text.Json;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Data;

/// <summary>Loads /content/fleets/*.json — the fleet layer's tuning (ship cost, upkeep, readiness, intel).</summary>
public static class FleetLoader
{
    private static readonly JsonDocumentOptions DocOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static FleetRules LoadFromDirectory(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "fleets");
        if (!Directory.Exists(dir)) return FleetRules.Default;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).OrderBy(static p => p, StringComparer.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file), DocOptions);
                var r = doc.RootElement;
                if (!r.TryGetProperty("rules", out var rules)) continue;
                var d = FleetRules.Default;
                return new FleetRules
                {
                    ShipCost = rules.TryGetProperty("shipCost", out _) ? HoldingCatalogLoader.ParseResources(rules, "shipCost") : d.ShipCost,
                    UpkeepPerStrength = rules.TryGetProperty("upkeepPerStrength", out _) ? HoldingCatalogLoader.ParseResources(rules, "upkeepPerStrength") : d.UpkeepPerStrength,
                    ReadinessRecovery = ConditionParser.Int(rules, "readinessRecovery", d.ReadinessRecovery),
                    ReadinessDecayMoving = ConditionParser.Int(rules, "readinessDecayMoving", d.ReadinessDecayMoving),
                    IntelDecay = ConditionParser.Int(rules, "intelDecay", d.IntelDecay),
                    ConfidenceInTerritory = ConditionParser.Int(rules, "confidenceInTerritory", d.ConfidenceInTerritory),
                    ConfidenceAdjacent = ConditionParser.Int(rules, "confidenceAdjacent", d.ConfidenceAdjacent),
                    MovePoolCost = ConditionParser.Int(rules, "movePoolCost", d.MovePoolCost),
                    DefenderBonus = Dbl(rules, "defenderBonus", d.DefenderBonus),
                    LoserLossRatio = Dbl(rules, "loserLossRatio", d.LoserLossRatio),
                    WinnerLossRatio = Dbl(rules, "winnerLossRatio", d.WinnerLossRatio),
                    WinnerReadinessCost = ConditionParser.Int(rules, "winnerReadinessCost", d.WinnerReadinessCost),
                    LoserReadinessCost = ConditionParser.Int(rules, "loserReadinessCost", d.LoserReadinessCost),
                    DeclareWarCost = ConditionParser.Int(rules, "declareWarCost", d.DeclareWarCost),
                    PeaceCost = ConditionParser.Int(rules, "peaceCost", d.PeaceCost),
                    PeaceRefusalRatio = Dbl(rules, "peaceRefusalRatio", d.PeaceRefusalRatio),
                    ConquestUnrest = ConditionParser.Int(rules, "conquestUnrest", d.ConquestUnrest),
                    DefaultClass = ConditionParser.OptStr(rules, "defaultClass", d.DefaultClass),
                    SiegeTurns = ConditionParser.Int(rules, "siegeTurns", d.SiegeTurns),
                    VeterancyMax = ConditionParser.Int(rules, "veterancyMax", d.VeterancyMax),
                    VeterancyBonus = Dbl(rules, "veterancyBonus", d.VeterancyBonus),
                    AdmiralDeathChance = Dbl(rules, "admiralDeathChance", d.AdmiralDeathChance),
                    WarWearinessTurns = ConditionParser.Int(rules, "warWearinessTurns", d.WarWearinessTurns),
                    TributePerScore = ConditionParser.Int(rules, "tributePerScore", d.TributePerScore)
                };
            }
            catch (JsonException ex) { throw new ContentException($"Invalid JSON in '{file}': {ex.Message}", ex); }
        }
        return FleetRules.Default;
    }

    /// <summary>Ship classes from any file in /content/fleets carrying a "classes" array.</summary>
    public static ShipClassCatalog LoadClasses(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "fleets");
        if (!Directory.Exists(dir)) return ShipClassCatalog.Empty;
        var defs = new List<ShipClassDef>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).OrderBy(static p => p, StringComparer.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file), DocOptions);
                if (!doc.RootElement.TryGetProperty("classes", out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
                foreach (var el in arr.EnumerateArray())
                    defs.Add(new ShipClassDef
                    {
                        Id = ConditionParser.Str(el, "id"),
                        Name = ConditionParser.OptStr(el, "name", ConditionParser.Str(el, "id")),
                        Attack = ConditionParser.Int(el, "attack", 1),
                        Defense = ConditionParser.Int(el, "defense", 1),
                        Cost = HoldingCatalogLoader.ParseResources(el, "cost"),
                        Upkeep = HoldingCatalogLoader.ParseResources(el, "upkeep"),
                        MinTitleRank = ConditionParser.Int(el, "minTitleRank", 0),
                        Requires = ConditionParser.OptStrOrNull(el, "requires")
                    });
            }
            catch (JsonException ex) { throw new ContentException($"Invalid JSON in '{file}': {ex.Message}", ex); }
        }
        return new ShipClassCatalog(defs);
    }

    private static double Dbl(JsonElement el, string name, double fallback) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : fallback;
}
