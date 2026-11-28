using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The first years (docs/ACADEMY.md, "The first years"): the steps pinned at the top of the list on the right in a first game. Every
/// step here ticks from the real game: a report answered through the turn, a command carried out by the command bus in the player's own name,
/// a window the client says was opened. Nothing the household or the other houses do ticks one, the list goes when its time is up, and a
/// saved game remembers it.</summary>
public class FirstStepsTests
{
    private static readonly string Root = ContentLocator.FindContentDir();
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(Root);
    private static FirstYearsCatalog Cat => Content.Academy.FirstYears;
    private static readonly string[] Starts = { "the_old_lion", "the_fallen_house", "the_circling_heir", "the_risen_officer", "the_imperial_court", "the_new_man", "the_academy", "the_institute" };

    /// <summary>A new saga of a start, its first years begun as the client begins them, and its first year open.</summary>
    private static (World w, TurnController tc) Begin(string scenario, bool open = true)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(Root, scenario), 1);
        FirstStepsSystem.Begin(w, Cat, scenario);
        var tc = new TurnController(w, Content);
        if (open) tc.BeginTurn();
        return (w, tc);
    }
    private static CommandContext Ctx(World w) => new(w, WorldBuilder.RngFor(w));
    private static bool Run(TurnController tc, ICommand c) => tc.Executor.TryExecute(c, Ctx(tc.World));
    private static FirstStepLine Step(World w, string id) => FirstStepsProjection.Build(w, Content)!.Steps.Single(s => s.Id == id);
    private static void NextYear(TurnController tc) { tc.EndTurn(); tc.BeginTurn(); }
    /// <summary>Play the Academy year out, answering every report with its first open choice.</summary>
    private static void PassOut(World w, TurnController tc)
    {
        for (int guard = 0; w.InAcademy && guard < 10; guard++)
        {
            foreach (var item in tc.BeginTurn()) if (tc.Offer(item).FirstOrDefault(o => o.Available) is { } pick) tc.Resolve(item, pick.Choice.Id);
            tc.EndTurn();
        }
    }

    [Fact]
    public void EveryStart_HasFourToSixSteps_AndTheAcademyStartsCarryTheirFourTermsInEight()
    {
        var windows = new HashSet<string> { "office", "character", "dynasty", "house", "planets", "military", "exchange", "senate", "laws", "houses", "chronicle", "galaxy", "fleet" };
        var rings = new HashSet<string> { "report", "vote", "build", "goods", "order", "fleet", "holdings" };
        foreach (var s in Starts)
        {
            var (w, _) = Begin(s, open: false);
            var steps = FirstStepsSystem.StepsOf(w, Cat);
            if (s is "the_academy" or "the_institute")
            {
                Assert.Equal(8, steps.Count);
                for (int term = 1; term <= 4; term++) Assert.Equal(2, steps.Count(x => x.Term == term));
                Assert.Equal(steps.Select(x => x.Term).OrderBy(t => t), steps.Select(x => x.Term));   // in the order of the terms
            }
            else Assert.InRange(steps.Count, 4, 6);
            Assert.Equal(steps.Count, steps.Select(x => x.Id).Distinct().Count());
            foreach (var step in steps)
            {
                foreach (var d in step.Done.Split('|'))
                    Assert.True(d == "report" || d is "did:vote" or "did:fleet" or "did:buy" or "did:venture" or "did:build" or "did:law" || d == "seen:map"
                        || (d.StartsWith("seen:") && windows.Contains(d[5..].Split(':')[0])), $"{s}/{step.Id}: {d}");
                // Show me opens a window and lights what to press there; for a step that is the opening of a window it opens nothing and
                // lights the window's socket, so the player opens it and the step is theirs
                if (step.Show.Length == 0) Assert.Matches("^seen:[a-z]+$", step.Done); else Assert.Contains(step.Show.Split(':')[0], windows);
                if (step.Done.StartsWith("seen:") && step.Done != "seen:map") Assert.NotEqual(step.Done[5..], step.Show);   // Show me never does the step itself
                Assert.NotEmpty(step.Ring);
                foreach (var r in step.Ring) Assert.True(rings.Contains(r) || (r.StartsWith("rail:") && windows.Contains(r[5..])) || r.StartsWith("tab:"), $"{s}/{step.Id}: {r}");
                // what a player reads: plain, with no em dash, no "(s)", a sentence that ends, a title of a few words
                foreach (var text in new[] { step.Title, step.Text })
                { Assert.DoesNotContain("—", text); Assert.DoesNotContain("(s)", text); }
                Assert.EndsWith(".", step.Text); Assert.InRange(step.Title.Split(' ').Length, 2, 6);
            }
        }
    }

    [Fact]
    public void ASagaThatIsNoOnesFirst_HasNoList()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(Root, "the_old_lion"), 1); var tc = new TurnController(w, Content); tc.BeginTurn();
        Assert.Null(w.FirstSteps); Assert.Null(FirstStepsProjection.Build(w, Content)); Assert.False(FirstStepsSystem.Labels(w, Cat));
        Assert.True(Run(tc, new BuyGoodCommand(w.ProtagonistId, Resource.Materials, 1, Content.Trade)));   // the bus still works without one
        Assert.Null(w.FirstSteps);
    }

    [Fact]
    public void AReportAnswered_TicksItsStep()
    {
        var (w, tc) = Begin("the_old_lion");
        Assert.False(Step(w, "report").Done);
        var item = tc.Briefing.First(b => tc.Offer(b).Any(o => o.Available));
        Assert.True(tc.Resolve(item, tc.Offer(item).First(o => o.Available).Choice.Id));
        Assert.True(Step(w, "report").Done);
        Assert.Equal(1, FirstStepsProjection.Build(w, Content)!.DoneCount);
    }

    [Fact]
    public void AWindowOpened_TicksItsStep_AndTheLedgerWantsItsOwnTab()
    {
        var (w, _) = Begin("the_old_lion");
        Assert.True(FirstStepsSystem.Saw(w, "dynasty")); Assert.False(FirstStepsSystem.Saw(w, "dynasty"));   // the first time only
        Assert.True(Step(w, "heir").Done);
        FirstStepsSystem.Saw(w, "house"); FirstStepsSystem.Saw(w, "house:estate");
        Assert.False(Step(w, "ledger").Done);   // the House window on its estate is not the ledger
        FirstStepsSystem.Saw(w, "house:ledger");
        Assert.True(Step(w, "ledger").Done);
    }

    [Fact]
    public void AFleetOrderedAndABuildingBegun_TickTheirSteps()
    {
        var (w, tc) = Begin("the_old_lion"); var p = w.Protagonist!; var house = w.House(p.HouseId)!;
        var fleet = w.Fleets.Values.First(f => f.OwnerId == house.Id);
        Assert.Equal("fleet:" + fleet.Id, Step(w, "fleet").Show);   // Show me picks the house's own fleet on the map
        var to = w.Galaxy!.Neighbors(fleet.SystemId).First(n => new MoveFleetCommand(p.Id, fleet.Id, n, Content.Fleets).CanExecute(Ctx(w)));
        Assert.True(Run(tc, new MoveFleetCommand(p.Id, fleet.Id, to, Content.Fleets)));
        Assert.True(w.Fleets[fleet.Id].Moving); Assert.True(Step(w, "fleet").Done);

        Assert.False(Step(w, "build").Done);
        var (hold, def) = w.HoldingsOf(house.Id).Where(h => h.IsPlanet).SelectMany(h => Content.Holdings.Buildings.Select(b => (h, b)))
            .First(x => new BuildCommand(p.Id, x.h.Id, x.b.Id, Content.Holdings, Content.Titles).CanExecute(Ctx(w)));
        Assert.True(Run(tc, new BuildCommand(p.Id, hold.Id, def.Id, Content.Holdings, Content.Titles)));
        Assert.True(Step(w, "build").Done);
    }

    [Fact]
    public void AVote_WaitsForAMotion_ThenTicksWhenItIsDeclared()
    {
        var (w, tc) = Begin("the_old_lion"); var p = w.Protagonist!;
        Assert.DoesNotContain(w.Motions, m => m.Open);   // nothing is before the Senate in year 1: the step says why it waits
        Assert.Equal(Cat.NotYet["vote"], Step(w, "vote").NotYet);
        NextYear(tc);
        var motion = w.Motions.First(m => m.Open);
        Assert.Null(Step(w, "vote").NotYet); Assert.False(Step(w, "vote").Done);
        Assert.True(Run(tc, new VoteCommand(p.Id, motion.Id, 1)));
        Assert.True(Step(w, "vote").Done);
    }

    [Fact]
    public void WhatTheHouseholdAndTheOtherHousesDo_TicksNothing()
    {
        var (w, tc) = Begin("the_old_lion");
        // years go by with nothing done by the player's own hand: the steward may build, the marshal buy fuel, the chancellor declare
        // the house's votes and the other houses do as they do, and none of it is the player's
        for (int y = 0; y < 2; y++) NextYear(tc);
        Assert.Empty(w.FirstSteps!.Did); Assert.Empty(w.FirstSteps.Seen);
        Assert.Equal(0, FirstStepsProjection.Build(w, Content)!.DoneCount);
        // an act in another's name does not count either, though it is the same command
        var other = w.Characters.Values.First(c => c.Alive && c.HouseId != w.Protagonist!.HouseId && w.House(c.HouseId) != null && !World.Outsider(c.HouseId));
        FirstStepsSystem.Acted(w, new BuyGoodCommand(other.Id, Resource.Materials, 1, Content.Trade));
        Assert.Empty(w.FirstSteps.Did);
    }

    [Fact]
    public void TheNewMan_HasOnlyStepsHeCanTake_AndPutsHisCoinToWorkEitherWay()
    {
        var (w, tc) = Begin("the_new_man"); var p = w.Protagonist!;
        var steps = FirstStepsSystem.StepsOf(w, Cat);
        Assert.DoesNotContain(steps, s => s.Done.Contains("did:build") || s.Done.Contains("did:fleet"));   // he holds no world and no fleet
        Assert.All(steps, s => Assert.True(s.Done.Contains("did:vote") || FirstStepsSystem.NotYet(w, Cat, s) == null, s.Id));   // only the vote waits, for a motion
        Assert.True(Run(tc, new BuyGoodCommand(p.Id, Resource.Materials, 1, Content.Trade)));
        Assert.True(Step(w, "coin").Done);

        var (w2, tc2) = Begin("the_new_man"); var p2 = w2.Protagonist!; var ctx = Ctx(w2);
        var found = Content.Businesses.Defs.SelectMany(d => w2.Galaxy!.Systems.Values.OrderBy(s => s.Id, StringComparer.Ordinal).Select(s => new FoundBusinessCommand(p2.Id, d.Id, s.Id, Content.Businesses, Content.Holdings, Content.Titles)))
            .First(c => c.CanExecute(ctx));
        Assert.True(Run(tc2, found));
        Assert.True(Step(w2, "coin").Done);
    }

    [Fact]
    public void TheList_GoesWhenPutAway_AndWhenTheThirdYearEnds_AndSaysWhenEveryStepIsDone()
    {
        var (w, tc) = Begin("the_risen_officer");
        Assert.True(FirstStepsSystem.Labels(w, Cat));   // a first game shows the rail's labels by themselves
        FirstStepsSystem.Dismiss(w);
        Assert.Null(FirstStepsProjection.Build(w, Content));

        var (w2, tc2) = Begin("the_risen_officer");
        for (int y = 1; y < Cat.Years; y++) NextYear(tc2);
        Assert.Equal(Cat.Years, w2.Turn); Assert.NotNull(FirstStepsProjection.Build(w2, Content));
        NextYear(tc2);
        Assert.Null(FirstStepsProjection.Build(w2, Content)); Assert.False(FirstStepsSystem.Labels(w2, Cat));

        var (w3, tc3) = Begin("the_risen_officer"); var p3 = w3.Protagonist!;
        var item = tc3.Briefing.First(b => tc3.Offer(b).Any(o => o.Available)); tc3.Resolve(item, tc3.Offer(item).First(o => o.Available).Choice.Id);
        foreach (var place in new[] { "character", "military", "senate", "senate:ladder" }) FirstStepsSystem.Saw(w3, place);
        Assert.False(FirstStepsProjection.Build(w3, Content)!.AllDone);
        NextYear(tc3);
        Assert.True(Run(tc3, new VoteCommand(p3.Id, w3.Motions.First(m => m.Open).Id, -1)));
        var done = FirstStepsProjection.Build(w3, Content)!;
        Assert.True(done.AllDone); Assert.True(FirstStepsSystem.AllDone(w3, Cat));
    }

    [Fact]
    public void TheList_IsKeptWithTheSaga_ThroughASaveBetweenYearsAndOneInTheMiddleOfAYear()
    {
        var (w, tc) = Begin("the_fallen_house");
        var item = tc.Briefing.First(b => tc.Offer(b).Any(o => o.Available)); tc.Resolve(item, tc.Offer(item).First(o => o.Available).Choice.Id);
        FirstStepsSystem.Saw(w, "house:ledger");
        var mid = SaveSystem.Deserialize(tc.SaveMidYear()); var tm = new TurnController(mid, Content); tm.Resume();
        Assert.True(Step(mid, "report").Done); Assert.True(Step(mid, "ledger").Done); Assert.False(Step(mid, "laws").Done);
        Assert.Equal(w.FirstSteps!.Steps, mid.FirstSteps!.Steps);

        tc.EndTurn();
        var between = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        Assert.Equal("the_fallen_house", between.FirstSteps!.Start);
        Assert.True(between.FirstSteps.Did.SetEquals(w.FirstSteps.Did)); Assert.True(between.FirstSteps.Seen.SetEquals(w.FirstSteps.Seen));
    }

    [Fact]
    public void Undo_TakesBackTheAct_NotWhatWasLearnt()
    {
        var (w, tc) = Begin("the_fallen_house");
        var before = SaveSystem.Deserialize(tc.SaveMidYear());   // the year as it opened, as undo loads it
        FirstStepsSystem.Saw(w, "laws"); FirstStepsSystem.Acted(w, new BuyGoodCommand(w.ProtagonistId, Resource.Materials, 1, Content.Trade)); FirstStepsSystem.Dismiss(w);
        FirstStepsSystem.Carry(w.FirstSteps, before);
        Assert.Contains("laws", before.FirstSteps!.Seen); Assert.Contains(FirstStepsSystem.Buy, before.FirstSteps.Did); Assert.True(before.FirstSteps.Dismissed);
    }

    [Fact]
    public void AtTheAcademy_TheListWaitsForThePassingOut_AndABriefedCadetHasNone()
    {
        var (w, tc) = Begin("the_academy", open: false);
        Assert.True(w.InAcademy); Assert.True(w.FirstSteps!.Academy);
        Run(tc, new SetTutorialCommand(w.ProtagonistId, "full"));
        Assert.Null(FirstStepsProjection.Build(w, Content)); Assert.False(FirstStepsSystem.Labels(w, Cat));
        PassOut(w, tc);   // twelve reports answered at the Academy: none of them is the world's
        Assert.False(w.InAcademy); Assert.Empty(w.FirstSteps.Did);
        tc.BeginTurn();
        var list = FirstStepsProjection.Build(w, Content)!;
        Assert.Equal(8, list.Steps.Count); Assert.True(list.Academy); Assert.Equal(0, list.DoneCount);
        Assert.Equal("the Office and you", list.Steps[0].TermSays);
        Assert.Contains(list.Steps, s => s.Id == "fleet");   // Marcus Vega's house has a squadron to order

        var (wi, ti) = Begin("the_institute", open: false);
        Run(ti, new SetTutorialCommand(wi.ProtagonistId, "brief"));
        PassOut(wi, ti); ti.BeginTurn();
        Assert.Null(FirstStepsProjection.Build(wi, Content)); Assert.False(FirstStepsSystem.Active(wi)); Assert.False(FirstStepsSystem.Labels(wi, Cat));

        var (wf, tf) = Begin("the_institute", open: false);   // a full course under the Imperium: the house has no fleet, so the map is found instead
        Run(tf, new SetTutorialCommand(wf.ProtagonistId, "full")); PassOut(wf, tf); tf.BeginTurn();
        var steps = FirstStepsProjection.Build(wf, Content)!.Steps;
        Assert.Contains(steps, s => s.Id == "map"); Assert.DoesNotContain(steps, s => s.Id == "fleet");
        FirstStepsSystem.Saw(wf, FirstStepsSystem.MapPlace);
        Assert.True(Step(wf, "map").Done);
    }

    [Fact]
    public void EveryAcademyTerm_HasAReportCard_ThatSaysWhatMovedThePlace_AndOneTip()
    {
        foreach (var scenario in new[] { "the_academy", "the_institute" })
        {
            var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(Root, scenario), 1); var tc = new TurnController(w, Content);
            var seen = new List<AcademyTermCard>();
            for (int guard = 0; w.InAcademy && guard < 10; guard++)
            {
                foreach (var item in tc.BeginTurn()) if (tc.Offer(item).FirstOrDefault(o => o.Available) is { } pick) tc.Resolve(item, pick.Choice.Id);
                int term = w.Academy!.Term;
                tc.EndTurn();
                var cards = AcademyProjection.Build(tc, Content)!.Reports;
                Assert.Equal(term, cards.Count);
                seen.Add(cards[^1]);
            }
            Assert.Equal(4, seen.Count); Assert.Equal(4, w.Academy!.Reports.Count);
            for (int i = 0; i < seen.Count; i++)
            {
                var c = seen[i]; var r = w.Academy.Reports[i];
                Assert.Equal(i + 1, c.Term);
                Assert.InRange(c.PlaceTo, 1, c.Of); Assert.InRange(c.PlaceFrom, 1, 12);
                Assert.Contains(c.Moved, l => l.Kind == "read" && l.Text.StartsWith("You read ") && l.Text.Contains(r.Marks.ToString()));   // what was read, with its marks
                Assert.Contains(c.Moved, l => l.Kind == "score" && l.Text == $"Your score went from {r.ScoreFrom} to {r.ScoreTo}.");
                Assert.Contains(c.Moved, l => l.Kind == "year" && l.Text.StartsWith("The other "));   // what the rest of the year did
                Assert.Equal(r.ScoreFrom + r.Marks + AcademySystem.StandingWeight * (Math.Max(0, r.StandingTo) - Math.Max(0, r.StandingFrom)), r.ScoreTo);
                Assert.NotEmpty(c.Tip); Assert.DoesNotContain("{", c.Tip);
                foreach (var l in c.Moved.Select(x => x.Text).Append(c.Tip)) { Assert.DoesNotContain("—", l); Assert.DoesNotContain("(s)", l); Assert.DoesNotContain("-1", l); }
            }
            Assert.Equal(w.Academy.Place, seen[^1].PlaceTo);   // the last card ends where the passing out says
            Assert.Equal(Cat.Tip("last"), seen[^1].Tip);
            if (scenario == "the_institute") Assert.Contains(seen, c => c.Moved.Any(l => l.Kind == "banner" && l.Text.Contains("took the banner of")));
        }
    }

    [Fact]
    public void EveryLecture_FoldsToTwoOrThreeSentences_InBothRegisters()
    {
        static int Sentences(string s) => s.Split(new[] { ". ", "? ", "! " }, StringSplitOptions.RemoveEmptyEntries).Length;
        foreach (var l in Content.Academy.Lessons)
        {
            Assert.True(Cat.Lectures.TryGetValue(l.Id, out var folded), l.Id);
            foreach (bool imperium in new[] { false, true })
            {
                string s = folded!.For(imperium);
                Assert.InRange(Sentences(s), 2, 3); Assert.EndsWith(".", s); Assert.DoesNotContain("—", s);
                Assert.True(s.Length < AcademyCatalog.Read(l, imperium).paragraphs.Sum(x => x.Length) / 2, l.Id);   // a fold, not the lecture again
            }
            if (l.ImperiumFull.Count > 0) Assert.NotEqual(folded!.Republic, folded.Imperium);   // a lecture the Imperium reads otherwise folds otherwise
        }
        var (w, tc) = Begin("the_institute", open: false);
        Assert.All(AcademyProjection.Build(tc, Content)!.Lessons, l => Assert.Equal(Cat.Lectures[l.Id].For(true), l.Summary));
    }
}
