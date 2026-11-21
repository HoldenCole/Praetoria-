using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Galaxy;

/// <summary>A body as the close map of a system draws it (docs/SYSTEMS.md, "The close map"). <see cref="Index"/> is its place in the
/// system's bodies, which is its name for good ("system#index" is where things are built). <see cref="Parent"/> is the index of the
/// world it goes round, or -1 for what goes round the sun. <see cref="Orbit"/> is the radius of its path in chart units (from the sun,
/// or from its world for a moon); <see cref="Angle"/> is where it is on that path this year, in radians, counter-clockwise from the
/// right; <see cref="X"/> and <see cref="Y"/> are where that puts it, the sun at nought and up being up on the galaxy map.</summary>
public sealed record ChartBody(int Index, string Name, string Class, int Size, bool Primary, int Parent, int Rank,
    double Orbit, double Angle, double Period, double Eccentricity, double X, double Y,
    string Feature, string FeatureName, string Deposit, int Works, int Rising, bool GreatWork);

/// <summary>Where a lane leaves the system: the neighbour it leads to, the direction it lies in on the galaxy map, and who holds it.</summary>
public sealed record ChartExit(string SystemId, string Name, double Angle, double X, double Y, string HolderId, string HolderName, string Accent, string Ring, bool Chokepoint);

/// <summary>A fleet in the system: the player's own exactly, another's as it was last reported. <see cref="AtBody"/> is the body it lies
/// off (the system's world, for the fleets of whoever holds it), or -1 for one standing out in the system. A fleet under way is drawn
/// at the lane it is making for (<see cref="ExitTo"/>).</summary>
public sealed record ChartFleet(string FleetId, string Name, string OwnerId, string OwnerName, string Accent, int Strength, bool IsPlayer, int Confidence,
    bool Moving, string ExitTo, bool LaidUp, int AtBody, double X, double Y);

/// <summary>A station in orbit, or a claim staked in the belt: a holding that is not the world.</summary>
public sealed record ChartPost(string HoldingId, string Name, string Kind, string OwnerId, string OwnerName, string Accent, int AtBody, double X, double Y);

/// <summary>One solar system laid out to be drawn close to: its sun, what goes round it and where each thing is this year, where its
/// lanes leave, and who and what is in it.</summary>
public sealed record SystemChart(string SystemId, string Name, string RegionName, string Ring, bool Capital, bool IsPlayer,
    string HolderId, string HolderName, string Accent, int SunTint, bool SunWorks, double Edge, int BeltRichness, int Year,
    IReadOnlyList<ChartBody> Bodies, IReadOnlyList<ChartExit> Exits, IReadOnlyList<ChartFleet> Fleets, IReadOnlyList<ChartPost> Posts)
{
    public ChartBody? Body(int index) => Bodies.FirstOrDefault(b => b.Index == index);
}

/// <summary>
/// Lays a solar system out for the close map (docs/SYSTEMS.md, "The close map"; the owner, 2026-11-21: "what the map would look like for
/// just the solar system ... the planets and where they are, the sun, any asteroids, and where the hyper lanes are"). Nothing here is
/// new state. Where a body is follows from the system's id, the body's place in its list, and the year: the same system is the same in
/// every game, and a year moves each thing a part of the way round, the near ones faster. Pure and RNG-free.
/// </summary>
public static class SystemCharts
{
    /// <summary>The first orbit's radius, the room between one orbit and the next, and the room beyond the last before the lanes.</summary>
    public const double FirstOrbit = 78, Step = 50, Margin = 84;
    /// <summary>A moon's first orbit round its world, and the room between moons.</summary>
    public const double MoonOrbit = 15, MoonStep = 8;
    /// <summary>How far apart two lanes are drawn at the least, in radians, when their neighbours lie in nearly one direction.</summary>
    public const double ExitGap = 0.3;
    /// <summary>A comet's path is long and thin.</summary>
    public const double CometEccentricity = 0.72;

    private static ulong Hash(string key) { ulong h = 14695981039346656037UL; foreach (var ch in key) h = (h ^ ch) * 1099511628211UL; return h; }
    private static double Turn(string key) => Hash(key) % 62832 / 10000.0;   // an angle from a name: 0 to 2π

    /// <summary>How warm a kind of body lies: the near ones first. Ties keep the order the system lists them in.</summary>
    public static int Zone(string cls) => cls switch
    {
        "forge" => 0, "arid" => 1, "terran" => 2, "ocean" => 2, "barren" => 3, Praetoria.Core.Galaxy.Body.Belt => 4, "gas" => 5, "frozen" => 6, Praetoria.Core.Galaxy.Body.Comet => 8, _ => 3
    };

