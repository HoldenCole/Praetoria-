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


/// <summary>A landless house petitions the court for a fief (GAP §4 "a landless house has no way back"): the capital's holder, if well
/// disposed and holding two worlds or more, grants its least peopled world. Three influence; the grantor gains legitimacy for the
/// generosity and the petitioner a barony, a governor's chair and a debt of gratitude.</summary>
public sealed class PetitionFiefCommand : ICommand
{
    private static readonly Dictionary<string, int> CostMap = new() { [Pool.Influence] = 3 };
    public PetitionFiefCommand(string actorId) { ActorId = actorId; }
    public string ActorId { get; }
    public string Describe() => "petition the court for a fief";

    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null;
        if (actor?.Alive != true || house == null) return "no house";
        if (w.HoldingsOf(house.Id).Any(h => h.IsPlanet)) return "you hold land already";
        var court = Systems.RegionSystem.CapitalHolder(w);
        if (court == null) return "no court stands to grant it";
        if (court.Id == house.Id) return "you are the court";
        if (w.AtWar(house.Id, court.Id)) return $"{court.Name} is at war with you";
        if (house.Legitimacy < 20) return "the court will not hear a house of so little standing";
        if (w.HoldingsOf(court.Id).Count(h => h.IsPlanet) < 2) return $"{court.Name} has no world to spare";
        var crown = Systems.Economy.HeadOf(w, court);
        if (crown != null && (w.GetRelationship(crown.Id, actor.Id)?.Disposition ?? 0) < 10) return $"{court.Name} does not think well enough of you";
        return ctx.PoolsOf(ActorId).CanAfford(CostMap) ? null : "not enough influence";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;

    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!; var house = w.House(actor.HouseId)!; var court = Systems.RegionSystem.CapitalHolder(w)!;
        ctx.PoolsOf(ActorId).Spend(CostMap);
        var fief = w.HoldingsOf(court.Id).Where(h => h.IsPlanet && !(w.Galaxy is { Capital.Length: > 0 } g && g.Capital == h.SystemId)).OrderBy(h => h.Population).ThenBy(h => h.Id, StringComparer.Ordinal).First();
        fief.FormerOwnerId = court.Id; fief.OwnerId = house.Id; fief.GovernorId = actor.Id;
        if (Progression.Title.RankOf(house.Title) < Progression.Title.RankOf(Progression.Title.Baron)) house.Title = Progression.Title.Baron;
        house.Legitimacy += 3; court.Legitimacy = Math.Min(100, court.Legitimacy + 5);
        var crown = Systems.Economy.HeadOf(w, court);
        if (crown != null) { w.Relationship(actor.Id, crown.Id).Disposition = Math.Clamp((w.GetRelationship(actor.Id, crown.Id)?.Disposition ?? 0) + 20, -100, 100); w.Relationship(crown.Id, actor.Id).Disposition = Math.Clamp((w.GetRelationship(crown.Id, actor.Id)?.Disposition ?? 0) + 10, -100, 100); }
        w.WorldCounters["fiefs_granted"] = w.Counter("fiefs_granted") + 1; if (house.Id == w.Protagonist?.HouseId) w.WorldFlags.Add("fief_granted");
        w.Log($"{court.Name} grants {fief.Name} to {house.Name} in fief. {actor.Name} kneels for it, and rises a {house.Title}.");
    }
}
