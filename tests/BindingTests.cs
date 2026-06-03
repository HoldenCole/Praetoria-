using Praetoria.Core.Events;
using Praetoria.Core.State;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Binder = the "by ROLE not NAME" rule (BuildSpec §4). These prove a role resolves to a
/// character satisfying its constraints, and that an unsatisfiable role makes the event unbindable.</summary>
public class BindingTests
{
    [Fact]
    public void Role_BindsOnlyToCharacterSatisfyingConstraint()
    {
        var w = TestContent.ThreeCadetWorld();
        // self despises rivalA (-30) but is neutral-positive to rivalB (+10).
        w.Relationship("self", "rivalA").Disposition = -30;
        w.Relationship("self", "rivalB").Disposition = 10;

        // Role "foe" requires self->foe disposition < 0  => only rivalA qualifies.
        var def = new EventDef
        {
            Id = "e",
            Roles = new[]
            {
                new RoleDef
                {
                    Name = "foe",
                    Constraints = new ICondition[] { new RelationshipCondition("self", "foe", CompareOp.Lt, 0) }
                }
            }
        };

        var binding = Binder.TryBind(def, w, TestContent.Rng());
        Assert.NotNull(binding);
        Assert.Equal("rivalA", binding!.Resolve("foe"));
    }

    [Fact]
    public void Event_IsUnbindable_WhenNoCandidateSatisfiesRole()
    {
        var w = TestContent.ThreeCadetWorld();
        // Nobody bears the 'feud' flag, so a role requiring it cannot bind.
        var def = new EventDef
        {
            Id = "e",
            Roles = new[]
            {
                new RoleDef
                {
                    Name = "enemy",
                    Constraints = new ICondition[] { new CharFlagCondition("enemy", "feud", true) }
                }
            }
        };

        Assert.Null(Binder.TryBind(def, w, TestContent.Rng()));
    }

    [Fact]
    public void Self_IsAlwaysPreBoundToProtagonist()
    {
        var w = TestContent.ThreeCadetWorld();
        var binding = Binder.TryBind(new EventDef { Id = "e" }, w, TestContent.Rng());
        Assert.NotNull(binding);
        Assert.Equal("self", binding!.Resolve(Binding.Self));
    }

    [Fact]
    public void NoCharacterFillsTwoRoles()
    {
        var w = TestContent.ThreeCadetWorld();
        var def = new EventDef
        {
            Id = "e",
            Roles = new[]
            {
                new RoleDef { Name = "a" },
                new RoleDef { Name = "b" }
            }
        };
        var binding = Binder.TryBind(def, w, TestContent.Rng());
        Assert.NotNull(binding);
        Assert.NotEqual(binding!.Resolve("a"), binding.Resolve("b"));
        Assert.NotEqual("self", binding.Resolve("a"));
    }
}
