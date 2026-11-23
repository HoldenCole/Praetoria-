using Praetoria.Core.Data;
using Praetoria.Core.Diagnostics;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>What a purchase or a sale between two houses would come to, and why it cannot be made if it cannot.</summary>
public sealed record HouseQuote(int Credits, double UnitPrice, string? Refusal);

/// <summary>What a standing deal delivers in a year, and the house that fell short if one did ("" if neither).</summary>
public readonly record struct Delivery(int Units, string ShortById);

/// <summary>
/// Trade between houses (docs/WEALTH.md, "Trade between houses"; the owner: "Yes you should be able to have trade deals and
/// buy stuff directly from other houses"). A house buys a good out of another house's store, sells out of its own, or
/// strikes a standing deal: so many units a year at a price fixed the day it is struck, for a term of years.
/// <list type="bullet">
/// <item>The goods are the ones the exchanges buy and sell (not manpower: the exchanges hire crews out and no house sells
/// its levies). Isotopes are sold between houses even while the law keeps them off the exchanges, at a premium.</item>
/// <item>A house KEEPS a floor of each good, two years of what it burns, and what it has promised under standing deals. It
/// sells what it holds above that, and buys up to it.</item>
/// <item>The PRICE is the market's, with no spread and no tariff, moved by what the house's head thinks of the other; a
/// lot that is a large part of what the seller can spare costs more a unit.</item>
/// <item>No house trades with one it is at war with, has blood with, thinks very ill of, or that the Senate has
/// embargoed.</item>
/// </list>
/// What has passed is kept in the record of dealings (<see cref="World.Dealings"/>); regard, a broken word and a default
/// are <see cref="DealingsSystem"/>'s own. RNG-free: where the other houses choose, a hash of their ids and the year chooses.
/// </summary>
public sealed class HouseTradeSystem
{
    // what is remembered, and the kind of the ledger's line
    public const string BoughtKind = "trade_bought", StruckKind = "deal_struck", KeptKind = "deal_kept", BrokenKind = "deal_broken", DefaultKind = "deal_default";
    public const string LedgerKind = "trade";
    public const string TradesKey = "house_trades", StruckKey = "trade_deals_struck", KeptKey = "trade_deals_kept", BrokenKey = "trade_deals_broken", DefaultedKey = "trade_deals_defaulted",
        OffersKey = "trade_offers_made", SeqKey = "trade_deals_seq";

    // a lot out of a store
    public static readonly int[] Lots = { 1, 5, 10, 25 };
    public const int LotMost = 999, KeepYears = 2;
    /// <summary>What a house keeps of a good whatever it burns.</summary>
    public static int Floor(string good) => good switch { Resource.Materials => 8, Resource.Helium3 => 4, _ => 2 };
    public const int RefuseAt = WealthSystem.RefuseGiftAt, DislikeMostPct = 40, FriendPointsAPct = 10, FriendMostPct = 5, PaysUnderPct = 10, ClosedPct = 50, LotSlopePct = 60;
    public const int CoinKept = DealingsSystem.AskCoinKeeps, TradeRegard = 1;

    // a standing deal
    public static readonly int[] Terms = { 5, 10 };
    public const int MostAYear = 25, FailYears = 2, IncomePart = 2;
    public const int KeptRegard = DealingsSystem.PledgeKeptRegard, BrokenRegard = DealingsSystem.PledgeBrokenRegard, DefaultRegard = DealingsSystem.DefaultRegard, MemoryYears = DealingsSystem.PledgeMemoryYears;

    // the other houses
    public const int OthersTradeOneIn = 6, OthersLotMost = 4, OthersDealLeast = 1, OthersDealMost = 3, OffersOneIn = 3, OfferEveryYears = 3, OfferLeast = 2;

    private readonly TradeRules _trade; private readonly Economy? _economy; private readonly FleetSystem? _fleets;
    public HouseTradeSystem(TradeRules trade, Economy? economy, FleetSystem? fleets) { _trade = trade; _economy = economy; _fleets = fleets; }

    private static void Count(World w, string key, int by = 1) => w.WorldCounters[key] = w.Counter(key) + by;
    private static uint Hash(string s) { uint h = 2166136261; foreach (char ch in s) h = (h ^ ch) * 16777619; return h; }
    private static string Signed(int n) => n.ToString("+0;-0");
    private static string Unit(double hundreds) => Money.Show(hundreds);
    /// <summary>So many of a good, in a sentence: "5 materials", and "1 unit of materials".</summary>
    public static string Of(int n, string good) => n == 1 ? $"1 unit of {Resource.Label(good)}" : $"{n} {Resource.Label(good)}";
    public static string SoldKey(int turn, string sellerId, string good) => $"house_sold:{turn}:{sellerId}:{good}";
    private static string WarmedKey(string a, string b) => string.CompareOrdinal(a, b) < 0 ? $"traded:{a}|{b}" : $"traded:{b}|{a}";
    /// <summary>The year a house last sent the player word of a bargain.</summary>
    public const string OfferedKey = "trade_offered";

    // ---------------------------------------------------------------- the goods, and what a house makes and burns

    /// <summary>The goods houses sell each other: every good the exchanges will take.</summary>
    public IEnumerable<GoodDef> Goods => _trade.Market.Goods.Where(g => g.Sell);
    private GoodDef? Good(string good) => _trade.Market.Good(good) is { Sell: true } d ? d : null;
    private string? NoGood(string good) => Good(good) != null ? null
        : good == Resource.Manpower ? "men are not sold between houses: the exchanges hire crews out, and a house's levies are its own" : "not a thing one house sells another";
    /// <summary>The market's price of a unit, in the books' hundreds.</summary>
    public double Mid(World w, string good) => MarketSystem.Of(w, _trade.Market).Goods[good].Price;
    /// <summary>Is the good kept off the exchanges as the law stands (isotopes, under the Arms Market)? Then it is sold between houses at a premium.</summary>
    public bool Closed(World w, string good) => Good(good) is { } d && !CornerSystem.Open(w, _trade, d);

