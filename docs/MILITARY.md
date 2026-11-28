# The military depth: tactics, command, supply, loyalty

Owner's direction (2026-11-09): deepen the military pillar — roles and promotions, campaigns, decisions in combat,
weapons and tactics, the character as front-line commander or general or Achilles figure, expanded fleet combat,
loyalties that move fleets, supply lines and resources. GDD §6 ("decisions: battles, deployments, loyalty-vs-orders"),
§13 ("each rung = command of larger resources"), §17 (the strategic three). The rules live in `content/fleets/rules.json`
under TACTICS, COMMAND, SUPPLY and LOYALTY; the engine is `FleetSystem` (`DepthFactor`, `FlagFactor`, `Supply`,
`Campaigns`, `Loyalty`, the battle's stance edge, prizes and commander outcomes) and the commands in `FleetCommands.cs`.

## Tactics: the stance and the loadout
Every fleet fights in a **stance**, set free of charge (`SetStanceCommand`):

| stance | power | own losses | note |
|---|---|---|---|
| line (default) | ×1.0 | ×1.0 | |
| press | ×1.15 | ×1.3 | the charge |
| skirmish | ×0.9 | ×0.6 | harass and evade |
| boarding | ×1.15 | ×1.3 | the press with prize crews: on victory, 2 manpower buys one hull per four the enemy lost |

The wheel: **the line beats the press, the press beats the skirmish, the skirmish beats the line** (boarding counts as the
press); the side with the edge fights at ×1.15. The dominant stance of a side is its largest fleet's. Rival houses pick a
stance by the odds (`NpcAi.Posture`): press when they outgun every enemy by half, skirmish when outgunned, boarding when
a tactician has manpower to spare.

A fleet's **loadout** is guns or **torpedoes** (`SetLoadoutCommand`, loaded at a world of your own with an isotope in
store): with isotopes in the house's store a torpedo fleet fights at ×1.25 and burns one isotope a battle whatever the
outcome; with the magazines empty the tubes are so much dead weight. This is the GDD's "special materials needed for
weapons" made tactical.

## Command: the front, the flag, the Achilles
A commander (`Fleet.AdmiralId`) has a **style** (`SetCommandStyleCommand`, flags `command:front` / `command:flag`):
- **The front.** The fleet fights at +10%, the crews' loyalty rises a point a turn for a commander who shares the line,
  and every victory adds a point of **valour** (`valour:<id>`). The commander's chance of dying with the line is
  doubled. At three points of valour the commander becomes an **Achilles** (flag `achilles`): +15% more to any fleet
  they lead, two legitimacy to the house, and a name the Reach speaks — the raw material of a Caesar (docs/REPUBLIC.md).
- **The flag.** A Commander or better on the military track may command from the rear: every fleet of the house fights
  at +3% per point of the commander's tactics (to +15%), and the commander never goes down with the line.
- **None.** The captains fight the ships.

**The command cap**: a commander may lead 2 hulls plus 3 per rank on the military track without strain (a Cadet 2, a
Lieutenant 8, a Captain 14); a fleet above its commander's cap fights at ×0.9. Promotions matter because fleets grow.

## Supply lines and campaigns
Every fleet carries **supply** (0–100). Within one lane of a world of its house it is in supply and recovers 25 a turn;
a **Fleet Supply Depot** (building, 6 materials and 400 credits) reaches two lanes. Beyond that the fleet drains 15 a turn
(8 within two lanes of home). Under 40 it fights at ×0.8 and loses readiness; a fleet that begins a turn at nought
loses a hull to desertion. Raiders live off the lanes.

A **campaign** (`DeclareCampaignCommand`, 2 influence, one at a time) names an enemy's world as the war's object for
eight turns: the house's fleets within two lanes of it fight at ×1.10 and count as supplied. Rival houses at war
declare one on the nearest enemy world. Flags for content: `campaign_declared`, `campaign_open`, `campaign_over`.

## Loyalty: the crews' and the commander's
Every fleet's **loyalty** (0–100, 70 to start) drifts +2 a turn when paid, fuelled and supplied; −4 when the house is
insolvent, −2 when the tanks are dry, −2 when supply is short, −2 when the house's **officers** estate is hostile (+1
when loyal), +1 under a commander at the front; +8 after a victory, −8 after a defeat. Under 30 the crews **will not
sail** on orders (the move is refused); under 15 a hull **deserts** each turn. Flags: `fleet_restive`, `fleet_mutinous`,
`fleet_deserts`.

