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
        if (f.Moving || f.SystemId == _targetSystemId || f.LaidUp) return false;
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
        if (FleetSystem.LaidUpRefusal(f) is { } laid) return laid;
        if (w.Galaxy.Hops(f.SystemId, _targetSystemId, Barred(w, f.OwnerId)) <= 0)
            return w.Galaxy.Hops(f.SystemId, _targetSystemId) > 0 ? "no way through: an enemy's fleet holds a gate on the road, and a gate that is held must be taken first" : "no lane runs there from where the fleet lies";
        if (FleetSystem.Unfuelled(w, f.OwnerId)) return "the tanks are dry: helium-3 is bought on the Exchange, and no fleet of the house sails without it";
        if (f.Loyalty < _rules.LoyaltyRefuse) return "the crews will not sail — loyalty " + f.Loyalty;
        return ctx.PoolsOf(ActorId).Short(new Dictionary<string, int> { [Pool.Treasury] = _rules.MovePoolCost });
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
        f.Eta = w.Galaxy!.Hops(f.SystemId, _targetSystemId, Barred(w, f.OwnerId)); f.Voyage = f.Eta; f.Errand = "";
        w.Log($"{f.Name} sets course for {w.Galaxy.System(_targetSystemId)?.Name ?? _targetSystemId} ({f.Eta} jump{(f.Eta == 1 ? "" : "s")}).");
    }
}

public static class FleetNames
{
    private static string Nth(int n) => n % 100 is 11 or 12 or 13 ? n + "th" : (n % 10) switch { 1 => n + "st", 2 => n + "nd", 3 => n + "rd", _ => n + "th" };
    /// <summary>A name no other fleet of the house has: "House X Squadron", then "House X 2nd Squadron", and so on.</summary>
    public static string Squadron(World w, House house)
    {
        bool Taken(string name) => w.Fleets.Values.Any(f => f.OwnerId == house.Id && f.Name == name);
        string first = $"{house.Name} Squadron"; if (!Taken(first)) return first;
        for (int n = 2; ; n++) { string name = $"{house.Name} {Nth(n)} Squadron"; if (!Taken(name)) return name; }
    }
}

/// <summary>Give a fleet a name of the house's choosing (docs/PLAYTHROUGH.md: two squadrons with one name, and no way to tell them
/// apart). Free. A levy keeps the name of the house that sent it, and an office fleet the Reach's.</summary>
public sealed class RenameFleetCommand : ICommand
{
    private readonly string _fleetId, _name;
    public RenameFleetCommand(string actorId, string fleetId, string name) { ActorId = actorId; _fleetId = fleetId; _name = (name ?? "").Trim(); }
    public string ActorId { get; }
    public string Describe() => $"rename {_fleetId} to {_name}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId);
        if (actor?.Alive != true || !w.Fleets.TryGetValue(_fleetId, out var f) || f.OwnerId != actor.HouseId) return "not your fleet";
        if (f.LevyOf.Length > 0) return "a levy keeps the name of the house that sent it"; if (f.Office) return "it is the Reach's fleet, and keeps the Reach's name";
        if (_name.Length < 2 || _name.Length > 40) return "a name of two to forty letters";
        if (_name.Any(ch => char.IsControl(ch) || ch is '<' or '>' or '{' or '}')) return "letters, numbers and plain marks only";
        return w.Fleets.Values.Any(x => x.Id != f.Id && x.OwnerId == f.OwnerId && string.Equals(x.Name, _name, StringComparison.OrdinalIgnoreCase)) ? "another of your fleets has that name" : f.Name == _name ? "it has that name" : null;
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { var f = ctx.World.Fleets[_fleetId]; ctx.World.Log($"{f.Name} is to be called {_name}."); f.Name = _name; }
}

