# Votes: the Senate's votes and the elections, before, during and after

The owner asked for "some events for war, voting, family events, and random interactions". This file is the voting part:
seventeen reports in `content/events/votes.json` (prose in `content/text/votes.json`), the marks in the Senate and the ladder
that let a report name the person in the news, and `tests/VoteEventsTests.cs`.

The reports of the ladder and the consuls are of the Republic: each is gated `era imperium present: false`, as the emergency
reports are, and the ladder's are also gated by the ladder itself, which does not sit under a throne. The ten reports of the
Senate's floor (1 to 9 and 7a) come in either era (round 1 of review): the chamber still sits and still votes under the Imperium
(docs/TITLES.md), `senate.json`'s reports are not gated, and `VoteMarks` marks its people in any era, so three of the eleven starts
had no vote reports from the floor at all.

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
| " | `lost_at_the_count` (char, round 1) | the one of the player's house who stood for that rung and lost to someone of another house | at the count | with `ladder_election_held`; round 2: one pair of victor and loser is live at a time, so where two of the house lose on two rungs of one count the second rung's pair replaces the first's (`VoteMarks.LadderCounted`) and no report pairs one rung's victor with the other's loser |
| `LadderSystem.Tick` (round 2) | `standing_heard` (char, set by report 10) | a relative whose candidacy you have answered | by the report | struck when they no longer stand (counted or withdrawn), so one candidacy is one report |
| `VoteMarks.Bargains`, as the choice is made (`EventEngine.Resolve`) and at the start of `SenateSystem.Tick` (round 1; the promises, 2026-11-27) | `owes_you_a_vote` (char, set by report 1) | a head who gave their word for yours | by the report | their house owes yours a favour, which is a vote for your motions (`WealthSystem.VotesFor`, see "Promises"); your word on their motion is a pledge, your vote declared for it and judged at its count (+3 kept, -15 broken: `DealingsSystem.MotionCounted`). The mark goes. |
| " | `promised_your_vote` (char, set by report 2, and by `the_debt_called`) | a head you told you would vote for their motion | by the report | made one favour owed by your house, judged at their count. The mark goes. |
| " | `their_house_owes_you_a_vote` (char, set by `a_favour_repaid`, `the_whip`, `the_envoys_friend`) | someone who gave your house a vote for later | by the report | made a favour their house owes yours. The mark goes. |
| `VoteMarks.Docket`, every session, and `Bargains` | `no_bargain_with_you` (char) | everyone of a house that owes yours a favour or is owed one, or to which you broke your word on a vote in the last 8 years | rewritten every session | the reports that strike a bargain are closed to them. |
| `ElectionSystem` (round 1) | `dined_with_you` (char, set by report 15) | the Senior Consul at whose table you were seen | by the report | spent the first time they would veto a motion of yours (`RivalVetoes`), which they let stand; struck out at the next consular count |
| " | `lost_to_yours_at_the_count` (char) | the losers of a rung one of the player's house won | at the count | with `ladder_election_held` |

Each is tested in `tests/VoteEventsTests.cs`. No new condition or effect verb: the bargain is said with a char flag, and the engine
turns it into the favours the Senate already keeps.

One file outside this theme was touched. `tests/EarnedTitlesTests.cs`, `In_a_game_the_other_houses_rise_above_Prefect_only_by_a_vote_of_the_Senate`,
plays one game of thirty years and asks that a rival's petition for a rank carried in it. Whether one does is the draw of
that game, and any new report in the content changes the draw (the binder spends the shared generator on every report it
tries): with these reports seed 5 had nine petitions and none carried; without them seed 4 has none carried either. The test
now plays seeds 5, 3 and 2 in turn until one has a petition carried, and holds every other check (no rival rises a rung
without a petition carried that year, the player's vote counted, no bench's line on it) in every game it plays. The other
themes' reports will move the draw too, so this is meant to survive their merge.

## The seventeen reports

