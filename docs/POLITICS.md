# Laws and estates

GDD §6.1 names legislation and §9 names resource policy among the things a head of house decides; the owner asked for
them in Democracy-4 terms — kinds of law, and groups that support and dislike them. This is that layer.

## Positions and their laws

Every law has a **scope**: the position you must hold to set it. The effects fall where the position reaches.

| scope | who may set it | the effects fall on |
|---|---|---|
| house | any head of house | the house's worlds and fleets |
| world | a house that holds a planet | its worlds |
| region | the dominant house of a region (most worlds there) | the regions it dominates: lawlessness, allegiance, and their worlds |
| chair | the capital's holder | the whole Reach (`reach: true`) |
| office:admiral · office:treasurer · office:speaker | the office's holder | the holder's fleets · the Reach's exchanges · every other house's motions |
| emperor | the throne | the whole Reach |
| bench | the leader of a Senate faction | the leader's votes |
| senate | no house — only an **Act of the Senate** carried by motion (docs/SENATE.md) | the whole Reach |

`content/politics/laws.json` ships twenty laws in seven categories — three of them the Senate's own (the Common Law, the Trade
Charter, the Levy of the Reach), set only by an Act carried in the chamber. The other seventeen are — the tithe, the levies, the court, the household
guard; tariffs, labour, justice, the franchise; march law and settlement; the tolls of the Reach and letters of marque;
fleet doctrine, the exchanges, standing orders; the imperial edict; the bench's platform. Each has three options with a
**default** every house holds until it chooses otherwise; changing one costs the law's influence (`EnactLawCommand`) and
settles it for its cooldown. The tithe's options are the tax policies of `exchange.json`.

## Effects

An option's effects are **modifier keys** that the systems read through `PolicySystem.Modifier` (the house's own laws
where it holds the scope, plus the estates' regard for the player's house) and `PolicySystem.ReachModifier` (reach-wide
laws held by whoever holds the chair, the throne or the office). The law of the Reach binds only the houses of the Reach:
where a house is named it is read through `PolicySystem.ReachModifierFor`, which is nothing for a house that has broken away,
bound only by its own laws (docs/BREAKAWAY.md). The keys:

`credits_per_world` · `materials_per_world` · `manpower_per_world` · `unrest_drift` · `prosperity_drift` · `pop_growth` ·
`lawlessness_drift` · `allegiance_drift` · `venture_yield_pct` · `charter_pct` · `market_price_pct` · `fleet_upkeep_pct` ·
`readiness` · `senate_weight` · `legitimacy_per_5` · `motion_cost` · `tolls_bonus`

The economy reads the first three on every world (region laws only on worlds in regions the house dominates), the drifts
in accrual, the percentages on ventures, charters, the open market and fleet upkeep; the regions read lawlessness and
allegiance from their dominant house and the chair; the fleet reads readiness; the Senate reads weight and motion cost;
legitimacy moves every fifth turn.

## The estates

`content/politics/estates.json` names six interest groups — the Commons, the Merchants, the Officers, the Old Blood, the
Magistrates, the Frontier — each with an **approval** of the player's house (counter `estate:<id>`, −100..100). Every
turn it drifts three points toward what the laws say (the sum of each option's opinion of the estate, over the laws the
house holds the scope for) plus what events have cultivated (`estate_offset:<id>`, decaying one a turn). Past +30 an
estate is **loyal** and its loyal effects join the house's modifiers; under −30 it is **hostile** and its hostile effects
do. A harsh tithe, bonded labour and conscription together put the Commons at −70 and cost two unrest a turn on every
world; a standing guard doubled pleases the Officers and costs a quarter more upkeep.

The estates are not only moved: an estate hostile across enough of the Reach **petitions the Senate** for the Act it
wants (docs/SENATE.md, *Petitions*), and the chamber's answer is felt in every house of the Reach. Heard, its regard rises
everywhere; refused, it falls and its worlds stir. A house that has broken away is not counted and does not feel it. The
Chancery's estate cards show a petition in progress, and warn when an estate is hostile in enough houses of the Reach that
one is near.

