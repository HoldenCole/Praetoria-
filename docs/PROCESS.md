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
- 2026-11-08 — Depth phase, the economy (`docs/ECONOMY.md`): prosperity, belts and claims, ventures with charters,
  loans, house wealth and the ledger. The accrual was rebuilt around one ledger the forecast and the turn both read.
  Lesson: the order of state updates inside a turn is a contract the tests hold — unrest and population move before
  the yield is read, prosperity after it, and a refactor that read the yield first broke an exact assertion.
- 2026-11-08 — Owner: make the galaxy more interesting — inner, middle and outer rings; the core is commerce and the
  capital, the rim is lawlessness and rebellion and houses breaking off, the middle can go either way; expand the scope
  and give regions behaviour. Done (`docs/GALAXY.md`): ten regions in three rings with a capital, allegiance and
  lawlessness per region, risings, garrisons, free holds and free marches, four new regions with their houses and two
  worlds no one holds. Lesson: a ring rule changes exact economy numbers everywhere, so the tests that pin the shipped
  scenarios' arithmetic are the first to shout — read them as a list of what the rule touched, then update them.
- 2026-11-08 — Owner: the galaxy is small and cramped, one planet per region; show all the planets as Star Dynasties
  does, highlight the regions, space it out, and make the lanes sparse so chokepoints matter for trade and war. Done:
  bodies on every system drawn on their orbits, region hulls, a map twice as wide, forty lanes through gateways,
  chokepoints computed from the web with tolls and barred passage. Lesson: when a hand-built lane list is meant to
  create chokepoints, compute the betweenness before writing the flavour — the gate you named is not always the gate
  the graph makes.
- 2026-11-08 — Owner: the lines need fading, there is too much on screen; go back to the references; the map should be
  too big to fit and you move around it. Reference pass (Star Dynasties, Stellaris) → `docs/ART_DIRECTION.md` "The
  map, second look". Done: hairline lanes, a default view on the player's seat, a minimap with the viewport, a fit
  button, smaller markers, fainter fills, labels that thin out with distance.
- 2026-11-08 — Full gap QA after the map work: 285 tests, validator, `qa` soak, sixty-turn `simulate` across every
  scenario, a twenty-turn browser drive through every screen with a save/reload, and a probe of the whole economy at
  turns 11/31/60. Found: half the houses insolvent by turn 60 (alphabetical venture founding, debt spirals, unsold
  materials, headless houses), and the fallen house "dying" every sixty-turn run because the auto-player never wed.
  Fixed all five; mean house credits at turn 60 went from 14 to 99 and houses in debt from 7 to 1. Lesson: a soak
  that only asks "did it crash" misses an economy that quietly fails; the probe that prints the twelve poorest houses
  with their specializations found in one line what forty turns of green QA had not.
- 2026-11-08 — Owner: fortress income and the throne gate; then free holds and the capital as a prize; then "remember
  the cycle". Done: escort fees, the throne standing in the capital, free holds as actors with buy-back and war, the
  capital with the Senate's chair and rivals who covet it. Then a full cycle batch: raiders (lawlessness with a face),
  governors who scheme and revolt, wars that end in exhaustion and have prizes, fleets that hold chokepoints, the fief
  petition, the pools as a constraint, era theming, a route preview, and ten QoL fixes. Lesson: a pseudo-house for the
  lawless (at war with everyone, holding nothing) slots into every existing system — battles, intel, the map — for the
  price of one `AtWar` special case and a handful of exclusions; building pirates as a separate entity would have
  touched twenty files.
- 2026-11-07 — Owner: office good; portraits can wait; galaxy needs a little more; then UI for the Academy and the
  political system at the office's level; then depth. Done: galaxy polish; Senate and Academy screens on new 3D sets.
- 2026-11-08 — QA phase of the depth cycle. The soak's randomised player only ever answered events, so every
  player-economy, fleet and court event was unreachable and the "never fired" list lied by twelve. It now gives
  commands too (every legal intent listed, a shuffled few tried, coverage reported). Lesson: a QA harness that cannot
  reach half the verbs reports the content as broken when the harness is; give the soak the same verbs the player has.
  The probe then found that "insolvent" meant "a name with nothing left" for a third of the red ledgers — houses now
  fall (struck, folded, or entering another's service), and the fief comes from a patron when the court has only the
  capital.
