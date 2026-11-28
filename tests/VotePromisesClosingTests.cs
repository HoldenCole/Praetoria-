using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Group "closing" (docs/events/votes.md, "Promises", the closing round): what the checkers found after the five groups. Each
/// promise is made as a player makes it and played through the real session and count, in the order the game plays them where the order
/// matters (a motion moved in a year is counted at the end of the next, after the year's own session).</summary>
public partial class VotePromisesTests
{
    /// <summary>The end of the year now being played: the Senate's own session for it, with no year turned.</summary>
    private static void YearEnds(G g) => g.Senate.Tick(g.W);
    /// <summary>The next year opens, as the turn does, before anything is moved in it.</summary>
    private static void YearOpens(G g) => g.W.Turn++;
    private static bool Open(G g, FiredEvent f, string choice) => new EventEngine(Content, WorldBuilder.RngFor(g.W)).IsChoiceAvailable(f, g.W, choice);
    private static string Why(G g, FiredEvent f, string choice) => ChoiceStakes.Unmet(f.Def.Choice(choice)!, new EvalContext(g.W, f.Binding, WorldBuilder.RngFor(g.W), Content));

    // ------------------------------------------------------------------ across the aisle: the censure the session itself moves

    [Fact]
    public void AcrossTheAisle_ACensureABenchLeaderMovesAtTheSession_RaisesTheReportWhileItIsBeforeTheSenate_AndTheBenchIsSplit()
    {
        var g = Start();
        var bench = g.W.Factions.Where(f => !f.Members.Contains(g.Mine.Id) && f.Members.Count(id => g.W.House(id) != null && id != f.LeaderHouseId) >= 2 && g.W.House(f.LeaderHouseId) != null)
            .OrderBy(f => f.Id, StringComparer.Ordinal).First();
        var leader = g.W.House(bench.LeaderHouseId)!;
        // the leader's house is the weightiest in the chamber and has heard a great deal against yours; the chamber thinks ill of you
        leader.SphereInfluence["senate"] = 60; g.Mine.Legitimacy = Math.Max(30, g.Mine.Legitimacy);
        g.W.WorldCounters[$"stirred_until:{leader.Id}:{g.Mine.Id}"] = 99;
        foreach (var h in Others(g)) g.W.Relationship(g.HeadOf(h).Id, g.Me.Id).Disposition = -40;
        int turn = g.W.Turn + 1; while (Fnv("senate:" + turn) % 3 != 0) turn++;   // a session at which a house brings business
        g.W.Turn = turn; Assert.Empty(SenateSystem.Docket(g.W));
        YearEnds(g);   // no Senate.Add here: the session moves it
        var censure = SenateSystem.Docket(g.W).Single(m => m.Kind == "censure" && m.TargetId == g.Mine.Id);
        Assert.Equal(leader.Id, censure.ProposerHouseId);
        Assert.True(g.W.HasFlag(SenateSystem.AgainstMeFlag));   // written once the session's motions are moved
        var report = ReportOf(g, "across_the_aisle");
        var weakest = g.W.LivingCharacters().Single(c => c.Flags.Contains(VoteMarks.WeakestChar)); var weakHouse = g.W.House(weakest.HouseId)!;
        Assert.Contains(weakHouse.Id, bench.Members); Assert.True(g.Senate.Stance(g.W, censure, weakHouse.Id) > 0);
        g.Me.Skills["intrigue"] = 3; g.W.PoolsFor(g.Me.Id).Agents = Math.Max(1, g.W.PoolsFor(g.Me.Id).Agents);
        Choose(g, report, "split_the_bench"); Assert.Equal(-1, censure.Votes[weakHouse.Id]);
        Session(g);
        Assert.True(censure.Resolved); Assert.Equal(-1, censure.Votes[weakHouse.Id]); Assert.Equal(AgainstWeight(g, censure), censure.AgainstWeight);
    }
    private static uint Fnv(string s) { uint h = 2166136261; foreach (var ch in s) h = (h ^ ch) * 16777619; return h; }

    // ------------------------------------------------------------------ your colleague: once a term, the bench's leader and its line

    /// <summary>Your colleague's bench, led by a house that is not theirs.</summary>
    private static (Faction bench, House leader) ColleaguesBench(G g, Character colleague)
    {
        var bench = FactionSystem.Of(g.W, colleague.HouseId)!;
        if (bench.LeaderHouseId == colleague.HouseId || bench.LeaderHouseId == g.Mine.Id) bench.LeaderHouseId = bench.Members.First(id => id != colleague.HouseId && id != g.Mine.Id && g.W.House(id) != null);
        return (bench, g.W.House(bench.LeaderHouseId)!);
    }