    private Dictionary<string, Dictionary<string, (int made, int burn)>>? _memo;   // for the length of the year's turn only
    private Dictionary<string, (int made, int burn)> Flows(World w, House h)
    {
        if (_memo != null && _memo.TryGetValue(h.Id, out var hit)) return hit;
        var flows = ReadCache.Get("trade_flow:" + h.Id, () =>
        {
            var d = new Dictionary<string, (int made, int burn)>(); if (_economy == null) return d;
            foreach (var line in _economy.Compute(w, h.Id, _fleets).Lines)
            {
                if (line.Kind == LedgerKind) continue;   // what a deal brings is not what the house makes
                foreach (var r in Resource.All)
                {
                    int v = line.Delta.Get(r); if (v == 0) continue;
                    var f = d.TryGetValue(r, out var was) ? was : (0, 0); d[r] = v > 0 ? (f.Item1 + v, f.Item2) : (f.Item1, f.Item2 - v);
                }
            }
            return d;
        });
        if (_memo != null) _memo[h.Id] = flows;
        return flows;
    }
    /// <summary>What a house's own ledger brings up of a good in a year, and what it burns: standing deals left out.</summary>
    public (int made, int burn) Flow(World w, House h, string good) => Flows(w, h).TryGetValue(good, out var f) ? f : (0, 0);
    /// <summary>What a house takes in over a year, in credits, standing deals left out.</summary>
    public int Income(World w, House h) => Math.Max(1, _economy != null ? Flow(w, h, Resource.Credits).made : h.Wealth / 10);

    public static int Promised(World w, string sellerId, string good) => w.TradeDeals.Where(d => d.SellerId == sellerId && d.Good == good).Sum(d => d.Units);
    public static int Coming(World w, string buyerId, string good) => w.TradeDeals.Where(d => d.BuyerId == buyerId && d.Good == good).Sum(d => d.Units);

    /// <summary>What a house keeps by of a good: a floor, <see cref="KeepYears"/> years of what it burns, and this year's
    /// deliveries under its standing deals.</summary>
    public int Keeps(World w, House h, string good) => Floor(good) + KeepYears * Flow(w, h, good).burn + Promised(w, h.Id, good);
    /// <summary>What it would sell out of its store: what it holds above what it keeps.</summary>
    public int Spare(World w, House h, string good) => Math.Max(0, h.Treasury.Get(good) - Keeps(w, h, good));
    /// <summary>What it would buy: what it holds under its floor and <see cref="KeepYears"/> years of what it burns.</summary>
    public int Wants(World w, House h, string good) => Math.Max(0, Floor(good) + KeepYears * Flow(w, h, good).burn - h.Treasury.Get(good));
    /// <summary>What it makes in a year beyond what it burns and has promised; and what it burns beyond what it makes and is sent.</summary>
    public int Surplus(World w, House h, string good) { var f = Flow(w, h, good); return f.made - f.burn - Promised(w, h.Id, good); }
    public int Need(World w, House h, string good) { var f = Flow(w, h, good); return f.burn - f.made - Coming(w, h.Id, good); }

    /// <summary>What a house holds of a good and why it keeps what it keeps, in a line.</summary>
    public string KeepsSays(World w, House h, string good)
    {
        int burn = Flow(w, h, good).burn, promised = Promised(w, h.Id, good);
        return $"it holds {h.Treasury.Get(good)} and keeps {Keeps(w, h, good)} by: {Floor(good)} whatever comes"
            + (burn > 0 ? $", {Counted.Years(KeepYears)} of the {burn} it burns a year" : "") + (promised > 0 ? $", and the {promised} it has promised this year" : "");
    }

    // ---------------------------------------------------------------- who trades with whom, and at what price

    /// <summary>Why that house will have no trade with the player's, or null: war, blood, the Senate's embargo on either, or
    /// a head who thinks very ill of the asker.</summary>
    public static string? WontTrade(World w, House mine, House theirs)
    {
        if (w.AtWar(mine.Id, theirs.Id)) return "you are at war with them";
        if (FeudSystem.Between(w, mine.Id, theirs.Id)) return "there is blood between your houses, and nothing is bought or sold across it";
        if (SenateSystem.Embargoed(w, mine.Id)) return "the Senate's embargo is on your house: no house of the Reach buys of it or sells to it until it lifts";
        if (SenateSystem.Embargoed(w, theirs.Id)) return $"the Senate's embargo is on {theirs.Name}: no house of the Reach buys of it or sells to it until it lifts";
        int regard = DealingsSystem.RegardOf(w, theirs, mine);
        return regard <= RefuseAt ? $"{Economy.HeadOf(w, theirs)?.Name ?? "its head"} thinks {Signed(regard)} of you, and does not trade with a house it thinks {Signed(RefuseAt)} or worse of" : null;
    }
    /// <summary>The same between any two houses, as a yes or no: and neither has broken a deal with the other of late.</summary>
    public static bool Trades(World w, House a, House b) =>
        !w.AtWar(a.Id, b.Id) && !FeudSystem.Between(w, a.Id, b.Id) && !SenateSystem.Embargoed(w, a.Id) && !SenateSystem.Embargoed(w, b.Id)
        && DealingsSystem.RegardOf(w, a, b) > RefuseAt && DealingsSystem.RegardOf(w, b, a) > RefuseAt && Fault(w, a.Id, b.Id) == null && Fault(w, b.Id, a.Id) == null;

    /// <summary>What regard does to a price, in parts in the hundred: a point dearer for every point of dislike, forty at
    /// the most; a point cheaper for every ten of liking, five at the most.</summary>
    public static int RegardPct(int regard) => regard < 0 ? Math.Min(DislikeMostPct, -regard) : -Math.Min(FriendMostPct, regard / FriendPointsAPct);
    private double Base(World w, string good) => Mid(w, good) * (Closed(w, good) ? (100 + ClosedPct) / 100.0 : 1);
    /// <summary>What the seller asks of this buyer for a unit, before the lot's own weight.</summary>
    public double Asks(World w, House seller, House buyer, string good) => Base(w, good) * (100 + RegardPct(DealingsSystem.RegardOf(w, seller, buyer))) / 100.0;
    /// <summary>What the buyer will pay this seller for a unit: under the market by <see cref="PaysUnderPct"/> parts in the hundred, less to one it dislikes.</summary>
    public double Pays(World w, House buyer, House seller, string good) => Base(w, good) * (100 - PaysUnderPct - RegardPct(DealingsSystem.RegardOf(w, buyer, seller))) / 100.0;

