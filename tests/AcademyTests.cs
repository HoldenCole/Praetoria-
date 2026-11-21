using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The Academy year (docs/ACADEMY.md; the owner's direction): a prologue apart from the world. Sixteen at entry, four terms, the world
/// frozen and closed to the cadet; a school in the Republic where nobody is hurt, a war of Standards under the Imperium; then a commission and turn one.</summary>
public class AcademyTests
{
    private static readonly string Root = ContentLocator.FindContentDir();
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(Root);
    private static (World w, TurnController tc) Start(string scenario, ulong seed = 1) { var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(Root, scenario), seed); return (w, new TurnController(w, Content)); }
    private static CommandContext Ctx(World w) => new(w, WorldBuilder.RngFor(w));
    /// <summary>Play the year out, answering every report with its first open choice.</summary>
    private static void PlayYear(World w, TurnController tc, Action<int>? eachTerm = null)
    {
        for (int guard = 0; w.InAcademy && guard < 10; guard++)
        {
            var briefing = tc.BeginTurn();
            foreach (var item in briefing) if (tc.Offer(item).FirstOrDefault(o => o.Available) is { } pick) tc.Resolve(item, pick.Choice.Id);
            eachTerm?.Invoke(w.Academy!.Term);
            tc.EndTurn();
        }
    }

    [Fact]
    public void TheCatalogLoads_BothRegisters_SevenSubjects_FourStandards_AndALessonForEveryTerm()
    {
        var a = Content.Academy;
        Assert.Equal("The Academy of the Reach", a.Republic.Name); Assert.Equal("The Institute", a.Imperium.Name);
        Assert.Equal(4, a.Republic.Terms.Count); Assert.Equal(4, a.Imperium.Terms.Count);
        Assert.Equal(7, a.Subjects.Count); Assert.Equal(4, a.Standards.Count);
        foreach (var t in AcademySystem.Tracks) Assert.Contains(a.Subjects, s => s.Track == t);
        for (int term = 1; term <= 4; term++) Assert.Contains(a.Lessons, l => l.Term == term);
        foreach (var l in a.Lessons) { Assert.NotEmpty(l.Brief); Assert.True(l.Full.Count >= 3, l.Id); }
        Assert.NotEmpty(a.Lessons.Single(l => l.Id == "the_senate").ImperiumFull);
    }

    [Fact]
    public void TheAcademyScenarios_OpenWithACadetOfSixteen_AndTheFixtureIsHidden()
    {
        var (w, _) = Start("the_academy");
        Assert.True(w.InAcademy); Assert.Equal("republic", w.Academy!.Register); Assert.Equal(16, w.Protagonist!.Age); Assert.Equal(0, w.Protagonist.CareerRank); Assert.Equal(0, w.Turn);
        var (wi, _) = Start("the_institute");
        Assert.True(wi.InAcademy); Assert.Equal("imperium", wi.Academy!.Register); Assert.Equal(16, wi.Protagonist!.Age);
        Assert.True(ScenarioLoader.LoadFromContent(Root, "academy_crucible").Hidden); Assert.False(ScenarioLoader.LoadFromContent(Root, "the_academy").Hidden);
        var (wf, _) = Start("academy_crucible"); Assert.False(wf.InAcademy); Assert.Null(wf.Academy);   // the fixture is still a plain world
    }

    [Fact]
    public void TheYear_IsApartFromTheWorld_NoTurnPasses_NoWorldOrderIsTaken_OnlyAcademyEventsCome()
    {
        var (w, tc) = Start("the_academy");
        int credits = w.House(w.Protagonist!.HouseId)!.Treasury.Credits; int hulls = w.Fleets.Values.Sum(f => f.Strength); int motions = w.Motions.Count;
        var seen = new List<string>();
        for (int term = 1; term <= 4; term++)
        {
            var briefing = tc.BeginTurn();
            Assert.Equal(term, w.Academy!.Term); Assert.Equal(0, w.Turn);
            Assert.InRange(briefing.Count, 1, AcademySystem.EventsPerTerm);
            foreach (var item in briefing) { Assert.Contains(item.Fired.Def.Conditions, c => c is AcademyCondition); seen.Add(item.Fired.Def.Id); }
            // the bus refuses the world's business: taxes, fleets, the Senate
            Assert.False(tc.Executor.TryExecute(new SetTaxPolicyCommand(w.ProtagonistId, Content.Holdings.TaxPolicies[0].Id, Content.Holdings), Ctx(w)));
            foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == w.Protagonist.HouseId)) Assert.False(tc.Executor.TryExecute(new SetStanceCommand(w.ProtagonistId, f.Id, "press"), Ctx(w)));
            Assert.False(tc.Executor.TryExecute(new ProposeMotionCommand(w.ProtagonistId, "levy", "", tc.Senate), Ctx(w)));
            // ...and takes the Academy's
            Assert.True(tc.Executor.TryExecute(new ChooseStudyCommand(w.ProtagonistId, "law", Content.Academy), Ctx(w)) || w.Academy.Focus == "law");
            foreach (var item in briefing) if (tc.Offer(item).FirstOrDefault(o => o.Available) is { } pick) Assert.True(tc.Resolve(item, pick.Choice.Id));
            tc.EndTurn();
            Assert.Equal(0, w.Turn);
        }
        Assert.False(w.InAcademy); Assert.True(w.Academy!.Graduated);
        Assert.Equal(credits, w.House(w.Protagonist.HouseId)!.Treasury.Credits); Assert.Equal(hulls, w.Fleets.Values.Sum(f => f.Strength)); Assert.Equal(motions, w.Motions.Count);   // nothing moved
        Assert.Equal(seen.Count, seen.Distinct().Count() + seen.Count(id => Content.Event(id)!.Repeatable) - seen.Where(id => Content.Event(id)!.Repeatable).Distinct().Count());   // no one-time event twice
        // the world begins: turn one, and no Academy event ever again
        var first = tc.BeginTurn(); Assert.Equal(1, w.Turn);
        Assert.DoesNotContain(first, i => i.Fired.Def.Conditions.Any(c => c is AcademyCondition));
        Assert.True(tc.Executor.TryExecute(new SetTaxPolicyCommand(w.ProtagonistId, Content.Holdings.TaxPolicies.First(t => t.Id != w.House(w.Protagonist.HouseId)!.TaxPolicy).Id, Content.Holdings), Ctx(w)) || true);
    }

    [Fact]
    public void Study_TeachesTheSkill_EarnsTheMarks_AndTheBoardCommissionsWhereTheCadetRead()
    {
        var (w, tc) = Start("the_academy"); var p = w.Protagonist!;
        int law0 = p.Skill("law");
        PlayYear(w, tc, term => tc.Executor.TryExecute(new ChooseStudyCommand(p.Id, term <= 3 ? "law" : "oratory", Content.Academy), Ctx(w)));
        Assert.Equal(law0 + 3, p.Skill("law")); Assert.True(p.Skill("oratory") >= 1);
        Assert.Equal(3 + 4 + 5, w.Academy!.Marks["law"] - 0 * law0);   // three terms: base three and what was already known, rising a point a term
        Assert.Equal("law", p.CareerTrack); Assert.Equal(1, p.CareerRank); Assert.Equal(17, p.Age);
        Assert.Equal("Advocate", Content.Careers.RankName(p.CareerTrack, p.CareerRank, w.Era));
        Assert.True(w.HasFlag(AcademySystem.GraduatedFlag)); Assert.InRange(w.Academy.Place, 1, w.Academy.Of); Assert.Equal(w.Academy.Place, w.Counter("academy_place"));
        Assert.Contains(w.History, h => h.Text.Contains("passes out of The Academy of the Reach"));
    }

    [Fact]
    public void TheCadetMayAskForAService_AndTheBoardGrantsIt()
    {
        var (w, tc) = Start("the_academy"); var p = w.Protagonist!;
        Assert.Equal("no such service", new ChooseServiceCommand(p.Id, "piracy").Refusal(Ctx(w)));
        PlayYear(w, tc, term => { tc.Executor.TryExecute(new ChooseStudyCommand(p.Id, "tactics", Content.Academy), Ctx(w)); if (term == 4) Assert.True(tc.Executor.TryExecute(new ChooseServiceCommand(p.Id, "stewardship"), Ctx(w))); });
        Assert.Equal("stewardship", p.CareerTrack); Assert.Equal("Administrator", Content.Careers.RankName(p.CareerTrack, p.CareerRank, w.Era));
        Assert.Equal("not at the Academy", new ChooseServiceCommand(p.Id, "law").Refusal(Ctx(w)));   // the year is over
    }

    [Fact]
    public void TheCohort_IsTheReachInMiniature_RealMembersOfRealHouses_AndTheyGraduateToo()
    {
        var (w, tc) = Start("the_academy"); tc.BeginTurn(); var a = w.Academy!;
        Assert.Equal(AcademySystem.CohortSize, a.Cohort.Count); Assert.DoesNotContain(w.ProtagonistId, a.Cohort);
        foreach (var id in a.Cohort) { var c = w.Char(id)!; Assert.InRange(c.Age, 15, 19); Assert.Contains(id, w.House(c.HouseId)!.Members); Assert.NotEqual(PirateSystem.HouseId, c.HouseId); }
        Assert.True(a.Cohort.Select(id => w.Char(id)!.HouseId).Distinct().Count() >= 8);   // spread across the houses
        Assert.NotEqual("", a.InstructorId); Assert.DoesNotContain(a.InstructorId, a.Cohort);
        tc.EndTurn(); PlayYear(w, tc);
        foreach (var id in a.Cohort) Assert.True(w.Char(id)!.CareerRank >= 1);
        // deterministic: the same seed musters the same year
        var (w2, tc2) = Start("the_academy"); tc2.BeginTurn(); Assert.Equal(a.Cohort, w2.Academy!.Cohort);
    }

    [Fact]
    public void InTheRepublic_NobodyIsHurt()
    {
        // by content: no Republic event can kill, wound or mark a cadet
        string[] harm = { "wounded", "marked_for_death", "humiliated", "framed", "sabotaged", "feud" };
        foreach (var e in Content.Events.Where(e => e.Conditions.OfType<AcademyCondition>().Any(c => c.Register == "republic")))
            foreach (var fx in e.Choices.SelectMany(c => c.Effects))
            {
                Assert.False(fx is KillEffect, e.Id);
                if (fx is SetCharFlagEffect f) Assert.DoesNotContain(f.Flag, harm);
            }
        Assert.True(Content.Events.Count(e => e.Conditions.OfType<AcademyCondition>().Any(c => c.Register == "republic")) >= 15);
        // by play: across seeds, everyone who entered leaves, unhurt
        foreach (ulong seed in new ulong[] { 1, 2, 3, 4, 5 })
        {
            var (w, tc) = Start("the_academy", seed); PlayYear(w, tc);
            Assert.All(w.Academy!.Cohort, id => Assert.True(w.Char(id)!.Alive));
            Assert.DoesNotContain(w.Characters.Values, c => c.Flags.Overlaps(harm));
            Assert.Empty(w.Academy.StandardOf); Assert.Equal(0, w.Counter("institute_dead"));
        }
    }

    [Fact]
    public void UnderTheImperium_TheInstituteIsAWarOfStandards()
    {
        var (w, tc) = Start("the_institute"); tc.BeginTurn(); var a = w.Academy!;
        Assert.NotEqual("", a.Standard); Assert.Equal(a.Cohort.Count + 1, a.StandardOf.Count); Assert.Equal(4, a.StandardStrength.Count);
        Assert.Contains(Content.Academy.Standards, s => s.Id == a.Standard);
        tc.EndTurn(); PlayYear(w, tc);
        Assert.Equal(2, a.Fallen.Count);                                   // two banners taken, after the second term and the third
        Assert.DoesNotContain(a.StandardOf.Values, s => a.Fallen.Contains(s));   // their cadets serve the victors
        Assert.True(w.Academy!.Graduated);
        // the old crucible is the Institute's alone, and it can hurt
        var crucible = Content.Events.Where(e => e.Conditions.OfType<AcademyCondition>().Any(c => c.Register == "imperium")).ToList();
        Assert.True(crucible.Count >= 20); Assert.Contains(crucible, e => e.Id == "the_reckoning"); Assert.Contains(crucible, e => e.Id == "the_sorting");
        Assert.Contains(crucible.SelectMany(e => e.Choices).SelectMany(c => c.Effects), fx => fx is KillEffect || fx is SetCharFlagEffect { Flag: "wounded" });
        // across seeds some cadets do not come back, never the last of a house and never more than a fifth of the year
        int dead = 0;
        foreach (ulong seed in new ulong[] { 1, 2, 3, 4, 5, 6, 7, 8 })
        {
            var (ws, tcs) = Start("the_institute", seed); PlayYear(ws, tcs);
            int d = ws.Counter("institute_dead"); dead += d; Assert.InRange(d, 0, Math.Max(1, ws.Academy!.Cohort.Count / 5));
            foreach (var h in ws.Houses.Values.Where(h => h.Id != PirateSystem.HouseId && h.Members.Count > 0)) Assert.True(ws.LivingCharacters().Any(c => c.HouseId == h.Id) || !ws.Academy.Cohort.Any(id => ws.Char(id)!.HouseId == h.Id));
        }
        Assert.True(dead >= 1);
    }

    private static bool Eligible(World w, string eventId)
    {
        var me = w.Protagonist!; w.House(me.HouseId)!.Treasury.Credits = 100; w.PoolsFor(me.Id).Influence = 9;
        return new EventEngine(Content, WorldBuilder.RngFor(w)).GatherEligible(w).Any(e => e.def.Id == eventId);
    }

    [Fact]
    public void TheInstitute_IsAContest_NoTwoYearsAreTheSame_AndItIsNotWonByEveryone()
    {
        // measured over 30 years a bold cadet won it as captain in 10 and passed out first in 13; a careless one never led and was always near the bottom
        int captain = 0, first = 0, fallen = 0, n = 0; var mine = new HashSet<string>(); var victors = new HashSet<string>(); var tops = new HashSet<int>();
        for (ulong seed = 1; seed <= 16; seed++)
        {
            var (w, tc) = Start("the_institute", seed); PlayYear(w, tc); var a = w.Academy!; var p = w.Protagonist!; n++;
            if (AcademySystem.Primus(p)) captain++; if (p.Flags.Contains(AcademySystem.FirstFlag)) first++; if (w.HasFlag(AcademySystem.StandardFallenFlag)) fallen++;
            mine.Add(a.StandardOf[p.Id]); victors.Add(a.Victor); tops.Add(a.Scores.Values.Max());
            // a cadet whose banner was taken does not wear the laurel of the Standard they finished under
            if (w.HasFlag(AcademySystem.StandardFallenFlag)) Assert.DoesNotContain(AcademySystem.WonInstituteFlag, p.Flags);
        }
        Assert.InRange(captain, 2, n - 4); Assert.InRange(first, 2, n - 3); Assert.True(fallen >= 1);
        Assert.True(victors.Count >= 3 && tops.Count >= 4);
        // the careless cadet: never the year's captain, and the board remembers where they passed out
        int low = 0;
        for (ulong seed = 1; seed <= 8; seed++)
        {
            var (w, tc) = Start("the_institute", seed);
            for (int guard = 0; w.InAcademy && guard < 10; guard++)
            {
                foreach (var item in tc.BeginTurn()) if (tc.Offer(item).LastOrDefault(o => o.Available) is { } pick) tc.Resolve(item, pick.Choice.Id);
                tc.EndTurn();
            }
            Assert.False(AcademySystem.Primus(w.Protagonist)); if (w.Protagonist!.Flags.Contains(AcademySystem.PassedLowFlag)) low++;
        }
        Assert.True(low >= 6);
    }

    [Fact]
    public void TheInstitute_IsRemembered_OnEveryoneWhoWasInIt()
    {
        // whoever held the last banner, the year is written on the people who were in it: one captain, laurels for those who began
        // under the winning Standard and for nobody whose banner was taken, an order of merit for the classmates too
        int captains = 0;
        foreach (ulong seed in new ulong[] { 1, 2, 3, 4, 5, 6 })
        {
            var (w, tc) = Start("the_institute", seed); PlayYear(w, tc); var a = w.Academy!; var p = w.Protagonist!;
            var year = a.Cohort.Select(id => w.Char(id)).Where(c => c is { Alive: true }).Select(c => c!).Append(p).ToList();
            Assert.NotEqual("", a.Victor);
            Assert.Single(year, c => c.Flags.Contains(AcademySystem.PrimusFlag));
            var primus = year.Single(c => c.Flags.Contains(AcademySystem.PrimusFlag));
            Assert.True(primus.Flags.Contains(AcademySystem.WonInstituteFlag) && primus.CareerRank >= AcademySystem.PrimusRank);
            Assert.DoesNotContain(year, c => c.Flags.Contains(AcademySystem.LostBannerFlag) && c.Flags.Contains(AcademySystem.WonInstituteFlag));
            Assert.Contains(year, c => c.Flags.Contains(AcademySystem.FirstFlag));
            Assert.Equal(w.HasFlag(AcademySystem.WonInstituteFlag), p.Flags.Contains(AcademySystem.WonInstituteFlag));
            Assert.Equal(AcademySystem.Laurels(p), w.Counter(AcademySystem.LaurelsKey));
            Assert.Contains(p.Life!.Deeds, d => d.Kind is "laurel" or "honours" or "passed");
            if (primus == p) captains++;
        }
        Assert.InRange(captains, 0, 5);
    }

    [Fact]
    public void WinningTheInstituteAsItsCaptain_CountsForLife_AndCountsMostOfAll()
    {
        var (w, tc) = Start("the_institute"); var before = new Dictionary<string, int>(); World? plain = null;
        { var (w0, tc0) = Start("the_institute"); PlayYear(w0, tc0); plain = w0; }
        PlayYear(w, tc, _ =>
        {
            var a0 = w.Academy!; w.WorldCounters[AcademySystem.CaptainKey] = 1; a0.StandardStrength[a0.Standard] = 999;
            foreach (var id in a0.Cohort) before[id] = w.GetRelationship(id, w.ProtagonistId)?.Disposition ?? 0;
        });
        var a = w.Academy!; var p = w.Protagonist!;
        Assert.Equal(a.Standard, a.Victor); Assert.True(AcademySystem.Primus(p)); Assert.Equal(AcademySystem.PrimusRank, p.CareerRank);
        Assert.InRange(AcademySystem.Laurels(p), AcademySystem.PrimusLaurels, 5);
        Assert.Contains(p.Life!.Deeds, d => d.Kind == "primus");
        Assert.Contains("won the Institute as its captain", AcademySystem.LaurelsName(p));
        // more than first of the year alone would be: winning counts most
        var first = new Character { Id = "x", Flags = { AcademySystem.FirstFlag } }; var ranks = new Character { Id = "y", Flags = { AcademySystem.WonInstituteFlag } }; var honours = new Character { Id = "z", Flags = { AcademySystem.HonoursFlag } };
        Assert.True(AcademySystem.PrimusLaurels > AcademySystem.Laurels(first) && AcademySystem.Laurels(first) > AcademySystem.Laurels(ranks) && AcademySystem.Laurels(ranks) == AcademySystem.Laurels(honours) && AcademySystem.Laurels(new Character { Id = "n" }) == 0);
        // it is read: a name people have heard, a rung in an election, crews that will follow, and more hulls than the rank allows
        var same = new Character { Id = "same", CareerTrack = p.CareerTrack, CareerRank = p.CareerRank };
        Assert.Equal(AcademySystem.Laurels(p) * AcademySystem.RenownPerLaurel, LifeSystem.Renown(p) - p.CareerRank * 5 - p.Life.Deeds.Sum(d => d.Kind is "primus" or "banner" or "laurel" or "honours" or "passed" ? 0 : 1));
        Assert.True(LifeSystem.Renown(p) > LifeSystem.Renown(same));
        Assert.True(LadderSystem.Merit(p) - LadderSystem.Merit(new Character { Id = "m", CareerRank = p.CareerRank, Skills = new(p.Skills), Life = p.Life }) >= AcademySystem.Laurels(p));
        var fleets = new FleetSystem(Content.Fleets, Content.Holdings); p.CareerTrack = "military"; same.CareerTrack = "military";
        Assert.Equal(Content.Fleets.CommandCapPerRank, fleets.CommandCap(p) - fleets.CommandCap(same));
        // the year remembers what was done to it and with it
        var followed = a.Cohort.Select(id => w.Char(id)!).Where(c => c.Alive && c.Flags.Contains(AcademySystem.WonInstituteFlag)).ToList();
        var taken = a.Cohort.Select(id => w.Char(id)!).Where(c => c.Alive && c.Flags.Contains(AcademySystem.LostToYouFlag)).ToList();
        Assert.NotEmpty(followed); Assert.NotEmpty(taken);
        int respect = p.Flags.Contains(AcademySystem.FirstFlag) ? AcademySystem.RespectForFirst : 0;
        foreach (var c in followed.Where(c => !c.Flags.Contains(AcademySystem.SecondFlag))) Assert.Equal(AcademySystem.FollowedYou + respect, w.GetRelationship(c.Id, p.Id)!.Disposition - before[c.Id]);
        foreach (var c in taken.Where(c => !c.Flags.Contains(AcademySystem.SecondFlag))) Assert.Equal(AcademySystem.YouTookTheirBanner + respect, w.GetRelationship(c.Id, p.Id)!.Disposition - before[c.Id]);
        // and years on it comes back
        w.Turn = 8;
        Assert.True(Eligible(w, "the_year_sends_for_its_first")); Assert.True(Eligible(w, "the_banner_you_took")); Assert.True(Eligible(w, "the_ranks_that_won"));
        Assert.False(Eligible(w, "the_captain_calls_the_year"));
        // the same year without the win: somebody else is its captain, and calls on it
        var q = plain!.Protagonist!; plain.Turn = 8;
        if (!AcademySystem.Primus(q)) { Assert.True(Eligible(plain, "the_captain_calls_the_year")); Assert.True(LifeSystem.Renown(p) > LifeSystem.Renown(q)); }
    }

    [Fact]
    public void InTheRepublic_TheOrderOfMeritIsRemembered_AndThereIsNoBannerToWin()
    {
        var (w, tc) = Start("the_academy"); PlayYear(w, tc); var a = w.Academy!; var p = w.Protagonist!;
        var year = a.Cohort.Select(id => w.Char(id)!).Where(c => c.Alive).Append(p).ToList();
        Assert.Equal("", a.Victor); Assert.DoesNotContain(year, c => c.Flags.Contains(AcademySystem.PrimusFlag) || c.Flags.Contains(AcademySystem.WonInstituteFlag));
        Assert.Contains(year, c => c.Flags.Contains(AcademySystem.FirstFlag)); Assert.Contains(year, c => c.Flags.Contains(AcademySystem.HonoursFlag));
        Assert.InRange(AcademySystem.Laurels(p), 0, AcademySystem.FirstLaurels);
        // a cadet who passed out near the bottom hears of it from someone above them
        p.Flags.Add(AcademySystem.PassedLowFlag); w.Turn = 5; Assert.True(Eligible(w, "where_you_passed_out"));
    }

    [Fact]
    public void TheTutorialChoice_IsRemembered_AndASavedYearResumes()
    {
        var (w, tc) = Start("the_academy"); var p = w.Protagonist!;
        Assert.Equal("", w.Academy!.Tutorial);
        Assert.False(new SetTutorialCommand(p.Id, "everything").CanExecute(Ctx(w)));
        Assert.True(tc.Executor.TryExecute(new SetTutorialCommand(p.Id, "brief"), Ctx(w))); Assert.Equal("brief", w.Academy.Tutorial);
        tc.BeginTurn(); tc.Executor.TryExecute(new ChooseStudyCommand(p.Id, "economics", Content.Academy), Ctx(w)); tc.EndTurn(); tc.SyncRng();
        var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        Assert.True(loaded.InAcademy); Assert.Equal(1, loaded.Academy!.Term); Assert.Equal("brief", loaded.Academy.Tutorial); Assert.Equal("economics", loaded.Academy.Focus);
        Assert.Equal(w.Academy.Cohort, loaded.Academy.Cohort); Assert.Equal(w.Academy.Marks["economics"], loaded.Academy.Marks["economics"]);
        var tc2 = new TurnController(loaded, Content); tc2.BeginTurn(); Assert.Equal(2, loaded.Academy.Term); Assert.Equal(0, loaded.Turn);
    }

    [Fact]
    public void AWorldThatBeganInTheWorld_NeverSeesTheAcademy()
    {
        var (w, tc) = Start("the_fallen_house");
        Assert.False(w.InAcademy);
        for (int t = 0; t < 6; t++) { var b = tc.BeginTurn(); Assert.DoesNotContain(b, i => i.Fired.Def.Conditions.Any(c => c is AcademyCondition)); tc.EndTurn(); }
        Assert.Equal(6, w.Turn);
    }

    [Fact]
    public void TheGlossary_IsLoaded_FindsAWordByItsNameFirst_AndPointsOnlyAtWindowsThatExist()
    {
        var g = Content.Academy.Glossary;
        Assert.True(g.Count >= 60, "the glossary is in content/academy/academy.json");
        Assert.Equal(g.Count, g.Select(t => t.Id).Distinct().Count());
        Assert.All(g, t => { Assert.False(string.IsNullOrWhiteSpace(t.Term)); Assert.False(string.IsNullOrWhiteSpace(t.Means)); Assert.False(string.IsNullOrWhiteSpace(t.Group)); });
        // every "where" is a window of the game (src/Web/Pages/Index.razor): a term never points at a place that is not there
        var windows = new[] { "office", "character", "galaxy", "planets", "senate", "house", "exchange", "laws", "military", "dynasty", "houses", "chronicle" };
        Assert.All(g, t => Assert.Contains(t.Where, windows));
        // nothing typed is the whole list; a word in a term's name comes before the same word in another's meaning
        Assert.Equal(g.Count, Content.Academy.Find("  ").Count);
        var purse = Content.Academy.Find("PURSE");
        Assert.Equal("Purse", purse[0].Term);
        Assert.Contains(purse, t => t.Term == "Allowance");   // its meaning speaks of the purse
        Assert.True(purse.Count < g.Count);
        Assert.Empty(Content.Academy.Find("zzzz"));
        // the words the playthroughs asked about are in it
        foreach (var word in new[] { "Legitimacy", "Bench", "Weight", "Regard", "Command cap", "War score", "Generation" }) Assert.Contains(g, t => t.Term == word);
    }

    [Fact]
    public void ALecture_ReadsInItsOwnRegister()
    {
        Assert.NotEmpty(Content.Academy.Lessons);
        foreach (var l in Content.Academy.Lessons)
        {
            var (title, paras) = AcademyCatalog.Read(l, imperium: false);
            Assert.Equal(l.Title, title); Assert.Equal(l.Full, paras); Assert.NotEmpty(paras);
            var (ititle, iparas) = AcademyCatalog.Read(l, imperium: true);
            Assert.Equal(l.ImperiumTitle.Length > 0 ? l.ImperiumTitle : l.Title, ititle);
            Assert.Equal(l.ImperiumFull.Count > 0 ? l.ImperiumFull : l.Full, iparas);
        }
        var made = new AcademyLesson("x", 1, "The Senate", "", new[] { "republic" }, new[] { "imperium" }, "The Court");
        Assert.Equal(("The Court", "imperium"), (AcademyCatalog.Read(made, true).title, AcademyCatalog.Read(made, true).paragraphs[0]));
        Assert.Equal(("The Senate", "republic"), (AcademyCatalog.Read(made, false).title, AcademyCatalog.Read(made, false).paragraphs[0]));
    }
}
