using Praetoria.Core.Rng;
using Praetoria.Core.State;

namespace Praetoria.Core.Data;

/// <summary>Materialises a <see cref="Scenario"/> into a fresh <see cref="World"/> at turn 0,
/// seeding the RNG state so the run is reproducible (BuildSpec §1.5).</summary>
public static class WorldBuilder
{
    public static World FromScenario(Scenario scenario, ulong seed)
    {
        var world = new World
        {
            Turn = 0,
            RngState = seed,
            Era = scenario.Era,
            ProtagonistId = scenario.ProtagonistId
        };

        foreach (var h in scenario.Houses)
            world.Houses[h.Id] = h;

        foreach (var c in scenario.Characters)
        {
            world.Characters[c.Id] = c;
            if (world.Houses.TryGetValue(c.HouseId, out var house) && !house.Members.Contains(c.Id))
                house.Members.Add(c.Id);

            // The player gets a full action economy; NPC houses act on abstracted budgets (GDD §3, §9).
            world.Pools[c.Id] = c.Id == scenario.ProtagonistId
                ? ActionPools.ForPlayer()
                : ActionPools.ForNpc();
        }

        foreach (var r in scenario.Relationships)
            world.Relationships.Add(r);

        world.Galaxy = scenario.Galaxy;

        foreach (var holding in scenario.Holdings)
        {
            if (!world.Houses.ContainsKey(holding.OwnerId))
                throw new ContentException(
                    $"Holding '{holding.Id}' is owned by unknown house '{holding.OwnerId}'.");
            if (world.Galaxy != null && !string.IsNullOrEmpty(holding.SystemId) && world.Galaxy.System(holding.SystemId) == null)
                throw new ContentException(
                    $"Holding '{holding.Id}' sits in unknown system '{holding.SystemId}' (galaxy '{world.Galaxy.Id}').");
            if (holding.Culture.Length == 0 && world.Galaxy != null && holding.SystemId.Length > 0) holding.Culture = world.Galaxy.CultureOf(holding.SystemId);
            world.Holdings[holding.Id] = holding;
        }
        foreach (var b in scenario.Businesses)
        {
            if (!world.Houses.ContainsKey(b.OwnerId)) throw new ContentException($"Venture '{b.Id}' is owned by unknown house '{b.OwnerId}'.");
            if (world.Galaxy != null && b.SystemId.Length > 0 && world.Galaxy.System(b.SystemId) == null) throw new ContentException($"Venture '{b.Id}' sits in unknown system '{b.SystemId}'.");
            if (b.Name.Length == 0) b.Name = $"{world.Houses[b.OwnerId].Name.Replace("House ", "")} {b.Kind.Replace('_', ' ')} of {world.Galaxy?.System(b.SystemId)?.Name ?? b.SystemId}";
            world.Businesses[b.Id] = b;
        }
        // Every holding has a governor (GDD §13): the one named, else the house's senior living member.
        foreach (var holding in world.Holdings.Values)
        {
            if (holding.GovernorId.Length > 0 && world.Char(holding.GovernorId)?.Alive == true) continue;
            holding.GovernorId = world.Characters.Values.Where(c => c.Alive && c.HouseId == holding.OwnerId && c.Age >= Systems.DynastySystem.AgeOfMajority)
                .OrderByDescending(c => c.CareerRank).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault()?.Id ?? "";
        }

        foreach (var fleet in scenario.Fleets)
        {
            if (!world.Houses.ContainsKey(fleet.OwnerId))
                throw new ContentException($"Fleet '{fleet.Id}' is owned by unknown house '{fleet.OwnerId}'.");
            if (world.Galaxy == null)
                throw new ContentException($"Fleet '{fleet.Id}' needs a galaxy to sit in; the scenario names none.");
            if (world.Galaxy.System(fleet.SystemId) == null)
                throw new ContentException($"Fleet '{fleet.Id}' sits in unknown system '{fleet.SystemId}'.");
            world.Fleets[fleet.Id] = fleet;
        }

        // every navy sails with its tanks part full (GDD §17 fuel): ten helium-3 and two per hull, unless the scenario says more
        foreach (var house in world.Houses.Values)
        {
            int hulls = world.Fleets.Values.Where(f => f.OwnerId == house.Id).Sum(f => f.Strength);
            if (hulls >= 2) house.Treasury.Helium3 = Math.Max(house.Treasury.Helium3, 10 + 2 * hulls);   // a dozen turns or so before the first purchase
        }

        if (world.Protagonist == null)
            throw new ContentException($"Scenario protagonist '{scenario.ProtagonistId}' is not among its characters.");

        return world;
    }

    /// <summary>Construct an <see cref="IRng"/> positioned at the world's current RNG state.</summary>
    public static IRng RngFor(World world) => new SplitMix64Rng(world.RngState);
}
