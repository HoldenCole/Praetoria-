using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>A ground on which one house may sue another: the matter, what the world records that gives rise to it, how plain the
/// law is (3 plain, 2 arguable, 1 doubtful), what turns on it, and which of the two houses the law favours.</summary>
public sealed record Ground(string Matter, string Says, string Record, int Weight, int Stake, string RightId);

/// <summary>
/// The docket (docs/LADDER.md, "The docket"; docs/PLAYTHROUGH.md, Hard: "a docket: the suits for and against the house, with a
/// plea, an advocate, a settlement and a counter-suit; a notice and a defence when you are charged").
/// <para>A suit has a PLAINTIFF and a defendant. The head of a house may BRING one against a house it has a quarrel with, on a
/// ground the world records between the two; it is entered at the year's turn and heard at the end of the year after, exactly as
/// a suit the bench draws up is. A house that is sued may COUNTER-SUE once on the same matter, which doubles what turns on it.</para>
/// A CHARGE another house brings against one of the player's house is not heard the year it is brought: it waits a year, the
/// house is told, and in that year the head may send someone to plead, or admit it and pay the fine, or do nothing. RNG-free.
/// </summary>
public static partial class LadderSystem
{
    public const int BringFeeShare = 5, BringClout = 2, BringRegard = 10, CounterClout = 1, CounterRegard = 5, AdmitLegitimacy = 2, AdmitRegard = 10;
    public const string SuitWonFlag = "suit_brought_won", SuitLostFlag = "suit_brought_lost", ChargeAdmittedFlag = "charge_admitted";
    public const string SuedChar = "sued_by_the_house", AdmittedChar = "admitted_a_charge";
    public const string Admitted = "admitted", Proved = "proved", LetGo = "let_go";

    private static string SuedKey(string houseId) => "sued_in:" + houseId;
    private static string SuitId(World w, string plaintiffId, string defendantId, string matter) => $"suit_{w.Turn}_{plaintiffId}_{defendantId}_{matter}";

    /// <summary>The house that brought a suit. A case made before suits had a plaintiff was the other house's, where it names the
    /// player's.</summary>
    public static string PlaintiffOf(World w, OfficeBusiness b) =>
        b.PlaintiffId.Length > 0 ? b.PlaintiffId : w.Protagonist?.HouseId is { } mine && (b.HouseA == mine || b.HouseB == mine) ? OtherIn(b, mine) : b.HouseA;
    /// <summary>The filing fee on a suit for this much: a fifth of it, and never less than a hundred credits.</summary>
    public static int FeeOf(int stake) => Math.Max(1, stake / BringFeeShare);
    /// <summary>The year at whose end a suit or a charge is heard: the one after it was entered.</summary>
    public static string HeardSays(World w, int entered) => entered + 1 <= w.Turn ? "at the end of this year" : "at the end of next year";
    /// <summary>What a praetor is known for, in three words or so.</summary>
    public static string NatureOf(Character j) => j.NatureTraits.Contains("Honorable") || j.NatureTraits.Contains("Just") ? "upright"
        : j.NatureTraits.Contains("Ruthless") || j.NatureTraits.Contains("Cruel") || j.Ambition == "amass_a_fortune" ? "known to be for sale" : "neither upright nor for sale";

    /// <summary>A fee goes to the purse of the Reach, which the house that holds the capital keeps.</summary>
    private static void PayFee(World w, House payer, int fee)
    {
        payer.Treasury.Credits -= fee;
        if (RegionSystem.CapitalHolder(w) is { } chair && chair.Id != payer.Id) chair.Treasury.Credits += fee;
    }

    // ---------------- the grounds of a suit ----------------

