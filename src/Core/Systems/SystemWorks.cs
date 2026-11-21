using Praetoria.Core.Data;
using Praetoria.Core.Galaxy;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// What is built on the bodies of a solar system, and the settling of a system nobody holds (docs/SYSTEMS.md; the owner, 2026-11-20:
/// "you should be able to do things for and with every part of your solar system. So like establishing new colonies building a new
/// resource ... building out military bases building out mega structures ... unique building[s] ... but for your own specific solar
/// system"). The holder of a system's world may build on every body in it. Three kinds of thing are built (docs/BUILDINGS.md; the owner:
/// "separate out the mega structures from the ordinary buildings ... every planetary body ... should have a certain amount of building
/// slots ... things that you just build out in space ... infrastructure that you can just invest in overtime"): BUILDINGS, which go in a
/// place's slots and can be taken to a higher level; GREAT WORKS, of which a place has room for one, on its site, and a house raises
/// as many at once as its standing allows; and INFRASTRUCTURE, which is the system's own and is bought a level at a time. Slots come
/// from the system's DEVELOPMENT, the RANK of its holder, and ground cleared for coin. The system's HOME WORLD takes its own buildings
/// (<see cref="Commands.BuildCommand"/>) and the ordinary ones of its kind, in the same slots. A work goes with the system:
/// whoever holds the world has what was built round it. Rules and reckoning only; the commands are in
/// <see cref="Commands.BuildWorkCommand"/>, <see cref="Commands.InvestInfrastructureCommand"/> and <see cref="Commands.FoundColonyCommand"/>. RNG-free.
/// </summary>
public static class SystemWorks
{
    public const string BuiltFlag = "work_built", WonderFlag = "wonder_built", ColonyFlag = "colony_founded";
    public const string BuiltCounter = "works_built", WondersCounter = "wonders_built", ColoniesCounter = "colonies_founded";

    public static string Target(string systemId, int body) => $"{systemId}#{body}";
    public static (string systemId, int body) Parse(string target) { int at = target.LastIndexOf('#'); return at < 0 ? (target, -1) : (target[..at], int.TryParse(target[(at + 1)..], out var i) ? i : -1); }

    public static IEnumerable<BodyWork> In(World w, string systemId) { foreach (var x in w.BodyWorks) if (x.SystemId == systemId) yield return x; }
    public static IEnumerable<BodyWork> On(World w, string systemId, int body) { foreach (var x in w.BodyWorks) if (x.SystemId == systemId && x.Body == body) yield return x; }
    /// <summary>Works being built on a body now.</summary>
    public static IEnumerable<Project> Underway(World w, string systemId, int body) { string t = Target(systemId, body); foreach (var p in w.Projects) if (p.Kind == Project.Work && p.TargetId == t) yield return p; }

    // ---------------- development ----------------

    /// <summary>How developed a system is, 0 to 100 (docs/BUILDINGS.md), and its level, 0 to 10: the main source of building slots and
    /// what lets a building be taken to a higher level.</summary>
    public static int Development(World w, string systemId) => w.Development.TryGetValue(systemId, out var v) ? v : 0;
    public static int DevelopmentLevel(World w, string systemId) => Development(w, systemId) / 10;
    /// <summary>How far a system's development can grow as it stands: the ground it starts with, what each level of its infrastructure
    /// adds, its people, and ten more on a prosperous world.</summary>
    public static int DevelopmentCeiling(World w, HoldingCatalog cat, string systemId)
    {
        var r = cat.Rules; var planet = w.PlanetOf(systemId); if (planet == null) return 0;
        int levels = 0; if (w.Infrastructure.TryGetValue(systemId, out var lv)) foreach (var v in lv.Values) levels += v;
        return Math.Clamp(r.DevCeiling + levels * r.DevCeilingPerLevel + planet.Population / r.DevCeilingPerPeople + (planet.Prosperity >= r.DevProsperous ? 10 : 0), 0, 100);
    }
    /// <summary>The parts of the ceiling, for the screen.</summary>
    public static IEnumerable<(string what, int points)> CeilingParts(World w, HoldingCatalog cat, string systemId)
    {
        var r = cat.Rules; var planet = w.PlanetOf(systemId); if (planet == null) yield break;
        yield return ("the ground itself", r.DevCeiling);
        int levels = 0; if (w.Infrastructure.TryGetValue(systemId, out var lv)) foreach (var v in lv.Values) levels += v;
        if (levels > 0) yield return (levels + (levels == 1 ? " level" : " levels") + " of infrastructure", levels * r.DevCeilingPerLevel);
        if (planet.Population / r.DevCeilingPerPeople > 0) yield return ("its people", planet.Population / r.DevCeilingPerPeople);
        if (planet.Prosperity >= r.DevProsperous) yield return ("a prosperous world", 10);
    }
    /// <summary>A held system that has no figure yet is given one: what its people have made of it so far.</summary>
    public static void SeedDevelopment(World w, HoldingCatalog cat)
    {
        if (w.Galaxy == null) return; var r = cat.Rules;
        foreach (var h in w.Holdings.Values)
            if (h.IsPlanet && h.SystemId.Length > 0 && !w.Development.ContainsKey(h.SystemId))
                w.Development[h.SystemId] = Math.Min(DevelopmentCeiling(w, cat, h.SystemId), r.DevSeed + h.Population / Math.Max(1, r.DevSeedPerPeople));
    }
    private static void Develop(World w, HoldingCatalog cat)
    {
        var r = cat.Rules; SeedDevelopment(w, cat);
        HashSet<string>? besieged = null; if (w.Sieges.Count > 0) { besieged = new(); foreach (var s in w.Sieges.Values) besieged.Add(s.HoldingId); }
        foreach (var h in w.Holdings.Values)
        {
            if (!h.IsPlanet || h.SystemId.Length == 0) continue;
            int now = Development(w, h.SystemId), top = DevelopmentCeiling(w, cat, h.SystemId), next = now;
            if (h.InRebellion || besieged?.Contains(h.Id) == true) next = now + r.DevSiege;
            else if (now < top) next = Math.Min(top, now + (h.Prosperity >= r.DevProsperous ? 2 : 1));
            else if (now > top) next = now - 1;
            w.Development[h.SystemId] = Math.Clamp(next, 0, 100);
        }
    }

    // ---------------- slots, and the one site ----------------

