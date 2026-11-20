using Praetoria.Core.Data;
using Praetoria.Core.Rng;
using Praetoria.Core.State;

namespace Praetoria.Core.Galaxy;

/// <summary>
/// Grows the written map to the size a game was begun at (docs/GALAXY_SIZE.md). The galaxy file is the small map. A larger one
/// spreads its systems apart about the middle of the map and fills the room with new systems, region by region, from the game's
/// seed: each new system is hung off one of its region by a lane, named from <see cref="GrowthCatalog"/>, and given a world of a
/// kind its region already has. In the Reach a new world goes to the house that holds the system it was hung off, while that house
/// has not had its share; to a new house raised to hold it; or, on the rim and in the middle, to nobody. Beyond the Reach the new
/// systems are left for the nation whose space they are in (<see cref="Systems.NationSystem.Worlds"/> gives them to it when it is
/// put on the map). Call once, on a world just built and before its first turn. Nothing here draws from the world's own dice.
/// </summary>
public static class GalaxyGrowth
{
    private static ulong Hash(string key) { ulong h = 14695981039346656037UL; foreach (var ch in key) h = (h ^ ch) * 1099511628211UL; return h; }

    /// <summary>Set the game's size and grow its map to it. The small size, a world with no map, and a map already grown are left alone.</summary>
    public static void Grow(World w, ContentDatabase content, string size)
    {
        var cat = content.Growth; var g = w.Galaxy;
        if (!GrowthCatalog.Sizes.Contains(size)) size = GrowthCatalog.Small;
        if (g != null && g.Systems.Values.Any(s => s.Grown)) return;
        w.GalaxySize = size; w.GalaxyScale = cat.PercentOf(size);
        if (g == null || w.GalaxyScale <= 100 || g.Systems.Count < 2) { w.GalaxySize = GrowthCatalog.Small; w.GalaxyScale = 100; return; }
        int pct = w.GalaxyScale; var rng = new SplitMix64Rng(w.RngState ^ Hash("galaxy:" + size));
        var written = g.Systems.Values.OrderBy(s => s.Id, StringComparer.Ordinal).ToList();

        // how far apart systems sit on the written map: the middle of every system's distance to its nearest neighbour
        var nearest = written.Select(a => written.Where(b => b != a).Min(b => Dist(a.X, a.Y, b.X, b.Y))).OrderBy(d => d).ToList();
        double spacing = nearest[nearest.Count / 2];
        // spread the written systems so that the map has room in proportion to what it will hold
        double f = Math.Sqrt(pct / 100.0), cx = written.Average(s => s.X), cy = written.Average(s => s.Y);
        foreach (var s in written) { s.X = Math.Round(cx + (s.X - cx) * f, 1); s.Y = Math.Round(cy + (s.Y - cy) * f, 1); }

        var names = new Namer(cat, g, rng); var all = new List<StarSystem>(written); var hungOff = new Dictionary<string, string>(); var made = new List<StarSystem>();
        foreach (var region in g.Regions)
        {
            var mine = all.Where(s => s.RegionId == region.Id).ToList(); if (mine.Count == 0) continue;
            int add = (mine.Count * (pct - 100) + 50) / 100;
            var kinds = mine.Select(s => s.PlanetClass).ToList();
            for (int i = 0; i < add; i++)
            {
                var spot = Spot(g, all, mine, region.Id, spacing, rng); if (spot == null) break;
                var (parent, x, y) = spot.Value;
                var sys = Furnish(names.System(region), x, y, region, kinds, cat, rng);
                g.Systems[sys.Id] = sys; all.Add(sys); mine.Add(sys); made.Add(sys); hungOff[sys.Id] = parent.Id;
                g.Lanes.Add(new Hyperlane { A = parent.Id, B = sys.Id });
            }
        }
        // a second lane for some, so the new systems are a web and not a row of dead ends: within a region, or between two regions of the Reach
        foreach (var sys in made)
        {
            bool reach = g.Region(sys.RegionId)?.Ring != Ring.Beyond; int chance = 55;
            foreach (var other in all.Where(o => o != sys && Dist(o.X, o.Y, sys.X, sys.Y) <= spacing * 1.55).OrderBy(o => Dist(o.X, o.Y, sys.X, sys.Y)).ThenBy(o => o.Id, StringComparer.Ordinal).Take(3))
            {
                if (g.Lanes.Any(l => (l.A == sys.Id && l.B == other.Id) || (l.B == sys.Id && l.A == other.Id))) continue;
                bool sameRegion = other.RegionId == sys.RegionId, bothReach = reach && g.Region(other.RegionId)?.Ring != Ring.Beyond;
                if (!sameRegion && !bothReach) continue;
                if (rng.NextInt(0, 100) >= chance || Blocked(g, all, sys.Id, sys.X, sys.Y, other, spacing)) continue;
                g.Lanes.Add(new Hyperlane { A = sys.Id, B = other.Id }); chance = 20;
            }
        }
        Settle(w, content, made, hungOff, pct, rng, names);
        var problems = g.Problems().ToList(); if (problems.Count > 0) throw new ContentException($"The grown galaxy is broken: {string.Join("; ", problems)}");
    }

