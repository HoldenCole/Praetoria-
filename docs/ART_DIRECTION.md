# Praetoria — Art Direction for the browser client

Written after looking at the reference games directly (Steam screenshots of Star Dynasties, Stellaris and Endless
Space 2, 2026-11-07) and at what the owner asked for: a space-opera grand-strategy look, not a visual novel, not a
college project. Everything in `src/Web` is held to this document.

## What the references actually do

**Star Dynasties** (the closest cousin). Portraits are painted, semi-realistic busts in uniform collars, set on a plate
with a diagonal house-colour stripe; they are the UI's main currency and appear everywhere (council rows, assignment
lists, map pins, event cards). The galaxy map is a web of thin lanes over a soft nebula; territory is drawn as bold
polygons with diagonal hatching in house colours; systems carry small icon stacks. Chrome is dark teal glass with
angled corners and an ornamental header strip. Events are illustrated with painted scenes. Its "throne room" is a
painted backdrop, not a 3D room. Reviews praise the tooltips and dislike the small fonts and the clash of UI colour
with the fiction — so: legible type, and chrome that belongs to the fiction.

**Stellaris**. The galaxy is the game: a dark field, a hyperlane web in hairlines, territory as soft translucent
fields with glowing borders, nebulae as soft coloured clouds, suns as glowing points, fleets as small glyphs.
Panels are dark green-black glass with thin bright edges and hexagonal cut-outs; the planet view pairs a painted
vista of the world with a lit 3D sphere. Everything sits on the map; windows float over it.

**Endless Space 2**. Cinematic: volumetric nebulae, strong rim light, lens flares, ships with engine trails. Beyond
our means in fidelity, right in mood: space is luminous, not black.

## Praetoria's direction

An **imperial console**: the player is a noble at a desk in a station or palace, and every screen is a lit
instrument or a lit room. Deep space is always present behind the glass. House colour is a material (banners,
plates, borders), not a label.

1. **The galaxy is rendered, not drawn.** WebGL (Three.js): a layered nebula field in shader noise, thousands of
   stars with bloom, suns as glowing cores with flares coloured by class, hyperlanes as hairlines that brighten
   near your worlds and burn red along fronts, territory as a soft translucent influence field with glowing
   borders (Stellaris) in house colour, planets as lit spheres when close, fleets as glyphs with engine glow, siege
   rings and bloc rings as light. Camera tilts slightly; pan and zoom; labels float in a CSS layer.
2. **The office is a lit 3D set.** A room with real materials and lights: dark metal floor with reflections, a tall
   viewport onto the seat's world (a shaded planet, stars, a passing hull), a holographic star table projecting the
   Reach, banners with the house sigil, emissive strips, haze and bloom. Visitors stand as framed portrait plates
   in the room; the player sits at the desk. Furnishing changes materials and props, never flat colours alone.
3. **Portraits are busts, not avatars.** Rendered 3D busts (parametric head, hair, uniform collar in house colour,
   studio lighting, slight stylisation to avoid the uncanny), on a plate with the house's diagonal stripe, greying
   and lined with age, stable per character. No cartoons, no photographs.
4. **Chrome is dark glass with light edges.** Near-black blue glass, hairline edges in brass (our identity) rather
   than teal, chamfered corners, a thin ornamental header rule, corner brackets; type: Cinzel for titles, Cormorant
   for prose, Plex Sans for readouts; large enough to read at 1080p (14–15px body).
5. **Windows float over the world.** Event windows, planet cards and house sheets are panels over the rendered
   scene, never a page of cards. Information density like Stellaris; one summary line before every detail.
6. **Nothing stock, nothing Earth.** No photographs of Earth landscapes. Vistas, when used, are rendered or painted
   in-fiction (shader skies, silhouetted cities) and belong to the planet class.

## What this rules out
Flat CSS boxes standing in for furniture; cartoon avatars; photographs; Earth skylines; a feed of cards as the main
screen; decorative elements that carry no information.

## Verification
Every pass is screenshotted through the headless browser (WebGL via SwiftShader) and judged against this document
before it is committed.