    private static int Rank(World w, string systemId) => w.PlanetHolder(systemId) is { } id && w.House(id) is { } h ? Progression.Title.RankOf(h.Title) : 0;
    public static int Cleared(World w, string systemId, int body) => w.ClearedSlots.TryGetValue(Target(systemId, body), out var v) ? v : 0;
    /// <summary>Whether nobody but its holder has a holding in the system or a province of its world: the whole of it is one house's.</summary>
    public static bool Undivided(World w, string systemId)
    {
        var holder = w.PlanetHolder(systemId); if (holder == null) return false;
        if (w.PlanetOf(systemId) is { } planet && ProvinceSystem.Divided(planet)) return false;
        foreach (var h in w.Holdings.Values) if (h.SystemId == systemId && h.OwnerId != holder) return false;
        return true;
    }
    /// <summary>Where a place's slots come from (docs/BUILDINGS.md; the owner, 2026-11-20: "three things ... determine how many slots you
    /// have ... development ... rank ... money"): what its kind starts with, the system's development, the rank of the house that holds
    /// it, ground cleared for coin, and on a home world one more when the whole system is one house's. The sum is capped at what the
    /// screen has room for. The sun has none.</summary>
    public static IEnumerable<(string what, int slots)> SlotParts(World w, HoldingCatalog cat, StarSystem sys, int body)
    {
        if (body < 0 || body >= sys.Bodies.Count) yield break; var b = sys.Bodies[body]; if (b.IsStar) yield break;
        var r = cat.Rules; var planet = w.PlanetOf(sys.Id); bool home = b.IsPrimary;
        int n;
        if (home) n = cat.Spec(planet?.Specialization ?? "")?.Slots ?? (cat.BodySlots.TryGetValue(b.Class, out var pv) ? pv : 1);
        else { n = cat.BodySlots.TryGetValue(b.Class, out var v) ? v : 1; if (b.Size <= 1 && b.Class is "barren" or "frozen" or "arid" or "forge") n = Math.Min(n, cat.SmallSlots); }
        yield return (home ? "the world itself" : "the place itself", n);
        if (planet == null) yield break;
        int level = DevelopmentLevel(w, sys.Id), dev = (home ? r.DevelopmentWorld : r.DevelopmentBody).Count(l => l <= level); if (dev > 0) yield return ("development", dev);
        int rank = Rank(w, sys.Id), byRank = (home ? r.RankWorld : r.RankBody).Count(l => l <= rank); if (byRank > 0) yield return ("the rank of its holder", byRank);
        int cleared = Cleared(w, sys.Id, body); if (cleared > 0) yield return ("ground cleared", cleared);
        if (home && r.Undivided > 0 && Undivided(w, sys.Id)) yield return ("the whole system is one house's", r.Undivided);
    }
    /// <summary>The building slots of a place: the sum of <see cref="SlotParts"/>, never more than the most a place can show.</summary>
    public static int Slots(World w, HoldingCatalog cat, StarSystem sys, int body)
    {
        if (body < 0 || body >= sys.Bodies.Count || sys.Bodies[body].IsStar) return 0;
        int n = 0; foreach (var (_, s) in SlotParts(w, cat, sys, body)) n += s;
        return Math.Min(n, sys.Bodies[body].IsPrimary ? cat.Rules.MaxWorld : cat.Rules.MaxBody);
    }
    private static int Primary(StarSystem sys) => sys.Bodies.FindIndex(b => b.IsPrimary);
    /// <summary>The building slots of a holding. A system's world, its home world, has the slots of <see cref="Slots"/>, and they are
    /// shared by its own buildings and the ordinary buildings raised on it; any other holding has its specialization's.</summary>
    public static int WorldSlots(World w, HoldingCatalog cat, Holding h)
    {
        if (h.IsPlanet && w.Galaxy?.System(h.SystemId) is { } sys && Primary(sys) is >= 0 and var p) return Slots(w, cat, sys, p);
        return cat.Spec(h.Specialization)?.Slots ?? 0;
    }
    /// <summary>Slots of a holding that are spent: its buildings standing and rising, and on a home world the works in its slots too.</summary>
    public static int WorldSlotsUsed(World w, HoldingCatalog cat, Holding h)
    {
        int n = h.Buildings.Count + ProjectSystem.SlotsUnderway(w, h.Id);
        if (h.IsPlanet && w.Galaxy?.System(h.SystemId) is { } sys && Primary(sys) is >= 0 and var p) n += WorksIn(w, cat, sys.Id, p);
        return n;
    }

    private static bool Great(HoldingCatalog cat, string defId) => cat.Work(defId)?.IsMegastructure == true;
    private static int WorksIn(World w, HoldingCatalog cat, string systemId, int body) => On(w, systemId, body).Count(x => !Great(cat, x.DefId)) + Underway(w, systemId, body).Count(p => !Great(cat, p.DefId));
    /// <summary>Buildings on a place, standing and rising: what its slots are spent on. Great works are not counted; they have the site.
    /// On a home world the holding's own buildings are counted with them.</summary>
    public static int SlotsUsed(World w, HoldingCatalog cat, string systemId, int body)
    {
        int n = WorksIn(w, cat, systemId, body);
        if (w.Galaxy?.System(systemId) is { } sys && body >= 0 && body < sys.Bodies.Count && sys.Bodies[body].IsPrimary && w.PlanetOf(systemId) is { } h) n += h.Buildings.Count + ProjectSystem.SlotsUnderway(w, h.Id);
        return n;
    }
    /// <summary>Room left for buildings on a place.</summary>
    public static int FreeSlots(World w, HoldingCatalog cat, StarSystem sys, int body) => Slots(w, cat, sys, body) - SlotsUsed(w, cat, sys.Id, body);
    /// <summary>The great work on a place's site, standing or rising: its definition id, or null when the site is free.</summary>
    public static string? Site(World w, HoldingCatalog cat, string systemId, int body) =>
        On(w, systemId, body).FirstOrDefault(x => Great(cat, x.DefId))?.DefId ?? Underway(w, systemId, body).FirstOrDefault(p => Great(cat, p.DefId))?.DefId;

    /// <summary>Why a house may not pay to clear another slot on a place, or null. The Nth slot cleared costs N times the first.</summary>
    public static string? ClearRefusal(World w, HoldingCatalog cat, House house, string systemId, int body, bool explain = true)
    {
        var sys = w.Galaxy?.System(systemId); if (sys == null) return "no such system";
        var planet = w.PlanetOf(systemId); if (planet == null) return "nobody holds this system: settle it first";
        if (planet.OwnerId != house.Id) return "you do not hold this system";
        if (planet.InRebellion) return "its world is in revolt";
        if (body < 0 || body >= sys.Bodies.Count) return "no such place"; var b = sys.Bodies[body];
        if (b.IsStar) return "there is no ground to clear on a sun";
        var r = cat.Rules; int cleared = Cleared(w, systemId, body);
        if (cleared >= (b.IsPrimary ? r.ClearMaxWorld : r.ClearMaxBody)) return "there is no more ground to clear";
        if (Slots(w, cat, sys, body) >= (b.IsPrimary ? r.MaxWorld : r.MaxBody)) return "it has all the slots a place can have";
        var cost = r.ClearCostOf(cleared);
        return house.Treasury.CanAfford(cost) ? null : !explain ? "needs more" : $"{house.Treasury.Short(cost)}";
    }

