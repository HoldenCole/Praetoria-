# Praetoria — Content Authoring Guide

This is the definitive list of **what content the engine can load today**, the **exact schemas**, the
**complete condition/effect vocabulary**, the **controlled vocabularies** to lock down, and a
**prioritized backlog** of what to write. It also calls out, honestly, what has **no engine slot yet**
so no writing is wasted.

Everything here is grounded in the current Core (`/src/Core`). When the engine grows (military layer,
galaxy map), this guide gets new sections — it won't silently drift. Last reconciled with the code:
v0.5.0, 2026-11-07 (see `docs/GAP_ANALYSIS.md` and `docs/RELEASE_0.5.md`).

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
  /crises      *.json   — gated crises: gates / onTrigger / onResolve / dampers   (§4d)
  /spheres     *.json   — the power-balance estates and the career track feeding each (§7)
  /titles      *.json   — the title ladder + legitimacy requirements (§13)
  /careers     *.json   — career tracks + named rank ladders; the top rung caps advanceCareer (§4e)
  /galaxy      *.json   — star maps: systems + lanes; a scenario names one and places holdings in it (§4g)
  /traits      *.json   — the §8 vocabularies as data (nature, aptitude, ambitions) for character creation (§4h)
  /backgrounds *.json   — character origins applied at creation (§4h)
  /fleets      *.json   — fleet rules (cost, upkeep, readiness, intel, combat, sieges) and ship classes (§4i)
  /honors      *.json   — Honorifics & Orders: condition lists over self + effects, invested once (§4j)
  /names       *.json   — given names by culture for children born in play (§4c)
  /holdings/exchange.json — exchange rates, tax policies, the contiguity bonus (§6)
  /great_crisis *.json  — the Great-Crisis archetype: clock, intensity, stages, behaviours (§4k)
```
All loaders accept JSONC (`//` and `/* */` comments, trailing commas).
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
- **Always leave one choice ungated.** A briefing item the player cannot act on is re-offered every
  turn (it is never marked fired), so an event whose every choice has a `requires` is a trap for any
  character meeting none of them. Both validators reject it.

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
| `{role.rankName}` | the rung's name from `/content/careers` ("Lieutenant"); falls back to the number |

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
| `bond` | `from`, `to`, `bond: blood\|sworn\|marriage`, `present:bool` | is there a formal bond of that type? (marriage is written on both edges) |
| `skill` | `role`, `skill`, `op`, `value` | skill-level compare |
| `trait` | `role`, `trait`, `kind: nature\|aptitude\|any`, `present:bool` | does the character have the trait? |
| `rank` | `role`, `op`, `value` **or** `vsRole` | career-rank compare (literal, or against another role) |
| `track` | `role`(default `self`), `track`, `present:bool` | is the character on this career track? (`military\|stewardship\|law`) — gate each track's arc on it |
| `era` | `era: fractured_stars\|imperium`, `present:bool` | which start the world is in (GDD §10) — gate court-of-the-emperor content on `imperium` |
| `fleetStrength` | `role`(default `self`), `op`, `value` | total hulls the role's house commands (GDD §6/§8) |
| `atWar` | `role`, `present:bool` | is the role's house at war with self's house? |
| `sex` | `role`, `value: male\|female` **or** `op: eq\|neq` + `vsRole` | a character's sex, or compared to another role's |
| `bloc` | `role`(default `self`), `bloc: hegemon\|alliance`, `present:bool` | which Great-Crisis bloc the role's house is in |
| `turn` | `op`, `value` | current turn number compare |
| `age` | `role`(default `self`), `op`, `value` | character age compare (GDD §14) |
| `counter` | `key`, `op`, `value` | world-counter compare (gate accumulators, clocks) |
| `resource` | `role`(default `self`), `resource`, `op`, `value` | house-treasury compare (GDD §17) |
| `sphere` | `role`(default `self`), `sphere`, `op`, `value` | house sphere-influence compare (GDD §7) |
| `title` | `role`(default `self`), `title`, `present:bool` | is the house's title **exactly** this rung? (GDD §13) |
| `claim` | `role`(default `self`), `title`, `present:bool` | does the house hold a claim to this title? (GDD §13) |
| `eventFired` | `event`, `value:bool` | has an event already fired this run? |

