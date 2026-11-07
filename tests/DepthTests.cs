using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

public class DepthTests
{
    private static (ContentDatabase content, World world) Fresh(string scenario, ulong seed = 1)
    {
        var root = ContentLocator.FindContentDir();
        var content = ContentLoader.LoadFromDirectory(root);
        return (content, WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, scenario), seed));
    }

    [Fact]
    public void TheOldLion_Loads_WithGenerationsFleetsAndAnAdmiral()
    {
        var (content, w) = Fresh("the_old_lion");
        Assert.Equal(68, w.Protagonist!.Age);
        Assert.Equal("corvin_castellan", w.Fleets["fleet_castellan"].AdmiralId);
        Assert.Equal(2, w.Fleets["fleet_castellan"].Ships["cruiser"]);
        Assert.Equal(4, w.Fleets["fleet_castellan"].Strength);
        Assert.Equal("corvin_castellan", DynastySystem.FindHeir(w, w.Protagonist)!.Id);
        var tc = new TurnController(w, content);
        for (int t = 0; t < 40 && !w.IsDynastyDead; t++) { foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); } tc.EndTurn(); }
        Assert.True(w.Counter("generation") >= 1, "the old lion should die and the seat pass within 40 years");
    }

    [Fact]
    public void FamilyAutonomy_MakesWillfulKinDefiant_AndEventuallyDefect()
    {
        var (content, w) = Fresh("the_old_lion");                     // Bastien: Ambitious/Vengeful, −35 toward Aurel
        var fam = new FamilySystem();
        fam.Tick(w);
        Assert.Contains(FamilySystem.DefiantFlag, w.Char("bastien_castellan")!.Flags);
        Assert.True(w.GetRelationship("bastien_castellan", "maren_oriel")!.Disposition > 20);   // courting the rival
        for (int i = 0; i < 30; i++) fam.Tick(w);
        Assert.Equal("oriel", w.Char("bastien_castellan")!.HouseId);                              // left for Oriel
        Assert.Contains("duke", w.House("oriel")!.Claims);                                       // taking a claim with him
        Assert.True(w.HasFlag("kin_defected"));
        Assert.DoesNotContain(FamilySystem.DefiantFlag, w.Char("ysolde_castellan")!.Flags);     // Loyal kin never defy

        var (_, w2) = Fresh("the_old_lion"); w2.FamilyAutonomy = 0;
        new FamilySystem().Tick(w2);
        Assert.DoesNotContain(FamilySystem.DefiantFlag, w2.Char("bastien_castellan")!.Flags);   // deferential setting
    }

    [Fact]
    public void PassedOverSiblings_ResentTheNewHead()
    {
        var (content, w) = Fresh("the_old_lion");
        var second = new Character { Id = "second_son", Name = "Second", HouseId = "castellan", Age = 30, Sex = "male", FatherId = "aurel_castellan" };
        w.Characters[second.Id] = second; w.House("castellan")!.Members.Add(second.Id);
        DynastySystem.Die(w, "aurel_castellan");
        Assert.Equal("corvin_castellan", w.ProtagonistId);
        Assert.Contains("passed_over", second.Flags);
        Assert.Equal(-20, w.GetRelationship("second_son", "corvin_castellan")!.Disposition);
    }

    [Fact]
    public void Elders_LoseTheirEdge_EveryFifthYear()
    {
        var (content, w) = Fresh("the_old_lion");
        var aurel = w.Protagonist!; aurel.Age = 69;                        // → 70 on tick
        var dyn = new DynastySystem { Content = content };
        int total = aurel.Skills.Values.Sum();
        dyn.Tick(w, new Praetoria.Core.Rng.SplitMix64Rng(99));
        if (aurel.Alive) Assert.Equal(total - 1, aurel.Skills.Values.Sum());
    }

    [Fact]
    public void Blockade_Starves_AndRevoltEmptiesAWorld()
    {
        var (content, w) = Fresh("academy_crucible");
        var eco = new Economy(content.Holdings);
        var barony = w.Holding("vega_barony")!;                             // pop 20
        w.Sieges["vega_barony"] = new Siege { HoldingId = "vega_barony", BesiegerId = "corwin", Turns = 0, Required = 2 };
        eco.Accrue(w);
        Assert.Equal(20 + 2 - 1, barony.Population);                         // growth, minus the siege's bite
        w.Sieges.Clear(); barony.Unrest = 85;
        eco.Accrue(w);
        Assert.Equal(21 + 1 - 2, barony.Population);                         // halved growth, flight of 2
    }

    [Fact]
    public void Recovery_HealsTheRealm_WhenNoCrisisIsActive()
    {
        var (content, w) = Fresh("academy_crucible");
        Assert.Equal(2, content.Recovery.Count);
        var engine = new CrisisEngine(content.Crises, content.Recovery);
        w.WorldCounters["legitimacy"] = -4; w.WorldCounters["unrest"] = 3;
        engine.Recover(w);
        Assert.Equal(-3, w.Counter("legitimacy")); Assert.Equal(2, w.Counter("unrest"));
        w.Crises["x"] = new ActiveCrisis { Id = "x" };
        engine.Recover(w);
        Assert.Equal(-3, w.Counter("legitimacy"));                           // not while a crisis rages
    }

    [Fact]
    public void Threat_IsWeightedByMagnitude()
    {
        var (content, w) = Fresh("academy_crucible");
        var s = new SphereSystem(content.Spheres);
        foreach (var c in w.Characters.Values) c.CareerRank = 0;
        w.Char("vega_marcus")!.CareerRank = 1;                                // the only Navy rank in the galaxy → 100% share
        s.Recompute(w);
        Assert.True(s.Threat(w, "vega") < SphereSystem.CoalitionThreshold, "one ensign owning 'the whole Navy' is not a threat");
        w.Char("vega_marcus")!.CareerRank = 6;
        s.Recompute(w);
        Assert.True(s.Threat(w, "vega") >= SphereSystem.CoalitionThreshold);
    }

    [Fact]
    public void Refit_RestoresReadiness_AtHome_ForMaterials()
    {
        var (content, w) = Fresh("academy_crucible");
        var ctx = new CommandContext(w, TestContent.Rng());
        var f = w.Fleets["fleet_vega"]; f.Readiness = 40;
        var cmd = new RefitFleetCommand(w.ProtagonistId, "fleet_vega");
        Assert.True(cmd.CanExecute(ctx));
        new CommandExecutor().TryExecute(cmd, ctx);
        Assert.Equal(100, f.Readiness);
        Assert.Equal(8 - RefitFleetCommand.MaterialsPerHull, w.House("vega")!.Treasury.Materials);
        f.Readiness = 40; f.SystemId = "anvil";
        Assert.False(cmd.CanExecute(ctx));                                    // not in foreign space
    }

    [Fact]
    public void Director_KeepsAmbientToOne_WhenWeightierReportsWait()
    {
        var (content, _) = Fresh("academy_crucible");
        for (ulong seed = 1; seed <= 15; seed++)
        {
            var (_, w) = Fresh("the_fallen_house", seed); w.Turn = 6;
            var briefing = new EventEngine(content, WorldBuilder.RngFor(w)).SelectBriefing(w, 3);
            if (briefing.Any(f => f.Def.Tier != EventTier.Ambient)) Assert.True(briefing.Count(f => f.Def.Tier == EventTier.Ambient) <= 1);
        }
    }

    [Fact]
    public void Saves_FromVersion1_Load_AndRecount()
    {
        var (content, w) = Fresh("academy_crucible");
        w.Fleets["fleet_vega"].Ships["cruiser"] = 3;                        // composition without the recount a v1 writer wouldn't do
        string json = SaveSystem.Serialize(w).Replace("\"Version\": 2", "\"Version\": 1");
        var loaded = SaveSystem.Deserialize(json);
        Assert.Equal(3, loaded.Fleets["fleet_vega"].Strength);
        Assert.Equal(World.ModeIronman, loaded.Mode);
    }

    [Fact]
    public void AdvisorReports_SpeakFromIntel_NotTruth()
    {
        var (content, w) = Fresh("academy_crucible");
        var reports = AdvisorReports.Build(w, content);
        Assert.Equal(3, reports.Count);
        Assert.Contains(reports, r => r.Seat == "Admiral" && r.Line.Contains("no reliable picture"));   // nothing seen yet
        new FleetSystem(content.Fleets).UpdateIntel(w);
        reports = AdvisorReports.Build(w, content);
        Assert.Contains(reports, r => r.Seat == "Admiral" && r.Line.Contains("House Corwin is reported at perhaps 2"));   // adjacent → hedged
        w.House("vega")!.Treasury.Credits = -3;
        Assert.Contains(AdvisorReports.Build(w, content), r => r.Seat == "Steward" && r.Warning && r.Line.Contains("insolvent"));
    }

    [Fact]
    public void SevenHonors_AndTheHouseholdEvents_Load()
    {
        var (content, w) = Fresh("the_old_lion");
        Assert.Equal(7, content.Honors.Count);
        new FamilySystem().Tick(w);
        var engine = new EventEngine(content, WorldBuilder.RngFor(w));
        var defiant = engine.GatherEligible(w).FirstOrDefault(e => e.def.Id == "the_defiant_kin");
        Assert.NotNull(defiant);
        Assert.Equal("bastien_castellan", defiant.binding.Resolve("kin"));
    }

    [Fact]
    public void Marriage_RefusesKin_WithinTwoGenerations()
    {
        var (content, w) = Fresh("the_circling_heir");
        var lucia = w.Protagonist!;                                    // Lucia's aunt Vera is her mother's sister; Castor is her brother
        var vera = w.Char("serval_aunt_vera")!; var castor = w.Char("serval_castor")!;
        var mother = new Character { Id = "serval_mother", Name = "Mother", HouseId = "serval", Age = 50, Sex = "female", MotherId = "serval_grandmother", Alive = false };
        var grand = new Character { Id = "serval_grandmother", Name = "Grandmother", HouseId = "serval", Age = 80, Sex = "female", Alive = false };
        w.Characters[mother.Id] = mother; w.Characters[grand.Id] = grand;
        lucia.MotherId = mother.Id; castor.MotherId = mother.Id; vera.MotherId = grand.Id;
        Assert.True(DynastySystem.AreKin(w, castor, vera));            // nephew and aunt share a grandmother
        Assert.True(DynastySystem.AreKin(w, castor, lucia));           // siblings
        Assert.False(DynastySystem.AreKin(w, castor, w.Char("rowena_thalen")!));

        var cmd = new ProposeMarriageCommand(w.ProtagonistId, castor.Id, vera.Id, content.Titles);
        Assert.Equal("too close in blood", cmd.Refusal(new CommandContext(w, TestContent.Rng())));
    }

    [Fact]
    public void Binder_NeverBindsChildren_UnlessTheRoleSpeaksOfAge()
    {
        var (content, w) = Fresh("the_circling_heir");
        var baby = new Character { Id = "serval_baby", Name = "Brisa", HouseId = "serval", Age = 0, Sex = "female", CareerRank = 9 };
        w.Characters[baby.Id] = baby;
        var steward = content.Events.First(e => e.Id == "the_skimming_steward");
        for (int i = 0; i < 20; i++)
        {
            var b = Praetoria.Core.Events.Binder.TryBind(steward, w, TestContent.Rng((ulong)i));
            if (b != null) Assert.NotEqual(baby.Id, b.Resolve("steward"));
        }
        var young = new Praetoria.Core.Events.EventDef { Id = "t", Roles = new[] { new Praetoria.Core.Events.RoleDef { Name = "child",
            Constraints = new Praetoria.Core.Events.ICondition[] { new Praetoria.Core.Events.AgeCondition("child", Praetoria.Core.Events.CompareOp.Lt, 10) } } } };
        var bound = Praetoria.Core.Events.Binder.TryBind(young, w, TestContent.Rng());
        Assert.NotNull(bound);
        Assert.Equal(baby.Id, bound!.Resolve("child"));
    }
}
