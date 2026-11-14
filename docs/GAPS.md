# The gap register

One list of everything in the game that is thin: built, but not deep enough to stand on. It exists so that thin things
do not pile up unseen. The rule for keeping it is in docs/PROCESS.md ("Map the gaps as you build").

- **S** is small: one change, hours. **M** is a cycle (build, test, measure, document). **L** is several cycles and
  needs a plan first.
- A gap is closed when it is built, tested and measured, and it then moves to the bottom table with the date.
- `tools/gaps.sh` counts what is open. The count is printed in docs/ROADMAP.md, section 1.

Last updated 2026-11-14 (eighth pass: quarrels and marked failures; A1 and A3 closed).

## The worklist: small gaps to fill now

Set by the owner on 2026-11-14: close what exists, then deepen it; no new systems until this list is short. Every
position (magistrate, justice, senator) must have something to do. Worked from the top.

| Order | Id | What | State |
|---|---|---|---|
| 1 | W2, W4, E2, H1 | Dowries from a purse; a gift to a relative; ask a relative to study; kin find mentors | next |
| 2 | B4, B9, B10 | Joining a state that exists; a second great war; raising the pressure as well as lowering it | next |
| 3 | L7 | Public trust in the magistrates moves elections and the estates, not only the troubles of the Reach | next |
| 4 | C9 | Decide whether the first gate asks too much of other houses' generals; probe and tune | next |
| 5 | J3 | The lowest ranks: an Advocate pleads before a bench, a Lieutenant and a Clerk have a year's work | next |

Closed from this list on 2026-11-14: L1, L4, F2, F4, C3, C5, E4, then J1 and S1, then O1, then J2, then A1 and A3 (see the bottom table).

## The same gap in many places

These recur across systems. Closing one properly closes several lines below, so they are worth more than they look.

| Id | Gap | Where it shows | Size |
|---|---|---|---|
| X1 | **Only the player's house has an inner life.** Other houses have no purses, records, renown, households, ambitions or courtships of their own; their people are chosen by rules. | Marriage, household, wealth, ambitions, a life, the ladder, the Caesar road | L |
| X2 | **Other houses do not use what the player can.** They do not canvass, back candidates, give games, work on a region, declare independence by choice, or pay donatives. Their magistrates decide their year's business by nature alone, never by what their house needs. Their senators do not spend their year (one audits), and their judges hear no hearings. | The ladder, the work of office, the Caesar road, breaking away | M |
| X3 | **Numbers are first guesses.** Every recent system says so. Each needs a probe and a tuning pass, as the Caesar road and the great war now have. | All of them | M each |
| X4 | **Events that need a rare player choice never fire in the soak.** They are proved by scripted tests, not seen in play. | Most event files since courtship | S each, ongoing |

## Breaking away and the great war (docs/BREAKAWAY.md)

| Id | Gap | Size | Leans on |
|---|---|---|---|
| B2 | A state's border means nothing for trade, the lanes or the Exchange. Its region is named on the map but not drawn as a different country. | M | Nations on the map |
| B3 | A state makes no agreements of its own: no trade treaty, no alliance, no envoys. It does not make war as a state. | M | B1 |
| B4 | A state is one region. Regions and houses cannot join one that exists. | S | B3 |
| B5 | The older free march (a rim house renouncing the capital) and free holds (a rebel world) are their own rules and should become breakaway states. | M | |
| B6 | Under a throne the Reach's temper is one number. A new Caesar should have his own. | S | Politics under a throne |
| B7 | Standing alone weighs fuel, the treasury and strategic monopolies. It does not weigh food, manpower or the goods of the market. | M | The economy |
| B8 | Working on a house is a word or a gift. No promise of a place in the new state, no marriage to bind it, no threat. | S | B1 |
| B9 | Only one great war in a saga. After it ends, pressure builds toward nothing. | S | |
| B10 | The player cannot deliberately raise the pressure toward a great war, only lower it by mending what is wrong. | S | |

## Forms of government (the owner's direction, docs/BREAKAWAY.md "The next stage")