`resource` ∈ `credits, materials, manpower, influence, exotics`.
`sphere` ∈ `navy, treasury, senate`.
`title`/`claim` title ids ∈ `landless, knight, baron, count, duke, archduke, emperor`. Resource ids ∈ `credits, materials,
manpower, influence, exotics, helium3, rare_earths, isotopes` (the last three come only from deposits; see ECONOMY.md).
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
| `advanceCareer` | `role` | +1 career rank, capped at the top rung of the track's ladder in `/content/careers` |
| `adjustResource` | `role`(default `self`), `resource`, `delta` | move a house-treasury resource (Credits may go negative; others clamp ≥0) |
| `grantClaim` | `role`(default `self`), `title` | add a claim to a title to the role's **house** (GDD §13) |
| `revokeClaim` | `role`(default `self`), `title` | remove a claim — a pressed or sold claim is spent (pair with `setTitle`) |
| `succeed` | `role` | hand the seat to a living character: they become the protagonist, the House keeps its title (abdication) |
| `declareWar` | `role` | self's house declares war on the role's house (GDD §6) |
| `makePeace` | `role` | self's house and the role's house make peace |
| `joinBloc` | `bloc: hegemon\|alliance` | move self's house into a Great-Crisis bloc |
| `adjustLegitimacy` | `role`(default `self`), `delta` | change the house's legitimacy/standing (clamped ≥0, GDD §13) |
| `setTitle` | `role`(default `self`), `title` | set the house's title outright — grant / usurp / abdicate (GDD §13) |
| `kill` | `role` | kill a character — assassination / duel / Bloody Event; if it's the protagonist, succession fires (GDD §14) |
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

**Two rules the title model imposes (titles live on the House, not the character):**
- A pressed claim must be **spent** — `revokeClaim` alongside the `setTitle` — or a repeatable
  claim event keeps re-firing (and could even *demote* a house that has since climbed higher). Gate
  "press a claim to a County" on `title_rank lt 3` as well.
- A handover **inside** the house is not two `setTitle`s (that demotes your own house). Use
  `succeed` on the heir: they become the protagonist and the House keeps its rung.

---

## 4c. The Dynasty Lifecycle (§14) — aging, death, birth, succession

This runs **automatically every turn** — you don't author it, but you can read and nudge it:

- **Aging**: every living character ages +1 each turn.
- **Death**: rolled only at/after age 50 (rising to certain by ~95). On the protagonist's death the seat
  passes to an **heir** (a living house member — blood children first, eldest first), or the dynasty ends.
- **Birth**: a married couple (`addBond … bond:"marriage"`) with a fertile mother (16–45) may bear a
  child each turn — a new character, age 0, joining the **father's** house with blood ties to both parents.
- **Stress** (§18): every living character sheds 1 stress a year; at 100 they **break** — the engine sets
  the char flag `broken`, resets stress to 50 and bumps the `breakdowns` counter. `the_breaking_point`
  (echoes) is the authored consequence; write others against `broken`. Nature traits don't charge stress
  automatically — pair the off-trait choice with `adjustStress` yourself.
- **Regency**: while the protagonist is under 16 the world flag `regency` is up (no regent character yet).
- **Game mode** (§14): `World.Mode` is `ironman` (dynasty death ends the run) or `continue` (the strongest
  sworn ally of the dead head takes up the banner; the `dynasty_adopted` flag and `adoptions` counter record
  it). The harness sets it with `--continue`.

**Character scenario fields** the lifecycle uses: `sex` (`"male"`/`"female"` — required for births),
`mother`/`father` (ids — succession rights), and `flags` (starting char flags, e.g. `["wed"]` for a cast
that begins married). Set these in a scenario's `characters`.

