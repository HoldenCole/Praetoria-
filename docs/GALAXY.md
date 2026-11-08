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
`concede_to_rebels`, `purge_rebels`, `fief_granted`, `house_fell:<name>`; counters `rebellions`, `secessions`,
`free_marches`, `fiefs_granted`, `houses_fallen`, `landless_since:<house>`. The ring, capital, free-hold, raider,
governor, fief and peace-term events ship in `content/events/rings.json`.

## The throne stands in the capital

A throne is taken where it stands (`ThroneSystem.CapitalGate`). To **seize** it, a fleet of yours of two hulls or more
must hold the capital's orbit, on top of the fleet-dominance and won-war rules. To be **proclaimed**, the capital must
be yours, or its holder must have knelt: at peace with you and its head disposed twenty or better toward yours. The
War of the Reach crowns its victor only when the field was won decisively (hulls at five to four) *and* the victor's
bloc holds the capital or the victor's fleet its orbit; otherwise the war ends in exhaustion (`great_crisis:stalemate`),
the victor is raised to Duke at least with ten legitimacy, and the Fractured Stars go on. A fortress world earns
**escort fees** from the lanes around it (`docs/ECONOMY.md`).

## Free holds as actors

A world that breaks away is a house from its first day: a leader, a treasury, a **militia** hull in orbit with the leader
in command, and a steward who trades, stakes claims and founds ventures like any other (`NpcAi`). It remembers its
old lord (`Holding.FormerOwnerId`). The way back is coin or war: **buy it back** (`ReclaimFreeholdCommand`) for
fifteen credits plus half its people plus half the leader's grudge, provided you are at peace with it, it is not in
revolt and its unrest is under seventy — the world swears again, the leader takes the governor's chair under the old
banner, and a free house left with nothing is folded away, militia and ventures with it; or **declare war** and
besiege it. Rival lords do the same: they buy back when they have the coin and fight when they have the fleet. The
player hears of it through *The Free Hold Sends Word* (`freehold_of_ours`).

## The capital as a prize

Holding the capital's world gives the **court** (two credits and an influence a turn), **the Senate's chair** (two
Senate influence by right), a point of **legitimacy** every third turn, five more prosperity on the world, and the
**throne gate** above. It also paints a target: five threat, and every ten turns a strong, legitimate, solvent rival
(hulls at three to two) may **declare for the capital** and sail on it. When the capital changes hands the chronicle
marks it (`capital_changes`), and the player's events *The Keys of the Capital* (`capital_taken`) and *The Capital
Falls* (`capital_lost`) fire. The Houses screen shows who holds it; the galaxy card double-rings it.

## Raiders: lawlessness with a face

When a region's lawlessness passes 60 and no raiders prowl it, a raider fleet gathers at its most lawless corner (an unheld
world first) under **The Lawless**, a pseudo-house at war with everyone that holds nothing (`PirateSystem`). Raiders roam a
lane a turn, never into the core, avoiding stronger fleets; where they sit over a held world with no fleet of its owner's,
they **raid** it (prosperity −3, the region's lawlessness +2, `raided` for the player). Any house fleet that meets them
fights them by the ordinary battle rules; a hunter that breaks them gains a point of legitimacy and quiets the region by
ten (`pirate_hunter`). Rival houses with two hulls or more sail on raiders within a lane of their worlds. Raiders dissolve
when the region's lawlessness falls under 30. Raiders are talked about on every lane: the map always shows them, as a red
skull. Content reads `pirates_near` (*Raiders at the Gate*, *The Pirate King's Offer*).

## Governors who scheme

A governor's loyalty is on the ledger: one who dislikes the head skims a credit; one who hates them (−40) lets the world
slide (prosperity target −10); an able one (administration 3) finds a credit the ledger did not know it had; being named
to the chair is an honour (+10 toward the one who named them). An **ambitious** governor who hates the head, on a restless
(unrest ≥ 50) or rich (prosperity ≥ 70) world, **declares it for themselves** one such turn in eight: a new house under
the governor's name, a free hold for every purpose, a militia hull, and a grievance for the old lord (`governor_revolt`).
A governor of a languishing world petitions the head (`governor_petition`). The House screen shows each governor's
loyalty and ability.

## Wars that end, and have prizes

Two rival houses whose fleets have not met in twelve turns go home in a white peace: exhaustion. Any peace with terms
makes the side behind by ten or more **cede** its least peopled world (never the capital, never its last) to the winner
(`won_a_world`, `ceded_a_world`). Rival fleets at war no longer sit at home: a strong, rested fleet sails to hold the
unguarded chokepoint between home and the enemy, or strikes the enemy's weakest unguarded world.

## The fief

A landless house of some standing (legitimacy 20) **petitions for a fief** (three influence). The grantor is the court if
it holds a world beyond the capital, else the greatest landed house at peace with the petitioner — a **patron**, never
a free hold. If the grantor's head thinks well of the petitioner (disposition 10) it grants its least peopled world;
the grantor gains five legitimacy, the petitioner a barony and a governor's chair (`fief_granted`). Rival landless
houses court the grantor first, then petition too.

## Houses that fall

A rival house that has lost everything ends (`HouseSystem`, see ECONOMY.md): struck from the rolls when no one is
left, folded into whoever took its last world if it was a free hold, or — landless, penniless and hull-less for twelve
years — entering another house's service. The counter `houses_fallen` counts them; `house_fell:<name>` is set for one
turn so the outliner and events can mark the passing. The player's house never ends this way.

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
