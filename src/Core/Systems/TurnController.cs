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
    private readonly ContentDatabase _content;
    private readonly NpcAi _npcAi;
    private readonly Economy? _economy;
    private readonly CrisisEngine? _crises;
    private readonly SphereSystem? _spheres;
    private readonly ProgressionSystem? _progression;
    private readonly FleetSystem _fleets;
    private readonly HonorSystem _honors;
    private readonly GreatCrisisSystem _greatCrisis;
    private readonly FamilySystem _family = new();
    private readonly DynastySystem _dynasty;           // always-on — the dynasty IS the game (GDD §14)
    private readonly int _briefingBudget;
    private readonly List<BriefingItem> _briefing = new();

    public IReadOnlyList<BriefingItem> Briefing => _briefing;

    /// <summary>The domain economy (GDD §17), or null when the content set defines no holdings catalog.</summary>
    public Economy? Economy => _economy;

    /// <summary>The crisis system (GDD §16), or null when the content set defines no crises.</summary>
    public CrisisEngine? Crises => _crises;

    /// <summary>The power-balance system (GDD §7), or null when the content set defines no spheres.</summary>
    public SphereSystem? Spheres => _spheres;

    /// <summary>The progression system (GDD §13), or null when the content set defines no titles.</summary>
    public ProgressionSystem? Progression => _progression;

    /// <summary>The dynasty lifecycle (GDD §14) — always present (aging/death/birth/succession).</summary>
    public DynastySystem Dynasty => _dynasty;

    /// <summary>The fleet layer (GDD §6/§19): transit, upkeep, incursions, intel. A no-op without a galaxy.</summary>
    public FleetSystem Fleets => _fleets;

    /// <summary>Honorifics & Orders (GDD §13).</summary>
    public HonorSystem Honors => _honors;

    /// <summary>The Great-Crisis framework (GDD §16, M6): clock, stages, blocs, resolution.</summary>
    public GreatCrisisSystem GreatCrisis => _greatCrisis;

    public TurnController(World world, ContentDatabase content, Director? director = null, int briefingBudget = 3)
    {
        World = world;
        _content = content;
        _rng = WorldBuilder.RngFor(world);
        Engine = new EventEngine(content, _rng, director);
        _economy = content.Holdings.IsEmpty ? null : new Economy(content.Holdings);
        _crises = content.Crises.Count == 0 ? null : new CrisisEngine(content.Crises, content.Recovery);
        _spheres = content.Spheres.IsEmpty ? null : new SphereSystem(content.Spheres, world.Difficulty);
        _progression = content.Titles.IsEmpty ? null : new ProgressionSystem(content.Titles);
        _fleets = new FleetSystem(content.Fleets, content.Holdings, content.ShipClasses, content.Friction);
        _npcAi = new NpcAi { ShipClasses = content.ShipClasses, Titles = content.Titles };
        _dynasty = new DynastySystem { Content = content };
        _honors = new HonorSystem(content.Honors);
        _greatCrisis = new GreatCrisisSystem(content.GreatCrisis, _fleets);
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
        _fleets.Tick(World);         // fleets move, eat, arrive; the intel picture refreshes (GDD §19) — RNG-free
        _fleets.ResolveBattles(World, _rng);   // warring fleets that share a system fight (seeded roll)
        _fleets.ResolveConquests(World);       // an unopposed fleet in enemy space takes its holdings
        _fleets.UpdateIntel(World);            // ...and the picture after the fighting
        _spheres?.Recompute(World);  // power-balance projection + threat/coalition (GDD §7), also RNG-free
        _progression?.Apply(World);  // title soft-lock instability (GDD §13), also RNG-free
        _honors.Apply(World, _rng);  // investitures when a distinction's conditions first hold (GDD §13)
        _greatCrisis.Tick(World, _rng);   // the great clock advances; stages fire (GDD §16, M6)

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
        _npcAi.Act(Executor, Context(), _crises, _economy == null ? null : _content.Holdings, _fleets.Rules);   // NPCs act, invest, sail, and may author a crisis

        // Organic onset: the ripe-conditions roll (GDD §16 mixed origin). NPC-authored crises this
        // turn are already active, so they're excluded from the causable pool here.
        if (_crises != null)
        {
            var organic = _crises.RollOrganic(World, _rng, abstainWeight: 3.0 / Math.Max(0.25, World.Difficulty));
            if (organic != null) _crises.Trigger(organic, World, _rng);
            _crises.Decay(World, _rng);   // active crises wind down once their root cause eases (GDD §16)
            _crises.Recover(World);       // and with no crisis active, the realm's wounds close (data rules)
        }

        _dynasty.Tick(World, _rng);   // age, deaths→succession, births (GDD §14). RNG only where life turns.
        _family.Tick(World);          // kin with wills of their own (GDD §18) — RNG-free

        SyncRng();
        Phase = TurnPhase.Idle;
    }

    private CommandContext Context() => new(World, _rng);
}
