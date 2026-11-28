# The Academy: a year apart from the world

Owner's direction (2026-11-11): the Academy is **separate from the main game**. A cadet of sixteen or seventeen does
not interact with the world. You enter at sixteen, the year takes about a year, and it is the **tutorial**: a first-time
player is taught how everything works and what the state of politics is; a returning player can skip the teaching and
be told only the state of politics, the houses that matter and who is in their year. In the **Republic** the Academy is
academics and friendly competition: there are war games, but nobody kills a classmate and the brutality is low. Under
the **Imperium** it is the Institute: warlike, cadets in command of factions, and people get hurt.

## How a run opens
A scenario with `"academy": true` (`the_academy`, Republic; `the_institute`, Imperium) starts in the Academy year.
`WorldBuilder` calls `AcademySystem.Enter`: the protagonist is sixteen, rank zero, and `World.Academy` is active with
its register fixed by the era. The old `academy_crucible` scenario stays as a plain-world fixture for tests and tools
(`"hidden": true`; the title screen does not list it).

## The year
- **Four terms.** `TurnController.BeginTurn` and `EndTurn` call `AcademySystem.BeginTerm` and `EndTerm` in place of
  every world system. `World.Turn` stays 0. Nothing in the world moves: no economy, fleets, Senate, estates, births.
- **Apart from the world.** The command bus runs only `IAcademyCommand`s and answers to Academy events while
  `World.InAcademy`; every other command is refused. The event engine admits only events that carry an `academy`
  condition while the year runs, and none of them after it.
- **The cohort.** At muster, up to eleven cadets of fifteen to nineteen are drawn from the real young of the houses,
  one house at a time, the greatest first. A thin year is filled by houses that sent no one: each enrols a son or
  daughter of sixteen to eighteen, who is a real member of that house from then on. The senior officer of the Reach is
  the instructor. The cohort is the Reach in miniature, and its members are the people you sit beside afterwards.
- **Study.** Each term the cadet reads one of seven subjects (`ChooseStudyCommand`): a point of its skill, and marks
  equal to three plus what they already know of it (to three more). Subjects lead to the three services.
- **Events.** Three a term from the register's own set, answered like any report.
- **The order of merit.** The cadet's score is standing twice plus every mark. Each classmate gains five to nine a
  term by a hash. Place is one more than the classmates who outscore you.
- **Passing out.** After the fourth term the cadet is seventeen, commissioned at the first rung of the service they
  asked for (`ChooseServiceCommand`) or the one they read most. First of the year lifts the house's legitimacy by
  three; the top third pass with honours. Classmates still at rank zero are commissioned too. The next `BeginTurn` is
  turn one of the world.

