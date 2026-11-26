# The Senate as a place of action

GDD §6.1 names the Senate as the political pillar's chamber; until this pass it showed standings and offered nothing to
do (GAP §10 item 2). Now houses move **motions**, every house **votes by weight**, and the player **whips**.

## Motions

`content/senate/motions.json` holds the kinds. A house of standing — a baron or better, or a head who is a Judge or
better on the law track — moves one for its influence cost (`ProposeMotionCommand`). One open motion a house, two on the
docket. A motion moved in turn T is voted at the end of turn T+1, so there is always one session to whip.

| motion | target | carries |
|---|---|---|
| Censure | a house | target legitimacy −6 |
| Recognition | a house (yourself allowed) | target legitimacy +6 |
| The Levy | — | every landed house pays 300 credits to the chair; the chair gains 2 legitimacy |
| Relief | a region | lawlessness −15, allegiance +10, prosperity +5 on its worlds; the proposer pays 800 credits (half effect if it cannot) |
| The Peace of the Reach | a house at war | white peace in all its wars |
| Commission | a house and an office | the house's best-qualified member takes the office; the previous holder is relieved; target legitimacy +2 |
| Embargo | a house | the exchanges close to it for eight turns: no open market, venture credits halved |
| War Beyond the Reach | a nation | the Reach is at war with a nation off the map (docs/FOREIGN_NATIONS.md); houses may send expeditions; one war abroad at a time |
| Act of the Senate | a law of the Senate and an option | the law is so set for the whole Reach (see *Acts* below) |
| Petition of an Estate | brought by an estate, never a house | an Act the estate wants; heard, its regard rises everywhere; refused, it falls and its worlds stir (see *Petitions*) |

## Acts: the law of the Reach by motion

Three laws in `content/politics/laws.json` carry scope `senate` and `reach: true` — the Common Law (local courts, the
custom, the Reach's assizes), the Trade Charter (protected markets, the custom, open lanes) and the Levy of the Reach (no
common levy, the custom, the Reach's levy). No house holds that scope; the only way to set one is an **Act** — motion kind
`act` (`law: true`), three influence, naming the law and the option. When it carries, `PolicySystem.Enact` writes it under
the pseudo-house `senate`, every house's `ReachModifier` and estates read it, and the Senate's cooldown (ten turns) keeps
the chamber from hearing that law again at once. Refusals: not a Senate law, the option already in force, the Senate
settled it lately, the law already on the docket.

Rivals lean on an Act by their **estates** (`SenateSystem.ActLean`): the option's opinions against the option in force,
doubled where it pleases an estate hostile to the house or galls one loyal to it, a tenth of the sum clamped to ±3; then
by situation — a house at war wants the common levy, a house with lawless worlds wants the assizes, a house with a navy
wants cheaper fuel, a house of three worlds likes a market it can tax, a house with a restless world fears anything that
adds unrest. Rival proposers move the Act that most pleases an estate hostile to them (`WantedAct`), after relief and
before censure. Flags: `act_open`, `act_passed`, `act_passed:<law>`, `act_failed`; counter `acts_passed`. The Chancery's
"law of the Reach" drawer shows every reach-wide law, who set it and how long before the chamber will hear it again. In
sixty-turn simulations two or three Acts carry a run.

## Petitions: the estates at the Senate's door

An estate hostile (regard −30 or worse) in three houses or an eighth of them, whichever is more, brings its own Act
(`SenateSystem.Petition`, run each turn before the rivals propose): the Senate law and option that would most please it
against what is in force (`WantedBy`), as motion kind `petition` with proposer `estate:<id>`, at no cost, one petition on
the docket at a time, and not again from that estate for twelve turns. It is voted like any Act; every house at whose
gate the estate stands leans a point further for it. **Heard**, the law moves and the estate's regard rises ten in every
house (the player's through `estate_offset:<id>`). **Refused**, its regard falls six everywhere and every world of every
house it already distrusts gains three unrest. No standing accrues to an estate. Flags `petition_open`,
`petition_open:<estate>`, `petition_carried`, `petition_refused` and the per-estate forms; counters `petitions_heard`,
`petitions_refused`. Three events: *The Petitioners*, *The Petition Heard*, *The Petition Refused*. One to three petitions
a sixty-turn run, mostly the Commons' and the old blood's.

## Weight and the benches

A house's vote weighs its Senate-sphere influence (the chair's two are in it), plus its title rank, plus one if its head
is a Justice or better. Ties fail. Rival houses lean by interest (`SenateSystem.Lean`): a target votes against its own
censure and its friends with it, a house at war with the target votes for; the poor vote down a levy and the chair
votes for it; a region's lords vote for its relief, and the rim is sympathetic; a losing belligerent begs for peace and
the bystanders like it; disposition toward the proposer tilts everything a point; a deterministic jitter breaks the
ties. The Senate screen shows every bench's lean before the vote.

**Whipping** (`LobbyCommand`): one influence leans a house that does not dislike you by two toward your stance; five hundred
credits bribe any house by three. Once per house per motion. **Voting** (`VoteCommand`) is free.

## Rivals bring their own business

About one turn in three the most influential house with a reason moves: a losing war (peace), an office it has the
rank for that is vacant or held by a house it hates or fights (commission), a title above its legitimacy (recognition), an empty chair's coffers (the levy), a lawless home region (relief), a hated rival (censure, or
embargo if at war with it). The chamber tires of repetition: the same house may not move the same motion against the same
target for ten turns.