    /// <summary>What a lot costs out of a store: each unit dearer than the last by the lot's share of what the seller could
    /// spare as the year stood, counting what it has sold already this year (so a lot split in ten costs what it costs whole).</summary>
    public int LotCost(World w, House seller, House buyer, string good, int units)
    {
        int already = w.Counter(SoldKey(w.Turn, seller.Id, good)); double reference = Math.Max(1, Spare(w, seller, good) + already);
        double cost = Asks(w, seller, buyer, good) * (units + LotSlopePct / 100.0 * (units * (double)already + units * (double)units / 2.0) / reference);
        return Math.Max(1, (int)Math.Ceiling(cost - 1e-9));
    }
    /// <summary>What the buyer hands over for a lot offered to it.</summary>
    public int LotFetches(World w, House buyer, House seller, string good, int units) => Math.Max(0, (int)Math.Floor(Pays(w, buyer, seller, good) * units + 1e-9));

    // ---------------------------------------------------------------- a purchase out of another house's store

    public HouseQuote BuyQuote(World w, Character? me, House? theirs, string good, int units)
    {
        if (DealingsSystem.Parties(w, me, theirs, out var mine, out _) is { } no) return new HouseQuote(0, 0, no);
        if (NoGood(good) is { } none) return new HouseQuote(0, 0, none);
        if (units < 1 || units > LotMost) return new HouseQuote(0, 0, units < 1 ? "nothing asked for" : $"a lot is of {LotMost} at the most");
        if (WontTrade(w, mine, theirs!) is { } wont) return new HouseQuote(0, 0, wont);
        int spare = Spare(w, theirs!, good); string label = Resource.Label(good);
        if (spare < units) return new HouseQuote(0, 0, spare == 0 ? $"{theirs!.Name} has no {label} to spare: {KeepsSays(w, theirs, good)}" : $"{theirs!.Name} would spare only {spare}: {KeepsSays(w, theirs, good)}");
        int cost = LotCost(w, theirs!, mine, good, units);
        return new HouseQuote(cost, cost / (double)units, mine.Treasury.Credits >= cost ? null : $"needs {Money.Credits(cost)} and the house has {Money.Show(mine.Treasury.Credits)}");
    }
    public string? BuyRefusal(World w, Character? me, House? theirs, string good, int units) => BuyQuote(w, me, theirs, good, units).Refusal;
    /// <summary>What the purchase is and what makes its price, said before it is made.</summary>
    public string BuySays(World w, House mine, House theirs, string good, int units)
    {
        int cost = LotCost(w, theirs, mine, good, units), spare = Spare(w, theirs, good);
        return $"{Of(units, good)} out of the store of {theirs.Name} for {Money.Credits(cost)}, {Unit(cost / (double)units)} a unit. {PriceSays(w, theirs, mine, good, selling: true)}"
            + $" It would spare {spare}, and a lot that is a large part of that costs more a unit. Its head thinks the better of you for a fair bargain, by {TradeRegard} and once a year.";
    }
    /// <summary>What makes the price a house names: the market's, the law, and what its head thinks of the other.</summary>
    public string PriceSays(World w, House theirs, House mine, string good, bool selling)
    {
        int regard = DealingsSystem.RegardOf(w, theirs, mine), pct = RegardPct(regard);
        string moved = pct == 0 ? "" : selling ? $", and {Math.Abs(pct)} in the hundred {(pct > 0 ? "dearer" : "cheaper")} to you" : $", and {Math.Abs(pct)} in the hundred {(pct > 0 ? "less" : "more")} to you";
        return $"The market's price is {Unit(Mid(w, good))}, and no caravaneer stands between you."
            + (Closed(w, good) ? $" The law keeps {Resource.Label(good)} off the exchanges, and a private sale is {ClosedPct} in the hundred dearer for it." : "")
            + (selling ? $" Its head thinks {Signed(regard)} of you{moved}." : $" It pays {PaysUnderPct} in the hundred under the market; its head thinks {Signed(regard)} of you{moved}.");
    }
    /// <summary>The goods and the credits change hands. Returns what was paid.</summary>
    public int Buy(World w, House buyer, House seller, string good, int units)
    {
        int cost = LotCost(w, seller, buyer, good, units); Move(w, seller, buyer, good, units, cost); return cost;
    }

    // ---------------------------------------------------------------- a sale to another house

