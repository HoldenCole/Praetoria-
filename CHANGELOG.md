# Changelog

## [Unreleased]

### Fixed
- The orders, courtship and household panels kept showing last year's figures after End Turn until something in them
  was clicked. They now redraw when the world changes.
- *Peacemaker of the Reach* was awarded for other houses' weddings: the marriages counter now counts only the player's
  own house's matches, and the honour reads wars the house itself began (`wars_begun`), not every war in the Reach.
- The workshop's "Load a file" control is the same size as the chips beside it.
- A house's legitimacy could run past 100: it is now held to 0–100 where it is stored.
- **CI is green again.** The Godot layer's compile check had failed on every push since titles took an era: one call
  in `src/Game/GameState.cs` still used the old signature. The tests and the published game were never affected.
  `tools/ci.sh` now runs CI's exact steps locally.

### Changed
- A wound in battle now costs health, is written into the person's record and leaves a scar on their portrait.
- Someone under fifty can now die, if their health is very low.
- Nobody climbs a skill a step a year any more. Other characters used to drill their best skill every year without
  limit; training now adds learning toward the next step and stops at the person's ceiling.
- The skill chart on the character sheet runs to ten, not five.
- **You play one person, not a house** (docs/PROCESS.md). Kin now court for themselves: they choose whom, the suit
  advances without the head, and when it is serious they ask your blessing. You can bless it, forbid it (the loyal obey;
  the wilful may marry anyway), or help with the family. A match you arrange for a kinsman shows their own wish first,
  and marrying them against it is resented or refused. Their marriages are theirs: you tend and end only your own.
- A fleet or a world of the house left without an admiral or governor for a year is taken up by whoever of the family is
  fit and willing. The head can still appoint anyone.
- Another house can no longer marry one of your kin without asking: its proposal becomes a suit the head answers.
- A steward's proposal to someone you are courting becomes a rival suit, not a wedding.
- **Faster turns.** A house's laws are summed into one table per ledger instead of rescanning every region on each
  lookup; turn processing is about a third of what it was. The browser builds the projection once per action and
  memoises pure questions (Senate weight, law tables, who leans how) until the next one. `?perf` in the address prints
  where each turn's time went. The 3D Office simplifies itself on a machine that cannot keep up.
- **Marriages make sense.** Rival houses look for a match about one year in three, not all at once; they do not court
  across twenty years, nor a woman past bearing for a man with no child. The older match event has an age range.
- **A phone layout**: one column, a rail you swipe, wide tables that scroll inside their panels.
- **The Senate screen** has two tabs: the chamber, and your standing in the three orders.
- **No paid DLC.** Praetoria is a personal project. The five expansions once listed as a DLC slate are later chapters
  on the one roadmap (docs/ROADMAP.md sections 4 and 6), each with what it builds on and what it should follow. The
  README, the design document, the build spec and code comments no longer speak of DLC or paid releases.

### Added
- **A life of one's own** (docs/LIFE.md). **Health** for everyone: age lowers what it can be, wounds, illness and
  strain lower it, rest and physicians restore it, and it bears on how long a person lives. **A record** of what each
  person of the house did, from which come their renown and, for some, a name. **Old friends and old enemies**, whose
  deaths are felt. **Retirement:** a head may lay the house down in life and stay on as its elder; relatives lay down
  their work at seventy or when their health fails. **Funerals,** at which what is said is made from the record.
  A Life panel on the character sheet, the house's health and its dead on the Dynasty screen, seven events,
  `tools/browser/life.js`.
- **Ambitions** (docs/AMBITIONS.md), on the Dynasty screen. Every grown member of the house goes after what they want
  without being told: takes an empty command, founds a venture with their own money, puts their purse into a house
  that is short, builds a standing to rival the heir's. Now and then one asks the head for a command, a stake, a word
  for their promotion, or to be named heir; the head grants it or says no, and that is all. They remember which.
  Ten years of nothing and too many refusals leaves them thwarted.
- **A named heir.** The head may name anyone grown of the house to succeed them, ahead of the law's choice, at a cost
  in standing and the regard of whoever is passed over.
- Four events; `tools/browser/ambitions.js`.
- **Personal wealth** (docs/WEALTH.md), on the House screen. Every grown member of the house has a purse of their own,
  fed by an allowance the head sets (mean, customary, generous), by their service's pay and by any estate. What they do
  with it is theirs: they give to a house in trouble, spend on being seen, gamble, save, and run into debt, which the
  head may pay or leave until the creditors come to the house. **Heirlooms** belong to a person and can be given, sold
  or left. **The will** sends the head's purse and heirlooms where they say, while the house goes by the law and the
  treasury is kept whole or shared out in portions. Five events; `tools/browser/wealth.js`.
- **Education and skills** (docs/EDUCATION.md). Every person has a ceiling in every skill, set by what they are. Skills
  are learned in points, each step costing more: from their service, a command, a governorship, an office or a duty;
  from what they choose to study; from a paid teacher; from a mentor. Children from six are brought up toward their
  bent without being managed; the head may set another course (learned slowly, and resented) or pay a tutor. A skill
  taken to 7 reveals its aptitude. A Learning panel on the character sheet and an Upbringing panel on the Dynasty
  screen; three events; `tools/browser/education.js`.
- **The household** (docs/HOUSEHOLD.md), on the House screen. **Found family:** take a child in as a ward or a grown
  person as a protégé or companion. They keep their own names, live as members of the house, and stand in the line only
  after the blood. A head can adopt one as an heir. People also come to the door and ask. **A mentor** for your own
  character: a senior of your service who teaches you every third year. **Delegation:** hand the routine of the estates
  to a steward and the upkeep of the fleets to a marshal, any grown member of the house. They act once a year by their
  own skill, leave the reserve you set, report in a line, and may not be honest.
- Six events for the household; the `takeIn` event effect; `tools/browser/household.js`.
- **Love, courting and marriage** (docs/MARRIAGE.md), on the Dynasty screen. A courtship is won over years from the
  person (favour) and from their family (consent), one act a year: time together, a gift, the case put to the family,
  word against a rival. Rivals press the same suit and can win it. The contract is bargained when the match is proposed:
  the dowry, and whose name the children carry. Children of six and over can be promised and wed at sixteen. A suit
  another house brings for one of your kin waits for your answer. At high favour a couple can marry without leave.