    private static string? SharedRegion(World w, string a, string b)
    {
        if (w.Galaxy == null) return null;
        HashSet<string> Of(string id) => w.HoldingsOf(id).Where(h => h.IsPlanet).Select(h => w.Galaxy.System(h.SystemId)?.RegionId ?? "").Where(r => r.Length > 0).ToHashSet();
        var theirs = Of(b); string? region = Of(a).Where(theirs.Contains).OrderBy(r => r, StringComparer.Ordinal).FirstOrDefault();
        return region == null ? null : w.Galaxy.Region(region)?.Name ?? region;
    }
    private static string? Tenure(World w, House a, House b)
    {
        foreach (var h in w.Holdings.Values.Where(h => h.OwnerId == a.Id || h.OwnerId == b.Id).OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            string other = h.OwnerId == a.Id ? b.Id : a.Id; var p = h.Provinces.FirstOrDefault(p => p.HolderId == other);
            if (p != null) return $"{w.House(other)?.Name} holds {p.Name} on {w.House(h.OwnerId)?.Name}'s {h.Name}";
        }
        return null;
    }
    private static string? Kinship(World w, House a, House b)
    {
        bool Of(Character c, string houseId) => c.BornHouseId == houseId || w.Char(c.MotherId)?.HouseId == houseId || w.Char(c.FatherId)?.HouseId == houseId;
        var kin = w.LivingCharacters().Where(c => (c.HouseId == a.Id && Of(c, b.Id)) || (c.HouseId == b.Id && Of(c, a.Id))).OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
        return kin == null ? null : $"{kin.Name} of {w.House(kin.HouseId)?.Name} is of {(kin.HouseId == a.Id ? b.Name : a.Name)}'s blood";
    }

    /// <summary>What one house could sue another over this year. There must be a quarrel between the two (a war, or heads who
    /// think ill of each other: <see cref="QuarrelHeat"/>); then every matter the world records between them is a ground, and the
    /// standing quarrel itself is one. Each ground has its own facts: how plain the law is, what turns on it, and whom it favours.</summary>
    public static List<Ground> Grounds(World w, House? plaintiff, House? defendant)
    {
        var list = new List<Ground>(); if (plaintiff == null || defendant == null || plaintiff.Id == defendant.Id || Heat(w, plaintiff, defendant) < QuarrelHeat) return list;
        void Add(string matter, string record)
        {
            if (list.Any(g => g.Matter == matter)) return;
            string id = SuitId(w, plaintiff.Id, defendant.Id, matter); var (weight, stake) = Weigh(id);
            list.Add(new Ground(matter, Matters.First(m => m.id == matter).says, record, weight, stake, Hash(id + ":right") % 2 == 0 ? plaintiff.Id : defendant.Id));
        }
        if (SharedRegion(w, plaintiff.Id, defendant.Id) is { } region) Add("boundary", $"both houses hold worlds in {region}");
        if (plaintiff.Debt > 0 && plaintiff.LenderId == defendant.Id) Add("debt", $"{plaintiff.Name} owes the bank of {defendant.Name} {Money.Credits(plaintiff.Debt)}");
        else if (defendant.Debt > 0 && defendant.LenderId == plaintiff.Id) Add("debt", $"{defendant.Name} owes the bank of {plaintiff.Name} {Money.Credits(defendant.Debt)}");
        if (Tenure(w, plaintiff, defendant) is { } tenure) Add("contract", tenure);
        if (Kinship(w, plaintiff, defendant) is { } kin) Add("inheritance", kin);
        if (w.AtWar(plaintiff.Id, defendant.Id)) Add("salvage", "the two houses are at war, and ships have been lost");
        else if (w.Battles.LastOrDefault(x => (x.AttackerId == plaintiff.Id && x.DefenderId == defendant.Id) || (x.AttackerId == defendant.Id && x.DefenderId == plaintiff.Id)) is { } fight)
            Add("salvage", $"the battle of {fight.Place} in year {fight.Turn}");
        Add(Matters[(int)(Hash($"suit_{w.Turn}_{plaintiff.Id}_{defendant.Id}") % (uint)Matters.Length)].id, "the standing quarrel between the two heads");
        return list;
    }
    /// <summary>How the law stands on a ground for the house that would bring it, in words.</summary>
    public static string LawOn(Ground g, string houseId) =>
        (g.Weight == Plain ? "The law, read plainly, is " : g.Weight == Arguable ? "The law is arguable, and leans " : "The law is doubtful. If anything it leans ") + (g.RightId == houseId ? "with you" : "against you") + ".";

