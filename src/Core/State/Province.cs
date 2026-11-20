namespace Praetoria.Core.State;

/// <summary>A province of a world (docs/PROVINCES.md): a part of a planet given over to one thing and held by one house, for life.
/// The first province of a world is its seat and is always the house's that holds the world, whatever <see cref="HolderId"/> says.</summary>
public sealed class Province
{
    public string Name { get; set; } = "";
    /// <summary>What it is given over to: a focus of provinces.json.</summary>
    public string Focus { get; set; } = "";
    public string HolderId { get; set; } = "";
    /// <summary>The year its holder was given it.</summary>
    public int Since { get; set; }
    /// <summary>1 to 3: what it yields is multiplied by it.</summary>
    public int Level { get; set; } = 1;
}

/// <summary>A demand that a holder step down (docs/PROVINCES.md): from a province of a world (<see cref="Province"/> 1 or 2), made by
/// the house that holds the world; or from the world itself (0), made by the leader over that house. It waits only when it is made
/// of the player; a rival answers at once.</summary>
public sealed class SeatDemand
{
    public string Id { get; set; } = "";
    public string HoldingId { get; set; } = "";
    public int Province { get; set; }
    public string ById { get; set; } = "";
    public string AgainstId { get; set; } = "";
    /// <summary>The house that is to have it.</summary>
    public string ForId { get; set; } = "";
    public int Turn { get; set; }
}
