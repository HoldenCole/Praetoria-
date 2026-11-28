# War: the events of the front, the home front, the sack and the peace

The owner asked for "some events for war". These fifteen reports are about the people a war is fought by: an officer of your
house who dared or ran, a young one of your blood who wants to go and may be hurt, a captain you took from the wrecks, an enemy
admiral who asks to talk before the guns, a house that came home rich from a sack beyond the rim, crews who deserted, and what the
peace asks of you: who is honoured, who comes home, and who pays the crews.

Files: `content/events/war_front.json`, `content/text/war_front.json`, `tests/WarEventsTests.cs`. The marks they read are in
`src/Core/Systems/FleetSystem.cs` (and one each in `NationSystem.War.cs`, `NationSystem.Map.cs`, `ServiceSystem.cs` and `NpcAi.cs`),
described at the end.

## What exists already, and how these differ

Read before writing: `military.json`, `nations.json`, `emergency.json`, `caesar.json`, `coalition_crises.json`, `great_crisis.json`,
`breakaway.json`, and the war reports in `situations.json`, `echoes.json` and `rings.json`.

- The existing battle reports are about the battle as a whole and about you: *The Line Holds* (pursue or rescue), *The Line Breaks*
  (a court-martial of "a captain", own the defeat, rebuild), *The Butcher's Bill* (the dead, the families from the purse),
  *Mentioned in Dispatches* (you, at the front), *The Prize Crews* (captured hulls). None of them names another person who was in
  the line. Here every battle report names one: your officer who broke the line or ran, the enemy captain you took.
- *The Envoy* (`echoes.json`) answers a house that is suing for peace; *The Enemy's Envoy* (`rings.json`) is a sealed case in your
  antechamber in any war. *A Parley Under a Flag of Truce* (`a_parley_before_the_guns`) is the commander of a fleet that is a year from yours, at the front, and it can
  end the war only if the war's score allows a white peace.
- The peace has reports for the nations beyond the rim (*A White Peace*, *Terms of Our Own*, *The Senate Votes Peace*) and for a
  world won or ceded (*The Terms of Peace*, *The Ceded World*). Nothing reads the end of a war between your house and another.
  *The Crews Want Paying* does, and so do the second steps of the officer thread and the young one's homecoming.
- *The Sack* reads a sack by your own fleet (it never fired in the baseline soak). *Plunder on the Exchange* reads a sack by another
  house, which happens about once a game, and is about the money that war makes for those who are not you.
