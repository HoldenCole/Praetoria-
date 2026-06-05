using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Progression;
using Praetoria.Core.Rng;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The progression system (GDD §13): the title ladder, the legitimacy soft-lock (a
/// modifier, not a gate), and the three rise paths at their three exchange rates.</summary>
public class ProgressionTests
{
    private static TitleCatalog Ladder() => new(new[]
    {
        new TitleDef { Id = "landless", Name = "Landless", Rank = 0, LegitimacyRequirement = 0 },
        new TitleDef { Id = "knight", Name = "Knight", Rank = 1, LegitimacyRequirement = 10 },
        new TitleDef { Id = "baron", Name = "Baron", Rank = 2, LegitimacyRequirement = 25 },
        new TitleDef { Id = "count", Name = "Count", Rank = 3, LegitimacyRequirement = 40 },
        new TitleDef { Id = "duke", Name = "Duke", Rank = 4, LegitimacyRequirement = 60 }
    });

    private static World PWorld(string title = "baron", int legit = 28, int rank = 3)
    {
        var w = new World { ProtagonistId = "marcus" };
        w.Houses["vega"] = new House { Id = "vega", Name = "House Vega", Title = title, Legitimacy = legit };
        w.Characters["marcus"] = new Character { Id = "marcus", Name = "Marcus", HouseId = "vega", Alive = true, CareerTrack = "military", CareerRank = rank };
        w.Houses["vega"].Members.Add("marcus");
        w.Pools["marcus"] = ActionPools.ForPlayer();
        return w;
    }

    [Fact]
    public void SoftLock_NoInstability_WhenTitleIsLegitimate()
    {
        var w = PWorld("baron", 28);                  // baron needs 25; 28 ≥ 25
        new ProgressionSystem(Ladder()).Apply(w);
        Assert.Equal(0, w.Counter("title_instability"));
        Assert.Equal(0, w.Counter("unrest"));
        Assert.Equal(28, w.House("vega")!.Legitimacy);   // no drift when already legitimate
    }

    [Fact]
    public void SoftLock_BreedsInstabilityAndUnrest_WhenHoldingAboveLegitimacy()
    {
        var w = PWorld("duke", 30);                   // duke needs 60; gap 30
        new ProgressionSystem(Ladder()).Apply(w);
        Assert.Equal(30, w.Counter("title_instability"));
        Assert.Equal(2, w.Counter("unrest"));          // grumble = min(3, 1 + 30/20)
        Assert.Equal(31, w.House("vega")!.Legitimacy);   // slowly legitimised by holding on
    }

    [Fact]
    public void Military_SeizesTitle_IgnoringLegitimacy_SpikingFear()
    {
        var w = PWorld("baron", 28, rank: 3);
        var prog = new ProgressionSystem(Ladder());
        Assert.True(prog.CanSeize(w, "marcus"));
        prog.Seize(w, "marcus");

        Assert.Equal("count", w.House("vega")!.Title);      // baron → count by force
        Assert.Equal(28, w.House("vega")!.Legitimacy);       // legitimacy ignored
        Assert.Equal(1, w.Counter("seizures"));             // feeds §7 threat
        prog.Apply(w);
        Assert.Equal(12, w.Counter("title_instability"));   // count needs 40; gap 12 — a usurper's keg
    }

    [Fact]
    public void Military_Seize_Blocked_WithoutMartialPower()
    {
        var w = PWorld("baron", 28, rank: 1);              // not yet a real officer
        Assert.False(new ProgressionSystem(Ladder()).CanSeize(w, "marcus"));
    }

    [Fact]
    public void Merit_Petition_OnlyWhenLegitimate_ArrivesClean()
    {
        var w = PWorld("baron", 45);                       // ≥ count's requirement of 40
        var prog = new ProgressionSystem(Ladder());
        Assert.True(prog.CanPetition(w, "marcus"));
        prog.Petition(w, "marcus");

        Assert.Equal("count", w.House("vega")!.Title);
        Assert.Equal(50, w.House("vega")!.Legitimacy);       // +5, clean
        prog.Apply(w);
        Assert.Equal(0, w.Counter("title_instability"));    // granted, so no instability
    }

    [Fact]
    public void Merit_Petition_Blocked_WhenNotLegitimateEnough()
    {
        var w = PWorld("baron", 30);                       // count needs 40; the soft-lock caps the grant
        Assert.False(new ProgressionSystem(Ladder()).CanPetition(w, "marcus"));
    }

    [Fact]
    public void Intrigue_ClaimByBlood_NeedsClaim_LaundersLegitimacy_LeavesATrail()
    {
        var w = PWorld("baron", 30);
        var prog = new ProgressionSystem(Ladder());
        Assert.False(prog.CanClaimByBlood(w, "marcus"));   // no claim in hand

        w.House("vega")!.Claims.Add("count");
        Assert.True(prog.CanClaimByBlood(w, "marcus"));
        prog.ClaimByBlood(w, "marcus");

        Assert.Equal("count", w.House("vega")!.Title);
        Assert.Equal(42, w.House("vega")!.Legitimacy);       // +12, laundered through bloodline
        Assert.DoesNotContain("count", w.House("vega")!.Claims); // claim consumed
        Assert.Equal(1, w.Counter("corruption"));           // the scheme leaves a trail
    }

    [Fact]
    public void TitleCommands_RouteThroughBus_AndSpendPools()
    {
        var w = PWorld("baron", 45, rank: 3);
        var prog = new ProgressionSystem(Ladder());
        var exec = new CommandExecutor();
        var ctx = new CommandContext(w, TestContent.Rng());
        int influence = w.PoolsFor("marcus").Influence;

        Assert.True(exec.TryExecute(new PetitionTitleCommand("marcus", prog), ctx));
        Assert.Equal("count", w.House("vega")!.Title);
        Assert.Equal(influence - 2, w.PoolsFor("marcus").Influence);
        Assert.Contains(exec.Log, l => l.Contains("petition for title"));
    }

    [Fact]
    public void GrantClaimEffect_AndTitleCondition_Work()
    {
        var w = PWorld("baron", 30);
        var ctx = new EvalContext(w, new Binding("marcus"), TestContent.Rng());

        new GrantClaimEffect("self", "count").Apply(ctx);
        Assert.Contains("count", w.House("vega")!.Claims);

        Assert.True(new TitleCondition("self", "baron", true).Evaluate(ctx));
        Assert.False(new TitleCondition("self", "duke", true).Evaluate(ctx));

        var parsed = ConditionParser.Parse(System.Text.Json.JsonDocument.Parse(
            """{ "type": "title", "title": "baron" }""").RootElement);
        Assert.True(parsed.Evaluate(ctx));
    }

    [Fact]
    public void SoftLock_FeedsTheContestedTitleCrisis()
    {
        // A usurper holds a Duke title (real ladder needs 60) on legitimacy 30 — a contested claim.
        var content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
        var prog = new ProgressionSystem(content.Titles);
        var crises = new CrisisEngine(content.Crises);
        var w = PWorld("duke", 30);

        prog.Apply(w);
        Assert.True(w.Counter("title_instability") >= 20);
        Assert.True(crises.IsCausable(crises.Def("contested_title")!, w, new SplitMix64Rng(1)));
    }
}
