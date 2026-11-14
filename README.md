# Praetoria

## ▶ [Play it in your browser](https://holdencole.github.io/Praetoria-/)

No install. The whole game — the office you furnish, the galaxy of six regions and eighteen worlds, the house, the
navy, the dynasty, the other houses, the chronicle, saves in your browser — runs as a WebAssembly build of the same
engine the tests run. Every push to `main` rebuilds it onto the `gh-pages` branch
([`pages.yml`](.github/workflows/pages.yml)). If the link 404s, Pages needs switching on once: Settings → Pages →
Source *Deploy from a branch* → `gh-pages` / `(root)`.

> *You don't play an empire. You play a family trying to survive one.*

Third-party art in the browser client (public-domain landscapes, CC0 portraits, CC BY icons) is listed in [`CREDITS.md`](CREDITS.md).

**Praetoria** is a human-only galactic grand-strategy / dynasty-roleplay game. You begin as the heir
of a minor noble house holding a single barony, and you climb — through war, merit, intrigue, and
bloodline — toward the throne of a fractured human galaxy. Your character will age and die. Your
*dynasty* is what you are really playing: it outlives every person in it, or it ends.

**Engine:** C# (.NET 8) headless core · **browser client** (Blazor WebAssembly, `src/Web`) · Godot 4.3 desktop layer (`src/Game`). **Status: v0.8.0-alpha — "The War of the Reach".** The whole human game is
built headless and tested — ships, admirals, sieges, wars with score and terms, named heirs who come of age,
succession laws, fostering, assassination, marriage, honors, megastructures, the throne, **the Great Crisis** (a
clocked galaxy-war that sorts the houses into blocs and births an Imperium), family with wills of their own,
advisors who speak from intel, and the saga epilogue — and the Godot layer (creation, Court, Galaxy, dynasty tree)
renders all of it. A balance simulation (`praetoria simulate`) drives tuning. The browser client is the playable build and is
driven through a headless browser for QA every session (`docs/WEB_CLIENT.md`). The Godot layer compiles against the Godot SDK and is
- [`docs/GALAXY.md`](docs/GALAXY.md) — the galaxy: three rings, the capital, allegiance and lawlessness, risings, free holds and free marches.
- [`docs/ECONOMY.md`](docs/ECONOMY.md) — the economy: the ledger, prosperity, belts and claims, ventures and charters, loans, house wealth.
- [`docs/SENATE.md`](docs/SENATE.md) — the Senate as a place of action: motions, weights, whipping, what rivals move and why.
- [`docs/POLITICS.md`](docs/POLITICS.md) — laws by position, their effects, and the six estates that approve or resent them.
wired to the same Core, but Godot's .NET build cannot target the web and it has **not yet been run in a Godot editor** —
treat it as the future desktop shell. See
[What's built](#whats-built-the-current-state), [What's not](#whats-not-built-the-gaps),
[`docs/MILESTONE_3_NOTES.md`](docs/MILESTONE_3_NOTES.md) and [`docs/RELEASE_0.5.md`](docs/RELEASE_0.5.md).

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
6. [Where it's going](#where-its-going-roadmap--expansion-passes) — what comes next, and the later chapters
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
    /Crises /Spheres /Progression /Careers   data models for the §16/§7/§13 catalogs
    /Galaxy /Creation   star map (systems, lanes, fleet markers) · traits, backgrounds, character creation
    /Presentation       CourtProjection — the Godot-free, tested model the Court view renders
  /Tools                headless console harness (play · create · map · demos · validate)
  /Game                 Godot layer — GameRoot (top bar, modes) · CreationScreen · CourtView · GalaxyView · CourtTheme
                        compiles with `dotnet build src/Game/...`; deliberately NOT in Praetoria.sln
/content                events · text · scenarios · galaxy · fleets · holdings · crises · spheres · titles · careers · traits · backgrounds
/tests                  xUnit against Core — runs without Godot
Praetoria.sln           Core + Tools + Tests (everything that builds headless)
validate_praetoria.py   pre-flight content validator (mirrors + extends the engine's gates)
```

The event-engine vocabulary — the condition and effect "verbs" content can use — is documented
exhaustively in [`docs/CONTENT_GUIDE.md`](docs/CONTENT_GUIDE.md); the locked names for traits, skills,
ambitions and ranks are in [`docs/PRAETORIA_VOCAB.md`](docs/PRAETORIA_VOCAB.md).

---

## What's built (the current state)

**In one sentence:** a complete human-only game, playable start to finish in the browser: deterministic, saved and
loaded, soaked for sixty-year runs across every start.

**The current summary is [`docs/ROADMAP.md`](docs/ROADMAP.md)**: what is done, the known gaps, what comes next, and
ideas for later. The table below is the original systems list and is kept for its spec references. Built since it was
written, each with its own doc: the browser client (`WEB_CLIENT`), regions, rings and raiders (`GALAXY`), strategic
resources (`ECONOMY`), the Senate, offices, parties and the consulship (`SENATE`, `REPUBLIC`), laws and estates
(`POLITICS`), military depth (`MILITARY`), nations beyond the Reach (`FOREIGN_NATIONS`), titles by era (`TITLES`), the
Academy as a prologue and tutorial (`ACADEMY`), prices, ventures, companies and shares (`ECONOMY_MARKET`), and the
three orders of the Republic (`REPUBLIC`).

| System | Spec | Status | What it does today |
|---|---|---|---|
| **Event engine** | GDD §15 | ✅ | Role-bound authored events; eligibility from world state; a Director paces by tier (ambient/situation/setpiece), suppresses repeats, raises set-pieces as the run advances; consequences write state so unscripted chains emerge. **Proven:** an insult arms a `feud` flag → a duel that was *impossible* becomes eligible. |
| **Turn loop, pools, commands** | §9, BuildSpec §7 | ✅ | Briefing → Action → Resolve. Finite per-turn action pools (Influence/Treasury/Agents). A command bus shared by player and NPC AI; the run is replayable from its command log. |
| **Domain economy** | §17 | ✅ | Five resources on a house treasury; holdings with specializations and building slots; per-turn accrual/upkeep; population grows to a per-spec ceiling and feeds Manpower ("people are soldiers"); insolvency → unrest → manpower suppression. Build command through the bus; **NPC houses invest in their own domains**. |
| **Crisis system** | §16 | ✅ | Crises as gated states with onset by organic roll *or* authored trigger (NPC houses light fuses); cascades through shared counters; dampers gated on prior play; **natural de-escalation** when a crisis's root cause eases. **Proven:** crushing a revolt arms the civil-war gate. |
| **Power-balance spheres** | §7 | ✅ | Navy/Treasury/Senate influence **derived from member careers**; a threat score (dominance + holdings + seizures); coalition pressure that arms a coalition-war crisis; bridged into the player-facing coalition event chain. |
| **Progression & soft-lock** | §13 | ✅ | A 7-rung title ladder with legitimacy requirements; the three rise paths as commands (Seize / Petition / Claim) at three exchange rates; holding above your legitimacy breeds instability that feeds the crisis web. |
| **Dynasty lifecycle** | §14 | ✅ | Aging; mortality from 50; births to married couples (patrilineal, blood ties; a marriage counts from either spouse's edge); protagonist death → heir handoff (eldest blood child first) with the house persisting; voluntary handover via the `succeed` verb; a child head rules under a `regency` flag. **Ironman** ends the run at dynasty death; **continue mode** (`--continue`) hands the banner to a sworn house. `kill` and `age` verbs for events. |
| **Courtship and marriage** | §14, §18 | ✅ | A suit won over years from the person and from their family; rivals; a bargained contract (dowry, the children's name); promises between children; a warmth that moves and bears on children and strain; divorce. See `docs/MARRIAGE.md`. |
| **The household** | §18 | ✅ | Found family (wards, protégés, companions, adoption), a mentor, and duties (steward, marshal) the head can hand to any member of the house. Kin act for themselves. See `docs/HOUSEHOLD.md`. |
| **Education and skills** | §13, §14 | ✅ | A ceiling in every skill; learning by doing, study and teaching, each step dearer than the last; children brought up toward their bent. See `docs/EDUCATION.md`. |
| **Personal wealth** | §14, §17 | ✅ | A purse for every grown member of the house, the allowance, debts, heirlooms, a will, and the estate kept whole or shared out. See `docs/WEALTH.md`. |
| **Ambitions** | §18 | ✅ | The people of the house pursue what they want by themselves and ask the head for help now and then; a named heir. See `docs/AMBITIONS.md`. |
| **A life of one's own** | §14, §18 | ✅ | Health and wounds, a record of deeds, renown and a name, old friends and enemies, retirement, funerals. See `docs/LIFE.md`. |
| **The ladder of offices** | §6.1 | ✅ | Quaestor, aedile and praetor below the consulship, held by people and filled by election; a consul must have been praetor; relatives stand for themselves. See `docs/LADDER.md`. |
| **Stress** | §18 | ✅ (thin) | Stress bleeds off slowly each year; at the ceiling the character *breaks* (a `broken` flag + `breakdowns` counter) and the authored *Breaking Point* set-piece decides what stands up afterward. |
| **Career ladders** | §13 | ✅ | Tracks and named rank ladders are data (`/content/careers`); `advanceCareer` caps at the ladder top; events gate on `track`; prose names the rung. |
| **Save / load** | BuildSpec §3, §7 | ✅ | The `World` (RNG position included) serialises to a versioned JSON envelope; a resumed run continues the exact same deterministic stream. `play --save` / `--load`. |
| **NPC AI** | §3, §18 | ✅ (basic) | Trait/ambition-driven actions through the same command bus; ambitious NPCs author crises; rival houses slot buildings when they can afford them. Deliberately abstracted (no full NPC economies). |
| **Content pipeline** | §15, §20 | ✅ | JSON loaders, JSONC-tolerant; an engine validator (text pairing, token resolution, the ungated-choice rule) and a stricter Python pre-flight (locked vocab, read-only counters, flag-chain audit in both directions). Both run in CI. |
| **Character creation** | §13, §18 | ✅ | Name, sex, a **background** (origin: legitimacy, treasury, traits, skills, hints), up to two nature and two aptitude traits, track and ambition — all read from data catalogs (`/content/traits`, `/content/backgrounds`) and validated by the engine. `play --background …`, `create`. |
| **Galaxy model** | §19, BuildSpec §3 | ✅ | Systems + hyperlanes as data (`/content/galaxy`); every scenario places its holdings in systems; territory is derived from holdings; neighbours and lane-hop distance; a Godot-free map projection. `map`. |
| **Fleets** | §6, §8, §19 | ✅ | House vs **office** fleets as data; move orders along lanes (one jump a turn, Treasury pool), readiness, per-hull upkeep, *Lay down a hull* at a holding; **incursions** when a fleet enters another house's space; the player's **intel picture** of rival fleets (full confidence in your territory, partial one lane out, decaying out of sight); rival houses raise hulls and hostile heads sail on your seat. |
| **War & combat** | §6, §13 | ✅ | Wars by command, event or a hostile NPC that outguns you. Fleets of warring houses that share a system **fight**: **ship classes** (corvette → battleship, as data) × readiness × **the admiral's skill** × veterancy, defenders boosted by fortress worlds and batteries, a seeded roll, losses screen-first, the loser falling back to port, a defeated admiral wounded or killed. **Sieges**: an unopposed fleet blockades (yield halved) and takes the holding when the clock runs out — the Military path's "take it". **War score** drives tribute and peace refusal; weariness grinds long wars; **titles follow the land**. An NPC's suit becomes *The Envoy* scene. |
| **Dynasty depth** | §14, §18 | ✅ | Children named from house cultures who **come of age** with inherited traits, a track and an ambition; **succession laws**; **fostering** hides kin from events and knives; **assassination** with odds, trails and NPC heir-hunting in war; **marriage proposals** that forge claims; real **regents**; **resentment of the risen**. |
| **Honors & friction** | §13, §18 | ✅ | Five Honorifics & Orders as data, invested when earned. Nature-trait **friction**: declaring war, conquering, plundering, suing for peace or buying a knife costs stress by nature, from a data table. |
| **The Great Crisis** | §11, §16, M6 | ✅ | V1's human archetype as data: a clock driven by intensity and the galaxy's turmoil; stages that sort the houses into the hegemon's bloc and the alliance, declare the bloc war, and resolve it — ending the wars and, in the fractured stars, enthroning the strongest house of the winning bloc. The player picks a side in *The Blocs*. Later archetypes are new files. |
| **Family autonomy & advisors** | §18, §3 | ✅ | Willful kin grow defiant, court your rivals, and may defect with a claim; passed-over siblings resent the heir; elders decline. Three advisor seats speak each turn, the Admiral from the house's intel picture, not the truth. |
| **Strategy & endgame** | §17, §10, §13, M6 | ✅ | The **Exchange**, **tax policy**, territory **contiguity**, **megastructures** gated by title; the **difficulty** knob. **Proclaim or seize the Imperium**: the era flips, office fleets pass with the office, the deposed house is marked, the galaxy's fear peaks. The **saga epilogue** narrates the run. The Director keeps one set-piece per briefing, favours the protagonist's ambition, and stirs a lull. |
| **Court UI (Godot)** | §6, §19, M3 | ◐ compiles, unrun | Creation screen → three-column Court (character card with portrait badge, pools, Steward with build and ship-laying buttons, Admiral, Chancellor with rise-path buttons · briefing feed with choice buttons · dynasty rail, connections, crisis clock with dampers, chronicle) with End Turn / Save / Load, and a **Galaxy view** (drawn map, territory by accent, fleet glyphs — chevron house / diamond office, rivals faded by confidence — click-to-inspect, movement orders). Serif for Court, sans for Galaxy; era-reactive palette. The Court renders a **Godot-free, unit-tested projection** (`CourtProjection`), so its logic is verified even though the window has not been opened yet. |
| **Console harness** | BuildSpec §2 | ✅ | `play` (the whole game as text), plus focused demos for every system, `create`, `map`, `validate`, `version`. |

**The authored content set** (all validated, all in `/content`):

| | Count | Notes |
|---|---|---|
| Events (+ paired prose) | **97** | Academy Crucible 18 · Career progression 16 · Steward/economy 15 · Coalition chain 8 · Succession 8 · Imperial court 3 · Household 8 · Great Crisis 4 · Echoes 17 |
| Scenarios | 6 | …plus **The Old Lion**: a sixty-eight-year-old Duke, a married heir, two grandchildren and a brother who was passed over — three generations in play from turn one |
| Ship classes / honors / backgrounds | 5 / 7 / 6 | all data |
| Scenarios | 5 | `academy_crucible` (the fixture), the three rise-path openings *The Fallen House* (starts married), *The Circling Heir*, *The Risen Officer*, and **the `imperium`-era start *The Imperial Court*** (starts married, under a living Emperor) — every cast sexed, every house titled, every holding placed on the map |
| Galaxy | 1 | *The Reach*: 14 systems, 20 lanes, shared by all scenarios; two frontier systems unclaimed; 13 starting fleets across the five scenarios, one of them an office fleet |
| Backgrounds / traits | 6 / 9+11+8 | character origins; the locked vocabularies as data (nature, aptitude, ambitions) |
| Crises | 5 | local revolt → civil war cascade · coalition war · contested title · court scandal (the wars un-arm the realm when they subside) |
| Holding specializations / buildings | 12 / 12 | |
| Spheres / title rungs / career tracks | 3 / 7 / 3 | the three locked rank ladders are data in `/content/careers` |
| Controlled vocabularies | locked v1.1 | 9 nature traits · 11 aptitude traits · 13 skills · 8 ambitions · 3 tracks with named rank ladders |

**Verification:** 582 automated tests, including whole-stack soaks that assert end-to-end determinism and a save/load
round-trip that must reproduce an uninterrupted run; both content validators clean; every start auto-plays sixty turns
across several seeds with a randomised player. The content counts in the table above are from the first release; the
current ones (261 events, 9 scenarios, 31 systems) are in `docs/ROADMAP.md`.

Per-milestone detail, with the acceptance proof for each: [`MILESTONE_1`](docs/MILESTONE_1_NOTES.md)
(engine) · [`MILESTONE_2`](docs/MILESTONE_2_NOTES.md) (turn loop) · [`MILESTONE_4`](docs/MILESTONE_4_NOTES.md)
(economy) · [`MILESTONE_5`](docs/MILESTONE_5_NOTES.md) (crises, spheres, progression) ·
[`MILESTONE_DYNASTY`](docs/MILESTONE_DYNASTY_NOTES.md) (lifecycle + hardening pass) ·
[`GAP_ANALYSIS`](docs/GAP_ANALYSIS.md) (the 2026-11 audit: what was broken, what was fixed, what remains).

---

## What's not built (the gaps)

Being honest about this is the point of the section. Nothing here is hidden behind a feature flag —
if it's listed, it does not exist yet.

The current, ranked list of gaps is **[`docs/ROADMAP.md`](docs/ROADMAP.md) section 3**, and the dated log behind it
is `docs/GAP_ANALYSIS.md`. In short:

- **The Republic**: no triumph, recall or march on the capital (the Caesar); a magistrate has no work to do in office; the state gives
  commerce no contracts.
- **The economy**: rival houses do not take each other over; some goods are in permanent glut.
- **The Imperium**: government under a throne is simple next to the Republic.
- **The world**: nations beyond the Reach are off the map; anomalies and archaeology are not built.
- **Presentation**: no sound; portraits all wear one uniform; the character sheet is thin; phone layout unchecked.
- **Housekeeping**: browser checks are run by hand; the Godot desktop shell compiles and has never been run.

---

## Where it's going (roadmap & expansion passes)

### Remaining milestones to a complete V1 (Build Spec §8)

| | Milestone | Scope | Acceptance |
|---|---|---|---|
| ✅ | **1 — The Engine Breathes** | headless event engine + Academy Crucible | a choice makes a different, unscripted event eligible later |
| ✅ | **2 — Turn Loop, Pools & Commands** | Briefing→Action→Resolve, pools, command bus, NPCs | a full turn cycle resolves deterministically from a seed |
| ✅ | **3 — Court Mode UI** | browser client ✅ (creation, Court with every command, portraits & sigils, saves) · Godot desktop shell compiles, unrun | the Academy + early court game fully playable via UI — **in the browser** |
| ✅ | **4 — Economy, Holdings & Galaxy** | economy ✅ · succession ✅ · save/load ✅ · galaxy map ✅ (SVG, click-to-move, intel, sieges, blocs) · fleets, war & combat ✅ | play from a barony, move fleets, advance through a succession |
| ✅ | **5 — Spheres, Progression & Crises** | crises ✅ · spheres ✅ · progression ✅ · Bloody Events ✅ (*The Marked Man*, *The Wedding Feast*, the purge) | engineer/defuse a crisis via gates; a damper arrests a cascade; an NPC triggers one |
| ✅ | **6 — The Human Endgame** | ironman/continue · Imperium start + `era` gating · proclaim/seize the throne + era flip · saga epilogue · the Great-Crisis framework with the galaxy-war archetype | a full run from Baron to a throne-endgame, won or lost. **V1-complete on systems** (headless). |
| ◐ | **7 — Reactive UI, Polish, Content Depth** | house accents ✅ · procedural portraits & sigils ✅ · era and role theming ✅ · dashboards ✅ · **audio ⬜ · look-and-feel pass ⬜ (planned in ROADMAP §5) · balance ⬜** | the UI transforms on proclamation; enough content for replayability |

**What comes next is [`docs/ROADMAP.md`](docs/ROADMAP.md) section 4**, grouped by theme (a custom character first); ideas for later expansions are in its
section 6. The working method follows `docs/PROCESS.md`: forty things → QA → graphics → QA → ten QoL fixes → bug
check → reassess, with `docs/GAP_ANALYSIS.md` §4 holding the ranked list the next forty are drawn from.

### One roadmap, nothing for sale
Praetoria is a personal project. There is no paid DLC and no release plan to protect. The five expansions the design
document once listed as DLC (*The Threat From Beyond*, *AI Ascendant*, *Courts of Intrigue*, *The Frontier*, *Wrath of
the Stars*) are later chapters on the same roadmap: see [`docs/ROADMAP.md`](docs/ROADMAP.md) sections 4 and 6 for
their order and what each builds on.

### Slots first, fillers later (GDD §20)
Every extensible system is built as a *generalised mechanism exposed to data*. A later chapter adds **instances**
into slots that already exist; it does not cut the game open to add a new system. You can see this in the code today:
career tracks, spheres, crisis archetypes, holding types, buildings, titles and traits are all data lists the engine
reads, so most future content is little or no engine work.

### The core game: a complete *human* game
Three career tracks, three spheres, the human crises (revolts, coups, civil wars, usurpers, succession wars, Bloody
Events), the gate and cascade system, the Great-Crisis framework seeded with the **human galaxy-war**, the economy,
the dynasty layer, the Republic. **Endgame = proclaim or seize the Imperium and hold the throne.** It is a whole arc
on its own; the later chapters add new axes to it and do not fill holes in it.

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
dotnet run --project src/Tools -- play --auto --turns 10 --save run.json   # save between turns…
dotnet run --project src/Tools -- play --load run.json --turns 10          # …and resume the same stream
dotnet run --project src/Tools -- play --scenario the_imperial_court --continue --turns 60 --auto  # Imperium start, continue mode

# Watch each system prove its design, deterministically from a seed:
dotnet run --project src/Tools -- demo --seed 1          # the cascade: an insult makes an impossible duel possible
dotnet run --project src/Tools -- turn --seed 1          # a full turn cycle with pools + NPC actions
dotnet run --project src/Tools -- economy --seed 1       # holdings accrue; invest in a building; yield compounds
dotnet run --project src/Tools -- crisis --seed 1        # a revolt cascades into civil war; dampers gate on goodwill
dotnet run --project src/Tools -- spheres --seed 1       # an heir's Navy career spikes threat until rivals coalesce
dotnet run --project src/Tools -- progression --seed 1   # the meritocrat rules secure; the conqueror rules a powder keg
dotnet run --project src/Tools -- dynasty --seed 3       # a ruler dies, his heir succeeds, children are born

# Character creation and the map, headless
dotnet run --project src/Tools -- create                                   # every background / trait / track / ambition on offer
dotnet run --project src/Tools -- map --scenario the_imperial_court        # the galaxy as text: territory, lanes, distances
dotnet run --project src/Tools -- play --auto --scenario the_risen_officer --background upjumped --nature Ambitious,Just --aptitude Tactician --name "Kessa Vane"

# The balance simulation (docs/PROCESS.md QA): many seeds, aggregate stats per scenario
dotnet run --project src/Tools -- simulate --scenario all --seeds 10 --turns 50

# The test suite (243 tests, incl. a 40-turn whole-stack soak and a save/load round-trip)
dotnet test

# The Godot UI (needs Godot 4.3 with .NET): open project.godot in the editor and press Play.
# Headless compile check only:
dotnet build src/Game/Praetoria.Game.csproj
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

Highest-value content right now: **Bloody Event set-pieces** (the mechanism is ready; the academy's
*The Marked Man* is the first to use `kill`), **Imperium-court depth** (three events gate on `era` today),
**regency and child-heir scenes** (gate on the `regency` flag), and more **academy / career / succession**
depth toward a 40–60 event opening act. Rules of the road: gate each
track's arc with `track`, spend claims with `revokeClaim`, and leave every event one ungated choice.

## License

TBD.
