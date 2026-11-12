using Praetoria.Core.Data;
using Praetoria.Core.Rng;
using Praetoria.Core.State;

namespace Praetoria.Core.Events;

/// <summary>A fired event with its resolved binding, ready to present to the player.</summary>
public sealed record FiredEvent(EventDef Def, Binding Binding);

/// <summary>An option as offered to the player: the choice plus whether its requirements are met.</summary>
public sealed record OfferedChoice(Choice Choice, bool Available);

/// <summary>
/// The heart of the simulation (BuildSpec §4). Each turn it gathers eligible events (roles
/// bindable + conditions hold), lets the Director pick one, and applies the chosen choice's
/// consequences back into State. Because a consequence can arm a flag/relationship that makes a
/// previously-impossible event eligible, unscripted storylines chain — the Milestone-1
/// acceptance property. The engine owns no Godot types and is driven entirely by data + RNG.
/// </summary>
public sealed class EventEngine
{
    private readonly ContentDatabase _content;
    private readonly Director _director;
    public IRng Rng { get; }

    public EventEngine(ContentDatabase content, IRng rng, Director? director = null)
    {
        _content = content;
        Rng = rng;
        _director = director ?? new Director();
    }

    /// <summary>
    /// All events that could fire right now, each paired with a concrete binding. An event is
    /// eligible iff: it isn't a spent non-repeatable, every role binds, and every condition holds.
    /// </summary>
    public List<(EventDef def, Binding binding)> GatherEligible(World world)
    {
        var result = new List<(EventDef, Binding)>();
        foreach (var def in _content.Events)
        {
            if (!def.Repeatable && world.HasFlag(EventFlags.Fired(def.Id))) continue;
            // the Academy is apart from the world (docs/ACADEMY.md): in session, only its own events; out of it, none of them
            if (world.InAcademy != def.Conditions.Any(c => c is AcademyCondition)) continue;

            var binding = Binder.TryBind(def, world, Rng);
            if (binding == null) continue;

            var ctx = new EvalContext(world, binding, Rng, _content);
            if (Binder.AllHold(def.Conditions, ctx))
                result.Add((def, binding));
        }
        return result;
    }

    /// <summary>Gather + Director-select a single event for this turn, or null if nothing is eligible.</summary>
    public FiredEvent? NextEvent(World world)
    {
        var pool = GatherEligible(world);
        var picked = _director.Select(pool, world, Rng);
        return picked is { } p ? new FiredEvent(p.def, p.binding) : null;
    }

    /// <summary>
    /// Director-select up to <paramref name="count"/> distinct events for a turn's Briefing
    /// (GDD §9). Each pick is removed (by id) before the next, so a turn surfaces a spread of
    /// reports rather than one event — the "briefing feed" the Court UI will render later.
    /// </summary>
    public List<FiredEvent> SelectBriefing(World world, int count)
    {
        var pool = GatherEligible(world);
        var result = new List<FiredEvent>();
        while (result.Count < count && pool.Count > 0)
        {
            var picked = _director.Select(pool, world, Rng);
            if (picked is not { } p) break;
            pool.RemoveAll(x => x.def.Id == p.def.Id);
            // One set-piece per briefing: dramatic peaks don't collide (GDD §15 "suppress nearby collisions").
            if (p.def.Tier == EventTier.Setpiece) pool.RemoveAll(x => x.def.Tier == EventTier.Setpiece);
            // At most one ambient beat while weightier reports wait: texture fills gaps, it doesn't crowd the feed.
            if (p.def.Tier == EventTier.Ambient && pool.Any(x => x.def.Tier != EventTier.Ambient)) pool.RemoveAll(x => x.def.Tier == EventTier.Ambient);
            result.Add(new FiredEvent(p.def, p.binding));
            MarkFired(p.def, world);   // presented is fired: a report read and set aside still happened (GDD §9 "reading is free")
        }
        // the lull clock: reset when the briefing carries anything above ambient, else lengthen
        world.WorldCounters[Director.LullCounter] = result.Any(f => f.Def.Tier != EventTier.Ambient) ? 0 : world.Counter(Director.LullCounter) + 1;
        return result;
    }

    /// <summary>The choices of a fired event, each flagged with whether its requirements are met (trait/skill/rank gates).</summary>
    public List<OfferedChoice> OfferChoices(FiredEvent fired, World world)
    {
        var ctx = new EvalContext(world, fired.Binding, Rng, _content);
        var list = new List<OfferedChoice>(fired.Def.Choices.Count);
        foreach (var choice in fired.Def.Choices)
            list.Add(new OfferedChoice(choice, Binder.AllHold(choice.Requirements, ctx)));
        return list;
    }

    /// <summary>True if the choice exists and its requirements (trait/skill/rank gates) hold.
    /// Non-throwing — the command layer uses this to decide availability before spending pools.</summary>
    public bool IsChoiceAvailable(FiredEvent fired, World world, string choiceId)
    {
        var choice = fired.Def.Choice(choiceId);
        if (choice == null) return false;
        var ctx = new EvalContext(world, fired.Binding, Rng, _content);
        return Binder.AllHold(choice.Requirements, ctx);
    }

    /// <summary>
    /// Apply a choice (BuildSpec §4 "Consequence applier"). Writes every effect into State,
    /// records the firing (for non-repeatable suppression and Director memory), and logs it.
    /// Throws if the choice id is unknown or its requirements aren't met — callers should only
    /// pass an available choice.
    /// </summary>
    public void Resolve(FiredEvent fired, World world, string choiceId)
    {
        var choice = fired.Def.Choice(choiceId)
            ?? throw new ArgumentException($"Event '{fired.Def.Id}' has no choice '{choiceId}'.");

        var ctx = new EvalContext(world, fired.Binding, Rng, _content);
        if (!Binder.AllHold(choice.Requirements, ctx))
            throw new InvalidOperationException($"Choice '{choiceId}' requirements are not met.");

        int before = world.History.Count;
        foreach (var effect in choice.Effects)
            effect.Apply(ctx);
        for (int i = before; i < world.History.Count; i++) world.History[i].EventId ??= fired.Def.Id;   // the chronicle knows which report wrote each line

        MarkFired(fired.Def, world);
    }

    private static void MarkFired(EventDef def, World world)
    {
        if (!def.Repeatable)
            world.WorldFlags.Add(EventFlags.Fired(def.Id));

        world.WorldCounters[Director.LastFiredCounter(def.Id)] = world.Turn;
        if (world.RecentEventIds.Count > 0 && world.RecentEventIds[^1] == def.Id) return;   // already recorded this briefing
        world.RecentEventIds.Add(def.Id);
        const int memory = 16;
        if (world.RecentEventIds.Count > memory)
            world.RecentEventIds.RemoveRange(0, world.RecentEventIds.Count - memory);
    }
}
