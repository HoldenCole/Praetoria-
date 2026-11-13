using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Galaxy;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The strategic three (GDD §17 "not every world has everything"): deposits, the buildings and ventures that work them, the
/// fuel a navy burns, the open market and its gates, and the conditions content reads.</summary>
public class StrategicResourceTests
{
    private static World Rings(int unrest) { var m = typeof(RegionSystemTests).GetMethod("Rings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!; return (World)m.Invoke(null, new object[] { unrest })!; }

    private static HoldingCatalog Catalog() => new(
        new[] { new HoldingSpec { Id = "agri", Name = "Agri-World", BaseYield = new Resources { Manpower = 3, Credits = 2 }, Upkeep = new Resources { Credits = 1 }, Slots = 3, PopGrowth = 2 },
                new HoldingSpec { Id = "belt_claim", Name = "Belt Claim", BaseYield = new Resources(), Slots = 0 } },
        new[] { new BuildingDef { Id = "skimmers", Name = "Helium Skimmers", RequiresDeposit = Resource.Helium3, Cost = new Resources { Credits = 4 }, Yield = new Resources { Helium3 = 2 }, Upkeep = new Resources { Credits = 1 } },
                new BuildingDef { Id = "works", Name = "Isotope Works", RequiresDeposit = Resource.Isotopes, Cost = new Resources { Credits = 4 }, Yield = new Resources { Isotopes = 1 } },
                new BuildingDef { Id = "array", Name = "Sensor Array", Cost = new Resources { Credits = 2, RareEarths = 2 }, Yield = new Resources { Influence = 1 } } });

    private static BusinessCatalog Ventures() => new(new[] {
        new BusinessDef { Id = "skimmer_fleet", Name = "Skimmer Fleet", Cost = new Resources { Credits = 6 }, Yield = new Resources { Helium3 = 1 }, RequiresDeposit = Resource.Helium3, Charter = 1 } });

    private static World Deposits(out HoldingCatalog cat, out BusinessCatalog ven)
    {
        var w = Rings(0); cat = Catalog(); ven = Ventures();
        w.Galaxy!.System("tessaly")!.Deposits[Resource.Helium3] = 3;      // the barony's system: a rich giant
        w.Galaxy!.System("mid1")!.Deposits[Resource.Isotopes] = 1;        // Corwin's world: a thin isotope seam
        w.Galaxy!.System("empty")!.Belt = 2; w.Galaxy!.System("empty")!.Deposits[Resource.RareEarths] = 2;
        w.House("vega")!.Treasury.Credits = 40;
        return w;
    }

    [Fact]
    public void ADepositBuilding_NeedsTheDeposit_AndYieldsByRichness()
    {
        var w = Deposits(out var cat, out var ven); var ctx = new CommandContext(w, TestContent.Rng()); var ex = new CommandExecutor();
        Assert.False(new BuildCommand("self", "barony", "works", cat).CanExecute(ctx));            // no isotopes under Tessaly
        Assert.True(ex.TryExecute(new BuildCommand("self", "barony", "skimmers", cat), ctx));
        var eco = new Economy(cat, ven); var lg = eco.Compute(w, "vega");
        Assert.Equal(6, lg.Net.Helium3);                                                            // 2 a level × richness 3
        Assert.Contains(Resource.Helium3, Economy.Deposits(w, "vega")); Assert.Equal(1, Economy.Producers(w, Resource.Helium3, cat, ven));
        Assert.False(new BuildCommand("self", "barony", "array", cat).CanExecute(ctx));             // wants two rare earths the house has not got
        w.House("vega")!.Treasury.RareEarths = 2; Assert.True(new BuildCommand("self", "barony", "array", cat).CanExecute(ctx));
    }

    [Fact]
    public void ABeltClaim_BringsUpRareEarths_AndAVentureNeedsItsDeposit()
    {
        var w = Deposits(out var cat, out var ven); var ctx = new CommandContext(w, TestContent.Rng());
        w.Holdings["claim"] = new Holding { Id = "claim", OwnerId = "vega", Name = "Empty Claim", Specialization = "belt_claim", SystemId = "empty", Kind = Holding.Claim };
        var lg = new Economy(cat, ven).Compute(w, "vega");
        Assert.Equal(1, lg.Net.RareEarths); Assert.Equal(1, Economy.Producers(w, Resource.RareEarths, cat, ven));   // (richness 2 + 1) / 2
        Assert.Equal("needs a helium-3 deposit", new FoundBusinessCommand("self", "skimmer_fleet", "prime", ven, cat).Refusal(ctx));
        Assert.Null(new FoundBusinessCommand("self", "skimmer_fleet", "tessaly", ven, cat).Refusal(ctx));
    }

    [Fact]
    public void TheFleetBurnsFuel_AndDryTanks_HoldItAtAnchor()
    {
        var w = Rings(0); var rules = new FleetRules(); var fs = new FleetSystem(rules, TestContent.EconomyCatalog());
        w.Fleets["v1"] = new Fleet { Id = "v1", Name = "Vega Line", OwnerId = "vega", SystemId = "tessaly", Strength = 3 };
        Assert.Equal(1, fs.FuelBill(w, "vega")); Assert.Equal(0, FleetSystem.FuelBillFor(w, "corwin", 3));   // three hulls burn one; no navy, no bill
        w.House("vega")!.Treasury.Helium3 = 1; w.House("vega")!.Treasury.Credits = 50;
        fs.Tick(w);
        Assert.Equal(0, w.House("vega")!.Treasury.Helium3); Assert.False(FleetSystem.Unfuelled(w, "vega")); Assert.False(w.HasFlag("fuel_short"));
        var ctx = new CommandContext(w, TestContent.Rng());
        Assert.True(new MoveFleetCommand("self", "v1", "mid1", rules).CanExecute(ctx));
        int readiness = w.Fleets["v1"].Readiness;
        fs.Tick(w);                                                                                 // nothing left to burn
        Assert.True(FleetSystem.Unfuelled(w, "vega")); Assert.True(w.HasFlag("fuel_short")); Assert.Equal(1, w.Counter(FleetSystem.FuelShortKey("vega")));
        Assert.True(w.Fleets["v1"].Readiness < readiness);
        Assert.False(new MoveFleetCommand("self", "v1", "mid1", rules).CanExecute(ctx));
        Assert.Contains(w.History, h => h.Text.Contains("run dry"));
        w.House("vega")!.Treasury.Helium3 = 5; fs.Tick(w);
        Assert.False(FleetSystem.Unfuelled(w, "vega")); Assert.False(w.HasFlag("fuel_short")); Assert.Equal(4, w.House("vega")!.Treasury.Helium3);
    }

    [Fact]
    public void TheMarket_SellsFuelAndRareEarths_AndNeverIsotopes_WithoutALicence()
    {
        var w = Deposits(out _, out _); var ctx = new CommandContext(w, TestContent.Rng()); var ex = new CommandExecutor();
        var trade = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir()).Trade;
        var buy = new BuyGoodCommand("self", Resource.Helium3, 2, trade);
        w.House("vega")!.Treasury.Credits = 5; Assert.StartsWith("needs", buy.Refusal(ctx));
        w.House("vega")!.Treasury.Credits = 40; int cost = buy.Cost(w); Assert.InRange(cost, 12, 20);   // two units near the base of six, the caravaneers' spread on top
        Assert.True(ex.TryExecute(buy, ctx));
        Assert.Equal(2, w.House("vega")!.Treasury.Helium3); Assert.Equal(40 - cost, w.House("vega")!.Treasury.Credits); Assert.Equal(2, w.Counter("procured"));
        Assert.Equal("weapons-grade isotopes are not sold on any open market", new BuyGoodCommand("self", Resource.Isotopes, 1, trade).Refusal(ctx));
        Assert.Equal("not a thing the exchanges deal in", new BuyGoodCommand("self", Resource.Credits, 1, trade).Refusal(ctx));
    }

    [Fact]
    public void WealthAndDepositConditions_GateContent()
    {
        var w = Deposits(out var cat, out var ven);
        var db = TestContent.Db(Array.Empty<EventDef>()); var ctx = new EvalContext(w, new Binding("self"), TestContent.Rng(), db);
        foreach (var h in w.Houses.Values) h.Wealth = 10; w.House("vega")!.Wealth = 150;             // the richest of three: opulent by rank
        Assert.True(new WealthCondition("self", CompareOp.Gte, "rich").Evaluate(ctx));
        Assert.Equal("opulent", Wealth.TierFor(w, "vega")); Assert.Equal("comfortable", Wealth.TierFor(w, "corwin"));   // third of three, after Aurelian on the tie
        w.House("vega")!.Wealth = -5; Assert.Equal("destitute", Wealth.TierFor(w, "vega")); Assert.False(new WealthCondition("self", CompareOp.Gte, "poor").Evaluate(ctx));
        Assert.True(new DepositCondition("self", Resource.Helium3, true).Evaluate(ctx));            // Tessaly's giant
        Assert.False(new DepositCondition("self", Resource.Isotopes, true).Evaluate(ctx));
        Assert.True(new DepositCondition("self", Resource.Isotopes, false).Evaluate(ctx));
        new Economy(cat, ven).Reckon(w);
        Assert.True(w.HasFlag("deposit:helium3")); Assert.False(w.HasFlag("strategic:helium3")); Assert.Equal(1, w.Counter("deposits_held"));
        w.House("vega")!.Treasury.Helium3 = 1; new Economy(cat, ven).Reckon(w); Assert.True(w.HasFlag("strategic:helium3"));
    }

    [Fact]
    public void StrategicStocks_CountTowardWealth_AndSurviveASave()
    {
        var w = Deposits(out var cat, out var ven);
        var h = w.House("vega")!; h.Treasury.Helium3 = 4; h.Treasury.RareEarths = 2; h.Treasury.Isotopes = 1;
        int plain = Wealth.Of(w, h, cat, ven); h.Treasury.Isotopes = 0; Assert.Equal(8, plain - Wealth.Of(w, h, cat, ven));   // a unit of isotopes at two-thirds of the market's twelve
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        Assert.Equal(4, back.House("vega")!.Treasury.Helium3); Assert.Equal(2, back.House("vega")!.Treasury.RareEarths);
        Assert.Equal(3, back.Galaxy!.System("tessaly")!.Deposit(Resource.Helium3));
    }

    [Fact]
    public void TheReach_HasDepositsWhereTheGalaxyPutThem_AndTheHeavyHullsWantThem()
    {
        var root = ContentLocator.FindContentDir(); var content = ContentLoader.LoadFromDirectory(root);
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, "the_fallen_house"), 1); var g = w.Galaxy!;
        Assert.Equal(9, g.Systems.Values.Count(s => s.Deposit(Resource.Helium3) > 0));
        Assert.Equal(6, g.Systems.Values.Count(s => s.Deposit(Resource.RareEarths) > 0));
        Assert.Equal(4, g.Systems.Values.Count(s => s.Deposit(Resource.Isotopes) > 0));
        Assert.All(g.Systems.Values.Where(s => s.Deposit(Resource.Helium3) > 0), s => Assert.Contains(s.Bodies, b => b.Class == "gas"));   // helium-3 is skimmed from giants
        Assert.All(g.Systems.Values.Where(s => s.Deposit(Resource.RareEarths) > 0), s => Assert.True(s.Belt > 0));                       // rare earths ride in belts
        Assert.Equal(1, content.ShipClasses.Resolve("cruiser").Cost.RareEarths); Assert.Equal(2, content.ShipClasses.Resolve("battleship").Cost.Isotopes);
        Assert.NotNull(content.Holdings.Building("helium_skimmers")); Assert.Equal(Resource.Helium3, content.Holdings.Building("helium_skimmers")!.RequiresDeposit);
        Assert.Equal(Resource.Isotopes, content.Businesses.Def("enrichment_concern")!.RequiresDeposit);
        Assert.Equal(3, content.Fleets.FuelHullsPerUnit);
        Assert.NotNull(content.Market.Good(Resource.Helium3)); Assert.Equal("isotope_market", content.Market.Good(Resource.Isotopes)!.License);   // fuel is traded freely; isotopes only under the Senate's licence
        Assert.All(w.Houses.Values.Where(h => w.Fleets.Values.Where(f => f.OwnerId == h.Id).Sum(f => f.Strength) >= 2), h => Assert.True(h.Treasury.Helium3 >= 14));   // tanks start part full
    }
}
