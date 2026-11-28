namespace Praetoria.Core.State;

/// <summary>
/// The first years of a first game (docs/ACADEMY.md, "The first years"): a short list of steps pinned at the top of the list on the right,
/// written for the start the saga began from, each ticking itself off from what the player really did. What is kept here is what happened,
/// not which boxes are ticked: the acts the player has done (<see cref="Did"/>) and the windows and tabs they have opened (<see cref="Seen"/>).
/// Which step those tick is read from the content each time (<see cref="Systems.FirstStepsSystem"/>), so a step ticks the same way after a
/// save is loaded. Serialised with the World; null in a saga that is not anyone's first.
/// </summary>
public sealed class FirstSteps
{
    /// <summary>The start the steps are written for: a scenario id, or "custom" for a character of the player's own making.</summary>
    public string Start { get; set; } = "";
    /// <summary>The steps of this saga, in their order: the start's list, kept to what the house can do (a step for a fleet needs a fleet).</summary>
    public List<string> Steps { get; set; } = new();
    /// <summary>The saga opened at the Academy: the list waits until the cadet passes out, and is not shown at all to a cadet who asked
    /// only to be briefed (docs/ACADEMY.md, "The tutorial").</summary>
    public bool Academy { get; set; }
    /// <summary>What the player has done by their own hand: "report", "vote", "fleet", "buy", "venture", "build", "law".</summary>
    public HashSet<string> Did { get; set; } = new();
    /// <summary>The windows and tabs the player has opened ("character", "house:ledger") and "map" for a world picked on the map.</summary>
    public HashSet<string> Seen { get; set; } = new();
    /// <summary>The player put the list away.</summary>
    public bool Dismissed { get; set; }
}
