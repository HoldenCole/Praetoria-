using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Group "round 2" (docs/events/votes.md, "Promises", round 2): what the checkers found after the closing round. Each hook they
/// took out with every test still green now has a test that fails without it, played in the order the game plays it; each promise found
/// unkept is kept, and proved the same way.</summary>
public partial class VotePromisesTests
{
    /// <summary>The house's chancellor, as the household window gives the post.</summary>
    private static Character Chancellor(G g)
    {
        var k = HouseholdSystem.Eligible(g.W, g.Mine).FirstOrDefault(c => HouseholdSystem.AssignRefusal(g.W, g.Me, HouseholdSystem.Chancellor, c) == null);
        if (k == null) { k = new Character { Id = "kin_chancellor", Name = "Test Chancellor", HouseId = g.Mine.Id, Age = 30, Sex = "female", CareerTrack = "law" }; g.W.Characters[k.Id] = k; g.Mine.Members.Add(k.Id); g.W.Pools[k.Id] = ActionPools.ForNpc(); g.W.Relationship(k.Id, g.Me.Id).Disposition = 40; }
        var give = new DelegateCommand(g.Me.Id, HouseholdSystem.Chancellor, k.Id, 0); Assert.True(g.Do(give), give.Refusal(g.Ctx));
        return k;
    }
    /// <summary>Blood between your house and theirs, as a killing by your hand leaves it: one of theirs, not their head nor one who owes you a turn.</summary>
    private static void Blooded(G g, House theirs)
    {
        // one of theirs by your hand; or, in a house of only its head and the one who owes you, one of yours by theirs: blood either way
        var victim = g.W.LivingCharacters().Where(c => c.HouseId == theirs.Id && c.Id != g.HeadOf(theirs).Id && !c.Flags.Contains("owes_you")).OrderByDescending(c => c.Age >= DynastySystem.AgeOfMajority).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
        if (victim != null) Assert.NotNull(FeudSystem.Blood(g.W, victim, g.Mine.Id, g.Me.Id, "hand"));
        else { victim = g.W.LivingCharacters().Where(c => c.HouseId == g.Mine.Id && c.Id != g.Me.Id && c.Id != "bastien_castellan").OrderBy(c => c.Id, StringComparer.Ordinal).First(); Assert.NotNull(FeudSystem.Blood(g.W, victim, theirs.Id, g.HeadOf(theirs).Id, "hand")); }
        DynastySystem.Die(g.W, victim.Id); Assert.True(FeudSystem.Between(g.W, g.Mine.Id, theirs.Id));
    }
    /// <summary>A sum from the Free Cities on a war before the Senate, as their factor brings it (ForeignGoldSystem.Write).</summary>
    private static GoldOffer Offer(G g, Motion war, string ask)
    {
        var o = new GoldOffer { Id = "gold_r2_" + ask, NationId = "thessar", WarOnId = "thessar", NationName = "the Free Cities of Thessar", WarOnName = "the Free Cities of Thessar", MotionId = war.Id, HouseId = g.Mine.Id, CharacterId = g.Me.Id, Ask = ask, Sum = 20, Turn = g.W.Turn, Before = 1, Seats = g.Senate.Weight(g.W, g.Mine.Id) };
        g.W.GoldOffers.Add(o); return o;
    }
    private static string Region(G g) => g.W.Galaxy!.ReachRegions.OrderBy(r => r.Id, StringComparer.Ordinal).First().Id;

    // ------------------------------------------------------------------ the word to the bench, kept by the chancellor (HouseholdSystem.ChancellorActs)

    [Fact]
    public void TheEmptySeat_TheChancellorPutsTheHouseOnItsFeetWithTheLeader_OnAVoteTheWordCovers_AndTheWordIsKept()
    {
        var (g, leader, mover, _) = Promised(); Chancellor(g);
        var m = g.Senate.Add(g.W, mover.Id, "levy", ""); Only(g, m); Leans(g, m, leader, 3); Leans(g, m, g.Mine, 0);   // your house leans neither way: left as it leans, it would sit
        int regard = g.Regard(leader), affinity = Affinity(g, leader); g.W.News.Clear();
        g.W.Turn++; g.Tc.Household.Tick(g.W, g.Tc.Executor, g.Ctx);   // the year's end: the chancellor declares what you left alone, before the Senate sits
        Assert.Equal(1, m.Votes[g.Mine.Id]);
        Assert.Contains("kept the house on its feet with the leader of its bench", g.Mine.Duty(HouseholdSystem.Chancellor)!.LastReport);
        g.Senate.Tick(g.W);
        Assert.True(m.Resolved); Assert.Equal(m.Votes[leader.Id], m.Votes[g.Mine.Id]);
        Assert.True(g.Told($"You kept your word to {leader.Name}: your house was on its feet at the vote."));
        Assert.Equal(regard, g.Regard(leader)); Assert.Equal(affinity + 1, Affinity(g, leader)); Assert.False(g.Told("it stayed in its seat"));   // +1 for voting alike (FactionSystem.RecordVotes), no -2
    }

    // ------------------------------------------------------------------ only a vote you cast breaks the whip (SenateSystem.Resolve, !undeclared)

    [Fact]
    public void TheWhip_AVoteYouLeftAlone_CountedByYourHousesLean_DoesNotBreakIt()
    {
        var (g, leader, mover, _, bench) = Seated();
        var m = TheyMove(g, mover); Leans(g, m, leader, 3); Leans(g, m, g.Mine, -3);   // the bench's leader is for it; your house, left as it leans, against
        Assert.False(m.Votes.ContainsKey(g.Mine.Id)); int regard = g.Regard(leader), affinity = Affinity(g, leader); g.W.News.Clear();
        Session(g);
        Assert.True(m.Resolved); Assert.Equal(1, m.Votes[leader.Id]); Assert.Equal(-1, m.Votes[g.Mine.Id]);   // counted by its lean, the other way from the leader
        Assert.False(g.W.HasFlag("broke_the_whip")); Assert.Equal(regard, g.Regard(leader)); Assert.Equal(affinity - 1, Affinity(g, leader));   // the count's own -1 for voting apart, not the whip's -2 more
        Assert.False(g.Told("You voted against")); Assert.Equal(0, g.W.Counter(FactionSystem.WhipKey(bench.Id)));
    }

    // ------------------------------------------------------------------ a purse that comes out on an owed vote (SenateSystem.Resolve, owed.Remove)

