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

## The Reach's temper

Whether the Reach fights for the region is a number, shown on the panel with its reasons. It starts at 2.

| It is | Temper |
|---|---|
| Weary: in the great war or a general's march, or within eight years of either ending | −2 |
| Facing fleets (the house and its followers) half as large as what it would send, or more | −1 |
| Facing fleets less than a quarter of what it would send | +1 |
| The region is on the rim | −1 |
| The region is in the core | +1 |
| Under a throne | +1 |

- **At nought or below it lets the region go.** No fleet comes, and the state is recognised at once.
- **Above nought it is a war of independence.** The capital's holder and the three strongest houses of the Reach go
  to war with the house and its followers.

## A war of independence

It has no fixed end. You may declare with one ship and fight for as long as you can.

- **The state is recognised** when it has stood three years, its houses still hold more than half the region, and
  either it leads the war by 6 or **the Reach's will has run out**. The will is the temper at the declaration, less
  one for every three years of war. So a house that only holds on outlasts a temper of 2 in six years.
- **The state is brought back** when its houses hold less than a quarter of the region, or after two years it is
  behind by 8. The house that led it out loses 15 legitimacy and three tenths of its treasury and may not stand for
  consul for ten years. Those who followed lose 6 legitimacy. Nobody is put to death for it.

## Refusing a throne

When a general has marched on the capital and won (docs/CAESAR.md), every house that declared for the Senate is
remembered as having opposed him. For three years:

- **the player** who opposed him and controls a region is told so (an alert and an event), and may take it out. The
  new throne has just come through a civil war, so it is weary;
- **another house** that opposed him and controls a region takes it out by itself, once.

## How the great war ends

The War of the Reach is still set off by its clock. How it starts is to be looked at again. How it ends has changed.

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

## Events

Eight, in `content/events/breakaway.json`: *The Declaration*, *They Let You Go*, *A War Of Independence*,
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
- `SecessionCommand` (declare, seek a partition) in `src/Core/Commands/HouseholdCommands.cs`.
- `GreatCrisisSystem`: `Ending`, `Resolve`, the ten-year floor in `Tick`.
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
- In the browser The Old Lion's house, which holds two of the three worlds of its region, declared a kingdom in the
  first year. The Reach sent four houses; three years on the kingdom had lost a world and was behind. No errors.

## Not in this stage

This is the first stage of a large piece. What it leaves:

- **How a state is governed.** The three forms differ only in name. A republic should have its own council, a
  dictatorship a term or a strongman, a kingdom a succession.
- **A state on the map.** Its region is not drawn differently, and its border has no meaning for trade, lanes or the
  Exchange.
- **A state as a neighbour.** It makes no treaties, has no war or peace with the Reach as a state after it is
  recognised, sends no envoys, and cannot be reconquered or rejoin.
- **More than one region,** and regions joining a state that already exists.
- **Standing alone as a test.** The owner's sketch of secession asks for an economy that feeds itself or has
  something the Reach cannot do without (a helium-3 monopoly) and a fleet to match. Today only the fleet and the
  region are weighed.
- **Persuading the region.** You cannot yet work on the houses of a region to bring them over; they follow or they
  do not.
- **The older free march** (a rim house that renounces the capital) is still its own small rule and should become
  one of these.
- **What triggers the great war** is unchanged, and is to be looked at again.
- Under a throne the Reach's temper is one number. A new Caesar should have his own.