/// <summary>Call a fleet under way back (docs/PLAYTHROUGH.md 11: "a fleet's order cannot be taken back": one squadron left on a
/// six-year voyage that could not be recalled). An order given this year is simply struck out; a fleet that has sailed some years
/// takes as many to come home. It costs nothing but the time.</summary>
public sealed class RecallFleetCommand : ICommand
{
    private readonly string _fleetId;
    public RecallFleetCommand(string actorId, string fleetId) { ActorId = actorId; _fleetId = fleetId; }
    public string ActorId { get; }
    public string Describe() => $"recall {_fleetId}";
    /// <summary>The years it would take to be home again: what it has sailed.</summary>
    public static int YearsBack(Fleet f) => f.Voyage > 0 ? Math.Max(0, f.Voyage - f.Eta) : 0;
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId);
        if (actor?.Alive != true) return "no one to give the order";
        if (!w.Fleets.TryGetValue(_fleetId, out var f) || f.OwnerId != actor.HouseId) return "not your fleet";
        if (f.IsAway) return "it is beyond the Reach: recall the expedition";
        if (!f.Moving) return "it is not under way";
        if (f.DestinationId == f.SystemId) return "it is already coming home";
        return null;
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var f = w.Fleets[_fleetId]; int back = YearsBack(f); string home = w.Galaxy?.System(f.SystemId)?.Name ?? f.SystemId;
        if (back <= 0) { f.DestinationId = ""; f.Eta = 0; f.Voyage = 0; w.Log($"{f.Name}'s orders are struck out before it has left {home}."); }
        else { f.DestinationId = f.SystemId; f.Eta = back; f.Voyage = back; w.Log($"{f.Name} is called back to {home} ({back} year{(back == 1 ? "" : "s")} to come about and return)."); }
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
        if (cls.Requires != null && cls.Requires != h.Specialization) return $"needs a {cls.Requires.Replace('_', ' ')}";
        var house = w.House(actor.HouseId);
        if (house == null) return "no house";
        if (cls.MinTitleRank > 0 && (_titles?.RankOf(house.Title) ?? 0) < cls.MinTitleRank) return $"needs the rank of {Progression.Title.StyleOf(Progression.Title.OfRank(cls.MinTitleRank), ctx.World.Era)}";
        return house.Treasury.CanAfford(cls.Cost) ? null : house.Treasury.Short(cls.Cost);
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
            .Where(f => f.OwnerId == house.Id && !f.Office && f.LevyOf.Length == 0 && !f.Moving && f.SystemId == h.SystemId)   // new hulls are not laid into a levy or a hired company
            .OrderBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
        if (fleet == null)
        {
            int n = w.Counter("fleets_raised") + 1;
            w.WorldCounters["fleets_raised"] = n;
            fleet = new Fleet { Id = $"fleet_{house.Id}_{n}", Name = FleetNames.Squadron(w, house), OwnerId = house.Id, Strength = 0, SystemId = h.SystemId, Readiness = 60 };
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
        if (f.AdmiralId == _characterId || f.LevyOf.Length > 0) return false;   // (a levy keeps the officer its own house gave it)
        if (Praetoria.Core.Systems.CaesarSystem.Barred(w, c)) return false;   // the Senate had them lay a command down (docs/CAESAR.md)
        return ctx.PoolsOf(ActorId).CanAfford(CostMap);
    }
    /// <summary>Why the appointment would be refused, said singly.</summary>
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var c = w.Char(_characterId);
        if (actor?.Alive != true || c?.Alive != true) return "no one to appoint";
        if (!w.Fleets.TryGetValue(_fleetId, out var f) || f.OwnerId != actor.HouseId || c.HouseId != actor.HouseId) return "not of your house";
        if (f.AdmiralId == _characterId) return "already commands it"; if (f.LevyOf.Length > 0) return "a levy keeps the officer its own house gave it";
        if (Praetoria.Core.Systems.CaesarSystem.Barred(w, c)) return "the Senate had them lay a command down";
        return ctx.PoolsOf(ActorId).Short(CostMap);
    }

    public void Execute(CommandContext ctx)
    {
        var w = ctx.World;
        ctx.PoolsOf(ActorId).Spend(CostMap);
        var f = w.Fleets[_fleetId]; var c = w.Char(_characterId)!;
        // one command to a person: whoever had another shifts their flag, and the fleet they leave has no admiral until one is named
        var left = w.Fleets.Values.FirstOrDefault(x => x.Id != _fleetId && x.AdmiralId == _characterId);
        if (left != null) left.AdmiralId = "";
        f.AdmiralId = c.Id;
        FleetSystem.RefreshAdmiralFlags(w);
        w.Log(left != null ? $"{c.Name} shifts {(c.Sex == "female" ? "her" : "his")} flag from {left.Name} to {f.Name}; {left.Name} is without an admiral." : $"{c.Name} takes command of {f.Name}.");
    }
}

