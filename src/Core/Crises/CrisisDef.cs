using Praetoria.Core.Events;

namespace Praetoria.Core.Crises;

/// <summary>
/// A damper (GDD §16): an option to arrest a crisis. Its <see cref="Availability"/> is the
/// mechanical expression of "the escalation you face is partly the bill for how you played" — a
/// damper unlocks only when accumulated state from prior choices permits it (high <c>goodwill</c>,
/// legitimacy, a standing army). Applying it runs <see cref="Effects"/> (reduce severity, un-arm a
/// gate) and reduces the crisis's severity by <see cref="Relief"/>. Both gates and effects reuse the
/// event-engine vocabulary, so dampers invent no new mechanisms.
/// </summary>
public sealed class DamperDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public IReadOnlyList<ICondition> Availability { get; init; } = Array.Empty<ICondition>();
    public IReadOnlyList<IEffect> Effects { get; init; } = Array.Empty<IEffect>();

    /// <summary>How much severity this damper removes when applied (default 1).</summary>
    public int Relief { get; init; } = 1;

    /// <summary>Optional action-pool cost to deploy the damper.</summary>
    public IReadOnlyDictionary<string, int> Cost { get; init; } = new Dictionary<string, int>();
}

/// <summary>
/// A gated, buildable crisis state (GDD §16) — NOT a random event. Its <see cref="Gates"/> are
/// preconditions that accumulate in world state; while any gate is un-cleared the crisis cannot
/// fire. Once every gate holds, the crisis is <em>causable</em> and may onset two ways (mixed
/// origin): organically (the engine rolls it from ripe conditions) or authored (an actor triggers
/// it deliberately because it serves their ambition). <see cref="OnTrigger"/> writes state — and
/// because those writes can clear <em>other</em> crises' gates, escalation cascades emerge rather
/// than being scripted. Pure data; gates/effects are the same vocabulary events use.
/// </summary>
public sealed class CrisisDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>Scale tier (GDD §16): personal | house | regional | imperial | existential.</summary>
    public string Tier { get; init; } = "regional";

    /// <summary>Selection weight for an organic (Director-rolled) onset.</summary>
    public double Weight { get; init; } = 1.0;

    /// <summary>If false, the crisis can't re-onset while already active (the common case).</summary>
    public bool Repeatable { get; init; }

    /// <summary>Severity the crisis onsets at; cascades that re-trigger it add this again.</summary>
    public int Severity { get; init; } = 1;

    /// <summary>Preconditions — ALL must hold for the crisis to be causable.</summary>
    public IReadOnlyList<ICondition> Gates { get; init; } = Array.Empty<ICondition>();

    /// <summary>State writes applied on onset. The engine of cascades (one crisis arms the next).</summary>
    public IReadOnlyList<IEffect> OnTrigger { get; init; } = Array.Empty<IEffect>();

    public IReadOnlyList<DamperDef> Dampers { get; init; } = Array.Empty<DamperDef>();
}
