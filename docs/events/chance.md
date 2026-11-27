# Chance: the people of the Reach crossing your path

The owner asked for events for "random interactions". These sixteen reports are the people you meet without meaning to: a
classmate from the Academy, an old enemy across a room, a guest at a feast who says too much, an old officer at dinner, a painter
with a wall to fill, a merchant prince with a venture, someone of a house that has lost its worlds, a seller of secrets, a quiet
word from a house you are at odds with, a young person who needs a patron, and two guests at your table with blood between their
houses. Most are one evening and move one person's regard; four of them start something that comes back.

Files: `content/events/encounters.json`, `content/text/encounters.json`, `tests/EncounterEventsTests.cs`. No Core change: every
person here is bound by a condition the engine already has (see "What the engine already marks", at the end). One change outside
these files, three lines in `tests/EarnedTitlesTests.cs`, is explained under "Outside these files".

## What exists already, and how these differ

Read before writing: `situations.json`, `echoes.json`, `court.json`, `council.json`, `rings.json`, `exchange.json`,
`academy_remembered.json`, `feud.json`, `life.json`, and the nearby `courtship.json`, `found_family.json`, `steward_decisions.json`.

- *An Old Friend* (`life.json`) is an old friend in the city; *A Friend Is Buried* and *The Enemy Is Dead* are the deaths of old
  ties. Nothing meets a **living old enemy**: the engine raises `has_old_enemy` every year one exists and no report reads it.
  *An Old Enemy Across the Room* does.
- The Academy is remembered (`academy_remembered.json`) by six reports, each once, each tied to a mark of the Academy year (first,
  second, the banner taken, the Standard that won). *A Classmate in the City* is any classmate, any year after, repeatable, and
  can end in a favour owed or a friendship sworn.
- *The Portrait* (`situations.json`) is a private likeness for the hall, once. *A Painter's Great Canvas* is a public wall, with
  a rival of yours already asking for the centre: it is about where you stand against them, not about the picture.
- The feud (`feud.json`) begins with a death. Nothing begins a quarrel short of one. *Words at the Feast* and *The Quarrel Comes
  to a Head* are the road there: it can end in a gift, in first blood, in an enmity left to harden (which, eight years on, is an
  old enemy), or in a killing that opens a blood debt the feud system then carries.
- *The Enemy's Envoy* (`rings.json`) is any war's envoy with terms; *The Envoy's Friend* and *The Envoy Turned Away* (`council.json`)
  are your own envoy's calls. *A Word Off the Record* is a house you are not at war with, but whose people think badly of you,
  asking quietly to stop it going further.
- *The Spymaster's Report* (`council.json`) is your own spymaster's work. *A Secret for Sale* is a stranger with something to sell
  about a person you are at odds with, and the choice is how to use it.
- *The Merchant's Gift* (`situations.json`) is an estate's gift; *The Influence Broker* and *The Lean Season*
  (`steward_decisions.json`) are your own money troubles. *A Merchant Prince's Venture* is a stake in another house's trade that
  pays, or does not, by how that house fares.
- *A Child with Nowhere to Go* (`found_family.json`) takes in a child. *A House Without Lands* and *A Veteran of the Old Wars* can take in a
  grown stranger as a companion, and *A Patron Wanted* as a protégé, through the same household rules (`takeIn`).
- *The Commons' Feast*, *The Old Blood's Invitation* and the other estate reports are invitations from an estate. *Two Guests at
  Odds* is two people at your own table whose houses have blood between them, and you choose between them.

## The threads

