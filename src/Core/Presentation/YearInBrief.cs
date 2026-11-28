using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Presentation;

/// <summary>One line of the year in brief: what kind of thing it was, how it should be read ("good", "warn", "info"), the line, and
/// the window that shows it. <see cref="Grave"/> is how bad the news is (0 for anything that is not among the worst of the year):
/// the gravest lines stand first, in the red band (<see cref="YearInBrief.Worst"/>).</summary>
public sealed record BriefLine(string Kind, string Tone, string Text, string Target, string PersonId = "", int Grave = 0);

/// <summary>
/// The year in brief (docs/PLAYTHROUGH.md, Medium: "births, deaths, marriages, wars declared and ended, suits and fines, elections,
/// what was finished, money in and out"). Read at the start of a year, it says what the year before did to the player's house, in
/// one place, each line opening where it can be looked at. Nothing here is new: it gathers what the world already wrote down
/// (what the house was told, the battles, the rulings, the count, the weddings, and where the treasury stood). Read only.
/// </summary>
public static class YearInBrief
{
    public const string Family = "The family", War = "War", Courts = "The courts", Senate = "The Senate", Works = "Works and worlds", Money = "Money", Other = "Besides";
    public static readonly string[] Order = { Family, War, Senate, Courts, Works, Money, Other };
    /// <summary>Where the treasury stood when the year opened, and how it moved over the year: kept by the turn so the brief can say so.</summary>
    public static string OpenKey(string resource) => "year_open:" + resource;
    public static string DeltaKey(string resource) => "year_delta:" + resource;
    /// <summary>Where a good stood when the head ended the year (after their own acts, before the year turned), and what the standing ledger brought.</summary>
    public static string ActedKey(string resource) => "year_acted:" + resource;
    public static string LedgerKey(string resource) => "year_ledger:" + resource;
    public static string LastKey(string part, string resource) => $"year_last:{part}:{resource}";

    /// <summary>Called as the head ends the year: where each counted good stands now their own acts are done.</summary>
    public static void MarkActed(World w)
    {
        var house = w.Protagonist != null ? w.House(w.Protagonist.HouseId) : null; if (house == null) return;
        foreach (var r in Counted) w.WorldCounters[ActedKey(r)] = house.Treasury.Get(r);
    }

    /// <summary>One line of last year's accounts for a good: what it was, and by how much.</summary>
    public sealed record AccountLine(string What, int By, string Says);

    /// <summary>Last year's accounts for a good (docs/PLAYTHROUGH.md, Medium: "last year's accounts in the ledger, one-off lines included,
    /// so that the change in the treasury adds up"). Three parts that sum to the change: what the head did with it in the year, what the
    /// standing ledger brought, and everything else the year's turn took or gave. Null before a whole year has been played.</summary>
    public static (int open, int close, List<AccountLine> lines)? Accounts(World w, string resource)
    {
        if (!w.WorldCounters.TryGetValue(LastKey("open", resource), out int open) || !w.WorldCounters.TryGetValue(LastKey("close", resource), out int close)) return null;
        int acted = w.Counter(LastKey("acted", resource)), ledger = w.Counter(LastKey("ledger", resource));
        var lines = new List<AccountLine>
        {
            new("Your own acts in the year", acted - open, "What you bought, sold, built, gave, bribed and answered reports with, while the year was yours."),
            new("The standing ledger", ledger, "What the worlds, ventures, fleets and the rest of the ledger above brought and cost when the year turned."),
            new("The year's turn besides", close - acted - ledger, "Everything else the turn of the year took or gave: the year's shares of what is building and hastening, tutors, allowances, the household's keep and wages, the steward's and the marshal's spending, fines and damages, dowries, tribute, what was paid over to relatives for their own ventures."),
        };
        return (open, close, lines);
    }
    private static readonly string[] Counted = { Resource.Credits, Resource.Materials, Resource.Manpower, Resource.Helium3 };

