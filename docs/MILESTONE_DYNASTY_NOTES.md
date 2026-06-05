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

## How to verify

```bash
dotnet test                                     # 102 pass
dotnet run --project src/Tools -- validate      # 62/62, 0 errors
dotnet run --project src/Tools -- dynasty --seed 3
```
