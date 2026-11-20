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
        ProjectSystem.Start(w, house.Id, Project.Work, def.Id, SystemWorks.Target(_systemId, _body), $"{(def.Name.StartsWith("The ", StringComparison.Ordinal) ? def.Name : "the " + def.Name)} {(b.IsSpace ? "in the " + sys.Name.Replace("The ", "") + " system" : "at " + b.Name)}", def.Cost, def.BuildTurns);
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
        if (years <= 1) { house.Treasury.Spend(cost); SystemWorks.Lay(w, _systemId, t); return; }
        ProjectSystem.Start(w, house.Id, Project.Infrastructure, t.Id, _systemId, $"{t.Name.ToLowerInvariant()} {SystemWorks.Roman(next)} at {sys.Name}", cost, years);
    }
}
