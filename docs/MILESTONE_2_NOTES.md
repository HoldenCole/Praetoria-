# Milestone 2 — Turn Loop, Pools & Commands

**Goal (BuildSpec §M2):** formalise the turn structure (Briefing → Action → Resolve, GDD §9),
the action pools, and the command interface (§7); NPC characters act through commands. Still
headless/console.

**Acceptance:** a full turn cycle with pool spending and NPC actions resolves deterministically
from a seed. ✅ Proven by `TurnCycleDeterminismTests` and the `turn` console demo.

**Status:** ✅ complete. 37 tests pass (21 from M1 + 16 new); content validator clean; both the
M1 cascade (`demo`) and the M2 cycle (`turn`) run deterministically.

## What was built

| Area | Where | Notes |
|---|---|---|
| Action pools | `src/Core/State/ActionPools.cs` | Influence / Treasury / Agents (GDD §9). Spend-down with per-turn regen + soft cap. Player-scale vs. abstracted-NPC budgets. Held per-actor on `World.Pools`. |
| Command interface | `src/Core/Commands` | `ICommand` (ActorId · `CanExecute` · `Execute`), `CommandContext`, `CommandExecutor` (guards, applies, logs for replay). The single mutation path for player **and** AI (BuildSpec §7). |
| Commands | `ResolveChoiceCommand`, `TrainCommand`, `NeedleRivalCommand`, `SeekAllyCommand` | The player's event-resolution and the Academy-era NPC verbs. All cost-gated. |
| Turn structure | `src/Core/Systems/TurnController.cs` | Explicit `BeginTurn` (advance + refill + compile briefing) → Action (player resolves, spending pools) → `EndTurn` (NPCs act) phases (GDD §9). One shared RNG threads selection/binding/AI. |
| Briefing | `EventEngine.SelectBriefing` | Director picks up to N distinct events per turn — the "briefing feed" the Court UI will render (GDD §19). |
| NPC AI | `src/Core/Systems/NpcAi.cs` | Each NPC house acts through the same command bus on an abstracted budget (GDD §3, §18). Trait/ambition-driven; deterministic (id order, RNG only for ties). |
| Harness | `src/Tools` — `play` (rewritten), `turn` (new) | `play` now runs the full phased loop with pools + NPC actions; `turn` is a deterministic full-cycle demo. |

## Acceptance property

A full turn cycle reproduces exactly from a seed — player pool-spending in Action **and** NPC
actions in Resolve. `TurnCycleDeterminismTests` runs the cycle twice and asserts identical command
log, RNG position, and a world-state digest (relationships + pools + history). A spread of other
seeds is checked to actually diverge, proving the RNG genuinely drives the run.

Watch it live:

```bash
dotnet run --project src/Tools -- turn --seed 1
```

## Emergent behaviour worth noting

Because NPCs act every Resolve phase through real commands, relationships drift on their own. In
the seed-1 run, Corwin (Arrogant) first needles the player, but once Sato's repeated needling has
soured Corwin's view of Sato below his dislike of the player, Corwin **switches targets to Sato** —
unscripted, just the AI re-reading State each turn. This is the §18 "people, not puppets" loop in
miniature.

## Key decisions / deviations

- **Two turn drivers coexist.** The simple M1 `GameSession` (one event/turn) is retained — the
  cascade `demo` and the M1 tests still use it. `TurnController` is the M2 phased loop. They share
  the same `EventEngine`; no duplication of rules.
- **Pools are per-actor**, stored on `World.Pools` keyed by character id, so NPCs spend their own
  abstracted budgets through the identical `CanAfford`/`Spend` path. Refill happens at `BeginTurn`,
  so a turn's spend is felt before relief.
- **Choice costs are data.** Costs live in the event JSON (`"cost": { "influence": 1 }`) and are
  enforced only at the command/`TurnController` layer — the raw `EventEngine.Resolve` stays
  pool-agnostic, which keeps the M1 path and tests valid.
- **NPC verbs are intentionally small** (train / needle / seek-ally) — enough to prove "NPCs act
  through commands, deterministically." The galaxy-scale command set (fleet orders, builds, plots)
  arrives with the economy and spheres in Milestones 4–5.

## How to verify

```bash
dotnet test                                   # 37 pass
dotnet run --project src/Tools -- validate    # 0 errors / 0 warnings
dotnet run --project src/Tools -- turn --seed 1
```

## Next — Milestone 3

Wrap the working core in the Court three-column UI (BuildSpec §6, the first Godot layer): the
briefing feed renders events by tier, choices spend pools, the dynasty rail + crisis clock display.
Base theme only. Acceptance: the Academy + early court game is fully playable via UI, no console.