| Id | Gap | Size | Leans on |
|---|---|---|---|
| G1 | **Each form of state is governed differently:** who rules, what the ruler takes, what holds the houses together, what a house that is not the ruler can do. Replaces B1. | L | |
| G2 | **A state can change its form.** Easier toward the democratic end; gated toward the authoritarian end, as a Caesar is gated. An imperium becoming a dictatorship is a step toward the democratic end. | M | G1 |
| G3 | **Two dictatorships:** one person who took the state, and a military that did. | M | G1, the Caesar road |
| G4 | **Two republics:** one where the great houses elect the ruler among themselves, one where the people elect directly. | M | G1 |
| G5 | **The Reach is a state under the same rules.** Its Republic and its Imperium are two rungs of the same ladder, and a region that leaves it is governed by the same rules. | L | G1, politics under a throne |
| G6 | **The people can revolt** against whoever rules, in any form. A later expansion. | L | G1, the estates |
| G7 | A house of a state can try for its chair, plot against its ruler, or take its own region out of the state in turn. | M | G1 |

## Positions with nothing to do

| Id | Gap | Size | Leans on |
|---|---|---|---|
| J3 | The lowest ranks have no work: a Cadet to a Lieutenant, a Clerk, an Administrator, an Aspirant, an Advocate. They learn by doing and that is all. An Advocate should plead before somebody's bench. | M | |
| O2 | The charge of an office of the Reach is one decision a year from three. A consul does not preside over anything, the Treasurer's budget has no figures, and the Speaker's order of business does not touch the motions actually on the docket. | M | The Senate |

## The Caesar road (docs/CAESAR.md)

A rival Caesar is rare on purpose (docs/PROCESS.md). Measured after E4 (a victory teaches only up to the ceiling): over 50 Republic runs another house's general was of the first rank in 2, passed all three gates in 1, and none marched. Before E4 it was first rank in 15 of 60. That is rarer than before and worth watching: see C9.

| Id | Gap | Size | Leans on |
|---|---|---|---|
| C2 | A general does not defect with his crews, and crews do not follow a dead commander's heir. | M | |
| C4 | No proscription, amnesty or negotiated end. | M | |
| C6 | A relative who is the general cannot cross against the head's wishes. | S | The one-person rule |
| C7 | The player cannot make the times, only meet them. | M | B10 |
| C8 | Nothing here under a throne: no triumph by the throne's gift, no guard. | L | Politics under a throne |
| C9 | With skill ceilings holding in battle, a commander of the first rank is now very rare among other houses (2 runs of 50). The gate may ask too much of them: `GateTactics` and `GateVictories` want a probe and a decision. | S | X3 |

## The ladder of offices (docs/LADDER.md)

| Id | Gap | Size | Leans on |
|---|---|---|---|
| L2 | The player cannot bribe a magistrate or prosecute a former one. (A bent ruling is now appealed before a Justice.) | M | |
| L3 | No censorship, tribunate, or province after office. | L | The state and commerce |
| L8 | A senator's year is one of three things. There are no committees, no standing with a committee, and no record of attendance beyond three absent years. | M | |
| L5 | The work of office is one decision a year from three. A case has no facts beyond who is in the right; the accounts have no figures; the works are one sum. | M | |
| L7 | Public trust in the magistrates feeds only the troubles of the Reach. It does not move elections, the estates or a house's standing. | S | |

## A life of one's own (docs/LIFE.md)

| Id | Gap | Size | Leans on |
|---|---|---|---|
| F1 | No ill fame: nothing a person does is held against them as a person. (A magistrate removed for taking money is barred for five years, and that is all.) | M | |
| F3 | No duels, and no wound but in battle. | M | |
| F5 | The elder teaches the new head and otherwise does nothing. | S | |
| F6 | Old ties count in the Senate and in a civil war. They do not count in an election, a court case or a marriage. | S | |

## Ambitions, wealth, education, the household, marriage

