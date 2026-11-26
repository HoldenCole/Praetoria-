# The Caesar road

Built 2026-11-14. Stages 4 and 5 of docs/REPUBLIC.md. Before it, a general who won battles got legitimacy for his
house and nothing else: the Senate could not honour him or call him home, the crews were the house's whoever led them,
and nobody could march on the capital. Now a commander makes a name, the Senate answers it, and the answer can end the
Republic.

Two rules from docs/PROCESS.md govern it.

**The Republic stands unless the player ends it.** Nothing ends it by a clock or by default. A Caesar is rare for a
reason: he has to be extremely able and extremely well liked, and the times have to be his. Another house's general
marches only through all three gates below, and should succeed about once in ten games or less. The player may try
without them, and can fight a rival who tries.

**You play one person.** The general may be you or one of your house. If it is one of your
house, the victories, the crews and the triumph are theirs. What the *house* does about a recall is the head's to say.

## The crews and their commander

Every fleet has a **devotion** (0 to 100) to the person who commands it, beside its loyalty to the house.

- It rises with **victories** under that commander: 6 a battle, 12 if led from the front. A battle lost costs 6.
- A point or two a year for the years together. A **donative** (200 credits a hull from the house, once a year) adds 8.
- At **50** the crews follow the commander before the house.
- **A new commander starts again.** If the crews were the old one's, they resent the change (loyalty −10), and if their
  commander dies they are sullen (−8).
- Someone who has risen in the service (third rank or higher) and takes a command in a saga's first three years is
  credited with the service they had before it: two victories a rank above the second, and crews who know them.

## Sway

What a commander could move the Reach with, 0 to 100. Shown on the Military screen with its parts.

| Part | Points |
|---|---|
| Battles won in command, in a whole life | 6 each, to 30 |
| Of them, won from the front (valour) | 3 each, to 15 |
| The name the Reach gives (Achilles) | 10 |
| Hulls whose crews follow | 2 each, to 30 |
| Triumphs | 12 each, to 30 |
| Rank in the military service | 2 a rank |

A house's **general** is whoever of its own people commands one of its fleets and has the most sway.

## The triumph

A motion before the Senate (`triumph`, 2 influence), for the general of a house. Any house with a voice in the Senate may
move it, for itself or another, and so may anyone who has held a magistracy, whatever door their house sits by.

- It asks **three victories** since the last triumph. A victory beyond the Reach counts; bringing a nation to terms
  counts two more.
- **Carried:** the general's count starts again, and the triumph is offered. It is **had at the gate**: the general
  comes home and lays the command down for a year to have it, or declines it and keeps the fleet (below).
- **Had:** the house gains 6 legitimacy and glory, the fleets the general commanded gain devotion (+10) and loyalty
  (+5), it counts toward the first rank and the sway, and it is written into their life.
- **Refused:** nothing is given, the crews take it personally (devotion +4), and it is a **cause**.
- Houses lean for a friend, a party fellow, a general the squares love; against from enmity, from envy (the other
  great names in arms), and the Optimates against one who already has much sway.

### At the gate

Built 2026-11-26 from docs/IDEAS_FROM_IMPERATOR.md, entry 1, which closes the rest of docs/GAPS.md Q15. What was asked:

> A general voted a triumph must come home and lay the command down for the year to have it. While they are home the
> fleet sits under its captain, and its crews' devotion does not fade. They may decline the triumph to keep the fleet. And
> while someone commands a fleet away from the capital, or governs abroad, they cannot stand for a magistracy or the
> consulship unless the Senate votes them leave to stand in absence. So a general chooses between glory, office and
> command, and a senator can move a triumph for a rival to take a fleet off them without a recall.

> (Q15) A triumph can be moved only by a Prefect or a Judge.