    /// <summary>The standard years a body takes to go once round the sun, by how far out it is (its rank among what goes round the sun).</summary>
    public static double PeriodOf(int rank) => Math.Round(3 + 2.5 * Math.Pow(rank, 1.4), 1);

    /// <summary>Where on its path a body that goes round the sun is in a year: counter-clockwise from the right, in radians. The map of
    /// the Reach draws each system small with its worlds in these same directions, so going into a system finds them where they were.</summary>
    public static double AngleOf(string systemId, int index, int rank, int year) => Turn(systemId + "#" + index) + 2 * Math.PI * Math.Max(0, year) / PeriodOf(rank);

    /// <summary>The order of what goes round the sun, nearest first, as indices into the system's bodies. The sun itself, open space and
    /// moons are not in it; a comet is always last.</summary>
    public static IReadOnlyList<int> Order(StarSystem sys) =>
        sys.Bodies.Select((b, i) => (b, i)).Where(x => !x.b.IsStar && !x.b.IsSpace && x.b.Orbits < 0)
            .OrderBy(x => Zone(x.b.Class)).ThenBy(x => x.i).Select(x => x.i).ToList();

    /// <summary>The chart of one system as it stands this year, or null when there is no such system.</summary>
    public static SystemChart? For(World w, string systemId, HoldingCatalog? cat = null)
    {
        var g = w.Galaxy; if (g?.System(systemId) is not { } sys) return null;
        int year = Math.Max(0, w.Turn);
        var order = Order(sys); var rankOf = order.Select((i, k) => (i, k)).ToDictionary(x => x.i, x => x.k);
        var moonsOf = sys.Bodies.Select((b, i) => (b, i)).Where(x => x.b.Orbits >= 0 && x.b.Orbits < sys.Bodies.Count && !x.b.IsStar && !x.b.IsSpace)
            .GroupBy(x => x.b.Orbits).ToDictionary(gr => gr.Key, gr => gr.Select(x => x.i).OrderBy(i => i).ToList());

        // each orbit's radius: a step out from the last, and further when the last had moons to make room for
        var orbit = new Dictionary<int, double>(); double r = FirstOrbit;
        foreach (int i in order)
        {
            if (sys.Bodies[i].Class == Praetoria.Core.Galaxy.Body.Comet) continue;
            int moons = moonsOf.TryGetValue(i, out var ms) ? ms.Count : 0; double room = moons > 0 ? MoonOrbit + (moons - 1) * MoonStep : 0;
            r += room * 0.6; orbit[i] = r; r += Step + room * 0.6;
        }
        double last = orbit.Count > 0 ? orbit.Values.Max() : FirstOrbit, edge = last + Margin;

        var works = SystemWorks_In(w, systemId).GroupBy(x => x.Body).ToDictionary(gr => gr.Key, gr => gr.ToList());
        var rising = w.Projects.Where(p => p.Kind == Project.Work).Select(p => Systems.SystemWorks.Parse(p.TargetId)).Where(t => t.systemId == systemId).GroupBy(t => t.body).ToDictionary(gr => gr.Key, gr => gr.Count());
        var at = new Dictionary<int, (double x, double y)>(); var bodies = new List<ChartBody>();
        ChartBody Make(int i, int parent, int rank, double orb, double angle, double period, double ecc, double x, double y)
        {
            var b = sys.Bodies[i]; var list = works.TryGetValue(i, out var ws) ? ws : new List<BodyWork>();
            // the system's own world also carries what stands on the holding itself (its power plants, its mines): all of it is "built here"
            int onHolding = b.IsPrimary ? w.PlanetOf(systemId)?.Buildings.Count ?? 0 : 0, holdingRising = b.IsPrimary && w.PlanetOf(systemId) is { } ph ? w.Projects.Count(p => p.Kind == Project.Building && p.TargetId == ph.Id) : 0;
            return new ChartBody(i, b.Name, b.Class, b.Size, b.IsPrimary, parent, rank, Math.Round(orb, 2), Math.Round(angle, 4), period, ecc, Math.Round(x, 2), Math.Round(y, 2),
                b.Feature, b.Feature.Length > 0 ? cat?.Feature(b.Feature)?.Name ?? b.Feature.Replace('_', ' ') : "", b.Deposit,
                list.Count + onHolding, (rising.TryGetValue(i, out var rc) ? rc : 0) + holdingRising, cat != null && list.Any(x => cat.Work(x.DefId)?.IsMegastructure == true));
        }
        foreach (int i in order)
        {
            var b = sys.Bodies[i]; int rank = rankOf[i]; double a0 = Turn(systemId + "#" + i);
            if (b.Class == Praetoria.Core.Galaxy.Body.Comet)
            {
                // a long thin path that comes in past the inner worlds and goes out nearly to the lanes: where it is on it is by the year
                double ecc = CometEccentricity, semi = edge * 0.94 / (1 + ecc), period = Math.Round(40 + Hash("comet:" + systemId) % 30 + 0.0, 1), tilt = Turn("tilt:" + systemId + "#" + i);
                double m = a0 + 2 * Math.PI * year / period, rr = semi * (1 - ecc * ecc) / (1 + ecc * Math.Cos(m));
                double x = rr * Math.Cos(m + tilt), y = rr * Math.Sin(m + tilt); at[i] = (x, y);
                bodies.Add(Make(i, -1, rank, semi, m + tilt, period, ecc, x, y)); continue;
            }
            double orb = orbit[i], p = PeriodOf(rank), ang = AngleOf(systemId, i, rank, year);
            at[i] = (orb * Math.Cos(ang), orb * Math.Sin(ang));
            bodies.Add(Make(i, -1, rank, orb, ang, p, 0, at[i].x, at[i].y));
        }
        foreach (var (parent, moons) in moonsOf.OrderBy(kv => kv.Key))
        {
            if (!at.TryGetValue(parent, out var pp)) continue;   // a moon of something the chart does not draw
            for (int k = 0; k < moons.Count; k++)
            {
                int i = moons[k]; double orb = MoonOrbit + k * MoonStep, p = Math.Round(0.31 + 0.23 * k, 2), ang = Turn(systemId + "#" + i) + 2 * Math.PI * year / p;
                at[i] = (pp.x + orb * Math.Cos(ang), pp.y + orb * Math.Sin(ang));
                bodies.Add(Make(i, parent, rankOf.TryGetValue(parent, out var pr) ? pr : 0, orb, ang, p, 0, at[i].x, at[i].y));
            }
        }
        var sunAt = sys.Bodies.FindIndex(b => b.IsStar);

        // where the lanes leave: toward each neighbour as it lies on the galaxy map (north there is up here), spread a little where two lie one way
        var exits = g.Neighbors(systemId).Select(n => g.System(n)).Where(n => n != null).Select(n => (n: n!, a: Norm(Math.Atan2(-(n!.Y - sys.Y), n.X - sys.X)))).OrderBy(x => x.a).ThenBy(x => x.n.Id, StringComparer.Ordinal).ToList();
        var angles = Spread(exits.Select(x => x.a).ToList(), ExitGap);
        var exitList = new List<ChartExit>();
        for (int k = 0; k < exits.Count; k++)
        {
            var n = exits[k].n; var holder = w.PlanetHolder(n.Id) is { } hid ? w.House(hid) : null;
            exitList.Add(new ChartExit(n.Id, n.Name, Math.Round(angles[k], 4), Math.Round(edge * Math.Cos(angles[k]), 2), Math.Round(edge * Math.Sin(angles[k]), 2),
                holder?.Id ?? "", holder?.Name ?? "", holder?.AccentColor ?? GalaxyProjection.UnclaimedAccent, g.RingOf(n.Id), g.IsChokepoint(n.Id)));
        }

        // who is here
        string holderId = w.PlanetHolder(systemId) ?? ""; var holderHouse = holderId.Length > 0 ? w.House(holderId) : null; string player = w.Protagonist?.HouseId ?? "";
        int primary = sys.Bodies.FindIndex(b => b.IsPrimary); var primaryAt = primary >= 0 && at.TryGetValue(primary, out var pa) ? pa : (x: FirstOrbit, y: 0.0);
        double clear = primary >= 0 && moonsOf.TryGetValue(primary, out var pm) ? MoonOrbit + pm.Count * MoonStep : MoonOrbit;   // outside the world's moons
        var fleets = new List<ChartFleet>(); int inOrbit = 0, standing = 0;
        foreach (var f in GalaxyProjection.Fleets(w).Where(f => f.SystemId == systemId).OrderBy(f => f.FleetId, StringComparer.Ordinal))
        {
            bool laidUp = w.Fleets.TryGetValue(f.FleetId, out var real) && real.LaidUp; double x, y; int body = -1; string exitTo = "";
            if (f.Moving && g.Path(systemId, f.DestinationId) is { Count: > 1 } path && exitList.FirstOrDefault(e => e.SystemId == path[1]) is { } way)
            { exitTo = way.SystemId; x = way.X * 0.9; y = way.Y * 0.9; }   // under way: at the lane it is making for
            else if (holderId.Length > 0 && (f.OwnerHouseId == holderId || f.Office && primary >= 0) && primary >= 0)
            { double a = 0.9 + inOrbit * 1.05, rr = clear + 12 + inOrbit / 6 * 9; inOrbit++; body = primary; x = primaryAt.x + rr * Math.Cos(a); y = primaryAt.y + rr * Math.Sin(a); }
            else
            {
                // not the holder's: standing off in the outer system, toward the lane that leads to its own nearest world when there is one
                var home = w.Holdings.Values.Where(h => h.OwnerId == f.OwnerHouseId && h.IsPlanet && h.SystemId != systemId).Select(h => h.SystemId).OrderBy(s => g.Hops(systemId, s) is var d && d > 0 ? d : 999).ThenBy(s => s, StringComparer.Ordinal).FirstOrDefault();
                var toward = home != null && g.Path(systemId, home) is { Count: > 1 } hp ? exitList.FirstOrDefault(e => e.SystemId == hp[1]) : null;
                double a = (toward?.Angle ?? Turn("fleet:" + f.FleetId)) + 0.16 * (standing % 2 == 0 ? 1 : -1) * (1 + standing / 2); standing++;
                x = edge * 0.78 * Math.Cos(a); y = edge * 0.78 * Math.Sin(a);
            }
            fleets.Add(new ChartFleet(f.FleetId, f.Name, f.OwnerHouseId, f.OwnerHouseName, f.AccentColor, f.Strength, f.IsPlayer, f.Confidence, f.Moving, exitTo, laidUp, body, Math.Round(x, 2), Math.Round(y, 2)));
        }
        var posts = new List<ChartPost>(); int belt = sys.Bodies.FindIndex(b => b.Class == Praetoria.Core.Galaxy.Body.Belt), st = 0, cl = 0;
        foreach (var h in w.Holdings.Values.Where(h => h.SystemId == systemId && !h.IsPlanet).OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            var owner = w.House(h.OwnerId); double x, y; int body;
            if (h.IsClaim && belt >= 0 && orbit.TryGetValue(belt, out var br)) { double a = Turn("claim:" + h.Id) + cl++ * 0.4; body = belt; x = br * Math.Cos(a); y = br * Math.Sin(a); }
            else { double a = 3.6 + st * 0.95, rr = clear + 7 + st / 6 * 8; st++; body = primary; x = primaryAt.x + rr * Math.Cos(a); y = primaryAt.y + rr * Math.Sin(a); }
            posts.Add(new ChartPost(h.Id, h.Name, h.IsClaim ? Holding.Claim : h.Kind, h.OwnerId, owner?.Name ?? h.OwnerId, owner?.AccentColor ?? GalaxyProjection.UnclaimedAccent, body, Math.Round(x, 2), Math.Round(y, 2)));
        }

        return new SystemChart(sys.Id, sys.Name, g.Regions.FirstOrDefault(rg => rg.Id == sys.RegionId)?.Name ?? "", g.RingOf(sys.Id), g.Capital == sys.Id, holderId.Length > 0 && holderId == player,
            holderId, holderHouse?.Name ?? "", holderHouse?.AccentColor ?? GalaxyProjection.UnclaimedAccent, (int)(Hash(sys.Id) % 6), sunAt >= 0 && works.ContainsKey(sunAt), Math.Round(edge, 2), sys.Belt, year,
            bodies.OrderBy(b => b.Parent >= 0 ? 1 : 0).ThenBy(b => b.Rank).ThenBy(b => b.Index).ToList(), exitList, fleets, posts);
    }