    public HouseQuote SellQuote(World w, Character? me, House? theirs, string good, int units)
    {
        if (DealingsSystem.Parties(w, me, theirs, out var mine, out _) is { } no) return new HouseQuote(0, 0, no);
        if (NoGood(good) is { } none) return new HouseQuote(0, 0, none);
        if (units < 1 || units > LotMost) return new HouseQuote(0, 0, units < 1 ? "nothing offered" : $"a lot is of {LotMost} at the most");
        if (WontTrade(w, mine, theirs!) is { } wont) return new HouseQuote(0, 0, wont);
        string label = Resource.Label(good);
        if (mine.Treasury.Get(good) < units) return new HouseQuote(0, 0, $"you hold only {mine.Treasury.Get(good)}");
        int wants = Wants(w, theirs!, good), burn = Flow(w, theirs!, good).burn;
        string use = $"it holds {theirs!.Treasury.Get(good)}, and buys up to {Floor(good)}" + (burn > 0 ? $" and {Counted.Years(KeepYears)} of the {burn} it burns a year" : ": it burns none");
        if (wants < units) return new HouseQuote(0, 0, wants == 0 ? $"{theirs.Name} has no use for more {label}: {use}" : $"{theirs.Name} would take only {wants}: {use}");
        int fetch = LotFetches(w, theirs, mine, good, units);
        if (fetch < 1) return new HouseQuote(0, 0, "too small a lot to be worth a hundred credits to it");
        if (theirs.Treasury.Credits - fetch < CoinKept) return new HouseQuote(0, 0, $"{theirs.Name} has not the coin: it holds {Money.Show(theirs.Treasury.Credits)}, this is {Money.Show(fetch)}, and it keeps {Money.Show(CoinKept)} by");
        return new HouseQuote(fetch, fetch / (double)units, null);
    }
    public string? SellRefusal(World w, Character? me, House? theirs, string good, int units) => SellQuote(w, me, theirs, good, units).Refusal;
    public string SellSays(World w, House mine, House theirs, string good, int units)
    {
        int fetch = LotFetches(w, theirs, mine, good, units);
        return $"{Of(units, good)} out of your store to {theirs.Name} for {Money.Credits(fetch)}, {Unit(fetch / (double)units)} a unit. {PriceSays(w, theirs, mine, good, selling: false)}"
            + $" It would take {Wants(w, theirs, good)}. Its head thinks the better of you for a fair bargain, by {TradeRegard} and once a year.";
    }
    /// <summary>The player's house sells a lot to a house that has a use for it. Returns what was paid.</summary>
    public int Sell(World w, House seller, House buyer, string good, int units)
    {
        int fetch = LotFetches(w, buyer, seller, good, units); Move(w, seller, buyer, good, units, fetch); return fetch;
    }

    private static void Move(World w, House seller, House buyer, string good, int units, int credits)
    {
        seller.Treasury.Add(good, -units); buyer.Treasury.Add(good, units); buyer.Treasury.Credits -= credits; seller.Treasury.Credits += credits;
        Count(w, SoldKey(w.Turn, seller.Id, good), units); Count(w, TradesKey); Remember(w, BoughtKind, buyer.Id, seller.Id, credits, good, units); Warm(w, seller, buyer);
        string? mine = w.Protagonist?.HouseId;
        if (seller.Id == mine || buyer.Id == mine) w.Log($"{buyer.Name} buys {Of(units, good)} out of the store of {seller.Name} for {Money.Credits(credits)}.");
    }
    /// <summary>A fair bargain: each head thinks a little better of the other, once a year between any two houses. The
    /// player's own head thinks what the player pleases.</summary>
    private static void Warm(World w, House a, House b)
    {
        string key = WarmedKey(a.Id, b.Id); if (w.Counter(key) == w.Turn + 1) return;
        w.WorldCounters[key] = w.Turn + 1; Think(w, a, b, TradeRegard); Think(w, b, a, TradeRegard);
    }
    /// <summary>What the head of one house thinks of another's moves, as <see cref="DealingsSystem"/> moves it; but the
    /// player's own head is not told what to think.</summary>
    private static void Think(World w, House of, House toward, int delta) { if (of.Id != w.Protagonist?.HouseId) DealingsSystem.Regard(w, of, toward, delta); }
    /// <summary>Write down a thing that passed in trade; a second lot of the same good in the same year is added to the first.</summary>
    private static void Remember(World w, string kind, string fromHouseId, string toHouseId, int amount, string good, int units)
    {
        if (kind == BoughtKind && w.Dealings.LastOrDefault(d => d.Kind == kind && d.Turn == w.Turn && d.FromHouseId == fromHouseId && d.ToHouseId == toHouseId && d.Good == good) is { } same)
        { same.Amount += amount; same.Units += units; return; }
        w.Dealings.Add(new DealingRecord { Turn = w.Turn, Kind = kind, FromHouseId = fromHouseId, ToHouseId = toHouseId, Amount = amount, Good = good, Units = units });
    }

    // ---------------------------------------------------------------- a standing deal

    public static TradeDeal? Deal(World w, string dealId) => w.TradeDeals.FirstOrDefault(d => d.Id == dealId);
    /// <summary>The deals in force between two houses, either way, in the order they were struck.</summary>
    public static IEnumerable<TradeDeal> Between(World w, string a, string b) =>
        w.TradeDeals.Where(d => d.SellerId == a && d.BuyerId == b || d.SellerId == b && d.BuyerId == a).OrderBy(d => d.Seq);
    /// <summary>What a year's delivery of so many units costs, in the books' hundreds.</summary>
    public static int YearCost(int unitCredits, int units) => units <= 0 ? 0 : Math.Max(1, (int)((units * (long)unitCredits + Money.PerUnit / 2) / Money.PerUnit));
    /// <summary>The year a house last ended a deal with another early, or failed it twice running, if that is still held against it.</summary>
    public static DealingRecord? Fault(World w, string byHouseId, string toHouseId) =>
        w.Dealings.Where(d => d.Kind is BrokenKind or DefaultKind && d.FromHouseId == byHouseId && d.ToHouseId == toHouseId && w.Turn - d.Turn < MemoryYears).OrderByDescending(d => d.Turn).FirstOrDefault();

    /// <summary>The price a unit a standing deal would be struck at, in single credits: what that house asks, or what it pays.</summary>
    public int DealUnitCredits(World w, House mine, House theirs, string good, bool selling) =>
        Math.Max(1, (int)Math.Round((selling ? Pays(w, theirs, mine, good) : Asks(w, theirs, mine, good)) * Money.PerUnit));

