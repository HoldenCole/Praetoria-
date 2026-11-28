using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The player's house's treasury at the end of each of the last three years (docs/GOALS.md, "Before ruin"), so the warning before
/// ruin can see a treasury that falls year on year for reasons the forecast cannot see: suits lost, answers that cost, raids.
/// Kept in world counters, so a save keeps them; a world saved before they were kept simply has none yet. RNG-free.
/// </summary>
public static class TreasuryTrend
{
    /// <summary>"treasury_end:1" is last year's end, ":2" the year before, ":3" the one before that; "treasury_end:n" how many are kept.</summary>
    public const string Key = "treasury_end:";
    public const int Kept = 3;

    public static void Record(World w)
    {
        if (w.Protagonist is not { } p || w.House(p.HouseId) is not { } house) return;
        for (int i = Kept; i > 1; i--) w.WorldCounters[Key + i] = w.Counter(Key + (i - 1));
        w.WorldCounters[Key + 1] = house.Treasury.Credits;
        w.WorldCounters[Key + "n"] = Math.Min(Kept, w.Counter(Key + "n") + 1);
    }

    /// <summary>The ends kept, oldest first, or fewer than <see cref="Kept"/> while the saga is young.</summary>
    public static IReadOnlyList<int> Ends(World w)
    {
        int n = Math.Min(Kept, w.Counter(Key + "n"));
        var ends = new List<int>();
        for (int i = n; i >= 1; i--) ends.Add(w.Counter(Key + i));
        return ends;
    }
}
