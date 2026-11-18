using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The chair (docs/LADDER.md, "The chair"). The great offices of the Reach act on the Senate's real business, not only on a
/// choice of three. The SPEAKER orders the docket: once a year one motion may be HELD OVER, and is not reached until the next
/// year; a motion can be held over once. The presiding CONSUL (the senior, or the junior when there is no senior) may SPEAK FROM
/// THE CHAIR on one motion a year, and the houses lean with the chair; and when the Senate is tied, the presiding consul's own
/// vote decides it. The TREASURER's charge is a budget with figures drawn from the state of the Reach: what the levy brought in,
/// what the lawless lanes lost of it, and what the fleet, the lanes and the regions call for. Other houses' officers use all of
/// it by their interest. RNG-free.
/// </summary>
public static partial class LadderSystem
{
    public const string HoldMotion = "hold", SpeakFor = "for", SpeakAgainst = "against";
    public const int ChairLean = 1, ChairLeanUnbenched = 2, HeldRegard = -6, SparedRegard = 4, SpokenForRegard = 3, SpokenAgainstRegard = -4;
    public const string HeldMineFlag = "my_motion_held_over", CastingFlag = "casting_vote_cast";
    public const string HeldCounter = "motions_held_over", SpokeCounter = "chair_spoke", CastingCounter = "casting_votes";
    private const string HeldKey = "speaker_held_in", SpokeKey = "chair_spoke_in";

    /// <summary>Who presides over the Senate: the senior consul, or the junior when the senior's chair is empty.</summary>
    public static Character? Presiding(World w) =>
        w.IsImperium ? null : (OfficeSystem.Holding(w, "consul_senior") ?? OfficeSystem.Holding(w, "consul_junior")) is { } h && w.Char(h.CharacterId) is { Alive: true } c ? c : null;
    public static Character? SpeakerOf(World w) => OfficeSystem.Holding(w, "speaker") is { } h && w.Char(h.CharacterId) is { Alive: true } c ? c : null;
    /// <summary>A motion aimed at a house to its hurt.</summary>
    private static bool Hostile(MotionDef def) => def.Target == "house" && def.Interest is "censure" or "embargo" or "breakup" or "recall";
    private static void Bump(World w, string key) => w.WorldCounters[key] = w.Counter(key) + 1;

    public static string? ChairRefusal(World w, Character? actor, Motion? m, string what)
    {
        if (actor?.Alive != true) return "nobody to take the chair";
        if (m == null || !m.Open) return "no such motion before the Senate";
        if (what == HoldMotion)
        {
            if (SpeakerOf(w)?.Id != actor.Id) return "only the Speaker orders the Senate's business";
            if (m.HeldOver) return "it has been held over once already, and must be reached";
            if (m.ProposedTurn >= w.Turn) return "it is not to be reached this year in any case";
            return w.WorldCounters.TryGetValue(HeldKey, out int y) && y == w.Turn ? "the Speaker has held one motion over this year" : null;
        }
        if (what != SpeakFor && what != SpeakAgainst) return "not a thing the chair does";
        if (Presiding(w)?.Id != actor.Id) return "only the presiding consul speaks from the chair";
        if (m.Chair != 0) return "the chair has spoken on it";
        return w.WorldCounters.TryGetValue(SpokeKey, out int s) && s == w.Turn ? "the chair has spoken once this year" : null;
    }

    /// <summary>Put a motion off by a year. Whoever moved it thinks the worse of the one who did it; whoever it was aimed at, the better.</summary>
    private static void HoldOver(World w, Motion m, House? by)
    {
        m.HeldOver = true; m.ProposedTurn++; Bump(w, HeldCounter);
        Shift(w, w.House(m.ProposerHouseId), by, HeldRegard);
        if (w.House(m.TargetId) is { } spared && spared.Id != by?.Id && spared.Id != m.ProposerHouseId) Shift(w, spared, by, SparedRegard);
        if (by != null && m.TargetId == by.Id) MoveTrust(w, -1);   // a Speaker who keeps what is aimed at their own house off the floor
        if (m.ProposerHouseId == w.Protagonist?.HouseId && by?.Id != m.ProposerHouseId) Mark(w, HeldMineFlag);
    }

