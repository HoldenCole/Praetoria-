# Praetoria roadmap

As of 2026-11-13. This page is the summary: what is built, what is missing, what comes next, and ideas for later.
The dated log of every pass, with measurements, is `docs/GAP_ANALYSIS.md`. Each system has its own design doc, linked
below. Update this page at the end of every cycle.

This is a personal project. Nothing is for sale and there will be no paid DLC. What the design document once called
the DLC slate is part of this one roadmap: the near work is in section 4 and the later chapters are in section 6.

## 1. Where the game is

Praetoria is playable start to finish in the browser. The simulation is complete for a human-only game: you run a
house in a Republic, rise by the Senate, by arms or by commerce, and can end the Republic by taking a throne. It is
deterministic and saves and loads.

| | Now |
|---|---|
| Automated tests | 582 |
| Events with text | 304 in 32 files |
| Starts | 8 on the title screen and a character of your own; 3 hidden (a test fixture, two blank bases) |
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
| A life of one's own | Health and wounds, a record of what each person did, renown and a name, old friends and enemies, retirement, funerals | `LIFE` |
| The ladder of offices | Quaestor, aedile and praetor below the consulship, held by people and filled by election every fifth year; a consul must be forty and have been praetor; relatives stand for themselves | `LADDER` |
| Ambitions | The people of the house go after what they want by themselves, ask the head for help now and then, and succeed or are thwarted; a named heir | `AMBITIONS` |
| Personal wealth | A purse for every grown member of the house, the allowance, what they do with their own money, debts, heirlooms, the will, and the estate kept whole or shared out | `WEALTH` |
| Education and skills | A ceiling in every skill, learning by doing, by study and by being taught, children brought up toward their bent, tutors; the end of a step a year for everyone | `EDUCATION` |
| The household | Found family (wards, protégés, companions, adoption), a mentor for yourself, people who come asking, and two duties (steward, marshal) you can hand to any member of the house | `HOUSEHOLD` |
| Love, courting and marriage | Courtship over years (the person's favour and the family's consent), rivals, a bargained contract, promises between children, suits for your kin, and a marriage with a warmth that moves | `MARRIAGE` |
| A character of your own | Every dial on the person, the face, the house, its holdings and its family; a fair budget or none; presets, dice, a saved file | `CHARACTER` |
| The Academy | A year apart from the world at sixteen; a school in the Republic, the Institute under the Imperium; the tutorial in full or in brief | `ACADEMY` |
| Crises and endgame | Revolts, civil war, coalition war, the Great Crisis, proclaiming or seizing the Imperium, the epilogue | `MILESTONE_5_NOTES` |
| Browser client | Twelve screens including the galaxy map, procedural portraits and sigils, role and era theming, primer, saves | `WEB_CLIENT`, `ART_DIRECTION` |

## 3. Known gaps

Ranked within each group by how much they hurt play. The list of what the 2026-11-13 fix pass closed is at the end.

**The Republic**
1. The road by arms stops at "heard in the Senate". There is no triumph or recall voted by the Senate, no army loyal
   to its general rather than the state, and no march on the capital. That is the Caesar, and it is the missing end
   of the Republic's story.
2. A magistrate has nothing to do in office. The ladder (docs/LADDER.md) is climbed, paid and counted, but a praetor hears no cases and a quaestor keeps no accounts.
3. Commerce gets nothing from the state: no tax-farming, no contracts to supply the fleet.
4. A patron notices how a client votes, but never asks for anything else: no loan to a third house, no marriage, no
   standing for an office in the patron's interest.
5. Callings lean to commerce: about 16 of 32 houses, against about 8 of arms.

**The economy**
1. **Materials have nowhere to go.** After sixty years the Reach makes about 280 materials a year and uses 30 to 100;
   rare earths 22 against 0 to 2; exotics 10 against 0. Prices sit at two-thirds of usual because of it, and it is why
   mean credits reach 800. The price formula is not the problem; the economy needs sinks (upkeep on what is built,
   worlds that consume, more to spend strategic goods on).