    [Fact]
    public void APurseThatComesOut_OnAHouseThatOwesYouAVote_SpendsNoFavour_SaysNoWordKept_AndTheFavourPaysYourNextMotion_AcrossASaveAndALoad()
    {
        var g0 = Start(); g0.Mine.Treasury.Credits = 5000; var m0 = IMove(g0);
        var outed0 = Takers(g0, m0, comesOut: true).First(h => g0.Tc.Dealings.GiftOwes(g0.W, h, DealingsSystem.GiftSizes.Max()));
        var bribe = new LobbyCommand(g0.Me.Id, m0.Id, outed0.Id, 1, bribe: true); Assert.True(g0.Do(bribe), bribe.Refusal(g0.Ctx));   // the purse, before any favour stood
        Gives(g0, outed0);   // then a gift on the house window: it owes your house a vote
        HardAgainst(g0, m0, outed0); Assert.Equal(1, g0.Senate.Stance(g0.W, m0, outed0.Id));
        var g = Reload(g0); var m = g.W.Motions.First(x => x.Id == m0.Id); var outed = g.W.House(outed0.Id)!; g.W.News.Clear();
        Session(g);
        Assert.True(m.Resolved); Assert.Equal(0, m.Votes[outed.Id]);   // the purse came out: the vote is struck
        Assert.True(WealthSystem.FavourOwed(g.W, outed.Id, g.Mine.Id)); Assert.False(g.Told($"{outed.Name} kept its word"));   // nothing spent, nothing said kept
        Only(g); var next = IMove(g, "relief", Region(g)); HardAgainst(g, next, outed); g.W.News.Clear(); Session(g);
        Assert.Equal(1, next.Votes[outed.Id]); Assert.True(g.Told($"{outed.Name} kept its word")); Assert.False(WealthSystem.FavourOwed(g.W, outed.Id, g.Mine.Id));   // it pays your next
    }

    // ------------------------------------------------------------------ blood: the house window, a debt called in, the bench split

    [Fact]
    public void BloodOnOurHands_NoVoteIsAskedOnTheHouseWindow_NoDebtIsPaidInTheSenate_AndTheHouseIsNotTheOneSplitFromItsBench()
    {
        // the house window's ask for a vote (DealingsSystem.AskRefusal)
        var g = Start(); var theirs = Others(g)[0]; var mine = IMove(g); Blooded(g, theirs); g.W.Relationship(g.HeadOf(theirs).Id, g.Me.Id).Disposition = 60;
        var ask = new DealingsCommand(g.Me.Id, DealingsCommand.AskVote, theirs.Id, g.Tc.Dealings, mine.Id);
        Assert.Equal("there is blood between your houses: they owe you nothing in the Senate", ask.Refusal(g.Ctx)); Assert.False(mine.Votes.ContainsKey(theirs.Id));
        // a debt called in from one of that house (DealingsSystem.DebtPaid)
        var (h, debtorHouse, debtor, f) = Indebted((x, t) => { IMove(x); Blooded(x, t); });
        Answer(h, f, "call_it_in");
        Assert.True(h.Told($"There is blood between your house and {debtorHouse.Name}, so {debtor.Name} pays what they owed in other ways: it owes you nothing in the Senate."));
        Assert.DoesNotContain(h.W.Motions, m => m.Open && m.Votes.ContainsKey(debtorHouse.Id)); Assert.False(WealthSystem.FavourOwed(h.W, debtorHouse.Id, h.Mine.Id));
        // a bench's leader moves a censure of your house: its least member, with blood between it and yours, is not the one found what it wants (VoteMarks.Docket)
        House? least = null;
        var (a, _, bench, censure) = Aisle((x, m, b) =>
        {
            foreach (var id in b.Members.Where(id => id != b.LeaderHouseId)) Leans(x, m, x.W.House(id)!, 6);
            least = b.Members.Where(id => id != b.LeaderHouseId && id != x.Mine.Id).OrderBy(id => x.Senate.Weight(x.W, id)).ThenBy(id => x.Senate.Lean(x.W, m, id)).ThenBy(id => id, StringComparer.Ordinal).Select(id => x.W.House(id)!).First();
            Blooded(x, least);
        });
        Assert.DoesNotContain(VoteMarks.WeakestChar, a.HeadOf(least!).Flags);
        Assert.True(a.W.HasFlag(VoteMarks.SplittableFlag)); var marked = a.W.LivingCharacters().Single(c => c.Flags.Contains(VoteMarks.WeakestChar));
        Assert.NotEqual(least!.Id, marked.HouseId); Assert.Contains(marked.HouseId, bench.Members);   // the next least of the bench
    }

    // ------------------------------------------------------------------ a gift sent back: that giver's, of that kind (WealthSystem.Answered)

    /// <summary>An orrery sent by the house <paramref name="pick"/> names, with a motion before the Senate, as WealthSystem sends one, and its report.</summary>
    private static (G g, House theirs, Motion theirMotion, FiredEvent report) OrreryFrom(Func<G, House> pick)
    {
        var g = Start(); var theirs = pick(g); var head = g.HeadOf(theirs); g.W.Heirlooms.Clear(); g.Mine.Legitimacy = 99;
        var theirMotion = TheyMove(g, theirs); int years = 0;
        while (!g.W.HasFlag(WealthSystem.AsksFavourFlag) && years++ < 200)
        { g.W.Turn++; foreach (var h in g.W.Houses.Values) h.Treasury.Credits = 0; theirs.Treasury.Credits = 200; g.W.Relationship(head.Id, g.Me.Id).Disposition = 0; WealthSystem.Tick(g.W); }
        Assert.True(WealthSystem.FavourOwed(g.W, g.Mine.Id, theirs.Id));
        var (f, giver) = Report(g, "a_gift_that_asks_a_favour", "giver"); Assert.Equal(head.Id, giver.Id);
        return (g, theirs, theirMotion, f);
    }

