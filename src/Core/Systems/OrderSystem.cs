using Praetoria.Core.Data;
using Praetoria.Core.Progression;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The three orders of the Republic (docs/REPUBLIC.md stage 3): the SENATE (rank, office, standing in the chamber), ARMS (fleets,
/// commands, victories) and COMMERCE (wealth, ventures, companies, a cornered good). A house has a standing in each, reckoned every
/// year from what it actually holds and has done, never granted. Most families are known for one: their calling. A few are great in
/// two; almost none in three.
/// <para>Standing is leverage. A great general or a great banker carries weight in the chamber whatever their rank. A house may sit in
/// the Senate by land, by the bench, by the purse (enrolled by the census) or by the sword. And a house that others depend on — its
/// debtors, its clients, the fleets that burn the fuel it corners, the holders of its companies' shares — has SWAY over how they
/// vote. That is how a new man buys his way to a command nobody would have given him: not with a purchase, with the votes of
/// everyone who owes him.</para>
/// RNG-free.
/// </summary>
public sealed class OrderSystem
{
    public const string Senate = "senate", Arms = "arms", Commerce = "commerce";
    public static readonly string[] All = { Senate, Arms, Commerce };
    public const int NotableAt = 20, GreatAt = 44, DominantAt = 64, CallingMargin = 8, CornerPct = 25, CensusFee = 60, ClientGift = 10, ClientStipend = 1,
        CanvassCredits = 10, CanvassCap = 5, GloryFadeEvery = 8, SwayCap = 4;
    public const string SeatBoughtFlag = "seat_bought", ClientTakenFlag = "client_taken", BecameClientFlag = "became_client", ClientLeftFlag = "client_left", PatronLostFlag = "patron_lost",
        TwoOrdersFlag = "house_of_two_orders", ThreeOrdersFlag = "house_of_three_orders", NewManConsulFlag = "new_man_consul", CornerFlag = "cornered_a_good";
    public static string GreatFlag(string order) => "order_great:" + order;
    public const string IsClientFlag = "is_client", IsPatronFlag = "is_patron", NewNameFlag = "new_name", NoSeatFlag = "no_seat";
    private static readonly string[] TickFlags = { IsClientFlag, IsPatronFlag, NewNameFlag, NoSeatFlag, ClientLeftFlag, PatronLostFlag, TwoOrdersFlag, ThreeOrdersFlag, NewManConsulFlag, CornerFlag, "order_great:senate", "order_great:arms", "order_great:commerce" };
    private static readonly string[] DeedFlags = { SeatBoughtFlag, ClientTakenFlag, BecameClientFlag };

    private readonly Economy? _economy; private readonly FleetSystem? _fleets; private readonly TitleCatalog _titles; private readonly OfficeCatalog _offices; private readonly MarketCatalog _market;
    public OrderSystem(Economy? economy, FleetSystem? fleets, TitleCatalog titles, OfficeCatalog offices, MarketCatalog market) { _economy = economy; _fleets = fleets; _titles = titles; _offices = offices; _market = market; }

    public static string Name(string order) => order switch { Senate => "the Senate", Arms => "Arms", Commerce => "Commerce", _ => order };
    public static string CallingName(string order) => order switch { Senate => "a senatorial house", Arms => "a house of arms", Commerce => "a house of commerce", _ => "a house of no order" };
    public static int Tier(int score) => score >= DominantAt ? 3 : score >= GreatAt ? 2 : score >= NotableAt ? 1 : 0;
    public static int Tier(House h, string order) => Tier(h.Order(order));
    public static string TierName(int tier) => tier switch { 3 => "dominant", 2 => "great", 1 => "notable", _ => "of no account" };
    public static int NextAt(int score) => score >= DominantAt ? 0 : score >= GreatAt ? DominantAt : score >= NotableAt ? GreatAt : NotableAt;
    public static IEnumerable<House> ClientsOf(World w, string houseId) => w.Houses.Values.Where(h => h.PatronId == houseId).OrderBy(h => h.Id, StringComparer.Ordinal);
    private static bool Real(House h) => h.Id != PirateSystem.HouseId;
    private static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }
    private static int Regard(World w, House from, House to)
    {
        var a = Economy.HeadOf(w, from); var b = Economy.HeadOf(w, to);
        return a != null && b != null ? w.GetRelationship(a.Id, b.Id)?.Disposition ?? 0 : 0;
    }
    private static void Warm(World w, House from, House to, int delta)
    {
        var a = Economy.HeadOf(w, from); var b = Economy.HeadOf(w, to); if (a == null || b == null || a.Id == b.Id) return;
        var rel = w.Relationship(a.Id, b.Id); rel.Disposition = Math.Clamp(rel.Disposition + delta, -100, 100);
    }

    // ---------------------------------------------------------------- measuring

    /// <summary>A house's standing in an order and what it is made of, largest part first.</summary>
    public (int score, List<(string what, int points)> parts) Measure(World w, House h, string order)
    {
        var parts = new List<(string, int)>(); void Add(string what, int pts) { if (pts > 0) parts.Add((what, pts)); }
        var living = w.LivingCharacters().Where(c => c.HouseId == h.Id).ToList();
        int Best(string track) => living.Where(c => c.CareerTrack == track).Select(c => c.CareerRank).DefaultIfEmpty(0).Max();
        var held = OfficeSystem.HeldBy(w, h.Id).Select(o => _offices.Def(o.OfficeId)).Where(d => d != null).Select(d => d!).ToList();
        switch (order)
        {
            case Senate:
                Add($"the rank of {_titles.NameOf(h.Title, w.Era)}", _titles.RankOf(h.Title) * 6);
                Add("legitimacy", h.Legitimacy / 6);
                Add("influence in the chamber", Math.Min(12, h.SphereInfluence.GetValueOrDefault("senate") * 2));
                Add("offices held", Math.Min(18, held.Where(d => d.Track.Length == 0 || d.Elected).Sum(d => 4 + d.Weight * 2)));
                if (w.Factions.Any(f => f.LeaderHouseId == h.Id)) Add("leads a party", 5);
                Add("magistracies its people have held", LadderSystem.SenateStanding(w, _offices, h));
                Add("a name at the bar", Best("law") * 2);
                if (h.Origin == "old") Add("an old name", 6);
                Add("clients", Math.Min(6, ClientsOf(w, h.Id).Count() * 2));
                break;
            case Arms:
                int hulls = w.Fleets.Values.Where(f => f.OwnerId == h.Id && !f.Office).Sum(f => f.Strength);
                Add($"{hulls} hull(s) of its own", Math.Min(40, hulls * 4));
                Add("rank in the Navy", Best("military") * 5);
                Add("victories beyond the Reach", Math.Min(10, w.Fleets.Values.Where(f => f.OwnerId == h.Id).Sum(f => f.ExpeditionWins) * 2));
                Add("victories still talked about", Math.Min(24, h.Glory * 4));
                Add("a command of the Reach", Math.Min(12, held.Where(d => d.Track == "military").Sum(d => 8) + (OfficeSystem.HeldBy(w, h.Id).Any(o => w.Fleets.Values.Any(f => f.Office && f.OwnerId == h.Id)) ? 4 : 0)));
                int vets = w.Fleets.Values.Where(f => f.OwnerId == h.Id).Select(f => f.Veterancy).DefaultIfEmpty(0).Max(); Add("veteran crews", Math.Min(8, vets * 2));
                break;
            case Commerce:
                Add($"{Wealth.TierFor(w, h.Id)} among the houses", Wealth.TierIndex(Wealth.TierFor(w, h.Id)) * 5);
                var ventures = w.BusinessesOf(h.Id).ToList(); Add($"{ventures.Count} venture(s)", Math.Min(14, ventures.Sum(b => 1 + b.Level)));
                Add("companies it directs", Math.Min(12, w.Companies.Count(c => c.ControllerId == h.Id) * 4));
                Add("paper on the Exchange", Math.Min(10, CompanySystem.PaperOf(w, h.Id) / 30));
                Add("houses in its debt", Math.Min(9, w.Houses.Values.Count(o => o.Debt > 0 && o.LenderId == h.Id && o.Id != h.Id) * 3));
                foreach (var g in h.Corners) Add($"a quarter of the Reach's {Resource.Label(g)}", 8);
                Add("rank in the Administration", Best("stewardship") * 2);
                Add("the Reach's purse", held.Where(d => d.Track == "stewardship").Sum(d => 8));
                break;
        }
        parts.Sort((a, b) => b.Item2.CompareTo(a.Item2));
        return (Math.Min(100, parts.Sum(x => x.Item2)), parts);
    }

    /// <summary>Which houses bring up a third or more of the Reach's production of a strategic good.</summary>
    private void Corner(World w, List<House> houses)
    {
        foreach (var h in houses) h.Corners.Clear();
        if (_economy == null) return;
        var made = houses.ToDictionary(h => h.Id, h => _economy.Compute(w, h.Id, _fleets));
        foreach (var good in _market.Goods.Where(g => Resource.IsStrategic(g.Id)).Select(g => g.Id))
        {
            var by = houses.ToDictionary(h => h.Id, h => made[h.Id].Lines.Sum(l => Math.Max(0, l.Delta.Get(good)))); int total = by.Values.Sum(); if (total < 6) continue;
            foreach (var h in houses) if (by[h.Id] * 100 >= total * CornerPct) h.Corners.Add(good);
        }
    }

    // ---------------------------------------------------------------- the yearly reckoning

    public void Tick(World w)
    {
        var houses = w.Houses.Values.Where(Real).OrderBy(h => h.Id, StringComparer.Ordinal).ToList(); var mine = w.Protagonist?.HouseId;
        foreach (var f in TickFlags) w.WorldFlags.Remove(f);
        foreach (var f in DeedFlags) if (w.WorldFlags.Contains(f) && w.Counter("mark:" + f) < w.Turn - 1) { w.WorldFlags.Remove(f); w.WorldCounters.Remove("mark:" + f); }
        foreach (var h in houses)
        {
            if (h.Origin.Length == 0) h.Origin = _titles.RankOf(h.Title) >= 2 ? "old" : "new";
            if (w.Turn > 0 && w.Turn % GloryFadeEvery == 0 && h.Glory > 0) h.Glory--;
        }
        var before = houses.ToDictionary(h => h.Id, h => h.Corners.Count);
        Corner(w, houses);
        // patronage: a client who has outgrown the patron, gone to war with them, or whose patron is no more, is a client no longer
        foreach (var c in houses.Where(h => h.PatronId.Length > 0).ToList())
        {
            var patron = w.House(c.PatronId);
            bool ends = patron == null || w.AtWar(c.Id, c.PatronId) || c.Wealth > patron.Wealth || patron.Treasury.Credits < -20;
            if (!ends) continue;
            if (c.Id == mine) w.WorldFlags.Add(PatronLostFlag); else if (c.PatronId == mine) w.WorldFlags.Add(ClientLeftFlag);
            w.Log(patron == null ? $"{c.Name} has no patron now." : $"{c.Name} is no longer the client of {patron.Name}.");
            c.PatronId = "";
        }
        foreach (var h in houses)
        {
            int greatWas = All.Count(o => Tier(h, o) >= 2);
            foreach (var o in All)
            {
                int was = Tier(h, o); int now = Measure(w, h, o).score; h.Orders[o] = now;
                if (h.Id == mine && was < 2 && Tier(now) >= 2 && w.Turn > 1) w.WorldFlags.Add(GreatFlag(o));
            }
            string top = All.OrderByDescending(o => h.Order(o)).ThenBy(o => o == h.Calling ? 0 : 1).ThenBy(o => Array.IndexOf(All, o)).First();
            if (h.Calling.Length == 0 || (top != h.Calling && h.Order(top) >= h.Order(h.Calling) + CallingMargin)) h.Calling = h.Order(top) > 0 ? top : h.Calling;
            int great = All.Count(o => Tier(h, o) >= 2);
            if (h.Id == mine && great > greatWas && w.Turn > 1) { if (great == 2) w.WorldFlags.Add(TwoOrdersFlag); else if (great >= 3) w.WorldFlags.Add(ThreeOrdersFlag); }
            if (h.Id == mine && h.Corners.Count > before.GetValueOrDefault(h.Id) && w.Turn > 1) w.WorldFlags.Add(CornerFlag);
            // a new man in the consul's chair: the first of his name
            if (h.Origin == "new" && !w.WorldFlags.Contains("first_consul:" + h.Id) && OfficeSystem.HeldBy(w, h.Id).Any(o => _offices.Def(o.OfficeId)?.Elected == true))
            {
                w.WorldFlags.Add("first_consul:" + h.Id); h.Legitimacy = Math.Min(100, h.Legitimacy + 5); if (h.Id == mine) w.WorldFlags.Add(NewManConsulFlag);
                w.Log($"{h.Name} holds a consul's chair: the first of that name to do so. The old families take note.");
            }
        }
        // where the player's own house stands, for content to read
        if (mine != null && w.House(mine) is { } me)
        {
            if (me.PatronId.Length > 0) w.WorldFlags.Add(IsClientFlag);
            if (ClientsOf(w, me.Id).Any()) w.WorldFlags.Add(IsPatronFlag);
            if (me.Origin == "new") w.WorldFlags.Add(NewNameFlag);
            if (!w.IsImperium && Door(w, me, _titles, SenateSystem.ProposerTitleRank, SenateSystem.ProposerLawRank) == null) w.WorldFlags.Add(NoSeatFlag);
        }
    }

    // ---------------------------------------------------------------- sway: who votes as you wish, and why

    /// <summary>How far <paramref name="voter"/> leans toward what <paramref name="target"/> wants because it depends on that house.</summary>
    public static (int lean, List<string> why) Sway(World w, House voter, House target)
    {
        var why = new List<string>(); int lean = 0; if (voter.Id == target.Id) return (0, why);
        if (voter.PatronId == target.Id) { lean += 3; why.Add("its client"); }
        if (target.PatronId == voter.Id) { lean += 2; why.Add("its patron"); }
        if (voter.PatronId.Length > 0 && voter.PatronId == target.PatronId) { lean += 1; why.Add("a fellow client"); }
        if (voter.Debt > 0 && voter.LenderId == target.Id) { lean += 2; why.Add("in its debt"); }
        if (target.Corners.Contains(Resource.Helium3) && !voter.Corners.Contains(Resource.Helium3) && w.Fleets.Values.Any(f => f.OwnerId == voter.Id)) { lean += 1; why.Add("its fleets burn that house's fuel"); }
        if (w.Companies.Any(c => c.ControllerId == target.Id && c.HeldBy(voter.Id) * 10 >= c.Shares)) { lean += 1; why.Add("holds its companies' shares"); }
        return (Math.Min(SwayCap, lean), why);
    }

    /// <summary>Every house that leans the way this one wants, strongest first.</summary>
    public static List<(House house, int lean, List<string> why)> Swayed(World w, House target) =>
        w.Houses.Values.Where(h => Real(h) && h.Id != target.Id).Select(h => { var s = Sway(w, h, target); return (house: h, s.lean, s.why); }).Where(x => x.lean > 0)
            .OrderByDescending(x => x.lean).ThenBy(x => x.house.Id, StringComparer.Ordinal).ToList();

    /// <summary>What a great general or a great banker adds to a house's voice in the chamber, and what its clients add.</summary>
    public static int WeightOf(World w, House h) => Tier(h, Arms) + Tier(h, Commerce) + Math.Min(2, ClientsOf(w, h.Id).Count() / 2);

    /// <summary>The door by which the house sits in the Senate: land, the bench, the purse, the sword; or none.</summary>
    public static string? Door(World w, House h, TitleCatalog titles, int titleRank, int lawRank)
    {
        if (titles.RankOf(h.Title) >= titleRank) return "land";
        if (Economy.HeadOf(w, h) is { CareerTrack: "law" } head && head.CareerRank >= lawRank) return "the bench";
        if (h.Enrolled) return "the purse";
        return Tier(h, Arms) >= 2 ? "the sword" : null;
    }

    // ---------------------------------------------------------------- the purse: a seat by the census

    public string? EnrollRefusal(World w, House h, bool seated)
    {
        if (w.IsImperium) return "the throne, not the census, seats a house now";
        if (h.Enrolled || seated) return "the house sits in the Senate already";
        if (Tier(h, Commerce) < 1) return "the census enrols only a house of some account in commerce";
        return h.Treasury.Credits >= CensusFee ? null : $"the census asks {CensusFee} credits";
    }
    public void Enroll(World w, House h)
    {
        h.Treasury.Credits -= CensusFee; var chair = RegionSystem.CapitalHolder(w); if (chair != null && chair.Id != h.Id) chair.Treasury.Credits += CensusFee;
        h.Enrolled = true; h.Legitimacy = Math.Min(100, h.Legitimacy + 3);
        if (h.Id == w.Protagonist?.HouseId) Mark(w, SeatBoughtFlag);
        w.Log($"{h.Name} is enrolled in the Senate by the census: a seat held by the purse. The old families say what they always say.");
    }

    // ---------------------------------------------------------------- patrons and clients

    public static int ClientCap(House patron) => 1 + Tier(patron, Commerce) + Tier(patron, Senate);
    public static int ClientCost(House client) => Math.Max(0, client.Debt) + ClientGift;

    public string? TakeClientRefusal(World w, House patron, House? client)
    {
        if (client == null || !Real(client) || client.Id == patron.Id) return "no such house";
        if (client.PatronId == patron.Id) return "already your client";
        if (client.PatronId.Length > 0) return $"already the client of {w.House(client.PatronId)?.Name}";
        if (patron.PatronId.Length > 0) return "a client takes no clients";
        if (w.AtWar(patron.Id, client.Id)) return "you are at war with it";
        if (ClientsOf(w, patron.Id).Count() >= ClientCap(patron)) return $"the house can keep {ClientCap(patron)} client(s) at its standing";
        if (patron.Wealth < client.Wealth * 2 || patron.Wealth < 60) return "a patron must be twice as rich as the client";
        if (ClientsOf(w, client.Id).Any()) return "it is a patron itself";
        bool needy = client.Debt > 0 || client.Treasury.Credits < 20 || Wealth.TierIndex(Wealth.TierFor(w, client.Id)) <= 1 || !w.HoldingsOf(client.Id).Any(x => x.IsPlanet);
        if (!needy) return "it has no need of a patron";
        if (Regard(w, client, patron) <= -30) return "its head would not take your coin";
        return patron.Treasury.Credits >= ClientCost(client) ? null : $"needs {ClientCost(client)} credits: its debts and a gift";
    }

    /// <summary>The patron pays the client's debts and a gift; the client is the patron's from now on: its votes, its gratitude.</summary>
    public void TakeClient(World w, House patron, House client)
    {
        int cost = ClientCost(client); patron.Treasury.Credits -= cost;
        if (client.Debt > 0) { if (w.House(client.LenderId) is { } lender && lender.Id != patron.Id) lender.Treasury.Credits += client.Debt; client.Debt = 0; client.LenderId = ""; }
        client.Treasury.Credits += ClientGift; client.PatronId = patron.Id; Warm(w, client, patron, 25);
        var mine = w.Protagonist?.HouseId; if (patron.Id == mine) Mark(w, ClientTakenFlag); else if (client.Id == mine) Mark(w, BecameClientFlag);
        w.Log($"{client.Name} becomes the client of {patron.Name}, which has paid its debts.");
    }

    public string? SeekPatronRefusal(World w, House client, House? patron)
    {
        if (patron == null || !Real(patron) || patron.Id == client.Id) return "no such house";
        if (client.PatronId.Length > 0) return "the house has a patron already";
        if (ClientsOf(w, client.Id).Any()) return "a patron does not seek one";
        if (patron.PatronId.Length > 0) return "it is a client itself";
        if (w.AtWar(patron.Id, client.Id)) return "you are at war with it";
        if (patron.Wealth < client.Wealth * 2 || patron.Wealth < 60) return "it is not rich enough to be your patron";
        if (ClientsOf(w, patron.Id).Count() >= ClientCap(patron)) return "it keeps as many clients as it can";
        return Regard(w, patron, client) >= 10 ? null : "its head does not think well enough of you";
    }
    public void SeekPatron(World w, House client, House patron)
    {
        client.PatronId = patron.Id; Warm(w, patron, client, 10); Warm(w, client, patron, 10);
        if (client.Id == w.Protagonist?.HouseId) Mark(w, BecameClientFlag);
        w.Log($"{client.Name} puts itself under the protection of {patron.Name}.");
    }

    /// <summary>The tie is cut. Whoever cut it is not forgiven quickly.</summary>
    public void Part(World w, House client, bool byClient)
    {
        var patron = w.House(client.PatronId); client.PatronId = ""; if (patron == null) return;
        if (byClient) Warm(w, patron, client, -30); else Warm(w, client, patron, -20);
        w.Log(byClient ? $"{client.Name} leaves the protection of {patron.Name}." : $"{patron.Name} turns {client.Name} away.");
    }

    // ---------------------------------------------------------------- canvassing: coin behind a candidate

    public static string CanvassKey(string nomineeId) => "canvass:" + nomineeId;
    public static string CanvassedKey(string houseId) => "canvassed:" + houseId;
    public string? CanvassRefusal(World w, House h, string nomineeId, IEnumerable<string> nominees)
    {
        if (w.IsImperium) return "there are no elections under the throne";
        if (!nominees.Contains(nomineeId)) return "not a nominee";
        if (w.Counter(CanvassedKey(h.Id)) >= CanvassCap) return $"the house has canvassed all it decently can this election ({CanvassCap})";
        return h.Treasury.Credits >= CanvassCredits ? null : $"needs {CanvassCredits} credits";
    }
    /// <summary>Largesse in the candidate's name: games, grain, debts forgiven. A vote's weight at the next election.</summary>
    public void Canvass(World w, House h, string nomineeId)
    {
        h.Treasury.Credits -= CanvassCredits; w.WorldCounters[CanvassKey(nomineeId)] = w.Counter(CanvassKey(nomineeId)) + 1; w.WorldCounters[CanvassedKey(h.Id)] = w.Counter(CanvassedKey(h.Id)) + 1;
        if (nomineeId != h.Id && w.House(nomineeId) is { } n) Warm(w, n, h, 5);
        w.Log(nomineeId == h.Id ? $"{h.Name} spends {CanvassCredits} credits on the voters in its own name." : $"{h.Name} spends {CanvassCredits} credits on the voters in the name of {w.House(nomineeId)?.Name}.");
    }
    public static void SweepCanvass(World w) { foreach (var k in w.WorldCounters.Keys.Where(k => k.StartsWith("canvass:") || k.StartsWith("canvassed:")).ToList()) w.WorldCounters.Remove(k); }
}