**Engine-written counters/flags you may READ:** `generation` (succession count), `dynasty_births`
(total born), `breakdowns`, `adoptions`; the char flag `broken`; the world flags `dynasty_dead` (no heir
remained — the run is over), `dynasty_adopted`, `regency`.

**Verbs that touch it:** `age` (condition — "when you grow old…") and `kill` (effect — assassination,
duel, Bloody Event; auto-fires succession if the target is the protagonist). Example Bloody-Event beat:
```jsonc
{ "id": "the_gala_massacre", "tier": "setpiece",
  "roles": [ { "name": "rival", "when": [ { "type": "charFlag", "role": "rival", "flag": "feud", "present": true } ] } ],
  "choices": [
    { "id": "spring_the_trap", "cost": { "agents": 2 },
      "requires": [ { "type": "trait", "role": "self", "trait": "Ruthless", "kind": "nature", "present": true } ],
      "effects": [ { "type": "kill", "role": "rival" },
                   { "type": "adjustCounter", "key": "seizures", "delta": 1 },   // the galaxy recoils (§7 fear)
                   { "type": "setWorldFlag", "flag": "realm_unstable", "value": true } ] }  // arms the §16 web
  ] }
```

> Every shipped scenario cast now has `sex`, and the match event (`the_advantageous_match`) can create a
> marriage mid-run, so births happen in play. Births need a mother **and** a father (a same-sex marriage is
> an alliance, not a bloodline). A marriage written from either spouse counts — the bond is mutual.

---

## 4d. Crises — `/content/crises/*.json` (§16)

A crisis is a *gated, buildable state*, not an event. Gates reuse the condition vocabulary; trigger,
resolve and damper writes reuse the effect vocabulary.
```jsonc
{ "crises": [
  { "id": "local_revolt", "name": "Local Revolt", "tier": "regional",   // personal|house|regional|imperial|existential
    "weight": 3,                 // organic-onset roll weight (an abstain weight competes, so ripe ≠ instant)
    "severity": 2,               // onsets at this severity; a re-trigger adds it again
    "repeatable": false,         // may it re-onset WHILE active? (usually no)
    "once": false,               // true = at most once per run; after it resolves it is marked spent
    "gates":     [ { "type": "counter", "key": "unrest", "op": "gte", "value": 5 } ],   // ALL must hold
    "onTrigger": [ { "type": "setWorldFlag", "flag": "realm_unstable", "value": true } ],// writes that cascade
    "onResolve": [ /* effects applied when it subsides — un-arm what onTrigger armed */ ],
    "dampers": [
      { "id": "concessions", "name": "Grant Concessions", "relief": 2,      // severity removed
        "available": [ { "type": "counter", "key": "goodwill", "op": "gte", "value": 2 } ],  // earned by prior play
        "cost": { "influence": 1 },                                          // optional pool cost
        "effects": [ { "type": "adjustCounter", "key": "unrest", "delta": -3 } ] }
    ] }
]}
```
An active crisis whose gates **no longer hold** decays one severity per turn and resolves (natural
de-escalation). The engine raises `crisis:<id>:active` while active and `crisis:<id>:spent` after a
`once` crisis resolves — read them with `worldFlag`, never set them.

## 4e. Careers — `/content/careers/careers.json` (§13)

```jsonc
{ "tracks": [
  { "id": "military", "name": "the Navy",
    "ranks": ["Cadet", "Ensign", "Lieutenant", "Commander", "Captain", "Admiral", "Grand Admiral"] }
]}
```
Rank *i* is named `ranks[i]`; the last rung caps `advanceCareer`. Gate a track's events with the
`track` condition and name rungs in prose with `{role.rankName}`. A new track is a new entry — no C#.

## 4g. The galaxy — `/content/galaxy/<id>.json` (§19)

