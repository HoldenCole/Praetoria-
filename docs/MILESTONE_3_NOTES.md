# Milestone 3 — Court Mode UI (first draft) + the Galaxy map + character creation

**Goal (BuildSpec §M3):** wrap the working core in the Court three-column UI; base theme; the briefing feed
renders events by tier, choices spend pools, dynasty rail + crisis clock display. Plus the first slice of
Milestone 4's Galaxy mode (the map itself) and character customisation (GDD §13/§18).

**Status:** ◐ **written and compiling; not yet run.** The session that built this had no Godot runtime or
display. Everything in `/src/Game` compiles against the `Godot.NET.Sdk 4.3.0` NuGet package (CI checks this),
and every value the UI shows comes from Core code that *is* tested. Layout, sizing, fonts, colours and feel
are unverified until someone opens `project.godot` in Godot 4.3 (.NET build) and presses Play. Expect a
first-run polish pass, not a rewrite.

## What was built

| Area | Where | Notes |
|---|---|---|
| Entry point | `src/Game/Main.tscn`, `GameRoot.cs` | Loads `/content` (`res://content`), shows creation, then hosts the two modes behind a persistent top bar: status · **Court / Galaxy** toggle · **End turn** · Save · Load · Quit. Saves to `user://praetoria.save.json`. |
| State facade | `src/Game/GameState.cs` | The one object views talk to: content, the `TurnController`, `Start / EndTurn / Resolve / Execute / Save / Load`, a `Changed` event. No rules. |
| Character creation | `src/Game/CreationScreen.cs` + Core `Creation/` | Scenario, seed, ironman/continue; name, sex, background (with its delta spelled out), up to 2 nature + 2 aptitude traits (boxes lock at the cap), track, ambition. Lists come from `/content/traits`, `/content/backgrounds`, `/content/careers`; the engine validates and the screen shows the error. |
| Court mode | `src/Game/CourtView.cs` | **Left:** the character card (rank name, traits, ambition, stress, regency), action pools, the Steward (treasury, holdings, build buttons through `BuildCommand`), the Chancellor (legitimacy, instability, claims, the three rise-path buttons through the progression commands, spheres/threat). **Centre:** the briefing — one card per fired event: title + tier, substituted prose, one button per choice (disabled with a tooltip when gated/unaffordable), resolved state; the dynasty-death end card. **Right:** the dynasty rail (members, bonds, dispositions), connections outside the house (with their flags), the crisis clock (counters, active crises with damper buttons through `ApplyDamperCommand`, gates-cleared list), the last ten chronicle lines. |
| Galaxy mode | `src/Game/GalaxyView.cs` (`GalaxyView`, `GalaxyMap`) | A `Control` that `_Draw`s the `GalaxyProjection`: lanes, nodes filled with the owning house's accent (unclaimed = steel), a glow for held territory, a gilt ring for the player's systems, a red ring for contested ones, names + owners. Click selects; the side panel shows legend (houses + system counts), the selected system's owner, holdings (your own in depth; rivals' as rough public record — GDD §19 mixed visibility), lanes, and jumps from your seat. |
| Theme | `src/Game/CourtTheme.cs` | Base palette + the first reactivity: fractured-stars (cold steel) vs Imperium (gilt) palettes; house accent on headers; panel/heading/body/button helpers. |
| Court model | `src/Core/Presentation/CourtProjection.cs` | Everything the Court shows as plain records (character card, pools, treasury, holdings with build/ship options, standing with path availability, fleets, briefing cards with substituted prose and per-choice availability, members, connections, crisis clock with damper availability, chronicle). `CourtProjectionTests` check it against the engine for all five scenarios — the Court's logic is verified without a window. |
| Fleets | `src/Core/State/Fleet.cs`, `Systems/FleetSystem.cs`, `Commands/FleetCommands.cs`, `/content/fleets/rules.json` | House vs office fleets; move orders (one jump/turn, Treasury pool); readiness; per-hull upkeep; *Lay down a hull*; incursions (`incursions`, `incursion:<house>`); the protagonist house's decaying intel on rival fleets; NPC houses raise hulls and hostile heads sail on your seat. Map glyphs: chevron = house fleet, diamond = office fleet; rivals hollow and faded by confidence; dashed course lines for your own. Orders are given from the system panel. |
| War on the map | `GalaxyView` system panel, `CourtView` Admiral panel | *Declare war on House X* from any rival's system; *Sue for peace* when at war (disabled with a reason while they outgun you); the Admiral lists standing wars and flags an envoy awaiting your answer in the briefing. Battles and conquests land in the chronicle and the crisis-clock counters. |
| Typography + portrait slot | `CourtTheme.Serif / Sans`, `PortraitBadge` | System serif for Court, sans for Galaxy (BuildSpec §6), by family name with fallbacks — no font files yet. An initials badge in the house accent stands where a portrait will go. |
| Core additions | `src/Core/Galaxy/*`, `src/Core/Creation/*`, loaders | `Galaxy` (systems, lanes, neighbours, hops, problems), `GalaxyProjection` (nodes/lanes/territory), `TraitCatalog`, `BackgroundDef/Catalog`, `CharacterCreation`; scenario `galaxy` + holding `system`; character `flags`. All unit-tested headless. |
| Content | `/content/galaxy/the_reach.json`, `/content/traits/traits.json`, `/content/backgrounds/backgrounds.json`; all five scenarios placed on the map | 14 systems, 20 lanes; 9/11/8 vocab entries; 6 backgrounds. |
| Harness | `create`, `map`, `play --background …` | The same creation and map data, headless. |