    /// <summary>The praetor who would hear a suit between two houses: one of neither house, the one with the fewest suits waiting,
    /// then the longest sitting.</summary>
    public static Character? Bench(World w, string houseA, string houseB) =>
        w.Magistrates.Where(m => m.OfficeId == Praetor && m.HouseId != houseA && m.HouseId != houseB && w.Char(m.CharacterId) is { Alive: true } c && c.HouseId != houseA && c.HouseId != houseB)
            .OrderBy(m => w.OfficeWork.Count(x => x.Held && x.Choice.Length == 0 && x.HolderId == m.CharacterId)).ThenBy(m => m.SinceTurn).ThenBy(m => m.CharacterId, StringComparer.Ordinal)
            .Select(m => w.Char(m.CharacterId)).FirstOrDefault();

    // ---------------- bringing a suit ----------------

    public static string? BringRefusal(World w, Character? head, House? target, string matter)
    {
        var house = head != null ? w.House(head.HouseId) : null; if (head?.Alive != true || house == null || Economy.HeadOf(w, house)?.Id != head.Id) return "only the head of a house brings a suit for it";
        if (!PolitySystem.InReach(w, house.Id)) return "the courts of the Reach are not yours to use";
        if (target == null || target.Id == house.Id || World.Outsider(target.Id) || !PolitySystem.InReach(w, target.Id) || Economy.HeadOf(w, target) == null) return "there is no such house to bring before the courts of the Reach";
        if (w.Counter(SuedKey(house.Id)) == w.Turn + 1) return "your house has brought a suit this year";
        if (w.OfficeWork.Any(b => b.Kind == Case && b.Held && b.Choice.Length == 0 && ((b.HouseA == house.Id && b.HouseB == target.Id) || (b.HouseA == target.Id && b.HouseB == house.Id))))
            return $"a suit between your house and {target.Name} is already waiting to be heard";
        if (Heat(w, house, target) < QuarrelHeat)
            return $"you have no quarrel with {target.Name} that a court would hear: it takes a war, or two heads who think ill of each other (their regard and yours must come to {-QuarrelHeat} or worse, and they come to {Regard(w, house, target) + Regard(w, target, house)})";
        var ground = Grounds(w, house, target).FirstOrDefault(g => g.Matter == matter); if (ground == null) return "there is no such ground between the two houses";
        if (Bench(w, house.Id, target.Id) == null) return "no praetor of a third house sits to hear it";
        int fee = FeeOf(ground.Stake);
        if (house.Treasury.Credits < fee) return $"the filing fee is {Money.Credits(fee)} and the house has {Money.Held(house.Treasury)}";
        return house.Treasury.Influence >= BringClout ? null : $"needs {BringClout} clout and the house has {house.Treasury.Influence}";
    }