/// <summary>Ask another house to take its fleet out of your space (docs/PLAYTHROUGH.md, War and peace: "a way to ask them to go").
/// One influence. Its head obliges when they think no worse of you than <see cref="FleetSystem.ObligesAt"/>, or when you have at
/// least as many hulls there as they: otherwise the answer is no, and the refusal says which. The fleet goes home, and its house
/// does not send one to stand over you again for <see cref="FleetSystem.AskedOffYears"/> years.</summary>
public sealed class AskFleetToLeaveCommand : ICommand
{
    private static readonly Dictionary<string, int> CostMap = new() { [Pool.Influence] = 1 };
    private readonly string _fleetId;
    public AskFleetToLeaveCommand(string actorId, string fleetId) { ActorId = actorId; _fleetId = fleetId; }
    public string ActorId { get; }
    public string Describe() => $"ask {_fleetId} to leave";
    private static string? Home(World w, Fleet f) => w.Holdings.Values.Where(h => h.OwnerId == f.OwnerId && h.SystemId.Length > 0 && h.SystemId != f.SystemId)
        .Select(h => (s: h.SystemId, hops: w.Galaxy!.Hops(f.SystemId, h.SystemId))).Where(x => x.hops > 0).OrderBy(x => x.hops).ThenBy(x => x.s, StringComparer.Ordinal).Select(x => x.s).FirstOrDefault();
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId);
        if (actor?.Alive != true || w.Galaxy == null) return "no one to ask";
        if (!w.Fleets.TryGetValue(_fleetId, out var f) || f.OwnerId == actor.HouseId) return "not another house's fleet";
        if (f.Moving || f.IsAway) return "it is under way";
        if (FleetSystem.TerritoryOwner(w, f.SystemId) != actor.HouseId) return "this is not your space: you have no standing to ask";
        if (f.OwnerId == PirateSystem.HouseId) return "raiders are not asked: they are driven off";
        if (w.AtWar(actor.HouseId, f.OwnerId)) return "you are at war with it: it is driven off, or peace is made";
        string them = w.House(f.OwnerId)?.Name ?? f.OwnerId;
        if (f.Office) return $"it is the Reach's squadron, not {them}'s own: the Senate's commission sends it where it goes";
        if (Home(w, f) == null) return $"{them} has no port of its own for it to go to";
        var head = w.House(f.OwnerId) is { } h ? Economy.HeadOf(w, h) : null;
        int regard = head != null ? w.GetRelationship(head.Id, ActorId)?.Disposition ?? 0 : 0, mine = (int)w.Fleets.Values.Where(x => x.OwnerId == actor.HouseId && !x.Moving && x.SystemId == f.SystemId).Sum(x => (double)x.Strength), theirs = w.Fleets.Values.Where(x => x.OwnerId == f.OwnerId && !x.Moving && x.SystemId == f.SystemId).Sum(x => x.Strength);
        if (regard < FleetSystem.ObligesAt && mine < theirs)
            return $"{them} would refuse: {head?.Name ?? "its head"} thinks too ill of you to oblige (regard {regard}; {FleetSystem.ObligesAt} or better would do), and you have {mine} hull(s) here to its {theirs} (as many as it has would do)";
        return ctx.PoolsOf(ActorId).Short(CostMap);
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!; var f = w.Fleets[_fleetId]; ctx.PoolsOf(ActorId).Spend(CostMap);
        string home = Home(w, f)!; string them = w.House(f.OwnerId)?.Name ?? f.OwnerId, where = w.Galaxy!.System(f.SystemId)?.Name ?? f.SystemId, to = w.Galaxy.System(home)?.Name ?? home;
        foreach (var x in w.Fleets.Values.Where(x => x.OwnerId == f.OwnerId && !x.Moving && !x.Office && x.SystemId == f.SystemId).ToList())
        { x.DestinationId = home; x.Eta = w.Galaxy.Hops(x.SystemId, home); x.Voyage = x.Eta; x.Errand = "home, asked out of " + (w.House(actor.HouseId)?.Name ?? actor.HouseId) + "'s space"; }
        w.WorldCounters[FleetSystem.AskedOffKey(f.OwnerId, actor.HouseId)] = w.Turn + FleetSystem.AskedOffYears;
        w.Log($"{actor.Name} asks {them} to take its fleet out of {where}, and it goes: it sails for {to}.");
        w.Tell("good", $"{them} takes its fleet out of {where} as you asked. It sails for {to}, and {them} will not send one to stand over you for {FleetSystem.AskedOffYears} years.", "galaxy:" + f.SystemId, f.OwnerId);
    }
}

