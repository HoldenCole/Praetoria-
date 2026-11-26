# Edicts

**The owner asked (2026-11-24):** "Then take a look at laws that are currently in the game and if they do anything and then
more laws that can be done. Executive edits, laws to vote on, local laws, etc" (by "edits" the owner means edicts).

This is the executive's part of that. A law is standing and slow: it is set from a position, it holds until someone changes it,
and the estates drift toward what they think of it. An edict is a decree by whoever holds an executive position, on their own
authority, without a vote. It bites at once, lasts a fixed number of years and then lapses, costs the issuer something, and in
the Republic the Senate can strike it down. The Roman magistrates' edicts, a dictator's decrees and a king's ordinances are the
models.

## What was built

**Positions.** An edict hangs on a position that is actually held in a game (each was checked over two games of thirty years,
below):

| Position | Who holds it | Where the edict falls |
|---|---|---|
| The head of a house | every house, always | the house's own worlds |
| For the Reach | a sitting consul; the Emperor; the head of the house that holds the capital while no consul sits (the first nine years of a Republic, and any gap between consuls) | every world of the Reach |
| Praetor, aedile | the two praetors and two aediles of the ladder, every year (docs/LADDER.md) | the whole Reach (its courts, its markets) |
| The powers | the holder of the emergency powers (docs/EMERGENCY.md), or the Emperor (docs/THRONE.md) | every world of the Reach |

There is no ladder under a throne, so there are no praetor's or aedile's edicts there.

**What one does.**

- **At once**, the day it is proclaimed: unrest or prosperity on each world it falls on, lawlessness or allegiance in each region,
  men or coin raised from each world for the house that holds it, the issuer's standing, the Reach's trust in its magistrates,
  what every other head thinks of the issuer, men sent by every other house (a requisition), or half a named house's treasury
  (proscriptions).
