using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Rng;
using Praetoria.Core.State;

namespace Praetoria.Tests;

/// <summary>Helpers for building small, precise worlds and content in-memory, so engine tests
/// don't depend on the authored Academy files (those get their own integration test).</summary>
internal static class TestContent
{
    public static IRng Rng(ulong seed = 1) => new SplitMix64Rng(seed);

    public static World ThreeCadetWorld()
    {
        var w = new World { ProtagonistId = "self" };
        w.Houses["h"] = new House { Id = "h", Name = "House Test" };
        foreach (var id in new[] { "self", "rivalA", "rivalB" })
        {
            w.Characters[id] = new Character { Id = id, Name = id, HouseId = "h", Alive = true };
            w.Houses["h"].Members.Add(id);
        }
        return w;
    }

    public static ContentDatabase Db(IEnumerable<EventDef> events, IEnumerable<EventText>? texts = null,
        HoldingCatalog? holdings = null)
    {
        var textMap = (texts ?? Array.Empty<EventText>()).ToDictionary(t => t.Id, t => t);
        return new ContentDatabase(events.ToList(), textMap, holdings);
    }

    /// <summary>A tiny domain catalog: one populated agri-world spec and a building that fits it.</summary>
    public static HoldingCatalog EconomyCatalog() => new(
        new[]
        {
            new HoldingSpec
            {
                Id = "agri", Name = "Agri-World",
                BaseYield = new Resources { Manpower = 3, Credits = 2 },
                Upkeep = new Resources { Credits = 1 },
                Slots = 2, PopGrowth = 2
            },
            new HoldingSpec
            {
                Id = "forge", Name = "Forge-World",
                BaseYield = new Resources { Materials = 4 },
                Upkeep = new Resources { Credits = 2 },
                Slots = 1, PopGrowth = 0
            }
        },
        new[]
        {
            new BuildingDef
            {
                Id = "farm", Name = "Farm", Requires = "agri",
                Cost = new Resources { Materials = 5 },
                Yield = new Resources { Manpower = 2, Credits = 1 }
            },
            // Two slot-fillers that fit any holding, so slot-exhaustion can be tested apart from
            // the no-duplicates rule.
            new BuildingDef { Id = "outpost", Name = "Outpost", Cost = new Resources { Credits = 2 } },
            new BuildingDef { Id = "depot", Name = "Depot", Cost = new Resources { Credits = 2 } }
        });

    /// <summary>A world with House Vega, a protagonist in it, and one agri-world barony it owns.</summary>
    public static World EconomyWorld(int credits = 10, int materials = 8)
    {
        var w = new World { ProtagonistId = "self" };
        w.Houses["vega"] = new House
        {
            Id = "vega", Name = "House Vega",
            Treasury = new Resources { Credits = credits, Materials = materials }
        };
        w.Characters["self"] = new Character { Id = "self", Name = "self", HouseId = "vega", Alive = true };
        w.Houses["vega"].Members.Add("self");
        w.Pools["self"] = ActionPools.ForPlayer();
        w.Holdings["barony"] = new Holding
        {
            Id = "barony", OwnerId = "vega", Name = "Barony",
            Specialization = "agri", Population = 20
        };
        return w;
    }

    public static EventEngine Engine(World w, ContentDatabase db) =>
        new(db, new SplitMix64Rng(w.RngState));
}