/// <summary>Hire a fleet out to a house that is at war, as a company of mercenaries (<see cref="MercenarySystem"/>). It costs
/// nothing to do; what it costs is the house's name.</summary>
public sealed class HireOutFleetCommand : ICommand
{
    private readonly string _fleetId, _employerId;
    public HireOutFleetCommand(string actorId, string fleetId, string employerId) { ActorId = actorId; _fleetId = fleetId; _employerId = employerId; }
    public string ActorId { get; }
    public string Describe() => $"hire {_fleetId} out to {_employerId}";
    public string? Refusal(CommandContext ctx) => ctx.World.Fleets.TryGetValue(_fleetId, out var lf) && FleetSystem.LaidUpRefusal(lf) is { } laid ? laid : MercenarySystem.Refusal(ctx.World, ctx.World.Char(ActorId), _fleetId, _employerId);
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { var w = ctx.World; MercenarySystem.Hire(w, w.Char(ActorId)!, w.Fleets[_fleetId], w.House(_employerId)!); }
}

/// <summary>Call a company back from hire before its term: the contract is broken, and the employer remembers it.</summary>
public sealed class RecallHiredFleetCommand : ICommand
{
    private readonly string _fleetId;
    public RecallHiredFleetCommand(string actorId, string fleetId) { ActorId = actorId; _fleetId = fleetId; }
    public string ActorId { get; }
    public string Describe() => $"call {_fleetId} back from hire";
    public string? Refusal(CommandContext ctx) => MercenarySystem.RecallRefusal(ctx.World, ctx.World.Char(ActorId), _fleetId);
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { var w = ctx.World; MercenarySystem.Recall(w, w.Char(ActorId)!, w.Fleets[_fleetId]); }
}

