using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Presentation;
using Praetoria.Core.Rng;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The wedding and the naming come with the marriage and the birth (docs/EVENTS.md, "Moments in the year"), and every start played
/// by a stand-in keeps the two rules of the deck: no card is drawn with fewer than two answers open, and none comes back to the same head
/// within its waiting time (docs/EVENTS.md, "Two answers open", "Waiting times").</summary>
public class EventMomentsTests
{
    private static readonly string Root = ContentLocator.FindContentDir();
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(Root);

    /// <summary>The real content with one more report: a match agreed by an answer (an event's choice that makes the marriage).</summary>
    private static ContentDatabase WithAMatch(EventDef match) =>
        new(Content.Events.Append(match).ToList(), Content.Texts, Content.Holdings, Content.Crises, Content.Spheres, Content.Titles, Content.Careers,
            Content.Traits, Content.Backgrounds, Content.Fleets, Content.ShipClasses, Content.Honors, Content.Friction, Content.Names, Content.GreatCrisis,
            Content.Recovery, Content.Businesses, Content.Motions, Content.Offices, Content.Policies, Content.Parties, Content.Nations, Content.Academy,
            Content.Market, Content.Exchange) { Ancestries = Content.Ancestries, Growth = Content.Growth, Edicts = Content.Edicts };

