using Praetoria.Core.Progression;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>Proclaim the Imperium (GDD §10 fractured-stars endgame, Merit/Intrigue flavour). Costs 3 Influence + 2 Agents.</summary>
public sealed class ProclaimImperiumCommand : ICommand
{
    private static readonly Dictionary<string, int> CostMap = new() { [Pool.Influence] = 3, [Pool.Agents] = 2 };
    private readonly TitleCatalog _titles;
    public ProclaimImperiumCommand(string actorId, TitleCatalog titles) { ActorId = actorId; _titles = titles; }
    public string ActorId { get; }
    public string Describe() => "proclaim the Imperium";
    public string? Refusal(CommandContext ctx)
    {
        var actor = ctx.World.Char(ActorId);
        if (actor?.Alive != true) return "no actor";
        return ThroneSystem.ProclaimRefusal(ctx.World, actor.HouseId, _titles) ?? (ctx.PoolsOf(ActorId).CanAfford(CostMap) ? null : "not enough influence and agents");
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        ctx.PoolsOf(ActorId).Spend(CostMap);
        ThroneSystem.Enthrone(ctx.World, ctx.World.Char(ActorId)!.HouseId, seized: false, ActorId);
    }
}

/// <summary>Seize the throne by force (GDD §13 Military — the highest ceiling, the whole galaxy your enemy). Costs 2 Treasury + 2 Agents.</summary>
public sealed class SeizeThroneCommand : ICommand
{
    private static readonly Dictionary<string, int> CostMap = new() { [Pool.Treasury] = 2, [Pool.Agents] = 2 };
    private readonly TitleCatalog _titles;
    private readonly FrictionTable _friction;
    public SeizeThroneCommand(string actorId, TitleCatalog titles, FrictionTable? friction = null) { ActorId = actorId; _titles = titles; _friction = friction ?? FrictionTable.Empty; }
    public string ActorId { get; }
    public string Describe() => "seize the throne";
    public string? Refusal(CommandContext ctx)
    {
        var actor = ctx.World.Char(ActorId);
        if (actor?.Alive != true) return "no actor";
        return ThroneSystem.SeizeRefusal(ctx.World, actor.HouseId, _titles) ?? (ctx.PoolsOf(ActorId).CanAfford(CostMap) ? null : "not enough treasury and agents");
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        ctx.PoolsOf(ActorId).Spend(CostMap);
        var actor = ctx.World.Char(ActorId)!;
        ThroneSystem.Enthrone(ctx.World, actor.HouseId, seized: true, ActorId);
        _friction.Apply(ctx.World, actor, "conquest");
    }
}

/// <summary>A rising against the throne (docs/THRONE.md): raise your own standard (3 Influence), or take a side in another house's:
/// go over to the pretender, or stand by the throne.</summary>
public sealed class ThroneCommand : ICommand
{
    public const string Raise = "raise", Pretender = "pretender", Throne = "throne";
    private static readonly Dictionary<string, int> CostMap = new() { [Pool.Influence] = 3 };
    private readonly string _what; private readonly TitleCatalog _titles; private readonly FleetSystem _fleets;
    public ThroneCommand(string actorId, string what, TitleCatalog titles, FleetSystem fleets) { ActorId = actorId; _what = what; _titles = titles; _fleets = fleets; }
    public string ActorId { get; }
    public string Describe() => _what == Raise ? "raise a standard against the throne" : _what == Pretender ? "go over to the pretender" : "stand by the throne";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor?.Alive == true ? w.House(actor.HouseId) : null;
        if (house == null) return "no actor";
        if (actor!.Id != w.ProtagonistId && Economy.HeadOf(w, house)?.Id != actor.Id) return "the head of the house decides this";
        if (_what == Raise) return ThroneSystem.RiseRefusal(w, house, _titles) ?? (ctx.PoolsOf(ActorId).CanAfford(CostMap) ? null : "not enough influence");
        return ThroneSystem.BackRefusal(w, house, _what);
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var house = w.House(w.Char(ActorId)!.HouseId)!;
        if (_what == Raise) { ctx.PoolsOf(ActorId).Spend(CostMap); ThroneSystem.Rise(w, house, _fleets); }
        else ThroneSystem.Back(w, house, _what, _fleets);
    }
}
