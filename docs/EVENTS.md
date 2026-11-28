# The four sets of new events: war, votes, family and chance

The owner asked: "Let's make some events for war, voting, family events, and random interactions." Four authors wrote one
set each, from one brief, and the four were merged on the branch `ev-build`; two rounds of review followed (below). That is 65 new reports, all of them news of
something that happened in the game: a battle, a peace, a count of votes, a birth, an illness, an evening where you met
someone. Each names the people in it by role and asks you to decide something that changes what you care about.

| Set | What it is about | Reports | Events and text | Tests | Design doc |
|---|---|---|---|---|---|
| War | The people a war is fought by: officers who dared or ran, a young relative who wants to go, may be hurt and comes home, a prisoner, an enemy who asks to talk, deserters, a sack beyond the rim, and what the peace asks of you | 15 | `content/events/war_front.json`, `content/text/war_front.json` | `tests/WarEventsTests.cs` | [docs/events/war.md](events/war.md) |
| Votes | The Senate's votes and the elections, before, during and after. The floor's reports in either era, the ladder's and the consuls' in the Republic | 17 | `content/events/votes.json`, `content/text/votes.json` | `tests/VoteEventsTests.cs` | [docs/events/votes.md](events/votes.md) |
| Family | The life of your own family, each of them a person with a mind of their own: births, children, the spouse, illness, old age, the will | 16 | `content/events/kin.json`, `content/text/kin.json` | `tests/KinEventsTests.cs` | [docs/events/family.md](events/family.md) |
| Chance | "Random interactions": the people of the Reach crossing your path, bound by who they are, at a low weight and repeatable | 17 | `content/events/encounters.json`, `content/text/encounters.json` | `tests/EncounterEventsTests.cs` | [docs/events/chance.md](events/chance.md) |

Each design doc says, for every report, when it fires, what each choice does, every number and why, what was cut, and what
was added to Core. This page is the index.

## The deck as a whole: waiting times, once a person, two answers, versions, moments (2026-11-28)

The review against Steam (docs/STEAM_REVIEW.md, row 3) found reports that fire for the wrong person and come back too often: a
68-year-old Admiral offered *The Empty Command* in year 1 and again, word for word, in year 2; *Not Seen in the Senate* eleven
times in a 54-year game; 133 reports answered and only 79 of them different. This is what changed in the engine and in the deck,
and how it was measured (`events`, docs/TOOLS.md, "A tool of our own: the events audit").

### Waiting times

- **A repeatable card waits before it comes again to the same head of the house**: its `"cooldown"` in years, or 10 when its
  event names none (`EventDef.DefaultCooldown`, measured below: 9 and 10 were tried). After its second coming it waits twice
  that, after its third three times, and so on (`EventEngine.WaitAfter`), so a card that is always eligible does not come back
  every decade of a long life. A card that is not repeatable comes once in a game, as before.
- **Counted per head.** The record is `World.Cards`, keyed by event and head: the year the card last came, how often, and in
  which version of its words. A new head has met none of them and may meet each again. A save keeps the record; a save from
  before it loads with none, as if no card had come yet (`EventCardsTests`).
- **New, or news, before old.** While a card the house has not had (under any head), or news of something that happened this year
  or last, can come, the year's reports are drawn from those; a card the house has had comes again only in a year with nothing
  new to tell, and then alone (`EventEngine.SelectBriefing`, `Director.HadByTheHouse`). Late in a long game a year may bring two
  reports, or one, where it brought three that had all come before. Among the cards that may come, the Director divides a card's
  weight by one more than the times it has come to the house (news), or by the square of that (any other card).
- Cards that name their own wait: *The Wedding* and *The Naming of a Child* (0, and once for each person: below), *A Motion Against
  the House* (2: each censure moved against the house is answered).

### Once for each person

A role may say `"once": true`: it binds each person at most once for that event, ever (`RoleDef.Once`; the record is
`World.CardsMet`, event, part and person, with the year). A life event about one person's milestone is one: *The Naming of a Child*
(the child), *The Wedding* (the spouse), *A Grandchild Is Born* (the grandchild).

### Two answers open

- **A card is drawn only when at least two of its answers are open** to the head: requirements met, and the pools an answer costs
  there to pay as the year's pools stand (`EventEngine.OpenAnswers`, in `GatherEligible`). An event with a single answer is refused
  by both validators. Answers later in the year can still close one (money or influence spent): that is the player's own doing,
  and the locked answer says so.
- **The check**: `EventMomentsTests.EveryStart_DrawsNoCardWithOneAnswerOpen_AndNoneBackWithinItsWait` plays every start ten years
  and fails if any card is drawn with fewer than two answers open, or comes back within its wait; the events audit counts the
  cards held back at the draw and the cards a stand-in found with one answer open.
- **The deck.** A card whose only open answer was the plain one, with the others for a trait, would now never come to a head without
  the trait. Where an answer anyone could give was gated on a trait (to refuse, to defer, to confess, to yield, to own it, to
  govern or rule by the law, to make peace at the table, to face a coalition down at its price, to answer a censure from the
  floor, to dismiss a whisper, to stay unsworn at its price), it is open to anyone now; the dark and the clever answers keep their
  traits. Where the second answer is skill, it can be had by the skill as well as the trait: the diplomat's (diplomacy 2), the
  orator's (oratory or charisma 2), the eclipsing commander's (tactics 3), the consolidating treasurer's (administration 3), the
  schemer among claimants (intrigue 2), the delegating officer (rank 2). An ask the house can only answer with money comes to a
  house that has it: *The In-Laws Ask a Favour* (1,500 credits), *A Secret for Sale* (400).
