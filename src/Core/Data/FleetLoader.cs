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
                    ConquestUnrest = ConditionParser.Int(rules, "conquestUnrest", d.ConquestUnrest)
                };
            }
            catch (JsonException ex) { throw new ContentException($"Invalid JSON in '{file}': {ex.Message}", ex); }
        }
        return FleetRules.Default;
    }

    private static double Dbl(JsonElement el, string name, double fallback) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : fallback;
}
