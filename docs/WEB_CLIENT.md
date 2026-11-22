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

Rebuilt 2026-11-21 from the playthroughs (docs/PLAYTHROUGH.md, "A first game": N49, N62, N25). Before this a save was
the world between years: **Save** in the middle of a year stored nothing new, and a reload put an answered report back
unanswered.

**A save is the game as it stands.** Between years it is the world, as the harness and the Godot layer define it. In the
middle of a year it is the world with one thing more: the year's reports, who each was about, and the answer given if one
was (`World.MidYear`, written by `TurnController.SaveMidYear`, read by `TurnController.Resume`). Loading such a save opens
the same year where it was left; no year passes and nothing is drawn again. The years after are the same as if the game
had never been closed, and a test holds that to the last character of the save. `BeginTurn` clears the mark, so a tool
that loads a save and begins a year behaves as before.

**Where saves live.** In the browser's `localStorage`:

| key | what |
|---|---|
| `praetoria.autosave` | the running saga: written by the game as each year opens, by **Save**, and on leaving (Quit saves first) |
| `praetoria.slot.1` to `.3` | kept saves: written only when the player says so, from the **Saves** window |
| each key + `:label` | what the list shows: house, head, year, whether the year is under way, and when it was saved |

The **Saves** window (top bar, and "Saved sagas" on the title) lists all four, with Save here, Load and Delete; it asks
before saving over a kept save, before loading over a game in play, and before deleting. A save is about half a million
characters in year 1 and grows; a browser allows about five million for the whole site. When a save does not fit, the
window says so and points to Delete and to **Export** (a JSON file, loaded again from "Load a save file" on the title).

**Undo.** The top bar shows **Undo** once something has been done in the year. It takes the last act back, and pressed
again the one before it, as far as the year's opening; nothing can be taken back once the year has ended. Making a save
takes about half a second in a browser, too long to do before every click, so the client does not keep a save per act.
It keeps the year as it opened (the save the autosave makes anyway, or the one loaded) and the list of what has been done
since; to take the last act back it loads the first and does the others again, in order (`GameClient.Undo`). That is
sound because commands name things by id and take their dice from the world's own state: the same acts on the same world
come out the same, and a test holds it. It follows that undo is no way to throw the dice twice: an act taken back and
done again has the same result. It does let a player see what an act did and then not do it.

## Settings

Built 2026-11-21 from the playthroughs (docs/PLAYTHROUGH.md, "A first game": N47, N3). **Options** in the top bar and on
the title opens them. They are kept in the browser (`localStorage`, key `praetoria.settings`) and apply to every saga
played in it; the page reads them before the game starts, so the title is already at the chosen size.

| setting | what it does |
|---|---|
| Text size | Small, Normal, Large, Larger: the root size is 13.5, 15, 16.5 or 18 pixels. Every size in the stylesheet is in `rem`, and so are the widths of the rail, the windows and the outliner, so the layout grows with the words. The map does not change. |
| Enter ends the year | On by default. Off, a year ends only by the End Turn button. |
| Number keys open windows | On by default. |
| The year in brief | Every year, or only when asked (the same choice the card itself offers). |

The window also lists the keys. They cannot be changed to others; that is not built.

**Sound.** The game has no music and no sound, so the window says so and offers no switch. A dead slider would be a lie.

**A top bar too short for its content** (a narrow screen, or the larger sizes on a 1600-pixel one): the house, the stores
and the year's pools keep their room; the gaps close first, then the era's name gives way, then the year's label is cut
short. Before this the stores ran over the house's name.

The title has three more buttons for the same reason (the newcomer looked there first): **Saved sagas**, **Options**, and
**Glossary and lectures** (the handbook without the primer, which speaks to the head of a house and needs a saga).

## The map when the browser takes its graphics away

A browser may take a page's graphics context away: a laptop that sleeps, a driver that restarts, too many tabs. The map
used to stay black after that (docs/PLAYTHROUGH.md, S79). Now nothing is drawn while the context is gone; when the
browser gives it back the map is sized and drawn again; and if it is not given back within two and a half seconds the
whole map is built afresh on a new canvas, looking where it looked, with what was picked still picked
(`praetoriaSpace.rebuild` in `js/space.js`; it tries a few times, then leaves the screens to work without the map).
Both ways were tried in a headless browser by taking the context away with `WEBGL_lose_context`. The other drawn scenes
(the Office, the portraits' frame) have not been given the same care.

Found while in that file and fixed: the map's "Back to your seat" button had done nothing since the seat was first
stored under the same name as the function that goes to it.

## Tooltips

Every window keeps its detail in tooltips, so they are the game's own and not the browser's (`js/tips.js`). Anything with a
`title` gets one: a heading when the text has one, a row a line for a breakdown.

**A tooltip may hold a tooltip.** A word of the game's own (legitimacy, clout, a bench, a war score: the 74 terms of the
glossary in `content/academy/academy.json`) is underlined where it stands in a tooltip. The tooltip stays while the
pointer is on it, and resting on the word opens what it means beside it, level with the word. That one may hold words
too, four deep at most. Each word is underlined once in a tooltip, five at most, never the word the tooltip is itself
about, and not the everyday ones (year, world, fleet, the Senate) that would mark every line. A label in a window
that is exactly one of the glossary's words explains itself the same way, and so does anything given `data-gloss="id"`.

