using Praetoria.Core.State;

namespace Praetoria.Core.Galaxy;

/// <summary>What the map shows for one system: territory (derived from holdings) and what sits there.</summary>
public sealed record MapNode(
    string SystemId, string Name, double X, double Y,
    string? OwnerHouseId, string? OwnerHouseName, string AccentColor,
    IReadOnlyList<string> HoldingIds, bool IsPlayerTerritory, bool Contested);

public sealed record MapLane(string A, string B);

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
            var owners = holdings.Select(h => h.OwnerId).Distinct().OrderBy(o => o, StringComparer.Ordinal).ToList();
            string? owner = owners.Count == 1 ? owners[0] : owners.Count > 1 ? MajorityOwner(holdings) : null;
            var house = owner != null ? world.House(owner) : null;

            list.Add(new MapNode(
                sys.Id, sys.Name, sys.X, sys.Y,
                owner, house?.Name, house?.AccentColor ?? UnclaimedAccent,
                holdings.Select(h => h.Id).ToList(),
                owner != null && owner == playerHouse,
                owners.Count > 1));
        }
        return list;
    }

    private static string MajorityOwner(List<Holding> holdings) =>
        holdings.GroupBy(h => h.OwnerId)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.Ordinal)
                .First().Key;

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
