using Praetoria.Core.Events;

namespace Praetoria.Core.Data;

/// <summary>
/// The loaded, validated content set (BuildSpec §2 /content). Events are logic; Texts are prose;
/// they are kept separate and joined only by id (GDD §15). This is what the engine reads from —
/// no content is hard-coded in Core (BuildSpec §7 "no hard-coded content in Core logic").
/// </summary>
public sealed class ContentDatabase
{
    public IReadOnlyList<EventDef> Events { get; }
    public IReadOnlyDictionary<string, EventText> Texts { get; }

    private readonly Dictionary<string, EventDef> _eventsById;

    public ContentDatabase(IReadOnlyList<EventDef> events, IReadOnlyDictionary<string, EventText> texts)
    {
        Events = events;
        Texts = texts;
        _eventsById = new Dictionary<string, EventDef>();
        foreach (var e in events) _eventsById[e.Id] = e;
    }

    public EventDef? Event(string id) => _eventsById.TryGetValue(id, out var e) ? e : null;
    public EventText? Text(string id) => Texts.TryGetValue(id, out var t) ? t : null;
}
