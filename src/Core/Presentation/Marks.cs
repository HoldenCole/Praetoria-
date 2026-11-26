namespace Praetoria.Core.Presentation;

/// <summary>What a mark on a person means, for a tooltip. A mark is a flag something done or suffered has left; the game has
/// some hundreds, most of them read by one later report. The few a player meets often are spelt out; the rest say what a
/// mark is, which is true of all of them.</summary>
public static class Marks
{
    private static readonly Dictionary<string, string> Known = new(StringComparer.Ordinal)
    {
        ["parvenu"] = $"The old houses look down on yours: the head of any house whose legitimacy is {Systems.DynastySystem.ParvenuGap} or more above yours cools toward you a point a year. It goes when your legitimacy catches up.",
        ["debt_to_crown"] = "You took the Crown's favour and owe for it. The Crown will call the debt in, in a year of its choosing.",
        ["feud"] = "There is a quarrel between you that has not been made up.",
        ["humiliated"] = "Shamed in public, and they remember who did it.",
        ["wounded"] = "Carries a wound.",
        ["owes_you"] = "They owe you a turn, and know it.",
        ["reconciled"] = "An old quarrel with you has been made up.",
        ["marked_for_death"] = "Somebody has paid, or sworn, to see them dead.",
        [Systems.DisownSystem.StruckChar] = "Struck out of the line of succession by the head of the house. They stand after everyone else, and have the house only if nobody else is left.",
        [Systems.DisownSystem.CastOutChar] = "Cast out of the house they were born to. They live in another now, and remember it.",
    };

    public static string Label(string flag) => flag.Replace('_', ' ').Replace(':', ' ');

    public static string Tip(string flag) => Known.TryGetValue(flag, out var s) ? s
        : "A mark that something done or suffered has left. Later reports read it: it can open a choice, close one, or bring what was owed.";
}
