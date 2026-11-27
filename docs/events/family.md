# Family: the events of births, children, the spouse, illness, old age and the will

The owner asked for "some events for family events". These sixteen reports are about the people of your own family as people with
minds of their own: a child of a marriage and what it is called, a grandchild, a child of six whom the other family
asks to raise, a child of sixteen who has chosen a road neither parent took, two of your blood when one has what the other wanted, a marriage marked
every tenth year, a spouse passed over in their own service, a spouse whose family is at war with yours, the one who kept the
house while you were ill and who then asks you to stand aside, those passed over at the reading of a will, and someone who left
the household unpaid and says why.

Files: `content/events/kin.json`, `content/text/kin.json`, `tests/KinEventsTests.cs`. The marks they read are in
`src/Core/Systems/DynastySystem.cs`, `CourtshipSystem.cs` and `HouseholdSystem.cs`, described at the end.

The rule they keep is "one person, not a house" (docs/PROCESS.md): every report is about something one of your people did or
wants, and what you have is influence over it, not control. A child of sixteen has chosen; you may bless it, help it or say it is
wrong, and you cannot change it. The one who kept the house asks you to stand aside; you may, or put them off, and they remember.

## What exists already, and how these differ

Read before writing: `life.json`, `household.json`, `found_family.json`, `courtship.json`, `matches.json`, `succession.json`,
`ambitions.json`, `education.json`, `wealth.json`, `feud.json`, `echoes.json`, `court.json`.

- Births. Nothing reads a birth. *The Nurse at the Cradle* (`court.json`) needs a nurse in the house and is about her. Here a
  child of a marriage is named, once a marriage, and a grandchild is born, once for each head.
- Children. *Not Made For It* and *Far Ahead of Their Years* (`education.json`) read how a child takes to lessons; *A Child with
  Nowhere to Go* (`found_family.json`, 2.2% of all reports in the baseline) takes in somebody else's child. Here it is the mirror:
  the other family asks for yours, and the engine's fostering (`hidden`, which only *The Defiant Kin* used, for a grown relative)
  is used for a child, who comes home at sixteen.
- Coming of age. *Coming of Age* (`household.json`) offers the Academy, home or a command, to anyone who has ever come of age.
  *A Life of Their Own* comes the year a child of yours turns sixteen bound for a service neither parent was in
  (`chose_own_road`, round 1), and only for them; *Coming of Age* no longer binds that child in that year, so one child of sixteen
  has one coming-of-age card. The rest, who follow a parent's road, are *Coming of Age*'s, as before these reports.
- Siblings. *Two of Yours Want One Thing* (`ambitions.json`) is the quarrel over a prize before it is won. *Left Behind* is after:
  one has it and another of your blood has not. *What They Wanted* is about the one who has it and you; nothing was about the
  other.
- The spouse. *The Wedding*, *A Cold House*, *A Marriage of Two Minds*, *The Household* and *The In-Laws Ask a Favour* (a loan,
  2.2% of all reports in the baseline) exist. Here: the marriage marked every tenth year; a spouse of another house who has stood
  still in their own service for six years (the ambitions of the house's own people are run by the engine, a spouse's are not);
  and a spouse whose house is at war with yours (the engine strains other houses' marriages in war and leaves the player's to the
  player).
- Old age. *The Physicians' Verdict* (frail, once), *The Abdication* (a set-piece by title), *The Elder at the Table* (after you
  retire). Here the family itself asks, after holding the house while you were ill, and a promise to stand aside comes back.
- The will. *The Will Is Read* (`wealth.json`) is for one the will left half the purse or more. *The Reading of the Will* is for
  those the law passed over at the same reading (round 1: the engine marks them `passed_over_at_the_will` for as long as the reading
  is news; the older `passed_over` lay on anyone ever passed over, at any succession or for a career).
- The household. *The Household Goes Unpaid* is the year the house cannot pay. *A Household Scandal* is the one who left over it
  and what they say in the city.

## The threads

1. **The fostered child** (two steps). *The Other Family Asks for the Child* (at six) may send a child of yours to be raised by
   your spouse's family (`hidden`, `fostered`). The year they turn sixteen and come home, *The Fostered Child Comes Home*.
