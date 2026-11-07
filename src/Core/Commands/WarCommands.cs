using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>Declare war on another house (GDD §6/§13 Military path). Costs Influence; sours the two heads.</summary>
public sealed class DeclareWarCommand : ICommand
{
    private readonly string _targetHouseId;
    private readonly FleetRules _rules;
    public DeclareWarCommand(string actorId, string targetHouseId, FleetRules rules) { ActorId = actorId; _targetHouseId = targetHouseId; _rules = rules; }

    public string ActorId { get; }
    public string Describe() => $"declare war on {_targetHouseId}";

    public bool CanExecute(CommandContext ctx)
    {
        var w = ctx.World;
        var actor = w.Char(ActorId);
        if (actor?.Alive != true || actor.HouseId == _targetHouseId || w.House(_targetHouseId) == null) return false;
        if (w.AtWar(actor.HouseId, _targetHouseId)) return false;
        return ctx.PoolsOf(ActorId).CanAfford(new Dictionary<string, int> { [Pool.Influence] = _rules.DeclareWarCost });
    }

    public void Execute(CommandContext ctx)
    {
        var w = ctx.World;
        var actor = w.Char(ActorId)!;
        ctx.PoolsOf(ActorId).Spend(new Dictionary<string, int> { [Pool.Influence] = _rules.DeclareWarCost });
        new FleetSystem(_rules).DeclareWar(w, actor.HouseId, _targetHouseId, ActorId);
        foreach (var head in w.LivingCharacters().Where(c => c.HouseId == _targetHouseId))
            w.Relationship(head.Id, ActorId).Disposition = Math.Clamp(w.Relationship(head.Id, ActorId).Disposition - 30, -100, 100);
    }
}

/// <summary>Sue for peace. Costs Influence. Toward an NPC house: refused while it smells victory (its hulls far
/// outnumber yours), otherwise peace at once. Toward the player's house: never auto-resolved — the actor's head is
/// flagged <c>suing_for_peace</c> and the player answers through the envoy event (GDD §15: decisions are scenes).</summary>
public sealed class SueForPeaceCommand : ICommand
{
    private readonly string _targetHouseId;
    private readonly FleetRules _rules;
    public SueForPeaceCommand(string actorId, string targetHouseId, FleetRules rules) { ActorId = actorId; _targetHouseId = targetHouseId; _rules = rules; }

    public string ActorId { get; }
    public string Describe() => $"sue for peace with {_targetHouseId}";

    public bool CanExecute(CommandContext ctx)
    {
        var w = ctx.World;
        var actor = w.Char(ActorId);
        if (actor?.Alive != true || !w.AtWar(actor.HouseId, _targetHouseId)) return false;
        bool toPlayer = _targetHouseId == w.Protagonist?.HouseId;
        if (toPlayer && actor.Flags.Contains(FleetSystem.SuingForPeaceFlag)) return false;       // offer already on the table
        if (!toPlayer && !new FleetSystem(_rules).WouldAcceptPeace(w, _targetHouseId, actor.HouseId)) return false;
        return ctx.PoolsOf(ActorId).CanAfford(new Dictionary<string, int> { [Pool.Influence] = _rules.PeaceCost });
    }

    public void Execute(CommandContext ctx)
    {
        var w = ctx.World;
        var actor = w.Char(ActorId)!;
        ctx.PoolsOf(ActorId).Spend(new Dictionary<string, int> { [Pool.Influence] = _rules.PeaceCost });
        if (_targetHouseId == w.Protagonist?.HouseId)
        {
            actor.Flags.Add(FleetSystem.SuingForPeaceFlag);                                        // the player decides
            w.Log($"{actor.Name} of {w.House(actor.HouseId)?.Name ?? actor.HouseId} sues for peace.");
            return;
        }
        var fleets = new FleetSystem(_rules);
        int tribute = fleets.Tribute(w, actor.HouseId, _targetHouseId);          // the side behind on score pays
        if (tribute > 0)
        {
            w.House(actor.HouseId)!.Treasury.Add(Resource.Credits, -tribute);
            w.House(_targetHouseId)!.Treasury.Add(Resource.Credits, tribute);
            w.Log($"{w.House(actor.HouseId)?.Name} pays {tribute} credits in tribute.");
        }
        fleets.MakePeace(w, actor.HouseId, _targetHouseId);
        foreach (var head in w.LivingCharacters().Where(c => c.HouseId == _targetHouseId))
            w.Relationship(head.Id, ActorId).Disposition = Math.Clamp(w.Relationship(head.Id, ActorId).Disposition + 10, -100, 100);
    }
}
