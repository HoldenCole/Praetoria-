using Praetoria.Core.State;

namespace Praetoria.Core.Galaxy;

/// <summary>What the map shows for one system: territory (derived from holdings) and what sits there.</summary>
public sealed record MapNode(
    string SystemId, string Name, double X, double Y,
    string? OwnerHouseId, string? OwnerHouseName, string AccentColor,
    IReadOnlyList<string> HoldingIds, bool IsPlayerTerritory, bool Contested,
    string RegionId = "", string Culture = "", string PlanetClass = "terran", string? GovernorName = null, int Population = 0, int Unrest = 0,
    string? PlanetHoldingId = null, int Stations = 0, int Belt = 0, int Claims = 0, int Ventures = 0, int Prosperity = 50, string Ring = "middle", int Rebellion = 0, bool Capital = false,
    bool Chokepoint = false, IReadOnlyList<Body>? Bodies = null, int Lanes = 0, IReadOnlyDictionary<string, int>? Deposits = null);

public sealed record MapLane(string A, string B);

/// <summary>A region of the map: its systems, its centre, the house that holds most of its planets (if any), and how
/// restless it is (mean unrest of its populated worlds).</summary>
public sealed record MapRegion(string Id, string Name, string Culture, string Description, IReadOnlyList<string> SystemIds, double CX, double CY,
    string? DominantHouseId, string? DominantHouseName, string AccentColor, int Planets, int Unrest, string Ring = "middle", int Allegiance = 50, int Lawlessness = 20, int Rebellions = 0, bool FreeMarch = false);

/// <summary>The rings drawn: the galaxy's centre and the mean radius of each ring's systems (map units).</summary>
public sealed record MapRings(double CX, double CY, double Core, double Middle, double Rim);

/// <summary>A fleet marker. Own fleets are exact (confidence 100); rival fleets come from the house's intel —
/// last-known system, estimated strength, and how much to trust it (GDD §19 "reported intel with a confidence").</summary>
public sealed record MapFleet(
    string FleetId, string Name, string OwnerHouseId, string OwnerHouseName, string AccentColor,
    string SystemId, int Strength, int Readiness, bool Office, bool Moving, string DestinationId, int Eta,
    bool IsPlayer, int Confidence, int TurnObserved);

/// <summary>
/// The Godot-free projection the Galaxy view renders (BuildSpec §1 — presentation reads from Core, never
/// the reverse). Territory shading comes from holding ownership and the owning house's accent colour
/// (GDD §19 "territory shaded by house accent"). A system holding two houses' holdings is <em>contested</em>.
/// Deterministic: nodes in system-id order, lanes in file order.
/// </summary>
public static class GalaxyProjection
{
    public const string UnclaimedAccent = "#3A3F47";

    public static IReadOnlyList<MapNode> Nodes(World world)
    {
        var list = new List<MapNode>();
        if (world.Galaxy == null) return list;

        var playerHouse = world.Protagonist?.HouseId;
        foreach (var sys in world.Galaxy.Systems.Values.OrderBy(s => s.Id, StringComparer.Ordinal))
        {
            var holdings = world.Holdings.Values
                .Where(h => h.SystemId == sys.Id)
                .OrderBy(h => h.Id, StringComparer.Ordinal)
                .ToList();
            // the planet decides the system; stations are the smaller things minor houses live on and do not contest it
            var planets = holdings.Where(h => h.IsPlanet).ToList();
            var owners = planets.Select(h => h.OwnerId).Distinct().OrderBy(o => o, StringComparer.Ordinal).ToList();
            string? owner = owners.Count == 1 ? owners[0] : owners.Count > 1 ? MajorityOwner(planets) : null;
            var house = owner != null ? world.House(owner) : null;
            var planet = planets.FirstOrDefault(h => h.OwnerId == owner);

            list.Add(new MapNode(
                sys.Id, sys.Name, sys.X, sys.Y,
                owner, house?.Name, house?.AccentColor ?? UnclaimedAccent,
                holdings.Select(h => h.Id).ToList(),
                owner != null && owner == playerHouse,
                owners.Count > 1,
                sys.RegionId, world.Galaxy.CultureOf(sys.Id), sys.PlanetClass,
                planet != null && planet.GovernorId.Length > 0 ? world.Char(planet.GovernorId)?.Name : null,
                planet?.Population ?? 0, planet?.Unrest ?? 0, planet?.Id, holdings.Count(h => h.Kind == Holding.Station),
                sys.Belt, holdings.Count(h => h.IsClaim), world.Businesses.Values.Count(b => b.SystemId == sys.Id), planet?.Prosperity ?? 50,
                world.Galaxy.RingOf(sys.Id), planet?.Rebellion ?? 0, world.Galaxy.Capital == sys.Id,
                world.Galaxy.IsChokepoint(sys.Id), sys.Bodies, world.Galaxy.Neighbors(sys.Id).Count(), sys.Deposits));
        }
        return list;
    }

