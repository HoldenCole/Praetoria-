using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Every promise the game makes about a vote is kept by the engine (docs/events/votes.md, "Promises"). Each is made the way a
/// player makes it, the report gathered by the event engine and the choice applied through it, or the command the window sends; then
/// the real Senate session and count are run (SenateSystem.Tick), and the promised vote is looked for in the count, with the regard and
/// the notices the words name. The hard cases: a house that leans hard against, a motion that strikes at the house that promised, a
/// lapse, a new head, two promises at once, a save and a load between the promise and the count.
///
/// Group "bargain": a vote for a vote, a favour repaid with a house's voice, the gift that asks a favour, the whip's favour named
/// later, the envoy's friend asked for a vote, a debt paid in favour, the notice when a favour is called in, what the house page and
/// the Senate window say a favour is, a favour asked for a vote, the seated friend at the door, a motion moved at your asking.
///
/// Group "bought": a vote sold in A Vote for a Vote, the bought vote called in, the buyer shown the door, the purse on the Senate floor,
/// the whip's purse, the gold from beyond the Reach, a bribe and a lobby, a purse that comes out at the count.
///
/// Group "pledge": the favours called in on a motion against your house, a debt called in and paid with a vote, the favour you owe judged
/// at the count, your word given on the house window, a favour called in for a vote, a pledge of your seats at the consular election,
/// and the backing of your patron or bench leader for consul.
///
/// Group "office": one of yours standing for the ladder, spoken for and canvassed; taught to speak; the count read again; the consul's
/// table and what the Senior Consul does; your colleague in the consulship and the business divided; the other consul courted after a
/// veto; the emergency powers (no election while they run, the veto, the levy, the lean); the canvass and the good grace at the count;
/// a motion held over; the attendance record, the committee, the coin behind a canvass.
///
/// Group "bench": your word to the bench's leader that the empty seat will not happen again; the leader's table (a dinner, the whip);
/// a client at the door and the protection of a patron (the way you vote, and the seats at an election); a client helped with a suit;
/// the senator brought over to a growing bloc; the bench you found; the whip broken and remembered at the next count; a bench's
/// leader moving against your house, and its weakest member split from it; the patron's wish and the patron who noticed.
///
/// Group "other": the vote still undecided and its three ways to bring the doubter over; the old grudge against your motion and its
/// three answers; the old grudge on the docket and the peace made; the house that voted against you and its grudge; the repeal promised
/// to a friend an Act fell on; the feast for the houses that voted your recognition; the round stood after a motion thrown out; the
/// names written down after a censure and a motion thrown out; a name in commerce and its loans; the shares and the corner; the
/// chancellor asked to be asked; the house you owe blood to; a favour between two other houses in the whip count; your threat and a
/// threat against you; a loan from the house window; foreign gold shown to the chamber; the accounts called for in the squares.</summary>
public partial class VotePromisesTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());

    private sealed class G
    {
        public TurnController Tc = null!; public World W = null!; public Character Me = null!; public House Mine = null!;
        public CommandContext Ctx => new(W, WorldBuilder.RngFor(W));
        public SenateSystem Senate => Tc.Senate;
        public Character HeadOf(House h) => Economy.HeadOf(W, h)!;
        public int Regard(House h) => W.GetRelationship(HeadOf(h).Id, Me.Id)?.Disposition ?? 0;
        public bool Told(string part) => W.News.Any(n => n.Text.Contains(part));
        public bool Do(ICommand c) => Tc.Executor.TryExecute(c, Ctx);
    }

    /// <summary>The Old Lion in year four (no count of the consuls or the ladder is due), with an empty docket, no favour or pledge
    /// standing anywhere, influence to move a motion, and a tongue for the whip's report.</summary>
    private static G Start(ulong seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), seed);
        var tc = new TurnController(w, Content); tc.BeginTurn(); w.Turn = 4;
        w.Motions.Clear(); w.VotePledges.Clear(); w.News.Clear();
        foreach (var k in w.WorldCounters.Keys.Where(k => k.StartsWith(WealthSystem.FavourPrefix, StringComparison.Ordinal)).ToList()) w.WorldCounters.Remove(k);
        var me = w.Protagonist!; var mine = w.House(me.HouseId)!; mine.Treasury.Credits = 200; w.PoolsFor(me.Id).Influence = 30; me.Skills["oratory"] = 5;
        w.Wars.RemoveAll(x => x.Involves(mine.Id));
        return new G { Tc = tc, W = w, Me = me, Mine = mine };
    }
    /// <summary>The other houses of the Reach with a head, by name.</summary>
    private static List<House> Others(G g) => g.W.Houses.Values.Where(h => h.Id != g.Mine.Id && !World.Outsider(h.Id) && Economy.HeadOf(g.W, h) != null && PolitySystem.InReach(g.W, h.Id))
        .OrderBy(h => h.Id, StringComparer.Ordinal).ToList();

    /// <summary>A house of the Reach moves a motion, and the session that hears it writes the docket's marks; anything else moved that
    /// session is taken off, so that the docket holds only what the test is about.</summary>
    private static Motion TheyMove(G g, House h, string kind = "levy", string target = "")
    {
        var m = g.Senate.Add(g.W, h.Id, kind, target); g.Senate.Tick(g.W); Only(g, m); return m;
    }
    private static void Only(G g, params Motion[] keep) { g.W.Motions.RemoveAll(m => m.Open && !keep.Contains(m)); VoteMarks.Docket(g.W, g.Senate, g.Mine.Id); if (SenateSystem.Docket(g.W).Any()) g.W.WorldFlags.Add("motion_open"); }
    /// <summary>The player's house moves a motion, as the window does.</summary>
    private static Motion IMove(G g, string kind = "levy", string target = "")
    {
        var cmd = new ProposeMotionCommand(g.Me.Id, kind, target, g.Senate); Assert.True(g.Do(cmd), cmd.Refusal(g.Ctx));
        return g.W.Motions.Last(m => m.Open && m.ProposerHouseId == g.Mine.Id);
    }
    private static void IVote(G g, Motion m, int stance) => Assert.True(g.Do(new VoteCommand(g.Me.Id, m.Id, stance)));
    /// <summary>The next session: the year turns and the Senate counts what was moved the year before.</summary>
    private static void Session(G g) { g.W.Turn++; g.Senate.Tick(g.W); }
    /// <summary>A house leans hard against a motion: the other benches have whipped it, and a nudge of two would not bring it over.</summary>
    private static void HardAgainst(G g, Motion m, House h, int to = -6) { Leans(g, m, h, to); Assert.True(to + WealthSystem.FavourLeanBy < 0, "a nudge alone would carry it"); }
    /// <summary>A house leans this way on a motion, whatever moved it there.</summary>
    private static void Leans(G g, Motion m, House h, int to) { m.Lobby[h.Id] = m.Lobby.GetValueOrDefault(h.Id) + to - g.Senate.Lean(g.W, m, h.Id); Assert.Equal(to, g.Senate.Lean(g.W, m, h.Id)); }

    /// <summary>The report as the event engine gathers it, with the one it casts in the part.</summary>
    private static (FiredEvent fired, Character cast) Report(G g, string eventId, string role)
    {
        var e = new EventEngine(Content, WorldBuilder.RngFor(g.W)).GatherEligible(g.W).FirstOrDefault(x => x.def.Id == eventId);
        Assert.True(e.def != null, $"{eventId} is not eligible");
        Assert.True(e.binding.TryGet(role, out var id)); return (new FiredEvent(e.def, e.binding), g.W.Char(id!)!);
    }
    private static void Answer(G g, FiredEvent f, string choice)
    {
        var engine = new EventEngine(Content, WorldBuilder.RngFor(g.W));
        Assert.True(engine.IsChoiceAvailable(f, g.W, choice), $"{f.Def.Id}/{choice} is not open");
        engine.Resolve(f, g.W, choice);
    }
    private static G Reload(G g)
    {
        var w = SaveSystem.Deserialize(SaveSystem.Serialize(g.W)); var me = w.Protagonist!;
        return new G { Tc = new TurnController(w, Content), W = w, Me = me, Mine = w.House(me.HouseId)! };
    }

    /// <summary>A Vote for a Vote, the word given: the house that moved the motion on the docket, its head cast as the senator.</summary>
    private static (G g, House theirs, Character senator, Motion theirMotion) Bargained(ulong seed = 7)
    {
        var g = Start(seed); var theirs = Others(g)[0]; g.W.Relationship(g.HeadOf(theirs).Id, g.Me.Id).Disposition = 0;
        var theirMotion = TheyMove(g, theirs);
        var (f, senator) = Report(g, "a_vote_for_a_vote", "senator"); Assert.Equal(g.HeadOf(theirs).Id, senator.Id);
        Answer(g, f, "give_your_word");
        return (g, theirs, senator, theirMotion);
    }

    // ------------------------------------------------------------------ a vote for a vote: their word

    [Fact]
    public void AVoteForAVote_TheirHouseVotesForYourNextMotion_HoweverHardItLeansAgainst()
    {
        var (g, theirs, _, _) = Bargained();
        Assert.True(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id));   // owed from the moment the word is given, not from the next session
        var mine = IMove(g); HardAgainst(g, mine, theirs);
        // the whip count and the forecast show it as a sure vote, with its reason
        Assert.True(g.Senate.Owes(g.W, mine, theirs.Id)); Assert.Equal(1, g.Senate.Stance(g.W, mine, theirs.Id));
        var (forBefore, _) = g.Senate.Tally(g.W, mine); ForecastWithout(g, mine, theirs, forBefore);
        Session(g);
        Assert.True(mine.Resolved); Assert.Equal(1, mine.Votes[theirs.Id]); Assert.True(g.Senate.Lean(g.W, mine, theirs.Id) < 0, "it would have voted against of itself");
        Assert.Equal(g.W.Houses.Values.Where(h => !World.Outsider(h.Id) && mine.Votes.GetValueOrDefault(h.Id) > 0).Sum(h => g.Senate.Weight(g.W, h.Id)), mine.ForWeight);   // counted in the tally
        Assert.False(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id));   // the vote it owed is given, and the favour spent
        Assert.True(g.Told($"{theirs.Name} kept its word")); Assert.Contains(g.W.History, h => h.Text.Contains($"as it owed {g.Mine.Name} a vote"));
        Assert.Equal(1, g.W.Dealings.Count(d => d.Kind == DealingsSystem.FavourKeptKind && d.FromHouseId == theirs.Id && d.ToHouseId == g.Mine.Id));
    }
    /// <summary>What the forecast would have said had the house not owed the vote: its seats against, not for.</summary>
    private static void ForecastWithout(G g, Motion m, House theirs, int forWith)
    {
        int weight = g.Senate.Weight(g.W, theirs.Id); var key = WealthSystem.FavourKey(theirs.Id, g.Mine.Id); int since = g.W.Counter(key);
        g.W.WorldCounters.Remove(key); var (forWithout, _) = g.Senate.Tally(g.W, m); g.W.WorldCounters[key] = since;
        Assert.Equal(forWith - weight, forWithout);
    }

    [Fact]
    public void AHouseThatOwesYouAVote_LeadingABench_TheForecastAndTheCountAgreeOnItsMembers()
    {
        var (g, theirs, _, _) = Bargained(); var member = Others(g).First(h => h.Id != theirs.Id);
        foreach (var f in g.W.Factions) { f.Members.Remove(theirs.Id); f.Members.Remove(member.Id); }
        g.W.Factions.RemoveAll(f => f.LeaderHouseId == theirs.Id || f.LeaderHouseId == member.Id);
        g.W.Factions.Add(new Faction { Id = "test_bench", Name = "the Test Bench", LeaderHouseId = theirs.Id, Members = { theirs.Id, member.Id } });
        var levy = IMove(g); HardAgainst(g, levy, theirs);
        // the member follows what its leader is for, not the vote its leader owes you: two against, on the bench's word
        Assert.True(g.Senate.LeaderLean(g.W, levy, theirs.Id) < 0); Leans(g, levy, member, -FactionSystem.Discipline);
        int forecast = g.Senate.Stance(g.W, levy, member.Id); Assert.Equal(-1, forecast);
        Session(g);
        Assert.Equal(1, levy.Votes[theirs.Id]); Assert.Equal(forecast, levy.Votes[member.Id]);   // the forecast and the count agree
    }

    [Fact]
    public void AVoteForAVote_TheirWordWaitsOnAMotionThatStrikesAtThem_AndIsPaidOnTheNext()
    {
        var (g, theirs, _, theirMotion) = Bargained();
        var censure = IMove(g, "censure", theirs.Id);
        Assert.False(g.Senate.Owes(g.W, censure, theirs.Id)); Session(g);
        Assert.True(censure.Votes[theirs.Id] < 0);   // nobody votes for their own censure for what they owe
        Assert.True(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id)); Assert.True(g.Told($"Your motion strikes at {theirs.Name}, so the vote it owes your house is not asked of it here"));
        // the next motion of yours: paid
        Only(g); g.W.WorldCounters.Remove($"motion_cd:{g.Mine.Id}:levy:"); var levy = IMove(g); HardAgainst(g, levy, theirs); Session(g);
        Assert.Equal(1, levy.Votes[theirs.Id]); Assert.False(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id));
    }

    [Fact]
    public void AVoteForAVote_ForAMotionTheHouseLikesAnyway_TheVoteIsStillOwed()
    {
        var (g, theirs, _, _) = Bargained();
        var levy = IMove(g); Leans(g, levy, theirs, 3); Session(g);
        Assert.Equal(1, levy.Votes[theirs.Id]); Assert.True(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id));   // it voted for it of its own accord: the favour waits for a motion it would not
        Assert.True(g.Told($"{theirs.Name} voted for your motion of its own accord"));
    }

    [Fact]
    public void AVoteForAVote_LapsesWithAWar_WithANotice()
    {
        var (g, theirs, _, _) = Bargained();
        var levy = IMove(g); HardAgainst(g, levy, theirs);
        g.Tc.Fleets.DeclareWar(g.W, g.Mine.Id, theirs.Id); Assert.True(g.W.AtWar(g.Mine.Id, theirs.Id));
        Assert.False(g.Senate.Owes(g.W, levy, theirs.Id)); Session(g);
        Assert.True(levy.Votes[theirs.Id] < 0);   // an enemy at war gives no vote
        WealthSystem.Tick(g.W);   // the year's end
        // their motion was counted at that session too, first: across the war the vote for a vote is void both ways, and said (DealingsSystem.MotionCounted)
        Assert.False(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id)); Assert.True(g.Told($"You are at war with {theirs.Name}: the vote for a vote is void both ways: {theirs.Name} owes your house no vote for it, and your vote on its motion was not judged."));
        Assert.DoesNotContain(g.W.Dealings, d => d.Kind == DealingsSystem.PledgeBrokenKind && d.FromHouseId == g.Mine.Id);   // your word on their motion was not judged
    }

    [Fact]
    public void AVoteForAVote_LapsesAfterEightYearsWithNoMotionOfYours_WithANotice()
    {
        var (g, theirs, _, _) = Bargained(); int since = g.W.Counter(WealthSystem.FavourKey(theirs.Id, g.Mine.Id));
        for (int y = 1; y < WealthSystem.FavourYears; y++) { g.W.Turn++; WealthSystem.Tick(g.W); }
        Assert.True(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id));
        g.W.Turn++; WealthSystem.Tick(g.W); Assert.Equal(since + WealthSystem.FavourYears, g.W.Turn);
        Assert.False(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id)); Assert.True(g.Told($"What {theirs.Name} owed your house has gone 8 years unspent"));
    }

    [Fact]
    public void AVoteForAVote_ANewHeadKeepsTheHousesWord_AcrossASaveAndALoad()
    {
        var (g0, theirs0, senator, _) = Bargained();
        DynastySystem.Die(g0.W, senator.Id); var heir = Economy.HeadOf(g0.W, theirs0); Assert.NotNull(heir); Assert.NotEqual(senator.Id, heir!.Id);
        var g = Reload(g0); var theirs = g.W.House(theirs0.Id)!;
        var levy = IMove(g); HardAgainst(g, levy, theirs); Session(g);
        Assert.Equal(1, levy.Votes[theirs.Id]); Assert.False(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id));
    }

    // ------------------------------------------------------------------ a vote for a vote: your word

    [Fact]
    public void AVoteForAVote_YourWordIsDeclared_YouAreReminded_AndKeepingItIsCounted()
    {
        var (g, theirs, senator, theirMotion) = Bargained();
        Assert.Equal(1, theirMotion.Votes[g.Mine.Id]);   // your vote on theirs is declared for it
        var pledge = DealingsSystem.PledgeOn(g.W, g.Mine.Id, theirMotion.Id); Assert.NotNull(pledge); Assert.Equal(theirs.Id, pledge!.ToHouseId);
        Assert.True(g.Told($"You gave {theirs.Name} your word: your vote on its motion is declared for it")); Assert.True(g.Told("-15"));   // the reminder, with what breaking it costs
        int before = g.Regard(theirs); g = Reload(g); theirs = g.W.House(theirs.Id)!; theirMotion = g.W.Motions.First(m => m.Id == theirMotion.Id);   // a save and a load between the word and the count
        Session(g);
        Assert.True(theirMotion.Resolved); Assert.Equal(1, theirMotion.Votes[g.Mine.Id]);
        Assert.Equal(before + DealingsSystem.PledgeKeptRegard, g.Regard(theirs));
        Assert.Equal(1, g.W.Dealings.Count(d => d.Kind == DealingsSystem.PledgeKeptKind && d.FromHouseId == g.Mine.Id && d.ToHouseId == theirs.Id));
        Assert.Contains(g.W.History, h => h.Text.Contains($"{g.Mine.Name} gave {theirs.Name} its word on a vote, and kept it"));   // the chamber sees it paid
    }

    [Fact]
    public void AVoteForAVote_YourWordBroken_IsRememberedAndBarsTheNextBargain()
    {
        var (g, theirs, senator, theirMotion) = Bargained();
        IVote(g, theirMotion, -1); int before = g.Regard(theirs); Session(g);
        Assert.Equal(before - DealingsSystem.PledgeBrokenRegard, g.Regard(theirs));
        Assert.Equal(1, g.W.Dealings.Count(d => d.Kind == DealingsSystem.PledgeBrokenKind && d.FromHouseId == g.Mine.Id && d.ToHouseId == theirs.Id));
        Assert.True(g.Told($"You gave {theirs.Name} your word on a vote and did not keep it")); Assert.Contains(g.W.History, h => h.Text.Contains("did not keep it"));
        // their house remembers it: it takes no word of yours for eight years, and no new bargain is struck with it
        Assert.Contains(VoteMarks.BargainBarredChar, senator.Flags);
        g.W.WorldCounters.Remove(WealthSystem.FavourKey(theirs.Id, g.Mine.Id)); VoteMarks.Docket(g.W, g.Senate, g.Mine.Id);
        Assert.Contains(VoteMarks.BargainBarredChar, senator.Flags);   // the broken word alone bars it, with no favour standing
        var again = TheyMove(g, theirs, "relief", g.W.Galaxy!.ReachRegions.OrderBy(r => r.Id, StringComparer.Ordinal).First().Id);
        Assert.DoesNotContain(new EventEngine(Content, WorldBuilder.RngFor(g.W)).GatherEligible(g.W), e => e.def.Id == "a_vote_for_a_vote" && e.binding.TryGet("senator", out var s) && s == senator.Id);
    }

    [Fact]
    public void AVoteForAVote_TheirMotionVetoed_YourWordIsVoid_WithANotice()
    {
        var (g, theirs, _, theirMotion) = Bargained();
        var consul = Others(g).First(h => h.Id != theirs.Id);
        ElectionSystem.Veto(g.W, g.Senate.Offices, consul.Id, theirMotion);
        Assert.Null(DealingsSystem.PledgeOn(g.W, g.Mine.Id, theirMotion.Id)); Assert.True(g.Told("Your word on it is void"));
        int before = g.Regard(theirs); Session(g);
        Assert.Equal(before, g.Regard(theirs)); Assert.DoesNotContain(g.W.Dealings, d => d.Kind == DealingsSystem.PledgeBrokenKind);
        Assert.True(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id));   // their word to you stands
    }

    // ------------------------------------------------------------------ their house's voice, the whip, the envoy's friend

    [Fact]
    public void AFavourRepaid_TheirVoice_TheirHouseVotesForYourMotion()
    {
        var g = Start(); var theirs = Others(g)[1]; var debtor = g.HeadOf(theirs); debtor.Flags.Add("owes_you_a_favour");   // as a_classmate_in_the_city or a_house_without_lands left it
        var (f, cast) = Report(g, "a_favour_repaid", "debtor"); Assert.Equal(debtor.Id, cast.Id);
        Answer(g, f, "their_voice");
        Assert.True(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id)); Assert.DoesNotContain("owes_you_a_favour", debtor.Flags);
        var levy = IMove(g); Leans(g, levy, theirs, 0); VoteMarks.Docket(g.W, g.Senate, g.Mine.Id);
        Assert.DoesNotContain(VoteMarks.UndecidedChar, debtor.Flags);   // leaning neither way, it is not a doubter to be won: it votes for you
        HardAgainst(g, levy, theirs); Assert.Equal(1, g.Senate.Stance(g.W, levy, theirs.Id)); Session(g);
        Assert.Equal(1, levy.Votes[theirs.Id]); Assert.False(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id)); Assert.True(g.Told($"{theirs.Name} kept its word"));
    }

    [Fact]
    public void TheWhip_AFavourNamedLater_IsOwedByTheWhipsHouse_AndVotesForYou_AlongsideASecondPromise()
    {
        var g = Start(); var mover = Others(g)[0]; TheyMove(g, mover);
        var (f, whip) = Report(g, "the_whip", "whip"); Assert.NotEqual(g.Mine.Id, whip.HouseId);   // a whip comes from another house
        Answer(g, f, "name_your_price");
        var theirs = g.W.House(whip.HouseId)!; Assert.True(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id));
        Assert.Contains(g.W.History, h => h.Text.Contains($"{theirs.Name} owes your house a favour"));
        // two promises at once: a second house owes your house a vote as well, by the envoy's friend
        var second = Others(g).First(h => h.Id != theirs.Id && h.Id != mover.Id);
        var envoy = g.W.LivingCharacters().First(c => c.HouseId == g.Mine.Id && c.Id != g.Me.Id && c.Age >= DynastySystem.AgeOfMajority); envoy.Flags.Add("post:envoy");
        g.HeadOf(second).Flags.Add("envoy_called_on"); g.W.WorldFlags.Add("envoy_made_a_friend"); g.W.WorldCounters["mark:envoy_made_a_friend"] = g.W.Turn;
        var (fe, host) = Report(g, "the_envoys_friend", "host"); Assert.Equal(g.HeadOf(second).Id, host.Id);
        Answer(g, fe, "ask_for_a_vote"); Assert.True(WealthSystem.FavourOwed(g.W, second.Id, g.Mine.Id));
        var levy = IMove(g); HardAgainst(g, levy, theirs); HardAgainst(g, levy, second); Session(g);
        Assert.Equal(1, levy.Votes[theirs.Id]); Assert.Equal(1, levy.Votes[second.Id]);
        Assert.False(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id)); Assert.False(WealthSystem.FavourOwed(g.W, second.Id, g.Mine.Id));
    }

    [Fact]
    public void TheWhip_IsNeverOfYourOwnHouse_AndAHouseAlreadyInAFavourWithYoursCannotBeAskedAgain()
    {
        var g = Start(); TheyMove(g, Others(g)[0]);
        var ranks = g.W.LivingCharacters().ToDictionary(c => c.Id, c => c.CareerRank);
        foreach (var c in g.W.LivingCharacters().Where(c => c.HouseId != g.Mine.Id)) c.CareerRank = Math.Min(c.CareerRank, 1);
        var kin = g.W.LivingCharacters().First(c => c.HouseId == g.Mine.Id && c.Id != g.Me.Id && c.Age >= DynastySystem.AgeOfMajority); kin.CareerRank = 3;
        Assert.DoesNotContain(new EventEngine(Content, WorldBuilder.RngFor(g.W)).GatherEligible(g.W), e => e.def.Id == "the_whip");   // only your own of the rank: no whip comes
        foreach (var (id, rank) in ranks) g.W.Char(id)!.CareerRank = rank;
        var (f, whip) = Report(g, "the_whip", "whip"); Answer(g, f, "name_your_price"); var theirs = g.W.House(whip.HouseId)!;
        // the next whip of that house finds the favour standing: the choice is shown, closed, and says why
        VoteMarks.Docket(g.W, g.Senate, g.Mine.Id); var engine = new EventEngine(Content, WorldBuilder.RngFor(g.W));
        Assert.False(engine.IsChoiceAvailable(f, g.W, "name_your_price"));
        var why = Praetoria.Core.Presentation.ChoiceStakes.Unmet(f.Def.Choice("name_your_price")!, new EvalContext(g.W, f.Binding, WorldBuilder.RngFor(g.W), Content));
        Assert.Contains($"not while a favour stands between your house and {theirs.Name}", why);
    }

    [Fact]
    public void TheWhip_TheFavourCanBeNamedForSomethingElse_AndThenNoVoteIsOwed()
    {
        var g = Start(); var theirMotion = TheyMove(g, Others(g)[0]);
        var (f, whip) = Report(g, "the_whip", "whip"); Answer(g, f, "name_your_price"); var theirs = g.W.House(whip.HouseId)!;
        theirs.Treasury.Credits = Math.Max(100, theirs.Treasury.Credits);
        var named = new WealthCommand(g.Me.Id, WealthCommand.Favour, theirs.Id, WealthSystem.ForMoney);
        Assert.Equal(DealingsSystem.BargainPendingSays(theirs), named.Refusal(g.Ctx));   // not while your word on its motion, the price of the favour, is still to be counted
        Session(g); Assert.True(theirMotion.Resolved); Only(g); int had = g.Mine.Treasury.Credits;   // your side kept at its count
        Assert.True(g.Do(named), named.Refusal(g.Ctx));   // named: money
        Assert.Equal(had + WealthSystem.FavourCredits, g.Mine.Treasury.Credits); Assert.False(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id));
        Only(g); var levy = IMove(g); HardAgainst(g, levy, theirs); Assert.False(g.Senate.Owes(g.W, levy, theirs.Id)); Session(g);
        Assert.True(levy.Votes[theirs.Id] < 0);
    }

    // ------------------------------------------------------------------ the gift that asks a favour

    /// <summary>An orrery sent by a house with a motion before the Senate, as WealthSystem sends one, and its report.</summary>
    private static (G g, House theirs, Motion theirMotion, FiredEvent report) Orrery()
    {
        var g = Start(); var theirs = Others(g)[0]; var head = g.HeadOf(theirs); g.W.Heirlooms.Clear(); g.Mine.Legitimacy = 99;
        var theirMotion = TheyMove(g, theirs); int years = 0;
        while (!g.W.HasFlag(WealthSystem.AsksFavourFlag) && years++ < 200)
        { g.W.Turn++; foreach (var h in g.W.Houses.Values) h.Treasury.Credits = 0; theirs.Treasury.Credits = 200; g.W.Relationship(head.Id, g.Me.Id).Disposition = 0; WealthSystem.Tick(g.W); }
        Assert.True(WealthSystem.FavourOwed(g.W, g.Mine.Id, theirs.Id));
        Assert.True(g.Told($"You owe {theirs.Name} a favour, and its motion is before the Senate"));   // the reminder, since the motion was moved before the favour
        var (f, giver) = Report(g, "a_gift_that_asks_a_favour", "giver"); Assert.Equal(head.Id, giver.Id);
        return (g, theirs, theirMotion, f);
    }

    [Fact]
    public void AGiftThatAsks_IsNotSentToTheHouseItsMotionStrikesAt()
    {
        var g = Start(); var theirs = Others(g)[0]; var head = g.HeadOf(theirs); g.W.Heirlooms.Clear(); g.Mine.Legitimacy = 99;
        TheyMove(g, theirs, "censure", g.Mine.Id); int years = 0;
        while (!g.W.Heirlooms.Any(h => h.Kind == WealthSystem.Orrery) && years++ < 200)
        { g.W.Turn++; foreach (var h in g.W.Houses.Values) h.Treasury.Credits = 0; theirs.Treasury.Credits = 200; g.W.Relationship(head.Id, g.Me.Id).Disposition = 0; WealthSystem.Tick(g.W); }
        var orrery = Assert.Single(g.W.Heirlooms, h => h.Kind == WealthSystem.Orrery);
        Assert.NotEqual(g.Mine.Id, orrery.OfHouseId); Assert.False(WealthSystem.FavourOwed(g.W, g.Mine.Id, theirs.Id));   // the weightiest house is passed over: nobody is asked to vote for their own censure
    }

    [Fact]
    public void AGiftThatAsks_KeptAndVotedFor_IsEven_KeptAndVotedAgainst_IsRemembered()
    {
        var (g, theirs, theirMotion, f) = Orrery(); Answer(g, f, "keep_it_and_remember");
        IVote(g, theirMotion, 1); int before = g.Regard(theirs); Session(g);
        Assert.Equal(before + WealthSystem.FavourKept, g.Regard(theirs)); Assert.False(WealthSystem.FavourOwed(g.W, g.Mine.Id, theirs.Id)); Assert.True(g.Told("you voted for it. The account is even"));
        var (g2, theirs2, theirMotion2, f2) = Orrery(); Answer(g2, f2, "keep_it_and_remember");
        IVote(g2, theirMotion2, -1); int before2 = g2.Regard(theirs2); Session(g2);
        Assert.Equal(before2 - WealthSystem.FavourBroken, g2.Regard(theirs2)); Assert.True(g2.Told("you did not vote for it"));
        Assert.Contains(g2.W.History, h => h.Text.Contains("called in the favour your house owed it") && !h.Text.Contains("gift"));
    }

    [Fact]
    public void AGiftThatAsks_SaidToBuyNothing_YourVoteIsNotJudged_InTheSameYear()
    {
        var (g, theirs, theirMotion, f) = Orrery(); Answer(g, f, "keep_it_and_promise_nothing");
        IVote(g, theirMotion, -1); int before = g.Regard(theirs), broken = g.W.Counter(WealthSystem.FavoursBrokenKey); Session(g);
        Assert.True(theirMotion.Resolved); Assert.Equal(before, g.Regard(theirs)); Assert.Equal(broken, g.W.Counter(WealthSystem.FavoursBrokenKey));
        Assert.False(g.Told("you did not vote for it")); Assert.Contains(g.W.Heirlooms, h => h.Kind == WealthSystem.Orrery);   // the orrery stayed
    }

    [Fact]
    public void AGiftThatAsks_SentBack_YourVoteIsNotJudged_InTheSameYear()
    {
        var (g, theirs, theirMotion, f) = Orrery(); Answer(g, f, "send_it_back");
        IVote(g, theirMotion, -1); int before = g.Regard(theirs); Session(g);
        Assert.Equal(before, g.Regard(theirs)); Assert.False(g.Told("you did not vote for it"));
        Assert.DoesNotContain(g.W.Heirlooms, h => h.Kind == WealthSystem.Orrery); Assert.False(WealthSystem.FavourOwed(g.W, g.Mine.Id, theirs.Id));
    }

    // ------------------------------------------------------------------ a debt paid in favour, and the notice that a favour is called in

    [Fact]
    public void TheDebtCalled_PaidInFavour_YourHouseOwesTheirsAVote_JudgedAtItsNextMotion_AcrossASaveAndALoad()
    {
        var g0 = Start(); var theirs0 = Others(g0)[0]; g0.Me.Flags.Add("indebted"); g0.HeadOf(theirs0).Flags.Add("creditor");   // as the_lean_season's loan left them
        var (f, creditor) = Report(g0, "the_debt_called", "creditor"); Assert.Equal(g0.HeadOf(theirs0).Id, creditor.Id);
        Answer(g0, f, "pay_with_favor"); Assert.True(WealthSystem.FavourOwed(g0.W, g0.Mine.Id, theirs0.Id)); Assert.DoesNotContain("indebted", g0.Me.Flags);
        var g = Reload(g0); var theirs = g.W.House(theirs0.Id)!;
        var m = g.Senate.Add(g.W, theirs.Id, "levy", "");
        Assert.True(g.Told($"{theirs.Name} has moved a motion, and you owe it a favour: it is called in at this motion's count")); Assert.True(g.Told("-15"));
        IVote(g, m, -1); int before = g.Regard(theirs); Session(g);
        Assert.Equal(before - WealthSystem.FavourBroken, g.Regard(theirs)); Assert.False(WealthSystem.FavourOwed(g.W, g.Mine.Id, theirs.Id));
    }

    [Fact]
    public void AFavourYouOwe_IsNotCalledInOnAMotionStruckBeforeTheCount_NorOnOneThatStrikesAtYou()
    {
        var g = Start(); var theirs = Others(g)[0]; g.Me.Flags.Add("indebted"); g.HeadOf(theirs).Flags.Add("creditor");
        var (f, _) = Report(g, "the_debt_called", "creditor"); Answer(g, f, "pay_with_favor");
        // a censure of your own house: no notice, and your vote against it is not judged
        var censure = g.Senate.Add(g.W, theirs.Id, "censure", g.Mine.Id); Assert.False(g.Told("you owe it a favour: it is called in"));
        IVote(g, censure, -1); int broken = g.W.Counter(WealthSystem.FavoursBrokenKey); Session(g);
        Assert.True(censure.Resolved); Assert.Equal(broken, g.W.Counter(WealthSystem.FavoursBrokenKey)); Assert.DoesNotContain(g.W.Dealings, d => d.Kind == DealingsSystem.FavourBrokenKind);
        Assert.True(WealthSystem.FavourOwed(g.W, g.Mine.Id, theirs.Id));
        // a motion vetoed before its count: you are told the favour waits for the next one
        Only(g); var levy = g.Senate.Add(g.W, theirs.Id, "levy", ""); Assert.True(g.Told("If the motion is struck before the count, the favour waits"));
        ElectionSystem.Veto(g.W, g.Senate.Offices, Others(g).First(h => h.Id != theirs.Id).Id, levy);
        Assert.True(g.Told($"The motion of {theirs.Name} was struck before the count, so the favour you owe it was not called in")); Session(g);
        Assert.True(WealthSystem.FavourOwed(g.W, g.Mine.Id, theirs.Id));
        // and at its next motion it is called in
        Only(g); g.W.WorldCounters.Remove($"motion_cd:{theirs.Id}:levy:"); var next = g.Senate.Add(g.W, theirs.Id, "levy", ""); IVote(g, next, 1); int kept = g.Regard(theirs); Session(g);
        Assert.Equal(kept + WealthSystem.FavourKept, g.Regard(theirs)); Assert.False(WealthSystem.FavourOwed(g.W, g.Mine.Id, theirs.Id));
    }

    // ------------------------------------------------------------------ what the house page says a favour is; a favour asked for a vote

    [Fact]
    public void TheHousePage_SaysWhatAFavourOwedIs_AndTheCountDoesIt()
    {
        var g = Start(); var theirs = Others(g)[0];
        WealthSystem.OweFor(g.W, theirs.Id, g.Mine.Id);   // owed, as a gift or a pledge kept leaves it
        var says = g.Tc.Dealings.Lasting(g.W, g.Mine, theirs).Select(x => x.text).First(t => t.StartsWith("It owes you a favour"));
        Assert.Contains("votes for every motion you move", says); Assert.Contains($"forgotten in year {g.W.Turn + WealthSystem.FavourYears}", says);
        var levy = IMove(g); Leans(g, levy, theirs, 2); Session(g);
        Assert.Equal(1, levy.Votes[theirs.Id]); Assert.True(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id));   // for it of itself: still owed
        Only(g); g.W.WorldCounters.Remove($"motion_cd:{g.Mine.Id}:levy:"); var again = IMove(g); HardAgainst(g, again, theirs); Session(g);
        Assert.Equal(1, again.Votes[theirs.Id]); Assert.False(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id));   // against of itself: paid, and spent
        Assert.Contains(g.Tc.Dealings.Lasting(g.W, g.Mine, theirs), x => x.text.Contains("paid with its vote: 1 time"));
    }

    [Fact]
    public void AFavourAskedForAVote_TheirVoteIsDeclared_AndYoursIsAskedAtTheirNextMotion()
    {
        var g = Start(); var theirs = Others(g)[0]; g.W.Relationship(g.HeadOf(theirs).Id, g.Me.Id).Disposition = 30;
        var levy = IMove(g); HardAgainst(g, levy, theirs);
        var ask = new DealingsCommand(g.Me.Id, DealingsCommand.AskVote, theirs.Id, g.Tc.Dealings, levy.Id); Assert.True(g.Do(ask), ask.Refusal(g.Ctx));
        Assert.Equal(1, levy.Votes[theirs.Id]); Assert.True(WealthSystem.FavourOwed(g.W, g.Mine.Id, theirs.Id));
        Session(g); Assert.Equal(1, levy.Votes[theirs.Id]);
        Only(g); var theirNext = g.Senate.Add(g.W, theirs.Id, "levy", ""); Assert.True(g.Told($"{theirs.Name} has moved a motion, and you owe it a favour"));
        IVote(g, theirNext, 1); int before = g.Regard(theirs); Session(g);
        Assert.Equal(before + WealthSystem.FavourKept, g.Regard(theirs)); Assert.False(WealthSystem.FavourOwed(g.W, g.Mine.Id, theirs.Id));
    }

    [Fact]
    public void AMotionMovedAtYourAsking_DoesNotSettleTheFavour_TheirOwnNextMotionDoes()
    {
        var g = Start(); var patron = Others(g).First(h => g.Senate.MayPropose(g.W, h.Id)); g.Mine.PatronId = patron.Id; var head = g.HeadOf(patron);
        g.W.Relationship(head.Id, g.Me.Id).Disposition = 30; g.W.PoolsFor(head.Id).Influence = 30;
        var ask = new AskToMoveCommand(g.Me.Id, patron.Id, "levy", "", g.Senate); Assert.True(g.Do(ask), ask.Refusal(g.Ctx));
        var moved = g.W.Motions.Single(m => m.Open && m.ProposerHouseId == patron.Id); Assert.Equal(g.Mine.Id, moved.MovedFor);
        Assert.True(WealthSystem.FavourOwed(g.W, g.Mine.Id, patron.Id)); Assert.False(g.Told("its motion is before the Senate: the favour is called in"));
        // voted against, the motion you asked for settles nothing (the patron's own displeasure at a client's vote is another matter, SenateSystem.PatronWish)
        IVote(g, moved, -1); int broken = g.W.Counter(WealthSystem.FavoursBrokenKey); Session(g);
        Assert.True(moved.Resolved); Assert.True(WealthSystem.FavourOwed(g.W, g.Mine.Id, patron.Id)); Assert.Equal(broken, g.W.Counter(WealthSystem.FavoursBrokenKey));
        Assert.DoesNotContain(g.W.Dealings, d => d.Kind == DealingsSystem.FavourBrokenKind);
        // the next motion the patron moves of its own: you are told, and judged
        Only(g); g.W.WorldCounters.Remove($"motion_cd:{patron.Id}:levy:"); var own = g.Senate.Add(g.W, patron.Id, "levy", ""); Assert.True(g.Told($"{patron.Name} has moved a motion, and you owe it a favour"));
        IVote(g, own, 1); int kept = g.W.Counter(WealthSystem.FavoursKeptKey); Session(g);
        Assert.Equal(kept + 1, g.W.Counter(WealthSystem.FavoursKeptKey)); Assert.False(WealthSystem.FavourOwed(g.W, g.Mine.Id, patron.Id));
        Assert.Contains(g.W.Dealings, d => d.Kind == DealingsSystem.FavourKeptKind && d.FromHouseId == g.Mine.Id && d.ToHouseId == patron.Id);
    }

    // ------------------------------------------------------------------ the doorkeeper

    [Fact]
    public void TheDoorkeeper_ASeatedFriend_PromisesNoFavourItCannotName()
    {
        var g = Start(); g.W.WorldFlags.Add(OrderSystem.NoSeatFlag);
        var (f, _) = Report(g, "the_doorkeeper", "self"); int lines = g.W.History.Count; Answer(g, f, "ask_a_seated_friend");
        Assert.DoesNotContain(g.W.WorldCounters.Keys, k => k.StartsWith(WealthSystem.FavourPrefix, StringComparison.Ordinal));
        Assert.DoesNotContain("favour", g.W.History.Skip(lines).Last().Text);
    }

    // ================================================================== group "bought"

    /// <summary>A Vote for a Vote, a price named: the house that moved the motion on the docket, its head cast as the senator, who pays
    /// for the house's vote on it. The player's own house leans hard against that motion, so a vote left to drift would go against it.</summary>
    private static (G g, House theirs, Character senator, Motion theirMotion) Sold(ulong seed = 7)
    {
        var g = Start(seed); var theirs = Others(g)[0]; g.W.Relationship(g.HeadOf(theirs).Id, g.Me.Id).Disposition = 0; theirs.Treasury.Credits = Math.Max(50, theirs.Treasury.Credits);
        g.Me.NatureTraits.Remove("Honorable"); g.Me.NatureTraits.Remove("Just");   // the upright do not name a price
        var theirMotion = TheyMove(g, theirs); Leans(g, theirMotion, g.Mine, -6);
        var (f, senator) = Report(g, "a_vote_for_a_vote", "senator"); Assert.Equal(g.HeadOf(theirs).Id, senator.Id);
        int mineHad = g.Mine.Treasury.Credits, theirsHad = theirs.Treasury.Credits;
        Answer(g, f, "name_a_price");
        Assert.Equal(mineHad + 4, g.Mine.Treasury.Credits); Assert.Equal(theirsHad - 4, theirs.Treasury.Credits);   // 400 credits, from their treasury to yours
        return (g, theirs, senator, theirMotion);
    }
    private static int ForWeight(G g, Motion m) => g.W.Houses.Values.Where(h => !World.Outsider(h.Id) && m.Votes.GetValueOrDefault(h.Id) > 0).Sum(h => g.Senate.Weight(g.W, h.Id));
    private static bool Eligible(G g, string eventId, string role, string houseId) =>
        new EventEngine(Content, WorldBuilder.RngFor(g.W)).GatherEligible(g.W).Any(e => e.def.Id == eventId && e.binding.TryGet(role, out var id) && g.W.Char(id!)?.HouseId == houseId);

    // ------------------------------------------------------------------ a vote sold: a_vote_for_a_vote/name_a_price

    [Fact]
    public void AVoteSold_IsDeclaredForTheirMotion_YouAreReminded_AndGivingItIsCounted_AcrossASaveAndALoad()
    {
        var (g, theirs, senator, theirMotion) = Sold();
        Assert.Equal(1, theirMotion.Votes[g.Mine.Id]);   // the senator can count on it: declared for their motion, whatever your house leans
        var bought = DealingsSystem.PledgeOn(g.W, g.Mine.Id, theirMotion.Id); Assert.NotNull(bought); Assert.True(bought!.Paid); Assert.Equal(theirs.Id, bought.ToHouseId);
        Assert.True(g.Told($"{theirs.Name} has bought your vote on its motion: it is declared for it")); Assert.True(g.Told("-15"));   // the reminder, with what breaking it costs
        Assert.Contains(g.Tc.Dealings.Lasting(g.W, g.Mine, theirs), x => x.text.StartsWith("Your vote is bought by it, for this motion"));
        int before = g.Regard(theirs); g = Reload(g); theirs = g.W.House(theirs.Id)!; theirMotion = g.W.Motions.First(m => m.Id == theirMotion.Id);   // a save and a load between the sale and the count
        Assert.True(g.Senate.Lean(g.W, theirMotion, g.Mine.Id) < 0);
        Session(g);
        Assert.True(theirMotion.Resolved); Assert.Equal(1, theirMotion.Votes[g.Mine.Id]); Assert.Equal(ForWeight(g, theirMotion), theirMotion.ForWeight);   // cast and counted
        Assert.Equal(before + DealingsSystem.PledgeKeptRegard, g.Regard(theirs));
        Assert.False(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id));   // the coin paid for it: no favour owed besides
        Assert.Equal(1, g.W.Dealings.Count(d => d.Kind == DealingsSystem.PledgeKeptKind && d.FromHouseId == g.Mine.Id && d.ToHouseId == theirs.Id));
        Assert.True(g.Told($"You voted for the motion of {theirs.Name}, as it had bought your vote")); Assert.Contains(g.W.History, h => h.Text.Contains($"gave {theirs.Name} the vote it had bought"));
        Assert.Contains(VoteMarks.BoughtChar, g.W.Char(senator.Id)!.Flags);   // and they paid once: the thread goes on (a_bought_vote_called_in)
    }

    [Fact]
    public void AVoteSold_AndChanged_IsBroken_WrittenDown_AndTheBuyerDoesNotCallInAnother()
    {
        var (g, theirs, senator, theirMotion) = Sold();
        IVote(g, theirMotion, -1); int before = g.Regard(theirs); Session(g);
        Assert.Equal(-1, theirMotion.Votes[g.Mine.Id]); Assert.Equal(before - DealingsSystem.PledgeBrokenRegard, g.Regard(theirs));
        Assert.Equal(1, g.W.Dealings.Count(d => d.Kind == DealingsSystem.PledgeBrokenKind && d.FromHouseId == g.Mine.Id && d.ToHouseId == theirs.Id));
        Assert.True(g.Told($"{theirs.Name} had bought your vote on its motion, and you did not give it")); Assert.Contains(g.W.History, h => h.Text.Contains($"took the price of its vote from {theirs.Name}"));
        // cheated, the buyer does not come back to call in another vote; their house takes no word of yours for eight years
        Assert.DoesNotContain(VoteMarks.BoughtChar, senator.Flags);
        TheyMove(g, theirs, "relief", g.W.Galaxy!.ReachRegions.OrderBy(r => r.Id, StringComparer.Ordinal).First().Id);
        Assert.Contains(VoteMarks.BargainBarredChar, senator.Flags);
        Assert.False(Eligible(g, "a_bought_vote_called_in", "buyer", theirs.Id)); Assert.False(Eligible(g, "a_vote_for_a_vote", "senator", theirs.Id));
    }

    [Fact]
    public void AVoteSold_IsNotOfferedOnAMotionThatStrikesAtYou_NorOnOneYourVoteIsPledgedOn()
    {
        var g = Start(); var theirs = Others(g)[0]; g.W.Relationship(g.HeadOf(theirs).Id, g.Me.Id).Disposition = 0; theirs.Treasury.Credits = 50;
        TheyMove(g, theirs, "breakup", g.Mine.Id);   // a break-up of your own house: nobody buys your vote for it
        Assert.DoesNotContain(VoteMarks.VoteWantedChar, g.HeadOf(theirs).Flags);
        Assert.False(Eligible(g, "a_vote_for_a_vote", "senator", theirs.Id)); Assert.False(Eligible(g, "the_whip", "whip", theirs.Id));
        // a motion of theirs your vote is already pledged on: nothing left to sell
        var levy = TheyMove(g, theirs); Assert.Contains(VoteMarks.VoteWantedChar, g.HeadOf(theirs).Flags);
        foreach (var h in g.W.Houses.Values.Where(h => !World.Outsider(h.Id) && h.Id != theirs.Id && h.Id != g.Mine.Id)) levy.Votes[h.Id] = 0;   // a close vote, so it wants your word
        var pledge = new DealingsCommand(g.Me.Id, DealingsCommand.Pledge, theirs.Id, g.Tc.Dealings, levy.Id); Assert.True(g.Do(pledge), pledge.Refusal(g.Ctx));
        VoteMarks.Docket(g.W, g.Senate, g.Mine.Id);
        Assert.DoesNotContain(VoteMarks.VoteWantedChar, g.HeadOf(theirs).Flags); Assert.False(Eligible(g, "a_vote_for_a_vote", "senator", theirs.Id));
    }

    [Fact]
    public void AVoteSold_TheBuyingHouseGone_TheVoteLapsesWithANotice()
    {
        var (g, theirs, _, theirMotion) = Sold(); var into = Others(g).First(h => h.Id != theirs.Id); string name = theirs.Name;
        HouseSystem.Fold(g.W, theirs, into, $"{name} is no more.");
        Assert.Null(g.W.House(theirs.Id)); Assert.Null(DealingsSystem.PledgeOn(g.W, g.Mine.Id, theirMotion.Id));
        Assert.True(g.Told($"{name} is no more, and the motion {name} bought your vote on has lapsed with it. No vote is asked of you for what was paid."));
        Session(g); Assert.DoesNotContain(g.W.Dealings, d => d.Kind is DealingsSystem.PledgeBrokenKind or DealingsSystem.PledgeKeptKind);
    }

    [Fact]
    public void AVoteSold_ANewHeadOfTheBuyingHouse_JudgesIt()
    {
        var (g, theirs, senator, theirMotion) = Sold();
        DynastySystem.Die(g.W, senator.Id); var heir = Economy.HeadOf(g.W, theirs); Assert.NotNull(heir); Assert.NotEqual(senator.Id, heir!.Id);
        int before = g.W.GetRelationship(heir.Id, g.Me.Id)?.Disposition ?? 0;
        IVote(g, theirMotion, 0); Session(g);   // stayed away: not the vote they bought
        Assert.Equal(before - DealingsSystem.PledgeBrokenRegard, g.W.GetRelationship(heir.Id, g.Me.Id)!.Disposition);   // the house remembers, under its new head
        Assert.Contains(g.W.Dealings, d => d.Kind == DealingsSystem.PledgeBrokenKind && d.FromHouseId == g.Mine.Id && d.ToHouseId == theirs.Id);
    }

    // ------------------------------------------------------------------ the bought vote called in: a_bought_vote_called_in

    /// <summary>The vote sold and given; the buyer moves another motion, and comes to call in another vote on the same bargain.</summary>
    private static (G g, House theirs, Character buyer, Motion next, FiredEvent report) CalledIn()
    {
        var (g, theirs, senator, _) = Sold(); Session(g);
        var next = TheyMove(g, theirs, "relief", g.W.Galaxy!.ReachRegions.OrderBy(r => r.Id, StringComparer.Ordinal).First().Id); Leans(g, next, g.Mine, -6);
        var (f, buyer) = Report(g, "a_bought_vote_called_in", "buyer"); Assert.Equal(senator.Id, buyer.Id);
        return (g, theirs, buyer, next, f);
    }

    [Fact]
    public void ABoughtVoteCalledIn_YourVoteIsDeclaredForTheirNewMotion_AndKeepingItIsCounted_AcrossASaveAndALoad()
    {
        var (g, theirs, buyer, next, f) = CalledIn(); News(g);
        Answer(g, f, "vote_as_they_ask");
        Assert.Equal(1, next.Votes[g.Mine.Id]); var bought = DealingsSystem.PledgeOn(g.W, g.Mine.Id, next.Id); Assert.True(bought is { Paid: true } && bought.ToHouseId == theirs.Id);
        Assert.True(g.Told($"{theirs.Name} calls in the vote it bought from you before: your vote on its motion is declared for it, unpaid this time.")); Assert.False(g.Told($"{theirs.Name} has bought your vote on its motion"));   // told as a vote called in, not a new sale Assert.True(g.Told("-15"));   // reminded before the count, this year
        Assert.False(WealthSystem.FavourOwed(g.W, g.Mine.Id, theirs.Id));   // a vote on this motion, not a favour carried to whatever it moves next
        int before = g.Regard(theirs); g = Reload(g); theirs = g.W.House(theirs.Id)!; next = g.W.Motions.First(m => m.Id == next.Id);
        Session(g);
        Assert.Equal(1, next.Votes[g.Mine.Id]); Assert.Equal(ForWeight(g, next), next.ForWeight); Assert.Equal(before + DealingsSystem.PledgeKeptRegard, g.Regard(theirs));
        Assert.Equal(2, g.W.Dealings.Count(d => d.Kind == DealingsSystem.PledgeKeptKind && d.FromHouseId == g.Mine.Id && d.ToHouseId == theirs.Id));
    }
    private static void News(G g) => g.W.News.Clear();

    [Fact]
    public void ABoughtVoteCalledIn_VotedOtherwise_TheyRememberIt()
    {
        var (g, theirs, buyer, next, f) = CalledIn(); Answer(g, f, "vote_as_they_ask");
        IVote(g, next, -1); int before = g.Regard(theirs); Session(g);
        Assert.Equal(before - DealingsSystem.PledgeBrokenRegard, g.Regard(theirs)); Assert.True(g.Told($"{theirs.Name} had bought your vote on its motion, and you did not give it"));
        // "they will remember it": written down, and a later report reads it: their house strikes no bargain with yours for eight years
        Assert.Contains(g.Tc.Dealings.Lasting(g.W, g.Mine, theirs), x => x.text.Contains("Your word to it on a vote: kept 1 time, broken 1 time"));
        TheyMove(g, theirs); Assert.Contains(VoteMarks.BargainBarredChar, buyer.Flags); Assert.False(Eligible(g, "a_vote_for_a_vote", "senator", theirs.Id));
        for (int y = 1; y < DealingsSystem.PledgeMemoryYears; y++) { g.W.Turn++; VoteMarks.Docket(g.W, g.Senate, g.Mine.Id); Assert.Contains(VoteMarks.BargainBarredChar, buyer.Flags); }
        g.W.Turn++; VoteMarks.Docket(g.W, g.Senate, g.Mine.Id); Assert.DoesNotContain(VoteMarks.BargainBarredChar, buyer.Flags);   // eight years on, forgotten
    }

    [Fact]
    public void ABoughtVoteCalledIn_TheirMotionVetoed_TheVoteIsVoid_AndNotCarriedToTheirNext()
    {
        var (g, theirs, buyer, next, f) = CalledIn(); Answer(g, f, "vote_as_they_ask");
        ElectionSystem.Veto(g.W, g.Senate.Offices, Others(g).First(h => h.Id != theirs.Id).Id, next);
        Assert.Null(DealingsSystem.PledgeOn(g.W, g.Mine.Id, next.Id)); Assert.True(g.Told($"The motion {theirs.Name} bought your vote on was struck before the count. No vote is asked of you for what was paid."));
        int before = g.Regard(theirs); Session(g); Assert.Equal(before, g.Regard(theirs));
        // their next motion asks nothing of your vote: you vote against it, and nothing is judged (a favour would have waited for it)
        var third = TheyMove(g, theirs); Assert.False(g.Told($"{theirs.Name} has moved a motion, and you owe it a favour")); IVote(g, third, -1); Session(g);
        Assert.True(third.Resolved); Assert.DoesNotContain(g.W.Dealings, d => d.Kind is DealingsSystem.PledgeBrokenKind or DealingsSystem.FavourBrokenKind && d.FromHouseId == g.Mine.Id);
    }

    [Fact]
    public void ABoughtVoteCalledIn_IsNotAskedOnAMotionAgainstYourHouse()
    {
        var (g, theirs, senator, _) = Sold(); Session(g); Assert.Contains(VoteMarks.BoughtChar, senator.Flags);
        TheyMove(g, theirs, "censure", g.Mine.Id);   // their next motion is a censure of yours: nobody asks you to vote for it
        Assert.Contains(VoteMarks.MovesChar, senator.Flags); Assert.False(Eligible(g, "a_bought_vote_called_in", "buyer", theirs.Id));
    }

    [Fact]
    public void ABoughtVoteCalledIn_ShownTheDoor_CostsYourHouseItsStanding_AndTheyRememberIt()
    {
        var (g, theirs, buyer, _, f) = CalledIn(); int legit = g.Mine.Legitimacy, regard = g.Regard(theirs);
        Answer(g, f, "show_them_the_door");
        Assert.Equal(legit - 2, g.Mine.Legitimacy); Assert.Equal(regard - 10, g.Regard(theirs));   // the chamber heard about the one
        Assert.Contains(g.Mine.LegitimacyRecord.Changes, c => c.By == -2 && c.Line.Contains("A Bought Vote Called In"));
        Assert.Contains("grudge_against_you", buyer.Flags); Assert.DoesNotContain(VoteMarks.BoughtChar, buyer.Flags);
        Assert.Null(DealingsSystem.PledgeOn(g.W, g.Mine.Id, SenateSystem.Docket(g.W).First(m => m.ProposerHouseId == theirs.Id).Id));   // and no vote is owed them
    }

    // ------------------------------------------------------------------ the whip's purse: the_whip/take_the_coin

    [Fact]
    public void TheWhip_ThePurseTaken_YourVoteIsSoldForTheirMotion_AndJudged_AlongsideASecondSale()
    {
        // first a vote sold to one house in A Vote for a Vote; then a second house moves, and its whip comes with a purse
        var (g, a, _, onA) = Sold(); var b = Others(g).First(h => h.Id != a.Id && g.W.LivingCharacters().Any(c => c.HouseId == h.Id && c.CareerRank >= 2 && c.Age >= DynastySystem.AgeOfMajority));
        var onB = g.Senate.Add(g.W, b.Id, "levy", ""); Only(g, onA, onB); Leans(g, onB, g.Mine, -6);
        Assert.DoesNotContain(VoteMarks.VoteWantedChar, g.HeadOf(a).Flags);   // your vote on theirs is sold already
        var (f, whip) = Report(g, "the_whip", "whip"); Assert.Equal(b.Id, whip.HouseId);
        int had = g.Mine.Treasury.Credits; Answer(g, f, "take_the_coin"); Assert.Equal(had + 6, g.Mine.Treasury.Credits);
        Assert.Equal(1, onB.Votes[g.Mine.Id]); Assert.True(DealingsSystem.PledgeOn(g.W, g.Mine.Id, onB.Id) is { Paid: true } p && p.ToHouseId == b.Id);
        Assert.True(g.Told($"{b.Name} has bought your vote on its motion: it is declared for it")); Assert.Contains(g.W.History, h => h.Text.Contains($"promised the house's vote for the motion of {b.Name}"));
        // you give the one and not the other: each judged, by the numbers the reminders named
        IVote(g, onA, -1); int toA = g.Regard(a), toB = g.Regard(b); Session(g);
        Assert.Equal(1, onB.Votes[g.Mine.Id]); Assert.Equal(ForWeight(g, onB), onB.ForWeight); Assert.Equal(toB + DealingsSystem.PledgeKeptRegard, g.Regard(b));
        Assert.Equal(toA - DealingsSystem.PledgeBrokenRegard, g.Regard(a));
    }

    [Fact]
    public void TheWhip_ComesOnlyFromAHouseWithAMotionYourVoteMayBeHadOn()
    {
        var g = Start(); var mine = IMove(g); Only(g, mine);
        Assert.True(g.W.HasFlag("motion_open")); Assert.DoesNotContain(new EventEngine(Content, WorldBuilder.RngFor(g.W)).GatherEligible(g.W), e => e.def.Id == "the_whip");   // your own motion: nobody buys your vote on it
        var theirs = Others(g)[0]; var censure = g.Senate.Add(g.W, theirs.Id, "censure", g.Mine.Id); Only(g, mine, censure);
        Assert.False(Eligible(g, "the_whip", "whip", theirs.Id));   // nor on a censure of your own house
    }

    // ------------------------------------------------------------------ a bribe, a lobby, a purse that comes out: LobbyCommand, SenateSystem.BribeComesOut

    /// <summary>The houses whose heads would take a purse on this motion, by whether it comes out at the count.</summary>
    private static List<House> Takers(G g, Motion m, bool comesOut)
    {
        foreach (var h in Others(g)) { var head = g.HeadOf(h); head.NatureTraits.Clear(); head.Ambition = ""; g.W.Relationship(head.Id, g.Me.Id).Disposition = 0; }
        return Others(g).Where(h => SenateSystem.BribeComesOut(g.W, m, h.Id) == comesOut && SenateSystem.BribeRefused(g.W, h, g.Me) == null && !FactionSystem.Leads(g.W, h.Id)).ToList();
    }

    [Fact]
    public void ABribe_LeansTheHouseByThree_AndTheCountShowsWhetherItWasEnough_AcrossASaveAndALoad()
    {
        var g = Start(); var m = IMove(g); var quiet = Takers(g, m, comesOut: false); Assert.True(quiet.Count >= 2, "two houses whose purse stays hidden");
        var near = quiet[0]; var far = quiet[1];
        Leans(g, m, near, -2); Leans(g, m, far, -6);   // one a little against, one hard against
        Assert.True(g.Do(new LobbyCommand(g.Me.Id, m.Id, near.Id, 1, bribe: true))); Assert.True(g.Do(new LobbyCommand(g.Me.Id, m.Id, far.Id, 1, bribe: true)));
        Assert.Equal(-2 + SenateSystem.BribeWeight, g.Senate.Lean(g.W, m, near.Id)); Assert.Equal(-6 + SenateSystem.BribeWeight, g.Senate.Lean(g.W, m, far.Id));
        Assert.True(g.Told($"{near.Name} has taken the purse: its own vote leans for the motion by 3 more. Whether that is enough shows in the count"));
        Assert.Contains(g.Senate.Why(g.W, m, far.Id), x => x.why == "its vote has been paid for" && x.by == SenateSystem.BribeWeight);
        g = Reload(g); m = g.W.Motions.First(x => x.Id == m.Id); Session(g);
        Assert.Equal(1, m.Votes[near.Id]); Assert.Equal(-1, m.Votes[far.Id]);   // enough for the one; not for the other, as the count shows
        Assert.Equal(ForWeight(g, m), m.ForWeight);
    }

    [Fact]
    public void ALobby_LeansTheHouseByTwo_AndItsBenchByOne_AndTheCountFollows()
    {
        var g = Start(); var levy = IMove(g); var leader = Others(g)[0]; var member = Others(g)[1];
        foreach (var f in g.W.Factions) { f.Members.Remove(leader.Id); f.Members.Remove(member.Id); }
        g.W.Factions.RemoveAll(f => f.LeaderHouseId == leader.Id || f.LeaderHouseId == member.Id);
        g.W.Factions.Add(new Faction { Id = "test_bench", Name = "the Test Bench", LeaderHouseId = leader.Id, Members = { leader.Id, member.Id } });
        g.W.Relationship(g.HeadOf(leader).Id, g.Me.Id).Disposition = 0; levy.Lobby.Remove(leader.Id);
        int lead = g.Senate.Lean(g.W, levy, leader.Id);
        Assert.True(g.Do(new LobbyCommand(g.Me.Id, levy.Id, leader.Id, 1)));
        Assert.Equal(lead + SenateSystem.LobbyWeight, g.Senate.Lean(g.W, levy, leader.Id));
        Assert.Contains(g.Senate.Why(g.W, levy, member.Id), x => x.why == "its leader has been lobbied" && x.by == SenateSystem.LobbyWeight / 2);   // its bench by half as much
        Assert.True(g.Told($"{leader.Name} has heard you: it leans for the motion by 2 more, and its bench by half as much"));
        Session(g);
        // the count goes as the lean, the lobby in it: the leader by its own, the bench with the leader's lobby halved
        Assert.Equal(Math.Sign(g.Senate.Lean(g.W, levy, leader.Id)), levy.Votes[leader.Id]); Assert.Equal(Math.Sign(g.Senate.Lean(g.W, levy, member.Id)), levy.Votes[member.Id]);
        Assert.Contains(g.Senate.Why(g.W, levy, leader.Id), x => x.why == "it has been lobbied" && x.by == SenateSystem.LobbyWeight);
        Assert.Contains(g.Senate.Why(g.W, levy, member.Id), x => x.why == "its leader has been lobbied" && x.by == SenateSystem.LobbyWeight / 2);
    }

    [Fact]
    public void APurseThatComesOut_StrikesTheVote_TheTallyLosesIt_AndThePayerLosesStanding()
    {
        var g = Start(); var m = IMove(g); var outed = Takers(g, m, comesOut: true).First();
        Leans(g, m, outed, 0); Assert.True(g.Do(new LobbyCommand(g.Me.Id, m.Id, outed.Id, 1, bribe: true))); Assert.Equal(SenateSystem.BribeWeight, g.Senate.Lean(g.W, m, outed.Id));   // bought, it would vote for it
        g = Reload(g); m = g.W.Motions.First(x => x.Id == m.Id); outed = g.W.House(outed.Id)!; Session(g);
        Assert.Equal(0, m.Votes[outed.Id]); Assert.Equal(ForWeight(g, m), m.ForWeight);   // struck, and the tally is taken without it
        Assert.Contains(g.Mine.LegitimacyRecord.Changes, c => c.By == -SenateSystem.BribeOutLegitimacy && c.Cause == LegitimacyBook.Scandal && c.Line == "A bought vote came out on the floor");
        Assert.Contains(g.W.History, h => h.Text.Contains($"It comes out on the floor that {g.Mine.Name} paid {outed.Name} for its vote. The vote is struck"));
        // and the report that says so names the head who took the purse
        var (f, bought) = Report(g, "a_purse_on_the_senate_floor", "bought"); Assert.Equal(g.HeadOf(outed).Id, bought.Id); Assert.True(g.W.HasFlag(VoteMarks.PurseOutFlag));
    }

    // ------------------------------------------------------------------ foreign gold: ForeignGoldSystem.TakeSays

    /// <summary>A war on the Free Cities before the Senate, close, with every house but one and the player's declared to stay away; the
    /// Cities write, and the player takes the gold on the Senate window. The player's house leans for the war, so it is asked to vote
    /// against it.</summary>
    private static (G g, Motion war, GoldOffer offer) GoldTaken()
    {
        var g = Start(); var cities = g.W.Nation("thessar")!; cities.PurseOpened = true; cities.Purse = ForeignGoldSystem.Cap(Content.Nations.Def("thessar")!); cities.BoughtOffTurn = g.W.Turn;
        var others = Others(g); var mover = others.Where(h => g.Senate.MayPropose(g.W, h.Id)).Last(); var open = others.First(h => h.Id != mover.Id);
        g.HeadOf(open).NatureTraits.Clear(); g.HeadOf(open).NatureTraits.Add("Loyal"); open.Treasury.Credits = 100;
        var war = g.Senate.Add(g.W, mover.Id, "foreign_war", "thessar");
        foreach (var h in g.W.Houses.Values.Where(h => !World.Outsider(h.Id) && h.Id != mover.Id && h.Id != g.Mine.Id && h.Id != open.Id)) war.Votes[h.Id] = 0;
        for (int i = 0; i < 6; i++) { Leans(g, war, open, 1); war.Lobby[g.Mine.Id] = war.Lobby.GetValueOrDefault(g.Mine.Id) + 1 - g.Senate.Lean(g.W, war, g.Mine.Id); }
        Assert.Equal(1, g.Senate.Lean(g.W, war, g.Mine.Id));
        ForeignGoldSystem.Tick(g.W, Content.Nations, g.Senate, Content.Offices);
        var offer = ForeignGoldSystem.WaitingOn(g.W, war.Id, g.Mine.Id); Assert.NotNull(offer); Assert.Equal(ForeignGoldSystem.Against, offer!.Ask);
        Assert.Contains($"Vote for the war after all and they send your house nothing for {Counted.Years(ForeignGoldSystem.BrokenYears)}.", ForeignGoldSystem.TakeSays(offer));   // what the button says
        Assert.True(g.Do(new ForeignGoldCommand(g.Me.Id, offer.Id, ForeignGoldSystem.Take)));
        Assert.Equal(-1, war.Votes[g.Mine.Id]);
        Assert.True(g.Told($"You took what {offer.NationName} sent: your house is declared against the war on {offer.WarOnName}. Vote for the war after all and they send your house nothing for 10 years."));   // reminded before the count
        return (g, war, offer);
    }
    private static void GoldYear(G g) { Session(g); ForeignGoldSystem.Tick(g.W, Content.Nations, g.Senate, Content.Offices); }

    [Fact]
    public void ForeignGold_TakenToVoteAgainst_StayingAwayIsNotVotingForTheWar_OnlyAVoteForItShutsYouOutForTenYears()
    {
        var (g, war, offer) = GoldTaken();
        IVote(g, war, 0); GoldYear(g);   // you stayed away instead of voting against: not a vote for the war
        Assert.True(war.Resolved && !war.Vetoed); Assert.Equal(0, war.Votes[g.Mine.Id]);
        Assert.False(offer.Broken); Assert.False(ForeignGoldSystem.Shut(g.W, g.Mine.Id)); Assert.False(g.Told("voted for the war all the same"));
        // a vote for the war after all: shut out, and told for how long
        var (g2, war2, offer2) = GoldTaken();
        IVote(g2, war2, 1); GoldYear(g2); int year = g2.W.Turn;
        Assert.True(offer2.Broken); Assert.True(g2.Told($"voted for the war all the same. They send your house nothing for {Counted.Years(ForeignGoldSystem.BrokenYears)}"));
        g2.W.Turn = year + ForeignGoldSystem.BrokenYears - 1; Assert.True(ForeignGoldSystem.Shut(g2.W, g2.Mine.Id));
        g2.W.Turn = year + ForeignGoldSystem.BrokenYears; Assert.False(ForeignGoldSystem.Shut(g2.W, g2.Mine.Id));   // ten sessions with nothing sent, then the Cities may write again
    }

    [Fact]
    public void ForeignGold_AWarVetoedBeforeTheCount_JudgesNoVote()
    {
        var (g, war, offer) = GoldTaken();
        IVote(g, war, 1);   // you changed your mind; then a consul strikes the war before it is voted
        ElectionSystem.Veto(g.W, g.Senate.Offices, Others(g).First(h => h.Id != war.ProposerHouseId).Id, war);
        ForeignGoldSystem.Tick(g.W, Content.Nations, g.Senate, Content.Offices);
        Assert.False(offer.Broken); Assert.False(ForeignGoldSystem.Shut(g.W, g.Mine.Id)); Assert.False(g.Told("voted for the war all the same"));
        Assert.True(g.Told($"The war on {offer.WarOnName} was vetoed before the count, and no vote was cast: nothing is asked of your house for the gold."));
    }

    // ================================================================== group "pledge"

    /// <summary>The report as the event engine gathers it, for one with no part but your own.</summary>
    private static FiredEvent ReportOf(G g, string eventId)
    {
        var e = new EventEngine(Content, WorldBuilder.RngFor(g.W)).GatherEligible(g.W).FirstOrDefault(x => x.def.Id == eventId);
        Assert.True(e.def != null, $"{eventId} is not eligible"); return new FiredEvent(e.def, e.binding);
    }
    /// <summary>A house comes to owe yours a favour the way a player makes one: a gift from the house window of a tenth of its year's
    /// income or more (DealingsSystem.GiftOwes).</summary>
    private static void Gives(G g, House h)
    {
        int sum = DealingsSystem.GiftSizes.First(n => g.Tc.Dealings.GiftOwes(g.W, h, n));
        var gift = new DealingsCommand(g.Me.Id, DealingsCommand.Gift, h.Id, g.Tc.Dealings, sum.ToString()); Assert.True(g.Do(gift), gift.Refusal(g.Ctx));
        Assert.True(WealthSystem.FavourOwed(g.W, h.Id, g.Mine.Id));
    }
    /// <summary>The houses whose year's income a gift of 4,000 credits is a tenth of, so a gift leaves a favour owed.</summary>
    private static List<House> Small(G g) => Others(g).Where(h => g.Tc.Dealings.GiftOwes(g.W, h, DealingsSystem.GiftSizes.Max())).ToList();
    private static int AgainstWeight(G g, Motion m) => g.W.Houses.Values.Where(h => !World.Outsider(h.Id) && m.Votes.GetValueOrDefault(h.Id) < 0).Sum(h => g.Senate.Weight(g.W, h.Id));
    private static int Influence(G g) => g.W.PoolsFor(g.Me.Id).Influence;

    // ------------------------------------------------------------------ the favours called in on a motion against you: a_censure_on_the_docket/call_in_favours

    /// <summary>Two houses owe yours a favour, from gifts; a third moves a censure of your house, and the report comes.</summary>
    private static (G g, House a, House b, House mover, Motion censure, FiredEvent report) Censured(ulong seed = 7)
    {
        var g = Start(seed); var small = Small(g); var a = small[0]; var b = small[1]; Gives(g, a); Gives(g, b);
        var mover = Others(g).First(h => h.Id != a.Id && h.Id != b.Id && !WealthSystem.FavourOwed(g.W, h.Id, g.Mine.Id));
        var censure = TheyMove(g, mover, "censure", g.Mine.Id); Assert.True(g.W.HasFlag("motion_against_me"));
        Assert.True(g.W.HasFlag(VoteMarks.FavoursToCallFlag));   // the gate: houses that owe yours a favour, and a motion against your house
        return (g, a, b, mover, censure, ReportOf(g, "a_censure_on_the_docket"));
    }

    [Fact]
    public void ACensure_CallingInFavours_EveryHouseThatOwesYoursVotesAgainstIt_HoweverHardItLeansFor_AcrossASaveAndALoad()
    {
        var (g0, a0, b0, _, censure0, f) = Censured(); Leans(g0, censure0, a0, 6); Leans(g0, censure0, b0, 6);   // both lean hard for your censure
        int influence = Influence(g0);
        Assert.True(g0.Do(new ResolveChoiceCommand(new EventEngine(Content, WorldBuilder.RngFor(g0.W)), f, "call_in_favours", g0.Me.Id)));   // as the briefing answers it
        Assert.Equal(influence - 2, Influence(g0));   // the night's cost
        Assert.Equal(-1, censure0.Votes[a0.Id]); Assert.Equal(-1, censure0.Votes[b0.Id]);   // declared against it as the choice is made, so the whip count has it
        Assert.Equal(-1, g0.Senate.Stance(g0.W, censure0, a0.Id)); Assert.Equal(-1, g0.Senate.Stance(g0.W, censure0, b0.Id));
        Assert.False(WealthSystem.FavourOwed(g0.W, a0.Id, g0.Mine.Id)); Assert.False(WealthSystem.FavourOwed(g0.W, b0.Id, g0.Mine.Id));   // the favours are spent
        Assert.True(g0.Told($"{a0.Name} and {b0.Name} owed your house a favour, and declare against the motion against your house"));
        Assert.False(g0.W.HasFlag(VoteMarks.FavoursToCallFlag));   // nothing left to call: the choice closes
        var g = Reload(g0); var censure = g.W.Motions.First(m => m.Id == censure0.Id); var a = g.W.House(a0.Id)!; var b = g.W.House(b0.Id)!;   // a save and a load before the count
        Assert.True(g.Senate.Lean(g.W, censure, a.Id) > 0 && g.Senate.Lean(g.W, censure, b.Id) > 0, "they would have voted for it of themselves");
        Session(g);
        Assert.True(censure.Resolved); Assert.Equal(-1, censure.Votes[a.Id]); Assert.Equal(-1, censure.Votes[b.Id]);
        Assert.Equal(AgainstWeight(g, censure), censure.AgainstWeight);   // cast and counted
        Assert.Contains(g.W.History, h => h.Text.Contains($"{a.Name} declares against the motion of") && h.Text.Contains($"as it owed {g.Mine.Name} a favour"));
        Assert.Contains(g.Tc.Dealings.Lasting(g.W, g.Mine, a), x => x.text.Contains("paid with its vote: 1 time"));
        // spent on the censure, it is not owed again for your next motion
        Only(g); var levy = IMove(g); Assert.False(g.Senate.Owes(g.W, levy, a.Id));
    }

    [Fact]
    public void ACensure_CallingInFavours_TheMoverAndAHouseAtWarAreNotAsked_AndTheChoiceIsClosedWithItsReasonWhenNoneCanBe()
    {
        var g = Start(); var small = Small(g); var mover = small[0]; var enemy = small[1]; Gives(g, mover); Gives(g, enemy);
        g.Tc.Fleets.DeclareWar(g.W, g.Mine.Id, enemy.Id); Assert.True(g.W.AtWar(g.Mine.Id, enemy.Id));
        var censure = TheyMove(g, mover, "censure", g.Mine.Id);
        // the mover owes you a favour, but it will not vote against its own motion; a house at war gives no vote: nobody to call
        Assert.Empty(DealingsSystem.CallableAgainst(g.W, g.Mine.Id)); Assert.False(g.W.HasFlag(VoteMarks.FavoursToCallFlag));
        var f = ReportOf(g, "a_censure_on_the_docket"); var engine = new EventEngine(Content, WorldBuilder.RngFor(g.W));
        Assert.False(engine.IsChoiceAvailable(f, g.W, "call_in_favours"));
        var choice = f.Def.Choices.First(c => c.Id == "call_in_favours");
        Assert.Contains("needs a house that owes yours a favour and can vote against the motion", ChoiceStakes.Unmet(choice, new EvalContext(g.W, f.Binding, WorldBuilder.RngFor(g.W))));
        // a third house comes to owe a favour: the choice opens, and calls in that one only
        var friend = small.First(h => h.Id != mover.Id && h.Id != enemy.Id); Gives(g, friend); VoteMarks.Docket(g.W, g.Senate, g.Mine.Id);
        Assert.True(engine.IsChoiceAvailable(f, g.W, "call_in_favours")); Answer(g, f, "call_in_favours");
        Assert.Equal(-1, censure.Votes[friend.Id]); Assert.False(censure.Votes.ContainsKey(mover.Id)); Assert.False(censure.Votes.ContainsKey(enemy.Id));
        Assert.True(WealthSystem.FavourOwed(g.W, mover.Id, g.Mine.Id));   // the mover's favour waits for a motion of yours
        Session(g); Assert.Equal(-1, censure.Votes[friend.Id]); Assert.True(WealthSystem.FavourOwed(g.W, mover.Id, g.Mine.Id));
        WealthSystem.Tick(g.W); Assert.False(WealthSystem.FavourOwed(g.W, enemy.Id, g.Mine.Id)); Assert.True(g.Told($"You are at war with {enemy.Name}: the vote it owed your house will not be given"));
    }

    [Fact]
    public void ACensure_CallingInFavours_ANewHeadOfAHouseCalledKeepsItsVote_AndTheWhipCannotUndoIt()
    {
        var (g, a, _, _, censure, f) = Censured(); Leans(g, censure, a, 6);
        Answer(g, f, "call_in_favours"); var head = g.HeadOf(a);
        DynastySystem.Die(g.W, head.Id); Assert.NotEqual(head.Id, Economy.HeadOf(g.W, a)?.Id);
        // whipping it your way again would strike the vote it declared: refused
        Assert.Equal("it has declared against it already", new LobbyCommand(g.Me.Id, censure.Id, a.Id, -1).Refusal(g.Ctx));
        Session(g); Assert.Equal(-1, censure.Votes[a.Id]);
    }

    // ------------------------------------------------------------------ a debt called in: the_favor_called/call_it_in

    /// <summary>Someone of another house owes you a turn (owes_you, as a judgement given their way leaves it), and the report comes.</summary>
    private static (G g, House theirs, Character debtor, FiredEvent report) Indebted(Action<G, House>? before = null)
    {
        var g = Start(); var theirs = Others(g).First(h => g.W.LivingCharacters().Any(c => c.HouseId == h.Id && c.Age >= DynastySystem.AgeOfMajority && c.Id != g.HeadOf(h).Id));
        var debtor = g.W.LivingCharacters().Where(c => c.HouseId == theirs.Id && c.Age >= DynastySystem.AgeOfMajority && c.Id != g.HeadOf(theirs).Id).OrderBy(c => c.Id, StringComparer.Ordinal).First();
        foreach (var c in g.W.Characters.Values) c.Flags.Remove("owes_you"); debtor.Flags.Add("owes_you");
        before?.Invoke(g, theirs);
        var (f, cast) = Report(g, "the_favor_called", "debtor"); Assert.Equal(debtor.Id, cast.Id);
        return (g, theirs, debtor, f);
    }

    [Fact]
    public void TheFavourCalled_TheDebtIsPaidWithTheirHousesVoteOnYourMotion_HoweverHardItLeansAgainst_AcrossASaveAndALoad()
    {
        Motion levy0 = null!;
        var (g0, theirs0, debtor, f) = Indebted((g, theirs) => { levy0 = IMove(g); HardAgainst(g, levy0, theirs); });
        int influence = g0.Mine.Treasury.Influence, regard = g0.W.GetRelationship(debtor.Id, g0.Me.Id)?.Disposition ?? 0;
        Answer(g0, f, "call_it_in");
        Assert.Equal(1, levy0.Votes[theirs0.Id]);   // the vote where you need one, declared as the choice is made
        Assert.True(g0.Told($"{debtor.Name} pays what they owed you with the vote of {theirs0.Name}: it declares for your motion"));
        Assert.Equal(influence + 3, g0.Mine.Treasury.Influence); Assert.Equal(regard - 10, g0.W.GetRelationship(debtor.Id, g0.Me.Id)!.Disposition);   // and what the chips said
        Assert.DoesNotContain("owes_you", debtor.Flags); Assert.DoesNotContain(VoteMarks.DebtCalledChar, debtor.Flags);
        Assert.DoesNotContain(g0.W.History, h => h.Text.Contains("a vote, a signature"));
        var g = Reload(g0); var levy = g.W.Motions.First(m => m.Id == levy0.Id); var theirs = g.W.House(theirs0.Id)!;
        Assert.True(g.Senate.Lean(g.W, levy, theirs.Id) < 0); Session(g);
        Assert.True(levy.Resolved); Assert.Equal(1, levy.Votes[theirs.Id]); Assert.Equal(ForWeight(g, levy), levy.ForWeight);
    }

    [Fact]
    public void TheFavourCalled_AMotionAgainstYourHouseComesFirst_OneThatStrikesAtTheirsIsNotAsked_AndWithNoneTheVoteIsKeptForYourNext()
    {
        // a censure of their own house is the only motion: nobody votes for their own censure, so the vote is kept: their house owes yours one, for your next motion
        var (g, theirs, debtor, f) = Indebted((g, theirs) => IMove(g, "censure", theirs.Id));
        var censureOfThem = g.W.Motions.Single(m => m.Open && m.ProposerHouseId == g.Mine.Id);
        Answer(g, f, "call_it_in");
        Assert.False(censureOfThem.Votes.ContainsKey(theirs.Id)); Assert.True(g.Told($"No motion before the Senate wants the vote of {theirs.Name} now, so {debtor.Name} owes it instead: {theirs.Name} owes your house a vote."));
        Assert.True(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id)); Assert.False(g.Senate.Owes(g.W, censureOfThem, theirs.Id));   // not on the censure of their own house
        Session(g); Only(g); var next = IMove(g); HardAgainst(g, next, theirs); Session(g); Assert.Equal(1, next.Votes[theirs.Id]);   // something you can hold: their house's vote on your next motion
        // your own motion and a censure of your house by a third: the vote goes against the censure
        Motion levy = null!, censure = null!;
        var (g2, theirs2, debtor2, f2) = Indebted((g, theirs) => { levy = IMove(g); var third = Others(g).First(h => h.Id != theirs.Id); censure = g.Senate.Add(g.W, third.Id, "censure", g.Mine.Id); Leans(g, censure, theirs, 6); });
        Answer(g2, f2, "call_it_in");
        Assert.Equal(-1, censure.Votes[theirs2.Id]); Assert.False(levy.Votes.ContainsKey(theirs2.Id));
        Session(g2); Assert.Equal(-1, censure.Votes[theirs2.Id]); Assert.Equal(AgainstWeight(g2, censure), censure.AgainstWeight);
        // at war with their house: no vote
        var (g3, theirs3, debtor3, f3) = Indebted((g, theirs) => { IMove(g); g.Tc.Fleets.DeclareWar(g.W, g.Mine.Id, theirs.Id); });
        Answer(g3, f3, "call_it_in"); Assert.True(g3.Told($"You are at war with {theirs3.Name}, so {debtor3.Name} pays what they owed in other ways: a house at war with yours gives you no vote"));
        Assert.DoesNotContain(g3.W.Motions, m => m.Open && m.Votes.ContainsKey(theirs3.Id));
    }

    // ------------------------------------------------------------------ the favour you owe, judged at the count: WealthSystem.FavoursCalled

    /// <summary>You ask a house for coin on the house window, and owe it a favour for it.</summary>
    private static (G g, House theirs) Asked()
    {
        var g = Start(); var theirs = Others(g)[0]; theirs.Treasury.Credits = 60; g.W.Relationship(g.HeadOf(theirs).Id, g.Me.Id).Disposition = 30;
        var ask = new DealingsCommand(g.Me.Id, DealingsCommand.AskCoin, theirs.Id, g.Tc.Dealings); Assert.True(g.Do(ask), ask.Refusal(g.Ctx));
        Assert.True(WealthSystem.FavourOwed(g.W, g.Mine.Id, theirs.Id)); return (g, theirs);
    }

    [Fact]
    public void AFavourYouOweForCoin_VotedAgainst_ANewHeadThinksTheWorse_TheChronicleNamesNoGift_AndItIsWrittenDown_AcrossASaveAndALoad()
    {
        var (g0, theirs0) = Asked(); var old = g0.HeadOf(theirs0);
        var g = Reload(g0); var theirs = g.W.House(theirs0.Id)!;
        DynastySystem.Die(g.W, old.Id); var heir = Economy.HeadOf(g.W, theirs)!; Assert.NotEqual(old.Id, heir.Id);
        var m = g.Senate.Add(g.W, theirs.Id, "levy", ""); Assert.True(g.Told($"{theirs.Name} has moved a motion, and you owe it a favour: it is called in at this motion's count"));   // the reminder
        IVote(g, m, -1); int before = g.Regard(theirs); Session(g);
        Assert.True(m.Resolved); Assert.Equal(before - WealthSystem.FavourBroken, g.Regard(theirs));   // the new head remembers, -15
        Assert.True(g.Told($"{theirs.Name} called in the favour you owed it on its motion, and you did not vote for it. Its head thinks -15 of you."));
        var line = Assert.Single(g.W.History, h => h.Text.Contains($"{theirs.Name} called in the favour your house owed it"));
        Assert.DoesNotContain("gift", line.Text); Assert.Contains($"{heir.Name} will remember it", line.Text);
        Assert.Contains(g.Tc.Dealings.Lasting(g.W, g.Mine, theirs), x => x.text == "Favours it called in: honoured 0 times, refused 1 time.");   // what it remembers
        Assert.False(WealthSystem.FavourOwed(g.W, g.Mine.Id, theirs.Id));
    }

    [Fact]
    public void AFavourYouOweForCoin_VotedFor_IsEven_PlusFive()
    {
        var (g, theirs) = Asked(); var m = g.Senate.Add(g.W, theirs.Id, "levy", ""); Leans(g, m, g.Mine, -6);
        IVote(g, m, 1); int before = g.Regard(theirs); Session(g);
        Assert.Equal(before + WealthSystem.FavourKept, g.Regard(theirs)); Assert.True(g.Told("you voted for it. The account is even, and its head thinks +5 of you."));
        Assert.Contains(g.W.History, h => h.Text.Contains($"{theirs.Name} called in the favour your house owed it, and you voted for its motion. The account is even."));
        Assert.False(WealthSystem.FavourOwed(g.W, g.Mine.Id, theirs.Id));
    }

    // ------------------------------------------------------------------ your word given on the house window: DealingsSystem.PledgeSays

    /// <summary>A house moves a motion the count will be close on, so it wants your word; your house leans hard against it.</summary>
    private static (G g, House theirs, Motion m) Pledged()
    {
        var g = Start(); var theirs = Others(g)[0]; g.W.Relationship(g.HeadOf(theirs).Id, g.Me.Id).Disposition = 0;
        var m = TheyMove(g, theirs); Close(g, m, theirs); Leans(g, m, g.Mine, -6);
        var pledge = new DealingsCommand(g.Me.Id, DealingsCommand.Pledge, theirs.Id, g.Tc.Dealings, m.Id); Assert.True(g.Do(pledge), pledge.Refusal(g.Ctx));
        return (g, theirs, m);
    }
    private static void Close(G g, Motion m, House theirs) { foreach (var h in g.W.Houses.Values.Where(h => !World.Outsider(h.Id) && h.Id != theirs.Id && h.Id != g.Mine.Id)) m.Votes[h.Id] = 0; }

    [Fact]
    public void YourWordOnTheHouseWindow_IsDeclared_YouAreReminded_AndKeptItIsAFavourAndPlusThree_AcrossASaveAndALoad()
    {
        var (g0, theirs0, m0) = Pledged();
        Assert.Equal(1, m0.Votes[g0.Mine.Id]);   // your vote declared for it, however your house leans
        Assert.True(g0.Told($"You gave {theirs0.Name} your word: your vote on its motion is declared for it. Keep it at the count and {theirs0.Name} owes you a favour, and its head thinks +3 of you; change it and its head thinks -15 of you, and the house takes no pledge of yours for 8 years."));
        var g = Reload(g0); var theirs = g.W.House(theirs0.Id)!; var m = g.W.Motions.First(x => x.Id == m0.Id);
        int before = g.Regard(theirs); Session(g);
        Assert.Equal(1, m.Votes[g.Mine.Id]); Assert.Equal(ForWeight(g, m), m.ForWeight);   // cast and counted
        Assert.Equal(before + DealingsSystem.PledgeKeptRegard, g.Regard(theirs)); Assert.True(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id));
        Assert.True(g.Told($"You voted as you gave {theirs.Name} your word you would. Its head thinks +3 of you, and it owes your house a favour."));
        // the favour it owes is a vote: it votes for your next motion, however it leans
        Only(g); var levy = IMove(g); HardAgainst(g, levy, theirs); Assert.True(g.Senate.Owes(g.W, levy, theirs.Id));
    }

    [Fact]
    public void YourWordOnTheHouseWindow_Broken_IsMinusFifteen_YouAreRemindedAsYouChangeIt_AndNoPledgeIsTakenForEightYears_EvenInACrowdedRecord()
    {
        var (g, theirs, m) = Pledged(); var head = g.HeadOf(theirs);
        IVote(g, m, -1); Assert.True(g.Told($"Your vote on this motion is pledged to {theirs.Name}, for it. Leave it so at the count, or its head thinks -15 of you"));
        DynastySystem.Die(g.W, head.Id); var heir = Economy.HeadOf(g.W, theirs)!; int before = g.W.GetRelationship(heir.Id, g.Me.Id)?.Disposition ?? 0;   // a new head judges it
        Session(g); int year = g.W.Turn;
        Assert.Equal(-1, m.Votes[g.Mine.Id]); Assert.Equal(before - DealingsSystem.PledgeBrokenRegard, g.W.GetRelationship(heir.Id, g.Me.Id)!.Disposition);
        Assert.True(g.Told($"You gave {theirs.Name} your word on a vote and did not keep it. Its head thinks -15 of you, and it takes no pledge of yours for 8 years."));
        Assert.False(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id));
        // a crowded record: three hundred dealings between other houses after it, and the broken word is still read
        var two = Others(g).Where(h => h.Id != theirs.Id).Take(2).ToList();
        for (int i = 0; i < DealingsSystem.RememberMost + 50; i++) DealingsSystem.Remember(g.W, DealingsSystem.GiftKind, two[0].Id, two[1].Id, 2);
        g.Tc.Dealings.Tick(g.W); Assert.Contains(g.W.Dealings, d => d.Kind == DealingsSystem.PledgeBrokenKind && d.FromHouseId == g.Mine.Id && d.ToHouseId == theirs.Id);
        Only(g); var next = g.Senate.Add(g.W, theirs.Id, "relief", g.W.Galaxy!.ReachRegions.OrderBy(r => r.Id, StringComparer.Ordinal).First().Id); Close(g, next, theirs);
        var again = new DealingsCommand(g.Me.Id, DealingsCommand.Pledge, theirs.Id, g.Tc.Dealings, next.Id);
        Assert.Equal($"you broke your word to it in year {year}: it takes no pledge of yours before year {year + DealingsSystem.PledgeMemoryYears}", again.Refusal(g.Ctx));
        g.W.Turn = year + DealingsSystem.PledgeMemoryYears - 1; Assert.StartsWith("you broke your word to it", again.Refusal(g.Ctx));
        g.W.Turn = year + DealingsSystem.PledgeMemoryYears; Assert.Null(again.Refusal(g.Ctx));   // eight years on, it takes your word again
    }

    // ------------------------------------------------------------------ a favour called in for a vote: DealingsSystem.CallVote

    [Fact]
    public void AFavourCalledInForAVote_TheHouseDeclaresYourWay_HoweverHardItLeans_AndOwesNothingMore_AcrossASaveAndALoad()
    {
        var g0 = Start(); var owes0 = Small(g0)[0]; Gives(g0, owes0);
        var mover = Others(g0).First(h => h.Id != owes0.Id); var m0 = TheyMove(g0, mover); IVote(g0, m0, -1); Leans(g0, m0, owes0, 6);   // you are against; it leans hard for
        var call = new DealingsCommand(g0.Me.Id, DealingsCommand.CallVote, owes0.Id, g0.Tc.Dealings, m0.Id); Assert.True(g0.Do(call), call.Refusal(g0.Ctx));
        Assert.Equal(-1, m0.Votes[owes0.Id]); Assert.False(WealthSystem.FavourOwed(g0.W, owes0.Id, g0.Mine.Id));
        Assert.Contains(g0.W.History, h => h.Text.Contains($"calls in what {owes0.Name} owes: it declares against the motion, and owes nothing more."));
        Assert.Equal("it has declared against it already", new LobbyCommand(g0.Me.Id, m0.Id, owes0.Id, -1).Refusal(g0.Ctx));   // a whip your way would strike it
        var g = Reload(g0); var owes = g.W.House(owes0.Id)!; var m = g.W.Motions.First(x => x.Id == m0.Id);
        Assert.True(g.Senate.Lean(g.W, m, owes.Id) > 0); Session(g);
        Assert.Equal(-1, m.Votes[owes.Id]); Assert.Equal(AgainstWeight(g, m), m.AgainstWeight);   // for certain, and counted
        Assert.Contains(g.Tc.Dealings.Lasting(g.W, g.Mine, owes), x => x.text.Contains("paid with its vote: 1 time"));
    }

    [Fact]
    public void AFavourCalledInForAVote_IsRefusedOnAMotionThatNamesTheirOwnHouse()
    {
        var g = Start(); var owes = Small(g)[0]; Gives(g, owes);
        var third = Others(g).First(h => h.Id != owes.Id); var censure = TheyMove(g, third, "censure", owes.Id); IVote(g, censure, 1);
        Assert.True(g.Senate.Lean(g.W, censure, owes.Id) < 0);
        Assert.Equal("the motion names their own house: they will not vote that way on it", new DealingsCommand(g.Me.Id, DealingsCommand.CallVote, owes.Id, g.Tc.Dealings, censure.Id).Refusal(g.Ctx));
        Assert.True(WealthSystem.FavourOwed(g.W, owes.Id, g.Mine.Id));
    }

    // ------------------------------------------------------------------ a pledge of your seats at the consular election: ElectionSystem.PledgeSays

    /// <summary>The year before the consuls are elected, the Old Lion's house not standing.</summary>
    private static G ElectionYear() { var g = Start(); g.W.Turn = ElectionSystem.Every - 1; Assert.False(ElectionSystem.Due(g.W)); return g; }
    private static Func<string, int> Wt(G g) => id => g.Senate.Weight(g.W, id);
    private static Dictionary<string, int> TallyNow(G g) => ElectionSystem.Tally(g.W, g.Senate.Offices, Wt(g), ElectionSystem.Nominees(g.W, g.Senate.Offices, Wt(g)));

    [Fact]
    public void APledgeOfYourSeats_IsCountedForTheNominee_NotYourBenchsNorYourPatrons_AndElectedItOwesYouAFavour_AcrossASaveAndALoad()
    {
        var g0 = ElectionYear(); var noms = ElectionSystem.Nominees(g0.W, g0.Senate.Offices, Wt(g0)); string? own = ElectionSystem.BenchNominee(g0.W, g0.Senate.Offices, Wt(g0), g0.Mine.Id);
        Assert.NotNull(own);
        // your patron is a nominee too: the pledge goes before it
        var before = TallyNow(g0); string to = before.Where(kv => kv.Key != own).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First().Key;
        string patron = noms.Select(n => n.houseId).First(h => h != own && h != to); g0.Mine.PatronId = patron;
        before = TallyNow(g0); int seats = g0.Senate.Weight(g0.W, g0.Mine.Id);
        var toHouse = g0.W.House(to)!; int regard = g0.Regard(toHouse), ownRegard = g0.Regard(g0.W.House(own!)!);
        var cmd = new PledgeVotesCommand(g0.Me.Id, to, g0.Senate.Offices, g0.Senate); Assert.True(g0.Do(cmd), cmd.Refusal(g0.Ctx));
        Assert.Equal(regard + ElectionSystem.PledgeRegard, g0.Regard(toHouse)); Assert.Equal(ownRegard - ElectionSystem.BenchSlight, g0.Regard(g0.W.House(own!)!));   // +8, and -5 from your bench's nominee
        Assert.StartsWith("your seats are pledged to", new StandForConsulCommand(g0.Me.Id, g0.Senate.Offices).Refusal(g0.Ctx));   // you cannot stand on them as well
        var g = Reload(g0);   // a save and a load before the count
        var tally = TallyNow(g); Assert.Equal(before[to] + seats, tally[to]); Assert.Equal(before[patron] - seats, tally[patron]); Assert.Equal(before[own!], tally[own!]);
        bool elected = tally.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(2).Any(kv => kv.Key == to);
        Session(g); Assert.True(g.W.HasFlag("election_held"));
        Assert.Contains(g.W.History, h => h.Text.StartsWith("The Senate elects its consuls") && h.Text.Contains($"{toHouse.Name} ({tally[to]})"));   // counted at the count
        Assert.Equal(elected, WealthSystem.FavourOwed(g.W, to, g.Mine.Id)); Assert.Null(ElectionSystem.PledgedTo(g.W, g.Mine.Id));
        if (elected) Assert.True(g.Told($"{toHouse.Name} is elected with the seats you pledged, and owes your house a favour."));
        Assert.True(elected, "the test picks the nominee your seats elect");
    }

    [Fact]
    public void APledgeOfYourSeats_TakenBackCostsTen_AndOneWhoNoLongerStandsAtTheCountLapsesItWithANotice()
    {
        var g = ElectionYear(); string? own = ElectionSystem.BenchNominee(g.W, g.Senate.Offices, Wt(g), g.Mine.Id);
        string to = ElectionSystem.Nominees(g.W, g.Senate.Offices, Wt(g)).Select(n => n.houseId).First(h => h != own); var toHouse = g.W.House(to)!;
        Assert.True(g.Do(new PledgeVotesCommand(g.Me.Id, to, g.Senate.Offices, g.Senate))); int regard = g.Regard(toHouse);
        Assert.True(g.Do(new PledgeVotesCommand(g.Me.Id, "", g.Senate.Offices, g.Senate))); Assert.Equal(regard - ElectionSystem.UnpledgeRegard, g.Regard(toHouse));
        // given again, and then it is no longer standing when the consuls are elected: elected last term
        Assert.True(g.Do(new PledgeVotesCommand(g.Me.Id, to, g.Senate.Offices, g.Senate)));
        g.W.WorldCounters["consul_term:" + to] = g.W.Turn; Assert.DoesNotContain(ElectionSystem.Nominees(g.W, g.Senate.Offices, Wt(g)), n => n.houseId == to);
        Session(g);
        Assert.True(g.Told($"{toHouse.Name} was not standing when the consuls were elected, so the seats you pledged it went as your bench or your patron had them, and it owes you nothing for them."));
        Assert.False(WealthSystem.FavourOwed(g.W, to, g.Mine.Id)); Assert.Null(ElectionSystem.PledgedTo(g.W, g.Mine.Id));
    }

    // ------------------------------------------------------------------ the backing of your patron or bench leader for consul: AskAbove.Says

    [Fact]
    public void YourPatronsBacking_ItsSeatsAreCountedForYou_EvenWhenItWouldBeItsOwnBenchsNominee_AndTheFavourIsJudgedAtItsNextMotion_AcrossASaveAndALoad()
    {
        var g0 = ElectionYear(); var w0 = g0.W; var offices = g0.Senate.Offices;
        Assert.True(g0.Do(new StandForConsulCommand(g0.Me.Id, offices)));
        // a patron on another bench, the second weightiest of it that may stand: not its nominee when you ask
        var bench = w0.Factions.First(f => !f.Members.Contains(g0.Mine.Id) && f.Members.Count(h => ElectionSystem.Eligible(w0, offices, h)) >= 3);
        var eligible = bench.Members.Where(h => ElectionSystem.Eligible(w0, offices, h)).OrderByDescending(Wt(g0)).ThenBy(h => h, StringComparer.Ordinal).ToList();
        string nominee = eligible[0], patronId = eligible[1]; var patron = w0.House(patronId)!;
        g0.Mine.PatronId = patronId; w0.Relationship(g0.HeadOf(patron).Id, g0.Me.Id).Disposition = 20;
        var ask = new AskAboveCommand(g0.Me.Id, patronId, AskAbove.Backing, offices, g0.Senate); Assert.True(g0.Do(ask), ask.Refusal(g0.Ctx));
        Assert.Equal(g0.Mine.Id, ElectionSystem.PledgedTo(w0, patronId)); Assert.True(WealthSystem.FavourOwed(w0, g0.Mine.Id, patronId));
        // then its bench's nominee may no longer stand (it held the consulship last term), and the patron is now the weightiest of it that may
        w0.WorldCounters["consul_term:" + nominee] = w0.Turn; Assert.True(ElectionSystem.Eligible(w0, offices, patronId));
        var g = Reload(g0); var w = g.W;
        var noms = ElectionSystem.Nominees(w, offices, Wt(g)); Assert.DoesNotContain(noms, n => n.houseId == patronId);   // it does not stand against the one it backs
        int mineBefore = ElectionSystem.Tally(w, offices, Wt(g), noms)[g.Mine.Id];
        w.WorldFlags.Remove(ElectionSystem.PledgePrefix + patronId + ">" + g.Mine.Id); int without = ElectionSystem.Tally(w, offices, Wt(g), ElectionSystem.Nominees(w, offices, Wt(g)))[g.Mine.Id]; w.WorldFlags.Add(ElectionSystem.PledgePrefix + patronId + ">" + g.Mine.Id);
        Assert.Equal(without + g.Senate.Weight(w, patronId), mineBefore);   // its seats are counted for you
        Session(g);
        Assert.Contains(w.History, h => h.Text.StartsWith("The Senate elects its consuls") && h.Text.Contains($"{g.Mine.Name} ({mineBefore})"));
        Assert.True(WealthSystem.FavourOwed(w, g.Mine.Id, patronId));   // and you owe it a favour: vote for its next motion and the account is even
        Only(g); var theirs = g.Senate.Add(w, patronId, "levy", ""); Assert.True(g.Told($"{w.House(patronId)!.Name} has moved a motion, and you owe it a favour"));
        IVote(g, theirs, 1); int before = g.Regard(w.House(patronId)!); Session(g);
        // +5, the account even; and +5 more as your patron, for a client's vote as it wished (SenateSystem.PatronWish)
        Assert.Equal(before + WealthSystem.FavourKept + SenateSystem.ObligeRegard, g.Regard(w.House(patronId)!)); Assert.False(WealthSystem.FavourOwed(w, g.Mine.Id, patronId));
        Assert.True(g.Told($"{w.House(patronId)!.Name} called in the favour you owed it on its motion, and you voted for it. The account is even"));
    }

    [Fact]
    public void YourBenchLeadersBacking_IsCountedForYouInsideTheBench_AndAtTheCount()
    {
        var g = ElectionYear(); var w = g.W; var offices = g.Senate.Offices; var bench = FactionSystem.Of(w, g.Mine.Id)!;
        // another house leads your bench, and the bench thinks better of you than of it: you are its nominee when you stand
        string leaderId = bench.Members.Where(h => h != g.Mine.Id && ElectionSystem.Eligible(w, offices, h)).OrderByDescending(Wt(g)).ThenBy(h => h, StringComparer.Ordinal).First();
        bench.LeaderHouseId = leaderId; var leader = w.House(leaderId)!; var leaderHead = g.HeadOf(leader);
        foreach (var m in bench.Members.Where(h => h != g.Mine.Id && h != leaderId)) if (Economy.HeadOf(w, w.House(m)!) is { } hd) { w.Relationship(hd.Id, g.Me.Id).Disposition = 20; w.Relationship(hd.Id, leaderHead.Id).Disposition = 0; }
        w.Relationship(leaderHead.Id, g.Me.Id).Disposition = 20;
        Assert.True(g.Do(new StandForConsulCommand(g.Me.Id, offices)));
        var ask = new AskAboveCommand(g.Me.Id, leaderId, AskAbove.Backing, offices, g.Senate); Assert.True(g.Do(ask), ask.Refusal(g.Ctx));
        var contest = ElectionSystem.BenchContest(w, offices, Wt(g), bench)!.Value;
        Assert.NotEqual(leaderId, contest.rivalId); Assert.Contains(leaderId, contest.forMine);   // inside the bench: its seats are behind you, and it does not stand
        var noms = ElectionSystem.Nominees(w, offices, Wt(g)); Assert.Contains(noms, n => n.houseId == g.Mine.Id && n.partyId == bench.Id); Assert.DoesNotContain(noms, n => n.houseId == leaderId);
        var tally = ElectionSystem.Tally(w, offices, Wt(g), noms); Session(g);
        Assert.Contains(w.History, h => h.Text.StartsWith("The Senate elects its consuls") && h.Text.Contains($"{g.Mine.Name} ({tally[g.Mine.Id]})"));
    }

    // ================================================================== group "office"

    /// <summary>The player's answer, as the briefing sends it: the cost is paid.</summary>
    private static void Choose(G g, FiredEvent f, string choice) =>
        Assert.True(g.Do(new ResolveChoiceCommand(new EventEngine(Content, WorldBuilder.RngFor(g.W)), f, choice, g.Me.Id)), $"{f.Def.Id}/{choice} is not open");
    /// <summary>A house moves a motion, and nothing else is before the Senate; no session is held for it.</summary>
    private static Motion Moves(G g, House h, string kind = "levy", string target = "") { var m = g.Senate.Add(g.W, h.Id, kind, target); Only(g, m); return m; }
    /// <summary>What the count of the ladder gave this person.</summary>
    private static int AtTheCount(G g, Character c) => g.W.Counts.Single(x => x.CharacterId == c.Id).Votes;
    /// <summary>Bastien Castellan, of your blood.</summary>
    private static Character Bastien(G g) => g.W.Char("bastien_castellan")!;
    private static OfficeDef Seat(string id) => Content.Offices.Def(id)!;
    private static Character? Holder(G g, string office) => OfficeSystem.Holding(g.W, office) is { } h ? g.W.Char(h.CharacterId) : null;

    // ------------------------------------------------------------------ one of yours stands: one_of_yours_stands_for_office, LadderSystem.Campaign

    /// <summary>Bastien Castellan, of your blood, who has not yet been praetor here, puts his own name down for it in the year before
    /// the count, and the report comes.</summary>
    private static (G g, FiredEvent report) Stands()
    {
        var g = Start(); var kin = Bastien(g); kin.Cursus.Remove(LadderSystem.Praetor); Assert.Null(LadderSystem.Bar(g.W, g.Senate.Offices, kin, g.Senate.Offices.Rung(LadderSystem.Praetor)));
        LadderSystem.Stand(g.W, g.Senate.Offices, kin, LadderSystem.Praetor);   // his own doing: the head cannot put a name down for another
        var (f, cast) = Report(g, "one_of_yours_stands_for_office", "kin"); Assert.Equal(kin.Id, cast.Id);
        return (g, f);
    }

    [Fact]
    public void OneOfYoursStands_TheHouseSpeaksForThemAndPaysForACanvass_AndTheCountHasBoth_AcrossASaveAndALoad()
    {
        // two games alike in all but this: in one the house speaks for her and pays for a modest canvass, in the other it does neither
        var (g0, f0) = Stands(); Answer(g0, f0, "let_them_win_it");
        var (h0, f1) = Stands(); Answer(h0, f1, "let_them_win_it");
        var kin = Bastien(h0); int influence = Influence(h0), treasury = h0.Mine.Treasury.Credits;
        var back = new LadderCommand(h0.Me.Id, LadderCommand.Back, kin.Id, "", h0.Senate.Offices); Assert.True(h0.Do(back), back.Refusal(h0.Ctx));
        var fund = new FundCampaignCommand(h0.Me.Id, kin.Id, LadderSystem.Modest, h0.Senate.Offices); Assert.True(h0.Do(fund), fund.Refusal(h0.Ctx));
        Assert.Equal(influence - LadderSystem.BackInfluence, Influence(h0)); Assert.Equal(treasury - LadderSystem.CampaignCost(LadderSystem.Modest), h0.Mine.Treasury.Credits);
        var h = Reload(h0);   // a save and a load before the count
        Session(g0); Session(h); Assert.True(h.W.HasFlag(LadderSystem.HeldFlag));
        Assert.Equal(AtTheCount(g0, Bastien(g0)) + LadderSystem.BackVotes + LadderSystem.CampaignVotes(LadderSystem.Modest), AtTheCount(h, Bastien(h)));   // +6 spoken for, +3 the canvass
    }

    [Fact]
    public void TheCoinBehindACanvass_BuysItsVotesAtTheCount_AndALargerOnePaysTheDifferenceForTheLargerOnly()
    {
        var (g, f) = Stands(); Answer(g, f, "let_them_win_it"); var (h, f2) = Stands(); Answer(h, f2, "let_them_win_it"); var kin = Bastien(h);
        Assert.True(h.Do(new FundCampaignCommand(h.Me.Id, kin.Id, LadderSystem.Modest, h.Senate.Offices))); int treasury = h.Mine.Treasury.Credits;
        Assert.True(h.Do(new FundCampaignCommand(h.Me.Id, kin.Id, LadderSystem.Handsome, h.Senate.Offices)));
        Assert.Equal(treasury - (LadderSystem.CampaignCost(LadderSystem.Handsome) - LadderSystem.CampaignCost(LadderSystem.Modest)), h.Mine.Treasury.Credits);
        Assert.Contains(h.W.History, x => x.Text.Contains("a handsome canvass"));
        Session(g); Session(h);
        Assert.Equal(AtTheCount(g, Bastien(g)) + LadderSystem.CampaignVotes(LadderSystem.Handsome), AtTheCount(h, Bastien(h)));   // 6, not 3 and 6
    }

    [Fact]
    public void TeachThemTheFloor_TheyHaveTwoMoreVotesAtTheCountOfTheirOwn()
    {
        var (g, f) = Stands(); Answer(g, f, "let_them_win_it");
        var (h, f2) = Stands(); int merit = LadderSystem.Merit(Bastien(h)); Answer(h, f2, "teach_them_the_floor");
        Assert.Equal(merit + 2, LadderSystem.Merit(Bastien(h)));   // a point of oratory, counted twice
        Session(g); Session(h);
        Assert.True(AtTheCount(h, Bastien(h)) >= AtTheCount(g, Bastien(g)) + 2, $"{AtTheCount(h, Bastien(h))} against {AtTheCount(g, Bastien(g))}");
        Assert.Contains(h.W.History, x => x.Text.Contains("They will speak better at the count for it."));
    }

    // ------------------------------------------------------------------ the count read again: the_one_who_won_the_office/demand_a_recount

    [Fact]
    public void TheCountReadAgain_YourRelativeThinksEightTheBetter_TheSquaresTrustTheCountLess_AndTheResultStands()
    {
        var (g, f) = Stands(); Answer(g, f, "let_them_win_it"); var kin = Bastien(g);
        foreach (var h in Others(g)) if (LadderSystem.Candidates(g.W, g.Senate.Offices, g.Senate.Offices.Rung(LadderSystem.Praetor)!).FirstOrDefault(c => c.HouseId == h.Id) is { } rival)
            g.W.Campaigns.Add(new CampaignFund { HouseId = h.Id, CharacterId = rival.Id, RungId = LadderSystem.Praetor, Turn = g.W.Turn, Size = LadderSystem.Lavish, Spent = 40, Votes = 500 });   // the other houses outspend him
        Session(g); Assert.Contains(VoteMarks.OursLostChar, kin.Flags);
        var counts = g.W.Counts.Select(c => (c.CharacterId, c.Votes, c.Won)).ToList();
        var (report, victor) = Report(g, "the_one_who_won_the_office", "victor"); var victorHouse = g.W.House(victor.HouseId)!;
        int regard = g.W.GetRelationship(kin.Id, g.Me.Id)?.Disposition ?? 0, trust = LadderSystem.Trust(g.W), mine = g.Mine.Legitimacy, theirs = victorHouse.Legitimacy;
        Choose(g, report, "demand_a_recount");
        Assert.Equal(regard + 8, g.W.GetRelationship(kin.Id, g.Me.Id)!.Disposition);   // "{kin.name} will know the house fought for them"
        Assert.Equal(trust - 1, LadderSystem.Trust(g.W));                              // "the squares will doubt the count"
        Assert.Equal(theirs - 1, victorHouse.Legitimacy); Assert.Equal(mine - 1, g.Mine.Legitimacy);
        Assert.Equal(counts, g.W.Counts.Select(c => (c.CharacterId, c.Votes, c.Won)).ToList());   // "It stood, as counts do."
        Assert.DoesNotContain(g.W.Magistrates, x => x.CharacterId == kin.Id);
    }

    // ------------------------------------------------------------------ the consul's table: the_consul_elects_feast

    /// <summary>The consuls are elected at the end of year ten, your house not standing; the Senior Consul's table, and its report.</summary>
    private static (G g, Character consul, FiredEvent report) Feast()
    {
        var g = Start(); g.W.Turn = ElectionSystem.Every - 1; Session(g); Assert.True(g.W.HasFlag("election_held")); Assert.False(g.W.HasFlag("consul_elected"));
        var consul = Holder(g, "consul_senior")!; var (f, cast) = Report(g, "the_consul_elects_feast", "grandee"); Assert.Equal(consul.Id, cast.Id);
        return (g, consul, f);
    }

    [Fact]
    public void TheSeniorConsul_Presides_SpeaksFromTheChair_BreaksATie_AndStrikesOneMotionATerm()
    {
        var (g, consul, f) = Feast(); Answer(g, f, "stay_away"); var house = g.W.House(consul.HouseId)!;
        Assert.Equal(consul.Id, LadderSystem.Presiding(g.W)?.Id);   // "they will preside"
        // "speak from the chair": for a motion of their bench moved by another house of it
        var bench = FactionSystem.Of(g.W, house.Id)!; var fellow = g.W.House(bench.Members.First(id => id != house.Id && id != g.Mine.Id && Economy.HeadOf(g.W, g.W.House(id)!) != null))!;
        var theirs = Moves(g, fellow); Session(g);
        Assert.Equal(1, theirs.Chair); Assert.True(theirs.Resolved);
        // "break a tied Senate": a motion of their own house, the rest of the chamber away but houses of exactly its weight against
        var tied = Moves(g, house); int weight = g.Senate.Weight(g.W, house.Id);
        foreach (var h in g.W.Houses.Values.Where(h => h.Id != house.Id)) tied.Votes[h.Id] = 0;
        int need = weight; foreach (var h in Others(g).Where(h => h.Id != house.Id).OrderByDescending(h => g.Senate.Weight(g.W, h.Id)).ThenBy(h => h.Id, StringComparer.Ordinal)) { int hw = g.Senate.Weight(g.W, h.Id); if (hw <= need && hw > 0) { tied.Votes[h.Id] = -1; need -= hw; } }
        Assert.Equal(0, need);
        Session(g); Assert.Equal(tied.ForWeight, tied.AgainstWeight); Assert.True(tied.Passed); Assert.Equal(1, g.W.Counter(LadderSystem.CastingCounter));
        // "once in their term strike a motion with a veto": the first motion against their house is struck, the second is not
        var first = IMove(g, "censure", house.Id); Only(g, first); Session(g); Assert.True(first.Vetoed); Assert.Equal(house.Id, first.VetoedBy);
        var second = IMove(g, "embargo", house.Id); Only(g, second); Session(g); Assert.False(second.Vetoed); Assert.True(second.Resolved);
    }

    [Fact]
    public void TheConsulsTable_TheConsulWhoDinedYou_LetsYourMotionStand_EvenWhenAnotherHeadsTheirHouse_AcrossASaveAndALoad()
    {
        var (g0, consul0, f) = Feast(); Answer(g0, f, "go_and_be_seen"); Assert.Contains(ElectionSystem.DinedChar, consul0.Flags);
        var house0 = g0.W.House(consul0.HouseId)!;
        var heir = g0.W.LivingCharacters().First(c => c.HouseId == house0.Id && c.Id != consul0.Id && c.Age >= DynastySystem.AgeOfMajority);
        house0.HeadId = heir.Id; Assert.Equal(heir.Id, Economy.HeadOf(g0.W, house0)!.Id);   // a new head, who sat at no table: the consul is not the head of their house
        var g = Reload(g0); var consul = g.W.Char(consul0.Id)!; var house = g.W.House(house0.Id)!;
        var censure = IMove(g, "censure", house.Id); Only(g, censure); Session(g);
        Assert.False(censure.Vetoed); Assert.True(censure.Resolved);   // let stand, and voted
        Assert.DoesNotContain(ElectionSystem.DinedChar, consul.Flags);   // once in the term
        Assert.True(g.Told($"{consul.Name}, as Senior Consul, lets your motion stand that they would have struck with the veto"));
        var embargo = IMove(g, "embargo", house.Id); Only(g, embargo); Session(g); Assert.True(embargo.Vetoed);   // the next one is struck
    }

    [Fact]
    public void TheConsulsTable_AGiftAhead_DoesTheSame_AndWithoutTheTableTheMotionIsStruck()
    {
        var (g, consul, f) = Feast(); int credits = g.Mine.Treasury.Credits; int regard = g.W.GetRelationship(consul.Id, g.Me.Id)?.Disposition ?? 0;
        Choose(g, f, "send_a_gift_ahead"); Assert.Equal(credits - 6, g.Mine.Treasury.Credits); Assert.Equal(regard + 10, g.W.GetRelationship(consul.Id, g.Me.Id)!.Disposition);
        var house = g.W.House(consul.HouseId)!; house.HeadId = g.W.LivingCharacters().First(c => c.HouseId == house.Id && c.Id != consul.Id && c.Age >= DynastySystem.AgeOfMajority).Id;   // and another heads their house
        var spared = IMove(g, "censure", consul.HouseId); Only(g, spared); Session(g); Assert.False(spared.Vetoed);
        Assert.True(g.Told($"{consul.Name}, as Senior Consul, lets your motion stand that they would have struck with the veto"));
        var (h, other, f2) = Feast(); Answer(h, f2, "stay_away");
        var struck = IMove(h, "censure", other.HouseId); Only(h, struck); Session(h); Assert.True(struck.Vetoed);
    }

    [Fact]
    public void TheConsulsTable_LapsesWithANotice_WhenTheConsulDies_OrTheTermEnds()
    {
        var (g, consul, f) = Feast(); Answer(g, f, "go_and_be_seen");
        DynastySystem.Die(g.W, consul.Id); var censure = IMove(g, "censure", consul.HouseId); Only(g, censure); Session(g);
        Assert.True(g.Told($"{consul.Name} no longer holds the chair, and the motion they would have spared you for coming to their table goes with it."));
        Assert.DoesNotContain(ElectionSystem.DinedChar, consul.Flags);
        var (h, c2, f2) = Feast(); Answer(h, f2, "go_and_be_seen");
        h.W.Turn = 2 * ElectionSystem.Every - 1; Session(h);
        Assert.True(h.Told($"{c2.Name}'s term as consul is over, and the motion they would have spared you for coming to their table ends with it."));
        Assert.DoesNotContain(ElectionSystem.DinedChar, c2.Flags);
    }

    // ------------------------------------------------------------------ your colleague in the consulship: your_colleague_the_consul

    /// <summary>You stand for consul and are elected Senior Consul at the end of year ten; your colleague, the Junior, asks to see you.</summary>
    private static (G g, Character colleague, FiredEvent report) Colleagues(bool colleaguePresides = false)
    {
        var g = Start(); g.W.Turn = ElectionSystem.Every - 1; Assert.True(g.Do(new StandForConsulCommand(g.Me.Id, g.Senate.Offices)));
        Session(g); Assert.Equal(g.Me.Id, Holder(g, "consul_senior")?.Id); var colleague = Holder(g, "consul_junior")!;
        if (colleaguePresides)   // the two chairs the other way about: your colleague presides
        {
            foreach (var o in g.W.Offices.Where(o => o.OfficeId is "consul_senior" or "consul_junior")) o.OfficeId = o.OfficeId == "consul_senior" ? "consul_junior" : "consul_senior";
            g.Me.Flags.Remove("office:consul_senior"); g.Me.Flags.Add("office:consul_junior"); colleague.Flags.Remove("office:consul_junior"); colleague.Flags.Add("office:consul_senior");
            Assert.Equal(colleague.Id, LadderSystem.Presiding(g.W)?.Id);
        }
        var (f, cast) = Report(g, "your_colleague_the_consul", "grandee"); Assert.Equal(colleague.Id, cast.Id);
        return (g, colleague, f);
    }

    [Fact]
    public void YourColleague_StrikesWhatStrikesAtTheirHouse_AndTheBusinessDivided_LeavesYoursAlone_AcrossASaveAndALoad()
    {
        var (h, other, f2) = Colleagues(); Answer(h, f2, "keep_your_counsel");
        var struck = IMove(h, "censure", other.HouseId); Only(h, struck); Session(h); Assert.True(struck.Vetoed); Assert.Equal(other.HouseId, struck.VetoedBy);   // what the report says they will strike
        var (g0, colleague0, f) = Colleagues(); Answer(g0, f, "divide_the_year"); Assert.Contains(ElectionSystem.DividedChar, colleague0.Flags);
        var g = Reload(g0); var colleague = g.W.Char(colleague0.Id)!;
        var censure = IMove(g, "censure", colleague.HouseId); Only(g, censure); Session(g);
        Assert.False(censure.Vetoed); Assert.True(censure.Resolved); Assert.Contains(ElectionSystem.DividedChar, colleague.Flags);   // left alone, and nothing spent
        var embargo = IMove(g, "embargo", colleague.HouseId); Only(g, embargo); Session(g); Assert.False(embargo.Vetoed);   // for the term, not once
    }

    [Fact]
    public void YourColleague_Presiding_DoesNotSpeakAgainstYourMotion_OnceTheBusinessIsDivided()
    {
        var (h, other, f2) = Colleagues(colleaguePresides: true); Answer(h, f2, "keep_your_counsel");
        var against = IMove(h, "censure", other.HouseId); Only(h, against); h.W.WorldCounters["veto_used:" + other.HouseId] = ElectionSystem.TermStart(h.W, h.Senate.Offices, other.HouseId);   // its veto spent: what is left is the chair
        Session(h); Assert.Equal(-1, against.Chair);
        var (g, colleague, f) = Colleagues(colleaguePresides: true); Answer(g, f, "divide_the_year");
        var censure = IMove(g, "censure", colleague.HouseId); Only(g, censure); g.W.WorldCounters["veto_used:" + colleague.HouseId] = ElectionSystem.TermStart(g.W, g.Senate.Offices, colleague.HouseId);
        Session(g); Assert.Equal(0, censure.Chair); Assert.True(censure.Resolved);
    }

    [Fact]
    public void YourColleague_YouAreRemindedWhenTheyMove_AndStrikingTheirMotionBreaksTheDivision_MinusFifteen_AndFreesTheirVeto()
    {
        var (g, colleague, f) = Colleagues(); Answer(g, f, "divide_the_year"); var house = g.W.House(colleague.HouseId)!;
        var theirs = Moves(g, house);
        Assert.True(g.Told($"{colleague.Name}, your colleague in the consulship, has moved a motion. You divided the consulship's business between you: strike it with the veto, or speak against it from the chair, and the division is broken, they think -15 of you"));
        int regard = g.W.GetRelationship(colleague.Id, g.Me.Id)?.Disposition ?? 0;
        var veto = new VetoCommand(g.Me.Id, theirs.Id, g.Senate.Offices, g.Senate); Assert.True(g.Do(veto), veto.Refusal(g.Ctx));
        Assert.Equal(regard - ElectionSystem.DivisionBroken, g.W.GetRelationship(colleague.Id, g.Me.Id)!.Disposition);
        Assert.DoesNotContain(ElectionSystem.DividedChar, colleague.Flags);
        Assert.True(g.Told($"You struck the business of {colleague.Name}, and the division of the consulship you made with them is broken: they think -15 of you, and are free to strike yours."));
        var censure = IMove(g, "censure", house.Id); Only(g, censure); Session(g); Assert.True(censure.Vetoed);   // and they do
        // from the chair, the same
        var (h, c2, f2) = Colleagues(); Answer(h, f2, "divide_the_year"); var m2 = Moves(h, h.W.House(c2.HouseId)!); h.W.Turn++;   // due this year
        Assert.True(h.Do(new ChairCommand(h.Me.Id, m2.Id, LadderSystem.SpeakAgainst, h.Senate))); Assert.DoesNotContain(ElectionSystem.DividedChar, c2.Flags);
    }

    [Fact]
    public void YourColleague_TheDivisionLapsesWithANotice_AtWar()
    {
        var (g, colleague, f) = Colleagues(); Answer(g, f, "divide_the_year");
        g.W.Wars.Add(new War { HouseA = g.Mine.Id, HouseB = colleague.HouseId, Since = g.W.Turn }); Assert.True(g.W.AtWar(g.Mine.Id, colleague.HouseId));
        var censure = IMove(g, "censure", colleague.HouseId); Only(g, censure); Session(g);
        Assert.True(g.Told($"{g.W.House(colleague.HouseId)!.Name} is at war with your house, and the division of the consulship's business you made with them goes with it."));
        Assert.True(censure.Vetoed);
    }

    // ------------------------------------------------------------------ the other consul courted after a veto: the_veto_falls/court_the_other_consul

    /// <summary>The consuls elected; your censure of the Senior Consul's house is struck by their veto, and the report comes.</summary>
    private static (G g, Character other, FiredEvent report) Struck()
    {
        var (g, senior, f) = Feast(); Answer(g, f, "stay_away");
        var censure = IMove(g, "censure", senior.HouseId); Only(g, censure); Session(g); Assert.True(censure.Vetoed); Assert.True(g.W.HasFlag("vetoed_mine"));
        var other = Holder(g, "consul_junior")!; Assert.Contains(ElectionSystem.OtherConsulChar, other.Flags);
        return (g, other, ReportOf(g, "the_veto_falls"));
    }

    [Fact]
    public void TheOtherConsulCourted_StrikesTheNextMotionAgainstYourHouse_HoweverHardItLeansFor_AcrossASaveAndALoad()
    {
        var (g0, other0, f) = Struck(); int influence = Influence(g0);
        Choose(g0, f, "court_the_other_consul"); Assert.Equal(influence - 1, Influence(g0));
        var g = Reload(g0); var other = g.W.Char(other0.Id)!;
        var mover = Others(g).First(h => h.Id != other.HouseId && h.Id != Holder(g, "consul_senior")!.HouseId);
        var censure = Moves(g, mover, "censure", g.Mine.Id); foreach (var h in Others(g).Where(h => h.Id != mover.Id)) Leans(g, censure, h, 6);   // the chamber is for it
        Session(g);
        Assert.True(censure.Vetoed); Assert.Equal(other.HouseId, censure.VetoedBy);
        Assert.True(g.Told($"{other.Name}, as Junior Consul, strikes the motion against your house with their veto, as a friend in the chair"));
        Assert.DoesNotContain(ElectionSystem.FriendChar, other.Flags);   // once
        Assert.False(ElectionSystem.MayVeto(g.W, g.Senate.Offices, other.HouseId));   // it was their veto for the term
    }

    [Fact]
    public void TheOtherConsulCourted_NotTheirOwnHousesMotion_WhichWaits_AndTheChoiceIsClosedWhenThereIsNoOtherConsulToCourt()
    {
        var (g, other, f) = Struck(); Choose(g, f, "court_the_other_consul");
        var own = Moves(g, g.W.House(other.HouseId)!, "censure", g.Mine.Id); Session(g); Assert.False(own.Vetoed);   // they do not strike their own house's motion
        Assert.Contains(ElectionSystem.FriendChar, other.Flags);   // the promise waits for the next
        var third = Moves(g, Others(g).First(h => h.Id != other.HouseId && h.Id != Holder(g, "consul_senior")!.HouseId), "embargo", g.Mine.Id); Session(g); Assert.True(third.Vetoed);
        // with the other consul's veto spent, there is nobody to court
        var (h, other2, _) = Feast(); h.W.WorldCounters["veto_used:" + other2.HouseId] = 0;
        var junior = Holder(h, "consul_junior")!; h.W.WorldCounters["veto_used:" + junior.HouseId] = ElectionSystem.TermStart(h.W, h.Senate.Offices, junior.HouseId);
        var c = IMove(h, "censure", other2.HouseId); Only(h, c); Session(h); Assert.True(c.Vetoed);
        Assert.False(h.W.HasFlag(ElectionSystem.CourtFlag)); var report = ReportOf(h, "the_veto_falls");
        Assert.False(new EventEngine(Content, WorldBuilder.RngFor(h.W)).IsChoiceAvailable(report, h.W, "court_the_other_consul"));
    }

    // ------------------------------------------------------------------ the emergency powers: emergency_powers_voted, the_powers_are_yours, EmergencySystem.GrantVoted

    private static void InDanger(G g) { foreach (var n in g.W.Nations) n.AtWar = false; g.W.Nations[0].AtWar = true; Assert.NotNull(EmergencySystem.Danger(g.W)); }
    /// <summary>The year as the turn runs it for the Senate and the powers: the session, then the powers' year.</summary>
    private static void Year(G g) { Session(g); EmergencySystem.Tick(g.W, g.Senate.Offices); }
    /// <summary>The chamber leans for a motion: leaders first, so a bench that follows its leader is already where it is set.</summary>
    private static void AllFor(G g, Motion m) { foreach (var h in Others(g).OrderBy(h => FactionSystem.Of(g.W, h.Id)?.LeaderHouseId == h.Id ? 0 : 1).ThenBy(h => h.Id, StringComparer.Ordinal)) if (h.Id != m.ProposerHouseId) Leans(g, m, h, 2); }

    [Fact]
    public void ThePowersVotedAnotherConsul_NoElectionIsHeldWhileTheyRun_ThenBothAreHeld_TheSenateLeansTheirWay_AndTheHousesAreLevied()
    {
        var (g, _, f) = Feast(); Answer(g, f, "stay_away"); InDanger(g); var holderHouse = g.W.House(Holder(g, "consul_senior")!.HouseId)!;
        var magistrates = g.W.Magistrates.Select(x => x.CharacterId).OrderBy(x => x).ToList(); int counted = g.W.Counts[0].Turn;
        g.W.Turn = 2 * ElectionSystem.Every - 3;   // year 17: the powers will run over year 20, when the consuls and the magistrates are both due
        var grant = Moves(g, holderHouse, "emergency", holderHouse.Id); AllFor(g, grant); Year(g);
        Assert.NotNull(g.W.Emergency); Assert.Equal(holderHouse.Id, g.W.Emergency!.HouseId);
        Assert.Contains(g.W.History, x => x.Text.Contains("The other consul's veto is set aside, and no election will be held while they run."));
        var (_, holder) = Report(g, "emergency_powers_voted", "holder"); Assert.Equal(g.W.Emergency.HolderId, holder.Id);
        // the Senate leans toward what they move: every other house, by two, and the whip count says why
        var theirs = Moves(g, holderHouse); var voter = Others(g).First(h => h.Id != holderHouse.Id);
        Assert.Contains(g.Senate.Why(g.W, theirs, voter.Id), x => x.why.EndsWith(" holds emergency powers") && x.by == EmergencySystem.HolderLeanBy);
        // the other consul's veto is set aside: a censure of their house is voted, not struck
        var junior = g.W.House(Holder(g, "consul_junior")!.HouseId)!; var censure = Moves(g, Others(g).First(h => h.Id != junior.Id && h.Id != holderHouse.Id), "censure", junior.Id);
        Session(g); Assert.True(censure.Resolved); Assert.False(censure.Vetoed);
        int treasury = g.Mine.Treasury.Credits; EmergencySystem.Tick(g.W, g.Senate.Offices);   // year 19: a levy without a vote
        Assert.Equal(treasury - EmergencySystem.LevyCredits, g.Mine.Treasury.Credits); Assert.True(g.W.HasFlag(EmergencySystem.LeviedFlag));
        Year(g);   // year 20: no election of the consuls, none of the magistrates
        Assert.False(g.W.HasFlag("election_held")); Assert.True(g.W.Emergency!.SkippedElection); Assert.True(g.W.Emergency.SkippedLadder);
        Assert.All(g.W.Counts, x => Assert.Equal(counted, x.Turn)); Assert.Equal(magistrates, g.W.Magistrates.Select(x => x.CharacterId).OrderBy(x => x).ToList());
        Year(g);   // year 21: the term is up, and another house's consul lays them down
        Assert.Null(g.W.Emergency); Assert.Equal(1, g.W.Counter(LadderSystem.OwedKey));
        Year(g);   // year 22: both elections that fell due are held
        Assert.True(g.W.HasFlag("election_held")); Assert.All(g.W.Counts, x => Assert.Equal(g.W.Turn, x.Turn)); Assert.Equal(0, g.W.Counter(LadderSystem.OwedKey));
    }

    [Fact]
    public void ThePowersAreYours_AVetoEveryYear_YourColleaguesSetAside_ALevyWithoutAVote_TheSenateLeansYourWay_AndNoElection_AcrossASaveAndALoad()
    {
        var (g0, colleague0, f) = Colleagues(); Answer(g0, f, "keep_your_counsel"); InDanger(g0);
        g0.W.Turn = 3 * g0.Senate.Offices.LadderEvery - 2;   // year 13: the magistrates are due at the end of year 15
        var grant = IMove(g0, "emergency", g0.Mine.Id); Only(g0, grant); AllFor(g0, grant); Year(g0);
        Assert.True(EmergencySystem.Holds(g0.W, g0.Mine.Id)); ReportOf(g0, "the_powers_are_yours");
        var g = Reload(g0); var colleague = g.W.Char(colleague0.Id)!; int counted = g.W.Counts[0].Turn;
        // a veto every year: one this year, refused a second, and one again the next
        var movers = Others(g).Where(h => h.Id != colleague.HouseId).ToList();
        var a = Moves(g, movers[0]); var b = g.Senate.Add(g.W, movers[1].Id, "levy", "");
        Assert.True(g.Do(new VetoCommand(g.Me.Id, a.Id, g.Senate.Offices, g.Senate))); Assert.Equal("you have struck one motion this year", new VetoCommand(g.Me.Id, b.Id, g.Senate.Offices, g.Senate).Refusal(g.Ctx));
        // a levy on the houses without a vote
        var payer = Others(g).First(h => h.Treasury.Credits >= EmergencySystem.LevyCredits); int paid = payer.Treasury.Credits, before = g.Mine.Treasury.Credits;
        Assert.True(g.Do(new EmergencyCommand(g.Me.Id, EmergencyCommand.Levy))); Assert.Equal(paid - EmergencySystem.LevyCredits, payer.Treasury.Credits); Assert.True(g.Mine.Treasury.Credits > before);
        // the Senate leans your way on what you move
        var mine = IMove(g); Assert.Contains(g.Senate.Why(g.W, mine, payer.Id), x => x.why.EndsWith("holds emergency powers") && x.by == EmergencySystem.HolderLeanBy);
        // your colleague's veto is set aside: a censure of their house is voted, not struck
        var censure = g.Senate.Add(g.W, movers[2].Id, "censure", colleague.HouseId);
        Year(g);   // year 15: no election of the magistrates
        Assert.False(censure.Vetoed); Assert.True(censure.Resolved); Assert.True(g.W.Emergency!.SkippedLadder); Assert.All(g.W.Counts, x => Assert.Equal(counted, x.Turn));
        var c = Moves(g, movers[0]); Assert.True(g.Do(new VetoCommand(g.Me.Id, c.Id, g.Senate.Offices, g.Senate)));   // and a veto again this year
        // laid down: the election that fell due under them is held at the end of the year
        Assert.True(g.Do(new EmergencyCommand(g.Me.Id, EmergencyCommand.LayDown))); Assert.Equal(1, g.W.Counter(LadderSystem.OwedKey));
        Year(g); Assert.All(g.W.Counts, x => Assert.Equal(g.W.Turn, x.Turn)); Assert.True(g.W.HasFlag(LadderSystem.HeldFlag));
    }

    // ------------------------------------------------------------------ the canvass and the good grace: the_canvass, the_count_goes_against_you

    /// <summary>You put your own name down for quaestor in the year before the count (the Old Lion has climbed nothing, here), and the
    /// report of the canvass comes.</summary>
    private static (G g, FiredEvent report) Canvass()
    {
        var g = Start(); g.Me.Cursus.Clear();
        Assert.True(g.Do(new LadderCommand(g.Me.Id, LadderCommand.Stand, g.Me.Id, LadderSystem.Quaestor, g.Senate.Offices)));
        LadderSystem.Tick(g.W, g.Senate.Offices, Content.Policies); Assert.True(g.W.HasFlag(LadderSystem.StandingFlag));
        return (g, ReportOf(g, "the_canvass"));
    }

    [Fact]
    public void TheCanvass_EveryDoorIsTwoVotesAtTheCount_ALittleSpentIsThree_AndBothAreFive_AcrossASaveAndALoad()
    {
        var (g, f) = Canvass(); Answer(g, f, "let_the_record_speak");
        var (d, f2) = Canvass(); Answer(d, f2, "shake_every_hand");
        var (c0, f3) = Canvass(); int credits = c0.Mine.Treasury.Credits; Choose(c0, f3, "spend_a_little"); Assert.Equal(credits - 6, c0.Mine.Treasury.Credits);
        var c = Reload(c0);
        var (b, f4) = Canvass(); Answer(b, f4, "shake_every_hand"); Answer(b, ReportOf(b, "the_canvass"), "spend_a_little");   // the report came twice
        Session(g); Session(d); Session(c); Session(b);
        int plain = AtTheCount(g, g.Me);
        Assert.Equal(plain + LadderSystem.DoorsVotes, AtTheCount(d, d.Me)); Assert.Equal(plain + LadderSystem.CoinVotes, AtTheCount(c, c.Me)); Assert.Equal(plain + LadderSystem.DoorsVotes + LadderSystem.CoinVotes, AtTheCount(b, b.Me));
        Assert.DoesNotContain(LadderSystem.DoorsChar, b.Me.Flags); Assert.DoesNotContain(LadderSystem.CoinChar, b.Me.Flags);   // counted at the one count
    }

    [Fact]
    public void TheCountGoesAgainstYou_TheGoodGraceIsTwoVotesAtTheNextCount_AndThenGone_AndGoingThroughTheTallyPromisesNothing()
    {
        G Lost(string choice)
        {
            var g = Start(); g.Me.Cursus.Remove(LadderSystem.Praetor);   // you have not been praetor here, and stand for it
            Assert.True(g.Do(new LadderCommand(g.Me.Id, LadderCommand.Stand, g.Me.Id, LadderSystem.Praetor, g.Senate.Offices)));
            foreach (var h in Others(g)) if (LadderSystem.Candidates(g.W, g.Senate.Offices, g.Senate.Offices.Rung(LadderSystem.Praetor)!).FirstOrDefault(x => x.HouseId == h.Id) is { } rival)
                g.W.Campaigns.Add(new CampaignFund { HouseId = h.Id, CharacterId = rival.Id, RungId = LadderSystem.Praetor, Turn = g.W.Turn, Size = LadderSystem.Lavish, Spent = 40, Votes = 500 });   // outspent at the count
            Session(g); Assert.True(g.W.HasFlag(LadderSystem.LostFlag));
            Answer(g, ReportOf(g, "the_count_goes_against_you"), choice);
            g.W.Turn = 2 * g.Senate.Offices.LadderEvery - 1;   // the year before the next count, and you stand again
            Assert.True(g.Do(new LadderCommand(g.Me.Id, LadderCommand.Stand, g.Me.Id, LadderSystem.Praetor, g.Senate.Offices)));
            Session(g); return g;
        }
        var grace = Lost("congratulate_the_winner"); var tally = Lost("learn_who_did_not_vote");
        Assert.Equal(AtTheCount(tally, tally.Me) + LadderSystem.GraceVotes, AtTheCount(grace, grace.Me));
        Assert.DoesNotContain(LadderSystem.GraceChar, grace.Me.Flags);   // remembered at the next count, not every count
        Assert.Contains(tally.W.History, x => x.Text.Contains("went through the tally house by house, to see where each house's votes had gone and why"));
        var words = Content.Events.First(e => e.Id == "the_count_goes_against_you");
        Assert.DoesNotContain("promise", string.Join(" ", words.Choices.SelectMany(ch => ch.Effects.OfType<LogEffect>().Select(l => l.Text))), StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------ a motion held over: my_motion_held_over

    [Fact]
    public void AMotionHeldOver_ComesToNextYearsSession_AndCannotBeHeldOverASecondTime()
    {
        var g = Start(); var speakerHouse = Others(g).First(h => h.Id != g.Mine.Id); var speaker = g.HeadOf(speakerHouse);
        OfficeSystem.Grant(g.W, Seat("speaker"), speakerHouse.Id, speaker);
        var censure = IMove(g, "censure", speakerHouse.Id); Only(g, censure); int proposed = censure.ProposedTurn;
        Session(g); Assert.True(censure.HeldOver); Assert.True(censure.Open); Assert.Equal(proposed + 1, censure.ProposedTurn);
        ReportOf(g, "my_motion_held_over");
        Assert.Equal("it has been held over once already, and must be reached", LadderSystem.ChairRefusal(g.W, speaker, censure, LadderSystem.HoldMotion));
        Session(g); Assert.True(censure.Resolved);   // dealt with at next year's session
        Assert.Contains("held it over to next year's session, and cannot hold it over a second time", Content.Text("my_motion_held_over")!.Body);
    }

    // ------------------------------------------------------------------ the record of attendance, the committee: LadderSystem.AttendanceSays, LadderSystem.CommitteeSays

    [Fact]
    public void TheAttendanceRecord_IsWorthItsVotesAtEveryElection_AsTheWordsSay_NotAtAMotionsCount()
    {
        var (g, f) = Canvass(); Answer(g, f, "let_the_record_speak"); var (h, f2) = Canvass(); Answer(h, f2, "let_the_record_speak");
        h.W.WorldCounters[LadderSystem.SatKey(h.Me.Id)] = 5;   // five years of five in the chamber
        Assert.EndsWith("It is worth 2 votes to you at every election: for an office you stand for, and for your house when it stands for consul.", LadderSystem.AttendanceSays(h.W, h.Me));
        var gm = IMove(g); Only(g, gm); var hm = IMove(h); Only(h, hm);
        Session(g); Session(h);
        Assert.Equal(AtTheCount(g, g.Me) + 2, AtTheCount(h, h.Me));                 // at the ladder's count
        Assert.Equal((gm.ForWeight, gm.AgainstWeight), (hm.ForWeight, hm.AgainstWeight));   // not at a motion's
        var e = ElectionYear(); Assert.True(e.Do(new StandForConsulCommand(e.Me.Id, e.Senate.Offices)));
        int without = TallyNow(e)[e.Mine.Id]; e.W.WorldCounters[LadderSystem.SatKey(e.Me.Id)] = 5;
        Assert.Equal(without + 2, TallyNow(e)[e.Mine.Id]);   // and for your house's nominee for consul
    }

    [Fact]
    public void TheCommitteesChair_ItsHousesLeanYourWayByTwo_OnItsBusiness_AndTheCountFollows()
    {
        G Chaired(int standing)
        {
            var g = Start(); Assert.True(g.Do(new CommitteeCommand(g.Me.Id, LadderSystem.Purse))); g.W.WorldCounters[LadderSystem.StandingKey(g.Mine.Id)] = standing; return g;
        }
        var g = Chaired(LadderSystem.StandingMax); var h = Chaired(0);
        Assert.StartsWith("You sit on the Committee of the Purse", LadderSystem.CommitteeSays(g.W, g.Mine)); Assert.Contains("You have the chair: its houses lean your way by two on its business", LadderSystem.CommitteeSays(g.W, g.Mine));
        var purse = LadderSystem.CommitteeMembers(g.W, LadderSystem.Purse).Select(x => x.Id).ToHashSet();
        var member = LadderSystem.CommitteeMembers(g.W, LadderSystem.Purse).First(x => x.Id != g.Mine.Id && (FactionSystem.Of(g.W, x.Id) is not { } b || (b.LeaderHouseId != x.Id && !purse.Contains(b.LeaderHouseId))));
        var levy = IMove(g); Only(g, levy); var levy2 = IMove(h); Only(h, levy2);
        Assert.Contains(g.Senate.Why(g.W, levy, member.Id), x => x.why == "your standing on its committee" && x.by == 2);
        Leans(g, levy, member, 1); levy2.Lobby[member.Id] = levy.Lobby[member.Id]; Assert.Equal(-1, h.Senate.Lean(h.W, levy2, member.Id));   // the same house, two points apart
        Session(g); Session(h);
        Assert.Equal(1, levy.Votes[member.Id]); Assert.Equal(-1, levy2.Votes[member.Id]);
    }

    // ================================================================== group "bench"

    /// <summary>The player's house sits on a bench that another house leads: the first house of the Reach by name leads it, and the third
    /// sits with them. The second is left to move the motions.</summary>
    private static (G g, House leader, House mover, House other, Faction bench) Seated(ulong seed = 7)
    {
        var g = Start(seed); var o = Others(g); var leader = o[0]; var mover = o[1]; var other = o[2];
        foreach (var f in g.W.Factions) { f.Members.Remove(leader.Id); f.Members.Remove(other.Id); f.Members.Remove(g.Mine.Id); }
        g.W.Factions.RemoveAll(f => f.LeaderHouseId == leader.Id || f.LeaderHouseId == other.Id || f.LeaderHouseId == g.Mine.Id);
        var bench = new Faction { Id = "test_bench", Name = "the Test Bench", LeaderHouseId = leader.Id, Members = { leader.Id, other.Id, g.Mine.Id } };
        g.W.Factions.Add(bench); g.W.Relationship(g.HeadOf(leader).Id, g.Me.Id).Disposition = 0;
        return (g, leader, mover, other, bench);
    }
    /// <summary>The player founds a bench, as the window does: two houses that have voted with theirs often enough, and no bench of their own.</summary>
    private static (G g, Faction bench, List<House> members) Leading(ulong seed = 7)
    {
        var g = Start(seed); var members = Others(g).Skip(1).Take(2).ToList();
        foreach (var f in g.W.Factions) { f.Members.Remove(g.Mine.Id); foreach (var h in members) f.Members.Remove(h.Id); }
        foreach (var h in members) g.W.WorldCounters[FactionSystem.AffinityKey(h.Id, g.Mine.Id)] = FactionSystem.FormThreshold;
        var found = new FoundFactionCommand(g.Me.Id); Assert.True(g.Do(found), found.Refusal(g.Ctx));
        var bench = FactionSystem.Of(g.W, g.Mine.Id)!; Assert.Equal(g.Mine.Id, bench.LeaderHouseId); Assert.True(members.All(h => bench.Members.Contains(h.Id)));
        return (g, bench, members);
    }
    private static int By(G g, Motion m, House h, string because) => g.Senate.Why(g.W, m, h.Id).Where(x => x.why.Contains(because)).Sum(x => x.by);
    private static int Affinity(G g, House h) => FactionSystem.Affinity(g.W, g.Mine.Id, h.Id);

    // ------------------------------------------------------------------ the empty seat: the_empty_seat_on_the_bench/say_it_will_not_happen_again

    /// <summary>The house sits out a vote its bench's leader votes in, and the leader's report comes; the player says it will not happen again.</summary>
    private static (G g, House leader, House mover, Faction bench) Promised()
    {
        var (g, leader, mover, _, bench) = Seated();
        var m = TheyMove(g, mover); Leans(g, m, leader, 3); IVote(g, m, 0); Session(g);
        Assert.Equal(0, m.Votes[g.Mine.Id]); Assert.Contains(VoteMarks.NoticedChar, g.HeadOf(leader).Flags);
        var (f, cast) = Report(g, "the_empty_seat_on_the_bench", "leader"); Assert.Equal(g.HeadOf(leader).Id, cast.Id);
        int regard = g.Regard(leader); g.W.News.Clear(); Answer(g, f, "say_it_will_not_happen_again");
        Assert.Equal(regard + 3, g.Regard(leader)); Assert.True(g.W.Counter(VoteMarks.StandKey(leader.Id)) > 0);
        Assert.True(g.Told($"You promised {leader.Name} that your house will vote at the next vote of the Test Bench that {leader.Name} votes in"));
        Assert.True(g.Told($"thinks {FactionSystem.BreakWhipDisposition} the worse of you and your tie to the bench is weaker by {FactionSystem.BreakWhipAffinity}"));   // what breaking it costs, said as it is given
        return (g, leader, mover, bench);
    }

    [Fact]
    public void TheEmptySeat_YourWordBroken_CostsWhatAVoteAgainstTheBenchCosts_YouAreReminded_AndTheLeaderDoesNotAskAgain()
    {
        var (g, leader, mover, bench) = Promised();
        g.W.News.Clear(); var m = g.Senate.Add(g.W, mover.Id, "levy", ""); Only(g, m);
        Assert.True(g.Told($"{mover.Name} has moved a motion. You promised {leader.Name} that your house would vote at the next vote of the bench"));   // the reminder, when it is moved
        Leans(g, m, leader, 3); g.W.News.Clear(); IVote(g, m, 0); Assert.True(g.Told("stay in your seat on this one"));   // and as you sit it out
        int regard = g.Regard(leader), affinity = Affinity(g, leader); g.HeadOf(leader).Flags.Remove(VoteMarks.NoticedChar); Session(g);
        Assert.Equal(0, m.Votes[g.Mine.Id]); Assert.Equal(1, m.Votes[leader.Id]);
        Assert.Equal(regard - FactionSystem.BreakWhipDisposition, g.Regard(leader)); Assert.Equal(affinity - FactionSystem.BreakWhipAffinity, Affinity(g, leader));   // -5 and -2
        Assert.True(g.Told($"You promised {leader.Name} that your house would vote at the next vote of the Test Bench, and it stayed in its seat"));
        Assert.Contains(g.W.History, h => h.Text.Contains("stayed in its seat at the vote, though it had promised"));
        Assert.Equal(0, g.W.Counter(VoteMarks.StandKey(leader.Id))); Assert.DoesNotContain(VoteMarks.NoticedChar, g.HeadOf(leader).Flags);   // judged, and the leader does not come to ask again
    }

    [Fact]
    public void TheEmptySeat_YourWordKept_ToANewHeadOfTheLeadersHouse_AcrossASaveAndALoad_AndAMotionAgainstYourHouseIsNotTheVote()
    {
        var (g0, leader0, mover0, _) = Promised();
        // the next vote is a censure of your house: not the bench's vote you promised, and the word waits
        var censure = g0.Senate.Add(g0.W, mover0.Id, "censure", g0.Mine.Id); Only(g0, censure); Leans(g0, censure, leader0, 3); IVote(g0, censure, 0); Session(g0);
        Assert.True(g0.W.Counter(VoteMarks.StandKey(leader0.Id)) > 0); Assert.False(g0.Told("it stayed in its seat"));
        var old = g0.HeadOf(leader0); DynastySystem.Die(g0.W, old.Id); var heir = g0.HeadOf(leader0); Assert.NotEqual(old.Id, heir.Id);
        var g = Reload(g0); var leader = g.W.House(leader0.Id)!; var mover = g.W.House(mover0.Id)!;   // a save and a load between the word and the vote
        var m = g.Senate.Add(g.W, mover.Id, "levy", ""); Only(g, m); Leans(g, m, leader, 3); IVote(g, m, 1);
        int regard = g.Regard(leader); g.W.News.Clear(); Session(g);
        Assert.Equal(1, m.Votes[g.Mine.Id]); Assert.True(g.Told($"You kept your word to {leader.Name}: your house was on its feet at the vote."));
        Assert.Equal(regard, g.Regard(leader)); Assert.Equal(0, g.W.Counter(VoteMarks.StandKey(leader.Id)));
    }

    [Fact]
    public void TheEmptySeat_YourWordLapsesWithANotice_WhenYouLeaveTheBench()
    {
        var (g, leader, mover, bench) = Promised();
        Assert.True(g.Do(new LeaveFactionCommand(g.Me.Id))); var m = g.Senate.Add(g.W, mover.Id, "levy", ""); Only(g, m); Leans(g, m, leader, 3); IVote(g, m, 0);
        g.W.News.Clear(); int regard = g.Regard(leader); Session(g);
        Assert.True(g.Told($"Your word to {leader.Name}, that your house would vote at the next vote of its bench, lapses: your house sits on no bench now."));
        Assert.Equal(0, g.W.Counter(VoteMarks.StandKey(leader.Id))); Assert.Equal(regard, g.Regard(leader)); Assert.False(g.Told("it stayed in its seat"));
    }

    // ------------------------------------------------------------------ the leader's table: the_leaders_table/a_dinner, the_whip

    /// <summary>The player leads a bench; another house moves a motion; the leader's report comes, and is answered.</summary>
    private static (G g, Faction bench, List<House> members, Motion m) AtTheTable(string choice)
    {
        var (g, bench, members) = Leading(); var mover = Others(g).First(h => !bench.Members.Contains(h.Id));
        var m = TheyMove(g, mover); FactionSystem.Tick(g.W, Wt(g)); Assert.True(g.W.HasFlag("faction_leader"));
        var f = ReportOf(g, "the_leaders_table"); Answer(g, f, choice);
        return (g, bench, members, m);
    }

    [Theory]
    [InlineData("a_dinner", "has fed its bench at the house's table")]
    [InlineData("the_whip", "has reminded its bench, one house at a time, who leads it")]
    public void TheLeadersTable_YourBenchFollowsYourVoteByOneMore_AtTheNextCount_AcrossASaveAndALoad_AndThenNoMore(string choice, string because)
    {
        var (g0, _, members0, m0) = AtTheTable(choice); var member0 = members0[0];
        IVote(g0, m0, 1);
        Assert.Equal(FactionSystem.Discipline, By(g0, m0, member0, $"{g0.Mine.Name}, who leads")); Assert.Equal(FactionSystem.FollowedBy, By(g0, m0, member0, because));   // the whip count's reason
        Leans(g0, m0, member0, 1);   // for it, by the one the table gave: without it the house would sit it out
        g0.W.WorldFlags.Remove(choice == "a_dinner" ? FactionSystem.FedFlag : FactionSystem.WhippedFlag); Assert.Equal(0, g0.Senate.Lean(g0.W, m0, member0.Id));
        g0.W.WorldFlags.Add(choice == "a_dinner" ? FactionSystem.FedFlag : FactionSystem.WhippedFlag);
        var g = Reload(g0); var m = g.W.Motions.First(x => x.Id == m0.Id); var member = g.W.House(member0.Id)!;   // a save and a load before the count
        int forecast = g.Senate.Stance(g.W, m, member.Id); Assert.Equal(1, forecast);
        Session(g);
        Assert.Equal(forecast, m.Votes[member.Id]);   // counted as the whip count showed it
        // the count is done, and with it what the table bought
        Assert.False(g.W.HasFlag(FactionSystem.FedFlag)); Assert.False(g.W.HasFlag(FactionSystem.WhippedFlag));
        var next = IMove(g, "relief", g.W.Galaxy!.ReachRegions.OrderBy(r => r.Id, StringComparer.Ordinal).First().Id); var other = Others(g).First(h => h.Id != member.Id && FactionSystem.Of(g.W, h.Id) is { } b && b.LeaderHouseId != h.Id);
        var mover = Others(g).First(h => FactionSystem.Of(g.W, h.Id)?.LeaderHouseId != g.Mine.Id); var third = g.Senate.Add(g.W, mover.Id, "levy", ""); Only(g, third); IVote(g, third, 1);
        Assert.Equal(0, By(g, third, member, because)); Assert.Equal(FactionSystem.Discipline, By(g, third, member, $"{g.Mine.Name}, who leads"));
    }

    // ------------------------------------------------------------------ the bench takes shape: the_bench_forms

    [Fact]
    public void TheBenchForms_YouLeadIt_AndItsHousesLeanTheWayYouVote_SoTheCountFollows()
    {
        var (g, bench, members) = Leading(); Assert.True(g.W.HasFlag("faction_formed"));
        var f = ReportOf(g, "the_bench_forms"); Answer(g, f, "a_dinner_for_the_bench");
        var mover = Others(g).First(h => !bench.Members.Contains(h.Id)); var m = TheyMove(g, mover); var member = members[0];
        IVote(g, m, -1);
        Assert.Equal(-FactionSystem.Discipline, By(g, m, member, $"it follows {g.Mine.Name}, who leads {bench.Name}"));   // the line the whip count shows
        Leans(g, m, member, -1); Assert.Equal(1, g.Senate.Lean(g.W, m, member.Id) + 2 * FactionSystem.Discipline - 2);   // without your lead it would be for it
        Session(g);
        Assert.Equal(-1, m.Votes[member.Id]); Assert.False(g.W.HasFlag("broke_the_whip"));   // the bench follows you; you are its whip, not its member
    }

    // ------------------------------------------------------------------ the whip's displeasure: FactionSystem.BreakWhip, the_whips_displeasure

    [Fact]
    public void TheWhipsDispleasure_AVoteAgainstYourBench_IsFiveAndTwo_AndEveryHouseOfItLeansOneAgainstYourNextMotionAtItsCount_ThenForgets()
    {
        var (g, leader, mover, other, bench) = Seated();
        var theirs = TheyMove(g, mover); Leans(g, theirs, leader, 3); IVote(g, theirs, -1);
        int regard = g.Regard(leader), affinity = Affinity(g, leader); Session(g);
        Assert.Equal(1, theirs.Votes[leader.Id]); Assert.True(g.W.HasFlag("broke_the_whip"));
        Assert.Equal(regard - FactionSystem.BreakWhipDisposition, g.Regard(leader));
        Assert.Equal(affinity - FactionSystem.BreakWhipAffinity - 1, Affinity(g, leader));   // the whip's two, and the vote apart itself
        Assert.True(g.Told($"You voted against the Test Bench, your bench: {g.HeadOf(leader).Name} thinks 5 the worse of you, and your tie to the bench is weaker by 2. At the count of your next motion every house of the Test Bench leans 1 against it"));
        // the next year, as the game plays it: the report, then your motion moved, the year's own session, and the count a year on
        g.W.Turn++; var (f, _) = (ReportOf(g, "the_whips_displeasure"), 0); Answer(g, f, "say_nothing");
        var mine = IMove(g); Only(g, mine);
        Assert.Equal(-FactionSystem.WhipRemembered, By(g, mine, leader, "it remembers that you voted against the Test Bench"));
        Assert.Equal(-FactionSystem.WhipRemembered, By(g, mine, other, "it remembers that you voted against the Test Bench"));
        Assert.Equal(0, By(g, mine, mover, "it remembers"));   // not a house of the bench
        g.Senate.Tick(g.W); Assert.False(mine.Resolved);   // the year's own session: nothing of yours to count, and the bench still remembers
        Assert.Equal(-FactionSystem.WhipRemembered, By(g, mine, other, "it remembers that you voted against the Test Bench"));
        g = Reload(g); mine = g.W.Motions.First(x => x.Id == mine.Id); other = g.W.House(other.Id)!; leader = g.W.House(leader.Id)!;   // a save and a load before the count
        Leans(g, mine, leader, 1); Leans(g, mine, other, 0); int forecast = g.Senate.Stance(g.W, mine, other.Id);   // the leader is for your motion still, so voting for it breaks no whip
        g.W.WorldCounters.Remove(FactionSystem.WhipKey(bench.Id)); Assert.True(g.Senate.Lean(g.W, mine, other.Id) > 0); g.W.WorldCounters[FactionSystem.WhipKey(bench.Id)] = FactionSystem.WhipActive;   // its own one, and what its leader remembers too
        Session(g);
        Assert.Equal(forecast, mine.Votes[other.Id]); Assert.Equal(0, mine.Votes[other.Id]);   // it sat on its hands, where it would have voted for you
        Assert.Equal(0, g.W.Counter(FactionSystem.WhipKey(bench.Id)));   // and it is forgotten after that count
        g.W.WorldCounters.Remove($"motion_cd:{g.Mine.Id}:levy:"); var later = IMove(g); Only(g, later); Assert.Equal(0, By(g, later, other, "it remembers"));
    }

    // ------------------------------------------------------------------ patrons and clients: a_client_at_the_door, under_protection, SenateSystem.YourWay

    /// <summary>The player's house takes a needy house as its client, as the window does.</summary>
    private static House TakeClient(G g, IEnumerable<string>? not = null)
    {
        g.Mine.Wealth = Math.Max(1000, g.Mine.Wealth); var skip = not?.ToHashSet() ?? new HashSet<string>();
        var c = Others(g).First(h => !skip.Contains(h.Id) && h.PatronId.Length == 0 && !OrderSystem.ClientsOf(g.W, h.Id).Any());
        c.Wealth = 10; c.Debt = Math.Max(1, c.Debt); g.W.Relationship(g.HeadOf(c).Id, g.Me.Id).Disposition = 0;
        var take = new ClientCommand(g.Me.Id, ClientCommand.Take, c.Id, g.Tc.Orders); Assert.True(g.Do(take), take.Refusal(g.Ctx));
        Assert.Equal(g.Mine.Id, c.PatronId); return c;
    }
    /// <summary>Whom a house gives its seats to at a count: the one tally with only its weight in it.</summary>
    private static string SeatsOf(Dictionary<string, int> tally) => tally.Single(kv => kv.Value >= 1000).Key;

    [Fact]
    public void AClientAtTheDoor_LeansTheWayYouVote_OnEveryDivision_AndTheCountFollows_ButALeanIsALean()
    {
        var g = Start(); var c = TakeClient(g); Assert.True(g.W.HasFlag(OrderSystem.ClientTakenFlag));
        var f = ReportOf(g, "a_client_at_the_door"); Answer(g, f, "receive_him_well");
        var mover = Others(g).First(h => h.Id != c.Id); var m = TheyMove(g, mover);   // a motion your house neither moves nor is named in
        Assert.Equal(0, By(g, m, c, "it leans the way you vote"));   // no vote of yours yet, so no way to lean
        IVote(g, m, -1); Assert.Equal(-2, By(g, m, c, "it leans the way you vote: it is your client"));   // the whip count's reason
        Leans(g, m, c, -1);   // for it by one of itself, against it by your two
        int forecast = g.Senate.Stance(g.W, m, c.Id); Assert.Equal(-1, forecast);
        // and one that leans against you by more than two votes its own way: the words promise a lean, not a vote
        var second = Others(g).First(h => h.Id != c.Id && h.Id != mover.Id); var n = g.Senate.Add(g.W, second.Id, "levy", ""); Only(g, m, n);
        IVote(g, n, 1); Leans(g, n, c, -1); Assert.Equal(2, By(g, n, c, "it leans the way you vote"));
        Session(g);
        Assert.Equal(forecast, m.Votes[c.Id]); Assert.Equal(-1, n.Votes[c.Id]);
    }

    [Fact]
    public void YourClientsAndPatron_LeanTheWayYouVoteOnlyWhenYouHaveVoted_AndTheForecastAndTheCountAgreeWhenYouHaveNot()
    {
        // you lead a bench, so before the count was mended your house was counted among the first; your client sits on no bench of yours
        var (g, bench, _) = Leading(); var c = TakeClient(g, bench.Members.Concat(g.W.Factions.Select(f => f.LeaderHouseId))); var mover = Others(g).First(h => h.Id != c.Id && !bench.Members.Contains(h.Id));
        var m = TheyMove(g, mover); Leans(g, m, g.W.House(g.Mine.Id)!, 1); Leans(g, m, c, -1);   // your house leans for it, and you have not voted; your client leans against it of itself
        int forecast = g.Senate.Stance(g.W, m, c.Id); Assert.Equal(-1, forecast);
        Session(g);
        Assert.Equal(1, m.Votes[g.Mine.Id]); Assert.Equal(forecast, m.Votes[c.Id]);   // your house's vote was cast for you at the count, after your client's: no way you had chosen
    }

    [Fact]
    public void AClientAtTheDoor_AtAnElection_GivesItsSeatsToYourHouseWhateverItsBenchSays_UnlessOneOfItsOwnStands()
    {
        // the consuls: your house stands, and your client's seats are counted for it, not for its own bench's nominee
        var g = Start(); g.W.Turn = ElectionSystem.Every - 1; Assert.True(g.Do(new StandForConsulCommand(g.Me.Id, g.Senate.Offices)));
        var c = TakeClient(g, ElectionSystem.Nominees(g.W, g.Senate.Offices, Wt(g)).Select(n => n.houseId).Concat(FactionSystem.Of(g.W, g.Mine.Id)!.Members));   // a client that is not its own bench's nominee, nor of your bench
        var noms = ElectionSystem.Nominees(g.W, g.Senate.Offices, Wt(g)); Assert.Contains(noms, n => n.houseId == g.Mine.Id); Assert.DoesNotContain(noms, n => n.houseId == c.Id);
        Assert.Equal(g.Mine.Id, SeatsOf(ElectionSystem.Tally(g.W, g.Senate.Offices, id => id == c.Id ? 1000 : 0, noms)));
        var tally = TallyNow(g); Session(g);
        Assert.Contains(g.W.History, h => h.Text.StartsWith("The Senate elects its consuls") && h.Text.Contains($"{g.Mine.Name} ({tally[g.Mine.Id]})"));   // counted so at the count
        // the ladder: one of yours stands for praetor, and your client's seats go to them, unless the client house has one of its own standing
        var (h, rep) = Stands(); Answer(h, rep, "let_them_win_it"); var client = TakeClient(h, FactionSystem.Of(h.W, h.Mine.Id)!.Members); var rung = h.Senate.Offices.Rung(LadderSystem.Praetor)!;
        h.W.Relationship(h.HeadOf(client).Id, h.Me.Id).Disposition = -50;   // its head thinks ill of you: what it gives you, it gives as your client
        var cands = LadderSystem.Candidates(h.W, h.Senate.Offices, rung, new HashSet<string>());
        string seats = SeatsOf(LadderSystem.Tally(h.W, h.Senate.Offices, rung, cands, id => id == client.Id ? 1000 : 0));
        var own = cands.Where(x => x.HouseId == client.Id).OrderByDescending(LadderSystem.Merit).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
        Assert.Equal(own?.Id ?? Bastien(h).Id, seats);
        foreach (var x in h.W.LivingCharacters().Where(x => x.HouseId == client.Id)) x.CareerTrack = "";   // none of its own can stand now
        cands = LadderSystem.Candidates(h.W, h.Senate.Offices, rung, new HashSet<string>()); Assert.DoesNotContain(cands, x => x.HouseId == client.Id);
        Assert.Equal(Bastien(h).Id, SeatsOf(LadderSystem.Tally(h.W, h.Senate.Offices, rung, cands, id => id == client.Id ? 1000 : 0)));
        int expected = LadderSystem.Tally(h.W, h.Senate.Offices, rung, cands, Wt(h))[Bastien(h).Id];
        Session(h); Assert.True(h.W.HasFlag(LadderSystem.HeldFlag)); Assert.Equal(expected, AtTheCount(h, Bastien(h)));
    }

    /// <summary>The player's house puts itself under a greater house's protection, as the window does; that house has another client.</summary>
    private static (G g, House patron, House fellow) Protected(ulong seed = 7, Func<G, House>? pick = null)
    {
        var g = Start(seed); g.Mine.Wealth = 10;
        var patron = pick?.Invoke(g) ?? Others(g).First(h => h.PatronId.Length == 0 && !OrderSystem.ClientsOf(g.W, h.Id).Any());
        patron.Wealth = Math.Max(1000, patron.Wealth); patron.Treasury.Credits = Math.Max(200, patron.Treasury.Credits); g.W.Relationship(g.HeadOf(patron).Id, g.Me.Id).Disposition = 20;
        var seek = new ClientCommand(g.Me.Id, ClientCommand.Seek, patron.Id, g.Tc.Orders); Assert.True(g.Do(seek), seek.Refusal(g.Ctx)); Assert.Equal(patron.Id, g.Mine.PatronId);
        var fellow = Others(g).First(h => h.Id != patron.Id && h.PatronId.Length == 0 && !OrderSystem.ClientsOf(g.W, h.Id).Any()); fellow.Wealth = 10; fellow.Debt = Math.Max(1, fellow.Debt);
        g.Tc.Orders.TakeClient(g.W, patron, fellow); Assert.Equal(patron.Id, fellow.PatronId);
        return (g, patron, fellow);
    }

    [Fact]
    public void UnderProtection_YourPatronAndItsOtherClients_LeanTheWayYouVote_ByTwoAndOne_AcrossASaveAndALoad()
    {
        var (g0, patron0, fellow0) = Protected(); Assert.True(g0.W.HasFlag(OrderSystem.BecameClientFlag));
        var f = ReportOf(g0, "under_protection"); Answer(g0, f, "be_a_good_client");
        var mover = Others(g0).First(h => h.Id != patron0.Id && h.Id != fellow0.Id); var m0 = TheyMove(g0, mover); IVote(g0, m0, 1);
        Assert.Equal(2, By(g0, m0, patron0, "it leans the way you vote: it is your patron")); Assert.Equal(1, By(g0, m0, fellow0, "it leans the way you vote: it is a fellow client of your patron"));
        Leans(g0, m0, patron0, 1); Leans(g0, m0, fellow0, 1);   // against it of themselves, by one and by nothing; for it by your lead
        var g = Reload(g0); var m = g.W.Motions.First(x => x.Id == m0.Id);   // a save and a load before the count
        Assert.Equal(1, g.Senate.Stance(g.W, m, patron0.Id)); Assert.Equal(1, g.Senate.Stance(g.W, m, fellow0.Id));
        Session(g);
        Assert.Equal(1, m.Votes[patron0.Id]); Assert.Equal(1, m.Votes[fellow0.Id]);
    }

    [Fact]
    public void UnderProtection_AtTheConsulsElection_YourSeatsGoToThePatron_UnlessYouStandOrPledgeThemElsewhere()
    {
        G Year(out House patron)
        {
            var probe = Start(); probe.W.Turn = ElectionSystem.Every - 1; string pid = ElectionSystem.Nominees(probe.W, probe.Senate.Offices, Wt(probe)).Select(n => n.houseId).First(id => id != probe.Mine.Id && probe.W.House(id)!.PatronId.Length == 0);
            var (g, p, _) = Protected(7, x => x.W.House(pid)!); g.W.Turn = ElectionSystem.Every - 1; patron = p; return g;
        }
        var g = Year(out var patron); var noms = ElectionSystem.Nominees(g.W, g.Senate.Offices, Wt(g)); Assert.Contains(noms, n => n.houseId == patron.Id);
        Assert.Equal(patron.Id, SeatsOf(ElectionSystem.Tally(g.W, g.Senate.Offices, id => id == g.Mine.Id ? 1000 : 0, noms)));   // your seats are the patron's
        var tally = TallyNow(g); Session(g);
        Assert.Contains(g.W.History, h => h.Text.StartsWith("The Senate elects its consuls") && h.Text.Contains($"{patron.Name} ({tally[patron.Id]})"));
        // pledged elsewhere, they go where you pledged them
        var p2 = Year(out var patron2); var to = ElectionSystem.Nominees(p2.W, p2.Senate.Offices, Wt(p2)).Select(n => n.houseId).First(id => id != patron2.Id);
        Assert.True(p2.Do(new PledgeVotesCommand(p2.Me.Id, to, p2.Senate.Offices, p2.Senate)));
        Assert.Equal(to, SeatsOf(ElectionSystem.Tally(p2.W, p2.Senate.Offices, id => id == p2.Mine.Id ? 1000 : 0, ElectionSystem.Nominees(p2.W, p2.Senate.Offices, Wt(p2)))));
        // standing yourself, they are your own
        var s3 = Year(out _); Assert.True(s3.Do(new StandForConsulCommand(s3.Me.Id, s3.Senate.Offices)));
        Assert.Equal(s3.Mine.Id, SeatsOf(ElectionSystem.Tally(s3.W, s3.Senate.Offices, id => id == s3.Mine.Id ? 1000 : 0, ElectionSystem.Nominees(s3.W, s3.Senate.Offices, Wt(s3)))));
    }

    // ------------------------------------------------------------------ the patron's wish: SenateSystem.PatronWish, the_patron_noticed

    [Fact]
    public void ThePatronsWish_YourVoteOnWhatTouchesIt_IsPlusFiveWith_MinusFifteenAgainst_AndThePatronNoticedComes()
    {
        var (g, patron, _) = Protected(); var m = TheyMove(g, patron); IVote(g, m, -1);
        int regard = g.Regard(patron); Session(g);
        Assert.Equal(regard + SenateSystem.DefyRegard, g.Regard(patron)); Assert.True(g.W.HasFlag(SenateSystem.DefiedPatronFlag));
        Assert.Contains(g.W.History, h => h.Text.Contains($"{g.Mine.Name} votes against the wishes of its patron, {patron.Name}. It is noted."));
        var f = ReportOf(g, "the_patron_noticed"); Answer(g, f, "stand_by_it");
        // a censure of the patron moved by another: against it is the patron's wish
        var mover = Others(g).First(h => h.Id != patron.Id && h.PatronId != patron.Id); var censure = g.Senate.Add(g.W, mover.Id, "censure", patron.Id); Only(g, censure); IVote(g, censure, -1);
        regard = g.Regard(patron); Session(g);
        Assert.Equal(regard + SenateSystem.ObligeRegard, g.Regard(patron)); Assert.True(g.W.HasFlag(SenateSystem.ObligedPatronFlag)); Assert.False(g.W.HasFlag(SenateSystem.DefiedPatronFlag));
        // a motion that does not touch it: no wish, and nothing noted
        var other = g.Senate.Add(g.W, mover.Id, "levy", ""); Only(g, other); IVote(g, other, -1); regard = g.Regard(patron); Session(g);
        Assert.Equal(regard, g.Regard(patron)); Assert.False(g.W.HasFlag(SenateSystem.ObligedPatronFlag)); Assert.False(g.W.HasFlag(SenateSystem.DefiedPatronFlag));
    }

    // ------------------------------------------------------------------ a client asks: a_client_asks/help_him

    [Fact]
    public void AClientAsks_PaidFor_TheClientsHouseVotesForYourNextMotion_HoweverHardItLeansAgainst_AndCannotBeAskedTwice()
    {
        var g = Start(); var c = TakeClient(g); g.Tc.Orders.Tick(g.W); Assert.True(g.W.HasFlag(OrderSystem.IsPatronFlag));
        Only(g); Assert.True(g.W.HasFlag(VoteMarks.ClientFlag));
        var f = ReportOf(g, "a_client_asks"); int credits = g.Mine.Treasury.Credits; g.W.News.Clear(); Answer(g, f, "help_him");
        Assert.Equal(credits - 3, g.Mine.Treasury.Credits); Assert.True(WealthSystem.FavourOwed(g.W, c.Id, g.Mine.Id));
        Assert.True(g.Told($"{g.HeadOf(c).Name} of {c.Name}, your client, owes the house a vote for the advocate you paid"));
        Assert.Contains(g.W.History, h => h.Text.Contains("The client's house owes the house a vote for it"));
        // a favour stands now: the choice is closed, and says why
        Assert.False(g.W.HasFlag(VoteMarks.ClientFlag)); var engine = new EventEngine(Content, WorldBuilder.RngFor(g.W)); Assert.False(engine.IsChoiceAvailable(f, g.W, "help_him"));
        Assert.Contains("needs a client house with no favour standing between it and yours", ChoiceStakes.Unmet(f.Def.Choice("help_him")!, new EvalContext(g.W, f.Binding, WorldBuilder.RngFor(g.W), Content)));
        var g1 = Reload(g); var c1 = g1.W.House(c.Id)!;   // a save and a load before the count
        var levy = IMove(g1); HardAgainst(g1, levy, c1); Assert.Equal(1, g1.Senate.Stance(g1.W, levy, c1.Id)); Session(g1);
        Assert.Equal(1, levy.Votes[c1.Id]); Assert.False(WealthSystem.FavourOwed(g1.W, c1.Id, g1.Mine.Id)); Assert.True(g1.Told($"{c1.Name} kept its word"));
    }

    // ------------------------------------------------------------------ the growing bloc: the_senate_listens/build_bloc, spread_credit

    [Theory]
    [InlineData("build_bloc")]
    [InlineData("spread_credit")]
    public void TheGrowingBloc_TheSenatorBroughtOver_TheirHouseVotesForYourNextMotion_HoweverHardItLeansAgainst(string choice)
    {
        var g = Start(); g.W.WorldCounters["senate_influence"] = 0; if (choice == "spread_credit") g.Me.AptitudeTraits.Add("Schemer");
        Only(g); Assert.True(g.W.HasFlag(VoteMarks.BlocFlag));
        // the one who comes over: of a house with no favour standing, who thinks best of you
        var best = Others(g).Where(h => !WealthSystem.FavourOwed(g.W, h.Id, g.Mine.Id) && !WealthSystem.FavourOwed(g.W, g.Mine.Id, h.Id) && !g.W.AtWar(h.Id, g.Mine.Id))
            .Select(h => g.HeadOf(h)).OrderByDescending(c => g.W.GetRelationship(c.Id, g.Me.Id)?.Disposition ?? 0).ThenBy(c => c.Id, StringComparer.Ordinal).First();
        var f = ReportOf(g, "the_senate_listens"); g.W.News.Clear(); Answer(g, f, choice);
        var theirs = g.W.House(best.HouseId)!; Assert.True(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id));
        Assert.True(g.Told($"{best.Name} of {theirs.Name} has come over to your side: {theirs.Name} owes your house a vote"));
        Assert.DoesNotContain(VoteMarks.BargainOpenChar, best.Flags);   // and no second bargain with that house while it stands
        var levy = IMove(g); HardAgainst(g, levy, theirs); Session(g);
        Assert.Equal(1, levy.Votes[theirs.Id]); Assert.False(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id)); Assert.True(g.Told($"{theirs.Name} kept its word"));
    }

    [Fact]
    public void TheGrowingBloc_BringsOverOnlyAHouseOfTheReachWithWhichABargainMayBeStruck_AndComesOnlyWhileThereIsOne()
    {
        var g = Start(); Only(g); var standing = Others(g)[0]; WealthSystem.OweFor(g.W, standing.Id, g.Mine.Id); var enemy = Others(g)[1]; g.Tc.Fleets.DeclareWar(g.W, g.Mine.Id, enemy.Id);
        VoteMarks.Docket(g.W, g.Senate, g.Mine.Id);
        Assert.DoesNotContain(VoteMarks.BargainOpenChar, g.HeadOf(standing).Flags); Assert.DoesNotContain(VoteMarks.BargainOpenChar, g.HeadOf(enemy).Flags);
        Assert.DoesNotContain(g.W.LivingCharacters(), c => c.Flags.Contains(VoteMarks.BargainOpenChar) && (c.HouseId == g.Mine.Id || World.Outsider(c.HouseId) || !PolitySystem.InReach(g.W, c.HouseId)));
        Assert.Contains(VoteMarks.BargainOpenChar, g.HeadOf(Others(g)[2]).Flags);
        // every head that could come over thinks ill of you: nobody is ready to, and the report does not come
        foreach (var c in g.W.LivingCharacters().Where(c => c.HouseId != g.Mine.Id)) g.W.Relationship(c.Id, g.Me.Id).Disposition = -5;
        VoteMarks.Docket(g.W, g.Senate, g.Mine.Id); Assert.False(g.W.HasFlag(VoteMarks.BlocFlag));
        g.W.WorldCounters["senate_influence"] = 0; Assert.DoesNotContain(new EventEngine(Content, WorldBuilder.RngFor(g.W)).GatherEligible(g.W), e => e.def.Id == "the_senate_listens");
    }

    // ------------------------------------------------------------------ across the aisle: across_the_aisle, split_the_bench

    /// <summary>The leader of a bench of three or more, not yours, moves a censure of your house, and the chamber leans for it.</summary>
    private static (G g, House leader, Faction bench, Motion censure) Aisle(Action<G, Motion, Faction>? leans = null)
    {
        var g = Start(); var bench = g.W.Factions.Where(f => !f.Members.Contains(g.Mine.Id) && f.Members.Count(id => g.W.House(id) != null && id != f.LeaderHouseId) >= 2 && g.W.House(f.LeaderHouseId) != null)
            .OrderBy(f => f.Id, StringComparer.Ordinal).First();
        var leader = g.W.House(bench.LeaderHouseId)!; var censure = g.Senate.Add(g.W, leader.Id, "censure", g.Mine.Id);
        AllFor(g, censure); leans?.Invoke(g, censure, bench);
        g.Senate.Tick(g.W); Only(g, censure);   // the session that hears it writes the docket's flags, once its motions are moved
        return (g, leader, bench, censure);
    }

    [Fact]
    public void AcrossTheAisle_ComesWhileTheMotionIsBeforeTheSenate_AndEveryHouseOfTheBenchLeansAsItsLeaderVotes_SaveOneWithAReasonOfItsOwn()
    {
        House? stands = null;
        var (g, leader, bench, censure) = Aisle((g, m, b) =>
        {
            stands = b.Members.Where(id => id != b.LeaderHouseId).Select(id => g.W.House(id)!).OrderBy(h => h.Id, StringComparer.Ordinal).First();
            g.W.Relationship(g.HeadOf(stands).Id, g.Me.Id).Disposition = 40; Leans(g, m, stands, -1);   // it stands with your house, and that is a reason of its own
        });
        Assert.True(g.W.HasFlag(SenateSystem.AgainstMeFlag)); var report = ReportOf(g, "across_the_aisle"); Answer(g, report, "face_them");
        foreach (var id in bench.Members.Where(id => id != leader.Id)) Assert.Equal(FactionSystem.Discipline, By(g, censure, g.W.House(id)!, $"it follows {leader.Name}, who leads {bench.Name}"));
        Assert.Equal(-2, By(g, censure, stands!, "it stands with"));
        var stances = bench.Members.ToDictionary(id => id, id => g.Senate.Stance(g.W, censure, id)); Session(g);
        foreach (var id in bench.Members) Assert.Equal(stances[id], censure.Votes[id]);   // as the whip count showed them
        Assert.Equal(1, censure.Votes[leader.Id]); Assert.Equal(-1, censure.Votes[stands!.Id]);
    }

    [Fact]
    public void AcrossTheAisle_IsNotAMotionForYourHouse_NorOneTheCountWouldThrowOut()
    {
        var g = Start(); var bench = g.W.Factions.First(f => !f.Members.Contains(g.Mine.Id) && g.W.House(f.LeaderHouseId) != null); var leader = g.W.House(bench.LeaderHouseId)!;
        var recognition = g.Senate.Add(g.W, leader.Id, "recognition", g.Mine.Id); AllFor(g, recognition); g.Senate.Tick(g.W); Only(g, recognition);
        Assert.False(g.W.HasFlag(SenateSystem.AgainstMeFlag));   // a motion that names your house for its good is not one against it
        var h = Start(); var hb = h.W.Factions.First(f => !f.Members.Contains(h.Mine.Id) && h.W.House(f.LeaderHouseId) != null);
        var censure = h.Senate.Add(h.W, hb.LeaderHouseId, "censure", h.Mine.Id); foreach (var x in Others(h).Where(x => x.Id != hb.LeaderHouseId)) Leans(h, censure, x, -2);
        h.Senate.Tick(h.W); Only(h, censure);
        Assert.False(h.W.HasFlag(SenateSystem.AgainstMeFlag));   // the count as it stands throws it out: the arithmetic is not against you
    }

    [Fact]
    public void AcrossTheAisle_SplitTheBench_ItsLeastHouseThatWouldVoteForIt_VotesAgainst_HoweverHardItLeansFor_AcrossASaveAndALoad()
    {
        var (g0, leader0, bench0, censure0) = Aisle((g, m, b) => { foreach (var id in b.Members.Where(id => id != b.LeaderHouseId)) Leans(g, m, g.W.House(id)!, 6); });
        var weakest0 = bench0.Members.Where(id => id != leader0.Id).OrderBy(id => g0.Senate.Weight(g0.W, id)).ThenBy(id => g0.Senate.Lean(g0.W, censure0, id)).ThenBy(id => id, StringComparer.Ordinal).Select(id => g0.W.House(id)!).First();
        Assert.True(g0.W.HasFlag(VoteMarks.SplittableFlag)); Assert.Contains(VoteMarks.WeakestChar, g0.HeadOf(weakest0).Flags);
        g0.Me.Skills["intrigue"] = 3; g0.W.PoolsFor(g0.Me.Id).Agents = Math.Max(1, g0.W.PoolsFor(g0.Me.Id).Agents); int agents = g0.W.PoolsFor(g0.Me.Id).Agents;
        var (forBefore, againstBefore) = g0.Senate.Tally(g0.W, censure0); int wt = g0.Senate.Weight(g0.W, weakest0.Id);
        g0.W.News.Clear(); Choose(g0, ReportOf(g0, "across_the_aisle"), "split_the_bench");
        Assert.Equal(agents - 1, g0.W.PoolsFor(g0.Me.Id).Agents); Assert.Equal(-1, censure0.Votes[weakest0.Id]);
        Assert.True(g0.Told($"{weakest0.Name}, the least of {bench0.Name}'s houses, has been found what it wanted: it votes against the motion against your house"));
        Assert.Equal((forBefore - wt, againstBefore + wt), g0.Senate.Tally(g0.W, censure0));   // by morning the whip count had changed
        var g = Reload(g0); var censure = g.W.Motions.First(m => m.Id == censure0.Id); Assert.True(g.Senate.Lean(g.W, censure, weakest0.Id) > 0);   // it still leans for it, and votes against
        Session(g);
        Assert.Equal(-1, censure.Votes[weakest0.Id]); Assert.Equal(AgainstWeight(g, censure), censure.AgainstWeight);
    }

    [Fact]
    public void AcrossTheAisle_SplitTheBench_IsClosedWithItsReason_WhenNoHouseOfTheBenchWouldVoteForIt()
    {
        var (g, _, bench, censure) = Aisle((g, m, b) => { foreach (var id in b.Members.Where(id => id != b.LeaderHouseId)) Leans(g, m, g.W.House(id)!, -1); });
        Assert.True(g.W.HasFlag(SenateSystem.AgainstMeFlag)); Assert.False(g.W.HasFlag(VoteMarks.SplittableFlag)); g.Me.Skills["intrigue"] = 3;
        var f = ReportOf(g, "across_the_aisle"); var engine = new EventEngine(Content, WorldBuilder.RngFor(g.W)); Assert.False(engine.IsChoiceAvailable(f, g.W, "split_the_bench"));
        var why = ChoiceStakes.Unmet(f.Def.Choice("split_the_bench")!, new EvalContext(g.W, f.Binding, WorldBuilder.RngFor(g.W), Content));
        Assert.Contains("needs a house of that bench, not its leader, that would vote for the motion", why);
    }

    // ================================================================== the other group (docs/events/votes.md, "Promises", the other group)

    // ------------------------------------------------------------------ the vote still undecided: the_vote_still_undecided

    /// <summary>Your motion before the Senate, and one head of another house who leans neither way on it and thinks <paramref name="regard"/>
    /// of you; every other head thinks nothing of you, so the report is about that one.</summary>
    private static (G g, Motion mine, House theirs) Undecided(int regard, ulong seed = 7)
    {
        var g = Start(seed); var mine = IMove(g); var theirs = Others(g)[0];
        foreach (var h in Others(g)) g.W.Relationship(g.HeadOf(h).Id, g.Me.Id).Disposition = h == theirs ? regard : 0;
        Leans(g, mine, theirs, 0); Only(g, mine);
        Assert.Contains(VoteMarks.UndecidedChar, g.HeadOf(theirs).Flags);
        return (g, mine, theirs);
    }

    [Theory]
    [InlineData("have_them_to_dinner", 12, 8, 1)]   // the likeliest: 12 to 19 is 20 or more after it, every time
    [InlineData("make_your_case", 14, 6, 1)]   // it may be enough
    [InlineData("make_your_case", 12, 6, 0)]   // and it may not
    [InlineData("speak_from_the_floor", 16, 4, 1)]   // if they were nearly there
    [InlineData("speak_from_the_floor", 15, 4, 0)]
    [InlineData("leave_them_be", 19, 0, 0)]
    public void TheVoteStillUndecided_EachChoiceMovesTheirRegardByItsNumber_AndAtTwentyTheirHouseVotesForIt_AcrossASaveAndALoad(string choice, int regard, int by, int vote)
    {
        var (g0, mine0, theirs) = Undecided(regard);
        var (f, doubter) = Report(g0, "the_vote_still_undecided", "doubter"); Assert.Equal(g0.HeadOf(theirs).Id, doubter.Id);
        Choose(g0, f, choice);
        Assert.Equal(regard + by, g0.Regard(theirs));
        var g = Reload(g0); var mine = g.W.Motions.First(m => m.Id == mine0.Id);
        Assert.Equal(vote, g.Senate.Stance(g.W, mine, theirs.Id));   // the whip count has it as it will be counted
        if (vote > 0) Assert.Contains(g.Senate.Why(g.W, mine, theirs.Id), x => x.why.StartsWith("what its head thinks of") && x.by == 1);
        Session(g);
        Assert.True(mine.Resolved); Assert.Equal(vote, mine.Votes[theirs.Id]); Assert.Equal(ForWeight(g, mine), mine.ForWeight);
    }

    [Fact]
    public void TheVoteStillUndecided_IsAskedOnlyOfAHouseThatLeansNeitherWayAsTheYearOpens()
    {
        var (g, mine, theirs) = Undecided(15); Assert.True(Eligible(g, "the_vote_still_undecided", "doubter", theirs.Id));
        // between the session and the year's reports its mind is made up (here, someone has had its ear): it leans for your motion now
        mine.Lobby[theirs.Id] = mine.Lobby.GetValueOrDefault(theirs.Id) + 2; Assert.True(g.Senate.Lean(g.W, mine, theirs.Id) > 0);
        g.Tc.BeginTurn();
        Assert.DoesNotContain(VoteMarks.UndecidedChar, g.HeadOf(theirs).Flags);
        Assert.False(Eligible(g, "the_vote_still_undecided", "doubter", theirs.Id));
    }

    // ------------------------------------------------------------------ the old grudge: those_who_voted_against_you, an_old_grudge_against_your_motion

    /// <summary>Your motion carried with one house voting against it to the end, and you let them lose: the grudge, by the report.</summary>
    private static (G g, House theirs) Grudged(ulong seed = 7)
    {
        var g = Start(seed); var theirs = Others(g)[0]; var mine = IMove(g);
        foreach (var h in Others(g)) { g.W.Relationship(g.HeadOf(h).Id, g.Me.Id).Disposition = 0; Leans(g, mine, h, h == theirs ? -3 : 3); }
        Only(g, mine); Session(g); Assert.True(mine.Passed); Assert.Equal(-1, mine.Votes[theirs.Id]);
        var (f, loser) = Report(g, "those_who_voted_against_you", "loser"); Assert.Equal(g.HeadOf(theirs).Id, loser.Id);
        Choose(g, f, "let_them_lose");
        Assert.Contains("grudge_against_you", loser.Flags); Assert.Equal(-3, g.Regard(theirs));
        Only(g); return (g, theirs);
    }
    /// <summary>Your next motion, a relief of a region, with the rival's house leaning <paramref name="lean"/> on it.</summary>
    private static Motion NextMotion(G g, House theirs, int lean)
    {
        var next = IMove(g, "relief", g.W.Galaxy!.Regions.OrderBy(r => r.Id, StringComparer.Ordinal).First().Id); Leans(g, next, theirs, lean); Only(g, next); return next;
    }

    [Theory]
    [InlineData("let_it_cost_you", -22, -3, -1)]   // their house leans against your motion, and the grudge is spent on it
    [InlineData("answer_them_on_the_floor", -22, -4, -1)]   // they will still lean against it
    [InlineData("buy_them_off", -22, 12, 0)]   // they may think well enough of you again not to lean against it
    [InlineData("buy_them_off", -35, 12, -1)]   // and they may not
    public void TheHouseThatVotedAgainstYou_LetThemLose_ItsGrudgeComesBackAtYourNextMotion_AndEachAnswerIsCountedAsItSays(string choice, int regard, int by, int vote)
    {
        var (g, theirs) = Grudged(); var rival = g.HeadOf(theirs);
        g.W.Relationship(rival.Id, g.Me.Id).Disposition = regard;   // the years have soured it further
        var next = NextMotion(g, theirs, -1);   // only the old quarrel against it: what its head thinks of you, -1, and nothing more
        Assert.Contains(g.Senate.Why(g.W, next, theirs.Id), x => x.why.StartsWith("what its head thinks of") && x.by == -1);
        var (f, cast) = Report(g, "an_old_grudge_against_your_motion", "rival"); Assert.Equal(rival.Id, cast.Id);
        int legitimacy = g.Mine.Legitimacy; Choose(g, f, choice);
        Assert.Equal(regard + by, g.Regard(theirs)); Assert.DoesNotContain("grudge_against_you", rival.Flags);
        if (choice == "answer_them_on_the_floor") Assert.Equal(legitimacy + 1, g.Mine.Legitimacy);   // the chamber thinks the better of you
        Session(g);
        Assert.Equal(vote, next.Votes[theirs.Id]); Assert.Equal(AgainstWeight(g, next), next.AgainstWeight);
        // spent: the grudge comes to neither report again
        Assert.False(Eligible(g, "an_old_grudge_against_your_motion", "rival", theirs.Id)); Assert.False(Eligible(g, "an_old_grudge_on_the_docket", "rival", theirs.Id));
    }

    [Fact]
    public void AnOldGrudgeAgainstYourMotion_IsNotSaidOfAHouseThatLeansForIt_NorOfOneWhoseHeadDoesNotHoldTheGrudge()
    {
        var (g, theirs) = Grudged(); var rival = g.HeadOf(theirs); g.W.Relationship(rival.Id, g.Me.Id).Disposition = -22;
        var next = NextMotion(g, theirs, 2);   // a grudge, and its house is for your motion all the same: "{rival.name} does" lean against it would be false
        Assert.DoesNotContain(VoteMarks.AgainstYoursChar, rival.Flags); Assert.False(Eligible(g, "an_old_grudge_against_your_motion", "rival", theirs.Id));
        Leans(g, next, theirs, -1); Only(g, next); Assert.True(Eligible(g, "an_old_grudge_against_your_motion", "rival", theirs.Id));
        // the grudge held by one of the house who is not its head: their regard leans nothing, so no report says the house leans their way
        rival.Flags.Remove("grudge_against_you");
        var kin = g.W.LivingCharacters().First(c => c.HouseId == theirs.Id && c.Id != rival.Id && c.Age >= DynastySystem.AgeOfMajority);
        kin.Flags.Add("grudge_against_you"); g.W.Relationship(kin.Id, g.Me.Id).Disposition = -40; Only(g, next);
        Assert.False(Eligible(g, "an_old_grudge_against_your_motion", "rival", theirs.Id));
    }

    [Fact]
    public void AnOldGrudgeOnTheDocket_MakeYourPeace_MendsTheQuarrel_ButTheirMotionAgainstYouIsStillCounted_AndTheyVoteForIt()
    {
        var g = Start(); var theirs = Others(g).First(h => g.Senate.MayPropose(g.W, h.Id)); var rival = g.HeadOf(theirs);
        rival.Flags.Add("grudge_against_you"); g.W.Relationship(rival.Id, g.Me.Id).Disposition = -30;   // the grudge, as let_them_lose leaves it (above)
        var censure = TheyMove(g, theirs, "censure", g.Mine.Id);
        var (f, cast) = Report(g, "an_old_grudge_on_the_docket", "rival"); Assert.Equal(rival.Id, cast.Id);
        int credits = g.Mine.Treasury.Credits; Choose(g, f, "make_your_peace");
        Assert.Equal(-18, g.Regard(theirs)); Assert.Equal(credits - 6, g.Mine.Treasury.Credits); Assert.DoesNotContain("grudge_against_you", rival.Flags);
        Assert.Equal(3, g.Senate.RawLean(g.W, censure, theirs.Id)); Assert.True(g.Senate.Lean(g.W, censure, theirs.Id) > 0);   // the mover's own lean is what it was
        Session(g);
        Assert.True(censure.Resolved); Assert.False(censure.Vetoed); Assert.Equal(1, censure.Votes[theirs.Id]);
    }

    // ------------------------------------------------------------------ the repeal promised: the_senates_act_falls_on_a_friend/promise_the_repeal

    /// <summary>An Act carries against the votes of a friend's house (another house moves the canvassing law to another option), and the
    /// friend writes to you; you promise the repeal. Returns the first year the chamber will hear it again.</summary>
    private static (G g, House theirs, Character friend, LawDef law, string act, int from) RepealPromised(ulong seed = 7)
    {
        var g = Start(seed); var others = Others(g); var theirs = others[0]; var mover = others.First(h => h != theirs && g.Senate.MayPropose(g.W, h.Id));
        var friend = g.HeadOf(theirs); g.W.Relationship(friend.Id, g.Me.Id).Disposition = 30; g.W.Relationship(g.Me.Id, friend.Id).Disposition = 30;
        var law = Content.Policies.Law("canvassing")!; string was = (PolicySystem.ReachOption(g.W, Content.Policies, law) ?? law.DefaultOption).Id;
        string act = law.Options.First(o => o.Id != was).Id;
        var m = g.Senate.Add(g.W, mover.Id, "act", "", lawId: law.Id, lawOption: act); Only(g, m);
        foreach (var h in others.Where(h => h != mover)) Leans(g, m, h, h == theirs ? -4 : 3); IVote(g, m, 1);
        Session(g); Assert.True(m.Passed); Assert.Equal(act, (PolicySystem.ReachOption(g.W, Content.Policies, law) ?? law.DefaultOption).Id);
        Only(g);
        var (f, cast) = Report(g, "the_senates_act_falls_on_a_friend", "friend"); Assert.Equal(friend.Id, cast.Id);
        int before = g.Regard(theirs); Choose(g, f, "promise_the_repeal"); Assert.Equal(before + 6, g.Regard(theirs));
        int from = g.W.Counter($"law_cd:{PolicySystem.SenateHolder}:{law.Id}"); Assert.True(from > g.W.Turn);
        Assert.True(g.Told($"You promised {friend.Name} of {theirs.Name} to move the repeal of") && g.Told($"from year {from}, and by year {from + VoteMarks.RepealYears - 1}"));
        return (g, theirs, friend, law, act, from);
    }
    private static ProposeMotionCommand RepealOf(G g, LawDef law, string act) => new(g.Me.Id, "act", "", g.Senate, lawId: law.Id, optionId: law.Options.First(o => o.Id != act).Id);
    /// <summary>The year turns to this one, with nothing of anyone else's before the Senate.</summary>
    private static void YearOf(G g, int turn) { g.W.Turn = turn - 1; Session(g); Only(g); }

    [Fact]
    public void TheActFallsOnAFriend_PromiseTheRepeal_WaitsUntilTheChamberWillHearIt_AndMovingItKeepsYourWord_ToANewHead_AcrossASaveAndALoad()
    {
        var (g0, theirs, friend, law, act, from) = RepealPromised();
        Assert.Contains("the Senate settled that lately", RepealOf(g0, law, act).Refusal(g0.Ctx));   // the chamber will not hear it yet
        var g = Reload(g0);
        DynastySystem.Die(g.W, friend.Id); var head = g.HeadOf(g.W.House(theirs.Id)!); Assert.NotEqual(friend.Id, head.Id);   // the house's word is held by its next head
        YearOf(g, from - 1); Assert.True(g.Told($"From next year the chamber will hear the repeal of"));
        g.W.Turn = from; int regard = g.Regard(theirs); var move = RepealOf(g, law, act);
        Assert.True(g.Do(move), move.Refusal(g.Ctx));
        Assert.Equal(regard + DealingsSystem.PledgeKeptRegard, g.Regard(theirs)); Assert.True(g.Told($"as you promised {theirs.Name}: its head thinks +3 of you"));
        Assert.Single(DealingsSystem.Remembered(g.W, DealingsSystem.PledgeKeptKind, g.Mine.Id, theirs.Id));
        Assert.DoesNotContain(g.W.WorldCounters.Keys, k => k.StartsWith(VoteMarks.RepealPrefix, StringComparison.Ordinal));
        YearOf(g, from + VoteMarks.RepealYears); Assert.Equal(regard + DealingsSystem.PledgeKeptRegard, g.Regard(theirs));   // nothing more is asked of you
    }

    [Fact]
    public void TheActFallsOnAFriend_PromiseTheRepeal_LetTheLastYearPassWithoutIt_AndItsHeadThinksFifteenTheWorse_ItIsWrittenDown_AndTakesNoWordOfYours()
    {
        var (g, theirs, _, law, act, from) = RepealPromised(); int by = from + VoteMarks.RepealYears - 1;
        YearOf(g, by - 1); Assert.True(g.Told("Next year is the last in which to move the repeal of"));
        int regard = g.Regard(theirs); YearOf(g, by);
        Assert.Equal(regard - DealingsSystem.PledgeBrokenRegard, g.Regard(theirs)); Assert.True(g.Told($"and year {by} has passed without it. Its head thinks -15 of you"));
        Assert.Single(DealingsSystem.Remembered(g.W, DealingsSystem.PledgeBrokenKind, g.Mine.Id, theirs.Id));
        Assert.Contains(g.Tc.Dealings.Lasting(g.W, g.Mine, theirs), l => l.text == "Your word to it on a vote: kept 0 times, broken 1 time.");
        Assert.Contains(VoteMarks.BargainBarredChar, g.HeadOf(theirs).Flags);   // and no bargain is struck with it for eight years
    }

    [Fact]
    public void TheActFallsOnAFriend_PromiseTheRepeal_IsSpentWithNothingOwed_WhenAnotherHouseMovesItFirst_OrTheHousesGoToWar()
    {
        var (g, theirs, _, law, act, from) = RepealPromised(); int regard = g.Regard(theirs);
        g.W.Turn = from; var other = Others(g).First(h => h != theirs && g.Senate.MayPropose(g.W, h.Id));
        g.Senate.Add(g.W, other.Id, "act", "", lawId: law.Id, lawOption: law.Options.First(o => o.Id != act).Id);
        Assert.True(g.Told($"{other.Name} has moved the repeal of")); Assert.Equal(regard, g.Regard(theirs));
        Assert.DoesNotContain(g.W.WorldCounters.Keys, k => k.StartsWith(VoteMarks.RepealPrefix, StringComparison.Ordinal));
        var (g2, theirs2, _, _, _, _) = RepealPromised(); g2.W.Wars.Add(new War { HouseA = g2.Mine.Id, HouseB = theirs2.Id, Since = g2.W.Turn }); Session(g2);
        Assert.True(g2.Told($"You are at war with {theirs2.Name}: your word to move the repeal of"));
        Assert.DoesNotContain(g2.W.WorldCounters.Keys, k => k.StartsWith(VoteMarks.RepealPrefix, StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------ the feast: recognised_by_the_senate/a_feast

    [Fact]
    public void Recognised_AFeastForTheHousesThatVotedIt_TheirHeadsThinkThreeTheBetterOfYou_AndYourNextCountHasIt_AcrossASaveAndALoad()
    {
        var g0 = Start(); var others = Others(g0); var mover = others.First(h => g0.Senate.MayPropose(g0.W, h.Id)); var no = others.Last(h => h != mover);
        foreach (var h in others) g0.W.Relationship(g0.HeadOf(h).Id, g0.Me.Id).Disposition = 17;
        var m = g0.Senate.Add(g0.W, mover.Id, "recognition", g0.Mine.Id); Only(g0, m);
        foreach (var h in others.Where(h => h != mover)) Leans(g0, m, h, h == no ? -2 : 2); IVote(g0, m, 1);
        Session(g0); Assert.True(m.Passed); Assert.True(g0.W.HasFlag("recognised")); Only(g0);
        var ayes = others.Where(h => m.Votes.GetValueOrDefault(h.Id) > 0).ToList(); Assert.Contains(mover, ayes); Assert.DoesNotContain(no, ayes);
        var aye = ayes.First(h => h != mover); var next = IMove(g0); Leans(g0, next, aye, 0); Only(g0, next);   // your next motion, on which it leans neither way at 17
        Choose(g0, ReportOf(g0, "recognised_by_the_senate"), "a_feast");
        foreach (var h in ayes) Assert.Equal(17 + VoteMarks.FeastRegard, g0.Regard(h)); Assert.Equal(17, g0.Regard(no));
        Assert.True(g0.Told("The houses that voted the recognition dined at your table"));
        var g = Reload(g0); next = g.W.Motions.First(x => x.Id == next.Id);
        Assert.Equal(1, g.Senate.Lean(g.W, next, aye.Id)); Assert.Contains(g.Senate.Why(g.W, next, aye.Id), x => x.why.StartsWith("what its head thinks of") && x.by == 1);
        Session(g);
        Assert.Equal(1, next.Votes[aye.Id]);   // a Senate that remembers the wine
    }

    // ------------------------------------------------------------------ the names written down: censured/remember_who_voted, the_motion_thrown_out/note_who_voted

    [Fact]
    public void Censured_WriteDownEveryName_EachHouseThatVotedForItIsWrittenOnItsPage_AcrossASaveAndALoad()
    {
        var g0 = Start(); var others = Others(g0); var mover = others.First(h => g0.Senate.MayPropose(g0.W, h.Id));
        var friends = others.Where(h => h != mover).Take(2).ToList();   // two stand with you
        var m = g0.Senate.Add(g0.W, mover.Id, "censure", g0.Mine.Id); Only(g0, m);
        foreach (var h in others.Where(h => h != mover)) Leans(g0, m, h, friends.Contains(h) ? -2 : 2); IVote(g0, m, -1);
        Session(g0); Assert.True(m.Passed); Assert.True(g0.W.HasFlag("censured"));
        var ayes = others.Where(h => m.Votes.GetValueOrDefault(h.Id) > 0).ToList(); Assert.Contains(mover, ayes);
        Choose(g0, ReportOf(g0, "censured"), "remember_who_voted");
        Assert.True(g0.Told("You wrote down the houses that voted for the censure of your house"));
        var g = Reload(g0); int year = m.ResolvedTurn;
        foreach (var h in ayes) Assert.Contains(g.Tc.Dealings.Lasting(g.W, g.Mine, g.W.House(h.Id)!), l => l.text == $"It voted for the censure of your house in year {year}, and you wrote the name down.");
        foreach (var h in friends) Assert.DoesNotContain(g.Tc.Dealings.Lasting(g.W, g.Mine, g.W.House(h.Id)!), l => l.text.Contains("you wrote the name down"));
    }

    /// <summary>Your motion thrown out: every other house against it but one, the first by name leaning only one against.</summary>
    private static (G g, Motion thrown, List<House> noes, House aye) ThrownOut(ulong seed = 7)
    {
        var g = Start(seed); var others = Others(g); var mine = IMove(g); var aye = others.Last(); var noes = others.Where(h => h != aye).ToList();
        foreach (var h in others) Leans(g, mine, h, h == aye ? 1 : -3);
        Only(g, mine); Session(g); Assert.False(mine.Passed); Assert.True(g.W.HasFlag("motion_failed_mine")); Only(g);
        return (g, mine, noes, aye);
    }

    [Fact]
    public void TheMotionThrownOut_WriteDownEveryName_EachHouseThatVotedItDownIsWrittenOnItsPage()
    {
        var (g, thrown, noes, aye) = ThrownOut(); Choose(g, ReportOf(g, "the_motion_thrown_out"), "note_who_voted");
        foreach (var h in noes) Assert.Contains(g.Tc.Dealings.Lasting(g.W, g.Mine, h), l => l.text == $"It voted your motion down in year {thrown.ResolvedTurn}, and you wrote the name down.");
        Assert.DoesNotContain(g.Tc.Dealings.Lasting(g.W, g.Mine, aye), l => l.text.Contains("you wrote the name down"));
    }

    [Fact]
    public void TheMotionThrownOut_StandTheChamberARound_EveryHouseThatVotedItDownLeansOneYourWayAtYourNextCount_SoItIsCloser_ThenItIsSpent_AcrossASaveAndALoad()
    {
        var (g0, _, noes, _) = ThrownOut(); var near = noes[0]; var far = noes[1];
        var next0 = NextMotion(g0, near, -1); Leans(g0, next0, far, -3); Only(g0, next0);   // your next motion, moved the same year
        int credits = g0.Mine.Treasury.Credits; Choose(g0, ReportOf(g0, "the_motion_thrown_out"), "buy_the_house_a_round");
        Assert.Equal(credits - 3, g0.Mine.Treasury.Credits); Assert.True(g0.Told("You stood the chamber a round"));
        var g = Reload(g0); var next = g.W.Motions.First(x => x.Id == next0.Id);
        foreach (var h in noes) Assert.Equal(VoteMarks.RoundBy, By(g, next, h, "you stood the chamber a round"));
        Assert.Equal(0, g.Senate.Lean(g.W, next, near.Id)); Assert.Equal(-2, g.Senate.Lean(g.W, next, far.Id));
        Session(g);
        Assert.Equal(0, next.Votes[near.Id]); Assert.Equal(-1, next.Votes[far.Id]);   // the one a round could bring over stays away: the count is closer by its seats
        Assert.Equal(AgainstWeight(g, next), next.AgainstWeight);
        Assert.DoesNotContain(g.W.WorldCounters.Keys, k => k.StartsWith(VoteMarks.RoundPrefix, StringComparison.Ordinal));   // spent on that count
    }

    [Fact]
    public void TheMotionThrownOut_ARoundStoodAndNoMotionAfterIt_IsForgottenAfterThreeYears_WithANotice()
    {
        var (g, _, _, _) = ThrownOut(); Choose(g, ReportOf(g, "the_motion_thrown_out"), "buy_the_house_a_round"); int since = g.W.Turn;
        for (int i = 1; i < VoteMarks.RoundYears; i++) { Session(g); Only(g); }
        Assert.Contains(g.W.WorldCounters.Keys, k => k.StartsWith(VoteMarks.RoundPrefix, StringComparison.Ordinal));
        Session(g);
        Assert.DoesNotContain(g.W.WorldCounters.Keys, k => k.StartsWith(VoteMarks.RoundPrefix, StringComparison.Ordinal));
        Assert.True(g.Told($"The round you stood the chamber in year {since} is forgotten"));
    }

    // ------------------------------------------------------------------ a name in commerce: a_name_in_commerce, lend_where_it_matters, DealingsSystem.LendSays

    [Fact]
    public void ANameInCommerce_LendWhereItMatters_LendsToTheTwoWeightiestHousesShortOfCoin_AtNoInterest_AndTheyLeanYourWayOnEveryDivision_AcrossASaveAndALoad()
    {
        var g0 = Start(); var others = Others(g0); foreach (var h in others) h.Treasury.Credits = 50;
        var poor = others.Where(h => DealingsSystem.LoanOwedBy(g0.W, h.Id) == null).OrderByDescending(h => h.SphereInfluence.GetValueOrDefault("senate") + Praetoria.Core.Progression.Title.RankOf(h.Title)).ThenBy(h => h.Id, StringComparer.Ordinal).Take(3).ToList();
        foreach (var h in poor) { h.Treasury.Credits = 2; g0.W.Relationship(g0.HeadOf(h).Id, g0.Me.Id).Disposition = 0; }   // three with seats and no coin: the two weightiest are lent to
        g0.W.WorldFlags.Add(OrderSystem.GreatFlag(OrderSystem.Commerce));   // the house is counted great in commerce
        int credits = g0.Mine.Treasury.Credits; Choose(g0, ReportOf(g0, "a_name_in_commerce"), "lend_where_it_matters");
        foreach (var h in poor.Take(2)) { var loan = DealingsSystem.LoanBetween(g0.W, g0.Mine.Id, h.Id)!; Assert.Equal(VoteMarks.LendEach, loan.Lent); Assert.Equal(0, loan.RatePct); Assert.Equal(VoteMarks.LendEach, loan.Owed); }
        Assert.Null(DealingsSystem.LoanBetween(g0.W, g0.Mine.Id, poor[2].Id)); Assert.Equal(credits - 6, g0.Mine.Treasury.Credits);
        Assert.True(g0.Told("at no interest"));
        var g = Reload(g0); var debtor = g.W.House(poor[0].Id)!;
        // on a motion you neither move nor are named in, once you have voted, the debtor leans your way by two, and the count follows
        var mover = Others(g).First(h => h.Id != poor[0].Id && h.Id != poor[1].Id); var m = Moves(g, mover); IVote(g, m, -1);
        Assert.Equal(-2, By(g, m, debtor, "it is in your debt")); Assert.Contains(g.Senate.Why(g.W, m, debtor.Id), x => x.why.StartsWith("it leans the way you vote: ") && x.why.Contains("it is in your debt"));
        Leans(g, m, debtor, -1);   // for it by one of itself, against it by your two
        Session(g); Assert.Equal(-1, m.Votes[debtor.Id]);
    }

    [Fact]
    public void ANameInCommerce_AHouseHoldingATenthOfACompanyYouControlLeansYourWay_OneHoldingLessDoesNot()
    {
        var g = Start(); var others = Others(g).Where(h => OrderSystem.Sway(g.W, h, g.Mine).lean == 0).ToList(); var holder = others[0]; var small = others[1];
        g.W.Companies.Add(new Company { Id = "test_company", Name = "Test Company", ControllerId = g.Mine.Id, Shares = 100, Holders = { [g.Mine.Id] = 51, [holder.Id] = 10, [small.Id] = 9 } });
        var m = Moves(g, others[2]); IVote(g, m, 1);
        Assert.Equal(1, By(g, m, holder, "it leans the way you vote: it holds a tenth of a company you control")); Assert.Equal(0, By(g, m, small, "it holds a tenth"));
        Leans(g, m, holder, 0); Session(g); Assert.Equal(0, m.Votes[holder.Id]);
    }

    [Fact]
    public void ALoanFromTheHouseWindow_WhileItOwesYouTheBorrowerLeansYourWay_OnWhatYouMoveAndOnEveryOtherDivision()
    {
        var g = Start(); House? them = null;
        foreach (var h in Others(g)) { int was = h.Treasury.Credits; h.Treasury.Credits = 2; if (new DealingsCommand(g.Me.Id, DealingsCommand.Lend, h.Id, g.Tc.Dealings, "5", 10).Refusal(g.Ctx) == null) { them = h; break; } h.Treasury.Credits = was; }
        Assert.NotNull(them); Assert.Contains("While it owes you it leans your way in the chamber", g.Tc.Dealings.LendSays(g.W, them!, 5, 10));
        Assert.True(g.Do(new DealingsCommand(g.Me.Id, DealingsCommand.Lend, them!.Id, g.Tc.Dealings, "5", 10)));
        var mover = Others(g).First(h => h != them); var m = Moves(g, mover); IVote(g, m, 1);
        Assert.Equal(2, By(g, m, them, "it is in your debt")); Leans(g, m, them, 1);
        Session(g); Assert.Equal(1, m.Votes[them.Id]);
        Only(g); var mine = IMove(g); Assert.Contains(g.Senate.Why(g.W, mine, them.Id), x => x.why.StartsWith("it depends on") && x.by == 2);
    }

    // ------------------------------------------------------------------ the corner: the_corner, OrderSystem.Sway

    [Fact]
    public void TheCorner_FleetsThatLoadTorpedoesFromYourIsotopesOrBurnYourFuelLeanYourWay_ARareEarthCornerLeansNobody()
    {
        var g = Start(); g.Mine.Corners.Clear();
        var them = Others(g).First(h => g.W.Fleets.Values.Any(f => f.OwnerId == h.Id) && !h.Corners.Any() && OrderSystem.Sway(g.W, h, g.Mine).lean == 0);
        var mine = IMove(g); Leans(g, mine, them, 0); Only(g, mine);
        foreach (var f in g.W.Fleets.Values.Where(f => f.OwnerId == them.Id)) f.Loadout = "guns";
        g.Mine.Corners.Clear(); g.Mine.Corners.Add(Resource.Isotopes);
        Assert.Equal(0, g.Senate.Lean(g.W, mine, them.Id));   // guns: nothing of yours in its magazines
        g.W.Fleets.Values.First(f => f.OwnerId == them.Id).Loadout = "torpedoes";
        Assert.Contains("its fleets arm from that house's isotopes", OrderSystem.Sway(g.W, them, g.Mine).why); Assert.Equal(1, g.Senate.Lean(g.W, mine, them.Id));
        g.Mine.Corners.Clear(); g.Mine.Corners.Add(Resource.RareEarths); Assert.Equal(0, g.Senate.Lean(g.W, mine, them.Id));   // rare earths go into a hull once
        g.Mine.Corners.Clear(); g.Mine.Corners.Add(Resource.Helium3); Assert.Contains("its fleets burn that house's fuel", OrderSystem.Sway(g.W, them, g.Mine).why);
        g.W.WorldFlags.Add(OrderSystem.CornerFlag); Choose(g, ReportOf(g, "the_corner"), "remember_who_asks");
        Session(g); Assert.Equal(1, mine.Votes[them.Id]);
    }

    // ------------------------------------------------------------------ the chancellor: the_chancellors_voice/ask_to_be_asked

    [Fact]
    public void TheChancellorsVoice_AskToBeAsked_TheNextDoubtfulVoteIsBroughtToYouBeforeItIsDeclared_AndTheRestAreDeclaredAsBefore()
    {
        var g = Start(); var others = Others(g);
        var k = HouseholdSystem.Eligible(g.W, g.Mine).FirstOrDefault(c => HouseholdSystem.AssignRefusal(g.W, g.Me, HouseholdSystem.Chancellor, c) == null);
        if (k == null) { k = new Character { Id = "kin_chancellor", Name = "Test Chancellor", HouseId = g.Mine.Id, Age = 30, Sex = "female", CareerTrack = "law" }; g.W.Characters[k.Id] = k; g.Mine.Members.Add(k.Id); g.W.Pools[k.Id] = ActionPools.ForNpc(); g.W.Relationship(k.Id, g.Me.Id).Disposition = 40; }
        var give = new DelegateCommand(g.Me.Id, HouseholdSystem.Chancellor, k.Id, 0); Assert.True(g.Do(give), give.Refusal(g.Ctx));
        // a year: the chancellor declares what you left alone, and the report comes
        var first = Moves(g, others[0]); g.W.Turn++; g.Tc.Household.Tick(g.W, g.Tc.Executor, g.Ctx);
        Assert.True(first.Votes.ContainsKey(g.Mine.Id)); Assert.True(g.W.HasFlag(HouseholdSystem.ChancellorVotedFlag));
        g.Senate.Tick(g.W); Only(g);
        Choose(g, ReportOf(g, "the_chancellors_voice"), "ask_to_be_asked"); Assert.True(g.W.HasFlag(HouseholdSystem.AskFirstFlag));
        // the session that hears the next year's business: one motion the house leans hard on, one it leans on by a single point
        var clear = g.Senate.Add(g.W, others[1].Id, "levy", ""); var doubtful = g.Senate.Add(g.W, others[2].Id, "relief", g.W.Galaxy!.Regions.OrderBy(r => r.Id, StringComparer.Ordinal).First().Id);
        Leans(g, clear, g.Mine, -3); Leans(g, doubtful, g.Mine, 1); g.W.News.Clear();
        g.Senate.Tick(g.W); Only(g, clear, doubtful);
        Assert.True(g.Told($"{k.Name}, your chancellor, brings the vote on this motion to you before declaring it, as you asked")); Assert.True(g.Told("the house leans a little for it"));
        Assert.False(g.W.HasFlag(HouseholdSystem.AskFirstFlag));   // the next doubtful vote: one
        g.W.Turn++; g.Tc.Household.Tick(g.W, g.Tc.Executor, g.Ctx);
        Assert.Equal(-1, clear.Votes[g.Mine.Id]); Assert.False(doubtful.Votes.ContainsKey(g.Mine.Id));   // declared as before; and the doubtful one left for you
        Assert.Contains("brought the doubtful one to you", g.Mine.Duty(HouseholdSystem.Chancellor)!.LastReport);
        IVote(g, doubtful, -1); g.Senate.Tick(g.W);
        Assert.True(doubtful.Resolved); Assert.Equal(-1, doubtful.Votes[g.Mine.Id]);
    }

    // ------------------------------------------------------------------ blood: blood_on_our_hands

    [Fact]
    public void BloodOnOurHands_TheHouseYouOweBloodToOwesYouNothingInTheSenate_ItsFavourIsNoVote_CannotBeCalled_AndGoesAtTheYearsEnd_AcrossASaveAndALoad()
    {
        var g0 = Start(); g0.Mine.Treasury.Credits = 5000; var theirs = Small(g0)[0]; Gives(g0, theirs);
        var victim = g0.W.LivingCharacters().First(c => c.HouseId == theirs.Id && c.Id != g0.HeadOf(theirs).Id && c.Age >= DynastySystem.AgeOfMajority);
        Assert.NotNull(FeudSystem.Blood(g0.W, victim, g0.Mine.Id, g0.Me.Id, "hand")); DynastySystem.Die(g0.W, victim.Id);
        Choose(g0, ReportOf(g0, "blood_on_our_hands"), "shrug");
        var g = Reload(g0); var mine = IMove(g); Leans(g, mine, theirs, -1);
        Assert.False(g.Senate.Owes(g.W, mine, theirs.Id)); Assert.Equal(-1, g.Senate.Stance(g.W, mine, theirs.Id));   // the whip count: no vote owed
        Assert.Contains(g.Senate.Why(g.W, mine, theirs.Id), x => x.why.StartsWith("there is blood between it and") && x.by == -FeudSystem.LeanBy);
        var call = new DealingsCommand(g.Me.Id, DealingsCommand.CallVote, theirs.Id, g.Tc.Dealings, mine.Id); Assert.Equal("there is blood between your houses: they owe you nothing in the Senate", call.Refusal(g.Ctx));
        Session(g); Assert.Equal(-1, mine.Votes[theirs.Id]);
        WealthSystem.Tick(g.W);
        Assert.False(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id)); Assert.True(g.Told($"There is blood between your house and {theirs.Name}: it owes you nothing in the Senate now"));
    }

    // ------------------------------------------------------------------ the whip count's favour: SenateSystem.Why, WealthSystem.FavourLean

    [Fact]
    public void TheWhipCount_AFavourOwedBetweenTwoOtherHouses_IsTheTwoInTheLeanAndTheCount_AndNothingAcrossBlood()
    {
        var g = Start(); var o = Others(g); var owes = o[0]; var mover = o.First(h => h != owes && g.Senate.MayPropose(g.W, h.Id));
        g.W.Relationship(g.HeadOf(owes).Id, g.HeadOf(mover).Id).Disposition = 50;   // so that the blood below leaves what its head thinks of the mover worth what it was
        WealthSystem.OweFor(g.W, owes.Id, mover.Id);
        var m = Moves(g, mover); Leans(g, m, owes, 1);   // for it by one: against it by one without the favour
        Assert.Contains(g.Senate.Why(g.W, m, owes.Id), x => x.why == $"it owes {mover.Name} a favour" && x.by == WealthSystem.FavourLeanBy);
        Assert.Equal(g.Senate.Lean(g.W, m, owes.Id), g.Senate.Why(g.W, m, owes.Id).Sum(x => x.by));
        Session(g); Assert.Equal(1, m.Votes[owes.Id]);
        // a second favour, and blood between them: the favour leans nothing
        Only(g); WealthSystem.OweFor(g.W, owes.Id, mover.Id); var n = Moves(g, mover); Leans(g, n, owes, 1);
        var victim = g.W.LivingCharacters().First(c => c.HouseId == owes.Id && c.Id != g.HeadOf(owes).Id && c.Age >= DynastySystem.AgeOfMajority);
        FeudSystem.Blood(g.W, victim, mover.Id, g.HeadOf(mover).Id, "hand"); DynastySystem.Die(g.W, victim.Id);
        Assert.DoesNotContain(g.Senate.Why(g.W, n, owes.Id), x => x.why.StartsWith("it owes"));
        Assert.Equal(1 - WealthSystem.FavourLeanBy - FeudSystem.LeanBy, g.Senate.Lean(g.W, n, owes.Id));
    }

    // ------------------------------------------------------------------ threats: DealingsSystem.ThreatSays, DealingsSystem.Threat

    [Fact]
    public void YourThreat_AHouseAfraidOfYouThatTookGoldToVoteAgainstYourWar_StaysAway_ItDoesNotVoteAgainstWhatYouMove()
    {
        var g = Start(); var them = g.W.House("ashgrove")!; Assert.Equal(0, FleetSystem.StrengthOf(g.W, them.Id));
        if (FleetSystem.StrengthOf(g.W, g.Mine.Id) < DealingsSystem.ThreatOdds) g.W.Fleets.Values.First(f => f.OwnerId == g.Mine.Id).Strength = 5;
        var threat = new DealingsCommand(g.Me.Id, DealingsCommand.Threat, them.Id, g.Tc.Dealings); Assert.True(g.Do(threat), threat.Refusal(g.Ctx));
        Assert.True(PolitySystem.Cowed(g.W, them, g.Mine)); Assert.Contains("it does not vote against what you move", g.Tc.Dealings.ThreatSays(g.W, g.Mine, them));
        var war = IMove(g, "foreign_war", "thessar"); Only(g, war);
        for (int i = 0; i < 4 && g.Senate.Lean(g.W, war, them.Id) != 2; i++) war.Lobby[them.Id] = war.Lobby.GetValueOrDefault(them.Id) + 2 - g.Senate.Lean(g.W, war, them.Id);
        Assert.Equal(2, g.Senate.Lean(g.W, war, them.Id));   // of itself it is for the war (a lean against what you move it would not show: it is afraid of you)
        var head = g.HeadOf(them); var o = new GoldOffer { Id = "gold_test", NationId = "thessar", WarOnId = "thessar", NationName = "the Free Cities of Thessar", WarOnName = "the Free Cities of Thessar", MotionId = war.Id, HouseId = them.Id, CharacterId = head.Id, Ask = ForeignGoldSystem.Against, Sum = 20, Turn = g.W.Turn, Before = 1, Seats = g.Senate.Weight(g.W, them.Id) };
        g.W.GoldOffers.Add(o); ForeignGoldSystem.Answer(g.W, o, ForeignGoldSystem.Take);   // it takes the gold to vote against your war
        var why = g.Senate.Why(g.W, war, them.Id);
        Assert.Contains(why, x => x.why.Contains("taken gold to vote against")); Assert.Contains(why, x => x.why.StartsWith("it is afraid of"));
        Assert.Equal(0, g.Senate.Lean(g.W, war, them.Id));
        Session(g); Assert.True(war.Resolved && !war.Vetoed); Assert.Equal(0, war.Votes[them.Id]);
    }

    [Fact]
    public void ThreatenedByAHouse_VotingAgainstWhatItMovesCostsTen_ForTheThreatsYears_EvenOnceYouHaveMoreThanHalfItsHulls()
    {
        var g = Start(); var them = g.W.House("aurelian")!; foreach (var f in g.W.Fleets.Values.Where(f => f.OwnerId == them.Id)) f.Strength = 40;
        g.Tc.Dealings.Threat(g.W, them, g.Mine); Assert.True(g.Told("Vote against what it moves and it will be remembered"));
        g.W.Fleets.Values.First(f => f.OwnerId == g.Mine.Id).Strength += FleetSystem.StrengthOf(g.W, them.Id);   // your fleets grow: you are not afraid of it now
        Assert.False(PolitySystem.Cowed(g.W, g.Mine, them));
        Assert.Contains(g.Tc.Dealings.Lasting(g.W, g.Mine, them), l => l.text == $"It threatened you in year {g.W.Turn}. Until year {g.W.Turn + DealingsSystem.CowedYears}, vote against what it moves and its head thinks -10 of you.");
        var m = Moves(g, them); IVote(g, m, -1); int regard = g.Regard(them);
        Session(g);
        Assert.Equal(regard - DealingsSystem.DefiedRegard, g.Regard(them)); Assert.True(g.Told($"You voted against the motion of {them.Name}, which had told you what its fleets could do"));
    }

    // ------------------------------------------------------------------ foreign gold shown to the chamber: ForeignGoldSystem.Answer

    [Fact]
    public void ForeignGoldShownToTheChamber_EveryHouseLeansOneMoreForTheWar_AndTheCountHasIt()
    {
        var g = Start(); var o = Others(g); var mover = o.First(h => g.Senate.MayPropose(g.W, h.Id)); var teller = o.First(h => h != mover); var waverer = o.First(h => h != mover && h != teller);
        var war = Moves(g, mover, "foreign_war", "thessar"); Leans(g, war, waverer, 0);
        var before = o.ToDictionary(h => h.Id, h => g.Senate.Lean(g.W, war, h.Id));
        var offer = new GoldOffer { Id = "gold_shown", NationId = "thessar", WarOnId = "thessar", NationName = "the Free Cities of Thessar", WarOnName = "the Free Cities of Thessar", MotionId = war.Id, HouseId = teller.Id, CharacterId = g.HeadOf(teller).Id, Ask = ForeignGoldSystem.Against, Sum = 20, Turn = g.W.Turn, Before = 1, Seats = g.Senate.Weight(g.W, teller.Id) };
        g.W.GoldOffers.Add(offer); ForeignGoldSystem.Answer(g.W, offer, ForeignGoldSystem.Expose);
        Assert.True(g.Told($"of {teller.Name} has told the Senate of the gold") && g.Told("The chamber leans the more for the war."));
        foreach (var h in o) Assert.Equal(before[h.Id] + ForeignGoldSystem.ExposeAnger, g.Senate.Lean(g.W, war, h.Id));
        Session(g); Assert.Equal(1, war.Votes[waverer.Id]);
    }

    // ------------------------------------------------------------------ the squares: the_squares_turn_on_the_magistrates/call_for_the_accounts_to_be_opened

    [Fact]
    public void TheSquaresTurn_CallingForTheAccounts_IsACallFromTheFloor_NotAMotionCarried_AndNoMotionIsCounted()
    {
        var g = Start(); g.W.WorldFlags.Add("trust_collapsed"); int motions = g.W.Motions.Count;
        Choose(g, ReportOf(g, "the_squares_turn_on_the_magistrates"), "call_for_the_accounts_to_be_opened");
        Assert.Equal(motions, g.W.Motions.Count);
        Assert.Contains(g.W.History, h => h.Text.Contains("called for the accounts of every sitting magistrate to be laid open"));
        Assert.DoesNotContain(g.W.History, h => h.Text.Contains("It carried"));
    }
}
