# The four sets of new events: war, votes, family and chance

The owner asked: "Let's make some events for war, voting, family events, and random interactions." Four authors wrote one
set each, from one brief, and the four were merged on the branch `ev-build`. That is 61 new reports, all of them news of
something that happened in the game: a battle, a peace, a count of votes, a birth, an illness, an evening where you met
someone. Each names the people in it by role and asks you to decide something that changes what you care about.

| Set | What it is about | Reports | Events and text | Tests | Design doc |
|---|---|---|---|---|---|
| War | The people a war is fought by: officers who dared or ran, a young relative who wants to go, a prisoner, an enemy who asks to talk, a sack beyond the rim, and what the peace asks of you | 13 | `content/events/war_front.json`, `content/text/war_front.json` | `tests/WarEventsTests.cs` | [docs/events/war.md](events/war.md) |
| Votes | The Senate's votes and the elections, before, during and after. Republic only | 16 | `content/events/votes.json`, `content/text/votes.json` | `tests/VoteEventsTests.cs` | [docs/events/votes.md](events/votes.md) |
| Family | The life of your own family, each of them a person with a mind of their own: births, children, the spouse, illness, old age, the will | 16 | `content/events/kin.json`, `content/text/kin.json` | `tests/KinEventsTests.cs` | [docs/events/family.md](events/family.md) |
| Chance | "Random interactions": the people of the Reach crossing your path, bound by who they are, at a low weight and repeatable | 16 | `content/events/encounters.json`, `content/text/encounters.json` | `tests/EncounterEventsTests.cs` | [docs/events/chance.md](events/chance.md) |

Each design doc says, for every report, when it fires, what each choice does, every number and why, what was cut, and what
was added to Core. This page is the index.

## No new verbs

None of the four sets added a condition or an effect verb. Everything is said with the verbs already in
`docs/CONTENT_GUIDE.md` and the parsers (`src/Core/Data/ConditionParser.cs`, `EffectParser.cs`), so the guide needs no change.

## What was added to Core

Small marks, so that a report can find the person in the news. Each is a flag cleared after a year or at the next moment of its
kind, in the pattern of the existing `Mark()` moments.

- **War** (`FleetSystem`, and one line each in `NationSystem.War.cs`, `NationSystem.Map.cs`, `NationSystem.cs`, `ServiceSystem.cs`,
  `NpcAi.cs`): `in_the_battle` on your house's officers in a battle; `taken_prisoner`, `held_to_the_peace` and `on_parole` on a
  captain taken from a beaten line (a prisoner may not command); `enemy_bears_down` and `bears_down_on_us` when an enemy fleet will
  reach yours next year; `our_peace` when your house makes peace; `sacked_beyond` on the head of a house whose fleet sacks a nation's
  world; `nation_war_over` when any war with a nation ends.
- **Votes** (new `src/Core/Systems/VoteMarks.cs`, called from `SenateSystem`, `ElectionSystem` and `LadderSystem`): who moves a
  motion, who moves against you, who is undecided on yours, who voted each way on yours, the bench leader who saw your abstention,
  the head who took your purse, the houses an Act went against, who beat yours and who yours beat at the ladder's count.
  `bribe_out` is now cleared after a session, and `election_held` is marked.
- **Family** (`DynastySystem`, `CourtshipSystem`, `HouseholdSystem`): `born_to_the_house`, `schooling_begins` (a child of six),
  `kin_came_of_age` (sixteen), `grandchild_of_the_head`; `in_law` on the head of your spouse's family; `wedding_remembered` every
  tenth year of your marriage; `left_unpaid` on someone who leaves the household unpaid.
- **Chance**: nothing. Every person is bound by a condition the engine already has (`cohort`, `old_enemy`, wealth, holding,
  rank, age, `blood_on_their_hands`).

## Every new report

### War (13)

- `out_of_the_line`: an officer of your house broke the line against orders and won the battle; name them, promote them, rebuke them or take the credit.
- `ran_from_the_battle`: an officer of your house pulled out of a line that then broke; the crews say it would have held.
- `a_laurel_at_the_peace`: at the peace, the officer who served you well waits to see whose name the house puts forward.
- `the_other_dispatch`: at the peace, the officer you wronged has written their own account of the battle, and the city reads it.
- `a_place_in_the_fleet`: a young relative asks for a place in the house's fleet while the war lasts.
- `blooded_in_battle`: the relative you sent to the front has come through their first battle.
- `taken_in_battle`: a captain of the beaten house is pulled from the wrecks and is yours to ransom, exchange, parole or parade through your capital.
- `the_parole_broken`: the prisoner you paroled has been given a fleet against you in the same war.
- `a_parley_before_battle`: an enemy commander a year out asks to meet before the guns.
- `the_spoils_of_a_sack`: a house home rich from a sack beyond the rim sells its plunder and offers you a share of the next raid.
- `a_scandal_in_the_accounts`: the war with a nation is over and the auditors find your name in your raid partner's books.
- `the_fleet_paid_off`: the peace leaves the quays full of crews who want paying and something to do.
- `who_pays_for_the_war`: the peace finds the treasury nearly empty; your worlds, the merchants or the house pay for it.

