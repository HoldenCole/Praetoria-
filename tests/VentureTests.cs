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
        Assert.Equal(2, b.Building("helium_skimmers")!.BuildTurns);   // works on a deposit two (a great work is not a building: docs/BUILDINGS.md)
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
        var holding = w.HoldingsOf(house.Id).First(h => h.IsPlanet); int free = SystemWorks.WorldSlots(w, Content.Holdings, holding) - SystemWorks.WorldSlotsUsed(w, Content.Holdings, holding); Assert.True(free >= 1);   // its kind's slots, and what development and rank have added
        var options = Content.Holdings.Buildings.Where(b => !b.Megastructure && b.RequiresDeposit == null && b.MinTitleRank == 0 && b.FitsSpecialization(holding.Specialization) && !holding.Buildings.Contains(b.Id)).OrderBy(b => b.Id, StringComparer.Ordinal).ToList();
        free = Math.Max(1, Math.Min(free, Content.Holdings.Spec(holding.Specialization)!.Slots - holding.Buildings.Count)); for (int i = SystemWorks.WorldSlots(w, Content.Holdings, holding) - SystemWorks.WorldSlotsUsed(w, Content.Holdings, holding); i > free; i--) holding.Buildings.Add("filler_" + i);   // (leave only as many free as there are buildings to choose from, and one to be refused)
        Assert.True(options.Count > free);
        for (int i = 0; i < free; i++) Assert.True(tc.Executor.TryExecute(new BuildCommand(w.ProtagonistId, holding.Id, options[i].Id, Content.Holdings, Content.Titles), new CommandContext(w, WorldBuilder.RngFor(w))));
        Assert.Equal(free, ProjectSystem.SlotsUnderway(w, holding.Id));
        Assert.StartsWith("no free slot", new BuildCommand(w.ProtagonistId, holding.Id, options[free].Id, Content.Holdings, Content.Titles).Refusal(new CommandContext(w, WorldBuilder.RngFor(w))));
        // play some years: your buildings stand, and the rival houses' sites have opened and closed
        for (int t = 0; t < 12; t++) { foreach (var i in tc.BeginTurn()) if (tc.Offer(i).FirstOrDefault(o => o.Available) is { } c) tc.Resolve(i, c.Choice.Id); tc.EndTurn(); }
        for (int i = 0; i < free; i++) Assert.Contains(options[i].Id, holding.Buildings);
        Assert.True(w.Counter("projects_done") > free); Assert.True(w.Counter("projects_begun") >= w.Counter("projects_done"));
        var model = Praetoria.Core.Presentation.CourtProjection.Build(tc, Content);
        Assert.NotNull(model.Assets); Assert.All(model.Ventures!, o => Assert.True(o.BuildTurns >= 1 && o.Payback.Length > 0));
        Assert.All(model.Assets!.ForSale, f => Assert.True(f.Ask > 0 || f.Refusal != null));
    }

    // ---------------- a venture can fail (docs/GAPS.md A6) ----------------

    /// <summary>A venture of the player's house on their own world, in a year so bad that it earns nothing: a ruined world, lawless lanes, an embargo.</summary>
    private static Business Losing(World w, VentureSystem vs, string founderId = "")
    {
        var barony = w.Holding("barony")!; barony.Prosperity = 0; RegionSystem.StateOf(w, "rim").Lawlessness = 100; w.WorldCounters["embargo:vega"] = w.Turn + 50;
        foreach (var def in Content.Businesses.Defs.OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            var b = Put(w, "losing", "vega", def.Id, "tessaly"); b.Founded = w.Turn - 5; b.FounderId = founderId;
            if (vs.Earnings(w, b) <= 0) return b;
            w.Businesses.Remove("losing");
        }
        throw new Xunit.Sdk.XunitException("no kind of venture earns nothing even on a ruined, lawless, embargoed world");
    }
    private static void Fires(World w, string eventId)
    {
        var me = w.Protagonist!; w.House(me.HouseId)!.Treasury.Credits = 100; w.House(me.HouseId)!.Treasury.Influence = 9; w.PoolsFor(me.Id).Influence = 9;
        var engine = new Praetoria.Core.Events.EventEngine(Content, WorldBuilder.RngFor(w));
        var found = engine.GatherEligible(w).FirstOrDefault(e => e.def.Id == eventId);
        Assert.True(found.def != null, $"{eventId} is not eligible");
        foreach (var c in found.def!.Choices)
        {
            var again = engine.GatherEligible(w).FirstOrDefault(e => e.def.Id == eventId); if (again.def == null) break;
            engine.Resolve(new Praetoria.Core.Events.FiredEvent(again.def, again.binding), w, c.Id);
        }
    }

    [Fact]
    public void A_venture_that_earns_nothing_three_years_running_is_wound_up_and_its_founder_has_failed_at_it()
    {
        var (w, _, vs) = Reach(); var mine = w.House("vega")!;
        var kin = new Character { Id = "kin", Name = "Test Kin", HouseId = "vega", Age = 30, Alive = true }; w.Characters["kin"] = kin; mine.Members.Add("kin");
        var b = Losing(w, vs, "kin"); Assert.True(vs.Earnings(w, b) <= 0);
        vs.Tick(w); Assert.Equal(1, b.LosingYears); Assert.False(w.HasFlag(VentureSystem.FailingFlag)); Assert.Equal(1, w.Counter(VentureSystem.LossYearsCounter));
        w.Turn++; vs.Tick(w); Assert.Equal(2, b.LosingYears); Assert.True(w.HasFlag(VentureSystem.FailingFlag));   // a year's warning
        int credits = mine.Treasury.Credits, back = vs.WoundUp(b); Assert.Equal(vs.Salvage(b) * VentureSystem.WoundUpPct / 100, back);
        w.Turn++; vs.Tick(w);
        Assert.Null(w.Business("losing")); Assert.Equal(credits + back, mine.Treasury.Credits); Assert.Equal(1, w.Counter(VentureSystem.FailedCounter));
        Assert.True(w.HasFlag(VentureSystem.FailedFlag)); Assert.False(w.HasFlag(VentureSystem.FailingFlag));
        // whoever of the house founded it has failed at something of their own, and it is written of them
        Assert.Contains(VentureSystem.FounderFailedChar, kin.Flags); Assert.True(w.HasFlag(AmbitionSystem.FailedFlag));
        Assert.Contains(kin.Life!.Deeds, d => d.Text.Contains("failed and was wound up"));
        Assert.Contains(w.History, h => h.Text.Contains("is wound up") && h.Text.Contains("Test Kin"));
        Fires(w, "a_venture_fails");
        // the news is a year's
        w.Turn++; vs.Tick(w); Assert.False(w.HasFlag(VentureSystem.FailedFlag)); Assert.DoesNotContain(VentureSystem.FounderFailedChar, kin.Flags);
    }

    [Fact]
    public void Money_put_in_or_a_year_of_profit_wipes_out_the_losing_years_and_a_new_or_a_companys_venture_is_not_judged()
    {
        var (w, _, vs) = Reach(); var mine = w.House("vega")!; var ctx = Ctx(w);
        var b = Losing(w, vs); vs.Tick(w); w.Turn++; vs.Tick(w); Assert.Equal(2, b.LosingYears);
        // the house carries it
        int cost = vs.PropCost(w, b); Assert.True(cost >= VentureSystem.PropFloor);
        mine.Treasury.Credits = cost - 1; Assert.Contains("credits", new PropVentureCommand("self", b.Id, vs).Refusal(ctx));
        mine.Treasury.Credits = cost + 10; new PropVentureCommand("self", b.Id, vs).Execute(ctx);
        Assert.Equal(0, b.LosingYears); Assert.Equal(10, mine.Treasury.Credits); Assert.Equal(1, w.Counter(VentureSystem.ProppedCounter));
        Assert.Contains("paying its way", new PropVentureCommand("self", b.Id, vs).Refusal(ctx));
        // three more years to come right: it is not wound up the year after
        w.Turn++; vs.Tick(w); Assert.NotNull(w.Business("losing")); Assert.Equal(1, b.LosingYears);
        // a year of profit wipes the count out
        w.Holding("barony")!.Prosperity = 80; RegionSystem.StateOf(w, "rim").Lawlessness = 0; w.WorldCounters.Remove("embargo:vega");
        var good = Content.Businesses.Defs.OrderBy(d => d.Id, StringComparer.Ordinal).Select(d => { var x = Put(w, "good_" + d.Id, "vega", d.Id, "tessaly"); x.Founded = w.Turn - 5; return x; }).First(x => vs.Earnings(w, x) > 0);
        good.LosingYears = 2; w.Turn++; vs.Tick(w); Assert.Equal(0, good.LosingYears);
        // its first year is not judged, and a company's venture is the company's affair
        w.WorldCounters["embargo:vega"] = w.Turn + 50; w.Holding("barony")!.Prosperity = 0; RegionSystem.StateOf(w, "rim").Lawlessness = 100;
        var fresh = Put(w, "fresh", "vega", b.Kind, "tessaly"); fresh.Founded = w.Turn + 1; var held = Put(w, "held", "vega", b.Kind, "tessaly"); held.Founded = w.Turn - 5; held.CompanyId = "co_1";
        w.Turn++; vs.Tick(w); Assert.Equal(0, fresh.LosingYears); Assert.Equal(0, held.LosingYears);
        Assert.Contains("company", vs.PropRefusal(w, mine, held));
    }

    // ---------------- a failing venture, beyond its owner and beyond one cause (docs/GAPS.md A7) ----------------

    /// <summary>A venture of another house on the ruined, lawless world, under an embargo of its own: it earns nothing.</summary>
    private static Business LosingFor(World w, VentureSystem vs, string owner, string id)
    {
        w.Holding("barony")!.Prosperity = 0; RegionSystem.StateOf(w, "rim").Lawlessness = 100; w.WorldCounters["embargo:" + owner] = w.Turn + 50;
        foreach (var def in Content.Businesses.Defs.OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            var b = Put(w, id, owner, def.Id, "tessaly"); b.Founded = w.Turn - 5;
            if (vs.Earnings(w, b) <= 0) return b;
            w.Businesses.Remove(id);
        }
        throw new Xunit.Sdk.XunitException("no kind of venture earns nothing there");
    }

    [Fact]
    public void A7_Another_house_that_minds_its_books_carries_a_failing_venture_or_sells_it_before_it_is_wound_up()
    {
        var (w, _, vs) = Reach(); var corwin = w.House("corwin")!; w.Char("corwin_head")!.Skills["economics"] = 2;
        var b = LosingFor(w, vs, "corwin", "theirs"); vs.Tick(w); Assert.Equal(1, b.LosingYears);
        // rich enough: it is carried, and has three more years
        int credits = corwin.Treasury.Credits, cost = vs.PropCost(w, b); w.Turn++; vs.Tick(w);
        Assert.Equal(0, b.LosingYears); Assert.Equal(credits - cost, corwin.Treasury.Credits); Assert.Equal(1, w.Counter(VentureSystem.CarriedByOthersCounter));
        // too poor to carry it: it is sold while it is still worth more than its salvage halved
        w.Turn++; vs.Tick(w); corwin.Treasury.Credits = 5; int fetch = vs.MarketPrice(w, b); Assert.True(fetch > vs.WoundUp(b));
        w.Turn++; vs.Tick(w);
        Assert.Null(w.Business("theirs")); Assert.Equal(5 + fetch, corwin.Treasury.Credits); Assert.Equal(1, w.Counter(VentureSystem.SoldFailingCounter)); Assert.Equal(0, w.Counter(VentureSystem.FailedCounter));
        Assert.False(VentureSystem.BadName(w, "corwin"));
        // every failing venture of a house is carried, sold or wound up: none is left standing in its fourth losing year
        w.Char("corwin_head")!.Skills["economics"] = 0; corwin.Treasury.Credits = 0;
        for (int i = 0; i < 9; i++) LosingFor(w, vs, "corwin", "n" + i);
        for (int y = 0; y < 3; y++) { w.Turn++; vs.Tick(w); }
        Assert.DoesNotContain(w.Businesses.Values, x => x.Id.StartsWith("n"));
        Assert.Equal(9, w.Counter(VentureSystem.SoldFailingCounter) - 1 + w.Counter(VentureSystem.FailedCounter)); Assert.True(w.Counter(VentureSystem.FailedCounter) > 0, "a head who does not mind the books lets some go");
        // the player is never acted for: their own is left to them, with a warning
        var mine = LosingFor(w, vs, "vega", "mine"); w.Turn++; vs.Tick(w); w.Turn++; vs.Tick(w); Assert.Equal(2, mine.LosingYears); Assert.True(w.HasFlag(VentureSystem.FailingFlag));
    }

    [Fact]
    public void A7_A_failure_pays_those_the_house_owes_first_and_leaves_it_a_name_for_bad_business()
    {
        var (w, _, vs) = Reach(); var mine = w.House("vega")!; var b = Losing(w, vs); int back = vs.WoundUp(b); Assert.True(back >= 2);
        mine.Debt = 1; mine.LenderId = "aurelian"; int merchants = PolicySystem.Approval(w, "vega", "merchants"), commons = PolicySystem.Approval(w, "vega", "commons");
        vs.Tick(w); w.Turn++; vs.Tick(w); int credits = mine.Treasury.Credits; w.Turn++; vs.Tick(w);
        Assert.Equal(0, mine.Debt); Assert.Equal("", mine.LenderId); Assert.Equal(credits + back - 1, mine.Treasury.Credits); Assert.Equal(1, w.Counter(VentureSystem.CreditorsCounter));
        Assert.Equal(merchants - VentureSystem.FailMerchants, PolicySystem.Approval(w, "vega", "merchants")); Assert.Equal(commons - VentureSystem.FailCommons, PolicySystem.Approval(w, "vega", "commons"));
        Assert.Contains(w.History, h => h.Text.Contains("to those the house owes"));
        // a name for bad business: for five years buyers bid a tenth less for what the house sells
        Assert.True(VentureSystem.BadName(w, "vega")); w.Holding("barony")!.Prosperity = 80; RegionSystem.StateOf(w, "rim").Lawlessness = 0; w.WorldCounters.Remove("embargo:vega");
        var good = Content.Businesses.Defs.OrderBy(d => d.Id, StringComparer.Ordinal).Select(d => { var x = Put(w, "good_" + d.Id, "vega", d.Id, "tessaly"); x.Founded = w.Turn - 5; return x; }).First(x => vs.Earnings(w, x) > 0 && vs.Bid(w, "aurelian", x) > 0);
        int low = vs.Bid(w, "aurelian", good); w.Turn += VentureSystem.BadNameYears; Assert.False(VentureSystem.BadName(w, "vega")); Assert.True(vs.Bid(w, "aurelian", good) > low);
    }

    [Fact]
    public void A7_A_companys_venture_is_carried_out_of_the_companys_cash_once_and_then_closed()
    {
        var (w, _, vs) = Reach(); var co = new Company { Id = "co_t", Name = "The Test Company", ControllerId = "vega", Shares = 100, Cash = 500 }; co.Holders["vega"] = 100; w.Companies.Add(co);
        var b = Losing(w, vs); b.CompanyId = co.Id; int cost = vs.PropCost(w, b);
        vs.Tick(w); w.Turn++; vs.Tick(w); Assert.Equal(2, b.LosingYears); Assert.False(w.HasFlag(VentureSystem.FailingFlag));
        w.Turn++; vs.Tick(w); Assert.Equal(0, b.LosingYears); Assert.Equal(500 - cost, co.Cash); Assert.Equal(1, w.Counter(VentureSystem.CompanyCarriedCounter));
        co.Cash = 0; int back = vs.WoundUp(b); for (int y = 0; y < 3; y++) { w.Turn++; vs.Tick(w); }
        Assert.Null(w.Business("losing")); Assert.Equal(back, co.Cash); Assert.True(co.Flow < 0); Assert.Equal(1, w.Counter(VentureSystem.CompanyClosedCounter));
        Assert.Equal(0, w.Counter(VentureSystem.FailedCounter)); Assert.False(VentureSystem.BadName(w, "vega")); Assert.Contains(w.History, h => h.Text.Contains("The Test Company closes"));
    }

    [Fact]
    public void A7_When_a_world_is_stormed_some_of_the_enemys_ventures_there_are_sacked()
    {
        var (w, _, vs) = Reach(); var kin = new Character { Id = "kin", Name = "Test Kin", HouseId = "vega", Age = 30, Alive = true }; w.Characters["kin"] = kin; w.House("vega")!.Members.Add("kin");
        string kind = Content.Businesses.Defs.OrderBy(d => d.Id, StringComparer.Ordinal).First().Id;
        for (int i = 0; i < 8; i++) Put(w, "s" + i, "vega", kind, "tessaly").FounderId = "kin";
        Put(w, "neutral", "aurelian", kind, "tessaly"); Put(w, "elsewhere", "vega", kind, "mid1");
        Assert.Equal(0, VentureSystem.Sack(w, "tessaly", "corwin"));   // no war, no sack
        w.Wars.Add(new War { HouseA = "vega", HouseB = "corwin" }); int credits = w.House("corwin")!.Treasury.Credits, home = w.House("vega")!.Treasury.Credits;
        int n = VentureSystem.Sack(w, "tessaly", "corwin");
        Assert.InRange(n, 1, 7); Assert.Equal(8 - n, w.Businesses.Values.Count(b => b.Id.StartsWith("s"))); Assert.NotNull(w.Business("neutral")); Assert.NotNull(w.Business("elsewhere"));
        Assert.Equal(credits + n * VentureSystem.SackPlunder, w.House("corwin")!.Treasury.Credits); Assert.Equal(home, w.House("vega")!.Treasury.Credits);
        Assert.Equal(n, w.Counter(VentureSystem.SackedCounter)); Assert.True(w.HasFlag(VentureSystem.SackedFlag)); Assert.Equal(w.Turn, w.Counter(VentureSystem.FailedFounderKey("kin")));
        Assert.Contains(kin.Life!.Deeds, d => d.Text.Contains("sacked"));
        Fires(w, "a_venture_is_sacked");
        vs.Tick(w); Assert.False(w.HasFlag(VentureSystem.SackedFlag));
    }

    // ---------------- claims at the peace, a company that sells, creditors who foreclose (docs/GAPS.md A8) ----------------

    [Fact]
    public void A8_What_was_sacked_is_billed_at_the_peace_unless_the_claimant_was_beaten()
    {
        var (w, _, _) = Reach(); string kind = Content.Businesses.Defs.OrderBy(d => d.Id, StringComparer.Ordinal).First().Id;
        for (int i = 0; i < 8; i++) Put(w, "s" + i, "vega", kind, "tessaly", level: 2);
        var war = new War { HouseA = "vega", HouseB = "corwin" }; w.Wars.Add(war); int n = VentureSystem.Sack(w, "tessaly", "corwin"); Assert.InRange(n, 1, 7);
        int claim = n * 2 * VentureSystem.SackClaimALevel; Assert.Equal(claim, w.Counter(VentureSystem.ClaimKey("vega", "corwin")));
        // the peace: the taker pays the bill, as far as its treasury goes
        int mine = w.House("vega")!.Treasury.Credits; w.House("corwin")!.Treasury.Credits = claim + 50; var fleets = new FleetSystem(Content.Fleets, Content.Holdings);
        fleets.MakePeace(w, "vega", "corwin");
        Assert.Equal(mine + claim, w.House("vega")!.Treasury.Credits); Assert.Equal(50, w.House("corwin")!.Treasury.Credits); Assert.Equal(0, w.Counter(VentureSystem.ClaimKey("vega", "corwin"))); Assert.Equal(claim, w.Counter(VentureSystem.ClaimsPaidCounter));
        Assert.Contains(w.History, h => h.Text.Contains("for the ventures sacked"));
        // a house beaten badly enough to cede a world presents no bill
        w.WorldCounters[VentureSystem.ClaimKey("vega", "corwin")] = 40; var lost = new War { HouseA = "vega", HouseB = "corwin", ScoreB = FleetSystem.CessionLead }; mine = w.House("vega")!.Treasury.Credits;
        VentureSystem.SettleClaims(w, lost, FleetSystem.CessionLead); Assert.Equal(mine, w.House("vega")!.Treasury.Credits); Assert.Equal(40, w.Counter(VentureSystem.ClaimsForfeitCounter)); Assert.Equal(0, w.Counter(VentureSystem.ClaimKey("vega", "corwin")));
        // a payer who has nothing pays nothing; a bill with no war behind it lapses at the year's reckoning
        w.WorldCounters[VentureSystem.ClaimKey("vega", "corwin")] = 40; w.House("corwin")!.Treasury.Credits = -5; VentureSystem.SettleClaims(w, new War { HouseA = "vega", HouseB = "corwin" }, FleetSystem.CessionLead); Assert.Equal(-5, w.House("corwin")!.Treasury.Credits);
        w.WorldCounters[VentureSystem.ClaimKey("vega", "aurelian")] = 30; var (_, _, vs) = (w, 0, new VentureSystem(new Economy(Content.Holdings, Content.Businesses, Content.Policies), Content.Businesses)); vs.Tick(w);
        Assert.Equal(0, w.Counter(VentureSystem.ClaimKey("vega", "aurelian")));
    }

    [Fact]
    public void A8_A_company_whose_board_minds_its_books_sells_a_failing_venture_rather_than_close_it()
    {
        var (w, _, vs) = Reach(); var co = new Company { Id = "co_t", Name = "The Test Company", ControllerId = "corwin", Shares = 100, Cash = 0 }; co.Holders["corwin"] = 100; w.Companies.Add(co);
        w.Char("corwin_head")!.Skills["economics"] = 2; var b = LosingFor(w, vs, "corwin", "held"); b.CompanyId = co.Id; int price = vs.MarketPrice(w, b); Assert.True(price > vs.WoundUp(b));
        for (int y = 0; y < 3; y++) { w.Turn++; vs.Tick(w); }
        Assert.Null(w.Business("held")); Assert.Equal(price, co.Cash); Assert.Equal(1, w.Counter(VentureSystem.CompanySoldCounter)); Assert.Equal(0, w.Counter(VentureSystem.CompanyClosedCounter));
        // the holder of the world it works takes it at that price, if it is not the player and can pay
        w.Holding("barony")!.OwnerId = "aurelian"; co.Cash = 0; var b2 = LosingFor(w, vs, "corwin", "held2"); b2.CompanyId = co.Id; int credits = w.House("aurelian")!.Treasury.Credits; price = vs.MarketPrice(w, b2);
        for (int y = 0; y < 3; y++) { w.Turn++; vs.Tick(w); }
        Assert.Equal("aurelian", b2.OwnerId); Assert.Equal("", b2.CompanyId); Assert.Equal(0, b2.LosingYears); Assert.Equal(price, co.Cash); Assert.Equal(credits - price, w.House("aurelian")!.Treasury.Credits);
    }

    [Fact]
    public void A8_A_house_that_owes_and_has_nothing_is_foreclosed_on_and_what_is_someones_own_is_not_taken()
    {
        var (w, _, vs) = Reach(); var mine = w.House("vega")!; var lender = w.House("aurelian")!; string[] kinds = Content.Businesses.Defs.Where(d => !d.Lender).OrderBy(d => d.Id, StringComparer.Ordinal).Select(d => d.Id).Take(2).ToArray();
        var kin = new Character { Id = "kin", Name = "Test Kin", HouseId = "vega", Age = 30, Alive = true }; w.Characters["kin"] = kin; mine.Members.Add("kin");
        var own = Put(w, "own", "vega", kinds[0], "tessaly"); own.HeldById = "kin"; own.Founded = w.Turn; var b = Put(w, "houses", "vega", kinds[1], "tessaly"); b.Founded = w.Turn;
        // in debt but solvent: nothing happens
        mine.Debt = 5; mine.LenderId = "aurelian"; mine.Treasury.Credits = 10; vs.Tick(w); Assert.Equal("vega", b.OwnerId); Assert.Equal(0, w.Counter(VentureSystem.ForeclosedCounter));
        // owing and empty: the lender takes the house's venture against the debt, pays over the rest, and leaves the one that is a person's own
        mine.Treasury.Credits = -3; int price = vs.MarketPrice(w, b), theirs = lender.Treasury.Credits; w.Turn++; vs.Tick(w);
        Assert.Equal("aurelian", b.OwnerId); Assert.Equal("vega", own.OwnerId); Assert.Equal(0, mine.Debt); Assert.Equal("", mine.LenderId); Assert.Equal(-3 + price - 5, mine.Treasury.Credits); Assert.Equal(theirs - (price - 5), lender.Treasury.Credits);
        Assert.Equal(1, w.Counter(VentureSystem.ForeclosedCounter)); Assert.Equal(5 + 3, w.Counter(VentureSystem.CreditorsCounter)); Assert.True(VentureSystem.BadName(w, "vega")); Assert.True(w.HasFlag(VentureSystem.ForeclosedFlag));
        Fires(w, "a_venture_is_foreclosed");
        // with nothing left that is the house's, there is nothing to take; with no lender who can take it, it is sold
        mine.Debt = 5; mine.LenderId = "aurelian"; mine.Treasury.Credits = -3; w.Turn++; vs.Tick(w); Assert.Equal(1, w.Counter(VentureSystem.ForeclosedCounter)); Assert.False(w.HasFlag(VentureSystem.ForeclosedFlag));
        var b3 = Put(w, "third", "vega", kinds[1], "mid1"); b3.Founded = w.Turn; mine.LenderId = ""; mine.Debt = 500; mine.Treasury.Credits = -3; price = vs.MarketPrice(w, b3); w.Turn++; vs.Tick(w);
        Assert.Null(w.Business("third")); Assert.Equal(500 - price, mine.Debt); Assert.Equal(-3, mine.Treasury.Credits); Assert.Equal(2, w.Counter(VentureSystem.ForeclosedCounter));
        // no loan at all, but a treasury far enough under: whoever the house owes is paid out of a sale
        void Year(Business keep, int credits) { w.Turn++; keep.Founded = w.Turn; mine.Treasury.Credits = credits; vs.Tick(w); }   // a new venture is not judged, so only its owner's credit is at stake
        var b4 = Put(w, "fourth", "vega", kinds[1], "mid1"); mine.Debt = 0; Year(b4, 5); Year(b4, VentureSystem.ForecloseAt + 1); Assert.NotNull(w.Business("fourth"));   // overdrawn one year: a warning
        Assert.Contains(w.History, h => h.Text.Contains("If it still is next year")); Year(b4, 5); Assert.Equal(0, w.Counter(VentureSystem.OverdrawnKey("vega")));
        price = vs.MarketPrice(w, b4); Year(b4, VentureSystem.ForecloseAt); Assert.Null(w.Business("fourth")); Assert.Equal(VentureSystem.ForecloseAt + price, mine.Treasury.Credits); Assert.Equal(3, w.Counter(VentureSystem.ForeclosedCounter));
        // or simply overdrawn two years running, however little
        var b5 = Put(w, "fifth", "vega", kinds[1], "mid1"); Year(b5, 5); Year(b5, -1); Assert.NotNull(w.Business("fifth")); Year(b5, -1);
        Assert.Null(w.Business("fifth")); Assert.Equal(4, w.Counter(VentureSystem.ForeclosedCounter));
    }

    // ---------------- a pledge, foreclosure beyond a venture, a bill for a blockade (docs/GAPS.md A9) ----------------

    [Fact]
    public void A9_The_head_names_what_the_creditors_take_first_and_they_take_that_and_leave_the_rest()
    {
        var (w, _, vs) = Reach(); var mine = w.House("vega")!; var me = w.Protagonist!; var ctx = Ctx(w); string[] kinds = Content.Businesses.Defs.Where(d => !d.Lender).OrderBy(d => d.Id, StringComparer.Ordinal).Select(d => d.Id).Take(2).ToArray();
        var small = Put(w, "small", "vega", kinds[0], "tessaly"); var big = Put(w, "big", "vega", kinds[1], "tessaly", level: 3); small.Founded = big.Founded = w.Turn + 50;
        Assert.True(vs.MarketPrice(w, big) > vs.MarketPrice(w, small));
        Assert.Equal("no such thing", vs.PledgeRefusal(w, me, "nothing")); Assert.Equal("nothing is pledged", vs.PledgeRefusal(w, me, ""));
        var theirs = Put(w, "theirs", "corwin", kinds[0], "mid1"); Assert.Equal("not a venture of yours", vs.PledgeRefusal(w, me, theirs.Id));
        new PledgeCommand(me.Id, big.Id, vs).Execute(ctx); Assert.Equal(big.Id, mine.PledgedId); Assert.Equal("it is pledged already", vs.PledgeRefusal(w, me, big.Id)); Assert.Equal(1, w.Counter(VentureSystem.PledgedCounter));
        // foreclosed on: the pledged one goes, though it is worth more, and the pledge is spent
        mine.Treasury.Credits = VentureSystem.ForecloseAt; w.Turn++; vs.Tick(w);
        Assert.Null(w.Business("big")); Assert.NotNull(w.Business("small")); Assert.Equal("", mine.PledgedId); Assert.Equal(1, w.Counter(VentureSystem.ForeclosedCounter));
        // the pledge can be withdrawn; and what is someone's own cannot be pledged by the house
        new PledgeCommand(me.Id, small.Id, vs).Execute(ctx); new PledgeCommand(me.Id, "", vs).Execute(ctx); Assert.Equal("", mine.PledgedId);
        var kin = new Character { Id = "kin", Name = "Test Kin", HouseId = "vega", Age = 30, Alive = true }; w.Characters["kin"] = kin; mine.Members.Add("kin"); small.HeldById = "kin";
        Assert.Contains("someone's own", vs.PledgeRefusal(w, me, small.Id));
    }

    [Fact]
    public void A9_With_no_venture_left_the_creditors_take_a_thing_that_is_not_entailed_and_at_the_last_a_world()
    {
        var (w, _, vs) = Reach(); var mine = w.House("vega")!; var me = w.Protagonist!; var lender = w.House("aurelian")!; var lenderHead = w.Char("aurelian_head")!;
        var entailed = new Heirloom { Id = "old", Name = "the old signet", Kind = "signet", HolderId = me.Id, OfHouseId = "vega", Value = 40, Entailed = true };
        var plate = new Heirloom { Id = "plate", Name = "the plate", Kind = "plate", HolderId = me.Id, OfHouseId = "vega", Value = 14 }; var sword = new Heirloom { Id = "sword", Name = "the sword", Kind = "sword", HolderId = me.Id, OfHouseId = "vega", Value = 30 };
        w.Heirlooms.AddRange(new[] { entailed, plate, sword }); Assert.Contains("entailed", vs.PledgeRefusal(w, me, entailed.Id)); Assert.Null(vs.PledgeRefusal(w, me, sword.Id));
        // overdrawn two years with no venture: nothing is taken yet; the third year, the least of what is not entailed is sold
        mine.Treasury.Credits = -1; w.Turn++; vs.Tick(w); mine.Treasury.Credits = -1; w.Turn++; vs.Tick(w); Assert.Equal(3, w.Heirlooms.Count); Assert.Equal(0, w.Counter(VentureSystem.ThingsForeclosedCounter));
        mine.Treasury.Credits = -1; w.Turn++; vs.Tick(w);
        Assert.DoesNotContain(plate, w.Heirlooms); Assert.Contains(sword, w.Heirlooms); Assert.Contains(entailed, w.Heirlooms); Assert.Equal(-1 + 14, mine.Treasury.Credits); Assert.Equal(1, w.Counter(VentureSystem.ThingsForeclosedCounter));
        Assert.True(VentureSystem.BadName(w, "vega")); Assert.True(w.HasFlag(VentureSystem.ForeclosedFlag));
        // against a loan from a named house, the thing goes to that house's head as their own
        mine.Debt = 10; mine.LenderId = "aurelian"; mine.Treasury.Credits = -1; int theirs = lender.Treasury.Credits; w.Turn++; vs.Tick(w);
        Assert.Equal(lenderHead.Id, sword.HolderId); Assert.Equal("aurelian", sword.OfHouseId); Assert.Equal(0, mine.Debt); Assert.Equal(-1 + 30 - 10, mine.Treasury.Credits); Assert.Equal(theirs - 20, lender.Treasury.Credits);
        // an entail is what keeps a thing from them: with only that left, and a loan from a named house, a world goes, but never the last
        w.Holdings["second"] = new Holding { Id = "second", OwnerId = "vega", Name = "Second", Specialization = "agri", SystemId = "empty", Population = 3 };
        mine.Debt = 10; mine.LenderId = "aurelian"; mine.Treasury.Credits = -1; w.Turn++; vs.Tick(w);
        Assert.Contains(entailed, w.Heirlooms); Assert.Equal(me.Id, entailed.HolderId); Assert.Equal("aurelian", w.Holding("second")!.OwnerId); Assert.Equal("vega", w.Holding("second")!.FormerOwnerId); Assert.Equal(0, mine.Debt); Assert.Equal(1, w.Counter(VentureSystem.WorldsForeclosedCounter));
        mine.Debt = 10; mine.LenderId = "aurelian"; mine.Treasury.Credits = -1; w.Turn++; vs.Tick(w); Assert.Equal("vega", w.Holding("barony")!.OwnerId); Assert.Equal(10, mine.Debt); Assert.Equal(1, w.Counter(VentureSystem.WorldsForeclosedCounter));
    }

    [Fact]
    public void A9_A_blockade_that_is_lifted_is_billed_to_whoever_laid_it_and_paid_at_the_peace()
    {
        var (w, _, _) = Reach(); var war = new War { HouseA = "vega", HouseB = "corwin" }; w.Wars.Add(war);
        var siege = new Siege { HoldingId = "barony", BesiegerId = "corwin", Turns = 4, Required = 9 };
        VentureSystem.SiegeLifted(w, siege); int bill = 4 * VentureSystem.SiegeClaimATurn; Assert.Equal(bill, w.Counter(VentureSystem.ClaimKey("vega", "corwin"))); Assert.Equal(bill, w.Counter(VentureSystem.SiegeClaimsCounter));
        // a blockade still standing when peace is made is billed with the rest, and the whole is paid
        w.Sieges["barony"] = new Siege { HoldingId = "barony", BesiegerId = "corwin", Turns = 2, Required = 9 }; int mine = w.House("vega")!.Treasury.Credits; w.House("corwin")!.Treasury.Credits = 500;
        new FleetSystem(Content.Fleets, Content.Holdings).MakePeace(w, "vega", "corwin");
        Assert.Empty(w.Wars); Assert.Empty(w.Sieges); Assert.Equal(mine + bill + 2 * VentureSystem.SiegeClaimATurn, w.House("vega")!.Treasury.Credits); Assert.Equal(0, w.Counter(VentureSystem.ClaimKey("vega", "corwin")));
        // no war, no bill: a siege laid by nobody's enemy is nothing to claim for
        VentureSystem.SiegeLifted(w, new Siege { HoldingId = "barony", BesiegerId = "aurelian", Turns = 5 }); Assert.Equal(0, w.Counter(VentureSystem.ClaimKey("vega", "aurelian")));
    }
}
