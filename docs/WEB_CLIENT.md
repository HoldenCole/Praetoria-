# The browser client (`src/Web`)

The playable build. A Blazor WebAssembly app over `Praetoria.Core`: the same `TurnController`, `CourtProjection`,
`GalaxyProjection`, `AdvisorReports` and `Epilogue` the console harness and tests use, rendered as HTML and SVG. No rules
live in `src/Web`; it reads projections and sends commands (BuildSpec §1 Pillar 1).

## Why a web client

Godot 4's .NET build cannot export to the browser, and the owner's bar is one link at the top of the README that runs the
game — plus a build the development sessions can play themselves for QA and graphics work. A WASM build of the headless
core meets both. The Godot layer (`src/Game`) stays as the future desktop shell and keeps compiling in CI.

## How content gets there

`Praetoria.Web.csproj` stages `/content/**/*.json` into `wwwroot/content/` with a `manifest.txt` at build time (the staged
copy is gitignored). At start the client fetches the manifest and every file, writes them into the WASM in-memory file
system under `/content`, and calls `ContentLoader.LoadFromDirectory("/content")` — the unchanged loader.

## Saves

A save is the world between turns (phase Idle), exactly as the harness and Godot layer define it. The client snapshots that
world after every Resolve phase; **Save** writes the snapshot to `localStorage` (autosaved each turn too), **Export**
downloads it as JSON, and the title screen offers **Continue** / **Load a save file**. A loaded game re-opens the current
turn's briefing.

## Running and testing it locally

```bash
dotnet publish src/Web/Praetoria.Web.csproj -c Release -o out
cd out/wwwroot && python3 -m http.server 8112
```

Then open http://localhost:8112/. For QA without a display, a Playwright script that starts a run, answers every report
with a random available choice for N turns, visits every tab, moves a fleet, saves, reloads and continues is the loop the
development sessions use; it screenshots each screen and fails on any console or page error.

## Deployment

`.github/workflows/pages.yml` publishes `out/wwwroot` to the `gh-pages` branch on every push to `main`, rewriting `<base href>`
to the repository path and copying `index.html` to `404.html` for deep links. GitHub Pages serves that branch; if it is not
yet switched on, Settings → Pages → Source *Deploy from a branch* → `gh-pages` / `(root)` does it once.

## Layout — the grand-strategy shell

A fixed shell, not a scrolling page: a resource strip across the top (treasury with per-turn forecasts, action pools,
era and turn), a navigation rail of screens down the left, the **outliner** down the right (reports, alerts, council,
fleets, holdings — every line jumps to the screen that acts on it), and End Turn docked bottom-right. Reports open as
event windows with the cast's portraits; the first visitor steps forward when a turn begins.

- `Pages/Index.razor` — the shell and its state: current screen, open report, succession interstitial, epilogue.
- `Components/TopBar.razor`, `Outliner.razor`, `EventWindow.razor` — the frame.
- `Components/OfficeScreen.razor` — **the Office**: an SVG room (walls, floor, window on your seat's world, banners with the
  house sigil, desk and chair, decor) that visitors with reports walk into; *Furnish the office* changes it, stored in
  `World.Cosmetics` so the save carries it. Beside it: the character sheet, "since your last turn", the Great Crisis.
- `Components/GalaxyMap.razor` — **the Galaxy**: six regions as coloured fields with labels, planets drawn by class, stations,
  lanes (front lines in red), fleets (own exact, rivals as intel), siege clocks, bloc rings; drag to pan, wheel to zoom,
  click a world for its planet card (class, culture, held by, governor, industry, population, loyalty, orbit, fleets,
  lanes), click a region for its roster; click your fleet then a ringed world to set course.
