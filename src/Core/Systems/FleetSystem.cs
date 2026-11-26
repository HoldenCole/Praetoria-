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
    /// <summary>What a laid-up fleet costs, of a hundred of its keep (docs/MILITARY.md, "Laid up").</summary>
    public int LaidUpUpkeepPct { get; init; } = 25;
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
    /// <summary>How far drill alone takes a fleet's veterancy: the rest is learnt only in battle. And how many hulls one
    /// manpower a year keeps at drill.</summary>
    public int DrillCap { get; init; } = 3; public int DrillHullsPerManpower { get; init; } = 2;
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
    /// <summary>A house has declared war on the player's: raised for the year it happens and the next, for the report of it.</summary>
    public const string DeclaredOnMeFlag = "war_declared_on_me";
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
    public static string ConqueredKey(string holdingId) => "conquered:" + holdingId;
    public const string HoldingLostFlag = "holding_lost";
    public const string UnderSiegeFlag = "under_siege";          // one of the player's holdings is blockaded
    public const string WarsDeclaredCounter = "wars_declared";
    /// <summary>Wars the player's own house began. <see cref="WarsDeclaredCounter"/> counts every war in the Reach.</summary>
    public const string WarsBegunCounter = "wars_begun";
    public static string AtWarFlag(string houseId) => $"at_war:{houseId}";
    public const string SuingForPeaceFlag = "suing_for_peace";
    public const string AdmiralFlag = "admiral";                  // the character commands a fleet

    private readonly FleetRules _rules;
    private readonly HoldingCatalog _catalog;
    /// <summary>The buildings, works and provinces this fleet system reckons with.</summary>
    public HoldingCatalog Catalog => _catalog;
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
    /// <summary>The game this is the navy of. Set when the game is taken up, so that what the laws do to a fleet's keep shows in the
    /// first year's forecast and not only after a year has turned (docs/LAWS_REVIEW.md).</summary>
    public void Bind(World w) => _world = w;
    public Resources Upkeep(Fleet f)
    {
        var r = new Resources();
        foreach (var (cls, n) in Composition(f)) foreach (var k in Resource.All) r.Add(k, cls.Upkeep.Get(k) * n);
        int pct = _world != null ? PolicySystem.Modifier(_world, Policies, f.OwnerId, "fleet_upkeep_pct") + PolicySystem.ReachModifierFor(_world, Policies, "fleet_upkeep_pct", f.OwnerId) : 0; if (pct != 0 && r.Credits > 0) { int raw = r.Credits * Math.Max(0, 100 + pct); r.Credits = raw / 100; r.Coin = raw % 100; }   // what is under a hundred is billed in single credits (docs/MONEY.md)
        // laid up: the caretakers' part of it, never rounded away to nothing
        if (f.LaidUp) { foreach (var k in Resource.All) { int v = r.Get(k); if (v > 0) r.Set(k, (v * _rules.LaidUpUpkeepPct + 99) / 100); } r.Coin = 0; }
        return r;
    }

    // ---------------- the turn ----------------

    public void Tick(World w)
    {
        _world = w;
        LastBattles.Clear();
        foreach (var f in DepthTurnFlags) w.WorldFlags.Remove(f);
        if (w.HasFlag(ConquestMadeFlag) && w.Counter("mark:" + ConquestMadeFlag) < w.Turn - 1) { w.WorldFlags.Remove(ConquestMadeFlag); w.WorldCounters.Remove("mark:" + ConquestMadeFlag); }   // a conquest is a report for the year it is made and the next, not for ever
        if (w.HasFlag(DeclaredOnMeFlag) && w.Counter("mark:" + DeclaredOnMeFlag) < w.Turn - 1) { w.WorldFlags.Remove(DeclaredOnMeFlag); w.WorldCounters.Remove("mark:" + DeclaredOnMeFlag); }
        if (w.Galaxy == null) { UpdateIntel(w); return; }
        Supply(w);
        Campaigns(w);

        foreach (var f in w.Fleets.Values.OrderBy(f => f.Id, StringComparer.Ordinal))
        {
            if (f.IsAway) continue;   // off the map (docs/FOREIGN_NATIONS.md)
            if (f.Moving)
            {
                f.Eta -= 1;
                f.Readiness = Math.Max(0, f.Readiness - Math.Max(0, _rules.ReadinessDecayMoving - CaptainHolds(w, f)));   // a captain keeps the crews to their work under way
                if (f.Eta <= 0) { f.SystemId = f.DestinationId; f.DestinationId = ""; f.Eta = 0; f.Voyage = 0; Arrive(w, f); }
            }
            else if (f.LaidUp) f.Readiness = 0;   // no crews: it gains nothing while it lies there
            else f.Readiness = Math.Clamp(f.Readiness + _rules.ReadinessRecovery + CaptainDrills(w, f) + PolicySystem.Modifier(w, Policies, f.OwnerId, "readiness") + PolicySystem.ReachModifierFor(w, Policies, "readiness", f.OwnerId), 0, 100);   // the Reach's law, for the fleets of the Reach: not the raiders', the nations' or a house's that has broken away (docs/SENATE.md, docs/BREAKAWAY.md)
        }

        Drill(w);
        var chairId = RegionSystem.CapitalHolder(w)?.Id;
        foreach (var house in w.Houses.Values.OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            var bill = new Resources();
            int keptAtWar = 0, odd = 0;   // what the fleets in commission cost: a war's account says what the year cost (docs/MILITARY.md, "Commanding a navy")
            foreach (var f in w.Fleets.Values.Where(f => BilledTo(f, chairId) == house.Id)) { var keep = Upkeep(f); bill.Add(keep); odd += keep.Coin; if (!f.LaidUp) keptAtWar += keep.Credits; }
            if (odd != 0) house.Treasury.AddCoin(-odd);
            if (bill.IsEmpty()) continue;
            int tanks = house.Treasury.Helium3;
            foreach (var r in Resource.All) house.Treasury.Add(r, -bill.Get(r));
            house.Treasury.ClampNonCredit();
            Desert(w, house);
            Refuel(w, house);
            WarLedger.Cost(w, house.Id, keptAtWar, tanks - house.Treasury.Helium3);
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
            int range = h.Buildings.Contains("supply_depot") || (SystemWorks.Any(w) && SystemWorks.Depot(w, _catalog, h.SystemId)) ? _rules.DepotRange : _rules.SupplyRange;
            if (h.SystemId == systemId || g.Hops(h.SystemId, systemId) is > 0 and var d && d <= range) return true;
        }
        var target = CampaignTarget(w, houseId);
        if (target != null && (target == systemId || g.Hops(target, systemId) is > 0 and var cd && cd <= _rules.CampaignRange)) return true;
        return false;
    }
    /// <summary>What a fleet's supply will do at a system, in the figures of the rules: fed there or not, how fast it falls, the year it
    /// begins to tell and the year hulls begin to desert (docs/PLAYTHROUGH.md, War and peace: "supply: how a fleet far away is fed").</summary>
    public string SupplySays(World w, Fleet f, string systemId)
    {
        string where = w.Galaxy?.System(systemId)?.Name ?? systemId;
        if (InSupply(w, f.OwnerId, systemId))
            return $"{where} is in supply: a world of yours is within {Counted.Lanes(_rules.SupplyRange)}, or a supply depot within {_rules.DepotRange}, or your campaign's target within {_rules.CampaignRange}. Supply recovers {_rules.SupplyRecover} a year there, to 100.";
        bool near = NearHome(w, f.OwnerId, systemId); int drain = near ? _rules.SupplyDrainNear : _rules.SupplyDrain;
        int toLow = f.Supply < _rules.SupplyLow ? 0 : (f.Supply - _rules.SupplyLow) / drain + 1, toNone = (f.Supply + drain - 1) / drain;
        return $"{where} is out of supply ({(near ? "within two lanes of a world of yours, so the drain is the gentler one" : "more than two lanes from any world of yours")}): supply falls {drain} a year from {f.Supply}. "
            + (toLow == 0 ? $"It is already under {_rules.SupplyLow}: the fleet fights at {_rules.SupplyLowPower * 100:0}% and loses 5 readiness a year. " : $"In {Counted.Years(toLow)} it is under {_rules.SupplyLow}, and the fleet fights at {_rules.SupplyLowPower * 100:0}% and loses 5 readiness a year. ")
            + $"In {Counted.Years(toNone)} it is at nought, and from the year after a hull deserts each year. A supply depot on a world of yours within {_rules.DepotRange} lanes, or a campaign declared on a system within {_rules.CampaignRange}, would feed it.";
    }

    /// <summary>Within two lanes of a world of the house: out of supply, but near enough that the drain is gentler.</summary>
    private static bool NearHome(World w, string houseId, string systemId) =>
        w.Galaxy != null && w.HoldingsOf(houseId).Any(h => h.IsPlanet && h.SystemId.Length > 0 && (h.SystemId == systemId || w.Galaxy.Hops(h.SystemId, systemId) is > 0 and <= 2));

    private void Supply(World w)
    {
        var mine = w.Protagonist?.HouseId;
        foreach (var f in w.Fleets.Values.OrderBy(f => f.Id, StringComparer.Ordinal).ToList())
        {
            if (f.OwnerId == PirateSystem.HouseId) { f.Supply = 100; continue; }   // raiders live off the lanes; a nation's host does not, and a season in the Reach is as long as it can stay (docs/BEYOND.md)
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
        foreach (var key in w.WorldCounters.Keys.Where(k => k.StartsWith("campaign_until:")).OrderBy(k => k, StringComparer.Ordinal).ToList())   // by name: a dictionary that has had removals does not enumerate as a reloaded one does
        {
            string houseId = key["campaign_until:".Length..];
            // a campaign ends with its term, or with the war it was for: a house at war with nobody is not on campaign (docs/PLAYTHROUGH.md, fleets)
            bool atPeace = !w.EnemiesOf(houseId).Any();   // (a nation the Reach fights on the map is among a house's enemies)
            if (w.WorldCounters[key] > w.Turn && !atPeace) continue;
            foreach (var f in w.WorldFlags.Where(f => f.StartsWith(CampaignPrefix(houseId))).ToList()) w.WorldFlags.Remove(f);
            w.WorldCounters.Remove(key);
            if (houseId == mine) w.WorldFlags.Add("campaign_over");
            w.Log(atPeace ? $"The war is over, and with it the campaign of {w.House(houseId)?.Name ?? houseId}; its fleets are recalled to their stations." : $"The campaign of {w.House(houseId)?.Name ?? houseId} is over; its fleets are recalled to their stations.");
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
            if (World.Outsider(f.OwnerId)) continue;
            var house = w.House(f.OwnerId); if (house == null) continue;
            int d = _rules.LoyaltyDrift;
            if (house.Treasury.Credits < 0) d -= _rules.LoyaltyUnpaid;
            if (w.Counter(FuelShortKey(f.OwnerId)) > 0) d -= 2;
            if (f.Supply < _rules.SupplyLow) d -= 2;
            int officers = PolicySystem.Approval(w, f.OwnerId, "officers");
            if (officers <= PolicySystem.Hostile) d -= 2; else if (officers >= PolicySystem.Loyal) d += 1;
            if (f.AdmiralId.Length > 0 && w.Char(f.AdmiralId) is { Alive: true } adm && adm.Flags.Contains(FrontFlag)) d += 1;   // crews love a commander who shares the line
            if (f.AdmiralId.Length > 0 && w.Char(f.AdmiralId) is { Alive: true } known && AcademySystem.Primus(known)) d += 1;   // and have heard of one who won the Institute
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
    public int CommandCap(Character? c) => c == null ? int.MaxValue : _rules.CommandCapBase + (c.CareerTrack == "military" ? c.CareerRank : 0) * _rules.CommandCapPerRank + (AcademySystem.Primus(c) ? _rules.CommandCapPerRank : 0);

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
    /// <summary>How the fuel stands against the years ahead (docs/MILITARY.md, "A forecast of fuel"): what is in the tanks, what the
    /// fleets burn a year, and what the house's accounts leave of helium-3 a year once that is burned (the year's net). <c>Years</c> is
    /// how many whole years the tanks last at that rate; -1 when they do not run down at all.</summary>
    public sealed record FuelOutlook(int InTanks, int Burned, int BroughtUp, int Years, string Says);
    public static FuelOutlook Outlook(int inTanks, int burnedAYear, int netAYear)
    {
        int brought = Math.Max(0, burnedAYear + netAYear);
        if (burnedAYear <= 0) return new FuelOutlook(inTanks, 0, brought, -1, brought > 0 ? $"{inTanks} in the tanks · nothing is burning it, and {brought} comes up a year" : $"{inTanks} in the tanks · nothing is burning it");
        if (netAYear >= 0) return new FuelOutlook(inTanks, burnedAYear, brought, -1, $"{inTanks} in the tanks · {brought} comes up a year and {burnedAYear} is burned: the tanks " + (netAYear > 0 ? $"fill by {netAYear} a year" : "hold level"));
        int years = inTanks / -netAYear;
        string end = years == 0 ? "dry within the year" : years == 1 ? "dry in a year" : $"dry in {years} years";
        return new FuelOutlook(inTanks, burnedAYear, brought, years, $"{inTanks} in the tanks · {(brought > 0 ? brought + " comes up a year" : "none comes up")} and {burnedAYear} is burned: {-netAYear} short each year, {end}");
    }

    public static int FuelBillFor(World w, string houseId, int hullsPerUnit)
    {
        if (World.Outsider(houseId)) return 0;
        int hulls = w.Fleets.Values.Where(f => f.OwnerId == houseId && !f.Office && !f.LaidUp).Sum(f => f.Strength);   // the state's bunkers fuel the office fleets, and a hull laid up burns nothing
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
        else w.Log($"Unpaid crews desert {victim.Name}: {house.Name} owes {Money.Held(house.Treasury).TrimStart('-')} credits it does not have.");
    }

    private static void Arrive(World w, Fleet f)
    {
        f.ArrivedTurn = w.Turn;
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
            if (owner == w.Protagonist?.HouseId) w.Tell(w.AtWar(owner, f.OwnerId) ? "warn" : "info", $"{f.Name} of {w.House(f.OwnerId)?.Name ?? f.OwnerId} ({Counted.Hulls(f.Strength)}) has come into your space at {sys?.Name ?? f.SystemId}" + (w.AtWar(owner, f.OwnerId) ? ": you are at war with it" : ". You are not at war with it"), "galaxy:" + f.SystemId, f.OwnerId);
        }
        else w.Log($"{f.Name} arrives at {sys?.Name ?? f.SystemId}.");
    }

    /// <summary>A house asked to take its fleet out of another's space is not sent back to stand over it for this many years.</summary>
    public const int AskedOffYears = 5;
    /// <summary>The head of a fleet's house obliges a request to leave when they think at least this well of the one who asks.</summary>
    public const int ObligesAt = -20;
    public static string AskedOffKey(string fleetHouse, string askerHouse) => $"asked_off:{fleetHouse}:{askerHouse}";

    /// <summary>What a fleet of another house is doing where it lies, as the house whose space it is in would be told
    /// (docs/PLAYTHROUGH.md, War and peace: "neutral fleets over your world: what they are doing").</summary>
    public static string Doing(World w, Fleet f, string viewer)
    {
        string owner = w.House(f.OwnerId)?.Name ?? f.OwnerId; int years = Math.Max(0, w.Turn - f.ArrivedTurn);
        string lain = f.Moving ? "" : years <= 0 ? " It came this year." : $" It has lain here {Counted.Years(years)}.";
        if (f.OwnerId == PirateSystem.HouseId) return "Raiders: they take what passes and the yield of the world they sit over." + lain;
        if (w.AtWar(viewer, f.OwnerId))
        {
            var siege = w.Sieges.Values.FirstOrDefault(s => s.BesiegerId == f.OwnerId && w.Holding(s.HoldingId)?.SystemId == f.SystemId);
            string company = f.HiredFrom.Length > 0 ? $" It is a company hired from {w.House(f.HiredFrom)?.Name ?? f.HiredFrom}, in {owner}'s pay." : "";
            return (siege != null ? $"At war with you, and besieging {w.Holding(siege.HoldingId)?.Name}: {siege.Turns} of {siege.Required} years spent." : "At war with you.") + company + lain;
        }
        if (f.Office) return $"The Reach's own squadron, under {owner}'s colours while it holds the office." + lain;
        if (f.HiredFrom.Length > 0) return $"A company hired from {w.House(f.HiredFrom)?.Name ?? f.HiredFrom}, in {owner}'s pay." + lain;
        if (f.LevyOf.Length > 0) return $"A levy of {w.House(f.LevyOf)?.Name ?? f.LevyOf}, called out by {owner}." + lain;
        string why = f.Errand.Length > 0 ? $"It came {f.Errand}." : $"{owner} has given no reason for it.";
        return $"Not at war with you. {why}{lain} It does not besiege or blockade: only a house at war with you can.";
    }

    public static string? TerritoryOwner(World w, string systemId)
    {
        if (w.PlanetHolder(systemId) is { Length: > 0 } holder) return holder;   /* a system is the space of whoever holds its world; a claim or a station there does not outvote the world */
        var owners = w.Holdings.Values.Where(h => h.SystemId == systemId)
            .GroupBy(h => h.OwnerId).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key).ToList();
        return owners.Count == 0 ? null : owners[0];
    }

    /// <summary>The hulls a house has in commission: what it is counted as having wherever strength is weighed. Hulls laid up are not in it.</summary>
    public static int StrengthOf(World w, string houseId) => w.Fleets.Values.Where(f => f.OwnerId == houseId && !f.LaidUp).Sum(f => f.Strength);
    /// <summary>Why a laid-up fleet may not be given an order that needs crews, or null.</summary>
    public static string? LaidUpRefusal(Fleet f) => f.LaidUp ? "it is laid up, with no crews aboard: put it back in commission first" : null;
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
        // captains: a dead one leaves the post, one made an admiral has left it, and nobody is captain of two fleets or of another house's
        var flags = w.Fleets.Values.Select(f => f.AdmiralId).Where(a => a.Length > 0).ToHashSet(); var captains = new HashSet<string>();
        foreach (var f in w.Fleets.Values.Where(f => f.CaptainId.Length > 0).OrderBy(f => f.Id, StringComparer.Ordinal))
        {
            var c = w.Char(f.CaptainId);
            if (c is not { Alive: true } || flags.Contains(c.Id) || (c.HouseId != f.OwnerId && c.HouseId != f.HiredFrom && !ServiceSystem.CommandedBy(w, f, c.HouseId)) || !captains.Add(c.Id)) f.CaptainId = "";
        }
    }

    // ---------------- captains (docs/MILITARY.md, "Commanding a navy") ----------------

    /// <summary>The lowest rank of the naval service that may be a fleet's captain: Lieutenant (Cadet 0, Ensign 1, Lieutenant 2).</summary>
    public const int CaptainRank = 2;
    /// <summary>A captain adds a point of readiness a year at anchor for each point of discipline, up to this; and keeps back a point
    /// of what a year under way wears off for every two points, up to <see cref="CaptainHoldsMax"/>.</summary>
    public const int CaptainDrillsMax = 5, CaptainHoldsMax = 3;
    /// <summary>What a captain's gunnery adds to how the fleet fights under its admiral, a point, and at most.</summary>
    public const double CaptainGunnery = 0.01, CaptainGunneryMax = 0.06;

    public static Character? AdmiralOf(World w, Fleet f) => f.AdmiralId.Length > 0 && w.Char(f.AdmiralId) is { Alive: true } c ? c : null;
    public static Character? CaptainOf(World w, Fleet f) => f.CaptainId.Length > 0 && w.Char(f.CaptainId) is { Alive: true } c ? c : null;
    public static int CaptainDrills(World w, Fleet f) => CaptainOf(w, f) is { } c ? Math.Clamp(c.Skill("discipline"), 0, CaptainDrillsMax) : 0;
    public static int CaptainHolds(World w, Fleet f) => CaptainOf(w, f) is { } c ? Math.Clamp(c.Skill("discipline") / 2, 0, CaptainHoldsMax) : 0;

    /// <summary>Who commands the fleet in a fight: its admiral; its captain when it has no admiral; and when the admiral is wounded,
    /// whichever of the two would do the better by it. Null when it has neither.</summary>
    public static Character? Commander(World w, Fleet f)
    {
        var adm = AdmiralOf(w, f); var cap = CaptainOf(w, f);
        if (adm == null) return cap;
        if (cap == null || !adm.Flags.Contains(LifeSystem.WoundedChar)) return adm;
        return CommandFactor(cap) > CommandFactor(adm) ? cap : adm;
    }

    /// <summary>What a captain's gunnery adds while the admiral commands: nothing when the captain is the one commanding, since their
    /// gunnery is then counted as the commander's.</summary>
    public static double CaptainFactor(World w, Fleet f) =>
        CaptainOf(w, f) is { } c && Commander(w, f) != c ? 1 + Math.Min(CaptainGunneryMax, Math.Max(0, c.Skill("gunnery")) * CaptainGunnery) : 1.0;

    /// <summary>Why this person may not be the fleet's captain, or null: a living, grown member of the fleet's house, in the naval
    /// service at the rank of Lieutenant or above, who is admiral of nothing. The same rule for every house.</summary>
    public static string? CaptainRefusal(World w, Fleet f, Character? c)
    {
        if (c is not { Alive: true }) return "no one to appoint";
        if (f.LevyOf.Length > 0) return "a levy keeps the officers its own house gave it";
        if (c.HouseId != f.OwnerId && !ServiceSystem.CommandedBy(w, f, c.HouseId)) return $"{c.Name} is not of the house";   // a legate may bring a captain of their own house
        if (f.CaptainId == c.Id) return $"{c.Name} is its captain already";
        if (c.Age < DynastySystem.AgeOfMajority) return $"{c.Name} is {c.Age}: a captain is a grown officer, {DynastySystem.AgeOfMajority} or older";
        if (c.CareerTrack != "military") return $"{c.Name} is not in the naval service: a captain is an officer of the Navy, a Lieutenant or above";
        if (c.CareerRank < CaptainRank) return $"{c.Name} is not yet a Lieutenant: a captain is an officer of the Navy, a Lieutenant or above";
        if (c.Flags.Contains(LifeSystem.FrailChar)) return $"{c.Name} is too frail for the sea";
        if (w.Fleets.Values.Where(x => x.AdmiralId == c.Id).OrderBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault() is { } flag)
            return flag.Id == f.Id ? $"{c.Name} is its admiral: nobody is admiral and captain at once" : $"{c.Name} is admiral of {flag.Name}: nobody is admiral and captain at once";
        if (TriumphSystem.HomeRefusal(w, c) is { } home) return home;   // home for a triumph (docs/CAESAR.md)
        if (CaesarSystem.Barred(w, c)) return "the Senate had them lay a command down";
        return null;
    }

    /// <summary>Where a fleet stands as a whole, in one line (docs/PLAYTHROUGH.md, Hard: "together they are a system"): in commission
    /// or laid up, in supply or not and why, who commands and who is captain, and its hulls against the commander's cap.</summary>
    public string Stands(World w, Fleet f)
    {
        string Sys(string id) => w.Galaxy?.System(id)?.Name ?? id;
        var parts = new List<string>();
        parts.Add(f.LaidUp ? $"Laid up at {Sys(f.SystemId)}: the hulls are kept and the crews paid off, at {_rules.LaidUpUpkeepPct} in a hundred of the keep."
            : f.IsAway ? "In commission, beyond the Reach."
            : f.Moving ? $"In commission, under way to {Sys(f.DestinationId)} ({Counted.Years(Math.Max(1, f.Eta))})."
            : $"In commission at {Sys(f.SystemId)}.");
        if (f.IsAway) parts.Add($"Its supply ({f.Supply}) is the expedition's own, and falls each season abroad.");
        else if (w.Galaxy != null && f.OwnerId != PirateSystem.HouseId)
        {
            string at = f.Moving ? f.DestinationId : f.SystemId; string? why = SupplyWhy(w, f.OwnerId, at); string where = f.Moving ? " where it is bound" : "";
            parts.Add(why != null ? $"In supply{where} ({f.Supply}): {why}."
                : $"Out of supply{where} ({f.Supply}, falling {(NearHome(w, f.OwnerId, at) ? _rules.SupplyDrainNear : _rules.SupplyDrain)} a year): no world of the house is within {Counted.Lanes(_rules.SupplyRange)}, no supply depot within {_rules.DepotRange}, and no campaign's object within {_rules.CampaignRange}.");
        }
        var adm = AdmiralOf(w, f); var cap = CaptainOf(w, f); var cmd = Commander(w, f);
        parts.Add(adm == null && cap == null ? "It has no admiral and no captain: it fights at 85 in a hundred."
            : adm == null ? $"It has no admiral: its captain, {cap!.Name}, commands."
            : cmd != adm ? $"{adm.Name}, its admiral, is wounded: the captain, {cap!.Name}, commands."
            : $"{adm.Name} commands as admiral{(adm.Flags.Contains(LifeSystem.WoundedChar) ? ", wounded" : "")}{(cap != null ? $", with {cap.Name} as captain" : "; it has no captain")}.");
        if (cmd != null)
        {
            int limit = CommandCap(cmd);
            parts.Add(f.Strength <= limit ? $"{Counted.Hulls(f.Strength)} against a cap of {limit}."
                : $"{Counted.Hulls(f.Strength)} against a cap of {limit}: {f.Strength - limit} more than {cmd.Name} leads without strain, and it fights at {_rules.OverCapPenalty * 100:0} in a hundred.");
        }
        else parts.Add($"{Counted.Hulls(f.Strength)}, and nobody whose cap they would strain.");
        return string.Join(" ", parts);
    }

    /// <summary>Why a system is in a house's supply, or null when it is not: the nearest of the reasons <see cref="InSupply"/> counts.</summary>
    public string? SupplyWhy(World w, string houseId, string systemId)
    {
        var g = w.Galaxy; if (g == null) return null;
        (int d, string says)? best = null;
        foreach (var h in w.HoldingsOf(houseId).Where(h => h.IsPlanet && h.SystemId.Length > 0).OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            bool depot = h.Buildings.Contains("supply_depot") || (SystemWorks.Any(w) && SystemWorks.Depot(w, _catalog, h.SystemId));
            int d = h.SystemId == systemId ? 0 : g.Hops(h.SystemId, systemId);
            if (d < 0 || (d == 0 && h.SystemId != systemId) || d > (depot ? _rules.DepotRange : _rules.SupplyRange)) continue;
            if (best != null && best.Value.d <= d) continue;
            best = (d, d == 0 ? $"it lies at {h.Name}, a world of the house" : depot ? $"the supply depot at {h.Name} is {Counted.Lanes(d)} off, and a depot reaches {_rules.DepotRange}" : $"{h.Name}, a world of the house, is {Counted.Lanes(d)} off");
        }
        if (best != null) return best.Value.says;
        var target = CampaignTarget(w, houseId);
        if (target != null && (target == systemId || g.Hops(target, systemId) is > 0 and var cd && cd <= _rules.CampaignRange))
            return $"the campaign's object, {g.System(target)?.Name ?? target}, is {(target == systemId ? "here" : Counted.Lanes(g.Hops(target, systemId)) + " off")}, and a campaign feeds the fleets within {_rules.CampaignRange}";
        return null;
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
            bool seen = watched.Contains(f.SystemId) || (f.Moving && watched.Contains(f.DestinationId)) || World.Outsider(f.OwnerId);   // raiders are talked about on every lane
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
        w.Wars.Add(new War { HouseA = a, HouseB = b, Since = w.Turn, ClosedThrough = w.Turn });
        w.WorldCounters[WarsDeclaredCounter] = w.Counter(WarsDeclaredCounter) + 1;
        if (a == w.Protagonist?.HouseId) w.WorldCounters[WarsBegunCounter] = w.Counter(WarsBegunCounter) + 1;
        RefreshWarFlags(w);
        var who = byCharacterId != null ? w.Char(byCharacterId) : null;
        w.Log($"{(who?.Name ?? w.House(a)?.Name ?? a)} declares war on {w.House(b)?.Name ?? b}.");
        if (b == w.Protagonist?.HouseId)
        {
            var theirHead = w.House(a) is { } ah ? Economy.HeadOf(w, ah) : null; int regard = theirHead != null ? w.GetRelationship(theirHead.Id, w.ProtagonistId)?.Disposition ?? 0 : 0;
            string why = FeudSystem.Between(w, a, b) ? "there is blood between the houses" : regard <= -20 ? $"{theirHead?.Name} thinks ill of you ({regard})" : StrengthOf(w, a) > StrengthOf(w, b) * 2 ? "it has the hulls and you have not" : "it wants what you hold";
            w.Tell("warn", $"{w.House(a)?.Name ?? a} has declared war on your house: {Counted.Hulls(StrengthOf(w, a))} to your {StrengthOf(w, b)}. Why: {why}", "military", a);
            w.Wars[^1].Why = why; w.WorldFlags.Add(DeclaredOnMeFlag); w.WorldCounters["mark:" + DeclaredOnMeFlag] = w.Turn;   // and a report comes of it while it is news
        }
        if (who != null) _friction.Apply(w, who, "declare_war");
    }

    public const int CessionLead = 10;   // the side this far behind cedes a world at the peace

    public void MakePeace(World w, string a, string b) => MakePeace(w, a, b, terms: true);

    /// <summary>End a war. With <paramref name="terms"/>, the house behind by <see cref="CessionLead"/> or more cedes its least peopled
    /// world (never the capital) to the winner: wars have prizes (GAP §7).</summary>
    public void MakePeace(World w, string a, string b, bool terms)
    {
        if (ThroneSystem.ForTheThrone(w, a, b)) return;   // a war for a throne is not ended by treaty: one of them sits on it at the end (docs/THRONE.md)
        var war = w.Wars.FirstOrDefault(x => x.Between(a, b));
        if (war == null) return;
        // blockades still standing end with the war, and what they cost is billed with the rest
        foreach (var s in w.Sieges.Values.Where(s => war.Involves(s.BesiegerId) && war.Involves(w.Holding(s.HoldingId)?.OwnerId ?? "")).OrderBy(s => s.HoldingId, StringComparer.Ordinal).ToList()) VentureSystem.SiegeLifted(w, s);
        w.Wars.Remove(war); WarLedger.Archive(w, war);   // kept, with its account by year (docs/MILITARY.md, "Commanding a navy")
        VentureSystem.SettleClaims(w, war, CessionLead);   // the bills for what was sacked are presented (docs/ECONOMY_MARKET.md, section 14)
        if (terms)
        {
            string? loser = war.Lead(a) >= CessionLead ? b : war.Lead(b) >= CessionLead ? a : null;
            if (loser != null)
            {
                string winner = loser == a ? b : a;
                var ceded = w.HoldingsOf(loser).Where(h => h.IsPlanet && !(w.Galaxy is { Capital.Length: > 0 } g && g.Capital == h.SystemId)).OrderBy(h => h.Population).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
                if (ceded != null && w.HoldingsOf(loser).Count(h => h.IsPlanet) > 1) CedeWorld(w, ceded, loser, winner);
            }
        }
        foreach (var s in w.Sieges.Values.Where(s => (war.Involves(s.BesiegerId)) && war.Involves(w.Holding(s.HoldingId)?.OwnerId ?? "")).ToList()) w.Sieges.Remove(s.HoldingId);
        foreach (var c in w.Characters.Values.Where(c => war.Involves(c.HouseId))) c.Flags.Remove(SuingForPeaceFlag);
        RefreshWarFlags(w);
        RefreshSiegeFlag(w);
        w.Log($"{w.House(a)?.Name ?? a} and {w.House(b)?.Name ?? b} make peace.");
        WarLedger.Finish(w, war, "peace was made");
    }

    /// <summary>A world changes hands at a peace: its new house's head governs it, and its people take it badly for a while.</summary>
    public void CedeWorld(World w, Holding ceded, string loser, string winner)
    {
        ceded.FormerOwnerId = loser; ceded.OwnerId = winner; ceded.Unrest = Math.Min(100, ceded.Unrest + 10);
        var newGov = Economy.HeadOf(w, w.House(winner)!); if (newGov != null) ceded.GovernorId = newGov.Id;
        w.WorldCounters["cessions"] = w.Counter("cessions") + 1;
        var player = w.Protagonist?.HouseId; if (winner == player) w.WorldFlags.Add("won_a_world"); if (loser == player) w.WorldFlags.Add("ceded_a_world");
        WarLedger.Changed(w, ceded, loser, winner, "ceded");
        w.Log($"By the terms of the peace, {w.House(loser)?.Name ?? loser} cedes {ceded.Name} to {w.House(winner)?.Name ?? winner}.");
        // a world that changes hands is news to the house that gains or loses it
        if (winner == player) w.Tell("good", $"By the terms of the peace {w.House(loser)?.Name ?? loser} cedes {ceded.Name} to you. You govern it yourself until you name another, and its people take the change badly for a while.", "galaxy:" + ceded.SystemId, ceded.Id);
        else if (loser == player) w.Tell("warn", $"By the terms of the peace {ceded.Name} is ceded to {w.House(winner)?.Name ?? winner}. It is yours no longer.", WarLedger.Target, ceded.Id);
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
        if ((w.Wars.Count == 0 && !w.Nations.Any(n => n.AtWar && n.OnMap)) || w.Galaxy == null) return;   // a war with a nation on the map is fought on it (docs/BEYOND.md)
        var player = w.Protagonist?.HouseId; var met = new HashSet<string>();

        foreach (var systemId in w.Fleets.Values.Where(f => !f.Moving && !f.IsAway).Select(f => f.SystemId).Distinct().OrderBy(s => s, StringComparer.Ordinal).ToList())
        {
            var houses = w.Fleets.Values.Where(f => !f.Moving && f.SystemId == systemId).Select(f => f.OwnerId).Distinct().OrderBy(h => h, StringComparer.Ordinal).ToList();
            for (int i = 0; i < houses.Count; i++)
                for (int j = i + 1; j < houses.Count; j++)
                {
                    string a = houses[i], b = houses[j];
                    if (!w.AtWar(a, b) || StrengthAt(w, a, systemId) == 0 || StrengthAt(w, b, systemId) == 0) continue;
                    // against a power from outside the houses of the Reach stand together (docs/BEYOND.md): one battle a year in a system, led
                    // by the strongest house there, with the others' guns behind it
                    if (w.Foreign(a) || w.Foreign(b))
                    {
                        string them = w.Foreign(a) ? a : b;
                        if (!met.Add(systemId + "|" + them)) continue;
                        string lead = houses.Where(h => h != them && w.AtWar(h, them) && StrengthAt(w, h, systemId) > 0).OrderByDescending(h => StrengthAt(w, h, systemId)).ThenBy(h => h, StringComparer.Ordinal).First();
                        if (string.CompareOrdinal(lead, them) < 0) { a = lead; b = them; } else { a = them; b = lead; }
                    }
                    var report = Battle(w, rng, systemId, a, b);
                    LastBattles.Add(report);
                    WealthSystem.StandardWon(w, report.Winner, report.Winner == a ? b : a, systemId, Admirals(w, report.Winner, systemId));   // sometimes a standard comes home (docs/WEALTH.md)
                    w.WorldCounters[BattlesCounter] = w.Counter(BattlesCounter) + 1;
                    if (w.House(report.Winner) is { } victor && !World.Outsider(victor.Id)) victor.Glory = Math.Min(12, victor.Glory + 1);   // a victory is talked about (docs/REPUBLIC.md stage 3)
                    var scored = w.Wars.FirstOrDefault(x => x.Between(a, b)); if (scored != null) { bool attWon = report.Winner == report.Attacker; WarLedger.Battle(w, scored, report.Winner, attWon ? report.AttackerLosses : report.DefenderLosses, attWon ? report.DefenderLosses : report.AttackerLosses); scored.AddScore(report.Winner, 2); scored.LastBattleTurn = w.Turn; }   // raiders have no war to score
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
        /* the one whose space it is holds; where it is neither's, the one who was there first holds and the one who came is the attacker */
        int Came(string h) => w.Fleets.Values.Where(f => f.OwnerId == h && !f.Moving && f.SystemId == systemId).Select(f => f.ArrivedTurn).DefaultIfEmpty(0).Max();
        bool aHolds = owner == a || (owner != b && Came(a) < Came(b));
        string attacker = aHolds ? b : a, defender = aHolds ? a : b;
        double pa = Power(w, attacker, systemId, defending: false);
        double pd = Power(w, defender, systemId, defending: owner == defender);
        // the other houses of the Reach in the system lend their guns against a power from outside; the losses are the leader's
        double Behind(string side, string foe) => !w.Foreign(foe) ? 0 : w.Fleets.Values.Where(f => !f.Moving && !f.IsAway && f.SystemId == systemId && f.OwnerId != side && w.AtWar(f.OwnerId, foe)).Select(f => f.OwnerId).Distinct().Sum(h => Power(w, h, systemId, defending: false));
        double la = Behind(attacker, defender), ld = Behind(defender, attacker); pa += la; pd += ld;
        if (la + ld > 0) w.Log($"At {w.Galaxy?.System(systemId)?.Name ?? systemId} the fleets of the Reach stand together behind {w.House(la > 0 ? attacker : defender)?.Name}.");
        string sa = StanceAt(w, attacker, systemId), sd = StanceAt(w, defender, systemId); int edge = StanceEdgeOf(sa, sd);
        if (edge > 0) pa *= 1 + _rules.StanceEdge; else if (edge < 0) pd *= 1 + _rules.StanceEdge;
        foreach (var f in w.Fleets.Values.Where(f => !f.Moving && f.SystemId == systemId && (f.OwnerId == a || f.OwnerId == b) && f.Loadout == "torpedoes"))
            if (w.House(f.OwnerId) is { } th && th.Treasury.Isotopes > 0) th.Treasury.Isotopes -= 1;   // the magazines are spent whatever the outcome
        int hullsA = StrengthAt(w, attacker, systemId), hullsD = StrengthAt(w, defender, systemId);
        var notes = new List<string>();
        if (owner == defender) notes.Add($"{w.House(defender)?.Name ?? defender} fought on its own ground: +{(int)Math.Round(100 * (_rules.DefenderBonus + Defense(w, systemId, defender)))}% for the ground and its works");
        if (edge != 0) notes.Add($"{w.House(edge > 0 ? attacker : defender)?.Name} chose the better order of battle ({(edge > 0 ? sa : sd)} against {(edge > 0 ? sd : sa)}): +{(int)Math.Round(100 * _rules.StanceEdge)}%");
        foreach (var side in new[] { attacker, defender })
        {
            var led = Admirals(w, side, systemId); var here = w.Fleets.Values.Where(f => f.OwnerId == side && !f.Moving && f.SystemId == systemId).ToList();
            int ready = here.Count == 0 ? 0 : (int)here.Average(f => f.Readiness);
            notes.Add($"{w.House(side)?.Name ?? side}: " + (led.Count > 0 ? "led by " + string.Join(" and ", led.Select(c => $"{c.Name} ({(here.Any(f => f.CaptainId == c.Id) ? "its captain, " : "")}tactics {c.Skill("tactics")})")) : "no admiral aboard") + $", readiness {ready}"
                + (here.Any(f => f.Supply < _rules.SupplyLow) ? ", short of supply" : "") + (here.Any(f => f.Loadout == "torpedoes") ? ", torpedoes loaded" : ""));
        }
        double roll = rng.NextDouble();
        bool attackerWins = roll < pa / (pa + pd);
        int chance = (int)Math.Round(100 * pa / (pa + pd));
        if (attackerWins ? chance < 40 : chance > 60) notes.Add($"The weaker side won: at these odds that happens {(attackerWins ? chance : 100 - chance)} times in 100");
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
            if (into != null) { if (into.Ships.Count == 0) into.Ships[_rules.DefaultClass] = into.Strength; into.Ships[_rules.DefaultClass] = into.Ships.GetValueOrDefault(_rules.DefaultClass) + prizes; into.Recount(); w.Log($"Prize crews of {into.Name} take {Counted.Hulls(prizes)} of the enemy as their own."); WarLedger.Prizes(w, winner, loser, prizes); if (winner == player) w.WorldFlags.Add("hulls_captured"); }
        }
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == winner && !f.Moving && f.SystemId == systemId))
        { f.Veterancy = Math.Min(_rules.VeterancyMax, f.Veterancy + 1); f.Loyalty = Math.Min(100, f.Loyalty + _rules.LoyaltyWin); }
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == loser && !f.Moving && f.SystemId == systemId)) f.Loyalty = Math.Max(0, f.Loyalty - _rules.LoyaltyLoss);
        if (player != null && (winner == player || loser == player))
        {
            w.WorldFlags.Add(winner == player ? "battle_won" : "battle_lost");
            if (winnerAdmirals.Concat(loserAdmirals).Any(c => c.Id == w.ProtagonistId && c.Flags.Contains(FrontFlag))) w.WorldFlags.Add("fought_at_the_front");
        }
        CaesarSystem.Victory(w, winner, loser, systemId);   // the crews take to the one who led them; a victory to his name (docs/CAESAR.md)
        ServiceSystem.Fought(w, winner, loser, systemId);   // and to a commander of another house, by service, the same (docs/MILITARY.md, "A command by service")
        NationSystem.BattleOnMap(w, winner, loser);        // and if it was fought against a nation, it counts in that war (docs/BEYOND.md)
        var ledBefore = w.Fleets.Values.Where(f => !f.Moving && f.SystemId == systemId && (f.OwnerId == winner || f.OwnerId == loser)).ToDictionary(f => f.Id, f => Commander(w, f)?.Id ?? "");
        AdmiralOutcomes(w, rng, winnerAdmirals, loserAdmirals, winner);
        CaptainsTakeOver(w, ledBefore, player);
        Retreat(w, loser, systemId);

        var sys = w.Galaxy!.System(systemId)?.Name ?? systemId;
        w.Log($"Battle of {sys}: {w.House(winner)?.Name ?? winner} defeats {w.House(loser)?.Name ?? loser} " +
              $"({w.House(attacker)?.Name ?? attacker} attacking, {w.House(defender)?.Name ?? defender} holding). " +
              $"Hulls lost — {w.House(attacker)?.Name ?? attacker} {(attackerWins ? winnerLosses : loserLosses)}, {w.House(defender)?.Name ?? defender} {(attackerWins ? loserLosses : winnerLosses)}.");
        w.Battles.Add(new BattleRecord { Turn = w.Turn, SystemId = systemId, Place = sys, AttackerId = attacker, DefenderId = defender, WinnerId = winner, AttackerHulls = hullsA, DefenderHulls = hullsD,
            AttackerLost = attackerWins ? winnerLosses : loserLosses, DefenderLost = attackerWins ? loserLosses : winnerLosses, AttackerPower = Math.Round(pa, 1), DefenderPower = Math.Round(pd, 1), Notes = notes });
        if (w.Battles.Count > 60) w.Battles.RemoveRange(0, w.Battles.Count - 60);
        return new BattleReport(systemId, attacker, defender, winner, attackerWins ? winnerLosses : loserLosses, attackerWins ? loserLosses : winnerLosses, pa, pd, w.Turn);
    }

    private static List<Character> Admirals(World w, string houseId, string systemId) =>
        w.Fleets.Values.Where(f => f.OwnerId == houseId && !f.Moving && f.SystemId == systemId)
            .Select(f => Commander(w, f)).Where(c => c is { Alive: true }).Select(c => c!).Distinct().OrderBy(c => c.Id, StringComparer.Ordinal).ToList();   // (a captain commands a fleet that has no admiral fit to)

    /// <summary>After a battle: where a fleet's admiral fell or was wounded and its captain has the command for it, say so.</summary>
    private static void CaptainsTakeOver(World w, Dictionary<string, string> ledBefore, string? player)
    {
        foreach (var (id, was) in ledBefore.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            if (!w.Fleets.TryGetValue(id, out var f) || CaptainOf(w, f) is not { } cap || was == cap.Id || Commander(w, f) != cap) continue;
            var adm = w.Char(f.AdmiralId); string why = adm == null ? "it has no admiral" : adm.Alive ? $"{adm.Name} is wounded" : $"{adm.Name} is dead";
            w.Log($"{cap.Name}, captain of {f.Name}, has the command: {why}.");
            if (f.OwnerId == player) w.Tell("warn", $"{cap.Name}, captain of {f.Name}, has the command: {why}. The fleet fights under the captain until {(adm is { Alive: true } ? "the admiral is well again" : "you name an admiral")}.", "military:deck", f.Id);
        }
    }

    /// <summary>The victor's admiral learns (tactics, then gunnery); the loser's is wounded — or, on a seeded roll, killed
    /// (GDD §5 "losing a Fleet Admiral means the heir starts as a green cadet"; a dead protagonist triggers succession).</summary>
    public void AdmiralOutcomes(World w, IRng rng, List<Character> winners, List<Character> losers, string winnerHouseId = "")
    {
        foreach (var c in winners)
        {
            // a victory teaches, but only what the commander has it in them to learn (docs/EDUCATION.md): the weaker of the two crafts
            // first, the other if that one is at its ceiling, and nothing once both are
            string skill = c.Skill("tactics") <= c.Skill("gunnery") ? "tactics" : "gunnery", other = skill == "tactics" ? "gunnery" : "tactics";
            if (c.Skill(skill) >= EducationSystem.Ceiling(c, skill)) skill = other;
            if (c.Skill(skill) < EducationSystem.Ceiling(c, skill)) { c.Skills[skill] = c.Skill(skill) + 1; w.Log($"{c.Name} learns from the victory ({skill} {c.Skill(skill)})."); }
            LifeSystem.Note(w, c, "victory", w.Fleets.Values.Where(f => f.CaptainId == c.Id).OrderBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault() is { } ship ? $"Won a battle as captain, in command of {ship.Name}."
                : ServiceSystem.Held(w, c) is { } served ? $"Won a battle in command of {served.FleetName}, for {w.House(served.OwnerId)?.Name ?? served.OwnerId}, as {ServiceSystem.KindName(served)}." : "Won a battle in command of a fleet.");
            if (c.Flags.Contains(FrontFlag))
            {
                int v = w.Counter(ValourKey(c.Id)) + 1; w.WorldCounters[ValourKey(c.Id)] = v;
                if (v >= _rules.AchillesValour && !c.Flags.Contains(AchillesFlag))
                {
                    c.Flags.Add(AchillesFlag); w.Log($"{c.Name} has led from the front through {v} victories; the Reach begins to speak the name the way it speaks of Achilles.");
                    if (c.Id == w.ProtagonistId) w.WorldFlags.Add("became_achilles");
                    if (w.House(c.HouseId) is { } ah) LegitimacyBook.Move(w, ah, 2, LegitimacyBook.War, "An admiral of the house spoken of as Achilles is");
                }
            }
        }
        foreach (var c in losers)
        {
            if (c.Flags.Contains(FlagFlag)) { c.Stress = Math.Min(100, c.Stress + 6); AmbitionSystem.Setback(w, c, "Lost a battle in command of a fleet."); continue; }   // the flag is not in the line
            double death = _rules.AdmiralDeathChance * (c.Flags.Contains(FrontFlag) ? _rules.FrontDeathMult : 1.0);
            if (rng.NextDouble() < death)
            {
                w.Log($"{c.Name} goes down with the line.");
                // war is forgotten at the peace, but not the death of a head or of a head's near blood (docs/FEUD.md)
                string killers = winnerHouseId.Length > 0 ? winnerHouseId : winners.FirstOrDefault()?.HouseId ?? "";
                if (killers.Length > 0 && FeudSystem.BattleDeathBinds(w, c)) FeudSystem.Blood(w, c, killers, winners.OrderByDescending(a => a.CareerRank).ThenBy(a => a.Id, StringComparer.Ordinal).FirstOrDefault()?.Id, "battle");
                DynastySystem.Die(w, c.Id, "in battle");
            }
            else
            {
                LifeSystem.Wound(w, c, "when the line broke");   // it costs their health, and it shows (docs/LIFE.md)
                AmbitionSystem.Setback(w, c, "Lost a battle in command of a fleet.");
                c.Stress = Math.Min(100, c.Stress + 10);
            }
        }
    }

    /// <summary>Power = Σ hulls × class stat (attack assaulting, defense holding) × (0.5 + readiness/200) × admiral × veterancy;
    /// defenders × (1 + bonus + the system's holdings' defense).</summary>
    public double Power(World w, string houseId, string systemId, bool defending) =>
        PowerWith(w, houseId, systemId, w.Fleets.Values.Where(f => f.OwnerId == houseId && !f.Moving && f.SystemId == systemId), defending);

    /// <summary>What a house would weigh in a system with the fleets named: <see cref="Power"/> for the fleets standing there, and the
    /// staff's estimate for a fleet that has not sailed yet (<see cref="OddsAt"/>).</summary>
    public double PowerWith(World w, string houseId, string systemId, IEnumerable<Fleet> fleets, bool defending)
    {
        double p = 0;
        foreach (var f in fleets) p += PowerOf(w, f, defending);
        if (defending) p *= 1 + _rules.DefenderBonus + Defense(w, systemId, houseId);
        p *= 1 + FlagFactor(w, houseId);
        if (CampaignTarget(w, houseId) is { } target && w.Galaxy != null && (target == systemId || w.Galaxy.Hops(target, systemId) is > 0 and var cd && cd <= _rules.CampaignRange)) p *= 1 + _rules.CampaignBonus;
        if (w.NationOfHouse(houseId) is { Mettle: > 0 } nation) p *= nation.Mettle / 100.0;   // a nation's fleets fight by its temper, as they did off the map (docs/BEYOND.md)
        return p;
    }

    /// <summary>What the staff makes of sending fleets to a system: what they would weigh there, what stands there against them, and
    /// the chance of the day (docs/PLAYTHROUGH.md 10: "no way to see the odds before sailing"). It counts only what the house can
    /// see: enemy fleets in a watched system or with a report on file. Null when no enemy is known to be there.</summary>
    public sealed record Odds(int Chance, int MyHulls, int TheirHulls, string Enemies, bool TheyHoldTheGround, int GroundPercent, bool Sighted, int ReportYear);
    public Odds? OddsAt(World w, string houseId, IReadOnlyCollection<Fleet> sailing, string systemId)
    {
        if (w.Galaxy == null) return null;
        var watched = Watched(w); bool sighted = watched.Contains(systemId);
        var there = w.Fleets.Values.Where(f => !f.Moving && !f.IsAway && f.SystemId == systemId && f.OwnerId != houseId && w.AtWar(f.OwnerId, houseId) && (sighted || w.Intel.ContainsKey(f.Id))).OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
        if (there.Count == 0) return null;
        string? owner = TerritoryOwner(w, systemId);
        var mine = sailing.Concat(w.Fleets.Values.Where(f => f.OwnerId == houseId && !f.Moving && f.SystemId == systemId && !sailing.Contains(f))).ToList();
        double pm = PowerWith(w, houseId, systemId, mine, defending: owner == houseId), pt = 0; bool ground = false; double bonus = 0;
        foreach (var enemy in there.Select(f => f.OwnerId).Distinct().OrderBy(h => h, StringComparer.Ordinal))
        {
            bool holds = owner == enemy; pt += PowerWith(w, enemy, systemId, there.Where(f => f.OwnerId == enemy), defending: holds);
            if (holds) { ground = true; bonus = _rules.DefenderBonus + Defense(w, systemId, enemy); }
        }
        int year = sighted ? w.Turn : there.Max(f => w.Intel.TryGetValue(f.Id, out var i) ? i.TurnObserved : w.Turn);
        return new Odds(pm + pt <= 0 ? 50 : (int)Math.Round(100 * pm / (pm + pt)), mine.Sum(f => f.Strength), there.Sum(f => f.Strength),
            string.Join(" and ", there.Select(f => w.House(f.OwnerId)?.Name ?? f.OwnerId).Distinct()), ground, (int)Math.Round(100 * bonus), sighted, year);
    }
    /// <summary>The odds in a sentence, for a screen.</summary>
    public static string Say(Odds o) => $"{o.Enemies} {(o.TheirHulls == 1 ? "has" : "have")} {Counted.Hulls(o.TheirHulls)} there{(o.Sighted ? "" : $" by the last report (year {o.ReportYear})")}" +
        (o.TheyHoldTheGround ? $", on their own ground (+{o.GroundPercent}% to them)" : "") + $". With {Counted.Hulls(o.MyHulls)} of yours the staff gives you {o.Chance} in 100" +
        (o.Chance >= 65 ? ": the day should be yours." : o.Chance >= 45 ? ": it could go either way." : o.Chance >= 25 ? ": the odds are against you." : ": it would be thrown away.");

    /// <summary>One fleet's power on its own: hulls × class stat × readiness × admiral × veterancy × the depth factor. The house-wide flag and
    /// campaign factors are added by <see cref="Power"/>; expeditions (NationSystem) read this alone, with the flag factor.</summary>
    public double PowerOf(World w, Fleet f, bool defending)
    {
        double hulls = 0;
        foreach (var (cls, n) in Composition(f)) hulls += n * (defending ? cls.Defense : cls.Attack);
        return hulls * (0.5 + f.Readiness / 200.0) * AdmiralFactor(w, f) * CaptainFactor(w, f) * (1 + f.Veterancy * _rules.VeterancyBonus) * DepthFactor(w, f);
    }

    /// <summary>The military depth's per-fleet factor (docs/MILITARY.md): stance, torpedoes with isotopes in store, a commander at the front
    /// (and an Achilles), a fleet over its commander's cap, short supply, restive crews.</summary>
    public double DepthFactor(World w, Fleet f)
    {
        double k = f.Stance switch { "press" => _rules.PressPower, "boarding" => _rules.PressPower, "skirmish" => _rules.SkirmishPower, _ => 1.0 };
        if (f.Loadout == "torpedoes" && (w.House(f.OwnerId)?.Treasury.Isotopes ?? 0) > 0) k *= 1 + _rules.TorpedoBonus;
        var c = Commander(w, f);
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
        // a commander by service lends it to the house they fight for, not to their own (docs/MILITARY.md, "A command by service")
        var best = w.Characters.Values.Where(c => c.Alive && c.Flags.Contains(FlagFlag) && (c.HouseId == houseId || w.Service.Count > 0) && ServiceSystem.Serves(w, c) == houseId).Select(c => c.Skill("tactics")).DefaultIfEmpty(-1).Max();
        return best < 0 ? 0 : Math.Min(_rules.FlagMax, best * _rules.FlagPerTactics);
    }

    /// <summary>The admiral's skill (GDD §6): tactics 5%, gunnery 3%, leadership 2% per point; Tactician +10%, Strategist +5%;
    /// a wounded or broken admiral −10%. Unled fleets fight at 0.85.</summary>
    public static double AdmiralFactor(World w, Fleet f) => Commander(w, f) is { } c ? CommandFactor(c) : 0.85;

    /// <summary>What a commander is worth to the fleet they command, by the admiral's reckoning: the same for a captain who commands.</summary>
    public static double CommandFactor(Character c)
    {
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
        if (w.PlanetOf(systemId) is { } seat && seat.OwnerId == houseId && seat.Provinces.Count > 0) d += ProvinceSystem.Defense(_catalog, seat);   // a march (docs/PROVINCES.md)
        if (SystemWorks.Any(w) && w.PlanetHolder(systemId) == houseId) d += SystemWorks.Defense(w, _catalog, systemId);   // batteries, a fortress moon (docs/SYSTEMS.md)
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
            f.DestinationId = havens[0].s; f.Eta = havens[0].hops; f.Voyage = f.Eta;
            w.Log($"{f.Name} falls back toward {w.Galaxy!.System(havens[0].s)?.Name ?? havens[0].s}.");
        }
    }

    // ---------------- siege & conquest ----------------

    /// <summary>Turns a blockade needs to take a holding: the base plus two per point of its defense.</summary>
    public int SiegeRequired(World w, Holding h) =>
        _rules.SiegeTurns + (int)Math.Ceiling(2 * ((_catalog.Spec(h.Specialization)?.Defense ?? 0) + h.Buildings.Sum(b => _catalog.Building(b)?.Defense ?? 0)
            + (h.IsPlanet && SystemWorks.Any(w) ? SystemWorks.Defense(w, _catalog, h.SystemId) : 0) + (h.IsPlanet && h.Provinces.Count > 0 ? ProvinceSystem.Defense(_catalog, h) : 0)));

    /// <summary>What a year at drill costs a fleet's house in manpower.</summary>
    public int DrillCost(Fleet f) => Math.Max(1, (f.Strength + _rules.DrillHullsPerManpower - 1) / Math.Max(1, _rules.DrillHullsPerManpower));
    /// <summary>Why a fleet would not drill this year, or null: it must lie at a world or station of its house's own, below what
    /// drill can teach, and the house must have the men.</summary>
    public string? DrillStops(World w, Fleet f)
    {
        if (f.Veterancy >= _rules.DrillCap) return $"it is as well drilled as drill makes a fleet (veterancy {f.Veterancy}): the rest is learnt in battle";
        if (f.IsAway || f.Moving) return "it is under way: a fleet drills where it lies";
        if (!w.Holdings.Values.Any(h => h.OwnerId == f.OwnerId && h.SystemId == f.SystemId)) return $"it drills only at a world or station of its house's own: {w.Galaxy?.System(f.SystemId)?.Name ?? "this system"} is not";
        int need = DrillCost(f), have = w.House(f.OwnerId)?.Treasury.Manpower ?? 0;
        return have >= need ? null : $"a year's drill is {need} manpower, and the house has {have}";
    }
    /// <summary>The year at drill: each fleet kept at it that can drill takes its manpower and gains a step of veterancy.</summary>
    private void Drill(World w)
    {
        var mine = w.Protagonist?.HouseId;
        foreach (var f in w.Fleets.Values.Where(f => f.Drilling).OrderBy(f => f.Id, StringComparer.Ordinal).ToList())
        {
            var house = w.House(f.OwnerId); if (house == null) { f.Drilling = false; continue; }
            string? stops = DrillStops(w, f);
            if (stops != null)
            {
                if (f.Veterancy >= _rules.DrillCap) { f.Drilling = false; if (f.OwnerId == mine) w.Tell("good", $"{f.Name} is as well drilled as drill makes a fleet (veterancy {f.Veterancy}, each step worth {_rules.VeterancyBonus * 100:0}% in a fight). The drill is ended; the rest is learnt in battle", "military", f.Id); }
                else if (f.OwnerId == mine) w.Tell("warn", $"{f.Name} did not drill this year: {stops}", "military", f.Id);
                continue;
            }
            house.Treasury.Manpower -= DrillCost(f); f.Veterancy += 1;
            w.Log($"{f.Name} is a year at drill and is the better for it (veterancy {f.Veterancy}).");
        }
    }

    /// <summary>How a world of a house you are at war with is taken, as these rules have it, or "" when you are not at war with its
    /// holder or it is a nation's (docs/PLAYTHROUGH.md, War and peace: "how to attack a world in a war between houses").</summary>
    public string HowToTake(World w, Holding h, string attacker)
    {
        if (h.OwnerId == attacker || !w.AtWar(attacker, h.OwnerId) || w.Foreign(h.OwnerId) || h.SystemId.Length == 0) return "";
        string them = w.House(h.OwnerId)?.Name ?? h.OwnerId; int years = SiegeRequired(w, h);
        bool theirs = StrengthAt(w, h.OwnerId, h.SystemId) > 0, mine = StrengthAt(w, attacker, h.SystemId) > 0;
        string now = w.Sieges.TryGetValue(h.Id, out var s) ? (s.BesiegerId == attacker ? $"Your siege stands at {s.Turns} of {s.Required} years." : $"{w.House(s.BesiegerId)?.Name ?? s.BesiegerId} is besieging it ({s.Turns} of {s.Required}): while its fleet stays, the siege and the world are its.")
            : theirs ? $"A fleet of {them}'s is over it now: it must be beaten or drawn off first." : mine ? "Your fleet is over it: the siege begins when the year turns." : "No fleet of yours is there.";
        return $"To take {h.Name}: keep a fleet over it while none of {them}'s is there. That is a siege. It takes {Counted.Years(years)} here ({_rules.SiegeTurns} for any world, and more for its defences), and while it lasts the world sends out half its yield and loses people. "
            + $"When the years are spent the world is yours. If a fleet of {them}'s is over it when a year turns, the siege is lifted and its years are lost. There is no separate raid or blockade between houses: the siege is both. {now}";
    }

    /// <summary>Whose siege it is when the holder has no fleet over the world: the house already pressing it while it
    /// keeps a fleet there; else, of the holder's enemies in the sky, the one that came first (then the stronger).</summary>
    public string? Besieger(World w, Holding h)
    {
        var there = w.EnemiesOf(h.OwnerId).Distinct().Where(e => StrengthAt(w, e, h.SystemId) > 0).ToList();
        if (there.Count == 0) return null;
        if (w.Sieges.TryGetValue(h.Id, out var s) && there.Contains(s.BesiegerId)) return s.BesiegerId;
        int Came(string e) => w.Fleets.Values.Where(f => f.OwnerId == e && !f.Moving && f.SystemId == h.SystemId).Select(f => f.ArrivedTurn).DefaultIfEmpty(int.MaxValue).Min();
        return there.OrderBy(Came).ThenByDescending(e => StrengthAt(w, e, h.SystemId)).ThenBy(e => e, StringComparer.Ordinal).First();
    }

    /// <summary>Advance sieges: an enemy fleet alone in the sky over a holding tightens the blockade; when the clock
    /// reaches the holding's resistance it changes hands (GDD §13 Military — "take it"). A contested sky lifts the siege.</summary>
    public void ResolveConquests(World w)
    {
        if (w.Galaxy == null) return;
        var player = w.Protagonist?.HouseId;

        foreach (var h in w.Holdings.Values.Where(h => h.SystemId != "").OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            var besieger = StrengthAt(w, h.OwnerId, h.SystemId) > 0 ? null : Besieger(w, h);
            if (besieger == null)
            {
                if (w.Sieges.TryGetValue(h.Id, out var lifted)) { VentureSystem.SiegeLifted(w, lifted); w.Sieges.Remove(h.Id); w.Log($"The blockade of {h.Name} is lifted."); }   // what it cost is written down for the peace
                continue;
            }
            // a house that raids a nation sacks its world and goes home (docs/BEYOND.md, "Whose war"): a season's work, and not again for years
            bool sack = NationSystem.Sacks(w, h, besieger);
            if (sack && NationSystem.Sacked(w, h)) { w.Sieges.Remove(h.Id); continue; }
            if (w.Sieges.TryGetValue(h.Id, out var left) && left.BesiegerId != besieger && !sack)
            {
                // the house that pressed it has gone, and another at war with the holder is still in the sky: the walls are no
                // stronger for the change of flag, so the years already spent stand (docs/PLAYTHROUGH.md: an ally's fleet took a siege over and its years were lost)
                string was = w.House(left.BesiegerId)?.Name ?? left.BesiegerId, now = w.House(besieger)?.Name ?? besieger;
                left.BesiegerId = besieger; w.Log($"{was} leaves the siege of {h.Name}; {now} takes it up at {left.Turns} of {left.Required}.");
                if (besieger == player) w.Tell("good", $"{was} has left the siege of {h.Name}. It is yours now, with {left.Turns} of {left.Required} years already spent.", "galaxy:" + h.SystemId, h.Id);
            }
            if (!w.Sieges.TryGetValue(h.Id, out var siege) || siege.BesiegerId != besieger)
            {
                siege = new Siege { HoldingId = h.Id, BesiegerId = besieger, Turns = 0, Required = sack ? NationSystem.SackTurns(w, h) : SiegeRequired(w, h) + (w.Foreign(besieger) ? NationSystem.SiegeSlow : 0) + (w.NationOfHouse(h.OwnerId)?.Walls ?? 0) };   // a host from beyond the rim is slow at walls (docs/BEYOND.md)
                w.Sieges[h.Id] = siege;
                w.Log($"{w.House(besieger)?.Name ?? besieger} lays siege to {h.Name} ({siege.Required} turns to fall).");
            }
            siege.Turns += 1;
            if (siege.Turns < siege.Required) continue;

            w.Sieges.Remove(h.Id);
            if (sack) NationSystem.Sack(w, h, besieger); else Conquer(w, h, besieger, player);
        }
        RefreshSiegeFlag(w);
        WarLedger.Close(w);   // the year's fighting is done: each war's account for the year is closed (docs/MILITARY.md, "Commanding a navy")
    }

    private void Conquer(World w, Holding h, string conqueror, string? player)
    {
        string old = h.OwnerId, before = h.FormerOwnerId;
        // what a nation took from a house of the Reach, and another house of the Reach takes back, goes home to the house it was taken from (docs/BEYOND.md)
        string? freedFor = w.Foreign(old) && before.Length > 0 && w.OfTheReach(before) && w.OfTheReach(conqueror) ? before : null;
        if (freedFor != null)
        {
            h.OwnerId = freedFor; h.FormerOwnerId = ""; h.Unrest = Math.Clamp(h.Unrest + _rules.ConquestUnrest / 2, 0, 100);
            NationSystem.WorldTaken(w, h, conqueror, old, freedFor);
            if (conqueror == player && freedFor != player) NationSystem.Mark(w, "world_freed_by_us", w.NationOfHouse(old)); if (freedFor == player && conqueror != player) NationSystem.Mark(w, "world_freed_for_us", w.NationOfHouse(old));
            w.Log(freedFor == conqueror ? $"{w.House(conqueror)?.Name ?? conqueror} takes {h.Name} back from {w.House(old)?.Name ?? old}." : $"{w.House(conqueror)?.Name ?? conqueror} frees {h.Name} from {w.House(old)?.Name ?? old}; it is {w.House(freedFor)?.Name ?? freedFor}'s again.");
            return;
        }
        h.OwnerId = conqueror; h.FormerOwnerId = old;
        if (h.IsPlanet) { WealthSystem.Plunder(w, old, conqueror, h, Admirals(w, conqueror, h.SystemId)); VentureSystem.Sack(w, h.SystemId, conqueror); }   // some of the enemy's ventures there do not survive the storming (docs/ECONOMY_MARKET.md)
        h.Unrest = Math.Clamp(h.Unrest + _rules.ConquestUnrest, 0, 100);
        WarLedger.Changed(w, h, old, conqueror, "taken");   // written into the war's account before the score moves
        w.Wars.FirstOrDefault(x => x.Between(old, conqueror))?.AddScore(conqueror, 3);
        NationSystem.WorldTaken(w, h, conqueror, old, null);
        // a world taken or lost is news (docs/playthroughs/soldier.md 27: a world fell with no report)
        bool scores = w.Wars.Any(x => x.Between(old, conqueror));
        if (conqueror == player) w.Tell("good", $"{h.Name} is taken from {w.House(old)?.Name ?? old}: it is yours. Its people take it badly (unrest {h.Unrest}){(scores ? ", and it counts three in the war's score" : "")}.", "galaxy:" + h.SystemId, h.Id);
        else if (old == player) w.Tell("warn", $"{h.Name} is lost: {w.House(conqueror)?.Name ?? conqueror} has taken it{(scores ? ", and it counts three for them in the war's score" : "")}.", WarLedger.Target, h.Id);
        if (conqueror == player)
        {
            w.WorldCounters[ConquestsCounter] = w.Counter(ConquestsCounter) + 1;
            w.WorldFlags.Add(ConquestMadeFlag); w.WorldCounters["mark:" + ConquestMadeFlag] = w.Turn; w.WorldCounters[ConqueredKey(h.Id)] = w.Turn;   // news the year it falls, and the report can say which world
            w.WorldCounters["seizures"] = w.Counter("seizures") + 1;
            _friction.Apply(w, w.Protagonist, "conquest");
        }
        if (old == player) { w.WorldFlags.Add(HoldingLostFlag); _friction.Apply(w, w.Protagonist, "holding_lost"); }
        w.Log($"{w.House(conqueror)?.Name ?? conqueror} takes {h.Name} from {w.House(old)?.Name ?? old}.");

        // titles follow the land (GDD §13): a house stripped of its last holding falls to landless, and the
        // conqueror gains a claim to the rung it held — pressable through the Intrigue path.
        var loser = w.House(old);
        if (World.Outsider(old)) return;   // a nation that loses its last world is dealt with by its own rules, and leaves no rank to claim
        if (loser != null && !w.HoldingsOf(old).Any() && loser.Title != "landless" && World.Outsider(conqueror))
        {
            // taken by a power from outside: the house is landless, and nobody of the Reach has a claim by it. A throne left empty is filled by the Reach (docs/THRONE.md)
            loser.Title = "landless"; w.Log($"{loser.Name} holds nothing now: its last world is in foreign hands.");
        }
        else if (loser != null && !w.HoldingsOf(old).Any() && loser.Title != "landless")
        {
            string fallen = loser.Title;
            if (fallen == Progression.Title.Emperor && w.House(conqueror) is { } taker)
            {
                // a throne does not stand empty: whoever takes the last world of the house that sat on it has taken it (docs/THRONE.md)
                ThroneSystem.Enthrone(w, conqueror, seized: true, Economy.HeadOf(w, taker)?.Id);
                loser.Title = "landless";
                w.Log($"{loser.Name} holds nothing now but its claim.");
            }
            else
            {
                loser.Title = "landless";
                w.House(conqueror)?.Claims.Add(fallen);
                w.Log($"{loser.Name}, landless, loses the rank of {Progression.Title.StyleOf(fallen, w.Era)}; {w.House(conqueror)?.Name ?? conqueror} holds a claim to it.");
            }
        }
    }
}
