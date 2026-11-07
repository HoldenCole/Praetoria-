using Praetoria.Core.Rng;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>Tuning for the fleet layer, loaded from /content/fleets (data, not code).</summary>
public sealed class FleetRules
{
    public Resources ShipCost { get; init; } = new() { Materials = 6, Manpower = 3, Credits = 2 };
    public Resources UpkeepPerStrength { get; init; } = new() { Credits = 1 };
    public int ReadinessRecovery { get; init; } = 10;
    public int ReadinessDecayMoving { get; init; } = 5;
    public int IntelDecay { get; init; } = 15;
    public int ConfidenceInTerritory { get; init; } = 100;
    public int ConfidenceAdjacent { get; init; } = 65;
    public int MovePoolCost { get; init; } = 1;

    public static FleetRules Default { get; } = new();
}

/// <summary>
/// The fleet layer's per-turn rules (GDD §6 military pillar, §19 map behaviour): transit along lanes, readiness,
/// upkeep from the owning treasury, incursions (a fleet arriving in another house's territory), and the
/// protagonist house's <em>intel</em> picture of rival fleets. RNG-free and id-ordered: deterministic.
/// </summary>
public sealed class FleetSystem
{
    public const string IncursionsCounter = "incursions";
    public static string IncursionFlag(string houseId) => $"incursion:{houseId}";

    private readonly FleetRules _rules;
    public FleetSystem(FleetRules rules) => _rules = rules;
    public FleetRules Rules => _rules;

    /// <summary>Call once per turn, after the economy accrues.</summary>
    public void Tick(World w)
    {
        if (w.Galaxy == null) { UpdateIntel(w); return; }

        // 1. transit + readiness
        foreach (var f in w.Fleets.Values.OrderBy(f => f.Id, StringComparer.Ordinal))
        {
            if (f.Moving)
            {
                f.Eta -= 1;
                f.Readiness = Math.Max(0, f.Readiness - _rules.ReadinessDecayMoving);
                if (f.Eta <= 0)
                {
                    f.SystemId = f.DestinationId;
                    f.DestinationId = "";
                    f.Eta = 0;
                    Arrive(w, f);
                }
            }
            else
            {
                f.Readiness = Math.Min(100, f.Readiness + _rules.ReadinessRecovery);
            }
        }

        // 2. upkeep — ships eat (GDD §17 Credits "the one you run out of")
        foreach (var house in w.Houses.Values.OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            int strength = w.Fleets.Values.Where(f => f.OwnerId == house.Id).Sum(f => f.Strength);
            if (strength == 0) continue;
            foreach (var r in Resource.All)
                if (_rules.UpkeepPerStrength.Get(r) != 0)
                    house.Treasury.Add(r, -_rules.UpkeepPerStrength.Get(r) * strength);
            house.Treasury.ClampNonCredit();
        }

        UpdateIntel(w);
    }

    /// <summary>A fleet entering territory another house holds is an incursion: public, remembered, and the
    /// kind of thing the §7 fear machine and authored events read (`incursions` counter, `incursion:&lt;house&gt;` flag).</summary>
    private static void Arrive(World w, Fleet f)
    {
        string? owner = TerritoryOwner(w, f.SystemId);
        var sys = w.Galaxy!.System(f.SystemId);
        if (owner != null && owner != f.OwnerId)
        {
            w.WorldCounters[IncursionsCounter] = w.Counter(IncursionsCounter) + 1;
            w.WorldFlags.Add(IncursionFlag(owner));
            w.Log($"{f.Name} of {w.House(f.OwnerId)?.Name ?? f.OwnerId} arrives at {sys?.Name ?? f.SystemId} — {w.House(owner)?.Name ?? owner}'s space.");
        }
        else
        {
            w.Log($"{f.Name} arrives at {sys?.Name ?? f.SystemId}.");
        }
    }

    /// <summary>The house holding a system (by its holdings), or null for the frontier. Ties → ordinal id.</summary>
    public static string? TerritoryOwner(World w, string systemId)
    {
        var owners = w.Holdings.Values.Where(h => h.SystemId == systemId)
            .GroupBy(h => h.OwnerId).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key).ToList();
        return owners.Count == 0 ? null : owners[0];
    }

    /// <summary>Total fleet strength a house commands.</summary>
    public static int StrengthOf(World w, string houseId) =>
        w.Fleets.Values.Where(f => f.OwnerId == houseId).Sum(f => f.Strength);

    /// <summary>Systems the protagonist's house can see into: its territory and everything one lane out.</summary>
    public static HashSet<string> Watched(World w)
    {
        var set = new HashSet<string>();
        var house = w.Protagonist?.HouseId;
        if (w.Galaxy == null || house == null) return set;
        var own = w.Holdings.Values.Where(h => h.OwnerId == house && h.SystemId != "").Select(h => h.SystemId).Distinct().ToList();
        foreach (var s in own) { set.Add(s); foreach (var n in w.Galaxy.Neighbors(s)) set.Add(n); }
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == house && !f.Moving)) { set.Add(f.SystemId); foreach (var n in w.Galaxy.Neighbors(f.SystemId)) set.Add(n); }
        return set;
    }

    /// <summary>Refresh the protagonist house's picture of rival fleets (GDD §19 mixed visibility).</summary>
    public void UpdateIntel(World w)
    {
        var house = w.Protagonist?.HouseId;
        if (house == null) return;
        var ownSystems = w.Holdings.Values.Where(h => h.OwnerId == house).Select(h => h.SystemId).ToHashSet();
        var watched = Watched(w);

        foreach (var f in w.Fleets.Values.OrderBy(f => f.Id, StringComparer.Ordinal))
        {
            if (f.OwnerId == house) continue;
            string where = f.Moving ? f.DestinationId : f.SystemId;   // a fleet in transit is seen where it is bound
            bool seen = watched.Contains(f.SystemId) || (f.Moving && watched.Contains(f.DestinationId));
            if (seen)
            {
                w.Intel[f.Id] = new FleetIntel
                {
                    FleetId = f.Id, SystemId = f.Moving ? f.SystemId : where, Strength = f.Strength,
                    Confidence = ownSystems.Contains(f.SystemId) ? _rules.ConfidenceInTerritory : _rules.ConfidenceAdjacent,
                    TurnObserved = w.Turn
                };
            }
            else if (w.Intel.TryGetValue(f.Id, out var old))
            {
                old.Confidence = Math.Max(0, old.Confidence - _rules.IntelDecay);
            }
        }
        // fleets that no longer exist fall out of the picture
        foreach (var id in w.Intel.Keys.Where(id => !w.Fleets.ContainsKey(id)).ToList()) w.Intel.Remove(id);
    }
}