**Coming home.** The honours are given at once (above), while the crews still have their commander to march behind.
Then every fleet the general commands is laid down until the end of the next year: it waits under its captain, who
commands it in battle, or with nobody in command if it has none. It is **held** for the general: the crews' devotion to
them does not fade, nobody is put in the empty flag unasked (another house's head, a relative stepping up, a legate, an
officer borrowed), and the general takes no other command meanwhile (the same bar a recall leaves, for the year). When
the year is out they take the command up again if it is still theirs. It is not if the fleet is gone, has passed to
another house, is laid up, or was given to somebody else meanwhile (the player's head may do that; then it is theirs);
nor if the general has died, left the house, grown frail or retired. A command by service waits too, and lapses
meanwhile only by its own rules (docs/MILITARY.md, "A command by service"): if the consul who named a legate leaves
office in that year, there is nothing to take up. Home, the general commands nothing, and may stand for office in person
(docs/LADDER.md, "Standing in absence").

**Declining.** The general keeps the fleet and nothing is given: no triumph is counted, and the victories that earned it
were counted off when it was voted. It is the general's own choice, and the Senate takes no offence: it is not a cause.
It is written into their life ("declined it to keep the command").

**Who chooses.** The player is asked, on the Military window (the general and the Senate) and on the Senate window's
chamber (`data-triumph-choice="home"`, `"keep"`), and the outliner says so. The answer is wanted by the end of the year
after the vote; left unanswered, the triumph lapses as if declined. One of the player's house chooses for themselves, as
another house's general does, in the year it is voted, by their nature and the war they are in, and the head is told
why. They keep the fleet when what follows sums above nought:

| what weighs | toward |
|---|---|
| the fleet is at war (its house at war, or beyond the Reach) | keep +2 |
| and the war is going badly | keep +1 |
| wants the throne | keep +2 |
| wants a command of their own | keep +1 |
| wants to outshine every rival, or to master the Senate | home −2 |
| loyal, honourable or just: they keep the Senate's custom | home −1 |
| proud, arrogant or ambitious: they want to be seen | home −1 |
| ruthless, cruel or suspicious: what is let go may not come back | keep +1 |
| a count is next year or the year after, and they could stand | home −1 |

**Who may move it.** Before this, only a house with a voice in the Senate (land of rank, a Judge at its head, the census,
a great name in arms) could move a triumph. Now anyone who has held a magistracy may, whatever their house's door
(`Character.Cursus`, or a seat now): a triumph and leave to stand in absence are the generals' business, and the only
motions so opened. A house with no voice is told so: "the house has its votes but no voice in the Senate, and you have
held no magistracy". Other houses move by the same rule.

#### The numbers

| what | number |
|---|---|
| the command laid down | to the end of the year after the answer (`TriumphSystem.HomeYears` 1) |
| the player's answer | wanted by the end of the year after the vote; left, it lapses as declined |
| the honours when had | as before: 6 legitimacy, 2 glory, the crews' devotion +10 and loyalty +5, a triumph counted |
| the proposer | thought 10 the better of when it is voted, whatever the general then does |
| declined | nothing given, the victories counted off at the vote, no cause |
| leave to stand in absence | 2 influence; lasts until the next election (the ladder's or the consuls') has been held |
| who may move a triumph or leave | a house with a voice, or anyone who has held a magistracy |
| other houses ask leave | in the two years before a count, one a year: their consul, or their general with sway of 20 or more; a friend of regard 30 moves it for a house with no voice |
| other houses' commanders come home to stand | the year before a count, the only one the house has for a rung or the consulship, unless at war; the fleet held until the count is past |
| finished triumphs kept for the record | 40 |

#### Measured

The Caesar road, as the probe measures it (`praetoria caesar --scenario all --seeds 10`: six Republic starts, ten
seeds, sixty years, a passive player), before and after. The doc's rule is that another house's general marches about
one game in ten or less.

| another house's general, in 50 Republic runs of 60 years | before | after |
|---|---|---|
| triumphs voted, of those moved by other houses | 26 of 47 | 34 of 57 |
| recalls carried, of those moved | 9 of 17 | 5 of 9 |
| was of the first rank | 5 | 3 |
| was loved | 48 | 47 |
| was both | 0 | 0 |
| passed all three gates, and would march | 0 and 0 | 0 and 0 |
| civil wars, and the Republic fell | 0 and 0 | 0 and 0 |

The Caesar rate is where it was: no general of another house marched in fifty runs before or after. What moved: more
triumphs were moved and carried, though hardly any by the new door (none in the sixty-year runs below), so that is
mostly the world running otherwise; a little fewer generals reach the first rank (a triumph declined counts for nothing,
and one had costs a year in command); and recalls fall by half (a general home for a triumph, or home to stand for
office, commands nothing and cannot be recalled, and their sway is lower for the year). A first try that let other
houses' commanders come home to stand without holding their fleets for them lost a third of the loved generals (33 of
50), because the crews took to whoever was put in their place; holding the fleet mends it.