They are placed by **Floating UI** (MIT, 1.8.0, kept in `js/vendor` and not loaded from a CDN: the detail of every
window must work offline). A tooltip opens beside the pointer, or under the thing itself when it was reached by the
keyboard; it is turned over or slid along when the edge of the screen is in the way and is never taller than the room
there is. They close when the pointer leaves, on a click elsewhere, on scrolling and on any key.

`tools/browser/tooltips.js` checks it in a real browser: a tooltip, one inside it and one inside that, all wholly on the
screen, closed on leaving, turned upward at the foot of the screen.

## Words

A count is said as a person says it. `Counted` (in the core) writes "1 hull" and "4 hulls", "3 more years", and gives
the verb that agrees ("1 seat is counted", "4 seats are"). About 170 lines across the game used to write "hull(s)" and
"year(s)"; none does now, and a test reads the source to keep it so.

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
- `Components/GalaxyMap.razor` — **the Galaxy**: every system with its bodies on their orbits, regions highlighted as
  soft areas, a sparse lane web with chokepoints marked (brass diamonds); ten regions in three rings (the rings drawn as faint ellipses, the capital
  double-ringed, a world in revolt pulsing red; the region list grouped by ring with allegiance and lawlessness) as
  coloured fields with labels, planets drawn by class, stations,
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
- `Components/PlanetsScreen.razor` — **Worlds**: the orrery set: under a star-pricked dome, a brass armillary sun at the
  heart and one arm per region, each region's worlds as turning textured globes on brass stalks (stations as rings,
  restless worlds under a red light, your worlds larger with a brighter halo); click a globe for its card (class, region,
  culture, holder, governor, industry, population, loyalty, stations, the system's description, open on the galaxy map);
  panels list every region's worlds with holder and loyalty.
- `Components/HouseScreen.razor` — **House**: the keep's great hall set (`sets.js`): the house banner over the dais, your
  worlds as turning globes (stations as rings) on pedestals down the hall, a red warning light on any restless one, the
  household seated at the long table (click one for their card and foster/recall); panels for treasury and forecast,
  holdings with prosperity and its drift, invest, build / shipyard / governor's chair; the per-turn ledger (every
  world, claim, venture, charter, loan and fleet line, the net, wealth with tier and rank, the loan, the wealth table
  of the houses); ventures with expand / sell, the kinds that can be founded and where, belts to stake; decrees
  (tithe, exchange, succession), threat, honors, claims.
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
- `Components/HousesScreen.razor` — **Houses**: the hall of banners set: every house's banner on the gallery walls with a
  brass floor plaque and its head on a plate before it, the greatest nearest the crest at the far end, smaller banners for
  the landless, a red pulse under any house at war with you; click a banner or a head for the house's card (title, culture,
  bloc, legitimacy, domain, navy, spheres, its people with marriage and intrigue actions, its worlds and stations, war and
  peace) and a roster of every house with its disposition.
- `Components/ChronicleScreen.razor` — **Chronicle**: the archive set: stacks of lit spines on both walls, the chronicle
  as an illuminated timeline on the far wall (notable turns as gold marks, wars as red, the present in red), a reading
  table with one bound volume per ten turns, the open one showing its notable lines; click a volume to open it, and the
  panels show that volume's turns with an "only the notable" filter.
- `Components/CharacterScreen.razor` — **You**: the quarters set: a viewport onto the seat's world, your great portrait
  over the mantel with the family as small plates along it (click one for their card), the house banner, a trophy case
  of honors, a desk with your record of service (career, ambition, skills) under a lamp, an armchair and a decanter;
  panels for the sheet (traits), career ladder, ambition, stress, honors, marks, skills, blood and bonds, those who
  matter, your story so far.
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
  the worlds on pedestals, a candle-lit long table), the Gallery (a skylit hall of banners with floor plaques and the
  crest at its end), the Orrery (a star-pricked dome, a lamp-lit parapet, a brass armillary sun with one arm per region
  and the worlds as globes on stalks), the Archive (stacks of lit spines, gallery rails and lamps, the chronicle framed on
  the far wall, a reading table of bound volumes) and the Quarters (a viewport, a mantel of family portraits, a banner, a
  trophy case, a desk with a lit record of service, an armchair). Each set takes plain JSON from Blazor, places framed
  cameo plates for its people and calls back `OnPlate(key)` when a plate, globe, banner or volume is clicked.
- **Portraits** (`portrait.js`, canvas): cameos — imperial coin profiles drawn from a parametric bust (forehead, brow,
  nose, lips, chin, jaw, neck), hair and beard shapes, a uniform collar and brass gorget in the house colour, relief
  shading, on a plate with the house's diagonal stripe; stable per character, greying and lined with age.
- **Planet sprites** (`fx.js`, canvas): noise-shaded spheres with lighting, atmosphere, clouds, bands or rock scatter.
- **Chrome**: smoked-glass panels with brass hairlines and corner brackets; Cinzel, Cormorant, Plex Sans.

Both WebGL scenes run at a frame budget (about 15–20 fps) and stop while the tab is hidden.
