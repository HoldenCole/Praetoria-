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

    /// <summary>Execute if legal. Returns false (and records nothing) if the command can't run.</summary>
    public bool TryExecute(ICommand command, CommandContext ctx)
    {
        if (!command.CanExecute(ctx)) return false;
        command.Execute(ctx);
        _log.Add($"T{ctx.World.Turn} {command.ActorId}: {command.Describe()}");
        return true;
    }
}
