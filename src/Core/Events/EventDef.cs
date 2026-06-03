namespace Praetoria.Core.Events;

/// <summary>Event weight class — the Director's pacing tool (GDD §15 tiers).</summary>
public enum EventTier
{
    /// <summary>Small, frequent, light state-nudges — texture/life.</summary>
    Ambient,
    /// <summary>Medium, periodic, real choices with real costs — the bread-and-butter.</summary>
    Situation,
    /// <summary>Rare, weighty, multi-stage, memorable — deployed at dramatic peaks.</summary>
    Setpiece
}

/// <summary>
/// A role variable on an event (BuildSpec §4). The Binder fills it with a character satisfying
/// <see cref="Constraints"/>, evaluated with the candidate provisionally bound to this name —
/// so a constraint may reference "self" or any earlier-bound role (e.g. "rank higher than self").
/// </summary>
public sealed class RoleDef
{
    public string Name { get; init; } = "";
    public IReadOnlyList<ICondition> Constraints { get; init; } = Array.Empty<ICondition>();
}

/// <summary>
/// A choice within an event (BuildSpec §4, GDD §15 L3): gated by <see cref="Requirements"/>
/// (trait/skill/rank — a Ruthless character sees options a Just one doesn't), costs pools, and
/// writes <see cref="Effects"/> back into State.
/// </summary>
public sealed class Choice
{
    public string Id { get; init; } = "";
    public IReadOnlyList<ICondition> Requirements { get; init; } = Array.Empty<ICondition>();
    public IReadOnlyList<IEffect> Effects { get; init; } = Array.Empty<IEffect>();

    /// <summary>Pool costs (Influence/Treasury/Agents — GDD §9). Present for forward-compat; pools land in Milestone 2.</summary>
    public IReadOnlyDictionary<string, int> Cost { get; init; } = new Dictionary<string, int>();
}

/// <summary>
/// An authored event — pure logic, no prose (text lives in a separate file keyed by id,
/// GDD §15 logic/text split). Eligibility = all roles bindable AND all <see cref="Conditions"/>
/// hold. Non-repeatable events fire once per run (tracked via an engine flag).
/// </summary>
public sealed class EventDef
{
    public string Id { get; init; } = "";
    public EventTier Tier { get; init; } = EventTier.Situation;
    public double Weight { get; init; } = 1.0;
    public bool Repeatable { get; init; }
    public IReadOnlyList<RoleDef> Roles { get; init; } = Array.Empty<RoleDef>();
    public IReadOnlyList<ICondition> Conditions { get; init; } = Array.Empty<ICondition>();
    public IReadOnlyList<Choice> Choices { get; init; } = Array.Empty<Choice>();

    public Choice? Choice(string id)
    {
        foreach (var c in Choices) if (c.Id == id) return c;
        return null;
    }
}
