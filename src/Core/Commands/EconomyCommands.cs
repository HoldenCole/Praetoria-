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
        return house.Treasury.Credits >= _ventures.Finance.InvestCredits ? null : $"needs {Money.Credits(_ventures.Finance.InvestCredits)}";
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

/// <summary>The holder of a system's world buys out a belt claim another house holds there (<see cref="ClaimSystem"/>).</summary>
public sealed class BuyOutClaimCommand : ICommand
{
    private readonly string _claimId; private readonly BusinessCatalog _ventures;
    public BuyOutClaimCommand(string actorId, string claimId, BusinessCatalog ventures) { ActorId = actorId; _claimId = claimId; _ventures = ventures; }
    public string ActorId { get; }
    public string Describe() => $"buy out {_claimId}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null;
        if (actor?.Alive != true || house == null) return "no house";
        return ClaimSystem.BuyOutRefusal(w, _ventures, house, w.Holding(_claimId));
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { var w = ctx.World; ClaimSystem.BuyOut(w, _ventures, w.House(w.Char(ActorId)!.HouseId)!, w.Holding(_claimId)!); }
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
        return house.Treasury.Credits >= f.ClaimCredits && house.Treasury.Materials >= f.ClaimMaterials ? null : $"needs {Money.Credits(f.ClaimCredits)}, {f.ClaimMaterials} materials";
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
    private readonly string _companyId;
    /// <param name="companyId">A company the house directs, to found the venture from the company's cash and into its hands; "" for the house's own.</param>
    public FoundBusinessCommand(string actorId, string defId, string systemId, BusinessCatalog ventures, HoldingCatalog catalog, Progression.TitleCatalog? titles = null, string companyId = "") { ActorId = actorId; _defId = defId; _systemId = systemId; _ventures = ventures; _catalog = catalog; _titles = titles; _companyId = companyId ?? ""; }
    public string ActorId { get; }
    public string Describe() => $"found {_defId} in {_systemId}";
    public static string IdFor(string houseId, string defId, string systemId) => $"biz_{houseId}_{defId}_{systemId}";

    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null; var def = _ventures.Def(_defId); var sys = w.Galaxy?.System(_systemId);
        if (actor?.Alive != true || house == null || def == null || sys == null) return "no such venture";
        if (w.Businesses.ContainsKey(IdFor(house.Id, _defId, _systemId))) return "you already have one here";
        if (ProjectSystem.Underway(w, house.Id, Project.Venture, _defId, _systemId)) return "already being founded here";
        var planet = w.Holdings.Values.Where(h => h.IsPlanet && h.SystemId == _systemId).OrderBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
        if (def.RequiresBelt && sys.Belt <= 0) return "needs a belt";
        if (def.RequiresDeposit != null && sys.Deposit(def.RequiresDeposit) == 0) return $"needs {Resource.A(def.RequiresDeposit)} deposit";
        if (def.RequiresPopulated && (planet == null || planet.Population <= 0)) return "needs a peopled world";
        if (def.RequiresOwnWorld && (planet == null || planet.OwnerId != house.Id)) return "needs a world of your own";
        if (def.MinLanes > 0 && w.Galaxy!.Neighbors(_systemId).Count() < def.MinLanes) return $"needs a hub of {def.MinLanes} lanes";
        if (def.PlanetClasses.Count > 0 && !def.PlanetClasses.Contains(sys.PlanetClass)) return $"needs a {string.Join(" or ", def.PlanetClasses)} world";
        if (def.Specializations.Count > 0 && (planet == null || !def.Specializations.Contains(planet.Specialization))) return $"needs {string.Join(" or ", def.Specializations.Select(s => _catalog.Spec(s)?.Name ?? s))}";
        if (def.MinTitleRank > 0 && (_titles?.RankOf(house.Title) ?? 0) < def.MinTitleRank) return "needs a greater title";
        if (planet != null && w.AtWar(house.Id, planet.OwnerId)) return "the world's holder is at war with you";
        if (planet != null && planet.OwnerId != house.Id && LocalLawSystem.Closed(w, _systemId)) return $"{planet.Name} is a closed world: its holder admits no venture but its own";   // local law (docs/POLITICS.md)
        var first = ProjectSystem.Split(def.Cost, def.BuildTurns).First;
        if (_companyId.Length > 0)
        {
            var co = w.Company(_companyId); if (co == null || co.Chartered) return "no such company";
            if (co.ControllerId != house.Id) return "you do not direct that company";
            int need = (int)Math.Ceiling(Economy.WorthOf(w, first));
            return co.Cash >= need ? null : $"the company needs {Money.Credits(need)} in hand (it has {Money.Show(co.Cash)})";
        }
        return house.Treasury.CanAfford(first) ? null : $"{house.Treasury.Short(first)}{(def.BuildTurns > 1 ? " to begin" : "")}";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var house = w.House(w.Char(ActorId)!.HouseId)!; var def = _ventures.Def(_defId)!; var sys = w.Galaxy!.System(_systemId)!;
        if (_companyId.Length > 0) { ProjectSystem.Start(w, house.Id, Project.Venture, def.Id, sys.Id, $"a {def.Name.ToLowerInvariant()} at {sys.Name}", def.Cost, Math.Max(1, def.BuildTurns), _companyId); return; }
        if (def.BuildTurns <= 0) { house.Treasury.Spend(def.Cost); house.Treasury.ClampNonCredit(); Open(w, house, def, sys); return; }
        ProjectSystem.Start(w, house.Id, Project.Venture, def.Id, sys.Id, $"a {def.Name.ToLowerInvariant()} at {sys.Name}", def.Cost, def.BuildTurns);
    }

    /// <summary>The venture opens its doors: it is on the books of the house from this turn.</summary>
    public static Business Open(World w, House house, BusinessDef def, Galaxy.StarSystem sys)
    {
        var id = IdFor(house.Id, def.Id, sys.Id);
        if (w.Businesses.TryGetValue(id, out var existing)) return existing;
        var b = new Business { Id = id, Name = $"{house.Name.Replace("House ", "")} {def.Name} of {sys.Name}", OwnerId = house.Id, Kind = def.Id, SystemId = sys.Id, Level = 1, Founded = w.Turn };
        w.Businesses[id] = b;
        w.WorldCounters["ventures_founded"] = w.Counter("ventures_founded") + 1;
        w.Log($"{house.Name} founds a {def.Name.ToLowerInvariant()} at {sys.Name}.");
        return b;
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
        if (ProjectSystem.Underway(w, house.Id, Project.Expansion, b.Kind, b.Id)) return "already being enlarged";
        if (b.CompanyId.Length > 0)   // a company's venture is enlarged from the company's cash, by whoever directs it
            return ProjectSystem.CanBegin(w, house.Id, def.ExpandCost, def.BuildTurns, b.CompanyId) ? null : $"the company needs {Money.Credits((int)Math.Ceiling(Economy.WorthOf(w, ProjectSystem.Split(def.ExpandCost, def.BuildTurns).First)))} in hand";
        var first = ProjectSystem.Split(def.ExpandCost, def.BuildTurns).First;
        return house.Treasury.CanAfford(first) ? null : $"{house.Treasury.Short(first)}{(def.BuildTurns > 1 ? " to begin" : "")}";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var house = w.House(w.Char(ActorId)!.HouseId)!; var b = w.Business(_id)!; var def = _ventures.Def(b.Kind)!;
        if (def.BuildTurns <= 0 && b.CompanyId.Length == 0) { house.Treasury.Spend(def.ExpandCost); house.Treasury.ClampNonCredit(); b.Level++; return; }
        ProjectSystem.Start(w, house.Id, Project.Expansion, b.Kind, b.Id, $"the enlargement of the {b.Name}", def.ExpandCost, Math.Max(1, def.BuildTurns), b.CompanyId);
    }
}