2. **The one who was left behind** (two steps, and one into existing content). *Left Behind* may tell two of your blood to stand
   together (`stood_together` on both); when one of them fails or is thwarted, *Blood Stands by Blood* comes, with the other as
   the helper only if they think 5 or better of the fallen one (round 1: any two marked would do). Or it takes the side
   of the one who has it, and the other is `slighted`, which *The Slighted Kinsman* (`succession.json`) already answers.
3. **The road they chose** (two steps). *A Life of Their Own* may say it is the wrong road (`doubted_by_the_head`). When that child
   has what they wanted, *You Were Wrong About Them*.
4. **The one who kept the house** (three steps, and two into existing content). *While You Were Ill* may leave the house's work with
   one of your people (`kept_the_house`). When you are old and failing, *They Ask You to Stand Aside*: you may lay the house down
   to them, promise a day (`promised_the_seat`), or refuse (`slighted`, to *The Slighted Kinsman*). A promise comes back as *The
   Day You Named*, where breaking it leaves them `slighted` (to *The Slighted Kinsman*). It made them `defiant`, which the family's
   own turn strikes out that same year from anyone whose regard is 0 or more, so the thread almost never reached a report (round 1).

Nine of the sixteen are in a thread; seven stand alone.

## Weights and deltas

Weights. The existing reports on a marked family moment (*What They Wanted*, *One of Yours Fails*, *The Will Is Read*, *The Heir
Is Named*) weigh 35 to 40 and are five times likelier while the moment is a year old; reports on a state that lasts (*A Cold
House* 14, *The In-Laws Ask a Favour* 9, *An Introduction* 10, *The Household* 3) weigh 3 to 14. These follow: the marked moments
weigh 12 to 40 (the anniversary least, as it comes back every ten years), the second steps of threads 25 to 40 (they need a flag
one of your choices set, so they are eligible far less often), and the lasting states 6 to 20. All are situations; none is a
set-piece.

Deltas. The existing family reports move regard by 3 to 25 (*The Slighted Kinsman* -20, *Coming of Age* +15, *The Suit* -30),
legitimacy by 1 to 3, credits by 3 to 25 (300 to 2,500 credits), stress by 2 to 12 and a marriage's warmth by 3 to 15. These keep
within that: regard 2 to 15 (the hardest, -25, is a promise of the seat broken), legitimacy 1 to 2, credits 3 to 8 (300 to 800
credits), stress 2 to 8, warmth 3 to 10, a skill by 1, one step in rank for someone else. Money in the files is hundreds
(docs/MONEY.md). What a choice does that the choice's chips do not show (a marriage's warmth, a child sent away, a step in rank for
someone else, the seat handed on, what two others think of each other) is said in the choice's own words.

## The moment first

Each report's moment (the mark it reads, or the age and illness of a head) is also the first test of its first role. The binder
binds a report's roles, and draws the dice to choose among those who fit, before it reads the report's own conditions; a role that
fits somebody in any year (a spouse, one passed over at a succession years ago) would draw in every year and move every roll after
it. The first build did that, and three tests that play a seeded game a few years forward (`PlaythroughFixTests`'s save in the
middle of a year, two of `EarnedTitlesTests`) came out differently. With the moment first the binder finds nobody outside the
moment's year and draws nothing, and those tests pass unchanged. The report's own conditions keep the moment as well, because the
Director reads only those for the five-fold weight of fresh news.

## The events

### 1. The Naming of a Child (`the_naming_of_a_child`)
- **What happened.** A child is born to you and your spouse, and is to be named before the house. The text makes no claim to the
  first child (round 1: nothing checked it, and a couple that start a game as parents had their next child called their first).
- **Fires when.** `born_to_the_house` (Core, a year), a `spouse` (married to you) who is not yet marked `named_a_child`, and a
  newborn `child` (age 0; round 1: it was one year or less, so a cousin's birth could bring the card for last year's child) of your
  blood and theirs and of your house (`inLine`). Repeatable, once a marriage; weight 30.
