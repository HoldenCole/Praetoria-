using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>Incorporate a venture (docs/ECONOMY_MARKET.md stage C): as a new company with a hundred shares, all the founder's, or folded
/// into a company the house already directs for new shares at today's price. Costs influence for the charter, no credits.</summary>
public sealed class IncorporateCommand : ICommand
{
    private readonly string _businessId, _companyId; private readonly CompanySystem _cs;
    public IncorporateCommand(string actorId, string businessId, string companyId, CompanySystem cs) { ActorId = actorId; _businessId = businessId; _companyId = companyId ?? ""; _cs = cs; }
    public string ActorId { get; }
    public string Describe() => _companyId.Length > 0 ? $"fold {_businessId} into {_companyId}" : $"incorporate {_businessId}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; if (w.Char(ActorId) is not { Alive: true } a) return "no such venture";
        return _cs.IncorporateRefusal(w, a.HouseId, w.Businesses.GetValueOrDefault(_businessId), _companyId);
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var house = w.House(w.Char(ActorId)!.HouseId)!; var b = w.Businesses[_businessId];
        if (_companyId.Length > 0) _cs.Fold(w, w.Company(_companyId)!, b); else _cs.Incorporate(w, house, b);
    }
}

/// <summary>Buy shares off the float at the Exchange's price, the order's own weight and the levy. A founder may refuse a takeover once.</summary>
public sealed class BuySharesCommand : ICommand
{
    private readonly string _companyId; private readonly int _n; private readonly CompanySystem _cs;
    public BuySharesCommand(string actorId, string companyId, int shares, CompanySystem cs) { ActorId = actorId; _companyId = companyId; _n = Math.Max(1, shares); _cs = cs; }
    public string ActorId { get; }
    public string Describe() => $"buy {_n} shares of {_companyId}";
    private string? HouseOf(World w) => w.Char(ActorId) is { Alive: true } c ? c.HouseId : null;
    public ShareQuote Quote(World w) => HouseOf(w) is { } h ? _cs.QuoteBuy(w, h, w.Company(_companyId), _n) : new ShareQuote(0, 0, 0, "no house");
    public string? Refusal(CommandContext ctx) => Quote(ctx.World).Refusal;
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { _cs.Buy(ctx.World, HouseOf(ctx.World)!, ctx.World.Company(_companyId)!, _n); }
}

/// <summary>Sell shares onto the float.</summary>
public sealed class SellSharesCommand : ICommand
{
    private readonly string _companyId; private readonly int _n; private readonly CompanySystem _cs;
    public SellSharesCommand(string actorId, string companyId, int shares, CompanySystem cs) { ActorId = actorId; _companyId = companyId; _n = Math.Max(1, shares); _cs = cs; }
    public string ActorId { get; }
    public string Describe() => $"sell {_n} shares of {_companyId}";
    private string? HouseOf(World w) => w.Char(ActorId) is { Alive: true } c ? c.HouseId : null;
    public ShareQuote Quote(World w) => HouseOf(w) is { } h ? _cs.QuoteSell(w, h, w.Company(_companyId), _n) : new ShareQuote(0, 0, 0, "no house");
    public string? Refusal(CommandContext ctx) => Quote(ctx.World).Refusal;
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { _cs.Sell(ctx.World, HouseOf(ctx.World)!, ctx.World.Company(_companyId)!, _n); }
}