    private static IEnumerable<BodyWork> SystemWorks_In(World w, string systemId) => Systems.SystemWorks.In(w, systemId);
    private static double Norm(double a) { a %= 2 * Math.PI; return a < 0 ? a + 2 * Math.PI : a; }

    /// <summary>Angles in rising order, moved apart so no two neighbours on the circle are nearer than the gap (as far as the circle has
    /// room: with too many for the gap they are spaced evenly). The order is kept.</summary>
    public static IReadOnlyList<double> Spread(IReadOnlyList<double> sorted, double gap)
    {
        int n = sorted.Count; if (n < 2) return sorted.ToList();
        if (n * gap >= 2 * Math.PI) return Enumerable.Range(0, n).Select(k => Norm(sorted[0] + k * 2 * Math.PI / n)).ToList();
        var a = sorted.ToList();
        for (int pass = 0; pass < 12; pass++)
        {
            bool moved = false;
            for (int k = 0; k < n; k++)
            {
                int next = (k + 1) % n; double d = a[next] - a[k] + (next == 0 ? 2 * Math.PI : 0);
                if (d < gap - 1e-9) { double push = (gap - d) / 2; a[k] -= push; a[next] += push; moved = true; }
            }
            if (!moved) break;
        }
        return a.Select(Norm).ToList();
    }
}
