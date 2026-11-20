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
        ProjectSystem.Start(w, house.Id, Project.Work, def.Id, SystemWorks.Target(_systemId, _body), $"{(def.Name.StartsWith("The ", StringComparison.Ordinal) ? def.Name : "the " + def.Name)} at {b.Name}", def.Cost, def.BuildTurns);
        if (def.IsMegastructure || def.Category == WorkDef.Wonder) w.Log($"{house.Name} begins {(def.Name.StartsWith("The ", StringComparison.Ordinal) ? def.Name : "the " + def.Name)} in the {sys.Name.Replace("The ", "")} system: {def.BuildTurns} years in the building.");
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
