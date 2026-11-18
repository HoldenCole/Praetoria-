using Praetoria.Core.Commands;
using Praetoria.Core.Progression;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>A throne is held, not owned (docs/THRONE.md, stage 1; the owner's word of 2026-11-17 and 18, docs/GAPS.md R5). Whoever sits on
/// it has a hold, 0 to 100, made of things that can be seen. When the hold is weak for long enough a house of rank raises its standard:
/// a war for the throne, in which other houses take a side. It is decided as a seizure is, on the terms a player must meet. The house that
/// is cast down drops a rung, keeps a claim, and plays on. This applies to a player who is emperor as to anyone. RNG-free.</summary>
public static partial class ThroneSystem
{
    public const string RisenFlag = "pretender_risen", AgainstMeFlag = "pretender_against_me", SideAskedFlag = "throne_side_asked", BrokenFlag = "pretender_broken", WonFlag = "pretender_won";
    public const string CastDownFlag = "cast_down", WeakFlag = "throne_weak", ForPretenderFlag = "throne_answer_pretender", ForThroneFlag = "throne_answer_throne";
    public const string PretenderChar = "pretender", BrokenChar = "broken_pretender";
    public const string RisenKey = "pretenders_risen", WonKey = "pretenders_won", BrokenKey = "pretenders_broken", WeakYearsKey = "throne_weak_years", LastRisingKey = "last_rising", HowKey = "throne_how", WeakTotalKey = "throne_years_weak", HoldSumKey = "throne_hold_sum", HoldYearsKey = "throne_hold_years";
    private static readonly string[] YearFlags = { RisenFlag, AgainstMeFlag, SideAskedFlag, BrokenFlag, WonFlag, CastDownFlag, WeakFlag };

    public const int HoldBase = 50, FirmAt = 70, ShakenAt = 45, RiseAt = 35, WeakYears = 2, RestYears = 10, PretenderYears = 8, CrediblePct = 60, SpentPct = 40, BreakLead = -5, BrokenStanding = 10, HeldStanding = 3, SideAt = 40;
    public const int BackedRegard = 20, CrossedRegard = -30;

    private static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }
    private static void Bump(World w, string key, int by = 1) => w.WorldCounters[key] = w.Counter(key) + by;
    private static int Hulls(World w, string houseId) => FleetSystem.StrengthOf(w, houseId);
    private static int Regard(World w, Character? from, Character? to) => from == null || to == null ? 0 : w.GetRelationship(from.Id, to.Id)?.Disposition ?? 0;
    private static void Move(World w, Character? from, Character? to, int by)
    { if (from == null || to == null || from.Id == to.Id) return; var r = w.Relationship(from.Id, to.Id); r.Disposition = Math.Clamp(r.Disposition + by, -100, 100); }

    // ---- the hold ----

    /// <summary>What the hold on the throne is made of, each with its points. Empty when no throne stands.</summary>
    public static List<(string what, int points)> HoldParts(World w)
    {
        var parts = new List<(string, int)>(); var e = Emperor(w); if (e == null) return parts;
        var head = Economy.HeadOf(w, e);
        parts.Add(("the standing of the house", (e.Legitimacy - 50) / 2));
        int all = Math.Max(1, w.Fleets.Values.Where(f => !World.Outsider(f.OwnerId)).Sum(f => f.Strength)), share = Hulls(w, e.Id) * 100 / all;
        parts.Add(($"{share} in a hundred of the Reach's hulls", share >= 40 ? 15 : share >= 25 ? 8 : share >= 15 ? 0 : share >= 8 ? -10 : -15));
        string? capital = w.Galaxy is { Capital.Length: > 0 } g ? w.PlanetHolder(g.Capital) : e.Id;
        if (capital == e.Id) parts.Add(("holds the capital", 10)); else if (capital != null) parts.Add(("the capital is in another's hands", -15));
        int years = w.WorldCounters.TryGetValue(SinceKey, out int since) ? w.Turn - since : 20;
        parts.Add(years < 5 ? ("new to the throne", -5) : ($"{years} years on the throne", Math.Min(10, years / 2)));
        if (w.Counter(HowKey) == 1 && years < 10) parts.Add(("took it by force", -10));
        if (head == null) parts.Add(("nobody of the house to sit on it", -20)); else if (head.Age < DynastySystem.AgeOfMajority) parts.Add(("a child on the throne", -15));
        int away = w.Polities.Count; if (away > 0) parts.Add(($"{away} state{(away == 1 ? "" : "s")} broken away", -Math.Min(15, 5 * away)));
        int blood = FeudSystem.Open(w).Where(f => f.OwingId == e.Id).Select(f => f.WrongedId).Distinct().Count(); if (blood > 0) parts.Add(($"blood owed to {blood} house{(blood == 1 ? "" : "s")}", -Math.Min(9, 3 * blood)));
        if (w.Wars.Any(x => x.Involves(e.Id) && x.Lead(e.Id) <= -5)) parts.Add(("losing a war", -10));
        if (w.Fleets.Values.Any(f => f.OwnerId == e.Id && f.Loyalty < 30)) parts.Add(("crews that will not sail", -5));
        if (e.Treasury.Credits < 0) parts.Add(("an empty treasury", -5));
        return parts;
    }
    public static int Hold(World w) => Emperor(w) == null ? 0 : Math.Clamp(HoldBase + HoldParts(w).Sum(p => p.points), 0, 100);
    public static string HoldName(int hold) => hold >= FirmAt ? "firm" : hold >= ShakenAt ? "holds" : hold >= RiseAt ? "shaken" : "failing";

    // ---- raising the standard ----

    /// <summary>May this house raise its standard against the throne? A house of ducal rank or one with a claim, with a grown head and a
    /// fleet worth the name beside the throne's.</summary>
    public static string? RiseRefusal(World w, House? house, TitleCatalog titles)
    {
        if (house == null) return "no house";
        var e = Emperor(w); if (!w.IsImperium || e == null) return "no throne stands";
        if (house.Id == e.Id) return "you hold the throne";
        if (w.Rising != null) return w.Rising.HouseId == house.Id ? "your standard is raised already" : $"{w.House(w.Rising.HouseId)?.Name} has raised its standard: take a side";
        if (titles.RankOf(house.Title) < titles.RankOf(Title.Duke) && !house.Claims.Contains(Title.Emperor)) return $"a {Title.StyleOf(Title.Duke, w.Era)} at least, or a house with a claim";
        var head = Economy.HeadOf(w, house); if (head == null || head.Age < DynastySystem.AgeOfMajority) return "the house has nobody grown to lead it";
        if (w.Counter(LastRisingKey) > 0 && w.Turn - w.Counter(LastRisingKey) < RestYears) return "the last pretender is too lately dead: nobody would follow another yet";
        int mine = Hulls(w, house.Id), theirs = Hulls(w, e.Id);
        return mine >= 3 && mine * 100 >= theirs * CrediblePct ? null : $"nobody would follow so small a fleet against the throne's ({mine} hulls to {theirs})";
    }

    /// <summary>The standard is raised: a war with the house on the throne, and every other house but the player's weighs the two heads
    /// against each other, with any blood between them, and goes over, stands by the throne, or stands aside. The player is asked.</summary>
    public static void Rise(World w, House house, FleetSystem fleets)
    {
        var e = Emperor(w)!; var head = Economy.HeadOf(w, house); var throne = Economy.HeadOf(w, e); string? mine = w.Protagonist?.HouseId;
        var r = new ThroneRising { HouseId = house.Id, EmperorId = e.Id, Since = w.Turn, HoldThen = Hold(w) }; w.Rising = r;
        void War(string a, string b) { if (w.AtWar(a, b)) return; fleets.DeclareWar(w, a, b); r.Wars.Add(a + "|" + b); }
        w.Log($"{head?.Name ?? house.Name} of {house.Name} RAISES A STANDARD AGAINST THE THRONE. {e.Name}'s hold is {HoldName(r.HoldThen)}, and the houses of the Reach must choose.");
        War(house.Id, e.Id);
        foreach (var h in w.Houses.Values.Where(h => h.Id != house.Id && h.Id != e.Id && h.Id != mine && !World.Outsider(h.Id) && PolitySystem.InReach(w, h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            var hh = Economy.HeadOf(w, h); if (hh == null) continue;
            int lean = SideLean(w, h, house, e);
            if (lean >= SideAt) { r.With.Add(h.Id); War(h.Id, e.Id); } else if (lean <= -SideAt) { r.Against.Add(h.Id); War(h.Id, house.Id); }
        }
        if (r.With.Count + r.Against.Count > 0) w.Log($"{(r.With.Count > 0 ? string.Join(", ", r.With.Select(id => w.House(id)?.Name)) + " go over to the pretender. " : "")}{(r.Against.Count > 0 ? string.Join(", ", r.Against.Select(id => w.House(id)?.Name)) + " stand by the throne." : "")}");
        head?.Flags.Add(PretenderChar); Bump(w, RisenKey); w.WorldCounters[LastRisingKey] = w.Turn; Mark(w, RisenFlag);
        if (e.Id == mine) Mark(w, AgainstMeFlag); else if (house.Id != mine && mine != null) Mark(w, SideAskedFlag);
        if (head != null) LifeSystem.Note(w, head, "office", $"Raised a standard against the throne of {e.Name}.");
    }

    /// <summary>How a house leans between the pretender (above zero) and the throne (below): what its head thinks of each of theirs, and
    /// any blood it is owed by either.</summary>
    public static int SideLean(World w, House h, House pretender, House emperor)
    {
        var hh = Economy.HeadOf(w, h);
        int lean = Regard(w, hh, Economy.HeadOf(w, pretender)) - Regard(w, hh, Economy.HeadOf(w, emperor));
        if (FeudSystem.Open(w).Any(f => f.WrongedId == h.Id && f.OwingId == emperor.Id)) lean += 20;
        if (FeudSystem.Open(w).Any(f => f.WrongedId == h.Id && f.OwingId == pretender.Id)) lean -= 20;
        return lean;
    }

    /// <summary>May the player's house take a side in a rising that is not its own?</summary>
    public static string? BackRefusal(World w, House? house, string side)
    {
        var r = w.Rising; if (house == null || r == null) return "nobody has raised a standard";
        if (house.Id == r.HouseId || house.Id == Emperor(w)?.Id) return "it is your own war";
        if (r.With.Contains(house.Id) || r.Against.Contains(house.Id)) return "your house has chosen";
        return side is ThroneCommand.Pretender or ThroneCommand.Throne ? null : "no such side";
    }

    /// <summary>The house goes over to the pretender or stands by the throne: at war with the other, thanked by the one and marked by the other.</summary>
    public static void Back(World w, House house, string side, FleetSystem fleets)
    {
        var r = w.Rising!; var e = Emperor(w); var p = w.House(r.HouseId); if (e == null || p == null) return; var head = Economy.HeadOf(w, house);
        bool forPretender = side == ThroneCommand.Pretender; var friend = forPretender ? p : e; var foe = forPretender ? e : p;
        (forPretender ? r.With : r.Against).Add(house.Id);
        if (!w.AtWar(house.Id, foe.Id)) { fleets.DeclareWar(w, house.Id, foe.Id); r.Wars.Add(house.Id + "|" + foe.Id); }
        Move(w, Economy.HeadOf(w, friend), head, BackedRegard); Move(w, Economy.HeadOf(w, foe), head, CrossedRegard);
        w.Log($"{house.Name} {(forPretender ? $"goes over to {p.Name}" : $"stands by the throne of {e.Name}")}.");
        if (head != null) LifeSystem.Note(w, head, "office", forPretender ? $"Went over to {p.Name} against the throne." : $"Stood by the throne of {e.Name} against {p.Name}.");
    }

    /// <summary>Is this one of the wars of a rising that is still in the field? Such a war is not ended by treaty.</summary>
    public static bool ForTheThrone(World w, string a, string b) => w.Rising is { } r && (r.Wars.Contains(a + "|" + b) || r.Wars.Contains(b + "|" + a));

    // ---- how it ends ----

    private static void EndWars(World w, ThroneRising r)
    {
        foreach (var pair in r.Wars) { var ab = pair.Split('|'); w.Wars.RemoveAll(x => x.Between(ab[0], ab[1])); }
        FleetSystem.RefreshWarFlags(w); if (w.Char(Economy.HeadOf(w, w.House(r.HouseId) ?? new House())?.Id ?? "") is { } head) head.Flags.Remove(PretenderChar);
        foreach (var c in w.LivingCharacters().Where(c => c.HouseId == r.HouseId)) c.Flags.Remove(PretenderChar);
        w.Rising = null;
    }

    /// <summary>The pretender is broken: the wars end, the house loses standing and any claim, its head is marked, those who went over
    /// with it are remembered by the throne, and the throne is the firmer for having been tried.</summary>
    public static void Break(World w, string why, string reason = "other")
    {
        Bump(w, "pretender_broken_by:" + reason);
        var r = w.Rising; if (r == null) return; var p = w.House(r.HouseId); var e = Emperor(w); var head = p != null ? Economy.HeadOf(w, p) : null; var throne = e != null ? Economy.HeadOf(w, e) : null;
        if (p != null) { p.Legitimacy = Math.Max(0, p.Legitimacy - BrokenStanding); p.Claims.Remove(Title.Emperor); }
        if (e != null) e.Legitimacy = Math.Min(100, e.Legitimacy + HeldStanding);
        if (head != null) { head.Flags.Add(BrokenChar); LifeSystem.Note(w, head, "failure", "Raised a standard against the throne, and was broken."); }
        foreach (var id in r.With) if (w.House(id) is { } with) Move(w, throne, Economy.HeadOf(w, with), CrossedRegard);
        w.Log($"The rising of {p?.Name ?? "the pretender"} is broken: {why}. {e?.Name ?? "The throne"} holds.");
        Bump(w, BrokenKey); Mark(w, BrokenFlag); EndWars(w, r);
    }

    private static void Won(World w, ThroneRising r)
    {
        var p = w.House(r.HouseId); var old = w.House(r.EmperorId); var head = p != null ? Economy.HeadOf(w, p) : null;
        foreach (var id in r.Against) if (w.House(id) is { } stood) Move(w, head, Economy.HeadOf(w, stood), CrossedRegard);
        foreach (var id in r.With) if (w.House(id) is { } with) Move(w, head, Economy.HeadOf(w, with), BackedRegard);
        w.Log($"The throne has a new house on it. {old?.Name ?? "The old house"} is cast down a rung and keeps a claim; it has not gone anywhere.");
        Bump(w, WonKey); Mark(w, WonFlag); EndWars(w, r);
    }

    // ---- the year ----

    /// <summary>Once a year under a throne. The hold is reckoned. A rising that is under way is carried on: the pretender takes the throne
    /// the year the terms of a seizure are met (a player who is the pretender does that for themselves), and is broken if the war is lost,
    /// the fleet spent, or eight years gone. With none under way, two years of a failing hold bring one: the strongest house that may
    /// rise and has no love for the throne. Never the player's house: that is the player's to do.</summary>
    public static void Tick(World w, TitleCatalog titles, FleetSystem fleets)
    {
        foreach (var flag in YearFlags) if (w.WorldFlags.Contains(flag) && w.Counter("mark:" + flag) < w.Turn - 1) { w.WorldFlags.Remove(flag); w.WorldCounters.Remove("mark:" + flag); }
        bool forP = w.WorldFlags.Remove(ForPretenderFlag), forT = w.WorldFlags.Remove(ForThroneFlag);
        var e = Emperor(w); string? mine = w.Protagonist?.HouseId;
        if (!w.IsImperium) { if (w.Rising != null) EndWars(w, w.Rising); return; }
        if (e == null)
        {
            // an empty throne is filled: by the pretender if there is one, and otherwise by the strongest house of rank or with a claim
            var heir = w.Rising != null ? w.House(w.Rising.HouseId) : w.Houses.Values.Where(h => h.Id != mine && !World.Outsider(h.Id) && PolitySystem.InReach(w, h.Id) && Economy.HeadOf(w, h) != null
                    && (titles.RankOf(h.Title) >= titles.RankOf(Title.Duke) || h.Claims.Contains(Title.Emperor)))
                .OrderByDescending(h => Hulls(w, h.Id)).ThenByDescending(h => h.Legitimacy).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
            if (heir == null) return;
            w.Log($"The throne stands empty, and {heir.Name} takes it.");
            Enthrone(w, heir.Id, seized: w.Rising != null, Economy.HeadOf(w, heir)?.Id);
            if (w.Rising is { } filled) Won(w, filled);
            return;
        }
        int hold = Hold(w); Bump(w, HoldSumKey, hold); Bump(w, HoldYearsKey);
        if (hold < ShakenAt) { Bump(w, WeakTotalKey); if (e.Id == mine) Mark(w, WeakFlag); }
        if (w.Rising is { } r)
        {
            var p = w.House(r.HouseId); var head = p != null ? Economy.HeadOf(w, p) : null;
            if (e.Id == r.HouseId) { Won(w, r); return; }                                        // a player who was the pretender has seized it
            if (e.Id != r.EmperorId) { EndWars(w, r); return; }                                  // someone else took the throne meanwhile
            if (p == null || head == null) { Break(w, "there is nobody left to lead it"); return; }
            if (mine != null && (forP || forT) && BackRefusal(w, w.House(mine), forP ? ThroneCommand.Pretender : ThroneCommand.Throne) == null) Back(w, w.House(mine)!, forP ? ThroneCommand.Pretender : ThroneCommand.Throne, fleets);
            if (p.Id != mine && SeizeRefusal(w, p.Id, titles) == null) { Enthrone(w, p.Id, seized: true, head.Id); Won(w, r); return; }
            var war = w.Wars.FirstOrDefault(x => x.Between(p.Id, e.Id));
            if (war == null) { Break(w, "its war ended without the throne", "terms"); return; }
            if (w.Turn - r.Since >= 2 && war.Lead(p.Id) <= BreakLead) { Break(w, "the war went against it", "war"); return; }
            if (Hulls(w, p.Id) * 100 < Hulls(w, e.Id) * SpentPct) { Break(w, "its fleet is spent", "spent"); return; }
            if (w.Turn - r.Since >= PretenderYears) { Break(w, $"{PretenderYears} years and no nearer the capital", "years"); return; }
            return;
        }
        if (hold >= RiseAt) { w.WorldCounters.Remove(WeakYearsKey); return; }
        Bump(w, WeakYearsKey); if (w.Counter(WeakYearsKey) < WeakYears) return;
        var throne = Economy.HeadOf(w, e);
        var riser = w.Houses.Values.Where(h => h.Id != mine && !World.Outsider(h.Id) && PolitySystem.InReach(w, h.Id) && RiseRefusal(w, h, titles) == null
                && (h.Claims.Contains(Title.Emperor) || Regard(w, Economy.HeadOf(w, h), throne) <= 10))
            .OrderByDescending(h => Hulls(w, h.Id)).ThenByDescending(h => h.Legitimacy).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
        if (riser != null) Rise(w, riser, fleets);
    }
}