## Rivals

Every house begins with its **culture's laws** (`content/politics/cultures.json`): the imperial core keeps a closed court,
a closed franchise, iron law and a doubled guard; the concourse trades free with guild rights and an open hall; the
vergeborn and palefolk of the rim keep lenient courts, open lanes and homesteads; the spirean tithe harshly and bond
their labour; the marchers live under martial law and conscription. The player's house starts with its culture's laws
too. Every house has its own **estates** (counter `estate:<house>:<id>`), drifting toward its laws the same way, with
the same loyal and hostile effects — so a rival with a harsh tithe and bonded labour has hostile Commons and two more
unrest a turn on every world, and its risings are its own doing. Rival **stewards legislate** (`NpcAi.Legislate`): about
one house in eight a turn relaxes whatever a hostile estate hates most, eases the law that drives unrest hardest when a
world of its is restless, tightens the tithe when the coffers are empty and
loosens it when they are full, conscripts and doubles the guard at war and stands down at peace, declares martial law
over a lawless region it dominates, raises the Reach's tolls when it holds the chair and is poor, and sets an aggressive
doctrine when it holds the Admiralty at war. The Houses cards show a rival's laws off the customary and its loyal or
hostile estates. Counter `rivals_with_hostile_commons`; *The Exiles* brings a harsher house's commons to your port.

Flags: `estate_loyal:<id>`, `estate_hostile:<id>`, `law:<law>:<option>`; counters `estate:<id>`, `estate_offset:<id>`,
`laws_enacted`. Six events in `content/events/politics.json`: *The Commons March*, *The Merchants' Delegation*, *The
Officers' Grievance*, *The Old Blood's Letter*, *The Magistrates' Find*, *The Frontier Rides In*.

## The Chancery

The Laws screen: the six estates on the wall with approval, where the laws will take it, mood and what the mood does;
the laws in your gift by category, each option with its effects and the estates' opinions coloured; the laws beyond your
gift and who may set them. The tithe is set here now, not on the House screen.

## Local law: a law for one world

**The owner asked (2026-11-24):** "Then take a look at laws that are currently in the game and if they do anything and then more
laws that can be done. Executive edits, laws to vote on, local laws, etc"

A law of scope "world" in `laws.json` falls on every world a house holds; nothing could be decreed for one world. A local law is
set on **one** world by the head of the house that holds it, and falls on that world alone.

### What was built

- **State.** A world keeps its own local laws: `Holding.LocalLaws`, law id to option id. A law not named is at its default, so a
  world that keeps the custom carries nothing. It is saved with the world.
- **Content.** `content/politics/local_laws.json`, eight laws with a default and one or two other options each (loader:
  `LocalLawCatalog`, carried as `PolicyCatalog.Local`). `laws.json` is not touched.
