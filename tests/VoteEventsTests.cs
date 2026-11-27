using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The Senate's votes and the elections (docs/events/votes.md): the marks <see cref="VoteMarks"/> puts on the people in the
/// news, where the Senate, the consular count and the ladder's count set and clear them; and, for every report in
/// content/events/votes.json, a world where its moment has happened, the report eligible, and every choice open and applied.</summary>
public class VoteEventsTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());

    private static (TurnController tc, World w, Character me, House mine, House theirs, Character head) Start(string scenario = "the_old_lion", int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content); tc.BeginTurn(); var me = w.Protagonist!; w.Turn = 4;   // not a year of any count: the consuls sit every tenth, the ladder every fifth
        var theirs = w.Houses.Values.Where(h => h.Id != me.HouseId && !World.Outsider(h.Id) && Economy.HeadOf(w, h) != null).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        return (tc, w, me, w.House(me.HouseId)!, theirs, Economy.HeadOf(w, theirs)!);
    }
    private static IEnumerable<House> Chamber(World w) => w.Houses.Values.Where(h => !World.Outsider(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal);
    private static Character Head(World w, string houseId) => Economy.HeadOf(w, w.House(houseId)!)!;
    private static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }

    /// <summary>What every gated choice asks of the player, given: coin, the year's influence, a tongue, a bench, a nature.</summary>
    private static void Fit(World w, Character me, string choiceId)
    {
        w.House(me.HouseId)!.Treasury.Credits = Math.Max(100, w.House(me.HouseId)!.Treasury.Credits); w.PoolsFor(me.Id).Influence = 9;
        me.Skills["oratory"] = 5; me.Skills["intrigue"] = 5; me.AptitudeTraits.Add("Orator"); me.AptitudeTraits.Add("Schemer");
        me.NatureTraits.Clear(); me.NatureTraits.Add(choiceId == "it_was_conscience" ? "Just" : "Proud");
        w.WorldFlags.Add("in_faction");
    }

    private static (EventDef def, Binding binding)? Find(World w, string eventId) =>
        new EventEngine(Content, WorldBuilder.RngFor(w)).GatherEligible(w).Where(e => e.def.Id == eventId).Select(e => ((EventDef, Binding)?)e).FirstOrDefault();

    /// <summary>For every choice of the report: a fresh world arranged for it, the report eligible, the choice open, and applied, with a
    /// line in the chronicle. Returns the worlds after each choice, by choice id, for what a thread reads next.</summary>
    private static Dictionary<string, (World w, Binding b)> EveryChoice(string eventId, Func<World> arrange)
    {
        var def = Content.Event(eventId); Assert.NotNull(def); var after = new Dictionary<string, (World, Binding)>();
        foreach (var choice in def!.Choices)
        {
            var w = arrange(); var me = w.Protagonist!; Fit(w, me, choice.Id);
            var engine = new EventEngine(Content, WorldBuilder.RngFor(w));
            var e = engine.GatherEligible(w).FirstOrDefault(x => x.def.Id == eventId);
            Assert.True(e.def != null, $"{eventId} is not eligible");
            var fired = new FiredEvent(e.def, e.binding);
            Assert.True(engine.IsChoiceAvailable(fired, w, choice.Id), $"{eventId}/{choice.Id} is not open");
            int lines = w.History.Count; engine.Resolve(fired, w, choice.Id);
            Assert.True(w.History.Count > lines, $"{eventId}/{choice.Id} wrote nothing in the chronicle");
            after[choice.Id] = (w, e.binding);
        }
        Assert.Contains(def.Choices, c => c.Requirements.Count == 0);
        return after;
    }
    private static Character Bound(World w, Binding b, string role) { Assert.True(b.TryGet(role, out var id)); return w.Char(id!)!; }

    // ------------------------------------------------------------------ the marks

    [Fact]
    public void TheDocket_MarksWhoMovesWhat_AndTheMarksGoWithTheMotion()
    {
        var (tc, w, me, mine, theirs, head) = Start();
        var m = tc.Senate.Add(w, theirs.Id, "levy", ""); tc.Senate.Tick(w);
        Assert.True(m.Open); Assert.True(w.HasFlag("motion_open")); Assert.Contains(VoteMarks.MovesChar, head.Flags);
        Assert.DoesNotContain(VoteMarks.MovesAgainstChar, head.Flags); Assert.DoesNotContain(VoteMarks.MovesChar, me.Flags);
        // a censure of the player's house: its mover is the one who moves against you
        var c = tc.Senate.Add(w, theirs.Id, "censure", mine.Id); tc.Senate.Tick(w);
        Assert.True(c.Open); Assert.True(w.HasFlag("motion_against_me")); Assert.Contains(VoteMarks.MovesAgainstChar, head.Flags);
        // once nothing of theirs is on the docket, the marks are gone
        foreach (var x in w.Motions.Where(x => x.ProposerHouseId == theirs.Id)) { x.Resolved = true; x.ResolvedTurn = w.Turn; }
        tc.Senate.Tick(w);
        if (!SenateSystem.Docket(w).Any(x => x.ProposerHouseId == theirs.Id)) { Assert.DoesNotContain(VoteMarks.MovesChar, head.Flags); Assert.DoesNotContain(VoteMarks.MovesAgainstChar, head.Flags); }
    }

    [Fact]
    public void ThePlayersOwnMotion_MarksTheHousesThatLeanNeitherWay()
    {
        var (tc, w, me, mine, theirs, head) = Start();
        var m = tc.Senate.Add(w, mine.Id, "levy", ""); tc.Senate.Tick(w);
        Assert.True(w.HasFlag(VoteMarks.MyMotionFlag));
        foreach (var h in Chamber(w).Where(h => h.Id != mine.Id))
            Assert.Equal(tc.Senate.Lean(w, m, h.Id) == 0, Head(w, h.Id).Flags.Contains(VoteMarks.UndecidedChar));
        // one house brought to lean neither way is marked; one that has declared is not
        m.Lobby[theirs.Id] = m.Lobby.GetValueOrDefault(theirs.Id) - tc.Senate.Lean(w, m, theirs.Id);
        Assert.Equal(0, tc.Senate.Lean(w, m, theirs.Id));
        VoteMarks.Docket(w, tc.Senate, mine.Id); Assert.Contains(VoteMarks.UndecidedChar, head.Flags);
        m.Votes[theirs.Id] = 1; VoteMarks.Docket(w, tc.Senate, mine.Id); Assert.DoesNotContain(VoteMarks.UndecidedChar, head.Flags);
        m.Resolved = true; VoteMarks.Docket(w, tc.Senate, mine.Id); Assert.False(w.HasFlag(VoteMarks.MyMotionFlag));
    }

    [Fact]
    public void TheCountOfThePlayersMotion_MarksWhoVotedEachWay_ForASession()
    {
        var (tc, w, me, mine, theirs, head) = Start();
        var m = tc.Senate.Add(w, mine.Id, "levy", "");
        foreach (var h in Chamber(w)) m.Votes[h.Id] = h.Id == theirs.Id ? -1 : 1;
        w.Turn++; tc.Senate.Tick(w);
        Assert.True(m.Passed); Assert.True(w.HasFlag("motion_passed_mine"));
        Assert.Contains(VoteMarks.VotedAgainstChar, head.Flags); Assert.DoesNotContain(VoteMarks.VotedWithChar, head.Flags);
        var ally = Chamber(w).First(h => h.Id != mine.Id && h.Id != theirs.Id && m.Votes[h.Id] > 0); Assert.Contains(VoteMarks.VotedWithChar, Head(w, ally.Id).Flags);
        Assert.DoesNotContain(VoteMarks.VotedWithChar, me.Flags);
        w.Turn++; tc.Senate.Tick(w);
        Assert.DoesNotContain(VoteMarks.VotedAgainstChar, head.Flags); Assert.DoesNotContain(VoteMarks.VotedWithChar, Head(w, ally.Id).Flags);
    }

    [Fact]
    public void AnAbstention_IsSeenByTheBenchesLeader_WhoVoted()
    {
        var (tc, w, me, mine, _, _) = Start();
        var bench = w.Factions.Where(f => f.LeaderHouseId != mine.Id && w.House(f.LeaderHouseId) != null).OrderBy(f => f.Id, StringComparer.Ordinal).First();
        foreach (var f in w.Factions) f.Members.Remove(mine.Id); bench.Members.Add(mine.Id);
        var mover = Chamber(w).First(h => h.Id != mine.Id && h.Id != bench.LeaderHouseId); var leader = Head(w, bench.LeaderHouseId);
        var m = tc.Senate.Add(w, mover.Id, "levy", ""); m.Votes[bench.LeaderHouseId] = 1; m.Votes[mine.Id] = 0;
        w.Turn++; tc.Senate.Tick(w);
        Assert.True(m.Resolved); Assert.True(w.HasFlag(VoteMarks.AbstainedFlag)); Assert.Equal(w.Turn, w.Counter("mark:" + VoteMarks.AbstainedFlag));
        Assert.Contains(VoteMarks.NoticedChar, leader.Flags); Assert.False(w.HasFlag("broke_the_whip"));   // sitting still is not voting against
        // a vote cast is not an abstention
        var cast = new Motion { Id = "x", Kind = "levy", ProposerHouseId = mover.Id }; cast.Votes[bench.LeaderHouseId] = 1; cast.Votes[mine.Id] = 1;
        VoteMarks.ClearSession(w); VoteMarks.Counted(w, cast, mine.Id); Assert.False(w.HasFlag(VoteMarks.AbstainedFlag)); Assert.DoesNotContain(VoteMarks.NoticedChar, leader.Flags);
    }

    [Fact]
    public void ABoughtVoteThatComesOut_IsMarkedForASession_AndNoLonger()
    {
        var (tc, w, me, mine, _, _) = Start();
        Motion? m = null; House? bought = null;
        foreach (var h in Chamber(w).Where(h => h.Id != mine.Id))
        {
            var hd = Head(w, h.Id); hd.NatureTraits.Clear(); hd.Ambition = "";
            m = tc.Senate.Add(w, mine.Id, "levy", "");
            if (SenateSystem.BribeComesOut(w, m, h.Id)) { bought = h; break; }
            w.Motions.Remove(m); m = null;
        }
        Assert.NotNull(bought);
        m!.Bought[bought!.Id] = 2; m.BoughtBy[bought.Id] = mine.Id;
        w.Turn++; tc.Senate.Tick(w);
        Assert.True(m.Resolved); Assert.True(w.HasFlag(VoteMarks.PurseOutFlag)); Assert.Equal(w.Turn, w.Counter("mark:" + VoteMarks.PurseOutFlag));
        Assert.Contains(VoteMarks.PurseOutChar, Head(w, bought.Id).Flags);
        w.Turn++; tc.Senate.Tick(w);   // it used to stay up for good
        Assert.False(w.HasFlag(VoteMarks.PurseOutFlag)); Assert.DoesNotContain(VoteMarks.PurseOutChar, Head(w, bought.Id).Flags);
    }

    [Fact]
    public void AnActThatCarries_MarksTheHousesThatVotedAgainstIt()
    {
        var (tc, w, me, mine, theirs, head) = Start(); var m = CarryAnAct(tc, w, mine, theirs);
        Assert.True(m.Passed); Assert.True(w.HasFlag("act_passed")); Assert.True(w.HasFlag(VoteMarks.ActAgainstFlag));
        Assert.Contains(VoteMarks.ActAgainstChar, head.Flags); Assert.DoesNotContain(VoteMarks.ActAgainstChar, me.Flags);
        Assert.All(Chamber(w).Where(h => h.Id != theirs.Id && h.Id != mine.Id), h => Assert.DoesNotContain(VoteMarks.ActAgainstChar, Head(w, h.Id).Flags));
        w.Turn++; tc.Senate.Tick(w); Assert.False(w.HasFlag(VoteMarks.ActAgainstFlag)); Assert.DoesNotContain(VoteMarks.ActAgainstChar, head.Flags);
    }
    private static Motion CarryAnAct(TurnController tc, World w, House mine, House against)
    {
        var law = Content.Policies.Laws.Where(l => l.Scope == "senate" && l.Options.Count > 1).OrderBy(l => l.Id, StringComparer.Ordinal).First();
        var cur = PolicySystem.ReachOption(w, Content.Policies, law) ?? law.DefaultOption; var opt = law.Options.First(o => o.Id != cur.Id);
        var m = tc.Senate.Add(w, mine.Id, "act", "", lawId: law.Id, lawOption: opt.Id);
        foreach (var h in Chamber(w)) m.Votes[h.Id] = h.Id == against.Id ? -1 : 1;
        w.Turn++; tc.Senate.Tick(w); return m;
    }

    [Fact]
    public void TheConsularCount_IsMarkedSoItsReportsComeInItsYear()
    {
        var (tc, w, _, _, _, _) = Start(); w.Turn = 10;
        ElectionSystem.Hold(w, Content.Offices, id => tc.Senate.Weight(w, id));
        Assert.True(w.HasFlag("election_held")); Assert.Equal(10, w.Counter("mark:election_held"));
        Assert.Contains(w.Characters.Values, c => c.Flags.Contains("office:consul_senior"));
    }

    [Fact]
    public void TheLaddersCount_MarksWhoBeatYoursAndWhoYoursBeat()
    {
        var (tc, w, me, mine, theirs, head) = Start(); var rival = Head(w, Chamber(w).First(h => h.Id != mine.Id && h.Id != theirs.Id).Id);
        var kin = new Character { Id = "kin_votes", Name = "Test Kin", HouseId = mine.Id, Age = 30, Sex = "female", CareerTrack = "law", CareerRank = 1, Health = 90, Alive = true };
        // one of the house lost: the winner of another house is marked; a loser of another house is not
        VoteMarks.LadderCounted(w, new[] { kin, head, rival }, new[] { head }, mine.Id);
        Assert.Contains(VoteMarks.BeatYoursChar, head.Flags); Assert.DoesNotContain(VoteMarks.LostToYoursChar, rival.Flags); Assert.DoesNotContain(VoteMarks.BeatYoursChar, kin.Flags);
        Assert.Contains(VoteMarks.OursLostChar, kin.Flags); kin.Flags.Remove(VoteMarks.OursLostChar);   // and the one of yours who lost, for the report of who beat them
        head.Flags.Remove(VoteMarks.BeatYoursChar);
        // one of the house won: those of other houses who lost are marked
        VoteMarks.LadderCounted(w, new[] { kin, head, rival }, new[] { kin, rival }, mine.Id);
        Assert.Contains(VoteMarks.LostToYoursChar, head.Flags); Assert.DoesNotContain(VoteMarks.LostToYoursChar, rival.Flags); Assert.DoesNotContain(VoteMarks.BeatYoursChar, head.Flags);
        Assert.DoesNotContain(VoteMarks.OursLostChar, kin.Flags);
        foreach (var c in w.Characters.Values) { c.Flags.Remove(VoteMarks.BeatYoursChar); c.Flags.Remove(VoteMarks.LostToYoursChar); }

        // the count itself: one of the house stands for quaestor, against a young hopeful of every other house, so that someone loses
        w.Characters[kin.Id] = kin; mine.Members.Add(kin.Id); w.Relationship(me.Id, kin.Id).Bond = BondType.Blood;
        foreach (var h in Chamber(w).Where(h => h.Id != mine.Id))
        { var hopeful = new Character { Id = "hopeful_" + h.Id, Name = "Hopeful " + h.Id, HouseId = h.Id, Age = 26, Sex = "male", CareerTrack = "law", CareerRank = 1, Health = 90, Alive = true }; w.Characters[hopeful.Id] = hopeful; h.Members.Add(hopeful.Id); }
        w.Candidacies.Add(new Candidacy { RungId = LadderSystem.Quaestor, CharacterId = kin.Id }); w.Turn = 5;
        LadderSystem.Hold(w, Content.Offices, id => tc.Senate.Weight(w, id));
        var count = w.Counts.Where(c => c.RungId == LadderSystem.Quaestor).ToList(); var ours = count.Single(c => c.CharacterId == kin.Id);
        foreach (var c in count.Where(c => c.HouseId != mine.Id))
        {
            var who = w.Char(c.CharacterId)!;
            Assert.Equal(!ours.Won && c.Won, who.Flags.Contains(VoteMarks.BeatYoursChar));
            Assert.Equal(ours.Won && !c.Won, who.Flags.Contains(VoteMarks.LostToYoursChar));
        }
        var marked = count.Where(c => c.HouseId != mine.Id).Select(c => w.Char(c.CharacterId)!).Where(c => c.Flags.Contains(VoteMarks.BeatYoursChar) || c.Flags.Contains(VoteMarks.LostToYoursChar)).ToList();
        Assert.NotEmpty(marked);
        // they are news while the count is: kept through the year's turn, gone with the ladder's flag
        LadderSystem.Tick(w, Content.Offices, Content.Policies); Assert.All(marked, c => Assert.True(c.Flags.Contains(VoteMarks.BeatYoursChar) || c.Flags.Contains(VoteMarks.LostToYoursChar)));
        w.Turn += 2; LadderSystem.Tick(w, Content.Offices, Content.Policies);
        Assert.False(w.HasFlag(LadderSystem.HeldFlag)); Assert.All(marked, c => Assert.False(c.Flags.Contains(VoteMarks.BeatYoursChar) || c.Flags.Contains(VoteMarks.LostToYoursChar)));
    }

    // ------------------------------------------------------------------ the reports, and the threads

    private static World VoteForAVote(Action<World, Character>? also = null)
    {
        var (tc, w, me, _, theirs, head) = Start(); tc.Senate.Add(w, theirs.Id, "levy", ""); tc.Senate.Tick(w);
        w.Relationship(head.Id, me.Id).Disposition = 0; theirs.Treasury.Credits = Math.Max(50, theirs.Treasury.Credits); also?.Invoke(w, head); return w;
    }

    [Fact]
    public void AVoteForAVote_AndTheBoughtVoteCalledIn()
    {
        var after = EveryChoice("a_vote_for_a_vote", () => VoteForAVote());
        { var (w, b) = after["give_your_word"]; var senator = Bound(w, b, "senator"); Assert.Contains("owes_you_a_vote", senator.Flags);
          var again = Find(w, "a_vote_for_a_vote"); Assert.False(again is { } a && a.binding.TryGet("senator", out var id) && id == senator.Id);   // one bargain at a time with the same house
          // the bargain is real: at the next session it is a favour owed each way, before the count
          string mine = w.Protagonist!.HouseId; var tc = new TurnController(w, Content); w.Turn++; tc.Senate.Tick(w);
          Assert.DoesNotContain("owes_you_a_vote", senator.Flags);
          Assert.False(WealthSystem.FavourOwed(w, mine, senator.HouseId));     // spent on their motion, counted this session: the player's vote was judged
          Assert.True(WealthSystem.FavourOwed(w, senator.HouseId, mine));      // and theirs is owed to the player's next motion
          var m = tc.Senate.Add(w, mine, "levy", "");
          Assert.Contains(tc.Senate.Why(w, m, senator.HouseId), x => x.why.Contains("owes") && x.by == WealthSystem.FavourLeanBy);
          foreach (var h in Chamber(w)) m.Votes[h.Id] = 1; w.Turn++; tc.Senate.Tick(w);
          Assert.False(WealthSystem.FavourOwed(w, senator.HouseId, mine)); }   // spent at the count of the player's motion
        { var (w, b) = after["name_a_price"]; var senator = Bound(w, b, "senator"); Assert.Contains("bought_your_vote", senator.Flags);
          // the thread: the motion it was sold for is voted at the year's end; when the buyer next moves one, it is called in
          var tc = new TurnController(w, Content); w.Turn++; tc.Senate.Tick(w);
          if (!SenateSystem.Docket(w).Any(m => m.ProposerHouseId == senator.HouseId)) { Assert.DoesNotContain(VoteMarks.MovesChar, senator.Flags); Assert.Null(Find(w, "a_bought_vote_called_in")); }
          tc.Senate.Add(w, senator.HouseId, "censure", Chamber(w).First(h => h.Id != senator.HouseId && h.Id != w.Protagonist!.HouseId).Id); tc.Senate.Tick(w);
          var called = Find(w, "a_bought_vote_called_in"); Assert.NotNull(called); Assert.True(called!.Value.binding.TryGet("buyer", out var buyer)); Assert.Equal(senator.Id, buyer); }
        Assert.DoesNotContain("bought_your_vote", Bound(after["keep_your_vote"].w, after["keep_your_vote"].b, "senator").Flags);

        var settled = EveryChoice("a_bought_vote_called_in", () => VoteForAVote((w, head) => head.Flags.Add("bought_your_vote")));
        Assert.All(settled.Values, x => Assert.DoesNotContain("bought_your_vote", Bound(x.w, x.b, "buyer").Flags));
        { var (w, b) = settled["vote_as_they_ask"]; var buyer = Bound(w, b, "buyer"); Assert.Contains(VoteMarks.VotePromisedChar, buyer.Flags);
          VoteMarks.Bargains(w, w.Protagonist!.HouseId);                        // the promise is one the count of their motion will judge
          Assert.True(WealthSystem.FavourOwed(w, w.Protagonist!.HouseId, buyer.HouseId)); Assert.False(WealthSystem.FavourOwed(w, buyer.HouseId, w.Protagonist!.HouseId)); }
        Assert.Contains("grudge_against_you", Bound(settled["show_them_the_door"].w, settled["show_them_the_door"].b, "buyer").Flags);   // spurned in front of the chamber, they remember it
        Assert.DoesNotContain("grudge_against_you", Bound(settled["give_back_the_coin"].w, settled["give_back_the_coin"].b, "buyer").Flags);
    }

    [Fact]
    public void TheVoteStillUndecided()
    {
        EveryChoice("the_vote_still_undecided", () =>
        {
            var (tc, w, _, mine, theirs, head) = Start(); var m = tc.Senate.Add(w, mine.Id, "levy", ""); tc.Senate.Tick(w);
            m.Lobby[theirs.Id] = m.Lobby.GetValueOrDefault(theirs.Id) - tc.Senate.Lean(w, m, theirs.Id); VoteMarks.Docket(w, tc.Senate, mine.Id);
            Assert.Contains(VoteMarks.UndecidedChar, head.Flags); return w;
        });
    }

    [Fact]
    public void TheEmptySeatOnTheBench()
    {
        EveryChoice("the_empty_seat_on_the_bench", () =>
        {
            var (_, w, _, _, _, head) = Start(); Mark(w, VoteMarks.AbstainedFlag); head.Flags.Add(VoteMarks.NoticedChar); return w;
        });
    }

    private static World MotionCarried(Action<World, Character, Character>? also = null)
    {
        var (tc, w, me, mine, theirs, head) = Start(); var m = tc.Senate.Add(w, mine.Id, "levy", "");
        foreach (var h in Chamber(w)) m.Votes[h.Id] = h.Id == theirs.Id ? -1 : 1;
        w.Turn++; tc.Senate.Tick(w); Assert.True(m.Passed);
        foreach (var c in w.Characters.Values.Where(c => c.Flags.Contains(VoteMarks.VotedWithChar))) w.Relationship(c.Id, me.Id).Disposition = 10;
        w.Relationship(head.Id, me.Id).Disposition = 0; also?.Invoke(w, me, head); return w;
    }

    [Fact]
    public void AFeastForTheAyes() => EveryChoice("a_feast_for_the_ayes", () => MotionCarried());

    [Fact]
    public void ThoseWhoVotedAgainstYou_AndTheGrudgeThatMovesAMotion()
    {
        var after = EveryChoice("those_who_voted_against_you", () => MotionCarried());
        Assert.DoesNotContain("grudge_against_you", Bound(after["offer_them_something"].w, after["offer_them_something"].b, "loser").Flags);
        var (w, b) = after["let_them_lose"]; var loser = Bound(w, b, "loser"); Assert.Contains("grudge_against_you", loser.Flags);
        Assert.Contains("grudge_against_you", Bound(after["remind_the_chamber"].w, after["remind_the_chamber"].b, "loser").Flags);
        // the thread: years on, it is they who move a censure of your house
        Assert.Null(Find(w, "an_old_grudge_on_the_docket"));
        var tc = new TurnController(w, Content); w.Turn++; tc.Senate.Add(w, loser.HouseId, "censure", w.Protagonist!.HouseId); tc.Senate.Tick(w);
        var grudge = Find(w, "an_old_grudge_on_the_docket"); Assert.NotNull(grudge); Assert.True(grudge!.Value.binding.TryGet("rival", out var rival)); Assert.Equal(loser.Id, rival);

        var settled = EveryChoice("an_old_grudge_on_the_docket", () =>
        {
            var (tc2, w2, _, mine, theirs, head) = Start(); head.Flags.Add("grudge_against_you"); tc2.Senate.Add(w2, theirs.Id, "censure", mine.Id); tc2.Senate.Tick(w2); return w2;
        });
        Assert.DoesNotContain("grudge_against_you", Bound(settled["make_your_peace"].w, settled["make_your_peace"].b, "rival").Flags);
        Assert.Contains("grudge_against_you", Bound(settled["let_them_have_their_day"].w, settled["let_them_have_their_day"].b, "rival").Flags);
    }

    [Fact]
    public void AnOldGrudge_SpeaksAgainstYourOwnMotion_AndMostAnswersEndIt()
    {
        World Grudge() { var (tc, w, me, mine, _, head) = Start(); head.Flags.Add("grudge_against_you"); w.Relationship(head.Id, me.Id).Disposition = -5; tc.Senate.Add(w, mine.Id, "levy", ""); tc.Senate.Tick(w); return w; }
        var after = EveryChoice("an_old_grudge_against_your_motion", Grudge);
        Assert.DoesNotContain("grudge_against_you", Bound(after["buy_them_off"].w, after["buy_them_off"].b, "rival").Flags);
        Assert.DoesNotContain("grudge_against_you", Bound(after["answer_them_on_the_floor"].w, after["answer_them_on_the_floor"].b, "rival").Flags);
        Assert.Contains("grudge_against_you", Bound(after["let_it_cost_you"].w, after["let_it_cost_you"].b, "rival").Flags);
        var (_, none, _, _, _, h) = Start(); h.Flags.Add("grudge_against_you"); Assert.Null(Find(none, "an_old_grudge_against_your_motion"));   // no motion of yours, no report
    }

    [Fact]
    public void AConsulWhoDinedWithYou_LetsAMotionOfYoursStand_OnceInTheTerm()
    {
        bool Vetoed(bool dined)
        {
            var (tc, w, me, mine, _, _) = Start(); w.Turn = 10; ElectionSystem.Hold(w, Content.Offices, id => tc.Senate.Weight(w, id));
            var consul = Content.Offices.Defs.Where(d => d.Veto && OfficeSystem.Holding(w, d.Id) is { } hd && hd.HouseId != mine.Id).Select(d => OfficeSystem.Holding(w, d.Id)!.HouseId).FirstOrDefault();
            Assert.NotNull(consul); var guest = Head(w, consul!); if (dined) guest.Flags.Add(ElectionSystem.DinedChar);
            var m = tc.Senate.Add(w, mine.Id, "censure", consul!); w.Turn++;
            ElectionSystem.RivalVetoes(w, Content.Offices, tc.Senate);
            if (dined) Assert.DoesNotContain(ElectionSystem.DinedChar, guest.Flags);   // spent
            return m.Vetoed;
        }
        Assert.True(Vetoed(dined: false)); Assert.False(Vetoed(dined: true));
        var feast = EveryChoice("the_consul_elects_feast", () => { var (_, w, _, _, _, head) = Start(); Mark(w, "election_held"); head.Flags.Add("office:consul_senior"); return w; });
        Assert.Contains(ElectionSystem.DinedChar, Bound(feast["go_and_be_seen"].w, feast["go_and_be_seen"].b, "grandee").Flags);
        Assert.DoesNotContain(ElectionSystem.DinedChar, Bound(feast["stay_away"].w, feast["stay_away"].b, "grandee").Flags);
    }

    [Fact]
    public void TheSenatesActFallsOnAFriend()
    {
        EveryChoice("the_senates_act_falls_on_a_friend", () =>
        {
            var (tc, w, me, mine, theirs, head) = Start(); CarryAnAct(tc, w, mine, theirs);
            w.Relationship(head.Id, me.Id).Disposition = 30; w.Relationship(me.Id, head.Id).Disposition = 30; return w;
        });
    }

    [Fact]
    public void APurseOnTheSenateFloor()
    {
        var after = EveryChoice("a_purse_on_the_senate_floor", () => { var (_, w, _, _, theirs, _) = Start(); VoteMarks.PurseOut(w, theirs.Id); return w; });
        Assert.Contains("grudge_against_you", Bound(after["say_they_named_the_price"].w, after["say_they_named_the_price"].b, "bought").Flags);   // the shame put on them is a grudge
        Assert.DoesNotContain("grudge_against_you", Bound(after["take_it_on_yourself"].w, after["take_it_on_yourself"].b, "bought").Flags);
    }

    [Fact]
    public void OneOfYoursStandsForOffice()
    {
        EveryChoice("one_of_yours_stands_for_office", () =>
        {
            var (_, w, me, mine, _, _) = Start();
            var kin = new Character { Id = "kin_stands", Name = "Test Kin", HouseId = mine.Id, Age = 30, Sex = "female", CareerTrack = "law", CareerRank = 1, Health = 90, Alive = true };
            w.Characters[kin.Id] = kin; mine.Members.Add(kin.Id); w.Relationship(me.Id, kin.Id).Bond = BondType.Blood; kin.Flags.Add(LadderSystem.StandingChar);
            return w;
        });
    }

    private static World AfterTheCount(Action<World, Character, Character> also)
    {
        var (_, w, me, _, _, head) = Start(); w.Turn = 5; Mark(w, LadderSystem.HeldFlag); w.Relationship(head.Id, me.Id).Disposition = 0; also(w, me, head); return w;
    }

    [Fact]
    public void ASmearAfterTheElection_AndThePamphletScandal()
    {
        var after = EveryChoice("a_smear_after_the_election", () => AfterTheCount((w, _, head) => head.Flags.Add(VoteMarks.LostToYoursChar)));
        var (w, b) = after["answer_in_kind"]; var accuser = Bound(w, b, "accuser"); Assert.Contains("pamphlet_war", accuser.Flags);
        Assert.DoesNotContain("pamphlet_war", Bound(after["let_it_blow_over"].w, after["let_it_blow_over"].b, "accuser").Flags);
        // the thread: the next year's briefing has the pamphlets in it
        var war = Find(w, "the_pamphlet_scandal"); Assert.NotNull(war); Assert.True(war!.Value.binding.TryGet("accuser", out var id)); Assert.Equal(accuser.Id, id);

        var ended = EveryChoice("the_pamphlet_scandal", () => AfterTheCount((w2, _, head) => head.Flags.Add("pamphlet_war")));
        Assert.All(ended.Values, x => Assert.DoesNotContain("pamphlet_war", Bound(x.w, x.b, "accuser").Flags));
    }

    [Fact]
    public void TheOneWhoWonTheOffice()
    {
        World Lost() => AfterTheCount((w, me, head) =>
        {
            head.Flags.Add(VoteMarks.BeatYoursChar);
            var kin = new Character { Id = "kin_lost", Name = "Test Kin", HouseId = me.HouseId, Age = 30, Sex = "female", CareerTrack = "law", CareerRank = 1, Health = 90, Alive = true };
            w.Characters[kin.Id] = kin; w.House(me.HouseId)!.Members.Add(kin.Id); w.Relationship(me.Id, kin.Id).Bond = BondType.Blood; kin.Flags.Add(VoteMarks.OursLostChar);
        });
        var after = EveryChoice("the_one_who_won_the_office", Lost);
        { var (w, b) = after["demand_a_recount"]; Assert.Equal("kin_lost", Bound(w, b, "kin").Id); Assert.True(w.GetRelationship("kin_lost", w.ProtagonistId)!.Disposition >= 8); }   // the recount is for them
        { var (w, _) = after["congratulate_them"]; Assert.True(w.GetRelationship("kin_lost", w.ProtagonistId)!.Disposition < 0); }
    }

    [Fact]
    public void ARiotOnTheDayOfTheCount()
    {
        EveryChoice("a_riot_on_the_day_of_the_count", () => AfterTheCount((w, _, _) => w.WorldCounters[LadderSystem.TrustKey] = 30));
        var calm = AfterTheCount((w, _, _) => w.WorldCounters[LadderSystem.TrustKey] = 60); Assert.Null(Find(calm, "a_riot_on_the_day_of_the_count"));   // a count the squares believe has no riot
    }

    [Fact]
    public void TheConsulElectsFeast()
    {
        EveryChoice("the_consul_elects_feast", () => { var (_, w, _, _, _, head) = Start(); Mark(w, "election_held"); head.Flags.Add("office:consul_senior"); return w; });
        var (_, won, _, _, _, h) = Start(); Mark(won, "election_held"); h.Flags.Add("office:consul_senior"); won.WorldFlags.Add("consul_elected");
        Assert.Null(Find(won, "the_consul_elects_feast"));   // the player who is elected has the_consulship instead
    }

    [Fact]
    public void YourColleagueTheConsul_OnceAColleague()
    {
        var after = EveryChoice("your_colleague_the_consul", () =>
        {
            var (_, w, me, _, _, head) = Start();
            foreach (var c in w.Characters.Values) { c.Flags.Remove("office:consul_senior"); c.Flags.Remove("office:consul_junior"); }
            me.Flags.Add("office:consul_senior"); head.Flags.Add("office:consul_junior"); return w;
        });
        Assert.All(after.Values, x => { Assert.Contains("met_as_colleague", Bound(x.w, x.b, "grandee").Flags); Assert.Null(Find(x.w, "your_colleague_the_consul")); });
    }

    [Fact]
    public void UnderAThrone_NoneOfTheseComes()
    {
        var (tc, w, me, mine, theirs, head) = Start("the_imperial_court");
        Mark(w, "election_held"); Mark(w, LadderSystem.HeldFlag); w.WorldFlags.Add("motion_open"); Mark(w, VoteMarks.PurseOutFlag); Mark(w, VoteMarks.AbstainedFlag);
        head.Flags.Add("office:consul_senior"); head.Flags.Add(VoteMarks.MovesChar); head.Flags.Add(VoteMarks.PurseOutChar); head.Flags.Add(VoteMarks.NoticedChar); head.Flags.Add("pamphlet_war"); head.Flags.Add(VoteMarks.BeatYoursChar);
        w.Relationship(head.Id, me.Id).Disposition = 0;
        var ids = Content.Events.Where(e => e.Id is "the_consul_elects_feast" or "the_pamphlet_scandal" or "the_one_who_won_the_office").Select(e => e.Id).ToList();
        Assert.Equal(3, ids.Count);
        var eligible = new EventEngine(Content, WorldBuilder.RngFor(w)).GatherEligible(w).Select(e => e.def.Id).ToHashSet();
        Assert.All(ids, id => Assert.DoesNotContain(id, eligible));
        Assert.All(Content.Events.Where(e => ids.Contains(e.Id)), e => Assert.Contains(e.Conditions, c => c is EraCondition { Era: "imperium", Present: false }));
        // the chamber still sits and still votes under the throne (docs/TITLES.md): the reports of its floor come in either era
        Assert.Contains("a_purse_on_the_senate_floor", eligible); Assert.Contains("the_empty_seat_on_the_bench", eligible);
        var floor = new[] { "a_vote_for_a_vote", "a_bought_vote_called_in", "the_vote_still_undecided", "the_empty_seat_on_the_bench", "a_feast_for_the_ayes", "those_who_voted_against_you",
            "an_old_grudge_on_the_docket", "an_old_grudge_against_your_motion", "the_senates_act_falls_on_a_friend", "a_purse_on_the_senate_floor" };
        Assert.All(floor, id => Assert.DoesNotContain(Content.Event(id)!.Conditions, c => c is EraCondition));
    }
}
