namespace Praetoria.Core.State;

/// <summary>A standing duty the head has handed to a member of the house (docs/HOUSEHOLD.md): the steward runs the routine of the
/// estates and ventures, the marshal keeps the fleets fuelled and in repair. The head may do any of it personally at any time.</summary>
public sealed class Duty
{
    public string Role { get; set; } = "";
    public string HolderId { get; set; } = "";
    public int Since { get; set; }
    /// <summary>Credits the holder must leave in the treasury. They spend only what is above it.</summary>
    public int Reserve { get; set; } = 20;
    /// <summary>What they did last year, in a line.</summary>
    public string LastReport { get; set; } = "";
    /// <summary>What has gone missing under them. Not shown until it is found out.</summary>
    public int Skimmed { get; set; }
}

/// <summary>Someone asking to be taken into the house. The head answers; it lapses in two years.</summary>
public sealed class Petition
{
    public string PersonId { get; set; } = "";
    public string As { get; set; } = "";
    public int Since { get; set; }
}

/// <summary>What a person has said shall be done with what is theirs when they die (docs/WEALTH.md). The house itself goes by the
/// succession law, whatever this says.</summary>
public sealed class Will
{
    /// <summary>Someone other than the heir who is to have part of the purse, or empty.</summary>
    public string LegateeId { get; set; } = "";
    /// <summary>The part of the purse they are to have: 0, 25, 50 or 100.</summary>
    public int SharePct { get; set; }
    /// <summary>Heirloom id → the person it is left to. Anything not named goes to the heir.</summary>
    public Dictionary<string, string> Items { get; set; } = new();
}

/// <summary>A thing worth leaving to someone: a signet, a sword, a library, an estate. It belongs to a person, not to the house.</summary>
public sealed class Heirloom
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>signet, sword, library or estate.</summary>
    public string Kind { get; set; } = "";
    public string HolderId { get; set; } = "";
    /// <summary>The house it has always belonged to. Selling it out of that house is felt.</summary>
    public string OfHouseId { get; set; } = "";
    /// <summary>What it would fetch.</summary>
    public int Value { get; set; }
}

/// <summary>What one of the house is doing about what they want (docs/AMBITIONS.md). They pursue it themselves; now and then they
/// ask the head for something that would help.</summary>
public sealed class Pursuit
{
    /// <summary>The ambition this pursuit is for.</summary>
    public string Goal { get; set; } = "";
    /// <summary>0..100. At a hundred they have what they wanted.</summary>
    public int Progress { get; set; }
    public int Since { get; set; }
    public bool Fulfilled { get; set; }
    public int FulfilledTurn { get; set; } = -1;
    /// <summary>What they have asked the head for and not been answered: command, stake, word or heir. Empty when nothing is asked.</summary>
    public string Request { get; set; } = "";
    /// <summary>The fleet they want, or the sum.</summary>
    public string RequestArg { get; set; } = "";
    public int RequestSince { get; set; } = -1;
    public int LastAsked { get; set; } = -99;
    /// <summary>Times the head helped, and times the head refused or did not answer.</summary>
    public int Helped { get; set; }
    public int Refused { get; set; }
    /// <summary>Credits of their own they have put into the house.</summary>
    public int Given { get; set; }
    /// <summary>What they did about it this year, in a line.</summary>
    public string Note { get; set; } = "";
}

/// <summary>One thing a person did, for the record of their life (docs/LIFE.md).</summary>
public sealed class Deed
{
    public int Turn { get; set; }
    public int Age { get; set; }
    public string Kind { get; set; } = "";
    public string Text { get; set; } = "";
}

/// <summary>The record of a life (docs/LIFE.md): what they did, how often they were wounded, and who has been their friend or their
/// enemy long enough for it to count.</summary>
public sealed class Life
{
    public List<Deed> Deeds { get; set; } = new();
    public int Wounds { get; set; }
    /// <summary>Things already noted once, so they are not noted again (a rank reached, a skill mastered, an office held).</summary>
    public HashSet<string> Marks { get; set; } = new();
    /// <summary>Person id → the turn they first thought well enough of this person to be called a friend.</summary>
    public Dictionary<string, int> Friends { get; set; } = new();
    /// <summary>Person id → the turn they first thought ill enough of this person to be called an enemy.</summary>
    public Dictionary<string, int> Enemies { get; set; } = new();
    public int RestedTurn { get; set; } = -1;
}

/// <summary>What was said when one of the house was buried.</summary>
public sealed class Funeral
{
    public int Turn { get; set; }
    public string PersonId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public string Epithet { get; set; } = "";
    public List<string> Lines { get; set; } = new();
}
