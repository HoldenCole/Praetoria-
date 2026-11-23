using Praetoria.Core.Progression;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Things worth a rich house's money (docs/WEALTH.md, the section of that name; docs/PLAYTHROUGH.md, "Hard": "endow a child, build a
/// seat, fund a relative's campaign, lend to houses. One treasury reached 303,400 at 20,000 a year with nothing to spend it on").
/// Three of the four are here and in <see cref="LadderSystem"/>'s campaign; lending between houses is built elsewhere.
/// <para><b>An estate settled</b> on one of the house, child or grown, bought from the treasury at about fifteen years' purchase. It
/// is land: it pays its holder a yearly income for life, it is theirs, and it goes as their own things go. It is the same kind of
/// thing as an estate handed down, so everything said of one (docs/WEALTH.md, "Heirlooms") is true of it.</para>
/// <para><b>The seat</b> of the house on a world of its own: a manor, a great house, a palace, each built once and dearer than the
/// last. It costs a keep and returns nothing in credits. <b>A town house at the capital</b> for one grown person of the house,
/// priced at five years of what that person takes in a year.</para>
/// Other houses do all three out of their treasuries when they are rich and idle. RNG-free.
/// </summary>
public static partial class WealthSystem
{
    public const string EstateKind = "estate", TownHouse = "town_house", SettledPrefix = "settled_estate_";
    public const string EstatesKey = "estates_settled", SeatsKey = "seats_built", TownHousesKey = "town_houses_bought";

    /// <summary>A price in the books' hundreds at this game's setting for how easy money is (docs/MONEY.md, "Settings"): what returns
    /// credits is scaled as everything that is built is.</summary>
    private static int AtSetting(World w, int n) => Math.Max(1, (int)Math.Ceiling(n * 100.0 / MoneySettings.ReturnPct(w.MoneyEase)));
    private static bool IsHead(World w, Character? c, out House house)
    {
        house = (c != null ? w.House(c.HouseId) : null)!; return c?.Alive == true && house != null && Economy.HeadOf(w, house)?.Id == c.Id;
    }
    private static string Holds(House house) => $"the treasury holds {Money.Held(house.Treasury)}";

    // ---------------- an estate settled on one of the house ----------------

    public const int EstatePays = 2, EstatePrice = 30, EstateDearer = 6, EstatesAPerson = 3, EstateRegard = 12, EstateSpouseRegard = 6, EstateMatch = 4, EstateMatchMost = 8;
    private static readonly string[] EstateLands = { "the home farm", "the river meadows", "the upland pastures", "the orchards", "the vineyards", "the salt marsh", "the old forest", "the quarry lands" };

    public static int EstatesHeld(World w, string characterId) => w.Heirlooms.Count(h => h.HolderId == characterId && h.Kind == EstateKind);
    /// <summary>What the next estate settled on this person costs: each is dearer than the last, since the best land goes first.</summary>
    public static int EstateCost(World w, Character to) => AtSetting(w, EstatePrice + EstateDearer * EstatesHeld(w, to.Id));
    /// <summary>What a son's or daughter's land adds to what another family thinks of a match with them (docs/MARRIAGE.md).</summary>
    public static int EstateMatchOf(World w, Character c) => Math.Min(EstateMatchMost, EstatesHeld(w, c.Id) * EstateMatch);
    /// <summary>The estates bought and settled (not those handed down from the start), held now by someone living of this house.</summary>
    public static List<Heirloom> Settled(World w, string houseId) =>
        w.Heirlooms.Where(h => h.Kind == EstateKind && h.Id.StartsWith(SettledPrefix, StringComparison.Ordinal) && w.Char(h.HolderId) is { Alive: true } c && c.HouseId == houseId).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();

    public static string? EndowRefusal(World w, Character? head, Character? to)
    {
        if (!IsHead(w, head, out var house)) return "only the head of the house settles its land";
        if (to?.Alive != true || to.HouseId != house.Id) return "not of your house";
        if (to.Id == head!.Id) return "an estate is settled on one of the house, not on its head";
        int held = EstatesHeld(w, to.Id); if (held >= EstatesAPerson) return $"they hold {Counted.Of(held, "estate")} already, and no more is settled on one person";
        int cost = EstateCost(w, to);
        return house.Treasury.Credits >= cost ? null : $"it costs {Money.Credits(cost)} and {Holds(house)}";
    }