## Laid up

Built 2026-11-21 from the playthroughs (docs/PLAYTHROUGH.md, Medium, War and peace: S50, S74). Before this the only way
to cut a fleet's cost was to pay hulls off: they were broken up for a quarter of their materials and were gone.

A fleet lying at a world or station of its house may be **laid up** (`LayUpFleetCommand`, one treasury action): the
hulls are kept and the crews paid off. While it is laid up (`Fleet.LaidUp`):

- it costs 25 in a hundred of its keep (`FleetRules.LaidUpUpkeepPct`), rounded up, so never nothing;
- it burns no helium-3, and its hulls are not in the house's fuel bill;
- it has no readiness and gains none;
- it takes no order that needs crews: to sail, to go beyond the Reach, to be hired out, to drill, to refit, to detach
  hulls, or to merge with a fleet in commission; each refusal says it is laid up;
- its hulls are not counted in the house's strength (`FleetSystem.StrengthOf`), wherever strength is weighed: the navies'
  table, a title's test of strength, what rivals and the Senate make of the house;
- if the system is attacked it fights as any fleet with no readiness does, at half weight.

It is **put back in commission** by the same command in any year (one treasury action): it may sail again and is counted
again, costs its whole keep and burns fuel, and starts with no readiness, gaining the usual ten a year at anchor. Nothing
is broken up either way. A fleet held by office, a levy and a hired company cannot be laid up. Only the player lays
fleets up: the other houses do not.

## A forecast of fuel

The Military window's helium-3 figure used to be the tanks divided by the year's burn. A house that brought up as much
as it burned was told it had a few years left. It is now `FleetSystem.Outlook`: what is in the tanks, what the fleets
burn, and what the year's accounts leave of helium-3 once that is burned. When more is burned than comes up, the years
are the tanks over the shortfall; when not, the tile says the tanks hold or fill, and by how much.

## Commanding a navy

Built 2026-11-23 from the owner's list (docs/PLAYTHROUGH.md, Hard):

> Commanding a navy: captains to appoint from the service, squadrons split and joined, supply lines, hulls laid up, and a
> year-end report of the war. The pieces are in Medium; together they are a system (S50, S65, S72, S74, S27).

The soldier's notes it comes from (docs/playthroughs/soldier.md): 27 (a world fell with no report), 50 (leave a guard),
65 (move the flag, "appoint captains from the service"), 72 (supply), 74 (lay up), 76 (an officer who could not be given
a command). Everything is on the Military window: each fleet's card on "Your fleets", and a new tab, "Wars".

### Captains

Each fleet may have a **flag captain** under its admiral (`Fleet.CaptainId`). A captain is a living member of the fleet's
house, of age, **in the naval service at the rank of Lieutenant or above**, who is the admiral of nothing
(`FleetSystem.CaptainRefusal`: one rule, for every house). `AppointCaptainCommand` names one for an agent;
`RelieveCaptainCommand` takes one off, free. Nobody is admiral and captain at once, nor captain of two fleets: a captain
named to a second fleet leaves the first, and a captain given a flag (`AssignAdmiralCommand`) leaves the captaincy.
A captain who dies, grows frail or leaves the house leaves the post. A levy keeps the officers its own house gave it.

What a captain does:

- **Readiness.** At anchor the fleet recovers a point more a year for each point of the captain's discipline. Under way
  it loses a point less a year for every two points.
- **Gunnery.** While the admiral commands, the fleet fights a hundredth harder for each point of the captain's gunnery.
- **Command.** A fleet with no admiral is commanded by its captain: it fights by the captain's own tactics, gunnery and
  leadership as an admiral's would be reckoned (`FleetSystem.CommandFactor`), not at 0.85, and it is the captain's
  command cap that the hulls are counted against. When the admiral falls in a battle the captain has the command from
  that moment, and the house is told. When the admiral is wounded the captain commands if the fleet does better so; a
  wounded admiral who would still do better keeps the command (`FleetSystem.Commander`).