2. Rival houses now add to a stake when a director holds less than half, but stewards defend their majority so well
   that it rarely happens. No takeover between rivals in the measured runs.
3. A boom is reported but never becomes a bubble that bursts.
4. Left out on purpose: foreign paper, share gifts as bribes, per-region prices, buying shares from a named house.

**The Imperium**
1. Government under a throne is simple next to the Republic. The orders are measured there but do little.
2. No court as a place: favour, audiences, the succession to the throne as a contest.

**The world**
1. Nations are off the map: numbers and expeditions, no territory.
2. Anomalies, archaeology and a source of exotics are designed but not built.
3. About 60 of 261 events never fire under the random test player. Checked: every flag they wait for is one the engine
   can raise, so none is dead; they need a player who gets into those situations (a triumph, a takeover, a revolt).

**The person and the family**
1. Courtship is built (docs/MARRIAGE.md), for the player's house only: stewards still marry by proposal, a spouse does
   nothing once married, and there are no children outside marriage.
2. Whose name a child carries is decided by a fixed rule (the head of a house keeps the seat's children; otherwise the
   greater house; otherwise the father's). It is never negotiated: no marriage contract says whose the children are.
3. Skills are learned now (docs/EDUCATION.md). Still missing: the Academy for anyone but you, asking a relative to study
   or teach, and any check of what slower skills do to battles and offices over a long run.
4. Purses, allowances, heirlooms and wills are built for the player's house (docs/WEALTH.md). A purse still holds only
   credits, and nobody outside the player's house has one.
5. Only houses can be patrons. A person cannot take a protégé, be a ward, or be adopted, and a house of one has no way
   to join a larger one.

**Starts**
1. *The Institute* is lethal by design: the line ends in about 1 to 3 of 10 unattended runs. Every other start is 0 or 1.

**Presentation**
1. No sound.
2. Portraits all wear the same uniform, whatever the person's service, rank or era.
3. The character sheet is thin next to the house and Senate screens.
4. The Exchange is dense: no view of one company.
5. The Office is a 3D room that a machine with no graphics card draws at about one frame a second. It now drops its
   bloom, then its shadows, then its frame rate when it cannot keep up, which is not enough on such a machine. A flat
   fallback for the room would be.
6. The phone layout works (one column, a swipeable rail) but was laid out by rule, not designed.

**Housekeeping**
1. The Godot desktop shell compiles and has never been run. Keep it or retire it.
2. The browser checks are in the repository but are not run by CI.

**Found while building the custom character**: a house's legitimacy could pass 100 (several systems added to it
without a ceiling). It is now held to 0–100 where it is stored.

**Closed by the 2026-11-13 fix pass**

| Was | Now |
|---|---|
| "The Old Lion stalls in the browser" | It never did. The test browser has no graphics card and the 3D Office starved the page of frames. A turn takes about half a second. |
| Turn processing | A third of what it was: a house's laws are summed once into a table. The browser builds each screen's data once per action. `?perf` in the address prints where a turn's time goes. |
| *The New Man*'s line ended in 12 of 12 unattended runs | 0 of 10. Rival houses no longer court across twenty years or past child-bearing, and no longer marry everyone off in the first spring. A patron offers a client an early match; an unwed head is offered one every so often. |
| The orders panel was cramped | It has its own tab on the Senate screen. |
| Phone width unchecked | Checked and given a layout. |
| Checks lived in scratch scripts | `tools/browser/play.js`, and `perf`, `survive`, `balance` commands in the tools. |
| Being a client asked nothing | The docket shows what your patron wants of a motion, and the patron's regard moves with how you vote on it. |
| Stewards never used sway | A house many others depend on now moves for the office its order fits. |
| A company could not found a venture | It can, from its own cash, by the player or a steward. |
| A save/load mismatch, raw code on the Senate screen, a misplaced validator check | Fixed (with the three orders). |

