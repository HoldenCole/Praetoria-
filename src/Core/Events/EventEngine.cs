using Praetoria.Core.Data;
using Praetoria.Core.Rng;
using Praetoria.Core.State;

namespace Praetoria.Core.Events;

/// <summary>A fired event with its resolved binding, ready to present to the player, and the version of its words it was drawn in
/// (0 is the text's own body; docs/EVENTS.md, "Versions").</summary>
public sealed record FiredEvent(EventDef Def, Binding Binding, int Version = 0);

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

    /// <summary>Told of every event that was eligible but held back because fewer than two of its answers were open (the events audit
    /// counts them, docs/TOOLS.md). Draws nothing and changes nothing.</summary>
    public Action<EventDef, Binding>? HeldForOneAnswer { get; set; }

    public EventEngine(ContentDatabase content, IRng rng, Director? director = null)
    {
        _content = content;
        Rng = rng;
        _director = director ?? new Director();
    }

    /// <summary>
    /// All events that could fire right now, each paired with a concrete binding. An event is
    /// eligible iff: it isn't a spent non-repeatable, it is not waiting out its time since it last came to this head, every role
    /// binds, every condition holds, and at least two of its answers are open (one, for an event that has only one).
    /// </summary>
    public List<(EventDef def, Binding binding)> GatherEligible(World world) => GatherEligible(world, null);

    /// <summary>The eligible events among those <paramref name="only"/> admits (asked before any role is bound, so the others draw no dice).</summary>
    public List<(EventDef def, Binding binding)> GatherEligible(World world, Func<EventDef, bool>? only)
    {
        var result = new List<(EventDef, Binding)>();
        foreach (var def in _content.Events)
        {
            if (only != null && !only(def)) continue;
            if (!def.Repeatable && world.HasFlag(EventFlags.Fired(def.Id))) continue;
            if (Waiting(def, world)) continue;   // it came to this head too lately (docs/EVENTS.md, "Waiting times")
            // the Academy is apart from the world (docs/ACADEMY.md): in session, only its own events; out of it, none of them
            if (world.InAcademy != def.Conditions.Any(c => c is AcademyCondition)) continue;

            var binding = Binder.TryBind(def, world, Rng);
            if (binding == null) continue;

            var ctx = new EvalContext(world, binding, Rng, _content);
            if (!Binder.AllHold(def.Conditions, ctx)) continue;
            // never fewer than two answers open: a card with one open answer is no choice at all (docs/EVENTS.md, "Two answers open")
            if (OpenAnswers(def, ctx, world) < Math.Min(2, def.Choices.Count)) { HeldForOneAnswer?.Invoke(def, binding); continue; }
            result.Add((def, binding));
        }
        return result;
    }

    /// <summary>Is this card waiting out its time since it last came to the head of the house now? (docs/EVENTS.md, "Waiting times")</summary>
    public static bool Waiting(EventDef def, World world) =>
        world.Cards.TryGetValue(CardSeen.Key(def.Id, world.ProtagonistId), out var seen) && seen.Times > 0 && world.Turn - seen.Turn < WaitAfter(def, seen.Times);

    /// <summary>How long a card waits after it has come to the same head <paramref name="times"/> times: its waiting time after the first, and
    /// each time it comes back the wait is that much longer again (twice after the second, three times after the third, and so on), so a card
    /// that is always eligible does not come back every decade of a long life (docs/EVENTS.md, "Waiting times").</summary>
    public static int WaitAfter(EventDef def, int times) => def.WaitYears * Math.Max(1, times);

    /// <summary>How many of the event's answers are open to the head under this binding: requirements met, and the pools it costs there
    /// to pay as the pools stand (a head with no pools can pay for nothing, as <see cref="Systems.TurnController.Offer"/> has it).</summary>
    public static int OpenAnswers(EventDef def, EvalContext ctx, World world)
    {
        world.Pools.TryGetValue(world.ProtagonistId, out var pools);
        int open = 0;
        foreach (var choice in def.Choices)
            if (Binder.AllHold(choice.Requirements, ctx) && (choice.Cost.Count == 0 || (pools != null && pools.CanAfford(choice.Cost)))) open++;
        return open;
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
        // while a card the house has not had, or news of this year or last, can come, the year's reports are of those: a card the house
        // has had comes again only in a year with nothing new to tell, and then alone (docs/EVENTS.md, "Waiting times")
        var had = Director.HadByTheHouse(world);
        if (pool.Any(x => New(x.def, world, had))) pool.RemoveAll(x => !New(x.def, world, had));
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
            int version = PickVersion(p.def, world);
            result.Add(new FiredEvent(p.def, p.binding, version));
            Remember(p.def, p.binding, world, version);
            MarkFired(p.def, world);   // presented is fired: a report read and set aside still happened (GDD §9 "reading is free")
            // after the year's first report, a card the house has had before comes only as news: a year with nothing new is a quieter year,
            // not one of old cards (docs/EVENTS.md, "Waiting times")
            if (result.Count == 1)
            {
                var now = Director.HadByTheHouse(world);
                pool.RemoveAll(x => !New(x.def, world, now));
            }
        }
        // the lull clock: reset when the briefing carries anything above ambient, else lengthen
        world.WorldCounters[Director.LullCounter] = result.Any(f => f.Def.Tier != EventTier.Ambient) ? 0 : world.Counter(Director.LullCounter) + 1;
        return result;
    }

    /// <summary>A card the house has not had under any head, or news of this year or last, or one whose author named its own waiting time:
    /// a card meant to come back after that time (a campaign each time the head stands, a censure each time one is moved) is not held to
    /// the rule that the new comes first.</summary>
    private static bool New(EventDef def, World world, Dictionary<string, int> had) => !had.ContainsKey(def.Id) || Director.IsNews(def, world) || def.Cooldown >= 0;

    /// <summary>A moment of the year (docs/EVENTS.md, "Moments in the year"): the head's own act has just raised the world flags in
    /// <paramref name="fresh"/>. Draws, from the reports that say they answer a moment and read one of those flags, one to add to the year's
    /// reports at once; none of those already in the year's reports. Null when none is eligible.</summary>
    public FiredEvent? DrawMoment(World world, IReadOnlySet<string> fresh, IEnumerable<string> already)
    {
        var skip = already.ToHashSet(StringComparer.Ordinal);
        var pool = GatherEligible(world, def => def.Moment && !skip.Contains(def.Id) && def.Conditions.OfType<WorldFlagCondition>().Any(c => c.Value && fresh.Contains(c.Flag)));
        if (_director.Select(pool, world, Rng) is not { } p) return null;
        int version = PickVersion(p.def, world);
        Remember(p.def, p.binding, world, version);
        MarkFired(p.def, world);
        return new FiredEvent(p.def, p.binding, version);
    }

    /// <summary>The version of its words a card is drawn in (docs/EVENTS.md, "Versions"): for a text with one body, its own; otherwise one
    /// chosen by the world's dice, never the one the card last came in, so a card that returns does not return word for word.</summary>
    private int PickVersion(EventDef def, World world)
    {
        int n = _content.Text(def.Id)?.VersionCount ?? 1;
        if (n <= 1) return 0;
        int last = LastVersion(def.Id, world);
        int pick = Rng.NextInt(0, last >= 0 ? n - 1 : n);
        return last >= 0 && pick >= last ? pick + 1 : pick;
    }

    /// <summary>The version the card last came in, to any head, or -1 if it has not come.</summary>
    private static int LastVersion(string eventId, World world)
    {
        string prefix = eventId + "|"; int turn = int.MinValue, version = -1;
        foreach (var kv in world.Cards)
            if (kv.Value.Times > 0 && kv.Key.StartsWith(prefix, StringComparison.Ordinal) && kv.Key.IndexOf('|', prefix.Length) < 0 && kv.Value.Turn >= turn)
            { turn = kv.Value.Turn; version = kv.Value.Version; }
        return version;
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

        var ctx = new EvalContext(world, fired.Binding, Rng, _content) { EventId = fired.Def.Id };
        if (!Binder.AllHold(choice.Requirements, ctx))
            throw new InvalidOperationException($"Choice '{choiceId}' requirements are not met.");

        string head = world.ProtagonistId;   // the head the card came to: a choice that hands the seat on does not make it the heir's card
        int before = world.History.Count;
        foreach (var effect in choice.Effects)
            effect.Apply(ctx);
        Systems.VoteMarks.Bargains(world, world.Protagonist?.HouseId);   // a vote given or promised in the choice is owed from now, so the whip count and the reminder have it (docs/events/votes.md, "Promises")
        for (int i = before; i < world.History.Count; i++) world.History[i].EventId ??= fired.Def.Id;   // the chronicle knows which report wrote each line

        Remember(fired.Def, fired.Binding, world, fired.Version, head);
        MarkFired(fired.Def, world);
    }

    /// <summary>The card has come to the head (docs/EVENTS.md, "Waiting times"): when, how often, in which words, and, for a role that binds
    /// each person once, who. Called as a card is drawn, and again as it is answered, which adds nothing for a card drawn this year.</summary>
    private static void Remember(EventDef def, Binding binding, World world, int version, string? headId = null)
    {
        string key = CardSeen.Key(def.Id, headId ?? world.ProtagonistId);
        if (!world.Cards.TryGetValue(key, out var seen)) world.Cards[key] = seen = new CardSeen();
        if (seen.Times == 0 || seen.Turn != world.Turn) { seen.Times++; seen.Turn = world.Turn; seen.Version = version; }
        foreach (var role in def.Roles)
            if (role.Once && binding.TryGet(role.Name, out var id)) world.CardsMet.TryAdd(CardSeen.Met(def.Id, role.Name, id), world.Turn);
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
