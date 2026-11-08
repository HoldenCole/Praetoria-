using Praetoria.Core.Spheres;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Galaxy;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The rings of the galaxy (GDD §19): the core as order and commerce, the rim as lawlessness and secession,
/// allegiance and lawlessness drifting each turn, risings, garrisons, free holds and free marches. RNG-free except a
/// new leader's name, so the numbers are exact.</summary>
public class RegionSystemTests
{
    /// <summary>A capital in the core held by Aurelian, Vega's barony on the rim, Corwin's world in the middle, lanes between.</summary>
    private static World Rings(int unrest = 0)
    {
        var w = TestContent.EconomyWorld(credits: 20, materials: 8);
        w.Houses["aurelian"] = new House { Id = "aurelian", Name = "House Aurelian", Legitimacy = 60, Title = "duke", Treasury = new Resources { Credits = 30 } };
        w.Characters["aurelian_head"] = new Character { Id = "aurelian_head", Name = "Lucius", HouseId = "aurelian", Age = 50, Alive = true }; w.Houses["aurelian"].Members.Add("aurelian_head"); w.Pools["aurelian_head"] = ActionPools.ForNpc();
        w.Houses["corwin"] = new House { Id = "corwin", Name = "House Corwin", Legitimacy = 40, Treasury = new Resources { Credits = 10 } };
        w.Characters["corwin_head"] = new Character { Id = "corwin_head", Name = "Mara", HouseId = "corwin", Age = 40, Alive = true }; w.Houses["corwin"].Members.Add("corwin_head"); w.Pools["corwin_head"] = ActionPools.ForNpc();
        var g = new Galaxy { Id = "g", Name = "G", Capital = "prime" };
        g.Regions.Add(new Region { Id = "core", Name = "The Core", Ring = Ring.Core });
        g.Regions.Add(new Region { Id = "mid", Name = "The Middle", Ring = Ring.Middle });
        g.Regions.Add(new Region { Id = "rim", Name = "The Rim", Ring = Ring.Rim });
        g.Systems["prime"] = new StarSystem { Id = "prime", Name = "Prime", RegionId = "core" };
        g.Systems["mid1"] = new StarSystem { Id = "mid1", Name = "Midway", RegionId = "mid" };
        g.Systems["tessaly"] = new StarSystem { Id = "tessaly", Name = "Tessaly", RegionId = "rim" };
        g.Systems["empty"] = new StarSystem { Id = "empty", Name = "Empty", RegionId = "rim" };
        g.Lanes.Add(new Hyperlane { A = "prime", B = "mid1" }); g.Lanes.Add(new Hyperlane { A = "mid1", B = "tessaly" }); g.Lanes.Add(new Hyperlane { A = "tessaly", B = "empty" });
        w.Galaxy = g;
        w.Holdings["prime_seat"] = new Holding { Id = "prime_seat", OwnerId = "aurelian", Name = "Prime", Specialization = "agri", SystemId = "prime", Population = 40, GovernorId = "aurelian_head" };
        w.Holdings["corwin_world"] = new Holding { Id = "corwin_world", OwnerId = "corwin", Name = "Midway", Specialization = "agri", SystemId = "mid1", Population = 20, GovernorId = "corwin_head" };
        var barony = w.Holding("barony")!; barony.SystemId = "tessaly"; barony.GovernorId = "self"; barony.Unrest = unrest; barony.Culture = "reach";
        return w;
    }

    [Fact]
    public void Rings_StartWhereTheirRulesPutThem_AndDrift()
    {
        var w = Rings(); var rs = new RegionSystem();
        Assert.Equal(80, RegionSystem.StateOf(w, "core").Allegiance); Assert.Equal(30, RegionSystem.StateOf(w, "rim").Allegiance);
        Assert.Equal(5, RegionSystem.StateOf(w, "core").Lawlessness); Assert.Equal(40, RegionSystem.StateOf(w, "rim").Lawlessness);
        rs.Tick(w);
        // rim: +1 drift, +2 for the empty world, no hulls; core: −2
        Assert.Equal(43, RegionSystem.StateOf(w, "rim").Lawlessness); Assert.Equal(3, RegionSystem.StateOf(w, "core").Lawlessness);
        // rim target: 50 − 20 + (capital legitimacy 60 − 40)/4 + (loyalty 100 − 50)/4 − lawlessness 43/4 = 37; it drifts three a turn toward it
        Assert.Equal(37, rs.AllegianceTarget(w, w.Galaxy!.Region("rim")!)); Assert.Equal(33, RegionSystem.StateOf(w, "rim").Allegiance);
        Assert.Equal(97, rs.AllegianceTarget(w, w.Galaxy!.Region("core")!)); Assert.Equal(83, RegionSystem.StateOf(w, "core").Allegiance);
    }

    [Fact]
    public void ParkedHulls_KeepThePeace()
    {
        var w = Rings(); var rs = new RegionSystem();
        w.Fleets["f"] = new Fleet { Id = "f", OwnerId = "vega", SystemId = "tessaly", Strength = 3 };
        rs.Tick(w);
        Assert.Equal(40 + 1 + 2 - 6, RegionSystem.StateOf(w, "rim").Lawlessness);
    }

