# The economy and the Exchange: a plan

Status: **all four stages are built** (2026-11-13). The plan below is kept as written; section 10 at the end says
what shipped, where it lives, which of the owner's decisions were taken as suggested, and what was left out. Owner's
direction (2026-11-11): the economic framework needs fleshing out (buying and selling businesses and resources,
starting new things, building things), and later a galactic stock market where you can found companies and put in
capital, one that reacts to what is actually happening in the world: war and resource scarcity.

This document says what exists, what is thin, what to build in what order, how prices would be computed, and which
decisions are the owner's. Each stage is playable on its own and each later stage reads numbers the earlier one made.

## 1. What exists today (docs/ECONOMY.md)

- **Resources**: credits, materials, manpower, influence, exotics; the strategic three (helium-3, rare earths, isotopes).
- **Income**: worlds by specialisation, buildings, prosperity and tithe; ventures by kind and level; charters; offices.
- **Ventures**: ten kinds, founded in a system for a cost, expanded to level three, sold back for six-tenths of book
  value. Owned whole by one house. No buyer on the other side of a sale.
- **Trade**: fixed conversion rates at a market building (`ExchangeCommand`); helium-3 and rare earths bought at a base
  price plus a step when fewer than four producers exist (`ProcureCommand`); the tariff law and an embargo move or close it.
- **Finance**: one standing loan of fixed size; belt claims; a one-off investment in a world's prosperity.
- **Building**: instant, paid in full, from a list per world.

## 2. What is thin

1. **Prices do not exist** except for two goods, and those barely move. War, blockade and raids change nothing a
   player can see in a number.
2. **You cannot sell** most things, and you cannot buy from a house or sell to one. The market is a wall, not a crowd.
3. **A venture is not an asset.** It has no market value, cannot change hands between houses, cannot be co-owned.
4. **Starting something is one click.** No construction time, no prospectus, no reason to prefer one site to another
   beyond the catalog's yes or no.
5. **Nothing tells you why.** The ledger lists lines; it does not say what a hull of fuel cost last year or what a
   forge would earn.

## 3. Stage A: prices for every good

The foundation. Everything after reads these prices.

**A commodity market for seven goods**: materials, manpower (as hired crews), exotics, helium-3, rare earths, and
isotopes on a licensed market only; influence is never sold. One Reach-wide price each, recomputed every turn,
deterministic, no dice.

For each good, each turn:

- `supply` = what every house produced of it this turn (already in the ledger), plus imports from nations at peace.
- `demand` = what every house consumed: upkeep, fuel burnt, hulls laid down, buildings begun, torpedoes fired.
- `stock` = a Reach-wide inventory that carries over: last stock plus supply minus demand, floored at zero.
- `cover` = stock divided by a year's demand.
- `price = base × clamp(1 + k × (1 − cover), 0.5, 3.0)`, then eased a third of the way from last year's price so it
  moves in trends, not jumps.

What moves it, and every one of these is already an engine fact:

| World event | Effect on the number |
|---|---|
| A war between houses | fleets burn more helium-3, yards draw rare earths and materials: demand up |
| A siege or blockade | that world's output leaves supply while it lasts |
| Raiders on a region's lanes | a share of that region's output is lost in transit |
| A world in revolt, or broken away | its output is gone |
| A Senate embargo | the house is out of the market, as now |
| The tariff law, the trade charter | a percentage on every price, as now |
| A war abroad | imports from that nation stop; tribute from a subdued one adds supply |
| A depot, a forge, a skimmer founded | supply up the year it completes |

**Buying and selling both ways.** Any good, any amount you can pay for or deliver, at a market building or the capital.
A spread of about a tenth between buy and sell. Orders have weight: an order larger than a tenth of the stock moves
the price against you for that order and nudges next year's price. This is what stops a rich house emptying the market
in one click, and it is what makes cornering a scarce good a real, visible, expensive act.

**Screen**: a Markets tab on the House screen. One row a good: price, a ten-year sparkline, cover, and one line of
cause ("up 30%: two wars, Tessaly under siege"). Buy and sell boxes with the price impact shown before you confirm.

**Measure before moving on**: across the soak, each price should spend most years within half and double its base,
spike in wars and sieges, and return within about eight years of peace. NPC stewards must not go bankrupt buying fuel.

