using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The account of a war by year (docs/MILITARY.md, "Commanding a navy"; docs/PLAYTHROUGH.md, Hard: "a year-end report of the war").
/// A war's year is closed when the year turns, after the battles are fought and the sieges resolved: the battles and who won them,
/// the hulls each side lost and took, the worlds and stations that changed hands, the score as the year opened and closed, and what
/// each house's fleets in commission cost it in credits and helium-3. What happens after the close (a peace and what it cedes) is
/// written into the next account. Every war between houses is kept this way, the player's or not; a war that ends goes to
/// <see cref="World.PastWars"/> with its account. Wars with the nations beyond the Reach have their own tally (docs/BEYOND.md) and
/// are not kept here. RNG-free; nothing here decides anything.
/// </summary>
public static class WarLedger
{
    /// <summary>How many ended wars are kept: of the player's house, and between other houses.</summary>
    public const int KeptOwn = 40, KeptOthers = 12;
    public const string Target = "military:wars";

    /// <summary>The last year whose account is closed: a war's first account is the year after it was declared.</summary>
    public static int Closed(War war) => Math.Max(war.ClosedThrough, war.Since);

    /// <summary>The account that what happens now is written into: this year's until the year's turn closes it, then the next.</summary>
    public static WarYear Year(World w, War war)
    {
        int turn = Closed(war) >= w.Turn ? w.Turn + 1 : w.Turn;
        if (war.Years.Count > 0 && war.Years[^1].Turn >= turn) return war.Years[^1];
        var y = new WarYear { Turn = turn, OpenA = war.ScoreA, OpenB = war.ScoreB, CloseA = war.ScoreA, CloseB = war.ScoreB };
        war.Years.Add(y); return y;
    }

    /// <summary>The accounts that are closed, oldest first.</summary>
    public static IEnumerable<WarYear> ClosedYears(War war) => war.Years.Where(y => y.Turn <= Closed(war) || war.Ended > 0);

    /// <summary>A battle of the war (call before the score is moved): who won, and what each lost.</summary>
    public static void Battle(World w, War war, string winner, int winnerLost, int loserLost)
    {
        var y = Year(w, war); y.Battles++;
        if (winner == war.HouseA) { y.WonA++; y.LostA += winnerLost; y.LostB += loserLost; } else { y.WonB++; y.LostB += winnerLost; y.LostA += loserLost; }
    }

    /// <summary>Somebody of another house commanded <paramref name="side"/>'s fleet by service in a battle of the war (docs/MILITARY.md, "A command by service").</summary>
    public static void Served(World w, War war, string side, string commanderId)
    {
        var y = Year(w, war); var list = side == war.HouseA ? y.ServedA : y.ServedB;
        if (!list.Contains(commanderId)) list.Add(commanderId);
    }

    /// <summary>Hulls taken as prizes by the boarders of <paramref name="taker"/> from <paramref name="from"/>.</summary>
    public static void Prizes(World w, string taker, string from, int hulls)
    {
        var war = w.Wars.FirstOrDefault(x => x.Between(taker, from)); if (war == null || hulls <= 0) return;
        var y = Year(w, war); if (taker == war.HouseA) y.TakenA += hulls; else y.TakenB += hulls;
    }

    /// <summary>A world or a station changed hands between two houses at war (call when it has, before the score is moved): taken by
    /// siege, or ceded at the peace. A peace made this year is still the war's: what it cedes is written into it.</summary>
    public static void Changed(World w, Holding h, string from, string to, string how)
    {
        var war = w.Wars.FirstOrDefault(x => x.Between(from, to)) ?? w.PastWars.LastOrDefault(x => x.Between(from, to) && x.Ended == w.Turn);
        if (war == null) return;
        Year(w, war).Changed.Add(new WarChange { HoldingId = h.Id, Name = h.Name, FromId = from, ToId = to, How = how, World = h.IsPlanet, Seat = !w.HoldingsOf(from).Any() });
    }

    /// <summary>A seat on a world (a province of it, docs/PROVINCES.md) passed from one house at war to the other: the house that
    /// held it had no fleet left to keep it.</summary>
    public static void Seat(World w, Holding h, string seat, string from, string to)
    {
        var war = w.Wars.FirstOrDefault(x => x.Between(from, to)); if (war == null) return;
        Year(w, war).Changed.Add(new WarChange { HoldingId = h.Id, Name = seat, FromId = from, ToId = to, How = "taken", World = false });
    }

