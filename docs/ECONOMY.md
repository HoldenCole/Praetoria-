# The economy — depth pass (GDD §17)

What a house is worth, where its coin comes from, and what it can do about it. Everything here is RNG-free and
id-ordered: a turn's ledger is exactly reproducible from a seed. The engine lives in `Economy.cs`, `Wealth.cs`,
`EconomyCommands.cs`; the data in `content/holdings` and `content/businesses`.

## The ledger

Every turn each house's **ledger** is computed line by line (`Economy.Compute`) and the net applied to its treasury
(`Economy.Accrue`). The resource strip's forecast and the House screen's ledger read the same lines, so what is shown
is what will happen.

| Line kind | Source |
|---|---|
| `world` / `station` / `claim` | a holding: specialization yield, buildings, population → manpower, the tithe, contiguity, the belt; scaled by prosperity; halved under blockade; manpower throttled by unrest; less a credit if the governor hates the head |
| `venture` | a business: yield × level + flat yield − upkeep × level; scaled by the world's prosperity; halved when the world's holder is at war with you or besieged; less the charter owed |
| `charter` | credits paid by another house's venture on a world you hold |
| `tolls` | two credits for each chokepoint whose world you hold (`docs/GALAXY.md`) |
| `escort` | a fortress world (specialization defense ≥ 0.4) earns a credit per neighbouring system another house holds and is at peace with, up to three: ships pass under its guns and pay |
| `court` | two credits and an influence for holding the capital |

A governor's hand is in the `world` line too: a hostile governor (disposition ≤ −20 toward the head) skims a credit, an
able one (administration ≥ 3) adds one, and a governor who hates the head (≤ −40) drags the prosperity target down ten.
| `loan` | this turn's instalment |
| `interest` | a credit per house paying down a loan your bank holds |
| `fleet` | fleet upkeep (forecast only; the fleet system charges it) |

## The rings

A holding's ring (`docs/GALAXY.md`) changes its ledger: the core adds a credit per peopled world and doubles the charters
its holder collects; prosperity targets carry the ring's offset (core +10, rim −10); unrest drifts with the ring; a
region's lawlessness taxes credits and materials from holdings and ventures alike (half at 100); a world in revolt
yields nothing but its upkeep; the capital's holder keeps the court (two credits, an influence).

## Holdings: prosperity

Every holding has **prosperity** (0..100, par 50). It scales credit and material output: a world at 100 yields half
again, a world at 0 yields half. Each turn it moves up to five points toward a target:

```
50 + 4 × governor's administration + 3 × buildings − unrest/2 − 4 × tithe credits bonus
   − 30 if blockaded + 2 × levels of ventures working the world
```

So the lenient tithe (−1 credit) lifts prosperity by 4, the harsh tithe (+2) drags it down by 8; a skilled governor is
worth more than a building; a thriving world (≥ 75) also grows a point of population faster. **Investing** (6 credits)
lifts a holding by 15 at once — the steward's lever when a world languishes.

## Asteroid belts and claims

Systems may carry a **belt** (`"belt": n` in the galaxy file, 0..3). The richness is also the number of **claims** the
belt supports. A claim is the smallest holding there is (`kind: claim`, specialization `belt_claim`): 6 credits and 4
materials to stake, 2 materials a turn plus the belt's richness, an exotic a turn from a rich belt, a credit of upkeep.
A house may stake where it holds something or next door to it — or anywhere at all if it holds nothing, which is the
landless house's first rung back. Eight systems of the Reach have belts.

## Ventures (businesses)

A **venture** is property that is not land. It is founded in a system for a cost, pays its owner by its kind and
level, can be expanded, can be sold for six-tenths of its book value, and pays a **charter** per level each turn to
whoever holds the planet there when that is another house — so holding a busy world pays, and a landless house's
company always has a landlord. Where a kind may be founded is data (`requires`): a peopled world, a belt, a hub of
three lanes, a world of your own, a title rank, a planet class, a specialization.

| Kind | Needs | Pays |
|---|---|---|
| Trading Company | a peopled world | credits |
| Mining Concern | a belt | materials, a little coin |
| Shipping Line | a hub of three lanes | credits, influence |
| Merchant Bank | your own world, a count's rank | credits, influence; holds other houses' loans |
| Salvage Guild | a barren, frozen or belt world | materials, an exotic |
| Spice Syndicate | a research world | credits, an exotic |
| Free Company | a peopled world | credits; costs people |
| Academy Patronage | your own peopled world | influence |

The two landless houses of the Reach each start with one. NPC stewards found one where the catalog lets them when they
have coin to spare, expand the one they have, stake claims near home, and invest in a slack world.

## The action pools

The per-turn action pools (GDD §9) are a constraint now: the player's cap is six, regenerating two influence, two
treasury actions and one agent a turn (was nine and 3/3/2). A proclamation (three influence, two agents) or a war
(two influence) is saved for, not spent on a whim.

## Loans

The standing loan: borrow 20, owe 26, pay 4 a turn whether the ledger can bear it or not. The lender is the house with
the strongest bank, if any (it takes a credit of interest a turn); otherwise the caravaneers' counting-houses. A house
in debt cannot borrow again; it can clear the debt in one payment. NPC stewards borrow at −5 credits and repay when
flush. Content reads `in_debt`.

## House wealth

One number for what a house is worth, reckoned and ranked every turn (`Wealth.Of`, `House.Wealth`):

```
credits + ⅔ materials + 4 × influence + 6 × exotics
+ 30 + prosperity/5 per world (+ what is built on it) + 12 per station + 8 per claim
+ each venture at book value − debt
```

Tiers: destitute (< 0), poor (< 40), comfortable (< 100), rich (< 200), opulent. The player's rank is the counter
`wealth_rank`, the richest house the flag `richest:<house>`; the Steward reports your standing, the House screen shows
the table.

## Engine flags and counters for content

`business:<kind>`, `business:any`, `claim_holder`, `in_debt`, `richest:<house>`; counters `wealth`, `wealth_rank`,
`claims`, `ventures_founded`, `loans`, `investments`. Five venture events ship in `content/events/ventures.json`.

## Houses that fall

A house that has lost everything is not left as a debt frozen in the ledger (`HouseSystem`, every end of turn, NPC
houses only — the player's ending is the saga's):

- **Struck from the rolls** — no living member, no world, no venture, no hull: the name ends, its wars with it.
- **The free hold folds** — a free-hold house whose last world was taken has no reason to exist; its people and its
  guard pass to whoever holds the world now, as the buy-back already did.
- **Enters the service** — a landed name made landless, penniless and hull-less waits twelve turns
  (`landless_since:<house>`), then enters the service of the richest landed house of its culture (or the court): its
  characters become that house's retainers, its debts are written off, its name is struck.

The counter `houses_fallen` counts them. Before this rule a third of the Reach's "insolvent" houses at turn sixty were
such names, frozen at the credit floor; after it, four to seven of some thirty-three houses are in the red, and they are
houses with a world in revolt or a loan to work off — a game still to play.

## The fief, and who grants it

A landless house with legitimacy twenty or more petitions for a fief (three influence). The grantor is the court if it
holds a world beyond the capital, else the greatest landed house at peace with the petitioner (two worlds at least,
never a free hold); the grantor's head must think well of the petitioner (disposition ten), so a landless rival courts
them first. The grantor's least peopled world changes hands, the petitioner rises a baron, the grantor gains five
legitimacy for the generosity. Fortress worlds, meanwhile, earn a garrison credit and a levy of manpower on top of
their escort fees, so a march lord is marginal rather than broke.
