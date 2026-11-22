using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The provinces of a world (docs/PROVINCES.md; the owner, 2026-11-20: "there are three regions on each planet and then the planet
/// overall. The governor of the planet ... also controls one of those regions. And then the other two regions are controlled by
/// different houses ... Governorship is for life unless the leader over you tries to [take it]; in that case you can either accept it
/// and step down and have that other family be in charge of you, which moves you down in the rankings, or you can fight in basically
/// what would be a civil war"). A planet has three provinces: the seat, which is always the house's that holds the world, and two
/// more, each given over to one thing and held by a house for life. A province pays its holder what its focus yields. A holder is
/// removed only by the one over it, who must ask; the holder steps down or takes up arms. RNG-free: who is given what, and who asks
/// what of whom, comes from the names and the year.
/// </summary>
public static class ProvinceSystem
{
    public const string DemandFlag = "seat_demanded";
    public const string DemandsCounter = "seat_demands", YieldedCounter = "seats_yielded", WarsCounter = "seat_wars";
    public const string Yielded = "yielded", War = "war", Pending = "pending";

    private static uint Hash(string s) { uint h = 2166136261; foreach (var ch in s) h = (h ^ ch) * 16777619; return h; }

    /// <summary>The house that holds a province: the world's own for its seat, and for a province given to nobody else.</summary>
    public static string HolderOf(Holding h, int index) => index <= 0 || index >= h.Provinces.Count || h.Provinces[index].HolderId.Length == 0 ? h.OwnerId : h.Provinces[index].HolderId;
    /// <summary>Whether a house holds a province of this world other than its seat.</summary>
    public static int ProvinceOf(Holding h, string houseId) { for (int i = 1; i < h.Provinces.Count; i++) if (HolderOf(h, i) == houseId) return i; return -1; }
    public static bool Divided(Holding h) { for (int i = 1; i < h.Provinces.Count; i++) if (HolderOf(h, i) != h.OwnerId) return true; return false; }

    // ---------------- the first day ----------------

    /// <summary>Every planet that has no provinces yet is given its three: the seat, and two whose focus suits the world, held by
    /// houses of the same part of the galaxy (the ones with least first). A nation's world, and a colony, is all its holder's.</summary>
    public static void Seed(World w, HoldingCatalog cat)
    {
        var rules = cat.Provinces; var g = w.Galaxy; if (rules.Focuses.Count == 0 || g == null || rules.Focus(ProvinceRules.Seat) == null) return;
        var todo = w.Holdings.Values.Where(h => h.IsPlanet && h.Provinces.Count == 0 && g.System(h.SystemId) != null).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        if (todo.Count == 0) { CitySystem.Seed(w, cat); return; }   // a game from before there were cities has them laid out here
        var held = new Dictionary<string, int>(); var byRegion = new Dictionary<string, List<string>>(); var landed = new HashSet<string>();
        foreach (var h in w.Holdings.Values.OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            for (int i = 1; i < h.Provinces.Count; i++) if (h.Provinces[i].HolderId.Length > 0) held[h.Provinces[i].HolderId] = held.GetValueOrDefault(h.Provinces[i].HolderId) + 1;
            if (!h.IsPlanet || World.Outsider(h.OwnerId) || w.House(h.OwnerId) == null || g.System(h.SystemId) is not { } s) continue;
            landed.Add(h.OwnerId); if (!byRegion.TryGetValue(s.RegionId, out var list)) byRegion[s.RegionId] = list = new List<string>(); if (!list.Contains(h.OwnerId)) list.Add(h.OwnerId);
        }
        var landless = w.Houses.Values.Where(x => !World.Outsider(x.Id) && !landed.Contains(x.Id)).Select(x => x.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var everyone = w.Houses.Values.Where(x => !World.Outsider(x.Id)).Select(x => x.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
        foreach (var h in todo)
        {
            var sys = g.System(h.SystemId)!; string world = sys.Name.StartsWith("The ", StringComparison.Ordinal) ? sys.Name[4..] : sys.Name;
            Province Make(ProvinceFocus f, string holder) => new() { Name = string.Format(f.Title, world), Focus = f.Id, HolderId = holder, Since = w.Turn, Level = 1 };
            h.Provinces.Add(Make(rules.Focus(ProvinceRules.Seat)!, ""));
            bool shared = !World.Outsider(h.OwnerId) && w.House(h.OwnerId) != null && !h.Id.StartsWith("colony_", StringComparison.Ordinal);
            var taken = new HashSet<string> { h.OwnerId };
            foreach (var f in Focuses(rules, w, h, sys))
            {
                string holder = "";
                if (shared)
                {
                    var pool = (byRegion.TryGetValue(sys.RegionId, out var near) ? near : new List<string>()).Concat(landless).Where(x => !taken.Contains(x)).Distinct().ToList();
                    if (pool.Count == 0) pool = everyone.Where(x => !taken.Contains(x)).ToList();
                    var pick = pool.OrderBy(x => held.GetValueOrDefault(x)).ThenBy(x => Hash(h.Id + ":" + f.Id + ":" + x)).ThenBy(x => x, StringComparer.Ordinal).FirstOrDefault();
                    if (pick != null) { holder = pick; taken.Add(pick); held[pick] = held.GetValueOrDefault(pick) + 1; }
                }
                h.Provinces.Add(Make(f, holder));
            }
        }
        w.WorldCounters["provinces_seeded"] = 1; w.ProvincesChanged();
        CitySystem.Seed(w, cat);
    }

    /// <summary>The two things a world's other provinces are given over to: a deposit its system carries first, the march on a
    /// fortress or a key of the lanes, then what suits its kind of world, in an order that is the world's own.</summary>
    private static List<ProvinceFocus> Focuses(ProvinceRules rules, World w, Holding h, Galaxy.StarSystem sys)
    {
        var first = new List<ProvinceFocus>(); var rest = new List<ProvinceFocus>();
        foreach (var f in rules.Focuses)
        {
            if (f.Id == ProvinceRules.Seat) continue;
            if (f.Deposit.Length > 0) { if (sys.Deposit(f.Deposit) > 0) first.Add(f); continue; }
            if (f.Military) { if (h.Specialization.Contains("fortress", StringComparison.Ordinal) || w.Galaxy!.IsChokepoint(sys.Id)) first.Add(f); else rest.Add(f); continue; }
            if (f.Classes.Contains(sys.PlanetClass)) rest.Add(f);
        }
        var order = first.OrderBy(f => Hash(h.Id + f.Id)).Concat(rest.OrderBy(f => Hash(h.Id + f.Id))).ToList();
        foreach (var f in rules.Focuses.Where(f => f.Id != ProvinceRules.Seat && f.Deposit.Length == 0).OrderBy(f => Hash(h.Id + f.Id))) if (order.Count < 2 && !order.Contains(f)) order.Add(f);
        return order.Take(2).ToList();
    }

    // ---------------- what a province does ----------------

    /// <summary>What a province brings in a year: what its focus yields, by its level, and what its buildings yield less what they cost to keep.</summary>
    public static Resources Yield(World w, HoldingCatalog cat, Holding planet, Province p)
    {
        var d = new Resources(); if (cat.Provinces.Focus(p.Focus) is not { } f) return d; var sys = w.Galaxy?.System(planet.SystemId);
        foreach (var r in Resource.All) d.Set(r, f.Yield.Get(r) * Math.Max(1, p.Level));
        var city = CitySystem.Yield(cat, p); foreach (var r in Resource.All) d.Add(r, city.Get(r));   // what its chief city gives, whatever the province's level (docs/CITIES.md)
        foreach (var id in p.Buildings) if (cat.Work(id) is { } b) foreach (var r in Resource.All) d.Add(r, ((b.Deposit == r ? b.Yield.Get(r) * Math.Max(1, sys?.Deposit(r) ?? 1) : b.Yield.Get(r)) - b.Upkeep.Get(r)) * p.LevelOf(id));
        return d;
    }
    /// <summary>What the house that holds a province under another owes the holder of the world this year (the owner, 2026-11-20: yes to
    /// "a share of yield, troops, a vote"): the tithe, by the province's level, and the levy too in a year the holder of the world is
    /// at war. Nothing for a province that is the world's holder's own, on a world in revolt, or held by a house at war with that holder.</summary>
    public static Resources Dues(World w, HoldingCatalog cat, Holding planet, int index)
    {
        var d = new Resources(); if (index <= 0 || index >= planet.Provinces.Count || planet.InRebellion) return d;
        string holder = HolderOf(planet, index); var p = planet.Provinces[index]; if (holder == planet.OwnerId || w.AtWar(holder, planet.OwnerId) || p.Withheld || DuesOf(w, p) <= 0) return d;
        int level = Math.Max(1, p.Level) * Math.Min(Heavy, DuesOf(w, p)); bool war = w.EnemiesOf(planet.OwnerId).Any();
        foreach (var r in Resource.All) d.Set(r, (cat.Provinces.Tithe.Get(r) + (war ? cat.Provinces.Levy.Get(r) : 0)) * level);
        return d;
    }
    public const int Forgiven = 0, Customary = 1, Heavy = 2;
    /// <summary>What is asked of a province this year: nothing while its dues are commuted, else what the house over it has set.</summary>
    public static int DuesOf(World w, Province p) => p.EasedUntil > w.Turn ? Forgiven : p.Dues;
    public static string DuesName(int dues) => dues <= Forgiven ? "forgiven" : dues >= Heavy ? "heavy" : "customary";
    /// <summary>How far a house leans, in the Senate, the way of a house it holds a province under: the more where its dues are
    /// forgiven, not at all where they are heavy or it is withholding them, or the two are at war.</summary>
    public static int LeanUnder(World w, string houseId, string overId)
    {
        if (houseId == overId || w.AtWar(houseId, overId)) return 0; int best = 0;
        foreach (var (h, i) in w.ProvincesOf(houseId)) { if (h.OwnerId != overId || i >= h.Provinces.Count) continue; var p = h.Provinces[i]; if (p.Withheld || DuesOf(w, p) >= Heavy) continue; best = Math.Max(best, DuesOf(w, p) <= Forgiven ? VoteLean + 1 : VoteLean); }
        return best;
    }
    /// <summary>Why the house that holds a world may not set a province's dues so, or null.</summary>
    public static string? DuesRefusal(World w, House by, Holding planet, int index, int dues)
    {
        if (!planet.IsPlanet || index <= 0 || index >= planet.Provinces.Count) return "no such province";
        if (planet.OwnerId != by.Id) return "only the house that holds the world sets its dues";
        if (HolderOf(planet, index) == by.Id) return "it is your own";
        if (dues < Forgiven || dues > Heavy) return "no such dues";
        if (dues >= Heavy && planet.Provinces[index].SettledUntil > w.Turn) return $"the peace holds its dues at what is customary until year {planet.Provinces[index].SettledUntil}";
        if (planet.Provinces[index].EasedUntil > w.Turn) return $"its dues are commuted until year {planet.Provinces[index].EasedUntil}";
        return planet.Provinces[index].Dues == dues ? "they are " + DuesName(dues) + " already" : null;
    }
    public static void SetDues(World w, Holding planet, int index, int dues)
    {
        var p = planet.Provinces[index]; int was = p.Dues; p.Dues = dues; w.ProvincesChanged();
        w.Log($"{w.House(planet.OwnerId)?.Name ?? "The holder of the world"} {(dues > was ? "raises" : "eases")} the dues of {p.Name}: they are {DuesName(dues)} now.");
    }
    /// <summary>Why a house may not begin, or stop, withholding the dues of a province it holds under another, or null.</summary>
    public static string? WithholdRefusal(World w, House by, Holding planet, int index, bool withhold)
    {
        if (!planet.IsPlanet || index <= 0 || index >= planet.Provinces.Count) return "no such province";
        if (HolderOf(planet, index) != by.Id || planet.OwnerId == by.Id) return "you hold no province under another here";
        var p = planet.Provinces[index]; if (p.Withheld == withhold) return withhold ? "you are withholding them" : "you are paying them";
        return withhold && DuesOf(w, p) <= Forgiven ? "nothing is asked of you" : null;
    }
    public static void Withhold(World w, Holding planet, int index, bool withhold)
    {
        var p = planet.Provinces[index]; p.Withheld = withhold; w.ProvincesChanged(); if (withhold) w.WorldCounters["dues_withheld"] = w.Counter("dues_withheld") + 1;
        string holder = w.House(HolderOf(planet, index))?.Name ?? "Its holder", over = w.House(planet.OwnerId)?.Name ?? "the holder of the world";
        w.Log(withhold ? $"{holder} will pay no more dues for {p.Name}. {over} may ask it to go." : $"{holder} pays the dues of {p.Name} again.");
        // the house over it is told when it begins and when it ends (docs/PLAYTHROUGH.md: "say what ends the withholding, and when it has ended")
        if (planet.OwnerId == w.Protagonist?.HouseId && HolderOf(planet, index) != planet.OwnerId)
            w.Tell(withhold ? "warn" : "good", withhold ? $"{holder} has stopped paying the dues of {p.Name}: you may ask it to give the province up" : $"{holder} pays the dues of {p.Name} again", "galaxy:" + planet.SystemId, HolderOf(planet, index));
    }
    /// <summary>What commuting a province's dues costs its holder, paid at once to the house over it: so many years of its tithe.</summary>
    public static Resources CommuteCost(HoldingCatalog cat, Province p) { var c = new Resources(); foreach (var r in Resource.All) c.Set(r, cat.Provinces.Tithe.Get(r) * Math.Max(1, p.Level) * cat.Provinces.CommuteYears); return c; }
    /// <summary>Why the dues of a province may not be commuted at the word of this house (its holder, or the house over it), or null.
    /// The other side must agree: a house over it that is at war wants its levy and will not; a holder that cannot find the coin cannot.</summary>
    public static string? CommuteRefusal(World w, HoldingCatalog cat, House by, Holding planet, int index, bool explain = true)
    {
        if (!planet.IsPlanet || index <= 0 || index >= planet.Provinces.Count) return "no such province";
        string holderId = HolderOf(planet, index); var p = planet.Provinces[index]; if (holderId == planet.OwnerId) return "it owes nobody";
        if (by.Id != holderId && by.Id != planet.OwnerId) return "it is between its holder and the house over it";
        if (w.AtWar(holderId, planet.OwnerId)) return "the two houses are at war";
        if (p.Withheld) return "its dues are being withheld";
        if (p.EasedUntil > w.Turn) return $"they are commuted already, until year {p.EasedUntil}";
        if (p.Dues <= Forgiven) return "nothing is asked of it";
        var holder = w.House(holderId); var cost = CommuteCost(cat, p); if (holder == null) return "no such house";
        if (w.EnemiesOf(planet.OwnerId).Any()) return by.Id == planet.OwnerId ? "you are at war: you need its levy" : "the house over you is at war and wants its levy";
        if (by.Id == holderId) return holder.Treasury.CanAfford(cost) ? null : !explain ? "needs more" : $"{holder.Treasury.Short(cost)}";
        var cushion = cost.Clone(); cushion.Credits += 20;
        return holder.Treasury.CanAfford(cushion) ? null : $"{holder.Name} cannot find it: commuting takes {Presentation.CourtProjection.Bundle(cost)} and a house keeps 20 credits back, and it holds {Presentation.CourtProjection.Bundle(holder.Treasury)}";
    }
    public static void Commute(World w, HoldingCatalog cat, Holding planet, int index)
    {
        var p = planet.Provinces[index]; var holder = w.House(HolderOf(planet, index)); var over = w.House(planet.OwnerId); if (holder == null || over == null) return; var cost = CommuteCost(cat, p);
        holder.Treasury.Spend(cost); foreach (var r in Resource.All) over.Treasury.Add(r, cost.Get(r));
        p.EasedUntil = w.Turn + cat.Provinces.CommuteTerm; w.ProvincesChanged(); w.WorldCounters["dues_commuted"] = w.Counter("dues_commuted") + 1;
        w.Log($"{holder.Name} commutes the dues of {p.Name}: {Presentation.CourtProjection.Bundle(cost)} to {over.Name} now, and nothing owed until year {p.EasedUntil}.");
    }
    /// <summary>The Senate has voted (docs/PROVINCES.md): every house that cast a vote on what a house over it moved, or on what is aimed
    /// at that house, has it counted. A vote against adds one and a vote its way takes one off; at the number it will bear, the
    /// house over it asks for the province, if it can. The player's is also felt at once in the regard between the two heads.</summary>
    /// <summary>What a house over another wants of a motion: carried (1) when it moved it or it is to its good, thrown out (-1)
    /// when it is aimed against it, and nothing (0) when it does not touch it.</summary>
    public static int Wish(Motion m, House over, string interest, bool aimedAtHouse) =>
        m.TargetId == over.Id && aimedAtHouse ? (interest is "censure" or "embargo" or "breakup" ? -1 : 1) : m.ProposerHouseId == over.Id ? 1 : 0;
    /// <summary>The wishes of every house a house holds a province under, on one motion (docs/PLAYTHROUGH.md, Medium, The Senate,
    /// P90): what each wants, how often the house has voted against it on what touched it, how many times it bears that before it
    /// asks for the province, and the province held under it.</summary>
    public static List<(House over, int want, int defied, int bears, string province)> Wishes(World w, HoldingCatalog cat, Motion m, string interest, bool aimedAtHouse, string houseId)
    {
        var list = new List<(House, int, int, int, string)>();
        foreach (var over in Overs(w, houseId).OrderBy(o => o.Id, StringComparer.Ordinal))
        {
            int want = Wish(m, over, interest, aimedAtHouse); if (want == 0) continue;
            string province = w.ProvincesOf(houseId).Where(x => x.h.OwnerId == over.Id).Select(x => x.h.Provinces[x.i].Name).FirstOrDefault() ?? "a province";
            list.Add((over, want, w.Counter(DefiedKey(over.Id, houseId)), cat.Provinces.Defiances, province));
        }
        return list;
    }
    public static void NoteVotes(World w, HoldingCatalog cat, Motion m, string interest, bool aimedAtHouse, string? mine, int oblige, int defy)
    {
        foreach (var (houseId, cast) in m.Votes.OrderBy(v => v.Key, StringComparer.Ordinal).ToList())
        {
            if (cast == 0 || World.Outsider(houseId) || w.House(houseId) is not { } under) continue;
            foreach (var over in Overs(w, houseId).ToList())
            {
                int want = Wish(m, over, interest, aimedAtHouse); if (want == 0) continue;
                bool with = cast == want; string key = DefiedKey(over.Id, houseId); int was = w.Counter(key), now = Math.Max(0, was + (with ? -1 : 1));
                if (now == 0) w.WorldCounters.Remove(key); else w.WorldCounters[key] = now;
                if (houseId != mine) { if (!with && now == cat.Provinces.Defiances && was < now) w.Log($"{under.Name} has voted against {over.Name}, which it holds a province under, once too often."); continue; }
                if (under.PatronId == over.Id) continue;   // (its patron has noted it already)
                if (Economy.HeadOf(w, over) is { } oh && Economy.HeadOf(w, under) is { } uh) { var rel = w.Relationship(oh.Id, uh.Id); rel.Disposition = Math.Clamp(rel.Disposition + (with ? oblige : defy), -100, 100); }
                w.WorldFlags.Add(with ? "obliged_overlord" : "defied_overlord");
                w.Log(with ? $"{under.Name} votes as {over.Name}, which it holds a province under, wished. It is noted." : $"{under.Name} votes against {over.Name}, which it holds a province under. It is noted.");
            }
        }
    }
    /// <summary>What a province adds to its world beyond coin: what its buildings add to where the world's prosperity heads, to the
    /// people it can hold (by their level), and to its unrest a year.</summary>
    public static (int prosperity, int population, int unrest) OnWorld(HoldingCatalog cat, Holding planet)
    {
        int pr = 0, po = 0, un = 0;
        foreach (var p in planet.Provinces) foreach (var id in p.Buildings) if (cat.Work(id) is { } b) { pr += b.Prosperity; po += b.Population * p.LevelOf(id); un += b.Unrest; }
        return (pr, po, un);
    }
    /// <summary>Whether a house holds a province of a world another house holds: it owes that house, and leans its way in the Senate.</summary>
    public static bool Under(World w, string houseId, string overId) { if (houseId == overId) return false; foreach (var (h, _) in w.ProvincesOf(houseId)) if (h.OwnerId == overId && !w.AtWar(houseId, overId)) return true; return false; }
    /// <summary>The houses a house holds a province under, and is not at war with.</summary>
    public static IEnumerable<House> Overs(World w, string houseId)
    {
        var seen = new HashSet<string>();
        foreach (var (h, _) in w.ProvincesOf(houseId)) if (h.OwnerId != houseId && seen.Add(h.OwnerId) && !w.AtWar(houseId, h.OwnerId) && w.House(h.OwnerId) is { } o) yield return o;
    }
    public static string DefiedKey(string overId, string houseId) => "defied_over:" + overId + ":" + houseId;
    public const int VoteLean = 2;
    /// <summary>What the provinces of a house's own world pay it: its seat, and any province it has given to nobody.</summary>
    public static IEnumerable<(string name, Resources net)> OwnLines(World w, HoldingCatalog cat, Holding planet)
    {
        if (planet.InRebellion) yield break;
        for (int i = 0; i < planet.Provinces.Count; i++)
        {
            if (i == 0 || planet.Provinces[i].HolderId.Length == 0) { var y = Yield(w, cat, planet, planet.Provinces[i]); if (!y.IsEmpty()) yield return (planet.Provinces[i].Name, y); }
            else { var due = Dues(w, cat, planet, i); if (!due.IsEmpty()) yield return ("Dues of " + planet.Provinces[i].Name, due); }
        }
    }
    /// <summary>What the provinces a house was given pay it, wherever they are. A province of a world in revolt pays nothing, nor does
    /// one whose holder is at war with the house that holds its world.</summary>
    public static IEnumerable<(string name, Resources net)> GivenLines(World w, HoldingCatalog cat, string houseId)
    {
        foreach (var (h, i) in w.ProvincesOf(houseId))
        {
            if (i >= h.Provinces.Count || h.InRebellion || (h.OwnerId != houseId && w.AtWar(houseId, h.OwnerId))) continue;
            var y = Yield(w, cat, h, h.Provinces[i]); var due = Dues(w, cat, h, i); foreach (var r in Resource.All) y.Add(r, -due.Get(r));
            if (!y.IsEmpty()) yield return (h.Provinces[i].Name, y);
        }
    }
    public static double Defense(HoldingCatalog cat, Holding planet) { double d = CitySystem.Defense(cat, planet); foreach (var p in planet.Provinces) { d += (cat.Provinces.Focus(p.Focus)?.Defense ?? 0) * Math.Max(1, p.Level); foreach (var id in p.Buildings) d += (cat.Work(id)?.Defense ?? 0) * p.LevelOf(id); } return d; }

    // ---------------- building in a province ----------------

    // ---------------- giving a province to another house (docs/PLAYTHROUGH.md, Medium, Building and holding, B21) ----------------

    /// <summary>What the head of a house given a province thinks the better of the giver, and what the giving is worth in legitimacy.</summary>
    public const int GrantRegard = 12, GrantLegitimacy = 1;
    public const string GrantedCounter = "provinces_granted";
    /// <summary>Why the house that holds a world may not give one of its provinces to another house, or null. Only a province it
    /// still holds itself is its to give; the seat goes with the world; and a house holds one province of a world, not two.</summary>
    public static string? GrantRefusal(World w, House by, Holding? planet, int index, string toId)
    {
        if (planet == null || !planet.IsPlanet || planet.OwnerId != by.Id) return "not your world";
        if (index <= 0 || index >= planet.Provinces.Count) return "the seat goes with the world";
        string holder = HolderOf(planet, index);
        if (holder != by.Id) return $"{w.House(holder)?.Name ?? "another house"} holds it for life: it is asked for, not given away";
        var to = w.House(toId); if (to == null || World.Outsider(toId) || toId == by.Id) return "choose a house to give it to";
        if (Economy.HeadOf(w, to) == null) return $"{to.Name} has no head to take it";
        if (w.AtWar(by.Id, toId)) return $"you are at war with {to.Name}";
        if (planet.InRebellion) return "its world is in revolt";
        if (planet.Provinces[index].FoughtOver) return "it is being fought over";
        return ProvinceOf(planet, toId) > 0 ? $"{to.Name} holds a province of {planet.Name} already" : null;
    }
    /// <summary>The province is another house's now, for life, with what was built in it. It owes the customary dues.</summary>
    public static void Grant(World w, House by, Holding planet, int index, string toId)
    {
        var to = w.House(toId)!; var p = planet.Provinces[index];
        p.HolderId = to.Id; p.Since = w.Turn; p.Withheld = false; p.Dues = Customary; p.Levied = false; p.LevyLost = false; p.EasedUntil = 0; w.ProvincesChanged();
        var mine = Economy.HeadOf(w, by); var theirs = Economy.HeadOf(w, to);
        if (mine != null && theirs != null && mine.Id != theirs.Id) { var r = w.Relationship(theirs.Id, mine.Id); r.Disposition = Math.Clamp(r.Disposition + GrantRegard, -100, 100); }
        by.Legitimacy = Math.Clamp(by.Legitimacy + GrantLegitimacy, 0, 100);
        w.WorldCounters[GrantedCounter] = w.Counter(GrantedCounter) + 1;
        w.Log($"{by.Name} gives {p.Name} on {planet.Name} to {to.Name}, to hold for life under it.");
        if (to.Id == w.Protagonist?.HouseId) w.Tell("good", $"{by.Name} has given your house {p.Name} on {planet.Name}, to hold for life. What it yields is yours, less the customary dues.", "galaxy:" + planet.SystemId);
    }

    public static string Target(string holdingId, int index) => holdingId + "@" + index;
    public static (string holdingId, int index) Parse(string target) { int at = target.LastIndexOf('@'); return at < 0 ? (target, -1) : (target[..at], int.TryParse(target[(at + 1)..], out var i) ? i : -1); }
    public static int Rising(World w, Holding planet, int index) { string t = Target(planet.Id, index); int n = 0; foreach (var p in w.Projects) if (p.Kind == Project.ProvinceWork && p.TargetId == t) n++; return n; }
    /// <summary>The building slots of a province (the owner, 2026-11-20: yes to a province having slots of its own). The seat has none
    /// of its own: the world's row is its holder's. Another province starts with one and gains by the system's development and the
    /// rank of the house that holds it.</summary>
    public static int Slots(World w, HoldingCatalog cat, Holding planet, int index)
    {
        if (index <= 0 || index >= planet.Provinces.Count) return 0; var r = cat.Provinces;
        int level = SystemWorks.DevelopmentLevel(w, planet.SystemId), rank = w.House(HolderOf(planet, index)) is { } h ? Progression.Title.RankOf(h.Title) : 0;
        return Math.Min(r.SlotMax, r.SlotBase + r.SlotDevelopment.Count(l => l <= level) + r.SlotRank.Count(l => l <= rank));
    }
    /// <summary>Whether a building suits a province: an ordinary building of the world's kind, not one for the home world's own row or
    /// one kept off a settled world, not one a system has only one of; and one that works a deposit only in the province given over to it.</summary>
    public static bool Suits(HoldingCatalog cat, Galaxy.StarSystem sys, Province p, WorkDef d) =>
        !d.IsMegastructure && d.On != "world" && !d.NotHome && d.Feature.Length == 0 && d.Requires.Length == 0 && d.Limit == WorkDef.PerBody && d.Fits(sys.PlanetClass)
        && (d.City.Length == 0 || d.City == p.CityKind)   // a building of one kind of city goes up in a city of that kind and nowhere else (docs/CITIES.md)
        && (d.Deposit.Length == 0 || (cat.Provinces.Focus(p.Focus)?.Deposit == d.Deposit && sys.Deposit(d.Deposit) > 0));
    public static string? BuildRefusal(World w, HoldingCatalog cat, House house, Holding planet, int index, WorkDef def, bool explain = true)
    {
        if (!planet.IsPlanet || index <= 0 || index >= planet.Provinces.Count) return "no such province";
        if (HolderOf(planet, index) != house.Id) return "it is not yours to build in";
        if (planet.InRebellion) return "its world is in revolt";
        var sys = w.Galaxy?.System(planet.SystemId); var p = planet.Provinces[index]; if (sys == null) return "no such world";
        if (!Suits(cat, sys, p, def)) return "does not suit this province";
        if (p.Buildings.Contains(def.Id)) return "already built here";
        if (w.Projects.Any(x => x.Kind == Project.ProvinceWork && x.TargetId == Target(planet.Id, index) && x.DefId == def.Id)) return "already being built here";
        if (p.Buildings.Count + Rising(w, planet, index) >= Slots(w, cat, planet, index)) return "no free slot in this province: development and rank open more";
        if (def.MinTitleRank > 0 && Progression.Title.RankOf(house.Title) < def.MinTitleRank) return "needs a greater rank";
        var first = ProjectSystem.Split(def.Cost, def.BuildTurns).First;
        return house.Treasury.CanAfford(first) ? null : !explain ? "needs more" : $"{house.Treasury.Short(first)}{(def.BuildTurns > 1 ? " to begin" : "")}";
    }
    public static void Raise(World w, Holding planet, int index, WorkDef def)
    {
        if (index <= 0 || index >= planet.Provinces.Count || planet.Provinces[index].Buildings.Contains(def.Id)) return;
        planet.Provinces[index].Buildings.Add(def.Id); w.WorldCounters["province_buildings"] = w.Counter("province_buildings") + 1;
    }
    private static string Below(string title, int ranks)
    {
        string[] ladder = { Progression.Title.Knight, Progression.Title.Baron, Progression.Title.Count, Progression.Title.Duke, Progression.Title.Archduke, Progression.Title.Emperor };
        int at = Array.IndexOf(ladder, title); return at <= 0 ? title : ladder[Math.Max(0, at - Math.Max(0, ranks))];
    }

    /// <summary>Why a house may not improve a province, or null: it must hold it, and the system's development sets how far it goes.</summary>
    public static string? ImproveRefusal(World w, HoldingCatalog cat, House house, Holding planet, int index, bool explain = true)
    {
        if (!planet.IsPlanet || index < 0 || index >= planet.Provinces.Count) return "no such province";
        if (HolderOf(planet, index) != house.Id) return "it is not yours to improve";
        if (planet.InRebellion) return "its world is in revolt";
        var p = planet.Provinces[index]; int level = Math.Max(1, p.Level);
        if (level >= cat.Rules.MaxLevel) return "it can be taken no higher";
        if (level >= SystemWorks.LevelAllowed(w, cat, planet.SystemId)) return $"needs development {cat.Rules.UpgradeAt[level]} (it is {SystemWorks.DevelopmentLevel(w, planet.SystemId)})";
        var cost = cat.Provinces.ImproveCostOf(level);
        return house.Treasury.CanAfford(cost) ? null : !explain ? "needs more" : $"{house.Treasury.Short(cost)}";
    }
    public static void Improve(World w, HoldingCatalog cat, House house, Holding planet, int index)
    {
        var p = planet.Provinces[index]; house.Treasury.Spend(cat.Provinces.ImproveCostOf(Math.Max(1, p.Level))); p.Level = Math.Min(cat.Rules.MaxLevel, Math.Max(1, p.Level) + 1);
        w.WorldCounters["provinces_improved"] = w.Counter("provinces_improved") + 1;
    }

    // ---------------- for life, unless the one over you asks ----------------

    /// <summary>The leader over the house that holds a world: the head of its state if it has left the Reach, the emperor if there is
    /// one, else the house that leads its part of the galaxy. Null when that is the house itself, or nobody.</summary>
    public static House? Over(World w, Holding planet)
    {
        string owner = planet.OwnerId; if (World.Outsider(owner)) return null;
        string? id = null;
        foreach (var p in w.Polities) if (p.Members.Contains(owner) || p.HeadHouseId == owner) { id = p.HeadHouseId; break; }
        id ??= ThroneSystem.Emperor(w)?.Id;
        id ??= w.Galaxy?.System(planet.SystemId) is { } s ? PolicySystem.DominantByRegion(w).GetValueOrDefault(s.RegionId) : null;
        return string.IsNullOrEmpty(id) || id == owner ? null : w.House(id);
    }
    public static string What(Holding planet, int index) => index == 0 ? planet.Name : index < planet.Provinces.Count ? planet.Provinces[index].Name : planet.Name;
    private static string AskedKey(string houseId) => "seat_asked:" + houseId;

    /// <summary>Why a house may not ask a holder to step down, or null. A province of a world (1 or 2) is asked for by the house that
    /// holds the world, for itself or any house; the world itself (0) by the leader over that house, and only for a house that
    /// already holds a province of it, so the one who steps down has a place under the one who rises.</summary>
    public static string? DemandRefusal(World w, HoldingCatalog cat, House by, Holding planet, int index, string forId, bool explain = true)
    {
        if (!planet.IsPlanet || planet.Provinces.Count < 2 || index < 0 || index >= planet.Provinces.Count) return "no such province";
        if (planet.InRebellion) return "its world is in revolt";
        string against = HolderOf(planet, index);
        if (index == 0)
        {
            if (Over(w, planet)?.Id != by.Id) return "only the leader over its holder may ask for a world";
            if (forId == against) return "they hold it already";
            if (ProvinceOf(planet, forId) < 0) return "it can go only to a house that holds a province of this world";
        }
        else
        {
            if (planet.OwnerId != by.Id) return "only the house that holds the world may ask for its provinces";
            if (against == by.Id) return "it is yours already";
            if (forId == against) return "they hold it already";
            if (w.House(forId) == null || World.Outsider(forId)) return "no such house";
        }
        if (w.AtWar(by.Id, against)) return "you are at war with them: the war will settle it";
        if (planet.Provinces[index].SettledUntil > w.Turn) return $"the peace holds it theirs until year {planet.Provinces[index].SettledUntil}";
        if (w.SeatDemands.Any(d => d.HoldingId == planet.Id && d.Province == index)) return "it has been asked, and not yet answered";
        if (w.WorldCounters.TryGetValue(AskedKey(by.Id), out var asked) && asked == w.Turn + 1) return "one such demand a year";
        int cost = DemandCost(cat, planet, index);
        return by.Treasury.Influence >= cost ? null : !explain ? "needs more" : $"needs {cost} clout (the house has {by.Treasury.Influence})";
    }

    /// <summary>What asking costs in influence: nothing, of a house that is withholding its dues.</summary>
    public static int DemandCost(HoldingCatalog cat, Holding planet, int index) => index == 0 ? cat.Provinces.DemandInfluenceWorld : planet.Provinces[index].Withheld ? 0 : cat.Provinces.DemandInfluence;

    // ---------------- other answers to a demand (docs/PLAYTHROUGH.md, Medium, The Senate, P91) ----------------
    // A house asked to give up what it holds for life could step down or fight. It may also buy the asker off; take it to
    // the praetor's court, which finds for a holder who has given no cause; or have its bench stand behind it. Each says
    // beforehand what would come of it, and each, where it holds, settles the matter for the years a peace would.

    public const string Coin = "coin", Court = "court", Bench = "bench";
    public const int CoinPerLevel = 20, CoinWorld = 60, CoinRegardFloor = -20, CourtInfluence = 2, CourtFee = 8, CourtLegitimacy = 6, BenchRegard = 5;
    public const string BoughtOffCounter = "seat_demands_bought_off", CourtCounter = "seat_demands_in_court", BenchCounter = "seat_demands_faced_down";
    private static void Closed(World w, SeatDemand d)
    {
        w.SeatDemands.RemoveAll(x => x.Id == d.Id); if (!w.SeatDemands.Any(x => x.AgainstId == w.Protagonist?.HouseId)) w.WorldFlags.Remove(DemandFlag);
    }
    private static void Settled(World w, HoldingCatalog cat, Holding planet, SeatDemand d) { if (d.Province < planet.Provinces.Count) planet.Provinces[d.Province].SettledUntil = w.Turn + cat.Provinces.SettleYears; }
    private static int RegardOf(World w, House of, House to) { var a = Economy.HeadOf(w, of); var b = Economy.HeadOf(w, to); return a != null && b != null ? w.GetRelationship(a.Id, b.Id)?.Disposition ?? 0 : 0; }

    /// <summary>What the asker would take to let the matter lie: by the level of the province, or a flat sum for a world.</summary>
    public static int BuyOffPrice(Holding planet, SeatDemand d) => d.Province == 0 ? CoinWorld : CoinPerLevel * Math.Max(1, planet.Provinces[d.Province].Level);
    /// <summary>Whether the court would find for the holder, and why: it holds for life, and loses only by a cause of its own giving.</summary>
    public static (bool forHolder, string why) CourtFinds(World w, Holding planet, SeatDemand d)
    {
        if (d.Province > 0 && planet.Provinces[d.Province].Withheld) return (false, "you are withholding the dues the province owes: the court would find you have given cause");
        if (d.Province == 0 && planet.InRebellion) return (false, "the world is in revolt under you: the court would find you have given cause");
        return (true, "it is held for life and you have given no cause: the court would find for you");
    }
    /// <summary>Whether the asker would abide by a court that found against it: not a head who is ruthless or arrogant and has the stronger fleet.</summary>
    public static bool Abides(World w, House asker, House holder)
    {
        var head = Economy.HeadOf(w, asker); bool hard = head != null && (head.NatureTraits.Contains("Ruthless") || head.NatureTraits.Contains("Arrogant"));
        return !(hard && FleetSystem.StrengthOf(w, asker.Id) > FleetSystem.StrengthOf(w, holder.Id));
    }
    private static int BenchSeats(World w, Func<string, int> weight, string houseId) => FactionSystem.Of(w, houseId) is { } f ? f.Members.Sum(weight) : weight(houseId);

    /// <summary>Why this answer is not open, or null.</summary>
    public static string? AnswerRefusal(World w, HoldingCatalog cat, Func<string, int> weight, House mine, SeatDemand? d, string how, ActionPools? pools)
    {
        if (d == null || d.AgainstId != mine.Id) return "nothing has been asked of you";
        var planet = w.Holding(d.HoldingId); var asker = w.House(d.ById); if (planet == null || asker == null || d.Province >= planet.Provinces.Count) return "nothing has been asked of you";
        switch (how)
        {
            case Coin:
                int regard = RegardOf(w, asker, mine);
                if (regard <= CoinRegardFloor) return $"{asker.Name} wants it, not your coin: its head thinks {regard:+0;-0} of you";
                int price = BuyOffPrice(planet, d); return mine.Treasury.Credits >= price ? null : $"needs {price} credits";
            case Court:
                if (w.IsImperium) return "there is no praetor's court to take it to";
                if ((pools?.Influence ?? 0) < CourtInfluence) return $"needs {CourtInfluence} influence";
                return mine.Treasury.Credits >= CourtFee ? null : $"needs {CourtFee} credits for the court's fees";
            case Bench:
                var bench = FactionSystem.Of(w, mine.Id); if (bench == null) return "you sit on no bench";
                if (bench.Members.Contains(asker.Id)) return $"{asker.Name} sits on your own bench: it will not stand against one of its own";
                int ours = BenchSeats(w, weight, mine.Id), theirs = BenchSeats(w, weight, asker.Id);
                return ours > theirs ? null : $"your bench has {ours} seats to their {theirs}: it would not face them down";
            default: return "no such answer";
        }
    }
    /// <summary>What the answer would do, said before it is given.</summary>
    public static string AnswerSays(World w, HoldingCatalog cat, Func<string, int> weight, House mine, SeatDemand d, string how)
    {
        var planet = w.Holding(d.HoldingId)!; var asker = w.House(d.ById)!; string what = What(planet, d.Province); int years = cat.Provinces.SettleYears;
        switch (how)
        {
            case Coin: return $"{asker.Name} is paid {BuyOffPrice(planet, d)} credits and lets it lie. {what} is yours, and may not be asked for again for {years} years.";
            case Court:
                var (forHolder, why) = CourtFinds(w, planet, d); string cost = $"It costs {CourtInfluence} influence and {CourtFee} credits in fees. ";
                if (!forHolder) return cost + $"But {why}, and you would have to step down as if you had been asked and agreed.";
                return cost + Cap(why) + ". " + (Abides(w, asker, mine)
                    ? $"{asker.Name} would abide by it: {what} is yours and may not be asked for again for {years} years, and {asker.Name} loses {CourtLegitimacy} legitimacy for asking what the law would not give."
                    : $"{asker.Name} is not one to abide by a court and has the stronger fleet: it would be war. It loses {CourtLegitimacy} legitimacy for defying the court, and you fight with the law on your side.");
            default:
                var bench = FactionSystem.Of(w, mine.Id); bool lead = bench?.LeaderHouseId == mine.Id;
                return $"{bench?.Name ?? "Your bench"} stands behind you with {BenchSeats(w, weight, mine.Id)} seats to {asker.Name}'s {BenchSeats(w, weight, asker.Id)}, and {asker.Name} lets it lie: {what} is yours and may not be asked for again for {years} years. {asker.Name}'s head thinks {BenchRegard} the worse of you"
                    + (lead || bench == null ? "." : $", and you owe {w.House(bench.LeaderHouseId)?.Name} a favour for it.");
        }
    }
    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
    /// <summary>Give the answer. Returns what came of it: <see cref="Yielded"/>, <see cref="War"/>, or "kept".</summary>
    public static string Answer(World w, HoldingCatalog cat, Func<string, int> weight, House mine, SeatDemand d, string how, ActionPools? pools)
    {
        var planet = w.Holding(d.HoldingId)!; var asker = w.House(d.ById)!; string what = What(planet, d.Province);
        switch (how)
        {
            case Coin:
                int price = BuyOffPrice(planet, d); mine.Treasury.Credits -= price; asker.Treasury.Credits += price; Closed(w, d); Settled(w, cat, planet, d);
                w.WorldCounters[BoughtOffCounter] = w.Counter(BoughtOffCounter) + 1;
                w.Log($"{mine.Name} pays {asker.Name} {price} credits to let the matter of {what} lie."); return "kept";
            case Court:
                pools?.Spend(new Dictionary<string, int> { [Pool.Influence] = CourtInfluence }); mine.Treasury.Credits -= CourtFee; w.WorldCounters[CourtCounter] = w.Counter(CourtCounter) + 1;
                if (!CourtFinds(w, planet, d).forHolder) { w.Log($"The praetor's court hears the matter of {what} and finds for {asker.Name}: {mine.Name} had given cause."); return Resolve(w, cat, d, true); }
                asker.Legitimacy = Math.Clamp(asker.Legitimacy - CourtLegitimacy, 0, 100);
                if (!Abides(w, asker, mine)) { w.Log($"The praetor's court finds that {what} is {mine.Name}'s for life. {asker.Name} does not abide by it."); return Resolve(w, cat, d, false); }
                Closed(w, d); Settled(w, cat, planet, d);
                w.Log($"The praetor's court finds that {what} is {mine.Name}'s for life, and {asker.Name} lets it lie."); return "kept";
            default:
                var bench = FactionSystem.Of(w, mine.Id); Closed(w, d); Settled(w, cat, planet, d); w.WorldCounters[BenchCounter] = w.Counter(BenchCounter) + 1;
                var a = Economy.HeadOf(w, asker); var m = Economy.HeadOf(w, mine);
                if (a != null && m != null) { var r = w.Relationship(a.Id, m.Id); r.Disposition = Math.Clamp(r.Disposition - BenchRegard, -100, 100); }
                if (bench != null && bench.LeaderHouseId != mine.Id && w.House(bench.LeaderHouseId) != null) WealthSystem.OweFor(w, mine.Id, bench.LeaderHouseId);
                w.Log($"{bench?.Name ?? "Its bench"} stands behind {mine.Name} over {what}, and {asker.Name} lets it lie."); return "kept";
        }
    }

    /// <summary>Whether a rival house steps down when asked, or fights: it steps down when it has no fleet, or when the one who asks
    /// is so much the stronger that fighting is hopeless.</summary>
    public static bool WillYield(World w, HoldingCatalog cat, string byId, string againstId)
    {
        int mine = FleetSystem.StrengthOf(w, againstId), theirs = FleetSystem.StrengthOf(w, byId);
        return mine == 0 || theirs * 100 >= mine * cat.Provinces.YieldsAt;
    }

    /// <summary>The demand is made. A rival answers at once; the player's answer is waited for. Returns what came of it.</summary>
    public static string Demand(World w, HoldingCatalog cat, House by, Holding planet, int index, string forId, string why = "")
    {
        string against = HolderOf(planet, index); by.Treasury.Influence -= DemandCost(cat, planet, index);
        w.WorldCounters[AskedKey(by.Id)] = w.Turn + 1; int n = w.Counter(DemandsCounter) + 1; w.WorldCounters[DemandsCounter] = n;
        var d = new SeatDemand { Id = "demand_" + n, HoldingId = planet.Id, Province = index, ById = by.Id, AgainstId = against, ForId = forId, Turn = w.Turn, Why = why };
        if (against == w.Protagonist?.HouseId)
        {
            w.SeatDemands.Add(d); w.WorldFlags.Add(DemandFlag);
            // an account of it, where the house can read it (docs/PLAYTHROUGH.md, Medium, The Senate, P90)
            w.Tell("warn", $"{by.Name} asks you to give up {What(planet, index)}{(why.Length > 0 ? ": " + why : "")}. The answer is yours.", "galaxy:" + planet.SystemId);
            w.Log($"{by.Name} asks {w.House(against)?.Name ?? against} to give up {What(planet, index)}{(forId == by.Id ? "" : " to " + (w.House(forId)?.Name ?? forId))}. It was held for life. The answer is yours: step down, or stand.");
            return Pending;
        }
        return Resolve(w, cat, d, WillYield(w, cat, by.Id, against));
    }

    /// <summary>The answer. Stepping down: the province, or the world, changes hands, and a house that steps down from a world takes
    /// the province its successor held there and is the less for it. Standing: the two houses are at war.</summary>
    public static string Resolve(World w, HoldingCatalog cat, SeatDemand d, bool yields)
    {
        w.SeatDemands.RemoveAll(x => x.Id == d.Id); if (!w.SeatDemands.Any(x => x.AgainstId == w.Protagonist?.HouseId)) w.WorldFlags.Remove(DemandFlag);
        var planet = w.Holding(d.HoldingId); var by = w.House(d.ById); var against = w.House(d.AgainstId); var to = w.House(d.ForId);
        if (planet == null || by == null || against == null || to == null || d.Province >= planet.Provinces.Count || HolderOf(planet, d.Province) != against.Id) return Yielded;
        string what = What(planet, d.Province);
        if (!yields)
        {
            if (!w.AtWar(by.Id, against.Id))
            {
                w.Wars.Add(new War { HouseA = against.Id, HouseB = by.Id, Since = w.Turn }); w.WorldCounters[FleetSystem.WarsDeclaredCounter] = w.Counter(FleetSystem.WarsDeclaredCounter) + 1;
                if (against.Id == w.Protagonist?.HouseId) w.WorldCounters[FleetSystem.WarsBegunCounter] = w.Counter(FleetSystem.WarsBegunCounter) + 1;
                FleetSystem.RefreshWarFlags(w);
            }
            w.WorldCounters[WarsCounter] = w.Counter(WarsCounter) + 1;
            w.Log($"{against.Name} will not give up {what}. It was theirs for life, and they take up arms against {by.Name} to keep it.");
            return War;
        }
        if (d.Province == 0)
        {
            int theirs = ProvinceOf(planet, to.Id);
            planet.OwnerId = to.Id; planet.GovernorId = Economy.HeadOf(w, to)?.Id ?? "";
            if (theirs > 0) { planet.Provinces[theirs].HolderId = against.Id; planet.Provinces[theirs].Since = w.Turn; }
            against.Legitimacy += cat.Provinces.StepDownLegitimacy; string was = against.Title; against.Title = Below(against.Title, cat.Provinces.StepDownRanks);
            w.HoldingsChanged(); w.ProvincesChanged();
            w.Log($"{against.Name} steps down from {planet.Name} at the word of {by.Name}. {to.Name} holds the world now{(theirs > 0 ? ", and " + against.Name + " holds " + planet.Provinces[theirs].Name + " under it" : "")}.{(was != against.Title ? " " + against.Name + " is a rung the lower for it." : "")}");
        }
        else
        {
            var p = planet.Provinces[d.Province]; p.HolderId = to.Id == planet.OwnerId ? "" : to.Id; p.Since = w.Turn; p.Withheld = false; p.Dues = Customary; w.ProvincesChanged();
            w.Log($"{against.Name} gives up {what} at the word of {by.Name}{(to.Id == by.Id ? "" : ": it goes to " + to.Name)}.");
        }
        w.WorldCounters[YieldedCounter] = w.Counter(YieldedCounter) + 1;
        return Yielded;
    }

    public const string LevyPrefix = "levy_";
    /// <summary>The levy (docs/PROVINCES.md): in a war of the house that holds a world, every province that pays it dues sends a squadron,
    /// once a war, which sails under that house until the peace. At peace the provinces are ready to be asked again.</summary>
    private static void Levies(World w, HoldingCatalog cat, Holding planet)
    {
        var rules = cat.Provinces; var enemies = World.Outsider(planet.OwnerId) ? new List<string>() : w.EnemiesOf(planet.OwnerId).ToList(); bool war = enemies.Count > 0;
        for (int i = 1; i < planet.Provinces.Count; i++)
        {
            var p = planet.Provinces[i]; if (!war) { p.Levied = false; p.LevyLost = false; continue; }
            string id = LevyPrefix + Target(planet.Id, i); string senderId = HolderOf(planet, i); var sender = w.House(senderId); var over = w.House(planet.OwnerId);
            if (p.Levied && !w.Fleets.ContainsKey(id))
            {
                // it was sent and is gone while the war goes on: it was lost, and the house that sent it feels it
                p.Levied = false; p.LevyLost = true; int hulls = rules.LevySquadron * Math.Max(1, p.Level);
                if (sender != null) { foreach (var r in Resource.All) sender.Treasury.Set(r, Math.Max(0, sender.Treasury.Get(r) - rules.LevyLost.Get(r) * hulls)); if (over != null && Economy.HeadOf(w, sender) is { } sh && Economy.HeadOf(w, over) is { } oh) { var rel = w.Relationship(sh.Id, oh.Id); rel.Disposition = Math.Clamp(rel.Disposition + rules.LevyLostRegard, -100, 100); } }
                w.WorldCounters["levies_lost"] = w.Counter("levies_lost") + 1;
                w.Log($"The levy of {p.Name} is lost in {over?.Name ?? "another house"}'s war. {sender?.Name ?? "Its house"} counts its dead, and remembers whose war it was.");
                continue;
            }
            if (p.Levied || p.LevyLost || rules.LevySquadron <= 0 || sender == null || Dues(w, cat, planet, i).IsEmpty()) continue;
            if (w.Fleets.ContainsKey(id)) { p.Levied = true; continue; }
            if (enemies.Any(e => LeanUnder(w, senderId, e) > 0 || e == senderId)) continue;   // it sends no ships against a house it also holds a province under
            var officer = w.Characters.Values.Where(c => c.HouseId == senderId && c.Alive && c.Age >= 18 && c.Id != Economy.HeadOf(w, sender)?.Id && !w.Fleets.Values.Any(f => f.AdmiralId == c.Id))
                .OrderByDescending(c => c.Skill("tactics") + c.Skill("leadership")).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
            string of = sender.Name.StartsWith("House ", StringComparison.Ordinal) ? sender.Name[6..] : sender.Name;
            w.Fleets[id] = new Fleet { Id = id, Name = "The " + of + " Levy", OwnerId = planet.OwnerId, LevyOf = senderId, AdmiralId = officer?.Id ?? "", SystemId = planet.SystemId, HomeSystemId = planet.SystemId,
                Strength = rules.LevySquadron * Math.Max(1, p.Level), Loyalty = DuesOf(w, p) >= Heavy ? 35 : 60 };
            p.Levied = true; w.WorldCounters["levies_raised"] = w.Counter("levies_raised") + 1;
            w.Log($"{sender.Name} sends the levy of {p.Name} to {over?.Name ?? "the holder of the world"}: {Counted.Hulls(rules.LevySquadron * Math.Max(1, p.Level))}{(officer != null ? " under " + officer.Name : "")}, until the peace.");
        }
    }

    // ---------------- the year ----------------

    /// <summary>The year's housekeeping: a demand the player has left unanswered too long is taken for defiance; a province whose house
    /// is gone, or was beaten in the field by the house it defied, falls to the holder of its world; and now and then a rival asks.</summary>
    public static void Tick(World w, HoldingCatalog cat)
    {
        var rules = cat.Provinces; if (rules.Focuses.Count == 0 || w.Galaxy == null) return;
        Seed(w, cat);
        w.WorldFlags.Remove("obliged_overlord"); w.WorldFlags.Remove("defied_overlord");
        foreach (var d in w.SeatDemands.Where(d => w.Turn - d.Turn > rules.AnswerYears).ToList()) Resolve(w, cat, d, false);
        string? me = w.Protagonist?.HouseId; bool changed = false;
        foreach (var h in w.Holdings.Values.Where(h => h.IsPlanet && h.Provinces.Count > 1).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            for (int i = 1; i < h.Provinces.Count; i++)
            {
                var p = h.Provinces[i]; if (p.HolderId.Length == 0) continue;
                if (p.HolderId == h.OwnerId) { p.HolderId = ""; changed = true; continue; }
                var holder = w.House(p.HolderId);
                if (holder == null) { p.HolderId = ""; changed = true; continue; }
                if (w.AtWar(holder.Id, h.OwnerId) && FleetSystem.StrengthOf(w, holder.Id) == 0 && !World.Outsider(h.OwnerId))
                {
                    w.Log($"{holder.Name} has no fleet left to keep {p.Name}: {w.House(h.OwnerId)?.Name ?? "the holder of the world"} takes it."); p.HolderId = ""; p.Since = w.Turn; changed = true;
                }
            }
            Levies(w, cat, h);
            for (int i = 0; i < h.Provinces.Count; i++)
            {
                var p = h.Provinces[i]; if (p.EasedUntil > 0 && p.EasedUntil <= w.Turn) { p.EasedUntil = 0; changed = true; }
                string a = i == 0 ? h.OwnerId : HolderOf(h, i), b = i == 0 ? Over(w, h)?.Id ?? "" : h.OwnerId; if (b.Length == 0 || a == b) { p.FoughtOver = false; continue; }
                if (w.AtWar(a, b)) { p.FoughtOver = true; continue; }
                if (!p.FoughtOver) continue;
                // they fought and have made peace, and the one who held it holds it still: the peace settles it for a term
                p.FoughtOver = false; p.SettledUntil = w.Turn + rules.SettleYears; p.Withheld = false; if (i > 0 && p.Dues >= Heavy) p.Dues = Customary; changed = true;
                w.WorldCounters["seats_settled"] = w.Counter("seats_settled") + 1;
                w.Log($"The peace between {w.House(a)?.Name ?? a} and {w.House(b)?.Name ?? b} settles {What(h, i)}: it stays {w.House(a)?.Name ?? a}'s{(i > 0 ? ", at customary dues," : "")} and may not be asked for until year {p.SettledUntil}.");
            }
            // a rival under heavy dues that is the match of the house over it stops paying; one whose dues are eased pays again
            for (int i = 1; i < h.Provinces.Count; i++)
            {
                var p = h.Provinces[i]; string holder = HolderOf(h, i); if (holder == h.OwnerId || holder == me) continue;
                if (!p.Withheld && DuesOf(w, p) >= Heavy && !w.AtWar(holder, h.OwnerId) && FleetSystem.StrengthOf(w, holder) >= FleetSystem.StrengthOf(w, h.OwnerId) && FleetSystem.StrengthOf(w, holder) > 0) { Withhold(w, h, i, true); changed = true; }
                else if (p.Withheld && DuesOf(w, p) < Heavy) { Withhold(w, h, i, false); changed = true; }
                // a rival with coin to spare under a rival at peace commutes its dues, about one year in ten
                else if (h.OwnerId != me && Hash(h.Id + ":commute:" + i + ":" + w.Turn) % 10 == 0 && w.House(holder) is { } hh && w.House(h.OwnerId) is { } oo && CommuteRefusal(w, cat, hh, h, i, false) == null && hh.Treasury.Credits >= CommuteCost(cat, p).Credits + 40) { Commute(w, cat, h, i); changed = true; }
            }
            if (World.Outsider(h.OwnerId) || h.OwnerId == me || h.InRebellion || w.House(h.OwnerId) is not { } owner) continue;
            // a rival at war asks more of the houses under it that are weaker than it; at peace it goes back to what is customary
            for (int i = 1; i < h.Provinces.Count; i++)
            {
                var p = h.Provinces[i]; string holder = HolderOf(h, i); if (holder == owner.Id) continue; bool atWar = w.EnemiesOf(owner.Id).Any(e => e != holder);
                if (atWar && p.Dues == Customary && Hash(h.Id + ":dues:" + i + ":" + w.Turn) % 8 == 0 && FleetSystem.StrengthOf(w, owner.Id) > FleetSystem.StrengthOf(w, holder)) { SetDues(w, h, i, Heavy); changed = true; }
                else if (!atWar && p.Dues == Heavy) { SetDues(w, h, i, Customary); changed = true; }
                // and at peace it forgives the dues of a house stronger than itself, to have that house's voice: about one year in twelve
                else if (!atWar && p.Dues == Customary && !p.Withheld && p.EasedUntil <= w.Turn && Hash(h.Id + ":woo:" + i + ":" + w.Turn) % 12 == 0 && FleetSystem.StrengthOf(w, holder) > FleetSystem.StrengthOf(w, owner.Id)) { SetDues(w, h, i, Forgiven); changed = true; }
            }
            // a rival that holds a world answers a house that withholds its dues, or the player who has voted against it once too often, by asking for the province
            if (!w.EnemiesOf(owner.Id).Any())
                for (int i = 1; i < h.Provinces.Count; i++)
                {
                    string holder = HolderOf(h, i); if (holder == owner.Id) continue;
                    int defied = w.Counter(DefiedKey(owner.Id, holder)); bool provoked = h.Provinces[i].Withheld || defied >= rules.Defiances;
                    if (!provoked || FleetSystem.StrengthOf(w, owner.Id) < FleetSystem.StrengthOf(w, holder) || DemandRefusal(w, cat, owner, h, i, owner.Id, false) != null) continue;
                    string why = h.Provinces[i].Withheld ? "you are withholding the dues the province owes it" : $"you have voted against it in the Senate {Counted.Times(defied)} on what touched it, and it bears {rules.Defiances - 1}";
                    w.WorldCounters.Remove(DefiedKey(owner.Id, holder)); Demand(w, cat, owner, h, i, owner.Id, why); changed = true; break;
                }
            // a rival who holds a world asks for one of its provinces for itself, when it is at peace and at least the holder's match
            if (Hash(h.Id + ":province:" + w.Turn) % (uint)rules.NpcProvinceYears == 0 && !w.EnemiesOf(owner.Id).Any())
                for (int i = 1; i < h.Provinces.Count; i++)
                {
                    string holder = HolderOf(h, i); if (holder == owner.Id || FleetSystem.StrengthOf(w, owner.Id) < FleetSystem.StrengthOf(w, holder)) continue;
                    if (DemandRefusal(w, cat, owner, h, i, owner.Id, false) == null) { Demand(w, cat, owner, h, i, owner.Id, "you have given it no cause: it is at peace, it is at least your match in arms, and it wants the province for itself"); changed = true; break; }
                }
            // and the leader over a rival asks it to make way for the stronger of the houses under it
            if (Hash(h.Id + ":world:" + w.Turn) % (uint)rules.NpcWorldYears == 0 && Over(w, h) is { } over && over.Id != me && !w.EnemiesOf(over.Id).Any() && FleetSystem.StrengthOf(w, over.Id) >= FleetSystem.StrengthOf(w, owner.Id))
            {
                string? to = Enumerable.Range(1, h.Provinces.Count - 1).Select(i => HolderOf(h, i)).Where(x => x != owner.Id).Distinct().OrderByDescending(x => FleetSystem.StrengthOf(w, x)).ThenBy(x => x, StringComparer.Ordinal).FirstOrDefault();
                if (to != null && DemandRefusal(w, cat, over, h, 0, to, false) == null) { Demand(w, cat, over, h, 0, to, $"it stands over you, and would have the stronger house, {w.House(to)?.Name ?? to}, hold the world"); changed = true; }
            }
        }
        // a levy whose province is gone, or whose war is over, has gone home
        foreach (var f in w.Fleets.Values.Where(f => f.Id.StartsWith(LevyPrefix, StringComparison.Ordinal)).ToList())
        {
            var (hid, idx) = Parse(f.Id[LevyPrefix.Length..]); var planet = w.Holding(hid);
            if (planet == null || idx <= 0 || idx >= planet.Provinces.Count || f.OwnerId != planet.OwnerId || !w.EnemiesOf(planet.OwnerId).Any() || Dues(w, cat, planet, idx).IsEmpty()) { w.Fleets.Remove(f.Id); w.WorldCounters["levies_home"] = w.Counter("levies_home") + 1; if (planet != null && idx > 0 && idx < planet.Provinces.Count) planet.Provinces[idx].Levied = false; }
        }
        if (changed) w.ProvincesChanged();
        if (w.SeatDemands.Any(x => x.AgainstId == me)) w.WorldFlags.Add(DemandFlag); else w.WorldFlags.Remove(DemandFlag);
    }
}