## The two registers
| | Republic: the Academy of the Reach | Imperium: the Institute |
|---|---|---|
| Terms | Matriculation, Winter, the War Game, Examinations | The Sorting, The Standards, The Long Night, The Last Banner |
| Events | 17: matriculation, lectures, the study circle, the fencing salle, the debating society, the regatta, a prank, the tutor's hour, a visiting senator, the war game on the tactical table, borrowed notes, examinations, the rival's hand | 22: the old crucible (feuds, duels, sabotage, the marked man) and the Standards (the sorting, the banner raid, a captain's justice, the last banner) |
| Harm | None. No Republic event can wound, mark or kill; a test enforces it by content and by play. | Cadets are wounded and humiliated; about one term in three a cadet does not come back, never the last of a house, never more than a fifth of the year. |
| Factions | None. | Four Standards. Each gains strength a term; yours gains what your choices win it. After the second and third terms the weakest loses its banner to the strongest and its cadets serve the victor. Hold the last banner and you have won the Institute. |

Conditions: `{ "type": "academy", "register": "republic", "termGte": 2, "termLte": 3 }` and
`{ "type": "cohort", "role": "friend" }` (the role must be a cadet of the year; `"present": false` for someone outside it).
Counters: `standing`, `standard_captain`, `standard_edge`, `institute_dead`, `academy_place`. Flags:
`academy_graduated`, `standard_fallen`, `won_the_institute`; character flags `first_of_the_year`, `graduated_with_honours`.

## The tutorial
`content/academy/academy.json` carries nine lessons, two a term and three in the last: the Senate; houses, rank and standing; the estates
and the law; coin, worlds and the strategic three; fleets and how they fight; the rings, the raiders and the nations
beyond; a year in the chair; coin, goods and paper (the Exchange); the house outlives you. Each has a one-line `brief` and `full` paragraphs; the first two
have Imperium versions.

When the year opens the player is asked once: *First time here: teach me everything* or *I know the Reach: just brief
me* (`SetTutorialCommand`; the browser remembers a previous graduation and offers the short course first).
- **Full**: this term's two lessons are open in the lecture hall; earlier ones fold away.
- **Brief**: one line a lesson, each openable.
- **Either course reads a lecture folded** (2026-11-28, from docs/STEAM_REVIEW.md 5): two or three sentences, and **Read more** under them
  for the whole of it. The folds are content (`lectures` in `content/academy/first_years.json`, with the Imperium's own where the lecture
  reads otherwise) and `AcademyProjection` hands them on as `AcademyLessonCard.Summary`; a test holds each to two or three sentences and to
  less than half the lecture.
- **Either way** the lecture hall shows the orientation, built from the live world by `AcademyProjection`: the state of
  the Reach (who holds the chair or the throne, the parties with their seats and leaders, the consuls and when the next
  election falls, the offices, the law of the Reach, wars, the nations beyond), the seven houses that matter by seats,
  and the cadet's own house. The year's order of merit shows who is in the cohort and what each thinks of you.

The screen has no rail and no world screens. It closes with a passing-out card: place, service, friends made, and
those who will remember you otherwise. A player who took the full course is not shown the old primer.

## The report card

The review found the Academy ranked a cadet 7th, then 12th of 12, and never said why (docs/STEAM_REVIEW.md 5). Each term now ends
with a report card. `AcademySystem` writes where the order of merit stood as the term opened (`AcademyState.Opening`: the cadet's score,
place, standing and Standard, and every classmate's score) and, as the term closes, a `TermReport`: the subject read and its marks,
standing before and after, the score before and after, the place before and after, the least, most and average the rest of the year
gained, how many passed whom, and under the Imperium the banner taken that term. `AcademyProjection.Card` says it in words, each line with
its figures:

- "You read Fleet Tactics: 4 marks." "Your standing rose from 0 to 2: 4 more marks on your score, since standing counts twice."
- "Your score went from 0 to 8." "The other 11 cadets gained 6 to 10 each, 8 on average." "2 cadets passed you, and you passed 4 cadets."
- "The Wolf Standard took the banner of the Bull Standard." (and, when it was yours, that you serve under the victor and lost 2 standing)

and one tip, the first that fits: your own banner taken; the last term (what honours and first place bring); a subject that would give
2 marks more than the one read; standing that did not rise ("Standing counts twice ..."); a lead to hold; a climb to keep up; the pace of
the year. The tips are content (`tips` in `first_years.json`). The card stands at the head of **This term** from the second term on (the
term just ended), every card is under **So far**, and the passing-out card carries the last (`TermCard.razor`).

## The first years

The Academy taught by lectures on a screen of its own; the real map was not seen until passing out, and the primer that a first game
opened was pages of text pointing at nothing, its "three things to do first" gone when it closed (docs/STEAM_REVIEW.md 5). The first years
teach on the real screen instead.

**The list.** In a first game a short list is pinned at the top of the list on the right (`FirstYears.razor` in the outliner): four to six
steps written for the start (`starts` in `content/academy/first_years.json`, by scenario id), eight for the two Academy starts, which carry
the four terms onto the real screen, two steps a term, each wearing its term's numeral:

| Start | The steps |
|---|---|
| The Old Lion | Answer a report · See who comes after you (Dynasty) · Begin a building · Order your fleet · Read the House ledger · Declare your vote |
| The Fallen House | Answer a report · Read the House ledger · Open the Laws · Order your fleet · Declare your vote |
| The Circling Heir | Answer a report · See who circles (Dynasty) · Open Houses · Begin a building · Read the House ledger · Declare your vote |
| The Risen Officer | Answer a report · Open the You window · Open Military · See the ladder of offices · Declare your vote |
| The Imperial Court | Answer a report · Open the Senate · Open the Laws · Begin a building · Read the House ledger · Declare your vote |
| The New Man | Answer a report · Open the You window · Read the House ledger · Put your coin to work (buy goods or a share, or found a venture) · See what a seat would take (the ladder) · Declare your vote |
| The Academy, The Institute | I: Answer a report, Open the You window · II: Declare your vote, Open the Laws · III: Order your squadron (or, for a house with no fleet, Find your world on the map), Open Military · IV: Read the House ledger, Buy on the Exchange |
| A character of one's own | Answer a report · Open the You window · Begin a building (a house with a world) or Put your coin to work (one without) · Order your fleet (a house with one) · Read the House ledger · Declare your vote |

The New Man holds no world and no fleet, so nothing on his list asks for either: a step's `needs` keeps it to a house that has (or lacks)
a world or a fleet, read when the list begins.

**What ticks a step** is what the player really did (`FirstStepsSystem`; the state is `World.FirstSteps`):

- an act: the command bus calls `FirstStepsSystem.Acted` for every command it carries out, and one done in the player's own name is written
  down by its kind: a report answered, a vote declared (moving a motion or pledging seats declares one too), an order to a fleet, something
  bought (goods, shares, a bond, a venture), a venture founded or enlarged, a building or a work begun, a law changed. The steward who
  begins a building, the marshal who buys fuel and the chancellor who declares the house's votes act in their own names, so none of it ticks
  a step; a test plays two years with nothing done and finds nothing ticked;
- a window or a tab opened: the shared `Tabs` part tells the client which window (the shell cascades its id) and which tab is showing
  (`GameClient.Saw("house:ledger")`), and a world picked on the map is `"map"`.

What is kept is what happened, not which boxes are ticked; which step that ticks is read from the content each time. So the list is kept
with the saga: a save between years or in the middle of one remembers it, and undo takes an act back but not having found where it is
done (`FirstStepsSystem.Carry`).

**Show me.** Each step still to do has a Show me: it opens the window the step is done in and lights the control with a pulsing brass ring
(`js/guide.js`): the vote of the motion before the chamber, a world's Build, the Exchange's order bar, the picked fleet's order bar, the
first unanswered report in the list; the ledger's tab with the treasury in the top bar it accounts for. A step that is the opening of a window lights its socket on the rail and leaves the opening to the
player, and a step that is a tab opens the window and lights the tab, so Show me never does the step itself. A ring goes out when its
control is pressed, when the step ticks, or after twelve seconds. A step that cannot be done yet says why under it ("Not yet: no motion":
nothing is before the Senate in year 1, and what the houses move then is voted in year 2).

**How long.** The list stays until every step is done (it says so for a few seconds, then folds away), the player puts it away (its close),
or the third year ends (`years` in the content). Help can pin it again while the first three years last. With a window open, or on a screen
under 1500 pixels wide, it shows only the steps left to do, a line each.

**A first game** is the browser's: one that has not seen the primer, and has not had a first game's list end (by every step done, put away
or its third year over), begins a new saga with the list. A saga that opens at the Academy has it whatever the browser has done, if the
cadet takes the full course: the list waits until passing out, and a cadet who asked only to be briefed has none (docs/WEB_CLIENT.md,
"The first years").

**The primer points.** Opened by itself in a first game (as before), each page about a part of the screen now points at it: the rest is
dimmed and that part ringed, and the box stands aside from it: your portrait and house, the reports on the right, the year's pools, the
treasury, the windows down the left, the Laws, your house's rank, End Year. Its last page, which used to be "three things to do first" and
was gone when the box closed, is the list: it points at it and lists its steps as they stand (or, in a saga without one, offers to pin it).

**Rail labels** are on by themselves in a first game for its first three years (`labelYears`), unless the player has chosen in Options.

## The handbook: the primer, the glossary, the lectures

Built 2026-11-21 from the playthroughs (docs/PLAYTHROUGH.md, "A first game": N48, N80). The `?` in the top bar (and the
`?` key) opens one window with three tabs.

- **The primer**: the eight cards it always had.
- **Glossary**: the words the screens use, each with one or two plain sentences and a button to the window where the thing
  is seen. They are grouped (money, standing, the Senate, worlds, war, family, time) and a box finds a word as it is typed:
  a match in a term's name comes before a match in another term's meaning (`AcademyCatalog.Find`). The terms are content:
  the `glossary` list of `content/academy/academy.json` (`id`, `group`, `term`, `means`, `where`). `where` must be a
  window's id; a test holds that.
- **The lectures**: every lecture of the Academy in full, in the register of the game being played
  (`AcademyCatalog.Read`), whether or not the player sat the Academy.

Escape closes the handbook, and while it is open no other key reaches the desk behind it.

A definition says what a thing is and where it is seen. It gives a figure only where the rule's figure is fixed and
already shown elsewhere (three hulls to a helium-3, legitimacy from 0 to 100). When a rule changes, its entry must be
changed with it: nothing checks the sentences against the rules.

## What is remembered

How a cadet did is written on them for life, and the better they did the more it counts (the owner's direction,
2026-11-17; docs/GAPS.md R14). `AcademySystem.Remember` runs at passing out.

| What | Who | Laurels |
|---|---|---|
| Won the Institute as its captain (the year's *primus*) | One person a year: the player if they led the Standard that held the last banner, otherwise the best cadet who began under it | 3 |
| First of the year | Whoever tops the order of merit | 2 |
| Stood in the ranks that won | Everyone who began under the winning Standard. A cadet whose banner was taken does not wear it, though they finished the year in the winner's ranks | 1 |
| Passed out with honours | The top third | 1 |

Winning and merit add: a captain who is also first of the year has five. Classmates are placed and marked exactly as
the player is. A cadet in the bottom third is remembered for that too.

**What reads it.**
- Renown: three for each laurel, for anyone, whether or not their life is written down. Renown already counts in
  elections, with a family weighing a suitor, and with people deciding whether to join a household.
- An election on the ladder of offices: one more for each laurel, directly.
- The first posting: the year's captain is commissioned a rung above the rest of the year. Anyone with two laurels or
  more is promoted a rung ahead of their age for the rest of their career.
- A fleet: crews are steadier under a captain of the Institute, and such a commander leads one rank's allowance of
  hulls more than their rank gives.
- The year itself: classmates who won under you think better of you (more if you led them), those whose banner your
  Standard took think worse (more if you led it), and the cadet you beat to first place does not forget.
- Six later events (`content/events/academy_remembered.json`): the Institute sends for its first; the cadet whose
  banner you took; one who stood in the ranks with you; the one who came second; the year's captain calling on the
  year, when it was not you; and a senior who has looked up where you passed out.

**It is a contest now.** Measuring this found the Institute was not one: the player's Standard won in 30 years of 30,
the player was first of the year in 30 of 30, and every year was the same year (the classmates' scores and the rival
Standards' strength did not change from game to game). Each year now takes its dice from the world, each rival
Standard has a captain of its own worth nothing, a little or a good deal, and classmates score more. Over 30 years:

| How the cadet answers | Won as captain | First of the year | Honours | Near the bottom | Own banner taken |
|---|---|---|---|---|---|
| Boldly (the first choice) | 10 | 13 | 22 | 0 | 9 |
| By guile (the middle choice) | 14 | 0 | 4 | 15 | 5 |
| Carelessly (the last choice) | 0 | 0 | 0 | 30 | 14 |

The bold cadet gathers standing for themselves; the one who works by guile gathers strength for the Standard. In the
Republic's Academy, where there is no banner, the bold cadet is first of the year in 15 years of 30.

## Files
`src/Core/State/AcademyState.cs`, `Systems/AcademySystem.cs`, `Commands/AcademyCommands.cs`, `Data/AcademyCatalog.cs`,
`Presentation/AcademyProjection.cs`; `content/academy/academy.json`, `content/events/academy_republic.json`,
`academy_institute.json`, `academy_crucible.json` (re-gated to the Institute) and their texts; `tests/AcademyTests.cs`;
`src/Web/Components/AcademyScreen.razor` and the Academy shell in `Pages/Index.razor`.
The first years and the report cards: `src/Core/State/FirstSteps.cs`, `Systems/FirstStepsSystem.cs`, `Data/FirstYearsCatalog.cs`,
`Presentation/FirstStepsProjection.cs`, `AcademyProjection.Card`; `content/academy/first_years.json`; `tests/FirstStepsTests.cs`;
`src/Web/Components/FirstYears.razor` (in the outliner), `TermCard.razor`, the primer's pages in `Primer.razor`, `wwwroot/js/guide.js`,
`wwwroot/css/win/firstyears.css`; `tools/browser/firststeps.js`.

## Not yet
- Only the player's own year is remembered. Other people's children do not go through an Institute year of their
  own, so a rival of forty has no laurels (E1).
- Nothing under a throne yet offers the year's captain a place in a great household or a command (R1).
- When the Republic falls in play, the next generation's heir does not yet go to the Institute: heirs still begin in
  the world. The Academy runs only at the start of a run.
- Five Institute chain events (the oath, the blooding, the humiliated returns, the marked man, the frame unravels) need
  more than four terms to line up and were not reached in the soak.
- Character creation's choice of service is overwritten at passing out by what the cadet read or asked for.
- The other starts begin in the world with older characters: they have the primer, now pointing at the screen, and the first years' list,
  but no Academy.
- The first years' list teaches the first windows only. Nothing yet walks a player through a war, a marriage or a succession the first
  time each comes (the moments the review asks for, docs/STEAM_REVIEW.md 4), and the steps do not change once the list has begun.
