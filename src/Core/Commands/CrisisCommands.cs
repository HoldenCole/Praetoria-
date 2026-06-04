using Praetoria.Core.Crises;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

// Crisis intents (GDD §16). Like every other intent they flow through the command bus, so the
// player and ambitious NPC houses author crises by the SAME path — the "crises have authors" rule.

/// <summary>
/// Deliberately onset a causable crisis (GDD §16 "authored" origin) because it serves the actor's
/// ambition. Legal only while the crisis's gates are cleared — you cannot conjure a civil war from
/// nothing, only light the fuse on one the world has already made possible.
/// </summary>
public sealed class TriggerCrisisCommand : ICommand
{
    private readonly CrisisEngine _engine;
    private readonly CrisisDef _crisis;

    public TriggerCrisisCommand(string actorId, CrisisEngine engine, CrisisDef crisis)
    {
        ActorId = actorId; _engine = engine; _crisis = crisis;
    }

    public string ActorId { get; }
    public string Describe() => $"trigger crisis {_crisis.Id}";

    public bool CanExecute(CommandContext ctx) =>
        ctx.World.Char(ActorId)?.Alive == true && _engine.IsCausable(_crisis, ctx.World, ctx.Rng);

    public void Execute(CommandContext ctx) => _engine.Trigger(_crisis, ctx.World, ctx.Rng, ActorId);
}

/// <summary>
/// Apply a damper to an active crisis to arrest it (GDD §16). Legal only when the crisis is active,
/// the damper's availability conditions hold (the "bill for how you played" gate), and any pool
/// cost is affordable. Reduces the crisis's severity; at zero the crisis resolves.
/// </summary>
public sealed class ApplyDamperCommand : ICommand
{
    private readonly CrisisEngine _engine;
    private readonly CrisisDef _crisis;
    private readonly DamperDef _damper;

    public ApplyDamperCommand(string actorId, CrisisEngine engine, CrisisDef crisis, DamperDef damper)
    {
        ActorId = actorId; _engine = engine; _crisis = crisis; _damper = damper;
    }

    public string ActorId { get; }
    public string Describe() => $"damper {_damper.Id} on {_crisis.Id}";

    public bool CanExecute(CommandContext ctx)
    {
        if (ctx.World.Char(ActorId)?.Alive != true) return false;
        if (!ctx.World.IsCrisisActive(_crisis.Id)) return false;
        if (!ctx.PoolsOf(ActorId).CanAfford(_damper.Cost)) return false;
        return _engine.AvailableDampers(_crisis, ctx.World, ctx.Rng).Any(d => d.Id == _damper.Id);
    }

    public void Execute(CommandContext ctx)
    {
        ctx.PoolsOf(ActorId).Spend(_damper.Cost);
        _engine.ApplyDamper(_crisis, _damper, ctx.World, ctx.Rng);
    }
}
