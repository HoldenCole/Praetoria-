# Praetoria roadmap

As of 2026-11-13. This page is the summary: what is built, what is missing, what comes next, and ideas for later.
The dated log of every pass, with measurements, is `docs/GAP_ANALYSIS.md`. Each system has its own design doc, linked
below. Update this page at the end of every cycle.

## 1. Where the game is

Praetoria is playable start to finish in the browser. The simulation is complete for a human-only game: you run a
house in a Republic, rise by the Senate, by arms or by commerce, and can end the Republic by taking a throne. It is
deterministic and saves and loads.

| | Now |
|---|---|
| Automated tests | 493 |
| Events with text | 257 in 24 files |
| Starts | 8 on the title screen, 1 hidden test fixture |
| Galaxy | 31 systems, 40 lanes, 10 regions, 31 standing houses |
| Laws / estates / parties | 22 / 6 / 4 |
| Senate motions / offices | 12 / 5 |
| Ventures / goods | 14 / 6 |
| Nations beyond the Reach | 5 |
| Academy lessons | 9 |

## 2. What is done

| Area | What it covers | Doc |
|---|---|---|
| Event engine and turn loop | Authored events bound to roles, a director that paces them, a command bus shared by player and stewards, replay from a seed | `MILESTONE_1_NOTES`, `MILESTONE_2_NOTES` |
| Dynasty | Ageing, births, marriage, succession laws, regency, fostering, assassination, wilful kin, the seat passing to an heir | `MILESTONE_DYNASTY_NOTES` |
| Houses and titles | A ladder of seven ranks; Republic names (Citizen to First Citizen) and Imperium names (Knight to Emperor); three ways to rise | `TITLES` |
| Domain and galaxy | Holdings, buildings, population, unrest, governors, regions, rings, raiders, free holds, a way back for a landless house | `GALAXY`, `ECONOMY` |
| Strategic resources | Helium-3 fuel, rare earths, isotopes; deposits; fuel bills | `ECONOMY` |
| War | Fleets, ship classes, stances, loadouts, command styles, loyalty, supply, campaigns, sieges, peace terms | `MILITARY` |
| Nations beyond the Reach | Five off-map nations; war by Senate motion; expeditions; tribute; raids | `FOREIGN_NATIONS` |
| The Senate | Motions, votes by weight, lobbying, offices, parties, the consulship and its veto, Acts, estates' petitions | `SENATE`, `REPUBLIC` |
| Laws and estates | 22 laws by position, six estates with opinions, Reach-wide Acts | `POLITICS` |
| The three orders | Standing in the Senate, Arms and Commerce; callings; old and new names; sway; patrons and clients; a seat by the census; canvassing | `REPUBLIC` |
| The economy | Prices that follow war and scarcity; ventures bought and sold; building over years; companies, shares, dividends, takeovers; war bonds; the Senate's and throne's hand on the market | `ECONOMY_MARKET` |
| The Academy | A year apart from the world at sixteen; a school in the Republic, the Institute under the Imperium; the tutorial in full or in brief | `ACADEMY` |
| Crises and endgame | Revolts, civil war, coalition war, the Great Crisis, proclaiming or seizing the Imperium, the epilogue | `MILESTONE_5_NOTES` |
| Browser client | Twelve screens including the galaxy map, procedural portraits and sigils, role and era theming, primer, saves | `WEB_CLIENT`, `ART_DIRECTION` |

## 3. Known gaps

Ranked within each group by how much they hurt play.

**The Republic**
1. The road by arms stops at "heard in the Senate". There is no triumph or recall voted by the Senate, no army loyal
   to its general rather than the state, and no march on the capital. That is the Caesar, and it is the missing end
   of the Republic's story.
2. No ladder of offices. Any qualified house may stand for consul without having held a lesser post.
3. Commerce gets nothing from the state: no tax-farming, no contracts to supply the fleet.
4. Being a client asks little. One recurring event; the patron never calls in a vote on a real motion.
5. Stewards never use sway on purpose. It tilts votes that were moved for other reasons.
6. Callings lean to commerce: about 15 of 33 houses, against about 8 of arms.

**The economy**
1. Rival houses do not take each other over on the Exchange. A player can.
2. Materials, rare earths and exotics are in glut and sit below their usual price most years.
3. A company can enlarge its ventures but cannot found one.
4. A boom is reported but never becomes a bubble that bursts.
5. Left out on purpose: foreign paper, share gifts as bribes, per-region prices, buying shares from a named house.

**The Imperium**
1. Government under a throne is simple next to the Republic. The orders are measured there but do little.
2. No court as a place: favour, audiences, the succession to the throne as a contest.

**The world**
1. Nations are off the map: numbers and expeditions, no territory.
2. Anomalies, archaeology and a source of exotics are designed but not built.
3. In a 27-run soak, 191 of 257 events fired. Some of the other 66 may be unreachable or only very rare.

**Starts**
1. *The New Man* is fragile: two young family members and no heir. The line often ends early.
2. *The Old Lion* has not been driven through in the browser by the test scripts. They stall on it.

**Presentation**
1. No sound.
2. Portraits all wear the same uniform, whatever the person's service, rank or era.
3. The character sheet is thin next to the house and Senate screens.
4. The orders panel is cramped in the Senate's side column.
5. The Exchange is dense: no view of one company, small tab labels.
6. Layout has not been checked at phone width.

**Housekeeping**
1. The browser checks and the balance probes are run by hand from scripts that are not in the repository.
2. The Godot desktop shell compiles and has never been run. Keep it or retire it.

