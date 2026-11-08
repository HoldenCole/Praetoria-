# Praetoria — Development Cycle

The owner's standing instruction for how this project advances. Every session reads this first and works the
current phase without asking.

```
40 things  →  QA  →  implement graphics  →  improve graphics  →  QA  →  10 small QoL fixes  →  check for bugs  →  reassess  →  repeat
```

- **40 things**: systems, content and depth. "If you see a gap, something that would make the game better or
  deeper, or closer to the design — just do it." Each thing lands with tests and a line in `CHANGELOG.md`.
- **QA**: full test suite, both validators, a 50-turn auto-play soak of every scenario on several seeds, the
  balance simulation (`praetoria simulate`), and a read of the chronicle for absurdities. Fix what it finds.
- **Implement graphics / improve graphics**: the Godot layer (`/src/Game`). Compile-checked in every session; run
  and polished in a session with Godot 4.3 and a display (see `MILESTONE_3_NOTES.md`).
- **10 small QoL fixes**: harness and UI ergonomics, labels, tooltips, defaults, error messages.
- **Check for bugs**: targeted soak with hostile seeds, the determinism and save/load round-trips, a review of
  engine-written flags that content never clears.
- **Reassess**: update `GAP_ANALYSIS.md` and the README's gap table; pick the next 40.

**The bar for V1:** at minimum everything Star Dynasties does; since the simulation is already deeper, V1 should
make Star Dynasties look unfinished. Nothing in V1 may feel like it is building toward an absent finale (GDD §16).

**Playing it.** The playable build is the browser client (`src/Web`, Blazor WebAssembly over Core), published to GitHub
Pages from `main`. Every session can play it headless: `dotnet publish src/Web -c Release -o out`, serve `out/wwwroot`, and
drive it with Playwright (see `docs/WEB_CLIENT.md`). Graphics phases iterate on screenshots from that loop.

**Phase log**
- 2026-11-07 — "40 things" phase: 31 done (v0.7.0-alpha) + 20 done (v0.8.0-alpha) = 51. First QA pass run with
  the simulation (found and fixed: an isolated scenario, lines dying out, cadet events for grown nobles, relentless
  heir-hunting). Next: QA proper, then the graphics phases (need a Godot session).
- 2026-11-07 — QA phase: `praetoria qa` soak added (randomised player, determinism replay, save/load round-trip, coverage,
  oddities). Found and fixed: unbounded NPC debt (fleet desertion + steward selling/tithe), caravan exchange rate, the
  First Morning never opening, newborns bound as stewards (adult-only Binder default), nephew wed to aunt (kin rule),
  raw ids in crisis logs, yearly births. Then "implement graphics": the browser client, built and screenshotted end to end.
- 2026-11-07 — Graphics (sigils, portraits, pips, map tooltips/readiness/jumps, tree) → QA (six scenarios × 20 turns in a
  headless browser, hostile-settings soak) → 10 QoL fixes (choice reasons, since-last-turn, yields, badges, save flash,
  chronicle link, tree dedupe, phone layout, unique child names, landless steward line) → bug check (events fired on
  presentation, four-turn repeat suppression, adult advisors, named crises, chronicle event ids) → reassessed in
  `GAP_ANALYSIS.md` §4. Next: the forty drawn from §4.
- 2026-11-07 — Direction from the owner: a grand-strategy UI, not a visual novel; multiple screens; the Court as a
  customisable office; a bigger, better galaxy (6 regions × 3 worlds to start, each with culture, industry, loyalty and
  houses; minor houses holding little or nothing; governors matter); UI first, then economy/holdings/businesses/wealth;
  still the player's RPG inside the dynasty. Done this pass: the expanded Reach with regions, minor houses and governors
  (engine + content), and the shell rewrite (office, galaxy, worlds, house, military, dynasty, houses, chronicle, outliner,
  event windows, succession interstitial).
- 2026-11-07 — Owner: the UI looked like a college project (photos, cartoon avatars, flat boxes). Researched the
  references directly (Star Dynasties, Stellaris, Endless Space 2 screenshots) → `docs/ART_DIRECTION.md`. Rebuilt the
  galaxy as a rendered WebGL scene, the office as a lit 3D set, portraits as drawn cameos; removed all stock photographs
  and avatars. Every pass screenshotted through headless WebGL before commit.
- 2026-11-08 — Owner: the office is the bar; portraits can wait; the galaxy needs a little more; now the Academy, the
  political system, then Dynasty, Military and House to the office's standard, and only then depth. Done: Senate and
  Academy sets, then the Hall, Command Deck and Keep sets with their screens. Lesson from the lighting passes: a
  downward-facing surface under a HemisphereLight takes the *ground* colour, so a near-black ground leaves every ceiling
  and vault black however strong the sky; and materials under 0x40 grey read as unlit at any light level.
- 2026-11-08 — Houses, Worlds, Chronicle and You on sets (gallery, orrery, archive, quarters): every screen now stands
  on a set. Lessons: a screen with a wide aside has a near-square stage, so its camera sits further back; a set whose
  camera is outside its dome shows stars as squares at the frame edge, so the dome must enclose the camera; plaques
  closest to the camera dominate, so arms nearest the viewer are the ones to shrink. UI phase closed; next is depth.
- 2026-11-07 — Owner: office good; portraits can wait; galaxy needs a little more; then UI for the Academy and the
  political system at the office's level; then depth. Done: galaxy polish; Senate and Academy screens on new 3D sets.