The new rules with nobody steering the player's house (`praetoria triumph`, two seeds of each start; the old rules
measured the same way by a scratch probe):

| | the Old Lion, 2 games of 30 years | every start, 16 games of 30 years | every start, 16 games of 60 years |
|---|---|---|---|
| triumphs moved by other houses, and voted | 0 and 0 | 0 and 0 | 6 and 4 |
| had at the gate, and declined to keep the fleet | 0 and 0 | 0 and 0 | 3 and 1 |
| moved by a house with no door, as one who has held a magistracy | 0 | 0 | 0 |
| leave to stand in absence moved, and granted | 3 and 3 | 32 and 16 | 90 and 58 |
| of them moved by a house with no door | 0 | 3 | 3 |
| elected while commanding away, by leave | 2 | 9 | 31 |
| came home the year before a count to stand, and took the command up again after | 75 and 72 | 486 and 471 | 1,099 and 1,041 |
| could have stood but commanded away without leave, at a count | 44 | 285 | 1,318 |
| magistrates elected, of whom commanding a fleet at the count's end (before, the same) | 87, 28 (89, 33) | 564, 152 (582, 204) | 1,091, 288 (1,119, 403) |
| seats left empty at a count (before) | 9 (7) | 124 (106) | 173 (145) |

Triumphs are rare in thirty years with a passive player (the old rules moved none in those sixteen games either), so
the sixty-year run is the one that shows the gate: of four voted, three generals came home for it (they kept the
Senate's custom) and took the command up again a year later, and one kept the fleet (wanting the throne). The rule on
standing is felt far more often than the triumph: most of the Reach's officers command somewhere, so at every count a
few houses have their only candidate come home for it, and a few ask leave instead. Somewhat more seats go unfilled
than before (173 against 145 at the counts of the sixty-year runs), and far fewer magistrates are commanders.

#### What I chose

- Declining is the general's own choice and no cause: the Senate offered an honour and it was not taken. Nothing is given,
  and the victories that earned it were counted off at the vote, so the same general is not voted another at once.
- The honours are given when the general comes home, not at the vote, so `triumph_voted` and its event follow the
  triumph that was had. The proposer is thanked at the vote.
- The player's answer is wanted within the year after the vote; silence is a refusal. A relative of the player's house
  chooses for themselves, as another house's general does, and the head is told why: the triumph is theirs (above,
  "You play one person").
- The weights of a general's nature and war, in the table above, and that one keeps the fleet only when they sum above
  nought.
- Home is one year: laid down when the answer is given, taken up at the end of the next year. The general may hold no
  other command meanwhile (the bar a recall leaves, for the year), and nobody else is put in the empty flag unasked;
  the player's head may give the fleet to another, and then it is that one's.
- A command by service waits for its general, and ends meanwhile only by its own rules.
- Away is any fleet the person commands that is not lying at the capital's system, under way, or beyond the Reach.
- Leave names a house and covers all its people who command away, for one election; carried in the year of a count it
  is good for that count.
- Leave and a triumph are the generals' business, and both are opened to anyone who has held a magistracy (a rung of
  the ladder in their record, or a seat now); no other motion is.
- How the houses lean on leave (docs/LADDER.md, "Standing in absence"), and that it costs 2 influence.
- Other houses ask leave only for their consul or for a general of sway 20, so that the docket is not filled with it;
  and the year before a count the only candidate a house has comes home to stand, unless at war, with the fleet held for
  them until the count is past.
- The player's own relatives are not brought home to stand: the head may take them off a fleet.

#### What is not built

- No new events: *The Triumph* and *A Triumph In The House* now follow a triumph had at the gate; nothing is reported
  for a triumph declined, for the year at home, or for leave given.
- Nobody moves a triumph for a rival on purpose to take a fleet off them; the lever is there for the player, and other
  houses move triumphs as before, for themselves or a friend.
- Governing abroad (docs/IDEAS_FROM_IMPERATOR.md, entry 6) is not counted as away: there are no governorships yet.
- Leave is for a house, not for one person, and there is no leave for the consulship alone.
- A magistrate or consul elected in absence holds the office while commanding; nothing asks them to come home.
- The random and scripted players (`src/Tools/Qa.cs`) do not yet answer a triumph: theirs lapse, as a passive player's do.

## The recall

