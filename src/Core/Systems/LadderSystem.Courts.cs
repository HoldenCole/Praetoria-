using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// What a magistrate did is answered for (docs/LADDER.md, "The courts"). A bent act in office (money taken from the accounts, a
/// gift taken to rule, a ruling against the law for a friend) is written on a RECORD that outlasts the year. While they sit they
/// cannot be touched. Once their term is over, any house may PROSECUTE them before a sitting praetor, and the verdict is that
/// praetor's: the upright convict on the record; one who thinks well of the accused's house lets them go; one who is for sale
/// finds for the richer house; before the rest it is the advocates' skill. Other houses prosecute too, and the people of the
/// player's house are not excepted. And a sitting praetor can be BOUGHT: the upright refuse and say so; one who takes it finds for
/// the buyer whenever that house is before them, until it comes out. RNG-free.
/// </summary>
public static partial class LadderSystem
{
    public const int RecordYears = 8, ProsecuteInfluence = 2, FineCredits = 10, ConvictLegitimacy = 4, BribeCredits = 15, BribeLegitimacy = 5, ProsecuteAt = -30;
    public const string TookAccounts = "accounts", TookGift = "gift", BentRuling = "ruling", Bribed = "bribe";
    public const string ProsecutionWonFlag = "prosecution_won", ProsecutionLostFlag = "prosecution_lost", BribeRefusedFlag = "bribe_refused", BribeOutFlag = "bribe_came_out", KinAcquittedFlag = "kin_acquitted";

    private static string TriedKey(string houseId) => "prosecuted_in:" + houseId;
    private static string BoughtFlag(string praetorId, string houseId) => $"bought:{praetorId}:{houseId}";

    /// <summary>Write down what was done, so that it can be answered for when the term is over.</summary>
    private static void Record(World w, Character who, OfficeBusiness b, string choice, bool answered)
    {
        string what = choice == Favour ? TookAccounts : choice == Gift ? TookGift : BentRuling;
        string against = b.Kind == Case && b.WinnerId.Length > 0 ? (b.WinnerId == b.HouseA ? b.HouseB : b.HouseA) : "";
        w.OfficeRecord.Add(new OfficeDeed { Id = "deed_" + b.Id, CharacterId = who.Id, HouseId = who.HouseId, RungId = b.RungId, Turn = w.Turn, What = what, AgainstId = against, Answered = answered });
    }

    public static string DeedSays(World w, OfficeCatalog offices, OfficeDeed d)
    {
        string office = offices.Rung(d.RungId)?.Name.ToLowerInvariant() ?? d.RungId, against = w.House(d.AgainstId)?.Name ?? "";
        return d.What switch
        {
            TookAccounts => $"took money out of the Reach's accounts as {office} in year {d.Turn}",
            TookGift => $"took a gift to rule as {office} in year {d.Turn}" + (against.Length > 0 ? $", against {against}" : ""),
            Bribed => $"bought a praetor in year {d.Turn}",
            _ => $"ruled against the law for a friend as {office} in year {d.Turn}" + (against.Length > 0 ? $", against {against}" : "")
        };
    }

    /// <summary>What could be brought before a court by this house: acts on the record that nobody has answered for, done by someone
    /// living, of another house, who no longer sits in the office they did it in.</summary>
    public static List<OfficeDeed> Chargeable(World w, string accuserHouseId) =>
        w.OfficeRecord.Where(d => !d.Answered && d.HouseId != accuserHouseId && w.Char(d.CharacterId) is { Alive: true } c && c.HouseId == d.HouseId && (d.What == Bribed || Seat(w, c.Id)?.OfficeId != d.RungId))
            .OrderByDescending(d => d.Turn).ThenBy(d => d.Id, StringComparer.Ordinal).ToList();

    /// <summary>The praetor who would hear it: the longest sitting who is of neither house.</summary>
    public static Character? Court(World w, OfficeDeed d, string accuserHouseId) =>
        w.Magistrates.Where(m => m.OfficeId == Praetor && m.HouseId != d.HouseId && m.HouseId != accuserHouseId && m.CharacterId != d.CharacterId)
            .OrderBy(m => m.SinceTurn).ThenBy(m => m.CharacterId, StringComparer.Ordinal).Select(m => w.Char(m.CharacterId)).FirstOrDefault(c => c is { Alive: true });