1. **The quarrel** (two steps, and then the feud). *Words at the Feast* can leave a guest with a quarrel against you
   (`quarrel_with_you`). *The Quarrel Comes to a Head* answers it: a gift, a meeting at first blood (a duelist only), a killing (the
   ruthless or cruel only, `kill` with `by: self`, so the dead person's house holds blood against yours and the feud reports follow),
   or letting it run, which leaves their regard lower and on its way to an old enemy.
2. **A favour owed** (two steps). *A Classmate in the City* (a word put in for them) or *A House Without Lands* (coin lent) leaves the
   person owing you (`owes_you_a_favour`). *A Favour Repaid* is them coming to pay it: their voice, the coin, or nothing asked. You
   may also keep it owed for later.
3. **The venture** (two steps, two endings). *A Merchant Prince's Venture* takes your money into a rich house's trade
   (`staked_by_you`). *The Venture Comes Home* pays it back with half as much again while their house stays rich and at peace with
   yours; *The Venture Goes Down* comes instead if their house falls out of the richest four in ten, or goes to war with yours.
4. **The patron** (two steps). *A Patron Wanted* can put your name behind a young person of a lesser house who has no rank yet
   (`your_protege`). When they reach their first rung, *The One You Stood For*: a friendship sworn (`addBond sworn`, both ways), a
   favour called in at once, or nothing asked.

Ten of the sixteen are in a thread (two only as the first step of the favour thread); six stand alone.

## Weights and the share of the briefing

The theme is the likeliest to crowd the briefing: almost every year someone fits most of these. So the stand-alone meetings are
light. An ambient report that is eligible most years at weight 5 takes 2.1% of all reports in the baseline soak (*The Quiet
Competence*), one at weight 3 takes 1.2% (*The Old Blood Sneers*), and a situation always eligible at weight 3 takes 1.6% (*The
Influence Broker*). A first build at weights 0.6 to 2 took 5.9% of the reports of a 22-game soak, which is too much; at the weights
below, a meeting that someone fits most years is drawn in about one year in fifty for each unit of weight, and the stand-alone ones
weigh 0.3 to 0.85 (weights may be fractions; *A Classmate in the City* and *Two Guests at Odds* weigh 2 because few games or few
years have the people they need). The second steps are narrow (one flagged person, and nothing else asks for them) and weigh 10
to 15, so they come one to three years after the first: at weight 2 the venture's return did not come once in a soak with five
stakes. Four of the meetings are ambient: a briefing takes one ambient report at most while weightier ones wait, which is a second
guard against crowding. None is a set-piece. What the file took in the final soak is at the end.

## Deltas

The existing meetings and evening reports move regard by 3 to 15 (*An Old Friend* 3, *The Ranks That Won* 15, the elder's peace
6), legitimacy by 1 to 2, credits by 3 to 8 (300 to 800 credits), stress by 2 to 8, an estate by 2 to 5, a skill by 1, influence
by 1 to 2. These stay inside that, with three exceptions said where they are: a friendship sworn (a bond of 40 or 50), a secret used
against someone (their house loses 2 legitimacy and they think 20 worse of you), and a killing. Money in the files is hundreds
(docs/MONEY.md): `"credits": 4` is 400 credits.

A person here is bound outside your house by a role name the binder already keeps outside it (`envious`, `grandee`, `claimant`,
`debtor`, `envoy`) or, where that name would mislead, by `inLine` false against you (one who does not stand in your house's line:
nobody of your house but one struck from it).

## The events

### 1. A Classmate in the City (`a_classmate_in_the_city`)
- **What happened.** Someone who was in your year at the Academy (the Institute, under the Imperium) is in the same room as you,
  years later, and has come over.
- **When it fires.** You passed out of the Academy (`academy_graduated`), it is the third year or later, and a classmate (`cohort`)
  is alive, grown, and not of your line. Ambient, weight 2, repeatable. Only the two Academy starts have a year of classmates, so
  it is weightier than the rest.
- **Roles.** `classmate`: `cohort` present, `inLine` false against you.
- **Choices.**
  - *Put in a word for them where it counts* (needs 1 influence): influence -1, their regard +12, and they owe you a favour
    (`owes_you_a_favour`, thread 2).
  - *Make an evening of the old stories*: stress -5, their regard +4.
  - *Ask how their house stands*: influence +1, their regard -3 (they notice what you came for).
  - *Swear the old friendship again* (needs their regard 30 or more, and no sworn or blood bond between you either way): a sworn
    bond of 40 each way, their regard +6.
- **Numbers.** 1 influence for a word, as *A Child with Nowhere to Go* charges to find a place. +12 is under *The Ranks That Won*
  (+15, which costs 600 credits). Stress -5 is under *An Old Friend*'s -8: an evening, not an old friendship. A sworn bond of 40: above
  the 25 a friendship sworn in the Academy year starts at (`academy_republic.json`), because this one has lasted, and under the 50 a
  companion taken into the house gets.
- **How it differs.** The remembered-Academy reports are once each and tied to a place in the order of merit; this is any classmate,
  again and again, and it feeds two threads.

### 2. An Old Enemy Across the Room (`an_old_enemy_across_the_room`)
- **What happened.** Someone who has thought ill of you for eight years or more is at the same gathering.
- **When it fires.** `has_old_enemy` is up (the engine raises it each year an old enemy lives) and the old enemy is bound by their
  mark. Situation, weight 0.85, repeatable. It is eligible every year one lives, so it is kept low.
- **Roles.** `enemy`: char flag `old_enemy` (the engine's own mark, cleared and set again each year).
- **Choices.**
  - *Cross the room and offer your hand*: their regard +12, yours to them +8, stress +4 (it costs you something to do it).
  - *Hold their eye until they look away*: legitimacy +1, their regard -5, stress -2.
  - *Leave before it comes to words*: nothing changes.
  - *Have someone stand near them all evening* (needs intrigue 3, or Schemer or Paranoid): influence +2, their regard -3.
- **Numbers.** An old enemy is one at -40 or worse; they stop being one above -25. +12 is half the way back: two such evenings, or
  one and time, end it. Legitimacy +1, the smallest step, for being seen unbowed. Influence +2 for what is overheard, like *Ask What
  They Are Hearing* (+1) from a friend, doubled because an enemy says more.
- **How it differs.** *The Enemy Is Dead* is the death of one. This is one still alive, and the only report that can make an old
  enemy less of one.

### 3. Words at the Feast (`words_at_the_feast`) (thread 1, step 1)
- **What happened.** At another house's feast, a guest who thinks badly of you says something clever about your house, loudly.
- **When it fires.** Second year or later. Situation, weight 0.7, repeatable.
- **Roles.** `envious` (kept outside your house by the binder): thinks -15 or worse of you, 20 or older, Proud, Arrogant, Cruel or
  Vengeful, and not already in a quarrel with you.
- **Choices.**
  - *Laugh with the room, and let it pass*: stress +3, their regard +4. It ends there.
  - *Answer them in kind, before everyone* (needs oratory 2 or Orator): legitimacy +2, their regard -12, and the quarrel is open
    (`quarrel_with_you`).
  - *Take it up with them in the morning*: their regard -6, stress +2, and the quarrel is open.
- **Numbers.** Legitimacy +2 for a public answer that lands (the room repeats yours, not theirs), like *Take the Credit*'s +1 doubled
  for being before a whole feast; gated, so the ungated way into the quarrel costs stress instead. -12 and -6: a public answer
  wounds more than a private one.
- **How it differs.** Nothing in the game begins a quarrel short of a death. This is the first step of one.

### 4. The Quarrel Comes to a Head (`the_quarrel_comes_to_a_head`) (thread 1, step 2)
- **What happened.** The one you quarrelled with has not let it rest. It has gone past words.
- **When it fires.** Someone holds `quarrel_with_you`. Situation, weight 15, repeatable (each choice clears the mark, or the person
  dies).
- **Roles.** `envious`: char flag `quarrel_with_you`.
- **Choices.**
  - *Send a gift, and an end to it* (needs 400 credits): credits -4, their regard +15.
  - *Meet them with blades, to first blood* (Duelist only): legitimacy +2, their regard +5 (a fair fight is respected), stress +4.
  - *Have it ended for good* (Ruthless or Cruel only): they die by your hand (`kill`, `by: self`), legitimacy -2, stress +6. Their
    house now holds blood against yours, and the feud reports take it from there (*A Death Counted Against Us*, the price, the
    challenge).
  - *Let them talk until they tire of it*: their regard -6, legitimacy -1.
- **Numbers.** 400 credits as *Double the Watch* (400) and *Send a Gift* (500) in the existing reports. +15 regard: a gift ends a
  quarrel, it does not make a friend. Legitimacy +2 for first blood before witnesses; -2 for a killing over words, which nobody
  admires even when they fear it. Stress +6 is *Answer in Kind* (the knife traced home)'s figure.
- **How it differs.** The feud's *Called Out* is a challenge over a death; this is the quarrel before any death, and its fourth
  answer feeds the feud.

### 5. A Veteran of the Old Wars (`a_veteran_of_the_old_wars`)
- **What happened.** An old officer of another house, years from any command, talks over dinner about the wars of their youth.
- **When it fires.** Second year or later. Ambient, weight 0.8, repeatable.
- **Roles.** `veteran`: on the navy's track, Captain or higher (rank 4), 58 or older, not commanding a fleet now (no `admiral`
  mark), `inLine` false against you.
- **Choices.**
  - *Ask how the old battles were won*: tactics +1, their regard +4.
  - *Ask them to speak for you in the wardrooms* (needs their regard 10 or more): the officers' estate +4.
  - *Ask them to join your household* (needs their regard 20 or more): they come as a companion if the household has room (`takeIn`).
  - *Listen, and go home*: stress -3.
- **Numbers.** A skill by 1, as *Inspect It* (*The Review of the Fleet*, also ambient). The officers +4 is under *Promise Hulls*
  (+5 for 500 credits): a word, not money. The household rules give the companion's regard and bond.
- **The name.** Not "The Old Admiral": the veteran may be a Captain, and "officer" in a title opens the event on the Senate's painting
  (the scene is chosen by words in the id and title, and "office" is one of the Senate's). It opens on the study with a visitor.
- **How it differs.** *The Old Retainer* is your own servant's last task; this is a stranger with a long memory and a name in the
  wardrooms. It is the only report that brings a grown officer of another house into yours.

### 6. A Painter's Great Canvas (`a_painters_great_canvas`)
- **What happened.** A painter of name has a public hall's great wall to fill with the houses of the Reach, and a rival of yours has
  already asked for the centre.
- **When it fires.** Fourth year or later. Ambient, weight 0.3, repeatable.
- **Roles.** `rival`: thinks -10 or worse of you, 25 or older, `inLine` false against you.
- **Choices.**
  - *Pay for the centre* (needs 600 credits): credits -6, legitimacy +2, their regard -8.
  - *Stand where the painter puts you*: nothing changes.
  - *Ask to stand beside them, as equals*: their regard +8, stress +2.
  - *A purse for the painter, and they end up small at the back* (Schemer, or intrigue 3): credits -2, legitimacy +1, their regard -12.
- **Numbers.** 600 credits and +2 against *The Portrait*'s 400 and +1: a public wall is seen by more than a hall. The rival's -8 and
  -12: being moved aside in public stings; being made small stings more.
- **How it differs.** *The Portrait* is once and private; this is public, repeatable, and about a rival.

### 7. A Merchant Prince's Venture (`a_merchant_princes_venture`) (thread 3, step 1)
- **What happened.** Someone of one of the richest houses is raising a venture and wants partners with a name.
- **When it fires.** Third year or later. Situation, weight 0.45, repeatable.
- **Roles.** `grandee` (outside your house): of a house in the top sixth by wealth (`wealth` opulent), 30 or older, thinks no worse
  than -10 of you, not already holding your money.
- **Choices.**
  - *Put money in* (needs 1,000 credits): credits -8, their regard +6, and your stake is with them (`staked_by_you`).
  - *Lend your name to the charter, not your money*: credits +3, legitimacy -1.
  - *Ask to see how it is put together*: economics +1, their regard +2.
  - *Decline*: their regard -2.
- **Numbers.** 800 credits in, 1,200 back (step 2): half as much again over the years it takes, against *Lend It on Terms*, where 1,500
  lent to the in-laws buys 2 influence. +300 credits for a name, -1 legitimacy for selling it. The 1,000 credits asked of the treasury before 800 is put in
  keeps the stake from emptying a poor house.
- **How it differs.** The existing money reports are your own house's troubles or an estate's gift. This is a bet on another
  house's fortune.

### 8. The Venture Comes Home (`the_venture_comes_home`) (thread 3, step 2a)
- **When it fires.** Someone holds `staked_by_you`, their house is still rich or better (`wealth` rich), and not at war with yours.
  Situation, weight 12, repeatable (each choice clears the mark).
- **Choices.**
  - *Take your share*: credits +12.
  - *Take your share, and a place at their table*: credits +10, their regard +10.
  - *Keep what you put in, and give the rest to the city*: credits +8, the commons +4, legitimacy +1.
- **Numbers.** +12 is the stake (8) and half again. Two hundred less buys +10 regard, the rate of the regard bought for money
  elsewhere (*Send a Gift*: 500 for +10). Giving the 400 of profit away buys the commons +4 and +1 standing: *Send Wine*'s commons
  +5 for 300 credits, a little dearer because the city's poor are not the commons' feast.

### 9. The Venture Goes Down (`the_venture_goes_down`) (thread 3, step 2b)
- **When it fires.** Someone holds `staked_by_you`, and either their house is no longer among the rich (comfortable or worse) or it
  is at war with yours. Situation, weight 15, repeatable (each choice clears the mark). Weightier than 8 because it is news.
- **Choices.**
  - *Write it off*: stress +2, their regard +10 (they will remember you did not press them).
  - *Press for what is owed*: credits +3, their regard -15.
  - *Take it in kind: goods, a debt, a name on a letter* (needs economics 3): credits +5, their regard -5.
- **Numbers.** Of the 800, 300 come back by pressing and 500 by knowing how to: a loss either way, so the first step was a real bet.

### 10. A House Without Lands (`a_house_without_lands`) (thread 2, step 1)
- **What happened.** Someone of a house that holds no world comes to you with a good coat worn thin.
- **When it fires.** Third year or later. Situation, weight 0.5, repeatable.
- **Roles.** `claimant` (outside your house): of a house with no world (`holding` of kind planet, none), 25 or older, not already
  owing you a favour.
- **Choices.**
  - *Lend them coin* (needs 300 credits): credits -3, their regard +12, the old blood +2, and they owe you a favour (thread 2).
  - *Take them into your household*: they come as a companion if there is room (`takeIn`).
  - *A kind word, and nothing more*: their regard +3.
  - *Turn them away*: their regard -8, the old blood -2.
- **Numbers.** 300 credits: enough to keep up appearances for a season. The old blood ±2, the smallest estate step in the existing
  reports, for how the old names see a name helped or shut out.
- **How it differs.** *The Empty Hall* (`situations.json`) is once, when a whole house has fallen and its people need a roof. This is
  a person of a living house that has lost its worlds, and it is repeatable.

### 11. A Favour Repaid (`a_favour_repaid`) (thread 2, step 2)
- **When it fires.** Someone holds `owes_you_a_favour`. Situation, weight 10, repeatable.
- **Roles.** `debtor` (outside your house): char flag `owes_you_a_favour`.
- **Choices.**
  - *Ask for their house's voice when you need it*: influence +2.
  - *Ask for the coin back, and a little over*: credits +4, their regard -2.
  - *Ask nothing: it was never a debt*: their regard +12, legitimacy +1.
  - *Not yet. Keep it owed*: nothing; it can come again in a few years.
- **Numbers.** Each answer gives back a little more than the first step spent: influence 2 for 1, 400 credits for 300.

### 12. A Secret for Sale (`a_secret_for_sale`)
- **What happened.** A stranger has proof of something a person you are at odds with would not want known.
- **When it fires.** Third year or later. Situation, weight 0.4, repeatable.
- **Roles.** `target`: you think -10 or worse of them, or they think -15 or worse of you; 25 or older; `inLine` false against you.
- **Choices.**
  - *Buy it, and keep it* (needs 400 credits): credits -4, influence +2.
  - *Buy it, and let it be known* (needs 400 credits): credits -4, their house's legitimacy -2, their regard -20.
  - *Buy it, and send it to them unread* (needs 400 credits): credits -4, their regard +15.
  - *Send the seller away*: nothing.
- **Numbers.** 400 credits, what *Double the Watch* costs in *A Death Counted Against Us*. -2 to another house's standing is the
  largest thing here: a scandal, twice the smallest step. -20 regard because they will know it was you. +15 because it is a large
  thing to be handed.
- **How it differs.** The council's spymaster brings you what your own agents found; here a stranger sells it, and the question is
  whether to use it, hold it, or give it back.

### 13. A Word Off the Record (`a_word_off_the_record`)
- **What happened.** Someone of a house whose people think badly of you asks to see you, alone, with nothing written down.
- **When it fires.** Third year or later. Situation, weight 0.5, repeatable.
- **Roles.** `envoy` (outside your house): thinks -15 or worse of you, 30 or older, their house not at war with yours.
- **Choices.**
  - *Come to an understanding, and give a little ground* (needs 1 influence): influence -1, their regard +15, yours to them +5.
  - *Ask what their house will give to mend it*: credits +3, their regard +5.
  - *Let it be known that they came to you*: legitimacy +1, their regard -15.
  - *Hear them out, and promise nothing*: their regard -3.
- **Numbers.** +15 for a concession, as a gift ends a quarrel. 300 credits for a peace they buy, the smallest money here. Legitimacy
  +1 and -15 for making their asking public: you look strong and they look small.
- **How it differs.** Not a war (*The Enemy's Envoy* is), and not your own envoy (the council's). A house that is not yet an enemy
  trying to keep it so.

### 14. A Patron Wanted (`a_patron_wanted`) (thread 4, step 1)
- **What happened.** A young person of a lesser house, with no rank yet, asks you to put your name behind them.
- **When it fires.** Third year or later. Ambient, weight 0.6, repeatable.
- **Roles.** `young`: 17 to 26, no rank yet (rank 0), of a house no better than comfortable (`wealth`), thinks no worse than -10 of
  you, `inLine` false against you, not already your protégé.
- **Choices.**
  - *Be their patron* (needs 1 influence): influence -1, their regard +15, and they are yours to watch (`your_protege`).
  - *Take them into your household as a protégé*: if there is room (`takeIn` as protege).
  - *Write a letter for them, and wish them well*: their regard +5.
  - *Say you have no time for it*: their regard -5.
- **Numbers.** 1 influence for patronage, the word put in for a classmate.

### 15. The One You Stood For (`the_one_you_stood_for`) (thread 4, step 2)
- **When it fires.** Someone holds `your_protege`, has reached their first rung (rank 1 or more) and is 23 or older. Situation,
  weight 12, repeatable (each choice clears the mark). Rivals climb a rung every nine years or so (`DynastySystem`), so it comes a
  few years after the first step.
- **Roles.** `risen`: char flag `your_protege`, rank 1 or more, 23 or older.
- **Choices.**
  - *Swear friendship* (needs their regard 15 or more, what the first step gave, and no sworn or blood bond between you): a sworn
    bond of 50 each way, their regard +10. A sworn friend outside your house is the one who takes up your banner if your line ends
    in a game played on (the continue mode, `DynastySystem.FindSwornSuccessor`).
  - *Call on them now*: influence +2, their regard -4.
  - *Ask nothing, and let them go their own way*: their regard +4, legitimacy +1.
- **Numbers.** A bond of 50 is what a companion taken into the house gets, and more than the classmate's 40: this one was earned.
  Influence 2 back for 1 spent, as in the favour thread.

### 16. Two Guests at Odds (`two_guests_at_odds`)
- **What happened.** At your own dinner, two guests whose houses have blood between them, and one place of honour left.
- **When it fires.** Second year or later. Situation, weight 2, repeatable. It needs a person held to blame for a death between two
  other houses (`blood_on_their_hands`, the feud system's mark) and someone of the dead person's house who thinks -20 or worse of
  them. The binder fills roles in order and does not look ahead, so the rare one is bound first.
- **Roles.** `first`: `blood_on_their_hands`, `inLine` false against you. `second`: thinks -20 or worse of `first`, `inLine` false.
- **Choices.**
  - *Give the place to {first}*: first +8, second -8.
  - *Give it to {second}*: second +8, first -8.
  - *Give it to neither, and seat them far apart*: both -3, stress +2.
  - *Bring them together over the wine* (Diplomat, or diplomacy 3): each thinks 10 better of the other, both think 5 better of you,
    stress +3.
- **Numbers.** ±8 is the elder's peace (6) and a little: a seat at table is public. A Diplomat can move two others' regard of each
  other, which nothing else in the game does by an event.
- **How it differs.** The feud reports are about your own house's blood. This is other houses' blood, and you are the host.

## What was cut, and why

Read as a critic would, before building:
- **A preacher in the square** and **a pilgrim**: no person to bind, and the choices were the estate reports again (the commons
  against the old blood). Cut.
- **A gossip**: the decision was "pay or laugh", with nothing behind it. The useful part, hearing something about someone, is *A
  Secret for Sale*. Cut.
- **A gambler**: it was the favour thread with dice. The favour thread kept its two better doors. Cut.
- **A day at the games** and **a ball**: two more invitations. The feast (*Words at the Feast*) and your own table (*Two Guests at
  Odds*) are where the social meetings live, each with a real choice. Cut.
- **The painter** first had only "sit or refuse", which is *The Portrait*. It was kept only once a rival was in it.
- **The venture** first had one ending. A second, the loss, was added so the first step is a bet and not a coupon.
- **The quarrel** first ended in a duel with a roll, which events cannot make. It ends in first blood only for a Duelist; the rest
  choose a gift, a killing, or letting it harden.

## What the engine already marks (no Core change)

- `has_old_enemy` (world flag) and `old_enemy` (char flag) are set each year by `LifeSystem.Tick` for anyone who has thought -40 or
  worse of you for eight years (`OldTieYears`); both are cleared and set again each year, so the person is always marked while the
  flag is up. An old enemy may be of your own house (only friends are kept outside it).
- The `cohort` condition reads `World.Academy.Cohort`, which is kept after the Academy closes, so classmates stay bindable for life.
- `blood_on_their_hands` is the feud system's mark on the one held to blame for a death (`FeudSystem.Blood`), and the dead person's
  house is set 30 worse against them (`KinRegard`), so *Two Guests at Odds* always has a second guest when it has a first.
- A `kill` with `by: self` opens a blood debt (`FeudSystem.Blood`), which is how *The Quarrel Comes to a Head* feeds the feud.
- `wealth` is a house's rank among all houses by wealth (`Wealth.TierFor`): opulent is the top sixth, rich the top four in ten.
- `takeIn` follows the household rules: nothing happens if the household is full or the houses are at war, and the household's own
  line in the chronicle says who came.

## Measured: the soak

`qa --scenario all --seeds 2` (22 games of sixty years, and the three players with an end in view on every start), against the
baseline soak of the same commit without this file (`/tmp/claude-0/ev-baseline.log`). It passed, with no failures and no unresolved
token.

- **All sixteen fired.** Reports in all: 7,975 (7,891 in the baseline). This file: **233, 2.9% of all reports**, sixteen of sixteen
  kinds. None of them is among the thirty most fired; the most fired of them takes 0.4%.

| Report | Fired | Share |
|---|---|---|
| Words at the Feast | 30 | 0.4% |
| A Merchant Prince's Venture | 27 | 0.3% |
| A Patron Wanted | 25 | 0.3% |
| A House Without Lands | 24 | 0.3% |
| A Veteran of the Old Wars | 22 | 0.3% |
| An Old Enemy Across the Room | 22 | 0.3% |
| A Secret for Sale | 19 | 0.2% |
| A Word Off the Record | 17 | 0.2% |
| The Quarrel Comes to a Head | 14 | 0.2% |
| A Painter's Great Canvas | 11 | 0.1% |
| A Classmate in the City | 7 | 0.1% (two starts of eleven have classmates) |
| Two Guests at Odds | 7 | 0.1% (needs a blood debt between two other houses) |
| A Favour Repaid | 3 | under 0.1% |
| The One You Stood For | 3 | under 0.1% (needs the protégé's first rung, years later) |
| The Venture Comes Home | 1 | under 0.1% |
| The Venture Goes Down | 1 | under 0.1% |

- **The second steps are rare because the first steps that arm them are a choice among three or four**, made at random by the soak's
  player, and often closed to it (*Put Money In* asks for 1,000 credits in the treasury). The tests drive each thread from the first
  step to the second, both endings of the venture included.
- **What the other files lost.** The 2.9 points came mostly from the files with many reports always in reach: the ladder (10.0% to
  9.2%), echoes (3.9% to 3.3%), the household (3.5% to 2.9%), the Senate (8.1% to 7.7%), the nations (7.9% to 7.6%) and courtship
  (5.6% to 5.3%). Some files rose a little (life 3.9% to 4.2%, rings, wealth, military), which is the world going differently, not
  this file: a report drawn changes the year after it.
- **The hook.** `has_old_enemy` is off the soak's list of flags the engine sets and no content reads.
- **Choices.** Of the 60 choices, the soak's player never took 13: most are gated (a Duelist's first blood, the friendship sworn,
  the venture in kind), or are on the rare second steps.

## Outside these files

`tests/EarnedTitlesTests.cs`, three lines (a comment and two calls). Two of its tests play the Old Lion's start (a head of 68) for two and thirty years and then
act as the head they began with. Any new report that binds a person changes the year's draws, and with this file the old head dies
at 70 in that seed, so the tests acted as a dead man ("no one to ask", "no house"). They now act as whoever holds the seat, which is
what they meant. Nothing else outside the four files changed.