| Id | Gap | Size | Leans on |
|---|---|---|---|
| A5 | A venture never fails, so a founder's failure is never marked; and a quarrel is between two people only: the house does not divide behind them, and the loser holds nothing against the winner afterwards. | S | |
| A2 | Restoring the house from an allowance is too slow to matter. | S | |
| A4 | An ambition is set by the dice at sixteen, not by an upbringing. | S | E3 |
| W1 | A purse holds credits only: no shares, ventures or worlds held personally. | M | |
| W2 | Dowries come from the treasury, not a purse. | S | |
| W3 | Nothing is entailed. | S | |
| W4 | You cannot lend to a relative or give a sum beyond the allowance. | S | |
| W5 | Heirlooms do nothing but be worth something. | S | |
| E1 | Only your own character goes to the Academy. | M | |
| E2 | You cannot ask a relative to study, or one to teach another. | S | |
| E3 | No nature is formed by an upbringing; no trait is lost. | M | |
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
| L1 | A magistrate had no work to do. | 2026-11-14 | Every sitting quaestor, aedile and praetor has a year's business: the accounts, the works, a case between two houses at odds. The player decides their own from three choices; everyone else decides theirs by nature. Money taken in office sometimes comes out: the seat is lost and the person barred five years. What they do moves public trust in the magistrates, which below 35 is one of the things wrong with the Reach. L5 and L7 are what is left. |
| L4 | A relative who lost did not blame the head. | 2026-11-14 | One who stood, was not spoken for and lost thinks 10 the less of the head and says so; one who was backed and won owes it. An event follows. |
| F2 | Old friends did nothing. | 2026-11-14 | A friend of eight years leans for what the player's house moves in the Senate, stands by it when a censure, embargo, recall or the like is aimed at it, and is likelier to take its side in a civil war. An old enemy does the opposite. F6 is what is left. |
| F4 | You could not send a physician to a relative. | 2026-11-14 | The head sends their own, from their own purse; the relative remembers it. |
| C3 | The parties did not act as parties in the civil war. | 2026-11-14 | A bench whose leader has declared brings its members, all but one with a strong reason of its own; the general's own bench is his. The forecast on the panel uses the same rule. |
| C5 | The Senate's sword got no command of the state's fleets. | 2026-11-14 | Every hull of the Reach's own that is not the marching general's to keep goes under the sword's colours for as long as the war lasts, and goes home after. |
| E4 | A battle added a step without regard to the ceiling. | 2026-11-14 | A victory teaches the weaker of tactics and gunnery up to its ceiling, then the other, then nothing. C9 is what it uncovered. |
| J1 | The law career's Judge and Justice heard nothing. | 2026-11-14 | A Judge hears a matter a year between an estate and a house; a Justice hears the appeal against a praetor's bent ruling and may overturn it. Another house's Justice hears it if yours has none, so a ruling against your house can be mended. |
| S1 | A senator who moved nothing had no business in a year. | 2026-11-14 | The head spends the year's attention: read the accounts (and perhaps follow stolen money to a magistrate), speak for the region, or work the bench. Three years of nothing is remarked on. One upright senator of another house audits each year too. L8 is what is left. |
| O1 | The consuls, the Treasurer and the Speaker decided nothing. | 2026-11-14 | Each carries a charge a year: a consul calls the magistrates to account, sends the fleet onto the lanes or serves the bench; the Treasurer balances the books, forgives a region its levy or lets the contracts to their own house; the Speaker keeps the order fair, brings their own business forward or gives an enemy no day. Other houses' officers decide by nature, and what they do to the player's house brings an event. O2 is what is left. |
| J2 | The navy's and the administration's ranks had no desk. | 2026-11-14 | A Governor or above has the year's returns to assess, press or skim; a Commander or above without a fleet has a year ashore to drill the crews, patrol the home lanes or sit on a board. J3 (the lowest ranks) is what is left. |
| A1 | Two relatives who wanted the same thing did not quarrel. | 2026-11-14 | Two after the same office, fleet or inheritance think less of each other every year it lasts; the head may stand behind one, stop it, or keep out. It ends when the prize is settled. A5 is what is left. |
| A3 | A relative's failure was not marked. | 2026-11-14 | An election lost and a battle lost in command are written into the life as failures, cost renown and nerve, and count against the ambition; three and the relative gives it up. Ventures do not fail yet (A5). |
