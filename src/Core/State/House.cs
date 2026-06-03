namespace Praetoria.Core.State;

/// <summary>
/// A dynasty (BuildSpec §3). The save-persistent entity (GDD §5) — characters die, the
/// House endures until no name-bearer remains (GDD §14 dynasty death). AccentColor drives
/// the reactive theme later (GDD §19); SphereInfluence feeds the power-balance system
/// (GDD §7). For Milestone 1 only identity + membership are exercised.
/// </summary>
public sealed class House
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>Hex accent (e.g. "#7A1F2B") — the single themeable house colour (GDD §19).</summary>
    public string AccentColor { get; set; } = "#888888";

    public List<string> Members { get; set; } = new();

    /// <summary>Per-sphere influence (GDD §7). Empty until Milestone 5; present so saves are forward-compatible.</summary>
    public Dictionary<string, int> SphereInfluence { get; set; } = new();
}
