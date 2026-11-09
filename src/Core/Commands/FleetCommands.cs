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
        if (f.Loyalty < _rules.LoyaltyRefuse) return false;      // the crews will not sail (docs/MILITARY.md loyalty)
        return ctx.PoolsOf(ActorId).CanAfford(new Dictionary<string, int> { [Pool.Treasury] = _rules.MovePoolCost });
    }
    /// <summary>Why the order would be refused, for the screens.</summary>
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId);
        if (actor?.Alive != true || w.Galaxy == null) return "no one to give the order";
        if (!w.Fleets.TryGetValue(_fleetId, out var f) || f.OwnerId != actor.HouseId) return "not your fleet";
        if (f.Moving) return "already under way"; if (f.SystemId == _targetSystemId) return "already there";
        if (w.Galaxy.Hops(f.SystemId, _targetSystemId, Barred(w, f.OwnerId)) <= 0) return "no route";
        if (FleetSystem.Unfuelled(w, f.OwnerId)) return "the tanks are dry";
        if (f.Loyalty < _rules.LoyaltyRefuse) return "the crews will not sail — loyalty " + f.Loyalty;
        return ctx.PoolsOf(ActorId).CanAfford(new Dictionary<string, int> { [Pool.Treasury] = _rules.MovePoolCost }) ? null : "needs a treasury action";
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


/// <summary>Set how a fleet fights (docs/MILITARY.md tactics): line, press, skirmish or boarding. Free; a fleet under way keeps its stance until it arrives.</summary>
public sealed class SetStanceCommand : ICommand
{
    private readonly string _fleetId, _stance;
    public SetStanceCommand(string actorId, string fleetId, string stance) { ActorId = actorId; _fleetId = fleetId; _stance = stance; }
    public string ActorId { get; }
    public string Describe() => $"{_fleetId}: stance {_stance}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); if (actor?.Alive != true) return "no one to give the order";
        if (!w.Fleets.TryGetValue(_fleetId, out var f) || f.OwnerId != actor.HouseId) return "not your fleet";
        if (!FleetSystem.Stances.Contains(_stance)) return "no such stance";
        if (f.Stance == _stance) return "already so";
        if (_stance == "boarding" && (w.House(f.OwnerId)?.Treasury.Manpower ?? 0) < 2) return "boarding wants prize crews — 2 manpower in hand";
        return null;
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { var w = ctx.World; var f = w.Fleets[_fleetId]; f.Stance = _stance; w.Log($"{f.Name} will fight in {(_stance == "line" ? "the line" : _stance == "press" ? "the press" : _stance == "skirmish" ? "skirmish order" : "boarding order")}."); }
}

/// <summary>Set what a fleet fights with: guns, or torpedoes that burn an isotope a battle for a quarter more power. Loading torpedoes is a
/// refit at one of the house's own systems with an isotope in store; guns may be run out anywhere.</summary>
public sealed class SetLoadoutCommand : ICommand
{
    private readonly string _fleetId, _loadout;
    public SetLoadoutCommand(string actorId, string fleetId, string loadout) { ActorId = actorId; _fleetId = fleetId; _loadout = loadout; }
    public string ActorId { get; }
    public string Describe() => $"{_fleetId}: load {_loadout}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); if (actor?.Alive != true) return "no one to give the order";
        if (!w.Fleets.TryGetValue(_fleetId, out var f) || f.OwnerId != actor.HouseId) return "not your fleet";
        if (!FleetSystem.Loadouts.Contains(_loadout)) return "no such loadout";
        if (f.Loadout == _loadout) return "already so";
        if (_loadout == "torpedoes")
        {
            if (f.Moving || !w.Holdings.Values.Any(h => h.OwnerId == actor.HouseId && h.SystemId == f.SystemId)) return "torpedoes are loaded at a world of your own";
            if ((w.House(f.OwnerId)?.Treasury.Isotopes ?? 0) < 1) return "no isotopes in store";
        }
        return null;
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { var w = ctx.World; var f = w.Fleets[_fleetId]; f.Loadout = _loadout; w.Log($"{f.Name} {(_loadout == "torpedoes" ? "loads torpedoes" : "runs out its guns")}."); }
}