### Votes (16)

- `a_vote_for_a_vote`: a head with a motion open offers their vote on your next motion for yours on theirs.
- `a_bought_vote_called_in`: the house that once paid for your vote moves again and expects it.
- `the_vote_still_undecided`: a head still undecided on your own motion before the count.
- `the_empty_seat_on_the_bench`: the leader of your bench saw your house sit out a vote.
- `a_feast_for_the_ayes`: your motion carried and a head who voted with you asks whose table the thanks will be at.
- `those_who_voted_against_you`: your motion carried and a head who voted against it has lost in front of the chamber.
- `an_old_grudge_on_the_docket`: a house with a grudge against yours moves a censure or embargo of you (set-piece).
- `the_senates_act_falls_on_a_friend`: a carried Act falls hard on a head who voted against it and writes to you.
- `a_purse_on_the_senate_floor`: your bought vote came out on the floor, and the head who took it waits to hear from you.
- `one_of_yours_stands_for_office`: a relative stands at the ladder's count and asks what you will do yourself.
- `a_smear_after_the_election`: the one your relative beat says the count was bought.
- `the_pamphlet_scandal`: a year of pamphlets between your house and the accuser's, and an offer to stop.
- `the_one_who_won_the_office`: the one who beat your relative at the count, and whether to ask for a recount.
- `a_riot_on_the_day_of_the_count`: trust in the magistrates is low and the squares riot at the count.
- `the_consul_elects_feast`: the new Senior Consul dines the Senate and everyone sees who comes.
- `your_colleague_the_consul`: you hold one consul's chair and the holder of the other asks to see you.

### Family (16)

- `the_naming_of_a_child`: the first child of your marriage, and whose family the name is seen to come from.
- `a_grandchild_is_born`: your first grandchild, and what you say to the parent first.
- `the_other_family_asks_for_the_child`: your spouse's family asks to raise a child of six until sixteen.
- `the_fostered_child_comes_home`: the fostered child comes home at sixteen with another house's manners.
- `kin_left_behind`: one of your blood has what they wanted, and another who wanted as much has nothing yet.
- `kin_stand_together`: two you told to stand together do, when one of them fails.
- `the_wedding_remembered`: another ten years of your marriage, and whether the day is kept.
- `a_spouses_ambition`: your spouse has been passed over in their own service for years.
- `between_two_families`: your house is at war with the house your spouse was born to.
- `a_life_of_their_own`: a child of sixteen has chosen their own service and wants to hear what you think.
- `you_were_wrong_about_them`: the child whose road you called wrong has what they wanted by it.
- `while_you_were_ill`: one of your people kept the house while you were ill and waits to hear if they did well.
- `they_ask_you_to_stand_aside`: the one who kept the house asks you, old and failing, to lay it down to them.
- `the_day_you_named`: you promised to stand aside on a day to come, and they say it has come.
- `the_reading_of_the_will`: the will made you head and a sibling passed over was in the room.
- `a_household_scandal`: someone who left your household unpaid is telling the city how your house treats its people.

### Chance (16)

- `a_classmate_in_the_city`: a classmate of your year at the Academy (the Institute, under the Imperium) comes over to you, older and ranked.
- `an_old_enemy_across_the_room`: an old enemy is at the same evening and the room waits to see who moves first.
- `words_at_the_feast`: a guest at another house's feast says something clever about your house, loud enough to carry.
- `the_quarrel_comes_to_a_head`: the guest from the feast has not let it rest: a gift, first blood, a killing, or let them talk.
- `a_veteran_of_the_old_wars`: an old officer long without a command talks at dinner of the wars and of your house's people in them.
- `a_painters_great_canvas`: a painter putting the whole Reach on a public wall asks where you will stand, and a rival wants the centre.
- `a_merchant_princes_venture`: a head of one of the richest houses asks you into a trading venture.
- `the_venture_comes_home`: the venture pays back what you put in and half as much again.
- `the_venture_goes_down`: the venture's house is no longer rich or no longer at peace with yours, and your stake is gone unless you chase it.
- `a_house_without_lands`: someone of a house that has lost its worlds comes to you in a coat worn thin.
- `a_favour_repaid`: someone who owes you a favour comes to pay it.
- `a_secret_for_sale`: a nameless seller offers proof of something another house's head has done.
- `a_word_off_the_record`: someone of a house at odds with yours asks to talk alone about how to end it.
- `a_patron_wanted`: a young person of a lesser house asks you to put your name behind them.
- `the_one_you_stood_for`: the young person you stood for has reached their first rank and comes to thank you.
- `two_guests_at_odds`: two guests at your dinner have blood between their houses, and one place of honour is left.

## The threads

About a third of each set arms a flag that a later report reads, so a short thread of two or three steps can happen. Every
thread stays inside its own set: no flag that one set's choices raise is read by another set's reports, so the four can be
read and tuned apart. The threads that go out of a set go into reports that were already in the game.

**War** (ten of thirteen in a thread)
1. The officer: `out_of_the_line` or `ran_from_the_battle` marks the officer `officer_owed` or `officer_wronged`; at your next
   peace, `a_laurel_at_the_peace` or `the_other_dispatch`.
