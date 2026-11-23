# Buildings, great works and infrastructure

The owner, 2026-11-20: "separate out the mega structures from the ordinary buildings. We should think of ordinary buildings
kind of in the same way that Star Dynasties has different structures you can build, and how other Paradox games work with
having building slots. And then the mega structures will be unique things that cost more money and have to be built over a
long period of time. Every planetary body within a solar system should have a certain amount of building slots and each
type of planetary body should have unique types of buildings as well. And then there should be able to be things that you
just build out in space that do not have to be connected to a planetary body or things such as infrastructure that you can
just invest in over time. Let's work on making a list of these kinds of things and then work on the way that building
slots work in the game. After we have that done, let us get some artwork that matches the buildings."

And on the slots, the same day: "there should be three things that determine how many slots you have so the main limiter
will be the user interface. The big one and main one is development so the higher development the more slots [and] the more
you can upgrade your building. And then another one is your rank ... And then, if you have more money, you can potentially
clear obstacles or just pay to unlock more slots. I think the home world of your system should have unique buildings and
should also be able to build ordinary buildings ... the whole world where you actually live should have more options of
things to do and more decisions ... everyone unless they are much higher up can only do one great work at a time per house.
But if you are the leader of a region, you can do two, and if you are higher up like a king or president, you can do three.
Finally, an emperor would be able to do five at a time. And then you can pay extra to speed up the development. This would
be a continuous cost over the development life cycle."

This is the list, and the rules for slots. The tables are written from the content (`content/holdings/works.json`,
`buildings.json`, `holdings_ext.json`); the figures there are the ones the game uses. docs/SYSTEMS.md says what a solar
system is.

## Three kinds of thing

| | where it goes | how many | cost and time |
|---|---|---|---|
| **A building** | a **slot** on a place, or in open space | as many as the place has slots; each can be taken to level III | 14 to 42 in credits and materials; at once, or up to four years |
| **A great work** (megastructure) | the place's one **site** | one to a place; a house raises one, two, three or five at a time, by its standing | 64 to 240; 4 to 12 years, paid as it rises, or half the years if hastened; gated by rank |
| **Infrastructure** | the system itself, no body | five tracks, four or five levels each | level N costs N times the first; the higher levels take two and three years |

## How slots work

A slot holds one building. A great work takes no slot. **Three things give a place its slots**, and the sum is capped at
what the screen has room for: **10 on a home world, 5 anywhere else**.

| | on the home world | on every other place | |
|---|---|---|---|
| **What the place starts with** | its specialization's (1 to 4) | by its kind (table below) | |
| **1. Development** (the main one) | +1 at development 2, 4, 6, 8, 10: up to +5 | +1 at 3, 6, 9: up to +3 | the system's, 0 to 10 |
| **2. Rank** of the house that holds it | +1 at baron, duke and emperor (title ranks 2, 4, 6): up to +3 | +1 at count and archduke (ranks 3, 5): up to +2 | goes with whoever holds the world |
| **3. Money**: ground cleared | up to +3 | up to +2 | the Nth costs N × 1,400 cr, 10 mat, paid at once |
| The whole system is one house's | +1 | | only while its holder has every province of its world and no other house has a holding in the system (docs/PROVINCES.md) |

| place | starts with | site for a great work |
|---|---|---|
| The sun | 0, and never any | yes |
| The home world | its specialization's | yes |
| A living or ocean sister-world | 3 | yes |
| An arid, forge, frozen or dead world | 2 (a small one 1) | yes |
| A gas giant | 2 | yes |
| An asteroid belt | 2 | yes |
| A moon | 1 | yes |
| A comet | 1 | yes |
| Open space | 2 | yes |

On the row of a place the slots are pips: gold for a building that stands, dashed for one rising, hollow for a free slot,
and faint dotted for the ones it could still gain. Opened, the place says where each of its slots comes from.

### Development

Every held system has a **development** from 0 to 100; its **level** is a tenth of it, 0 to 10.

- A system starts at 6 and a point for every 2 of its people (a new colony at 6), never more than it can bear.
- **What it can bear** is 30, and 4 for every level of infrastructure laid in it, a point for every 3 of
  its people, and 10 on a prosperous world (prosperity 65 or more).
