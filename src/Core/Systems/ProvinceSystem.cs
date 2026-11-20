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
        if (todo.Count == 0) return;
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
        foreach (var id in p.Buildings) if (cat.Work(id) is { } b) foreach (var r in Resource.All) d.Add(r, (b.Deposit == r ? b.Yield.Get(r) * Math.Max(1, sys?.Deposit(r) ?? 1) : b.Yield.Get(r)) - b.Upkeep.Get(r));
        return d;
    }
    /// <summary>What the house that holds a province under another owes the holder of the world this year (the owner, 2026-11-20: yes to
    /// "a share of yield, troops, a vote"): the tithe, by the province's level, and the levy too in a year the holder of the world is
    /// at war. Nothing for a province that is the world's holder's own, on a world in revolt, or held by a house at war with that holder.</summary>
    public static Resources Dues(World w, HoldingCatalog cat, Holding planet, int index)
    {
        var d = new Resources(); if (index <= 0 || index >= planet.Provinces.Count || planet.InRebellion) return d;
        string holder = HolderOf(planet, index); if (holder == planet.OwnerId || w.AtWar(holder, planet.OwnerId)) return d;
        int level = Math.Max(1, planet.Provinces[index].Level); bool war = w.EnemiesOf(planet.OwnerId).Any();
        foreach (var r in Resource.All) d.Set(r, (cat.Provinces.Tithe.Get(r) + (war ? cat.Provinces.Levy.Get(r) : 0)) * level);
        return d;
    }
    /// <summary>Whether a house holds a province of a world another house holds: it owes that house, and leans its way in the Senate.</summary>
    public static bool Under(World w, string houseId, string overId) { if (houseId == overId) return false; foreach (var (h, _) in w.ProvincesOf(houseId)) if (h.OwnerId == overId && !w.AtWar(houseId, overId)) return true; return false; }
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
    public static double Defense(HoldingCatalog cat, Holding planet) { double d = 0; foreach (var p in planet.Provinces) { d += (cat.Provinces.Focus(p.Focus)?.Defense ?? 0) * Math.Max(1, p.Level); foreach (var id in p.Buildings) d += cat.Work(id)?.Defense ?? 0; } return d; }

    // ---------------- building in a province ----------------

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
        return house.Treasury.CanAfford(first) ? null : !explain ? "needs more" : $"needs {Presentation.CourtProjection.Bundle(first)}{(def.BuildTurns > 1 ? " to begin" : "")}";
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
        return house.Treasury.CanAfford(cost) ? null : !explain ? "needs more" : $"needs {Presentation.CourtProjection.Bundle(cost)}";
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
        if (w.SeatDemands.Any(d => d.HoldingId == planet.Id && d.Province == index)) return "it has been asked, and not yet answered";
        if (w.WorldCounters.TryGetValue(AskedKey(by.Id), out var asked) && asked == w.Turn + 1) return "one such demand a year";
        int cost = index == 0 ? cat.Provinces.DemandInfluenceWorld : cat.Provinces.DemandInfluence;
        return by.Treasury.Influence >= cost ? null : !explain ? "needs more" : $"needs {cost} influence";
    }

    /// <summary>Whether a rival house steps down when asked, or fights: it steps down when it has no fleet, or when the one who asks
    /// is so much the stronger that fighting is hopeless.</summary>
    public static bool WillYield(World w, HoldingCatalog cat, string byId, string againstId)
    {
        int mine = FleetSystem.StrengthOf(w, againstId), theirs = FleetSystem.StrengthOf(w, byId);
        return mine == 0 || theirs * 100 >= mine * cat.Provinces.YieldsAt;
    }

    /// <summary>The demand is made. A rival answers at once; the player's answer is waited for. Returns what came of it.</summary>
    public static string Demand(World w, HoldingCatalog cat, House by, Holding planet, int index, string forId)
    {
        string against = HolderOf(planet, index); by.Treasury.Influence -= index == 0 ? cat.Provinces.DemandInfluenceWorld : cat.Provinces.DemandInfluence;
        w.WorldCounters[AskedKey(by.Id)] = w.Turn + 1; int n = w.Counter(DemandsCounter) + 1; w.WorldCounters[DemandsCounter] = n;
        var d = new SeatDemand { Id = "demand_" + n, HoldingId = planet.Id, Province = index, ById = by.Id, AgainstId = against, ForId = forId, Turn = w.Turn };
        if (against == w.Protagonist?.HouseId)
        {
            w.SeatDemands.Add(d); w.WorldFlags.Add(DemandFlag);
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
            var p = planet.Provinces[d.Province]; p.HolderId = to.Id == planet.OwnerId ? "" : to.Id; p.Since = w.Turn; w.ProvincesChanged();
            w.Log($"{against.Name} gives up {what} at the word of {by.Name}{(to.Id == by.Id ? "" : ": it goes to " + to.Name)}.");
        }
        w.WorldCounters[YieldedCounter] = w.Counter(YieldedCounter) + 1;
        return Yielded;
    }

    // ---------------- the year ----------------

    /// <summary>The year's housekeeping: a demand the player has left unanswered too long is taken for defiance; a province whose house
    /// is gone, or was beaten in the field by the house it defied, falls to the holder of its world; and now and then a rival asks.</summary>
    public static void Tick(World w, HoldingCatalog cat)
    {
        var rules = cat.Provinces; if (rules.Focuses.Count == 0 || w.Galaxy == null) return;
        Seed(w, cat);
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
            if (World.Outsider(h.OwnerId) || h.OwnerId == me || h.InRebellion || w.House(h.OwnerId) is not { } owner) continue;
            // a rival who holds a world asks for one of its provinces for itself, when it is at peace and at least the holder's match
            if (Hash(h.Id + ":province:" + w.Turn) % (uint)rules.NpcProvinceYears == 0 && !w.EnemiesOf(owner.Id).Any())
                for (int i = 1; i < h.Provinces.Count; i++)
                {
                    string holder = HolderOf(h, i); if (holder == owner.Id || FleetSystem.StrengthOf(w, owner.Id) < FleetSystem.StrengthOf(w, holder)) continue;
                    if (DemandRefusal(w, cat, owner, h, i, owner.Id, false) == null) { Demand(w, cat, owner, h, i, owner.Id); changed = true; break; }
                }
            // and the leader over a rival asks it to make way for the stronger of the houses under it
            if (Hash(h.Id + ":world:" + w.Turn) % (uint)rules.NpcWorldYears == 0 && Over(w, h) is { } over && over.Id != me && !w.EnemiesOf(over.Id).Any() && FleetSystem.StrengthOf(w, over.Id) >= FleetSystem.StrengthOf(w, owner.Id))
            {
                string? to = Enumerable.Range(1, h.Provinces.Count - 1).Select(i => HolderOf(h, i)).Where(x => x != owner.Id).Distinct().OrderByDescending(x => FleetSystem.StrengthOf(w, x)).ThenBy(x => x, StringComparer.Ordinal).FirstOrDefault();
                if (to != null && DemandRefusal(w, cat, over, h, 0, to, false) == null) { Demand(w, cat, over, h, 0, to); changed = true; }
            }
        }
        if (changed) w.ProvincesChanged();
        if (w.SeatDemands.Any(x => x.AgainstId == me)) w.WorldFlags.Add(DemandFlag); else w.WorldFlags.Remove(DemandFlag);
    }
}
