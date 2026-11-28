using Praetoria.Core.Presentation;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// A house that loses its last world (docs/GOALS.md, "A fallen house"). The review of the game found a house that had lost all its
/// land told so in one line of a year's summary, and offered nothing (docs/STEAM_REVIEW.md, item 6). At the year's end, the player's
/// house holding no world where it held one a year before raises <see cref="Flag"/>, marked as news, for the report
/// the_last_world_lost (content/events/fallen.json); it is lowered again after the year that follows. RNG-free.
/// </summary>
public static class FallenHouse
{
    public const string Flag = "the_last_world_lost", HeldKey = "worlds_held_last_year";

    public static void Tick(World w)
    {
        // news for the year it happens and the next, not for ever
        if (w.HasFlag(Flag) && w.Counter("mark:" + Flag) < w.Turn) { w.WorldFlags.Remove(Flag); w.WorldCounters.Remove("mark:" + Flag); }
        if (w.Protagonist is not { } p || w.House(p.HouseId) is not { } house) return;
        int now = Goals.WorldsHeld(w, house.Id), before = w.Counter(HeldKey);
        if (before > 0 && now == 0) { w.WorldFlags.Add(Flag); w.WorldCounters["mark:" + Flag] = w.Turn; }
        w.WorldCounters[HeldKey] = now;
    }
}
