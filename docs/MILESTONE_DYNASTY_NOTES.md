# Dynasty Lifecycle — Aging, Death, Birth, Succession (GDD §14)

**Goal:** the loop that makes the **dynasty, not the character, the save-persistent entity** — the
literal premise of the game (GDD §5/§14). It also realises systems already built that were waiting on
it: the §13 soft-lock ("rules a powder keg *until generations* stabilise it"), title/legitimacy
persistence across deaths, and the succession events authored against the §13 interface.

**Acceptance:** characters age; the old die; the protagonist's death passes the seat to an heir (the
House endures) or ends the dynasty; married couples bear children — all deterministic from a seed.
✅ Proven by `DynastyTests` and the `dynasty` console demo. (Satisfies the BuildSpec M4 "advance through
a succession" and M6 "dynasty death / ironman vs continue" criteria.)

**Status:** ✅ complete. 102 tests pass (93 prior + 9 dynasty); engine `validate` clean (62/62); both
content validators clean; the `dynasty` demo runs deterministically.

## What was built

| Area | Where | Notes |
|---|---|---|
| Character lifecycle fields | `State/Character.cs` | `Sex`, `MotherId`, `FatherId` — births and blood-succession rights. |
| Dynasty system | `src/Core/Systems/DynastySystem.cs` | Per-turn `Tick`: age everyone; roll deaths (quadratic mortality from age 50, certain by ~95) with succession; roll births for fertile married couples. Static `Die`/`FindHeir`/`DeathProbability`. |
| Heir handoff | `DynastySystem.Succeed`/`FindHeir` | Protagonist death → eldest blood child, else eldest house member, takes the seat; House (title/legitimacy/holdings) persists; `generation` increments. No heir → `dynasty_dead`. |
| Vocabulary | `age` condition, `kill` effect (+ parsers) | Events can gate on age and kill characters (assassination / duel / Bloody Event); killing the protagonist auto-fires succession. |
| Scenario fields | `ScenarioLoader` | `sex` / `mother` / `father` on characters. |
| Turn integration | `TurnController.EndTurn` | `Dynasty.Tick` runs in the Resolve phase (GDD §9 "character ages… possibly dies → succession"). |
| Harness | `src/Tools` — `dynasty` (new) | An old ruler dies and his married heir succeeds while the couple bears children — the house outliving the man. |

## The design choice that matters: frugal RNG

Mortality is rolled **only at/after age 50**, and birth **only for an eligible (married, fertile)
couple. A young, unmarried cast — the entire Academy — draws *no* randomness from the lifecycle.** That
is why this system is **always-on yet perturbs not one existing test**: the academy/economy/crisis
determinism runs consume the exact same RNG stream as before (proven — 93 prior tests unchanged, and
`YoungUnmarriedCast_ConsumesNoRandomness` asserts the RNG position is untouched). The lifecycle only
touches the stream where a life actually turns.

```bash
dotnet run --project src/Tools -- dynasty --seed 3
```

## Key decisions

- **House persists, character resets** (GDD §13/§14): title, legitimacy, holdings and claims live on the
  House and survive the handoff; the new head brings their own age/career.
- **Primogeniture by age** among blood children, then any house member — a simple, deterministic rule;
  richer succession law (matrilineal/elective, contested claims) is future work.
- **Patrilineal births**: a child joins the father's house. Matrilineal marriages are a future toggle.
- **`kill` shares the succession path**, so a Bloody-Event assassination of the protagonist resolves
  exactly like a natural death — one code path, deterministic.

## Deferred / content asks

- **Scenario casts need `sex` + a `marriage` tie** for births to occur — none currently do, so the
  dynasty layer is structurally present but dormant in the shipped scenarios. Seeding a couple (and
  ages spanning generations) brings it alive; I can do this on request.
- Succession depth: contested successions, regency for child heirs, matrilineal/elective law,
  "scatter the seed" heir-hiding (§14), and the dynasty-death endgame wiring (M6).
- Aging effects on skills/fertility curves and infant mortality are simplified.

## Hardening / integration pass

A whole-stack soak (`IntegrationTests` — a rich world driven 40 turns through `TurnController` against
the real content) flushed out interaction bugs the isolated unit tests couldn't:

**Fixed**
- **Crises never resolved.** Once triggered, a crisis stayed active forever (the player auto-resolves
  *events*, not *dampers*, and there was no de-escalation). Added `CrisisEngine.Decay`: an active crisis
  whose **gates no longer hold** (its root cause eased — unrest fell, legitimacy recovered) winds down
  one severity/turn and resolves. Crises now cycle instead of accumulating.
- **Coalition pressure ratcheted unboundedly**, pinning `coalition_war` active permanently. Capped it
  (`SphereSystem.MaxCoalitionPressure = 6`) and made the crisis **discharge** the pressure on onset, so
  it can then decay out — modelling recurring *waves* of coalition pressure rather than a stuck war.
- **Pools weren't upgraded on succession.** The new head kept NPC-scale pool regen (and a newborn heir
  would have had none). `DynastySystem.Succeed` now grants the heir player-scale `ActionPools`.

**Known issues / design observations (left intentionally — no crash, determinism intact)**
- **Threat from trivial absolute share:** when all rival admirals die, a house holding `navy = 1`
  reads as 100% Navy share → max threat → recurring coalitions. Defensible ("you own the whole Navy"),
  but the absolute scale is small; a magnitude-weighted threat formula would need a demo/test rebalance.
- **`realm_unstable` never clears** (set by crisis `onTrigger`); the `civil_war` gate stays partly armed,
  though its unrest/legitimacy components still gate it. Left to content.
- **Imperial `legitimacy` counter is unbounded-negative** (revolts/civil wars lower it with no floor or
  recovery) — could keep late-game gates warm; mitigated by the unrest component + decay.
- **Non-repeatable crises can re-onset after resolving** (the flag only blocks re-onset *while active*).
  Reads as "recurring" for revolts/coalitions; a one-time crisis would need a spent-marker.
- **No regency**: a child can inherit the seat (with player pools) but there's no minor-ruler model.

## How to verify

```bash
dotnet test                                     # 104 pass (incl. IntegrationTests soak)
dotnet run --project src/Tools -- validate      # 62/62, 0 errors
dotnet run --project src/Tools -- dynasty --seed 3
```
