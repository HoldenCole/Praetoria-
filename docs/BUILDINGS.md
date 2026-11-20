# Buildings, great works and infrastructure

The owner, 2026-11-20: "separate out the mega structures from the ordinary buildings. We should think of ordinary buildings
kind of in the same way that Star Dynasties has different structures you can build, and how other Paradox games work with
having building slots. And then the mega structures will be unique things that cost more money and have to be built over a
long period of time. Every planetary body within a solar system should have a certain amount of building slots and each
type of planetary body should have unique types of buildings as well. And then there should be able to be things that you
just build out in space that do not have to be connected to a planetary body or things such as infrastructure that you can
just invest in over time. Let's work on making a list of these kinds of things and then work on the way that building
slots work in the game. After we have that done, let us get some artwork that matches the buildings."

This is the list, and the rules for slots. The tables are written from the content (`content/holdings/works.json`,
`buildings.json`, `holdings_ext.json`); the figures there are the ones the game uses. docs/SYSTEMS.md says what a solar
system is.

## Three kinds of thing

| | where it goes | how many | cost and time |
|---|---|---|---|
| **A building** | a **slot** on a body, or in open space | as many as the body has slots | 14 to 42 in credits and materials; at once, or up to four years |
| **A great work** (megastructure) | the body's one **site** | one to a body, and a house raises one at a time | 64 to 240; 4 to 12 years, paid as it rises; gated by rank |
| **Infrastructure** | the system itself, no body | five tracks, four or five levels each | level N costs N times the first; the higher levels take two and three years |

## How slots work

- **Every body has building slots by its kind.** A slot holds one building. A great work takes no slot.

| place | slots | site for a great work |
|---|---|---|
| The sun | 0 | yes |
| The system's own world | its specialization's (2 or 3), +1 at civil works I, III and V | yes |
| A living or ocean sister-world | 3 | yes |
| An arid, forge, frozen or dead world | 2 (a small one 1) | yes |
| A gas giant | 2 | yes |
| An asteroid belt | 2 | yes |
| A moon | 1 | yes |
| A comet | 1 | yes |
| Open space | 2 | yes |

- **Outworks** (infrastructure) open one more slot on every body but the world, and in open space, at their second and
  fourth levels. **Civil works** open one on the system's world at their first, third and fifth.
- **The system's own world is built on through its holding**, as before: its buildings are the ones its specialization
  allows (the last table here), and its slots are the holding's. No building from the other tables goes on it.
- **Each kind of body has buildings of its own.** A forge world has smelters, an ocean has tidal mills, a belt has a
  prospectors' exchange; a few (an outpost, a strip mine, a listening post) suit several kinds.
- **One site, one great work.** Where a body has a feature, the work only that feature allows competes for the site
  with the great works any such body could take: the ringed giant can have the Ring Habitat or the Sky Harbour, not both.
- A house **raises one great work at a time**, wherever it is.
- A building or a great work **goes with the system**: whoever holds its world has what was built round it.

## Buildings, by where they go

### A living sister-world

| building | what it does | cost | years | note |
|---|---|---|---|---|
| Settled Sister-World | +3 cr, 3 men · upkeep 1 cr · room +16 · prosperity +3 | 26 cr, 14 mat, 8 men | 4 |  |
| Terrace Farms | +2 men, 2 cr · upkeep 1 cr · room +6 | 12 cr, 8 mat, 3 men | 1 | only here |
| Game Parks | +1 inf · upkeep 1 cr · prosperity +3 | 16 cr, 6 mat, 1 inf | 2 | only here |
| Orbital Batteries | upkeep 1 cr, 1 mat · defence +0.5 | 18 cr, 14 mat | 2 |  |

### An ocean sister-world

| building | what it does | cost | years | note |
|---|---|---|---|---|
| Settled Sister-World | +3 cr, 3 men · upkeep 1 cr · room +16 · prosperity +3 | 26 cr, 14 mat, 8 men | 4 |  |
| Sea Farms | +2 men, 2 cr · upkeep 1 cr · room +6 | 12 cr, 8 mat, 3 men | 1 | only here |
| Tidal Mills | +2 mat, 2 cr · upkeep 1 cr | 14 cr, 12 mat | 2 | only here |
| Deep Harbour | +3 cr · upkeep 1 mat · prosperity +2 | 18 cr, 12 mat | 2 | only here |
| Orbital Batteries | upkeep 1 cr, 1 mat · defence +0.5 | 18 cr, 14 mat | 2 |  |