/// <summary>Keep a fleet at drill, or end it (the owner, 2026-11-21: a smaller force that is better trained). Each year it lies at a
/// world of the house's own it takes manpower by its hulls and gains a step of veterancy, as far as <see cref="FleetRules.DrillCap"/>.
/// Setting it costs nothing; a year it cannot drill, it does not, and the house is told why.</summary>
public sealed class DrillFleetCommand : ICommand
{
    private readonly string _fleetId; private readonly bool _on; private readonly FleetSystem _fleets;
    public DrillFleetCommand(string actorId, string fleetId, bool on, FleetSystem fleets) { ActorId = actorId; _fleetId = fleetId; _on = on; _fleets = fleets; }
    public string ActorId { get; }
    public string Describe() => $"{(_on ? "drill" : "end the drill of")} {_fleetId}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId);
        if (actor?.Alive != true) return "no one to give the order";
        if (!w.Fleets.TryGetValue(_fleetId, out var f) || f.OwnerId != actor.HouseId) return "not your fleet";
        if (f.Office) return "it is the Reach's squadron: the Reach sees to its training"; if (f.LevyOf.Length > 0) return "a levy is drilled by its own house";
        if (_on && FleetSystem.LaidUpRefusal(f) is { } laid) return laid;
        if (f.Drilling == _on) return _on ? "it is at drill" : "it is not at drill";
        return _on && f.Veterancy >= _fleets.Rules.DrillCap ? $"it is as well drilled as drill makes a fleet (veterancy {f.Veterancy}): the rest is learnt in battle" : null;
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { var w = ctx.World; var f = w.Fleets[_fleetId]; f.Drilling = _on; w.Log(_on ? $"{f.Name} is put to drill." : $"{f.Name}'s drill is ended."); }
}

/// <summary>Hulls taken out of a fleet, the lightest first (the cheapest to build), as a guard left behind or a squadron paid off would be.</summary>
public static class FleetHulls
{
    public static List<(ShipClassDef cls, int n)> Lightest(Fleet f, int hulls, ShipClassCatalog classes, FleetRules rules)
    {
        var have = f.Ships.Count > 0 ? f.Ships.Where(kv => kv.Value > 0).Select(kv => (cls: classes.Resolve(kv.Key), n: kv.Value)).ToList() : new() { (classes.Resolve(rules.DefaultClass), f.Strength) };
        var took = new List<(ShipClassDef, int)>();
        foreach (var (cls, n) in have.OrderBy(x => x.cls.Cost.Materials + x.cls.Cost.Credits).ThenBy(x => x.cls.Attack + x.cls.Defense).ThenBy(x => x.cls.Id, StringComparer.Ordinal))
        { if (hulls <= 0) break; int t = Math.Min(n, hulls); took.Add((cls, t)); hulls -= t; }
        return took;
    }
    public static string Say(List<(ShipClassDef cls, int n)> hulls) => string.Join(", ", hulls.Select(x => $"{x.n} {x.cls.Name.ToLowerInvariant()}{(x.n == 1 ? "" : "s")}"));
    /// <summary>Strike them from the fleet's composition.</summary>
    public static void Take(Fleet f, List<(ShipClassDef cls, int n)> hulls, FleetRules rules)
    {
        if (f.Ships.Count == 0 && f.Strength > 0) f.Ships[rules.DefaultClass] = f.Strength;
        foreach (var (cls, n) in hulls) { string key = f.Ships.ContainsKey(cls.Id) ? cls.Id : rules.DefaultClass; int left = f.Ships.GetValueOrDefault(key) - n; if (left > 0) f.Ships[key] = left; else f.Ships.Remove(key); }
        if (f.Ships.Count == 0) f.Strength = 0; else f.Recount();
    }
}