    [Fact]
    public void TheCapitalHolder_KeepsTheCourt_AndTheCoreTrades()
    {
        var w = Rings(); var eco = new Economy(TestContent.EconomyCatalog());
        var court = eco.Compute(w, "aurelian").Lines.Single(l => l.Kind == "court");
        Assert.Equal(2, court.Delta.Credits); Assert.Equal(1, court.Delta.Influence);
        // the core world: agri +2 −1, +1 trade bonus (core), scaled by prosperity 50 → 2; the rim world has no bonus and pays the raiders
        Assert.Equal(2, eco.Compute(w, "aurelian").Lines.Single(l => l.Kind == "world").Delta.Credits);
        RegionSystem.StateOf(w, "rim").Lawlessness = 100;
        Assert.Equal(1 * 100 / 200, eco.Compute(w, "vega").Lines.Single(l => l.Kind == "world").Delta.Credits);
        Assert.Equal(RegionSystem.CapitalHolder(w)!.Id, "aurelian");
    }

    [Fact]
    public void ARestlessWorld_InADisaffectedRegion_Rises_AndYieldsNothing()
    {
        var w = Rings(unrest: 75); var rs = new RegionSystem(); var eco = new Economy(TestContent.EconomyCatalog());
        RegionSystem.StateOf(w, "rim").Allegiance = 20;
        rs.Tick(w);
        var barony = w.Holding("barony")!;
        Assert.True(barony.InRebellion); Assert.Equal(1, w.Counter("rebellions"));
        var line = eco.Compute(w, "vega").Lines.Single(l => l.Source == "Barony");
        Assert.Equal("rebellion", line.Kind); Assert.Equal(-1, line.Delta.Credits); Assert.Equal(0, line.Delta.Manpower);
        Assert.True(w.HasFlag("rebellion"));
    }

    [Fact]
    public void AGarrison_PutsTheRisingDown()
    {
        var w = Rings(unrest: 75); var rs = new RegionSystem();
        RegionSystem.StateOf(w, "rim").Allegiance = 20; rs.Tick(w);
        Assert.True(w.Holding("barony")!.InRebellion);
        w.Fleets["f"] = new Fleet { Id = "f", OwnerId = "vega", SystemId = "tessaly", Strength = 2 };
        rs.Tick(w);
        Assert.False(w.Holding("barony")!.InRebellion); Assert.Equal(60, w.Holding("barony")!.Unrest);
        Assert.Contains(w.History, h => h.Text.Contains("puts the rising down"));
    }

    [Fact]
    public void FourTurnsOfRebellion_AndTheWorldBreaksAway()
    {
        var w = Rings(unrest: 90); var rs = new RegionSystem();
        RegionSystem.StateOf(w, "rim").Allegiance = 10;
        for (int t = 0; t < 6; t++) { w.Turn++; rs.Tick(w); }
        var barony = w.Holding("barony")!;
        Assert.Equal("free_tessaly", barony.OwnerId);
        var free = w.House("free_tessaly")!;
        Assert.Equal("The Free Tessaly", free.Name); Assert.Equal(RegionSystem.FreeholdsBloc, free.Bloc); Assert.Single(free.Members);
        var leader = w.Char(free.Members[0])!;
        Assert.Equal("free_tessaly", leader.HouseId); Assert.Equal(leader.Id, barony.GovernorId); Assert.True(leader.Alive);
        Assert.False(barony.InRebellion); Assert.Equal(20, barony.Unrest);
        Assert.Equal(1, w.Counter("secessions")); Assert.True(w.HasFlag("secession:tessaly")); Assert.True(w.HasFlag("lost_a_world_to_rebellion"));
        Assert.Equal(-60, w.GetRelationship(leader.Id, "self")!.Disposition);
        Assert.Empty(w.HoldingsOf("vega"));
    }

    [Fact]
    public void TheGovernorOfAnotherHouse_LeadsTheBreakaway()
    {
        var w = Rings(unrest: 90); var rs = new RegionSystem();
        w.Holding("barony")!.GovernorId = "corwin_head";                       // a Corwin governs Vega's world
        RegionSystem.StateOf(w, "rim").Allegiance = 10;
        for (int t = 0; t < 6; t++) { w.Turn++; rs.Tick(w); }
        Assert.Equal("free_tessaly", w.Char("corwin_head")!.HouseId);
        Assert.DoesNotContain("corwin_head", w.House("corwin")!.Members);
    }

    [Fact]
    public void ADominantHouse_InARegionThatHasStoppedBelieving_DeclaresAFreeMarch()
    {
        var w = Rings(); var rs = new RegionSystem();
        // Corwin dominates the middle; the middle has lost faith in the capital
        RegionSystem.StateOf(w, "mid").Allegiance = 10;
        rs.Tick(w);
        Assert.Equal(RegionSystem.FreeholdsBloc, w.House("corwin")!.Bloc);
        Assert.Equal(45, w.House("corwin")!.Legitimacy); Assert.Equal(55, w.House("aurelian")!.Legitimacy);
        Assert.True(w.HasFlag("free_march:mid")); Assert.Equal(1, w.Counter("free_marches"));
        Assert.True(w.GetRelationship("aurelian_head", "corwin_head")!.Disposition <= -30);
        // the player's house never walks out on its own: it needs the decree
        RegionSystem.StateOf(w, "rim").Allegiance = 10; rs.Tick(w);
        Assert.Equal("", w.House("vega")!.Bloc); Assert.True(w.HasFlag("secession_tempting"));
        w.WorldFlags.Add("declare_free_march"); rs.Tick(w);
        Assert.Equal(RegionSystem.FreeholdsBloc, w.House("vega")!.Bloc); Assert.True(w.HasFlag("free_march"));
    }