    // ---------------- great works: how many at once ----------------

    public const string StandingHouse = "house", StandingRegion = "region", StandingState = "state", StandingEmperor = "emperor";
    /// <summary>What a house is, for how many great works it may raise at once (the owner, 2026-11-20: "everyone unless they are much
    /// higher up can only do one great work at a time per house. But if you are the leader of a region, you can do two, and if you
    /// are higher up like a king or president, you can do three. Finally, an emperor would be able to do five"): the emperor; a head
    /// of state (a consul of the Republic; the king, president or dictator of a state that has left it; the ruling house of a nation); the leader of
    /// a region (the house that dominates one, or a duke or archduke); or a house like any other.</summary>
    public static string Standing(World w, House house)
    {
        // (the owner, 2026-11-20: a king is the head of a monarchy "not big enough to be an empire"; in the Republic the head is a consul;
        // other republics have their own.) A head of state is the head of a state and nobody else.
        if (house.Title == Progression.Title.Emperor) return StandingEmperor;
        if (HeadOfState(w, house) != null) return StandingState;
        if (Progression.Title.RankOf(house.Title) >= 4 || PolicySystem.RegionsDominated(w, house.Id).Any()) return StandingRegion;
        return StandingHouse;
    }
    /// <summary>What a house is the head of state as, or null: "a consul", "the king of X", "the president of X", "the dictator of X",
    /// "the ruler of a nation".</summary>
    public static string? HeadOfState(World w, House house)
    {
        foreach (var p in w.Polities) if (p.HeadHouseId == house.Id) return (p.Form == "kingdom" ? "the king of " : p.Form == "dictatorship" ? "the dictator of " : "the president of ") + p.Name;
        foreach (var o in w.Offices) if (o.HouseId == house.Id && o.OfficeId.StartsWith("consul", StringComparison.Ordinal)) return "a consul";
        return State.Nation.IsHouse(house.Id) ? "the ruler of a nation" : null;
    }
    /// <summary>What a house is, in words, for how many great works it may raise.</summary>
    public static string StandingName(World w, House house) => Standing(w, house) switch { StandingEmperor => "an emperor", StandingState => HeadOfState(w, house) ?? "a head of state", StandingRegion => "the leader of a region", _ => "a house" };
    public static int Capacity(World w, HoldingCatalog cat, House house) => Standing(w, house) switch
    { StandingEmperor => cat.Rules.CapacityEmperor, StandingState => cat.Rules.CapacityHeadOfState, StandingRegion => cat.Rules.CapacityRegionLeader, _ => cat.Rules.CapacityHouse };
    public static string StandingLabel(string standing) => standing switch { StandingEmperor => "an emperor", StandingState => "a head of state", StandingRegion => "the leader of a region", _ => "a house" };
    /// <summary>The great works a house is raising now.</summary>
    public static IEnumerable<Project> Raising(World w, HoldingCatalog cat, string houseId) { foreach (var p in w.Projects) if (p.Kind == Project.Work && p.OwnerId == houseId && Great(cat, p.DefId)) yield return p; }
    public static bool IsGreat(HoldingCatalog cat, Project p) => p.Kind == Project.Work && Great(cat, p.DefId);

    // ---------------- levels: a building is taken higher ----------------

    /// <summary>The highest level a building may have in a system as it stands: one for each threshold its development has reached.</summary>
    public static int LevelAllowed(World w, HoldingCatalog cat, string systemId) { int lv = DevelopmentLevel(w, systemId); return Math.Max(1, cat.Rules.UpgradeAt.Count(t => t <= lv)); }
    public static Project? Upgrading(World w, string target, string defId) => w.Projects.FirstOrDefault(p => p.Kind == Project.Upgrade && p.TargetId == target && p.DefId == defId);
    /// <summary>What the next level of a building costs: its cost, times the level it leaves.</summary>
    public static Resources UpgradeCost(Resources cost, int level) { var c = new Resources(); foreach (var r in Resource.All) c.Set(r, cost.Get(r) * Math.Max(1, level)); return c; }
    /// <summary>Why a house may not take a building a level higher, or null. <paramref name="target"/> is a holding's id for one of its
    /// own buildings, or "system#place" for a building raised on a place.</summary>
    public static string? UpgradeRefusal(World w, HoldingCatalog cat, House house, string target, string defId, bool explain = true)
    {
        string systemId; int level; Resources cost; int years;
        if (target.Contains('@'))
        {
            var (hid, idx) = ProvinceSystem.Parse(target); var planet = w.Holding(hid); var def = cat.Work(defId);
            if (planet == null || def == null || idx <= 0 || idx >= planet.Provinces.Count || !planet.Provinces[idx].Buildings.Contains(defId)) return "no such building";
            if (ProvinceSystem.HolderOf(planet, idx) != house.Id) return "it is not your province";
            systemId = planet.SystemId; level = planet.Provinces[idx].LevelOf(defId); cost = def.Cost; years = def.BuildTurns;
        }
        else if (target.Contains('#'))
        {
            var (sysId, body) = Parse(target); systemId = sysId; var x = On(w, sysId, body).FirstOrDefault(o => o.DefId == defId); var def = cat.Work(defId);
            if (x == null || def == null) return "no such building"; if (def.IsMegastructure) return "a great work has no levels";
            if (w.PlanetHolder(sysId) != house.Id) return "you do not hold this system";
            level = Math.Max(1, x.Level); cost = def.Cost; years = def.BuildTurns;
        }
        else
        {
            var h = w.Holding(target); var def = cat.Building(defId); if (h == null || def == null || !h.Buildings.Contains(defId)) return "no such building";
            if (h.OwnerId != house.Id) return "not your holding"; if (def.Megastructure) return "a great work has no levels";
            systemId = h.SystemId; level = h.LevelOf(defId); cost = def.Cost; years = def.BuildTurns;
        }
        if (w.PlanetOf(systemId)?.InRebellion == true) return "its world is in revolt";
        if (level >= cat.Rules.MaxLevel) return "it can be taken no higher";
        if (Upgrading(w, target, defId) != null) return "the next level is being built";
        if (level >= LevelAllowed(w, cat, systemId)) return $"needs development {cat.Rules.UpgradeAt[level]} (it is {DevelopmentLevel(w, systemId)})";
        var first = ProjectSystem.Split(UpgradeCost(cost, level), Math.Max(1, years)).First;
        return house.Treasury.CanAfford(first) ? null : !explain ? "needs more" : $"{house.Treasury.Short(first)}{(years > 1 ? " to begin" : "")}";
    }
    // ---------------- one list of what a home world can take (docs/PLAYTHROUGH.md, Medium, Building and holding, B2 and B42) ----------------