    /// <summary>Called as a year opens: what each counted good did over the year just ended, and where it stands now.</summary>
    public static void Mark(World w)
    {
        var house = w.Protagonist != null ? w.House(w.Protagonist.HouseId) : null; if (house == null) return;
        // the odd coin beside the hundreds, so that the year's account of credits is right to the single credit (docs/MONEY.md)
        if (w.WorldCounters.TryGetValue(OpenKey(Resource.Coin), out int coinOpen)) w.WorldCounters[DeltaKey(Resource.Coin)] = house.Treasury.Coin - coinOpen; else w.WorldCounters.Remove(DeltaKey(Resource.Coin));
        w.WorldCounters[OpenKey(Resource.Coin)] = house.Treasury.Coin;
        foreach (var r in Counted)
        {
            int now = house.Treasury.Get(r);
            if (w.WorldCounters.TryGetValue(OpenKey(r), out int open))
            {
                w.WorldCounters[DeltaKey(r)] = now - open;
                /* last year's accounts: where it opened and closed, where it stood when the head ended the year, and what the ledger brought */
                w.WorldCounters[LastKey("open", r)] = open; w.WorldCounters[LastKey("close", r)] = now;
                w.WorldCounters[LastKey("acted", r)] = w.WorldCounters.TryGetValue(ActedKey(r), out int acted) ? acted : open;
                w.WorldCounters[LastKey("ledger", r)] = w.Counter(LedgerKey(r));
            }
            else w.WorldCounters.Remove(DeltaKey(r));
            w.WorldCounters[OpenKey(r)] = now;
        }
        // the house's rank as the year opens, and as the last one opened: a rank lost in the year is one of its moments (YearMoments)
        if (w.WorldCounters.TryGetValue(OpenKey(RankKey), out int rankWas)) w.WorldCounters[LastKey("open", RankKey)] = rankWas; else w.WorldCounters.Remove(LastKey("open", RankKey));
        w.WorldCounters[OpenKey(RankKey)] = Progression.Title.RankOf(house.Title);
    }
    /// <summary>The key the house's rank is kept under as each year opens (<see cref="OpenKey"/>, and <see cref="LastKey"/> with "open").</summary>
    public const string RankKey = "rank";

