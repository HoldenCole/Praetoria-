# The gap register

One list of everything in the game that is thin: built, but not deep enough to stand on. It exists so that thin things
do not pile up unseen. The rule for keeping it is in docs/PROCESS.md ("Map the gaps as you build").

- **S** is small: one change, hours. **M** is a cycle (build, test, measure, document). **L** is several cycles and
  needs a plan first.
- A gap is closed when it is built, tested and measured, and it then moves to the bottom table with the date.
- `tools/gaps.sh` counts what is open. The count is printed in docs/ROADMAP.md, section 1.

Last updated 2026-11-14 (second pass).

## The same gap in many places

These recur across systems. Closing one properly closes several lines below, so they are worth more than they look.

| Id | Gap | Where it shows | Size |
|---|---|---|---|
| X1 | **Only the player's house has an inner life.** Other houses have no purses, records, renown, households, ambitions or courtships of their own; their people are chosen by rules. | Marriage, household, wealth, ambitions, a life, the ladder, the Caesar road | L |
| X2 | **Other houses do not use what the player can.** They do not canvass, back candidates, give games, work on a region, declare independence by choice, or pay donatives. | The ladder, the Caesar road, breaking away | M |
| X3 | **Numbers are first guesses.** Every recent system says so. Each needs a probe and a tuning pass, as the Caesar road and the great war now have. | All of them | M each |
| X4 | **Events that need a rare player choice never fire in the soak.** They are proved by scripted tests, not seen in play. | Most event files since courtship | S each, ongoing |

## Breaking away and the great war (docs/BREAKAWAY.md)

| Id | Gap | Size | Leans on |
|---|---|---|---|
| B1 | **The three forms of state differ only in name.** A republic needs its own council, a dictatorship a term or a strongman, a kingdom a succession. A sketch is in docs/BREAKAWAY.md ("The next stage") and waits on the owner. | L | Politics under a throne |
| B2 | A state's border means nothing for trade, the lanes or the Exchange. Its region is named on the map but not drawn as a different country. | M | Nations on the map |
| B3 | A state makes no agreements of its own: no trade treaty, no alliance, no envoys. It does not make war as a state. | M | B1 |
| B4 | A state is one region. Regions and houses cannot join one that exists. | S | B3 |
| B5 | The older free march (a rim house renouncing the capital) and free holds (a rebel world) are their own rules and should become breakaway states. | M | |
| B6 | Under a throne the Reach's temper is one number. A new Caesar should have his own. | S | Politics under a throne |
| B7 | Standing alone weighs fuel, the treasury and strategic monopolies. It does not weigh food, manpower or the goods of the market. | M | The economy |
| B8 | Working on a house is a word or a gift. No promise of a place in the new state, no marriage to bind it, no threat. | S | B1 |
| B9 | Only one great war in a saga. After it ends, pressure builds toward nothing. | S | |
| B10 | The player cannot deliberately raise the pressure toward a great war, only lower it by mending what is wrong. | S | |

## The Caesar road (docs/CAESAR.md)

A rival Caesar is rare on purpose (docs/PROCESS.md). Measured: a general passed all three gates in 1 of 60 runs.

| Id | Gap | Size | Leans on |
|---|---|---|---|
| C2 | A general does not defect with his crews, and crews do not follow a dead commander's heir. | M | |
| C3 | The parties do not act as parties in the civil war. | S | |
| C4 | No proscription, amnesty or negotiated end. | M | |
| C5 | The Senate's sword gets no command of the state's fleets. | S | |
| C6 | A relative who is the general cannot cross against the head's wishes. | S | The one-person rule |
| C7 | The player cannot make the times, only meet them. | M | B10 |
| C8 | Nothing here under a throne: no triumph by the throne's gift, no guard. | L | Politics under a throne |

## The ladder of offices (docs/LADDER.md)

| Id | Gap | Size | Leans on |
|---|---|---|---|
| L1 | **A magistrate has no work to do.** No cases for a praetor, no accounts for a quaestor, nothing for an aedile but games. | M | |
| L2 | No bribery, no prosecution of a former magistrate. | M | L1 |
| L3 | No censorship, tribunate, or province after office. | L | The state and commerce |
| L4 | A relative who wins asks nothing of the head; one who loses does not blame the head. | S | |