## 4. Stage B: ventures as assets, and building as projects

**Valuation.** A venture's value is its yearly yield at this year's prices, less upkeep and charter, times a multiple
(say six years' earnings), never below its salvage value. A helium skimmer is worth more in a fuel shortage. This
replaces book value everywhere, including the wealth tiers.

**Sale between houses.** Offer a venture to a named house or to the open market; make an offer for another house's
venture. An NPC accepts by rule, not whim: price against its valuation, how short of credits it is, what it thinks of
you, whether the venture sits on its own world. A distressed house sells cheap; that is how fortunes change hands.
The world's holder may have a right of first refusal (owner's decision, below).

**Projects, not clicks.** Founding a venture, raising a building, staking a claim and investing in a world become one
list of projects. Each has a cost paid over its construction years, a build time of one to four years, and a
**prospectus** before you commit: expected yearly return at current prices, payback in years, and what it depends on
(a deposit, a lane count, a market for its output). A project can be paused, sped up for a premium, or sold half
built. Sites differ: the same forge pays more next to a mining concern and a shipping line.

**New things to start.** Kinds the market makes meaningful: a commodity warehouse (stores a good and sells into a
spike), an arsenal concern (turns isotopes and rare earths into torpedo stocks), a shipyard company (builds hulls for
any house for a fee), an insurance house (pays out on raided shipping, profits in quiet years).

**Screen**: a Ventures tab: what you own, what each is worth and earns, expand, sell, offers received. An
Opportunities list: what could be founded where, sorted by payback.

## 5. Stage C: companies and the Exchange

The stock market. It needs stages A and B, because a share price is a claim on a venture's earnings at market prices.

**A company** is one or more ventures incorporated under a name, with shares (say a hundred), a cash balance, and a
dividend policy. The founder starts with every share.

**What you can do**
- **Found a company**: incorporate ventures you own. Free in credits, costs influence.
- **Raise capital**: float new shares on the Exchange. The cash goes into the company, not your treasury, and can only
  be spent on its projects. You are diluted. This is the "input more capital" loop: sell a quarter of the company to
  build the yard that doubles its earnings.
- **Buy and sell shares** of any listed company, yours or another house's, with the same order-weight rule as goods.
- **Set the dividend** if you hold the majority: pay earnings out, or keep them in to grow.
- **Take control**: half the shares and one more makes the company yours to direct, whoever founded it. Its earnings
  still pay every holder by their share. A takeover is a legal, bloodless way to gut a rival.
- **Buy back**, to close the door again.

**The share price**, each turn, deterministic:

- `fundamental` = the company's earnings per share at this year's prices × a multiple.
- `multiple` = a base, adjusted by the sector's outlook and by confidence in the Reach as a whole.
- `sector outlook` is read from the same world facts as prices: war raises arms, yards and fuel and lowers shipping
  and trade; a region under raid or siege lowers every company working there; scarcity of a good raises its producers
  and lowers its consumers; a tariff Act helps protected producers and hurts traders.
- `confidence` is one number for the Reach: down with wars at home, a lost war abroad, the great crisis clock, a house
  falling, a default; up with peace, tribute coming in, a triumph.
- `flow`: net buying this year pushes the price up a little, net selling down; it decays.
- `price` eases toward `fundamental × confidence` plus `flow`. A small hash-based wobble keeps two similar companies
  from moving in lockstep.

So the market reacts to real things without scripted events: declare war and the yards rise that year; lose a fleet
and confidence falls; besiege a rival's forge world and its mining company's shares sink, which you may have sold
first.

**Other listed things**
- **Great companies of the Reach**, chartered by the Senate: the fuel exchange, the lanes company. Widely held, steady.
- **War bonds**: the Senate borrows for a war abroad; the yield rises as the war goes badly; repaid from tribute.
- **Foreign paper**, later: Thessar's banks, once nations are on the map.

**How rival houses behave**: a house with spare credits invests by temperament (the Factors most, the Sword least);
a house in distress sells, which is what a crash is; a house defends control of its own company; a hostile house may
build a stake in yours.

