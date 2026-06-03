# Praetoria — Game Design Document

**Working title:** *Praetoria* (pending Steam / trademark / domain availability check — confirm before it becomes load-bearing).
**Version:** 0.1
**Status:** Living document. Sections marked `[TBD]` are not yet designed.
**Genre:** Human-only galactic grand-strategy / dynasty roleplay
**Engine (recommended):** Godot 4, C#
**Visual approach:** Text + character portraits (Court) plus a stylized 2D galaxy map (Galaxy). No full 3D.

---

## 1. The Pitch

A human-only galactic grand-strategy game where you play a **noble dynasty — not an empire**. You climb from a minor barony to galactic power through war, merit, intrigue, and bloodline, while an escalating existential threat slowly reframes humanity's petty squabbles into a fight for survival.

**The fantasy:** Crusader Kings character depth × Stellaris systemic depth × *Legend of the Galactic Heroes* / *The Expanse* tone, with Star Dynasties' feudal-space flavor but far more mechanical meat.

**Influences and what we take from each:**
- *Star Dynasties* — human-only feudal space, levels of rulership, family/character focus. (But too shallow — we add depth.)
- *Stellaris* — resource/building depth, megastructures, growing navy, anomalies/special projects/archaeology, replayability. (But too much going on, and no aliens — we abstract and remove aliens.)
- *Crusader Kings* — playing a dynasty with permadeath/succession, personal ambitions.
- *Suzerain* — advisor-driven, decision-scene narrative on imperfect information. (But too scripted/repetitive — we make it systemic.)
- *The Expanse* — tiers of human civilization in tension; a Proto-molecule-style external/alien threat.
- *Legend of the Galactic Heroes* — Galactic Empire vs. free alliance; an over-mighty military house terrifying the old aristocracy.
- *Red Rising* — a single ruling society with internal tiers; the low-born rising by merit and will; a brutal academy crucible; elite honorific orders (Olympic Knights) earned by excellence; resentment of the upjumped by old houses.
---

## 2. Core Design Principles

1. **You play a person/dynasty, not a state.** You don't directly command the empire; you control your dynasty's *position within* it, via roles, titles, and influence.
2. **Family comes first** — and it's a *mechanic*, not just flavor. You can betray the Imperium for your house.
3. **No hard victory.** A roleplay sandbox with milestones and fail states. The "win" is the story you got. You can rise to Galactic Emperor, be ruined by the Crisis, or watch your dynasty collapse.
4. **Replayability over scripting.** Narrative comes from a systemic event engine (state + weighted pools + stats), not a fixed sequence. Avoids Suzerain's content-exhaustion problem.
5. **Extract dynamics, not plot.** Pull *reusable, role-bound dynamics and textures* from influences (Red Rising, Expanse, CK, etc.) — "the gala that turns to slaughter," "tier-tension between factions," "succession crisis" — never a specific saga's one-time plot. The authoring voice-target is Red Rising's tone (formal/ritualized surface over brutal politics); the *machine* must always be able to fire many ways.

---

## 3. The Fidelity Model (the key to small-team viability)

**Deep simulation only where the player touches it. Abstraction everywhere else.**

- **Your dynasty's domain = deeply simulated.** Real resources, real buildings, real tradeoffs. Starts as a single barony.
- **The rest of the galaxy = "advisor abstraction."** Rival realms do NOT track minerals/energy. Each has a handful of high-level stats (Military, Treasury, Stability, Disposition-toward-you, Ambition) that drift via events and light pressure simulation.
- **Information is filtered.** The player learns galaxy state through **advisor reports** — biased, sometimes wrong, politically colored. This gives free fog-of-war and Suzerain-style decision tension as a *feature*.
- The world only recomputes on **turn-advance**, not continuously — cheap for a small team.

---

## 4. Structure: Two Co-Equal Modes

The player toggles between two modes; the toggle is itself a core feature ("zooming" between personal and political).

- **Court mode** — dynasty layer + Political pillar. Character portraits, advisor briefings, Suzerain-style decision scenes, family, ambitions, succession.
- **Galaxy mode** — a real 2D galaxy map. Your holdings, fleet positions, abstracted rival realms as nodes, the Crisis creeping in from an edge. This is where conquest is spatial and meaningful.
- **The advisor report** is the bridge object — read in Court, points to action in Galaxy.

---

## 5. The Two Axes

### Axis 1 — The Dynasty (persists across deaths)
Titles, **family fleet**, **family money**, holdings, standing among houses. This is what survives succession. The save-persistent entity is the *dynasty*, not the character.

### Axis 2 — The Character's Career (resets each generation)
The individual's personal climb through an institution. Each heir starts a fresh career — the engine of generational variety. Losing a Fleet Admiral means the heir starts as a green cadet. Permadeath has real weight.

**Career tracks** (each a ladder of ranks unlocking authority, contacts, and a flavor of decisions):
- **Military:** cadet → officer → ship command → fleet command → Grand Admiral. Decisions: battles, deployments, loyalty-vs-orders.
- **Stewardship / Economy:** administrator → governor → minister → Imperial Treasurer. Decisions: resource policy, corruption, megaprojects.
- **Law / Politics:** clerk → magistrate → senator → consul. Decisions: legislation, trials, voting blocs, intrigue.
- *[DLC tracks]:* Intelligence/Espionage, Church/Ideology, Science/Exploration.

Your career track determines which advisor seat you can eventually occupy yourself, and which pillar you have *native authority* in. A military character is an outsider in politics and needs allies there.

---

## 6. The Three Deep Pillars

Each pillar is genuinely deep and has its own advisor. The player's domain is simulated through these three.

