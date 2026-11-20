namespace Praetoria.Core.State;

/// <summary>Something built on a body of a solar system (docs/SYSTEMS.md): a colony on a moon, a mine in the belt, a fortress, a
/// megastructure round the sun. It belongs to the system, not to a house: whoever holds the system's world has it, and a house that
/// takes the world takes what was built round it. Plain data; the World is the save.</summary>
public sealed class BodyWork
{
    public string Id { get; set; } = "";
    public string SystemId { get; set; } = "";
    /// <summary>Which body: its index in the system's bodies.</summary>
    public int Body { get; set; }
    /// <summary>The work's definition (content/holdings/works.json).</summary>
    public string DefId { get; set; } = "";
    /// <summary>The year it was finished, and the house that built it.</summary>
    public int Turn { get; set; }
    public string BuiltBy { get; set; } = "";
}
