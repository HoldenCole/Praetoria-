# The powers beyond the Reach, on the map: the plan

Written 2026-11-18 at the owner's asking ("let's work on the off-map stuff as well; at least a good plan for it";
docs/GAPS.md R10). **Stage 0 was built the same day** (see "Stage 0 as built"). Later that day the owner said: "let's
focus on adding in nations, then we can figure out the details. Add in the khanate first." **The Vashti Khanates are
on the map**: stages 1 and 2, for that one nation (see "The Vashti Khanates on the map, as built"). Then: "Add the
Free Cities of Thessar next." **The Free Cities are on the map** too, and are a different kind of neighbour (see "The
Free Cities of Thessar on the map, as built"). Then: "Add the Ardent Concord next." **The Ardent Concord is on the
map**, and is the strong one (see "The Ardent Concord on the map, as built"). Then: "Add the Outer Compact and the
Hollow Crown next." **All five nations are on the map** (see "The Outer Compact and the Hollow Crown on the map, as
built"). Then the owner answered the first seven decisions, on the map and on war, and they are **built** (see "Whose
war, and how it is fought"). Then the owner answered the four that building those raised (16 to 19), and those are
**built** too (see "Kinds of peace, how often they come, and whose fleet it is"), and then decision 21: a peace is
negotiated or won, by the war score (see "Negotiated or won"). Stages 3 and 4 are a plan. The older rules in docs/FOREIGN_NATIONS.md (expeditions that leave the map) are
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

## Whose war, and how it is fought

Built 2026-11-19 from the owner's answers to decisions 1 to 7. What was said is quoted in the list at the end; this is
what it became.

### No two games have the same neighbours

- **Each nation's strength is fixed when a game begins**, from that game's dice: between four-fifths and six-fifths
  of what is written for it. The Khanates are 11 to 17 hulls, the Concord 21 to 31.
- **A setting on the new-game screen, "The powers beyond"**: gentle (a fifth weaker), as written, or harsh (a quarter
  stronger). It multiplies with the dice. Luck stays in every battle whatever is chosen.
- This is one slider, and it moves strength only. More are wanted later (decision 5).

### A nation can attack: the great ruler

The Concord already began its own wars. Now any nation whose definition names a great title can, under such a ruler.

- **The Khanates are raiders until a Great Khan is raised.** A Khan of Khans, however he hates the Reach, begins no
  war: his host comes through the march when the Senate has voted one, takes a world, and goes home.
- **A Great Khan comes two ways.** One accession in eight raises one. And a warlike Khan who beats the Reach in a war
  is acclaimed.
- **What changes under him.** The khanates are one host: half as many hulls again, mustered every year. He begins
  wars himself, three years after he is raised and eight years after any war. His host stays in the field and takes
  world after world, two lanes deeper than a raid goes. Because he began it, every house answers, and any house may
  take worlds of the Steppe and keep them.
- **When he dies** his successor is a Khan of Khans again, unless another is raised. What the host took it still
  holds.
- **The Hollow Crown has the same door**: one accession in twelve gives it a Basileus, under whom it stops decaying,
  builds, and makes war for what it lost.
- **The Free Cities and the Outer Compact never begin a war**: one buys, the other never leaves home. That is one
  line of content each if it should change.
- A report warns when a great ruler is raised, another when he strikes, another when he dies.

### Houses raid; they conquer only when the nation attacked first