**Politics and the market touching**
- The Merchants estate and the Factors party care about confidence and about any law that taxes trades.
- New Senate business: charter or dissolve a great company, a transaction tax, a rescue of a failing company.
- The Treasurer can steady a falling market from the Reach's purse, once a term.
- A gift of shares is a bribe with a paper trail: it buys a house's regard and can be exposed.
- Under the Imperium, the throne grants monopolies; the Imperial Edict's proscriptions seize shares.

**Crashes and bubbles**: no special machinery. A crash is confidence falling while distressed houses sell into thin
flow. A bubble is flow outrunning fundamentals in a long peace. One event chain marks each for the player.

**Screen**: an Exchange screen in the dashboard style: an index line, a table of listed companies (sector, price,
ten-year line, earnings, dividend, who controls it), your holdings and their value, an order box with price impact,
and a short "what moved the market this year" list.

## 6. How it would sit in the engine

- **State**: `World.Market` (goods: price, stock, a short history), `World.Projects`, `World.Companies` (shares by
  holder, cash, ventures, dividend policy, price history), bonds. All serialised; all deterministic.
- **Turn order**: after the economy accrues and before events, `MarketSystem.Tick` reads this year's production and
  consumption and sets prices; `CompanySystem.Tick` values companies, pays dividends and reprices shares. Rival
  houses trade in their action phase through the same commands as the player.
- **Content**: `content/market/goods.json` (base price, sensitivity, who may trade it), `sectors.json` (what each
  venture kind counts as and what moves it), `companies.json` (the chartered companies).
- **Flags for events**: `price_spike:<good>`, `market_crash`, `takeover_of_mine`, `company_floated`, `dividend_cut`.

## 7. Risks, and what guards against them

| Risk | Guard |
|---|---|
| Too much to manage every year | Nothing here needs yearly attention. Prices and shares run themselves; the player acts when they want something. Alerts only for spikes, offers and takeovers. |
| Money runs away | Order weight, a spread, a cap on shares bought a year, and a tax law the Senate can raise. Measure wealth concentration in the probe. |
| Rival houses are fleeced | They trade by valuation rules and refuse bad prices; they never sell control except in distress. |
| It reads as a spreadsheet | Every move carries a one-line cause in world terms. No number moves without a named reason. |
| It unbalances the existing economy | Stage A ships with prices pinned near today's fixed rates in peace; only war and scarcity move them. |
| Insider dealing is too strong | It is the point, within limits: acting on your own war plans is legal; the paper trail is what the estates and rivals react to. |

## 8. Order of work and what proves each stage

1. **Stage A, prices** (engine, Markets tab, rival stewards buy and sell). Proof: the soak shows spikes in wars and
   sieges and recovery in peace; no steward ruined by fuel; a test per driver in the table above.
2. **Stage B, assets and projects** (valuation, sale between houses, projects with prospectus, four new kinds).
   Proof: ventures change hands in the soak; payback shown matches what the ledger later pays within a fifth.
3. **Stage C, companies and shares** (found, float, trade, dividend, control; the Exchange screen; rival investors).
   Proof: a declared war moves sector prices the same year; a takeover is reachable in a test; no house's share of
   total wealth passes a set bound in sixty years of soak.
4. **Stage D, the wider market** (chartered companies, war bonds, Senate business, the Treasurer, events).

Each stage is roughly the size of the military depth work. A and B are worth having even if C never ships.

## 9. Decisions for the owner

1. **How many goods?** I suggest seven, as above. Fewer is simpler; more needs production chains we do not have.
2. **One price for the Reach, or one per region?** I suggest one to start, with a regional surcharge under raid or
   siege. Per-region prices make trade routes a game of their own, which is a larger project.
3. **May isotopes be traded?** Today never. I suggest a licensed market the Senate can open by Act.
4. **Who may refuse a sale?** I suggest the holder of the world gets first refusal on a venture sold there.
5. **Is a hostile takeover allowed against a house's only company?** I suggest yes, with a founder's golden share
   that blocks it once, at a cost in standing.
6. **How visible are rival holdings?** I suggest stakes above a tenth are public; smaller ones are intel.
7. **Does the Academy teach it?** I suggest a ninth lesson once Stage A exists, and the timetable's Political Economy
   then gives a small edge in reading the market.
8. **Republic and Imperium**: I suggest the Republic has a free Exchange with a Senate that can tax and charter, and
   the Imperium has monopolies by grant and seizure by edict. Same engine, different law.