## Standing

A carried motion lifts the proposer's standing in the Senate sphere by one (counter `senate_standing:<house>`, read by
`SphereSystem`); a failed one costs one. Standing feeds threat and coalition pressure like any sphere influence.

## For content

Flags: `motion_open`, `motion_against_me` (a censure or embargo of yours is on the docket), `censured`, `recognised`,
`levied`, `embargoed`, `motion_passed_mine`, `motion_failed_mine` (the last five for one turn). Counters:
`motions_proposed`, `motions_passed`, `motions_passed:<house>`, `embargo:<house>` (the turn it lifts), `lobbied`. Six
events ship in `content/events/senate.json`: *The Whip*, *A Motion Against the House*, *The Censure Carries*,
*Recognised*, *The Levy Falls Due*, *The Motion Carried*.

## The offices of the Reach

GDD §8 puts the drama in the gap between office-power and house-power. `content/senate/offices.json` names three offices,
each granted by a **Commission** motion to a house whose member holds the rank on the office's career track:

| office | track · rank | confers |
|---|---|---|
| Admiral of the Reach | military · Lieutenant | command of the **office fleets** (`Fleet.Office`): they sail under the holder's colours and fight its wars, the chair pays their upkeep and the state's bunkers fuel them; +3 Navy influence |
| Treasurer of the Reach | stewardship · Governor | three hundred credits a turn from the chair's coffers; +3 Treasury influence |
| Speaker of the Senate | law · Judge | three more votes of weight, motions cost one influence less; +2 Senate influence |

An office is the state's, not the house's. It is lost when the Senate commissions another (the holder's friends vote
against, a house with its own candidate is jealous, the chair likes a safe pair of hands), when the holder dies without
a kinsman of the rank, when the house falls — and the Admiralty is lost for **treason** the turn the holder's house is at
war with the chair (legitimacy −6, the fleets return to the chair). The Emperor holds every office by the throne. Rivals
move commissions for themselves when an office is vacant or held by a house they hate or fight. The Reach Squadron (three
frigates and a cruiser at the capital) is the office fleet of the fractured stars; the Imperial Home Fleet of the Imperium.

Flags: `holds_office:<id>`, `office_granted`, `office_lost`, `treason`, `office_vacant:<id>`; counter `offices_granted`.
Five events in `content/events/offices.json`, among them *The Admiral's Temptation* — the design's classic arc: the fleet
is the state's, the guns are in your hand.

## The benches: the parties of the Republic

Since the Republic pass (docs/REPUBLIC.md) the benches are four **parties** seeded from `content/senate/parties.json`
rather than blocs that form from voting records: every house is seated by its laws at the world's creation, rivals
cross the floor every tenth turn when their laws drift, and the player founds the fifth. The affinity, discipline and
whip mechanics below still apply within a party.

## The benches: persistent factions (the mechanics beneath the parties)

