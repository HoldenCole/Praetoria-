namespace Praetoria.Core.State;

/// <summary>A post left empty by a death, and the window where it is filled.</summary>
public sealed class DeathPost
{
    public string What { get; set; } = "";
    public string Target { get; set; } = "";
}

/// <summary>The death of someone of the player's house, as a report (docs/PLAYTHROUGH.md, Medium: "who, how, what passes to whom,
/// which posts are now empty, with the buttons to fill them"). The last twenty are kept on the world.</summary>
public sealed class DeathRecord
{
    public int Turn { get; set; }
    public string PersonId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Age { get; set; }
    /// <summary>How they died, in a few words: "in battle at Cinder Gate", "by a knife", "of age". Empty where nothing is known.</summary>
    public string How { get; set; } = "";
    /// <summary>They stood first in the line to the head when they died (the year's moments say "your heir is dead").</summary>
    public bool Heir { get; set; }
    public List<DeathPost> Posts { get; set; } = new();
    /// <summary>What they had, and whose it is now: one line each.</summary>
    public List<string> Passed { get; set; } = new();
}
