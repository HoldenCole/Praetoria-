namespace Praetoria.Core.State;

/// <summary>A command by service (docs/MILITARY.md, "A command by service"): somebody commands a fleet that their own house does not
/// own. A <b>legate</b> is named by one who holds the consulship, the Admiral of the Reach's office or emergency powers, to a fleet
/// under them; a <b>lent</b> officer is taken by a house at war, with the leave of the head of the officer's own house. The
/// victories, the valour, the renown and the crews' devotion are the commander's; the hulls, the upkeep and the spoils stay the
/// owner's. It ends when the owner, the one who named them or the Senate relieves them, when they resign, or when what it rested on
/// is gone: the office, the war, the fleet, or the peace between their house and the owner.</summary>
public sealed class ServiceCommand
{
    public string FleetId { get; set; } = "";
    /// <summary>The fleet's name when the command began, for the record once the fleet is gone.</summary>
    public string FleetName { get; set; } = "";
    public string CommanderId { get; set; } = "";
    /// <summary>The commander's own house, which is not <see cref="OwnerId"/>.</summary>
    public string CommanderHouseId { get; set; } = "";
    /// <summary>The house that owns the fleet: its hulls, its keep and its spoils.</summary>
    public string OwnerId { get; set; } = "";
    /// <summary>"legate" or "lent".</summary>
    public string Kind { get; set; } = "";
    /// <summary>What it rests on: "consul", "admiral" or "emergency" for a legate; "war" for an officer lent.</summary>
    public string Authority { get; set; } = "";
    /// <summary>Who named or took them: the office-holder, or the head of the house at war.</summary>
    public string NamedById { get; set; } = "";
    /// <summary>The head of the commander's house who gave leave for them to be lent, or "" when no leave was asked (a legate, or a head who offered themselves).</summary>
    public string LeaveById { get; set; } = "";
    /// <summary>The house the owner was at war with when an officer was lent.</summary>
    public string EnemyId { get; set; } = "";
    /// <summary>They offered themselves for it.</summary>
    public bool Sought { get; set; }
    public int Since { get; set; }
    /// <summary>Battles won and lost under this command.</summary>
    public int Won { get; set; }
    public int Lost { get; set; }
    /// <summary>When it ended, and why, as a clause ("relieved by Corvin Castellan"); 0 and "" while it stands.</summary>
    public int Ended { get; set; }
    public string How { get; set; } = "";
}

/// <summary>Something put to the head of the player's house about a command by service, answered within the year (docs/MILITARY.md,
/// "A command by service"): "lend" (a house at war asks leave to take one of the house as an officer), "offer" (somebody of another
/// house offers to command one of the house's fleets, or one under the head's office), "legate" (an office-holder would name the
/// head legate of a fleet under them).</summary>
public sealed class ServiceAsk
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string FleetId { get; set; } = "";
    /// <summary>Who asks: the head of the house at war, the one who offers, or the office-holder.</summary>
    public string AskerId { get; set; } = "";
    /// <summary>Who would command.</summary>
    public string CharacterId { get; set; } = "";
    public string Authority { get; set; } = "";
    public string EnemyId { get; set; } = "";
    public int Turn { get; set; }
}