    private static double Dist(double ax, double ay, double bx, double by) => Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

    /// <summary>Somewhere for a new system of a region: a lane's length from one of its systems, clear of every other system, nearer its
    /// own region than any other, and reached by a lane that crosses none and passes close to no third system. Of several such places, the most surrounded.</summary>
    private static (StarSystem parent, double x, double y)? Spot(Galaxy g, List<StarSystem> all, List<StarSystem> mine, string regionId, double spacing, IRng rng)
    {
        // of a handful of places that would do, the one with the most systems round it: so the gaps inside a realm fill before it sprawls outward
        (StarSystem parent, double x, double y)? best = null; int bestScore = -1, found = 0;
        for (int attempt = 0; attempt < 900 && found < 8; attempt++)
        {
            double clear = spacing * (attempt < 400 ? 0.76 : attempt < 700 ? 0.66 : 0.58);
            var parent = mine[rng.NextInt(0, mine.Count)];
            double ang = rng.NextDouble() * Math.PI * 2, dist = spacing * (0.88 + 0.5 * rng.NextDouble());
            double x = Math.Round(parent.X + Math.Cos(ang) * dist, 1), y = Math.Round(parent.Y + Math.Sin(ang) * dist, 1);
            StarSystem? near = null; double nd = double.MaxValue; bool crowded = false; int round = 0;
            foreach (var o in all) { double d = Dist(o.X, o.Y, x, y); if (d < clear) { crowded = true; break; } if (d < nd) { nd = d; near = o; } if (d < spacing * 2.1) round += o.RegionId == regionId ? 3 : 2; }
            if (crowded || near == null || near.RegionId != regionId) continue;
            if (Blocked(g, all, "", x, y, parent, spacing)) continue;
            found++; if (round > bestScore) { bestScore = round; best = (parent, x, y); }
        }
        return best;
    }

    /// <summary>Whether a lane from a point (a system's, when <paramref name="fromId"/> names it) to a system would cross a lane or pass too close to a third system.</summary>
    private static bool Blocked(Galaxy g, List<StarSystem> all, string fromId, double x, double y, StarSystem to, double spacing)
    {
        foreach (var l in g.Lanes)
        {
            if (l.A == to.Id || l.B == to.Id || l.A == fromId || l.B == fromId) continue;
            var a = g.Systems[l.A]; var b = g.Systems[l.B];
            if (Crosses(x, y, to.X, to.Y, a.X, a.Y, b.X, b.Y)) return true;
        }
        foreach (var o in all) { if (o == to || o.Id == fromId) continue; if (ToSegment(o.X, o.Y, x, y, to.X, to.Y) < spacing * 0.3) return true; }
        return false;
    }
    private static double Side(double ax, double ay, double bx, double by, double px, double py) => (bx - ax) * (py - ay) - (by - ay) * (px - ax);
    private static bool Crosses(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy) =>
        Side(ax, ay, bx, by, cx, cy) * Side(ax, ay, bx, by, dx, dy) < 0 && Side(cx, cy, dx, dy, ax, ay) * Side(cx, cy, dx, dy, bx, by) < 0;
    private static double ToSegment(double px, double py, double ax, double ay, double bx, double by)
    {
        double vx = bx - ax, vy = by - ay, len = vx * vx + vy * vy; if (len <= 0) return Dist(px, py, ax, ay);
        double t = Math.Clamp(((px - ax) * vx + (py - ay) * vy) / len, 0, 1); return Dist(px, py, ax + t * vx, ay + t * vy);
    }

    private static readonly (string kind, int weight)[] Kinds = { ("terran", 3), ("ocean", 3), ("arid", 3), ("forge", 2), ("frozen", 2), ("barren", 3), ("gas", 2), ("belt", 1) };

