namespace Praetoria.Core.State;

/// <summary>One good on the Reach's commodity market as it stands (docs/ECONOMY_MARKET.md stage A): its price, the merchants' stock,
/// the running averages of demand and supply the price is read from, this turn's orders, and a short history. Serialised with the World.</summary>
public sealed class GoodState
{
    public string Id { get; set; } = "";
    /// <summary>Credits a unit, before the spread and any tariff.</summary>
    public double Price { get; set; }
    /// <summary>What the merchants hold. Orders draw it down or fill it at once; it drifts back to its target over years.</summary>
    public double Stock { get; set; }
    /// <summary>The stock when this turn's trading opened: the yardstick an order's weight is measured against.</summary>
    public double StockRef { get; set; }
    public double DemandShort { get; set; }
    public double DemandLong { get; set; }
    public double SupplyShort { get; set; }
    public double SupplyLong { get; set; }
    /// <summary>Units the houses bought and sold this turn so far.</summary>
    public double Bought { get; set; }
    public double Sold { get; set; }
    /// <summary>The house that bought most this turn, for the cause line.</summary>
    public string TopBuyer { get; set; } = "";
    public double TopBought { get; set; }
    /// <summary>The Reach's total holdings of the good at the last reckoning, from which consumption is read.</summary>
    public int LastTotal { get; set; }
    public double LastProduction { get; set; }
    public double LastConsumption { get; set; }
    /// <summary>The share of the Reach's need that its own production and imports do not meet, 0..1.</summary>
    public double Deficit { get; set; }
    /// <summary>Why the price moved this year, in the world's terms.</summary>
    public string Cause { get; set; } = "";
    /// <summary>Recent prices, oldest first.</summary>
    public List<double> History { get; set; } = new();
}

/// <summary>The Reach's markets: the commodity prices now, and (stage C) the Exchange's confidence and index.</summary>
public sealed class MarketState
{
    public Dictionary<string, GoodState> Goods { get; set; } = new();
    /// <summary>The turn the markets were last reckoned.</summary>
    public int LastTick { get; set; } = -1;
    public GoodState? Good(string id) => Goods.TryGetValue(id, out var g) ? g : null;
}
