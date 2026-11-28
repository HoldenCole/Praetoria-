using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Presentation;

/// <summary>A step of the first years as the list shows it: what to do and why, whether it is done, why it cannot be done yet (or null; and the
/// same in a few words), where "Show me" goes (a fleet step goes to the house's first fleet by id) and what it lights, and the Academy term
/// it carries (0 for none) with what that term was about, and where the step is done (a window id, with a tab after a colon; "map").</summary>
public sealed record FirstStepLine(string Id, string Title, string Text, bool Done, string? NotYet, string Show, IReadOnlyList<string> Ring, int Term, string TermSays, string Waits = "", string At = "");

/// <summary>The first years' list (docs/ACADEMY.md, "The first years"): the house it is written for, the year of the saga against the years it
/// stays, and its steps in order.</summary>
public sealed record FirstStepsModel(string HouseName, int Year, int Years, bool Academy, IReadOnlyList<FirstStepLine> Steps)
{
    public int DoneCount => Steps.Count(s => s.Done);
    public bool AllDone => Steps.Count > 0 && Steps.All(s => s.Done);
}

public static class FirstStepsProjection
{
    /// <summary>The list as it stands, or null when it is not on the screen: no first years in this saga, still at the Academy, put away, or
    /// the third year over.</summary>
    public static FirstStepsModel? Build(World w, ContentDatabase content)
    {
        var cat = content.Academy.FirstYears;
        if (!FirstStepsSystem.Showing(w, cat) || w.FirstSteps is not { } fs) return null;
        var house = w.Protagonist is { } p ? w.House(p.HouseId) : null;
        string firstFleet = w.Fleets.Values.Where(f => f.OwnerId == house?.Id).OrderBy(f => f.Id, StringComparer.Ordinal).Select(f => f.Id).FirstOrDefault() ?? "";
        var lines = FirstStepsSystem.StepsOf(w, cat).Select(s =>
        {
            bool done = FirstStepsSystem.IsDone(fs, s);
            string show = s.Show == "fleet" ? (firstFleet.Length > 0 ? "fleet:" + firstFleet : "military") : s.Show;
            string? notYet = done ? null : FirstStepsSystem.NotYet(w, cat, s);
            string at = s.Done.StartsWith("seen:", StringComparison.Ordinal) ? s.Done[5..].Split('|')[0] : show;
            return new FirstStepLine(s.Id, s.Title, s.Text, done, notYet, show, s.Ring, s.Term, cat.Term(s.Term),
                notYet != null && cat.Waits.TryGetValue(FirstStepsSystem.Act(s), out var waits) ? waits : notYet ?? "", at);
        }).ToList();
        if (lines.Count == 0) return null;
        return new FirstStepsModel(house?.Name ?? "", w.Turn, cat.Years, fs.Academy, lines);
    }
}
