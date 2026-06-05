# Praetoria — Content Authoring Guide

This is the definitive list of **what content the engine can load today**, the **exact schemas**, the
**complete condition/effect vocabulary**, the **controlled vocabularies** to lock down, and a
**prioritized backlog** of what to write. It also calls out, honestly, what has **no engine slot yet**
so no writing is wasted.

Everything here is grounded in the current Core (`/src/Core`). When the engine grows (military layer,
spheres, crises), this guide gets new sections — it won't silently drift.

> **Golden rule:** the engine only accepts the vocabulary documented here. Anything outside it throws
> a `ContentException` at load and fails `validate`. Author *against this doc*, run `validate`, ship.

---

## 0. How content works

- **Logic/text split (GDD §15).** Every event is two records joined by `id`: machine logic in
  `/content/events/*.json`, human prose in `/content/text/*.json`. They are loaded by separate passes
  and never mixed. This is what lets prose be rewritten/localized without touching balance.
- **Data is the moddable surface.** Code provides *mechanisms* (the condition/effect verbs); data
  provides *instances* (the actual events). You can author the entire game in JSON without C#.
- **Validation is a hard gate.** `dotnet run --project src/Tools -- validate` checks: every event has
  text, every choice has text, no orphan text, and every `{role.field}` token references a real role.
  Errors fail CI. Run it after every batch.
- **Determinism.** Selection is a single seeded weighted draw. Same seed + same content = same run,
  exactly. Your content never introduces randomness the engine can't reproduce.

### Where files live
```
/content
  /events      *.json   — event LOGIC (when it fires, choices, effects)
  /text        *.en.json — event PROSE (title, body, choice labels)   [.en = English locale]
  /scenarios   *.json   — starting situations (houses, characters, holdings)
  /holdings    *.json   — economy catalog (specializations, buildings)
```
File names don't matter to the engine (it loads every `*.json` in each dir); group by theme for sanity
(e.g. `academy_crucible.json`, `steward_decisions.json`).

---

## 1. Events — logic file

`/content/events/<theme>.json`:
```jsonc
{ "events": [
  {
    "id": "barracks_confrontation",      // REQUIRED. unique, snake_case. Joins to text + flags.
    "tier": "situation",                 // "ambient" | "situation" | "setpiece". default "situation".
    "weight": 4,                         // selection weight (relative, within the eligible pool). default 1.0
    "repeatable": false,                 // default false = fires at most once per run
    "roles": [                           // optional. "self" (the protagonist) is ALWAYS pre-bound.
      { "name": "rival",
        "when": [                        // this role binds to any character satisfying ALL of these
          { "type": "relationship", "from": "self", "to": "rival", "op": "lte", "value": 0 }
        ] }
    ],
    "when": [                            // event is ELIGIBLE only if ALL of these hold (and all roles bind)
      { "type": "eventFired", "event": "first_formation", "value": true }
    ],
    "choices": [
      {
        "id": "mock",                    // REQUIRED. unique within this event. Joins to choice text.
        "requires": [ /* conditions */ ],// optional. choice is shown-but-disabled unless ALL hold
        "cost": { "influence": 1 },      // optional. action pools: influence | treasury | agents
        "effects": [ /* effects */ ]     // optional. state writes applied when the choice is taken
      }
    ]
  }
]}
```

### Tiers (the Director's pacing lever — GDD §15)
| tier | feel | use for |
|---|---|---|
| `ambient` | small, frequent, light nudges | texture, mood, tiny relationship/stress drifts |
| `situation` | medium, periodic, real choices with costs | the bread-and-butter; most events |
| `setpiece` | rare, weighty, memorable | duels, oaths, betrayals, crisis turns |

**Pacing math you can rely on (the `Director`):**
- Set-piece weight *rises* as the run advances: effective weight ×= `1 + min(2.0, turn × 0.15)`. So
  set-pieces are unlikely early and increasingly likely late (rising action). Author them assuming
  they'll surface in the back half.
- An event that fired in the **last 3 turns** is suppressed ×0.15 (anti-flooding). Repeatable events
  still recur — just not back-to-back.