- **In a war the nation did not begin**, a house of the Reach other than yours **sacks** a world of the nation's and
  goes home. A sack takes a season (longer behind the Compact's walls), pays what taking the world would, counts two
  in the war where taking counts three, and leaves the world the nation's. It is not worth sacking again for six
  years.
- **In a war the nation began**, the same house takes the world and holds it.
- **Your house does as you order.** It takes and holds until you tell it to raid; the order is on every nation's
  card.
- A world of the Reach's that a nation holds is never sacked: it is freed.

### Whose war it is

The Senate votes a war and directs nothing: no fleet is ordered anywhere and nobody is given the command. Three
things follow from that, and all three are built.

1. **A house may make war on its own account.** No vote. Only that house is at war with the nation: nobody else's
   fleets fight, and the nation's come only for that house. It needs a fleet of three hulls, the nation must not be
   paying the Reach tribute, and there is still one war beyond the Reach at a time. Other houses do this too: a
   house of the war party, short of money, with a fleet and a world near the march, one year in eight.
2. **The Senate may vote peace**, with any nation the Reach or a house is at war with. It ends nothing by itself:
   those carrying the war (the house whose war it is, or any house with a fleet in the nation's space) are told and
   have a year to answer. If they come home the peace is made: whoever is ahead keeps what they hold, nobody pays
   tribute, nobody triumphs.
3. **A general may not listen.** You are asked by a report: come home, or fight on. Another house fights on when its
   general has a great name, or the war is its own and its head is ambitious, ruthless or proud; and only while the
   war is not being lost. Then it is that house's war alone. It costs the house 6 standing at once and 2 every year.
   The Senate may vote to recall its general whatever his name is worth, and the chair and the consuls move it. A
   recall refused is the road the Republic already knows (docs/CAESAR.md). The house comes home when it is losing,
   when it has nothing left to fight with, after six years, or when you say so.

The Senate may also make a house's own war the Reach's, by voting war on that nation.

**How the benches lean on a peace.** The Factors for, the Sword against. One house's own war is nobody else's
quarrel. A war going badly or a long one is worth ending. Nobody moves to cede worlds to an invader until the war is
ten years old. A house whose world the nation holds votes for a peace that brings it home and against one that cedes
it.

### On the map

- **A conquered world is marked**: a broken ring in the holder's colour, on the Galaxy screen and in its legend. It
  is drawn for a world of the Reach's that a nation has taken or been ceded, and for a world of a nation's that a
  house holds. The world's card says which, and whether freeing it gives it back.
- **A house whose lands are all conquered is not conquered.** It goes on while one of it lives, with its fleets, its
  purse and its seat. This was so already; it is tested now.
- **The map is as it was.** The owner wants a far larger one later, filling the screen and scrolled across, for the
  day the nations are conquered in earnest. That is N20 in docs/GAPS.md, and is not begun.

### Measured

60 unattended games of 60 years each; the player gives no orders, so every figure is what the other houses and the
nations do with these rules.

| | Concord | Khanates | Hollow Crown | Free Cities | Compact |
|---|---|---|---|---|---|
| Wars | 95 | 51 | 19 | 4 | 0 |
| Begun by the nation | 73 | 18 | 8 | 0 | 0 |
| Made by a house on its own account | 0 | 3 | 11 | 4 | 0 |
| Won by the Reach; lost | 26; 23 | 15; 8 | 5; 2 | 0; 0 | |
| Ended by a peace the Senate voted | 26 | 16 | 9 | 2 | |
| Great rulers raised | | 24 (3 acclaimed) | 9 | | |
| Its worlds sacked; taken | 8; 43 | 37; 14 | 8; 1 | 0; 0 | |
| Worlds of the Reach ceded to it | 76 | 12 | 5 | 0 | |

- **A Great Khan was raised in about one game in three**, and began 18 wars. The Reach lost 8 wars to the Khanates
  and ceded 12 worlds, five of them still theirs at the end.
- **Houses raid.** 45 worlds of the Khanates and the Crown were sacked. The 57 that were taken were nearly all taken
  in wars the nation began (the Concord's, and a Great Khan's).
- **Houses make their own wars**: 18 in sixty games, mostly on the Hollow Crown, which is rich and weak. The four on
  the Free Cities came to nothing: two were bought off and two stopped by the Senate before a fleet arrived.
- **The Senate voted peace 54 times and the peace was made 53 times.** One general did not listen, and came home.
- **The cost of the peace vote**: worlds ceded to the Concord rose from 55 to 76. The Senate now ends some wars it
  is losing, and whoever is ahead keeps what they hold. That is decision 18.
- **No house goes for the Compact**, and no house has taken any nation's capital.
- About 22 battles a game (24 before: raids fight less than sieges). The Reach is "in danger" in three years in
  five, as before. The Republic stood in all 60.

With a war voted on the Khanates every twenty years (60 games): 119 wars; 84 of their worlds sacked and 41 taken;
the Reach won 35 and lost 21, and 49 ended in a voted peace; 16 warlike Khans were acclaimed after winning, which is
decision 16. The same on the Hollow Crown: 128 wars; 52 sacked, 20 taken; won 29, lost 13, 67 ended in a voted
peace; its capital was never taken.

**What this left out.**
- **Raiding is one order for all your fleets**, not one fleet's stance.
- **One war beyond the Reach at a time** still: a house's own raid holds the place the Senate's war or a nation's
  strike would have.
- **A voted peace has no terms.** Whoever is ahead keeps what they hold. Nobody negotiates a world, a sum or a
  hostage.
- **Defiance costs standing and risks a recall.** It is not a crime anyone is tried for, and it does not make a
  house an enemy of the Reach.
- **A nation does not tell one house from another** when it answers: in the Reach's war it comes for the nearest
  world, whoever raided it.

## Kinds of peace, how often they come, and whose fleet it is

Built 2026-11-19 from the owner's answers to decisions 16 to 19, which are quoted in the list at the end.

### How often they attack

- **A second setting on the new-game screen, "How often they attack"**: seldom, as written, or often. It is the
  nations' aggressiveness, apart from their strength.
- **And each game's own luck.** Every nation's boldness is fixed when a game begins, between seven-tenths and
  seven-fifths of what the setting gives. So a Great Khan is likelier in one game than the next: written as one
  accession in eight, it is anywhere from one in six to one in eleven, and with the setting at its ends one in five
  or one in thirteen. A nation's card says what it is in this game.
- Boldness moves three things: the odds of a great ruler, the rest a nation takes between its wars (the Concord's
  twenty-five years, a great ruler's eight), and how often the raiders raid.
- **The Free Cities and the Outer Compact can attack now, and seldom do.** The Cities may put every hired hull under
  a **Navarch** (one accession in twenty); the Compact may choose a **High Warden** (one in twenty-four), who leads
  out the fleets that never left home. Under either the nation does what a Great Khan's does: musters half as many
  hulls again, begins its own wars, and stays in the field. A Navarch does not buy his peace.

### What kind of peace it is

A war with a nation ends one of six ways. Which one depends on how it ended and what each side holds.

| The war ends | What each side holds | The peace |
|---|---|---|
| They sue (the Reach is ahead by what makes them sue) | They are beaten decisively, and the house that won it wants a client | **A client.** They keep their ruler and every world of their own. They pay the chair every year and half as much again to their patron, the house whose fleets won it. No end date |
| They sue | Otherwise | **Tribute**, twenty years to the chair. Houses keep what they took: **territory** |
| The Reach is beaten | They hold worlds of the Reach's | **Ceded.** They keep them, and the chair buys the peace once |
| The Reach is beaten | They hold nothing | **The Reach pays tribute**, ten years, from the chair. While it is paid they neither raid nor strike |
| The Senate votes peace, or the war gutters out | Two or more ahead on the tally | Whoever is ahead keeps what they hold. Nobody pays |
| The Senate votes peace, or the war gutters out | Closer than two | **A white peace.** Every world goes back to whoever had it before the war. Nobody pays |

- **Decisively** means: their capital is in the Reach's hands, or the tally is twice what makes them sue, or three
  in four of their hulls are gone.
- **A client is your choice when the war is yours.** Other houses always make one when they can. Your house keeps
  what it took unless you have said you would make clients; the order is on every nation's card.
- **A client can break away.** One of the great throws the treaty off at once. A warlike ruler with the fleets
  rebuilt does so one year in twelve, or one in three while the Reach is divided. A mild or wary ruler keeps it.
- **A nation's card says what a peace would be this year**: voted now, and fought to the end either way.

### Whose fleet it is

What defying the Senate costs depends on who commands for the house. It is read off the Caesar road
(docs/CAESAR.md): a commander's sway is his battles won, the crews that follow him, his triumphs and his rank.

| The commander | Sway | Standing lost at once; a year | His crews | The benches, on recalling him |
|---|---|---|---|---|
| Nobody the Senate has heard of | under 25 | 8; 3 | waver: 12 loyalty at once, 6 a year | lean hard for it (+4) |
| A known commander | 25 | 6; 2 | waver less | lean for it (+2) |
| A name the Senate knows | 45 | 4; 1 | waver a little | do not lean |
| A great name | 65 | 2; 0 | hold | lean against it (-2) |

- **Skill counts a step.** A commander with seven or more in tactics is treated one row better than his name.
- **A house with nobody of its own in command** is on the top row.
- **The chair and the consuls move a recall** only against the top two rows. A name gives them pause.
- **Crews devoted to their commander do not waver**, whoever he is.
- Other houses refuse a peace when their commander is on the bottom two rows; or when the war is their own and he is
  at least known, or their head is ambitious, ruthless or proud.
- The "Fight on" order on a nation's card shows the two figures and which row your commander is on, before you
  choose.

### Measured

60 unattended games of 60 years; the player gives no orders.

| | Concord | Khanates | Hollow Crown | Free Cities | Compact |
|---|---|---|---|---|---|
| Wars | 75 | 35 | 23 | 8 | 4 |
| Begun by the nation | 63 | 6 | 5 | 3 | 4 |
| Great rulers raised | | 14 | 11 | 6 | 6 |
| Peace: tribute to the Reach | 19 | 9 | 5 | 0 | 0 |
| Peace: the Reach kept worlds | 9 | 3 | 4 | 0 | 1 |
| Peace: white | 4 | 8 | 7 | 4 | 1 |
| Peace: worlds ceded | 26 | 7 | 3 | 0 | 2 |
| Peace: the Reach paid tribute | 0 | 1 | 0 | 0 | 0 |
| Made a client | 0 | 0 | 1 | 0 | 0 |
| Worlds of the Reach ceded to it | 38 | 5 | 3 | 0 | 0 |

- **The Free Cities attacked in three games and the Compact in four**, of sixty: a Navarch or a High Warden came in
  about one game in ten each.
- **A Great Khan came in fewer games than before** (14 against 24): that is the luck, which now differs by game.
- **Worlds ceded to the Concord halved** (38 against 76). A peace voted with the tally close is white now, and gives
  nothing up.
- **A client was made once**, and the Reach paid tribute once. Other houses raid and go home, so they are rarely
  decisive, and a nation that wins usually holds something to keep. Both kinds are mostly yours to bring about.
- **Three generals did not listen** (one before).
- About 22 battles a game, and the Reach "in danger" in three years in five, as before. The Republic stood in all
  sixty. The soak passes and the survival check lost none of 80.
- **More war makes more Caesars.** The Senate voted 25 recalls (8 before) and two civil wars began, both put down:
  the Republic fell in none. The same sixty games with every nation at its written boldness gave 11 recalls and no
  civil war, so the rise is the per-game luck: a game with bold neighbours has more battles, more commanders with
  names, and more of them recalled. Two in sixty is inside the rule (the Republic stands unless you end it; another
  house's Caesar at most once in ten games). The setting "How often they attack" moves it either way.

**What this left out.**
- **Nobody negotiates.** The kind of peace is read off the tally and the map. No world is asked for by name, no sum
  is haggled, no hostage or marriage seals it.
- **A client owes money and nothing else.** It sends no fleet when the Reach is at war, and its ruler is nobody its
  patron can call on.
- **The Reach is never the client.** A house cannot be made a nation's vassal, and the Reach's tribute ends in ten
  years.
- **Two settings.** How well the nations fight, and how much the other houses help, are not settings.

## Negotiated or won

Built 2026-11-19 on the owner's answer to decision 21: "Peace can be negotiated or won. Depends on war score."

### The war score

Every war with a nation has a score from -100 to +100. It is how far the tally stands toward the lead that ends the
war: +100 when the Reach is ahead by what makes them sue, -100 when it is behind by what beats it. A nation's card
shows it, with the tally it comes from.

### Won

At **+100** the peace is won, and dictated: tribute for twenty years, the houses keep what they took, or the nation
is made a client if it is beaten decisively. At **-100** the war is lost: what they hold is ceded, or the Reach pays
them for ten years. These are the kinds of peace built before, unchanged.

### Negotiated

Anywhere between, a peace can be negotiated, and the terms that can be had step up with the score.

| War score | Terms the nation agrees to |
|---|---|
| +60 or more | They pay tribute for ten years, and the Reach keeps what it holds |
| +25 or more | The Reach keeps what it holds, and has its own back |
| -24 to +24 | A white peace: everything goes home |
| -25 or less | They keep what they hold |
| -60 or less | They keep what they hold, and are paid tribute for five years |

- **A stubborn ruler asks more.** A warlike ruler wants fifteen points more for the same terms; one of the great,
  twenty-five. So a Great Khan at +30 will give a white peace and no more.
- **The Senate's peace is negotiated.** When it votes peace and the fleets come home, the Reach gets the best terms
  the score allows that year.
- **A house in a war of its own makes its own peace.** No vote: it was never the Senate's war. You choose the terms
  from a nation's card, and they agree or will not hear of it; the card says what score each wants. If they pay
  tribute they pay **your house**, not the chair. This also ends a war you fought on against a voted peace.
- **Other houses do the same** in their own wars: at once when tribute is on the table or the war is going against
  them, and after four years on whatever the score allows.
- **A house that wins its own war outright** is the one that is paid the twenty years' tribute.
- **A war that gutters out is not negotiated.** Each side keeps what the score gives it, and nobody pays.
- The Senate's benches move a peace once tribute is on the table and the war is three years old: they would pay
  now, so why fight on. The war party still votes against.

### Measured

60 unattended games of 60 years; the player gives no orders.

| | Concord | Khanates | Hollow Crown | Free Cities | Compact |
|---|---|---|---|---|---|
| Wars | 86 | 45 | 26 | 11 | 5 |
| Won outright by the Reach; lost outright | 22; 14 | 13; 6 | 1; 1 | 1; 0 | 2; 0 |
| Peaces negotiated | 33 | 19 | 21 | 7 | 2 |
| of them, by a house for itself | 0 | 10 | 19 | 7 | 0 |
| Negotiated or won: tribute to the Reach | 20 | 12 | 3 | 1 | 0 |
| the Reach kept worlds | 8 | 8 | 2 | 0 | 3 |
| white | 8 | 5 | 15 | 7 | 0 |
| worlds ceded | 25 | 13 | 3 | 0 | 0 |
| the Reach paid tribute | 8 | 0 | 0 | 0 | 1 |

- **About half of all wars now end at a table.** 82 peaces were negotiated in sixty games against 39 won outright
  and 21 lost outright.
- **Houses end their own wars themselves**: 36 times. Most of those were white. A house that raids the Hollow Crown
  alone usually cannot get past its gate, waits four years, and goes home with nothing: 21 such wars, 4 worlds
  sacked.
- **The Senate pays to get out of a war it is losing badly.** Eight times it negotiated at -60 or worse against the
  Concord, ceding what was held and paying tribute for five years, where fighting on to -100 would have cost as much
  or more.
- Battles, danger, recalls and civil wars are as in the run before (about 22 a game; three years in five; 20
  recalls voted; two civil wars, both put down). The Republic stood in all sixty. The soak passes and the
  survival check lost none of 80.

With a war voted on the Khanates every twenty years (60 games): 118 wars; 33 won outright, 16 lost outright, 56
negotiated. Of all their peaces, 37 had them paying tribute, 23 had the Reach paying it, 26 ceded worlds and 14 were
white.

**What this left out.**
- **The terms are a ladder, not a table.** Nobody asks for one named world, a sum of their own choosing, a hostage
  or a marriage; and the nation never opens the talking.
- **You negotiate only your own war.** In the Reach's war the Senate's peace takes the best terms on offer; you
  cannot ask it to hold out for more, or settle for less.

## What is yours to decide

Three nations were built on assumptions, as the owner asked ("then we can figure out the details"; "after we add the
nations you give me a list of things to decide"). This is that list. Each item says what is built now, what I would
do, and what turns on it. Each can be changed without rebuilding the rest.

**The map**

1. **The map.** *Answered 2026-11-19:* "for now we can leave the other nations off screen and their interactions
   will be done, but eventually we need to expand the map to make it very large ... where it takes up the entire
   screen and you have to scroll around to see everything. Because eventually you are going to want to conquer the
   other nations and that would show up on the map." **Kept as built**: the five nations stay where they are and
   work. The large scrolled map is N20, not begun. (I read "off screen" as "no more map work now"; if you meant the
   nations should come off the map again, say so and it is a content change.)
2. **Can the Reach lose worlds?** *Answered:* "Yes ... and those would show up as conquered on the map and then you
   can conquer them again just like any kind of grand strategy game." **Built**: lost and retaken as before, and now
   marked on the map.
3. **Can your house be conquered?** *Answered:* "the lands and assets your house controls can be conquered, but your
   house itself is not conquered because it is still alive as long as a member is still alive. And then of course
   you can control things outside of the conquered area." **Built as that, and tested.**

**War**

4. **Should a nation begin a war?** *Answered:* "other nations should be able to attack and I think the khanates
   should start as raiders and then they can try to take over just like if they get some sort of great khan."
   **Built**: the great ruler (above). The Khanates and the Hollow Crown can have one; the Concord strikes as
   before; the Free Cities and the Compact never do.
5. **How hard should each be?** *Answered:* "a bit of RNG because it'll be different each game, but we eventually
   do need some difficulty sliders in there. And just like real war, even if you have it on easy or hard, there
   were still a bit of randomness and luck to it." **Built**: each nation's strength varies by the game, and one
   setting scales them. More sliders are not built (N24).
6. **Should other houses make war on a nation?** *Answered:* "other houses can declare war but more like raiding
   wars, and they would only conquer something if an opposing nation attacks them first and start a war." **Built**:
   houses make war on their own account and sack; they take worlds only in a war the nation began.
7. **Should the Senate direct a war?** *Answered:* "The Senate should not direct a war. They can vote on it and they
   can even vote to try to have peace, but then a general or somebody can choose not to listen to them, but that
   runs the risk of going against the republic." **Built**: the peace vote, the answer, and what not listening
   costs.

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

**From building your answers** (each is a number in the content or one rule)

16. **How often should a Great Khan come?** *Answered 2026-11-19:* "the great khan showing up potentially every three
    games or so is fine. There could probably be some randomness to it." **Built**: the odds differ by the game.
17. **Should the Free Cities or the Outer Compact ever attack?** *Answered:* "I do think that the free cities or the
    compact should attack, but those are less likely to attack. There should also be a slider kind of like how
    Stellaris has it or it talks about AI aggressiveness." **Built**: a Navarch and a High Warden, at long odds; and
    the setting "How often they attack".
18. **What should a voted peace cost?** *Answered:* "there are different ways for peace to be done ... where you pay
    tribute or it is where you keep the territory or even where they become a vassal state. It really depends on the
    war and what is achieved in the war." **Built**: six kinds of peace, by how the war ended and what each side
    holds.
19. **How much should defying the Senate cost?** *Answered:* "if you are a well-known general and you are very good,
    then you probably have more leeway to defy the Senate and they are less likely to recall you. If you are not
    well known in general, then it probably cost you more and your chance of success is a lot lower. Your attributes
    and your fame will impact how much the Senate pushes you on things." **Built**: the table under "Whose fleet it
    is".

**From building those**

20. **Whose client is a client?** Built: the Reach's, with the house that won the war as its patron, paid half as
    much again. Other houses always make a client when they can; yours does when you say so.
21. **Should a peace be negotiated?** *Answered 2026-11-19:* "Peace can be negotiated or won. Depends on war score."
    **Built**: the war score, terms that step up with it, and a house's own peace in its own war.

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
