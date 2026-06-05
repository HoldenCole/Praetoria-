# Milestone 5 — Crisis System + Power-Balance Spheres + Progression

**Goal (BuildSpec §M5, GDD §7 + §13 + §16):** the power-balance + threat/coalition system, two-ladder
progression + legitimacy soft-lock + three paths, and the full crisis gate/cascade/damper/Bloody-Event
system; NPC houses authoring crises.

**Scope note.** M5 is three large systems (spheres §7, progression §13, crises §16) — **all three are
now built**, end-to-end and headless, and wired into one another: a career feeds sphere dominance (§7)
→ coalition pressure → a coalition crisis (§16); seizing a title (§13) ignores legitimacy → soft-lock
instability → vassals grumble → the same crisis web. **The Bloody-Event set-piece class is the
remaining M5 work** (see *Deferred*).

**Acceptance (this slice, BuildSpec §M5):** *a crisis can be engineered or defused via gates; a cascade
escalates and a damper (earned by prior play) arrests it; an NPC house triggers a crisis.* ✅ Proven by
`CrisisTests` + `CrisisDeterminismTests` and the `crisis` console demo.

**Status:** ✅ complete. 91 tests pass (64 prior + 10 crisis + 7 sphere + 10 progression); engine
`validate` clean (55/55); both content validators clean; the `crisis`, `spheres` and `progression`
demos run deterministically.

## What was built — progression & the legitimacy soft-lock (GDD §13)

| Area | Where | Notes |
|---|---|---|
| Title ladder | `src/Core/Progression/TitleCatalog.cs`; `src/Core/Data/TitleLoader.cs` | `Title` keys + `TitleDef` (rank + legitimacy requirement), loaded from `/content/titles`. The dynasty ladder — persists across deaths. |
| House state | `House.Title` / `House.Legitimacy` / `House.Claims` | Title rung, dynastic standing (birth baseline, set per scenario), and intrigue claims. |
| Progression system | `src/Core/Systems/ProgressionSystem.cs` | The three rise paths and the per-turn **soft-lock**: holding a title above its legitimacy requirement breeds `title_instability` → vassals grumble (`unrest`) → feeds §16, while the holder slowly legitimises. Surfaces `title_rank`/`house_legitimacy`/`title_instability` counters. RNG-free. |
| Three paths | `src/Core/Commands/ProgressionCommands.cs` | **Seize** (military — needs martial rank, ignores legitimacy, spikes `seizures` → §7 threat), **Petition** (merit — capped by legitimacy, arrives clean), **Claim** (intrigue — needs a claim, launders legitimacy, leaves `corruption`). Through the shared command bus. |
| Vocabulary | `title` condition, `grantClaim` effect (+ parsers) | Events can gate on a house's title and forge claims (marriage/inheritance). |
| Turn integration | `TurnController.BeginTurn` | Soft-lock applied after spheres, before the turn's crisis gates. |
| Content | `/content/titles/titles.json`; `contested_title` crisis; academy house titles | The 7-rung ladder (strawman); a crisis gating on `title_instability ≥ 20` with marry-up / buy-silence dampers; the academy houses given legitimate titles. |
| Harness | `src/Tools` — `progression` (new) | The meritocrat (granted, secure) vs the conqueror (seized, instability → contested-title crisis). |

**The soft-lock, proven** (`SoftLock_FeedsTheContestedTitleCrisis`, visible in `progression`): a house
that **seizes** a Duke title on legitimacy 30 (requirement 60) runs `title_instability` 32, its vassals
grumble (`unrest`), and the **contested_title** crisis becomes causable within a turn — while a house
**granted** a County it was legitimate for rules with zero instability. *Birth is a soft-lock, never a
wall: a low-born can take the throne, but rules a powder keg until standing catches up* (§13).

```bash
dotnet run --project src/Tools -- progression --seed 1
```

## What was built — power-balance spheres (GDD §7)

## What was built — power-balance spheres (GDD §7)

