using Praetoria.Core.Data;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The goal under the top bar (docs/GOALS.md): every written start has an aim for its first ten years, in steps read from
/// the world; once it is over the player's own ambition stands in its place; and a treasury that will run out within three years
/// says so, with why and two ways out.</summary>
public class GoalTests
{
    private static (ContentDatabase content, TurnController tc) Start(string scenario, ulong seed = 1)
    {
        var root = ContentLocator.FindContentDir();
        var content = ContentLoader.LoadFromDirectory(root);
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, scenario), seed);
        var tc = new TurnController(w, content);
        tc.BeginTurn();
        return (content, tc);
    }

    [Theory]
    [InlineData("the_new_man", "Win a seat in the Senate")]
    [InlineData("the_old_lion", "See the seat pass safely")]
    [InlineData("the_circling_heir", "Hold the house, and rise in the Senate")]
    [InlineData("the_fallen_house", "Restore the house")]
    [InlineData("the_risen_officer", "Earn a command and a name")]
    [InlineData("the_imperial_court", "Rise at the Emperor's court")]
    public void Every_written_start_has_a_goal_with_steps_in_its_first_year(string scenario, string title)
    {
        var (content, tc) = Start(scenario);
        Assert.Equal(scenario, tc.World.Start);
        var g = Goals.For(tc, content);
        Assert.NotNull(g);
        Assert.Equal("start", g!.Kind);
        Assert.Equal(title, g.Title);
        Assert.InRange(g.Steps.Count, 3, 4);
        Assert.False(g.Reached);
        Assert.NotNull(g.Next);
        Assert.All(g.Steps, s => { Assert.False(string.IsNullOrWhiteSpace(s.Text)); Assert.False(string.IsNullOrWhiteSpace(s.Target)); Assert.DoesNotContain("—", s.Text + s.Stands); });
        Assert.Equal(g.Steps.Count(s => s.Done) * 100 / g.Steps.Count, g.Percent);
    }

    [Fact]
    public void The_new_man_takes_his_seat_by_the_census_and_the_goal_gives_way_to_his_ambition()
    {
        var (content, tc) = Start("the_new_man");
        var w = tc.World; var house = w.House(w.Protagonist!.HouseId)!;
        var g = Goals.For(tc, content)!;
        Assert.False(g.Steps[2].Done);   // no seat yet
        Assert.Contains("of the 20 the census asks", g.Steps[0].Stands);
        house.Enrolled = true;   // the purse: a seat by the census
        var after = Goals.For(tc, content)!;
        Assert.Equal("ambition", after.Kind);   // every step reads done once the house is seated, and the ambition is the aim now
        Assert.Equal("Win a seat in the Senate: done.", after.Was);
    }

    [Fact]
    public void The_old_lions_goal_is_over_once_the_seat_passes_and_says_whether_every_world_was_kept()
    {
        var (content, tc) = Start("the_old_lion");
        var w = tc.World; var house = w.House(w.Protagonist!.HouseId)!;
        int had = w.Counter(Goals.StartWorldsKey);
        Assert.True(had >= 1);
        w.WorldCounters["generation"] = 1;   // the seat has passed
        var start = Goals.Start(w, content, w.Protagonist!, house)!;
        Assert.True(start.Reached);
        Assert.Contains("every world kept", start.Was);
        Assert.Equal("ambition", Goals.For(tc, content)!.Kind);
    }

    [Fact]
    public void A_world_saved_before_the_start_was_kept_shows_the_ambition()
    {
        var (content, tc) = Start("the_fallen_house");
        tc.World.Start = "";
        var g = Goals.For(tc, content);
        Assert.NotNull(g);
        Assert.Equal("ambition", g!.Kind);
        Assert.Equal("See the house rich again", g.Title);   // its head wants to see the house rich again, said to the player
    }

    [Fact]
    public void No_goal_is_shown_at_the_academy()
    {
        var (content, tc) = Start("the_academy");
        Assert.True(tc.World.InAcademy);
        Assert.Null(Goals.For(tc, content));
    }

    [Fact]
    public void A_treasury_that_runs_out_within_three_years_says_why_and_gives_two_ways_out()
    {
        var r = Goals.Ruin(-400, 900, new[] { ("the household's keep and stipends", 300), ("fuel for the fleet", 200), ("tutors", 20) }, 0);
        Assert.NotNull(r);
        Assert.Equal(2, r!.Years);
        Assert.Contains("runs out within 3 years", r.Says);
        Assert.Contains("40000 credits a year more than it takes in", r.Says.Replace(",", ""));   // the books are kept in hundreds (docs/MONEY.md)
        Assert.Contains("the household's keep and stipends", r.Says);
        Assert.Equal(2, r.WaysOut.Count);
        Assert.Equal("house:court", r.WaysOut[0].target);
        Assert.Equal("military", r.WaysOut[1].target);

        var now = Goals.Ruin(-400, 100, Array.Empty<(string, int)>(), 50)!;
        Assert.StartsWith("The treasury will not last the year", now.Says);
        Assert.Equal("house:purse", now.WaysOut[0].target);   // the head's own purse first, when there is nothing named to cut
    }

    [Fact]
    public void A_treasury_that_lasts_three_years_or_grows_gives_no_warning()
    {
        Assert.Null(Goals.Ruin(-100, 300, Array.Empty<(string, int)>(), 0));
        Assert.Null(Goals.Ruin(0, 0, Array.Empty<(string, int)>(), 0));
        Assert.Null(Goals.Ruin(250, 10, Array.Empty<(string, int)>(), 0));
    }
    [Fact]
    public void A_house_that_loses_its_last_world_raises_the_report_for_the_year_after_and_only_then()
    {
        var (content, tc) = Start("the_fallen_house");
        var w = tc.World; var house = w.House(w.Protagonist!.HouseId)!;
        FallenHouse.Tick(w);   // a year's end with its world held: nothing to say
        Assert.False(w.HasFlag(FallenHouse.Flag));
        foreach (var h in w.HoldingsOf(house.Id).ToList()) h.OwnerId = "";   // the last world is gone
        FallenHouse.Tick(w);
        Assert.True(w.HasFlag(FallenHouse.Flag));
        Assert.Contains(content.Events, e => e.Id == "the_last_world_lost");
        var engine = new Praetoria.Core.Events.EventEngine(content, TestContent.Rng(3));
        w.Turn++;   // the next year: the report is among those that may be drawn
        Assert.Contains(engine.GatherEligible(w), e => e.def.Id == "the_last_world_lost");
        FallenHouse.Tick(w);   // that year's end: the news is old, and the flag comes down
        Assert.False(w.HasFlag(FallenHouse.Flag));
    }

    [Fact]
    public void The_old_lion_wants_an_office_not_to_restore_the_richest_house()
    {
        var (_, tc) = Start("the_old_lion");
        Assert.Equal("master_the_senate", tc.World.Protagonist!.Ambition);
    }
}