- It **grows a point a year** toward that, two on a prosperous world. **Laying a level of infrastructure adds 3 at once.**
- Under **siege or revolt it falls 2 a year**; above what it can bear it falls one.
- Slots that development gave are not taken away from what stands: a place can be **over-full**. Then nothing new is
  built there, and every building that stands without a slot is a point of unrest a year on the system's world, until
  there is room again.

So the way to more slots is the old way to a richer world: infrastructure, people, prosperity, peace.

### Levels

A building stands at **level I, II or III**. At level N it yields N times over and costs N times to keep (and defends,
and makes room for people, N times over). The next level **costs the building's cost times the level it leaves** (II costs
the price again, III twice it) and takes as long as the building did, at least a year. How high a building may go is the
system's development: **II from development 3, III from 6**. A level is slot-cheap and coin-dear: three
levels in one slot cost four buildings and do the work of three. Great works have no levels.

### The home world

The system's own world, where its people live and its holder sits, has **more to build and more to decide** than any
other place:

- its **own buildings**, the ones its specialization allows (the last table here), as before;
- **eight buildings only a home world takes** (below): a palace, a forum, an academy, a temple, a barracks, a spaceport,
  a hospital, a mint;
- the **ordinary buildings of its kind** of world (a forge home world takes the smelters a forge sister-world takes),
  all but the ones that make no sense on a settled world (an outpost, a domed colony);
- and its site for a great work, among them the three only a home world can have (the Orbital Ring, the Great Stair, the Aegis).

All of them share **one row of slots**, the largest in the system. Which to spend them on is the decision.

| building | what it does | cost | years | note |
|---|---|---|---|---|
| Governor's Palace | +1 inf · upkeep 100 cr · unrest −1/yr · legitimacy +2 once | 2,400 cr, 14 mat, 1 inf | 2 | one to a system |
| Forum | +200 cr, 1 inf · upkeep 100 cr · prosperity +3 | 3,500 cr, 24 mat | 2 | one to a system |
| Academy | +2 inf · upkeep 200 cr · prosperity +2 | 4,900 cr, 25 mat, 3 inf | 3 | one to a system |
| Great Temple | upkeep 100 cr · unrest −2/yr · legitimacy +1 once | 2,200 cr, 16 mat | 3 | one to a system |
| Barracks Quarter | +2 men · upkeep 100 cr · defence +0.25 | 1,600 cr, 14 mat, 2 men | 2 | one to a system |
| Spaceport | +300 cr · upkeep 1 mat · room +6 · prosperity +2 | 2,000 cr, 16 mat | 2 | one to a system |
| Hospital Quarter | upkeep 100 cr · room +10 · unrest −1/yr | 1,600 cr, 10 mat | 2 | one to a system |
| Mint | +500 cr · upkeep 1 mat | 2,600 cr, 16 mat, 2 inf | 3 | one to a house; count or higher |

**Part of a world, a whole world, a whole system.** A world has three provinces (docs/PROVINCES.md): its seat, which
is its holder's, and two held by other houses. A house with a province has what the province pays and nothing else of
the world. The house that holds the world has its row of slots and every other place in the system. A house that holds
the world, all three provinces and every holding in the system has the slot more that the table above gives.

### The rest

- **Each kind of body has buildings of its own.** A forge world has smelters, an ocean has tidal mills, a belt has a
  prospectors' exchange; a few (an outpost, a strip mine, a listening post) suit several kinds.
- **One site, one great work.** Where a body has a feature, the work only that feature allows competes for the site
  with the great works any such body could take: the ringed giant can have the Ring Habitat or the Sky Harbour, not both.
- A building or a great work **goes with the system**: whoever holds its world has what was built round it.

## Buildings, by where they go

### A living sister-world

| building | what it does | cost | years | note |
|---|---|---|---|---|
| Settled Sister-World | +300 cr, 3 men · upkeep 100 cr · room +16 · prosperity +3 | 4,200 cr, 23 mat, 13 men | 4 |  |
| Terrace Farms | +2 men, 200 cr · upkeep 100 cr · room +6 | 2,500 cr, 17 mat, 7 men | 1 | only here |
| Game Parks | +1 inf · upkeep 100 cr · prosperity +3 | 2,600 cr, 10 mat, 2 inf | 2 | only here |
| Orbital Batteries | upkeep 100 cr, 1 mat · defence +0.5 | 1,800 cr, 14 mat | 2 |  |

