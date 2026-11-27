# Chance: the people of the Reach crossing your path

The owner asked for events for "random interactions". These seventeen reports are the people you meet without meaning to: a
classmate from the Academy, an old enemy across a room, a guest at a feast who says too much, an old officer at dinner, a painter
with a wall to fill, a merchant prince with a venture, someone of a house that has lost its worlds, a seller of secrets, a quiet
word from a house you are at odds with, a young person who needs a patron, and two guests at your table with blood between their
houses. Most are one evening and move one person's regard; five of them start something that comes back.

Files: `content/events/encounters.json`, `content/text/encounters.json`, `tests/EncounterEventsTests.cs`. One Core change (round
2): the chance roles' names in `Binder.OutsideParts`, so nobody of your house is met as a stranger. Every person here is bound by a
condition the engine already has (see "What the engine already marks", at the end). One change outside these files, three lines in
`tests/EarnedTitlesTests.cs`, is explained under "Outside these files".

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
3. **The venture** (two steps, two endings). *A Great Merchant's Venture* takes your money into a rich house's trade in exotics
   (`staked_by_you`). *The Venture Comes Home* pays it back with half as much again in a year exotics sell a tenth dearer than the
   year before, while their house stays rich and at peace with yours; *The Venture Goes Down* comes instead if exotics fall a tenth on
   the year, or their house falls out of the richest, or goes to war with yours. The two cannot both hold.
5. **The secret** (two steps, round 2). *A Secret for Sale*, bought and kept, marks the person it is about (`you_hold_their_secret`).
   When they turn on you (they think -30 or worse of you, or move a censure or embargo of your house), *The Secret You Hold*: use it,
   sell it back to their house, or burn it.
4. **The patron** (two steps). *A Patron Wanted* can put your name behind a young person of a lesser house who has no rank yet
   (`your_protege`). When they reach their first rung, *The One You Stood For*: a friendship sworn (`addBond sworn`, both ways), a
   favour called in at once, or nothing asked.

Twelve of the seventeen are in a thread (two only as the first step of the favour thread); five stand alone.

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

A person here is bound outside your house by a role name the binder keeps outside it (`Binder.OutsideParts`): `envious`, `grandee`,
`claimant`, `debtor`, `envoy`, and since round 2 `classmate`, `veteran`, `young`, `sitter` (the painter's rival), `subject` (the one a
secret is about) and `guest` and `other_guest` (the two at your feast). Those roles also kept `inLine` false against you, which lets
in one of your own house struck from the line (`DisownSystem`): such a relative could be met as a stranger "of {house}", your own,
and a secret used against them cost your own house its standing. Round 2 renamed the painter's `rival`, the secret's `target` and the
feast's `first` and `second`, whose names other files use for parts that may be of your house. Every role here also has the char flag
`foreign` false (round 2): a nation on the map embodies its ruler and commanders as people of the nation's house
(`NationSystem.Embody`), and nothing else kept a nation's First Admiral from your dinner table, your venture or your feud.

## The events

### 1. A Classmate in the City (`a_classmate_in_the_city`)
- **What happened.** Someone who was in your year at the Academy (the Institute, under the Imperium) is in the same room as you,
  years later, and has come over.
- **When it fires.** You passed out of the Academy (`academy_graduated`), it is the third year or later, and a classmate (`cohort`)
  is alive, grown, and not of your line. Ambient, weight 2, repeatable. Only the two Academy starts have a year of classmates, so
  it is weightier than the rest.
- **Roles.** `classmate` (outside your house): `cohort` present.
- **The school.** The text says "a cadet of your year" and never names the school: `{era:Academy|Institute}` follows the era now,
  and the school is the one you went to, so a graduate of the Academy was said to have been at the Institute once the Imperium was
  proclaimed (round 2).
- **Choices.**
  - *Put in a word for them where it counts* (needs 1 influence): influence -1, their regard +12, and they owe you a favour
    (`owes_you_a_favour`, thread 2).
  - *Ask them to help you bring your old year together for a dinner* (300 credits, round 2): their regard +6, legitimacy +1 (the city
    sees how far the year has come). It replaced *an evening of old stories*, which with *ask how their house stands* made two of four
    answers a copy of *An Old Friend*'s evening; only a classmate can give you your year.
  - *Ask how their house stands*: your intrigue +1, their regard -3 (they notice what you came for). Round 1: it gave influence
    from nowhere; listening closely is a skill learned.
  - *Swear the old friendship again* (needs their regard 30 or more, and no sworn or blood bond between you either way): a sworn
    bond of 40 each way, their regard +6.