    /// <summary>The head buys land out of the treasury and settles it on one of the house. It is theirs for life and theirs to
    /// leave; they and whoever they are married to think the better of the head for it.</summary>
    public static Heirloom Endow(World w, Character head, Character to)
    {
        var house = w.House(head.HouseId)!; int cost = EstateCost(w, to); house.Treasury.Credits -= cost; Count(w, EstatesKey); int n = w.Counter(EstatesKey);
        var worlds = w.HoldingsOf(house.Id).Where(h => h.IsPlanet).OrderBy(h => h.Id, StringComparer.Ordinal).ToList(); uint x = Hash($"{house.Id}:{to.Id}:{n}:land") / 7;
        string land = EstateLands[(int)(x % (uint)EstateLands.Length)], name = worlds.Count > 0 ? $"{land} on {worlds[(int)(x / 11 % (uint)worlds.Count)].Name}" : $"{land}, bought in year {w.Turn}";
        var item = new Heirloom { Id = SettledPrefix + n, Name = name, Kind = EstateKind, HolderId = to.Id, OfHouseId = house.Id, Value = cost * MadeWorthPct / 100 };
        w.Heirlooms.Add(item); Regard(w, to, head, EstateRegard);
        if (DynastySystem.Spouse(w, to) is { } spouse && spouse.Id != head.Id) Regard(w, spouse, head, EstateSpouseRegard);
        LifeSystem.Note(w, to, "deed", $"Was settled with {name} by {head.Name}.");
        if (house.Id == w.Protagonist?.HouseId)
            w.Log($"{head.Name} buys {name} for {Money.Credits(cost)} and settles it on {to.Name}: {Money.Show(EstatePays)} a year for life{(to.Age < Adult ? ", kept for them until they are of age" : "")}, and theirs to leave.");
        return item;
    }

    // ---------------- the seat of the house ----------------

    public const int SeatGrades = 3, SeatSway = 3, SeatRegard = 4, SeatMendsBelow = 50, PalaceMendsBelow = 60, GuestRegard = 3, GuestCeiling = 40, SeatLostLegitimacy = 2;
    private static readonly int[] SeatCosts = { 0, 60, 150, 400 }, SeatKeeps = { 0, 1, 3, 6 };
    public static string SeatShutKey(string houseId) => "seat_shut:" + houseId;

    public static int SeatCost(int grade) => grade is >= 1 and <= SeatGrades ? SeatCosts[grade] : 0;
    /// <summary>What a seat of this grade costs the treasury a year.</summary>
    public static int SeatKeep(int grade) => grade is >= 1 and <= SeatGrades ? SeatKeeps[grade] : 0;
    public static string SeatName(int grade) => grade switch { 1 => "manor", 2 => "great house", 3 => "palace", _ => "" };
    /// <summary>What a seat of this grade gives that the grade below does not, in a line.</summary>
    public static string SeatGives(int grade) => grade switch
    {
        1 => "Room for one more in the household who is not of the blood.",
        2 => $"Room for a second, and the house's standing mends a point a year while it is under {SeatMendsBelow}.",
        3 => $"Room for a third, the standing mends while it is under {PalaceMendsBelow}, and each year the head of the greatest house that is not yet a friend is received: each thinks {GuestRegard} the better of the other, up to {GuestCeiling}.",
        _ => ""
    };
    /// <summary>The world a house's seat stands on, while it has one.</summary>
    public static Holding? SeatWorld(World w, House house) => house.SeatGrade > 0 ? w.Holding(house.SeatHoldingId) : null;
    /// <summary>Where a house would build if it did not say: the most peopled world of its own.</summary>
    public static Holding? SeatSite(World w, House house) =>
        SeatWorld(w, house) ?? w.HoldingsOf(house.Id).Where(h => h.IsPlanet).OrderByDescending(h => h.Population).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
    /// <summary>Whether a house's seat was shut up for want of its keep at the last turn of the year.</summary>
    public static bool SeatShut(World w, House house) => house.SeatGrade > 0 && w.WorldCounters.ContainsKey(SeatShutKey(house.Id));

    /// <summary>Why the head may not build the next grade of the house's seat, or null. The first is built on a world of the
    /// house's own that the head names; the others where the first stands.</summary>
    public static string? SeatRefusal(World w, Character? head, string holdingId)
    {
        if (!IsHead(w, head, out var house)) return "only the head of the house builds its seat";
        if (house.SeatGrade >= SeatGrades) return "the house has its palace: there is nothing grander to build";
        var world = house.SeatGrade > 0 ? w.Holding(house.SeatHoldingId) : w.Holding(holdingId);
        if (world == null || !world.IsPlanet || world.OwnerId != house.Id)
            return house.SeatGrade > 0 ? "the world the seat stood on is lost" : w.HoldingsOf(house.Id).Any(h => h.IsPlanet) ? "the seat stands on a world of the house's own" : "the house holds no world to build a seat on";
        int next = house.SeatGrade + 1, cost = SeatCost(next);
        return house.Treasury.Credits >= cost ? null : $"a {SeatName(next)} costs {Money.Credits(cost)} and {Holds(house)}";
    }

