using Praetoria.Core.Data;
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
        return who.Purse >= CommissionCost(kind) ? null : $"your purse holds {Money.Show(who.Purse)} and it costs {Money.Show(CommissionCost(kind))}: move coin in House, under Purses and the will";
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
        foreach (var h in w.Houses.Values.Where(h => !World.Outsider(h.Id) && h.Id != mine && h.Treasury.Credits >= OthersCommissionAt).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            var head = Economy.HeadOf(w, h); if (head?.Alive != true) continue;
            uint x = Hash($"{h.Id}:{w.Turn}:commission") / 7; if (x % OthersCommissionOneIn != 0) continue;   // the low bits of the hash follow the id too closely to be used bare
            string kind = Commissions[(int)(x / 17 % (uint)Commissions.Length)];
            if (w.Heirlooms.Any(i => i.Kind == kind && i.OfHouseId == h.Id)) continue;
            h.Treasury.Credits -= CommissionCost(kind); Make(w, h, head, kind, CommissionCost(kind) * MadeWorthPct / 100);
        }
    }

    /// <summary>Who keeps a standard won: the first commander, unless that one commanded by service, when the spoils stay with the house that owns
    /// the fleets (docs/MILITARY.md, "A command by service"): a commander of its own, else its head.</summary>
    private static Character Keeper(World w, House house, IReadOnlyList<Character> admirals) =>
        ServiceSystem.Held(w, admirals[0]) is null ? admirals[0] : admirals.FirstOrDefault(a => a.HouseId == house.Id) ?? (Economy.HeadOf(w, house) is { Alive: true } head ? head : admirals[0]);

    /// <summary>A battle is won, and sometimes whoever commanded brings the other side's standard home. It is theirs: they
    /// took it. A house keeps no more than <see cref="StandardsAHouse"/>; raiders fly nothing worth taking.</summary>
    public static Heirloom? StandardWon(World w, string winnerId, string loserId, string systemId, IReadOnlyList<Character> admirals)
    {
        var house = w.House(winnerId); var beaten = w.House(loserId);
        if (house == null || beaten == null || World.Outsider(winnerId) || World.Outsider(loserId) || admirals.Count == 0) return null;
        string where = w.Galaxy?.System(systemId)?.Name ?? systemId;
        // a standard of the winner's that the beaten house flies is what the day was for: it comes home
        var lost = w.Heirlooms.Where(h => IsSpoil(h) && h.FromHouseId == winnerId && w.Char(h.HolderId) is { } hc && hc.HouseId == loserId).OrderBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
        if (lost != null)
        {
            // a standard is whoever retook it's; anything else that was the house's goes back to its head
            // the spoils are the house's that owns the fleets: a standard goes to a commander of its own, or its head when one of another house led by service
            var keeper = Keeper(w, house, admirals);
            var home = lost.Kind == Standard ? keeper : Economy.HeadOf(w, house) is { Alive: true } hd ? hd : admirals[0];
            lost.HolderId = home.Id; lost.OfHouseId = winnerId; lost.FromHouseId = ""; lost.Entailed = false; if (lost.Kind == Standard) lost.Name = $"the {Bare(house)} standard, lost and retaken at {where}"; Count(w, RetakenKey);
            LifeSystem.Note(w, admirals[0], "victory", lost.Kind == Standard ? $"Retook the house's standard at {where}." : $"Retook {lost.Name} at {where}.");
            if (winnerId == w.Protagonist?.HouseId) w.Log($"{admirals[0].Name} retakes {lost.Name}.");
            return lost;
        }
        if (Hash($"{systemId}:{w.Turn}:{winnerId}:{loserId}:standard") / 7 % StandardOneIn != 0) return null;
        if (w.Heirlooms.Count(h => h.Kind == Standard && h.OfHouseId == winnerId) >= StandardsAHouse) return null;
        var who = Keeper(w, house, admirals); string id = $"standard_{winnerId}_{w.Turn}_{systemId}"; if (w.Heirlooms.Any(h => h.Id == id)) return null;
        string place = w.Galaxy?.System(systemId)?.Name ?? systemId;
        var item = new Heirloom { Id = id, Name = $"the {Bare(beaten)} standard taken at {place}", Kind = Standard, HolderId = who.Id, OfHouseId = winnerId, FromHouseId = loserId, Value = StandardValue };
        w.Heirlooms.Add(item); Count(w, StandardsKey);
        LifeSystem.Note(w, admirals[0], "victory", $"Took the {Bare(beaten)} standard at {place}.");   // the deed is the commander's, whoever keeps the standard
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
        if (fromHouse == null || toHouse == null || World.Outsider(fromHouse.Id) || World.Outsider(toHouse.Id) || fromHouse.Id == w.Protagonist?.HouseId) return null;
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
        else if (item.Kind == Orrery) { if (holder.Study.Length > 0) EducationSystem.Gain(w, holder, holder.Study, 1); }
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
        if (item.Kind == Standard) item.Name = $"the {(home != null ? Bare(home) : "old")} standard, come home"; if (from?.Will != null) from.Will.Items.Remove(item.Id);
        Regard(w, to, from, paid > 0 ? RansomRegard : ReturnRegard); Count(w, paid > 0 ? RansomedKey : ReturnedKey); w.WorldCounters.Remove(AskedKey(item.Id));
        if (from?.HouseId == w.Protagonist?.HouseId || to.HouseId == w.Protagonist?.HouseId)
            w.Log(paid > 0 ? $"{item.Name}: {to.Name} pays {Money.Credits(paid)} and has it back." : $"{from?.Name} sends {to.Name} {(item.Kind == Standard ? "their standard" : item.Name)} back, and asks nothing for it.");
    }

    /// <summary>Your house's standard is flown by another. Its holder's house will sell it home if its head can bear you; at war
    /// there is only one way to have it back.</summary>
    public static string? RansomRefusal(World w, Character? me, Heirloom? item)
    {
        if (me?.Alive != true || item == null || !IsSpoil(item) || item.FromHouseId != me.HouseId) return "not a thing taken from your house";
        var holder = w.Char(item.HolderId); var theirs = holder != null ? w.House(holder.HouseId) : null; if (holder?.Alive != true || theirs == null || theirs.Id == me.HouseId) return "nobody else holds it";
        if (w.AtWar(me.HouseId, theirs.Id)) return "you are at war with them: it is taken back in battle or not at all";
        var head = Economy.HeadOf(w, theirs); if (head != null && (w.GetRelationship(head.Id, me.Id)?.Disposition ?? 0) < RansomWillNot) return $"{head.Name} will not hear of it";
        return me.Purse >= RansomPrice(item) ? null : $"your purse holds {Money.Show(me.Purse)} and they would ask {Money.Show(RansomPrice(item))}: move coin in House, under Purses and the will";
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
        var item = w.Heirlooms.Where(h => IsSpoil(h) && w.Counter(AskedKey(h.Id)) > 0 && w.Char(h.HolderId)?.HouseId == mine && w.House(h.FromHouseId) != null)
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
        foreach (var item in w.Heirlooms.Where(IsSpoil).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
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
        Answered(w);
        foreach (var a in w.Houses.Values.Where(h => !World.Outsider(h.Id) && h.Id != mine && h.Treasury.Credits >= MendAt).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            var head = Economy.HeadOf(w, a); if (head?.Alive != true || Hash($"{a.Id}:{w.Turn}:mend") / 7 % MendOneIn != 0) continue;
            // whoever they think worst of, of those they could still make it up with
            var to = w.Houses.Values.Where(b => b.Id != a.Id && !World.Outsider(b.Id) && !w.AtWar(a.Id, b.Id)).Select(b => Economy.HeadOf(w, b)).Where(c => c is { Alive: true }).Select(c => (c: c!, r: w.GetRelationship(head.Id, c!.Id)?.Disposition ?? 0))
                .Where(x => x.r >= MendFrom && x.r <= MendTo).OrderBy(x => x.r).ThenBy(x => x.c.Id, StringComparer.Ordinal).Select(x => x.c).FirstOrDefault();
            if (to == null || w.Heirlooms.Any(h => h.Kind == Plate && h.FromHouseId == a.Id && h.OfHouseId == to.HouseId)) continue;
            if (Refuses(w, to, head, RefuseMendAt, mine)) continue;   // sent back unopened: the quarrel is worse for the asking
            a.Treasury.Credits -= PlateValue;
            w.Heirlooms.Add(new Heirloom { Id = $"gift_{a.Id}_{to.HouseId}_{w.Turn:D4}", Name = $"the {Bare(a)} gift", Kind = Plate, HolderId = to.Id, OfHouseId = to.HouseId, FromHouseId = a.Id, Value = PlateValue });
            Regard(w, to, head, MendRegard); Regard(w, head, to, MendRegard); Count(w, MendKey);
            if (to.HouseId == mine) { Mark(w, MendFlag); head.Flags.Add(MendChar); w.Log($"{head.Name} of {a.Name} sends you a gift, unasked. There has been ill feeling, and this is meant to end it."); }
        }
    }

    // ---------------- plunder that is not a standard, and a gift that asks a favour (docs/WEALTH.md, W11) ----------------

    public const string Orrery = "orrery";
    public static readonly string[] CarriedOff = { "signet", "sword", Library, Standard, Regalia };
    public const int PlunderOneIn = 2, PlunderLegitimacy = 1, FavourLeanBy = 2, FavourOneIn = 10, FavourFrom = -10, FavourTo = 19, FavourCost = 18, FavourRegard = 4, FavourKept = 5, FavourBroken = 15, OrreryValue = 18;
    public const string PlunderedKey = "heirlooms_plundered", SurrenderedKey = "signets_surrendered", FavourGiftsKey = "favour_gifts", FavoursOwedKey = "favours_owed", FavoursCalledKey = "favours_called", FavoursKeptKey = "favours_kept", FavoursBrokenKey = "favours_broken", FavoursPaidKey = "favours_paid_with_a_vote";
    public const string PlunderedFlag = "heirloom_plundered", AsksFavourFlag = "gift_that_asks", AsksFavourChar = "asks_a_favour", DeclineFlag = "favour_declined";
    public const string FavourPrefix = "favour:";

    /// <summary>A thing held by someone other than the house it was taken from in war. (Plate and an orrery carry the name of
    /// whoever gave them, which is another matter.)</summary>
    public static bool IsSpoil(Heirloom h) => h.FromHouseId.Length > 0 && h.Kind != Plate && h.Kind != Orrery;

    /// <summary>A world is stormed. Whoever governed it for the beaten house, and its head if that was its last world, may
    /// lose something that can be carried: a signet, a sword, a library, a standard. The commander has it. A thing that had
    /// been taken from the stormers' own house comes home instead, for certain.</summary>
    public static Heirloom? Plunder(World w, string loserId, string takerId, Holding world, IReadOnlyList<Character> admirals)
    {
        var loser = w.House(loserId); var takers = w.House(takerId); if (loser == null || takers == null || World.Outsider(loserId) || World.Outsider(takerId)) return null;
        var from = new List<string>(); if (w.Char(world.GovernorId) is { Alive: true } gov && gov.HouseId == loserId) from.Add(gov.Id);
        if (!w.HoldingsOf(loserId).Any(h => h.IsPlanet) && Economy.HeadOf(w, loser) is { Alive: true } lh && !from.Contains(lh.Id)) from.Add(lh.Id);
        var things = w.Heirlooms.Where(h => from.Contains(h.HolderId) && CarriedOff.Contains(h.Kind)).OrderByDescending(h => h.Value).ThenBy(h => h.Id, StringComparer.Ordinal).ToList(); if (things.Count == 0) return null;
        var commander = admirals.FirstOrDefault() ?? Economy.HeadOf(w, takers); if (commander?.Alive != true) return null; string? mine = w.Protagonist?.HouseId;
        if (things.FirstOrDefault(h => h.FromHouseId == takerId) is { } ours)
        {
            var was = w.Char(ours.HolderId); var home = ours.Kind == Standard ? commander : Economy.HeadOf(w, takers) is { Alive: true } th ? th : commander;
            ours.HolderId = home.Id; ours.OfHouseId = takerId; ours.FromHouseId = ""; ours.Entailed = false; if (was?.Will != null) was.Will.Items.Remove(ours.Id); Count(w, RetakenKey);
            if (takerId == mine) w.Log($"{ours.Name} is found on {world.Name} and comes home."); return ours;
        }
        var item = things[0]; if (Hash($"{item.Id}:{w.Turn}:{takerId}:plunder") / 7 % PlunderOneIn != 0) return null;
        var holder = w.Char(item.HolderId); if (holder?.Will != null) holder.Will.Items.Remove(item.Id);
        item.HolderId = commander.Id; item.Entailed = false; if (item.FromHouseId.Length == 0) item.FromHouseId = loserId;   // one already taken from a third house is still that house's to want back
        if (item.Kind == Standard) item.OfHouseId = takerId;
        LegitimacyBook.Move(w, loser, -PlunderLegitimacy, LegitimacyBook.War, "A thing of the family carried off when a world fell"); Count(w, PlunderedKey); LifeSystem.Note(w, commander, "victory", $"Carried {item.Name} off from {world.Name}.");
        if (loserId == mine) { Mark(w, PlunderedFlag); w.Log($"{world.Name} falls, and {commander.Name} of {takers.Name} carries off {item.Name}."); }
        else if (takerId == mine) w.Log($"{commander.Name} carries off {item.Name} from {world.Name}. {loser.Name} will want it back.");
        return item;
    }

    public const string Regalia = "regalia", RegaliaTakenKey = "regalia_taken"; public const int RegaliaValue = 40, RegaliaMendsBelow = 85;
    /// <summary>The vaults of a fallen capital are opened (docs/BEYOND.md, the Hollow Crown): the thing in them goes to the head of
    /// the house that took it. On a head's hand it mends the house's name a point a year; it can be sold, given, willed, and carried
    /// off in its turn by whoever storms that house's last world.</summary>
    public static Heirloom? TakeRegalia(World w, House taker, string name, string nationId)
    {
        if (name.Length == 0 || Economy.HeadOf(w, taker) is not { Alive: true } head || w.Heirlooms.Any(h => h.Id == "regalia_" + nationId)) return null;
        var item = new Heirloom { Id = "regalia_" + nationId, Name = name, Kind = Regalia, HolderId = head.Id, OfHouseId = taker.Id, Value = RegaliaValue };
        w.Heirlooms.Add(item); Count(w, RegaliaTakenKey); LifeSystem.Note(w, head, "victory", $"Took {name} out of the vaults of a fallen capital.");
        return item;
    }

    /// <summary>A house is broken and folded into another: its signet goes to the head of the house that has it now.</summary>
    public static void Broken(World w, House house, House into)
    {
        var head = Economy.HeadOf(w, into); if (head?.Alive != true) return;
        foreach (var signet in w.Heirlooms.Where(h => h.Kind == "signet" && h.OfHouseId == house.Id).OrderBy(h => h.Id, StringComparer.Ordinal))
        { signet.HolderId = head.Id; signet.Entailed = false; signet.FromHouseId = ""; Count(w, SurrenderedKey); }
    }

    public static string FavourKey(string owerHouseId, string toHouseId) => $"{FavourPrefix}{owerHouseId}>{toHouseId}";
    public static bool FavourOwed(World w, string owerHouseId, string toHouseId) => w.Counter(FavourKey(owerHouseId, toHouseId)) > 0;
    /// <summary>A house that owes the proposer a favour leans its way: the favour is what the gift was for. Between two other
    /// houses only: a house that owes the player's house a favour does not lean, it votes (<see cref="VotesFor"/>), and the
    /// player's house votes as the player pleases.</summary>
    public static int FavourLean(World w, string houseId, string proposerHouseId) => houseId != proposerHouseId && houseId != w.Protagonist?.HouseId && proposerHouseId != w.Protagonist?.HouseId && FavourOwed(w, houseId, proposerHouseId) && !Apart(w, houseId, proposerHouseId) ? FavourLeanBy : 0;

    /// <summary>Two houses with a war or blood between them (FeudSystem.Between): no favour between them is a vote, a lean or a call, and
    /// it lapses at the year's end (<see cref="LapseFavours"/>). A house with blood against yours "owes you nothing in the Senate"
    /// (content/text/feud.json, blood_on_our_hands), as an enemy at war does.</summary>
    public static bool Apart(World w, string a, string b) => w.AtWar(a, b) || FeudSystem.Between(w, a, b);

    /// <summary>A motion that strikes at a house: a censure, an embargo, a break-up, a recall or a reconquest of it. No favour is
    /// called in on one, either way: a house is not asked to vote against itself for what it owes (docs/WEALTH.md, "A favour owed is a vote").</summary>
    public static bool StrikesAt(Motion m, string houseId, MotionDef? def = null) =>
        houseId.Length > 0 && m.TargetId == houseId && (def == null || def.Target == "house") && (def?.Interest ?? m.Kind) is "censure" or "embargo" or "breakup" or "recall" or "reconquest";

    /// <summary>A favour owed to the player's house is a vote (docs/WEALTH.md, "A favour owed is a vote"): a house that owes it one votes
    /// for every motion the player's house moves while it owes it, whatever it leans, and the favour is spent at the count of the first
    /// one it would not have voted for of its own accord. Not on a motion that strikes at that house (it waits for the next), nor while
    /// the two are at war (it lapses at the year's end), nor from a house that has left the Reach and its Senate (it waits).</summary>
    public static bool VotesFor(World w, Motion m, string houseId, MotionDef? def = null)
    {
        string? mine = w.Protagonist?.HouseId;
        return mine != null && m.ProposerHouseId == mine && houseId != mine && FavourOwed(w, houseId, mine) && !World.Outsider(houseId)
            && w.House(houseId) != null && PolitySystem.InReach(w, houseId) && !Apart(w, houseId, mine) && !StrikesAt(m, houseId, def);
    }

    /// <summary>The motion now before the Senate at whose count a favour this house owes that one would be called in, or null: that
    /// house's own, not one it moved at the asking of the house that owes (<see cref="Motion.MovedFor"/>), not one that strikes at it.</summary>
    public static Motion? CalledOn(World w, string owerHouseId, string toHouseId) => Apart(w, owerHouseId, toHouseId) ? null :
        w.Motions.Where(m => m.Open && m.ProposerHouseId == toHouseId && m.MovedFor != owerHouseId && !StrikesAt(m, owerHouseId)).OrderBy(m => m.ProposedTurn).ThenBy(m => m.Id, StringComparer.Ordinal).FirstOrDefault();

    private static void Owe(World w, string owerHouseId, string toHouseId)
    {
        if (FavourOwed(w, owerHouseId, toHouseId)) return;
        w.WorldCounters[FavourKey(owerHouseId, toHouseId)] = Math.Max(1, w.Turn); Count(w, FavoursOwedKey);
        // the player comes to owe a house whose motion is already before the Senate: SenateSystem.Add told them nothing, so they are told now
        if (owerHouseId == w.Protagonist?.HouseId && w.House(toHouseId) is { } to && CalledOn(w, owerHouseId, toHouseId) != null)
            w.Tell("warn", $"You owe {to.Name} a favour, and its motion is before the Senate: the favour is called in at that count. Vote for it and the account is even, and its head thinks {FavourKept:+0;-0} of you; vote otherwise and its head thinks {-FavourBroken:+0;-0} of you.", "senate:chamber", to.Id);
    }
    /// <summary>One house comes to owe another a favour for something done for it outside the giving of gifts (a pledge of votes kept).</summary>
    public static void OweFor(World w, string owerHouseId, string toHouseId) => Owe(w, owerHouseId, toHouseId);
    public static List<House> OweMe(World w, string houseId) => w.Houses.Values.Where(h => FavourOwed(w, h.Id, houseId)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
    public static List<House> IOwe(World w, string houseId) => w.Houses.Values.Where(h => FavourOwed(w, houseId, h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();

    /// <summary>A motion is voted. Every favour owed to its proposer is spent on it, but one owed by a house the motion strikes at, or by
    /// the house that asked for it to be moved, which waits, and one between houses at war, which lapses at the year's end. The player,
    /// who votes as they please, is judged by how they voted: for it, the favour is kept; anything else, and it is remembered.
    /// On a motion of the player's own house, a favour owed to it is a vote (<see cref="VotesFor"/>): <paramref name="votedFor"/> holds
    /// each house that voted for it because it owed one, and whether it would have of its own accord. Only one that would not is spent.</summary>
    public static void FavoursCalled(World w, Motion m, MotionDef? def = null, IReadOnlyDictionary<string, bool>? votedFor = null)
    {
        string? mine = w.Protagonist?.HouseId; var proposer = w.House(m.ProposerHouseId); if (proposer == null) return;
        foreach (var key in w.WorldCounters.Keys.Where(k => k.StartsWith(FavourPrefix, StringComparison.Ordinal) && k.EndsWith(">" + proposer.Id, StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal).ToList())
        {
            string ower = key[FavourPrefix.Length..^(proposer.Id.Length + 1)];
            if (proposer.Id == mine) { VotePaid(w, m, def, key, ower, proposer, votedFor); continue; }
            if (m.MovedFor == ower || StrikesAt(m, ower, def) || Apart(w, ower, proposer.Id)) continue;   // it waits for the proposer's own next motion, or goes with the war or the blood
            w.WorldCounters.Remove(key); Count(w, FavoursCalledKey);
            if (ower != mine) continue;
            var theirs = Economy.HeadOf(w, proposer); var me = w.Protagonist; bool kept = m.Votes.TryGetValue(mine!, out int cast) && cast > 0;
            Regard(w, theirs, me, kept ? FavourKept : -FavourBroken); Count(w, kept ? FavoursKeptKey : FavoursBrokenKey); w.WorldCounters.Remove(DealingsSystem.CalledKey(mine!, proposer.Id));
            DealingsSystem.Remember(w, kept ? DealingsSystem.FavourKeptKind : DealingsSystem.FavourBrokenKind, mine!, proposer.Id);
            w.Tell(kept ? "good" : "warn", kept ? $"{proposer.Name} called in the favour you owed it on its motion, and you voted for it. The account is even, and its head thinks {FavourKept:+0;-0} of you."
                : $"{proposer.Name} called in the favour you owed it on its motion, and you did not vote for it. Its head thinks {-FavourBroken:+0;-0} of you.", "houses", proposer.Id);
            w.Log(kept ? $"{proposer.Name} called in the favour your house owed it, and you voted for its motion. The account is even." : $"{proposer.Name} called in the favour your house owed it, and you did not vote for its motion. {theirs?.Name ?? "Its head"} will remember it.");
        }
    }

    /// <summary>A favour owed to the player's house, at the count of its motion: paid with a vote and spent, or kept because the house
    /// was for the motion anyway, or waiting because the motion strikes at that house. The player is told which.</summary>
    private static void VotePaid(World w, Motion m, MotionDef? def, string key, string ower, House mine, IReadOnlyDictionary<string, bool>? votedFor)
    {
        var owes = w.House(ower); if (owes == null) return;   // a house that is no more: the favour lapses at the year's end, and the player is told then
        if (votedFor != null && votedFor.TryGetValue(ower, out bool ofItself))
        {
            if (ofItself) { w.Tell("info", $"{owes.Name} voted for your motion of its own accord, so the vote it owes your house is still owed, for a motion it would not vote for.", "houses", owes.Id); return; }
            w.WorldCounters.Remove(key); Count(w, FavoursCalledKey); Count(w, FavoursPaidKey); DealingsSystem.Remember(w, DealingsSystem.FavourKeptKind, owes.Id, mine.Id);
            w.Tell("good", $"{owes.Name} kept its word: it owed your house a vote, and voted for your motion, though it would not have of its own accord. The favour is spent.", "houses", owes.Id);
            w.Log($"{owes.Name} votes for the motion of {mine.Name}, as it owed {mine.Name} a vote.");
        }
        else if (StrikesAt(m, ower, def))
            w.Tell("info", $"Your motion strikes at {owes.Name}, so the vote it owes your house is not asked of it here: it waits for your next motion.", "houses", owes.Id);
    }

    /// <summary>A head with a motion before the Senate sometimes sends an orrery to the head of a weighty house that is not
    /// yet a friend. Kept, it is a favour owed: that house leans for the motion. Sent to the player, it asks for an answer.</summary>
    /// <summary>The answers to a gift that the reports give with a flag (a_gift_that_asks_a_favour, and the plate sent to mend a quarrel):
    /// said it buys nothing, the favour it asked is gone; sent back, the thing and the favour both. Read when the Senate sits, before its
    /// count (SenateSystem.Tick), so that a vote the player said was not for sale is not judged at the count of the giver's motion; and at
    /// the year's gifts, as before.
    /// <para>Each answer marks the giver the report named (<see cref="OrreryBackChar"/>, <see cref="PlateBackChar"/>, <see cref="DeclinedChar"/>), and only
    /// that giver's house is answered: its own gift of the kind the report was about, the newest, goes back, and only the favour its orrery
    /// asked is gone (a plate asks none). A save from before the marks, with the world flag alone, is answered as it was then.</para></summary>
    public static void Answered(World w)
    {
        string? mine = w.Protagonist?.HouseId;
        var declined = w.Characters.Values.Where(c => c.Flags.Remove(DeclinedChar)).Select(c => c.HouseId).ToHashSet();
        if (w.WorldFlags.Remove(DeclineFlag) && mine != null)
        {
            if (declined.Count == 0) declined = IOwe(w, mine).Where(h => Economy.HeadOf(w, h) is { } hd && hd.Flags.Contains(AsksFavourChar)).Select(h => h.Id).ToHashSet();
            foreach (var id in declined.OrderBy(x => x, StringComparer.Ordinal)) w.WorldCounters.Remove(FavourKey(mine, id));
        }
        var givers = w.Characters.Values.Where(c => c.Flags.Contains(OrreryBackChar) || c.Flags.Contains(PlateBackChar)).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (c.HouseId, orrery: c.Flags.Remove(OrreryBackChar), plate: c.Flags.Remove(PlateBackChar))).ToList();
        if (!w.WorldFlags.Remove(SendBackFlag) || mine == null) return;
        bool Mine(Heirloom h) => h.Id.StartsWith("gift_", StringComparison.Ordinal) && w.Char(h.HolderId)?.HouseId == mine;
        if (givers.Count == 0)
        {
            if (w.Heirlooms.Where(Mine).OrderByDescending(h => h.Id.Length).ThenByDescending(h => h.Id, StringComparer.Ordinal).FirstOrDefault(h => w.House(h.FromHouseId) != null) is { } old)
            { w.Heirlooms.Remove(old); Count(w, SentBackKey); if (old.Kind == Orrery) w.WorldCounters.Remove(FavourKey(mine, old.FromHouseId)); }
            return;
        }
        foreach (var (houseId, orrery, plate) in givers)
            foreach (var kind in new[] { orrery ? Orrery : "", plate ? Plate : "" }.Where(k => k.Length > 0))
            {
                var back = w.Heirlooms.Where(h => Mine(h) && h.FromHouseId == houseId && h.Kind == kind).OrderByDescending(h => h.Id, StringComparer.Ordinal).FirstOrDefault(); if (back == null) continue;
                w.Heirlooms.Remove(back); Count(w, SentBackKey);
                if (kind == Orrery) w.WorldCounters.Remove(FavourKey(mine, houseId));   // the favour the orrery asked goes back with it; a plate asked none
            }
    }
    /// <summary>On the giver the report named: their orrery sent back (a_gift_that_asks_a_favour/send_it_back), their plate sent back
    /// (a_gift_to_mend_a_quarrel/send_it_back), the favour their orrery asked declined (a_gift_that_asks_a_favour/keep_it_and_promise_nothing).</summary>
    public const string OrreryBackChar = "orrery_sent_back", PlateBackChar = "plate_sent_back", DeclinedChar = "favour_it_asked_declined";

    private static void GiftsThatAsk(World w)
    {
        string? mine = w.Protagonist?.HouseId;
        Answered(w);
        foreach (var a in w.Houses.Values.Where(h => !World.Outsider(h.Id) && h.Id != mine && h.Treasury.Credits >= MendAt).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            var head = Economy.HeadOf(w, a); if (head?.Alive != true || !w.Motions.Any(m => m.Open && m.ProposerHouseId == a.Id)) continue;
            if (Hash($"{a.Id}:{w.Turn}:favour") / 7 % FavourOneIn != 0) continue;
            // not to a house its motion strikes at: nobody is asked to vote for their own censure
            // nor to the player's house when its vote on that motion is given already (a word, a sale, gold taken: VoteMarks.Given): the orrery would ask what is not the player's to give
            var to = w.Houses.Values.Where(b => b.Id != a.Id && !World.Outsider(b.Id) && !w.AtWar(a.Id, b.Id) && !FavourOwed(w, b.Id, a.Id) && CalledOn(w, b.Id, a.Id) is { } asked && !(b.Id == mine && VoteMarks.Given(w, mine, asked) != null)).Select(b => (b, c: Economy.HeadOf(w, b))).Where(x => x.c is { Alive: true })
                .Where(x => (w.GetRelationship(head.Id, x.c!.Id)?.Disposition ?? 0) is >= FavourFrom and <= FavourTo).OrderByDescending(x => x.b.Legitimacy).ThenBy(x => x.b.Id, StringComparer.Ordinal).Select(x => x.c!).FirstOrDefault();
            if (to == null || w.Heirlooms.Any(h => h.Kind == Orrery && h.FromHouseId == a.Id && h.OfHouseId == to.HouseId)) continue;
            if (Refuses(w, to, head, RefuseFavourAt, mine)) continue;   // they will not be put under an obligation to this house
            a.Treasury.Credits -= FavourCost;
            w.Heirlooms.Add(new Heirloom { Id = $"gift_{a.Id}_{to.HouseId}_{w.Turn:D4}o", Name = $"the {Bare(a)} orrery", Kind = Orrery, HolderId = to.Id, OfHouseId = to.HouseId, FromHouseId = a.Id, Value = OrreryValue });
            Regard(w, to, head, FavourRegard); Regard(w, head, to, FavourRegard); Owe(w, to.HouseId, a.Id); Count(w, FavourGiftsKey);
            if (to.HouseId == mine) { Mark(w, AsksFavourFlag); head.Flags.Add(AsksFavourChar); w.Log($"{head.Name} of {a.Name} sends you an orrery. {a.Name} has a motion before the Senate."); }
        }
    }

    // ---------------- a favour called in, a favour that lapses, a gift refused (docs/WEALTH.md, W13) ----------------

    public const int FavourYears = 8, FavourConsent = 20, FavourCredits = 15, RefuseMendAt = -35, RefuseFavourAt = -15, RefuseGiftAt = -40, RefusedRegard = 6;
    public const string ForMoney = "money", ForMatch = "match";
    public const string FavoursLapsedKey = "favours_lapsed", FavoursForMoneyKey = "favours_for_money", FavoursForMatchKey = "favours_for_a_match", GiftsRefusedKey = "gifts_refused";

    /// <summary>A head who thinks ill enough of whoever sent it will not take the gift. Nothing changes hands, and the sender
    /// thinks the worse of them for it. The player is asked, never answered for.</summary>
    private static bool Refuses(World w, Character to, Character giver, int at, string? mine)
    {
        if (to.HouseId == mine || (w.GetRelationship(to.Id, giver.Id)?.Disposition ?? 0) > at) return false;
        Regard(w, giver, to, -RefusedRegard); Count(w, GiftsRefusedKey); return true;
    }
    /// <summary>Will the head of another house take a thing from this giver? Not one they think very ill of, nor an enemy at war.
    /// A thing that was theirs to begin with is always taken back.</summary>
    public static string? TakesRefusal(World w, Character giver, Heirloom item, Character to)
    {
        if (to.HouseId == giver.HouseId || IsSpoil(item) && item.FromHouseId == to.HouseId) return null;
        return WontTake(w, giver, to);
    }
    /// <summary>The same rule for anything else one house would press on another (a sum, a loan, a pledge): not from an enemy
    /// at war, nor from one their head thinks very ill of.</summary>
    public static string? WontTake(World w, Character giver, Character to)
    {
        if (w.AtWar(giver.HouseId, to.HouseId)) return $"{to.Name} is at war with you, and will take nothing";
        return (w.GetRelationship(to.Id, giver.Id)?.Disposition ?? 0) <= RefuseGiftAt ? $"{to.Name} will take nothing from you" : null;
    }

    /// <summary>The suit between the two houses that a favour could move: one of the caller's people courting one of theirs.</summary>
    public static Courtship? SuitBetween(World w, string myHouseId, string theirHouseId) =>
        w.Courtships.Where(c => w.Char(c.SuitorId) is { Alive: true } s && w.Char(c.TargetId) is { Alive: true } t && (s.HouseId == myHouseId && t.HouseId == theirHouseId || s.HouseId == theirHouseId && t.HouseId == myHouseId))
            .OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();

    /// <summary>A favour owed is spent on the next vote if nothing else is asked. It can be asked for something else instead:
    /// money, or the family's consent to a match between the two houses.</summary>
    public static string? CallRefusal(World w, Character? me, House? theirs, string what)
    {
        var mine = me != null ? w.House(me.HouseId) : null; if (me?.Alive != true || mine == null || Economy.HeadOf(w, mine)?.Id != me.Id) return "only the head of a house calls in what it is owed";
        if (theirs == null || !FavourOwed(w, theirs.Id, mine.Id)) return "they owe you nothing";
        if (DealingsSystem.BargainPending(w, mine.Id, theirs.Id) != null) return DealingsSystem.BargainPendingSays(theirs);   // a vote for a vote: your side is not yet counted
        if (what == ForMoney) return theirs.Treasury.Credits >= FavourCredits ? null : "they have not the money to give";
        if (what == ForMatch) return SuitBetween(w, mine.Id, theirs.Id) != null ? null : "there is no suit between your house and theirs";
        return "no such thing to ask";
    }
    public static void Call(World w, Character me, House theirs, string what)
    {
        var mine = w.House(me.HouseId)!; w.WorldCounters.Remove(FavourKey(theirs.Id, mine.Id));
        if (what == ForMoney) { theirs.Treasury.Credits -= FavourCredits; mine.Treasury.Credits += FavourCredits; Count(w, FavoursForMoneyKey); w.Log($"{me.Name} calls in what {theirs.Name} owes: {Money.Credits(FavourCredits)}, paid without a word."); }
        else { var suit = SuitBetween(w, mine.Id, theirs.Id)!; suit.Consent = Math.Clamp(suit.Consent + FavourConsent, 0, 100); Count(w, FavoursForMatchKey); w.Log($"{me.Name} calls in what {theirs.Name} owes: the family looks more kindly on the match."); }
    }

    /// <summary>A favour nobody has called in is forgotten after <see cref="FavourYears"/> years; one between two houses at war, or with
    /// blood between them, is gone with the war or the blood; one owed by or to a house that is no more is gone with it. The player is
    /// told of their own, and why.</summary>
    private static void LapseFavours(World w)
    {
        string? mine = w.Protagonist?.HouseId;
        foreach (var key in w.WorldCounters.Keys.Where(k => k.StartsWith(FavourPrefix, StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal).ToList())
        {
            var parts = key[FavourPrefix.Length..].Split('>'); if (parts.Length != 2) continue;
            var ower = w.House(parts[0]); var owed = w.House(parts[1]);
            bool gone = ower == null || owed == null, war = !gone && w.AtWar(parts[0], parts[1]), blood = !gone && !war && FeudSystem.Between(w, parts[0], parts[1]), old = w.Turn - w.Counter(key) >= FavourYears;
            if (!gone && !war && !blood && !old) continue;
            w.WorldCounters.Remove(key); w.WorldCounters.Remove(DealingsSystem.CalledKey(parts[0], parts[1])); Count(w, FavoursLapsedKey);
            if (mine == null || (parts[0] != mine && parts[1] != mine)) continue;
            bool toMe = parts[1] == mine; var other = toMe ? ower : owed;
            // a vote for a vote: the vote it owed your house is gone with the war or the blood, and so is your word on its motion that bought it (DealingsSystem.MotionCounted voids it at the count too)
            string voided = "";
            if (toMe && (war || blood) && DealingsSystem.BargainPending(w, mine, other!.Id) is { } bargain) { w.VotePledges.Remove(bargain); voided = " Your word on its motion, given for that vote, is void with it: vote on it as you please."; }
            string line = gone ? (toMe ? "A house that owed yours a favour is no more, and the favour is gone with it." : "A house your house owed a favour is no more, and the favour is gone with it.")
                : war ? (toMe ? $"You are at war with {other!.Name}: the vote it owed your house will not be given, and the favour is gone." : $"You are at war with {other!.Name}: the favour your house owed it is gone.")
                : blood ? (toMe ? $"There is blood between your house and {other!.Name}: it owes you nothing in the Senate now, and the vote it owed your house is gone." : $"There is blood between your house and {other!.Name}: the favour your house owed it is gone.")
                : toMe ? $"What {other!.Name} owed your house has gone {Counted.Years(FavourYears)} unspent, and is forgotten." : $"What your house owed {other!.Name} has gone {Counted.Years(FavourYears)} uncalled, and is no longer spoken of.";
            w.Tell("info", line + voided, "houses", other?.Id ?? ""); w.Log(line);
        }
    }
}
