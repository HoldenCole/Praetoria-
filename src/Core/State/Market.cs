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

    // ---- cornering (docs/ECONOMY_MARKET.md, "Cornering a good"): who held the most of all there is at the last reckoning
    /// <summary>The house that held the largest share of all there is of the good, if that share was a third or more; "" otherwise.</summary>
    public string CornerHouse { get; set; } = "";
    /// <summary>That house's share of all there is (the merchants' stock and every house's store), 0..1.</summary>
    public double CornerShare { get; set; }
    /// <summary>Whether that house holds half or more: the good is cornered.</summary>
    public bool Cornered { get; set; }
    /// <summary>The turn the corner that stands was made (-1 = no corner stands).</summary>
    public int CornerSince { get; set; } = -1;
    /// <summary>What the holding adds to the price the market aims at, as a share of it: 0 = nothing, 0.5 = half as much again.</summary>
    public double CornerPremium { get; set; }
}

/// <summary>The Reach's markets: the commodity prices now, and (stage C) the Exchange's confidence and index.</summary>
public sealed class MarketState
{
    public Dictionary<string, GoodState> Goods { get; set; } = new();
    /// <summary>The turn the markets were last reckoned.</summary>
    public int LastTick { get; set; } = -1;
    public GoodState? Good(string id) => Goods.TryGetValue(id, out var g) ? g : null;

    // ---- the Exchange (stage C): one number for how the Reach's money feels, and an index of every listed share
    /// <summary>Confidence in the Reach as a whole, about 0.6..1.25: down with war and crisis and debt, up with years of peace.</summary>
    public double Confidence { get; set; } = 1;
    /// <summary>All listed shares together, 100 when the Exchange opened.</summary>
    public double Index { get; set; } = 100;
    public List<double> IndexHistory { get; set; } = new();
    /// <summary>Years in a row with no war in the Reach or abroad.</summary>
    public int PeaceYears { get; set; }
    /// <summary>What moved the market this year, in the world's terms.</summary>
    public List<string> Movers { get; set; } = new();
    /// <summary>The turn the Treasurer last steadied the market from the Reach's purse (-1 = never).</summary>
    public int SteadiedTurn { get; set; } = -1;
    /// <summary>The turn the Exchange was last reckoned.</summary>
    public int LastExchangeTick { get; set; } = -1;
}