## 10. What was built (2026-11-13)

The owner asked for the expansion to be finished, so the eight decisions in section 9 were taken as suggested. Each can
be changed in content without touching the engine.

| Stage | What shipped | Where |
|---|---|---|
| A. Prices | Six goods with a price each year from supply, demand, war, siege, raids, imports and the size of orders. Buy and sell both ways. Isotopes need the Arms Market Act. Stewards trade. | `MarketSystem`, `content/market/goods.json`, Exchange → Goods |
| B. Ventures and projects | A venture is worth six years of its earnings. Houses bid, ask and refuse by rule; the world's holder has first refusal. Buildings and ventures take years and are paid by the year (pause, rush, abandon). Four new venture kinds. | `VentureSystem`, `ProjectSystem`, Exchange → Ventures, Projects |
| C. Companies and shares | Incorporate a venture as a company of a hundred shares. Float new shares for capital, buy and sell on the float, set the dividend, buy back, pay out cash, wind up. More than half directs. A founder refuses a takeover once. Stewards list, float, invest and sell by party. | `CompanySystem`, `content/market/exchange.json`, Exchange → Shares |
| D. The wider market | Three chartered great companies. War bonds for a war abroad. Senate motions to break up a company and to rescue the Exchange. The Treasurer steadies a falling market once a term. Under the Imperium the throne grants monopolies and, with proscriptions, seizes shares. Thirteen events. A ninth Academy lesson. | `CompanySystem`, `content/senate/motions.json`, `content/events/exchange.json` |

**The share price**, each year, for every company:

- `fundamental` = (earnings at this year's goods prices × 8 × the sector's outlook × confidence + cash) ÷ shares,
  never less than two-fifths of book value a share.
- `outlook` per sector, from `exchange.json`: per cent for each war, for lawless lanes, for scarce goods. Arms +12 a
  war; finance −10; trade −8; shipping −6; extraction +5 and +25 for scarcity.
- `confidence` eases toward a target: −5 for each war between houses, −6 for a war abroad, −5 for each stage of the
  great crisis, down with the share of houses in debt, +2 for each year of peace up to 12, +10 for two years after the
  Reach's purse is opened. It is held between 60 and 125.
- `flow`: this year's net buying or selling as a share of the company, at 60 per cent; it halves each year.
- The price moves seven-tenths of the way to `fundamental × (1 + flow)` with a hash wobble of three per cent.
- The index is chained from the total value of every listed share. A fall of a tenth in a year raises `market_crash`.

**Rules a player will meet**

- An order costs more a share the larger it is, and no house may buy more than a fifth of a company in a year.
- Shares are bought from the float. A founder who holds everything is not for sale until they float or sell.
- Dividends are declared from the year's earnings and paid the next year through the ledger, to whoever holds then.
- A chartered company lets no house hold more than a quarter, unless the throne has granted its monopoly.
- The Exchange Levy Act taxes every share trade and pays the chair.
- A company whose cash falls below −30 is wound up: its ventures are broken up and its holders paid what is left.
- A bond costs 20 credits, pays 6 per cent a year (more when the war goes badly), and is repaid when the war ends:
  110 per cent if the nation was subdued, 85 if not. The chair pays the coupons and the principal. If the chair cannot
  pay, the bonds default and confidence falls.

**Measured** (eight scenarios, sixty years, no player): twelve house companies listed in every run, 400 to 750 shares
traded, the index between 56 and 230, nought to two crashes and one to four booms, and the richest house holding 8 to
13 per cent of all wealth.

**Left out, on purpose**

- Foreign paper (a nation's banks listed on the Exchange). It needs more of the nations than is modelled.
- A gift of shares as a bribe with a paper trail.
- Per-region prices and trade routes. One price for the Reach stands.
- Buying shares directly from a named house. Everything goes through the float.
- Small stakes as intelligence. Every holding of a tenth or more is public; smaller ones are simply not shown.

**Known thin spots**

- Rival houses almost never complete a takeover of each other; a player can. They defend their own majority well.
- Goods the Reach overproduces (materials, rare earths, exotics) sit below their usual price most years.
- A player's company keeps its cash until told otherwise; a steward's pays it out when it has nothing to build.