    /// <summary>The suit is brought: the fee and the clout are paid, the other head takes it ill, and it is entered to be heard at
    /// the end of next year.</summary>
    public static OfficeBusiness Bring(World w, Character head, House target, string matter)
    {
        var house = w.House(head.HouseId)!; var g = Grounds(w, house, target).First(x => x.Matter == matter); var judge = Bench(w, house.Id, target.Id)!;
        var b = new OfficeBusiness
        {
            Id = SuitId(w, house.Id, target.Id, matter), RungId = Praetor, HolderId = judge.Id, Kind = Case, Turn = w.Turn, HouseA = house.Id, HouseB = target.Id,
            RightId = g.RightId, Matter = g.Matter, Weight = g.Weight, Stake = g.Stake, Held = true, PlaintiffId = house.Id
        };
        w.OfficeWork.Add(b); PayFee(w, house, FeeOf(g.Stake)); house.Treasury.Influence -= BringClout;
        Shift(w, target, house, -BringRegard); w.WorldCounters[SuedKey(house.Id)] = w.Turn + 1; w.WorldCounters["suits_brought"] = w.Counter("suits_brought") + 1;
        w.Log($"{house.Name} brings a suit against {target.Name} over {MatterOf(b)}: {Money.Credits(g.Stake)} turn on it. {judge.Name}, praetor, hears it at the end of next year. {LawFor(w, b, house.Id)}");
        return b;
    }

    // ---------------- the counter-suit ----------------

    public static string? CounterRefusal(World w, Character? head, OfficeBusiness? b)
    {
        var house = head != null ? w.House(head.HouseId) : null; if (head?.Alive != true || house == null || Economy.HeadOf(w, house)?.Id != head.Id) return "only the head of a house brings a counter-suit";
        if (b == null || !b.Held || b.Kind != Case || b.Choice.Length > 0 || (b.HouseA != house.Id && b.HouseB != house.Id)) return "no such suit is waiting";
        if (PlaintiffOf(w, b) == house.Id) return "it is your own suit: there is nothing to counter";
        if (b.Countered) return "a counter-suit has been brought already";
        if (WeightOf(b) == Plain && b.RightId != house.Id) return "the law, read plainly, is against you: no advocate would put the house's name to a counter-suit";
        int fee = FeeOf(StakeOf(b));
        if (house.Treasury.Credits < fee) return $"the filing fee is {Money.Credits(fee)} and the house has {Money.Held(house.Treasury)}";
        return house.Treasury.Influence >= CounterClout ? null : $"needs {CounterClout} clout and the house has {house.Treasury.Influence}";
    }

    /// <summary>The defendant brings its own claim on the same matter: what turns on the suit is doubled, and whoever loses pays
    /// all of it. Terms out of court are half of the doubled sum.</summary>
    public static void Counter(World w, Character head, OfficeBusiness b)
    {
        var house = w.House(head.HouseId)!; var plaintiff = w.House(PlaintiffOf(w, b));
        PayFee(w, house, FeeOf(StakeOf(b))); house.Treasury.Influence -= CounterClout;
        b.PlaintiffId = plaintiff?.Id ?? b.PlaintiffId; b.Stake = StakeOf(b) * 2; b.Countered = true; Shift(w, plaintiff, house, -CounterRegard);
        w.WorldCounters["counter_suits"] = w.Counter("counter_suits") + 1;
        w.Log($"{house.Name} answers {plaintiff?.Name}'s suit over {MatterOf(b)} with a counter-suit. {Money.Credits(StakeOf(b))} now turn on it, and whoever loses pays it.");
    }

    /// <summary>At the turn of the year in which the player's house brought a suit, the house it sued answers: with a counter-suit,
    /// if the law leans its way and is not doubtful, and it has the fee and the clout.</summary>
    private static void Answered(World w)
    {
        var mine = w.Protagonist?.HouseId; if (mine == null) return;
        foreach (var b in w.OfficeWork.Where(b => b.Held && b.Choice.Length == 0 && b.Kind == Case && b.PlaintiffId == mine && b.Turn == w.Turn && !b.Countered).OrderBy(b => b.Id, StringComparer.Ordinal).ToList())
        {
            var defendant = w.House(OtherIn(b, mine)); var head = defendant != null ? Economy.HeadOf(w, defendant) : null;
            if (defendant == null || head == null || b.RightId != defendant.Id || WeightOf(b) == Doubtful || CounterRefusal(w, head, b) != null) continue;
            Counter(w, head, b);
            w.Tell("warn", $"{defendant.Name} answers your suit over {MatterOf(b)} with a counter-suit: {Money.Credits(StakeOf(b))} now turn on it, and whoever loses pays it. {LawFor(w, b, mine)}", "senate:ladder");
        }
    }

