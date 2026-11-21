using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>Build a work on a body of a system the actor's house holds (docs/SYSTEMS.md): a colony on a moon, a mine in the belt, a
/// fortress, a megastructure round the sun. A work that takes years is begun, not bought: the purse need only meet the first
/// yearly instalment, and the rest is paid as it rises.</summary>
public sealed class BuildWorkCommand : ICommand
{
    private readonly string _systemId; private readonly int _body; private readonly string _workId; private readonly HoldingCatalog _catalog; private readonly Progression.TitleCatalog? _titles;
    public BuildWorkCommand(string actorId, string systemId, int body, string workId, HoldingCatalog catalog, Progression.TitleCatalog? titles = null)
    { ActorId = actorId; _systemId = systemId; _body = body; _workId = workId; _catalog = catalog; _titles = titles; }
    public string ActorId { get; }
    public string Describe() => $"build {_workId} on body {_body} of {_systemId}";

    public string? Refusal(CommandContext ctx) => Refusal(ctx, true);
    private string? Refusal(CommandContext ctx, bool explain)
    {
        var w = ctx.World; var actor = w.Char(ActorId); if (actor?.Alive != true) return "no one to give the order";
        var house = w.House(actor.HouseId); if (house == null) return "no house";
        var def = _catalog.Work(_workId); if (def == null) return "no such work";
        return SystemWorks.Refusal(w, _catalog, _titles, house, _systemId, _body, def, explain);
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx, false) == null;

    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!; var house = w.House(actor.HouseId)!; var def = _catalog.Work(_workId)!; var sys = w.Galaxy!.System(_systemId)!; var b = sys.Bodies[_body];
        if (def.BuildTurns <= 1) { house.Treasury.Spend(def.Cost); SystemWorks.Raise(w, house, _systemId, _body, def); return; }
        var begun = ProjectSystem.Start(w, house.Id, Project.Work, def.Id, SystemWorks.Target(_systemId, _body), $"{(def.Name.StartsWith("The ", StringComparison.Ordinal) ? def.Name : "the " + def.Name)} {(b.IsSpace ? "in the " + sys.Name.Replace("The ", "") + " system" : "at " + (b.IsPrimary && w.PlanetOf(_systemId) is { } seat ? seat.Name : b.Name))}", def.Cost, def.BuildTurns);
        if (begun != null) begun.Great = def.IsMegastructure;
        if (def.IsMegastructure) w.Log($"{house.Name} begins {(def.Name.StartsWith("The ", StringComparison.Ordinal) ? def.Name : "the " + def.Name)} in the {sys.Name.Replace("The ", "")} system: {def.BuildTurns} years in the building.");
    }
}

/// <summary>Send a colony to a system nobody holds (docs/SYSTEMS.md): it must lie a lane from a world of the house, or have a fleet of
/// the house standing in it. The colony takes years to land; when it does, the system's world is the house's.</summary>
public sealed class FoundColonyCommand : ICommand
{
    private readonly string _systemId; private readonly HoldingCatalog _catalog;
    public FoundColonyCommand(string actorId, string systemId, HoldingCatalog catalog) { ActorId = actorId; _systemId = systemId; _catalog = catalog; }
    public string ActorId { get; }
    public string Describe() => $"found a colony at {_systemId}";

    public string? Refusal(CommandContext ctx) => Refusal(ctx, true);
    private string? Refusal(CommandContext ctx, bool explain)
    {
        var w = ctx.World; var actor = w.Char(ActorId); if (actor?.Alive != true) return "no one to give the order";
        var house = w.House(actor.HouseId); if (house == null) return "no house";
        return SystemWorks.ColonyRefusal(w, _catalog, house, _systemId, explain);
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx, false) == null;

    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!; var house = w.House(actor.HouseId)!; var sys = w.Galaxy!.System(_systemId)!;
        if (ProjectSystem.Start(w, house.Id, Project.Colony, "colony", _systemId, $"the colony at {sys.Name}", _catalog.Colony.Cost, Math.Max(1, _catalog.Colony.BuildTurns)) != null)
            w.Log($"{house.Name} sends a colony to {sys.Name}: ships, settlers and stores, {Math.Max(1, _catalog.Colony.BuildTurns)} years on the way.");
    }
}

/// <summary>Lay the next level of a track of a system's infrastructure (docs/BUILDINGS.md): civil works, habitats, orbital traffic,
/// outworks, the defence grid. It is the system's own and not any body's; each level costs more than the last and the higher ones
/// take years, paid as they are laid.</summary>
public sealed class InvestInfrastructureCommand : ICommand
{
    private readonly string _systemId; private readonly string _trackId; private readonly HoldingCatalog _catalog;
    public InvestInfrastructureCommand(string actorId, string systemId, string trackId, HoldingCatalog catalog) { ActorId = actorId; _systemId = systemId; _trackId = trackId; _catalog = catalog; }
    public string ActorId { get; }
    public string Describe() => $"lay {_trackId} at {_systemId}";

