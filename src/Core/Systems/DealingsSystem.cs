using Praetoria.Core.Data;
using Praetoria.Core.Diagnostics;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Dealings between houses (docs/WEALTH.md, "Dealings between houses"): a gift of credits, a favour asked and called in, a
/// vote pledged, a threat, a loan at interest. What comes of each is kept where the game already keeps such things: regard
/// is what one head thinks of another, a favour owed is the counter <see cref="WealthSystem.FavourKey"/>, and a house that
/// is afraid is <see cref="PolitySystem.CowedKey"/>. Loans, pledges and the record of what passed are the three lists on
/// <see cref="World"/>. RNG-free: where the other houses choose, a hash of their ids and the year chooses.
/// </summary>
public sealed class DealingsSystem
{
    // what is remembered
    public const string GiftKind = "gift", FavourKeptKind = "favour_kept", FavourBrokenKind = "favour_broken", PledgeKeptKind = "pledge_kept", PledgeBrokenKind = "pledge_broken",
        ThreatKind = "threat", LoanKind = "loan", ForgivenKind = "loan_forgiven", PressedKind = "loan_pressed", DefaultKind = "loan_default", RepaidKind = "loan_repaid";
    public const int RememberYears = 20, RememberMost = 300, GiftsShownYears = 10;

    // a gift of credits
    public static readonly int[] GiftSizes = { 2, 5, 15, 40 };
    public const int GiftPctAPoint = 5, GiftRegardMost = 20, GiftFavourPct = 10, GiftFavourLeast = WealthSystem.FavourCredits;
    public static string GiftKey(string fromHouseId, string toHouseId) => $"gift_sent:{fromHouseId}>{toHouseId}";

    // a favour asked
    public const string Coin = "coin", Vote = "vote";
    public const int AskRegard = 20, AskCoinLeast = 2, AskCoinKeeps = 10, CallsCoinAt = 5;
    public static string CalledKey(string owerHouseId, string toHouseId) => $"favour_called:{owerHouseId}>{toHouseId}";

    // a vote pledged
    public const int PledgeCares = 4, PledgeLeadTimes = 3, PledgeKeptRegard = 3, PledgeBrokenRegard = 15, PledgeMemoryYears = 8;

    // a threat
    public const int ThreatOdds = PolitySystem.ThreatOdds, ThreatRegard = PolitySystem.ThreatRegard, CowedYears = PolitySystem.PledgeYears, PatronDispleasure = 15, DefiedRegard = 10;
    public const int OthersThreatenAt = -20, OthersThreatenOneIn = 4;

    // a loan at interest
    public static readonly int[] LoanSizes = { 5, 10, 20, 40 };
    public static readonly int[] LoanRates = { 10, 20, 30 };
    public const int LoanYears = 5, ShortAt = 10, BearsShort = 10, BearsPinched = 20, BearsInTheRed = 30, PinchedAt = 5, LoanIncomeYears = 2;
    public const int KindRateRegard = 4, FairRateRegard = 1, HardRateRegard = -1, ForgiveRegard = 10, PressRegard = 8, DefaultYears = 3, DefaultLegitimacy = 4, DefaultRegard = 10, RepaidRegard = 2;
    public const int OthersLendAgainYears = 5, OthersBorrowAt = 3, OthersLendFrom = 30, OthersLendRegard = 10, OthersLendPart = 3, OthersRate = 10, OthersForgiveAt = 30;
    public const string LoansMadeKey = "house_loans_made", LoansDefaultedKey = "house_loans_defaulted", LoansForgivenKey = "house_loans_forgiven", LoansRepaidKey = "house_loans_repaid",
        GiftsKey = "gifts_of_credits", ThreatsKey = "threats_made", PledgesKey = "votes_pledged", PledgesBrokenKey = "pledges_broken", CoinCallsKey = "favours_called_for_coin";

    private readonly Economy? _economy; private readonly SenateSystem _senate; private readonly MotionCatalog _motions;
    public DealingsSystem(Economy? economy, SenateSystem senate, MotionCatalog motions) { _economy = economy; _senate = senate; _motions = motions; }