    /// <summary>Who pleads for a house: whoever of it knows most law and speaks best.</summary>
    public static Character? Advocate(World w, string houseId) =>
        w.LivingCharacters().Where(c => c.HouseId == houseId && c.Age >= DynastySystem.AgeOfMajority).OrderByDescending(c => c.Skill("law") + c.Skill("oratory")).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();

    public static string? ProsecuteRefusal(World w, Character? head, OfficeDeed? d)
    {
        var house = head != null ? w.House(head.HouseId) : null; if (head?.Alive != true || house == null || Economy.HeadOf(w, house)?.Id != head.Id) return "only the head of a house brings a charge";
        if (!PolitySystem.InReach(w, house.Id)) return "the courts of the Reach are not yours to use";
        if (d == null || !Chargeable(w, house.Id).Contains(d)) return "there is nothing on the record to charge them with, or they still sit";
        if (w.Counter(TriedKey(house.Id)) == w.Turn + 1) return "your house has brought a charge this year";
        return Court(w, d, house.Id) == null ? "no praetor sits who could hear it" : null;
    }

    /// <summary>How the praetor would find, and why: so that the one who brings the charge can see what they are walking into.</summary>
    public static (bool guilty, string why) Verdict(World w, OfficeDeed d, string accuserHouseId, Character judge)
    {
        bool honest = judge.NatureTraits.Contains("Honorable") || judge.NatureTraits.Contains("Just");
        if (honest) return (true, "upright, and will go by the record");
        var accused = w.House(d.HouseId); var accuser = w.House(accuserHouseId); var bench = w.House(judge.HouseId);
        if (BoughtBy(w, judge) is { } buyer && (buyer == d.HouseId || buyer == accuserHouseId)) return (buyer == accuserHouseId, "bought, and will find for whoever paid");
        if (Regard(w, bench, accused) >= 30) return (false, "a friend of the accused's house");
        bool grasping = judge.NatureTraits.Contains("Ruthless") || judge.NatureTraits.Contains("Cruel") || judge.Ambition == "amass_a_fortune";
        if (grasping) return ((accuser?.Treasury.Credits ?? 0) >= (accused?.Treasury.Credits ?? 0), "known to be for sale, and will find for the richer house");
        int plead = Advocate(w, accuserHouseId) is { } a ? a.Skill("law") + a.Skill("oratory") : 0, defend = Advocate(w, d.HouseId) is { } df ? df.Skill("law") + df.Skill("oratory") : 0;
        return (40 + plead * 6 - defend * 4 > (int)(Hash(d.Id + ":verdict") % 100), "neither upright nor a friend: it will go by who pleads better");
    }