## 4. What comes next

The custom character the owner asked for first is **built** (docs/CHARACTER.md). Everything below is grouped by theme
and is **not in order**, within a group or between them. The last column says what each piece leans on, which is
the only real constraint on sequence. Each line is roughly one cycle of work (build, test, soak, browser, docs).

**The person and the family**

| Work | What it delivers | Ties to |
|---|---|---|
| **Marriage, the rest** | Built: docs/MARRIAGE.md. Left: stewards who court, a spouse who counsels and runs the house in your absence, children outside marriage, a spouse's household. | Personal wealth (dowries in land and shares). Education (a spouse's skills count). |
| **Education, the rest** | Built: docs/EDUCATION.md. Left: kin sent to the Academy, asking a relative to study or to teach, natures formed by an upbringing, learning that slows with age. | The Academy. Kin who pursue their ambitions. |
| **Wealth, the rest** | Built: docs/WEALTH.md. Left: purses for everyone in the Reach, shares and ventures held personally, dowries from a purse, entailed land, lending to a relative, heirlooms that do something. | The Exchange (holders who are people). Kin who pursue their ambitions. |
| **The household, the rest** | Built: docs/HOUSEHOLD.md. Left: other houses with households of their own, kin who find their own mentors, wards as hostages, the cost of keeping people, and wider duties (the Exchange, moving fleets). | Education. Personal wealth. Politics under a throne (hostages). |
| **Ambitions, the rest** | Built: docs/AMBITIONS.md. Left: other houses' people with ambitions, two relatives wanting the same thing, an ambition formed by an upbringing, failure that is felt. | The ladder of offices. Politics under a throne. |
| **A life, the rest** | Built: docs/LIFE.md. Left: records and renown for people of other houses, ill fame, old friends who act, duels, an elder who does more than teach. | Politics under a throne. The Caesar road. |

**The Republic**

| Work | What it delivers | Ties to |
|---|---|---|
| **The Caesar road** | The Senate votes a triumph or a recall. Fleets a commander has led to victory become loyal to him. A recalled general with loyal fleets may obey or cross the line: a civil war with the parties choosing sides. Victory ends the Republic. | Arms as an order; fleet loyalty; leads straight into the Imperium work. |
| **The ladder, the rest** | Built: docs/LADDER.md. Left: work to do in office (cases, accounts, the city), other houses that canvass and back, bribery and prosecution, the censorship and the tribunate, a province after office. | The Caesar road (a command after the consulship). The state and commerce. |
| **The state and commerce** | Tax-farming a region and contracts to supply the fleet, let by Senate motion. A patron calls in a client's vote on a real motion. Stewards use sway deliberately. | The Exchange; patrons and clients; under the wing. |

**The Imperium**

| Work | What it delivers | Ties to |
|---|---|---|
| **Politics under a throne** | The owner's Imperium expansion. How an Emperor governs and how everyone else gets what they want from him: the court as a place, favour, a council, offices by appointment, the succession as a contest, the Senate that remains, the guard. Sketch below. Needs its own plan before building. | The Caesar road is how most runs arrive here. The three orders need a meaning under a throne. Marriage (the imperial match). |

**Breaking away**

