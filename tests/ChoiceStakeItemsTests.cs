using Praetoria.Core;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>A choice's stakes as items (docs/VISUAL_SYSTEM.md 6, "What this needs from Core"): the event window draws a chip and a tooltip
/// line from each, so each must carry its kind, icon, signed figure, before and after and person, agreeing with the words of
/// <see cref="ChoiceStakes.Of"/>.</summary>
public class ChoiceStakeItemsTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static (World w, House house, Character me) Start()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 7);
        var me = w.Protagonist!; return (w, w.House(me.HouseId)!, me);
    }

    [Fact]
    public void EachEffect_IsAnItemWithItsFigureAndItsBeforeAndAfter()
    {
        var (w, house, me) = Start(); house.Treasury.Credits = 25; house.Treasury.Coin = 0; me.Stress = 4; house.Legitimacy = 60;
        var other = w.LivingCharacters().First(c => c.HouseId != house.Id);
        int now = w.GetRelationship(other.Id, me.Id)?.Disposition ?? 0;
        var ectx = new EvalContext(w, new Binding(me.Id).With("superior", other.Id), TestContent.Rng(), Content);
        var choice = new Choice { Id = "carry", Effects = new IEffect[] {
            new AdjustResourceEffect("self", Resource.Credits, -6), new AdjustStressEffect("self", 10), new AdjustLegitimacyEffect("self", 2),
            new AdvanceCareerEffect("self"), new AdjustRelationshipEffect("superior", "self", 30) } };
        var items = ChoiceStakes.Items(choice, ectx, me);

        var cr = Assert.Single(items, i => i.Kind == "resource:credits");
        Assert.Equal(-6, cr.Amount); Assert.Equal("−600", cr.Figure); Assert.False(cr.Good); Assert.Equal("credits", cr.Icon);
        Assert.Equal(Money.Exact(2500), cr.Before); Assert.Equal(Money.Exact(1900), cr.After);
        var st = Assert.Single(items, i => i.Kind == "stress");
        Assert.Equal("+10", st.Figure); Assert.False(st.Good); Assert.Equal("4", st.Before); Assert.Equal("14", st.After); Assert.Equal("Strain +10", st.Words);
        var lg = Assert.Single(items, i => i.Kind == "legitimacy"); Assert.True(lg.Good); Assert.Equal("62", lg.After);
        var up = Assert.Single(items, i => i.Kind == "career"); Assert.Equal("Step up", up.Figure); Assert.True(up.Good);
        Assert.Equal(Content.Careers.RankName(me.CareerTrack, me.CareerRank, w.Era) ?? "", up.Before);
        var rg = Assert.Single(items, i => i.Kind == "regard");
        Assert.Equal(other.Id, rg.PersonId); Assert.Equal("+30", rg.Figure); Assert.True(rg.Good); Assert.Equal(Counted.Figure(now), rg.Before);
        Assert.Contains(other.Name + " thinks better of you", rg.Words);
        // the words of the string say the same things
        string said = ChoiceStakes.Of(choice, ectx, me);
        Assert.Contains("−600 credits", said); Assert.Contains("stress +10", said); Assert.Contains("a step up in the service", said);
    }

    [Fact]
    public void Requirements_AreSaidMetOrNot_AndATraitRefusalNamesWhatYouAre()
    {
        var (w, house, me) = Start(); house.Treasury.Credits = 50;
        var ectx = new EvalContext(w, new Binding(me.Id), TestContent.Rng(), Content);
        string lacking = Content.Traits.Nature.Select(t => t.Id).First(t => !me.NatureTraits.Contains(t) && !me.AptitudeTraits.Contains(t));
        var choice = new Choice { Id = "refuse", Requirements = new ICondition[] {
            new TraitCondition("self", lacking, "nature", true), new ResourceCondition("self", Resource.Credits, CompareOp.Gte, 10) } };
        var needs = ChoiceStakes.Needs(choice, ectx);
        var trait = Assert.Single(needs, n => !n.Met); Assert.StartsWith("needs one who is " + lacking, trait.Words);
        if (me.NatureTraits.Count + me.AptitudeTraits.Count > 0) Assert.Contains("(you are ", trait.Words);
        var coin = Assert.Single(needs, n => n.Met); Assert.Contains("credits in the treasury", coin.Words);
        Assert.Contains("needs one who is " + lacking, ChoiceStakes.Unmet(choice, ectx));
    }

    [Fact]
    public void AcrossARealGame_EveryChoiceHasItsItems_AndThePoolsItSpends()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 11);
        var tc = new TurnController(w, Content); tc.BeginTurn(); int seen = 0;
        for (int y = 0; y < 5; y++)
        {
            var model = CourtProjection.Build(tc, Content);
            foreach (var card in model.Briefing) foreach (var ch in card.Choices)
            {
                seen++;
                foreach (var i in ch.Items) { Assert.False(string.IsNullOrWhiteSpace(i.Icon)); Assert.False(string.IsNullOrWhiteSpace(i.Figure)); Assert.DoesNotContain("-", i.Figure.TrimStart('+')); }
                if (ch.Cost.Length > 0) Assert.Contains(ch.Items, i => i.Kind.StartsWith("pool:"));
                if (ch.Stakes.Length > 0) Assert.NotEmpty(ch.Items);
            }
            tc.EndTurn(); if (w.IsDynastyDead) break; tc.BeginTurn();
        }
        Assert.True(seen > 0);
    }
}
