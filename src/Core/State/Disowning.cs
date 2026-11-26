namespace Praetoria.Core.State;

/// <summary>A head struck someone of the house out of its line, or cast them out of the house (docs/HOUSEHOLD.md, "Struck from the
/// line, cast out, and the undutiful will"). The record stays when it is undone, so that what was done can be read.</summary>
public sealed class Disowning
{
    public string Id { get; set; } = "";
    /// <summary>"struck" (out of the line, still of the house) or "cast_out" (out of the house).</summary>
    public string Kind { get; set; } = "";
    public string PersonId { get; set; } = "";
    public string PersonName { get; set; } = "";
    /// <summary>The house they were struck from or cast out of.</summary>
    public string HouseId { get; set; } = "";
    /// <summary>The head who did it.</summary>
    public string HeadId { get; set; } = "";
    public int Turn { get; set; }
    /// <summary>The cause on the record against them when it was done, in words, or "" when there was none.</summary>
    public string Cause { get; set; } = "";
    /// <summary>What it cost: the legitimacy the house lost, and what each of the kin came to think the less of the head. Undoing it
    /// costs the same again.</summary>
    public int Legitimacy { get; set; }
    public int KinRegard { get; set; }
    /// <summary>What they wanted before: what they want now is their grievance, and it is given back when the thing is undone.</summary>
    public string WasAmbition { get; set; } = "";
    public string Ambition { get; set; } = "";
    /// <summary>Cast out: the house they went to, and how ("spouse", "patron", "parent" or "alone", the first of a new name).</summary>
    public string WentTo { get; set; } = "";
    public string WentHow { get; set; } = "";
    /// <summary>Cast out: what was written of their birth before they went, given back if they are taken back.</summary>
    public string WasBornHouseId { get; set; } = "";
    /// <summary>The year it was undone (restored to the line, taken back, or set aside by a court), or -1 while it stands.</summary>
    public int UndoneTurn { get; set; } = -1;
    /// <summary>How it ended: "restored", "taken_back", "found_undutiful" (a court set it aside), "left" (they left the house, or the
    /// house they were cast out of), "died".</summary>
    public string Ended { get; set; } = "";
    public bool Stands => UndoneTurn < 0;
}

/// <summary>The suit of the undutiful will: at a head's death, a child they had struck out with no cause on the record says before a
/// praetor that the will did not do its duty by them, and asks their part.</summary>
public sealed class WillSuit
{
    public string Id { get; set; } = "";
    /// <summary>The house whose head's will it is; its head now answers the suit.</summary>
    public string HouseId { get; set; } = "";
    public string PlaintiffId { get; set; } = "";
    public string DeceasedId { get; set; } = "";
    public string DeceasedName { get; set; } = "";
    /// <summary>The year it was brought: it is heard at the end of the year after.</summary>
    public int Turn { get; set; }
    /// <summary>The praetor who is to hear it (of neither house), or "" when none sat.</summary>
    public string JudgeId { get; set; } = "";
    /// <summary>What turns on it: the part of the dead head's purse asked for, and the heirlooms (ids) asked for.</summary>
    public int Share { get; set; }
    public List<string> Items { get; set; } = new();
    /// <summary>Who the head has sent to answer it, or "".</summary>
    public string PleaderId { get; set; } = "";
    /// <summary>"" while it waits; "upheld" (the will stands), "undutiful" (the child wins), "settled", "lapsed".</summary>
    public string Outcome { get; set; } = "";
    public int DecidedTurn { get; set; } = -1;
    /// <summary>How the praetor went, in a line, once it is decided.</summary>
    public string How { get; set; } = "";
}