    [Fact]
    public void YourColleague_StrikesTheFirstMotionOfYoursAgainstTheirBenchsLeader_AndTheSecondThatTermGoesToTheVote()
    {
        var (g, colleague, f) = Colleagues(); Answer(g, f, "keep_your_counsel");
        var (_, leader) = ColleaguesBench(g, colleague);
        Assert.Contains("once in the term, {grandee.name} will strike the first motion of yours", Content.Text("your_colleague_the_consul")!.Body);
        var first = IMove(g, "censure", leader.Id); Only(g, first); Session(g);
        Assert.True(first.Vetoed); Assert.Equal(colleague.HouseId, first.VetoedBy);   // "at their bench's leader"
        var second = IMove(g, "embargo", leader.Id); Only(g, second); Session(g);
        Assert.False(second.Vetoed); Assert.True(second.Resolved);   // the veto is once a term: the second goes to the vote
    }

    [Fact]
    public void YourColleague_StrikesYourActThatMovesTheLawOffTheirBenchsLine()
    {
        var (g, colleague, f) = Colleagues(); Answer(g, f, "keep_your_counsel");
        var bench = FactionSystem.Of(g.W, colleague.HouseId)!;
        var law = Content.Policies.Laws.Where(l => l.Scope == "senate" && !PolicySystem.OnCooldown(g.W, PolicySystem.SenateHolder, l.Id)).OrderBy(l => l.Id, StringComparer.Ordinal).First();
        string line = (PolicySystem.ReachOption(g.W, Content.Policies, law) ?? law.DefaultOption).Id; bench.Laws[law.Id] = line;   // the bench's line is the law as it stands
        var cmd = new ProposeMotionCommand(g.Me.Id, "act", "", g.Senate, lawId: law.Id, optionId: law.Options.First(o => o.Id != line).Id); Assert.True(g.Do(cmd), cmd.Refusal(g.Ctx));
        var act = g.W.Motions.Last(m => m.Open && m.ProposerHouseId == g.Mine.Id); Only(g, act); Session(g);
        Assert.True(act.Vetoed); Assert.Equal(colleague.HouseId, act.VetoedBy);   // "at their bench's line"
    }

    // ------------------------------------------------------------------ a vote for a vote: not with a house at war with yours

    [Fact]
    public void AVoteForAVote_IsNotOfferedByAHouseAtWarWithYours_AndTheChoiceIsClosedForOneThatGoesToWarAfterTheReportCame()
    {
        var g = Start(); var theirs = Others(g)[0]; g.W.Relationship(g.HeadOf(theirs).Id, g.Me.Id).Disposition = 0;
        g.Tc.Fleets.DeclareWar(g.W, g.Mine.Id, theirs.Id); var m = TheyMove(g, theirs);
        Assert.Contains(VoteMarks.BargainBarredChar, g.HeadOf(theirs).Flags);
        Assert.False(Eligible(g, "a_vote_for_a_vote", "senator", theirs.Id));   // an enemy's vote would be void as it was promised
        Assert.False(Eligible(g, "the_whip", "whip", theirs.Id) && g.HeadOf(theirs).Flags.Contains(VoteMarks.VoteWantedChar) && !g.HeadOf(theirs).Flags.Contains(VoteMarks.BargainBarredChar));
        // at peace when the report came, at war by the time it is answered
        var h = Start(); var them = Others(h)[0]; h.W.Relationship(h.HeadOf(them).Id, h.Me.Id).Disposition = 0; TheyMove(h, them);
        var (f, senator) = Report(h, "a_vote_for_a_vote", "senator"); Assert.Equal(h.HeadOf(them).Id, senator.Id);
        h.Tc.Fleets.DeclareWar(h.W, h.Mine.Id, them.Id);
        Assert.False(Open(h, f, "give_your_word")); Assert.Contains($"not while your house is at war with {them.Name}", Why(h, f, "give_your_word"));
        Assert.Empty(h.W.VotePledges); Assert.False(WealthSystem.FavourOwed(h.W, them.Id, h.Mine.Id));
    }

    // ------------------------------------------------------------------ two words on one vote