### An arid world

| building | what it does | cost | years | note |
|---|---|---|---|---|
| Outpost | +1 mat, 1 men · upkeep 1 cr · room +4 | 8 cr, 6 mat, 2 men | 1 |  |
| Domed Colony | +3 cr, 2 men · upkeep 1 mat · room +12 · prosperity +2 | 22 cr, 16 mat, 6 men | 3 | needs an outpost there |
| Sun Fields | +3 cr · upkeep 1 mat | 14 cr, 10 mat | 1 | only here |
| Caravanserai | +2 cr · upkeep 1 cr · prosperity +3 | 14 cr, 8 mat | 1 | only here |
| Strip Mine | +3 mat · upkeep 1 cr | 12 cr, 8 mat, 2 men | 1 |  |
| Rare-Earth Diggings | +1 rare earths ×richness · upkeep 1 cr | 16 cr, 10 mat, 2 men | 1 | needs a rare earths deposit |
| Orbital Batteries | upkeep 1 cr, 1 mat · defence +0.5 | 18 cr, 14 mat | 2 |  |

### A forge world

| building | what it does | cost | years | note |
|---|---|---|---|---|
| Outpost | +1 mat, 1 men · upkeep 1 cr · room +4 | 8 cr, 6 mat, 2 men | 1 |  |
| Strip Mine | +3 mat · upkeep 1 cr | 12 cr, 8 mat, 2 men | 1 |  |
| Smelter Row | +4 mat · upkeep 1 cr | 16 cr, 10 mat, 2 men | 2 | only here |
| Geothermal Tap | +3 cr · upkeep 1 mat | 14 cr, 12 mat | 2 | only here |
| Orbital Batteries | upkeep 1 cr, 1 mat · defence +0.5 | 18 cr, 14 mat | 2 |  |

### A frozen world or ice dwarf

| building | what it does | cost | years | note |
|---|---|---|---|---|
| Outpost | +1 mat, 1 men · upkeep 1 cr · room +4 | 8 cr, 6 mat, 2 men | 1 |  |
| Domed Colony | +3 cr, 2 men · upkeep 1 mat · room +12 · prosperity +2 | 22 cr, 16 mat, 6 men | 3 | needs an outpost there |
| Ice Works | +2 cr, 1 mat · upkeep 1 cr | 10 cr, 6 mat | 1 |  |
| Cold Granaries | upkeep 1 cr · room +4 · unrest -1/yr | 14 cr, 10 mat | 1 | only here |
| Penal Colony | +2 mat, 1 men · upkeep 1 cr · prosperity -1 · unrest -1/yr | 10 cr, 8 mat | 1 |  |
| Isotope Pits | +1 isotopes ×richness · upkeep 2 cr | 20 cr, 12 mat, 3 men | 2 | needs a isotopes deposit; rank 1 |
| Hermitage | +1 inf · upkeep 1 cr · unrest -1/yr | 10 cr, 6 mat, 1 inf | 1 | one to a system |
| Observatory | +1 inf · upkeep 1 cr | 14 cr, 8 mat, 1 exo | 2 | one to a system |
| Listening Post | upkeep 1 cr · defence +0.25 | 10 cr, 6 mat | 1 | one to a system |
| Orbital Batteries | upkeep 1 cr, 1 mat · defence +0.5 | 18 cr, 14 mat | 2 |  |

### A dead world or rock