    /// <summary>The next grade is built, out of the treasury. The old blood and the house's own people think the better of the
    /// house and its head for it.</summary>
    public static void BuildSeat(World w, Character head, string holdingId)
    {
        var house = w.House(head.HouseId)!; int grade = house.SeatGrade + 1; if (house.SeatGrade == 0) house.SeatHoldingId = holdingId;
        var world = w.Holding(house.SeatHoldingId)!; house.Treasury.Credits -= SeatCost(grade); house.SeatGrade = grade; house.SeatSince = w.Turn; w.WorldCounters.Remove(SeatShutKey(house.Id)); Count(w, SeatsKey);
        PolicySystem.Sway(w, house.Id, "old_blood", SeatSway);
        foreach (var k in w.LivingCharacters().Where(k => k.HouseId == house.Id && k.Id != head.Id && k.Age >= Adult).OrderBy(k => k.Id, StringComparer.Ordinal)) Regard(w, k, head, SeatRegard);
        LifeSystem.Note(w, head, "deed", grade == 1 ? $"Built the manor of {house.Name} on {world.Name}." : $"Made the seat of {house.Name} on {world.Name} a {SeatName(grade)}.");
        w.Log(grade == 1 ? $"{head.Name} builds a manor on {world.Name} for {Money.Credits(SeatCost(grade))}. {house.Name} has a seat." : $"{head.Name} makes the seat of {house.Name} on {world.Name} a {SeatName(grade)}, for {Money.Credits(SeatCost(grade))}.");   // any house's: a seat is seen
    }

    /// <summary>The year of a seat, for every house that has one: it is lost with the world it stands on; its keep is paid, or it is
    /// shut up for the year and gives nothing; a great house mends the house's standing; a palace receives a guest of rank.</summary>
    private static void Seats(World w)
    {
        string? mine = w.Protagonist?.HouseId;
        foreach (var h in w.Houses.Values.Where(h => h.SeatGrade > 0).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            var world = w.Holding(h.SeatHoldingId); string name = SeatName(h.SeatGrade);
            if (world == null || world.OwnerId != h.Id)
            {
                h.Legitimacy -= SeatLostLegitimacy * h.SeatGrade; h.SeatGrade = 0; h.SeatHoldingId = ""; w.WorldCounters.Remove(SeatShutKey(h.Id));
                if (h.Id == mine) { w.Tell("warn", $"The {name} of your house stood on {world?.Name ?? "a world that is gone"}, and is lost with it", "house:purse"); w.Log($"The {name} of {h.Name} stood on {world?.Name ?? "a world that is gone"}. It is lost with the world, and the house is thought the less of."); }
                continue;
            }
            int keep = SeatKeep(h.SeatGrade);
            if (h.Treasury.Credits < keep)
            {
                bool first = !w.WorldCounters.ContainsKey(SeatShutKey(h.Id)); w.WorldCounters[SeatShutKey(h.Id)] = w.Turn;
                if (h.Id == mine && first) w.Log($"{h.Name} cannot pay the {Money.Credits(keep)} its {name} costs a year. The house is shut up, and gives nothing until its keep is paid.");
                continue;
            }
            h.Treasury.Credits -= keep; w.WorldCounters.Remove(SeatShutKey(h.Id));
            if (h.SeatGrade >= 2 && h.Legitimacy < (h.SeatGrade >= 3 ? PalaceMendsBelow : SeatMendsBelow)) h.Legitimacy += 1;
            if (h.SeatGrade >= 3 && Economy.HeadOf(w, h) is { Alive: true } host && Guest(w, h, host) is { } guest)
            {
                Regard(w, guest, host, GuestRegard, ceiling: GuestCeiling); Regard(w, host, guest, GuestRegard, ceiling: GuestCeiling);
                if (h.Id == mine) w.Log($"{guest.Name} of {w.House(guest.HouseId)?.Name} is received at the palace on {world.Name}. Each thinks the better of the other for it.");
            }
        }
    }
    /// <summary>Who a palace receives this year: the head of the house of highest rank, of those at peace with it whose head does
    /// not yet think <see cref="GuestCeiling"/> of its own.</summary>
    public static Character? Guest(World w, House house, Character host) =>
        w.Houses.Values.Where(o => o.Id != house.Id && !World.Outsider(o.Id) && !w.AtWar(o.Id, house.Id))
            .OrderByDescending(o => Title.RankOf(o.Title)).ThenByDescending(o => o.Legitimacy).ThenBy(o => o.Id, StringComparer.Ordinal)
            .Select(o => Economy.HeadOf(w, o)).FirstOrDefault(c => c is { Alive: true } && (w.GetRelationship(c.Id, host.Id)?.Disposition ?? 0) < GuestCeiling);