    /// <summary>What a house's fleets in commission cost it as the year turned, in credits (hundreds) and helium-3. A house in more
    /// wars than one bears it once: it is shared between them, the odd part to the oldest.</summary>
    public static void Cost(World w, string houseId, int credits, int fuel)
    {
        var wars = w.Wars.Where(x => x.Involves(houseId)).ToList(); if (wars.Count == 0 || (credits <= 0 && fuel <= 0)) return;
        credits = Math.Max(0, credits); fuel = Math.Max(0, fuel);
        for (int i = 0; i < wars.Count; i++)
        {
            int c = credits / wars.Count + (i == 0 ? credits % wars.Count : 0), f = fuel / wars.Count + (i == 0 ? fuel % wars.Count : 0);
            var y = Year(w, wars[i]);
            if (houseId == wars[i].HouseA) { y.CreditsA += c; y.FuelA += f; y.SharedA = wars.Count > 1; } else { y.CreditsB += c; y.FuelB += f; y.SharedB = wars.Count > 1; }
        }
    }

    /// <summary>The year turns: every standing war's account for it is closed with the score as it stands, and the next is opened.
    /// Safe to call more than once in a year.</summary>
    public static void Close(World w)
    {
        foreach (var war in w.Wars)
        {
            if (Closed(war) >= w.Turn) continue;
            var y = Year(w, war); y.CloseA = war.ScoreA; y.CloseB = war.ScoreB;
            war.ClosedThrough = w.Turn; Year(w, war);
        }
    }

    /// <summary>A war is over and is struck from the standing wars by the caller: it is kept among the past wars from this moment,
    /// so that what the peace cedes can still be written into it. <see cref="Finish"/> settles it.</summary>
    public static void Archive(World w, War war)
    {
        if (war.Ended > 0 || w.PastWars.Contains(war)) return;
        war.Ended = Math.Max(1, w.Turn); war.NameA = w.House(war.HouseA)?.Name ?? war.HouseA; war.NameB = w.House(war.HouseB)?.Name ?? war.HouseB; w.PastWars.Add(war);
    }

    /// <summary>Settle a war that is over: how it ended, its last account closed, the player's house told, and the oldest of the
    /// past wars let go when there are too many.</summary>
    public static void Finish(World w, War war, string how)
    {
        Archive(w, war); war.Ending = how;
        if (war.Years.Count > 0 && war.Years[^1].Turn > Closed(war))
        {
            var last = war.Years[^1];
            if (last.Empty()) war.Years.RemoveAt(war.Years.Count - 1); else { last.CloseA = war.ScoreA; last.CloseB = war.ScoreB; }
        }
        string? mine = w.Protagonist?.HouseId;
        if (mine != null && war.Involves(mine)) w.Tell("info", Over(w, war, mine), Target, war.Other(mine));
        Trim(w, mine);
    }

    /// <summary>End every standing war that <paramref name="which"/> picks, without terms: the houses did not make peace, something
    /// else ended it (a house is no more, a throne was taken). <paramref name="how"/> is a clause that stands alone.</summary>
    public static void Strike(World w, Func<War, bool> which, string how)
    {
        foreach (var war in w.Wars.Where(which).ToList()) { w.Wars.Remove(war); Finish(w, war, how); }
    }

    private static void Trim(World w, string? mine)
    {
        bool Own(War x) => mine != null && x.Involves(mine);
        while (w.PastWars.Count(Own) > KeptOwn) w.PastWars.Remove(w.PastWars.First(Own));
        while (w.PastWars.Count(x => !Own(x)) > KeptOthers) w.PastWars.Remove(w.PastWars.First(x => !Own(x)));
    }

    // ---------------- in words ----------------

