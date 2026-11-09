using Praetoria.Core.Data;
using Praetoria.Core.Rng;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>Tuning for the fleet layer, loaded from /content/fleets (data, not code).</summary>
public sealed class FleetRules
{
    public Resources ShipCost { get; init; } = new() { Materials = 6, Manpower = 3, Credits = 2 };
    public Resources UpkeepPerStrength { get; init; } = new() { Credits = 1 };
    public int ReadinessRecovery { get; init; } = 10;
    public int ReadinessDecayMoving { get; init; } = 5;
    public int IntelDecay { get; init; } = 15;
    public int ConfidenceInTerritory { get; init; } = 100;
    public int ConfidenceAdjacent { get; init; } = 65;
    public int MovePoolCost { get; init; } = 1;
    /// <summary>Hulls per unit of helium-3 a turn (GDD §17 fuel): three hulls burn one, rounded up. Fleets under two hulls run on scraps and pay nothing.</summary>
    public int FuelHullsPerUnit { get; init; } = 3;

    // ---- combat (GDD §6) ----
    public double DefenderBonus { get; init; } = 0.25;
    public double LoserLossRatio { get; init; } = 0.5;
    public double WinnerLossRatio { get; init; } = 0.3;
    public int WinnerReadinessCost { get; init; } = 20;
    public int LoserReadinessCost { get; init; } = 40;
    public int DeclareWarCost { get; init; } = 2;
    public int PeaceCost { get; init; } = 1;
    public double PeaceRefusalRatio { get; init; } = 2.0;
    public int ConquestUnrest { get; init; } = 20;

    // ---- classes, admirals, sieges, war score ----
    public string DefaultClass { get; init; } = "frigate";
    public int SiegeTurns { get; init; } = 2;
    public int VeterancyMax { get; init; } = 5;
    public double VeterancyBonus { get; init; } = 0.05;
    public double AdmiralDeathChance { get; init; } = 0.12;
    public int WarWearinessTurns { get; init; } = 6;
    public int TributePerScore { get; init; } = 3;

    // ---- tactics (docs/MILITARY.md) ----
    public double StanceEdge { get; init; } = 0.15;
    public double PressPower { get; init; } = 1.15; public double PressLosses { get; init; } = 1.3;
    public double SkirmishPower { get; init; } = 0.9; public double SkirmishLosses { get; init; } = 0.6;
    public int BoardingManpower { get; init; } = 2; public int BoardingCapturePer { get; init; } = 4;
    public double TorpedoBonus { get; init; } = 0.25;
    // ---- command ----
    public double FrontBonus { get; init; } = 0.10; public double FrontDeathMult { get; init; } = 2.0; public int AchillesValour { get; init; } = 3; public double AchillesBonus { get; init; } = 0.15;
    public double FlagPerTactics { get; init; } = 0.03; public double FlagMax { get; init; } = 0.15;
    public int CommandCapBase { get; init; } = 2; public int CommandCapPerRank { get; init; } = 3; public double OverCapPenalty { get; init; } = 0.9;
    // ---- supply and campaigns ----
    public int SupplyRange { get; init; } = 1; public int DepotRange { get; init; } = 2; public int SupplyRecover { get; init; } = 25; public int SupplyDrain { get; init; } = 15; public int SupplyDrainNear { get; init; } = 8;
    public int SupplyLow { get; init; } = 40; public double SupplyLowPower { get; init; } = 0.8;
    public int CampaignCost { get; init; } = 2; public int CampaignTurns { get; init; } = 8; public int CampaignRange { get; init; } = 2; public double CampaignBonus { get; init; } = 0.10;
    // ---- loyalty ----
    public int LoyaltyStart { get; init; } = 70; public int LoyaltyRefuse { get; init; } = 30; public int LoyaltyMutiny { get; init; } = 15;
    public int LoyaltyWin { get; init; } = 8; public int LoyaltyLoss { get; init; } = 8; public int LoyaltyDrift { get; init; } = 2; public int LoyaltyUnpaid { get; init; } = 4;

    public static FleetRules Default { get; } = new();
}

/// <summary>
/// The fleet layer (GDD §6 military pillar, §8 office/house fleets, §19 map behaviour): transit along lanes,
/// readiness, upkeep by hull class, incursions, the protagonist house's intel, <b>battle</b> (composition ×
/// readiness × admiral × veterancy, defenders fortified by their holdings), <b>sieges</b> that end in conquest,
/// war score and weariness. RNG only where a fight is decided; everything else id-ordered and deterministic.
/// </summary>
public sealed class FleetSystem
{
    public const string IncursionsCounter = "incursions";
    public static string IncursionFlag(string houseId) => $"incursion:{houseId}";
    public const string BattlesCounter = "battles";
    public const string BattlesWonCounter = "battles_won";
    public const string BattlesLostCounter = "battles_lost";
    public const string ConquestsCounter = "conquests";
    public const string BattleFoughtFlag = "battle_fought";
    /// <summary>One-turn flags of the military depth (docs/MILITARY.md), cleared at the top of <see cref="Tick"/>.</summary>
    public static readonly string[] DepthTurnFlags = { "battle_won", "battle_lost", "fought_at_the_front", "hulls_captured", "became_achilles", "fleet_deserts", "campaign_over" };
    public const string FrontFlag = "command:front", FlagFlag = "command:flag", AchillesFlag = "achilles";
    public static string ValourKey(string characterId) => "valour:" + characterId;
    public static string CampaignUntilKey(string houseId) => "campaign_until:" + houseId;
    public static string CampaignPrefix(string houseId) => $"campaign:{houseId}:";
    /// <summary>The system a house's campaign names, or null.</summary>
    public static string? CampaignTarget(World w, string houseId) => w.Counter(CampaignUntilKey(houseId)) > w.Turn ? w.WorldFlags.FirstOrDefault(f => f.StartsWith(CampaignPrefix(houseId)))?[CampaignPrefix(houseId).Length..] : null;
    public static readonly string[] Stances = { "line", "press", "skirmish", "boarding" };
    public static readonly string[] Loadouts = { "guns", "torpedoes" };
    public const string ConquestMadeFlag = "conquest_made";
    public const string HoldingLostFlag = "holding_lost";
    public const string UnderSiegeFlag = "under_siege";          // one of the player's holdings is blockaded
    public const string WarsDeclaredCounter = "wars_declared";
    public static string AtWarFlag(string houseId) => $"at_war:{houseId}";
    public const string SuingForPeaceFlag = "suing_for_peace";
    public const string AdmiralFlag = "admiral";                  // the character commands a fleet

    private readonly FleetRules _rules;
    private readonly HoldingCatalog _catalog;
    private readonly ShipClassCatalog _classes;
    private readonly FrictionTable _friction;

    public FleetSystem(FleetRules rules, HoldingCatalog? catalog = null, ShipClassCatalog? classes = null, FrictionTable? friction = null)
    {
        _rules = rules; _catalog = catalog ?? HoldingCatalog.Empty; _classes = classes ?? ShipClassCatalog.Empty; _friction = friction ?? FrictionTable.Empty;
    }