/// <summary>Detach hulls from a fleet where it lies, as a squadron of their own: a guard left over a world while the rest sails on
/// (docs/PLAYTHROUGH.md, War and peace: "detach hulls to leave a guard"). The lightest hulls go. The admiral stays with the fleet;
/// the detachment has none until one is named. It costs nothing.</summary>
public sealed class DetachHullsCommand : ICommand
{
    private readonly string _fleetId; private readonly int _hulls; private readonly FleetRules _rules; private readonly ShipClassCatalog _classes;
    public DetachHullsCommand(string actorId, string fleetId, int hulls, FleetRules rules, ShipClassCatalog? classes = null) { ActorId = actorId; _fleetId = fleetId; _hulls = hulls; _rules = rules; _classes = classes ?? ShipClassCatalog.Empty; }
    public string ActorId { get; }
    public string Describe() => $"detach {_hulls} hull(s) from {_fleetId}";
    public List<(ShipClassDef cls, int n)> Which(World w) => w.Fleets.TryGetValue(_fleetId, out var f) ? FleetHulls.Lightest(f, _hulls, _classes, _rules) : new();
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId);
        if (actor?.Alive != true) return "no one to give the order";
        if (!w.Fleets.TryGetValue(_fleetId, out var f) || f.OwnerId != actor.HouseId) return "not your fleet";
        if (f.LevyOf.Length > 0) return "a levy sails under its own house's officers and goes home whole";
        if (f.IsAway) return "it is beyond the Reach"; if (f.Moving) return "it is under way: hulls are detached where a fleet lies";
        if (FleetSystem.LaidUpRefusal(f) is { } laid) return laid;
        if (f.Strength < 2) return "it is one hull: there is nothing to detach";
        return _hulls >= 1 && _hulls < f.Strength ? null : $"a detachment is at least one hull and leaves at least one: {f.Name} has {f.Strength}";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var f = w.Fleets[_fleetId]; var house = w.House(f.OwnerId)!; var took = Which(w);
        FleetHulls.Take(f, took, _rules);
        int n = w.Counter("fleets_raised") + 1; w.WorldCounters["fleets_raised"] = n;
        var d = new Fleet { Id = $"fleet_{house.Id}_{n}", Name = FleetNames.Squadron(w, house), OwnerId = f.OwnerId, SystemId = f.SystemId, HomeSystemId = f.HomeSystemId, Office = f.Office,
            Readiness = f.Readiness, Veterancy = f.Veterancy, Supply = f.Supply, Loyalty = f.Loyalty, Stance = f.Stance, Loadout = f.Loadout, ArrivedTurn = f.ArrivedTurn, Strength = 0 };
        foreach (var (cls, k) in took) d.Ships[cls.Id] = d.Ships.GetValueOrDefault(cls.Id) + k;
        d.Recount(); w.Fleets[d.Id] = d;
        w.Log($"{FleetHulls.Say(took)} of {f.Name} {(took.Sum(x => x.n) == 1 ? "is" : "are")} detached at {w.Galaxy?.System(f.SystemId)?.Name ?? f.SystemId} as {d.Name}; {f.Name} keeps {f.Strength} hull(s).");
    }
}

