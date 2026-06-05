using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

// The three rise paths (GDD §13) as commands through the shared bus. Each converts what the actor
// has into a title rung at a different exchange rate and risk. Title costs spend the per-turn pools
// (§9); the deeper costs (legitimacy, fear, a trail of corruption) are paid in State.

/// <summary>Military path — take the title by force. Needs martial power; ignores legitimacy.</summary>
public sealed class SeizeTitleCommand : ICommand
{
    private static readonly Dictionary<string, int> CostMap = new() { [Pool.Treasury] = 2, [Pool.Agents] = 1 };
    private readonly ProgressionSystem _prog;
    public SeizeTitleCommand(string actorId, ProgressionSystem prog) { ActorId = actorId; _prog = prog; }

    public string ActorId { get; }
    public string Describe() => "seize title";
    public bool CanExecute(CommandContext ctx) =>
        _prog.CanSeize(ctx.World, ActorId) && ctx.PoolsOf(ActorId).CanAfford(CostMap);
    public void Execute(CommandContext ctx)
    {
        ctx.PoolsOf(ActorId).Spend(CostMap);
        _prog.Seize(ctx.World, ActorId);
    }
}

/// <summary>Merit path — be granted the title. Capped by the soft-lock; arrives clean.</summary>
public sealed class PetitionTitleCommand : ICommand
{
    private static readonly Dictionary<string, int> CostMap = new() { [Pool.Influence] = 2 };
    private readonly ProgressionSystem _prog;
    public PetitionTitleCommand(string actorId, ProgressionSystem prog) { ActorId = actorId; _prog = prog; }

    public string ActorId { get; }
    public string Describe() => "petition for title";
    public bool CanExecute(CommandContext ctx) =>
        _prog.CanPetition(ctx.World, ActorId) && ctx.PoolsOf(ActorId).CanAfford(CostMap);
    public void Execute(CommandContext ctx)
    {
        ctx.PoolsOf(ActorId).Spend(CostMap);
        _prog.Petition(ctx.World, ActorId);
    }
}

/// <summary>Intrigue path — press a claim. Needs a claim in hand; launders legitimacy.</summary>
public sealed class ClaimTitleCommand : ICommand
{
    private static readonly Dictionary<string, int> CostMap = new() { [Pool.Influence] = 1, [Pool.Agents] = 2 };
    private readonly ProgressionSystem _prog;
    public ClaimTitleCommand(string actorId, ProgressionSystem prog) { ActorId = actorId; _prog = prog; }

    public string ActorId { get; }
    public string Describe() => "claim title by blood";
    public bool CanExecute(CommandContext ctx) =>
        _prog.CanClaimByBlood(ctx.World, ActorId) && ctx.PoolsOf(ActorId).CanAfford(CostMap);
    public void Execute(CommandContext ctx)
    {
        ctx.PoolsOf(ActorId).Spend(CostMap);
        _prog.ClaimByBlood(ctx.World, ActorId);
    }
}
