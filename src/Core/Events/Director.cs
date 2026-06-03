using Praetoria.Core.Rng;
using Praetoria.Core.State;

namespace Praetoria.Core.Events;

/// <summary>
/// The pacing meta-system (BuildSpec §4, GDD §15 L4). It does not decide *what is possible*
/// (that's eligibility) — it biases *what surfaces*, so runs feel story-shaped, not random:
///   • suppress events that fired very recently (breathing room, no category flooding);
///   • lift set-piece weight as the run advances (rising action);
///   • otherwise honour authored weights.
/// Selection is a single seeded weighted draw, so it's deterministic and testable.
/// </summary>
public sealed class Director
{
    private readonly int _suppressionWindow;
    private readonly double _suppressionFactor;

    public Director(int suppressionWindow = 3, double suppressionFactor = 0.15)
    {
        _suppressionWindow = suppressionWindow;
        _suppressionFactor = suppressionFactor;
    }

    public double WeightOf(EventDef def, World world)
    {
        double w = Math.Max(0.0001, def.Weight);

        // Rising action: set-pieces grow more likely the longer the run has gone.
        if (def.Tier == EventTier.Setpiece)
            w *= 1.0 + Math.Min(2.0, world.Turn * 0.15);

        // Suppress recent repeats (look at the tail of the recent-event memory).
        int from = Math.Max(0, world.RecentEventIds.Count - _suppressionWindow);
        for (int i = from; i < world.RecentEventIds.Count; i++)
            if (world.RecentEventIds[i] == def.Id) { w *= _suppressionFactor; break; }

        return w;
    }

    /// <summary>Weighted-random pick over the eligible pool, or null if the pool is empty.</summary>
    public (EventDef def, Binding binding)? Select(
        IReadOnlyList<(EventDef def, Binding binding)> pool, World world, IRng rng)
    {
        if (pool.Count == 0) return null;

        double total = 0;
        var weights = new double[pool.Count];
        for (int i = 0; i < pool.Count; i++)
        {
            weights[i] = WeightOf(pool[i].def, world);
            total += weights[i];
        }
        if (total <= 0) return pool[0];

        double roll = rng.NextDouble() * total;
        for (int i = 0; i < pool.Count; i++)
        {
            roll -= weights[i];
            if (roll < 0) return pool[i];
        }
        return pool[^1];
    }
}
