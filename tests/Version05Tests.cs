using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Rng;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The 0.5 headless pass: population feeds Manpower, the `era` condition, stress consequences,
/// regency, continue mode (a sworn house takes up the banner), NPC houses investing, the relationship
/// index, and the Imperium-start scenario.</summary>
public class Version05Tests
{
    private static (ContentDatabase content, string root) Load()
    {
        var root = ContentLocator.FindContentDir();
        return (ContentLoader.LoadFromDirectory(root), root);
    }

    // ---- population → manpower ----

    [Fact]
    public void Population_FeedsManpower_AndIsCapped_WhenTheSpecSaysSo()
    {
        var catalog = new HoldingCatalog(new[]
        {
            new HoldingSpec { Id = "agri", BaseYield = new Resources { Manpower = 3 }, Slots = 1, PopGrowth = 5, MaxPopulation = 24, PopManpower = 10 }
        }, Array.Empty<BuildingDef>());
        var w = TestContent.EconomyWorld();
        w.Holding("barony")!.Specialization = "agri";          // pop 20

        new Economy(catalog).Accrue(w);
        Assert.Equal(24, w.Holding("barony")!.Population);      // 20 + 5, capped at 24
        Assert.Equal(3 + 24 / 10, w.House("vega")!.Treasury.Manpower);   // spec 3 + 2 from people

        new Economy(catalog).Accrue(w);
        Assert.Equal(24, w.Holding("barony")!.Population);      // stays capped
    }

    [Fact]
    public void Population_IsDecorative_WhenTheSpecOmitsTheNewFields()
    {
        // The pre-0.5 contract (and the older economy tests) must hold unchanged by default.
        var w = TestContent.EconomyWorld();
        new Economy(TestContent.EconomyCatalog()).Accrue(w);
        Assert.Equal(3, w.House("vega")!.Treasury.Manpower);
        Assert.Equal(22, w.Holding("barony")!.Population);
    }

    // ---- era ----

    [Fact]
    public void EraCondition_ReadsTheWorldsStart()
    {
        var w = TestContent.ThreeCadetWorld();
        var ctx = new EvalContext(w, new Binding("self"), TestContent.Rng());
        Assert.True(new EraCondition("fractured_stars", true).Evaluate(ctx));
        Assert.False(new EraCondition("imperium", true).Evaluate(ctx));
        w.Era = "imperium";
        Assert.True(new EraCondition("imperium", true).Evaluate(ctx));
    }