| building | what it does | cost | years | note |
|---|---|---|---|---|
| Outpost | +1 mat, 1 men · upkeep 1 cr · room +4 | 8 cr, 6 mat, 2 men | 1 |  |
| Domed Colony | +3 cr, 2 men · upkeep 1 mat · room +12 · prosperity +2 | 22 cr, 16 mat, 6 men | 3 | needs an outpost there |
| Strip Mine | +3 mat · upkeep 1 cr | 12 cr, 8 mat, 2 men | 1 |  |
| Penal Colony | +2 mat, 1 men · upkeep 1 cr · prosperity -1 · unrest -1/yr | 10 cr, 8 mat | 1 |  |
| Proving Ground | +2 men · upkeep 1 cr · defence +0.25 | 14 cr, 10 mat, 2 men | 1 | only here |
| Rare-Earth Diggings | +1 rare earths ×richness · upkeep 1 cr | 16 cr, 10 mat, 2 men | 1 | needs a rare earths deposit |
| Isotope Pits | +1 isotopes ×richness · upkeep 2 cr | 20 cr, 12 mat, 3 men | 2 | needs a isotopes deposit; rank 1 |
| Observatory | +1 inf · upkeep 1 cr | 14 cr, 8 mat, 1 exo | 2 | one to a system |
| Listening Post | upkeep 1 cr · defence +0.25 | 10 cr, 6 mat | 1 | one to a system |
| Orbital Batteries | upkeep 1 cr, 1 mat · defence +0.5 | 18 cr, 14 mat | 2 |  |

### A gas giant

| building | what it does | cost | years | note |
|---|---|---|---|---|
| Skimmer Fleet | +1 He-3 ×richness · upkeep 1 cr | 14 cr, 10 mat | 1 | needs a helium3 deposit; only here |
| Cloud Habitat | +4 cr, 1 men · upkeep 1 cr · room +8 | 24 cr, 18 mat, 4 men, 1 exo | 3 | only here |
| Aerostat Refinery | +2 mat, 2 cr · upkeep 1 cr | 16 cr, 14 mat | 2 | only here |
| Orbital Batteries | upkeep 1 cr, 1 mat · defence +0.5 | 18 cr, 14 mat | 2 |  |
| Fleet Anchorage | upkeep 2 cr · defence +0.25 · depot | 22 cr, 18 mat | 2 | one to a system |

### An asteroid belt

| building | what it does | cost | years | note |
|---|---|---|---|---|
| Belt Foundry | +4 mat · upkeep 1 cr | 16 cr, 10 mat, 2 men | 2 | only here |
| Prospectors' Exchange | +3 cr · upkeep 1 cr | 14 cr, 8 mat | 1 | only here |
| Hidden Dock | +4 cr · unrest +1/yr | 12 cr, 8 mat | 1 | only here |
| Rare-Earth Diggings | +1 rare earths ×richness · upkeep 1 cr | 16 cr, 10 mat, 2 men | 1 | needs a rare earths deposit |
| Fleet Anchorage | upkeep 2 cr · defence +0.25 · depot | 22 cr, 18 mat | 2 | one to a system |

### A moon

| building | what it does | cost | years | note |
|---|---|---|---|---|
| Outpost | +1 mat, 1 men · upkeep 1 cr · room +4 | 8 cr, 6 mat, 2 men | 1 |  |
| Domed Colony | +3 cr, 2 men · upkeep 1 mat · room +12 · prosperity +2 | 22 cr, 16 mat, 6 men | 3 | needs an outpost there |
| Strip Mine | +3 mat · upkeep 1 cr | 12 cr, 8 mat, 2 men | 1 |  |
| Rare-Earth Diggings | +1 rare earths ×richness · upkeep 1 cr | 16 cr, 10 mat, 2 men | 1 | needs a rare earths deposit |
| Isotope Pits | +1 isotopes ×richness · upkeep 2 cr | 20 cr, 12 mat, 3 men | 2 | needs a isotopes deposit; rank 1 |
| Mass Driver | +3 mat · upkeep 1 cr | 16 cr, 12 mat | 2 | only here |
| Moon Fort | upkeep 1 cr · defence +0.5 | 18 cr, 16 mat, 2 men | 2 | only here |
| Hermitage | +1 inf · upkeep 1 cr · unrest -1/yr | 10 cr, 6 mat, 1 inf | 1 | one to a system |
| Observatory | +1 inf · upkeep 1 cr | 14 cr, 8 mat, 1 exo | 2 | one to a system |
| Listening Post | upkeep 1 cr · defence +0.25 | 10 cr, 6 mat | 1 | one to a system |
| Fleet Anchorage | upkeep 2 cr · defence +0.25 · depot | 22 cr, 18 mat | 2 | one to a system |