- **Marriages have a warmth** that moves with the years. Warm ones bear more children and ease the head; cold ones bear
  fewer; the coldest live apart and bear none. The head can tend their own marriage, or divorce. A widow or widower is
  free to marry again.
- Twelve events for courtship and marriage; three event effects (`courtship`, `marriage`, `beginCourtship`); alerts for
  a suit to answer, a rival ahead and a suit waiting on the year's attention.
- `tools/browser/courtship.js` plays a suit to its end in a headless browser. The soak's random player now courts.
- **A character of your own** (docs/CHARACTER.md), the first card on the title screen. Six tabs set the person (age,
  service, rank, ambition, natures, aptitudes, thirteen skills), the face (skin, hair, cut, beard, a scar), the house
  (name, old or new, rank, legitimacy, culture, party, colour, the whole treasury, a seat, a patron), what it holds
  (worlds, hulls, ventures), the family (spouse, children, siblings) and the world (Republic or Imperium, in the world
  or at the Academy, a friend and a rival). **Standard** keeps to a 60-point budget; **Free** has no budget and marks
  the run as custom. A power rating shows either way. Four presets, a throw of the dice, and save and load as a file.
- **A patron who notices.** The docket says what your patron wants of a motion that touches them; voting with or
  against it moves the patron's regard, with an event either way.
- **Matches for the unwed**: *The Patron's Match* for a client with no heir, and *A Match Is Proposed* for any unwed
  head. *The New Man* now founds a line in every unattended run.
- **Companies found ventures** from their own cash. Stewards of companies do the same, and a house with a tenth of a
  company whose director holds less than half keeps buying. A house many others depend on moves for the office its
  order fits.
- **Checks in the repository**: `tools/browser/play.js` (screens, turn times, the Exchange, phone width) and the tools'
  `perf`, `survive` and `balance` commands.
- **Roadmap: the person and the family.** Five pieces added and tied in: love, courting and marriage; education and
  improving skills; personal wealth beside house wealth and the dynasty's assets; being taken under a person's or a
  family's wing (mentors, wards, adoption); and politics under a throne. "What comes next" is now grouped by theme
  with what each piece leans on, not numbered, with a custom character the one fixed next step.
- **A roadmap** (docs/ROADMAP.md): what is done, the known gaps, the next ten cycles in order (a custom character first), a planned look-and-feel
  pass for screens and characters (not started), and ideas for later expansions. The README's status sections now
  point to it.
- **The three orders of the Republic** (docs/REPUBLIC.md). Every house now has a standing in the Senate, in Arms and
  in Commerce, measured each year from its rank and offices, its hulls and victories, its wealth, ventures and paper.
  A family is known for one (its calling); a few are great in two. Standing is leverage: a great general or banker
  carries extra seats; a house may sit in the Senate by land, the bench, the sword, or the purse (enrolment by the
  census for 60 credits); and houses lean toward whoever they depend on (patron, creditor, the house whose fuel their
  fleets burn, the director of a company they hold). Patrons take clients by paying their debts; clients vote for
  their patron at an election. Coin may be spent on the voters in any nominee's name. A new name in a consul's chair is
  marked. New start *The New Man*: no land, no seat, one trading company and a patron. Sixteen events. The Senate
  screen has an orders panel; the Houses screen shows every house's three standings and offers patronage.
- **The economy expansion, all four stages** (docs/ECONOMY_MARKET.md §10). A new **Exchange** screen with four tabs.
  *Goods*: six goods with a price that follows what the houses make, burn and fight over; buy and sell both ways; a
  large order moves the price against itself. *Ventures*: a venture is worth six years of its earnings, houses bid for
  yours and ask a price for theirs, and the holder of the world has first refusal; four new kinds (warehouse, arsenal,
  shipyard, insurance). *Projects*: buildings and ventures take one to four years and are paid by the year; pause,
  rush or abandon. *Shares*: incorporate a venture as a company, float shares to raise capital, buy and sell any
  listed company, set the dividend, and take control with more than half; a founder may refuse a takeover once. Share
  prices are read each year from earnings at market prices, the sector's outlook (war lifts arms and sinks the banks),
  confidence in the Reach, and flow. Three great companies are chartered from the start. The Reach sells war bonds for
  a war abroad. The Senate may tax share trades, break a company up or rescue the Exchange; the Treasurer may steady a
  falling market once a term; under the Imperium the throne grants monopolies and, with proscriptions, seizes shares.
  Thirteen events on the market's own flags, and a ninth Academy lesson.
- **The plan for the economy and the Exchange** (docs/ECONOMY_MARKET.md): prices for every good driven by war, siege,
  raids and scarcity; ventures as assets that change hands; building as projects with a prospectus; then companies,
  shares, capital raises and takeovers. Written before any of it was built; it lists the decisions that were the
  owner's.
- **The Academy is a year apart from the world** (docs/ACADEMY.md). A run that begins there opens with four terms
  before turn one: the cadet is sixteen, the world does not turn, no world order is taken, and only the Academy's own
  events come. The cadet reads a subject a term, climbs an order of merit against eleven real sons and daughters of the
  houses, and passes out at seventeen into the Navy, the Administration or the Courts. In the Republic it is a school
  (lectures, a debating society, a regatta, a war game on the table, examinations) and nobody is hurt. Under the
  Imperium it is the Institute: four Standards at war, and cadets who do not come back. It is also the tutorial: nine
  lessons taught in full to a first-time player, or the short course with the state of the Reach, the houses that
  matter and the cohort for a returning one. New starts *The Academy* and *The Institute*.
- **Titles follow the era** (docs/TITLES.md). The Republic has no nobles: houses are Citizen, Warden, Prefect, Legate,
  Proconsul, First Citizen; services top out at Fleet Admiral, High Treasurer, High Justice. Noble and imperial names
  (Baron, Duke, Grand Admiral, Imperial Treasurer) appear only once a throne stands. Prose follows with `{role.title}`
  and `{era:Republic wording|Imperium wording}`.
