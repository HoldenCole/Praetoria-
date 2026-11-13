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

    /// <summary>Why it cannot be built, or null. A building with a build time is begun, not bought: the purse need only meet the first
    /// yearly instalment (docs/ECONOMY_MARKET.md stage B).</summary>
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId);
        if (actor?.Alive != true) return "no one to give the order";
        var holding = w.Holding(_holdingId);
        if (holding == null || holding.OwnerId != actor.HouseId) return "not your holding";
        var spec = _catalog.Spec(holding.Specialization); var building = _catalog.Building(_buildingId);
        if (spec == null || building == null) return "no such building";
        if (holding.Buildings.Count + Systems.ProjectSystem.SlotsUnderway(w, holding.Id) >= spec.Slots) return "no free slot";
        if (holding.Buildings.Contains(_buildingId)) return "already built";
        if (Systems.ProjectSystem.Underway(w, actor.HouseId, Project.Building, _buildingId, holding.Id)) return "already being built";
        if (!building.FitsSpecialization(holding.Specialization)) return "does not suit this holding";
        if (building.RequiresDeposit != null && (w.Galaxy?.System(holding.SystemId)?.Deposit(building.RequiresDeposit) ?? 0) == 0) return $"needs a {Resource.Label(building.RequiresDeposit)} deposit";
        var house = w.House(actor.HouseId); if (house == null) return "no house";
        if (building.MinTitleRank > 0 && (_titles?.RankOf(house.Title) ?? 0) < building.MinTitleRank) return "needs a greater rank";   // megastructures want a seat
        var first = Systems.ProjectSystem.Split(building.Cost, building.BuildTurns).First;
        return house.Treasury.CanAfford(first) ? null : $"needs {Presentation.CourtProjection.Bundle(first)}{(building.BuildTurns > 1 ? " to begin" : "")}";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;

    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!; var house = w.House(actor.HouseId)!; var holding = w.Holding(_holdingId)!; var building = _catalog.Building(_buildingId)!;
        if (building.BuildTurns <= 0) { house.Treasury.Spend(building.Cost); Raise(w, house, holding, building, actor.Id == w.ProtagonistId); return; }
        Systems.ProjectSystem.Start(w, house.Id, Project.Building, building.Id, holding.Id, $"the {building.Name} at {holding.Name}", building.Cost, building.BuildTurns);
        if (building.Megastructure) w.Log($"{house.Name} begins the {building.Name} over {holding.Name}: {building.BuildTurns} years in the building.");
    }

    /// <summary>The building stands: it is on the holding, and a megastructure is announced.</summary>
    public static void Raise(World w, House house, Holding holding, Data.BuildingDef building, bool byPlayer)
    {
        if (holding.Buildings.Contains(building.Id)) return;
        holding.Buildings.Add(building.Id);
        if (building.Megastructure)
        {
            w.WorldCounters["megastructures"] = w.Counter("megastructures") + 1;
            if (byPlayer) w.WorldFlags.Add("megastructure_raised");
            w.Log($"{house.Name} raises the {building.Name} over {holding.Name} — a thing the whole Reach can see.");
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


/// <summary>Seat one of your house in the governor's chair of a holding (GDD §13 "a governor of a planet is a bigger deal").
/// Costs 1 Influence; the governor must be a living adult of the owning house who is not already governing it.</summary>
public sealed class AppointGovernorCommand : ICommand
{
    private static readonly Dictionary<string, int> CostMap = new() { [Pool.Influence] = 1 };
    private readonly string _holdingId, _governorId;
    public AppointGovernorCommand(string actorId, string holdingId, string governorId) { ActorId = actorId; _holdingId = holdingId; _governorId = governorId; }
    public string ActorId { get; }
    public string Describe() => $"appoint {_governorId} governor of {_holdingId}";

    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var h = w.Holding(_holdingId); var g = w.Char(_governorId);
        if (actor?.Alive != true || h == null || g?.Alive != true) return "no such person or place";
        if (h.OwnerId != actor.HouseId) return "not your holding";
        if (g.HouseId != actor.HouseId) return "not of your house";
        if (g.Age < Systems.DynastySystem.AgeOfMajority) return "too young";
        if (g.Flags.Contains(Systems.DynastySystem.HiddenFlag)) return "fostered away";
        if (h.GovernorId == g.Id) return "already governs it";
        return ctx.PoolsOf(ActorId).CanAfford(CostMap) ? null : "no influence";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;

    public void Execute(CommandContext ctx)
    {
        var w = ctx.World;
        ctx.PoolsOf(ActorId).Spend(CostMap);
        var h = w.Holding(_holdingId)!; var g = w.Char(_governorId)!;
        var old = w.Char(h.GovernorId);
        if (old != null && old.Id != g.Id && old.Id != w.ProtagonistId) w.Relationship(old.Id, ActorId).Disposition = Math.Clamp(w.Relationship(old.Id, ActorId).Disposition - 10, -100, 100);   // the displaced resent it
        h.GovernorId = g.Id;
        if (g.Id != ActorId) w.Relationship(g.Id, ActorId).Disposition = Math.Clamp(w.Relationship(g.Id, ActorId).Disposition + 10, -100, 100);   // the chair is an honour
        w.WorldCounters["appointments"] = w.Counter("appointments") + 1;
        w.Log($"{g.Name} is named governor of {h.Name}{(old != null && old.Id != g.Id ? $", displacing {old.Name}" : "")}.");
    }
}


/// <summary>A landless house petitions for a fief (GAP §4 "a landless house has no way back"): the <b>grantor</b> — the capital's holder
/// if it has two worlds or more, else the greatest landed house at peace with the petitioner — if well disposed, grants its least
/// peopled world. Three influence; the grantor gains legitimacy for the generosity and the petitioner a barony, a governor's chair
/// and a debt of gratitude. In a Reach whose court holds only the capital, the fief comes from a patron, as it did in every feudal age.</summary>
public sealed class PetitionFiefCommand : ICommand
{
    private static readonly Dictionary<string, int> CostMap = new() { [Pool.Influence] = 3 };
    public PetitionFiefCommand(string actorId) { ActorId = actorId; }
    public string ActorId { get; }
    public string Describe() => "petition for a world to hold";

    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null;
        if (actor?.Alive != true || house == null) return "no house";
        if (w.HoldingsOf(house.Id).Any(h => h.IsPlanet)) return "you hold land already";
        if (house.Legitimacy < 20) return w.IsImperium ? "no court will hear a house of so little standing" : "the Senate will not entrust a world to a house of so little standing";
        var court = Grantor(w, house.Id);
        if (court == null) return "no house has a world to spare";
        var crown = Systems.Economy.HeadOf(w, court);
        if (crown != null && (w.GetRelationship(crown.Id, actor.Id)?.Disposition ?? 0) < 10) return $"{court.Name} does not think well enough of you";
        return ctx.PoolsOf(ActorId).CanAfford(CostMap) ? null : "not enough influence";
    }

    /// <summary>Who would grant the fief: the court if it holds a world beyond the capital, else the landed house with most worlds (two at
    /// least) that is at peace with the petitioner and is not a free hold. Null when no one can spare a world.</summary>
    public static House? Grantor(World w, string houseId)
    {
        int Spare(House h) => w.HoldingsOf(h.Id).Count(x => x.IsPlanet);
        var court = Systems.RegionSystem.CapitalHolder(w);
        if (court != null && court.Id != houseId && !w.AtWar(houseId, court.Id) && Spare(court) >= 2) return court;
        return w.Houses.Values.Where(h => h.Id != houseId && h.Id != Systems.PirateSystem.HouseId && h.Bloc != Systems.RegionSystem.FreeholdsBloc && !w.AtWar(houseId, h.Id) && Spare(h) >= 2)
            .OrderByDescending(Spare).ThenByDescending(h => h.Legitimacy).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;

    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!; var house = w.House(actor.HouseId)!; var court = Grantor(w, house.Id)!;
        ctx.PoolsOf(ActorId).Spend(CostMap);
        var fief = w.HoldingsOf(court.Id).Where(h => h.IsPlanet && !(w.Galaxy is { Capital.Length: > 0 } g && g.Capital == h.SystemId)).OrderBy(h => h.Population).ThenBy(h => h.Id, StringComparer.Ordinal).First();
        fief.FormerOwnerId = court.Id; fief.OwnerId = house.Id; fief.GovernorId = actor.Id;
        if (Progression.Title.RankOf(house.Title) < Progression.Title.RankOf(Progression.Title.Baron)) house.Title = Progression.Title.Baron;
        house.Legitimacy += 3; court.Legitimacy = Math.Min(100, court.Legitimacy + 5);
        var crown = Systems.Economy.HeadOf(w, court);
        if (crown != null) { w.Relationship(actor.Id, crown.Id).Disposition = Math.Clamp((w.GetRelationship(actor.Id, crown.Id)?.Disposition ?? 0) + 20, -100, 100); w.Relationship(crown.Id, actor.Id).Disposition = Math.Clamp((w.GetRelationship(crown.Id, actor.Id)?.Disposition ?? 0) + 10, -100, 100); }
        w.WorldCounters["fiefs_granted"] = w.Counter("fiefs_granted") + 1; if (house.Id == w.Protagonist?.HouseId) w.WorldFlags.Add("fief_granted");
        string style = Progression.Title.StyleOf(house.Title, w.Era); bool patron = court.Id != Systems.RegionSystem.CapitalHolder(w)?.Id;
        w.Log(w.IsImperium
            ? $"{court.Name} grants {fief.Name} to {house.Name} in fief{(patron ? ", as patron" : "")}. {actor.Name} kneels for it, and rises a {style}."
            : $"{court.Name} entrusts {fief.Name} to {house.Name} as a prefecture{(patron ? ", as patron" : "")}. {actor.Name} takes the oath for it, and rises a {style}.");
    }
}