    /// <summary>Why a standing deal cannot be struck, or null. <paramref name="selling"/>: the player's house sends the goods.</summary>
    public string? DealRefusal(World w, Character? me, House? theirs, string good, int units, int years, bool selling)
    {
        if (DealingsSystem.Parties(w, me, theirs, out var mine, out _) is { } no) return no;
        if (NoGood(good) is { } none) return none;
        if (units < 1 || units > MostAYear) return $"a standing deal is for 1 to {MostAYear} units a year";
        if (!Terms.Contains(years)) return "not a term that is offered";
        if (WontTrade(w, mine, theirs!) is { } wont) return wont;
        if (Fault(w, mine.Id, theirs!.Id) is { } fault)
            return $"you {(fault.Kind == BrokenKind ? "ended a deal with it early" : "failed it twice running")} in year {fault.Turn}: it strikes none with you before year {fault.Turn + MemoryYears}";
        string label = Resource.Label(good);
        if (Between(w, mine.Id, theirs.Id).Any(d => d.Good == good)) return $"a deal in {label} stands between your houses already";
        if (!selling)
        {
            int can = Surplus(w, theirs, good), promised = Promised(w, theirs.Id, good); var f = Flow(w, theirs, good);
            string makes = $"it brings up {f.made} a year and burns {f.burn}" + (promised > 0 ? $", and has promised {promised}" : "");
            if (can < units) return can <= 0 ? $"{theirs.Name} can promise no {label} every year: {makes}. What it will spare of its store is bought by the lot" : $"{theirs.Name} would promise only {can} a year: {makes}";
            return null;
        }
        int need = Need(w, theirs, good), coming = Coming(w, theirs.Id, good); var g = Flow(w, theirs, good);
        string burns = $"it burns {g.burn} a year and brings up {g.made}" + (coming > 0 ? $", and is sent {coming}" : "");
        if (need < units) return need <= 0 ? $"{theirs.Name} has no use for {label} every year: {burns}" : $"{theirs.Name} would take only {need} a year: {burns}";
        int yearly = YearCost(DealUnitCredits(w, mine, theirs, good, true), units), income = Income(w, theirs);
        return yearly * IncomePart > income ? $"{theirs.Name} takes in {Money.Credits(income)} a year and will not promise more than half of it: this is {Money.Show(yearly)} a year" : null;
    }
    public string DealSays(World w, House mine, House theirs, string good, int units, int years, bool selling)
    {
        int unit = DealUnitCredits(w, mine, theirs, good, selling), yearly = YearCost(unit, units);
        return (selling ? $"You send {theirs.Name} {Of(units, good)} every year for {Counted.Years(years)}, from next year, at {Unit(unit / (double)Money.PerUnit)} a unit: {Money.Credits(yearly)} a year into your treasury when the year's books are done. "
                : $"{theirs.Name} sends you {Of(units, good)} every year for {Counted.Years(years)}, from next year, at {Unit(unit / (double)Money.PerUnit)} a unit: {Money.Credits(yearly)} a year out of your treasury when the year's books are done. ")
            + PriceSays(w, theirs, mine, good, !selling) + " The price is fixed for the term, whatever the market does. " + RulesSay;
    }
    public static string RulesSay =>
        $"A year in which the seller has not the goods it sends what it has; a year in which the buyer has not the credits nothing is sent. {Counted.Years(FailYears)} of that running end the deal, and the head of the other house thinks {Signed(-DefaultRegard)} of the one at fault. "
        + $"Either may end it early: the other's head then thinks {Signed(-BrokenRegard)} of the one that did. Either way the house wronged strikes no deal with the other for {Counted.Years(MemoryYears)}.";

    /// <summary>The deal is struck: by the player's head, or between two other houses' stewards.</summary>
    public static TradeDeal Strike(World w, House seller, House buyer, string good, int units, int years, int unitCredits)
    {
        int seq = w.Counter(SeqKey) + 1; w.WorldCounters[SeqKey] = seq;
        var deal = new TradeDeal { Id = "deal" + seq, Seq = seq, SellerId = seller.Id, BuyerId = buyer.Id, Good = good, Units = units, UnitCredits = unitCredits, Since = w.Turn, Until = w.Turn + years };
        w.TradeDeals.Add(deal); Count(w, StruckKey); Remember(w, StruckKind, buyer.Id, seller.Id, YearCost(unitCredits, units), good, units); Warm(w, seller, buyer);
        string? mine = w.Protagonist?.HouseId;
        if (seller.Id == mine || buyer.Id == mine)
            w.Log($"{seller.Name} and {buyer.Name} strike a standing deal: {Of(units, good)} a year for {Counted.Years(years)}, at {Unit(unitCredits / (double)Money.PerUnit)} a unit.");
        return deal;
    }

    /// <summary>What a deal is, as one of its houses sees it.</summary>
    public static string Describe(World w, TradeDeal d, string houseId)
    {
        bool sells = d.SellerId == houseId; string other = w.House(sells ? d.BuyerId : d.SellerId)?.Name ?? "a house that is gone"; int yearly = YearCost(d.UnitCredits, d.Units);
        return (sells ? $"You send {other} {Of(d.Units, d.Good)} a year" : $"{other} sends you {Of(d.Units, d.Good)} a year")
            + $" at {Unit(d.UnitCredits / (double)Money.PerUnit)} a unit: {Money.Credits(yearly)} a year {(sells ? "to you" : "from you")}, until year {d.Until}."
            + (d.Short > 0 ? $" It has fallen {d.Short} short so far." : "") + (d.Failed > 0 ? " The last delivery failed: one more such year ends it." : "");
    }

    public string? EndRefusal(World w, Character? me, string dealId)
    {
        var house = me != null ? w.House(me.HouseId) : null; if (me?.Alive != true || house == null) return "no house";
        if (Economy.HeadOf(w, house)?.Id != me.Id) return "only the head of a house deals with another in its name";
        var d = Deal(w, dealId); return d == null || d.SellerId != house.Id && d.BuyerId != house.Id ? "no such deal of your house's" : null;
    }
    public static string EndSays(World w, TradeDeal d, string houseId) =>
        $"End it now, with {Counted.Years(Math.Max(0, d.Until - w.Turn))} still to run. The head of {w.House(d.SellerId == houseId ? d.BuyerId : d.SellerId)?.Name} thinks {Signed(-BrokenRegard)} of you for a word not kept, and the house strikes no deal with yours for {Counted.Years(MemoryYears)}.";
    /// <summary>One side ends a deal before its term: a word not kept, as a broken pledge is.</summary>
    public static void End(World w, TradeDeal d, House by)
    {
        w.TradeDeals.Remove(d); var other = w.House(d.SellerId == by.Id ? d.BuyerId : d.SellerId); if (other == null) return;
        Think(w, other, by, -BrokenRegard); Count(w, BrokenKey); Remember(w, BrokenKind, by.Id, other.Id, YearCost(d.UnitCredits, d.Units), d.Good, d.Units);
        string? mine = w.Protagonist?.HouseId; string label = Resource.Label(d.Good);
        if (by.Id == mine || other.Id == mine) w.Log($"{by.Name} ends its standing deal in {label} with {other.Name} before its term.");
        if (other.Id == mine)
            w.Tell("warn", $"{Economy.HeadOf(w, by)?.Name ?? "The head"} of {by.Name} has ended the standing deal in {label} before its term: {(d.SellerId == by.Id ? "it can no longer spare what it promised" : "it will take no more")}. It was {d.Units} a year until year {d.Until}.", "houses", by.Id);
    }