    public FleetRules Rules => _rules;
    public ShipClassCatalog Classes => _classes;
    public List<BattleReport> LastBattles { get; } = new();

    private ShipClassDef Class(string id) => _classes.Resolve(id);
    private ShipClassDef DefaultClass => _classes.Resolve(_rules.DefaultClass);

    // ---------------- composition ----------------

    /// <summary>Hulls per class, materialising the default class for fleets authored by strength alone.</summary>
    public IEnumerable<(ShipClassDef cls, int count)> Composition(Fleet f)
    {
        if (f.Ships.Count == 0) { if (f.Strength > 0) yield return (DefaultClass, f.Strength); yield break; }
        foreach (var kv in f.Ships.OrderBy(k => k.Key, StringComparer.Ordinal)) if (kv.Value > 0) yield return (Class(kv.Key), kv.Value);
    }

    public string Describe(Fleet f) =>
        string.Join(", ", Composition(f).Select(c => $"{c.count} {c.cls.Name.ToLowerInvariant()}{(c.count == 1 ? "" : "s")}"));

    /// <summary>Per-turn upkeep of a fleet from its classes.</summary>
    /// <summary>Laws and estates (PolicySystem): the household guard, the Admiral's doctrine, the officers' regard.</summary>
    public PolicyCatalog Policies { get; set; } = PolicyCatalog.Empty;
    private World? _world;
    public Resources Upkeep(Fleet f)
    {
        var r = new Resources();
        foreach (var (cls, n) in Composition(f)) foreach (var k in Resource.All) r.Add(k, cls.Upkeep.Get(k) * n);
        int pct = _world != null ? PolicySystem.Modifier(_world, Policies, f.OwnerId, "fleet_upkeep_pct") : 0; if (pct != 0 && r.Credits > 0) r.Credits = Math.Max(0, r.Credits * (100 + pct) / 100);
        return r;
    }

    // ---------------- the turn ----------------

    public void Tick(World w)
    {
        _world = w;
        LastBattles.Clear();
        foreach (var f in DepthTurnFlags) w.WorldFlags.Remove(f);
        if (w.Galaxy == null) { UpdateIntel(w); return; }
        Supply(w);
        Campaigns(w);

        foreach (var f in w.Fleets.Values.OrderBy(f => f.Id, StringComparer.Ordinal))
        {
            if (f.IsAway) continue;   // off the map (docs/FOREIGN_NATIONS.md)
            if (f.Moving)
            {
                f.Eta -= 1;
                f.Readiness = Math.Max(0, f.Readiness - _rules.ReadinessDecayMoving);
                if (f.Eta <= 0) { f.SystemId = f.DestinationId; f.DestinationId = ""; f.Eta = 0; Arrive(w, f); }
            }
            else f.Readiness = Math.Clamp(f.Readiness + _rules.ReadinessRecovery + PolicySystem.Modifier(w, Policies, f.OwnerId, "readiness"), 0, 100);
        }

        var chairId = RegionSystem.CapitalHolder(w)?.Id;
        foreach (var house in w.Houses.Values.OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            var bill = new Resources();
            foreach (var f in w.Fleets.Values.Where(f => BilledTo(f, chairId) == house.Id)) bill.Add(Upkeep(f));
            if (bill.IsEmpty()) continue;
            foreach (var r in Resource.All) house.Treasury.Add(r, -bill.Get(r));
            house.Treasury.ClampNonCredit();
            Desert(w, house);
            Refuel(w, house);
        }
        Loyalty(w);

        RefreshAdmiralFlags(w);
        WarWeariness(w);
        UpdateIntel(w);
    }

    // ---------------- supply, campaigns, loyalty (docs/MILITARY.md) ----------------

    /// <summary>Is the system within the house's supply: a world of the house within <see cref="FleetRules.SupplyRange"/> lanes, a supply
    /// depot within <see cref="FleetRules.DepotRange"/>, or the house's campaign target within its range.</summary>
    public bool InSupply(World w, string houseId, string systemId)
    {
        var g = w.Galaxy; if (g == null) return true;
        foreach (var h in w.HoldingsOf(houseId).Where(h => h.IsPlanet && h.SystemId.Length > 0))
        {
            int range = h.Buildings.Contains("supply_depot") ? _rules.DepotRange : _rules.SupplyRange;
            if (h.SystemId == systemId || g.Hops(h.SystemId, systemId) is > 0 and var d && d <= range) return true;
        }
        var target = CampaignTarget(w, houseId);
        if (target != null && (target == systemId || g.Hops(target, systemId) is > 0 and var cd && cd <= _rules.CampaignRange)) return true;
        return false;
    }
    /// <summary>Within two lanes of a world of the house: out of supply, but near enough that the drain is gentler.</summary>
    private static bool NearHome(World w, string houseId, string systemId) =>
        w.Galaxy != null && w.HoldingsOf(houseId).Any(h => h.IsPlanet && h.SystemId.Length > 0 && (h.SystemId == systemId || w.Galaxy.Hops(h.SystemId, systemId) is > 0 and <= 2));

    private void Supply(World w)
    {
        var mine = w.Protagonist?.HouseId;
        foreach (var f in w.Fleets.Values.OrderBy(f => f.Id, StringComparer.Ordinal).ToList())
        {
            if (f.OwnerId == PirateSystem.HouseId) { f.Supply = 100; continue; }   // raiders live off the lanes
            if (f.IsAway) continue;   // an expedition's supply is the NationSystem's to drain
            string at = f.Moving ? f.DestinationId : f.SystemId;
            if (InSupply(w, f.OwnerId, at)) f.Supply = Math.Min(100, f.Supply + _rules.SupplyRecover);
            else
            {
                bool starved = f.Supply == 0;   // a fleet that begins the turn with nothing loses a hull; the turn it runs dry it only goes hungry
                f.Supply = Math.Max(0, f.Supply - (NearHome(w, f.OwnerId, at) ? _rules.SupplyDrainNear : _rules.SupplyDrain));
                if (f.Supply < _rules.SupplyLow) f.Readiness = Math.Max(0, f.Readiness - 5);
                if (starved && f.Strength > 1) { ShedHull(w, f); w.Log($"{f.Name}, out of supply at {w.Galaxy?.System(at)?.Name ?? at}, loses a hull to desertion."); if (f.OwnerId == mine) w.WorldFlags.Add("fleet_deserts"); }
            }
        }
        foreach (var f in new[] { "out_of_supply", "supply_low" }) w.WorldFlags.Remove(f);
        if (mine != null)
        {
            if (w.Fleets.Values.Any(f => f.OwnerId == mine && f.Supply == 0)) w.WorldFlags.Add("out_of_supply");
            else if (w.Fleets.Values.Any(f => f.OwnerId == mine && f.Supply < _rules.SupplyLow)) w.WorldFlags.Add("supply_low");
        }
    }

