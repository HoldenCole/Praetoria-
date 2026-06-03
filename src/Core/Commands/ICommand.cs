namespace Praetoria.Core.Commands;

/// <summary>
/// A player/AI intent that mutates State (BuildSpec §7 "Command pattern"). ALL state mutation
/// flows through commands so the player and NPC houses share one interface — which gives replay,
/// testability, deterministic AI, and (later) undo. A command knows who is acting, whether it can
/// currently act (pools/requirements), and how to apply itself.
/// </summary>
public interface ICommand
{
    /// <summary>The acting character id (the protagonist, or an NPC house's actor).</summary>
    string ActorId { get; }

    /// <summary>Human/debug description of the intent (also the replay-log line).</summary>
    string Describe();

    /// <summary>True if the intent is legal right now: requirements met and pools affordable.
    /// Must not mutate State.</summary>
    bool CanExecute(CommandContext ctx);

    /// <summary>Apply the intent. Deterministic given State + RNG. Callers should check
    /// <see cref="CanExecute"/> first (the executor does).</summary>
    void Execute(CommandContext ctx);
}
