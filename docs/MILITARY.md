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
