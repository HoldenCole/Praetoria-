using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>Emergency powers (docs/EMERGENCY.md; the owner's word of 2026-11-17, docs/GAPS.md R8). When the Reach is in danger the Senate
/// may vote one of its consuls powers above the other for a term: no colleague's veto and a veto of their own every year, the Senate's
/// deference to what they move, a levy on the houses without a vote, and no election while it lasts. When the term is up or the danger
/// past they are laid down. Another house's consul always lays them down. The player may ask for more, or keep them without leave,
/// and one who has held them past a first term may put it to the Senate that they be held for life: carried, that ends the Republic by
/// its own vote; refused, the powers are stripped. RNG-free.</summary>
public static class EmergencySystem
{
    public const string GrantedFlag = "emergency_granted", MineFlag = "emergency_mine", DueFlag = "emergency_due", KeptFlag = "emergency_kept", LaidDownFlag = "emergency_laid_down", LaidDownMineFlag = "emergency_laid_down_mine";
    public const string StrippedFlag = "emergency_stripped", LeviedFlag = "emergency_levied", LayFlag = "emergency_answer_lay", ForLifeFlag = "powers_for_life", HoldsChar = "holds_emergency_powers", LaidDownChar = "laid_down_the_powers";
    public const string GrantedKey = "emergencies_granted", RenewedKey = "emergencies_renewed", LaidDownKey = "emergencies_laid_down", KeptKey = "emergency_years_kept", StrippedKey = "emergencies_stripped";
    public const string DangerYearsKey = "danger_years", MovedKey = "emergencies_moved", RefusedKey = "emergencies_refused", LeviesKey = "emergency_levies", YearsKey = "emergency_years", EndedKey = "emergency_ended", ByVoteKey = "republics_ended_by_vote", ForLifeRefusedKey = "for_life_refused";
    public static string KeptOnceKey(string houseId) => "kept_powers:" + houseId;
    private static readonly string[] YearFlags = { GrantedFlag, DueFlag, KeptFlag, LaidDownFlag, LaidDownMineFlag, StrippedFlag, LeviedFlag };

    public const int Years = 3, GapYears = 12, Reluctance = 2, LesserOneIn = 3, LevyCredits = 3, LevyRegard = -3, HolderLeanBy = 2, MoveOneIn = 4, KeptMax = 3;
    public const int LaidDownStanding = 5, LaidDownTrust = 3, LaidDownRegard = 5, KeptTrust = -5, KeptStanding = -2, KeptRegard = -5, StrippedStanding = -8, StrippedRegard = -10;

    private static uint Hash(string key) { uint h = 2166136261; foreach (var ch in key) h = (h ^ ch) * 16777619; return h; }
    private static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }
    private static void Bump(World w, string key, int by = 1) => w.WorldCounters[key] = w.Counter(key) + by;
    private static bool Mine(World w, string houseId) => w.Protagonist?.HouseId == houseId;
    private static void Trust(World w, int by) => w.WorldCounters[LadderSystem.TrustKey] = Math.Clamp(LadderSystem.Trust(w) + by, 0, 100);
    private static void RegardOfAll(World w, Character whom, int by)
    {
        foreach (var h in w.Houses.Values.Where(h => h.Id != whom.HouseId && !World.Outsider(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal))
            if (Economy.HeadOf(w, h) is { } head) { var r = w.Relationship(head.Id, whom.Id); r.Disposition = Math.Clamp(r.Disposition + by, -100, 100); }
    }
    private static int Hulls(World w, string houseId) => w.Fleets.Values.Where(f => f.OwnerId == houseId).Sum(f => f.Strength);
    private static int AllHulls(World w) => Math.Max(1, w.Fleets.Values.Where(f => !World.Outsider(f.OwnerId)).Sum(f => f.Strength));

    // ---- reading ----

    /// <summary>What the Reach is in danger from, in words, or null if it is in none that would move the Senate to this.</summary>
    public static string? Danger(World w)
    {
        if (w.IsImperium) return null;
        if (GreatCrisisSystem.AtWar(w)) return "the great war";
        if (w.CivilWar != null) return "the civil war";
        if (w.Polities.Any(p => p.Recognised == 0)) return "a state in arms against the Reach";
        if (w.Nations.Any(n => n.AtWar)) return "the war beyond the Reach";
        return null;
    }
    /// <summary>How grave it is: a war for the Reach itself counts for more than one at its edge.</summary>
    public static int Gravity(World w) => GreatCrisisSystem.AtWar(w) || w.CivilWar != null ? 3 : Danger(w) != null ? 1 : 0;

    public static Character? Holder(World w) => w.Emergency is { } e && w.Char(e.HolderId) is { Alive: true } c ? c : null;
    public static bool Holds(World w, string houseId) => w.Emergency?.HouseId == houseId;
    /// <summary>The consul of a house, as a person, if it has one.</summary>
    public static Character? ConsulOf(World w, OfficeCatalog offices, string houseId) =>
        ElectionSystem.ConsulOffices(offices).Select(d => OfficeSystem.Holding(w, d.Id)).Where(h => h?.HouseId == houseId).Select(h => w.Char(h!.CharacterId)).FirstOrDefault(c => c is { Alive: true });
    /// <summary>The Senate defers to what the holder moves, until the powers are due to be laid down.</summary>
    public static int HolderLean(World w, string proposerHouseId) => w.Emergency is { Due: false } e && e.HouseId == proposerHouseId ? HolderLeanBy : 0;

    // ---- the vote ----

    /// <summary>May the Senate be asked to give this house's consul the powers, or to extend them?</summary>
    public static string? GrantRefusal(World w, House? target, OfficeCatalog offices)
    {
        if (target == null) return "no such house";
        if (w.IsImperium) return "there is no Senate to vote them";
        if (w.Emergency is { } e)
        {
            if (e.HouseId != target.Id) return $"{w.Char(e.HolderId)?.Name ?? "a consul"} holds them already";
            if (!e.Due && e.Until - w.Turn > 1) return $"they run until year {e.Until}: it is too soon to ask for more";
            return Danger(w) == null ? "the danger is past: there is nothing to ask more time for" : null;
        }
        if (Danger(w) == null) return "the Reach is in no danger that calls for it";
        if (ConsulOf(w, offices, target.Id) == null) return "only a sitting consul may be given them";
        int ended = w.Counter(EndedKey);
        return ended > 0 && w.Turn - ended < GapYears ? $"the Senate gave them only {w.Turn - ended} year(s) ago" : null;
    }

    /// <summary>How a house leans on giving them. No Senate likes to; the graver the danger the readier it is; a house thinks of the consul it is being asked to
    /// trust, of its party, and of what that consul could do with them: a commander the crews love, or a house with a great share of
    /// the Reach's hulls, is feared. Asking again costs, and a house that kept them once is not given them lightly.</summary>
    public static int GrantLean(World w, House voter, House target, int regardToTargetHead)
    {
        if (voter.Id == target.Id) return 5;
        int lean = Gravity(w) - Reluctance + Math.Clamp(regardToTargetHead / 20, -3, 3);
        if (FactionSystem.Of(w, voter.Id) is { } mine && FactionSystem.Of(w, target.Id)?.Id == mine.Id) lean += 2;
        if (CaesarSystem.GeneralOf(w, target) is { } g && CaesarSystem.Sway(w, g) >= CaesarSystem.AlarmSway) lean -= 3;
        if (Hulls(w, target.Id) * 100 / AllHulls(w) >= 40) lean -= 2;
        lean -= (w.Emergency?.HouseId == target.Id ? (w.Emergency.Renewals + 1) * 2 : 0) + (w.Counter(KeptOnceKey(target.Id)) > 0 ? 4 : 0);
        return lean;
    }

    public static void GrantVoted(World w, House target, House? proposer, OfficeCatalog offices, bool passed)
    {
        bool renewal = w.Emergency?.HouseId == target.Id;
        if (!passed)
        {
            Bump(w, RefusedKey);
            if (renewal) { w.Emergency!.Due = true; w.Log($"The Senate will not extend the powers. {w.Char(w.Emergency.HolderId)?.Name} is to lay them down."); if (Mine(w, target.Id)) Mark(w, DueFlag); }
            return;
        }
        if (renewal)
        {
            var e = w.Emergency!; e.Renewals++; e.Until = w.Turn + Years; e.Due = false; e.KeptYears = 0; Bump(w, RenewedKey);
            w.Log($"The Senate extends the emergency powers of {w.Char(e.HolderId)?.Name} of {target.Name} to year {e.Until}.");
            return;
        }
        var consul = ConsulOf(w, offices, target.Id); if (consul == null || w.Emergency != null) return;
        string cause = Danger(w) ?? "the state of the Reach";
        w.Emergency = new EmergencyPowers { HolderId = consul.Id, HouseId = target.Id, Cause = cause, Since = w.Turn, Until = w.Turn + Years };
        consul.Flags.Add(HoldsChar); Bump(w, GrantedKey); Mark(w, GrantedFlag); if (Mine(w, target.Id)) w.WorldFlags.Add(MineFlag);
        LifeSystem.Note(w, consul, "office", $"Was voted the Senate's emergency powers, for {cause}.");
        w.Log($"THE SENATE VOTES EMERGENCY POWERS to {consul.Name} of {target.Name}, for {cause}, until year {w.Turn + Years}. The other consul's veto is set aside, and no election will be held while they run.");
    }

    /// <summary>May this house ask the Senate that its consul hold the powers for life? Only the one who holds them, only after a first
    /// term, and only the player: no other house ends the Republic (docs/PROCESS.md, the second rule).</summary>
    public static string? ForLifeRefusal(World w, House? target)
    {
        if (target == null) return "no such house";
        if (w.IsImperium) return "there is no Senate to vote them";
        if (w.Emergency is not { } e || e.HouseId != target.Id) return "only the one who holds the emergency powers can ask to keep them";
        if (!Mine(w, target.Id)) return "no other house would put that to the Senate";
        return e.Renewals >= 1 || e.KeptYears >= 1 || e.Due ? null : "not in a first term: the Senate would hear it as a threat";
    }

    /// <summary>How a house leans on the powers for life. Against, to begin with. Regard and party count; so does fear, the other way
    /// round this time: a holder with a great share of the Reach's hulls is not easily refused. A Reach that has stopped believing in its
    /// magistrates, or is still in danger, gives way sooner. Every year they were kept without leave is remembered.</summary>
    public static int ForLifeLean(World w, House voter, House target, int regardToTargetHead)
    {
        if (voter.Id == target.Id) return 6;
        int lean = -5 + Math.Clamp(regardToTargetHead / 15, -4, 4);
        if (FactionSystem.Of(w, voter.Id) is { } mine && FactionSystem.Of(w, target.Id)?.Id == mine.Id) lean += 2;
        int share = Hulls(w, target.Id) * 100 / AllHulls(w); lean += share >= 40 ? 3 : share >= 25 ? 1 : 0;
        if (Danger(w) != null) lean += 2;
        if (LadderSystem.Trust(w) < LadderSystem.TrustLow) lean += 1;
        return lean - (w.Emergency?.KeptYears ?? 0);
    }

    public static void ForLifeVoted(World w, House target, bool passed)
    {
        var e = w.Emergency; if (e == null || e.HouseId != target.Id) return; var holder = w.Char(e.HolderId);
        if (!passed)
        {
            Bump(w, ForLifeRefusedKey);
            w.Log($"The Senate refuses {holder?.Name ?? target.Name} the powers for life, and in the same breath declares the emergency ended.");
            Strip(w); return;
        }
        Bump(w, ByVoteKey); Bump(w, "republics_ended"); w.WorldFlags.Add(CaesarSystem.RepublicFallenFlag); w.WorldFlags.Add(ForLifeFlag);
        w.Log($"THE SENATE VOTES {(holder?.Name ?? target.Name).ToUpperInvariant()} THE EMERGENCY POWERS FOR LIFE. Nobody is made to do it. The Republic ends by its own vote.");
        if (holder != null) { holder.Flags.Remove(HoldsChar); LifeSystem.Note(w, holder, "head", "Was voted the emergency powers for life. The Republic ended that day."); }
        w.Emergency = null; w.WorldFlags.Remove(MineFlag);
        ThroneSystem.Enthrone(w, target.Id, seized: false, holder?.Id);
    }

    // ---- ending ----

    private static void End(World w)
    {
        var e = w.Emergency; if (e == null) return;
        if (w.Char(e.HolderId) is { } holder) holder.Flags.Remove(HoldsChar);
        if (e.SkippedElection) w.WorldCounters[ElectionSystem.OwedKey] = 1;
        w.WorldCounters[EndedKey] = w.Turn; w.Emergency = null; w.WorldFlags.Remove(MineFlag);
    }

    public static string? LayDownRefusal(World w, Character? me) =>
        me?.Alive != true || w.Emergency is not { } e || e.HolderId != me.Id ? "you do not hold the emergency powers" : null;

    /// <summary>The holder lays them down: the house's standing rises, the Reach believes a little more in its magistrates, and the heads
    /// of the other houses think better of one who gave back what could have been kept. More so if it is done before it is due.</summary>
    public static void LayDown(World w)
    {
        var e = w.Emergency; if (e == null) return; var holder = w.Char(e.HolderId); var house = w.House(e.HouseId);
        bool early = !e.Due && e.KeptYears == 0, late = e.KeptYears > 0;
        if (house != null && !late) house.Legitimacy = Math.Min(100, house.Legitimacy + LaidDownStanding + (early ? 2 : 0));
        if (!late) Trust(w, LaidDownTrust);
        if (holder != null)
        {
            if (!late) { RegardOfAll(w, holder, LaidDownRegard); holder.Flags.Add(LaidDownChar); }
            LifeSystem.Note(w, holder, "office", late ? "Laid down the emergency powers, late." : $"Laid down the emergency powers{(early ? " before the term was up" : "")}.");
        }
        w.Log($"{holder?.Name ?? "The consul"} of {house?.Name} lays down the emergency powers{(early ? " before the term is up" : late ? ", late" : "")}. The Republic has its two consuls again.");
        Bump(w, LaidDownKey); Mark(w, Mine(w, e.HouseId) ? LaidDownMineFlag : LaidDownFlag); End(w);
    }

    /// <summary>Taken away: by the Senate refusing them for life, or by time after they were kept too long.</summary>
    public static void Strip(World w)
    {
        var e = w.Emergency; if (e == null) return; var holder = w.Char(e.HolderId); var house = w.House(e.HouseId);
        if (house != null) { house.Legitimacy = Math.Max(0, house.Legitimacy + StrippedStanding); w.WorldCounters[KeptOnceKey(house.Id)] = w.Turn; }
        if (holder != null) { RegardOfAll(w, holder, StrippedRegard); LifeSystem.Note(w, holder, "failure", "Was stripped of the emergency powers by the Senate."); }
        w.Log($"The emergency powers of {holder?.Name ?? "the consul"} of {house?.Name} are ended by the Senate, not by {(holder?.Sex == "female" ? "her" : "his")} own hand.");
        Bump(w, StrippedKey); if (Mine(w, e.HouseId)) Mark(w, StrippedFlag); End(w);
    }

    public static string? LevyRefusal(World w, Character? me)
    {
        if (me?.Alive != true || w.Emergency is not { } e || e.HolderId != me.Id) return "you do not hold the emergency powers";
        if (e.LevyTurn == w.Turn) return "the houses have been levied this year";
        return Danger(w) == null ? "there is no war to levy for" : null;
    }

    /// <summary>A levy without a vote: every other house of the Reach pays into the holder's house for the fleets. Its head thinks the
    /// worse of the holder, and the Reach a little less of its magistrates.</summary>
    public static int Levy(World w)
    {
        var e = w.Emergency; if (e == null) return 0; var holder = w.Char(e.HolderId); var house = w.House(e.HouseId); if (holder == null || house == null) return 0;
        int raised = 0; string? mine = w.Protagonist?.HouseId;
        foreach (var h in w.Houses.Values.Where(h => h.Id != house.Id && !World.Outsider(h.Id) && PolitySystem.InReach(w, h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            int pay = Math.Clamp(h.Treasury.Credits, 0, LevyCredits); h.Treasury.Credits -= pay; raised += pay;
            if (Economy.HeadOf(w, h) is { } head) { var r = w.Relationship(head.Id, holder.Id); r.Disposition = Math.Clamp(r.Disposition + LevyRegard, -100, 100); }
            if (h.Id == mine && pay > 0) Mark(w, LeviedFlag);
        }
        house.Treasury.Credits += raised; e.LevyTurn = w.Turn; Trust(w, -1); Bump(w, LeviesKey);
        w.Log($"{holder.Name} levies on the houses by the emergency powers, without a vote: {raised} credits for the fleets.");
        return raised;
    }

    // ---- the veto ----

    /// <summary>While the powers run the holder may strike one motion a year and the other consul none.</summary>
    public static bool? MayVeto(World w, string houseId) => w.Emergency is not { } e ? null : e.HouseId == houseId && e.VetoTurn != w.Turn;
    public static void Vetoed(World w, string houseId) { if (w.Emergency is { } e && e.HouseId == houseId) e.VetoTurn = w.Turn; }

    // ---- the year ----

    /// <summary>Once a year. The powers end with the Republic, with the holder, or with the consulship. When the term is up or the danger
    /// past they are due: another house's consul lays them down at once, and the player is told. Held past that without leave they cost
    /// trust, standing and regard each year, and after three years the Senate takes them. Another house's consul levies every other
    /// year while the danger lasts.</summary>
    public static void Tick(World w, OfficeCatalog offices)
    {
        foreach (var flag in YearFlags) if (w.WorldFlags.Contains(flag) && w.Counter("mark:" + flag) < w.Turn - 1) { w.WorldFlags.Remove(flag); w.WorldCounters.Remove("mark:" + flag); }
        bool lay = w.WorldFlags.Remove(LayFlag);
        if (Danger(w) != null) Bump(w, DangerYearsKey);
        var e = w.Emergency; if (e == null) return;
        var holder = w.Char(e.HolderId);
        if (w.IsImperium) { End(w); return; }
        if (holder?.Alive != true || ConsulOf(w, offices, e.HouseId)?.Id != e.HolderId)
        { w.Log($"The emergency powers lapse: {holder?.Name ?? "the consul"} no longer holds the chair they were given to."); End(w); return; }
        if (lay) { LayDown(w); return; }
        Bump(w, YearsKey); bool player = holder.Id == w.ProtagonistId;
        if (!e.Due && (w.Turn >= e.Until || Danger(w) == null))
        {
            e.Due = true;
            if (!player) { LayDown(w); return; }
            Mark(w, DueFlag); w.Log($"{(Danger(w) == null ? "The danger is past" : "The term the Senate gave is up")}: the emergency powers are to be laid down.");
            return;
        }
        if (e.Due)
        {
            if (!player) { LayDown(w); return; }
            if (w.Motions.Any(m => m.Open && m.Kind is "emergency" or "for_life" && m.TargetId == e.HouseId)) return;   // the Senate is being asked: this year is not held against them
            e.KeptYears++; Bump(w, KeptKey); Trust(w, KeptTrust); RegardOfAll(w, holder, KeptRegard);
            if (w.House(e.HouseId) is { } house) { house.Legitimacy = Math.Max(0, house.Legitimacy + KeptStanding); w.WorldCounters[KeptOnceKey(house.Id)] = w.Turn; }
            Mark(w, KeptFlag); w.Log($"{holder.Name} keeps the emergency powers, a year past the Senate's leave. It is noticed.");
            if (e.KeptYears >= KeptMax) { w.Log("The consuls-elect are sworn all the same, and the lictors do not come when they are sent for."); Strip(w); }
            return;
        }
        if (!player && Danger(w) != null && (w.Turn - e.Since) % 2 == 1 && e.LevyTurn != w.Turn) Levy(w);
    }

    /// <summary>The house that would move it for a consul, some year the Reach is in danger: the consul's own, or a house of standing that
    /// thinks well of them. Never the player's house, and never for the player.</summary>
    public static (House mover, House target)? WouldMove(World w, OfficeCatalog offices, Func<House, bool> can)
    {
        // for a war that is for the Reach itself, one year in four; for a lesser danger, a third as often
        if (w.Emergency != null || Danger(w) == null || Hash($"emergency:{w.Turn}:{w.RngState % 9973}") / 7 % (MoveOneIn * (Gravity(w) >= 3 ? 1 : LesserOneIn)) != 0) return null;
        string? mine = w.Protagonist?.HouseId;
        var consuls = ElectionSystem.ConsulOffices(offices).Select(d => OfficeSystem.HolderHouse(w, d.Id)).Where(id => id != null && id != mine).Select(id => w.House(id!)).Where(h => h != null).Select(h => h!)
            .OrderByDescending(h => h.SphereInfluence.GetValueOrDefault("senate")).ThenBy(h => h.Id, StringComparer.Ordinal).ToList();
        foreach (var t in consuls)
        {
            if (GrantRefusal(w, t, offices) != null) continue;
            var mover = can(t) ? t : w.Houses.Values.Where(h => h.Id != mine && can(h) && Economy.HeadOf(w, t) is { } th && Economy.HeadOf(w, h) is { } hh && (w.GetRelationship(hh.Id, th.Id)?.Disposition ?? 0) >= 10)
                .OrderByDescending(h => h.SphereInfluence.GetValueOrDefault("senate")).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
            if (mover != null) return (mover, t);
        }
        return null;
    }
}