/// <summary>Pay hulls off: break them up at a world or station of the house's own, to cut what the fleet costs each year
/// (docs/PLAYTHROUGH.md, War and peace: "lay up or pay off hulls to cut the cost"). The lightest go first. A part of what they cost
/// to build comes back as materials (<see cref="ScrapPct"/>). An office fleet is the Reach's and is not broken up by its commander.</summary>
/// <summary>Lay a fleet up, or put it back in commission (docs/MILITARY.md, "Laid up"). Laid up at a world or station of the house, its
/// hulls are kept and its crews paid off: it costs <see cref="FleetRules.LaidUpUpkeepPct"/> of a hundred of its keep, burns no fuel,
/// has no readiness, takes no order that needs crews, and is not counted in the house's strength. Put back in commission it starts
/// with no readiness and gains it by the year, as any fleet at anchor does. One treasury action either way. Nothing is broken up.</summary>
public sealed class LayUpFleetCommand : ICommand
{
    private readonly string _fleetId; private readonly bool _on; private readonly FleetSystem _fleets;
    public LayUpFleetCommand(string actorId, string fleetId, bool on, FleetSystem fleets) { ActorId = actorId; _fleetId = fleetId; _on = on; _fleets = fleets; }
    public string ActorId { get; }
    public string Describe() => _on ? $"lay up {_fleetId}" : $"put {_fleetId} back in commission";
    private Dictionary<string, int> Cost => new() { [Pool.Treasury] = _fleets.Rules.MovePoolCost };
    /// <summary>What it costs a year as it is, and what it would cost laid up.</summary>
    public (Resources now, Resources laidUp) Keep(World w)
    {
        if (!w.Fleets.TryGetValue(_fleetId, out var f)) return (new Resources(), new Resources());
        bool was = f.LaidUp; f.LaidUp = false; var now = _fleets.Upkeep(f); f.LaidUp = true; var laid = _fleets.Upkeep(f); f.LaidUp = was;
        return (now, laid);
    }
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId);
        if (actor?.Alive != true) return "no one to give the order";
        if (!w.Fleets.TryGetValue(_fleetId, out var f) || f.OwnerId != actor.HouseId) return "not your fleet";
        if (f.Office) return "it is held by office and is the Reach's: it is not yours to lay up";
        if (f.LevyOf.Length > 0) return "a levy is its own house's: send it home instead";
        if (f.LaidUp == _on) return _on ? "it is laid up already" : "it is in commission";
        if (_on)
        {
            if (f.IsAway) return "it is beyond the Reach"; if (f.Moving) return "it is under way: a fleet is laid up in port";
            if (!w.Holdings.Values.Any(h => h.OwnerId == actor.HouseId && h.SystemId == f.SystemId)) return $"only at a world or station of your own: {w.Galaxy?.System(f.SystemId)?.Name ?? "this system"} is not";
        }
        return ctx.PoolsOf(ActorId).Short(Cost);
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var f = w.Fleets[_fleetId]; ctx.PoolsOf(ActorId).Spend(Cost);
        string where = w.Galaxy?.System(f.SystemId)?.Name ?? f.SystemId;
        if (_on) { f.LaidUp = true; f.Readiness = 0; f.Drilling = false; w.Log($"{f.Name} is laid up at {where}: its crews are paid off and its {f.Strength} hull(s) kept, at {_fleets.Rules.LaidUpUpkeepPct} in a hundred of their keep."); }
        else { f.LaidUp = false; f.Readiness = 0; w.Log($"{f.Name} is put back in commission at {where}. Its crews are new to it: it has no readiness, and gains {_fleets.Rules.ReadinessRecovery} a year at anchor."); }
    }
}