    /// <summary>One thing that could be built on a home world. <see cref="Group"/> is "home" (only on a home world), "own" (the
    /// world's own, by what it is given over to) or "ordinary". <see cref="Work"/> or <see cref="Building"/> is set, never both.</summary>
    public sealed record Buildable(string Group, WorkDef? Work, BuildingDef? Building, string? Refusal)
    {
        public string Id => Work?.Id ?? Building!.Id;
        public string Name => Work?.Name ?? Building!.Name;
        public Resources Cost => Work?.Cost ?? Building!.Cost;
        public int BuildTurns => Work?.BuildTurns ?? Building!.BuildTurns;
    }
    /// <summary>Everything that could be built in the slots of a home world and does not stand or rise there yet, with why not where
    /// it cannot be begun now: the buildings only a home world takes, the world's own, and the ordinary ones, each cheapest first.
    /// The system's panel and the House window both read this, so there is one list whichever window it is opened from.</summary>
    public static List<Buildable> BuildableOn(World w, HoldingCatalog cat, Progression.TitleCatalog? titles, House house, Holding planet, Commands.CommandContext ctx)
    {
        var list = new List<Buildable>(); var sys = w.Galaxy?.System(planet.SystemId); if (sys == null || !planet.IsPlanet) return list;
        int body = sys.Bodies.FindIndex(b => b.IsPrimary); if (body < 0) return list; var b = sys.Bodies[body];
        var head = Economy.HeadOf(w, house); string actor = head?.Id ?? ""; bool mine = planet.OwnerId == house.Id && head != null;
        var standing = On(w, sys.Id, body).Select(x => x.DefId).ToHashSet(); var rising = Underway(w, sys.Id, body).Select(p => p.DefId).ToHashSet();
        var ownRising = w.Projects.Where(p => p.Kind == Project.Building && p.TargetId == planet.Id).Select(p => p.DefId).ToHashSet();
        string? No(WorkDef d) => mine ? new Commands.BuildWorkCommand(actor, sys.Id, body, d.Id, cat, titles).Refusal(ctx) : "not your world";
        foreach (var d in cat.Works.Where(d => !d.IsMegastructure && d.On == "world" && Suits(sys, b, d, planet) && !standing.Contains(d.Id) && !rising.Contains(d.Id)).OrderBy(d => d.Cost.Credits + d.Cost.Materials))
            list.Add(new Buildable("home", d, null, No(d)));
        foreach (var x in cat.Buildings.Where(x => x.FitsSpecialization(planet.Specialization) && !planet.Buildings.Contains(x.Id) && !ownRising.Contains(x.Id) && (x.RequiresDeposit == null || sys.Deposit(x.RequiresDeposit) > 0)).OrderBy(x => x.Cost.Credits + x.Cost.Materials))
            list.Add(new Buildable("own", null, x, mine ? new Commands.BuildCommand(actor, planet.Id, x.Id, cat, titles).Refusal(ctx) : "not your world"));
        foreach (var d in cat.Works.Where(d => !d.IsMegastructure && d.On != "world" && Suits(sys, b, d) && !standing.Contains(d.Id) && !rising.Contains(d.Id)).OrderByDescending(d => d.Deposit.Length > 0).ThenBy(d => d.Cost.Credits + d.Cost.Materials))
            list.Add(new Buildable("ordinary", d, null, No(d)));
        return list;
    }

    // ---------------- pulling a building down (docs/PLAYTHROUGH.md, Medium, Building and holding, B65) ----------------

    /// <summary>What the breakers give back: this share of the materials the building's first level cost, for each level it stands at.</summary>
    public const int ScrapPct = 25;
    public const string PulledDownCounter = "buildings_pulled_down";
    /// <summary>The building a target names, wherever it stands: its name, what it cost, its level, where it is, and whether it is a great work.</summary>
    private static (string name, Resources cost, int level, string where, bool great, int legitimacy, string systemId)? Stands(World w, HoldingCatalog cat, House house, string target, string defId, out string? refusal)
    {
        refusal = null;
        if (target.Contains('@'))
        {
            var (hid, idx) = ProvinceSystem.Parse(target); var planet = w.Holding(hid); var def = cat.Work(defId);
            if (planet == null || def == null || idx <= 0 || idx >= planet.Provinces.Count || !planet.Provinces[idx].Buildings.Contains(defId)) { refusal = "no such building"; return null; }
            if (ProvinceSystem.HolderOf(planet, idx) != house.Id) { refusal = "it is not your province"; return null; }
            return (def.Name, def.Cost, planet.Provinces[idx].LevelOf(defId), "in " + planet.Provinces[idx].Name, def.IsMegastructure, def.Legitimacy, planet.SystemId);
        }
        if (target.Contains('#'))
        {
            var (sysId, body) = Parse(target); var x = On(w, sysId, body).FirstOrDefault(o => o.DefId == defId); var def = cat.Work(defId); var sys = w.Galaxy?.System(sysId);
            if (x == null || def == null || sys == null || body < 0 || body >= sys.Bodies.Count) { refusal = "no such building"; return null; }
            if (w.PlanetHolder(sysId) != house.Id) { refusal = "you do not hold this system"; return null; }
            return (def.Name, def.Cost, Math.Max(1, x.Level), Where(sys.Bodies[body]), def.IsMegastructure, def.Legitimacy, sysId);
        }
        var h = w.Holding(target); var bd = cat.Building(defId);
        if (h == null || bd == null || !h.Buildings.Contains(defId)) { refusal = "no such building"; return null; }
        if (h.OwnerId != house.Id) { refusal = "not your holding"; return null; }
        return (bd.Name, bd.Cost, h.LevelOf(defId), "at " + h.Name, bd.Megastructure, 0, h.SystemId);
    }
    /// <summary>The materials the breakers give back for it.</summary>
    public static int Scrap(World w, HoldingCatalog cat, House house, string target, string defId) =>
        Stands(w, cat, house, target, defId, out _) is { } s ? s.cost.Materials * s.level * ScrapPct / 100 : 0;
    /// <summary>Why a building may not be pulled down, or null. Whoever may build in a place may pull down what stands there; a great
    /// work is not pulled down, and nothing is while its next level is rising.</summary>
    public static string? PullDownRefusal(World w, HoldingCatalog cat, House house, string target, string defId)
    {
        if (Stands(w, cat, house, target, defId, out var no) is not { } s) return no;
        if (s.great) return "a great work is not pulled down";
        if (w.PlanetOf(s.systemId)?.InRebellion == true) return "its world is in revolt";
        return Upgrading(w, target, defId) != null ? "its next level is being built: let that finish, or abandon it" : null;
    }
    /// <summary>What pulling it down would do, in a line, for the confirm.</summary>
    public static string PullDownSays(World w, HoldingCatalog cat, House house, string target, string defId)
    {
        if (Stands(w, cat, house, target, defId, out _) is not { } s) return "";
        int scrap = s.cost.Materials * s.level * ScrapPct / 100;
        return $"{s.name}{(s.level > 1 ? " " + Roman(s.level) : "")} {s.where} is pulled down and its slot is free. "
            + (scrap > 0 ? $"The breakers give back {scrap} materials ({ScrapPct}% of what its {(s.level > 1 ? s.level + " levels" : "first level")} cost in them). " : "Nothing of it is worth carrying away. ")
            + "What it yielded and what it cost to keep both stop."
            + (s.legitimacy > 0 ? $" The house loses the {s.legitimacy} legitimacy it had for raising it." : "") + " It cannot be undone.";
    }
    public static void PullDown(World w, HoldingCatalog cat, House house, string target, string defId)
    {
        if (Stands(w, cat, house, target, defId, out _) is not { } s) return;
        int scrap = s.cost.Materials * s.level * ScrapPct / 100;
        if (target.Contains('@')) { var (hid, idx) = ProvinceSystem.Parse(target); var p = w.Holding(hid)!.Provinces[idx]; p.Buildings.Remove(defId); p.BuildingLevels.Remove(defId); w.WorldCounters["province_buildings"] = Math.Max(0, w.Counter("province_buildings") - 1); }
        else if (target.Contains('#')) { var (sysId, body) = Parse(target); w.BodyWorks.RemoveAll(o => o.SystemId == sysId && o.Body == body && o.DefId == defId); }
        else { var h = w.Holding(target)!; h.Buildings.Remove(defId); h.Upgrades.Remove(defId); }
        house.Treasury.Materials += scrap;
        if (s.legitimacy > 0) house.Legitimacy = Math.Clamp(house.Legitimacy - s.legitimacy, 0, 100);
        w.WorldCounters[PulledDownCounter] = w.Counter(PulledDownCounter) + 1;
        w.Log($"{house.Name} pulls down {s.name} {s.where}{(scrap > 0 ? $": {scrap} materials come back from the breakers" : "")}.");
    }