    /// <summary>A house moves a levy; another wants it thrown out and would take your word on it.</summary>
    private static (G g, House mover, House against, Motion levy) TwoWanting()
    {
        var g = Start(); var o = Others(g); var mover = o[0]; var against = o[1];
        g.W.Relationship(g.HeadOf(mover).Id, g.Me.Id).Disposition = 0; g.W.Relationship(g.HeadOf(against).Id, g.Me.Id).Disposition = 30;
        var levy = TheyMove(g, mover); foreach (var h in o.Where(h => h.Id != mover.Id && h.Id != against.Id)) Leans(g, levy, h, 1);
        Leans(g, levy, against, -6); return (g, mover, against, levy);
    }

    [Fact]
    public void TwoWordsOnOneVote_AWordOnTheHouseWindow_ClosesTheReportsBargainOnThatMotion_WithItsReason_AndNoSecondPledgeIsMade()
    {
        var (g, mover, against, levy) = TwoWanting();
        var (f, senator) = Report(g, "a_vote_for_a_vote", "senator"); Assert.Equal(g.HeadOf(mover).Id, senator.Id);
        var pledge = new DealingsCommand(g.Me.Id, DealingsCommand.Pledge, against.Id, g.Tc.Dealings, levy.Id); Assert.Null(pledge.Refusal(g.Ctx)); Assert.True(g.Do(pledge));
        Assert.Equal(-1, levy.Votes[g.Mine.Id]);
        Assert.DoesNotContain(VoteMarks.VoteWantedChar, g.HeadOf(mover).Flags);   // your vote on it is given: no report asks it again
        Assert.False(Open(g, f, "give_your_word")); Assert.False(Open(g, f, "name_a_price"));
        Assert.Contains($"your vote on the motion of {mover.Name} is pledged to {against.Name}, against it already", Why(g, f, "give_your_word"));
        Answer(g, f, "keep_your_vote");
        Assert.Single(g.W.VotePledges); Assert.Equal(-1, levy.Votes[g.Mine.Id]);
        int regard = g.Regard(against); Session(g); Assert.Equal(regard + DealingsSystem.PledgeKeptRegard, g.Regard(against));   // the one word, kept
    }

    [Fact]
    public void TwoWordsOnOneVote_AFavourYouOweTheMover_RefusesAWordAgainstItsMotion_OnTheHouseWindow()
    {
        var (g, mover, against, levy) = TwoWanting();
        WealthSystem.OweFor(g.W, g.Mine.Id, mover.Id); Assert.Same(levy, WealthSystem.CalledOn(g.W, g.Mine.Id, mover.Id));
        var pledge = new DealingsCommand(g.Me.Id, DealingsCommand.Pledge, against.Id, g.Tc.Dealings, levy.Id);
        Assert.Equal($"you owe {mover.Name} a favour, and it is called in at this motion's count: a word against it would break that", pledge.Refusal(g.Ctx));
    }

    // ------------------------------------------------------------------ foreign gold and a word on the same vote

    /// <summary>A war on the Free Cities before the Senate, moved by another house, which has bought your vote for it.</summary>
    private static (G g, House mover, Motion war) WarSold()
    {
        var g = Start(); var mover = Others(g)[0]; g.W.Relationship(g.HeadOf(mover).Id, g.Me.Id).Disposition = 0; mover.Treasury.Credits = Math.Max(50, mover.Treasury.Credits);
        g.Me.NatureTraits.Remove("Honorable"); g.Me.NatureTraits.Remove("Just");
        var war = g.Senate.Add(g.W, mover.Id, "foreign_war", "thessar"); YearEnds(g); Only(g, war);
        var (f, senator) = Report(g, "a_vote_for_a_vote", "senator"); Assert.Equal(g.HeadOf(mover).Id, senator.Id); Answer(g, f, "name_a_price");
        Assert.Equal(1, war.Votes[g.Mine.Id]); return (g, mover, war);
    }

