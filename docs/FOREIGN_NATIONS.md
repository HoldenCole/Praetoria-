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

## Order of work
Military depth stages 1–3 (tactics, command, loyalty, supply, campaigns) → the Republic's parties and consuls → foreign
nations off-map (expeditions as campaigns) → the Caesar → the map expansion.
