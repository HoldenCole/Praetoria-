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

## What is built

Whoever holds a system's world may build in it. docs/BUILDINGS.md has the whole list and the rules: **buildings** go in a
body's slots, and each kind of body has its own; a **great work** takes a body's one site, costs far more and takes
years; **infrastructure** is the system's own and is laid a level at a time; and **open space** is a place to build that
is tied to no world. What any of it does is data: a yearly yield less upkeep (in the house's ledger, a line each; a
blockade halves what gets out), defence for the system (a point of it is two more years to besiege), supply for fleets,
where the world's prosperity heads, unrest a year, room for more people on the world, and legitimacy and glory given once
when a great work is finished.

**What is built goes with the system.** Nothing records an owner: whoever holds the world has what was built round it,
and a house that takes the world takes it all.

## Settling

A system nobody holds can be settled (`FoundColonyCommand`): it must lie a lane from a world of the house, or have a
fleet of the house standing in it, and not lie in a nation's space. The colony costs 3,000 credits, 20 materials, 8
manpower and 2 influence, paid over three years on the way; when it lands the system's world is a small holding of the
house, and its bodies are the house's to build on. The larger maps leave systems empty for this.

## The other houses

About one year in three a rival house with coin to spare builds the first thing it can on a body it holds: buildings
before great works, what works a deposit first; a great work only when it could pay for most of it today. About one year in
four it lays the next level of its seat's infrastructure, and about one in six it sends a colony to an empty system beside
its own. In forty years of a medium map the houses raised eight buildings or more of several kinds, laid five levels of
infrastructure or more, and settled at least one system (tests).

## On the screen

- **The close map** (below): the map goes into a system, and from there to a single world.
- **The map** draws what goes round each sun on its own orbit and a moon beside its world. A body with something built
  on it carries a small gold light, and a ring turns round a sun that has been built on.
- **Clicking a system** opens its System tab: the sun first, then each world with its moons under it, then open space.
  A row is a place: its kind, its feature, its deposit, its building slots as pips (filled when built, dashed when
  rising) and its site for a great work as a diamond. Opening a row shows what stands there, the buildings that suit
  it, and the great works its site could take, each with its picture, what it does, what it costs, and why it cannot
  be built if it cannot. The Infrastructure tab has the system's tracks. A system nobody holds says what settling it
  takes.
- The House window's ledger has a line a work, and a world on the estate tab says how many works stand round its sun.

`node tools/browser/systems.js` drives it; `tests/SystemWorksTests.cs` holds the rules.

## The close map

The owner, 2026-11-21: "build out the closer end map so what the map would look like for just the solar system and for
the planet you control. Think about how when you click on a system in Stellaris how it shows the planets and where they
are, the sun, any asteroids, and where the hyper lanes are."

The map now has three distances: the Reach, one solar system, and one world.

**Going in and coming out.** A system is entered by clicking it twice, by "Go into the system" on its panel, or by
pushing in with the wheel past the nearest the Reach is looked at from. Inside, a breadcrumb at the top says where the
map is (The Reach ▸ the region ▸ the system ▸ the world) and each step of it is a button. Escape comes out one step; so
does pulling back with the wheel past the system's edge.

**What a system shows.** Its sun, in its own colour. Everything that goes round it on its own orbit, the warm kinds of
world nearest and the cold furthest, each drawn where it is this year with its lit side to the sun. Moons beside their
worlds. The belt as a ring of rock, thicker where the system is richer in it, with the claims staked in it. A comet on a
long thin path. A lamp on a body for each thing built there, a turning ring for a great work, a dashed one for what is
rising. The fleets that are here: the holder's lying off its world, another's standing out in the system toward the lane
that leads home, one under way at the gate it is making for. Stations off the world.

**Where the lanes leave.** Each lane has a gate at the edge of the system, in the direction its neighbour lies on the map
of the Reach, named for where it leads and coloured for who holds that. Clicking a gate follows the lane to the next
system. With a fleet picked, clicking a gate shows the way there in the order bar as before: the order is still given by
the button.

**The year moves it.** Each thing goes a part of the way round each year, the near ones faster (the innermost takes three
years to go round, and each further out takes longer), and when a year ends inside a system the worlds are seen to move to
where they now are. Where a thing is follows from the system's id, its place in the system's list and the year, so a
system is laid out the same in every game. The small drawing of each system on the map of the Reach uses the same order
and the same directions, so going in finds the worlds where they were.

**One world.** Clicking a body picks it and opens its row in the panel, where what can be built on it is. Clicking twice
goes to it: the view closes on the world and its moons, the sun is turned down, and a strip says what it is (its kind,
its year, its feature, what is in the ground, what is built and how many slots it has; for the system's own world, who
holds it, its citizens, loyalty, prosperity and governor). The system's own world has its three provinces pinned on its
face, each with its chief city, the kind of city, who holds it and how many live in the city; clicking one opens the
world's provinces in the panel.

**How it is made.** `SystemCharts.For` (src/Core/Galaxy/SystemChart.cs) lays a system out: orbits, where each body is,
the gates, the fleets and stations, what is built. It adds no state and rolls nothing. `tests/SystemChartTests.cs` holds
it: every body on its own orbit, the same chart in two different games, each world moved by its year, the gates toward
the neighbours and never one on another, and who is in the system where they lie. The map's script draws the chart
(`setSystem` in `js/space.js`); what is looked at is put in the middle of what can be seen between the panel at the left
and the list at the right.

Not done in this piece: a world is a painted disc and does not turn; a province has a pin and a card, not ground of its
own on the world's face; the fleets do not move about inside a system, and a battle is not shown being fought there;
nothing new can be done from the close map that could not be done from the panel.

## Not done

See docs/GAPS.md, "Solar systems".