    // ---------------- a town house at the capital ----------------

    public const int TownHouseYears = 5, TownHouseLeast = 10, TownHouseVotes = 3, TownHouseRegard = 10, TownHousePurseRegard = 14, KinHouseKeepsBack = 3;

    /// <summary>What a person of a house takes in a year of their own: the pay of their service, the allowance, what their land
    /// brings, and the stipend of a post of the council.</summary>
    public static int YearlyIncome(World w, Character c)
    {
        var house = w.House(c.HouseId); bool head = house != null && Economy.HeadOf(w, house)?.Id == c.Id;
        return Pay(c) + EstateIncome(w, c.Id) + (head || house == null ? 0 : AllowanceOf(house.Allowance)) + (house?.Duties.Any(d => d.HolderId == c.Id) == true ? HouseholdSystem.Stipend : 0);
    }
    /// <summary>A house at the capital costs five years of what the one who is to live in it takes in (the owner's yardstick: a home
    /// is four to six years of the income of whoever lives in it), and never less than the least such a house sells for.</summary>
    public static int TownHouseCost(World w, Character c) => Math.Max(TownHouseLeast, YearlyIncome(w, c) * TownHouseYears);
    public static Heirloom? TownHouseOf(World w, string characterId) => w.Heirlooms.FirstOrDefault(h => h.Kind == TownHouse && h.HolderId == characterId);
    /// <summary>One who holds a house at the capital is there for what the game has that happens there: their canvass for an office
    /// is worth more, and they hear how a count stands before it is held.</summary>
    public static bool AtTheCapital(World w, string characterId) => TownHouseOf(w, characterId) != null;
    private static string CapitalName(World w) => w.Galaxy?.System(w.Galaxy.Capital)?.Name ?? "the capital";

    public static string? TownHouseRefusal(World w, Character? head, Character? who, bool fromPurse)
    {
        if (!IsHead(w, head, out var house)) return "only the head of the house buys for its people";
        if (who?.Alive != true || who.HouseId != house.Id) return "not of your house";
        if (who.Age < Adult) return "they are a child: a house at the capital is for one who is grown";
        if (AtTheCapital(w, who.Id)) return who.Id == head!.Id ? "you have a house at the capital already" : "they have a house at the capital already";
        int cost = TownHouseCost(w, who);
        if (fromPurse) return head!.Purse >= cost ? null : $"it costs {Money.Credits(cost)} and your purse holds {Money.Show(head.Purse)}";
        return house.Treasury.Credits >= cost ? null : $"it costs {Money.Credits(cost)} and {Holds(house)}";
    }

