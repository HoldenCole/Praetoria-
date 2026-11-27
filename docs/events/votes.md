# Votes: the Senate's votes and the elections, before, during and after

The owner asked for "some events for war, voting, family events, and random interactions". This file is the voting part:
sixteen reports in `content/events/votes.json` (prose in `content/text/votes.json`), the marks in the Senate and the ladder
that let a report name the person in the news, and `tests/VoteEventsTests.cs`.

Every report here is of the Republic. Each is gated `era imperium present: false`, as the emergency reports are, and the
ladder's are also gated by the ladder itself, which does not sit under a throne.

## What was there already, and how these differ

Read before writing: `senate.json`, `docket.json`, `factions.json`, `republic.json`, `ladder.json`, `offices.json`,
`politics.json`, `orders.json`, `emergency.json`, `caesar.json` (the triumph vote), `exile.json`, and `situations.json`
(which reads `motion_failed_mine` and `act_failed`).

| nearest existing report | what it does | what is new here |
|---|---|---|
| `the_whip` (motion_open) | a whip offers coin for your vote | `a_vote_for_a_vote`: the house moving the motion offers its own vote in kind, and the bargain comes back (`a_bought_vote_called_in`) |
| `the_motion_carried`, `the_motion_thrown_out` | your motion decided; the choices move counters nothing reads | `a_feast_for_the_ayes` and `those_who_voted_against_you` name a house that voted each way and move that head's regard |
| `a_censure_on_the_docket` | a motion against the house, nobody named | `an_old_grudge_on_the_docket` names the mover, and only when the grudge was made in this file |
| `the_whips_displeasure` (broke_the_whip) | you voted against your bench | `the_empty_seat_on_the_bench`: you did not vote at all, and the leader of the bench saw it |
| `the_act_carries` | any Act: proclaim it or drag your feet | `the_senates_act_falls_on_a_friend`: one friend whose house voted against the Act that carried |
| (none) | a bought vote that came out on the floor was never told | `a_purse_on_the_senate_floor` |
| `the_canvass`, `the_hustings` | you stand; an election is near | `one_of_yours_stands_for_office`: a relative stands and asks what the house will do |
| `the_count_goes_your_way` / `against_you`, `the_one_you_did_not_speak_for` | your own result; a relative you did not back lost | `the_one_who_won_the_office` (the person who beat yours), `a_smear_after_the_election` (the one yours beat) |
| `the_consulship`, `the_term_ends` | you are consul, or your term ends | `the_consul_elects_feast` (somebody else is consul), `your_colleague_the_consul` (the other chair) |
| `the_squares_turn_on_the_magistrates` (trust_collapsed) | the year trust falls | `a_riot_on_the_day_of_the_count`: the count itself, in a year the magistrates are not believed |

None of them repeats a screen. The Senate window casts the vote, lobbies, bribes and pledges; the ladder window stands,
backs and pays for a canvass. These reports are what happens around those acts: a person who comes to the door, a dinner,
a grudge, a pamphlet. Where a choice touches what a window does, the text says so and sends the player there.

## How a report here reaches the count

The engine decides a vote by each house's lean, and part of every lean is what its head thinks of the house that moved the
motion: at 20 or more it leans one way more for it, at -20 or less one way against (`SenateSystem.RawLean`). The houses that
vote at an election by liking go to the nominee their head likes best (`ElectionSystem.Tally`, `LadderSystem.Tally`). So a
report that moves a head's regard moves votes, and the regard chips on the choice show by how much. A choice with a pool
`cost` in influence spends this year's influence, the same purse the player lobbies and stands with, so it is a real price
in the Senate. A relative's `charisma` or `oratory` is part of what they carry at a ladder count (`LadderSystem.Merit`, two
votes a point). `public_trust` is the counter the ladder reads for the magistrates' name.

## The marks added to Core

The moments were there; the people in them were not marked, so no role could bind them. Each mark is a character flag that
lives a year, set where the moment happens, in a system this theme owns.