    /// <summary>A suit that named the player's house is decided: one the house brought itself is marked apart, with the head it sued.</summary>
    private static void Ruled(World w, OfficeBusiness b, bool won)
    {
        var mine = w.Protagonist?.HouseId;
        if (mine == null || b.PlaintiffId != mine) { w.WorldFlags.Add(won ? "case_won" : "case_lost"); return; }
        Mark(w, won ? SuitWonFlag : SuitLostFlag);
        foreach (var c in w.Characters.Values) c.Flags.Remove(SuedChar);   // one such report at a time: it is about the head last sued
        if (w.House(OtherIn(b, mine)) is { } defendant && Economy.HeadOf(w, defendant) is { } head) head.Flags.Add(SuedChar);
    }

    // ---------------- a charge against one of the player's house ----------------

    /// <summary>The charges against a house's people that wait to be heard.</summary>
    public static List<Indictment> ChargesOn(World w, string houseId) =>
        w.Indictments.Where(c => c.Outcome.Length == 0 && c.HouseId == houseId).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
    public static bool UnderCharge(World w, string deedId) => w.Indictments.Any(c => c.Outcome.Length == 0 && c.DeedId == deedId);
    public static OfficeDeed? DeedOf(World w, Indictment c) => w.OfficeRecord.FirstOrDefault(d => d.Id == c.DeedId);
    /// <summary>What a pleader brings to a defence: their law and oratory, and the bar retained behind the house's advocate.</summary>
    public static int DefenceOf(World w, Character? pleader) =>
        pleader is not { Alive: true } ? 0 : Craft(pleader) + (HouseholdSystem.IsAdvocateOf(w, pleader.HouseId, pleader.Id) ? w.Counter(HouseholdSystem.CounselEdgeKey(pleader.HouseId)) : 0);
    /// <summary>Who is to plead a charge, if they still live and are still of the house.</summary>
    public static Character? PleaderOf(World w, Indictment c) => w.Char(c.PleaderId) is { Alive: true } p && p.HouseId == c.HouseId ? p : null;
    /// <summary>The praetor who is to hear a charge: the one named when it was brought while they sit, or whoever would hear it now.</summary>
    public static Character? JudgeOf(World w, Indictment c)
    {
        if (w.Char(c.JudgeId) is { Alive: true } j && Seat(w, j.Id)?.OfficeId == Praetor && j.HouseId != c.HouseId && j.HouseId != c.AccuserId) return j;
        return DeedOf(w, c) is { } d ? Court(w, d, c.AccuserId) : null;
    }
    /// <summary>In a hundred, how likely a charge is found proved before a praetor who goes by the pleading.</summary>
    public static int ProvedChance(World w, string accuserHouseId, int defence) => Math.Clamp(40 + Craft(Advocate(w, accuserHouseId)) * 6 - defence * 4, 0, 100);
    /// <summary>What a conviction would cost the house, in words.</summary>
    public static string ConvictionSays(World w, Character who) =>
        $"If it is found proved, {who.Name} is put out of any office and may not stand for {BarYears} years, the house pays {Money.Credits(FineCredits)} and loses {ConvictLegitimacy} legitimacy, and its candidates lose votes for {ScandalYears} years.";
    /// <summary>What admitting it would cost, in words.</summary>
    public static string AdmitSays(World w, Character who) =>
        $"The house pays the fine of {Money.Credits(FineCredits)} now and loses {AdmitLegitimacy} legitimacy. There is no trial: {who.Name} keeps what they hold and may stand, and no scandal is laid on the house's candidates.";