- **The event window.** A locked answer, clicked, flashes the line under it that says what it needs, and a reader is told the same
  (`EventWindow.razor`; `tools/browser/locked.js`).

### Versions of the words

A text entry may carry `"versions"`: other bodies, each with its own title and choice words if it likes (`EventText.Versions`).
The engine draws one each time the card comes, by the world's dice, never the one it last came in (`FiredEvent.Version`); a save
made in the middle of the year keeps the version each report was drawn in. Written for the four cards the review named:
*Not Seen in the Senate* (`content/text/ladder.json`), *Shares of Their Own* (`wealth.json`; now *Shares of {kin}'s Own*, with
its words to the player as "you" and no "they" for a named relative), *The Leader's Table* (`situations.json`) and *The Empty
Command* (`career_progression.en.json`). Each has three versions now, with their own titles and answers.

### Moments in the year: the wedding and the naming

The year in brief told a marriage and a birth as done, and only afterwards came the cards asking how the wedding and the naming
should go. A match agreed in the year had its wedding card as the next year opened, and up to two years late. The fix is in the
engine, because the cards were right and the timing was not:

1. **A marriage made in the year is made then.** A match an answer agrees (`addBond` marriage) is written into the record at once,
   with its mark, instead of at the year's turn (`AddBondEffect` calls `CourtshipSystem.Sync`); a match agreed by a command already
   was (`CourtshipSystem.Wed`).
2. **A report about a moment of the year comes with the moment.** An event that says `"moment": true` is drawn at once into the
   year's reports when the head's own act in the year (a command, or an answer) raises a world flag its conditions read
   (`TurnController.AfterAct`, `EventEngine.DrawMoment`, told by `CommandExecutor.Executed`). *The Wedding* is one: it comes the
   moment the match is agreed, in the year of the marriage, and the next year's brief tells the marriage after it.
3. **A moment at the turn of the year is told with its card.** A birth happens as the year turns, and its naming is drawn as the
   next year opens, with the birth. The year in brief's line for a birth or a marriage whose own card (a role marked once) came
   as the year opened says so: "A child is born to your house: ... “The Naming of a Child” is among this year's reports."
   (`YearInBrief.WithItsCard`). So the brief never tells a moment as done before the card that is the moment.
4. Once for each person, so the wedding and the naming never come twice for one spouse or one child.

### Conditions from the real state of the game

Four conditions were added (docs/CONTENT_GUIDE.md): `office` (the role holds an office: `sovereign` is the head of the house on
the throne, or in the Republic of the house that holds the chair; `head` the head of their house; `consul`, `magistrate`,
`commander`, or an office or rung by id), `ventures`, `topRank` (on the top rung of their own ladder) and `origin` (an old name or
a new one).

| The misfire the review found | The fix |
|---|---|
| A 68-year-old Admiral offered *The Empty Command* in year 1 and again in year 2 | Lieutenant to Captain, 40 or under, in no office of the Reach, and the house at war; a 10-year wait; three versions |
| A 17-year-old Ensign with no ships told "another commission lands on your desk, more hulls" (*The Fleet Grows*) | A house with 3 hulls or more, a head of Commander's rank or more and 21 or older; the rival bound to the head of a house. *The Treasury Tightens* likewise: rank 2 and 21 |
| Last year's fellow cadet shown as the sovereign, "the most powerful person alive" (*The Emperor's Eye*) | The sovereign binds by `office`: the head of the house on the throne. So do *The Crown's Due* and *The Crown's Eye*, which bound anyone of higher rank |
| The New Man asked to honour "paper your grandfather signed" (*The Old Paper*) | Not for a new name (`new_name`); a plain refusal open to anyone |
| A Fleet Admiral whose ambition is "Earn a command" | `AmbitionSystem.Outgrow` (docs/AMBITIONS.md, "Outgrown") |
| The wedding and the naming after the brief told them | Moments in the year, above |
| At least 5 of the 43 reports of a first hour with one answer open, and a locked answer that did nothing | Two answers open, above |