    // ---------------------------------------------------------------- the year's deliveries

    /// <summary>What every standing deal delivers this year as things stand, the earlier deal served first: the seller sends
    /// what it holds, up to what it promised; the buyer must have the credits for what is sent, or nothing is. Nothing
    /// passes between houses at war or with blood between them, nor to or from a house under the Senate's embargo.</summary>
    public static Dictionary<TradeDeal, Delivery> Due(World w)
    {
        var due = new Dictionary<TradeDeal, Delivery>(); if (w.TradeDeals.Count == 0) return due;
        var store = new Dictionary<string, int>(); var purse = new Dictionary<string, int>();
        foreach (var d in w.TradeDeals.OrderBy(d => d.Seq))
        {
            var s = w.House(d.SellerId); var b = w.House(d.BuyerId);
            if (s == null || b == null || Ended(w, d) || Held(w, d)) { due[d] = new Delivery(0, ""); continue; }
            string key = s.Id + ":" + d.Good; int has = store.TryGetValue(key, out int left) ? left : Math.Max(0, s.Treasury.Get(d.Good));
            int n = Math.Min(d.Units, has), coin = purse.TryGetValue(b.Id, out int c) ? c : b.Treasury.Credits;
            if (n > 0 && coin < YearCost(d.UnitCredits, n)) { due[d] = new Delivery(0, b.Id); continue; }
            store[key] = has - n; purse[b.Id] = coin - YearCost(d.UnitCredits, n);
            due[d] = new Delivery(n, n < d.Units ? s.Id : "");
        }
        return due;
    }
    /// <summary>War or blood between the two ends a deal, and neither is at fault.</summary>
    private static bool Ended(World w, TradeDeal d) => w.AtWar(d.SellerId, d.BuyerId) || FeudSystem.Between(w, d.SellerId, d.BuyerId);
    /// <summary>The Senate's embargo on either holds a deal up for as long as it lasts, and neither is at fault.</summary>
    private static bool Held(World w, TradeDeal d) => SenateSystem.Embargoed(w, d.SellerId) || SenateSystem.Embargoed(w, d.BuyerId);

    /// <summary>The lines a house's ledger carries for its standing deals (called by the economy): the goods one way and the
    /// credits the other, as <see cref="Due"/> has them, or as they were settled when the year's books were opened.</summary>
    public static IEnumerable<(string source, Resources delta)> LedgerLines(World w, string houseId, IReadOnlyDictionary<TradeDeal, Delivery>? settled)
    {
        if (w.TradeDeals.Count == 0 || !w.TradeDeals.Any(d => d.SellerId == houseId || d.BuyerId == houseId)) yield break;
        var due = settled ?? Due(w);
        foreach (var d in w.TradeDeals.Where(d => d.SellerId == houseId || d.BuyerId == houseId).OrderBy(d => d.Seq))
        {
            if (!due.TryGetValue(d, out var sent) || sent.Units <= 0) continue;
            bool sells = d.SellerId == houseId; int cost = YearCost(d.UnitCredits, sent.Units); var delta = new Resources { Credits = sells ? cost : -cost }; delta.Add(d.Good, sells ? -sent.Units : sent.Units);
            yield return ($"{Resource.Label(d.Good)} {(sells ? "sold to" : "bought of")} {w.House(sells ? d.BuyerId : d.SellerId)?.Name}", delta);
        }
    }

    /// <summary>The year's deliveries have been made, or not (called by the economy once every house's books are done).</summary>
    public static void Settled(World w, IReadOnlyDictionary<TradeDeal, Delivery> due)
    {
        string? mine = w.Protagonist?.HouseId;
        foreach (var d in w.TradeDeals.OrderBy(d => d.Seq).ToList())
        {
            var s = w.House(d.SellerId); var b = w.House(d.BuyerId); string label = Resource.Label(d.Good);
            if (s == null || b == null)
            {
                w.TradeDeals.Remove(d);
                if ((d.SellerId == mine || d.BuyerId == mine) && (s ?? b) != null) w.Tell("info", $"The house you had a standing deal in {label} with is no more, and the deal with it.", "house:ledger");
                continue;
            }
            if (!due.TryGetValue(d, out var sent)) continue;   // struck since the year's books were opened: nothing falls due yet
            var other = s.Id == mine ? b : s;
            if (Ended(w, d))
            {
                w.TradeDeals.Remove(d);
                if (s.Id == mine || b.Id == mine) w.Tell("info", $"The standing deal in {label} with {other.Name} is at an end: nothing is bought or sold across a war, or across blood.", "houses", other.Id);
                continue;
            }
            if (Held(w, d))
            {
                if (s.Id == mine || b.Id == mine) w.Tell("info", $"Nothing passed this year under the standing deal in {label} with {other.Name}: the Senate's embargo holds it up. It is held against neither house.", "houses", other.Id);
            }
            else if (sent.Units >= d.Units) { d.Delivered += sent.Units; d.Failed = 0; d.FailedById = ""; }
            else
            {
                d.Delivered += sent.Units; d.Short += d.Units - sent.Units; d.Failed += 1; d.FailedById = sent.ShortById; bool buyerShort = sent.ShortById == b.Id;
                if (d.Failed >= FailYears) { Default(w, d, buyerShort ? b : s, buyerShort ? s : b); continue; }
                if (s.Id == mine || b.Id == mine)
                    w.Tell("warn", (buyerShort ? (b.Id == mine ? $"You had not the {Money.Credits(YearCost(d.UnitCredits, d.Units))} for this year's {Of(d.Units, d.Good)} from {s.Name}, and nothing was sent."
                                : $"{b.Name} had not the credits for this year's {Of(d.Units, d.Good)}, and nothing was sent.")
                            : s.Id == mine ? $"You had only {sent.Units} of the {Of(d.Units, d.Good)} you owe {b.Name} this year: it was sent, and paid for."
                                : $"{s.Name} sent {sent.Units} of the {Of(d.Units, d.Good)} it owes you this year: it had no more. You paid for what came.")
                        + $" One more such year ends the deal, and the head of the other house thinks {Signed(-DefaultRegard)} of the one at fault.", "houses", other.Id);
            }
            if (w.Turn >= d.Until && w.TradeDeals.Contains(d))
            {
                w.TradeDeals.Remove(d); Count(w, KeptKey); Remember(w, KeptKind, b.Id, s.Id, YearCost(d.UnitCredits, d.Units), d.Good, d.Delivered);
                Think(w, s, b, KeptRegard); Think(w, b, s, KeptRegard);
                if (s.Id == mine || b.Id == mine)
                    w.Tell("good", $"The standing deal in {label} with {other.Name} has run its term: {d.Delivered} were sent in all{(d.Short > 0 ? $", {d.Short} short of what was promised" : "")}. Its head thinks {Signed(KeptRegard)} of you for a word kept.", "houses", other.Id);
            }
        }
    }