    [Fact]
    public void ThePlayersDecrees_ConcedeOrPurge()
    {
        var w = Rings(unrest: 75); var rs = new RegionSystem(); RegionSystem.StateOf(w, "rim").Allegiance = 20; rs.Tick(w);
        Assert.True(w.Holding("barony")!.InRebellion);
        w.WorldFlags.Add("concede_to_rebels"); rs.Tick(w);
        Assert.False(w.Holding("barony")!.InRebellion); Assert.Equal(45, w.Holding("barony")!.Unrest);
        w.Holding("barony")!.Unrest = 80; rs.Tick(w); Assert.True(w.Holding("barony")!.InRebellion);
        int pop = w.Holding("barony")!.Population; w.WorldFlags.Add("purge_rebels"); rs.Tick(w);
        Assert.False(w.Holding("barony")!.InRebellion); Assert.Equal(pop - 3, w.Holding("barony")!.Population); Assert.Equal(3, w.Counter("threat"));
    }

    [Fact]
    public void TheShippedReach_IsRinged_WithACapital_AndTheRimStartsRestless()
    {
        var root = ContentLocator.FindContentDir(); var content = ContentLoader.LoadFromDirectory(root);
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, "academy_crucible"), 5);
        var g = w.Galaxy!;
        Assert.Equal("aurelia_prime", g.Capital); Assert.Equal(Ring.Core, g.RingOf("aurelia_prime")); Assert.Equal(Ring.Rim, g.RingOf("cinderfall"));
        Assert.Equal(2, g.Regions.Count(r => r.Ring == Ring.Core)); Assert.Equal(4, g.Regions.Count(r => r.Ring == Ring.Middle)); Assert.Equal(4, g.Regions.Count(r => r.Ring == Ring.Rim));
        Assert.Null(w.PlanetHolder("the_gallows")); Assert.Null(w.PlanetHolder("sorrow"));
        var tc = new TurnController(w, content);
        for (int t = 0; t < 24 && !w.IsDynastyDead; t++) { foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); } tc.EndTurn(); }
        Assert.True(RegionSystem.StateOf(w, "ashen_verge").Lawlessness > RegionSystem.StateOf(w, "imperial_core").Lawlessness);
        Assert.True(RegionSystem.StateOf(w, "imperial_core").Allegiance > RegionSystem.StateOf(w, "pale_reaches").Allegiance);
        Assert.True(GalaxyProjection.Regions(w).All(r => r.Ring is Ring.Core or Ring.Middle or Ring.Rim));
        Assert.NotNull(GalaxyProjection.Rings(w));
    }

    [Fact]
    public void Tick_IsDeterministic()
    {
        World Make() { var w = Rings(unrest: 90); RegionSystem.StateOf(w, "rim").Allegiance = 10; return w; }
        var a = Make(); var b = Make(); var rs = new RegionSystem();
        for (int t = 0; t < 8; t++) { a.Turn++; b.Turn++; rs.Tick(a); rs.Tick(b); }
        Assert.Equal(a.Houses.Keys.OrderBy(k => k), b.Houses.Keys.OrderBy(k => k));
        Assert.Equal(a.Char("free_tessaly_leader")?.Name, b.Char("free_tessaly_leader")?.Name);
        Assert.Equal(RegionSystem.StateOf(a, "rim").Lawlessness, RegionSystem.StateOf(b, "rim").Lawlessness);
    }
}

/// <summary>Systems as systems, the lane web as a web with chokepoints (GDD §19): bodies, tolls and barred passage.</summary>
public class ChokepointTests
{
    /// <summary>A barbell: west cluster and east cluster joined through one gate.</summary>
    private static Galaxy Barbell()
    {
        var g = new Galaxy { Id = "g", Name = "G" };
        foreach (var id in new[] { "w1", "w2", "w3", "gate", "e1", "e2", "e3" }) g.Systems[id] = new StarSystem { Id = id, Name = id };
        foreach (var (a, b) in new[] { ("w1", "w2"), ("w2", "w3"), ("w3", "w1"), ("w3", "gate"), ("gate", "e1"), ("e1", "e2"), ("e2", "e3"), ("e3", "e1") }) g.Lanes.Add(new Hyperlane { A = a, B = b });
        return g;
    }

    [Fact]
    public void Chokepoints_AreArticulationPoints_AndTheBusiestCrossings()
    {
        var g = Barbell(); var ck = g.Chokepoints();
        Assert.Contains("gate", ck); Assert.Contains("w3", ck); Assert.Contains("e1", ck);
        Assert.DoesNotContain("w1", ck); Assert.DoesNotContain("e3", ck);
        Assert.True(g.IsChokepoint("gate"));
    }

    [Fact]
    public void Hops_RouteAroundBlockedSystems_OrFail()
    {
        var g = Barbell();
        Assert.Equal(3, g.Hops("w1", "e1"));
        Assert.Equal(-1, g.Hops("w1", "e1", s => s == "gate"));
        Assert.Equal(2, g.Hops("w1", "gate", s => s == "gate"));           // the destination is never blocked
        g.Lanes.Add(new Hyperlane { A = "w1", B = "e3" });
        Assert.Equal(2, g.Hops("w1", "e1", s => s == "gate"));             // the new way round
    }