## Acceptance (BuildSpec §M3) — where it stands

*"The Academy + early court game is fully playable via UI, no console needed."* Structurally yes: every
console verb has a button. **Unverified at runtime.** The checklist for the first Godot session:

1. Open `project.godot` (Godot 4.3, .NET). Build. Play. The creation screen should appear.
2. Take the stage with defaults on `academy_crucible`. The Court should show three columns and a briefing of up to 3 cards.
3. Click a choice; the card marks Resolved and pools drop. End turn; the chronicle grows, NPC actions resolve.
4. Toggle Galaxy: 14 systems, Tessaly ringed gold, four houses in the legend; click a system.
5. Save, quit, Load — same turn, same state.
6. Then polish: column widths (`SizeFlagsStretchRatio`), minimum sizes, wrapping, fonts (ceremonial serif for Court, technical sans for Galaxy per BuildSpec §6), portraits slot.

## Key decisions

- **Views rebuild wholesale** on every change (`Refresh()` clears and re-adds). Simple and always correct; optimise only if it stutters.
- **UI nodes are built in code**, not in `.tscn` files, except the one-node main scene. Fewer blind-authoring errors; everything is diffable C#.
- **The Game project stays out of `Praetoria.sln`** (BuildSpec M1 rule) but CI compiles it separately, so UI breakage is caught without a Godot install.
- **Mixed visibility is enforced at the panel**, not in Core: the map panel shows a rival's holding as "pop ~tens · 2 building(s) reported". When fleets arrive, intel confidence belongs in Core state (BuildSpec §3 "reported intel with a confidence value").
- **Character creation is a Core service** (`CharacterCreation.Apply`), so the console and the UI share one validated path.

## Why it is still unrun (and the two ways to change that)

This repository's cloud sessions cannot execute a downloaded Godot binary: the attempt (official 4.3 .NET
release, checksum verified against Godot's `SHA512-SUMS.txt`) was refused by the session's safety policy.
Xvfb and Mesa *are* available in the environment, so with permission a headless/virtual-display run and
screenshots are possible. Either:
1. **Run it locally** — Godot 4.3 (.NET) → open `project.godot` → Play; follow the checklist above.
2. **Allow it in a cloud session** — add a Bash permission rule for running the Godot binary; the session can
   then import, build and run the project under Xvfb and attach screenshots to the next pass.

## Deferred

- Running it. Font files, real portraits, audio (Milestone 7).
- Combat / blockade / seizure-by-fleet — fleets move and are seen but do not fight.
- A dedicated full-tree dynasty overlay (GDD §19); today the rail lists members and connections.
- Contextual connection-surfacing inside event cards ("who do I know in House Corwin?").
