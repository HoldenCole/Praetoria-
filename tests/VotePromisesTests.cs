using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
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
/// the whip's purse, the gold from beyond the Reach, a bribe and a lobby, a purse that comes out at the count.</summary>
public class VotePromisesTests
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
        Assert.False(WealthSystem.FavourOwed(g.W, theirs.Id, g.Mine.Id)); Assert.True(g.Told($"You are at war with {theirs.Name}: the vote it owed your house will not be given"));
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
        var g = Start(); TheyMove(g, Others(g)[0]);
        var (f, whip) = Report(g, "the_whip", "whip"); Answer(g, f, "name_your_price"); var theirs = g.W.House(whip.HouseId)!;
        theirs.Treasury.Credits = Math.Max(100, theirs.Treasury.Credits); int had = g.Mine.Treasury.Credits;
        Assert.True(g.Do(new WealthCommand(g.Me.Id, WealthCommand.Favour, theirs.Id, WealthSystem.ForMoney)));   // named: money
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
        Assert.True(g.Told($"{theirs.Name} has bought your vote on its motion: it is declared for it")); Assert.True(g.Told("-15"));   // reminded before the count, this year
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
}