    /// <summary>The lines for the year that has just ended (the world is at the start of the next).</summary>
    public static List<BriefLine> For(World w, ContentDatabase content)
    {
        var lines = new List<BriefLine>(); var p = w.Protagonist; var house = p != null ? w.House(p.HouseId) : null; if (p == null || house == null) return lines;
        int year = w.Turn - 1; string mine = house.Id; string Nm(string id) => w.House(id)?.Name ?? id;

        // what the house was told as the year turned: births, deaths, wars declared on it, attempts, tutors, illness, dues, dowries
        var dead = w.Deaths.Where(d => d.Turn >= year).ToList();
        foreach (var n in w.NewsNow())
        {
            if (dead.Any(d => n.SubjectId == d.PersonId && n.Text.Contains("has died"))) continue;
            if (Stale(w, n)) continue;
            lines.Add(new BriefLine(KindOf(n.Target), n.Tone, n.Text, n.Target, n.SubjectId));
        }
        // a death is a report of its own: who and how, each post it left empty (the line opens the place it is filled), and what passed
        foreach (var d in dead)
        {
            lines.Add(new BriefLine(Family, "warn", $"{d.Name} died at {d.Age}{(d.How.Length > 0 ? ", " + d.How : "")}.", "dynasty:dead", d.PersonId, GraveDeath));
            foreach (var post in d.Posts) lines.Add(new BriefLine(Family, "warn", $"Left empty by {d.Name}: {post.What}. Fill it here.", post.Target, d.PersonId));
            foreach (var passed in d.Passed) lines.Add(new BriefLine(Family, "info", passed, "house:purse"));
        }

        // the seat passed: said first of all, since everything else in the year is read in its light
        foreach (var s in w.Successions.Where(s => s.Turn >= year && s.HeirId == p.Id))
            lines.Insert(0, new BriefLine(Family, "warn", $"{s.DeceasedName}, head of the house, {(s.Cause == "death" ? $"died at {s.DeceasedAge}" : "stepped down")}. You are head now, and what {(w.Char(s.DeceasedId)?.Sex switch { "female" => "she", "male" => "he", _ => "the old head" })} had begun in the year is yours to finish.", "dynasty:tree", s.DeceasedId, s.Cause == "death" ? YearMoments.HeadDies : 0));

        foreach (var m in w.Marriages.Where(m => m.Since == year).OrderBy(m => m.A, StringComparer.Ordinal))
        {
            var a = w.Char(m.A); var b = w.Char(m.B); if (a == null || b == null || (a.HouseId != mine && b.HouseId != mine)) continue;
            var ours = a.HouseId == mine ? a : b; var theirs = ours == a ? b : a;
            lines.Add(new BriefLine(Family, "good", $"{ours.Name} married {theirs.Name}{(theirs.HouseId != mine ? " of " + Nm(theirs.HouseId) : "")}{(m.Dowry > 0 ? $", with a dowry of {Praetoria.Core.Money.Credits(m.Dowry)} {(m.DowryFrom == mine ? "paid" : "received")}" : "")}.", "dynasty:marriages", ours.Id));
        }
        // a battle is fought as the year turns and is dated with the year that opens: those are the battles this brief is of
        foreach (var b in w.Battles.Where(b => b.Turn == w.Turn && b.Involves(mine)))
        {
            bool won = b.WinnerId == mine, att = b.AttackerId == mine; string foe = Nm(att ? b.DefenderId : b.AttackerId);
            lines.Add(new BriefLine(War, won ? "good" : "warn", $"The battle of {b.Place}: {(won ? "won against" : "lost to")} {foe}. You lost {Praetoria.Core.Counted.Hulls((att ? b.AttackerLost : b.DefenderLost))}, they lost {(att ? b.DefenderLost : b.AttackerLost)}.", "military:battles", Grave: won ? 0 : GraveBattle));
        }
        // the war's year as one account: battles, hulls, worlds, the score and the cost (docs/MILITARY.md, "Commanding a navy")
        foreach (var (tone, text) in WarLedger.Brief(w, mine)) lines.Add(new BriefLine(War, tone, text, WarLedger.Target));
        foreach (var war in w.Wars.Where(x => x.Since == year && x.Involves(mine)))
        {
            string other = war.HouseA == mine ? war.HouseB : war.HouseA;
            if (!lines.Any(l => l.Kind == War && l.Text.Contains(Nm(other)) && l.Text.Contains("declared war"))) lines.Add(new BriefLine(War, "warn", $"War began with {Nm(other)}.", "military", other));
        }
        foreach (var j in w.Judgments.Where(j => j.Turn == year && (j.WinnerId == mine || j.LoserId == mine)))
        {
            bool won = j.WinnerId == mine;
            if (j.Kind == "charge" && j.Judge.Length == 0) { lines.Add(new BriefLine(Courts, "warn", $"The charge {Nm(j.WinnerId)} brought was admitted before it was heard. {Praetoria.Core.Money.Credits(j.Sum)} left the house.", "senate:ladder")); continue; }
            lines.Add(new BriefLine(Courts, won ? "good" : "warn", $"{(j.Judge.Length > 0 ? j.Judge + ", praetor, ruled" : "It was settled")} {(won ? "for you against " + Nm(j.LoserId) : "against you for " + Nm(j.WinnerId))}{(j.Matter.Length > 0 ? ": " + j.Matter : "")}{(j.Sum > 0 ? $". {Praetoria.Core.Money.Credits(j.Sum)} {(won ? "came to" : "left")} the house" : "")}.", "senate:ladder", Grave: won ? 0 : GraveRuling));
        }
        foreach (var c in w.Counts.Where(c => c.Turn == year && c.HouseId == mine).OrderBy(c => c.RungId, StringComparer.Ordinal))
            lines.Add(new BriefLine(Senate, c.Won ? "good" : "info", $"{c.Name} {(c.Won ? "was elected " : "stood for ")}{c.Rung.ToLowerInvariant()}{(c.Won ? "" : " and was not chosen")}, with {c.Votes} votes.", "senate:ladder", c.CharacterId));

        // where the treasury stood and stands
        var moved = Moves(w).Select(m => $"{m.Label} {m.Before} → {m.After} ({m.Change})").ToList();
        if (moved.Count > 0) lines.Add(new BriefLine(Money, house.Treasury.Credits < 0 ? "warn" : "info", "Over the year, with everything you spent and everything that came in: " + string.Join("; ", moved) + ".", "house:ledger", Grave: house.Treasury.Credits < 0 ? GraveInsolvent : 0));

        // the year's moments (YearMoments): a bad one makes the line that says it grave, and one that no line says yet is said here
        foreach (var m in YearMoments.For(w, content))
        {
            int at = lines.FindIndex(l => l.PersonId == m.Subject && l.Tone == m.Tone && l.Kind == m.Section);
            if (at >= 0) { if (m.Bad) lines[at] = lines[at] with { Grave = Math.Max(lines[at].Grave, m.Weight) }; }
            else lines.Add(new BriefLine(m.Section, m.Tone, m.Line, m.Target, m.Subject, m.Bad ? m.Weight : 0));
        }
        return lines.OrderBy(l => Array.IndexOf(Order, l.Kind)).ToList();
    }

