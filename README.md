# Praetoria

A human-only galactic grand-strategy / dynasty roleplay game — Crusader Kings character
depth × Stellaris systemic depth, in the tone of *Legend of the Galactic Heroes* / *Red Rising*.
See the design docs in [`/docs`](docs/): the [GDD](docs/GDD.md) and the [Build Spec](docs/BuildSpec.md).

**Engine:** Godot 4.x · C# (.NET 8). **Build philosophy:** simulation first, presentation
second; the core is proven headless before any UI; all content-bearing data is data-driven.

## Architecture (BuildSpec §1–2)

The cardinal rule: **simulation and presentation are split.** The entire game state and rules
live in a plain-C# `Core` assembly with **no dependency on Godot**. Godot is an additive
presentation + input layer (arriving at Milestone 3). This is what enables headless testing,
the reactive UI, modding, and clean saves.

```
/docs                  GDD + Build Spec + milestone notes
/src
  /Core                PLAIN C# — no Godot. State, Events, Data, Rng, Commands, Systems.
  /Tools               headless console harness + content validator (this is "the game" today)
  /Game                Godot layer — staged for Milestone 3 (not in Praetoria.sln yet)
/content               DATA FILES (JSON): events (logic), text (prose), scenarios, …
/tests                 xUnit against Core — runs WITHOUT Godot
Praetoria.sln          Core + Tools + Tests (everything that builds headless)
```

## Status — Milestones 1–2 complete + M4 economy + M5 crisis system (headless core)

The headless **Academy Crucible** runs with a full turn loop. Implemented: the State model, the
data-driven Event Engine (binder → eligibility → Director → consequence applier), JSON loaders with
a strict **logic/text split**, a deterministic seeded RNG, action pools, a command bus through
which both the player and NPC houses act, the formal **Briefing → Action → Resolve** turn structure,
the **five-resource domain economy** (holdings, specializations, buildings, per-turn accrual/upkeep),
the **gate/cascade/damper crisis system** (§16 — crises as gated buildable states, not random events),
the **power-balance spheres** (§7 — career feeds sphere-influence feeds threat/coalition),
a console harness, and an authored content set (47 events, 3 scenarios, a crisis pool, the spheres).

- **Milestone 1 — "The Engine Breathes":** [`docs/MILESTONE_1_NOTES.md`](docs/MILESTONE_1_NOTES.md).
  Proven: *a choice in one event arms state that makes a different, unscripted event eligible later*
  (the barracks insult → the duel).
- **Milestone 2 — Turn Loop, Pools & Commands:** [`docs/MILESTONE_2_NOTES.md`](docs/MILESTONE_2_NOTES.md).
  Proven: *a full turn cycle with pool spending and NPC actions resolves deterministically from a seed.*
- **Milestone 4 — Economy & Holdings (headless slice):** [`docs/MILESTONE_4_NOTES.md`](docs/MILESTONE_4_NOTES.md).
  Proven: *a house treasury accrues from its holdings each turn and an investment made through the
  command bus compounds — deterministically from a seed.* (Galaxy mode / fleets / succession are the
  remaining UI-bound M4 work; Milestone 3, the Court UI, is the Godot layer in between.)
- **Milestone 5 — Crisis System + Power-Balance Spheres:** [`docs/MILESTONE_5_NOTES.md`](docs/MILESTONE_5_NOTES.md).
  Proven: *a crisis can be engineered or defused via gates; a cascade escalates and a damper (earned by
  prior play) arrests it; an NPC house triggers a crisis* — and *a house's career-fed dominance of an
  estate accrues threat until a coalition crisis becomes causable.* (Progression §13 and Bloody Events
  are the remaining M5 work.)

## Quickstart (needs the .NET 8 SDK)

```bash
# Lint the content set (every event has text, tokens resolve, no orphans)
dotnet run --project src/Tools -- validate

# Watch the cascade: 'the_duel' is impossible until the barracks insult arms a feud flag
dotnet run --project src/Tools -- demo --seed 1

# Watch a full turn cycle: Briefing → Action (spend pools) → Resolve (NPC houses act)
dotnet run --project src/Tools -- turn --seed 1

# Watch the domain economy: holdings accrue resources each turn, then invest in a building
dotnet run --project src/Tools -- economy --seed 1

# Watch the crisis system: a revolt's gate clears, it cascades into civil war, dampers gate on prior play
dotnet run --project src/Tools -- crisis --seed 1

# Watch the power balance: an heir's Navy career spikes the house's sphere share until rivals coalesce
dotnet run --project src/Tools -- spheres --seed 1

# Play the Academy as text (interactive; --auto picks first affordable choice each report)
dotnet run --project src/Tools -- play --seed 1 --turns 8

# Run the test suite
dotnet test
```

## License

TBD.