- **Choices.**
  - *A naming feast the city hears of* (needs 800 credits): credits -8, legitimacy +2, warmth +3.
  - *A name from {spouse}'s family*: warmth +8, the spouse's regard +6.
  - *A name of your own line*: legitimacy +1, warmth -3 (they had wanted another).
  - *Name the child between the two of you, and nobody else*: stress -6, warmth +3.
  All four mark the spouse `named_a_child`.
- **Numbers.** 800 credits is under the wedding feast (2,500) and a funeral the city sees (1,000); +2 legitimacy is under the
  wedding feast's +3. Warmth +8 is the top of *The Wedding* (+6) for giving way on something that matters to them.
- **Nearest.** *The Nurse at the Cradle* is about the nurse; nothing else reads a birth.

### 2. A Grandchild Is Born (`a_grandchild_is_born`)
- **What happened.** A grandchild is born. The text makes no claim to the first (round 1).
- **Fires when.** `born_to_the_house`, a `grandchild` of one year or less marked `grandchild_of_the_head` (Core: born to a child of
  yours, by the parents' ids, so that a brother or sister bound to you by blood in a scenario does not make a niece a grandchild),
  and a `parent` of your blood and your house, of the grandchild's blood. Once for each head (round 1: it was once a game; now
  repeatable while the head lacks `had_a_grandchild`, which every choice sets on them); weight 35.
- **Choices.**
  - *Go to them yourself, today*: stress -8, the parent's regard +8.
  - *A gift from the house worthy of it* (needs 500 credits): credits -5, legitimacy +1, the parent's regard +5.
  - *Send your blessing, and keep to your work*: your stress -2, the parent's regard -3 (round 1: it gave influence from nowhere).
- **Numbers.** -8 stress is *An Old Friend*'s evening; 500 credits is a gift, and +1 legitimacy is what the house is seen to gain by
  a line that goes on.
- **Nearest.** None.

### 3. The Other Family Asks for the Child (`the_other_family_asks_for_the_child`)
- **What happened.** A child of yours and your spouse's is six, the age the schoolroom begins. The head of your spouse's family
  asks to raise them in their house, as the old families do.
- **Fires when.** `schooling_begins` (Core, a year), a `spouse`, a `child` of exactly six of your blood and theirs, of your house
  and not already `fostered`, and an `in_law` (Core: the head of your spouse's family). Repeatable; weight 20.
- **Choices.**
  - *Let them go to {in_law}*: the child is fostered away until sixteen (`hidden`, `fostered`: no report can name them until then),
    the in-law's regard +12, warmth +6, your stress +3.
  - *Keep them home, with thanks*: the in-law's regard -5, warmth -3.
  - *Keep them home, and bring {in_law}'s tutor here* (needs 600 credits): credits -6, the in-law's regard +4, warmth +3, the
    child's charisma +1.
  - *Let them go, and ask something in return* (needs Schemer or intrigue 2): fostered as above, influence +2, the in-law's regard
    +4, warmth -2 (your spouse knows what you asked).
- **Numbers.** +12 is about what taking a child in gives their family (the engine gives +10 for a ward). 600 credits is a year of a
  good tutor. Influence +2 is what *The In-Laws Ask a Favour* gives for lending on terms.
- **Nearest.** *A Child with Nowhere to Go* takes another house's child in; this sends yours out.

### 4. The Fostered Child Comes Home (`the_fostered_child_comes_home`)
- **What happened.** The child you let go at six is sixteen and home, and half a stranger.
- **Fires when.** `kin_came_of_age` (Core, a year), and a `child` of exactly sixteen marked `fostered`, of your house. Repeatable;
  weight 40.
- **Choices.**
  - *A feast for the homecoming* (needs 600 credits): credits -6, their regard +12, legitimacy +1.
  - *At your side from the first day*: their administration +1, regard +5, your stress +2.
  - *Let them keep both houses*: regard +8, legitimacy -1 (the house's own child is half another's).
  - *Tell them this house is theirs now, and only this one*: regard -8, legitimacy +1.
  All four clear `fostered` and `came_of_age` (this is their coming of age).
- **Numbers.** +12 and -8 as *Coming of Age* (+15) and *A Place at the Table* (-8).
- **Nearest.** *Coming of Age*, which this replaces for the fostered child.

### 5. Left Behind (`kin_left_behind`)
- **What happened.** One of your blood has what they wanted, and another of your blood, who wanted as much, has not.
- **Fires when.** `kin_fulfilled` (engine, a year), you are 36 or more, a `winner` of your blood aged 16 to 50 with
  `has_what_they_wanted`, and an `other` of your blood and house aged 16 to 50, not of the winner's blood either way (so not their
  parent or child), who has not had what they wanted and has not had this report before. Repeatable; weight 25.