- **Foreign nations, off the map** (docs/FOREIGN_NATIONS.md): five nations beyond the rim with strength, temper, wealth,
  distance and regard. The Senate makes war on one by motion; houses send expeditions that leave the map, sail the
  distance, fight a season of battles with the real fleet math against the nation's committed strength, and come home
  with plunder, valour and fewer hulls. A war ends in subdual (twenty turns of tribute to the chair, a triumph for the
  house that won it), in a bought peace, or in exhaustion. Aggressive nations raid the rim when their regard falls.
  NPC hawks move the war and send fleets. Military screen panel, Senate picker, alerts, ten events, tests.
- **The consulship** (docs/REPUBLIC.md stage 2): two elected offices, the Senior and Junior Consul, filled every ten
  turns by the parties' weight. Each party nominates its weightiest eligible member; the player may stand (two
  influence) as their party's nominee or alone; a house with no party votes for the nominee its head likes best; no one
  is consul two terms running. Consuls weigh more, draw a stipend from the chair, move motions cheaper, and hold a veto
  — one motion a term struck from the docket before the vote; rival consuls veto what strikes at their house, their
  party's leader or their party's line. The Senate shows the consulship, the nominees and their tallies; four events.

### Changed
- **Four parties, not a crowd of benches** (the owner's direction; docs/REPUBLIC.md stage 1): the Optimates, the
  Populares, the Sword and the Factors ship in content with a colour, the estates they court, positions on the laws
  and stances on the kinds of motion. Every house is seated by its laws at the world's creation, each party with at
  least two members; a house with no party takes the best-fitting seat; every tenth turn one rival whose laws have
  drifted crosses the floor. Benches no longer form of themselves — the player founds the fifth, in silver, on their
  own law. Members lean with the party line on motions and Acts. The Senate shows the parties' colours, courted estates
  and platforms.
- **The palette**: one bright, well-separated set for every house (red, blue, yellow, green, silver, then orange,
  violet, cyan, magenta and the rest), assigned at creation and at a house's birth; regions take soft-bright tints of
  their own rather than the dominant house's colour; parties take a colour as they form; planet classes are brighter and
  more distinct in the dots, the sprites and the Office window.
- **Dashboards** (the owner's direction: the Office and the map keep their sets; every other screen is an information
  surface, each with its own graphic). The **Senate** is a chamber view in the Stellaris-galactic-community and
  Lawgivers mould: a header strip (seats, standing, bench, threat, pressure), a hemicycle of every seat coloured by
  house, grouped by bench and rimmed by its lean on the watched resolution, a seat card with whip, bribe and connection
  actions, the spheres of power, the resolutions with tallies and verdicts, the offices, benches, ladder and crises.
  The **House** is a ledger register: resource tiles with the forecast, a signed bar chart of where the coin comes
  from and goes, the wealth table, ventures and claims, a domain board of world cards with loyalty, prosperity and
  population gauges, the household with a person card, and the decrees. The **Chancery** is an estates board (regard
  bars with the loyal and hostile bands and the target tick, what each estate would warm to, petitions near) beside a
  law grid by category with the option in force and its opinions on every card, the law of the Reach and the laws
  beyond your gift aside. The **Command Deck** is a war plot (every system and lane, fronts in red, your supply in
  gold, the campaign's object ringed, your fleets and the enemy's as reported), the navies as bars, fleet cards with
  readiness, loyalty and supply gauges and every control, the battle brief, the campaign, the wars with their score
  tallies, and the dispatches. The **Houses** are a sortable standings table with bars for worlds, hulls and seats
  and the focused house's card; the **Line** (Dynasty) is a family tree by generation with the wed beside and the
  succession, bonds and remembered aside; **You** is a skills radar, the career ladder, traits, and bonds as centred
  disposition bars; the **Worlds** are a board by region with loyalty and prosperity bars, riches and deposits, and a
  regions panel; the **Chronicle** is a timeline strip of every turn with volumes to open and the run's counters in
  the header; the **Academy** is the cohort by skill with your standing against the centre line. Portraits now paint
  themselves wherever they appear.

### Added
- **Military depth** (docs/MILITARY.md; the owner's ask for roles, promotions, campaigns, decisions in combat, weapons,
  tactics, the front-line commander against the general against the Achilles figure, loyalties, supply lines).
  **Tactics**: every fleet fights in a stance — line, press, skirmish or boarding — on a counter wheel (the line beats
  the press beats the skirmish beats the line); boarding takes prize hulls for manpower; a torpedo loadout burns an
  isotope a battle for a quarter more power. **Command**: a commander leads from the front (+10%, the crews' love, a
  point of valour a victory, twice the risk; three victories make an Achilles) or from the flag (tactics lent to every
  fleet, never lost with the line); a fleet larger than its commander's cap by rank fights at nine-tenths. **Supply**:
  fleets drain beyond a lane of home (two with a Fleet Supply Depot), fight at four-fifths when short and lose hulls at
  nought. **Campaigns** name an enemy world as the war's object for eight turns: fleets within two lanes fight harder and
  count as supplied. **Loyalty**: the crews' regard for the house drifts with pay, fuel, supply, victories and the
  officers' estate; restive crews will not sail, mutinous ones desert. Rival houses choose stances by the odds, load
  torpedoes, declare campaigns and pick a command style by rank and age. The Military screen gains the battle brief
  (odds and the stance edge for every sky an enemy shares), stance, loadout, loyalty, supply and command controls, and
  the campaign panel; the outliner warns of battles, restive crews, short supply and a campaign's end. Thirteen events
  (the front or the flag, mentioned in dispatches, the line holds, the line breaks, the Achilles name, the prize crews,
  the long supply line, the restive crews, the mutiny, the campaign opens and ends, the torpedo run, the general's
  sway). Design notes for the Republic's parties and consuls (docs/REPUBLIC.md) and for foreign nations off the map
  (docs/FOREIGN_NATIONS.md).
- **Role theming** (GDD Art Direction, "by character's job"): the shell carries a `data-role` register beside the era's,
  from the protagonist's career track. The **military** register is functional — a technical sans for titles, squared
  console frames with corner ticks, steel in place of brass, the Military and Galaxy screens first on the rail, the
  Admiral first at the desk, every briefing card headed DISPATCH, a blinking signal lamp, a model hull and a chart tube
  on the desk. The **stewardship** register is administrative — ruled ledger panels on a parchment tint, solid rules
  under every figure, the House and Worlds first, the Steward first, cards headed LEDGER, ledgers, a brass balance and
  an abacus on the desk. The **law** register is ceremonial — engraved double frames, ornaments on every header, a
  brighter brass, a halo on the decision window, the Senate and the Chancery first, the Chancellor first, cards headed
  THE COURT, a gavel, a codex and a robe stand. The register changes at succession: the seat-passing card says what the
  heir's game will look like, and the heir lands on their register's home screen. Under the Imperium the era's gold
  still wins.
- **Twenty-six new situations** (`content/events/situations.json`), hooked to flags the engine already raised but no
  event read: the Senate's refusals (your motion thrown out, an Act that falls), the embargo, the Speaker's gavel, the
  bench leader's table, the loyal estates (the commons' feast, the officers' toast, the merchants' gift, the old blood's
  invitation), your own laws biting (the press gang, the runaway bondsmen, the barred door, the open hall, the ballot
  box, the iron law, the marque captains), the siege bell, after the raid, the lost province, the free march, the
  isotope broker, the helium glut, the empty hall of a fallen house, and three ambients of a long life (the portrait,
  the comet, the old retainer). 180 events.