    /// <summary>The charge is brought and heard. Whichever way it goes, it is not heard twice.</summary>
    public static bool Prosecute(World w, OfficeCatalog offices, Character head, OfficeDeed d)
    {
        // what a court does with an old wrong is set against the office it was done in, and felt where the accused's house lives
        _trustOffice = d.RungId; _trustRegion = HomeRegion(w, d.HouseId) ?? "";
        try { return ProsecuteIt(w, offices, head, d); } finally { TrustDone(); }
    }
    private static bool ProsecuteIt(World w, OfficeCatalog offices, Character head, OfficeDeed d)
    {
        var accuser = w.House(head.HouseId)!; var judge = Court(w, d, accuser.Id)!; var who = w.Char(d.CharacterId)!; var accused = w.House(d.HouseId);
        var (guilty, why) = Verdict(w, d, accuser.Id, judge); bool bent = !guilty && !why.StartsWith("neither", StringComparison.Ordinal);
        d.Answered = true; w.WorldCounters[TriedKey(accuser.Id)] = w.Turn + 1; w.WorldCounters["prosecutions"] = w.Counter("prosecutions") + 1;
        var mine = w.Protagonist; bool ours = mine != null && accuser.Id == mine.HouseId, theirs = mine != null && d.HouseId == mine.HouseId;
        var pleader = Advocate(w, accuser.Id) ?? head;
        if (guilty)
        {
            w.Magistrates.RemoveAll(m => m.CharacterId == who.Id); w.WorldCounters[BarredKey(who.Id)] = w.Turn + BarYears; w.WorldCounters[ScandalKey(d.HouseId)] = w.Turn;
            if (accused != null)
            {
                accused.Legitimacy = Math.Clamp(accused.Legitimacy - ConvictLegitimacy, 0, 100);
                int fine = Math.Min(FineCredits, Math.Max(0, accused.Treasury.Credits)); accused.Treasury.Credits -= fine;
                if (w.House(d.AgainstId) is { } wronged) wronged.Treasury.Credits += fine; else if (RegionSystem.CapitalHolder(w) is { } chair && chair.Id != accused.Id) chair.Treasury.Credits += fine;
            }
            accuser.Legitimacy = Math.Clamp(accuser.Legitimacy + 2, 0, 100); PolicySystem.Sway(w, accuser.Id, "commons", 2); MoveTrust(w, 1); Shift(w, accused, accuser, -25);
            who.Flags.Add(ExposedChar); LifeSystem.Note(w, who, "office", "Was convicted of what they did in office."); LifeSystem.Note(w, pleader, "office", $"Prosecuted {who.Name} and won.");
            if (ours) Mark(w, ProsecutionWonFlag); if (theirs) Mark(w, who.Id == mine!.Id ? ExposedFlag : KinExposedFlag);
            w.WorldCounters["convictions"] = w.Counter("convictions") + 1;
            w.Log($"{accuser.Name} charges {who.Name} of {accused?.Name}, who {DeedSays(w, offices, d)}. {judge.Name}, praetor, finds it proved. {who.Name} may not stand for {BarYears} years, and {accused?.Name} pays.");
        }
        else
        {
            accuser.Legitimacy = Math.Clamp(accuser.Legitimacy - 1, 0, 100); Shift(w, accused, accuser, -15); if (bent) MoveTrust(w, -1);
            if (ours) Mark(w, ProsecutionLostFlag); if (theirs) Mark(w, KinAcquittedFlag);
            w.Log($"{accuser.Name} charges {who.Name} of {accused?.Name}, who {DeedSays(w, offices, d)}. {judge.Name}, praetor, lets {who.Name} go{(bent ? ", and it is understood why" : "")}.");
        }
        return guilty;
    }

    // ---------------- a praetor in the pocket ----------------

    /// <summary>The house that has bought this praetor, while they sit.</summary>
    public static string? BoughtBy(World w, Character praetor)
    {
        string prefix = $"bought:{praetor.Id}:";
        return w.WorldFlags.Where(f => f.StartsWith(prefix, StringComparison.Ordinal)).OrderBy(f => f, StringComparer.Ordinal).Select(f => f[prefix.Length..]).FirstOrDefault();
    }
    /// <summary>The house a bought praetor finds for, when it is before them.</summary>
    private static string? BoughtWinner(World w, OfficeBusiness b, Character judge) => BoughtBy(w, judge) is { } buyer && (b.HouseA == buyer || b.HouseB == buyer) ? buyer : null;

