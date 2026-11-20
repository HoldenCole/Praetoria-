using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>Improve a province the actor's house holds (docs/PROVINCES.md): it yields as many times over as its level. How high it
/// may go is the development of the system it is in.</summary>
public sealed class ImproveProvinceCommand : ICommand
{
    private readonly string _holdingId; private readonly int _index; private readonly HoldingCatalog _catalog;
    public ImproveProvinceCommand(string actorId, string holdingId, int index, HoldingCatalog catalog) { ActorId = actorId; _holdingId = holdingId; _index = index; _catalog = catalog; }
    public string ActorId { get; }
    public string Describe() => $"improve province {_index} of {_holdingId}";
    public string? Refusal(CommandContext ctx) => Refusal(ctx, true);
    private string? Refusal(CommandContext ctx, bool explain)
    {
        var w = ctx.World; var actor = w.Char(ActorId); if (actor?.Alive != true) return "no one to give the order";
        var house = w.House(actor.HouseId); var planet = w.Holding(_holdingId); if (house == null || planet == null) return "no such province";
        return ProvinceSystem.ImproveRefusal(w, _catalog, house, planet, _index, explain);
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx, false) == null;
    public void Execute(CommandContext ctx) { var w = ctx.World; ProvinceSystem.Improve(w, _catalog, w.House(w.Char(ActorId)!.HouseId)!, w.Holding(_holdingId)!, _index); }
}

/// <summary>Ask a holder to step down (docs/PROVINCES.md; the owner, 2026-11-20: "Governorship is for life unless the leader over you
/// tries to [take it]"). The house that holds a world asks for one of its provinces (index 1 or 2), for itself or another house; the
/// leader over that house asks for the world itself (index 0), for a house that holds a province of it. It costs influence. The one
/// asked steps down, or fights.</summary>
public sealed class DemandSeatCommand : ICommand
{
    private readonly string _holdingId; private readonly int _index; private readonly string _forId; private readonly HoldingCatalog _catalog;
    public DemandSeatCommand(string actorId, string holdingId, int index, string forHouseId, HoldingCatalog catalog) { ActorId = actorId; _holdingId = holdingId; _index = index; _forId = forHouseId; _catalog = catalog; }
    public string ActorId { get; }
    public string Describe() => $"ask for province {_index} of {_holdingId} for {_forId}";
    public string? Refusal(CommandContext ctx) => Refusal(ctx, true);
    private string? Refusal(CommandContext ctx, bool explain)
    {
        var w = ctx.World; var actor = w.Char(ActorId); if (actor?.Alive != true) return "no one to give the order";
        var house = w.House(actor.HouseId); var planet = w.Holding(_holdingId); if (house == null || planet == null) return "no such province";
        return ProvinceSystem.DemandRefusal(w, _catalog, house, planet, _index, _forId, explain);
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx, false) == null;
    public void Execute(CommandContext ctx) { var w = ctx.World; ProvinceSystem.Demand(w, _catalog, w.House(w.Char(ActorId)!.HouseId)!, w.Holding(_holdingId)!, _index, _forId); }
}

/// <summary>Answer a demand that the actor's house step down (docs/PROVINCES.md): step down, and hold what is left under the house
/// that rises; or stand, which is war with the one who asked.</summary>
public sealed class AnswerSeatDemandCommand : ICommand
{
    private readonly string _id; private readonly bool _stepDown; private readonly HoldingCatalog _catalog;
    public AnswerSeatDemandCommand(string actorId, string demandId, bool stepDown, HoldingCatalog catalog) { ActorId = actorId; _id = demandId; _stepDown = stepDown; _catalog = catalog; }
    public string ActorId { get; }
    public string Describe() => $"{(_stepDown ? "step down at" : "defy")} {_id}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); if (actor?.Alive != true) return "no one to answer";
        return w.SeatDemands.Any(d => d.Id == _id && d.AgainstId == actor.HouseId) ? null : "nothing has been asked of you";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { var w = ctx.World; ProvinceSystem.Resolve(w, _catalog, w.SeatDemands.First(d => d.Id == _id), _stepDown); }
}

/// <summary>Build in a province the actor's house holds (docs/PROVINCES.md): an ordinary building of the world's kind, in one of the
/// province's own slots. What it yields is the holder's. A building that takes years is begun and paid for as it rises.</summary>
public sealed class BuildInProvinceCommand : ICommand
{
    private readonly string _holdingId; private readonly int _index; private readonly string _workId; private readonly HoldingCatalog _catalog;
    public BuildInProvinceCommand(string actorId, string holdingId, int index, string workId, HoldingCatalog catalog) { ActorId = actorId; _holdingId = holdingId; _index = index; _workId = workId; _catalog = catalog; }
    public string ActorId { get; }
    public string Describe() => $"build {_workId} in province {_index} of {_holdingId}";
    public string? Refusal(CommandContext ctx) => Refusal(ctx, true);
    private string? Refusal(CommandContext ctx, bool explain)
    {
        var w = ctx.World; var actor = w.Char(ActorId); if (actor?.Alive != true) return "no one to give the order";
        var house = w.House(actor.HouseId); var planet = w.Holding(_holdingId); var def = _catalog.Work(_workId); if (house == null || planet == null || def == null) return "no such building";
        return ProvinceSystem.BuildRefusal(w, _catalog, house, planet, _index, def, explain);
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx, false) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var house = w.House(w.Char(ActorId)!.HouseId)!; var planet = w.Holding(_holdingId)!; var def = _catalog.Work(_workId)!;
        if (def.BuildTurns <= 1) { house.Treasury.Spend(def.Cost); ProvinceSystem.Raise(w, planet, _index, def); return; }
        ProjectSystem.Start(w, house.Id, Project.ProvinceWork, def.Id, ProvinceSystem.Target(planet.Id, _index), def.Name + " in " + planet.Provinces[_index].Name, def.Cost, def.BuildTurns);
    }
}