    /// <summary>A deal has failed <see cref="FailYears"/> years running: it is at an end, and the house at fault has lost the other's regard.</summary>
    private static void Default(World w, TradeDeal d, House at, House wronged)
    {
        w.TradeDeals.Remove(d); Think(w, wronged, at, -DefaultRegard); Count(w, DefaultedKey); Remember(w, DefaultKind, at.Id, wronged.Id, YearCost(d.UnitCredits, d.Units), d.Good, d.Short);
        string? mine = w.Protagonist?.HouseId; string label = Resource.Label(d.Good); bool buyer = d.BuyerId == at.Id;
        if (at.Id == mine || wronged.Id == mine) w.Log($"The standing deal in {label} between {w.House(d.SellerId)?.Name} and {w.House(d.BuyerId)?.Name} has failed {Counted.Years(FailYears)} running, and is at an end.");
        if (wronged.Id == mine)
            w.Tell("warn", $"{at.Name} has {(buyer ? "not paid for" : "not sent")} the {label} of your standing deal {Counted.Years(FailYears)} running. The deal is at an end.", "houses", at.Id);
        else if (at.Id == mine)
            w.Tell("warn", $"You have {(buyer ? "not paid for" : "not sent")} the {label} of your standing deal with {wronged.Name} {Counted.Years(FailYears)} running. The deal is at an end: its head thinks {Signed(-DefaultRegard)} of you, and the house strikes no deal with yours for {Counted.Years(MemoryYears)}.", "houses", wronged.Id);
    }

    // ---------------------------------------------------------------- what lasts

    /// <summary>What has passed in trade between two houses and is remembered, as the first sees it (the lines join the
    /// list of dealings on the other house's page).</summary>
    public static List<(string text, string tone)> Remembered(World w, House mine, House theirs)
    {
        var list = new List<(string, string)>();
        bool Ours(DealingRecord d) => d.FromHouseId == mine.Id && d.ToHouseId == theirs.Id || d.FromHouseId == theirs.Id && d.ToHouseId == mine.Id;
        foreach (var d in w.Dealings.Where(d => d.Kind is BoughtKind or StruckKind or KeptKind or BrokenKind or DefaultKind && Ours(d)).OrderByDescending(d => d.Turn).Take(6))
        {
            bool me = d.FromHouseId == mine.Id; string label = Resource.Label(d.Good);
            list.Add(d.Kind switch
            {
                BoughtKind => (me ? $"You bought {Of(d.Units, d.Good)} out of its store in year {d.Turn}, for {Money.Credits(d.Amount)}." : $"It bought {Of(d.Units, d.Good)} out of your store in year {d.Turn}, for {Money.Credits(d.Amount)}.", ""),
                StruckKind => ($"A standing deal in {label} was struck in year {d.Turn}: {d.Units} a year {(me ? "to you" : "to it")}, {Money.Credits(d.Amount)} a year.", ""),
                KeptKind => ($"A standing deal in {label} ran its term in year {d.Turn}: {d.Units} were sent in all.", "good"),
                BrokenKind => (me ? $"You ended a standing deal in {label} early in year {d.Turn}: it strikes none with you before year {d.Turn + MemoryYears}." : $"It ended a standing deal in {label} early in year {d.Turn}.", "warn"),
                _ => (me ? $"You failed it {Counted.Years(FailYears)} running under a standing deal in {label}, which ended in year {d.Turn}: it strikes none with you before year {d.Turn + MemoryYears}." : $"It failed you {Counted.Years(FailYears)} running under a standing deal in {label}, which ended in year {d.Turn}.", "warn"),
            });
        }
        return list;
    }

    // ---------------------------------------------------------------- the year: the other houses