- **Choices.**
  - *Stand with {winner}*: the winner's regard +6, the other's -8, and the other is `slighted` (to *The Slighted Kinsman*).
  - *Something for {other}, from the house* (needs 500 credits): credits -5, the other's regard +8, the winner's -2.
  - *Tell them both to stand together*: each thinks 10 better of the other, each 2 worse of you, your stress +2; both marked
    `stood_together`.
  - *Leave them to it*: the other thinks 6 worse of the winner.
  All four mark the other `heard_left_behind`, so it comes once for each of them.
- **Numbers.** -8 and +8 as *Two of Yours Want One Thing* (10 each way, here a little less: there is no prize left to give).
- **Nearest.** *What They Wanted* (the winner and you), *Two of Yours Want One Thing* (before the prize is won).

### 6. Blood Stands by Blood (`kin_stand_together`)
- **What happened.** Things have gone badly for one of your people (a failure of their own, an election or a battle lost in command,
  or years of wanting something and coming no nearer it), and the one you told to stand with them has gone to them before you could.
- **Fires when.** A `fallen` of your house marked `stood_together` who either has `has_failed` in a year of `kin_failed` (engine) or
  `thwarted` in a year of `kin_thwarted` (engine), and a `helper` of your house marked `stood_together`. Repeatable; weight 40.
- **Choices.**
  - *Let {helper} see them through it*: the fallen one's stress -8, they think 8 better of the helper and 3 better of you.
  - *Go to them both*: the fallen one's regard +8, the helper's +6, your stress +3.
  - *Mend it yourself, where it counts* (1 influence): the fallen one's regard +10 and stress -4, the helper's regard -4 (it was
    theirs to do).
  All three clear `stood_together` from both.
- **Numbers.** *One of Yours Fails* gives +8 for going to them; 1 influence is the price of a word in the right ear.
- **Nearest.** *One of Yours Fails* and *No Nearer It*: you and the one who failed. Here someone else came first.

### 7. The Wedding Remembered (`the_wedding_remembered`)
- **What happened.** Your marriage is ten years old, or twenty, or thirty.
- **Fires when.** `wedding_remembered` (Core, every tenth year of your marriage), and a `spouse`. Repeatable; weight 12 (it was 30,
  and took 1.5% of all reports in the first soak: a marriage of forty years has four of these, and few reports compete for the
  year). The choices clear the mark, so once answered it does not come again in the year after.
- **Choices.**
  - *A feast for both families* (needs 800 credits): credits -8, warmth +6, legitimacy +1.
  - *A gift between the two of you* (needs 300 credits): credits -3, warmth +8, stress -4.
  - *A day like any other*: warmth -6.
  - *Say what has gone wrong between you* (only in a cold marriage): warmth +10, stress +4.
- **Numbers.** Warmth +10 is *A Cold House*'s mending, here without its influence, because it is said aloud on the day.
- **Nearest.** *The Wedding* (the day itself), *A Marriage of Two Minds* (once, in a warm marriage), *The Household* (an evening).

### 8. A Spouse's Ambition (`a_spouses_ambition`)
- **What happened.** Your spouse, who is ambitious, proud or arrogant, has held the same rank in their own service for six years.
- **Fires when.** Year 4 or later, and a `spouse` of another house (one who lives in yours is one of the house's own, whose
  ambitions the engine already runs) of rank 1 to 3 who has held it six years or more, who is Ambitious, Proud or Arrogant, and
  has not had this report before. Repeatable, once a spouse; weight 8. (Six, not eight: the engine promotes another house's people
  every nine years at most, so at eight years the report had one year in nine to come, and at weight 6 and eight years it did not
  fire in the second soak.)
