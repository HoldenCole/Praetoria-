namespace Praetoria.Core.Commands;

/// <summary>
/// The single gate every command passes through (BuildSpec §7). It guards with
/// <see cref="ICommand.CanExecute"/>, applies, and records a replay line. Because player and NPC
/// actions share this path, a run is fully described by its seed + command log — the basis for
/// replay and the determinism the Milestone-2 acceptance test checks.
/// </summary>
public sealed class CommandExecutor
{
    private readonly List<string> _log = new();

    /// <summary>Ordered, human-readable record of every command that executed this run.</summary>
    public IReadOnlyList<string> Log => _log;

    /// <summary>Told after every command that executed, the player's and every other: the turn uses it to draw at once a report that answers a
    /// moment the head's own act has made (docs/EVENTS.md, "Moments in the year").</summary>
    public event Action<ICommand, CommandContext>? Executed;

    /// <summary>Execute if legal. Returns false (and records nothing) if the command can't run.</summary>
    public bool TryExecute(ICommand command, CommandContext ctx)
    {
        // the Academy is apart from the world (docs/ACADEMY.md): a cadet answers the Academy's events and does the Academy's business, nothing else
        if (ctx.World.InAcademy && command is not (IAcademyCommand or ResolveChoiceCommand)) return false;
        if (!command.CanExecute(ctx)) return false;
        command.Execute(ctx);
        // a command of the player's can spend or make a favour, a bargain or a war: the gates of the year's reports are written again, so a
        // choice open on the screen is one the engine can keep (docs/events/votes.md, "Promises", round 2). A choice does it itself (EventEngine.Resolve).
        if (command.ActorId == ctx.World.ProtagonistId && command is not ResolveChoiceCommand) Systems.VoteMarks.Refresh(ctx.World, ctx.World.Protagonist?.HouseId);
        Systems.FirstStepsSystem.Acted(ctx.World, command);   // the first years: an act of the player's own ticks its step (docs/ACADEMY.md, "The first years")
        _log.Add($"T{ctx.World.Turn} {command.ActorId}: {command.Describe()}");
        Executed?.Invoke(command, ctx);
        return true;
    }
}
