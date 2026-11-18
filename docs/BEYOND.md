# The powers beyond the Reach, on the map: the plan

Written 2026-11-18 at the owner's asking ("let's work on the off-map stuff as well; at least a good plan for it";
docs/GAPS.md R10). **Stage 0 was built the same day** (see "Stage 0 as built"); stages 1 to 4 are a plan. The rules
as they stand are in docs/FOREIGN_NATIONS.md.

## What was true before stage 0

Five nations are defined: the Ardent Concord, the Free Cities of Thessar, the Vashti Khanates, the Outer Compact and
the Hollow Crown. Each is a handful of numbers (strength, temper, wealth, distance, regard for the Reach). They are
not on the map, and:

- **A nation has no place.** A raid picks any world of the rim by lot. "Distance" is a count of years, not of lanes.
  There is no border, no gate and no edge to the map (31 systems, 10 regions, three rings, all of it the Reach).
- **A nation has no people.** "The council of admirals" and "the khans" are description text. An expedition fights a
  number; the only commander in the battle is yours.
- **A nation has no fleets.** Its strength is one integer. An expedition is resolved by a formula of its own, not by
  the battle rules the rest of the game uses.
- **War with a nation has no shape on the map.** A fleet sent on expedition vanishes from the map for some years and
  comes back. Nothing a nation does can be met, blocked or seen coming.
- **Nothing is measured.** The probe prints no line about nations, though the design doc quotes figures from one.

So the stories that need a rival power cannot be played: holding a corridor against an empire, conquering the state
next door, governing what was conquered, fleeing to a foreign court.

## What it should be

A foreign power is a neighbour: it lies in a direction, it comes through a particular door, it is led by people with
names who can win, lose, die and be dealt with, and what it holds can be taken and governed. It is brought onto the
map **in layers**, each of which leaves a whole game, and a nation that has not yet been brought on keeps working as
it does today.

## The stages

### 0. A door, a face, and a figure (built: see "Stage 0 as built" below)

Everything here uses what is on the map already.

- **A door.** Each nation is given one or two systems of the rim that face it: its **march**. Raids come through the
  march and fall on worlds near it, not on any rim world by lot. An expedition's length is the lanes from where the
  fleet lies to the march, plus the nation's depth beyond it. A fleet standing at the march meets a raid before it
  lands. That is the corridor: something to fortify, garrison and be posted to.
- **A face.** Each nation gets a ruler and one or two commanders: real people, named from a list of the nation's own
  (name lists are data; the game already picks names by culture). An expedition is fought against a named
  commander whose skill counts, who can be beaten, killed, or come back with a grudge. The ruler's nature moves the
  nation's temper, and the ruler dies and is succeeded.
- **A figure.** A line in the probe: raids, where they fell, wars voted, expeditions sent and how they ended, tribute.
- **A safe way to ask "which houses are of the Reach".** About 45 places in the code walk every house and leave out
  only the raiders. They are moved onto one question asked in one place, so that a foreign house can exist later
  without sitting in the Senate, the marriage market or the Exchange by accident. *Not done in stage 0: see below.*

### 1. Their fleets on the map (one or two cycles)

- First, the safe question above: it is needed the moment a nation owns a fleet on the map, and not before.

- A nation at war, or raiding in strength, sends **a real fleet through its march**: it moves along lanes, fights by
  the ordinary battle rules, and besieges. Its hulls are drawn from the nation's strength, and what it loses on the
  map the nation has lost.
- **War with a nation takes the shape the raiders already have**: every house of the Reach is at war with a nation
  the Senate is at war with, without a war record for each pair.
- **A world of the rim can be occupied.** It pays the nation, not its house, until it is retaken or the peace gives
  it back. A bad peace cedes it.
- The Reach answers with what it has: the houses nearest, the consuls' fleets, emergency powers (a war beyond the
  borders is already a danger the Senate can vote them for).

### 2. Their side of the border (several cycles; one nation first)

- **Space beyond the march**: for a nation, two to four systems of its own (a border world, a heartland, a capital),
  in a fourth ring. One nation is done first as the pilot, start to finish, before any other.
- **An expedition becomes ordinary movement.** The fleet sails through the march into their space, meets their home
  fleets, and besieges their worlds. Taking a nation's capital is what subduing it means. The off-map expedition is
  retired for that nation; fleets that were away when a save is loaded are brought home.
- **The Reach's own rules stay inside the Reach.** Allegiance to the capital, rebellion, the free march and raider
  nests do not run in foreign space; the map draws a fourth ring.

### 3. What is conquered is governed (one or two cycles)

- A subdued nation's worlds become **a province**: held for the Reach, not by a house. It has a governor, a
  garrison, a tribute, and a temper of its own that can end in revolt.
- **The governor is a person appointed**: by the Senate under the Republic, by the throne under one. This is where a
  command by service (R1), the acts of an emperor (R6) and the reward for backing a winner (R16) come to rest.
- A general who governs a province for years gathers what the Caesar road is made of: victories, devoted crews,
  money. A province is where a Caesar comes from.
- The other ending: a nation left standing under a ruler the Reach chose, paying tribute.

### 4. Their politics, and ours with them (several cycles)

- Each nation is **a state under the same rules as the Reach** (the forms of government the owner has planned,
  G1 to G7): cities that elect, khans who fight for the horde, a crown in decay.