| Work | What it delivers | Ties to |
|---|---|---|
| **Secession** | A region, or only your own worlds in it, leaves the Republic or the Imperium and becomes a state of its own with you at its head, as the Rim left the Society in *Red Rising*. It can be planned across generations or done in a decade by a house strong enough. You must be able to stand alone first: an economy that feeds itself or has something the rest cannot do without, and a fleet of your own. Then, almost always, a civil war for it. Sketch below. Needs its own plan; several cycles. | The economy (what a domain makes and needs; a monopoly as leverage). Military depth (supply, loyalty). The Caesar road (a civil war with sides). Nations on the map (a second state with a border). Politics under a throne (how an Emperor answers). Marriage and patronage (binding the region's houses). |

**The economy**

| Work | What it delivers | Ties to |
|---|---|---|
| **Economy follow-ups** | Hostile stakes by rival stewards, a bubble that bursts, companies that found ventures, a view of one company, the glut. | Personal wealth (who owns the shares). |

**The world**

| Work | What it delivers | Ties to |
|---|---|---|
| **Nations on the map** | Territory, borders and fronts for the nations beyond the Reach. | Needed before *The Threat From Beyond* (section 6). |
| **Sound, balance, content** | Audio, a balance pass across sixty-year runs, and events for whatever the soak shows never fires. | Best done after the person-and-family group, which will add many events. |

**Presentation and the first hour**

| Work | What it delivers | Ties to |
|---|---|---|
| **Look and feel pass** | Section 5. Planned, not started. | The custom character (the face editor). A life of one's own (the character sheet). |
| **Starts and first hour** | The title screen grouped by road (born to a name, by arms, by commerce, the Academy). The orders, courtship and education in the Academy's lessons. | Marriage (an early match for the New Man). |

**Later chapters** (section 6): *Courts of Intrigue*, *The Frontier*, *The Threat From Beyond*, *AI Ascendant*, *Wrath
of the Stars*. Each is several cycles.

### Sketches for the newer pieces

Short on purpose. Each needs a proper plan before it is built. "Today" is what the game has now.

The sketches for love and marriage and for education have been built: see docs/MARRIAGE.md and docs/EDUCATION.md.

**Secession: breaking away from the Republic or the Imperium**
- *Today*: the Reach has ten regions of three or four systems, and each house's worlds sit in one or two of them. Wars
  are between houses. Nothing can leave the state, and the only other powers are the nations beyond the map.
- *The aim*: you take a region out of the state and rule it. It works under either government.
- **Standing alone comes first.** A panel shows, for the domain you propose to take out, whether it could live by itself:
  - *Economy*: what the domain makes of each good against what it uses. A shortfall must be bought, so the domain needs
    something to sell.
  - *Leverage*: a good the rest of the Reach cannot do without and gets mostly from you. A monopoly on helium-3 is the
    clear case: you can trade it for everything you lack, and the centre's fleets go short of fuel while they fight you.
    With that and a strong fleet, winning the war is nearly all that is left to do.
  - *Arms*: hulls, yards and depots inside the domain, and whether those fleets answer to you or to the state.
  - *Unity*: where the region's other houses stand, and whether its people think of themselves as apart.
- **Long or short.** Yards, ventures, marriages into the neighbouring houses and a regional identity all build slowly,
  so a founder can prepare what a grandchild declares. A house that is already rich and armed can go much sooner. How
  long it takes is decided by how ready you are, not by a timer.
- **How much to take.** Two choices, and the second is deliberately the harder war:
  - *The whole region.* Say it has six worlds and you hold two. The houses holding the other four must be brought with
    you: persuaded, married into, made clients, paid, or frightened. Each ends up with you, neutral, or loyal to the
    centre and the first enemy. If it works the new state is large and you are its ruler.
  - *Only your own worlds.* Nobody to persuade, but a small base, loyal neighbours on every side and a far harder war.
- **Pushing the region.** A head who is strong in a region can work on it for years before any declaration: favours
  and loans, clients, grievances kept alive (taxes, an Act that hurt the region, a war the centre lost), local laws.
- **The declaration and the answer.** The centre may bargain (autonomy, a charter, lower taxes: a middle outcome that
  some will take), embargo, or fight. In the Republic the Senate votes it and the parties split, so friends in the
  chamber can delay or weaken the answer. Under a throne the Emperor's temper and strength decide; a weak one may let
  you go.
- **The war of independence.** A civil war with sides: the state's fleets and the loyal houses against your league. It
  is won by holding your worlds long enough, by decisive battles, or by wearing the centre down (war weariness, the
  embargo, unrest at home). A nation beyond the Reach may recognise and help you, at a price.
- **Afterwards.** Win, and there is a new state with a border, a form of government you choose, and the rest of the
  game to play as its ruler: trade or not with the old state, and the risk it comes back. Lose, and it is attainder,
  seized worlds, exile or life as someone's client, with the region garrisoned. A draw is autonomy.
- **Others can do it too.** A strong regional house may try to leave a state you lead.
- *Sizing*: a readiness panel; the league and persuasion; the declaration and the centre's answer; the war; the new
  state. Five pieces, most of them a cycle each. It leans on a civil war with sides (the Caesar road) and on a second
  state on the map (nations on the map), so those two are best built first.

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

## 6. Later chapters and ideas

**Later chapters.** These five were the paid DLC slate in `GDD.md` sections 12 and 20. They are now simply later work
on the same game, suggested in this order. The engine was built with slots for them, so most of each is content.

| Chapter | What it adds | What it builds on today | Do it after |
|---|---|---|---|
| *Courts of Intrigue* | An intelligence sphere and a spymaster's career. Deep plots, counter-intelligence, secret societies. Possibly a fourth order beside the Senate, Arms and Commerce. | Career tracks and spheres are data lists. Assassination, intel pictures, sway and patronage exist. | A custom character and the state and commerce, so a new career has somewhere to matter. |
| *The Frontier* | Colonisation and terraforming, new holding kinds, deeper building trees, anomalies and archaeology that yield exotics. | Projects already build over years. Two systems start unclaimed. Exotics are a good with a price and no source. | Economy follow-ups. |
| *The Threat From Beyond* | Non-human great crises: a swarm that advances as a front, a plague that spreads along lanes, an awakening where someone dug. | The great-crisis framework runs one human archetype from a data file. | Nations on the map, which adds fronts and borders, and *The Frontier*, whose digs foreshadow it. |
| *AI Ascendant* | Machine sentience as a crisis: placate, fight or prevent. A career around it. Your own systems turning. | The same crisis framework; fleet loyalty and defection. | *The Threat From Beyond*. |
| *Wrath of the Stars* | The full megastructure catalogue, planet-killers, a higher ceiling on crisis intensity. | Megastructures exist as four-year projects gated by rank. | Last. It is the most content and the least system. |

*Houses of Legend* (named great houses with their own mechanics and legacies) was the reserve. It fits best as part
of the look and feel pass and the custom character: a line of history for every house, and a house you design.

**Newer ideas.** Not ordered and not promised. They came out of the work so far and suit the Roman model.

1. *The Provinces*: governing a subdued nation. Extortion, tribute, and being prosecuted for it when you come home.
2. *The Courts*: trials as play. Prosecution as a political weapon, advocates who make a name, exile as a verdict.
3. *The Commons*: tribunes with a veto, the grain dole, riots in the capital, and politics by the crowd.
4. *Veterans*: land for discharged crews, colonies founded by a general, and whose clients those colonists become.
5. *Pacts*: a formal compact between two or three great houses to share the offices, and what happens when it breaks.
6. *Priesthoods and omens*: colleges that can delay a vote or bless a war, as another, softer road to standing.
7. *Trade routes*: prices by region, convoys, insurance, and piracy as a business.
8. *The saga as a book*: export a finished run as a readable chronicle.
9. *Other galaxies*: a generated map, or an editor. The content is already data.

## 7. Keeping this page honest

**The rule for anything that touches the family:** you play one person, not a house. Kin act for themselves; the
player approves, forbids, helps and arranges, and is never required to give a relative orders for them to have a life
(docs/PROCESS.md, "One person, not a house"). Every piece in the person-and-family group is to be built to it.

- A thing is "done" here only when it is tested, soaked and checked in the browser.
- Numbers in section 1 come from the content files and the test run, not from memory.
- When a gap is closed, move it to section 2 and note the pass in `GAP_ANALYSIS.md`.
