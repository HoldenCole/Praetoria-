namespace Praetoria.Core.State;

/// <summary>
/// A crisis that is currently active in the world (GDD §16). Crises are not momentary events — they
/// persist, escalate (<see cref="Severity"/> climbs as cascades feed them), and are arrested by
/// dampers (severity falls; at zero the crisis resolves). Tracked on <see cref="World.Crises"/> and
/// serialised with the save. The matching <c>crisis:&lt;id&gt;:active</c> world flag lets ordinary
/// event/crisis content reference an active crisis through the existing flag vocabulary.
/// </summary>
public sealed class ActiveCrisis
{
    public string Id { get; set; } = "";
    public string Tier { get; set; } = "regional";

    /// <summary>Climbs when cascades re-trigger it; dampers reduce it. Resolves at &lt;= 0.</summary>
    public int Severity { get; set; } = 1;

    public int TurnStarted { get; set; }

    /// <summary>Who caused it: a character/house id, or "" for an organic (Director-rolled) onset.</summary>
    public string AuthorId { get; set; } = "";

    public bool Organic => string.IsNullOrEmpty(AuthorId);
}
