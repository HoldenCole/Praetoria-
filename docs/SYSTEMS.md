# Solar systems

The owner, 2026-11-20: "when you zoom in on the map and you see one part for example and Anvil's Rest, that is a solar
system, and there are or at least should be multiple planets, asteroids and other things per solar system. Each system
that has multiple things so basically every system should be slightly different from each other. But also, you should be
able to do things for and with every part of your solar system. So like establishing new colonies building a new resource
... building out military bases building out mega structures starting their own things there. Think of any unique
building kind of like Crusader Kings has unique buildings and Stellaris has megastructures, but for your own specific
solar system."

## What a system is

A sun and everything that goes round it. The map file writes a system's world and a sister or two; when a game begins
`SystemBodies` fills in the rest, once:

- **the sun itself**, which is a place like any other;
- **a body for every deposit** (helium-3 in a giant's clouds, rare earths in a belt or a dead world, isotopes under rock
  or ice), and a belt where the system has one;
- **moons** for the system's world and for its giants;
- something far out until four or five things go round the sun, and for some a **comet**;
- **one feature, and for some a second**, each on a body of a kind that can have it.

A system has between four and ten bodies besides its sun. What it gets follows from its id alone, so Anvil's Rest is the
same Anvil's Rest in every game, and a body's place in the list never changes. On a larger map (docs/GALAXY_SIZE.md) the
systems the map grew are furnished the same way.

## Features (`content/holdings/works.json`, `features`)

Eighteen of them: great rings, an eternal storm, a wreck field, the mother lode, ruins older than the Reach, deep vaults,
a buried sea, singing ice, a garden country, a capital of the old kings, the great reefs, the Rift, an open mantle, one
face to its world, a hollow moon, the long comet, a flare star, a companion sun. Each is found only on the kinds of body
it names, and each opens **one work that can be built nowhere else**.

## Works (`works`)

Whoever holds a system's world may build on every body in it. A body has room for one to three works: one on the sun, a
rock or a moon; two on a world or a belt; three on the system's own world or a giant. There are 38:

| kind | works |
|---|---|
| colonies | Outpost, Domed Colony (wants an outpost there first), Cloud Habitat, Settled Sister-World |
| extraction | Strip Mine, Belt Foundry, Skimmer Fleet, Rare-Earth Diggings, Isotope Pits, Ice Works. A work on a deposit brings up as much as the deposit is rich. |
| military | Listening Post, Orbital Batteries, Fleet Anchorage (supplies fleets as a depot does), Fortress Moon |
| orbital | Trade Station, Observatory |
| megastructures and wonders | Sun Foundry (round the sun), the Mausoleum of the House (one to a house), the Lighthouse of the Lanes (one in the galaxy, at a key of the lanes) |
| only where the feature is | the Ring Habitat, the Storm Tap, the Breaker's Yard, the Lode Works, the Great Dig, the Vault Archive, the Sunless Cities, the Ice Choir, the Garden Estates, the Old Capital Restored, the Reef Cities, the Rift Citadel, the Mantle Forge, the Moon-Port, the Hollow Arsenal, the Comet Watch, the Flare Harvesters, the Twin Crown |

What a work does is data: a yearly yield less upkeep (in the house's ledger, a line a work; a blockade halves what gets
out), defence for the system (a point of it is two more years to besiege), supply for fleets, where the world's prosperity
heads, unrest a year, room for more people on the world, and legitimacy and glory given once when it is finished. A work
that takes years is begun and paid for as it rises, like a building (`ProjectSystem`); the greatest take seven. Ranks gate
the great ones as they gate megastructures on a world.

**A work goes with the system.** Nothing records an owner: whoever holds the world has what was built round it, and a
house that takes the world takes the works.

## Settling

A system nobody holds can be settled (`FoundColonyCommand`): it must lie a lane from a world of the house, or have a
fleet of the house standing in it, and not lie in a nation's space. The colony costs 30 credits, 20 materials, 8
manpower and 2 influence, paid over three years on the way; when it lands the system's world is a small holding of the
house, and its bodies are the house's to build on. The larger maps leave systems empty for this.

## The other houses

About one year in three a rival house with coin to spare builds the first work it can on a body it holds: what works a
deposit first, then what only that body's feature allows, then the rest; a megastructure only when it could pay for it
twice. About one year in six a house with the coin sends a colony to an empty system beside its own. In forty years of a
medium map the houses raised eight works or more of several kinds and settled at least one system (tests).

## On the screen

- **The map** draws what goes round each sun on its own orbit and a moon beside its world. A body with something built
  on it carries a small gold light, and a ring turns round a sun that has been built on.
- **Clicking a system** opens its System tab: the sun first, then each world with its moons under it. A row is a body:
  its kind, its feature, its deposit, and its room as pips (filled when built, dashed when rising). Opening a row
  shows what suits that body, the work only it allows first, each with what it does, what it costs, and why it cannot
  be built if it cannot. A system nobody holds says what settling it takes.
- The House window's ledger has a line a work, and a world on the estate tab says how many works stand round its sun.

`node tools/browser/systems.js` drives it; `tests/SystemWorksTests.cs` holds the rules.

## Not done

See docs/GAPS.md, "Solar systems".