    public static string Words(int n) => n switch { 0 => "no", 1 => "one", 2 => "two", 3 => "three", 4 => "four", 5 => "five", 6 => "six", 7 => "seven", 8 => "eight", 9 => "nine", 10 => "ten", 11 => "eleven", 12 => "twelve", _ => n.ToString() };
    public static string Nth(int n) => n switch { 1 => "first", 2 => "second", 3 => "third", 4 => "fourth", 5 => "fifth", 6 => "sixth", 7 => "seventh", 8 => "eighth", 9 => "ninth", 10 => "tenth", 11 => "eleventh", 12 => "twelfth",
        _ => n % 100 is 11 or 12 or 13 ? n + "th" : (n % 10) switch { 1 => n + "st", 2 => n + "nd", 3 => n + "rd", _ => n + "th" } };
    /// <summary>A house's name, or the name it had when the war ended if it is no more.</summary>
    private static string Nm(World w, War war, string id) => w.House(id)?.Name ?? (id == war.HouseA && war.NameA.Length > 0 ? war.NameA : id == war.HouseB && war.NameB.Length > 0 ? war.NameB : id);

    /// <summary>The side a reader stands on: their own house if the war is theirs, else the first of the two.</summary>
    private static string Side(War war, string viewer) => war.Involves(viewer) ? viewer : war.HouseA;

    /// <summary>The whole of a war in one account: its years added together.</summary>
    public static WarYear Whole(War war)
    {
        var t = new WarYear { OpenA = 0, OpenB = 0, CloseA = war.ScoreA, CloseB = war.ScoreB };
        foreach (var y in war.Years)
        {
            t.Battles += y.Battles; t.WonA += y.WonA; t.WonB += y.WonB; t.LostA += y.LostA; t.LostB += y.LostB; t.TakenA += y.TakenA; t.TakenB += y.TakenB;
            t.CreditsA += y.CreditsA; t.CreditsB += y.CreditsB; t.FuelA += y.FuelA; t.FuelB += y.FuelB; t.Changed.AddRange(y.Changed);
            foreach (var id in y.ServedA) if (!t.ServedA.Contains(id)) t.ServedA.Add(id);
            foreach (var id in y.ServedB) if (!t.ServedB.Contains(id)) t.ServedB.Add(id);
        }
        return t;
    }

    private static string BattlesSay(World w, War war, WarYear y, string a, bool mine)
    {
        int won = a == war.HouseA ? y.WonA : y.WonB, lost = y.Battles - won; string b = war.Other(a);
        if (y.Battles == 0) return "no battle was fought";
        if (mine) return y.Battles == 1 ? (won == 1 ? "one battle, and it was won" : "one battle, and it was lost") : $"{Words(y.Battles)} battles, {(won == y.Battles ? "all" : won == 0 ? "none" : Words(won))} won";
        return y.Battles == 1 ? $"one battle, which {Nm(w, war, won == 1 ? a : b)} won" : $"{Words(y.Battles)} battles, {Nm(w, war, a)} winning {(won == 0 ? "none" : Words(won))} and {Nm(w, war, b)} {(lost == 0 ? "none" : Words(lost))}";
    }

    private static string HullsSay(World w, War war, WarYear y, string a, bool mine)
    {
        bool first = a == war.HouseA; int la = first ? y.LostA : y.LostB, lb = first ? y.LostB : y.LostA, ta = first ? y.TakenA : y.TakenB, tb = first ? y.TakenB : y.TakenA; string b = war.Other(a);
        string s = "";
        if (la + lb > 0)
            s = mine ? (la == 0 ? $"You lost no hull and sank {lb}." : lb == 0 ? $"You lost {Counted.Hulls(la)} and sank none." : $"You lost {Counted.Hulls(la)} and sank {lb}.")
                     : $"{Nm(w, war, a)} lost {(la == 0 ? "no hull" : Counted.Hulls(la))} and {Nm(w, war, b)} {(lb == 0 ? "none" : lb.ToString())}.";
        if (ta > 0) s += (s.Length > 0 ? " " : "") + (mine ? $"Your prize crews took {Counted.Hulls(ta)}." : $"The prize crews of {Nm(w, war, a)} took {Counted.Hulls(ta)}.");
        if (tb > 0) s += (s.Length > 0 ? " " : "") + (mine ? $"Theirs took {Counted.Hulls(tb)} of yours." : $"The prize crews of {Nm(w, war, b)} took {Counted.Hulls(tb)}.");
        return s;
    }

