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
public static class VoteMarks
{
    // the docket, rewritten every session (SenateSystem.WriteFlags)
    public const string MovesChar = "moves_a_motion", MovesAgainstChar = "moves_against_you", UndecidedChar = "undecided_on_yours", MyMotionFlag = "my_motion_open";
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

    private static readonly string[] SessionChars = { VotedWithChar, VotedAgainstChar, NoticedChar, PurseOutChar, ActAgainstChar };
    private static readonly string[] SessionFlags = { AbstainedFlag, PurseOutFlag, ActAgainstFlag };
    private static readonly string[] DocketChars = { MovesChar, MovesAgainstChar, UndecidedChar, VoteWantedChar };

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
        foreach (var c in w.Characters.Values.Where(c => c.Flags.Contains(VoteOwedChar) || c.Flags.Contains(VotePromisedChar) || c.Flags.Contains(HouseOwesChar) || c.Flags.Contains(VoteSoldChar)).OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
        {
            bool both = c.Flags.Remove(VoteOwedChar), promised = c.Flags.Remove(VotePromisedChar), owes = c.Flags.Remove(HouseOwesChar), sold = c.Flags.Remove(VoteSoldChar);
            if (mine == null || c.HouseId == mine || w.House(c.HouseId) is not { } theirs || World.Outsider(c.HouseId)) continue;
            if (both) Word(w, mine, theirs);
            if (sold) Sell(w, mine, theirs);
            if (promised) WealthSystem.OweFor(w, mine, theirs.Id);
            if (both || owes) WealthSystem.OweFor(w, theirs.Id, mine);
            foreach (var x in w.LivingCharacters().Where(x => x.HouseId == theirs.Id)) x.Flags.Add(BargainBarredChar);
        }
    }

    /// <summary>The player's word on the motion of the house that gave its own: a pledge for it, the vote declared, and the player told
    /// what it binds them to. Nothing, if that house's motion is no longer before the Senate.</summary>
    private static void Word(World w, string mine, House theirs)
    {
        var m = SenateSystem.Docket(w).FirstOrDefault(x => x.ProposerHouseId == theirs.Id); if (m == null) return;
        if (DealingsSystem.PledgeOn(w, mine, m.Id) is { } given && given.ToHouseId == theirs.Id) return;
        DealingsSystem.GiveWord(w, mine, theirs.Id, m, 1);
        w.Tell("warn", $"You gave {theirs.Name} your word: your vote on its motion is declared for it. Keep it at the count and its head thinks {DealingsSystem.PledgeKeptRegard:+0;-0} of you; "
            + $"change it and its head thinks {-DealingsSystem.PledgeBrokenRegard:+0;-0} of you, it is written down, and {theirs.Name} takes no word of yours on a vote for {Praetoria.Core.Counted.Years(DealingsSystem.PledgeMemoryYears)}. In return {theirs.Name} owes your house a vote.", "senate:chamber", theirs.Id);
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
        if (mine == null) return;
        // who stands in a favour with the player's house, either way, or has had the player's word broken to it lately
        var standing = w.Houses.Values.Where(h => h.Id != mine && (WealthSystem.FavourOwed(w, h.Id, mine) || WealthSystem.FavourOwed(w, mine, h.Id)
            || DealingsSystem.Remembered(w, DealingsSystem.PledgeBrokenKind, mine, h.Id).Any(d => w.Turn - d.Turn < DealingsSystem.PledgeMemoryYears))).Select(h => h.Id).ToHashSet();
        foreach (var c in w.Characters.Values) { if (c.Alive && standing.Contains(c.HouseId)) c.Flags.Add(BargainBarredChar); else c.Flags.Remove(BargainBarredChar); }
        foreach (var m in SenateSystem.Docket(w).ToList())
        {
            if (m.ProposerHouseId == mine)
            {
                w.WorldFlags.Add(MyMotionFlag);
                foreach (var h in w.Houses.Values.Where(h => h.Id != mine && !World.Outsider(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
                    if (!m.Votes.ContainsKey(h.Id) && !senate.Owes(w, m, h.Id) && senate.Lean(w, m, h.Id) == 0 && Head(w, h.Id) is { } undecided) undecided.Flags.Add(UndecidedChar);   // a house that owes yours a vote has made up its mind
                continue;
            }
            if (SenateSystem.IsPetition(m) || Head(w, m.ProposerHouseId) is not { } mover) continue;
            mover.Flags.Add(MovesChar);
            // its house wants the player's vote on it, if it is to be had (a report that buys it, or bargains for it, reads this)
            if (!WealthSystem.StrikesAt(m, mine, senate.Motions.Def(m.Kind)) && DealingsSystem.PledgeOn(w, mine, m.Id) == null)
                foreach (var c in w.LivingCharacters().Where(c => c.HouseId == m.ProposerHouseId)) c.Flags.Add(VoteWantedChar);
            if (m.TargetId == mine && senate.Motions.Def(m.Kind) is { Target: "house" } d && (d.LegitimacyTarget < 0 || d.EmbargoTurns > 0)) mover.Flags.Add(MovesAgainstChar);
        }
    }

    /// <summary>At the count. On the player's own motion: the heads of the houses that voted for it and against it. On a motion the
    /// leader of the player's bench voted on, and the player's house did not: the leader saw it.</summary>
    public static void Counted(World w, Motion m, string? mine)
    {
        if (mine == null) return;
        if (m.ProposerHouseId == mine)
            foreach (var (houseId, vote) in m.Votes.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                if (houseId != mine && vote != 0 && Head(w, houseId) is { } head) head.Flags.Add(vote > 0 ? VotedWithChar : VotedAgainstChar);
        if (m.ProposerHouseId != mine && m.TargetId != mine && m.Votes.GetValueOrDefault(mine) == 0 && FactionSystem.Of(w, mine) is { } bench
            && bench.LeaderHouseId != mine && m.Votes.GetValueOrDefault(bench.LeaderHouseId) != 0 && Head(w, bench.LeaderHouseId) is { } leader)
        { leader.Flags.Add(NoticedChar); Mark(w, AbstainedFlag); }
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
