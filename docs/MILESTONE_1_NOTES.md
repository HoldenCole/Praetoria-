# Milestone 1 — "The Engine Breathes"

**Goal (BuildSpec §M1):** prove the headless core. State model + Event Engine + JSON loaders
(logic/text split) + seeded RNG + a console harness + the Academy Crucible event pool, with
unit tests covering binding, eligibility, consequence-application, and one cascade. **No Godot UI.**

**Status:** ✅ complete. 21 tests pass; the content validator is clean; the cascade is proven
in isolation, on the authored content, and live in the `demo` command.

## What was built

| Area | Where | Notes |
|---|---|---|
| Seeded RNG | `src/Core/Rng` | `SplitMix64Rng` — fixed algorithm, platform-independent stream; full position is one `ulong` (`State`), so it serialises with the World. |
| State model | `src/Core/State` | `Character`, `House`, directed `Relationship` graph (two-axis loyalty), `World` (flags, counters, history, Director memory). Composition over inheritance (BuildSpec §3). |
| Event engine | `src/Core/Events` | `Binder` (role→character), eligibility, `Director` (weighting/suppression/rising-action), `EventEngine` (gather → select → resolve). |
| Logic mini-language | `src/Core/Events/Conditions.cs`, `Effects.cs` | JSON-authored predicates/consequences parsed to objects. New verbs = one class + one parser case. |
| Logic/Text split | `src/Core/Data` + `/content` | Events (`/content/events`) hold zero prose; text (`/content/text`) is joined only by id, with `{role.field}` substitution (GDD §15). |
| Content tooling | `ContentValidator`, `praetoria validate` | CI gate: every event has text, every choice has prose, tokens resolve to declared roles, no orphan text. |
| Console harness | `src/Tools` | `play` / `demo` / `validate`. Runs the whole sim with no engine. |
| Academy Crucible | `/content/{scenarios,events,text}` | 4 cadet houses, 7 events across all three tiers, two emergent chains. |

## The cascade (acceptance property)

A consequence in one event changes State so a *different, unscripted* event becomes eligible:

- **Barracks insult → the duel.** `barracks_confrontation`'s `mock` choice arms a per-character
  `feud` flag. `the_duel`'s role can *only* bind to a character bearing that flag, so the duel is
  **unbindable — literally impossible — until the insult happens.** Nothing sequences them; the
  link is pure emergent state. (A second chain: friendly overture → sworn oath.)

Proven three ways: `CascadeTests` (synthetic), `ContentIntegrationTests` (authored files), and the
`demo` command, which prints the eligible pool each turn so you can watch the duel appear.

## Key decisions / deviations

- **Project layout vs. the spec.** BuildSpec §2 describes a Godot project root. To keep the
  Milestone-1 rule literal — *Core and tests build and run without Godot* — `Core`/`Tools`/`Tests`
  are plain SDK-style .NET projects in `Praetoria.sln`. The Godot `/src/Game` project and
  `project.godot` exist as the Milestone-3 entry point but are **excluded from the solution** so the
  headless build never needs the Godot SDK. Same `Core` code feeds both.
- **Roles bind sequentially** (declared order), so a later role's constraints may reference earlier
  roles and `self`. Greedy with RNG tie-break (deterministic). Full backtracking deferred — not
  needed at this scope.
- **One event per turn** in M1. The full Briefing → Action → Resolve loop, action pools, and the
  command interface are Milestone 2.
- **Forward-compat slots already present but inert:** `House.SphereInfluence`, `Choice.Cost`
  (pools), `Commands/ICommand`, world `Era`. They serialise now so saves stay compatible (GDD §20).

## How to verify

```bash
dotnet test                                   # 21 pass
dotnet run --project src/Tools -- validate    # 0 errors / 0 warnings
dotnet run --project src/Tools -- demo --seed 1
```

## Next — Milestone 2

Formalise the turn structure (Briefing → Action → Resolve), the action pools (Influence /
Treasury / Agents), and the command interface; route NPC character actions through commands.
Still headless. Acceptance: a full turn cycle with pool spending and NPC actions resolves
deterministically from a seed.