    /// <summary>A new system: a world of a kind its region leans to, a belt for some, a deposit where its kind has one, and a body or two beside the world.</summary>
    private static StarSystem Furnish((string id, string name) named, double x, double y, Region region, List<string> regionKinds, GrowthCatalog cat, IRng rng)
    {
        var weights = Kinds.Select(k => (k.kind, weight: k.weight + 3 * regionKinds.Count(r => r == k.kind))).ToList();
        int roll = rng.NextInt(0, weights.Sum(k => k.weight)); string kind = weights[^1].kind;
        foreach (var k in weights) { if (roll < k.weight) { kind = k.kind; break; } roll -= k.weight; }
        var sys = new StarSystem { Id = named.id, Name = named.name, X = x, Y = y, RegionId = region.Id, PlanetClass = kind, Grown = true };
        if (kind == "belt") sys.Belt = rng.NextInt(2, 4); else if (rng.NextInt(0, 100) < 22) sys.Belt = rng.NextInt(1, 3);
        if (kind == "gas" && rng.NextInt(0, 100) < 80) sys.Deposits["helium3"] = rng.NextInt(1, 4);
        else if (rng.NextInt(0, 100) < 10) sys.Deposits["helium3"] = 1;
        if ((kind is "belt" or "barren" or "arid" || sys.Belt > 0) && rng.NextInt(0, 100) < 40) sys.Deposits["rare_earths"] = rng.NextInt(1, 3);
        if (kind is "barren" or "frozen" && rng.NextInt(0, 100) < 25) sys.Deposits["isotopes"] = rng.NextInt(1, 3);
        if (cat.Descriptions.TryGetValue(kind, out var lines) && lines.Count > 0) sys.Description = lines[rng.NextInt(0, lines.Count)];
        string bare = named.name.StartsWith("The ", StringComparison.Ordinal) ? named.name[4..] : named.name;
        int inner = rng.NextInt(0, 2), outer = rng.NextInt(1, 3); string[] numerals = { "I", "II", "III", "IV", "V", "VI" }; int n = 0;
        for (int i = 0; i < inner; i++) sys.Bodies.Add(new Body { Name = $"{bare} {numerals[n++]}", Class = rng.NextInt(0, 2) == 0 ? "barren" : "forge", Size = rng.NextInt(1, 3) });
        sys.Bodies.Add(new Body { Name = named.name, Class = kind, Size = 3, IsPrimary = true }); n++;
        for (int i = 0; i < outer; i++) { string c = new[] { "gas", "frozen", "barren", "gas", "arid" }[rng.NextInt(0, 5)]; sys.Bodies.Add(new Body { Name = $"{bare} {numerals[n++]}", Class = c, Size = c == "gas" ? 3 : rng.NextInt(1, 3) }); }
        if (sys.Belt > 0 && kind != "belt") sys.Bodies.Add(new Body { Name = $"The {bare} Belt", Class = "belt", Size = 2 });
        return sys;
    }