### An ocean sister-world

| building | what it does | cost | years | note |
|---|---|---|---|---|
| Settled Sister-World | +300 cr, 3 men · upkeep 100 cr · room +16 · prosperity +3 | 2,600 cr, 14 mat, 8 men | 4 |  |
| Sea Farms | +2 men, 200 cr · upkeep 100 cr · room +6 | 2,500 cr, 17 mat, 7 men | 1 | only here |
| Tidal Mills | +2 mat, 200 cr · upkeep 100 cr | 1,800 cr, 15 mat | 2 | only here |
| Deep Harbour | +300 cr · upkeep 1 mat · prosperity +2 | 1,800 cr, 12 mat | 2 | only here |
| Orbital Batteries | upkeep 100 cr, 1 mat · defence +0.5 | 1,800 cr, 14 mat | 2 |  |

### An arid world

| building | what it does | cost | years | note |
|---|---|---|---|---|
| Outpost | +1 mat, 1 men · upkeep 100 cr · room +4 | 1,000 cr, 8 mat, 3 men | 1 |  |
| Domed Colony | +300 cr, 2 men · upkeep 1 mat · room +12 · prosperity +2 | 2,700 cr, 19 mat, 8 men | 3 | needs an outpost there |
| Sun Fields | +300 cr · upkeep 1 mat | 1,400 cr, 10 mat | 1 | only here |
| Caravanserai | +200 cr · upkeep 100 cr · prosperity +3 | 1,400 cr, 8 mat | 1 | only here |
| Strip Mine | +3 mat · upkeep 100 cr | 1,500 cr, 10 mat, 3 men | 1 |  |
| Rare-Earth Diggings | +1 rare earths ×richness · upkeep 100 cr | 2,500 cr, 16 mat, 4 men | 1 | needs a rare earths deposit |
| Orbital Batteries | upkeep 100 cr, 1 mat · defence +0.5 | 1,800 cr, 14 mat | 2 |  |

### A forge world

| building | what it does | cost | years | note |
|---|---|---|---|---|
| Outpost | +1 mat, 1 men · upkeep 100 cr · room +4 | 800 cr, 6 mat, 2 men | 1 |  |
| Strip Mine | +3 mat · upkeep 100 cr | 1,200 cr, 8 mat, 2 men | 1 |  |
| Smelter Row | +4 mat · upkeep 100 cr | 2,300 cr, 15 mat, 3 men | 2 | only here |
| Geothermal Tap | +300 cr · upkeep 1 mat | 1,400 cr, 12 mat | 2 | only here |
| Orbital Batteries | upkeep 100 cr, 1 mat · defence +0.5 | 1,800 cr, 14 mat | 2 |  |

### A frozen world or ice dwarf

| building | what it does | cost | years | note |
|---|---|---|---|---|
| Outpost | +1 mat, 1 men · upkeep 100 cr · room +4 | 800 cr, 6 mat, 2 men | 1 |  |
| Domed Colony | +300 cr, 2 men · upkeep 1 mat · room +12 · prosperity +2 | 2,200 cr, 16 mat, 6 men | 3 | needs an outpost there |
| Ice Works | +200 cr, 1 mat · upkeep 100 cr | 1,400 cr, 8 mat | 1 |  |
| Cold Granaries | upkeep 100 cr · room +4 · unrest -1/yr | 1,400 cr, 10 mat | 1 | only here |
| Penal Colony | +2 mat, 1 men · upkeep 100 cr · prosperity -1 · unrest -1/yr | 1,900 cr, 15 mat | 1 |  |
| Isotope Pits | +1 isotopes ×richness · upkeep 200 cr | 3,100 cr, 19 mat, 5 men | 2 | needs a isotopes deposit; rank 1 |
| Hermitage | +1 inf · upkeep 100 cr · unrest -1/yr | 2,000 cr, 12 mat, 2 inf | 1 | one to a system |
| Observatory | +1 inf · upkeep 100 cr | 2,200 cr, 13 mat, 2 exo | 2 | one to a system |
| Listening Post | upkeep 100 cr · defence +0.25 | 1,000 cr, 6 mat | 1 | one to a system |
| Orbital Batteries | upkeep 100 cr, 1 mat · defence +0.5 | 1,800 cr, 14 mat | 2 |  |

