# Changelog

## [Unreleased]

### Added
- **A province has building slots of its own** (the owner: yes to all three). One to begin with, more by the system's
  development and the rank of the house that holds the province, four at the most; its holder builds the ordinary
  buildings of the world's kind in them, and what they yield is the holder's.
- **A house with a province owes the house over it**: a tithe every year, a levy of manpower in any year the holder of
  the world is at war, and its voice in the Senate, where it leans that house's way.
- **Stepping down from a world costs a rung of title**, as well as legitimacy and the world.
- **The provinces of a world** (docs/PROVINCES.md; the owner: "three regions on each planet and then the planet
  overall ... the other two regions are controlled by different houses ... Governorship is for life unless the leader
  over you tries to [take it] ... accept it and step down ... or you can fight in basically what would be a civil
  war"). Every planet has three provinces: the seat, which is its holder's, and two given over to one thing each
  (helium-3 fields, a march, foundry country, port towns: thirteen kinds, chosen for the world) and held by two other
  houses for life. A province pays its holder and can be improved. The house that holds a world may ask a province's
  holder to step down; the leader over that house may ask it to give up the world to one of the houses under it. The
  one asked steps down, or stands and it is war. Rivals ask too, and the player is given a year to answer.
- A **head of state** is the head of a state: a consul of the Republic, the king, president or dictator of a state that
  has left it, the ruling house of a nation. An archduke and the holder of the capital no longer count as one.
- An **over-full place** troubles its world: a point of unrest a year for every building without a slot.
- **Slots from development, rank and money** (docs/BUILDINGS.md; the owner: "three things ... determine how many slots
  you have ... the big one and main one is development ... another one is your rank ... if you have more money, you can
  ... pay to unlock more slots"). Every held system has a **development** from 0 to 100 that grows a point a year toward
  what its infrastructure, people and prosperity can bear, and falls under siege or revolt. A home world gains a slot at
  development 2, 4, 6, 8 and 10, every other place at 3, 6 and 9; the rank of the house that holds it adds up to three
  more on the home world and two elsewhere; and ground can be **cleared for coin**, each slot dearer than the last. Ten
  slots at the most on a home world, five anywhere else. Infrastructure no longer opens slots itself: it develops the system.
- **Building levels.** A building can be taken to level II and III, where development allows (3 and 6): it yields and
  costs to keep that many times over, for the building's price again and then twice it.
- **The home world** takes its own buildings, **eight that only a home world takes** (Governor's Palace, Forum, Academy,
  Great Temple, Barracks Quarter, Spaceport, Hospital Quarter, Mint, each with its picture), and the ordinary buildings of
  its kind, all in one row of slots. A system that is all one house's has a slot more on it.
- **Great works by standing.** A house raises one great work at a time; the leader of a region two; a head of state (a
  consul, the holder of the capital, the head of a state that has left, an archduke) three; an emperor five. A great
  work can be **hastened**: two years' work in one, for both years' shares and half as much again, every year it is
  hastened. It is no longer bought outright.
- Rival houses upgrade their buildings and clear ground on their seats.
- **A picture for everything that can be built.** `tools/art/works.py` paints a small wide painting for each of the 92
  buildings, great works, tracks of infrastructure and buildings of a system's own world; the system panel shows it beside
  the thing. A test fails if anything buildable has none.
- **Buildings, great works and infrastructure** (docs/BUILDINGS.md; the owner: "separate out the mega structures from
  the ordinary buildings ... every planetary body ... should have a certain amount of building slots"). A **building**
  goes in a slot: every kind of body has its number of slots and buildings of its own (37 in all: smelters on a forge
  world, tidal mills on an ocean, a prospectors' exchange in a belt). A **great work** takes a body's one site, costs
  several times as much, takes four to twelve years, and a house raises one at a time (32: the Orbital Ring, the Great
  Stair, the Aegis, the Dyson Lattice, and one for each feature). **Open space** is a place to build that is tied to no
  world: a waystation, a customs cordon, minefields, the Lane Gate Anchor, the Grand Arsenal. **Infrastructure** is the
  system's own, laid a level at a time: civil works, habitats, orbital traffic, outworks and a defence grid; civil works
  and outworks open more building slots.
- **Solar systems** (docs/SYSTEMS.md; the owner: "there ... should be multiple planets, asteroids and other things per
  solar system ... you should be able to do things for and with every part of your solar system"). Every system is a sun
  and what goes round it: worlds, moons, giants, a belt, a comet, four to ten bodies, with one or two of eighteen features
  that make it unlike its neighbours. The holder of a system builds on every body in it: 38 works, among them colonies,
  mines, skimmers, batteries, a fleet anchorage, a fortress moon, a foundry round the sun, and a work for each feature
  that can be built nowhere else (the Ring Habitat, the Great Dig, the Old Capital Restored). Works pay into the ledger,
  defend the system, supply fleets and make room for people; they go with the world when it changes hands.
- **Settling.** A system nobody holds can be colonised from a world a lane away, or under a fleet standing in it.
- Rival houses build works and send colonies too. Clicking a system on the map opens it body by body.
- **The size of the map** (docs/GALAXY_SIZE.md; the owner: "a scaler for how big the galaxy is ... the empire that you
  are a part of, and the other empires would be bigger as well"). A setting on the new-game screen: small (the map as
  written, 50 systems), medium (about 80), large (about 115) and extra large (about 165). A larger map is grown from the
  written one by the game's seed: every region of the Reach and every nation beyond it gains systems, houses gain
  worlds, new houses are raised to hold the rest, some systems are left to nobody, and the nations are stronger.
- **Borders that tell realms apart.** Every realm has a strong border in its colour with a glow inside it, at every
  zoom level. Your own realm's border is gold; the land of another realm is hatched in its colour.
- **The portrait library is complete at 80 faces** (392 pictures): six men and six women of each of the six
  ancestries of the Reach and four of each for the Steppe, every one at five ages. A house no longer shares two or
  three faces among all its people.
- **Empty sections fold away.** Military at peace is one line, not "Wars" and "The campaign" over nothing; the same
  for an empty docket, an unwritten record and "List a venture". `tools/browser/windows.js` walks every window and
  tab and reports which scroll and any heading left over nothing.
- **The Senate, the Exchange and the Chronicle redesigned.** The **Senate** is a normal-width window that opens on
  the chamber, with moving a motion and the consulship as tabs of their own. The **Exchange** lists a good on one
  row, with what moves its price in a tooltip unless the good is dear, cheap or moving. The **Chronicle** has tabs
  for all that was written, your house, and the Reach, and a year is one list with your house's lines marked.
- **Three more windows**: **Military** is a normal-width window with tabs for your fleets, what is near, and the
  navies, so the map stays in view; **Laws** has tabs for the estates, your laws and the law of the Reach;
  **Worlds** has tabs for worlds and regions, and picks a region from a list.
- **Four windows redesigned** (docs/ART_DIRECTION.md, "The windows, one by one"; the owner: "redesign the windows one
  by one next"). **You** is laid out like a character window in Crusader Kings III: a large portrait, the people
  closest beside it, traits, the best skills as large figures, and tabs for the rest. **Dynasty** opens on the
  tree, with marriages, ambitions, upbringing and the dead behind tabs. **House** splits its long page into the
  estate, the ledger and ventures. **Houses** is one full-width table whose rows open a house's own page.
- **The map shows a different level at each zoom** (the owner: "the ck3 method"). From far off it shows the realms:
  the Reach as one, each nation beyond it as one, with their names. In the middle distance it shows the houses,
  each holder's worlds run together in its colour. Close to it shows the systems, a fine line round each. The
  three cross-fade as you zoom, and the lines and fills are much lighter than before.
- **Nothing on the map is written over anything else.** Names of regions, systems and fleets no longer overlap each
  other, the stars, or the outliner, legend, minimap and windows: a name with no room is left out rather than drawn
  on top of another. The legend is a single line until hovered, and the outliner's text has a solid ground.
- **Territory you can read** (docs/ART_DIRECTION.md, "The shell"; the owner: "do the filled-cell territory on the map
  next"). Every held system on the map is a cell filled in its holder's colour, with a dark line between cells
  of one holder and a wider one, lit along its edge, where two holders meet. A house's worlds, the Reach and each
  nation beyond it read as blocks at any zoom and on the minimap. A system nobody holds is a pale ring, so the
  map thins out where the holding does, and a region's edge is a pale line.
- **Houses have an ancestry, and their portraits follow it** (docs/ART_DIRECTION.md, "Portraits"; the owner:
  "certain houses will have certain genetics"). Every painted face is tagged with one of seven ancestries, and
  every house and every nation beyond the Reach has one, named in `content/ancestries/ancestries.json` or fallen
  to by the dice of its id. The people of a house are painted from it. Someone who marries in keeps the face of the
  house they were born to, and a child of the marriage takes after its father or its mother. A profile says what
  someone is.
- **Your own look at creation.** The workshop's Face tab asks for the ancestry of your house, offers the faces
  painted for it, and shows the one you choose as a child, in youth, grown, in middle age and in old age: the
  faces you will wear across a life.
- **A game's interface, not a dashboard** (docs/ART_DIRECTION.md, "The shell, the windows and the art"; the owner:
  "de-AI the UI because the amount of cards and side paneling is too much. Look at popular grand strategy games").
  The map is now the screen and is always there. A bar runs along the top with your portrait, the house and its
  stocks; a column of icons runs down the left; each opens one window docked beside it, over the map; the outliner
  floats on the map at the right. Inside a window nothing is boxed: sections are split by a ruled header, what
  was a card is an entry under a hairline, the tile grid is one strip of figures with the explanation in the
  tooltip, pills are plain words, and buttons are square plates. Picking a world opens the map's own window.
  Escape returns to the map. The research behind it (Crusader Kings III, Stellaris, Victoria 3, Old World and
  others) and a table of what read as machine-made and what replaced it are in the document.
- **Painted portraits** (the owner, of three attempts at drawing faces in code: "It's bad. … do some research into
  tools"). `tools/art/portraits.py` paints the game's portraits with an openly licensed image model that runs
  with no account and no key, and the client ships them. A person is one face painted at five ages, so it grows
  old with its owner. The faces drawn in code remain only as the fallback where no picture exists. The pictures are
  AI-generated, which a store page must say; the document sets out what a hosted model or an artist would cost.
- **A face at every age, and families that look like families.** The library is complete: 32 faces (24 of the
  Reach, 8 from beyond it) each painted as a child, a youth, an adult, in middle age and in old age, 144
  pictures in all. A child takes a parent's colouring. The workshop's Face tab lets you choose your own.
- **Event windows open on a painting** (`tools/art/scenes.py`, sixteen occasions chosen by what the event is about),
  with the title over its foot, the people involved at bust size, and the choices as full-width plates. The title
  screen has key art behind it and its scenarios as a list.
- **A profile for everyone**: any portrait opens that person's window: who they are, their station and offices,
  what they think of you and why, what they are like (with each trait's meaning written out), what they are
  good at, their kin as portraits that open in turn, and what they have done. The You screen opens on your
  portrait, and the family tree shows faces instead of initials.
- **Negotiated or won** (docs/BEYOND.md; the owner: "Peace can be negotiated or won. Depends on war score."). Every
  war with a nation has a war score from -100 to +100. At the ends the peace is won or lost. Between, it is
  negotiated, and the terms step up with the score: a white peace near level, keeping what you hold from +25,
  tribute for ten years from +60, and the same the other way when the war goes badly. A warlike ruler asks more for
  the same terms, and one of the great more still. The Senate's peace takes the best terms on offer. In a war of
  your own you make your own peace from the nation's card, with no vote, and tribute is paid to your house. The
  card shows the score, the terms now and what the next step wants.
- **Kinds of peace** (docs/BEYOND.md, "Kinds of peace, how often they come, and whose fleet it is"; the owner's
  answers to decisions 16 to 19). A war with a nation ends one of six ways, by how it ended and what each side
  holds. Beaten decisively, a nation can be made a **client**: it keeps its ruler and its worlds and pays the chair
  and its patron every year, until a warlike ruler throws the treaty off. Otherwise it pays tribute and the houses
  keep what they took. A war the Reach loses cedes what the nation holds, or, if it holds nothing, the Reach pays
  tribute for ten years. A peace the Senate votes goes by the tally, and with the tally close it is a **white
  peace**: everything goes home. A nation's card says what a peace would be this year.
- **How often they attack** is a second new-game setting, and each game's own luck: a Great Khan is likelier in one
  game than the next. The Free Cities can raise a Navarch and the Outer Compact a High Warden, at long odds, and
  then they attack too.
- **What defying the Senate costs depends on whose fleet it is.** A commander nobody has heard of costs his house 8
  standing and 3 a year, his crews waver, and the benches lean hard to recall him. A great name costs 2 and nothing,
  his crews hold, and the benches lean against a recall. Skill counts a step. The order shows the figures first.
- Six new reports and two new orders on a nation's card.
- **Whose war it is** (docs/BEYOND.md, "Whose war"; the owner's answers on the map and on war). The Senate votes a
  war and directs nothing, so three things are new. A house may make war on a nation on its own account, with no
  vote: only that house is at war. The Senate may vote peace beyond the Reach: those in the field are told and
  answer. And a general may not listen: the war is his house's own then, it costs the house 6 standing at once and 2
  a year, and the Senate may recall him whatever his name is worth.
- **Houses raid.** In a war the nation did not begin, another house sacks a world of the nation's in a season and
  goes home: paid as for a world taken, the world still theirs, not worth sacking again for six years. It takes
  worlds only when the nation attacked first. Your own fleets take and hold until you order them to raid.
- **A nation can attack, under a great ruler.** The Khanates are raiders until a Great Khan is raised: one accession
  in eight, or a warlike Khan acclaimed after beating the Reach. Then they muster half as many hulls again, begin
  their own wars and stay in the field, taking world after world. The Hollow Crown can have a Basileus, who stops
  its decay.
- **No two games have the same neighbours.** Each nation's strength is fixed when a game begins, a fifth either way.
  A new-game setting, "The powers beyond", makes them gentle, as written or harsh.
- **A conquered world is marked on the map**: a broken ring in the holder's colour, with a line on the world's card.
- One new motion (Peace Beyond the Reach), three orders on a nation's card, eight reports.

- In a war the Senate voted, houses other than yours no longer take a nation's worlds: they sack them (a change to
  what was built on 2026-11-18).
- **The Outer Compact and the Hollow Crown are on the map**, so all five nations are (docs/BEYOND.md; docs/GAPS.md
  R10). The Compact lies south of Sorrow, past the Long Dark, an empty system no house can hold: Pike's Landing, the
  Wrenn Seams, the Mallow Fields and the Wardenry. It never leaves home. Invaded, its guard and militia turn out
  together and fight at five-quarters of a fleet of the Reach's; its worlds take two years longer to reduce and rise
  in four. Each world of it gives the house that holds it two isotopes a year. The Hollow Crown lies south-west of
  the Ashen Verge: the Bronze Gate, Melitene, Chrysopolis and the Purple Palace at Porphyra. It decays: a hull struck
  from its lists every ten years, down to four. Its fleets fight at seven-tenths. A world it loses stays lost, so it
  can be taken apart and ended. Whoever first takes Porphyra has 120 credits, eight points of standing and the
  diadem of the Autokrators, an heirloom the head of the house keeps. A house short of money may move a war on the
  Crown for plunder. Six new reports, and each nation's card on the Military screen says what it is.
- **The Ardent Concord is on the map** (docs/BEYOND.md; docs/GAPS.md R10). East of the Valmont Anvils and Ironhold
  lies the Ardent Gate, the only way in, and behind it the Brandt Yards, the Marrek Foundries and Concordia, where
  the First Admiral sits. It is the strong neighbour: 26 hulls, a home fleet and two divisions. It begins wars
  itself: when the Reach is divided by the great war, a civil war, a state in arms against it or a standard raised
  against a throne; or to take back a world of its own that a house of the Reach holds. Its two divisions come
  through its two marches and fight a campaign: each takes a world, slowly, and then the next. A war it began is
  every house's to answer. What it takes it keeps until it is beaten, and its own worlds do not rise: it comes back
  for them. Three new reports, and its card on the Military screen says what it is.
- **The Free Cities of Thessar are on the map** (docs/BEYOND.md; docs/GAPS.md R10). South-east of Wintermoor and
  Brackwater lies the Thessaran Shore: Port Meront, Pellas, and Thessar, where the Speaker of the Cities sits. They
  are a different neighbour from the Khanates: they would rather buy than fight. Their squadron stays at home
  unless a warlike Speaker rules. Attacked, they hire a hull a year. They sue for peace at half the usual tally.
  When they lose, they buy their cities back from the houses that took them, 36 credits a city, and if the house is
  yours you are asked: the money, or the city. They pay tribute only while a fleet of the Reach stands at their
  march or over their cities; three years unwatched and they stop. A war nobody fights for four years they buy
  off. Beating their fleet pays 9 credits and taking a city 18. Hull for hull they fight at four-fifths of a fleet
  of the Reach's. Eight new reports, and their card on the Military screen says what kind of neighbour they are.
- **The Vashti Khanates are on the map** (docs/BEYOND.md; docs/GAPS.md R10). West of The Anvil and The Veil lies the
  Vashti Steppe: The Long Grass, Tul Aimag and Khal Orda, where the Khan of Khans sits. The Khanates hold these
  worlds, and their strength is two fleets: a guard that stays with the Khan and a host under the Noyan. When the
  Senate votes war, every house is at war with them and it is fought on the map. Their host comes through the march
  and takes a world, slowly, then goes home with it. A world they hold is theirs until a house of the Reach frees it,
  and a freed world goes home to the house it was taken from. Against their fleets the houses of the Reach fight as
  one. At the peace the winner keeps what it took: a lost war cedes what they hold. Their own worlds are easy to take
  and rise again in three years without a fleet over them. Taking the Great Camp ends a war. The old expedition
  against them is gone: you sail there by the lanes. The Galaxy and Military screens show all of it, and ten new
  reports tell you what happens. The other four nations are as they were.
- **A door and a face for the nations beyond the Reach** (docs/BEYOND.md, stage 0; docs/GAPS.md R10). Each nation
  has a march: one or two systems of the rim that face it. Its raids come through the march and fall on worlds
  within two lanes of it, and a fleet of three hulls standing there turns them back before they land. An
  expedition's voyage is the lanes from your fleet to the march plus the nation's depth beyond it, so a fleet
  posted at the march sails soonest. Each nation has a ruler and a commander of its fleets, with titles and names
  of its own: the commander's skill counts in a battle, and one who is routed may be killed; the ruler's nature
  moves the nation's regard for the Reach, and rulers die and are followed. The Military screen shows all of it.
- **A throne that can be lost, stage 1** (docs/THRONE.md; docs/GAPS.md R5). Whoever sits a throne has a hold on it,
  out of 100, made of things you can see: standing, hulls, the capital, years, how it was taken. Two years of a
  failing hold and a house of rank raises a standard: a war for the throne that no treaty ends. The other houses
  go over, stand by the throne or stand aside, and you are asked. The pretender takes the throne on the terms of
  a seizure, or is broken. The house cast down drops a rung, keeps a claim, and plays on. This applies to you if
  you are the one on it. You may raise your own standard. The Senate screen shows the hold and the rising.
- **Emergency powers** (docs/EMERGENCY.md; docs/GAPS.md R8). When the Reach is in danger (the great war, a civil
  war, a state broken away and unrecognised, a war beyond the borders) the Senate may vote a sitting consul its
  emergency powers for three years: the other consul's veto is set aside, the holder may strike a motion every
  year, the Senate leans toward what the holder moves, the houses can be levied without a vote, and no election is
  held. They are to be laid down when the term is up or the danger past. You may ask for more time, or keep them at
  a cost each year until the Senate takes them, and after a first term you may put it to the Senate that you hold
  them for life: carried, the Republic ends by its own vote; refused, you are stripped of them. Other houses move
  it for their own consuls, who use them and always lay them down. The Senate screen shows all of it.
- **Blood between houses** (docs/FEUD.md; docs/GAPS.md R4). A killing binds the dead person's house to answer it.
  The debt is the house's and outlives the people. It begins with a knife that is traced, the death in battle of a
  head or a head's near blood, or a killing by hand in an event; while it stands the two houses vote against each
  other and will not marry; and it ends by blood, by a duel, by a price, or by being let lie at a cost in
  standing. Other houses carry and end theirs by their head's nature, and ask you before they act against you.
  A panel on the You screen shows what is owed to you and held against you, with your odds on the field.
- **Duels.** The head of a house, or a kinsman who would go, may call out the one held to blame for a death.
  Whoever falls, it ends there.
- **The Institute remembered** (docs/ACADEMY.md, "What is remembered"; docs/GAPS.md R14a). How you did in your year
  is written on you for life: won as the year's captain, first of the year, in the ranks that won, honours, or
  near the bottom. Classmates carry theirs too. It counts toward renown and elections, the captain is commissioned
  a rung up and leads more hulls with steadier crews, the year thinks better or worse of you for what you did to
  it, and six later events recall it. The life panel shows it.

### Changed
- **The soak says why a line ended**: the last deaths in the player's house, for each game that ended.
- **A saved game resumes the same when a house is made after the save.** The houses declared their votes in the
  order they happened to be held in memory, and a house leans with its bench's leader when the leader's vote is
  already cast. A house made after a save could vote before its leader in one game and after it in the same game
  resumed. The leaders of the benches now declare first and the rest by name. Found by the soak when the Concord's
  wars made more worlds change hands; `praetoria drift` is the tool that found the year and the vote.
- **A ransom answer is read by the nation that offered it.** With more than one nation on the map, whichever was
  reckoned first cleared your answer before the Free Cities saw it. Found when the third nation went on.
- **Against a nation on the map, the other houses gather before they attack.** A defended world beyond the march
  is no longer attacked one squadron at a time: the fleets go to the nearest march, wait, and go in together when
  those standing there are a quarter more than what holds the world. Against the Khanates the Reach now wins 45 in
  a hundred of its battles, where it won 40.
- **Beating a nation's fleet on the map pays plunder**, and taking one of its worlds pays twice as much: 4 and 8
  credits from the Khanates, 9 and 18 from the Free Cities.
- **The Galaxy screen's "fit the whole map" allows for a narrow panel**, so the nations beyond the rim are in the
  picture whatever the shape of the window.
- **Raids from beyond the rim fall near the raider's march**, not on any world of the rim by lot. A house that has
  been raided posts a fleet there.
- **Seizing a throne asks you to outmatch the throne's own fleet**, by a quarter while its hold is good and at all
  once it is shaken. It used to ask you to outnumber every house in the Reach. A claim will do in place of ducal
  rank.
- **Other houses use the knife on each other.** A cruel, ruthless or vengeful head who cannot abide another
  house's head now and then sends a hired hand at its ablest adult: never its head, never its last adult, never
  your house, and no more than once in twenty years. Until now no house but yours ever killed outside a war.
- A successful assassination is no longer always unexplained: one time in three the trail is found, and a house
  that already has cause needs no proof.
- **The Institute is a contest.** It was won in 30 years of 30 and was the same year in every game. Each year now
  has its own dice, each rival Standard a captain of its own, and classmates score more. A bold cadet wins it as
  captain about one year in three; a careless one never leads.

### Added (docs)
- **The powers beyond the Reach, on the map: the plan** (docs/BEYOND.md; docs/GAPS.md R10). Five stages, from a
  door on the rim and named commanders to provinces and foreign courts, with four questions for the owner.
- **A throne that can be lost: the plan** (docs/THRONE.md; docs/GAPS.md R5). Four stages, what each rests on, and
  the five things assumed.
- **Playing a known story** (docs/ROLEPLAY.md). Darrow, Reinhard von Lohengramm, Yang Wen-li, Nero, Palpatine and
  Paul Atreides, each cut into beats and each beat checked against the engine: what can be done today, what can
  partly be done, and what has no road. Nero au Augustus added the same day, with the owner's answers. Eighteen
  gaps mapped from it (docs/GAPS.md, R1 to R18).

### Added (tools)
- `praetoria throne` plays each start that opens under a throne twice, as written and with the player's house
  crowned in the first year, and says how the hold fared and how pretenders did.
- **Players with an end in view** (docs/QA.md; docs/GAPS.md Q2). `praetoria roads` plays a general, a seceder and a
  courtier, each taking the steps of its road in order, and reports how far each got and what refused it, in the
  game's own words. The soak now plays each once per start and counts their coverage with its random player's, and a
  short run is a step in CI.

### Fixed
- A turn is about a quarter quicker on the map as written: the head of a house is found in one pass, the year's
  quarrels are worked out once for every bench, and a rival steward asks the cheap question first.
- The browser scripts in `tools/browser` found the End Turn button by its text, which the game shell now shows in
  capitals, so they ended no turns. They find it by its class.
- **A throne no longer stands empty.** A house on the throne that lost its last world became landless and nobody
  took its place. Whoever takes that world has now taken the throne.
- **The epilogue is the player's own.** It was written whenever any house took a throne, as if you had.
- **The throne is not a rung of the title ladder** (docs/ROLEPLAY.md). Seizing, petitioning for or claiming a title
  could carry a house from archduke to emperor in a Republic that went on as before: no proclamation, nobody
  deposed. The ladder now stops below the throne, which is taken by proclamation or by a fleet over the capital.
- **Found by the full QA** (docs/QA.md). The Dynasty screen no longer scrolls sideways on a phone. A raid on your
  world is a year's news and no longer remembered for ever. The build has no warnings. The README and the roadmap
  give the right counts again.

### Added
- **A favour called in or forgotten, and a gift refused** (docs/WEALTH.md; docs/GAPS.md W13). A house that owes
  you a favour can be asked for 15 credits or for its consent to a match, from the purses panel; left alone the
  favour is still spent on your next motion. A favour nobody calls in is forgotten after eight years. Heads of
  other houses now refuse a gift from someone they think ill enough of, and will take nothing from you if they
  think very ill of you or are at war with you.
- **A pledge, and foreclosure beyond a venture** (docs/ECONOMY_MARKET.md, section 15; docs/GAPS.md A9). You can
  name what your creditors take first if the house is foreclosed on: a venture, or a thing you hold that is not
  entailed. With no venture left they take a thing, though never an entailed one, and at the last a world against a
  loan from a named house. A blockade that fails is billed to whoever laid it and paid at the peace.
- **Plunder, and a gift that asks** (docs/WEALTH.md; docs/GAPS.md W11). When a world is stormed, its governor's
  signet, sword, library or standard may be carried off by the commander. Anything taken is resented, can be
  retaken in battle or bought home, and may be asked for, as a standard already could. A new kind of thing, an
  orrery, makes its holder study faster; a head with a motion before the Senate sometimes sends you one, and if
  you keep it you are judged by how you vote. Giving a thing to another head puts their house in your debt for a
  favour, spent on your next motion. The purses panel lists favours owed. Two events.
- **Bills at the peace, and foreclosure** (docs/ECONOMY_MARKET.md, section 14; docs/GAPS.md A8). A venture sacked in
  a war is billed to whoever took the world and paid when peace is made, unless the claimant was beaten. A company
  that cannot carry a failing venture sells it rather than close it, if its board minds the books. A house that
  owes and cannot pay, or has been overdrawn two years running, is foreclosed on: its least venture goes to its
  lender or is sold. You are warned a year before. A venture that is a person's own is never taken. One event.
- **A venture of one's own** (docs/WEALTH.md; docs/GAPS.md W10). You can settle a venture on one of your house, or
  on yourself, from its card on the Exchange. What it earns is paid over to them, it is not sold or listed unless
  they agree, its price is theirs, and it goes by the will. A relative who founds a venture with their own money
  owns it. A founder who does not own what they founded gets a fifth of its price when you sell it, and minds.
  Shares held in a person's own name now vote with their house, while that person thinks no ill of its head.
- **A standard asked back, and gifts between houses** (docs/WEALTH.md; docs/GAPS.md W9). The house a standard was
  taken from thinks worse of whoever flies it, retakes it if it beats them in battle, and at peace buys it home or
  asks you for it. You can send one home, or buy your own back from your purse. A relative with the money has a
  shrine or a library made in their own name. A head who would rather not be at odds with you sometimes sends a
  gift, which you can accept, answer or send back. Three events.
- **A failing venture, beyond its owner** (docs/ECONOMY_MARKET.md, section 13; docs/GAPS.md A7). Other houses now
  carry a failing venture or sell it before it is wound up. A company carries or closes its own. A failure pays
  those the house owes first, costs it with the merchants and the commons, and leaves it a name for bad business
  for five years: buyers bid a tenth less for what it sells. A venture on a world that is stormed may be sacked. And
  you can stake a founder whose venture failed to a second start, from the ambitions panel. An event for a sack.
- **Shares in your own name** (docs/WEALTH.md, "A purse that holds more than credits"; docs/GAPS.md W1). You can buy
  shares out of your own purse, up to a tenth of a company. What they pay goes into your purse, not the treasury;
  they are not the house's, give it no say in the company, and go by your will. Relatives with a head for money buy
  shares for themselves, and one in debt sells them; the purses list says what each holds. Whoever founded a
  venture now draws a stake from it each year. An event for a relative's first purchase.
- **Things a house comes by** (docs/WEALTH.md, "New things"; docs/GAPS.md W8). Three new kinds of thing to hand
  down. A shrine or a library can be made to order out of your own purse. A standard is sometimes taken in a battle
  by whoever commanded, and is theirs: flown from their flagship, the fleet grows more loyal. Another house
  sometimes sends plate at a wedding, to the one who married: the two think better of each other while it is kept,
  and selling it is taken badly. Other houses come by all three as yours does. You can also give a thing to the head
  of another house. Two events.
- **A venture can fail** (docs/ECONOMY_MARKET.md, section 11; docs/GAPS.md A6). A venture that earns nothing or loses
  money three years running is wound up: half its salvage comes back. You are warned a year before, on the venture's
  card and in the alerts, and can put money in to give it three more years, or sell it. If one of your house founded
  it, that failure is written into their life. An event for the doors being shut.
- **Trust told apart by office and by region** (docs/LADDER.md; docs/GAPS.md L10). What a magistrate does is set
  against their own office's name and against the belief of the region where it was felt, as well as against the
  Reach's trust. A scandal costs a house double at the count for an office whose own name is low. A region that does
  not believe the magistrates holds a little less closely to the Reach each year. The ladder panel says which offices
  and regions stand apart; the Worlds screen shows a region's belief.
- **Patrons** (docs/LADDER.md; docs/GAPS.md J5). A senior who thinks well of a junior and has had three good years'
  work of them takes them up: they are that junior's patron wherever either is posted, and when the patron rises the
  junior is taken along. A junior the one above has come to think ill of falls out with them, loses a patron, and is
  not under that senior again. The juniors of every house have it, not only yours. Three events for the head of a
  house whose own people are taken up, taken along, or fall out.
- **A war over what was said** (docs/BREAKAWAY.md; docs/GAPS.md B15). A house that has been stirred against another,
  already thought very ill of it and is no weaker in ships now goes to war with it; a censure used to be as far as it
  went. If the money that paid for the talk was yours, an event says so.
- **Free marches and free holds are states** (docs/BREAKAWAY.md; docs/GAPS.md B5). A house that declares a free
  march takes its region out of the Reach as a state, in a form of its own choosing, and the Reach lets it go or
  sends the fleets. A rebel world that breaks from its holder is a free state from its first day: outside the Reach,
  with no seat in the Senate; its old holder may buy it back or take it; after three years the Reach names it in a
  treaty. The Senate does not go back for a single world.
- **The chair** (docs/LADDER.md; docs/GAPS.md O2). The Speaker can hold one motion a year over to the next. The
  presiding consul can speak from the chair for or against one motion a year, and the houses lean with it; a tied
  Senate now goes as the presiding consul voted. Officers of other houses do the same by their interest. The Senate
  screen shows on each motion whether it was held over and what the chair said, and which way a tie would go.
- **The Treasurer's budget has figures**: what the levy brought in, what the lawless lanes lost, and what the Reach's
  hulls, lanes and loose regions call for. The three choices are sums read against that year's surplus or shortfall.
- An event for a motion of yours that a Speaker does not reach.
- **Houses and Worlds open with a sentence and six tiles**, like every other dashboard: where you stand and who is
  with and against you; what is yours, what is restless and what is there to be had. Their tables are as they were.
- **The Chronicle, rebuilt** (docs/DASHBOARDS.md). One sentence on what the last year wrote of your house. Six tiles.
  The years have their numbers under them and a key. It opens on the last five years. Each year is two columns: what
  was written of your house, and what of the Reach, each line marked by its kind.
- **The Dynasty screen, rebuilt**. One sentence on the heir and how deep the line is. Six tiles: the heir, the line,
  the blood, who is unwed and of age, the health of the house, the generation. The family tree is drawn to the width
  of the family, with first names and larger text; one who married in stands beside their partner. The line of
  succession is first in the side column.
- **The Exchange, rebuilt**. One sentence: an embargo, a good you burn and have none of and what it would cost, or
  what has moved most and why. Six tiles. The goods have the whole width; "what moves a price" and "how building
  works" are folded away. With nothing being built, Projects says what would be there and how to begin.
- **The Laws screen, rebuilt** (docs/DASHBOARDS.md). One sentence on the estates: who is hostile and what it costs,
  who the laws are driving away, or who is nearest to loyalty and the one change of law that would bring them nearer.
  Six tiles with what each figure is read against. One estate to a row: a bar from −100 to +100 with a mark where the
  laws are taking it, and one line on what it gives, costs or wants. The law of the Reach is a table.
- **The You screen, rebuilt**. One sentence on what you are and what state you are in. Six tiles: rank on its ladder,
  health against what your age allows, renown and the next name it earns, strain, your best skill against its
  ceiling, where you command from. One display of skills in place of two. Two columns that scroll, in place of a grid
  that cut its panels off.
- **The Senate screen, rebuilt** (docs/DASHBOARDS.md). It opens with one sentence: a motion against you and how it
  will go, or what is before the chamber, or that nothing is, with your seats, your bench's and how far that is from a
  majority. Six tiles: your seats, ranked; your bench against a majority; the order paper and how it leans; the
  consulship and who leads; the fear of you against the mark where the houses combine; trust in the magistrates. Five
  tabs in place of two, so that each holds one or two panels that can be read whole. The seats are drawn in their
  bench's colour, so the legend describes the picture.
- **An alert opens the tab it is about**: the duties, the offices, the household, the purses.
- **The House screen, rebuilt** (docs/DASHBOARDS.md). It opens with one sentence on how the house stands: whether it
  pays its way, on what the money goes, how long the treasury would bear a shortfall, or what it clears a year and
  where it ranks. Six tiles carry a figure and what to read it against: the treasury and what a year does to it; what
  comes in and how much of it is spent; wealth, ranked; debt; the worlds, by the least loyal; legitimacy against what
  the house's rank wants. The estate (worlds, ledger, stores, ventures, the wealth of the houses) is the first tab; the
  household and the purses are tabs of their own. Each person of the household says how they regard you and what they
  want.
- **The Command Deck, rebuilt** (docs/DASHBOARDS.md). It opens with one sentence that says how things stand: a
  battle at the year's end, dry tanks, a war, a hostile fleet near a world of yours, or peace and where you rank. Six
  tiles each carry a figure and what to read it against: your hulls and your place among the navies; whether the fleets
  will fight; helium-3 in years of burn; upkeep against what the house takes in; the nearest hostile fleet and how
  far; your wars. The plot is drawn on what lies within three jumps of your worlds, with every system and fleet named.
  A table lists every fleet near you and what its house is to you. Fleets carry a status word. The navies are ranked.
  The general and the Senate, and the nations beyond the Reach, are on tabs of their own.
- **An audit of every dashboard** (docs/DASHBOARDS.md, `tools/browser/audit.js`): each screen at laptop size,
  measured and judged; what Victoria 3, Europa Universalis IV, Democracy 4, Football Manager and Total War do about
  the same problems; ten rules taken from them; and the order the screens will be rebuilt in.
- **The Senate has committees.** The Purse, the Fleet and the Laws. Every house sits on one. A senator can spend the
  year on the committee's work, and each such year raises their standing with it: at three the committee's other
  houses lean toward what your house moves on the committee's own business, and the highest standing takes the chair.
  A year missed costs a point. You can move to another committee, and start at the bottom of its table. One event.
- **An attendance record.** The years you sat and the years you missed are kept, and count for or against you at any
  election once there are five of them.
- **A case has facts.** What it is about, how many credits turn on it, and whether the law is plain, arguable or
  doubtful. The plainer the law, the more a ruling against it costs; where it is doubtful a ruling either way can be
  defended, and is neither appealed nor kept on the record. A praetor can also **bring the two houses to terms**.
- **The accounts have figures.** A quaestor sees what is over this year and where it came from; that sum is what can
  be sent to a lawless region or taken, and a large sum taken is likelier noticed.
- **An aedile can give lanes and grain both**, for twice the sum.
- **The courts.** What a magistrate took in office is kept on a record for eight years. They cannot be touched while
  they sit; afterwards the head of any house can bring a charge before a sitting praetor, and the verdict is that
  praetor's: the upright convict, a friend of the accused's house lets them go, one known to be for sale finds for the
  richer house, and before the rest it goes by who pleads better. Other houses bring charges too, against your people
  as against anyone's. A new panel on the Senate screen shows the record, who would hear each charge, and what stands
  against your own house.
- **A praetor can be bought**, for fifteen credits of your own. The upright refuse and say so from the bench. One who
  takes it finds for your house whenever it is before them, until it comes out, and then you fall with them. Three
  events.
- **Ask the elder.** The head can ask the elder for one thing this year: to make peace in the house, to take some of
  the work, or to speak for them. An elder who thinks ill of the head keeps their own counsel.
- **A price that is not money.** A relative who would want a sum to study something will take a word said for them in
  the right place instead: one or two influence, and an empty purse is no bar.
- **Going unpaid has an end.** One of the household unpaid two years running, who does not think well of the head,
  goes home or lays down the work. A grown person of standing costs two a year to keep.
- **Another house's divorce is somebody's choice**: after three years apart, with no child or with the houses fallen
  out, the head of the house that took the dowry ends it. An upright head does not.
- **A marriage between two houses is strained when the houses fall out**, and more when they are at war. (Other
  houses' marriages never went cold before, so none ever ended: found by measuring.)
- **A person's service pays them their rank and one more a year** (it had been half that), so that relatives have
  something of their own to lend, give or lose. The head can ask to borrow five as well as ten.
- **An entail.** The head can bind a thing of the house's own name to the house: it goes with whoever has the house,
  whatever a will says, and cannot be sold, given or left away. Breaking it is done in court and is remembered. An old
  name's own heirloom is entailed from the start.
- **Heirlooms do something.** A signet on the head's hand mends the house's name; a sword carried in the fleet teaches
  discipline; a library speeds the study of everyone in its holder's house; an estate pays. The panel says which.
- **Loans the other way.** The head can borrow of a relative (who may refuse, or want a fifth on top) and can lend at
  interest. One of the house in debt is helped by another who has it to spare and thinks well of them. A bride puts
  half her own purse toward her portion.
- **A household costs something.** A credit a year for each person taken in, and two a year into the purse of whoever
  does the head's work. A house that cannot pay keeps them, and they think the worse of it.
- **A price for studying, and thanks for teaching.** One who would not study for the asking may name a sum; the head
  can thank one of the house who is bringing another on.
- **The elder is not idle.** Each year the elder makes peace between two of the house, or takes work off the head, or
  speaks for the new head to someone who still thinks well of the old one. The elder's old friends stay the house's.
- **Old ties count at the count, in a court and in a match.** Old friends are worth votes to anyone of the house who
  stands; a magistrate who is an old friend finds for the house; a family whose head is one is readier for a match.
  Old enemies the other way.
- **Other houses divorce**, now and then, when a marriage has gone on apart.
- **A junior has somebody above them.** The judge an advocate appears before, a clerk's senior, the captain of the
  ship a junior officer serves in: by name, and the same one year after year. Good work is noticed; after three good
  years they are spoken for and go up a step. An advocate before a judge of their own house who is not upright wins
  what they would have lost, and it is seen.
- **The merchants and the magistrates' own order** now mind it when the magistrates are not believed, as the commons do.
- **Stirring against a house can end in a censure**: a house that has come to believe what it is told moves one.
- **The house divides behind a quarrel**: the others take a side, and say so.
- Seven events.

**Changed in the same pass:**
- **Nobody sits above their ceiling.** What a person has already reached is taken as what was in them.
- The gap register now marks the rows that are the owner's to decide, with the question each turns on; and the process
  notes say to work down the register without asking (docs/PROCESS.md).

- **More ways to bring a house over.** Promise it a place (its regard rises at once; if the state is founded with it
  in it, you owe it three credits a year for ten years), or threaten it if you have three hulls to its one (it follows
  for as long as it is afraid). A marriage between the two houses makes it easier asked.
- **A state can be joined while it is still fighting**, and a house that does is at war with the Reach for it. **A
  member house can leave a state**, and does by itself when it has come to think ill of its ruler. **A state's ruler
  can court houses of the Reach** in or beside the state, and one that controls a bordering region may bring it over.
- **Stirring the camps can be aimed at one house.**
- **Under a throne, whether the Reach fights for a region is the Emperor's own answer**: a new throne will not be seen
  to lose a province; a just Emperor, or one who thinks well of your house, may let it go. Two events.
- **What a child is brought up to shapes what they want.** Two in three come of age wanting what they were raised
  toward (a command, the Senate, a fortune, or the house restored if it is poor). One set against their own bent wants
  something that is nobody's plan. The rest are left to the dice.
- **The loser of a quarrel holds it against the winner** when the other one gets the office or the command.
- **The lowest ranks have work.** An Aspirant or Advocate pleads a brief (on the law, for the side that pays, or for
  one who cannot pay). A Clerk or Administrator keeps the ledgers (exactly, usefully, or with an account padded). A
  Cadet, Ensign or Lieutenant with no command has the watches (stand them, volunteer for the patrol, or make friends
  in the mess). With this every rank of every service, every elected office, every office of the Reach and the Senate
  seat has a year's work. One event.
- **Join a state that exists.** A house of the Reach can go over to a recognised state as it stands, or take a
  bordering region into it. The Reach lets the region go, or calls the treaty broken and comes for the whole state.
  Houses that think well of a state's ruler and ill of the capital go over by themselves.
- **A second great war.** Fifteen years after a great war ends it is forgotten, and what is wrong with the Reach can
  bring another: the Second War of the Reach, then the Third. Every finished war is kept on the record.
- **Stir the camps.** A house can push the Reach toward its great war on purpose, for influence and credits, at the
  risk of the capital hearing whose money it was. Three events.
- **The work of office.** A quaestor, an aedile and a praetor each have business every year, and what they do with it
  counts. The quaestor closes the Reach's accounts (straight, or with money found for a lawless region, or to their
  own house's good). The aedile sees to the works from their own purse (the lanes, grain, or nothing). The praetor
  hears a case between two houses at odds (on the law, for a friend, or for a gift). You decide your own; relatives
  and every other house's magistrates decide theirs by nature. Money taken in office sometimes comes out: the seat is
  lost and the person may not stand for five years. What the magistrates do moves **public trust** in them, and below
  35 "the magistrates are known to be for sale" is one of the things wrong with the Reach. The ladder panel shows your
  business, the trust, and what every magistrate did this year. Five events. docs/LADDER.md.
- **A senator's year.** The head of a house has one thing a year to do with its seat besides the chamber's motions:
  read the accounts (and perhaps follow stolen money to the magistrate who took it), speak for the region, or work
  the bench. Three years of doing nothing with it is remarked on. One upright senator of another house audits each
  year too, so money taken in office may come out a year later.
- **A Judge hears a matter a year** between an estate and a house, and **a Justice hears the appeal** against a
  praetor's ruling that went against the law, and may overturn it. If your house has no Justice, one of another house
  hears it, so a bent ruling against you can be mended. "Your duties this year" on the Senate's standing view. Three
  events.
- **The consuls, the Treasurer and the Speaker each carry a charge a year.** A consul calls the magistrates to account,
  sends the Reach's fleet onto the lanes, or spends the year on the bench. The Treasurer balances the books, forgives a
  region its levy, or lets the contracts to their own house (and may lose the office for it). The Speaker keeps the
  order fair, brings their own business forward, or gives an enemy no day. Other houses' officers decide by nature,
  and when one forgives your region its levy or keeps your house off the floor you hear of it. Two events.
- **A desk and a posting.** A Governor, Minister or High Treasurer of your house has the year's returns across their
  desk (assess fairly, press the collection, or keep a share and risk being broken a grade). A Commander or above with
  no fleet has a year ashore (drill the crews, patrol the home lanes, or sit on a board of inquiry). One event.
- **Two relatives who want one prize quarrel over it**: the same office, the same fleet, or the house itself. You may
  stand behind one, tell them both it stops, or keep out of it.
- **A relative's failure is marked.** An election lost or a battle lost in command is written into their life, costs
  their renown and counts against what they want. Three, and they give the hope up. Two events.
- **Give or lend a relative a sum** from your own purse. A loan is paid back as they can; six years unpaid it sits
  between you, and you can forgive it.
- **Whose money a dowry is**: the treasury's, or your own purse first.
- **Ask a relative to study something**, which they may refuse, and **ask one of the house to teach another**.
- **Relatives find mentors for themselves**, and a relative's mentor teaches them. Three events.
- **Send your physicians to a relative** who is ill or ailing, from your own purse (the life panel).
- **Old friends and old enemies count.** A friend of eight years leans your way in the Senate and stands by your house
  when something is aimed at it, and is likelier to take your side in a civil war. An old enemy does the opposite.
- **A relative who stood for office without a word from you, and lost, blames you for it.** One you spoke for who won
  knows what they owe.
- **In a civil war the parties go to war as parties:** a bench follows its leader unless a house has a strong reason
  of its own. The forecast on the Caesar panel uses the same rule.
- **The Senate's sword commands the Reach's own fleets** for as long as the civil war lasts.

### Changed
- Other houses' juniors are now spoken for by their seniors as yours are, so their people rise in rank a little
  sooner than they did. Measured effects are in docs/GAP_ANALYSIS.md, section 56.
- A house that had declared a free march, and a rebel world's new house, no longer sit in the Senate or stand for
  office: they have left the Reach.
- A Speaker who gives an enemy no day now holds over whatever that house has moved. Balancing the books banks half the
  year's surplus, not a fixed 5 credits; forgiving a levy and the contracts are sums from the year's figures.
- **A relative who means to restore the house now can.** They keep 3 credits and give the rest while the house is
  poor, and a venture they found counts toward it, by its cost and by what it brings in each year. It used to take a
  lifetime on an allowance.
- **Public trust in the magistrates now means something, and moves as it should.** A house one of whose people was
  removed from office for taking money loses votes at every count for ten years, for the ladder and the consulship,
  and twice as many when the magistrates are little trusted. When trust collapses the commons turn on every house in
  office and the house that holds the capital loses standing; when it is high, office is honoured. Measured over
  sixty-year sagas, trust had been sitting near its ceiling while some house was under a scandal nine years in ten:
  catching a thief raised it more than the theft lowered it. Now routine honesty only mends it, a point a year;
  something notable is needed to lift it; and a caught thief no longer outweighs the theft. Two events.
- What a person of your house does to an estate (a judge's ruling, a governor's returns) now lasts: it fades a point
  a year instead of being gone within the year.
- A bench or a desk left empty costs its own house with the commons or the merchants, not the Reach's trust. An
  aedile who cannot afford the works is no longer held against the magistrates' name.
- **A victory teaches its commander only what they have it in them to learn.** It used to add a step whatever the
  skill's ceiling. Measured: a rival general of the first rank is rarer than before (docs/CAESAR.md, docs/GAPS.md C9).
- The gap register (docs/GAPS.md) has a worklist at its top, a section for forms of government as the owner set them
  out, and a section for positions with nothing to do. 51 open, 37 closed.

### Fixed
- **A test that failed once in CI, at its cause.** The parties kept one shared copy of the laws for every world, so
  two tests running side by side with different content could overwrite each other's, and a house that should have
  crossed the floor did not. Each world a controller runs now has its own. Found from a red CI run on a commit that
  touched only two screens.
- The Exchange's Buy and Sell buttons ran off the edge of the goods table at laptop width.
- On the family tree, one who had married into the house was drawn in the first row, with the founders.
- **"Your standing" on the Senate screen could not be read on a laptop**: five panels were squeezed into strips with
  a scroll bar each. They are on tabs of their own.
- The Senate's bench legend was printed over itself, by a style added with the Command Deck. "You moves that" is "You
  move that".
- **The worlds and the ledger could not be seen on a laptop.** Two panels added under the House screen's grid took
  all of its height in a window 900 pixels tall, and the grid above them was squeezed to nothing. They are tabs now.
- The wealth table's bars were drawn under the house names instead of beside them.
- **Panel headings wrapped into ragged columns** on every screen (a title beside three lines of capitals). The
  subtitle now sits on its own line under the title, in ordinary type.
- **The player's own character could be made to rebel out of their own house.** A world taken by another house kept the
  governor its old house had seated, and an ambitious governor who hated the new owner could declare the world for
  themselves, the player's character included. A governor now leaves with the house that lost the world, and the
  player's character never does what the player did not choose. Found by the survival probe, which now says how each
  lost line ended.
- **A save could drift from an unbroken run** when a magistrate's relief, a senator's speech for a region or an
  officer's charge moved several houses' regard in one go: the houses were walked in an order that a load could
  change. They are now walked by name. The soak caught it.
- **A new head had no year in the Senate** if the old one died after the year's business was drawn up. The seat is the
  house's: the year passes to whoever has the house.
- **A saved game could drift from an unbroken one.** Loading a save noted the crews' commanders a second time, which
  put a commander appointed in the year of the save one point of devotion ahead. It is now done once, at the start of
  the first year in the Reach. The soak caught it; a test holds it.
- When a house other than the player's took the throne, the Reach's resentment was recorded against the player. It
  is now recorded against the head of the house that took it.
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
- **What sets off the great war.** It no longer comes by the calendar. Its clock is moved only by what is wrong with
  the Reach (lawless regions, a capital that is not respected, a war abroad going badly, wars between houses, one
  house overshadowing the rest, a general's march, a region breaking away) and **runs back when nothing is**. Camps
  that had formed stand down. A well-kept Reach may never have a great war. The panel shows how near it is and why.
- **How the great war ends.** It now lasts **ten years at the least**, whatever its clock says. **Holding the
  capital is winning, not having won:** the other side fights on. After the tenth year it ends when one side is
  **crushed** (a tenth of the victor's hulls or less, and the victor holds the capital), by **partition** (a losing
  side that holds a region keeps it as a state of its own), or by **exhaustion** (sixteen years, or both sides at half
  the hulls they began with). The player's house is never taken out of the Reach for it: you ask for a partition, or
  you fight on.
- **The Republic stands unless the player ends it** (docs/PROCESS.md, the owner's rule of 2026-11-14).
  - **The War of the Reach no longer makes an emperor.** It used to end the Republic in about two games of three,
    around year 28 to 40. It is still fought and won; the victor gains legitimacy, glory and rank; the Senate still
    sits; and the Republic is shaken for fifteen years.
  - **Another house's general marches only through three gates:** a commander of the first rank (tactics 6, four
    battles won, a triumph), loved (charm or the squares' hero, crews who would follow, a respected house), and the
    times (two or more things wrong with the Republic at once). One gate short, he comes home when recalled.
  - **The player may still try without them,** and the gates decide who stands with them: a sound Republic is
    defended. The Military screen shows the three gates, what each lacks, the state of the Republic, and how many
    houses would declare each way if the house crossed today.
  - Three warning events and an outliner alert: the Republic falters, a rival of great name rises, the hour is yours.
- The law career's ranks **Magistrate** and **Praetor** are now **Judge** and **Justice**, so that "praetor" means
  one thing: the elected office.
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
- **A gap register** (docs/GAPS.md, `tools/gaps.sh`) and the rule for keeping it (docs/PROCESS.md): every thin thing
  in one list with its size, added in the same commit that makes it; close before you open.
- **A state as a neighbour** (docs/BREAKAWAY.md). Recognition signs a **ten-year treaty** that bars war either way.
  When it lapses a rested Reach may vote a **Reconquest**, and a state that holds on is recognised again. The ruling
  house may **rejoin the Reach**. A state's name is on its region on the map and the Worlds screen.
- **Could it stand alone?** (docs/BREAKAWAY.md). A region that would break away is asked three things, and the
  answers move the Reach's temper: fuel of its own (without it the Reach closes its exchanges while the state fights),
  a treasury that pays its way, and something the Reach cannot do without (half its helium-3, rare earths or
  isotopes; with that the Reach's will runs out faster).
- **Working on the region's houses.** A quiet word or a gift raises a neighbouring house's regard; at 25 it gives its
  word to go with the region. A house that is the capital's creature reports the approach.
- **Breaking away** (docs/BREAKAWAY.md), on the Senate screen under Your standing. A house that holds a quarter of
  a region's worlds, and three in five with the houses there that would follow it, may take the region out of the
  Reach as **a republic, a republic under one hand, or a kingdom**. Whether the Reach fights depends on its
  **temper**: one that has just fought a great war, faces a strong breakaway, or would have to reach the rim lets it
  go. Otherwise it is a **war of independence with no fixed end**: the state is recognised when it has held the
  region and outlasted or beaten the Reach, and brought back if it loses the region or the field. You may declare with
  one ship and fight for as long as you can. A house that fought a general who then took the throne **need not
  kneel**. Eight events, `tools/browser/breakaway.js`.
- **The Caesar road** (docs/CAESAR.md), on the Military screen. Crews come to follow the commander who wins with them
  (**devotion**, beside loyalty to the house), and a commander's victories, crews, triumphs and rank make up his
  **sway**. The Senate may vote a general a **triumph** after three victories, or **recall** one with sway. A recalled
  general comes home, or his house **crosses the line**: a civil war in which every other house declares for him, for
  the Senate or stands aside, and a few fight. Hold the capital's orbit two years or break the Senate's fleets and
  **the Republic ends** with the general's house on the throne; lose and the general dies and the house is ruined
  for a generation. When another's general marches you declare for a side or say nothing. A relative who is the
  general keeps their own triumph and their own crews; what the house does about a recall is the head's to say.
  Twelve events, a donative for the crews, `tools/browser/caesar.js`, and a probe: `praetoria caesar`.
- **The ladder of offices** (docs/LADDER.md), on the Senate screen. Three elected offices below the consulship:
  **quaestor** (four, from 25), **aedile** (two, from 30) and **praetor** (two, from 34), each held by a person for
  five years. A **consul must be forty and have been praetor.** The houses vote with their weight and a candidate's
  own speaking, standing and record count besides. The head stands for themselves and may call in what is owed;
  **relatives decide for themselves whether to stand**, and the head may speak for them or ask them to stand down
  (the loyal do, the wilful do not). An aedile gives games out of their own purse. Office pays, teaches its skill and
  adds to the house's standing in the Senate. A saga opens with the seats filled. Five events,
  `tools/browser/ladder.js`.
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
