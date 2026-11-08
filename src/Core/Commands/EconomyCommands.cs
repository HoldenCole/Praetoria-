using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

// The economy's verbs beyond building (GDD §17): invest in a world, stake a belt claim, found / expand / sell a venture,
// borrow and repay. All spend the house treasury, all are refusal-gated, all deterministic; NPC stewards use the same.

/// <summary>Pour credits into a holding's economy: prosperity rises at once (GDD §17 investment).</summary>
public sealed class InvestCommand : ICommand
{
    private readonly string _holdingId; private readonly BusinessCatalog _ventures;
    public InvestCommand(string actorId, string holdingId, BusinessCatalog ventures) { ActorId = actorId; _holdingId = holdingId; _ventures = ventures; }
    public string ActorId { get; }
    public string Describe() => $"invest in {_holdingId}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var h = w.Holding(_holdingId); var house = actor != null ? w.House(actor.HouseId) : null;
        if (actor?.Alive != true || h == null || house == null) return "no such place";
        if (h.OwnerId != house.Id) return "not your holding";
        if (h.Prosperity >= 100) return "already thriving";
        return house.Treasury.Credits >= _ventures.Finance.InvestCredits ? null : $"needs {_ventures.Finance.InvestCredits} credits";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var house = w.House(w.Char(ActorId)!.HouseId)!; var h = w.Holding(_holdingId)!;
        house.Treasury.Credits -= _ventures.Finance.InvestCredits;
        h.Prosperity = Math.Min(100, h.Prosperity + _ventures.Finance.InvestProsperity);
        w.WorldCounters["investments"] = w.Counter("investments") + 1;
    }
}

/// <summary>Stake a mining claim in a system's asteroid belt (GDD §17 asteroids). The belt's richness is how many claims it
/// takes; a house may stake where it holds something, next door to where it does, or anywhere at all if it holds nothing —
/// the landless house's way back onto the ledger.</summary>
public sealed class StakeClaimCommand : ICommand
{
    public const string Spec = "belt_claim";
    private readonly string _systemId; private readonly HoldingCatalog _catalog; private readonly BusinessCatalog _ventures;
    public StakeClaimCommand(string actorId, string systemId, HoldingCatalog catalog, BusinessCatalog ventures) { ActorId = actorId; _systemId = systemId; _catalog = catalog; _ventures = ventures; }
    public string ActorId { get; }
    public string Describe() => $"stake a claim in {_systemId}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null; var sys = w.Galaxy?.System(_systemId);
        if (actor?.Alive != true || house == null || sys == null) return "no such system";
        if (_catalog.Spec(Spec) == null) return "no claims in this age";
        if (sys.Belt <= 0) return "no belt";
        int staked = w.Holdings.Values.Count(h => h.IsClaim && h.SystemId == _systemId);
        if (staked >= sys.Belt) return "the belt is claimed out";
        if (w.Holdings.Values.Any(h => h.IsClaim && h.SystemId == _systemId && h.OwnerId == house.Id)) return "already staked here";
        bool present = w.HoldingsOf(house.Id).Any(h => h.SystemId == _systemId || w.Galaxy!.Neighbors(h.SystemId).Contains(_systemId));
        if (!present && w.HoldingsOf(house.Id).Any()) return "too far from your holdings";
        var f = _ventures.Finance;
        return house.Treasury.Credits >= f.ClaimCredits && house.Treasury.Materials >= f.ClaimMaterials ? null : $"needs {f.ClaimCredits} credits, {f.ClaimMaterials} materials";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!; var house = w.House(actor.HouseId)!; var sys = w.Galaxy!.System(_systemId)!; var f = _ventures.Finance;
        house.Treasury.Credits -= f.ClaimCredits; house.Treasury.Materials -= f.ClaimMaterials;
        var id = $"claim_{house.Id}_{_systemId}";
        w.Holdings[id] = new Holding { Id = id, OwnerId = house.Id, Name = $"{sys.Name} Claim", Specialization = Spec, SystemId = _systemId, Kind = Holding.Claim, GovernorId = actor.Id, Culture = w.Galaxy.CultureOf(_systemId) };
        w.WorldCounters["claims"] = w.Counter("claims") + 1;
        w.Log($"{house.Name} stakes a mining claim in the {sys.Name} belt.");
    }
}

