namespace Praetoria.Core.State;

/// <summary>A charge another house has brought against one of the player's house, or against one of the powerful (docs/LADDER.md, "The docket"). It is not
/// heard the year it is brought: it waits a year, the house is told, and in that year the head may send someone to plead, or
/// admit it and pay the fine, or do nothing.</summary>
public sealed class Indictment
{
    public string Id { get; set; } = "";
    /// <summary>The act on the record (<see cref="OfficeDeed.Id"/>) that is charged.</summary>
    public string DeedId { get; set; } = "";
    /// <summary>The house that brings it.</summary>
    public string AccuserId { get; set; } = "";
    /// <summary>The person charged, and their house.</summary>
    public string AccusedId { get; set; } = "";
    public string HouseId { get; set; } = "";
    /// <summary>The praetor who is to hear it.</summary>
    public string JudgeId { get; set; } = "";
    /// <summary>The year it was brought: it is heard at the end of the next.</summary>
    public int Turn { get; set; }
    /// <summary>Who of the accused's house is to plead it, or "".</summary>
    public string PleaderId { get; set; } = "";
    /// <summary>"" while it waits; then "admitted", "proved", "let_go" or "lapsed"; or, for a charge against the powerful
    /// (docs/LADDER.md, "A charge against the powerful"), "exiled" or "crossed" when the accused would not wait for the verdict.</summary>
    public string Outcome { get; set; } = "";
    /// <summary>A charge against the powerful: a person of great sway, or the head of a house of Legate rank or above. It is held
    /// a year whoever brings it, and the accused may stand trial, go into exile, or cross the line.</summary>
    public bool Powerful { get; set; }
    /// <summary>How the accused has said they will answer it: "", "trial", "exile" or "cross".</summary>
    public string Answer { get; set; } = "";
    /// <summary>The year it was heard, or answered without a hearing.</summary>
    public int HeardTurn { get; set; }
    /// <summary>When one who commands is convicted: "lay_down" while they must lay the command down or cross; then "laid_down"
    /// or "crossed".</summary>
    public string Sentence { get; set; } = "";
    /// <summary>The year by whose end a convicted commander must have answered.</summary>
    public int AnswerBy { get; set; }
}

/// <summary>One who went into exile beyond the Reach rather than wait for a verdict (docs/LADDER.md, "A charge against the
/// powerful"). They live at a nation's court, out of the Reach's politics, until the Senate votes them home.</summary>
public sealed class Exile
{
    public string CharacterId { get; set; } = "";
    /// <summary>The house they are of, and lead if <see cref="WasHead"/>.</summary>
    public string HouseId { get; set; } = "";
    public bool WasHead { get; set; }
    /// <summary>The nation whose court took them in, and its name as it was then ("" and the far worlds if none would).</summary>
    public string NationId { get; set; } = "";
    public string NationName { get; set; } = "";
    public int Since { get; set; }
    /// <summary>The house that brought the charge, and the charge.</summary>
    public string AccuserId { get; set; } = "";
    public string ChargeId { get; set; } = "";
}

/// <summary>A house has let it be known that a general will be charged the day the command is laid down (docs/LADDER.md, "A
/// charge against the powerful"). While it stands it is a cause to cross the line; a house that never brings the charge pays.</summary>
public sealed class ChargeThreat
{
    public string Id { get; set; } = "";
    /// <summary>The house that threatens.</summary>
    public string AccuserId { get; set; } = "";
    /// <summary>The general, their house, and the act on the record they would be charged with.</summary>
    public string AccusedId { get; set; } = "";
    public string HouseId { get; set; } = "";
    public string DeedId { get; set; } = "";
    public int Turn { get; set; }
    /// <summary>The first year the general was found to hold no command: the charge is due by the end of the year after.</summary>
    public int LaidDownTurn { get; set; }
    /// <summary>"" while it stands; then "kept" (the charge was brought), "empty" (it never was) or "lapsed" (the general died,
    /// or another answered for it first).</summary>
    public string Outcome { get; set; } = "";
}