Weights. The reports in `ladder.json`, `docket.json` and `caesar.json`, which read a flag the engine marks for a year, sit
between 25 and 45; the Director makes a report on a marked flag five times likelier in its year. Most here are of that kind and
sit at 20 to 40. Two read a flag that is up in many years (`motion_open`, and the player's own motion), and are kept low (4 and
10) so that they stay well under the 1.5% share the brief allows: `the_whip`, on `motion_open` at 9, takes 3.4% of all reports.

Deltas. The existing Senate and ladder reports move a head's regard by 4 to 15, legitimacy by 1 to 3, stress by 1 to 6,
credits by 3 to 12 (hundreds), clout by 1 or 2. These stay inside that: regard 2 to 15, legitimacy 1 or 2, stress 1 to 4,
credits 2 to 8, public trust 1 or 2, an estate 3 to 6. A credit amount is hundreds (docs/MONEY.md); prose names no sums.

### Before a vote

**1. `a_vote_for_a_vote` "A Vote for a Vote"** (thread A, step 1). Situation, weight 4, repeatable.
- Fires: a motion is on the docket (`motion_open`), turn 3 or later. Role `senator`: `moves_a_motion` and not
  `moves_against_you` (nobody trades a vote on a censure of your own house), thinks -15 or better of you, and is not already in a
  bargain with you (`owes_you_a_vote`, `bought_your_vote` and `no_bargain_with_you` all unset: no favour stands between the houses,
  and you have not broken your word to theirs in eight years).
- What happened: the head of a house with a motion before the Senate comes to count heads, and offers its own vote on your next
  motion for yours on this one.
- Choices:
  - Give your word, and take theirs: their regard +6, your stress +1; arms `owes_you_a_vote` on them, which `VoteMarks.Bargains`
    makes good as the choice is made (round 1: the flag was read by nothing; the promises, 2026-11-27: see "Promises" below).
    Their house votes for your next motion, whichever way it leans, unless it strikes at it; your vote on their motion is
    declared for it, a pledge judged at its count, +3 regard if you keep it, -15 if not, written down, and no word of yours taken
    by that house for eight years. The body and the label say both. (+6: a pledge of seats at the consulship is +8,
    ElectionSystem.PledgeRegard; a word not yet kept is a little less.)
  - Tell them a vote costs more than a vote (not for the Honorable or the Just; needs their house to have 400 credits): +4 credits
    from their treasury to yours, legitimacy -1, their regard +2; arms `bought_your_vote`. (4: below the 5 that buying the cheapest
    house's vote costs, SenateSystem.BribeCredits; it was offered at your door. -1: a bought vote that comes out costs 3; one taken
    quietly is talked of a little. Round 1: the money had come from nowhere.)
  - Tell them your vote is your own: their regard -4. (Less than a broken pledge, 15: nothing was promised.)
- Differs from `the_whip`: coin from a whip there; here a vote in kind from the mover, a person named, and a thread.
- Weight 4: on the same flag as `the_whip` (9, 3.4%), and the role binds most years.

**2. `a_bought_vote_called_in` "A Bought Vote Called In"** (thread A, step 2). Situation, weight 30, repeatable.
- Fires: `motion_open`, and role `buyer` has both `bought_your_vote` (you sold them your vote once) and `moves_a_motion`
  (they have another motion before the Senate now).
- What happened: they paid you once and have another motion up; they do not mean to pay again.
- Choices:
  - Say you will vote for it: their regard +4, legitimacy -1 (the house is seen as theirs); clears the flag, and arms
    `promised_your_vote`, a favour your house owes theirs, judged at their count as the word given in report 1 is (round 1:
    the promise was checked by nothing).
  - Send back what they paid, and more (needs 5 credits): -5 credits, legitimacy +1, their regard -2; clears.
  - Tell them the bargain was for one vote: their regard -10, legitimacy -2 (they let it be known your vote is for sale);
    clears, and arms `grudge_against_you` (thread B): spurned in front of the chamber, they remember it.