    /// <summary>The level is built: the building stands one higher.</summary>
    public static void Upgrade(World w, HoldingCatalog cat, string target, string defId)
    {
        if (target.Contains('@')) { var (hid, idx) = ProvinceSystem.Parse(target); if (w.Holding(hid) is { } planet && idx > 0 && idx < planet.Provinces.Count && planet.Provinces[idx].Buildings.Contains(defId)) planet.Provinces[idx].BuildingLevels[defId] = Math.Min(cat.Rules.MaxLevel, planet.Provinces[idx].LevelOf(defId) + 1); }
        else if (target.Contains('#')) { var (sysId, body) = Parse(target); if (On(w, sysId, body).FirstOrDefault(o => o.DefId == defId) is { } x) x.Level = Math.Min(cat.Rules.MaxLevel, Math.Max(1, x.Level) + 1); }
        else if (w.Holding(target) is { } h && h.Buildings.Contains(defId)) h.Upgrades[defId] = Math.Min(cat.Rules.MaxLevel, h.LevelOf(defId) + 1);
        w.WorldCounters["buildings_upgraded"] = w.Counter("buildings_upgraded") + 1;
    }

    /// <summary>Whether a work suits a place at all, whoever asks: its kind, its feature and its deposit. A work marked for the home
    /// world is for it alone; an ordinary building goes on the home world too, in its slots, unless it is not for one (an outpost, a colony).</summary>
    /// <summary>A building of one kind of city is not for a body's slots. The one exception is a building of the world's own capital
    /// (the Senate's precinct in the capital of the Reach), which stands on the home world when its capital is that city (docs/CITIES.md).</summary>
    public static bool CitySuits(WorkDef def, Holding? planet) =>
        def.City.Length == 0 || (def.On == "world" && planet is { Provinces.Count: > 0 } && planet.Provinces[0].CityKind == def.City);
    public static bool Suits(StarSystem sys, Body b, WorkDef def, Holding? planet = null) =>
        CitySuits(def, planet) && def.Fits(b.Class) && (def.On == "world" ? b.IsPrimary : !(b.IsPrimary && def.NotHome))
        && (def.Feature.Length == 0 || b.Feature == def.Feature) && (def.Deposit.Length == 0 || (b.Deposit == def.Deposit && sys.Deposit(def.Deposit) > 0));

