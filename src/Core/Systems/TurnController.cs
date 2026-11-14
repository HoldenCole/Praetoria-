using Praetoria.Core.Diagnostics;
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
    private readonly RegionSystem _regions;
    private readonly PirateSystem _pirates = new();
    private readonly CrisisEngine? _crises;
    private readonly SphereSystem? _spheres;
    private readonly ProgressionSystem? _progression;
    private readonly FleetSystem _fleets; private readonly NationSystem _nations; private readonly AcademySystem _academy; private readonly MarketSystem _market; private readonly VentureSystem? _ventures; private readonly CompanySystem? _companies; private readonly OrderSystem _orders;
    private readonly HonorSystem _honors;
    private readonly GreatCrisisSystem _greatCrisis;
    private readonly FamilySystem _family = new();
    private readonly AmbitionSystem _ambitions;        // what the people of the house are after, and what they ask of the head (docs/AMBITIONS.md)
    public AmbitionSystem Ambitions => _ambitions;
    private readonly HouseholdSystem _household;       // found family, and the duties the head hands out (docs/HOUSEHOLD.md)
    private readonly DynastySystem _dynasty;           // always-on — the dynasty IS the game (GDD §14)
    private readonly int _briefingBudget;
    private readonly List<BriefingItem> _briefing = new();

    public IReadOnlyList<BriefingItem> Briefing => _briefing;

    /// <summary>The domain economy (GDD §17), or null when the content set defines no holdings catalog.</summary>
    public Economy? Economy => _economy;
    /// <summary>The rings of the galaxy as behaviour (GDD §19): allegiance, lawlessness, rebellion, secession.</summary>
    public RegionSystem Regions => _regions;

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
    public NationSystem Nations => _nations;
    public AcademySystem Academy => _academy;
    public MarketSystem Market => _market;
    public VentureSystem? Ventures => _ventures;
    /// <summary>The Exchange: companies, shares, the yearly share prices (docs/ECONOMY_MARKET.md stage C).</summary>
    public CompanySystem? Companies => _companies;
    /// <summary>The three orders of the Republic: standing, sway, patrons and clients (docs/REPUBLIC.md stage 3).</summary>
    public OrderSystem Orders => _orders;

    /// <summary>Honorifics & Orders (GDD §13).</summary>
    public HonorSystem Honors => _honors;

    /// <summary>The Great-Crisis framework (GDD §16, M6): clock, stages, blocs, resolution.</summary>
    public GreatCrisisSystem GreatCrisis => _greatCrisis;
    public SenateSystem Senate => _senate;
    private readonly SenateSystem _senate;

    public TurnController(World world, ContentDatabase content, Director? director = null, int briefingBudget = 3)
    {
        World = world;
        _content = content;
        _rng = WorldBuilder.RngFor(world);
        Engine = new EventEngine(content, _rng, director);
        _economy = content.Holdings.IsEmpty ? null : new Economy(content.Holdings, content.Businesses);
        _regions = new RegionSystem(content.Names);
        _crises = content.Crises.Count == 0 ? null : new CrisisEngine(content.Crises, content.Recovery);
        _spheres = content.Spheres.IsEmpty ? null : new SphereSystem(content.Spheres, world.Difficulty, content.Offices);
        _progression = content.Titles.IsEmpty ? null : new ProgressionSystem(content.Titles);
        _fleets = new FleetSystem(content.Fleets, content.Holdings, content.ShipClasses, content.Friction);
        _senate = new SenateSystem(content.Motions, content.Titles, _fleets, content.Offices) { Policies = content.Policies, Nations = content.Nations };
        _nations = new NationSystem(content.Nations, _fleets); if (world.Turn == 0) NationSystem.Seed(world, content.Nations);   // the nations beyond the Reach (docs/FOREIGN_NATIONS.md)
        if (world.Turn == 0 && world.Laws.Count == 0) PolicySystem.ApplyCultures(world, content.Policies);   // every house begins with its culture's laws (GDD §19)
        FactionSystem.Policies = content.Policies;
        if (world.Turn == 0 && world.Factions.Count == 0) FactionSystem.Seed(world, content.Parties, id => _senate.Weight(world, id));   // the four parties of the Republic (docs/REPUBLIC.md)
        _fleets.Policies = content.Policies; _regions.Policies = content.Policies; if (_economy != null) _economy.Policies = content.Policies;
        _academy = new AcademySystem(content.Academy, content.Names);
        _ventures = _economy != null ? new VentureSystem(_economy, content.Businesses) : null;   // ventures as assets: value, bids, asks, sales (stage B)
        _market = new MarketSystem(content.Trade, _economy, _fleets, content.Nations); _market.Seed(world);   // the Reach's exchanges open at base prices (docs/ECONOMY_MARKET.md)
        _companies = _economy != null && !content.Exchange.IsEmpty ? new CompanySystem(_economy, content.Businesses, content.Exchange, content.Trade) { Nations = content.Nations } : null;   // the Exchange (stage C)
        if (_companies != null && !world.InAcademy) _companies.Seed(world);
        _orders = new OrderSystem(_economy, _fleets, content.Titles, content.Offices, content.Market); if (!world.InAcademy && world.Houses.Values.All(h => h.Orders.Count == 0)) _orders.Tick(world);
        _senate.Exchange = _companies; if (_companies != null) _companies.Offices = content.Offices;
        _npcAi = new NpcAi { ShipClasses = content.ShipClasses, Titles = content.Titles, Policies = content.Policies, Nations = content.Nations, Trade = content.Trade, VentureTrade = _ventures, Exchange = _companies, Orders = _orders, HoldingCatalogForFounding = content.Holdings };
        _dynasty = new DynastySystem { Content = content };
        _ambitions = new AmbitionSystem(content.Businesses, content.Holdings, content.Titles, content.Careers);
        if (world.Counter("health_opened") == 0) { world.WorldCounters["health_opened"] = 1; foreach (var c in world.Characters.Values) c.Health = Math.Min(c.Health, LifeSystem.Cap(c.Age)); }   // nobody opens a saga haler than their years allow
        WealthSystem.Seed(world);   // the old names come with something handed down, once
        _household = new HouseholdSystem(content.Holdings, content.Businesses, content.Titles, content.Trade, content.Fleets.FuelHullsPerUnit);
        // Every marriage the world opens with gets its record, without fanfare. Once only: a save resumed mid-year must not
        // quietly record a wedding the year's own reckoning would have announced.
        if (world.Counter(CourtshipSystem.OpenedCounter) == 0) { CourtshipSystem.Sync(world, announce: false); world.WorldCounters[CourtshipSystem.OpenedCounter] = 1; }
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
        if (World.InAcademy) return BeginTerm();
        World.Turn++;
        foreach (var pools in World.Pools.Values) pools.Regenerate();
        using (Perf.Time("begin:_economy.Accrue")) { _economy?.Accrue(World); }     // RNG-free, so determinism is preserved (GDD §17)
        using (Perf.Time("begin:ProjectSystem.Tick")) { ProjectSystem.Tick(World, _content.Holdings, _content.Businesses); }   // a year passes on every building site (docs/ECONOMY_MARKET.md stage B)
        using (Perf.Time("begin:_ventures.Tick")) { _ventures?.Tick(World); }      // last year's offers lapse
        using (Perf.Time("begin:_regions.Tick")) { _regions.Tick(World); }        // the rings: lawlessness, allegiance, risings and secessions (GDD §19) — seeded only for a new leader's name
        using (Perf.Time("begin:_pirates.Tick")) { _pirates.Tick(World); }        // raiders gather where the lanes are lawless, roam and plunder (GAP §6) — RNG-free
        using (Perf.Time("begin:_fleets.Tick")) { _fleets.Tick(World); }         // fleets move, eat, arrive; the intel picture refreshes (GDD §19) — RNG-free
        using (Perf.Time("begin:_fleets.ResolveBattles")) { _fleets.ResolveBattles(World, _rng); }   // warring fleets that share a system fight (seeded roll)
        using (Perf.Time("begin:PirateSystem.AfterBattles")) { PirateSystem.AfterBattles(World, _fleets.LastBattles); PirateSystem.WriteFlags(World); }
        using (Perf.Time("begin:_fleets.ResolveConquests")) { _fleets.ResolveConquests(World); }       // an unopposed fleet in enemy space takes its holdings
        using (Perf.Time("begin:_fleets.UpdateIntel")) { _fleets.UpdateIntel(World); }            // ...and the picture after the fighting
        using (Perf.Time("begin:_nations.Tick")) { _nations.Tick(World); }                  // beyond the Reach: raids, expeditions, the wars abroad (docs/FOREIGN_NATIONS.md) — RNG-free
        using (Perf.Time("begin:_market.Tick")) { _market.Tick(World); MarketSystem.Sweep(World); }   // the year's prices, from what the houses produced and consumed (docs/ECONOMY_MARKET.md) — RNG-free
        using (Perf.Time("begin:_companies.Tick")) { _companies?.Tick(World); }   // then the Exchange: earnings at those prices, dividends declared, the year's share prices
        using (Perf.Time("begin:_orders.Tick")) { _orders.Tick(World); }       // where every house stands in the Senate, in Arms and in Commerce, and who is whose client
        using (Perf.Time("begin:_spheres.Recompute")) { _spheres?.Recompute(World); }  // power-balance projection + threat/coalition (GDD §7), also RNG-free
        using (Perf.Time("begin:_progression.Apply")) { _progression?.Apply(World); }  // title soft-lock instability (GDD §13), also RNG-free
        using (Perf.Time("begin:_honors.Apply")) { _honors.Apply(World, _rng); }  // investitures when a distinction's conditions first hold (GDD §13)
        using (Perf.Time("begin:_greatCrisis.Tick")) { _greatCrisis.Tick(World, _rng); }   // the great clock advances; stages fire (GDD §16, M6)

        Phase = TurnPhase.Briefing;
        _briefing.Clear();
        using (Perf.Time("begin:events"))
            foreach (var fired in Engine.SelectBriefing(World, _briefingBudget))
                _briefing.Add(new BriefingItem(fired));

        Phase = TurnPhase.Action;
        SyncRng();
        return _briefing;
    }

    /// <summary>A term at the Academy opens in place of a turn (docs/ACADEMY.md): no world system runs and the year does not advance; the
    /// briefing carries the Academy's own events only (the event engine admits no others while it is in session).</summary>
    private IReadOnlyList<BriefingItem> BeginTerm()
    {
        _academy.BeginTerm(World);
        Phase = TurnPhase.Briefing;
        _briefing.Clear();
        foreach (var fired in Engine.SelectBriefing(World, AcademySystem.EventsPerTerm))
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
        if (World.InAcademy) { Phase = TurnPhase.Resolve; _academy.EndTerm(World); SyncRng(); Phase = TurnPhase.Idle; return; }   // the term closes; the world has not moved
        Phase = TurnPhase.Resolve;
        using (Perf.Time("end:household")) { _household.Tick(World, Executor, Context()); }   // those the head has handed work to do it; the household's own lives go on (docs/HOUSEHOLD.md)
        using (Perf.Time("end:ambitions")) { _ambitions.Tick(World, Executor, Context()); }   // the people of the house go after what they want (docs/AMBITIONS.md) — RNG-free
        using (Perf.Time("end:npc")) { _npcAi.Act(Executor, Context(), _crises, _economy == null ? null : _content.Holdings, _fleets.Rules, _content.Businesses); }   // NPCs act, invest, sail, and may author a crisis
        using (Perf.Time("end:senate")) { _senate.Tick(World); }          // the Senate votes last session's motions and hears new ones (GDD §6.1) — RNG-free
        using (Perf.Time("end:policy")) { PolicySystem.Tick(World, _content.Policies); }   // the estates weigh the laws (GDD §6.1) — RNG-free

        // Organic onset: the ripe-conditions roll (GDD §16 mixed origin). NPC-authored crises this
        // turn are already active, so they're excluded from the causable pool here.
        if (_crises != null)
        {
            var organic = _crises.RollOrganic(World, _rng, abstainWeight: 3.0 / Math.Max(0.25, World.Difficulty));
            if (organic != null) _crises.Trigger(organic, World, _rng);
            _crises.Decay(World, _rng);   // active crises wind down once their root cause eases (GDD §16)
            _crises.Recover(World);       // and with no crisis active, the realm's wounds close (data rules)
        }

        using (Perf.Time("end:life")) { LifeSystem.Tick(World, _content); }   // health, the record of each life, ties that have lasted (docs/LIFE.md) — RNG-free
        using (Perf.Time("end:wealth")) { WealthSystem.Tick(World); }   // allowances are paid, and people do as they like with their own money (docs/WEALTH.md) — RNG-free
        using (Perf.Time("end:education")) { EducationSystem.Tick(World); }   // everyone learns by what they do and what they study; children are brought up (docs/EDUCATION.md) — RNG-free
        using (Perf.Time("end:courtship")) { CourtshipSystem.Tick(World); }   // suits advance, rivals press, marriages warm or cool (docs/MARRIAGE.md) — RNG-free
        using (Perf.Time("end:dynasty")) { _dynasty.Tick(World, _rng); }   // age, deaths→succession, births (GDD §14). RNG only where life turns.
        using (Perf.Time("end:family")) { _family.Tick(World); }          // kin with wills of their own (GDD §18) — RNG-free
        using (Perf.Time("end:houses")) { HouseSystem.Tick(World); }      // names that have lost everything pass out of the rolls (GDD §13) — RNG-free

        SyncRng();
        Phase = TurnPhase.Idle;
    }

    private CommandContext Context() => new(World, _rng);
}