### A comet

| building | what it does | cost | years | note |
|---|---|---|---|---|
| Outpost | +1 mat, 1 men · upkeep 1 cr · room +4 | 8 cr, 6 mat, 2 men | 1 |  |
| Ice Works | +2 cr, 1 mat · upkeep 1 cr | 10 cr, 6 mat | 1 |  |
| Hermitage | +1 inf · upkeep 1 cr · unrest -1/yr | 10 cr, 6 mat, 1 inf | 1 | one to a system |
| Observatory | +1 inf · upkeep 1 cr | 14 cr, 8 mat, 1 exo | 2 | one to a system |
| Listening Post | upkeep 1 cr · defence +0.25 | 10 cr, 6 mat | 1 | one to a system |

### Open space

| building | what it does | cost | years | note |
|---|---|---|---|---|
| Listening Post | upkeep 1 cr · defence +0.25 | 10 cr, 6 mat | 1 | one to a system |
| Fleet Anchorage | upkeep 2 cr · defence +0.25 · depot | 22 cr, 18 mat | 2 | one to a system |
| Waystation | +2 cr · upkeep 1 mat · prosperity +3 | 16 cr, 12 mat | 2 | only here |
| Customs Cordon | +4 cr · upkeep 1 cr · prosperity -1 | 14 cr, 10 mat | 1 | only here |
| Relay Beacon | +1 inf · upkeep 1 cr | 12 cr, 8 mat, 1 exo | 1 | only here |
| Patrol Base | upkeep 1 cr · defence +0.5 · unrest -1/yr | 16 cr, 14 mat, 2 men | 2 | only here |
| Minefields | upkeep 1 mat · defence +0.75 · prosperity -2 | 12 cr, 16 mat | 1 | only here |

## Great works

One to a body, on its site. All are years in the raising and wanted for the name they give as much as for what they yield.

### Any system may raise

| great work | where | what it does | cost | years | note |
|---|---|---|---|---|---|
| The Orbital Ring | the system's own world | +8 cr, 3 mat · upkeep 2 cr · prosperity +4 · glory +6 | 60 cr, 110 mat, 4 exo | 8 | rank 3 |
| The Great Stair | the system's own world | +6 cr · upkeep 2 cr · room +10 · prosperity +6 · glory +4 | 50 cr, 90 mat, 3 exo | 7 | rank 2 |
| The Aegis | the system's own world | upkeep 3 cr, 1 mat · defence +2 · glory +6 | 70 cr, 120 mat, 6 exo | 9 | rank 3 |
| The Spire City | living world, ocean world, arid world | +6 cr, 3 men · upkeep 2 cr · room +30 · prosperity +3 · glory +5 | 70 cr, 100 mat, 10 men | 8 | rank 2 |
| The Dyson Lattice | the sun | +16 cr · upkeep 2 mat · glory +12 | 80 cr, 160 mat, 8 exo | 12 | rank 4 |
| The Sun Foundry | the sun | +6 cr, 8 mat · upkeep 2 mat · glory +6 | 50 cr, 110 mat, 4 exo | 8 | rank 3 |
| The Sky Harbour | gas giant | +7 cr, 1 He-3 · upkeep 2 cr · room +10 · glory +4 | 50 cr, 90 mat, 3 exo | 7 | rank 2 |
| The Hollowed World | belt | +5 mat, 2 men · upkeep 2 cr · defence +0.5 · room +12 · glory +4 | 50 cr, 80 mat, 6 men | 7 | rank 2 |
| The Fortress Moon | moon | upkeep 2 cr, 1 mat · defence +1.5 · glory +4 | 50 cr, 80 mat, 6 men | 6 | rank 2 |
| The Mausoleum of the House | moon, dead world, frozen world | +1 inf · upkeep 1 cr · legitimacy +5 once · glory +6 | 50 cr, 50 mat, 4 inf | 6 | one to a house; rank 1 |
| The Lane Gate Anchor | open space | +4 inf · upkeep 3 cr · defence +0.5 · glory +8 | 60 cr, 110 mat, 6 exo | 9 | rank 4 |
| The Grand Arsenal | open space | upkeep 3 cr, 1 mat · defence +1 · depot · glory +6 | 60 cr, 120 mat, 6 men | 9 | one to a house; rank 3 |
| The Long Cylinder | open space | +5 cr, 3 men · upkeep 2 cr · room +24 · glory +5 | 60 cr, 110 mat, 8 men | 8 | rank 2 |
| The Lighthouse of the Lanes | open space | +6 cr, 4 inf · upkeep 3 cr · legitimacy +6 once · glory +12 | 80 cr, 100 mat, 6 exo, 5 inf | 10 | one in the galaxy; at a key of the lanes; rank 3 |