A motion (`recall`, 3 influence) that the general of a house lay down the command and come home. It can only be moved
against one with sway of **40** or more.

- Houses lean for it from enmity, from fear of a large name (45 and over), and the capital's holder and the consuls
  most of all; against it for a friend, a party fellow, a patron, and when the general's house has twice their fleet.
- **Carried:** the general has two years.
  - **Another house's general** answers within the year: he crosses the line if he would and can, and otherwise
    comes home.
  - **You**, if you are the general: come home, or cross.
  - **One of your house:** a loyal one keeps the law and comes home the next year by themselves. A wilful one waits to
    hear what the head will do. The head may order them home (a wilful one resents it) or cross the line with them.
  - **Left unanswered** to the deadline, the Senate's officers take the command and the house loses 6 legitimacy. A war
    never starts by neglect.
- **Coming home:** the fleets are left without a commander, the general may hold no command for five years, and the
  house gains 4 legitimacy for keeping the law.

## The three gates

What a Caesar is made of. Shown on the Military screen for your own general, each as met or not yet with what it
lacks.

| Gate | It asks |
|---|---|
| **A commander of the first rank** | tactics of 6 or more, four battles won in command, and a triumph |
| **Loved** | liked by nature (charisma 5, or charisma and oratory together 8) or made the squares' own by deeds (the name the Reach gives one who leads from the front, a second triumph, or seven victories); four hulls whose crews would follow; and a house with legitimacy of 50 or more |
| **The times** | the Republic is **troubled**: two or more things wrong at once |

**What can be wrong with the Republic:**

- the War of the Reach is in living memory (the fifteen years after it ends);
- the lanes of more than one region are lawless;
- the house that holds the capital is not respected (legitimacy under 40);
- a war beyond the Reach is going badly;
- the houses are at war with each other (four wars or more);
- the Exchange has crashed.

**Another house's general** crosses only if all three gates are met, he is the kind who would (not loyal by nature, and not one who wants only a quiet life), the Senate has given him a
cause, and his house's fleet is at least two fifths of the strongest other. One gate short, he comes home when recalled.

**The player** needs only what "Crossing the line" asks. The gates then decide who stands with them: every house weighs
the times (a sound Republic is defended: −2; one thing wrong −1; three or more +1), whether the general is of the first
rank (+1) and whether he is loved (+1, or −1 for one with no charm and no name). The panel says how many houses would
declare each way if the house crossed today.

**Warnings before anything is done.** When the Republic is troubled, when another house's general is both of the first
rank and loved, and when your own house has passed all three, the outliner and an event say so.

## The War of the Reach no longer makes an emperor

The game's great war used to end with the winning side's strongest house on a throne, which ended the Republic in
about two runs of three around year 28 to 40. It is still fought, won and lost. Its victor gains legitimacy, glory and
rank, and the Senate still sits. The Republic is shaken for fifteen years afterwards, which is one of the things that
can be wrong with it. A house that wants a throne must take it: by this road, or by the older Seize and Proclaim
commands, which only the player uses.

## Crossing the line

The head of a house may march its general on the capital when all of these hold:

- it is the Republic, and the Reach is not already at war with itself (a march, or the great war);
- the house does not itself hold the capital;
- **four hulls** or more would follow the general before the house;
- the general's sway is **40** or more;
- the Senate has given a **cause** in the last five years: a recall, a triumph refused, or a censure. Having crushed
  all resistance in the War of the Reach (docs/BREAKAWAY.md) also counts: the Reach is in the house's hands.

Then:

- **The houses declare.** Each weighs its regard for the general's house, party, patronage, what it depends on that
  house for, and fear. The house that holds the capital is always for the Senate. The rest are for the general, for
  the Senate, or stand aside. **The player's house is not declared for:** you choose, or say nothing.
- **A few fight.** The three strongest for the general and the four strongest for the Senate, with the capital's
  holder, go to war. The Senate gives its war to a consul's house if one stands with it, else to the strongest: **the
  Senate's sword**.
- Crews that follow the general stay with him; some of the rest of his house's crews will not fire on the Republic
  (loyalty −15).

## How it ends

Each year the war's standing is the lead in its battles, plus one for the stronger side in any year nobody met.

**The general wins** if his house's fleet holds the capital's orbit two years running with none of the Senate's there,
or the standing reaches +8 after two years.

