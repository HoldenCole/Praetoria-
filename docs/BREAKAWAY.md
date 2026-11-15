# Breaking away, and how the great war ends

Built 2026-11-14, first stage. The owner's direction: the great war should last ten years at the least and have several
ways to end; holding the capital means you are winning, not that you have won; a house that controls a region should
be able to set up a nation of its own, smaller than the Reach, as a republic, a republic under one hand or a kingdom;
it should be possible with the Reach's leave or against it, for as long as the house can fight; and most of all when a
rival Caesar has taken the throne and the house will not submit.

The principle behind it is in docs/PROCESS.md: **if it happened in history, the player should be able to do it.**

## States that have left the Reach

A **state** is a region, the houses in it that went, a form of government and a ruling house (`World.Polities`,
`House.PolityId`).

A house that belongs to one:

- has no seat in the Senate: no weight in votes, no motions, no party, no offices, no consulship, no ladder;
- is not in the Reach's wars: the great war and a general's march are no longer its business;
- cannot be voted a triumph or recalled, and cannot march on a capital that is no longer its own.

**The forms.** A republic (its head is First Citizen), a republic under one hand (Dictator), a kingdom (King or
Queen). Today the form is a name and a style. How each is governed is the next stage.

## Taking a region out

On the Senate screen, under **Your standing**, the panel **Breaking away** lists each region the house holds a world
in, with what it controls there, who would follow, and whether the Reach would fight.

To declare, a house must:

- still be of the Reach, and not hold the capital;
- pick a region that is not the capital's own and has not already left;
- hold **a quarter** of that region's worlds itself;
- hold **three in five** counting the houses there that would follow it.

**Who follows.** A client of the house; a house whose head thinks well of the house's head (25 or more); a house
that depends on it (docs/REPUBLIC.md, sway). Never the house that holds the capital.

On declaring, the house and those who follow leave the Senate, their offices and their parties, and any war they
were in ends.

## Working on the region's houses

The panel lists, for each region, the other houses that hold a world there, whether each would follow, and their
regard for you. Once a year for each you may send:

- **a quiet word** (2 influence): their regard rises by 8;
- **a gift** (12 credits from the house): their regard rises by 12, and by 4 more if they are poor.

- **a promise of a place** (2 influence): their regard rises by 15 at once, and you owe them. If the state is founded
  with them in it, its ruler pays them 3 credits a year for ten years. A ruler who cannot pay is thought 8 the less of
  each year, and there is an event;
- **a threat** (2 influence), if you have three hulls to their one: they will follow you for as long as they are
  afraid (ten years, and while you still have two hulls to their one), and think 20 the less of you.

At a regard of **25** they give their word, and it holds for ten years so long as they do not turn against you.
**A marriage between the two houses** makes it 10: one of yours wed to one of theirs, both living.

**The capital's creature tells the capital.** A house that thinks well of the capital's holder (30 or more) and little
of you (under 10) hears you out and reports it. Nothing is gained, the capital's holder thinks 15 the worse of you,
and the house loses 2 legitimacy. The house that holds the capital cannot be approached at all.

## Could it stand alone?

Three questions, answered on the panel for each region, each with what it does to the Reach's temper:

| Question | If the answer is no | If it is yes |
|---|---|---|
| **Fuel of its own:** does one of the houses that would go bring up helium-3? | Temper +1: it can be starved. While it fights for its independence the Reach's exchanges are closed to it | |
| **A treasury that pays its way:** do those houses together take in more than they spend, and are they out of debt? | Temper +1: it cannot pay for a war | |
| **Something the Reach cannot do without:** does half or more of the Reach's output of a strategic resource come from those houses? | | Helium-3: temper −2. Rare earths or isotopes: −1. And in the war the Reach's will falls every two years, not three |

So a region with a helium-3 monopoly and a fleet is very likely to be let go, as the owner's sketch asked; and one that
cannot fuel or pay for its own ships will be fought for.

## The Reach's temper

Whether the Reach fights for the region is a number, shown on the panel with its reasons. It starts at 2.

| It is | Temper |
|---|---|
| Weary: in the great war or a general's march, or within eight years of either ending | −2 |
| Facing fleets (the house and its followers) half as large as what it would send, or more | −1 |
| Facing fleets less than a quarter of what it would send | +1 |
| The region is on the rim | −1 |
| The region is in the core | +1 |
| Under a throne | The Emperor's own: see below |

- **At nought or below it lets the region go.** No fleet comes, and the state is recognised at once.
- **Above nought it is a war of independence.** The capital's holder and the three strongest houses of the Reach go
  to war with the house and its followers.

### Under a throne

The answer is the Emperor's own, not one number for every throne:

| | Will |
|---|---|
| New on the throne (eight years or less) | +2: cannot be seen to lose a province |
| An older throne | +1 |
| The Emperor is ruthless, cruel or proud | +1 |
| The Emperor is just or honourable | −1: would sooner treat than burn a region |
| The throne's house is not respected (legitimacy under 40) | −1: cannot spare the fleets from home |
| The Emperor thinks well of your house (30 or more) | −1 |
| The Emperor has no love for it (−30 or less) | +1 |

So an old, just Emperor who thinks well of you may let a rim region go; a new and ruthless one will not.

## A war of independence

It has no fixed end. You may declare with one ship and fight for as long as you can.

- **The state is recognised** when it has stood three years, its houses still hold more than half the region, and
  either it leads the war by 6 or **the Reach's will has run out**. The will is the temper at the declaration, less
  one for every three years of war. So a house that only holds on outlasts a temper of 2 in six years.
- **The state is brought back** when its houses hold less than a quarter of the region, or after two years it is
  behind by 8. The house that led it out loses 15 legitimacy and three tenths of its treasury and may not stand for
  consul for ten years. Those who followed lose 6 legitimacy. Nobody is put to death for it.

## A state as a neighbour

- **The treaty.** Recognition, however it came, signs a treaty for **ten years**. While it holds no house of the Reach
  may make war on a house of the state, nor the other way.