- **The record.** A captain who commands in a won battle learns as an admiral does (the weaker of tactics and gunnery
  rises a point, within what they can learn) and it is written in the record of their life: "Won a battle as captain, in
  command of ...". One who commands in a lost battle may be wounded or killed, as an admiral may. The appointment is in
  the record too.

The other houses give their fleets captains by the same rule (`NpcAi`, with the admirals): the officer of the house with
the most discipline and gunnery who may be one. For them, as with their admirals, it costs no agent.

### Squadrons

- **The merge was backwards.** The option on a fleet's card reads "Fold <other> into <this>", and its refusal was reckoned
  that way, but the button folded this fleet into the other and so kept the other fleet's admiral. The button now runs
  the fold its label names, and the label says who commands afterwards. The receiving fleet keeps its admiral and its
  captain; if it has none it takes the other's.
- `MergeFleetsCommand.Refusal` gave no reason when one fleet was laid up and the other was not (`CanExecute` refused). It
  now says which is which and what to do.
- **A detachment of a chosen class.** `DetachHullsCommand` takes a class of hull (a list on the card: "the lightest", or
  any class the fleet has). Hulls paid off are still the lightest.
- **Under its captain.** A detachment may be sent under the fleet's captain. The captain commands the new squadron as its
  admiral, and the fleet is without a captain until another is named.

### Where a fleet stands

Supply, laying up, command and the cap were each said in a different place on the card. One line now says them
together (`FleetSystem.Stands`): in commission or laid up, and where; in supply or not, and why (`FleetSystem.SupplyWhy`:
the world of the house, the depot or the campaign's object that feeds it, or that none is within reach, and how fast the
supply falls); who commands and who is captain; and the hulls against the commander's cap. No convoys or supply lines
were added as things of their own.

### The account of a war

Each war between houses keeps an account by year (`War.Years`, kept by `WarLedger`). A year's account is closed when the
year turns, after the fleets are paid for, the battles fought and the sieges resolved. It holds: the battles and who won
them; the hulls each side lost, and the hulls taken as prizes by boarding; the worlds, stations and seats that changed
hands (taken by siege, ceded at the peace, or a province whose holder had no fleet left to keep it), and whether it was
the last the losing house held; the score as the year opened and as it closed; and what each house's fleets in
commission cost it in credits and in helium-3. A house in two wars bears its fleets once: the cost is shared between its
wars. What happens after the close (a peace, and what it cedes) is written into the war's last account, "at its end".

A war that ends, by a peace or because a house or a throne is gone, is kept among `World.PastWars` with its account and
how it ended. The accounts are kept for every war between houses, the player's or not.

It is shown:

- **In the year in brief**, under War, one paragraph for each war of the house: "The war with House Oriel, its third
  year: two battles, one won. You lost 3 hulls and sank 5. Cinder Gate was taken. The score stands 7 to 2; it was 4 to
  2. It cost 1,400 credits and 3 helium-3."
- **On the Military window, the tab "Wars"**: every year of every war of the house, standing and ended, and below them
  the wars between other houses.
- **As news**: a world taken or lost by siege, a world ceded to or by the house at a peace, a seat taken or lost, a
  captain taking the command when the admiral falls, and a war that is over, with the whole of it added up.

Found while building it: the battle lines in the year in brief were a year late. A battle is fought as the year turns and
is dated with the year that opens; the brief looked for battles dated with the year before. It now shows the battles
fought as the year turned.

### The numbers

| what | number |
|---|---|
| lowest rank of a captain | Lieutenant, the third rung of the Navy (`FleetSystem.CaptainRank` 2) |
| lowest age | 16, the age of majority |
| to appoint a captain | 1 agent; to relieve one, nothing |
| readiness at anchor | +1 a year for each point of the captain's discipline, to +5 (on the usual 10) |
| readiness under way | 1 less lost a year for every two points of discipline, to 3 less (of the usual 5) |
| gunnery, under an admiral | +1% for each point of the captain's gunnery, to +6% |
| a captain in command | the admiral's reckoning on the captain's own skills (tactics 5%, gunnery 3%, leadership 2% a point), in place of 0.85 |
| ended wars kept | 40 of the player's house, 12 between other houses |
| what a war's year cost | the credits of the fleets in commission and the helium-3 burned, shared equally between a house's wars, the odd part to the oldest |