## The map, second look (2026-11-08)

Reference pass against Star Dynasties and Stellaris galaxy screens (Steam store captures):

- **Lanes are hairlines.** In both games a lane is a thin, faint, non-glowing line; the eye reads the web as texture,
  not as drawing. Fronts and the player's reach may carry colour, but thinly.
- **You see a corner, not the galaxy.** The default view holds a dozen to twenty systems around your own seat and the
  map runs off every edge; you pan to see the rest, and a minimap in a corner shows the whole with the viewport drawn
  on it. Fitting the whole galaxy on screen is a mode you choose, not the default.
- **Systems are small.** A bright dot or a small star, the name beneath, one or two small glyphs. Halos and rings mark
  selection, the capital, trouble — not every system.
- **Territory is quiet, and it is everywhere.** Star Dynasties partitions the whole map: every point belongs to the nearest
  system's territory, borders are crisp polygons in the holder's colour, fills are dark tints, a contested or special
  area gets a diagonal hatch. No bubbles around systems. The border carries the information; the fill is a hint.
- **Few things glow.** Suns, a selected system, a fleet in motion. Everything else sits back.

## The role register

The GDD's second reactive register (after the era): the same screens, chrome and emphasis by the protagonist's career
track, changing at succession. `RoleTheme` (Core/Presentation) maps the track to a register id — `military`,
`stewardship`, `law`, or `none` — and the shell carries it as `data-role` beside `data-era`. Everything else reads
that: the stylesheet's three register blocks (variables, header faces, frame treatment, rail stripe, kicker colour),
the rail order (`RoleTheme.Priority`), the desk's advisor order, the briefing kicker (DISPATCH · LEDGER · THE COURT),
the Office set's dressing (`opts.role` in office.js: signal lamp, hull and chart tube; ledgers, balance and abacus;
gavel, codex and robe stand) and the landing screen after the seat passes. The era's gold overrides the register's
variables under the Imperium, so a proclamation still transforms the whole look.

## Sets and dashboards (2026-11-09)

The owner's direction: the Office and the Galaxy map keep their lit sets; every other screen is an information
surface — a dashboard with its own graphic, dense, unique, and useful. The register still applies (era and role), the
palette and type do not change, but the three-dimensional stage is gone from those screens.

| screen | the graphic | what it answers |
|---|---|---|
| Senate | a hemicycle of every seat, coloured by house, grouped by bench, rimmed by its lean on the watched resolution | who sits where, how the vote falls, whom to whip |
| House | a signed bar chart of the ledger, resource tiles with the forecast, world cards with gauges | where the coin comes from, what each world is doing |
| Chancery | an estates board with regard bars, bands and the target tick; a law grid with the option in force | who is angry, what would warm them, what each law costs |
| Command Deck | the war plot — systems, lanes, fronts, supply, the campaign's object, fleets as reported — and navy bars | where the war is, whether the fleet can fight there |
| Houses | a sortable standings table with bars for worlds, hulls and seats | who outranks whom and by how much |
| Line | the family tree by generation with the wed beside | who follows, who is lost |
| You | a skills radar, the career ladder, bonds as centred disposition bars | what the character can do and who feels what |
| Worlds | a world board by region with loyalty and prosperity bars, riches and deposits | where the land is and what it bears |
| Chronicle | a timeline strip of every turn, brass for the notable, red for war, a star for a seat passing | the shape of the run |
| Academy | the cohort by skill, your standing against the centre line | where the cadet stands |

Shared classes: `.dash`, `.dash-head` (title and stat strip), `.dash-grid` (per-screen columns), `.dash-side`,
`.standings`, `.gauge`, `.mbar`, `.seat-card`. SVG graphics are drawn in the component from the projection; no JS set
is created for these screens, and portraits paint themselves.

## The palette (2026-11-09)

The owner's direction: the colours of parties, regions and planets must be bright enough to read and different enough
not to be confused — red, blue, yellow, green and silver first. `Presentation.Palette` holds the one palette:

- **Houses**: twenty-four bright colours ordered so consecutive picks differ in hue (red, blue, yellow, green, silver,
  orange, violet, cyan, magenta, lime, gold, teal, coral, cobalt, ivory, lilac, chartreuse, amber, aqua, hot pink,
  firebrick, dodger, canary, jade). Assigned at world creation, the protagonist's house first and the rest by id, so a
  scenario's hand-picked accents are replaced; a house born in play takes the first colour no living house wears.
  Raiders keep their dark red.
- **Regions**: twelve soft-bright tints by position in the galaxy file, mixed with the ring tint on the map; no longer
  the dominant house's colour, so a region reads as a place rather than as whoever holds most of it.
- **Parties**: the benches take the first ten house colours in the order they form (`Faction.Accent`).
- **Planet classes**: terran green over blue sea, ocean blue, arid gold, forge red, frozen white, gas tan-striped,
  belt slate, barren silver — the same colours in the SVG dots, the sprites and the Office's window planet.

## The shell, the windows and the art (2026-11-19)

The owner, on seeing portraits drawn in code and the dashboards as they stood: *"It's bad. … Needs to look 10x better.
Also, going to need to de-AI the UI because the amount of cards and side paneling is too much. Look at popular grand
strategy games to redesign the UI."* And before it: *"I want everything to look like a game you got right off Steam."*
That is the bar. This section replaces "Sets and dashboards" above where the two disagree.

### What the genre does (researched 2026-11-19)

Every game looked at — Crusader Kings III, Stellaris, Victoria 3, Hearts of Iron IV, Europa Universalis IV, Imperator,
Old World — is **map-first**: the map fills the screen, the chrome sits at its edges, and one window opens over it,
docked to a side. Inside a window nothing is a card. CK3's realm, court and council windows are tabbed lists of rows,
each with a portrait or a shield and a few figures. Stellaris shows a resource as an icon, a stock and a monthly
change; the breakdown is in the tooltip. Victoria 3's diaries put ornament "around the borders and corners" only,
warn against "too many detailed patterns", add illustration to "menus that are very text heavy", and name nested
tooltips as what lets a window stay sparse. CK3's character window is a large portrait with spouse and heir beside
it, a row of trait icons, a row of skills as icon and number, then tabs of portraits; its designer's stated problem
was "Wait, who is this guy?". Event windows are a painting, the people involved, prose, and stacked full-width
choices. Civilization VII is the warning: reviewers called its menus "drab grey slabs" once the thematic art was
gone. Star Dynasties is a second: reviewers found its simple portraits made people hard to remember.

### What made ours look machine-made, and what replaces it

| the tell | the replacement |
|---|---|
| a page of bordered, rounded cards; cards inside cards | one window; sections split by a ruled small-caps header; entries divided by a hairline |
| a grid of stat tiles under every heading | one strip of figures: a label and a number, hairlines between, the explanation in the tooltip |
| pills and chips for every word | plain words; italics for a trait; a bevelled plate only for something you can press |
| a muted subtitle under each heading | the heading alone |
| a side rail, a content column and a side panel | the map, one docked window, and an outliner that floats on the map |
| rounded corners, soft glows, glass | square corners, one metal, one drop shadow, a tiled grain |
| a centred box for an event | a painting across the top, the title over its foot, large portraits, serif prose, stacked choices |
| faces drawn by code | painted portraits (below) |

### The shell

- **The map is the screen.** It is always mounted and fills the window. Escape, or closing a window, returns to it.
- **Top bar**: your portrait in a brass frame with the house's shield on its corner (it opens your profile), the
  house and its rank, then stocks as icon, number and yearly change, the three strategic goods, the turn's pools,
  and the era and year.
- **Left**: one column of icons. Each opens **one window docked beside it**, over the map: 700px, or 1080px for a
  chamber, a table or a market. The window has a metal title bar and a close button; its body scrolls as one page.
- **Right**: the outliner, on the map, with no column behind it: reports as rows, alerts, the council, fleets,
  holdings. It hides on a narrow screen.