/// <summary>Borrow the standing loan (GDD §17 "the one you run out of"): credits now, more credits later, in instalments
/// that fall due whether or not the ledger can bear them. The lender is the house with the strongest bank, if any.</summary>
public sealed class TakeLoanCommand : ICommand
{
    private readonly BusinessCatalog _ventures; private readonly string? _lenderId; private readonly int _amount;
    public TakeLoanCommand(string actorId, BusinessCatalog ventures) { ActorId = actorId; _ventures = ventures; }
    /// <summary>Borrow a chosen amount of a chosen lender ("" is the counting-houses), on the terms <see cref="Lending.Lenders"/> gives.</summary>
    public TakeLoanCommand(string actorId, BusinessCatalog ventures, string lenderId, int amount) { ActorId = actorId; _ventures = ventures; _lenderId = lenderId; _amount = amount; }
    public string ActorId { get; }
    public string Describe() => _lenderId == null ? "take a loan" : $"borrow {Money.Show(_amount)} of {(_lenderId.Length == 0 ? "the counting-houses" : _lenderId)}";
    public static string? LenderFor(World w, string borrowerId, BusinessCatalog ventures) =>
        w.Businesses.Values.Where(b => b.OwnerId != borrowerId && ventures.Def(b.Kind)?.Lender == true).OrderByDescending(b => b.Level).ThenBy(b => b.Id, StringComparer.Ordinal).FirstOrDefault()?.OwnerId;
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null;
        if (actor?.Alive != true || house == null) return "no house";
        if (_lenderId == null) return house.Debt > 0 ? "already in debt" : null;
        var t = Lending.Lenders(w, house.Id, _ventures).FirstOrDefault(x => x.LenderId == _lenderId);
        if (t == null) return "no such lender";
        if (t.Refusal != null) return t.Refusal;
        if (_amount < Lending.Step) return $"no less than {Money.Credits(Lending.Step)}";
        int room = Lending.Room(w, house, t);
        if (room < Lending.Step) return $"{t.Lender} will lend you no more: its limit for you is {Money.Show(t.Limit)}, and you owe it {Money.Show(house.Debt)}{SenateActs.CapNote(w, t)}";
        return _amount <= room ? null : $"{t.Lender} will lend you {Money.Show(room)} and no more{SenateActs.CapNote(w, t)}";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var house = w.House(w.Char(ActorId)!.HouseId)!; var f = _ventures.Finance;
        if (_lenderId != null)
        {
            var t = Lending.Lenders(w, house.Id, _ventures).First(x => x.LenderId == _lenderId); int owed = Lending.Owed(_amount, t.RatePct);
            house.Treasury.Credits += _amount; house.Debt += owed; house.LenderId = _lenderId; house.LoanInstalment = Lending.Instalment(f, house.Debt);
            w.WorldCounters["loans"] = w.Counter("loans") + 1;
            w.Log($"{house.Name} borrows {Money.Credits(_amount)} from {t.Lender} at {t.RatePct} in the hundred, and owes {Money.Show(house.Debt)}: {Money.Show(house.LoanInstalment)} a year.");
            return;
        }
        var (lent, owes) = SenateActs.StandingLoan(w, f);   // the Usury Law, where the Senate has carried one: less is lent, and less is owed for it
        house.Treasury.Credits += lent; house.Debt = owes; house.LoanInstalment = 0; house.LenderId = LenderFor(w, house.Id, _ventures) ?? "";
        w.WorldCounters["loans"] = w.Counter("loans") + 1;
        var lender = house.LenderId.Length > 0 ? w.House(house.LenderId) : null;
        w.Log($"{house.Name} borrows {Money.Credits(lent)} from {(lender != null ? $"the bank of {lender.Name}" : "the caravaneers' counting-houses")}, owing {Money.Show(owes)}.");
    }
}

