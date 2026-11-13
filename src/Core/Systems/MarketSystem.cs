using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>What an order would cost or fetch, and why it cannot be placed if it cannot.</summary>
public sealed record TradeQuote(int Credits, double UnitPrice, string? Refusal);

/// <summary>
/// The Reach's commodity market (docs/ECONOMY_MARKET.md stage A; the owner's direction: prices that react to what is really happening —
/// war and scarcity). One price a good, recomputed each turn from the houses' own ledgers, RNG-free:
/// <list type="bullet">
/// <item>SUPPLY is what every house produced this turn (the positive lines of its ledger: sieges, raiders and revolts are already in
/// those lines) plus what friendly nations ship in.</item>
/// <item>DEMAND is what the houses consumed since the last reckoning (production less the change in what they hold, plus what they
/// bought), lifted by a percentage for every war: yards and magazines buying ahead.</item>
/// <item>The price follows the CHANGE of each from its own long-run average (a short average over a long one), so a Reach at peace
/// sits at the base price, a shock spikes, and the spike fades as the long average catches up.</item>
/// <item>A DEFICIT (the Reach needs more than it brings up) adds a standing premium; the merchants' STOCK, which orders and any
/// unusual surplus or shortfall move and which drifts back over years, adds depth: a house that buys a large share of it pays
/// more and leaves the price higher.</item>
/// </list>
/// Houses buy and sell through <see cref="Buy"/> and <see cref="Sell"/>: a spread, the tariff law, an order's own weight.
/// </summary>
public sealed class MarketSystem
{
    public const string BoughtCounter = "market_bought", SoldCounter = "market_sold";
    private readonly TradeRules _trade; private readonly Economy? _economy; private readonly FleetSystem? _fleets; private readonly NationCatalog _nations;
    public MarketSystem(TradeRules trade, Economy? economy, FleetSystem? fleets, NationCatalog? nations = null) { _trade = trade; _economy = economy; _fleets = fleets; _nations = nations ?? NationCatalog.Empty; }
    public TradeRules Trade => _trade;

    // ---------------------------------------------------------------- state

    /// <summary>The market as it stands; created at base prices with the merchants' least stock when a world has none yet (a hand-built
    /// test world, or a save from before the market).</summary>
    public static MarketState Of(World w, MarketCatalog cat)
    {
        w.Market ??= new MarketState();
        foreach (var d in cat.Goods)
            if (!w.Market.Goods.ContainsKey(d.Id))
                w.Market.Goods[d.Id] = new GoodState { Id = d.Id, Price = d.Base, Stock = d.MinStock, StockRef = d.MinStock, DemandShort = d.MinDemand, DemandLong = d.MinDemand, SupplyShort = d.MinDemand, SupplyLong = d.MinDemand, LastTotal = Total(w, d.Id), History = new List<double> { d.Base } };
        return w.Market;
    }

