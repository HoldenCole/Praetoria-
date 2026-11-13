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
| Automated tests | 500 |
| Events with text | 261 in 25 files |
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

Ranked within each group by how much they hurt play. The list of what the 2026-11-13 fix pass closed is at the end.

**The Republic**
1. The road by arms stops at "heard in the Senate". There is no triumph or recall voted by the Senate, no army loyal
   to its general rather than the state, and no march on the capital. That is the Caesar, and it is the missing end
   of the Republic's story.
2. No ladder of offices. Any qualified house may stand for consul without having held a lesser post.
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
1. Marriage is a proposal, a suit and three match events. There is no courtship, no affection that moves, no rival
   suitor, no contract.
2. A child takes the father's house. A daughter of the house who marries out takes her children with her.
3. Skills change only when an event says so, or in the Academy year. Nobody studies, trains or is taught.
4. A house has one purse. A person owns nothing; kin have no allowance, dowry or inheritance of their own.
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

One thing is fixed: **a custom character is next** (the owner's priority; plan below). Everything else is grouped by
theme and is **not in order**, within a group or between them. The last column says what each piece leans on, which is
the only real constraint on sequence. Each line is roughly one cycle of work (build, test, soak, browser, docs).

**The person and the family**

| Work | What it delivers | Ties to |
|---|---|---|
| **A custom character** (next) | Build your own person, house and family with every dial exposed, balanced or not. | Everything else in this group adds dials to it. |
| **Love, courting and marriage** | Courtship as something you do over years: meeting, favour, rival suitors, a match for love or for advantage, the contract and the dowry, and what the marriage is like afterwards. Sketch below. | Personal wealth (dowries). Education (a spouse's skills count). Fixes the fragile *New Man* start. |
| **Education, skills and getting better** | Children are raised: a tutor, a focus, a household to be sent to. Adults improve by doing, by study and by being taught, and decline with age. Sketch below. | The Academy (a year of this already exists). The ladder of offices. Under the wing (a mentor teaches). |
| **Personal wealth, house wealth and the dynasty's assets** | A person's own purse beside the house's. Allowances, dowries, wills, heirlooms, and an estate that is divided or kept whole when the head dies. Sketch below. | The economy (shares and ventures can be held personally). Succession laws. Marriage. |
| **Under the wing** | A person, not only a house, can have a patron: a mentor, a guardian for a ward, adoption as an heir. A house of one can be taken into a greater household. Sketch below. | Patrons and clients (built for houses). Education. The custom character's "alone" start. |
| **A life of one's own** | Health and wounds, a personal reputation apart from the house's, friendships and rivalries that last decades, retirement, a funeral and what is said at it. | The look and feel pass (the character sheet). Glory in Arms is a first piece of this. |

**The Republic**

| Work | What it delivers | Ties to |
|---|---|---|
| **The Caesar road** | The Senate votes a triumph or a recall. Fleets a commander has led to victory become loyal to him. A recalled general with loyal fleets may obey or cross the line: a civil war with the parties choosing sides. Victory ends the Republic. | Arms as an order; fleet loyalty; leads straight into the Imperium work. |
| **The ladder of offices** | Lesser offices before the consulship, each with an age and a prior post. Holding each feeds standing in the Senate. | Education (what a post demands of its holder). |
| **The state and commerce** | Tax-farming a region and contracts to supply the fleet, let by Senate motion. A patron calls in a client's vote on a real motion. Stewards use sway deliberately. | The Exchange; patrons and clients; under the wing. |

**The Imperium**

| Work | What it delivers | Ties to |
|---|---|---|
| **Politics under a throne** | The owner's Imperium expansion. How an Emperor governs and how everyone else gets what they want from him: the court as a place, favour, a council, offices by appointment, the succession as a contest, the Senate that remains, the guard. Sketch below. Needs its own plan before building. | The Caesar road is how most runs arrive here. The three orders need a meaning under a throne. Marriage (the imperial match). |

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

### Next up: a custom character

The owner wants this first: lots of toggles, real freedom, and the ability to make someone overpowered on purpose.

**Today.** Creation offers a name, a sex, one background, up to two nature and two aptitude traits, a career track
and an ambition, laid over one of the fixed starts. The house, the family and the numbers come from the start.

**Two modes**

| Mode | Rule |
|---|---|
| Standard | A points budget. Strengths are paid for with weaknesses, so the character is fair. |
| Free | No limits. Any trait, any number, any rank, any treasury. The run is marked as custom in the save and the epilogue. |

A **power rating** is shown as you build, in both modes, so an overpowered character is a choice you can see.

**What can be set**

| Part | Dials |
|---|---|
| The person | Name, sex, age. Every skill set directly. Any nature and aptitude traits. Career track and starting rank. Ambition. |
| The face | Skin, hair, style, beard, age lines, scars; reroll and lock. Uses the portrait work in section 5. |
| The house | Name, colour and sigil, culture, old or new name, rank, legitimacy, party, every resource in the treasury. |
| What it holds | Worlds and stations (which, or none), hulls, ventures, a seat in the Senate, a patron or clients. |
| The family | Spouse, children, siblings: how many, how old, who is heir. |
| The world | Republic or Imperium. Start at the Academy or in the world. A friend and a rival among the great houses. |

**Shortcuts**

- Presets that fill every dial: the Heir, the Officer, the New Man, and one that is frankly overpowered.
- Randomise everything, or one part.
- Save a character to a small file and load it again.

**Engine work.** `CreationOptions` grows to carry all of the above; `CharacterCreation` applies it and validates by
mode; the world builder places custom holdings, fleets and family. Tests: any legal combination builds a valid world,
and a soak of extreme characters (all skills at the top, an empty house, a child head) runs sixty years clean.

**Not in this piece.** New traits, new backgrounds or new art. It exposes what the game already has.

### Sketches for the newer pieces

Short on purpose. Each needs a proper plan before it is built. "Today" is what the game has now.

**Love, courting and marriage**
- *Today*: one command proposes a marriage; a few events arrange a match; a marriage forges a claim and allows children.
- A courtship runs over years: you meet (at the Academy, at court, through a patron), win or lose favour, and may have a rival.
- Two kinds of match, and most are a mix: for love (affection, less stress, a loyal spouse) and for advantage (a claim, an alliance, a dowry, a patron's daughter).
- The families negotiate: dowry, whose name the children take, what happens to the estate.
- Afterwards the marriage is a relationship that moves: warmth, estrangement, infidelity, divorce where the law allows it, widowhood and remarriage.
- A spouse is a person with skills, a career and opinions, who can run the house while you are away.
- Children can be betrothed young, and a betrothal can be broken at a price.

**Education, skills and getting better**
- *Today*: a child comes of age at sixteen with inherited traits, a track and starting skills; skills move only by events and the Academy year.
- Each child has an upbringing you choose: a focus (arms, law, the counting-house), a tutor of some quality, or a place in another house's household.
- What a child becomes depends on that, on their parents and on what happens to them. Not every child takes to what they are taught.
- Adults improve by doing: commanding raises tactics, sitting as a magistrate raises law, running ventures raises economics. Slowly, with a ceiling set by aptitude.
- Adults can also study or be taught by someone better, which costs time or coin.
- Skills fade in old age. Some traits can be earned or lost in adult life.
- The Academy year becomes one case of this, and other kin can be sent there.

**Personal wealth, house wealth and the dynasty's assets**
- *Today*: one treasury per house. People own nothing. The heir receives everything.
- Each adult has a purse of their own; the house has its treasury; the head controls both the house's and their own.
- Kin receive an allowance, and ask for more. A generous head is loved; a mean one is plotted against.
- Assets can be held by a person: shares, a venture, a villa, an heirloom. What a person holds is theirs to sell, give or leave.
- A will says who gets what. The succession law says who gets the house. They need not agree, and that is a quarrel.
- Dowries and bequests move wealth between houses. Debts are inherited too.
- An entailed estate cannot be sold or split; the rest can. A house can be rich in land and poor in coin.

**Under the wing**
- *Today*: a house may be the client of a richer house. In the Academy events there is a mentor. Kin can be fostered away for safety. Nothing ties one person to another as patron and protégé.
- A person can take a protégé or seek a mentor: someone senior in their own service who teaches them, speaks for them at promotions, and expects loyalty.
- A ward: an orphan or a hostage raised in your household, or you in theirs.
- Adoption as the Romans did it: a head with no fit heir adopts a grown, able person, who takes the name. A talented nobody can be adopted into a great house.
- A house of one (the lone officer, the new man with no kin) can be taken into a greater household as a junior branch, keeping its name, until it can stand alone.
- All of these can go wrong: a mentor who falls, a protégé who outgrows you, an adopted heir the blood kin resent.

**Politics under a throne**
- *Today*: an Emperor exists; the Imperial Edict is a law only he sets; office fleets follow the office; he grants monopolies and, under proscriptions, seizes shares. Next to the Republic there is little to do.
- The court is a place: who has the Emperor's ear this year, and what it costs to be heard.
- Favour replaces the vote. Offices, commands, governorships and monopolies are appointments, won by favour, service, kinship or fear.
- A council of a few great officers, each with real powers, and rivals for each seat.
- The Senate remains: without the last word, but it can confirm, obstruct, and matter in a succession.
- The succession is the great contest: a named heir, an adopted one, the guard's candidate, a general on the frontier.
- The three orders mean something different: the Senate becomes the court, Arms can make an Emperor, Commerce funds the throne and is squeezed by it.
- A bad Emperor can be endured, managed, or removed, and each has a price.

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

- A thing is "done" here only when it is tested, soaked and checked in the browser.
- Numbers in section 1 come from the content files and the test run, not from memory.
- When a gap is closed, move it to section 2 and note the pass in `GAP_ANALYSIS.md`.
