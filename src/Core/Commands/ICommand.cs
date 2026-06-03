using Praetoria.Core.State;

namespace Praetoria.Core.Commands;

/// <summary>
/// A player/AI intent that mutates State (BuildSpec §7 "Command pattern"). ALL state mutation
/// is meant to flow through commands so player and NPC houses share one interface — giving
/// replay, testability, and (later) undo. Milestone 1 ships the contract and one command;
/// Milestone 2 formalises the turn/pool economy and routes NPC actions through here too.
/// </summary>
public interface ICommand
{
    /// <summary>Human/debug description of the intent.</summary>
    string Describe();

    /// <summary>Apply the intent to the world. Implementations must be deterministic given State.</summary>
    void Execute(World world);
}
