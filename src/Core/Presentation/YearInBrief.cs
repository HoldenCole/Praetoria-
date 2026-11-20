using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Presentation;

/// <summary>One line of the year in brief: what kind of thing it was, how it should be read ("good", "warn", "info"), the line, and
/// the window that shows it.</summary>
public sealed record BriefLine(string Kind, string Tone, string Text, string Target);

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
    private static readonly string[] Counted = { Resource.Credits, Resource.Materials, Resource.Manpower, Resource.Helium3 };

    /// <summary>Called as a year opens: what each counted good did over the year just ended, and where it stands now.</summary>
    public static void Mark(World w)
    {
        var house = w.Protagonist != null ? w.House(w.Protagonist.HouseId) : null; if (house == null) return;
        foreach (var r in Counted)
        {
            int now = house.Treasury.Get(r);
            if (w.WorldCounters.TryGetValue(OpenKey(r), out int open)) w.WorldCounters[DeltaKey(r)] = now - open; else w.WorldCounters.Remove(DeltaKey(r));
            w.WorldCounters[OpenKey(r)] = now;
        }
    }

    /// <summary>The lines for the year that has just ended (the world is at the start of the next).</summary>
    public static List<BriefLine> For(World w, ContentDatabase content)
    {
        var lines = new List<BriefLine>(); var p = w.Protagonist; var house = p != null ? w.House(p.HouseId) : null; if (p == null || house == null) return lines;
        int year = w.Turn - 1; string mine = house.Id; string Nm(string id) => w.House(id)?.Name ?? id;

        // what the house was told as the year turned: births, deaths, wars declared on it, attempts, tutors, illness, dues, dowries
        foreach (var n in w.NewsNow()) lines.Add(new BriefLine(KindOf(n.Target), n.Tone, n.Text, n.Target));

        // the seat passed: said first of all, since everything else in the year is read in its light
        foreach (var s in w.Successions.Where(s => s.Turn >= year && s.HeirId == p.Id))
            lines.Insert(0, new BriefLine(Family, "warn", $"{s.DeceasedName}, head of the house, {(s.Cause == "death" ? $"died at {s.DeceasedAge}" : "stepped down")}. {p.Name} is head now, and what the old head had begun in the year is theirs to finish.", "dynasty:tree"));

        foreach (var m in w.Marriages.Where(m => m.Since == year).OrderBy(m => m.A, StringComparer.Ordinal))
        {
            var a = w.Char(m.A); var b = w.Char(m.B); if (a == null || b == null || (a.HouseId != mine && b.HouseId != mine)) continue;
            var ours = a.HouseId == mine ? a : b; var theirs = ours == a ? b : a;
            lines.Add(new BriefLine(Family, "good", $"{ours.Name} married {theirs.Name}{(theirs.HouseId != mine ? " of " + Nm(theirs.HouseId) : "")}{(m.Dowry > 0 ? $", with a dowry of {m.Dowry} credits {(m.DowryFrom == mine ? "paid" : "received")}" : "")}.", "dynasty:marriages"));
        }
        foreach (var b in w.Battles.Where(b => b.Turn == year && b.Involves(mine)))
        {
            bool won = b.WinnerId == mine, att = b.AttackerId == mine; string foe = Nm(att ? b.DefenderId : b.AttackerId);
            lines.Add(new BriefLine(War, won ? "good" : "warn", $"The battle of {b.Place}: {(won ? "won against" : "lost to")} {foe}. You lost {(att ? b.AttackerLost : b.DefenderLost)} hull(s), they lost {(att ? b.DefenderLost : b.AttackerLost)}.", "military:battles"));
        }
        foreach (var war in w.Wars.Where(x => x.Since == year && x.Involves(mine)))
        {
            string other = war.HouseA == mine ? war.HouseB : war.HouseA;
            if (!lines.Any(l => l.Kind == War && l.Text.Contains(Nm(other)) && l.Text.Contains("declared war"))) lines.Add(new BriefLine(War, "warn", $"War began with {Nm(other)}.", "military"));
        }
        foreach (var j in w.Judgments.Where(j => j.Turn == year && (j.WinnerId == mine || j.LoserId == mine)))
        {
            bool won = j.WinnerId == mine;
            lines.Add(new BriefLine(Courts, won ? "good" : "warn", $"{(j.Judge.Length > 0 ? j.Judge + ", praetor, ruled" : "It was settled")} {(won ? "for you against " + Nm(j.LoserId) : "against you for " + Nm(j.WinnerId))}{(j.Matter.Length > 0 ? ": " + j.Matter : "")}{(j.Sum > 0 ? $". {j.Sum} credits {(won ? "came to" : "left")} the house" : "")}.", "senate:ladder"));
        }
        foreach (var c in w.Counts.Where(c => c.Turn == year && c.HouseId == mine).OrderBy(c => c.RungId, StringComparer.Ordinal))
            lines.Add(new BriefLine(Senate, c.Won ? "good" : "info", $"{c.Name} {(c.Won ? "was elected " : "stood for ")}{c.Rung.ToLowerInvariant()}{(c.Won ? "" : " and was not chosen")}, with {c.Votes} votes.", "senate:ladder"));

        // where the treasury stood and stands
        var moved = new List<string>();
        foreach (var r in Counted)
            if (w.WorldCounters.TryGetValue(DeltaKey(r), out int d) && d != 0) moved.Add($"{Resource.Label(r).ToLowerInvariant()} {house.Treasury.Get(r) - d} → {house.Treasury.Get(r)} ({(d > 0 ? "+" : "−")}{Math.Abs(d)})");
        if (moved.Count > 0) lines.Add(new BriefLine(Money, house.Treasury.Credits < 0 ? "warn" : "info", "Over the year, with everything you spent and everything that came in: " + string.Join("; ", moved) + ".", "house:ledger"));
        return lines.OrderBy(l => Array.IndexOf(Order, l.Kind)).ToList();
    }

    private static string KindOf(string target)
    {
        string head = target.Split(':')[0];
        return head switch { "dynasty" or "character" or "you" => Family, "military" or "galaxy" or "fleet" => target.StartsWith("galaxy") ? Works : War, "senate" or "laws" => Senate, "exchange" => Works, "house" => target.Contains("ledger") || target.Contains("purse") ? Money : Family, _ => Other };
    }
}