    private static void Count(World w, string key) => w.WorldCounters[key] = w.Counter(key) + 1;
    private static uint Hash(string s) { uint h = 2166136261; foreach (char ch in s) h = (h ^ ch) * 16777619; return h; }
    private static Character? Head(World w, House? h) => h != null ? Economy.HeadOf(w, h) : null;
    /// <summary>What one house's head thinks of another's moves by this much, and what the rest of its grown people think
    /// by half as much: they hear of it, and whoever has the house next has not forgotten it.</summary>
    private static void Regard(World w, House of, House toward, int delta)
    {
        var a = Head(w, of); var b = Head(w, toward); if (a == null || b == null || a.Id == b.Id || delta == 0) return;
        var r = w.Relationship(a.Id, b.Id); r.Disposition = Math.Clamp(r.Disposition + delta, -100, 100);
        if (delta / KinHearPart == 0) return;
        foreach (var kin in w.LivingCharacters().Where(c => c.HouseId == of.Id && c.Id != a.Id && c.Id != b.Id && c.Age >= DynastySystem.AgeOfMajority).OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
        { var k = w.Relationship(kin.Id, b.Id); k.Disposition = Math.Clamp(k.Disposition + delta / KinHearPart, -100, 100); }
    }
    public const int KinHearPart = 2;
    /// <summary>What the head of one house thinks of the head of another.</summary>
    public static int RegardOf(World w, House of, House toward) => PolitySystem.Regard(w, of, toward);
    private static string Signed(int n) => n.ToString("+0;-0");

    /// <summary>Write down a thing that passed between two houses.</summary>
    public static void Remember(World w, string kind, string fromHouseId, string toHouseId, int amount = 0) =>
        w.Dealings.Add(new DealingRecord { Turn = w.Turn, Kind = kind, FromHouseId = fromHouseId, ToHouseId = toHouseId, Amount = amount });
    public static IEnumerable<DealingRecord> Remembered(World w, string kind, string fromHouseId, string toHouseId) =>
        w.Dealings.Where(d => d.Kind == kind && d.FromHouseId == fromHouseId && d.ToHouseId == toHouseId);

    /// <summary>What a house takes in over a year, in credits: every line of its ledger that brings credits in.</summary>
    public int Income(World w, House h) => ReadCache.Get("dealings_income:" + h.Id, () =>
        Math.Max(1, _economy != null ? _economy.Compute(w, h.Id).Lines.Where(l => l.Delta.Credits > 0).Sum(l => l.Delta.Credits) : h.Wealth / 10));

    /// <summary>Who is dealing with whom, or why they cannot: only the head of a house deals in its name, and only with a
    /// house of the Reach that has a head to answer.</summary>
    private static string? Parties(World w, Character? me, House? theirs, out House mine, out Character head)
    {
        mine = null!; head = null!;
        var house = me != null ? w.House(me.HouseId) : null; if (me?.Alive != true || house == null) return "no house";
        if (Economy.HeadOf(w, house)?.Id != me.Id) return "only the head of a house deals with another in its name";
        if (theirs == null || theirs.Id == house.Id) return "no such house";
        if (World.Outsider(theirs.Id)) return "they are not a house of the Reach, and are not dealt with as one";
        var th = Economy.HeadOf(w, theirs); if (th?.Alive != true) return $"{theirs.Name} has no head to deal with";
        mine = house; head = th; return null;
    }

    // ---------------------------------------------------------------- a gift of credits

    /// <summary>How much better their head would think of the giver for this sum: a point, and one more for every
    /// <see cref="GiftPctAPoint"/> parts in the hundred the sum is of what their house takes in a year.</summary>
    public int GiftRegard(World w, House theirs, int amount) => Math.Clamp(1 + GiftPct(w, theirs, amount) / GiftPctAPoint, 1, GiftRegardMost);
    public int GiftPct(World w, House theirs, int amount) => (int)Math.Min(1000, (long)amount * 100 / Income(w, theirs));
    /// <summary>A gift of a tenth of their year's income or more is a favour owed; but never one smaller than what a favour
    /// can be called in for, or a favour would be bought for less than it fetches.</summary>
    public bool GiftOwes(World w, House theirs, int amount) => GiftPct(w, theirs, amount) >= GiftFavourPct && amount >= GiftFavourLeast;

    public string? GiftRefusal(World w, Character? me, House? theirs, int amount)
    {
        if (Parties(w, me, theirs, out var mine, out var head) is { } no) return no;
        if (!GiftSizes.Contains(amount)) return "not a sum that is sent";
        if (WealthSystem.WontTake(w, me!, head) is { } wont) return wont;
        if (w.Counter(GiftKey(mine.Id, theirs!.Id)) == w.Turn + 1) return "you have sent them a gift this year";
        return mine.Treasury.Credits >= amount ? null : $"needs {Money.Credits(amount)} and the house has {Money.Show(mine.Treasury.Credits)}";
    }
    /// <summary>What the gift would do, said before it is sent.</summary>
    public string GiftSays(World w, House theirs, int amount)
    {
        int income = Income(w, theirs), r = GiftRegard(w, theirs, amount); bool owes = GiftOwes(w, theirs, amount);
        return $"{theirs.Name} takes in {Money.Credits(income)} a year, and this is {GiftPct(w, theirs, amount)} parts in the hundred of that: its head would think {Signed(r)} of you for it"
            + (owes ? ", and the house would owe you a favour." : $". A gift of {GiftFavourPct} parts in the hundred or more, and not less than {Money.Credits(GiftFavourLeast)}, leaves a favour owed.");
    }
    public void Gift(World w, Character me, House theirs, int amount)
    {
        var mine = w.House(me.HouseId)!; int r = GiftRegard(w, theirs, amount); bool owes = GiftOwes(w, theirs, amount) && !WealthSystem.FavourOwed(w, theirs.Id, mine.Id);
        mine.Treasury.Credits -= amount; theirs.Treasury.Credits += amount; Regard(w, theirs, mine, r);
        w.WorldCounters[GiftKey(mine.Id, theirs.Id)] = w.Turn + 1; Count(w, GiftsKey); Remember(w, GiftKind, mine.Id, theirs.Id, amount);
        if (owes) WealthSystem.OweFor(w, theirs.Id, mine.Id);
        w.Log($"{mine.Name} sends {theirs.Name} a gift of {Money.Credits(amount)}.{(owes ? " It is a favour owed." : "")}");
        if (mine.Id == w.Protagonist?.HouseId)
            w.Tell("good", $"{Head(w, theirs)?.Name} of {theirs.Name} takes your gift of {Money.Credits(amount)}, and thinks {Signed(r)} of you for it.{(owes ? $" {theirs.Name} owes your house a favour." : "")}", "houses", theirs.Id);
    }

    // ---------------------------------------------------------------- a favour asked, and called in

    /// <summary>The regard at which this house hears a request from that one: a patron or the leader of one's bench hears
    /// sooner than a stranger.</summary>
    public static int HearsAt(World w, House mine, House theirs) => AskAbove.Above(w, mine.Id).Any(x => x.house.Id == theirs.Id) ? AskAbove.AskAt : AskRegard;

    /// <summary>The side a house would be asked to vote on a motion: the asker's own, which it has moved or declared.</summary>
    public static int SideOf(Motion m, string houseId) => m.ProposerHouseId == houseId ? 1 : m.Votes.GetValueOrDefault(houseId);

    /// <summary>Why this house cannot be had to vote as the asker votes on a motion, or null.</summary>
    public string? VoteRefusal(World w, House mine, House theirs, string motionId)
    {
        var m = w.Motions.FirstOrDefault(x => x.Id == motionId); if (m == null || !m.Open) return "no such motion before the Senate";
        int side = SideOf(m, mine.Id); if (side == 0) return "declare your own vote on it first: they would vote as you do";
        if (m.ProposerHouseId == theirs.Id) return side > 0 ? "it is their own motion: they vote for it already" : "it is their own motion: they will not vote against it";
        if (_motions.Def(m.Kind) is { Target: "house" } && m.TargetId == theirs.Id && Math.Sign(_senate.Lean(w, m, theirs.Id)) != side) return "the motion names their own house: they will not vote that way on it";
        return m.Votes.TryGetValue(theirs.Id, out int cast) && cast == side ? "they have declared that way already" : null;
    }

    public string? AskRefusal(World w, Character? me, House? theirs, string what, string motionId = "")
    {
        if (Parties(w, me, theirs, out var mine, out var head) is { } no) return no;
        if (what != Coin && what != Vote) return "no such thing to ask";
        if (w.AtWar(mine.Id, theirs!.Id)) return "you are at war with them";
        int regard = RegardOf(w, theirs, mine), needs = HearsAt(w, mine, theirs);
        if (regard < needs) return $"{head.Name} thinks {Signed(regard)} of you, and hears a request at {Signed(needs)} ({needs - regard} short)";
        if (WealthSystem.FavourOwed(w, mine.Id, theirs.Id)) return $"you owe {theirs.Name} a favour already: settle that, and ask then";
        int last = w.Counter(AskAbove.AskedKey(theirs.Id, mine.Id));
        if (last > 0 && w.Turn - last < AskAbove.EveryYears) return $"you asked {theirs.Name} in year {last}: not again before year {last + AskAbove.EveryYears}";
        if (what == Vote) return VoteRefusal(w, mine, theirs, motionId);
        int coin = AskAbove.CoinOf(theirs);
        return coin < AskCoinLeast || theirs.Treasury.Credits - coin < AskCoinKeeps ? $"{theirs.Name} has no coin to spare: it holds {Money.Show(theirs.Treasury.Credits)}, gives a sixth of that, and keeps {Money.Show(AskCoinKeeps)} by" : null;
    }
    public string AskSays(World w, House theirs, string what) =>
        (what == Coin ? $"{theirs.Name} gives you {Money.Credits(AskAbove.CoinOf(theirs))}." : $"{theirs.Name} declares its vote as yours is, on the motion you name.")
        + $" You then owe it a favour. It calls it in on the next motion it moves, where it expects your vote; or, short of coin, it asks {Money.Credits(WealthSystem.FavourCredits)} of you. You may not ask it again for {Counted.Years(AskAbove.EveryYears)}.";
    public void Ask(World w, Character me, House theirs, string what, string motionId = "")
    {
        var mine = w.House(me.HouseId)!;
        if (what == Coin)
        {
            int coin = AskAbove.CoinOf(theirs); theirs.Treasury.Credits -= coin; mine.Treasury.Credits += coin;
            w.Log($"{mine.Name} asks {theirs.Name} for coin, and is given {Money.Credits(coin)}. It is a favour owed.");
        }
        else
        {
            var m = w.Motions.First(x => x.Id == motionId); int side = SideOf(m, mine.Id); m.Votes[theirs.Id] = side;
            w.Log($"{mine.Name} asks {theirs.Name} for its vote, and {theirs.Name} declares {(side > 0 ? "for" : "against")} the motion. It is a favour owed.");
        }
        w.WorldCounters[AskAbove.AskedKey(theirs.Id, mine.Id)] = Math.Max(1, w.Turn); Count(w, AskAbove.AskedCounter); WealthSystem.OweFor(w, mine.Id, theirs.Id);
    }

    /// <summary>A favour owed is spent on a vote now before the Senate: the house that owes it declares as the caller votes.</summary>
    public string? CallVoteRefusal(World w, Character? me, House? theirs, string motionId)
    {
        if (Parties(w, me, theirs, out var mine, out _) is { } no) return no;
        if (!WealthSystem.FavourOwed(w, theirs!.Id, mine.Id)) return "they owe you nothing";
        return VoteRefusal(w, mine, theirs, motionId);
    }
    public void CallVote(World w, Character me, House theirs, string motionId)
    {
        var mine = w.House(me.HouseId)!; var m = w.Motions.First(x => x.Id == motionId); int side = SideOf(m, mine.Id);
        w.WorldCounters.Remove(WealthSystem.FavourKey(theirs.Id, mine.Id)); m.Votes[theirs.Id] = side; Count(w, WealthSystem.FavoursCalledKey);
        w.Log($"{me.Name} calls in what {theirs.Name} owes: it declares {(side > 0 ? "for" : "against")} the motion, and owes nothing more.");
    }

    /// <summary>Has that house called in, for coin, the favour this one owes it, and not yet been answered?</summary>
    public static bool Called(World w, string owerHouseId, string toHouseId) => w.WorldCounters.ContainsKey(CalledKey(owerHouseId, toHouseId)) && WealthSystem.FavourOwed(w, owerHouseId, toHouseId);

    public string? AnswerRefusal(World w, Character? me, House? theirs, bool honour)
    {
        if (Parties(w, me, theirs, out var mine, out _) is { } no) return no;
        if (!Called(w, mine.Id, theirs!.Id)) return "they have asked nothing of you";
        return !honour || mine.Treasury.Credits >= WealthSystem.FavourCredits ? null : $"needs {Money.Credits(WealthSystem.FavourCredits)} and the house has {Money.Show(mine.Treasury.Credits)}";
    }
    /// <summary>The favour is called in for coin, and the house that owes it pays, or will not.</summary>
    public void Answer(World w, House mine, House theirs, bool honour, bool unanswered = false)
    {
        w.WorldCounters.Remove(CalledKey(mine.Id, theirs.Id)); w.WorldCounters.Remove(WealthSystem.FavourKey(mine.Id, theirs.Id));
        if (honour)
        {
            mine.Treasury.Credits -= WealthSystem.FavourCredits; theirs.Treasury.Credits += WealthSystem.FavourCredits; Regard(w, theirs, mine, WealthSystem.FavourKept);
            Count(w, WealthSystem.FavoursKeptKey); Remember(w, FavourKeptKind, mine.Id, theirs.Id, WealthSystem.FavourCredits);
            w.Log($"{mine.Name} pays {theirs.Name} {Money.Credits(WealthSystem.FavourCredits)}, as it was asked. The account between them is even.");
            return;
        }
        Regard(w, theirs, mine, -WealthSystem.FavourBroken); Count(w, WealthSystem.FavoursBrokenKey); Remember(w, FavourBrokenKind, mine.Id, theirs.Id);
        w.Log($"{theirs.Name} called in what {mine.Name} owed it, and {(unanswered ? "had no answer" : "was refused")}. {Head(w, theirs)?.Name ?? "Its head"} will remember it.");
        if (unanswered && mine.Id == w.Protagonist?.HouseId)
            w.Tell("warn", $"{theirs.Name} asked {Money.Credits(WealthSystem.FavourCredits)} for the favour you owed it, and had no answer. Its head thinks {Signed(-WealthSystem.FavourBroken)} of you for it.", "houses", theirs.Id);
    }

    // ---------------------------------------------------------------- a vote pledged

    /// <summary>The way a house wants a motion to go, if it has enough at stake to take another's word on it: +1, -1 or 0.
    /// The house that moved it wants it carried; any other must lean <see cref="PledgeCares"/> or more.</summary>
    public int Wants(World w, Motion m, House theirs)
    {
        if (m.ProposerHouseId == theirs.Id) return 1;
        int lean = _senate.Lean(w, m, theirs.Id); return Math.Abs(lean) >= PledgeCares ? Math.Sign(lean) : 0;
    }
    /// <summary>How far one side of a motion stands ahead as the houses lean, leaving out one house's own seats (negative: behind).</summary>
    public int LeadWithout(World w, Motion m, int side, string houseId)
    {
        var (f, a) = _senate.Tally(w, m); int stance = _senate.Stance(w, m, houseId), seats = _senate.Weight(w, houseId);
        if (stance > 0) f -= seats; else if (stance < 0) a -= seats;
        return side > 0 ? f - a : a - f;
    }
    public static VotePledge? PledgeOn(World w, string byHouseId, string motionId) => w.VotePledges.FirstOrDefault(p => p.ByHouseId == byHouseId && p.MotionId == motionId);

    public string? PledgeRefusal(World w, Character? me, House? theirs, string motionId)
    {
        if (Parties(w, me, theirs, out var mine, out var head) is { } no) return no;
        if (WealthSystem.WontTake(w, me!, head) is { } wont) return wont;
        var m = w.Motions.FirstOrDefault(x => x.Id == motionId); if (m == null || !m.Open) return "no such motion before the Senate";
        if (m.ProposerHouseId == mine.Id) return "it is your own motion: nobody doubts how you will vote";
        if (_motions.Def(m.Kind) is { Target: "house" } && m.TargetId == mine.Id) return "the motion names your own house: nobody doubts how you will vote";
        if (PledgeOn(w, mine.Id, m.Id) is { } given) return $"your vote on it is pledged to {w.House(given.ToHouseId)?.Name ?? "another house"} already";
        int wants = Wants(w, m, theirs!); if (wants == 0) return $"{theirs!.Name} does not care enough how it goes to want your word on it";
        int lead = LeadWithout(w, m, wants, mine.Id), seats = _senate.Weight(w, mine.Id);
        if (lead > PledgeLeadTimes * seats) return $"its side stands {lead} ahead without your {Counted.Seats(seats)}, and it does not need your word: it would at {PledgeLeadTimes * seats} or under";
        if (Remembered(w, PledgeBrokenKind, mine.Id, theirs!.Id).OrderByDescending(d => d.Turn).FirstOrDefault() is { } broke && w.Turn - broke.Turn < PledgeMemoryYears)
            return $"you broke your word to it in year {broke.Turn}: it takes no pledge of yours before year {broke.Turn + PledgeMemoryYears}";
        return WealthSystem.FavourOwed(w, theirs.Id, mine.Id) ? $"{theirs.Name} owes you a favour already, and will not owe two" : null;
    }
    public string PledgeSays(World w, Motion m, House theirs) =>
        $"{theirs.Name} wants it {(Wants(w, m, theirs) > 0 ? "carried" : "thrown out")}. Your vote is declared {(Wants(w, m, theirs) > 0 ? "for" : "against")} it. Vote so at the count and {theirs.Name} owes you a favour, and its head thinks {Signed(PledgeKeptRegard)} of you; "
        + $"change your vote and its head thinks {Signed(-PledgeBrokenRegard)} of you, and the house takes no pledge of yours for {Counted.Years(PledgeMemoryYears)}.";
    public void Pledge(World w, Character me, House theirs, string motionId)
    {
        var mine = w.House(me.HouseId)!; var m = w.Motions.First(x => x.Id == motionId); int side = Wants(w, m, theirs);
        w.VotePledges.Add(new VotePledge { ByHouseId = mine.Id, ToHouseId = theirs.Id, MotionId = m.Id, Side = side, Turn = w.Turn }); m.Votes[mine.Id] = side; Count(w, PledgesKey);
        w.Log($"{mine.Name} gives {theirs.Name} its word: it votes {(side > 0 ? "for" : "against")} the motion.");
    }

    /// <summary>A motion is counted (called by the Senate when it is). Every vote pledged on it was kept or broken; and a
    /// house that voted against the motion of one that had threatened it has defied it.</summary>
    public static void MotionCounted(World w, Motion m)
    {
        string? mine = w.Protagonist?.HouseId;
        foreach (var p in w.VotePledges.Where(p => p.MotionId == m.Id).OrderBy(p => p.ByHouseId, StringComparer.Ordinal).ThenBy(p => p.ToHouseId, StringComparer.Ordinal).ToList())
        {
            w.VotePledges.Remove(p); var by = w.House(p.ByHouseId); var to = w.House(p.ToHouseId); if (by == null || to == null) continue;
            bool kept = m.Votes.GetValueOrDefault(by.Id) == p.Side;
            if (kept)
            {
                Regard(w, to, by, PledgeKeptRegard); WealthSystem.OweFor(w, to.Id, by.Id); Remember(w, PledgeKeptKind, by.Id, to.Id);
                if (by.Id == mine) w.Tell("good", $"You voted as you gave {to.Name} your word you would. It owes your house a favour.", "houses", to.Id);
            }
            else
            {
                Regard(w, to, by, -PledgeBrokenRegard); Remember(w, PledgeBrokenKind, by.Id, to.Id); Count(w, PledgesBrokenKey);
                w.Log($"{by.Name} gave {to.Name} its word on a vote, and did not keep it.");
                if (by.Id == mine) w.Tell("warn", $"You gave {to.Name} your word on a vote and did not keep it. Its head thinks {Signed(-PledgeBrokenRegard)} of you, and it takes no pledge of yours for {Counted.Years(PledgeMemoryYears)}.", "houses", to.Id);
            }
        }
        // the player's house, threatened by the house that moved this, voted against it all the same
        if (mine != null && m.ProposerHouseId != mine && w.House(mine) is { } me && w.House(m.ProposerHouseId) is { } mover && PolitySystem.Cowed(w, me, mover) && m.Votes.GetValueOrDefault(mine) < 0)
        {
            Regard(w, mover, me, -DefiedRegard);
            w.Tell("warn", $"You voted against the motion of {mover.Name}, which had told you what its fleets could do. Its head thinks {Signed(-DefiedRegard)} of you for it.", "houses", mover.Id);
        }
    }

    // ---------------------------------------------------------------- a threat

    /// <summary>What a house that is afraid of another does about a motion: it will not vote against what that house moves,
    /// nor for a motion aimed at it. It stays away instead. Returns what to add to its lean. The player's house votes as it
    /// pleases, afraid or not.</summary>
    public static int CowedLean(World w, MotionDef? def, Motion m, string houseId, int lean, out string? of)
    {
        of = null; if (lean == 0 || houseId == w.Protagonist?.HouseId || w.House(houseId) is not { } h) return 0;
        if (lean < 0 && m.ProposerHouseId != houseId && w.House(m.ProposerHouseId) is { } mover && PolitySystem.Cowed(w, h, mover)) { of = mover.Name; return -lean; }
        if (lean > 0 && def is { Target: "house" } && def.Interest is "censure" or "embargo" or "breakup" or "recall" or "reconquest" && m.TargetId != houseId
            && w.House(m.TargetId) is { } struck && PolitySystem.Cowed(w, h, struck)) { of = struck.Name; return -lean; }
        return 0;
    }

    public string? ThreatRefusal(World w, Character? me, House? theirs)
    {
        if (Parties(w, me, theirs, out var mine, out _) is { } no) return no;
        if (w.AtWar(mine.Id, theirs!.Id)) return "you are at war with them already: the fleets speak for themselves";
        if (theirs.PatronId == mine.Id) return "it is your own client, and does as you wish already";
        int hulls = FleetSystem.StrengthOf(w, mine.Id), their = FleetSystem.StrengthOf(w, theirs.Id), need = ThreatOdds * Math.Max(1, their);
        if (hulls < need) return $"they are not afraid of you: it takes {ThreatOdds} hulls to their one, and you have {hulls} to their {their} ({need - hulls} short)";
        return PolitySystem.Cowed(w, theirs, mine) ? "they are afraid of you already" : null;
    }
    /// <summary>The patron whose displeasure a threat against this house brings: any patron it has that is not the one who threatens.</summary>
    public static House? Protector(World w, House theirs, House mine) => theirs.PatronId.Length > 0 && theirs.PatronId != mine.Id ? w.House(theirs.PatronId) : null;
    public string ThreatSays(World w, House mine, House theirs) =>
        $"Its head thinks {Signed(ThreatRegard)} of you. For {Counted.Years(CowedYears)}, while you keep twice its hulls, {theirs.Name} is afraid of you: {CowedDoes}"
        + (Protector(w, theirs, mine) is { } patron ? $" It is a client of {patron.Name}, whose head will think {Signed(-PatronDispleasure)} of you for it." : "");
    public const string CowedDoes = "it does not vote against what you move, nor for a motion that strikes at your house, and it goes with you if your region leaves the Reach.";
    public void Threat(World w, House mine, House theirs)
    {
        Regard(w, theirs, mine, ThreatRegard); w.WorldCounters[PolitySystem.CowedKey(theirs.Id, mine.Id)] = w.Turn; Count(w, ThreatsKey); Remember(w, ThreatKind, mine.Id, theirs.Id);
        w.Log($"{mine.Name} lets {theirs.Name} understand what its fleets could do. {theirs.Name} will not stand in its way, and will not forget being told.");
        if (Protector(w, theirs, mine) is { } patron)
        {
            Regard(w, patron, mine, -PatronDispleasure); w.Log($"{patron.Name}, whose client {theirs.Name} is, takes it as said to itself.");
            if (mine.Id == w.Protagonist?.HouseId) w.Tell("warn", $"{patron.Name} is the patron of {theirs.Name}, and has heard what you said to its client. Its head thinks {Signed(-PatronDispleasure)} of you for it.", "houses", patron.Id);
        }
        if (theirs.Id == w.Protagonist?.HouseId)
            w.Tell("warn", $"{Head(w, mine)?.Name} of {mine.Name} lets you understand what its fleets could do to yours. Vote against what it moves and it will be remembered.", "houses", mine.Id);
    }

    // ---------------------------------------------------------------- a loan at interest

    public static HouseLoan? LoanBetween(World w, string lenderId, string borrowerId) => w.HouseLoans.FirstOrDefault(l => l.LenderId == lenderId && l.BorrowerId == borrowerId && l.Owed > 0);
    public static HouseLoan? LoanOwedBy(World w, string borrowerId) => w.HouseLoans.FirstOrDefault(l => l.BorrowerId == borrowerId && l.Owed > 0);
    /// <summary>The rate a house will bear: more the nearer it is to having nothing.</summary>
    public static int Bears(House theirs) => theirs.Treasury.Credits < 0 ? BearsInTheRed : theirs.Treasury.Credits < PinchedAt ? BearsPinched : BearsShort;
    /// <summary>The most a house can be lent: what would leave it owing <see cref="LoanIncomeYears"/> years of what it takes in.</summary>
    public int LendMost(World w, House theirs, int ratePct) => LoanSizes.Where(a => Lending.Owed(a, ratePct) <= LoanIncomeYears * Income(w, theirs)).DefaultIfEmpty(0).Max();
    public static int InstalmentOf(int owed) => Math.Max(1, (owed + LoanYears - 1) / LoanYears);
    public static int RateRegard(int ratePct) => ratePct <= BearsShort ? KindRateRegard : ratePct <= BearsPinched ? FairRateRegard : HardRateRegard;

    public string? LendRefusal(World w, Character? me, House? theirs, int amount, int ratePct)
    {
        if (Parties(w, me, theirs, out var mine, out var head) is { } no) return no;
        if (!LoanSizes.Contains(amount) || !LoanRates.Contains(ratePct)) return "not a sum or a rate that is offered";
        if (WealthSystem.WontTake(w, me!, head) is { } wont) return wont;
        if (LoanOwedBy(w, theirs!.Id) is { } has) return has.LenderId == mine.Id ? $"they owe you {Money.Credits(has.Owed)} already" : $"they owe {Money.Credits(has.Owed)} to {w.House(has.LenderId)?.Name ?? "another house"}: a second lender wants the first paid";
        if (theirs.Treasury.Credits >= ShortAt) return $"{theirs.Name} is not short of coin: it holds {Money.Show(theirs.Treasury.Credits)}, and borrows only under {Money.Show(ShortAt)}";
        if (ratePct > Bears(theirs)) return $"{head.Name} will not pay {ratePct} in the hundred: with {Money.Show(theirs.Treasury.Credits)} in hand, {Bears(theirs)} is the most it bears";
        int owed = Lending.Owed(amount, ratePct), income = Income(w, theirs);
        if (owed > LoanIncomeYears * income) return $"{theirs.Name} takes in {Money.Credits(income)} a year and could not repay {Money.Show(owed)}: {(LendMost(w, theirs, ratePct) is > 0 and var most ? $"it would borrow {Money.Show(most)} at the most" : "it can borrow nothing")}";
        return mine.Treasury.Credits >= amount ? null : $"needs {Money.Credits(amount)} and the house has {Money.Show(mine.Treasury.Credits)}";
    }
    public string LendSays(World w, House theirs, int amount, int ratePct)
    {
        int owed = Lending.Owed(amount, ratePct), each = InstalmentOf(owed);
        return $"{theirs.Name} would owe you {Money.Credits(owed)}, and pay {Money.Show(each)} into your treasury each year for {Counted.Years(Lending.Years(owed, each))}, when it has it. Its head would think {Signed(RateRegard(ratePct))} of you for the terms. "
            + "While it owes you it leans your way in the chamber.";
    }
    /// <summary>The loan is made: by the player's head, or by another house's steward.</summary>
    public HouseLoan Lend(World w, House lender, House borrower, int amount, int ratePct)
    {
        lender.Treasury.Credits -= amount; borrower.Treasury.Credits += amount; int owed = Lending.Owed(amount, ratePct);
        var loan = new HouseLoan { LenderId = lender.Id, BorrowerId = borrower.Id, Lent = amount, RatePct = ratePct, Owed = owed, Instalment = InstalmentOf(owed), Since = w.Turn };
        w.HouseLoans.Add(loan); Regard(w, borrower, lender, RateRegard(ratePct)); Count(w, LoansMadeKey); Remember(w, LoanKind, lender.Id, borrower.Id, amount);
        string? mine = w.Protagonist?.HouseId;
        if (lender.Id == mine || borrower.Id == mine) w.Log($"{lender.Name} lends {borrower.Name} {Money.Credits(amount)} at {ratePct} in the hundred: {Money.Show(owed)} is owed, {Money.Show(loan.Instalment)} a year.");
        return loan;
    }

    /// <summary>What a borrower pays this year on what it was lent: the instalment if its treasury holds it; if not, nothing,
    /// or whatever it has once the lender has pressed. Nothing passes between houses at war.</summary>
    public static int Due(World w, HouseLoan l)
    {
        var b = w.House(l.BorrowerId); if (b == null || w.House(l.LenderId) == null || l.Owed <= 0 || w.AtWar(l.LenderId, l.BorrowerId)) return 0;
        int pay = Math.Min(l.Instalment, l.Owed), have = b.Treasury.Credits;
        return have >= pay ? pay : l.Pressed ? Math.Max(0, have) : 0;
    }

    /// <summary>The year's instalments have been paid, or not (called by the economy once every house's books are done).</summary>
    public static void Paid(World w, IReadOnlyDictionary<HouseLoan, int> dues)
    {
        string? mine = w.Protagonist?.HouseId;
        foreach (var l in w.HouseLoans.OrderBy(l => l.LenderId, StringComparer.Ordinal).ThenBy(l => l.BorrowerId, StringComparer.Ordinal).ToList())
        {
            var lender = w.House(l.LenderId); var borrower = w.House(l.BorrowerId);
            if (lender == null || borrower == null)
            {
                w.HouseLoans.Remove(l);
                if (l.LenderId == mine && lender != null) w.Tell("warn", $"The house that owed you {Money.Credits(l.Owed)} is no more, and the debt with it.", "house:ledger");
                continue;
            }
            if (!dues.TryGetValue(l, out int paid)) continue;   // made since the year's books were opened: nothing falls due yet
            int due = Math.Min(l.Instalment, l.Owed); l.Owed -= paid;
            if (l.Owed <= 0)
            {
                w.HouseLoans.Remove(l); Regard(w, borrower, lender, RepaidRegard); Count(w, LoansRepaidKey); Remember(w, RepaidKind, borrower.Id, lender.Id, l.Lent);
                if (lender.Id == mine) w.Tell("good", $"{borrower.Name} has paid back the last of what you lent it.", "houses", borrower.Id);
                continue;
            }
            if (paid >= due) { l.Missed = 0; continue; }
            l.Missed += 1;
            if (l.Pressed && l.Missed >= DefaultYears) { Default(w, l, lender, borrower); continue; }
            if (lender.Id == mine)
                w.Tell("warn", $"{borrower.Name} {(paid > 0 ? $"pays {Money.Credits(paid)} of" : "cannot pay")} this year's {Money.Credits(due)} on what you lent it: {Money.Show(l.Owed)} is still owed. "
                    + (l.Pressed ? $"You have pressed for it: {Counted.MoreYears(DefaultYears - l.Missed)} of this and it is a default." : "Forgive the rest, or press for it, on its page."), "houses", borrower.Id);
        }
    }

    /// <summary>A pressed loan has gone <see cref="DefaultYears"/> years short: it is written off, and the Reach hears of it.</summary>
    private static void Default(World w, HouseLoan l, House lender, House borrower)
    {
        w.HouseLoans.Remove(l); LegitimacyBook.Move(w, borrower, -DefaultLegitimacy, LegitimacyBook.Scandal, $"Defaulted on a loan from {lender.Name}"); Regard(w, lender, borrower, -DefaultRegard);
        w.WorldCounters[VentureSystem.BadNameKey(borrower.Id)] = Math.Max(w.Counter(VentureSystem.BadNameKey(borrower.Id)), w.Turn + VentureSystem.BadNameYears);
        Count(w, LoansDefaultedKey); Remember(w, DefaultKind, borrower.Id, lender.Id, l.Owed);
        w.Log($"{borrower.Name} has defaulted on what it owed {lender.Name}: {Money.Credits(l.Owed)} is written off, and every counting-house in the Reach has heard of it.");
        if (lender.Id == w.Protagonist?.HouseId)
            w.Tell("warn", $"{borrower.Name} has defaulted: the {Money.Credits(l.Owed)} it still owed you is written off. Its standing falls by {DefaultLegitimacy}, and for {Counted.Years(VentureSystem.BadNameYears)} every lender charges it more.", "houses", borrower.Id);
    }

    public string? ForgiveRefusal(World w, Character? me, House? theirs)
    {
        if (Parties(w, me, theirs, out var mine, out _) is { } no) return no;
        var l = LoanBetween(w, mine.Id, theirs!.Id); if (l == null) return "they owe you nothing";
        return l.Missed > 0 ? null : "they have missed no payment: there is nothing yet to forgive";
    }
    /// <summary>The lender lets the rest go: the borrower's head thinks the better of it, and the house owes a favour.</summary>
    public static void Forgive(World w, House lender, House borrower)
    {
        var l = LoanBetween(w, lender.Id, borrower.Id); if (l == null) return;
        w.HouseLoans.Remove(l); Regard(w, borrower, lender, ForgiveRegard); WealthSystem.OweFor(w, borrower.Id, lender.Id); Count(w, LoansForgivenKey); Remember(w, ForgivenKind, lender.Id, borrower.Id, l.Owed);
        string? mine = w.Protagonist?.HouseId;
        if (lender.Id == mine || borrower.Id == mine) w.Log($"{lender.Name} forgives {borrower.Name} the {Money.Credits(l.Owed)} it still owed. It is a favour owed.");
    }
    public string? PressRefusal(World w, Character? me, House? theirs)
    {
        if (Parties(w, me, theirs, out var mine, out _) is { } no) return no;
        var l = LoanBetween(w, mine.Id, theirs!.Id); if (l == null) return "they owe you nothing";
        if (l.Pressed) return "you have pressed them already";
        return l.Missed > 0 ? null : "they have missed no payment";
    }
    /// <summary>The lender says it wants its money: the borrower pays what it has each year, and thinks the worse of the lender.</summary>
    public static void Press(World w, House lender, House borrower)
    {
        var l = LoanBetween(w, lender.Id, borrower.Id); if (l == null) return;
        l.Pressed = true; Regard(w, borrower, lender, -PressRegard); Remember(w, PressedKind, lender.Id, borrower.Id, l.Owed);
        string? mine = w.Protagonist?.HouseId;
        if (lender.Id == mine || borrower.Id == mine) w.Log($"{lender.Name} presses {borrower.Name} for the {Money.Credits(l.Owed)} it owes.");
    }

    // ---------------------------------------------------------------- what lasts

    /// <summary>What stands between two houses and is remembered, as the first sees it: favours each way, loans each way,
    /// gifts of late, whether either is afraid of the other, pledges and favours kept and broken. Each line with its tone
    /// ("good", "warn" or "").</summary>
    public List<(string text, string tone)> Lasting(World w, House mine, House theirs)
    {
        var list = new List<(string, string)>(); void Say(string text, string tone = "") => list.Add((text, tone));
        if (WealthSystem.FavourOwed(w, theirs.Id, mine.Id))
        { int since = w.Counter(WealthSystem.FavourKey(theirs.Id, mine.Id)); Say($"It owes you a favour, from year {since}. Uncalled, it is forgotten in year {since + WealthSystem.FavourYears}.", "good"); }
        if (WealthSystem.FavourOwed(w, mine.Id, theirs.Id))
        {
            int since = w.Counter(WealthSystem.FavourKey(mine.Id, theirs.Id));
            Say(Called(w, mine.Id, theirs.Id) ? $"You owe it a favour, and it has called it in: it asks {Money.Credits(WealthSystem.FavourCredits)}. Unanswered by the year's end, it is taken as refused."
                : $"You owe it a favour, from year {since}. It calls it in on the next motion it moves, where it expects your vote; or, short of coin, it asks {Money.Credits(WealthSystem.FavourCredits)}.", "warn");
        }
        if (LoanBetween(w, mine.Id, theirs.Id) is { } lent)
            Say($"You lent it {Money.Credits(lent.Lent)} in year {lent.Since} at {lent.RatePct} in the hundred: {Money.Show(lent.Owed)} is still owed, {Money.Show(Math.Min(lent.Instalment, lent.Owed))} a year."
                + (lent.Missed > 0 ? $" It has gone {Counted.Years(lent.Missed)} short." : "") + (lent.Pressed ? $" You have pressed for it: {Counted.Years(DefaultYears)} short and it is a default." : ""), lent.Missed > 0 ? "warn" : "");
        if (LoanBetween(w, theirs.Id, mine.Id) is { } owe) Say($"It lent you {Money.Credits(owe.Lent)} in year {owe.Since}: {Money.Show(owe.Owed)} is still owed.", "warn");
        if (theirs.Debt > 0 && theirs.LenderId == mine.Id) Say($"It owes your bank {Money.Credits(theirs.Debt)}.");
        if (mine.Debt > 0 && mine.LenderId == theirs.Id) Say($"You owe its bank {Money.Credits(mine.Debt)}.", "warn");
        if (w.WorldCounters.TryGetValue(PolitySystem.CowedKey(theirs.Id, mine.Id), out int told) && w.Turn - told <= CowedYears)
            Say(PolitySystem.Cowed(w, theirs, mine) ? $"It has been afraid of you since year {told}, and is until year {told + CowedYears} while you keep twice its hulls: {CowedDoes}"
                : $"You threatened it in year {told}. It is not afraid now: you have not twice its hulls.", PolitySystem.Cowed(w, theirs, mine) ? "good" : "");
        if (w.WorldCounters.TryGetValue(PolitySystem.CowedKey(mine.Id, theirs.Id), out int toldMe) && w.Turn - toldMe <= CowedYears)
            Say($"It threatened you in year {toldMe}. Vote against what it moves and its head thinks {Signed(-DefiedRegard)} of you.", "warn");
        foreach (var p in w.VotePledges.Where(p => p.ByHouseId == mine.Id && p.ToHouseId == theirs.Id).OrderBy(p => p.MotionId, StringComparer.Ordinal))
            if (w.Motions.FirstOrDefault(m => m.Id == p.MotionId) is { } pm) Say($"Your vote is pledged to it, {(p.Side > 0 ? "for" : "against")} this motion: {_senate.Describe(w, pm)}");
        var sent = Remembered(w, GiftKind, mine.Id, theirs.Id).Where(d => w.Turn - d.Turn <= GiftsShownYears).OrderByDescending(d => d.Turn).ToList();
        if (sent.Count > 0) Say("You sent it " + string.Join(", ", sent.Take(4).Select(d => $"{Money.Credits(d.Amount)} in year {d.Turn}")) + (sent.Count > 4 ? ", and more before" : "") + ".");
        var got = w.Heirlooms.Where(h => h.FromHouseId == theirs.Id && h.Kind is WealthSystem.Plate or WealthSystem.Orrery && w.Char(h.HolderId)?.HouseId == mine.Id).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        if (got.Count > 0) Say("It sent your house " + string.Join(" and ", got.Take(3).Select(h => h.Name)) + ".");
        int Times(string kind, string from, string to) => Remembered(w, kind, from, to).Count();
        int kept = Times(PledgeKeptKind, mine.Id, theirs.Id), broken = Times(PledgeBrokenKind, mine.Id, theirs.Id);
        if (kept + broken > 0) Say($"Your word to it on a vote: kept {Counted.Times(kept)}, broken {Counted.Times(broken)}.", broken > 0 ? "warn" : "good");
        int honoured = Times(FavourKeptKind, mine.Id, theirs.Id), refused = Times(FavourBrokenKind, mine.Id, theirs.Id);
        if (honoured + refused > 0) Say($"Favours it called in: honoured {Counted.Times(honoured)}, refused {Counted.Times(refused)}.", refused > 0 ? "warn" : "good");
        foreach (var d in w.Dealings.Where(d => d.Kind == ForgivenKind && d.FromHouseId == mine.Id && d.ToHouseId == theirs.Id || d.Kind is DefaultKind or RepaidKind && d.FromHouseId == theirs.Id && d.ToHouseId == mine.Id).OrderByDescending(d => d.Turn).Take(3))
            Say(d.Kind == ForgivenKind ? $"You forgave it {Money.Credits(d.Amount)} in year {d.Turn}." : d.Kind == DefaultKind ? $"It defaulted on {Money.Credits(d.Amount)} it owed you in year {d.Turn}." : $"It paid back in year {d.Turn} the {Money.Credits(d.Amount)} you lent it.", d.Kind == DefaultKind ? "warn" : "");
        return list;
    }

    // ---------------------------------------------------------------- the year

    public void Tick(World w)
    {
        string? mine = w.Protagonist?.HouseId;
        w.Dealings.RemoveAll(d => w.Turn - d.Turn > RememberYears || w.House(d.FromHouseId) == null || w.House(d.ToHouseId) == null);
        if (w.Dealings.Count > RememberMost) w.Dealings.RemoveRange(0, w.Dealings.Count - RememberMost);
        // a pledge on a motion that never came to a count is void
        w.VotePledges.RemoveAll(p => w.Motions.FirstOrDefault(m => m.Id == p.MotionId) is not { Open: true });
        // a threat that has run its years is forgotten
        foreach (var key in w.WorldCounters.Keys.Where(k => k.StartsWith("cowed:", StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal).ToList())
            if (w.Turn - w.Counter(key) > CowedYears) w.WorldCounters.Remove(key);
        CallsForCoin(w, mine);
        OthersAnswerLoans(w, mine);
        OthersLend(w, mine);
        OthersThreaten(w, mine);
    }

    /// <summary>A house the player owes a favour to, and that is short of coin with no motion of its own to spend it on, asks
    /// for coin instead. The asking stands for a year; unanswered, it is taken as refused.</summary>
    private void CallsForCoin(World w, string? mine)
    {
        var me = mine != null ? w.House(mine) : null; if (me == null) return;
        foreach (var key in w.WorldCounters.Keys.Where(k => k.StartsWith("favour_called:" + me.Id + ">", StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal).ToList())
        {
            var to = w.House(key[(key.IndexOf('>') + 1)..]);
            if (to == null || !WealthSystem.FavourOwed(w, me.Id, to.Id)) { w.WorldCounters.Remove(key); continue; }   // lapsed, or the house is gone
            if (w.Counter(key) < w.Turn) Answer(w, me, to, honour: false, unanswered: true);
        }
        foreach (var to in WealthSystem.IOwe(w, me.Id))
        {
            var head = Head(w, to); if (head?.Alive != true || World.Outsider(to.Id) || Called(w, me.Id, to.Id) || w.AtWar(me.Id, to.Id)) continue;
            if (w.Turn - w.Counter(WealthSystem.FavourKey(me.Id, to.Id)) < 1 || to.Treasury.Credits >= CallsCoinAt || w.Motions.Any(m => m.Open && m.ProposerHouseId == to.Id)) continue;
            w.WorldCounters[CalledKey(me.Id, to.Id)] = w.Turn; Count(w, CoinCallsKey);
            w.Tell("warn", $"{head.Name} of {to.Name} calls in the favour you owe: the house is short of coin, and asks {Money.Credits(WealthSystem.FavourCredits)}. Pay it or refuse it on its page; unanswered by the year's end, it is taken as refused.", "houses", to.Id);
        }
    }

    /// <summary>A house other than the player's that is owed and has not been paid: a friend or a patron forgives, anyone else presses.</summary>
    private static void OthersAnswerLoans(World w, string? mine)
    {
        foreach (var l in w.HouseLoans.Where(l => l.LenderId != mine && l.Missed > 0 && !l.Pressed).OrderBy(l => l.LenderId, StringComparer.Ordinal).ThenBy(l => l.BorrowerId, StringComparer.Ordinal).ToList())
        {
            var lender = w.House(l.LenderId); var borrower = w.House(l.BorrowerId); if (lender == null || borrower == null) continue;
            if (borrower.PatronId == lender.Id || RegardOf(w, lender, borrower) >= OthersForgiveAt) Forgive(w, lender, borrower); else Press(w, lender, borrower);
        }
    }

    /// <summary>Other houses lend to each other the same way: one with next to nothing in hand is lent a small sum by the
    /// richest house that is its patron, or whose head thinks well of its own, or that sits on its bench and thinks no ill of
    /// it; at the lowest rate. They do not lend to the player's house, which borrows of their banks.</summary>
    private void OthersLend(World w, string? mine)
    {
        bool Lends(House h) => h.Id != mine && !World.Outsider(h.Id) && Head(w, h)?.Alive == true;
        foreach (var b in w.Houses.Values.Where(h => Lends(h) && h.Treasury.Credits < OthersBorrowAt && LoanOwedBy(w, h.Id) == null).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            // one that was let off, or that defaulted, is not lent to again for some years
            if (w.Dealings.Any(d => w.Turn - d.Turn < OthersLendAgainYears && (d.Kind == ForgivenKind && d.ToHouseId == b.Id || d.Kind == DefaultKind && d.FromHouseId == b.Id))) continue;
            var bench = FactionSystem.Of(w, b.Id);
            var lender = w.Houses.Values.Where(l => Lends(l) && l.Id != b.Id && l.Treasury.Credits >= OthersLendFrom && !w.AtWar(l.Id, b.Id) && LoanOwedBy(w, l.Id) == null
                    && (b.PatronId == l.Id || RegardOf(w, l, b) >= OthersLendRegard || bench != null && RegardOf(w, l, b) >= 0 && FactionSystem.Of(w, l.Id)?.Id == bench.Id))
                .OrderByDescending(l => l.Treasury.Credits).ThenBy(l => l.Id, StringComparer.Ordinal).FirstOrDefault();
            if (lender == null) continue;
            int amount = Math.Min((Math.Max(0, -b.Treasury.Credits) / Lending.Step + 1) * Lending.Step, lender.Treasury.Credits / OthersLendPart / Lending.Step * Lending.Step);
            if (amount >= Lending.Step && Lending.Owed(amount, OthersRate) <= LoanIncomeYears * Income(w, b)) Lend(w, lender, b, amount, OthersRate);
        }
    }

    /// <summary>A hard head with three times the hulls of a house it thinks ill of sometimes says what its fleets could do.
    /// The player's house is told, and votes as it pleases; what it risks is that head's regard.</summary>
    private void OthersThreaten(World w, string? mine)
    {
        foreach (var a in w.Houses.Values.Where(h => h.Id != mine && !World.Outsider(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            var head = Head(w, a); if (head?.Alive != true || !HardHeads.Any(t => head.NatureTraits.Contains(t))) continue;
            int hulls = FleetSystem.StrengthOf(w, a.Id); if (hulls < ThreatOdds || Hash($"{a.Id}:{w.Turn}:threat") / 7 % OthersThreatenOneIn != 0) continue;
            var target = w.Houses.Values.Where(h => h.Id != a.Id && !World.Outsider(h.Id) && Head(w, h)?.Alive == true && h.PatronId != a.Id && !w.AtWar(h.Id, a.Id)
                    && hulls >= ThreatOdds * Math.Max(1, FleetSystem.StrengthOf(w, h.Id)) && !PolitySystem.Cowed(w, h, a) && RegardOf(w, a, h) <= OthersThreatenAt)
                .OrderBy(h => RegardOf(w, a, h)).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
            if (target != null) Threat(w, a, target);
        }
    }
    public static readonly string[] HardHeads = { "Ruthless", "Cruel", "Arrogant", "Vengeful" };
}