1. **Political / Court** — *Advisor: Chancellor / Spymaster.* Titles, factions, the Senate (or Emperor's favor), marriages, plots, blackmail, voting blocs. Your position in the hierarchy; the Merit/Intrigue rise paths.
2. **Military / Naval** — *Advisor: Grand Admiral / Marshal.* Fleet composition, naval capacity, readiness, the few real wars you fight. The Military rise path and your shield against the Crisis.
3. **Domain / Economy** — *Advisor: Steward / Minister.* Your actual holdings, their buildings, resource flows (energy, materials, population, influence). Where investment decisions live. Funds the other two.

**The economic loop:** Domain generates resources → invest into Military + Political leverage → leverage lets you climb the hierarchy and survive the Crisis → climbing grants bigger Domain → repeat at larger scale.

---

## 7. The Power-Balance System (strategic heart)

Imperial power is divided among **spheres / estates** — Navy, Treasury, Senate (later: Church, Intelligence). Each house holds **influence in each sphere**. Total influence in any sphere is **finite** — your gain is someone's loss.

- Domination of one sphere is checked by rivals' control of others (e.g., Vega holds the Navy, but Corwin holds the Senate's funding/legal authority over it).
- Your **career feeds your house's sphere-influence.** A Vega Grand Admiral spikes the family's Navy control — which frightens other houses, who gang up where you're weak.
- Smart dynasties **diversify across generations** (military father, political son) or **double down** and risk a galaxy-wide coalition forming against an over-mighty house. (The LotGH dynamic.)
- Emergent, replayable political metagame **without simulating rival economies** — rival houses just need sphere-influence values + ambitions, jockeying via the event engine.

---

## 8. The Three Treasuries

The drama lives in the **gap between office-power and house-power.**