```jsonc
{ "id": "the_reach", "name": "The Reach",
  "systems": [ { "id": "tessaly", "name": "Tessaly", "x": 120, "y": 160 } ],   // map units; the UI scales
  "lanes":   [ ["tessaly", "anvil"] ] }                                        // undirected hyperlanes
```
A scenario says `"galaxy": "the_reach"` and gives each holding a `"system"`. Territory is **derived**: a
system belongs to whoever owns the holdings in it (two owners ⇒ *contested*, majority rules), shaded by the
owning house's accent. Both validators check that every lane and every holding points at a real system.
`praetoria map --scenario id` prints it. Fleets and movement are the next layer; keep lanes meaningful.

## 4h. Character creation — `/content/traits` and `/content/backgrounds` (§13, §18)

`traits.json` holds the locked vocabularies **as data** — `nature`, `aptitude`, `ambitions`, each
`{ id, name, description }`; the pre-flight asserts it equals `PRAETORIA_VOCAB.md`. The creation screen and
`praetoria create` read it.

A background is a one-time **delta** on the scenario's protagonist and house:
```jsonc
{ "backgrounds": [
  { "id": "upjumped", "name": "The Upjumped", "description": "…",
    "legitimacy": -10,                       // added to the house's legitimacy (clamped ≥ 0)
    "treasury": { "credits": -2 },           // added to the house treasury
    "nature": [], "aptitude": ["Brilliant"], // ADDED to the character's traits (picks replace, backgrounds add)
    "skills": { "tactics": 1 }, "flags": [], "stress": 5,
    "trackHint": "military", "ambitionHint": "earn_a_command" }   // pre-selected, never forced
]}
```
The engine writes `background:<id>` as a char flag, so events can read where the protagonist came from
(`charFlag self background:upjumped`). Picks are capped at 2 nature + 2 aptitude; a background may add one
more of each (hard cap 3). Everything is validated against the catalogs — an unknown trait throws.

## 4i. Fleets — `/content/fleets/rules.json` and scenario `fleets` (§6, §8, §19)

