using Praetoria.Core.State;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The five-resource bundle (GDD §17): arithmetic, affordability, and the deliberate
/// asymmetry that Credits may go negative while the other four clamp at zero.</summary>
public class ResourcesTests
{
    [Fact]
    public void GetSetAdd_RoundTripByKey()
    {
        var r = new Resources();
        r.Set(Resource.Materials, 5);
        r.Add(Resource.Materials, 3);
        Assert.Equal(8, r.Get(Resource.Materials));
        Assert.Equal(8, r.Materials);
        Assert.Equal(0, r.Get(Resource.Exotics));
    }

    [Fact]
    public void AddBundle_IsFieldWise()
    {
        var r = new Resources { Credits = 1, Materials = 2 };
        r.Add(new Resources { Credits = 4, Manpower = 3 });
        Assert.Equal(5, r.Credits);
        Assert.Equal(2, r.Materials);
        Assert.Equal(3, r.Manpower);
    }

    [Fact]
    public void CanAfford_GatesOnEveryResource()
    {
        var bank = new Resources { Credits = 5, Materials = 5 };
        Assert.True(bank.CanAfford(new Resources { Credits = 5, Materials = 4 }));
        Assert.False(bank.CanAfford(new Resources { Credits = 6 }));
        Assert.False(bank.CanAfford(new Resources { Exotics = 1 }));
    }

    [Fact]
    public void Spend_SubtractsBundle()
    {
        var bank = new Resources { Credits = 10, Materials = 6 };
        bank.Spend(new Resources { Credits = 4, Materials = 6 });
        Assert.Equal(6, bank.Credits);
        Assert.Equal(0, bank.Materials);
    }

    [Fact]
    public void ClampNonCredit_LeavesCreditsNegative_ClampsTheRest()
    {
        var r = new Resources { Credits = -7, Materials = -3, Manpower = -1, Influence = -2, Exotics = -5 };
        r.ClampNonCredit();
        Assert.Equal(-7, r.Credits);   // insolvency is a real state (§17)
        Assert.Equal(0, r.Materials);
        Assert.Equal(0, r.Manpower);
        Assert.Equal(0, r.Influence);
        Assert.Equal(0, r.Exotics);
    }

    [Fact]
    public void Clone_IsIndependent()
    {
        var a = new Resources { Credits = 3 };
        var b = a.Clone();
        b.Credits = 99;
        Assert.Equal(3, a.Credits);
    }
}