- **The Republic ends.** His house takes the throne (by force, so its legitimacy is not raised).
- Those who declared for him gain legitimacy and his regard; those against lose both; the Senate's sword is
  proscribed and forfeits three tenths of its treasury.

**The general loses** if his house has no fleet left, or a quarter of what it crossed with after a year, or the
standing reaches −8, or ten years pass, or he dies.

- **He does not outlive it.** If that is you, the seat passes to your heir.
- His house loses 25 legitimacy and four tenths of its treasury, its offices, and may not stand for consul for ten
  years. Those who declared for him lose legitimacy.
- The Senate's sword gains 10 legitimacy and its general a triumph.

A march is cut short without a victor if a throne is taken some other way or the great war breaks out.

## Where it is

- **Military screen**, "The general and the Senate": the general's sway and its parts, victories toward a triumph,
  whose the crews are (with the donative), the recall and its answer, the march and why not, a civil war's sides and
  standing with the buttons to declare, and the names the Senate watches.
- **Senate screen:** Triumph, Recall and Leave to stand in absence are motions like any other. A triumph voted to you is
  answered on the chamber's tab as well as on the Military screen.
- **Outliner:** a recall to answer, a triumph voted to you to answer, a march to declare on, a triumph that could be asked.

## Events

Fifteen, in `content/events/caesar.json`. Three are warnings: *The Republic Falters*, *A Name On Every Lip* (another
house's general who is of the first rank and loved), *They Are Saying It Of Your House*. The other twelve: *The Triumph*, *A Triumph In The House*, *No Triumph*, *The Recall*, *They
Have Recalled One Of Yours*, *The Line Is Crossed*, *A General Marches On The Capital*, *The Republic Stands*, *The
Republic Falls*, *The Capital Is Yours*, *The March Has Failed*, *The Crews Are Theirs*. They colour what happened.
What is decided (to come home, to cross, to declare) is decided on the Military screen.

## For content

- World flags, each year: `recalled`, `kin_recalled`, `civil_war`, `civil_war_mine`, `sword_of_the_senate`,
  `general_has_sway`, `laurels_earned`, `kin_general_loved`, `republic_troubled`, `great_name_rises`,
  `the_hour_is_mine`.
- World flags, each year (the triumph at the gate): `triumph_at_the_gate` (a triumph voted to you waits on your answer),
  `home_for_a_triumph` (one of the house is home for one), `leave_to_stand_in_absence` (the house has leave).
- World flags, for a year after: `triumph_voted` and `kin_triumph` (raised when the triumph is had at the gate, not when it
  is voted), `triumph_declined`, `kin_declined_triumph`, `command_taken_up`, `triumph_refused`, `recall_obeyed`,
  `crossed_the_line`, `caesar_marches`, `caesar_crowned`, `republic_fell`, `republic_saved`, `caesar_broken`,
  `served_caesar`, `opposed_caesar`. For good: `republic_fallen`.
- Character flags: `triumphator`, `new_triumphator`, `home_for_a_triumph`, `recalled_general`, `caesar`, `loved_by_the_fleet`,
  `first_emperor`, `great_name`.
- Counters: `laurels:<id>`, `victories:<id>`, `triumphs:<id>`; `triumphs_voted`, `recalls_voted`, `civil_wars`,
  `republics_ended`, `caesars_broken`; the gate's `triumphs_had`, `triumphs_declined`, `triumphs_lapsed`, `commands_taken_up`,
  `commands_not_taken_up`; the leave's `absence_leave:<house>` (the year it was given, while it stands), `absence_moved`,
  `absence_granted`, `absence_refused`, `absence_vetoed`, `absence_spent`, `elected_in_absence`, `names_taken_down_away`.
- No new effect types.

## How it is built

- `src/Core/Systems/CaesarSystem.cs`; state in `src/Core/State/Caesar.cs` (`Recall`, `CivilWar`), `Fleet.DevotedTo`
  and `Fleet.Devotion`, `World.Recalls`, `World.CivilWar`.
- `CaesarCommand` (obey, cross, join, donative) in `src/Core/Commands/HouseholdCommands.cs`.
- The triumph at the gate and standing in absence: `src/Core/Systems/TriumphSystem.cs`, state `World.TriumphVotes`
  (`src/Core/State/TriumphVote.cs`), `TriumphCommand` (`src/Core/Commands/TriumphCommands.cs`), the motion `absence` in
  `content/senate/motions.json`, `src/Web/Components/TriumphGate.razor`, `tests/TriumphAtTheGateTests.cs`, and
  `praetoria triumph` (`src/Tools/TriumphProbe.cs`).
- Battles call `CaesarSystem.Victory`; expeditions call `ExpeditionVictory` and `Subdued`.
- `SenateSystem` leans on, applies and (for other houses) moves the two motions. The generals' business is moved
  whoever else has business that year.
- `ThroneSystem.Enthrone` no longer assumes the one taking the throne is the player.
- `src/Web/Components/CaesarPanel.razor`. `tests/CaesarTests.cs` (10). `tools/browser/caesar.js`.
- `praetoria caesar --scenario all --seeds 5`: how far generals get in sixty years with a passive player.

## Checked

- 594 tests. One walks all three roads (your own, a relative's, another house's) and resolves every choice of the
  fifteen events. One shows another house's general refusing to march with any one gate unmet, and marching with all
  of them. One shows the same general finding more houses for him in a troubled Republic than a sound one. One runs
  sixty years on two seeds and asserts there is never more than one civil war and none under a throne.
- The soak passes (eleven starts, three seeds, sixty years, save and load compared), with the random player answering
  recalls, crossing, declaring and paying donatives when it can. The survival probe is unchanged.
- In the browser on The Old Lion the panel reads the general's sway, the three gates with what each lacks, the state
  of the Republic and who would declare, and a donative moved the crews' regard. No errors.

## What the probe found

`praetoria caesar --scenario all --seeds 10`: sixty Republic runs of sixty years with a passive player.

- **The Republic stood in all 60.** Before the rule it fell in about two runs of three, to the great war.
- The Senate's business with generals is lively: 64 triumphs voted of 112 moved, 11 recalls carried of 38 moved.
- **A rival is possible and very rare.** After the last adjustment (seven victories or charisma of 5 count toward being
  loved; the Senate's guardians recall a general of that stature) another house's general was of the first rank in 15
  runs of 60, loved in 42, both in 3, and passed all three gates in 1. None marched in those 60. That is on the rare
  side of the owner's "once in ten games or less", on purpose. The knobs: `GateVictories`, `GateTactics`,
  `GateHeroVictories`, `GateCharisma` and `TroubledAt` in `CaesarSystem`. Change one, run the probe.
- Found while measuring: the benches used to recall any commander who had won twice, and he came home and lost his
  command, so nobody could ever grow into the full figure. Other houses now move a recall only against a general they
  hate, or (the capital's holder and the consuls) one whose sway alarms them.

The older routes to a throne (Seize and Proclaim) are the player's alone; no other house uses them.

### Since then: parties, the sword, and what a victory teaches

- **The parties go to war as parties.** Each house still weighs the general for itself. Then a bench whose leader has
  declared brings its members with it, all but one with a strong reason of its own (a score of 4 or more for the
  general, or −3 or less against). The general's own bench is his. The forecast on the panel uses the same rule, so
  what it shows is what happens.
- **The Senate's sword commands the Reach's own fleets.** When the Senate gives its war to a house, every hull of the
  state that is not the marching general's own devoted command goes under that house's colours for as long as the war
  lasts, and goes home when it ends. If you are the sword, they are yours to fight with.
- **Old ties** (docs/LIFE.md) move a house two points toward or away from the side the player is on.
- **A victory teaches only up to the ceiling** (docs/EDUCATION.md). Measured again with all of this in
  (`praetoria caesar --scenario all --seeds 10`, 50 Republic runs): the Republic stood in all 50; another house's
  general was of the first rank in 2 runs, loved in 42, passed all three gates in 1, and none marched. Before, skill
  rose without limit and the first rank was reached in 15 runs of 60. A rival is rarer than it was. That is inside the
  owner's rule (once in ten games or less) and may be too rare: docs/GAPS.md C9.

## Not in this piece

- A general does not defect with his crews to another house, and crews do not follow a dead commander's heir.
- No proscription lists, no amnesty, no negotiated end.
- A relative who is the general cannot cross against the head's wishes.
- The player cannot yet *make* the times: nothing lets a house deliberately trouble the Republic, or mend it to stop a
  rival.
- Under a throne there is nothing here yet: no triumph by the throne's gift, no guard. That is politics under a throne.
- Every number is a first guess.
