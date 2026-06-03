using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The domain economy (GDD §17, BuildSpec §97): per-turn accrual/upkeep, building yields,
/// population growth, and the insolvency→unrest→manpower-suppression feedback loop.</summary>
public class EconomyTests
{
    [Fact]
    public void Accrue_AddsSpecYield_NetOfUpkeep_ToOwnerTreasury()
    {
        var w = TestContent.EconomyWorld(credits: 10);
        new Economy(TestContent.EconomyCatalog()).Accrue(w);

        var t = w.House("vega")!.Treasury;
        Assert.Equal(11, t.Credits);   // agri +2 credits, -1 upkeep
        Assert.Equal(3, t.Manpower);   // +3 manpower (started at 0)
    }

    [Fact]
    public void Accrue_GrowsPopulation_OnPopulatedHoldings()
    {
        var w = TestContent.EconomyWorld();
        new Economy(TestContent.EconomyCatalog()).Accrue(w);
        Assert.Equal(22, w.Holding("barony")!.Population);   // +2 popGrowth
    }

    [Fact]
    public void Accrue_IncludesBuildingYields()
    {
        var w = TestContent.EconomyWorld(credits: 10);
        w.Holding("barony")!.Buildings.Add("farm");          // +2 manpower, +1 credits
        new Economy(TestContent.EconomyCatalog()).Accrue(w);

        var t = w.House("vega")!.Treasury;
        Assert.Equal(12, t.Credits);   // agri net +1, farm +1
        Assert.Equal(5, t.Manpower);   // agri +3, farm +2
    }

    [Fact]
    public void Insolvency_RaisesUnrest_AndSuppressesManpower()
    {
        var w = TestContent.EconomyWorld(credits: -1);       // already in the red
        new Economy(TestContent.EconomyCatalog()).Accrue(w);

        var holding = w.Holding("barony")!;
        Assert.Equal(Economy.UnrestStep, holding.Unrest);    // unrest climbed
        // Manpower yield throttled by the new unrest: 3 * (100-5)/100 = 2 (integer).
        Assert.Equal(2, w.House("vega")!.Treasury.Manpower);
    }

    [Fact]
    public void Solvency_DecaysUnrest_TowardZero()
    {
        var w = TestContent.EconomyWorld(credits: 10);
        w.Holding("barony")!.Unrest = 10;
        new Economy(TestContent.EconomyCatalog()).Accrue(w);
        Assert.Equal(10 - Economy.UnrestDecay, w.Holding("barony")!.Unrest);
    }

    [Fact]
    public void EmptyCatalog_IsNoOp()
    {
        var w = TestContent.EconomyWorld(credits: 10);
        new Economy(Praetoria.Core.Data.HoldingCatalog.Empty).Accrue(w);
        Assert.Equal(10, w.House("vega")!.Treasury.Credits);
    }

    [Fact]
    public void Accrue_IgnoresUnknownSpecialization()
    {
        var w = TestContent.EconomyWorld(credits: 10);
        w.Holding("barony")!.Specialization = "does_not_exist";
        new Economy(TestContent.EconomyCatalog()).Accrue(w);
        Assert.Equal(10, w.House("vega")!.Treasury.Credits);   // untouched
    }
}