## A life of one's own (docs/LIFE.md)

| Id | Gap | Size | Leans on |
|---|---|---|---|
| F1 | No ill fame: nothing a person does is held against them as a person. | M | |
| F2 | Old friends do nothing: they do not help in the Senate or a war. | S | |
| F3 | No duels, and no wound but in battle. | M | |
| F4 | You cannot send a physician to a relative. | S | |
| F5 | The elder teaches the new head and otherwise does nothing. | S | |

## Ambitions, wealth, education, the household, marriage

| Id | Gap | Size | Leans on |
|---|---|---|---|
| A1 | Two relatives who want the same thing do not quarrel over it. | S | |
| A2 | Restoring the house from an allowance is too slow to matter. | S | |
| A3 | A relative's failure (a venture, a command) is not marked. | S | |
| A4 | An ambition is set by the dice at sixteen, not by an upbringing. | S | E3 |
| W1 | A purse holds credits only: no shares, ventures or worlds held personally. | M | |
| W2 | Dowries come from the treasury, not a purse. | S | |
| W3 | Nothing is entailed. | S | |
| W4 | You cannot lend to a relative or give a sum beyond the allowance. | S | |
| W5 | Heirlooms do nothing but be worth something. | S | |
| E1 | Only your own character goes to the Academy. | M | |
| E2 | You cannot ask a relative to study, or one to teach another. | S | |
| E3 | No nature is formed by an upbringing; no trait is lost. | M | |
| E4 | A battle adds a step to its commander without regard to the ceiling. | S | |
| E5 | Authored characters sit above their ceilings. | S | |
| H1 | Kin do not find mentors for themselves. | S | |
| H2 | Wards are not hostages. | M | |
| H3 | Keeping a household costs nothing. | S | |
| H4 | A steward does routine only: no Exchange, tithe or ventures. | M | |
| M1 | Stewards do not court; other houses never divorce. | S | X1 |
| M2 | No children outside marriage. | M | |
| M3 | A spouse does nothing: no counsel, no running the house in your absence. | M | |
| M4 | A spouse stays in their own house. | S | |
| M5 | No affection outside courtship. | M | |

## Older, from docs/ROADMAP.md section 3

Kept there in full. In short: the state gives commerce nothing (no tax-farming or contracts), callings lean to commerce,
rival houses do not take each other over, some goods are in permanent glut, politics under a throne is unbuilt, the
look-and-feel pass is planned and not begun, there is no sound.

## Closed

| Id | Gap | Closed | How |
|---|---|---|---|
| B-a | Standing alone was not tested: no economy, no monopoly. | 2026-11-14 | Fuel of its own, a treasury that pays its way, and half the Reach's output of a strategic resource now move the Reach's temper; a state with no fuel is embargoed while it fights; one with leverage outlasts the Reach faster. B7 is what is left. |
| B-b | You could not work on a region's houses. | 2026-11-14 | A quiet word or a gift raises a house's regard; at 25 it gives its word. The capital's creature reports you. B8 is what is left. |
| B-c | The great war came by the calendar. | 2026-11-14 | Its clock is moved only by what is wrong with the Reach, and runs back when nothing is; blocs that had formed stand down. B9 and B10 are what is left. |
| B-d | A state was not a neighbour: no treaty, no reconquest, no rejoining. | 2026-11-14 | Recognition signs a ten-year treaty that bars war either way; when it lapses a rested Reach may vote a reconquest; the ruling house may take the state back in. B3 is what is left. |
| B-e | A state was not on the map. | 2026-11-14 | Its name is on its region on the map, in the region list and on the Worlds screen, with whether it is still fighting. B2 is what is left. |
| C-a | A rival Caesar could never arise: no general was both of the first rank and loved. | 2026-11-14 | Seven victories now count as being the squares' hero, charisma 5 as charm, and the Senate's guardians recall a general of that stature, which is his cause. Measured: one in 60 runs passes every gate. Rare by the owner's rule. |
| R-a | The Republic fell in two games of three with nobody trying. | 2026-11-14 | The great war no longer crowns anyone; a rival must pass three gates. |
| A-a | A relative could not stand for office. | 2026-11-14 | The ladder of offices. |