    /// <summary>How grave a line of the year is that is not one of its moments: a death in the house beyond its main line, a battle
    /// lost, the treasury below nought at the year's end, a ruling against the house.</summary>
    public const int GraveDeath = 60, GraveInsolvent = 45, GraveBattle = 40, GraveRuling = 35;
    /// <summary>How many lines the red band holds at most.</summary>
    public const int WorstCount = 3;

    /// <summary>The worst news of the year, the gravest first: what the red band at the head of the brief holds. The other lines stay
    /// in their sections.</summary>
    public static List<BriefLine> Worst(IEnumerable<BriefLine> lines) =>
        lines.Where(l => l.Grave > 0 && l.Tone == "warn").OrderByDescending(l => l.Grave).Take(WorstCount).ToList();

    /// <summary>A line told during the year that the year has made untrue: a warning about someone of the house (that they are ill,
    /// that their tutor has gone) who has died since.</summary>
    private static bool Stale(World w, NewsItem n) =>
        n.Tone == "warn" && n.Target.StartsWith("dynasty", StringComparison.Ordinal) && n.SubjectId.Length > 0 && w.Char(n.SubjectId) is { Alive: false };

    /// <summary>One stock as the year moved it: what it was when the year opened, what it is now, and the change with its sign (the true
    /// minus for a loss), for the brief's stock chips.</summary>
    public sealed record StockMove(string Resource, string Label, string Before, string After, string Change, bool Up);
    /// <summary>How the year moved each of the house's stocks, credits first; only those that moved.</summary>
    public static List<StockMove> Moves(World w)
    {
        var house = w.Protagonist is { } p ? w.House(p.HouseId) : null; var list = new List<StockMove>(); if (house == null) return list;
        foreach (var r in Counted)
        {
            if (r == Resource.Credits)
            {
                long by = (long)w.Counter(DeltaKey(r)) * Praetoria.Core.Money.PerUnit + w.Counter(DeltaKey(Resource.Coin));
                if (by != 0) list.Add(new StockMove(r, "credits", Praetoria.Core.Money.Exact(house.Treasury.Total - by), Praetoria.Core.Money.Held(house.Treasury), (by > 0 ? "+" : "−") + Praetoria.Core.Money.Exact(Math.Abs(by)), by > 0));
                continue;
            }
            if (w.WorldCounters.TryGetValue(DeltaKey(r), out int d) && d != 0)
                list.Add(new StockMove(r, Resource.Label(r).ToLowerInvariant(), Praetoria.Core.Money.Of(r, house.Treasury.Get(r) - d), Praetoria.Core.Money.Of(r, house.Treasury.Get(r)), (d > 0 ? "+" : "−") + Praetoria.Core.Money.Of(r, Math.Abs(d)), d > 0));
        }
        return list;
    }

    private static string KindOf(string target)
    {
        string head = target.Split(':')[0];
        return head switch { "dynasty" or "character" or "you" => Family, "military" or "galaxy" or "fleet" => target.StartsWith("galaxy") ? Works : War, "senate" or "laws" => Senate, "exchange" => Works, "house" => target.Contains("ledger") || target.Contains("purse") ? Money : Family, _ => Other };
    }
}
