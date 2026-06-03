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

    public static ContentDatabase Db(IEnumerable<EventDef> events, IEnumerable<EventText>? texts = null)
    {
        var textMap = (texts ?? Array.Empty<EventText>()).ToDictionary(t => t.Id, t => t);
        return new ContentDatabase(events.ToList(), textMap);
    }

    public static EventEngine Engine(World w, ContentDatabase db) =>
        new(db, new SplitMix64Rng(w.RngState));
}
