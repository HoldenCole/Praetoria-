using Praetoria.Core.Events;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Eligibility = conditions hold AND roles bind (BuildSpec §4).</summary>
public class EligibilityTests
{
    private static EventDef GatedByFlag() => new()
    {
        Id = "gated",
        Conditions = new ICondition[] { new WorldFlagCondition("door_open", true) },
        Choices = new[] { new Choice { Id = "ok" } }
    };

    [Fact]
    public void Event_NotEligible_WhenConditionFails()
    {
        var w = TestContent.ThreeCadetWorld();
        var db = TestContent.Db(new[] { GatedByFlag() });
        var engine = TestContent.Engine(w, db);

        Assert.Empty(engine.GatherEligible(w));
    }

    [Fact]
    public void Event_BecomesEligible_WhenConditionMet()
    {
        var w = TestContent.ThreeCadetWorld();
        w.WorldFlags.Add("door_open");
        var db = TestContent.Db(new[] { GatedByFlag() });
        var engine = TestContent.Engine(w, db);

        var eligible = engine.GatherEligible(w);
        Assert.Single(eligible);
        Assert.Equal("gated", eligible[0].def.Id);
    }

    [Fact]
    public void NonRepeatableEvent_DropsOut_AfterFiring()
    {
        var w = TestContent.ThreeCadetWorld();
        w.WorldFlags.Add("door_open");
        var def = GatedByFlag(); // non-repeatable by default
        var db = TestContent.Db(new[] { def });
        var engine = TestContent.Engine(w, db);

        var fired = engine.NextEvent(w)!;
        engine.Resolve(fired, w, "ok");

        Assert.Empty(engine.GatherEligible(w));
    }

    [Fact]
    public void RepeatableEvent_StaysEligible_AfterFiring()
    {
        var w = TestContent.ThreeCadetWorld();
        var def = new EventDef { Id = "rep", Repeatable = true, Choices = new[] { new Choice { Id = "x" } } };
        var db = TestContent.Db(new[] { def });
        var engine = TestContent.Engine(w, db);

        var fired = engine.NextEvent(w)!;
        engine.Resolve(fired, w, "x");

        Assert.Single(engine.GatherEligible(w));
    }
}