    [Fact]
    public void ImperialCourtContent_FiresOnlyInTheImperiumStart()
    {
        var (content, root) = Load();
        var imperial = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, "the_imperial_court"), 1);
        var fractured = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, "the_fallen_house"), 1);
        imperial.Turn = fractured.Turn = 3;

        var inImperium = new EventEngine(content, WorldBuilder.RngFor(imperial)).GatherEligible(imperial).Select(e => e.def.Id).ToHashSet();
        var inFractured = new EventEngine(content, WorldBuilder.RngFor(fractured)).GatherEligible(fractured).Select(e => e.def.Id).ToHashSet();

        Assert.Contains("the_emperors_favor", inImperium);          // the sovereign binds by TITLE (emperor)
        Assert.DoesNotContain("the_emperors_favor", inFractured);
        Assert.DoesNotContain("the_court_of_whispers", inFractured);
    }

    [Fact]
    public void ImperialCourtScenario_StartsMarried_WithStartingFlags()
    {
        var (_, root) = Load();
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, "the_imperial_court"), 1);
        Assert.Equal("imperium", w.Era);
        Assert.Contains("wed", w.Protagonist!.Flags);                                   // scenario `flags`
        Assert.Equal(BondType.Marriage, w.GetRelationship("cassian_thorne", "elara_thorne")!.Bond);
        Assert.NotNull(DynastySystem.Spouse(w, w.Char("elara_thorne")!));
        Assert.Equal("emperor", w.House("aurelian")!.Title);
    }

    // ---- stress ----

    [Fact]
    public void Stress_BleedsOffEachYear_AndBreaksAtTheCeiling()
    {
        var w = TestContent.ThreeCadetWorld();
        var self = w.Char("self")!;
        self.Age = 20;
        self.Stress = 10;
        var sys = new DynastySystem();

        sys.Tick(w, TestContent.Rng());
        Assert.Equal(10 - DynastySystem.StressRelief, self.Stress);
        Assert.DoesNotContain(DynastySystem.BrokenFlag, self.Flags);

        self.Stress = DynastySystem.StressBreakPoint;
        sys.Tick(w, TestContent.Rng());
        Assert.Contains(DynastySystem.BrokenFlag, self.Flags);
        Assert.Equal(DynastySystem.StressAfterBreak, self.Stress);
        Assert.Equal(1, w.Counter("breakdowns"));
        Assert.Contains(w.History, h => h.Text.Contains("breaks under the strain"));
    }

    [Fact]
    public void TheBreakingPoint_IsAuthoredAgainstTheBrokenFlag_AndClearsIt()
    {
        var (content, root) = Load();
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, "academy_crucible"), 1);
        var engine = new EventEngine(content, WorldBuilder.RngFor(w));
        Assert.DoesNotContain("the_breaking_point", engine.GatherEligible(w).Select(e => e.def.Id));

        w.Protagonist!.Flags.Add(DynastySystem.BrokenFlag);
        var fired = engine.GatherEligible(w).First(e => e.def.Id == "the_breaking_point");
        engine.Resolve(new FiredEvent(fired.def, fired.binding), w, "gather_yourself");
        Assert.DoesNotContain(DynastySystem.BrokenFlag, w.Protagonist!.Flags);
    }

    // ---- regency ----

    [Fact]
    public void AChildHead_RulesThroughARegent_UntilMajority()
    {
        var w = TestContent.ThreeCadetWorld();
        w.Char("self")!.Age = DynastySystem.AgeOfMajority - 3;
        var sys = new DynastySystem();
        sys.Tick(w, TestContent.Rng());
        Assert.True(w.HasFlag(DynastySystem.RegencyFlag));
        w.Char("self")!.Age = DynastySystem.AgeOfMajority - 1;
        sys.Tick(w, TestContent.Rng());                          // turns 16 this year
        Assert.False(w.HasFlag(DynastySystem.RegencyFlag));
    }

    // ---- continue mode ----

    private static World LastOfTheLine(string mode)
    {
        var w = new World { ProtagonistId = "last", Mode = mode };
        w.Houses["vega"] = new House { Id = "vega", Name = "House Vega" };
        w.Houses["drake"] = new House { Id = "drake", Name = "House Drake" };
        w.Characters["last"] = new Character { Id = "last", Name = "last", HouseId = "vega", Age = 70 };
        w.Characters["sworn"] = new Character { Id = "sworn", Name = "sworn", HouseId = "drake", Age = 30 };
        w.Characters["stranger"] = new Character { Id = "stranger", Name = "stranger", HouseId = "drake", Age = 30 };
        w.Houses["vega"].Members.Add("last");
        w.Houses["drake"].Members.AddRange(new[] { "sworn", "stranger" });
        w.Relationship("sworn", "last").Bond = BondType.Sworn;       // sworn TO the dying head (one edge is enough)
        w.Relationship("sworn", "last").BondStrength = 70;
        w.Pools["last"] = ActionPools.ForPlayer();
        return w;
    }

    [Fact]
    public void Ironman_EndsTheRun_WhenTheBloodlineIsExtinct()
    {
        var w = LastOfTheLine(World.ModeIronman);
        DynastySystem.Die(w, "last");
        Assert.True(w.IsDynastyDead);
        Assert.Equal("last", w.ProtagonistId);
    }

    [Fact]
    public void ContinueMode_HandsTheGameToTheSwornHouse()
    {
        var w = LastOfTheLine(World.ModeContinue);
        DynastySystem.Die(w, "last");
        Assert.False(w.IsDynastyDead);
        Assert.True(w.HasFlag(DynastySystem.DynastyAdoptedFlag));
        Assert.Equal("sworn", w.ProtagonistId);                   // the sworn man, not the stranger
        Assert.Equal(1, w.Counter("adoptions"));
        Assert.Equal(ActionPools.ForPlayer().AgentsRegen, w.Pools["sworn"].AgentsRegen);
    }

    [Fact]
    public void ContinueMode_StillEnds_WhenNoSwornHouseRemains()
    {
        var w = LastOfTheLine(World.ModeContinue);
        w.Char("sworn")!.Alive = false;
        DynastySystem.Die(w, "last");
        Assert.True(w.IsDynastyDead);
    }

    [Fact]
    public void Mode_RoundTripsThroughASave()
    {
        var w = LastOfTheLine(World.ModeContinue);
        var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        Assert.Equal(World.ModeContinue, loaded.Mode);
    }

    // ---- NPC houses invest ----

    [Fact]
    public void NpcHouses_BuildWhenTheyCanAfford_OneBuildPerHousePerTurn()
    {
        var catalog = TestContent.EconomyCatalog();
        var w = TestContent.EconomyWorld(credits: 0, materials: 0);         // the PLAYER'S house is broke
        w.Houses["drake"] = new House { Id = "drake", Name = "House Drake", Treasury = new Resources { Credits = 10, Materials = 10 } };
        w.Characters["drake_lord"] = new Character { Id = "drake_lord", Name = "lord", HouseId = "drake", Age = 40 };
        w.Houses["drake"].Members.Add("drake_lord");
        w.Pools["drake_lord"] = ActionPools.ForNpc();
        w.Holdings["drake_port"] = new Holding { Id = "drake_port", OwnerId = "drake", Specialization = "forge", Population = 0 };  // 1 slot

        var executor = new CommandExecutor();
        new NpcAi().Act(executor, new CommandContext(w, TestContent.Rng()), null, catalog);

        Assert.Single(w.Holding("drake_port")!.Buildings);                   // one build, the first affordable in id order
        Assert.Contains(executor.Log, l => l.Contains("drake_lord: build"));
        Assert.Empty(w.Holding("barony")!.Buildings);                        // never touches the player's domain
    }

    // ---- relationship index ----

    [Fact]
    public void RelationshipIndex_SeesEdgesAddedBehindItsBack()
    {
        var w = TestContent.ThreeCadetWorld();
        Assert.Null(w.GetRelationship("self", "rivalA"));
        w.Relationships.Add(new Relationship { FromId = "self", ToId = "rivalA", Disposition = 7 });   // raw list append (scenario/load path)
        Assert.Equal(7, w.GetRelationship("self", "rivalA")!.Disposition);
        var created = w.Relationship("rivalA", "self");                      // get-or-create path
        Assert.Same(created, w.GetRelationship("rivalA", "self"));
        Assert.Equal(2, w.Relationships.Count);
    }

    [Fact]
    public void Version_IsStamped()
    {
        Assert.StartsWith("0.8.0", Praetoria.Core.PraetoriaVersion.Current);
    }
}
