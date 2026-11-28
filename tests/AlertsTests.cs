using Praetoria.Core.Data;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The alerts row (docs/VISUAL_SYSTEM.md 3.5): every alert carries a kind and a severity the client draws from, the most
/// pressing come first, and what the outliner used to alert is still alerted, now with its kind.</summary>
public class AlertsTests
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

    private static List<Alert> Now(ContentDatabase content, TurnController tc) => Alerts.For(tc.World, content, CourtProjection.Build(tc, content), tc.Progression);

    [Theory]
    [InlineData("the_old_lion")]
    [InlineData("the_fallen_house")]
    [InlineData("the_circling_heir")]
    [InlineData("the_risen_officer")]
    [InlineData("the_imperial_court")]
    public void EveryAlert_HasAKnownKind_ASeverity_AndNoEmDash(string scenario)
    {
        var (content, tc) = Start(scenario);
        for (int year = 0; year < 3; year++)
        {
            var list = Now(content, tc);
            foreach (var a in list)
            {
                Assert.Contains(a.Kind, Alerts.Kinds);
                Assert.Contains(a.Severity, Alerts.Severities);
                Assert.False(string.IsNullOrWhiteSpace(a.Text));
                Assert.False(string.IsNullOrWhiteSpace(a.Go));
                Assert.DoesNotContain("—", a.Text);
                Assert.False(string.IsNullOrWhiteSpace(Alerts.Title(a.Kind)));
            }
            // the most pressing first
            for (int i = 1; i < list.Count; i++) Assert.True(Alerts.Rank(list[i - 1].Severity) <= Alerts.Rank(list[i].Severity), $"{list[i - 1].Severity} before {list[i].Severity}");
            tc.EndTurn(); tc.BeginTurn();
        }
    }

    [Fact]
    public void AnEmptyGovernorsChair_IsADangerOfTheGovernorKind_ThatOpensItsSystem()
    {
        var (content, tc) = Start("the_old_lion"); var w = tc.World;
        var world = w.HoldingsOf(w.Protagonist!.HouseId).First(h => h.IsPlanet); world.GovernorId = "";
        var a = Assert.Single(Now(content, tc), x => x.Text.StartsWith(world.Name + " has no governor"));
        Assert.Equal(Alerts.Governor, a.Kind); Assert.Equal(Alerts.Danger, a.Severity); Assert.Equal("galaxy:" + world.SystemId, a.Go);
    }

    [Fact]
    public void DryTanks_AndAWarAbroad_HaveTheirKinds_AndTheWarHasNoEmDash()
    {
        var (content, tc) = Start("the_old_lion"); var w = tc.World;
        w.WorldFlags.Add("fuel_short"); w.WorldFlags.Add("foreign_war_declared"); w.WorldFlags.Add("market_crash"); w.WorldFlags.Add("election_near");
        var list = Now(content, tc);
        var fuel = Assert.Single(list, x => x.Text.StartsWith("The tanks are dry"));
        Assert.Equal((Alerts.Fleet, Alerts.Danger, "exchange:goods"), (fuel.Kind, fuel.Severity, fuel.Go));
        var war = Assert.Single(list, x => x.Text.StartsWith("The Senate has made war on"));
        Assert.Equal(Alerts.Beyond, war.Kind); Assert.Contains(": fleets may be sent", war.Text);
        Assert.Equal(Alerts.Exchange, Assert.Single(list, x => x.Text.StartsWith("The Exchange has fallen")).Kind);
        var vote = Assert.Single(list, x => x.Text.StartsWith("The consuls are elected"));
        Assert.Equal((Alerts.Vote, Alerts.Decision), (vote.Kind, vote.Severity));
        // dangers stand before decisions
        Assert.True(list.IndexOf(fuel) < list.IndexOf(vote));
    }

    [Fact]
    public void News_TakesItsKindFromWhereItOpens_AndAnIllnessIsHealth()
    {
        var (_, tc) = Start("the_old_lion"); var w = tc.World; var me = w.Protagonist!;
        Assert.Equal(Alerts.Houses, Alerts.FromNews(w, new NewsItem { Tone = "info", Text = "A house rose", Target = "houses", SubjectId = me.HouseId }).Kind);
        Assert.Equal("houses:" + me.HouseId, Alerts.FromNews(w, new NewsItem { Tone = "info", Text = "A house rose", Target = "houses", SubjectId = me.HouseId }).Go);
        Assert.Equal(Alerts.Money, Alerts.FromNews(w, new NewsItem { Tone = "warn", Text = "Short", Target = "house:ledger" }).Kind);
        Assert.Equal(Alerts.Court, Alerts.FromNews(w, new NewsItem { Tone = "info", Text = "A suit", Target = "senate:ladder" }).Kind);
        Assert.Equal(Alerts.Vote, Alerts.FromNews(w, new NewsItem { Tone = "good", Text = "Carried", Target = "senate:chamber" }).Kind);
        Assert.Equal(Alerts.Good, Alerts.FromNews(w, new NewsItem { Tone = "good", Text = "Carried", Target = "senate:chamber" }).Severity);
        var kin = w.Characters.Values.First(c => c.Alive && c.HouseId == me.HouseId && c.Id != me.Id); kin.Flags.Add(LifeSystem.IllChar);
        var ill = Alerts.FromNews(w, new NewsItem { Tone = "warn", Text = kin.Name + " is ill", Target = "dynasty", SubjectId = kin.Id });
        Assert.Equal((Alerts.Health, Alerts.Danger, "dynasty:tree:" + kin.Id), (ill.Kind, ill.Severity, ill.Go));
        Assert.Equal("Wars: at an end", Alerts.FromNews(w, new NewsItem { Text = "Wars — at an end", Target = "" }).Text);
    }

    [Fact]
    public void AStackedBadge_TakesTheWorstSeverity()
    {
        var a = new[] { new Alert(Alerts.Vote, Alerts.Info, "a", "senate"), new Alert(Alerts.Vote, Alerts.Decision, "b", "senate"), new Alert(Alerts.Vote, Alerts.Good, "c", "senate") };
        Assert.Equal(Alerts.Decision, Alerts.Worst(a));
        Assert.Equal(Alerts.Info, Alerts.Worst(Array.Empty<Alert>()));
    }
}
