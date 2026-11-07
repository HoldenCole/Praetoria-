using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

// Fleet intents (GDD §6 military pillar, §9 Action phase "fleet orders"). Like every intent they are
// cost-gated and flow through the shared bus, so NPC houses move fleets exactly as the player does.

/// <summary>Order a fleet along the lanes to a system. Legal when the actor's house commands the fleet, the
/// target is reachable, and the Treasury pool covers the order. One jump per turn; readiness erodes en route.</summary>
public sealed class MoveFleetCommand : ICommand
{
    private readonly string _fleetId, _targetSystemId;
    private readonly FleetRules _rules;

    public MoveFleetCommand(string actorId, string fleetId, string targetSystemId, FleetRules rules)
    {
        ActorId = actorId; _fleetId = fleetId; _targetSystemId = targetSystemId; _rules = rules;
    }

    public string ActorId { get; }
    public string Describe() => $"move {_fleetId} → {_targetSystemId}";

    public bool CanExecute(CommandContext ctx)
    {
        var w = ctx.World;
        var actor = w.Char(ActorId);
        if (actor?.Alive != true || w.Galaxy == null) return false;
        if (!w.Fleets.TryGetValue(_fleetId, out var f) || f.OwnerId != actor.HouseId) return false;
        if (f.Moving || f.SystemId == _targetSystemId) return false;
        if (w.Galaxy.Hops(f.SystemId, _targetSystemId) <= 0) return false;
        return ctx.PoolsOf(ActorId).CanAfford(new Dictionary<string, int> { [Pool.Treasury] = _rules.MovePoolCost });
    }

    public void Execute(CommandContext ctx)
    {
        var w = ctx.World;
        ctx.PoolsOf(ActorId).Spend(new Dictionary<string, int> { [Pool.Treasury] = _rules.MovePoolCost });
        var f = w.Fleets[_fleetId];
        f.DestinationId = _targetSystemId;
        f.Eta = w.Galaxy!.Hops(f.SystemId, _targetSystemId);
        w.Log($"{f.Name} sets course for {w.Galaxy.System(_targetSystemId)?.Name ?? _targetSystemId} ({f.Eta} jump{(f.Eta == 1 ? "" : "s")}).");
    }
}

/// <summary>Lay down ships at one of the actor's holdings (GDD §17 Materials → shipyard → fleet). Spends the
/// house treasury; reinforces the house fleet already in that system or founds one. Office fleets are never
/// raised this way — they come with the post.</summary>
public sealed class RaiseFleetCommand : ICommand
{
    private readonly string _holdingId;
    private readonly FleetRules _rules;

    public RaiseFleetCommand(string actorId, string holdingId, FleetRules rules)
    {
        ActorId = actorId; _holdingId = holdingId; _rules = rules;
    }

    public string ActorId { get; }
    public string Describe() => $"raise ships at {_holdingId}";

    public bool CanExecute(CommandContext ctx)
    {
        var w = ctx.World;
        var actor = w.Char(ActorId);
        if (actor?.Alive != true || w.Galaxy == null) return false;
        var h = w.Holding(_holdingId);
        if (h == null || h.OwnerId != actor.HouseId || h.SystemId == "") return false;
        var house = w.House(actor.HouseId);
        return house != null && house.Treasury.CanAfford(_rules.ShipCost);
    }

    public void Execute(CommandContext ctx)
    {
        var w = ctx.World;
        var actor = w.Char(ActorId)!;
        var house = w.House(actor.HouseId)!;
        var h = w.Holding(_holdingId)!;
        house.Treasury.Spend(_rules.ShipCost);
        house.Treasury.ClampNonCredit();

        var fleet = w.Fleets.Values
            .Where(f => f.OwnerId == house.Id && !f.Office && !f.Moving && f.SystemId == h.SystemId)
            .OrderBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
        if (fleet != null)
        {
            fleet.Strength += 1;
            w.Log($"{fleet.Name} takes on a new hull at {h.Name} (strength {fleet.Strength}).");
        }
        else
        {
            int n = w.Counter("fleets_raised") + 1;
            w.WorldCounters["fleets_raised"] = n;
            fleet = new Fleet { Id = $"fleet_{house.Id}_{n}", Name = $"{house.Name} Squadron", OwnerId = house.Id, Strength = 1, SystemId = h.SystemId, Readiness = 60 };
            w.Fleets[fleet.Id] = fleet;
            w.Log($"{house.Name} lays down a squadron at {h.Name}.");
        }
        w.WorldCounters["ships_raised"] = w.Counter("ships_raised") + 1;
    }
}