### What I chose

- A captain is a Lieutenant or above. An Ensign or a Cadet is refused, and so is anyone outside the naval service.
- One agent to appoint, as for an admiral. Relieving is free.
- The captain's two skills are discipline (readiness) and gunnery (the fight), and the sizes above: +5 readiness at
  most, 3 less lost under way at most, +6% at most.
- A captain in command is reckoned exactly as an admiral, with no penalty for being a captain. The only limit is the
  captain's lower rank, which gives a smaller command cap.
- A wounded admiral hands over only to a captain who would do better.
- A detachment under the captain makes the captain its admiral, and empties the captaincy of the fleet it left.
- A war's first account is the year after it was declared.
- The cost in the account is credits and helium-3 only, of fleets in commission, and it is divided between a house's wars.
- 40 and 12 ended wars are kept.
- A war that ends otherwise than by a peace is kept with a plain reason ("House X was no more").
- The news of a world taken opens the map at the world. The news of a world lost opens the Wars tab.

### What is not built

- Convoys and supply lines as objects (by the owner's instruction).
- Pay for captains: the payroll pays admirals only.
- A captain is at no risk in a battle the admiral commands, and no report (event) is about a captain.
- A captaincy teaches nothing by the year, as an admiral's post does; a captain learns only by a battle won in command.
- In the player's house no relative takes up an empty captaincy unasked, as they do an empty command.
- The other houses do not detach squadrons under their captains.
- Wars with the nations beyond the Reach are not in the account: they keep their own tally (docs/BEYOND.md).
- Manpower for drill, materials for refits and isotopes for torpedoes are not in a war year's cost.
- Hulls paid off are always the lightest: only a detachment takes a chosen class.

## A command by service

Built 2026-11-26 from docs/IDEAS_FROM_MODS.md, entry 1, which closes the first and fifth rows of the worklist (docs/GAPS.md
Q15 and R1):

> The player, or anyone of their house, can be given command of a fleet that is not their house's own: as a legate under a
> consul, the Admiral of the Reach or the holder of emergency powers, or as an officer lent to an ally for that ally's war.
> The victories, the valour and the crews' devotion are theirs. The hulls stay the owner's, and the owner or the Senate can
> relieve them.

> (Q15) A new man cannot rise to a command by service. (R1) Nobody rises by service. A person cannot ... command another
> house's ships, the state gives a command only to an office-holder, and under a throne no command is given at all.

Before this a fleet's commander had to be of the house that owned it (`AssignAdmiralCommand`, `FleetSystem.CaptainRefusal`),
and a house without land never got a fleet: in the scripted general's games (docs/QA.md, Q2) nobody of the house commanded
one in 11 of 24 when it was first measured, and in 9 of 24 when this was begun.

### The ways in

- **A legate.** Whoever holds the consulship, the Admiral of the Reach's office or emergency powers may name a legate from
  any house to a fleet under them (`NameLegateCommand`, one agent, on the Senate window under the offices). What is under
  each: the Admiral, the office fleets under the house's colours and the house's own fleets; a consul, the house's own
  fleets, and the state's fleet when no Admiral holds it; emergency powers, the house's own and every office fleet. Under a
  throne the Emperor holds every office, so the throne names legates too. Never another house's head (a head of house stays
  at home), nobody of the raiders or of a nation, and nobody of a house at war with the house whose fleet it is. The
  commission lasts while the one who named them holds the office; if the office passes to another of the same house, that
  one keeps the legate on.
- **An officer lent.** The head of a house at war may take an officer of another house to command one of its own fleets
  (`BorrowOfficerCommand`, one agent), with the leave of that house's head. The head gives leave to a house it thinks 10 of,
  or 0 if it is in the same war, and not while its own house is at war with somebody else. A head is never lent. The
  officer must not think worse than −20 of the one they would serve. It lasts while the owner is at war.