- **A scripted reachability check** (`tests/EventReachabilityTests.cs`): every event the randomised soak never reached
  is built by hand — flags raised, roles seeded — and must be eligible. All twenty-two are; their silence in the soak
  is rarity, not a dead trigger. The soak itself now samples world flags every turn and names the flags content reads
  that no run ever raised, so a dead trigger shows up as a flag rather than a missing event.
- **QoL, politics**: the docket says outright whether a motion *carries* or *fails* as the benches lean; the outliner
  and the Chancellor warn when a motion is decided this session and your vote is undeclared, and when an Act is before
  the chamber; each estate card in the Chancery names the one law change in your gift that would warm it most, with its
  cost; the Act picker shows each option's estate opinions; the Chronicle gains a *politics* filter; the primer gains a
  card on the laws and the estates.

### Fixed
- A house struck from the rolls took its open motions with it only in name: the docket now drops a dead house's own
  motion (with a line) and any motion aimed at it.
- The QA soak no longer tries to move a petition as the player (only an estate brings one).
- The four xUnit analyzer warnings in the test suite.
- **Graphics: the Chancery and the chamber read their politics.** The Chancery's walls now hang six estate tapestries,
  each woven in its mood's colour with a glyph and a regard bar at the hem, lit by a lamp that burns steady for a loyal
  estate and gutters for a hostile one; a brass bell swings beside an estate that is petitioning. The desk carries a
  sealed scroll for every law set off the custom and a bound Act when the Senate has carried one. In the Senate, a
  petitioning estate's delegation stands in the gap of the colonnade under its own standard, gold-lit; petition cards on
  the docket carry a gold edge.
- **The estates' petitions** (GAP §16 item 8): an estate hostile in three houses or an eighth of the Reach brings its
  own Act before the Senate for the law it wants. Heard, its regard rises in every house; refused, it falls and the
  worlds of every house it distrusts stir. Petitions show on the docket under the estate's name, on the Chancery's
  estate cards, in the outliner and the Chancellor's report; three events (*The Petitioners*, *The Petition Heard*,
  *The Petition Refused*).
- **Acts of the Senate** (GAP §15 item 8; docs/SENATE.md): a motion kind that sets a law for the whole Reach. Three
  Senate-scoped laws — the Common Law, the Trade Charter, the Levy of the Reach — can be set by no house, only by an Act
  carried in the chamber. Rivals lean by their estates (pleasing a hostile estate counts double) and by situation (war
  wants the common levy, lawless worlds want the assizes, navies want open lanes); rival proposers move the Act that
  most pleases an estate hostile to them. The Senate's propose drawer gains a law-and-option picker, the Chancery a
  "law of the Reach" drawer that names who set each reach-wide law and the Senate's cooldown; two events (*An Act Before
  the Senate*, *The Act Carries*). The estates of every house now also read the Reach's laws.
- **Rival laws by culture and estates for every house**: each house begins with its culture's laws (the imperial
  core's closed court and iron law, the rim's lenient courts and open lanes, the spirean harsh tithe and bonded
  labour…), every house has its own six estates with the same loyal and hostile effects, and rival stewards legislate
  — relaxing what a hostile estate hates, tightening the tithe when poor, conscripting at war, declaring martial law
  over lawless regions, raising the chair's tolls. The Houses cards show a rival's law and its estates' moods; *The
  Exiles* brings a harsher house's commons to your port.
- **Laws and estates** (GDD §6.1 legislation; docs/POLITICS.md): seventeen laws in seven categories, each with a scope —
  the position you must hold to set it: a head of house, the holder of a world, the dominant house of a region, the
  chair, an office, the throne, the leader of a bench — and three options whose effects the economy, the regions, the
  fleet, the Senate and the open market read. Six estates — the Commons, the Merchants, the Officers, the Old Blood,
  the Magistrates, the Frontier — hold an approval of the house that drifts toward what its laws say, and loyal or
  hostile they help or hurt it. A Chancery screen (Laws) shows the estates and the laws in your gift; six events let the
  estates speak. The tithe is a law now.
- **Persistent Senate factions** (GDD §6.1 "voting blocs"): every decided motion leaves an affinity between the houses
  that declared; benches form around the weightiest of three friends, grow, break and dissolve on their own, and the
  chamber names them (*The Rim Benches*, *The Old Blood*, *The Court Party*, *The Purse-Strings*…). Members lean toward
  their leader's stance and a whip on the leader is half a whip on the bench. The player takes a seat, leaves, founds a
  bench, or breaks the whip and pays for it. Five events, the benches on the Senate screen with a tag on every bench row.