    private static string ChangesSay(World w, War war, WarYear y, string a, bool mine)
    {
        var parts = new List<string>();
        foreach (var c in y.Changed)
        {
            string from = Nm(w, war, c.FromId), to = Nm(w, war, c.ToId);
            if (!mine) { parts.Add((c.How == "ceded" ? $"{from} ceded {c.Name} to {to}." : $"{to} took {c.Name} from {from}.") + (c.Seat ? $" It was the last {from} held." : "")); continue; }
            bool gained = c.ToId == a;
            parts.Add((c.How == "ceded" ? (gained ? $"{c.Name} was ceded to you." : $"{c.Name} was ceded to {to}.") : gained ? $"{c.Name} was taken." : $"{c.Name} was lost.")
                + (c.Seat ? (gained ? $" It was the last {from} held." : " It was the last you held.") : ""));
        }
        return string.Join(" ", parts);
    }

    /// <summary>Who of other houses commanded either side's fleets by service, by name and house.</summary>
    private static string ServedSay(World w, War war, WarYear y, string a, bool mine)
    {
        bool first = a == war.HouseA; var ours = first ? y.ServedA : y.ServedB; var theirs = first ? y.ServedB : y.ServedA; var parts = new List<string>();
        string Names(List<string> ids) => string.Join(" and ", ids.Select(id => w.Char(id) is { } c ? $"{c.Name} of {w.House(c.HouseId)?.Name ?? c.HouseId}" : id));
        if (ours.Count > 0) parts.Add($"{Names(ours)} commanded {(mine ? "your" : Nm(w, war, a) + "'s")} fleets by service.");
        if (theirs.Count > 0) parts.Add($"{Names(theirs)} commanded {Nm(w, war, war.Other(a))}'s by service.");
        return string.Join(" ", parts);
    }

    private static string CostSay(World w, War war, WarYear y, string a, bool mine)
    {
        bool first = a == war.HouseA; int ca = first ? y.CreditsA : y.CreditsB, fa = first ? y.FuelA : y.FuelB, cb = first ? y.CreditsB : y.CreditsA, fb = first ? y.FuelB : y.FuelA;
        string One(int c, int f) => $"{(c > 0 ? Money.Credits(c) : "no credits")} and {(f > 0 ? f.ToString() : "no")} helium-3";
        if (mine) return ca + fa == 0 ? "" : $"It cost {One(ca, fa)}{((first ? y.SharedA : y.SharedB) ? ", the keep of your fleets being shared between your wars" : "")}.";
        return ca + fa + cb + fb == 0 ? "" : $"It cost {Nm(w, war, a)} {One(ca, fa)}, and {Nm(w, war, war.Other(a))} {One(cb, fb)}.";
    }

    /// <summary>One year of a war as a paragraph, for the house that reads it (docs/PLAYTHROUGH.md, Hard): "The war with House Oriel,
    /// its third year: two battles, one won. You lost 3 hulls and sank 5. Cinder Gate was taken. The score stands 7 to 2; it was 4
    /// to 2. It cost 1,400 credits and 3 helium-3." A house that is in neither side reads both by name.</summary>
    public static string Say(World w, War war, WarYear y, string viewer, bool named = true)
    {
        bool mine = war.Involves(viewer); string a = Side(war, viewer), b = war.Other(a); bool first = a == war.HouseA;
        bool atEnd = war.Ended > 0 && y.Turn > war.Ended, latest = war.Ended == 0 && y.Turn == Closed(war);
        int n = war.Years.IndexOf(y) + 1;
        string head = named ? (mine ? $"The war with {Nm(w, war, b)}" : $"The war between {Nm(w, war, a)} and {Nm(w, war, b)}") + (atEnd ? ", at its end" : $", its {Nth(Math.Max(1, n))} year")
            : atEnd ? "At its end" : $"Year {y.Turn}, its {Nth(Math.Max(1, n))} year";
        var parts = new List<string>();
        if (!atEnd || y.Battles > 0) parts.Add(BattlesSay(w, war, y, a, mine) + ".");
        foreach (var s in new[] { HullsSay(w, war, y, a, mine), ChangesSay(w, war, y, a, mine), ServedSay(w, war, y, a, mine) }) if (s.Length > 0) parts.Add(s);
        int oa = first ? y.OpenA : y.OpenB, ob = first ? y.OpenB : y.OpenA, ca = first ? y.CloseA : y.CloseB, cb = first ? y.CloseB : y.CloseA;
        string whose = mine ? "" : $" ({Nm(w, war, a)} first)";
        parts.Add(oa == ca && ob == cb ? $"The score {(latest ? "stands" : "stood")} {ca} to {cb}{whose}, as it {(latest ? "did" : "had")}." : $"The score {(latest ? "stands" : "stood")} {ca} to {cb}{whose}; it {(latest ? "was" : "had been")} {oa} to {ob}.");
        string cost = CostSay(w, war, y, a, mine); if (cost.Length > 0) parts.Add(cost);
        return head + ": " + string.Join(" ", parts);
    }

    /// <summary>A war that is over, in a paragraph: how it ended and when, and the whole of it added up.</summary>
    public static string Over(World w, War war, string viewer)
    {
        bool mine = war.Involves(viewer); string a = Side(war, viewer), b = war.Other(a); bool first = a == war.HouseA; var t = Whole(war);
        int years = Math.Max(1, war.Ended - war.Since);
        string head = (mine ? $"The war with {Nm(w, war, b)} is over" : $"The war between {Nm(w, war, a)} and {Nm(w, war, b)} is over") + $": {(war.Ending.Length > 0 ? war.Ending : "it ended")} in year {war.Ended}, {(war.Ended <= war.Since ? "the year it began" : "after " + Counted.Years(years))}.";
        var parts = new List<string> { "In all: " + BattlesSay(w, war, t, a, mine) + "." };
        foreach (var s in new[] { HullsSay(w, war, t, a, mine), ChangesSay(w, war, t, a, mine), ServedSay(w, war, t, a, mine) }) if (s.Length > 0) parts.Add(s);
        parts.Add($"The score stood {(first ? war.ScoreA : war.ScoreB)} to {(first ? war.ScoreB : war.ScoreA)}{(mine ? "" : $" ({Nm(w, war, a)} first)")}.");
        string cost = CostSay(w, war, t, a, mine); if (cost.Length > 0) parts.Add(cost.Replace(", the keep of your fleets being shared between your wars", ""));
        return head + " " + string.Join(" ", parts);
    }

    /// <summary>A standing war in a line: since when, and how the score stands.</summary>
    public static string Standing(World w, War war, string viewer)
    {
        bool mine = war.Involves(viewer); string a = Side(war, viewer), b = war.Other(a); bool first = a == war.HouseA;
        return (mine ? $"At war with {Nm(w, war, b)}" : $"{Nm(w, war, a)} and {Nm(w, war, b)} are at war") + $" since year {war.Since}. The score stands {(first ? war.ScoreA : war.ScoreB)} to {(first ? war.ScoreB : war.ScoreA)}{(mine ? "" : $" ({Nm(w, war, a)} first)")}."
            + (war.Why.Length > 0 && mine ? $" Why it was declared: {war.Why}." : "");
    }

    /// <summary>The paragraphs for the year in brief: each standing war of the house whose account closed as this year opened.</summary>
    public static List<(string tone, string text)> Brief(World w, string houseId)
    {
        var list = new List<(string, string)>();
        foreach (var war in w.Wars.Where(x => x.Involves(houseId)))
        {
            var y = war.Years.LastOrDefault(y => y.Turn == w.Turn && y.Turn <= Closed(war)); if (y == null) continue;
            bool first = houseId == war.HouseA; int won = first ? y.WonA : y.WonB, lost = y.Battles - won;
            bool gained = y.Changed.Any(c => c.ToId == houseId), gave = y.Changed.Any(c => c.FromId == houseId);
            list.Add((gave || lost > won ? "warn" : gained || won > lost ? "good" : "info", Say(w, war, y, houseId)));
        }
        return list;
    }

    /// <summary>Every war to show on the Military window: the house's own, standing first and then ended, newest first; and the same
    /// for the wars between other houses.</summary>
    public static (List<War> own, List<War> others) History(World w, string houseId)
    {
        var all = w.Wars.OrderByDescending(x => x.Since).Concat(w.PastWars.AsEnumerable().Reverse()).ToList();
        return (all.Where(x => x.Involves(houseId)).ToList(), all.Where(x => !x.Involves(houseId)).ToList());
    }
}
