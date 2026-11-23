using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// What legitimacy is made of (docs/TITLES.md; docs/PLAYTHROUGH.md, Hard: "a breakdown of what legitimacy is made of"). Every
/// rule that moves a house's legitimacy does it through <see cref="Move"/>, naming one of a dozen kinds of cause and saying in a
/// plain line what happened. The record (<see cref="LegitimacyRecord"/>, on the house) keeps the total by cause for the whole
/// game and year by year for the last <see cref="YearsKept"/> years, and the last <see cref="ChangesKept"/> changes. It always
/// adds up to the number: a move is entered at what it actually moved (legitimacy is held between 0 and 100), and whatever part
/// of the number moved without passing through here is entered under <see cref="Other"/> at the next move or when the year
/// closes. It changes nothing that any source of legitimacy gives. Deterministic and RNG-free.
/// </summary>
public static class LegitimacyBook
{
    public const string Titles = "titles", Senate = "senate", Offices = "offices", Courts = "courts", Laws = "laws", Works = "works", Land = "land",
        Family = "family", War = "war", Beyond = "beyond", Scandal = "scandal", Events = "events", Other = "other";
    /// <summary>The kinds of cause, in the order they are shown.</summary>
    public static readonly string[] Causes = { Titles, Senate, Offices, Courts, Laws, Works, Land, Family, War, Beyond, Scandal, Events, Other };
    public const int YearsKept = 10, ChangesKept = 12;

    public static string CauseName(string cause) => cause switch
    {
        Titles => "Titles and the throne",
        Senate => "The Senate",
        Offices => "Offices of the Reach",
        Courts => "The courts",
        Laws => "Laws and the estates",
        Works => "Works",
        Land => "Land and the worlds",
        Family => "Marriages and the family",
        War => "War",
        Beyond => "The powers beyond the Reach",
        Scandal => "Scandal",
        Events => "Reports answered",
        _ => "Other things",
    };

    /// <summary>Move a house's legitimacy by <paramref name="by"/> and enter it in the record under <paramref name="cause"/>, with
    /// a plain line. Returns what it actually moved by (less than asked at 0 or at 100).</summary>
    public static int Move(World w, House? house, int by, string cause, string line = "")
    {
        if (house == null) return 0;
        var rec = Settle(w, house);
        int before = house.Legitimacy; house.Legitimacy = before + by; int moved = house.Legitimacy - before;
        Enter(w, rec, moved, cause, line.Length > 0 ? line : CauseName(cause)); rec.Reckoned = house.Legitimacy;
        return moved;
    }

    /// <summary>Open the record if it is not, and enter under <see cref="Other"/> whatever moved the number since it was last
    /// reckoned without saying why.</summary>
    public static LegitimacyRecord Settle(World w, House house)
    {
        var rec = house.LegitimacyRecord;
        if (!rec.Opened) { rec.Opened = true; rec.Began = house.Legitimacy; rec.BeganTurn = w.Turn; rec.Reckoned = house.Legitimacy; return rec; }
        int drift = house.Legitimacy - rec.Reckoned;
        if (drift != 0) { Enter(w, rec, drift, Other, "Other things in the year"); rec.Reckoned = house.Legitimacy; }
        return rec;
    }

    private static void Enter(World w, LegitimacyRecord rec, int moved, string cause, string line)
    {
        if (moved == 0) return;
        rec.Totals[cause] = rec.Totals.GetValueOrDefault(cause) + moved;
        var year = rec.Years.Count > 0 && rec.Years[^1].Turn == w.Turn ? rec.Years[^1] : null;
        if (year == null) { year = new LegitimacyYear { Turn = w.Turn }; rec.Years.Add(year); }
        year.ByCause[cause] = year.ByCause.GetValueOrDefault(cause) + moved;
        // the same thing twice in a year is one line, not two
        if (rec.Changes.Count > 0 && rec.Changes[^1] is { } last && last.Turn == w.Turn && last.Cause == cause && last.Line == line) last.By += moved;
        else rec.Changes.Add(new LegitimacyChange { Turn = w.Turn, Cause = cause, By = moved, Line = line });
        if (rec.Changes.Count > ChangesKept) rec.Changes.RemoveRange(0, rec.Changes.Count - ChangesKept);
    }

    /// <summary>Open every house's record: once, as a game begins, so that "where the house began" is where it began.</summary>
    public static void Open(World w) { foreach (var h in w.Houses.Values) Settle(w, h); }

    /// <summary>The year closes: every house's record is settled, and years older than the last <see cref="YearsKept"/> drop out
    /// of the year-by-year count (they stay in the totals).</summary>
    public static void CloseYear(World w)
    {
        foreach (var h in w.Houses.Values)
        {
            var rec = Settle(w, h);
            rec.Years.RemoveAll(y => y.Turn <= w.Turn - YearsKept);
        }
    }

    /// <summary>A house's legitimacy by cause over the last <see cref="YearsKept"/> years, this year among them.</summary>
    public static Dictionary<string, int> Lately(World w, House house)
    {
        var sums = new Dictionary<string, int>();
        foreach (var y in house.LegitimacyRecord.Years.Where(y => y.Turn > w.Turn - YearsKept))
            foreach (var (cause, by) in y.ByCause) sums[cause] = sums.GetValueOrDefault(cause) + by;
        return sums;
    }

    /// <summary>What has moved the number since the record was last settled and is not yet entered: shown under "other" so that
    /// what is on the screen adds up in the middle of a year too.</summary>
    public static int Unentered(House house) => house.LegitimacyRecord.Opened ? house.Legitimacy - house.LegitimacyRecord.Reckoned : 0;
}
