using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Presentation;
using Praetoria.Core.Progression;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>A title ladder whose rungs are earned, and what legitimacy is made of (docs/TITLES.md; docs/PLAYTHROUGH.md, Hard: "A
/// title ladder with rungs that must be earned: land, seats or a carried vote above Prefect; a breakdown of what legitimacy is
/// made of (P94, P83, S58)").</summary>
public class EarnedTitlesTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static World Rings() { var m = typeof(RegionSystemTests).GetMethod("Rings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!; return (World)m.Invoke(null, new object[] { 0 })!; }

    /// <summary>A chamber of three: House Vega (the player's, a Prefect with one world), House Aurelian (a Proconsul who holds the
    /// capital and weighs eight) and House Corwin (a Citizen with a world, who weighs one).</summary>
    private static (World w, ProgressionSystem prog, SenateSystem senate, CommandContext ctx, House vega) Chamber(string title = "baron", int legitimacy = 45)
    {
        var w = Rings(); w.Turn = 5;
        var vega = w.House("vega")!; vega.Title = title; vega.Legitimacy = legitimacy; w.Holding("barony")!.Kind = Holding.Planet;
        w.House("aurelian")!.SphereInfluence["senate"] = 4; w.Pools["self"].Influence = 6;
        var senate = new SenateSystem(Content.Motions, Content.Titles); var prog = new ProgressionSystem(Content.Titles) { Senate = senate }; senate.TitleLadder = prog;
        LegitimacyBook.Open(w);
        return (w, prog, senate, new CommandContext(w, TestContent.Rng()), vega);
    }
    private static void Regard(World w, string of, string to, int d) => w.Relationship(of, to).Disposition = d;
    private static (TurnController tc, World w, House house, Character me, CommandContext ctx) Start(ulong seed = 5)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), seed); var tc = new TurnController(w, Content);
        var me = w.Protagonist!; return (tc, w, w.House(me.HouseId)!, me, new CommandContext(w, WorldBuilder.RngFor(w)));
    }
    private static void Year(TurnController tc, bool answer = true)
    {
        foreach (var i in tc.BeginTurn()) { var ch = answer ? tc.Offer(i).FirstOrDefault(x => x.Available) : null; if (ch != null) tc.Resolve(i, ch.Choice.Id); }
        tc.EndTurn();
    }
    private static int Sum(House h) => h.LegitimacyRecord.Began + h.LegitimacyRecord.Totals.Values.Sum();

    // ---------------------------------------------------------------- the rungs

    [Fact]
    public void The_numbers_of_the_ladder_are_what_the_page_says()
    {
        Assert.Equal(3, ProgressionSystem.LandedFromRank); Assert.Equal(ProgressionSystem.LandedFromRank, Title.RankOf(Title.Count));
        Assert.Equal(new[] { 0, 0, 0, 1, 2, 3 }, Enumerable.Range(0, 6).Select(ProgressionSystem.WorldsFor).ToArray());
        Assert.Equal(new[] { 0, 0, 0, 8, 12, 16 }, Enumerable.Range(0, 6).Select(ProgressionSystem.SeatsFor).ToArray());
        Assert.Equal(3, ProgressionSystem.AskAgainYears); Assert.Equal(4, ProgressionSystem.RivalPetitionOneIn); Assert.Equal(15, SenateSystem.ElevationWellAbove);
        var def = Content.Motions.Def(ProgressionSystem.PetitionKind)!; Assert.True(def.Rank); Assert.Equal("house", def.Target); Assert.Equal("elevation", def.Interest); Assert.Equal(0, def.LegitimacyTarget);
        Assert.Contains("{rank}", def.Description); Assert.True(LadderSystem.IsBusinessOf("law", "elevation") || LadderSystem.Committees.Any(c => LadderSystem.IsBusinessOf(c, "elevation")));
    }

    [Fact]
    public void Up_to_Prefect_a_petition_is_granted_at_once_and_gives_no_legitimacy()
    {
        var (w, prog, _, ctx, vega) = Chamber("landless", 30); var ex = new CommandExecutor(); var cmd = new PetitionTitleCommand("self", prog);
        Assert.Null(cmd.Refusal(ctx)); Assert.True(ex.TryExecute(cmd, ctx));
        Assert.Equal(Title.Knight, vega.Title); Assert.Equal(30, vega.Legitimacy); Assert.Equal(4, w.Pools["self"].Influence); Assert.Empty(w.Motions);   // granted: no vote, and no +5 (S58)
        Assert.Contains("one a year", cmd.Refusal(ctx)); Assert.False(cmd.CanExecute(ctx));
        w.Turn++; Assert.True(ex.TryExecute(cmd, ctx)); Assert.Equal(Title.Baron, vega.Title); Assert.Equal(30, vega.Legitimacy);
        // three clicks no longer make a Legate of a house of legitimacy 30: the standing was never made by the rising
        w.Turn++; Assert.Equal("needs legitimacy 40: the house has 30", cmd.Refusal(ctx));
        Assert.Empty(vega.LegitimacyRecord.Totals); Assert.Empty(vega.LegitimacyRecord.Changes);
        // and what it lacks is said: the influence wanted and the influence in hand
        vega.Title = Title.Landless; w.Turn++; w.Pools["self"].Influence = 1; Assert.Equal("needs 2 influence this year (1 left)", cmd.Refusal(ctx)); Assert.False(cmd.CanExecute(ctx));
        vega.Legitimacy = 3; Assert.Equal("needs legitimacy 10: the house has 3", cmd.Refusal(ctx));
    }

    [Fact]
    public void Above_Prefect_a_petition_goes_before_the_Senate_and_the_house_rises_only_when_it_is_carried()
    {
        var (w, prog, senate, ctx, vega) = Chamber(); var ex = new CommandExecutor(); var cmd = new PetitionTitleCommand("self", prog);
        var next = prog.NextTitle(w, "vega")!; Assert.Equal(Title.Count, next.Id); Assert.True(prog.Voted(next)); Assert.False(prog.Voted(Content.Titles.ById(Title.Baron)!));
        Assert.Null(cmd.Refusal(ctx)); Assert.True(ex.TryExecute(cmd, ctx));
        // asking is not rising: the petition is a motion on the docket, the house's own vote cast for it
        Assert.Equal(Title.Baron, vega.Title); Assert.Equal(4, w.Pools["self"].Influence);
        var m = SenateSystem.Docket(w).Single(); Assert.Equal(ProgressionSystem.PetitionKind, m.Kind); Assert.Equal("vega", m.ProposerHouseId); Assert.Equal("vega", m.TargetId); Assert.Equal(Title.Count, m.TitleId); Assert.Equal(1, m.Votes["vega"]);
        Assert.Same(m, ProgressionSystem.PetitionOf(w, "vega"));
        Assert.Equal("House Vega petitions the Senate that House Vega be raised to the rank of Legate.", senate.Describe(w, m));
        Assert.Contains(w.History, h => h.Text.StartsWith("Before the Senate: House Vega petitions"));
        Assert.Contains("before the Senate already", cmd.Refusal(ctx)); Assert.False(cmd.CanExecute(ctx));
        senate.Tick(w); Assert.True(m.Open); Assert.Equal(Title.Baron, vega.Title);   // the same year: too soon
        // the year turns; the house that weighs most thinks well of the petitioner; the chamber carries it
        w.Turn++; Regard(w, "aurelian_head", "self", 40); Assert.True(senate.Lean(w, m, "aurelian") > 0);
        senate.Tick(w);
        Assert.True(m.Resolved); Assert.True(m.Passed, $"for {m.ForWeight} against {m.AgainstWeight}");
        Assert.Equal(Title.Count, vega.Title); Assert.Equal(45, vega.Legitimacy);   // rank follows standing and does not make it
        Assert.Contains(w.History, h => h.Text.Contains("House Vega is GRANTED the rank of Legate by vote of the Senate"));
        Assert.Contains(w.News, n => n.Tone == "good" && n.Text.Contains("carried your petition") && n.Target == "senate:ladder");
        Assert.Equal(1, w.Counter("senate_standing:vega"));
        Assert.Contains("one a year", cmd.Refusal(ctx));
        // the next rung asks more of both kinds
        w.Turn++; vega.Legitimacy = 70; Assert.Equal("a Proconsul holds 2 worlds or has 12 seats in the Senate: the house holds 1 and has 3 seats", cmd.Refusal(ctx));
    }

    [Fact]
    public void A_petition_thrown_out_leaves_the_house_where_it_was_and_it_waits_to_ask_again()
    {
        var (w, prog, senate, ctx, vega) = Chamber(); var ex = new CommandExecutor(); var cmd = new PetitionTitleCommand("self", prog);
        Assert.True(ex.TryExecute(cmd, ctx)); var m = SenateSystem.Docket(w).Single();
        w.Turn++; Regard(w, "aurelian_head", "self", -40); Regard(w, "corwin_head", "self", -40); Assert.True(senate.Lean(w, m, "aurelian") < 0);
        senate.Tick(w);
        Assert.True(m.Resolved); Assert.False(m.Passed); Assert.Equal(Title.Baron, vega.Title); Assert.Equal(45, vega.Legitimacy);
        Assert.Contains(w.History, h => h.Text.StartsWith("The Senate throws it out") && h.Text.Contains("rank of Legate"));
        Assert.Contains(w.News, n => n.Tone == "warn" && n.Text.Contains("thrown out your petition") && n.Text.Contains("2 years"));
        foreach (var open in w.Motions.Where(x => x.Open).ToList()) w.Motions.Remove(open);   // whatever the others have moved since
        w.Pools["self"].Influence = 6;
        Assert.Equal("the Senate has heard the house on this lately: it may ask again in 2 years", cmd.Refusal(ctx));
        w.Turn++; Assert.Equal("the Senate has heard the house on this lately: it may ask again in 1 year", cmd.Refusal(ctx));
        w.Turn++; Assert.Equal(5 + ProgressionSystem.AskAgainYears, w.Turn); Assert.Null(cmd.Refusal(ctx));
    }

    [Fact]
    public void Seats_in_the_Senate_stand_in_place_of_land_for_a_petition_and_for_a_claim()
    {
        var (w, prog, senate, ctx, vega) = Chamber(); var cmd = new PetitionTitleCommand("self", prog); var next = prog.NextTitle(w, "vega")!;
        w.Holding("barony")!.OwnerId = "aurelian"; w.HoldingsChanged();   // a house of politicians: no world at all
        Assert.Equal(2, prog.Seats(w, "vega")); Assert.Equal(senate.Weight(w, "vega"), prog.Seats(w, "vega"));
        var f = prog.FootingFor(w, "vega", next); Assert.True(f.Asked); Assert.False(f.ByLand); Assert.False(f.BySeats); Assert.False(f.Met); Assert.Equal((1, 0, 8, 2), (f.WorldsWanted, f.WorldsHeld, f.SeatsWanted, f.SeatsHeld));
        Assert.Equal("a Legate holds a world or has 8 seats in the Senate: the house holds 0 and has 2 seats", cmd.Refusal(ctx));
        vega.Claims.Add(Title.Count); Assert.Equal(cmd.Refusal(ctx), new ClaimTitleCommand("self", prog).Refusal(ctx));
        // six of influence in the chamber and a Prefect's two make eight seats
        vega.SphereInfluence["senate"] = 6; f = prog.FootingFor(w, "vega", next); Assert.True(f.BySeats); Assert.False(f.ByLand); Assert.True(f.Met);
        Assert.Null(cmd.Refusal(ctx)); Assert.Null(new ClaimTitleCommand("self", prog).Refusal(ctx));
        // the land alone does as well, as before
        vega.SphereInfluence["senate"] = 0; w.Holding("barony")!.OwnerId = "vega"; w.HoldingsChanged(); f = prog.FootingFor(w, "vega", next); Assert.True(f.ByLand); Assert.False(f.BySeats); Assert.Null(cmd.Refusal(ctx));
        // nothing is asked below Legate
        Assert.False(prog.FootingFor(w, "vega", Content.Titles.ById(Title.Baron)!).Asked); Assert.True(prog.FootingFor(w, "vega", Content.Titles.ById(Title.Baron)!).Met);
        // in a world built without a Senate the seats count for nothing, and a petition is granted as it was
        var bare = new ProgressionSystem(Content.Titles); w.Holding("barony")!.OwnerId = "aurelian"; w.HoldingsChanged(); vega.SphereInfluence["senate"] = 9;
        Assert.Equal("a Legate holds a world: the house holds 0", bare.PetitionRefusal(w, "self")); Assert.Equal(0, bare.Seats(w, "vega"));
        w.Holding("barony")!.OwnerId = "vega"; w.HoldingsChanged(); Assert.False(bare.Voted(next)); Assert.Null(bare.Petition(w, "self")); Assert.Equal(Title.Count, vega.Title); Assert.Equal(45, vega.Legitimacy);
    }

    [Fact]
    public void The_chamber_must_have_room_and_a_rank_is_not_moved_from_the_floor()
    {
        var (w, prog, senate, ctx, _) = Chamber(); var cmd = new PetitionTitleCommand("self", prog);
        // a rank is asked for from the ladder, not moved as a motion
        Assert.Equal("a rank is asked for by petition, from the ladder of titles", new ProposeMotionCommand("self", ProgressionSystem.PetitionKind, "vega", senate).Refusal(ctx));
        Assert.DoesNotContain(CourtProjectionKinds(w, senate), k => k == ProgressionSystem.PetitionKind);
        // one matter of the house's at a time
        var mine = senate.Add(w, "vega", "levy", ""); Assert.Equal("the house has a motion before the Senate already: one at a time", cmd.Refusal(ctx)); w.Motions.Remove(mine);
        // the docket holds two
        senate.Add(w, "aurelian", "levy", ""); Assert.Null(cmd.Refusal(ctx)); senate.Add(w, "corwin", "relief", "rim");
        Assert.Equal("the docket is full this session: the Senate hears two matters at a time", cmd.Refusal(ctx));
        w.Motions.Clear(); Assert.Null(cmd.Refusal(ctx));
        // a house that has left the Reach does not petition its Senate
        w.House("vega")!.PolityId = "gone"; Assert.Equal("a house that has left the Reach does not petition its Senate", cmd.Refusal(ctx));
    }
    private static List<string> CourtProjectionKinds(World w, SenateSystem senate) => senate.Motions.Defs.Where(d => d.Id != "petition" && !d.Rank).Select(d => d.Id).ToList();

    [Fact]
    public void The_Senates_leave_lapses_for_a_house_that_is_no_longer_where_it_asked_from()
    {
        // the land is lost between the asking and the vote
        var (w, prog, senate, ctx, vega) = Chamber(); Assert.True(new CommandExecutor().TryExecute(new PetitionTitleCommand("self", prog), ctx)); var m = SenateSystem.Docket(w).Single();
        w.Turn++; Regard(w, "aurelian_head", "self", 40); w.Holding("barony")!.OwnerId = "corwin"; w.HoldingsChanged();
        senate.Tick(w);
        Assert.True(m.Passed); Assert.Equal(Title.Baron, vega.Title);
        Assert.Contains(w.History, h => h.Text.Contains("leave for House Vega to take the rank of Legate lapses: the house no longer has the land or the seats"));
        Assert.Contains(w.News, n => n.Tone == "warn" && n.Text.Contains("its leave lapses"));
        // the house has risen another way in the meantime: a claim pressed
        var (w2, prog2, senate2, ctx2, vega2) = Chamber(); Assert.True(new CommandExecutor().TryExecute(new PetitionTitleCommand("self", prog2), ctx2)); var m2 = SenateSystem.Docket(w2).Single();
        w2.Turn++; Regard(w2, "aurelian_head", "self", 40); vega2.Claims.Add(Title.Count); w2.Pools["self"].Agents = 3; Assert.True(new CommandExecutor().TryExecute(new ClaimTitleCommand("self", prog2), ctx2)); Assert.Equal(Title.Count, vega2.Title);
        senate2.Tick(w2);
        Assert.True(m2.Passed); Assert.Equal(Title.Count, vega2.Title);   // not two rungs for one petition
        Assert.Contains(w2.History, h => h.Text.Contains("lapses: the house is not on the rung it asked from"));
    }

    [Fact]
    public void The_houses_lean_on_a_petition_for_reasons_the_roll_call_can_say()
    {
        var (w, prog, senate, ctx, vega) = Chamber(); Assert.True(new CommandExecutor().TryExecute(new PetitionTitleCommand("self", prog), ctx)); var m = SenateSystem.Docket(w).Single();
        var aurelian = w.House("aurelian")!; var corwin = w.House("corwin")!;
        int Lean(string id) => senate.Lean(w, m, id); List<string> Why(string id) => senate.Why(w, m, id).Select(x => x.why).ToList();
        Assert.Equal(3, Lean("vega")); Assert.Contains("it moved the motion", Why("vega"));
        int plain = Lean("aurelian");
        // regard: counted once for the mover and once for the house to be raised, which are the same house
        Regard(w, "aurelian_head", "self", 40); Assert.Equal(plain + 2, Lean("aurelian")); Assert.Contains("what its head thinks of House Vega", Why("aurelian")); Assert.Contains("whether its head would see House Vega raised", Why("aurelian"));
        Regard(w, "aurelian_head", "self", -40); Assert.Equal(plain - 2, Lean("aurelian")); Regard(w, "aurelian_head", "self", 0);
        // a house that stands well above what the rung asks is thought to deserve it
        vega.Legitimacy = 40 + SenateSystem.ElevationWellAbove; Assert.Equal(plain + 1, Lean("aurelian")); Assert.Contains("House Vega stands well above what the rank asks", Why("aurelian")); vega.Legitimacy = 45;
        // an old family and a new name
        vega.Origin = "new"; aurelian.Origin = "old"; Assert.Equal(plain - 1, Lean("aurelian")); Assert.Contains("an old family does not care to see a new name rise", Why("aurelian")); aurelian.Origin = "";
        // a rival claimant, an equal who would be passed, a house at war with the petitioner
        aurelian.Claims.Add(Title.Count); Assert.Equal(plain - 2, Lean("aurelian")); Assert.Contains("it claims the same rank", Why("aurelian")); aurelian.Claims.Clear();
        int corwinPlain = Lean("corwin"); corwin.Title = Title.Baron; Assert.Equal(corwinPlain - 1, Lean("corwin")); Assert.Contains("it is of House Vega's rank today, and would be passed", Why("corwin")); corwin.Title = Title.Landless;
        w.Wars.Add(new War { HouseA = "corwin", HouseB = "vega" }); Assert.Equal(corwinPlain - 2, Lean("corwin")); Assert.Contains("it is at war with House Vega", Why("corwin")); w.Wars.Clear();
        // the reasons add up to the lean, and lobbying bears on it as on any motion
        Assert.Equal(Lean("aurelian"), senate.Why(w, m, "aurelian").Sum(x => x.by));
        Assert.Null(new LobbyCommand("self", m.Id, "aurelian", 1).Refusal(ctx)); Assert.True(new CommandExecutor().TryExecute(new LobbyCommand("self", m.Id, "aurelian", 1), ctx));
        Assert.Equal(plain + SenateSystem.LobbyWeight, Lean("aurelian"));
        // the forecast shown before the asking is the docket's own count
        var (w3, prog3, senate3, _, _) = Chamber(); var before = senate3.Forecast(w3, "vega", ProgressionSystem.PetitionKind, "vega", titleId: Title.Count);
        var asked = prog3.Petition(w3, "self")!; Assert.Equal(before, senate3.Tally(w3, asked));
    }

    [Fact]
    public void Seizing_and_claiming_are_as_they_were()
    {
        var (w, prog, _, ctx, vega) = Chamber(); var ex = new CommandExecutor(); var me = w.Char("self")!;
        // to seize: an officer, and hulls of the house's own; no land asked, no seats, no vote, and legitimacy unmoved
        var seize = new SeizeTitleCommand("self", prog); Assert.Equal("only an officer of some rank has the men to do it", seize.Refusal(ctx));
        me.CareerRank = 3; Assert.Equal("needs 6 hulls of the house's own to take the rank of Legate: it has 0", seize.Refusal(ctx)); Assert.False(seize.CanExecute(ctx));
        w.Fleets["f"] = new Fleet { Id = "f", Name = "The House Fleet", OwnerId = "vega", SystemId = "tessaly", Strength = 6 }; w.Holding("barony")!.OwnerId = "corwin"; w.HoldingsChanged();
        w.Pools["self"].Treasury = 1; Assert.StartsWith("needs 2 treasury actions this year", seize.Refusal(ctx)); w.Pools["self"].Treasury = 2; w.Pools["self"].Agents = 3;
        Assert.Null(seize.Refusal(ctx)); Assert.True(ex.TryExecute(seize, ctx)); Assert.Equal(Title.Count, vega.Title); Assert.Equal(45, vega.Legitimacy); Assert.Equal(1, w.Counter("seizures")); Assert.Empty(w.Motions);
        // to claim: the claim in hand, and the land or the seats; twelve of legitimacy, entered under titles, and a trail
        w.Turn++; vega.Legitimacy = 70; var claim = new ClaimTitleCommand("self", prog); Assert.Equal("a Proconsul holds 2 worlds or has 12 seats in the Senate: the house holds 0 and has 3 seats", claim.Refusal(ctx));
        vega.SphereInfluence["senate"] = 9; Assert.Equal("the house holds no claim to the rank of Proconsul", claim.Refusal(ctx));
        vega.Claims.Add(Title.Duke); w.Pools["self"].Influence = 6; Assert.Null(claim.Refusal(ctx)); Assert.True(ex.TryExecute(claim, ctx));
        Assert.Equal(Title.Duke, vega.Title); Assert.Equal(82, vega.Legitimacy); Assert.Equal(1, w.Counter("corruption")); Assert.DoesNotContain(Title.Duke, vega.Claims);
        Assert.Equal(12, vega.LegitimacyRecord.Totals[LegitimacyBook.Titles]); Assert.Equal("A claim pressed to the rank of Proconsul", vega.LegitimacyRecord.Changes[^1].Line);
    }

    // ---------------------------------------------------------------- the other houses

    [Fact]
    public void A_rival_petitions_by_the_same_command_and_the_player_votes_on_it()
    {
        var (w, prog, senate, ctx, _) = Chamber(); var ex = new CommandExecutor(); var corwin = w.House("corwin")!; var head = w.Char("corwin_head")!;
        w.Holding("corwin_world")!.Kind = Holding.Planet; corwin.Title = Title.Baron; corwin.Legitimacy = 44;
        // about one year in four, by the house's name and the year: never by chance
        var years = Enumerable.Range(6, 40).Where(t => { w.Turn = t; return prog.RivalWouldPetition(w, corwin, head); }).ToList();
        Assert.InRange(years.Count, 4, 18); w.Turn = years[0]; Assert.Equal(prog.RivalWouldPetition(w, corwin, head), prog.RivalWouldPetition(w, corwin, head));
        corwin.Legitimacy = 30; Assert.False(prog.RivalWouldPetition(w, corwin, head)); corwin.Legitimacy = 44;   // it must have the standing, as the player must
        var cmd = new PetitionTitleCommand(head.Id, prog); Assert.Null(cmd.Refusal(ctx)); Assert.True(ex.TryExecute(cmd, ctx));
        var m = SenateSystem.Docket(w).Single(); Assert.Equal("corwin", m.ProposerHouseId); Assert.Equal(Title.Count, m.TitleId); Assert.Equal(Title.Baron, corwin.Title);
        Assert.Contains(w.News, n => n.Tone == "info" && n.Text.StartsWith("House Corwin petitions the Senate for the rank of Legate") && n.Target == "senate:chamber" && n.SubjectId == "corwin");
        // only one house's petition for a rank is before the Senate at a time, among the rivals
        var aurelian = w.House("aurelian")!; aurelian.Legitimacy = 95; aurelian.SphereInfluence["senate"] = 20; var ah = w.Char("aurelian_head")!;
        Assert.DoesNotContain(Enumerable.Range(w.Turn, 12), t => { int was = w.Turn; w.Turn = t; bool would = prog.RivalWouldPetition(w, aurelian, ah); w.Turn = was; return would; });
        // the player's house votes on it like any motion, and the vote is counted
        var vote = new VoteCommand("self", m.Id, -1); Assert.Null(vote.Refusal(ctx)); Assert.True(ex.TryExecute(vote, ctx)); Assert.Equal(-1, m.Votes["vega"]);
        w.Turn++; Regard(w, "aurelian_head", "corwin_head", -40); senate.Tick(w);
        Assert.True(m.Resolved); Assert.False(m.Passed); Assert.True(m.AgainstWeight >= senate.Weight(w, "vega") + senate.Weight(w, "aurelian")); Assert.Equal(Title.Baron, corwin.Title);
        Assert.DoesNotContain(w.News, n => n.Text.Contains("your petition"));   // it was not the player's
        // carried, a rival rises exactly as the player's house would
        var (w2, prog2, senate2, ctx2, _) = Chamber(); var corwin2 = w2.House("corwin")!; w2.Holding("corwin_world")!.Kind = Holding.Planet; corwin2.Title = Title.Baron; corwin2.Legitimacy = 44;
        Assert.True(new CommandExecutor().TryExecute(new PetitionTitleCommand("corwin_head", prog2), ctx2)); var m2 = SenateSystem.Docket(w2).Single();
        Assert.True(new CommandExecutor().TryExecute(new VoteCommand("self", m2.Id, 1), ctx2)); w2.Turn++; Regard(w2, "aurelian_head", "corwin_head", 40); senate2.Tick(w2);
        Assert.True(m2.Passed); Assert.Equal(Title.Count, corwin2.Title); Assert.Equal(44, corwin2.Legitimacy); Assert.True(m2.ForWeight >= senate2.Weight(w2, "vega") + senate2.Weight(w2, "aurelian"));
    }

    [Fact]
    public void In_a_game_the_other_houses_rise_above_Prefect_only_by_a_vote_of_the_Senate()
    {
        // whether a rival's petition comes up and carries inside thirty years is the draw of one game, and a new report anywhere in
        // the content changes the draw: a second and a third game are played if the first has none carried, and everything else
        // below is held in every game played
        var lost = new List<string>();
        foreach (ulong seed in new ulong[] { 5, 3, 2 })
        {
            var (tc, w, house, me, _) = Start(seed); var seen = new List<Motion>(); bool voted = false;
            var rank = w.Houses.Values.ToDictionary(h => h.Id, h => Title.RankOf(h.Title));
            for (int y = 0; y < 30 && !w.IsDynastyDead; y++)
            {
                foreach (var i in tc.BeginTurn()) { var ch = tc.Offer(i).FirstOrDefault(x => x.Available); if (ch != null) tc.Resolve(i, ch.Choice.Id); }
                foreach (var m in SenateSystem.Docket(w).Where(m => m.Kind == ProgressionSystem.PetitionKind && m.ProposerHouseId != house.Id))
                {
                    if (!seen.Contains(m)) seen.Add(m);
                    // the player may vote on a rival's petition the year it is voted (whoever holds the seat that year: the old head may have died)
                    if (!voted) { var vote = new VoteCommand(w.Protagonist!.Id, m.Id, -1); Assert.Null(vote.Refusal(new CommandContext(w, WorldBuilder.RngFor(w)))); voted = tc.Executor.TryExecute(vote, new CommandContext(w, WorldBuilder.RngFor(w))); Assert.Equal(-1, m.Votes[house.Id]); }
                }
                Assert.True(SenateSystem.Docket(w).Count(m => m.Kind == ProgressionSystem.PetitionKind && m.ProposerHouseId != house.Id) <= 1);
                tc.EndTurn();
                foreach (var h in w.Houses.Values.Where(h => h.Id != house.Id && !World.Outsider(h.Id)))
                {
                    int was = rank.GetValueOrDefault(h.Id), now = Title.RankOf(h.Title); rank[h.Id] = now;
                    // a rival that has gone up a rung to Legate or beyond did it by a petition the chamber carried that year
                    if (now == was + 1 && now >= ProgressionSystem.LandedFromRank && now < Title.RankOf(Title.Emperor))
                        Assert.True(seen.Any(m => m.TargetId == h.Id && m.Passed && m.ResolvedTurn == w.Turn), $"{h.Name} rose to {h.Title} in year {w.Turn} with no petition carried");
                }
            }
            Assert.True(voted); Assert.NotEmpty(seen);
            // no bench has made a party line of it: each house is judged for itself
            Assert.All(w.Factions, f => Assert.False(f.Platform.ContainsKey(ProgressionSystem.PetitionKind)));
            Assert.All(seen.Where(m => m.Resolved), m => Assert.True(m.Votes.ContainsKey(house.Id)));
            // and somebody was raised by the chamber in thirty years
            if (seen.Any(m => m.Resolved && m.Passed)) return;
            lost.Add($"seed {seed}: " + string.Join("; ", seen.Select(m => $"{m.ProposerHouseId} {m.TitleId} in year {m.ProposedTurn}: {m.ForWeight} to {m.AgainstWeight}")));
        }
        Assert.Fail("no rival's petition carried in thirty years, in three games: " + string.Join(" | ", lost));
    }

    [Fact]
    public void In_a_game_the_players_petition_is_voted_at_the_end_of_the_next_year()
    {
        var (tc, w, house, me, ctx) = Start(); var prog = tc.Progression!;
        tc.BeginTurn();
        house.Title = Title.Baron; house.Legitimacy = 55; ctx.PoolsOf(me.Id).Influence = 6; foreach (var open in w.Motions.Where(x => x.Open).ToList()) w.Motions.Remove(open);
        var cmd = new PetitionTitleCommand(me.Id, prog); Assert.Null(cmd.Refusal(ctx)); Assert.True(tc.Executor.TryExecute(cmd, ctx));
        var m = ProgressionSystem.PetitionOf(w, house.Id)!; Assert.Equal(Title.Count, m.TitleId); int asked = w.Turn;
        tc.EndTurn(); Assert.True(m.Open); Assert.Equal(Title.Baron, house.Title);
        // every house is brought round before the vote: it is carried, and the house is a Legate the year after it asked
        for (int y = 0; y < 3 && m.Open; y++)
        {
            tc.BeginTurn(); foreach (var h in w.Houses.Values) m.Lobby[h.Id] = 12; tc.EndTurn();
        }
        Assert.True(m.Resolved); Assert.InRange(m.ResolvedTurn, asked + 1, asked + 2);   // the Speaker may hold a motion over a year
        if (!m.Vetoed) { Assert.True(m.Passed); Assert.Equal(Title.Count, house.Title); Assert.Contains(w.History, e => e.Text.Contains($"{house.Name} is GRANTED the rank of Legate by vote of the Senate")); }
    }

    // ---------------------------------------------------------------- what legitimacy is made of

    [Fact]
    public void What_moves_legitimacy_is_entered_by_cause_with_a_line_and_adds_up()
    {
        var (w, _, _, _, vega) = Chamber(); var rec = vega.LegitimacyRecord;
        Assert.True(rec.Opened); Assert.Equal(45, rec.Began); Assert.Equal(5, rec.BeganTurn); Assert.Equal(45, rec.Reckoned);
        Assert.Equal(6, LegitimacyBook.Move(w, vega, 6, LegitimacyBook.Senate, "Recognition, carried in the Senate"));
        Assert.Equal(-3, LegitimacyBook.Move(w, vega, -3, LegitimacyBook.Scandal, "A bought vote came out on the floor"));
        Assert.Equal(48, vega.Legitimacy); Assert.Equal(6, rec.Totals[LegitimacyBook.Senate]); Assert.Equal(-3, rec.Totals[LegitimacyBook.Scandal]); Assert.Equal(48, Sum(vega));
        Assert.Equal((5, LegitimacyBook.Scandal, -3, "A bought vote came out on the floor"), (rec.Changes[^1].Turn, rec.Changes[^1].Cause, rec.Changes[^1].By, rec.Changes[^1].Line));
        // a write that says nothing of its cause is entered under "other" when the year closes, so that the record adds up
        vega.Legitimacy += 4; Assert.Equal(4, LegitimacyBook.Unentered(vega)); Assert.NotEqual(vega.Legitimacy, Sum(vega));
        LegitimacyBook.CloseYear(w); Assert.Equal(4, rec.Totals[LegitimacyBook.Other]); Assert.Equal(vega.Legitimacy, Sum(vega)); Assert.Equal(0, LegitimacyBook.Unentered(vega));
        // or at the next move, before it
        w.Turn++; vega.Legitimacy -= 2; LegitimacyBook.Move(w, vega, 1, LegitimacyBook.Works, "A work finished: The Orrery");
        Assert.Equal(2, rec.Totals[LegitimacyBook.Other]); Assert.Equal(1, rec.Totals[LegitimacyBook.Works]); Assert.Equal(vega.Legitimacy, Sum(vega));
        // a move is entered at what it moved: legitimacy stops at 100 and at nought
        vega.Legitimacy = 97; LegitimacyBook.Settle(w, vega); Assert.Equal(3, LegitimacyBook.Move(w, vega, 10, LegitimacyBook.War, "The great war won")); Assert.Equal(3, rec.Totals[LegitimacyBook.War]); Assert.Equal(100, Sum(vega));
        Assert.Equal(0, LegitimacyBook.Move(w, vega, 5, LegitimacyBook.War, "The great war won")); Assert.Equal(3, rec.Totals[LegitimacyBook.War]);
        Assert.Equal(-100, LegitimacyBook.Move(w, vega, -250, LegitimacyBook.War, "Declared an enemy of the Reach")); Assert.Equal(0, vega.Legitimacy); Assert.Equal(0, Sum(vega));
        // the same thing twice in a year is one line
        int lines = rec.Changes.Count; LegitimacyBook.Move(w, vega, 2, LegitimacyBook.Family, "A marriage between houses of the same rank"); LegitimacyBook.Move(w, vega, 2, LegitimacyBook.Family, "A marriage between houses of the same rank");
        Assert.Equal(lines + 1, rec.Changes.Count); Assert.Equal(4, rec.Changes[^1].By);
        // the last dozen changes are kept, the totals for the whole game, and the year by year for ten years
        for (int i = 0; i < 20; i++) { w.Turn++; LegitimacyBook.Move(w, vega, 1, LegitimacyBook.Offices, "The year's business in office"); LegitimacyBook.CloseYear(w); }
        Assert.Equal(LegitimacyBook.ChangesKept, rec.Changes.Count); Assert.All(rec.Changes, c => Assert.Equal(LegitimacyBook.Offices, c.Cause)); Assert.Equal(w.Turn, rec.Changes[^1].Turn);
        Assert.Equal(20, rec.Totals[LegitimacyBook.Offices]); Assert.Equal(LegitimacyBook.YearsKept, rec.Years.Count);
        var lately = LegitimacyBook.Lately(w, vega); Assert.Equal(10, lately[LegitimacyBook.Offices]); Assert.False(lately.ContainsKey(LegitimacyBook.Senate)); Assert.Equal(6, rec.Totals[LegitimacyBook.Senate]);
        Assert.Equal(vega.Legitimacy, Sum(vega));
        // every kind of cause has a plain name, and a house with no record yet is opened where it stands
        Assert.Equal(13, LegitimacyBook.Causes.Length); Assert.Equal(13, LegitimacyBook.Causes.Select(LegitimacyBook.CauseName).Distinct().Count()); Assert.All(LegitimacyBook.Causes, c => Assert.False(LegitimacyBook.CauseName(c).Contains('_')));
        var fresh = new House { Id = "fresh", Name = "House Fresh", Legitimacy = 22 }; w.Houses["fresh"] = fresh; Assert.False(fresh.LegitimacyRecord.Opened); Assert.Equal(0, LegitimacyBook.Unentered(fresh));
        LegitimacyBook.Move(w, fresh, 3, LegitimacyBook.Land, "A world granted to the house"); Assert.Equal(22, fresh.LegitimacyRecord.Began); Assert.Equal(w.Turn, fresh.LegitimacyRecord.BeganTurn); Assert.Equal(25, Sum(fresh));
        Assert.Equal(0, LegitimacyBook.Move(w, null, 3, LegitimacyBook.Land));
    }

    [Fact]
    public void The_rules_that_move_legitimacy_say_what_they_were()
    {
        var (w, _, senate, ctx, vega) = Chamber(); var corwin = w.House("corwin")!; var ex = new CommandExecutor();
        // a censure carried in the Senate
        Assert.True(ex.TryExecute(new ProposeMotionCommand("self", "censure", "corwin", senate), ctx)); w.Turn++; Regard(w, "aurelian_head", "corwin_head", -40); senate.Tick(w);
        Assert.Equal(-6, corwin.LegitimacyRecord.Totals[LegitimacyBook.Senate]); Assert.Equal("Censure, carried in the Senate", corwin.LegitimacyRecord.Changes[^1].Line); Assert.Equal(corwin.Legitimacy, Sum(corwin));
        // a report: entered under the report's own title
        var text = Content.Texts.Values.First(t => t.Title is { Length: > 0 } title && !title.Contains('{')); string id = Content.Texts.First(kv => ReferenceEquals(kv.Value, text)).Key;
        new AdjustLegitimacyEffect("self", 3).Apply(new EvalContext(w, new Binding("self"), TestContent.Rng(), Content) { EventId = id });
        Assert.Equal(3, vega.LegitimacyRecord.Totals[LegitimacyBook.Events]); Assert.Equal("A report: " + text.Title, vega.LegitimacyRecord.Changes[^1].Line);
        new AdjustLegitimacyEffect("self", -1).Apply(new EvalContext(w, new Binding("self"), TestContent.Rng()));
        Assert.Equal("A report answered", vega.LegitimacyRecord.Changes[^1].Line); Assert.Equal(2, vega.LegitimacyRecord.Totals[LegitimacyBook.Events]);
        // an honour, or a stage of the great war, says what it is and is not counted as a report
        new AdjustLegitimacyEffect("self", 4).Apply(new EvalContext(w, new Binding("self"), TestContent.Rng()) { Standing = (LegitimacyBook.Titles, "An honour conferred: Protector of the Reach") });
        Assert.Equal(4, vega.LegitimacyRecord.Totals[LegitimacyBook.Titles]); Assert.Equal("An honour conferred: Protector of the Reach", vega.LegitimacyRecord.Changes[^1].Line);
        // a marriage, a prosecution, an office: each under its own head
        var a = w.Char("self")!; var b = w.Char("corwin_head")!; a.Sex = "male"; b.Sex = "female"; vega.Title = Title.Baron; corwin.Title = Title.Baron;
        CourtshipSystem.Wed(w, a, b, CourtshipSystem.Arranged);
        Assert.Equal(2, vega.LegitimacyRecord.Totals[LegitimacyBook.Family]); Assert.Equal("A marriage between houses of the same rank", vega.LegitimacyRecord.Changes[^1].Line);
        Assert.Equal(vega.Legitimacy, Sum(vega)); Assert.Equal(corwin.Legitimacy, Sum(corwin));
    }

    [Fact]
    public void In_a_game_every_houses_record_adds_up_every_year()
    {
        var (tc, w, house, _, _) = Start();
        Assert.All(w.Houses.Values, h => { Assert.True(h.LegitimacyRecord.Opened); Assert.Equal(h.Legitimacy, h.LegitimacyRecord.Began); Assert.Equal(0, h.LegitimacyRecord.BeganTurn); });
        var began = w.Houses.Values.ToDictionary(h => h.Id, h => h.Legitimacy);
        for (int y = 0; y < 20 && !w.IsDynastyDead; y++)
        {
            Year(tc);
            foreach (var h in w.Houses.Values)
            {
                var rec = h.LegitimacyRecord; Assert.True(rec.Opened, h.Id);
                Assert.True(h.Legitimacy == Sum(h), $"{h.Name} in year {w.Turn}: {h.Legitimacy} against {rec.Began} and {string.Join(", ", rec.Totals.Select(kv => kv.Key + " " + kv.Value))}");
                Assert.Equal(h.Legitimacy, rec.Reckoned); Assert.True(rec.Changes.Count <= LegitimacyBook.ChangesKept); Assert.True(rec.Years.Count <= LegitimacyBook.YearsKept);
                Assert.All(rec.Years, yr => Assert.True(yr.Turn > w.Turn - LegitimacyBook.YearsKept)); Assert.All(rec.Changes, c => { Assert.NotEqual("", c.Line); Assert.Contains(c.Cause, LegitimacyBook.Causes); });
                if (began.TryGetValue(h.Id, out int b)) Assert.Equal(b, rec.Began);   // where a house began does not move
            }
        }
        // twenty years in, the player's house can read where its number came from, and so can any other
        var card = LegitimacyProjection.Of(w, house); Assert.Equal(house.Legitimacy, card.Began + card.Causes.Sum(c => c.Total)); Assert.NotEmpty(card.Causes); Assert.NotEmpty(card.Changes);
        Assert.All(card.Changes, c => Assert.DoesNotContain("(s)", c.Line)); Assert.True(card.Changes[0].Turn >= card.Changes[^1].Turn);   // the newest first
        Assert.All(w.Houses.Values, h => { var c = LegitimacyProjection.Of(w, h); Assert.Equal(h.Legitimacy, c.Began + c.Causes.Sum(x => x.Total)); });
        Assert.Contains(w.Houses.Values, h => h.Id != house.Id && h.LegitimacyRecord.Totals.Count > 0);
    }

    [Fact]
    public void The_record_and_a_petition_before_the_Senate_are_kept_in_a_save()
    {
        var (tc, w, house, me, ctx) = Start(); var prog = tc.Progression!;
        Year(tc); Year(tc); tc.BeginTurn(); me = w.Protagonist!;   // whoever holds the seat now: the old head may have died in the two years
        LegitimacyBook.Move(w, house, -4, LegitimacyBook.Scandal, "A bought vote came out on the floor"); LegitimacyBook.Move(w, house, 2, LegitimacyBook.Works, "A work finished: The Orrery");
        house.Title = Title.Baron; house.Legitimacy = Math.Max(house.Legitimacy, 50); ctx.PoolsOf(me.Id).Influence = 6; foreach (var open in w.Motions.Where(x => x.Open).ToList()) w.Motions.Remove(open);
        Assert.True(tc.Executor.TryExecute(new PetitionTitleCommand(me.Id, prog), ctx), new PetitionTitleCommand(me.Id, prog).Refusal(ctx));
        var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(w)); var h2 = loaded.House(house.Id)!; var r1 = house.LegitimacyRecord; var r2 = h2.LegitimacyRecord;
        Assert.Equal(house.Legitimacy, h2.Legitimacy); Assert.True(r2.Opened); Assert.Equal((r1.Began, r1.BeganTurn, r1.Reckoned), (r2.Began, r2.BeganTurn, r2.Reckoned));
        Assert.Equal(r1.Totals.OrderBy(kv => kv.Key), r2.Totals.OrderBy(kv => kv.Key));
        Assert.Equal(r1.Years.Select(y => (y.Turn, string.Join(",", y.ByCause.OrderBy(kv => kv.Key)))), r2.Years.Select(y => (y.Turn, string.Join(",", y.ByCause.OrderBy(kv => kv.Key)))));
        Assert.Equal(r1.Changes.Select(c => (c.Turn, c.Cause, c.By, c.Line)), r2.Changes.Select(c => (c.Turn, c.Cause, c.By, c.Line)));
        Assert.Contains(r2.Changes, c => c.Line == "A work finished: The Orrery" && c.Cause == LegitimacyBook.Works && c.By == 2);
        var m2 = ProgressionSystem.PetitionOf(loaded, house.Id)!; Assert.Equal(Title.Count, m2.TitleId); Assert.Equal(ProgressionSystem.PetitionKind, m2.Kind); Assert.Equal(1, m2.Votes[house.Id]);
        Assert.All(w.Houses.Values, h => Assert.Equal(Sum(h) + LegitimacyBook.Unentered(h), Sum(loaded.House(h.Id)!) + LegitimacyBook.Unentered(loaded.House(h.Id)!)));
        // the game taken up from the save goes on as the one that was never closed: the petition is voted, the record kept
        var tc2 = new TurnController(loaded, Content); Assert.Same(tc2.Senate, tc2.Progression!.Senate);
        Assert.Contains("before the Senate already", new PetitionTitleCommand(me.Id, tc2.Progression!).Refusal(new CommandContext(loaded, WorldBuilder.RngFor(loaded))));
        // a save from before the record was kept opens it where the house stands, and it adds up from there
        h2.LegitimacyRecord = new LegitimacyRecord(); Assert.False(h2.LegitimacyRecord.Opened); LegitimacyBook.Move(loaded, h2, 1, LegitimacyBook.Offices, "The year's business in office");
        Assert.True(h2.LegitimacyRecord.Opened); Assert.Equal(h2.Legitimacy, Sum(h2));
    }

    // ---------------------------------------------------------------- the screens

    [Fact]
    public void The_screen_says_for_the_next_rung_what_the_house_has_and_what_it_lacks()
    {
        var (tc, w, house, me, ctx) = Start(); var prog = tc.Progression!; tc.BeginTurn();
        house.Title = Title.Baron; house.Legitimacy = 50; ctx.PoolsOf(me.Id).Influence = 6; foreach (var open in w.Motions.Where(x => x.Open).ToList()) w.Motions.Remove(open);
        foreach (var h in w.HoldingsOf(house.Id).ToList()) h.OwnerId = "nobody"; w.HoldingsChanged(); house.SphereInfluence["senate"] = 20;   // no world at all, and a great many seats
        var s = CourtProjection.Build(tc, Content).Standing!; var footing = prog.FootingFor(w, house.Id, prog.NextTitle(w, house.Id)!);
        Assert.Equal(new[] { "standing", "land", "seats", "vote" }, s.Needs.Select(n => n.Id).ToArray());
        Assert.True(s.Needs[0].Met); Assert.Equal("Legitimacy 40", s.Needs[0].Asks); Assert.Equal("the house has 50", s.Needs[0].Has);
        Assert.False(s.Needs[1].Met); Assert.Equal("1 world", s.Needs[1].Asks); Assert.Equal("the house holds none", s.Needs[1].Has);
        Assert.True(s.Needs[2].Met); Assert.Equal("or 8 seats in the Senate", s.Needs[2].Asks); Assert.Equal($"the house has {footing.SeatsHeld}", s.Needs[2].Has); Assert.True(footing.SeatsHeld >= 20);
        var (yes, no) = tc.Senate.Forecast(w, house.Id, ProgressionSystem.PetitionKind, house.Id, titleId: Title.Count);
        Assert.Equal("A vote of the Senate", s.Needs[3].Asks); Assert.Equal($"as the houses lean today, {yes} for and {no} against", s.Needs[3].Has); Assert.Equal(yes > no, s.Needs[3].Met);
        Assert.Contains("must be earned: by land or by seats in the Senate, and either is enough", s.Earned); Assert.Contains("it lacks the land (0 of 1 world) but it has the seats", s.Earned); Assert.Contains("only if the chamber carries it", s.Earned);
        // the petition's button: its name, its cost, what is asked before it is done; and nothing about five of legitimacy
        var path = s.Paths.Single(p => p.Id == "petition"); Assert.True(path.Available); Assert.Equal("Petition the Senate", path.Label); Assert.Equal("2 influence of this year's", path.Cost);
        Assert.Contains("goes before the Senate", path.Confirm); Assert.Contains("rises only if it is carried", path.Confirm); Assert.Contains("ask again in 2 years", path.Confirm);
        Assert.All(s.Paths, p => { Assert.DoesNotContain("+5", p.Confirm); Assert.DoesNotContain("thought the better of", p.Confirm); });
        // asked, the vote line reads from the docket
        Assert.True(tc.Executor.TryExecute(new PetitionTitleCommand(me.Id, prog), ctx)); s = CourtProjection.Build(tc, Content).Standing!;
        Assert.StartsWith("your petition is before it", s.Needs.Single(n => n.Id == "vote").Has); path = s.Paths.Single(p => p.Id == "petition"); Assert.False(path.Available); Assert.Contains("before the Senate already", path.Refusal);
        // neither land nor seats: both are said to be lacking, and the button says so
        w.Motions.Clear(); house.SphereInfluence["senate"] = 0; foreach (var o in w.Offices.Where(o => o.HouseId == house.Id).ToList()) w.Offices.Remove(o);
        s = CourtProjection.Build(tc, Content).Standing!; footing = prog.FootingFor(w, house.Id, prog.NextTitle(w, house.Id)!);
        if (!footing.BySeats)
        {
            Assert.Contains("it lacks the land (0 of 1 world) and it lacks the seats", s.Earned); Assert.False(s.Needs.Single(n => n.Id == "seats").Met);
            Assert.StartsWith("a Legate holds a world or has 8 seats in the Senate", s.Paths.Single(p => p.Id == "petition").Refusal);
        }
        // below Legate nothing more than the standing is asked, and it is said
        house.Title = Title.Landless; s = CourtProjection.Build(tc, Content).Standing!;
        Assert.Equal(new[] { "standing" }, s.Needs.Select(n => n.Id).ToArray()); Assert.Equal("The rank of Warden is granted to a house of the standing for it. Nothing more is asked.", s.Earned);
        Assert.Contains("is granted the rank of Warden. Its legitimacy stays as it is", s.Paths.Single(p => p.Id == "petition").Confirm);
        // what the house's legitimacy is made of is on the card, adds up in the middle of a year, and reads as one line
        house.Legitimacy += 3; var made = CourtProjection.Build(tc, Content).Standing!.Made!;
        Assert.Equal(house.Legitimacy, made.Now); Assert.Equal(made.Now, made.Began + made.Causes.Sum(c => c.Total)); Assert.Contains(made.Causes, c => c.Id == LegitimacyBook.Other && c.Name == "Other things");
        Assert.StartsWith("began at " + made.Began, made.Line); Assert.Equal("+3", LegitimacyProjection.Signed(3)); Assert.Equal("−3", LegitimacyProjection.Signed(-3)); Assert.Equal("0", LegitimacyProjection.Signed(0));
        // at the top of the ladder there is nothing to ask for
        house.Title = Title.Archduke; s = CourtProjection.Build(tc, Content).Standing!; Assert.Empty(s.Needs); Assert.Null(s.NextTitle); Assert.StartsWith("The house stands at the top of the ladder", s.Earned);
    }
}
