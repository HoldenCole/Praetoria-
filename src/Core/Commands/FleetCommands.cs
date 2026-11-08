using Praetoria.Core.Progression;
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
        if (w.Galaxy.Hops(f.SystemId, _targetSystemId, Barred(w, f.OwnerId)) <= 0) return false;
        if (FleetSystem.Unfuelled(w, f.OwnerId)) return false;   // the tanks are dry (GDD §17 fuel)
        return ctx.PoolsOf(ActorId).CanAfford(new Dictionary<string, int> { [Pool.Treasury] = _rules.MovePoolCost });
    }

    /// <summary>A chokepoint whose world is held by a house at war with the mover, with that house's fleet sitting there, cannot be
    /// passed through (GDD §19 chokepoints): the route must go round, or there is no route.</summary>
    public static Func<string, bool> Barred(World w, string moverHouse) => sys =>
        w.Galaxy != null && w.Galaxy.IsChokepoint(sys) && w.PlanetHolder(sys) is { } holder && holder != moverHouse && w.AtWar(holder, moverHouse)
        && w.Fleets.Values.Any(x => x.OwnerId == holder && !x.Moving && x.SystemId == sys);

    public void Execute(CommandContext ctx)
    {
        var w = ctx.World;
        ctx.PoolsOf(ActorId).Spend(new Dictionary<string, int> { [Pool.Treasury] = _rules.MovePoolCost });
        var f = w.Fleets[_fleetId];
        f.DestinationId = _targetSystemId;
        f.Eta = w.Galaxy!.Hops(f.SystemId, _targetSystemId, Barred(w, f.OwnerId));
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
    private readonly ShipClassCatalog _classes;
    private readonly string _classId;
    private readonly TitleCatalog? _titles;

    /// <summary>Lay down one hull of <paramref name="classId"/> (null ⇒ the rules' default class).</summary>
    public RaiseFleetCommand(string actorId, string holdingId, FleetRules rules, ShipClassCatalog? classes = null, string? classId = null, TitleCatalog? titles = null)
    {
        ActorId = actorId; _holdingId = holdingId; _rules = rules;
        _classes = classes ?? ShipClassCatalog.Empty; _classId = classId ?? rules.DefaultClass; _titles = titles;
    }

    public string ActorId { get; }
    public string ClassId => _classId;
    public string Describe() => $"raise {_classId} at {_holdingId}";

    private ShipClassDef Cls => _classes.Resolve(_classId);

    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World;
        var actor = w.Char(ActorId);
        if (actor?.Alive != true || w.Galaxy == null) return "no actor";
        var h = w.Holding(_holdingId);
        if (h == null || h.OwnerId != actor.HouseId || h.SystemId == "") return "not your holding";
        var cls = Cls;
        if (_classes.ById(_classId) == null && _classId != _rules.DefaultClass) return $"unknown class {_classId}";
        if (cls.Requires != null && cls.Requires != h.Specialization) return $"needs a {cls.Requires}";
        var house = w.House(actor.HouseId);
        if (house == null) return "no house";
        if (cls.MinTitleRank > 0 && (_titles?.RankOf(house.Title) ?? 0) < cls.MinTitleRank) return $"needs title rank {cls.MinTitleRank}";
        return house.Treasury.CanAfford(cls.Cost) ? null : "cannot afford it";
    }

    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;

    public void Execute(CommandContext ctx)
    {
        var w = ctx.World;
        var actor = w.Char(ActorId)!;
        var house = w.House(actor.HouseId)!;
        var h = w.Holding(_holdingId)!;
        var cls = Cls;
        house.Treasury.Spend(cls.Cost);
        house.Treasury.ClampNonCredit();

        var fleet = w.Fleets.Values
            .Where(f => f.OwnerId == house.Id && !f.Office && !f.Moving && f.SystemId == h.SystemId)
            .OrderBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
        if (fleet == null)
        {
            int n = w.Counter("fleets_raised") + 1;
            w.WorldCounters["fleets_raised"] = n;
            fleet = new Fleet { Id = $"fleet_{house.Id}_{n}", Name = $"{house.Name} Squadron", OwnerId = house.Id, Strength = 0, SystemId = h.SystemId, Readiness = 60 };
            w.Fleets[fleet.Id] = fleet;
            w.Log($"{house.Name} lays down a squadron at {h.Name}.");
        }
        if (fleet.Ships.Count == 0 && fleet.Strength > 0) fleet.Ships[_rules.DefaultClass] = fleet.Strength;   // materialise the default composition
        fleet.Ships[cls.Id] = fleet.Ships.GetValueOrDefault(cls.Id) + 1;
        fleet.Recount();
        w.Log($"{fleet.Name} takes on a {cls.Name.ToLowerInvariant()} at {h.Name} ({fleet.Strength} hulls).");
        w.WorldCounters["ships_raised"] = w.Counter("ships_raised") + 1;
    }
}

