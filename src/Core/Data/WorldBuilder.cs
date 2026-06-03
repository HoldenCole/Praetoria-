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
        }

        foreach (var r in scenario.Relationships)
            world.Relationships.Add(r);

        if (world.Protagonist == null)
            throw new ContentException($"Scenario protagonist '{scenario.ProtagonistId}' is not among its characters.");

        return world;
    }

    /// <summary>Construct an <see cref="IRng"/> positioned at the world's current RNG state.</summary>
    public static IRng RngFor(World world) => new SplitMix64Rng(world.RngState);
}