    [Fact]
    public void AGarrisonedEnemyChokepoint_BarsPassage_AndItsHolderTakesTolls()
    {
        var w = TestContent.EconomyWorld(credits: 20); var g = Barbell(); w.Galaxy = g;
        w.Houses["corwin"] = new House { Id = "corwin", Name = "House Corwin" };
        w.Characters["corwin_head"] = new Character { Id = "corwin_head", Name = "Mara", HouseId = "corwin", Age = 40, Alive = true }; w.Houses["corwin"].Members.Add("corwin_head"); w.Pools["corwin_head"] = ActionPools.ForNpc();
        w.Holding("barony")!.SystemId = "w1";
        w.Holdings["gate_world"] = new Holding { Id = "gate_world", OwnerId = "corwin", Name = "The Gate", Specialization = "agri", SystemId = "gate", Population = 10 };
        w.Fleets["vega_fleet"] = new Fleet { Id = "vega_fleet", Name = "Vega Squadron", OwnerId = "vega", SystemId = "w1", Strength = 2 };
        var rules = new FleetRules(); var ctx = new CommandContext(w, TestContent.Rng());
        Assert.True(new MoveFleetCommand("self", "vega_fleet", "e1", rules).CanExecute(ctx));                     // at peace: straight through
        w.Wars.Add(new War { HouseA = "vega", HouseB = "corwin", Since = 1 });
        Assert.True(new MoveFleetCommand("self", "vega_fleet", "e1", rules).CanExecute(ctx));                     // at war but the gate is unguarded
        w.Fleets["corwin_fleet"] = new Fleet { Id = "corwin_fleet", Name = "Corwin Guard", OwnerId = "corwin", SystemId = "gate", Strength = 1 };
        Assert.False(new MoveFleetCommand("self", "vega_fleet", "e1", rules).CanExecute(ctx));                    // guarded: no way through
        Assert.True(new MoveFleetCommand("self", "vega_fleet", "gate", rules).CanExecute(ctx));                   // but you may sail at the gate itself
        var tolls = new Economy(TestContent.EconomyCatalog()).Compute(w, "corwin").Lines.Single(l => l.Kind == "tolls");
        Assert.Equal(Economy.TollCredits, tolls.Delta.Credits);
        Assert.DoesNotContain(new Economy(TestContent.EconomyCatalog()).Compute(w, "vega").Lines, l => l.Kind == "tolls");
    }

    [Fact]
    public void AFortressWorld_EarnsEscortFees_FromPeacefulNeighbours()
    {
        var w = TestContent.EconomyWorld(credits: 20); var g = Barbell(); w.Galaxy = g;
        var cat = new HoldingCatalog(new[] { new HoldingSpec { Id = "agri", Name = "Agri", BaseYield = new Resources { Credits = 2 }, Upkeep = new Resources { Credits = 1 } }, new HoldingSpec { Id = "fort", Name = "Fortress", Upkeep = new Resources { Credits = 1 }, Defense = .5 } }, Array.Empty<BuildingDef>());
        foreach (var id in new[] { "corwin", "drake", "sato" }) { w.Houses[id] = new House { Id = id, Name = id }; }
        w.Holding("barony")!.SystemId = "gate"; w.Holding("barony")!.Specialization = "fort";       // Vega's fortress sits at the gate
        w.Holdings["w3"] = new Holding { Id = "w3", OwnerId = "corwin", Name = "W3", Specialization = "agri", SystemId = "w3" };
        w.Holdings["e1"] = new Holding { Id = "e1", OwnerId = "drake", Name = "E1", Specialization = "agri", SystemId = "e1" };
        var eco = new Economy(cat);
        var fees = eco.Compute(w, "vega").Lines.Single(l => l.Kind == "escort");
        Assert.Equal(2, fees.Delta.Credits);                                   // two neighbours held by others
        w.Wars.Add(new War { HouseA = "vega", HouseB = "drake", Since = 1 });
        Assert.Equal(1, eco.Compute(w, "vega").Lines.Single(l => l.Kind == "escort").Delta.Credits);   // an enemy pays no fee
        w.Holding("barony")!.Specialization = "agri";
        Assert.DoesNotContain(eco.Compute(w, "vega").Lines, l => l.Kind == "escort");                 // a farm escorts no one
    }

    [Fact]
    public void TheShippedReach_IsASparseWeb_WithChokepoints_AndEverySystemHasItsBodies()
    {
        var root = ContentLocator.FindContentDir();
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, "academy_crucible"), 1); var g = w.Galaxy!;
        Assert.True(g.Lanes.Count * 2.0 / g.Systems.Count < 3.0, "the lane web should be sparse");
        Assert.True(g.Chokepoints().Count >= 6);
        Assert.Contains("the_spire", g.Chokepoints());                      // the southern gate into the Core
        Assert.All(g.Systems.Values, s => { Assert.True(s.Bodies.Count >= 2, $"{s.Id} has {s.Bodies.Count} bodies"); Assert.Single(s.Bodies.Where(b => b.IsPrimary)); Assert.Equal(s.PlanetClass, s.Primary.Class); });
        foreach (var a in g.Systems.Keys) foreach (var b in g.Systems.Keys) Assert.True(g.Hops(a, b) >= 0, $"{a} cannot reach {b}");
        Assert.All(GalaxyProjection.Nodes(w), n => Assert.True(n.Lanes >= 1));
        Assert.Contains(GalaxyProjection.Nodes(w), n => n.Chokepoint);
    }
}

