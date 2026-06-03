# Praetoria — Build Spec for Claude Code

**Companion to:** the Game Design Document (GDD). This spec translates the design into an architecture and a staged build order for an agentic coding workflow (Claude Code, real repo).
**Engine:** Godot 4.x, C# (.NET).
**Build philosophy:** simulation first, presentation second; prove the core headless before any UI; everything content-bearing is data-driven from line one.

---

## 0. How to Use This Spec with Claude Code

- Work **one milestone at a time** (§Build Order below). Do not start a later milestone until the prior one runs and its acceptance test passes.
- At the start of each Claude Code session, point it at this spec + the GDD, name the current milestone, and tell it to work only within that milestone's scope.
- Keep a `/docs` folder in the repo containing both this spec and the GDD so the agent always has design context.
- After each milestone: commit, tag, and write a short `MILESTONE_N_NOTES.md` recording decisions/deviations.
- **Golden rule for the agent:** never entangle simulation logic with Godot nodes/UI. The simulation must be runnable without the engine's render layer (see §2).

---

## 1. Architectural Pillars (non-negotiable)

1. **Simulation / Presentation split.** The entire game state and rules live in plain C# (a `Core` assembly) with NO dependency on Godot types. Godot is a *presentation + input* layer that reads from and sends commands to Core. Rationale: enables headless testing, the reactive UI (GDD §19), modding (GDD §20), and clean saves.
2. **Data-driven content.** Events, traits, careers, spheres, holdings, crisis archetypes, exotics, megastructures — all defined in external data files (JSON), loaded at runtime. Code provides *mechanisms*; data provides *instances* (GDD §20 "slots in V1, fillers in DLC"). Modding falls out for free; do NOT build mod tooling in V1.
3. **Logic / Text separation.** Event *logic* (conditions, choices, effects) and event *text* (prose, dialogue) are separate, keyed by ID (GDD §15). Enables AI-assisted writing and localization without touching mechanics.
4. **Turn-resolved, not real-time.** The world recomputes on turn-advance only (GDD §9). Cheap; deterministic; easy to test and save.
5. **Deterministic core + seeded RNG.** All randomness flows through a single seeded RNG owned by Core. Same seed + same inputs = same outcome. Essential for save integrity, debugging, and reproducible tests.

---

## 2. Project / Assembly Structure

```
/ (Godot project root)
├─ project.godot
├─ /docs                     (GDD + this spec + milestone notes)
├─ /src
│  ├─ /Core                  (PLAIN C# — no Godot references)
│  │  ├─ /State              (world model: entities + components)
│  │  ├─ /Events             (engine: pool, triggers, binding, director)
│  │  ├─ /Systems            (rules: careers, spheres, crisis/gates, economy, succession)
│  │  ├─ /Data               (loaders + typed records for JSON content)
│  │  ├─ /Commands           (player/AI intents → state changes)
│  │  └─ /Rng                (single seeded RNG service)
│  ├─ /Game                  (Godot layer — references Core)
│  │  ├─ /Presentation       (Court UI, Galaxy UI, dynasty overlay)
│  │  ├─ /Input              (command construction from UI)
│  │  └─ /Theme              (reactive art-direction: era × role × house)
│  └─ /Tools                 (headless runner, content validators)
├─ /content                  (DATA FILES — JSON)
│  ├─ /events                 (logic)
│  ├─ /text                   (prose, keyed to event/choice IDs)
│  ├─ /traits  /careers  /spheres  /holdings  /crises  /houses
└─ /tests                    (xUnit/NUnit against Core — no Godot)
```

**Key rule:** `/src/Core` and `/tests` compile and run **without Godot**. A developer can run the whole simulation from a console harness in `/src/Tools`. The Godot `/Game` layer is additive.

---

## 3. The State Model (GDD §15 Layer 1 — the spine)