- **What each estate makes of it** moves the issuer's house at once, through `PolicySystem.Sway`.
- **While it lasts**, its effects are the modifier keys laws use, read by the same rules. A house's own edicts reach every rule
  that reads its laws through one line in `PolicySystem.For`; a Reach-wide edict reaches every rule that reads the Reach's laws
  through one line in `PolicySystem.ReachModifier`. What it does while it lasts falls only on the houses of the Reach:
  `Economy`, and `MarketSystem` for what a house pays the exchanges, read the law of the Reach through
  `PolicySystem.ReachModifierFor`, which is nothing for a house that has broken away, as the part done at once leaves that
  house out. What is read with no house named (the four effects
  below) still reaches a house that has broken away. Four effects are the edicts' own and are read only from edicts in force:
  `debts_stayed` (the moratorium: `DealingsSystem.Due` and `Paid`, `Lending.InstalmentOf`, the bank's interest in the ledger,
  pressing a loan, and foreclosure), `court_proof` (added to the forty in a hundred in `LadderSystem.Verdict`, which the
  docket's charges go through too), `price_ceiling` (`MarketSystem.BuyPrice` and `SellPrice` on the good named, and between
  houses `HouseTradeSystem.Mid`, which every price of a sale between houses is reckoned from, and `Asks`, so that a seller who
  dislikes the buyer asks no more) and `senate_cowed` (`ProposeMotionCommand` and the other houses' motions in `SenateSystem`).
  They are not in `ReachRead.Also`: no rule reads them through `PolicySystem.ReachModifier`, so a law of the Senate that named
  one would do nothing, and the audit of the laws should go on saying so.
- **It lapses** at the year's turn when its years are run, with a line in the chronicle: first in the year's turn, before the
  house's court is paid, so that an edict is read on as many paydays as it has years. It lapses early when its issuer leaves the
  position it was issued from (dies, loses the seat or the consulship, is no longer head or Emperor, or the powers end), in the
  same year's turn: it is looked at again at the end of the year's turn, after the elections, the seats, the powers and the
  deaths, so that it does not stand in the way of whoever comes to the position next. Three outlive their issuer, and none of
  those three can be taken back: an amnesty (a pardon given stands), a year's remission of the tithe (it was promised), and a
  forced loan (the house borrowed, not the person, and the merchants are still owed).
- **Limits.** One edict of a kind at a time over the same worlds (another house may have its own amnesty while yours runs); the
  praetor's two edicts on the courts are one group, and only one of them stands at a time; one person may have two edicts in force;
  and the same edict may not be proclaimed again over the same worlds for some years after it ends, however it ended.
- **The Senate's check.** A new motion, "Annulment of an edict" (`annul`, its target an edict, 2 influence to move), may be
  moved by any house of standing against a Reach-wide edict that may be annulled and that would still stand when the vote comes.
  Carried, the edict ends at once and the issuer's house loses 3 legitimacy. The houses lean on it by what the edict does to
  them (a lender under a moratorium, a house holding the good under a price edict, a house with something on the record under
  the strict letter, a house with restless worlds under a requisition, all for; a debtor, a house in a lawless region under the
  closed lanes, against), by what the estates loyal to them make of it, and by what their head thinks of the issuer (a friend at
  +30 against, an enemy at -30 for); the issuer's own bench leans against, and the issuer's house votes against. Under a throne,
  or while emergency powers run, there is no annulment: none may be moved, and one moved before the powers began and carried
  while they run strikes nothing down, with a line in the chronicle that says why. A house's own edicts on its own worlds are
  its own business. The Senate is not asked to strike down an edict of the asker's own house: the one who issued it may take it
  back, and nobody else may.
- **Commands.** `IssueEdictCommand(actorId, edictId, content, targetId = "")` and `WithdrawEdictCommand(actorId, edictId)`. Every
  refusal says what is missing: "only a sitting praetor may issue it", "an amnesty is in force already on the house's worlds, until
  year 9", "you have 2 edicts in force, and 2 is the most one person may have", "an amnesty was proclaimed lately: it may be again
  in year 14", "needs 600 credits and the house has 200", "needs 300 credits of your own, and you have 100", "nothing is short: the
  dearest good, materials, is at 112 in the hundred of its usual price, and an edict of maximum prices wants 150", "an amnesty has
  been given and cannot be taken back".
- **Who pays.** Influence of the year from the issuer, and credits from the house's treasury. A relative of the player pays from
  their own purse, whatever the edict, as for games (the house's treasury is the head's).
- **Other houses and magistrates** issue edicts for plain reasons, through the same command, in `EdictSystem.Tick` (one line in
  `TurnController.EndTurn`, after the other houses act and before the Senate sits; `EdictSystem.Lapse` comes first in the year's
  turn and `EdictSystem.EndWithIssuers` last). Each person who holds a position considers it one year in two (a head of a house
  one year in four), takes the edicts of the position in an order of that year's, and issues the first whose reason holds and
  which the command allows: one a person a year. The player's relatives in any of these positions (a seat as praetor or aedile,
  a consulship, the capital while no consul sits, the emergency powers) are people with wills of their own: they decide the
  position's edicts, as they decide its business, and pay for them out of their own purse. Another house moves an annulment one
  year in two when an edict galls it by 2 or more and it may move a motion. The player is told of every Reach-wide edict another
  issues (a warning when it galls the player's house; when the one who issues it is a relative, whose purse paid for it), of one
  that names the house, and of the end of one that was the house's, fell on its worlds or named it, and of every annulment
  carried, and why one carried strikes nothing down.
- **Screen.** The Laws window (`LawsScreen.razor`, one line) carries `EdictsPanel.razor` (block `data-edicts`): the edicts in force
  that touch the player's house (`data-edict-in-force="<id>"`), with who issued them, until when and what they do, a button to take
  back one's own (`data-edict-withdraw`) and whether the Senate may be asked to strike it down (`data-edict-annul`); then the
  edicts the player may issue now by the positions held, each a button (`data-edict="<id>"`) whose title is the cost, what it does
  at once and while it lasts, who minds and whether the Senate may annul it, or the refusal when it is greyed; and a house to name
  for proscriptions (`data-edict-target`). The Senate window offers the annulment among its motions, with the edicts in force that
  may be struck down as its targets.

## Every edict

Amounts are in credits as the game shows them. "Years / again" is how long it lasts, then how many years before it may be
proclaimed again over the same worlds.

| Edict | Issued by | Cost | At once | While it lasts | Years / again | Who minds (estates) | Senate may annul | Modelled on |
|---|---|---|---|---|---|---|---|---|
| An amnesty | head, own worlds | 2 influence | unrest -10 on each world | unrest -1 a year | 3 / 8 (outlives) | commons +6, magistrates -6, officers -3 | no | the Athenian amnesty of 403 BC; Caesar's clemency |
| A muster of the worlds | head, own worlds | 1 influence | +3 manpower from each world | prosperity settles 4 lower; unrest +1 a year | 2 / 5 | commons -6, officers +5, frontier -2 | no | the Roman dilectus; the feudal arriere-ban |
| A forced loan from the merchants | head, own worlds | 1 influence | +400 credits from each world | -100 credits a world a year (repaid, no interest) | 4 / 8 (outlives) | merchants -10, commons +3, old blood -2 | no | the Forced Loan of Charles I, 1626; the Venetian prestiti |
| A year's remission of the tithe | head, own worlds | 1 influence | unrest -6; legitimacy +1 | -200 credits a world; unrest -2 a year; +1 people a year a world | 1 / 10 (outlives) | commons +10, magistrates -5, merchants +3 | no | the jubilee of Leviticus; Hadrian's burning of the tax records, 118 |
| A sumptuary edict on the household | head, own worlds | 1 influence | | +100 credits a world a year; each court place paid 1 credit less, loyalty -2 a year | 3 / 6 | old blood -8, merchants -5, commons +4, magistrates +3 | no | the Lex Oppia, 215 BC; the Lex Fannia, 161 BC; the Tudor statutes of apparel |
| A day of games | head, own worlds | 1 influence and 600 credits | unrest -5; legitimacy +1 | unrest -1 a year | 1 / 4 | commons +8, old blood -2, magistrates -2 | no | the Roman ludi |
| A corn dole | for the Reach | 2 influence and 1,000 credits | unrest -5 on every world | unrest -1 a year everywhere | 2 / 6 | commons +8, merchants -4, old blood -4 | yes | the Lex Frumentaria of Gaius Gracchus, 123 BC; Clodius's free dole, 58 BC |
| A requisition of men for the state | for the Reach | 2 influence | every other house sends 1 manpower; its head thinks 2 the worse of the issuer | +1 manpower a world a year everywhere; unrest +1 a year | 2 / 6 | commons -6, officers +5, frontier -4 | yes | the consular levy in a tumultus |
| A moratorium on debts | for the Reach | 3 influence | | nothing falls due on any loan, no interest is paid, no creditor presses or forecloses, no year is missed | 2 / 10 | merchants -12, commons +6, magistrates -4, old blood +3 | yes | Caesar's relief of debtors, 49 BC; the moratoria of August 1914 |
| An edict of maximum prices | for the Reach | 2 influence | names the dearest good (it must be at 150 in the hundred of its usual price or more) | that good bought and sold at no more than 125 in the hundred of its usual price, on the exchanges and between houses | 2 / 8 | merchants -10, commons +6, officers +2 | yes | Diocletian's Edict on Maximum Prices, 301 |
| The lanes closed to raiders | for the Reach | 2 influence and 800 credits | lawlessness -8 in every region | lawlessness -2 a year everywhere; -100 credits on every toll | 3 / 8 | frontier -8, merchants +6, officers +3 | yes | the Lex Gabinia, 67 BC |
| A day of thanksgiving | for the Reach | 1 influence and 500 credits | allegiance +4 in every region; trust +2; legitimacy +1 | unrest -1 a year everywhere | 1 / 5 | old blood +4, commons +4, merchants -2 | no (it lapses before a vote) | the Roman supplicatio |
| The praetor's edict: the strict letter | praetor | 1 influence | trust +2 | a charge proved 15 in a hundred more often before a praetor who goes by the pleading | 2 / 3 | old blood -6, commons +5, magistrates +4 | yes | the praetor's edict posted on taking office; the Lex Cornelia of 67 BC |
| The praetor's edict: equity | praetor | 1 influence | trust -1 | a charge proved 15 in a hundred less often | 2 / 3 | old blood +5, magistrates -4, commons -4 | yes | the praetor's relief against the strict law: the exceptio, restitutio in integrum |
| The aedile's edict on the markets | aedile | 1 influence and 300 credits | trust +1 | everything on the exchanges 8% cheaper to buy, for every house of the Reach | 2 / 4 | merchants -6, commons +5 | yes | the curule aediles' edict on sales in the market |
| Proscriptions | the powers | 2 influence | half the named house's treasury, up to 3,000 credits, to the issuer's; it loses 4 legitimacy and its head thinks 40 the worse; the issuer's house loses 3 legitimacy; trust -3; every other head thinks 8 the worse | unrest +1 a year everywhere | 3 / 6 | old blood -12, magistrates -8, officers +4, commons -3 | no | Sulla's lists, 82 BC; the Triumvirs', 43 BC |
| Rule by decree | the powers | 2 influence | legitimacy -2; trust -4; every other head thinks 5 the worse | no house but the issuer's may move a motion; the other houses bring none | 3 / 8 | magistrates -10, old blood -8, officers +3 | no | Sulla's dictatorship for the making of laws, 82 BC |
| A levy in mass | the powers | 2 influence | +2 manpower from every world of the Reach, for its holder | +1 manpower a world a year everywhere; unrest +2 a year | 2 / 8 | commons -10, officers +8, merchants -5 | no | the levee en masse of August 1793 |

## Why another house or magistrate issues one

| Edict | The reason |
|---|---|
| An amnesty | a world of the house at unrest 15 or more |
| A muster | the house at war with under 12 manpower |
| A forced loan | three worlds or more, and under 500 credits in hand |
| A remission of the tithe | a world at unrest 25 or more, and 4,000 credits in hand |
| A sumptuary edict | one or two worlds, and 100 credits or less in hand |
| A day of games | a world at unrest 10 or more, and 2,000 credits in hand |
| A corn dole | four worlds of the Reach at unrest 20 or more, or one of the house's at 25, or the house's commons at -20 or worse |
| A requisition | the Reach in danger (docs/EMERGENCY.md), and the house at war or the Reach at war beyond its rim |
| A moratorium | the issuer's own house in debt, and lending nothing |
| An edict of maximum prices | a good at 160 in the hundred of its usual price |
| The lanes closed | a region where the house has a world at lawlessness 35, or any region of the Reach at 50 |
| A day of thanksgiving | the danger just past, a war of the house just won, or the Reach's trust in its magistrates under 45 with 2,000 credits in hand |
| The strict letter | the praetor is honourable or just |
| Equity | someone of the praetor's house has something on the record not yet answered for |
| The aedile's edict | a good at 130 in the hundred of its usual price |
| Proscriptions | a head who thinks -40 of the holder or worse (that head's house is named) |
| Rule by decree | a motion against the holder's house before the Senate, or a censure of it in the last two years |
| A levy in mass | the great war, a civil war, or a rising against the throne |

## Measured

Two games of thirty years, `the_old_lion`, seeds 1 and 2, a player who does nothing (the loop of `src/Tools/LawsProbe.cs`):
`dotnet run --project src/Tools -- edicts --seeds 2`.

| | Seed 1 | Seed 2 | Both |
|---|---|---|---|
| Edicts issued by other houses and magistrates | 26 | 29 | 55 |
| ... by the heads of houses, on their own worlds | 9 | 12 | 21 |
| ... for the Reach (a consul, or the holder of the capital while no consul sits) | 8 | 8 | 16 |
| ... by praetors | 5 | 5 | 10 |
| ... by aediles | 4 | 4 | 8 |
| ... under the powers or a throne | 0 | 0 | 0 |
| Annulments moved by other houses | 2 | 2 | 4 |
| ... carried, and the edict struck down | 2 | 1 | 3 |
| ... lost | 0 | 1 | 1 |
| Times the player was told | | | 38 |

By kind, in both games: sumptuary 18, the aedile's market edict 8, equity 5, maximum prices 5, the strict letter 5, the lanes
closed 3, moratorium 3, requisition 3, amnesty 2, thanksgiving 2, muster 1; games, the remission, the corn dole, the forced loan,
proscriptions, rule by decree and the levy in mass none. Ended: 50 with their years, 3 by the Senate, and 2 still in force when the
games stopped. The annulment that was lost was of a requisition, 68 to 71.

A first measure found magistrates proclaiming edicts in the year their seats fell vacant, to lapse the next year with them (seven
in two games); a magistrate whose seat is up at this year's election, and a consul at the end of the term, now decree nothing.
An edict already in force went on for a year after its issuer's term ended, and stood in the way of the one who came next; it now
ends in the same year's turn. Once it did, a holder of the capital was found proclaiming edicts in the year the consuls were
elected, to lapse with the position at that same year's turn (two in four games); the holder of the capital in that year, and a
holder of the powers in the year they are laid down, now decree nothing either.

Nobody held the emergency powers or a throne in either game, so the three edicts of the powers were not issued by others there;
`tests/EdictTests.cs` seats a holder and shows each one issued, and the reason each is issued for. The unrest edicts (games, the
remission, the corn dole) are few because unrest stays low in these games: the most restless world in most years is under 10.

The economy (`dotnet run --project src/Tools -- econ --seeds 2`, the median treasury), the standard row at year 30: 2,110 credits
before, 2,400 after. The generous row moved most, 7,759 to 13,159 at year 30, and the lean row 1,675 to 2,360; at year 10 the rows
are where they were to within 210 credits (the standard row 925 before, 825 after). A forced loan, a sumptuary edict and a price
edict all move coin, and thirty years of other houses' choices compound. The houses in the red at any year measured are 0 to 3 of about
sixty in every row (0 to 2 before); the most is the row that pays crews, at year 20.

## What I chose

Every number here is mine.

- 18 edicts: six for the head of a house, six for the Reach, two for the praetor, one for the aedile, three for the powers.
- Each edict's cost, years, years before it may be proclaimed again, what it does at once and while it lasts, and what each estate
  makes of it: the table above.
- Two edicts in force at most for one person (`EdictSystem.MaxPerIssuer`).
- 3 legitimacy lost by the issuer's house for an edict struck down (`AnnulledStanding`).
- Proscriptions: half the treasury, up to 3,000 credits; 4 legitimacy lost by the house named, and its head 40 the worse
  (`ProscribedStanding`, `ProscribedRegard`).
- A requisition: one man from every other house, and its head 2 the worse (`RequisitionRegard`).
- An edict of maximum prices names a good at 150 in the hundred of its usual price or more (`ShortAt`), and holds it at 125.
- The annulment motion costs 2 influence to move, like a censure; it may not be moved against an edict that lapses before the
  vote (fewer than two years left), nor against one's own house's edict.
- Other houses: a head considers an edict one year in four (`HeadOneIn`), a magistrate or a holder for the Reach one year in two
  (`OfficeOneIn`); a house moves an annulment one year in two (`AnnulOneIn`) when the edict galls it by 2 or more (`MoveAt`).
- How a house leans on an annulment: the issuer's house -4; a lender under a moratorium +3, a debtor -2; a house holding 10 of the
  good under a price edict +2, one with fleets -1; under the aedile's edict a house of three worlds +1 and one with fleets -1; a
  house with a world at unrest 30 under an edict that raises unrest +2, and +1 more for a requisition; a house with a world at
  unrest 20 under one that calms it -1; a rim house under the closed lanes +1, a house with a world in a region at lawlessness 40
  -2; a house with something on the record +2 under the strict letter, -2 under equity; the estates loyal to the house, a point for
  each six of their dislike, two at most either way; a head at +30 to the issuer -2, at -30 +2; the issuer's bench -1.
- The reasons for which others issue each edict, and their thresholds: the table above.
- The three that outlive their issuer and cannot be taken back: the amnesty, the remission and the forced loan.
- An edict that a relative of the player issues paid from that relative's own purse, whatever its kind.
- One edict a person a year, the Reach's business before the house's; none from a magistrate whose seat is up at this year's
  election, nor from a consul or the holder of the capital in the year of the consular election, nor from a holder of the powers
  in the year they are laid down.

## What is not built

- **The head of a breakaway state** (a king, president or dictator, docs/BREAKAWAY.md) has no edicts for the houses of that state:
  "for the Reach" means the Reach. A state's head decrees on their own house's worlds like any head. The Senate's Acts and the
  chair's laws now follow the same rule, on the owner's word (docs/BREAKAWAY.md, "A house that has broken away is bound only
  by its own laws").
- **Credit drying up.** Under a moratorium the banks and the counting-houses still lend; only what falls due waits.
- **Edicts that name a region or a world.** Every edict falls on all of the house's worlds or all of the Reach.
- **The magistrates of the ladder below the praetor and aedile** (the quaestors) have no edicts.
- **A record of edicts past.** The chronicle has every one; the screen shows only those in force.
- **Events.** There are no reports written for edicts; the news and the chronicle carry them.

## Files

`content/politics/edicts.json`, `content/senate/motions.json` (the `annul` motion), `src/Core/Data/EdictCatalog.cs`,
`src/Core/State/Edict.cs`, `src/Core/Systems/EdictSystem.cs`, `src/Core/Commands/EdictCommands.cs`,
`src/Web/Components/EdictsPanel.razor`, `src/Tools/EdictsProbe.cs`, `tests/EdictTests.cs`.
