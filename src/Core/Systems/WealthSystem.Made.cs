using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>How a house comes by a thing it was not born with (docs/WEALTH.md "New things"): one is commissioned out of a
/// person's own purse, one is taken in a battle by whoever commanded, one is given by another house at a wedding. Each is a
/// kind of its own and does its own thing.</summary>
public static partial class WealthSystem
{
    public const string Shrine = "shrine", Standard = "standard", Plate = "plate", Library = "library";
    public static readonly string[] Commissions = { Shrine, Library };
    public const int ShrineCost = 24, LibraryCost = 36, MadeWorthPct = 66, ShrineCeiling = 40, StandardCeiling = 90, PlateCeiling = 40;
    public const int StandardOneIn = 4, StandardsAHouse = 2, StandardValue = 12, PlateOneIn = 12, PlateValue = 14, SoldGiftRegard = 12;
    public const int OthersCommissionAt = 80, OthersCommissionOneIn = 80;
    public const string StandardWonFlag = "standard_won", WonStandardChar = "won_a_standard", WeddingGiftFlag = "wedding_gift_given", GaveGiftChar = "gave_wedding_plate";
    public const string CommissionedKey = "heirlooms_commissioned", StandardsKey = "standards_won", WeddingGiftsKey = "wedding_gifts", SoldKey = "heirlooms_sold";

    private static void Count(World w, string key) => w.WorldCounters[key] = w.Counter(key) + 1;
    private static string Bare(House h) => h.Name.Replace("House ", "");

    public static int CommissionCost(string kind) => kind == Library ? LibraryCost : ShrineCost;

    /// <summary>A person has a thing made out of their own purse. One of a kind to a house: a second shrine is only a bigger one.</summary>
    public static string? CommissionRefusal(World w, Character? who, string kind)
    {
        var house = who != null ? w.House(who.HouseId) : null; if (who?.Alive != true || house == null) return "no house";
        if (!Commissions.Contains(kind)) return "nobody can make that to order";
        if (who.Age < Adult) return "too young to order it";
        if (w.Heirlooms.Any(h => h.Kind == kind && h.OfHouseId == house.Id)) return $"your house has a {kind} already";
        return who.Purse >= CommissionCost(kind) ? null : $"your purse does not hold the {CommissionCost(kind)} credits it costs";
    }

    public static Heirloom Commission(World w, Character who, string kind)
    {
        var house = w.House(who.HouseId)!; who.Purse -= CommissionCost(kind);
        var item = Make(w, house, who, kind, CommissionCost(kind) * MadeWorthPct / 100);
        LifeSystem.Note(w, who, "deed", $"Had {item.Name} made.");
        w.Log($"{who.Name} has {item.Name} made, out of their own purse. It is theirs, and the house's after them.");
        return item;
    }

    private static Heirloom Make(World w, House house, Character holder, string kind, int value)
    {
        string name = kind == Library ? $"the {Bare(house)} library" : $"the {Bare(house)} shrine";
        var item = new Heirloom { Id = $"made_{kind}_{house.Id}_{w.Turn}", Name = name, Kind = kind, HolderId = holder.Id, OfHouseId = house.Id, Value = value };
        w.Heirlooms.Add(item); Count(w, CommissionedKey); return item;
    }

