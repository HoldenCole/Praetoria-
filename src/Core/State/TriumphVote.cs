namespace Praetoria.Core.State;

/// <summary>A triumph the Senate has voted a general, and what the general did with it (docs/CAESAR.md, "The triumph"). It is had
/// only at the gate: the general comes home and lays the command down for a year, or declines it and keeps the fleet. The record
/// stays when it is done, so that what was chosen can be read; the finished ones are kept forty deep.</summary>
public sealed class TriumphVote
{
    public string CharacterId { get; set; } = "";
    /// <summary>The general's name, kept for the record after they are dead.</summary>
    public string Name { get; set; } = "";
    public string HouseId { get; set; } = "";
    /// <summary>The house that moved it, or "".</summary>
    public string ProposerHouseId { get; set; } = "";
    /// <summary>The year the Senate voted it.</summary>
    public int Turn { get; set; }
    /// <summary>"" while the general's answer is wanted; "home" (came home and had it), "keep" (declined it and kept the fleet),
    /// "lapsed" (left unanswered for the year, which is a refusal), "void" (the general died or the throne came before an answer); or "stand",
    /// which is no triumph: one of another house who came home to stand for office, whose fleet waits for them the same way until the count.</summary>
    public string Choice { get; set; } = "";
    /// <summary>For one who chose by their own nature: why, in words.</summary>
    public string Why { get; set; } = "";
    /// <summary>Home: the year the command was laid down, and the year at whose end it is taken up again.</summary>
    public int HomeSince { get; set; }
    public int HomeUntil { get; set; }
    /// <summary>The fleets laid down, held for the general while they are home: nobody else is given them.</summary>
    public List<string> FleetIds { get; set; } = new();
    /// <summary>How the year at home ended: "taken_up", or what stopped the general taking the command up again. "" while home.</summary>
    public string Ended { get; set; } = "";
    /// <summary>Nothing more is to come of it.</summary>
    public bool Done { get; set; }
}
