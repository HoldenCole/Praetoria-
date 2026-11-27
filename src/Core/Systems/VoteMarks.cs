using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The people in the Senate's and the elections' news, marked so that a report can name them (docs/events/votes.md, "The marks
/// added to Core"). The moments were already there (a motion on the docket, a count, a purse found out, an Act carried, a rung
/// won and lost); what was missing was a flag on the person, so no role could bind them. Each mark is a character flag that lives
/// a year, set where the moment happens, and cleared by the system that owns it: the Senate's at its next session, the ladder's
/// with <see cref="LadderSystem.HeldFlag"/>. Nothing here moves a vote or a count. RNG-free.
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
    // a bargain struck in a report (a_vote_for_a_vote, a_bought_vote_called_in), made a favour owed at the next session (Bargains)
    public const string VoteOwedChar = "owes_you_a_vote", VotePromisedChar = "promised_your_vote";

    private static readonly string[] SessionChars = { VotedWithChar, VotedAgainstChar, NoticedChar, PurseOutChar, ActAgainstChar };
    private static readonly string[] SessionFlags = { AbstainedFlag, PurseOutFlag, ActAgainstFlag };
    private static readonly string[] DocketChars = { MovesChar, MovesAgainstChar, UndecidedChar };

    private static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }
    private static Character? Head(World w, string houseId) => w.House(houseId) is { } h ? Economy.HeadOf(w, h) : null;

    /// <summary>Last session's news, cleared before this session's is written.</summary>
    public static void ClearSession(World w)
    {
        foreach (var f in SessionFlags) w.WorldFlags.Remove(f);
        foreach (var c in w.Characters.Values) foreach (var f in SessionChars) c.Flags.Remove(f);
    }

    /// <summary>A bargain made in a report becomes a favour owed, which the Senate already keeps (WealthSystem, "A gift that asks"),
    /// before this session's motions are counted. A head marked <see cref="VoteOwedChar"/> gave their word for yours: their house owes
    /// yours a vote (it leans for your next motion, and the favour is spent at its count), and yours owes theirs one (at the count of
    /// their motion your vote is judged: for it, kept; anything else, broken). A head marked <see cref="VotePromisedChar"/> was only
    /// told you would vote for their motion: yours owes theirs. The marks go once they are favours.</summary>
    public static void Bargains(World w, string? mine)
    {
        foreach (var c in w.Characters.Values.Where(c => c.Flags.Contains(VoteOwedChar) || c.Flags.Contains(VotePromisedChar)).OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
        {
            bool both = c.Flags.Remove(VoteOwedChar); c.Flags.Remove(VotePromisedChar);
            if (mine == null || !c.Alive || c.HouseId == mine || w.House(c.HouseId) == null || World.Outsider(c.HouseId)) continue;
            WealthSystem.OweFor(w, mine, c.HouseId);
            if (both) WealthSystem.OweFor(w, c.HouseId, mine);
        }
    }

    /// <summary>Who has a motion on the docket, who moves against the player's house, and on the player's own motion who has not
    /// made up their mind. Written with the docket's other flags, every session.</summary>
    public static void Docket(World w, SenateSystem senate, string? mine)
    {
        w.WorldFlags.Remove(MyMotionFlag);
        foreach (var c in w.Characters.Values) foreach (var f in DocketChars) c.Flags.Remove(f);
        if (mine == null) return;
        foreach (var m in SenateSystem.Docket(w).ToList())
        {
            if (m.ProposerHouseId == mine)
            {
                w.WorldFlags.Add(MyMotionFlag);
                foreach (var h in w.Houses.Values.Where(h => h.Id != mine && !World.Outsider(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
                    if (!m.Votes.ContainsKey(h.Id) && senate.Lean(w, m, h.Id) == 0 && Head(w, h.Id) is { } undecided) undecided.Flags.Add(UndecidedChar);
                continue;
            }
            if (SenateSystem.IsPetition(m) || Head(w, m.ProposerHouseId) is not { } mover) continue;
            mover.Flags.Add(MovesChar);
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
    /// player's house won it, those of other houses who lost it.</summary>
    public static void LadderCounted(World w, IReadOnlyList<Character> candidates, IReadOnlyList<Character> winners, string? mine)
    {
        if (mine == null) return;
        bool oursLost = candidates.Any(c => c.HouseId == mine && !winners.Contains(c)), oursWon = winners.Any(c => c.HouseId == mine);
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
