using Praetoria.Core.Spheres;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The power-balance system (GDD §7) — the strategic counterweight to raw ascent. Imperial power is
/// divided among spheres/estates; each house holds influence in each, and <b>total influence in a
/// sphere is shared</b> (your share is someone else's loss). Influence is <em>derived</em> from the
/// careers of a house's members: a Vega Grand Admiral spikes the family's Navy share. From the share
/// distribution the system computes a house's <b>threat score</b> (sphere-dominance + holdings +
/// recent seizures) and, when the protagonist's house grows over-mighty, accumulates
/// <c>coalition_pressure</c> — the seed a coalition crisis (§16) reads. Pure, deterministic, RNG-free.
/// </summary>
public sealed class SphereSystem
{
    /// <summary>Threat at/above which rivals begin to coordinate (scaled by difficulty).</summary>
    public const int CoalitionThreshold = 25;

    /// <summary>Ceiling on accumulated coalition pressure, so it can't ratchet forever (a coalition
    /// crisis discharges it, letting the crisis then wind down — GDD §7/§16).</summary>
    public const int MaxCoalitionPressure = 6;

    private readonly SphereCatalog _catalog;
    private readonly double _difficulty;

    public SphereSystem(SphereCatalog catalog, double difficulty = 1.0)
    {
        _catalog = catalog;
        _difficulty = difficulty <= 0 ? 1.0 : difficulty;
    }

    /// <summary>Recompute every house's sphere influence from member careers, then update the
    /// protagonist house's threat and the coalition pressure against it. Call once per turn.</summary>
    public void Recompute(World w)
    {
        if (_catalog.IsEmpty) return;

        foreach (var house in w.Houses.Values.OrderBy(h => h.Id, StringComparer.Ordinal))
            foreach (var sphere in _catalog.Defs)
                house.SphereInfluence[sphere.Id] = DerivedInfluence(w, house, sphere);

        BridgeToPlayerCounters(w);

        var pHouse = w.Protagonist?.HouseId;
        int threat = pHouse != null ? Threat(w, pHouse) : 0;
        w.WorldCounters["threat"] = threat;

        // Coalition pressure builds while the house is over-mighty and ebbs when it isn't — capped, so
        // it can't ratchet unboundedly, and a coalition crisis (which discharges it on onset) can wind
        // back down once the pressure is spent.
        int effectiveThreshold = (int)Math.Round(CoalitionThreshold / _difficulty);
        int pressure = w.Counter("coalition_pressure") + (threat >= effectiveThreshold ? 1 : -1);
        pressure = Math.Clamp(pressure, 0, MaxCoalitionPressure);
        w.WorldCounters["coalition_pressure"] = pressure;
        if (threat >= effectiveThreshold) w.WorldFlags.Add("coalition_forming");
        else if (pressure == 0) w.WorldFlags.Remove("coalition_forming");
    }

    /// <summary>
    /// Bridge the protagonist house's career-derived sphere influence into the player-facing
    /// <c>{sphere}_influence</c> world counters the coalition event chain reads (e.g. <c>navy_influence</c>).
    /// Only the <em>change</em> since last turn is applied, so the events' own cultivation increments
    /// (a player building influence through choices) accumulate on top of the structural base rather
    /// than being clobbered. So <c>navy_influence</c> = career-derived structural power + cultivated.
    /// House.SphereInfluence stays the authoritative structural value (threat reads it).
    /// </summary>
    private void BridgeToPlayerCounters(World w)
    {
        var prot = w.Protagonist;
        if (prot == null || !w.Houses.TryGetValue(prot.HouseId, out var house)) return;

        foreach (var sphere in _catalog.Defs)
        {
            int structural = house.SphereInfluence.GetValueOrDefault(sphere.Id);
            string counterKey = sphere.Id + "_influence";          // navy_influence, treasury_influence, ...
            string appliedKey = "sphere_applied:" + sphere.Id;     // bookkeeping: structural already folded in
            int applied = w.Counter(appliedKey);
            if (structural != applied)
            {
                w.WorldCounters[counterKey] = w.Counter(counterKey) + (structural - applied);
                w.WorldCounters[appliedKey] = structural;
            }
        }
    }

    /// <summary>A house's influence in a sphere = the summed career ranks of its living members on
    /// that sphere's feeder track (GDD §7 "your career feeds your house's sphere-influence").</summary>
    private static int DerivedInfluence(World w, House house, SphereDef sphere)
    {
        int sum = 0;
        foreach (var id in house.Members)
        {
            var c = w.Char(id);
            if (c is { Alive: true } && c.CareerTrack == sphere.CareerTrack)
                sum += c.CareerRank;
        }
        return sum;
    }

    public int Influence(World w, string houseId, string sphereId) =>
        w.House(houseId)?.SphereInfluence.GetValueOrDefault(sphereId) ?? 0;

    public int TotalInfluence(World w, string sphereId)
    {
        int total = 0;
        foreach (var h in w.Houses.Values) total += h.SphereInfluence.GetValueOrDefault(sphereId);
        return total;
    }

    /// <summary>A house's percent share of a sphere (0 if the sphere is empty galaxy-wide).</summary>
    public double Share(World w, string houseId, string sphereId)
    {
        int total = TotalInfluence(w, sphereId);
        return total > 0 ? 100.0 * Influence(w, houseId, sphereId) / total : 0;
    }

    /// <summary>
    /// Threat score (GDD §7 / §16): how alarming this house looks to its rivals. Sums each sphere's
    /// share <em>above an even split</em> (dominance), plus a factor for holdings beyond the first and
    /// recent seizures. Over-concentration in any estate is what frightens the galaxy.
    /// </summary>
    public int Threat(World w, string houseId)
    {
        int houses = Math.Max(1, w.Houses.Count);
        double even = 100.0 / houses;

        double dominance = 0;
        foreach (var sphere in _catalog.Defs)
            dominance += Math.Max(0, Share(w, houseId, sphere.Id) - even);

        int holdingsFactor = Math.Max(0, w.HoldingsOf(houseId).Count() - 1) * 5;
        int seizures = w.Counter("seizures") * 10;   // a hook for the military layer to feed later
        return (int)Math.Round(dominance) + holdingsFactor + seizures;
    }
}
