using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Rng;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>The three phases of a turn (GDD §9).</summary>
public enum TurnPhase { Idle, Briefing, Action, Resolve }

/// <summary>One report in the turn's briefing: a fired event plus whether the player has acted on it.</summary>
public sealed class BriefingItem
{
    public FiredEvent Fired { get; }
    public bool Resolved { get; internal set; }
    public BriefingItem(FiredEvent fired) => Fired = fired;
}

/// <summary>
/// Formalises the turn structure (GDD §9, BuildSpec §M2): <b>Briefing</b> (advisors present the
/// turn's reports — reading is free) → <b>Action</b> (the player spends finite pools answering
/// them, all via commands) → <b>Resolve</b> (pools regenerate, NPC houses act through the same
/// command bus, the turn advances). One shared RNG threads selection, binding, and AI, so a full
/// cycle reproduces exactly from a seed. Owns no Godot types — drives entirely from State.
/// </summary>
public sealed class TurnController
{
    public World World { get; }
    public EventEngine Engine { get; }
    public CommandExecutor Executor { get; } = new();
    public TurnPhase Phase { get; private set; } = TurnPhase.Idle;

    private readonly IRng _rng;
    private readonly NpcAi _npcAi = new();
    private readonly Economy? _economy;
    private readonly CrisisEngine? _crises;
    private readonly SphereSystem? _spheres;
    private readonly ProgressionSystem? _progression;
    private readonly DynastySystem _dynasty = new();   // always-on — the dynasty IS the game (GDD §14)
    private readonly int _briefingBudget;
    private readonly List<BriefingItem> _briefing = new();

    public IReadOnlyList<BriefingItem> Briefing => _briefing;

    /// <summary>The crisis system (GDD §16), or null when the content set defines no crises.</summary>
    public CrisisEngine? Crises => _crises;

    /// <summary>The power-balance system (GDD §7), or null when the content set defines no spheres.</summary>
    public SphereSystem? Spheres => _spheres;

    /// <summary>The progression system (GDD §13), or null when the content set defines no titles.</summary>
    public ProgressionSystem? Progression => _progression;

    /// <summary>The dynasty lifecycle (GDD §14) — always present (aging/death/birth/succession).</summary>
    public DynastySystem Dynasty => _dynasty;

    public TurnController(World world, ContentDatabase content, Director? director = null, int briefingBudget = 3)
    {
        World = world;
        _rng = WorldBuilder.RngFor(world);
        Engine = new EventEngine(content, _rng, director);
        _economy = content.Holdings.IsEmpty ? null : new Economy(content.Holdings);
        _crises = content.Crises.Count == 0 ? null : new CrisisEngine(content.Crises);
        _spheres = content.Spheres.IsEmpty ? null : new SphereSystem(content.Spheres);
        _progression = content.Titles.IsEmpty ? null : new ProgressionSystem(content.Titles);
        _briefingBudget = briefingBudget;
    }

    public ActionPools PlayerPools => World.PoolsFor(World.ProtagonistId);

    /// <summary>Snapshot the RNG position into the World (call before serialising a save).</summary>
    public void SyncRng() => World.RngState = _rng.State;

    /// <summary>Begin a turn: advance the counter, refill every actor's pools, and compile the
    /// briefing. Leaves the controller in the Action phase, ready for player choices.</summary>
    public IReadOnlyList<BriefingItem> BeginTurn()
    {
        World.Turn++;
        foreach (var pools in World.Pools.Values) pools.Regenerate();
        _economy?.Accrue(World);     // RNG-free, so determinism is preserved (GDD §17)
        _spheres?.Recompute(World);  // power-balance projection + threat/coalition (GDD §7), also RNG-free
        _progression?.Apply(World);  // title soft-lock instability (GDD §13), also RNG-free

        Phase = TurnPhase.Briefing;
        _briefing.Clear();
        foreach (var fired in Engine.SelectBriefing(World, _briefingBudget))
            _briefing.Add(new BriefingItem(fired));

        Phase = TurnPhase.Action;
        SyncRng();
        return _briefing;
    }

    /// <summary>The choices for a briefing item, flagged Available only if requirements hold AND the
    /// player can afford the pool cost (GDD §9 — reading is free, acting costs).</summary>
    public List<OfferedChoice> Offer(BriefingItem item)
    {
        var pools = PlayerPools;
        var list = new List<OfferedChoice>(item.Fired.Def.Choices.Count);
        foreach (var choice in item.Fired.Def.Choices)
        {
            bool ok = Engine.IsChoiceAvailable(item.Fired, World, choice.Id) && pools.CanAfford(choice.Cost);
            list.Add(new OfferedChoice(choice, ok));
        }
        return list;
    }

    /// <summary>Player answers a briefing item. Routed through the command bus so the spend and the
    /// consequence-write happen atomically and get logged. Returns false if it wasn't affordable/legal.</summary>
    public bool Resolve(BriefingItem item, string choiceId)
    {
        if (Phase != TurnPhase.Action)
            throw new InvalidOperationException("Choices can only be resolved during the Action phase.");

        var cmd = new ResolveChoiceCommand(Engine, item.Fired, choiceId, World.ProtagonistId);
        bool ok = Executor.TryExecute(cmd, Context());
        if (ok) item.Resolved = true;
        SyncRng();
        return ok;
    }

    /// <summary>End the turn: NPC houses act (through commands), then return to Idle. Pools are NOT
    /// refilled here — that happens at the next BeginTurn, so a turn's spend is felt before relief.</summary>
    public void EndTurn()
    {
        Phase = TurnPhase.Resolve;
        _npcAi.Act(Executor, Context(), _crises);   // NPCs act — and may author a crisis

        // Organic onset: the ripe-conditions roll (GDD §16 mixed origin). NPC-authored crises this
        // turn are already active, so they're excluded from the causable pool here.
        if (_crises != null)
        {
            var organic = _crises.RollOrganic(World, _rng);
            if (organic != null) _crises.Trigger(organic, World, _rng);
        }

        _dynasty.Tick(World, _rng);   // age, deaths→succession, births (GDD §14). RNG only where life turns.

        SyncRng();
        Phase = TurnPhase.Idle;
    }

    private CommandContext Context() => new(World, _rng);
}