- **The offices of the Reach** (GDD §8): the Admiralty, the Treasury and the Speaker's gavel, granted by a Commission
  motion to a house whose member has the rank on the office's track. The Admiral's house commands the office fleets
  (the Reach Squadron, three frigates and a cruiser at the capital; the Imperial Home Fleet under the throne) and fights
  its wars with them while the chair pays their upkeep and the state fuels them — and loses them for treason the turn it
  makes war on the chair. The Treasurer draws a stipend from the chair; the Speaker weighs three more and moves motions
  for less. Rivals move commissions for themselves; the Emperor holds every office by the throne; a fallen house's
  offices fall vacant. Five events, the offices on the Senate screen, the Admiralty on the Military screen.
- **The Senate as a place of action** (GDD §6.1; GAP §10 item 2): houses move motions — censure, recognition, the
  levy, relief for a region, the Peace of the Reach, an embargo — and every house votes by weight (Senate influence,
  title rank, the chair's two, a Praetor's one). Rivals lean by interest and move their own business; the player votes,
  whips with influence or bribes with coin, and sees every bench's lean before the vote. Carried motions lift the
  proposer's standing in the Senate sphere. Six events; the docket, the whip count and the history on the Senate screen.
- **Onboarding and the seat passing** (GAP §10 item 1): a five-card primer on the first turn of a run (remembered per
  browser; reopen it from the ? in the top bar), and a succession interstitial with both portraits, what passes with the
  name, what does not (the career), who the heir is, and a regency if the heir is a child.
- **The strategic three** (GDD §17 "not every world has everything"): helium-3, rare earths and isotopes come only
  from deposits the galaxy placed (nineteen in the Reach — giants, belts, dead worlds). Helium Skimmers, a Rare-Earth
  Refinery and Isotope Works (buildings), a Skimmer Fleet and an Enrichment Concern (ventures), and belt claims bring
  them up, richness multiplying the yield. The fleet burns helium-3 (one a turn per three hulls; dry tanks hold it at
  anchor); cruisers and carriers want rare earths, battleships and bastions isotopes; a sensor array and a planetary
  arsenal spend them. An open market sells fuel and rare earths at a scarcity price — never isotopes — so wealth buys
  what geography withheld. Six events, two new conditions (`wealth` tier, `deposit`), map glyphs, planet-card deposits,
  a fuel line on the Military screen, and stewards who work deposits, buy fuel, sell hoards and buy the rare earths a
  heavy hull wants.
- Quality of life: the Houses roster counts the names that have passed out of the rolls; the chronicle's realm volume
  carries fallen houses and fiefs; the outliner marks a fief granted and a house fallen; the petition names its grantor.
- **Houses that fall**: a name with no one left is struck from the rolls; a free hold whose world is taken folds into
  the taker; a landless, penniless, hull-less house enters another's service after twelve years, its debts written
  off (`houses_fallen`). **The patron's fief**: when the court holds only the capital, the greatest landed house at
  peace grants the fief, and landless rivals court the grantor first. Fortress worlds earn a garrison credit and a
  levy of manpower.
- **A QA player who acts**: the `qa` soak's randomised player now gives commands as well as answering events — every
  legal intent listed each turn, a shuffled few tried — and reports the coverage; distinct events surfacing rose from
  90 to 101 of 120.
- **Raiders** (lawlessness with a face): where a region's lanes go lawless a raider fleet gathers under The Lawless, at
  war with everyone; it roams, plunders unguarded worlds and is hunted by any fleet that meets it; the map shows raiders
  as a red skull, the outliner warns of them, and two events bring them to the door.
- **Governors who scheme**: loyalty and ability on the ledger, an honour for being named, and an ambitious hostile
  governor on a restless or rich world who declares it for themselves; the House screen shows each governor's loyalty.
- **Wars that end, and have prizes**: exhaustion ends a rival war with no battle in twelve turns; a peace cedes a world
  to the side ten ahead; rival fleets at war sail to hold the chokepoint between home and the enemy or strike the
  enemy's weakest world.
- **The fief**: a landless house petitions the court for a world; rival landless houses do too.
- **The pools as a constraint** (GDD §9): cap six, regenerating 2/2/1.
- **Era theming**: the Imperium gilds the chrome. **Route preview**: a selected fleet's lane-by-lane path to a selected
  system. **The capital wears a crown** on the map.
- Quality of life: the resource strip's tooltip lists the ledger; the chronicle filters by war, dynasty, coin and
  realm; the Houses roster sorts by title, wealth, disposition or hulls; the outliner warns of raiders, revolts, free
  holds of yours, and the capital changing hands; the Military screen counts raiders among the navies.
- **Free holds as actors**: a world that breaks away raises a militia, trades and stakes like any house, and remembers
  its old lord; the old lord may buy it back for a price that carries the leader's grudge (the leader keeps the
  governor's chair under the old banner, and a free house left with nothing is folded away) or make war on it, and
  rival lords do both. *The Free Hold Sends Word* brings it to the player.
- **The capital as a prize**: its holder has the Senate's chair, a point of legitimacy every third turn, a richer
  world, and five threat; a strong rival declares for the capital once a decade; its passing is a chronicle moment
  with *The Keys of the Capital* and *The Capital Falls*; the Houses screen names its holder.
- **The throne stands in the capital** (GDD §19): seizing it needs a fleet of yours in the capital's orbit; being
  proclaimed needs the capital to be yours or its holder to have knelt; the War of the Reach crowns its victor only
  after a decisive field *and* the capital, and otherwise ends in exhaustion with no one crowned.
- **Fortress worlds earn escort fees**: a credit a turn for each neighbouring system another house holds and is at peace
  with, up to three, so a fortress house lives by its guns rather than by luck.

### Fixed
- The offices' one-turn flags (`office_granted`, `office_lost`, `treason`) were cleared by the office tick right after
  the motion resolution had set them, so *The Commission* and *Relieved of Office* could never fire; all Senate, office
  and bench flags are now cleared once, before motions resolve.
