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

    /// <summary>World counter the engine maintains: turns since anything above ambient fired. The Director lifts
    /// situations and set-pieces as a lull lengthens (GDD §15 L4 "inject a complication if things are quiet too long").</summary>
    public const string LullCounter = "lull";
    /// <summary>World counter: the turn an event last surfaced. Repeats within <see cref="RepeatWindowTurns"/> turns are suppressed.</summary>
    public static string LastFiredCounter(string eventId) => $"evt:{eventId}:turn";
    public const int RepeatWindowTurns = 4;
    /// <summary>A report read less than this many years ago is remembered, and is half as likely to be drawn again.</summary>
    public const int RememberedTurns = 10;
    public const double RememberedFactor = 0.5;
    public const double AmbitionBias = 1.6;
    /// <summary>How much likelier a report is while the thing that raised it is a year old or less.</summary>
    public const double FreshBias = 5.0;

    /// <summary>How many times each card has come to the player's house, under any head: the sum of <see cref="World.Cards"/> by event.</summary>
    public static Dictionary<string, int> HadByTheHouse(World world)
    {
        var had = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (key, seen) in world.Cards)
        {
            int bar = key.IndexOf('|'); if (bar <= 0 || seen.Times <= 0) continue;
            string id = key[..bar]; had[id] = had.GetValueOrDefault(id) + seen.Times;
        }
        return had;
    }

    public double WeightOf(EventDef def, World world) => WeightOf(def, world, 0);

    /// <summary>Is the report raised by something that happened this year or last (a world flag it reads, marked then)?</summary>
    public static bool IsNews(EventDef def, World world)
    {
        foreach (var c in def.Conditions.OfType<WorldFlagCondition>())
            if (c.Value && world.WorldCounters.TryGetValue("mark:" + c.Flag, out var at) && world.Turn - at <= 1) return true;
        return false;
    }

    /// <param name="had">How many times the card has come to the house before, under any head: a card the house has had is drawn the less
    /// readily, its weight divided by one more than that, squared unless it is news of this year or last (a quarter as readily after once,
    /// a ninth after twice; news half and a third), so the reports of a long game are more often new and news still comes (docs/EVENTS.md,
    /// "Waiting times"). A new head may still meet it once its waiting time is out.</param>
    public double WeightOf(EventDef def, World world, int had)
    {
        double w = Math.Max(0.0001, def.Weight);
        if (had > 0 && def.Cooldown < 0) w /= IsNews(def, world) ? 1 + had : (1 + had) * (1 + had);   // a card that names its own wait comes as often as its author meant

        // Rising action: set-pieces grow more likely the longer the run has gone.
        if (def.Tier == EventTier.Setpiece)
            w *= 1.0 + Math.Min(2.0, world.Turn * 0.15);

        // Personal story: events that speak to the protagonist's ambition come to them.
        var ambition = world.Protagonist?.Ambition;
        if (ambition != null && def.Ambitions.Count > 0)
            w *= def.Ambitions.Contains(ambition) ? AmbitionBias : 0.8;

        // News comes while it is news: a report raised by something that happened this year or last is drawn ahead of the rest,
        // so the card for an election does not come two years after the count (docs/PLAYTHROUGH.md, Easy, P78).
        if (IsNews(def, world)) w *= FreshBias;

        // A lull breeds complications: weight above-ambient events up the longer nothing has happened.
        if (def.Tier != EventTier.Ambient)
            w *= 1.0 + Math.Min(1.5, world.Counter(LullCounter) * 0.25);

        // Suppress recent repeats: by turn (a report seen four turns ago is still fresh in mind) and by the tail of the
        // recent-event memory (within a briefing).
        if (world.WorldCounters.TryGetValue(LastFiredCounter(def.Id), out var last) && world.Turn - last < RepeatWindowTurns) w *= _suppressionFactor;
        else if (world.WorldCounters.TryGetValue(LastFiredCounter(def.Id), out var seen) && world.Turn - seen < RememberedTurns) w *= RememberedFactor;   // read within the decade: it comes less readily (docs/PLAYTHROUGH.md, Easy: "the same card with the same words")
        else
        {
            int from = Math.Max(0, world.RecentEventIds.Count - _suppressionWindow);
            for (int i = from; i < world.RecentEventIds.Count; i++)
                if (world.RecentEventIds[i] == def.Id) { w *= _suppressionFactor; break; }
        }

        return w;
    }

    /// <summary>Weighted-random pick over the eligible pool, or null if the pool is empty.</summary>
    public (EventDef def, Binding binding)? Select(
        IReadOnlyList<(EventDef def, Binding binding)> pool, World world, IRng rng)
    {
        if (pool.Count == 0) return null;

        double total = 0;
        var weights = new double[pool.Count];
        var had = HadByTheHouse(world);
        for (int i = 0; i < pool.Count; i++)
        {
            weights[i] = WeightOf(pool[i].def, world, had.GetValueOrDefault(pool[i].def.Id));
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
