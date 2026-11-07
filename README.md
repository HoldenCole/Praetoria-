# Praetoria

> *You don't play an empire. You play a family trying to survive one.*

**Praetoria** is a human-only galactic grand-strategy / dynasty-roleplay game. You begin as the heir
of a minor noble house holding a single barony, and you climb — through war, merit, intrigue, and
bloodline — toward the throne of a fractured human galaxy. Your character will age and die. Your
*dynasty* is what you are really playing: it outlives every person in it, or it ends.

**Engine:** Godot 4.x · C# (.NET 8). **Status:** the entire simulation core is built and runs headless
(no UI yet); see [What's built](#whats-built-the-current-state) and [What's not](#whats-not-built-the-gaps).

This README is the front door. If you are new — a programmer, a writer, or just curious — it should
leave you knowing what the game *is*, how it's designed, exactly what exists today, what doesn't, and
where it's going. The deep sources are in [`/docs`](docs/): the full [Game Design Document](docs/GDD.md)
and the [Build Spec](docs/BuildSpec.md) that turns it into a construction order.

---

## Table of contents

1. [The idea](#the-idea) — what the game is and why it's different
2. [The design](#the-design) — principles, the fidelity model, and what each influence contributes
3. [How it's built](#how-its-built-architecture) — the architecture, for programmers
4. [What's built](#whats-built-the-current-state) — the current state, system by system
5. [What's not built](#whats-not-built-the-gaps) — the honest gap list
6. [Where it's going](#where-its-going-roadmap--expansion-passes) — remaining milestones, then V1 → free updates → DLC
7. [Try it](#try-it-quickstart) — run the headless game in two minutes
8. [Repo map & reading order](#repo-map--where-to-read-next)
9. [Contributing content](#contributing-content)

---

## The idea

Most space-strategy games hand you a state and a map to paint. Praetoria hands you a **person** and a
**position** inside a larger order you don't control. The galaxy belongs to humanity alone — no aliens
— divided into a fractured feudal web of houses (or, in another start, an already-standing Imperium).
Power is split among **spheres** — the Navy, the Treasury, the Senate — and every house holds some
share of each. Your gain in any sphere is someone else's loss.

The fantasy, in one line from the design doc: **Crusader Kings' character depth × Stellaris'
systemic depth, in the tone of *Legend of the Galactic Heroes* and *Red Rising*.**

Three things make it distinctive:

- **The dynasty is the save, not the character.** People die — of age, in duels, at a gala that turns
  to slaughter. The seat passes to an heir; the house's titles, legitimacy, holdings and claims endure.
  Lose your last name-bearer and the game is over. So heirs are precious, marriages are strategy, and
  "scatter the seed" (hide a spare heir somewhere safe) is a real survival choice.
- **Narrative is systemic, not scripted.** There is no fixed plot. Authored events are written *by
  role* ("a rival who outranks you"), not by name, and fire when the world's state makes them true.
  A choice in one scene arms a flag that makes a *different, unscripted* scene possible later. Stories
  emerge; they don't repeat.
- **Crises are built, not rolled.** A revolt, a coup, a civil war is a *gated state* — it cannot
  happen until preconditions accumulate in the world. Once the gates clear it can erupt on its own
  **or be deliberately lit by an ambitious house (you included)**. Every crisis is both a threat and a
  weapon. Crushing a small revolt can clear the gates of a civil war. *How you climbed determines
  whether you can hold what you took.*

There is no hard victory. You can seize the throne, be ruined, or watch your line go extinct. The
"win" is the story you got.

---

## The design

### Five core principles (GDD §2)
1. **You play a person/dynasty, not a state.** You control your house's *position within* the order.
2. **Family comes first — as a mechanic.** You can betray the Imperium for your house.
3. **No hard victory.** A roleplay sandbox with milestones and fail states.
4. **Replayability over scripting.** Narrative from a systemic event engine, never a fixed sequence.
5. **Extract dynamics, not plot.** Borrow reusable *dynamics* from the influences ("the gala that turns
   to slaughter", "tier tension", "succession crisis") — never a specific saga's one-time story.

### The fidelity model (GDD §3) — why a small team can build this
**Deep simulation only where the player touches it; abstraction everywhere else.** Your own domain is
simulated for real — resources, buildings, population, unrest. Rival realms are *not* simulated
economies; they're a handful of stats and ambitions that drift through the same event engine. The
world recomputes only on turn-advance. Information reaches you through **advisor reports** — biased,
sometimes wrong — which gives fog-of-war and decision tension for free.

### Two co-equal modes (GDD §4)
- **Court mode** — the dynasty and political layer: portraits, advisor briefings, decision scenes,
  family, succession. A ceremonial register.
- **Galaxy mode** — a curated 2D node-and-lane map: your holdings, fleets, rival realms, and (late
  game) the Crisis creeping in from an edge. A command-table, not a 4X paint job. A functional register.

The advisor report is the bridge: read in Court, acted on in Galaxy. The UI is *reactive* — it is meant
to visibly transform when the Imperium is proclaimed and to re-skin by your character's career.

### The influences — and exactly what each contributes (GDD §1)

| Influence | What we take | What we deliberately don't |
|---|---|---|
| **Star Dynasties** | Human-only feudal space; levels of rulership; family/character focus | Its shallowness — we add mechanical depth |
| **Stellaris** | Resource/building depth, megastructures, a growing navy, anomalies/archaeology, replayability | Its sprawl and its aliens — we abstract hard and remove non-humans |
| **Crusader Kings** | Playing a dynasty with permadeath and succession; personal ambitions | — |
| **Suzerain** | Advisor-driven decision scenes on imperfect information | Its scripted, repeating structure — ours is systemic |
| **The Expanse** | Tiers of human civilisation in tension; a Protomolecule-style external threat | — |
| **Legend of the Galactic Heroes** | Empire vs. free alliance; an over-mighty military house terrifying the old aristocracy | — |
| **Red Rising** | One ruling society with internal tiers; the low-born rising by merit; a brutal academy crucible; elite orders; the old houses' contempt for the upjumped. **The authoring voice-target.** | — |

### The systems, in brief
- **Two ladders** (§13): a **career** ladder (resets each generation) and a **title** ladder (persists on
  the house). The mid-game is converting what you have into what you lack.
- **Three rise paths** = three exchange rates: **Military** (*take it* — fast, ignores legitimacy,
  makes the galaxy your enemy), **Merit** (*be granted it* — slow, clean, capped), **Intrigue**
  (*inherit/marry/usurp it* — generational, catastrophic if caught).
- **The legitimacy soft-lock** (§13): birth is a worse exchange rate, never a wall. Hold a title above
  your standing and you rule a powder keg until standing catches up.
- **Power balance** (§7): career feeds your house's sphere influence; dominate an estate and the
  rivals coalesce against you where you're weak.
- **Crises as gated states** (§16): preconditions accumulate, cascades escalate through shared gates,
  **dampers** arrest them — but dampers are earned by prior play.
- **The dynasty layer** (§14/§18): nature traits (constraining, stress-bearing) vs aptitude traits
  (free); blood ties vs sworn bonds; family with its own ambitions and the will to defy you.
- **The economy** (§17): five resources — Credits, Materials, Manpower, Influence, Exotics — tuned so
  each is *dominant under some path*, never balanced to uniformity.

---

## How it's built (architecture)

Three non-negotiables shape every file (Build Spec §1):

1. **Simulation / presentation split.** Everything that *is the game* — state, rules, the event engine,
   every system — lives in a plain-C# **`Core`** assembly with **zero dependency on Godot**. Godot is
   an additive presentation + input layer that reads from and sends commands to Core. This is what lets
   the whole game run and be tested headless, enables clean saves and modding, and is why a console
   harness can play the entire simulation today.
2. **Data-driven content, with a strict logic/text split.** Events, crises, holdings, spheres, titles,
   scenarios are JSON in `/content`. Event *logic* (conditions, choices, effects) and event *prose* are
   separate files joined by id — so writers and AI can author text without touching balance, and the
   game is mod-ready from line one.
3. **Deterministic core + one seeded RNG.** Same seed + same inputs = same outcome, always. Every
   system threads a single `SplitMix64` stream; most systems draw no randomness at all. A save is just
   the serialised `World` (including the RNG position).

A fourth pillar fell out of practice: **all state mutation flows through a command bus** that the player
and NPC houses share — so AI and player are one interface, every run is replayable from its log, and
the turn is a formal **Briefing → Action → Resolve** cycle.

```
/docs                   GDD · Build Spec · content guide · vocab bible · milestone notes
/src
  /Core                 PLAIN C#, no Godot — the whole game
    /State              World, Character, House, Holding, Relationship, ActiveCrisis, Resources, Pools
    /Events             the event engine: Binder, Director, conditions & effects vocabulary
    /Systems            TurnController, Economy, CrisisEngine, SphereSystem, ProgressionSystem, DynastySystem, NpcAi
    /Commands           the command bus + every player/NPC intent
    /Data               JSON loaders, content validator, scenario builder
    /Crises /Spheres /Progression   data models for the §16/§7/§13 catalogs
  /Tools                headless console harness (play · demos · validate)
  /Game                 Godot layer — staged for Milestone 3, deliberately NOT in Praetoria.sln
/content                events · text · scenarios · holdings · crises · spheres · titles  (JSON/JSONC)
/tests                  xUnit against Core — runs without Godot
Praetoria.sln           Core + Tools + Tests (everything that builds headless)
validate_praetoria.py   pre-flight content validator (mirrors + extends the engine's gates)
```

The event-engine vocabulary — the condition and effect "verbs" content can use — is documented
exhaustively in [`docs/CONTENT_GUIDE.md`](docs/CONTENT_GUIDE.md); the locked names for traits, skills,
ambitions and ranks are in [`docs/PRAETORIA_VOCAB.md`](docs/PRAETORIA_VOCAB.md).

---

## What's built (the current state)

**In one sentence:** the complete headless simulation — every core system of the human game, wired
together, deterministic, soaked for 40-turn playthroughs, with an authored content set — and no UI.

| System | Spec | Status | What it does today |
|---|---|---|---|
| **Event engine** | GDD §15 | ✅ | Role-bound authored events; eligibility from world state; a Director paces by tier (ambient/situation/setpiece), suppresses repeats, raises set-pieces as the run advances; consequences write state so unscripted chains emerge. **Proven:** an insult arms a `feud` flag → a duel that was *impossible* becomes eligible. |
| **Turn loop, pools, commands** | §9, BuildSpec §7 | ✅ | Briefing → Action → Resolve. Finite per-turn action pools (Influence/Treasury/Agents). A command bus shared by player and NPC AI; the run is replayable from its command log. |
| **Domain economy** | §17 | ✅ | Five resources on a house treasury; holdings with specializations and building slots; per-turn accrual/upkeep; insolvency → unrest → manpower suppression. Build command through the bus. |
| **Crisis system** | §16 | ✅ | Crises as gated states with onset by organic roll *or* authored trigger (NPC houses light fuses); cascades through shared counters; dampers gated on prior play; **natural de-escalation** when a crisis's root cause eases. **Proven:** crushing a revolt arms the civil-war gate. |
| **Power-balance spheres** | §7 | ✅ | Navy/Treasury/Senate influence **derived from member careers**; a threat score (dominance + holdings + seizures); coalition pressure that arms a coalition-war crisis; bridged into the player-facing coalition event chain. |
| **Progression & soft-lock** | §13 | ✅ | A 7-rung title ladder with legitimacy requirements; the three rise paths as commands (Seize / Petition / Claim) at three exchange rates; holding above your legitimacy breeds instability that feeds the crisis web. |
| **Dynasty lifecycle** | §14 | ✅ | Aging; mortality from 50; births to married couples (patrilineal, blood ties); protagonist death → heir handoff (eldest blood child first) with the house persisting; dynasty death when no heir remains. `kill` and `age` verbs for events. |
| **NPC AI** | §3, §18 | ✅ (basic) | Trait/ambition-driven actions through the same command bus; ambitious NPCs author crises. Deliberately abstracted (no NPC economies). |
| **Content pipeline** | §15, §20 | ✅ | JSON loaders, JSONC-tolerant; an engine validator (text pairing, token resolution) and a stricter Python pre-flight (locked vocab, read-only counters, flag-chain audit). |
| **Console harness** | BuildSpec §2 | ✅ | `play` (the whole game as text), plus focused demos for every system, and `validate`. |

**The authored content set** (all validated, all in `/content`):

| | Count | Notes |
|---|---|---|
| Events (+ paired prose) | **62** | Academy Crucible 17 · Career progression 15 · Steward/economy 15 · Coalition chain 8 · Succession 7 |
| Scenarios | 4 | `academy_crucible` (the fixture) + the three rise-path openings: *The Fallen House*, *The Circling Heir*, *The Risen Officer* |
| Crises | 5 | local revolt → civil war cascade · coalition war · contested title · court scandal |
| Holding specializations / buildings | 12 / 12 | |
| Spheres / title rungs | 3 / 7 | |
| Controlled vocabularies | locked v1.1 | 9 nature traits · 11 aptitude traits · 13 skills · 8 ambitions · 3 tracks with named rank ladders |

**Verification:** 104 automated tests, including a 40-turn whole-stack soak that asserts end-to-end
determinism, succession landing, crises de-escalating, and no absurd state; both validators clean;
every scenario runs 30+ turns crash-free.

Per-milestone detail, with the acceptance proof for each: [`MILESTONE_1`](docs/MILESTONE_1_NOTES.md)
(engine) · [`MILESTONE_2`](docs/MILESTONE_2_NOTES.md) (turn loop) · [`MILESTONE_4`](docs/MILESTONE_4_NOTES.md)
(economy) · [`MILESTONE_5`](docs/MILESTONE_5_NOTES.md) (crises, spheres, progression) ·
[`MILESTONE_DYNASTY`](docs/MILESTONE_DYNASTY_NOTES.md) (lifecycle + hardening pass).

---

## What's not built (the gaps)

Being honest about this is the point of the section. Nothing here is hidden behind a feature flag —
if it's listed, it does not exist yet.

### Not started
| Gap | Spec | Why it matters / notes |
|---|---|---|
| **Court UI (Godot)** — Milestone 3 | §19, §6 | The three-column Court screen: advisors + pools / briefing feed / dynasty rail + crisis clock. **The game is not yet playable by a human without the console.** The Godot `/src/Game` project is scaffolded but empty. This was deliberately deferred: it can't be built or verified in a headless environment; it needs a session with Godot and a display. |
| **Galaxy mode + fleets** | §19, §6, M4 | The 2D node-and-lane star map, systems/hyperlanes, territory shading, fleet markers with the house-vs-office distinction, mixed visibility (own holdings deep, rival intel fogged). **No fleet or military-unit model exists at all** — "weapons/ships" have no data slot yet. |
| **Bloody Events** | §16 | The gala/wedding/tournament set-piece class (safe gathering → sudden violence → arms a larger crisis). The *mechanism* now exists (`kill` + crisis gates), so this is authorable content, not engine work. |
| **The Great-Crisis framework & human endgame** — Milestone 6 | §16, §11 | The archetype framework seeded with the human galaxy-war; proclaiming/seizing the Imperium and the **era flip**; ironman vs continue mode; the generated **saga epilogue**. The *dynasty-death terminal state* exists; the endgame arc around it does not. |
| **Reactive UI, polish, content depth** — Milestone 7 | §19 | Era × role × house theming, ceremonial/functional typography, portraits, audio, the free-content pipeline, the balance pass. |
| **Honorifics & Orders** | §13 | Merit-path prestige distinctions ("Olympic Knight"-style) that raise legitimacy independent of title. |
| **Scatter-the-seed / heir-hunting** | §14 | Hidden kin, fostering as placement, the cost model for hunting a bloodline. |
| **Marriage/fostering as *systems*** | §18 | Marriage exists as a bond and forges claims via content; there's no proposal/negotiation system or fostering. |
| **Family autonomy** | §18 | Family members resisting/defying the player's direction based on loyalty + ambition + traits. NPC *houses* have will; the player's *own family* does not yet. |
| **Anomalies/archaeology & Exotics sources** | §17 | The Exotics resource exists; the exploration system that produces it (and foreshadows the Crisis) is DLC-era design and not built. |
| **Save/load** | BuildSpec §7 | The `World` is designed to serialise (RNG position included) and is deterministic, but no save-file format or loader is implemented yet. |

### Built but shallow (works; known to need depth or tuning)
- **Succession law** is primogeniture-by-age among blood children, then eldest house member. No
  regency for child rulers, no matrilineal/elective law, no contested successions as a system.
- **Births** are patrilineal only; **the shipped scenarios have no sexed characters or marriages**, so the
  dynasty layer is structurally present but *dormant* in them until a cast is seeded.
- **Sphere influence** derives only from careers; titles, marriages and standing don't yet feed it.
- **Threat formula**: a house that's the *sole* holder of a sphere reads as maximally threatening even
  at trivial absolute influence (defensible — "you own the whole Navy" — but coarse).
- **Accumulators** `realm_unstable` (never clears) and imperial `legitimacy` (unbounded negative) can
  keep late-game crisis gates warm; mitigated by de-escalation, flagged for a balance pass.
- **Non-repeatable crises** can re-onset after resolving (the flag only blocks re-onset *while active*).
- **NPC AI** is a small set of trait-driven verbs — enough to prove the shared-command architecture,
  not a strategic opponent.
- The three rise-path scenarios default to **landless / zero legitimacy**, so the title and soft-lock
  systems are inert in them until seeded with starting titles and standing.

---

## Where it's going (roadmap & expansion passes)

### Remaining milestones to a complete V1 (Build Spec §8)

| | Milestone | Scope | Acceptance |
|---|---|---|---|
| ✅ | **1 — The Engine Breathes** | headless event engine + Academy Crucible | a choice makes a different, unscripted event eligible later |
| ✅ | **2 — Turn Loop, Pools & Commands** | Briefing→Action→Resolve, pools, command bus, NPCs | a full turn cycle resolves deterministically from a seed |
| ⬜ | **3 — Court Mode UI** | first Godot layer; the three-column Court; base theme | the Academy + early court game fully playable via UI |
| ◐ | **4 — Economy, Holdings & Galaxy** | economy ✅ · succession ✅ · **Galaxy map + fleets ⬜** | play from a barony, move fleets, advance through a succession |
| ◐ | **5 — Spheres, Progression & Crises** | crises ✅ · spheres ✅ · progression ✅ · **Bloody Events ⬜** | engineer/defuse a crisis via gates; a damper arrests a cascade; an NPC triggers one |
| ⬜ | **6 — The Human Endgame** | Great-Crisis framework (human galaxy-war); proclaim/seize the Imperium; era flip; ironman; saga epilogue | a full run from Baron to a throne-endgame, won or lost. **V1-complete on systems.** |
| ⬜ | **7 — Reactive UI, Polish, Content Depth** | era × role × house theming; typography; portraits; audio; content volume; balance | the UI transforms on proclamation; enough content for replayability |

The recommended order from here: **3 (Court UI)** — the game becomes human-playable and everything
built becomes visible; then **Galaxy mode + fleets** (which also unlocks the military rise-path's
weapons/ships); then **6**. Bloody Events can land any time as content.

### The expansion philosophy: "slots in V1, fillers in DLC" (GDD §20)
Every extensible system is built as a *generalised mechanism exposed to data*. DLC adds **instances**
into slots that already exist — it never cuts the game open to add a new system. You can see this in
the code today: career tracks, spheres, crisis archetypes, holding types, buildings, titles and traits
are all data lists the engine reads, so most future content is zero engine work.

### V1 — a complete *human* game
Everything above: 3 career tracks, 3 spheres, the full human-crisis suite (revolts, coups, civil wars,
usurpers, succession wars, Bloody Events), the gate/cascade system, the Great-Crisis framework seeded
with the **human galaxy-war**, both modes, the reactive UI, the economy, the dynasty layer.
**Endgame = proclaim or seize the Imperium and hold the throne.** A complete arc with no DLC required —
V1 must never feel like it's building toward an absent finale.

### Free updates
Balance, fixes, QoL — and **ongoing free event/story content**, because events are data and keeping
the human-political game alive between paid releases is core to the model.

### Paid DLC — five planned, each adds a *new axis*, never restores a missing one
1. **The Threat From Beyond** *(flagship)* — the cosmic layer: non-human Great-Crisis archetypes
   (**Swarm**: an advancing front · **Plague**: spreading across nodes · **Awakening**: dormant sites
   erupting where you dug) with their signature map behaviours, and the anomaly/archaeology
   foreshadowing payoff. Recontextualises a game players already found whole: *"you thought the throne
   was the endgame — now something worse is coming."*
2. **AI Ascendant** — the AI-sentience Great Crisis (placate / fight / prevent) + a Synthetics/
   Inquisitor career track; "defection/conversion" map behaviour.
3. **Courts of Intrigue** — the **Intelligence sphere + Spymaster track**: deep plot/counter-intel,
   secret societies, more Bloody Events. (Intrigue *as a playstyle* ships in V1; only the specialised
   profession is the deepening.)
4. **The Frontier** — planet-building: deeper specialisation and building trees, terraforming/
   colonisation, new holding types, more anomalies.
5. **Wrath of the Stars** — planet-killers, the deep megastructure catalog, doomsday-scale content, a
   higher Crisis-intensity ceiling, new Exotics.

*(Reserve: "Houses of Legend" — legendary named houses, bespoke dynastic mechanics, legacy perks.)*

### Three personalisation axes (tunable at setup)
**Crisis intensity** (off → dominant: a pure court-intrigue game or an all-consuming war),
**family autonomy** (deferential family → a CK-style pressure-cooker of scheming relatives), and
**resource emphasis** (emerges from your path — a conquest run lives on Materials + Manpower, an
intrigue run on Influence + Credits). Together they make the same systems play as different games.

---

## Try it (quickstart)

Needs the .NET 8 SDK. No Godot required — this is the whole simulation, headless.

```bash
# Lint the content set (every event has prose, every token resolves, vocab is locked)
dotnet run --project src/Tools -- validate

# Play the game as text — the Academy Crucible, full Briefing→Action→Resolve turns
dotnet run --project src/Tools -- play --seed 1 --turns 8          # interactive
dotnet run --project src/Tools -- play --seed 1 --turns 30 --auto  # auto-pilot, any scenario via --scenario

# Watch each system prove its design, deterministically from a seed:
dotnet run --project src/Tools -- demo --seed 1          # the cascade: an insult makes an impossible duel possible
dotnet run --project src/Tools -- turn --seed 1          # a full turn cycle with pools + NPC actions
dotnet run --project src/Tools -- economy --seed 1       # holdings accrue; invest in a building; yield compounds
dotnet run --project src/Tools -- crisis --seed 1        # a revolt cascades into civil war; dampers gate on goodwill
dotnet run --project src/Tools -- spheres --seed 1       # an heir's Navy career spikes threat until rivals coalesce
dotnet run --project src/Tools -- progression --seed 1   # the meritocrat rules secure; the conqueror rules a powder keg
dotnet run --project src/Tools -- dynasty --seed 3       # a ruler dies, his heir succeeds, children are born

# The test suite (104 tests, incl. a 40-turn whole-stack soak)
dotnet test
```

Running `dotnet run --project src/Tools -- help` lists every command.

---

## Repo map & where to read next

**If you want to understand the game:** read [`docs/GDD.md`](docs/GDD.md) — §1–5 for the idea, §13–18
for the systems, §20 for scope. It's the source of truth; this README summarises it.

**If you want to build on the code:** read [`docs/BuildSpec.md`](docs/BuildSpec.md) (the architecture
and milestone order), then start in `src/Core/Systems/TurnController.cs` — it's the spine that calls
every system in order each turn — and follow the calls out. Every system has focused unit tests in
`/tests` that double as executable documentation; `tests/IntegrationTests.cs` shows the whole stack
running together.

**If you want to write content:** read [`docs/CONTENT_GUIDE.md`](docs/CONTENT_GUIDE.md) (the complete
authoring interface: every condition/effect verb, schemas, worked patterns, the read-only counters) and
[`docs/PRAETORIA_VOCAB.md`](docs/PRAETORIA_VOCAB.md) (the locked names). Then look at any file in
`/content/events` with its twin in `/content/text`.

**If you want the history:** the `docs/MILESTONE_*_NOTES.md` files record what each milestone built,
the acceptance proof, the decisions and deviations, and (for the latest) the hardening pass and its
known issues.

---

## Contributing content

Content is the heart of this game and it needs no C#. The engine only accepts the documented
vocabulary, so author against [`CONTENT_GUIDE.md`](docs/CONTENT_GUIDE.md), then gate it:

```bash
python3 validate_praetoria.py                   # strict pre-flight: vocab, tokens, chains, read-only counters
dotnet run --project src/Tools -- validate      # the engine's own load-time gate
dotnet test                                     # nothing regressed
```

Highest-value content right now: **Bloody Event set-pieces** (the mechanism is ready), **seeding the
three rise-path scenarios** with titles/legitimacy/sexed married casts (so the progression and dynasty
layers come alive in them), and more **academy / career / succession** depth toward a 40–60 event
opening act.

## License

TBD.