    /// <summary>Why a house may not build this work on this body, or null. <paramref name="explain"/> false skips writing out what is lacking.</summary>
    public static string? Refusal(World w, HoldingCatalog cat, Progression.TitleCatalog? titles, House house, string systemId, int body, WorkDef def, bool explain = true)
    {
        var sys = w.Galaxy?.System(systemId); if (sys == null) return "no such system";
        var planet = w.PlanetOf(systemId);
        if (planet == null) return "nobody holds this system: settle it first";
        if (planet.OwnerId != house.Id) return "you do not hold this system";
        if (planet.InRebellion) return "its world is in revolt";
        if (body < 0 || body >= sys.Bodies.Count) return "no such body";
        var b = sys.Bodies[body];
        if (!def.Fits(b.Class)) return $"does not suit {(b.IsStar ? "a sun" : b.IsSpace ? "open space" : Article(b.Class))}";
        if (!CitySuits(def, planet)) return $"only in {cat.Cities.Kind(def.City)?.Name ?? "a city of its own kind"}";
        if (def.On == "world" && !b.IsPrimary) return "only on the system's home world";
        if (def.NotHome && b.IsPrimary) return "not for a home world";
        if (def.Feature.Length > 0 && b.Feature != def.Feature) return $"needs {cat.Feature(def.Feature)?.Name.ToLowerInvariant() ?? def.Feature}";
        if (def.Deposit.Length > 0 && (b.Deposit != def.Deposit || sys.Deposit(def.Deposit) == 0)) return $"needs {Resource.A(def.Deposit)} deposit here";
        if (def.Chokepoint && !w.Galaxy!.IsChokepoint(systemId)) return "only at a key of the lanes";
        if (On(w, systemId, body).Any(x => x.DefId == def.Id)) return "already built here";
        if (w.Projects.Any(p => p.Kind == Project.Work && p.DefId == def.Id && p.TargetId == Target(systemId, body))) return "already being built here";
        if (def.Requires.Length > 0 && !On(w, systemId, body).Any(x => x.DefId == def.Requires)) return $"needs {Article2(cat.Work(def.Requires)?.Name ?? def.Requires)} here first";
        switch (def.Limit)
        {
            case WorkDef.PerSystem:
                if (In(w, systemId).Any(x => x.DefId == def.Id) || w.Projects.Any(p => p.Kind == Project.Work && p.DefId == def.Id && Parse(p.TargetId).systemId == systemId)) return "one to a system";
                break;
            case WorkDef.PerHouse:
                if (w.BodyWorks.Any(x => x.DefId == def.Id && w.PlanetHolder(x.SystemId) == house.Id) || w.Projects.Any(p => p.Kind == Project.Work && p.DefId == def.Id && p.OwnerId == house.Id)) return "a house has one";
                break;
            case WorkDef.PerGalaxy:
                if (w.BodyWorks.Any(x => x.DefId == def.Id)) return "it has been built: there is one";
                if (w.Projects.Any(p => p.Kind == Project.Work && p.DefId == def.Id)) return "another house is building it";
                break;
        }
        if (def.IsMegastructure)
        {
            if (Site(w, cat, systemId, body) is { } there) return $"its site is taken: {cat.Work(there)?.Name ?? there}";
            int rising = Raising(w, cat, house.Id).Count(), may = Capacity(w, cat, house);
            if (rising >= may) return may == 1 ? $"the house is already raising {Raising(w, cat, house.Id).First().Name}: one great work at a time" : $"the house is raising {rising} great works: {StandingName(w, house)} may raise {may} at a time";
        }
        else if (FreeSlots(w, cat, sys, body) <= 0) return Slots(w, cat, sys, body) == 0 ? "nothing but a great work can be built here" : "no free slot here: development, rank and cleared ground open more";
        if (def.MinTitleRank > 0 && (titles?.RankOf(house.Title) ?? Progression.Title.RankOf(house.Title)) < def.MinTitleRank) return "needs a greater rank";
        var first = ProjectSystem.Split(def.Cost, def.BuildTurns).First;
        return house.Treasury.CanAfford(first) ? null : !explain ? "needs more" : $"{house.Treasury.Short(first)}{(def.BuildTurns > 1 ? " to begin" : "")}";
    }
    public static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", _ => n.ToString() };
    private static string Article(string cls) => cls switch { "gas" => "a gas giant", "belt" => "a belt", "moon" => "a moon", "comet" => "a comet", "ocean" => "an ocean world", "arid" => "an arid world", _ => $"a {cls} world" };
    private static string Article2(string name) => "aeiouAEIOU".Contains(name[0]) ? "an " + name : "a " + name;

    /// <summary>The work stands: it is on the body, and the house has what finishing it gives.</summary>
    public static BodyWork Raise(World w, House house, string systemId, int body, WorkDef def)
    {
        var sys = w.Galaxy!.System(systemId)!; var b = sys.Bodies[body];
        var work = new BodyWork { Id = $"work_{systemId}_{body}_{def.Id}", SystemId = systemId, Body = body, DefId = def.Id, Turn = w.Turn, BuiltBy = house.Id };
        w.BodyWorks.Add(work);
        if (def.Legitimacy != 0) house.Legitimacy = Math.Clamp(house.Legitimacy + def.Legitimacy, 0, 100);
        if (def.Glory != 0) house.Glory += def.Glory;
        w.WorldCounters[BuiltCounter] = w.Counter(BuiltCounter) + 1; bool wonder = def.IsMegastructure;
        if (wonder) { w.WorldCounters[WondersCounter] = w.Counter(WondersCounter) + 1; w.WorldCounters["megastructures"] = w.Counter("megastructures") + 1; }   // (the old counter and flag: an event and the epilogue read them)
        if (house.Id == w.Protagonist?.HouseId) { w.WorldFlags.Add(BuiltFlag); if (wonder) { w.WorldFlags.Add(WonderFlag); w.WorldFlags.Add("megastructure_raised"); } }
        if (wonder) w.Log($"{def.Name} stands {Where(b)} in the {sys.Name.Replace("The ", "")} system: the work of {house.Name}.{(def.Limit == WorkDef.PerGalaxy ? " There will not be another." : "")}");
        return work;
    }
    private static string Where(Body b) => b.IsStar ? "round the sun" : b.IsSpace ? "in open space" : b.Class == Body.Belt ? $"in {b.Name}" : b.Class == "gas" ? $"over {b.Name}" : $"on {b.Name}";

    // ---------------- what the works of a system do ----------------