    internal static void ShedHull(World w, Fleet f)
    {
        if (f.Ships.Count > 0) { var cls = f.Ships.Where(k => k.Value > 0).OrderByDescending(k => k.Value).ThenBy(k => k.Key, StringComparer.Ordinal).First().Key; f.Ships[cls] -= 1; if (f.Ships[cls] == 0) f.Ships.Remove(cls); f.Recount(); }
        else f.Strength -= 1;
    }

    private void Campaigns(World w)
    {
        var mine = w.Protagonist?.HouseId;
        foreach (var key in w.WorldCounters.Keys.Where(k => k.StartsWith("campaign_until:")).ToList())
        {
            if (w.WorldCounters[key] > w.Turn) continue;
            string houseId = key["campaign_until:".Length..];
            foreach (var f in w.WorldFlags.Where(f => f.StartsWith(CampaignPrefix(houseId))).ToList()) w.WorldFlags.Remove(f);
            w.WorldCounters.Remove(key);
            if (houseId == mine) w.WorldFlags.Add("campaign_over");
            w.Log($"The campaign of {w.House(houseId)?.Name ?? houseId} is over; its fleets are recalled to their stations.");
        }
        foreach (var f in new[] { "campaign_open" }) w.WorldFlags.Remove(f);
        if (mine != null && CampaignTarget(w, mine) != null) w.WorldFlags.Add("campaign_open");
    }

    /// <summary>Loyalty drifts with pay, fuel, supply and the officers' regard (PolicySystem); under the refusal line the crews will not sail
    /// (MoveFleetCommand), under the mutiny line a hull deserts a turn.</summary>
    private void Loyalty(World w)
    {
        var mine = w.Protagonist?.HouseId;
        w.WorldFlags.Remove("fleet_mutinous"); w.WorldFlags.Remove("fleet_restive");
        foreach (var f in w.Fleets.Values.OrderBy(f => f.Id, StringComparer.Ordinal).ToList())
        {
            if (f.OwnerId == PirateSystem.HouseId) continue;
            var house = w.House(f.OwnerId); if (house == null) continue;
            int d = _rules.LoyaltyDrift;
            if (house.Treasury.Credits < 0) d -= _rules.LoyaltyUnpaid;
            if (w.Counter(FuelShortKey(f.OwnerId)) > 0) d -= 2;
            if (f.Supply < _rules.SupplyLow) d -= 2;
            int officers = PolicySystem.Approval(w, f.OwnerId, "officers");
            if (officers <= PolicySystem.Hostile) d -= 2; else if (officers >= PolicySystem.Loyal) d += 1;
            if (f.AdmiralId.Length > 0 && w.Char(f.AdmiralId) is { Alive: true } adm && adm.Flags.Contains(FrontFlag)) d += 1;   // crews love a commander who shares the line
            f.Loyalty = Math.Clamp(f.Loyalty + d, 0, 100);
            if (f.Loyalty < _rules.LoyaltyMutiny && f.Strength > 1)
            {
                ShedHull(w, f); w.Log($"A hull of {f.Name} deserts: the crews have had enough of {house.Name}.");
                if (f.OwnerId == mine) { w.WorldFlags.Add("fleet_deserts"); }
            }
            if (f.OwnerId == mine) { if (f.Loyalty < _rules.LoyaltyMutiny) w.WorldFlags.Add("fleet_mutinous"); else if (f.Loyalty < _rules.LoyaltyRefuse) w.WorldFlags.Add("fleet_restive"); }
        }
    }

    /// <summary>The hulls a commander may lead without strain: the base plus the rank's allowance on the military track (any other track, the base alone).</summary>
    public int CommandCap(Character? c) => c == null ? int.MaxValue : _rules.CommandCapBase + (c.CareerTrack == "military" ? c.CareerRank : 0) * _rules.CommandCapPerRank;

    /// <summary>The dominant stance of a house's fleets in a system: the largest fleet's.</summary>
    public static string StanceAt(World w, string houseId, string systemId) =>
        w.Fleets.Values.Where(f => f.OwnerId == houseId && !f.Moving && f.SystemId == systemId).OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault()?.Stance ?? "line";
    /// <summary>Who has the edge: the line beats the press, the press beats the skirmish, the skirmish beats the line; boarding fights as the press.</summary>
    public static int StanceEdgeOf(string a, string b)
    {
        string A = a == "boarding" ? "press" : a, B = b == "boarding" ? "press" : b;
        if (A == B) return 0;
        return (A, B) switch { ("line", "press") => 1, ("press", "skirmish") => 1, ("skirmish", "line") => 1, _ => -1 };
    }

    public const int FuelReadinessLoss = 15, LayUpTurns = 6;
    public static string FuelShortKey(string houseId) => "fuel_short:" + houseId;

    /// <summary>Helium-3 the house's fleets burn this turn: one unit per <see cref="FleetRules.FuelHullsPerUnit"/> hulls, rounded up;
    /// nothing under two hulls, nothing for the Lawless.</summary>
    /// <summary>Who pays a fleet's upkeep: its owner, except an office fleet, which the chair pays for (GDD §8) — the owner if there is no chair.</summary>
    public static string BilledTo(Fleet f, string? chairId) => f.Office && chairId != null ? chairId : f.OwnerId;
    public int FuelBill(World w, string houseId) => FuelBillFor(w, houseId, _rules.FuelHullsPerUnit);
    public static int FuelBillFor(World w, string houseId, int hullsPerUnit)
    {
        if (houseId == PirateSystem.HouseId) return 0;
        int hulls = w.Fleets.Values.Where(f => f.OwnerId == houseId && !f.Office).Sum(f => f.Strength);   // the state's bunkers fuel the office fleets
        return hulls < 2 ? 0 : (hulls + hullsPerUnit - 1) / Math.Max(1, hullsPerUnit);
    }

    /// <summary>A house whose tanks have run dry: its fleets ride at anchor (GDD §17 fuel). Counter <c>fuel_short:&lt;house&gt;</c> counts the turns.</summary>
    public static bool Unfuelled(World w, string houseId) => w.Counter(FuelShortKey(houseId)) > 0 && (w.House(houseId)?.Treasury.Helium3 ?? 0) == 0;

