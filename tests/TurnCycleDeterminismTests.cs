using System.Text;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>
/// THE Milestone-2 acceptance property (BuildSpec §M2): a full turn cycle — pool spending in the
/// Action phase AND NPC actions in Resolve — resolves deterministically from a seed. Two runs of
/// the same seed must produce an identical command log, RNG position, and world state.
/// </summary>
public class TurnCycleDeterminismTests
{
    private sealed record RunResult(string Log, ulong Rng, string Digest);

    private static RunResult RunFullCycle(ulong seed, int turns)
    {
        var root = ContentLocator.FindContentDir();
        var content = ContentLoader.LoadFromDirectory(root);
        var scenario = ScenarioLoader.LoadFromContent(root, "academy_crucible");
        var world = WorldBuilder.FromScenario(scenario, seed);
        var tc = new TurnController(world, content);

        for (int t = 0; t < turns; t++)
        {
            var briefing = tc.BeginTurn();
            foreach (var item in briefing)
            {
                // Deterministic player policy: take the first affordable, available choice.
                var choice = tc.Offer(item).FirstOrDefault(c => c.Available);
                if (choice != null) tc.Resolve(item, choice.Choice.Id);
            }
            tc.EndTurn();
        }
        tc.SyncRng();

        return new RunResult(string.Join("\n", tc.Executor.Log), world.RngState, Digest(world));
    }

    private static string Digest(World w)
    {
        var sb = new StringBuilder();
        sb.Append("turn=").Append(w.Turn).Append(';');
        foreach (var r in w.Relationships.OrderBy(r => r.FromId + ">" + r.ToId, StringComparer.Ordinal))
            sb.Append(r.FromId).Append('>').Append(r.ToId).Append('=')
              .Append(r.Disposition).Append('/').Append(r.Bond).Append(';');
        foreach (var (id, p) in w.Pools.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            sb.Append(id).Append(":I").Append(p.Influence).Append("T").Append(p.Treasury).Append("A").Append(p.Agents).Append(';');
        foreach (var h in w.History) sb.Append('|').Append(h.Turn).Append(':').Append(h.Text);
        return sb.ToString();
    }

    [Fact]
    public void FullTurnCycle_IsIdentical_FromSameSeed()
    {
        var a = RunFullCycle(2024, 8);
        var b = RunFullCycle(2024, 8);
        Assert.Equal(a.Log, b.Log);
        Assert.Equal(a.Rng, b.Rng);
        Assert.Equal(a.Digest, b.Digest);
    }

    [Fact]
    public void FullTurnCycle_IncludesBothPlayerSpendingAndNpcActions()
    {
        var r = RunFullCycle(1, 6);
        // Player resolved at least one event...
        Assert.Contains("vega_marcus: resolve", r.Log);
        // ...and NPC houses acted through the same bus.
        Assert.Contains("corwin_lucan", r.Log);
        Assert.Contains("sato_ren", r.Log);
    }

    [Fact]
    public void DifferentSeeds_CanDiverge()
    {
        var baseline = RunFullCycle(1, 8);
        bool anyDiffer = false;
        foreach (var seed in new ulong[] { 2, 3, 4, 5, 6, 7, 8, 9 })
            if (RunFullCycle(seed, 8).Log != baseline.Log) { anyDiffer = true; break; }
        Assert.True(anyDiffer, "Expected some seed to drive a different turn cycle.");
    }
}