/// <summary>Found a venture of a kind in a system (GDD §17 businesses).</summary>
public sealed class FoundBusinessCommand : ICommand
{
    private readonly string _defId, _systemId; private readonly BusinessCatalog _ventures; private readonly HoldingCatalog _catalog; private readonly Progression.TitleCatalog? _titles;
    public FoundBusinessCommand(string actorId, string defId, string systemId, BusinessCatalog ventures, HoldingCatalog catalog, Progression.TitleCatalog? titles = null) { ActorId = actorId; _defId = defId; _systemId = systemId; _ventures = ventures; _catalog = catalog; _titles = titles; }
    public string ActorId { get; }
    public string Describe() => $"found {_defId} in {_systemId}";
    public static string IdFor(string houseId, string defId, string systemId) => $"biz_{houseId}_{defId}_{systemId}";

    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null; var def = _ventures.Def(_defId); var sys = w.Galaxy?.System(_systemId);
        if (actor?.Alive != true || house == null || def == null || sys == null) return "no such venture";
        if (w.Businesses.ContainsKey(IdFor(house.Id, _defId, _systemId))) return "you already have one here";
        var planet = w.Holdings.Values.Where(h => h.IsPlanet && h.SystemId == _systemId).OrderBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
        if (def.RequiresBelt && sys.Belt <= 0) return "needs a belt";
        if (def.RequiresDeposit != null && sys.Deposit(def.RequiresDeposit) == 0) return $"needs a {Resource.Label(def.RequiresDeposit)} deposit";
        if (def.RequiresPopulated && (planet == null || planet.Population <= 0)) return "needs a peopled world";
        if (def.RequiresOwnWorld && (planet == null || planet.OwnerId != house.Id)) return "needs a world of your own";
        if (def.MinLanes > 0 && w.Galaxy!.Neighbors(_systemId).Count() < def.MinLanes) return $"needs a hub of {def.MinLanes} lanes";
        if (def.PlanetClasses.Count > 0 && !def.PlanetClasses.Contains(sys.PlanetClass)) return $"needs a {string.Join(" or ", def.PlanetClasses)} world";
        if (def.Specializations.Count > 0 && (planet == null || !def.Specializations.Contains(planet.Specialization))) return $"needs {string.Join(" or ", def.Specializations.Select(s => _catalog.Spec(s)?.Name ?? s))}";
        if (def.MinTitleRank > 0 && (_titles?.RankOf(house.Title) ?? 0) < def.MinTitleRank) return "needs a greater title";
        if (planet != null && w.AtWar(house.Id, planet.OwnerId)) return "the world's holder is at war with you";
        return house.Treasury.CanAfford(def.Cost) ? null : $"needs {Presentation.CourtProjection.Bundle(def.Cost)}";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var house = w.House(w.Char(ActorId)!.HouseId)!; var def = _ventures.Def(_defId)!; var sys = w.Galaxy!.System(_systemId)!;
        house.Treasury.Spend(def.Cost); house.Treasury.ClampNonCredit();
        var id = IdFor(house.Id, _defId, _systemId);
        w.Businesses[id] = new Business { Id = id, Name = $"{house.Name.Replace("House ", "")} {def.Name} of {sys.Name}", OwnerId = house.Id, Kind = _defId, SystemId = _systemId, Level = 1, Founded = w.Turn };
        w.WorldCounters["ventures_founded"] = w.Counter("ventures_founded") + 1;
        w.Log($"{house.Name} founds a {def.Name.ToLowerInvariant()} at {sys.Name}.");
    }
}

