using Praetoria.Core.Events;

namespace Praetoria.Core.Crises;

/// <summary>One stage of a Great Crisis (GDD §16): a clock threshold, a flag, effects, and an optional engine behaviour.</summary>
public sealed class GreatCrisisStage
{
    public string Id { get; init; } = "";
    /// <summary>Clock value (tenths) at which the stage begins.</summary>
    public int At { get; init; }
    /// <summary>Engine behaviour on entry: "blocs", "bloc_war", "climax", "resolution", or "" for none.</summary>
    public string Behavior { get; init; } = "";
    public IReadOnlyList<IEffect> Effects { get; init; } = Array.Empty<IEffect>();
    public string Log { get; init; } = "";
}

/// <summary>A Great-Crisis archetype (GDD §11/§16, BuildSpec M6): the core game ships the human galaxy-war; later chapters add cosmic ones.</summary>
public sealed class GreatCrisisDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public int StartTurn { get; init; } = 10;
    /// <summary>Clock tenths added per turn at intensity 1.0, before turmoil.</summary>
    public int BaseRate { get; init; } = 10;
    public IReadOnlyDictionary<string, double> Intensity { get; init; } = new Dictionary<string, double> { ["off"] = 0, ["low"] = 0.5, ["default"] = 1.0, ["dominant"] = 2.0 };
    public IReadOnlyList<GreatCrisisStage> Stages { get; init; } = Array.Empty<GreatCrisisStage>();
    public double IntensityOf(string setting) => Intensity.TryGetValue(setting, out var v) ? v : 1.0;
}