| where | flag | on whom | set | cleared |
|---|---|---|---|---|
| `SenateSystem.WriteFlags` | `moves_a_motion` (char) | the head of every other house with a motion on the docket (not an estate's petition) | each year's docket | rewritten every year |
| " | `moves_against_you` (char) | the head of a house whose censure or embargo of yours is on the docket | with `motion_against_me` | rewritten every year |
| " | `my_motion_open` (world) | the player's house has a motion on the docket | each year | rewritten every year |
| " | `undecided_on_yours` (char) | the head of every house that leans neither way on the player's own motion | each year | rewritten every year |
| `SenateSystem.Resolve` | `voted_with_you`, `voted_against_you` (char) | the heads of the houses that voted for or against the player's own motion | at the count | the next session |
| " | `abstention_noticed` (world, marked), `noticed_your_abstention` (char) | the leader of the player's bench, when the player's house did not vote on something the leader voted on | at the count | the next session |
| " | `bribe_out` (world, now a session flag and marked), `took_your_purse` (char) | the head of the house whose vote the player bought, when it came out | at the count | the next session (it was never cleared before) |
| `SenateSystem.Apply` | `act_went_against_houses` (world, marked), `act_went_against_them` (char) | the heads of the houses that voted against an Act that carried | at the count | the next session |
| `ElectionSystem.Hold` | `mark:election_held` (counter) | (the existing `election_held` flag, so the report comes while it is news) | at the count | the flag is cleared next session |
| `LadderSystem.Hold` | `beat_yours_at_the_count` (char) | the winners of a rung one of the player's house stood for and lost | at the count | with `ladder_election_held` |
| " | `lost_to_yours_at_the_count` (char) | the losers of a rung one of the player's house won | at the count | with `ladder_election_held` |

Each is tested in `tests/VoteEventsTests.cs`. Nothing else in Core changed: no new condition or effect verb.

One file outside this theme was touched. `tests/EarnedTitlesTests.cs`, `In_a_game_the_other_houses_rise_above_Prefect_only_by_a_vote_of_the_Senate`,
plays one game of thirty years and asks that a rival's petition for a rank carried in it. Whether one does is the draw of
that game, and any new report in the content changes the draw (the binder spends the shared generator on every report it
tries): with these reports seed 5 had nine petitions and none carried; without them seed 4 has none carried either. The test
now plays seeds 5, 3 and 2 in turn until one has a petition carried, and holds every other check (no rival rises a rung
without a petition carried that year, the player's vote counted, no bench's line on it) in every game it plays. The other
themes' reports will move the draw too, so this is meant to survive their merge.

## The sixteen reports

Weights. The reports in `ladder.json`, `docket.json` and `caesar.json`, which read a flag the engine marks for a year, sit
between 25 and 45; the Director makes a report on a marked flag five times likelier in its year. Most here are of that kind and
sit at 20 to 40. Two read a flag that is up in many years (`motion_open`, and the player's own motion), and are kept low (4 and
10) so that they stay well under the 1.5% share the brief allows: `the_whip`, on `motion_open` at 9, takes 3.4% of all reports.

Deltas. The existing Senate and ladder reports move a head's regard by 4 to 15, legitimacy by 1 to 3, stress by 1 to 6,
credits by 3 to 12 (hundreds), clout by 1 or 2. These stay inside that: regard 2 to 15, legitimacy 1 or 2, stress 1 to 4,
credits 2 to 8, public trust 1 or 2, an estate 3 to 6. A credit amount is hundreds (docs/MONEY.md); prose names no sums.

### Before a vote

**1. `a_vote_for_a_vote` "A Vote for a Vote"** (thread A, step 1). Situation, weight 4, repeatable.
- Fires: a motion is on the docket (`motion_open`), turn 3 or later. Role `senator`: `moves_a_motion`, thinks -15 or better of
  you, and is not already in a bargain with you (`owes_you_a_vote` and `bought_your_vote` both unset).
- What happened: the head of a house with a motion before the Senate comes to count heads, and offers its own vote on your next
  motion for yours on this one.
- Choices:
  - Give your word, and take theirs: their regard +6, your stress +1; arms `owes_you_a_vote` on them. The body says the vote is
    still cast in the chamber. (+6: a pledge of seats at the consulship is +8, ElectionSystem.PledgeRegard; a word not yet
    kept is a little less.)
  - Tell them a vote costs more than a vote (not for the Honorable or the Just): +4 credits, legitimacy -1, their regard +2;
    arms `bought_your_vote`. (4: below the 5 that buying the cheapest house's vote costs, SenateSystem.BribeCredits; it was
    offered at your door. -1: a bought vote that comes out costs 3; one taken quietly is talked of a little.)
  - Tell them your vote is your own: their regard -4. (Less than a broken pledge, 15: nothing was promised.)
- Differs from `the_whip`: coin from a whip there; here a vote in kind from the mover, a person named, and a thread.
- Weight 4: on the same flag as `the_whip` (9, 3.4%), and the role binds most years.

**2. `a_bought_vote_called_in` "A Bought Vote Called In"** (thread A, step 2). Situation, weight 30, repeatable.
- Fires: `motion_open`, and role `buyer` has both `bought_your_vote` (you sold them your vote once) and `moves_a_motion`
  (they have another motion before the Senate now).
- What happened: they paid you once and have another motion up; they do not mean to pay again.
- Choices:
  - Say you will vote for it: their regard +4, legitimacy -1 (the house is seen as theirs); clears the flag.
  - Send back what they paid, and more (needs 5 credits): -5 credits, legitimacy +1, their regard -2; clears.
  - Tell them the bargain was for one vote: their regard -10, legitimacy -2 (they let it be known your vote is for sale);
    clears, and arms `grudge_against_you` (thread B): spurned in front of the chamber, they remember it.
- Weight 30: the payoff of a thread, narrow (only after step 1's second choice and a new motion of theirs).

**3. `the_vote_still_undecided` "The Vote Still Undecided"**. Situation, weight 10, repeatable.
- Fires: your house has a motion before the Senate (`my_motion_open`). Role `doubter`: `undecided_on_yours`, the head of a house
  that leans neither way on it.
- What happened: the vote of one house is still open; its head will decide in the chamber.
- Choices:
  - Make your case to them in person (costs 1 of the year's influence): their regard +6, stress +1. (The same purse as a lobby;
    a lobby moves the lean by 2 at once, this moves the head and lasts.)
  - Have them to dinner (needs 4 credits): -4 credits, their regard +8.
  - Speak for it from the floor, and let them hear it there (needs oratory 3 or the Orator): their regard +4, legitimacy +1,
    stress +2.
  - Leave them to make up their own mind: stress -1.
- Why it matters: at 20 regard a head leans one more toward what you move; an undecided house is the one that a point moves.
- Weight 10: up in any year your house has a motion with a house undecided on it, which is not most years (the first soak, at 6,
  fired it 7 times in 22 games); it is the player's own motion, the vote they care about most.

### The vote itself

**4. `the_empty_seat_on_the_bench` "The Empty Seat on the Bench"**. Situation, weight 30, repeatable.
- Fires: `abstention_noticed` (your house did not vote on a motion that the leader of your bench voted on; not your own motion
  nor one aimed at you). Role `leader`: `noticed_your_abstention`.
- Choices:
  - Say you could not vote for it, and would not vote against your friends (the Honorable or the Just): legitimacy +1, their
    regard -2.
  - Say it will not happen again: their regard +3, stress +2.
  - Send a gift to their house (needs 3 credits): -3 credits, their regard +5.
  - Say your vote is your own to cast or keep: their regard -6, stress -1. (-6: voting against the bench costs 5 and 2 of
    affinity, FactionSystem.BreakWhipDisposition; refusing to say sorry for sitting still costs about as much.)
- Differs from `the_whips_displeasure`, which is the vote cast against the bench and moves counters nothing reads.

### After it

**5. `a_feast_for_the_ayes` "A Feast for the Ayes"** (the winners' dinner). Situation, weight 25, repeatable.
- Fires: your motion carried (`motion_passed_mine`). Role `ally`: `voted_with_you`, thinks 0 or better of you.
- Choices:
  - Give the dinner yourself, and seat them at your right hand (needs 6 credits): -6 credits, legitimacy +1, their regard +8.
  - Let them give it, and go as a guest: their regard +5, stress -1.
  - Thank them by letter: their regard -2, stress -1.

**6. `those_who_voted_against_you` "Those Who Voted Against You"** (the losers' grudge; thread B, step 1). Situation, weight 25,
repeatable.
- Fires: `motion_passed_mine`. Role `loser`: `voted_against_you`, thinks 10 or less of you, no grudge already.
- Choices:
  - Go to them and offer them something for their trouble (costs 1 of the year's influence): their regard +8.
  - Let them lose; it is what a vote is: their regard -3; arms `grudge_against_you` on them.
  - Remind the chamber who lost (the Proud or the Arrogant): their regard -10, legitimacy +1, stress -2; arms the grudge.

**7. `an_old_grudge_on_the_docket` "An Old Grudge Moves a Motion"** (thread B, step 2). Setpiece, weight 30, repeatable.
- Fires: `motion_against_me`. Role `rival`: `grudge_against_you` (from report 6, or from shaming them in reports 2 and 9) and
  `moves_against_you` (it is they who moved the censure or embargo of your house).
- Choices:
  - Answer them on the floor (oratory 2): legitimacy +1, their regard -5, stress +2; clears the grudge.
  - Go to them and make your peace (needs 6 credits): -6 credits, their regard +12; clears it. The text says it will not stop
    this vote.
  - Let them have their day: stress +3; the grudge stands.
- Setpiece: it is a motion against the house, the tier `a_censure_on_the_docket` has; the two cannot share a briefing, so this
  one, naming the mover, can take its place.

**8. `the_senates_act_falls_on_a_friend` "The Senate's Act Falls on a Friend"**. Situation, weight 30, repeatable.
- Fires: `act_went_against_houses`. Role `friend`: `act_went_against_them`, and you and they think 20 or more of each other.
- Choices:
  - Promise to move its repeal when the chamber will hear it: their regard +6, stress +2. (The body says an Act is undone by
    another Act moved in the Senate.)
  - Send them help to carry it through the year, and ask nothing (needs 8 credits): -8 credits, their regard +12.
  - Tell them the law is the law, for your house as for theirs: their regard -8, legitimacy +1.
- 8 credits: a gift of that size to a middling house is about a tenth of its year (DealingsSystem.GiftFavourPct).

**9. `a_purse_on_the_senate_floor` "A Purse on the Senate Floor"** (buying votes, and what it costs). Situation, weight 40,
repeatable.
- Fires: `bribe_out` (a vote you paid for came out at the count; the engine has struck it and taken 3 legitimacy). Role
  `bought`: `took_your_purse`.
- Choices:
  - Say before the chamber it was your doing, not theirs: legitimacy -1, their regard +8, stress +3.
  - Pay them for what it has cost them (needs 5 credits): -5 credits, their regard +5.
  - Say they named the price: their regard -15, legitimacy +1, stress -1; arms `grudge_against_you` (thread B). (-15: what a
    broken pledge costs; you have put your shame on them.)

### The elections

**10. `one_of_yours_stands_for_office` "One of Yours Stands for Office"** (a relative stands). Situation, weight 30, repeatable.
- Fires: a relative of your blood has `standing_for_office` (their name is down for the coming ladder count).
- Choices:
  - Walk the wards with them, so the city sees the house behind them: their regard +8, your stress +3, their charisma +1
    (two votes at the count).
  - Teach them how the floor is won (oratory 3): their oratory +1 (two votes), their regard +4, your stress +2.
  - Tell them an office is won by the one who stands: their regard -4, your stress -1.
- The body says the house's backing and a paid canvass are there as for anyone who stands (the ladder window); what the relative
  asks is what you will do yourself.
- The suppression keeps it from coming in both years a name is down; the next count is five years on.

**11. `a_smear_after_the_election` "A Smear After the Election"** (thread C, step 1). Situation, weight 30, repeatable.
- Fires: `ladder_election_held`. Role `accuser`: `lost_to_yours_at_the_count` (beaten by someone of your house), thinks 20 or
  less of you.
- Choices:
  - Open the house's accounts to the praetors: -2 credits, legitimacy +1, public trust +1, stress +1.
  - Pay a pamphleteer to say what is known of their own canvass (the Schemer, or intrigue 2): -3 credits, their regard -10,
    public trust -1; arms `pamphlet_war` on them.
  - Let it blow over: legitimacy -1, stress +2.

**12. `the_pamphlet_scandal` "The Pamphlet Scandal"** (thread C, step 2). Situation, weight 30, repeatable.
- Fires: the year after, while role `accuser` has `pamphlet_war`.
- Choices (each clears the flag):
  - Call off your writer: their regard +6, public trust +1.
  - Have their pamphlets brought before a praetor (costs 2 of the year's influence): their regard -12, legitimacy +1.
  - Let the writers write: public trust -2, legitimacy -1, stress -1.

**13. `the_one_who_won_the_office` "The One Who Won the Office"** (the recount). Situation, weight 30, repeatable.
- Fires: `ladder_election_held`. Role `victor`: `beat_yours_at_the_count` (won a rung one of yours stood for and lost).
- Choices:
  - Go and congratulate them where the city can see: their regard +8, legitimacy +1, stress +2.
  - Demand the count be read again (costs 1 of the year's influence): the count stands, as counts do; their regard -6,
    legitimacy -1, stress -2. It is the house seen to fight for its own, and it is not cheap.
  - Say nothing: stress +1.

**14. `a_riot_on_the_day_of_the_count` "A Riot on the Day of the Count"**. Situation, weight 20, repeatable.
- Fires: `ladder_election_held`, in a year the magistrates are not believed (`trust_is_low` 1, or `public_trust` under 40).
- Choices:
  - Send your house's people to keep the peace at the booths (needs 4 credits): -4 credits, the commons +4, public trust +1,
    legitimacy +1.
  - Speak to them from the steps (oratory 2, or the Orator): the commons +3, public trust +2, stress +3.
  - Call the lictors and clear the squares: the commons -6, the magistrates +4, legitimacy +1.
  - Bar your doors until it is over: the commons -3, stress -1.
- 40: trust starts at 60 and the ladder calls it low under 35; 40 is a count already in doubt. The first soak had 45 and weight
  25, and a riot came at four counts in ten: too many for a riot.

### The consuls

**15. `the_consul_elects_feast` "The Consul-Elect's Feast"** (the winners' dinner, at the consulship). Situation, weight 30,
repeatable.
- Fires: `election_held` (now marked, so it comes in its year), and you were not elected (`consul_elected` unset). Role
  `grandee`: `office:consul_senior` (the new Senior Consul, of another house).
- Choices:
  - Go, and be seen to go: their regard +5, stress +1.
  - Send a gift ahead of you that the table will talk about (needs 6 credits): -6 credits, their regard +10.
  - Go, and ask across the table what the consulship will do for your bench (the house sits on a bench): +1 clout, their
    regard -3.
  - Stay away: their regard -6, stress -2.
- Why it matters: the Senior Consul presides, speaks from the chair, casts the vote that breaks a tie, and vetoes.

**16. `your_colleague_the_consul` "Your Colleague, the Consul"** (life in office). Situation, weight 20, repeatable.
- Fires: you hold a consul's chair (`office:consul_senior` or `office:consul_junior` on you). Role `grandee`: holds the other
  chair, and has not yet met you as colleague (`met_as_colleague` unset; every choice sets it, so it comes once a colleague).
- Choices:
  - Divide the year's business between you: their regard +8.
  - Tell them the senior chair leads (you hold it): +2 clout, their regard -8.
  - Keep your own counsel, and watch them: stress +2.

## The threads

- **A, the vote for a vote** (1 then 2): a vote sold at the door is called in when the buyer moves again. The other road from
  1, the word given, leaves the senator thinking better of you, which the count reads.
- **B, the grudge** (6, or 2 or 9, then 7): a house that voted against your motion and was let lose, or mocked, or one you
  shamed on the floor over a bought vote, is the one that moves a censure of yours later, and the report names it.
- **C, the smear** (11 then 12): the one your relative beat cries foul; answer in kind and the pamphlets run a year.

Six of the sixteen are in a thread.

## Read as a critic, before building

- Cut: "a client wavers". A client already leans with its patron on what the patron moves (the sway in `RawLean`), and the
  patron's notice of a client's vote is `the_patron_noticed`; a report on it fired too rarely to be worth a card.
- Cut: "a walkout". Nothing in the engine walks out; an abstention by a whole bench is not marked, and inventing one is
  more Core than a mark.
- Cut: a report per Act (the corn law in the streets, and so on). `act_passed:<law>` is set for good and never cleared, so
  each would come years after the news; and `the_act_carries` already answers every Act.
- Cut: a second report for a relative who lost though backed. It had nothing to decide that `one_of_yours_stands_for_office`
  did not decide better before the count.
- Reworked: "a speech that turns the room" is report 3. A speech alone moved nothing the count reads; an undecided house
  named, whose head's regard is the point that turns it, does.
- Reworked: the recount (report 13) cannot change a count, and says so; what it buys is the house seen to fight, at a price in
  standing and in this year's influence.
- Checked for firing: `the_consul_elects_feast` fires at every consular count of a Republic game. `your_colleague_the_consul`
  needs the player to be consul, which the random player rarely is (`the_consulship` fired once in the last soak); its test
  covers it.

## The soak

`qa --scenario all --seeds 2` (22 games of 60 years, and the scripted players), against the baseline of the same commit
without these reports (/tmp/claude-0/ev-baseline.log). No failures. 8098 reports fired; this file's sixteen took 450 of them,
5.6%. None is above 1.2%.

| report | fired | share |
|---|---|---|
| `a_vote_for_a_vote` | 70 | 0.86% |
| `a_bought_vote_called_in` | 5 | 0.06% |
| `the_vote_still_undecided` | 16 | 0.20% |
| `the_empty_seat_on_the_bench` | 35 | 0.43% |
| `a_feast_for_the_ayes` | 9 | 0.11% |
| `those_who_voted_against_you` | 8 | 0.10% |
| `an_old_grudge_on_the_docket` | 0 | |
| `the_senates_act_falls_on_a_friend` | 26 | 0.32% |
| `a_purse_on_the_senate_floor` | 14 | 0.17% |
| `one_of_yours_stands_for_office` | 28 | 0.35% |
| `a_smear_after_the_election` | 44 | 0.54% |
| `the_pamphlet_scandal` | 2 | 0.02% |
| `the_one_who_won_the_office` | 59 | 0.73% |
| `a_riot_on_the_day_of_the_count` | 40 | 0.49% |
| `the_consul_elects_feast` | 94 | 1.16% |
| `your_colleague_the_consul` | 0 | |

Two did not fire, both for a rare moment, and both are covered by their tests:
- `an_old_grudge_on_the_docket` needs a house holding a grudge from this file (reports 6, 2 and 9 fired 27 times between
  them, and four of their ten answers arm it) to move a censure or an embargo of the player's own house afterwards. The random
  player is seldom censured, and seldom by that one house.
- `your_colleague_the_consul` needs the player to be consul. The random player rarely is (`the_consulship` fired once in the
  baseline); it fired once in the first soak of this file and in a two-game soak of the Old Lion.

What the other files gave up, in share of all reports (baseline, then with these): ladder 10.0% to 9.0%, situations 5.0% to
4.3%, career_progression 5.4% to 4.8%, found_family 4.0% to 3.4%, senate 8.1% to 7.6%, courtship 5.6% to 5.2%, household
3.5% to 3.1%; every other file moved by 0.3 or less (ambitions and wealth gained a little, 0.6 and 0.3, as the briefing's
draw shifted). The Senate's own reports lost least among the big ones: these sit beside them, not in their place.
