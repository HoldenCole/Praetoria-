# The powers beyond the Reach, on the map: the plan

Written 2026-11-18 at the owner's asking ("let's work on the off-map stuff as well; at least a good plan for it";
docs/GAPS.md R10). Nothing on this page is built yet. What exists today is docs/FOREIGN_NATIONS.md.

## What is true today

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

### 0. A door, a face, and a figure (one cycle; no new system)

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
  without sitting in the Senate, the marriage market or the Exchange by accident.

### 1. Their fleets on the map (one or two cycles)

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

### Not in this plan

The threat from beyond the galaxy's edge (the design's late crisis), and anomalies and archaeology. Both want the map
to have an edge, which stage 2 gives it.

## What I would do, and what is yours to decide

**I would build stage 0 next.** It needs no new system, it makes every nation a neighbour at once, and it gives the
corridor to hold.

Yours:

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