    private static string MajorityOwner(List<Holding> holdings) =>
        holdings.GroupBy(h => h.OwnerId)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.Ordinal)
                .First().Key;

    public static IReadOnlyList<MapRegion> Regions(World world)
    {
        var list = new List<MapRegion>();
        if (world.Galaxy == null) return list;
        var nodes = Nodes(world);
        foreach (var r in world.Galaxy.Regions)
        {
            var systems = world.Galaxy.SystemsIn(r.Id).ToList();
            if (systems.Count == 0) continue;
            var mine = nodes.Where(n => n.RegionId == r.Id).ToList();
            var dominant = mine.Where(n => n.OwnerHouseId != null).GroupBy(n => n.OwnerHouseId!).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).FirstOrDefault()?.Key;
            var house = dominant != null ? world.House(dominant) : null;
            var populated = mine.Where(n => n.Population > 0).ToList();
            list.Add(new MapRegion(r.Id, r.Name, r.Culture, r.Description, systems.Select(s => s.Id).ToList(),
                systems.Average(s => s.X), systems.Average(s => s.Y), dominant, house?.Name, Presentation.Palette.Region(world.Galaxy!.Regions.IndexOf(r)),
                mine.Count(n => n.PlanetHoldingId != null), populated.Count == 0 ? 0 : (int)populated.Average(n => n.Unrest),
                r.Ring, world.Regions.TryGetValue(r.Id, out var rs) ? rs.Allegiance : Math.Clamp(50 + world.Galaxy.RulesFor(r.Ring).Allegiance, 0, 100),
                rs?.Lawlessness ?? world.Galaxy.RulesFor(r.Ring).LawlessnessStart, mine.Count(n => n.Rebellion > 0), house?.Bloc == Systems.RegionSystem.FreeholdsBloc));
        }
        return list;
    }

    /// <summary>Centre and ring radii for drawing the three rings (null without a galaxy or rings).</summary>
    public static MapRings? Rings(World world)
    {
        var g = world.Galaxy; if (g == null || g.Systems.Count == 0) return null;
        double cx = g.Systems.Values.Average(s => s.X), cy = g.Systems.Values.Average(s => s.Y);
        double R(string ring) { var xs = g.Systems.Values.Where(s => g.RingOf(s.Id) == ring).ToList(); return xs.Count == 0 ? 0 : xs.Average(s => Math.Sqrt((s.X - cx) * (s.X - cx) + (s.Y - cy) * (s.Y - cy) / 0.38)); }
        return new MapRings(cx, cy, R(Ring.Core), R(Ring.Middle), R(Ring.Rim));
    }

    public static IReadOnlyList<MapLane> Lanes(World world) =>
        world.Galaxy == null
            ? Array.Empty<MapLane>()
            : world.Galaxy.Lanes.Select(l => new MapLane(l.A, l.B)).ToList();

    /// <summary>Fleet markers: the player's own fleets as they are; rivals' as the intel picture says.</summary>
    public static IReadOnlyList<MapFleet> Fleets(World world)
    {
        var list = new List<MapFleet>();
        var player = world.Protagonist?.HouseId;
        foreach (var f in world.Fleets.Values.OrderBy(f => f.Id, StringComparer.Ordinal))
        {
            var house = world.House(f.OwnerId);
            string accent = house?.AccentColor ?? UnclaimedAccent;
            if (f.OwnerId == player)
                list.Add(new MapFleet(f.Id, f.Name, f.OwnerId, house?.Name ?? f.OwnerId, accent, f.SystemId, f.Strength, f.Readiness, f.Office, f.Moving, f.DestinationId, f.Eta, true, 100, world.Turn));
            else if (world.Intel.TryGetValue(f.Id, out var intel) && intel.Confidence > 0)
                list.Add(new MapFleet(f.Id, f.Name, f.OwnerId, house?.Name ?? f.OwnerId, accent, intel.SystemId, intel.Strength, -1, f.Office, false, "", 0, false, intel.Confidence, intel.TurnObserved));
        }
        return list;
    }

    /// <summary>Systems a house holds territory in, in id order.</summary>
    public static IReadOnlyList<string> TerritoryOf(World world, string houseId) =>
        Nodes(world).Where(n => n.OwnerHouseId == houseId).Select(n => n.SystemId).ToList();
}
