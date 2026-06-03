using Praetoria.Core.State;
using Xunit;

namespace Praetoria.Tests;

public class ActionPoolsTests
{
    [Fact]
    public void CanAfford_And_Spend()
    {
        var p = new ActionPools { Influence = 3, Treasury = 1, Agents = 2 };
        var cost = new Dictionary<string, int> { [Pool.Influence] = 2, [Pool.Agents] = 1 };

        Assert.True(p.CanAfford(cost));
        p.Spend(cost);
        Assert.Equal(1, p.Influence);
        Assert.Equal(1, p.Agents);
        Assert.Equal(1, p.Treasury);
    }

    [Fact]
    public void CanAfford_False_WhenShort()
    {
        var p = new ActionPools { Influence = 1 };
        Assert.False(p.CanAfford(new Dictionary<string, int> { [Pool.Influence] = 2 }));
    }

    [Fact]
    public void Regenerate_AddsRegen_ClampedToCap()
    {
        var p = new ActionPools { Influence = 8, InfluenceRegen = 3, Cap = 9 };
        p.Regenerate();
        Assert.Equal(9, p.Influence); // 8+3 clamped to 9
    }

    [Fact]
    public void Spend_NeverGoesNegative()
    {
        var p = new ActionPools { Treasury = 1 };
        p.Spend(new Dictionary<string, int> { [Pool.Treasury] = 5 });
        Assert.Equal(0, p.Treasury);
    }
}
