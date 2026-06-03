namespace Praetoria.Core.Events;

/// <summary>
/// The prose for an event, kept in a separate file from logic (GDD §15 — the hard
/// architectural split that makes AI-assisted writing and localisation safe). Keyed by the
/// event id; choice prose keyed by choice id. Bodies may contain {role.field} tokens
/// (e.g. {rival.name}) substituted at render time against the live binding.
/// </summary>
public sealed class EventText
{
    public string Id { get; init; } = "";
    public string? Title { get; init; }
    public string Body { get; init; } = "";
    public IReadOnlyDictionary<string, string> Choices { get; init; } = new Dictionary<string, string>();
}