- **Choices.**
  - *Speak for them where it counts* (2 influence): a step in rank for the spouse, warmth +8, their regard +6.
  - *Put your own name to it before the Senate* (needs rank 3 or oratory 3): a step in rank for the spouse, warmth +10,
    legitimacy -1 (it is called favour).
  - *Say it is their own house's place to speak for them*: warmth -4.
  - *Say the house needs them more than their service does*: warmth -8, their stress +6, your stress -4 (they take some of yours).
  All four mark the spouse `spouse_ambition_heard`.
- **Numbers.** 2 influence is what a relative's asked-for word costs (`AmbitionSystem.WordInfluence`).
- **Nearest.** *No Nearer It* and the requests of the house's own people; a spouse of another house was outside them.

### 9. A Family Divided (`between_two_families`)
- **What happened.** Your spouse's house is at war with yours.
- **Fires when.** A `spouse` whose house is at war with yours and who has not had this report. Repeatable, once a spouse; weight 12.
- **Choices.**
  - *Let them write to their family*: warmth +6, legitimacy -1 (letters to the enemy are talked of).
  - *No letters while the war lasts*: warmth -10, their stress +8, legitimacy +1.
  - *Ask them to carry an offer of peace* (only when their house would take a white peace, and 1 influence): peace, nothing
    changing hands, warmth +6.
  All three mark the spouse `torn_between`.
- **Numbers.** The white peace is the one the Military window offers on the same terms; the report adds no peace that was not to be
  had, only a way to it through your spouse.
- **Nearest.** None.

### 10. A Life of Their Own (`a_life_of_their_own`)
- **What happened.** A child of your blood is sixteen and has put their name down for a service (the report names the first rank:
  a Cadet, a Clerk, an Aspirant).
- **Fires when.** `kin_came_of_age`, and a `kid` of exactly sixteen of your blood and house, with `came_of_age` and `chose_own_road`
  (Core, round 1: bound for a service neither parent was in), not `fostered`. Repeatable; weight 30, kept: it now comes for a few
  children of sixteen, not all of them, so the old card has the rest back.
- **Choices.**
  - *Give them your blessing before the house*: regard +10.
  - *Write to someone you know in that service* (1 influence): a step in rank for them, regard +6.
  - *Ask them to learn the house's business for a year first*: their administration +1, regard -3.
  - *Say it is the wrong road for them*: regard -10, their stress +8, marked `doubted_by_the_head`.
  All four clear `came_of_age`.
- **Numbers.** +10 and -10 as *Coming of Age* (+15) and *Their Study Is Their Own* (+4 / -2), between the two.
- **Nearest.** *Coming of Age*, which does not bind this child in this year; a child of sixteen on a parent's road has that card.
- **Round 1.** The play critic offered two answers: cut this and fold *the wrong road* into *Coming of Age*, or narrow it to the
  child who chose a road of their own. It is narrowed: the wrong road belongs to the child who did not follow you, and the thread to
  *You Were Wrong About Them* stays whole.

### 11. You Were Wrong About Them (`you_were_wrong_about_them`)
- **What happened.** The child you said had taken the wrong road has what they wanted.
- **Fires when.** `kin_fulfilled`, and a `kid` with `has_what_they_wanted` and `doubted_by_the_head`. Repeatable; weight 40.
- **Choices.**
  - *Say you were wrong*: regard +15, your stress +4.
  - *Raise a cup to them, and say nothing of the rest*: regard +5.
  - *Say they were lucky*: regard -10.
  All three clear `doubted_by_the_head`.
- **Nearest.** *What They Wanted*, on the same news, is about whether you helped; this is about having said they would fail.

### 12. While You Were Ill (`while_you_were_ill`)
- **What happened.** You have been ill this year, and one of your people kept the house's business going.
- **Fires when.** `head_ill` (engine, the years you are ill), and a `keeper` of your blood and your house aged 20 or more (so not
  your spouse, nor someone taken in), not an elder who has laid the house down, who does not think worse than -20 of you and is not
  already `kept_the_house` or `promised_the_seat`. Repeatable; weight 20.
- **Choices.**
  - *Thank them before the house, and leave them at it*: their regard +10, their administration +1, your stress -5, marked
    `kept_the_house`.
  - *Ask what they would change, and change it*: their regard +12, legitimacy -1 (you are seen to defer), marked `kept_the_house`.
  - *Take it all back, and go through what they did*: their regard -8, legitimacy +1, your stress +3.
