namespace Praetoria.Core.State;

/// <summary>Canonical action-pool keys (GDD §9). Match the keys used in a Choice's cost map.</summary>
public static class Pool
{
    public const string Influence = "influence"; // plots, Senate maneuvers, favors
    public const string Treasury = "treasury";   // builds, fleet orders, bribes
    public const string Agents = "agents";       // advisor attention / concurrent schemes
}

/// <summary>
/// A party's finite per-turn action economy (GDD §9: "unlimited interaction per turn; limited
/// resources to act with"). The player gets a generous set; NPC houses act on lighter, abstracted
/// budgets (GDD §3). Pools regenerate each turn — they are spend-down, not accumulating wealth
/// (that's the §17 resource economy, Milestone 4). Plain data, serialised with the World.
/// </summary>
public sealed class ActionPools
{
    public int Influence { get; set; }
    public int Treasury { get; set; }
    public int Agents { get; set; }

    public int InfluenceRegen { get; set; }
    public int TreasuryRegen { get; set; }
    public int AgentsRegen { get; set; }

    /// <summary>Soft cap so unspent pools don't snowball across idle turns.</summary>
    public int Cap { get; set; } = 9;

    public int Get(string pool) => pool switch
    {
        Pool.Influence => Influence,
        Pool.Treasury => Treasury,
        Pool.Agents => Agents,
        _ => 0
    };

    private void Set(string pool, int value)
    {
        switch (pool)
        {
            case Pool.Influence: Influence = value; break;
            case Pool.Treasury: Treasury = value; break;
            case Pool.Agents: Agents = value; break;
        }
    }

    public bool CanAfford(IReadOnlyDictionary<string, int> cost)
    {
        foreach (var kv in cost)
            if (Get(kv.Key) < kv.Value) return false;
        return true;
    }

    public void Spend(IReadOnlyDictionary<string, int> cost)
    {
        foreach (var kv in cost)
            Set(kv.Key, Math.Max(0, Get(kv.Key) - kv.Value));
    }

    /// <summary>Top up each pool by its regen, clamped to <see cref="Cap"/>. Called each turn.</summary>
    public void Regenerate()
    {
        Influence = Math.Min(Cap, Influence + InfluenceRegen);
        Treasury = Math.Min(Cap, Treasury + TreasuryRegen);
        Agents = Math.Min(Cap, Agents + AgentsRegen);
    }

    public ActionPools Clone() => new()
    {
        Influence = Influence, Treasury = Treasury, Agents = Agents,
        InfluenceRegen = InfluenceRegen, TreasuryRegen = TreasuryRegen, AgentsRegen = AgentsRegen,
        Cap = Cap
    };

    /// <summary>A player-scale budget.</summary>
    public static ActionPools ForPlayer() => new()
    {
        Influence = 3, Treasury = 3, Agents = 2,
        InfluenceRegen = 3, TreasuryRegen = 3, AgentsRegen = 2
    };

    /// <summary>A lean, abstracted budget for an NPC house (GDD §3).</summary>
    public static ActionPools ForNpc() => new()
    {
        Influence = 2, Treasury = 0, Agents = 1,
        InfluenceRegen = 2, TreasuryRegen = 0, AgentsRegen = 1
    };
}
