using Praetoria.Core.Events;
using Praetoria.Core.State;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The resource consequence/predicate vocabulary (GDD §17): events can read and move a
/// house treasury so the Steward can surface domain decisions ("credits dry — raise taxes?").</summary>
public class ResourceVocabularyTests
{
    private static EvalContext Ctx(World w) =>
        new(w, new Binding("self"), TestContent.Rng());

    [Fact]
    public void AdjustResource_MovesTheRolesHouseTreasury()
    {
        var w = TestContent.EconomyWorld(credits: 10);
        new AdjustResourceEffect("self", Resource.Credits, -4).Apply(Ctx(w));
        Assert.Equal(6, w.House("vega")!.Treasury.Credits);
    }

    [Fact]
    public void AdjustResource_AllowsNegativeCredits_ButClampsOthers()
    {
        var w = TestContent.EconomyWorld(credits: 2);
        new AdjustResourceEffect("self", Resource.Credits, -9).Apply(Ctx(w));
        new AdjustResourceEffect("self", Resource.Materials, -100).Apply(Ctx(w));
        Assert.Equal(-7, w.House("vega")!.Treasury.Credits);   // insolvency allowed
        Assert.Equal(0, w.House("vega")!.Treasury.Materials);  // clamped
    }

    [Fact]
    public void ResourceCondition_ComparesAgainstTheTreasury()
    {
        var w = TestContent.EconomyWorld(credits: 0);
        w.House("vega")!.Treasury.Credits = -1;
        Assert.True(new ResourceCondition("self", Resource.Credits, CompareOp.Lt, 0).Evaluate(Ctx(w)));
        Assert.False(new ResourceCondition("self", Resource.Credits, CompareOp.Gte, 0).Evaluate(Ctx(w)));
    }

    [Fact]
    public void Vocabulary_ParsesFromJson()
    {
        // The data mini-language wires up exactly as the authored content would express it.
        var effect = Praetoria.Core.Data.EffectParser.Parse(
            System.Text.Json.JsonDocument.Parse(
                """{ "type": "adjustResource", "resource": "credits", "delta": 5 }""").RootElement);
        var cond = Praetoria.Core.Data.ConditionParser.Parse(
            System.Text.Json.JsonDocument.Parse(
                """{ "type": "resource", "resource": "credits", "op": "gte", "value": 5 }""").RootElement);

        var w = TestContent.EconomyWorld(credits: 0);
        effect.Apply(Ctx(w));
        Assert.Equal(5, w.House("vega")!.Treasury.Credits);
        Assert.True(cond.Evaluate(Ctx(w)));
    }
}