- **The Reach may come back.** When the treaty has lapsed and the Reach is rested (not weary), the Senate may vote a
  **Reconquest** (a motion, 3 influence, against the state's ruling house). The capital's holder moves it by itself
  when what the Reach would send is twice what it would meet and the state has nothing the Reach needs. Houses lean
  for it as the capital's holder, the Optimates and the Sword do; against it for a friend, the Factors and the
  Populares, the rim, and a state that still sells the Reach what it makes. Carried, it is a war of independence
  again on the same terms, and a state that holds on is recognised again with a new treaty.
- **Rejoining.** The ruling house may take the state back into the Reach. At peace it comes back whole and the
  capital thinks the better of it. In the middle of its war it comes back on the Reach's terms: a little legitimacy
  lost, no fine, no ban.
- **On the map.** The state's name is on its region on the galaxy map, in the region list and on the Worlds screen,
  with whether it is still fighting for it.

## Joining a state that exists

A state is not closed. A house of the Reach may go over to one that the Reach has recognised, in two ways. Either
way its ruler must be willing (regard of 0 or better for your house), and the house leaves the Senate, its offices and
its party as it would by declaring.

- **Go over as you stand.** The house must hold a world inside the state. It becomes a house of the state, under its
  ruler. The capital's holder thinks 20 the less of you. No war comes of one house leaving.
- **Take a region into it.** The region must border the state (a lane runs from one to the other) and the house must
  control it exactly as it would to declare it independent: a quarter of its worlds itself, three in five with those
  who would follow. The capital thinks 30 the less of you. Then the Reach's temper is weighed as for any region:
  - no will to fight, and **the treaty stands**: the state is two regions, and the maps are redrawn;
  - the will to fight, and **the Reach calls it a breach of the treaty**: the whole state is at war for its life
    again, on the same rules as a war of independence. There is an event for a house of that state.

**A state still fighting can be joined, as it stands.** A house that holds a world in it and goes over is at war
with the Reach from that day, on the state's side. If it had been one of those sent against the state, it is its enemy
no longer. A region cannot be taken into a state until the Reach has recognised it.

**A house can leave.** A member that is not the ruler may go back to the Reach, with its worlds: the ruler thinks 25
the less of it, the capital 10 the better. Members do it by themselves when they have come to think ill of their ruler
(−20 or worse), about one year in three, unless they are afraid to. If one leaves your state there is an event.

**A state reaches for its neighbours.** Its ruler may send a word or a gift to a house of the Reach that holds a world
in the state or in a region on its border. Such a house, once it thinks well enough of the ruler (30) and has no love
for the capital, comes over by itself: with its worlds, or (at 40, if it controls the region) with the whole region.

**Other houses do it too.** A house of the Reach that holds worlds inside a recognised state, thinks well of its ruler
(regard 30) and no better than nothing of the capital's holder, goes over of its own accord, about one year in four,
one house a year at the most. If the state is yours, you are told. The player's own house is never moved for them.

Where: the breakaway panel. "Go over to it" sits under each state in the list; "Take *region* into *state*" sits in a
region's block when a recognised state borders it.

## Refusing a throne

When a general has marched on the capital and won (docs/CAESAR.md), every house that declared for the Senate is
remembered as having opposed him. For three years:

- **the player** who opposed him and controls a region is told so (an alert and an event), and may take it out. The
  new throne has just come through a civil war, so it is weary;
- **another house** that opposed him and controls a region takes it out by itself, once.

## How the great war ends

### What sets it off

It no longer comes by the calendar. From its tenth year the Reach has a pressure toward a great war, shown on the panel
with its causes ("the peace of the Reach"). Each year the pressure is the sum of what is wrong:

| What is wrong | Pressure |
|---|---|
| Each of: more than one region lawless; the capital's holder not respected; a war beyond the Reach going badly; the Exchange crashed | 7 |
| Wars between houses | 3 each, to 12 |
| One house with twice the fleet of the next | 6 |
| Houses drawing together against one of their number; worlds taken by force | as counted |
| A general marching on the capital | 8 |
| A region has left the Reach in the last eight years | 4 |

- At 40 there are rumblings. At 150 the houses sort into two camps. At 300 it is war.
- **It can be aimed.** The same act can name one house: the five strongest of the others each think 8 the less of it.
  If the capital hears whose money it was, so does that house (−25).
- **A house can push it on purpose.** "Stir the camps" (3 influence, 12 credits): money and letters in the right
  places, and for three years "somebody's money is setting the great houses against one another" adds 6 to the
  pressure. One time in three the capital hears whose money it was: its holder thinks 15 the less of you and your house
  loses 3 legitimacy. It cannot be done once the war has come, or while the last one is remembered.
- **With nothing wrong, the pressure runs back** by 6 a year. If it falls well below where the camps formed, **the
  houses stand down** and the camps dissolve.
- So mending what is wrong (relief to a lawless region, a peace imposed on warring houses, a recognised capital) is
  how the great war is put off, and a Reach that is well kept may never have one.
- Once the war itself begins it runs on its own clock as before, and ends as below.

### How it ends

- **It lasts ten years at the least.** Its last stage cannot begin before the sixth year, and it cannot end before
  the tenth whatever the clock says.
- **The capital is not the war.** A side that holds the capital and has the larger fleet is winning. The other side
  fights on from where it stands.
- After the tenth year it ends when one of these is true, checked each year:

| Ending | When | What it leaves |
|---|---|---|
| **Crushed** | The losing side has a tenth of the winner's hulls or less, and the winner holds the capital | The victor holds the Reach: legitimacy, glory, rank. The Senate still sits. For five years that is a cause, if the victor wants to march (docs/CAESAR.md) |
| **Partition** | The winner is ahead by a quarter, and the losing side holds three in five of the worlds of a region that is not the capital's | That region leaves as a republic under the losing side's leading house there, recognised in the peace |
| **Exhaustion** | Sixteen years, or both sides together have half the hulls they began with | Nobody wins. The fleets go home |
| **Forced** | Twenty years | As exhaustion |

- **The player's house is never taken out of the Reach for it.** If your side is losing and you hold a region, you
  must say that you would take it as the price of peace (a button on the panel during the war). Otherwise the war
  goes on.
- Nobody is given a throne by it (docs/PROCESS.md: the Republic stands unless the player ends it).
- The Republic is shaken for fifteen years afterwards, and the Reach is weary for eight.

### After it: a second great war

The Reach does not have one great war and then none for ever. **Fifteen years after a great war ends it is something
people were told about.** Its clock, its camps and its verdict are cleared, there is an event, and what is wrong with
the Reach can bring another, from nothing, by the same rules. It is called the Second War of the Reach, then the
Third. Each finished war is kept on the record (when it began, how long it ran, how it ended). In a sixty-year saga
the first war usually ends too late for a second to be fought; in a longer one it can be.

## Events

Twelve, in `content/events/breakaway.json`: *They Come Back For It*, *Back In The Reach*, *They Told The Capital*, *A House Gives Its Word*, and *The Declaration*, *They Let You Go*, *A War Of Independence*,
*Independence*, *The State Is No More*, *You Need Not Kneel*, *A State Breaks Away*, *The Reach Is Partitioned*.

## For content

- World flags, each year: `independent`, `war_of_independence`, `breakaway_possible`, `may_refuse_the_throne`.
- For a year after: `declared_independence`, `let_go_by_the_reach`, `independence_won`, `independence_lost`,
  `a_state_breaks_away`, `the_reach_is_partitioned`.
- From the great war: `great_crisis:ended:crushed|partition|exhaustion|held`, `great_crisis:crushed`,
  `great_crisis:partition`, `great_crisis:victor`, `great_crisis:stalemate`, `great_crisis:player_holds_the_reach`.
- Counters: `polities_founded`, `polities_recognised`, `polities_dissolved`, `great_war_began`.

## How it is built

- `src/Core/Systems/PolitySystem.cs`; `src/Core/State/Polity.cs`.
- `WorkHouseCommand` (a word, a gift) and `SecessionCommand` (declare, seek a partition) in `src/Core/Commands/HouseholdCommands.cs`.
- `GreatCrisisSystem`: `Pressure`, `Nearness`, `Ending`, `Resolve`, the ten-year floor in `Tick`.
- `PolitySystem.InReach` is the one test of membership, read by the Senate, elections, the ladder and the Caesar road.
- `src/Web/Components/BreakawayPanel.razor`. `tests/BreakawayTests.cs` (8). `tools/browser/breakaway.js`.
- `praetoria caesar` now also reports how long the great war lasted, how it ended, and how many states broke away.

## Checked

- 602 tests, eight of them for this. They cover: what a house must control; a weary Reach letting a region go and
  the house leaving the Senate; a war of independence won by holding on and lost by losing the region; the great war
  refusing to end before its tenth year or on the capital alone; a partition; refusing a new throne; every one of the
  eight events; and sixty years on two seeds in which the Republic stands and every state that left is whole.
- The soak passes (eleven starts, three seeds, sixty years, save and load compared). Across its 33 runs seven
  states were founded (by the random player and by partitions), six were recognised and one was brought back.
- `praetoria caesar --scenario all --seeds 6`, 36 Republic runs with a passive player: the great war lasted 10 to 16
  years in every run and ended **crushed in 16, by exhaustion in 16 and by partition in 4**. The Republic stood in all
  36. Four states came out of partitions.
- **What sets it off, measured** (the same probe after the change): the great war broke out in 24 of 36 runs within
  sixty years, starting between year 29 and year 52; it did not come in 12; and in two the camps formed and stood down.
  Before the change it came in all 36 by the calendar.
- Three more tests cover the pressure and its easing, standing alone, and working on a region's houses (606 in all).
- In the browser on The Circling Heir the panel showed, for each of its two regions, the three standing-alone
  answers and the neighbouring houses with their regard, and a quiet word raised one of them.
- In the browser The Old Lion's house, which holds two of the three worlds of its region, declared a kingdom in the
  first year. The Reach sent four houses; three years on the kingdom had lost a world and was behind. No errors.

### Measured again, 2026-11-15

Sixty Republic runs of sixty years, a passive player: the Republic stood in all 60; the great war broke out in 36; it
was forgotten before the saga's end in 6 and no run reached a second; 11 states were recognised, all by partition; one
house went over to a state by itself. Joining, the second war and stirring are each proved by a scripted test.

## The next stage: forms of government (the owner's direction, not built)

Set by the owner on 2026-11-14 in answer to the sketch that stood here. It is planned work, held in docs/GAPS.md as
G1 to G7. Nothing below is built yet: the three forms a state can take today still differ only in name.

**One ladder, for every state.** The Reach is a state like any other and is governed by the same rules. A region that
breaks away is run as its own government by them. From the most authoritarian to the most democratic:

| Form | Who rules | How the ruler is made |
|---|---|---|
| **Imperium** | An emperor | The throne, taken or inherited |
| **Kingdom** | A king or queen | Inherited in the ruling house |
| **Dictatorship: one person** | A strongman | Seized, and held until death or overthrow |
| **Dictatorship: the military** | The fleets' commanders | A command that took the state, and rules through its officers |
| **Republic of the nobles** | A ruler chosen by the great houses among themselves | Elected by the houses |
| **Republic of the people** | A ruler the people choose | Elected directly |

**A state can change its form.**

- **Toward the democratic end it is easier.** Each step down the table asks less. Going from an imperium to a
  dictatorship counts: it is the more democratic of the two, and should be the easier move.
- **Toward the authoritarian end there are gates,** as there are for a Caesar: the one who would do it must have the
  strength, the standing and the moment, and the houses and the people weigh in.

**The people can revolt.** A further expansion, after the forms themselves: a state whose people are pushed far
enough rise against whoever rules it, whatever the form.

**What each form should feel like to rule and to live under** is the design still to do: who the ruler answers to,
what the ruler takes, what holds the houses together, and what a house that is not the ruler can do about it. The
earlier sketch (election in a republic, force in a dictatorship, legitimacy in a kingdom) is the starting point.

## Not in this stage

This is the first stage of a large piece. What it leaves is in docs/GAPS.md (B1 to B10), and in short:

- **How a state is governed.** The three forms differ only in name. A republic should have its own council, a
  dictatorship a term or a strongman, a kingdom a succession.
- **A state's border.** It has no meaning for trade, lanes or the
  Exchange.
- **A state's own dealings.** It makes no trade treaty or alliance, sends no envoys, and does not make war as a state.
- **A state does not make war as a state** to take a region: it grows only by houses coming over.
- **The older free march** (a rim house that renounces the capital) is still its own small rule and should become
  one of these.
- Standing alone does not weigh food, manpower or the goods of the market. A marriage binds a house only if it already exists: one cannot be offered as the price.