The sweep for the same kinds: *The Act Carries*, *The Act Falls*, *The Comet*, *The Exiles*, *The Commons March*, *The Merchants'
Delegation*, *The Magistrates' Find*, *The Commons' Feast*, *The Merchants' Gift* and *The Old Blood's Invitation* speak of the
house's worlds and now require one; *Dry Tanks* and *The Officers' Toast* speak of its fleet and require hulls; *The Crash* comes
to a house with holdings; every role named for the head of a house binds the head (`rival_house` in four coalition cards,
`coalition_head`, the grandee of *The Old Blood* and of *A Great Merchant's Venture*, who is also of an old name in the first);
twelve cards whose answer is a step up the ladder (*The First Command*, *The Test*, *The Admiral's Shadow*, *The Governor's Seat*,
*The Policy Fight*, *The High Treasury*, *The First Case*, *The Great Trial*, *The Praetor's Robe*, *The Crossroads*, *The Teacher,
Outgrown*, *Mentioned in Dispatches*) no longer come to one at the top, and the "first" cards only to the first rungs.

### Measured

`events --scenario all --years 20 --seeds 3`, and the same at 54 years (the length of the review's game), on the code before this
work (`c7ac572`, with the audit tool added and measured against a ten-year wait it did not have) and after. The stand-in answers
every report; "different of answered" is the different events among those answered in one game, summed over the games.

| Start | 20 years: different of answered, before → after | Back within the wait | Misfires | One answer open | 54 years: different of answered, before → after | Reports a game, before → after |
|---|---|---|---|---|---|---|
| The Academy | 81.5% → 96.3% | 32 → 0 | 33 → 0 | 25 → 0 | 58.6% → 85.1% | 174 → 127 |
| The Circling Heir | 80.6% → 96.2% | 30 → 0 | 26 → 0 | 11 → 0 | 54.7% → 85.7% | 162 → 133 |
| The Fallen House | 78.9% → 95.0% | 29 → 0 | 30 → 0 | 19 → 0 | 57.4% → 79.9% | 162 → 139 |
| The Imperial Court | 76.7% → 97.8% | 31 → 0 | 36 → 1 | 11 → 0 | 54.5% → 81.6% | 162 → 123 |
| The Institute | 81.0% → 98.6% | 25 → 0 | 45 → 0 | 9 → 0 | 57.3% → 90.0% | 174 → 127 |
| The New Man | 75.0% → 97.8% | 31 → 0 | 42 → 0 | 18 → 0 | 58.4% → 80.2% | 162 → 129 |
| The Old Lion | 77.2% → 96.1% | 22 → 0 | 28 → 0 | 11 → 0 | 52.7% → 80.9% | 162 → 136 |
| The Risen Officer | 76.7% → 97.8% | 30 → 0 | 37 → 1 | 21 → 0 | 54.6% → 84.9% | 152 → 106 |
| All | 78.6% → 97.0% | 230 → 0 | 277 → 2 | 125 → 0 | 56.1% → 83.4% | 164 → 128 |

- **The known misfires**: 237 over 20 years before (508 over 54), none after on either.
- **What is left**: two misfires over 20 years and three over 54, all one card: *A Great Khan* tells a house with no fleet to "see
  to your own fleets", and its words are to change with the rewrite of the text. *The Line Breaks*, about the battle that took the
  last hull, is right to speak of it and is not counted. Over 54 years the stand-in twice found *A Secret for Sale* with one answer
  open: two were open at the draw, and its own earlier answers that year had spent the money.
- **The price**: late in a long game a year with nothing new to tell brings fewer reports, 128 a game over 54 years where there were
  164. The first 20 years keep three a year (1,520 reports against 1,512).
- **How the wait was chosen**: 9 and 10 years were measured on the fixed deck before the last rule was added: 78.6% and 79.9% of 54
  years' answers were different. 10 was kept, and with "new, or news, before old" it gives 83.4%. Every head may meet a card
  again: a five-year memory across heads was tried and made no difference (79.8%).

## No new verbs

None of the four sets added a condition or an effect verb. Everything is said with the verbs already in
`docs/CONTENT_GUIDE.md` and the parsers (`src/Core/Data/ConditionParser.cs`, `EffectParser.cs`). What a choice shows the player
changed in review: its chips now name an estate it moves ("the Commons +4"), the magistrates' trust, and a killing
(`src/Core/Presentation/ChoiceStakes.cs`); `docs/CONTENT_GUIDE.md` says so.

## What was added to Core

Small marks, so that a report can find the person in the news. Each is a flag cleared after a year or at the next moment of its
kind, in the pattern of the existing `Mark()` moments.

- **War** (`FleetSystem`, and one line each in `NationSystem.War.cs`, `NationSystem.Map.cs`, `NationSystem.cs`, `ServiceSystem.cs`,
  `NpcAi.cs`, `ProvinceSystem.cs`, `FamilySystem.cs`): `in_the_battle` on your house's officers in a battle, with `in_a_won_battle` or
  `in_a_lost_battle` by its result; `taken_prisoner`, `held_to_the_peace` and `on_parole` on a captain taken from a beaten line (a
  prisoner may not command, lead a levy or step up to a post); `enemy_bears_down` and `bears_down_on_us` when an enemy fleet will
  reach yours next year, and `ships_counted`/`ships_known` for a fleet whose ships were counted at a parley (it fights at 90%);
  `our_peace` when your house makes peace (news for two briefings); `sacked_beyond` on the head of a house whose fleet sacks a
  nation's world; `nation_war_over` when any war with a nation ends (news for two briefings), and, in round 2, `our_nation_war_over`
  when it was a war your house was in (`NationSystem.WasOurs`), which the reports of your own peace read; a battle lost, on the map or
  beyond it, may wound a relative at the front (`wounded_at_the_front`); a battle beyond the Reach marks your officers as one on the
  map does (`FleetSystem.MarkTheBattleAbroad`); `at_war_any` is refreshed when a war with a nation begins or ends; the war's people
  (`officer_owed`, `officer_wronged`, `at_the_front`, `blooded`, `wounded_at_the_front`) and `crews_reckoned` are struck out once the
  peace is old news.
- **Votes** (new `src/Core/Systems/VoteMarks.cs`, called from `SenateSystem`, `ElectionSystem` and `LadderSystem`): who moves a
  motion, who moves against you, who is undecided on yours, who voted each way on yours, the bench leader who saw your abstention,
  the head who took your purse, the houses an Act went against, who beat yours and who yours beat at the ladder's count.
  `bribe_out` is now cleared after a session, and `election_held` is marked. In review: `VoteMarks.Bargains` turns a vote given
  for a vote (`owes_you_a_vote`) into two favours owed and a promised vote (`promised_your_vote`) into one, which the Senate already
  keeps and judges at the count; `lost_at_the_count` on your relative who lost at the ladder; `dined_with_you` on a consul, who once
  in the term lets a motion of yours stand (`ElectionSystem`). Round 2: `standing_heard` on a relative whose candidacy you answered,
  struck by the ladder when they no longer stand; one pair of victor and loser live at a time at the ladder's count. The vote
  promises (2026-11-27, docs/events/votes.md "Promises"): a bargain is made as the choice is (`EventEngine.Resolve` calls
  `VoteMarks.Bargains`); a favour owed to your house is a vote for your motion (`WealthSystem.VotesFor`); the word given in *A Vote
  for a Vote* is a pledge; `their_house_owes_you_a_vote` (*A Favour Repaid*, *The Whip*, *A Friend at Another Table*) and
  `no_bargain_with_you` (a favour standing, or a broken word) are marked; `whip`, `lender` and `creditor` are outsiders' parts.
  The bought group (2026-11-28): `sold_your_vote` (*A Vote for a Vote*'s price, *A Bought Vote Called In*'s word, *The
  Whip*'s purse) makes a vote bought, declared for the buyer's motion and judged at its count (`VoteMarks.Sell`,
  `VotePledge.Paid`); `wants_your_vote` marks the houses whose motion your vote may be had on, read by those three reports.
  The pledge group (2026-11-28): `calling_in_favours` (*A Motion Against the House*) has every house that owes yours a favour
  declare against the motion against your house (`DealingsSystem.CallInAgainst`), open while `favours_to_call` stands;
  `debt_called_in` (*The Favor Called*) pays the debt with the debtor's house's vote where a motion needs it
  (`DealingsSystem.DebtPaid`). The office group (2026-11-28): `dined_with_you` is read on the consul, not the head of their
  house; `divided_the_year` (*Your Colleague, the Consul*) keeps the colleague off your motions for the term; `the_other_consul`
  and `other_consul_to_court`, written when a consul strikes your motion, open *The Veto Falls*' court, which makes that consul
  `a_friend_in_the_chair` who strikes the next motion against your house; `canvassed_every_door`, `canvassed_with_coin` (*The
  Canvass*) and `lost_with_grace` (*The Count Goes Against You*) are votes at the next count of the ladder; under emergency powers
  the ladder's count waits too (`ladder_election_owed`); the Reach's trust is written down from the first. The bench group
  (2026-11-28): `promised_to_stand` (*The Empty Seat on the Bench*) is your word to vote at the bench's next vote, judged at that
  count; `bench_fed` and `bench_whipped` (*The Leader's Table*) have your bench follow your vote one point more at the next count;
  a vote against your bench is remembered at the next count (`whip_remembered:`); `bringing_a_senator_over` (*The Growing Bloc*,
  open while `bloc_can_grow`) and `helping_a_client` (*A Client Asks*, open while `client_to_help`) make a house owe yours a vote,
  chosen by the engine, not by a new role; `splitting_the_bench` (*Across the Aisle*, open while `bench_can_be_split`) declares the
  bench's weakest member against the motion; `faction_against_me` is written with the docket, for a motion that strikes at you and
  would carry; your clients and patron lean the way you vote on every division. The other group (2026-11-28): `against_yours`
  marks a head whose house leans against your motion, read by *An Old Grudge Against Your Motion*, and it and `undecided_on_yours`
  are written again as the year's reports are chosen (`VoteMarks.Leanings`); `voted_your_recognition` and `voted_your_censure` mark
  who voted a motion that named your house; `feasting_the_ayes` (*Recognised*), `a_round_stood` and `names_of_the_noes` (*The Motion
  Thrown Out*), `names_of_the_censure` (*The Censure Carries*, in place of the unread `grudge_list`), `promised_repeal` (*The
  Senate's Act Falls on a Friend*), `lending_where_it_matters` (*A Name in Commerce*) and `chancellor_asks_first` (*The
  Chancellor's Voice*) are each made what the words say (`VoteMarks.Answers`, `HouseholdSystem.BringUpstairs`); every house that
  depends on yours leans the way you vote; blood is as war for a favour.
- **Family** (`DynastySystem`, `CourtshipSystem`, `HouseholdSystem`): `born_to_the_house`, `schooling_begins` (a child of six),
  `kin_came_of_age` (sixteen), `grandchild_of_the_head`; `in_law` on the head of your spouse's family; `wedding_remembered` every
  tenth year of your marriage; `left_unpaid` on someone who leaves the household unpaid. In review: `chose_own_road` on one who comes
  of age bound for a service neither parent was in; `passed_over_at_the_will` on the grown children a will passed over
  (`WealthSystem.Inherit`); `HouseholdSystem.HardRefusal`, which the `takeIn` effect now obeys (the head of a house does not leave it,
  nor one married into another).
- **Chance**: no mark. Every person is bound by a condition the engine already has (`cohort`, `old_enemy`, wealth, holding,
  rank, age, `blood_on_their_hands`, the price of exotics against last year). Its take-in choices are the reason for `HardRefusal`
  above. Round 2: its strangers' role names are in `Binder.OutsideParts`, so nobody of your house is met as one.

## Every new report

### War (15)

- `out_of_the_line`: an officer of your house broke the line against orders and won the battle; name them, promote them, rebuke them or take the credit.
- `ran_from_the_battle`: an officer of your house pulled out of a line that then broke; the crews say it would have held.
- `a_laurel_at_the_peace`: at the peace, the officer who served you well waits to see whose name the house puts forward.
- `the_other_dispatch`: at the peace, the officer you wronged has written their own account of the battle, and the city reads it.
- `a_place_in_the_fleet`: a young relative asks for a place in the house's fleet while the war lasts.
- `blooded_in_battle`: the relative you sent to the front has come through their first battle.
- `wounded_aboard_the_fleet`: the house lost a battle, and the relative you sent to the front was wounded in it (added in round 2).
- `kin_home_from_the_war`: at the peace, the relative you sent comes home (added in review).
- `taken_in_battle`: a captain of the beaten house is pulled from the wrecks and is yours to ransom, exchange, parole or parade through your capital.
- `the_parole_broken`: the prisoner you paroled has been given a fleet against you in the same war.
- `deserters_from_the_fleet`: crews who deserted one of your fleets are taken at your ports (added in review).
- `a_parley_before_the_guns`: an enemy commander a year out asks to meet under a flag of truce before the fleets meet (was `a_parley_before_battle`).
- `plunder_on_the_exchange`: a house home rich from a sack beyond the rim sells its plunder and offers you a share of the next raid (was `the_spoils_of_a_sack`).
- `a_scandal_in_the_accounts`: the war with a nation is over and the auditors find your name in your raid partner's books.
- `the_fleet_paid_off`: the peace leaves the quays full of crews who want paying: the treasury, a war tax or nobody pays them ("The Crews Want Paying"; in round 2 it took in `who_pays_for_the_war`, which was a second card about the same bill).

### Votes (17)

- `a_vote_for_a_vote`: a head with a motion open offers their vote on your next motion for yours on theirs; both are favours owed, kept or broken at the count.
- `a_bought_vote_called_in`: the house that once paid for your vote moves again and expects it.
- `the_vote_still_undecided`: a head still undecided on your own motion before the count.
- `the_empty_seat_on_the_bench`: the leader of your bench saw your house sit out a vote.
- `a_feast_for_the_ayes`: your motion carried and a head who voted with you asks whose table the thanks will be at.
- `those_who_voted_against_you`: your motion carried and a head who voted against it has lost in front of the chamber.
- `an_old_grudge_against_your_motion`: a head with a grudge against yours speaks against your own motion (added in review).
- `an_old_grudge_on_the_docket`: a house with a grudge against yours moves a censure or embargo of you (set-piece).
- `the_senates_act_falls_on_a_friend`: a carried Act falls hard on a head who voted against it and writes to you.
- `a_purse_on_the_senate_floor`: your bought vote came out on the floor, and the head who took it waits to hear from you.
- `one_of_yours_stands_for_office`: a relative stands at the ladder's count and asks what you will do yourself.
- `a_smear_after_the_election`: the one your relative beat says the count was bought.
- `the_pamphlet_scandal`: a year of pamphlets between your house and the accuser's, and an offer to stop.
- `the_one_who_won_the_office`: the one who beat your relative at the count, and whether to ask for a recount for them.
- `a_riot_on_the_day_of_the_count`: trust in the magistrates is low and the squares riot at the count.
- `the_consul_elects_feast`: the new Senior Consul dines the Senate and everyone sees who comes.
- `your_colleague_the_consul`: you hold one consul's chair and the holder of the other asks to see you.

### Family (16)

- `the_naming_of_a_child`: a child of your marriage, and whose family the name is seen to come from.
- `a_grandchild_is_born`: a grandchild, once for each head, and what you say to the parent first.
- `the_other_family_asks_for_the_child`: your spouse's family asks to raise a child of six until sixteen.
- `the_fostered_child_comes_home`: the fostered child comes home at sixteen with another house's manners.
- `kin_left_behind`: one of your blood has what they wanted, and another who wanted as much has nothing yet.
- `kin_stand_together`: two you told to stand together do, when one of them fails.
- `the_wedding_remembered`: another ten years of your marriage, and whether the day is kept.
- `a_spouses_ambition`: your spouse has been passed over in their own service for years.
- `between_two_families`: your house is at war with the house your spouse was born to.
- `a_life_of_their_own`: a child of sixteen has chosen a service neither parent was in and wants to hear what you think.
- `you_were_wrong_about_them`: the child whose road you called wrong has what they wanted by it.
- `while_you_were_ill`: one of your people kept the house while you were ill and waits to hear if they did well.
- `they_ask_you_to_stand_aside`: the one who kept the house asks you, old and failing, to lay it down to them.
- `the_day_you_named`: you promised to stand aside on a day to come, and they say it has come.
- `the_reading_of_the_will`: the will made you head and a sibling passed over was in the room.
- `a_household_scandal`: someone who left your household unpaid is telling the city how your house treats its people.

### Chance (16)

- `a_classmate_in_the_city`: a cadet of your year at the school you went to comes over to you, older and ranked.
- `an_old_enemy_across_the_room`: an old enemy is at the same evening and the room waits to see who moves first.
- `words_at_the_feast`: a guest at another house's feast says something clever about your house, loud enough to carry.
- `the_quarrel_comes_to_a_head`: the guest from the feast has not let it rest: a gift, first blood, a killing, or let them talk.
- `a_veteran_of_the_old_wars`: an old officer long without a command talks at dinner of the wars and of your house's people in them.
- `a_painters_great_canvas`: a painter putting the whole Reach on a public wall asks where you will stand, and a rival wants the centre.
- `a_merchant_princes_venture`: a head of one of the richest houses asks you into a trading venture ("A Great Merchant's Venture").
- `the_venture_comes_home`: in a year exotics sell a tenth dearer, the venture pays back what you put in and half as much again.
- `the_venture_goes_down`: the venture's house is no longer among the richest or no longer at peace with yours, or exotics fall, and your stake is gone unless you chase it.
- `a_house_without_lands`: someone of a house that has lost its worlds comes to you in a coat worn thin.
- `a_favour_repaid`: someone who owes you a favour comes to pay it.
- `a_secret_for_sale`: a nameless seller offers proof of something another house's head has done.
- `the_secret_you_hold`: the one whose secret you kept has turned on your house (added in round 2).
- `a_word_off_the_record`: someone of a house at odds with yours asks to talk alone about how to end it.
- `a_patron_wanted`: a young person of a lesser house asks you to put your name behind them.
- `the_one_you_stood_for`: the young person you backed has reached their first rank and comes to thank you ("The One You Backed").
- `two_guests_at_odds`: at your feast, one guest is blamed for a death and another cannot abide them, and one place of honour is left.

## The threads

About a third of each set arms a flag that a later report reads, so a short thread of two or three steps can happen. Every
thread stays inside its own set: no flag that one set's choices raise is read by another set's reports, so the four can be
read and tuned apart. The threads that go out of a set go into reports that were already in the game.

**War** (twelve of fifteen in a thread)
1. The officer: `out_of_the_line` or `ran_from_the_battle` marks the officer `officer_owed` or `officer_wronged`; at your next
   peace or the end of a war with a nation that your house was in, `a_laurel_at_the_peace` or `the_other_dispatch`.
2. The young one: `a_place_in_the_fleet` sends a relative to the front (`at_the_front`); the next battle is `blooded_in_battle`
   (`blooded`; left with the fleet, they stay at the front); a battle lost may wound them, and `wounded_aboard_the_fleet` comes;
   at the peace, `kin_home_from_the_war`.
3. The prisoner: `taken_in_battle` may parole the captain (`on_parole`); if their house gives them a fleet in the same war,
   `the_parole_broken`.
4. The spoils: `plunder_on_the_exchange` may put your money into another house's raids (`war_partner`); when a war with a nation
   ends, `a_scandal_in_the_accounts`.

**Votes** (nine of seventeen)
1. The vote for a vote: a vote sold in `a_vote_for_a_vote` (`bought_your_vote`) is called in by `a_bought_vote_called_in` when
   the buyer moves again. A word given there, or a promise made in the second, is a favour owed (`VoteMarks.Bargains`), judged at
   the count.
2. The grudge: `those_who_voted_against_you`, `a_bought_vote_called_in` or `a_purse_on_the_senate_floor` can leave a head with
   `grudge_against_you`; when your house next moves a motion, `an_old_grudge_against_your_motion`, and when that house moves a
   censure or embargo of yours, `an_old_grudge_on_the_docket`.
3. The smear: `a_smear_after_the_election`, answered in kind, starts a year of pamphlets (`pamphlet_war`), which ends in
   `the_pamphlet_scandal`.
4. The consul's table: `the_consul_elects_feast` marks the consul `dined_with_you`, who once spares a motion of yours the veto;
   `your_colleague_the_consul` may divide the business (`divided_the_year`), and `the_veto_falls` may make the other consul a friend
   in the chair (`a_friend_in_the_chair`), who strikes the next motion against your house.

**Family** (nine of sixteen)
1. The fostered child: `the_other_family_asks_for_the_child` at six (`fostered`), then `the_fostered_child_comes_home` at sixteen.
2. The one left behind: `kin_left_behind` tells two of your blood to stand together (`stood_together`), then `kin_stand_together`
   when one of them fails; or the other is `slighted`, which the existing `the_slighted_kinsman` (`succession.json`) answers.
3. The road they chose: `a_life_of_their_own` may call it the wrong road (`doubted_by_the_head`), then `you_were_wrong_about_them`.
4. The one who kept the house: `while_you_were_ill` (`kept_the_house`), then `they_ask_you_to_stand_aside`, then, if you named
   a day (`promised_the_seat`), `the_day_you_named`. A refusal marks them `slighted` (to `the_slighted_kinsman`); so does a broken
   promise (it marked them `defiant`, which the family's own turn struck out before a report could read it).

**Chance** (twelve of seventeen)
1. The quarrel: `words_at_the_feast` (`quarrel_with_you`), then `the_quarrel_comes_to_a_head`. A killing there binds the dead
   person's house to a blood debt, and the existing feud reports (`feud.json`, docs/FEUD.md) follow.
2. A favour owed: `a_classmate_in_the_city` or `a_house_without_lands` (`owes_you_a_favour`), then `a_favour_repaid`.
3. The venture: `a_merchant_princes_venture` (`staked_by_you`), then `the_venture_comes_home` or `the_venture_goes_down`.
4. The patron: `a_patron_wanted` (`your_protege`), then `the_one_you_stood_for` when they reach their first rank.
5. The secret: `a_secret_for_sale` kept (`you_hold_their_secret`), then `the_secret_you_hold` when they turn on you.

**Where the sets meet.** They meet through the engine, not through flags. A head's regard for you is part of how their house
leans on your motions (at 20 or more, for; at -20 or less, against: `SenateSystem.RawLean`) and whom it votes for at an
election, so a chance meeting, a feast for the ayes or a favour forgiven moves votes. A regard that stays low for years
makes an old enemy (`LifeSystem`), whom `an_old_enemy_across_the_room` then binds. A war with the house your spouse was born to is
the moment of `between_two_families`. And a battle, an election or a birth in the same year can bring reports from two sets into one
briefing.

## Checks

`dotnet run --project src/Tools -c Release -- validate` (0 errors), `python3 validate_praetoria.py`, the four test files, and
the soak (`qa --scenario all --seeds 2`), whose "Reports fired by the file they come from" shows what the four files take of
all reports.

### Measured after round 1 of review (2026-11-27)

The soak, 11 starts by 2 seeds by 60 years with the randomised player and the three scripted players, passed with no failures
and no unresolved tokens. Against the baseline taken before the four sets (`aa2673a`):

- Reports fired: 7,843 (baseline 7,891), of 372 kinds out of 539 (baseline 323 of 475).
- The four new files took 990 reports, 12.6% of all: votes 386 (4.9%, 16 of 17 kinds), kin 282 (3.6%, 13 of 16), encounters
  190 (2.4%, 15 of 16), war_front 132 (1.7%, 10 of 15). Before review they took 13.1%; the consul's feast at a third of its weight
  and *A Life of Their Own* narrowed to the child who chose their own road are most of the difference.
- The files that gave up most: ladder 10.0% to 7.7%, senate 8.1% to 6.9% (the votes reports come on the same moments),
  courtship 5.6% to 4.5%, career_progression 5.4% to 4.4%, situations 5.0% to 4.0%, household 3.5% to 2.7%, found_family 4.0% to
  3.2%, echoes 3.9% to 3.6%.
- No new report is above 1.5% of all reports. The most fired are *The Wedding Remembered* (71, 0.9%), *A Vote for a Vote* (64,
  0.8%) and *The Naming of a Child* (64, 0.8%); the old most fired, `the_whip`, is at 2.8%.
- 54 of the 64 fired. The ten that did not are each the end of a thread whose first step the random player seldom takes or
  seldom reaches, and each is built and answered in its test: `ran_from_the_battle`, `blooded_in_battle`, `kin_home_from_the_war`
  and `the_other_dispatch` (a named officer or relative of yours in a battle the house fought, then for the last two a peace; the
  random player fights a battle about one year in 140), `the_parole_broken` (a prisoner taken, paroled, then given a fleet in the
  same war), `an_old_grudge_on_the_docket` (a grudge from this set and then a censure or embargo by that house; its common second
  step, `an_old_grudge_against_your_motion`, fired twice), `kin_stand_together`, `they_ask_you_to_stand_aside` and
  `the_day_you_named` (the second and third steps of family threads, which also did not fire, or fired once, in the family set's
  own soak), and `the_venture_comes_home` (a stake taken, then a year of high prices while the house is still opulent; its other
  ending, `the_venture_goes_down`, fired twice). In the review's first soak `blooded_in_battle`, `kin_home_from_the_war` and
  `the_other_dispatch` fired, and `ran_from_the_battle` and `deserters_from_the_fleet` did not: these are draws of the same rare moments.

### Measured after round 2 of review (2026-11-27)

The soak, 11 starts by 2 seeds by 60 years with the randomised player and the three scripted players, passed with no failures
and no unresolved tokens.

- Reports fired: 7,996 (baseline 7,891), of 378 kinds out of 540.
- The four new files took 993 reports, 12.4% of all: votes 394 (4.9%, 16 of 17 kinds), kin 305 (3.8%, 15 of 16), encounters 207
  (2.6%, 16 of 17), war_front 87 (1.1%, 8 of 15). The war set fell from 1.7%: its peace reports come only at a peace of the house's own
  with no war left, and its two cards about the crews' bill are one, once a peace.
- The ladder's file is at 8.1% (7.7% after round 1, 10.0% in the baseline), with the votes set's count-day reports at weight 20; the
  Senate's at 6.8% (6.9%).
- No new report is above 1.5% of all reports: the most fired are *A Vote for a Vote* (87, 1.1%) and *The Wedding Remembered* (75,
  0.9%); the old most fired, `the_whip`, is at 2.7%.
- 55 of the 65 fired. The ten that did not: seven war reports that need one of the house named in a battle, a relative at the front
  in a lost battle, an officer named and then a clean peace, or a desertion (`ran_from_the_battle`, `taken_in_battle`,
  `the_parole_broken`, `wounded_aboard_the_fleet`, `kin_home_from_the_war`, `a_laurel_at_the_peace`, `deserters_from_the_fleet`);
  `an_old_grudge_on_the_docket`, `the_day_you_named` and `the_venture_comes_home`, as before. The random player sends no fleet beyond the
  Reach, so the war set's new branches for a war there were not reached. Each is built and answered in its test.

### Round 2 of review

Three critics read the merged sets again, and the owner said "Make sure the vote promises actually work". The promises about votes
(a word given for a vote, a vote sold or called in, the bench's whip, a repeal promised, a favour that is "your house's voice") have
their own phase after this one, with its own brief, so there are not two designs; nothing here changes how they are kept. The rest:

- **War.** The young one's thread can reach its end (a relative left with the fleet stays at the front, can be wounded, and comes
  home); the peace reports read a peace of the house's own (`our_nation_war_over`) with no war left; `at_war_any` follows a war with a
  nation; a battle beyond the Reach names the house's officers; the two cards about the crews' bill are one, once a peace, and the
  freed place is *Wounded Aboard the Fleet*; the auditor's share has a price; labels say money kept as money kept.
- **Votes.** The undecided doubter and the old grudge bind only those the choices and the lean can move; no answer of the ayes'
  feast is dominated; the colleague consul comes in the year of the count; a candidacy is one report; the ladder's count pairs the
  victor and the loser of one rung; the count-day reports are at 20.
- **Family.** *A Family Divided* binds the house the spouse was born to (`in_law`); the grandchild's visit and the wedding feast trade
  something real; a plain "not yet" for a head asked to stand aside.
- **Chance.** Nobody of your house (struck from the line or not) and nobody of a nation's house is met as a stranger; a secret kept
  is held over them, with a second step; the venture settles on the price of exotics, its two endings exclusive, and every credit it
  pays comes out of the grandee's house; a classmate gives what only a classmate can.
- Words across all four, as the word critic marked them.

### Round 1 of review

Three critics (the words, the code, the play) read the merged sets. What changed, in short; each design doc has a "Round 1"
section and the detail at each report.

- **Every vote promise listed.** docs/events/votes.md, "Promises", lists every promise about a vote the reports, notices and windows
  make (five groups, a closing round and round 2), what in the engine keeps it, and the test in tests/VotePromisesTests.cs,
  tests/VotePromisesClosingTests.cs or tests/VotePromisesRound2Tests.cs that proves it through the real count; each hook was taken
  out and its test failed.
- **Promises made real.** A vote given for a vote, and a promise to vote for a motion, are favours owed that the count judges
  (`VoteMarks.Bargains`); a consul who dined with you spares a motion of yours the veto; the parley's counted ships fight the worse;
  the young one sent to the front can be wounded and comes home; the recount is for your relative who lost.
- **Threads that could not end now can.** The grudge has a common second step; the broken promise of the seat leads to a flag the
  engine keeps; the peace reports come at the end of a war with a nation, and see two briefings; the war's people are struck out
  once the peace is old news; the venture settles in a year the market makes news.
- **The binding names the right person.** Officers by the battle's result; the will's sibling by this reading; the helper who
  thinks well of the fallen; the old enemy and the one who stands for office by house; a child of sixteen on their own road, so one
  child has one coming-of-age card.
- **No more taking the head of another house into yours** (`HouseholdSystem.HardRefusal`, obeyed by `takeIn`), and no prisoner of
  war leads a levy or steps up to a post.
- **What a choice shows.** Its chips name the estates it moves, the magistrates' trust, and a killing; labels are actions and say
  their estate; money another house pays leaves that house's treasury; influence from nowhere is gone.
- **Three new reports:** `kin_home_from_the_war`, `deserters_from_the_fleet` (on `fleet_deserts`, which nothing read) and
  `an_old_grudge_against_your_motion`. Two renamed so they open on the right painting: `a_parley_before_the_guns` and
  `plunder_on_the_exchange`.
- **The Senate's floor under the Imperium.** Its ten reports are no longer gated to the Republic; the ladder's and the consuls' are.
- Not done, and why: `kin_left_behind` and `you_were_wrong_about_them` still read the same success as `what_they_wanted`, since a
  briefing is drawn at once and no gate can keep them apart, and the three ask different things; `a_household_scandal` keeps its
  id, whose "scandal" opens it on the intrigue painting, which suits a servant telling the city; no report was added on
  `supply_low` (the state before `out_of_supply`, which *The Long Supply Line* answers with the same choices), `campaign_open` (a
  lasting state the campaign reports bracket) or `laurels_earned` (the Caesar road's).