### A dead world or rock

| building | what it does | cost | years | note |
|---|---|---|---|---|
| Outpost | +1 mat, 1 men · upkeep 100 cr · room +4 | 800 cr, 6 mat, 2 men | 1 |  |
| Domed Colony | +300 cr, 2 men · upkeep 1 mat · room +12 · prosperity +2 | 2,200 cr, 16 mat, 6 men | 3 | needs an outpost there |
| Strip Mine | +3 mat · upkeep 100 cr | 1,200 cr, 8 mat, 2 men | 1 |  |
| Penal Colony | +2 mat, 1 men · upkeep 100 cr · prosperity -1 · unrest -1/yr | 1,000 cr, 8 mat | 1 |  |
| Proving Ground | +2 men · upkeep 100 cr · defence +0.25 | 1,600 cr, 12 mat, 3 men | 1 | only here |
| Rare-Earth Diggings | +1 rare earths ×richness · upkeep 100 cr | 1,600 cr, 10 mat, 2 men | 1 | needs a rare earths deposit |
| Isotope Pits | +1 isotopes ×richness · upkeep 200 cr | 2,000 cr, 12 mat, 3 men | 2 | needs a isotopes deposit; rank 1 |
| Observatory | +1 inf · upkeep 100 cr | 1,400 cr, 8 mat, 1 exo | 2 | one to a system |
| Listening Post | upkeep 100 cr · defence +0.25 | 1,000 cr, 6 mat | 1 | one to a system |
| Orbital Batteries | upkeep 100 cr, 1 mat · defence +0.5 | 1,800 cr, 14 mat | 2 |  |

### A gas giant

| building | what it does | cost | years | note |
|---|---|---|---|---|
| Skimmer Fleet | +1 He-3 ×richness · upkeep 100 cr | 1,700 cr, 12 mat | 1 | needs a helium3 deposit; only here |
| Cloud Habitat | +400 cr, 1 men · upkeep 100 cr · room +8 | 2,400 cr, 18 mat, 4 men, 1 exo | 3 | only here |
| Aerostat Refinery | +2 mat, 200 cr · upkeep 100 cr | 1,800 cr, 16 mat | 2 | only here |
| Orbital Batteries | upkeep 100 cr, 1 mat · defence +0.5 | 1,800 cr, 14 mat | 2 |  |
| Fleet Anchorage | upkeep 200 cr · defence +0.25 · depot | 2,200 cr, 18 mat | 2 | one to a system |

### An asteroid belt

| building | what it does | cost | years | note |
|---|---|---|---|---|
| Belt Foundry | +4 mat · upkeep 100 cr | 2,300 cr, 15 mat, 3 men | 2 | only here |
| Prospectors' Exchange | +300 cr · upkeep 100 cr | 1,600 cr, 9 mat | 1 | only here |
| Hidden Dock | +400 cr · unrest +1/yr | 2,900 cr, 20 mat | 1 | only here |
| Rare-Earth Diggings | +1 rare earths ×richness · upkeep 100 cr | 1,600 cr, 10 mat, 2 men | 1 | needs a rare earths deposit |
| Fleet Anchorage | upkeep 200 cr · defence +0.25 · depot | 2,200 cr, 18 mat | 2 | one to a system |

### A moon

| building | what it does | cost | years | note |
|---|---|---|---|---|
| Outpost | +1 mat, 1 men · upkeep 100 cr · room +4 | 800 cr, 6 mat, 2 men | 1 |  |
| Domed Colony | +300 cr, 2 men · upkeep 1 mat · room +12 · prosperity +2 | 2,200 cr, 16 mat, 6 men | 3 | needs an outpost there |
| Strip Mine | +3 mat · upkeep 100 cr | 1,200 cr, 8 mat, 2 men | 1 |  |
| Rare-Earth Diggings | +1 rare earths ×richness · upkeep 100 cr | 1,600 cr, 10 mat, 2 men | 1 | needs a rare earths deposit |
| Isotope Pits | +1 isotopes ×richness · upkeep 200 cr | 2,000 cr, 12 mat, 3 men | 2 | needs a isotopes deposit; rank 1 |
| Mass Driver | +3 mat · upkeep 100 cr | 1,700 cr, 13 mat | 2 | only here |
| Moon Fort | upkeep 100 cr · defence +0.5 | 1,800 cr, 16 mat, 2 men | 2 | only here |
| Hermitage | +1 inf · upkeep 100 cr · unrest -1/yr | 1,000 cr, 6 mat, 1 inf | 1 | one to a system |
| Observatory | +1 inf · upkeep 100 cr | 1,400 cr, 8 mat, 1 exo | 2 | one to a system |
| Listening Post | upkeep 100 cr · defence +0.25 | 1,000 cr, 6 mat | 1 | one to a system |
| Fleet Anchorage | upkeep 200 cr · defence +0.25 · depot | 2,200 cr, 18 mat | 2 | one to a system |

