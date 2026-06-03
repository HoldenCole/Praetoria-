# Milestone 4 — Economy & Holdings (headless slice)

**Goal (BuildSpec §M4, GDD §17):** add the five resources and the holdings/specializations/buildings
economy to the headless Core. Per-turn accrual and upkeep run inside the existing turn cycle; the
player invests through the same command bus as everything else.

**Scope note.** Milestone 4 in the BuildSpec also covers succession and **Galaxy mode** (the
command-table map, fleet movement, mixed visibility). Those are UI/Godot work and aren't verifiable
in this headless environment, so this pass deliberately builds **only the economy spine** —
everything testable without a display. Galaxy mode, fleets, and succession are called out under
*Deferred* below and remain Milestone-4 work.

**Acceptance (this slice):** play from a single barony, watch its treasury accrue from holdings each
turn, invest in a building, and have the new yield compound — all deterministically from a seed.
✅ Proven by `EconomyDeterminismTests` and the `economy` console demo.

**Status:** ✅ complete. 64 tests pass (37 from M1+M2 + 27 new); content validator clean; the
`economy` demo and the existing `turn`/`demo`/`play` harnesses all run deterministically.

## What was built

| Area | Where | Notes |
|---|---|---|
| Five resources | `src/Core/State/Resources.cs` | `Resource` keys + `Resources` bundle (Credits / Materials / Manpower / Influence / Exotics, GDD §17). Used as both a held balance and a delta. Credits may go negative (insolvency); the other four clamp at zero. |
| House treasury | `src/Core/State/House.cs` | Each house now banks a `Resources Treasury` — the §8 House treasury (Imperial/personal split is later). Distinct from the per-turn action pools (§9). |
| Holding entity | `src/Core/State/Holding.cs` | A controlled place: owner, specialization, population, unrest, built slots, and a `SystemId` reserved for Galaxy mode. Held on `World.Holdings`. |
| Catalog (data) | `src/Core/Data/HoldingCatalog.cs` + `HoldingCatalogLoader.cs` | `HoldingSpec` (yield/upkeep/slots/popGrowth) and `BuildingDef` (cost/yield/upkeep/requires), loaded from `/content/holdings`. Code = mechanism, data = instances (BuildSpec §2). Joined to live holdings by id, exactly like events↔text. |
| Economy system | `src/Core/Systems/Economy.cs` | Per-turn accrual: each holding feeds its owner's treasury (spec + buildings − upkeep); populated worlds grow; insolvency raises unrest which suppresses Manpower output. **RNG-free and order-stable** → determinism preserved. No-op on an empty catalog. |
| Build command | `src/Core/Commands/DomainCommands.cs` | `BuildCommand` spends the house treasury to slot a building. Gated on ownership, free slot, specialization fit, no duplicates, affordability. Routed through the same `CommandExecutor` as player/NPC actions (BuildSpec §7). |
| Event vocabulary | `Effects.cs` / `Conditions.cs` (+ parsers) | `adjustResource` effect and `resource` condition let authored events read/move a house treasury — the hook for the Steward to "surface domain decisions" (GDD §17). |
| Turn integration | `TurnController.BeginTurn` | Builds an `Economy` from the loaded catalog and accrues each turn after pool regen. Backward-compatible: scenarios with no holdings are unaffected. |
| Content | `/content/holdings/{specializations,buildings}.json`; barony added to `academy_crucible` | Five specializations, four buildings; each house gets a starting treasury and one barony (player's is deeply managed, NPC houses' are abstracted per GDD §3). |
| Harness | `src/Tools` — `economy` (new); `play` shows treasury/holdings | `economy` runs the accrual ledger and auto-invests once affordable; deterministic from a seed. |

## Acceptance property

`EconomyDeterminismTests` runs the real authored scenario through six full turn cycles and asserts an
identical treasury/holdings ledger from the same seed, confirms the catalog + scenario holdings load,
shows the treasury accruing from holdings, and proves a `BuildCommand` issued through the bus
compounds Manpower yield in later turns (agri 3 + farm complex 2 ≥ 5/turn).

Watch it live:

```bash
dotnet run --project src/Tools -- economy --seed 1
```

## Key decisions / deviations

- **Resources vs. action pools are separate economies.** Pools (§9) are spend-down per-turn
  bandwidth; resources (§17) accumulate in a house treasury. "Influence" exists in both — pool =
  this turn's political bandwidth, resource = banked political capital. They never share a container,
  so the key overlap is harmless (documented in `Resources.cs`).
- **Accrual is deliberately RNG-free.** The economy is pure arithmetic over State, so adding it to
  `BeginTurn` did not perturb any existing seed-driven test — the M1 cascade and M2 turn-cycle
  determinism still hold unchanged.
- **Insolvency is a state, not an error.** Credits alone may go negative; unrest then climbs on
  populated holdings and throttles Manpower — the GDD §17 tension loop, ready to feed uprising
  crisis gates in Milestone 5.
- **NPC houses stay abstracted** (GDD §3): they own a single representative holding rather than a
  full economy. Only the player's house is managed in depth.
- **No new authored economy *events* yet.** The `adjustResource`/`resource` vocabulary and parsers
  are in and unit-tested, but adding Steward "raise taxes / sell a holding" events to the authored
  pool was held back to avoid perturbing the existing acceptance scenarios. That content is the
  natural next content pass.

## Deferred (remaining Milestone-4 work, needs the Godot/UI layer)

- **Galaxy mode** — the command-table star map, system/holding selection, mixed visibility
  (own holdings deep, rival intel fogged), house/office fleet distinction (GDD §19, §6).
- **Fleet movement** and the Materials→shipyard→fleet pipeline's military end.
- **Succession** — generational handoff of the dynasty and its domain.
- **Megastructures, anomalies/archaeology** (the Exotics + Crisis-foreshadowing engine, GDD §17).

## How to verify

```bash
dotnet test                                     # 64 pass
dotnet run --project src/Tools -- validate      # 0 errors / 0 warnings
dotnet run --project src/Tools -- economy --seed 1
```