    [Fact]
    public void AGiftThatAsks_SentBack_SendsBackThatGiversOrrery_NotAnotherGiftTheHouseHolds_AndOnlyItsFavourGoes()
    {
        // the giver: a short name, and not the last by name, so that neither the longest gift's name nor the last one's is its own
        var (g, theirs, theirMotion, f) = OrreryFrom(g => Others(g).Where(h => g.Senate.MayPropose(g.W, h.Id) && h.Id != Others(g).OrderByDescending(x => x.Id, StringComparer.Ordinal).First().Id).OrderBy(h => h.Id.Length).ThenBy(h => h.Id, StringComparer.Ordinal).First());
        // the house holds two other gifts besides: an older plate from a house whose name is longer, and an orrery from another house this year
        var other = Others(g).Where(h => h.Id != theirs.Id).OrderByDescending(h => h.Id.Length).ThenBy(h => h.Id, StringComparer.Ordinal).First();
        Assert.True(other.Id.Length > theirs.Id.Length + 1, "the old rule took the longest name's gift");
        var plate = new Heirloom { Id = $"gift_{other.Id}_{g.Mine.Id}_{g.W.Turn - 3:D4}", Name = "the old gift", Kind = WealthSystem.Plate, HolderId = g.Me.Id, OfHouseId = g.Mine.Id, FromHouseId = other.Id, Value = WealthSystem.PlateValue };
        var third = Others(g).Where(h => h.Id != theirs.Id && h.Id != other.Id).OrderByDescending(h => h.Id, StringComparer.Ordinal).First();   // the last by name
        var orrery2 = new Heirloom { Id = $"gift_{third.Id}_{g.Mine.Id}_{g.W.Turn:D4}o", Name = "another orrery", Kind = WealthSystem.Orrery, HolderId = g.Me.Id, OfHouseId = g.Mine.Id, FromHouseId = third.Id, Value = 1 };
        g.W.Heirlooms.Add(plate); g.W.Heirlooms.Add(orrery2); WealthSystem.OweFor(g.W, g.Mine.Id, third.Id); g.HeadOf(third).Flags.Add(WealthSystem.AsksFavourChar);
        Answer(g, f, "send_it_back");
        IVote(g, theirMotion, -1); int before = g.Regard(theirs); Session(g);
        Assert.DoesNotContain(g.W.Heirlooms, h => h.Kind == WealthSystem.Orrery && h.FromHouseId == theirs.Id);   // its orrery went back
        Assert.Contains(g.W.Heirlooms, h => h.Id == plate.Id); Assert.Contains(g.W.Heirlooms, h => h.Id == orrery2.Id);   // the others stay
        Assert.Equal(before, g.Regard(theirs)); Assert.False(g.Told($"{theirs.Name} called in the favour you owed it"));   // and your vote on its motion is not judged
        Assert.True(WealthSystem.FavourOwed(g.W, g.Mine.Id, third.Id));   // what you owe the other giver stands
    }

    [Fact]
    public void AGiftThatAsks_SaidToBuyNothing_DeclinesOnlyThatGiversFavour()
    {
        var (g, theirs, theirMotion, f) = Orrery();
        var other = Others(g).First(h => h.Id != theirs.Id); WealthSystem.OweFor(g.W, g.Mine.Id, other.Id); g.HeadOf(other).Flags.Add(WealthSystem.AsksFavourChar);   // a second orrery that year, from another house
        Answer(g, f, "keep_it_and_promise_nothing"); IVote(g, theirMotion, -1); int before = g.Regard(theirs); Session(g);
        Assert.False(WealthSystem.FavourOwed(g.W, g.Mine.Id, theirs.Id)); Assert.Equal(before, g.Regard(theirs));
        Assert.True(WealthSystem.FavourOwed(g.W, g.Mine.Id, other.Id));   // the other's favour was not declined
    }

    // ------------------------------------------------------------------ the one you did not speak for: say you will next time (LadderSystem.Word.cs)

    /// <summary>Bastien stands for praetor at the count of year five with nobody's word for him, and loses; the report comes, and you
    /// tell him you will speak for him next time.</summary>
    private static (G g, Character kin) SaidNextTime()
    {
        var g = Start(); var kin = Bastien(g); kin.Cursus.Remove(LadderSystem.Praetor); var offices = g.Senate.Offices;
        LadderSystem.Stand(g.W, offices, kin, LadderSystem.Praetor);   // his own doing
        foreach (var h in Others(g)) g.W.WorldCounters[OrderSystem.CanvassKey(h.Id)] = 500;   // every other house's candidate is far ahead
        Session(g); Assert.True(g.W.HasFlag(LadderSystem.HeldFlag)); Assert.Null(LadderSystem.Seat(g.W, kin.Id));
        foreach (var h in Others(g)) g.W.WorldCounters.Remove(OrderSystem.CanvassKey(h.Id));
        Assert.Contains(LadderSystem.BlamesChar, kin.Flags);
        var (f, cast) = Report(g, "the_one_you_did_not_speak_for", "kin"); Assert.Equal(kin.Id, cast.Id);
        g.W.News.Clear(); Answer(g, f, "say_you_will_next_time");
        Assert.True(LadderSystem.SpeakPromised(g.W, kin.Id));
        Assert.True(g.Told($"You told {kin.Name} you would speak for them the next time they stand."));
        Assert.True(g.Told($"they think {LadderSystem.SpeakKept:+0;-0} of you; let the count come with neither, and they think {-LadderSystem.SpeakBroken:+0;-0} of you"));
        return (g, kin);
    }
    /// <summary>The years to the next count of the ladder, Bastien putting his name down two years before it, as he wants to rise (or, if
    /// he does not, of his own doing then): the head is reminded once, then <paramref name="before"/>, then the count.</summary>
    private static void ToTheNextCount(G g, Character kin, Action? before = null)
    {
        var offices = g.Senate.Offices; g.W.News.Clear();
        while (LadderSystem.CountIn(g.W, offices) != 2) { Session(g); Only(g); LadderSystem.Tick(g.W, offices, Content.Policies); }
        if (!g.W.Candidacies.Any(c => c.CharacterId == kin.Id)) { LadderSystem.Stand(g.W, offices, kin, LadderSystem.Praetor); LadderSystem.Tick(g.W, offices, Content.Policies); }
        int count = g.W.Turn + LadderSystem.CountIn(g.W, offices); string reminder = $"{kin.Name} stands for praetor at the count in year {count}, and you told them you would speak for them.";
        Assert.True(g.Told(reminder)); g.W.News.Clear(); LadderSystem.Tick(g.W, offices, Content.Policies); Assert.False(g.Told(reminder));   // reminded, once
        before?.Invoke();
        while (g.W.Turn < count) { Session(g); Only(g); if (g.W.Turn < count) LadderSystem.Tick(g.W, offices, Content.Policies); }
        Assert.True(g.W.HasFlag(LadderSystem.HeldFlag));
    }

    [Fact]
    public void TheOneYouDidNotSpeakFor_SayYouWillNextTime_SpeakingForThemAtTheirNextCountKeepsIt_AcrossASaveAndALoad()
    {
        var (g0, kin0) = SaidNextTime(); var g = Reload(g0); var kin = g.W.Char(kin0.Id)!;
        int regard = 0;
        ToTheNextCount(g, kin, () =>
        {
            var back = new LadderCommand(g.Me.Id, LadderCommand.Back, kin.Id, "", g.Senate.Offices); Assert.True(g.Do(back), back.Refusal(g.Ctx));
            regard = g.W.GetRelationship(kin.Id, g.Me.Id)!.Disposition; g.W.News.Clear();
        });
        int won = LadderSystem.Seat(g.W, kin.Id) != null ? 6 : 0;   // one spoken for who wins knows what they owe besides (LadderSystem.Hold)
        Assert.Equal(regard + LadderSystem.SpeakKept + won, g.W.GetRelationship(kin.Id, g.Me.Id)!.Disposition);
        Assert.True(g.Told($"You kept your word to {kin.Name}: you spoke for them when they stood for praetor."));
        Assert.False(LadderSystem.SpeakPromised(g.W, kin.Id));
    }

