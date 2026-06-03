using Praetoria.Core.Events;

namespace Praetoria.Core.Commands;

/// <summary>
/// The player's act of answering a briefing item (GDD §9 Action phase). Routes event-choice
/// resolution through the command bus so it shares the spend/replay path with NPC actions
/// (BuildSpec §7). Spends the choice's pool cost, then applies its consequences via the engine.
/// </summary>
public sealed class ResolveChoiceCommand : ICommand
{
    private readonly EventEngine _engine;
    private readonly FiredEvent _fired;
    private readonly string _choiceId;

    public ResolveChoiceCommand(EventEngine engine, FiredEvent fired, string choiceId, string actorId)
    {
        _engine = engine;
        _fired = fired;
        _choiceId = choiceId;
        ActorId = actorId;
    }

    public string ActorId { get; }

    public string Describe() => $"resolve {_fired.Def.Id} → {_choiceId}";

    public bool CanExecute(CommandContext ctx)
    {
        var choice = _fired.Def.Choice(_choiceId);
        if (choice == null) return false;
        if (!_engine.IsChoiceAvailable(_fired, ctx.World, _choiceId)) return false;
        return ctx.PoolsOf(ActorId).CanAfford(choice.Cost);
    }

    public void Execute(CommandContext ctx)
    {
        var choice = _fired.Def.Choice(_choiceId)!;
        ctx.PoolsOf(ActorId).Spend(choice.Cost);
        _engine.Resolve(_fired, ctx.World, _choiceId);
    }
}