### A comet

| building | what it does | cost | years | note |
|---|---|---|---|---|
| Outpost | +1 mat, 1 men · upkeep 100 cr · room +4 | 800 cr, 6 mat, 2 men | 1 |  |
| Ice Works | +200 cr, 1 mat · upkeep 100 cr | 1,000 cr, 6 mat | 1 |  |
| Hermitage | +1 inf · upkeep 100 cr · unrest -1/yr | 1,000 cr, 6 mat, 1 inf | 1 | one to a system |
| Observatory | +1 inf · upkeep 100 cr | 1,400 cr, 8 mat, 1 exo | 2 | one to a system |
| Listening Post | upkeep 100 cr · defence +0.25 | 1,000 cr, 6 mat | 1 | one to a system |

### Open space

| building | what it does | cost | years | note |
|---|---|---|---|---|
| Listening Post | upkeep 100 cr · defence +0.25 | 1,000 cr, 6 mat | 1 | one to a system |
| Fleet Anchorage | upkeep 200 cr · defence +0.25 · depot | 2,200 cr, 18 mat | 2 | one to a system |
| Waystation | +200 cr · upkeep 1 mat · prosperity +3 | 1,600 cr, 12 mat | 2 | only here |
| Customs Cordon | +400 cr · upkeep 100 cr · prosperity -1 | 2,100 cr, 15 mat | 1 | only here |
| Relay Beacon | +1 inf · upkeep 100 cr | 2,000 cr, 13 mat, 2 exo | 1 | only here |
| Patrol Base | upkeep 100 cr · defence +0.5 · unrest -1/yr | 1,600 cr, 14 mat, 2 men | 2 | only here |
| Minefields | upkeep 1 mat · defence +0.75 · prosperity -2 | 1,200 cr, 16 mat | 1 | only here |

## Great works

**How many at once.** A house raises as many great works at a time as its standing allows:

| the house is | at a time |
|---|---|
| a house like any other | 1 |
| the leader of a region: the house that dominates one, or a duke or archduke | 2 |
| a head of state: a consul of the Republic; the king, president or dictator of a state that has left it; the ruling house of a nation | 3 |
| the emperor | 5 |

**Hastening.** A great work is not bought outright (the rush that an ordinary project allows is refused it). It can be
**hastened**: a hastened work does two years' work in one, and every year it is hastened pays both years' shares and
50 in the hundred more, which buys nothing but the speed. It can be stopped and begun again any year. A year the
purse cannot stand it, the work goes at its ordinary pace that year and is not stalled. Hastened from the first year, a
work of twelve years stands in six and costs about half as much again.

One to a body, on its site. All are years in the raising and wanted for the name they give as much as for what they yield.

### Any system may raise

