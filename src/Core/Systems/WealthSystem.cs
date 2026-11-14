using Praetoria.Core.Progression;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Personal wealth, house wealth and the dynasty's assets (docs/WEALTH.md). A house has its treasury; each grown person of it has
/// a PURSE of their own. The purse is fed by an allowance the head sets for everyone, by the pay of a person's service, and by any
/// estate they hold. What a person does with their purse is their own business (docs/PROCESS.md, "One person, not a house"): they
/// save, spend on being seen, put their own money into a house in trouble, gamble, and run into debt. The head decides how generous
/// the house is, whether to pay a relative's debts, and what their own WILL says.
/// <para>HEIRLOOMS belong to a person, not to the house: a signet, a sword, a library, an estate. They can be given, sold, and left
/// to someone. When a head dies the house goes by the succession law; their purse and heirlooms go by their will; and the treasury
/// is kept whole or shared out in portions by the custom of the house.</para>
/// Only the player's house keeps purses. RNG-free.
/// </summary>
public static class WealthSystem
{
    public const string Mean = "mean", Customary = "customary", Generous = "generous";
    public static readonly string[] Allowances = { Mean, Customary, Generous };
    public const string Entail = "entail", Portions = "portions";
    public const int Adult = DynastySystem.AgeOfMajority, DebtAt = -8, ScandalAt = -20, PortionPct = 10, ComfortablePurse = 20;
    // each year
    public const string DebtFlag = "kin_in_debt", ResentedFlag = "allowance_resented";
    // deeds
    public const string KinGaveFlag = "kin_gave_to_the_house", WillReadFlag = "will_read", WillContestedFlag = "will_contested", HeirloomSoldFlag = "heirloom_sold", HeirloomGivenFlag = "heirloom_given", DebtScandalFlag = "debt_scandal";
    public const string InDebtChar = "in_debt", GaveChar = "gave_to_the_house", LegateeChar = "remembered_in_the_will";
    private static readonly string[] DeedFlags = { KinGaveFlag, WillReadFlag, WillContestedFlag, HeirloomSoldFlag, HeirloomGivenFlag, DebtScandalFlag };