1. **Imperial / State resources** — what the *office* controls (e.g., as Grand Admiral you command the Imperial Navy — but it's not yours; you can lose the post).
2. **House / Dynasty resources** — your **family fleet** and **family money**, loyal to your blood regardless of office. Smaller, but permanently yours. What you scheme, flee, or rebel with.
3. **Personal / Character** — individual rank, reputation, contacts, skills.

Classic arc: rise to command the Imperial fleet (office), quietly grow the family fleet (house), then face the choice — stay loyal or turn the office's power toward your blood's ambition.

---

## 9. Time Model & Core Loop

**Turn-based, action-economy style.** Unlimited *interaction* per turn; limited *resources to act with*. You can always do more, but actions spend finite pools:
- **Influence / political capital** — plots, Senate maneuvers, favors.
- **Treasury & resources** — builds, fleet orders, bribes.
- **Advisor attention / agents** — limited concurrent schemes.

A turn (one season/year of game time) flows through phases the player moves freely between:

**1. Briefing Phase (Court).** Advisors present filtered/biased reports about domain, galaxy, rivals, Crisis. Some carry **decision scenes** (Suzerain-style). Reading is free; acting costs.

**2. Action Phase (both modes, free-form).** Spend pools on anything available: Domain (build queue, budgets, special projects), Military (build ships, reposition fleets, prepare/declare war), Political (plots, lobby Senate, marriages, claim titles), Dynasty (assign family to roles, pursue character ambition). Toggle modes as needed.

**3. Resolve / Advance.** Builds complete, resources accrue, fleets arrive, plots mature/get discovered, rival realms drift, events fire, **Crisis clock advances**, character ages (and possibly dies → succession → play as heir).

The loop tightens as the game escalates: early turns = one barony's squabbles; late turns = same loop moving Imperial fleets against an existential threat.

---

## 10. Start Dates / Scenarios

- **Fractured Stars** — no galactic imperium exists; petty human realms squabble. A mid/late path exists to **proclaim the Imperium** (Stellaris-style) and become the first emperor.
- **The Imperium** — a galactic empire already exists; start as a minor noble inside it and climb the existing hierarchy.

---

## 11. The Crisis (escalation arc)

Early game: small human squabbles for advantage. Mid game: information surfaces about a potential external/extra-universal threat (alien, or Proto-molecule-style). Scale broadens toward existential stakes.

Must be **replayable** — not a fixed Suzerain-style script. Design `[TBD — next section]`.

---

## 12. DLC Roadmap (base game built to accept these)

- Planet building deepening.
- AI sentience & uprising (placate / fight / prevent paths).
- Increased "Apocalypse"-scale content (bigger megastructures, planet-killers).
- Additional career tracks (Intelligence, Church, Science).
- Further immersion systems.

Design `[TBD]`.

---

## 13. Progression: Two Ladders + Three Conversion Paths

**Core idea:** Title and Career are two independent currencies. A low-born admiral has Career power but no Title; a useless duke has Title but no Career power. The mid-game is the struggle to **convert what you have into what you lack** — and the three rise paths are the three *exchange rates* for that conversion. Birth is a **soft-lock** (a worse exchange rate), never a wall.

### The Two Ladders (independent)

**Title ladder (dynasty, persists across deaths):**
Landless/Commoner → Knight → Baron → Count → Duke → Archduke/Prince → Emperor.
Each rung = more holdings, more sphere-influence weight (§7), more Senate votes, a bigger seat.

**Career ladder (character, resets each generation):**
~5–6 ranks per track. e.g. Military: Cadet → Lieutenant → Commander → Commodore → Admiral → Grand Admiral. Each rung = command of larger *office* (Imperial) resources, and access to higher-stakes decision scenes.

These rise independently; the gap between them is the drama.

### The Three Rise Paths = Three Conversion Methods

To gain a Title you lack, you need a **claim** + the **power to secure it**. The three paths generate and cash claims differently. They are **blendable** (most ascents mix all three) but each has a **signature payoff and signature risk**:

- **Military — *take it.*** Convert fleet/career power directly into title (win wars, seize holdings, strong-arm the throne). *Payoff:* acquire titles nobody will grant; can break the soft-lock by force (low-born → self-made Emperor). *Cost:* every seizure spikes rival fear → coalitions form against you.
- **Merit — *be granted it.*** Convert achievements + Imperial favor into title (serve brilliantly, get ennobled). *Payoff:* titles arrive with **legitimacy** — undisputed, no coalition. *Cost:* dependent on Emperor's goodwill, slow, hard-capped by the soft-lock (the throne is nearly impossible to *receive*).
- **Intrigue — *inherit / marry / usurp it.*** Convert influence + agents into title via the succession system (marry up, engineer deaths, forge claims, blackmail). *Payoff:* leapfrog the soft-lock through **bloodline** over a generation. *Cost:* discovery is catastrophic; inherited claims can be contested.

### The Soft-Lock = a Legitimacy Modifier (not a gate)

- Each title has a **legitimacy requirement** to hold comfortably.
- Birth sets a baseline legitimacy (high-born = high; low-born = low).
- Holding a title *above* your legitimacy is possible but generates ongoing **instability** (rivals contest it, vassals grumble, upkeep cost).
- The paths raise effective legitimacy differently: **Merit** earns it slowly/cleanly; **Intrigue** launders it through marriage/bloodline over a generation; **Military** ignores it but pays in blood and fear.
- Result: a low-born *can* take the throne, but rules a powder keg until generations of marriage or sustained force stabilize it. A story, not a stat wall.

### Intended Balance (no path should dominate)

- **Military:** fast, highest ceiling — *makes the whole galaxy your enemy.*
- **Merit:** slow, capped — *makes the galaxy your friend.*
- **Intrigue:** medium, sneaky — *catastrophic if caught, generational if patient.*

The **power-balance/coalition system (§7)** is the counterweight that keeps Military from dominating: raw conquest must be self-limiting via fear/coalition mechanics. **This fear response must be designed with teeth.**

### Red Rising–Inspired Refinements

**Resentment of the Risen (soft-lock texture).** The soft-lock isn't a cold number — it's *social contempt*. Old houses don't merely out-rank an upjumped low-born; they **resent** them. A risen character carries a persistent "parvenu" friction modifier with high-legitimacy houses that decays slowly over generations of marriage and accomplishment. Being technically noble is not being *accepted*. This friction feeds directly into the coalition/fear system.

**The Academy Crucible (early-game opening).** Each career track begins in a brutal, gamified proving ground (Naval Academy, magistrate's apprenticeship, administrative corps) where the young character rises against a **cohort of rival young nobles** — who are the heirs of the rival houses. Relationships forged or soured here (allies, rivals, debts, grudges, romances) **persist for the rest of the playthrough and echo across generations.** This gives the game a tight, personal, high-stakes opening before the galaxy opens up — and seeds the rival-house web with personal history rather than abstract opposition.

**Honorifics & Orders (Merit payoff, birth-independent).** Separate from the title ladder: prestige distinctions earned by *personal excellence* — elite martial orders (an "Olympic Knight"–style honor), meritocratic societies, Imperial decorations. They cut across birth, grant authority, open decision scenes, and raise legitimacy. The key Merit-path lever for the low-born: a risen admiral may never be granted a Dukedom, but as a holder of the highest martial honor, **no one questions his right to command.** Undeniable personal prestige even when titles are gated. (Note: distinct from the title-ladder "Knight," which is the lowest landed rung. Different concept, shared word — UI should disambiguate.)

---

## 14. Dynasty Death, Succession & Survival

### Two Layers of Death
- **Character death:** routine, expected — you play the heir. Happens constantly. (The §5 spine.)
- **Dynasty death:** the true fail state — rare, catastrophic. The whole game's tension lives in the gap between these two.

### What Triggers Dynasty Death
The dynasty ends when **no living member of the House name remains.** Anyone sharing your house name/bloodline counts — direct line OR distant cadet cousins ("of House Vega"). A lost war ends you only if it kills your last name-bearer. **The real resource you protect is bodies carrying your name.**

### Two Game Modes (chosen at start)
- **Ironman / Hard-over:** bloodline extinct → run ends → generated **saga epilogue** (a chronicle of the dynasty's rise and fall). The intended "real" experience.
- **Continue mode:** bloodline extinct → adopt a surviving sworn cadet/vassal house and play on. Softer sandbox option.

### Coalition Punishment is Difficulty-Scaled
The §7 coalition system computes a **threat score** for your house (sphere-dominance + recent seizures + holdings-vs-legitimacy). A **difficulty multiplier** scales how aggressively rivals convert threat into coordinated action (alliance speed, willingness to fund your enemies, readiness to commit fleets at a loss). Leans harsh by default. Same system, one knob.

### The "Scatter the Seed" Subsystem (survival strategy)
Heirs are **targetable**, but hunting **hidden kin** is expensive/risky for the attacker. This creates an ongoing strategic choice: **expose blood for power vs. hide it for survival.**
- **Prominent kin** (titled heir, admiral son, senator daughter) — visible, exposed. Power and safety trade off.
- **Hidden kin** (cousin fostered in a backwater, daughter married out under another banner, untracked bastard) — cheap insurance; an enemy *can* hunt them but must first *find* them (intel sub-step), pay agents+time, and risk legitimacy blowback.
- Ties three systems together: **Intrigue path = succession insurance = counter to extermination threat.** The marriage web is your distributed backup, not flavor.

### Heir-Hunting Costs (so it's a campaign, not a cheap dice roll)
An attacker pursuing your bloodline pays: **agents + time** per attempt (attempts can fail/be traced); **legitimacy/fear blowback** if exposed (murdering children galvanizes coalitions *against the murderer* — §7 fear cuts both ways); **diminishing access** (public figures reachable; hidden kin must be located first). A "kill-the-bloodline war" is a terrifying lategame threat the enemy *commits* to — and the player always has counterplay: scatter, hide, marry out, keep a reserve heir.

---

## 15. The Event Engine (replayability core)

**The problem:** achieve Suzerain's authored depth AND Stellaris's replayability — which normally pull against each other. The resolution is a **layered architecture** where authored *pieces* are recombined by a systemic engine: writing feels hand-crafted, but sequence/context/binding never repeat.

### Four Layers

1. **The State (world model).** Everything the engine can see: stats, titles, career rank, legitimacy, sphere-influence, every tracked character + relationships, rival-house threat/ambition, the Crisis clock, and a recent-history log. The engine is a *function of this state*. (Also the only thing that must exist for the engine to run — building it well makes every other system cheaper.)

2. **The Event Pool (authored, conditional).** A large library of authored events, each tagged with **trigger conditions** (state requirements) and a **weight**. Eligible events enter a weighted pool and *may* fire. Because eligibility depends on emergent state, the same authored event lands in different contexts each run. **Critical craft rule: events reference state by ROLE, not by NAME.** Write `{a_rival_of_higher_rank}`, not "Lord Corwin" — the engine binds the variable to whoever fits. One authored event → hundreds of lived experiences.

3. **The Decision & Consequence Grammar (Suzerain depth).** A fired event presents choices. Each choice has **costs** (spend pools — §9), **requirements** (gated by stats/traits/rank — a Ruthless character sees options a Just one doesn't), and **consequences that write back into State.** Consequences are not "+5 minerals" — they **change relationships, set flags, and arm future events.** Insulting a rival sets a `feud` flag → makes *other* events eligible → a multi-event storyline emerges that was never scripted as one. **Narrative arcs are emergent from chained state-writes.**

4. **The Director (pacing & dramaturgy).** A meta-system that biases the pool so runs feel story-shaped, not random: suppress frequency after a big event (breathing room), raise Crisis-event weight as the clock advances, prevent category flooding, inject a complication if things are quiet too long. The difference between "random events" and "rising action." Budgets the player's attention across the three tiers (below).

### Authoring Model: Human Spine + AI Detail
- **Humans own the mechanical spine** of core events/set-pieces: trigger conditions, meaningful choices, consequence-writes (everything that touches the simulation/balance).
- **AI fills the writing inside the spine**: dialogue, flavor, variant phrasings. AI never invents consequences (can't break balance).
- **Hard architectural requirement:** strict separation of **logic** (structured data: conditions/choices/effects, read by the engine) from **text** (a separate, swappable layer keyed to the logic). This makes AI text-generation safe, and makes modding free later.

### Moddability: "Mod-Ready, Not Mod-Featured" (the Paradox lesson)
- **Events live in external data files, NOT hard-coded in C#.** The engine reads events from files at load.
- For V1 this is just *our* authoring workflow — same effort, cleaner. No mod tools, Workshop integration, or docs in V1 (deliberately deferred).
- But the format already exists, so modding unlocks later for near-zero added cost. Paradox's whole ecosystem is "the game reads its own content from editable files." **This is a hard V1 requirement; the tooling around it is deferred.**

### Event Tiers (the "mixed texture")
- **Ambient beats** — small, frequent, light state-nudges. Texture/life. Mostly AI-generated from templates.
- **Situations** — medium, periodic, real choices with real costs. The bread-and-butter. Human-architected, AI-detailed.
- **Set-pieces** — rare, weighty, Suzerain-scale, multi-stage, branching, memorable (Crisis turning points, succession crises, proclaiming the Imperium, academy final trials). Fully human-architected and carefully written.
- The **Director** uses tiers as its pacing tool: beats fill quiet stretches, situations carry the mid-rhythm, set-pieces deploy at dramatic peaks (and suppress nearby collisions).

### First Build Target
**The Academy Crucible (§13)** is this engine with a constrained State and a focused event pool. Build the engine against the Academy first — it validates all four layers in a bounded scope before the full galaxy is wired in.

---

## 16. The Crisis System

**Reframe:** "Crisis" is not one event — it's two layers:
- **Crises (recurring threat system):** the constant drumbeat of trouble at every scale. Always *something* brewing. The galaxy's "weather."
- **The Great Crisis (endgame existential arc):** the singular threat that reframes everything. **In V1 this is purely HUMAN** (a galaxy-spanning civil war / war for the throne — the LotGH Empire-vs-Alliance climax). The cosmic/non-human Great Crisis is **flagship DLC** (see §21). The *framework* is built in V1; cosmic content fills it later.

Crises are largely **an application of the event engine (§15)** — weighty event-chains with escalation logic.

### V1 = a Complete HUMAN Game (critical scoping)
V1 ships the **full human-vs-human game** — civil wars, revolts, coups, usurpers, succession wars, Bloody Events — with **no cosmic Great Crisis.** V1's endgame is the **human apex**: proclaim/seize the Imperium, win the great human wars, become (and hold) the throne. This is a *complete, satisfying arc on its own.* The cosmic threat arrives as DLC that recontextualizes a game players already found whole ("you thought the throne was the endgame — now something worse is coming"). Per the strict "never sell back missing features" rule, **V1 must NOT feel like it's building toward an absent finale.**

### Crises as Gated, Buildable States (the core mechanism — NOT random events)
A crisis (revolt, civil war, succession war, coup) has **preconditions ("gates")** that accumulate in world state — e.g. a civil war's gates: low imperial legitimacy + two houses with high military spheres + a contested succession.
- While gates are **un-cleared**, the crisis cannot fire (no civil wars from nowhere).
- Once gates are **cleared**, the crisis becomes **causable**.

**Crises have authors.** A cleared-gate crisis can fire two ways (mixed origin):
- **Organic:** the Director (§15 L4) rolls it spontaneously from the ripe conditions.
- **Authored:** an actor — **you OR an ambitious NPC house** — deliberately *triggers* it because it serves their ambition. Even big ones (revolutions, civil wars) can be deliberately *caused*.

So every major crisis is simultaneously an **ambient threat** and a **usable weapon**, depending on who's watching the gates. The mid-game gains a layer of *setting up* (engineering gates for the crisis you want) or *defusing* (denying gates for the crisis you fear).

### Cascading + Dampers (escalation as emergent, not scripted)
Crises **cascade through shared gates** — one alters state in ways that arm the next (brutally suppress a revolt → unrest spreads → wider uprising gate clears → imperial legitimacy drops → civil-war gate clears → a house triggers it). The small→galaxy-spanning escalation arc emerges from the **gate-cascade**, not a scripted clock.
- **Dampers** arrest cascades, scaled by **difficulty AND the player's prior choices.** A ruler who kept legitimacy high and built goodwill has dampers available (defuse, buy time, rally support); the tyrant who clawed up through brutality/resentment (§13) has *fewer* — they spent their goodwill climbing. **The escalation you face is partly the bill for how you played.** (LotGH/Red Rising theme: how you gained power determines whether you can hold it.)
- Dampers = function of (difficulty × accumulated state from prior choices).

### Strategic Weight: One Layer, Not the Whole Game (guardrail)
Gate-engineering is *a* powerful lever, NOT the entire mid-game. The mid-game is also career climb, economy, dynasty, wars. The gate system **rewards attention without demanding it** — play reactively (deal with crises as they come) or proactively (engineer them); both viable. Must not collapse the game into "min-max the preconditions."

### Crisis Scale Tiers (also stage-gated by position)
| Scale | Examples | Threatens |
|---|---|---|
| **Personal** | assassination plot, duel, blackmail, scandal | your character/heir |
| **House** | succession dispute, internal coup, family betrayal | your dynasty |
| **Regional** | vassal revolt, peasant uprising, rival's war, bloody wedding | your holdings |
| **Imperial** | civil war, throne usurper, Senate schism | the political order |
| **Existential** | the human war for the throne (V1) / cosmic Great Crisis (DLC) | the galaxy / humanity |

Tiers unlock via *time* + *your title/power* + gate-state. A landless cadet faces Personal/House crises; a Duke faces Imperial ones. **The bigger you get, the bigger the targets on your back.** Ties Crisis to the progression ladder (§13).

### The Great Crisis Framework (built in V1, seeded with human content)
A reusable **archetype framework** (built V1, future-proof per the "all foundations in V1" rule):
- **V1 archetype:** the **human galaxy-war** (great civil war / Empire-vs-Alliance for the throne) — runs on the same gate/cascade system.
- **DLC archetypes** (§21): cosmic — **Swarm/Incursion** (advancing front), **Plague** (spreading nodes), **Awakening** (dormant sites erupt — payoff of §17 anomalies), **AI Uprising** (defection/conversion). Each has signature map behavior (§19).
- **Determination:** setup choice to **specify** an archetype OR **hidden/random** (revealed via mid-game foreshadowing — default for dread).
- **Timing — soft clock + irreducible exogenous floor.** Agency *reduces* risk and *improves* readiness but never eliminates the threat; some triggers are **exogenous** (fire on the world's schedule). You can be *ready*, not make it *not come*. (Applies fully to DLC cosmic crises; the V1 human war is more player-/NPC-authored via gates.)

### Intensity Setting (setup slider: off / low / standard / dominant)
Scales how dominant the Great Crisis (human war in V1, cosmic in DLC) is in the late game. Preserves the dynasty-first fantasy: dial down for a pure court-intrigue power game, up for an all-consuming war/apocalypse. One slider, both audiences. (Third personalization axis with resource emphasis §17 and family autonomy §18.)

### Bloody Events (a dedicated set-piece class)
Rare, devastating, structured like the Red Rising gala:
- **Setup:** a high-society gathering (gala, wedding, tournament, coronation) framed as *safe* — guards lowered, rivals in one room.
- **The turn:** sudden shocking violence. Rare enough to always shock.
- **The consequence:** **arms a larger crisis** (clears gates — a bloody wedding ignites civil war; a massacred gala triggers a coup). Escalation is **player-optional**: absorb it or escalate. A fork, not a railroad. (Direct expression of the gate system — a Bloody Event is a dramatic gate-clearing trigger.)
- **Both sides of the knife:** player can be victim OR author. *Plan* a gala massacre to decapitate a rival house (high-risk Intrigue/Military, enormous payoff, catastrophic fear/legitimacy blowback §7/§14) — or walk into one and survive.

---

## 17. Resources & Holdings

**Design goal:** Stellaris-flavored *depth* without Stellaris-grade *breadth*. This deep economy exists ONLY for your own holdings (§3 fidelity model) — narrow scope buys us richness. Each resource is entangled with the pillars and dynasty layer, not just a build-queue feed.

### The Five Resources
1. **Credits (liquid wealth)** — *from:* trade hubs, taxes, populated worlds. *Spent on:* upkeep (per-turn drain), bribes, plots, ships, loans. *The one you run out of.* Negative balance forces ugly choices: raise taxes (unrest↑), sell a holding (house power↓), borrow from a rival (their leverage↑).
2. **Materials (alloys/build-stuff)** — *from:* forge-worlds, mining holdings. *Spent on:* buildings, ships, fortifications, megastructure stages. *Chain:* mining → forge-world → Materials → shipyard → fleet (the Domain→Military pipeline). Military houses need forge-worlds.
3. **Manpower (population)** — *from:* agri-worlds/populated holdings (grows over time). *Spent on:* crewing fleets, garrisons, armies. **Has loyalty/unrest** — it's *people*. Overtax/lose-war/plague → unrest↑ → uprising & civil-war crises (§16) eligible. Both your strength and your most volatile liability.
4. **Influence (political capital)** — *from:* titles, standing, honorifics, Senate seats, marriages. *Spent on:* Senate maneuvers, title claims, marriages, schemes. Currency of the climb (§13). Resource-rich/influence-poor (rich but unrespected) vs. the reverse (prestigious but broke) — the gap drives decisions.
5. **Exotics (strategic spice)** — *from:* rare; specific systems + anomaly/archaeology finds (not buildable at will). *Spent on:* megastructures, elite/flagship fleets, **Crisis countermeasures**. Scarcity = casus belli; the Great Crisis disrupts/demands it. (DLC: new exotics per expansion.)

Conversion-chain fun lives mainly in **Materials** (production pipeline) and the **Credits/Manpower/unrest** tension. Exotics are the gated late-game key. Influence is the political throughline. The **three treasuries (§8)** apply on top — each resource exists in *Imperial* (office) and *House* (yours); manage the gap.

### Variable Emphasis (tuning principle — do NOT balance to uniformity)
The five resources are **not equally relevant every game** — emphasis shifts with the player's path, career track, and difficulty/Crisis settings. A conquest run lives on Materials + Manpower; an intrigue court game runs on Influence + Credits and barely touches Materials; a high-Crisis run makes Exotics the thing everyone fights over, while a low-Crisis political run treats them as a footnote.
- **Tuning rule:** balance so each resource becomes *dominant under some configuration of path + settings*; none should be dead weight in *every* run.
- **Failure mode to cut:** a resource marginal in *all* playthroughs. **Working as intended:** a resource marginal in *most* runs but decisive in *some* (e.g. Exotics).
- This reinforces build differentiation across the three rise paths (§13): if every resource mattered equally always, the paths would converge on one economic optimum. Variable emphasis keeps a military house and an intrigue house playing genuinely different economic games.

### Holdings
A **holding** = a place you control (planet, station, moon, orbital). Each has:
- A **type/specialization** (agri-world, forge-world, fortress, trade hub, research station — simplified specialized-planet idea)
- A few **building slots** (you choose what to build — "boost energy" = power plant; "boost navy capacity" = shipyard/dock)
- **Population** with loyalty/unrest
- A **strategic map position** (matters for conquest, defense, trade)

Start = **one barony = one modest holding.** Climb the title ladder → gain holdings (granted/conquered/inherited/married-in). Domain pillar manages this growing portfolio.

### Management Depth: Medium Baseline + Optional Depth
- **Baseline (medium):** manage build slots, budgets/taxes, population; the **Steward advisor surfaces the big decisions** as reports/choices ("Energy dry in 3 turns; raise taxes / sell holding / take a rival's loan?"). Depth lives in *decisions*, not clicking.
- **Optional depth:** a player who *wants* to fine-tune (deeply specialize a world, optimize a production chain) can dig in. Medium by default, depth available on demand.

### Megastructures: Scaled Down + DLC
Base game = a **few iconic ones** as rare, weighty, multi-stage, Exotic-hungry set-piece projects (a real achievement to finish) — NOT Stellaris's full catalog. Deeper catalog + "Apocalypse"-scale structures = DLC (§ DLC roadmap).

### Anomalies / Archaeology = the Crisis-Foreshadowing Engine
Their **primary purpose is to seed Great-Crisis foreshadowing** (not "+5 research" filler). The dig that uncovers a dormant horror; the anomaly that's the first sign of the plague; the ancient warning about the swarm — **exploration is how the galaxy whispers the coming Crisis before it arrives.**
- Implemented as **event-engine content (§15)**, tier: situations/set-pieces.
- The main mid-game delivery vehicle for the foreshadowing that §16's hidden/random Great Crisis depends on.
- *Also* occasionally yields Exotics and lore — but the *point* is dread, not loot.
- **Consolidation win:** exploration, the exotic economy, and Crisis foreshadowing become ONE system, not three.

---

## 18. The Dynasty Layer

**Design stance:** family members are **people, not puppets** — the most ambitious version, and the source of the CK "my heir is plotting with my rival" magic. Tunable in intensity (see safety valve below) so it never becomes misery.

### Characters as Two Classes of Traits
- **Nature traits (constraining, stress-bearing)** — identity traits that *are* the character: Honorable, Cruel, Zealous, Craven, Devoted, etc. Acting against them costs **stress/strain** (CK-style). An Honorable character who breaks a sworn oath suffers real internal cost; a Devoted one who abandons family to advance their career is mechanically torn. **This is what makes "family comes first" *felt*, not stated.**
- **Aptitude traits (gating/flavoring, free)** — skill/disposition traits: Diplomat, Strategist, Greedy, etc. Gate or flavor options, give bonuses, color AI — but *no* penalty for acting against them. A Greedy character isn't pained by generosity; they just rarely choose it.
- (Better than CK's blanket model: constraint where identity matters, freedom everywhere else.)

### Skills / Development Tied to Career
Characters grow along their career track (§13) over their life. The **Academy Crucible (§13)** is where development *starts* and where Nature traits first crystallize (the proving ground shapes who they become).

### Two-Axis Loyalty Web (the Red Rising heart)
- **Blood ties** — given, not chosen; carry inheritance/succession rights (§14); loyalty NOT guaranteed (an ambitious blood heir can be your enemy).
- **Sworn bonds** — chosen, earned (forged at the Academy, through shared war, mentorship, oaths); carry NO inheritance rights but can carry *fiercer loyalty than blood*. Your sworn shield might die for you; your cousin might poison you.
- **The drama is in crossing the axes:** the blood heir you distrust vs. the sworn friend you'd die for who can't inherit. Can you convert a sworn bond into a blood one (adoption, marriage into the house)? Do you pass over your own son for someone who earned it?
- **Ties to scatter-the-seed (§14):** sworn bonds are *also* survival infrastructure — a sworn vassal hides your heir loyally; a resentful cousin sells them out.

### Control Model: Directed, But With Will
You can **direct** family (assign roles, propose marriages, request actions), but they can **resist, negotiate, or defy** based on traits + ambition + their bond/loyalty to you. A loyal low-ambition daughter complies; a brilliant weakly-bonded heir may refuse a marriage, demand a better holding, or pursue their own ambition behind your back. **Loyalty + ambition + traits determine obedience** — which makes *earning loyalty* (sworn bonds, good treatment, shared cause) a real investment.

### Personal Ambitions (family members generate their own story)
Each significant character has a **personal ambition** that generates their own goals and events through the event engine (§15). An heir may want command of a fleet; a daughter may want a throne; a sibling may want *your* seat. These ambitions can **align with or conflict with** the player's — producing emergent internal drama (the loyal brother, the scheming son) without scripting.

### Marriage & Fostering
- **Marriage** = alliance + bloodline engineering + claim acquisition (§13 Intrigue path) + converting sworn/political ties into blood. Matrilineal/patrilineal matters for which house heirs belong to.
- **Fostering** = placing young kin with mentors/houses — shapes their traits/skills (who trains them matters, per Red Rising), forges sworn bonds, AND functions as scatter-the-seed placement (§14): foster a spare heir somewhere safe and loyal.

### Tuning Safety Valve: "Family Autonomy" Intensity (setup slider)
Autonomous ambitious family can frustrate, so intensity is tunable (like Crisis intensity §16):
- **Low:** family largely defers; ambitions are gentle background color.
- **High:** a true CK pressure-cooker of scheming relatives.
- Default mid. Protects the player who wants the *fantasy* of a great house without the *headache* of constant betrayal; lets the hardcore player crank it. (Third independent replayability/personalization axis alongside Crisis intensity and resource emphasis.)

---

## 19. UI / UX & Art Direction

### Two-Mode Layout (Court mode — established)
Persistent **top status bar** (character identity: name/title/career/house/age; the five resources §17 at a glance; the **Court/Galaxy toggle** §4 — always present in both modes). Court mode is a three-column layout:
- **Left rail:** the three pillar advisors (§6) with report counts + status flags (e.g. Steward flags "energy low"); below, the **action pools** (§9: Influence, Treasury, Agents).
- **Center: the Briefing feed (§9, the heart)** — the only thing that changes turn-to-turn (rails are stable status; feed is the living game, so player attention always lands in one place). Reports stack by event tier (§15): set-piece decisions (full choices + pool costs + trait-gated options) → situations → ambient beats.
- **Right rail:** the dynasty layer (§18) — key family with bonds/dispositions (blood vs. *sworn*, loyalty flags), standing (title/career/legitimacy), and the **Crisis clock** (§16).

*(A flat dashboard-style wireframe exists as LAYOUT REFERENCE ONLY — it does not represent art direction; see brief below.)*

### Dynasty Viewing — Low-Frequency Management, Contextual Connections (revisitable)
**Dynasty is managed, not a core gameplay loop** — you set it up and it largely ticks along; not constantly fiddled with. So it does NOT need its own mode. BUT family members are **connective tissue**: they carry your influence and are your links into the spheres and other houses.

**Key principle — connections are contextual, not centralized.** Rather than siloing family in one screen you must visit, the relevant connection surfaces *wherever it's relevant, at the point of use*:
- Viewing the Senate → see which kin/sworn bonds sit in it; act through them inline.
- Eyeing a rival house (Galaxy/panel) → see your tie to them (daughter married in, sworn friend serving there, fostered cousin) and the leverage it gives.
- A decision scene involves House Corwin → your existing Corwin ties surface inline; never dig through a tree to recall who you know where.

Family is **infrastructure you act *through*, displayed in context** — not a destination you travel to. "You should easily be able to find connections anywhere."

Right rail = summary of key figures. A **full dynasty tree overlay** still exists, summonable from either mode, for when you *do* want the whole picture (succession planning, checking who's hidden where §14) — but day-to-day family use is woven into the other screens as contextual connection-surfacing. Preserves the two-mode duality (§4); extends the §18 principle (family surfaced through the event engine) to the interface itself. *(Revisit only if dynasty management becomes active enough to deserve a permanent mode — currently it does not.)*

### Art Direction — Diegetic & Reactive (the core concept)
**The UI is not a static skin — it reflects game state and evolves as you play.** Register shifts by **era** and **role**:

**By era / political state:**
- **Fractured Stars (pre-Imperium):** feudal, fragmented, provincial; regional heraldry; rougher, less unified language; *your house identity dominates* (no greater authority above it).
- **The Imperium (post-proclamation):** a unifying imperial visual order asserts itself — standardized iconography, the imperial sigil, a grander ceremonial register layered *over* house identity. **The UI visibly transforms at the moment of proclamation** — chrome shifts from patchwork-regional to a single imperial standard. Makes proclaiming the Imperium feel momentous: the game's whole look changes.

**By character's job (changes each generation as career resets §13):**
- Admiral → functional/military (naval consoles, tactical readouts, dispatches).
- Governor/Steward → administrative (holdings, ledgers, civic tone).
- Senator/Politician → ceremonial/courtly (the chamber, ornate framing).
- Same underlying screens; chrome + emphasis reflect what the character *is*. A military generation's game *looks* different from the intrigue generation that follows.

### Palette — Dark + Warm + House Accents
- **Base:** dark and warm (NOT cold black-space). Candlelit-court-meets-starship: deep warm darks (charcoal-brown, oxblood, bronze-black), Red Rising opulence. Atmosphere, not dashboard. (Explicitly NOT flat white business UI.)
- **House accents:** your house color threads through borders/sigil/highlights/holding-glow as a single themeable accent variable. Rival houses render in *their* colors — the map/court reads as a clash of heraldic palettes. Personalizes every playthrough's look; cheap to implement.
- **Imperial gold:** a third register that enters *only* once the Imperium exists — the throne's color, overlaid on the house-accent world.

### Typography — Ceremonial Court / Functional Galaxy
- **Court mode:** ceremonial — serif/engraved display face for titles, names, decision-scene headers. Weighty, dynastic, reads like a proclamation or house chronicle. The operatic Red Rising tone.
- **Galaxy mode:** functional — technical sans, military-dispatch clarity, tactical readouts.
- The contrast makes mode-switching feel like switching contexts: throne room ↔ war room.

**Principle:** art direction is *systemically driven* — the look is a function of era + role + house, exactly as gameplay is a function of state.

### Galaxy Mode Layout
**Purpose:** Court is where you *decide*; Galaxy is where decisions become *spatial*. It is a **command table, not a 4X map** — you're a noble reading an intelligence picture and committing fleets/resources to positions, not painting tiles. Same persistent top status bar carries over.

- **Center — the star map (dominant element).** Node-and-lane galaxy, **"Star Dynasties+"**: Star Dynasties' clean, legible bones (systems as nodes, hyperlanes as links, small curated scale — NOT Stellaris sprawl) fleshed out with more per-system depth (systems may contain multiple holdings), heraldic territory shading (house accents §19), and Crisis presence. A *curated* galaxy of meaningful systems, not procedural filler.
- **Left rail — contextual detail panel.** Click a system/fleet/holding → details here. Own holding: specialization, buildings, population/unrest, build queue (§17 management surfaces here). Rival system: *reported* intel + confidence. Fleet: composition, orders, readiness.
- **Right rail — fleets & orders.** Your fleets with the **house vs. office distinction (§8) made visually explicit** (different sigils for your house fleet vs. an Imperial fleet you merely command); status, orders; a queue of military actions committed this turn (spending pools §9).
- **Bottom/overlay — intel ticker.** Map-relevant advisor flags ("Corwin fleet sighted near Vesper — confidence: moderate"), tying Galaxy back to the Court briefing.

### Map Visibility — Mixed (the war-room plot)
**Territory is visible** (who owns what is public knowledge — map stays readable, player never lost on the political situation). **Fleet positions and strength estimates are uncertain** — advisor-reported, confidence-flagged, sometimes wrong (per §3 abstraction). Fog sits on the *dangerous* info (military intel), not basic geography. Feels like an Expanse/LotGH command-table plot — *your intelligence picture*, not ground truth. Full detail only on your own deeply-simulated holdings/fleets.

### Great Crisis — Archetype-Driven Map Behavior
Each Great-Crisis archetype (§16) has a signature *visual behavior* on the map, so late-game runs look and feel distinct:
- **Swarm/Incursion** → an **advancing front** from a galaxy edge; directional, you watch it march.
- **Plague** → **spreading corruption across nodes**; jumps along trade/hyperlanes, infects non-contiguously, unpredictable bloom (not a clean front).
- **Awakening** → **stirs from within**; dormant sites (seeded earlier by anomalies/archaeology §17) activate across the interior — threat erupts from the systems the player investigated mid-game. (Direct payoff: foreshadowing digs become eruption sites.)
- **AI Uprising (DLC)** → **defection/conversion**; your own systems/fleets turn — threat from inside your territory.

Early game the Crisis is absent/rumored (anomaly markers only); late game it has full spatial presence.

---

---

## 20. Scope Split: V1 / Free Updates / Paid DLC

### Architectural Principle: "Slots in V1, Fillers in DLC"
**Build the *skeleton* for everything in V1; ship only *some of the meat*.** Every extensible system is built as a *generalized mechanism* exposed to data in V1; DLC adds *instances* into slots that already exist — never cuts the game open to add new ones. This is the Paradox-longevity model and the whole reason V1 must contain "99% of UI and foundations."
- **Career tracks** — V1 builds a track *system*; ships 3 (Military / Stewardship / Law). DLC adds tracks by data.
- **Spheres** — power-balance (§7) holds N spheres; ships 3 (Navy / Treasury / Senate). DLC adds spheres.
- **Crisis archetypes** — framework + archetype→map-behavior (§19) built V1; ships with the **human galaxy-war** archetype. DLC adds cosmic archetypes as content.
- **Event engine** — already data-driven/mod-ready (§15), so *all* DLC narrative is just more data files. (Most DLC content = zero engine work.)
- **Exotics, megastructures, building types, holding specializations, traits, house templates** — all data-driven lists in V1, extensible by DLC.

### V1 — A Complete HUMAN Game (richer human side; 3+3)
Full human-vs-human game: 3 career tracks, 3 spheres, complete human-crisis suite (civil wars / revolts / coups / usurpers / succession wars / Bloody Events), the gate/cascade crisis system (§16), the Great-Crisis framework *seeded with the human galaxy-war*, both modes, full reactive UI, complete event engine, base economy & holdings, dynasty layer. **Endgame = proclaim/seize the Imperium and win/hold the throne.** A complete, satisfying arc with no DLC required.
- **Intrigue safeguard:** intrigue-*as-playstyle* ships in V1 (the Intrigue rise-path §13, plots, marriages, Bloody Events) so the political game feels complete. Only the **dedicated Intelligence sphere + Spymaster career track** (deep counter-intel as a *profession*) is reserved for DLC. Intrigue itself is NOT sold back; the *specialized profession* is the deepening. (Keeps the strict "never sell back missing features" rule.)

### Free Updates (live-game goodwill)
Balance, bug fixes, QoL/UI improvements — AND **ongoing free event/story content** (events are data §15, so free human-political event drops are core to keeping the base game alive between paid releases). Possibly one free track or archetype post-launch as good-faith signal.

### Paid DLC Slate (5) — each adds a NEW axis, never restores a missing one
1. **The Threat From Beyond** *(flagship)* — the entire cosmic layer: non-human Great-Crisis archetypes (Swarm / Plague / Awakening), the anomaly-foreshadowing payoff (§17), archetype map-behaviors (§19). Recontextualizes a game players already found complete. *Fills: Crisis-archetype slots, map-behavior slots. Zero new engine systems.*
2. **AI Ascendant** — the AI-sentience Great Crisis (placate / fight / prevent paths) + a Synthetics/Inquisitor career track; "defection/conversion" map behavior. *Fills: archetype slot, career-track slot.*
3. **Courts of Intrigue** — the **Intelligence/Espionage sphere + Spymaster career track**, deep plot/counter-intel mechanics, secret societies, more Bloody Event set-pieces. *Fills: sphere slot, career-track slot, event pool.*
4. **The Frontier** *(planet-building)* — deeper holding specialization & building trees, terraforming/colonization, new holding types, more anomaly/archaeology content. *Fills: specialization/building/anomaly lists (§17).*
5. **Wrath of the Stars** *(Apocalypse)* — planet-killers, the deep megastructure catalog held back from V1 (§17), doomsday-scale content, higher Crisis-intensity ceiling, new Exotics. *Fills: megastructure/exotic lists, intensity ceiling.*

*(Sixth candidate — "Houses of Legend": legendary named houses, bespoke dynastic mechanics, legacy perks, more traits. Reserve.)*

---

## 21. Outstanding Sections To Design

- [x] Progression ladder + three rise paths — *see §13*
- [x] Dynasty death / succession / survival — *see §14*
- [x] Event engine (systemic narrative core) — *see §15*
- [x] Crisis system — gated/cascading, human V1 / cosmic DLC — *see §16*
- [x] Resources & holdings — *see §17*
- [x] Dynasty layer (traits, bonds, ambitions, marriage, fostering) — *see §18*
- [x] UI/UX & art direction — *Court + Galaxy, art direction, dynasty-view (§19)*
- [x] Scope split: V1 / free / DLC — *see §20*
- [x] **Claude Code build spec — see companion document `BuildSpec.md`**

**DESIGN COMPLETE.** All systems specified, scoped (V1/free/DLC), and given a staged build order in the companion build spec.
