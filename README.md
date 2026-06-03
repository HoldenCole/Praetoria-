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

## Status — Milestone 1 complete: "The Engine Breathes"

The headless **Academy Crucible** runs. Implemented: the State model, the data-driven Event
Engine (binder → eligibility → Director → consequence applier), JSON loaders with a strict
**logic/text split**, a deterministic seeded RNG, a console harness, and the first event pool.
See [`docs/MILESTONE_1_NOTES.md`](docs/MILESTONE_1_NOTES.md).

The Milestone-1 acceptance property is proven: *a choice in one event arms state that makes a
different, unscripted event eligible later* (the barracks insult → the duel).

## Quickstart (needs the .NET 8 SDK)

```bash
# Lint the content set (every event has text, tokens resolve, no orphans)
dotnet run --project src/Tools -- validate

# Watch the cascade: 'the_duel' is impossible until the barracks insult arms a feud flag
dotnet run --project src/Tools -- demo --seed 1

# Play the Academy as text (interactive; --auto picks first available choice each scene)
dotnet run --project src/Tools -- play --seed 1 --turns 8

# Run the test suite
dotnet test
```

## License

TBD.