    private static int Total(World w, string good) => w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId).Sum(h => Math.Max(0, h.Treasury.Get(good)));

    /// <summary>What every house produced and needs of each good this turn, from its ledger.</summary>
    private Dictionary<string, (double produced, double needed)> Ledgers(World w)
    {
        var sums = _trade.Market.Goods.ToDictionary(g => g.Id, _ => (produced: 0.0, needed: 0.0));
        if (_economy == null) return sums;
        foreach (var h in w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId).OrderBy(h => h.Id, StringComparer.Ordinal))
            foreach (var line in _economy.Compute(w, h.Id, _fleets).Lines)
                foreach (var g in _trade.Market.Goods)
                {
                    int v = line.Delta.Get(g.Id); if (v == 0) continue;
                    var s = sums[g.Id]; sums[g.Id] = v > 0 ? (s.produced + v, s.needed) : (s.produced, s.needed - v);
                }
        return sums;
    }

    /// <summary>What the nations beyond the Reach ship in this turn: a nation at peace and not hostile exports; a subdued one, twice as much.</summary>
    public double Imports(World w, string good)
    {
        double sum = 0;
        foreach (var n in w.Nations)
        {
            var d = _nations.Def(n.Id); if (d == null || !d.Exports.TryGetValue(good, out var units)) continue;
            if (n.AtWar || n.Disposition <= NationsClosedAt) continue;
            sum += n.Subdued(w.Turn) ? units * 2 : units;
        }
        return sum;
    }
    public const int NationsClosedAt = -30, WarmUpTurns = 4;

    /// <summary>Is the good fetching a fair price: at least the given percentage of its base? A steward with a hoard waits for one.</summary>
    public static bool FairPrice(World w, MarketCatalog cat, string good, int pct = 80) => cat.Good(good) is { } d && Of(w, cat).Goods[d.Id].Price >= d.Base * pct / 100.0;

    private static int WarsOf(World w) => Math.Min(4, w.Wars.Count + (NationSystem.AtWar(w) != null ? 1 : 0));

    /// <summary>Open the markets on a new world: prices at base, the averages at this world's own production and consumption, the stock at
    /// its target. Idempotent.</summary>
    public void Seed(World w)
    {
        if (_trade.Market.IsEmpty) return;
        bool fresh = w.Market == null || w.Market.Goods.Count == 0;
        var m = Of(w, _trade.Market); if (!fresh) return;
        var ledgers = Ledgers(w); var rules = _trade.Market.Rules; int wars = WarsOf(w);
        foreach (var d in _trade.Market.Goods)
        {
            var g = m.Goods[d.Id]; var (produced, needed) = ledgers[d.Id];
            double supply = d.MinDemand + produced + Imports(w, d.Id), demand = (d.MinDemand + needed) * (1 + d.WarPct * wars / 100.0);
            g.SupplyShort = g.SupplyLong = supply; g.DemandShort = g.DemandLong = demand;
            g.Stock = g.StockRef = TargetStock(d, rules, g); g.LastTotal = Total(w, d.Id); g.LastProduction = produced; g.LastConsumption = needed;
            // a good the Reach is short of from the first day opens at its premium, so the first year is not a false alarm
            g.Deficit = needed <= 0 ? 0 : Math.Clamp((needed - produced - Imports(w, d.Id)) / needed, 0, 1);
            g.Price = d.Base * (1 + rules.DeficitPct / 100.0 * g.Deficit); g.History = new List<double> { g.Price }; g.Cause = "the markets open";
        }
        m.LastTick = w.Turn;
    }

    private static double TargetStock(GoodDef d, MarketRules r, GoodState g) => Math.Max(d.MinStock, r.CoverYears * r.SharePct / 100.0 * g.SupplyLong);

    // ---------------------------------------------------------------- the yearly reckoning

    public void Tick(World w)
    {
        if (_trade.Market.IsEmpty) return;
        if (w.Market == null || w.Market.Goods.Count == 0) { Seed(w); }
        var m = Of(w, _trade.Market); var rules = _trade.Market.Rules; var ledgers = Ledgers(w); int wars = WarsOf(w); var mine = w.Protagonist?.HouseId;
        foreach (var f in w.WorldFlags.Where(f => f.StartsWith("price_spike") || f.StartsWith("price_slump")).ToList()) w.WorldFlags.Remove(f);
        foreach (var d in _trade.Market.Goods)
        {
            var g = m.Goods[d.Id]; var (produced, needed) = ledgers[d.Id];
            int total = Total(w, d.Id); double imports = Imports(w, d.Id);
            // conservation: what the houses hold changed by production less consumption, plus what they bought, less what they sold
            double consumed = Math.Max(needed, produced - (total - g.LastTotal) + (g.Bought - g.Sold));
            consumed = Math.Min(consumed, needed + Math.Max(8, g.LastTotal));   // an event that empties a treasury is not a year's appetite
            double supply = d.MinDemand + produced + imports, demand = (d.MinDemand + consumed) * (1 + d.WarPct * wars / 100.0);
            double prevSupplyLong = g.SupplyLong, prevDemandLong = g.DemandLong;
            // the first years teach the market what is usual for this world (the opening estimate knows upkeep, not building): the long
            // average learns as fast as the short one until the fifth year, so an opening is not read as a shock
            int longPct = w.Turn <= WarmUpTurns ? rules.ShortPct : rules.LongPct;
            g.SupplyShort += (supply - g.SupplyShort) * rules.ShortPct / 100.0; g.SupplyLong += (supply - g.SupplyLong) * longPct / 100.0;
            g.DemandShort += (demand - g.DemandShort) * rules.ShortPct / 100.0; g.DemandLong += (demand - g.DemandLong) * longPct / 100.0;
            double pressure = (g.DemandShort / Math.Max(0.01, g.DemandLong)) / Math.Max(0.01, g.SupplyShort / Math.Max(0.01, g.SupplyLong));
            // a standing deficit: the Reach needs more than it brings up and is shipped
            g.Deficit = needed <= 0 ? 0 : Math.Clamp((needed - produced - imports) / needed, 0, 1);
            // the merchants' stock: the traded share of the year's surplus or shortfall, then a drift back toward what they like to hold
            double target = TargetStock(d, rules, g);
            // only the ABNORMAL part of the year's surplus or shortfall reaches the stock: the Reach's usual surplus is already where it usually goes
            g.Stock = Math.Max(0, g.Stock + rules.SharePct / 100.0 * ((supply - demand) - (prevSupplyLong - prevDemandLong)));
            g.Stock += (target - g.Stock) * rules.RelaxPct / 100.0;
            double depth = Math.Clamp(1 + rules.DepthPct / 100.0 * (1 - g.Stock / Math.Max(1, target)), 0.75, 1.75);
            double factor = (1 + d.Sensitivity * (pressure - 1)) * (1 + rules.DeficitPct / 100.0 * g.Deficit) * depth;
            double targetPrice = d.Base * Math.Clamp(factor, rules.FloorPct / 100.0, rules.CapPct / 100.0);
            double before = g.Price;
            g.Price += (targetPrice - g.Price) * rules.EasePct / 100.0;
            g.Cause = Cause(w, d, g, before, wars, produced + imports, prevSupplyLong - d.MinDemand, demand, prevDemandLong, target);
            g.History.Add(g.Price); while (g.History.Count > rules.HistoryKept) g.History.RemoveAt(0);
            g.LastProduction = produced; g.LastConsumption = consumed; g.LastTotal = total; g.Bought = 0; g.Sold = 0; g.TopBuyer = ""; g.TopBought = 0; g.StockRef = g.Stock;
            if (g.Price >= d.Base * rules.SpikePct / 100.0) { w.WorldFlags.Add("price_spike"); w.WorldFlags.Add("price_spike:" + d.Id); }
            if (g.Price <= d.Base * rules.SlumpPct / 100.0) { w.WorldFlags.Add("price_slump"); w.WorldFlags.Add("price_slump:" + d.Id); }
        }
        m.LastTick = w.Turn;
    }

    /// <summary>Why the price moved, in the world's terms: wars, sieges, worlds in revolt, raiders, closed ports, a house buying heavily,
    /// a standing shortage — or the market settling.</summary>
    private string Cause(World w, GoodDef d, GoodState g, double before, int wars, double supplyNow, double supplyNorm, double demandNow, double demandNorm, double targetStock)
    {
        double move = before <= 0 ? 0 : (g.Price - before) / before * 100;
        var why = new List<string>();
        bool up = g.Price > before + 0.005, down = g.Price < before - 0.005;
        if (up)
        {
            if (wars > 0 && d.WarPct > 0) why.Add(w.Wars.Count > 4 ? "war across the Reach" : wars == 1 ? "a war" : $"{Number(wars)} wars");
            if (supplyNow < supplyNorm * 0.95)
            {
                var sieged = w.Sieges.Keys.Select(id => w.Holding(id)).Where(h => h != null && Yields(h!, d.Id)).Select(h => h!.Name).OrderBy(n => n, StringComparer.Ordinal).Take(2).ToList();
                if (sieged.Count > 0) why.Add(string.Join(" and ", sieged) + " under siege");
                int risen = w.Holdings.Values.Count(h => h.InRebellion && Yields(h, d.Id));
                if (risen > 0) why.Add(risen == 1 ? "a world in revolt" : $"{Number(risen)} worlds in revolt");
                var lawless = w.Galaxy?.Regions.Where(r => RegionSystem.StateOf(w, r.Id).Lawlessness >= 60 && w.Holdings.Values.Any(h => w.Galaxy.System(h.SystemId)?.RegionId == r.Id && Yields(h, d.Id))).Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal).FirstOrDefault();
                if (lawless != null) why.Add($"raiders on the lanes of {lawless}");
            }
            var closed = w.Nations.Where(n => _nations.Def(n.Id) is { } nd && nd.Exports.ContainsKey(d.Id) && n.AtWar).Select(n => _nations.Def(n.Id)!.Name).OrderBy(n => n, StringComparer.Ordinal).FirstOrDefault();
            if (closed != null) why.Add($"no ships from {closed}");
            if (g.TopBought >= Math.Max(2, g.StockRef / 10) && w.House(g.TopBuyer) is { } buyer) why.Add($"heavy buying by {buyer.Name}");
            if (g.Deficit >= 0.15) why.Add("the Reach burns more than it brings up");
            if (why.Count == 0) why.Add(g.Stock < targetStock * 0.9 ? "the merchants' stocks are thin" : "demand is rising");
        }
        else if (down)
        {
            if (wars == 0 && before > d.Base * 1.05) why.Add("peace");
            if (g.Sold > Math.Max(2, g.StockRef / 10)) why.Add("heavy selling");
            if (supplyNow > supplyNorm * 1.05) why.Add("more is coming up");
            if (why.Count == 0) why.Add(before > d.Base ? "the market settles" : "the merchants' stocks are full");
        }
        else return "steady";
        return $"{(up ? "up" : "down")} {Math.Abs(move):0}% on the year: {string.Join("; ", why)}";
    }
    private static string Number(int n) => n switch { 2 => "two", 3 => "three", 4 => "four", 5 => "five", _ => n.ToString() };
    private bool Yields(Holding h, string good) =>
        (_trade.Holdings.Spec(h.Specialization)?.BaseYield.Get(good) ?? 0) > 0 || h.Buildings.Any(b => (_trade.Holdings.Building(b)?.Yield.Get(good) ?? 0) > 0) || (good == Resource.Materials && h.IsClaim);

    // ---------------------------------------------------------------- orders

    /// <summary>Has the house a market of its own, or the capital? Then it trades at the narrow spread; otherwise the caravaneers take a wider one.</summary>
    public static bool HasMarket(World w, string houseId, HoldingCatalog holdings) =>
        (holdings.ExchangeBuildings.Count > 0 && w.HoldingsOf(houseId).Any(h => h.Buildings.Any(b => holdings.ExchangeBuildings.Contains(b)))) || RegionSystem.CapitalHolder(w)?.Id == houseId;

    private static double Spread(World w, string houseId, TradeRules t) => (HasMarket(w, houseId, t.Holdings) ? t.Market.Rules.SpreadPct : t.Market.Rules.CaravanSpreadPct) / 100.0;
    private static double Tariff(World w, string houseId, TradeRules t) =>
        t.Policies.IsEmpty ? 1 : Math.Max(0.1, 1 + (PolicySystem.ReachModifier(w, t.Policies, "market_price_pct") + PolicySystem.Modifier(w, t.Policies, houseId, "market_price_pct")) / 100.0);

    /// <summary>The price a house would pay for the next unit, with its spread and the tariff, before its order's own weight.</summary>
    public static double BuyPrice(World w, TradeRules t, string houseId, string good) =>
        t.Market.Good(good) is { } d ? Of(w, t.Market).Goods[d.Id].Price * (1 + Spread(w, houseId, t) / 2) * Tariff(w, houseId, t) : double.MaxValue;
    public static double SellPrice(World w, TradeRules t, string houseId, string good) =>
        t.Market.Good(good) is { } d ? Of(w, t.Market).Goods[d.Id].Price * (1 - Spread(w, houseId, t) / 2) : 0;

    /// <summary>What buying <paramref name="units"/> would cost: each unit dearer than the last by the order's share of the stock the
    /// turn opened with, counting what has already been bought this turn (so an order split in ten costs what it costs whole).</summary>
    public static TradeQuote QuoteBuy(World w, TradeRules t, string houseId, string good, int units)
    {
        var d = t.Market.Good(good); var house = w.House(houseId);
        if (d == null || house == null) return new TradeQuote(0, 0, "not a thing the exchanges deal in");
        if (units <= 0) return new TradeQuote(0, 0, "nothing ordered");
        if (!d.Buy) return new TradeQuote(0, 0, $"{d.Name.ToLowerInvariant()} are not bought on the exchanges");
        if (d.License.Length > 0 && (t.Policies.IsEmpty || PolicySystem.ReachModifier(w, t.Policies, d.License) <= 0)) return new TradeQuote(0, 0, good == Resource.Isotopes ? "weapons-grade isotopes are not sold on any open market" : $"{d.Name.ToLowerInvariant()} need a licence of the Senate");
        if (SenateSystem.Embargoed(w, houseId)) return new TradeQuote(0, 0, "the Senate's embargo closes the exchanges to you");
        var g = Of(w, t.Market).Goods[d.Id];
        if (g.Stock < units) return new TradeQuote(0, 0, g.Stock < 1 ? $"the merchants have no {Resource.Label(good)} to sell" : $"the merchants hold only {Math.Floor(g.Stock):0}");
        double unit = BuyPrice(w, t, houseId, good); double reference = Math.Max(4, g.StockRef); double already = Math.Max(0, g.Bought - g.Sold);
        double cost = unit * (units + t.Market.Rules.ImpactPct / 100.0 * (units * already + units * units / 2.0) / reference);
        int credits = Math.Max(1, (int)Math.Ceiling(cost - 1e-9));
        return new TradeQuote(credits, cost / units, house.Treasury.Credits < credits ? $"needs {credits} credits" : null);
    }

    /// <summary>What selling <paramref name="units"/> would fetch: each unit cheaper than the last by the order's weight; never less than
    /// three-tenths of the quoted price.</summary>
    public static TradeQuote QuoteSell(World w, TradeRules t, string houseId, string good, int units)
    {
        var d = t.Market.Good(good); var house = w.House(houseId);
        if (d == null || house == null) return new TradeQuote(0, 0, "not a thing the exchanges deal in");
        if (units <= 0) return new TradeQuote(0, 0, "nothing offered");
        if (!d.Sell) return new TradeQuote(0, 0, $"{d.Name.ToLowerInvariant()} are not sold on the exchanges");
        if (SenateSystem.Embargoed(w, houseId)) return new TradeQuote(0, 0, "the Senate's embargo closes the exchanges to you");
        if (house.Treasury.Get(good) < units) return new TradeQuote(0, 0, $"you hold only {house.Treasury.Get(good)}");
        var g = Of(w, t.Market).Goods[d.Id];
        double unit = SellPrice(w, t, houseId, good); double reference = Math.Max(4, g.StockRef) + Math.Max(0, g.Sold - g.Bought); double already = Math.Max(0, g.Sold - g.Bought);
        double fetched = unit * Math.Max(0.3 * units, units - t.Market.Rules.ImpactPct / 100.0 * (units * already + units * units / 2.0) / (reference + units));
        int credits = Math.Max(0, (int)Math.Floor(fetched + 1e-9));
        return new TradeQuote(credits, fetched / units, null);
    }

    public static bool Buy(World w, TradeRules t, string houseId, string good, int units)
    {
        var q = QuoteBuy(w, t, houseId, good, units); if (q.Refusal != null) return false;
        var house = w.House(houseId)!; var g = Of(w, t.Market).Goods[good];
        house.Treasury.Credits -= q.Credits; house.Treasury.Add(good, units);
        g.Stock -= units; g.Bought += units;
        double mine = w.Counter($"market_buy:{w.Turn}:{houseId}:{good}") + units; w.WorldCounters[$"market_buy:{w.Turn}:{houseId}:{good}"] = (int)mine;
        if (mine > g.TopBought) { g.TopBought = mine; g.TopBuyer = houseId; }
        w.WorldCounters[BoughtCounter] = w.Counter(BoughtCounter) + units;
        if (Resource.IsStrategic(good)) w.WorldCounters["procured"] = w.Counter("procured") + units;
        return true;
    }

    public static bool Sell(World w, TradeRules t, string houseId, string good, int units)
    {
        var q = QuoteSell(w, t, houseId, good, units); if (q.Refusal != null) return false;
        var house = w.House(houseId)!; var g = Of(w, t.Market).Goods[good];
        house.Treasury.Credits += q.Credits; house.Treasury.Add(good, -units);
        g.Stock += units; g.Sold += units;
        w.WorldCounters[SoldCounter] = w.Counter(SoldCounter) + units;
        return true;
    }

    /// <summary>Last year's order tallies are swept so the World does not carry them forever.</summary>
    public static void Sweep(World w)
    {
        foreach (var k in w.WorldCounters.Keys.Where(k => k.StartsWith("market_buy:")).ToList())
        {
            var parts = k.Split(':'); if (parts.Length > 1 && int.TryParse(parts[1], out var turn) && turn < w.Turn) w.WorldCounters.Remove(k);
        }
    }
}
