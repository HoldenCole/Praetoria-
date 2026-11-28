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
    /// <summary>A life event about one person's milestone (a birth, a naming, a coming of age): the role binds each person at most once
    /// for this event, ever (JSON "once": true on the role). Kept in <see cref="State.World.CardsMet"/>.</summary>
    public bool Once { get; init; }
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
/// hold. Non-repeatable events fire once per run (tracked via an engine flag). A repeatable one waits
/// <see cref="WaitYears"/> before it can come again to the same head of the house (docs/EVENTS.md, "Waiting times").
/// </summary>
public sealed class EventDef
{
    /// <summary>The years a repeatable card waits before it can come again to the same head, when its JSON names no "cooldown". Measured
    /// with the events audit (docs/TOOLS.md, "The events audit"; docs/EVENTS.md, "Waiting times").</summary>
    public const int DefaultCooldown = 10;

    public string Id { get; init; } = "";
    public EventTier Tier { get; init; } = EventTier.Situation;
    public double Weight { get; init; } = 1.0;
    public bool Repeatable { get; init; }
    /// <summary>The waiting time in years the JSON names ("cooldown"), or -1 for the default. 0 lets a repeatable card come every year.</summary>
    public int Cooldown { get; init; } = -1;
    /// <summary>How many years must pass before this card can come again to the same head of the house: its own "cooldown", else
    /// <see cref="DefaultCooldown"/> for a repeatable card. A card that is not repeatable comes once in a game and never waits.</summary>
    public int WaitYears => !Repeatable ? 0 : Cooldown >= 0 ? Cooldown : DefaultCooldown;
    /// <summary>A report that answers a moment of the year (JSON "moment": true): when the head's own act in the year raises a world flag its
    /// conditions read (a wedding made by a match agreed), it is drawn at once into the year's reports, not as the next year opens after
    /// the year in brief has told the moment as done (docs/EVENTS.md, "Moments in the year").</summary>
    public bool Moment { get; init; }
    public IReadOnlyList<RoleDef> Roles { get; init; } = Array.Empty<RoleDef>();
    public IReadOnlyList<ICondition> Conditions { get; init; } = Array.Empty<ICondition>();
    public IReadOnlyList<Choice> Choices { get; init; } = Array.Empty<Choice>();

    /// <summary>Ambitions this event speaks to (GDD §18 "personal-story events"); the Director favours them for a
    /// protagonist who holds one. Empty = no bias.</summary>
    public IReadOnlyList<string> Ambitions { get; init; } = Array.Empty<string>();

    /// <summary>The painting the event opens on (one of img/scenes, docs/ART_DIRECTION.md), when its author named one.
    /// Empty = chosen by the words of its id and title (<see cref="Praetoria.Core.Presentation.SceneArt"/>).</summary>
    public string Scene { get; init; } = "";

    public Choice? Choice(string id)
    {
        foreach (var c in Choices) if (c.Id == id) return c;
        return null;
    }
}
