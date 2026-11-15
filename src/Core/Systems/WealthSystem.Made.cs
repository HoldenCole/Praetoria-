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
        else if (item.Kind == Standard) { if (FlagshipOf(w, holder.Id) is { } f && f.Loyalty < StandardCeiling) f.Loyalty += 1; }
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
}
