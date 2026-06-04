using System.Text;
using Praetoria.Core.Data;
using Praetoria.Core.Rng;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The crisis system stays exactly reproducible from a seed (GDD §16): organic onset is a
/// seeded weighted roll, so two runs of the same seed yield an identical crisis timeline; different
/// seeds may diverge.</summary>
public class CrisisDeterminismTests
{
    private static string RunOrganic(ulong seed, int turns)
    {
        var root = ContentLocator.FindContentDir();
        var content = ContentLoader.LoadFromDirectory(root);
        var engine = new CrisisEngine(content.Crises);
        var w = TestContent.EconomyWorld(credits: 10);
        var rng = new SplitMix64Rng(seed);

        var sb = new StringBuilder();
        for (int t = 0; t < turns; t++)
        {
            w.Turn = t + 1;
            // A steadily worsening realm: ripe ground over which the organic roll plays out.
            w.WorldCounters["unrest"] = 5 + t;
            w.WorldCounters["corruption"] = 3 + t;
            var onset = engine.RollOrganic(w, rng);
            if (onset != null) engine.Trigger(onset, w, rng);
            sb.Append('T').Append(w.Turn).Append(':')
              .Append(string.Join(",", w.Crises.Keys.OrderBy(k => k, StringComparer.Ordinal))).Append(';');
        }
        return sb.ToString();
    }

    [Fact]
    public void OrganicTimeline_IsIdentical_FromSameSeed()
    {
        Assert.Equal(RunOrganic(2024, 10), RunOrganic(2024, 10));
    }

    [Fact]
    public void OrganicTimeline_CanDiverge_AcrossSeeds()
    {
        var baseline = RunOrganic(1, 10);
        bool anyDiffer = false;
        foreach (ulong s in new ulong[] { 2, 3, 4, 5, 6, 7, 8, 9 })
            if (RunOrganic(s, 10) != baseline) { anyDiffer = true; break; }
        Assert.True(anyDiffer, "Expected some seed to drive a different crisis timeline.");
    }

    [Fact]
    public void RipeConditions_DoEventuallyProduceACrisis_OrganicallyOverTime()
    {
        // Over a worsening 10-turn arc the abstain-weighted roll should fire at least one crisis.
        var timeline = RunOrganic(2024, 10);
        Assert.Contains("local_revolt", timeline);
    }
}