| Area | Where | Notes |
|---|---|---|
| Sphere model | `src/Core/Spheres/SphereCatalog.cs` | `Sphere` keys (navy/treasury/senate) + `SphereDef` (name + feeding career track). Data-driven catalog. |
| Loader | `src/Core/Data/SphereLoader.cs` → `ContentDatabase.Spheres` | Loads `/content/spheres/*.json`. |
| Sphere system | `src/Core/Systems/SphereSystem.cs` | Each turn **derives** a house's sphere influence from its members' career ranks on the feeding track (climb §13 = power-balance §7, one system), computes the **threat score** (sphere-dominance above an even split + holdings + seizures), and accumulates `coalition_pressure` / a `coalition_forming` flag when the protagonist's house grows over-mighty. RNG-free. |
| `sphere` condition | `Events/Conditions.cs` (+ parser) | Events/crisis-gates can read a house's sphere influence. |
| Turn integration | `TurnController.BeginTurn` | Recomputes spheres after economy accrual, before the turn's gates are read. |
| Content | `/content/spheres/spheres.json`; `coalition_war` crisis | The three estates (strawman — confirm); a coalition crisis gating on `coalition_pressure ≥ 3` whose writes cascade into the existing civil war. |
| Coalition events | `content/events/coalition_crises.json` (+ text) | 8 authored events — the *player-facing* §7 layer: cultivate an estate → rivals fear you → the coalition moves → the crown notices → humbled-house aftermath. Gate on `navy_influence`/`treasury_influence`/`senate_influence`. |
| Career↔event bridge | `SphereSystem.BridgeToPlayerCounters` | Mirrors the protagonist's career-derived sphere influence into the `{sphere}_influence` counters those events read — **delta-only**, so the events' own cultivation increments are never clobbered. So `navy_influence` = structural (careers) + cultivated (choices); a climbing career now feeds **both** the systemic threat/coalition crisis **and** the narrative coalition events. |
| Harness | `src/Tools` — `spheres` (new) | Shows an heir climbing the Navy → share/threat/`navy_influence` climb → coalition becomes causable. |

**The §7 loop, proven** (`CoalitionPressure_Accumulates_AndArmsTheCoalitionCrisis`, visible in `spheres`):
as House Vega's heir climbs the Navy ladder, its Navy share rises 50 → 67 → 75%, its threat climbs past
the coalition threshold, `coalition_pressure` accrues, and the authored **coalition_war** crisis becomes
causable — *career feeds spheres feeds coalitions feeds crisis*. Let the admiral die and the dominance
(and the pressure) collapse. Spheres and the §16 crisis system are one connected mid-game.

```bash
dotnet run --project src/Tools -- spheres --seed 1
```

## What was built — crisis system (GDD §16)

| Area | Where | Notes |
|---|---|---|
| Crisis state | `src/Core/State/ActiveCrisis.cs`; `World.Crises` | A crisis persists and escalates (severity), unlike a momentary event. A `crisis:<id>:active` world flag mirrors it so ordinary flag conditions can read it. |
| Crisis model | `src/Core/Crises/CrisisDef.cs` (`CrisisDef`, `DamperDef`) | Data: tier, weight, **gates** (conditions), **onTrigger** (effects), **dampers** (each with availability conditions + relief + cost). Reuses the event-engine vocabulary — no new mini-language. |
| Loader | `src/Core/Data/CrisisLoader.cs` → `ContentDatabase.Crises` | Loads `/content/crises/*.json` via the existing `ConditionParser`/`EffectParser`. Bad vocabulary throws at load (caught by `validate`). |
| Crisis engine | `src/Core/Systems/CrisisEngine.cs` | `IsCausable`/`Causable` (gates), `Trigger` (authored or organic; applies onTrigger → cascade), `AvailableDampers`/`ApplyDamper` (arrest + resolve at severity ≤ 0), `RollOrganic` (seeded weighted roll with an abstain weight). RNG-deterministic. |
| Commands | `src/Core/Commands/CrisisCommands.cs` | `TriggerCrisisCommand` (authored onset) and `ApplyDamperCommand` — through the same command bus the player and NPCs already share. |
| NPC authoring | `NpcAi.Act(..., crises)` | An ambitious NPC lights the fuse on a causable crisis (one per turn, deterministic) — the "crises have authors" rule (GDD §16). |
| Turn integration | `TurnController.EndTurn` | NPCs may author a crisis; then the organic roll runs over the ripe pool. Backward-compatible: no crises ⇒ no-op. |
| Content | `/content/crises/human_crises.json` | `local_revolt` → `civil_war` cascade + `court_scandal`, with prior-play-gated dampers. Reads counters the steward/academy pools already feed (`unrest`, `legitimacy`, `goodwill`, `corruption`, `standing`). |
| Harness | `src/Tools` — `crisis` (new) | Narrates gate → eruption → cascade → damper-gated-on-goodwill, deterministically. |