    [Fact]
    public void ForeignGold_IsNotWrittenToNorTakenByAHouseWhoseVoteIsSold_AndAVoteTheGoldHoldsIsNotSold()
    {
        var (g, mover, war) = WarSold();
        foreach (var h in Others(g).Where(h => h.Id != mover.Id)) Leans(g, war, h, 1);   // the war would carry: the Cities write
        ForeignGoldSystem.Tick(g.W, Content.Nations, g.Senate, Content.Offices);
        Assert.DoesNotContain(g.W.GoldOffers, o => o.HouseId == g.Mine.Id);   // nobody offers gold for a vote already sold
        // a factor who came before the sale: taking it would turn the sold vote, and is refused with its reason
        var head = g.Me; var o = new GoldOffer { Id = "gold_test", NationId = "thessar", WarOnId = "thessar", NationName = "the Free Cities of Thessar", WarOnName = "the Free Cities of Thessar", MotionId = war.Id, HouseId = g.Mine.Id, CharacterId = head.Id, Ask = ForeignGoldSystem.Against, Sum = 20, Turn = g.W.Turn, Before = 1, Seats = g.Senate.Weight(g.W, g.Mine.Id) };
        g.W.GoldOffers.Add(o);
        Assert.Equal($"your vote on it is bought by {mover.Name}, for it: take this and you break that word (its head would think -15 of you)", ForeignGoldSystem.AnswerRefusal(g.W, g.Me, o, ForeignGoldSystem.Take));
        Assert.Equal(1, war.Votes[g.Mine.Id]);
        // the other way about: the gold taken first, then a buyer: the report's sale is closed with its reason
        var h2 = Start(); var mover2 = Others(h2)[0]; h2.W.Relationship(h2.HeadOf(mover2).Id, h2.Me.Id).Disposition = 0; mover2.Treasury.Credits = Math.Max(50, mover2.Treasury.Credits);
        h2.Me.NatureTraits.Remove("Honorable"); h2.Me.NatureTraits.Remove("Just");
        var war2 = h2.Senate.Add(h2.W, mover2.Id, "foreign_war", "thessar"); YearEnds(h2); Only(h2, war2);
        var (f2, _) = Report(h2, "a_vote_for_a_vote", "senator");
        var o2 = new GoldOffer { Id = "gold_test2", NationId = "thessar", WarOnId = "thessar", NationName = "the Free Cities of Thessar", WarOnName = "the Free Cities of Thessar", MotionId = war2.Id, HouseId = h2.Mine.Id, CharacterId = h2.Me.Id, Ask = ForeignGoldSystem.Against, Sum = 20, Turn = h2.W.Turn, Before = 1, Seats = h2.Senate.Weight(h2.W, h2.Mine.Id) };
        h2.W.GoldOffers.Add(o2); Assert.True(h2.Do(new ForeignGoldCommand(h2.Me.Id, o2.Id, ForeignGoldSystem.Take)));
        Assert.Equal(-1, war2.Votes[h2.Mine.Id]); Assert.False(Open(h2, f2, "name_a_price")); Assert.False(Open(h2, f2, "give_your_word"));
        Assert.Contains("held by the gold you took from the Free Cities of Thessar to vote against the war", Why(h2, f2, "name_a_price"));
        Assert.Empty(h2.W.VotePledges);
    }

    // ------------------------------------------------------------------ the bench remembers: a motion moved before the break, and the apology

    [Fact]
    public void TheWhipsDispleasure_YourMotionMovedBeforeTheBreak_HasTheBenchsMemoryAtItsCount()
    {
        var (g, leader, mover, other, bench) = Seated();
        var theirs = Moves(g, mover); YearEnds(g);   // year four: their motion moved, nothing counted
        YearOpens(g); var mine = IMove(g); Only(g, theirs, mine); Leans(g, theirs, leader, 3); IVote(g, theirs, -1);   // year five: your motion moved, and your vote against your bench's
        YearEnds(g);   // the end of year five: their motion counted, your vote breaks the whip
        Assert.True(theirs.Resolved); Assert.False(mine.Resolved); Assert.True(g.W.HasFlag("broke_the_whip"));
        Assert.Equal(-FactionSystem.WhipRemembered, By(g, mine, other, "it remembers that you voted against the Test Bench"));
        Leans(g, mine, leader, 1); Leans(g, mine, other, 0); int forecast = g.Senate.Stance(g.W, mine, other.Id); Assert.Equal(0, forecast);
        Session(g);   // the end of year six: your motion counted
        Assert.Equal(forecast, mine.Votes[other.Id]); Assert.Equal(0, g.W.Counter(FactionSystem.WhipKey(bench.Id)));
    }

    [Fact]
    public void TheWhipsDispleasure_AnApologyAccepted_TheBenchLetsTheVoteGo()
    {
        var (g, leader, mover, other, bench) = Seated();
        var theirs = TheyMove(g, mover); Leans(g, theirs, leader, 3); IVote(g, theirs, -1); Session(g); YearOpens(g);
        var mine = IMove(g); Only(g, mine); Assert.Equal(-FactionSystem.WhipRemembered, By(g, mine, other, "it remembers"));
        Choose(g, ReportOf(g, "the_whips_displeasure"), "apologise");
        Assert.Equal(0, By(g, mine, other, "it remembers")); Assert.Equal(0, g.W.Counter(FactionSystem.WhipKey(bench.Id)));
        Assert.True(g.Told($"{leader.Name} accepted the apology: the Test Bench will not lean against your next motion for that vote."));
    }