| great work | where | what it does | cost | years | note |
|---|---|---|---|---|---|
| The Orbital Ring | the system's own world | +800 cr, 3 mat · upkeep 200 cr · prosperity +4 · glory +6 | 6,000 cr, 110 mat, 4 exo | 8 | rank 3 |
| The Great Stair | the system's own world | +600 cr · upkeep 200 cr · room +10 · prosperity +6 · glory +4 | 5,000 cr, 90 mat, 3 exo | 7 | rank 2 |
| The Aegis | the system's own world | upkeep 300 cr, 1 mat · defence +2 · glory +6 | 7,000 cr, 120 mat, 6 exo | 9 | rank 3 |
| The Spire City | living world, ocean world, arid world | +600 cr, 3 men · upkeep 200 cr · room +30 · prosperity +3 · glory +5 | 7,000 cr, 100 mat, 10 men | 8 | rank 2 |
| The Dyson Lattice | the sun | +1,600 cr · upkeep 2 mat · glory +12 | 8,000 cr, 160 mat, 8 exo | 12 | rank 4 |
| The Sun Foundry | the sun | +600 cr, 8 mat · upkeep 2 mat · glory +6 | 5,000 cr, 110 mat, 4 exo | 8 | rank 3 |
| The Sky Harbour | gas giant | +700 cr, 1 He-3 · upkeep 200 cr · room +10 · glory +4 | 5,000 cr, 90 mat, 3 exo | 7 | rank 2 |
| The Hollowed World | belt | +5 mat, 2 men · upkeep 200 cr · defence +0.5 · room +12 · glory +4 | 5,000 cr, 80 mat, 6 men | 7 | rank 2 |
| The Fortress Moon | moon | upkeep 200 cr, 1 mat · defence +1.5 · glory +4 | 5,000 cr, 80 mat, 6 men | 6 | rank 2 |
| The Mausoleum of the House | moon, dead world, frozen world | +1 inf · upkeep 100 cr · legitimacy +5 once · glory +6 | 5,000 cr, 50 mat, 4 inf | 6 | one to a house; rank 1 |
| The Lane Gate Anchor | open space | +4 inf · upkeep 300 cr · defence +0.5 · glory +8 | 9,500 cr, 174 mat, 10 exo | 9 | rank 4 |
| The Grand Arsenal | open space | upkeep 300 cr, 1 mat · defence +1 · depot · glory +6 | 6,000 cr, 120 mat, 6 men | 9 | one to a house; rank 3 |
| The Long Cylinder | open space | +500 cr, 3 men · upkeep 200 cr · room +24 · glory +5 | 6,000 cr, 110 mat, 8 men | 8 | rank 2 |
| The Lighthouse of the Lanes | open space | +600 cr, 4 inf · upkeep 300 cr · legitimacy +6 once · glory +12 | 14,400 cr, 179 mat, 11 exo, 9 inf | 10 | one in the galaxy; at a key of the lanes; rank 3 |

### Only where the feature is

| great work | the feature | on | what it does | cost | years |
|---|---|---|---|---|---|
| The Ring Habitat | Great rings | gas giant | +800 cr, 4 men · upkeep 200 cr · room +20 · prosperity +3 · glory +5 | 7,200 cr, 116 mat, 6 exo | 7 |
| The Storm Tap | An eternal storm | gas giant | +4 He-3, 2 exo · upkeep 200 cr · glory +4 | 7,900 cr, 126 mat, 5 exo | 6 |
| The Breaker's Yard | A wreck field | belt | +8 mat, 400 cr · upkeep 100 cr · glory +4 | 4,900 cr, 61 mat, 5 men | 5 |
| The Lode Works | The mother lode | belt | +7 mat, 2 rare earths, 2 exo · upkeep 200 cr · glory +4 | 11,000 cr, 139 mat, 10 men | 5 |
| The Great Dig | Ruins older than the Reach | dead world, arid world, moon | +4 inf, 2 exo · upkeep 200 cr · glory +6 | 11,800 cr, 94 mat, 8 inf | 6 |
| The Vault Archive | Deep vaults | dead world, frozen world, moon | +3 inf, 400 cr · upkeep 100 cr · prosperity +4 · legitimacy +2 once · glory +4 | 13,900 cr, 152 mat, 10 inf | 6 |
| The Sunless Cities | A buried sea | frozen world, moon | +600 cr, 4 men · upkeep 1 mat · room +18 · glory +4 | 7,400 cr, 103 mat, 12 men | 6 |
| The Ice Choir | Singing ice | frozen world, comet | +3 inf · upkeep 100 cr · unrest -1/yr · legitimacy +2 once · glory +4 | 12,200 cr, 110 mat, 10 inf | 5 |
| The Garden Estates | A garden country | living world | +700 cr, 2 inf · upkeep 100 cr · prosperity +6 · glory +4 | 14,100 cr, 113 mat, 9 inf | 5 |
| The Old Capital Restored | A capital of the old kings | living world, ocean world, arid world | +4 inf, 500 cr · upkeep 300 cr · prosperity +5 · legitimacy +8 once · glory +8 | 17,700 cr, 177 mat, 14 inf | 8 |
| The Reef Cities | The great reefs | ocean world | +600 cr, 3 men · upkeep 100 cr · room +14 · prosperity +3 · glory +4 | 6,900 cr, 90 mat, 9 men | 6 |
| The Rift Citadel | The Rift | arid world, dead world | +3 men · upkeep 200 cr · defence +1 · glory +4 | 5,000 cr, 80 mat, 6 men | 6 |
| The Mantle Forge | An open mantle | forge world | +11 mat · upkeep 200 cr · glory +5 | 5,000 cr, 110 mat, 4 exo | 8 |
| The Moon-Port | One face to its world | moon | +600 cr · upkeep 100 cr · defence +0.25 · depot · glory +4 | 4,400 cr, 60 mat | 5 |
| The Hollow Arsenal | A hollow moon | moon | upkeep 200 cr, 1 mat · defence +1.25 · depot · glory +4 | 5,000 cr, 90 mat, 4 men | 7 |
| The Comet Watch | The long comet | comet | +3 inf · upkeep 100 cr · legitimacy +3 once · glory +4 | 12,800 cr, 113 mat, 8 inf | 4 |
| The Flare Harvesters | A flare star | the sun | +3 He-3, 2 exo, 500 cr · upkeep 1 mat · glory +4 | 7,000 cr, 125 mat, 6 exo | 7 |
| The Twin Crown | A companion sun | the sun | +1,400 cr · upkeep 2 mat · prosperity +3 · glory +7 | 7,000 cr, 130 mat, 6 exo | 10 |

