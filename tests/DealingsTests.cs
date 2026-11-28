using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Dealings between houses (docs/WEALTH.md, "Dealings between houses"): a gift of credits, a favour asked and called
/// in, a vote pledged, a threat, a loan at interest, and what lasts of each.</summary>
public class DealingsTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private sealed record Game(TurnController Tc, World W, House Mine, Character Me, CommandContext Ctx)
    {
        public DealingsSystem D => Tc.Dealings;
        public House H(string id) => W.House(id)!;
        public Character HeadOf(string id) => Economy.HeadOf(W, H(id))!;
        public DealingsCommand Cmd(string what, string houseId, string value = "", int rate = 0) => new(Me.Id, what, houseId, D, value, rate);
        public string? No(string what, string houseId, string value = "", int rate = 0) => Cmd(what, houseId, value, rate).Refusal(Ctx);
        public void Do(string what, string houseId, string value = "", int rate = 0) { var c = Cmd(what, houseId, value, rate); Assert.True(Tc.Executor.TryExecute(c, Ctx), c.Refusal(Ctx)); }
        /// <summary>What the head of that house thinks of the player.</summary>
        public int Regard(string id) => DealingsSystem.RegardOf(W, H(id), Mine);
        public void SetRegard(string id, int n) => W.Relationship(HeadOf(id).Id, Me.Id).Disposition = n;
        public bool Told(string part) => W.News.Any(n => n.Text.Contains(part));
        public int Remembered(string kind) => W.Dealings.Count(d => d.Kind == kind);
        public Motion Move(string proposer, string kind, string target) => Tc.Senate.Add(W, proposer, kind, target);
    }
    private static Game Start(ulong seed = 5, int credits = 100)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), seed); var tc = new TurnController(w, Content);
        var me = w.Protagonist!; var mine = w.House(me.HouseId)!; mine.Treasury.Credits = credits; w.Turn = 3;   // a few years in, so that "last year" is a year
        return new Game(tc, w, mine, me, new CommandContext(w, WorldBuilder.RngFor(w)));
    }
    private static void Year(TurnController tc) { foreach (var i in tc.BeginTurn()) { var ch = tc.Offer(i).FirstOrDefault(x => x.Available); if (ch != null) tc.Resolve(i, ch.Choice.Id); } tc.EndTurn(); }
    /// <summary>Every other house stays away from a motion but the one that moved it and the heaviest of the rest, which is against:
    /// a close thing, on which a vote is worth having.</summary>
    private static void Close(Game g, Motion m, string notThis = "")
    {
        var others = g.W.Houses.Values.Where(h => !World.Outsider(h.Id) && h.Id != g.Mine.Id && h.Id != m.ProposerHouseId).OrderByDescending(h => g.Tc.Senate.Weight(g.W, h.Id)).ThenBy(h => h.Id, StringComparer.Ordinal).ToList();
        foreach (var h in others) m.Votes[h.Id] = 0;
        m.Votes[m.ProposerHouseId] = 1; m.Votes[others.First(h => h.Id != notThis).Id] = -1;
    }
    /// <summary>The Senate counts a motion now.</summary>
    private static void Count(Game g, Motion m) { m.ProposedTurn = g.W.Turn - 1; g.Tc.Senate.Tick(g.W); Assert.True(m.Resolved && !m.Vetoed, "the motion was not counted"); }

    [Fact]
    public void Only_the_head_deals_and_only_with_a_house_of_the_Reach_that_is_not_its_own()
    {
        var g = Start(); var kin = g.W.LivingCharacters().First(c => c.HouseId == g.Mine.Id && c.Id != g.Me.Id);
        if (g.W.House(PirateSystem.HouseId) == null) g.W.Houses[PirateSystem.HouseId] = new House { Id = PirateSystem.HouseId, Name = "the raiders" };
        foreach (var what in DealingsCommand.All)
        {
            Assert.Equal("only the head of a house deals with another in its name", new DealingsCommand(kin.Id, what, "marrow", g.D, "5", 10).Refusal(g.Ctx));
            Assert.Equal("no such house", g.No(what, g.Mine.Id, "5", 10)); Assert.Equal("no such house", g.No(what, "nobody", "5", 10));
            Assert.Contains("not a house of the Reach", g.No(what, PirateSystem.HouseId, "5", 10));
            Assert.False(g.Tc.Executor.TryExecute(g.Cmd(what, "nobody", "5", 10), g.Ctx));
        }
        Assert.Equal("no such dealing", g.No("flattery", "marrow"));
    }

    [Fact]
    public void A_gift_of_credits_buys_regard_by_what_it_is_to_the_house_that_takes_it()
    {
        var g = Start(); g.SetRegard("ashgrove", -25); g.SetRegard("lucent", 0);
        int poor = g.D.Income(g.W, g.H("ashgrove")), rich = g.D.Income(g.W, g.H("lucent")); Assert.True(rich > poor, $"{rich} {poor}");
        // the same sum is more to the poorer house, and a larger sum is more to either
        Assert.True(g.D.GiftRegard(g.W, g.H("ashgrove"), 5) > g.D.GiftRegard(g.W, g.H("lucent"), 5));
        Assert.True(g.D.GiftRegard(g.W, g.H("lucent"), 15) > g.D.GiftRegard(g.W, g.H("lucent"), 2));
        Assert.Equal(Math.Clamp(1 + 5 * 100 / poor / DealingsSystem.GiftPctAPoint, 1, DealingsSystem.GiftRegardMost), g.D.GiftRegard(g.W, g.H("ashgrove"), 5));
        Assert.Contains("a year", g.D.GiftSays(g.W, g.H("ashgrove"), 5));

        int had = g.H("ashgrove").Treasury.Credits, gain = g.D.GiftRegard(g.W, g.H("ashgrove"), 5);
        var kin = g.W.LivingCharacters().First(c => c.HouseId == "ashgrove" && c.Id != g.HeadOf("ashgrove").Id && c.Age >= DynastySystem.AgeOfMajority); int kinThought = g.W.Relationship(kin.Id, g.Me.Id).Disposition;
        g.Do(DealingsCommand.Gift, "ashgrove", "5");
        Assert.Equal(kinThought + gain / DealingsSystem.KinHearPart, g.W.Relationship(kin.Id, g.Me.Id).Disposition);   // the rest of the house hears of it, by half
        Assert.Equal(95, g.Mine.Treasury.Credits); Assert.Equal(had + 5, g.H("ashgrove").Treasury.Credits); Assert.Equal(-25 + gain, g.Regard("ashgrove"));
        Assert.False(WealthSystem.FavourOwed(g.W, "ashgrove", g.Mine.Id));   // 500 credits is under what a favour can be called in for
        Assert.True(g.Told("takes your gift of 500 credits")); Assert.Equal(1, g.Remembered(DealingsSystem.GiftKind));
        Assert.Contains(g.D.Lasting(g.W, g.Mine, g.H("ashgrove")), l => l.text.Contains("You sent it 500 credits in year 3"));
        Assert.Equal("you have sent them a gift this year", g.No(DealingsCommand.Gift, "ashgrove", "2"));
        g.W.Turn++; Assert.Null(g.No(DealingsCommand.Gift, "ashgrove", "2"));

        // a large gift is a favour owed
        Assert.True(g.D.GiftOwes(g.W, g.H("lucent"), 15)); Assert.False(g.D.GiftOwes(g.W, g.H("lucent"), 5));
        g.Do(DealingsCommand.Gift, "lucent", "15");
        Assert.True(WealthSystem.FavourOwed(g.W, "lucent", g.Mine.Id)); Assert.True(g.Told("House Lucent owes your house a favour"));
        Assert.Contains(g.D.Lasting(g.W, g.Mine, g.H("lucent")), l => l.text.StartsWith("It owes you a favour") && l.tone == "good");
        Assert.Contains(WealthSystem.OweMe(g.W, g.Mine.Id), h => h.Id == "lucent");
    }

    [Fact]
    public void A_gift_is_refused_by_a_head_who_hates_you_or_is_at_war_and_by_an_empty_treasury()
    {
        var g = Start(credits: 3);
        Assert.Equal("not a sum that is sent", g.No(DealingsCommand.Gift, "marrow", "7"));
        Assert.Equal("needs 500 credits and the house has 300", g.No(DealingsCommand.Gift, "marrow", "5"));
        g.SetRegard("marrow", WealthSystem.RefuseGiftAt); Assert.Equal($"{g.HeadOf("marrow").Name} will take nothing from you", g.No(DealingsCommand.Gift, "marrow", "2"));
        g.SetRegard("marrow", WealthSystem.RefuseGiftAt + 1); Assert.Null(g.No(DealingsCommand.Gift, "marrow", "2"));
        g.W.Wars.Add(new War { HouseA = g.Mine.Id, HouseB = "marrow", Since = g.W.Turn });
        Assert.Contains("is at war with you", g.No(DealingsCommand.Gift, "marrow", "2"));
        Assert.False(g.Tc.Executor.TryExecute(g.Cmd(DealingsCommand.Gift, "marrow", "2"), g.Ctx)); Assert.Equal(3, g.Mine.Treasury.Credits);   // sent back: nothing moved
    }

    [Fact]
    public void A_favour_is_asked_of_any_house_whose_head_thinks_well_enough_and_leaves_one_owed()
    {
        var g = Start(); g.SetRegard("marrow", 19); g.H("marrow").Treasury.Credits = 30;
        Assert.Equal(DealingsSystem.AskRegard, DealingsSystem.HearsAt(g.W, g.Mine, g.H("marrow")));
        Assert.Equal($"{g.HeadOf("marrow").Name} thinks +19 of you, and hears a request at +20 (1 short)", g.No(DealingsCommand.AskCoin, "marrow"));
        // a patron hears sooner than a stranger
        g.Mine.PatronId = "marrow"; Assert.Equal(AskAbove.AskAt, DealingsSystem.HearsAt(g.W, g.Mine, g.H("marrow"))); Assert.Null(g.No(DealingsCommand.AskCoin, "marrow")); g.Mine.PatronId = "";
        g.SetRegard("marrow", 20);
        g.H("marrow").Treasury.Credits = 11; Assert.Contains("has no coin to spare: it holds 1,100", g.No(DealingsCommand.AskCoin, "marrow"));
        g.H("marrow").Treasury.Credits = 30; Assert.Contains("gives you 500 credits", g.D.AskSays(g.W, g.H("marrow"), DealingsSystem.Coin));
        g.Do(DealingsCommand.AskCoin, "marrow");
        Assert.Equal(105, g.Mine.Treasury.Credits); Assert.Equal(25, g.H("marrow").Treasury.Credits); Assert.True(WealthSystem.FavourOwed(g.W, g.Mine.Id, "marrow"));
        Assert.Contains("you owe House Marrow a favour already", g.No(DealingsCommand.AskCoin, "marrow"));
        Assert.Contains(g.D.Lasting(g.W, g.Mine, g.H("marrow")), l => l.text.StartsWith("You owe it a favour, from year 3") && l.tone == "warn");
        g.W.WorldCounters.Remove(WealthSystem.FavourKey(g.Mine.Id, "marrow"));
        Assert.Contains("not again before year 5", g.No(DealingsCommand.AskCoin, "marrow"));
        g.W.Wars.Add(new War { HouseA = g.Mine.Id, HouseB = "marrow", Since = g.W.Turn }); Assert.Equal("you are at war with them", g.No(DealingsCommand.AskCoin, "marrow"));
    }

    [Fact]
    public void A_vote_is_asked_as_a_favour_on_a_motion_the_asker_has_moved_or_declared_on()
    {
        var g = Start(); g.SetRegard("marrow", 30); var m = g.Move("aurelian", "censure", "oriel");
        Assert.Equal("no such motion before the Senate", g.No(DealingsCommand.AskVote, "marrow", "motion_none"));
        Assert.Contains("declare your own vote on it first", g.No(DealingsCommand.AskVote, "marrow", m.Id));
        Assert.True(g.Tc.Executor.TryExecute(new VoteCommand(g.Me.Id, m.Id, -1), g.Ctx));
        g.SetRegard("aurelian", 30); Assert.Contains("their own motion: they will not vote against it", g.No(DealingsCommand.AskVote, "aurelian", m.Id));
        g.Do(DealingsCommand.AskVote, "marrow", m.Id);
        Assert.Equal(-1, m.Votes["marrow"]); Assert.True(WealthSystem.FavourOwed(g.W, g.Mine.Id, "marrow"));
        // the vote stands at the count
        Count(g, m); Assert.Equal(-1, m.Votes["marrow"]);

        // on the asker's own motion they vote for it
        var g2 = Start(); g2.SetRegard("marrow", 30); var own = g2.Move(g2.Mine.Id, "censure", "oriel");
        g2.Do(DealingsCommand.AskVote, "marrow", own.Id); Assert.Equal(1, own.Votes["marrow"]);
        Assert.Equal("they have declared that way already", g2.D.VoteRefusal(g2.W, g2.Mine, g2.H("marrow"), own.Id));
        // a house will not be asked to vote against itself
        var struck = g2.Move(g2.Mine.Id, "censure", "lucent"); g2.SetRegard("lucent", 40);
        Assert.Contains("names their own house", g2.No(DealingsCommand.AskVote, "lucent", struck.Id));
    }

    [Fact]
    public void A_favour_owed_is_called_in_for_a_vote_from_the_house_that_owes_it()
    {
        var g = Start(); var own = g.Move(g.Mine.Id, "censure", "oriel"); var m = g.Move("lucent", "levy", "");
        Assert.Equal("they owe you nothing", g.No(DealingsCommand.CallVote, "ashgrove", m.Id));
        WealthSystem.OweFor(g.W, "ashgrove", g.Mine.Id); g.SetRegard("ashgrove", -30);   // a favour is owed whatever its head thinks
        Assert.Equal("no such motion before the Senate", g.No(DealingsCommand.CallVote, "ashgrove", ""));
        // on your own motion it votes for it already, and is spent only if it would not have of its own accord (WealthSystem.VotesFor)
        Assert.Equal("it votes for your motion already: the favour is kept for one it would not vote for", g.No(DealingsCommand.CallVote, "ashgrove", own.Id));
        m.Votes[g.Mine.Id] = -1;   // you have declared against their levy
        int called = g.W.Counter(WealthSystem.FavoursCalledKey);
        g.Do(DealingsCommand.CallVote, "ashgrove", m.Id);
        Assert.Equal(-1, m.Votes["ashgrove"]); Assert.False(WealthSystem.FavourOwed(g.W, "ashgrove", g.Mine.Id)); Assert.Equal(called + 1, g.W.Counter(WealthSystem.FavoursCalledKey));
        Assert.Equal("they owe you nothing", g.No(DealingsCommand.CallVote, "ashgrove", m.Id));
        // the two ways it could already be called in still stand, and are what the house's page offers beside this one
        WealthSystem.OweFor(g.W, "lucent", g.Mine.Id); g.H("lucent").Treasury.Credits = 40;
        Assert.True(g.Tc.Executor.TryExecute(new WealthCommand(g.Me.Id, WealthCommand.Favour, "lucent", WealthSystem.ForMoney), g.Ctx));
        Assert.Equal(100 + WealthSystem.FavourCredits, g.Mine.Treasury.Credits);
    }

    [Fact]
    public void A_favour_the_player_owes_is_called_in_on_the_creditors_motion_and_the_player_is_told()
    {
        foreach (bool keeps in new[] { true, false })
        {
            var g = Start(); WealthSystem.OweFor(g.W, g.Mine.Id, "aurelian"); g.SetRegard("aurelian", 10);
            var m = g.Move("aurelian", "censure", "oriel");
            Assert.True(g.Told("House Aurelian has moved a motion, and you owe it a favour"));
            Assert.True(g.Tc.Executor.TryExecute(new VoteCommand(g.Me.Id, m.Id, keeps ? 1 : -1), g.Ctx));
            Count(g, m);
            Assert.False(WealthSystem.FavourOwed(g.W, g.Mine.Id, "aurelian"));
            Assert.Equal(10 + (keeps ? WealthSystem.FavourKept : -WealthSystem.FavourBroken), g.Regard("aurelian"));
            Assert.Equal(1, g.Remembered(keeps ? DealingsSystem.FavourKeptKind : DealingsSystem.FavourBrokenKind));
            Assert.True(g.Told(keeps ? "and you voted for it" : "and you did not vote for it"));
            Assert.Contains(g.D.Lasting(g.W, g.Mine, g.H("aurelian")), l => l.text == (keeps ? "Favours it called in: honoured 1 time, refused 0 times." : "Favours it called in: honoured 0 times, refused 1 time."));
        }
    }

    [Fact]
    public void A_creditor_short_of_coin_calls_the_favour_in_for_coin_and_is_paid_refused_or_left_unanswered()
    {
        foreach (string answer in new[] { DealingsCommand.Honour, DealingsCommand.Break, "" })
        {
            var g = Start(); g.SetRegard("orlov", 10); var orlov = g.H("orlov");
            Assert.Equal("they have asked nothing of you", g.No(DealingsCommand.Honour, "orlov")); Assert.Equal("they have asked nothing of you", g.No(DealingsCommand.Break, "orlov"));
            g.W.WorldCounters[WealthSystem.FavourKey(g.Mine.Id, "orlov")] = g.W.Turn;
            orlov.Treasury.Credits = DealingsSystem.CallsCoinAt; g.D.Tick(g.W); Assert.False(DealingsSystem.Called(g.W, g.Mine.Id, "orlov"));   // not short of coin, and the favour is this year's
            orlov.Treasury.Credits = 2; g.D.Tick(g.W); Assert.False(DealingsSystem.Called(g.W, g.Mine.Id, "orlov"));
            g.W.Turn++; g.D.Tick(g.W);
            Assert.True(DealingsSystem.Called(g.W, g.Mine.Id, "orlov")); Assert.True(g.Told("calls in the favour you owe: the house is short of coin, and asks 1,500 credits"));
            Assert.Contains(g.D.Lasting(g.W, g.Mine, orlov), l => l.text.Contains("it has called it in: it asks 1,500 credits"));
            g.D.Tick(g.W); Assert.True(DealingsSystem.Called(g.W, g.Mine.Id, "orlov"));   // it stands for the year
            if (answer == DealingsCommand.Honour)
            {
                g.Mine.Treasury.Credits = 10; Assert.Equal("needs 1,500 credits and the house has 1,000", g.No(DealingsCommand.Honour, "orlov")); g.Mine.Treasury.Credits = 100;
                int theirs = orlov.Treasury.Credits;
                g.Do(DealingsCommand.Honour, "orlov");
                Assert.Equal(85, g.Mine.Treasury.Credits); Assert.Equal(theirs + 15, orlov.Treasury.Credits); Assert.Equal(10 + WealthSystem.FavourKept, g.Regard("orlov")); Assert.Equal(1, g.Remembered(DealingsSystem.FavourKeptKind));
            }
            else if (answer == DealingsCommand.Break)
            {
                g.Do(DealingsCommand.Break, "orlov");
                Assert.Equal(100, g.Mine.Treasury.Credits); Assert.Equal(10 - WealthSystem.FavourBroken, g.Regard("orlov")); Assert.Equal(1, g.Remembered(DealingsSystem.FavourBrokenKind));
            }
            else
            {
                g.W.Turn++; g.D.Tick(g.W);
                Assert.Equal(10 - WealthSystem.FavourBroken, g.Regard("orlov")); Assert.True(g.Told("and had no answer")); Assert.Equal(1, g.Remembered(DealingsSystem.FavourBrokenKind));
            }
            Assert.False(WealthSystem.FavourOwed(g.W, g.Mine.Id, "orlov")); Assert.False(DealingsSystem.Called(g.W, g.Mine.Id, "orlov"));
            Assert.Equal("they have asked nothing of you", g.No(DealingsCommand.Honour, "orlov"));
        }
    }

    [Fact]
    public void A_vote_pledged_and_kept_is_a_favour_owed_and_one_broken_costs_regard_and_is_remembered()
    {
        var g = Start(); g.SetRegard("aurelian", 0); var m = g.Move("aurelian", "censure", "oriel");
        Assert.Equal(1, g.D.Wants(g.W, m, g.H("aurelian")));
        // a side that is far ahead without the player's seats does not need the player's word
        int seats = g.Tc.Senate.Weight(g.W, g.Mine.Id);
        foreach (var h in g.W.Houses.Values.Where(h => !World.Outsider(h.Id) && h.Id != g.Mine.Id)) m.Votes[h.Id] = 1;
        int lead = g.D.LeadWithout(g.W, m, 1, g.Mine.Id); Assert.True(lead > DealingsSystem.PledgeLeadTimes * seats);
        Assert.Equal($"its side stands {lead} ahead without your {Counted.Seats(seats)}, and it does not need your word: it would at {DealingsSystem.PledgeLeadTimes * seats} or under", g.No(DealingsCommand.Pledge, "aurelian", m.Id));
        Close(g, m); Assert.True(g.D.LeadWithout(g.W, m, 1, g.Mine.Id) <= DealingsSystem.PledgeLeadTimes * seats);
        Assert.Equal("no such motion before the Senate", g.No(DealingsCommand.Pledge, "aurelian", "motion_none"));
        if (g.W.Houses.Values.FirstOrDefault(h => !World.Outsider(h.Id) && h.Id != g.Mine.Id && g.D.Wants(g.W, m, h) == 0) is { } cold)
        { g.SetRegard(cold.Id, 0); Assert.Equal($"{cold.Name} does not care enough how it goes to want your word on it", g.No(DealingsCommand.Pledge, cold.Id, m.Id)); }
        Assert.Contains("Vote so at the count and House Aurelian owes you a favour", g.D.PledgeSays(g.W, m, g.H("aurelian")));
        g.Do(DealingsCommand.Pledge, "aurelian", m.Id);
        Assert.Equal(1, m.Votes[g.Mine.Id]); Assert.Single(g.W.VotePledges); Assert.Equal("your vote on it is pledged to House Aurelian already", g.No(DealingsCommand.Pledge, "aurelian", m.Id));
        Assert.Contains(g.D.Lasting(g.W, g.Mine, g.H("aurelian")), l => l.text.StartsWith("Your vote is pledged to it, for this motion"));
        Count(g, m);
        Assert.Empty(g.W.VotePledges); Assert.True(WealthSystem.FavourOwed(g.W, "aurelian", g.Mine.Id)); Assert.Equal(DealingsSystem.PledgeKeptRegard, g.Regard("aurelian"));
        Assert.True(g.Told("You voted as you gave House Aurelian your word you would")); Assert.Equal(1, g.Remembered(DealingsSystem.PledgeKeptKind));
        var next = g.Move("aurelian", "censure", "valmont"); Close(g, next);
        Assert.Equal("House Aurelian owes you a favour already, and will not owe two", g.No(DealingsCommand.Pledge, "aurelian", next.Id));

        // broken: the vote is changed before the count
        var b = Start(); b.SetRegard("aurelian", 0); var bm = b.Move("aurelian", "censure", "oriel"); Close(b, bm);
        b.Do(DealingsCommand.Pledge, "aurelian", bm.Id); Assert.True(b.Tc.Executor.TryExecute(new VoteCommand(b.Me.Id, bm.Id, -1), b.Ctx));
        Count(b, bm);
        Assert.False(WealthSystem.FavourOwed(b.W, "aurelian", b.Mine.Id)); Assert.Equal(-DealingsSystem.PledgeBrokenRegard, b.Regard("aurelian"));
        Assert.True(b.Told("You gave House Aurelian your word on a vote and did not keep it")); Assert.Equal(1, b.Remembered(DealingsSystem.PledgeBrokenKind));
        var again = b.Move("aurelian", "censure", "valmont"); Close(b, again);
        Assert.Equal($"you broke your word to it in year {b.W.Turn}: it takes no pledge of yours before year {b.W.Turn + DealingsSystem.PledgeMemoryYears}", b.No(DealingsCommand.Pledge, "aurelian", again.Id));
        Assert.Contains(b.D.Lasting(b.W, b.Mine, b.H("aurelian")), l => l.text == "Your word to it on a vote: kept 0 times, broken 1 time." && l.tone == "warn");
        b.W.Turn += DealingsSystem.PledgeMemoryYears; Assert.Null(b.No(DealingsCommand.Pledge, "aurelian", again.Id));
    }

    [Fact]
    public void A_pledge_is_not_taken_on_ones_own_motion_nor_by_a_head_who_hates_and_lapses_if_the_motion_is_never_counted()
    {
        var g = Start(); var own = g.Move(g.Mine.Id, "censure", "oriel"); var mine = g.Move("aurelian", "censure", g.Mine.Id); var m = g.Move("lucent", "censure", "oriel");
        g.SetRegard("marrow", 0); g.SetRegard("aurelian", 0);
        Assert.Equal("it is your own motion: nobody doubts how you will vote", g.No(DealingsCommand.Pledge, "marrow", own.Id));
        Assert.Equal("the motion names your own house: nobody doubts how you will vote", g.No(DealingsCommand.Pledge, "aurelian", mine.Id));
        g.SetRegard("lucent", -40); Assert.Contains("will take nothing from you", g.No(DealingsCommand.Pledge, "lucent", m.Id));
        g.SetRegard("lucent", 0); Close(g, m); g.Do(DealingsCommand.Pledge, "lucent", m.Id);
        m.Resolved = true; g.D.Tick(g.W);   // struck from the docket without a count
        Assert.Empty(g.W.VotePledges); Assert.Equal(0, g.Remembered(DealingsSystem.PledgeKeptKind) + g.Remembered(DealingsSystem.PledgeBrokenKind));
    }

    [Fact]
    public void A_threat_needs_three_hulls_to_one_and_cows_a_house_which_then_stays_away_from_votes_against_you()
    {
        var g = Start(); int hulls = FleetSystem.StrengthOf(g.W, g.Mine.Id), their = FleetSystem.StrengthOf(g.W, "aurelian"); Assert.True(hulls >= 3 && their * 3 > hulls);
        Assert.Equal($"they are not afraid of you: it takes 3 hulls to their one, and you have {hulls} to their {their} ({3 * their - hulls} short)", g.No(DealingsCommand.Threat, "aurelian"));
        Assert.Equal(0, FleetSystem.StrengthOf(g.W, "ashgrove")); g.SetRegard("ashgrove", -30);
        // before: lobbied hard against the player's motion it leans against it, and lobbied for a censure of the player it leans for it
        var own = g.Move(g.Mine.Id, "censure", "oriel"); own.Lobby["ashgrove"] = -12; var struck = g.Move("aurelian", "censure", g.Mine.Id); struck.Lobby["ashgrove"] = 12;
        Assert.True(g.Tc.Senate.Lean(g.W, own, "ashgrove") < 0); Assert.True(g.Tc.Senate.Lean(g.W, struck, "ashgrove") > 0);
        Assert.Contains("is afraid of you", g.D.ThreatSays(g.W, g.Mine, g.H("ashgrove")));
        g.Do(DealingsCommand.Threat, "ashgrove");
        Assert.Equal(-30 + DealingsSystem.ThreatRegard, g.Regard("ashgrove")); Assert.True(PolitySystem.Cowed(g.W, g.H("ashgrove"), g.Mine)); Assert.Equal(1, g.Remembered(DealingsSystem.ThreatKind));
        Assert.Equal("they are afraid of you already", g.No(DealingsCommand.Threat, "ashgrove"));
        Assert.Equal(0, g.Tc.Senate.Lean(g.W, own, "ashgrove")); Assert.Equal(0, g.Tc.Senate.Lean(g.W, struck, "ashgrove"));
        Assert.Contains(g.Tc.Senate.Why(g.W, own, "ashgrove"), x => x.why == "it is afraid of " + g.Mine.Name && x.by > 0);
        Assert.Contains(g.Tc.Senate.Why(g.W, struck, "ashgrove"), x => x.why == "it is afraid of " + g.Mine.Name && x.by < 0);
        Assert.Equal(g.Tc.Senate.Lean(g.W, own, "ashgrove"), g.Tc.Senate.Why(g.W, own, "ashgrove").Sum(x => x.by));
        Assert.Contains(g.D.Lasting(g.W, g.Mine, g.H("ashgrove")), l => l.text.StartsWith($"It has been afraid of you since year 3, and is until year {3 + DealingsSystem.CowedYears}") && l.text.Contains("does not vote against what you move"));
        // where it would have voted with the player anyway, nothing is changed
        var third = g.Move(g.Mine.Id, "censure", "valmont"); third.Lobby["ashgrove"] = 12; Assert.True(g.Tc.Senate.Lean(g.W, third, "ashgrove") > 0);
        Count(g, own); Assert.Equal(0, own.Votes["ashgrove"]);   // and so it votes: it stays away
        // the fear lasts only while the player keeps twice its hulls, and its years and no longer
        var fleets = g.W.Fleets.Values.Where(f => f.OwnerId == g.Mine.Id).ToList(); foreach (var f in fleets) f.LaidUp = true;
        Assert.False(PolitySystem.Cowed(g.W, g.H("ashgrove"), g.Mine)); Assert.Contains(g.D.Lasting(g.W, g.Mine, g.H("ashgrove")), l => l.text.Contains("It is not afraid now"));
        foreach (var f in fleets) f.LaidUp = false;
        g.W.Turn += DealingsSystem.CowedYears + 1; g.D.Tick(g.W);
        Assert.False(g.W.WorldCounters.ContainsKey(PolitySystem.CowedKey("ashgrove", g.Mine.Id))); Assert.Null(g.No(DealingsCommand.Threat, "ashgrove"));
    }

    [Fact]
    public void A_threat_against_a_client_brings_its_patrons_displeasure_and_none_is_made_in_a_war_or_against_ones_own_client()
    {
        var g = Start(); g.H("ashgrove").PatronId = "lucent"; g.SetRegard("lucent", 20);
        Assert.Equal("lucent", DealingsSystem.Protector(g.W, g.H("ashgrove"), g.Mine)?.Id); Assert.Contains("It is a client of House Lucent", g.D.ThreatSays(g.W, g.Mine, g.H("ashgrove")));
        g.Do(DealingsCommand.Threat, "ashgrove");
        Assert.Equal(20 - DealingsSystem.PatronDispleasure, g.Regard("lucent")); Assert.True(g.Told("House Lucent is the patron of House Ashgrove"));
        Assert.True(PolitySystem.Cowed(g.W, g.H("ashgrove"), g.Mine));
        g.H("brannock").PatronId = g.Mine.Id; Assert.Equal("it is your own client, and does as you wish already", g.No(DealingsCommand.Threat, "brannock"));
        g.W.Wars.Add(new War { HouseA = g.Mine.Id, HouseB = "cask", Since = g.W.Turn }); Assert.Contains("you are at war with them already", g.No(DealingsCommand.Threat, "cask"));
    }

    [Fact]
    public void A_hard_head_with_the_hulls_threatens_a_house_it_thinks_ill_of_and_the_player_is_told_and_may_defy_it()
    {
        var g = Start(); var hard = g.HeadOf("aurelian"); hard.NatureTraits.Add("Ruthless");
        foreach (var f in g.W.Fleets.Values.Where(f => f.OwnerId == "aurelian")) f.Strength = 40;
        g.W.Relationship(hard.Id, g.Me.Id).Disposition = -80; g.W.Relationship(hard.Id, g.HeadOf("ashgrove").Id).Disposition = -30;
        for (int y = 0; y < 60 && !(PolitySystem.Cowed(g.W, g.Mine, g.H("aurelian")) && PolitySystem.Cowed(g.W, g.H("ashgrove"), g.H("aurelian"))); y++) { g.W.Turn++; g.D.Tick(g.W); }
        Assert.True(PolitySystem.Cowed(g.W, g.Mine, g.H("aurelian"))); Assert.True(PolitySystem.Cowed(g.W, g.H("ashgrove"), g.H("aurelian")));
        Assert.True(g.Told("lets you understand what its fleets could do to yours")); Assert.True(g.W.Counter(DealingsSystem.ThreatsKey) >= 2);
        Assert.Contains(g.D.Lasting(g.W, g.Mine, g.H("aurelian")), l => l.text.StartsWith("It threatened you in year"));
        // the weaker house would follow the stronger out of the Reach; the player's house goes where its head says
        g.W.Relationship(g.Me.Id, hard.Id).Disposition = -50; g.W.Relationship(g.HeadOf("ashgrove").Id, hard.Id).Disposition = -50;
        Assert.True(PolitySystem.Joins(g.W, g.H("ashgrove"), g.H("aurelian"))); Assert.False(PolitySystem.Joins(g.W, g.Mine, g.H("aurelian")));
        // the weaker house stays away from a vote against what the stronger moves; the player votes as they please, at a price
        var m = g.Move("aurelian", "censure", "oriel"); m.Lobby["ashgrove"] = -12; Assert.Equal(0, g.Tc.Senate.Lean(g.W, m, "ashgrove"));
        Assert.True(g.Tc.Executor.TryExecute(new VoteCommand(g.Me.Id, m.Id, -1), g.Ctx)); g.SetRegard("aurelian", -50);
        Count(g, m);
        Assert.Equal(-50 - DealingsSystem.DefiedRegard, g.Regard("aurelian")); Assert.True(g.Told("You voted against the motion of House Aurelian, which had told you what its fleets could do"));
        // an upright head never does it
        var g2 = Start(); var mild = g2.HeadOf("aurelian"); mild.NatureTraits.Clear(); mild.NatureTraits.Add("Honorable");
        foreach (var f in g2.W.Fleets.Values.Where(f => f.OwnerId == "aurelian")) f.Strength = 40;
        g2.W.Relationship(mild.Id, g2.Me.Id).Disposition = -80;
        for (int y = 0; y < 40; y++) { g2.W.Turn++; g2.D.Tick(g2.W); }
        Assert.False(g2.W.WorldCounters.ContainsKey(PolitySystem.CowedKey(g2.Mine.Id, "aurelian")));
    }

    [Fact]
    public void A_loan_is_offered_at_a_rate_and_taken_by_a_house_short_of_coin_whose_head_does_not_hate_you()
    {
        var g = Start(); var wren = g.H("wren"); wren.Treasury.Credits = 4; g.SetRegard("wren", 0); int income = g.D.Income(g.W, wren); Assert.True(income >= 4, income.ToString());
        Assert.Equal("not a sum or a rate that is offered", g.No(DealingsCommand.Lend, "wren", "7", 10)); Assert.Equal("not a sum or a rate that is offered", g.No(DealingsCommand.Lend, "wren", "5", 15));
        // the rate it bears follows how short it is
        Assert.Equal(DealingsSystem.BearsPinched, DealingsSystem.Bears(wren));
        Assert.Equal($"{g.HeadOf("wren").Name} will not pay 30 in the hundred: with 400 in hand, 20 is the most it bears", g.No(DealingsCommand.Lend, "wren", "5", 30));
        wren.Treasury.Credits = -1; Assert.Null(g.No(DealingsCommand.Lend, "wren", "5", 30)); wren.Treasury.Credits = 9; Assert.Equal(DealingsSystem.BearsShort, DealingsSystem.Bears(wren));
        wren.Treasury.Credits = DealingsSystem.ShortAt; Assert.Equal("House Wren is not short of coin: it holds 1,000, and borrows only under 1,000", g.No(DealingsCommand.Lend, "wren", "5", 10));
        wren.Treasury.Credits = 4;
        // no more than it could repay out of two years of what it takes in
        Assert.Contains($"House Wren takes in {Money.Credits(income)} a year and could not repay 4,400", g.No(DealingsCommand.Lend, "wren", "40", 10));
        Assert.Equal(DealingsSystem.LoanSizes.Where(a => Lending.Owed(a, 10) <= 2 * income).Max(), g.D.LendMost(g.W, wren, 10));
        g.SetRegard("wren", -40); Assert.Contains("will take nothing from you", g.No(DealingsCommand.Lend, "wren", "5", 10)); g.SetRegard("wren", 0);
        g.Mine.Treasury.Credits = 4; Assert.Equal("needs 500 credits and the house has 400", g.No(DealingsCommand.Lend, "wren", "5", 10)); g.Mine.Treasury.Credits = 100;
        int worth = Wealth.Of(g.W, g.Mine, Content.Holdings, Content.Businesses);
        Assert.Contains("House Wren would owe you 600 credits, and pay 200 into your treasury each year for 3 years", g.D.LendSays(g.W, wren, 5, 10));

        g.Do(DealingsCommand.Lend, "wren", "5", 10);
        var loan = Assert.Single(g.W.HouseLoans);
        Assert.Equal((g.Mine.Id, "wren", 5, 10, 6, 2, 3, 0, false), (loan.LenderId, loan.BorrowerId, loan.Lent, loan.RatePct, loan.Owed, loan.Instalment, loan.Since, loan.Missed, loan.Pressed));
        Assert.Equal(95, g.Mine.Treasury.Credits); Assert.Equal(9, wren.Treasury.Credits); Assert.Equal(DealingsSystem.KindRateRegard, g.Regard("wren"));
        Assert.Equal(worth + 1, Wealth.Of(g.W, g.Mine, Content.Holdings, Content.Businesses));   // the sum is out, and what is owed for it is the house's to come
        Assert.Equal("they owe you 600 credits already", g.No(DealingsCommand.Lend, "wren", "5", 10));
        Assert.Contains("in its debt", OrderSystem.Sway(g.W, wren, g.Mine).why); Assert.True(OrderSystem.Sway(g.W, wren, g.Mine).lean >= 2);
        Assert.Contains(g.D.Lasting(g.W, g.Mine, wren), l => l.text == "You lent it 500 credits in year 3 at 10 in the hundred: 600 is still owed, 200 a year.");
        // another house's loan stands in the way of a second lender
        var orlov = g.H("orlov"); orlov.Treasury.Credits = 2; g.SetRegard("orlov", 0); g.W.HouseLoans.Add(new HouseLoan { LenderId = "lucent", BorrowerId = "orlov", Lent = 5, Owed = 6, Instalment = 2, RatePct = 10, Since = 3 });
        Assert.Equal("they owe 600 credits to House Lucent: a second lender wants the first paid", g.No(DealingsCommand.Lend, "orlov", "5", 10));
        // a hard rate is taken as business
        Assert.Equal((DealingsSystem.KindRateRegard, DealingsSystem.FairRateRegard, DealingsSystem.HardRateRegard), (DealingsSystem.RateRegard(10), DealingsSystem.RateRegard(20), DealingsSystem.RateRegard(30)));
    }

    [Fact]
    public void A_loan_is_paid_in_yearly_instalments_through_the_ledger_until_it_is_cleared()
    {
        var g = Start(); var wren = g.H("wren"); wren.Treasury.Credits = 4; g.SetRegard("wren", 0); g.Do(DealingsCommand.Lend, "wren", "5", 10); var econ = g.Tc.Economy!;
        var mineLine = econ.Compute(g.W, g.Mine.Id).Lines.Single(l => l.Source == "instalment from House Wren"); Assert.Equal(2, mineLine.Delta.Credits); Assert.Equal("interest", mineLine.Kind);
        var theirLine = econ.Compute(g.W, "wren").Lines.Single(l => l.Source == "instalment to " + g.Mine.Name); Assert.Equal(-2, theirLine.Delta.Credits); Assert.Equal("loan", theirLine.Kind);
        int got = 0;
        for (int y = 1; y <= 3; y++)
        {
            wren.Treasury.Credits = 20; int mine = g.Mine.Treasury.Credits, net = econ.Compute(g.W, g.Mine.Id).Net.Credits, theirNet = econ.Compute(g.W, "wren").Net.Credits;
            g.W.Turn++; econ.Accrue(g.W);
            Assert.Equal(mine + net, g.Mine.Treasury.Credits); Assert.InRange(wren.Treasury.Credits, 20 + theirNet - 1, 20 + theirNet + 1); got += 2;   // (its own worlds' yield moves a little as the year turns)
            if (y < 3) Assert.Equal(6 - got, g.W.HouseLoans.Single().Owed);
        }
        Assert.Empty(g.W.HouseLoans); Assert.True(g.Told("House Wren has paid back the last of what you lent it")); Assert.Equal(1, g.Remembered(DealingsSystem.RepaidKind));
        Assert.Equal(DealingsSystem.KindRateRegard + DealingsSystem.RepaidRegard, g.Regard("wren"));
        Assert.DoesNotContain(econ.Compute(g.W, g.Mine.Id).Lines, l => l.Source.StartsWith("instalment"));
        Assert.Contains(g.D.Lasting(g.W, g.Mine, wren), l => l.text == $"It paid back in year {g.W.Turn} the 500 credits you lent it.");
    }

    [Fact]
    public void A_missed_payment_is_told_and_may_be_forgiven_for_regard_and_a_favour()
    {
        var g = Start(); var wren = g.H("wren"); wren.Treasury.Credits = 4; g.SetRegard("wren", 0); g.Do(DealingsCommand.Lend, "wren", "5", 10); var loan = g.W.HouseLoans.Single();
        Assert.Equal("they have missed no payment", g.No(DealingsCommand.Press, "wren")); Assert.Equal("they have missed no payment: there is nothing yet to forgive", g.No(DealingsCommand.Forgive, "wren")); Assert.Equal("they owe you nothing", g.No(DealingsCommand.Forgive, "lucent")); Assert.Equal("they owe you nothing", g.No(DealingsCommand.Press, "lucent"));
        wren.Treasury.Credits = 1; Assert.Equal(0, DealingsSystem.Due(g.W, loan));
        Assert.DoesNotContain(g.Tc.Economy!.Compute(g.W, g.Mine.Id).Lines, l => l.Source.StartsWith("instalment"));   // the ledger does not promise what will not come
        g.W.Turn++; g.Tc.Economy!.Accrue(g.W);
        Assert.Equal((6, 1), (loan.Owed, loan.Missed)); Assert.True(g.Told("House Wren cannot pay this year's 200 credits on what you lent it: 600 is still owed. Forgive the rest, or press for it, on its page."));
        Assert.Contains(g.D.Lasting(g.W, g.Mine, wren), l => l.text.Contains("It has gone 1 year short.") && l.tone == "warn");
        int regard = g.Regard("wren");
        g.Do(DealingsCommand.Forgive, "wren");
        Assert.Empty(g.W.HouseLoans); Assert.Equal(regard + DealingsSystem.ForgiveRegard, g.Regard("wren")); Assert.True(WealthSystem.FavourOwed(g.W, "wren", g.Mine.Id));
        Assert.Equal(1, g.Remembered(DealingsSystem.ForgivenKind)); Assert.Contains(g.D.Lasting(g.W, g.Mine, wren), l => l.text == $"You forgave it 600 credits in year {g.W.Turn}.");
        Assert.DoesNotContain("in its debt", OrderSystem.Sway(g.W, wren, g.Mine).why);
    }

    [Fact]
    public void A_pressed_loan_is_paid_as_it_can_be_and_after_three_years_short_is_a_default_that_costs_the_debtor_standing()
    {
        var g = Start(); var wren = g.H("wren"); wren.Treasury.Credits = 4; g.SetRegard("wren", 0); g.Do(DealingsCommand.Lend, "wren", "5", 10); var loan = g.W.HouseLoans.Single(); var econ = g.Tc.Economy!;
        wren.Treasury.Credits = 0; g.W.Turn++; econ.Accrue(g.W); Assert.Equal(1, loan.Missed);
        int regard = g.Regard("wren");
        g.Do(DealingsCommand.Press, "wren");
        Assert.True(loan.Pressed); Assert.Equal(regard - DealingsSystem.PressRegard, g.Regard("wren")); Assert.Equal("you have pressed them already", g.No(DealingsCommand.Press, "wren")); Assert.Equal(1, g.Remembered(DealingsSystem.PressedKind));
        // pressed, it pays what it has
        wren.Treasury.Credits = 1; Assert.Equal(1, DealingsSystem.Due(g.W, loan)); int mine = g.Mine.Treasury.Credits, net = econ.Compute(g.W, g.Mine.Id).Net.Credits;
        g.W.Turn++; econ.Accrue(g.W);
        Assert.Equal(mine + net, g.Mine.Treasury.Credits); Assert.Equal((5, 2), (loan.Owed, loan.Missed)); Assert.True(g.Told("House Wren pays 100 credits of this year's 200 credits on what you lent it: 500 is still owed. You have pressed for it: 1 more year of this and it is a default."));
        // a year paid in full and the count begins again
        wren.Treasury.Credits = 10; g.W.Turn++; econ.Accrue(g.W); Assert.Equal((3, 0), (loan.Owed, loan.Missed));
        int legitimacy = 0;
        for (int y = 0; y < DealingsSystem.DefaultYears; y++) { Assert.Single(g.W.HouseLoans); wren.Treasury.Credits = 0; legitimacy = wren.Legitimacy; g.W.Turn++; econ.Accrue(g.W); }
        Assert.Empty(g.W.HouseLoans); Assert.Equal(legitimacy - DealingsSystem.DefaultLegitimacy, wren.Legitimacy); Assert.True(VentureSystem.BadName(g.W, "wren"));
        Assert.True(g.Told("House Wren has defaulted: the 300 credits it still owed you is written off")); Assert.Equal(1, g.Remembered(DealingsSystem.DefaultKind)); Assert.Equal(1, g.W.Counter(DealingsSystem.LoansDefaultedKey));
        Assert.Contains(g.D.Lasting(g.W, g.Mine, wren), l => l.text == $"It defaulted on 300 credits it owed you in year {g.W.Turn}." && l.tone == "warn");
        // nothing passes between houses at war, and a house that is gone owes nothing
        var g2 = Start(); g2.H("wren").Treasury.Credits = 4; g2.SetRegard("wren", 0); g2.Do(DealingsCommand.Lend, "wren", "5", 10); g2.H("wren").Treasury.Credits = 50;
        g2.W.Wars.Add(new War { HouseA = g2.Mine.Id, HouseB = "wren", Since = g2.W.Turn }); Assert.Equal(0, DealingsSystem.Due(g2.W, g2.W.HouseLoans.Single()));
        g2.W.Houses.Remove("wren"); g2.W.Turn++; g2.Tc.Economy!.Accrue(g2.W); Assert.Empty(g2.W.HouseLoans); Assert.True(g2.Told("is no more, and the debt with it"));
    }

    [Fact]
    public void Other_houses_lend_to_each_other_and_forgive_or_press_as_they_think_of_the_borrower()
    {
        foreach (bool friend in new[] { true, false })
        {
            var g = Start(); var wren = g.H("wren"); var lucent = g.H("lucent"); wren.Treasury.Credits = 1; lucent.Treasury.Credits = 300;
            foreach (var h in g.W.Houses.Values.Where(h => h.Id != "wren" && Economy.HeadOf(g.W, h) != null)) g.W.Relationship(Economy.HeadOf(g.W, h)!.Id, g.HeadOf("wren").Id).Disposition = -5;   // nobody else is its friend
            var rel = g.W.Relationship(g.HeadOf("lucent").Id, g.HeadOf("wren").Id); rel.Disposition = friend ? DealingsSystem.OthersForgiveAt : DealingsSystem.OthersLendRegard;
            g.D.Tick(g.W);
            var loan = DealingsSystem.LoanBetween(g.W, "lucent", "wren"); Assert.NotNull(loan);
            Assert.Equal((5, DealingsSystem.OthersRate, 6, 2), (loan!.Lent, loan.RatePct, loan.Owed, loan.Instalment)); Assert.Equal(6, wren.Treasury.Credits); Assert.Equal(295, lucent.Treasury.Credits);
            Assert.Contains("in its debt", OrderSystem.Sway(g.W, wren, lucent).why);
            // they do not lend to the player's house
            Assert.DoesNotContain(g.W.HouseLoans, l => l.BorrowerId == g.Mine.Id || l.LenderId == g.Mine.Id);
            // it is paid like any other
            wren.Treasury.Credits = 10; int had = lucent.Treasury.Credits, net = g.Tc.Economy!.Compute(g.W, "lucent").Net.Credits; g.W.Turn++; g.Tc.Economy!.Accrue(g.W);
            Assert.Equal(4, loan.Owed); Assert.Equal(had + net, lucent.Treasury.Credits);
            // and when it is not: a friend forgives, anyone else presses
            wren.Treasury.Credits = 0; g.W.Turn++; g.Tc.Economy!.Accrue(g.W); Assert.Equal(1, loan.Missed);
            rel.Disposition = friend ? DealingsSystem.OthersForgiveAt : DealingsSystem.OthersLendRegard; wren.Treasury.Credits = 20; g.D.Tick(g.W);
            if (friend) { Assert.Null(DealingsSystem.LoanBetween(g.W, "lucent", "wren")); Assert.True(WealthSystem.FavourOwed(g.W, "wren", "lucent")); Assert.Equal(1, g.W.Counter(DealingsSystem.LoansForgivenKey)); }
            else { Assert.True(loan.Pressed); Assert.Equal(DealingsSystem.OthersLendRegard, rel.Disposition); }
            // one that was let off is not lent to again for some years
            wren.Treasury.Credits = 0; g.D.Tick(g.W); Assert.Equal(friend ? 0 : 1, g.W.HouseLoans.Count(l => l.BorrowerId == "wren"));
        }
    }

    [Fact]
    public void What_has_passed_is_kept_some_years_and_what_never_was_is_said_plainly()
    {
        var g = Start(); Assert.Empty(g.D.Lasting(g.W, g.Mine, g.H("cask")));
        g.SetRegard("cask", 0); g.Do(DealingsCommand.Gift, "cask", "2"); Assert.Single(g.D.Lasting(g.W, g.Mine, g.H("cask")));
        // the player's bank and theirs are loans each way too
        g.H("cask").Debt = 12; g.H("cask").LenderId = g.Mine.Id; Assert.Contains(g.D.Lasting(g.W, g.Mine, g.H("cask")), l => l.text == "It owes your bank 1,200 credits.");
        g.Mine.Debt = 7; g.Mine.LenderId = "cask"; Assert.Contains(g.D.Lasting(g.W, g.Mine, g.H("cask")), l => l.text == "You owe its bank 700 credits.");
        g.W.Turn += DealingsSystem.GiftsShownYears + 1; Assert.DoesNotContain(g.D.Lasting(g.W, g.Mine, g.H("cask")), l => l.text.StartsWith("You sent it"));
        g.W.Turn += DealingsSystem.RememberYears; g.D.Tick(g.W); Assert.Empty(g.W.Dealings);
    }

    [Fact]
    public void Dealings_are_saved_and_loaded_as_they_stand()
    {
        var g = Start(); var wren = g.H("wren"); wren.Treasury.Credits = 4; g.SetRegard("wren", 0); g.SetRegard("aurelian", 0); g.SetRegard("ashgrove", 0);
        g.Do(DealingsCommand.Lend, "wren", "5", 20); g.W.HouseLoans[0].Missed = 2; g.W.HouseLoans[0].Pressed = true;
        g.Do(DealingsCommand.Gift, "aurelian", "15"); g.Do(DealingsCommand.Threat, "ashgrove");
        var m = g.Move("aurelian", "censure", "oriel"); WealthSystem.OweFor(g.W, g.Mine.Id, "lucent"); g.W.WorldCounters.Remove(WealthSystem.FavourKey("aurelian", g.Mine.Id)); Close(g, m); g.Do(DealingsCommand.Pledge, "aurelian", m.Id);
        g.W.WorldCounters[DealingsSystem.CalledKey(g.Mine.Id, "lucent")] = g.W.Turn;

        var w2 = SaveSystem.Deserialize(SaveSystem.Serialize(g.W)); var tc2 = new TurnController(w2, Content); var mine2 = w2.House(g.Mine.Id)!;
        var a = g.W.HouseLoans.Single(); var b = Assert.Single(w2.HouseLoans);
        Assert.Equal((a.LenderId, a.BorrowerId, a.Lent, a.RatePct, a.Owed, a.Instalment, a.Since, a.Missed, a.Pressed), (b.LenderId, b.BorrowerId, b.Lent, b.RatePct, b.Owed, b.Instalment, b.Since, b.Missed, b.Pressed));
        var p = g.W.VotePledges.Single(); var q = Assert.Single(w2.VotePledges); Assert.Equal((p.ByHouseId, p.ToHouseId, p.MotionId, p.Side, p.Turn), (q.ByHouseId, q.ToHouseId, q.MotionId, q.Side, q.Turn));
        Assert.Equal(g.W.Dealings.Select(d => (d.Turn, d.Kind, d.FromHouseId, d.ToHouseId, d.Amount)), w2.Dealings.Select(d => (d.Turn, d.Kind, d.FromHouseId, d.ToHouseId, d.Amount)));
        Assert.True(w2.Dealings.Count >= 3); Assert.True(PolitySystem.Cowed(w2, w2.House("ashgrove")!, mine2)); Assert.True(DealingsSystem.Called(w2, mine2.Id, "lucent"));
        foreach (var id in new[] { "wren", "aurelian", "ashgrove", "lucent" })
        { var lines = g.D.Lasting(g.W, g.Mine, g.H(id)); Assert.NotEmpty(lines); Assert.Equal(lines, tc2.Dealings.Lasting(w2, mine2, w2.House(id)!)); }
    }

    [Fact]
    public void The_same_world_and_seed_play_the_same_dealings()
    {
        string Run()
        {
            var g = Start(seed: 11); g.W.Turn = 0; g.H("wren").Treasury.Credits = 4; g.SetRegard("wren", 0); g.Do(DealingsCommand.Lend, "wren", "5", 10); g.SetRegard("cask", 0); g.Do(DealingsCommand.Gift, "cask", "5");
            for (int y = 0; y < 12; y++) Year(g.Tc);
            g.Tc.SyncRng(); return SaveSystem.Serialize(g.W);
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Over_the_years_other_houses_lend_and_threaten_too()
    {
        var g = Start(); g.W.Turn = 0; for (int y = 0; y < 30; y++) Year(g.Tc);
        Assert.True(g.W.Counter(DealingsSystem.LoansMadeKey) > 0, "no house lent to another in thirty years");
        Assert.True(g.W.Counter(DealingsSystem.ThreatsKey) > 0, "no house threatened another in thirty years");
        Assert.All(g.W.HouseLoans, l => { Assert.True(l.Owed > 0 && l.Instalment > 0); Assert.NotNull(g.W.House(l.LenderId)); Assert.NotNull(g.W.House(l.BorrowerId)); });
        Assert.True(g.W.Dealings.Count <= DealingsSystem.RememberMost);
    }

    [Fact]
    public void Every_refusal_and_every_line_is_written_plainly()
    {
        var g = Start(); g.SetRegard("wren", 0); g.H("wren").Treasury.Credits = 4; var m = g.Move("aurelian", "censure", "oriel"); var said = new List<string>();
        foreach (var h in g.W.Houses.Values.Where(h => h.Id != g.Mine.Id).OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            foreach (var what in DealingsCommand.All) foreach (string value in new[] { "5", "40", m.Id }) foreach (int rate in new[] { 10, 30 }) if (g.No(what, h.Id, value, rate) is { } no) said.Add(no);
            if (World.Outsider(h.Id)) continue;
            said.Add(g.D.GiftSays(g.W, h, 5)); said.Add(g.D.AskSays(g.W, h, DealingsSystem.Coin)); said.Add(g.D.AskSays(g.W, h, DealingsSystem.Vote)); said.Add(g.D.PledgeSays(g.W, m, h)); said.Add(g.D.ThreatSays(g.W, g.Mine, h)); said.Add(g.D.LendSays(g.W, h, 5, 10));
        }
        g.Do(DealingsCommand.Lend, "wren", "5", 10); g.SetRegard("cask", 0); g.Do(DealingsCommand.Gift, "cask", "15"); g.SetRegard("ashgrove", 0); g.Do(DealingsCommand.Threat, "ashgrove");
        foreach (var h in g.W.Houses.Values.Where(h => h.Id != g.Mine.Id && !World.Outsider(h.Id))) said.AddRange(g.D.Lasting(g.W, g.Mine, h).Select(l => l.text));
        said.AddRange(g.W.News.Select(n => n.Text)); said.AddRange(DealingsCommand.All.Select(what => g.Cmd(what, "wren", "5", 10).Describe()));
        Assert.True(said.Count > 100);
        Assert.All(said, s => { Assert.False(string.IsNullOrWhiteSpace(s)); Assert.DoesNotContain("(s)", s); Assert.DoesNotContain("!", s); Assert.DoesNotContain("—", s); Assert.DoesNotContain("  ", s); });
        Assert.Equal("lend wren 500 at 10", g.Cmd(DealingsCommand.Lend, "wren", "5", 10).Describe()); Assert.Equal("gift cask 1,500", g.Cmd(DealingsCommand.Gift, "cask", "15").Describe());
    }
}
