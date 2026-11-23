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
- A company whose cash falls below −3,000 is wound up: its ventures are broken up and its holders paid what is left.
- A bond costs 2,000 credits, pays 6 per cent a year (more when the war goes badly), and is repaid when the war ends:
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

## 11. A venture can fail (2026-11-15)

Closed off the register (docs/GAPS.md A6). A venture was an asset that could be sold, enlarged or taken, and could
never go wrong. Now every venture a house owns outright is reckoned at the start of each year.

- **A year without a profit** is one in which it earned nothing or lost money at that year's prices. Its first year
  is not judged. A year of profit wipes the count out.
- **Two such years running**, and the owner is warned (an alert, and the venture's card says so).
- **Three**, and it is wound up. The house gets half its salvage, which is a quarter of what it cost; the rest goes to
  those it owed. The venture is gone.
- **Carrying it.** The owner can put money in: twice its yearly loss and 200 credits for each level of it, 400 at the
  least. The losing years are wiped out and it has three more to come right. Or it can be sold, as before.
- **The founder.** If one of the owner's own house founded it (docs/AMBITIONS.md), that is a failure of their own: it
  is written into their life, it tells on them, and it counts against what they want. Three failures and a person
  gives the thing up.

A venture stops paying for the reasons its earnings already answer to: a world brought low, lawless lanes, an
embargo, a war with the world's holder, prices that have turned. Nothing new was invented to make it fail. (A
company's venture was not judged then. It is now: see section 13.)

Measured before the rule was fixed (ten runs of one start): 3% of venture-years went without a profit, and the
unprofitable ones stayed so, which is what makes three years the right length: about twenty ventures a run are wound
up over sixty years, of some hundred and ten standing at the end.

## 12. Shares in a person's own name (2026-11-15)

A holder of a company is a house, the public float, or now a person (docs/WEALTH.md, "A purse that holds more than
credits"; docs/GAPS.md W1). A person buys out of their own purse at the price a house pays, may hold a tenth of a
company, is paid their dividend into their purse, and is paid out like a house when a company is wound up. A
person's shares are out of the float but carry no vote: who directs a company is still reckoned among houses only,
and a company no house holds is still wound up, with its private holders paid. (Since W10 a person's shares vote
with their house's own, while they think no ill of its head: docs/WEALTH.md, "A venture of one's own".)

## 13. A failing venture, beyond its owner and beyond one cause (2026-11-15)

Closed off the register (docs/GAPS.md A7). Section 11 gave a venture one way to fail and only the player a way to
stop it.

- **Other houses act.** A year from being wound up, a house whose head knows economics (2 or better), or which sits
  with the Factors, or one house in three otherwise, does something. If it has the cost of carrying the venture and
  6,000 credits beside, it carries it. If not, it sells: to the best bidder among the other houses, or on the open
  market, which pays more than a winding up would. The rest let it go. The player is never sold to unasked.
- **A company's venture is judged** like any other. After three years without a profit the company carries it out of
  its own cash if it has the cost and 2,000 beside; otherwise it closes it, takes half the salvage into its cash, and
  its shares are sold off a little (a twentieth of them, as selling pressure).
- **A failure costs more than money.** Those the house owes are paid out of what comes back before the house is. The
  merchants think 3 worse of the house and the commons 1. And for five years the house has **a name for bad
  business**: other houses bid a tenth less for any venture it sells. The Exchange screen says so.
- **A second cause: sacked.** When a world falls to a siege, about half the ventures there that belong to houses at
  war with whoever took it are sacked. Nothing comes back to the owner; the taker carries off 400 credits a level. A
  founder of the owner's house has failed at it, as if it had been wound up. An event for the head.
- **A second start.** For six years after a venture they founded fails or is sacked, the head can stake its founder
  to another: the house founds the cheapest venture it can, in the founder's name. They think 10 better of the head
  and one of their setbacks is taken off. The button is on the founder's line in the ambitions panel.

Measured over sixty runs of sixty years: 697 failing ventures were carried by their houses and 575 sold before they
failed; 469 were still wound up (1,074 before this); companies carried 5 and closed 28; 189 were sacked. About as
many ventures stood at the end as before (7,064 against 7,082).

Not seen in play: nothing was paid to creditors out of a failure (houses in debt seldom own a failing venture), and
no second start was given, in the probe or the soak. Both are proved by tests.

Found while building it, and mapped (A8): a sacked venture is never claimed at the peace or rebuilt; a company only
carries or closes a failing venture and never sells it; other houses give no founder a second start.

## 14. Bills at the peace, a company that sells, creditors who foreclose (2026-11-15)

Closed off the register (docs/GAPS.md A8).

- **A bill for what was sacked.** Every venture sacked is written down against whoever took the world: 1,000 credits a
  level. When the two make peace the bill is paid, as far as the payer's treasury goes. A house beaten badly enough
  to cede a world presents no bill: its claim is struck out. A bill with no war behind it (the war ended some other
  way) lapses at the year's reckoning.
- **A company sells.** A company that cannot carry a failing venture now sells it rather than close it, if the house
  that directs it minds its books (the same test as for a house): to the holder of the world it works, at the
  market's price, or on the open market. The money is the company's. A board that does not mind still closes it.
- **Foreclosure.** A house is foreclosed on when it owes on a loan and its treasury is empty, or its treasury is 2,000
  or more under, or it has been overdrawn two years running. Its lender takes the least valuable of its ventures
  against the loan and pays over anything above it; with no lender able to, the venture is sold. The proceeds pay
  the loan, then whatever else the house owes, then the house. It has a name for bad business for five years. **A
  venture that is someone's own is not the house's to lose**, and is never taken. You are warned in the first year
  you are overdrawn. An event when it happens to you.

Measured over sixty runs of sixty years: 305,900 credits were paid at a peace for what had been sacked and 60 struck
out; companies sold 10 failing ventures and closed 11; 26 ventures were foreclosed on. A house was overdrawn in
4,596 house-years, about one in twenty-five, but by 2,000 or more in only 42 of them, which is why the first form of the
rule (a loan unpaid, or 2,000 under) never once applied and two years overdrawn was added.

Found while building it, and mapped (A9): foreclosure takes a venture and nothing else, and the house has no say in
which; a bill at the peace is for ventures only.

## 15. A pledge, foreclosure beyond a venture, and a bill for a blockade (2026-11-16)

Closed off the register (docs/GAPS.md A9).

- **A pledge.** The head can name what the house's creditors are to take first if it is foreclosed on: any venture
  the house owns outright, or any thing the head holds that is not entailed. They take that and leave the rest,
  though it be worth more. The button is on the venture's card and on the thing in the purses panel. A pledge can be
  withdrawn, and is spent when it is taken. Without one the creditors take the least of the ventures, as before.
- **A thing handed down.** A house with no venture left to give loses a thing instead: the one pledged, else the
  least valuable the head holds. This waits a year longer than a venture does (three years overdrawn, or a loan
  unpaid, or 2,000 under). **An entailed thing is never taken**: that is what an entail is for. Against a loan from a
  named house the thing goes to that house's head as their own; otherwise it is sold.
- **A world, last of all.** Only against a loan from a named house, only when there is neither a venture nor a thing
  to take, never a house's last world and never the capital. The least peopled world passes to the lender and the
  whole loan is written off.
- **A bill for a blockade.** A blockade that is lifted without the world falling, or still stands when peace is
  made, is written down against whoever laid it: 300 credits for each year it stood. It is paid at the peace with the
  bill for what was sacked, by the same rule. A fleet lost is not billed: the tribute the beaten side pays at a
  peace already answers to how the fighting went.

Measured over sixty runs of sixty years: creditors took 27 ventures and 46 things, and no world; 78,000 credits were
billed for blockades that failed, and 419,200 paid at a peace in all (305,900 before blockades were billed). In the soak
a world was foreclosed on once, and its player made four pledges.

Found while building it, and mapped (A10): only the head's own things are at risk, never a relative's; other houses
never pledge; and a world taken for a debt is never redeemed.

## 16. A loan of one's choosing, and an offer below the asking (2026-11-21)

From the playthroughs (docs/PLAYTHROUGH.md, Medium, Money).

**Lenders.** `Lending.Lenders` lists who would lend a house and on what terms: the caravaneers' counting-houses, and every
house that keeps a bank. The rules are constants in `src/Core/Systems/Lending.cs`.

| | limit | rate |
|---|---|---|
| the counting-houses | 2,000, and 1,000 for each world held (four counted) | 30 in the hundred |
| a house's bank | 2,000 for each level of the bank, and the same 1,000 a world | 30; 10 off at +20 regard, 10 more at -10, 10 off for its client |

A house known for bad business pays 10 more everywhere. A bank whose head thinks -30 or worse of the borrower, or is at war
with it, lends nothing. One lender at a time; the lender already owed lends more, to its limit. What is owed is the sum and
its interest together from the day, paid by the year in the proportion the standing loan always was (400 a year on 2,600), and
kept on the house as `LoanInstalment`. `RepayLoanCommand` takes an amount: a part paid shortens the years and saves no
interest. The standing loan the stewards of other houses take is unchanged, and is the counting-houses' terms exactly.

**Offers.** `VentureSystem.Offer`: the asking price is where an owner begins. `Least` is what it would take from this buyer
this year: 85 in the hundred of the asking; 80 at +30 regard; 95 at -10; 75 when the seller is short of coin; four points
either way by the year; never under what the open market pays. The buyer is not shown it. One offer a year on each venture.
At or above it the venture changes hands at the offer (the world's holder may still match it). Below it the owner answers
with the midpoint of the offer and the asking, or its least if that is higher, and `Ask` returns that answer to that buyer
until the year turns. Under half the asking the owner does not answer and its head thinks 3 the worse of the buyer.

Not built: a lender paying a loan out of its own treasury; houses other than the player offering below an asking.

## 17. Cornering a good (2026-11-23)

The owner's list (docs/PLAYTHROUGH.md, Hard): *"Cornering a good: who holds what, how much there is, and a price that
answers to a majority held; or a plain word that it cannot be done (F46)."* F46 (docs/playthroughs/family.md): the player
bought 5 of the 6 exotics the merchants held; the next year the stock was back to 6 and the price had fallen, and no screen
said how much there was, who held it or what a corner would need. Since then (docs/ECONOMY_REVIEW.md, section 10): a dear
good that is short should pay.

**What was built.** Both halves: the price answers, and where a corner cannot be bought the screen says so in numbers.

- **All there is.** For each good: the merchants' stock and every house's store of it, added together
  (`CornerSystem.Standing`). The Exchange shows it under what you hold ("of 31"), and a fold under the table, *Who holds
  what, and what a corner would take*, lists for every good the total, the merchants' part, the three largest holders with
  their units and share, and your own house if it holds any.
- **A third is felt, half is a corner.** A house that holds a third of all there is, and no fewer than 6 units, is felt in
  the price. One that holds half has cornered the good. The premium multiplies the price the market aims at (after war,
  shortage and the merchants' stock, inside the same floor and cap) and lasts exactly as long as the share does: it is
  worked out afresh at every yearly reckoning from what is then in store.
- **The merchants bring less.** Their stock drifts back toward what they like to hold by a fifth of the gap a year. While
  a house holds a third it is half of that, and while a corner stands a quarter: what they bring is bought as it lands or
  priced out of reach. A glut still leaves at the usual rate.
- **Selling it.** Nothing new was needed. An order to sell is weighed against the merchants' stock as the year opened
  (never less than 4), and under a corner that stock is next to nothing: one order of the whole hoard fetches between half
  and seven-tenths of the quoted price a unit, the less the larger it is. What is sold fills the merchants' shelves and
  shrinks the share, so the next reckoning takes the premium off and the price comes down.
- **The plain word** (`CornerSystem.Word`), on the row of any good in which a house holds a third, in the tooltip of every
  row's "of N", and in the fold for every good: who holds it; and for your house, "You hold 9 of the 31 in the Reach: 7 more
  is half, and the merchants hold 12", or "Half is 164 and the merchants hold only 62: with all the merchants have you would
  hold 70 of 327, or 21%. It cannot be cornered by buying. The other houses keep 257 in their own stores, and the Reach
  brings up 55 more a year." Isotopes are not sold on any open market while the Arms Market law keeps them off the
  exchanges: the word says a corner in them cannot be bought and has to be brought up from the house's own worlds.
- **It is heard of.** The year a house comes to hold half: a line in the chronicle, the house is told (and the player is
  told when it is another house), and the cause line of the price names the holder for as long as the holding adds a
  hundredth or more. The merchants' estate thinks 3 the worse of the house that year and 1 more every year the corner
  stands. When the share falls under half the chronicle says the corner is broken. The player is also told when their own
  house first passes a third.
- **On the screen besides.** The Exchange's headline names a corner when one stands. On the Houses screen a house that
  holds one is marked "holds a corner in ...".
- **Every house.** The rule reads stores, not who the player is. The stewards of other houses do not set out to corner
  anything; they already sell a surplus when the price is fair, which is what breaks a corner.

`OrderSystem`'s older mark, a house that *makes* a quarter of the Reach's yearly production of helium-3, rare earths or
isotopes, is a different thing and is unchanged. Its comment said "a third" and now says a quarter; the House and Houses
screens call it "makes a quarter of the Reach's ..." and keep "corner" for the market.

### The numbers

| | |
|---|---|
| All there is | the merchants' stock (whole units) and the store of every house of the Reach; raiders and nations are not counted |
| Felt | a third of it or more, and at least 6 units |
| Cornered | half of it or more, and at least 6 units |
| The merchants' refill | 20% of the gap a year as before; 10% while a third is held; 5% while a corner stands |
| The merchants' regard | 3 the worse the year a corner is made, 1 more each year it stands |
| Holders listed | the 3 largest, and your own house |
| Named in the cause line | when the holding adds 1% or more |

The premium, on the price the market aims at (the price itself moves half of the way there each year, as always):

| Share of all there is | under 33% | 36% | 40% | 45% | 50% | 60% | 75% | all of it |
|---|---|---|---|---|---|---|---|---|
| Added to the price | nothing | 1% | 8% | 25% | 50% | 60% | 75% | 100% |

From a third to a half it is the square of the way there, times 50%; from a half, 1% for each point of share.

### What it comes to in a game

Measured with `the_fallen_house`, seed 1, the other houses played by their stewards, the player given all the credits it
could want:

| What the player did | What happened |
|---|---|
| Year 4: bought all 7 exotics the merchants held (7,700 credits) | Held 7 of 23, under a third. The word: "Half is 12 and the merchants hold none: it cannot be cornered by buying." Sold a year or three later for 3,100 to 3,400 |
| Years 4 to 7: bought every exotic the merchants had each year (21,200 credits) | Held 17 of 44, then 17 of 52 as the Reach brought up more: never a third. Sold over three years for 5,900 |
| The same with rare earths, helium-3 and materials | Never a third of any. A loss of between 5,300 and 20,600 credits |

So F46 has its answer: a house that makes none of a good cannot corner it by buying the merchants out, after the first
year or two of a game, and the screen now says so with the figures. The houses keep far more in their own stores than the
merchants ever hold, and the Reach brings up more every year. A corner is within reach of a house that **makes** the good
and keeps it (a maker of exotics that sells nothing holds over two-fifths of them in twelve years), or that buys the
merchants out on top of a large store. Bought and sold again with nothing else gained, a corner loses money, as corners
mostly did. It pays the house that sells its own production at the higher price, or owns the ventures and companies whose
earnings follow that price.

**The other houses, measured.** Sixteen games of forty years played by the stewards alone (eight starts, two seeds each):
no house came to hold half of anything, and no game opens with a house holding a third. A maker's store passed a third in
thirteen of the sixteen, of exotics or isotopes and once of materials. In most it added under 5% to the price; the most
was 48% of the isotopes in one game, which added 41%.

**One start does open with corners**: a game begun with the boost *Full stores*, which gives the player's house 20 each
of exotics, rare earths and isotopes when the whole Reach has 8 to 14 of each. The house holds two-thirds of all three,
is told so in the first year, and their prices rise. The rule was left to say what is true of that store.

### What I chose

- A third for "felt" and a half for "cornered" (the task named both).
- 6 units as the least that counts: in the first year there are 8 to 14 of a thin good in the whole Reach, and a handful
  is not a corner.
- The premium: nothing at a third, 50% at half, 100% at the whole; the square between a third and a half, so that a
  maker whose store has crept to 36% adds 1% and not 9%.
- The merchants' refill at a half and a quarter of the usual.
- 3 and then 1 a year off the merchants' regard, and the merchants as the estate that minds.
- 3 holders listed. The holder named in the cause line from 1%.
- Telling the player when their own house passes a third, and when any house makes or loses a corner. Nothing is said
  when another house merely passes a third: the row and the cause line show it.
- The row shows "of 31" and the tooltip and the fold say "in the Reach": the table has no room for more.
- One existing test was changed (`tests/MarketTests.cs`, twelve years of peace): it asserted that nothing moved the price
  of exotics, and in that test no steward ever sells, so the largest maker comes to hold 44% of them. The test now allows
  for the premium of a holder it can see, and checks that the cause line names it.

### What is not built

- **No new command.** A corner is made with the orders that exist (`BuyGoodCommand`, `SellGoodCommand`) or by not selling.
- Buying out of another house's store. Without it a corner in a good the house does not make is out of reach in most
  years, which the screen says.
- Stewards of other houses that set out to corner a good, or that buy to break one.
- A motion against forestalling in the Senate, or an event for a market corner (`the_corner` is still the maker's one).
- The premium does not apply the moment the share is bought: like every price, it moves at the year's reckoning. The
  screen's word changes at once.
