using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Galaxy;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The fleet layer (GDD §6/§8/§19): transit along lanes, upkeep, incursions, raising ships, and the
/// protagonist house's reported, decaying intel on rival fleets.</summary>
public class FleetTests
{
    private static (ContentDatabase content, World world) Fresh(string scenario = "academy_crucible", ulong seed = 1)
    {
        var root = ContentLocator.FindContentDir();
        var content = ContentLoader.LoadFromDirectory(root);
        return (content, WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, scenario, includeBaseline: false), seed));
    }

    [Fact]
    public void ScenarioFleets_LoadIntoTheirSystems_AndRulesLoad()
    {
        var (content, w) = Fresh();
        Assert.Equal(4, w.Fleets.Count);
        Assert.Equal("tessaly", w.Fleets["fleet_vega"].SystemId);
        Assert.Equal(6, content.Fleets.ShipCost.Materials);
        var (_, imperial) = Fresh("the_imperial_court");
        Assert.True(imperial.Fleets["fleet_imperial_home"].Office);          // the §8 office fleet
    }

    [Fact]
    public void MoveOrder_TakesOneJumpPerTurn_AndErodesReadiness()
    {
        var (content, w) = Fresh();
        var sys = new FleetSystem(content.Fleets);
        var ctx = new CommandContext(w, TestContent.Rng());
        var cmd = new MoveFleetCommand(w.ProtagonistId, "fleet_vega", "drake_crossing", content.Fleets);   // tessaly→anvil→drake_crossing = 2
        Assert.True(cmd.CanExecute(ctx));
        new CommandExecutor().TryExecute(cmd, ctx);

        var f = w.Fleets["fleet_vega"];
        Assert.True(f.Moving);
        Assert.Equal(2, f.Eta);
        sys.Tick(w);
        Assert.True(f.Moving);
        Assert.Equal("tessaly", f.SystemId);
        Assert.Equal(95, f.Readiness);
        sys.Tick(w);
        Assert.False(f.Moving);
        Assert.Equal("drake_crossing", f.SystemId);
    }

    [Fact]
    public void ArrivingInAnotherHousesSpace_IsAnIncursion()
    {
        var (content, w) = Fresh();
        var sys = new FleetSystem(content.Fleets);
        var f = w.Fleets["fleet_vega"];
        f.DestinationId = "anvil"; f.Eta = 1;                       // Corwin's system
        sys.Tick(w);
        Assert.Equal(1, w.Counter(FleetSystem.IncursionsCounter));
        Assert.True(w.HasFlag(FleetSystem.IncursionFlag("corwin")));
        Assert.Contains(w.History, h => h.Text.Contains("House Corwin's space"));
    }

    [Fact]
    public void MoveOrder_IsRefused_ForOtherHousesFleets_UnreachableTargets_AndWhileUnderway()
    {
        var (content, w) = Fresh();
        var ctx = new CommandContext(w, TestContent.Rng());
        Assert.False(new MoveFleetCommand(w.ProtagonistId, "fleet_corwin", "tessaly", content.Fleets).CanExecute(ctx));   // not ours
        Assert.False(new MoveFleetCommand(w.ProtagonistId, "fleet_vega", "tessaly", content.Fleets).CanExecute(ctx));     // already there
        Assert.False(new MoveFleetCommand(w.ProtagonistId, "fleet_vega", "nowhere", content.Fleets).CanExecute(ctx));
        w.Fleets["fleet_vega"].DestinationId = "anvil"; w.Fleets["fleet_vega"].Eta = 1;
        Assert.False(new MoveFleetCommand(w.ProtagonistId, "fleet_vega", "sato_holdfast", content.Fleets).CanExecute(ctx)); // underway
    }

    [Fact]
    public void Upkeep_IsPaidPerHull_FromTheOwningTreasury()
    {
        var (content, w) = Fresh();
        var sys = new FleetSystem(content.Fleets);
        int before = w.House("corwin")!.Treasury.Credits;             // Corwin fleet strength 2
        sys.Tick(w);
        Assert.Equal(before - 2 * content.Fleets.UpkeepPerStrength.Credits, w.House("corwin")!.Treasury.Credits);
    }

    [Fact]
    public void RaiseFleet_SpendsTheTreasury_AndReinforcesOrFounds()
    {
        var (content, w) = Fresh();
        var house = w.House("vega")!;
        house.Treasury.Materials = 20; house.Treasury.Manpower = 10; house.Treasury.Credits = 10;
        var ctx = new CommandContext(w, TestContent.Rng());

        var cmd = new RaiseFleetCommand(w.ProtagonistId, "vega_barony", content.Fleets, content.ShipClasses, "frigate", content.Titles);
        Assert.True(cmd.CanExecute(ctx));
        new CommandExecutor().TryExecute(cmd, ctx);
        Assert.Equal(2, w.Fleets["fleet_vega"].Strength);           // reinforced the squadron already at Tessaly
        Assert.Equal(2, w.Fleets["fleet_vega"].Ships["frigate"]);  // the default composition was materialised
        Assert.Equal(14, house.Treasury.Materials);

        w.Fleets["fleet_vega"].DestinationId = "anvil"; w.Fleets["fleet_vega"].Eta = 1;   // away → a new squadron is founded
        new CommandExecutor().TryExecute(new RaiseFleetCommand(w.ProtagonistId, "vega_barony", content.Fleets, content.ShipClasses, "frigate", content.Titles), ctx);
        Assert.Equal(2, w.Fleets.Values.Count(f => f.OwnerId == "vega"));
        Assert.Equal(2, w.Counter("ships_raised"));

        Assert.False(new RaiseFleetCommand(w.ProtagonistId, "corwin_forge", content.Fleets).CanExecute(ctx));   // not our holding
    }

    [Fact]
    public void Intel_SeesNeighbours_AndDecaysWhenOutOfSight()
    {
        var (content, w) = Fresh();                                  // Vega at Tessaly; Corwin at Anvil (adjacent); Drake at Drake Crossing (2 away)
        var sys = new FleetSystem(content.Fleets);
        sys.UpdateIntel(w);
        Assert.Equal(content.Fleets.ConfidenceAdjacent, w.Intel["fleet_corwin"].Confidence);
        Assert.False(w.Intel.ContainsKey("fleet_drake"));            // never seen

        w.Fleets["fleet_corwin"].SystemId = "greywater";             // slips out of sight
        sys.UpdateIntel(w);
        Assert.Equal(content.Fleets.ConfidenceAdjacent - content.Fleets.IntelDecay, w.Intel["fleet_corwin"].Confidence);
        Assert.Equal("anvil", w.Intel["fleet_corwin"].SystemId);     // last-known position stands

        w.Fleets["fleet_corwin"].SystemId = "tessaly";               // in our territory → full confidence
        sys.UpdateIntel(w);
        Assert.Equal(100, w.Intel["fleet_corwin"].Confidence);
    }

    [Fact]
    public void Projection_ShowsOwnFleetsExact_AndRivalsOnlyAsIntel()
    {
        var (content, w) = Fresh();
        new FleetSystem(content.Fleets).UpdateIntel(w);
        var fleets = GalaxyProjection.Fleets(w);
        Assert.Contains(fleets, f => f.FleetId == "fleet_vega" && f.IsPlayer && f.Confidence == 100 && f.Readiness == 100);
        Assert.Contains(fleets, f => f.FleetId == "fleet_corwin" && !f.IsPlayer && f.Confidence == content.Fleets.ConfidenceAdjacent);
        Assert.DoesNotContain(fleets, f => f.FleetId == "fleet_drake");   // unreported → not on the map
    }

    [Fact]
    public void FleetStrengthCondition_ReadsTheHousesHulls()
    {
        var (_, w) = Fresh();
        var ctx = new Praetoria.Core.Events.EvalContext(w, new Praetoria.Core.Events.Binding("vega_marcus"), TestContent.Rng());
        Assert.True(new Praetoria.Core.Events.FleetStrengthCondition("self", Praetoria.Core.Events.CompareOp.Eq, 1).Evaluate(ctx));
        Assert.False(new Praetoria.Core.Events.FleetStrengthCondition("self", Praetoria.Core.Events.CompareOp.Gte, 2).Evaluate(ctx));
    }

    [Fact]
    public void HostileNpc_SendsItsFleetToward_ThePlayersSeat()
    {
        var (content, w) = Fresh();
        w.Relationship("corwin_lucan", "vega_marcus").Disposition = -60;     // Lucan despises Marcus
        var executor = new CommandExecutor();
        new NpcAi().Act(executor, new CommandContext(w, TestContent.Rng()), null, null, content.Fleets);
        var corwin = w.Fleets["fleet_corwin"];
        Assert.True(corwin.Moving);
        Assert.Equal("tessaly", corwin.DestinationId);
        Assert.Contains(executor.Log, l => l.Contains("corwin_lucan: move fleet_corwin"));
    }

    [Fact]
    public void Fleets_SurviveASave_AndTheTurnStaysDeterministic()
    {
        var (content, w) = Fresh();
        var tc = new TurnController(w, content);
        for (int t = 0; t < 6; t++) { tc.BeginTurn(); tc.EndTurn(); }
        tc.SyncRng();
        var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        Assert.Equal(w.Fleets.Count, loaded.Fleets.Count);
        Assert.Equal(w.Intel.Count, loaded.Intel.Count);
        foreach (var f in w.Fleets.Values)
            Assert.Equal((f.SystemId, f.Strength, f.Readiness, f.DestinationId, f.Eta), (loaded.Fleets[f.Id].SystemId, loaded.Fleets[f.Id].Strength, loaded.Fleets[f.Id].Readiness, loaded.Fleets[f.Id].DestinationId, loaded.Fleets[f.Id].Eta));
    }
}
