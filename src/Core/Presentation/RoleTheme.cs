using Praetoria.Core.State;

namespace Praetoria.Core.Presentation;

/// <summary>The role register (GDD Art Direction, "by character's job"): the same screens, with chrome and emphasis that follow the
/// protagonist's career track — an admiral's game is dispatches and readouts, a steward's is ledgers, a senator's is the chamber. It
/// changes at succession, when the career resets. Purely presentational; the web shell reads it as <c>data-role</c> beside <c>data-era</c>.</summary>
public static class RoleTheme
{
    public const string Military = "military", Stewardship = "stewardship", Law = "law", None = "none";

    /// <summary>The register of the protagonist: the career track, or <see cref="None"/> for a head with no career yet.</summary>
    public static string Of(World w) => w.Protagonist?.CareerTrack switch { Military => Military, Stewardship => Stewardship, Law => Law, _ => None };

    /// <summary>What the register is called on the seat-passing card.</summary>
    public static string Name(string role) => role switch { Military => "the Admiralty's register", Stewardship => "the Ministry's register", Law => "the Court's register", _ => "the house's own register" };

    /// <summary>The kicker printed over every briefing card: the voice the reports arrive in.</summary>
    public static string Kicker(string role) => role switch { Military => "DISPATCH", Stewardship => "LEDGER", Law => "THE COURT", _ => "THE BRIEFING" };

    /// <summary>The screen a new protagonist of this register lands on after the seat passes.</summary>
    public static string Home(string role) => role switch { Military => "military", Stewardship => "house", Law => "senate", _ => "office" };

    /// <summary>The screens this register puts first on the rail, after the Office.</summary>
    public static IReadOnlyList<string> Priority(string role) => role switch
    {
        Military => new[] { "military", "galaxy" }, Stewardship => new[] { "house", "planets" }, Law => new[] { "senate", "laws" }, _ => Array.Empty<string>()
    };

    /// <summary>The advisor whose report leads the desk under this register.</summary>
    public static IReadOnlyList<string> AdvisorOrder(string role) => role switch
    {
        Military => new[] { "Admiral", "Chancellor", "Steward" }, Stewardship => new[] { "Steward", "Chancellor", "Admiral" }, Law => new[] { "Chancellor", "Steward", "Admiral" }, _ => new[] { "Steward", "Admiral", "Chancellor" }
    };

    /// <summary>One line for the seat-passing card: what the heir's register will look like.</summary>
    public static string Describe(string role) => role switch
    {
        Military => "The game turns martial: dispatches, readouts, the fleet first on the rail and the Admiral first at the desk.",
        Stewardship => "The game turns administrative: ledgers, ruled lines, the house and its worlds first on the rail and the Steward first at the desk.",
        Law => "The game turns ceremonial: the chamber, engraved framing, the Senate and the Chancery first on the rail and the Chancellor first at the desk.",
        _ => "No career yet: the house's own register, until the heir takes a track."
    };

    /// <summary>Order the rail: the Office, then the register's screens, then the rest in their usual order.</summary>
    public static IEnumerable<T> OrderScreens<T>(string role, IEnumerable<T> screens, Func<T, string> id)
    {
        var list = screens.ToList(); var pri = Priority(role);
        return list.Where(s => id(s) == "office").Concat(pri.Select(p => list.FirstOrDefault(s => id(s) == p)).Where(s => s != null)!).Concat(list.Where(s => id(s) != "office" && !pri.Contains(id(s))));
    }
}
