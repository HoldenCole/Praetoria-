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
    private readonly Progression.TitleCatalog? _titles;

    public BuildCommand(string actorId, string holdingId, string buildingId, HoldingCatalog catalog, Progression.TitleCatalog? titles = null)
    {
        ActorId = actorId;
        _holdingId = holdingId;
        _buildingId = buildingId;
        _catalog = catalog;
        _titles = titles;
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
        if (house == null) return false;
        if (building.MinTitleRank > 0 && (_titles?.RankOf(house.Title) ?? 0) < building.MinTitleRank) return false;   // megastructures want a seat
        return house.Treasury.CanAfford(building.Cost);
    }

    public void Execute(CommandContext ctx)
    {
        var actor = ctx.World.Char(ActorId)!;
        var house = ctx.World.House(actor.HouseId)!;
        var holding = ctx.World.Holding(_holdingId)!;
        var building = _catalog.Building(_buildingId)!;

        house.Treasury.Spend(building.Cost);
        holding.Buildings.Add(_buildingId);
        if (building.Megastructure)
        {
            ctx.World.WorldCounters["megastructures"] = ctx.World.Counter("megastructures") + 1;
            if (actor.Id == ctx.World.ProtagonistId) ctx.World.WorldFlags.Add("megastructure_raised");
            ctx.World.Log($"{house.Name} raises the {building.Name} over {holding.Name} — a thing the whole Reach can see.");
        }
    }
}

/// <summary>Convert resources at the Exchange (GDD §17 conversion chains). Needs a market-type building anywhere in
/// the house's domain; rates are data. Spends the house treasury.</summary>
public sealed class ExchangeCommand : ICommand
{
    private readonly ExchangeRate _rate;
    private readonly int _times;
    private readonly HoldingCatalog _catalog;
    public ExchangeCommand(string actorId, ExchangeRate rate, HoldingCatalog catalog, int times = 1) { ActorId = actorId; _rate = rate; _catalog = catalog; _times = Math.Max(1, times); }
    public string ActorId { get; }
    public string Describe() => $"exchange {_rate.GiveAmount * _times} {_rate.Give} → {_rate.GetAmount * _times} {_rate.Get}";

    public static bool HasExchange(World w, string houseId, HoldingCatalog catalog) =>
        catalog.ExchangeBuildings.Count == 0 || w.HoldingsOf(houseId).Any(h => h.Buildings.Any(b => catalog.ExchangeBuildings.Contains(b)));

    /// <summary>What a house without a market of its own receives: the caravaneers take half (GDD §17 — a market is
    /// worth building, but no house is ever locked out of converting what it has into what it lacks).</summary>
    public static int Received(World w, string houseId, ExchangeRate rate, HoldingCatalog catalog, int times) =>
        HasExchange(w, houseId, catalog) ? rate.GetAmount * times : Math.Max(1, rate.GetAmount * times / 2);

    public bool CanExecute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null;
        if (actor?.Alive != true || house == null || _catalog.Rates.Count == 0) return false;
        return house.Treasury.Get(_rate.Give) >= _rate.GiveAmount * _times;
    }

    public void Execute(CommandContext ctx)
    {
        var house = ctx.World.House(ctx.World.Char(ActorId)!.HouseId)!;
        house.Treasury.Add(_rate.Give, -_rate.GiveAmount * _times);
        house.Treasury.Add(_rate.Get, Received(ctx.World, house.Id, _rate, _catalog, _times));
        house.Treasury.ClampNonCredit();
    }
}

/// <summary>Set the house's tax posture (GDD §17). Free: a decree, not a scheme — but the realm feels it every turn.</summary>
public sealed class SetTaxPolicyCommand : ICommand
{
    private readonly string _policyId;
    private readonly HoldingCatalog _catalog;
    public SetTaxPolicyCommand(string actorId, string policyId, HoldingCatalog catalog) { ActorId = actorId; _policyId = policyId; _catalog = catalog; }
    public string ActorId { get; }
    public string Describe() => $"tax policy → {_policyId}";
    public bool CanExecute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null;
        return actor?.Alive == true && house != null && _catalog.Tax(_policyId) != null && house.TaxPolicy != _policyId;
    }
    public void Execute(CommandContext ctx)
    {
        var house = ctx.World.House(ctx.World.Char(ActorId)!.HouseId)!;
        house.TaxPolicy = _policyId;
        ctx.World.Log($"{house.Name} decrees the {_catalog.Tax(_policyId)!.Name.ToLowerInvariant()}.");
    }
}
