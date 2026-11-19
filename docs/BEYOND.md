# The powers beyond the Reach, on the map: the plan

Written 2026-11-18 at the owner's asking ("let's work on the off-map stuff as well; at least a good plan for it";
docs/GAPS.md R10). **Stage 0 was built the same day** (see "Stage 0 as built"). Later that day the owner said: "let's
focus on adding in nations, then we can figure out the details. Add in the khanate first." **The Vashti Khanates are
on the map**: stages 1 and 2, for that one nation (see "The Vashti Khanates on the map, as built"). Then: "Add the
Free Cities of Thessar next." **The Free Cities are on the map** too, and are a different kind of neighbour (see "The
Free Cities of Thessar on the map, as built"). Then: "Add the Ardent Concord next." **The Ardent Concord is on the
map**, and is the strong one (see "The Ardent Concord on the map, as built"). Then: "Add the Outer Compact and the
Hollow Crown next." **All five nations are on the map** (see "The Outer Compact and the Hollow Crown on the map, as
built"). Stages 3 and 4 are a plan. The older rules in docs/FOREIGN_NATIONS.md (expeditions that leave the map) are
kept in the engine for a map drawn without a nation's worlds, and no shipped nation uses them. Everything that is the
owner's to decide is in one list at the end.

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

### 1. Their fleets on the map (built, for all five nations)

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

### 2. Their side of the border (built, for all five nations; the Khanates were the pilot)

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

## The Vashti Khanates on the map, as built