    /// <summary>Other houses have things made too, when they are rich enough to think of it. Their heads have no purse the
    /// game keeps, so it comes out of the house.</summary>
    private static void OthersCommission(World w)
    {
        string? mine = w.Protagonist?.HouseId;
        foreach (var h in w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId && h.Id != mine && h.Treasury.Credits >= OthersCommissionAt).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            var head = Economy.HeadOf(w, h); if (head?.Alive != true) continue;
            uint x = Hash($"{h.Id}:{w.Turn}:commission") / 7; if (x % OthersCommissionOneIn != 0) continue;   // the low bits of the hash follow the id too closely to be used bare
            string kind = Commissions[(int)(x / 17 % (uint)Commissions.Length)];
            if (w.Heirlooms.Any(i => i.Kind == kind && i.OfHouseId == h.Id)) continue;
            h.Treasury.Credits -= CommissionCost(kind); Make(w, h, head, kind, CommissionCost(kind) * MadeWorthPct / 100);
        }
    }

    /// <summary>A battle is won, and sometimes whoever commanded brings the other side's standard home. It is theirs: they
    /// took it. A house keeps no more than <see cref="StandardsAHouse"/>; raiders fly nothing worth taking.</summary>
    public static Heirloom? StandardWon(World w, string winnerId, string loserId, string systemId, IReadOnlyList<Character> admirals)
    {
        var house = w.House(winnerId); var beaten = w.House(loserId);
        if (house == null || beaten == null || winnerId == PirateSystem.HouseId || loserId == PirateSystem.HouseId || admirals.Count == 0) return null;
        string where = w.Galaxy?.System(systemId)?.Name ?? systemId;
        // a standard of the winner's that the beaten house flies is what the day was for: it comes home
        var lost = w.Heirlooms.Where(h => h.Kind == Standard && h.FromHouseId == winnerId && w.Char(h.HolderId) is { } hc && hc.HouseId == loserId).OrderBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
        if (lost != null)
        {
            lost.HolderId = admirals[0].Id; lost.OfHouseId = winnerId; lost.FromHouseId = ""; lost.Entailed = false; lost.Name = $"the {Bare(house)} standard, lost and retaken at {where}"; Count(w, RetakenKey);
            LifeSystem.Note(w, admirals[0], "victory", $"Retook the house's standard at {where}.");
            if (winnerId == w.Protagonist?.HouseId) w.Log($"{admirals[0].Name} retakes {lost.Name}.");
            return lost;
        }
        if (Hash($"{systemId}:{w.Turn}:{winnerId}:{loserId}:standard") / 7 % StandardOneIn != 0) return null;
        if (w.Heirlooms.Count(h => h.Kind == Standard && h.OfHouseId == winnerId) >= StandardsAHouse) return null;
        var who = admirals[0]; string id = $"standard_{winnerId}_{w.Turn}_{systemId}"; if (w.Heirlooms.Any(h => h.Id == id)) return null;
        string place = w.Galaxy?.System(systemId)?.Name ?? systemId;
        var item = new Heirloom { Id = id, Name = $"the {Bare(beaten)} standard taken at {place}", Kind = Standard, HolderId = who.Id, OfHouseId = winnerId, FromHouseId = loserId, Value = StandardValue };
        w.Heirlooms.Add(item); Count(w, StandardsKey);
        LifeSystem.Note(w, who, "victory", $"Took the {Bare(beaten)} standard at {place}.");
        if (winnerId == w.Protagonist?.HouseId)
        {
            w.Log($"{who.Name} brings home {item.Name}.");
            if (who.Id != w.ProtagonistId) { Mark(w, StandardWonFlag); who.Flags.Add(WonStandardChar); }
        }
        return item;
    }

    /// <summary>Two houses marry, and sometimes the one that did best out of it sends plate: to the bride if a dowry came
    /// with her, to the groom if none did. It is the receiver's own, and it keeps the two of them in mind of each other. The
    /// player's own gifts are the player's to make: nothing is sent in their name.</summary>
    public static Heirloom? WeddingGift(World w, Character bride, Character groom, int dowryPaid)
    {
        if (w.Turn <= 0 || bride.HouseId == groom.HouseId) return null;
        var to = dowryPaid > 0 ? bride : groom; var fromHouse = w.House(dowryPaid > 0 ? groom.HouseId : bride.HouseId); var toHouse = w.House(to.HouseId);
        if (fromHouse == null || toHouse == null || fromHouse.Id == PirateSystem.HouseId || toHouse.Id == PirateSystem.HouseId || fromHouse.Id == w.Protagonist?.HouseId) return null;
        var giver = Economy.HeadOf(w, fromHouse); if (giver?.Alive != true) return null;
        if (Hash($"{bride.Id}:{groom.Id}:plate") / 7 % PlateOneIn != 0) return null;
        string id = $"plate_{bride.Id}_{groom.Id}"; if (w.Heirlooms.Any(h => h.Id == id)) return null;
        var item = new Heirloom { Id = id, Name = $"the {Bare(fromHouse)} wedding plate", Kind = Plate, HolderId = to.Id, OfHouseId = toHouse.Id, FromHouseId = fromHouse.Id, Value = PlateValue };
        w.Heirlooms.Add(item); Count(w, WeddingGiftsKey); Regard(w, to, giver, 8);
        if (toHouse.Id == w.Protagonist?.HouseId)
        {
            Mark(w, WeddingGiftFlag); giver.Flags.Add(GaveGiftChar);
            w.Log($"{giver.Name} of {fromHouse.Name} sends plate for the wedding. It is {to.Name}'s own.");
        }
        return item;
    }

    /// <summary>The fleet whoever holds a standard commands, if they command one.</summary>
    public static Fleet? FlagshipOf(World w, string holderId) =>
        w.Fleets.Values.Where(f => f.AdmiralId == holderId).OrderBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();

    /// <summary>The year's work of the three kinds a house comes by.</summary>
    private static void MadeThings(World w, Heirloom item, Character holder)
    {
        if (item.Kind == Shrine) { if (PolicySystem.Approval(w, holder.HouseId, "commons") < ShrineCeiling) PolicySystem.Sway(w, holder.HouseId, "commons", 1); }
        else if (item.Kind == Standard)
        {
            if (FlagshipOf(w, holder.Id) is { } f && f.Loyalty < StandardCeiling) f.Loyalty += 1;
            // the house it was taken from does not forget who flies it
            if (w.House(item.FromHouseId) is { } lostBy && lostBy.Id != holder.HouseId && Economy.HeadOf(w, lostBy) is { Alive: true } theirHead) Regard(w, theirHead, holder, -1, floor: StandardGrudgeFloor);
        }
        else if (item.Kind == Plate && w.House(item.FromHouseId) is { } from && from.Id != holder.HouseId && Economy.HeadOf(w, from) is { Alive: true } giver)
        { Regard(w, holder, giver, 1, ceiling: PlateCeiling); Regard(w, giver, holder, 1, ceiling: PlateCeiling); }
    }

    /// <summary>A gift sold is an answer to whoever gave it.</summary>
    private static void SoldAGift(World w, Character seller, Heirloom item)
    {
        Count(w, SoldKey);
        if (item.Kind != Plate || w.House(item.FromHouseId) is not { } from || from.Id == seller.HouseId) return;
        var giver = Economy.HeadOf(w, from); if (giver?.Alive != true) return;
        Regard(w, giver, seller, -SoldGiftRegard); w.Log($"{giver.Name} of {from.Name} hears what became of the plate they sent.");
    }

    // ---------------- a standard asked back, a relative's own commission, a gift to mend a quarrel (docs/WEALTH.md, W9) ----------------

    public const int StandardGrudgeFloor = -40, RansomTimes = 3, ReturnRegard = 25, RansomRegard = 5, AskOneIn = 4, AskEvery = 5, OthersRansomOneIn = 8, RansomWillNot = -20;
    public const int KinMakesOneIn = 4, KinKeepsBack = 6, MendOneIn = 30, MendFrom = -45, MendTo = -15, MendRegard = 8, MendAt = 60;
    public const string RetakenKey = "standards_retaken", ReturnedKey = "standards_returned", RansomedKey = "standards_ransomed", KinMadeKey = "kin_commissions", MendKey = "gifts_between_houses", SentBackKey = "gifts_sent_back";
    public const string StandardAskedFlag = "standard_asked_back", AsksChar = "asks_for_their_standard", GivenBackFlag = "standard_given_back", RansomAgreedFlag = "standard_ransom_agreed";
    public const string KinMadeFlag = "kin_had_a_thing_made", KinMadeChar = "had_a_thing_made", MendFlag = "gift_to_mend", MendChar = "sent_a_gift", SendBackFlag = "gift_sent_back";
    private static string AskedKey(string itemId) => "standard_asked:" + itemId;
    public static int RansomPrice(Heirloom item) => item.Value * RansomTimes;

    /// <summary>The standard goes back to the house it was taken from, to its head. Given freely it is a great thing; paid for,
    /// it is a bargain. Either way it is that house's own again, with nothing owed.</summary>
    public static void ReturnStandard(World w, Heirloom item, Character? from, Character to, int paid)
    {
        var home = w.House(to.HouseId); item.HolderId = to.Id; item.OfHouseId = to.HouseId; item.FromHouseId = ""; item.Entailed = false;
        item.Name = $"the {(home != null ? Bare(home) : "old")} standard, come home"; if (from?.Will != null) from.Will.Items.Remove(item.Id);
        Regard(w, to, from, paid > 0 ? RansomRegard : ReturnRegard); Count(w, paid > 0 ? RansomedKey : ReturnedKey); w.WorldCounters.Remove(AskedKey(item.Id));
        if (from?.HouseId == w.Protagonist?.HouseId || to.HouseId == w.Protagonist?.HouseId)
            w.Log(paid > 0 ? $"{item.Name}: {to.Name} pays {paid} credits and has it back." : $"{from?.Name} sends {to.Name} their standard back, and asks nothing for it.");
    }

    /// <summary>Your house's standard is flown by another. Its holder's house will sell it home if its head can bear you; at war
    /// there is only one way to have it back.</summary>
    public static string? RansomRefusal(World w, Character? me, Heirloom? item)
    {
        if (me?.Alive != true || item == null || item.Kind != Standard || item.FromHouseId != me.HouseId) return "not a standard of your house";
        var holder = w.Char(item.HolderId); var theirs = holder != null ? w.House(holder.HouseId) : null; if (holder?.Alive != true || theirs == null || theirs.Id == me.HouseId) return "nobody else holds it";
        if (w.AtWar(me.HouseId, theirs.Id)) return "you are at war with them: it is taken back in battle or not at all";
        var head = Economy.HeadOf(w, theirs); if (head != null && (w.GetRelationship(head.Id, me.Id)?.Disposition ?? 0) < RansomWillNot) return $"{head.Name} will not hear of it";
        return me.Purse >= RansomPrice(item) ? null : $"your purse does not hold the {RansomPrice(item)} credits they would ask";
    }
    public static void Ransom(World w, Character me, Heirloom item)
    {
        var holder = w.Char(item.HolderId)!; var theirs = w.House(holder.HouseId)!; int price = RansomPrice(item); me.Purse -= price; theirs.Treasury.Credits += price;
        Regard(w, Economy.HeadOf(w, theirs), me, RansomRegard); ReturnStandard(w, item, holder, me, price);
    }

    /// <summary>What was answered to a house that asked for its standard: given back, or sold back at the asking.</summary>
    private static void AnswerStandards(World w)
    {
        bool given = w.WorldFlags.Remove(GivenBackFlag), sold = w.WorldFlags.Remove(RansomAgreedFlag); string? mine = w.Protagonist?.HouseId; if (!given && !sold || mine == null) return;
        var item = w.Heirlooms.Where(h => h.Kind == Standard && w.Counter(AskedKey(h.Id)) > 0 && w.Char(h.HolderId)?.HouseId == mine && w.House(h.FromHouseId) != null)
            .OrderByDescending(h => w.Counter(AskedKey(h.Id))).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
        var theirs = item != null ? w.House(item.FromHouseId) : null; var head = theirs != null ? Economy.HeadOf(w, theirs) : null; if (item == null || head?.Alive != true) return;
        var holder = w.Char(item.HolderId)!; int price = sold ? Math.Min(RansomPrice(item), Math.Max(0, theirs!.Treasury.Credits)) : 0;
        if (sold) { theirs!.Treasury.Credits -= price; holder.Purse += price; }
        ReturnStandard(w, item, holder, head, sold ? Math.Max(1, price) : 0);
    }

    /// <summary>The houses whose standards are flown elsewhere: one asks the player for its own back, now and then, when the
    /// two are at peace; among themselves the others sometimes buy theirs home.</summary>
    private static void StandardsAsked(World w)
    {
        string? mine = w.Protagonist?.HouseId;
        foreach (var item in w.Heirlooms.Where(h => h.Kind == Standard && h.FromHouseId.Length > 0).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            var holder = w.Char(item.HolderId); var from = w.House(item.FromHouseId); var head = from != null ? Economy.HeadOf(w, from) : null;
            if (holder?.Alive != true || head?.Alive != true || holder.HouseId == from!.Id || w.AtWar(holder.HouseId, from.Id) || from.Id == mine) continue;
            if (holder.HouseId == mine)
            {
                int asked = w.Counter(AskedKey(item.Id)); if (asked > 0 && w.Turn - asked < AskEvery || w.HasFlag(StandardAskedFlag)) continue;
                if (Hash($"{item.Id}:{w.Turn}:ask") / 7 % AskOneIn != 0) continue;
                w.WorldCounters[AskedKey(item.Id)] = w.Turn; Mark(w, StandardAskedFlag); head.Flags.Add(AsksChar);
                w.Log($"{head.Name} of {from.Name} asks for {item.Name} back.");
                continue;
            }
            var keeper = w.House(holder.HouseId); var keeperHead = keeper != null ? Economy.HeadOf(w, keeper) : null; int price = RansomPrice(item);
            if (keeper == null || from.Treasury.Credits < price + 40 || keeperHead != null && (w.GetRelationship(keeperHead.Id, head.Id)?.Disposition ?? 0) < RansomWillNot) continue;
            if (Hash($"{item.Id}:{w.Turn}:ransom") / 7 % OthersRansomOneIn != 0) continue;
            from.Treasury.Credits -= price; keeper.Treasury.Credits += price; ReturnStandard(w, item, holder, head, price);
        }
    }

    /// <summary>One of the house with enough put by has a thing made in their own name, some years: a library if they study,
    /// a shrine otherwise. Returns what they had made, if anything.</summary>
    private static Heirloom? KinCommission(World w, Character c, int h)
    {
        if (h % KinMakesOneIn != 0) return null;
        string kind = c.Skill("law") + c.Skill("economics") + c.Skill("administration") >= 6 ? Library : Shrine;
        if (c.Purse < CommissionCost(kind) + KinKeepsBack || CommissionRefusal(w, c, kind) != null) { kind = Shrine; if (c.Purse < ShrineCost + KinKeepsBack || CommissionRefusal(w, c, kind) != null) return null; }
        var item = Commission(w, c, kind); Count(w, KinMadeKey); c.Flags.Add(KinMadeChar); Mark(w, KinMadeFlag);
        c.LastSpend = $"Had {item.Name} made, with their own money."; return item;
    }

    /// <summary>A head who has come to think ill of another, short of hatred, and would rather not, sometimes sends a gift.
    /// It is the receiver's own. Sent to the player, it asks for an answer.</summary>
    private static void GiftsToMend(World w)
    {
        string? mine = w.Protagonist?.HouseId;
        if (w.WorldFlags.Remove(SendBackFlag) && mine != null && w.Heirlooms.Where(h => h.Id.StartsWith("gift_", StringComparison.Ordinal) && w.Char(h.HolderId)?.HouseId == mine).OrderByDescending(h => h.Id.Length).ThenByDescending(h => h.Id, StringComparer.Ordinal).FirstOrDefault(h => w.House(h.FromHouseId) != null) is { } back)
        { w.Heirlooms.Remove(back); Count(w, SentBackKey); }
        foreach (var a in w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId && h.Id != mine && h.Treasury.Credits >= MendAt).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            var head = Economy.HeadOf(w, a); if (head?.Alive != true || Hash($"{a.Id}:{w.Turn}:mend") / 7 % MendOneIn != 0) continue;
            // whoever they think worst of, of those they could still make it up with
            var to = w.Houses.Values.Where(b => b.Id != a.Id && b.Id != PirateSystem.HouseId && !w.AtWar(a.Id, b.Id)).Select(b => Economy.HeadOf(w, b)).Where(c => c is { Alive: true }).Select(c => (c: c!, r: w.GetRelationship(head.Id, c!.Id)?.Disposition ?? 0))
                .Where(x => x.r >= MendFrom && x.r <= MendTo).OrderBy(x => x.r).ThenBy(x => x.c.Id, StringComparer.Ordinal).Select(x => x.c).FirstOrDefault();
            if (to == null || w.Heirlooms.Any(h => h.Kind == Plate && h.FromHouseId == a.Id && h.OfHouseId == to.HouseId)) continue;
            a.Treasury.Credits -= PlateValue;
            w.Heirlooms.Add(new Heirloom { Id = $"gift_{a.Id}_{to.HouseId}_{w.Turn:D4}", Name = $"the {Bare(a)} gift", Kind = Plate, HolderId = to.Id, OfHouseId = to.HouseId, FromHouseId = a.Id, Value = PlateValue });
            Regard(w, to, head, MendRegard); Regard(w, head, to, MendRegard); Count(w, MendKey);
            if (to.HouseId == mine) { Mark(w, MendFlag); head.Flags.Add(MendChar); w.Log($"{head.Name} of {a.Name} sends you a gift, unasked. There has been ill feeling, and this is meant to end it."); }
        }
    }
}