    [Fact]
    public void AWeddingMadeInTheYear_IsAnsweredInTheYear_AndTheBriefTellsItAfter()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(Root, "the_risen_officer"), 4);
        var me = w.Protagonist!; Assert.Null(DynastySystem.Spouse(w, me));
        var bride = w.LivingCharacters().Where(c => c.HouseId != me.HouseId && c.Sex != me.Sex && c.Age is >= 20 and <= 40 && DynastySystem.Spouse(w, c) == null)
            .OrderBy(c => c.Id, StringComparer.Ordinal).First();
        bride.Flags.Add("test_bride");
        var match = new EventDef
        {
            Id = "test_match", Weight = 1e9,
            Roles = new[] { new RoleDef { Name = "bride", Constraints = new ICondition[] { new CharFlagCondition("bride", "test_bride", true) } } },
            Choices = new[] { new Choice { Id = "wed", Effects = new IEffect[] { new AddBondEffect("self", "bride", BondType.Marriage, 60) } }, new Choice { Id = "not" } }
        };
        var tc = new TurnController(w, WithAMatch(match), briefingBudget: 1);
        var year = tc.BeginTurn(); int turn = w.Turn;
        Assert.Equal("test_match", year.Single().Fired.Def.Id);

        Assert.True(tc.Resolve(year[0], "wed"));
        // the marriage is made now, and its wedding is among this year's reports at once
        Assert.Equal(turn, w.MarriageOf(me.Id)!.Since);
        var wedding = tc.Briefing.SingleOrDefault(b => b.Fired.Def.Id == "the_wedding_day");
        Assert.NotNull(wedding);
        Assert.Equal(bride.Id, wedding!.Fired.Binding.Resolve("spouse"));
        Assert.True(tc.Resolve(wedding, "the_two_families_at_one_table"));
        tc.EndTurn();

        // the next year: no second wedding card, and the year in brief tells the marriage as done, after its card came
        tc.BeginTurn();
        Assert.DoesNotContain(tc.Briefing, b => b.Fired.Def.Id == "the_wedding_day");
        var line = YearInBrief.For(w, Content).Single(l => l.Target == "dynasty:marriages" && l.Text.Contains(bride.Name));
        Assert.DoesNotContain("among this year's reports", line.Text);
    }

    [Fact]
    public void ABirthWhoseCardCameAsTheYearOpened_IsToldInTheBriefWithItsCard()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(Root, "the_fallen_house"), 4); w.Turn = 6;
        var me = w.Protagonist!;
        var kid = new Character { Id = "kid", Name = "Ulric Voss", HouseId = me.HouseId, Age = 0, Sex = "male", FatherId = me.Id };
        w.Characters[kid.Id] = kid; w.House(me.HouseId)!.Members.Add(kid.Id);
        w.Tell("good", "A child is born to your house: Ulric Voss, son of Aldric Voss", "dynasty", kid.Id);
        Assert.Contains(YearInBrief.For(w, Content), l => l.Text == "A child is born to your house: Ulric Voss, son of Aldric Voss");
        // the naming card was drawn as the year opened, with the child bound to its once-a-person part
        w.CardsMet[CardSeen.Met("the_naming_of_a_child", "child", kid.Id)] = w.Turn;
        var line = YearInBrief.For(w, Content).Single(l => l.PersonId == kid.Id);
        Assert.Equal("A child is born to your house: Ulric Voss, son of Aldric Voss. “The Naming of a Child” is among this year's reports.", line.Text);
        // a year on, the card is last year's, and the line is as it was
        w.Turn = 7;
        Assert.DoesNotContain("among this year's reports", YearInBrief.For(w, Content).Single(l => l.PersonId == kid.Id).Text);
    }

    [Fact]
    public void TheWeddingAndTheNaming_AreOncePerPerson_AndCarryNoWaitingTime()
    {
        var wedding = Content.Event("the_wedding_day")!; var naming = Content.Event("the_naming_of_a_child")!;
        Assert.True(wedding.Moment); Assert.Equal(0, wedding.WaitYears); Assert.True(wedding.Roles.Single(r => r.Name == "spouse").Once);
        Assert.Equal(0, naming.WaitYears); Assert.True(naming.Roles.Single(r => r.Name == "child").Once);
    }

    /// <summary>The check on the deck (docs/EVENTS.md, "Two answers open"): every start, played ten years by a stand-in who answers every
    /// report, draws no card with fewer than two answers open as the year's reports are drawn, and no card comes back to the same head
    /// within its waiting time.</summary>
    [Fact]
    public void EveryStart_DrawsNoCardWithOneAnswerOpen_AndNoneBackWithinItsWait()
    {
        var problems = new List<string>();
        foreach (var sid in Directory.EnumerateFiles(Path.Combine(Root, "scenarios"), "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(s => s, StringComparer.Ordinal))
        {
            var scenario = ScenarioLoader.LoadFromContent(Root, sid!); if (scenario.Hidden) continue;
            var w = WorldBuilder.FromScenario(scenario, 5); var tc = new TurnController(w, Content); var dice = new SplitMix64Rng(11);
            var last = new Dictionary<(string, string), (int turn, int times)>();
            for (int guard = 0; guard < 24 && !w.IsDynastyDead && (w.Turn < 10 || w.InAcademy); guard++)
            {
                var year = tc.BeginTurn();
                foreach (var item in year)
                {
                    var def = item.Fired.Def; int open = tc.Offer(item).Count(c => c.Available);
                    if (open < Math.Min(2, def.Choices.Count)) problems.Add($"{sid} year {w.Turn}: {def.Id} drawn with {open} answer(s) open");
                    if (!w.InAcademy)
                    {
                        var key = (def.Id, w.ProtagonistId);
                        if (def.Repeatable && last.TryGetValue(key, out var was) && w.Turn - was.turn < EventEngine.WaitAfter(def, was.times))
                            problems.Add($"{sid}: {def.Id} came in year {was.turn} and again in year {w.Turn}");
                        last[key] = (w.Turn, last.TryGetValue(key, out var p) ? p.times + 1 : 1);
                    }
                }
                foreach (var item in tc.Briefing.ToList())
                {
                    var open = tc.Offer(item).Where(c => c.Available).ToList();
                    if (!item.Resolved && open.Count > 0) tc.Resolve(item, open[dice.NextInt(0, open.Count)].Choice.Id);
                }
                tc.EndTurn();
            }
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
