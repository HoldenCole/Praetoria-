# The galaxy — rings, regions and how they behave (GDD §19)

The Reach is ten regions of three or four systems in three rings. The rings are geography and behaviour: the engine
(`RegionSystem.cs`, with hooks in `Economy.cs`) reads a region's ring every turn and makes the core and the rim feel
different to live in. Data in `content/galaxy/the_reach.json` (`ring` on each region, `capital`, `rings` rules).

| Ring | Regions | Character |
|---|---|---|
| **Core** | The Imperial Core, The Golden Concourse | commerce and the court: a credit more per peopled world, charters pay double, prosperity +10, unrest eases a point a turn, lawlessness falls, allegiance settles high |
| **Middle** | The Drake Lanes, The Greywater Reach, The Southern Spire, The Lantern Shoals | no thumb on the scale: it leans the way the capital's standing, its worlds' loyalty and the lanes' lawlessness push it |
| **Rim** | The Western Marches, The Valmont Anvils, The Ashen Verge, The Pale Reaches | lawless and restless: prosperity −10, unrest climbs a point a turn, lawlessness grows a point a turn, allegiance settles low; two worlds (the Gallows, Sorrow) are held by no one |

## The capital

`capital` names the system the galaxy turns on (Aurelia Prime). Whoever holds its planet keeps **the court**: two
credits and an influence a turn on the ledger, and every region's allegiance reads that house's legitimacy. Content
reads `holds_capital`.

## Each region's mood

Every region carries two numbers in the save (`World.Regions`):

- **Lawlessness** (0..100): raiders on the lanes. Each turn it moves by the ring's drift, less two per hull parked in the
  region (up to four hulls), less a tenth of how far the region's prosperity sits above par, plus two per world no one
  holds, plus three per world in revolt. It taxes every ledger in the region: credits and materials from holdings and
  ventures lose up to half at 100.
- **Allegiance** (0..100): how far the region holds to the capital's order. It drifts three a turn toward
  `50 + ring offset + (capital's legitimacy − 40)/4 + (mean loyalty − 50)/4 − lawlessness/4`, twenty lower when the
  region's dominant house is at war with the capital, fifteen lower when it has already declared a free march.

## Risings and free holds

A peopled world with unrest ≥ 70, in a region with allegiance under 35, with no blockade and no garrison, **rises**.
While it is in revolt it yields nothing but its upkeep, its prosperity target drops twenty, and the region's
lawlessness climbs. The rising ends when unrest falls under 50, or when a fleet of two hulls or more of the owner's
sits in orbit (a garrison: unrest −15). Left four turns, the world **breaks away** as a free hold: a new house
(`The Free <World>`, bloc `freeholds`) under its governor if the governor was of another house, else under a leader
raised from its own people and named from the culture's lists. The old house loses five legitimacy and gains an enemy.
NPC stewards send their nearest idle fleet to a rising. The player's events offer a decree: concede (unrest −30,
prosperity −10, legitimacy −3) or purge (unrest −20, population −3, legitimacy −6, threat +3).

## Free marches

A region's dominant house, in a middle or rim region whose allegiance has fallen under 20, that is not the capital's
holder and belongs to no bloc, **declares a free march**: it joins the `freeholds` bloc, gains five legitimacy at home,
costs the capital five, and the two heads fall out. The player's house never walks out on its own: the event
*Whispers of Secession* sets `declare_free_march`, and the engine does the rest next turn.

## Flags and counters for content

`holds_capital`, `rebellion`, `region_lawless`, `secession_tempting`, `free_march`, `ring:<core|middle|rim>`,
`lost_a_world_to_rebellion`, `secession:<system>`, `free_march:<region>`; decrees `declare_free_march`,
`concede_to_rebels`, `purge_rebels`; counters `rebellions`, `secessions`, `free_marches`. Five ring events ship in
`content/events/rings.json`.

## Systems, bodies and the lane web

A system is a system, not a planet: every one carries two to five **bodies** (`bodies` in the galaxy file) — the
primary world a holding sits on, its sisters, moons, giants and belts — and the map draws them all on their orbits.

The lanes are **sparse** (forty lanes for thirty-one systems, fewer than three a system) and run through gateways:
Cinder Gate and Drydock Nine from the Greywater side, the Spire and Pallas Reach from the south, Castrum from the
Anvils, Lanternfall down to the Verge, the Veil and Saltmere out to the Marches. **Chokepoints** are computed from the
web (`Galaxy.Chokepoints()`): every articulation point plus the quarter of systems the most shortest paths run through.
Holding a chokepoint's world pays **tolls** (two credits a turn on the ledger), and while a fleet of the holder's sits
there, a house at war with the holder cannot route *through* it — its fleets must go round, or cannot go at all. The
map marks chokepoints with a brass diamond; the planet card names them.

## On the map

The galaxy screen is laid out twice as wide as before, draws every region as a soft highlighted area around its systems (the ring's tint, the dominant house's hairline), every system with its bodies on their orbits, the three rings as faint ellipses, the capital with a double ring, a world in revolt with a red
pulse, and lists the regions by ring with allegiance (⚜) and lawlessness (☠). The orrery's plaques carry the ring.