Every decided motion leaves an **affinity** between each pair of houses that declared: +1 for voting together, −1 for
voting apart (counter `affinity:<a>|<b>`, clamped −6..12). When a house outside any bench has affinity three or more with
two others also outside one, a **faction** forms around the weightiest of them; a house whose affinity with a leader
reaches three joins (one a bench a turn); a member whose affinity with its leader falls under one breaks with it; a bench
of one dissolves; a dead or fallen leader is replaced by the weightiest member. The chamber names a bench by what it is:
*The Rim Benches*, *The Old Blood*, *The Court Party*, *The Purse-Strings*, *The Peace Benches*, or *The <Leader> Bloc*.

**Discipline.** A member leans two toward its leader's stance on every motion that does not target it or come from it, and
a whip on the leader is half a whip on the bench — so whipping the right house moves five votes. A leader's carried
motions build the bench's **record**; its platform remembers the kinds it stood for and against.

**The player** takes a seat (two influence; the leader's head must not dislike them), leaves (the leader takes it ill:
affinity −2, disposition −5), or founds a bench of their own (two influence, two houses with the affinity). Voting against
your own bench breaks the whip — the same cost — and the bench may drift you out. Flags: `in_faction`, `faction_leader`,
`faction_formed`, `faction_joined`, `faction_dissolved`, `broke_the_whip`, `faction_against_me` (a bench's leader has a
motion against you on the docket: the whole bench will follow); counter `factions`. Five events in
`content/events/factions.json`, among them *Across the Aisle*.

## The consulship

Two elected offices (docs/REPUBLIC.md): every tenth turn the parties' weight elects a Senior and a Junior Consul from
their nominees; the player stands with `StandForConsulCommand`. Consuls weigh more, are paid by the chair, move motions
cheaper, and may veto one open motion a term (`VetoCommand`; rivals veto what strikes at them). Flags `election_near`,
`election_held`, `consul_elected`, `consul_lost`, `vetoed_mine`; counters `elections`, `vetoes`, `consul_term:<house>`.

## The Senate's law, enlarged

**The owner asked (2026-11-24):** "Then take a look at laws that are currently in the game and if they do anything and then
more laws that can be done. Executive edits, laws to vote on, local laws, etc." This section is the laws to vote on: ten
more Acts of the Senate, and a chamber that makes law by itself at a rate a player notices. The review of the laws that
were there before is docs/LAWS_REVIEW.md.

### What was built

**Ten more Acts** (`content/politics/laws.json`, the last ten laws, scope `senate`, `reach: true`). Each is a standing law
of the whole Reach with a custom that does nothing and one or two other options. Each option gives something and costs
something, and its estates' opinions are balanced: what one option pleases, it galls about as much elsewhere, so that a
chamber that carries many Acts does not, by that alone, make every estate in the Reach loyal. Every effect is read by a
rule that already existed, for every house, through the Reach's law (`PolicySystem.ReachModifier`, or
`SenateActs.Reach` for the rules that are handed no catalogue of laws), and is listed in `ReachRead.Also`
(`SenateActs.ReadForTheReach`, registered when the assembly loads).

