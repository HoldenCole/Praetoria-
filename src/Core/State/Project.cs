namespace Praetoria.Core.State;

/// <summary>
/// Something being built (docs/ECONOMY_MARKET.md stage B "projects, not clicks"): a building on a holding, a venture being founded, a
/// venture being raised a level. Its cost is paid in yearly instalments over its build time; a year the instalment cannot be met, it
/// stalls. It may be paused, rushed for a premium, or abandoned for half of what was paid. Serialised with the World.
/// </summary>
public sealed class Project
{
    public const string Building = "building", Venture = "venture", Expansion = "expand";
    public string Id { get; set; } = "";
    public string OwnerId { get; set; } = "";
    /// <summary>The company paying for it (stage C), or "" when the house pays.</summary>
    public string CompanyId { get; set; } = "";
    public string Kind { get; set; } = Building;
    /// <summary>The building or venture definition.</summary>
    public string DefId { get; set; } = "";
    /// <summary>Where: a holding id (building), a system id (venture), a business id (expansion).</summary>
    public string TargetId { get; set; } = "";
    public string Name { get; set; } = "";
    public int TurnsTotal { get; set; } = 1;
    public int TurnsLeft { get; set; } = 1;
    /// <summary>The whole cost, and what has been paid of it.</summary>
    public Resources Cost { get; set; } = new();
    public Resources Paid { get; set; } = new();
    public bool Paused { get; set; }
    /// <summary>Last year's instalment could not be met.</summary>
    public bool Stalled { get; set; }
    public int Started { get; set; }
}

/// <summary>A venture offered to a house at a price, good until <see cref="Expires"/> (stage B): a distressed seller offering first
/// refusal to the holder of the world, or to a friend with the coin.</summary>
public sealed class VentureOffer
{
    public string Id { get; set; } = "";
    public string BusinessId { get; set; } = "";
    public string FromHouseId { get; set; } = "";
    public string ToHouseId { get; set; } = "";
    public int Price { get; set; }
    public int Expires { get; set; }
    public string Reason { get; set; } = "";
}
