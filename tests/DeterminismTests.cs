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
    public void DifferentSeeds_DriveDifferentSelections()
    {
        // Content-independent proof that the RNG threads event selection (BuildSpec §1.5): a pool of
        // equal-weight, always-eligible, repeatable events MUST yield different sequences under
        // different seeds. (Asserting this against the full authored pool is brittle — adding content
        // shifts the RNG stream and arbitrary seeds can coincide — so we prove it by construction.)
        var defs = Enumerable.Range(0, 5).Select(i => new EventDef
        {
            Id = $"e{i}", Repeatable = true, Weight = 1.0,
            Choices = new[] { new Choice { Id = "x" } }
        }).ToArray();

        string Run(ulong seed)
        {
            var w = TestContent.ThreeCadetWorld();
            w.RngState = seed;
            var s = new GameSession(w, TestContent.Db(defs));
            var seq = new List<string>();
            for (int t = 0; t < 8; t++)
            {
                s.AdvanceTurn();
                var f = s.NextEvent();
                seq.Add(f?.Def.Id ?? "-");
                if (f != null) s.Resolve(f, "x");
            }
            return string.Join(",", seq);
        }

        var distinct = Enumerable.Range(1, 20).Select(i => Run((ulong)i)).Distinct().Count();
        Assert.True(distinct > 1, "RNG should drive different selections across seeds.");
    }
}
