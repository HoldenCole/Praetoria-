namespace Praetoria.Core.State;

/// <summary>One thing that moved a house's legitimacy (docs/TITLES.md, "What legitimacy is made of"): the year, the kind of
/// cause, by how much it actually moved, and a plain line saying what it was.</summary>
public sealed class LegitimacyChange
{
    public int Turn { get; set; }
    public string Cause { get; set; } = "";
    public int By { get; set; }
    public string Line { get; set; } = "";
}

/// <summary>What moved a house's legitimacy in one year, by cause.</summary>
public sealed class LegitimacyYear
{
    public int Turn { get; set; }
    public Dictionary<string, int> ByCause { get; set; } = new();
}

/// <summary>The record of what a house's legitimacy is made of: where it began, the total by cause for the whole game, the same
/// year by year for the last ten years, and the last dozen changes. It always adds up: <see cref="Began"/> and the totals make
/// <see cref="Reckoned"/>, which is the house's legitimacy as it was last reckoned (whatever moved the number without saying why
/// is entered under "other" when the year closes). Kept by <see cref="Systems.LegitimacyBook"/>.</summary>
public sealed class LegitimacyRecord
{
    /// <summary>False until the record is first opened: a house made in play, or a save from before the record was kept.</summary>
    public bool Opened { get; set; }
    /// <summary>The legitimacy the house had when the record was opened, and the year it was.</summary>
    public int Began { get; set; }
    public int BeganTurn { get; set; }
    /// <summary>The legitimacy the record adds up to.</summary>
    public int Reckoned { get; set; }
    public Dictionary<string, int> Totals { get; set; } = new();
    public List<LegitimacyYear> Years { get; set; } = new();
    public List<LegitimacyChange> Changes { get; set; } = new();
}