- **Territory is drawn by zoom, as Crusader Kings III does it** (the owner, 2026-11-20: "The map and highlighting looks
  bulky. Lets do the ck3 method that shows specific highlighting on certain levels depending on zoom"). Three
  drawings of the same cells, cross-faded by the camera's distance. **From far off, the realms**: the Reach as one
  wash of marble, each nation beyond it as one in its own colour, a state that has left as one, with the realms'
  names and nothing else. **In the middle distance, the houses**: each holder's worlds run together in its colour,
  a thin line where two holders meet, a hairline at a region's edge, and the regions' names. **Close to, the
  systems**: a faint tint and a fine line round each cell, so the stars and worlds are what is looked at. Lines
  are thin and fills are light; a system nobody holds is a pale ring.
- **Nothing on the map is written over anything else** (the owner, 2026-11-20: "overlapping words and things on the
  map"). A name that would sit on another name, or under the bar, a window, the outliner, the legend or the minimap,
  is not shown; a region's name looks for room near its centre, clear of the stars if it can, and is left out if
  there is none. From far off the regions' names are placed first; close to, the systems', yours before others'.
  The legend is one line until the pointer is on it.
- **Picking a world or a fleet** on the map opens the map's own window in the same dock; the regions list is a
  button among the map tools.
- **Bottom right**: the minimap and the end of the turn.
- A portrait anywhere opens that person's **profile**.

### Portraits

Portraits are painted, not drawn by code. `tools/art/portraits.py` runs an openly licensed diffusion model
(Lykon/dreamshaper-8-lcm, of the Stable Diffusion 1.5 family; CreativeML OpenRAIL-M, whose output may be used
commercially) and writes `src/Web/wwwroot/img/portraits/` with a manifest. It needs no account and no key, and runs
on a CPU at about half a minute a picture.

- A person is one **identity** — a face, a colouring, a way of dressing — painted at **five ages** from the same
  seed: child, youth, adult, older, elder. The game gives each character an identity by a hash of their id, among
  those of their sex (and of the Reach, or of a nation beyond it), and shows the picture for their age, so a face
  grows old with its owner. Old World ships four portraits a character switched by age; this is that.
- **A house has an ancestry, and its people are painted from it** (the owner, 2026-11-20: "certain houses will
  have certain genetics ... the portraits match with what you would imagine the house to look like. Wouldn't make
  sense to have a mix of different kinds of ethnicities in a house. Unless they were brought into the house").
  `content/ancestries/ancestries.json` lists them (Mediterranean, Northern, Levantine, African, East Asian, South
  Asian, and the Steppe beyond the Reach), names which house and which nation is which, and gives the share of the
  unnamed houses that falls to each. Every painted face is tagged with one. `Lineage` decides who is what: someone
  with a parent on record takes after the father or the mother, by the dice of their own id; someone with none
  takes the ancestry of the house they were born to, so **whoever marries in keeps their own face** and the
  children of a mixed marriage take after one side or the other. Nothing is stored but a house's own choice.
- **The library** is 80 faces and 392 pictures (8 MB): six men and six women of each ancestry of the Reach, four
  and four of the Steppe, each at every age (the Steppe has no children painted). A face whose first throw came
  out wrong (a gilt picture frame painted into the picture is the usual fault) is thrown again from another seed,
  and the list of those is in `tools/art/portraits.py`.
- **Ageing.** A face is painted five times from one seed, as a child, in youth, grown, in middle age and in old
  age, and the game shows the one for the person's age (under 13, under 20, under 46, under 62, and after). The
  person stays recognisable; they are not swapped for someone else.
- **Your own look.** In the workshop's Face tab you choose the ancestry of your house, then a face among those
  painted for it and your sex, and you are shown that face at all five ages before you begin. A face is a choice
  among painted ones, not a set of sliders: each further thing to choose (hair, a beard, a scar) would multiply the
  library, or wants a model that paints on request.
- Small portraits (72px and under) are the same picture drawn in to the face.
- The house's colour is the frame. Rank, office and mood are not in the picture. The profile says what someone is.
- Where the library has no picture, the old canvas painter (`js/portrait.js`) still draws one. It is the fallback
  only, and should go when the library covers everyone.
- The pictures are **AI-generated**. Steam's content survey asks a store page to say so, and purely AI-made images
  are not copyrightable in the United States; the code, the writing and the selection are.

What would be better, and what it costs (researched the same day): a hosted image model with reference-image
editing (OpenAI's image models, Google's, or FLUX) holds one face across five ages far better than a shared seed
does, can dress the same person for each career, and paints at a higher finish. A library of about 100 identities at
five ages with career outfits is roughly 2,000 to 2,800 calls, **$40 to $140**; with event paintings and
experiment, **$100 to $300** in all. It needs an API key from the owner. Commissioned portraits run $35 to $180 each
before commercial rights, so the same library by hand is $17,000 and up. For UI art: game-icons.net (CC BY 3.0,
about 4,000 SVG icons, needs a credits screen), Kenney's CC0 frame kits, ambientCG and Poly Haven textures (CC0).

### The event window

A painting across the top (`tools/art/scenes.py` paints sixteen occasions: an audience, the Senate, a ship's bridge,
a battle, the Exchange, a family hall, a funeral, a court, unrest, a hall beyond the rim, a dispatch, a feast, an
intrigue, a world, a shipyard, a triumph), chosen by what the event is about (`SceneArt.For`). The title sits over
the foot of the painting in capitals. The people involved are shown at bust size in brass frames. The prose is
serif. The choices are full-width plates; setting it aside is a plain line beneath.

### Buildings (2026-11-20)

The owner, of the building list: "let us get some artwork that matches the buildings to make sense." Every building, great
work, track of infrastructure and building of a system's own world has a small wide painting (`tools/art/works.py`, 92 of
them, about 2 MB), made the way the event paintings are and keyed by the thing's id in content. The system panel shows it
beside the thing: at a glance a smelter row is furnaces and a tidal mill is a sea wall.

- A thing in space is told not to be a sea. The model paints a shore, a sunset and a sailing ship for anything called
  a harbour, a dock or a fleet, so the prompt says spacecraft and starfield and the negative prompt names water. A third
  of the first pass was thrown again for this.
- A picture is one per kind of thing, not per place: the same Outpost on a moon and on a comet (docs/GAPS.md, U5).
- A test fails if anything that can be built has no picture, so a new building needs its line in the art script.

### A mod worth learning from: BattleTech, Heirs of the Star League (looked at 2026-11-20)

The owner pointed at this Crusader Kings III conversion (Steam Workshop 3822860401; one author, a month old, about
2,300 subscribers) for ideas. Only its Workshop page, its 104 comments and its twelve pictures could be read; nothing
of its in-game windows is published, so what it teaches is the map and the presentation.

- **Territory as stained glass.** Each star system is a cell with a bright star at its centre, filled in its owner's
  colour, with thin dark gaps between cells, so a great house reads as one block at any zoom. Frontier systems are
  lone haloed discs that thin out into bare starfield. Ours draws a region's border as a bright line and leaves
  the inside empty; a filled cell under each owned system would read better.
- **The map staged as an object**: a tilted table with a pale glowing frame, a title cartouche, a compass rose, and
  painted nebula at its edge.
- **One frame language, not a re-skin of every widget**: a dark panel with chamfered corners, corner brackets, a
  single accent colour, one wear motif; the base game's serif type kept inside it.
- **A house's hall as the home screen**: a painted hall with the house's banner, the head, spouse and heir standing
  in it, and the house's crest on Continue.
- **Scenario cards**: a year in large numerals and two or three named crises, each a sentence with a person in it.
- **Its failures are lessons too.** Its star systems are islands with no routes drawn, and its players' loudest
  complaint is war declared "half way across the galaxy": distance has to govern politics, and ours has lanes to do
  it with. Its characters still wear medieval clothes, which players say breaks the setting: costume carries a
  setting, and ours shows no rank or office in a portrait yet (V5). And a system of its own (the Clans' yearly
  children) went unexplained and was reported as a bug: every bespoke system wants an event that introduces it.
- **Mechanics it suggests**: a named capital ship as an heirloom of the house that can be lent, lost or captured;
  captured hulls sold through the Exchange; capital ships that need a shipyard world, so the yard matters more
  than the fleet; wards offered to the head by an academy or a client house; a house in exile fighting on as a
  hired fleet.

These are rows V12 to V17 and N27 to N30 in docs/GAPS.md.

### The windows, one by one (begun 2026-11-20)

The owner: "redesign the windows one by one next." The rule for each: its subject on top, the first screen whole
without scrolling, everything else behind tabs, rows and not cards. Every window's tabs are the same tabs.

| window | state | what it is now |
|---|---|---|
| You | done | Laid out as CK3 lays out a character: a large portrait; beside it the name, the house and ancestry, the station, and the people closest (wife or husband, heir, children) as small portraits; a row of traits, each with its meaning in the tooltip; the best skills as large figures; a strip of rank, health, renown and strain; then tabs: A life, Family, Relations, Learning, Record. |
| Dynasty | done | The line's headline and figures, then tabs: The tree (with whoever is picked on it and the succession), Marriages, Ambitions, Upbringing, The dead. |
| House | done | Five tabs: The estate (the worlds alone), The ledger (where the coin comes from, the stores, the wealth of the houses), Ventures, The household, Purses and the will. |
| Houses | done | The standings as one full-width table; a row opens that house's own page, with a way back. The side-by-side detail that overlapped the table is gone. |
| Military | done | A normal-width window, so the map it is about stays in view. Tabs: Your fleets (with the wars and the campaign), What is near, The navies, The general and the Senate, Beyond the Reach. |
| Laws | done | Tabs: The estates, The laws in your gift, The law of the Reach. |
| Worlds | done | Tabs: The worlds (a region chosen from a list, not a wall of buttons), The regions. |
| Senate | done | A normal-width window. The chamber itself leads (the seats by party, then what is before it); moving a motion and the consulship are tabs of their own beside offices and parties, your duties, the ladder and courts, and the orders. |
| Exchange | done | The goods table is one row a good: what moves a price is in the row's tooltip, and written under the row only when the good is dear, cheap, short or moving. Favours for coin is one line at the foot, not a panel. |
| Chronicle | done | Tabs for whose record to read: all that was written, of your house, of the Reach. A year is one list, your house's lines first and marked; the span of years is chosen from a list or by clicking the timeline. |
| Office | not yet | Still a room modelled in code (V9): the owner chooses between a painting and dropping it. |

An empty section folds to one line and loses its heading (Military at peace says so in a sentence, not under "Wars"
and "The campaign"). `tools/browser/windows.js` walks every window and every tab and prints which first screens fit,
which scroll and by how much, and any section that is a heading over nothing.

### Not done yet

See docs/GAPS.md, "The look". The windows were converted by rule (what was a card is now an entry), not redesigned
one by one: several still read as long pages and want tabs. Tooltips are the game's own and may hold one another (docs/WEB_CLIENT.md, "Tooltips"). The Academy and the
character creation pages have not been through the pass. Traits, skills and offices are still words, not icons.

### The rule for pictures of things (the owner, 2026-11-20)

"It is better to have artwork that matches and getting that right instead of crappy artwork or something that doesn't make
sense. It's even better to have no artwork instead of artwork that doesn't match because that is less immersion breaking.
But we can't have artwork for some buildings and not others."

So, for every set of pictures (buildings, great works, infrastructure):

1. **A picture shows its subject.** Somebody who has not read the name should be able to say what the thing is: a
   hospital has beds, a breakers' yard has a ship being cut up, a sensor array has dishes.
2. **No picture is better than a wrong one.** A picture that shows something else is taken out, not left in.
3. **All or none.** A set ships with a picture for every member or with none. A test holds the first half
   (`EverythingThatCanBeBuilt_HasItsPicture`); the second is a decision made when the set is audited.
4. **The audit is by eye, on a contact sheet of the whole set**, every time pictures are added. A subject the model will
   not paint is given a different composition (a ward, not a hospital seen from the street; a hangar, not a wreck in
   orbit) before it is given up on.

