using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The answers to the Senate's news that promised something of a vote and did nothing (docs/events/votes.md, "Promises", the other
/// group): the feast for the houses that voted your recognition, the round stood after your motion was thrown out, the names written
/// down, the repeal promised to a friend an Act fell on, the loans to houses with seats and no coin. Each report's choice sets a mark;
/// the mark is read as the choice is made (<see cref="Bargains"/>, from EventEngine.Resolve), and what the words promised is done then,
/// or kept where the Senate keeps it (a lean read by the whip count and the count, a record on the house page, a word judged when it
/// falls due). RNG-free: the houses are found by the marks the count left, and chosen by weight, then name.
/// </summary>
public static partial class VoteMarks
{
    /// <summary>The heads of the houses that voted for a motion that named the player's house and carried: for good (a recognition, a
    /// commission) or for ill (a censure). Set at the count (SenateSystem.Apply), cleared at the next session (<see cref="ClearSession"/>).</summary>
    public const string VotedYouUpChar = "voted_your_recognition", VotedYouDownChar = "voted_your_censure";
    /// <summary>The choices' marks: recognised_by_the_senate/a_feast, the_motion_thrown_out/buy_the_house_a_round, censured/remember_who_voted,
    /// the_motion_thrown_out/note_who_voted, a_name_in_commerce/lend_where_it_matters.</summary>
    public const string FeastFlag = "feasting_the_ayes", RoundFlag = "a_round_stood", CensureNamesFlag = "names_of_the_censure", NoesNamesFlag = "names_of_the_noes", LendFlag = "lending_where_it_matters";
    /// <summary>The heads of the houses that voted for and against a motion for the player's house that the reports ask the roll of: a
    /// triumph (the_triumph_refused), a return from exile (called_home), the powers for life (stripped_of_the_powers). Set at the count
    /// (<see cref="Counted"/>), cleared at the next session. The choices that read the roll set <see cref="DivisionFlag"/>.</summary>
    public const string ForYoursChar = "voted_for_your_house", AgainstYoursHouseChar = "voted_against_your_house", DivisionFlag = "names_of_the_division";
    private static readonly string[] DivisionKinds = { "triumph", ChargeSystem.HomeKind, "for_life" };
    /// <summary>Whether this is a motion for the player's house whose roll a report reads.</summary>
    public static bool RollRead(Data.MotionDef? def, Motion m, string mine) => def is { Target: "house" } && m.TargetId == mine && DivisionKinds.Contains(def.Interest);
    /// <summary>The friend an Act fell on, promised its repeal (the_senates_act_falls_on_a_friend/promise_the_repeal).</summary>
    public const string RepealChar = "promised_repeal";
    /// <summary>The numbers (docs/events/votes.md, "Promises", the other group, for why each is what it is).</summary>
    public const int FeastRegard = 3, RoundBy = 1, RoundYears = 3, RepealYears = 3, LendEach = 3, LendHouses = 2, LendRate = 0;
    public const string RoundPrefix = "round_stood:", RepealPrefix = "repeal_promised:", RepealBlockedPrefix = "repeal_blocked:";
    /// <summary>A house that voted down the player's last motion and was stood a round for it: the turn it was stood.</summary>
    public static string RoundKey(string houseId) => RoundPrefix + houseId;
    /// <summary>The player's word to a house to move the repeal of a Senate law while it stands at the option the Act made it: the turn given.</summary>
    public static string RepealKey(string houseId, string lawId, string optionId) => $"{RepealPrefix}{houseId}:{lawId}:{optionId}";

