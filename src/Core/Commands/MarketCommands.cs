using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>Buy a good on the Reach's exchanges (docs/ECONOMY_MARKET.md stage A): credits for units at the market's price, the house's
/// spread, the tariff, and the order's own weight. No pool point: a purser's errand, not a decree.</summary>
public sealed class BuyGoodCommand : ICommand
{
    private readonly string _good; private readonly int _units; private readonly TradeRules _trade;
    public BuyGoodCommand(string actorId, string good, int units, TradeRules trade) { ActorId = actorId; _good = good; _units = Math.Max(1, units); _trade = trade; }
    public string ActorId { get; }
    public string Describe() => $"buy {_units} {Resource.Label(_good)}";
    private string? HouseOf(World w) => w.Char(ActorId) is { Alive: true } c ? c.HouseId : null;
    public TradeQuote Quote(World w) => HouseOf(w) is { } h ? MarketSystem.QuoteBuy(w, _trade, h, _good, _units) : new TradeQuote(0, 0, "no house");
    public int Cost(World w) => Quote(w).Credits;
    public string? Refusal(CommandContext ctx) => Quote(ctx.World).Refusal;
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { MarketSystem.Buy(ctx.World, _trade, HouseOf(ctx.World)!, _good, _units); }
}

/// <summary>Sell a good on the Reach's exchanges: units for credits, less the spread and the order's own weight.</summary>
public sealed class SellGoodCommand : ICommand
{
    private readonly string _good; private readonly int _units; private readonly TradeRules _trade;
    public SellGoodCommand(string actorId, string good, int units, TradeRules trade) { ActorId = actorId; _good = good; _units = Math.Max(1, units); _trade = trade; }
    public string ActorId { get; }
    public string Describe() => $"sell {_units} {Resource.Label(_good)}";
    private string? HouseOf(World w) => w.Char(ActorId) is { Alive: true } c ? c.HouseId : null;
    public TradeQuote Quote(World w) => HouseOf(w) is { } h ? MarketSystem.QuoteSell(w, _trade, h, _good, _units) : new TradeQuote(0, 0, "no house");
    public string? Refusal(CommandContext ctx) => Quote(ctx.World).Refusal;
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { MarketSystem.Sell(ctx.World, _trade, HouseOf(ctx.World)!, _good, _units); }
}