    public static string? BuyRefusal(World w, Character? head, Character? praetor)
    {
        var house = head != null ? w.House(head.HouseId) : null; if (head?.Alive != true || house == null || Economy.HeadOf(w, house)?.Id != head.Id) return "only the head of a house makes such an offer";
        if (praetor?.Alive != true || Seat(w, praetor.Id)?.OfficeId != Praetor) return "they are not a sitting praetor";
        if (praetor.HouseId == house.Id) return "they are of your own house";
        if (BoughtBy(w, praetor) != null) return "they are spoken for";
        return (house.Id == w.Protagonist?.HouseId ? head.Purse : house.Treasury.Credits) >= BribeCredits ? null : $"needs {BribeCredits} credits of your own";
    }
    /// <summary>Would they take it? The upright never; one who is for sale always; the rest only from a house they do not think ill of.</summary>
    public static bool WouldTake(World w, Character head, Character praetor)
    {
        if (praetor.NatureTraits.Contains("Honorable") || praetor.NatureTraits.Contains("Just")) return false;
        if (praetor.NatureTraits.Contains("Ruthless") || praetor.NatureTraits.Contains("Cruel") || praetor.Ambition == "amass_a_fortune") return true;
        return (w.GetRelationship(praetor.Id, head.Id)?.Disposition ?? 0) >= 0 && Hash(praetor.Id + ":takes") % 2 == 0;
    }
    /// <summary>The offer is made. True if it was taken.</summary>
    public static bool Buy(World w, Character head, Character praetor)
    {
        var house = w.House(head.HouseId)!; bool ours = house.Id == w.Protagonist?.HouseId; bool honest = praetor.NatureTraits.Contains("Honorable") || praetor.NatureTraits.Contains("Just");
        if (!WouldTake(w, head, praetor))
        {
            var r = w.Relationship(praetor.Id, head.Id); r.Disposition = Math.Clamp(r.Disposition - (honest ? 20 : 5), -100, 100);
            if (!honest) { w.Log($"{praetor.Name}, praetor, does not take what {head.Name} offers, and says nothing of it."); return false; }
            // the upright say so, in open court
            house.Legitimacy = Math.Clamp(house.Legitimacy - BribeLegitimacy + 1, 0, 100); w.WorldCounters[ScandalKey(house.Id)] = w.Turn; MoveTrust(w, 2);
            if (ours) Mark(w, BribeRefusedFlag);
            w.Log($"{praetor.Name}, praetor, says in open court what {head.Name} of {house.Name} offered. Nothing was taken. Everything was heard."); return false;
        }
        if (ours) head.Purse -= BribeCredits; else house.Treasury.Credits -= BribeCredits;
        if (w.House(praetor.HouseId) is { } theirs) theirs.Treasury.Credits += BribeCredits;
        w.WorldFlags.Add(BoughtFlag(praetor.Id, house.Id)); w.WorldCounters["praetors_bought"] = w.Counter("praetors_bought") + 1;
        w.OfficeRecord.Add(new OfficeDeed { Id = $"deed_bribe_{w.Turn}_{praetor.Id}", CharacterId = head.Id, HouseId = house.Id, RungId = Praetor, Turn = w.Turn, What = Bribed, AgainstId = "", Answered = false });
        w.Log($"{head.Name} and {praetor.Name}, praetor, come to an understanding. Nothing is written down."); return true;
    }
    /// <summary>A bought ruling comes out: the buyer falls with the praetor.</summary>
    private static void BribeOut(World w, string buyerId, Character praetor)
    {
        var buyer = w.House(buyerId); if (buyer == null) return;
        buyer.Legitimacy = Math.Clamp(buyer.Legitimacy - BribeLegitimacy, 0, 100); w.WorldCounters[ScandalKey(buyer.Id)] = w.Turn; MoveTrust(w, -2);
        foreach (var d in w.OfficeRecord.Where(d => d.What == Bribed && d.HouseId == buyerId)) d.Answered = true;
        if (buyer.Id == w.Protagonist?.HouseId) Mark(w, BribeOutFlag);
        w.Log($"It comes out whose money {praetor.Name} had taken: {buyer.Name}'s. The house is thought the less of, and its people will find it harder at the count.");
    }

    // ---------------- the year ----------------

    /// <summary>The record is pruned; a praetor who no longer sits is nobody's; and one house that hates another enough, and finds
    /// something on the record against one of its people, brings a charge. One such a year in the whole Reach.</summary>
    private static void Courts(World w, OfficeCatalog offices)
    {
        w.OfficeRecord.RemoveAll(d => w.Turn - d.Turn > RecordYears || w.Char(d.CharacterId) is not { Alive: true });
        foreach (var f in w.WorldFlags.Where(f => f.StartsWith("bought:", StringComparison.Ordinal)).ToList())
        {
            var parts = f.Split(':'); if (parts.Length < 3 || w.Char(parts[1]) is not { Alive: true } pr || Seat(w, pr.Id)?.OfficeId != Praetor) w.WorldFlags.Remove(f);
        }
        var mine = w.Protagonist?.HouseId;
        foreach (var h in w.Houses.Values.Where(h => h.Id != mine && h.Id != PirateSystem.HouseId && PolitySystem.InReach(w, h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            var head = Economy.HeadOf(w, h); if (head == null) continue;
            var d = Chargeable(w, h.Id).FirstOrDefault(x => Regard(w, h, w.House(x.HouseId)) <= ProsecuteAt && Court(w, x, h.Id) != null);
            if (d == null || ProsecuteRefusal(w, head, d) != null) continue;
            Prosecute(w, offices, head, d); return;
        }
    }
}