Strength is an abstract hull count; one jump per turn along lanes. A scenario seeds fleets:
```jsonc
"fleets": [
  { "id": "fleet_imperial_home", "name": "The Imperial Home Fleet", "owner": "aurelian",
    "system": "aurelia_prime", "strength": 6, "readiness": 100,
    "office": true }      // GDD §8: commanded by the post, not owned by the blood; false = a house fleet
]
```
`rules.json` tunes `shipCost` (paid from the house treasury by the *Lay down a hull* command), `upkeepPerStrength`
(per turn), readiness recovery/decay, and intel: a rival fleet in your territory is seen at full confidence,
one lane out at `confidenceAdjacent`; out of sight the report loses `intelDecay`/turn while the last-known
position stands. **Engine-written, read-only:** `incursions` counter and `incursion:<houseId>` flags (a fleet
arrived in another house's space), `ships_raised`, `fleets_raised`. Rival houses raise hulls when flush and a
hostile head sends a rested fleet at your seat. *Unfamiliar Sails* and *The Review* are the first fleet events.
**War and combat.** Houses go to war by the *Declare war* command (Influence), by an NPC head that hates and
outguns you, or by the `declareWar` effect. Fleets of warring houses that share a system **fight** at the start
of the turn after moving: power = hulls × (0.5 + readiness/200), the house holding the system defends at
×(1 + `defenderBonus` + Σ its holdings' and buildings' `defense` — fortress worlds 0.5, fortress-stations 0.6,
bastion batteries 0.4); a seeded roll on power share picks the winner; the loser sheds `loserLossRatio` of its
hulls and falls back to its nearest port (or scatters), the winner sheds `winnerLossRatio` scaled by how close it
was. An **unopposed fleet in enemy space takes its holdings** (conquest: +`conquestUnrest`, and `seizures`+1 when
the player does it — the §7 fear machine). Peace: the player sues by command (an NPC refuses while its hulls exceed
yours by `peaceRefusalRatio`); an NPC suing the player flags its head `suing_for_peace` and *The Envoy* asks you.
**Engine-written, read-only:** `battles`, `battles_won`, `battles_lost`, `conquests`, `wars_declared` (every war in the Reach), `wars_begun` (the player's own house's) counters;
`battle_fought`, `conquest_made`, `holding_lost` flags (content clears them — see *The Butcher's Bill*, *The
Conquered World*, *The Lost World*); `at_war:<house>` flags; the `suing_for_peace` char flag.

## 4j. Honors — `/content/honors/honors.json` (§13)

```jsonc
{ "honors": [ { "id": "burning_lance", "name": "Knight of the Burning Lance", "description": "…",
  "when": [ { "type": "counter", "key": "battles_won", "op": "gte", "value": 3 } ],     // any conditions over self
  "effects": [ { "type": "adjustLegitimacy", "role": "self", "delta": 10 } ] } ] }     // applied once
```
Invested the turn the conditions first hold; the char flag `honor:<id>` is written for content. Honors are the
Merit payoff that is birth-independent: a risen admiral may never be granted a Dukedom, but no one questions a Knight
of the Burning Lance's right to command.

### Nature-trait friction — `/content/traits/friction.json` (§18)
`{ "friction": { "declare_war": { "Honorable": 8, "Just": 6, "Cruel": -2 }, … } }` — stress charged (or relieved) per
nature trait when the protagonist's house does an act the engine recognises: `declare_war`, `battle_won`,
`battle_lost`, `conquest`, `holding_lost`, `sue_for_peace`, `refuse_peace`, `plunder`, `assassination`. This is how
"acting against your nature costs stress" happens without every event spelling it out.

### Engine-written flags and counters from the 0.7 systems (read-only)
| what | names |
|---|---|
| Dynasty | char flags `hidden` (fostered away — unbindable), `regent`, `parvenu`, `came_of_age`, `admiral`, `deposed`, `suing_for_peace`, `wed`; counters `marriages`, `assassinations_attempted` |
| Intrigue | world flags `plot_exposed` (your plot traced), `knife_turned` (an attempt on your blood traced), `assassin:<house>` |
| War | `battles`, `battles_won`, `battles_lost`, `conquests`, `wars_declared`; `battle_fought`, `conquest_made`, `holding_lost`, `under_siege`, `at_war:<house>` |
| Throne | `imperium_proclaimed`, `throne_seized`, `thrones_taken`, `peak_title_rank` |
| Domain | `megastructures`, `megastructure_raised`; `honors` |
| Director | `lull` (turns since anything above ambient fired) |

Events may carry `"ambitions": ["earn_a_command"]` — the Director weights them ×1.6 for a protagonist who holds one
of them and ×0.8 otherwise. Ship classes: `/content/fleets/ship_classes.json` (`attack`, `defense`, `cost`, `upkeep`,
`minTitleRank`, `requires`). Holdings' `defense` and buildings' `defense`, `minTitleRank`, `megastructure`.

## 4k. The Great Crisis — `/content/great_crisis/*.json` (§11, §16)

An archetype is a clock and a list of stages. The clock starts at `startTurn` and advances each turn by
`baseRate × intensity` (the `off|low|default|dominant` multipliers; `World.CrisisIntensity`) plus turmoil (3 per
standing war, coalition pressure, fear). Each stage has `at` (clock tenths), `effects`, a `log`, and optionally a
`behavior` the engine implements: `blocs` (sort houses by disposition toward the hegemon — the strongest house),
`bloc_war` (every hegemon house vs every alliance house), `climax`, `resolution` (wars end; the bloc with more hulls
wins; in the fractured stars its strongest house takes the throne). The engine raises `great_crisis:<stage>`,
`great_crisis:resolved`, `great_crisis:player_won` / `player_lost`; the `bloc` condition reads a house's side and the
`joinBloc` effect changes the player's. A cosmic archetype from a later chapter is a new file with new behaviours (a "front" that
advances from an edge system is the obvious next one).