## Infrastructure

The system's own. Bought a level at a time on the Infrastructure tab; level N costs N times the first. **Every level
laid adds 3 to the system's development at once and 4 to what it can bear**: infrastructure no longer opens slots
itself; it is how a system is developed, and development opens them.

| track | levels | each level | and | first level costs | years by level |
|---|---|---|---|---|---|
| Civil Works | 5 | prosperity +1 |  | 1,000 cr, 8 mat | 1, 1, 2, 2, 3 |
| Habitats | 5 | room +6 | unrest −1/yr at 3, 5 | 800 cr, 10 mat, 1 men | 1, 1, 2, 2, 3 |
| Orbital Traffic | 5 | +100 cr | a depot from 3 | 1,200 cr, 6 mat | 1, 1, 2, 2, 3 |
| Outworks | 4 | +1 mat |  | 1,000 cr, 12 mat | 1, 2, 2, 3 |
| Defence Grid | 5 | defence +0.2 |  | 1,000 cr, 12 mat | 1, 1, 2, 2, 3 |

## The home world's own buildings, by specialization

These were in the game before. They share the home world's slots with the eight above and the ordinary buildings of its
kind, and each can be taken to level III. They are unchanged, except that the three megastructures among them (the Orbital Ring, the Dyson
Lattice, the Lane Gate Anchor) are great works now and no longer take a slot. A world built before the change has its ring
moved to its site.

| building | only on | what it does | cost |
|---|---|---|---|
| Power Plant | any world | +200 cr | 20 mat |
| Deep Mine | Forge-World | +3 mat · upkeep 100 cr | 3,500 cr |
| Hydroponic Complex | Agri-World | +2 men, 100 cr | 42 mat |
| Free Market | Trade Hub | +300 cr · upkeep 100 cr | 20 mat |
| Helium Skimmers | any world with a helium3 deposit | +1 He-3 · upkeep 100 cr | 1,600 cr, 12 mat |
| Rare-Earth Refinery | any world with a rare earths deposit | +1 rare earths, 100 cr · upkeep 100 cr | 3,200 cr, 19 mat |
| Isotope Works | any world with a isotopes deposit | +1 isotopes · upkeep 100 cr | 3,700 cr, 25 mat |
| Deep-Sky Sensor Array | any world | +1 inf · defence +0.1 | 1,500 cr, 5 rare earths |
| Planetary Arsenal | any world | defence +0.3 | 8 mat, 2 isotopes |
| Fleet Supply Depot | any world | upkeep 100 cr · defence +0.1 | 6 mat, 400 cr |
| Deep-Core Drill | Mining Outpost | +3 mat · upkeep 100 cr | 2,000 cr, 10 mat |
| Orbital Foundry | Shipyard World | +4 mat, 100 cr · upkeep 300 cr | 16 mat, 1,000 cr, 2 rare earths |
| Conscription Hall | Agri-Collective | +3 men · upkeep 100 cr | 3,600 cr, 18 mat |
| Bastion Batteries | Fortress-Station | upkeep 1 mat · defence +0.4 | 14 mat, 400 cr, 2 isotopes |
| Exotics Laboratory | Research Enclave | +2 exo · upkeep 300 cr | 3,200 cr, 3 exo, 3 rare earths |
| Free Port | Trade Consortium | +400 cr · upkeep 1 men | 2,900 cr |
| Patronage Office | any world | +2 inf · upkeep 200 cr | 10,000 cr |
| Public Granary | any world | upkeep 100 cr | 700 cr, 4 mat |

