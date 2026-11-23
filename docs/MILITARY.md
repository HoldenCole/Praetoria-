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

## Flags and counters for content
`battle_won`, `battle_lost`, `fought_at_the_front`, `hulls_captured`, `became_achilles`, `fleet_deserts`, `out_of_supply`,
`supply_low`, `fleet_restive`, `fleet_mutinous`, `campaign_declared`, `campaign_open`, `campaign_over`; counters
`valour:<character>`, `campaigns`, `campaign_until:<house>`.

## Beyond the Reach
Expeditions against the foreign nations (docs/FOREIGN_NATIONS.md) fight with this same math: `FleetSystem.PowerOf` for
the fleet, the house's flag factor, the nation's committed strength on the other side. Victories abroad count for
valour and the Achilles like victories at home; a commander at the front risks the same doubled death.

## Still to come
Stage 2: the Military screen's stance, loadout, style, supply and loyalty controls; the battle brief in the outliner;
supply shading on the map; a dozen situations. Then the Republic's parties, consuls and generals (docs/REPUBLIC.md) and
the wars beyond the Reach (docs/FOREIGN_NATIONS.md).
