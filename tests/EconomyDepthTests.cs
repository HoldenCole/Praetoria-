using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Galaxy;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The depth pass on the economy (GDD §17): prosperity, investment, belt claims, ventures and charters, loans,
/// house wealth. Every rule here is RNG-free, so the assertions are exact.</summary>
public class EconomyDepthTests
{
    private static CommandContext Ctx(World w) => new(w, TestContent.Rng());

    private static BusinessCatalog Ventures() => new(new[]
    {
        new BusinessDef { Id = "trading_company", Name = "Trading Company", Cost = new Resources { Credits = 12, Materials = 4 }, Yield = new Resources { Credits = 4 }, ExpandCost = new Resources { Credits = 10 }, MaxLevel = 3, Charter = 1, RequiresPopulated = true },
        new BusinessDef { Id = "mining_concern", Name = "Mining Concern", Cost = new Resources { Credits = 10, Materials = 6 }, Yield = new Resources { Materials = 3 }, MaxLevel = 2, Charter = 1, RequiresBelt = true },
        new BusinessDef { Id = "merchant_bank", Name = "Merchant Bank", Cost = new Resources { Credits = 24 }, Yield = new Resources { Credits = 2 }, FlatYield = new Resources { Influence = 1 }, Lender = true, RequiresOwnWorld = true }
    });

    private static HoldingCatalog Catalog() => new(
        new[]
        {
            new HoldingSpec { Id = "agri", Name = "Agri-World", BaseYield = new Resources { Manpower = 3, Credits = 2 }, Upkeep = new Resources { Credits = 1 }, Slots = 2, PopGrowth = 2 },
            new HoldingSpec { Id = StakeClaimCommand.Spec, Name = "Belt Claim", BaseYield = new Resources { Materials = 2 }, Upkeep = new Resources { Credits = 1 } }
        },
        new[] { new BuildingDef { Id = "farm", Name = "Farm", Requires = "agri", Cost = new Resources { Materials = 5 }, Yield = new Resources { Manpower = 2, Credits = 1 } } });

    /// <summary>Two houses, two systems joined by a lane, Vega's barony in the first; the second has a rich belt and Corwin's world.</summary>
    private static World Reach(int credits = 30, int materials = 10)
    {
        var w = TestContent.EconomyWorld(credits, materials);
        w.Houses["corwin"] = new House { Id = "corwin", Name = "House Corwin", Treasury = new Resources { Credits = 10, Materials = 5 } };
        w.Characters["corwin_head"] = new Character { Id = "corwin_head", Name = "Lucan", HouseId = "corwin", Age = 40, Alive = true };
        w.Houses["corwin"].Members.Add("corwin_head"); w.Pools["corwin_head"] = ActionPools.ForNpc();
        var g = new Galaxy { Id = "g", Name = "G" };
        g.Systems["tessaly"] = new StarSystem { Id = "tessaly", Name = "Tessaly", PlanetClass = "terran" };
        g.Systems["ember"] = new StarSystem { Id = "ember", Name = "Ember", PlanetClass = "barren", Belt = 3 };
        g.Systems["far"] = new StarSystem { Id = "far", Name = "Far", Belt = 1 };
        g.Lanes.Add(new Hyperlane { A = "tessaly", B = "ember" }); g.Lanes.Add(new Hyperlane { A = "ember", B = "far" });
        w.Galaxy = g;
        w.Holding("barony")!.SystemId = "tessaly"; w.Holding("barony")!.GovernorId = "self";
        w.Holdings["corwin_world"] = new Holding { Id = "corwin_world", OwnerId = "corwin", Name = "Ember Hold", Specialization = "agri", SystemId = "ember", Population = 10, GovernorId = "corwin_head" };
        return w;
    }

    [Fact]
    public void Prosperity_AtPar_LeavesYieldUnchanged_AndDriftsTowardItsTarget()
    {
        var w = Reach(); var eco = new Economy(Catalog(), Ventures());
        var before = w.House("vega")!.Treasury.Credits;
        eco.Accrue(w);
        Assert.Equal(before + 1, w.House("vega")!.Treasury.Credits);           // agri +2 −1 at prosperity 50 = par
        // target: 50 + admin 0 − unrest 0 + 0 buildings; the barony sits at par and stays there
        Assert.Equal(50, w.Holding("barony")!.Prosperity);
        w.Char("self")!.Skills["administration"] = 3; w.Holding("barony")!.Buildings.Add("farm");
        Assert.Equal(50 + 12 + 3, eco.ProsperityTarget(w, w.Holding("barony")!));
        eco.Accrue(w);
        Assert.Equal(55, w.Holding("barony")!.Prosperity);                        // moves five a turn
    }