- Otherwise authored `weight` is honored directly. Bigger = more often. Use this for "the common
  scene" vs "the rare one."

### Roles & binding (the heart — "by role, not by name", GDD §15)
- `self` is always the protagonist. You never declare it.
- Each declared role binds to **any character** satisfying its `when`. One authored event becomes many
  lived ones: `{rival.name}` is whoever fit *this* world.
- Roles bind **in array order**, and a later role's `when` may reference an earlier-bound role (and
  `self`). Example: a role `superior` with `{ "type": "rank", "role": "superior", "op": "gt",
  "vsRole": "self" }` binds someone outranking you.
- **If any role can't bind, the event is ineligible** — it simply won't fire. This is a feature: an
  event about a rival only happens when a rival exists.

### Choices
- `requires` gates *visibility-as-enabled*: a failing requirement shows the choice **disabled** (the
  player sees the locked option — "you'd need rank 3"), it doesn't hide it. Trait/skill/rank gates are
  the texture of §13/§18 ("a Ruthless character sees options a Just one doesn't").
- `cost` spends **action pools** (per-turn budget, §9), not resources. A choice is only *available* if
  requirements hold **and** pools are affordable.
- `effects` apply atomically when taken, through the command bus (logged for replay).

---

## 2. Events — text file

`/content/text/<theme>.en.json`:
```jsonc
{ "texts": [
  {
    "id": "barracks_confrontation",      // MUST match the event id
    "title": "Words in the Barracks",
    "body": "The cohort falls quiet as {rival.name} of {rival.house} blocks your path...",
    "choices": {
      "mock":       "Humiliate {rival.name} in front of everyone.",
      "extend_hand":"Offer a hand. Rivals can become shields."
    }
  }
]}
```

### Tokens (substituted against the live binding)
Only these resolve. Anything else is left intact **and flagged by the validator** (so typos are caught):
| token | becomes |
|---|---|
| `{role.name}` | the character's name |
| `{role.house}` | the character's house name |
| `{role.rank}` | the character's career rank (number) |

`role` is `self` or any role you declared. Example: `{self.name}`, `{rival.house}`.

---

## 3. Condition reference (complete)

Used in event `when`, role `when`, and choice `requires`. **All must hold** in a `when` array (implicit
AND). Comparison `op` ∈ `eq, neq, lt, lte, gt, gte` (symbols `==, !=, <, <=, >, >=` also accepted).

| `type` | fields | meaning |
|---|---|---|
| `all` | `of: [conditions]` | logical AND (empty ⇒ true) |
| `any` | `of: [conditions]` | logical OR (empty ⇒ false) |
| `not` | `of: condition` | negation |
| `const` | `value: bool` | literal true/false (testing/scaffolding) |
| `worldFlag` | `flag`, `value:bool` | is a world flag set? |
| `charFlag` | `role`, `flag`, `value:bool` | is a per-character flag set? |
| `relationship` | `from`, `to`, `op`, `value` | directed disposition compare (−100..100) |
| `bond` | `from`, `to`, `bond: blood\|sworn`, `present:bool` | is there a formal bond of that type? |
| `skill` | `role`, `skill`, `op`, `value` | skill-level compare |
| `trait` | `role`, `trait`, `kind: nature\|aptitude\|any`, `present:bool` | does the character have the trait? |
| `rank` | `role`, `op`, `value` **or** `vsRole` | career-rank compare (literal, or against another role) |
| `turn` | `op`, `value` | current turn number compare |
| `counter` | `key`, `op`, `value` | world-counter compare (gate accumulators, clocks) |
| `resource` | `role`(default `self`), `resource`, `op`, `value` | house-treasury compare (GDD §17) |
| `sphere` | `role`(default `self`), `sphere`, `op`, `value` | house sphere-influence compare (GDD §7) |
| `title` | `role`(default `self`), `title`, `present:bool` | is the house's title **exactly** this rung? (GDD §13) |
| `claim` | `role`(default `self`), `title`, `present:bool` | does the house hold a claim to this title? (GDD §13) |
| `eventFired` | `event`, `value:bool` | has an event already fired this run? |

