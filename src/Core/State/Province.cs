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
    /// <summary>What its holder has built in its slots: works of works.json. They go with the province.</summary>
    public List<string> Buildings { get; set; } = new();
    /// <summary>The levels of its buildings above the first: work id → level.</summary>
    public Dictionary<string, int> BuildingLevels { get; set; } = new();
    public int LevelOf(string workId) => BuildingLevels.TryGetValue(workId, out var v) ? Math.Max(1, v) : 1;
    /// <summary>What the house that holds the world asks of it: 0 forgiven, 1 customary, 2 heavy.</summary>
    public int Dues { get; set; } = 1;
    /// <summary>Its holder is refusing to pay.</summary>
    public bool Withheld { get; set; }
    /// <summary>Its levy has been sent to this war: it is not sent twice.</summary>
    public bool Levied { get; set; }
    /// <summary>Its levy was destroyed in this war: no other is sent until the peace.</summary>
    public bool LevyLost { get; set; }
    /// <summary>Its dues were commuted: nothing is owed until this year.</summary>
    public int EasedUntil { get; set; }
    /// <summary>Its holder and the house over it are at war over it; and, after their peace, the year until which it may not be asked for.</summary>
    public bool FoughtOver { get; set; }
    public int SettledUntil { get; set; }
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