    [Fact]
    public void Prosperity_ScalesCredits_AndMaterials()
    {
        var w = Reach(); w.Holding("barony")!.Prosperity = 100;
        var lg = new Economy(Catalog(), Ventures()).Compute(w, "vega");
        Assert.Equal(1 * 150 / 100, lg.Net.Credits);                                // (2−1) × 1.5 → 1 (integer); manpower untouched
        Assert.Equal(3, lg.Net.Manpower);
        w.Holding("barony")!.Prosperity = 0; w.Holding("barony")!.Buildings.Add("farm");
        lg = new Economy(Catalog(), Ventures()).Compute(w, "vega");
        Assert.Equal((2 - 1 + 1) * 50 / 100, lg.Net.Credits);                     // 2 × 0.5 = 1
    }

    [Fact]
    public void Invest_SpendsCredits_AndLiftsProsperityAtOnce()
    {
        var w = Reach(credits: 20);
        Assert.True(new CommandExecutor().TryExecute(new InvestCommand("self", "barony", Ventures()), Ctx(w)));
        Assert.Equal(14, w.House("vega")!.Treasury.Credits);
        Assert.Equal(65, w.Holding("barony")!.Prosperity);
        w.Holding("barony")!.Prosperity = 100;
        Assert.Equal("already thriving", new InvestCommand("self", "barony", Ventures()).Refusal(Ctx(w)));
    }

    [Fact]
    public void StakeClaim_NeedsABelt_NearHome_AndRespectsRichness()
    {
        var w = Reach(); var cat = Catalog(); var v = Ventures();
        Assert.Equal("no belt", new StakeClaimCommand("self", "tessaly", cat, v).Refusal(Ctx(w)));
        Assert.Equal("too far from your holdings", new StakeClaimCommand("self", "far", cat, v).Refusal(Ctx(w)));
        Assert.True(new CommandExecutor().TryExecute(new StakeClaimCommand("self", "ember", cat, v), Ctx(w)));   // next door
        var claim = w.Holding("claim_vega_ember")!;
        Assert.True(claim.IsClaim); Assert.False(claim.IsPlanet); Assert.Equal("vega", claim.OwnerId);
        Assert.Equal(24, w.House("vega")!.Treasury.Credits); Assert.Equal(6, w.House("vega")!.Treasury.Materials);
        Assert.Equal("already staked here", new StakeClaimCommand("self", "ember", cat, v).Refusal(Ctx(w)));
        // the rich belt pays: 2 base + 3 richness materials, and an exotic, less a credit of upkeep
        var line = new Economy(cat, v).Compute(w, "vega").Lines.Single(l => l.Kind == "claim");
        Assert.Equal(5, line.Delta.Materials); Assert.Equal(1, line.Delta.Exotics); Assert.Equal(-1, line.Delta.Credits);
    }

    [Fact]
    public void LandlessHouse_MayStakeAnywhere()
    {
        var w = Reach(); w.Holdings.Remove("barony");
        Assert.Null(new StakeClaimCommand("self", "far", Catalog(), Ventures()).Refusal(Ctx(w)));
    }

    [Fact]
    public void Belt_ClaimsOut_AtItsRichness()
    {
        var w = Reach(); var cat = Catalog(); var v = Ventures();
        w.Holdings["c1"] = new Holding { Id = "c1", OwnerId = "corwin", SystemId = "far", Kind = Holding.Claim, Specialization = StakeClaimCommand.Spec };
        w.Holdings["corwin_far"] = new Holding { Id = "corwin_far", OwnerId = "corwin", SystemId = "far", Specialization = "agri" };
        Assert.Equal("the belt is claimed out", new StakeClaimCommand("corwin_head", "far", cat, v).Refusal(Ctx(w)));
    }