/// <summary>Pay the loan: the whole of it, or (amount above nought) a part. A part paid shortens the years; the yearly instalment stays as it was.</summary>
public sealed class RepayLoanCommand : ICommand
{
    private readonly int _amount;
    public RepayLoanCommand(string actorId, int amount = 0) { ActorId = actorId; _amount = amount; }
    public string ActorId { get; }
    public string Describe() => _amount > 0 ? $"pay {Money.Show(_amount)} of the loan" : "repay the loan";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null;
        if (actor?.Alive != true || house == null) return "no house";
        if (house.Debt <= 0) return "nothing owed";
        int pay = _amount > 0 ? Math.Min(_amount, house.Debt) : house.Debt;
        return house.Treasury.Credits >= pay ? null : $"needs {Money.Credits(pay)}";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var house = w.House(w.Char(ActorId)!.HouseId)!; int pay = _amount > 0 ? Math.Min(_amount, house.Debt) : house.Debt;
        house.Treasury.Credits -= pay; house.Debt -= pay;
        if (house.Debt > 0) { w.Log($"{house.Name} pays {Money.Credits(pay)} of its debt; {Money.Show(house.Debt)} is still owed."); return; }
        house.LenderId = ""; house.LoanInstalment = 0;
        w.Log($"{house.Name} clears its debt.");
    }
}