| Act | Option | What it does | Estates who mind | Houses who mind | The bench that wants it | Modelled on |
|---|---|---|---|---|---|---|
| **The Common Law** | Local courts | lawlessness +1 a year in every region | frontier +10, old blood +10, magistrates -10 | | | the manorial and hundred courts |
| | The customary law (default) | as things are | | | | |
| | The Reach's assizes | lawlessness -1 a year, unrest -1 a year | magistrates +15, commons +5, frontier -10, old blood -10 | a house with worlds in a lawless region (40 or more), or a restless world, is for | Optimates | the Assize of Clarendon (1166) and the king's justices in eyre |
| **The Trade Charter** | Protected markets | what every house pays on the exchanges +10%; tolls +100 credits to a lane world | merchants -15, magistrates +5, old blood +5 | a house of three worlds or more is for | Optimates | the Statute of the Staple (1353) |
| | The customary charter (default) | as things are | | | | |
| | Open lanes | the exchanges -10%; ventures pay +10% | merchants +20, frontier +5, magistrates -5 | a house with a navy to fuel is for | Factors | the Carta Mercatoria (1303) |
| **The Arms Market** | Closed (default) | as things are | | | | the Roman ban on selling arms beyond the frontier (Codex 4.41) |
| | A licensed market | isotopes may be bought on the exchanges | officers +10, merchants +10, magistrates -10, commons -5 | | Sword | the licensing of saltpetre and powder under the Tudors |
| **The Exchange Levy** | No levy (default) | as things are | | | Factors | |
| | A light levy | 5% on shares traded | merchants -10, commons +5 | | Populares | the stamp duty on the transfer of shares (1694 onward) |
| | A heavy levy | 15% on shares traded | merchants -25, commons +10, magistrates +5 | | | the same |
| **The Levy of the Reach** | No common levy | manpower -1 a world | commons +10, officers -10 | | Populares | |
| | The customary levy (default) | as things are | | | | |
| | The Reach's levy | manpower +1 a world; unrest +1 a year | officers +15, commons -15 | a house at war is for; one with a restless world against | Sword | the Roman dilectus, the consuls' levy of citizens |
| **The Usury Law** | Freedom of contract (default) | as things are | | | Factors | |
| | No more than twenty in the hundred | no loan above 20 in the hundred: the counting-houses and every bank cut their rate to it and lend less in the same proportion (the counting-houses' 3,000 credits at 30 to a house of one world become 2,000 at 20); no loan's interest passes the cap, so it is rounded down where rounding up would pass it; a house may not lend another above it, and a refused loan names the law | commons +5, frontier +5, merchants -10 | a lender (a bank, or a house another owes) is against, 2; a house in debt or short of coin for, 1 | | the Twelve Tables' cap on interest |
| | No more than ten in the hundred | the same at 10 (those 3,000 credits become 1,000; the stewards' standing loan of 2,000 becomes 1,000, owing 1,200, under the cap of 20, and 500, owing 500, under this) | commons +10, frontier +5, merchants -15 | a lender against, 3; a borrower for, 2 | Populares | the English Act against Usury (1571), ten in the hundred |
| **The Law on Canvassing** | Coin forbidden at elections | no lavish canvass (4,000 credits) may be paid for; a handsome one (1,500) is written on the record as votes bought, and may be charged | old blood +10, magistrates +10, merchants -10, commons -10 | a house with 7,500 credits or more is against, 2; a house with under 2,500, or with an upright head, for | Optimates | the Roman laws on ambitus (the lex Calpurnia, 67 BC); the Corrupt and Illegal Practices Act (1883) |
| | The customary canvass (default) | a lavish canvass is written down, as before | | | | |
| | Money is free | nothing is written down, however lavish | merchants +10, commons +5, old blood -10, magistrates -5 | the rich are for | Factors | |
| **The Law against Forestalling** | A free market (default) | as things are | | | Factors | |
| | The corner's premium capped | a corner adds no more than 20% to a price | commons +5, merchants +5, old blood -10 | whoever holds a third of a good is against, 2; while a corner stands, a house with a navy (which must buy) is for | | the Assize of Bread and Ale, a price set by law |
| | Engrossing forbidden | a house holding half of a good is made to sell down to under a third at the year's reckoning, to the merchants, at what the good is ordinarily worth | commons +10, magistrates +10, merchants -10, old blood -10 | the holder against, 3 | Populares | the statute against forestallers, regrators and engrossers (1552) |
| **The Law of Hired Companies** | Free companies (default) | as things are | | | Sword | the condotta of the Italian cities |
| | Licensed companies | a fleet hired out costs its house 300 credits, paid to the chair where another house holds the capital, and 1 legitimacy where it costs 3 | magistrates +5, merchants +5, officers -10 | a house with a fleet out on hire is against, 2; the chair, which is paid, for | Factors | the Foreign Enlistment Act (1819), service abroad by licence |
| | Hired companies forbidden | no fleet may be hired out; companies on hire come home at the year's reckoning; lawlessness -1 a year | magistrates +10, merchants +5, commons +5, officers -15, frontier -10 | a house with a fleet on hire against, 3; a house that has hired one against, 2; a house at war with one that has, for, 2 | Optimates | the Statute of Liveries (1468) against private retinues |
| **The Corn Law** | No dole (default) | as things are | | | Optimates, Factors | |
| | Corn at a fixed price | every house pays 100 credits a world a year; unrest -1 a year on every world | commons +10, merchants -5, old blood -5 | a house pays it on every world (half a point a world against, at most 3); a house with a world at unrest 40 or more is for, 2 | Populares | the lex frumentaria of Gaius Gracchus (123 BC) |
| | The free dole | 200 credits a world a year; unrest -2 | commons +15, merchants -10, old blood -10, magistrates -5 | the same, twice | | the free corn of Clodius (58 BC) |
| **The Land Law** | The great estates confirmed | prosperity on every world settles 4 higher; its people grow 1 less a year | old blood +15, merchants +5, commons -15, officers -5 | a house with two worlds at prosperity 60 or more is for | Optimates | the great estates (latifundia) the Gracchi meant to break |
| | The customary holdings (default) | as things are | | | | |
| | Land for settlers and veterans | the people grow 1 more a year; prosperity settles 4 lower | commons +10, officers +5, frontier +5, old blood -15, merchants -5 | a house with a world on the rim is for | Populares | the lex Sempronia agraria of Tiberius Gracchus (133 BC) |
| **The Law of Service** | Short service | the fleets' keep -25% for every house; crews recover readiness 2 a year slower (the fleets of the Reach, not the raiders' or the nations') | commons +10, merchants +5, officers -15 | a house of four hulls or more at peace is for | Populares, Factors | the citizen levy before Marius |
| | The customary term (default) | as things are | | | | |
| | Long service | the keep +25%; readiness 3 a year faster | officers +15, commons -10, merchants -5 | a house at war is for, 2; a large fleet at peace, or a house short of coin, against | Sword | the fixed terms of Augustus (13 BC) |
| **The Navigation Act** | Free dealing between houses (default) | as things are | | | Populares | |
| | A levy on private sales | the chair takes 10 in the hundred of every lot one house sells another, to the single credit, out of the seller's pay | magistrates +5, merchants +5, old blood -10, frontier -5 | a house that has traded with another these three years against, 1; the chair for, 2 | Optimates | the customs on alien merchants' goods |
| | The staple | no house sells another a lot out of its store, and no new standing deal is struck; tolls +100 credits a lane world | merchants +15, magistrates +5, old blood -15, frontier -10 | a trading house against, 2; a house with lane worlds for | Factors | the Navigation Act (1651) and the Ordinance of the Staple (1353) |
| **The Law of the Years** | The years relaxed | every office of the ladder, and the consulship, may be stood for five years younger (never under 18) | officers +10, commons +5, old blood -10, magistrates -10 | a house with someone it would let stand, for an office of the ladder or for consul, is for, a point each, at most 2 | Sword | the waivers the Senate gave Scipio and Pompey |
| | The customary years (default) | as things are | | | | |
| | The years enforced | five years older | old blood +10, magistrates +5, officers -10, commons -5 | a house with someone it would make wait is against | Optimates | the lex Villia annalis (180 BC) |
| **The Citizenship of the Reach** | Citizens of the old worlds only | every region's allegiance settles 8 lower | old blood +10, commons +5, frontier -15 | the chair against, 2; a house that has left the capital's order for | Optimates | the Senate's refusal of the Italians before the Social War |
| | The customary citizenship (default) | as things are | | | | |
| | Citizenship for the whole Reach | every region settles 8 higher | frontier +15, old blood -15, commons -5 | the chair for, 2; a house whose worlds lie where allegiance is under 45 for, 1 | Populares | the lex Julia (90 BC) and the Constitutio Antoniniana (AD 212) |

Where the effect is read, for every house: `Lending.Lenders`, the stewards' standing loan in `TakeLoanCommand` and
`DealingsSystem.LendRefusal` (usury); `LadderSystem.CampaignRefusal` and what `LadderSystem.Campaign` and the other houses'
canvasses write on the record (canvassing); `CornerSystem.Reckon` and `CornerSystem.Held` (forestalling);
`MercenarySystem.Refusal`, `Hire` and `Tick` (hired companies); the per-world line of `Economy.Compute` and its unrest causes
(the Corn Law); `Economy.ProsperityWhy` and the yearly growth in `Economy.Accrue` (the Land Law); `FleetSystem.Upkeep` and
`FleetSystem.Tick` (service, for the fleets of the Reach and not the raiders' or the nations'); `HouseTradeSystem.WontTrade`, `Trades` and the sale itself (the Navigation Act), and the tolls in
`Economy.Compute`; `LadderSystem.Bar` and `OfficeSystem.Candidate` (the years); `RegionSystem.AllegianceTarget` (citizenship).

**The chamber legislates** (`SenateActs.Legislate`, called once a year at the end of `SenateSystem.Tick`). Before, an Act
was moved only as one of the reasons the busiest house in the Senate might have in one year in three, after a war, an
office, a title, the chair's purse and a lawless region; and only when it pleased an estate already hostile to that house.
Over two games of thirty years seven were moved. Now, every year in which nothing at all is before the Senate (so that a
place is always left for the player's own motion), and no Act is on the docket:

1. a house that a law of the Reach in force hurts, by what it does to that house (a lender under a cap, a house with a
   fleet on hire under a licence, a house paying the dole on many worlds), moves its **repeal**, back to the custom, if it
   counts at least 60 for every 100 against;
2. else the **benches** are asked in turn, a different one first each year: each moves the Act of its platform that it
   counts best placed to carry, if it counts at least 80 for every 100 against. It counts as the Senate screen's own
   forecast does. A bench the player leads moves nothing of itself.

The vote is a year later, and the leans move in the meantime. Each Act has a place on the platform of the bench that wants
it (`content/senate/parties.json`, the table above); where two benches want different options, an Act carried by one can be
carried back by the other once the six years of its settling are out and the benches' weight has changed. The player is
told when an Act is moved that would touch the house, with what it would do in figures.

**How a house leans on an Act** follows from what the Act does to that house (`SenateActs.Interest`, added to
`SenateSystem.ActLean`), besides what its estates and its bench make of it: the "houses who mind" column above. The roll
call names the cause: "it lends, and a cap on usury cuts what it earns".

**What the player sees.** The Senate's Act picker lists the fifteen Acts grouped by their category, and against each option
says, after what it does, what it would do to the player's own house in figures (`SenateActs.ToHouse`): "To your house:
your house would pay 500 credits a year, 100 credits for each of its 5 worlds; unrest on each of your 5 worlds would move 1
a year lower." An Act on the docket says the same on its card: the counting-houses' terms as the House screen gives them, and
each of the house the Law of the Years would let stand sooner, or make wait, for the ladder and for consul. The House screen's
standing loan, the Ladder panel and the consular election show the loan and the ages as the law in force leaves them. The Laws window's table of the law of the Reach is grouped
by category. Every new effect has plain words wherever an effect is shown (`SenateActs.Words`).

### The numbers

Measured with `LAWS_USE=1 dotnet run --project src/Tools -- laws --seeds 2`: two games of thirty years of the Old Lion,
a player who does nothing. Before is the working branch as it stood before this work, measured with the same probe.

| | Before | After |
|---|---|---|
| Acts of the Senate in the game | 5 | 15 |
| Acts and petitions moved | 7 | 48 |
| of which a bench moved for its platform (`SenateActs.Legislate`) | none: there was no such thing | 21 |
| of which a house the law in force hurt moved as a repeal | none | 6 |
| of which petitions of an estate | 0 | 11 |
| of which moved as the busiest house's business of the year (`WantedAct`, as before) | 7 | 10 |
| of all these, moved back to the custom | 0 | 9 |
| Carried | 6 | 34 |
| Lost | 0 | 12 |
| Years in which an Act was moved | 7 of 60 | 44 of 60 |
| Motions of every kind decided | 38 | 70 |
| Moved by bench | Populares 6, Optimates 1 | Optimates 16, Populares 11, Factors 9, Sword 1; the estates 11 |

`dotnet run --project src/Tools -- econ --seeds 2`, the middle house's treasury at year 30, money standard, no salaries:
2,110 credits before, 2,370 after; over four games (`--seeds 4`) 2,187 before and 2,515 after. With salaries paid the rise
is larger: standard salaries 1,654 to 2,684 over four games, light 2,094 to 3,440; Generous 7,771 to 11,775. The cause is the
estates, not the Acts' own effects (with every new Act's effects set to nothing the treasury still rose): an Act's estates'
opinions weigh on every house's estates alike, and in these games the Optimates carried most of their platform, so that by
year 20 the Reach's law gave the magistrates +35 to +50 and they were loyal in 17 to 30 houses, which pays each of them 100
credits a world and two of prosperity a year; the officers, galled by the same laws, were hostile in up to 23. Which estate
the law of the Reach makes loyal now follows which bench carries the chamber. This is for the owner (below).

### What I chose

- The ten Acts, their options, effects and estates' opinions as in the table; every option's opinions sum to nothing or
  less. A first version had options that pleased more than they galled, and the chamber carrying a dozen of them made the
  magistrates and the merchants loyal in every house: the money table's middle treasury rose from 2,110 to 3,550 credits.
- The new Acts are settled for six years once carried (the five older ones keep ten), and cost three influence to move.
- Usury: caps of 20 and 10 in the hundred; a lender's limit is cut in the proportion of the cap to its rate, in steps of 500
  credits and never below 500. Loans already made keep their rate. Under a cap the interest of a loan is rounded down to the
  hundred where rounding up would pass the cap, so that no loan costs more than it: 500 lent at 10 owe 500. Where contract is
  free it is rounded up, as before.
- Canvassing: the strict law forbids a lavish canvass and writes a handsome one down; money free writes nothing down.
- Forestalling: the premium capped at 20%; under the ban the holder may keep a third of all there is less one, and is paid
  the lower of the price and what the good is ordinarily worth.
- Hired companies: a licence of 300 credits to the chair, asked only where another house holds the capital, and 1 legitimacy for hiring out instead of 3; the ban also takes
  1 a year from every region's lawlessness.
- The Corn Law: 100 credits a world and unrest -1, or 200 and -2. The Land Law: prosperity 2 (it settles 4 higher or
  lower in the reckoning, which doubles a law's prosperity) against 1 a year of people's growth. Service: 25% of the keep
  against 2 or 3 of readiness a year, for the fleets of the Reach only. The Navigation Act: a levy of 10 in the hundred of a
  lot, taken to the single credit (20 credits of a sale of 200), none on the chair's own sales; the staple adds 100 credits a toll. The years: 5, never under 18. Citizenship: 8 on where every
  region's allegiance settles. Citizenship moves where allegiance settles rather than the existing `allegiance_drift`:
  a drift of a point a year is pulled back by the drift toward where a region settles, so it moves the settled allegiance
  by that point only.
- How a house leans for what an Act does to it: the weights in the "houses who mind" column; a world restless at unrest
  40, rich at prosperity 60; a large fleet four hulls; allegiance thin under 45; a rich house 7,500 credits (five handsome
  canvasses), a poor one under 2,500.
- The chamber: only when nothing is before the Senate; a bench moves at 80 for each 100 against, a hurt house at 60; a
  house is hurt at 2 of interest; the benches are asked in turn.

### What is not built

- Other houses do not hire their fleets out (only the player does), so the Law of Hired Companies touches other houses as
  employers and enemies of the player's companies, by the licences paid to the chair, and by the lawlessness of the ban.
- The Navigation Act's levy falls on lots sold out of a store, not on the deliveries of standing deals; the staple lets a
  deal already struck run out its term.
- No events were written for the new Acts. They are moved, voted and carried as the five older ones are, with the same
  flags (`act_open`, `act_passed:<law>`) for content to read.
- A bench does not whip or lobby for its own Act, and nobody pays to have one moved: a bench that counts short waits.

### For the owner

1. **The estates follow the chamber.** With fifteen Acts, the opinions of what the Senate has carried can push an estate
   past +30 or under -30 in every house at once, and the estates' loyal and hostile effects are not of the same worth: a
   loyal magistracy pays 100 credits a world, a loyal commons calms and fills the worlds. Should the Reach's law count for
   less in what an estate thinks of a house than the house's own laws do (half, say), so that a house is judged first by
   what it does itself?
