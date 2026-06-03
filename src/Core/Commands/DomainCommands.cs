using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Commands;

// Domain-economy intents (GDD §17). Like every other command they are cost-gated, deterministic,
// and routed through the executor — the player and (later) NPC stewards share this path. These
// spend a HOUSE TREASURY (accumulating resources), not the per-turn action pools: investment is
// wealth, not bandwidth. The galaxy-scale verbs (move fleet, colonise) grow in a later milestone.

/// <summary>
/// Slot a building into one of an actor's holdings (GDD §17 "boost energy / navy capacity"). Legal
/// only when the holding belongs to the actor's house, has a free slot, the building fits the
/// specialization, it isn't already built, and the house treasury covers the one-time cost.
/// </summary>
public sealed class BuildCommand : ICommand
{
    private readonly string _holdingId;
    private readonly string _buildingId;
    private readonly HoldingCatalog _catalog;

    public BuildCommand(string actorId, string holdingId, string buildingId, HoldingCatalog catalog)
    {
        ActorId = actorId;
        _holdingId = holdingId;
        _buildingId = buildingId;
        _catalog = catalog;
    }

    public string ActorId { get; }
    public string Describe() => $"build {_buildingId} at {_holdingId}";

    public bool CanExecute(CommandContext ctx)
    {
        var actor = ctx.World.Char(ActorId);
        if (actor?.Alive != true) return false;

        var holding = ctx.World.Holding(_holdingId);
        if (holding == null || holding.OwnerId != actor.HouseId) return false;

        var spec = _catalog.Spec(holding.Specialization);
        var building = _catalog.Building(_buildingId);
        if (spec == null || building == null) return false;

        if (holding.Buildings.Count >= spec.Slots) return false;       // no free slot
        if (holding.Buildings.Contains(_buildingId)) return false;     // no duplicates
        if (!building.FitsSpecialization(holding.Specialization)) return false;

        var house = ctx.World.House(actor.HouseId);
        return house != null && house.Treasury.CanAfford(building.Cost);
    }

    public void Execute(CommandContext ctx)
    {
        var actor = ctx.World.Char(ActorId)!;
        var house = ctx.World.House(actor.HouseId)!;
        var holding = ctx.World.Holding(_holdingId)!;
        var building = _catalog.Building(_buildingId)!;

        house.Treasury.Spend(building.Cost);
        holding.Buildings.Add(_buildingId);
    }
}
