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
