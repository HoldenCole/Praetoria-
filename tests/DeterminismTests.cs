using Praetoria.Core;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Same seed + same inputs = same outcome (BuildSpec §1.5). The whole save model rests on this.</summary>
public class DeterminismTests
{
    private static (string history, ulong rng) RunScriptedAcademy(ulong seed)
    {
        var contentRoot = ContentLocator.FindContentDir();
        var content = ContentLoader.LoadFromDirectory(contentRoot);
        var scenario = ScenarioLoader.LoadFromContent(contentRoot, "academy_crucible");
        var world = WorldBuilder.FromScenario(scenario, seed);
        var session = new GameSession(world, content);

        for (int t = 0; t < 8; t++)
        {
            session.AdvanceTurn();
            var fired = session.NextEvent();
            if (fired == null) continue;
            // Deterministic policy: first available choice.
            var choice = session.Offer(fired).First(c => c.Available).Choice.Id;
            session.Resolve(fired, choice);
        }
        session.SyncRng();
        return (string.Join("|", world.History.Select(h => $"{h.Turn}:{h.Text}")), world.RngState);
    }

    [Fact]
    public void TwoRunsFromSameSeed_AreIdentical()
    {
        var a = RunScriptedAcademy(12345);
        var b = RunScriptedAcademy(12345);
        Assert.Equal(a.history, b.history);
        Assert.Equal(a.rng, b.rng);
    }

    [Fact]
    public void DifferentSeeds_CanDiverge()
    {
        // Not guaranteed for every pair, but across a spread the run should differ for some seed.
        var baseline = RunScriptedAcademy(1);
        bool anyDiffer = false;
        foreach (var seed in new ulong[] { 2, 3, 4, 5, 6, 7, 8 })
            if (RunScriptedAcademy(seed).history != baseline.history) { anyDiffer = true; break; }
        Assert.True(anyDiffer, "Expected at least one differing seed to prove RNG actually drives selection.");
    }
}