/// <summary>Expand a venture one level (GDD §17).</summary>
public sealed class ExpandBusinessCommand : ICommand
{
    private readonly string _id; private readonly BusinessCatalog _ventures;
    public ExpandBusinessCommand(string actorId, string businessId, BusinessCatalog ventures) { ActorId = actorId; _id = businessId; _ventures = ventures; }
    public string ActorId { get; }
    public string Describe() => $"expand {_id}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null; var b = w.Business(_id); var def = b != null ? _ventures.Def(b.Kind) : null;
        if (actor?.Alive != true || house == null || b == null || def == null) return "no such venture";
        if (b.OwnerId != house.Id) return "not yours";
        if (b.Level >= def.MaxLevel) return "at its height";
        return house.Treasury.CanAfford(def.ExpandCost) ? null : $"needs {Presentation.CourtProjection.Bundle(def.ExpandCost)}";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var house = w.House(w.Char(ActorId)!.HouseId)!; var b = w.Business(_id)!; var def = _ventures.Def(b.Kind)!;
        house.Treasury.Spend(def.ExpandCost); house.Treasury.ClampNonCredit(); b.Level++;
    }
}

/// <summary>Sell a venture for six-tenths of its book value in credits (GDD §17).</summary>
public sealed class SellBusinessCommand : ICommand
{
    private readonly string _id; private readonly BusinessCatalog _ventures;
    public SellBusinessCommand(string actorId, string businessId, BusinessCatalog ventures) { ActorId = actorId; _id = businessId; _ventures = ventures; }
    public string ActorId { get; }
    public string Describe() => $"sell {_id}";
    public static int Price(BusinessDef def, Business b) => def.Value(b.Level) * 6 / 10;
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null; var b = w.Business(_id);
        if (actor?.Alive != true || house == null || b == null || _ventures.Def(b.Kind) == null) return "no such venture";
        return b.OwnerId == house.Id ? null : "not yours";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var house = w.House(w.Char(ActorId)!.HouseId)!; var b = w.Business(_id)!; var def = _ventures.Def(b.Kind)!;
        house.Treasury.Credits += Price(def, b); w.Businesses.Remove(_id);
        w.Log($"{house.Name} sells the {b.Name}.");
    }
}

/// <summary>Borrow the standing loan (GDD §17 "the one you run out of"): credits now, more credits later, in instalments
/// that fall due whether or not the ledger can bear them. The lender is the house with the strongest bank, if any.</summary>
public sealed class TakeLoanCommand : ICommand
{
    private readonly BusinessCatalog _ventures;
    public TakeLoanCommand(string actorId, BusinessCatalog ventures) { ActorId = actorId; _ventures = ventures; }
    public string ActorId { get; }
    public string Describe() => "take a loan";
    public static string? LenderFor(World w, string borrowerId, BusinessCatalog ventures) =>
        w.Businesses.Values.Where(b => b.OwnerId != borrowerId && ventures.Def(b.Kind)?.Lender == true).OrderByDescending(b => b.Level).ThenBy(b => b.Id, StringComparer.Ordinal).FirstOrDefault()?.OwnerId;
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null;
        if (actor?.Alive != true || house == null) return "no house";
        return house.Debt > 0 ? "already in debt" : null;
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var house = w.House(w.Char(ActorId)!.HouseId)!; var f = _ventures.Finance;
        house.Treasury.Credits += f.LoanPrincipal; house.Debt = f.LoanRepay; house.LenderId = LenderFor(w, house.Id, _ventures) ?? "";
        w.WorldCounters["loans"] = w.Counter("loans") + 1;
        var lender = house.LenderId.Length > 0 ? w.House(house.LenderId) : null;
        w.Log($"{house.Name} borrows {f.LoanPrincipal} credits from {(lender != null ? $"the bank of {lender.Name}" : "the caravaneers' counting-houses")}, owing {f.LoanRepay}.");
    }
}