    /// <summary>Another house charges one of the player's house: it is not heard this year. It is written down, and the house is told.</summary>
    private static void Indict(World w, OfficeCatalog offices, Character head, OfficeDeed d)
    {
        var accuser = w.House(head.HouseId)!; var judge = Court(w, d, accuser.Id)!; var who = w.Char(d.CharacterId)!; var accused = w.House(d.HouseId);
        w.Indictments.Add(new Indictment { Id = "charge_" + d.Id, DeedId = d.Id, AccuserId = accuser.Id, AccusedId = who.Id, HouseId = d.HouseId, JudgeId = judge.Id, Turn = w.Turn });
        w.WorldCounters[TriedKey(accuser.Id)] = w.Turn + 1;
        w.Log($"{accuser.Name} charges {who.Name} of {accused?.Name}, who {DeedSays(w, offices, d)}. {judge.Name}, praetor, hears it at the year's end.");
        w.Tell("warn", $"{accuser.Name} charges {who.Name} of your house, who {DeedSays(w, offices, d)}. {judge.Name}, praetor, who is {Verdict(w, d, accuser.Id, judge, 0).why}, hears it at the end of the year. {ConvictionSays(w, who)} Until then you may send someone to plead, or admit it and pay the fine.", "senate:ladder", who.Id);
    }