/// <summary>Give a fleet an admiral (GDD §6 "the admiral's skill"). The character must be a living member of the
/// owning house, present in or bound for the fleet's system is NOT required (command is a commission); one
/// command per character. Costs 1 Agent.</summary>
public sealed class AssignAdmiralCommand : ICommand
{
    private static readonly Dictionary<string, int> CostMap = new() { [Pool.Agents] = 1 };
    private readonly string _fleetId, _characterId;
    public AssignAdmiralCommand(string actorId, string fleetId, string characterId) { ActorId = actorId; _fleetId = fleetId; _characterId = characterId; }
    public string ActorId { get; }
    public string Describe() => $"appoint {_characterId} to {_fleetId}";

    public bool CanExecute(CommandContext ctx)
    {
        var w = ctx.World;
        var actor = w.Char(ActorId); var c = w.Char(_characterId);
        if (actor?.Alive != true || c?.Alive != true) return false;
        if (!w.Fleets.TryGetValue(_fleetId, out var f) || f.OwnerId != actor.HouseId || c.HouseId != actor.HouseId) return false;
        if (f.AdmiralId == _characterId) return false;
        if (w.Fleets.Values.Any(x => x.Id != _fleetId && x.AdmiralId == _characterId)) return false;
        return ctx.PoolsOf(ActorId).CanAfford(CostMap);
    }

    public void Execute(CommandContext ctx)
    {
        var w = ctx.World;
        ctx.PoolsOf(ActorId).Spend(CostMap);
        var f = w.Fleets[_fleetId]; var c = w.Char(_characterId)!;
        f.AdmiralId = c.Id;
        FleetSystem.RefreshAdmiralFlags(w);
        w.Log($"{c.Name} takes command of {f.Name}.");
    }
}

/// <summary>Refit a fleet at one of the house's own systems (GDD §6 readiness): pays materials per hull from the
/// treasury and restores readiness to full. Cannot refit underway or in foreign space.</summary>
public sealed class RefitFleetCommand : ICommand
{
    public const int MaterialsPerHull = 2;
    private readonly string _fleetId;
    public RefitFleetCommand(string actorId, string fleetId) { ActorId = actorId; _fleetId = fleetId; }
    public string ActorId { get; }
    public string Describe() => $"refit {_fleetId}";
    public bool CanExecute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId);
        if (actor?.Alive != true || !w.Fleets.TryGetValue(_fleetId, out var f) || f.OwnerId != actor.HouseId) return false;
        if (f.Moving || f.Readiness >= 100) return false;
        if (!w.Holdings.Values.Any(h => h.OwnerId == actor.HouseId && h.SystemId == f.SystemId)) return false;
        return (w.House(actor.HouseId)?.Treasury.Materials ?? 0) >= MaterialsPerHull * f.Strength;
    }
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var f = w.Fleets[_fleetId]; var house = w.House(w.Char(ActorId)!.HouseId)!;
        house.Treasury.Materials -= MaterialsPerHull * f.Strength;
        f.Readiness = 100;
        w.Log($"{f.Name} is refitted at {w.Galaxy?.System(f.SystemId)?.Name ?? f.SystemId}.");
    }
}

/// <summary>Fold one fleet into another in the same system (same house). The receiving fleet keeps its admiral.</summary>
public sealed class MergeFleetsCommand : ICommand
{
    private readonly string _fromId, _intoId;
    public MergeFleetsCommand(string actorId, string fromFleetId, string intoFleetId) { ActorId = actorId; _fromId = fromFleetId; _intoId = intoFleetId; }
    public string ActorId { get; }
    public string Describe() => $"merge {_fromId} into {_intoId}";

    public bool CanExecute(CommandContext ctx)
    {
        var w = ctx.World;
        var actor = w.Char(ActorId);
        if (actor?.Alive != true || _fromId == _intoId) return false;
        if (!w.Fleets.TryGetValue(_fromId, out var a) || !w.Fleets.TryGetValue(_intoId, out var b)) return false;
        return a.OwnerId == actor.HouseId && b.OwnerId == actor.HouseId && !a.Moving && !b.Moving && a.SystemId == b.SystemId && a.Office == b.Office;
    }

    public void Execute(CommandContext ctx)
    {
        var w = ctx.World;
        var a = w.Fleets[_fromId]; var b = w.Fleets[_intoId];
        string def = ctx.World.Fleets.Count > 0 ? "frigate" : "frigate";
        if (a.Ships.Count == 0 && a.Strength > 0) a.Ships[def] = a.Strength;
        if (b.Ships.Count == 0 && b.Strength > 0) b.Ships[def] = b.Strength;
        foreach (var kv in a.Ships) b.Ships[kv.Key] = b.Ships.GetValueOrDefault(kv.Key) + kv.Value;
        b.Recount();
        b.Readiness = (a.Readiness * a.Strength + b.Readiness * (b.Strength - a.Strength)) / Math.Max(1, b.Strength);
        b.Veterancy = Math.Max(a.Veterancy, b.Veterancy);
        if (b.AdmiralId == "" && a.AdmiralId != "") b.AdmiralId = a.AdmiralId;
        w.Fleets.Remove(a.Id); w.Intel.Remove(a.Id);
        FleetSystem.RefreshAdmiralFlags(w);
        w.Log($"{a.Name} is folded into {b.Name} ({b.Strength} hulls).");
    }
}