`resource` ∈ `credits, materials, manpower, influence, exotics`.
`sphere` ∈ `navy, treasury, senate`.
`title`/`claim` title ids ∈ `landless, knight, baron, count, duke, archduke, emperor`.
Note `title`/`claim` test an **exact title id** (held / not-held), not a rank threshold — for "Count or
higher" gate on the `title_rank` counter (see §Progression below).

**Examples**
```jsonc
{ "type": "all", "of": [
  { "type": "turn", "op": "gte", "value": 3 },
  { "type": "trait", "role": "self", "trait": "Ambitious", "kind": "nature", "present": true },
  { "type": "resource", "resource": "credits", "op": "lt", "value": 0 }   // the house is insolvent
]}
```

---

## 4. Effect reference (complete)

Used in a choice's `effects` array. Applied in order when the choice is taken.

| `type` | fields | meaning / clamps |
|---|---|---|
| `setWorldFlag` | `flag`, `value:bool` | set/clear a world flag |
| `setCharFlag` | `role`, `flag`, `value:bool` | set/clear a per-character flag (arm a personal gate) |
| `adjustRelationship` | `from`, `to`, `delta` | nudge directed disposition (clamped −100..100) |
| `addBond` | `from`, `to`, `bond: blood\|sworn\|marriage`, `strength`(default 50) | forge/upgrade a bond (strength 0..100; one bond per directed edge) |
| `adjustSkill` | `role`, `skill`, `delta` | change a skill level |
| `adjustStress` | `role`, `delta` | change stress (clamped 0..100) |
| `adjustCounter` | `key`, `delta` | change a world counter (gate accumulator / clock) |
| `addTrait` | `role`, `trait`, `kind: nature\|aptitude`(default aptitude) | grant a trait |
| `advanceCareer` | `role` | +1 career rank |
| `adjustResource` | `role`(default `self`), `resource`, `delta` | move a house-treasury resource (Credits may go negative; others clamp ≥0) |
| `grantClaim` | `role`(default `self`), `title` | add a claim to a title to the role's **house** (GDD §13) |
| `adjustLegitimacy` | `role`(default `self`), `delta` | change the house's legitimacy/standing (clamped ≥0, GDD §13) |
| `setTitle` | `role`(default `self`), `title` | set the house's title outright — grant / usurp / abdicate (GDD §13) |
| `log` | `text` | write a chronicle line (supports `{role.field}` tokens) |

**The flag/counter levers are how unscripted storylines chain.** A choice in event A arms a flag/raises
a counter; a *different* event B has that flag/counter in its `when`. That's the whole emergent-story
engine (proven cascade: `barracks_confrontation/mock` arms `feud` → `the_duel` becomes eligible).

**Examples**
```jsonc
"effects": [
  { "type": "setCharFlag", "role": "rival", "flag": "feud", "value": true },
  { "type": "adjustRelationship", "from": "rival", "to": "self", "delta": -30 },
  { "type": "adjustResource", "resource": "credits", "delta": 6 },          // taxes came in
  { "type": "adjustCounter", "key": "unrest", "delta": 1 },                 // ...but the people seethe
  { "type": "log", "text": "{self.name} squeezed the harvest tithe. The barony pays, and remembers." }
]
```

### Engine-managed flags (don't set these yourself; you may *read* them)
- `evt:<eventId>:fired` — auto-set when a non-repeatable event fires. Read it via the `eventFired`
  condition (cleaner than the raw flag).

---

## 4b. Progression & Succession (§13) — title, legitimacy, claims, marriage

Title and legitimacy live on the **House** (the dynasty), not the character. The pieces:

- **Title** — a rung id (`landless`→`knight`→`baron`→`count`→`duke`→`archduke`→`emperor`). Read with the
  `title` condition (exact id). Change with `setTitle` (event-driven grant/usurp/abdicate).
- **Legitimacy** — the house's standing (int, ≥0; birth baseline set per scenario). Change with
  `adjustLegitimacy`. Each title has a legitimacy *requirement* (the soft-lock): holding a title above it
  breeds instability until standing catches up.