    // ------------------------------------------------------------------ the leader's table: for the next count that counts anything

    [Fact]
    public void TheLeadersTable_AYearWithNothingCounted_KeepsTheTablesFollowingForTheCountOfYourMotion()
    {
        var (g, bench, members) = Leading(); var member = members[0];
        g.W.WorldCounters["senate_influence"] = 5; g.Mine.Treasury.Credits = 2000;
        FactionSystem.Tick(g.W, Wt(g)); Assert.True(g.W.HasFlag("faction_leader")); Assert.Empty(SenateSystem.Docket(g.W));
        var f = ReportOf(g, "the_leaders_table"); Answer(g, f, "a_dinner");   // the dinner, with nothing before the Senate
        var levy = IMove(g); Only(g, levy);   // and your motion moved the same year
        Assert.Equal(FactionSystem.FollowedBy, By(g, levy, member, "has fed its bench at the house's table"));
        YearEnds(g); Assert.True(g.W.HasFlag(FactionSystem.FedFlag));   // the year's own session counts nothing: the dinner waits
        YearOpens(g); Assert.Equal(FactionSystem.FollowedBy, By(g, levy, member, "has fed its bench at the house's table"));
        // with another house's motion due at this year's session, the table is for that one only
        var mover = Others(g).First(h => !bench.Members.Contains(h.Id)); var due = g.Senate.Add(g.W, mover.Id, "relief", g.W.Galaxy!.ReachRegions.OrderBy(r => r.Id, StringComparer.Ordinal).First().Id); due.ProposedTurn = g.W.Turn - 1;
        Assert.True(FactionSystem.AtNextCount(g.W, due)); Assert.True(FactionSystem.AtNextCount(g.W, levy));
        g.W.Motions.Remove(due);
        Session(g); Assert.True(levy.Resolved); Assert.False(g.W.HasFlag(FactionSystem.FedFlag));   // counted, and gone with that count
    }

    // ------------------------------------------------------------------ the friend in the chair keeps the veto for you

    [Fact]
    public void TheOtherConsulCourted_IsToldWhatTheyPromise_KeepsTheVetoOverTheirBenchsBusiness_AndSpentOnTheirOwnHouseYouAreTold()
    {
        var (g, other, f) = Struck(); g.W.News.Clear(); Choose(g, f, "court_the_other_consul"); Session(g);
        Assert.True(g.Told($"{other.Name} of {g.W.House(other.HouseId)!.Name} is your friend in the chair: once this term they strike with their veto the first motion before the Senate that strikes at your house"));
        Assert.Contains(ElectionSystem.FriendChar, other.Flags);
        var friendBench = FactionSystem.Of(g.W, other.HouseId)!;
        if (friendBench.LeaderHouseId == other.HouseId || friendBench.LeaderHouseId == g.Mine.Id) friendBench.LeaderHouseId = friendBench.Members.First(id => id != other.HouseId && id != g.Mine.Id && g.W.House(id) != null);
        var third = Others(g).First(h => h.Id != other.HouseId && h.Id != Holder(g, "consul_senior")!.HouseId && h.Id != friendBench.LeaderHouseId);
        var atLeader = Moves(g, third, "censure", friendBench.LeaderHouseId); Session(g);
        Assert.False(atLeader.Vetoed); Assert.Contains(ElectionSystem.FriendChar, other.Flags);   // the veto kept for you, not spent on their bench's leader
        var atThem = Moves(g, third, "embargo", other.HouseId); g.W.News.Clear(); Session(g);
        Assert.True(atThem.Vetoed); Assert.Equal(other.HouseId, atThem.VetoedBy); Assert.DoesNotContain(ElectionSystem.FriendChar, other.Flags);
        Assert.True(g.Told($"{other.Name}, as Junior Consul, has struck a motion against their own house with their veto"));
        Assert.True(g.Told("The veto they would have used for you this term is gone with it."));
        var atYou = Moves(g, third, "censure", g.Mine.Id); foreach (var h in Others(g).Where(h => h.Id != third.Id)) Leans(g, atYou, h, 6); Session(g);
        if (!atYou.Resolved) Session(g);   // held over by the chair, it comes a year later
        Assert.False(atYou.Vetoed); Assert.True(atYou.Resolved);   // and you knew it would not be
    }