/// <summary>Where a commander stands (docs/MILITARY.md command): at the FRONT — the fleet fights harder, the crews love it, the commander may
/// die, and victories build valour toward the Achilles name — or at the FLAG, lending every fleet of the house a share of their tactics and
/// never going down with the line. "none" is the default. Free; the actor sets their own style, or a kinsman's.</summary>
public sealed class SetCommandStyleCommand : ICommand
{
    private readonly string _characterId, _style;
    public SetCommandStyleCommand(string actorId, string characterId, string style) { ActorId = actorId; _characterId = characterId; _style = style; }
    public string ActorId { get; }
    public string Describe() => $"{_characterId}: command from the {_style}";
    public static string StyleOf(Character c) => c.Flags.Contains(FleetSystem.FrontFlag) ? "front" : c.Flags.Contains(FleetSystem.FlagFlag) ? "flag" : "none";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var c = w.Char(_characterId);
        if (actor?.Alive != true || c?.Alive != true || c.HouseId != actor.HouseId) return "not of your house";
        if (_style is not ("front" or "flag" or "none")) return "front, flag, or none";
        if (StyleOf(c) == _style) return "already so";
        if (_style == "flag" && c.CareerTrack == "military" && c.CareerRank < 3) return "the flag is for a Commander or better";
        return null;
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var c = w.Char(_characterId)!;
        c.Flags.Remove(FleetSystem.FrontFlag); c.Flags.Remove(FleetSystem.FlagFlag);
        if (_style == "front") c.Flags.Add(FleetSystem.FrontFlag); else if (_style == "flag") c.Flags.Add(FleetSystem.FlagFlag);
        w.Log(_style == "front" ? $"{c.Name} will lead from the front." : _style == "flag" ? $"{c.Name} will command from the flag." : $"{c.Name} leaves the line to the captains.");
    }
}

/// <summary>Declare a campaign (docs/MILITARY.md): name an enemy's system as the house's war aim for a season. Fleets within reach of it fight
/// harder and count as supplied. Costs influence; one campaign at a time; the target's holder must be at war with you.</summary>
public sealed class DeclareCampaignCommand : ICommand
{
    private readonly string _systemId; private readonly FleetRules _rules;
    public DeclareCampaignCommand(string actorId, string systemId, FleetRules rules) { ActorId = actorId; _systemId = systemId; _rules = rules; }
    public string ActorId { get; }
    public string Describe() => $"campaign on {_systemId}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); if (actor?.Alive != true || w.Galaxy == null) return "no one to give the order";
        if (w.Galaxy.System(_systemId) == null) return "no such system";
        var holder = w.PlanetHolder(_systemId); if (holder == null) return "no one holds it";
        if (holder == actor.HouseId) return "your own world";
        if (!w.AtWar(actor.HouseId, holder)) return $"you are not at war with {w.House(holder)?.Name ?? holder}";
        if (FleetSystem.CampaignTarget(w, actor.HouseId) != null) return "a campaign is already under way";
        if (!w.Fleets.Values.Any(f => f.OwnerId == actor.HouseId && !f.Office)) return "no fleet to campaign with";
        return ctx.PoolsOf(ActorId).CanAfford(new Dictionary<string, int> { [Pool.Influence] = _rules.CampaignCost }) ? null : $"needs {_rules.CampaignCost} influence";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!;
        ctx.PoolsOf(ActorId).Spend(new Dictionary<string, int> { [Pool.Influence] = _rules.CampaignCost });
        foreach (var f in w.WorldFlags.Where(f => f.StartsWith(FleetSystem.CampaignPrefix(actor.HouseId))).ToList()) w.WorldFlags.Remove(f);
        w.WorldFlags.Add(FleetSystem.CampaignPrefix(actor.HouseId) + _systemId);
        w.WorldCounters[FleetSystem.CampaignUntilKey(actor.HouseId)] = w.Turn + _rules.CampaignTurns;
        w.WorldCounters["campaigns"] = w.Counter("campaigns") + 1;
        if (actor.HouseId == w.Protagonist?.HouseId) w.WorldFlags.Add("campaign_declared");
        w.Log($"{w.House(actor.HouseId)?.Name} declares a campaign: {w.Galaxy!.System(_systemId)?.Name ?? _systemId} is the war's object for {_rules.CampaignTurns} years.");
    }
}