### Only where the feature is

| great work | the feature | on | what it does | cost | years |
|---|---|---|---|---|---|
| The Ring Habitat | Great rings | gas giant | +8 cr, 4 men · upkeep 2 cr · room +20 · prosperity +3 · glory +5 | 56 cr, 90 mat, 4 exo | 7 |
| The Storm Tap | An eternal storm | gas giant | +4 He-3, 2 exo · upkeep 2 cr · glory +4 | 50 cr, 80 mat, 3 exo | 6 |
| The Breaker's Yard | A wreck field | belt | +8 mat, 4 cr · upkeep 1 cr · glory +4 | 40 cr, 50 mat, 4 men | 5 |
| The Lode Works | The mother lode | belt | +7 mat, 2 rare earths, 2 exo · upkeep 2 cr · glory +4 | 44 cr, 56 mat, 4 men | 5 |
| The Great Dig | Ruins older than the Reach | dead world, arid world, moon | +4 inf, 2 exo · upkeep 2 cr · glory +6 | 50 cr, 40 mat, 3 inf | 6 |
| The Vault Archive | Deep vaults | dead world, frozen world, moon | +3 inf, 4 cr · upkeep 1 cr · prosperity +4 · legitimacy +2 once · glory +4 | 46 cr, 50 mat, 3 inf | 6 |
| The Sunless Cities | A buried sea | frozen world, moon | +6 cr, 4 men · upkeep 1 mat · room +18 · glory +4 | 50 cr, 70 mat, 8 men | 6 |
| The Ice Choir | Singing ice | frozen world, comet | +3 inf · upkeep 1 cr · unrest -1/yr · legitimacy +2 once · glory +4 | 40 cr, 36 mat, 3 inf | 5 |
| The Garden Estates | A garden country | living world | +7 cr, 2 inf · upkeep 1 cr · prosperity +6 · glory +4 | 50 cr, 40 mat, 3 inf | 5 |
| The Old Capital Restored | A capital of the old kings | living world, ocean world, arid world | +4 inf, 5 cr · upkeep 3 cr · prosperity +5 · legitimacy +8 once · glory +8 | 80 cr, 80 mat, 6 inf | 8 |
| The Reef Cities | The great reefs | ocean world | +6 cr, 3 men · upkeep 1 cr · room +14 · prosperity +3 · glory +4 | 46 cr, 60 mat, 6 men | 6 |
| The Rift Citadel | The Rift | arid world, dead world | +3 men · upkeep 2 cr · defence +1 · glory +4 | 50 cr, 80 mat, 6 men | 6 |
| The Mantle Forge | An open mantle | forge world | +11 mat · upkeep 2 cr · glory +5 | 50 cr, 110 mat, 4 exo | 8 |
| The Moon-Port | One face to its world | moon | +6 cr · upkeep 1 cr · defence +0.25 · depot · glory +4 | 44 cr, 60 mat | 5 |
| The Hollow Arsenal | A hollow moon | moon | upkeep 2 cr, 1 mat · defence +1.25 · depot · glory +4 | 50 cr, 90 mat, 4 men | 7 |
| The Comet Watch | The long comet | comet | +3 inf · upkeep 1 cr · legitimacy +3 once · glory +4 | 34 cr, 30 mat, 2 inf | 4 |
| The Flare Harvesters | A flare star | the sun | +3 He-3, 2 exo, 5 cr · upkeep 1 mat · glory +4 | 50 cr, 90 mat, 4 exo | 7 |
| The Twin Crown | A companion sun | the sun | +14 cr · upkeep 2 mat · prosperity +3 · glory +7 | 70 cr, 130 mat, 6 exo | 10 |