public sealed class PayOffHullsCommand : ICommand
{
    public const int ScrapPct = 25;
    private readonly string _fleetId; private readonly int _hulls; private readonly FleetRules _rules; private readonly ShipClassCatalog _classes;
    public PayOffHullsCommand(string actorId, string fleetId, int hulls, FleetRules rules, ShipClassCatalog? classes = null) { ActorId = actorId; _fleetId = fleetId; _hulls = hulls; _rules = rules; _classes = classes ?? ShipClassCatalog.Empty; }
    public string ActorId { get; }
    public string Describe() => $"pay off {_hulls} hull(s) of {_fleetId}";
    public List<(ShipClassDef cls, int n)> Which(World w) => w.Fleets.TryGetValue(_fleetId, out var f) ? FleetHulls.Lightest(f, _hulls, _classes, _rules) : new();
    /// <summary>The materials the breakers give back for them.</summary>
    public int Scrap(World w) => Which(w).Sum(x => x.cls.Cost.Materials * x.n) * ScrapPct / 100;
    /// <summary>What they cost each year, which is what is saved.</summary>
    public Resources Saved(World w) { var r = new Resources(); foreach (var (cls, n) in Which(w)) foreach (var k in Resource.All) r.Add(k, cls.Upkeep.Get(k) * n); return r; }
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId);
        if (actor?.Alive != true) return "no one to give the order";
        if (!w.Fleets.TryGetValue(_fleetId, out var f) || f.OwnerId != actor.HouseId) return "not your fleet";
        if (f.Office) return "it is held by office and is the Reach's: it is not yours to break up";
        if (f.LevyOf.Length > 0) return "a levy is its own house's: send it home instead";
        if (f.IsAway) return "it is beyond the Reach"; if (f.Moving) return "it is under way: hulls are paid off in port";
        if (!w.Holdings.Values.Any(h => h.OwnerId == actor.HouseId && h.SystemId == f.SystemId)) return $"only at a world or station of your own: {w.Galaxy?.System(f.SystemId)?.Name ?? "this system"} is not";
        return _hulls >= 1 && _hulls <= f.Strength ? null : $"{f.Name} has {f.Strength} hull(s)";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var f = w.Fleets[_fleetId]; var house = w.House(f.OwnerId)!; var took = Which(w); int scrap = Scrap(w);
        FleetHulls.Take(f, took, _rules); house.Treasury.Add(Resource.Materials, scrap);
        string where = w.Galaxy?.System(f.SystemId)?.Name ?? f.SystemId;
        if (f.Strength <= 0) { w.Fleets.Remove(f.Id); w.Intel.Remove(f.Id); FleetSystem.RefreshAdmiralFlags(w); w.Log($"{f.Name} is paid off at {where} ({FleetHulls.Say(took)}); the breakers give back {scrap} materials."); }
        else w.Log($"{FleetHulls.Say(took)} of {f.Name} {(took.Sum(x => x.n) == 1 ? "is" : "are")} paid off at {where}; the breakers give back {scrap} materials, and {f.Name} keeps {f.Strength} hull(s).");
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
        if (f.Moving || f.Readiness >= 100 || f.LaidUp) return false;
        if (!w.Holdings.Values.Any(h => h.OwnerId == actor.HouseId && h.SystemId == f.SystemId)) return false;
        return (w.House(actor.HouseId)?.Treasury.Materials ?? 0) >= MaterialsPerHull * f.Strength;
    }
    /// <summary>Why a refit would be refused: the one thing that is in the way (docs/PLAYTHROUGH.md, "Refusals that say the wrong thing").</summary>
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId);
        if (actor?.Alive != true || !w.Fleets.TryGetValue(_fleetId, out var f) || f.OwnerId != actor.HouseId) return "not your fleet";
        if (f.Moving) return "it is under way: a fleet is refitted in port"; if (FleetSystem.LaidUpRefusal(f) is { } laid) return laid; if (f.Readiness >= 100) return "already at full readiness";
        if (!w.Holdings.Values.Any(h => h.OwnerId == actor.HouseId && h.SystemId == f.SystemId)) return $"only at a world or station of your own: {w.Galaxy?.System(f.SystemId)?.Name ?? "this system"} is not";
        int have = w.House(actor.HouseId)?.Treasury.Materials ?? 0, need = MaterialsPerHull * f.Strength;
        return have >= need ? null : $"{need - have} materials short (it takes {need}, the house has {have})";
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
        if (a.LevyOf.Length > 0 || b.LevyOf.Length > 0) return false;   // a levy sails under its own house's officers, and goes home whole (docs/PROVINCES.md)
        if (a.LaidUp != b.LaidUp) return false;   // a fleet with crews and one without are not one fleet
        return a.OwnerId == actor.HouseId && b.OwnerId == actor.HouseId && !a.Moving && !b.Moving && a.SystemId == b.SystemId && a.Office == b.Office;
    }
    /// <summary>Why two fleets cannot be folded together, said singly.</summary>
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId);
        if (actor?.Alive != true || _fromId == _intoId || !w.Fleets.TryGetValue(_fromId, out var a) || !w.Fleets.TryGetValue(_intoId, out var b)) return "no such fleets";
        if (a.LevyOf.Length > 0 || b.LevyOf.Length > 0) return "a levy sails under its own house's officers and goes home whole: it is not folded into yours";
        if (a.OwnerId != actor.HouseId || b.OwnerId != actor.HouseId) return "not both yours";
        if (a.Office != b.Office) return "one is held by office and is the Reach's, not the house's: they are kept apart";
        if (a.Moving || b.Moving) return "one of them is under way";
        return a.SystemId == b.SystemId ? null : "not in the same system";
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