- `Components/SenateScreen.razor` — **Senate**: the political system on the Senate-chamber set (`sets.js`): the heads of
  the great houses seated by influence, the speaker's podium (or the throne under the Imperium), house banners; panels for
  the spheres of power (every house's share of Navy, Treasury and Senate), threat and coalition pressure, the ladder of
  titles with legitimacy and the three paths, the throne, succession, honors, crises and dampers.
- `Components/AcademyScreen.razor` — **Academy** (shown while the protagonist is 22 or younger): the parade-ground set at
  dawn with the cohort in ranks, the instructor on the dais, the cohort houses' banners; panels for your record and
  standing, the cohort with dispositions, bonds and marks, and the year so far.
- `Components/PlanetsScreen.razor` — **Worlds**: every world by region in a table (class, holder, governor, industry,
  population, loyalty, stations).
- `Components/HouseScreen.razor` — **House**: the keep's great hall set (`sets.js`): the house banner over the dais, your
  worlds as turning globes (stations as rings) on pedestals down the hall, a red warning light on any restless one, the
  household seated at the long table (click one for their card and foster/recall); panels for treasury and forecast,
  holdings with build / shipyard / governor's chair, decrees (tithe, exchange, succession), threat, honors, claims.
- `Components/MilitaryScreen.razor` — **Military**: the command deck set: a strategic table projecting the Reach as a
  hologram (your systems bright, war fronts in red, your fleets as markers), your admirals at the table (click one for the
  fleet's card: refit, command), the fleet parked outside the viewport above the seat's world, readiness / hulls / fronts
  on the wall displays; panels for the navy and each fleet, wars with score and peace, rival navies as reported bars,
  recent battles.
- `Components/DynastyScreen.razor` — **Dynasty**: the hall of the house set: the family tree as generations down a
  vaulted hall — the eldest by the hearth and great banner, children in rows toward you, brass lines on the floor from
  parent to child, a gold thread between spouses, spouses from other houses in their own colours (click anyone for their
  card: parents, spouse, disposition, heir mark, flags); panels for the house, the line of succession, bonds within the
  house, the remembered dead.
- `Components/HousesScreen.razor` — **Houses**: every house with sigil, title, worlds, stations, hulls, legitimacy, bloc,
  disposition; its people with marriage and intrigue actions; its domain; war and peace.
- `Components/ChroniclePanel.razor` — **Chronicle**: the whole history by turn.
- `Components/CreationScreen.razor` — the title and creation screen.
- `Components/Portrait.razor`, `Sigil.razor` — procedural portraits and heraldry; `Services/Svg.cs` — SVG helpers.
- `Services/GameClient.cs` — the one object the UI talks to. `wwwroot/css/app.css` — the look.

## Art and how it is made

The art direction is `docs/ART_DIRECTION.md`, written after looking at the reference games. Nothing is stock and
nothing is Earth; everything is rendered or drawn in-fiction at runtime (`src/Web/wwwroot/js/`):

- **The galaxy** (`space.js`, Three.js over WebGL): a shader nebula, thousands of bloomed stars with parallax, suns as
  glowing cores with flares, a hyperlane web (red along fronts, gold within a selected fleet's reach), territory as a
  soft influence field with lit borders in house colour, lit planets beside their stars, stations, siege arcs, bloc rings,
  fleet glyphs with engine glow and course lines, labels in a CSS layer; drag, wheel, click; picking goes back to Blazor.
- **The office** (`office.js`, Three.js): a lit set with a reflective brushed floor and brass seams, pilasters, a tall
  viewport onto the seat's world (a shaded planet, a moon, stars, a nebula, a passing hull), sconces and emissive strips in
  the house colour, banners carrying the sigil, a desk with a lamp, a holographic star table of the Reach, a star chart,
  trophy case or archive on the far wall; the player and the visitors as framed cameo plates with nameplates; haze,
  shadows and bloom; furnishing changes materials and props.
- **The sets** (`sets.js`, Three.js): lit 3D rooms built with the office's helpers, one per screen — the Senate (a domed
  chamber with an oculus light shaft, colonnade, tiers of benches and banners), the Academy (a flagstone parade ground
  under a dawn sky with a gas giant, a skyline of lit towers, colonnades, braziers and a reviewing dais), the Hall (a
  barrel-vaulted hall with ribs, hanging lamps and sconces, a hearth and the great banner, the family tree laid out by
  generation), the Command Deck (a steel deck with light rails, a glass wall onto the seat's world and the fleet outside,
  a holographic strategic table, wall displays) and the Keep (a timbered great hall with tall windows, a dais and seat,
  the worlds on pedestals, a candle-lit long table). Each set takes plain JSON from Blazor, places framed cameo plates
  for its people and calls back `OnPlate(key)` when one is clicked.
- **Portraits** (`portrait.js`, canvas): cameos — imperial coin profiles drawn from a parametric bust (forehead, brow,
  nose, lips, chin, jaw, neck), hair and beard shapes, a uniform collar and brass gorget in the house colour, relief
  shading, on a plate with the house's diagonal stripe; stable per character, greying and lined with age.
- **Planet sprites** (`fx.js`, canvas): noise-shaded spheres with lighting, atmosphere, clouds, bands or rock scatter.
- **Chrome**: smoked-glass panels with brass hairlines and corner brackets; Cinzel, Cormorant, Plex Sans.

Both WebGL scenes run at a frame budget (about 15–20 fps) and stop while the tab is hidden.