- **Seeking a command.** The head offers their own service, or a grown relative's, to an office-holder or a house at war
  (`SeekCommandCommand`, one agent, spent only if they are taken on). Taken or refused by plain reasons, each said with
  what is missing: tactics of 3 or a Lieutenant's rank in the Navy; renown of 10; the two houses at peace; the taker's head
  not thinking ill (below 0) of the seeker's head; the seeker not thinking worse than −20 of the one who would take them on;
  and a command free under the taker: a fleet of a house at peace with the seeker's, with no admiral, or one whose admiral
  has 2 less tactics than the seeker and is neither the taker, the owner's head, the player nor a general on the march. A
  relative who is defiant of the head will not go at the head's word.

### What it gives, and what stays the owner's

- **The commander's:** the victories (`CaesarSystem.Victories`, the triumph's count, the sway), the valour and the name of
  Achilles, what a victory teaches, the record of their life ("Won a battle in command of The Harker Guard, for House
  Harker, as legate."), the crews' devotion, and the house's general: `CaesarSystem.GeneralOf` now counts one of the house
  who commands a fleet of another's by service. A commander from the flag lends their tactics to the fleets of the house they
  serve, not to their own. The commander's house may pay the crews a donative and give the fleet a captain of its own house;
  that captain leaves with them. The war's account names who commanded each side's fleets by service.
- **The owner's:** the hulls (counted in the owner's strength), their keep and fuel, the admiral's wage, the prizes, the
  plunder of an expedition, and a standard taken or retaken: it goes to a commander of the owner's own, or to its head.

### How it ends

- The owner's head, or the one who named or took them, relieves them (`RelieveServiceCommand`, free): the one relieved thinks
  10 the worse of them. The Senate relieves by its recall, which a general of 40 sway or more can be given as before.
- The commander resigns, or the head of their house calls them home (`ResignServiceCommand`, free): whoever named them
  thinks 5 the worse of it.