Model the world as **entities with components** (lightweight; doesn't need a full ECS framework — composition over deep inheritance). Core entities:

- **Character** — id, name, houseId, age, alive; `careerTrack` + `careerRank`; **Nature traits** (constraining, stress-bearing) and **Aptitude traits** (gating/flavor) (GDD §18); skills; `stress`; personal `ambition`; status flags.
- **House (Dynasty)** — id, name, **houseAccentColor** (drives reactive theme), members (character ids), **sphere-influence values** (per sphere, GDD §7), the **three treasuries** split (Imperial office holdings vs. House holdings vs. personal, GDD §8), legitimacy baseline, threat-score inputs.
- **Bond** — typed edge between characters: `blood` (carries succession rights) or `sworn` (no inheritance, can carry higher loyalty); strength; disposition (GDD §18 two-axis loyalty web). Relationships are a queryable graph (see §7 — "find connections anywhere", GDD §19).
- **Holding** — id, ownerId, systemId, **type/specialization**, building slots + built buildings, population + loyalty/unrest, strategic position (GDD §17).
- **System / Galaxy node** — id, position, hyperlane links, owning house (territory = public/visible), contained holdings (GDD §19 "Star Dynasties+").
- **Fleet** — id, ownerId (+ `office` vs `house` flag, GDD §8), composition, position, orders, readiness. Rival fleet knowledge stored as *reported* intel with a confidence value (GDD §19 mixed visibility).
- **Resources** — five pools (Credits, Materials, Manpower, Influence, Exotics — GDD §17), tracked per treasury.
- **Action Pools** — Influence, Treasury, Agents (GDD §9) — finite per-turn spend.
- **CrisisState** — active crises; **gates** (named precondition predicates) and their cleared/uncleared status; cascade links; damper availability (GDD §16).
- **World** — turn counter, seeded RNG state, the **Great-Crisis framework** (active archetype, clock, intensity setting), era flag (pre/post-Imperium → drives reactive UI), a **recent-history log** (for the Director + event conditions).

All of the above must be **serializable** (save/load = serialize World). Saves are the single source of truth.

---

## 4. The Event Engine (GDD §15 Layers 2–4)

- **Event record (data):** id, tier (`ambient` | `situation` | `setpiece`), trigger conditions (predicates over State), weight, role variables (e.g. `{a_rival_of_higher_rank}`), choices.
- **Choice (data):** id, cost (pools), requirements (state/trait/rank gates), consequences (list of state-writes: set flag, modify relationship, clear/arm a crisis gate, adjust resource/sphere, etc.).
- **Text record (data, separate file):** keyed by event/choice id → prose, with role-variable substitution.
- **Binder:** resolves role variables to concrete entities that satisfy the role's constraints (the "by ROLE not NAME" rule). One event → many lived contexts.
- **Eligibility + weighted selection:** each turn, gather events whose conditions hold given current State; the **Director** biases weights (suppress after big events, raise crisis-tier weight as game advances, prevent category flooding, inject complications in lulls) and selects.
- **Consequence applier:** writes choice consequences back into State — crucially including **arming/clearing crisis gates** (this is how chained storylines and cascades emerge).

**Acceptance property:** a consequence in one event must be able to change State such that a *different*, unscripted event becomes eligible later. If that chaining works, the engine is correct.

---

## 5. Core Systems (built as data-fed mechanisms)

- **Careers (GDD §13):** track + rank ladders from data; rank gates decisions and advisor-seat access. V1 ships 3 tracks.
- **Spheres / Power-balance (GDD §7):** N spheres from data (V1: 3). Finite influence per sphere; house values; **threat-score** computation; **coalition** behavior scaled by a difficulty multiplier.
- **Progression / Legitimacy (GDD §13):** two independent ladders (title, career); legitimacy modifier as soft-lock; three conversion paths (Military/Merit/Intrigue) as claim-generation + enforcement mechanisms.
- **Crisis & Gates (GDD §16):** crises as **gated buildable states**; gates = named predicates over State; crises fire **organically** (Director) or **authored** (player/NPC command) once gates clear; **cascade** links; **dampers** = f(difficulty × accumulated prior-choice state). Great-Crisis framework with the human galaxy-war archetype in V1; archetype→map-behavior hooks present but only human-seeded.
- **Economy (GDD §17):** five resources, conversion chains, per-turn accrual/upkeep on turn-advance; Steward surfaces decisions as events. Variable-emphasis tuning (do NOT balance to uniformity).
- **Succession & Dynasty (GDD §14, §18):** permadeath → heir; dynasty death = no living house-name member; ironman vs. continue mode; "scatter the seed" exposure/hiding; family autonomy intensity (NPC family run ambitions through the same event engine).
- **AI (NPC houses & family):** NPC houses act through the **same command interface** the player uses (issue intents, spend abstracted resources, pursue ambitions, clear/trigger gates). Keep NPC realms abstracted (GDD §3) — stats + ambitions, not full economies.

---

## 6. Presentation Layer (Godot — built AFTER the core works)

- **Two modes (GDD §4, §19):** Court (three-column: advisors+pools / briefing feed / dynasty+crisis-clock) and Galaxy (command-table map). Persistent top status bar with the mode toggle.
- **Reactive theming (GDD §19):** a Theme service reads era (pre/post-Imperium) + character role + house accent color and skins the UI accordingly. Build a strong *base* theme first; layer era/role reactivity later (it's real added cost — stage it).
- **Dynasty as contextual connections (GDD §19):** no dedicated mode; summary rail + summonable full-tree overlay; relationship graph queryable so connections surface in-context everywhere ("who do I know in the Senate / in House Corwin?").
- **Galaxy map:** 2D node-and-lane, curated scale; territory shaded by house accent; fleets as markers (office vs house sigils); mixed visibility (territory visible, fleet/strength intel confidence-flagged); Great-Crisis map behavior per archetype (DLC archetypes add their own).
- **Typography:** ceremonial (serif/engraved) for Court; functional (technical sans) for Galaxy.

---

## 7. Cross-Cutting Requirements

- **Queryable relationship graph:** the bond graph must answer "connections of X in context Y" cheaply, to power contextual connection-surfacing (GDD §19) and binding.
- **Command pattern:** all state mutation (player and AI) goes through `/Core/Commands` — gives undo/redo potential, replay, testability, and a single AI/player interface.
- **Saves = serialize World** (incl. RNG state). Versioned for forward-compat with DLC.
- **Content validation tool** (`/src/Tools`): lints data files (every event-logic id has matching text; referenced traits/roles exist; no dangling gate references). Run in CI.
- **No hard-coded content in Core logic** — if it's an instance (a trait, an event, a sphere), it lives in `/content`.

---

## 8. Build Order (staged milestones — each playable, each builds on a proven core)

### Milestone 1 — "The Engine Breathes" (headless Academy Crucible)
Build the **State model** (§3), **Event Engine** (§4), **data-file format + loaders** (logic/text split), **seeded RNG**, and a **console harness** (`/src/Tools`). Author the **Academy Crucible** as the first event pool (a cadet cohort, relationship bonds, a small set of events with role-binding, choices, and consequences that arm other events).
- **No Godot UI. Runs in console.**
- **Acceptance:** play the Academy as text; advance turns; a relationship/flag created by one choice causes a *different, unscripted* event to fire later. Engine chaining proven. Unit tests cover binding, eligibility, consequence-application, and a cascade.

### Milestone 2 — Turn Loop, Pools & Commands
Formalize the turn structure (Briefing → Action → Resolve, GDD §9), action pools, and the command interface (§7). NPC characters act through commands. Still headless/console.
- **Acceptance:** a full turn cycle with pool spending and NPC actions resolves deterministically from a seed.

### Milestone 3 — Court Mode UI (first Godot layer)
Wrap the working core in the Court three-column UI (§6). Base theme only (no reactivity yet). Briefing feed renders events by tier; choices spend pools; dynasty rail + crisis clock display.
- **Acceptance:** the Academy + early court game is fully playable via UI, no console needed.

### Milestone 4 — Economy, Holdings & Galaxy Mode
Add the five resources, holdings/specializations/buildings, succession, and the Galaxy command-table map (§6) with mixed visibility and the house/office fleet distinction.
- **Acceptance:** play from a single barony, manage domain via Steward decisions, move fleets, advance through a succession.

### Milestone 5 — Spheres, Progression & the Gate/Cascade Crisis System
Power-balance + threat/coalition; two-ladder progression + legitimacy soft-lock + three paths; the full crisis gate/cascade/damper system; Bloody Events; NPC houses authoring crises.
- **Acceptance:** a crisis can be engineered or defused via gates; a cascade escalates and a damper (earned by prior play) arrests it; an NPC house triggers a crisis.

### Milestone 6 — The Human Endgame
The Great-Crisis framework seeded with the human galaxy-war; proclaiming/seizing the Imperium (era flip); dynasty death / ironman vs continue; saga epilogue.
- **Acceptance:** a full run from Baron to a throne-endgame (won or lost), with the era flip and a generated epilogue. **This is V1-complete on systems.**

### Milestone 7 — Reactive UI, Polish, Content Depth
Era × role × house reactive theming (§6); ceremonial/functional typography; portraits; audio; the free-content event pipeline; balance pass (respect variable-emphasis, GDD §17). Build out V1 event/content volume.
- **Acceptance:** the diegetic reactive UI transforms on proclamation and by role; content volume sufficient for replayability.

### Post-V1 — DLC (slots already exist)
Fill the data slots per GDD §20: The Threat From Beyond (cosmic archetypes), AI Ascendant, Courts of Intrigue (Intelligence sphere + Spymaster track), The Frontier, Wrath of the Stars. Mostly content + data; minimal engine work.

---

## 9. First Session Kickoff (suggested prompt to Claude Code)

> "Read /docs/GDD and /docs/BuildSpec. We are building Milestone 1 only: the headless Academy Crucible. Set up a Godot 4 C# project with the assembly structure in BuildSpec §2, ensuring /src/Core and /tests build and run WITHOUT Godot. Implement the State model (§3), the Event Engine (§4), the JSON data loaders with logic/text separation, and a single seeded RNG service. Author a small Academy Crucible event pool in /content to prove the engine. Provide a console harness in /src/Tools to play it as text. Write unit tests proving role-binding, eligibility, consequence-application, and one cascade (a choice in event A makes unscripted event B eligible later). Do not build any UI. Stop at the Milestone 1 acceptance criteria."
