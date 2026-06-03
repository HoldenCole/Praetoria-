using Praetoria.Core;
using Praetoria.Core.Data;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Exercises the *authored* Academy Crucible content end-to-end: it loads, validates
/// clean, and exhibits the cascade through the real files (not just synthetic test events).</summary>
public class ContentIntegrationTests
{
    private static (ContentDatabase content, string root) Load()
    {
        var root = ContentLocator.FindContentDir();
        return (ContentLoader.LoadFromDirectory(root), root);
    }

    [Fact]
    public void AuthoredContent_LoadsAndValidatesWithoutErrors()
    {
        var (content, _) = Load();
        var issues = ContentValidator.Validate(content);
        var errors = issues.Where(i => i.Severity == "error").ToList();
        Assert.True(errors.Count == 0, "Content errors:\n" + string.Join("\n", errors));
        Assert.NotEmpty(content.Events);
    }

    [Fact]
    public void TheDuel_OnlyBecomesEligible_AfterTheBarracksInsult()
    {
        var (content, root) = Load();
        var scenario = ScenarioLoader.LoadFromContent(root, "academy_crucible");
        var world = WorldBuilder.FromScenario(scenario, 1);
        var session = new GameSession(world, content);

        bool duelEligibleBeforeInsult = false;
        bool insulted = false;

        for (int t = 0; t < 8; t++)
        {
            session.AdvanceTurn();
            var eligibleIds = session.Engine.GatherEligible(world).Select(e => e.def.Id).ToHashSet();
            if (!insulted && eligibleIds.Contains("the_duel"))
                duelEligibleBeforeInsult = true;

            var fired = session.NextEvent();
            if (fired == null) continue;

            // Force the insult path when the barracks scene appears; otherwise first available.
            string choice = fired.Def.Id == "barracks_confrontation"
                ? "mock"
                : session.Offer(fired).First(c => c.Available).Choice.Id;
            session.Resolve(fired, choice);

            if (fired.Def.Id == "barracks_confrontation" && choice == "mock") insulted = true;
        }

        Assert.True(insulted, "Test precondition: the barracks insult should have fired.");
        Assert.False(duelEligibleBeforeInsult, "The duel must NOT be eligible before the feud is armed.");
        // And once armed, it must have become available (it fires/clears within the run).
        Assert.True(world.History.Exists(h => h.EventId == null && h.Text.Contains("challenge"))
                    || world.HasFlag("evt:the_duel:fired"),
                    "The duel should have become eligible and resolved after the insult.");
    }
}
