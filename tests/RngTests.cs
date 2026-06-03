using Praetoria.Core.Rng;
using Xunit;

namespace Praetoria.Tests;

public class RngTests
{
    [Fact]
    public void SameSeed_ProducesIdenticalSequence()
    {
        var a = new SplitMix64Rng(42);
        var b = new SplitMix64Rng(42);
        for (int i = 0; i < 1000; i++)
            Assert.Equal(a.NextInt(0, 1_000_000), b.NextInt(0, 1_000_000));
    }

    [Fact]
    public void DifferentSeeds_Diverge()
    {
        var a = new SplitMix64Rng(1);
        var b = new SplitMix64Rng(2);
        bool anyDifferent = false;
        for (int i = 0; i < 50; i++)
            if (a.NextInt(0, int.MaxValue) != b.NextInt(0, int.MaxValue)) { anyDifferent = true; break; }
        Assert.True(anyDifferent);
    }

    [Fact]
    public void State_RoundTrips_ReproducesStream()
    {
        var a = new SplitMix64Rng(123);
        for (int i = 0; i < 10; i++) a.NextDouble();   // advance
        var resumed = new SplitMix64Rng(a.State == 0 ? 0 : a.State); // resume from captured state

        // A fresh RNG seeded with the captured state continues identically to the original.
        var continued = new SplitMix64Rng(a.State);
        for (int i = 0; i < 100; i++)
            Assert.Equal(a.NextInt(0, 10_000), continued.NextInt(0, 10_000));
        Assert.NotNull(resumed);
    }

    [Fact]
    public void NextInt_StaysInRange()
    {
        var r = new SplitMix64Rng(7);
        for (int i = 0; i < 10_000; i++)
        {
            int v = r.NextInt(5, 9);
            Assert.InRange(v, 5, 8);
        }
    }

    [Fact]
    public void NextInt_EmptyRange_ReturnsMin()
    {
        var r = new SplitMix64Rng(7);
        Assert.Equal(3, r.NextInt(3, 3));
        Assert.Equal(3, r.NextInt(3, 2));
    }
}
