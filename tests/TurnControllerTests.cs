using Praetoria.Core.Events;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The formal turn structure (GDD §9, BuildSpec §M2): Briefing → Action → Resolve, with
/// pools and NPC actions.</summary>
public class TurnControllerTests
{
    private static World CadetWorldWithPools()
    {
        var w = TestContent.ThreeCadetWorld();
        w.Pools["self"] = ActionPools.ForPlayer();
        w.Pools["rivalA"] = ActionPools.ForNpc();
        w.Pools["rivalB"] = ActionPools.ForNpc();
        return w;
    }

    [Fact]
    public void BeginTurn_EntersActionPhase_AndAdvancesCounter()
    {
        var w = CadetWorldWithPools();
        var tc = new TurnController(w, TestContent.Db(Array.Empty<EventDef>()));
        Assert.Equal(TurnPhase.Idle, tc.Phase);

        tc.BeginTurn();
        Assert.Equal(TurnPhase.Action, tc.Phase);
        Assert.Equal(1, w.Turn);
    }

    [Fact]
    public void Offer_MarksUnaffordableChoiceUnavailable()
    {
        var w = CadetWorldWithPools();
        w.PoolsFor("self").Influence = 1; // need 5 below

        var def = new EventDef
        {
            Id = "pricey",
            Repeatable = true,
            Choices = new[]
            {
                new Choice { Id = "expensive", Cost = new Dictionary<string, int> { [Pool.Influence] = 5 } }
            }
        };
        var tc = new TurnController(w, TestContent.Db(new[] { def }), briefingBudget: 1);
        var briefing = tc.BeginTurn();

        Assert.Single(briefing);
        var offered = tc.Offer(briefing[0]);
        Assert.False(offered[0].Available); // requirements met, but can't afford
    }

    [Fact]
    public void Resolve_OutsideActionPhase_Throws()
    {
        var w = CadetWorldWithPools();
        var def = new EventDef { Id = "e", Repeatable = true, Choices = new[] { new Choice { Id = "x" } } };
        var tc = new TurnController(w, TestContent.Db(new[] { def }), briefingBudget: 1);
        var briefing = tc.BeginTurn();
        var item = briefing[0];
        tc.EndTurn(); // now Idle

        Assert.Throws<InvalidOperationException>(() => tc.Resolve(item, "x"));
    }

    [Fact]
    public void EndTurn_RunsNpcActionsThroughTheCommandBus()
    {
        var w = CadetWorldWithPools();
        // Make rivalA aggressive so it issues a needle command.
        w.Char("rivalA")!.NatureTraits.Add("Arrogant");

        var tc = new TurnController(w, TestContent.Db(Array.Empty<EventDef>()));
        tc.BeginTurn();
        int before = tc.Executor.Log.Count;
        tc.EndTurn();

        Assert.True(tc.Executor.Log.Count > before, "NPCs should have acted during Resolve.");
        Assert.Contains(tc.Executor.Log, l => l.Contains("rivalA"));
    }

    [Fact]
    public void Pools_RefillEachTurn()
    {
        var w = CadetWorldWithPools();
        var tc = new TurnController(w, TestContent.Db(Array.Empty<EventDef>()));

        tc.BeginTurn();
        int afterFirst = tc.PlayerPools.Influence;
        tc.EndTurn();
        tc.BeginTurn();
        int afterSecond = tc.PlayerPools.Influence;

        Assert.True(afterSecond >= afterFirst, "Unspent pools should regenerate (capped) each turn.");
        Assert.True(afterSecond <= tc.PlayerPools.Cap);
    }
}