- **Claim** — a house's pressable right to a title (`grantClaim` forges one; the `claim` condition reads
  it). The key the Intrigue path needs to usurp.
- **Marriage** — a relationship bond (`addBond … bond:"marriage"`), read by the `bond` condition. The
  vehicle for converting a political tie into a claim.

**Engine-written counters you READ (do not write — the progression system overwrites them each turn,
and they reflect the PROTAGONIST's house only):**

| counter | meaning | range |
|---|---|---|
| `title_rank` | protagonist house's title rank | 0 (landless) … 6 (emperor) |
| `house_legitimacy` | protagonist house's legitimacy | 0 … (requirements top out at 95) |
| `title_instability` | how far the title outstrips legitimacy (the soft-lock gap) | 0 … ; the `contested_title` crisis gates at ≥ 20 |

**Counters the path commands raise (shared, you may read OR write):** `seizures` (a military seizure;
also feeds §7 threat), `corruption` (an intrigue scheme's trail). The soft-lock also adds to `unrest`
each turn a title is held above legitimacy.

> Note: `title_*`/`house_legitimacy` are read-only **projections of the player's house**, refreshed every
> turn — gate on them for player-facing scenes, but to mutate state use `setTitle`/`adjustLegitimacy`/
> `grantClaim` (which write the House directly).

**Worked succession patterns** (author these as ordinary events):

```jsonc
// 1) PETITION — a kinsman asks you to recognize his claim to a County.
{ "id": "the_petition", "tier": "situation",
  "roles": [ { "name": "petitioner", "when": [ { "type": "bond", "from": "self", "to": "petitioner", "bond": "blood", "present": true } ] } ],
  "when": [ { "type": "title", "role": "self", "title": "duke" } ],   // only a Duke can grant a County
  "choices": [
    { "id": "recognize", "cost": { "influence": 1 },
      "effects": [ { "type": "grantClaim", "role": "petitioner", "title": "count" },
                   { "type": "adjustRelationship", "from": "petitioner", "to": "self", "delta": 20 } ] },
    { "id": "refuse",
      "effects": [ { "type": "setCharFlag", "role": "petitioner", "flag": "slighted", "value": true } ] }
  ] }

// 2) MARRIAGE-FORGED CLAIM — a union that becomes leverage.
{ "id": "the_advantageous_match", "tier": "situation",
  "roles": [ { "name": "bride", "when": [ { "type": "bond", "from": "self", "to": "bride", "bond": "marriage", "present": false } ] } ],
  "choices": [
    { "id": "wed", "cost": { "influence": 2 },
      "effects": [ { "type": "addBond", "from": "self", "to": "bride", "bond": "marriage", "strength": 60 },
                   { "type": "adjustLegitimacy", "role": "self", "delta": 8 },   // a good match raises standing
                   { "type": "grantClaim", "role": "self", "title": "count" } ] } // ...and a claim by blood
  ] }

// 3) USURPATION — press a claim you hold (the set-piece, with the honorable/ruthless split).
{ "id": "press_the_claim", "tier": "setpiece",
  "when": [ { "type": "claim", "role": "self", "title": "count", "present": true } ],
  "choices": [
    { "id": "claim_lawfully", "requires": [ { "type": "trait", "role": "self", "trait": "Honorable", "kind": "nature", "present": true } ],
      "effects": [ { "type": "setTitle", "role": "self", "title": "count" },
                   { "type": "adjustLegitimacy", "role": "self", "delta": 5 } ] },     // clean: standing rises
    { "id": "take_by_force", "requires": [ { "type": "trait", "role": "self", "trait": "Ruthless", "kind": "nature", "present": true } ],
      "effects": [ { "type": "setTitle", "role": "self", "title": "count" },
                   { "type": "adjustCounter", "key": "seizures", "delta": 1 },         // spikes §7 fear
                   { "type": "adjustCounter", "key": "unrest", "delta": 2 } ] }        // ...and the gap breeds revolt
  ] }
```

(There is **no** `adjustResource` for claims/titles and no "transfer title" verb — model handoffs with
`setTitle` on the gainer + a `log`. A future pass may add an explicit `succeed`/inheritance effect.)

---

## 5. Flags & counters — naming conventions

These aren't pre-declared anywhere; **you coin them as you author** and the engine creates them on
first write. Consistency is everything (a typo just silently never matches). Conventions:

- **World flags** (`setWorldFlag`/`worldFlag`): global one-time gates. `snake_case` describing a world
  fact: `feud_declared`, `oath_sworn`, `barony_lost`, `imperium_proclaimed`.
- **Char flags** (`setCharFlag`/`charFlag`): per-person state. Short adjective/state: `feud`,
  `humiliated`, `wounded`, `indebted`, `marked_for_death`.
- **Counters** (`adjustCounter`/`counter`): accumulating numbers — crisis gates, clocks, tallies:
  `unrest`, `corruption`, `crisis_pressure`, `victories`. Crisis gates (M5) will read these.

Keep a running list in the theme file's top `"//"` comment (as `academy_crucible.json` does) so the
chain is documented.

---

## 6. Holdings economy catalog

### Specializations — `/content/holdings/specializations.json`
```jsonc
{ "specializations": [
  { "id": "forge_world", "name": "Forge-World",
    "yield":  { "materials": 4 },     // gross per-turn production (any of the 5 resources)
    "upkeep": { "credits": 2 },       // per-turn running cost
    "slots": 3,                        // how many buildings can be slotted here
    "popGrowth": 1 }                   // population added per turn; >0 marks a POPULATED world (carries unrest)
]}
```
Currently authored: `agri_world, forge_world, trade_hub, fortress, research_station`.

### Buildings — `/content/holdings/buildings.json`
```jsonc
{ "buildings": [
  { "id": "shipyard", "name": "Shipyard",
    "requires": "forge_world",        // optional: pin to one specialization. omit = fits any holding
    "cost":   { "materials": 10, "credits": 6 },   // one-time, paid from the house treasury
    "yield":  { },                     // ongoing per-turn (minus upkeep)
    "upkeep": { "credits": 2 } }
]}
```
Currently authored: `power_plant, mine, farm_complex, market`.

Resource keys (everywhere): `credits, materials, manpower, influence, exotics`.

**Economy mechanics that make these matter (so you can tune):**
- Each turn, every holding feeds its owner's treasury: `spec.yield + Σ building.yield − all upkeep`.
- Populated worlds (`popGrowth>0`) grow, and carry **unrest**: when the owning house's **Credits < 0**,
  unrest climbs +5/turn and **suppresses Manpower output** (`× (100−unrest)/100`); when solvent it
  decays −3/turn. This is the §17 "the one you run out of" tension.
- **Tuning rule (GDD §17): do NOT balance to uniformity.** Author so each resource is *dominant under
  some path/settings* — a forge-heavy military house and an influence-heavy intrigue house should play
  genuinely different economic games. A resource marginal in *most* runs but decisive in *some* is
  working as intended.

---

## 7. Scenarios — starting situations

`/content/scenarios/<id>.json` — currently only `academy_crucible`.
```jsonc
{
  "id": "academy_crucible",
  "protagonist": "vega_marcus",        // must be one of the characters below
  "era": "fractured_stars",            // "fractured_stars" (pre-Imperium) | "imperium"

  "houses": [
    { "id": "vega", "name": "House Vega", "accent": "#7A1F2B",
      "treasury": { "credits": 10, "materials": 8, "manpower": 5, "influence": 3, "exotics": 0 } }
  ],
  "characters": [
    { "id": "vega_marcus", "name": "Marcus Vega", "house": "vega", "age": 17,
      "careerTrack": "military", "careerRank": 0,
      "nature": ["Proud"], "aptitude": ["Strategist"],
      "skills": { "tactics": 1, "discipline": 1, "charisma": 1 },
      "ambition": "command_a_fleet" }
  ],
  "relationships": [
    { "from": "vega_marcus", "to": "corwin_lucan", "disposition": -5,
      "bond": "none", "strength": 0 }          // bond: none | blood | sworn
  ],
  "holdings": [
    { "id": "vega_barony", "owner": "vega", "name": "Tessaly Reach",
      "specialization": "agri_world", "system": "", "population": 20,
      "unrest": 0, "buildings": [] }
  ]
}
```
**Want:** alternative openings — a fallen house clawing back from one poor holding; a great-house heir
with rivals already circling; a low-born self-made officer (the §13 rise-paths each want a distinct
starting fantasy).

---

## 8. Controlled vocabularies (referenced by string — lock these down)

These are **not** JSON files the engine validates — characters and events just *use the strings*. So the
single most valuable thing you can give me is a **canonical list** for each, used consistently. Below is
a **strawman drawn from the GDD** — edit names, add/remove, define meanings, and this becomes the
project bible.

### 8a. Nature traits (constraining, stress-bearing — GDD §18)
Identity traits that *are* the character. Acting against them should cost stress (events you author
enforce this by pairing the off-trait choice with `adjustStress`). Strawman:
`Honorable, Cruel, Zealous, Craven, Devoted, Proud, Ambitious, Just, Ruthless, Loyal, Vengeful, Pious`.
→ *Give me: final list + one line each ("Honorable: keeps oaths; breaking one spikes stress").*

### 8b. Aptitude traits (gating/flavoring, free — GDD §18)
Skill/disposition traits. Gate or flavor options, no stress penalty. Strawman:
`Strategist, Diplomat, Duelist, Quartermaster, Schemer, Orator, Tactician, Administrator, Greedy, Charismatic, Paranoid, Brilliant`.
→ *Give me: final list + what each gates/flavors.*

### 8c. Skills (numeric, grow over a career — GDD §13/§18)
Currently used: `tactics, discipline, charisma`. Strawman full set, grouped by track:
- Military: `tactics, discipline, leadership, gunnery`
- Stewardship: `administration, economics, logistics, engineering`
- Law/Intrigue: `oratory, intrigue, law, diplomacy`
- Cross-cutting: `charisma`
→ *Give me: final skill list (and which track each belongs to).*

### 8d. Ambitions (drive NPC AI + personal-story events — GDD §18)
A character's `ambition` string. Strawman: `command_a_fleet, outshine_all_rivals, earn_a_command,
restore_house_fortunes, seize_the_throne, win_a_great_love, avenge_the_house, master_the_senate,
amass_a_fortune, become_legend`.
→ *Give me: final list + one line on what each character pursues (so I can write ambition-driven events).*

### 8e. Career tracks + rank ladders (GDD §13)
Currently: `careerTrack` ∈ `military, stewardship, law`; `careerRank` is an integer the `advanceCareer`
effect increments. The GDD wants **named rungs**. Strawman:
- **Military:** 0 Cadet → 1 Ensign → 2 Lieutenant → 3 Commander → 4 Captain → 5 Admiral → 6 Grand Admiral
- **Stewardship:** 0 Clerk → 1 Administrator → 2 Governor → 3 Minister → 4 Imperial Treasurer
- **Law/Intrigue:** 0 Aspirant → 1 Advocate → 2 Magistrate → 3 Praetor → 4 High Justice
→ *Give me: final track list + the rung name for each rank index (so the UI/log can name them).*

---

## 9. The content backlog — what to write, by priority

### Priority A — Academy Crucible depth (proves the engine, ships first content)
The opening act. ~15–30 events across the tiers. Specific asks:
- **Rivalry chains** beyond the one duel: insult → feud → sabotage → reconciliation-or-bloodshed.
- **Sworn-bond forging**: shared watches, covering for a friend, oaths (use `addBond sworn`).
- **Trait-crystallizing trials** (GDD §13/§18): a setpiece where a choice grants a *nature* trait
  (`addTrait kind:nature`) — "the proving ground shapes who they become."
- **Mentor relationships**: an instructor role (bind by `rank gt vsRole self`) who teaches/tests.
- **Ambient academy life**: mess-hall, drills, leave — short `ambient` beats that drift stress/skills.

### Priority B — Steward / economy decisions (NEW: vocabulary just landed)
The §17 fantasy as events. Read `resource`, write `adjustResource`/`adjustCounter`. Examples to author:
- "Credits dry in 3 turns — **raise taxes** (`+credits`, `+unrest counter`) / **sell a holding** /
  **take a rival's loan** (`+credits`, arm an `indebted` char flag)."
- "The forge-world's reactor is failing — **invest materials** now or **risk output**."
- "A bumper harvest" / "a market crash" — ambient resource swings for texture.
- Gate some on `trait` (a Greedy heir gets a tempting corrupt option; an Honorable one pays stress).

### Priority C — Career-track progression events (GDD §13)
Beats that gate on `rank`/`skill`/`trait` and call `advanceCareer`. One small arc per track to start
(military first, since the Academy feeds it): a first command, a contested promotion, a test of loyalty.

### Priority D — Holdings catalog breadth (GDD §17)
More specializations (mining outpost, shipyard-world, agri-collective, fortress-station, research-enclave)
and a fuller building tree per type. Respect the variable-emphasis tuning rule.

### Priority E — Alternative scenarios (GDD §13 rise-paths)
2–3 new openings so the three rise paths each have a distinct starting fantasy (see §7).

---

## 10. NOT YET LOADABLE — please don't author these yet

Honest scope guard. These have GDD slots but **no loader/mechanism** in the engine today, so authored
content would have nowhere to go:

| Area | Status | When |
|---|---|---|
| **Weapons / ships / fleet units** | No military layer at all. No data shape. | needs a future milestone |
| **Megastructures** | GDD slot, no loader. | M4-deferred / later |
| **Spheres (power balance) + threat/coalition** | No loader. | Milestone 5 |
| **Crisis gates / cascades / Bloody Events** | The `counter` plumbing exists; the gate *system* doesn't. | Milestone 5 |
| **Marriage / fostering / succession** | No data shape yet. | M4-deferred / M5 |
| **Exotics *sources* / anomalies / archaeology** | The Exotics resource exists; the anomaly→Crisis-foreshadowing system (its whole point) is M5. | Milestone 5 |
| **Galaxy map / systems / hyperlanes** | `Holding.system` field is reserved but unused. | Milestone 3/4 UI |

You *can* write generic exotic-find or anomaly *flavor* events now as ordinary events (they'd just grant
`adjustResource exotics` and set a flag) — but hold the structured Crisis-foreshadowing framing until M5.

---

## 11. How to submit content

Hand it over in **either** form:
1. **Raw JSON** using the schemas above (fastest for me to drop in), or
2. **Structured prose** — for each event: `id`, tier, "fires when …", each choice (label + what it does
   + any gate/cost), and the body text. I'll encode it into valid JSON.

Then I:
```bash
dotnet run --project src/Tools -- validate     # must be 0 errors
dotnet test                                    # must stay green
dotnet run --project src/Tools -- play --seed 1 --turns 8   # eyeball it in the harness
```
and confirm it loads, validates, and plays.

---

## Quick reference card

```
TIERS      ambient | situation | setpiece
POOLS      influence | treasury | agents              (per-turn budget, choice cost)
RESOURCES  credits | materials | manpower | influence | exotics   (banked house wealth)
SPHERES    navy | treasury | senate                  (§7 estate influence)
TITLES     landless knight baron count duke archduke emperor   (§13 ladder ids)
OPS        eq neq lt lte gt gte   (== != < <= > >=)
BONDS      none | blood | sworn | marriage
TRAIT KIND nature | aptitude | any
TOKENS     {role.name} {role.house} {role.rank}      (role = self or a declared role)
CLAMPS     disposition −100..100 · stress 0..100 · bondStrength 0..100 · legitimacy ≥0 · Credits may be negative
CONDITIONS all any not const worldFlag charFlag relationship bond skill trait rank turn counter resource sphere title claim eventFired
EFFECTS    setWorldFlag setCharFlag adjustRelationship addBond adjustSkill adjustStress adjustCounter addTrait advanceCareer adjustResource grantClaim adjustLegitimacy setTitle log
READ-ONLY  title_rank · house_legitimacy · title_instability   (system-written, protagonist house)
```
