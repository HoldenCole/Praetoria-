using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Rng;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>War, battle, retreat, conquest and peace (GDD §6, §13 Military path).</summary>
public class CombatTests
{
    private static (ContentDatabase content, World world, FleetSystem fleets) Fresh(string scenario = "academy_crucible", ulong seed = 1)
    {
        var root = ContentLocator.FindContentDir();
        var content = ContentLoader.LoadFromDirectory(root);
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, scenario), seed);
        return (content, w, new FleetSystem(content.Fleets, content.Holdings));
    }

    [Fact]
    public void DeclareWar_AndPeace_AreCommands_WithFlagsContentCanRead()
    {
        var (content, w, _) = Fresh();
        var ctx = new CommandContext(w, TestContent.Rng());
        var declare = new DeclareWarCommand(w.ProtagonistId, "corwin", content.Fleets);
        Assert.True(declare.CanExecute(ctx));
        new CommandExecutor().TryExecute(declare, ctx);

        Assert.True(w.AtWar("vega", "corwin"));
        Assert.True(w.HasFlag(FleetSystem.AtWarFlag("corwin")));
        Assert.Equal(1, w.Counter(FleetSystem.WarsDeclaredCounter));
        Assert.False(declare.CanExecute(ctx));                                   // already at war
        Assert.True(w.GetRelationship("corwin_lucan", "vega_marcus")!.Disposition <= -30);

        var peace = new SueForPeaceCommand(w.ProtagonistId, "corwin", content.Fleets);
        Assert.True(peace.CanExecute(ctx));                                      // Corwin's 2 hulls vs our 1: under the refusal ratio
        new CommandExecutor().TryExecute(peace, ctx);
        Assert.False(w.AtWar("vega", "corwin"));
        Assert.False(w.HasFlag(FleetSystem.AtWarFlag("corwin")));
    }

    [Fact]
    public void Peace_IsRefused_WhileTheEnemySmellsVictory()
    {
        var (content, w, fleets) = Fresh();
        fleets.DeclareWar(w, "vega", "corwin");
        w.Fleets["fleet_corwin"].Strength = 5;                                   // 5 vs 1 ≥ ratio 2.0
        Assert.False(new SueForPeaceCommand(w.ProtagonistId, "corwin", content.Fleets).CanExecute(new CommandContext(w, TestContent.Rng())));
    }

    [Fact]
    public void NoBattle_WithoutAWar()
    {
        var (_, w, fleets) = Fresh();
        w.Fleets["fleet_corwin"].SystemId = "tessaly";                           // sharing Vega's system, at peace
        fleets.ResolveBattles(w, TestContent.Rng());
        Assert.Empty(fleets.LastBattles);
        Assert.Equal(0, w.Counter(FleetSystem.BattlesCounter));
    }

    [Fact]
    public void Battle_HasAWinner_LossesOnBothSides_AndTheLoserFallsBack()
    {
        var (_, w, fleets) = Fresh();
        fleets.DeclareWar(w, "vega", "corwin");
        w.Fleets["fleet_vega"].Strength = 4;
        w.Fleets["fleet_corwin"].Strength = 4;
        w.Fleets["fleet_corwin"].SystemId = "tessaly";                           // Corwin attacks Vega's seat

        fleets.ResolveBattles(w, new SplitMix64Rng(7));
        var r = Assert.Single(fleets.LastBattles);
        Assert.Equal("corwin", r.Attacker);
        Assert.Equal("vega", r.Defender);
        Assert.True(r.DefenderPower > r.AttackerPower);                           // defender bonus at equal hulls
        Assert.True(r.AttackerLosses + r.DefenderLosses >= 1);
        Assert.Equal(1, w.Counter(FleetSystem.BattlesCounter));
        Assert.True(w.HasFlag(FleetSystem.BattleFoughtFlag));

        string loser = r.Winner == "vega" ? "corwin" : "vega";
        var loserFleet = w.Fleets.Values.FirstOrDefault(f => f.OwnerId == loser);
        if (loserFleet != null)                                                   // survivors retreat toward home
        {
            Assert.True(loserFleet.Moving);
            Assert.Equal(loser == "corwin" ? "anvil" : "tessaly", loserFleet.DestinationId == "" ? loserFleet.SystemId : loserFleet.DestinationId);
        }
    }

    [Fact]
    public void Battle_IsDeterministic_FromTheSeed()
    {
        string Run(ulong seed)
        {
            var (_, w, fleets) = Fresh(seed: seed);
            fleets.DeclareWar(w, "vega", "corwin");
            w.Fleets["fleet_vega"].Strength = 3; w.Fleets["fleet_corwin"].Strength = 3; w.Fleets["fleet_corwin"].SystemId = "tessaly";
            fleets.ResolveBattles(w, new SplitMix64Rng(seed));
            var r = fleets.LastBattles[0];
            return $"{r.Winner}:{r.AttackerLosses}:{r.DefenderLosses}";
        }
        Assert.Equal(Run(3), Run(3));
    }

    [Fact]
    public void FortressHoldings_AndBatteries_RaiseDefensivePower()
    {
        var (_, w, fleets) = Fresh();
        double plain = fleets.Power(w, "sato", "sato_holdfast", defending: true);   // fortress spec: defense 0.5
        double attacking = fleets.Power(w, "sato", "sato_holdfast", defending: false);
        Assert.True(plain > attacking * 1.7, $"fortress defence should bite: {plain} vs {attacking}");
        Assert.Equal(0.5, fleets.Defense(w, "sato_holdfast", "sato"));
    }

    [Fact]
    public void AnUnopposedFleetInEnemySpace_TakesTheHolding_AndThePlayerConquestSpikesSeizures()
    {
        var (_, w, fleets) = Fresh();
        fleets.DeclareWar(w, "vega", "drake");
        w.Fleets.Remove("fleet_drake");                                           // Drake Crossing undefended
        w.Fleets["fleet_vega"].SystemId = "drake_crossing";

        fleets.ResolveConquests(w);                                               // siege begins (2 turns for a trade hub)
        Assert.Equal("drake", w.Holding("drake_port")!.OwnerId);
        Assert.True(w.Sieges.ContainsKey("drake_port"));
        fleets.ResolveConquests(w);                                               // the clock reaches the resistance
        Assert.Equal("vega", w.Holding("drake_port")!.OwnerId);
        Assert.False(w.Sieges.ContainsKey("drake_port"));
        Assert.Equal(1, w.Counter(FleetSystem.ConquestsCounter));
        Assert.Equal(1, w.Counter("seizures"));
        Assert.True(w.HasFlag(FleetSystem.ConquestMadeFlag));
        Assert.True(w.Holding("drake_port")!.Unrest >= 20);
        Assert.Contains(w.History, h => h.Text.Contains("takes Drake Crossing"));
    }

    [Fact]
    public void NoConquest_WhileTheOwnerStillHoldsTheSky()
    {
        var (_, w, fleets) = Fresh();
        fleets.DeclareWar(w, "vega", "drake");
        w.Fleets["fleet_vega"].SystemId = "drake_crossing";                       // Drake's picket is still there
        for (int t = 0; t < 4; t++) fleets.ResolveConquests(w);
        Assert.Equal("drake", w.Holding("drake_port")!.OwnerId);
        Assert.Empty(w.Sieges);
    }

    [Fact]
    public void AnNpcConquest_RaisesTheHoldingLostFlag_NotTheSeizuresCounter()
    {
        var (_, w, fleets) = Fresh();
        fleets.DeclareWar(w, "vega", "corwin");
        w.Fleets.Remove("fleet_vega");
        w.Fleets["fleet_corwin"].SystemId = "tessaly";
        for (int t = 0; t < 3; t++) fleets.ResolveConquests(w);
        Assert.Equal("corwin", w.Holding("vega_barony")!.OwnerId);
        Assert.True(w.HasFlag(FleetSystem.HoldingLostFlag));
        Assert.Equal(0, w.Counter("seizures"));
    }

    [Fact]
    public void HostileAndStronger_NpcDeclaresWar_AndSuesForPeaceWhenBroken()
    {
        var (content, w, fleets) = Fresh();
        w.Relationship("corwin_lucan", "vega_marcus").Disposition = -80;        // hates Marcus; Corwin 2 hulls > Vega 1
        var exec = new CommandExecutor();
        new NpcAi().Act(exec, new CommandContext(w, TestContent.Rng()), null, null, content.Fleets);
        Assert.True(w.AtWar("corwin", "vega"));

        w.Fleets["fleet_corwin"].Strength = 1; w.Fleets["fleet_vega"].Strength = 4;   // now badly outgunned
        foreach (var pools in w.Pools.Values) pools.Regenerate();                     // a new turn: pools refill
        new NpcAi().Act(exec, new CommandContext(w, TestContent.Rng()), null, null, content.Fleets);
        Assert.Contains(exec.Log, l => l.Contains("sue for peace"));
        Assert.True(w.AtWar("corwin", "vega"));                                   // the player decides, not the engine
        Assert.Contains(FleetSystem.SuingForPeaceFlag, w.Char("corwin_lucan")!.Flags);

        // ...through the envoy scene
        var engine = new Praetoria.Core.Events.EventEngine(content, WorldBuilder.RngFor(w));
        var envoy = engine.GatherEligible(w).First(e => e.def.Id == "the_envoy_of_peace");
        Assert.Equal("corwin_lucan", envoy.binding.Resolve("envoy"));
        engine.Resolve(new Praetoria.Core.Events.FiredEvent(envoy.def, envoy.binding), w, "accept_peace");
        Assert.False(w.AtWar("corwin", "vega"));
        Assert.DoesNotContain(FleetSystem.SuingForPeaceFlag, w.Char("corwin_lucan")!.Flags);
    }

    [Fact]
    public void WarEvents_ReadTheEngineFlags_AndClearThem()
    {
        var (content, w, fleets) = Fresh();
        var engine = new Praetoria.Core.Events.EventEngine(content, WorldBuilder.RngFor(w));
        w.Turn = 3;
        Assert.DoesNotContain("the_butchers_bill", engine.GatherEligible(w).Select(e => e.def.Id));
        w.WorldFlags.Add(FleetSystem.BattleFoughtFlag);
        var fired = engine.GatherEligible(w).First(e => e.def.Id == "the_butchers_bill");
        engine.Resolve(new Praetoria.Core.Events.FiredEvent(fired.def, fired.binding), w, "honor_the_dead");
        Assert.False(w.HasFlag(FleetSystem.BattleFoughtFlag));

        fleets.DeclareWar(w, "vega", "corwin");
        w.WorldFlags.Add(FleetSystem.HoldingLostFlag);
        var lost = engine.GatherEligible(w).First(e => e.def.Id == "the_lost_world");   // enemy role binds a house at war
        engine.Resolve(new Praetoria.Core.Events.FiredEvent(lost.def, lost.binding), w, "sue_for_terms");
        Assert.False(w.AtWar("vega", "corwin"));                                 // the makePeace effect
    }

    [Fact]
    public void AWar_PlaysOutThroughTheTurnLoop_Deterministically_AndSaves()
    {
        string Run()
        {
            var (content, w, _) = Fresh(seed: 11);
            var tc = new TurnController(w, content);
            w.Relationship("corwin_lucan", "vega_marcus").Disposition = -90;
            w.Fleets["fleet_corwin"].Strength = 4;
            for (int t = 0; t < 12; t++)
            {
                foreach (var item in tc.BeginTurn()) { var c = tc.Offer(item).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(item, c.Choice.Id); }
                tc.EndTurn();
            }
            tc.SyncRng();
            var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(w));
            Assert.Equal(w.Wars.Count, loaded.Wars.Count);
            return $"{w.Counter(FleetSystem.BattlesCounter)}|{w.Counter(FleetSystem.WarsDeclaredCounter)}|{w.Holding("vega_barony")!.OwnerId}|{w.RngState}";
        }
        var a = Run();
        Assert.Equal(a, Run());
        Assert.True(int.Parse(a.Split('|')[1]) >= 1, "a house that hates and outguns you should go to war");
    }
}