    /// <summary>The fleet burns helium-3 (GDD §17 "fuel is the other thing you run out of"): paid in full the tanks are fine; short,
    /// readiness slides, the fleets cannot sail, and the player hears of it through <c>fuel_short</c>.</summary>
    private void Refuel(World w, House house)
    {
        int need = FuelBill(w, house.Id); string key = FuelShortKey(house.Id); bool mine = house.Id == w.Protagonist?.HouseId;
        if (need == 0 || house.Treasury.Helium3 >= need)
        {
            house.Treasury.Helium3 -= need;
            if (w.WorldCounters.Remove(key) && mine) w.WorldFlags.Remove("fuel_short");
            return;
        }
        house.Treasury.Helium3 = 0;
        int turns = w.Counter(key) + 1; w.WorldCounters[key] = turns;
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == house.Id)) f.Readiness = Math.Max(0, f.Readiness - FuelReadinessLoss);
        if (mine) w.WorldFlags.Add("fuel_short");
        if (turns == 1) w.Log($"The tanks of {house.Name} run dry: its fleets ride at anchor for want of helium-3 ({need} a turn).");
        else if (turns % LayUpTurns == 0)   // a navy dry for years is laid up a hull at a time: fewer mouths, a smaller bill, a way back
        {
            var victim = w.Fleets.Values.Where(f => f.OwnerId == house.Id && f.Strength > 0).OrderBy(f => f.Readiness).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
            if (victim != null)
            {
                if (victim.Ships.Count > 0) { var cls = victim.Ships.Where(k => k.Value > 0).OrderByDescending(k => k.Value).ThenBy(k => k.Key, StringComparer.Ordinal).First().Key; victim.Ships[cls] -= 1; if (victim.Ships[cls] == 0) victim.Ships.Remove(cls); victim.Recount(); if (victim.Ships.Count == 0) victim.Strength = 0; }
                else victim.Strength -= 1;
                if (victim.Strength <= 0) { w.Fleets.Remove(victim.Id); w.Intel.Remove(victim.Id); }
                w.WorldCounters["laid_up"] = w.Counter("laid_up") + 1;
                w.Log($"Dry these {turns} years, {house.Name} lays up a hull of {victim.Name} for want of helium-3.");
            }
        }
    }

    /// <summary>Credits a house is allowed to owe before its unpaid crews start walking (GDD §17 "the one you run out of").</summary>
    public const int DesertionDebt = 20;
    public const int UnpaidReadinessLoss = 10;
    public const string DesertionsCounter = "desertions";

    /// <summary>An insolvent house cannot pay its crews: readiness slides, and past <see cref="DesertionDebt"/> one hull a turn
    /// slips away from the least-ready fleet. Debt is therefore self-limiting — a fleet shrinks until its upkeep is payable —
    /// and the chronicle says why the picket thinned.</summary>
    private void Desert(World w, House house)
    {
        if (house.Treasury.Credits >= 0) return;
        var fleets = w.Fleets.Values.Where(f => f.OwnerId == house.Id).OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
        if (fleets.Count == 0) return;
        foreach (var f in fleets) f.Readiness = Math.Max(0, f.Readiness - UnpaidReadinessLoss);
        if (house.Treasury.Credits > -DesertionDebt) return;

        var victim = fleets.OrderBy(f => f.Readiness).ThenBy(f => f.Id, StringComparer.Ordinal).First();
        if (victim.Ships.Count > 0)
        {
            var cls = victim.Ships.Where(k => k.Value > 0).OrderByDescending(k => k.Value).ThenBy(k => k.Key, StringComparer.Ordinal).First().Key;
            victim.Ships[cls] -= 1;
            if (victim.Ships[cls] == 0) victim.Ships.Remove(cls);
            victim.Recount();
            if (victim.Ships.Count == 0) victim.Strength = 0;
        }
        else victim.Strength -= 1;
        w.WorldCounters[DesertionsCounter] = w.Counter(DesertionsCounter) + 1;
        if (victim.Strength <= 0)
        {
            w.Fleets.Remove(victim.Id); w.Intel.Remove(victim.Id);
            w.Log($"Unpaid for too long, the last crews of {victim.Name} desert; {house.Name} has no fleet left there.");
        }
        else w.Log($"Unpaid crews desert {victim.Name}: {house.Name} owes {-house.Treasury.Credits} credits it does not have.");
    }

    private static void Arrive(World w, Fleet f)
    {
        string? owner = TerritoryOwner(w, f.SystemId);
        var sys = w.Galaxy!.System(f.SystemId);
        if (f.OwnerId == PirateSystem.HouseId)   // raiders roam every turn; the chronicle hears of them only when they reach someone's world
        {
            if (owner != null && w.PlanetHolder(f.SystemId) == owner) w.Log($"{f.Name} prowl {sys?.Name ?? f.SystemId} — {w.House(owner)?.Name ?? owner}'s world lies under their guns.");
            return;
        }
        if (owner != null && owner != f.OwnerId)
        {
            w.WorldCounters[IncursionsCounter] = w.Counter(IncursionsCounter) + 1;
            w.WorldFlags.Add(IncursionFlag(owner));
            w.Log($"{f.Name} of {w.House(f.OwnerId)?.Name ?? f.OwnerId} arrives at {sys?.Name ?? f.SystemId} — {w.House(owner)?.Name ?? owner}'s space.");
        }
        else w.Log($"{f.Name} arrives at {sys?.Name ?? f.SystemId}.");
    }

    public static string? TerritoryOwner(World w, string systemId)
    {
        var owners = w.Holdings.Values.Where(h => h.SystemId == systemId)
            .GroupBy(h => h.OwnerId).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key).ToList();
        return owners.Count == 0 ? null : owners[0];
    }

    public static int StrengthOf(World w, string houseId) => w.Fleets.Values.Where(f => f.OwnerId == houseId).Sum(f => f.Strength);
    public static int StrengthAt(World w, string houseId, string systemId) =>
        w.Fleets.Values.Where(f => f.OwnerId == houseId && !f.Moving && f.SystemId == systemId).Sum(f => f.Strength);

    /// <summary>Characters commanding a fleet carry the <c>admiral</c> flag so content can bind "the admiral".</summary>
    public static void RefreshAdmiralFlags(World w)
    {
        var admirals = w.Fleets.Values.Select(f => f.AdmiralId).Where(a => a.Length > 0).ToHashSet();
        foreach (var c in w.Characters.Values)
        {
            if (admirals.Contains(c.Id) && c.Alive) c.Flags.Add(AdmiralFlag); else c.Flags.Remove(AdmiralFlag);
        }
        foreach (var f in w.Fleets.Values) if (f.AdmiralId.Length > 0 && w.Char(f.AdmiralId) is not { Alive: true }) f.AdmiralId = "";
    }

    /// <summary>Long wars grind: past the weariness threshold, every holding of a warring house grows restless each turn.</summary>
    public const int ExhaustionTurns = 12;   // a war between rivals with no battle this long ends in a white peace

    private void WarWeariness(World w)
    {
        foreach (var war in w.Wars)
        {
            if (w.Turn - war.Since < _rules.WarWearinessTurns) continue;
            foreach (var h in w.Holdings.Values.Where(h => war.Involves(h.OwnerId))) h.Unrest = Math.Min(100, h.Unrest + 2);
        }
        // exhaustion (GAP §7 "wars are frequent and bloodless"): two rival houses that have not met in battle for twelve turns go home
        var player = w.Protagonist?.HouseId;
        foreach (var war in w.Wars.Where(x => !x.Involves(player ?? "") && w.Turn - Math.Max(x.Since, x.LastBattleTurn) >= ExhaustionTurns).ToList())
        {
            w.Log($"The war between {w.House(war.HouseA)?.Name ?? war.HouseA} and {w.House(war.HouseB)?.Name ?? war.HouseB} ends in exhaustion; no fleet has met another in a dozen turns.");
            MakePeace(w, war.HouseA, war.HouseB, terms: false);
        }
    }

    public static HashSet<string> Watched(World w)
    {
        var set = new HashSet<string>();
        var house = w.Protagonist?.HouseId;
        if (w.Galaxy == null || house == null) return set;
        foreach (var s in w.Holdings.Values.Where(h => h.OwnerId == house && h.SystemId != "").Select(h => h.SystemId).Distinct().ToList())
        { set.Add(s); foreach (var n in w.Galaxy.Neighbors(s)) set.Add(n); }
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == house && !f.Moving && !f.IsAway)) { set.Add(f.SystemId); foreach (var n in w.Galaxy.Neighbors(f.SystemId)) set.Add(n); }
        return set;
    }

    public void UpdateIntel(World w)
    {
        var house = w.Protagonist?.HouseId;
        if (house == null) return;
        var ownSystems = w.Holdings.Values.Where(h => h.OwnerId == house).Select(h => h.SystemId).ToHashSet();
        var watched = Watched(w);
        foreach (var f in w.Fleets.Values.OrderBy(f => f.Id, StringComparer.Ordinal))
        {
            if (f.OwnerId == house) continue;
            bool seen = watched.Contains(f.SystemId) || (f.Moving && watched.Contains(f.DestinationId)) || f.OwnerId == PirateSystem.HouseId;   // raiders are talked about on every lane
            if (seen)
                w.Intel[f.Id] = new FleetIntel { FleetId = f.Id, SystemId = f.SystemId, Strength = f.Strength,
                    Confidence = ownSystems.Contains(f.SystemId) ? _rules.ConfidenceInTerritory : _rules.ConfidenceAdjacent, TurnObserved = w.Turn };
            else if (w.Intel.TryGetValue(f.Id, out var old)) old.Confidence = Math.Max(0, old.Confidence - _rules.IntelDecay);
        }
        foreach (var id in w.Intel.Keys.Where(id => !w.Fleets.ContainsKey(id)).ToList()) w.Intel.Remove(id);
    }

    // ---------------- war & peace ----------------

    public void DeclareWar(World w, string a, string b, string? byCharacterId = null)
    {
        if (a == b || w.AtWar(a, b)) return;
        w.Wars.Add(new War { HouseA = a, HouseB = b, Since = w.Turn });
        w.WorldCounters[WarsDeclaredCounter] = w.Counter(WarsDeclaredCounter) + 1;
        RefreshWarFlags(w);
        var who = byCharacterId != null ? w.Char(byCharacterId) : null;
        w.Log($"{(who?.Name ?? w.House(a)?.Name ?? a)} declares war on {w.House(b)?.Name ?? b}.");
        if (who != null) _friction.Apply(w, who, "declare_war");
    }

    public const int CessionLead = 10;   // the side this far behind cedes a world at the peace

    public void MakePeace(World w, string a, string b) => MakePeace(w, a, b, terms: true);

    /// <summary>End a war. With <paramref name="terms"/>, the house behind by <see cref="CessionLead"/> or more cedes its least peopled
    /// world (never the capital) to the winner: wars have prizes (GAP §7).</summary>
    public void MakePeace(World w, string a, string b, bool terms)
    {
        var war = w.Wars.FirstOrDefault(x => x.Between(a, b));
        if (war == null) return;
        w.Wars.Remove(war);
        if (terms)
        {
            string? loser = war.Lead(a) >= CessionLead ? b : war.Lead(b) >= CessionLead ? a : null;
            if (loser != null)
            {
                string winner = loser == a ? b : a;
                var ceded = w.HoldingsOf(loser).Where(h => h.IsPlanet && !(w.Galaxy is { Capital.Length: > 0 } g && g.Capital == h.SystemId)).OrderBy(h => h.Population).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
                if (ceded != null && w.HoldingsOf(loser).Count(h => h.IsPlanet) > 1)
                {
                    ceded.FormerOwnerId = loser; ceded.OwnerId = winner; ceded.Unrest = Math.Min(100, ceded.Unrest + 10);
                    var newGov = Economy.HeadOf(w, w.House(winner)!); if (newGov != null) ceded.GovernorId = newGov.Id;
                    w.WorldCounters["cessions"] = w.Counter("cessions") + 1;
                    var player = w.Protagonist?.HouseId; if (winner == player) w.WorldFlags.Add("won_a_world"); if (loser == player) w.WorldFlags.Add("ceded_a_world");
                    w.Log($"By the terms of the peace, {w.House(loser)?.Name ?? loser} cedes {ceded.Name} to {w.House(winner)?.Name ?? winner}.");
                }
            }
        }
        foreach (var s in w.Sieges.Values.Where(s => (war.Involves(s.BesiegerId)) && war.Involves(w.Holding(s.HoldingId)?.OwnerId ?? "")).ToList()) w.Sieges.Remove(s.HoldingId);
        foreach (var c in w.Characters.Values.Where(c => war.Involves(c.HouseId))) c.Flags.Remove(SuingForPeaceFlag);
        RefreshWarFlags(w);
        RefreshSiegeFlag(w);
        w.Log($"{w.House(a)?.Name ?? a} and {w.House(b)?.Name ?? b} make peace.");
    }

    /// <summary>Tribute the lower-scoring side pays the higher on peace (credits), from the war's score difference.</summary>
    public int Tribute(World w, string from, string to)
    {
        var war = w.Wars.FirstOrDefault(x => x.Between(from, to));
        if (war == null) return 0;
        int lead = war.Lead(to);
        return lead > 0 ? Math.Min(w.House(from)?.Treasury.Credits ?? 0, lead * _rules.TributePerScore) : 0;
    }

    /// <summary>Would NPC house <paramref name="houseId"/> accept peace from <paramref name="from"/>? Not while it smells
    /// victory — ahead on war score, or hulls past the refusal ratio.</summary>
    public bool WouldAcceptPeace(World w, string houseId, string from)
    {
        var war = w.Wars.FirstOrDefault(x => x.Between(houseId, from));
        if (war != null && war.Lead(houseId) >= 5 && StrengthOf(w, houseId) >= StrengthOf(w, from)) return false;
        return StrengthOf(w, houseId) <= Math.Max(1, StrengthOf(w, from)) * _rules.PeaceRefusalRatio;
    }

    public static void RefreshWarFlags(World w)
    {
        foreach (var f in w.WorldFlags.Where(f => f.StartsWith("at_war:")).ToList()) w.WorldFlags.Remove(f);
        w.WorldFlags.Remove("at_war_any");
        var house = w.Protagonist?.HouseId;
        if (house == null) return;
        foreach (var e in w.EnemiesOf(house)) w.WorldFlags.Add(AtWarFlag(e));
        if (w.EnemiesOf(house).Any()) w.WorldFlags.Add("at_war_any");   // the generic flag content reads (GAP_ANALYSIS §4 item 4)
    }

    private static void RefreshSiegeFlag(World w)
    {
        var house = w.Protagonist?.HouseId;
        bool any = house != null && w.Sieges.Values.Any(s => w.Holding(s.HoldingId)?.OwnerId == house);
        if (any) w.WorldFlags.Add(UnderSiegeFlag); else w.WorldFlags.Remove(UnderSiegeFlag);
    }

    // ---------------- battle ----------------

    public void ResolveBattles(World w, IRng rng)
    {
        LastBattles.Clear();
        if (w.Wars.Count == 0 || w.Galaxy == null) return;
        var player = w.Protagonist?.HouseId;

        foreach (var systemId in w.Fleets.Values.Where(f => !f.Moving && !f.IsAway).Select(f => f.SystemId).Distinct().OrderBy(s => s, StringComparer.Ordinal).ToList())
        {
            var houses = w.Fleets.Values.Where(f => !f.Moving && f.SystemId == systemId).Select(f => f.OwnerId).Distinct().OrderBy(h => h, StringComparer.Ordinal).ToList();
            for (int i = 0; i < houses.Count; i++)
                for (int j = i + 1; j < houses.Count; j++)
                {
                    string a = houses[i], b = houses[j];
                    if (!w.AtWar(a, b) || StrengthAt(w, a, systemId) == 0 || StrengthAt(w, b, systemId) == 0) continue;
                    var report = Battle(w, rng, systemId, a, b);
                    LastBattles.Add(report);
                    w.WorldCounters[BattlesCounter] = w.Counter(BattlesCounter) + 1;
                    var scored = w.Wars.FirstOrDefault(x => x.Between(a, b)); if (scored != null) { scored.AddScore(report.Winner, 2); scored.LastBattleTurn = w.Turn; }   // raiders have no war to score
                    if (player != null && (a == player || b == player))
                    {
                        w.WorldFlags.Add(BattleFoughtFlag);
                        bool won = report.Winner == player;
                        w.WorldCounters[won ? BattlesWonCounter : BattlesLostCounter] = w.Counter(won ? BattlesWonCounter : BattlesLostCounter) + 1;
                        _friction.Apply(w, w.Protagonist, won ? "battle_won" : "battle_lost");
                    }
                }
        }
    }

    private BattleReport Battle(World w, IRng rng, string systemId, string a, string b)
    {
        string? owner = TerritoryOwner(w, systemId);
        string attacker = owner == a ? b : a, defender = owner == a ? a : b;
        double pa = Power(w, attacker, systemId, defending: false);
        double pd = Power(w, defender, systemId, defending: owner == defender);
        string sa = StanceAt(w, attacker, systemId), sd = StanceAt(w, defender, systemId); int edge = StanceEdgeOf(sa, sd);
        if (edge > 0) pa *= 1 + _rules.StanceEdge; else if (edge < 0) pd *= 1 + _rules.StanceEdge;
        foreach (var f in w.Fleets.Values.Where(f => !f.Moving && f.SystemId == systemId && (f.OwnerId == a || f.OwnerId == b) && f.Loadout == "torpedoes"))
            if (w.House(f.OwnerId) is { } th && th.Treasury.Isotopes > 0) th.Treasury.Isotopes -= 1;   // the magazines are spent whatever the outcome
        double roll = rng.NextDouble();
        bool attackerWins = roll < pa / (pa + pd);
        string winner = attackerWins ? attacker : defender, loser = attackerWins ? defender : attacker;
        double pw = attackerWins ? pa : pd, pl = attackerWins ? pd : pa;

        int loserStrength = StrengthAt(w, loser, systemId), winnerStrength = StrengthAt(w, winner, systemId);
        string sWin = attackerWins ? sa : sd, sLose = attackerWins ? sd : sa;
        double LossMult(string st) => st switch { "press" => _rules.PressLosses, "boarding" => _rules.PressLosses, "skirmish" => _rules.SkirmishLosses, _ => 1.0 };
        int loserLosses = Math.Min(loserStrength, Math.Max(1, (int)Math.Ceiling(loserStrength * _rules.LoserLossRatio * LossMult(sLose))));
        int winnerLosses = Math.Max(0, Math.Min(winnerStrength - 1, (int)Math.Floor(winnerStrength * _rules.WinnerLossRatio * Math.Min(1.0, pl / pw) * LossMult(sWin))));

        var loserAdmirals = Admirals(w, loser, systemId); var winnerAdmirals = Admirals(w, winner, systemId);
        ApplyLosses(w, loser, systemId, loserLosses, _rules.LoserReadinessCost);
        ApplyLosses(w, winner, systemId, winnerLosses, _rules.WinnerReadinessCost);
        var player = w.Protagonist?.HouseId;
        // boarding: the victor's prize crews take a hull for every few the loser shed, for manpower
        if (sWin == "boarding" && w.House(winner) is { } wh && wh.Treasury.Manpower >= _rules.BoardingManpower && loserLosses >= 2)
        {
            int prizes = Math.Max(1, loserLosses / _rules.BoardingCapturePer); wh.Treasury.Manpower -= _rules.BoardingManpower;
            var into = w.Fleets.Values.Where(f => f.OwnerId == winner && !f.Moving && f.SystemId == systemId).OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
            if (into != null) { if (into.Ships.Count == 0) into.Ships[_rules.DefaultClass] = into.Strength; into.Ships[_rules.DefaultClass] = into.Ships.GetValueOrDefault(_rules.DefaultClass) + prizes; into.Recount(); w.Log($"Prize crews of {into.Name} take {prizes} hull(s) of the enemy as their own."); if (winner == player) w.WorldFlags.Add("hulls_captured"); }
        }
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == winner && !f.Moving && f.SystemId == systemId))
        { f.Veterancy = Math.Min(_rules.VeterancyMax, f.Veterancy + 1); f.Loyalty = Math.Min(100, f.Loyalty + _rules.LoyaltyWin); }
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == loser && !f.Moving && f.SystemId == systemId)) f.Loyalty = Math.Max(0, f.Loyalty - _rules.LoyaltyLoss);
        if (player != null && (winner == player || loser == player))
        {
            w.WorldFlags.Add(winner == player ? "battle_won" : "battle_lost");
            if (winnerAdmirals.Concat(loserAdmirals).Any(c => c.Id == w.ProtagonistId && c.Flags.Contains(FrontFlag))) w.WorldFlags.Add("fought_at_the_front");
        }
        AdmiralOutcomes(w, rng, winnerAdmirals, loserAdmirals);
        Retreat(w, loser, systemId);

        var sys = w.Galaxy!.System(systemId)?.Name ?? systemId;
        w.Log($"Battle of {sys}: {w.House(winner)?.Name ?? winner} defeats {w.House(loser)?.Name ?? loser} " +
              $"({w.House(attacker)?.Name ?? attacker} attacking, {w.House(defender)?.Name ?? defender} holding). " +
              $"Hulls lost — {w.House(attacker)?.Name ?? attacker} {(attackerWins ? winnerLosses : loserLosses)}, {w.House(defender)?.Name ?? defender} {(attackerWins ? loserLosses : winnerLosses)}.");
        return new BattleReport(systemId, attacker, defender, winner, attackerWins ? winnerLosses : loserLosses, attackerWins ? loserLosses : winnerLosses, pa, pd, w.Turn);
    }

    private static List<Character> Admirals(World w, string houseId, string systemId) =>
        w.Fleets.Values.Where(f => f.OwnerId == houseId && !f.Moving && f.SystemId == systemId && f.AdmiralId.Length > 0)
            .Select(f => w.Char(f.AdmiralId)).Where(c => c is { Alive: true }).Select(c => c!).Distinct().OrderBy(c => c.Id, StringComparer.Ordinal).ToList();

    /// <summary>The victor's admiral learns (tactics, then gunnery); the loser's is wounded — or, on a seeded roll, killed
    /// (GDD §5 "losing a Fleet Admiral means the heir starts as a green cadet"; a dead protagonist triggers succession).</summary>
    internal void AdmiralOutcomes(World w, IRng rng, List<Character> winners, List<Character> losers)
    {
        foreach (var c in winners)
        {
            string skill = c.Skill("tactics") <= c.Skill("gunnery") ? "tactics" : "gunnery";
            c.Skills[skill] = c.Skill(skill) + 1;
            w.Log($"{c.Name} learns from the victory ({skill} {c.Skill(skill)}).");
            if (c.Flags.Contains(FrontFlag))
            {
                int v = w.Counter(ValourKey(c.Id)) + 1; w.WorldCounters[ValourKey(c.Id)] = v;
                if (v >= _rules.AchillesValour && !c.Flags.Contains(AchillesFlag))
                {
                    c.Flags.Add(AchillesFlag); w.Log($"{c.Name} has led from the front through {v} victories; the Reach begins to speak the name the way it speaks of Achilles.");
                    if (c.Id == w.ProtagonistId) w.WorldFlags.Add("became_achilles");
                    if (w.House(c.HouseId) is { } ah) ah.Legitimacy = Math.Min(100, ah.Legitimacy + 2);
                }
            }
        }
        foreach (var c in losers)
        {
            if (c.Flags.Contains(FlagFlag)) { c.Stress = Math.Min(100, c.Stress + 6); continue; }   // the flag is not in the line
            double death = _rules.AdmiralDeathChance * (c.Flags.Contains(FrontFlag) ? _rules.FrontDeathMult : 1.0);
            if (rng.NextDouble() < death)
            {
                w.Log($"{c.Name} goes down with the line.");
                DynastySystem.Die(w, c.Id);
            }
            else
            {
                c.Flags.Add("wounded");
                c.Stress = Math.Min(100, c.Stress + 10);
            }
        }
    }

    /// <summary>Power = Σ hulls × class stat (attack assaulting, defense holding) × (0.5 + readiness/200) × admiral × veterancy;
    /// defenders × (1 + bonus + the system's holdings' defense).</summary>
    public double Power(World w, string houseId, string systemId, bool defending)
    {
        double p = 0;
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == houseId && !f.Moving && f.SystemId == systemId)) p += PowerOf(w, f, defending);
        if (defending) p *= 1 + _rules.DefenderBonus + Defense(w, systemId, houseId);
        p *= 1 + FlagFactor(w, houseId);
        if (CampaignTarget(w, houseId) is { } target && w.Galaxy != null && (target == systemId || w.Galaxy.Hops(target, systemId) is > 0 and var cd && cd <= _rules.CampaignRange)) p *= 1 + _rules.CampaignBonus;
        return p;
    }

    /// <summary>One fleet's power on its own: hulls × class stat × readiness × admiral × veterancy × the depth factor. The house-wide flag and
    /// campaign factors are added by <see cref="Power"/>; expeditions (NationSystem) read this alone, with the flag factor.</summary>
    public double PowerOf(World w, Fleet f, bool defending)
    {
        double hulls = 0;
        foreach (var (cls, n) in Composition(f)) hulls += n * (defending ? cls.Defense : cls.Attack);
        return hulls * (0.5 + f.Readiness / 200.0) * AdmiralFactor(w, f) * (1 + f.Veterancy * _rules.VeterancyBonus) * DepthFactor(w, f);
    }

    /// <summary>The military depth's per-fleet factor (docs/MILITARY.md): stance, torpedoes with isotopes in store, a commander at the front
    /// (and an Achilles), a fleet over its commander's cap, short supply, restive crews.</summary>
    public double DepthFactor(World w, Fleet f)
    {
        double k = f.Stance switch { "press" => _rules.PressPower, "boarding" => _rules.PressPower, "skirmish" => _rules.SkirmishPower, _ => 1.0 };
        if (f.Loadout == "torpedoes" && (w.House(f.OwnerId)?.Treasury.Isotopes ?? 0) > 0) k *= 1 + _rules.TorpedoBonus;
        var c = f.AdmiralId.Length > 0 ? w.Char(f.AdmiralId) : null;
        if (c is { Alive: true })
        {
            if (c.Flags.Contains(FrontFlag)) k *= 1 + _rules.FrontBonus + (c.Flags.Contains(AchillesFlag) ? _rules.AchillesBonus : 0);
            if (f.Strength > CommandCap(c)) k *= _rules.OverCapPenalty;
        }
        if (f.Supply < _rules.SupplyLow) k *= _rules.SupplyLowPower;
        if (f.Loyalty < _rules.LoyaltyRefuse) k *= 0.9;
        return k;
    }

    /// <summary>A commander who leads from the flag lends every fleet of the house a share of their tactics: the house's best such commander counts.</summary>
    public double FlagFactor(World w, string houseId)
    {
        var best = w.Characters.Values.Where(c => c.Alive && c.HouseId == houseId && c.Flags.Contains(FlagFlag)).Select(c => c.Skill("tactics")).DefaultIfEmpty(-1).Max();
        return best < 0 ? 0 : Math.Min(_rules.FlagMax, best * _rules.FlagPerTactics);
    }

    /// <summary>The admiral's skill (GDD §6): tactics 5%, gunnery 3%, leadership 2% per point; Tactician +10%, Strategist +5%;
    /// a wounded or broken admiral −10%. Unled fleets fight at 0.85.</summary>
    public static double AdmiralFactor(World w, Fleet f)
    {
        var c = f.AdmiralId.Length > 0 ? w.Char(f.AdmiralId) : null;
        if (c is not { Alive: true }) return 0.85;
        double k = 1.0 + c.Skill("tactics") * 0.05 + c.Skill("gunnery") * 0.03 + c.Skill("leadership") * 0.02;
        if (c.AptitudeTraits.Contains("Tactician")) k += 0.10;
        if (c.AptitudeTraits.Contains("Strategist")) k += 0.05;
        if (c.Flags.Contains("wounded") || c.Flags.Contains(DynastySystem.BrokenFlag)) k -= 0.10;
        return Math.Clamp(k, 0.6, 2.0);
    }

    public double Defense(World w, string systemId, string houseId)
    {
        double d = 0;
        foreach (var h in w.Holdings.Values.Where(h => h.SystemId == systemId && h.OwnerId == houseId))
        {
            d += _catalog.Spec(h.Specialization)?.Defense ?? 0;
            foreach (var b in h.Buildings) d += _catalog.Building(b)?.Defense ?? 0;
        }
        return d;
    }

    /// <summary>Hulls are lost screen-first: the most numerous (cheapest) class in the largest fleet goes first.</summary>
    private void ApplyLosses(World w, string houseId, string systemId, int losses, int readinessCost)
    {
        var fleets = w.Fleets.Values.Where(f => f.OwnerId == houseId && !f.Moving && f.SystemId == systemId).ToList();
        foreach (var f in fleets) f.Readiness = Math.Max(0, f.Readiness - readinessCost);
        while (losses > 0 && fleets.Any(f => f.Strength > 0))
        {
            var f = fleets.Where(f => f.Strength > 0).OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).First();
            if (f.Ships.Count > 0)
            {
                var cls = f.Ships.Where(k => k.Value > 0).OrderByDescending(k => k.Value).ThenBy(k => Class(k.Key).Attack).ThenBy(k => k.Key, StringComparer.Ordinal).First().Key;
                f.Ships[cls] -= 1;
                if (f.Ships[cls] == 0) f.Ships.Remove(cls);
                f.Recount();
                if (f.Ships.Count == 0) f.Strength = 0;
            }
            else f.Strength -= 1;
            losses -= 1;
        }
        foreach (var f in fleets.Where(f => f.Strength <= 0).ToList())
        {
            w.Fleets.Remove(f.Id); w.Intel.Remove(f.Id);
            w.Log($"{f.Name} is destroyed.");
        }
    }

    private static void Retreat(World w, string houseId, string systemId)
    {
        var survivors = w.Fleets.Values.Where(f => f.OwnerId == houseId && !f.Moving && f.SystemId == systemId).OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
        if (survivors.Count == 0) return;
        var havens = w.Holdings.Values.Where(h => h.OwnerId == houseId && h.SystemId != "" && h.SystemId != systemId)
            .Select(h => h.SystemId).Distinct().Select(s => (s, hops: w.Galaxy!.Hops(systemId, s))).Where(x => x.hops > 0)
            .OrderBy(x => x.hops).ThenBy(x => x.s, StringComparer.Ordinal).ToList();
        foreach (var f in survivors)
        {
            if (havens.Count == 0) { w.Fleets.Remove(f.Id); w.Intel.Remove(f.Id); w.Log($"{f.Name}, with no port to run to, scatters and is lost."); continue; }
            f.DestinationId = havens[0].s; f.Eta = havens[0].hops;
            w.Log($"{f.Name} falls back toward {w.Galaxy!.System(havens[0].s)?.Name ?? havens[0].s}.");
        }
    }

    // ---------------- siege & conquest ----------------

    /// <summary>Turns a blockade needs to take a holding: the base plus two per point of its defense.</summary>
    public int SiegeRequired(World w, Holding h) =>
        _rules.SiegeTurns + (int)Math.Ceiling(2 * ((_catalog.Spec(h.Specialization)?.Defense ?? 0) + h.Buildings.Sum(b => _catalog.Building(b)?.Defense ?? 0)));

    /// <summary>Advance sieges: an enemy fleet alone in the sky over a holding tightens the blockade; when the clock
    /// reaches the holding's resistance it changes hands (GDD §13 Military — "take it"). A contested sky lifts the siege.</summary>
    public void ResolveConquests(World w)
    {
        if (w.Galaxy == null) return;
        var player = w.Protagonist?.HouseId;

        foreach (var h in w.Holdings.Values.Where(h => h.SystemId != "").OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            var besieger = StrengthAt(w, h.OwnerId, h.SystemId) > 0 ? null
                : w.EnemiesOf(h.OwnerId).FirstOrDefault(e => StrengthAt(w, e, h.SystemId) > 0);
            if (besieger == null)
            {
                if (w.Sieges.Remove(h.Id)) w.Log($"The blockade of {h.Name} is lifted.");
                continue;
            }
            if (!w.Sieges.TryGetValue(h.Id, out var siege) || siege.BesiegerId != besieger)
            {
                siege = new Siege { HoldingId = h.Id, BesiegerId = besieger, Turns = 0, Required = SiegeRequired(w, h) };
                w.Sieges[h.Id] = siege;
                w.Log($"{w.House(besieger)?.Name ?? besieger} lays siege to {h.Name} ({siege.Required} turns to fall).");
            }
            siege.Turns += 1;
            if (siege.Turns < siege.Required) continue;

            w.Sieges.Remove(h.Id);
            Conquer(w, h, besieger, player);
        }
        RefreshSiegeFlag(w);
    }

    private void Conquer(World w, Holding h, string conqueror, string? player)
    {
        string old = h.OwnerId;
        h.OwnerId = conqueror; h.FormerOwnerId = old;
        h.Unrest = Math.Clamp(h.Unrest + _rules.ConquestUnrest, 0, 100);
        w.Wars.FirstOrDefault(x => x.Between(old, conqueror))?.AddScore(conqueror, 3);
        if (conqueror == player)
        {
            w.WorldCounters[ConquestsCounter] = w.Counter(ConquestsCounter) + 1;
            w.WorldFlags.Add(ConquestMadeFlag);
            w.WorldCounters["seizures"] = w.Counter("seizures") + 1;
            _friction.Apply(w, w.Protagonist, "conquest");
        }
        if (old == player) { w.WorldFlags.Add(HoldingLostFlag); _friction.Apply(w, w.Protagonist, "holding_lost"); }
        w.Log($"{w.House(conqueror)?.Name ?? conqueror} takes {h.Name} from {w.House(old)?.Name ?? old}.");

        // titles follow the land (GDD §13): a house stripped of its last holding falls to landless, and the
        // conqueror gains a claim to the rung it held — pressable through the Intrigue path.
        var loser = w.House(old);
        if (loser != null && !w.HoldingsOf(old).Any() && loser.Title != "landless")
        {
            string fallen = loser.Title;
            loser.Title = "landless";
            w.House(conqueror)?.Claims.Add(fallen);
            w.Log($"{loser.Name}, landless, loses its {fallen} rank; {w.House(conqueror)?.Name ?? conqueror} holds a claim to it.");
        }
    }
}
