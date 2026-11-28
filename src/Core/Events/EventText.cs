namespace Praetoria.Core.Events;

/// <summary>
/// The prose for an event, kept in a separate file from logic (GDD §15 — the hard
/// architectural split that makes AI-assisted writing and localisation safe). Keyed by the
/// event id; choice prose keyed by choice id. Bodies may contain {role.field} tokens
/// (e.g. {rival.name}) substituted at render time against the live binding.
/// A card that comes back may carry other versions of its words (<see cref="Versions"/>): the engine picks one each time the
/// card is drawn (<see cref="FiredEvent.Version"/>), so it does not come back word for word (docs/EVENTS.md, "Versions").
/// </summary>
public sealed class EventText
{
    public string Id { get; init; } = "";
    public string? Title { get; init; }
    public string Body { get; init; } = "";
    public IReadOnlyDictionary<string, string> Choices { get; init; } = new Dictionary<string, string>();
    /// <summary>The other versions of the words, after the text's own (which is version 0). Each has a body, and may have its own title
    /// and its own words for some of the choices; what a version leaves out is the text's own.</summary>
    public IReadOnlyList<TextVersion> Versions { get; init; } = Array.Empty<TextVersion>();

    /// <summary>How many versions of its words the card has: its own, and each in <see cref="Versions"/>.</summary>
    public int VersionCount => 1 + Versions.Count;

    private TextVersion? Of(int version) => version >= 1 && version <= Versions.Count ? Versions[version - 1] : null;

    /// <summary>The title in the version named (the text's own where that version has none).</summary>
    public string? TitleOf(int version) => Of(version) is { Title: { Length: > 0 } t } ? t : Title;
    /// <summary>The body in the version named (the text's own for 0, or a version that is not there).</summary>
    public string BodyOf(int version) => Of(version) is { Body: { Length: > 0 } b } ? b : Body;
    /// <summary>A choice's words in the version named, else the text's own; null when the text has none for it.</summary>
    public string? ChoiceOf(string choiceId, int version) =>
        Of(version) is { } v && v.Choices.TryGetValue(choiceId, out var own) && own.Length > 0 ? own
        : Choices.TryGetValue(choiceId, out var t) ? t : null;
}

/// <summary>One other version of a card's words (JSON: an entry of "versions" in a text entry).</summary>
public sealed class TextVersion
{
    public string? Title { get; init; }
    public string Body { get; init; } = "";
    public IReadOnlyDictionary<string, string> Choices { get; init; } = new Dictionary<string, string>();
}
