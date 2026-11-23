using Praetoria.Core.Progression;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Presentation;

/// <summary>One kind of cause in what a house's legitimacy is made of: its total over the whole game and over the last ten years.</summary>
public sealed record LegitimacyCause(string Id, string Name, int Total, int Lately);
/// <summary>One of the last changes: the year, the kind of cause, by how much, and a plain line.</summary>
public sealed record LegitimacyChangeLine(int Turn, string Cause, string CauseName, int By, string Line);
/// <summary>What a house's legitimacy is made of (docs/TITLES.md): where it began, each cause's total, and the last changes.
/// <see cref="Began"/> and the totals add up to <see cref="Now"/>.</summary>
public sealed record LegitimacyCard(int Now, int Began, int BeganTurn, IReadOnlyList<LegitimacyCause> Causes, IReadOnlyList<LegitimacyChangeLine> Changes)
{
    /// <summary>The whole of it in one line, for a tooltip: "began at 30 · The Senate +6 · Scandal −3".</summary>
    public string Line => "began at " + Began + string.Concat(Causes.Select(c => " · " + c.Name + " " + LegitimacyProjection.Signed(c.Total)));
}

/// <summary>One thing the next rung of the ladder asks, what the house has of it, and whether that is enough (docs/TITLES.md,
/// "Rungs that are earned"). <see cref="Id"/>: standing, land, seats, vote, year. Land and seats are alternatives.</summary>
public sealed record RungNeed(string Id, string Asks, string Has, bool Met);

public static class LegitimacyProjection
{
    public static string Signed(int n) => n > 0 ? "+" + n : n < 0 ? "−" + (-n) : "0";

    /// <summary>A house's record, read for a screen. What has moved the number since the record was last settled is shown under
    /// "other", so that the card adds up in the middle of a year as it does at the end of one.</summary>
    public static LegitimacyCard Of(World w, House house)
    {
        var rec = house.LegitimacyRecord; int began = rec.Opened ? rec.Began : house.Legitimacy; int loose = LegitimacyBook.Unentered(house);
        var lately = LegitimacyBook.Lately(w, house);
        var causes = new List<LegitimacyCause>();
        foreach (var c in LegitimacyBook.Causes)
        {
            int total = rec.Totals.GetValueOrDefault(c) + (c == LegitimacyBook.Other ? loose : 0), late = lately.GetValueOrDefault(c) + (c == LegitimacyBook.Other ? loose : 0);
            if (total != 0 || late != 0) causes.Add(new LegitimacyCause(c, LegitimacyBook.CauseName(c), total, late));
        }
        var changes = rec.Changes.AsEnumerable().Reverse().Select(x => new LegitimacyChangeLine(x.Turn, x.Cause, LegitimacyBook.CauseName(x.Cause), x.By, x.Line)).ToList();
        return new LegitimacyCard(house.Legitimacy, began, rec.Opened ? rec.BeganTurn : w.Turn, causes, changes);
    }

    /// <summary>What the next rung asks of a house and what it has: the standing; from Legate up the land or the seats, and the
    /// Senate's vote as the houses lean today; and that it has not risen already this year. Empty at the top of the ladder.</summary>
    public static List<RungNeed> Needs(World w, House house, ProgressionSystem prog)
    {
        var needs = new List<RungNeed>(); var next = prog.NextTitle(w, house.Id); if (next == null) return needs;
        needs.Add(new RungNeed("standing", $"Legitimacy {next.LegitimacyRequirement}", $"the house has {house.Legitimacy}", house.Legitimacy >= next.LegitimacyRequirement));
        var f = prog.FootingFor(w, house.Id, next);
        if (f.Asked)
        {
            needs.Add(new RungNeed("land", Counted.Worlds(f.WorldsWanted), f.WorldsHeld == 0 ? "the house holds none" : $"the house holds {f.WorldsHeld}", f.ByLand));
            if (f.SeatsCount) needs.Add(new RungNeed("seats", $"or {Counted.Seats(f.SeatsWanted)} in the Senate", $"the house has {f.SeatsHeld}", f.BySeats));
        }
        if (prog.Voted(next) && prog.Senate is { } senate)
        {
            var open = ProgressionSystem.PetitionOf(w, house.Id);
            var (yes, no) = open != null ? senate.Tally(w, open) : senate.Forecast(w, house.Id, ProgressionSystem.PetitionKind, house.Id, titleId: next.Id);
            needs.Add(new RungNeed("vote", "A vote of the Senate", open != null ? $"your petition is before it: {yes} for and {no} against as the houses lean, voted at the end of {(open.ProposedTurn < w.Turn ? "this" : "next")} year"
                : $"as the houses lean today, {yes} for and {no} against", yes > no));
        }
        bool rose = w.WorldCounters.TryGetValue("rose:" + house.Id, out var at) && at == w.Turn + 1;
        if (rose) needs.Add(new RungNeed("year", "One rung a year", "the house has risen a rung this year", false));
        return needs;
    }

    /// <summary>The same in a sentence: which of the roads to the rung the house has and which it lacks.</summary>
    public static string Says(World w, House house, ProgressionSystem prog)
    {
        var next = prog.NextTitle(w, house.Id); if (next == null) return "The house stands at the top of the ladder. What is above it is not asked for.";
        var f = prog.FootingFor(w, house.Id, next); string rung = next.NameIn(w.Era);
        if (!f.Asked) return $"The rank of {rung} is granted to a house of the standing for it. Nothing more is asked.";
        string land = f.ByLand ? $"it has the land ({Counted.Worlds(f.WorldsHeld)})" : $"it lacks the land ({f.WorldsHeld} of {Counted.Worlds(f.WorldsWanted)})";
        string seats = !f.SeatsCount ? "" : f.BySeats ? $"it has the seats ({f.SeatsHeld})" : $"it lacks the seats ({f.SeatsHeld} of {f.SeatsWanted})";
        string either = seats.Length == 0 ? land : f.ByLand == f.BySeats ? $"{land} and {seats}" : $"{land} but {seats}";
        return $"The rank of {rung} must be earned: by land or by seats in the Senate, and either is enough. Of these, {either}."
            + (prog.Voted(next) ? " A petition then goes before the Senate, and the house rises only if the chamber carries it." : "");
    }
}
