using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Cornering a good (docs/ECONOMY_MARKET.md, "Cornering a good"; docs/playthroughs/family.md F46): all there is and who holds it,
/// a price that answers to a third and to a half held, the merchants' slower refill, the plain word, and the word that goes round.</summary>
public class CornerTests
{
    private static readonly string Root = ContentLocator.FindContentDir();
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(Root);
    private static readonly string[] Scenarios = { "the_fallen_house", "the_old_lion", "the_new_man", "the_circling_heir", "the_risen_officer", "the_imperial_court", "custom_republic", "custom_imperium" };
    private static (World w, TurnController tc) Start(string scenario = "the_fallen_house", ulong seed = 1) { var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(Root, scenario), seed); return (w, new TurnController(w, Content)); }
    private static CommandContext Ctx(World w) => new(w, WorldBuilder.RngFor(w));
    private static GoodState Good(World w, string id) => w.Market!.Goods[id];
    private static House Mine(World w) => w.House(w.Protagonist!.HouseId)!;
    private static IEnumerable<House> Houses(World w) => w.Houses.Values.Where(h => !World.Outsider(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal);
    /// <summary>Reckon the market for some years with nothing else moving (as tests/MarketTests.cs does).</summary>
    private static void Years(World w, TurnController tc, int n)
    {
        if (w.Turn < MarketSystem.WarmUpTurns + 6) w.Turn = MarketSystem.WarmUpTurns + 6;
        for (int i = 0; i < n; i++) { w.Turn++; tc.Economy!.Accrue(w); tc.Market.Tick(w); }
    }
    /// <summary>Put a house's store of a good at a figure, as if it had stood there last year too: the market reads consumption from the
    /// change in what the houses hold, and a store set by hand is not a year's appetite.</summary>
    private static void Hold(World w, House h, string good, int units)
    {
        var g = Good(w, good); int was = Math.Max(0, h.Treasury.Get(good)); h.Treasury.Set(good, units); g.LastTotal += units - was;
    }
    /// <summary>A market of a good with exactly these stores: the merchants', the player's, and one other house's; every other house has none.</summary>
    private static House Lay(World w, string good, int merchants, int mine, int other)
    {
        var rival = Houses(w).First(h => h.Id != Mine(w).Id);
        foreach (var h in Houses(w)) Hold(w, h, good, h.Id == Mine(w).Id ? mine : h.Id == rival.Id ? other : 0);
        var g = Good(w, good); g.Stock = g.StockRef = merchants;
        return rival;
    }
    /// <summary>A market of a good in which every house but the player's keeps the same store, so that none of them holds much of it.</summary>
    private static void Spread(World w, string good, int merchants, int mine, int each)
    {
        foreach (var h in Houses(w)) Hold(w, h, good, h.Id == Mine(w).Id ? mine : each);
        var g = Good(w, good); g.Stock = g.StockRef = merchants;
    }
    private static int Merchants(World w, string houseId) => PolicySystem.Approval(w, houseId, CornerSystem.Estate);

    [Fact]
    public void AllThereIs_IsTheMerchantsStockAndEveryHousesStore_AndTheLargestHoldersAreListed()
    {
        var (w, tc) = Start(); Years(w, tc, 3);
        foreach (var d in Content.Market.Goods)
        {
            var s = CornerSystem.Standing(w, Content.Market, d.Id);
            Assert.Equal((int)Math.Floor(Good(w, d.Id).Stock), s.Merchants); Assert.Equal(Houses(w).Sum(h => Math.Max(0, h.Treasury.Get(d.Id))), s.Houses); Assert.Equal(s.Merchants + s.Houses, s.All);
            Assert.Equal(s.Holders.OrderByDescending(h => h.Units).ThenBy(h => h.HouseId, StringComparer.Ordinal).Select(h => h.HouseId), s.Holders.Select(h => h.HouseId));   // the largest first
            Assert.All(s.Holders, h => { Assert.True(h.Units > 0); Assert.Equal((int)Math.Round(100.0 * h.Units / s.All), h.SharePct); Assert.Equal(w.House(h.HouseId)!.Name, h.Name); });
            Assert.DoesNotContain(s.Holders, h => World.Outsider(h.HouseId));
        }
        // laid out by hand: 12 with the merchants, 9 with the player, 10 with another house: 31 in all
        var rival = Lay(w, "exotics", 12, 9, 10); var st = CornerSystem.Standing(w, Content.Market, "exotics");
        Assert.Equal(31, st.All); Assert.Equal(12, st.Merchants); Assert.Equal(19, st.Houses); Assert.Equal(rival.Id, st.Top!.HouseId); Assert.Equal(10, st.Top.Units); Assert.Equal(32, st.Top.SharePct);
        Assert.Equal(9, st.Of(Mine(w).Id)); Assert.Equal(0, st.Of("nobody"));
        // the list is the first three, and the house asking even when it is not among them
        var others = Houses(w).Where(h => h.Id != Mine(w).Id).Take(4).ToList(); for (int i = 0; i < 4; i++) Hold(w, others[i], "exotics", 20 + i);
        Hold(w, Mine(w), "exotics", 2); st = CornerSystem.Standing(w, Content.Market, "exotics"); var listed = CornerSystem.Listed(st, Mine(w).Id);
        Assert.Equal(CornerSystem.Shown + 1, listed.Count); Assert.Equal(Mine(w).Id, listed[^1].HouseId); Assert.Equal(new[] { 23, 22, 21, 2 }, listed.Select(h => h.Units).ToArray());
        Assert.Equal(CornerSystem.Shown, CornerSystem.Listed(st, "nobody").Count);
        // and the screen's model carries it
        tc.BeginTurn(); var line = CourtProjection.Build(tc, Content).Market!.Goods.Single(g => g.Id == "exotics"); var now = CornerSystem.Standing(w, Content.Market, "exotics");
        Assert.Equal(now.All, line.All); Assert.Equal(now.Merchants, line.Stock); Assert.Equal(now.Of(Mine(w).Id), line.Held);
        Assert.Equal(CornerSystem.Listed(now, Mine(w).Id).Select(h => (h.HouseId, h.Units, h.SharePct)), line.Holders.Select(h => (h.HouseId, h.Units, h.SharePct)));
        Assert.Single(line.Holders, h => h.Mine); Assert.Equal(Mine(w).Id, line.Holders.Single(h => h.Mine).HouseId);
        Assert.NotEmpty(line.CornerTake);
    }

    [Fact]
    public void AThirdIsFelt_AHalfIsACorner_AHandfulIsNeither_AndThePremiumGrowsWithTheShare()
    {
        Assert.Equal(6, CornerSystem.LeastUnits); Assert.Equal(50, CornerSystem.HalfPremiumPct); Assert.Equal(100, CornerSystem.WholePremiumPct);
        Assert.False(CornerSystem.Felt(9, 31)); Assert.True(CornerSystem.Felt(11, 31)); Assert.False(CornerSystem.Cornered(15, 31)); Assert.True(CornerSystem.Cornered(16, 31));
        Assert.Equal(16, CornerSystem.HalfOf(31)); Assert.Equal(11, CornerSystem.ThirdOf(31)); Assert.Equal(6, CornerSystem.HalfOf(8)); Assert.Equal(6, CornerSystem.ThirdOf(9));
        // a handful: five of six is not a corner, six of six is
        Assert.False(CornerSystem.Felt(5, 6)); Assert.False(CornerSystem.Cornered(5, 6)); Assert.Equal(0, CornerSystem.Premium(5, 6)); Assert.True(CornerSystem.Cornered(6, 6));
        // the premium: nothing below a third, slowly from a third, half as much again at half, double at the whole
        Assert.Equal(0, CornerSystem.Premium(32, 100)); Assert.Equal(0, CornerSystem.Premium(0, 0));
        Assert.InRange(CornerSystem.Premium(36, 100), 0.001, 0.03); Assert.Equal(0.08, CornerSystem.Premium(40, 100), 3); Assert.Equal(0.245, CornerSystem.Premium(45, 100), 3);
        Assert.Equal(0.5, CornerSystem.Premium(50, 100), 6); Assert.Equal(0.6, CornerSystem.Premium(60, 100), 6); Assert.Equal(0.75, CornerSystem.Premium(75, 100), 6); Assert.Equal(1.0, CornerSystem.Premium(100, 100), 6);
        double last = -1; for (int n = 33; n <= 100; n++) { double p = CornerSystem.Premium(n, 100); Assert.True(p >= last, $"{n}: {p} after {last}"); last = p; }
        Assert.Equal(1, CornerSystem.Refill(32, 100)); Assert.Equal(0.5, CornerSystem.Refill(34, 100)); Assert.Equal(0.25, CornerSystem.Refill(50, 100));

        // in the market: the same quiet Reach four times over, the player's house holding nothing, two-fifths, a little over half, and four-fifths of the rare earths
        var prices = new List<double>(); var premiums = new List<double>();
        foreach (int pct in new[] { 0, 40, 55, 80 })
        {
            // every other house keeps 12, so that a year's production does not move the shares; the player's store is set against all the rest
            var (w, tc) = Start(); Years(w, tc, 2); Spread(w, "rare_earths", 9, 0, 12); var st = CornerSystem.Standing(w, Content.Market, "rare_earths"); int rest = st.All + 20;
            Hold(w, Mine(w), "rare_earths", pct == 0 ? 0 : (int)Math.Ceiling(rest * pct / (100.0 - pct)));
            Years(w, tc, 3); var g = Good(w, "rare_earths"); prices.Add(g.Price); premiums.Add(g.CornerPremium);
            if (pct == 0) { Assert.Equal("", g.CornerHouse); Assert.False(g.Cornered); Assert.Equal(0, g.CornerPremium); Assert.DoesNotContain("of all there is", g.Cause); }
            else
            {
                Assert.Equal(Mine(w).Id, g.CornerHouse); Assert.Equal(pct >= 50, g.Cornered); Assert.Contains("your house", g.Cause); Assert.Contains("of all there is", g.Cause);
                if (pct >= 50) Assert.Contains("has cornered it", g.Cause);
                var live = CornerSystem.Standing(w, Content.Market, "rare_earths"); Assert.Equal(CornerSystem.Premium(live.Top!.Units, live.All) > 0, g.CornerPremium > 0);
            }
        }
        Assert.True(premiums[0] < premiums[1] && premiums[1] < premiums[2] && premiums[2] < premiums[3], string.Join(" ", premiums));
        Assert.True(prices[0] < prices[1] && prices[1] < prices[2] && prices[2] < prices[3], string.Join(" ", prices));
        Assert.True(prices[2] > prices[0] * 1.25, $"{prices[0]} → {prices[2]}");   // a corner is worth having
        Assert.True(prices[3] <= Content.Market.Good("rare_earths")!.Base * Content.Market.Rules.CapPct / 100.0);   // and the cap still holds
    }

    [Fact]
    public void TheStartOfAGame_HasNoCorner_AndNoPriceStandsHigherForAHoard()
    {
        foreach (var sc in Scenarios)
        {
            var (w, tc) = Start(sc, 2);
            foreach (var d in Content.Market.Goods)
            {
                var g = Good(w, d.Id); var s = CornerSystem.Standing(w, Content.Market, d.Id);
                Assert.Equal("", g.CornerHouse); Assert.False(g.Cornered); Assert.Equal(-1, g.CornerSince); Assert.Equal(0, g.CornerPremium);
                Assert.True(s.Top == null || !CornerSystem.Felt(s.Top.Units, s.All), $"{sc} {d.Id}: {s.Top?.Name} holds {s.Top?.Units} of {s.All} on the first day");
            }
            // the first two years: nothing is cornered, no chronicle line speaks of a corner, and nothing is added to any price
            for (int t = 0; t < 2; t++) { foreach (var i in tc.BeginTurn()) if (tc.Offer(i).FirstOrDefault(o => o.Available) is { } c) tc.Resolve(i, c.Choice.Id); tc.EndTurn(); }
            foreach (var d in Content.Market.Goods) { var g = Good(w, d.Id); Assert.False(g.Cornered, $"{sc} {d.Id}"); Assert.Equal(0, g.CornerPremium); }
            Assert.DoesNotContain(w.History, e => e.Text.Contains("cornered")); Assert.DoesNotContain(w.News, n => n.Text.Contains("cornered"));
        }
    }

    [Fact]
    public void TheOtherHouses_LiveUnderTheSameRule_AndTheirStoresDoNotCornerTheReachByAccident()
    {
        // a whole game played by the stewards alone: a maker's stores may pass a third and be felt a little; none comes to hold half
        var (w, tc) = Start("the_old_lion", 3); double most = 0;
        for (int t = 0; t < 16; t++)
        {
            foreach (var i in tc.BeginTurn()) if (tc.Offer(i).FirstOrDefault(o => o.Available) is { } c) tc.Resolve(i, c.Choice.Id); tc.EndTurn();
            foreach (var g in w.Market!.Goods.Values) { Assert.False(g.Cornered, $"T{w.Turn} {g.Id} {g.CornerHouse} {g.CornerShare:F2}"); most = Math.Max(most, g.CornerPremium); }
        }
        Assert.True(most < 0.25, $"{most}");
        // and when one of them does hold half, the rule is the one the player lives under: the same premium, the same word, the same cost
        var (w2, tc2) = Start(); Years(w2, tc2, 2); var rival = Lay(w2, "exotics", 4, 0, 20); int regard = Merchants(w2, rival.Id); double before = Good(w2, "exotics").Price;
        Years(w2, tc2, 1); var ex = Good(w2, "exotics");
        Assert.Equal(rival.Id, ex.CornerHouse); Assert.True(ex.Cornered); Assert.Equal(w2.Turn, ex.CornerSince); Assert.True(ex.CornerPremium >= 0.5); Assert.True(ex.Price > before);
        Assert.Contains($"{rival.Name} has cornered it", ex.Cause);
        Assert.Contains(w2.History, e => e.Text.StartsWith($"{rival.Name} has cornered exotics: it holds "));
        var told = w2.News.Last(n => n.Text.Contains("has cornered exotics")); Assert.Equal("warn", told.Tone); Assert.Equal("exchange:goods", told.Target); Assert.Equal("exotics", told.SubjectId); Assert.StartsWith(rival.Name, told.Text);
        Assert.Equal(regard - CornerSystem.MerchantsOnCorner, Merchants(w2, rival.Id));
        Assert.Equal(new[] { "exotics" }, CornerSystem.HeldBy(w2, rival.Id).ToArray()); Assert.Empty(CornerSystem.HeldBy(w2, Mine(w2).Id));
        // the player is told plainly who holds it, and what it would take to have it from them
        string word = CornerSystem.Word(w2, Content.Trade, Mine(w2).Id, "exotics");
        Assert.StartsWith($"{rival.Name} has cornered it: it holds ", word); Assert.Contains("It cannot be cornered by buying", word);
        var line = Build(tc2).Single(g => g.Id == "exotics"); Assert.Equal(rival.Id, line.CornerHouseId); Assert.True(line.Cornered); Assert.False(line.CornerMine); Assert.True(line.PremiumPct >= 50);
    }
    private static IReadOnlyList<GoodLine> Build(TurnController tc) { tc.BeginTurn(); return CourtProjection.Build(tc, Content).Market!.Goods; }

    [Fact]
    public void ACorner_IsBoughtOnTheExchange_IsHeardOf_CostsTheMerchantsRegard_AndLastsOnlyWhileItIsHeld()
    {
        // F46: the player buys what the merchants hold of a thin good. Here it is enough: 12 with the merchants, 9 in the house's store, 10 elsewhere
        var (w, tc) = Start(); var p = w.Protagonist!; var house = Mine(w); var t = Content.Trade; Years(w, tc, 2); house.Treasury.Credits = 5000;
        Lay(w, "exotics", 12, 9, 10); double calm = Good(w, "exotics").Price; int regard = Merchants(w, house.Id);
        Assert.StartsWith("You hold 9 of the 31 in the Reach: 7 more is half, and the merchants hold 12.", CornerSystem.Word(w, t, house.Id, "exotics"));
        Assert.True(new CommandExecutor().TryExecute(new BuyGoodCommand(p.Id, "exotics", 7, t), Ctx(w)));
        // at once the screen says so; the price answers at the year's reckoning
        Assert.StartsWith("Your house has cornered it: you hold 16 of the 31 in the Reach.", CornerSystem.Word(w, t, house.Id, "exotics")); Assert.Contains("Hold fewer than 16 and the corner is broken.", CornerSystem.Word(w, t, house.Id, "exotics"));
        Assert.False(Good(w, "exotics").Cornered); int news = w.News.Count;
        // the rest of what the merchants hold as well, so that a year's production does not undo it before the reckoning
        Assert.True(new CommandExecutor().TryExecute(new BuyGoodCommand(p.Id, "exotics", 5, t), Ctx(w))); Assert.Equal(21, house.Treasury.Get("exotics"));
        Assert.NotNull(new BuyGoodCommand(p.Id, "exotics", 1, t).Refusal(Ctx(w)));   // the shelves are bare
        Years(w, tc, 1); var g = Good(w, "exotics");
        Assert.True(g.Cornered); Assert.Equal(house.Id, g.CornerHouse); Assert.Equal(w.Turn, g.CornerSince); Assert.True(g.CornerShare >= 0.5); Assert.True(g.CornerPremium >= 0.5);
        Assert.True(g.Price > calm * 1.2, $"{calm} → {g.Price}"); Assert.StartsWith("up ", g.Cause); Assert.Contains("your house has cornered it and holds", g.Cause);
        Assert.Contains(w.History, e => e.Text.StartsWith($"{house.Name} has cornered exotics: it holds "));
        var told = w.News.Skip(news).Single(n => n.Text.Contains("It is cornered")); Assert.Equal("good", told.Tone); Assert.Equal("exchange:goods", told.Target); Assert.Equal("exotics", told.SubjectId);
        Assert.Equal(regard - CornerSystem.MerchantsOnCorner, Merchants(w, house.Id));
        Assert.Equal(new[] { "exotics" }, CornerSystem.HeldBy(w, house.Id).ToArray());
        // it stands a second year: dearer still, a point more of the merchants' regard, and no second announcement
        int since = g.CornerSince, lines = w.History.Count(e => e.Text.Contains("has cornered exotics")); double first = g.Price;
        Years(w, tc, 1);
        Assert.True(g.Cornered); Assert.Equal(since, g.CornerSince); Assert.True(g.Price > first); Assert.Equal(lines, w.History.Count(e => e.Text.Contains("has cornered exotics")));
        Assert.Equal(regard - CornerSystem.MerchantsOnCorner - CornerSystem.MerchantsEachYear, Merchants(w, house.Id));
        // the hoard cannot be cashed at the top price: one order of the whole of it fetches far less a unit than the quote
        int hoard = house.Treasury.Get("exotics"); double quote = MarketSystem.SellPrice(w, t, house.Id, "exotics"); var dump = MarketSystem.QuoteSell(w, t, house.Id, "exotics", hoard);
        Assert.Null(dump.Refusal); Assert.True(dump.Credits < quote * hoard * 0.7, $"{dump.Credits} against {quote * hoard}"); Assert.True(dump.UnitPrice < MarketSystem.QuoteSell(w, t, house.Id, "exotics", 2).UnitPrice);
        // half of it sold: the corner is broken, the Reach hears it, the price starts down
        double top = g.Price; int after = regard - CornerSystem.MerchantsOnCorner - CornerSystem.MerchantsEachYear;
        Assert.True(new CommandExecutor().TryExecute(new SellGoodCommand(p.Id, "exotics", hoard / 2, t), Ctx(w)));
        Years(w, tc, 1);
        Assert.False(g.Cornered); Assert.Equal(-1, g.CornerSince); Assert.True(g.Price < top, $"{top} → {g.Price}"); Assert.StartsWith("down ", g.Cause);
        Assert.Contains(w.History, e => e.Text == $"{house.Name} no longer holds half of the Reach's exotics: the corner is broken.");
        Assert.Contains(w.News, n => n.Text.Contains("the corner is broken") && n.Target == "exchange:goods");
        Assert.Equal(after, Merchants(w, house.Id));   // no further cost once it is let go
        Assert.Empty(CornerSystem.HeldBy(w, house.Id));
        // the rest sold: nothing is held, nothing is added to the price, and it comes down further
        double mid = g.Price; Assert.True(new CommandExecutor().TryExecute(new SellGoodCommand(p.Id, "exotics", house.Treasury.Get("exotics"), t), Ctx(w)));
        Years(w, tc, 2);
        Assert.Equal(0, g.CornerPremium); Assert.True(g.CornerHouse != house.Id); Assert.True(g.Price < mid, $"{mid} → {g.Price}");
    }

    [Fact]
    public void WhileACornerStands_TheMerchantsBringAQuarterOfWhatTheyWould_AndHalfWhileAThirdIsHeld()
    {
        // three Reaches alike in everything but who holds the rare earths; the merchants' shelves emptied in each
        double Refilled(int tenths)
        {
            // every other house keeps 10 and the merchants nothing; the player's store is set against that, in tenths of theirs
            var (w, tc) = Start(); Years(w, tc, 2); int others = 10 * (Houses(w).Count() - 1); Spread(w, "rare_earths", 0, others * tenths / 10, 10);
            var g = Good(w, "rare_earths");
            Years(w, tc, 1);
            Assert.Equal(tenths >= 10, g.Cornered); Assert.Equal(tenths > 0, g.CornerHouse == Mine(w).Id); if (tenths == 0) Assert.Equal("", g.CornerHouse);
            return g.Stock;
        }
        double free = Refilled(0), felt = Refilled(7), cornered = Refilled(15); int usual = Content.Market.Rules.RelaxPct;
        Assert.True(free > 1, $"{free}"); Assert.True(cornered > 0 && cornered < felt && felt < free, $"{cornered} {felt} {free}");
        // the year's refill is a fifth of what the shelves lack; half of that while a third is held, a quarter while a corner stands
        double lack = (free - cornered) / (usual / 100.0 * (1 - CornerSystem.CorneredRefillPct / 100.0)), opened = free - lack * usual / 100.0;
        Assert.InRange(opened, -1e-6, 1.0);   // what the year's own surplus left on the shelves before the refill: next to nothing
        Assert.Equal(opened + lack * usual / 100.0 * CornerSystem.FeltRefillPct / 100.0, felt, 6);
        Assert.Equal(opened + lack * usual / 100.0 * CornerSystem.CorneredRefillPct / 100.0, cornered, 6);
        Assert.True(cornered - opened < (free - opened) * 0.26 && felt - opened < (free - opened) * 0.51, $"{cornered} {felt} {free}");
    }

    [Fact]
    public void ThePlainWord_SaysWhatACornerWouldTake_OrWhyItCannotBeBought()
    {
        var (w, tc) = Start(); var house = Mine(w); var t = Content.Trade; Years(w, tc, 2);
        // a thin good within reach
        Lay(w, "exotics", 12, 9, 10);
        Assert.Equal("", CornerSystem.Held(w, t, house.Id, "exotics"));
        string fed = $" The Reach brings up {Good(w, "exotics").LastProduction:0} more a year, so a corner that is not fed is soon a smaller share.";
        Assert.Equal("You hold 9 of the 31 in the Reach: 7 more is half, and the merchants hold 12." + fed, CornerSystem.Take(w, t, house.Id, "exotics"));
        // a third held, half within reach
        Lay(w, "exotics", 12, 11, 8);
        Assert.Equal("You hold 11 of the 31 in the Reach, a third or more, and the price feels it. The price it settles at is 1% higher for it. The merchants bring half of what they would. 5 more is half, and the merchants hold 12." + fed, CornerSystem.Word(w, t, house.Id, "exotics"));
        // two houses each with a third: the word names the larger, and still says what this house holds
        Lay(w, "exotics", 6, 12, 13);
        Assert.StartsWith(Houses(w).First(h => h.Id != house.Id).Name + " holds 13 of the 31 in the Reach, a third or more, and the price feels it.", CornerSystem.Word(w, t, house.Id, "exotics"));
        Assert.Contains(" You hold 12 of the 31 in the Reach: 4 more is half, and the merchants hold 6.", CornerSystem.Word(w, t, house.Id, "exotics"));
        // half out of reach, a third within it
        Lay(w, "exotics", 6, 5, 20);
        Assert.Equal("You hold 5 of the 31 in the Reach. Half is 16 and the merchants hold only 6: with all the merchants have you would hold 11 of 31, or 35%. It cannot be cornered by buying. The other houses keep 20 in their own stores"
            + $", and the Reach brings up {Good(w, "exotics").LastProduction:0} more a year. A third, from which the price begins to feel it, is 6 more.", CornerSystem.Take(w, t, house.Id, "exotics"));
        // the shelves bare
        Lay(w, "exotics", 0, 5, 20);
        Assert.Equal($"You hold 5 of the 25 in the Reach. Half is 13 and the merchants hold none: it cannot be cornered by buying. The other houses keep 20 in their own stores, and the Reach brings up {Good(w, "exotics").LastProduction:0} more a year.", CornerSystem.Take(w, t, house.Id, "exotics"));
        // a good the Reach makes in plenty: it cannot be done, and the figures say why
        var st = CornerSystem.Standing(w, Content.Market, "materials"); string word = CornerSystem.Take(w, t, house.Id, "materials"); int me = st.Of(house.Id);
        Assert.True(st.Merchants + me < CornerSystem.ThirdOf(st.All), $"{st.Merchants} + {me} of {st.All}");
        Assert.StartsWith($"You hold {me} of the {st.All} in the Reach. Half is {CornerSystem.HalfOf(st.All)} and the merchants hold only {st.Merchants}: with all the merchants have you would hold {me + st.Merchants} of {st.All}, or ", word);
        Assert.Contains("It cannot be cornered by buying.", word); Assert.Contains($"The other houses keep {st.Houses - me} in their own stores, and the Reach brings up {Good(w, "materials").LastProduction:0} more a year.", word);
        Assert.DoesNotContain("A third, from which", word);
        // isotopes are not sold on any open market: a corner in them cannot be bought at all
        Assert.NotNull(MarketSystem.QuoteBuy(w, t, house.Id, "isotopes", 1).Refusal); Assert.False(CornerSystem.Open(w, t, Content.Market.Good("isotopes")!)); Assert.True(CornerSystem.Open(w, t, Content.Market.Good("exotics")!));
        Lay(w, "isotopes", 5, 2, 13); word = CornerSystem.Take(w, t, house.Id, "isotopes");
        Assert.Equal("You hold 2 of the 20 in the Reach. Isotopes are not sold on any open market while the law of the Arms Market keeps them off the exchanges, so a corner in them cannot be bought: it would have to be brought up from your own worlds. 8 more is half.", word);
        // a handful: where half of all there is would be fewer than six, six is the least
        Lay(w, "exotics", 5, 0, 2);
        Assert.Equal("You hold 0 of the 7 in the Reach. The least that makes a corner (half of all there is, and no fewer than 6) is 6 and the merchants hold only 5: with all the merchants have you would hold 5 of 7, or 71%. It cannot be cornered by buying. The other houses keep 2 in their own stores"
            + $", and the Reach brings up {Good(w, "exotics").LastProduction:0} more a year.", CornerSystem.Take(w, t, house.Id, "exotics"));
        Lay(w, "exotics", 0, 0, 0); Assert.Equal("There is none in the Reach to corner.", CornerSystem.Take(w, t, house.Id, "exotics"));
        // the holder of a corner is told what breaks it and nothing about making one
        Lay(w, "exotics", 4, 20, 6);
        Assert.Equal("", CornerSystem.Take(w, t, house.Id, "exotics")); Assert.StartsWith("Your house has cornered it: you hold 20 of the 30 in the Reach. The price it settles at is 67% higher for it.", CornerSystem.Held(w, t, house.Id, "exotics"));
        // and the screen's model says the same
        var line = Build(tc).Single(g => g.Id == "exotics"); var now = CornerSystem.Standing(w, Content.Market, "exotics");
        Assert.True(line.Cornered && line.CornerMine); Assert.Equal(house.Id, line.CornerHouseId); Assert.Equal(house.Name, line.CornerHouseName); Assert.Equal(now.Top!.SharePct, line.CornerSharePct);
        Assert.Equal(CornerSystem.Held(w, t, house.Id, "exotics"), line.CornerHeld); Assert.Equal(CornerSystem.Take(w, t, house.Id, "exotics"), line.CornerTake);
        Assert.Equal((int)Math.Round(100 * CornerSystem.Premium(now.Top.Units, now.All)), line.PremiumPct);
    }

    [Fact]
    public void ACorner_SurvivesASave_AndIsTheSameEveryTime()
    {
        string Run(out World world)
        {
            var (w, tc) = Start(); Years(w, tc, 2); Lay(w, "exotics", 4, 30, 6); Years(w, tc, 2); world = w;
            return string.Join("|", w.Market!.Goods.Values.OrderBy(g => g.Id, StringComparer.Ordinal).Select(g => $"{g.Id}:{g.Price:R}:{g.Stock:R}:{g.CornerHouse}:{g.CornerShare:R}:{g.Cornered}:{g.CornerSince}:{g.CornerPremium:R}:{g.Cause}"));
        }
        string a = Run(out var w1), b = Run(out _); Assert.Equal(a, b);
        var ex = Good(w1, "exotics"); Assert.True(ex.Cornered); Assert.Equal(Mine(w1).Id, ex.CornerHouse); Assert.True(ex.CornerPremium > 0.5);
        var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(w1)); var lx = Good(loaded, "exotics");
        Assert.Equal(ex.CornerHouse, lx.CornerHouse); Assert.Equal(ex.CornerShare, lx.CornerShare); Assert.Equal(ex.Cornered, lx.Cornered); Assert.Equal(ex.CornerSince, lx.CornerSince); Assert.Equal(ex.CornerPremium, lx.CornerPremium); Assert.Equal(ex.Cause, lx.Cause);
        Assert.Equal(CornerSystem.Word(w1, Content.Trade, Mine(w1).Id, "exotics"), CornerSystem.Word(loaded, Content.Trade, Mine(loaded).Id, "exotics"));
        Assert.Equal(new[] { "exotics" }, CornerSystem.HeldBy(loaded, Mine(loaded).Id).ToArray());
        // a year on, the loaded game and the one that was never closed agree
        var tcA = new TurnController(loaded, Content); loaded.Turn++; tcA.Economy!.Accrue(loaded); tcA.Market.Tick(loaded);
        var tcB = new TurnController(w1, Content); w1.Turn++; tcB.Economy!.Accrue(w1); tcB.Market.Tick(w1);
        Assert.Equal(Good(w1, "exotics").Price, Good(loaded, "exotics").Price, 9); Assert.Equal(Good(w1, "exotics").CornerPremium, Good(loaded, "exotics").CornerPremium, 9);
        // a save from before there were corners loads with none
        Assert.Equal(-1, new GoodState().CornerSince); Assert.Equal("", new GoodState().CornerHouse);
    }
}