## 4. What comes next

In order. Each line is about one cycle of work (build, test, soak, browser, docs).

| # | Work | What it delivers |
|---|---|---|
| 1 | **The Caesar road** | The Senate votes a triumph or a recall. Fleets that a commander has led to victory become loyal to him. A recalled general with loyal fleets may obey or cross the line: a civil war with the parties choosing sides. Victory ends the Republic. |
| 2 | **The ladder of offices** | Lesser offices before the consulship, each with an age and a prior post. Holding each feeds standing in the Senate. |
| 3 | **The state and commerce** | Tax-farming a region and contracts to supply the fleet, let by Senate motion. A patron calls in a client's vote on a real motion. Stewards use sway deliberately. |
| 4 | **Look and feel pass** | Section 5 below. Planned, not started. |
| 5 | **Economy follow-ups** | Hostile stakes by rival stewards, a bubble that bursts, companies that found ventures, a view of one company, the glut. |
| 6 | **Starts and first hour** | The title screen grouped by road (born to a name, by arms, by commerce, the Academy). An early marriage for the New Man. The orders in the Academy's lessons. |
| 7 | **Governments and the Imperium** | The owner's later item: how a throne governs, the court as a place, the orders under an Emperor. Needs its own plan first. |
| 8 | **Nations on the map** | Territory, borders and fronts for the nations beyond the Reach. |
| 9 | **Sound, balance, content** | Audio, a balance pass across sixty-year runs, and events for whatever the soak shows never fires. |

## 5. Look and feel pass: the plan

Planned only. Nothing in this section has been started.

**Screens**

| Screen | What is wrong now | What to do |
|---|---|---|
| Senate | The orders panel is squeezed into the side column and its heading wraps. The screen is long. | Give standing, sway and patronage their own tab beside the chamber. Keep the hemicycle and docket as the first view. |
| Exchange | Four tabs of dense tables. Inactive tabs are hard to read. No picture of one company. | A company page: a large price line, earnings and dividends by year, who holds it. Larger tab labels. A one-line summary above each table. |
| Houses | The standings table is wide and the focused house's panel is one long list. | Group the panel into sections (standing, ties, people, holdings). Let the order badges sort and filter. |
| You (character) | Short. It lists the person and their bonds and little else. | See "Characters" below. |
| House | Carries the ledger, the worlds and a summary of ventures. | Show where the year's credits came from and went as a simple bar by source. |
| Title screen | Eight starts in a row with a line each. | Group by road and say in a sentence what each start is like to play. |
| Galaxy map | Not reviewed this cycle. | Review for readability at a glance: whose space, where the war is, where the fuel is. |
| All | Layout unchecked on a phone. No sound. | Check at phone width and fix what breaks. Decide on sound in cycle 9. |

**Characters**

| Now | Plan |
|---|---|
| Every portrait is a profile bust in the same collar and gorget. The face comes from the person's id; hair greys with age. | Dress by service and rank: Navy uniform with rank marks, a magistrate's robe, a factor's coat. Republic and Imperium dress differ. |
| Children do not look like their parents. | Family resemblance: a child's face takes features from both parents. |
| A face never changes except by greying. | Scars from battles and duels, strain when stress is high, mourning after a death. |
| Portraits are small everywhere. | A large portrait on the character sheet and for whoever is speaking in an event. |
| The character sheet shows skills and bonds as lists. | A life line of what the person has done, skills as bars, each trait with what it does, the next rung of their career and what it needs, and what the Reach knows them for. |
| Houses are a sigil and a colour. | A line of description per house drawn from its calling, age and record: "an old senatorial name, twice consul". |

## 6. Ideas for future expansions

Ideas, not commitments. The first five are the long-standing plan in `GDD.md` section 12. The rest came out of the
work so far and suit the Roman model.

**From the design document**
1. *The Threat From Beyond*: non-human great crises (a swarm, a plague, an awakening) with their own behaviour on the map.
2. *AI Ascendant*: machine sentience as a crisis, and a career track around it.
3. *Courts of Intrigue*: an intelligence sphere and a spymaster's career; deeper plots and secret societies.
4. *The Frontier*: colonisation, terraforming, deeper building trees.
5. *Wrath of the Stars*: planet-killers and the great megastructures.

**Newer ideas**
1. *The Provinces*: governing a subdued nation. Extortion, tribute, and being prosecuted for it when you come home.
2. *The Courts*: trials as play. Prosecution as a political weapon, advocates who make a name, exile as a verdict.
3. *The Commons*: tribunes with a veto, the grain dole, riots in the capital, and politics by the crowd.
4. *Veterans*: land for discharged crews, colonies founded by a general, and whose clients those colonists become.
5. *Pacts*: a formal compact between two or three great houses to share the offices, and what happens when it breaks.
6. *Priesthoods and omens*: colleges that can delay a vote or bless a war, as a fourth, softer road to standing.
7. *Trade routes*: prices by region, convoys, insurance, and piracy as a business.
8. *Houses of Legend*: named great houses with their own mechanics and legacies.
9. *The saga as a book*: export a finished run as a readable chronicle.
10. *Other galaxies*: a generated map, or an editor. The content is already data.

## 7. Keeping this page honest

- A thing is "done" here only when it is tested, soaked and checked in the browser.
- Numbers in section 1 come from the content files and the test run, not from memory.
- When a gap is closed, move it to section 2 and note the pass in `GAP_ANALYSIS.md`.