    public static void Chair(World w, Character actor, Motion m, string what, string says)
    {
        var house = w.House(actor.HouseId); bool ours = actor.HouseId == w.Protagonist?.HouseId || m.ProposerHouseId == w.Protagonist?.HouseId || m.TargetId == w.Protagonist?.HouseId;
        if (what == HoldMotion)
        {
            HoldOver(w, m, house); w.WorldCounters[HeldKey] = w.Turn;
            w.Log($"{actor.Name}, Speaker, does not reach it this year: {says}");
            if (actor.Life?.Marks.Add("chair:held") != false) LifeSystem.Note(w, actor, "office", "Held a motion over, as Speaker.");
            return;
        }
        m.Chair = what == SpeakFor ? 1 : -1; w.WorldCounters[SpokeKey] = w.Turn; Bump(w, SpokeCounter);
        if (m.ProposerHouseId != actor.HouseId) Shift(w, w.House(m.ProposerHouseId), house, what == SpeakFor ? SpokenForRegard : SpokenAgainstRegard);
        if (ours) w.Log($"{actor.Name}, presiding, speaks from the chair {(what == SpeakFor ? "for" : "against")} it: {says}");
        if (actor.Life?.Marks.Add("chair:spoke") != false) LifeSystem.Note(w, actor, "office", "Spoke from the chair of the Senate.");
    }

    /// <summary>What the chair's word is worth to a house that neither moved a motion nor is its target: one who sits on no bench
    /// leans with it further than one who has a leader to follow.</summary>
    public static int ChairLeanOf(World w, Motion m, string houseId) =>
        m.Chair == 0 || houseId == m.ProposerHouseId || houseId == m.TargetId || Presiding(w)?.HouseId == houseId ? 0 : m.Chair * (FactionSystem.Of(w, houseId) == null ? ChairLeanUnbenched : ChairLean);

    /// <summary>A tie: the presiding consul's own vote decides it. True when it carries on the casting vote.</summary>
    public static bool CastingVote(World w, Motion m, out Character? consul)
    {
        consul = Presiding(w); if (consul == null || m.Votes.GetValueOrDefault(consul.HouseId) <= 0) return false;
        Bump(w, CastingCounter); if (consul.HouseId == w.Protagonist?.HouseId) Mark(w, CastingFlag);
        if (consul.Life?.Marks.Add("chair:casting") != false) LifeSystem.Note(w, consul, "office", "Carried a tied Senate on the casting vote.");
        return true;
    }

    /// <summary>What the officers of other houses do with the chair, before the vote: a Speaker holds over what is aimed at their
    /// own house or their bench's leader; a presiding consul speaks against the same, or else for what their own bench has moved.</summary>
    public static void ChairActs(World w, SenateSystem senate)
    {
        var me = w.Protagonist?.Id; var due = SenateSystem.Docket(w).Where(m => m.ProposedTurn < w.Turn).OrderBy(m => m.Id, StringComparer.Ordinal).ToList();
        bool Against(Motion m, string houseId, Faction? bench) => senate.Motions.Def(m.Kind) is { } def && Hostile(def) && m.ProposerHouseId != houseId && (m.TargetId == houseId || (bench != null && m.TargetId == bench.LeaderHouseId));
        if (SpeakerOf(w) is { } sp && sp.Id != me)
        {
            var bench = FactionSystem.Of(w, sp.HouseId);
            var m = due.FirstOrDefault(m => Against(m, sp.HouseId, bench) && ChairRefusal(w, sp, m, HoldMotion) == null);
            if (m != null) { Chair(w, sp, m, HoldMotion, senate.Describe(w, m)); due.Remove(m); }
        }
        if (Presiding(w) is { } pc && pc.Id != me)
        {
            var bench = FactionSystem.Of(w, pc.HouseId);
            var bad = due.FirstOrDefault(m => Against(m, pc.HouseId, bench) && ChairRefusal(w, pc, m, SpeakAgainst) == null);
            var good = bad != null ? null : due.FirstOrDefault(m => m.ProposerHouseId != pc.HouseId && bench != null && bench.Members.Contains(m.ProposerHouseId) && m.TargetId != pc.HouseId && ChairRefusal(w, pc, m, SpeakFor) == null);
            if (bad != null) Chair(w, pc, bad, SpeakAgainst, senate.Describe(w, bad));
            else if (good != null) Chair(w, pc, good, SpeakFor, senate.Describe(w, good));
        }
    }

