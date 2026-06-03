using Praetoria.Core.Events;
using Praetoria.Core.State;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Consequence application = choices write back into State (BuildSpec §4, GDD §15 L3).</summary>
public class ConsequenceTests
{
    [Fact]
    public void Choice_WritesFlagsRelationshipsAndSkills()
    {
        var w = TestContent.ThreeCadetWorld();
        var def = new EventDef
        {
            Id = "act",
            Roles = new[] { new RoleDef { Name = "target" } },
            Choices = new[]
            {
                new Choice
                {
                    Id = "strike",
                    Effects = new IEffect[]
                    {
                        new SetWorldFlagEffect("war_declared", true),
                        new SetCharFlagEffect("target", "wounded", true),
                        new AdjustRelationshipEffect("self", "target", -40),
                        new AdjustSkillEffect("self", "tactics", 2),
                        new AddBondEffect("self", "target", BondType.Sworn, 30)
                    }
                }
            }
        };
        var db = TestContent.Db(new[] { def });
        var engine = TestContent.Engine(w, db);

        var fired = engine.NextEvent(w)!;
        var target = fired.Binding.Resolve("target");
        engine.Resolve(fired, w, "strike");

        Assert.True(w.HasFlag("war_declared"));
        Assert.Contains("wounded", w.Char(target)!.Flags);
        Assert.Equal(-40, w.GetRelationship("self", target)!.Disposition);
        Assert.Equal(2, w.Char("self")!.Skill("tactics"));
        var bond = w.GetRelationship("self", target)!;
        Assert.Equal(BondType.Sworn, bond.Bond);
        Assert.Equal(30, bond.BondStrength);
    }

    [Fact]
    public void Disposition_IsClamped()
    {
        var w = TestContent.ThreeCadetWorld();
        w.Relationship("self", "rivalA").Disposition = 90;
        var ctx = new EvalContext(w, new Binding("self").With("r", "rivalA"), TestContent.Rng());
        new AdjustRelationshipEffect("self", "r", 50).Apply(ctx);
        Assert.Equal(100, w.GetRelationship("self", "rivalA")!.Disposition);
    }

    [Fact]
    public void GatedChoice_ThrowsWhenRequirementsUnmet()
    {
        var w = TestContent.ThreeCadetWorld(); // self has tactics 0
        var def = new EventDef
        {
            Id = "trial",
            Choices = new[]
            {
                new Choice
                {
                    Id = "hard",
                    Requirements = new ICondition[] { new SkillCondition("self", "tactics", CompareOp.Gte, 5) },
                    Effects = new IEffect[] { new SetWorldFlagEffect("passed", true) }
                }
            }
        };
        var db = TestContent.Db(new[] { def });
        var engine = TestContent.Engine(w, db);
        var fired = engine.NextEvent(w)!;

        Assert.False(engine.OfferChoices(fired, w)[0].Available);
        Assert.Throws<InvalidOperationException>(() => engine.Resolve(fired, w, "hard"));
        Assert.False(w.HasFlag("passed"));
    }
}
