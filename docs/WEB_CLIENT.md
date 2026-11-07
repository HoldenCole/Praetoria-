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

## Layout

- `Pages/Index.razor` — the state machine: loading → title/creation → play (Court · Galaxy · Dynasty · Chronicle) → epilogue.
- `Components/CreationScreen.razor` — scenario picker with a one-line brief per scenario, world knobs (seed, mode,
  difficulty, crisis intensity, family autonomy), character customisation (name, sex, track, ambition, background, traits).
- `Components/CourtScreen.razor` — three columns: the character, pools, treasury, council, standing, Great Crisis; the
  briefing with its choices and the realm's temperature; holdings, fleets, war, household, connections with every command.
- `Components/GalaxyMap.razor` — SVG star map: territory glow by house accent, lanes, fleets (own exact, rivals as intel with
  confidence as opacity), siege clocks, bloc rings, click-to-move with jump counts.
- `Components/DynastyPanel.razor` — line of succession, a nested family tree, bonds within the house.
- `Components/ChroniclePanel.razor` — the whole history by turn, with a "notable only" filter.
- `Services/GameClient.cs` — the one object the UI talks to.
- `wwwroot/css/app.css` — the look: deep space, brass, parchment text, house accents.
