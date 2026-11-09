using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The nations beyond the Reach (docs/FOREIGN_NATIONS.md; the owner's direction: one playable nation, the others off the map as the targets of
/// its wars, in the mould of LOTGH's single strong neighbour and Rome's many). RNG-free: every roll is a hash of the turn and the parties.
/// Each turn, after the fleets at home have moved and fought: nations regain strength and drift in their regard for the Reach; the aggressive
/// and the decaying raid the rim when their regard is low; expeditions sail, fight the real battle math against the nation's committed strength,
/// and come home with plunder, valour and fewer hulls; a war ends when either side's score passes the nation's threshold (the Reach subdues it
/// and takes tribute for twenty turns, or the chair buys peace), or when twelve turns pass without a battle.
/// </summary>
public sealed class NationSystem
{
    public const int SubduedTurns = 20, FightTurns = 4, ExhaustionTurns = 12, RegenEvery = 5, RaidEvery = 6, RaidUnrest = 8, RaidCredits = 5, RaidMinGap = 4;
    public const double NationPowerPerStrength = 1.0, NationReserve = 1.25, WinStrengthLoss = 0.4, WinHullLoss = 0.15, LossHullLoss = 0.3;
    public const int ScorePerBattle = 2, ExpeditionSupplyDrain = 10, ReadinessWin = 15, ReadinessLoss = 30, LegitimacyTriumph = 8, InfluenceTriumph = 4, LegitimacyDefeat = 6;
    /// <summary>One-turn flags this system raises in BeginTurn, cleared at the top of its next Tick.</summary>
    public static readonly string[] TurnFlags = { "fleet_sailed_abroad", "expedition_returned", "expedition_lost", "expedition_victory", "expedition_defeat", "nation_subdued", "triumph", "war_lost_abroad", "raided_by_nation", "foreign_peace", "nation_freed" };
    /// <summary>One-turn flags raised in EndTurn (by the Senate's vote or the player's order), cleared with the Senate's at the top of the next EndTurn so the next turn's events see them.</summary>
    public static readonly string[] SenateTurnFlags = { "foreign_war_declared" };
    public const string WarsCounter = "foreign_wars", ExpeditionsCounter = "expeditions", BattlesCounter = "foreign_battles", SubduedCounter = "nations_subdued", TributeCounter = "tribute_received", RaidsCounter = "foreign_raids", LostCounter = "foreign_wars_lost";
    public static string WinsKey(string houseId, string nationId) => $"expedition_wins:{houseId}:{nationId}";
    public static string RaidedKey(string houseId) => "raided_turn:" + houseId;
    public static string RaidedByKey(string houseId) => "raided_by:" + houseId;
    /// <summary>The nation that last raided a world of the house within the turns given, or null.</summary>
    public static string? RaiderOf(World w, string houseId, int withinTurns) =>
        w.WorldCounters.TryGetValue(RaidedKey(houseId), out var t) && w.Turn - t <= withinTurns && w.WorldCounters.TryGetValue(RaidedByKey(houseId), out var i) && i >= 0 && i < w.Nations.Count ? w.Nations[i].Id : null;

    private readonly NationCatalog _catalog; private readonly FleetSystem _fleets;
    public NationSystem(NationCatalog catalog, FleetSystem fleets) { _catalog = catalog; _fleets = fleets; }
    public NationCatalog Catalog => _catalog;

    /// <summary>Create the nations from content at the start of a run (idempotent: a nation already in the World is kept).</summary>
    public static void Seed(World w, NationCatalog catalog)
    {
        foreach (var d in catalog.Defs.OrderBy(d => d.Id, StringComparer.Ordinal))
            if (w.Nation(d.Id) == null) w.Nations.Add(new Nation { Id = d.Id, Strength = d.Strength, Disposition = d.Disposition });
    }

    public static Nation? AtWar(World w) => w.Nations.Where(n => n.AtWar).OrderBy(n => n.Id, StringComparer.Ordinal).FirstOrDefault();
    public static bool Raided(World w, string nationId, int withinTurns) => w.Nation(nationId) is { } n && n.LastRaidTurn > 0 && w.Turn - n.LastRaidTurn <= withinTurns;
    public static IEnumerable<Fleet> Expeditions(World w, string? nationId = null) =>
        w.Fleets.Values.Where(f => f.IsAway && (nationId == null || f.Away == nationId)).OrderBy(f => f.Id, StringComparer.Ordinal);

    private static uint Hash(string key) { uint h = 2166136261; foreach (var ch in key) h = (h ^ ch) * 16777619; return h; }
    private static double Roll(string key) => (Hash(key) % 10000) / 10000.0;

    /// <summary>The Senate has voted war (SenateSystem.Apply) — or a test has. One war abroad at a time.</summary>
    public static void DeclareWar(World w, NationCatalog catalog, string nationId, string proposerHouseId)
    {
        var n = w.Nation(nationId); var d = catalog.Def(nationId); if (n == null || d == null || n.AtWar || n.Subdued(w.Turn)) return;
        if (AtWar(w) != null) return;
        n.AtWar = true; n.WarSince = w.Turn; n.ScoreReach = 0; n.ScoreNation = 0; n.LastBattleTurn = w.Turn; n.Wars += 1; n.Disposition = Math.Max(-100, n.Disposition - 20);
        w.WorldCounters[WarsCounter] = w.Counter(WarsCounter) + 1;
        w.WorldFlags.Add("foreign_war"); w.WorldFlags.Add("foreign_war_declared"); w.WorldFlags.Add("foreign_war:" + nationId);
        w.Log($"The Reach is at war with {d.Name}, on the motion of {w.House(proposerHouseId)?.Name ?? proposerHouseId}. The fleets that sail against it must beat {n.Strength} hulls; a score of {d.Threshold} subdues it.");
    }

    public void Tick(World w)
    {
        foreach (var f in TurnFlags) w.WorldFlags.Remove(f);
        if (w.WorldFlags.Remove("expedition_sent")) w.WorldFlags.Add("fleet_sailed_abroad");   // the player's order last turn becomes this turn's news
        foreach (var f in w.WorldFlags.Where(f => f.StartsWith("nation_subdued:") || f.StartsWith("raided_by:")).ToList()) w.WorldFlags.Remove(f);
        if (_catalog.IsEmpty) return;
        Seed(w, _catalog);
        var mine = w.Protagonist?.HouseId;
        foreach (var n in w.Nations.OrderBy(n => n.Id, StringComparer.Ordinal))
        {
            var d = _catalog.Def(n.Id); if (d == null) continue;
            Drift(w, n, d);
            Raid(w, n, d, mine);
            foreach (var f in Expeditions(w, n.Id).ToList()) Sail(w, n, d, f, mine);
            ResolveWar(w, n, d, mine);
            Tribute(w, n, d, mine);
        }
        // standing flags
        foreach (var f in new[] { "foreign_war", "expedition_away", "nation_tributary" }) w.WorldFlags.Remove(f);
        foreach (var f in w.WorldFlags.Where(f => f.StartsWith("foreign_war:")).ToList()) w.WorldFlags.Remove(f);
        if (AtWar(w) is { } war) { w.WorldFlags.Add("foreign_war"); w.WorldFlags.Add("foreign_war:" + war.Id); }
        if (mine != null && Expeditions(w).Any(f => f.OwnerId == mine)) w.WorldFlags.Add("expedition_away");
        if (w.Nations.Any(n => n.Subdued(w.Turn))) w.WorldFlags.Add("nation_tributary");
    }

    /// <summary>Strength regrows toward the content figure (decaying nations lose a hull every ten turns instead); regard drifts by temper, and sours at war.</summary>
    private static void Drift(World w, Nation n, NationDef d)
    {
        if (w.Turn % RegenEvery == 0)
        {
            if (d.Temper == "decaying") { if (w.Turn % (RegenEvery * 2) == 0 && n.Strength > 4) n.Strength -= 1; }
            else if (n.Strength < d.Strength) n.Strength = Math.Min(d.Strength, n.Strength + Math.Max(1, d.Regen));
        }
        if (n.AtWar) n.Disposition = Math.Max(-80, n.Disposition - 2);
        else if (n.Subdued(w.Turn)) { }
        else if (w.Turn % 3 == 0)
        {
            if (d.Temper == "aggressive" && n.Disposition > -60) n.Disposition -= 1;
            else if (d.Temper == "mercantile" && n.Disposition < 30) n.Disposition += 1;
            else if (d.Temper != "aggressive" && n.Disposition < d.Disposition) n.Disposition += 1;
        }
    }

    /// <summary>An aggressive or decaying nation whose regard has fallen raids a rim world about one turn in six: unrest there, credits from its
    /// holder; at war the raid also scores against the Reach.</summary>
    private static void Raid(World w, Nation n, NationDef d, string? mine)
    {
        if (d.Temper is not ("aggressive" or "decaying") || n.Disposition > -20 || n.Subdued(w.Turn) || w.Galaxy == null) return;
        if (w.Turn - n.LastRaidTurn < RaidMinGap || Hash($"raid:{n.Id}:{w.Turn}:{n.Disposition}:{w.Holdings.Count}") % RaidEvery != 0) return;
        var rim = w.Holdings.Values.Where(h => h.IsPlanet && h.SystemId.Length > 0 && h.OwnerId != PirateSystem.HouseId && w.Galaxy.RingOf(h.SystemId) == Galaxy.Ring.Rim).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        if (rim.Count == 0) rim = w.Holdings.Values.Where(h => h.IsPlanet && h.SystemId.Length > 0 && h.OwnerId != PirateSystem.HouseId).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        if (rim.Count == 0) return;
        var target = rim[(int)(Hash($"raidat:{n.Id}:{w.Turn}") % (uint)rim.Count)];
        if (FleetSystem.StrengthAt(w, target.OwnerId, target.SystemId) >= 3) { n.LastRaidTurn = w.Turn; w.Log($"Raiders of {d.Name} come for {target.Name} and find the fleet of {w.House(target.OwnerId)?.Name} in orbit; they turn back."); return; }
        n.LastRaidTurn = w.Turn;
        target.Unrest = Math.Min(100, target.Unrest + RaidUnrest);
        if (w.House(target.OwnerId) is { } owner) owner.Treasury.Credits -= Math.Min(RaidCredits, Math.Max(0, owner.Treasury.Credits));
        w.WorldCounters[RaidsCounter] = w.Counter(RaidsCounter) + 1;
        if (n.AtWar) n.ScoreNation += 1;
        w.WorldCounters[RaidedKey(target.OwnerId)] = w.Turn; w.WorldCounters[RaidedByKey(target.OwnerId)] = w.Nations.IndexOf(n);
        if (target.OwnerId == mine) { w.WorldFlags.Add("raided_by_nation"); w.WorldFlags.Add("raided_by:" + n.Id); }
        w.Log($"Raiders of {d.Name} fall on {target.Name}: cargo and crews taken, the world in uproar.{(n.AtWar ? " The war's tally turns a point their way." : "")}");
    }

    /// <summary>One turn of an expedition: sailing out, fighting in the nation's space, or sailing home.</summary>
    private void Sail(World w, Nation n, NationDef d, Fleet f, string? mine)
    {
        var house = w.House(f.OwnerId);
        if (house == null) { w.Fleets.Remove(f.Id); return; }
        switch (f.AwayPhase)
        {
            case "out":
                f.AwayTurns -= 1; f.Supply = Math.Max(0, f.Supply - ExpeditionSupplyDrain / 2);
                if (f.AwayTurns <= 0)
                {
                    if (!n.AtWar) { GoHome(w, f, d, "finds the war over and turns for home"); break; }
                    f.AwayPhase = "fighting"; f.AwayTurns = FightTurns; w.Log($"{f.Name} reaches the space of {d.Name}.");
                }
                break;
            case "fighting":
                if (!n.AtWar) { GoHome(w, f, d, "is recalled: the war is over"); break; }
                f.Supply = Math.Max(0, f.Supply - ExpeditionSupplyDrain);
                if (n.Strength <= 0) { n.ScoreReach += 1; w.Log($"{f.Name} finds nothing left of {d.Name}'s fleets to fight, and sweeps its lanes unopposed."); }
                else Fight(w, n, d, f, house, mine);
                if (f.AwayPhase != "fighting") break;   // broken in the fight: already turned for home with the full distance to sail
                f.AwayTurns -= 1;
                if (f.AwayTurns <= 0) GoHome(w, f, d, "has done its season and turns for home");
                break;
            case "home":
                f.AwayTurns -= 1;
                if (f.AwayTurns <= 0)
                {
                    f.SystemId = f.HomeSystemId.Length > 0 ? f.HomeSystemId : w.HoldingsOf(f.OwnerId).FirstOrDefault(h => h.IsPlanet && h.SystemId.Length > 0)?.SystemId ?? "";
                    house.Treasury.Credits += f.Plunder;
                    w.Log($"{f.Name} comes home from {d.Name}{(f.Plunder > 0 ? $" with {f.Plunder} credits of plunder" : "")}{(f.ExpeditionWins > 0 ? $" and {f.ExpeditionWins} victor{(f.ExpeditionWins == 1 ? "y" : "ies")}" : "")}.");
                    if (f.OwnerId == mine) w.WorldFlags.Add("expedition_returned");
                    f.Away = ""; f.AwayPhase = ""; f.AwayTurns = 0; f.HomeSystemId = ""; f.Plunder = 0; f.ExpeditionWins = 0;
                    if (f.SystemId.Length == 0) { w.Fleets.Remove(f.Id); w.Log($"{f.Name} has no port left to come home to; its crews scatter."); }
                }
                break;
            default:
                f.AwayPhase = "out"; f.AwayTurns = d.Distance; break;
        }
    }

    private static void GoHome(World w, Fleet f, NationDef d, string why)
    {
        if (f.AwayPhase == "home") return;
        f.AwayPhase = "home"; f.AwayTurns = d.Distance; w.Log($"{f.Name} {why}: {d.Distance} year{(d.Distance == 1 ? "" : "s")} out from the Reach.");
    }

    /// <summary>The abstract battle: the fleet's own power (the real math — hulls, readiness, admiral, veterancy, stance, supply, the flag) against
    /// the strength the nation commits — it matches the force sent and a quarter more, up to all it has — a power a hull, by temper (the defensive
    /// fight hardest at home, the mercantile and the decaying softest). A hash decides it in proportion: an even fleet with a plain admiral wins a
    /// little under half its battles; a veteran fleet under a good one, more.</summary>
    private void Fight(World w, Nation n, NationDef d, Fleet f, House house, string? mine)
    {
        double pw = _fleets.PowerOf(w, f, defending: false) * (1 + _fleets.FlagFactor(w, f.OwnerId));
        int committed = Math.Min(n.Strength, Math.Max(2, (int)Math.Ceiling(f.Strength * NationReserve)));
        double temper = d.Temper switch { "defensive" => 1.25, "mercantile" => 0.8, "decaying" => 0.85, _ => 1.0 };
        double pn = committed * NationPowerPerStrength * temper;
        double pWin = pw / Math.Max(0.001, pw + pn);
        bool win = Roll($"abroad:{n.Id}:{f.Id}:{w.Turn}") < pWin;
        n.LastBattleTurn = w.Turn; w.WorldCounters[BattlesCounter] = w.Counter(BattlesCounter) + 1;
        var commander = f.AdmiralId.Length > 0 ? w.Char(f.AdmiralId) : null; if (commander is { Alive: false }) commander = null;
        if (win)
        {
            int lost = Math.Clamp((int)Math.Floor(f.Strength * WinHullLoss * Math.Min(2.0, pn / Math.Max(0.001, pw))), 0, Math.Max(0, f.Strength - 1));
            for (int i = 0; i < lost; i++) FleetSystem.ShedHull(w, f);
            n.Strength = Math.Max(0, n.Strength - Math.Max(1, (int)Math.Ceiling(committed * WinStrengthLoss)));
            n.ScoreReach += ScorePerBattle; f.Plunder += d.Wealth; f.ExpeditionWins += 1;
            w.WorldCounters[WinsKey(house.Id, n.Id)] = w.Counter(WinsKey(house.Id, n.Id)) + 1;
            f.Readiness = Math.Max(0, f.Readiness - ReadinessWin); f.Veterancy = Math.Min(_fleets.Rules.VeterancyMax, f.Veterancy + 1); f.Loyalty = Math.Min(100, f.Loyalty + _fleets.Rules.LoyaltyWin);
            w.Log($"{f.Name} beats the fleets of {d.Name} in their own space: {committed} hulls met, {lost} of ours lost, {d.Wealth} credits taken. The tally stands {n.ScoreReach} to {n.ScoreNation}.");
            if (f.OwnerId == mine) w.WorldFlags.Add("expedition_victory");
            if (commander != null)
            {
                string skill = commander.Skill("tactics") <= commander.Skill("gunnery") ? "tactics" : "gunnery"; commander.Skills[skill] = commander.Skill(skill) + 1;
                if (commander.Flags.Contains(FleetSystem.FrontFlag))
                {
                    int v = w.Counter(FleetSystem.ValourKey(commander.Id)) + 1; w.WorldCounters[FleetSystem.ValourKey(commander.Id)] = v;
                    if (v >= _fleets.Rules.AchillesValour && !commander.Flags.Contains(FleetSystem.AchillesFlag))
                    {
                        commander.Flags.Add(FleetSystem.AchillesFlag); w.Log($"{commander.Name} has led from the front through {v} victories, the last beyond the Reach; the name is spoken the way Achilles' is.");
                        if (commander.Id == w.ProtagonistId) w.WorldFlags.Add("became_achilles");
                        house.Legitimacy = Math.Min(100, house.Legitimacy + 2);
                    }
                }
            }
        }
        else
        {
            int lost = Math.Max(1, (int)Math.Ceiling(f.Strength * LossHullLoss));
            for (int i = 0; i < lost && f.Strength > 1; i++) FleetSystem.ShedHull(w, f);
            n.ScoreNation += ScorePerBattle;
            f.Readiness = Math.Max(0, f.Readiness - ReadinessLoss); f.Loyalty = Math.Max(0, f.Loyalty - _fleets.Rules.LoyaltyLoss);
            w.Log($"{f.Name} is beaten in the space of {d.Name}: {lost} hull{(lost == 1 ? "" : "s")} lost against {committed}. The tally stands {n.ScoreReach} to {n.ScoreNation}.");
            if (f.OwnerId == mine) w.WorldFlags.Add("expedition_defeat");
            if (commander != null)
            {
                if (commander.Flags.Contains(FleetSystem.FlagFlag)) commander.Stress = Math.Min(100, commander.Stress + 6);
                else
                {
                    double death = _fleets.Rules.AdmiralDeathChance * (commander.Flags.Contains(FleetSystem.FrontFlag) ? _fleets.Rules.FrontDeathMult : 1.0);
                    if (Roll($"fall:{n.Id}:{commander.Id}:{w.Turn}") < death) { w.Log($"{commander.Name} falls far from home, in the space of {d.Name}."); DynastySystem.Die(w, commander.Id); }
                    else { commander.Flags.Add("wounded"); commander.Stress = Math.Min(100, commander.Stress + 10); }
                }
            }
            if (f.Strength <= 1) { GoHome(w, f, d, "is broken and limps for home"); if (f.OwnerId == mine) w.WorldFlags.Add("expedition_lost"); }
        }
    }

    /// <summary>The war's end: the Reach's score past the threshold (or the nation's fleets gone) subdues it; the nation's past it, the chair buys
    /// peace at thrice the tribute; twelve turns without a battle, both sides tire.</summary>
    private static void ResolveWar(World w, Nation n, NationDef d, string? mine)
    {
        if (!n.AtWar) return;
        var chair = RegionSystem.CapitalHolder(w);
        if (n.ScoreReach - n.ScoreNation >= d.Threshold || n.Strength <= 0)
        {
            n.SubduedUntil = w.Turn + SubduedTurns; n.Disposition = Math.Min(100, n.Disposition + 10); n.Strength = Math.Max(2, n.Strength);
            w.WorldCounters[SubduedCounter] = w.Counter(SubduedCounter) + 1; w.WorldFlags.Add("nation_subdued"); w.WorldFlags.Add("nation_subdued:" + n.Id);
            var top = w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId).OrderByDescending(h => w.Counter(WinsKey(h.Id, n.Id))).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault(h => w.Counter(WinsKey(h.Id, n.Id)) > 0);
            if (top != null)
            {
                top.Legitimacy = Math.Min(100, top.Legitimacy + LegitimacyTriumph); top.SphereInfluence["senate"] = top.SphereInfluence.GetValueOrDefault("senate") + InfluenceTriumph;
                if (top.Id == mine) w.WorldFlags.Add("triumph");
                w.Log($"{d.Name} sues for peace and will pay the Reach {d.Tribute} credits a year for {SubduedTurns} years. The triumph is {top.Name}'s, whose fleets won it.");
            }
            else w.Log($"{d.Name} sues for peace and will pay the Reach {d.Tribute} credits a year for {SubduedTurns} years.");
            EndWar(w, n, d);
        }
        else if (n.ScoreNation - n.ScoreReach >= d.Threshold)
        {
            n.Disposition = Math.Max(-100, n.Disposition - 10);
            w.WorldCounters[LostCounter] = w.Counter(LostCounter) + 1; w.WorldFlags.Add("war_lost_abroad");
            if (chair != null) { chair.Treasury.Credits -= d.Tribute * 3; chair.Legitimacy = Math.Max(0, chair.Legitimacy - LegitimacyDefeat); }
            w.Log($"The war with {d.Name} is lost: the chair{(chair != null ? $" of {chair.Name}" : "")} buys peace for {d.Tribute * 3} credits, and the Reach is shamed.");
            EndWar(w, n, d);
        }
        else if (w.Turn - Math.Max(n.WarSince, n.LastBattleTurn) >= ExhaustionTurns)
        {
            w.WorldFlags.Add("foreign_peace");
            w.Log($"The war with {d.Name} gutters out: no fleet has met theirs in {ExhaustionTurns} years, and both sides let it lie.");
            EndWar(w, n, d);
        }
    }

    /// <summary>The war is over, however it ended: the scores are struck and every expedition against the nation turns for home.</summary>
    private static void EndWar(World w, Nation n, NationDef d)
    {
        n.AtWar = false;
        foreach (var h in w.Houses.Keys.ToList()) w.WorldCounters.Remove(WinsKey(h, n.Id));
        foreach (var f in Expeditions(w, n.Id).ToList()) GoHome(w, f, d, "is recalled: the war is over");
    }

    private static void Tribute(World w, Nation n, NationDef d, string? mine)
    {
        if (n.SubduedUntil == w.Turn) { n.Disposition = d.Disposition; w.WorldFlags.Add("nation_freed"); w.Log($"{d.Name} pays its last tribute; the treaty is done and its envoys go home."); return; }
        if (!n.Subdued(w.Turn)) return;
        var chair = RegionSystem.CapitalHolder(w); if (chair == null) return;
        chair.Treasury.Credits += d.Tribute; w.WorldCounters[TributeCounter] = w.Counter(TributeCounter) + d.Tribute;
    }
}