- *The Press Gang* (a levy under the conscription law) and *The Levy Falls Due* (the Senate's levy) cover the levy; nothing here
  copies them.

## The threads

1. **The officer** (two steps). *Out of the Line* (an officer of a battle won) or *The Officer Who Ran* (of a battle lost), on the
   map or beyond the Reach, marks an officer of your house as one who owes you (`officer_owed`) or holds something against you
   (`officer_wronged`). At the next peace your house makes, or the end of a war with a nation that the house was in
   (`our_nation_war_over`), and with no war left, *A Laurel at the Peace* comes for the first, *The Other Dispatch* for the second.
   Once the house is at peace and that news is over, the engine strikes both marks out, so nobody waits for ever.
2. **The young one** (up to four steps). *A Place in the Fleet* sends a relative of sixteen to twenty-four to the war (`at_the_front`),
   or keeps them home (`kept_from_war`, for good). The first battle the house fights after that is *Blooded in Battle* (once a
   relative: `blooded`); left with the fleet, they stay at the front. A battle lost may wound them (one in three at the front, by
   hash), and then *Wounded Aboard the Fleet* comes. At the peace, *Home From the War*, which clears all of it. The marks are struck
   out with the officers'.
3. **The prisoner** (two steps). *Taken in Battle* hands you an enemy captain. Paroled (`on_parole`), they gave their word not to
   fight you again in this war; if their house gives them a fleet while the war lasts, *The Parole Broken* comes.
4. **The spoils** (two steps). *Plunder on the Exchange* can put your money into another house's raids beyond the rim
   (`war_partner`); when a war with a nation ends, *A Scandal in the Accounts* asks what you do with your share.

Twelve of the fifteen are in a thread; three stand alone (*A Parley Before the Fleets Meet*, *Deserters From the Fleet*, *The Crews
Want Paying*).

## The events

Weights. The existing battle reports, on one-year flags with no fresh bias, weigh 8 to 12; reports on a marked moment beyond the rim
weigh 35 to 60 (and are five times likelier while the moment is a year old). The battle reports here weigh 14 to 30: each needs a
named person in the battle as well as the battle, so it is eligible far less often than *The Line Holds*. The peace and parley
reports are on marked flags (the fresh bias applies) and weigh 30 to 40, like the nation reports. Reports on the long-lived
`at_war_any` weigh 6, like *The Enemy's Envoy*. All are situations; none is a set-piece, so none rises with the turn.

Deltas. The existing war reports move legitimacy by 1 to 3, credits by 2 to 8 (200 to 800 credits), regard by 10 to 25 and stress
by 2 to 6, and estates by 3 to 5. A choice's chips show its estates and the magistrates' trust (`ChoiceStakes`, round 1 of review),
so the labels say the estate in words and the chips give the number. These keep within that: legitimacy 1 to 3, credits 2 to 8, regard 3 to 30 (the two hardest, -25 and -30,
are a public breaking and a parade in chains), your stress 2 to 4 (another's 5 to 10), estates 2 to 4, a skill by 1, one step in rank. Money in the files is hundreds (docs/MONEY.md).

### 1. Out of the Line (`out_of_the_line`)
- **What happened.** Your house won a battle. An officer of your house who stood in the line (an admiral or captain of one of your
  fleets there, not you) did not wait for orders and broke the line, and it won the day.
- **Fires when.** `battle_won` or `expedition_victory` (this year: a battle on the map, or one beyond the Reach, round 2), and a role
  `officer` with the char flag `in_a_won_battle` (Core, below: not one who stood in a battle the house lost the same year), and neither
  `officer_owed` nor `officer_wronged`. Repeatable; weight 16.
- **Choices.**
  - *Name {officer} in the dispatch*: their regard +12, marks `officer_owed`.
  - *Promote {officer} (1 influence)* (needs them in the Navy below Admiral): a step in rank, regard +15, influence -1,
    marks `officer_owed`.
  - *Rebuke {officer} for breaking the line*: regard -12, their discipline +1, the officers' estate +2 (they like order), marks
    `officer_wronged`.
  - *Let the dispatch say it was your plan* (needs Proud, Ambitious or Arrogant): legitimacy +2, regard -20, marks
    `officer_wronged`.
- **Numbers.** 12 and 15 are a good turn between two people (the courtship and household reports give 10 to 15); -20 is the cost of
  a stolen victory, about what *The Envoy* costs for asking tribute. +2 legitimacy is the top of what a battle report gives.
- **Nearest.** *Mentioned in Dispatches* is about you; this is about someone else, and what you say about them is the choice.

### 2. The Officer Who Ran (`ran_from_the_battle`)
- **What happened.** Your house lost a battle, and an officer of yours in the line pulled out before the line broke.
- **Fires when.** `battle_lost` or `expedition_defeat`, and an `officer` with `in_a_lost_battle`, otherwise as above. Repeatable; weight 16.
- **Choices.**
  - *Dress {officer} down before the fleet*: regard -25, legitimacy +1, the officers' estate -2 (no wardroom likes
    to see one of its own broken), marks `officer_wronged`. (It was "strip them of the command", which nothing did: the words now
    say what the choice does.)
  - *Write the report so the line broke somewhere else*: regard +15, your stress +4 (it is a lie), marks `officer_owed`.
  - *Ask {officer} why*: they tell you the signal was wrong. Your tactics +1, regard +5; no mark: it ends there.
- **Numbers.** -25 is the hardest cut in the set, for a public breaking; +4 stress is what the existing reports charge for acting
  against yourself.
- **Nearest.** *The Line Breaks* convenes a court-martial of an unnamed captain to protect the house's name. Here the officer has
  a name, did run, and the third choice (it was your signal) has no counterpart there.

### 3. A Laurel at the Peace (`a_laurel_at_the_peace`)
- **What happened.** The war your house was in is over, and the officer who served you well is owed something.
- **Fires when.** `our_peace` or `our_nation_war_over` (Core), `at_war_any` false ("the war is over": not while the house has
  another), and an `officer` with `officer_owed` who is still of your house (`inLine`). Repeatable; weight 40. The body says only that
  they are owed for the war: one road in is a cover-up for an officer who ran.
- **Choices.**
  - *Put {officer}'s name to the Senate (1 influence)*: influence -1, legitimacy +1, regard +10, and they swear to you
    (a sworn bond of 60).
  - *Give them a purse from the house* (300 credits): regard +10.
  - *Thank them, and no more*: regard +3.
  All three clear `officer_owed`.
- **Numbers.** A sworn bond is the strongest thing a report can give one person; it costs the influence of a motion. 300 credits
  is a purse, not a fortune.
- **Nearest.** *A Triumph in the House* is for a general the Senate honours; this is for one the house chooses to.

### 4. The Other Dispatch (`the_other_dispatch`)
- **What happened.** The war is over, and the officer you wronged has written their own account of the battle, and it is being
  read in the city.
- **Fires when.** `our_peace` or `our_nation_war_over`, `at_war_any` false, and an `officer` with `officer_wronged` whose regard for
  you is 20 or less (it was 10: a wronged officer whose regard had mended a little never published, and kept the mark for good).
  Repeatable; weight 40.
- **Choices.**
  - *Answer it in the Senate (1 influence)* (needs oratory 3): influence -1, regard -10. Your account stands.
  - *Make it up with {officer}* (not for one who is Proud): regard +20, legitimacy -1 (you admit it).
  - *Pay for their silence* (400 credits): regard +5.
  - *Let it stand*: legitimacy -2.
  All four clear `officer_wronged`.
- **Numbers.** Letting it stand costs what taking the credit gained; answering costs influence and keeps the name.
- **Nearest.** Nothing reads a slighted officer at the peace.

### 5. A Place in the Fleet (`a_place_in_the_fleet`)
- **What happened.** In a war, a relative of yours between sixteen and twenty-four asks to go to the fleet.
- **Fires when.** `at_war_any`, or a fleet of the house away at a war beyond the Reach (`expedition_away` and `foreign_war`, round 2:
  the Senate's usual war is off the map, and `at_war_any` counts only enemies on it); your house has a hull; and a role `kin` of
  your house (`inLine`), aged 16 to 24, not an admiral, with neither `at_the_front` nor `kept_from_war`. Repeatable (once a relative,
  by the flags); weight 6.
- **Choices.**
  - *Send {kin} aboard the house's fleet*: regard +10, their tactics +1, your stress +3, marks `at_the_front`. The label says a
    battle lost may cost them: one in three at the front is wounded when the house loses (`FleetSystem.FrontWoundOneIn`).
  - *Take {kin} onto your own staff* (needs you in the Navy, Commander or above): regard +12, tactics +1, discipline +1, marks
    `at_the_front`.
  - *Find them a post at the yards* (1 influence): their logistics +1, regard -3, marks `kept_from_war`.
  - *Forbid it*: regard -12, marks `kept_from_war`.
- **Numbers.** Weight 6, as *The Enemy's Envoy*: the house is at war in two years of three in the soak, and the flags already stop
  it asking twice about one person. Stress +3 is a parent's worry.
- **Nearest.** *The Front or the Flag* is your own command style; the ambition reports are kin asking for help with a goal of their
  own. Nothing asks about a young one going to the war.

### 6. Blooded in Battle (`blooded_in_battle`)
- **What happened.** The house's fleet has fought, and the relative you sent was aboard.
- **Fires when.** `battle_won`, `battle_lost`, `expedition_victory` or `expedition_defeat`, and a `kin` with `at_the_front` and
  without `blooded` or `wounded_at_the_front` (the wound has its own report), aged 26 or under (sent at 24 at the latest, a war and a year or two later; never "their first battle" at
  thirty-four). Repeatable; weight 30.
- **Choices.**
  - *Ask the Admiralty to give {kin} a step. They stay with the fleet.* (needs them in the Navy, below Admiral): a step in rank,
    regard +8.
  - *Bring {kin} home now they have seen it*: regard -6, your stress -3, clears `at_the_front`, marks `kept_from_war`.
  - *Leave them with the fleet. A battle lost may still cost them.*: their discipline +1, their stress +5.
  All three mark `blooded`, so the card comes once a relative. Only the one brought home leaves the front (round 2: all three
  cleared `at_the_front`, so a relative "left with the fleet" was safe from wounds, never came home at the peace, and could be asked
  for a place in the fleet again).
- **Numbers.** A step in rank is the largest gain the thread can give; the other two trade the relative's regard for your peace of
  mind, or their hardening for their nerves.
- **Nearest.** *Mentioned in Dispatches* again is you; *The Wound* is your body. This is someone else's first battle.

### 6a. Home From the War (`kin_home_from_the_war`, added in review)
- **What happened.** The war is over, and the relative you sent to the front is home.
- **Fires when.** `our_peace` or `our_nation_war_over`, `at_war_any` false, and a `kin` of your house with `at_the_front`.
  Repeatable; weight 35, the weight of the other second steps on a marked moment.
- **Choices.**
  - *Give a feast for their homecoming* (400 credits): regard +8, legitimacy +1.
  - *Ask the Admiralty to give them a step* (needs them in the Navy at Captain or below, and 1 influence): a step in rank, regard +6.
  - *Let them be, and ask nothing*: their stress -6, regard +4.
  - *Ask them what they saw*: your tactics +1, their stress +4.
  All four clear `at_the_front`, `blooded` and `wounded_at_the_front`.
- **Why.** The critics found the thread had no end: the mark was never cleared, so a relative sent in one war was "blooded" in
  another decades later. This is its last step, and the engine also strikes the mark out once the peace is old news.

### 6b. Wounded Aboard the Fleet (`wounded_aboard_the_fleet`, added in round 2)
- **What happened.** The house lost a battle, and the relative you sent to the front was wounded in it (`FleetSystem.WoundTheFront`,
  one in three, marks `wounded_at_the_front` beside `LifeSystem.Wound`). The wound was a line in their record and nothing more: the
  label "A battle lost may cost them" was kept by the engine, and no report told you of it.
- **Fires when.** `battle_lost` or `expedition_defeat` (the year of the battle), and a `kin` of your house with `wounded_at_the_front`
  and `at_the_front`. Repeatable; weight 35, as *Home From the War*: a rare, marked moment that is the year's news.
- **Choices.**
  - *Bring {kin} home to mend*: clears `at_the_front`, marks `kept_from_war`, regard -2, your stress -4.
  - *Send the house's own physician out to them* (300 credits): regard +6, their stress -5. They stay with the fleet.
  - *Leave them to the fleet's surgeons*: their discipline +1, their stress +6, your stress +3. They stay.
  All three clear `wounded_at_the_front` and mark `blooded`: they have seen their first battle, and *Blooded in Battle*, which
  waits while the wound is news (its role wants `wounded_at_the_front` false), does not come after it to say "they came through".
- **Numbers.** Bringing them home is resented less than after their first battle (-2 against -6): they are hurt. 300 credits is a
  purse, as at the laurel; the stress moves are those of the young one's other steps. Nothing claims the physician heals them
  faster: health mends as it does for every wound (`LifeSystem`).
- **Nearest.** *The Wound* is your own. This replaced *Who Pays for the War*, which was folded into *The Crews Want Paying* (12).

### 7. Taken in Battle (`taken_in_battle`)
- **What happened.** Your house won a battle that cost the beaten line two hulls or more, and one of its captains (or, where it
  had none, an admiral whose fleet went down) was taken from the wrecks (Core, below).
- **Fires when.** `battle_won`, and a role `prisoner` with `taken_prisoner`, without `held_to_the_peace`, whose house is at war
  with yours. Repeatable; weight 30.
- **Choices.**
  - *Ransom {prisoner} back to {prisoner.house} for 400 credits* (needs their house to have it): 400 credits from their treasury to yours,
    their regard -5.
  - *Exchange them for your own crews they hold*: manpower +2, their regard +5.
  - *Let them go on their word*: regard +20, marks `on_parole`.
  - *Parade them through your capital* (needs Proud, Arrogant or Cruel): legitimacy +2, regard -30, marks `held_to_the_peace`
    (they stay a prisoner, with no command, until the peace).
  The first three free them at once.
- **Numbers.** The ransom is half what *The Prize Crews* gives for a hull sold: a person is worth less to the yards than a ship.
  Manpower +2 is what *The Line Holds* gives for survivors pulled out of the wrecks.
- **Nearest.** *The Prize Crews* ransoms hulls. Here it is a person, and the parole has a sequel.

### 8. The Parole Broken in the Line (`the_parole_broken`)
- **What happened.** The captain you let go on their word has been given a fleet by their house, in the same war.
- **Fires when.** A role `parolee` with `on_parole` and `admiral`, whose house is at war with yours. Repeatable; weight 40.
- **Choices.**
  - *Denounce {parolee} in the Senate* (1 influence): their house's legitimacy -3, their regard -15.
  - *Send a herald to say it to their face*: their stress +10, their regard -10.
  - *Let it go: it is a war*: your stress +3.
  All three clear `on_parole`.
- **Numbers.** -3 to another house's legitimacy is half what fighting on against the Senate's peace costs a known commander at once
  (6, `NationSystem.DefyAtOnce`): a broken word is a smaller thing than a defied Senate.
- **Nearest.** None. Another house's head gives the fleet (the house does not keep the word its captain gave), so this is not a
  trap set by the first report: a house that honours it will not appoint them captain, and one that does not is named for it.

### 8a. Deserters From the Fleet (`deserters_from_the_fleet`, added in review)
- **What happened.** Crews deserted one of your fleets this year (`fleet_deserts`: FleetSystem raises it when a starved or mutinous
  fleet sheds a hull, and no report read it), and many have been taken at your ports.
- **Fires when.** `fleet_deserts`. No role: the deserters are crews, not a person. Repeatable; weight 20, above *The Mutiny* (12) and
  *The Long Supply Line* (10), which come in some of the same years: desertion is rarer than either (at 12 it did not fire in the
  first soak of review), and it is the year's news.
- **Choices.**
  - *Hang them where they were taken* (needs Cruel, Ruthless or Proud): the officers +3, the commons -3, legitimacy +1, stress +2.
  - *Take them back into the fleet, flogged and unpaid*: manpower +1, the officers -2.
  - *Pay them what they were owed and let them go* (300 credits): the commons +3, the officers -1.
  - *Send them to the yards under guard*: materials +1, the commons -1.
- **Nearest.** *The Mutiny* is crews who have not left yet; *The Long Supply Line* is a fleet with no supply. This is what the house
  does with the ones who went.

### 9. A Parley Before the Fleets Meet (`a_parley_before_the_guns`)
- **What happened.** An enemy fleet is a year out from a system where a fleet of yours lies, and its commander sends a herald
  to ask to talk first.
- **Fires when.** `enemy_bears_down` (Core) and a role `foe` with `bears_down_on_us` whose house is at war with yours.
  Repeatable; weight 30.
- **Choices.**
  - *Offer a white peace* (needs {foe.house} to take it, by the war's score): peace, nothing changes hands.
  - *Meet under a flag of truce, and fight when they come*: their regard +10, your stress -2, the officers -2 (the wardroom does not
    drink with the enemy). (It said "fight tomorrow", but their fleet is a year out.)
  - *Use the parley to count their ships* (needs Schemer, or intrigue 3): your tactics +1, their regard -15, and their fleet fights
    the year it arrives at 90% (`ships_counted`, which the next tick makes `ships_known`, read by `FleetSystem.DepthFactor`).
  - *Send the herald back unheard*: their regard -5, the officers +2, legitimacy +1.
- **Numbers.** The peace is the real stake, and it is offered only where the war's score allows it. Counting the ships is now a real
  edge in the battle that follows, a tenth off their power, half of what a fleet short of supply loses (0.8); the herald and the
  truce move the wardroom by 2, the smallest estate move in the set. Renamed in review: the old id and title held "battle", which
  opened the card on a battle painting before any battle; round 2 retitled it again, as no rule matched "Under a Flag of Truce" and
  it opened on the audience painting: "fleets" picks the bridge. The herald is aboard "the house's flagship", not yours: you may not
  be with the fleet.
- **Nearest.** *The Enemy's Envoy* is any war, in your antechamber, with no peace in it.

### 10. Plunder on the Exchange (`plunder_on_the_exchange`; was `the_spoils_of_a_sack`)
- **What happened.** Another house's fleet has sacked a world beyond the rim and come home rich, and its head is selling.
- **Fires when.** `world_sacked` (the year another house sacks a nation's world) and a role `sacker` with `sacked_beyond` (Core),
  not of your house. Repeatable; weight 30.
- **Choices.**
  - *Buy from the plunder* (500 credits): materials +2, exotics +1, their regard +5.
  - *Put money into their next raid* (needs Greedy, Ruthless or Ambitious; 300 credits): their regard +10, marks them
    `war_partner`.
  - *Ask the Senate for a tax on plunder* (1 influence): their regard -15, the commons +3.
  - *Have nothing to do with it*: nothing.
- **Numbers.** At the Exchange's base prices (materials 150, exotics 700) the lot is worth about 1000: plunder sells at half, as the
  label says. (It was three lots of metals, about 1150 for 500, a sure profit every time.) The choice is whether the house wants a
  share of a sack at all. Renamed in review: "sack" in the id opened a sale at home on the battle painting; "exchange" picks the market. The stake is 300 credits, which the second step pays back as 800.
- **Nearest.** *The Sack* is your own sack. This is another house's, and the beginning of a share in it.

### 11. A Scandal in the Accounts (`a_scandal_in_the_accounts`)
- **What happened.** The war beyond the rim is over, and the share you put into another house's raids has come back with interest,
  with the Senate's auditors behind it.
- **Fires when.** `nation_war_over` (Core: a war with a nation has ended this year or last, however it ended) and a role `partner` with
  `war_partner`. Repeatable; weight 35. (A first version read `foreign_peace` and the peace flags; those miss a war that is won,
  lost or guttered out, and in the soak six stakes came to no scandal.)
- **Choices.**
  - *Keep the 800 credits, and say nothing*: 800 credits, legitimacy -2.
  - *Give it to the families of the crews*: legitimacy +1, the commons +3.
  - *Share it with the auditor, and keep 500 credits* (needs intrigue 3): 500 credits, `corruption` +1 (as *A Household Scandal*),
    your stress +2. The label says an auditor of the Senate will know what the house is.
  All three clear `war_partner`.
- **Numbers.** 300 in, 800 out over a war is a good raid; 2 legitimacy is the price of being seen to have profited. Round 2 gave the
  bribe a price (it gave 500 for nothing), and wrote both money labels as money kept: "(500 credits)" after an action reads as a
  cost everywhere else in the file.
- **Nearest.** None reads a stake in another house's war.

### 12. The Crews Want Paying (`the_fleet_paid_off`; was *The Fleet Paid Off* and *Who Pays for the War*)
- **What happened.** Your house's war is over, and its crews are coming home: there are more of them than the peace wants, and they
  are owed their pay. Someone has to find it.
- **Fires when.** `our_peace` or `our_nation_war_over`, `at_war_any` false, a fleet of two hulls or more, and `crews_reckoned` false.
  Every choice sets `crews_reckoned`, which `FleetSystem.WarMarks` strikes out while the house is at war again or once the news of the
  peace is over: once a peace, not once a briefing (the peace is news for two). Repeatable; weight 30. Each label says which estate it
  moves, and the chips give the number. The id is kept: "fleet" opens it on the bridge painting.
- **Choices.**
  - *Pay them off in full from the treasury* (600 credits): the officers +3, legitimacy +1.
  - *Settle them on the house's worlds* (needs a world; 200 credits): manpower +3, the commons +2.
  - *Lay a war tax on the people of your worlds to pay them* (needs a world): the officers +3, the commons -4. The tax pays the
    crews, so no money comes to the house.
  - *Turn them loose unpaid*: the officers -4, the commons -3, legitimacy -1.
- **Numbers.** 600 is what *The Fleet Comes Home* pays crews off on the quay; settling is cheaper and gives back manpower. The tax is
  the treasury's 600 and a point of legitimacy traded for the commons' -4, the trade *The Press Gang* makes.
- **Why one card.** Round 2's critics found the two cards came at the same peace with no gate between them, about one bill: "the
  crews want paying" beside "the treasury is close to empty". *Who Pays for the War*'s war debt was not real (nothing was owed if you
  chose nothing), so its taxes were money for an estate's goodwill. Now the tax has a debt behind it. The theme is not thinner: the
  freed place went to *Wounded Aboard the Fleet* (6b). *Keep them under arms* went with the merge: it cost nothing and settled nothing.
- **Nearest.** *The Fleet Comes Home* is an expedition back from beyond the rim. This is the end of a war, and the choice to settle,
  tax or turn them loose is new.

## What was cut in review

- *A levy that bites one of your worlds*: the only hook that fits is the house's manpower running low in a war, and every choice I
  could give it (press, pay bounties, exempt the harvest) is *The Press Gang* again.
- *The families of the crews lost*: *The Butcher's Bill* already pays the families from the purse. A version about one widow needs a
  mark on the dead officer's spouse; not worth a Core change for one card.
- *Hired service* (`service_asked`, `service_given`, `service_ended`, and the companies of `MercenarySystem`): the randomised player
  never hires a company out and the service flags were raised twice in eleven games, and the ask is already answered on the
  Military screen. A report on it would repeat the screen.
- *A great ruler dies or rises*: every nation with a great title already has a report for the rising, and the Vashti for the death;
  `great_ruler_dead` for the others came three times in eleven games, and a ruler beyond the rim is not a person a report can bind
  without a new mark on the ruler's character.
- *The ship that does not come back*: a fleet destroyed happens a few times a game and *The Line Breaks* and *The Butcher's Bill*
  both answer it.
- *A world of yours sacked*: no hook fired for it in the soak (`raided_by_nation` was never raised), and *Raiders From Beyond* is on it.

## What was added to Core

All are marks: a char flag or a world flag set at the moment and struck out a year later, the pattern the other systems use.

- `FleetSystem`:
  - `in_the_battle` (char): after a battle your house fought, on each admiral and captain of your house who was in the line, not
    you, with `in_a_won_battle` or `in_a_lost_battle` beside it by how the battle went (round 1: a house that won one battle and lost
    another in a year named an officer of the defeat as the one who won the day). All struck out at the next `FleetSystem.Tick`.
  - A battle lost wounds one in three of the house's relatives marked `at_the_front` (`FrontWoundOneIn`, by a hash of the person,
    the year and the place, so a game resumed agrees), through `LifeSystem.Wound`, and marks them `wounded_at_the_front`
    (`WoundTheFront`, round 2), on the map or beyond it.
  - `MarkTheBattleAbroad` (round 2): a battle one of the house's fleets fights beyond the Reach (`NationSystem.Fight`) marks its admiral
    and captain, of the house and not you, `in_the_battle` and `in_a_won_battle` or `in_a_lost_battle`, as a battle on the map does;
    the world flags are NationSystem's own `expedition_victory` and `expedition_defeat`. Without it a war beyond the rim, the Senate's
    usual war, never named one of your people at the front.
  - `ships_counted` (char, set by the parley) becomes `ships_known` at the next tick, and a fleet whose commander carries it fights at
    `CountedPower` (0.9) that year.
  - `officer_owed`, `officer_wronged`, `at_the_front`, `blooded` and `wounded_at_the_front` are struck out on the house's people once
    the house is at war with nobody and neither `our_peace` nor `our_nation_war_over` is raised (`WarPeopleChars`): the reports of the
    peace have had their briefings. `crews_reckoned` (world) is struck out then too, and whenever the house is at war again.
  - `taken_prisoner` (char): when your house wins a battle that cost the beaten line `PrisonerLosses` (2) hulls or more, the first
    captain of the beaten line still alive (from its strongest fleet; of that house; not its head; not a nation's) is taken, or,
    where it had none, an admiral of it whose fleet went down. They leave the captaincy, and `CaptainRefusal` refuses them any
    captaincy while it lasts. `held_to_the_peace` (char, set by the report)
    and `on_parole` (char, set by the report) go with it. All three are struck out, and the prisoner goes home with a line in the
    chronicle, at the first tick at which their house is no longer at war with yours.
  - `enemy_bears_down` (world, marked) and `bears_down_on_us` (char on the fleet's commander): at the end of the tick, an enemy fleet
    under way that comes next year into a system where a fleet of yours lies. Struck out at the next tick.
  - `our_peace` (world, marked): `MakePeace`, when your house is one of the two. Struck out when the mark is more than
    `PeaceNewsYears` (2) years old (round 1: it was one, so a peace made after a year's briefing was read by one briefing only).
  - `Captive(c)`: whether a person is a prisoner.
- `NationSystem.War.cs`, `NationSystem.Map.cs`: `sacked_beyond` (char) on the head of a house of the Reach, not yours, whose fleet
  sacks a nation's world; struck out with the `world_sacked` flag.
- `NationSystem.cs` (`EndWar`): `nation_war_over` (world, and `nation_war_over:<nation>`, marked), the year a war with a nation ends,
  however it ends; struck out a year later than the other map flags (round 1: a war that ended after a briefing, by the player's
  peace or a vote, was struck out before the next one read it). Round 2: `our_nation_war_over` beside it, the same way, only when the
  war was the house's (`WasOurs`: its own private war; or the Reach's war, with the nation on the map, where every house of the Reach
  is its enemy, or with a fleet of the house sent against it: one still away, or one that fought there, counted by
  `expedition_fought:<house>:<nation>` and struck with the war). The probe of round 2 found every fire of the old fleet report going
  to a house that had no enemy, and the peace reports coming "at the peace" while the house was still at war.
- `NationSystem` (round 2): `FleetSystem.RefreshWarFlags` runs when a war with a nation begins (`DeclareWar`, `PrivateWar`) or ends
  (`EndWar`), and at the end of the nations' tick (a nation that struck; a war one house fights on against the Senate's peace). It ran
  only at a house's own war or peace, so `at_war_any` disagreed with `World.EnemiesOf` in 61 of 938 briefings of the probe.
- `ServiceSystem.FreeRefusal`: a prisoner may not take a command by service.
- `NpcAi` (another theme's file, one condition): a house does not make a prisoner of war an admiral. Without it a captain taken
  from the wrecks could command a fleet the next year. Round 1 added the same condition to the levy officer a province sends
  (`ProvinceSystem`) and to a relative stepping up to an empty post (`FamilySystem.StepUp`), each another theme's file.
- `ChoiceStakes` (round 1): a choice's chips show `estate_offset:<estate>` as "the Commons +4" and `public_trust` as "trust in the
  magistrates +1", and a `kill` as "{name} is killed". Many war choices moved an estate and showed no chip at all.

Tests: `tests/WarEventsTests.cs` builds each moment, shows each report eligible, applies every choice, and checks each mark: who is
marked in and after a battle (on the map and beyond it), the prisoner's captaincy and release, the parole's release, the fleet
bearing down, the peace mark and its expiry, the sacker's mark, the end of a war with a nation and whose war it was (another house's
private war, a war beyond the rim the house sent no fleet to, one it did, a war on the map), `at_war_any` following a nation's war,
the wound at the front, the crews reckoned once a peace, and that no house makes a prisoner its admiral. The young one's thread is
walked end to end: sent, blooded and left with the fleet, and home at the peace, and not asked again for a place they have.

## The soak (11 starts, 2 seeds, 60 years, and the three scripted players)

7879 reports in all; these thirteen took 113 of them (1.4%), ten of the thirteen kinds firing. None is above 0.5% on its own: the
most fired are *The Spoils of a Sack* (34), *Who Pays for the War* (33) and *A Parley Before Battle* (24); then *A Place in the
Fleet* (10), *Out of the Line* and *Blooded in Battle* (3 each), *The Officer Who Ran* and *A Scandal in the Accounts* (2 each),
*A Laurel at the Peace* and *The Fleet Paid Off* (1 each).

Three never fired, all at the end of a thread whose first step is rare for the randomised player, who wins a battle in about one
year in 140 (9 of 1278 years) and seldom puts one of the house in command: *Taken in Battle* needs a won battle against a house whose beaten
line had a captain (or lost a fleet with its admiral aboard), *The Parole Broken* needs that and a parole and a fleet given
afterwards, and *The Other Dispatch* needs an officer wronged in a battle and then a peace. Each is built and answered in
`tests/WarEventsTests.cs`, and the marks they read are tested on a real battle there.

Against the baseline soak (the same starts and seeds before these events), the files that gave up most of the share were
`ladder` (10.0% to 9.2%), `situations` and `steward_decisions` (each 0.6 points), `found_family` (0.4) and `career_progression`
and `resources` (0.3 each); the games run differently once new reports fire, so some files gained as well.

## Round 1 of review (2026-11-27)

What the critics found and what changed, in short (each entry above has the detail):

- The officer reports bind on the battle's result (`in_a_won_battle`, `in_a_lost_battle`). "Strip them of the command" became "dress
  them down", which is what the choice does. The peace reports also come at the end of a war with a nation; the dispatch's regard
  gate is 20; and the war's people are struck out once the peace is old news.
- `our_peace` and `nation_war_over` are each read by one more briefing.
- The young one can be wounded, is "blooded" only at 26 or under, and comes home in a new last step, *Home From the War*.
- The parley touches the battle (counted ships fight at 90%; the wardroom minds a truce and cheers the herald sent back) and was
  renamed so it opens on the right painting; so was *Plunder on the Exchange*, whose purchase is now at half the market price, not
  under it.
- *Deserters From the Fleet* reads `fleet_deserts`, which nothing read. *The Fleet Paid Off* was kept rather than cut: with the
  estates now shown on the chips its stakes are no longer hidden, and the war set was the smallest share of the four.
- *Who Pays for the War*: carrying it yourself costs 200 credits; every label is an action and says its estate.
- Words: *A Laurel at the Peace*, *Taken in Battle*, *Blooded in Battle*, *A Scandal in the Accounts*, *The Other Dispatch*,
  *The Parole Broken in the Line*.

`war_score_bonus`, which some existing reports set, is read by nothing in Core; the parley does not use it.

### Measured after round 1

The soak after review (22 games, 7,843 reports) passed. This file took 132 reports (1.7%), ten of its fifteen kinds; none above
0.5%. `deserters_from_the_fleet` and `taken_in_battle` fired once each. Five did not fire: `ran_from_the_battle`,
`blooded_in_battle`, `kin_home_from_the_war`, `the_other_dispatch` and `the_parole_broken`, each needing one of the house named in a
battle the random player seldom fights (and the first soak of review fired three of them). Each is built and answered in
`tests/WarEventsTests.cs`.

## Round 2 of review (2026-11-27)

What the critics found and what changed, in short (each entry above has the detail):

- **The young one's thread can reach its end.** All three answers to *Blooded in Battle* cleared `at_the_front`, so a relative "left
  with the fleet" was safe from wounds, never came home at the peace, and could be asked for a place in the fleet again; the thread
  test went from the first step to the last and never took the second. Now only the one brought home leaves the front, every
  answer marks `blooded` (once a relative), and *Home From the War* clears both. A test walks it: sent, blooded and left with the
  fleet, the peace, and home; and not asked again for a place they have.
- **The peace is the house's own.** `nation_war_over` is raised at the end of any war with a nation: one the Reach fought without
  the house, another house's own war. A probe of 8 starts by 2 seeds found every fire of *The Fleet Paid Off* going to a house with
  no enemy, and *Who Pays for the War* mostly to houses still at war, once twice for one war's end. `EndWar` now also raises
  `our_nation_war_over` when the war was the house's (`NationSystem.WasOurs`), which the peace reports read, with `at_war_any`
  false. *A Scandal in the Accounts* keeps reading any nation's end: it is about another house's raids.
- **`at_war_any` follows a war with a nation** on the map: it is refreshed when one begins or ends, and at the end of the nations'
  tick. It disagreed with `World.EnemiesOf` in 61 of 938 briefings.
- **A war beyond the Reach has people in it.** A battle an expedition of the house fights marks its admiral and captain as one on the
  map does, and a battle lost there may wound a relative at the front; *Out of the Line*, *The Officer Who Ran* and *Blooded in
  Battle* read `expedition_victory` and `expedition_defeat`; *A Place in the Fleet* comes while the house has a fleet away at such a
  war.
- **One bill, one card.** *The Fleet Paid Off* and *Who Pays for the War* became *The Crews Want Paying*, once a peace
  (`crews_reckoned`), with a war tax that pays the crews. The freed place is *Wounded Aboard the Fleet*, which makes the label "A
  battle lost may cost them" a thing the player hears of.
- **A Scandal in the Accounts:** the auditor's share costs corruption and stress, and both money labels say money kept.
- Words: *Taken in Battle* (the ransom label, the house's flagship, "{self.house}'s capital"), *A Parley Before the Fleets Meet*
  (title for the painting, "fight when they come", the house's flagship), *The Other Dispatch* ("It" follows the account), the
  influence costs in labels, *Plunder on the Exchange* ("holds full"), *Blooded in Battle*, *Deserters From the Fleet*.

### Measured after round 2

The soak after round 2 (22 games, 7,996 reports, `qa --scenario all --seeds 2`) passed, with no unresolved token. This file took 87
reports (1.1%; 132 and 1.7% after round 1), eight of its fifteen kinds, none near 1.5%. Most of the fall is the point of the round:
the peace reports now come only at a peace of the house's own with no war left (in the probe, most fires of the two bill cards had
gone to houses that were not in the war, or were still at war), and the two bill cards are one, once a peace. Seven did not fire:
`ran_from_the_battle`, `taken_in_battle`, `the_parole_broken`, `wounded_aboard_the_fleet`, `kin_home_from_the_war`,
`a_laurel_at_the_peace` and `deserters_from_the_fleet`. Each needs a rare moment for the random player: a battle the house fights
with one of its own in the line (about one year in 140), then a lost battle with a relative at the front, or a peace with no war
left after an officer was named; or a fleet that deserts (it fired once in round 1). The random player never sends a fleet beyond
the Reach (`expedition_away`, `expedition_victory` and `expedition_defeat` were never raised), so the new abroad branches had no
chance in the soak. Each report and each branch is built and answered in `tests/WarEventsTests.cs`, through the real battle and the
real end of a war where the engine raises the moment.