    [Fact]
    public void FoundVenture_PaysOwner_AndChartersTheWorldsHolder()
    {
        var w = Reach(); var cat = Catalog(); var v = Ventures(); var eco = new Economy(cat, v);
        // a trading company on Corwin's peopled world: Vega earns 4, pays Corwin a charter of 1
        Assert.True(new CommandExecutor().TryExecute(new FoundBusinessCommand("self", "trading_company", "ember", v, cat), Ctx(w)));
        var biz = w.Business(FoundBusinessCommand.IdFor("vega", "trading_company", "ember"))!;
        Assert.Equal(1, biz.Level); Assert.Equal(18, w.House("vega")!.Treasury.Credits);
        var mine = eco.Compute(w, "vega"); var theirs = eco.Compute(w, "corwin");
        Assert.Equal(3, mine.Lines.Single(l => l.Kind == "venture").Delta.Credits);     // 4 − charter 1
        Assert.Equal(1, theirs.Lines.Single(l => l.Kind == "charter").Delta.Credits);
        Assert.Equal("you already have one here", new FoundBusinessCommand("self", "trading_company", "ember", v, cat).Refusal(Ctx(w)));
        Assert.Equal("needs a peopled world", new FoundBusinessCommand("self", "trading_company", "far", v, cat).Refusal(Ctx(w)));
        Assert.Equal("needs a belt", new FoundBusinessCommand("self", "mining_concern", "tessaly", v, cat).Refusal(Ctx(w)));
        Assert.Equal("needs a world of your own", new FoundBusinessCommand("self", "merchant_bank", "ember", v, cat).Refusal(Ctx(w)));
    }

    [Fact]
    public void ExpandAndSell_MoveTheBooks()
    {
        var w = Reach(credits: 40); var cat = Catalog(); var v = Ventures(); var exec = new CommandExecutor();
        exec.TryExecute(new FoundBusinessCommand("self", "trading_company", "tessaly", v, cat), Ctx(w));
        var id = FoundBusinessCommand.IdFor("vega", "trading_company", "tessaly");
        Assert.True(exec.TryExecute(new ExpandBusinessCommand("self", id, v), Ctx(w)));
        Assert.Equal(2, w.Business(id)!.Level); Assert.Equal(40 - 12 - 10, w.House("vega")!.Treasury.Credits);
        int price = SellBusinessCommand.Price(v.Def("trading_company")!, w.Business(id)!);
        Assert.Equal((12 + 4 + 10) * 6 / 10, price);
        Assert.True(exec.TryExecute(new SellBusinessCommand("self", id, v), Ctx(w)));
        Assert.Null(w.Business(id)); Assert.Equal(18 + price, w.House("vega")!.Treasury.Credits);
        Assert.Equal("no such venture", new SellBusinessCommand("corwin_head", "nothing", v).Refusal(Ctx(w)));
    }

    [Fact]
    public void AtWar_HalvesAVentureOnTheEnemysWorld()
    {
        var w = Reach(); var cat = Catalog(); var v = Ventures();
        new CommandExecutor().TryExecute(new FoundBusinessCommand("self", "trading_company", "ember", v, cat), Ctx(w));
        w.Wars.Add(new War { HouseA = "vega", HouseB = "corwin", Since = 1 });
        var line = new Economy(cat, v).Compute(w, "vega").Lines.Single(l => l.Kind == "venture");
        Assert.Equal(2 - 1, line.Delta.Credits);                                         // 4 halved, less the charter
        Assert.Equal("the world's holder is at war with you", new FoundBusinessCommand("self", "mining_concern", "ember", v, cat).Refusal(Ctx(w)));
    }

    [Fact]
    public void Loan_PaysInstalments_AndInterestToTheLendersBank()
    {
        var w = Reach(credits: 2); var cat = Catalog(); var v = Ventures(); var eco = new Economy(cat, v); var exec = new CommandExecutor();
        // Corwin keeps a bank on its own world
        w.House("corwin")!.Treasury.Credits = 30;
        Assert.True(exec.TryExecute(new FoundBusinessCommand("corwin_head", "merchant_bank", "ember", v, cat), Ctx(w)));
        Assert.True(exec.TryExecute(new TakeLoanCommand("self", v), Ctx(w)));
        var vega = w.House("vega")!;
        Assert.Equal(22, vega.Treasury.Credits); Assert.Equal(26, vega.Debt); Assert.Equal("corwin", vega.LenderId);
        Assert.Equal("already in debt", new TakeLoanCommand("self", v).Refusal(Ctx(w)));
        int corwinBefore = w.House("corwin")!.Treasury.Credits;
        eco.Accrue(w);
        Assert.Equal(22, vega.Debt);                                                   // 26 − 4
        Assert.Equal(22 + 1 - 4, vega.Treasury.Credits);                               // barony +1, instalment −4
        var corwinLedger = eco.Compute(w, "corwin");
        Assert.Contains(corwinLedger.Lines, l => l.Kind == "interest" && l.Delta.Credits == 1);
        Assert.True(w.House("corwin")!.Treasury.Credits > corwinBefore);
        // clearing it
        vega.Treasury.Credits = 50;
        Assert.True(exec.TryExecute(new RepayLoanCommand("self"), Ctx(w)));
        Assert.Equal(0, vega.Debt); Assert.Equal("", vega.LenderId); Assert.Equal(28, vega.Treasury.Credits);
        Assert.Equal("nothing owed", new RepayLoanCommand("self").Refusal(Ctx(w)));
    }