    // ------------------------------------------------------------------ a house that owes you a vote: its consul does not strike your motion
    // (made the player's way, through the_senate_listens/build_bloc: tests/VotePromisesRound2Tests.cs, AHouseBroughtOverToYourSide_...)

    // ------------------------------------------------------------------ a bribe or a whip on an owed vote

    [Fact]
    public void ABribeOrAWhip_IsRefusedOnAHouseThatOwesYouAVote_ItVotesForYourMotionAlready()
    {
        var (g, theirs, _, _) = Bargained(); var levy = IMove(g); HardAgainst(g, levy, theirs);
        var bribe = new LobbyCommand(g.Me.Id, levy.Id, theirs.Id, 1, bribe: true); var whip = new LobbyCommand(g.Me.Id, levy.Id, theirs.Id, 1);
        Assert.Equal("it votes for your motion already: it owes your house a favour", bribe.Refusal(g.Ctx));
        Assert.Equal("it votes for your motion already: it owes your house a favour", whip.Refusal(g.Ctx));
        var call = new DealingsCommand(g.Me.Id, DealingsCommand.CallVote, theirs.Id, g.Tc.Dealings, levy.Id);
        Assert.Equal("it votes for your motion already: the favour is kept for one it would not vote for", call.Refusal(g.Ctx));
    }

    // ------------------------------------------------------------------ a vote for a vote: your word broken, theirs goes too

