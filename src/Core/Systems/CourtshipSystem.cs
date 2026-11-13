using Praetoria.Core.Progression;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Love, courting and marriage (docs/MARRIAGE.md). A match can still be struck in a season by treaty (the old proposal), but it
/// can also be <em>courted</em> over years: the courted person's <see cref="Courtship.Favour"/> and their family's
/// <see cref="Courtship.Consent"/> are won separately, a rival may press the same suit, and the contract (the dowry, whose name
/// the children carry) is bargained when the match is proposed. A courtship lets a house marry above its station, because a
/// person who wants the match argues for it at home. Afterwards every marriage has a <see cref="Marriage.Warmth"/> that moves
/// with the years: warm marriages bear more children and ease their partners, cold ones the opposite, and the coldest live apart.
/// <para>RNG-free: chance is an FNV hash of ids and the year, so a save replays exactly.</para>
/// </summary>
public static class CourtshipSystem
{
    public const int MaxCourtships = 3, AcceptAt = 50, LoveAt = 60, ElopeAt = 75, UnwillingBelow = 15, RivalWinsAt = 60, LapseYears = 4, SuitLapseYears = 2,
        GiftCredits = 12, WarmAt = 65, ColdAt = 30, EstrangedAt = 15, ReconciledAt = 30, BetrothalAge = 6, Adult = DynastySystem.AgeOfMajority;
    public const string None = "none", Customary = "customary", Rich = "rich";
    public const string Love = "love", Advantage = "advantage", Arranged = "arranged";
    public const string ActTime = "time", ActGift = "gift", ActFamily = "family", ActRival = "rival";

    // world flags re-reckoned every year (content reads them)
    public const string CourtingFlag = "courting", RivalFlag = "courtship_rival", SuitFlag = "suit_received", BetrothedFlag = "child_betrothed", MarriedFlag = "married",
        WarmFlag = "marriage_warm", ColdFlag = "marriage_cold", EstrangedFlag = "estranged";
    // world flags raised by a deed, kept through the next events phase
    public const string JustWedFlag = "just_wed", LoveMatchFlag = "love_match", JiltedFlag = "jilted", ElopedFlag = "eloped", DivorcedFlag = "divorced",
        BetrothalBrokenFlag = "betrothal_broken", ExposedFlag = "undermining_exposed", WidowedFlag = "widowed_now", SuitRefusedFlag = "suit_refused";
    // character flags re-reckoned every year, so an event can bind the person
    public const string CourtedChar = "courted", RivalChar = "rival_suitor", WidowedChar = "widowed";
    public const string WarmthCounter = "marriage_warmth", StandingCounter = "courtship_standing", OpenedCounter = "marriages_opened";
    private static readonly string[] TickFlags = { CourtingFlag, RivalFlag, SuitFlag, BetrothedFlag, MarriedFlag, WarmFlag, ColdFlag, EstrangedFlag };
    private static readonly string[] DeedFlags = { JustWedFlag, LoveMatchFlag, JiltedFlag, ElopedFlag, DivorcedFlag, BetrothalBrokenFlag, ExposedFlag, WidowedFlag, SuitRefusedFlag };