## Infrastructure

The system's own. Bought a level at a time on the Infrastructure tab; level N costs N times the first.

| track | levels | each level | and | first level costs | years by level |
|---|---|---|---|---|---|
| Civil Works | 5 | prosperity +1 | a slot on the world at 1, 3, 5 | 10 cr, 8 mat | 1, 1, 2, 2, 3 |
| Habitats | 5 | room +6 | unrest −1/yr at 3, 5 | 8 cr, 10 mat, 1 men | 1, 1, 2, 2, 3 |
| Orbital Traffic | 5 | +1 cr | a depot from 3 | 12 cr, 6 mat | 1, 1, 2, 2, 3 |
| Outworks | 4 | +1 mat | a slot on every other body and open space at 2, 4 | 10 cr, 12 mat | 1, 2, 2, 3 |
| Defence Grid | 5 | defence +0.2 |  | 10 cr, 12 mat | 1, 1, 2, 2, 3 |

## The buildings of a system's own world

These were in the game before and are unchanged, except that the three megastructures among them (the Orbital Ring, the Dyson
Lattice, the Lane Gate Anchor) are great works now and no longer take a slot. A world built before the change has its ring
moved to its site.

| building | only on | what it does | cost |
|---|---|---|---|
| Power Plant | any world | +2 cr | 6 mat |
| Deep Mine | Forge-World | +3 mat · upkeep 1 cr | 4 cr |
| Hydroponic Complex | Agri-World | +2 men, 1 cr | 5 mat |
| Free Market | Trade Hub | +3 cr · upkeep 1 cr | 6 mat |
| Helium Skimmers | any world with a helium3 deposit | +1 He-3 · upkeep 1 cr | 8 cr, 6 mat |
| Rare-Earth Refinery | any world with a rare earths deposit | +1 rare earths, 1 cr · upkeep 1 cr | 10 cr, 6 mat |
| Isotope Works | any world with a isotopes deposit | +1 isotopes · upkeep 1 cr | 12 cr, 8 mat |
| Deep-Sky Sensor Array | any world | +1 inf · defence +0.1 | 6 cr, 2 rare earths |
| Planetary Arsenal | any world | defence +0.3 | 8 mat, 2 isotopes |
| Fleet Supply Depot | any world | upkeep 1 cr · defence +0.1 | 6 mat, 4 cr |
| Deep-Core Drill | Mining Outpost | +3 mat · upkeep 1 cr | 8 cr, 4 mat |
| Orbital Foundry | Shipyard World | +4 mat, 1 cr · upkeep 3 cr | 16 mat, 10 cr, 2 rare earths |
| Conscription Hall | Agri-Collective | +3 men · upkeep 1 cr | 6 cr, 3 mat |
| Bastion Batteries | Fortress-Station | upkeep 1 mat · defence +0.4 | 14 mat, 4 cr, 2 isotopes |
| Exotics Laboratory | Research Enclave | +2 exo · upkeep 3 cr | 12 cr, 1 exo, 1 rare earths |
| Free Port | Trade Consortium | +4 cr · upkeep 1 men | 10 cr |
| Patronage Office | any world | +2 inf · upkeep 2 cr | 9 cr |
| Public Granary | any world | upkeep 1 cr | 7 cr, 4 mat |

## Artwork

`tools/art/works.py` paints a small wide picture for every building, great work, track and world building (92 of them),
keyed by the id in content, with the same openly licensed model as the portraits; the panel shows it beside the thing.
They are AI-generated (docs/ART_DIRECTION.md).

## What the owner might want to change

1. **The numbers of slots.** They are in `works.json` under `slots` and are a first guess: a moon 1, a giant 2, a living
   sister-world 3.
2. **Whether the system's own world should take the same buildings as a sister-world.** It does not: it has its
   specialization's list, as before.
3. **One great work at a time for a house.** It keeps a rich house from raising five at once; it also means a duke with
   ten systems builds no faster than a baron.
4. **Which buildings are missing.** The list above is what could be thought of in a sitting. Anything a kind of body
   ought to have and does not is a line of content and a line in the art script.

## Not done

See docs/GAPS.md, "Buildings".
