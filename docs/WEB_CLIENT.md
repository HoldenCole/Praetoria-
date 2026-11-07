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
- `Components/PlanetsScreen.razor` — **Worlds**: every world by region in a table (class, holder, governor, industry,
  population, loyalty, stations).
- `Components/HouseScreen.razor` — **House**: treasury and forecast, standing, title paths, decrees (succession, tithe,
  exchange, throne), the realm's temperature, holdings with build / shipyard / governor's chair, household.
- `Components/MilitaryScreen.razor` — **Military**: fleets with readiness, admiral, composition, refit, command, merge;
  wars with score and peace; recent battles; rival navies as reported.
- `Components/DynastyPanel.razor` — **Dynasty**: succession, the family tree, bonds.
- `Components/HousesScreen.razor` — **Houses**: every house with sigil, title, worlds, stations, hulls, legitimacy, bloc,
  disposition; its people with marriage and intrigue actions; its domain; war and peace.
- `Components/ChroniclePanel.razor` — **Chronicle**: the whole history by turn.
- `Components/CreationScreen.razor` — the title and creation screen.
- `Components/Portrait.razor`, `Sigil.razor` — procedural portraits and heraldry; `Services/Svg.cs` — SVG helpers.
- `Services/GameClient.cs` — the one object the UI talks to. `wwwroot/css/app.css` — the look.

## Art and where it comes from

The client is meant to look like a game, not a form. Its art is open-licensed or procedural (full list in `CREDITS.md`):

- **Landscapes** — public-domain photographs (via the Endless Sky collection) chosen by planet class, shown in the office
  window, the planet card and the worlds table. `Services/Land.cs` maps a world to a stable picture.
- **Portraits** — a pre-drawn pool of 80 illustrated faces (DiceBear *Lorelei*, CC0), two per sex, chosen by a hash of the
  character id so a face is stable everywhere; set in medallions ringed with the house accent and greyed with age.
- **Icons** — game-icons.net glyphs (CC BY 3.0) baked into `Services/Icons.cs` as path data; `Components/Icon.razor`.
- **Planets and nebulae** — drawn at runtime on canvas by `wwwroot/js/fx.js` (value-noise sphere with lighting, atmosphere,
  clouds, bands or rock-scatter per class; a nebula field with stars); the shell calls `praetoriaFx.paint()` after every
  render to fill `data-planet` images from a cache.
- **Chrome** — smoked-glass panels over night-steel with hairline brass frames and corner brackets; Cinzel for titles,
  Cormorant for prose, Plex Sans for readouts; a procedural noise texture on panels; house accents on everything that is a
  house's.