- **Numbers.** 1 influence for a word, as *A Child with Nowhere to Go* charges to find a place. +12 is under *The Ranks That Won*
  (+15, which costs 600 credits). The year's dinner: 300 credits for +1 legitimacy, as a gift from the house worthy of a grandchild
  (500 for +1 and +5 regard). A sworn bond of 40: above
  the 25 a friendship sworn in the Academy year starts at (`academy_republic.json`), because this one has lasted, and under the 50 a
  companion taken into the house gets.
- **How it differs.** The remembered-Academy reports are once each and tied to a place in the order of merit; this is any classmate,
  again and again, and it feeds two threads.

### 2. An Old Enemy Across the Room (`an_old_enemy_across_the_room`)
- **What happened.** Someone who has thought ill of you for years (eight at the least, `LifeSystem.OldTieYears`) is at the same
  gathering. The text says "years", not "eight years": the enmity can be twenty years old.
- **When it fires.** `has_old_enemy` is up (the engine raises it each year an old enemy lives) and the old enemy is bound by their
  mark. Situation, weight 0.85, repeatable. It is eligible every year one lives, so it is kept low.
- **Roles.** `enemy`: char flag `old_enemy` (the engine's own mark, cleared and set again each year), and not of your house
  (`inLine` false, round 1: the engine marks old enemies inside the house too, and "across the room at another house's evening"
  is not a relative).
- **Choices.**
  - *Cross the room and offer your hand*: their regard +12, yours to them +8, stress +4 (it costs you something to do it).
  - *Hold their eye until they look away*: legitimacy +1, their regard -5, stress -2.
  - *Leave before it comes to words*: nothing changes.
  - *Have someone stand near them all evening* (needs intrigue 3, or Schemer or Paranoid): your intrigue +1, their regard -3
    (round 1: it gave 2 influence from nowhere).
- **Numbers.** An old enemy is one at -40 or worse; they stop being one above -25. +12 is half the way back: two such evenings, or
  one and time, end it. Legitimacy +1, the smallest step, for being seen unbowed. What is overheard teaches the trade (intrigue +1)
  rather than paying in influence.
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
  - *Have {envious} killed* (Ruthless or Cruel only; round 1: the label said "have it ended for good", and the chips did not
    show a death; now it says the killing and the chips show "{name} is killed"): they die by your hand (`kill`, `by: self`), legitimacy -2, stress +6. Their
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
- **Roles.** `veteran` (outside your house, not a nation's): on the navy's track, Captain or higher (rank 4), 58 or older, not
  commanding a fleet now (no `admiral` mark). The body says they "rose to {rankName}" (round 2: "{rankName} of the fleet" read "Fleet
  Admiral of the fleet").
- **Choices.**
  - *Ask how the old battles were won, and go over the plots with them till late*: tactics +1, their regard +4, your stress +2.
  - *Ask them to speak for you in the wardrooms* (needs their regard 10 or more; 1 influence): the officers' estate +4.
  - *Ask them to join your household* (needs their regard 20 or more): they come as a companion if the household has room and they
    are free to come (`takeIn`, which now refuses the head of a house and one married into another), and the house keeps them.
  - *Listen, and go home*: stress -3, their regard +2.
  Round 1: every answer had been a gift with nothing given up. The lesson costs a late night, the word in the wardrooms costs the
  year's influence, and the companion is a mouth the household feeds.
- **Numbers.** A skill by 1, as *Inspect It* (*The Review of the Fleet*, also ambient). The officers +4 is under *Promise Hulls*
  (+5 for 500 credits): a word, not money. The household rules give the companion's regard and bond.
- **The name.** Not "The Old Admiral": the veteran may be a Captain, and "officer" in a title opens the event on the Senate's painting
  (the scene is chosen by words in the id and title, and "office" is one of the Senate's). It opens on the study with a visitor.
- **How it differs.** *The Old Retainer* is your own servant's last task; this is a stranger with a long memory and a name in the
  wardrooms. It is the only report that brings a grown officer of another house into yours.

### 6. A Painter's Great Canvas (`a_painters_great_canvas`)
- **What happened.** A famous painter has a public hall's great wall to fill with the houses of the Reach, and a rival of yours has
  already asked for the centre.
- **When it fires.** Fourth year or later. Ambient, weight 0.3, repeatable.
- **Roles.** `sitter` (outside your house; `rival` before round 2): thinks -10 or worse of you, 25 or older.
- **Choices.**
  - *Pay for the centre (600 credits)*: credits -6, legitimacy +2, their regard -8.
  - *Stand where the painter puts you*: nothing changes.
  - *Ask to stand beside them, as equals*: their regard +8, stress +2.
  - *Pay the painter to put them small, at the back* (Schemer, or intrigue 3; 200 credits): credits -2, legitimacy +1, their regard -12.
- **Numbers.** 600 credits and +2 against *The Portrait*'s 400 and +1: a public wall is seen by more than a hall. The rival's -8 and
  -12: being moved aside in public stings; being made small stings more.
- **How it differs.** *The Portrait* is once and private; this is public, repeatable, and about a rival.

### 7. A Great Merchant's Venture (`a_merchant_princes_venture`) (thread 3, step 1; retitled in round 1: the Republic has no princes)
- **What happened.** Someone of one of the richest houses is raising a venture and wants partners with a name.
- **When it fires.** Third year or later. Situation, weight 0.45, repeatable.
- **Roles.** `grandee` (outside your house): of a house in the top sixth by wealth (`wealth` opulent), 30 to 65 (round 2: the stake
  sits on the person, and one who dies before the market moves takes it with them, so the old are not offered), thinks no worse
  than -10 of you, not already holding your money.
- **Choices.**
  - *Put in 800 credits. In a good year it comes back with half as much again.*: credits -8, their regard +6, and your stake is with
    them (`staked_by_you`). Round 1: it asked for 1,000 in the treasury to put 800 in. Round 2: the label was 156 characters of
    conditions; the conditions are in the body now.
  - *Lend your name to the charter for a fee from their house* (needs their house to have 300): 300 credits from their treasury to
    yours, legitimacy -1 (round 2: the fee came from nowhere).
  - *Ask to see how it is put together*: economics +1, their regard +2.
  - *Decline*: their regard -2.
- **Numbers.** 800 credits in, 1,200 back (step 2): half as much again over the years it takes, against *Lend It on Terms*, where 1,500
  lent to the in-laws buys 2 influence. +300 credits for a name, -1 legitimacy for selling it.
- **How it differs.** The existing money reports are your own house's troubles or an estate's gift. This is a bet on another
  house's fortune.

### 8. The Venture Comes Home (`the_venture_comes_home`) (thread 3, step 2a)
- **When it fires.** In a year exotics sell a tenth dearer than the year before (`price`, exotics, 110% against the year; round 2),
  someone holds `staked_by_you`, their house is still among the richest (`wealth` opulent), and not at war with yours. Situation,
  weight 35, as the other second steps, repeatable (each choice clears the mark).
- **Round 2.** It read `price_spike`, which is up whenever any one good is at half again its base: in the probe 841 years of 938,
  with `price_slump` in 679 of them, so both endings were eligible three years in four and the outcome was a draw between two cards,
  not the market. Exotics against last year rose a tenth in about one year in ten of a 60-year probe, and fell a tenth about as
  often; the two cannot hold at once, and the text names the good. (Exotics never stood 15% over their base in that probe: they
  settle below it, so the year-on-year move is the one that means a good year.)
- **Round 1.** It had weight 12 on no moment, and the house had only to stay rich, so the return came years late or never (in the
  soak neither ending fired though the stake was taken) and, when it came, was nearly sure. Now it comes in a good market year, and
  only while the house is still opulent: a real bet on the house and on the market.
- **Choices.**
  - *Take your share*: credits +12.
  - *Take your share, and a place at their table*: credits +10, their regard +10.
  - *Keep what you put in, and give the rest to the city*: credits +8, the commons +4, legitimacy +1.
- **Numbers.** +12 is the stake (8) and half again. Two hundred less buys +10 regard, the rate of the regard bought for money
  elsewhere (*Send a Gift*: 500 for +10). Giving the 400 of profit away buys the commons +4 and +1 standing: *Send Wine*'s commons
  +5 for 300 credits, a little dearer because the city's poor are not the commons' feast.

### 9. The Venture Goes Down (`the_venture_goes_down`) (thread 3, step 2b)
- **When it fires.** Someone holds `staked_by_you`, and either their house has fallen from the richest (rich or worse), or it is at
  war with yours, or exotics have fallen a tenth on the year (round 2: it was `price_slump`, any good). Situation, weight 35,
  repeatable (each choice clears the mark). It cannot share a year with *The Venture Comes Home*.
- **Choices.**
  - *Write it off*: stress +2, their regard +10 (they will remember you did not press them).
  - *Press their house for what is owed* (needs it to have 300): 300 credits from their treasury to yours, their regard -15.
  - *Take it in kind: goods, a debt, a name on a letter* (needs economics 3, and their house to have 500): 500 credits from their
    treasury, their regard -5. Round 2: both came from nowhere, against round 1's rule that money another house pays leaves it.
- **Numbers.** Of the 800, 300 come back by pressing and 500 by knowing how to: a loss either way, so the first step was a real bet.

### 10. A House Without Lands (`a_house_without_lands`) (thread 2, step 1)
- **What happened.** Someone of a house that holds no world comes to you with a good coat worn thin.
- **When it fires.** Third year or later. Situation, weight 0.5, repeatable.
- **Roles.** `claimant` (outside your house): of a house with no world (`holding` of kind planet, none), 25 or older, not already
  owing you a favour.
- **Choices.**
  - *Lend them 300 credits* (needs 300 credits): credits -3, their regard +12, the old blood +2, and they owe you a favour (thread 2).
  - *Ask them to join your household*: they come as a companion if there is room and they are free to come (`takeIn`: a landless
    claimant of 25 or more is often the head of what is left of their house, and the head of a house does not leave it).
  - *Give them a kind word, and nothing more*: their regard +3.
  - *Turn them away*: their regard -8, the old blood -2.
  Round 1: each label is an action, and the two that move the old blood say so.
- **Numbers.** 300 credits: enough to keep up appearances for a season. The old blood ±2, the smallest estate step in the existing
  reports, for how the old names see a name helped or shut out.
- **How it differs.** *The Empty Hall* (`situations.json`) is once, when a whole house has fallen and its people need a roof. This is
  a person of a living house that has lost its worlds, and it is repeatable.

### 11. A Favour Repaid (`a_favour_repaid`) (thread 2, step 2)
- **When it fires.** Someone holds `owes_you_a_favour`. Situation, weight 10, repeatable.
- **Roles.** `debtor` (outside your house): char flag `owes_you_a_favour`.
- **Choices.**
  - *Ask for their house's voice when you need it*: influence +2. (A vote promise: the words promise a house's voice and the choice
    gives influence now. Left to the vote-promises phase, `promises-brief.md`, so there are not two designs.)
  - *Ask for 400 credits, and call it settled* (needs their house to have 400 credits): 400 credits from their treasury to yours,
    their regard -2. Round 1: the label asked for "the coin back", though a classmate's favour lent no coin; and the money came from
    nowhere.
  - *Ask nothing: it was never a debt*: their regard +12, legitimacy +1.
  - *Not yet. Keep it owed*: nothing; it can come again in a few years.
- **Numbers.** Each answer gives back a little more than the first step spent: influence 2 for 1, 400 credits for 300.

### 12. A Secret for Sale (`a_secret_for_sale`)
- **What happened.** A stranger has proof of something a person you are at odds with would not want known.
- **When it fires.** Third year or later. Situation, weight 0.4, repeatable.
- **Roles.** `subject` (outside your house; `target` before round 2): you think -10 or worse of them, or they think -15 or worse of
  you; 25 or older; you do not already hold a secret of theirs.
- **Choices.**
  - *Buy it, and keep it for the day you need it* (needs 400 credits): credits -4, marks them `you_hold_their_secret` (round 2: it
    gave influence +2 and the secret was never heard of again: influence bought with coin, the kind round 1 removed elsewhere).
  - *Buy it, and let it be known* (needs 400 credits): credits -4, their house's legitimacy -2, their regard -20. The label says
    their house's standing falls, with no number (round 1).
  - *Buy it, and send it to them unread* (needs 400 credits): credits -4, their regard +15.
  - *Send the seller away*: nothing.
- **Numbers.** 400 credits, what *Double the Watch* costs in *A Death Counted Against Us*. -2 to another house's standing is the
  largest thing here: a scandal, twice the smallest step. -20 regard because they will know it was you. +15 because it is a large
  thing to be handed.
- **How it differs.** The council's spymaster brings you what your own agents found; here a stranger sells it, and the question is
  whether to use it, hold it, or give it back.

### 12a. The Secret You Hold (`the_secret_you_hold`) (thread 5, step 2; added in round 2)
- **What happened.** You kept a secret about someone, and since then they have turned against your house, openly.
- **When it fires.** A `subject` (outside your house, not a nation's) marked `you_hold_their_secret` who thinks -30 or worse of you,
  or who is moving a censure or embargo of your house this year (`moves_against_you`, VoteMarks). Situation, weight 15, as *The
  Quarrel Comes to a Head*: a second step on a person's flag, not on a moment of the year. Repeatable (each choice clears the mark).
- **Choices.**
  - *Let it be known*: their house's legitimacy -2, their regard -20: what using it at once would have done, now that it is wanted.
  - *Sell it back to their house for 400 credits* (needs their house to have it): 400 from their treasury to yours, their regard -5.
  - *Burn it, and tell them you have*: their regard +15.
- **Numbers.** The three answers of the first step that were not "keep it" (use, return), and the price paid for it, now as a
  choice at the moment it matters: -2 and -20, +15, and the 400 back from them, resented a little.
- **How it differs.** Nothing in the game held a secret over a person until they gave you cause.

### 13. A Word Off the Record (`a_word_off_the_record`)
- **What happened.** Someone of a house whose people think badly of you asks to see you, alone, with nothing written down.
- **When it fires.** Third year or later. Situation, weight 0.5, repeatable.
- **Roles.** `envoy` (outside your house): thinks -15 or worse of you, 30 or older, their house not at war with yours.
- **Choices.**
  - *Come to an understanding, and give a little ground* (needs 1 influence): influence -1, their regard +15, yours to them +5.
  - *Ask what their house will give to mend it* (needs their house to have 300 credits): 300 credits from their treasury to yours,
    their regard -3: they paid, and resent it. Round 1: it was +5 regard and money from nowhere, better than every other answer.
  - *Let it be known that they came to you*: legitimacy +1, their regard -15.
  - *Hear them out, and promise nothing*: their regard -3.
- **Numbers.** +15 for a concession, as a gift ends a quarrel. 300 credits for a peace they buy, the smallest money here, paid by them. Legitimacy
  +1 and -15 for making their asking public: you look strong and they look small.
- **How it differs.** Not a war (*The Enemy's Envoy* is), and not your own envoy (the council's). A house that is not yet an enemy
  trying to keep it so.

### 14. A Patron Wanted (`a_patron_wanted`) (thread 4, step 1)
- **What happened.** A young person of a lesser house, with no rank yet, asks you to put your name behind them.
- **When it fires.** Third year or later. Ambient, weight 0.6, repeatable.
- **Roles.** `young` (outside your house): 17 to 26, no rank yet (rank 0), of a house no better than comfortable (`wealth`), thinks
  no worse than -10 of you, not already your protégé.
- **Choices.**
  - *Be their patron* (needs 1 influence): influence -1, their regard +15, and they are yours to watch (`your_protege`).
  - *Take them into your household as a protégé*: if there is room (`takeIn` as protege).
  - *Write a letter for them, and wish them well*: their regard +5.
  - *Say you have no time for it*: their regard -5.
- **Numbers.** 1 influence for patronage, the word put in for a classmate.

### 15. The One You Backed (`the_one_you_stood_for`) (thread 4, step 2; retitled in round 1: "stood for" is the ladder's word)
- **When it fires.** Someone holds `your_protege`, has reached their first rung (rank 1 or more) and is 23 or older. Situation,
  weight 12, repeatable (each choice clears the mark). Rivals climb a rung every nine years or so (`DynastySystem`), so it comes a
  few years after the first step.
- **Roles.** `risen`: char flag `your_protege`, rank 1 or more, 23 or older.
- **Choices.**
  - *Swear friendship* (needs their regard 15 or more, what the first step gave, and no sworn or blood bond between you): a sworn
    bond of 50 each way, their regard +10. A sworn friend outside your house is the one who takes up your banner if your line ends
    in a game played on (the continue mode, `DynastySystem.FindSwornSuccessor`).
  - *Call in what they owe you, while it is fresh*: influence +2, their regard -4 (round 2: "call on them" read as a visit).
  - *Ask nothing, and let them go their own way*: their regard +4, legitimacy +1.
- **Numbers.** A bond of 50 is what a companion taken into the house gets, and more than the classmate's 40: this one was earned.
  Influence 2 back for 1 spent, as in the favour thread.

### 16. Two Guests at Your Feast (`two_guests_at_odds`)
- **What happened.** At your own feast, one guest is held to blame for a death, and another who cannot abide them is there too,
  and one place of honour is left. Round 1: the text said the death was in the second guest's house, which the role does not check
  (any guest who thinks -20 or worse of the first will do). Round 2: it said the second "has not forgiven it", which still assumed
  their grievance was the death; it now says only that they cannot abide the first, and the Diplomat's chronicle line says they spoke
  civilly "for the first time in years", not "since the death". The title opens the card on the feast painting.
- **When it fires.** Second year or later. Situation, weight 2, repeatable. It needs a person held to blame for a death between two
  other houses (`blood_on_their_hands`, the feud system's mark) and someone of the dead person's house who thinks -20 or worse of
  them. The binder fills roles in order and does not look ahead, so the rare one is bound first.
- **Roles.** `guest` (`first` before round 2): `blood_on_their_hands`. `other_guest` (`second`): thinks -20 or worse of `guest`. Both
  outside your house.
- **Choices.**
  - *Give the place to {guest}*: their regard +8, the other guest's -8.
  - *Give it to {other_guest}*: their regard +8, the first guest's -8.
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

## What the engine already marks

- `has_old_enemy` (world flag) and `old_enemy` (char flag) are set each year by `LifeSystem.Tick` for anyone who has thought -40 or
  worse of you for eight years (`OldTieYears`); both are cleared and set again each year, so the person is always marked while the
  flag is up. An old enemy may be of your own house (only friends are kept outside it).
- The `cohort` condition reads `World.Academy.Cohort`, which is kept after the Academy closes, so classmates stay bindable for life.
- `blood_on_their_hands` is the feud system's mark on the one held to blame for a death (`FeudSystem.Blood`), and the dead person's
  house is set 30 worse against them (`KinRegard`), so *Two Guests at Odds* always has a second guest when it has a first.
- A `kill` with `by: self` opens a blood debt (`FeudSystem.Blood`), which is how *The Quarrel Comes to a Head* feeds the feud.
- `wealth` is a house's rank among all houses by wealth (`Wealth.TierFor`): opulent is the top sixth, rich the top four in ten.
- `takeIn` follows the household rules (round 1: `HouseholdSystem.HardRefusal`): nobody comes into a full household, from a house at
  war with yours, as the head of their own house, or married into another; the chronicle says who came, or who thanked you and
  stayed. This is the one Core change this file needed: the take-in choices could pull another house's head into yours.

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

## Round 1 of review (2026-11-27)

- The test file's `EveryChoice` now asks, for each choice, that the report is one the engine gathers in that world and that the
  people the test chose are ones the binder could cast (alive, found, grown, of another house for an outsider's part, holding each
  part's conditions and the event's): the binding the test builds by hand is checked against the binder's own filters.
- Money paid by another house now leaves that house's treasury (the favour's 400, the word off the record's 300).
- Influence from nowhere is gone: the classmate and the old enemy teach intrigue instead.
- The venture settles in a year the market makes news, at the weight of a second step, and its stake asks only what it takes.
- The take-in choices obey the household's hard refusals.
- Words: the classmate's "passed out", the old enemy's "eight years", the veteran's "of your house", the painter, the merchant, the
  house without lands, the favour, the secret, the one you backed, the two guests.

## Round 2 of review (2026-11-27)

- Strangers are strangers: the chance roles are in `Binder.OutsideParts` (a relative struck from the line is still of your house),
  and no role binds a nation's person (`foreign` false).
- *A Secret for Sale* kept is a secret held, with a second step, *The Secret You Hold*.
- The venture settles on the price of exotics against last year, so its two endings cannot both hold, and the grandee's house pays
  every credit it gives you (the fee, the pressed debt, the debt in kind). The offer is to those of 65 or under.
- *A Classmate in the City*: "a cadet of your year", and an answer only a classmate can give.
- Words: the veteran's rank, the painter's price, the merchant's label, the venture's ending, the one you backed, words at the feast,
  the two guests.
- Left to the vote-promises phase: *A Favour Repaid*'s "their house's voice".

### Measured after round 1

The soak after review (22 games, 7,843 reports) passed. This file took 190 reports (2.4%), fifteen of its sixteen kinds. The stake
was taken this time and `the_venture_goes_down` fired twice; `the_venture_comes_home` did not (it needs a stake, then a year of high
prices while the house is still opulent), and its test covers it.

### Measured after round 2

The soak after round 2 (22 games, 7,996 reports) passed. This file took 207 reports (2.6%), sixteen of its seventeen kinds, none
among the thirty most fired. The new `the_secret_you_hold` fired once. `the_venture_comes_home` did not fire: it needs a stake, then a
year in which exotics sell a tenth dearer than the last (about one year in ten) while the grandee's house is still opulent and at
peace with yours; `the_venture_goes_down` fired once, and the test covers both endings.