/// <summary>What a company's director may order: float new shares for capital, buy shares back with the company's cash, set the
/// dividend, pay out the cash it is sitting on, or (holding every share) wind it up.</summary>
public sealed class BoardCommand : ICommand
{
    public const string Float = "float", Buyback = "buyback", Payout = "payout", Private = "private", Special = "special";
    private readonly string _companyId, _act; private readonly int _n; private readonly CompanySystem _cs;
    public BoardCommand(string actorId, string companyId, string act, int amount, CompanySystem cs) { ActorId = actorId; _companyId = companyId; _act = act; _n = amount; _cs = cs; }
    public string ActorId { get; }
    public string Describe() => $"{_act} {_companyId} {_n}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; if (w.Char(ActorId) is not { Alive: true } a) return "no such company"; var co = w.Company(_companyId);
        return _act switch
        {
            Float => _cs.FloatRefusal(w, a.HouseId, co, _n),
            Buyback => _cs.BuybackRefusal(w, a.HouseId, co, _n),
            Payout => co == null || co.Chartered ? "no such company" : co.ControllerId != a.HouseId ? "you do not direct it" : _n is < 0 or > 100 ? "between nothing and all of it" : co.PayoutPct == _n ? "already so" : null,
            Private => _cs.PrivateRefusal(w, a.HouseId, co),
            Special => _cs.SpecialRefusal(w, a.HouseId, co),
            _ => "no such order"
        };
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var co = w.Company(_companyId)!;
        switch (_act)
        {
            case Float: _cs.Float(w, co, _n); break;
            case Buyback: _cs.Buyback(w, co, _n); break;
            case Payout: co.PayoutPct = _n; w.Log($"{co.Name} will pay out {_n}% of what it earns."); break;
            case Private: _cs.TakePrivate(w, co); break;
            case Special: _cs.SpecialDividend(w, co); break;
        }
    }
}

/// <summary>Lend to the Reach for its war abroad (docs/ECONOMY_MARKET.md stage D): a bond at face, a coupon a year, repaid when the war ends.</summary>
public sealed class BuyBondCommand : ICommand
{
    private readonly CompanySystem _cs;
    public BuyBondCommand(string actorId, CompanySystem cs) { ActorId = actorId; _cs = cs; }
    public string ActorId { get; }
    public string Describe() => "buy a war bond";
    public string? Refusal(CommandContext ctx) => ctx.World.Char(ActorId) is { Alive: true } a ? _cs.BondRefusal(ctx.World, a.HouseId) : "no house";
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { _cs.BuyBond(ctx.World, ctx.World.Char(ActorId)!.HouseId); }
}

/// <summary>The Treasurer opens the Reach's purse under a falling market, once a term.</summary>
public sealed class SteadyMarketCommand : ICommand
{
    private readonly CompanySystem _cs;
    public SteadyMarketCommand(string actorId, CompanySystem cs) { ActorId = actorId; _cs = cs; }
    public string ActorId { get; }
    public string Describe() => "steady the market";
    public string? Refusal(CommandContext ctx) => ctx.World.Char(ActorId) is { Alive: true } a ? _cs.SteadyRefusal(ctx.World, a.HouseId) : "no house";
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { var w = ctx.World; _cs.Rescue(w, $"the Treasurer, {w.House(w.Char(ActorId)!.HouseId)?.Name}, opens the Reach's purse under the market"); }
}

/// <summary>The throne's hand on the Exchange, under the Imperium: grant a great company's monopoly to a house, or (the Edict standing
/// at proscriptions) declare a house's shares forfeit.</summary>
public sealed class ThroneExchangeCommand : ICommand
{
    public const string GrantAct = "grant", SeizeAct = "seize";
    private readonly string _act, _companyId, _houseId; private readonly CompanySystem _cs; private readonly Data.PolicyCatalog _policies;
    public ThroneExchangeCommand(string actorId, string act, string companyId, string houseId, CompanySystem cs, Data.PolicyCatalog policies) { ActorId = actorId; _act = act; _companyId = companyId; _houseId = houseId; _cs = cs; _policies = policies; }
    public string ActorId { get; }
    public string Describe() => $"{_act} {_companyId} {_houseId}".Trim();
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; if (w.Char(ActorId) is not { Alive: true } a) return "no house";
        return _act == GrantAct ? _cs.GrantRefusal(w, a.HouseId, w.Company(_companyId), _houseId) : _act == SeizeAct ? _cs.SeizeRefusal(w, a.HouseId, _houseId, _policies) : "no such order";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { if (_act == GrantAct) _cs.Grant(ctx.World, ctx.World.Company(_companyId)!, _houseId); else _cs.Seize(ctx.World, _houseId); }
}
