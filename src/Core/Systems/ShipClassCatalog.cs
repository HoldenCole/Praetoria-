using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>A hull class (GDD §6 fleet composition) — data from /content/fleets/ship_classes.json.</summary>
public sealed class ShipClassDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public int Attack { get; init; } = 1;
    public int Defense { get; init; } = 1;
    public Resources Cost { get; init; } = new();
    public Resources Upkeep { get; init; } = new();
    /// <summary>Minimum title rank of the building house (0 = anyone).</summary>
    public int MinTitleRank { get; init; }
    /// <summary>Specialization the laying-down holding must have, or null for any.</summary>
    public string? Requires { get; init; }
}

public sealed class ShipClassCatalog
{
    private readonly List<ShipClassDef> _defs;
    private readonly Dictionary<string, ShipClassDef> _byId;
    public ShipClassCatalog(IEnumerable<ShipClassDef> defs) { _defs = defs.ToList(); _byId = _defs.ToDictionary(d => d.Id); }
    public static ShipClassCatalog Empty { get; } = new(Array.Empty<ShipClassDef>());
    public IReadOnlyList<ShipClassDef> Defs => _defs;
    public ShipClassDef? ById(string id) => _byId.TryGetValue(id, out var d) ? d : null;
    public bool IsEmpty => _defs.Count == 0;

    /// <summary>A fallback class when the catalog lacks the rules' default (so fleets always have numbers).</summary>
    public static readonly ShipClassDef Generic = new() { Id = "hull", Name = "Hull", Attack = 2, Defense = 2, Cost = new Resources { Materials = 6, Manpower = 3, Credits = 2 }, Upkeep = new Resources { Credits = 1 } };
    public ShipClassDef Resolve(string id) => ById(id) ?? Generic;
}
