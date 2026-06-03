using Praetoria.Core.Events;
using Xunit;

namespace Praetoria.Tests;

/// <summary>
/// THE Milestone-1 acceptance property (BuildSpec §M1): a consequence in one event must change
/// State so that a *different*, unscripted event becomes eligible later. If this holds, the
/// engine's chaining is correct. Proven here in isolation, then again on the authored content.
/// </summary>
public class CascadeTests
{
    [Fact]
    public void ArmingAFlagInOneEvent_MakesAnotherEventEligibleLater()
    {
        var w = TestContent.ThreeCadetWorld();

        // Event A: insult a rival -> arms a per-character 'feud' flag. Nothing references duel here.
        var insult = new EventDef
        {
            Id = "insult",
            Roles = new[] { new RoleDef { Name = "rival" } },
            Choices = new[]
            {
                new Choice
                {
                    Id = "mock",
                    Effects = new IEffect[] { new SetCharFlagEffect("rival", "feud", true) }
                }
            }
        };

        // Event B: a duel — its role can ONLY bind to someone bearing 'feud'. Never sequenced after A.
        var duel = new EventDef
        {
            Id = "duel",
            Roles = new[]
            {
                new RoleDef
                {
                    Name = "rival",
                    Constraints = new ICondition[] { new CharFlagCondition("rival", "feud", true) }
                }
            },
            Choices = new[] { new Choice { Id = "fight" } }
        };

        var db = TestContent.Db(new[] { insult, duel });
        var engine = TestContent.Engine(w, db);

        // BEFORE: the duel is not eligible — no one bears the feud flag.
        var before = engine.GatherEligible(w).Select(e => e.def.Id).ToHashSet();
        Assert.Contains("insult", before);
        Assert.DoesNotContain("duel", before);

        // Resolve the insult (arming the flag).
        var firedInsult = engine.GatherEligible(w).First(e => e.def.Id == "insult");
        engine.Resolve(new FiredEvent(firedInsult.def, firedInsult.binding), w, "mock");

        // AFTER: the duel — never scripted to follow — is now eligible purely via emergent state.
        var after = engine.GatherEligible(w).Select(e => e.def.Id).ToHashSet();
        Assert.Contains("duel", after);
        Assert.DoesNotContain("insult", after); // non-repeatable, already fired
    }
}
