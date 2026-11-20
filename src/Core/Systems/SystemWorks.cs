using Praetoria.Core.Data;
using Praetoria.Core.Galaxy;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// What is built on the bodies of a solar system, and the settling of a system nobody holds (docs/SYSTEMS.md; the owner, 2026-11-20:
/// "you should be able to do things for and with every part of your solar system. So like establishing new colonies building a new
/// resource ... building out military bases building out mega structures ... unique building[s] ... but for your own specific solar
/// system"). The holder of a system's world may build on every body in it, the sun included: a body has room for one to three works.
/// A work goes with the system: whoever holds the world has what was built round it. Rules and reckoning only; the commands are in
/// <see cref="Commands.BuildWorkCommand"/> and <see cref="Commands.FoundColonyCommand"/>. RNG-free.
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
    /// <summary>Room left on a body: its slots, less what stands and what is being built.</summary>
    public static int FreeSlots(World w, StarSystem sys, int body) => body < 0 || body >= sys.Bodies.Count ? 0 : sys.Bodies[body].Slots - On(w, sys.Id, body).Count() - Underway(w, sys.Id, body).Count();

    /// <summary>Whether a work suits a body at all, whoever asks: its kind, its feature and its deposit.</summary>
    public static bool Suits(StarSystem sys, Body b, WorkDef def) =>
        def.Fits(b.Class) && (def.Feature.Length == 0 || b.Feature == def.Feature) && (def.Deposit.Length == 0 || (b.Deposit == def.Deposit && sys.Deposit(def.Deposit) > 0));

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
        if (!def.Fits(b.Class)) return $"does not suit {(b.IsStar ? "a sun" : Article(b.Class))}";
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
        if (FreeSlots(w, sys, body) <= 0) return "no room left on this body";
        if (def.MinTitleRank > 0 && (titles?.RankOf(house.Title) ?? Progression.Title.RankOf(house.Title)) < def.MinTitleRank) return "needs a greater rank";
        var first = ProjectSystem.Split(def.Cost, def.BuildTurns).First;
        return house.Treasury.CanAfford(first) ? null : !explain ? "needs more" : $"needs {Presentation.CourtProjection.Bundle(first)}{(def.BuildTurns > 1 ? " to begin" : "")}";
    }
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
        w.WorldCounters[BuiltCounter] = w.Counter(BuiltCounter) + 1; bool wonder = def.Category == WorkDef.Wonder || def.IsMegastructure;
        if (wonder) w.WorldCounters[WondersCounter] = w.Counter(WondersCounter) + 1;
        if (house.Id == w.Protagonist?.HouseId) { w.WorldFlags.Add(BuiltFlag); if (wonder) w.WorldFlags.Add(WonderFlag); }
        if (wonder) w.Log($"{def.Name} stands {Where(b)} in the {sys.Name.Replace("The ", "")} system: the work of {house.Name}.{(def.Limit == WorkDef.PerGalaxy ? " There will not be another." : "")}");
        return work;
    }
    private static string Where(Body b) => b.IsStar ? "round the sun" : b.Class == Body.Belt ? $"in {b.Name}" : b.Class == "gas" ? $"over {b.Name}" : $"on {b.Name}";

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
            yield return ($"{def.Name} · {(x.Body >= 0 && x.Body < sys.Bodies.Count ? sys.Bodies[x.Body].Name : sys.Name)}", d);
        }
    }
    public static double Defense(World w, HoldingCatalog cat, string systemId) { double d = 0; foreach (var x in In(w, systemId)) d += cat.Work(x.DefId)?.Defense ?? 0; return d; }
    public static bool Depot(World w, HoldingCatalog cat, string systemId) { foreach (var x in In(w, systemId)) if (cat.Work(x.DefId)?.Depot == true) return true; return false; }
    public static int Prosperity(World w, HoldingCatalog cat, string systemId) { int v = 0; foreach (var x in In(w, systemId)) v += cat.Work(x.DefId)?.Prosperity ?? 0; return v; }
    public static int Unrest(World w, HoldingCatalog cat, string systemId) { int v = 0; foreach (var x in In(w, systemId)) v += cat.Work(x.DefId)?.Unrest ?? 0; return v; }
    public static int Population(World w, HoldingCatalog cat, string systemId) { int v = 0; foreach (var x in In(w, systemId)) v += cat.Work(x.DefId)?.Population ?? 0; return v; }

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