- **Dealings**: envoys, alliance, recognition of a state that has broken away, a marriage into a foreign house.
- **Refuge and backing**: a deposed emperor or a broken pretender flees to a foreign court (docs/THRONE.md, stage 4);
  a nation backs a pretender or a breakaway for a price.
- Nations at war with each other, and the Reach choosing a side.

## Stage 0 as built

**A door.** Each nation has a march: one or two systems of the rim that face it.

| Nation | Its march | Years beyond it |
|---|---|---|
| The Ardent Concord | The Valmont Anvils and Ironhold (east) | 2 |
| The Free Cities of Thessar | Wintermoor and Brackwater (south-east) | 1 |
| The Vashti Khanates | The Anvil and The Veil (west) | 1 |
| The Outer Compact | Sorrow (south) | 3 |
| The Hollow Crown | Last Harbour and Hollow Reach (south-west) | 2 |

- **Raids come through the march** and fall on a world within two lanes of it. Before, a raid picked any world of
  the rim by lot.
- **A fleet of three hulls or more standing at the march turns a raid back** before it lands, and its commander has
  the name of it. The older rule still holds behind it: three hulls in orbit over the world itself turn a raid back
  there.
- **An expedition's voyage** is the nation's years beyond its march, and a year more for every three lanes between
  where the fleet lies and the march. A fleet posted at the march sails soonest.
- **Other houses use the door.** A house raided from beyond the rim in the last eight years, at peace and with a
  fleet of three to spare, posts it at that nation's march.

**A face.** Each nation has a ruler and a commander of its fleets, with titles and names of its own (a First Admiral
and Admirals, a Khan of Khans and Noyans, an Autokrator and a Grand Domestic).

- **The commander's skill counts**, 1 to 9: every point over four makes the nation's fleets fight six in a hundred
  harder, and every point under as much less. A commander who beats an expedition learns from it. One whose fleet is
  routed is killed one time in four, the victor has the name of it, and someone new and greener takes the command.
- **The ruler's nature leans on the nation's regard for the Reach.** The warlike sour it; the wary raid only in a
  real quarrel; the grasping raid for twice the cargo; the mild sweeten it. Half the time a nation gets the kind of
  ruler its temper makes, and otherwise anyone.
- **Rulers die and are followed.** One time in three, if the commander is any good, it is the commander who takes
  the chair, and such a ruler is never mild. The nation's regard moves at once with the new ruler.
- They are not yet people of the Reach's world: they cannot be married, bribed, met or captured. That needs the
  safe question of stage 1.

**A figure.** The probe prints a line for the nations: raids that landed, were turned back at the world, were met at
the march; wars, expeditions and battles; commanders killed; rulers succeeded.

**On the screen.** "Beyond the Reach" on the Military screen shows, for each nation, who rules it and of what
nature, who commands its fleets and how well, its march and whether a fleet holds it, and how many years out an
expedition would be from your nearest fleet. Three reports (`content/events/nations.json`): met at the march, a new
hand beyond the rim, their commander falls.

**What stage 0 left out of its own plan.** The safe question for "which houses are of the Reach" was not built. It
turned out not to be needed while a nation's people are not characters of the Reach's world, and moving 45 places
for nothing to depend on was risk without a use. It is the first thing in stage 1. The map does not yet mark a
march: the Military screen names it.

**Measured** over sixty unattended games of sixty years: 521 raids landed, 170 were met at a march (other houses
posted a fleet to one 54 times) and 45 were turned back at the world; 81 wars voted, 250 expeditions, 550 battles;
82 of their commanders killed; 643 rulers succeeded, about two to a nation in sixty years; rulers at the end were
grasping 114, warlike 88, mild 60, wary 38. The full account is docs/GAP_ANALYSIS.md, section 75.

### Not in this plan

The threat from beyond the galaxy's edge (the design's late crisis), and anomalies and archaeology. Both want the map
to have an edge, which stage 2 gives it.

## What is yours to decide

Stage 1 can be built on the answers assumed below. Stage 2 should not begin without them.

1. **How many come onto the map.** All five, or the nearer three with the Outer Compact and the Hollow Crown left as
   far powers that trade and send embassies. I would bring on three, one at a time. The map grows by about a third
   for three nations and by a half for five.
2. **Whether the Reach can lose worlds for good.** I have assumed yes: a rim world can be occupied in a war and ceded
   in a bad peace, and taken back in the next.
3. **Whether the player's own house can be conquered.** I have assumed its worlds can be, and that the house goes on
   landless as a fallen house does now.
4. **Which nation is first in stage 2.** I would take the Vashti Khanates: nearest, hostile, and raiders already.

## What it opens

Holding a corridor against an empire (stage 0 and 1). Conquering the rival state (stage 2). A province and the
general who comes home from it (stage 3). A foreign court to flee to and a foreign power behind a pretender (stage
4). And a border that means something for trade and the lanes (B2).

## What is hard about it

- **War is between two houses.** A war between the Reach and a nation has no form on the map. Stage 1 gives it the
  form the raiders have.
- **The code assumes every house is of the Reach** in about 45 places. Stage 0 fixes that before anything depends
  on it.
- **Strength is one number** and has to become hulls that are built, supplied and moved by rules.
- **The map is drawn by hand**, with three rings assumed in the drawing and in the checks. A fourth ring and fifteen
  new systems are real authoring.
- **Subduing is not conquering.** Today a subdued nation pays tribute for twenty years; taking a world gives it to
  one house. A province is neither, and is new.
- **Saves.** Fleets away on expedition, war bonds and events are keyed to a nation that is off the map.
