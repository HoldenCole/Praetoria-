using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Ventures as assets and building as projects (docs/ECONOMY_MARKET.md stage B): a venture is worth what it earns at this year's
/// prices; houses bid, ask and refuse by rule; the world's holder has first refusal; things take years and are paid for by the year.</summary>
public class VentureTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static World Rings() { var m = typeof(RegionSystemTests).GetMethod("Rings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!; return (World)m.Invoke(null, new object[] { 0 })!; }
    private static CommandContext Ctx(World w) => new(w, TestContent.Rng());
    private static (World w, Economy eco, VentureSystem vs) Reach()
    {
        var w = Rings(); var eco = new Economy(Content.Holdings, Content.Businesses, Content.Policies); var vs = new VentureSystem(eco, Content.Businesses);
        foreach (var h in w.Houses.Values) h.Treasury.Credits = 300;
        return (w, eco, vs);
    }
    private static Business Put(World w, string id, string owner, string kind, string system, int level = 1) { var b = new Business { Id = id, Name = id, OwnerId = owner, Kind = kind, SystemId = system, Level = level }; w.Businesses[id] = b; return b; }

    [Fact]
    public void TheCatalog_GivesEveryVentureABuildTimeAndASector_AndBuildingsTakeYears()
    {
        var v = Content.Businesses; Assert.Equal(14, v.Defs.Count);
        Assert.All(v.Defs, d => { Assert.InRange(d.BuildTurns, 1, 4); Assert.Contains(d.Sector, new[] { "trade", "extraction", "arms", "shipping", "finance", "services" }); });
        Assert.Equal(2, v.Def("arsenal_concern")!.WarCredits); Assert.Equal(3, v.Def("insurance_house")!.PeaceCredits); Assert.Equal(-1, v.Def("insurance_house")!.WarCredits); Assert.Equal(2, v.Def("commodity_warehouse")!.SpikeCredits);
        var b = Content.Holdings;
        Assert.Equal(4, b.Building("orbital_ring")!.BuildTurns); Assert.Equal(2, b.Building("helium_skimmers")!.BuildTurns);   // megastructures four, works on a deposit two
        Assert.All(b.Buildings, x => Assert.InRange(x.BuildTurns, 1, 4));
    }

    [Fact]
    public void AVenture_IsWorthSixYearsOfWhatItEarnsNow_AndNeverLessThanHalfItsBook()
    {
        var (w, eco, vs) = Reach(); var trade = Put(w, "t", "vega", "trading_company", "tessaly");
        int earnings = eco.VentureEarnings(w, trade); Assert.Equal(3, earnings);                      // three credits a level at par prosperity, on its own world: no charter
        Assert.Equal(Math.Max(Content.Businesses.Def("trading_company")!.Value(1) / 2, earnings * 6), vs.Value(w, trade)); Assert.Equal(18, vs.Value(w, trade));
        Assert.Equal(18 * 80 / 100, vs.MarketPrice(w, trade));
        // a skimmer is worth what fuel is worth: the same hulls, a dearer market, a dearer company
        w.Galaxy!.System("tessaly")!.Deposits[Resource.Helium3] = 2; var skim = Put(w, "s", "vega", "skimmer_fleet", "tessaly");
        MarketSystem.Of(w, Content.Market).Goods["helium3"].Price = 6; int calm = vs.Value(w, skim); Assert.Equal(2 * 6 * 6, calm);   // two units a year at six credits, six years
        w.Market!.Goods["helium3"].Price = 15; Assert.Equal(2 * 15 * 6, vs.Value(w, skim));
        // a venture that earns nothing is still worth its salvage
        var idle = Put(w, "a", "vega", "academy_patronage", "tessaly"); w.Market.Goods["helium3"].Price = 6;
        Assert.Equal(Content.Businesses.Def("academy_patronage")!.Value(1) / 2, Math.Min(vs.Value(w, idle), Content.Businesses.Def("academy_patronage")!.Value(1) / 2));
        // wealth counts ventures at value
        eco.Reckon(w); int with = w.House("vega")!.Wealth; w.Businesses.Remove("s"); eco.Reckon(w); Assert.Equal(calm, with - w.House("vega")!.Wealth);
    }

    [Fact]
    public void NeighbouringVentures_Wars_Peace_AndShortages_AllMoveWhatAVentureEarns()
    {
        var (w, eco, _) = Reach(); var trade = Put(w, "t", "vega", "trading_company", "tessaly", level: 3);
        int alone = eco.VentureLine(w, trade).Credits; Assert.Equal(9, alone);
        Put(w, "m", "vega", "mercenary_company", "tessaly"); Assert.Equal(alone * 110 / 100, eco.VentureLine(w, trade).Credits);   // a tenth more beside another kind of business (nine and nine-tenths, in whole credits)
        Put(w, "i", "vega", "insurance_house", "tessaly"); Put(w, "a", "vega", "arsenal_concern", "tessaly"); Put(w, "c", "vega", "commodity_warehouse", "tessaly");
        Assert.Equal(alone * 130 / 100, eco.VentureLine(w, trade).Credits);                                                 // three other kinds at most
        var arsenal = w.Business("a")!; var insurer = w.Business("i")!; var shed = w.Business("c")!;
        int arsenalPeace = eco.VentureLine(w, arsenal).Credits, insurerPeace = eco.VentureLine(w, insurer).Credits, shedCalm = eco.VentureLine(w, shed).Credits;
        w.Wars.Add(new War { HouseA = "aurelian", HouseB = "corwin", Since = 1 });
        Assert.Equal(arsenalPeace + 2, eco.VentureLine(w, arsenal).Credits); Assert.Equal(insurerPeace - 3 - 1, eco.VentureLine(w, insurer).Credits);   // the arsenal's war, the insurer's lost peace
        w.Wars.Clear(); w.WorldFlags.Add("price_spike:helium3"); w.WorldFlags.Add("price_spike:isotopes");
        Assert.Equal(shedCalm + 4, eco.VentureLine(w, shed).Credits);                                                           // two goods spiking, two credits each
    }

    [Fact]
    public void HousesBid_Ask_AndRefuse_ByRule()
    {
        var (w, _, vs) = Reach(); var trade = Put(w, "t", "vega", "trading_company", "mid1");   // Vega's company on Corwin's world
        int value = vs.Value(w, trade); Assert.True(value > 0);
        // the holder of the world bids most; a stranger bids less; a house that cannot keep a reserve bids nothing
        int corwin = vs.Bid(w, "corwin", trade), aurelian = vs.Bid(w, "aurelian", trade);
        Assert.Equal(value * (85 + 25) / 100, corwin); Assert.Equal(value * 85 / 100, aurelian);
        Assert.Equal("corwin", vs.Bidders(w, trade)[0].house.Id);
        w.House("aurelian")!.Treasury.Credits = aurelian + VentureSystem.Reserve - 1; Assert.Equal(0, vs.Bid(w, "aurelian", trade)); w.House("aurelian")!.Treasury.Credits = 300;
        w.Wars.Add(new War { HouseA = "vega", HouseB = "aurelian", Since = 1 }); Assert.Equal(0, vs.Bid(w, "aurelian", trade)); w.Wars.Clear();
        // asking: a premium to part with it, more on its own world, less in distress, nothing to a house it hates
        var theirs = Put(w, "c", "corwin", "trading_company", "mid1"); int theirValue = vs.Value(w, theirs);
        Assert.Equal("you already have one here", vs.Ask(w, theirs, "vega").refusal);
        w.Businesses.Remove("t");
        Assert.Equal((theirValue * (130 + 30) / 100, (string?)null), vs.Ask(w, theirs, "vega"));
        w.House("corwin")!.Debt = 10; Assert.Equal(theirValue * (95 + 30) / 100, vs.Ask(w, theirs, "vega").price); w.House("corwin")!.Debt = 0;
        w.Relationship("corwin_head", "self").Disposition = -40; Assert.Equal("House Corwin will not sell to you at any price", vs.Ask(w, theirs, "vega").refusal);
        w.Relationship("corwin_head", "self").Disposition = 40; Assert.Equal(theirValue * (130 + 30 - 10) / 100, vs.Ask(w, theirs, "vega").price);
    }

    [Fact]
    public void Sales_MoveTheVentureAndTheCoin_AndTheWorldsHolderHasFirstRefusal()
    {
        var (w, _, vs) = Reach(); var ex = new CommandExecutor();
        // you buy Corwin's company on Corwin's world at its asking price
        var theirs = Put(w, "c", "corwin", "trading_company", "mid1"); int ask = vs.Ask(w, theirs, "vega").price;
        w.House("vega")!.Treasury.Credits = ask - 1; Assert.Equal($"needs {ask} credits", new BuyVentureCommand("self", "c", vs).Refusal(Ctx(w)));
        w.House("vega")!.Treasury.Credits = 300; int corwinBefore = w.House("corwin")!.Treasury.Credits;
        Assert.True(ex.TryExecute(new BuyVentureCommand("self", "c", vs), Ctx(w)));
        Assert.Equal("vega", theirs.OwnerId); Assert.Equal(300 - ask, w.House("vega")!.Treasury.Credits); Assert.Equal(corwinBefore + ask, w.House("corwin")!.Treasury.Credits);
        Assert.True(w.HasFlag(VentureSystem.BoughtFlag)); Assert.Equal(1, w.Counter(VentureSystem.SoldCounter)); Assert.Contains(w.History, h => h.Text.Contains("House Vega buys the c from House Corwin"));
        // you sell it on to Aurelian at Aurelian's bid — but it works Corwin's world, and Corwin matches the price first
        int bid = vs.Bid(w, "aurelian", theirs); Assert.True(bid > 0);
        Assert.Equal("corwin", vs.FirstRefuser(w, theirs, "aurelian", bid)!.Id);
        Assert.True(ex.TryExecute(new SellVentureCommand("self", "c", "aurelian", vs), Ctx(w)));
        Assert.Equal("corwin", theirs.OwnerId); Assert.Equal(300 - ask + bid, w.House("vega")!.Treasury.Credits); Assert.Contains(w.History, h => h.Text.Contains("takes up its first refusal"));
        // a company on your own world: no one has first refusal, and the open market breaks it up for four-fifths
        var mine = Put(w, "m", "vega", "trading_company", "tessaly"); int market = vs.MarketPrice(w, mine); int credits = w.House("vega")!.Treasury.Credits;
        Assert.Null(vs.FirstRefuser(w, mine, null, market));
        Assert.True(ex.TryExecute(new SellVentureCommand("self", "m", "", vs), Ctx(w)));
        Assert.Null(w.Business("m")); Assert.Equal(credits + market, w.House("vega")!.Treasury.Credits);
        Assert.Equal("not yours", new SellVentureCommand("self", "c", "", vs).Refusal(Ctx(w)));
        Assert.Equal("House Aurelian will not buy it", new SellVentureCommand("corwin_head", "c", "aurelian", vs).Refusal(Ctx(SetBroke(w, "aurelian"))));
    }
    private static World SetBroke(World w, string house) { w.House(house)!.Treasury.Credits = 0; return w; }

    [Fact]
    public void AStewardInDistress_OffersYouFirst_WhereItIsDue_AndTheOfferLapses()
    {
        var (w, _, vs) = Reach(); var ex = new CommandExecutor();
        var onYours = Put(w, "c", "corwin", "trading_company", "tessaly");   // Corwin's company on YOUR world
        Assert.True(ex.TryExecute(new SellVentureCommand("corwin_head", "c", SellVentureCommand.Best, vs), Ctx(w)));
        var offer = Assert.Single(w.Offers); Assert.Equal("vega", offer.ToHouseId); Assert.Equal("corwin", onYours.OwnerId); Assert.True(w.HasFlag(VentureSystem.OfferedFlag));
        Assert.Equal(Math.Max(vs.Salvage(onYours), vs.Value(w, onYours) * 90 / 100), offer.Price); Assert.Contains("first refusal", offer.Reason);
        // not taken: it lapses after its year, and the next time the steward sells it goes to the best bidder
        w.Turn += 2; vs.Tick(w); Assert.Empty(w.Offers); Assert.False(w.HasFlag(VentureSystem.OfferedFlag));
        Assert.True(ex.TryExecute(new SellVentureCommand("corwin_head", "c", SellVentureCommand.Best, vs), Ctx(w)));
        Assert.Equal("aurelian", onYours.OwnerId);
        // taken: the venture is yours at the offered price
        var again = Put(w, "d", "corwin", "mercenary_company", "tessaly");
        Assert.True(ex.TryExecute(new SellVentureCommand("corwin_head", "d", SellVentureCommand.Best, vs), Ctx(w))); var o2 = Assert.Single(w.Offers);
        int credits = w.House("vega")!.Treasury.Credits;
        Assert.True(ex.TryExecute(new AcceptVentureOfferCommand("self", o2.Id, vs), Ctx(w)));
        Assert.Equal("vega", again.OwnerId); Assert.Equal(credits - o2.Price, w.House("vega")!.Treasury.Credits); Assert.Empty(w.Offers);
        Assert.Equal("no such offer", new AcceptVentureOfferCommand("self", o2.Id, vs).Refusal(Ctx(w)));
    }

    [Fact]
    public void AProject_IsPaidByTheYear_Stalls_Pauses_Rushes_AndIsAbandonedForHalf()
    {
        var (w, _, _) = Reach(); var ex = new CommandExecutor(); var v = Content.Businesses; var vega = w.House("vega")!; vega.Treasury.Materials = 100;
        // a shipping line takes two years: 14 credits and 6 materials, half now and half next year
        var def = v.Def("shipping_line")!; Assert.Equal(2, def.BuildTurns);
        w.Galaxy!.Lanes.Add(new Praetoria.Core.Galaxy.Hyperlane { A = "tessaly", B = "prime" }); w.Galaxy.Lanes.Add(new Praetoria.Core.Galaxy.Hyperlane { A = "tessaly", B = "mid1" });   // a hub of three lanes
        var found = new FoundBusinessCommand("self", "shipping_line", "tessaly", v, Content.Holdings, Content.Titles);
        vega.Treasury.Credits = 6; Assert.Equal("needs 7 credits, 3 materials to begin", found.Refusal(Ctx(w)));
        vega.Treasury.Credits = 100; Assert.True(ex.TryExecute(found, Ctx(w)));
        var p = Assert.Single(w.Projects); Assert.Equal(Project.Venture, p.Kind); Assert.Equal(2, p.TurnsLeft); Assert.Equal(93, vega.Treasury.Credits); Assert.Equal(97, vega.Treasury.Materials);
        Assert.Null(w.Business(FoundBusinessCommand.IdFor("vega", "shipping_line", "tessaly"))); Assert.Equal("already being founded here", found.Refusal(Ctx(w)));
        // the next instalment cannot be met: the site stalls and nothing moves
        vega.Treasury.Credits = 0; w.Turn++; ProjectSystem.Tick(w, Content.Holdings, v);
        Assert.True(p.Stalled); Assert.Equal(2, p.TurnsLeft); Assert.True(w.HasFlag(ProjectSystem.StalledFlag));
        // paused: nothing is charged and nothing moves, even with the coin
        vega.Treasury.Credits = 100; Assert.True(ex.TryExecute(new PauseProjectCommand("self", p.Id), Ctx(w)));
        w.Turn++; ProjectSystem.Tick(w, Content.Holdings, v); Assert.Equal(2, p.TurnsLeft); Assert.Equal(100, vega.Treasury.Credits);
        Assert.True(ex.TryExecute(new PauseProjectCommand("self", p.Id), Ctx(w)));
        // resumed: the second half is paid and a year is left; then it opens
        w.Turn++; ProjectSystem.Tick(w, Content.Holdings, v); Assert.Equal(1, p.TurnsLeft); Assert.Equal(93, vega.Treasury.Credits); Assert.Equal(14, p.Paid.Credits);
        w.Turn++; ProjectSystem.Tick(w, Content.Holdings, v);
        Assert.Empty(w.Projects); Assert.NotNull(w.Business(FoundBusinessCommand.IdFor("vega", "shipping_line", "tessaly"))); Assert.True(w.HasFlag(ProjectSystem.DoneFlag));
        // enlarging it is a project too; rushed, it is done next year for the rest and a quarter more
        var biz = w.Business(FoundBusinessCommand.IdFor("vega", "shipping_line", "tessaly"))!;
        Assert.True(ex.TryExecute(new ExpandBusinessCommand("self", biz.Id, v), Ctx(w))); var e = Assert.Single(w.Projects); Assert.Equal(1, biz.Level); Assert.Equal("already being enlarged", new ExpandBusinessCommand("self", biz.Id, v).Refusal(Ctx(w)));
        int owed = ProjectSystem.Remaining(e).Credits, premium = ProjectSystem.RushPremium(w, e); Assert.True(premium > 0); int before = vega.Treasury.Credits;
        Assert.True(ex.TryExecute(new RushProjectCommand("self", e.Id), Ctx(w))); Assert.Equal(before - owed - premium, vega.Treasury.Credits); Assert.Equal(1, e.TurnsLeft);
        Assert.Equal("done next year as it is", new RushProjectCommand("self", e.Id).Refusal(Ctx(w)));
        w.Turn++; ProjectSystem.Tick(w, Content.Holdings, v); Assert.Equal(2, biz.Level);
        // abandoned: half of what was paid comes back
        Assert.True(ex.TryExecute(new ExpandBusinessCommand("self", biz.Id, v), Ctx(w))); var a = Assert.Single(w.Projects); before = vega.Treasury.Credits;
        Assert.True(ex.TryExecute(new AbandonProjectCommand("self", a.Id), Ctx(w))); Assert.Empty(w.Projects); Assert.Equal(before + a.Paid.Credits / 2, vega.Treasury.Credits); Assert.Equal(2, biz.Level);
    }

    [Fact]
    public void ABuildingUnderWay_HoldsItsSlot_AndRivalStewardsBuildByTheYearToo()
    {
        var root = ContentLocator.FindContentDir(); var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, "the_circling_heir"), 1); var tc = new TurnController(w, Content);
        var house = w.House(w.Protagonist!.HouseId)!; house.Treasury.Credits = 500; house.Treasury.Materials = 500;
        var holding = w.HoldingsOf(house.Id).First(h => h.IsPlanet); var spec = Content.Holdings.Spec(holding.Specialization)!; int free = spec.Slots - holding.Buildings.Count; Assert.True(free >= 1);
        var options = Content.Holdings.Buildings.Where(b => !b.Megastructure && b.RequiresDeposit == null && b.MinTitleRank == 0 && b.FitsSpecialization(holding.Specialization) && !holding.Buildings.Contains(b.Id)).OrderBy(b => b.Id, StringComparer.Ordinal).ToList();
        Assert.True(options.Count > free);
        for (int i = 0; i < free; i++) Assert.True(tc.Executor.TryExecute(new BuildCommand(w.ProtagonistId, holding.Id, options[i].Id, Content.Holdings, Content.Titles), new CommandContext(w, WorldBuilder.RngFor(w))));
        Assert.Equal(free, ProjectSystem.SlotsUnderway(w, holding.Id));
        Assert.Equal("no free slot", new BuildCommand(w.ProtagonistId, holding.Id, options[free].Id, Content.Holdings, Content.Titles).Refusal(new CommandContext(w, WorldBuilder.RngFor(w))));
        // play some years: your buildings stand, and the rival houses' sites have opened and closed
        for (int t = 0; t < 12; t++) { foreach (var i in tc.BeginTurn()) if (tc.Offer(i).FirstOrDefault(o => o.Available) is { } c) tc.Resolve(i, c.Choice.Id); tc.EndTurn(); }
        for (int i = 0; i < free; i++) Assert.Contains(options[i].Id, holding.Buildings);
        Assert.True(w.Counter("projects_done") > free); Assert.True(w.Counter("projects_begun") >= w.Counter("projects_done"));
        var model = Praetoria.Core.Presentation.CourtProjection.Build(tc, Content);
        Assert.NotNull(model.Assets); Assert.All(model.Ventures!, o => Assert.True(o.BuildTurns >= 1 && o.Payback.Length > 0));
        Assert.All(model.Assets!.ForSale, f => Assert.True(f.Ask > 0 || f.Refusal != null));
    }
}