    private static string And(IReadOnlyList<string> names) => names.Count switch { 0 => "", 1 => names[0], _ => string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1] };
    /// <summary>The houses of the Reach (not the player's) whose living heads carry a mark, by name.</summary>
    private static List<House> Marked(World w, string flag, string mine) =>
        w.LivingCharacters().Where(c => c.Flags.Contains(flag) && c.HouseId != mine).Select(c => w.House(c.HouseId)).Where(h => h != null && !World.Outsider(h.Id))
            .Select(h => h!).Distinct().OrderBy(h => h.Name, StringComparer.Ordinal).ThenBy(h => h.Id, StringComparer.Ordinal).ToList();

    /// <summary>At the count of a motion that named the player's house and carried: the heads of the houses that voted for it.</summary>
    public static void Raised(World w, Motion m, string? mine, bool censure)
    {
        if (mine == null || m.TargetId != mine) return;
        foreach (var (houseId, vote) in m.Votes.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            if (houseId != mine && vote > 0 && Head(w, houseId) is { Alive: true } head) head.Flags.Add(censure ? VotedYouDownChar : VotedYouUpChar);
    }

    /// <summary>The marks a report's choice left, read as it is made.</summary>
    private static void Answers(World w, string? mine)
    {
        bool feast = w.WorldFlags.Remove(FeastFlag), round = w.WorldFlags.Remove(RoundFlag), censure = w.WorldFlags.Remove(CensureNamesFlag), noes = w.WorldFlags.Remove(NoesNamesFlag), lend = w.WorldFlags.Remove(LendFlag), division = w.WorldFlags.Remove(DivisionFlag);
        var friends = w.Characters.Values.Where(c => c.Flags.Contains(RepealChar)).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        foreach (var c in friends) c.Flags.Remove(RepealChar);
        if (mine == null || w.House(mine) is not { } house) return;
        if (feast) Feast(w, house);
        if (round) Round(w, house);
        if (censure) Names(w, house, VotedYouDownChar, DealingsSystem.VotedCensureKind, "voted for the censure of your house");
        if (noes) Names(w, house, VotedAgainstChar, DealingsSystem.VotedDownKind, "voted your motion down");
        if (division) Roll(w, house);
        foreach (var friend in friends) Repeal(w, house, friend);
        if (lend) Lend(w, house);
    }

    // ---------------- the feast: recognised_by_the_senate/a_feast ----------------

    /// <summary>The houses that voted the recognition dine at the player's table: each head thinks <see cref="FeastRegard"/> the better of
    /// the player (and their grown kin half that), which is what "a Senate that remembers the wine" remembers.</summary>
    private static void Feast(World w, House mine)
    {
        var ayes = Marked(w, VotedYouUpChar, mine.Id);
        if (ayes.Count == 0) { w.Tell("info", "Nobody of another house voted the recognition, so the feast was your own house's.", "houses"); return; }
        foreach (var h in ayes) DealingsSystem.Regard(w, h, mine, FeastRegard);
        w.Tell("good", $"The houses that voted the recognition dined at your table: the heads of {And(ayes.Select(h => h.Name).ToList())} each think {FeastRegard:+0;-0} of you for it.", "houses");
    }

    // ---------------- the round: the_motion_thrown_out/buy_the_house_a_round ----------------

    /// <summary>Every house that voted down the player's motion just thrown out leans <see cref="RoundBy"/> the player's way at the count
    /// of the next motion the player's house moves, within <see cref="RoundYears"/> years: so that count is closer than it would have been.</summary>
    private static void Round(World w, House mine)
    {
        var noes = Marked(w, VotedAgainstChar, mine.Id); if (noes.Count == 0) return;
        foreach (var h in noes) w.WorldCounters[RoundKey(h.Id)] = Math.Max(1, w.Turn);
        w.Tell("good", $"You stood the chamber a round. At the count of the next motion your house moves, each house that voted the last one down leans {RoundBy} your way: {And(noes.Select(h => h.Name).ToList())}. "
            + $"Move none in {Praetoria.Core.Counted.Years(RoundYears)} and the round is forgotten.", "senate:chamber");
    }
    /// <summary>What the round stood adds to a house's lean on the player's motion, and why, or 0. Read by the lean (SenateSystem.RawLean),
    /// so the whip count, the forecast and the count all have it.</summary>
    public static int RoundLean(World w, Motion m, string houseId, out string because)
    {
        because = ""; string? mine = w.Protagonist?.HouseId;
        if (mine == null || houseId == mine || m.ProposerHouseId != mine || !w.WorldCounters.ContainsKey(RoundKey(houseId))) return 0;
        because = "you stood the chamber a round after your last motion was thrown out"; return RoundBy;
    }
    /// <summary>The player's motion is counted: the round was for that count, and is spent.</summary>
    private static void RoundSpent(World w)
    {
        foreach (var k in w.WorldCounters.Keys.Where(k => k.StartsWith(RoundPrefix, StringComparison.Ordinal)).ToList()) w.WorldCounters.Remove(k);
    }
    /// <summary>A round stood <see cref="RoundYears"/> years ago with no motion of the player's since, and none before the Senate now, is forgotten.</summary>
    public static void RoundLapses(World w, string? mine)
    {
        var keys = w.WorldCounters.Keys.Where(k => k.StartsWith(RoundPrefix, StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal).ToList(); if (keys.Count == 0) return;
        if (mine != null && SenateSystem.Docket(w).Any(m => m.ProposerHouseId == mine)) return;   // its count is coming
        int since = keys.Min(k => w.Counter(k)); if (w.Turn - since < RoundYears) return;
        foreach (var k in keys) w.WorldCounters.Remove(k);
        if (mine != null) w.Tell("info", $"The round you stood the chamber in year {since} is forgotten: your house has moved no motion since.", "senate:chamber");
    }

    // ---------------- the names written down: censured/remember_who_voted, the_motion_thrown_out/note_who_voted ----------------

    /// <summary>Each house whose head carries the mark is written down on its page (DealingsSystem.Lasting), and the player is told which.</summary>
    private static void Names(World w, House mine, string mark, string kind, string did)
    {
        var houses = Marked(w, mark, mine.Id);
        foreach (var h in houses) DealingsSystem.Remember(w, kind, h.Id, mine.Id);
        w.Tell("info", houses.Count == 0 ? $"No house of the Reach {did}, so there was no name to write down."
            : $"You wrote down the houses that {did}: {And(houses.Select(h => h.Name).ToList())}. Each house's page keeps it.", "houses");
    }

    /// <summary>The roll of the division on a motion for the player's house (a triumph, a return from exile, the powers for life): the
    /// player is told who voted for it and who against, and each house that voted against it is written on its page.</summary>
    private static void Roll(World w, House mine)
    {
        var ayes = Marked(w, ForYoursChar, mine.Id); var noes = Marked(w, AgainstYoursHouseChar, mine.Id);
        if (ayes.Count + noes.Count == 0) { w.Tell("info", "There was no division of the Senate on it to read: no house of the Reach voted either way.", "houses"); return; }
        foreach (var h in noes) DealingsSystem.Remember(w, DealingsSystem.VotedAgainstYoursKind, h.Id, mine.Id);
        w.Tell("info", $"The roll of the division: {(ayes.Count > 0 ? "for it, " + And(ayes.Select(h => h.Name).ToList()) : "nobody of another house for it")}; {(noes.Count > 0 ? "against it, " + And(noes.Select(h => h.Name).ToList()) + ". Each house that voted against it is written down on its page" : "nobody against it")}.", "houses");
    }

    // ---------------- the repeal promised: the_senates_act_falls_on_a_friend/promise_the_repeal ----------------

    private static string LawOf(Data.LawDef? law) => law != null ? SenateActs.InASentence(law.Name) : "the Act";
    private static string InForce(World w, Data.PolicyCatalog cat, Data.LawDef law) => (PolicySystem.ReachOption(w, cat, law) ?? law.DefaultOption).Id;
    /// <summary>The first year the chamber will hear the law again (its Senate cooldown lifted), or now.</summary>
    private static int Opens(World w, string lawId) => w.Counter($"law_cd:{PolicySystem.SenateHolder}:{lawId}");
    private static (string house, string law, string option) Parse(string key) { var p = key[RepealPrefix.Length..].Split(':'); return p.Length == 3 ? (p[0], p[1], p[2]) : ("", "", ""); }
    private static List<string> RepealKeys(World w) => w.WorldCounters.Keys.Where(k => k.StartsWith(RepealPrefix, StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal).ToList();
    /// <summary>The years of the window in which the chamber could not hear a motion of the player's house, each a year more to keep the word in.</summary>
    private static string BlockedKey(string key) => RepealBlockedPrefix + key[RepealPrefix.Length..];
    /// <summary>The first year the word may be kept, and the last: <see cref="RepealYears"/> from the year the chamber will hear it, and a
    /// year more for each in which it could not hear a motion of the player's house (<see cref="RepealBlocked"/>).</summary>
    private static (int from, int by) RepealYearsOf(World w, string key, string lawId) { int from = Math.Max(w.Counter(key), Opens(w, lawId)); return (from, from + RepealYears - 1 + w.Counter(BlockedKey(key))); }
    /// <summary>Why the player's house could not have moved the repeal this year, for a reason not of its choosing, or null: no voice in
    /// the Senate (ProposeMotionCommand: no door, its head abroad), rule by decree, the docket full with other houses' business, or a motion
    /// of its own still before the Senate that was moved before the word could be kept.</summary>
    private static string? RepealBlocked(World w, SenateSystem senate, string mine, int from)
    {
        if (!senate.MayPropose(w, mine)) return "your house had no voice in the Senate";
        if (EdictSystem.Cowed(w, mine) != null) return "the Senate heard nothing moved while the holder ruled by decree";
        var docket = SenateSystem.Docket(w).ToList();
        if (docket.Count(m => m.ProposerHouseId != mine) >= SenateSystem.DocketSize) return "the docket was full with other houses' business";
        return docket.Any(m => m.ProposerHouseId == mine && m.ProposedTurn < from) ? "a motion of yours moved before then was still before the Senate" : null;
    }

    /// <summary>The player's word to the friend's house: the Act it voted against and lost, still in force as that Act made it, is to be
    /// moved again by the player's house when the chamber will hear it (its cooldown lifted), within <see cref="RepealYears"/> years.
    /// Kept when the player's house moves any motion on that law to another option (SenateSystem.Add calls <see cref="RepealMoved"/>);
    /// broken when the last year passes without it (<see cref="RepealDue"/>), as a word on a vote is (DealingsSystem.MotionCounted).
    /// The word is to the house: a new head of it holds the player to it.</summary>
    private static void Repeal(World w, House mine, Character friend)
    {
        if (w.House(friend.HouseId) is not { } theirs || theirs.Id == mine.Id) return;
        var act = w.Motions.Where(m => m.Resolved && m.Passed && m.LawId.Length > 0 && m.Votes.GetValueOrDefault(theirs.Id) < 0)
            .OrderByDescending(m => m.ResolvedTurn).ThenByDescending(m => m.Id, StringComparer.Ordinal).FirstOrDefault();
        var cat = SenateActs.Of(w); var law = act != null ? cat.Law(act.LawId) : null;
        if (act == null || law == null || InForce(w, cat, law) != act.LawOption) { w.Tell("info", $"There is no Act in force that {theirs.Name} voted against, so there is nothing to repeal.", "houses", theirs.Id); return; }
        string key = RepealKey(theirs.Id, law.Id, act.LawOption); if (w.WorldCounters.ContainsKey(key)) return;
        w.WorldCounters[key] = Math.Max(1, w.Turn);
        var (from, by) = RepealYearsOf(w, key, law.Id);
        w.Tell("warn", $"You promised {friend.Name} of {theirs.Name} to move the repeal of {LawOf(law)} when the chamber will hear it: {(from > w.Turn ? $"from year {from}" : "from this year")}, and by year {by}. "
            + $"Move it, and its head thinks {DealingsSystem.PledgeKeptRegard:+0;-0} of you; let year {by} pass without it, and its head thinks {-DealingsSystem.PledgeBrokenRegard:+0;-0} of you, it is written down, "
            + $"and {theirs.Name} takes no word of yours on a vote for {Praetoria.Core.Counted.Years(DealingsSystem.PledgeMemoryYears)}. A year in which the chamber cannot hear a motion of yours (the docket full with other houses' business, or your house without a voice in the Senate) gives you a year more.", "houses", theirs.Id);
    }
    /// <summary>A motion on a law is moved: by the player's house, to another option than the Act made, it keeps the word; by another house,
    /// it is the repeal moved before the player did, and the word is spent with nothing owed.</summary>
    public static void RepealMoved(World w, Motion m)
    {
        if (m.LawId.Length == 0) return; string? mine = w.Protagonist?.HouseId; var me = mine != null ? w.House(mine) : null;
        foreach (var key in RepealKeys(w))
        {
            var (houseId, lawId, option) = Parse(key); if (lawId != m.LawId || m.LawOption == option) continue;
            w.WorldCounters.Remove(key); var theirs = w.House(houseId); if (theirs == null || me == null) continue;
            string law = LawOf(SenateActs.Of(w).Law(lawId));
            if (m.ProposerHouseId == mine)
            {
                DealingsSystem.Regard(w, theirs, me, DealingsSystem.PledgeKeptRegard); DealingsSystem.Remember(w, DealingsSystem.PledgeKeptKind, me.Id, theirs.Id);
                w.Tell("good", $"You moved the repeal of {law}, as you promised {theirs.Name}: its head thinks {DealingsSystem.PledgeKeptRegard:+0;-0} of you.", "houses", theirs.Id);
                w.Log($"{me.Name} moves the repeal of {law}, as it promised {theirs.Name}.");
            }
            else w.Tell("info", $"{w.House(m.ProposerHouseId)?.Name ?? "Another house"} has moved the repeal of {law} before you did: your word to {theirs.Name} is spent, and nothing is owed either way.", "houses", theirs.Id);
        }
    }
    /// <summary>At each session: a word that can no longer be kept as given lapses, with a notice (the house is gone, the law is no longer
    /// as the Act made it, the two houses are at war or have blood between them); the player is reminded the year before the chamber will
    /// hear it and the year before the last; and a word whose last year has passed without the repeal moved is broken.</summary>
    public static void RepealDue(World w, SenateSystem senate, string? mine)
    {
        var cat = SenateActs.Of(w); var me = mine != null ? w.House(mine) : null;
        foreach (var k in w.WorldCounters.Keys.Where(k => k.StartsWith(RepealBlockedPrefix, StringComparison.Ordinal) && !w.WorldCounters.ContainsKey(RepealPrefix + k[RepealBlockedPrefix.Length..])).ToList()) w.WorldCounters.Remove(k);
        foreach (var key in RepealKeys(w))
        {
            var (houseId, lawId, option) = Parse(key); var theirs = w.House(houseId); var law = cat.Law(lawId); string name = LawOf(law);
            string? lapse = me == null ? "" : theirs == null || Economy.HeadOf(w, theirs) == null ? $"The house you promised to move the repeal of {name} for is no more, and your word goes with it."
                : law == null || InForce(w, cat, law) != option ? $"{char.ToUpperInvariant(name[0])}{name[1..]} is no longer what it was when you promised {theirs.Name} its repeal: your word is spent, and nothing is owed either way."
                : WealthSystem.Apart(w, me.Id, theirs.Id) ? $"{(w.AtWar(me.Id, theirs.Id) ? "You are at war with" : "There is blood between your house and")} {theirs.Name}: your word to move the repeal of {name} for it goes with that." : null;
            if (lapse != null) { w.WorldCounters.Remove(key); w.WorldCounters.Remove(BlockedKey(key)); if (lapse.Length > 0) w.Tell("info", lapse, "houses", houseId); continue; }
            var (from, by) = RepealYearsOf(w, key, lawId);
            // a year of the window in which the chamber could not hear the player's house: a year more, and said; after as many such years as the window had, the word lapses with nothing owed
            if (w.Turn >= from && w.Turn <= by && RepealBlocked(w, senate, me!.Id, from) is { } blocked)
            {
                int more = w.Counter(BlockedKey(key)) + 1;
                if (more > RepealYears) { w.WorldCounters.Remove(key); w.WorldCounters.Remove(BlockedKey(key)); w.Tell("info", $"For {Praetoria.Core.Counted.Years(RepealYears)} the chamber could not hear the repeal of {name} from your house, so your word to {theirs!.Name} lapses, and nothing is owed either way.", "houses", houseId); continue; }
                w.WorldCounters[BlockedKey(key)] = more; by++;
                w.Tell("info", $"This year the chamber could not hear the repeal of {name} from your house: {blocked}. Your word to {theirs!.Name} waits a year more: move it by year {by}.", "houses", houseId);
                continue;
            }
            if (w.Turn >= by)
            {
                w.WorldCounters.Remove(key); w.WorldCounters.Remove(BlockedKey(key)); DealingsSystem.Regard(w, theirs!, me!, -DealingsSystem.PledgeBrokenRegard); DealingsSystem.Remember(w, DealingsSystem.PledgeBrokenKind, me!.Id, theirs!.Id);
                w.Tell("warn", $"You promised {theirs.Name} to move the repeal of {name}, and year {by} has passed without it. Its head thinks {-DealingsSystem.PledgeBrokenRegard:+0;-0} of you, it is written down, and {theirs.Name} takes no word of yours on a vote for {Praetoria.Core.Counted.Years(DealingsSystem.PledgeMemoryYears)}.", "houses", theirs.Id);
                w.Log($"{me.Name} promised {theirs.Name} the repeal of {name}, and never moved it.");
            }
            else if (w.Turn == from - 1 || w.Turn == by - 1)
                w.Tell("warn", $"{(w.Turn == from - 1 ? "From next year the chamber will hear" : "Next year is the last in which to move")} the repeal of {name}, which you promised {theirs!.Name}. Move it by year {by}, and its head thinks {DealingsSystem.PledgeKeptRegard:+0;-0} of you; "
                    + $"let that year pass, and its head thinks {-DealingsSystem.PledgeBrokenRegard:+0;-0} of you, it is written down, and {theirs.Name} takes no word of yours on a vote for {Praetoria.Core.Counted.Years(DealingsSystem.PledgeMemoryYears)}.", "houses", houseId);
        }
    }

    // ---------------- the loans: a_name_in_commerce/lend_where_it_matters ----------------

    /// <summary>The house lends <see cref="LendEach"/> at no interest to each of the <see cref="LendHouses"/> weightiest houses of the Reach
    /// with seats and no coin (short of it, as a house that borrows is: DealingsSystem.ShortAt) that owe no loan already, are not at war
    /// or in blood with it, and whose heads would take its money. Each is then in its debt, and leans its way while it owes (OrderSystem.Sway,
    /// SenateSystem.YourWay). The choice paid out the whole sum; what nobody borrows comes back.</summary>
    private static void Lend(World w, House mine)
    {
        var me = w.Protagonist; mine.Treasury.Credits += LendEach * LendHouses;
        var borrowers = w.Houses.Values.Where(h => h.Id != mine.Id && !World.Outsider(h.Id) && PolitySystem.InReach(w, h.Id) && !WealthSystem.Apart(w, h.Id, mine.Id)
                && h.Treasury.Credits < DealingsSystem.ShortAt && DealingsSystem.LoanOwedBy(w, h.Id) == null && Head(w, h.Id) is { Alive: true } head && me != null && WealthSystem.WontTake(w, me, head) == null)
            .OrderByDescending(h => h.SphereInfluence.GetValueOrDefault("senate") + Progression.Title.RankOf(h.Title)).ThenBy(h => h.Id, StringComparer.Ordinal).Take(LendHouses).ToList();
        foreach (var b in borrowers) DealingsSystem.Make(w, mine, b, LendEach, LendRate);
        w.Tell(borrowers.Count > 0 ? "good" : "info", borrowers.Count == 0 ? "No house with a seat was short of coin this year and willing to take yours: the house lent nothing, and keeps its coin."
            : $"You lent {And(borrowers.Select(b => b.Name).ToList())} {Money.Credits(LendEach)} {(borrowers.Count > 1 ? "each " : "")}at no interest. {(borrowers.Count > 1 ? "Each owes" : "It owes")} your house what it was lent, paid back a little each year, "
              + "and while it owes it leans your way in the chamber: for what you move, against what is moved at your house, and the way you vote on the rest.", "houses", borrowers.FirstOrDefault()?.Id ?? "");
    }
}