- Rival careers never advanced: three hundred rival adults sat at rank zero for sixty years, so the spheres, the Senate's
  weights and the offices were the player's alone (found when the offices probe showed one house in the Reach qualified
  for the Admiralty). Every adult outside the player's seat now climbs a rung every nine turns, heads every six, never
  faster than a rung per six years of adult life.
- CI had been red since the strategic three: the pre-flight validator's locked vocabularies lacked the new resources and
  the `wealth`/`deposit` conditions. The Military screen listed the Lawless both as raiders and as a rival navy, put the
  fuel line among the rivals, and called a fleet without an admiral "unled". A scenario's starting tank lasted nine turns;
  it is ten helium-3 plus two a hull now, and the Admiral's report says when the tanks are low or dry.
- A character born or raised in play had no action budget at all (an empty pool with a cap of zero), so a rival house
  whose founding head died could never again raise a hull, sail, buy or build — only its scripted stewardship ran. Any
  character the world meets without a pool now gets a rival's lean budget. Found by the fuel probe: rich houses sat dry
  for twenty turns with "no treasury action left".
- The chronicle's war volume was a roll of raider movements ("… arrives at The Veil — House Quill's space", several a
  turn); raiders are now heard of only when they reach someone's world, and a scattered band needs six years to gather
  again in the same region. The map's "fit the whole Reach" clipped the outermost systems and ran region names off
  the edges; it has room now, and region names stay inside the map.
- Rival stewards staked belt claims every turn they could afford one (six claims, no coin); capped at two unless the
  ledger is comfortably in the black. The Lawless ran up a debt paying hull upkeep; they keep no ledger now. Two
  academy set-pieces (*The Marked Man*, *The Frame Unravels*) asked for a protagonist aged 22 or under on a turn no
  academy protagonist could still be; widened.
- **Half the Reach went bankrupt and stayed there.** Rival stewards founded ventures in alphabetical order (Academy
  Patronage first, a credit drain), borrowed on a sinking ledger (a loan every seven turns, each deeper), and let
  materials pile up unsold; a house whose last member died kept its worlds and let them rot at unrest 100. Stewards now
  found the venture that clears the most coin and nothing that loses it, sell a losing venture before borrowing,
  borrow only when the ledger is positive, sell materials in smaller lots, and stake claims on a thinner cushion when
  their land earns nothing; a headless rival house raises a cousin from the provinces to take up the banner.
- The court offered a marriage match and an "accident" against children of other houses; neither is listed now, and the
  assassinate command refuses a target under sixteen outright. The Houses card names the system of each venture.
- The simulate harness's auto-player now weds, so sixty-turn soaks have heirs; the fallen house no longer "dies" every
  run for want of a marriage the harness never proposed.

### Changed
- **Territory as areas, not bubbles**: the map is partitioned the way Star Dynasties does it — every point belongs to
  its nearest system's region, regions are crisp bordered areas in the dominant house's colour with a dark tinted fill
  (hatched for a free march), region names sit at the centre of their area and no longer collide, and the minimap shows
  the same partition. The per-house glow field and the ring ellipses are gone.
- **A quieter map** after a second look at the reference games: lanes are hairlines with no glow (fronts and your own
  reach excepted), the default view opens on your own seat with the Reach running off the edges, a minimap in the
  corner shows the whole with the viewport drawn on it (click to travel), a fit button shows everything, system
  markers and orbit rings are smaller and fainter, region fills lighter, and labels thin out as you zoom away.

### Added
- **Reactive events on the engine's own flags** (GAP_ANALYSIS §4 item 4): the enemy's envoy under a flag of parley, the
  assassin's knife traced to its house, the coalition gathering at one inn; the engine now also writes the generic
  `at_war_any` and `assassin_any` flags content can read.
- **Region names avoid system names too**, by a bounded search around each region's centre.
- **The galaxy as a galaxy** (GDD §19): the map is laid out twice as wide; every system carries two to five bodies
  (worlds, moons, giants, belts) drawn on their orbits around the star; regions are highlighted as soft areas around
  their systems; the lane web is sparse (forty lanes, under three a system) and runs through gateways; **chokepoints**
  are computed from the web, pay **tolls** to the holder of their world, and bar passage to an enemy while the holder's
  fleet sits there; the map marks them and the planet card lists a system's bodies, lanes and whether it is a chokepoint.
- **The rings of the Reach** (GDD §19; `docs/GALAXY.md`): the galaxy grows to ten regions and thirty-one systems laid
  out in three rings with a **capital**. The **core** is commerce and the court (more coin, doubled charters, order);
  the **rim** is lawless and restless (raiders tax the ledger, unrest climbs, allegiance to the capital settles low);
  the **middle** leans whichever way it is pushed. Every region carries **allegiance** and **lawlessness** that drift
  each turn; a restless world in a disaffected region **rises** and yields nothing; a garrison ends it; left four turns
  it **breaks away** as a free hold under a new house; a region's dominant house that has stopped believing in the
  capital **declares a free march**. Four new regions with eleven houses, two worlds no one holds, five ring events,
  the player's decrees on a rising, NPC fleets that sail to put risings down, and the rings drawn on the map.
- **The economy, deeper** (GDD §17; `docs/ECONOMY.md`): every holding has **prosperity** (a governor's administration,
  what is built, the tithe, unrest and blockade move it; it scales output; an investment lifts it); systems carry
  **asteroid belts** and houses stake **claims** in them (the landless anywhere); **ventures** — trading companies,
  mining concerns, shipping lines, merchant banks, salvage guilds, spice syndicates, free companies, academy patronage —
  founded, expanded and sold, paying a **charter** to whoever holds the world; the standing **loan** with instalments
  and a lender's interest; **house wealth** reckoned and ranked every turn with tiers from destitute to opulent. A
  per-turn **ledger** on the House screen shows every line; the Steward reports your rank; NPC stewards borrow, invest,
  stake and trade; five venture events; the two landless houses of the Reach each start with a venture.
