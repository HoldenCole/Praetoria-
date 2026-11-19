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
public static partial class WealthSystem
{
    public const string Mean = "mean", Customary = "customary", Generous = "generous";
    public static readonly string[] Allowances = { Mean, Customary, Generous };
    public const string Entail = "entail", Portions = "portions";
    public const int Adult = DynastySystem.AgeOfMajority, DebtAt = -8, ScandalAt = -20, PortionPct = 10, ComfortablePurse = 30;
    // each year
    public const string DebtFlag = "kin_in_debt", ResentedFlag = "allowance_resented";
    // deeds
    public const string KinGaveFlag = "kin_gave_to_the_house", WillReadFlag = "will_read", WillContestedFlag = "will_contested", HeirloomSoldFlag = "heirloom_sold", HeirloomGivenFlag = "heirloom_given", DebtScandalFlag = "debt_scandal";
    public const string InDebtChar = "in_debt", GaveChar = "gave_to_the_house", LegateeChar = "remembered_in_the_will";
    private static readonly string[] DeedFlags = { KinGaveFlag, WillReadFlag, WillContestedFlag, HeirloomSoldFlag, HeirloomGivenFlag, DebtScandalFlag, LoanRepaidFlag, LoanSouredFlag, KinLentFlag, HeadOwesFlag, EntailBrokenFlag, StandardWonFlag, WeddingGiftFlag, StandardAskedFlag, KinMadeFlag, MendFlag, PlunderedFlag, AsksFavourFlag };

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
    public static int Pay(Character c) => c.CareerTrack.Length > 0 ? c.CareerRank + 1 : 0;
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
        foreach (var h in w.Houses.Values.Where(h => !World.Outsider(h.Id) && (h.Origin == "old" || Title.RankOf(h.Title) >= 3)).OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            var head = Economy.HeadOf(w, h); if (head == null) continue;
            string name = h.Name.Replace("House ", ""); uint x = Hash(h.Id + ":heirloom");
            var (kind, what) = (x % 4) switch { 0u => ("signet", $"the {name} signet"), 1u => ("sword", $"the sword of {h.Name}"), 2u => ("library", $"the {name} library"), _ => ("estate", $"the old {name} estate") };
            w.Heirlooms.Add(new Heirloom { Id = "heirloom_" + h.Id, Name = what, Kind = kind, HolderId = head.Id, OfHouseId = h.Id, Value = 15 + (int)(x / 7 % 26), Entailed = h.Origin == "old" });
        }
    }

    public static string? GiveRefusal(World w, Character? giver, Heirloom? item, Character? to)
    {
        if (giver?.Alive != true || item == null || item.HolderId != giver.Id) return "not yours to give";
        if (to?.Alive != true || to.Id == giver.Id) return "no such person";
        if (item.Entailed) return "it is entailed: it goes with the house";
        if (TakesRefusal(w, giver, item, to) is { } unwilling) return unwilling;
        return to.Age >= 6 ? null : "too young to be given it";
    }

    public static void Give(World w, Character giver, Heirloom item, Character to)
    {
        if (IsSpoil(item) && to.HouseId == item.FromHouseId) { ReturnStandard(w, item, giver, to, 0); Mark(w, HeirloomGivenFlag); return; }   // it goes home
        // given to the head of another house, a thing is a favour asked: that house owes one (docs/WEALTH.md "A gift that asks")
        if (to.HouseId != giver.HouseId && w.House(to.HouseId) is { } theirs && Economy.HeadOf(w, theirs)?.Id == to.Id && giver.HouseId == w.Protagonist?.HouseId) Owe(w, theirs.Id, giver.HouseId);
        item.HolderId = to.Id; Regard(w, to, giver, to.HouseId == giver.HouseId ? 15 : 20);
        if (giver.Will != null) giver.Will.Items.Remove(item.Id);
        if (to.HouseId != item.OfHouseId && w.House(item.OfHouseId) is { } of && of.Id == giver.HouseId) of.Legitimacy -= 1;
        Mark(w, HeirloomGivenFlag);
        w.Log($"{giver.Name} gives {item.Name} to {to.Name}.");
    }

    public static string? SellRefusal(World w, Character? seller, Heirloom? item) =>
        seller?.Alive != true || item == null || item.HolderId != seller.Id ? "not yours to sell" : item.Entailed ? "it is entailed: it goes with the house" : null;
    public static string? BequeathRefusal(World w, Character? who, Heirloom? item, string toId) =>
        who?.Alive != true || item == null || item.HolderId != who.Id ? "not yours to leave" : item.Entailed && toId.Length > 0 ? "it is entailed: it goes with the house" : toId.Length == 0 || w.Char(toId) is { Alive: true } ? null : "no such person";

    // ---------------- the entail, and what a thing handed down does ----------------

    public const int EntailSway = 2, BreakLegitimacy = 3, BreakRegard = 6, SignetMendsBelow = 50, SwordPoints = 2, LibraryPoints = 1;
    public const string EntailBrokenFlag = "entail_broken";

    /// <summary>Only the head of the house a thing has always belonged to can entail it, or break the entail.</summary>
    public static string? EntailRefusal(World w, Character? head, Heirloom? item, bool on)
    {
        var house = head != null ? w.House(head.HouseId) : null; if (head?.Alive != true || house == null || item == null || item.HolderId != head.Id) return "not yours";
        if (Economy.HeadOf(w, house)?.Id != head.Id) return "only the head of the house can bind it";
        if (item.OfHouseId != house.Id) return "it is not of your house's name";
        return item.Entailed == on ? (on ? "it is entailed already" : "it is not entailed") : null;
    }
    /// <summary>Entail it: it goes with the house from now on, to whoever has the house, and cannot be sold, given or willed away. The
    /// old families approve. Breaking an entail is done in a court, and is remembered by the family.</summary>
    public static void SetEntail(World w, Character head, Heirloom item, bool on)
    {
        var house = w.House(head.HouseId)!; item.Entailed = on;
        if (on)
        {
            if (head.Will != null) head.Will.Items.Remove(item.Id);
            PolicySystem.Sway(w, house.Id, "old_blood", EntailSway); w.Log($"{head.Name} entails {item.Name}: it goes with {house.Name} from now on.");
        }
        else
        {
            house.Legitimacy -= BreakLegitimacy; PolicySystem.Sway(w, house.Id, "old_blood", -EntailSway - 1);
            foreach (var k in Members(w, house).Where(k => k.Id != head.Id && !HouseholdSystem.IsFound(k))) Regard(w, k, head, -BreakRegard);
            Mark(w, EntailBrokenFlag); w.Log($"{head.Name} has the entail on {item.Name} broken in court. It is theirs to sell now, and the family knows why.");
        }
    }

    /// <summary>What a thing handed down does for whoever holds it (docs/WEALTH.md), in a line.</summary>
    public static string Does(World w, Heirloom item)
    {
        var holder = w.Char(item.HolderId); var of = w.House(item.OfHouseId); bool home = holder != null && holder.HouseId == item.OfHouseId;
        string taken = IsSpoil(item) && w.House(item.FromHouseId) is { } lostBy && lostBy.Id != holder?.HouseId ? $" {lostBy.Name} has not forgotten whose it was." : "";
        return taken.Length > 0 && item.Kind == "signet" ? "A trophy: a signet does nothing off the hand of the head of its own house." + taken : DoesAlone(w, item, holder, of, home) + taken;
    }
    private static string DoesAlone(World w, Heirloom item, Character? holder, House? of, bool home)
    {
        return item.Kind switch
        {
            "signet" => home && of != null && Economy.HeadOf(w, of)?.Id == holder!.Id ? $"On the head's hand, the house's name mends a point a year while its standing is under {SignetMendsBelow}." : "Does nothing off the hand of the head of its own house.",
            "sword" => holder?.CareerTrack == "military" ? "Carried in the fleet, it is something to live up to: its holder learns discipline faster." : "Does nothing on a wall: it wants carrying by someone in the fleet.",
            "library" => "Everyone of its holder's house who has set themselves a study learns it a little faster.",
            "estate" => "Brings its holder 2 credits a year.",
            Regalia => holder != null && w.House(holder.HouseId) is { } wornBy && Economy.HeadOf(w, wornBy)?.Id == holder.Id ? $"Kept by the head of a house, the house's name mends a point a year while its standing is under {RegaliaMendsBelow}." : "Does nothing in a relative's keeping: it wants the head of a house.",
            Shrine => $"The commons think a point better of its holder's house each year, while they think less than {ShrineCeiling} of it.",
            Standard => (holder != null && FlagshipOf(w, holder.Id) != null ? $"Flown from a flagship, the fleet under it is a point more loyal each year, up to {StandardCeiling}." : "Does nothing in a hall: it wants flying from the flagship of whoever holds it."),
            Orrery => (holder?.Study.Length > 0 ? "Its holder learns what they have set themselves to study a point faster each year." : "Does nothing for one who studies nothing.") + (w.House(item.FromHouseId) is { } sentBy && holder != null && FavourOwed(w, holder.HouseId, sentBy.Id) ? $" It came from {sentBy.Name}, which has a favour to call in for it." : ""),
            Plate => w.House(item.FromHouseId) is { } from && from.Id != holder?.HouseId ? $"A gift from {from.Name}: its holder and the head of that house think a point better of each other each year, up to {PlateCeiling}. Selling it would be taken badly." : "A gift that has come back to the house that gave it. It does nothing here.",
            _ => ""
        };
    }
    public static bool HasLibrary(World w, string houseId) => w.Heirlooms.Any(h => h.Kind == "library" && w.Char(h.HolderId) is { Alive: true } c && c.HouseId == houseId);

    /// <summary>The year's work of things handed down, for every house: an entailed thing follows the house; a signet mends a name;
    /// a sword in the fleet teaches. (The library is read in the schoolroom: <see cref="EducationSystem"/>.)</summary>
    private static void Heirlooms(World w)
    {
        AnswerStandards(w);
        foreach (var item in w.Heirlooms.OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            var of = w.House(item.OfHouseId); var head = of != null ? Economy.HeadOf(w, of) : null; var holder = w.Char(item.HolderId);
            if (item.Entailed && head != null && holder?.Id != head.Id)
            {
                item.HolderId = head.Id; holder = head;
                if (of!.Id == w.Protagonist?.HouseId) w.Log($"{item.Name} is entailed, and goes to {head.Name} with the house.");
            }
            if (holder?.Alive != true) continue;
            // the house a thing was taken from does not forget who has it
            if (IsSpoil(item) && w.House(item.FromHouseId) is { } lostBy && lostBy.Id != holder.HouseId && Economy.HeadOf(w, lostBy) is { Alive: true } theirHead) Regard(w, theirHead, holder, -1, floor: StandardGrudgeFloor);
            if (item.Kind == "signet" && of != null && head?.Id == holder.Id && of.Legitimacy < SignetMendsBelow) of.Legitimacy += 1;
            else if (item.Kind == Regalia) { if (w.House(holder.HouseId) is { } worn && Economy.HeadOf(w, worn)?.Id == holder.Id && worn.Legitimacy < RegaliaMendsBelow) worn.Legitimacy += 1; }
            else if (item.Kind == "sword" && holder.CareerTrack == "military") EducationSystem.Gain(w, holder, "discipline", SwordPoints);
            else MadeThings(w, item, holder);
        }
        OthersCommission(w); StandardsAsked(w); GiftsToMend(w); GiftsThatAsk(w); LapseFavours(w);
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
        Mark(w, HeirloomSoldFlag); SoldAGift(w, seller, item);
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

    // ---------------- a sum to a relative: given, or lent ----------------

    public const int DowryRegard = 6, LoanSoursAfter = 6; public const string LoanRepaidFlag = "loan_repaid", LoanSouredFlag = "loan_soured", OwesTooLongChar = "owes_too_long", LoanForgivenChar = "loan_forgiven";
    private static string GiftKey(string id) => "gift_year:" + id;

    public static string? GiftRefusal(World w, Character? head, Character? to, int amount, bool lend)
    {
        if (head?.Alive != true || to?.Alive != true || to.Id == head.Id || to.HouseId != head.HouseId) return "not of your house";
        if (to.Age < Adult) return "they are a child, and have no purse of their own";
        if (amount <= 0) return "not a sum";
        if (head.Purse < amount) return "your purse does not hold it";
        if (lend && to.OwesHead > 0) return "they owe you already";
        return !lend && w.Counter(GiftKey(to.Id)) == w.Turn ? "you have given to them this year" : null;
    }
    /// <summary>The head gives one of the house a sum out of their own purse. It is theirs, and they know whose it was.</summary>
    public static void Gift(World w, Character head, Character to, int amount)
    {
        head.Purse -= amount; to.Purse += amount; w.WorldCounters[GiftKey(to.Id)] = w.Turn; if (to.Purse > DebtAt) to.Flags.Remove(InDebtChar);
        Regard(w, to, head, Math.Min(10, 2 + amount / 2)); w.Log($"{head.Name} gives {to.Name} {amount} credits of their own.");
    }
    /// <summary>The head lends one of the house a sum. They pay it back as they can; a debt that sits too long sits between the two.</summary>
    public static void Lend(World w, Character head, Character to, int amount)
    {
        head.Purse -= amount; to.Purse += amount; to.OwesHead = amount; to.OwesSince = w.Turn; if (to.Purse > DebtAt) to.Flags.Remove(InDebtChar);
        Regard(w, to, head, 3); w.Log($"{head.Name} lends {to.Name} {amount} credits.");
    }
    /// <summary>The same loan with a fifth on top. It is business, and is taken as business.</summary>
    public static int WithInterest(int amount) => amount + Math.Max(1, amount / InterestPart);
    public static void LendAtInterest(World w, Character head, Character to, int amount)
    {
        head.Purse -= amount; to.Purse += amount; to.OwesHead = WithInterest(amount); to.OwesSince = w.Turn; if (to.Purse > DebtAt) to.Flags.Remove(InDebtChar);
        Regard(w, to, head, -1); w.Log($"{head.Name} lends {to.Name} {amount} credits, to be paid back as {to.OwesHead}.");
    }

    public const int InterestPart = 5, LendsAtRegard = 20, KeepsBack = 2, KinLendsAtRegard = 30, KinLendsFrom = 6;
    public const string KinLentFlag = "kin_lent_to_kin", HeadOwesFlag = "head_owes_too_long", KinLentChar = "lent_to_kin", HeadOwesChar = "is_owed_by_the_head";
    private static bool Grasping(Character c) => c.AptitudeTraits.Contains("Greedy") || c.Ambition == "amass_a_fortune";

    /// <summary>The head asks one of the house for a loan out of that person's own purse. It is theirs to refuse.</summary>
    public static string? BorrowRefusal(World w, Character? head, Character? from, int amount)
    {
        if (head?.Alive != true || from?.Alive != true || from.Id == head.Id || from.HouseId != head.HouseId) return "not of your house";
        if (from.Age < Adult) return "they are a child, and have no purse of their own";
        if (amount <= 0) return "not a sum";
        if (from.LentHead > 0) return "you owe them already";
        if (from.OwesHead > 0) return "they owe you: ask for that back instead";
        if (from.Purse < amount + KeepsBack) return "their purse does not hold it";
        return from.NatureTraits.Contains("Loyal") || (w.GetRelationship(from.Id, head.Id)?.Disposition ?? 0) >= LendsAtRegard ? null : "they will not lend it to you";
    }
    /// <summary>What the head will owe for it: the sum, and a fifth on top to one who counts such things.</summary>
    public static int BorrowOwed(Character from, int amount) => Grasping(from) ? WithInterest(amount) : amount;
    public static void Borrow(World w, Character head, Character from, int amount)
    {
        from.Purse -= amount; head.Purse += amount; from.LentHead = BorrowOwed(from, amount); from.LentSince = w.Turn;
        w.Log($"{from.Name} lends {head.Name} {amount} credits of their own{(from.LentHead > amount ? $", to be paid back as {from.LentHead}" : "")}.");
    }
    public static string? RepayRefusal(World w, Character? head, Character? to) =>
        head?.Alive != true || to?.Alive != true || to.HouseId != head.HouseId ? "not of your house" : to.LentHead <= 0 ? "you owe them nothing" : head.Purse <= 0 ? "your purse is empty" : null;
    /// <summary>Pay back what the head borrowed, as far as their purse goes.</summary>
    public static void Repay(World w, Character head, Character to)
    {
        int pay = Math.Min(to.LentHead, head.Purse); head.Purse -= pay; to.Purse += pay; to.LentHead -= pay;
        if (to.LentHead == 0) { bool late = w.Turn - to.LentSince >= LoanSoursAfter; to.LentSince = -1; to.Flags.Remove(HeadOwesChar); Regard(w, to, head, late ? 2 : 5); w.Log($"{head.Name} pays {to.Name} back the last of what they lent."); }
        else w.Log($"{head.Name} pays {to.Name} back {pay} credits, and owes {to.LentHead} still.");
    }

    public static string? ForgiveRefusal(World w, Character? head, Character? to) =>
        head?.Alive != true || to?.Alive != true || to.HouseId != head.HouseId ? "not of your house" : to.OwesHead <= 0 ? "they owe you nothing" : null;
    public static void Forgive(World w, Character head, Character to)
    {
        int owed = to.OwesHead; to.OwesHead = 0; to.OwesSince = -1; Regard(w, to, head, 8); w.Log($"{head.Name} forgives {to.Name} the {owed} credits they owed.");
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
        // what the dead head borrowed of their own people is paid before anything is shared: from the purse, then from the house
        foreach (var c in w.LivingCharacters().Where(c => c.LentHead > 0 && c.HouseId == house.Id).OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
        {
            int fromPurse = Math.Clamp(dead.Purse, 0, c.LentHead), fromHouse = c.Id == heir.Id ? 0 : Math.Clamp(house.Treasury.Credits, 0, c.LentHead - fromPurse);
            dead.Purse -= fromPurse; house.Treasury.Credits -= fromHouse; c.Purse += fromPurse + fromHouse;
            if (fromPurse + fromHouse < c.LentHead && c.Id != heir.Id) { Regard(w, c, heir, -5); w.Log($"{dead.Name} died owing {c.Name} {c.LentHead - fromPurse - fromHouse} credits that nobody will see now."); }
            c.LentHead = 0; c.LentSince = -1; c.Flags.Remove(HeadOwesChar);
        }
        CompanySystem.Leave(w, dead, heir, legatee, will.SharePct);   // shares in the dead head's own name go as the purse goes
        VentureSystem.Leave(w, dead, legatee != null && will.SharePct >= 50 ? legatee : heir);   // and a venture that was their own goes to whoever has the larger part
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
            var to = !item.Entailed && will.Items.TryGetValue(item.Id, out var id) && w.Char(id) is { Alive: true } named ? named : heir;
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
        if (dead.LentHead > 0) { int pay = Math.Clamp(head.Purse, 0, dead.LentHead); head.Purse -= pay; dead.LentHead = 0; dead.LentSince = -1; }
        dead.OwesKin = 0; dead.OwesKinId = ""; CompanySystem.Leave(w, dead, head); VentureSystem.Leave(w, dead, null);   // their own venture is the house's again
        dead.Purse = 0; dead.OwesHead = 0; dead.OwesSince = -1; foreach (var item in HeldBy(w, dead.Id)) item.HolderId = head.Id;   // what the dead owed is written off
    }

    // ---------------- the year ----------------

    public static void Tick(World w)
    {
        w.WorldFlags.Remove(DebtFlag); w.WorldFlags.Remove(ResentedFlag);
        foreach (var f in DeedFlags) if (w.WorldFlags.Contains(f) && w.Counter("mark:" + f) < w.Turn - 1) { w.WorldFlags.Remove(f); w.WorldCounters.Remove("mark:" + f); }
        Heirlooms(w);
        var p = w.Protagonist; var house = p != null ? w.House(p.HouseId) : null; if (p is not { Alive: true } || house == null) return;
        foreach (var c in w.Characters.Values) { if (!w.HasFlag(KinGaveFlag)) c.Flags.Remove(GaveChar); if (!w.HasFlag(WillReadFlag)) c.Flags.Remove(LegateeChar); if (!w.HasFlag(KinLentFlag)) c.Flags.Remove(KinLentChar); if (!w.HasFlag(StandardWonFlag)) c.Flags.Remove(WonStandardChar); if (!w.HasFlag(WeddingGiftFlag)) c.Flags.Remove(GaveGiftChar); if (!w.HasFlag(StandardAskedFlag)) c.Flags.Remove(AsksChar); if (!w.HasFlag(KinMadeFlag)) c.Flags.Remove(KinMadeChar); if (!w.HasFlag(MendFlag)) c.Flags.Remove(MendChar); if (!w.HasFlag(AsksFavourFlag)) c.Flags.Remove(AsksFavourChar); }
        // the dead keep nothing; a holder who is gone or has left loses nothing, but the dead's things come home
        foreach (var dead in w.Characters.Values.Where(c => !c.Alive && (c.Purse != 0 || w.Heirlooms.Any(h => h.HolderId == c.Id) || CompanySystem.HeldPersonally(w, c.Id).Count > 0 || w.Businesses.Values.Any(b => b.HeldById == c.Id))).OrderBy(c => c.Id, StringComparer.Ordinal).ToList()) Lapse(w, dead);

        int give = AllowanceOf(house.Allowance);
        foreach (var c in Members(w, house))
        {
            int income = Pay(c) + EstateIncome(w, c.Id);
            if (c.Id == p.Id) { c.Purse += income; c.OwesHead = 0; c.OwesSince = -1; c.LentHead = 0; c.LentSince = -1; c.Flags.Remove(HeadOwesChar); continue; }   // one who has the house now owes its head nothing
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
            else if (KinCommission(w, c, h) != null) { }
            else if (!steady && c.Skill("discipline") < 2 && h < 12) { int lost = 5 + h % 8; c.Purse -= lost; c.LastSpend = $"Lost {lost} credits at the tables."; }
            else if ((c.NatureTraits.Contains("Proud") || c.NatureTraits.Contains("Arrogant") || c.Ambition == "outshine_all_rivals") && c.Purse >= 4)
            {
                int spent = Math.Min(c.Purse, 3 + h % 4) + (h >= 85 ? 6 : 0); c.Purse -= spent; c.LastSpend = $"Spent {spent} credits on being seen.";
            }
            else if (c.AptitudeTraits.Contains("Greedy") || c.Ambition == "amass_a_fortune") c.LastSpend = "Put it by.";
            else { c.Purse = Math.Min(c.Purse, ComfortablePurse); c.LastSpend = "Lived within their means."; }

            // what they were lent by the head: paid back as they can; left too long, it sits between the two
            if (c.Flags.Remove(LoanForgivenChar) && c.OwesHead > 0) { c.OwesHead = 0; c.OwesSince = -1; }   // the head said it was forgotten (an event's choice)
            if (!w.HasFlag(LoanSouredFlag)) c.Flags.Remove(OwesTooLongChar);
            if (c.OwesHead > 0)
            {
                if (c.Purse >= 4 && (steady || h < 60))
                {
                    int pay = Math.Min(c.OwesHead, Math.Max(1, c.Purse / 3)); c.Purse -= pay; p.Purse += pay; c.OwesHead -= pay;
                    if (c.OwesHead == 0) { c.OwesSince = -1; Regard(w, c, p, 2); Mark(w, LoanRepaidFlag); w.Log($"{c.Name} has paid back the last of what {p.Name} lent them."); }
                }
                else if (w.Turn - c.OwesSince >= LoanSoursAfter)
                {
                    Regard(w, c, p, -1, floor: -30); Regard(w, p, c, -1, floor: -30);
                    if (w.Turn - c.OwesSince == LoanSoursAfter) { Mark(w, LoanSouredFlag); c.Flags.Add(OwesTooLongChar); w.Log($"{c.Name} has owed {p.Name} {c.OwesHead} credits for {LoanSoursAfter} years, and neither of them mentions it."); }
                }
            }

            // what the head borrowed of them: left too long, they mind it, and say so sooner than the head would
            if (c.LentHead > 0 && w.Turn - c.LentSince >= LoanSoursAfter)
            {
                Regard(w, c, p, -2, floor: -30);
                if (w.Turn - c.LentSince == LoanSoursAfter) { Mark(w, HeadOwesFlag); c.Flags.Add(HeadOwesChar); w.Log($"{p.Name} has owed {c.Name} {c.LentHead} credits for {LoanSoursAfter} years."); }
            }
            else if (c.LentHead == 0 || !w.HasFlag(HeadOwesFlag)) c.Flags.Remove(HeadOwesChar);

            // what one of the house lent another: paid back as they can; written off if the lender is gone
            if (c.OwesKin > 0)
            {
                var lender = w.Char(c.OwesKinId);
                if (lender?.Alive != true || lender.HouseId != house.Id) { c.OwesKin = 0; c.OwesKinId = ""; }
                else if (c.Purse >= 4)
                {
                    int pay = Math.Min(c.OwesKin, Math.Max(1, c.Purse / 3)); c.Purse -= pay; lender.Purse += pay; c.OwesKin -= pay;
                    if (c.OwesKin == 0) { c.OwesKinId = ""; Regard(w, lender, c, 3); }
                }
            }
            // one in debt is helped by another of the house who has it to spare and thinks well of them
            if (c.Purse <= DebtAt && c.OwesKin == 0 && w.FamilyAutonomy > 0)
            {
                var lender = Members(w, house).Where(k => k.Id != c.Id && k.Id != p.Id && k.Purse >= KinLendsFrom && (w.GetRelationship(k.Id, c.Id)?.Disposition ?? 0) >= KinLendsAtRegard)
                    .OrderByDescending(k => w.GetRelationship(k.Id, c.Id)?.Disposition ?? 0).ThenBy(k => k.Id, StringComparer.Ordinal).FirstOrDefault();
                if (lender != null)
                {
                    int sum = Math.Min(-c.Purse, lender.Purse / 2); lender.Purse -= sum; c.Purse += sum; c.OwesKinId = lender.Id; c.OwesKin = Grasping(lender) ? WithInterest(sum) : sum;
                    Regard(w, c, lender, 6); c.Flags.Add(KinLentChar); lender.Flags.Add(KinLentChar); Mark(w, KinLentFlag);
                    w.Log($"{lender.Name} lends {c.Name} {sum} credits of their own, without being asked to.");
                }
            }

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
