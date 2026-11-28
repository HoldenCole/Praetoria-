using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The people in the Senate's and the elections' news, marked so that a report can name them (docs/events/votes.md, "The marks
/// added to Core"). The moments were already there (a motion on the docket, a count, a purse found out, an Act carried, a rung
/// won and lost); what was missing was a flag on the person, so no role could bind them. Each mark is a character flag that lives
/// a year, set where the moment happens, and cleared by the system that owns it: the Senate's at its next session, the ladder's
/// with <see cref="LadderSystem.HeldFlag"/>. A mark moves no vote of itself; a bargain struck in a report is made here into the pledge or the
/// favour the Senate already keeps (<see cref="Bargains"/>), which declares the player's vote where the player gave or sold it. RNG-free.
/// </summary>
public static partial class VoteMarks
{
    // the docket, rewritten every session (SenateSystem.WriteFlags)
    public const string MovesChar = "moves_a_motion", MovesAgainstChar = "moves_against_you", UndecidedChar = "undecided_on_yours", MyMotionFlag = "my_motion_open";
    /// <summary>On the head of a house that leans against the player's motion now before the Senate, as the count stands (declared, or by
    /// its lean): an_old_grudge_against_your_motion reads it, so that "{rival.name} does" lean against it is so when the report comes.
    /// Rewritten with <see cref="UndecidedChar"/> by <see cref="Leanings"/>, at the session and again as the year's reports are chosen.</summary>
    public const string AgainstYoursChar = "against_yours";
    // the count, cleared at the next session (SenateSystem.Tick)
    public const string VotedWithChar = "voted_with_you", VotedAgainstChar = "voted_against_you";
    public const string AbstainedFlag = "abstention_noticed", NoticedChar = "noticed_your_abstention";
    public const string PurseOutFlag = "bribe_out", PurseOutChar = "took_your_purse";
    public const string ActAgainstFlag = "act_went_against_houses", ActAgainstChar = "act_went_against_them";
    // the ladder's count, cleared with the ladder's own flag (LadderSystem.Tick)
    public const string BeatYoursChar = "beat_yours_at_the_count", LostToYoursChar = "lost_to_yours_at_the_count", OursLostChar = "lost_at_the_count";
    /// <summary>A relative's candidacy the head has answered (one_of_yours_stands_for_office): set by the report, struck by the ladder when
    /// the candidacy is counted or withdrawn (LadderSystem), so one candidacy is one report and not one a year until the count.</summary>
    public const string StandingHeardChar = "standing_heard";
    // a bargain struck in a report (a_vote_for_a_vote, a_bought_vote_called_in), made a favour owed as the choice is made (Bargains)
    public const string VoteOwedChar = "owes_you_a_vote", VotePromisedChar = "promised_your_vote";
    /// <summary>A vote a report has someone of another house give your house for later (their house's voice, a favour named later, a vote
    /// when the time comes): made a favour that house owes yours, which is a vote (WealthSystem.VotesFor).</summary>
    public const string HouseOwesChar = "their_house_owes_you_a_vote";
    /// <summary>On everyone of a house with which no new bargain is to be had: it owes the player's house a favour or is owed one (a house
    /// owes another one favour at a time, WealthSystem.OweFor, so a second would be nothing), or the player broke their word on a vote to
    /// it in the last <see cref="DealingsSystem.PledgeMemoryYears"/> years (it takes no word of theirs). The reports that strike a bargain
    /// read it. Rewritten every session, and set when a report makes a bargain.</summary>
    public const string BargainBarredChar = "no_bargain_with_you";
    /// <summary>A vote a report has the player sell, or give again on a sale, to someone of another house (a_vote_for_a_vote/name_a_price,
    /// the_whip/take_the_coin, a_bought_vote_called_in/vote_as_they_ask): made a vote bought on that house's motion, declared for it and
    /// judged at its count (<see cref="Sell"/>). The mark goes.</summary>
    public const string VoteSoldChar = "sold_your_vote";
    /// <summary>The buyer of the player's vote in a_vote_for_a_vote, read by a_bought_vote_called_in; struck by the count when the vote it
    /// bought was not given (DealingsSystem.MotionCounted), since a buyer cheated does not come back to call in another.</summary>
    public const string BoughtChar = "bought_your_vote";
    /// <summary>On everyone of a house with a motion before the Senate on which the player's vote is to be had: not a petition, not one
    /// that strikes at the player's house (WealthSystem.StrikesAt), and not one the player's vote is already pledged or sold on. The
    /// reports that buy or bargain for the player's vote on a house's motion read it (a_vote_for_a_vote, a_bought_vote_called_in,
    /// the_whip). Rewritten every session, with the docket.</summary>
    public const string VoteWantedChar = "wants_your_vote";
    /// <summary>The night spent calling in favours before a motion against the house (a_censure_on_the_docket/call_in_favours): set by the
    /// choice and read as it is made (<see cref="Bargains"/>). Every house that owes yours a favour and can be called on the motion
    /// against your house declares against it, and its favour is spent (DealingsSystem.CallInAgainst).</summary>
    public const string CallInFlag = "calling_in_favours";
    /// <summary>The report's gate for that choice: some house owes yours a favour that could be called on a motion now before the Senate
    /// that strikes at your house (DealingsSystem.CallableAgainst). Rewritten with the docket, and after every choice.</summary>
    public const string FavoursToCallFlag = "favours_to_call";
    /// <summary>A debt called in from someone of another house (the_favor_called/call_it_in): paid with their house's vote where you need
    /// one, on a motion now before the Senate, as the choice is made (DealingsSystem.DebtPaid). The mark goes.</summary>
    public const string DebtCalledChar = "debt_called_in";
    /// <summary>The leader of the player's bench, who saw the house's seat empty, told it will not happen again (the_empty_seat_on_the_bench/
    /// say_it_will_not_happen_again): made the player's word that the house will vote at the next vote of the bench (<see cref="StandKey"/>),
    /// judged at that count (<see cref="Counted"/>). The mark goes.</summary>
    public const string StandChar = "promised_to_stand";
    public const string StandPrefix = "stand_promised:";
    /// <summary>The player's word to the house that leads their bench, that their house will vote at the next vote the bench's leader votes
    /// in: a world counter, the turn it was given.</summary>
    public static string StandKey(string leaderHouseId) => StandPrefix + leaderHouseId;
    /// <summary>On the head of every house of the Reach with which a bargain may be struck: not the player's, not at war with it, not one
    /// that stands in a favour with it either way or has had the player's word broken to it lately (<see cref="BargainBarredChar"/>). A
    /// report that brings a house over to the player's side reads it (the_senate_listens/build_bloc). Rewritten with the docket.</summary>
    public const string BargainOpenChar = "may_bargain_with_you";
    /// <summary>A bench's leader has moved against the player's house (across_the_aisle): the least weighty house of that bench that would
    /// vote for the motion is marked on its head, and the report's gate is set; finding what it wants (split_the_bench, which sets
    /// <see cref="SplitFlag"/>) declares its vote against the motion as the choice is made (<see cref="Split"/>). Rewritten with the docket.</summary>
    public const string WeakestChar = "weakest_on_the_bench", SplittableFlag = "bench_can_be_split", SplitFlag = "splitting_the_bench";
    /// <summary>Another senator brought over to the player's side (the_senate_listens/build_bloc, spread_credit set <see cref="BringOverFlag"/>):
    /// the head marked <see cref="BargainOpenChar"/> who thinks best of the player, of a house of the Reach with no favour standing with
    /// the player's, is chosen as the choice is made, and their house owes the player's a favour, which is a vote. The report comes only
    /// while there is such a head (<see cref="BlocFlag"/>). Chosen by regard, then name: no draw of the shared RNG.</summary>
    public const string BringOverFlag = "bringing_a_senator_over", BlocFlag = "bloc_can_grow";
    /// <summary>A client of the player's house helped with its suit (a_client_asks/help_him sets <see cref="HelpFlag"/>): the first client by
    /// name with no favour standing with the player's house owes it a favour, which is a vote, as the choice is made. The choice is open
    /// only while there is such a client (<see cref="ClientFlag"/>).</summary>
    public const string HelpFlag = "helping_a_client", ClientFlag = "client_to_help";

    private static readonly string[] SessionChars = { VotedWithChar, VotedAgainstChar, NoticedChar, PurseOutChar, ActAgainstChar, VotedYouUpChar, VotedYouDownChar, ForYoursChar, AgainstYoursHouseChar };
    private static readonly string[] SessionFlags = { AbstainedFlag, PurseOutFlag, ActAgainstFlag };
    private static readonly string[] DocketChars = { MovesChar, MovesAgainstChar, UndecidedChar, AgainstYoursChar, VoteWantedChar, BargainOpenChar, WeakestChar };

    private static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }
    private static Character? Head(World w, string houseId) => w.House(houseId) is { } h ? Economy.HeadOf(w, h) : null;

    /// <summary>Last session's news, cleared before this session's is written.</summary>
    public static void ClearSession(World w)
    {
        foreach (var f in SessionFlags) w.WorldFlags.Remove(f);
        foreach (var c in w.Characters.Values) foreach (var f in SessionChars) c.Flags.Remove(f);
    }

    /// <summary>A bargain made in a report becomes what the Senate already keeps, as the choice is made (EventEngine.Resolve; and at the
    /// session, before the count, for a mark left from before). A head marked <see cref="VoteOwedChar"/> gave their word for yours: their
    /// house owes yours a vote (a favour, which votes for your motions: WealthSystem.VotesFor), and your word on their motion now before
    /// the Senate is a pledge (DealingsSystem), your vote declared for it and judged at its count. Someone marked <see cref="HouseOwesChar"/>
    /// gave your house a vote for later: their house owes yours a favour. A head marked <see cref="VotePromisedChar"/> was told you would
    /// vote for their motion: yours owes theirs. Someone marked <see cref="VoteSoldChar"/> bought your vote on their house's motion: it is
    /// declared for it and judged at its count (<see cref="Sell"/>). The house keeps its word if the one who gave it has died since. The
    /// marks go.</summary>
    public static void Bargains(World w, string? mine)
    {
        // favours called in on the motion against the house, before any bargain below makes a new one
        if (w.WorldFlags.Remove(CallInFlag) && mine != null) DealingsSystem.CallInAgainst(w, mine);
        if (w.WorldFlags.Remove(SplitFlag) && mine != null) Split(w, mine);
        if (w.WorldFlags.Remove(BringOverFlag) && mine != null) BringOver(w, mine);
        if (w.WorldFlags.Remove(HelpFlag) && mine != null) HelpClient(w, mine);
        FactionSystem.Apologised(w);   // the apology to the bench's leader for a vote against the bench (the_whips_displeasure/apologise)
        Answers(w, mine);   // the feast, the round, the names written down, the repeal promised, the loans (VoteMarks.Answers.cs)
        foreach (var c in w.Characters.Values.Where(c => c.Flags.Contains(VoteOwedChar) || c.Flags.Contains(VotePromisedChar) || c.Flags.Contains(HouseOwesChar) || c.Flags.Contains(VoteSoldChar) || c.Flags.Contains(DebtCalledChar) || c.Flags.Contains(StandChar)).OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
        {
            bool both = c.Flags.Remove(VoteOwedChar), promised = c.Flags.Remove(VotePromisedChar), owes = c.Flags.Remove(HouseOwesChar), sold = c.Flags.Remove(VoteSoldChar), debt = c.Flags.Remove(DebtCalledChar), stand = c.Flags.Remove(StandChar);
            if (mine == null || c.HouseId == mine || w.House(c.HouseId) is not { } theirs || World.Outsider(c.HouseId)) continue;
            if (stand) Stand(w, mine, theirs);
            if (debt) DealingsSystem.DebtPaid(w, mine, c, theirs);
            if (!both && !sold && !promised && !owes) continue;
            // a vote owed by a house at war with yours, or across blood, is void as it is given (WealthSystem.Apart): no bargain is struck
            if ((both || owes) && WealthSystem.Apart(w, theirs.Id, mine))
            {
                w.Tell("info", $"{(w.AtWar(theirs.Id, mine) ? "You are at war with" : "There is blood between your house and")} {theirs.Name}: no bargain is struck across it, and neither house owes the other a vote for it.", "houses", theirs.Id);
                both = owes = false; if (!sold && !promised) continue;
            }
            if (both && !Word(w, mine, theirs)) both = false;   // your vote on their motion is given elsewhere already: no second word, and nothing owed for it
            if (sold) Sell(w, mine, theirs);
            if (promised) WealthSystem.OweFor(w, mine, theirs.Id);
            if (both || owes) WealthSystem.OweFor(w, theirs.Id, mine);
            foreach (var x in w.LivingCharacters().Where(x => x.HouseId == theirs.Id)) { x.Flags.Add(BargainBarredChar); x.Flags.Remove(BargainOpenChar); }
        }
        Callable(w, mine); Gates(w, mine); Wanted(w, mine);
    }

    /// <summary>The houses that stand in a favour with the player's house, either way, or have had the player's word broken to them lately,
    /// or are at war with it or have blood between them and it: no new bargain is struck with them (a house owes another one favour at a
    /// time, and across a war or blood none at all, WealthSystem.Apart: a vote promised by an enemy would be void as it was given).</summary>
    private static HashSet<string> Standing(World w, string mine) =>
        w.Houses.Values.Where(h => h.Id != mine && (WealthSystem.FavourOwed(w, h.Id, mine) || WealthSystem.FavourOwed(w, mine, h.Id) || WealthSystem.Apart(w, h.Id, mine)
            || DealingsSystem.Remembered(w, DealingsSystem.PledgeBrokenKind, mine, h.Id).Any(d => w.Turn - d.Turn < DealingsSystem.PledgeMemoryYears))).Select(h => h.Id).ToHashSet();
    /// <summary>The head to bring over: marked open to a bargain, thinking no worse than nothing of the player, the one who thinks best of them.</summary>
    private static Character? Senator(World w)
    {
        var me = w.Protagonist; if (me == null) return null;
        return w.LivingCharacters().Where(c => c.Flags.Contains(BargainOpenChar) && c.HouseId != me.HouseId).Select(c => (c, by: w.GetRelationship(c.Id, me.Id)?.Disposition ?? 0)).Where(x => x.by >= 0)
            .OrderByDescending(x => x.by).ThenBy(x => x.c.Id, StringComparer.Ordinal).Select(x => x.c).FirstOrDefault();
    }
    /// <summary>The client to help: the first by name with no favour standing with the player's house.</summary>
    private static House? ClientToHelp(World w, string mine)
    {
        var standing = Standing(w, mine);
        return OrderSystem.ClientsOf(w, mine).FirstOrDefault(h => !standing.Contains(h.Id) && !World.Outsider(h.Id) && Head(w, h.Id) != null && !w.AtWar(h.Id, mine));
    }
    /// <summary>The gates of the reports that bring a house over or help a client, as things stand now.</summary>
    private static void Gates(World w, string? mine)
    {
        if (mine != null && Senator(w) != null) w.WorldFlags.Add(BlocFlag); else w.WorldFlags.Remove(BlocFlag);
        if (mine != null && ClientToHelp(w, mine) != null) w.WorldFlags.Add(ClientFlag); else w.WorldFlags.Remove(ClientFlag);
    }
    private static void BringOver(World w, string mine)
    {
        if (Senator(w) is not { } senator || w.House(senator.HouseId) is not { } theirs) { w.Tell("info", "Nobody would come over to your side this year: every house of the Reach stands in a favour with yours, is at war with it, or thinks ill of you.", "senate:chamber"); return; }
        WealthSystem.OweFor(w, theirs.Id, mine);
        foreach (var x in w.LivingCharacters().Where(x => x.HouseId == theirs.Id)) { x.Flags.Add(BargainBarredChar); x.Flags.Remove(BargainOpenChar); }
        w.Tell("good", $"{senator.Name} of {theirs.Name} has come over to your side: {theirs.Name} owes your house a vote. It votes for your next motion whichever way it leans, unless that motion strikes at it.", "senate:chamber", theirs.Id);
        w.Log($"{senator.Name} of {theirs.Name} comes over to the side of {w.House(mine)?.Name}.");
    }
    private static void HelpClient(World w, string mine)
    {
        if (ClientToHelp(w, mine) is not { } client) return;
        WealthSystem.OweFor(w, client.Id, mine);
        foreach (var x in w.LivingCharacters().Where(x => x.HouseId == client.Id)) { x.Flags.Add(BargainBarredChar); x.Flags.Remove(BargainOpenChar); }
        w.Tell("good", $"{Head(w, client.Id)?.Name} of {client.Name}, your client, owes the house a vote for the advocate you paid: {client.Name} votes for your next motion whichever way it leans, unless that motion strikes at it.", "houses", client.Id);
    }

    // ---------------- the player's word to the bench: on its feet at the next vote (docs/events/votes.md, "Promises", the bench group) ----------------

    /// <summary>The player's word to the house that leads their bench: their house will vote at the next vote of the bench, and the player
    /// is told what breaking it costs. Judged at that count (<see cref="Counted"/>); it lapses, with a notice, if the house no longer
    /// leads the player's bench (<see cref="StandLapses"/>). The word is to the house: a new head of it holds the player to it.</summary>
    private static void Stand(World w, string mine, House leader)
    {
        w.WorldCounters[StandKey(leader.Id)] = Math.Max(1, w.Turn);
        string bench = FactionSystem.Of(w, leader.Id)?.Name ?? "your bench";
        w.Tell("warn", $"You promised {leader.Name} that your house will vote at the next vote of {bench} that {leader.Name} votes in, for or against. Vote, and the word is kept; "
            + $"stay in your seat, and {Head(w, leader.Id)?.Name ?? "its head"} thinks {FactionSystem.BreakWhipDisposition} the worse of you and your tie to the bench is weaker by {FactionSystem.BreakWhipAffinity}, as for a vote against it.", "senate:chamber", leader.Id);
    }
    /// <summary>The head of the house the player has promised to vote with at the next vote of the bench, if this motion would be that
    /// vote (not the player's own motion, nor one that names the player's house), or null: for the reminder when it is moved and when
    /// the player would stay in their seat on it.</summary>
    public static Character? StandsOn(World w, Motion m)
    {
        string? mine = w.Protagonist?.HouseId; if (mine == null || m.ProposerHouseId == mine || m.TargetId == mine || FactionSystem.Of(w, mine) is not { } bench || bench.LeaderHouseId == mine) return null;
        return w.Counter(StandKey(bench.LeaderHouseId)) > 0 ? Head(w, bench.LeaderHouseId) : null;
    }
    /// <summary>What the reminder says of the word to the bench, on a motion it covers.</summary>
    public static string StandSays(World w, Character leader) =>
        $"You promised {w.House(leader.HouseId)?.Name ?? "the leader of your bench"} that your house would vote at the next vote of the bench: stay in your seat on this one, if {leader.Name} votes in it, and they think {FactionSystem.BreakWhipDisposition} the worse of you and your tie to the bench is weaker by {FactionSystem.BreakWhipAffinity}.";
    /// <summary>A word to the bench's leader that can no longer be kept as given: that house is gone, or leads the player's bench no longer.</summary>
    public static void StandLapses(World w, string? mine)
    {
        foreach (var key in w.WorldCounters.Keys.Where(k => k.StartsWith(StandPrefix, StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal).ToList())
        {
            string leaderId = key[StandPrefix.Length..]; var leader = w.House(leaderId); var bench = mine != null ? FactionSystem.Of(w, mine) : null;
            if (mine != null && leader != null && bench?.LeaderHouseId == leaderId) continue;
            w.WorldCounters.Remove(key); if (mine == null) continue;
            w.Tell("info", leader == null ? "The house you promised to vote with at the next vote of your bench is no more, and your word goes with it."
                : $"Your word to {leader.Name}, that your house would vote at the next vote of its bench, lapses: {(bench == null ? "your house sits on no bench now" : $"{leader.Name} no longer leads {bench.Name}")}.", "senate:offices", leader?.Id ?? "");
        }
    }
    /// <summary>The count of the first vote the bench's leader voted in after the player's word: on its feet, the word is kept; in its
    /// seat, broken, at the cost of a vote against the bench (the leader's regard and the tie to the bench), and said. In place of the
    /// abstention noticed: the leader does not come to ask a second time.</summary>
    private static void Stood(World w, Motion m, string mine, Faction bench, Character leader)
    {
        var house = w.House(mine); var leaderHouse = w.House(bench.LeaderHouseId); string you = house?.Name ?? "Your house", them = leaderHouse?.Name ?? "the leader of your bench";
        if (m.Votes.GetValueOrDefault(mine) != 0)
        {
            w.Tell("good", $"You kept your word to {them}: your house was on its feet at the vote.", "senate:offices", bench.LeaderHouseId);
            w.Log($"{you} was on its feet at the vote, as it had promised {them}.");
            return;
        }
        FactionSystem.AddAffinity(w, mine, bench.LeaderHouseId, -FactionSystem.BreakWhipAffinity);
        if (w.Protagonist is { } me) { var r = w.Relationship(leader.Id, me.Id); r.Disposition = Math.Clamp(r.Disposition - FactionSystem.BreakWhipDisposition, -100, 100); }
        w.Tell("warn", $"You promised {them} that your house would vote at the next vote of {bench.Name}, and it stayed in its seat: {leader.Name} thinks {FactionSystem.BreakWhipDisposition} the worse of you, and your tie to the bench is weaker by {FactionSystem.BreakWhipAffinity}.", "senate:offices", bench.LeaderHouseId);
        w.Log($"{you} stayed in its seat at the vote, though it had promised {them} it would not.");
    }

    // ---------------- the bench's weakest member (across_the_aisle/split_the_bench) ----------------

    /// <summary>The house marked as the weakest of the bench whose leader moved against the player's house: given what it wanted, it
    /// declares its vote against that motion, whatever its bench does. The mark and the gate go.</summary>
    private static void Split(World w, string mine)
    {
        var head = w.LivingCharacters().Where(c => c.Flags.Contains(WeakestChar)).OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
        foreach (var c in w.Characters.Values) c.Flags.Remove(WeakestChar); w.WorldFlags.Remove(SplittableFlag);
        if (head == null || w.House(head.HouseId) is not { } weakest || FactionSystem.Of(w, weakest.Id) is not { } bench) return;
        var m = SenateSystem.Docket(w).FirstOrDefault(x => x.ProposerHouseId == bench.LeaderHouseId && WealthSystem.StrikesAt(x, mine)); if (m == null) return;
        m.Votes[weakest.Id] = -1;
        w.Tell("good", $"{weakest.Name}, the least of {bench.Name}'s houses, has been found what it wanted: it votes against the motion against your house, whatever its bench does.", "senate:chamber", weakest.Id);
    }

    /// <summary>The gate of the night spent calling in favours (<see cref="FavoursToCallFlag"/>), as things stand now.</summary>
    private static void Callable(World w, string? mine)
    {
        if (mine != null && DealingsSystem.CallableAgainst(w, mine).Count > 0) w.WorldFlags.Add(FavoursToCallFlag); else w.WorldFlags.Remove(FavoursToCallFlag);
    }

    /// <summary>The player's word on the motion of the house that gave its own: a pledge for it, the vote declared, and the player told
    /// what it binds them to. Nothing, if that house's motion is no longer before the Senate. False, with the player told why, if the
    /// player's vote on it is given elsewhere already (<see cref="Given"/>): a second word on one vote would break the first.</summary>
    private static bool Word(World w, string mine, House theirs)
    {
        var m = SenateSystem.Docket(w).FirstOrDefault(x => x.ProposerHouseId == theirs.Id); if (m == null) return true;
        if (DealingsSystem.PledgeOn(w, mine, m.Id) is { } given && given.ToHouseId == theirs.Id) return true;
        if (Elsewhere(w, mine, theirs, m)) return false;
        DealingsSystem.GiveWord(w, mine, theirs.Id, m, 1, bargain: true);
        w.Tell("warn", $"You gave {theirs.Name} your word: your vote on its motion is declared for it. Keep it at the count and its head thinks {DealingsSystem.PledgeKeptRegard:+0;-0} of you; "
            + $"change it and its head thinks {-DealingsSystem.PledgeBrokenRegard:+0;-0} of you, it is written down, and {theirs.Name} takes no word of yours on a vote for {Praetoria.Core.Counted.Years(DealingsSystem.PledgeMemoryYears)}. In return {theirs.Name} owes your house a vote, "
            + "and if you break your word it owes you nothing.", "senate:chamber", theirs.Id);
        return true;
    }
    /// <summary>The player's vote on that house's motion is given elsewhere already: said, and true.</summary>
    private static bool Elsewhere(World w, string mine, House theirs, Motion m)
    {
        if (Given(w, mine, m) is not { } given) return false;
        w.Tell("warn", $"Your vote on the motion of {theirs.Name} is {given} already: you cannot give it to {theirs.Name} as well without breaking that, so it is not given, and nothing is owed for it either way.", "senate:chamber", theirs.Id);
        return true;
    }

    /// <summary>The player's vote sold to a house, on its motion now before the Senate (not one that strikes at the player's house): a
    /// pledge bought and paid for (<see cref="VotePledge.Paid"/>), the vote declared for it, and the player told what it binds them to. At
    /// the count it is judged as a word given is (DealingsSystem.MotionCounted): kept, its head thinks the better of them and the bargain
    /// is done, with no favour owed, since the coin was the price; broken, its head thinks the worse, it is written down, and the house
    /// takes no word of theirs on a vote for some years. Nothing, if that house has no such motion before the Senate, or has their vote
    /// on it already.</summary>
    private static void Sell(World w, string mine, House theirs)
    {
        var m = SenateSystem.Docket(w).FirstOrDefault(x => x.ProposerHouseId == theirs.Id && !WealthSystem.StrikesAt(x, mine)); if (m == null) return;
        if (DealingsSystem.PledgeOn(w, mine, m.Id) is { } given && given.ToHouseId == theirs.Id) return;
        if (Elsewhere(w, mine, theirs, m)) return;
        DealingsSystem.GiveWord(w, mine, theirs.Id, m, 1, paid: true);
        w.Tell("warn", $"{theirs.Name} has bought your vote on its motion: it is declared for it. Vote so at the count and its head thinks {DealingsSystem.PledgeKeptRegard:+0;-0} of you; "
            + $"change it and its head thinks {-DealingsSystem.PledgeBrokenRegard:+0;-0} of you, it is written down, and {theirs.Name} takes no word of yours on a vote for {Praetoria.Core.Counted.Years(DealingsSystem.PledgeMemoryYears)}.", "senate:chamber", theirs.Id);
    }

    /// <summary>Who has a motion on the docket, who moves against the player's house, and on the player's own motion who has not
    /// made up their mind. Written with the docket's other flags, every session.</summary>
    public static void Docket(World w, SenateSystem senate, string? mine)
    {
        w.WorldFlags.Remove(MyMotionFlag);
        foreach (var c in w.Characters.Values) foreach (var f in DocketChars) c.Flags.Remove(f);
        Callable(w, mine);
        if (mine == null) return;
        // who stands in a favour with the player's house, either way, or has had the player's word broken to it lately
        var standing = Standing(w, mine);
        foreach (var c in w.Characters.Values) { if (c.Alive && standing.Contains(c.HouseId)) c.Flags.Add(BargainBarredChar); else c.Flags.Remove(BargainBarredChar); }
        foreach (var h in w.Houses.Values.Where(h => h.Id != mine && !World.Outsider(h.Id) && PolitySystem.InReach(w, h.Id) && !standing.Contains(h.Id) && !w.AtWar(h.Id, mine)))
            if (Head(w, h.Id) is { Alive: true } open) open.Flags.Add(BargainOpenChar);
        w.WorldFlags.Remove(SplittableFlag);
        Gates(w, mine);
        if (SenateSystem.Docket(w).FirstOrDefault(m => senate.BenchAgainst(w, m, mine)) is { } against && FactionSystem.Of(w, against.ProposerHouseId) is { } bench && bench.LeaderHouseId == against.ProposerHouseId
            && bench.Members.Where(id => id != bench.LeaderHouseId && id != mine && w.House(id) != null && !WealthSystem.Apart(w, id, mine) && !against.Votes.ContainsKey(id) && senate.Stance(w, against, id) > 0)
                .OrderBy(id => senate.Weight(w, id)).ThenBy(id => senate.Lean(w, against, id)).ThenBy(id => id, StringComparer.Ordinal).FirstOrDefault() is { } least && Head(w, least) is { Alive: true } weakestHead)
        { weakestHead.Flags.Add(WeakestChar); w.WorldFlags.Add(SplittableFlag); }
        foreach (var m in SenateSystem.Docket(w).ToList())
        {
            if (m.ProposerHouseId == mine) { w.WorldFlags.Add(MyMotionFlag); continue; }
            if (SenateSystem.IsPetition(m) || Head(w, m.ProposerHouseId) is not { } mover) continue;
            mover.Flags.Add(MovesChar);
            if (m.TargetId == mine && senate.Motions.Def(m.Kind) is { Target: "house" } d && (d.LegitimacyTarget < 0 || d.EmbargoTurns > 0)) mover.Flags.Add(MovesAgainstChar);
        }
        Wanted(w, mine);   // its house wants the player's vote on it, if it is to be had (a report that buys it, or bargains for it, reads this)
        Leanings(w, senate, mine);
    }

    /// <summary>Everyone of a house with a motion before the Senate on which the player's vote is to be had (<see cref="VoteWantedChar"/>):
    /// not a petition, not one that strikes at the player's house, not one the player's vote is given on already (<see cref="Given"/>).
    /// Written with the docket, and again whenever the player's vote is given (a word, a sale: DealingsSystem.GiveWord; gold taken:
    /// ForeignGoldSystem.Answer; a report's bargain), so that a report that would have it a second time is closed at once.</summary>
    public static void Wanted(World w, string? mine)
    {
        foreach (var c in w.Characters.Values) c.Flags.Remove(VoteWantedChar);
        if (mine == null) return;
        foreach (var m in SenateSystem.Docket(w).Where(m => m.ProposerHouseId != mine && !SenateSystem.IsPetition(m) && !WealthSystem.StrikesAt(m, mine) && Given(w, mine, m) == null).ToList())
            foreach (var c in w.LivingCharacters().Where(c => c.HouseId == m.ProposerHouseId)) c.Flags.Add(VoteWantedChar);
    }
    /// <summary>What the player's vote on this motion is given to already, in words, or null: a word or a sale to a house, or gold taken
    /// from beyond the Reach to vote against it or stay away.</summary>
    public static string? Given(World w, string mine, Motion m)
    {
        if (DealingsSystem.PledgeOn(w, mine, m.Id) is { } p) return $"{(p.Paid ? "bought by" : "pledged to")} {w.House(p.ToHouseId)?.Name ?? "another house"}, {(p.Side > 0 ? "for" : "against")} it";
        if (w.GoldOffers.FirstOrDefault(o => o.MotionId == m.Id && o.HouseId == mine && o.Answer == ForeignGoldSystem.Taken && o.Ask != ForeignGoldSystem.Withdraw) is { } gold)
            return $"held by the gold you took from {gold.NationName} {ForeignGoldSystem.AskSays(gold)}";
        return null;
    }

    /// <summary>On the player's motion now before the Senate, the heads of the houses that have not made up their mind (no vote declared,
    /// no vote owed, a lean of nothing: <see cref="UndecidedChar"/>) and of those that are against it as the count stands
    /// (<see cref="AgainstYoursChar"/>). Written with the docket, and again as the year's reports are chosen (TurnController.BeginTurn),
    /// since a year's turn moves leans: the_vote_still_undecided says "they lean neither for it nor against", and
    /// an_old_grudge_against_your_motion that the rival does lean against it, and both are so when the report comes.</summary>
    public static void Leanings(World w, SenateSystem senate, string? mine)
    {
        foreach (var c in w.Characters.Values) { c.Flags.Remove(UndecidedChar); c.Flags.Remove(AgainstYoursChar); }
        if (mine == null || SenateSystem.Docket(w).FirstOrDefault(m => m.ProposerHouseId == mine) is not { } m) return;
        foreach (var h in w.Houses.Values.Where(h => h.Id != mine && !World.Outsider(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            if (Head(w, h.Id) is not { Alive: true } head) continue;
            if (!m.Votes.ContainsKey(h.Id) && !senate.Owes(w, m, h.Id) && senate.Lean(w, m, h.Id) == 0) head.Flags.Add(UndecidedChar);   // a house that owes yours a vote has made up its mind
            else if (senate.Stance(w, m, h.Id) < 0) head.Flags.Add(AgainstYoursChar);
        }
    }

    /// <summary>At the count. On the player's own motion: the heads of the houses that voted for it and against it. On a motion the
    /// leader of the player's bench voted on, and the player's house did not: the leader saw it; or, where the player had given that
    /// house their word to vote at this vote, the word is judged (<see cref="Stood"/>).</summary>
    public static void Counted(World w, Motion m, string? mine, Data.MotionDef? def = null)
    {
        if (mine == null) return;
        // a motion for the player's house whose roll a report reads (a triumph, a return from exile, the powers for life): who voted which way
        if (RollRead(def, m, mine))
            foreach (var (houseId, vote) in m.Votes.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                if (houseId != mine && vote != 0 && Head(w, houseId) is { Alive: true } head) head.Flags.Add(vote > 0 ? ForYoursChar : AgainstYoursHouseChar);
        if (m.ProposerHouseId == mine)
        {
            foreach (var (houseId, vote) in m.Votes.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                if (houseId != mine && vote != 0 && Head(w, houseId) is { } head) head.Flags.Add(vote > 0 ? VotedWithChar : VotedAgainstChar);
            RoundSpent(w);   // a round stood after the last motion thrown out was for this count
        }
        if (m.ProposerHouseId != mine && m.TargetId != mine && FactionSystem.Of(w, mine) is { } bench
            && bench.LeaderHouseId != mine && m.Votes.GetValueOrDefault(bench.LeaderHouseId) != 0 && Head(w, bench.LeaderHouseId) is { } leader)
        {
            if (w.WorldCounters.Remove(StandKey(bench.LeaderHouseId))) Stood(w, m, mine, bench, leader);   // the word given that it will not happen again
            else if (m.Votes.GetValueOrDefault(mine) == 0) { leader.Flags.Add(NoticedChar); Mark(w, AbstainedFlag); }
        }
    }

    /// <summary>A vote the player's house paid for came out on the floor: the head of the house that took the purse.</summary>
    public static void PurseOut(World w, string boughtHouseId)
    {
        Mark(w, PurseOutFlag);
        if (Head(w, boughtHouseId) is { } head) head.Flags.Add(PurseOutChar);
    }

    /// <summary>An Act carried: the heads of the houses that voted against it, the player's own aside.</summary>
    public static void ActCarried(World w, Motion m, string? mine)
    {
        bool any = false;
        foreach (var (houseId, vote) in m.Votes.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            if (houseId != mine && vote < 0 && Head(w, houseId) is { } head) { head.Flags.Add(ActAgainstChar); any = true; }
        if (any) Mark(w, ActAgainstFlag);
    }

    /// <summary>A rung of the ladder counted: if one of the player's house stood for it and lost, those who won it; if one of the
    /// player's house won it, those of other houses who lost it. One pair of victor and loser is live at a time: where the house lost on
    /// two rungs in one count, the second rung's pair replaces the first's, so a report never pairs one rung's victor with the other's loser.</summary>
    public static void LadderCounted(World w, IReadOnlyList<Character> candidates, IReadOnlyList<Character> winners, string? mine)
    {
        if (mine == null) return;
        bool oursLost = candidates.Any(c => c.HouseId == mine && !winners.Contains(c)), oursWon = winners.Any(c => c.HouseId == mine);
        if (oursLost && winners.Any(c => c.HouseId != mine)) foreach (var c in w.Characters.Values) { c.Flags.Remove(BeatYoursChar); c.Flags.Remove(OursLostChar); }
        if (oursLost) foreach (var c in winners.Where(c => c.HouseId != mine)) c.Flags.Add(BeatYoursChar);
        if (oursLost && winners.Any(c => c.HouseId != mine)) foreach (var c in candidates.Where(c => c.HouseId == mine && !winners.Contains(c))) c.Flags.Add(OursLostChar);
        if (oursWon) foreach (var c in candidates.Where(c => c.HouseId != mine && !winners.Contains(c))) c.Flags.Add(LostToYoursChar);
    }

    /// <summary>The ladder's marks go when the count is no longer news.</summary>
    public static void ClearLadder(World w)
    {
        if (w.HasFlag(LadderSystem.HeldFlag)) return;
        foreach (var c in w.Characters.Values) { c.Flags.Remove(BeatYoursChar); c.Flags.Remove(LostToYoursChar); c.Flags.Remove(OursLostChar); }
    }
}