    /// <summary>The charges held from last year are heard.</summary>
    private static void HearCharges(World w, OfficeCatalog offices)
    {
        w.Indictments.RemoveAll(c => c.Outcome.Length > 0 && c.Turn < w.Turn - 1);
        foreach (var c in w.Indictments.Where(c => c.Outcome.Length == 0 && c.Turn < w.Turn).OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
        {
            var d = DeedOf(w, c); var who = w.Char(c.AccusedId); var accuser = w.House(c.AccuserId); var head = accuser != null ? Economy.HeadOf(w, accuser) : null;
            var judge = d != null ? JudgeOf(w, c) : null;
            if (d == null || d.Answered || who is not { Alive: true } || who.HouseId != c.HouseId || head == null || judge == null)
            {
                c.Outcome = Lapsed; string name = who?.Name ?? "one of the house";
                w.Log($"The charge against {name} lapses: {(judge == null && d != null ? "no praetor sits who could hear it" : "there is nobody left to bring it, or to answer it")}.");
                if (c.HouseId == w.Protagonist?.HouseId) w.Tell("info", $"The charge against {name} has lapsed and will not be heard.", "senate:ladder");
                continue;
            }
            Prosecute(w, offices, head, d, judge, c);
        }
    }

    public static string? ChargePleadRefusal(World w, Character? head, Indictment? c, Character? pleader)
    {
        var house = head != null ? w.House(head.HouseId) : null; if (head?.Alive != true || house == null || Economy.HeadOf(w, house)?.Id != head.Id) return "only the head of a house sends its pleader";
        if (c == null || c.Outcome.Length > 0 || c.HouseId != house.Id) return "no such charge is waiting";
        if (pleader is not { Alive: true } || pleader.HouseId != house.Id || pleader.Age < DynastySystem.AgeOfMajority) return "there is no one of age in the house to send";
        return c.PleaderId == pleader.Id ? "they are already to plead it" : null;
    }
    public static void SendDefender(World w, Character head, Indictment c, Character pleader)
    {
        c.PleaderId = pleader.Id; var who = w.Char(c.AccusedId);
        w.Log($"{pleader.Name} is to answer the charge against {(who?.Id == pleader.Id ? "themselves" : who?.Name)} for {w.House(head.HouseId)?.Name} (law and oratory {DefenceOf(w, pleader)}; {w.House(c.AccuserId)?.Name}'s best has {Craft(Advocate(w, c.AccuserId))}).");
    }
    /// <summary>What sending this pleader would do, in words, for the docket.</summary>
    public static string ChargePleadSays(World w, Indictment c, Character pleader)
    {
        var d = DeedOf(w, c); var judge = JudgeOf(w, c); int ours = DefenceOf(w, pleader); if (d == null || judge == null) return $"law and oratory {ours}";
        string why = Verdict(w, d, c.AccuserId, judge, ours).why;
        return $"law and oratory {ours}: " + (why.StartsWith("upright", StringComparison.Ordinal) ? "the praetor is upright and will go by the record, and pleading will not move it"
            : why.StartsWith("bought", StringComparison.Ordinal) ? "the praetor has been bought, and pleading will not move it"
            : why.StartsWith("a friend", StringComparison.Ordinal) ? "the praetor is a friend of your house and will let them go, whoever pleads"
            : why.StartsWith("known", StringComparison.Ordinal) ? $"the praetor is for sale and will find for the richer house; it would take {AdvocateHolds} to keep the verdict from being sold"
            : $"against their {Craft(Advocate(w, c.AccuserId))}, about {ProvedChance(w, c.AccuserId, ours)} in a hundred that it is found proved ({ProvedChance(w, c.AccuserId, 0)} with nobody to answer)");
    }

    public static string? AdmitRefusal(World w, Character? head, Indictment? c)
    {
        var house = head != null ? w.House(head.HouseId) : null; if (head?.Alive != true || house == null || Economy.HeadOf(w, house)?.Id != head.Id) return "only the head of a house answers a charge for it";
        var who = c != null ? w.Char(c.AccusedId) : null;
        if (c == null || c.Outcome.Length > 0 || c.HouseId != house.Id || who is not { Alive: true } || DeedOf(w, c) == null) return "no such charge is waiting";
        if (who.Id != head.Id && CourtshipSystem.Wilful(who) && w.FamilyAutonomy > 0) return $"{who.Name} will not admit it: it is their name, and they mean to be heard";
        return house.Treasury.Credits >= FineCredits ? null : $"the fine is {Money.Credits(FineCredits)} and the house has {Money.Held(house.Treasury)}";
    }

    /// <summary>The charge is admitted before it is heard: the fine is paid in full, the house loses less standing than a conviction
    /// would cost it, and there is no trial, no bar from office and no scandal.</summary>
    public static void Admit(World w, OfficeCatalog offices, Character head, Indictment c)
    {
        var d = DeedOf(w, c)!; _trustOffice = d.RungId; _trustRegion = HomeRegion(w, d.HouseId) ?? "";
        try
        {
            var house = w.House(head.HouseId)!; var who = w.Char(c.AccusedId)!; var accuser = w.House(c.AccuserId);
            house.Treasury.Credits -= FineCredits;
            if (w.House(d.AgainstId) is { } wronged && wronged.Id != house.Id) wronged.Treasury.Credits += FineCredits; else if (RegionSystem.CapitalHolder(w) is { } chair && chair.Id != house.Id) chair.Treasury.Credits += FineCredits;
            house.Legitimacy = Math.Clamp(house.Legitimacy - AdmitLegitimacy, 0, 100); d.Answered = true; c.Outcome = Admitted; MoveTrust(w, 1); Shift(w, house, accuser, -AdmitRegard);
            w.WorldCounters["charges_admitted"] = w.Counter("charges_admitted") + 1;
            LifeSystem.Note(w, who, "office", "Admitted what they did in office before it was heard, and the house paid for it.");
            Judged(w, "", c.AccuserId, house.Id, FineCredits, null, $"{who.Name} {DeedSays(w, offices, d)}: admitted before it was heard, and the fine paid", "charge");
            if (who.Id != head.Id) { foreach (var x in w.Characters.Values) x.Flags.Remove(AdmittedChar); who.Flags.Add(AdmittedChar); Mark(w, ChargeAdmittedFlag); }
            w.Log($"{house.Name} admits the charge {accuser?.Name} brought: {who.Name} {DeedSays(w, offices, d)}. The fine of {Money.Credits(FineCredits)} is paid, and it is not heard.");
        }
        finally { TrustDone(); }
    }
}
