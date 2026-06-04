# Milestone 5 — Crisis System + Power-Balance Spheres

**Goal (BuildSpec §M5, GDD §7 + §16):** the power-balance + threat/coalition system, two-ladder
progression + legitimacy soft-lock + three paths, and the full crisis gate/cascade/damper/Bloody-Event
system; NPC houses authoring crises.

**Scope note.** M5 is three large systems (spheres §7, progression §13, crises §16). This milestone
delivers two of the three — the **crisis gate/cascade/damper engine** and the **power-balance spheres +
threat/coalition** — end-to-end and headless, wired together (sphere dominance → coalition pressure →
a coalition crisis). **The two-ladder progression / legitimacy soft-lock and the Bloody-Event set-piece
class are the remaining M5 work** (see *Deferred*).

**Acceptance (this slice, BuildSpec §M5):** *a crisis can be engineered or defused via gates; a cascade
escalates and a damper (earned by prior play) arrests it; an NPC house triggers a crisis.* ✅ Proven by
`CrisisTests` + `CrisisDeterminismTests` and the `crisis` console demo.

**Status:** ✅ complete. 80 tests pass (64 prior + 10 crisis + 6 sphere); engine `validate` clean
(47/47); both content validators clean; the `crisis` and `spheres` demos run deterministically.

## What was built — power-balance spheres (GDD §7)

| Area | Where | Notes |
|---|---|---|
| Sphere model | `src/Core/Spheres/SphereCatalog.cs` | `Sphere` keys (navy/treasury/senate) + `SphereDef` (name + feeding career track). Data-driven catalog. |
| Loader | `src/Core/Data/SphereLoader.cs` → `ContentDatabase.Spheres` | Loads `/content/spheres/*.json`. |
| Sphere system | `src/Core/Systems/SphereSystem.cs` | Each turn **derives** a house's sphere influence from its members' career ranks on the feeding track (climb §13 = power-balance §7, one system), computes the **threat score** (sphere-dominance above an even split + holdings + seizures), and accumulates `coalition_pressure` / a `coalition_forming` flag when the protagonist's house grows over-mighty. RNG-free. |
| `sphere` condition | `Events/Conditions.cs` (+ parser) | Events/crisis-gates can read a house's sphere influence. |
| Turn integration | `TurnController.BeginTurn` | Recomputes spheres after economy accrual, before the turn's gates are read. |
| Content | `/content/spheres/spheres.json`; `coalition_war` crisis | The three estates (strawman — confirm); a coalition crisis gating on `coalition_pressure ≥ 3` whose writes cascade into the existing civil war. |
| Harness | `src/Tools` — `spheres` (new) | Shows an heir climbing the Navy → share/threat climb → coalition becomes causable. |

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

- **Progression (§13)** — the two ladders (title + career), the **legitimacy soft-lock** as a modifier,
  the three rise-paths, and "resentment of the risen." (The career ladder already feeds spheres; what's
  left is the *title* ladder + legitimacy.)
- **Bloody Events (§16)** — the gala/wedding set-piece class as dramatic gate-clearing triggers (the
  content handoff notes this becomes authorable now that the gate system exists).
- **Sphere inputs beyond career** — titles, marriages and standing should also grant sphere influence
  (§7); today it derives purely from member careers. And `seizures` is a threat hook the military layer
  will feed later.
- A small refinement: a resolved non-repeatable crisis can currently re-onset if its gate still holds;
  a "spent" suppression flag could keep it down.

## Content asks (flagged to the author)

- **Confirm the three spheres** + career→sphere mapping in `spheres.json` (strawman: Navy←military,
  Treasury←stewardship, Senate←law).
- **Sphere/coalition-themed events** — the *feel* of rivals ganging up on an over-mighty house. The
  `sphere` condition and the `coalition_pressure`/`threat`/`coalition_forming` accumulators are in and
  ready to gate such content.

## How to verify

```bash
dotnet test                                     # 80 pass
dotnet run --project src/Tools -- validate      # 47/47, 0 errors
dotnet run --project src/Tools -- crisis --seed 1
dotnet run --project src/Tools -- spheres --seed 1
```