    [Fact]
    public void Wealth_RanksHouses_AndWritesTheFlags()
    {
        var w = Reach(credits: 30); var cat = Catalog(); var v = Ventures(); var eco = new Economy(cat, v);
        eco.Reckon(w);
        var vega = w.House("vega")!; var corwin = w.House("corwin")!;
        Assert.Equal(30 + 10 * 2 / 3 + Wealth.PlanetValue + 10, vega.Wealth);          // credits, materials, a world at par
        Assert.True(vega.Wealth > corwin.Wealth);
        Assert.Equal(1, w.Counter("wealth_rank")); Assert.True(w.HasFlag("richest:vega"));
        Assert.Equal("comfortable", Wealth.Tier(vega.Wealth));
        new CommandExecutor().TryExecute(new TakeLoanCommand("self", v), Ctx(w));
        new CommandExecutor().TryExecute(new StakeClaimCommand("self", "ember", cat, v), Ctx(w));
        eco.Reckon(w);
        Assert.True(w.HasFlag("in_debt")); Assert.True(w.HasFlag("claim_holder"));
        Assert.Equal(vega.Wealth, Wealth.Of(w, vega, cat, v));
    }

    [Fact]
    public void GovernorWhoHatesTheHead_Skims()
    {
        var w = Reach();
        w.Characters["cousin"] = new Character { Id = "cousin", Name = "Cousin", HouseId = "vega", Age = 35, Alive = true };
        w.Holding("barony")!.GovernorId = "cousin";
        w.Relationship("cousin", "self").Disposition = -30;
        var line = new Economy(Catalog(), Ventures()).Compute(w, "vega").Lines.Single(l => l.Kind == "world");
        Assert.Equal(0, line.Delta.Credits);                                            // +1 net, −1 skimmed
    }

    [Fact]
    public void Accrue_IsDeterministic_WithVenturesAndLoans()
    {
        World Make() { var w = Reach(credits: 2); var v = Ventures(); var cat = Catalog(); var e = new CommandExecutor();
            w.House("corwin")!.Treasury.Credits = 30; e.TryExecute(new FoundBusinessCommand("corwin_head", "merchant_bank", "ember", v, cat), Ctx(w));
            e.TryExecute(new TakeLoanCommand("self", v), Ctx(w)); e.TryExecute(new FoundBusinessCommand("self", "trading_company", "ember", v, cat), Ctx(w)); return w; }
        var a = Make(); var b = Make(); var eco = new Economy(Catalog(), Ventures());
        for (int i = 0; i < 8; i++) { eco.Accrue(a); eco.Accrue(b); }
        Assert.Equal(a.House("vega")!.Treasury.Credits, b.House("vega")!.Treasury.Credits);
        Assert.Equal(a.House("vega")!.Wealth, b.House("vega")!.Wealth);
        Assert.Equal(a.Holding("barony")!.Prosperity, b.Holding("barony")!.Prosperity);
        Assert.Equal(0, a.House("vega")!.Debt);                                         // 26 over 8 turns at 4 a turn clears by turn 7
    }
}