- **Nearest.** *The Physicians' Verdict* and *The Wound* are about you; nothing was about who held the house.

### 13. They Ask You to Stand Aside (`they_ask_you_to_stand_aside`)
- **What happened.** You are old and failing, and the one who kept the house while you were ill asks you to lay it down.
- **Fires when.** You are 55 or more and either 62 or more, or ill, ailing or frail this year; and a `keeper` marked
  `kept_the_house`, of your blood and house, aged 20 or more and not retired. Repeatable; weight 25. (Heads in the probe soak died
  between 64 and 70; the engine begins to roll for death at 50 and asks another house's head to stand aside at 65.)
- **Choices.**
  - *Lay the house down to {keeper}*: they have the seat from today and you stay on as the house's elder (`retired`), legitimacy +2
    (as retiring does), their regard +15.
  - *Name a day, but not yet*: their regard +4, marked `promised_the_seat`.
  - *Not while you live* (needs Proud, Ambitious or Arrogant): their regard -15, their stress +8, marked `slighted`.
  All three clear `kept_the_house`.
- **Numbers.** +2 legitimacy is what `LifeSystem.Retire` gives the house; -15 as *The Slighted Kinsman*'s -20, a little less.
- **Nearest.** *The Abdication* is a set-piece about yielding to an heir by title. Here the family asks, and can be put off.

### 14. The Day You Named (`the_day_you_named`)
- **What happened.** The day you promised to stand aside has come.
- **Fires when.** Either you are 68 or more, or you are 60 or more and ill, ailing or frail this year; and a `keeper` of your house
  marked `promised_the_seat`, of your blood and house and not retired. So a promise made in good health is called in by the next
  illness, or at 68. Repeatable; weight 25.
- **Choices.**
  - *Keep your word*: as laying the house down above.
  - *Ask for one more year*: their regard -6; the promise stands, and the report comes back more slowly, as reports read lately do.
  - *Break it*: their regard -25, legitimacy -1, and they are `slighted` (to *The Slighted Kinsman*). Round 1: it set `defiant`,
    which `FamilySystem.Tick` strikes out at the end of the same year from anyone Loyal or whose regard is 0 or more, and this
    keeper's regard is usually still above 0 after the -25.
  Keeping or breaking clears `promised_the_seat`.
- **Numbers.** -25 is the hardest cut in the set, for the seat promised and refused; a broken promise in the house costs 1
  legitimacy, as a broken betrothal costs the house 3 with another house.
- **Nearest.** None.

### 15. The Reading of the Will (`the_reading_of_the_will`)
- **What happened.** The old head is dead and you have the house. At the reading, those the law passed over for you are in the
  room.
- **Fires when.** `will_read` (engine, at the reading), and a `sibling` of your house aged 16 or more with `passed_over_at_the_will`
  (Core, round 1: the grown children of the dead head who are not the heir, at this reading) who does not think better than 20 of
  you and has not had this report. Repeatable;
  weight 35.
- **Choices.**
  - *A portion for them from the treasury* (needs 800 credits): credits -8, their regard +15.
  - *Keep them close, and ask their advice*: their regard +8, legitimacy -1 (the house sees two voices). It said "a seat at your
    counsel", a misspelt council that the choice does not give.
  - *Speak to them alone, before the reading* (needs Diplomat or charisma 3): their regard +12, your stress -2.
  - *Tell them the house is one, and it is yours*: their regard -8, legitimacy +2.
  All four mark them `heard_at_the_will`.
- **Numbers.** The engine takes 20 off their regard for the new head; +15 makes most of that good, and costs what a portion of an
  estate shared out would.
- **Nearest.** *The Will Is Read*: one who was left the purse. This is about those who were left nothing.

### 16. A Household Scandal (`a_household_scandal`)
- **What happened.** Someone of your household went unpaid two years running and has left, or laid the house's work down, and is
  saying why to anyone who asks.
- **Fires when.** `household_left_unpaid` (engine, a year) and a `leaver` marked `left_unpaid` (Core). Repeatable; weight 35.
- **Choices.**
  - *Pay what was owed, and more* (500 credits): credits -5, their regard +15. Round 1: it was 400, exactly what a steward or a grown
    person of standing who left after two unpaid years was owed, so there was no "more".
  - *Go to them yourself*: their regard +8, legitimacy -1, your stress +3.
  - *Have it said they were let go for cause* (needs Schemer or intrigue 2): their regard -15, corruption +1.
  - *Let them talk*: legitimacy -2.
- **Numbers.** -2 legitimacy is the cost of the house's name in the street, as *The Plot Exposed*'s owning of it costs 3.
- **Nearest.** *The Household Goes Unpaid*: the year it happened, not what came of it.

## What was cut in review

- *The Sixtieth Year* (a feast at the head's sixtieth birthday, who sits at your right hand). Cut: heads reach sixty in about one
  game in two, and its choices were *The Wedding Remembered*'s again. The family feast is the anniversary's first choice and the
  naming feast.
- *The Grandchild Comes to Stay* (a second step to the grandchild). Cut: in the probe soak a grandchild under thirteen lived in 38
  of 660 years, so it would never fire, and it had no stake beyond regard.
- *The Promise Falls Due* (a child promised as a child, at sixteen). Cut: no effect can break a betrothal, so the report could only
  comment; and no betrothal was made in the probe soak.
- *The Match You Forbade* (`match_forbidden`) and *The Children After a Divorce* (`divorced`). Cut: both follow the player's own
  act on a screen, and in the probe soak each happened two or three times in eleven games.
- *In-Laws Send a Young Relative* (a protégé). Cut: binding a young relative of the in-law's house needs a parent tie that the cast
  rarely has, and a third in-law report would crowd the briefing beside *The In-Laws Ask a Favour*.
- *At the Sickbed* (a relative ill). Cut: the screen already tells it and sends the physicians, and a report could only add regard.
- *A Spouse's Ambition* kept, but at weight 8 and once a spouse: it is a lasting state, not a moment.

## What was added to Core

All in systems this theme owns; each with a test in `tests/KinEventsTests.cs`.

- `DynastySystem`: three marks of a year, raised as the year turns, for the player's house only, and cleared the year after like
  every other mark: `born_to_the_house` (a child is born to the house, or to one of it), `schooling_begins` (a child of the house is
  six), `kin_came_of_age` (one of the house is sixteen). The persons are found by their age; the marks are what makes the Director
  bring the report while it is news. And `grandchild_of_the_head`, a char flag on a child born to a child of the head (born or
  adopted), cleared with `born_to_the_house`: a blood bond cannot tell a child of the head from a brother or sister.
- `CourtshipSystem`: `in_law`, a char flag on the head of the player's spouse's family (the house the spouse was born to if they
  live in yours), set each year with the other courtship flags; and `wedding_remembered`, a mark in every tenth year of the player's
  marriage.
- `HouseholdSystem`: `left_unpaid`, a char flag on whoever left the household unpaid, or laid its work down, cleared with
  `household_left_unpaid`. Round 1: `HardRefusal`, the refusals that hold whatever a person thinks of you (the head of a house does
  not leave it, one married into another house goes with the marriage, no house at war, no household without room), which
  `TakeInRefusal` now begins with and the `takeIn` effect (`Events/Effects.cs`) obeys. A report could take in the head of another
  house, purse, offices and marriage, and leave it headless. A refused offer is said in the chronicle.
- `DynastySystem` (round 1): `chose_own_road`, a char flag on one who comes of age bound for a service neither parent was in, struck
  out the year after.
- `WealthSystem.Inherit` (round 1; the wealth theme's system, one line): `passed_over_at_the_will` on the grown children of the dead
  head, of the house, who are not the heir, struck out with `will_read`.
- `household.json`'s *Coming of Age* (one condition): its heir is not one marked `chose_own_road`.

## The soak (11 starts, 2 seeds, 60 years, and the three scripted players)

`qa --scenario all --seeds 2` passes: no failures, no unresolved tokens, 7,924 reports fired (the baseline fired 7,891; every
change to content moves the dice, so the totals differ by run). How often each of these fired, in the last of four soaks (the
first three were before the moment-first change above):

| Report | Fired |
|---|---|
| *A Life of Their Own* | 98 (1.2%) |
| *The Wedding Remembered* | 75 (0.9%) |
| *The Naming of a Child* | 65 (0.8%) |
| *The Other Family Asks for the Child* | 39 |
| *The Reading of the Will* | 28 |
| *A Household Scandal* | 20 |
| *Left Behind* | 16 |
| *A Grandchild Is Born* | 15 |
| *A Spouse's Ambition* | 9 |
| *The Fostered Child Comes Home* | 6 |
| *While You Were Ill* | 6 |
| *You Were Wrong About Them* | 3 |
| *A Family Divided* | 1 |
| *They Ask You to Stand Aside* | 1 |
| *Blood Stands by Blood* | 0 |
| *The Day You Named* | 0 |

Two did not fire in the last soak, and each did in earlier ones. *Blood Stands by Blood* (once in each of the second and third
soaks) is the second step of a thread whose first step is one choice in four of *Left Behind*, and then needs one of the two to
fail or be thwarted afterwards. *The Day You Named* (twice and once in the first two soaks) is the third step of a chain: an
illness, the keeper left at the work, a head of 55 or more asked to stand aside who promises a day, then another illness or the
age of 68; the random player takes each turn of it about one time in three. Both tests build their moments. *The Wedding
Remembered* took 1.5% of all reports at weight 30 in the first soak and was cut to 12; *A Spouse's Ambition* did not fire at eight
years and weight 6 and was eased to six years and weight 8. No report of this file is above 1.5%.

The file took 382 of 7,924 reports (4.8%). What the baseline's files gave up, in points of the whole: `ladder` 10.0% to 9.3%,
`household` 3.5% to 2.9% (most likely *Coming of Age*, which *A Life of Their Own* now answers for a child of sixteen; the soak
lists only the top 30 and the rare by name, so this is the reckoning, not a count), `situations` 5.0% to 4.4%, `found_family` 4.0%
to 3.5%, `senate` 8.1% to 7.6%, `echoes` 3.9% to 3.5%, `life` 3.9% to 3.5%, `courtship` 5.6% to 5.2%, `career_progression` 5.4% to
5.1%, `nations` 7.9% to 7.6%, and 0.1 to 0.2 each from a dozen smaller files; `ambitions` rose 3.0% to 3.6% (*What They Wanted* and
*No Nearer It* came more often in this run). The briefing still takes three reports a year, so what this file gains the others
lose, spread thinly: no file lost more than 0.7 of a point.

## Round 1 of review (2026-11-27)

- The naming and the grandchild claim no "first"; the grandchild comes once for each head, and a blessing gives no influence.
- *A Life of Their Own* is for the child who chose their own road; *Coming of Age* is for the rest, and never for the same child in
  the same year. No new family report was added in its place: the card was narrowed, not cut, and the family set was already the
  second largest of the four.
- *The Day You Named* leads, when broken, to a flag the engine keeps (`slighted`).
- *The Reading of the Will* binds the one this reading passed over.
- *Blood Stands by Blood* binds a helper who thinks 5 or better of the fallen one (telling them to stand together gave each +10 of
  the other), and says only what is known.
- Labels that were not actions: the wedding remembered, a family divided, stand aside, the will, a spouse's ambition (which now
  says "their own family"), the tutor brought here; chronicle lines in the third person.
- `kin_left_behind` and `you_were_wrong_about_them` beside `what_they_wanted`: not changed. A briefing is drawn all at once, so an
  `eventFired` gate cannot keep two of them from one briefing, and the three ask different things (the ambition won, the one left
  behind, an old judgement).

### Measured after round 1

The soak after review (22 games, 7,843 reports) passed. This file took 282 reports (3.6%), thirteen of its sixteen kinds; the most
fired are `the_wedding_remembered` (71, 0.9%) and `the_naming_of_a_child` (64, 0.8%), and `a_life_of_their_own` is no longer among
the thirty most fired. `kin_stand_together`, `they_ask_you_to_stand_aside` and `the_day_you_named` did not fire: second and third
steps of threads, as in this file's own first soak, each covered by its test.
