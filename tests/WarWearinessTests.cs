using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>A decided war ends (docs/MILITARY.md, "A decided war ends"; docs/STEAM_REVIEW.md, item 13). The review found a war with the
/// player standing at nought to ten in its sixteenth year with every peace refused: the house ahead smelled victory and would not hear
/// of one, and the house behind had no world it could give. Past the weariness threshold the side ahead takes its price, a house ahead
/// of the player's sends its terms, and a house behind or level sues.</summary>
public class WarWearinessTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());

    private static (World w, TurnController tc, House house, Character me, House foe, Character foeHead, CommandContext ctx) Start()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 7);
        var tc = new TurnController(w, Content); var me = w.Protagonist!; var house = w.House(me.HouseId)!;
        var foe = w.Houses.Values.Where(h => h.Id != house.Id && !World.Outsider(h.Id) && Economy.HeadOf(w, h) != null).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        return (w, tc, house, me, foe, Economy.HeadOf(w, foe)!, new CommandContext(w, TestContent.Rng()));
    }
    private static void Hulls(World w, string houseId, int n)
    {
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == houseId).ToList()) w.Fleets.Remove(f.Id);
        if (n > 0) w.Fleets["t_" + houseId] = new Fleet { Id = "t_" + houseId, OwnerId = houseId, Name = "T", SystemId = w.Holdings.Values.First(h => h.IsPlanet).SystemId, Strength = n };
    }
    /// <summary>A war the foe leads by ten, begun <paramref name="yearsAgo"/> years back, with the foe the stronger by far.</summary>
    private static War Losing(World w, House house, House foe, int yearsAgo)
    {
        foreach (var old in w.Wars.Where(x => x.Involves(house.Id)).ToList()) w.Wars.Remove(old);
        var war = new War { HouseA = house.Id, HouseB = foe.Id, Since = w.Turn - yearsAgo, ClosedThrough = w.Turn - yearsAgo };
        w.Wars.Add(war); war.AddScore(foe.Id, 10);
        Hulls(w, house.Id, 3); Hulls(w, foe.Id, 30);
        return war;
    }

    [Fact]
    public void A_winner_that_smells_victory_takes_its_price_once_the_war_is_weary()
    {
        var (w, tc, house, me, foe, _, _) = Start(); var rules = Content.Fleets; var fleets = tc.Fleets;
        var war = Losing(w, house, foe, 1);
        Assert.False(fleets.Weary(w, war));
        var tribute = HousePeace.Terms(w, fleets, rules, house.Id, foe.Id).First(t => t.Kind == HousePeace.Tribute);
        Assert.False(tribute.Accepted);
        Assert.Contains($"from year {war.Since + rules.WarWearinessTurns}", tribute.Why);   // the refusal says when it will listen

        war.Since = w.Turn - rules.WarWearinessTurns;
        Assert.True(fleets.Weary(w, war));
        var terms = HousePeace.Terms(w, fleets, rules, house.Id, foe.Id);
        Assert.True(terms.First(t => t.Kind == HousePeace.Tribute).Accepted);
        Assert.False(terms.First(t => t.Kind == HousePeace.White).Accepted);   // weary or not, the side ahead wants something for stopping
    }

    [Fact]
    public void A_house_ahead_in_a_weary_war_sends_its_terms_and_the_report_offers_them()
    {
        var (w, tc, house, me, foe, foeHead, ctx) = Start(); var rules = Content.Fleets;
        var war = Losing(w, house, foe, 1);
        var offer = new OfferTermsCommand(foeHead.Id, house.Id, rules);
        Assert.False(offer.CanExecute(ctx));   // not weary yet
        war.Since = w.Turn - rules.WarWearinessTurns;
        Assert.True(offer.CanExecute(ctx));
        offer.Execute(ctx);
        Assert.Contains(FleetSystem.OfferingTermsFlag, foeHead.Flags);
        Assert.False(offer.CanExecute(ctx));   // one envoy at a time

        // the report: the envoy is bound by its flag and the war it leads; paying and fighting on are both open
        var def = Content.Event(ReportFacts.VictorReport)!;
        var ectx = new EvalContext(w, new Binding(me.Id).With("envoy", foeHead.Id), TestContent.Rng(), Content);
        Assert.All(def.Roles.Single().Constraints, c => Assert.True(c.Evaluate(ectx), c.GetType().Name));
        var open = def.Choices.Where(c => c.Requirements.All(r => r.Evaluate(ectx))).Select(c => c.Id).ToList();
        Assert.Contains("pay_tribute", open); Assert.Contains("fight_on", open);
        Assert.Contains("What they will take", ReportFacts.For(w, def, Content));
        Assert.True(CourtProjection.Build(tc, Content).Wars.First(x => x.EnemyHouseId == foe.Id).TheyOfferTerms);

        // paying ends the war, moves what the score says was owed, and clears the envoy
        house.Treasury.Credits = 1000; int had = house.Treasury.Credits;
        foreach (var e in def.Choices.First(c => c.Id == "pay_tribute").Effects) e.Apply(ectx);
        Assert.False(w.AtWar(house.Id, foe.Id));
        Assert.Equal(10 * rules.TributePerScore, had - house.Treasury.Credits);
        Assert.DoesNotContain(FleetSystem.OfferingTermsFlag, foeHead.Flags);
    }

    [Fact]
    public void The_envoy_who_sues_asks_no_price_of_a_house_that_is_behind()
    {
        // the envoy's text says the envoy's house pays: a price can be named only by the side ahead
        var (w, tc, house, me, foe, foeHead, _) = Start();
        Losing(w, house, foe, 1); foeHead.Flags.Add(FleetSystem.SuingForPeaceFlag);
        var def = Content.Event(ReportFacts.EnvoyReport)!;
        var ectx = new EvalContext(w, new Binding(me.Id).With("envoy", foeHead.Id), TestContent.Rng(), Content);
        var open = def.Choices.Where(c => c.Requirements.All(r => r.Evaluate(ectx))).Select(c => c.Id).ToList();
        Assert.DoesNotContain("demand_tribute", open); Assert.DoesNotContain("demand_world", open);
        Assert.Contains("accept_peace", open); Assert.Contains("refuse_terms", open);
    }

    [Fact]
    public void Left_alone_a_weary_war_with_the_player_does_not_stand_unanswered()
    {
        // the rivals' own turns: within a few years of the threshold the house ahead has sent its terms or the war is over
        var (w, tc, house, me, foe, foeHead, _) = Start();
        var war = Losing(w, house, foe, Content.Fleets.WarWearinessTurns);
        bool answered = false;
        for (int y = 0; y < 4 && !answered; y++)
        {
            tc.BeginTurn(); tc.EndTurn();
            answered = !w.AtWar(house.Id, foe.Id) || w.LivingCharacters().Any(c => c.HouseId == foe.Id && (c.Flags.Contains(FleetSystem.OfferingTermsFlag) || c.Flags.Contains(FleetSystem.SuingForPeaceFlag)));
        }
        Assert.True(answered, $"the war at {war.ScoreA} to {war.ScoreB} since year {war.Since} stood unanswered in year {w.Turn}");
    }
}