## The acceptance, concretely

The authored cascade (proven by `Cascade_BrutalSuppressionOfARevolt_ClearsTheCivilWarGate` and visible
in `crisis`): unrest hits 5 → **Local Revolt** is causable → it erupts (unrest→7, legitimacy→−2,
`realm_unstable` set) → the ruler **crushes** it (unrest→8, legitimacy→−3) → those very writes clear
**Civil War**'s gates (`realm_unstable` + legitimacy ≤ −3 + unrest ≥ 6). *Suppressing the small crisis
armed the large one — unscripted.* Then the damper that would arrest the civil war (**Rally the
Loyalists**) is **unavailable at goodwill 0** and **available at goodwill 4** — "the escalation you face
is partly the bill for how you played" (GDD §16), made mechanical.

```bash
dotnet run --project src/Tools -- crisis --seed 1
```

## Key decisions

- **Crises reuse the event vocabulary.** Gates are `ICondition`s, triggers/dampers are `IEffect`s. A
  crisis invents no new verbs — it's a second, *persistent/stateful* selection layer over the same
  data the event engine speaks (GDD §16 "crises are largely an application of the event engine").
- **Cascade is emergent, not scripted.** No escalation clock — a crisis's effects write counters/flags
  that happen to clear other crises' gates. Re-evaluated each turn.
- **Determinism preserved.** Organic onset is a seeded weighted roll with an abstain weight; gate
  evaluation draws no RNG, so adding the system didn't perturb existing seed-driven tests.
- **Mixed origin.** Authored (player/NPC via command) and organic (the roll) both onset crises; an
  NPC-authored crisis this turn is excluded from the same turn's organic pool.
- **Counter namespacing note.** Crisis gates read the *world counter* `unrest` (fed by events), which
  is distinct from per-holding `Holding.Unrest` in the §17 economy. They model different things; a
  future pass may bridge them (insolvency unrest → world unrest).

## Deferred (remaining Milestone-5 work)

- **Bloody Events (§16)** — the gala/wedding set-piece class as dramatic gate-clearing triggers (the
  content handoff notes this becomes authorable now that the gate system exists).
- **"Resentment of the risen" (§13)** — a parvenu friction modifier with high-legitimacy houses that
  decays over generations; today the soft-lock is the legitimacy *gap*, not yet social contempt.
- **Sphere inputs beyond career** — titles, marriages and standing should also grant sphere influence
  (§7); today it derives from member careers (now also fed by title rank via `seizures`→threat).
- A small refinement: a resolved non-repeatable crisis can currently re-onset if its gate still holds;
  a "spent" suppression flag could keep it down.

## Content asks (flagged to the author)

- **Confirm the three spheres** + career→sphere mapping in `spheres.json` (strawman: Navy←military,
  Treasury←stewardship, Senate←law).
- **Confirm the title ladder** legitimacy requirements in `titles.json` (strawman: Knight 10 / Baron 25 /
  Count 40 / Duke 60 / Archduke 80 / Emperor 95).
- **Per-scenario starting title + legitimacy** for the three rise-path scenarios — `the_fallen_house`
  (a title held above a fallen house's standing = instant soft-lock drama) and `the_risen_officer` (a
  landless low-born) would *showcase* §13. Today they default to landless/0 (inert). I can seed these
  on your word.
- **Title/succession events** — petition scenes, claims forged by marriage (`grantClaim`), usurpation.
  The `title`/`grantClaim` vocabulary and the `title_*`/`house_legitimacy` counters are ready to gate them.
- ~~Sphere/coalition-themed events~~ ✅ **delivered** (the 8-event coalition chain, bridged to spheres).

## How to verify

```bash
dotnet test                                     # 91 pass
dotnet run --project src/Tools -- validate      # 55/55, 0 errors
dotnet run --project src/Tools -- crisis --seed 1
dotnet run --project src/Tools -- spheres --seed 1
dotnet run --project src/Tools -- progression --seed 1
```