## Artwork

`tools/art/works.py` paints a small wide picture for every building, great work, track and world building (100 of them),
keyed by the id in content, with the same openly licensed model as the portraits; the panel shows it beside the thing. (115 since the cities.)
They are AI-generated (docs/ART_DIRECTION.md).

The owner's rule (2026-11-20) is that a picture matches its subject or is not there, and that a set has all its pictures
or none. The fifteen buildings of the cities (docs/CITIES.md) were painted on 2026-11-21 and audited the same way: a market
hall with lettering on a banner and a pit that came out as a refinery in a valley were painted again from simpler prompts,
and two the model had captioned along the bottom were trimmed. The set is 115 and ships whole. The first hundred were
audited on contact sheets on 2026-11-20: nineteen that did not show their subject were painted
again from simpler prompts and a different composition (a hospital as a ward with beds, a breakers' yard as a hull in a
hangar with men cutting it, a sensor array as a row of dishes), and one that the model had signed in a corner was
trimmed. The set ships whole.

## Decided by the owner

- **A head of state is the head of a state.** "King" is the head of a monarchy that is not big enough to be an empire;
  the Republic the game opens in is Roman, so its heads are the consuls; a republic of another kind (one a player founds,
  or the Republic reformed) has its own. So three great works at a time go to a consul, to the king, president or
  dictator of a state that has left the Reach (by its form of government), and to the ruling house of a nation. An
  archduke under someone, and the house that happens to hold the capital, are not heads of state.
- **Rank adds slots everywhere**, on the home world and on every other place.
- **Artwork matches or there is none**, and never some with and some without (docs/ART_DIRECTION.md).

## What the owner might want to change

1. **The figures.** All of them are in `works.json` under `slotRules`, `development` and `greatWorks`.
2. **What an over-full place suffers.** A point of unrest a year for each building without a slot. Nothing falls down.
3. **Which buildings are missing.** Anything a kind of body or a home world ought to have and does not is a line of
   content and a line in the art script.

## Not done

See docs/GAPS.md, "Buildings".

## Pulling a building down (2026-11-21)

From the playthroughs (docs/PLAYTHROUGH.md, Medium, Building and holding, B65). `PullDownCommand` takes the same targets
as an upgrade: a holding's id (the home world's own buildings), `system#body`, or `holding@index` (a province). Whoever
may build in the place may pull down what stands there. The slot is free; a quarter of the materials of each level come
back (`SystemWorks.ScrapPct`); legitimacy a building gave for being raised is taken back. A great work is not pulled
down, and nothing is while its next level is being built. Other houses do not pull anything down.

## One list of what a home world can take (2026-11-21)

`SystemWorks.BuildableOn` gives everything that could be built in a home world's slots and does not stand or rise there:
the buildings only a home world takes, the world's own, and the ordinary ones, each with the refusal its own command
gives. The system's panel and the House window's world card both read it. The panel shows what can be begun now first
and folds the rest by kind; great works are behind their own header.

## Where a colony can go, and claims in your belt (2026-11-21)

`SystemWorks.ColonySites` lists the empty systems within `ColonySearch` (4) lanes of a world of the house, with how far,
whether a colony can be sent now, and the house's nearest fleet. `ClaimSystem` lists the belt claims other houses hold in
a system whose world the house holds and prices a buy-out (`BuyOutClaimCommand`): twice the stake at today's prices, 300
in the hundred from one who thinks ill, 150 from a friend. A bought claim is kept under its buyer's name.
