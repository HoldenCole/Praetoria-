using Praetoria.Core;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Rng;
using Praetoria.Core.State;
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

    /// <summary>
    /// THE cascade property on the authored Academy content (GDD §15, M1 acceptance): a setpiece
    /// (<c>the_reckoning</c>) is *impossible* until a choice in an earlier event arms the state it
    /// depends on. The reckoning binds its "rival" role only to a cadet carrying the <c>feud</c>
    /// flag — and that flag is set by <c>barracks_slight/humiliate</c>. So the duel cannot exist
    /// before the slight, and does after — unscripted, just eligibility re-reading State.
    ///
    /// Driven deterministically through <see cref="EventEngine.GatherEligible"/> (we fire the
    /// specific events directly rather than hoping the Director's weighted draw surfaces them) so
    /// the proof is robust to the size of the event pool.
    /// </summary>
    [Fact]
    public void TheReckoning_OnlyBecomesEligible_AfterTheBarracksSlightArmsTheFeud()
    {
        var (content, root) = Load();
        var scenario = ScenarioLoader.LoadFromContent(root, "academy_crucible");
        var world = WorldBuilder.FromScenario(scenario, 1);
        var engine = new EventEngine(content, WorldBuilder.RngFor(world));

        FiredEvent? Find(string id)
        {
            foreach (var (def, binding) in engine.GatherEligible(world))
                if (def.Id == id) return new FiredEvent(def, binding);
            return null;
        }

        // first_formation gates on early turns; fire it to open the barracks scene (which gates on
        // first_formation having fired).
        world.Turn = 1;
        var ff = Find("first_formation");
        Assert.NotNull(ff);
        engine.Resolve(ff!, world, ff!.Def.Choices[0].Id);

        // Jump to when the reckoning's turn-gate (turn >= 4) is satisfied. It must STILL be
        // ineligible: no cadet carries the feud flag, so its "rival" role cannot bind.
        world.Turn = 4;
        Assert.Null(Find("the_reckoning"));

        // The slight is available (no turn-gate, a higher/equal-rank disliking rival exists).
        var slight = Find("barracks_slight");
        Assert.NotNull(slight);
        engine.Resolve(slight!, world, "humiliate");   // arms feud on the rival

        // Now — same turn, same world — the reckoning is eligible: the feud-flagged rival binds.
        var reckoning = Find("the_reckoning");
        Assert.NotNull(reckoning);
        Assert.True(world.LivingCharacters().Any(c => c.Flags.Contains("feud")),
            "The slight should have armed a feud flag the reckoning binds against.");
    }
}