- It ends of itself when what it rested on is gone: the office (unless it passed within the house), the war, the fleet, the
  peace between the two houses, the commander's life or health, the commander's house, or a fleet laid up. A commander who
  grows frail or lays their work down ends it at the year's end, and the owner is told why. A commander who dies gives it
  up that day, and one cast out of their house or taken back into one gives it up that day too (docs/HOUSEHOLD.md), so that
  nobody commands for a day the fleet of a house their own is at war with. A commander of another house who comes to lead
  their own house, by the old head's death or by a challenge, lays it down at the year's end: a head of house stays at home
  (the player's own head may serve). One who comes into the owning
  house (by marriage), or whose fleet passes to their own house, keeps the command, by right now and not by service. The
  owner giving the fleet to one of its own relieves the commander by service; the owner may also shift them to another of
  its fleets, or fold their fleet into another: the service goes with them into a fleet that had no admiral, and ends if the
  other has its own. A detachment is not sent off under a captain of the commander's house. One of the house who commands
  another's fleet by service and is given a fleet of the house (by the head, or granted what they asked for) lays the
  other down first, and its owner is told.
- An office fleet no longer keeps the admiral of a house that has lost the office (found while building this: the old
  Admiral's officer stayed aboard the Reach Squadron when the office passed). A legate named to it is kept.

### Other houses

Every year (`ServiceSystem.Tick`, after the Senate's business):

- **Office-holders** of other houses name a legate where a fleet of two hulls or more under them has no admiral, or somebody
  is a clearly better commander (2 tactics) than the one it has: only somebody with tactics of 4, a name, not the head of a
  house, from a house the holder thinks 20 of (or 0, of the holder's bench). One a year each, and two at a time at most. If
  the best is the player's head, the player is asked. A relative of the player's house in office does not give the house's
  own fleets to anybody (the head does), and on an office fleet of the house fills only a command that is empty; nor does
  any office-holder put out unasked a commander the player's head put on a fleet of the house.
- **Houses at war** with a fleet of two hulls or more that nobody of their own commands borrow an officer from a house whose
  head gives leave. One of the player's house is asked for, and the player gives leave or refuses.
- **Younger people** (not the head, 45 or younger, with no command, in the Navy or with tactics of 3) look for a command of
  their own when their house has none to give them, by the same rules the player is held to, with their head's leave if
  they would be lent. One in three of them looks in a given year, and nobody more often than once in five years. One who
  would serve the player is put to the player.

What is put to the player (`AnswerServiceCommand`) is answered on the Military window within the year, and shown in the
outliner. A refusal costs 5 of the asker's regard; leave given gains 10. The asker does not ask again for the same person
for five years, and turns to the next instead; an ask left unanswered for the year is taken as a no, without the cost. An
offer to the player as consul, Admiral or holder of emergency powers is taken on by the office (a legate), and one to the
player's house at war, for the war (an officer lent). Leave to lend a relative who has since become the head, or left the
house, no longer stands. To be named legate is asked of the head by name: when the seat passes (by a death at the year's
end, or a head standing aside), what was asked of the old head is not put to the heir, who can neither take it nor refuse it.

### The Caesar road

A legate whose crews come to love them is a general like any other: the triumph, the recall and the three gates
(docs/CAESAR.md) read the commander, not the owner. The hulls whose crews would follow count toward the four a march asks.
When another house's general would cross, the strength weighed is the house's own and the hulls that are the general's by
service; and when a general crosses, a fleet they command by service whose crews are theirs goes over with them and is the
general's house's from that day (as the legions of the state went with Caesar). The gates themselves are unchanged.

### On the screens

- **Military window, a tab "A command by service"** (`data-service`): the commands the house's people hold by service, each
  with a button to resign or call them home (`data-resign`); the house's own fleets under another's commander, and legates
  the player named, with a button to relieve (`data-relieve="<fleetId>"`); what is asked of the head (`data-answer-ask`); offers
  to seek a command for the head or a relative, to every office-holder and every house at war, each greyed with its refusal
  (`data-seek-command="<holderOrHouseId>"`); and, when the house is at war, officers it could ask for (`data-borrow`).
- **Senate window, Offices and parties:** a consul, the Admiral or the holder of emergency powers sees each fleet under the
  office with a choice of who could be its legate (`data-name-legate="<fleetId>"`).
- The general and the Senate: a fleet commanded by service by one of the house is listed for a donative.

### The numbers

| what | number |
|---|---|
| to name a legate, borrow an officer, or be taken on when seeking | 1 agent; to relieve, resign or answer, nothing |
| skill in command a taker asks | tactics 3, or a Lieutenant's rank in the Navy |
| a name a taker asks | renown 10 (a Lieutenant's rank alone gives it) |
| regard the taker's head must have for the seeker's head | 0 or better |
| a command is free | no admiral, or an admiral with 2 less tactics than the seeker |
| leave to lend an officer | the lending head thinks 10 of the borrower, or 0 in the same war; not while at war with another |
| the most ill an officer may think of the one they serve | −20 |
| regard moved | named: +10 to the one who named them; the two heads +5 each way; one displaced −10; relieved −10; resigned −5; asked and refused −5; leave given +10 |
| a no, or an ask left unanswered, is remembered | 5 years: the asker does not ask for the same person again, and asks the next |
| other houses' legates | tactics 4 or more, a fleet of 2 hulls or more, from a house thought 20 of (or 0 of the same bench), one a year, two at a time |
| other houses' seekers | 45 or younger, not the head, one in three a year, once in five years |
| ended commands kept | 40 |

**Measured** (`praetoria roads --scenario all --seeds 3 --turns 60`, the scripted general on eight starts and three seeds,
before and after; and `praetoria service`, a passive player):

The scripted general (docs/QA.md, Q2) now seeks a command by service for the head or a grown relative when nobody of the
house commands anything, and takes one that is offered, while it goes on building a fleet of its own:

| The general, 24 games of 60 years | before | after |
|---|---|---|
| one of the house commands a fleet (its own, or by service) | 15 | 22 |
| nobody of the house ever commands one | 9 | 2 |
| a battle won | 17 | 17 |
| the Senate votes a triumph or a recall for the house | 7 | 6 |
| the line is crossed | 5 | 3 |
| the march ends: a throne, or broken | 2 | 3 |
| the march is swallowed by a larger war | 4 | 0 |

It sought a command and was taken on 48 times, and took one offered 4 times. What refused it most: tactics under 3 with no
rank in the Navy (222 years, the head or a relative who is not a sailor), a house at war with the taker (125), and a taker
who thinks ill of the house (71). (Measured again after the review's mends: the table above is unchanged, and before them it was
taken on 68 times.)

With nobody steering the player's house (`praetoria service`, a passive player), measured again on the merged build after
the review that followed the merge (the three ideas of docs/IDEAS_FROM_MODS.md together):

| | two games of thirty years, the Old Lion | sixteen games of thirty years, every start |
|---|---|---|
| commands by service begun | 19 | 65 |
| held by other houses' people | 16 (8 a game), of 7 houses | 61 (3.8 a game), of 11 houses |
| held by the player's own house | 3 | 4 |
| legates named by a consul | 18 | 41 |
| legates named by the Admiral of the Reach | 1 | 9 |
| officers lent to a house at war | 0 | 15 |
| of them sought by the one who held it | 3 | 22 |
| battles won under them, and lost | 0 and 0 | 3 and 0 |
| years held in all | 62 | 309 |
| put to the player to answer | 0 | 11 (28 before an ask left unanswered was taken as a no, when the same was asked year after year) |
| how they ended | the office passed 5, the one who named them died 4, another given it 3, the fleet passed 2, the commander died 2, the commander came to lead their own house 2; 1 standing | the commander came to lead their own house 16, the one who named them died 11, the office passed 10, the commander died 5, the fleet passed 4, another given it 3, the fleet gone 2, the commander cast out 1; 13 standing |

Most are a consul's legate on a fleet of the consul's own house, held a few years and ended by the consul's death or term,
or by the legate coming to lead their own house. The other houses' younger officers find a command a little more than once
a game. On the Old Lion the count is nearly twice what this idea gave measured alone (10 in two games), and it is the
friends handed down at a succession (docs/LIFE.md, "Friends and enemies handed down") that make it so: a holder who
thought well of a house's old head thinks 20 the better of the new one, and 20 is what a holder asks of another house to
name a legate from it. Played the same way with nothing handed down, the two games give 11. That is the rule as meant (a
friendship handed down is a friendship), and it is left so.

The Caesar road, measured as docs/CAESAR.md measures it (`praetoria caesar --scenario all --seeds 10`: six Republic starts,
ten seeds, sixty years, a passive player). The doc's rule is that another house's general marches about one game in ten or
less; it was rarer than that before, and it is the same now:

| another house's general, in 60 runs | before | after |
|---|---|---|
| was of the first rank | 5 | 4 |
| was loved | 51 | 53 |
| was both | 2 | 0 |
| passed all three gates | 1 | 0 |
| would have marched | 1 | 0 |
| civil wars, and the Republic fell | 0 and 0 | 0 and 0 |

So the road is open to a legate (the tests show one counting devoted crews of another's fleet, and the crews going over), and
it is no more often walked: a legate's commission is short, and the gates are unchanged.

### What I chose

- Three authorities name legates, and each over its own fleets as listed above; the consul has the state's fleet only when
  no Admiral holds it, so that the Admiral is not overruled by a consul.
- A commission ends with the office, but is carried over when the office passes within the same house.
- An officer lent goes home when the owner is at war with nobody; a head is never lent; a house at war with another does not
  lend.
- The gates for being taken on are tactics 3 or a Lieutenant, renown 10, a head who thinks 0 or better, and a command free:
  no admiral, or one with 2 less tactics. The same gates for every house.
- One agent for each act that gives a command; relieving, resigning and answering are free.
- The regard figures in the table.
- Other houses: tactics 4 for a legate, fleets of 2 hulls, 20 of regard, one a year and two at a time; seekers 45 or younger,
  one in three a year, once in five years. These were set so that a command by service is a thing that happens a few times
  in a game, not every year.
- The Senate relieves only by its recall; there is no new motion.
- The player's head is asked before being named legate or lent; a relative of the player's house named legate by another
  accepts by their own will (the head may call them home), but is not lent without the head's leave.
- A no, or an ask left unanswered, is remembered for five years, and the asker asks the next instead.
- A relative of the player's house in office gives none of the house's own fleets, and fills only an empty command on an
  office fleet of the house; no office-holder displaces unasked a commander of a fleet of the player's house.
- One who comes into the owning house keeps the command; folding a fleet carries the service into one with no admiral and
  ends it in one with its own; a captain of the commander's house is not given a detachment.
- A legate does not order the fleet's movements: the owner sails it, the legate commands it in battle and chooses their own
  style.
- A general's devoted fleets held by service go over to the general's house when the line is crossed.
- An office fleet's admiral of a house that no longer holds the office lays the command down.
- A command by service ends the day the commander dies, is cast out of their house or is taken back into one; one cast out
  into the house whose fleet it is keeps it by right. One of another house who comes to lead their own house lays it down at
  the year's end, as a head is never lent or named.
- To be named legate goes with the head it was asked of: it is not put to the heir.
- A friendship handed down at a succession counts toward the 20 a holder asks of another house, as any regard does.

### What is not built

- No report (event) about a command by service: what happens is told as news, on the Military window and in the outliner.
- A legate cannot order their fleet to sail or set its stance; only the owner's head can.
- A legate is paid only the admiral's wage the owner already pays; there is no share of the spoils for the commander's house.
- The player's own relatives do not seek a command of themselves; the head offers their service. Other houses' do.
- The Senate has no motion to relieve a commander by service short of a recall.
- Another house's head is never named legate or lent (only the player's head may serve).
- Nobody serves in another house's household (the other half of GAPS R1).

## A decided war ends

Built 2026-11-28 from the review against Steam (docs/STEAM_REVIEW.md, item 13). The review's long campaign found a war with the
player standing at nought to ten in its sixteenth year with every peace refused: the house ahead smelled victory and would not
hear of one, the house behind had no world it could give, and no fleet met another. Only wars between other houses ended of
exhaustion. Now a war past the weariness threshold (`warWearinessTurns` in content/fleets/rules.json, six years, the same that
makes both sides' worlds restless) is weary (`FleetSystem.Weary`), and:

- **The side ahead takes its price.** A weary house hears a peace that pays it (`WouldAcceptPeace`): the side behind can buy
  one with the tribute the score says, or a world, from the Military window. It still will not take a white peace from the side
  behind. Before the threshold the refusal says from which year it will listen.
- **A house ahead of the player's sends its terms** (`OfferTermsCommand`, the head flagged `offering_terms`): the report "The
  Victor's Terms" (`the_victors_terms` in content/events/war_front.json) offers paying the tribute, giving up a world, or fighting
  on. Fought on, the envoy comes back after three years at the soonest; meanwhile the terms stand under Wars in the Military
  window, and the war card and the alert say they have been sent.
- **A house behind or level sues** (the existing envoy, `the_envoy_of_peace`), as a house that has lost half its hulls already did.
  Its answers that name a price are open only to the side ahead (the condition `warLead`): its words say the envoy's house pays.

A war for a throne is not ended this way (docs/THRONE.md). `tests/WarWearinessTests.cs` holds it: the price taken once weary
and refused with its year before; the terms sent, the report bound and its answers open, the tribute paid and the war over; the
envoy asking no price of a house behind; and a weary war with the player never standing unanswered for four years.

## Flags and counters for content
`battle_won`, `battle_lost`, `fought_at_the_front`, `hulls_captured`, `became_achilles`, `fleet_deserts`, `out_of_supply`,
`supply_low`, `fleet_restive`, `fleet_mutinous`, `campaign_declared`, `campaign_open`, `campaign_over`; counters
`valour:<character>`, `campaigns`, `campaign_until:<house>`. A command by service, for a year after: `service_given`,
`service_ended`, `service_asked`; counters `service_commands`, `service_commands_others`, `legates_named`, `officers_lent`,
`commands_sought`, `service_commands_ended`, `service_went_over`, `sought:<character>`,
`service_refused:<asker>:<character>` (the year the player's head said no, or let the ask lapse).

## Beyond the Reach
Expeditions against the foreign nations (docs/FOREIGN_NATIONS.md) fight with this same math: `FleetSystem.PowerOf` for
the fleet, the house's flag factor, the nation's committed strength on the other side. Victories abroad count for
valour and the Achilles like victories at home; a commander at the front risks the same doubled death.

## Still to come
Stage 2: the Military screen's stance, loadout, style, supply and loyalty controls; the battle brief in the outliner;
supply shading on the map; a dozen situations. Then the Republic's parties, consuls and generals (docs/REPUBLIC.md) and
the wars beyond the Reach (docs/FOREIGN_NATIONS.md).
