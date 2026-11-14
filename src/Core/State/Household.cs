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