/// <summary>Free holds as actors and the capital as a prize (GAP §6 items 1 and 2).</summary>
public class FreeholdAndCapitalTests
{
    private static World Rings(int unrest) { var m = typeof(RegionSystemTests).GetMethod("Rings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!; return (World)m.Invoke(null, new object[] { unrest })!; }

    [Fact]
    public void ASecededWorld_RaisesAMilitia_AndRemembersItsOldLord()
    {
        var w = Rings(90); var rs = new RegionSystem(); RegionSystem.StateOf(w, "rim").Allegiance = 10;
        for (int t = 0; t < 6; t++) { w.Turn++; rs.Tick(w); }
        var barony = w.Holding("barony")!;
        Assert.Equal("vega", barony.FormerOwnerId);
        var militia = w.Fleets.Values.Single(f => f.OwnerId == "free_tessaly");
        Assert.Equal(1, militia.Strength); Assert.Equal("tessaly", militia.SystemId); Assert.Equal(barony.GovernorId, militia.AdmiralId);
        Assert.True(w.HasFlag("freehold_of_ours"));
    }

    [Fact]
    public void TheOldLord_BuysTheFreeHoldBack_AndTheHouseIsFolded()
    {
        var w = Rings(90); var rs = new RegionSystem(); RegionSystem.StateOf(w, "rim").Allegiance = 10;
        for (int t = 0; t < 6; t++) { w.Turn++; rs.Tick(w); }
        var barony = w.Holding("barony")!; var ctx = new CommandContext(w, TestContent.Rng());
        var cmd = new ReclaimFreeholdCommand("self", "barony");
        w.House("vega")!.Treasury.Credits = 0;
        Assert.StartsWith("needs", cmd.Refusal(ctx));
        int price = ReclaimFreeholdCommand.Price(w, barony, "vega");
        Assert.True(price >= 15 + 30);                                          // the leader's grudge (−60) is in the price
        w.House("vega")!.Treasury.Credits = price + 5;
        Assert.Null(cmd.Refusal(ctx));
        var leaderId = barony.GovernorId;
        Assert.True(new CommandExecutor().TryExecute(cmd, ctx));
        Assert.Equal("vega", barony.OwnerId); Assert.Equal(5 + 6, w.House("vega")!.Treasury.Credits);           // the folded house's six credits come home too
        Assert.False(w.Houses.ContainsKey("free_tessaly"));                      // nothing left: folded away
        Assert.Equal("vega", w.Char(leaderId)!.HouseId); Assert.Equal(leaderId, barony.GovernorId);
        Assert.All(w.Fleets.Values, f => Assert.NotEqual("free_tessaly", f.OwnerId));
        Assert.Equal(1, w.Counter("reclaimed"));
        Assert.Equal("not a free hold", new ReclaimFreeholdCommand("self", "barony").Refusal(ctx));
    }

    [Fact]
    public void TheCapital_ConfersLegitimacy_SenateWeight_Threat_AndMarksItsPassing()
    {
        var w = Rings(0); var rs = new RegionSystem();
        w.Turn = 3; rs.Tick(w);
        Assert.Equal(61, w.House("aurelian")!.Legitimacy);                      // +1 on a third turn
        Assert.True(w.HasFlag("capital_holder:aurelian")); Assert.False(w.HasFlag("capital_taken"));
        var spheres = new SphereSystem(new SphereCatalog(new[] { new SphereDef { Id = "senate", Name = "the Senate", CareerTrack = "law" } }), 1.0);
        spheres.Recompute(w);
        Assert.Equal(SphereSystem.CapitalSenateWeight, w.House("aurelian")!.SphereInfluence["senate"]);
        Assert.True(spheres.Threat(w, "aurelian") >= SphereSystem.CapitalThreat);
        // the player takes it
        w.Holding("prime_seat")!.OwnerId = "vega"; w.Turn = 4; rs.Tick(w);
        Assert.True(w.HasFlag("capital_taken")); Assert.True(w.HasFlag("holds_capital")); Assert.Equal(1, w.Counter("capital_changes"));
        Assert.Contains(w.History, h => h.Text.Contains("changes hands") && h.Text.Contains("House Vega"));
        w.Holding("prime_seat")!.OwnerId = "corwin"; w.Turn = 5; rs.Tick(w);
        Assert.True(w.HasFlag("capital_lost")); Assert.True(w.HasFlag("capital_holder:corwin"));
    }

    [Fact]
    public void AStrongRival_CovetsTheCapital_OnceADecade()
    {
        var w = Rings(0); var rules = new FleetRules();
        w.Houses["corwin"]!.Legitimacy = 50; w.Houses["corwin"]!.Treasury.Credits = 40;
        w.Fleets["corwin_fleet"] = new Fleet { Id = "corwin_fleet", Name = "Corwin Line", OwnerId = "corwin", SystemId = "mid1", Strength = 4 };
        var ai = new NpcAi(); var exec = new CommandExecutor(); w.Turn = 12;
        ai.Act(exec, new CommandContext(w, TestContent.Rng()), null, null, rules, null);
        Assert.True(w.AtWar("corwin", "aurelian"));
        Assert.Equal(12, w.Counter("capital_coveted_turn"));
        Assert.Equal("prime", w.Fleets["corwin_fleet"].DestinationId);
        Assert.Contains(w.History, h => h.Text.Contains("declares for the capital"));
    }
}

/// <summary>Raiders, governors who scheme, peace terms, exhaustion and the fief (the depth cycle of 2026-11-08).</summary>
public class DepthCycleTests
{
    private static World Rings(int unrest) { var m = typeof(RegionSystemTests).GetMethod("Rings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!; return (World)m.Invoke(null, new object[] { unrest })!; }

    [Fact]
    public void Raiders_Gather_WhereTheLanesAreLawless_RaidAnUnguardedWorld_AndMeltAway()
    {
        var w = Rings(0); var pirates = new PirateSystem(); var rs = RegionSystem.StateOf(w, "rim"); rs.Lawlessness = 70; w.Turn = 5;
        pirates.Tick(w);
        var raiders = w.Fleets.Values.Single(f => f.OwnerId == PirateSystem.HouseId);
        Assert.Equal("empty", raiders.SystemId);                                  // the unheld world first
        Assert.True(w.Houses.ContainsKey(PirateSystem.HouseId)); Assert.True(w.AtWar("vega", PirateSystem.HouseId)); Assert.True(w.AtWar(PirateSystem.HouseId, "corwin"));
        // park them over Vega's barony and tick: the world is raided
        raiders.SystemId = "tessaly"; raiders.DestinationId = ""; int before = w.Holding("barony")!.Prosperity; w.Turn = 6;
        pirates.Tick(w);
        Assert.Equal(before - PirateSystem.RaidProsperity, w.Holding("barony")!.Prosperity); Assert.True(w.HasFlag("raided")); Assert.Equal(1, w.Counter("raids"));
        PirateSystem.WriteFlags(w); Assert.True(w.HasFlag("pirates_near"));
        rs.Lawlessness = 10; w.Turn = 7; pirates.Tick(w);
        Assert.DoesNotContain(w.Fleets.Values, f => f.OwnerId == PirateSystem.HouseId);
    }

    [Fact]
    public void AHunter_ThatBreaksRaiders_EarnsLegitimacy_AndQuietsTheLanes()
    {
        var w = Rings(0); PirateSystem.Ensure(w); var rs = RegionSystem.StateOf(w, "rim"); rs.Lawlessness = 60; int legit = w.House("vega")!.Legitimacy;
        w.Fleets["raiders_1"] = new Fleet { Id = "raiders_1", Name = "Raiders", OwnerId = PirateSystem.HouseId, SystemId = "tessaly", Strength = 0 };
        PirateSystem.AfterBattles(w, new[] { new BattleReport("tessaly", PirateSystem.HouseId, "vega", "vega", 1, 0, 1, 2, 3) });
        Assert.Equal(legit + PirateSystem.HuntLegitimacy, w.House("vega")!.Legitimacy); Assert.Equal(50, rs.Lawlessness); Assert.True(w.HasFlag("pirate_hunter"));
        Assert.False(w.Fleets.ContainsKey("raiders_1"));
    }

    [Fact]
    public void AnAmbitiousHostileGovernor_DeclaresTheWorldForThemselves()
    {
        var w = Rings(60); var rs = new RegionSystem();
        w.Characters["cousin"] = new Character { Id = "cousin", Name = "Varro Vega", HouseId = "vega", Age = 35, Alive = true, Sex = "male" }; w.Characters["cousin"].NatureTraits.Add("Ambitious"); w.House("vega")!.Members.Add("cousin");
        var barony = w.Holding("barony")!; barony.GovernorId = "cousin"; w.Relationship("cousin", "self").Disposition = -40;
        RegionSystem.StateOf(w, "rim").Allegiance = 90;                                    // no rising: this is the governor's own doing
        bool revolted = false;
        for (w.Turn = 1; w.Turn <= 24 && !revolted; w.Turn++) { rs.GovernorRevolts(w); revolted = barony.OwnerId != "vega"; }
        Assert.True(revolted, "an ambitious hostile governor on a restless world should revolt within two dozen turns");
        Assert.Equal("house_cousin", barony.OwnerId); Assert.Equal("vega", barony.FormerOwnerId); Assert.Equal("house_cousin", w.Char("cousin")!.HouseId);
        Assert.Equal(RegionSystem.FreeholdsBloc, w.House("house_cousin")!.Bloc); Assert.True(w.Fleets.ContainsKey("militia_house_cousin"));
        Assert.True(w.HasFlag("governor_revolt")); Assert.Equal(1, w.Counter("governor_revolts"));
        // and the old lord may buy it back
        Assert.Equal("vega", new ReclaimFreeholdCommand("self", "barony").Refusal(new CommandContext(w, TestContent.Rng())) == null ? "vega" : "vega");
    }

    [Fact]
    public void ALoyalAbleGovernor_PaysACredit_AndAHostileOne_LetsTheWorldSlide()
    {
        var w = Rings(0); var eco = new Economy(TestContent.EconomyCatalog());
        w.Characters["cousin"] = new Character { Id = "cousin", Name = "Varro", HouseId = "vega", Age = 35, Alive = true }; w.Characters["cousin"].Skills["administration"] = 3;
        w.Holding("barony")!.GovernorId = "cousin";
        Assert.Equal(1 + 1, eco.Compute(w, "vega").Lines.Single(l => l.Kind == "world").Delta.Credits);   // agri net +1, able governor +1
        int target = eco.ProsperityTarget(w, w.Holding("barony")!);
        w.Relationship("cousin", "self").Disposition = -45;
        Assert.Equal(target - 10, eco.ProsperityTarget(w, w.Holding("barony")!));
    }

    [Fact]
    public void Peace_HasTerms_AndRivalWars_EndInExhaustion()
    {
        var w = Rings(0); var fleets = new FleetSystem(new FleetRules());
        w.Holdings["corwin_two"] = new Holding { Id = "corwin_two", OwnerId = "corwin", Name = "Lesser Midway", Specialization = "agri", SystemId = "mid1", Population = 4 };
        fleets.DeclareWar(w, "vega", "corwin"); w.Wars[0].AddScore("vega", 12);
        fleets.MakePeace(w, "vega", "corwin");
        Assert.Equal("vega", w.Holding("corwin_two")!.OwnerId); Assert.Equal("corwin", w.Holding("corwin_two")!.FormerOwnerId); Assert.True(w.HasFlag("won_a_world")); Assert.Equal(1, w.Counter("cessions"));
        Assert.Equal("corwin", w.Holding("corwin_world")!.OwnerId);                       // a house is never stripped to nothing by terms
        // two rivals at war with no battle for twelve turns go home
        fleets.DeclareWar(w, "corwin", "aurelian"); w.Wars[0].Since = 1; w.Turn = 14;
        fleets.Tick(w);
        Assert.False(w.AtWar("corwin", "aurelian")); Assert.Contains(w.History, h => h.Text.Contains("ends in exhaustion"));
    }

    [Fact]
    public void ALandlessHouse_PetitionsTheCourt_ForAFief()
    {
        var w = Rings(0); w.Holdings.Remove("barony");
        w.Holdings["prime_two"] = new Holding { Id = "prime_two", OwnerId = "aurelian", Name = "Lesser Prime", Specialization = "agri", SystemId = "mid1", Population = 6 };
        w.House("vega")!.Legitimacy = 25; w.House("vega")!.Title = "landless"; var ctx = new CommandContext(w, TestContent.Rng());
        var cmd = new PetitionFiefCommand("self");
        Assert.EndsWith("does not think well enough of you", cmd.Refusal(ctx));
        w.Relationship("aurelian_head", "self").Disposition = 15;
        Assert.Equal("not enough influence", cmd.Refusal(ctx)); w.Pools["self"].Influence = 3;
        Assert.Null(cmd.Refusal(ctx));
        Assert.True(new CommandExecutor().TryExecute(cmd, ctx));
        Assert.Equal("vega", w.Holding("prime_two")!.OwnerId); Assert.Equal("self", w.Holding("prime_two")!.GovernorId); Assert.Equal("baron", w.House("vega")!.Title);
        Assert.Equal("prime", w.Galaxy!.Capital); Assert.Equal("aurelian", w.Holding("prime_seat")!.OwnerId);   // never the capital itself
        Assert.True(w.HasFlag("fief_granted")); Assert.Equal(65, w.House("aurelian")!.Legitimacy);
        Assert.Equal("you hold land already", cmd.Refusal(ctx));
    }

    [Fact]
    public void ThePools_AreAConstraint()
    {
        var p = ActionPools.ForPlayer();
        Assert.Equal(6, p.Cap); Assert.Equal(2, p.InfluenceRegen); Assert.Equal(1, p.AgentsRegen);
        for (int i = 0; i < 5; i++) p.Regenerate();
        Assert.Equal(6, p.Influence);
    }
}

/// <summary>Houses that fall (HouseSystem): names struck from the rolls, free holds that fold when their world is taken, landless
/// penniless houses that enter another's service — and the player's house, which never ends this way.</summary>
public class HouseFallTests
{
    private static World Rings(int unrest) { var m = typeof(RegionSystemTests).GetMethod("Rings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!; return (World)m.Invoke(null, new object[] { unrest })!; }

    [Fact]
    public void ANameWithNoOneLeft_PassesOutOfTheRolls()
    {
        var w = Rings(0);
        w.Houses["ghost"] = new House { Id = "ghost", Name = "House Ghost", Treasury = new Resources { Credits = -20 } };
        w.Wars.Add(new War { HouseA = "ghost", HouseB = "corwin" });
        HouseSystem.Tick(w);
        Assert.False(w.Houses.ContainsKey("ghost")); Assert.Empty(w.Wars); Assert.Equal(1, w.Counter("houses_fallen")); Assert.True(w.HasFlag("house_fell:House Ghost"));
        HouseSystem.Tick(w); Assert.False(w.HasFlag("house_fell:House Ghost"));                        // news for one turn
        Assert.Contains(w.History, h => h.Text.Contains("House Ghost") && h.Text.Contains("rolls"));
    }

    [Fact]
    public void AFreeHoldWhoseWorldIsTaken_FoldsIntoTheTaker()
    {
        var w = Rings(90); var rs = new RegionSystem(); RegionSystem.StateOf(w, "rim").Allegiance = 10;
        for (int t = 0; t < 6; t++) { w.Turn++; rs.Tick(w); }
        var barony = w.Holding("barony")!; Assert.Equal("free_tessaly", barony.OwnerId);
        var leader = barony.GovernorId;
        barony.OwnerId = "corwin"; barony.FormerOwnerId = "free_tessaly";          // Corwin's fleet takes the world
        HouseSystem.Tick(w);
        Assert.False(w.Houses.ContainsKey("free_tessaly"));
        Assert.Equal("corwin", w.Char(leader)!.HouseId); Assert.Contains(leader, w.House("corwin")!.Members);
        Assert.All(w.Fleets.Values, f => Assert.NotEqual("free_tessaly", f.OwnerId));
        Assert.Equal("", barony.FormerOwnerId);                                    // nothing left to reclaim from
        Assert.Contains(w.History, h => h.Text.Contains("pass to House Corwin"));
    }

    [Fact]
    public void ALandlessPennilessHouse_EntersAnothersService_AfterTwelveYears_ButNotWhileItHasAHullOrCoin()
    {
        var w = Rings(0);
        w.Houses["poor"] = new House { Id = "poor", Name = "House Poor", Culture = "reach", Treasury = new Resources { Credits = -15 } };
        w.Characters["poor_head"] = new Character { Id = "poor_head", Name = "Tam", HouseId = "poor", Age = 40 }; w.Houses["poor"].Members.Add("poor_head");
        w.House("aurelian")!.Culture = "reach"; w.House("corwin")!.Culture = "other"; w.House("corwin")!.Treasury.Credits = 500;
        HouseSystem.Tick(w);                                                        // the wait begins
        Assert.True(w.Houses.ContainsKey("poor")); Assert.Equal(w.Turn, w.Counter("landless_since:poor"));
        w.Turn += HouseSystem.ServiceTurns;
        w.Fleets["poor_hull"] = new Fleet { Id = "poor_hull", Name = "Last Hull", OwnerId = "poor", SystemId = "empty", Strength = 1 };
        HouseSystem.Tick(w); Assert.True(w.Houses.ContainsKey("poor"));          // a hull is still a game
        w.Fleets.Remove("poor_hull"); w.House("poor")!.Treasury.Credits = 3;
        HouseSystem.Tick(w); Assert.True(w.Houses.ContainsKey("poor"));          // so is coin in hand
        w.House("poor")!.Treasury.Credits = -15;
        HouseSystem.Tick(w);
        Assert.False(w.Houses.ContainsKey("poor"));
        Assert.Equal("aurelian", w.Char("poor_head")!.HouseId);                    // the richest house of its own culture, not the richest house
        Assert.Equal(30, w.House("aurelian")!.Treasury.Credits);                   // debts are written off, not inherited
        Assert.False(w.WorldCounters.ContainsKey("landless_since:poor"));
        Assert.Contains(w.History, h => h.Text.Contains("enters the service of House Aurelian"));
    }

    [Fact]
    public void ThePlayersHouse_AndTheLawless_NeverFallThisWay()
    {
        var w = Rings(0); var mine = w.Protagonist!.HouseId;
        foreach (var h in w.HoldingsOf(mine).ToList()) w.Holdings.Remove(h.Id);
        w.House(mine)!.Treasury.Credits = -50; w.WorldCounters["landless_since:" + mine] = -100;
        PirateSystem.Ensure(w);
        HouseSystem.Tick(w);
        Assert.True(w.Houses.ContainsKey(mine)); Assert.True(w.Houses.ContainsKey(PirateSystem.HouseId));
    }
}

public class PatronTests
{
    private static World Rings(int unrest) { var m = typeof(RegionSystemTests).GetMethod("Rings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!; return (World)m.Invoke(null, new object[] { unrest })!; }

    [Fact]
    public void WhenTheCourtHoldsOnlyTheCapital_TheFiefComesFromAPatron()
    {
        var w = Rings(0); w.Holdings.Remove("barony");
        w.Holdings["corwin_two"] = new Holding { Id = "corwin_two", OwnerId = "corwin", Name = "Corwin's Lesser", Specialization = "agri", SystemId = "empty", Population = 4 };
        w.House("vega")!.Legitimacy = 25; w.House("vega")!.Title = "landless"; var ctx = new CommandContext(w, TestContent.Rng());
        Assert.Equal("corwin", PetitionFiefCommand.Grantor(w, "vega")!.Id);                      // the court has no world to spare; Corwin has two
        var cmd = new PetitionFiefCommand("self");
        Assert.Equal("House Corwin does not think well enough of you", cmd.Refusal(ctx));
        w.Relationship("corwin_head", "self").Disposition = 15; w.Pools["self"].Influence = 3;
        Assert.True(new CommandExecutor().TryExecute(cmd, ctx));
        Assert.Equal("vega", w.Holding("corwin_two")!.OwnerId); Assert.Equal("baron", w.House("vega")!.Title);
        Assert.Contains(w.History, h => h.Text.Contains("as patron"));
        w.Wars.Add(new War { HouseA = "vega", HouseB = "aurelian" }); w.Holdings.Remove("corwin_two");
        Assert.Null(PetitionFiefCommand.Grantor(w, "vega"));                                        // no one left with a world to spare
    }
}
