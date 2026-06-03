using Praetoria.Core.State;

namespace Praetoria.Core.Commands;

// Light, abstracted intents an NPC house (or the player) can spend a pool on (GDD §3, §9).
// These are the Academy-era verbs; the galaxy-scale command set grows in later milestones, but the
// shape is fixed here: cost-gated, deterministic, routed through the executor.

/// <summary>Drill to sharpen a skill. Cost: 1 agent (attention). Effect: +1 to the named skill.</summary>
public sealed class TrainCommand : ICommand
{
    private static readonly Dictionary<string, int> CostMap = new() { [Pool.Agents] = 1 };
    private readonly string _skill;

    public TrainCommand(string actorId, string skill) { ActorId = actorId; _skill = skill; }

    public string ActorId { get; }
    public string Describe() => $"train {_skill}";

    public bool CanExecute(CommandContext ctx) =>
        Alive(ctx, ActorId) && ctx.PoolsOf(ActorId).CanAfford(CostMap);

    public void Execute(CommandContext ctx)
    {
        ctx.PoolsOf(ActorId).Spend(CostMap);
        var c = ctx.World.Char(ActorId)!;
        c.Skills[_skill] = c.Skill(_skill) + 1;
    }

    private static bool Alive(CommandContext ctx, string id) => ctx.World.Char(id)?.Alive == true;
}

/// <summary>Undermine a rival. Cost: 1 influence. Effect: sours the rival's view of the actor.</summary>
public sealed class NeedleRivalCommand : ICommand
{
    private static readonly Dictionary<string, int> CostMap = new() { [Pool.Influence] = 1 };
    private readonly string _targetId;

    public NeedleRivalCommand(string actorId, string targetId) { ActorId = actorId; _targetId = targetId; }

    public string ActorId { get; }
    public string Describe() => $"needle {_targetId}";

    public bool CanExecute(CommandContext ctx) =>
        ctx.World.Char(ActorId)?.Alive == true &&
        ctx.World.Char(_targetId)?.Alive == true &&
        ctx.PoolsOf(ActorId).CanAfford(CostMap);

    public void Execute(CommandContext ctx)
    {
        ctx.PoolsOf(ActorId).Spend(CostMap);
        ctx.World.Relationship(_targetId, ActorId).Disposition =
            Math.Clamp(ctx.World.Relationship(_targetId, ActorId).Disposition - 10, -100, 100);
        ctx.World.Relationship(ActorId, _targetId).Disposition =
            Math.Clamp(ctx.World.Relationship(ActorId, _targetId).Disposition - 5, -100, 100);
    }
}

/// <summary>Court an ally. Cost: 1 influence. Effect: warms disposition both ways.</summary>
public sealed class SeekAllyCommand : ICommand
{
    private static readonly Dictionary<string, int> CostMap = new() { [Pool.Influence] = 1 };
    private readonly string _targetId;

    public SeekAllyCommand(string actorId, string targetId) { ActorId = actorId; _targetId = targetId; }

    public string ActorId { get; }
    public string Describe() => $"seek ally {_targetId}";

    public bool CanExecute(CommandContext ctx) =>
        ctx.World.Char(ActorId)?.Alive == true &&
        ctx.World.Char(_targetId)?.Alive == true &&
        ctx.PoolsOf(ActorId).CanAfford(CostMap);

    public void Execute(CommandContext ctx)
    {
        ctx.PoolsOf(ActorId).Spend(CostMap);
        foreach (var (a, b) in new[] { (ActorId, _targetId), (_targetId, ActorId) })
            ctx.World.Relationship(a, b).Disposition =
                Math.Clamp(ctx.World.Relationship(a, b).Disposition + 10, -100, 100);
    }
}