    // ---------------- the budget ----------------

    public const int LevyEach = 2, LaneCall = 2, ReliefCall = 2, FleetShort = 5, LostAt = 40;
    /// <summary>The Reach's budget for a year, as it lies before the Treasurer: what was assessed on the landed houses, what the
    /// lawless lanes lost of it, what the Reach's own hulls, its lanes and its loose regions call for, and what the region that
    /// might be forgiven its levy pays.</summary>
    public sealed record BudgetFigures(int Assessed, int Lost, int Fleet, int Lanes, int Relief, int Region)
    {
        public int Collected => Math.Max(0, Assessed - Lost);
        public int Wanted => Fleet + Lanes + Relief;
        public int Surplus => Collected - Wanted;
    }
    private const string BudgetPrefix = "budget:";
    public static BudgetFigures? BudgetOf(OfficeBusiness b)
    {
        if (!b.Matter.StartsWith(BudgetPrefix)) return null;
        var p = b.Matter[BudgetPrefix.Length..].Split('|'); if (p.Length != 6) return null;
        var n = new int[6]; for (int i = 0; i < 6; i++) if (!int.TryParse(p[i], out n[i])) return null;
        return new BudgetFigures(n[0], n[1], n[2], n[3], n[4], n[5]);
    }
    /// <summary>Draw up the year's figures from the state of the Reach, and keep them with the charge so that what is read is what is decided.</summary>
    private static void DrawBudget(World w, OfficeBusiness b)
    {
        var landed = w.Houses.Values.Where(h => !World.Outsider(h.Id) && PolitySystem.InReach(w, h.Id) && w.HoldingsOf(h.Id).Any(x => x.IsPlanet)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        int assessed = landed.Count * LevyEach;
        int lost = landed.Count(h => HomeRegion(w, h.Id) is { Length: > 0 } r && RegionSystem.StateOf(w, r).Lawlessness >= LostAt) * LevyEach;
        int fleet = w.Fleets.Values.Where(f => f.Office).Sum(f => f.Strength);
        int lanes = w.Regions.Values.Count(r => r.Lawlessness >= 20) * LaneCall, relief = w.Regions.Values.Count(r => r.Allegiance < 60) * ReliefCall;
        int region = b.RegionId.Length > 0 ? landed.Count(h => HomeRegion(w, h.Id) == b.RegionId) * LevyEach : 0;
        b.Matter = BudgetPrefix + string.Join("|", assessed, lost, fleet, lanes, relief, region);
    }
    /// <summary>What balancing the books banks for the capital's purse: half the surplus, or nothing in a year of shortfall.</summary>
    public static int BalanceSum(OfficeBusiness b) => BudgetOf(b) is { } f ? (f.Surplus > 0 ? Math.Clamp((f.Surplus + 1) / 2, 1, 12) : 0) : BalanceCredits;
    /// <summary>What forgiving the region its levy costs the capital's purse: what its landed houses would have paid.</summary>
    public static int RemitSum(OfficeBusiness b) => BudgetOf(b) is { } f ? Math.Clamp(f.Region, 2, 12) : RemitCredits;
    /// <summary>What the Reach's contracts are worth to the house they are let to: a third of what the year calls for.</summary>
    public static int ContractSum(OfficeBusiness b) => BudgetOf(b) is { } f ? Math.Clamp(f.Wanted / 3, 6, 18) : ContractCredits;
    public static string BudgetSays(OfficeBusiness b)
    {
        if (BudgetOf(b) is not { } f) return "";
        string came = f.Lost > 0 ? $"{f.Assessed} credits were assessed on the landed houses and {f.Collected} came in: the lawless lanes lost {f.Lost}." : $"{f.Collected} credits came in from the levy on the landed houses.";
        string calls = $"The Reach's own hulls call for {f.Fleet}, its lanes for {f.Lanes} and its loose regions for {f.Relief}: {f.Wanted} in all.";
        return $" {came} {calls} " + (f.Surplus > 0 ? $"That leaves {f.Surplus} over." : f.Surplus < 0 ? $"That is {-f.Surplus} short." : "It comes out even.");
    }
}