/// <summary>Clear the loan in one payment.</summary>
public sealed class RepayLoanCommand : ICommand
{
    public RepayLoanCommand(string actorId) { ActorId = actorId; }
    public string ActorId { get; }
    public string Describe() => "repay the loan";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null;
        if (actor?.Alive != true || house == null) return "no house";
        if (house.Debt <= 0) return "nothing owed";
        return house.Treasury.Credits >= house.Debt ? null : $"needs {house.Debt} credits";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var house = w.House(w.Char(ActorId)!.HouseId)!;
        house.Treasury.Credits -= house.Debt; house.Debt = 0; house.LenderId = "";
        w.Log($"{house.Name} clears its debt.");
    }
}

/// <summary>
/// Buy a strategic resource on the open market (GDD §17 "decisions gated behind wealth and resources"). Helium-3 and rare earths
/// are sold at the capital's exchanges by whoever brings them up; the price climbs when few do. Isotopes are weapons-grade and
/// never on the open market — they come from a deposit, a venture, or an event. Credits per unit and no pool point: buying fuel is
/// a purser's errand, not a decree, and a rival's one Treasury action a turn should go to the yards and the lanes.
/// </summary>
public sealed class ProcureCommand : ICommand
{
    public const int Helium3Price = 6, RareEarthsPrice = 9, ScarcityStep = 2, ScarcityBelow = 4;
    private readonly string _resource; private readonly int _units; private readonly HoldingCatalog _catalog; private readonly BusinessCatalog _ventures; private readonly PolicyCatalog _policies;
    public ProcureCommand(string actorId, string resource, int units, HoldingCatalog catalog, BusinessCatalog ventures, PolicyCatalog? policies = null) { ActorId = actorId; _resource = resource; _units = Math.Max(1, units); _catalog = catalog; _ventures = ventures; _policies = policies ?? PolicyCatalog.Empty; }
    public string ActorId { get; }
    public string Describe() => $"procure {_units} {Resource.Label(_resource)}";

    /// <summary>Credits per unit: the base price, plus a step for every producer short of <see cref="ScarcityBelow"/>.</summary>
    public static int Price(World w, string resource, HoldingCatalog catalog, BusinessCatalog ventures, PolicyCatalog? policies = null, string? buyerHouseId = null)
    {
        int basePrice = resource == Resource.Helium3 ? Helium3Price : resource == Resource.RareEarths ? RareEarthsPrice : int.MaxValue;
        if (basePrice == int.MaxValue) return basePrice;
        int price = basePrice + Math.Max(0, ScarcityBelow - Systems.Economy.Producers(w, resource, catalog, ventures)) * ScarcityStep;
        if (policies != null && !policies.IsEmpty)   // tariffs and the merchants' regard on the buyer, the Treasurer's exchanges on everyone
        {
            int pct = Systems.PolicySystem.ReachModifier(w, policies, "market_price_pct") + (buyerHouseId != null ? Systems.PolicySystem.Modifier(w, policies, buyerHouseId, "market_price_pct") : 0);
            price = Math.Max(1, price * (100 + pct) / 100);
        }
        return price;
    }
    public int Cost(World w) => Price(w, _resource, _catalog, _ventures, _policies, w.Char(ActorId)?.HouseId) * _units;

    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null;
        if (actor?.Alive != true || house == null) return "no house";
        if (_resource == Resource.Isotopes) return "weapons-grade isotopes are not sold on any open market";
        if (!Resource.IsStrategic(_resource)) return "not a thing the exchanges deal in";
        if (Systems.SenateSystem.Embargoed(w, house.Id)) return "the Senate's embargo closes the exchanges to you";
        if (Systems.Economy.Producers(w, _resource, _catalog, _ventures) == 0) return $"no one in the Reach brings {Resource.Label(_resource)} up";
        return house.Treasury.Credits < Cost(w) ? $"needs {Cost(w)} credits" : null;
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;

    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!; var house = w.House(actor.HouseId)!;
        house.Treasury.Credits -= Cost(w); house.Treasury.Add(_resource, _units);
        w.WorldCounters["procured"] = w.Counter("procured") + _units;
    }
}