- Weight 30: the payoff of a thread, narrow (only after step 1's second choice and a new motion of theirs).

**3. `the_vote_still_undecided` "The Vote Still Undecided"**. Situation, weight 10, repeatable.
- Fires: your house has a motion before the Senate (`my_motion_open`). Role `doubter`: `undecided_on_yours`, the head of a house
  that leans neither way on it, who thinks 12 to 19 of you (round 2).
- What happened: the vote of one house is still open; its head will decide in the chamber, and is not far off leaning your way.
- Choices:
  - Make your case to them in person (costs 1 of the year's influence): their regard +6, stress +1. (The same purse as a lobby;
    a lobby moves the lean by 2 at once, this moves the head and lasts.) Tips a doubter at 14 or more.
  - Have them to dinner (needs 4 credits): -4 credits, their regard +8. Tips any doubter the report binds: the label says it is the
    likeliest.
  - Speak for it from the floor, and let them hear it there (needs oratory 3 or the Orator): their regard +4, legitimacy +1,
    stress +2. Tips one at 16 or more.
  - Leave them to make up their own mind: stress -1.
- Why it matters: at 20 regard a head leans one more toward what you move (`SenateSystem.RawLean`); an undecided house is the one
  that a point moves. Round 2: the doubter was any head with a lean of 0, so a head below 12 could not be tipped by any choice and
  one at 20 or more was held at 0 by something else, and the player could not tell whether a choice changed the count. Now the
  three choices differ in how sure they are, and each label says it may bring them over.
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
  - Let them give it, and go as a guest: their regard +5, stress -1, legitimacy -1 (round 2: the city says it was their motion).
  - Thank them by letter, and keep your evening: their regard -2, stress -3 (round 2: it was -1, the same as the guest's, so the
    letter was the guest's evening with less regard and never chosen).
- The three trade money and standing (your table), standing for regard (theirs), and regard for your own evening (the letter).

**6. `those_who_voted_against_you` "The House That Voted Against You"** (retitled in round 1: it names one house) (the losers' grudge; thread B, step 1). Situation, weight 25,
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

**7a. `an_old_grudge_against_your_motion` "An Old Grudge Against Your Motion"** (thread B, the common second step; added in round 1).
Situation, weight 30, repeatable.
- Fires: your house has a motion before the Senate (`my_motion_open`). Role `rival`: `grudge_against_you`, thinks -20 or less of
  you (round 2: it was 10 or less, and only at -20 does a head's regard lean their house against what you move, so for most
  bindings letting them talk cost nothing at the count, and the 600 credits bought nothing there).
- What happened: the head who holds the grudge is speaking against your motion in every corridor, and leans against it. The body
  says "a head who thinks ill enough of you leans against what you move, and they do" (it said they "talk others into it", which
  nothing in the engine does).
- Choices:
  - Send them something to let it go (600 credits): their regard +12, which may lift them out of leaning against it; clears the
    grudge.
  - Answer them on the floor, and name the old quarrel (oratory 2, or the Orator): legitimacy +1, their regard -4 (they still lean
    against it), stress +2; clears it (it is out in the open).
  - Let them talk: their regard -3, stress +1. The grudge is spent on this vote and cleared (round 2: it stayed, and the card came
    back at a later motion saying "this is the first chance they have had").
- Why: report 7 needs that same head to move a censure or embargo of your house, which in the soak never happened, so four answers
  in three reports armed a grudge that did nothing. Your own motion comes most years a Republic house is active. Weight 30, as the
  other second steps.

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

**10. `one_of_yours_stands_for_office` "One of Yours Stands for Office"** (a relative stands). Situation, weight 20 (round 2: 30),
repeatable.
- Fires: a relative of your blood, of your house (`inLine`, round 1: the ladder's marks follow the house, so a relative of
  another house's count never came back), has `standing_for_office` (their name is down for the coming ladder count) and not
  `standing_heard` (round 2: every choice sets it, and the ladder strikes it when the candidacy is counted or withdrawn; a name goes
  down two years before the count, and the report came twice for one count).
- Choices:
  - Walk the wards with them, so the city sees the house behind them: their regard +8, your stress +3, their charisma +1
    (two votes at the count).
  - Teach them how the floor is won (oratory 3): their oratory +1 (two votes), their regard +4, your stress +2.
  - Tell them an office is won by the one who stands: their regard -4, your stress -1.
- The body says the house's backing and a paid canvass are there as for anyone who stands (the ladder window); what the relative
  asks is what you will do yourself.
- One candidacy, one report (`standing_heard`); the next count is five years on.

**11. `a_smear_after_the_election` "A Smear After the Election"** (thread C, step 1). Situation, weight 20 (round 2: 30), repeatable.
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
  - Call off your writer: their regard +6, public trust +1, legitimacy -1 (the house is seen to blink first; round 1: it was free and
    better on every count).
  - Have their pamphlets brought before a praetor (costs 2 of the year's influence): their regard -12, legitimacy +1.
  - Let the writers write: public trust -2, legitimacy -1, stress -1, and their house's legitimacy -2: the pamphlets bite both ways.

**13. `the_one_who_won_the_office` "The One Who Won the Office"** (the recount). Situation, weight 20 (round 2: 30), repeatable.
- Fires: `ladder_election_held`. Role `victor`: `beat_yours_at_the_count` (won a rung one of yours stood for and lost). Role `kin`
  (round 1): `lost_at_the_count`, the one of your house who lost it, who is in the room.
- Choices:
  - Go and congratulate them where the city can see: their regard +8, legitimacy +1, stress +2, your relative's regard -4.
  - Demand the count be read again (costs 1 of the year's influence): the count stands, as counts do; their regard -6, legitimacy
    -1, stress -2; your relative's regard +8 (the house fought for them), the victor's house's legitimacy -1 and public trust -1
    (the count is doubted). Round 1: it had changed nothing and cost something, so congratulating won on every count.
  - Say nothing: stress +1, your relative's regard -2.

**14. `a_riot_on_the_day_of_the_count` "A Riot on the Day of the Count"**. Situation, weight 20, repeatable.
- Fires: `ladder_election_held`, in a year the magistrates are not believed (`trust_is_low` 1, or `public_trust` under 40).
- Choices:
  - Send your house's people to keep the peace at the booths (needs 4 credits): -4 credits, the commons +4, public trust +1,
    legitimacy +1.
  - Speak to them from the steps (oratory 2, or the Orator): the commons +3, public trust +2, stress +3.
  - Call the lictors and clear the squares: the commons -6, the magistrates +4, legitimacy +1.
  Round 1: each label now says what the answer does to the squares, and the chips give the estate and the trust.
  - Bar your doors until it is over: the commons -3, stress -1.
- 40: trust starts at 60 and the ladder calls it low under 35; 40 is a count already in doubt. The first soak had 45 and weight
  25, and a riot came at four counts in ten: too many for a riot.

### The consuls

**15. `the_consul_elects_feast` "The Consul-Elect's Feast"** (the winners' dinner, at the consulship). Situation, weight 10
(round 1: at 30 it was the most fired new report, 1.3%, at nearly every consular count), repeatable.
- Fires: `election_held` (now marked, so it comes in its year), and you were not elected (`consul_elected` unset). Role
  `grandee`: `office:consul_senior` (the new Senior Consul, of another house).
- Choices:
  - Go, and be seen to go: their regard +5, stress +1; marks them `dined_with_you`.
  - Send a gift ahead of you that the table will talk about (needs 6 credits): -6 credits, their regard +10; marks them
    `dined_with_you`.
  - Go, and ask across the table what the consulship will do for your bench (the house sits on a bench): +1 clout, their
    regard -8 (round 1: it was -3, a clout for nearly nothing).
  - Stay away: their regard -6, stress -2.
- Why it matters: the Senior Consul presides, speaks from the chair, casts the vote that breaks a tie, and once in the term
  vetoes. A consul who dined with you lets the first motion of yours they would strike stand (`ElectionSystem.DinedChar`): the
  stake that comes back. The body says the veto is once a term (it said "what they like", which docs/REPUBLIC.md does not allow).

**16. `your_colleague_the_consul` "Your Colleague, the Consul"** (life in office). Situation, weight 20, repeatable.
- Fires: the year of the consular count (`election_held`, round 2: it came in any year of the ten-year term, though the body says
  the colleague has asked to see you before the first session), and you hold a consul's chair (`office:consul_senior` or
  `office:consul_junior` on you). Role `grandee`: holds the other chair, and has not yet met you as colleague (`met_as_colleague`
  unset; every choice sets it, so it comes once a colleague).
- Choices:
  - Divide the year's business between you: their regard +8.
  - Tell them the senior chair leads (you hold it): +2 clout, their regard -8.
  - Keep your own counsel, and watch them: your intrigue +1, stress +1 (round 2: it was stress +2 and nothing else, pure cost).

## The threads

- **A, the vote for a vote** (1 then 2, and the count): a vote sold at the door is called in when the buyer moves again. The
  word given in 1 is two favours owed, kept or broken at the two counts; the promise in 2 is one, kept or broken at theirs.
- **B, the grudge** (6, or 2 or 9, then 7a or 7): a house that voted against your motion and was let lose, or mocked, or one you
  shamed on the floor over a bought vote, speaks against your next motion (7a), or moves a censure of yours (7), and the report
  names it.
- **D, the consul's table** (15, then the veto): a consul who dined with you spares one motion of yours from the veto.
- **C, the smear** (11 then 12): the one your relative beat cries foul; answer in kind and the pamphlets run a year.

Eight of the seventeen are in a thread.

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

## Round 1 of review (2026-11-27)

- The vote for a vote is real (`VoteMarks.Bargains`), and so is the promise in *A Bought Vote Called In*. The price named in
  report 1 is paid by the senator's house.
- The grudge has a common second step, *An Old Grudge Against Your Motion*.
- The ten reports of the Senate's floor come under the Imperium too.
- *The One Who Won the Office* has your beaten relative in it, and the recount does something; *The Pamphlet Scandal*'s two cheap
  answers each cost something and give something.
- *The Consul-Elect's Feast* is a third as likely, its dinner comes back as a spared veto, and asking for your bench costs regard.
- Words: *The Vote Still Undecided*, *The House That Voted Against You*, *The Senate's Act Falls on a Friend*, the riot's labels
  and the consul's veto.
- *A Vote for a Vote* stays at weight 4: with its second step real it is no longer a hollow card, and its share was under 0.9%.

## Round 2 of review (2026-11-27)

The owner has said "Make sure the vote promises actually work", and those promises have their own phase after this review
(promises-brief.md), so there are not two designs. Left to it: *A Vote for a Vote* (the price named, and the word given, judged
at the count), *The Empty Seat on the Bench* (the promise to vote with the bench), *The Senate's Act Falls on a Friend* (the
promised repeal), *A Bought Vote Called In*, and *A Favour Repaid*'s "their house's voice" (docs/events/chance.md).

Everything else the critics found:

- *The Vote Still Undecided* binds a doubter its choices can tip (12 to 19), and the labels say how likely each is to.
- *An Old Grudge Against Your Motion* binds a rival who really does lean against it (-20 or worse), says only that, and every answer
  ends the grudge.
- *A Feast for the Ayes*: the guest's evening costs a point of standing, the letter keeps your evening; no answer is dominated.
- *Your Colleague, the Consul* comes in the year of the count, and keeping your counsel teaches you something.
- *One of Yours Stands for Office* comes once a candidacy (`standing_heard`, struck by the ladder).
- *The One Who Won the Office* pairs the victor and the loser of one rung (`VoteMarks.LadderCounted`).
- Weights: the count-day reports *One of Yours Stands for Office*, *The One Who Won the Office* and *A Smear After the Election*
  from 30 to 20, so the ladder's own count reports get back some of their share.
- Words: *The House That Voted Against You* ("{loser.house} remembers who it lost to"), *The Senate's Act Falls on a Friend* ("An Act
  of the Senate has carried"), *The Pamphlet Scandal* ("bought what votes they could"; the Reach "trusted its magistrates the less").

### Measured after round 1

The soak after review (22 games, 7,843 reports) passed. This file took 386 reports (4.9%), sixteen of its seventeen kinds; the most
fired is `a_vote_for_a_vote` (64, 0.8%), and `the_consul_elects_feast` is no longer among the thirty most fired. The new
`an_old_grudge_against_your_motion` fired twice; `an_old_grudge_on_the_docket` did not, as before (its test covers it).

### Measured after round 2

The soak after round 2 (22 games, 7,996 reports) passed. This file took 394 reports (4.9%, as after round 1), sixteen of its
seventeen kinds; the most fired is still `a_vote_for_a_vote` (87, 1.1%). `an_old_grudge_on_the_docket` did not fire, as before (its
test covers it); `the_vote_still_undecided`, `an_old_grudge_against_your_motion` and `your_colleague_the_consul` fired once each, now
that each binds only where it can do what it says. The ladder's own file is back to 8.1% of all reports (7.7% after round 1, 10.0% in
the baseline) with the count-day reports at 20; the Senate's is at 6.8% (6.9%).

## Promises

The owner said "Make sure the vote promises actually work" (promises-brief.md). The rule: every promise about a vote that the
game makes is kept by the engine; only where one cannot sensibly be kept are the words changed to say what happens. Each promise
below is proved in `tests/VotePromisesTests.cs` the way a player makes it (the report gathered by the event engine and the
choice applied through it, or the command the window sends), then the real session and count (`SenateSystem.Tick`); each test was
run with its hook taken out and failed.

### The bargain group (2026-11-27)

**The rule these rest on: a favour owed to your house is a vote** (`WealthSystem.VotesFor`, read by `SenateSystem.Owes`). A house
that owes your house a favour votes for every motion your house moves while it owes it, whatever it leans, and the favour is spent
at the count of the first motion it would not have voted for of its own accord. The whip count (`Stance`, shown as declared, with
the line "votes for it: it owes your house a favour"), the forecast before a motion is moved (`Forecast`), the tally and the count
all read the one rule, so they agree. A bench does not follow its leader's owed vote, as it does not follow a bought one
(`LeaderStance`): that is the leader's debt, not the bench's, and without it the forecast and the count parted on the members. It
is not asked on a motion that strikes at that house (a censure, an embargo, a break-up, a recall or a reconquest of it): the favour
waits for your next motion, and you are told so. It lapses at the year's end, with a notice that says why, if the two houses are at
war, if either house is gone, or after 8 years (`WealthSystem.FavourYears`, as before: no motion of yours in eight years that it
would not have voted for anyway). The favour is the house's, so a new head keeps it. Between two other houses a favour is still the +2 lean it
was (`FavourLean`), but not on a motion that strikes at the house that owes it, where it is not spent either; on your motion it
is a vote. At the count you are told "X kept its word" when the favour bought the
vote, and that it is still owed when the house was for the motion anyway; the house page counts the favours paid with a vote.

Why "spent only where it changes the vote": the sweep found that a favour spent on whichever motion came next, even one the house
would have carried anyway, was not the favour that waits until you need it that the house page and *A Favour Repaid* promise. A
house for your motion of itself gives up nothing by voting for it, so it still owes.

**The favour you owe** is judged as before at the count of that house's next motion (+5 kept, `FavourKept`; -15 broken,
`FavourBroken`), with three repairs: you are reminded when you come to owe a house whose motion is already before the Senate
(`WealthSystem.Owe`: the notice `SenateSystem.Add` gives never fired then); it is not called in on a motion that strikes at your
own house, nor between houses at war; and a motion struck before its count (a consul's veto, `ElectionSystem.Veto`; withdrawn for
foreign gold, `ForeignGoldSystem`) tells you the favour waits for that house's next motion, and voids a word given on it
(`DealingsSystem.Struck`).

| Promise | Words (where) | Outcome | What keeps it now | Tests |
|---|---|---|---|---|
| `a_vote_for_a_vote` | "owes your house a vote ... on the next motion your house moves" (body) | now kept, body made exact | The bargain is made as the choice is (`EventEngine.Resolve` calls `VoteMarks.Bargains`; the session's call stays as a backstop), so the whip count has it at once. Their house owes yours a favour, which is a vote. The body now says "It votes for the next motion your house moves, whichever way it leans, unless that motion strikes at it." | `AVoteForAVote_TheirHouseVotesForYourNextMotion_HoweverHardItLeansAgainst`, `..._TheirWordWaitsOnAMotionThatStrikesAtThem_AndIsPaidOnTheNext`, `..._ForAMotionTheHouseLikesAnyway_TheVoteIsStillOwed`, `..._LapsesWithAWar_WithANotice`, `..._LapsesAfterEightYearsWithNoMotionOfYours_WithANotice`, `..._ANewHeadKeepsTheHousesWord_AcrossASaveAndALoad`, `AHouseThatOwesYouAVote_LeadingABench_TheForecastAndTheCountAgreeOnItsMembers` |
| `a_vote_for_a_vote/give_your_word` | "Vote for their motion, or they will remember it"; body "the chamber keeps count of a word given and not kept"; log "the chamber will see whether both are paid" | now kept; one body phrase reworded | Your word is a vote pledged (`DealingsSystem.GiveWord`, the pledge the house window gives): your vote on their motion is declared for it, you are told what it binds you to, and the count judges it (`MotionCounted`: kept, their head +3 and it is logged; broken, -15, written down, and that house takes no word of yours on a vote for 8 years, and no new bargain is struck with it, `VoteMarks.BargainBarredChar`). Struck before the count, the word is void and you are told. "The chamber keeps count" became "a word given and not kept is written down, and {senator.house} takes no word of yours on a vote for eight years": the chamber as a whole keeps no count in the engine, and giving a broken pledge a cost in legitimacy would be a new rule for every pledge in the game, which the house window's pledge does not promise. Both payments are now in the chronicle, so the log line is kept. | `AVoteForAVote_YourWordIsDeclared_YouAreReminded_AndKeepingItIsCounted`, `..._YourWordBroken_IsRememberedAndBarsTheNextBargain`, `..._TheirMotionVetoed_YourWordIsVoid_WithANotice` |
| `a_favour_repaid/their_voice` | "Ask for their house's vote when you need it" | now kept | The choice marks the debtor `their_house_owes_you_a_vote` (`VoteMarks.HouseOwesChar`), made a favour their house owes yours: a vote, spent where it changes the vote. The +2 influence that stood in for it is gone. Closed while a favour stands between the houses. | `AFavourRepaid_TheirVoice_TheirHouseVotesForYourMotion` |
| `a_gift_that_asks_a_favour` | "Keep it, and you will be expected to vote for it" | already kept | Judged at the giver's count (+5 / -15). Now also: you are reminded when it comes, since the motion was moved first; it is never sent to a house the giver's motion strikes at; the log no longer says "with its gift" of favours that came with none. | `AGiftThatAsks_KeptAndVotedFor_IsEven_KeptAndVotedAgainst_IsRemembered`, `AGiftThatAsks_IsNotSentToTheHouseItsMotionStrikesAt` |
| `.../keep_it_and_promise_nothing` | "Keep it, and say plainly that it buys nothing" | now kept | `WealthSystem.Answered` reads the answer at the start of the Senate's session, before the count, as well as at the year's gifts. | `AGiftThatAsks_SaidToBuyNothing_YourVoteIsNotJudged_InTheSameYear` |
| `.../send_it_back` | "Send it back unopened" | now kept | The same. | `AGiftThatAsks_SentBack_YourVoteIsNotJudged_InTheSameYear` |
| `the_whip/name_your_price` | "No coin. A favour, to be named later." | now kept | The whip's house owes yours a favour (`HouseOwesChar`): named later for a vote, money or a match on its page, or left alone a vote for your motion. A whip is of another house (`Binder.OutsideParts`), and the choice is closed, with its reason, while a favour stands between the houses. The +2 influence is gone. The em dash is gone from the label and the log. | `TheWhip_AFavourNamedLater_IsOwedByTheWhipsHouse_AndVotesForYou_AlongsideASecondPromise`, `TheWhip_TheFavourCanBeNamedForSomethingElse_AndThenNoVoteIsOwed`, `TheWhip_IsNeverOfYourOwnHouse_AndAHouseAlreadyInAFavourWithYoursCannotBeAskedAgain` |
| `the_envoys_friend/ask_for_a_vote` | "A vote, when the time comes." | now kept | The host's house owes yours a favour (`HouseOwesChar`). The +2 influence is gone; the host still thinks 4 worse of you for asking. | `TheWhip_AFavourNamedLater_..._AlongsideASecondPromise` (two promises at once) |
| `the_debt_called/pay_with_favor` | "Pay in favour: your house will owe theirs a vote. Trade one debt for another." | reworded, and now kept | The creditor is marked `promised_your_vote`: your house owes theirs a favour, judged at its next motion (+5 / -15) or asked for in coin. "A posting, a door" and "never quite ends" are gone: there is no engine for a posting or a door owed, and a favour is settled once. A lender and a creditor are of another house (`Binder.OutsideParts`). | `TheDebtCalled_PaidInFavour_YourHouseOwesTheirsAVote_JudgedAtItsNextMotion_AcrossASaveAndALoad` |
| `SenateSystem.Add` | "you owe it a favour: this is where it is called in" | reworded, and kept | Now "it is called in at this motion's count ... If the motion is struck before the count, the favour waits for the next one it moves", and a struck motion tells you so. Not given for a motion that strikes at your house, nor at war, where it is not called in. The veto cannot be undone to keep the old words. | `AFavourYouOwe_IsNotCalledInOnAMotionStruckBeforeTheCount_NorOnOneThatStrikesAtYou` |
| `DealingsSystem.Lasting` | "It owes you a favour ... Uncalled, it is forgotten in year N" | reworded | It now says the favour votes for your motions and is spent on the first it would not have voted for, or can be called in; forgotten in year N, or at once at war. It cannot wait for a call alone without breaking the Senate window's and WEALTH.md's promise that a favour left alone buys a vote. The "you owe it" line says the favour is not called on a motion that strikes at your house. | `TheHousePage_SaysWhatAFavourOwedIs_AndTheCountDoesIt` |
| `DealingsSystem.AskSays` | "declares its vote as yours is ... It calls it in on the next motion it moves" | already kept | As before; the words add "that does not strike at your house". | `AFavourAskedForAVote_TheirVoteIsDeclared_AndYoursIsAskedAtTheirNextMotion` |
| `the_doorkeeper/ask_a_seated_friend` | "It cost a favour." | reworded | The report names no house, so no favour could be owed to anyone; the log now says the house spent some of its influence (the -1 it costs). Giving it a role would change who the report comes to. | `TheDoorkeeper_ASeatedFriend_PromisesNoFavourItCannotName` |
| `AskAbove.Move` | "It is a favour owed", settled "by voting for the next motion it moves" | now kept | The motion moved at your asking is marked (`Motion.MovedFor`) and does not settle the favour; the patron's own next motion does, with the notice. | `AMotionMovedAtYourAsking_DoesNotSettleTheFavour_TheirOwnNextMotionDoes` |
| `SenateScreen.Favours` | "Each leans +2 for the next motion you move" | reworded to the new rule | "Each votes for every motion you move while it owes it, whatever it leans, and the favour is spent at the count of the first it would not have voted for of its own accord; not on a motion that strikes at that house, and not while you are at war with it." The whip count shows the owed vote. | `AVoteForAVote_TheirHouseVotesForYourNextMotion_...`, `TheHousePage_...` |

**Numbers.** None is new. A favour owed lapses after 8 years (`FavourYears`, as before), and at once with a war or a house gone (a
favour is between two houses of the Senate; an enemy at war gives no vote). The word given in *A Vote for a Vote* is a pledge with
the pledge's numbers, +3 kept, -15 broken, 8 years remembered (`PledgeKeptRegard`, `PledgeBrokenRegard`, `PledgeMemoryYears`):
they were the house window's own, and the report's words name no other (round 1 had used the favour's +5 / -15). The favour you owe
keeps +5 / -15. *A Favour Repaid*, the whip and the envoy's friend lose the +2 influence each had in place of the vote: the vote is
what they give now, and a favour can be named for 1,500 credits, so the two together would be paid twice. The reports that strike a
bargain are closed while a favour stands either way between the two houses, or for 8 years after a broken word: a house owes
another one favour at a time (`WealthSystem.OweFor`), so a second would be nothing.

**Not done here.** The whip's report does not bind the motion it whips for, so the vote you give in return for the favour is not
judged (the label promises only the favour); *take_the_coin*'s "promise the vote" belongs to another group.
