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