2. The young one: `a_place_in_the_fleet` sends a relative to the front (`at_the_front`); the next battle is `blooded_in_battle`.
3. The prisoner: `taken_in_battle` may parole the captain (`on_parole`); if their house gives them a fleet in the same war,
   `the_parole_broken`.
4. The spoils: `the_spoils_of_a_sack` may put your money into another house's raids (`war_partner`); when a war with a nation
   ends, `a_scandal_in_the_accounts`.

**Votes** (seven of sixteen)
1. The vote for a vote: a vote sold in `a_vote_for_a_vote` (`bought_your_vote`) is called in by `a_bought_vote_called_in` when
   the buyer moves again.
2. The grudge: `those_who_voted_against_you`, `a_bought_vote_called_in` or `a_purse_on_the_senate_floor` can leave a head with
   `grudge_against_you`; when that house moves a censure or embargo of yours, `an_old_grudge_on_the_docket`.
3. The smear: `a_smear_after_the_election`, answered in kind, starts a year of pamphlets (`pamphlet_war`), which ends in
   `the_pamphlet_scandal`.

**Family** (nine of sixteen)
1. The fostered child: `the_other_family_asks_for_the_child` at six (`fostered`), then `the_fostered_child_comes_home` at sixteen.
2. The one left behind: `kin_left_behind` tells two of your blood to stand together (`stood_together`), then `kin_stand_together`
   when one of them fails; or the other is `slighted`, which the existing `the_slighted_kinsman` (`succession.json`) answers.
3. The road they chose: `a_life_of_their_own` may call it the wrong road (`doubted_by_the_head`), then `you_were_wrong_about_them`.
4. The one who kept the house: `while_you_were_ill` (`kept_the_house`), then `they_ask_you_to_stand_aside`, then, if you named
   a day (`promised_the_seat`), `the_day_you_named`. A refusal marks them `slighted` (to `the_slighted_kinsman`); a broken promise
   marks them `defiant`, which the existing `the_defiant_kin` (`household.json`) answers.

**Chance** (ten of sixteen)
1. The quarrel: `words_at_the_feast` (`quarrel_with_you`), then `the_quarrel_comes_to_a_head`. A killing there binds the dead
   person's house to a blood debt, and the existing feud reports (`feud.json`, docs/FEUD.md) follow.
2. A favour owed: `a_classmate_in_the_city` or `a_house_without_lands` (`owes_you_a_favour`), then `a_favour_repaid`.
3. The venture: `a_merchant_princes_venture` (`staked_by_you`), then `the_venture_comes_home` or `the_venture_goes_down`.
4. The patron: `a_patron_wanted` (`your_protege`), then `the_one_you_stood_for` when they reach their first rank.

**Where the sets meet.** They meet through the engine, not through flags. A head's regard for you is part of how their house
leans on your motions (at 20 or more, for; at -20 or less, against: `SenateSystem.RawLean`) and whom it votes for at an
election, so a chance meeting, a feast for the ayes or a favour forgiven moves votes. A regard that stays low for years
makes an old enemy (`LifeSystem`), whom `an_old_enemy_across_the_room` then binds. A war with your spouse's house is the moment
of `between_two_families`. And a battle, an election or a birth in the same year can bring reports from two sets into one
briefing.

## Checks

`dotnet run --project src/Tools -c Release -- validate` (0 errors), `python3 validate_praetoria.py`, the four test files, and
the soak (`qa --scenario all --seeds 2`), whose "Reports fired by the file they come from" shows what the four files take of
all reports.

### Measured on the merged build (2026-11-27)

The soak, 11 starts by 2 seeds by 60 years with the randomised player and the three scripted players, passed with no failures
and no unresolved tokens. Against the baseline taken before the four sets (`aa2673a`):

- Reports fired: 8,080 (baseline 7,891), of 384 kinds out of 536 (baseline 323 of 475).
- The four new files took 1,062 reports, 13.1% of all: votes 429 (5.3%, 15 of 16 kinds), kin 313 (3.9%, 16 of 16),
  encounters 216 (2.7%, 14 of 16), war_front 104 (1.3%, 12 of 13).
- The files that gave up most: ladder 10.0% to 7.9%, senate 8.1% to 6.7% (the votes reports come on the same moments),
  household 3.5% to 2.5%, career_progression 5.4% to 4.5%, courtship 5.6% to 4.7%, echoes 3.9% to 3.1%.
- No new report is above 1.5% of all reports. The most fired is `the_consul_elects_feast`, 105 (1.3%); the old most fired,
  `the_whip`, went from 3.4% to 2.7%.
- 57 of the 61 fired. Four did not, each a second step of a thread the random player seldom arms, and each covered by its
  test: `the_venture_comes_home` and `the_venture_goes_down` (the stake of 800 credits needs 1,000 in the treasury), `an_old_grudge_on_the_docket`
  (a house with a grudge from this set must later censure or embargo you), `the_other_dispatch` (a wronged officer, then a peace
  your house makes). Twelve more fired only once or twice, mostly the war reports, which need a named officer or relative of
  yours in a battle.