/// <summary>On the shipped content: the Reach has belts and seeded ventures, and rival stewards use the new verbs over a run.</summary>
public class EconomyDepthContentTests
{
    [Fact]
    public void TheReach_HasBelts_SeededVentures_AndStewardsWhoTrade()
    {
        var root = ContentLocator.FindContentDir();
        var content = ContentLoader.LoadFromDirectory(root);
        Assert.False(content.Businesses.IsEmpty);
        Assert.NotNull(content.Holdings.Spec(StakeClaimCommand.Spec));
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, "academy_crucible"), 3);
        Assert.True(w.Galaxy!.Systems.Values.Count(s => s.Belt > 0) >= 6);
        Assert.Equal(4, w.Businesses.Count);                                  // the two landless houses' ventures, the Lucent bank, the Morrow traders
        Assert.All(w.Businesses.Values, b => Assert.NotNull(content.Businesses.Def(b.Kind)));

        var tc = new TurnController(w, content);
        for (int t = 0; t < 30 && !w.IsDynastyDead; t++) { foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); } tc.EndTurn(); }

        Assert.True(w.Businesses.Count > 4, "no steward founded a venture in thirty turns");
        Assert.True(w.Holdings.Values.Any(h => h.IsClaim), "no steward staked a claim in thirty turns");
        Assert.True(w.Houses.Values.All(h => h.Wealth != 0 || !w.HoldingsOf(h.Id).Any()), "wealth never reckoned");
        Assert.True(w.HasFlag("richest:" + Wealth.Ranked(w)[0].house.Id));
        Assert.Contains(w.History, h => h.Text.Contains("founds a"));
    }
}

/// <summary>The QA pass of 2026-11-08: stewards that do not dig debt spirals or found losing ventures, and houses that do not die headless.</summary>
public class StewardQaTests
{
    [Fact]
    public void AHeadlessRivalHouse_GetsACousin_ButThePlayersDoesNot()
    {
        var w = TestContent.EconomyWorld();
        w.Houses["corwin"] = new House { Id = "corwin", Name = "House Corwin", Culture = "reach" };
        w.Holdings["corwin_world"] = new Holding { Id = "corwin_world", OwnerId = "corwin", Name = "Ember Hold", Specialization = "agri", Population = 10, GovernorId = "ghost" };
        w.Houses["empty"] = new House { Id = "empty", Name = "House Empty" };                                 // nothing left: ends quietly
        var dyn = new DynastySystem(); dyn.Tick(w, TestContent.Rng());
        var cousin = w.Characters.Values.Single(c => c.HouseId == "corwin");
        Assert.True(cousin.Alive); Assert.StartsWith("cousin_corwin_", cousin.Id); Assert.Equal(cousin.Id, w.Holding("corwin_world")!.GovernorId);
        Assert.Contains(cousin.Id, w.House("corwin")!.Members); Assert.True(w.Pools.ContainsKey(cousin.Id));
        Assert.DoesNotContain(w.Characters.Values, c => c.HouseId == "empty");
        Assert.Contains(w.History, h => h.Text.Contains("raises the banner of House Corwin"));
        // the player's house: kill the protagonist with no heir and no cousin appears
        w.Char("self")!.Alive = false; dyn.Tick(w, TestContent.Rng());
        Assert.DoesNotContain(w.Characters.Values, c => c.HouseId == "vega" && c.Alive);
    }

    [Fact]
    public void ASteward_DoesNotBorrow_OnASinkingLedger_AndSellsALosingVenture()
    {
        var w = TestContent.EconomyWorld(credits: -12, materials: 0); var cat = TestContent.EconomyCatalog();
        var ventures = new BusinessCatalog(new[] { new BusinessDef { Id = "academy_patronage", Name = "Academy Patronage", Cost = new Resources { Credits = 12 }, FlatYield = new Resources { Influence = 1 }, Upkeep = new Resources { Credits = 1 }, MaxLevel = 2 } });
        w.Houses["npc"] = new House { Id = "npc", Name = "House Npc", Treasury = new Resources { Credits = -12 } };
        w.Characters["npc_head"] = new Character { Id = "npc_head", Name = "Mara", HouseId = "npc", Age = 40, Alive = true }; w.Houses["npc"].Members.Add("npc_head"); w.Pools["npc_head"] = ActionPools.ForNpc();
        w.Holdings["npc_fort"] = new Holding { Id = "npc_fort", OwnerId = "npc", Name = "Fort", Specialization = "forge", SystemId = "" };          // forge: −2 credits a turn, materials only
        w.Businesses["biz"] = new Business { Id = "biz", Name = "Npc Patronage", OwnerId = "npc", Kind = "academy_patronage", SystemId = "", Level = 1 };
        var ai = new NpcAi(); var exec = new CommandExecutor(); var ctx = new CommandContext(w, TestContent.Rng());
        ai.Act(exec, ctx, null, cat, null, ventures);
        Assert.Equal(0, w.House("npc")!.Debt);                                            // no loan on a negative forecast
        Assert.False(w.Businesses.ContainsKey("biz"));                                      // the losing venture was sold
        Assert.True(w.House("npc")!.Treasury.Credits > -12);
    }
}