    [Fact]
    public void TheOneYouDidNotSpeakFor_SayYouWillNextTime_NeitherWordNorCoinAtTheirNextCountBreaksIt_ItIsWrittenInTheirLife_AndYouMayNotAskThemNotToStand()
    {
        var (g, kin) = SaidNextTime(); int regard = 0;
        ToTheNextCount(g, kin, () =>
        {
            var dissuade = new LadderCommand(g.Me.Id, LadderCommand.Dissuade, kin.Id, "", g.Senate.Offices);
            Assert.Equal("you told them you would speak for them the next time they stood: asking them not to stand would break that", dissuade.Refusal(g.Ctx));
            regard = g.W.GetRelationship(kin.Id, g.Me.Id)!.Disposition; g.W.News.Clear();
        });
        Assert.Equal(regard - LadderSystem.SpeakBroken, g.W.GetRelationship(kin.Id, g.Me.Id)!.Disposition);   // the broken word, and no second blame for the same silence
        Assert.True(g.Told($"You told {kin.Name} you would speak for them the next time they stood, and they stood for praetor with neither your word among the houses nor your coin behind their canvass."));
        Assert.Contains(kin.Life!.Deeds, d => d.Text.Contains("had promised to speak for them, and did not"));
        Assert.False(LadderSystem.SpeakPromised(g.W, kin.Id));
    }

    [Fact]
    public void TheOneYouDidNotSpeakFor_SayYouWillNextTime_LapsesWithANotice_WhenTheyDie_OrNeverStandAgain()
    {
        var (g, kin) = SaidNextTime(); DynastySystem.Die(g.W, kin.Id); g.W.News.Clear(); LadderSystem.Tick(g.W, g.Senate.Offices, Content.Policies);
        Assert.True(g.Told("The one you promised to speak for at their next election is dead, and your word goes with them.")); Assert.False(LadderSystem.SpeakPromised(g.W, kin.Id));
        var (h, kin2) = SaidNextTime(); int since = h.W.Counter(LadderSystem.SpeakKey(kin2.Id)); int regard = h.W.GetRelationship(kin2.Id, h.Me.Id)!.Disposition;
        h.W.Turn = since + LadderSystem.SpeakYears; h.W.Candidacies.Clear(); h.W.News.Clear(); LadderSystem.Tick(h.W, h.Senate.Offices, Content.Policies);
        Assert.DoesNotContain(h.W.Candidacies, c => c.CharacterId == kin2.Id);   // five years from the next count: he has not put his name down
        Assert.True(h.Told($"{kin2.Name} has not stood for office in {Counted.Years(LadderSystem.SpeakYears)} since you promised to speak for them, so the word lapses, and nothing is owed either way."));
        Assert.Equal(regard, h.W.GetRelationship(kin2.Id, h.Me.Id)!.Disposition);
    }

    // ------------------------------------------------------------------ a vote for a vote: theirs counted first (SenateSystem.Tick's order)

    [Fact]
    public void AVoteForAVote_WithYourMotionDueAtTheSameSession_TheirsIsCountedFirst_SoAWordBrokenStrikesTheVoteTheyOwed()
    {
        var g = Start(); var theirs = Others(g)[0]; g.W.Relationship(g.HeadOf(theirs).Id, g.Me.Id).Disposition = 0;
        var mine = IMove(g); var theirMotion = g.Senate.Add(g.W, theirs.Id, "relief", Region(g));   // both moved in year four; yours first by id
        YearEnds(g); Only(g, mine, theirMotion);
        var (f, senator) = Report(g, "a_vote_for_a_vote", "senator"); Assert.Equal(g.HeadOf(theirs).Id, senator.Id); Answer(g, f, "give_your_word");
        // while the word waits for its count, the vote they owe for it is not called in elsewhere, nor named for money
        Assert.Equal(DealingsSystem.BargainPendingSays(theirs), new WealthCommand(g.Me.Id, WealthCommand.Favour, theirs.Id, WealthSystem.ForMoney).Refusal(g.Ctx));
        Assert.Equal(DealingsSystem.BargainPendingSays(theirs), new DealingsCommand(g.Me.Id, DealingsCommand.CallVote, theirs.Id, g.Tc.Dealings, theirMotion.Id).Refusal(g.Ctx));
        HardAgainst(g, mine, theirs); IVote(g, theirMotion, -1); Assert.True(string.CompareOrdinal(mine.Id, theirMotion.Id) < 0); g.W.News.Clear();
        Session(g);
        Assert.True(mine.Resolved && theirMotion.Resolved);
        Assert.True(g.Told($"{theirs.Name} will not give the vote it owed your house for a word you did not keep: it owes your house nothing now."));
        Assert.Equal(-1, mine.Votes[theirs.Id]); Assert.False(g.Told($"{theirs.Name} kept its word"));   // its vote on yours was its own
        // kept, the same order gives your motion its vote
        var h = Start(); var them = Others(h)[0]; h.W.Relationship(h.HeadOf(them).Id, h.Me.Id).Disposition = 0;
        var yours = IMove(h); var theirs2 = h.Senate.Add(h.W, them.Id, "relief", Region(h)); YearEnds(h); Only(h, yours, theirs2);
        var (f2, _) = Report(h, "a_vote_for_a_vote", "senator"); Answer(h, f2, "give_your_word"); HardAgainst(h, yours, them); h.W.News.Clear();
        Session(h);
        Assert.Equal(1, yours.Votes[them.Id]); Assert.True(h.Told($"{them.Name} kept its word")); Assert.True(h.Told($"You voted as you gave {them.Name} your word you would"));
    }