    /// <summary>What the works of a system bring in a year, less what they cost to keep: one line a work. A work on a deposit brings up
    /// richness-many of that good.</summary>
    public static IEnumerable<(string name, Resources net)> Lines(World w, HoldingCatalog cat, string systemId)
    {
        var sys = w.Galaxy?.System(systemId); if (sys == null) yield break;
        foreach (var x in In(w, systemId).OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            var def = cat.Work(x.DefId); if (def == null) continue;
            var d = new Resources();
            int lvl = Math.Max(1, x.Level);
            foreach (var r in Resource.All) d.Add(r, ((def.Deposit == r ? def.Yield.Get(r) * Math.Max(1, sys.Deposit(r)) : def.Yield.Get(r)) - def.Upkeep.Get(r)) * lvl);
            yield return ($"{def.Name}{(lvl > 1 ? " " + Roman(lvl) : "")} · {(x.Body >= 0 && x.Body < sys.Bodies.Count && !sys.Bodies[x.Body].IsSpace ? sys.Bodies[x.Body].Name : sys.Name)}", d);
        }
        foreach (var (t, lv) in Levels(w, cat, systemId))
        {
            if (t.Yield.IsEmpty()) continue; var d = new Resources(); foreach (var r in Resource.All) d.Add(r, t.Yield.Get(r) * lv);
            yield return ($"{t.Name} {Roman(lv)} · {sys.Name}", d);
        }
    }
    /// <summary>Whether anything is built or laid anywhere: asked first, so a bare map costs nothing to reckon.</summary>
    public static bool Any(World w) => w.BodyWorks.Count > 0 || w.Infrastructure.Count > 0 || w.Counter("province_buildings") > 0;
    private static IEnumerable<(InfrastructureDef def, int level)> Levels(World w, HoldingCatalog cat, string systemId)
    {
        if (!w.Infrastructure.TryGetValue(systemId, out var levels)) yield break;
        foreach (var t in cat.Infrastructure) if (levels.TryGetValue(t.Id, out var lv) && lv > 0) yield return (t, lv);
    }
    public static double Defense(World w, HoldingCatalog cat, string systemId) { double d = 0; foreach (var x in In(w, systemId)) d += (cat.Work(x.DefId)?.Defense ?? 0) * Math.Max(1, x.Level); foreach (var (t, lv) in Levels(w, cat, systemId)) d += t.Defense * lv; return d; }
    public static bool Depot(World w, HoldingCatalog cat, string systemId) { foreach (var x in In(w, systemId)) if (cat.Work(x.DefId)?.Depot == true) return true; foreach (var (t, lv) in Levels(w, cat, systemId)) if (t.DepotAt > 0 && lv >= t.DepotAt) return true; return false; }
    public static int Prosperity(World w, HoldingCatalog cat, string systemId) { int v = w.PlanetOf(systemId) is { } pp ? ProvinceSystem.OnWorld(cat, pp).prosperity : 0; foreach (var x in In(w, systemId)) v += cat.Work(x.DefId)?.Prosperity ?? 0; foreach (var (t, lv) in Levels(w, cat, systemId)) v += t.Prosperity * lv; return v; }
    /// <summary>How many buildings in a system stand where there is no slot for them (its development fell, a lesser house took it):
    /// each is a point of unrest a year on its world until there is room again.</summary>
    public static int Crowding(World w, HoldingCatalog cat, string systemId)
    {
        var sys = w.Galaxy?.System(systemId); if (sys == null || w.PlanetOf(systemId) == null) return 0; int n = 0;
        for (int i = 0; i < sys.Bodies.Count; i++) { if (sys.Bodies[i].IsStar) continue; int used = SlotsUsed(w, cat, systemId, i); if (used > 0) n += Math.Max(0, used - Slots(w, cat, sys, i)); }
        return n;
    }
    public static int Unrest(World w, HoldingCatalog cat, string systemId) { int v = Crowding(w, cat, systemId) + (w.PlanetOf(systemId) is { } pp ? ProvinceSystem.OnWorld(cat, pp).unrest : 0); foreach (var x in In(w, systemId)) v += cat.Work(x.DefId)?.Unrest ?? 0; foreach (var (t, lv) in Levels(w, cat, systemId)) v -= t.CalmAt.Count(l => l <= lv); return v; }
    public static int Population(World w, HoldingCatalog cat, string systemId) { int v = w.PlanetOf(systemId) is { } pp ? ProvinceSystem.OnWorld(cat, pp).population : 0; foreach (var x in In(w, systemId)) v += (cat.Work(x.DefId)?.Population ?? 0) * Math.Max(1, x.Level); foreach (var (t, lv) in Levels(w, cat, systemId)) v += t.Population * lv; return v; }

    // ---------------- infrastructure: the system's own, a level at a time ----------------

    public static int Level(World w, string systemId, string trackId) => w.Infrastructure.TryGetValue(systemId, out var l) && l.TryGetValue(trackId, out var v) ? v : 0;
    public static Project? Laying(World w, string systemId, string trackId) => w.Projects.FirstOrDefault(p => p.Kind == Project.Infrastructure && p.TargetId == systemId && p.DefId == trackId);
    /// <summary>Why a house may not lay the next level of a track in a system, or null.</summary>
    public static string? InfrastructureRefusal(World w, HoldingCatalog cat, House house, string systemId, InfrastructureDef t, bool explain = true)
    {
        var planet = w.PlanetOf(systemId); if (w.Galaxy?.System(systemId) == null) return "no such system";
        if (planet == null) return "nobody holds this system: settle it first";
        if (planet.OwnerId != house.Id) return "you do not hold this system";
        if (planet.InRebellion) return "its world is in revolt";
        int level = Level(w, systemId, t.Id); if (level >= t.MaxLevel) return "it can be carried no further";
        if (Laying(w, systemId, t.Id) != null) return "the next level is being laid";
        var first = ProjectSystem.Split(t.CostOf(level + 1), t.YearsOf(level + 1)).First;
        return house.Treasury.CanAfford(first) ? null : !explain ? "needs more" : $"{house.Treasury.Short(first)}{(t.YearsOf(level + 1) > 1 ? " to begin" : "")}";
    }
    /// <summary>A level is laid: the track stands one higher, and the system is the more developed for it at once.</summary>
    public static void Lay(World w, HoldingCatalog cat, string systemId, InfrastructureDef t)
    {
        w.Development[systemId] = Math.Clamp(Development(w, systemId) + cat.Rules.DevPerLevel, 0, 100);
        if (!w.Infrastructure.TryGetValue(systemId, out var levels)) w.Infrastructure[systemId] = levels = new Dictionary<string, int>();
        levels[t.Id] = Math.Min(t.MaxLevel, levels.GetValueOrDefault(t.Id) + 1);
        w.WorldCounters["infrastructure_laid"] = w.Counter("infrastructure_laid") + 1;
    }

    /// <summary>A world built before great works had sites of their own may have one in a building slot (the Orbital Ring, the Dyson
    /// Lattice, the Lane Gate Anchor): it is moved to where it belongs, on the world, round the sun, or in open space.</summary>
    public static void Migrate(World w, HoldingCatalog cat)
    {
        if (w.Galaxy == null) return;
        foreach (var h in w.Holdings.Values.Where(h => h.Buildings.Any(b => cat.Building(b) == null && cat.Work(b) is { IsMegastructure: true })).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            var sys = w.Galaxy.System(h.SystemId);
            foreach (var id in h.Buildings.Where(b => cat.Building(b) == null && cat.Work(b) is { IsMegastructure: true }).ToList())
            {
                h.Buildings.Remove(id); var def = cat.Work(id)!; if (sys == null) continue;
                int body = sys.Bodies.FindIndex(b => def.Fits(b.Class) && (def.On != "world" || b.IsPrimary) && Site(w, cat, sys.Id, sys.Bodies.IndexOf(b)) == null);
                if (body >= 0) w.BodyWorks.Add(new BodyWork { Id = $"work_{sys.Id}_{body}_{def.Id}", SystemId = sys.Id, Body = body, DefId = def.Id, Turn = w.Turn, BuiltBy = h.OwnerId });
            }
        }
    }

    // ---------------- settling a system nobody holds ----------------

