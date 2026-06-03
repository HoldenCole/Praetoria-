namespace Praetoria.Core.State;

/// <summary>
/// Canonical resource keys — the five resources of the domain economy (GDD §17). These are
/// deliberately distinct from the per-turn action <see cref="Pool"/>s (§9): pools are spend-down
/// budgets that refill every turn, whereas resources are <em>accumulating wealth</em> banked in a
/// house <see cref="House.Treasury"/>. Note "influence" lives in both worlds — the pool is this
/// turn's political bandwidth; the resource is banked political capital (titles, standing). They
/// never share a container, so the string overlap is harmless.
/// </summary>
public static class Resource
{
    public const string Credits = "credits";     // liquid wealth — upkeep drain; "the one you run out of" (§17)
    public const string Materials = "materials"; // alloys — buildings, ships, fortifications
    public const string Manpower = "manpower";   // population — crews, garrisons; carries unrest
    public const string Influence = "influence"; // political capital — climbs, claims, marriages
    public const string Exotics = "exotics";     // strategic spice — megastructures, Crisis countermeasures

    public static readonly string[] All = { Credits, Materials, Manpower, Influence, Exotics };
}

/// <summary>
/// A bundle of the five resources (GDD §17). Used both as a held balance (a house treasury) and as
/// a delta (a per-turn yield, an upkeep bill, a building cost). Plain data, serialised with the
/// World. Mirrors <see cref="ActionPools"/> in shape so the two economies read the same way, but
/// these accumulate. Credits may go negative — insolvency is a deliberate tension lever (§17 "the
/// one you run out of"); the other four are clamped at zero by the systems that mutate them.
/// </summary>
public sealed class Resources
{
    public int Credits { get; set; }
    public int Materials { get; set; }
    public int Manpower { get; set; }
    public int Influence { get; set; }
    public int Exotics { get; set; }

    public int Get(string r) => r switch
    {
        Resource.Credits => Credits,
        Resource.Materials => Materials,
        Resource.Manpower => Manpower,
        Resource.Influence => Influence,
        Resource.Exotics => Exotics,
        _ => 0
    };

    public void Set(string r, int value)
    {
        switch (r)
        {
            case Resource.Credits: Credits = value; break;
            case Resource.Materials: Materials = value; break;
            case Resource.Manpower: Manpower = value; break;
            case Resource.Influence: Influence = value; break;
            case Resource.Exotics: Exotics = value; break;
        }
    }

    public void Add(string r, int delta) => Set(r, Get(r) + delta);

    /// <summary>Field-wise add another bundle into this one (apply a yield, pay a negated upkeep).</summary>
    public void Add(Resources delta)
    {
        foreach (var r in Resource.All) Add(r, delta.Get(r));
    }

    public bool CanAfford(IReadOnlyDictionary<string, int> cost)
    {
        foreach (var kv in cost)
            if (Get(kv.Key) < kv.Value) return false;
        return true;
    }

    /// <summary>True if this balance covers a cost bundle (every resource ≥ the cost).</summary>
    public bool CanAfford(Resources cost)
    {
        foreach (var r in Resource.All)
            if (Get(r) < cost.Get(r)) return false;
        return true;
    }

    public void Spend(IReadOnlyDictionary<string, int> cost)
    {
        foreach (var kv in cost) Add(kv.Key, -kv.Value);
    }

    public void Spend(Resources cost)
    {
        foreach (var r in Resource.All) Add(r, -cost.Get(r));
    }

    /// <summary>Clamp every resource at zero <em>except</em> Credits, which may stay negative so
    /// insolvency can drive its own consequences (GDD §17).</summary>
    public void ClampNonCredit()
    {
        if (Materials < 0) Materials = 0;
        if (Manpower < 0) Manpower = 0;
        if (Influence < 0) Influence = 0;
        if (Exotics < 0) Exotics = 0;
    }

    public bool IsEmpty()
    {
        foreach (var r in Resource.All) if (Get(r) != 0) return false;
        return true;
    }

    public Resources Clone() => new()
    {
        Credits = Credits, Materials = Materials, Manpower = Manpower,
        Influence = Influence, Exotics = Exotics
    };
}