    /// <summary>The head buys one of the house (or themselves) a house at the capital, out of the treasury or their own purse. It
    /// is that person's own.</summary>
    public static Heirloom BuyTownHouse(World w, Character head, Character who, bool fromPurse)
    {
        var house = w.House(head.HouseId)!; int cost = TownHouseCost(w, who); if (fromPurse) head.Purse -= cost; else house.Treasury.Credits -= cost;
        var item = MakeTownHouse(w, house, who, cost);
        if (who.Id != head.Id) Regard(w, who, head, fromPurse ? TownHousePurseRegard : TownHouseRegard);
        LifeSystem.Note(w, who, "deed", who.Id == head.Id ? $"Took a house at {CapitalName(w)}." : $"Was given a house at {CapitalName(w)} by {head.Name}.");
        if (house.Id == w.Protagonist?.HouseId)
            w.Log(who.Id == head.Id ? $"{head.Name} takes a house at {CapitalName(w)} for {Money.Credits(cost)}{(fromPurse ? ", out of their own purse" : "")}." : $"{head.Name} buys {who.Name} a house at {CapitalName(w)} for {Money.Credits(cost)}{(fromPurse ? ", out of their own purse" : "")}. It is theirs.");
        return item;
    }
    private static Heirloom MakeTownHouse(World w, House house, Character who, int cost)
    {
        Count(w, TownHousesKey);
        var item = new Heirloom { Id = $"{TownHouse}_{w.Counter(TownHousesKey)}", Name = $"a town house at {CapitalName(w)}", Kind = TownHouse, HolderId = who.Id, OfHouseId = house.Id, Value = cost * MadeWorthPct / 100 };
        w.Heirlooms.Add(item); return item;
    }
    /// <summary>One of the house who is standing for an office, or holds one, and has the price and a little beside buys their own
    /// (docs/PROCESS.md: relatives have wills of their own). True if they did.</summary>
    private static bool KinTownHouse(World w, House house, Character c)
    {
        if (w.FamilyAutonomy <= 0 || AtTheCapital(w, c.Id) || !(w.Candidacies.Any(cd => cd.CharacterId == c.Id) || LadderSystem.Seat(w, c.Id) != null)) return false;
        int cost = TownHouseCost(w, c); if (c.Purse < cost + KinHouseKeepsBack) return false;
        c.Purse -= cost; MakeTownHouse(w, house, c, cost); c.LastSpend = $"Bought a house at {CapitalName(w)} for {Money.Credits(cost)}.";
        LifeSystem.Note(w, c, "deed", $"Took a house at {CapitalName(w)}."); w.Log($"{c.Name} buys a house at {CapitalName(w)} out of their own purse."); return true;
    }

    // ---------------- other houses, and the year ----------------

    public const int OthersSpendOneIn = 3, OthersAfford = 3;

    /// <summary>Whether a house has nothing pressing on its money: it owes nothing and is at war with no other house. (A war of
    /// the whole Reach with a nation beyond the rim is every house's, and does not count.)</summary>
    public static bool Idle(World w, House h) => h.Debt <= 0 && !w.Wars.Any(x => x.Involves(h.Id));

    /// <summary>Another house that is rich and idle spends on one of the three, about one such year in three: a seat, land for a
    /// child of its head, or a house at the capital for the head. Only what it can pay for three times over.</summary>
    private static void OthersSpend(World w)
    {
        string? mine = w.Protagonist?.HouseId;
        foreach (var h in w.Houses.Values.Where(h => !World.Outsider(h.Id) && h.Id != mine && h.Treasury.Credits >= TownHouseLeast * OthersAfford).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            var head = Economy.HeadOf(w, h); if (head?.Alive != true || !Idle(w, h)) continue;
            uint x = Hash($"{h.Id}:{w.Turn}:spend") / 7; if (x % OthersSpendOneIn != 0) continue;
            bool Can(int cost) => h.Treasury.Credits >= cost * OthersAfford;
            for (int i = 0, first = (int)(x / 13 % 3); i < 3; i++)
            {
                int what = (first + i) % 3;
                if (what == 0 && SeatSite(w, h) is { } site && SeatRefusal(w, head, site.Id) == null && Can(SeatCost(h.SeatGrade + 1))) { BuildSeat(w, head, site.Id); break; }
                if (what == 1)
                {
                    var child = w.LivingCharacters().Where(c => c.HouseId == h.Id && c.Id != head.Id && (c.MotherId == head.Id || c.FatherId == head.Id || c.AdoptedById == head.Id))
                        .OrderBy(c => EstatesHeld(w, c.Id)).ThenByDescending(c => c.Age).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
                    if (child != null && EndowRefusal(w, head, child) == null && Can(EstateCost(w, child))) { Endow(w, head, child); break; }
                }
                if (what == 2 && TownHouseRefusal(w, head, head, false) == null && Can(TownHouseCost(w, head))) { BuyTownHouse(w, head, head, false); break; }
            }
        }
    }

    /// <summary>The year, for every house: seats, and what other houses spend. Called before the purses are reckoned.</summary>
    private static void RichYear(World w) { Seats(w); OthersSpend(w); }

    /// <summary>The year, for the player's house: what a child's land brings is kept for them until they are of age, and one of
    /// the house with business at the capital may buy a house there.</summary>
    private static void RichKin(World w, House house, Character head)
    {
        foreach (var c in w.LivingCharacters().Where(c => c.HouseId == house.Id && c.Age < Adult).OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            int income = EstateIncome(w, c.Id); if (income <= 0) continue;
            c.Purse += income; c.LastSpend = "Kept for them until they are of age.";
        }
        foreach (var c in Members(w, house).Where(c => c.Id != head.Id)) KinTownHouse(w, house, c);
    }
}
