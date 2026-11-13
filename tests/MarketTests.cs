using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The commodity market (docs/ECONOMY_MARKET.md stage A): one price a good, read each turn from what the houses really produced and
/// consumed; steady in peace; moved by war, siege, revolt, raiders and closed ports; a spread, a tariff and an order's own weight on every trade.</summary>
public class MarketTests
{
    private static readonly string Root = ContentLocator.FindContentDir();
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(Root);
    private static (World w, TurnController tc) Start(string scenario = "the_fallen_house", ulong seed = 1) { var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(Root, scenario), seed); return (w, new TurnController(w, Content)); }
    private static CommandContext Ctx(World w) => new(w, WorldBuilder.RngFor(w));
    private static GoodState Good(World w, string id) => w.Market!.Goods[id];
    private static double Ratio(World w, string id) => Good(w, id).Price / Content.Market.Good(id)!.Base;
    /// <summary>Reckon the market for some years with nothing else moving, so only what the test changed is in the price.</summary>
    private static void Years(World w, TurnController tc, int n)
    {
        if (w.Turn < MarketSystem.WarmUpTurns + 6) w.Turn = MarketSystem.WarmUpTurns + 6;   // past the opening years, when the market is still learning what is usual
        for (int i = 0; i < n; i++) { w.Turn++; tc.Economy!.Accrue(w); tc.Market.Tick(w); }
    }
    /// <summary>The holdings that bring most of a good up, largest first: the ones a siege or a revolt must hit to be felt.</summary>
    private static List<Holding> Producers(World w, TurnController tc, string good, int take) =>
        w.Holdings.Values.Select(h => (h, v: tc.Economy!.Compute(w, h.OwnerId).Lines.Where(l => l.Source == h.Name).Sum(l => Math.Max(0, l.Delta.Get(good))))).Where(x => x.v > 0).OrderByDescending(x => x.v).ThenBy(x => x.h.Id, StringComparer.Ordinal).Take(take).Select(x => x.h).ToList();

    [Fact]
    public void TheCatalogLoads_SixGoods_AndEveryWorldOpensItsMarkets()
    {
        Assert.Equal(new[] { "materials", "manpower", "exotics", "helium3", "rare_earths", "isotopes" }, Content.Market.Goods.Select(g => g.Id).ToArray());
        Assert.Equal(6.0, Content.Market.Good("helium3")!.Base); Assert.False(Content.Market.Good("manpower")!.Sell); Assert.Equal("isotope_market", Content.Market.Good("isotopes")!.License);
        Assert.Null(Content.Market.Good("influence")); Assert.Null(Content.Market.Good("credits"));   // influence is never sold
        Assert.Equal(10, Content.Market.Rules.SpreadPct); Assert.Equal(30, Content.Market.Rules.CaravanSpreadPct);
        var (w, _) = Start();
        Assert.NotNull(w.Market); Assert.Equal(6, w.Market!.Goods.Count);
        foreach (var d in Content.Market.Goods) { var g = Good(w, d.Id); Assert.InRange(g.Price, d.Base, d.Base * 2.0001); Assert.True(g.Stock >= d.MinStock); Assert.Single(g.History); }
        Assert.Equal(1.5, Good(w, "materials").Price, 6);   // nothing short: the base price
        // a hand-built world with no controller still has a market the moment anyone asks
        var bare = TestContent.EconomyWorld(); Assert.Null(bare.Market); Assert.Equal(9.0, MarketSystem.Of(bare, Content.Market).Goods["rare_earths"].Price);
    }

    [Fact]
    public void InPeace_NoPriceLeavesItsBand_AndWhatTheReachMakesSteadilyHoldsItsPrice()
    {
        // twelve years of the ledgers alone: no war, no siege, no orders. Worlds still drift (prosperity, unrest), so what they make moves a little
        var (w, tc) = Start(); var before = Content.Market.Goods.ToDictionary(g => g.Id, g => Good(w, g.Id).Price);
        Years(w, tc, 12);
        foreach (var d in Content.Market.Goods)
        {
            var g = Good(w, d.Id); string trace = $"{d.Id}: {before[d.Id]:F2} → {g.Price:F2} [{g.Cause}] hist {string.Join(",", g.History.Select(x => x.ToString("F2")))}";
            Assert.True(g.History.All(x => x >= d.Base * 0.5 && x <= d.Base * 2.0), trace);                                       // half to double: the band
            if (d.Id is "exotics" or "rare_earths" or "isotopes") Assert.True(g.Price >= before[d.Id] * 0.85 && g.Price <= before[d.Id] * 1.15, trace);   // nothing made them move
        }
        Assert.Equal(Content.Market.Rules.HistoryKept, Good(w, "materials").History.Count);
        Assert.False(w.HasFlag("price_spike:exotics"));
    }

    [Fact]
    public void War_LiftsThePrice_AndPeaceBringsItBack()
    {
        var (w, tc) = Start(); Years(w, tc, 2); double calm = Good(w, "helium3").Price, calmIso = Good(w, "isotopes").Price;
        var houses = w.Houses.Keys.Where(h => h != PirateSystem.HouseId).OrderBy(h => h, StringComparer.Ordinal).Take(6).ToList();
        for (int i = 0; i + 1 < houses.Count; i += 2) w.Wars.Add(new War { HouseA = houses[i], HouseB = houses[i + 1], Since = w.Turn });
        Years(w, tc, 3);
        Assert.True(Good(w, "helium3").Price > calm * 1.2, $"{calm} → {Good(w, "helium3").Price}"); Assert.True(Good(w, "isotopes").Price > calmIso * 1.2);
        Assert.Contains("three wars", Good(w, "helium3").Cause); Assert.StartsWith("up ", Good(w, "helium3").Cause);
        double peak = Good(w, "helium3").Price;
        w.Wars.Clear(); Years(w, tc, 1); Assert.Contains("peace", Good(w, "helium3").Cause);
        Years(w, tc, 9);
        Assert.True(Good(w, "helium3").Price < peak); Assert.InRange(Good(w, "helium3").Price, calm * 0.75, calm * 1.15);   // back near where it stood, within ten years
    }

    [Fact]
    public void ASiege_ARevolt_AndRaiders_EachCutSupply_AndTheMarketNamesThem()
    {
        // siege: the three greatest producers of materials are blockaded
        var (w, tc) = Start(); Years(w, tc, 2); double calm = Good(w, "materials").Price; var top = Producers(w, tc, "materials", 4);
        foreach (var h in top) w.Sieges[h.Id] = new Siege { HoldingId = h.Id, BesiegerId = PirateSystem.HouseId, Required = 99 };
        Years(w, tc, 2);
        Assert.True(Good(w, "materials").Price > calm * 1.05, $"{calm} → {Good(w, "materials").Price}"); Assert.Contains("under siege", Good(w, "materials").Cause);
        // revolt: the same worlds rise instead
        var (w2, tc2) = Start(); Years(w2, tc2, 2); calm = Good(w2, "materials").Price;
        foreach (var h in Producers(w2, tc2, "materials", 4)) h.Rebellion = 3;
        Years(w2, tc2, 2);
        Assert.True(Good(w2, "materials").Price > calm * 1.05); Assert.Contains("in revolt", Good(w2, "materials").Cause);
        // raiders: every region's lanes go lawless
        var (w3, tc3) = Start(); Years(w3, tc3, 2); calm = Good(w3, "materials").Price;
        foreach (var r in w3.Galaxy!.Regions) RegionSystem.StateOf(w3, r.Id).Lawlessness = 95;
        Years(w3, tc3, 2);
        Assert.True(Good(w3, "materials").Price > calm * 1.05); Assert.Contains("raiders on the lanes", Good(w3, "materials").Cause);
    }

    [Fact]
    public void ANationsPorts_FeedTheMarket_AndAWarAbroadClosesThem()
    {
        var (w, tc) = Start();
        Assert.Equal(2, Content.Nations.Def("thessar")!.Exports["rare_earths"]);
        double open = tc.Market.Imports(w, "rare_earths"); Assert.True(open >= 3);   // Thessar's two and the Hollow Crown's one
        Assert.Equal(0, tc.Market.Imports(w, "manpower"));                         // the Khanates are hostile from the first day: no ships
        w.Nation("thessar")!.SubduedUntil = w.Turn + 20; Assert.Equal(open + 2, tc.Market.Imports(w, "rare_earths"));   // a tributary ships double
        w.Nation("thessar")!.SubduedUntil = 0; Years(w, tc, 2); double calm = Good(w, "rare_earths").Price;
        w.Nation("thessar")!.AtWar = true; w.Nation("hollow_crown")!.AtWar = true;
        Assert.Equal(0, tc.Market.Imports(w, "rare_earths"));
        Years(w, tc, 2);
        Assert.True(Good(w, "rare_earths").Price > calm); Assert.Contains("no ships from", Good(w, "rare_earths").Cause);
    }

    [Fact]
    public void AnOrder_PaysTheSpread_AndItsOwnWeight_AndCannotBeSplitToDodgeIt()
    {
        var (w, tc) = Start(); var p = w.Protagonist!; var house = w.House(p.HouseId)!; var t = Content.Trade; Years(w, tc, 2); house.Treasury.Credits = 5000;
        var g = Good(w, "materials"); double stock = g.StockRef; Assert.Equal(g.Stock, stock, 6); int half = (int)(stock / 2), quarter = half / 2; Assert.True(quarter >= 4);
        double mid = g.Price, buy1 = MarketSystem.BuyPrice(w, t, house.Id, "materials"), sell1 = MarketSystem.SellPrice(w, t, house.Id, "materials");
        Assert.True(sell1 < mid && mid < buy1);
        int spread = MarketSystem.HasMarket(w, house.Id, t.Holdings) ? 10 : 30; Assert.Equal(mid * (1 + spread / 200.0), buy1, 6); Assert.Equal(mid * (1 - spread / 200.0), sell1, 6);
        // weight: an order for half the stock costs a quarter more a unit than the first unit would
        var whole = MarketSystem.QuoteBuy(w, t, house.Id, "materials", half);
        Assert.Equal(buy1 * (1 + half / (2 * stock)), whole.UnitPrice, 6); Assert.True(whole.UnitPrice > buy1 * 1.2);
        // split in two, the second half is dearer: together they cost what the whole did (within the rounding of two receipts)
        var two = MarketSystem.QuoteBuy(w, t, house.Id, "materials", quarter * 2); int credits = house.Treasury.Credits;
        Assert.True(new CommandExecutor().TryExecute(new BuyGoodCommand(p.Id, "materials", quarter, t), Ctx(w)));
        Assert.True(new CommandExecutor().TryExecute(new BuyGoodCommand(p.Id, "materials", quarter, t), Ctx(w)));
        Assert.InRange(credits - house.Treasury.Credits, two.Credits, two.Credits + 2);
        Assert.Equal(stock - quarter * 2, g.Stock, 6); Assert.Equal(quarter * 2, g.Bought, 6); Assert.Equal(house.Id, g.TopBuyer);
        // the merchants cannot sell what they do not hold
        Assert.StartsWith("the merchants hold only", MarketSystem.QuoteBuy(w, t, house.Id, "materials", (int)stock).Refusal);
        // selling: less than the quote for size, never less than three-tenths of it
        house.Treasury.Materials = 5000; var dump = MarketSystem.QuoteSell(w, t, house.Id, "materials", 4000);
        Assert.True(dump.UnitPrice < sell1); Assert.True(dump.Credits >= (int)(sell1 * 4000 * 0.3));
        Assert.Equal("you hold only 5000", MarketSystem.QuoteSell(w, t, house.Id, "materials", 5001).Refusal);
        // next year the market remembers who emptied it
        house.Treasury.Materials = 0; Years(w, tc, 1);
        Assert.True(Good(w, "materials").Price > mid, $"{mid} → {Good(w, "materials").Price}: {Good(w, "materials").Cause}"); Assert.Contains("heavy buying by " + house.Name, Good(w, "materials").Cause);
        Assert.Equal(0, Good(w, "materials").Bought);
    }

    [Fact]
    public void TheMarket_IsClosedByEmbargo_TaxedByTheCharter_AndLicensedForIsotopes()
    {
        var (w, _) = Start(); var p = w.Protagonist!; var house = w.House(p.HouseId)!; var t = Content.Trade; house.Treasury.Credits = 500; house.Treasury.Manpower = 20; house.Treasury.Isotopes = 3;
        Assert.Equal("hired crews are not sold on the exchanges", new SellGoodCommand(p.Id, "manpower", 1, t).Refusal(Ctx(w)));
        Assert.Null(new BuyGoodCommand(p.Id, "manpower", 2, t).Refusal(Ctx(w)));
        // isotopes: sold freely, bought only under the Senate's licence
        Assert.Null(new SellGoodCommand(p.Id, "isotopes", 1, t).Refusal(Ctx(w)));
        Assert.Equal("weapons-grade isotopes are not sold on any open market", new BuyGoodCommand(p.Id, "isotopes", 1, t).Refusal(Ctx(w)));
        PolicySystem.Enact(w, Content.Policies, PolicySystem.SenateHolder, "arms_market", "licensed");
        Assert.Null(new BuyGoodCommand(p.Id, "isotopes", 1, t).Refusal(Ctx(w)));
        // the trade charter moves every buying price
        double plain = MarketSystem.BuyPrice(w, t, house.Id, "helium3");
        PolicySystem.Enact(w, Content.Policies, PolicySystem.SenateHolder, "trade_charter", "protected");
        Assert.True(MarketSystem.BuyPrice(w, t, house.Id, "helium3") > plain * 1.05);
        // an embargo closes both doors
        w.WorldCounters["embargo:" + house.Id] = w.Turn + 5;
        Assert.Equal("the Senate's embargo closes the exchanges to you", new BuyGoodCommand(p.Id, "helium3", 1, t).Refusal(Ctx(w)));
        Assert.Equal("the Senate's embargo closes the exchanges to you", new SellGoodCommand(p.Id, "isotopes", 1, t).Refusal(Ctx(w)));
    }

    [Fact]
    public void TheMarket_IsDeterministic_SurvivesASave_AndTheStewardsUseIt()
    {
        static string Snapshot(World w) => string.Join("|", w.Market!.Goods.Values.OrderBy(g => g.Id, StringComparer.Ordinal).Select(g => $"{g.Id}:{g.Price:R}:{g.Stock:R}"));
        string Run(out World world)
        {
            var (w, tc) = Start("the_old_lion", 3);
            for (int t = 0; t < 20; t++) { foreach (var i in tc.BeginTurn()) if (tc.Offer(i).FirstOrDefault(o => o.Available) is { } c) tc.Resolve(i, c.Choice.Id); tc.EndTurn(); }
            world = w; return Snapshot(w);
        }
        string a = Run(out var w1), b = Run(out _); Assert.Equal(a, b);
        Assert.True(w1.Counter(MarketSystem.BoughtCounter) > 0); Assert.True(w1.Counter(MarketSystem.SoldCounter) > 0);   // rival stewards trade
        var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(w1)); Assert.Equal(a, Snapshot(loaded));
        Assert.Equal(w1.Market!.Goods["helium3"].History, loaded.Market!.Goods["helium3"].History); Assert.Equal(w1.Market.Goods["helium3"].Cause, loaded.Market.Goods["helium3"].Cause);
        Assert.DoesNotContain(w1.WorldCounters.Keys, k => k.StartsWith("market_buy:") && int.Parse(k.Split(':')[1]) < w1.Turn - 1);   // last year's tallies are swept
    }

    [Fact]
    public void TheCourtModel_ShowsTheMarket()
    {
        var (w, tc) = Start(); tc.BeginTurn(); var m = Praetoria.Core.Presentation.CourtProjection.Build(tc, Content);
        Assert.NotNull(m.Market); Assert.Equal(6, m.Market!.Goods.Count);
        var fuel = m.Market.Goods.Single(g => g.Id == "helium3");
        Assert.True(fuel.BuyUnit > fuel.Price && fuel.SellUnit < fuel.Price); Assert.NotEmpty(fuel.Cause); Assert.NotEmpty(fuel.History);
        Assert.NotNull(m.Market.Goods.Single(g => g.Id == "isotopes").BuyRefusal); Assert.NotNull(m.Market.Goods.Single(g => g.Id == "manpower").SellRefusal);
        Assert.Equal(MarketSystem.HasMarket(w, w.Protagonist!.HouseId, Content.Holdings) ? 10 : 30, m.Market.SpreadPct);
    }
}