- **Houses, Worlds, Chronicle and You screens** on their own lit 3D sets: the hall of banners (every house on the walls,
  heads on plates, the greatest nearest the crest, war under a red pulse); the orrery (a dome, an armillary sun, one arm
  per region with the worlds as turning globes); the archive (stacks, the chronicle as a framed timeline, a volume per
  ten turns on the reading table); the quarters (a viewport, the family on the mantel, a trophy case of honors, a desk
  with the record of service). Every screen in the game now stands on a set.
- **Dynasty, Military and House screens** on their own lit 3D sets: the family tree as generations down a vaulted hall
  (parent-to-child lines on the floor, spouse threads, other houses' spouses in their colours, heirs and the remembered);
  the command deck with a holographic strategic table (fronts in red, fleets as markers), the admirals at the table, the
  fleet outside the glass, readiness / hulls / fronts on the wall; the keep's great hall with the worlds on pedestals
  (restless ones under a red light), the household at the long table, treasury, decrees, holdings and their chairs.
- **Senate and Academy screens** on their own lit 3D sets: the political system (spheres of power, threat and coalition
  pressure, the ladder of titles, the throne, laws, honors, crises) in a domed chamber with the great houses seated by
  influence; the cadet year on a parade ground at dawn with the cohort in ranks. Galaxy polish: textured lit planets,
  star classes with flares, glowing lanes, hover tooltips, smoothed camera, merged territory fields.
- **Art direction** (`docs/ART_DIRECTION.md`) from the reference games, and the first pass against it: the galaxy as a
  rendered WebGL scene (shader nebula, bloomed stars, suns, hyperlane web, territory fields with lit borders, lit
  planets, fleet glyphs); the office as a lit 3D set (reflective floor, viewport onto the seat's world, holographic star
  table, banners, sconces, framed cameo plates for the player and visitors); portraits as drawn cameo busts; game-icons
  glyphs; smoked-glass chrome with brass hairlines; Cinzel titles. No stock photographs, no avatars.
- **Grand-strategy shell** for the browser client: resource strip with forecasts, navigation rail, outliner, event
  windows with the cast, End Turn dock; screens for the Office (a furnishable SVG room visitors walk into), Galaxy
  (regions, planets by class, pan/zoom, planet cards), Worlds, House, Military, Dynasty, Houses and Chronicle; a
  succession interstitial when the seat passes.
- **The Reach expands**: six regions of three worlds with cultures and descriptions; twenty minor houses with people,
  planets and stations merged into every scenario; governors on every holding and a command to appoint them; planet
  and station kinds; five new industries.
- **Browser client** (`src/Web`): a Blazor WebAssembly build of the game — creation, Court, Galaxy map, Dynasty tree,
  Chronicle, saves in the browser — published to GitHub Pages from `main` and linked at the top of the README.
- `praetoria qa`: the QA soak — randomised player over every scenario, determinism replay, save/load round-trip, event and
  choice coverage, unresolved text tokens, chronicle oddities, engine flags no content reads.
- Unpaid fleets lose readiness and desert past 20 credits of debt; NPC stewards sell on the exchange and adjust the tithe
  when insolvent; the exchange works without a market at half yield; forge-worlds tithe a credit.
- Marriage refuses kin within two generations; the Binder never fills a role with a child unless the role speaks of age.

### Fixed
- The First Morning now reliably opens the Academy; crisis logs name their author; a mother bears at most one child a year.


All notable changes to Praetoria. The format follows Keep a Changelog; versions follow SemVer with 1.0 = the
Build Spec's V1 ("a complete human game").

## [0.8.0-alpha] — Unreleased — "The War of the Reach"
Twenty more systems plus the first QA pass driven by the new balance simulation (243 tests).

### The Great Crisis (Milestone 6)
- The human galaxy-war as a data archetype: a clock (base rate × intensity + the galaxy's turmoil), stages with
  flags/effects/behaviours (blocs → bloc war → climax → resolution), the hegemon's bloc vs the alliance, NPC-vs-NPC
  wars, and a resolution that ends the wars and births an Imperium. Crisis-intensity knob (off/low/default/dominant).
  `bloc` condition, `joinBloc` effect; four stage scenes (Rumblings, The Blocs, The War of the Reach, After the War).
### Dynasty & character
- Family autonomy (knob): willful kin grow defiant, court your rivals, and may defect — carrying a claim to your title
  with them (*A Will of Their Own*, *The Kin Who Left*). Passed-over siblings resent the new head. Elders lose their
  edge. Children keep the name of the seat (a ruling woman raises heirs to her own house) or of the greater house.
  Suits for the protagonist's hand become *The Suit*; NPC courtship prefers matches that can bear heirs. Heir-hunting
  has a cooldown. `sex` condition; the match event excludes kin and same-sex matches.
### Economy & war
- Blockades starve and revolts empty worlds; growth halves under unrest. Refit at home for materials. NPC houses buy
  materials at the Exchange and raise megastructures when rich. Threat is weighted by absolute influence. Counter
  recovery rules (imperial legitimacy and unrest heal between crises). Two more honors (Peacemaker, Lord Protector).
### Advisors, Director, tooling
- Advisor reports each turn (Steward, Admiral, Chancellor) — the Admiral speaks from intel, not truth. Director keeps
  ambient beats to one when weightier reports wait. `praetoria simulate` runs many seeds and tabulates wars, battles,
  conquests, dynasty deaths, thrones, crises, births, plots. Save format v2 with a migration hook. The Old Lion: a
  sixth scenario that starts with three generations in play. `docs/PROCESS.md` records the development cycle.
### QA (from the simulation)
- The circling heir had no Reach to circle in (added House Thalen) and her line died out (heirs now keep the seat's
  name); cadet events fired for grown nobles (now gated to a young protagonist); Cruel enemies hunted heirs every
  season (cooldown).
### UI (compiles; unrun)
- Great-Crisis card, advisor lines atop the briefing, refit buttons, defiant-kin notices, bloc rings on the map,
  intensity and family-autonomy knobs at creation.

## [0.7.0-alpha] — "Blood and Iron"
Twenty-odd systems in one autonomous pass. Everything headless is tested (224 tests); the UI additions compile.

### Military
- Ship classes as data (corvette → battleship; attack/defense/cost/upkeep; title-rank and specialization gates);
  fleet composition; losses fall screen-first. Admirals (assign command; skill and traits multiply power; victory
  teaches, defeat wounds or kills). Veterancy. Sieges with a blockade clock that halves yield. War score →
  tribute and peace refusal; war weariness. Titles follow the land (a landless house loses its rank; the conqueror
  gains a claim). Merge fleets.
### Dynasty
- Children named from house cultures; coming of age with inherited traits, a track, skills and an ambition.
  Succession laws (primogeniture, ultimogeniture, seniority, merit). Fostering hides kin from events and knives.
  Assassination with odds, trails and consequences; NPC heir-hunting in war. Marriage proposals (claims from
  marrying up); NPC courtship. Real regents. Resentment of the risen (`parvenu`).
### Strategy & endgame
- The Exchange, tax policy, territory contiguity, megastructures (title-gated), the difficulty knob. Proclaim or
  seize the Imperium (era flip, office fleets pass, the deposed house marked). The saga epilogue. Director: one
  set-piece per briefing, ambition-weighted events, lull-driven complications.
### Character
- Honorifics & Orders (five, condition/effect lists). Nature-trait friction: stress for acts against your nature.
### Content
- 17 new events (90 total): coming of age, the regent, the old blood, the knife turned, the plot exposed, the
  great work, the butcher's bill, the conquered world, the lost world, the envoy, the proclamation, the wedding
  feast (a Bloody Event), and more; 14 events tagged with ambitions; two name cultures; six backgrounds.
### UI (compiles; unrun)
- Shipyard per holding by class; command and merge of fleets; siege clocks on the map and in the Admiral panel;
  tax, exchange, succession law, throne and intrigue actions; foster/recall; dynasty tree overlay; difficulty at
  creation; the epilogue card.

## [0.6.0-alpha] — "the Court opens"
### Added
- **Godot layer (Milestone 3, first draft):** `GameRoot` (top bar, Court/Galaxy toggle, End Turn, Save, Load),
  `CreationScreen`, `CourtView` (advisors · briefing feed · dynasty rail / crisis clock / chronicle),
  `GalaxyView` + `GalaxyMap` (drawn node-and-lane map, territory by accent, click to inspect),
  `CourtTheme` (era-reactive palette). Compiles against Godot.NET.Sdk 4.3; **not yet run in an editor**.
- **Galaxy model**: `/content/galaxy` (systems + lanes), `World.Galaxy`, holdings placed in systems in every
  scenario, derived territory, neighbours/hops, `GalaxyProjection`; `map` command.
- **Character creation**: `/content/traits` (vocabularies as data), `/content/backgrounds` (six origins),
  `CharacterCreation` with validation; `create` command and `play --name/--sex/--background/--nature/--aptitude/--track/--ambition`.
- CI compiles the Godot layer.
- **Fleets (first layer)**: `Fleet`/`FleetIntel` state, `FleetSystem` (transit, readiness, upkeep, incursions, intel),
  `MoveFleetCommand` / `RaiseFleetCommand`, `/content/fleets/rules.json`, scenario `fleets`, `fleetStrength`
  condition, NPC houses raise and sail, `GalaxyProjection.Fleets`; two fleet events (*Unfamiliar Sails*, *The Review*).
- **`CourtProjection`**: the Godot-free, tested model of everything Court mode shows; `CourtView` renders it.
- UI: Admiral panel, ship-laying buttons, fleet glyphs and movement orders on the map, serif/sans typography,
  portrait badge slot. NPC action pools gain one Treasury point a turn (fleet orders).
- **War & combat**: `War` state, `DeclareWarCommand` / `SueForPeaceCommand`, `atWar` condition, `declareWar` /
  `makePeace` effects; `FleetSystem.ResolveBattles` (hulls × readiness, defender + fortress/battery `defense`,
  seeded roll, losses, retreat) and `ResolveConquests` (an unopposed fleet in enemy space takes its holdings,
  `seizures`+1 for the player); NPC houses declare war when hostile and stronger, sue for peace when broken
  (the player answers through *The Envoy*). Four events: *The Butcher's Bill*, *The Conquered World*,
  *The Lost World*, *The Envoy*. War/peace buttons in the Court (Admiral) and on the map.

## [0.5.0] — 2026-11-07 — "headless complete"
See `docs/RELEASE_0.5.md` for the full notes.

### Added
- Save/load (`SaveSystem`, versioned envelope; `play --save/--load`).
- `track` and `era` conditions; `revokeClaim` and `succeed` effects; `{role.rankName}` token.
- Career ladders as data (`/content/careers`) with rank caps.
- Crisis `once` and `onResolve`.
- Stress bleed-off and break point (`broken`, `breakdowns`); regency flag; ironman vs continue mode (`--continue`).
- Population → Manpower with per-specialization ceilings; NPC houses invest in their domains.
- Scenario `the_imperial_court` (Imperium start); starting `flags` on scenario characters; married casts.
- 14 events: *The Favor Called*, *The Slighted Kinsman*, *The Frame Unravels*, three Imperial-court events, eight "echoes".
- CI runs the Python pre-flight validator; both validators reject events with no ungated choice.
- `version` command; `World.IsDynastyDead`; relationship index.

### Fixed
- Career events fired for the wrong track; ranks ran past the ladder top.
- Marriages authored from one spouse never produced heirs.
- The run continued after dynasty death.
- Four events had every choice gated and were re-offered forever.
- `press_the_claim` never spent its claim; `the_abdication` demoted the player's own house.
- `first_case/rule_by_favor` cleared `owes_you` instead of arming it; `the_marked_man/finish_it` killed no one.
- `realm_unstable` / `civil_war_active` never cleared.
- Content guide drift; duplicate upload artifacts at the repo root.

## [0.4] — 2026-06 — engine branch (pre-versioning)
Milestones 1, 2, 4 (economy), 5 and the dynasty lifecycle, headless. See `docs/MILESTONE_*_NOTES.md`.