### Knobs the engine exposes (GDD §20)
`World.Difficulty` (rivals' appetite), `World.CrisisIntensity` (the great clock), `World.FamilyAutonomy` (how willful
your kin are; 0 = deferential). Harness: `--difficulty`, `--intensity`, `--autonomy`; the creation screen offers all three.

### Family autonomy flags (read-only)
`defiant` char flag (answer it with *A Will of Their Own*), `defected`, world flag `kin_defected`, counter `defections`;
`suitor` char flag (someone asks for the protagonist's hand — *The Suit*).

### Advisor reports
`AdvisorReports.Build` gives three lines a turn (Steward, Admiral, Chancellor) from house members on the matching
tracks. The Admiral's line is built from the house's intel picture, so it is confident, hedged or wrong exactly as the
GDD's "filtered information" wants. Not content, but the model the briefing header shows.

## 4f. Save files

A save is the serialised `World` (RNG position included) in a versioned envelope — nothing else.
Content is *not* in the save; it reloads from `/content`, so content patches apply to saved games.
```bash
dotnet run --project src/Tools -- play --auto --turns 10 --save my.json
dotnet run --project src/Tools -- play --load my.json --turns 10        # resumes the same deterministic stream
```

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
    "popGrowth": 1,                    // population added per turn; >0 marks a POPULATED world (carries unrest)
    "maxPopulation": 40,               // population ceiling (0/omitted = uncapped)
    "popManpower": 20 }                // +1 Manpower per this many population ("people are soldiers"; 0 = decorative)
]}
```
Currently authored (`specializations.json` + `holdings_ext.json`): `agri_world, forge_world, trade_hub,
fortress, research_station, mining_outpost, shipyard_world, agri_collective, fortress_station,
research_enclave, trade_consortium, senatorial_seat`.

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
Currently authored (`buildings.json` + `holdings_ext.json`): `power_plant, mine, farm_complex, market,
deep_drill, orbital_foundry, conscription_hall, bastion_guns, exotics_lab, free_port, patronage_office,
granary_relief`.

Resource keys (everywhere): `credits, materials, manpower, influence, exotics`.

**Economy mechanics that make these matter (so you can tune):**
- Each turn, every holding feeds its owner's treasury: `spec.yield + Σ building.yield − all upkeep`.
- Populated worlds (`popGrowth>0`) grow to `maxPopulation`, add `population / popManpower` Manpower a
  turn, and carry **unrest**: when the owning house's **Credits < 0**, unrest climbs +5/turn and
  **suppresses Manpower output** (`× (100−unrest)/100`); when solvent it decays −3/turn. This is the §17
  "the one you run out of" tension. NPC houses build on their own holdings when they can afford to.
- **Tuning rule (GDD §17): do NOT balance to uniformity.** Author so each resource is *dominant under
  some path/settings* — a forge-heavy military house and an influence-heavy intrigue house should play
  genuinely different economic games. A resource marginal in *most* runs but decisive in *some* is
  working as intended.

---

## 7. Scenarios — starting situations

`/content/scenarios/<id>.json` — five ship: `academy_crucible` (the fixture), the three rise-path openings
`the_fallen_house` (starts married) / `the_circling_heir` / `the_risen_officer`, and the `imperium`-era
`the_imperial_court` (starts married, under a living Emperor; the sovereign binds by *title*). Every
character carries `sex`; every house a `title` + `legitimacy` (the fallen house holds a Baron title above
its standing on purpose).
```jsonc
{
  "id": "academy_crucible",
  "protagonist": "vega_marcus",        // must be one of the characters below
  "era": "fractured_stars",            // "fractured_stars" (pre-Imperium) | "imperium"

  "houses": [
    { "id": "vega", "name": "House Vega", "accent": "#7A1F2B", "title": "baron", "legitimacy": 28,
      "treasury": { "credits": 10, "materials": 8, "manpower": 5, "influence": 3, "exotics": 0 } }
  ],
  "characters": [
    { "id": "vega_marcus", "name": "Marcus Vega", "house": "vega", "age": 17, "sex": "male",
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
**Want:** a scenario spanning generations (an aged head with a grown heir), and a cast with a child heir
so the `regency` flag gets content.

---

## 8. Controlled vocabularies — LOCKED

**The canonical lists live in [`PRAETORIA_VOCAB.md`](PRAETORIA_VOCAB.md) (v1.1)** and
`validate_praetoria.py` enforces them as a hard gate. Traits/skills/ambitions are still bare strings to
the engine (no loader); tracks and rank ladders are now data in `/content/careers` (§4e). The sections
below are kept as the original strawman for history — when they disagree with the bible, the bible wins.

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
Now **data**: `/content/careers/careers.json` (§4e) names each rung and caps `advanceCareer` at the top.
`careerTrack` ∈ `military, stewardship, law`; the locked ladders:
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
| **Weapons / ships / fleet units** | No military layer at all. No data shape. | Galaxy-mode milestone |
| **Megastructures** | GDD slot, no loader. | later |
| **Fostering / hidden kin ("scatter the seed")** | No data shape. Marriage, claims, succession and `kill` *are* loadable. | later |
| **Honorifics & Orders** | GDD §13 slot, no loader. | later |
| **Exotics *sources* / anomalies / archaeology** | The Exotics resource exists; the anomaly→Crisis-foreshadowing system belongs to a later chapter (*The Frontier*). | later |
| **Galaxy map / systems / hyperlanes** | `Holding.system` field is reserved but unused. | Milestone 3/4 UI |
| **The Great Crisis / era flip / epilogue** | `World.Era` is readable (`era` condition) and the Imperium *start* ships; nothing *flips* the era mid-run yet, and there is no epilogue generator. | Milestone 6 |

Spheres, crises (gates / cascades / dampers / `once` / `onResolve`), titles, claims, marriage, careers,
`kill` and `succeed` **are** all loadable now — see §4b–§4e. Bloody Events are ordinary set-pieces that
use `kill` (the academy's `the_marked_man/finish_it` is the first).

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
TOKENS     {role.name} {role.house} {role.rank} {role.rankName}   (role = self or a declared role)
TRACKS     military | stewardship | law                 (ladders + rung names in /content/careers)
CLAMPS     disposition −100..100 · stress 0..100 · bondStrength 0..100 · legitimacy ≥0 · Credits may be negative
FLAGS      background:<id> char flag (set at creation)       regency · dynasty_adopted world flags
CONDITIONS all any not const worldFlag charFlag relationship bond skill trait rank track era turn age sex counter resource sphere title claim fleetStrength atWar bloc eventFired
EVENT KEYS id tier weight repeatable roles when choices ambitions
FLEETS     incursions · ships_raised · fleets_raised counters · incursion:<house> flags   (system-written, §19)
WAR        battles · battles_won · battles_lost · conquests · wars_declared counters · battle_fought · conquest_made · holding_lost · at_war:<house> flags · suing_for_peace char flag
EFFECTS    setWorldFlag setCharFlag adjustRelationship addBond adjustSkill adjustStress adjustCounter addTrait advanceCareer adjustResource grantClaim revokeClaim adjustLegitimacy setTitle succeed kill declareWar makePeace joinBloc log
READ-ONLY  title_rank · house_legitimacy · title_instability   (system-written, protagonist house)
LIFECYCLE  generation · dynasty_births · breakdowns · adoptions counters · dynasty_dead · dynasty_adopted · regency flags · broken char flag   (system-written, §14/§18)
CRISES     crisis:<id>:active · crisis:<id>:spent flags            (system-written, §16)
RULE       every event keeps at least one choice with no `requires`
```