    private static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }
    private static uint Hash(string s) { uint h = 2166136261; foreach (char ch in s) h = (h ^ ch) * 16777619; return h; }
    private static void Regard(World w, Character? from, Character? to, int delta, int floor = -100, int ceiling = 100)
    {
        if (from == null || to == null || from.Id == to.Id) return;
        var r = w.Relationship(from.Id, to.Id);
        if (delta < 0 && r.Disposition <= floor || delta > 0 && r.Disposition >= ceiling) return;
        r.Disposition = Math.Clamp(r.Disposition + delta, -100, 100);
    }

    /// <summary>What each grown member is given a year under an allowance.</summary>
    public static int AllowanceOf(string policy) => policy == Generous ? 3 : policy == Mean ? 0 : 1;
    public static string AllowanceSays(string policy) => policy == Generous ? "They think the better of you for it, year by year." : policy == Mean ? "They think the worse of you for it, year by year, and the wilful most of all." : "Nobody remarks on it.";
    /// <summary>What a person's own service pays them.</summary>
    public static int Pay(Character c) => c.CareerTrack.Length > 0 ? (c.CareerRank + 1) / 2 : 0;
    public static List<Character> Members(World w, House house) =>
        w.LivingCharacters().Where(c => c.HouseId == house.Id && c.Age >= Adult).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
    public static int AllowanceBill(World w, House house) => Members(w, house).Count(c => c.Id != w.ProtagonistId) * AllowanceOf(house.Allowance);
    public static List<Heirloom> HeldBy(World w, string characterId) => w.Heirlooms.Where(h => h.HolderId == characterId).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
    public static int EstateIncome(World w, string characterId) => w.Heirlooms.Count(h => h.HolderId == characterId && h.Kind == "estate") * 2;

    // ---------------- heirlooms ----------------

    /// <summary>A house of an old name or of rank comes with something handed down. Called once when a world is opened.</summary>
    public static void Seed(World w)
    {
        if (w.Counter("heirlooms_seeded") != 0) return; w.WorldCounters["heirlooms_seeded"] = 1;
        foreach (var h in w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId && (h.Origin == "old" || Title.RankOf(h.Title) >= 3)).OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            var head = Economy.HeadOf(w, h); if (head == null) continue;
            string name = h.Name.Replace("House ", ""); uint x = Hash(h.Id + ":heirloom");
            var (kind, what) = (x % 4) switch { 0u => ("signet", $"the {name} signet"), 1u => ("sword", $"the sword of {h.Name}"), 2u => ("library", $"the {name} library"), _ => ("estate", $"the old {name} estate") };
            w.Heirlooms.Add(new Heirloom { Id = "heirloom_" + h.Id, Name = what, Kind = kind, HolderId = head.Id, OfHouseId = h.Id, Value = 15 + (int)(x / 7 % 26) });
        }
    }

    public static string? GiveRefusal(World w, Character? giver, Heirloom? item, Character? to)
    {
        if (giver?.Alive != true || item == null || item.HolderId != giver.Id) return "not yours to give";
        if (to?.Alive != true || to.Id == giver.Id) return "no such person";
        return to.Age >= 6 ? null : "too young to be given it";
    }

    public static void Give(World w, Character giver, Heirloom item, Character to)
    {
        item.HolderId = to.Id; Regard(w, to, giver, to.HouseId == giver.HouseId ? 15 : 20);
        if (giver.Will != null) giver.Will.Items.Remove(item.Id);
        if (to.HouseId != item.OfHouseId && w.House(item.OfHouseId) is { } of && of.Id == giver.HouseId) of.Legitimacy -= 1;
        Mark(w, HeirloomGivenFlag);
        w.Log($"{giver.Name} gives {item.Name} to {to.Name}.");
    }

    /// <summary>Sell it. The money is the seller's own. Selling something the family has always had is noticed and resented.</summary>
    public static void Sell(World w, Character seller, Heirloom item)
    {
        w.Heirlooms.Remove(item); seller.Purse += item.Value; if (seller.Will != null) seller.Will.Items.Remove(item.Id);
        if (item.OfHouseId == seller.HouseId && w.House(seller.HouseId) is { } house)
        {
            house.Legitimacy -= 3;
            foreach (var k in Members(w, house).Where(k => k.Id != seller.Id && !HouseholdSystem.IsFound(k))) Regard(w, k, seller, -8);
        }
        Mark(w, HeirloomSoldFlag);
        w.Log($"{seller.Name} sells {item.Name} for {item.Value} credits.");
    }

    // ---------------- the head's own dealings ----------------

    /// <summary>Move credits between the head's own purse and the treasury of their house. Positive: out of the treasury.</summary>
    public static string? MoveRefusal(World w, Character? head, int amount)
    {
        var house = head != null ? w.House(head.HouseId) : null; if (head?.Alive != true || house == null || amount == 0) return "nothing to move";
        if (Economy.HeadOf(w, house)?.Id != head.Id) return "only the head of the house has its purse";
        return amount > 0 ? (house.Treasury.Credits >= amount ? null : "the treasury does not hold it") : (head.Purse >= -amount ? null : "your purse does not hold it");
    }

    public static void Move(World w, Character head, int amount)
    {
        var house = w.House(head.HouseId)!; house.Treasury.Credits -= amount; head.Purse += amount;
        // a head who helps themselves while the family goes without is watched doing it
        if (amount > 0 && house.Allowance == Mean)
            foreach (var k in Members(w, house).Where(k => k.Id != head.Id)) Regard(w, k, head, -2, floor: -40);
        w.Log(amount > 0 ? $"{head.Name} takes {amount} credits from the treasury of {house.Name} for their own purse." : $"{head.Name} puts {-amount} credits of their own into the treasury of {house.Name}.");
    }

    public static string? SettleRefusal(World w, Character? head, Character? debtor, bool fromPurse)
    {
        if (head?.Alive != true || debtor?.Alive != true || debtor.HouseId != head.HouseId || debtor.Purse >= 0) return "they owe nothing";
        int owed = -debtor.Purse; int have = fromPurse ? head.Purse : w.House(head.HouseId)?.Treasury.Credits ?? 0;
        return have >= owed ? null : fromPurse ? "your purse does not hold it" : "the treasury does not hold it";
    }

    /// <summary>Pay what one of the house owes, from the treasury or from the head's own purse.</summary>
    public static void Settle(World w, Character head, Character debtor, bool fromPurse)
    {
        int owed = -debtor.Purse; if (fromPurse) head.Purse -= owed; else w.House(head.HouseId)!.Treasury.Credits -= owed;
        debtor.Purse = 0; debtor.Flags.Remove(InDebtChar); Regard(w, debtor, head, fromPurse ? 15 : 10);
        w.Log($"{head.Name} pays the {owed} credits {debtor.Name} owed{(fromPurse ? ", out of their own purse" : "")}.");
    }

    // ---------------- the will, and what happens when a head dies ----------------

    public static string? WillRefusal(World w, Character? who, string legateeId, int sharePct)
    {
        if (who?.Alive != true) return "no such person";
        if (sharePct is not (0 or 25 or 50 or 100)) return "a quarter, a half or all of it";
        if (legateeId.Length == 0) return sharePct == 0 ? null : "name who is to have it";
        return w.Char(legateeId) is { Alive: true } l && l.Id != who.Id ? null : "no such person";
    }

    /// <summary>The head is dead and <paramref name="heir"/> has the house. The purse and heirlooms go by the will; the treasury is
    /// kept whole or shared out by the custom of the house; debts are the house's now.</summary>
    public static void Inherit(World w, Character dead, Character heir)
    {
        var house = w.House(heir.HouseId); if (house == null) return;
        var will = dead.Will ?? new Will(); var legatee = will.LegateeId.Length > 0 ? w.Char(will.LegateeId) : null; if (legatee is not { Alive: true } || legatee.Id == heir.Id) legatee = null;
        int purse = dead.Purse; dead.Purse = 0;
        if (purse < 0) { house.Treasury.Credits += purse; w.Log($"{dead.Name} died owing {-purse} credits. {house.Name} must pay it."); }
        else if (purse > 0)
        {
            int toLegatee = legatee != null ? purse * will.SharePct / 100 : 0;
            if (legatee != null && toLegatee > 0)
            {
                legatee.Purse += toLegatee; legatee.Flags.Add(LegateeChar);
                w.Log($"By {dead.Name}'s will, {toLegatee} credits go to {legatee.Name}.");
                if (will.SharePct >= 50) { Regard(w, heir, legatee, -15); Mark(w, WillContestedFlag); }
            }
            heir.Purse += purse - toLegatee;
        }
        foreach (var item in HeldBy(w, dead.Id))
        {
            var to = will.Items.TryGetValue(item.Id, out var id) && w.Char(id) is { Alive: true } named ? named : heir;
            item.HolderId = to.Id; if (to.Id != heir.Id) { to.Flags.Add(LegateeChar); w.Log($"{item.Name} goes to {to.Name}, by {dead.Name}'s will."); }
        }
        // the estate: kept whole, or shared out
        var others = w.LivingCharacters().Where(c => c.Id != heir.Id && c.Age >= Adult && c.HouseId == house.Id && (c.MotherId == dead.Id || c.FatherId == dead.Id || c.AdoptedById == dead.Id))
            .OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        if (house.Inheritance == Portions && house.Treasury.Credits > 0)
        {
            int each = house.Treasury.Credits * PortionPct / 100;
            foreach (var c in others) { if (each <= 0) break; house.Treasury.Credits -= each; c.Purse += each; Regard(w, c, heir, 5); }
            if (others.Count > 0 && each > 0) w.Log($"By the custom of {house.Name}, {dead.Name}'s other grown children take {each} credits each from the treasury.");
        }
        // kept whole, the grown children passed over already resent the new head (the succession sees to that); one the will
        // remembered minds it less
        else foreach (var c in others.Where(c => c.Flags.Contains(LegateeChar))) Regard(w, c, heir, 10);
        Mark(w, WillReadFlag);
    }

    /// <summary>Someone of the house other than its head has died: what they had goes to the head.</summary>
    public static void Lapse(World w, Character dead)
    {
        var house = w.House(dead.HouseId); var head = house != null ? Economy.HeadOf(w, house) : null; if (head == null || head.Id == dead.Id) return;
        if (dead.Purse < 0 && house != null) house.Treasury.Credits += dead.Purse; else head.Purse += dead.Purse;
        dead.Purse = 0; foreach (var item in HeldBy(w, dead.Id)) item.HolderId = head.Id;
    }

    // ---------------- the year ----------------

    public static void Tick(World w)
    {
        w.WorldFlags.Remove(DebtFlag); w.WorldFlags.Remove(ResentedFlag);
        foreach (var f in DeedFlags) if (w.WorldFlags.Contains(f) && w.Counter("mark:" + f) < w.Turn - 1) { w.WorldFlags.Remove(f); w.WorldCounters.Remove("mark:" + f); }
        var p = w.Protagonist; var house = p != null ? w.House(p.HouseId) : null; if (p is not { Alive: true } || house == null) return;
        foreach (var c in w.Characters.Values) { if (!w.HasFlag(KinGaveFlag)) c.Flags.Remove(GaveChar); if (!w.HasFlag(WillReadFlag)) c.Flags.Remove(LegateeChar); }
        // the dead keep nothing; a holder who is gone or has left loses nothing, but the dead's things come home
        foreach (var dead in w.Characters.Values.Where(c => !c.Alive && (c.Purse != 0 || w.Heirlooms.Any(h => h.HolderId == c.Id))).OrderBy(c => c.Id, StringComparer.Ordinal).ToList()) Lapse(w, dead);

        int give = AllowanceOf(house.Allowance);
        foreach (var c in Members(w, house))
        {
            int income = Pay(c) + EstateIncome(w, c.Id);
            if (c.Id == p.Id) { c.Purse += income; continue; }
            // the allowance, as far as the treasury goes
            if (give > 0) { if (house.Treasury.Credits >= give) { house.Treasury.Credits -= give; income += give; } else Regard(w, c, p, -1, floor: -40); }
            c.Purse += income;
            if (house.Allowance == Mean) { Regard(w, c, p, CourtshipSystem.Wilful(c) ? -2 : -1, floor: -40); if (c.Purse <= 0) w.WorldFlags.Add(ResentedFlag); }
            else if (house.Allowance == Generous) Regard(w, c, p, 1, ceiling: 60);

            // what they do with it is their own business
            int h = (int)(Hash(c.Id + ":purse:" + w.Turn) % 100); int regard = w.GetRelationship(c.Id, p.Id)?.Disposition ?? 0;
            bool steady = c.NatureTraits.Contains("Honorable") || c.NatureTraits.Contains("Loyal");
            if (c.Purse < 0) { c.Purse -= Math.Max(1, -c.Purse / 10); c.LastSpend = $"Owes {-c.Purse} credits, and it grows."; }
            else if (house.Treasury.Credits < 0 && regard >= 30 && c.Purse >= 6 && (steady || h < 40))
            {
                int gift = Math.Min(c.Purse / 2, -house.Treasury.Credits); c.Purse -= gift; house.Treasury.Credits += gift;
                c.Flags.Add(GaveChar); Mark(w, KinGaveFlag); Regard(w, p, c, 3);
                c.LastSpend = $"Put {gift} credit{(gift == 1 ? "" : "s")} of their own into the house."; w.Log($"{c.Name} puts {gift} credits of their own into the treasury of {house.Name}, unasked.");
            }
            else if (!steady && c.Skill("discipline") < 2 && h < 12) { int lost = 5 + h % 8; c.Purse -= lost; c.LastSpend = $"Lost {lost} credits at the tables."; }
            else if ((c.NatureTraits.Contains("Proud") || c.NatureTraits.Contains("Arrogant") || c.Ambition == "outshine_all_rivals") && c.Purse >= 4)
            {
                int spent = Math.Min(c.Purse, 3 + h % 4) + (h >= 85 ? 6 : 0); c.Purse -= spent; c.LastSpend = $"Spent {spent} credits on being seen.";
            }
            else if (c.AptitudeTraits.Contains("Greedy") || c.Ambition == "amass_a_fortune") c.LastSpend = "Put it by.";
            else { c.Purse = Math.Min(c.Purse, ComfortablePurse); c.LastSpend = "Lived within their means."; }

            if (c.Purse <= DebtAt)
            {
                c.Flags.Add(InDebtChar); w.WorldFlags.Add(DebtFlag);
                if (c.Purse <= ScandalAt && (!w.WorldCounters.TryGetValue("scandal:" + c.Id, out int last) || w.Turn - last > 5))
                {
                    // the creditors come to the house
                    int taken = Math.Min(-c.Purse, Math.Max(0, house.Treasury.Credits)); house.Treasury.Credits -= taken; c.Purse += taken; house.Legitimacy -= 2;
                    w.WorldCounters["scandal:" + c.Id] = w.Turn; Mark(w, DebtScandalFlag);
                    w.Log($"{c.Name}'s creditors come to {house.Name} itself{(taken > 0 ? $" and are paid {taken} credits from its treasury" : "")}. It is talked of.");
                }
            }
            else c.Flags.Remove(InDebtChar);
        }
    }
}