    /// <summary>Names for what the growth makes: systems from the lists, in an order the seed sets, never one already on the map; and
    /// surnames for the houses raised, never one a house already bears.</summary>
    private sealed class Namer
    {
        private readonly Galaxy _g; private readonly Dictionary<string, Queue<string>> _region = new(); private readonly Queue<string> _reach; private readonly Queue<string> _surnames;
        private readonly Dictionary<string, int> _count = new();
        public Namer(GrowthCatalog cat, Galaxy g, IRng rng)
        {
            _g = g; _reach = Shuffled(cat.Names, rng); _surnames = Shuffled(cat.Surnames, rng);
            foreach (var (region, list) in cat.RegionNames.OrderBy(k => k.Key, StringComparer.Ordinal)) _region[region] = Shuffled(list, rng);
        }
        private static Queue<string> Shuffled(IReadOnlyList<string> list, IRng rng)
        {
            var a = list.ToList(); for (int i = a.Count - 1; i > 0; i--) { int j = rng.NextInt(0, i + 1); (a[i], a[j]) = (a[j], a[i]); }
            return new Queue<string>(a);
        }
        private static string Slug(string name) => new string(name.ToLowerInvariant().Select(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' ? c : '_').ToArray()).Trim('_').Replace("__", "_");
        public (string id, string name) System(Region region)
        {
            var from = _region.TryGetValue(region.Id, out var own) ? own : region.Ring == Ring.Beyond ? new Queue<string>() : _reach;
            while (from.Count > 0)
            {
                string name = from.Dequeue(), id = Slug(name);
                if (id.Length == 0 || _g.Systems.ContainsKey(id) || _g.Systems.Values.Any(s => s.Name == name)) continue;
                return (id, name);
            }
            string[] numerals = { "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII", "XIII", "XIV", "XV", "XVI" };   // the lists ran out: number the region's own systems
            var bases = _g.SystemsIn(region.Id).Where(s => !s.Grown).ToList(); string stem = (bases.Count > 0 ? bases[0].Name : region.Name).Replace("The ", "");
            for (int n = _count.GetValueOrDefault(region.Id); ; n++)
            {
                string name = $"{stem} {(n < numerals.Length ? numerals[n] : (n + 2).ToString())}", id = Slug(name);
                if (_g.Systems.ContainsKey(id)) continue;
                _count[region.Id] = n + 1; return (id, name);
            }
        }
        public string? Surname(World w)
        {
            while (_surnames.Count > 0) { string s = _surnames.Dequeue(); if (!w.Houses.Values.Any(h => h.Name == "House " + s) && !w.Houses.ContainsKey("house_" + Slug(s))) return s; }
            return null;
        }
        public static string SlugOf(string name) => Slug(name);
    }

    /// <summary>Who holds the new systems of the Reach. A house gains again a share of what it holds; new houses are raised for the rest; some are left to nobody.</summary>
    private static void Settle(World w, ContentDatabase content, List<StarSystem> made, Dictionary<string, string> hungOff, int pct, IRng rng, Namer names)
    {
        var g = w.Galaxy!; var cat = content.Growth;
        var quota = new Dictionary<string, int>();
        foreach (var grp in w.Holdings.Values.Where(h => h.IsPlanet && w.OfTheReach(h.OwnerId)).GroupBy(h => h.OwnerId))
            quota[grp.Key] = (grp.Count() * (pct - 100) * cat.Kept + 5000) / 10000;
        // people to make the new houses' people like: those of the Reach already written
        var grown = w.Characters.Values.Where(c => c.Alive && c.Id != w.ProtagonistId && w.OfTheReach(c.HouseId)).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        var leaders = grown.Where(c => c.Age >= 28 && c.CareerRank >= 2).ToList(); var adults = grown.Where(c => c.Age >= 22).ToList(); var young = grown.Where(c => c.Age is >= 16 and < 26).ToList();
        foreach (var sys in made)
        {
            var region = g.Region(sys.RegionId); if (region == null || region.Ring == Ring.Beyond) continue;   // a nation's own: NationSystem gives them to it
            if (rng.NextInt(0, 100) < cat.Unheld.GetValueOrDefault(region.Ring)) continue;
            string spec = Specialization(sys.PlanetClass, region.Ring, rng); string culture = g.CultureOf(sys.Id);
            string bare = sys.Name.StartsWith("The ", StringComparison.Ordinal) ? sys.Name[4..] : sys.Name;
            string worldName = cat.WorldNames.TryGetValue(spec, out var pats) && pats.Count > 0 ? string.Format(pats[rng.NextInt(0, pats.Count)], bare) : sys.Name;
            int population = content.Holdings.Spec(spec) is { PopGrowth: > 0 } sp ? Math.Clamp(rng.NextInt(10, 34), 6, Math.Max(8, sp.MaxPopulation * 2 / 3)) : rng.NextInt(8, 20);
            var holding = new Holding { Id = "grown_" + sys.Id, Name = worldName, Specialization = spec, SystemId = sys.Id, Population = population, Culture = culture };
            string? near = hungOff.TryGetValue(sys.Id, out var p) ? w.PlanetHolder(p) : null;
            if (near != null && quota.GetValueOrDefault(near) > 0 && w.House(near) is { } keeper)
            {
                quota[near]--; holding.OwnerId = keeper.Id;
                holding.GovernorId = w.Characters.Values.Where(c => c.Alive && c.HouseId == keeper.Id && c.Age >= Systems.DynastySystem.AgeOfMajority).OrderByDescending(c => c.CareerRank).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault()?.Id ?? "";
                w.Holdings[holding.Id] = holding; continue;
            }
            var surname = names.Surname(w); if (surname == null) continue;   // no names left: the world stays empty
            var house = Raise(w, content, surname, culture, sys, rng, leaders, adults, young);
            holding.OwnerId = house.Id; holding.GovernorId = house.Members[0]; w.Holdings[holding.Id] = holding;
        }
    }

    private static string Specialization(string kind, string ring, IRng rng)
    {
        bool rim = ring == Ring.Rim, core = ring == Ring.Core; int r = rng.NextInt(0, 100);
        return kind switch
        {
            "terran" => core && r < 45 ? "trade_hub" : rim && r < 30 ? "frontier_colony" : "agri_world",
            "ocean" => r < (core ? 70 : 45) ? "trade_hub" : "agri_world",
            "arid" => rim && r < 45 ? "frontier_colony" : r < 70 ? "mining_belt" : "fortress",
            "forge" => "forge_world",
            "frozen" => r < 30 ? "research_station" : "frontier_colony",
            "barren" => r < 45 ? "fortress" : r < 75 ? "mining_belt" : "frontier_colony",
            "gas" => r < 55 ? "orbital_yard" : "frontier_colony",
            _ => "mining_belt"
        };
    }

    /// <summary>A house raised to hold a new world: a head, a husband or wife, and for most a child, made like people already written, and for some a ship or two.</summary>
    private static House Raise(World w, ContentDatabase content, string surname, string culture, StarSystem sys, IRng rng, List<Character> leaders, List<Character> adults, List<Character> young)
    {
        string id = "house_" + Namer.SlugOf(surname);
        var house = new House { Id = id, Name = "House " + surname, AccentColor = Presentation.Palette.NextHouseColor(w, id), Title = rng.NextInt(0, 100) < 30 ? "knight" : "baron", Legitimacy = rng.NextInt(16, 31), Culture = culture,
            Treasury = new Resources { Credits = rng.NextInt(8, 17), Materials = rng.NextInt(4, 9), Manpower = rng.NextInt(4, 9), Influence = rng.NextInt(1, 3) } };
        w.Houses[id] = house;
        Character Person(string role, int age, string sex, Character? like, int maxRank)
        {
            string given = content.Names.IsEmpty ? "Scion" : content.Names.Pick(culture, sex, rng);
            var c = new Character { Id = $"{id}_{role}", Name = $"{given} {surname}", HouseId = id, Age = age, Sex = sex, CareerTrack = like?.CareerTrack.Length > 0 ? like.CareerTrack : "stewardship", CareerRank = Math.Min(maxRank, like?.CareerRank ?? 0), Ambition = like?.Ambition ?? "" };
            if (like != null) { foreach (var t in like.NatureTraits) c.NatureTraits.Add(t); foreach (var t in like.AptitudeTraits) c.AptitudeTraits.Add(t); foreach (var (k, v) in like.Skills) c.Skills[k] = v; }
            w.Characters[c.Id] = c; w.Pools[c.Id] = ActionPools.ForNpc(); house.Members.Add(c.Id); return c;
        }
        Character? Like(List<Character> from) => from.Count == 0 ? null : from[rng.NextInt(0, from.Count)];
        string headSex = rng.NextInt(0, 100) < 50 ? "female" : "male";
        var head = Person("head", rng.NextInt(32, 59), headSex, Like(leaders) ?? Like(adults), 3);
        var spouse = Person("spouse", Math.Max(24, head.Age + rng.NextInt(-6, 5)), headSex == "female" ? "male" : "female", Like(adults), 1);
        w.Relationships.Add(new Relationship { FromId = head.Id, ToId = spouse.Id, Bond = BondType.Marriage, BondStrength = 50, Disposition = rng.NextInt(10, 51) });
        w.Relationships.Add(new Relationship { FromId = spouse.Id, ToId = head.Id, Bond = BondType.Marriage, BondStrength = 50, Disposition = rng.NextInt(10, 51) });
        int children = rng.NextInt(0, 100) < 75 ? rng.NextInt(1, 3) : 0; int eldest = Math.Min(head.Age, spouse.Age) - 20;
        for (int i = 0; i < children && eldest - i * 4 >= 2; i++)
        {
            int age = Math.Max(2, eldest - i * 4 - rng.NextInt(0, 4));
            var child = Person(i == 0 ? "heir" : "child" + (i + 1), age, rng.NextInt(0, 2) == 0 ? "female" : "male", age >= 16 ? Like(young) : null, 0);
            if (age < 16) { child.CareerTrack = head.CareerTrack; child.Ambition = ""; }
            child.MotherId = headSex == "female" ? head.Id : spouse.Id; child.FatherId = headSex == "female" ? spouse.Id : head.Id;
        }
        if (rng.NextInt(0, 100) < 55)
        {
            int hulls = rng.NextInt(1, 3);
            w.Fleets["fleet_" + id] = new Fleet { Id = "fleet_" + id, Name = $"The {surname} Guard", OwnerId = id, SystemId = sys.Id, Strength = hulls, AdmiralId = head.Id };
            house.Treasury.Helium3 = Math.Max(house.Treasury.Helium3, 10 + 2 * hulls);
        }
        return house;
    }
}