**Where.** A fourth ring, **beyond**, holds one region: the Vashti Steppe, three systems west of the rim. The Long
Grass is one lane beyond both The Anvil and The Veil (the Khanates' march). Tul Aimag and Khal Orda lie behind it.
The map is 34 systems, 45 lanes and 11 regions now.

| World | System | What it is |
|---|---|---|
| The Long Grass Camps | The Long Grass | the border world, where the host stands at peace |
| The Wells of Tul | Tul Aimag | the heartland |
| The Great Camp | Khal Orda | a fortress, and the capital: the Khan of Khans sits here |

**What holds it.** A house, "The Vashti Khanates", that is **not a house of the Reach**. It holds worlds and fleets
and can be fought. It has no seat in the Senate, no party, no office, no marriages, no place on the Exchange, and no
house of the Reach can declare a private war on it. The code asks one question for this, `World.Outsider`, in the 111
places that used to leave out only the raiders. The Reach's own rules do not run in the Steppe: no allegiance to the
capital, no raider nests, no part in the great crisis.

**Its people.** The Khan of Khans and the Noyan are persons on the map now. The Khan commands the guard at the
capital and governs the Khanates' worlds; the Noyan commands the host. Either can be killed in a battle, and is then
followed by the stage 0 rules. They are still not people of the Reach's own life: nobody can court, bribe or meet
them, and no event casts them.

**Its fleets.** The Khanates' strength, 14, is hulls: half in the guard, which stays with the ruler, half in the
host. Hulls lost are lost; two grow back every five years while the capital is held. A host that is lost is raised
again from the guard when the guard has hulls to spare.

**War.** War is still the Senate's to vote. While it lasts **every house of the Reach is at war with the Khanates**,
with no war record for each pair. Fleets reach them by the lanes; the off-map expedition is refused for them.

- **Their host** comes through the march and sits over the nearest world of the Reach, within three lanes of its
  border world, whose defenders it outnumbers. It is slow at walls: a siege takes it two years longer than it takes
  a fleet of the Reach. **One world a season**: when the world falls the host goes home with it, and comes out again
  when it is rested and supplied. It needs supply in the Reach like any fleet.
- **A world they take is theirs** until a house of the Reach frees it or the peace is made.
- **A world freed goes home to the house it was taken from**, whichever house freed it. The house that freed it
  gains a point of standing and the credit toward the triumph.
- **Against their fleets the houses of the Reach fight as one**: one battle a year in a system, led by the strongest
  house there, with the guns of the others behind it. The losses are the leader's.
- **Other houses go to the war.** A house whose own world is taken or sat over goes to it. A house of the war party,
  a serving officer's, or one holding a consulship or the Admiralty goes to drive the host out, and after that to
  take the Khanates' own worlds, the weakest held first. Each goes when what the Reach could bring to that system
  (its own fleet, those there or bound there, and the rested fleets of other such houses within six lanes) matches
  what is there, or is a quarter more beyond the march.
- **They gather first.** A defended world beyond the march is not attacked one squadron at a time. The fleets go to
  the march nearest it, wait there, and go in together in the year those standing at the march are a quarter more
  than what holds the world. (Added with the Free Cities, for both nations.)
- **Beating them pays.** The house that beats a nation's fleet takes the nation's wealth in credits (4 for the
  Khanates), and twice that for taking one of its worlds. (Added with the Free Cities, for both nations.)

**The tally.** The war ends as before, when one side leads by six.

| What | For the Reach | For the Khanates |
|---|---|---|
| A battle | 2 for beating their fleet | 1 for beating a fleet of the Reach |
| A world | 3 for freeing one of the Reach's, 3 for taking one of theirs | 3 for taking one of the Reach's |
| Their capital | ends the war at once | |

**The peace.**
- **The Reach wins**: they pay tribute for twenty years as before; what they still hold of the Reach's goes home;
  what the Reach took of theirs it keeps.
- **The Reach loses** (the chair buys peace), or the war gutters out after twelve quiet years: **what they hold of
  the Reach's is ceded**, and what the Reach took of theirs is given up. A ceded world can be taken back in the next
  war.

**Easy to take, impossible to hold.** A world of the Khanates' own held by a house of the Reach rises and is theirs
again after three years without a fleet of three hulls over it. While they pay tribute it keeps quiet.

**Their capital, and their end.** Taking the Great Camp during a war ends the war; the Khan sits at another world
afterward. A nation with no world left **is no more**: its fleets scatter, its people are gone, it raids nobody, and
no war can be voted on it.

**An older game.** A save from before this is brought forward at its next year: the Khanates are placed if their
systems are on the map and nobody holds them, and a fleet away on the old kind of expedition against them is called
home.

**On the screen.** The map shows the Steppe, its worlds in the Khanates' gold, and their two fleets. On the Military
screen the Khanates' card says "on the map" and shows where the Khan sits, what they hold, where each fleet is and
where it is bound, which worlds of the Reach they hold and whose, and which of their own are in the Reach's hands
and how many years those have gone without a garrison. Ten reports (`content/events/nations.json`): the host through
the march; a world of the Reach taken; your own world taken; a world freed for you; you freed a world; their host
beaten; ceded at the peace; the camps restless; the camps rise; their capital falls.

**Measured** over sixty unattended games of sixty years (six starts under the Republic, ten each).

| | |
|---|---|
| Wars with the Khanates | 46 |
| Won by the Reach, lost, still being fought at the end | 36, 8, 2 |
| Their host through the march | 63 times |
| Fleets of the Reach sent against them | 469 |
| Battles on the map | 169; the Reach won 67 |
| Worlds of the Reach they took | 25: 17 freed by a house of the Reach, 1 given back at a won peace, 7 ceded at a lost one; 3 in their hands at the end |
| Their own worlds taken by the Reach | 77; 43 rose again |
| Their capital taken; the nation ended | never, in 60 games |
| At the end, on average | 13.8 hulls and 2.5 worlds |
| The Republic | stood in all 60 |

Half of the lost wars were in one start (the Academy). The full account is docs/GAP_ANALYSIS.md, section 76.

(Measured again with the Free Cities on the map and the houses gathering at the march: see the Free Cities'
section below.)

**What this left out of stages 1 and 2.**
- **Their raids are still rolled, not sailed.** At peace a raid lands by the stage 0 rule; no fleet crosses the map.
- **The Khanates never begin a war.** Only the Senate does.
- **Their worlds make nothing.** Their house is kept from going hungry by rule; it has no economy.
- **No other house has taken the capital** in sixty games. A player can.
- **The houses that stand behind the leader in a battle take no losses and no share of the credit.**
- **The map does not mark a march or a border**, and the rings are drawn for the Reach alone.
- **What a house takes in the Steppe is held as a world of that house**, not as a province. That is stage 3.

## The Free Cities of Thessar on the map, as built

Built 2026-11-18 on the owner's word. The second nation on the map, and made to be unlike the first: the Cities
**would rather buy than fight**.

**Where.** A second region beyond the rim, the Thessaran Shore, south-east of the Pale Reaches. Port Meront is one
lane beyond both Wintermoor and Brackwater (the Cities' march). The map is 37 systems, 50 lanes and 12 regions.

| City | System | What it is |
|---|---|---|
| Port Meront | Port Meront | the nearest city, where the squadron lies |
| Pellas | Pellas | the refiners' city |
| Thessar | Thessar | the first city and the capital: the Speaker of the Cities sits here |

**What they have.** A house that is not of the Reach, as the Khanates have. A strength of 12: the Harbour Guard of
Thessar and the Thessaran Squadron, six hulls each. Hull for hull their fleets fight at four-fifths of a fleet of the
Reach's, the figure a mercantile nation had off the map. They are friendly (their regard starts at +10 and rises),
they do not raid, and they sell rare earths, exotics and materials into the Reach's exchanges while there is peace.

**How they differ from the Khanates.**

| | The Vashti Khanates | The Free Cities of Thessar |
|---|---|---|
| In a war their fleet | comes through the march and takes a world a season | stays at home, unless a warlike Speaker rules |
| Hulls lost | grow back two every five years | are replaced by hire: one a year while the war lasts |
| They sue for peace | when the Reach leads by 6 | when the Reach leads by 3 |
| A world of theirs the Reach took | is the taker's while a fleet stands over it | is bought back at the peace for 36 credits |
| Tribute | is paid for twenty years | is paid only while a fleet of the Reach is watching |
| A war nobody fights | gutters out after twelve years | is bought off: after four years if nobody presses them, after twelve if a fleet waits at their march |
| A world of theirs held without a fleet | rises in 3 years | shuts its gates in 6 |
| Beating their fleet; taking a world | pays 4 credits; 8 | pays 9 credits; 18 |

- **The ransom.** When the Cities lose a war, each city a house of the Reach holds is bought back: the house is paid
  and the city goes home. If the house is yours you are asked, by a report each year for two years. Take the money and
  the city goes home. Keep the city and the Cities think ten points the worse of the Reach, and the city needs a fleet
  over it. Say nothing and the offer lapses.
- **Tribute while watched.** They pay while a fleet of three hulls or more stands at Wintermoor or Brackwater, or over
  one of their cities. Three years unwatched and they stop: they are free again, and a war can be voted again. The
  house that holds the capital, which takes the tribute, posts a fleet at the march when it has one to spare.
- **A peace bought.** The Cities do not let a war lie. Four years running with no battle and nobody pressing them (no
  fleet of the Reach of three hulls at their march, over their cities, or on its way), and they pay the house that
  holds the capital four years' tribute (20 credits) to end it. A fleet at the march keeps the war alive; twelve
  years without a battle and they buy it off all the same. What each side took goes home, their cities for the
  ransom, and no tribute follows.
- **A war for plunder.** A house that wants a war and has under 40 credits may name the Cities when no nation has
  given the Reach a better reason, and a house short of money leans a point toward a war on a rich neighbour.

**On the screen.** The Galaxy screen lists the Shore under "beyond" and the whole map fits the panel whatever its
shape. The Cities' card on the Military screen says what kind of neighbour they are in a line, shows a ransom on
offer and until when, and says whether the tribute is watched. Eight reports: the Cities are hiring; the Cities
offer a ransom; a city is bought back; the Cities stop paying; the Cities buy their peace; the plunder of a city; the
city is restless; the city shuts its gates.

**Measured.** Left to themselves the houses of the Reach made no war on the Cities: none was moved in sixty
unattended games of sixty years. So the figures below are from sixty more games in which the Senate's war on the
Cities was made for it, in year 8 and every twenty years after (`FORCE_WAR=thessar:8 praetoria caesar`). The player
gives no orders in either set.

| | |
|---|---|
| Wars with the Cities | 124 |
| Won by the Reach; lost; bought off; still being fought at the end | 50; 10; 58; 6 |
| Battles | 193; the Reach won 67 |
| Their squadron through the march (a warlike Speaker) | 37 times |
| Worlds of the Reach they took | 17: 6 freed, 1 given back at a won peace, 8 ceded at a lost one |
| Cities taken by houses of the Reach | 49; 42 bought back, none rose, the rest were still held |
| Plunder taken | 1,485 credits |
| Hulls hired | 155 |
| Tribute stopped because nobody was watching | 34 times |
| Thessar taken; the Cities ended | never |
| The Republic | stood in all 60 |

In the sixty games left to themselves the Khanates were as before, and a little easier for the Reach now that its
houses gather before they attack: 37 wars, 28 won and 4 lost; 143 battles, of which the Reach won 65; 26 worlds of
the Reach taken, 19 freed and 4 ceded. The full account is docs/GAP_ANALYSIS.md, section 77.

**What this left out.**
- **No house but yours moves a war on the Cities.** See the figures above.
- **The ransom is answered through a report**, not from the card.
- **The Cities buy nothing else**: not a peace before any fighting, not a house's vote in the Senate, not the
  Khanates' swords against the Reach.
- **Their trade is the old kind**: goods arrive on the Reach's exchanges. No lane, no convoy, and nothing for the
  houses that hold Wintermoor and Brackwater.

## The Ardent Concord on the map, as built

Built 2026-11-18 on the owner's word. The third nation on the map, and the strong one: a unified state under a
council of admirals, with a fleet nearly as large as the other two nations' together.

**Where.** A third region beyond the rim, east of the Valmont Anvils. The Ardent Gate is one lane beyond both the
Valmont Anvils and Ironhold (the Concord's march) and is the only way in. The map is 41 systems, 56 lanes and 13
regions.

| World | System | What it is |
|---|---|---|
| The Gate Fortress | The Ardent Gate | a fortress, and the anchorage where its divisions lie at peace |
| The Brandt Yards | Brandt | a forge-world |
| The Marrek Foundries | Marrek | a forge-world |
| Concordia | Concordia | a fortress, and the capital: the First Admiral sits here |

**What it has.** A strength of 26: the Home Fleet of Concordia (8 hulls) and the Concord Line in two divisions of 9.
Its fleets fight hull for hull as the Reach's do. One commander: he leads the first division, and the second sails
without him.

**How it differs from the other two.**

| | The Khanates | The Free Cities | The Ardent Concord |
|---|---|---|---|
| Who begins a war | the Senate | the Senate | the Senate, **or the Concord** |
| In a war its fleet | one host, one world a season, then home | stays at home | **two divisions through two marches, that stay in the field** |
| What it takes | it holds until freed or the peace | (it takes nothing) | it holds until freed or the peace |
| A world of its own in the Reach's hands | rises in 3 years without a fleet | is bought back; shuts its gates in 6 | **does not rise: the Concord makes war to take it back** |
| It sues for peace | when the Reach leads by 6 | by 3 | by 10 |

- **It begins wars.** When the Reach is divided (the great war, a civil war, a state in arms against the Reach, or a
  standard raised against a throne) and the Concord thinks ill of it (a regard of -10 or worse), it makes war. One
  year in three while that holds.
- **It comes back for its own.** If a house of the Reach holds a world of the Concord's, it makes war to take it
  back, divided Reach or not, whatever it thinks of the Reach.
- **When it may not.** While it pays tribute; with under three-quarters of its strength; within twenty-five years of
  its last war (one invasion a generation); or while another war beyond the Reach is being fought.
- **A war it began is every house's to answer.** Every house, not only those that wanted a war, will go to drive its
  divisions out of the Reach. Carrying the war beyond the Gate is still for the war party, serving officers and the
  great offices of arms.
- **A campaign, not a raid.** A division that takes a world makes for the next, within three lanes of the Gate. It
  goes home when it is short of supply or is beaten. The two divisions do not make for the same world.
- **The peace is the same as the Khanates'**: if the Reach wins it pays tribute for twenty years and what it holds
  goes home; if the Reach loses, what it holds is ceded.

**On the screen.** The Galaxy screen lists the Concord under "beyond". Its card on the Military screen says in a line
that it begins wars and why, and lists all three fleets. Three reports: the Concord crosses the Gate; the Concord
comes back for its own; two fleets through the marches.

**Measured** over sixty unattended games of sixty years (six starts under the Republic, ten each), with all three
nations on the map.

| | The Ardent Concord | The Vashti Khanates | The Free Cities |
|---|---|---|---|
| Wars | 84 | 24 | 1 |
| Begun by the nation | 63 (4 to take back a world) | none | none |
| Won by the Reach; lost; still being fought | 41; 24; 19 | 9; 2; 13 | 0; 0; 1 |
| Battles; won by the Reach | 876; 383 | 59; 24 | 3; 1 |
| Worlds of the Reach taken | 150 | 10 | 1 |
| Freed by a house of the Reach | 95 | 8 | 1 |
| Ceded at a lost peace | 47 | 1 | 0 |
| In its hands at the end | 16 | 2 | 0 |
| Its own worlds taken | 58 | 21 (12 rose again) | 0 |
| Plunder taken from it | 2,994 credits | 264 credits | 9 credits |
| Its capital taken; the nation ended | never | never | never |

- **The Republic stood in all 60.**
- **The Concord is at war with the Reach about once or twice in a game**: one war begun by it, and the Senate votes
  one on it in one game in three. The Reach wins five in eight of those that are decided.
- **It costs the Reach worlds.** Two and a half are taken in a game, and nearly two-thirds of those are freed again.
  47 were ceded in sixty games, 20 of them in one start (the Fallen House, where the Reach lost nine wars of
  eighteen).
- **A game has about twice as many battles as before the Concord** (about 24, where it was 11), and the Reach is "in
  danger" in three years of five, where it was one in two. No civil war followed, and no general marched.
- **With the Concord taking the one war the Reach fights at a time**, the Khanates' wars fell from 37 to 24.

The full account is docs/GAP_ANALYSIS.md, section 78.

**What this left out.**
- **Nothing warns of a strike.** No report of its fleets gathering at the Gate, no envoy, no demand first.
- **Only the Reach's own divisions count.** A war with the Khanates, or the Reach's fleets being away, does not
  tempt it; and it cannot strike while another war beyond the Reach is being fought.
- **The second division has no commander of its own.**
- **It makes no treaty but tribute or cession**: no truce, no border agreed, no hostages.

## The Outer Compact and the Hollow Crown on the map, as built

Built 2026-11-19 on the owner's word. The last two nations, and the two that the Reach has no quarrel with: one a
hard nut a long way off, one a prize waiting to be taken.

### The Outer Compact

**Where.** South, past Sorrow (which no house holds) and **the Long Dark**: an empty system that nobody holds or can.
Every fleet bound for the Compact crosses it, two lanes from Sorrow to the nearest Compact world, with no supply on
the way.

| World | System | What it is |
|---|---|---|
| Pike's Landing | Pike's Landing | the nearest world, where the militia stands |
| The Wrenn Seams | Wrenn | isotope fields under the ice |
| The Mallow Fields | Mallow | the deepest fields |
| The Wardenry | The Wardenry | a fortress, and the capital: the Warden of the Compact is chosen here |

**What it has.** Eight hulls: the Warden's Guard and the Compact Militia, four each. It is friendly enough (a regard
of 0), does not raid, and sells isotopes and helium into the Reach's exchanges at peace.

- **It fights only when invaded.** Its fleets never come through the march, whoever rules.
- **And then well.** Hull for hull its militia fights at five-quarters of a fleet of the Reach's. Invaded, the guard
  leaves the capital to meet an intruder beside the militia, if the two together can beat it. Its worlds take two
  years longer to reduce.
- **What the Reach wants.** Each of its worlds gives the house that holds it **two isotopes a year**.
- **Held, not owned.** A world of the Compact's rises in four years without a fleet of three hulls over it. While
  the Compact pays tribute its worlds keep quiet.
- It sues for peace when the Reach leads by 5.

### The Hollow Crown

**Where.** South-west of the Ashen Verge. Chalke, the Bronze Gate, is one lane beyond both Last Harbour and Hollow
Reach (the Crown's march).

| World | System | What it is |
|---|---|---|
| The Bronze Gate | Chalke | the frontier fortress, where the Tagmata stands |
| The Estates of Melitene | Melitene | estates and mortgages |
| The Mint of Chrysopolis | Chrysopolis | the treasury city |
| The Purple Palace | Porphyra | the capital: the Autokrator sits here |

**What it has.** Ten hulls at the start: the Palace Guard of Porphyra and the Tagmata, five each. Unpaid, they fight
at seven-tenths of a fleet of the Reach's.

- **It decays.** Every ten years it strikes a hull from its lists, down to a remnant of four. It replaces none: what
  it loses in a war it has lost.
- **It does not come back.** A world it loses stays lost. It does not rise, and the Crown makes no war for it. So it
  is the one nation that can be taken apart world by world, and **ended**.
- **Its vaults.** Whoever first takes the Purple Palace takes 120 credits, eight points of standing and **the diadem
  of the Autokrators**, once. The diadem is a thing like a signet or a sword: the head of the house has it, and while
  a head of a house keeps it that house's standing mends a point a year up to 85. It can be sold, given or willed,
  and carried off in its turn by whoever storms that house's last world. This is besides the plunder every nation's
  worlds pay (16 credits a world from the Crown).
- **A war for plunder.** A house that wants a war and is short of money may name the Crown, as it may the Free
  Cities, when no nation has given the Reach a better reason.
- Its fleet stays at home unless a warlike Autokrator rules. It sues for peace when the Reach leads by 5.

### How the five differ

| | Khanates | Free Cities | Concord | Outer Compact | Hollow Crown |
|---|---|---|---|---|---|
| Strength | 14 | 12 | 26 | 8 | 10, falling to 4 |
| Fights, hull for hull | as the Reach | at four-fifths | as the Reach | at five-quarters | at seven-tenths |
| Begins a war | no | no | yes | no | no |
| Its fleet in a war | raids: one world, then home | stays home | campaigns on two lines | never leaves home | stays home |
| A world of its own, taken | rises in 3 years | bought back, or 6 | it makes war for it | rises in 4 years | stays taken |
| What taking one is worth | 8 credits | 18 credits | 12 credits | 10 credits, and 2 isotopes a year | 16 credits; the capital, 120 more and the diadem |

**On the screen.** The Galaxy screen lists five regions under "beyond". Each nation's card on the Military screen
says what kind of neighbour it is. Six reports: the Long Dark (a war voted on the Compact); the isotope fields; the
vaults of the Crown; the Crown is broken; another hull from the lists; the last of the Crown.

**Measured** (60 unattended games of 60 years each; the player gives no orders).

Left alone, **nobody makes war on either**. The Senate moved a war on the Compact, the Crown or the Free Cities in
none of the 60 games. The Crown fell to its remnant of four hulls in nearly every game (350 hulls struck off, of a
possible 360). So both are yours to go for, or to leave.

With a war forced on each in year 8 and every twenty years after (60 games each):

| | The Outer Compact | The Hollow Crown |
|---|---|---|
| Wars | 78 | 96 |
| Won by the Reach; lost; guttered out | 33; 18; 15 | 39; 13; 35 |
| Battles; won by the Reach | 290; 71 | 180; 91 |
| Its worlds taken; risen again | 40; 32 | 49; 0 |
| Its capital taken | never | never |
| Worlds of the Reach it took; freed; ceded | 0; 0; 0 | 23; 13; 10 |
| What the takers had of it | 1,376 isotopes, 755 credits | 1,512 credits; the vaults never opened |

- **The Compact** wins three battles in four in its own space. Its worlds are taken and lost again: four in five
  rose within the war or after it.
- **The Crown** loses half its battles and every world taken from it stays taken, but no house pressed on to
  Porphyra: more than a third of its wars guttered out with nobody fighting them. A warlike Autokrator sent the
  Tagmata through the march 39 times, and the Reach lost 13 of those wars and ceded 10 worlds.
- **No house but the player's has taken any nation's capital**, in any run. The Crown has never been ended
  unattended. That is decision 15.
- The Republic stood in all 180 games. The soak passes (33 games, saved and resumed, no failure).

**The other three, in the same 60 free games.** The Concord: 87 wars (71 begun by it), 29 won by the Reach, 29 lost,
55 worlds ceded. The Khanates: 24 wars, 8 won, 9 lost, 9 worlds ceded. The Free Cities: no war.

**The Khanates' figures moved, and no rule moved them.** In the three runs before this one the Reach lost 2 or 3 of
about 24 wars with the Khanates; in this one it lost 9. Nothing that touches the Khanates was changed, and no route
inside the Reach is shorter for the new lanes (checked pair by pair). Two more runs of 60 were made to look for a
cause: one without the rule that makes fleets gather longer before a fortress (8 won, 10 lost), and one with the
Compact and the Crown taken back off the map (12 won, 10 lost of 34). Both gave the same. So the earlier "four wars
in five" was one sample, and a larger map plays out differently. The honest figure for the Khanates is: the Reach
wins about as many wars as it loses, and cedes a world in about one war in three. That is decision 5.

**What this left out.**
- **The Compact's distance is two lanes and a supply rule.** Nothing is built in the Long Dark, and no depot can be.
- **The Crown's court is a ruler and a commander**, like every nation's. It has no factions, pretenders or
  provinces of its own, and nobody inherits its titles.
- **A nation that is ended leaves worlds held by houses**, with no law of the Senate over them (stage 3).

### Not in this plan

The threat from beyond the galaxy's edge (the design's late crisis), and anomalies and archaeology. Both want the map
to have an edge, which stage 2 gives it.

## What is yours to decide

Three nations were built on assumptions, as the owner asked ("then we can figure out the details"; "after we add the
nations you give me a list of things to decide"). This is that list. Each item says what is built now, what I would
do, and what turns on it. Each can be changed without rebuilding the rest.

**The map**

1. **All five nations are on the map** (decided by the owner, 2026-11-19). What remains of this question: the old
   kind of war, an expedition that leaves the map, is used by no shipped nation now. *I would keep it in the engine*
   for a map drawn without a nation's worlds, and take its button off the screens.
2. **Can the Reach lose worlds for good?** Built as yes: a world a nation holds at a lost peace is ceded, and can be
   taken back in the next war.
3. **Can the player's own house be conquered?** Built as yes: its worlds are taken like anyone's, a freed world
   comes home, and a house with no world left goes on landless.

**War**

4. **Should a nation begin a war?** Built: the Concord does (when the Reach is divided, or to take back its own);
   the Khanates and the Free Cities never do. *I would leave the Khanates as raiders*, but it is one switch in the
   content if you want them to strike too.
5. **How hard should each be, and how much war do you want?** The measured figures are in each nation's section. In
   short, from the last run of sixty games: against the Khanates the Reach wins about as many wars as it loses (it
   looked like four in five earlier; see the note in the last section); wars with the Free Cities are won or bought
   off; against the Concord it wins as many as it loses, and the lost ones cost it worlds; the Compact beats off
   more than half of what is sent at it; the Crown loses its worlds and keeps its capital. The Concord doubled the
   battles in a game. No house but the player's has ever taken a nation's capital. Each of these is a number or two in the
   content.
6. **Should any house but yours make war on the Free Cities?** Built: none does. They are friendly and do not raid,
   and a war for plunder is almost never moved. Say how often you want the war party to go looking for one.
7. **Should the Senate direct a war it votes?** Built: it votes and directs nothing. Houses go by their own lights.
   A command given to a person (your R1) would start here.

**What is taken**

8. **Who holds what is taken from a nation?** Built: the house whose fleet took it, while it keeps a fleet there. A
   province held for the Reach under an appointed governor is stage 3 of this plan.
9. **Is a ransom the right end for a city of the Free Cities?** Built: the Cities buy back what they lost, other
   houses always sell, and you may refuse.
10. **Should a nation's worlds make anything?** Built: only the Compact's, which give two isotopes a year each to the
    house that holds them. Otherwise no: a nation's house is kept from going hungry by rule, and its worlds pay
    nothing to whoever takes them beyond the plunder.

**Their people, and later**

11. **Should their rulers and commanders be people you can deal with?** Built: they command fleets and can be
    killed, and nothing else. Envoys, marriage, bribery, capture and refuge are stage 4.
12. **Should nations deal with each other?** Built: no. They never fight, ally or trade among themselves.

**From the last two**

13. **Should a nation be able to end?** Built: yes, when its last world is taken. In practice only the Hollow Crown
    can be, because it alone does not rise and does not come back. Say if the others should be endable too, or none.
14. **Is the Crown's treasure the right prize?** Built: 120 credits, eight points of standing and the diadem (which
    mends a house's standing while its head keeps it), once, to whoever first takes its capital. No title or claim
    comes with it: taking the Crown makes you rich, and heir to nothing.
15. **Should anyone but you go for the Compact or the Crown?** Built: as with the Free Cities, almost never. The
    measured figures are in their section.

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