    public string? Refusal(CommandContext ctx) => Refusal(ctx, true);
    private string? Refusal(CommandContext ctx, bool explain)
    {
        var w = ctx.World; var actor = w.Char(ActorId); if (actor?.Alive != true) return "no one to give the order";
        var house = w.House(actor.HouseId); if (house == null) return "no house";
        var t = _catalog.Track(_trackId); if (t == null) return "no such thing";
        return SystemWorks.InfrastructureRefusal(w, _catalog, house, _systemId, t, explain);
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx, false) == null;

    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!; var house = w.House(actor.HouseId)!; var t = _catalog.Track(_trackId)!; var sys = w.Galaxy!.System(_systemId)!;
        int next = SystemWorks.Level(w, _systemId, t.Id) + 1; var cost = t.CostOf(next); int years = t.YearsOf(next);
        if (years <= 1) { house.Treasury.Spend(cost); SystemWorks.Lay(w, _catalog, _systemId, t); return; }
        ProjectSystem.Start(w, house.Id, Project.Infrastructure, t.Id, _systemId, $"{t.Name.ToLowerInvariant()} {SystemWorks.Roman(next)} at {sys.Name}", cost, years);
    }
}

/// <summary>Pay to clear ground for another building slot on a place of a system the actor's house holds (docs/BUILDINGS.md; the
/// owner, 2026-11-20: "if you have more money, you can potentially clear obstacles or just pay to unlock more slots"). Each slot
/// cleared on a place costs more than the last, and there is only so much ground.</summary>
public sealed class ClearSlotCommand : ICommand
{
    private readonly string _systemId; private readonly int _body; private readonly HoldingCatalog _catalog;
    public ClearSlotCommand(string actorId, string systemId, int body, HoldingCatalog catalog) { ActorId = actorId; _systemId = systemId; _body = body; _catalog = catalog; }
    public string ActorId { get; }
    public string Describe() => $"clear a slot on body {_body} of {_systemId}";

    public string? Refusal(CommandContext ctx) => Refusal(ctx, true);
    private string? Refusal(CommandContext ctx, bool explain)
    {
        var w = ctx.World; var actor = w.Char(ActorId); if (actor?.Alive != true) return "no one to give the order";
        var house = w.House(actor.HouseId); if (house == null) return "no house";
        return SystemWorks.ClearRefusal(w, _catalog, house, _systemId, _body, explain);
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx, false) == null;

    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!; var house = w.House(actor.HouseId)!; string key = SystemWorks.Target(_systemId, _body);
        int cleared = SystemWorks.Cleared(w, _systemId, _body);
        house.Treasury.Spend(_catalog.Rules.ClearCostOf(cleared)); w.ClearedSlots[key] = cleared + 1;
        w.WorldCounters["slots_cleared"] = w.Counter("slots_cleared") + 1;
    }
}

/// <summary>Take a building a level higher (docs/BUILDINGS.md): it yields, and costs to keep, as many times over as its level. How
/// high it may go is the system's development. <c>target</c> is a holding's id for one of its own buildings, or "system#place" for a
/// building raised on a place. The level takes as long as the building did, and is paid as it is built.</summary>
public sealed class UpgradeBuildingCommand : ICommand
{
    private readonly string _target; private readonly string _defId; private readonly HoldingCatalog _catalog;
    public UpgradeBuildingCommand(string actorId, string target, string defId, HoldingCatalog catalog) { ActorId = actorId; _target = target; _defId = defId; _catalog = catalog; }
    public string ActorId { get; }
    public string Describe() => $"upgrade {_defId} at {_target}";

    public string? Refusal(CommandContext ctx) => Refusal(ctx, true);
    private string? Refusal(CommandContext ctx, bool explain)
    {
        var w = ctx.World; var actor = w.Char(ActorId); if (actor?.Alive != true) return "no one to give the order";
        var house = w.House(actor.HouseId); if (house == null) return "no house";
        return SystemWorks.UpgradeRefusal(w, _catalog, house, _target, _defId, explain);
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx, false) == null;

    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!; var house = w.House(actor.HouseId)!;
        string name; Resources cost; int years, level;
        if (_target.Contains('@')) { var (hid, idx) = ProvinceSystem.Parse(_target); var planet = w.Holding(hid)!; var def = _catalog.Work(_defId)!; level = planet.Provinces[idx].LevelOf(_defId); cost = def.Cost; years = def.BuildTurns; name = def.Name + " " + SystemWorks.Roman(level + 1) + " in " + planet.Provinces[idx].Name; }
        else if (_target.Contains('#')) { var (sysId, body) = SystemWorks.Parse(_target); var def = _catalog.Work(_defId)!; var x = SystemWorks.On(w, sysId, body).First(o => o.DefId == _defId); level = Math.Max(1, x.Level); cost = def.Cost; years = def.BuildTurns; var sys = w.Galaxy!.System(sysId)!; name = def.Name + " " + SystemWorks.Roman(level + 1) + (sys.Bodies[body].IsSpace ? " in the " + sys.Name.Replace("The ", "") + " system" : " at " + sys.Bodies[body].Name); }
        else { var h = w.Holding(_target)!; var def = _catalog.Building(_defId)!; level = h.LevelOf(_defId); cost = def.Cost; years = def.BuildTurns; name = def.Name + " " + SystemWorks.Roman(level + 1) + " at " + h.Name; }
        ProjectSystem.Start(w, house.Id, Project.Upgrade, _defId, _target, name, SystemWorks.UpgradeCost(cost, level), Math.Max(1, years));
    }
}