    /// <summary>Why a house may not settle a system, or null. It must be nobody's, and within reach: a lane from a world the house holds,
    /// or with a fleet of the house standing in it.</summary>
    public static string? ColonyRefusal(World w, HoldingCatalog cat, House house, string systemId, bool explain = true)
    {
        var g = w.Galaxy; var sys = g?.System(systemId); if (g == null || sys == null) return "no such system";
        if (w.PlanetOf(systemId) != null) return "it is held already";
        if (g.RingOf(systemId) == Ring.Beyond) return "it lies in a nation's space: a world there is taken in war, not settled";
        if (w.Projects.Any(p => p.Kind == Project.Colony && p.TargetId == systemId)) return w.Projects.Any(p => p.Kind == Project.Colony && p.TargetId == systemId && p.OwnerId == house.Id) ? "your colony is on its way" : "another house is settling it";
        if (World.Outsider(house.Id)) return "not for you";
        bool near = g.Neighbors(systemId).Any(n => w.PlanetHolder(n) == house.Id) || w.Fleets.Values.Any(f => f.OwnerId == house.Id && f.SystemId == systemId && !f.Moving && !f.IsAway);
        if (!near) return "too far: it must lie a lane from a world of yours, or have a fleet of yours standing in it";
        if (w.Fleets.Values.Any(f => f.SystemId == systemId && !f.Moving && !f.IsAway && f.OwnerId != house.Id && w.AtWar(f.OwnerId, house.Id))) return "an enemy fleet holds the sky";
        var first = ProjectSystem.Split(cat.Colony.Cost, cat.Colony.BuildTurns).First;
        return house.Treasury.CanAfford(first) ? null : !explain ? "needs more" : $"{house.Treasury.Short(first)} to begin";
    }

    /// <summary>A system nobody holds that a house might settle (docs/PLAYTHROUGH.md, Medium, Building and holding, B32): how far it
    /// lies from the house's nearest world, whether a colony can be sent now and if not why, and the house's nearest fleet, which
    /// standing in it would put it within reach.</summary>
    public sealed record ColonySite(string SystemId, string Name, string Class, string Deposits, int Lanes, string FromWorld, bool InReach, string? Refusal, string? FleetId, string? FleetName, int FleetLanes);
    /// <summary>How many lanes from a world of the house the list looks.</summary>
    public const int ColonySearch = 4;
    /// <summary>The empty systems within <see cref="ColonySearch"/> lanes of a world of the house, or with a fleet of its own standing
    /// in them: those a colony can be sent to now first, then the nearest.</summary>
    public static List<ColonySite> ColonySites(World w, HoldingCatalog cat, House house)
    {
        var list = new List<ColonySite>(); var g = w.Galaxy; if (g == null) return list;
        var mine = w.HoldingsOf(house.Id).Where(h => h.IsPlanet && g.System(h.SystemId) != null).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        var fleets = w.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.IsAway && f.LevyOf.Length == 0 && f.HiredFrom.Length == 0 && g.System(f.SystemId) != null).OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
        foreach (var sys in g.Systems.Values.OrderBy(s => s.Id, StringComparer.Ordinal))
        {
            if (w.PlanetOf(sys.Id) != null || g.RingOf(sys.Id) == Ring.Beyond) continue;
            int lanes = int.MaxValue; string from = "";
            foreach (var h in mine) { int d = g.Hops(h.SystemId, sys.Id); if (d >= 0 && d < lanes) { lanes = d; from = h.Name; } }
            bool standing = fleets.Any(f => f.SystemId == sys.Id && !f.Moving);
            if (lanes > ColonySearch && !standing) continue;
            Fleet? near = null; int fl = int.MaxValue;
            foreach (var f in fleets) { int d = g.Hops(f.SystemId, sys.Id); if (d >= 0 && d < fl) { fl = d; near = f; } }
            string deposits = string.Join(", ", sys.Deposits.Where(kv => kv.Value > 0).OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key.Replace('_', ' ')} {kv.Value}"));
            list.Add(new ColonySite(sys.Id, sys.Name, sys.PlanetClass, deposits, lanes == int.MaxValue ? -1 : lanes, from, lanes == 1 || standing, ColonyRefusal(w, cat, house, sys.Id), near?.Id, near?.Name, near == null ? -1 : fl));
        }
        return list.OrderByDescending(x => x.Refusal == null).ThenByDescending(x => x.InReach).ThenBy(x => x.Lanes < 0 ? 99 : x.Lanes).ThenBy(x => x.SystemId, StringComparer.Ordinal).ToList();
    }

    /// <summary>The colony lands: the system's world is a holding of the house, small and loyal, governed by its head.</summary>
    public static Holding? Settle(World w, HoldingCatalog cat, House house, string systemId)
    {
        var g = w.Galaxy; var sys = g?.System(systemId); if (g == null || sys == null || w.PlanetOf(systemId) != null) return null;
        string spec = sys.PlanetClass == Body.Belt && cat.Spec("mining_belt") != null ? "mining_belt" : cat.Colony.Specialization; if (cat.Spec(spec) == null) spec = cat.Specs.OrderBy(s => s.Id, StringComparer.Ordinal).FirstOrDefault()?.Id ?? spec;
        string id = "colony_" + systemId; int n = 1; while (w.Holdings.ContainsKey(id)) id = $"colony_{systemId}_{++n}";
        string bare = sys.Name.StartsWith("The ", StringComparison.Ordinal) ? sys.Name[4..] : sys.Name;
        var h = new Holding { Id = id, OwnerId = house.Id, Name = $"{bare} Colony", Specialization = spec, SystemId = systemId, Population = cat.Colony.Population, Culture = house.Culture, GovernorId = Economy.HeadOf(w, house)?.Id ?? "", Prosperity = 40 };
        w.Holdings[id] = h; w.HoldingsChanged(); w.Development[systemId] = Math.Min(cat.Rules.DevSeed, DevelopmentCeiling(w, cat, systemId));
        w.WorldCounters[ColoniesCounter] = w.Counter(ColoniesCounter) + 1; if (house.Id == w.Protagonist?.HouseId) w.WorldFlags.Add(ColonyFlag);
        w.Log($"{house.Name} plants a colony at {sys.Name}: a world nobody held is held.");
        return h;
    }

    /// <summary>The yearly housekeeping: the player's flags of last year are cleared, a work whose body is gone is dropped, and every
    /// held system's development moves a step toward what it can be.</summary>
    public static void Tick(World w, HoldingCatalog cat)
    {
        Develop(w, cat);
        w.WorldFlags.Remove(BuiltFlag); w.WorldFlags.Remove(WonderFlag); w.WorldFlags.Remove(ColonyFlag);
        if (w.Galaxy == null) return;
        w.BodyWorks.RemoveAll(x => w.Galaxy.System(x.SystemId) is not { } s || x.Body < 0 || x.Body >= s.Bodies.Count);
    }
}
