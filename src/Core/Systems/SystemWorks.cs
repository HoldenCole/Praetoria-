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
/// body's slots; GREAT WORKS, of which a body has room for one, on its site, and a house raises one at a time; and INFRASTRUCTURE,
/// which is the system's own and is bought a level at a time. The system's own world is built on through its holding
/// (<see cref="Commands.BuildCommand"/>): its slots are its specialization's, and civil works open more. A work goes with the system:
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

    // ---------------- slots, and the one site ----------------

    /// <summary>The building slots of a body (docs/BUILDINGS.md): by its kind, one for a small rock or ice-dwarf, none on the sun; and one
    /// more for each level of the system's infrastructure that opens them. The system's own world has none here: it is built on through
    /// its holding, whose slots are <see cref="WorldSlots"/>.</summary>
    public static int Slots(World w, HoldingCatalog cat, StarSystem sys, int body)
    {
        if (body < 0 || body >= sys.Bodies.Count) return 0; var b = sys.Bodies[body]; if (b.IsStar || b.IsPrimary) return 0;
        int n = cat.BodySlots.TryGetValue(b.Class, out var v) ? v : 1;
        if (b.Size <= 1 && b.Class is "barren" or "frozen" or "arid" or "forge") n = Math.Min(n, cat.SmallSlots);
        return n + Opened(w, cat, sys.Id, "bodies");
    }
    /// <summary>Slots that the system's infrastructure has opened on its world ("world") or on every other body ("bodies").</summary>
    public static int Opened(World w, HoldingCatalog cat, string systemId, string where)
    {
        int n = 0; if (!w.Infrastructure.TryGetValue(systemId, out var levels)) return 0;
        foreach (var t in cat.Infrastructure) if (t.Opens == where && levels.TryGetValue(t.Id, out var lv)) n += t.SlotsUpTo(lv);
        return n;
    }
    /// <summary>The building slots of a holding: its specialization's, and for a system's world those its civil works have opened.</summary>
    public static int WorldSlots(World w, HoldingCatalog cat, Holding h) => (cat.Spec(h.Specialization)?.Slots ?? 0) + (h.IsPlanet && w.Infrastructure.Count > 0 ? Opened(w, cat, h.SystemId, "world") : 0);

    private static bool Great(HoldingCatalog cat, string defId) => cat.Work(defId)?.IsMegastructure == true;
    /// <summary>Buildings on a body, standing and rising: what its slots are spent on. Great works are not counted; they have the site.</summary>
    public static int SlotsUsed(World w, HoldingCatalog cat, string systemId, int body) => On(w, systemId, body).Count(x => !Great(cat, x.DefId)) + Underway(w, systemId, body).Count(p => !Great(cat, p.DefId));
    /// <summary>Room left for buildings on a body.</summary>
    public static int FreeSlots(World w, HoldingCatalog cat, StarSystem sys, int body) => Slots(w, cat, sys, body) - SlotsUsed(w, cat, sys.Id, body);
    /// <summary>The great work on a body's site, standing or rising: its definition id, or null when the site is free.</summary>
    public static string? Site(World w, HoldingCatalog cat, string systemId, int body) =>
        On(w, systemId, body).FirstOrDefault(x => Great(cat, x.DefId))?.DefId ?? Underway(w, systemId, body).FirstOrDefault(p => Great(cat, p.DefId))?.DefId;
    /// <summary>The great work a house is raising now, if any: a house raises one at a time.</summary>
    public static Project? Raising(World w, HoldingCatalog cat, string houseId) => w.Projects.FirstOrDefault(p => p.Kind == Project.Work && p.OwnerId == houseId && Great(cat, p.DefId));

    /// <summary>Whether a work suits a body at all, whoever asks: its kind, its feature and its deposit. A building is for every body but
    /// the system's own world (which has its holding's buildings); a great work may be for the world alone.</summary>
    public static bool Suits(StarSystem sys, Body b, WorkDef def) =>
        def.Fits(b.Class) && (def.IsMegastructure ? def.On != "world" || b.IsPrimary : !b.IsPrimary)
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
        if (!def.IsMegastructure && b.IsPrimary) return "the system's own world is built on through its holding";
        if (def.IsMegastructure && def.On == "world" && !b.IsPrimary) return "only on the system's own world";
        if (def.Feature.Length > 0 && b.Feature != def.Feature) return $"needs {cat.Feature(def.Feature)?.Name.ToLowerInvariant() ?? def.Feature}";
        if (def.Deposit.Length > 0 && (b.Deposit != def.Deposit || sys.Deposit(def.Deposit) == 0)) return $"needs a {Resource.Label(def.Deposit)} deposit here";
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
            if (Raising(w, cat, house.Id) is { } rising) return $"the house is already raising {rising.Name}: one great work at a time";
        }
        else if (FreeSlots(w, cat, sys, body) <= 0) return Slots(w, cat, sys, body) == 0 ? "nothing but a great work can be built here" : "no slot left here: outworks open more";
        if (def.MinTitleRank > 0 && (titles?.RankOf(house.Title) ?? Progression.Title.RankOf(house.Title)) < def.MinTitleRank) return "needs a greater rank";
        var first = ProjectSystem.Split(def.Cost, def.BuildTurns).First;
        return house.Treasury.CanAfford(first) ? null : !explain ? "needs more" : $"needs {Presentation.CourtProjection.Bundle(first)}{(def.BuildTurns > 1 ? " to begin" : "")}";
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
            foreach (var r in Resource.All) d.Add(r, (def.Deposit == r ? def.Yield.Get(r) * Math.Max(1, sys.Deposit(r)) : def.Yield.Get(r)) - def.Upkeep.Get(r));
            yield return ($"{def.Name} · {(x.Body >= 0 && x.Body < sys.Bodies.Count && !sys.Bodies[x.Body].IsSpace ? sys.Bodies[x.Body].Name : sys.Name)}", d);
        }
        foreach (var (t, lv) in Levels(w, cat, systemId))
        {
            if (t.Yield.IsEmpty()) continue; var d = new Resources(); foreach (var r in Resource.All) d.Add(r, t.Yield.Get(r) * lv);
            yield return ($"{t.Name} {Roman(lv)} · {sys.Name}", d);
        }
    }
    /// <summary>Whether anything is built or laid anywhere: asked first, so a bare map costs nothing to reckon.</summary>
    public static bool Any(World w) => w.BodyWorks.Count > 0 || w.Infrastructure.Count > 0;
    private static IEnumerable<(InfrastructureDef def, int level)> Levels(World w, HoldingCatalog cat, string systemId)
    {
        if (!w.Infrastructure.TryGetValue(systemId, out var levels)) yield break;
        foreach (var t in cat.Infrastructure) if (levels.TryGetValue(t.Id, out var lv) && lv > 0) yield return (t, lv);
    }
    public static double Defense(World w, HoldingCatalog cat, string systemId) { double d = 0; foreach (var x in In(w, systemId)) d += cat.Work(x.DefId)?.Defense ?? 0; foreach (var (t, lv) in Levels(w, cat, systemId)) d += t.Defense * lv; return d; }
    public static bool Depot(World w, HoldingCatalog cat, string systemId) { foreach (var x in In(w, systemId)) if (cat.Work(x.DefId)?.Depot == true) return true; foreach (var (t, lv) in Levels(w, cat, systemId)) if (t.DepotAt > 0 && lv >= t.DepotAt) return true; return false; }
    public static int Prosperity(World w, HoldingCatalog cat, string systemId) { int v = 0; foreach (var x in In(w, systemId)) v += cat.Work(x.DefId)?.Prosperity ?? 0; foreach (var (t, lv) in Levels(w, cat, systemId)) v += t.Prosperity * lv; return v; }
    public static int Unrest(World w, HoldingCatalog cat, string systemId) { int v = 0; foreach (var x in In(w, systemId)) v += cat.Work(x.DefId)?.Unrest ?? 0; foreach (var (t, lv) in Levels(w, cat, systemId)) v -= t.CalmAt.Count(l => l <= lv); return v; }
    public static int Population(World w, HoldingCatalog cat, string systemId) { int v = 0; foreach (var x in In(w, systemId)) v += cat.Work(x.DefId)?.Population ?? 0; foreach (var (t, lv) in Levels(w, cat, systemId)) v += t.Population * lv; return v; }

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
        return house.Treasury.CanAfford(first) ? null : !explain ? "needs more" : $"needs {Presentation.CourtProjection.Bundle(first)}{(t.YearsOf(level + 1) > 1 ? " to begin" : "")}";
    }
    /// <summary>A level is laid: the track stands one higher.</summary>
    public static void Lay(World w, string systemId, InfrastructureDef t)
    {
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
        if (g.RingOf(systemId) == Ring.Beyond) return "it lies in a nation's space";
        if (w.Projects.Any(p => p.Kind == Project.Colony && p.TargetId == systemId)) return w.Projects.Any(p => p.Kind == Project.Colony && p.TargetId == systemId && p.OwnerId == house.Id) ? "your colony is on its way" : "another house is settling it";
        if (World.Outsider(house.Id)) return "not for you";
        bool near = g.Neighbors(systemId).Any(n => w.PlanetHolder(n) == house.Id) || w.Fleets.Values.Any(f => f.OwnerId == house.Id && f.SystemId == systemId && !f.Moving && !f.IsAway);
        if (!near) return "too far: it must lie a lane from a world of yours, or have a fleet of yours standing in it";
        if (w.Fleets.Values.Any(f => f.SystemId == systemId && !f.Moving && !f.IsAway && f.OwnerId != house.Id && w.AtWar(f.OwnerId, house.Id))) return "an enemy fleet holds the sky";
        var first = ProjectSystem.Split(cat.Colony.Cost, cat.Colony.BuildTurns).First;
        return house.Treasury.CanAfford(first) ? null : !explain ? "needs more" : $"needs {Presentation.CourtProjection.Bundle(first)} to begin";
    }

    /// <summary>The colony lands: the system's world is a holding of the house, small and loyal, governed by its head.</summary>
    public static Holding? Settle(World w, HoldingCatalog cat, House house, string systemId)
    {
        var g = w.Galaxy; var sys = g?.System(systemId); if (g == null || sys == null || w.PlanetOf(systemId) != null) return null;
        string spec = sys.PlanetClass == Body.Belt && cat.Spec("mining_belt") != null ? "mining_belt" : cat.Colony.Specialization; if (cat.Spec(spec) == null) spec = cat.Specs.OrderBy(s => s.Id, StringComparer.Ordinal).FirstOrDefault()?.Id ?? spec;
        string id = "colony_" + systemId; int n = 1; while (w.Holdings.ContainsKey(id)) id = $"colony_{systemId}_{++n}";
        string bare = sys.Name.StartsWith("The ", StringComparison.Ordinal) ? sys.Name[4..] : sys.Name;
        var h = new Holding { Id = id, OwnerId = house.Id, Name = $"{bare} Colony", Specialization = spec, SystemId = systemId, Population = cat.Colony.Population, Culture = house.Culture, GovernorId = Economy.HeadOf(w, house)?.Id ?? "", Prosperity = 40 };
        w.Holdings[id] = h; w.HoldingsChanged();
        w.WorldCounters[ColoniesCounter] = w.Counter(ColoniesCounter) + 1; if (house.Id == w.Protagonist?.HouseId) w.WorldFlags.Add(ColonyFlag);
        w.Log($"{house.Name} plants a colony at {sys.Name}: a world nobody held is held.");
        return h;
    }

    /// <summary>The yearly housekeeping: the player's flags of last year are cleared, and a work whose body is gone is dropped.</summary>
    public static void Tick(World w)
    {
        w.WorldFlags.Remove(BuiltFlag); w.WorldFlags.Remove(WonderFlag); w.WorldFlags.Remove(ColonyFlag);
        if (w.Galaxy == null) return;
        w.BodyWorks.RemoveAll(x => w.Galaxy.System(x.SystemId) is not { } s || x.Body < 0 || x.Body >= s.Bodies.Count);
    }
}