    private static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }
    private static uint Hash(string s) { uint h = 2166136261; foreach (char ch in s) h = (h ^ ch) * 16777619; return h; }
    private static int Rank(World w, string houseId) => Title.RankOf(w.House(houseId)?.Title ?? "");
    private static void Sour(World w, Character? from, Character? to, int delta)
    {
        if (from == null || to == null || from.Id == to.Id) return;
        var r = w.Relationship(from.Id, to.Id); r.Disposition = Math.Clamp(r.Disposition + delta, -100, 100);
    }
    private static Character? Head(World w, string houseId) => w.House(houseId) is { } h ? Economy.HeadOf(w, h) : null;

    public static string KindName(string kind) => kind switch { Love => "a match for love", Advantage => "a match of advantage", _ => "an arranged match" };
    public static string WarmthName(Marriage m) => m.Estranged ? "living apart" : m.Warmth >= 80 ? "devoted" : m.Warmth >= WarmAt ? "warm" : m.Warmth > ColdAt ? "settled" : m.Warmth > EstrangedAt ? "cold" : "bitter";

    // ---------------- who may be matched ----------------

    public static bool Promised(World w, string id) => w.Courtships.Any(c => c.Betrothed && (c.SuitorId == id || c.TargetId == id));
    public static bool IsChildMatch(World w, Courtship c) => (w.Char(c.SuitorId)?.Age ?? 0) < Adult || (w.Char(c.TargetId)?.Age ?? 0) < Adult;

    /// <summary>Why these two cannot be courted or promised to each other, or null. Children of six and over may be promised.</summary>
    public static string? Bar(World w, Character? s, Character? t)
    {
        if (s?.Alive != true || t?.Alive != true) return "no such person";
        if (s.Id == t.Id || s.HouseId == t.HouseId) return "too close";
        if (DynastySystem.AreKin(w, s, t)) return "too close in blood";
        if (s.Sex == "" || t.Sex == "" || s.Sex == t.Sex) return "no heirs could come of it";
        if (s.Age < BetrothalAge || t.Age < BetrothalAge) return "too young even to be promised";
        if (DynastySystem.Spouse(w, s) != null || DynastySystem.Spouse(w, t) != null) return "already wed";
        if (w.AtWar(s.HouseId, t.HouseId)) return "not while your houses are at war";
        if (s.Flags.Contains(DynastySystem.HiddenFlag) || t.Flags.Contains(DynastySystem.HiddenFlag)) return "they cannot be found";
        if (Promised(w, s.Id) || Promised(w, t.Id)) return "already promised";
        if (Math.Abs(s.Age - t.Age) > 30) return "too many years between them";
        return null;
    }

    /// <summary>What there is between these two before anything is done about it: fixed for the pair, −10 to +20.</summary>
    public static int Spark(string a, string b) => (int)(Hash(string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a) % 31) - 10;

    /// <summary>What the courted person feels for the suitor on the first day.</summary>
    public static int StartFavour(World w, Character s, Character t)
    {
        int v = 25 + Spark(s.Id, t.Id) + (w.GetRelationship(t.Id, s.Id)?.Disposition ?? 0) / 3 + s.Skill("charisma") * 3;
        if (w.House(s.HouseId)?.Culture == w.House(t.HouseId)?.Culture) v += 5;
        v -= Math.Max(0, Math.Abs(s.Age - t.Age) - 8);
        if (t.Ambition == "win_a_great_love") v += 6;
        if (s.NatureTraits.Contains("Honorable")) v += 3;
        if (s.NatureTraits.Contains("Cruel")) v -= 8;
        if (s.NatureTraits.Contains("Arrogant")) v -= 4;
        return Math.Clamp(v, 0, 70);
    }

    /// <summary>What the courted person's family thinks of the suitor's house on the first day.</summary>
    public static int StartConsent(World w, Character s, Character t)
    {
        var ours = w.House(s.HouseId); var theirs = w.House(t.HouseId); if (ours == null || theirs == null) return 0;
        var ourHead = Economy.HeadOf(w, ours); var theirHead = Economy.HeadOf(w, theirs);
        int v = 30;
        if (ourHead != null && theirHead != null) v += (w.GetRelationship(theirHead.Id, ourHead.Id)?.Disposition ?? 0) / 3;
        v += Math.Clamp((ours.Legitimacy - theirs.Legitimacy) / 2, -20, 20);
        v += Math.Clamp((Rank(w, ours.Id) - Rank(w, theirs.Id)) * 6, -18, 18);
        v += ours.Wealth >= theirs.Wealth ? 8 : ours.Wealth * 2 < theirs.Wealth ? -8 : 0;
        if (ours.PatronId == theirs.Id || theirs.PatronId == ours.Id) v += 15;
        if (ours.Bloc.Length > 0 && ours.Bloc == theirs.Bloc) v += 5;
        if (ourHead?.Id == s.Id) v += 6;
        v += Math.Min(10, s.CareerRank * 2);
        if (ours.Origin == "new" && theirs.Origin == "old") v -= 8;
        return Math.Clamp(v, 0, 80);
    }

    /// <summary>How far a suit has got: the person and the family together, or the family alone where a child is promised.</summary>
    public static int Standing(World w, Courtship c) => IsChildMatch(w, c) ? c.Consent : (c.Favour + c.Consent) / 2;

    /// <summary>Everyone the suitor could court or be promised to, with where the suit would start. Best first.</summary>
    public static List<(Character person, int favour, int consent)> Prospects(World w, Character suitor) =>
        w.LivingCharacters().Where(t => t.HouseId != suitor.HouseId && t.HouseId != PirateSystem.HouseId && Bar(w, suitor, t) == null && !w.Courtships.Any(c => c.SuitorId == suitor.Id && c.TargetId == t.Id))
            .Select(t => (t, f: t.Age < Adult || suitor.Age < Adult ? 0 : StartFavour(w, suitor, t), k: StartConsent(w, suitor, t)))
            .OrderByDescending(x => CanBear(suitor, x.t)).ThenByDescending(x => x.f + x.k).ThenBy(x => x.t.Id, StringComparer.Ordinal).Select(x => (x.t, x.f, x.k)).ToList();

    /// <summary>Are children likely of this match? Not when the woman is within five years of the end of her bearing years, or past them.</summary>
    public static bool CanBear(Character a, Character b) => (a.Sex == "female" ? a : b).Age <= DynastySystem.FertileMax - 5;

    // ---------------- courting ----------------

    public static string? BeginRefusal(World w, Character? head, Character? s, Character? t)
    {
        if (head?.Alive != true || s == null || s.HouseId != head.HouseId) return "not of your house";
        if (w.Courtships.Any(c => c.SuitorId == s.Id && !c.Incoming)) return $"{s.Name} is already paying court to someone";
        if (w.Courtships.Count(c => !c.Incoming) >= MaxCourtships) return "the house has as many suits in hand as it can manage";
        return Bar(w, s, t);
    }

    public static Courtship Begin(World w, Character s, Character t, int favourBonus = 0)
    {
        int n = w.Counter("courtships") + 1; w.WorldCounters["courtships"] = n;
        bool child = s.Age < Adult || t.Age < Adult;
        var c = new Courtship { Id = "court_" + n, SuitorId = s.Id, TargetId = t.Id, Since = w.Turn, Favour = child ? 0 : Math.Clamp(StartFavour(w, s, t) + favourBonus, 0, 100), Consent = StartConsent(w, s, t) };
        w.Courtships.Add(c);
        w.Log(child ? $"{w.House(s.HouseId)?.Name} sounds out {w.House(t.HouseId)?.Name} about promising {s.Name} to {t.Name}."
                    : $"{s.Name} begins to pay court to {t.Name} of {w.House(t.HouseId)?.Name}.");
        return c;
    }

    public static string? ActRefusal(World w, Courtship? c, string act)
    {
        if (c == null || c.Incoming) return "no such suit";
        if (c.Betrothed) return "the match is agreed";
        if (c.ActedTurn >= w.Turn) return "one thing a year; let it rest until the next";
        bool child = IsChildMatch(w, c);
        return act switch
        {
            ActTime => child ? "they are a child; this is for the families" : null,
            ActGift => (w.TreasuryOf(c.SuitorId)?.Credits ?? 0) >= GiftCredits ? null : "the house cannot spare the credits",
            ActFamily => null,
            ActRival => c.RivalId.Length > 0 ? null : "no rival presses the suit",
            _ => "no such thing to do"
        };
    }

    /// <summary>The year's one act for a courtship. Returns a line saying what came of it.</summary>
    public static string Act(World w, Courtship c, string act)
    {
        var s = w.Char(c.SuitorId)!; var t = w.Char(c.TargetId)!; c.ActedTurn = w.Turn; string line;
        switch (act)
        {
            case ActTime:
                int gain = 6 + s.Skill("charisma") * 2 + (Spark(s.Id, t.Id) > 8 ? 3 : 0);
                c.Favour = Math.Min(100, c.Favour + gain);
                line = $"{s.Name} spent what time the year allowed with {t.Name}."; break;
            case ActGift:
                w.TreasuryOf(s.Id)!.Credits -= GiftCredits;
                if (!IsChildMatch(w, c)) c.Favour = Math.Min(100, c.Favour + 5);
                c.Consent = Math.Min(100, c.Consent + 4);
                line = $"A gift went from {w.House(s.HouseId)?.Name} to {t.Name}'s family, and was noticed."; break;
            case ActFamily:
                var head = Head(w, s.HouseId) ?? s;
                c.Consent = Math.Min(100, c.Consent + 6 + Math.Max(head.Skill("oratory"), head.Skill("diplomacy")) * 2);
                line = $"{head.Name} made the case for the match to {w.House(t.HouseId)?.Name}."; break;
            default:
                var actor = Head(w, s.HouseId) ?? s; var rival = w.Char(c.RivalId);
                c.RivalStanding = Math.Max(0, c.RivalStanding - 10 - actor.Skill("intrigue") * 3);
                bool exposed = Hash(c.Id + ":undermine:" + w.Turn) % 100 < Math.Max(5, 35 - actor.Skill("intrigue") * 6);
                if (exposed)
                {
                    c.Consent = Math.Max(0, c.Consent - 10); Sour(w, rival, s, -25); Mark(w, ExposedFlag);
                    line = $"Word was put about against {rival?.Name}'s suit, and it was traced to {w.House(s.HouseId)?.Name}. {t.Name}'s family did not like it.";
                }
                else line = $"Word was put about against {rival?.Name}'s suit. It did the suit no good.";
                break;
        }
        w.Log(line); return line;
    }

    // ---------------- the contract ----------------

    /// <summary>What custom expects to pass with a bride of this house.</summary>
    public static int CustomaryDowry(World w, string brideHouseId) => Math.Clamp(8 + Rank(w, brideHouseId) * 6 + (w.House(brideHouseId)?.Wealth ?? 0) / 20, 8, 60);
    public static int DowryAmount(World w, string brideHouseId, string level) => level == Rich ? CustomaryDowry(w, brideHouseId) * 2 : level == Customary ? CustomaryDowry(w, brideHouseId) : 0;

    /// <summary>Whose name the children would carry with nothing agreed: the head of a house keeps the seat's children, otherwise the
    /// greater house does, otherwise the father's.</summary>
    public static string DefaultNameHouse(World w, Character x, Character y)
    {
        var mother = x.Sex == "female" ? x : y; var father = x.Sex == "female" ? y : x;
        bool motherKeeps = mother.Id == w.ProtagonistId || (father.Id != w.ProtagonistId && Rank(w, mother.HouseId) > Rank(w, father.HouseId));
        return motherKeeps ? mother.HouseId : father.HouseId;
    }

    private static (Character ours, Character theirs) Sides(World w, Courtship c)
    {
        var s = w.Char(c.SuitorId)!; var t = w.Char(c.TargetId)!; return c.Incoming ? (t, s) : (s, t);
    }

    /// <summary>What the terms asked add to or take from the other family's willingness.</summary>
    public static int Terms(World w, Courtship c, string dowry, string childrenTo)
    {
        var (ours, theirs) = Sides(w, c); bool weReceive = theirs.Sex == "female"; int v = 0;
        v += weReceive ? dowry switch { None => 8, Rich => -14, _ => 0 } : dowry switch { None => -8, Rich => 12, _ => 0 };
        string custom = DefaultNameHouse(w, ours, theirs);
        if (childrenTo == ours.HouseId && custom != ours.HouseId) v -= 15;
        if (childrenTo == theirs.HouseId && custom == ours.HouseId) v += 15;
        return v;
    }

    /// <summary>Would the other family agree to the match on these terms? With the reckoning and, if not, why.</summary>
    public static (bool ok, int score, string why) Answer(World w, Courtship c, string dowry, string childrenTo)
    {
        var (ours, theirs) = Sides(w, c); bool child = IsChildMatch(w, c); bool weReceive = theirs.Sex == "female";
        if (!weReceive && (w.TreasuryOf(ours.Id)?.Credits ?? 0) < DowryAmount(w, ours.HouseId, dowry)) return (false, 0, "the house cannot pay such a dowry");
        int score;
        if (c.Incoming)
            score = 62 + Math.Clamp(((w.House(ours.HouseId)?.Legitimacy ?? 0) - (w.House(theirs.HouseId)?.Legitimacy ?? 0)) / 2, -10, 15) + Terms(w, c, dowry, childrenTo);   // they asked: custom's terms are always enough
        else
        {
            if (!child && c.Favour < UnwillingBelow) return (false, 0, $"{theirs.Name} will not have it, whatever the family says");
            score = c.Consent + (child ? 0 : c.Favour >= LoveAt ? 15 : c.Favour < 25 ? -10 : 0) + Terms(w, c, dowry, childrenTo);
        }
        return score >= AcceptAt ? (true, score, "they will agree") : (false, score, $"they will refuse: the match stands at {score} with them and needs {AcceptAt}");
    }

    /// <summary>Put the match to the other family. Agreed: a wedding, or a betrothal if either is a child. Refused: it costs a little
    /// of their goodwill to have been asked too soon.</summary>
    public static bool Propose(World w, Courtship c, string dowry, string childrenTo)
    {
        var (ok, _, _) = Answer(w, c, dowry, childrenTo); var s = w.Char(c.SuitorId)!; var t = w.Char(c.TargetId)!;
        if (!ok)
        {
            c.Consent = Math.Max(0, c.Consent - 5);
            w.Log($"{w.House(t.HouseId)?.Name} would not agree to the match between {s.Name} and {t.Name} on the terms offered.");
            return false;
        }
        if (IsChildMatch(w, c))
        {
            c.Betrothed = true; c.Dowry = dowry; c.ChildrenTo = childrenTo; c.RivalId = ""; c.RivalStanding = 0;
            w.Log($"{s.Name} of {w.House(s.HouseId)?.Name} is promised to {t.Name} of {w.House(t.HouseId)?.Name}. They will wed when both are of age.");
            return true;
        }
        string kind = c.Incoming ? (c.Favour >= LoveAt ? Love : Arranged) : c.Favour >= LoveAt ? Love : Advantage;
        Wed(w, s, t, kind, dowry, childrenTo, favour: c.Favour);
        return true;
    }

    /// <summary>Refuse a suit another house brought for one of your kin.</summary>
    public static void RefuseSuit(World w, Courtship c)
    {
        var s = w.Char(c.SuitorId); var t = w.Char(c.TargetId); w.Courtships.Remove(c);
        Sour(w, Head(w, s?.HouseId ?? ""), Head(w, t?.HouseId ?? ""), -8);
        if (t != null && c.Favour >= LoveAt) { Sour(w, t, Head(w, t.HouseId), -15); Mark(w, SuitRefusedFlag); }
        w.Log($"{w.House(t?.HouseId ?? "")?.Name} declined {s?.Name}'s suit for {t?.Name}.{(c.Favour >= LoveAt ? $" {t?.Name} had wanted it." : "")}");
    }

    public static string? ElopeRefusal(World w, Courtship? c)
    {
        if (c == null || c.Incoming || c.Betrothed) return "no such suit";
        if (IsChildMatch(w, c)) return "they are a child";
        return c.Favour >= ElopeAt ? null : $"{w.Char(c.TargetId)?.Name} would not leave their family for it (favour {c.Favour} of {ElopeAt})";
    }

    /// <summary>Marry without the family's leave: no dowry, the children yours, their house's anger and some of your standing.</summary>
    public static void Elope(World w, Courtship c)
    {
        var s = w.Char(c.SuitorId)!; var t = w.Char(c.TargetId)!; var ours = w.House(s.HouseId)!;
        Wed(w, s, t, Love, None, s.HouseId, favour: c.Favour);
        Sour(w, Head(w, t.HouseId), Head(w, s.HouseId), -40); ours.Legitimacy -= 4; Mark(w, ElopedFlag);
        w.Log($"{s.Name} and {t.Name} married without {w.House(t.HouseId)?.Name}'s leave. It will not be forgotten there.");
    }

    public static void BreakOff(World w, Courtship c)
    {
        var s = w.Char(c.SuitorId); var t = w.Char(c.TargetId); w.Courtships.Remove(c); if (s == null || t == null) return;
        if (c.Betrothed)
        {
            string? mine = w.Protagonist?.HouseId; var breaker = s.HouseId == mine ? s : t; var other = breaker == s ? t : s;
            if (w.House(breaker.HouseId) is { } bh) bh.Legitimacy -= 3;
            Sour(w, Head(w, other.HouseId), Head(w, breaker.HouseId), -20); Mark(w, BetrothalBrokenFlag);
            w.Log($"{w.House(breaker.HouseId)?.Name} broke the promise between {s.Name} and {t.Name}.");
            return;
        }
        if (c.Favour >= 50) Sour(w, t, s, -10);
        w.Log($"{s.Name} let the suit for {t.Name} drop.");
    }

    // ---------------- weddings ----------------

    private static int StartWarmth(World w, Character a, Character b, string kind, int favour) =>
        Math.Clamp(kind switch
        {
            Love => 62 + Spark(a.Id, b.Id) / 2 + Math.Max(0, favour - LoveAt) / 4,
            Advantage => 46 + Spark(a.Id, b.Id) / 2 + favour / 8,
            _ => 40 + Spark(a.Id, b.Id) / 2 + ((w.GetRelationship(a.Id, b.Id)?.Disposition ?? 0) + (w.GetRelationship(b.Id, a.Id)?.Disposition ?? 0)) / 8
        }, 5, 95);

    /// <summary>The one place a marriage is made: the bond both ways, what rank brings each house, the dowry, and the record.</summary>
    public static Marriage Wed(World w, Character a, Character b, string kind, string dowry = None, string childrenTo = "", int favour = 0, bool log = true)
    {
        foreach (var (x, y) in new[] { (a.Id, b.Id), (b.Id, a.Id) })
        {
            var rel = w.Relationship(x, y); rel.Bond = BondType.Marriage; rel.BondStrength = kind == Love ? 80 : 60;
            rel.Disposition = Math.Clamp(rel.Disposition + (kind == Love ? 30 : 20), -100, 100);
        }
        a.Flags.Add("wed"); b.Flags.Add("wed"); a.Flags.Remove(WidowedChar); b.Flags.Remove(WidowedChar);
        var ha = w.House(a.HouseId); var hb = w.House(b.HouseId);
        if (ha != null && hb != null && ha.Id != hb.Id)
        {
            int ra = Title.RankOf(ha.Title), rb = Title.RankOf(hb.Title);
            if (rb > ra) { ha.Claims.Add(hb.Title); ha.Legitimacy += 5; }
            else if (ra > rb) { hb.Claims.Add(ha.Title); hb.Legitimacy += 5; }
            else { ha.Legitimacy += 2; hb.Legitimacy += 2; }
        }
        string? mine = w.Protagonist?.HouseId; bool ourWedding = mine != null && (a.HouseId == mine || b.HouseId == mine);
        // the counter is the player's own house's matches: an honour and the epilogue read it as "bound to yours"
        if (ourWedding) w.WorldCounters["marriages"] = w.Counter("marriages") + 1;

        var bride = a.Sex == "female" ? a : b; var groom = bride == a ? b : a; int paid = 0;
        if (dowry != None && w.House(bride.HouseId) is { } from && w.House(groom.HouseId) is { } to && from.Id != to.Id)
        {
            paid = Math.Min(DowryAmount(w, from.Id, dowry), Math.Max(0, from.Treasury.Credits));
            from.Treasury.Credits -= paid; to.Treasury.Credits += paid;
        }
        bool first = string.CompareOrdinal(a.Id, b.Id) < 0;
        var m = new Marriage { A = first ? a.Id : b.Id, B = first ? b.Id : a.Id, Since = w.Turn, Kind = kind, ChildrenTo = childrenTo, Dowry = paid, DowryFrom = paid > 0 ? bride.HouseId : "", Warmth = StartWarmth(w, a, b, kind, favour) };
        w.Marriages.RemoveAll(x => x.Has(a.Id) || x.Has(b.Id)); w.Marriages.Add(m);
        // every other suit for either hand is over
        w.Courtships.RemoveAll(c => c.SuitorId == a.Id || c.SuitorId == b.Id || c.TargetId == a.Id || c.TargetId == b.Id);
        foreach (var c in w.Courtships.Where(c => c.RivalId == a.Id || c.RivalId == b.Id)) { c.RivalId = ""; c.RivalStanding = 0; }
        if (log) w.Log($"{a.Name} of {ha?.Name} weds {b.Name} of {hb?.Name}{(kind == Love ? ", and by every account gladly" : "")}.{(paid > 0 ? $" {paid} credits pass with the bride." : "")}");
        if (ourWedding)
        {
            Mark(w, JustWedFlag); if (kind == Love) { Mark(w, LoveMatchFlag); w.WorldCounters["love_matches"] = w.Counter("love_matches") + 1; }
            var p = w.Protagonist;
            if (p != null && (p.Id == a.Id || p.Id == b.Id) && kind == Love && p.Ambition == "win_a_great_love") p.Stress = Math.Max(0, p.Stress - 15);
        }
        return m;
    }

    // ---------------- the marriage afterwards ----------------

    public static string? TendRefusal(World w, Character? who)
    {
        var m = who == null ? null : w.MarriageOf(who.Id); if (m == null) return "not married";
        return m.TendedTurn >= w.Turn ? "once a year" : null;
    }

    /// <summary>Give the marriage a season of attention.</summary>
    public static void Tend(World w, Character who)
    {
        var m = w.MarriageOf(who.Id)!; var spouse = w.Char(m.Other(who.Id)); m.TendedTurn = w.Turn;
        m.Warmth = Math.Min(100, m.Warmth + 6 + who.Skill("charisma"));
        Sour(w, spouse, who, 5);
        if (m.Estranged && m.Warmth >= ReconciledAt) { m.Estranged = false; w.Log($"{who.Name} and {spouse?.Name} are under one roof again."); }
        else w.Log($"{who.Name} gave the year's quiet months to {spouse?.Name}.");
    }

    public static string? DivorceRefusal(World w, Character? head, Character? who)
    {
        if (head?.Alive != true || who?.Alive != true || who.HouseId != head.HouseId) return "not of your house";
        return w.MarriageOf(who.Id) == null || DynastySystem.Spouse(w, who) == null ? "not married" : null;
    }

    /// <summary>End a marriage. The children stay where they are; a dowry received goes back; the other house takes it as an insult
    /// unless the two were already living apart.</summary>
    public static void Divorce(World w, Character who)
    {
        var m = w.MarriageOf(who.Id)!; var spouse = w.Char(m.Other(who.Id))!; var ours = w.House(who.HouseId)!; var theirs = w.House(spouse.HouseId);
        foreach (var (x, y) in new[] { (who.Id, spouse.Id), (spouse.Id, who.Id) })
        {
            var rel = w.Relationship(x, y); rel.Bond = BondType.None; rel.BondStrength = 0; rel.Disposition = Math.Clamp(rel.Disposition - 30, -100, 100);
        }
        who.Flags.Remove("wed"); spouse.Flags.Remove("wed"); w.Marriages.Remove(m);
        ours.Legitimacy -= (w.IsImperium ? 8 : 4) - (m.Estranged ? 2 : 0);
        if (theirs != null && theirs.Id != ours.Id)
        {
            Sour(w, Head(w, theirs.Id), Head(w, ours.Id), m.Estranged ? -10 : -25);
            if (m.Dowry > 0 && m.DowryFrom == theirs.Id) { int back = Math.Min(m.Dowry, Math.Max(0, ours.Treasury.Credits)); ours.Treasury.Credits -= back; theirs.Treasury.Credits += back; }
        }
        Mark(w, DivorcedFlag);
        w.Log($"{who.Name} of {ours.Name} and {spouse.Name} are divorced.{(m.Dowry > 0 && m.DowryFrom == theirs?.Id ? " The dowry goes back." : "")}");
    }

    /// <summary>How a marriage's warmth bears on children: none while the couple live apart, fewer in a cold house, more in a warm one.</summary>
    public static double Fertility(World w, Character mother, Character father)
    {
        var m = w.MarriageOf(mother.Id); if (m == null || !m.Has(father.Id)) return 1.0;
        return m.Estranged ? 0.0 : 0.7 + m.Warmth * 0.006;
    }

    /// <summary>The house the contract gives the children to, if the contract said.</summary>
    public static string? ContractNameHouse(World w, Character mother, Character father)
    {
        var m = w.MarriageOf(mother.Id); if (m == null || !m.Has(father.Id) || m.ChildrenTo.Length == 0) return null;
        return m.ChildrenTo == mother.HouseId || m.ChildrenTo == father.HouseId ? m.ChildrenTo : null;
    }

    // ---------------- what the stewards' proposals run into ----------------

    /// <summary>A steward's proposal that touches the player's house or its suits is not a wedding in a season: it becomes a rival
    /// suit, or a suit the head must answer, or is turned away because the person is promised. True when it was dealt with here.</summary>
    public static bool Intercept(World w, Character s, Character t)
    {
        string? mine = w.Protagonist?.HouseId; if (mine == null || s.HouseId == mine) return false;
        if (Promised(w, s.Id) || Promised(w, t.Id)) return true;
        foreach (var c in w.Courtships.Where(c => !c.Incoming))
        {
            string? other = c.TargetId == t.Id ? s.Id : c.TargetId == s.Id ? t.Id : null;
            if (other == null) continue;
            if (c.RivalId.Length == 0) AddRival(w, c, w.Char(other)!);
            return true;
        }
        if (t.HouseId == mine && t.Id != w.ProtagonistId)
        {
            if (!w.Courtships.Any(c => c.Incoming && c.TargetId == t.Id))
            {
                int n = w.Counter("courtships") + 1; w.WorldCounters["courtships"] = n;
                w.Courtships.Add(new Courtship { Id = "court_" + n, SuitorId = s.Id, TargetId = t.Id, Incoming = true, Since = w.Turn, Favour = StartFavour(w, s, t), Consent = 100 });
                w.Log($"{s.Name} of {w.House(s.HouseId)?.Name} asks for the hand of {t.Name}. The head of the house must answer.");
            }
            return true;
        }
        return false;
    }

    private static void AddRival(World w, Courtship c, Character rival)
    {
        var s = w.Char(c.SuitorId); var t = w.Char(c.TargetId);
        c.RivalId = rival.Id;
        c.RivalStanding = Math.Clamp(22 + ((w.House(rival.HouseId)?.Legitimacy ?? 0) - (w.House(s?.HouseId ?? "")?.Legitimacy ?? 0)) / 3 + (int)(Hash(c.Id + rival.Id) % 10), 10, 50);
        w.Log($"{rival.Name} of {w.House(rival.HouseId)?.Name} is also seeking {t?.Name}'s hand.");
    }

    // ---------------- the year ----------------

    /// <summary>Make a record for every marriage that has none (authored in a scenario, made by an event) and drop the records of
    /// marriages that have ended. Called when a world is opened, without fanfare, and every year after.</summary>
    public static void Sync(World w, bool announce)
    {
        string? mine = w.Protagonist?.HouseId;
        foreach (var r in w.Relationships.Where(r => r.Bond == BondType.Marriage).ToList())
        {
            var a = w.Char(r.FromId); var b = w.Char(r.ToId);
            if (a?.Alive != true || b?.Alive != true || w.Marriages.Any(m => m.Has(a.Id) && m.Has(b.Id))) continue;
            if (w.Marriages.Any(m => m.Has(a.Id) || m.Has(b.Id))) continue;   // one living spouse each; the first written stands
            bool first = string.CompareOrdinal(a.Id, b.Id) < 0;
            w.Marriages.Add(new Marriage { A = first ? a.Id : b.Id, B = first ? b.Id : a.Id, Since = w.Turn, Kind = Arranged, Warmth = StartWarmth(w, a, b, Arranged, 0) });
            a.Flags.Add("wed"); b.Flags.Add("wed");
            w.Courtships.RemoveAll(c => c.SuitorId == a.Id || c.SuitorId == b.Id || c.TargetId == a.Id || c.TargetId == b.Id);
            if (announce && mine != null && (a.HouseId == mine || b.HouseId == mine)) Mark(w, JustWedFlag);
        }
        foreach (var m in w.Marriages.ToList())
        {
            var a = w.Char(m.A); var b = w.Char(m.B);
            bool bonded = w.GetRelationship(m.A, m.B)?.Bond == BondType.Marriage || w.GetRelationship(m.B, m.A)?.Bond == BondType.Marriage;
            if (a?.Alive == true && b?.Alive == true && bonded) continue;
            w.Marriages.Remove(m);
            var survivor = a?.Alive == true && b?.Alive != true ? a : b?.Alive == true && a?.Alive != true ? b : null;
            if (survivor != null)
            {
                survivor.Flags.Remove("wed"); survivor.Flags.Add(WidowedChar);
                if (survivor.Id == w.ProtagonistId) Mark(w, WidowedFlag);
            }
        }
    }

    public static void Tick(World w)
    {
        foreach (var f in TickFlags) w.WorldFlags.Remove(f);
        foreach (var f in DeedFlags) if (w.WorldFlags.Contains(f) && w.Counter("mark:" + f) < w.Turn - 1) { w.WorldFlags.Remove(f); w.WorldCounters.Remove("mark:" + f); }
        foreach (var ch in w.Characters.Values) { ch.Flags.Remove(CourtedChar); ch.Flags.Remove(RivalChar); }
        Sync(w, announce: true);
        var p = w.Protagonist; string? mine = p?.HouseId;

        foreach (var c in w.Courtships.OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
        {
            if (!w.Courtships.Contains(c)) continue;   // a wedding earlier in this pass ended it
            var s = w.Char(c.SuitorId); var t = w.Char(c.TargetId);
            if (s?.Alive != true || t?.Alive != true || DynastySystem.Spouse(w, s) != null || DynastySystem.Spouse(w, t) != null || s.HouseId == t.HouseId
                || w.AtWar(s.HouseId, t.HouseId) || (mine != null && s.HouseId != mine && t.HouseId != mine))
            {
                w.Courtships.Remove(c);
                if (s != null && t != null) w.Log($"The match between {s.Name} and {t.Name} has come to nothing.");
                continue;
            }
            if (c.Incoming)
            {
                if (w.Turn - c.Since >= SuitLapseYears)
                {
                    w.Courtships.Remove(c); Sour(w, Head(w, s.HouseId), Head(w, t.HouseId), -5);
                    w.Log($"{s.Name} of {w.House(s.HouseId)?.Name} withdrew the suit for {t.Name}; it had gone unanswered.");
                }
                continue;
            }
            if (c.Betrothed)
            {
                if (s.Age >= Adult && t.Age >= Adult) Wed(w, s, t, Arranged, c.Dowry, c.ChildrenTo);
                continue;
            }
            bool child = IsChildMatch(w, c);
            if (!child && c.Favour == 0 && w.Turn > c.Since) c.Favour = StartFavour(w, s, t);   // promised as children, never agreed, now grown

            if (c.RivalId.Length > 0)
            {
                var r = w.Char(c.RivalId);
                if (r?.Alive != true || DynastySystem.Spouse(w, r) != null || w.AtWar(r.HouseId, t.HouseId) || r.HouseId == t.HouseId)
                {
                    if (r != null) w.Log($"{r.Name} has given up the suit for {t.Name}.");
                    c.RivalId = ""; c.RivalStanding = 0;
                }
                else
                {
                    c.RivalStanding = Math.Min(100, c.RivalStanding + 7 + (int)(Hash(c.Id + ":press:" + w.Turn) % 6));
                    if (c.RivalStanding >= RivalWinsAt && c.RivalStanding > Standing(w, c))
                    {
                        w.Log($"{t.Name} of {w.House(t.HouseId)?.Name} is to marry {r.Name}. {s.Name}'s suit is at an end.");
                        Sour(w, s, r, -30); if (s.Id == p?.Id) s.Stress += 10;
                        Wed(w, r, t, Advantage);
                        Mark(w, JiltedFlag);
                        continue;
                    }
                }
            }
            else if (!child && w.Turn > c.Since && Standing(w, c) < 70 && Hash(c.Id + ":rival:" + w.Turn) % 100 < 22)
            {
                var rival = w.LivingCharacters().Where(x => x.HouseId != s.HouseId && x.HouseId != t.HouseId && x.HouseId != PirateSystem.HouseId && x.Age >= Adult && Bar(w, x, t) == null && Math.Abs(x.Age - t.Age) <= 20)
                    .OrderByDescending(x => w.House(x.HouseId)?.Legitimacy ?? 0).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
                if (rival != null) AddRival(w, c, rival);
            }

            if (c.ActedTurn < w.Turn && w.Turn > c.Since)
            {
                if (!child) c.Favour = Math.Max(0, c.Favour - 3);
                c.Consent = Math.Max(0, c.Consent - 1);
                if (w.Turn - Math.Max(c.ActedTurn, c.Since) >= LapseYears)
                {
                    w.Courtships.Remove(c); w.Log($"Nothing has been done about {s.Name}'s suit for {t.Name} in years. It is over.");
                    continue;
                }
            }
        }

        foreach (var m in w.Marriages.OrderBy(m => m.A, StringComparer.Ordinal).ToList())
        {
            var a = w.Char(m.A); var b = w.Char(m.B); if (a == null || b == null) continue;
            int children = w.Characters.Values.Count(k => (k.MotherId == a.Id && k.FatherId == b.Id) || (k.MotherId == b.Id && k.FatherId == a.Id));
            int target = 50 + Spark(a.Id, b.Id) / 2 + Math.Min(9, children * 3) + (m.Kind == Love ? 10 : m.Kind == Arranged ? -4 : 0);
            foreach (var x in new[] { a, b })
            {
                foreach (var good in new[] { "Honorable", "Loyal", "Just" }) if (x.NatureTraits.Contains(good)) target += 4;
                foreach (var bad in new[] { "Cruel", "Arrogant" }) if (x.NatureTraits.Contains(bad)) target -= 6;
                if (x.NatureTraits.Contains("Vengeful")) target -= 3;
            }
            if (m.Warmth < target) m.Warmth = Math.Min(target, m.Warmth + 2);
            else if (m.Warmth > target && m.TendedTurn < w.Turn) m.Warmth = Math.Max(target, m.Warmth - 2);
            if ((a.Stress >= 60 || b.Stress >= 60) && m.TendedTurn < w.Turn) m.Warmth -= 2;
            m.Warmth = Math.Clamp(m.Warmth, 0, 100);
            bool ours = mine != null && (a.HouseId == mine || b.HouseId == mine);
            if (!m.Estranged && m.Warmth <= EstrangedAt) { m.Estranged = true; if (ours) w.Log($"{a.Name} and {b.Name} no longer live under one roof."); }
            else if (m.Estranged && m.Warmth >= ReconciledAt) { m.Estranged = false; if (ours) w.Log($"{a.Name} and {b.Name} are reconciled."); }
            if (a.HouseId != b.HouseId)
            {
                var ha = Head(w, a.HouseId); var hb = Head(w, b.HouseId); int tie = m.Estranged ? -2 : m.Warmth >= WarmAt ? 1 : 0;
                if (tie != 0 && ha != null && hb != null && (tie < 0 || (w.GetRelationship(ha.Id, hb.Id)?.Disposition ?? 0) < 50)) { Sour(w, ha, hb, tie); Sour(w, hb, ha, tie); }
            }
            if (p != null && m.Has(p.Id))
            {
                if (m.Warmth >= WarmAt) p.Stress = Math.Max(0, p.Stress - 2); else if (m.Warmth <= 20) p.Stress += 2;
            }
        }

        // what content may read
        if (p == null) return;
        var own = w.Courtships.Where(c => !c.Incoming && !c.Betrothed).ToList();
        if (own.Count > 0) w.WorldFlags.Add(CourtingFlag);
        foreach (var c in own.Where(c => c.SuitorId == p.Id)) { w.Char(c.TargetId)?.Flags.Add(CourtedChar); if (c.RivalId.Length > 0) w.Char(c.RivalId)?.Flags.Add(RivalChar); }
        if (own.Any(c => c.RivalId.Length > 0)) w.WorldFlags.Add(RivalFlag);
        if (w.Courtships.Any(c => c.Incoming)) w.WorldFlags.Add(SuitFlag);
        if (w.Courtships.Any(c => c.Betrothed)) w.WorldFlags.Add(BetrothedFlag);
        var pm = w.MarriageOf(p.Id);
        w.WorldCounters[WarmthCounter] = pm?.Warmth ?? 0;
        w.WorldCounters[StandingCounter] = own.Where(c => c.SuitorId == p.Id).Select(c => Standing(w, c)).DefaultIfEmpty(0).Max();
        if (pm != null)
        {
            w.WorldFlags.Add(MarriedFlag);
            if (pm.Estranged) w.WorldFlags.Add(EstrangedFlag);
            if (pm.Warmth >= WarmAt) w.WorldFlags.Add(WarmFlag); else if (pm.Warmth <= ColdAt) w.WorldFlags.Add(ColdFlag);
        }
    }
}
