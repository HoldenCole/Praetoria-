using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Trade between houses (docs/WEALTH.md, "Trade between houses"): a lot bought out of another house's store or
/// sold out of one's own, a standing deal and its yearly delivery, what the other houses do with it, and a corner bought
/// out of the houses' stores.</summary>
public class TradeBetweenHousesTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private const string Mat = Resource.Materials, He = Resource.Helium3, Exo = Resource.Exotics, Iso = Resource.Isotopes;
    private sealed record Game(TurnController Tc, World W, House Mine, Character Me, CommandContext Ctx)
    {
        public HouseTradeSystem T => Tc.HouseTrade;
        public House H(string id) => W.House(id)!;
        public Character HeadOf(House h) => Economy.HeadOf(W, h)!;
        public IEnumerable<House> Others => W.Houses.Values.Where(h => !World.Outsider(h.Id) && h.Id != Mine.Id && Economy.HeadOf(W, h)?.Alive == true).OrderBy(h => h.Id, StringComparer.Ordinal);
        public HouseTradeCommand Cmd(string what, House theirs, string good = Mat, int units = 5, int years = 5, string dealId = "") => new(Me.Id, what, theirs.Id, T, good, units, years, dealId);
        public string? No(string what, House theirs, string good = Mat, int units = 5, int years = 5, string dealId = "") => Cmd(what, theirs, good, units, years, dealId).Refusal(Ctx);
        public void Do(string what, House theirs, string good = Mat, int units = 5, int years = 5, string dealId = "") { var c = Cmd(what, theirs, good, units, years, dealId); Assert.True(Tc.Executor.TryExecute(c, Ctx), c.Refusal(Ctx)); }
        /// <summary>What the head of that house thinks of the player.</summary>
        public int Regard(House h) => DealingsSystem.RegardOf(W, h, Mine);
        public void SetRegard(House h, int n) => W.Relationship(HeadOf(h).Id, Me.Id).Disposition = n;
        public void SetRegard(House of, House toward, int n) => W.Relationship(HeadOf(of).Id, HeadOf(toward).Id).Disposition = n;
        public bool Told(string part) => W.News.Any(n => n.Text.Contains(part));
        public int Remembered(string kind) => W.Dealings.Count(d => d.Kind == kind);
        public GoodState Good(string id) => MarketSystem.Of(W, Content.Market).Goods[id];
        /// <summary>The year's books and nothing else of the year: the year turns and every house's ledger is paid.</summary>
        public void Books() { W.Turn++; Tc.Economy!.Accrue(W); }
        /// <summary>A house that brings up a good, or burns it, as its ledger stands.</summary>
        public House Maker(string good, int least) => Others.First(h => T.Surplus(W, h, good) >= least);
        public House Burner(string good, int least = 1) => Others.First(h => T.Flow(W, h, good).burn >= least);
        public House Idle(string good) => Others.First(h => T.Flow(W, h, good) == (0, 0));
    }
    private static Game Start(ulong seed = 5, int credits = 100, string scenario = "the_old_lion")
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), seed); var tc = new TurnController(w, Content);
        var me = w.Protagonist!; var mine = w.House(me.HouseId)!; mine.Treasury.Credits = credits; w.Turn = 3;
        return new Game(tc, w, mine, me, new CommandContext(w, WorldBuilder.RngFor(w)));
    }
    private static void Year(TurnController tc) { foreach (var i in tc.BeginTurn()) { var ch = tc.Offer(i).FirstOrDefault(x => x.Available); if (ch != null) tc.Resolve(i, ch.Choice.Id); } tc.EndTurn(); }
    private static int Ceil(double x) => Math.Max(1, (int)Math.Ceiling(x - 1e-9));

    [Fact]
    public void Only_the_head_trades_and_only_in_goods_with_a_house_of_the_Reach()
    {
        var g = Start(); var kin = g.W.LivingCharacters().First(c => c.HouseId == g.Mine.Id && c.Id != g.Me.Id); var theirs = g.Others.First();
        if (g.W.House(PirateSystem.HouseId) == null) g.W.Houses[PirateSystem.HouseId] = new House { Id = PirateSystem.HouseId, Name = "the raiders" };
        foreach (var what in HouseTradeCommand.All.Where(x => x != HouseTradeCommand.End))
        {
            Assert.Equal("only the head of a house deals with another in its name", new HouseTradeCommand(kin.Id, what, theirs.Id, g.T, Mat, 5, 5).Refusal(g.Ctx));
            Assert.Equal("no such house", g.No(what, g.Mine)); Assert.Equal("no such house", new HouseTradeCommand(g.Me.Id, what, "nobody", g.T, Mat, 5, 5).Refusal(g.Ctx));
            Assert.Contains("not a house of the Reach", g.No(what, g.H(PirateSystem.HouseId)));
            Assert.StartsWith("men are not sold between houses", g.No(what, theirs, Resource.Manpower)); Assert.Equal("not a thing one house sells another", g.No(what, theirs, Resource.Influence));
            Assert.False(g.Tc.Executor.TryExecute(g.Cmd(what, theirs, Resource.Manpower), g.Ctx));
        }
        Assert.Equal("nothing asked for", g.No(HouseTradeCommand.Buy, theirs, Mat, 0)); Assert.Equal("nothing offered", g.No(HouseTradeCommand.Sell, theirs, Mat, 0));
        Assert.Equal("a lot is of 999 at the most", g.No(HouseTradeCommand.Buy, theirs, Mat, HouseTradeSystem.LotMost + 1)); Assert.Equal("a lot is of 999 at the most", g.No(HouseTradeCommand.Sell, theirs, Mat, 1000));
        Assert.Equal("a standing deal is for 1 to 25 units a year", g.No(HouseTradeCommand.DealBuy, theirs, Mat, 26)); Assert.Equal("a standing deal is for 1 to 25 units a year", g.No(HouseTradeCommand.DealSell, theirs, Mat, 0));
        Assert.Equal("not a term that is offered", g.No(HouseTradeCommand.DealBuy, theirs, Mat, 5, 7));
        Assert.Equal("no such trade", g.No("barter", theirs));
        Assert.Equal("1 unit of materials", HouseTradeSystem.Of(1, Mat)); Assert.Equal("5 rare earths", HouseTradeSystem.Of(5, Resource.RareEarths));
        Assert.Equal("no such deal of your house's", g.No(HouseTradeCommand.End, theirs, dealId: "deal99"));
        Assert.Equal("only the head of a house deals with another in its name", new HouseTradeCommand(kin.Id, HouseTradeCommand.End, theirs.Id, g.T, dealId: "deal1").Refusal(g.Ctx));
        // the goods are the ones the exchanges will take: not manpower
        Assert.Equal(new[] { Mat, Exo, He, Resource.RareEarths, Iso }, g.T.Goods.Select(x => x.Id).ToArray());
    }

    [Fact]
    public void A_house_sells_what_it_holds_above_what_it_keeps_by()
    {
        var g = Start(); var idle = g.Idle(Mat); g.SetRegard(idle, 0);
        // a house that burns none keeps the floor
        idle.Treasury.Materials = 30;
        Assert.Equal(HouseTradeSystem.Floor(Mat), g.T.Keeps(g.W, idle, Mat)); Assert.Equal(8, HouseTradeSystem.Floor(Mat)); Assert.Equal(22, g.T.Spare(g.W, idle, Mat)); Assert.Equal(0, g.T.Wants(g.W, idle, Mat));
        Assert.Equal($"{idle.Name} would spare only 22: it holds 30 and keeps 8 by: 8 whatever comes", g.No(HouseTradeCommand.Buy, idle, Mat, 23));
        Assert.Null(g.No(HouseTradeCommand.Buy, idle, Mat, 22));
        idle.Treasury.Materials = 8;
        Assert.Equal($"{idle.Name} has no materials to spare: it holds 8 and keeps 8 by: 8 whatever comes", g.No(HouseTradeCommand.Buy, idle, Mat, 1));
        // one that burns a good keeps two years of what it burns besides
        var burner = g.Burner(He); int burn = g.T.Flow(g.W, burner, He).burn; g.SetRegard(burner, 0); g.Mine.Treasury.Credits = 5000;
        Assert.Equal(4 + HouseTradeSystem.KeepYears * burn, g.T.Keeps(g.W, burner, He));
        burner.Treasury.Helium3 = 4 + 2 * burn + 3;
        Assert.Equal(3, g.T.Spare(g.W, burner, He));
        Assert.Equal($"{burner.Name} would spare only 3: it holds {burner.Treasury.Helium3} and keeps {4 + 2 * burn} by: 4 whatever comes, 2 years of the {burn} it burns a year", g.No(HouseTradeCommand.Buy, burner, He, 4));
        // and what it has promised under a standing deal this year
        HouseTradeSystem.Strike(g.W, burner, g.Others.First(h => h.Id != burner.Id), He, 2, 5, 600);
        Assert.Equal(1, g.T.Spare(g.W, burner, He));
        Assert.EndsWith($", and the 2 it has promised this year", g.No(HouseTradeCommand.Buy, burner, He, 3));
        Assert.Null(g.No(HouseTradeCommand.Buy, burner, He, 1));
        // the treasury must cover it
        g.Mine.Treasury.Credits = 0;
        Assert.Equal($"needs {Money.Credits(g.T.LotCost(g.W, burner, g.Mine, He, 1))} and the house has 0", g.No(HouseTradeCommand.Buy, burner, He, 1));
    }

    [Fact]
    public void A_lot_bought_changes_hands_at_once_and_is_remembered()
    {
        var g = Start(credits: 200); var theirs = g.Idle(Mat); g.SetRegard(theirs, 0); theirs.Treasury.Materials = 48; g.Mine.Treasury.Materials = 3; int theirCoin = theirs.Treasury.Credits;
        var cmd = g.Cmd(HouseTradeCommand.Buy, theirs, Mat, 10); var q = cmd.Quote(g.W); double mid = g.T.Mid(g.W, Mat);
        Assert.Null(q.Refusal); Assert.Equal(Ceil(mid * (10 + 0.6 * 50 / 40)), q.Credits); Assert.Equal(q.Credits / 10.0, q.UnitPrice, 6);
        Assert.Contains($"10 materials out of the store of {theirs.Name} for {Money.Credits(q.Credits)}", g.T.BuySays(g.W, g.Mine, theirs, Mat, 10));
        Assert.Contains("no caravaneer stands between you", g.T.BuySays(g.W, g.Mine, theirs, Mat, 10)); Assert.Contains("It would spare 40", g.T.BuySays(g.W, g.Mine, theirs, Mat, 10));
        int all = CornerSystem.Standing(g.W, Content.Market, Mat).All;
        g.Do(HouseTradeCommand.Buy, theirs, Mat, 10);
        Assert.Equal(13, g.Mine.Treasury.Materials); Assert.Equal(38, theirs.Treasury.Materials); Assert.Equal(200 - q.Credits, g.Mine.Treasury.Credits); Assert.Equal(theirCoin + q.Credits, theirs.Treasury.Credits);
        Assert.Equal(all, CornerSystem.Standing(g.W, Content.Market, Mat).All);   // goods moved from one store to another: none made, none used up
        Assert.Equal(1, g.Regard(theirs));   // a fair bargain
        var rec = g.W.Dealings.Single(d => d.Kind == HouseTradeSystem.BoughtKind); Assert.Equal((g.Mine.Id, theirs.Id, Mat, 10, q.Credits, 3), (rec.FromHouseId, rec.ToHouseId, rec.Good, rec.Units, rec.Amount, rec.Turn));
        Assert.Contains(g.W.History, e => e.Text == $"{g.Mine.Name} buys 10 materials out of the store of {theirs.Name} for {Money.Credits(q.Credits)}.");
        Assert.Contains(g.Tc.Dealings.Lasting(g.W, g.Mine, theirs), l => l.text == $"You bought 10 materials out of its store in year 3, for {Money.Credits(q.Credits)}.");
        // a second lot the same year is added to the first in the record, and moves regard no further
        int second = g.T.LotCost(g.W, theirs, g.Mine, Mat, 5); g.Do(HouseTradeCommand.Buy, theirs, Mat, 5);
        Assert.Equal(1, g.Regard(theirs)); Assert.Equal(1, g.Remembered(HouseTradeSystem.BoughtKind)); Assert.Equal((15, q.Credits + second), (rec.Units, rec.Amount));
        g.W.Turn++; g.Do(HouseTradeCommand.Buy, theirs, Mat, 1); Assert.Equal(2, g.Regard(theirs)); Assert.Equal(2, g.Remembered(HouseTradeSystem.BoughtKind));
        Assert.Equal(3, g.W.Counter(HouseTradeSystem.TradesKey));
    }

    [Fact]
    public void The_price_is_the_markets_moved_by_regard_and_a_large_lot_costs_more_a_unit()
    {
        var g = Start(credits: 5000); var theirs = g.Idle(Mat); theirs.Treasury.Materials = 48; double mid = g.T.Mid(g.W, Mat);
        Assert.Equal(g.Good(Mat).Price, mid);
        // no spread and no tariff: a house that thinks nothing of you asks the market's price, which is less than the exchanges charge
        g.SetRegard(theirs, 0); Assert.Equal(mid, g.T.Asks(g.W, theirs, g.Mine, Mat), 9); Assert.True(g.T.Asks(g.W, theirs, g.Mine, Mat) < MarketSystem.BuyPrice(g.W, Content.Trade, g.Mine.Id, Mat));
        Assert.Equal(mid * 0.90, g.T.Pays(g.W, theirs, g.Mine, Mat), 9);
        // dearer to one it dislikes, a point in the hundred for a point, forty at the most; a little cheaper to a friend
        Assert.Equal(new[] { 0, 0, -1, -5, -5, 1, 20, 40, 40 }, new[] { 0, 9, 10, 50, 100, -1, -20, -40, -90 }.Select(HouseTradeSystem.RegardPct).ToArray());
        g.SetRegard(theirs, -20); Assert.Equal(mid * 1.20, g.T.Asks(g.W, theirs, g.Mine, Mat), 9); Assert.Equal(mid * 0.70, g.T.Pays(g.W, theirs, g.Mine, Mat), 9);
        Assert.Contains("Its head thinks -20 of you, and 20 in the hundred dearer to you.", g.T.PriceSays(g.W, theirs, g.Mine, Mat, selling: true));
        g.SetRegard(theirs, 50); Assert.Equal(mid * 0.95, g.T.Asks(g.W, theirs, g.Mine, Mat), 9); Assert.Equal(mid * 0.95, g.T.Pays(g.W, theirs, g.Mine, Mat), 9);
        Assert.Contains("Its head thinks +50 of you, and 5 in the hundred cheaper to you.", g.T.PriceSays(g.W, theirs, g.Mine, Mat, selling: true));
        // a friend's best price to buy at is no better than its best price to sell at: nothing is made by passing goods round
        Assert.True(g.T.Pays(g.W, theirs, g.Mine, Mat) <= g.T.Asks(g.W, theirs, g.Mine, Mat));
        // the lot: the whole of what it would spare costs three-tenths more a unit than the first of it
        g.SetRegard(theirs, 0); Assert.Equal(40, g.T.Spare(g.W, theirs, Mat));
        Assert.Equal(Ceil(mid * 40 * 1.3), g.T.LotCost(g.W, theirs, g.Mine, Mat, 40)); Assert.Equal(Ceil(mid * (4 + 0.6 * 8 / 40)), g.T.LotCost(g.W, theirs, g.Mine, Mat, 4));
        Assert.True(g.T.LotCost(g.W, theirs, g.Mine, Mat, 40) / 40.0 > g.T.LotCost(g.W, theirs, g.Mine, Mat, 4) / 4.0);
        // and splitting it does not help: four lots of ten cost what forty cost, but for the rounding of each
        int whole = g.T.LotCost(g.W, theirs, g.Mine, Mat, 40), parts = 0;
        for (int i = 0; i < 4; i++) { parts += g.T.LotCost(g.W, theirs, g.Mine, Mat, 10); g.Do(HouseTradeCommand.Buy, theirs, Mat, 10); }
        Assert.InRange(parts, whole, whole + 3); Assert.Equal(40, g.W.Counter(HouseTradeSystem.SoldKey(g.W.Turn, theirs.Id, Mat)));
        Assert.StartsWith($"{theirs.Name} has no materials to spare", g.No(HouseTradeCommand.Buy, theirs, Mat, 1));
    }

    [Fact]
    public void Isotopes_the_law_keeps_off_the_exchanges_are_sold_between_houses_at_a_premium()
    {
        var g = Start(credits: 5000); var theirs = g.Idle(Iso); g.SetRegard(theirs, 0); theirs.Treasury.Isotopes = 10;
        Assert.NotNull(new BuyGoodCommand(g.Me.Id, Iso, 1, Content.Trade).Refusal(g.Ctx)); Assert.True(g.T.Closed(g.W, Iso)); Assert.False(g.T.Closed(g.W, Exo));
        Assert.Equal(g.T.Mid(g.W, Iso) * 1.5, g.T.Asks(g.W, theirs, g.Mine, Iso), 9); Assert.Equal(g.T.Mid(g.W, Iso) * 1.5 * 0.9, g.T.Pays(g.W, theirs, g.Mine, Iso), 9);
        Assert.Contains("The law keeps isotopes off the exchanges, and a private sale is 50 in the hundred dearer for it.", g.T.BuySays(g.W, g.Mine, theirs, Iso, 2));
        g.Do(HouseTradeCommand.Buy, theirs, Iso, 2); Assert.Equal(2, g.Mine.Treasury.Isotopes); Assert.Equal(8, theirs.Treasury.Isotopes);
    }

    [Fact]
    public void No_house_trades_across_a_war_blood_ill_regard_or_the_Senates_embargo()
    {
        var acts = new[] { HouseTradeCommand.Buy, HouseTradeCommand.Sell, HouseTradeCommand.DealBuy, HouseTradeCommand.DealSell };
        Game Ready(out House theirs) { var g = Start(credits: 500); theirs = g.Idle(Mat); theirs.Treasury.Materials = 40; g.Mine.Treasury.Materials = 40; g.SetRegard(theirs, 0); return g; }

        var g = Ready(out var theirs); string head = g.HeadOf(theirs).Name;
        Assert.Null(g.No(HouseTradeCommand.Buy, theirs)); Assert.Null(HouseTradeSystem.WontTrade(g.W, g.Mine, theirs));
        g.SetRegard(theirs, HouseTradeSystem.RefuseAt);
        foreach (var what in acts) Assert.Equal($"{head} thinks -40 of you, and does not trade with a house it thinks -40 or worse of", g.No(what, theirs));
        g.SetRegard(theirs, HouseTradeSystem.RefuseAt + 1); Assert.Null(g.No(HouseTradeCommand.Buy, theirs));
        Assert.False(HouseTradeSystem.Trades(g.W, g.Mine, theirs) && g.Regard(theirs) <= HouseTradeSystem.RefuseAt);

        g.W.Wars.Add(new War { HouseA = g.Mine.Id, HouseB = theirs.Id, Since = g.W.Turn });
        foreach (var what in acts) { Assert.Equal("you are at war with them", g.No(what, theirs)); Assert.False(g.Tc.Executor.TryExecute(g.Cmd(what, theirs), g.Ctx)); }
        Assert.Equal(40, g.Mine.Treasury.Materials); Assert.False(HouseTradeSystem.Trades(g.W, g.Mine, theirs));

        g = Ready(out theirs);
        g.W.Feuds.Add(new BloodFeud { Id = "feud_test", WrongedId = theirs.Id, OwingId = g.Mine.Id, Since = g.W.Turn });
        foreach (var what in acts) Assert.Equal("there is blood between your houses, and nothing is bought or sold across it", g.No(what, theirs));
        Assert.False(HouseTradeSystem.Trades(g.W, theirs, g.Mine));

        // the embargo closes the exchanges, and the houses keep it too: whichever of the two it is on
        g = Ready(out theirs); g.W.WorldCounters["embargo:" + g.Mine.Id] = g.W.Turn + 4; Assert.True(SenateSystem.Embargoed(g.W, g.Mine.Id));
        foreach (var what in acts) Assert.Equal("the Senate's embargo is on your house: no house of the Reach buys of it or sells to it until it lifts", g.No(what, theirs));
        g.W.WorldCounters.Remove("embargo:" + g.Mine.Id); g.W.WorldCounters["embargo:" + theirs.Id] = g.W.Turn + 4;
        foreach (var what in acts) Assert.Equal($"the Senate's embargo is on {theirs.Name}: no house of the Reach buys of it or sells to it until it lifts", g.No(what, theirs));
        g.W.Turn += 4; Assert.Null(g.No(HouseTradeCommand.Buy, theirs));
    }

    [Fact]
    public void A_house_buys_a_lot_it_has_a_use_for_when_it_has_the_coin()
    {
        var g = Start(); var theirs = g.Idle(Mat); g.SetRegard(theirs, 0); theirs.Treasury.Materials = 2; theirs.Treasury.Credits = 60; g.Mine.Treasury.Materials = 4;
        Assert.Equal(6, g.T.Wants(g.W, theirs, Mat));
        Assert.Equal("you hold only 4", g.No(HouseTradeCommand.Sell, theirs, Mat, 5));
        g.Mine.Treasury.Materials = 30;
        Assert.Equal($"{theirs.Name} would take only 6: it holds 2, and buys up to 8: it burns none", g.No(HouseTradeCommand.Sell, theirs, Mat, 7));
        int fetch = (int)Math.Floor(g.T.Mid(g.W, Mat) * 0.9 * 5 + 1e-9); var q = g.Cmd(HouseTradeCommand.Sell, theirs, Mat, 5).Quote(g.W);
        Assert.Null(q.Refusal); Assert.Equal(fetch, q.Credits); Assert.True(fetch >= 1);
        Assert.Contains($"5 materials out of your store to {theirs.Name} for {Money.Credits(fetch)}", g.T.SellSays(g.W, g.Mine, theirs, Mat, 5)); Assert.Contains("It pays 10 in the hundred under the market", g.T.SellSays(g.W, g.Mine, theirs, Mat, 5));
        theirs.Treasury.Credits = HouseTradeSystem.CoinKept + fetch - 1;
        Assert.Equal($"{theirs.Name} has not the coin: it holds {Money.Show(theirs.Treasury.Credits)}, this is {Money.Show(fetch)}, and it keeps 1,000 by", g.No(HouseTradeCommand.Sell, theirs, Mat, 5));
        theirs.Treasury.Credits = 60;
        g.Do(HouseTradeCommand.Sell, theirs, Mat, 5);
        Assert.Equal(25, g.Mine.Treasury.Materials); Assert.Equal(7, theirs.Treasury.Materials); Assert.Equal(100 + fetch, g.Mine.Treasury.Credits); Assert.Equal(60 - fetch, theirs.Treasury.Credits); Assert.Equal(1, g.Regard(theirs));
        var rec = g.W.Dealings.Single(d => d.Kind == HouseTradeSystem.BoughtKind); Assert.Equal((theirs.Id, g.Mine.Id, Mat, 5, fetch), (rec.FromHouseId, rec.ToHouseId, rec.Good, rec.Units, rec.Amount));
        Assert.Contains(g.Tc.Dealings.Lasting(g.W, g.Mine, theirs), l => l.text == $"It bought 5 materials out of your store in year 3, for {Money.Credits(fetch)}.");
        // filled, it has no use for more
        g.Do(HouseTradeCommand.Sell, theirs, Mat, 1);
        Assert.Equal($"{theirs.Name} has no use for more materials: it holds 8, and buys up to 8: it burns none", g.No(HouseTradeCommand.Sell, theirs, Mat, 1));
        // a house that burns a good wants two years of it besides
        var burner = g.Burner(He); int burn = g.T.Flow(g.W, burner, He).burn; burner.Treasury.Helium3 = 0; Assert.Equal(4 + 2 * burn, g.T.Wants(g.W, burner, He));
    }

    [Fact]
    public void A_standing_deal_sends_the_goods_each_year_at_the_price_fixed_and_runs_its_term()
    {
        var g = Start(credits: 1000); var maker = g.Maker(Mat, 5); g.SetRegard(maker, 0); int can = g.T.Surplus(g.W, maker, Mat); var f = g.T.Flow(g.W, maker, Mat); maker.Treasury.Materials = 100;
        // it promises no more a year than it brings up beyond what it burns
        if (can < HouseTradeSystem.MostAYear) Assert.Equal($"{maker.Name} would promise only {can} a year: it brings up {f.made} a year and burns {f.burn}", g.No(HouseTradeCommand.DealBuy, maker, Mat, can + 1));
        var idle = g.Idle(Mat); g.SetRegard(idle, 0);
        Assert.Equal($"{idle.Name} can promise no materials every year: it brings up 0 a year and burns 0. What it will spare of its store is bought by the lot", g.No(HouseTradeCommand.DealBuy, idle, Mat, 1));
        Assert.Null(g.No(HouseTradeCommand.DealBuy, maker, Mat, 5, 5)); Assert.Null(g.No(HouseTradeCommand.DealBuy, maker, Mat, 5, 10));
        int unit = g.T.DealUnitCredits(g.W, g.Mine, maker, Mat, selling: false), yearly = HouseTradeSystem.YearCost(unit, 5);
        Assert.Equal((int)Math.Round(g.T.Mid(g.W, Mat) * 100), unit);
        string says = g.T.DealSays(g.W, g.Mine, maker, Mat, 5, 5, selling: false);
        Assert.StartsWith($"{maker.Name} sends you 5 materials every year for 5 years, from next year, at {Money.Show(unit / 100.0)} a unit: {Money.Credits(yearly)} a year out of your treasury", says);
        Assert.Contains("The price is fixed for the term", says); Assert.Contains("2 years of that running end the deal", says); Assert.Contains("thinks -15 of the one that did", says);

        g.Do(HouseTradeCommand.DealBuy, maker, Mat, 5, 5);
        var deal = g.W.TradeDeals.Single(); Assert.Equal((maker.Id, g.Mine.Id, Mat, 5, unit, 3, 8), (deal.SellerId, deal.BuyerId, deal.Good, deal.Units, deal.UnitCredits, deal.Since, deal.Until));
        Assert.Equal(1, g.Regard(maker)); Assert.Equal(1, g.Remembered(HouseTradeSystem.StruckKind)); Assert.Equal(1, g.W.Counter(HouseTradeSystem.StruckKey));
        Assert.Equal($"a deal in materials stands between your houses already", g.No(HouseTradeCommand.DealBuy, maker, Mat, 1)); Assert.Equal($"a deal in materials stands between your houses already", g.No(HouseTradeCommand.DealSell, maker, Mat, 1));
        Assert.Equal(can - 5, g.T.Surplus(g.W, maker, Mat)); Assert.Equal(5, HouseTradeSystem.Coming(g.W, g.Mine.Id, Mat));
        Assert.Equal($"{maker.Name} sends you 5 materials a year at {Money.Show(unit / 100.0)} a unit: {Money.Credits(yearly)} a year from you, until year 8.", HouseTradeSystem.Describe(g.W, deal, g.Mine.Id));
        Assert.Contains(g.Tc.Dealings.Lasting(g.W, g.Mine, maker), l => l.text == $"A standing deal in materials was struck in year 3: 5 a year to you, {Money.Credits(yearly)} a year.");

        // it is in both ledgers, and the forecast: the goods one way, the credits the other
        var mineLine = g.Tc.Economy!.Compute(g.W, g.Mine.Id).Lines.Single(l => l.Kind == HouseTradeSystem.LedgerKind);
        Assert.Equal(($"materials bought of {maker.Name}", 5, -yearly), (mineLine.Source, mineLine.Delta.Materials, mineLine.Delta.Credits));
        var theirLine = g.Tc.Economy.Compute(g.W, maker.Id).Lines.Single(l => l.Kind == HouseTradeSystem.LedgerKind);
        Assert.Equal(($"materials sold to {g.Mine.Name}", -5, yearly), (theirLine.Source, theirLine.Delta.Materials, theirLine.Delta.Credits));
        // what a deal moves is not what a house makes or burns
        Assert.Equal(f, g.T.Flow(g.W, maker, Mat));

        // each year, when the books are done
        for (int year = 1; year <= 5; year++)
        {
            var net = g.Tc.Economy.Forecast(g.W, g.Mine.Id); int had = g.Mine.Treasury.Materials, coin = g.Mine.Treasury.Credits;
            g.Books();
            Assert.Equal(had + net.Materials, g.Mine.Treasury.Materials); Assert.Equal(coin + net.Credits, g.Mine.Treasury.Credits); Assert.Equal(5 * year, deal.Delivered); Assert.Equal(0, deal.Failed);
            Assert.Equal(year < 5, g.W.TradeDeals.Contains(deal));
        }
        // the term has run: a word kept
        Assert.Equal(1 + HouseTradeSystem.KeptRegard, g.Regard(maker)); Assert.True(g.Told($"The standing deal in materials with {maker.Name} has run its term: 25 were sent in all."));
        var kept = g.W.Dealings.Single(d => d.Kind == HouseTradeSystem.KeptKind); Assert.Equal((Mat, 25, 8), (kept.Good, kept.Units, kept.Turn)); Assert.Equal(1, g.W.Counter(HouseTradeSystem.KeptKey));
        Assert.Contains(g.Tc.Dealings.Lasting(g.W, g.Mine, maker), l => l.text == "A standing deal in materials ran its term in year 8: 25 were sent in all." && l.tone == "good");
        Assert.Empty(g.Tc.Economy.Compute(g.W, g.Mine.Id).Lines.Where(l => l.Kind == HouseTradeSystem.LedgerKind));
    }

    [Fact]
    public void Goods_passed_under_a_deal_are_not_counted_as_made_or_used_up_by_the_market()
    {
        // two games alike but for one deal between two other houses: the exchanges reckon the same production, the same appetite and the same price
        GoodState Reckoned(bool withDeal)
        {
            var g = Start(); var seller = g.Idle(Mat); var buyer = g.Others.First(h => h.Id != seller.Id); seller.Treasury.Materials = 100; buyer.Treasury.Credits = 100;
            if (withDeal)
            {
                var deal = HouseTradeSystem.Strike(g.W, seller, buyer, Mat, 9, 5, 150);
                Assert.Equal(new Delivery(9, ""), HouseTradeSystem.Due(g.W)[deal]); Assert.Single(g.Tc.Economy!.Compute(g.W, buyer.Id).Lines.Where(l => l.Kind == HouseTradeSystem.LedgerKind));
            }
            g.Tc.Market.Tick(g.W); return g.Good(Mat);
        }
        var plain = Reckoned(false); var dealt = Reckoned(true);
        Assert.Equal((plain.LastProduction, plain.LastConsumption, plain.SupplyShort, plain.DemandShort, plain.Price), (dealt.LastProduction, dealt.LastConsumption, dealt.SupplyShort, dealt.DemandShort, dealt.Price));
    }

    [Fact]
    public void The_player_may_be_the_seller_to_a_house_that_burns_more_than_it_brings_up()
    {
        var g = Start(); var burner = g.Others.First(h => g.T.Need(g.W, h, He) >= 1); g.SetRegard(burner, 0); int need = g.T.Need(g.W, burner, He); var f = g.T.Flow(g.W, burner, He);
        g.Good(He).Price = 2.0; g.Mine.Treasury.Helium3 = 30;   // cheap, so that the house's income covers it
        Assert.Equal($"{burner.Name} would take only {need} a year: it burns {f.burn} a year and brings up {f.made}", g.No(HouseTradeCommand.DealSell, burner, He, need + 1));
        var idle = g.Idle(He); g.SetRegard(idle, 0);
        Assert.Equal($"{idle.Name} has no use for helium-3 every year: it burns 0 a year and brings up 0", g.No(HouseTradeCommand.DealSell, idle, He, 1));
        Assert.Null(g.No(HouseTradeCommand.DealSell, burner, He, need, 10));
        // it pays under the market, and will not promise more than half of what it takes in
        int unit = g.T.DealUnitCredits(g.W, g.Mine, burner, He, selling: true), yearly = HouseTradeSystem.YearCost(unit, need); Assert.Equal(180, unit); Assert.Equal((need * 180 + 50) / 100, yearly);
        g.Good(He).Price = 400; int income = g.T.Income(g.W, burner);
        Assert.Equal($"{burner.Name} takes in {Money.Credits(income)} a year and will not promise more than half of it: this is {Money.Show(HouseTradeSystem.YearCost(36000, need))} a year", g.No(HouseTradeCommand.DealSell, burner, He, need));
        g.Good(He).Price = 2.0;
        Assert.StartsWith($"You send {burner.Name} {HouseTradeSystem.Of(need, He)} every year for 10 years, from next year, at 180 a unit: {Money.Credits(yearly)} a year into your treasury", g.T.DealSays(g.W, g.Mine, burner, He, need, 10, selling: true));
        g.Do(HouseTradeCommand.DealSell, burner, He, need, 10);
        var deal = g.W.TradeDeals.Single(); Assert.Equal((g.Mine.Id, burner.Id, need, 180, 13), (deal.SellerId, deal.BuyerId, deal.Units, deal.UnitCredits, deal.Until));
        Assert.Equal(0, g.T.Need(g.W, burner, He)); Assert.Equal(need, HouseTradeSystem.Promised(g.W, g.Mine.Id, He));
        var line = g.Tc.Economy!.Compute(g.W, g.Mine.Id).Lines.Single(l => l.Kind == HouseTradeSystem.LedgerKind);
        Assert.Equal(($"helium-3 sold to {burner.Name}", -need, yearly), (line.Source, line.Delta.Helium3, line.Delta.Credits));
        Assert.Equal($"You send {burner.Name} {HouseTradeSystem.Of(need, He)} a year at 180 a unit: {Money.Credits(yearly)} a year to you, until year 13.", HouseTradeSystem.Describe(g.W, deal, g.Mine.Id));
        burner.Treasury.Credits = 50; g.Books(); Assert.Equal(need, deal.Delivered); Assert.Equal(0, deal.Failed);
    }

    [Fact]
    public void A_seller_without_the_goods_sends_what_it_has_and_two_short_years_running_end_the_deal()
    {
        var g = Start(credits: 1000); var seller = g.Idle(Mat); g.SetRegard(seller, 0);
        var deal = HouseTradeSystem.Strike(g.W, seller, g.Mine, Mat, 5, 10, 160); int yearly = HouseTradeSystem.YearCost(160, 5); Assert.Equal(8, yearly);
        // a short year: it sends what it holds, and that is paid for
        seller.Treasury.Materials = 2;
        var line = g.Tc.Economy!.Compute(g.W, g.Mine.Id).Lines.Single(l => l.Kind == HouseTradeSystem.LedgerKind); Assert.Equal((2, -3), (line.Delta.Materials, line.Delta.Credits));
        g.Books();
        Assert.Equal((2, 3, 1, seller.Id), (deal.Delivered, deal.Short, deal.Failed, deal.FailedById)); Assert.Equal(0, seller.Treasury.Materials);
        Assert.True(g.Told($"{seller.Name} sent 2 of the 5 materials it owes you this year: it had no more. You paid for what came. One more such year ends the deal"));
        Assert.EndsWith("It has fallen 3 short so far. The last delivery failed: one more such year ends it.", HouseTradeSystem.Describe(g.W, deal, g.Mine.Id));
        // a full year clears it
        seller.Treasury.Materials = 20; g.Books(); Assert.Equal((7, 3, 0, ""), (deal.Delivered, deal.Short, deal.Failed, deal.FailedById));
        // two running end it, and it is the seller's fault: but the player's own head thinks what the player pleases
        int mineThinks = g.W.Relationship(g.Me.Id, g.HeadOf(seller).Id).Disposition;
        seller.Treasury.Materials = 0; g.Books(); Assert.Equal(1, deal.Failed); Assert.Contains(deal, g.W.TradeDeals);
        seller.Treasury.Materials = 0; g.Books();
        Assert.Empty(g.W.TradeDeals); Assert.Equal(1, g.W.Counter(HouseTradeSystem.DefaultedKey)); Assert.Equal(mineThinks, g.W.Relationship(g.Me.Id, g.HeadOf(seller).Id).Disposition);
        Assert.True(g.Told($"{seller.Name} has not sent the materials of your standing deal 2 years running. The deal is at an end."));
        var rec = g.W.Dealings.Single(d => d.Kind == HouseTradeSystem.DefaultKind); Assert.Equal((seller.Id, g.Mine.Id, Mat), (rec.FromHouseId, rec.ToHouseId, rec.Good));
        Assert.Contains(g.Tc.Dealings.Lasting(g.W, g.Mine, seller), l => l.text == $"It failed you 2 years running under a standing deal in materials, which ended in year {g.W.Turn}." && l.tone == "warn");
        Assert.NotNull(HouseTradeSystem.Fault(g.W, seller.Id, g.Mine.Id)); Assert.Null(HouseTradeSystem.Fault(g.W, g.Mine.Id, seller.Id));

        // where one store will not cover two deals, the earlier is served first
        var g2 = Start(); var s2 = g2.Idle(Mat); var other = g2.Others.First(h => h.Id != s2.Id); other.Treasury.Credits = 100;
        var first = HouseTradeSystem.Strike(g2.W, s2, g2.Mine, Mat, 5, 5, 100); var second = HouseTradeSystem.Strike(g2.W, s2, other, Mat, 5, 5, 100); s2.Treasury.Materials = 7;
        var due = HouseTradeSystem.Due(g2.W); Assert.Equal(new Delivery(5, ""), due[first]); Assert.Equal(new Delivery(2, s2.Id), due[second]);
    }

    [Fact]
    public void A_buyer_without_the_credits_is_sent_nothing_and_two_such_years_cost_it_the_sellers_regard()
    {
        var g = Start(credits: 0); var seller = g.Idle(Mat); g.SetRegard(seller, 0); seller.Treasury.Materials = 100;
        var deal = HouseTradeSystem.Strike(g.W, seller, g.Mine, Mat, 5, 10, 160); int regard = g.Regard(seller);
        Assert.Empty(g.Tc.Economy!.Compute(g.W, g.Mine.Id).Lines.Where(l => l.Kind == HouseTradeSystem.LedgerKind));   // nothing will come as things stand, and the forecast says so
        g.Books();
        Assert.Equal((0, 5, 1, g.Mine.Id), (deal.Delivered, deal.Short, deal.Failed, deal.FailedById));
        Assert.True(g.Told($"You had not the 800 credits for this year's 5 materials from {seller.Name}, and nothing was sent. One more such year ends the deal"));
        g.Mine.Treasury.Credits = 0; g.Books();
        Assert.Empty(g.W.TradeDeals); Assert.Equal(regard - HouseTradeSystem.DefaultRegard, g.Regard(seller)); Assert.Equal(10, HouseTradeSystem.DefaultRegard);
        Assert.True(g.Told($"You have not paid for the materials of your standing deal with {seller.Name} 2 years running. The deal is at an end: its head thinks -10 of you, and the house strikes no deal with yours for 8 years."));
        var rec = g.W.Dealings.Single(d => d.Kind == HouseTradeSystem.DefaultKind); Assert.Equal((g.Mine.Id, seller.Id), (rec.FromHouseId, rec.ToHouseId));
        // it is held against the house for eight years: for a standing deal, not for a lot
        var maker = seller; int year = g.W.Turn; g.Mine.Treasury.Credits = 500;
        Assert.Equal($"you failed it twice running in year {year}: it strikes none with you before year {year + 8}", g.No(HouseTradeCommand.DealBuy, maker, Mat, 1));
        Assert.Equal($"you failed it twice running in year {year}: it strikes none with you before year {year + 8}", g.No(HouseTradeCommand.DealSell, maker, Mat, 1));
        Assert.Null(g.No(HouseTradeCommand.Buy, maker, Mat, 1));
        Assert.Contains(g.Tc.Dealings.Lasting(g.W, g.Mine, seller), l => l.text == $"You failed it 2 years running under a standing deal in materials, which ended in year {year}: it strikes none with you before year {year + 8}.");
    }

    [Fact]
    public void Either_side_may_end_a_deal_early_at_the_cost_of_a_word_not_kept()
    {
        var g = Start(credits: 1000); var maker = g.Maker(Mat, 5); g.SetRegard(maker, 0); maker.Treasury.Materials = 100;
        g.Do(HouseTradeCommand.DealBuy, maker, Mat, 5, 10); var deal = g.W.TradeDeals.Single(); int regard = g.Regard(maker);
        Assert.Null(g.No(HouseTradeCommand.End, maker, dealId: deal.Id));
        Assert.Equal($"End it now, with 10 years still to run. The head of {maker.Name} thinks -15 of you for a word not kept, and the house strikes no deal with yours for 8 years.", HouseTradeSystem.EndSays(g.W, deal, g.Mine.Id));
        g.Do(HouseTradeCommand.End, maker, dealId: deal.Id);
        Assert.Empty(g.W.TradeDeals); Assert.Equal(regard - HouseTradeSystem.BrokenRegard, g.Regard(maker)); Assert.Equal(DealingsSystem.PledgeBrokenRegard, HouseTradeSystem.BrokenRegard);
        var rec = g.W.Dealings.Single(d => d.Kind == HouseTradeSystem.BrokenKind); Assert.Equal((g.Mine.Id, maker.Id, Mat, 5), (rec.FromHouseId, rec.ToHouseId, rec.Good, rec.Units)); Assert.Equal(1, g.W.Counter(HouseTradeSystem.BrokenKey));
        Assert.Contains(g.W.History, e => e.Text == $"{g.Mine.Name} ends its standing deal in materials with {maker.Name} before its term.");
        Assert.Equal("no such deal of your house's", g.No(HouseTradeCommand.End, maker, dealId: deal.Id));
        Assert.Equal("you ended a deal with it early in year 3: it strikes none with you before year 11", g.No(HouseTradeCommand.DealBuy, maker, Mat, 5));
        Assert.Contains(g.Tc.Dealings.Lasting(g.W, g.Mine, maker), l => l.text == "You ended a standing deal in materials early in year 3: it strikes none with you before year 11." && l.tone == "warn");
        g.Books(); Assert.Empty(g.Tc.Economy!.Compute(g.W, g.Mine.Id).Lines.Where(l => l.Kind == HouseTradeSystem.LedgerKind));
        g.W.Turn = 11; g.SetRegard(maker, 0); Assert.Null(g.No(HouseTradeCommand.DealBuy, maker, Mat, 5));
        // a deal between two other houses is not the player's to end
        var a = g.Others.First(); var b = g.Others.Skip(1).First(); var theirs = HouseTradeSystem.Strike(g.W, a, b, Mat, 2, 5, 150);
        Assert.Equal("no such deal of your house's", g.No(HouseTradeCommand.End, a, dealId: theirs.Id));

        // another house ends a deal it can no longer keep without cutting into what it burns, and bears the same
        var g2 = Start(); var seller = g2.Idle(He); var deal2 = HouseTradeSystem.Strike(g2.W, seller, g2.Mine, He, 3, 10, 600); seller.Treasury.Helium3 = 2;
        g2.SetRegard(g2.Mine, seller, 0);
        g2.T.Tick(g2.W);
        Assert.DoesNotContain(deal2, g2.W.TradeDeals);
        Assert.True(g2.Told($"{g2.HeadOf(seller).Name} of {seller.Name} has ended the standing deal in helium-3 before its term: it can no longer spare what it promised. It was 3 a year until year 13."));
        var rec2 = g2.W.Dealings.Single(d => d.Kind == HouseTradeSystem.BrokenKind); Assert.Equal((seller.Id, g2.Mine.Id), (rec2.FromHouseId, rec2.ToHouseId));
        Assert.Contains(g2.Tc.Dealings.Lasting(g2.W, g2.Mine, seller), l => l.text == "It ended a standing deal in helium-3 early in year 3.");
        // one that still has the goods keeps its word
        var g3 = Start(); var s3 = g3.Idle(He); var deal3 = HouseTradeSystem.Strike(g3.W, s3, g3.Mine, He, 3, 10, 600); s3.Treasury.Helium3 = 3; g3.T.Tick(g3.W); Assert.Contains(deal3, g3.W.TradeDeals);
    }

    [Fact]
    public void A_war_ends_a_deal_and_the_embargo_holds_it_up_and_neither_is_held_against_a_house()
    {
        var g = Start(credits: 1000); var seller = g.Idle(Mat); g.SetRegard(seller, 0); seller.Treasury.Materials = 100;
        var deal = HouseTradeSystem.Strike(g.W, seller, g.Mine, Mat, 5, 10, 160); int regard = g.Regard(seller);
        g.W.WorldCounters["embargo:" + g.Mine.Id] = g.W.Turn + 2;
        Assert.Equal(new Delivery(0, ""), HouseTradeSystem.Due(g.W)[deal]); Assert.Empty(g.Tc.Economy!.Compute(g.W, g.Mine.Id).Lines.Where(l => l.Kind == HouseTradeSystem.LedgerKind));
        g.Books();
        Assert.Contains(deal, g.W.TradeDeals); Assert.Equal((0, 0, 0), (deal.Delivered, deal.Short, deal.Failed));
        Assert.True(g.Told($"Nothing passed this year under the standing deal in materials with {seller.Name}: the Senate's embargo holds it up. It is held against neither house."));
        g.Books(); Assert.Equal(5, deal.Delivered);   // the embargo has lifted
        g.W.Wars.Add(new War { HouseA = g.Mine.Id, HouseB = seller.Id, Since = g.W.Turn });
        g.Books();
        Assert.Empty(g.W.TradeDeals); Assert.Equal(5, deal.Delivered); Assert.Equal(regard, g.Regard(seller)); Assert.Equal(0, g.Remembered(HouseTradeSystem.DefaultKind) + g.Remembered(HouseTradeSystem.BrokenKind));
        Assert.True(g.Told($"The standing deal in materials with {seller.Name} is at an end: nothing is bought or sold across a war, or across blood."));
    }

    [Fact]
    public void Trade_survives_a_save()
    {
        var g = Start(credits: 1000); var maker = g.Maker(Mat, 5); g.SetRegard(maker, 0); maker.Treasury.Materials = 100;
        g.Do(HouseTradeCommand.Buy, maker, Mat, 10); g.Do(HouseTradeCommand.DealBuy, maker, Mat, 5, 10); g.Books();
        var deal = g.W.TradeDeals.Single(); deal.Failed = 1; deal.FailedById = maker.Id; deal.Short = 2;
        string saved = SaveSystem.Serialize(g.W); var w2 = SaveSystem.Deserialize(saved);
        var d2 = w2.TradeDeals.Single();
        Assert.Equal((deal.Id, deal.Seq, deal.SellerId, deal.BuyerId, deal.Good, deal.Units, deal.UnitCredits, deal.Since, deal.Until, deal.Failed, deal.FailedById, deal.Delivered, deal.Short),
            (d2.Id, d2.Seq, d2.SellerId, d2.BuyerId, d2.Good, d2.Units, d2.UnitCredits, d2.Since, d2.Until, d2.Failed, d2.FailedById, d2.Delivered, d2.Short));
        Assert.Equal(5, d2.Delivered);
        var bought = w2.Dealings.Single(d => d.Kind == HouseTradeSystem.BoughtKind); Assert.Equal((Mat, 10, g.Mine.Id, maker.Id), (bought.Good, bought.Units, bought.FromHouseId, bought.ToHouseId));
        Assert.Contains(w2.Dealings, d => d.Kind == HouseTradeSystem.StruckKind && d.Good == Mat && d.Units == 5);
        Assert.Equal(g.W.Counter(HouseTradeSystem.SeqKey), w2.Counter(HouseTradeSystem.SeqKey)); Assert.Equal(g.W.House(maker.Id)!.Treasury.Materials, w2.House(maker.Id)!.Treasury.Materials);
        Assert.Equal(saved, SaveSystem.Serialize(w2));
        // and the loaded game goes on with it: the next year's delivery is made
        var tc2 = new TurnController(w2, Content); w2.Turn++; tc2.Economy!.Accrue(w2); Assert.Equal(10, d2.Delivered); Assert.Equal(0, d2.Failed);
    }

    [Fact]
    public void A_house_short_of_what_it_burns_buys_of_the_friendliest_house_that_will_spare_it()
    {
        var g = Start(); var buyer = g.Burner(He); int burn = g.T.Flow(g.W, buyer, He).burn; g.Good(He).Price = 2.0;
        foreach (var h in g.Others) h.Treasury.Helium3 = 0;
        var friend = g.Others.First(h => h.Id != buyer.Id && g.T.Flow(g.W, h, He).burn == 0); var stranger = g.Others.First(h => h.Id != buyer.Id && h.Id != friend.Id && g.T.Flow(g.W, h, He).burn == 0);
        friend.Treasury.Helium3 = 200; stranger.Treasury.Helium3 = 200; g.Mine.Treasury.Helium3 = 200; buyer.Treasury.Credits = 300;
        foreach (var h in g.Others.Where(h => h.Id != buyer.Id)) g.SetRegard(h, buyer, h.Id == friend.Id ? 60 : 5);
        g.SetRegard(g.Mine, buyer, 90);
        // one house in four looks to its stores in a year: within a few years this one does
        for (int i = 0; i < 40 && buyer.Treasury.Helium3 == 0; i++) { g.W.Turn++; g.T.Tick(g.W); }
        var rec = g.W.Dealings.First(d => d.Kind == HouseTradeSystem.BoughtKind && d.FromHouseId == buyer.Id);
        Assert.Equal(friend.Id, rec.ToHouseId); Assert.Equal(He, rec.Good); Assert.InRange(rec.Units, 1, HouseTradeSystem.OthersLotMost); Assert.Equal(Math.Min(4 + 2 * burn, HouseTradeSystem.OthersLotMost), rec.Units);
        Assert.Equal(rec.Units, buyer.Treasury.Helium3); Assert.Equal(300 - rec.Amount, buyer.Treasury.Credits);
        // nobody helps themselves to the player's store
        Assert.Equal(200, g.Mine.Treasury.Helium3); Assert.DoesNotContain(g.W.Dealings, d => d.ToHouseId == g.Mine.Id || d.FromHouseId == g.Mine.Id);
        // it keeps coin by: a house with none buys nothing
        var g2 = Start(); var poor = g2.Burner(He); g2.Good(He).Price = 2.0;
        foreach (var h in g2.Others) { h.Treasury.Helium3 = h.Id == poor.Id ? 0 : 50; }
        poor.Treasury.Credits = HouseTradeSystem.CoinKept;
        for (int i = 0; i < 40; i++) { g2.W.Turn++; g2.T.Tick(g2.W); }
        Assert.Equal(0, poor.Treasury.Helium3);
    }

    [Fact]
    public void A_house_that_burns_more_than_it_brings_up_strikes_a_standing_deal_with_one_that_makes_it()
    {
        var g = Start(); g.Good(He).Price = 2.0;
        var buyer = g.Others.First(h => g.T.Need(g.W, h, He) >= HouseTradeSystem.OthersDealLeast); var maker = g.Others.First(h => h.Id != buyer.Id && g.T.Surplus(g.W, h, He) >= HouseTradeSystem.OthersDealLeast);
        int need = g.T.Need(g.W, buyer, He), can = g.T.Surplus(g.W, maker, He);
        buyer.Treasury.Helium3 = g.T.Flow(g.W, buyer, He).burn + 20; buyer.Treasury.Credits = 300;   // a year or more in store: it looks ahead, and does not buy a lot
        foreach (var h in g.Others.Where(h => h.Id != buyer.Id)) { g.SetRegard(h, buyer, h.Id == maker.Id ? 60 : 5); h.Treasury.Credits = 0; }   // the rest have no coin, and strike nothing of their own
        for (int i = 0; i < 40 && !g.W.TradeDeals.Any(d => d.BuyerId == buyer.Id); i++) { g.W.Turn++; g.T.Tick(g.W); }
        Assert.Single(g.W.TradeDeals);
        var deal = g.W.TradeDeals.Single(d => d.BuyerId == buyer.Id);
        Assert.Equal((maker.Id, He, Math.Min(Math.Min(need, can), HouseTradeSystem.OthersDealMost), g.W.Turn, g.W.Turn + HouseTradeSystem.Terms[0]), (deal.SellerId, deal.Good, deal.Units, deal.Since, deal.Until));
        Assert.InRange(deal.UnitCredits, 190, 200);   // the market's 200, a little cheaper to a house its head thinks +60 of (and +1 since, for the bargain)
        Assert.Contains(g.W.Dealings, d => d.Kind == HouseTradeSystem.StruckKind && d.FromHouseId == buyer.Id && d.ToHouseId == maker.Id);
        // it strikes no second while the first runs
        for (int i = 0; i < 12; i++) { g.T.Tick(g.W); }
        Assert.Single(g.W.TradeDeals.Where(d => d.BuyerId == buyer.Id && d.Good == He));
        // and the deal is kept through the books like any other
        maker.Treasury.Helium3 = 50; int had = deal.Delivered; g.Books(); Assert.Equal(had + deal.Units, deal.Delivered);
    }

    [Fact]
    public void Now_and_then_a_house_sends_the_player_word_of_a_bargain()
    {
        // a house short of a good the player has to spare
        var g = Start(); var burner = g.Burner(He); g.Good(He).Price = 2.0;
        foreach (var h in g.Others) { h.Treasury.Helium3 = 0; h.Treasury.Materials = 20; h.Treasury.Credits = 100; g.SetRegard(h, h.Id == burner.Id ? 50 : -5); }
        g.Mine.Treasury.Helium3 = 100; g.Mine.Treasury.Materials = 50;
        for (int i = 0; i < 40 && g.W.Counter(HouseTradeSystem.OffersKey) == 0; i++) { g.W.Turn++; g.T.Tick(g.W); foreach (var h in g.Others) h.Treasury.Helium3 = 0; }
        var told = g.W.News.Single(n => n.Text.Contains("under Trade")); int year = g.W.Turn;
        Assert.Equal(("info", "houses", burner.Id), (told.Tone, told.Target, told.SubjectId));
        Assert.StartsWith($"{g.HeadOf(burner).Name} of {burner.Name} is short of helium-3: the house burns {g.T.Flow(g.W, burner, He).burn} a year and holds 0. It would buy up to ", told.Text);
        Assert.EndsWith("It is done on its page, under Trade.", told.Text);
        Assert.Null(g.No(HouseTradeCommand.Sell, burner, He, 2));   // and it can be acted on
        // not again for three years, from any house
        for (int i = 0; i < 2; i++) { g.W.Turn++; g.T.Tick(g.W); }
        Assert.Equal(1, g.W.Counter(HouseTradeSystem.OffersKey)); Assert.Equal(year, g.W.Counter(HouseTradeSystem.OfferedKey));
        // a house that thinks ill of the player sends no word
        var g2 = Start(); g2.Good(He).Price = 2.0;
        foreach (var h in g2.Others) { h.Treasury.Helium3 = 0; h.Treasury.Credits = 100; g2.SetRegard(h, -1); }
        g2.Mine.Treasury.Helium3 = 100;
        for (int i = 0; i < 40; i++) { g2.W.Turn++; g2.T.Tick(g2.W); foreach (var h in g2.Others) h.Treasury.Helium3 = 0; }
        Assert.Equal(0, g2.W.Counter(HouseTradeSystem.OffersKey));

        // a house with to spare what the player burns and has not
        var g3 = Start(); string? burnt = g3.T.Goods.Select(x => x.Id).FirstOrDefault(id => g3.T.Flow(g3.W, g3.Mine, id).burn > 0);
        if (burnt == null) return;   // this house burns nothing in its ledger at the start
        g3.Good(burnt).Price = 2.0; var holder = g3.Others.First(h => g3.T.Flow(g3.W, h, burnt).burn == 0);
        foreach (var h in g3.Others) { foreach (var good in g3.T.Goods) h.Treasury.Set(good.Id, 100); g3.SetRegard(h, h.Id == holder.Id ? 50 : -5); }
        foreach (var good in g3.T.Goods) g3.Mine.Treasury.Set(good.Id, good.Id == burnt ? 0 : 5);
        for (int i = 0; i < 40 && g3.W.Counter(HouseTradeSystem.OffersKey) == 0; i++) { g3.W.Turn++; g3.T.Tick(g3.W); g3.Mine.Treasury.Set(burnt, 0); }
        var word = g3.W.News.Single(n => n.Text.Contains("under Trade"));
        Assert.Equal(holder.Id, word.SubjectId); Assert.Contains($"has {Resource.Label(burnt)} to spare, and your house burns", word.Text); Assert.Contains("It would sell you up to ", word.Text);
    }

    [Fact]
    public void Over_the_years_the_other_houses_trade_with_each_other_and_the_same_game_plays_the_same()
    {
        World Play(int years) { var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 5); var tc = new TurnController(w, Content); for (int y = 0; y < years; y++) Year(tc); return w; }
        var w = Play(14);
        Assert.True(w.Counter(HouseTradeSystem.TradesKey) > 0, "no house bought of another in fourteen years");
        var bought = w.Dealings.Where(d => d.Kind == HouseTradeSystem.BoughtKind).ToList(); Assert.NotEmpty(bought);
        Assert.All(bought, d => { Assert.NotEqual(w.Protagonist!.HouseId, d.FromHouseId); Assert.NotEqual(w.Protagonist!.HouseId, d.ToHouseId); Assert.True(d.Units >= 1 && d.Units <= HouseTradeSystem.OthersLotMost * 4 && d.Amount >= 1); });
        // sparingly: a handful of lots a year in the whole Reach at the most, and no store driven below nothing
        Assert.True(w.Counter(HouseTradeSystem.TradesKey) <= 14 * 3, $"{w.Counter(HouseTradeSystem.TradesKey)} lots in fourteen years");
        Assert.All(w.Houses.Values, h => Assert.All(Content.Market.Goods, good => Assert.True(h.Treasury.Get(good.Id) >= 0)));
        Assert.Equal(SaveSystem.Serialize(w), SaveSystem.Serialize(Play(14)));
    }

    [Fact]
    public void A_corner_the_merchants_cannot_supply_is_bought_out_of_the_houses_stores()
    {
        var g = Start(credits: 20000); var cat = Content.Market; var def = cat.Good(Exo)!; var state = g.Good(Exo);
        var holders = g.Others.Where(h => g.T.Flow(g.W, h, Exo).burn == 0).Take(3).ToList(); Assert.Equal(3, holders.Count);
        foreach (var h in g.W.Houses.Values) h.Treasury.Exotics = 0;
        foreach (var h in holders) { h.Treasury.Exotics = 20; g.SetRegard(h, 0); }
        state.Stock = state.StockRef = 4;
        var before = CornerSystem.Standing(g.W, cat, Exo); Assert.Equal((64, 4, 60), (before.All, before.Merchants, before.Houses)); Assert.Equal(32, CornerSystem.HalfOf(before.All));
        // the merchants alone cannot supply it, and the word says where the rest is
        Assert.True(MarketSystem.QuoteBuy(g.W, Content.Trade, g.Mine.Id, Exo, 5).Refusal != null);
        string word = CornerSystem.Take(g.W, Content.Trade, g.Mine.Id, Exo);
        Assert.Contains("Half is 32 and the merchants hold only 4", word); Assert.Contains("The merchants cannot supply a corner: the other 28 would have to be bought out of the houses' own stores, of what each will spare, on its page under Trade.", word);
        Assert.Contains($"The other houses keep 60: {holders[0].Name} holds 20 and {holders[1].Name} 20.", word);
        // each house spares what it holds above its floor, and two of them are enough
        foreach (var h in holders.Take(2))
        {
            Assert.Equal(18, g.T.Spare(g.W, h, Exo));
            g.Do(HouseTradeCommand.Buy, h, Exo, 18);
        }
        Assert.Equal(36, g.Mine.Treasury.Exotics);
        var after = CornerSystem.Standing(g.W, cat, Exo); Assert.Equal(64, after.All); Assert.Equal(g.Mine.Id, after.Top!.HouseId); Assert.True(CornerSystem.Cornered(after.Top.Units, after.All));
        Assert.Equal("", CornerSystem.Take(g.W, Content.Trade, g.Mine.Id, Exo)); Assert.StartsWith("Your house has cornered it: you hold 36 of the 64 in the Reach.", CornerSystem.Held(g.W, Content.Trade, g.Mine.Id, Exo));
        // and the year's reckoning finds it
        CornerSystem.Reckon(g.W, cat, def, state);
        Assert.True(state.Cornered); Assert.Equal(g.Mine.Id, state.CornerHouse); Assert.True(state.CornerPremium >= 0.5);
        Assert.True(g.Told("Your house holds 36 of the 64 exotics in the Reach: half or more of all there is. It is cornered."));
        // what it cost: more a unit than the market's price, for taking all a house would spare
        var paid = g.W.Dealings.Where(d => d.Kind == HouseTradeSystem.BoughtKind).ToList(); Assert.Equal(2, paid.Count);
        Assert.All(paid, d => Assert.Equal(Ceil(g.T.Mid(g.W, Exo) * 18 * 1.3), d.Amount));
    }
}