    [Fact]
    public void AVoteForAVote_YourWordBroken_TheirHouseOwesYouNothing_AndItsVoteOnYourNextMotionIsItsOwn()
    {
        var (g, theirs, _, theirMotion) = Bargained(); Assert.True(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id));
        Assert.True(g.Told("In return " + theirs.Name + " owes your house a vote, and if you break your word it owes you nothing."));
        IVote(g, theirMotion, -1); Session(g);
        Assert.False(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id));
        Assert.True(g.Told($"{theirs.Name} will not give the vote it owed your house for a word you did not keep: it owes your house nothing now."));
        Only(g); var levy = IMove(g); HardAgainst(g, levy, theirs); Assert.False(g.Senate.Owes(g.W, levy, theirs.Id));
        Session(g); Assert.Equal(-1, levy.Votes[theirs.Id]);
    }

    // ------------------------------------------------------------------ a vote for a vote: lapses with their house gone

    [Fact]
    public void AVoteForAVote_LapsesWhenTheirHouseIsGone_WithANotice()
    {
        var (g, theirs, _, _) = Bargained(); string name = theirs.Name; var into = Others(g).First(h => h.Id != theirs.Id);
        HouseSystem.Fold(g.W, theirs, into, $"{name} is no more."); Assert.Null(g.W.House(theirs.Id));
        WealthSystem.Tick(g.W);   // the year's end
        Assert.False(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id));
        Assert.True(g.Told("A house that owed yours a favour is no more, and the favour is gone with it."));
    }

    // ------------------------------------------------------------------ a threat against you: past its years it costs nothing

    [Fact]
    public void ThreatenedByAHouse_PastTheThreatsYears_AVoteAgainstWhatItMovesCostsNothing()
    {
        var g = Start(); var them = g.W.House("aurelian")!; foreach (var f in g.W.Fleets.Values.Where(f => f.OwnerId == them.Id)) f.Strength = 40;
        g.Tc.Dealings.Threat(g.W, them, g.Mine); int told = g.W.Turn;
        g.W.Turn = told + DealingsSystem.CowedYears + 1;   // year N has passed
        Assert.DoesNotContain(g.Tc.Dealings.Lasting(g.W, g.Mine, them), l => l.text.StartsWith("It threatened you"));
        var m = Moves(g, them); IVote(g, m, -1); int regard = g.Regard(them); g.W.News.Clear();
        Session(g);
        Assert.True(m.Resolved); Assert.Equal(-1, m.Votes[g.Mine.Id]); Assert.Equal(regard, g.Regard(them)); Assert.False(g.Told("which had told you what its fleets could do"));
    }

    // ------------------------------------------------------------------ your threat: nor for a motion that strikes at your house

    [Fact]
    public void YourThreat_AHouseAfraidOfYou_DoesNotVoteForACensureOfYourHouse_ItStaysAway()
    {
        var g = Start(); var them = g.W.House("ashgrove")!;
        if (FleetSystem.StrengthOf(g.W, g.Mine.Id) < DealingsSystem.ThreatOdds) g.W.Fleets.Values.First(f => f.OwnerId == g.Mine.Id).Strength = 5;
        var threat = new DealingsCommand(g.Me.Id, DealingsCommand.Threat, them.Id, g.Tc.Dealings); Assert.True(g.Do(threat), threat.Refusal(g.Ctx));
        var mover = Others(g).First(h => h.Id != them.Id); var censure = Moves(g, mover, "censure", g.Mine.Id);
        for (int i = 0; i < 4 && g.Senate.RawLean(g.W, censure, them.Id) != 3; i++) censure.Lobby[them.Id] = censure.Lobby.GetValueOrDefault(them.Id) + 3 - g.Senate.RawLean(g.W, censure, them.Id);
        Assert.Equal(0, g.Senate.Lean(g.W, censure, them.Id)); Assert.Contains(g.Senate.Why(g.W, censure, them.Id), x => x.why.StartsWith("it is afraid of"));
        Session(g); Assert.True(censure.Resolved); Assert.Equal(0, censure.Votes[them.Id]);
    }

    // ------------------------------------------------------------------ the repeal promised: a year the docket is full gives a year more

    [Fact]
    public void TheActFallsOnAFriend_PromiseTheRepeal_AYearTheDocketIsFullWithOtherHousesBusiness_GivesAYearMore_AndThenItIsBroken()
    {
        var (g, theirs, _, law, act, from) = RepealPromised();
        int by = from + VoteMarks.RepealYears - 1;
        YearOf(g, by - 1); g.W.Turn = by;   // the last year comes; the docket is full with two other houses' business all of it
        var a = Others(g).Where(h => h.Id != theirs.Id).ToList(); var one = g.Senate.Add(g.W, a[0].Id, "levy", ""); var two = g.Senate.Add(g.W, a[1].Id, "relief", g.W.Galaxy!.ReachRegions.OrderBy(r => r.Id, StringComparer.Ordinal).First().Id);
        Assert.Equal("the docket is full this session", RepealOf(g, law, act).Refusal(g.Ctx));
        int regard = g.Regard(theirs); g.W.News.Clear(); YearEnds(g);
        Assert.Equal(regard, g.Regard(theirs)); Assert.True(g.Told($"Your word to {theirs.Name} waits a year more: move it by year {by + 1}."));
        g.W.News.Clear(); Only(g); YearOf(g, by + 1);   // a year more, and the docket open: let it pass
        Assert.Equal(regard - DealingsSystem.PledgeBrokenRegard, g.Regard(theirs)); Assert.True(g.Told($"and year {by + 1} has passed without it"));
    }

    // ------------------------------------------------------------------ your patron's backing, never counted

    [Fact]
    public void YourPatronsBacking_WhenYourHouseIsNotStandingAtTheCount_TheSeatsWereNeverCounted_AndYouOweNothingForThem()
    {
        var (g, patron) = Backed(); Assert.True(WealthSystem.FavourOwed(g.W, g.Mine.Id, patron.Id));
        g.W.WorldCounters["consul_term:" + g.Mine.Id] = g.W.Turn; Assert.False(ElectionSystem.Eligible(g.W, g.Senate.Offices, g.Mine.Id));   // your house may not stand when the count comes
        g.W.News.Clear(); Session(g); Assert.True(g.W.HasFlag("election_held"));
        Assert.False(WealthSystem.FavourOwed(g.W, g.Mine.Id, patron.Id));
        Assert.True(g.Told($"your house was not standing when the consuls were elected, so the seats {patron.Name} pledged you were never counted for you, and you owe it nothing for them"));
        // and with a throne, the same: nothing is owed for seats no election will count
        var (h, p2) = Backed(); ElectionSystem.NoMoreElections(h.W);
        Assert.False(WealthSystem.FavourOwed(h.W, h.Mine.Id, p2.Id)); Assert.Null(ElectionSystem.PledgedTo(h.W, p2.Id));
        Assert.True(h.Told($"A throne stands, and the consuls will not be elected again, so the seats {p2.Name} pledged you were never counted for you, and you owe it nothing for them"));
    }
    /// <summary>The year before the consuls are elected: you stand, and your patron, which does not, backs you at your asking.</summary>
    private static (G g, House patron) Backed()
    {
        var g = ElectionYear(); var offices = g.Senate.Offices; Assert.True(g.Do(new StandForConsulCommand(g.Me.Id, offices)));
        var nominees = ElectionSystem.Nominees(g.W, offices, Wt(g));
        var patron = Others(g).First(h => !nominees.Any(n => n.houseId == h.Id) && !g.W.HasFlag(ElectionSystem.StandFlag(h.Id)));
        g.Mine.PatronId = patron.Id; g.W.Relationship(g.HeadOf(patron).Id, g.Me.Id).Disposition = 20;
        var ask = new AskAboveCommand(g.Me.Id, patron.Id, AskAbove.Backing, offices, g.Senate); Assert.True(g.Do(ask), ask.Refusal(g.Ctx));
        Assert.Equal(g.Mine.Id, ElectionSystem.PledgedTo(g.W, patron.Id)); return (g, patron);
    }

    // ------------------------------------------------------------------ the debt called: with nothing before the Senate, a vote kept

    [Fact]
    public void TheFavourCalled_NothingBeforeTheSenate_TheDebtIsKeptAsAVoteTheirHouseOwesYours_ForYourNextMotion()
    {
        var (g, theirs, debtor, f) = Indebted((g, theirs) => { });
        Assert.Empty(SenateSystem.Docket(g.W)); Assert.True(Open(g, f, "call_it_in"));
        Answer(g, f, "call_it_in");
        Assert.True(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id));
        Assert.True(g.Told($"No motion before the Senate wants the vote of {theirs.Name} now, so {debtor.Name} owes it instead: {theirs.Name} owes your house a vote. It votes for your next motion whichever way it leans, unless that motion strikes at it."));
        var levy = IMove(g); HardAgainst(g, levy, theirs); YearEnds(g); Session(g); Assert.Equal(1, levy.Votes[theirs.Id]);
    }

    // ------------------------------------------------------------------ the whip's favour named later: your vote for their motion

    [Fact]
    public void TheWhip_NameYourPrice_YourVoteIsGivenForTheirMotion_Reminded_AndJudgedAtItsCount()
    {
        var g = Start(); g.Me.Skills["oratory"] = 5; var theirs = Others(g)[0]; g.W.Relationship(g.HeadOf(theirs).Id, g.Me.Id).Disposition = 0;
        var m = TheyMove(g, theirs); var (f, whip) = Report(g, "the_whip", "whip"); Assert.Equal(theirs.Id, whip.HouseId);
        Answer(g, f, "name_your_price");
        Assert.Equal(1, m.Votes[g.Mine.Id]); Assert.NotNull(DealingsSystem.PledgeOn(g.W, g.Mine.Id, m.Id)); Assert.True(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id));
        Assert.True(g.Told($"You gave {theirs.Name} your word: your vote on its motion is declared for it."));
        int regard = g.Regard(theirs); Session(g); Assert.Equal(regard + DealingsSystem.PledgeKeptRegard, g.Regard(theirs));
        Assert.True(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id));   // the favour to be named later stands, whichever it is named for
    }

    // ------------------------------------------------------------------ the roll of a division on a motion for your house

    [Fact]
    public void TheRollOfTheDivision_OnAMotionForYourHouse_TellsWhoVotedEachWay_AndWritesTheNoesDown()
    {
        var g = Start(); var mover = Others(g)[0]; var ayes = Others(g).Skip(1).Take(1).ToList(); var noes = Others(g).Skip(2).ToList();
        var home = g.Senate.Add(g.W, mover.Id, "triumph", g.Mine.Id); Only(g, home);
        foreach (var h in ayes) Leans(g, home, h, 2); foreach (var h in noes) Leans(g, home, h, -2);   // the houses as they lean: the count casts them
        Session(g); Assert.True(home.Resolved); Assert.False(home.Passed);
        Assert.Contains(VoteMarks.AgainstYoursHouseChar, g.HeadOf(noes[0]).Flags);
        g.W.News.Clear(); Choose(g, ReportOf(g, "the_triumph_refused"), "learn_who_voted");   // the report, as the briefing answers it
        Assert.True(g.Told("The roll of the division: for it,")); Assert.True(g.Told($"against it, {noes.OrderBy(h => h.Name, StringComparer.Ordinal).First().Name}"));
        Assert.Contains(g.Tc.Dealings.Lasting(g.W, g.Mine, noes[0]), l => l.text == $"It voted against a motion for your house in year {g.W.Turn}, and you wrote the name down.");
        foreach (var e in new[] { ("caesar", "the_triumph_refused", "learn_who_voted"), ("emergency", "stripped_of_the_powers", "remember_who_voted"), ("exile", "called_home", "remember_who_voted") })
            Assert.Contains(Content.Event(e.Item2)!.Choice(e.Item3)!.Effects, x => x is SetWorldFlagEffect s && s.Flag == VoteMarks.DivisionFlag && s.Value);
    }
}