    public void Tick(World w)
    {
        foreach (var key in w.WorldCounters.Keys.Where(k => k.StartsWith("house_sold:", StringComparison.Ordinal) || k.StartsWith("traded:", StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal).ToList())
        {
            if (key.StartsWith("traded:", StringComparison.Ordinal)) { if (w.Counter(key) < w.Turn + 1) w.WorldCounters.Remove(key); continue; }
            var parts = key.Split(':'); if (parts.Length > 1 && int.TryParse(parts[1], out int turn) && turn < w.Turn) w.WorldCounters.Remove(key);
        }
        if (_trade.Market.IsEmpty || w.Market == null) return;
        string? mine = w.Protagonist?.HouseId;
        _memo = new Dictionary<string, Dictionary<string, (int made, int burn)>>();
        try
        {
            var houses = w.Houses.Values.Where(h => !World.Outsider(h.Id) && Economy.HeadOf(w, h)?.Alive == true).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
            OthersEnd(w, mine);
            OthersBuy(w, houses, mine);
            Offers(w, houses, mine);
        }
        finally { _memo = null; }
    }

    /// <summary>A house other than the player's that can no longer send what it promised without cutting into what it burns
    /// ends the last deal it struck, and bears what that costs.</summary>
    private void OthersEnd(World w, string? mine)
    {
        foreach (var d in w.TradeDeals.Where(d => d.SellerId != mine).OrderByDescending(d => d.Seq).ToList())
        {
            var s = w.House(d.SellerId); if (s == null || w.House(d.BuyerId) == null || Ended(w, d) || Held(w, d)) continue;
            var f = Flow(w, s, d.Good);
            if (s.Treasury.Get(d.Good) + f.made - f.burn < Promised(w, s.Id, d.Good)) End(w, d, s);
        }
    }

    /// <summary>One house in <see cref="OthersTradeOneIn"/> each year looks to what it burns. With less than a year of a
    /// good in store it buys a lot of the friendliest house that will spare it; burning more than it brings up, it strikes
    /// a standing deal with the friendliest house that brings up more than it burns. It never helps itself to the player's
    /// store: the player is asked (<see cref="Offers"/>).</summary>
    private void OthersBuy(World w, List<House> houses, string? mine)
    {
        foreach (var b in houses.Where(h => h.Id != mine && Hash($"{h.Id}:{w.Turn}:trade") / 7 % OthersTradeOneIn == 0))
            foreach (var g in Goods)
            {
                var f = Flow(w, b, g.Id); int held = b.Treasury.Get(g.Id); if (f.burn <= 0) continue;
                var others = houses.Where(s => s.Id != b.Id && s.Id != mine && Trades(w, s, b)).OrderByDescending(s => DealingsSystem.RegardOf(w, s, b)).ThenBy(s => s.Id, StringComparer.Ordinal).ToList();
                if (held < f.burn)
                {
                    var seller = others.FirstOrDefault(s => Spare(w, s, g.Id) >= 1); if (seller == null) continue;
                    int units = Math.Min(Math.Min(Wants(w, b, g.Id), Spare(w, seller, g.Id)), OthersLotMost);
                    while (units >= 1 && LotCost(w, seller, b, g.Id, units) > b.Treasury.Credits - CoinKept) units--;
                    if (units < 1) continue;
                    Buy(w, b, seller, g.Id, units); break;
                }
                int need = Need(w, b, g.Id); if (need < OthersDealLeast || Coming(w, b.Id, g.Id) > 0) continue;
                var maker = others.FirstOrDefault(s => Surplus(w, s, g.Id) >= OthersDealLeast && !Between(w, s.Id, b.Id).Any()); if (maker == null) continue;
                int a = Math.Min(Math.Min(need, Surplus(w, maker, g.Id)), OthersDealMost), unit = Math.Max(1, (int)Math.Round(Asks(w, maker, b, g.Id) * Money.PerUnit)), yearly = YearCost(unit, a);
                if (yearly * IncomePart > Income(w, b) || b.Treasury.Credits - yearly < CoinKept) continue;
                Strike(w, maker, b, g.Id, a, Terms[0], unit); break;
            }
    }

    /// <summary>Now and then a house whose head thinks no ill of the player sends word: it is short of a good the player has
    /// to spare, or has to spare a good the player is short of. One such word in <see cref="OfferEveryYears"/> years at the
    /// most. The player acts on it, or not, on that house's page.</summary>
    private void Offers(World w, List<House> houses, string? mine)
    {
        var me = mine != null ? w.House(mine) : null; if (me == null || Economy.HeadOf(w, me)?.Alive != true) return;
        if (w.WorldCounters.TryGetValue(OfferedKey, out int last) && w.Turn - last < OfferEveryYears) return;
        foreach (var h in houses.Where(h => h.Id != me.Id && WontTrade(w, me, h) == null && DealingsSystem.RegardOf(w, h, me) >= 0 && Fault(w, me.Id, h.Id) == null)
                     .OrderByDescending(h => DealingsSystem.RegardOf(w, h, me)).ThenBy(h => h.Id, StringComparer.Ordinal))
        {
            if (Hash($"{h.Id}:{w.Turn}:offer") / 7 % OffersOneIn != 0) continue;
            string head = Economy.HeadOf(w, h)?.Name ?? "The head";
            foreach (var g in Goods)
            {
                string label = Resource.Label(g.Id); var theirs = Flow(w, h, g.Id); var ours = Flow(w, me, g.Id);
                int take = Math.Min(Wants(w, h, g.Id), Spare(w, me, g.Id)), give = Math.Min(Spare(w, h, g.Id), Wants(w, me, g.Id));
                string? word = null;
                if (theirs.burn > 0 && h.Treasury.Get(g.Id) < theirs.burn && take >= OfferLeast && h.Treasury.Credits - LotFetches(w, h, me, g.Id, take) >= CoinKept)
                    word = $"{head} of {h.Name} is short of {label}: the house burns {theirs.burn} a year and holds {h.Treasury.Get(g.Id)}. It would buy up to {take} out of your store, at {Unit(Pays(w, h, me, g.Id))} a unit. It is done on its page, under Trade.";
                else if (ours.burn > 0 && me.Treasury.Get(g.Id) < ours.burn && give >= OfferLeast)
                    word = $"{head} of {h.Name} has {label} to spare, and your house burns {ours.burn} a year and holds {me.Treasury.Get(g.Id)}. It would sell you up to {give} out of its store, at {Unit(Asks(w, h, me, g.Id))} a unit before the lot's weight. It is done on its page, under Trade.";
                if (word == null) continue;
                w.WorldCounters[OfferedKey] = w.Turn; Count(w, OffersKey); w.Tell("info", word, "houses", h.Id);
                return;
            }
        }
    }
}