- **The reader.** `LocalLawSystem.Effect` adds a world's own local laws to the rules that are already worked world by world, one
  added term on each line: the world's line of the ledger (`credits_per_world`, `materials_per_world`, `manpower_per_world`), its
  yearly unrest and prosperity and the growth of its people (`unrest_drift`, `prosperity_drift`, `pop_growth`), all in `Economy`.
  Two effects are new and each has its reader and a test: `lawlessness_drift` on a world is added to the lawlessness of that
  world's own region (`RegionSystem`), and `closed_to_ventures` refuses any other house that would found a venture there
  (`FoundBusinessCommand`, a company's founding included). The lists a player reads name the law as a cause, in words: a world's
  unrest shows "local law: a corn dole -3", its prosperity "local law: a free port +6".
- **Estates.** What the estates think of a local law weighs on the holder's approval by the share of its worlds: the opinions
  over all its worlds are added and divided by the number of its worlds, rounded to the nearest whole number with a half going
  away from nought (7.5 is 8, -2.5 is -3). A house with one world feels its local laws in full. The Laws window's line under each
  estate says it: "local law on 1 of 2 worlds +8".
- **The command.** `SetLocalLawCommand(actorId, holdingId, lawId, optionId, PolicyCatalog cat)`. Only the head of the house that
  holds the world; it costs the law's influence; the law is then settled on that world for the law's years. Setting a law back to
  its default lifts it. Refusals: "only the head of House Vega makes law for its worlds", "Ember Hold is not held by House Vega",
  "a local law is made for a world, and Tessaly Post is not one", "already the law on Barony", "decreed too lately on Barony:
  settled for 3 more years", "Barony can carry 2 local laws off the custom and has 2: lift one first", "needs 1 influence this
  year (0 left)".
- **A world that changes hands keeps its local laws** until the new holder changes them, and the new holder may change them at
  once: the years a decree is settled for belong to the house that made it (counter `local_law_cd:<house>:<holding>:<law>`). The
  estates of the new holder feel the laws from the day it takes the world, and the old holder's stop.
- **The limit.** A world carries two local laws off their default; the seat of a house, or a world whose development is at level
  6 (60 of 100) or more, carries three. A law already off the custom may be moved to its other option, and any may be lifted,
  whatever the count.
- **Other houses use it** (`LocalLawSystem.Tick`, at the year's turn, after the houses' own acts and before the Senate). Each
  head looks at its worlds in id order and makes at most one decree a year, in about one year in two (a hash of the house and the
  year) unless a world of its is in revolt. The plain reasons, in order: a world in revolt, at unrest 50 or more, or taken from a
  house that still stands and at unrest 15 or more, is put under martial rule, lifted at unrest 5; a surcharge is lifted once the
  world's unrest reaches 20 or the treasury holds 2,000 credits; a world stirring at unrest 15 or more gets a corn dole where the
  treasury holds 800 credits, and a garrison quartered where it does not, each lifted at unrest 5; a world at unrest 10 or under
  and prosperity 55 or over is given a surcharge when the treasury holds under 400 credits; a peopled world under three fifths of
  its room gets settlers' grants when the treasury holds 1,000 credits, stopped at four fifths or under 300 credits. A house does
  not decree what would bring one of its estates within 5 of hostile, unless a world of its is in revolt. A reason the world
  cannot take (it carries its limit of laws, or an estate would turn) gives way to the next reason on the same world, and a world
  in that trouble has any law of its that raises unrest lifted (a surcharge, a labour levy, a veterans' colony), so that a world
  taken with its limit of laws has the surcharge lifted one year and is put under martial rule the next. It is all
  deterministic. Every decree goes into the chronicle: "House Corwin decrees for Ember Hold: a surcharge on the tithe."
- **Screens.** Wherever one world is looked at (its card on the Worlds window, and the system panel), a "Local law" block
  (`LocalLawBlock.razor`, `data-local-laws="<holdingId>"`): each law (`data-local-law="<lawId>"`) with the option in force and
  how long it is settled; on a world of the player's, the other options as buttons (`data-local-option="<lawId>:<optionId>"`),
  what each does and who minds in the title, the refusal in the title when it is greyed, and a question before it is decreed;
  the count off the custom against the limit (`data-local-count`). A world of another house shows its laws without buttons. The
  Laws window, on "The laws in your gift", has a short line for each world of the player's under a local law
  (`LocalLawSummary.razor`, `data-local-summary="<holdingId>"`).
- **A probe.** `dotnet run --project src/Tools -- locallaws --seeds 2`: the passive-player loop of `src/Tools/LawsProbe.cs`
  (every event answered with its first open choice, nothing else done), two games of thirty years of the Old Lion, every change of
  a local law by a house that is not the player's counted by law and option.

### The local laws

Amounts are as the game shows them. Unrest and lawlessness are a year's drift; prosperity is where the world's prosperity settles
(the target it drifts toward, five a year).

| Law | Option | What it does on that world | Who minds |
|---|---|---|---|
| **The tithe of this world** (1 influence, settled 4 years) | The customary tithe (the default) | as things are |  |
|  | The tithe remitted | costs 200 credits a year; unrest eases by 3 a year; prosperity settles 4 higher | commons +10, magistrates -5 |
|  | A surcharge laid on | brings 200 credits more a year; unrest rises by 5 a year; prosperity settles 4 lower | commons -15, magistrates +5, merchants -5 |
| **The garrison** (1 influence, settled 4 years) | No garrison (the default) | as things are |  |
|  | A garrison quartered | 1 less manpower a year; unrest eases by 2 a year; prosperity settles 2 lower | commons -10, officers +5 |
|  | Martial rule | 1 less manpower a year; unrest eases by 5 a year; prosperity settles 6 lower; its people grow by 1 less a year | commons -20, magistrates -15, officers +10, frontier -5 |
| **The charter** (1 influence, settled 6 years) | Governed from the seat (the default) | as things are |  |
|  | A town charter | costs 100 credits a year; unrest eases by 1 a year; prosperity settles 4 higher | merchants +10, old blood -10, commons +5 |
|  | A free city | costs 200 credits a year; 1 less manpower a year; unrest eases by 2 a year; prosperity settles 6 higher | old blood -20, merchants +15, commons +10, magistrates -5 |
| **The port** (1 influence, settled 6 years) | Customs kept (the default) | as things are |  |
|  | A free port | brings 100 credits more a year; prosperity settles 6 higher; lawlessness in its region rises by 2 a year | merchants +15, magistrates -10, frontier +5 |
|  | A staple port | brings 200 credits more a year; prosperity settles 4 lower | merchants -15, magistrates +10 |
| **Settlement** (1 influence, settled 6 years) | No grants of land (the default) | as things are |  |
|  | Settlers' grants | costs 200 credits a year; its people grow by 2 more a year | frontier +10, old blood -10, commons +5 |
|  | A veterans' colony | costs 100 credits a year; 1 more manpower a year; unrest rises by 2 a year; its people grow by 1 more a year | officers +15, commons -10 |
| **The labour levy** (1 influence, settled 4 years) | No levy of labour (the default) | as things are |  |
|  | A levy for public works | 2 more materials a year; unrest rises by 3 a year; prosperity settles 2 lower | commons -10, magistrates +5 |
|  | The heavy levy | 4 more materials a year; unrest rises by 5 a year; prosperity settles 4 lower; its people grow by 1 less a year | commons -20, magistrates +5, merchants +5 |
| **Bread and games** (1 influence, settled 4 years) | No dole (the default) | as things are |  |
|  | A corn dole | costs 200 credits a year; unrest eases by 3 a year | commons +10, magistrates -5 |
|  | A corn dole and games | costs 400 credits a year; unrest eases by 5 a year | commons +15, magistrates -10, old blood -5 |
| **The gates** (2 influence, settled 8 years) | Open to the Reach (the default) | as things are |  |
|  | A closed world | unrest eases by 1 a year; prosperity settles 6 lower; lawlessness in its region falls by 1 a year; no other house may found a venture here | merchants -20, old blood +10, frontier -5 |

| Law | The real law it is modelled on |
|---|---|
| The tithe of this world | Tiberius remitted for five years the tribute of the cities of Asia thrown down by the earthquake of AD 17; the later Empire laid the superindiction, an extraordinary levy, on a province that could bear it. |
| The garrison | The Quartering Acts of 1765 and 1774 billeted the king's troops on the American colonies; the rule of the Major-Generals, 1655 to 1657, put the counties of England under soldiers. |
| The charter | The Flavian municipal charter of Irni in Spain, about AD 91, and the Magdeburg rights of the medieval towns; the free imperial cities of the Holy Roman Empire answered to the Emperor alone. |
| The port | Rome made Delos a free port in 166 BC, and it became the great mart of the pirates; Livorno was opened to all nations in 1593. The Staple fixed at Calais in 1363 made all English wool pass one harbour. |
| Settlement | The headright grants of Virginia, 1618, gave fifty acres for every person brought over; Sulla and Augustus settled their discharged legions in colonies on land taken from its holders. |
| The labour levy | The royal corvee of France, made general in 1738, took days of unpaid labour for the king's roads; the Highways Act of 1555 laid statute labour on every English parish. |
| Bread and games | The corn law of Gaius Gracchus, 123 BC, sold grain to the citizens of Rome below its price, and Clodius made it free in 58 BC; Juvenal's bread and circuses. |
| The gates | The edicts of 1633 to 1639 that closed Japan to all but one Dutch factory; the Spanish Crown's closing of the trade of the Indies to every foreigner through the House of Trade at Seville, founded 1503. |

### What was measured

Two games of thirty years of the Old Lion (seeds 1 and 2), a player who does nothing, `locallaws --seeds 2`:

| | Seed 1 | Seed 2 | Both |
|---|---|---|---|
| Changes of a local law by other houses | 37 | 39 | 76 |
| Houses that made one, of those in the game | 14 of 28 | 16 of 28 | 30 of 56 |
| Worlds of other houses under a local law after thirty years | 7 | 5 | 12 |

Across both games, decrees made: a surcharge on the tithe 29, a garrison quartered 8, settlers' grants 6, martial rule 1; and
lifted again: the surcharge 17, the garrison 8, the grants 6, martial rule 1. A world of another house was under some local law
in 415 of its 1,641 world-years. Against that, other houses changed their own laws 106 times in the same sixty years of play
(docs/LAWS_REVIEW.md, section 3).

The economy (`econ --seeds 2`), the standard row with no salaries, the median house's treasury at year 30: **2,110 credits
before, 2,430 after.** The other rows moved too, as any change in what houses do moves the games apart: lean 1,675 to 2,100,
generous 7,759 to 5,650, lavish 21,621 to 19,460, standard salaries 1,825 to 1,457. At year 30 no row has more than two houses in
the red (before, no more than one).

### What I chose

- Eight laws, their options and every number in the table above. The surcharge's numbers follow the harsh tithe's (200 credits
  more, unrest 5 a year), on one world instead of all of them, and with less harm to prosperity.
- One influence a decree, two for closing a world; settled four years (the tithe, the garrison, the labour levy, the dole), six
  (the charter, the port, settlement) or eight (the gates).
- Two local laws off the custom on a world; three on the seat of a house or at development level 6.
- The estates' opinions divided by the number of the house's worlds, a half rounded away from nought.
- A world that changes hands keeps its laws; the years a decree is settled for are the decreeing house's, so a new holder may
  change them at once.
- The other houses: one decree a house a year, in about one year in two unless a world is in revolt; martial rule at unrest 50, or
  15 on a world taken from a house that still stands, lifted at 5; the dole or a garrison at unrest 15, lifted at 5, the dole
  only with 800 credits in hand; a surcharge on a world at unrest 10 or under and prosperity 55 or over when the treasury holds
  under 400 credits, lifted at unrest 20 or 2,000 credits; settlers' grants under three fifths of a world's room with 1,000
  credits in hand and unrest under 15, stopped at four fifths or under 300 credits; nothing that would bring an estate within 5 of
  hostile.

### What is not built

- **Other houses use four of the eight laws.** They never grant a charter, free a port or make it a staple, levy labour, close a
  world, or settle veterans; in two games they never gave a dole, for no house with a stirring world had 800 credits in hand,
  and they put a world under martial rule once. In a quiet game the worlds of other houses are seldom restless: over the two
  games their unrest was under 5 in 1,455 of 1,641 world-years and never over 23.
- A closed world turns away new ventures only; those already founded there stay. A staple port does not draw the trade of the
  other worlds of its region.
- No events are written for local laws (a riot when the dole is stopped, a free port's smugglers), and the player is not told
  when another house decrees on a world: it is in the chronicle.
- A local law is made for a world, not a station or a claim, and the player's steward does not make one.
