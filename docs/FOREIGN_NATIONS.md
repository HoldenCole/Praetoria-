# Foreign nations: the wars beyond the Reach

Owner's direction (2026-11-09): the playable Reach is one nation. **Other nations** exist beyond it and serve as the
**targets of wars** — in the LOTGH mould, where after the Imperium one strong nation fights on, and in the Roman mould,
where a republic of many parties conquers many nations, large and small. **For now they are not on the map.** A later
expansion puts them there: conquest, contested border regions, new holdings to take.

## Off-map first (what this means for the engine)
- A `Nation` catalogue (`content/nations/*.json`): name, culture, strength (an abstract fleet count that drifts),
  temper (aggressive / defensive), wealth, what they hold that the Reach wants (deposits, a chokepoint), and a
  disposition toward the Reach.
- **Foreign wars** are declared by the Senate (a motion, or the consuls' prerogative in docs/REPUBLIC.md), fought as
  **expeditions**: a campaign (docs/MILITARY.md) whose target is a nation rather than a system. Fleets committed to an
  expedition leave the map for the campaign's turns, fight abstract battles against the nation's strength each turn
  (the same battle math, supply draining with distance), and come back with plunder, glory, valour, hulls lost — or
  not at all. The commanding general's sway grows with each victory.
- Nations **raid** the border: an aggressive nation whose disposition falls sends incursions into rim regions (the pirate
  machinery already does this), and a defeated nation pays tribute for a time.
- Conquest off-map yields **tribute and a province**: a counter the Reach shares out (the consuls decide), and a claim
  the conquering house may press into a title.

## On-map later (the expansion)
The galaxy gains a second region set beyond the rim with the nations' systems; expeditions become ordinary fleet
movement across a border; conquest takes holdings as it does now; border regions are fought over for their deposits
and chokepoints. Everything built off-map (nations, expeditions, tribute, sway) carries over unchanged.

## Built (2026-11-09) — off-map stage 1 and 2
- **Catalogue** `content/nations/nations.json` (`NationCatalog`): five nations — the Ardent Concord (the one strong
  neighbour: aggressive, 26 hulls, raids the rim), the Free Cities of Thessar (mercantile, rich, soft), the Vashti
  Khanates (aggressive raiders, weak, cheap to beat), the Outer Compact (defensive, far, fights hardest at home), the
  Hollow Crown (decaying: loses a hull every ten turns, rich vaults). Each has strength, regen, wealth (plunder a win),
  distance (turns out), disposition, tribute, threshold.
- **State** `World.Nations` (`Nation`: strength, disposition, war and its two scores, subdued-until, last raid). `Fleet`
  gains `Away`, `AwayPhase` (out / fighting / home), `AwayTurns`, `HomeSystemId`, `Plunder`, `ExpeditionWins`. An away
  fleet has no system: it is out of every battle, siege, supply line and map marker at home, and still costs upkeep.
- **`NationSystem.Tick`** (BeginTurn, after the fleets at home; RNG-free): strength regrows every five turns; regard drifts
  by temper (aggressive sour, mercantile warm) and falls two a turn at war; **raids** — an aggressive or decaying nation
  at regard −20 or worse hits a rim world about one turn in six (unrest +8, five credits from its holder, a point to its
  war tally; three hulls in orbit turn it back); **expeditions** sail the distance, fight a season of four battles, come
  home with plunder paid into the treasury; **the war ends** when the Reach's score clears the nation's by its threshold
  (or the nation's fleets are gone) — subdued twenty turns, tribute a turn to the chair, the house with most wins takes
  the triumph (+8 legitimacy, +4 Senate influence) — or the nation's clears the Reach's (the chair buys peace at thrice
  the tribute, −6 legitimacy), or twelve turns pass without a battle. One war abroad at a time.
- **The battle abroad** uses the real fleet math (`FleetSystem.PowerOf`: hulls, class, readiness, admiral, veterancy,
  stance, torpedoes, supply, loyalty, the front; plus the house's flag factor) against the strength the nation commits
  (the force sent and a quarter more, up to all it has; a power a hull; ×1.25 defensive, ×0.8 mercantile, ×0.85 decaying).
  Win: the nation loses 40% of what it committed, the Reach scores two, plunder = wealth, the fleet loses ~15% of its
  hulls scaled by the odds, veterancy and loyalty rise, a commander at the front gains valour (and may become the
  Achilles). Lose: 30% of hulls, the nation scores two, loyalty falls, the commander risks death (doubled at the front);
  at one hull the fleet limps home.
- **The Senate** moves `foreign_war` (target a nation; 3 influence): the Sword +2, the Factors −2, hulls to send +1, a
  home war −2, a hated or raiding nation +1, a mercantile one −1; a Commander's house +1. NPC hawks (Sword houses,
  military heads of rank ≥2, houses raided lately) move it when the Reach has six hulls to send and fights no other war
  abroad; passing it declares the war.
- **Commands** `SendExpeditionCommand` (at war, whole ≥2 hulls, supply ≥50, loyal, one treasury action) and
  `RecallExpeditionCommand`. NPC houses at peace at home with a hawkish reason send their largest idle fleet (≥3 hulls
  and ≥ a quarter of the nation's strength), one abroad at a time, leaving something at home.
- **Surface**: the Military screen's *Beyond the Reach* panel (every nation's strength bar, regard, temper, distance, what
  it holds; the war's tally; *Move for war* buttons; word from beyond), expedition state and *Send against* / *Recall*
  on each fleet card, a header stat; the Senate's war motion with a nation picker; eleven outliner alerts; ten events
  (`content/events/nations.json`) hung on the one-turn flags.
- **Flags**: `foreign_war`, `foreign_war:<id>`, `foreign_war_declared`, `expedition_sent` (the order; becomes `fleet_sailed_abroad` next turn), `expedition_away`,
  `expedition_victory`, `expedition_defeat`, `expedition_lost`, `expedition_returned`, `nation_subdued[:<id>]`,
  `triumph`, `war_lost_abroad`, `foreign_peace`, `nation_tributary`, `nation_freed`, `raided_by_nation`,
  `raided_by:<id>`. **Counters**: `foreign_wars`, `foreign_wars_lost`, `nations_subdued`, `expeditions`,
  `foreign_battles`, `foreign_raids`, `tribute_received`, `expedition_wins:<house>:<nation>`, `raided_turn:<house>`.
- **Measured** (probe, four scenarios, 60 turns): 1–2 wars abroad a run, 1–5 expeditions, 2–13 battles abroad, 12–17
  raids, one subdual (84 tribute to the chair) and one lost war across the four.

## Still off-map
The province and the claim a conquest yields (a title to press), the consuls' prerogative to make war without a vote,
and the general's sway from victories abroad belong with the Caesar (docs/REPUBLIC.md stages 4–5). The map expansion
puts the nations' systems beyond the rim and turns expeditions into ordinary movement.

## Order of work
Military depth stages 1–3 (tactics, command, loyalty, supply, campaigns) → the Republic's parties and consuls → foreign
nations off-map (expeditions as campaigns) → the Caesar → the map expansion.
