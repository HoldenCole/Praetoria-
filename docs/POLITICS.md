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
laws held by whoever holds the chair, the throne or the office):

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