    [Fact]
    public void AVoteForAVote_AcrossAWarBeforeTheirCount_IsVoidBothWays_AndYourWordIsNotJudged()
    {
        var (g, theirs, _, theirMotion) = Bargained(); IVote(g, theirMotion, -1);
        g.Tc.Fleets.DeclareWar(g.W, g.Mine.Id, theirs.Id); int regard = g.Regard(theirs); g.W.News.Clear(); Session(g);
        Assert.True(theirMotion.Resolved); Assert.True(g.Told($"You are at war with {theirs.Name}: the vote for a vote is void both ways: {theirs.Name} owes your house no vote for it, and your vote on its motion was not judged."));
        Assert.False(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id)); Assert.DoesNotContain(g.W.Dealings, d => d.Kind == DealingsSystem.PledgeBrokenKind && d.FromHouseId == g.Mine.Id);
        Assert.Equal(regard, g.Regard(theirs));
    }

    [Fact]
    public void AVoteForAVote_AWarAndTheYearsEndBeforeTheirCount_TheVoteTheyOwedLapses_AndYourWordWithIt_SaidPlainly()
    {
        var (g, theirs, _, theirMotion) = Bargained(); g.Tc.Fleets.DeclareWar(g.W, g.Mine.Id, theirs.Id); g.W.News.Clear();
        WealthSystem.Tick(g.W);   // the year's end comes before their motion is counted
        Assert.False(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id)); Assert.Null(DealingsSystem.PledgeOn(g.W, g.Mine.Id, theirMotion.Id));
        Assert.True(g.Told($"You are at war with {theirs.Name}: the vote it owed your house will not be given, and the favour is gone. Your word on its motion, given for that vote, is void with it: vote on it as you please."));
        IVote(g, theirMotion, -1); Session(g); Assert.True(theirMotion.Resolved);
        Assert.DoesNotContain(g.W.Dealings, d => d.Kind == DealingsSystem.PledgeBrokenKind && d.FromHouseId == g.Mine.Id);
    }

    // ------------------------------------------------------------------ an orrery asks nothing of a vote already given (WealthSystem.GiftsThatAsk)

    [Fact]
    public void AGiftThatAsks_IsNotSentToYourHouse_WhenYourVoteOnTheGiversMotionIsSoldAlready()
    {
        var (g, theirs, _, theirMotion) = Sold(); Assert.NotNull(VoteMarks.Given(g.W, g.Mine.Id, theirMotion)); g.W.Heirlooms.Clear(); g.Mine.Legitimacy = 99; var head = g.HeadOf(theirs); int years = 0;
        while (!g.W.Heirlooms.Any(h => h.Kind == WealthSystem.Orrery && h.FromHouseId == theirs.Id) && years++ < 200)
        { g.W.Turn++; foreach (var h in g.W.Houses.Values) h.Treasury.Credits = 0; theirs.Treasury.Credits = 200; g.W.Relationship(head.Id, g.Me.Id).Disposition = 0; WealthSystem.Tick(g.W); }
        var orrery = Assert.Single(g.W.Heirlooms, h => h.Kind == WealthSystem.Orrery && h.FromHouseId == theirs.Id);
        Assert.NotEqual(g.Mine.Id, orrery.OfHouseId); Assert.False(WealthSystem.FavourOwed(g.W, g.Mine.Id, theirs.Id));   // the weightiest house passed over: its vote is not its own to give
    }

    // ------------------------------------------------------------------ the gold taken, then a word for the war (DealingsSystem.PledgeRefusal)

    [Fact]
    public void ForeignGold_TakenToVoteAgainstTheWar_TheHouseWindowRefusesAWordForIt()
    {
        var (g, war, offer) = GoldTaken(); var mover = g.W.House(war.ProposerHouseId)!;
        var pledge = new DealingsCommand(g.Me.Id, DealingsCommand.Pledge, mover.Id, g.Tc.Dealings, war.Id);
        Assert.Equal($"you took gold from {offer.NationName} {ForeignGoldSystem.AskSays(offer)}: a word for it would break that", pledge.Refusal(g.Ctx));
        Assert.False(g.Do(pledge)); Assert.Equal(-1, war.Votes[g.Mine.Id]); Assert.Empty(g.W.VotePledges);
    }

    [Fact]
    public void ForeignGold_TakenToStayAway_AWordAgainstTheWarIsTaken_AndKeepsBoth()
    {
        var g = Start(); var o = Others(g); var mover = o.First(h => g.Senate.MayPropose(g.W, h.Id)); var against = o.First(h => h.Id != mover.Id);
        g.W.Relationship(g.HeadOf(against).Id, g.Me.Id).Disposition = 30;
        var war = g.Senate.Add(g.W, mover.Id, "foreign_war", "thessar"); YearEnds(g); Only(g, war);
        foreach (var h in o.Where(h => h.Id != mover.Id && h.Id != against.Id)) war.Votes[h.Id] = 0;
        Leans(g, war, against, -6);
        var gold = Offer(g, war, ForeignGoldSystem.Away); Assert.True(g.Do(new ForeignGoldCommand(g.Me.Id, gold.Id, ForeignGoldSystem.Take)));
        var pledge = new DealingsCommand(g.Me.Id, DealingsCommand.Pledge, against.Id, g.Tc.Dealings, war.Id); Assert.Null(pledge.Refusal(g.Ctx)); Assert.True(g.Do(pledge));
        Assert.Equal(-1, war.Votes[g.Mine.Id]); int regard = g.Regard(against);
        Session(g); ForeignGoldSystem.Tick(g.W, Content.Nations, g.Senate, Content.Offices);
        Assert.True(war.Resolved); Assert.False(gold.Broken); Assert.Equal(regard + DealingsSystem.PledgeKeptRegard, g.Regard(against));   // the word kept, and the gold too
    }

    // ------------------------------------------------------------------ the repeal promised: a year with no voice, and three such years

    [Fact]
    public void TheActFallsOnAFriend_PromiseTheRepeal_AYearYourHouseHasNoVoiceInTheSenate_GivesAYearMore_AndAfterAsManyAsTheWindowHadTheWordLapses()
    {
        var (g, theirs, _, law, act, from) = RepealPromised(); int by = from + VoteMarks.RepealYears - 1;
        YearOf(g, from - 1); g.W.Turn = from;   // the first year of the window
        g.Me.Flags.Add(ChargeSystem.ExiledChar); Assert.False(g.Senate.MayPropose(g.W, g.Mine.Id));   // your head abroad: the house may move nothing
        int regard = g.Regard(theirs); g.W.News.Clear(); YearEnds(g);
        Assert.True(g.Told("your house had no voice in the Senate")); Assert.True(g.Told($"Your word to {theirs.Name} waits a year more: move it by year {by + 1}.")); Assert.Equal(regard, g.Regard(theirs));
        for (int y = from + 1; y < from + VoteMarks.RepealYears; y++) { Only(g); g.W.Turn = y; YearEnds(g); }   // three years without a voice in all
        Only(g); g.W.Turn = from + VoteMarks.RepealYears; g.W.News.Clear(); YearEnds(g);
        Assert.True(g.Told($"For {Counted.Years(VoteMarks.RepealYears)} the chamber could not hear the repeal of")); Assert.True(g.Told($"so your word to {theirs.Name} lapses, and nothing is owed either way."));
        Assert.Equal(regard, g.Regard(theirs)); Assert.Empty(DealingsSystem.Remembered(g.W, DealingsSystem.PledgeBrokenKind, g.Mine.Id, theirs.Id));
        Assert.DoesNotContain(g.W.WorldCounters.Keys, k => k.StartsWith(VoteMarks.RepealPrefix, StringComparison.Ordinal));
        _ = law; _ = act;
    }

    // ------------------------------------------------------------------ a word on a war its mover withdraws for gold (ForeignGoldSystem.Withdrawn)

    [Fact]
    public void AWordOnAWarItsMoverWithdrawsForGold_IsVoid_YouAreTold_NoRegardMoves_AndTheVoteOwedForItStands()
    {
        var g = Start(); var cities = g.W.Nation("thessar")!; cities.PurseOpened = true; cities.Purse = ForeignGoldSystem.Cap(Content.Nations.Def("thessar")!); cities.BoughtOffTurn = 0;
        var mover = Others(g).First(h => g.Senate.MayPropose(g.W, h.Id)); g.W.Relationship(g.HeadOf(mover).Id, g.Me.Id).Disposition = 0;
        g.HeadOf(mover).NatureTraits.Clear(); g.HeadOf(mover).NatureTraits.Add("Ruthless");   // for sale by nature: it takes the Cities' sum to withdraw
        var war = g.Senate.Add(g.W, mover.Id, "foreign_war", "thessar"); YearEnds(g); Only(g, war);
        var (f, senator) = Report(g, "a_vote_for_a_vote", "senator"); Assert.Equal(g.HeadOf(mover).Id, senator.Id); Answer(g, f, "give_your_word");
        Assert.NotNull(DealingsSystem.PledgeOn(g.W, g.Mine.Id, war.Id));
        foreach (var h in Others(g).Where(h => h.Id != mover.Id)) Leans(g, war, h, 1);   // the war would carry: the Cities offer its mover a sum to withdraw it
        int regard = g.Regard(mover); g.W.News.Clear();
        ForeignGoldSystem.Tick(g.W, Content.Nations, g.Senate, Content.Offices);
        Assert.DoesNotContain(g.W.Motions, m => m.Id == war.Id);   // withdrawn
        Assert.Null(DealingsSystem.PledgeOn(g.W, g.Mine.Id, war.Id));
        Assert.True(g.Told($"The motion you gave {mover.Name} your word on was struck before the count. Your word on it is void; the vote {mover.Name} owes your house for it still stands."));
        Assert.Equal(regard, g.Regard(mover)); Assert.True(WealthSystem.FavourOwed(g.W, mover.Id, g.Mine.Id));
    }

    // ------------------------------------------------------------------ your veto on what your word or a favour you owe is given on (VetoCommand)

    [Fact]
    public void YourVeto_IsNotForAMotionYouGaveYourWordOn_NorOneAtWhoseCountAFavourYouOweIsCalledIn()
    {
        var (g, colleague, _) = Colleagues();
        var theirs = Others(g).First(h => h.Id != colleague.HouseId && g.Senate.MayPropose(g.W, h.Id)); g.W.Relationship(g.HeadOf(theirs).Id, g.Me.Id).Disposition = 0;
        var m = Moves(g, theirs);   // before the Senate as the session that elected you closed
        var (f, senator) = Report(g, "a_vote_for_a_vote", "senator"); Assert.Equal(g.HeadOf(theirs).Id, senator.Id); Answer(g, f, "give_your_word");
        var veto = new VetoCommand(g.Me.Id, m.Id, g.Senate.Offices, g.Senate);
        Assert.Equal($"your vote on it is pledged to {theirs.Name}, for it: strike it and you break that word", veto.Refusal(g.Ctx)); Assert.False(g.Do(veto)); Assert.False(m.Vetoed);
        // a house you asked for coin on the house window moves a motion: the favour you owe it is called in at its count
        var other = Others(g).First(h => h.Id != colleague.HouseId && h.Id != theirs.Id && g.Senate.MayPropose(g.W, h.Id)); other.Treasury.Credits = 60; g.W.Relationship(g.HeadOf(other).Id, g.Me.Id).Disposition = 30;
        var ask = new DealingsCommand(g.Me.Id, DealingsCommand.AskCoin, other.Id, g.Tc.Dealings); Assert.True(g.Do(ask), ask.Refusal(g.Ctx));
        var n = g.Senate.Add(g.W, other.Id, "relief", Region(g)); Assert.Same(n, WealthSystem.CalledOn(g.W, g.Mine.Id, other.Id));
        Assert.Equal($"you owe {other.Name} a favour, and it is called in at this motion's count: strike it and you break that", new VetoCommand(g.Me.Id, n.Id, g.Senate.Offices, g.Senate).Refusal(g.Ctx));
    }

    // ------------------------------------------------------------------ gold against a favour you owe the war's mover (ForeignGoldSystem.AnswerRefusal)

    [Fact]
    public void ForeignGold_IsRefused_WhenAFavourYouOweTheWarsMoverIsCalledInAtItsCount_AndStayingAwayTellsWhatItBreaksWithTheBench()
    {
        var g = Start(); var mover = Others(g).First(h => g.Senate.MayPropose(g.W, h.Id)); mover.Treasury.Credits = 60; g.W.Relationship(g.HeadOf(mover).Id, g.Me.Id).Disposition = 30;
        var ask = new DealingsCommand(g.Me.Id, DealingsCommand.AskCoin, mover.Id, g.Tc.Dealings); Assert.True(g.Do(ask), ask.Refusal(g.Ctx));
        var war = g.Senate.Add(g.W, mover.Id, "foreign_war", "thessar"); YearEnds(g); Only(g, war); Assert.Same(war, WealthSystem.CalledOn(g.W, g.Mine.Id, mover.Id));
        var take = new ForeignGoldCommand(g.Me.Id, Offer(g, war, ForeignGoldSystem.Against).Id, ForeignGoldSystem.Take);
        Assert.Equal($"you owe {mover.Name} a favour, and it is called in at this motion's count: take this and you break that (its head would think -15 of you)", take.Refusal(g.Ctx));
        Assert.False(g.Do(take)); Assert.False(war.Votes.ContainsKey(g.Mine.Id));
        // gold to stay away, with your word to the bench's leader that your house would vote at the bench's next vote
        var (h, leader, mover2, _) = Promised();
        var war2 = h.Senate.Add(h.W, mover2.Id, "foreign_war", "thessar"); Only(h, war2); h.W.News.Clear();
        Assert.True(h.Do(new ForeignGoldCommand(h.Me.Id, Offer(h, war2, ForeignGoldSystem.Away).Id, ForeignGoldSystem.Take))); Assert.Equal(0, war2.Votes[h.Mine.Id]);
        Assert.True(h.Told($"your word to {leader.Name} that your house would vote at the next vote of the bench is broken: they think {FactionSystem.BreakWhipDisposition} the worse of you"));
    }

    // ------------------------------------------------------------------ a threat, an overlord: only a vote you cast

    [Fact]
    public void ThreatenedByAHouse_AVoteYouLeftAlone_CountedByYourHousesLean_IsNotJudged_AndOneYouCastIs()
    {
        var g = Start(); var them = g.W.House("aurelian")!; foreach (var f in g.W.Fleets.Values.Where(f => f.OwnerId == them.Id)) f.Strength = 40;
        g.Tc.Dealings.Threat(g.W, them, g.Mine);
        var m = Moves(g, them); Leans(g, m, g.Mine, -3); Assert.False(m.Votes.ContainsKey(g.Mine.Id)); int regard = g.Regard(them); g.W.News.Clear();
        Session(g);
        Assert.True(m.Resolved); Assert.Equal(-1, m.Votes[g.Mine.Id]); Assert.Equal(regard, g.Regard(them)); Assert.False(g.Told("which had told you what its fleets could do"));
        Only(g); var n = Moves(g, them, "relief", Region(g)); IVote(g, n, -1); g.W.News.Clear(); Session(g);
        Assert.Equal(regard - DealingsSystem.DefiedRegard, g.Regard(them)); Assert.True(g.Told($"You voted against the motion of {them.Name}, which had told you what its fleets could do."));
    }

    [Fact]
    public void TheHouseYouHoldAProvinceUnder_NotesOnlyAVoteYouCast_AndTellsYouWhenItDoes()
    {
        var g = Start(); var over = Others(g)[0];
        var world = g.W.Holdings.Values.Where(h => h.OwnerId == over.Id && h.Provinces.Count > 1).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        world.Provinces[1].HolderId = g.Mine.Id; g.W.ProvincesChanged(); Assert.Contains(ProvinceSystem.Overs(g.W, g.Mine.Id), o => o.Id == over.Id);
        var m = Moves(g, over); Leans(g, m, g.Mine, -3); int regard = g.Regard(over); g.W.News.Clear();
        Session(g);
        Assert.True(m.Resolved); Assert.Equal(-1, m.Votes[g.Mine.Id]); Assert.Equal(regard, g.Regard(over)); Assert.Equal(0, g.W.Counter(ProvinceSystem.DefiedKey(over.Id, g.Mine.Id)));
        Only(g); var n = Moves(g, over, "relief", Region(g)); IVote(g, n, -1); g.W.News.Clear(); Session(g);
        Assert.Equal(regard + SenateSystem.DefyRegard, g.Regard(over)); Assert.Equal(1, g.W.Counter(ProvinceSystem.DefiedKey(over.Id, g.Mine.Id)));
        Assert.True(g.Told($"You voted against {over.Name}, which you hold a province under, on what touched it: its head thinks -15 of you."));
    }

    // ------------------------------------------------------------------ the gates after a window command: a favour repaid across a war, the night of favours

    [Fact]
    public void AFavourRepaid_TheirVoice_IsClosedWhenYourHousesGoToWarBeforeItIsAnswered_WithTheReason()
    {
        var g = Start(); var theirs = Others(g)[1]; var debtor = g.HeadOf(theirs); debtor.Flags.Add("owes_you_a_favour");
        var (f, cast) = Report(g, "a_favour_repaid", "debtor"); Assert.Equal(debtor.Id, cast.Id); Assert.True(Open(g, f, "their_voice"));
        var war = new DeclareWarCommand(g.Me.Id, theirs.Id, Content.Fleets); Assert.True(g.Do(war), "the war is declared");   // on the window, before the report is answered
        Assert.False(Open(g, f, "their_voice")); Assert.Equal($"not while your house is at war with {theirs.Name}", Why(g, f, "their_voice"));   // said once, by its true cause
        Assert.Contains("owes_you_a_favour", debtor.Flags);   // the debt is not spent on a vote that would be void
        // blood instead of war: no_bargain_with_you is written again by the next thing you do on a window, and names the blood
        var h = Start(); var them = Others(h)[1]; var d2 = h.HeadOf(them); d2.Flags.Add("owes_you_a_favour"); var (f2, _) = Report(h, "a_favour_repaid", "debtor");
        Blooded(h, them); var gift = new DealingsCommand(h.Me.Id, DealingsCommand.Gift, Others(h).First(x => x.Id != them.Id).Id, h.Tc.Dealings, DealingsSystem.GiftSizes[0].ToString()); Assert.True(h.Do(gift), gift.Refusal(h.Ctx));
        Assert.False(Open(h, f2, "their_voice")); Assert.Contains($"not while there is blood between your house and {them.Name}", Why(h, f2, "their_voice"));
    }

    [Fact]
    public void AVoteForAVote_GiveYourWord_IsClosedByBloodThatCameAfterTheReport_WithTheReason_SoItsLogIsNeverFalse()
    {
        var g = Start(); var them = Others(g)[0]; g.W.Relationship(g.HeadOf(them).Id, g.Me.Id).Disposition = 0; TheyMove(g, them);
        var (f, senator) = Report(g, "a_vote_for_a_vote", "senator"); Assert.Equal(g.HeadOf(them).Id, senator.Id); Assert.True(Open(g, f, "give_your_word"));
        Blooded(g, them); var gift = new DealingsCommand(g.Me.Id, DealingsCommand.Gift, Others(g).First(x => x.Id != them.Id).Id, g.Tc.Dealings, DealingsSystem.GiftSizes[0].ToString()); Assert.True(g.Do(gift), gift.Refusal(g.Ctx));
        Assert.False(Open(g, f, "give_your_word")); Assert.Contains($"not while there is blood between your house and {them.Name}", Why(g, f, "give_your_word"));
    }

    [Fact]
    public void ACensure_CallingInFavours_IsClosedOnceTheFavoursAreSpentOnTheHouseWindow_AndOpensWhenOneIsGained()
    {
        var (g, a, b, _, censure, f) = Censured(); IVote(g, censure, -1);
        foreach (var h in new[] { a, b }) { var call = new DealingsCommand(g.Me.Id, DealingsCommand.CallVote, h.Id, g.Tc.Dealings, censure.Id); Assert.True(g.Do(call), call.Refusal(g.Ctx)); }
        Assert.Empty(DealingsSystem.CallableAgainst(g.W, g.Mine.Id)); Assert.False(g.W.HasFlag(VoteMarks.FavoursToCallFlag)); Assert.False(Open(g, f, "call_in_favours"));
        // a favour gained on the window this year: the choice opens
        var c = Small(g).First(x => x.Id != a.Id && x.Id != b.Id && x.Id != censure.ProposerHouseId); Gives(g, c);
        Assert.True(g.W.HasFlag(VoteMarks.FavoursToCallFlag)); Assert.True(Open(g, f, "call_in_favours"));
    }

    // ------------------------------------------------------------------ a word that goes against your bench, said as it is given

    [Fact]
    public void AWordGivenAgainstYourBenchsLeader_SaysWhatKeepingItCostsWithTheBench_AndTheCountChargesIt()
    {
        var (g, leader, mover, _, bench) = Seated(); g.W.Relationship(g.HeadOf(mover).Id, g.Me.Id).Disposition = 0;
        var m = TheyMove(g, mover); Leans(g, m, leader, -3);   // the bench's leader is against the mover's motion
        var (f, senator) = Report(g, "a_vote_for_a_vote", "senator"); Assert.Equal(g.HeadOf(mover).Id, senator.Id); g.W.News.Clear();
        Answer(g, f, "give_your_word");
        Assert.True(g.Told($"Keeping it costs more than that: {leader.Name}, which leads {bench.Name}, is against the motion as things stand, so your vote is against your bench, and at the count {g.HeadOf(leader).Name} thinks {FactionSystem.BreakWhipDisposition} the worse of you, your tie to the bench is weaker by {FactionSystem.BreakWhipAffinity}"));
        int regard = g.Regard(leader); Session(g);
        Assert.Equal(1, m.Votes[g.Mine.Id]); Assert.True(g.W.HasFlag("broke_the_whip")); Assert.Equal(regard - FactionSystem.BreakWhipDisposition, g.Regard(leader));   // as it said
    }

    // ------------------------------------------------------------------ the lesser promises: a struck bargain's notice, the bench's word when its leader's house falls, the ladder's end

    [Fact]
    public void AVoteForAVote_TheirMotionStruckByAConsul_TheNoticeSaysTheVoteTheyOweStillStands()
    {
        var (g, theirs, _, theirMotion) = Bargained(); g.W.News.Clear();
        ElectionSystem.Veto(g.W, g.Senate.Offices, Others(g).First(h => h.Id != theirs.Id).Id, theirMotion);
        Assert.True(g.Told($"Your word on it is void; the vote {theirs.Name} owes your house for it still stands.")); Assert.True(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id));
    }

    [Fact]
    public void TheEmptySeat_YourWordLapsesWithANotice_WhenTheLeadersHouseIsNoMore()
    {
        var (g, leader, _, _) = Promised(); var into = Others(g).First(h => h.Id != leader.Id); g.W.News.Clear();
        HouseSystem.Fold(g.W, leader, into, $"{leader.Name} is no more.");
        Assert.True(g.Told("The house you promised to vote with at the next vote of your bench is no more, and your word goes with it."));
        Assert.Equal(0, g.W.Counter(VoteMarks.StandKey(leader.Id)));
    }

    [Fact]
    public void TheLadderEndsUnderAThrone_YouAreToldWhatWillNeverBeCounted()
    {
        var (g, f) = Stands(); Answer(g, f, "let_them_win_it"); var kin = Bastien(g);
        var fund = new FundCampaignCommand(g.Me.Id, kin.Id, LadderSystem.Modest, g.Senate.Offices); Assert.True(g.Do(fund), fund.Refusal(g.Ctx));
        g.W.Era = "imperium"; g.W.News.Clear(); LadderSystem.Tick(g.W, g.Senate.Offices, Content.Policies);
        Assert.True(g.Told("A throne stands, and the magistrates will not be elected again: the ladder's counts are over, and nobody of your house stands at one now."));
        Assert.True(g.Told($"The coin put behind {kin.Name}'s canvass ({Money.Credits(LadderSystem.CampaignCost(LadderSystem.Modest))}) bought votes at a count that will not be held, and is gone."));
        g.W.News.Clear(); LadderSystem.Tick(g.W, g.Senate.Offices, Content.Policies); Assert.False(g.Told("A throne stands"));   // once
    }

    // ------------------------------------------------------------------ the leader's table: for the count of the motion due, and gone with it

    [Fact]
    public void TheLeadersTable_WithAnotherHousesMotionDue_IsForThatCount_NotForYourMotionMovedThisYear_AndGoesWithIt()
    {
        var (g, bench, members) = Leading(); var member = members[0];
        g.W.WorldCounters["senate_influence"] = 5; g.Mine.Treasury.Credits = 2000;
        var mover = Others(g).First(h => !bench.Members.Contains(h.Id)); var due = TheyMove(g, mover, "relief", Region(g)); YearOpens(g);   // moved at last year's session: due at this year's
        FactionSystem.Tick(g.W, Wt(g)); Assert.True(g.W.HasFlag("faction_leader")); Assert.True(due.ProposedTurn < g.W.Turn);
        Answer(g, ReportOf(g, "the_leaders_table"), "a_dinner");
        var levy = IMove(g); Only(g, due, levy); Assert.Equal(g.W.Turn, levy.ProposedTurn);   // your motion, moved the same year as the dinner
        Assert.Equal(FactionSystem.FollowedBy, By(g, due, member, "has fed its bench at the house's table"));
        Assert.Equal(0, By(g, levy, member, "has fed its bench at the house's table"));   // not counted at this session: the table is not for it
        YearEnds(g); Assert.True(due.Resolved); Assert.False(g.W.HasFlag(FactionSystem.FedFlag));   // the count that counted something: the table is spent with it
        YearOpens(g); Assert.Equal(0, By(g, levy, member, "has fed its bench at the house's table"));
        Session(g); Assert.True(levy.Resolved);
    }

    // ------------------------------------------------------------------ a house that owes you a vote, made the player's way: the_senate_listens/build_bloc

    [Fact]
    public void AHouseBroughtOverToYourSide_ItsConsulDoesNotStrikeYourMotion_AndItsVoteIsCounted()
    {
        var (g, consul, f) = Feast(); Answer(g, f, "stay_away"); var house = g.W.House(consul.HouseId)!;
        var bench = FactionSystem.Of(g.W, house.Id)!;
        if (bench.LeaderHouseId == house.Id || bench.LeaderHouseId == g.Mine.Id) bench.LeaderHouseId = bench.Members.First(id => id != house.Id && id != g.Mine.Id && g.W.House(id) != null);
        var leader = g.W.House(bench.LeaderHouseId)!;
        foreach (var h in Others(g)) g.W.Relationship(g.HeadOf(h).Id, g.Me.Id).Disposition = h.Id == house.Id ? 50 : -5;   // the consul's head thinks best of you
        g.W.WorldCounters["senate_influence"] = 0; VoteMarks.Docket(g.W, g.Senate, g.Mine.Id);
        Choose(g, ReportOf(g, "the_senate_listens"), "build_bloc");
        Assert.True(WealthSystem.FavourOwed(g.W, house.Id, g.Mine.Id)); Assert.True(g.Told($"{consul.Name} of {house.Name} has come over to your side"));
        var censure = IMove(g, "censure", leader.Id); Only(g, censure); HardAgainst(g, censure, house);
        Assert.Equal(1, g.Senate.Stance(g.W, censure, house.Id)); Session(g);
        Assert.False(censure.Vetoed); Assert.True(censure.Resolved); Assert.Equal(1, censure.Votes[house.Id]); Assert.False(WealthSystem.FavourOwed(g.W, house.Id, g.Mine.Id));
    }
}