/// <summary>Pull down a building that stands: on the home world (target the holding's id), on a body ("system#body") or in a
/// province ("holding@index"). The slot is free; a quarter of its materials come back (<see cref="SystemWorks.PullDown"/>).</summary>
public sealed class PullDownCommand : ICommand
{
    private readonly string _target; private readonly string _defId; private readonly HoldingCatalog _catalog;
    public PullDownCommand(string actorId, string target, string defId, HoldingCatalog catalog) { ActorId = actorId; _target = target; _defId = defId; _catalog = catalog; }
    public string ActorId { get; }
    public string Describe() => $"pull down {_defId} at {_target}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); if (actor?.Alive != true) return "no one to give the order";
        var house = w.House(actor.HouseId); if (house == null) return "no house";
        return SystemWorks.PullDownRefusal(w, _catalog, house, _target, _defId);
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public string Says(World w) => w.Char(ActorId) is { } a && w.House(a.HouseId) is { } h ? SystemWorks.PullDownSays(w, _catalog, h, _target, _defId) : "";
    public void Execute(CommandContext ctx) { var w = ctx.World; SystemWorks.PullDown(w, _catalog, w.House(w.Char(ActorId)!.HouseId)!, _target, _defId); }
}

/// <summary>Hasten a great work, or let it go at its own pace again (docs/BUILDINGS.md; the owner, 2026-11-20: "you can pay extra to
/// speed up the development. This would be a continuous cost over the development life cycle"). A hastened work does two years in
/// one, and every year it is hastened pays both instalments and half as much again; a year the purse cannot stand it, it goes at its
/// ordinary pace.</summary>
public sealed class HastenWorkCommand : ICommand
{
    private readonly string _id; private readonly bool _on; private readonly HoldingCatalog _catalog;
    public HastenWorkCommand(string actorId, string projectId, bool on, HoldingCatalog catalog) { ActorId = actorId; _id = projectId; _on = on; _catalog = catalog; }
    public string ActorId { get; }
    public string Describe() => $"{(_on ? "hasten" : "unhasten")} {_id}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var p = w.Projects.FirstOrDefault(x => x.Id == _id); var house = actor != null ? w.House(actor.HouseId) : null;
        if (actor?.Alive != true || house == null || p == null || p.OwnerId != house.Id) return "no such project";
        if (!SystemWorks.IsGreat(_catalog, p)) return "only a great work is hastened";
        if (p.Hastened == _on) return _on ? "it is being hastened" : "it goes at its own pace";
        if (_on && p.TurnsLeft <= 1) return "done next year as it is";
        if (_on && p.Gang) return "a gang is on it already: it is sped one way or the other, not both";
        return null;
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { var p = ctx.World.Projects.First(x => x.Id == _id); p.Hastened = _on; }
}

/// <summary>Put men of the house to a work as a gang, or call them off (the owner, 2026-11-21: "you can definitely spend manpower to
/// hasten buildings, but it would be more of a cost ... over [time] and your manpower just wouldn't accumulate as quickly"). A work
/// with a gang on it does two years in one. Each year the gang is on it the house gives <see cref="BuildRules.GangManpower"/>
/// manpower as well as the two instalments; a year it cannot, the work goes at its own pace. Any work of the house's own takes one,
/// great or small; a company's does not, and a great work hastened with coin is not ganged as well.</summary>
public sealed class WorkGangCommand : ICommand
{
    private readonly string _id; private readonly bool _on; private readonly HoldingCatalog _catalog;
    public WorkGangCommand(string actorId, string projectId, bool on, HoldingCatalog catalog) { ActorId = actorId; _id = projectId; _on = on; _catalog = catalog; }
    public string ActorId { get; }
    public string Describe() => $"{(_on ? "gang" : "ungang")} {_id}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var p = w.Projects.FirstOrDefault(x => x.Id == _id); var house = actor != null ? w.House(actor.HouseId) : null;
        if (actor?.Alive != true || house == null || p == null || p.OwnerId != house.Id) return "no such project";
        if (p.CompanyId.Length > 0) return "it is a company's work: the company's hands build it, not the house's men";
        if (p.Gang == _on) return _on ? "a gang is on it" : "no gang is on it";
        if (!_on) return null;
        if (p.TurnsLeft <= 1) return "done next year as it is";
        if (p.Hastened) return "it is hastened with coin already: it is sped one way or the other, not both";
        int need = _catalog.Rules.GangManpower; return house.Treasury.Manpower >= need ? null : $"a gang is {need} manpower a year, and the house has {house.Treasury.Manpower}";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var p = w.Projects.First(x => x.Id == _id); p.Gang = _on;
        w.Log(_on ? $"A gang of the house's men is put to {p.Name}: it goes two years in one while they are on it." : $"The gang is called off {p.Name}: it goes at its own pace.");
    }
}
