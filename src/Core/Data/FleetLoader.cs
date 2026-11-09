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
                    FuelHullsPerUnit = ConditionParser.Int(rules, "fuelHullsPerUnit", d.FuelHullsPerUnit),
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
                    TributePerScore = ConditionParser.Int(rules, "tributePerScore", d.TributePerScore),
                    StanceEdge = Dbl(rules, "stanceEdge", d.StanceEdge), PressPower = Dbl(rules, "pressPower", d.PressPower), PressLosses = Dbl(rules, "pressLosses", d.PressLosses),
                    SkirmishPower = Dbl(rules, "skirmishPower", d.SkirmishPower), SkirmishLosses = Dbl(rules, "skirmishLosses", d.SkirmishLosses),
                    BoardingManpower = ConditionParser.Int(rules, "boardingManpower", d.BoardingManpower), BoardingCapturePer = ConditionParser.Int(rules, "boardingCapturePer", d.BoardingCapturePer),
                    TorpedoBonus = Dbl(rules, "torpedoBonus", d.TorpedoBonus),
                    FrontBonus = Dbl(rules, "frontBonus", d.FrontBonus), FrontDeathMult = Dbl(rules, "frontDeathMult", d.FrontDeathMult), AchillesValour = ConditionParser.Int(rules, "achillesValour", d.AchillesValour), AchillesBonus = Dbl(rules, "achillesBonus", d.AchillesBonus),
                    FlagPerTactics = Dbl(rules, "flagPerTactics", d.FlagPerTactics), FlagMax = Dbl(rules, "flagMax", d.FlagMax),
                    CommandCapBase = ConditionParser.Int(rules, "commandCapBase", d.CommandCapBase), CommandCapPerRank = ConditionParser.Int(rules, "commandCapPerRank", d.CommandCapPerRank), OverCapPenalty = Dbl(rules, "overCapPenalty", d.OverCapPenalty),
                    SupplyRange = ConditionParser.Int(rules, "supplyRange", d.SupplyRange), DepotRange = ConditionParser.Int(rules, "depotRange", d.DepotRange), SupplyRecover = ConditionParser.Int(rules, "supplyRecover", d.SupplyRecover),
                    SupplyDrain = ConditionParser.Int(rules, "supplyDrain", d.SupplyDrain), SupplyDrainNear = ConditionParser.Int(rules, "supplyDrainNear", d.SupplyDrainNear), SupplyLow = ConditionParser.Int(rules, "supplyLow", d.SupplyLow), SupplyLowPower = Dbl(rules, "supplyLowPower", d.SupplyLowPower),
                    CampaignCost = ConditionParser.Int(rules, "campaignCost", d.CampaignCost), CampaignTurns = ConditionParser.Int(rules, "campaignTurns", d.CampaignTurns), CampaignRange = ConditionParser.Int(rules, "campaignRange", d.CampaignRange), CampaignBonus = Dbl(rules, "campaignBonus", d.CampaignBonus),
                    LoyaltyStart = ConditionParser.Int(rules, "loyaltyStart", d.LoyaltyStart), LoyaltyRefuse = ConditionParser.Int(rules, "loyaltyRefuse", d.LoyaltyRefuse), LoyaltyMutiny = ConditionParser.Int(rules, "loyaltyMutiny", d.LoyaltyMutiny),
                    LoyaltyWin = ConditionParser.Int(rules, "loyaltyWin", d.LoyaltyWin), LoyaltyLoss = ConditionParser.Int(rules, "loyaltyLoss", d.LoyaltyLoss), LoyaltyDrift = ConditionParser.Int(rules, "loyaltyDrift", d.LoyaltyDrift), LoyaltyUnpaid = ConditionParser.Int(rules, "loyaltyUnpaid", d.LoyaltyUnpaid)
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
