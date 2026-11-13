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
- **Either way** the lecture hall shows the orientation, built from the live world by `AcademyProjection`: the state of
  the Reach (who holds the chair or the throne, the parties with their seats and leaders, the consuls and when the next
  election falls, the offices, the law of the Reach, wars, the nations beyond), the seven houses that matter by seats,
  and the cadet's own house. The year's order of merit shows who is in the cohort and what each thinks of you.

The screen has no rail and no world screens. It closes with a passing-out card: place, service, friends made, and
those who will remember you otherwise. A player who took the full course is not shown the old primer.

## Files
`src/Core/State/AcademyState.cs`, `Systems/AcademySystem.cs`, `Commands/AcademyCommands.cs`, `Data/AcademyCatalog.cs`,
`Presentation/AcademyProjection.cs`; `content/academy/academy.json`, `content/events/academy_republic.json`,
`academy_institute.json`, `academy_crucible.json` (re-gated to the Institute) and their texts; `tests/AcademyTests.cs`;
`src/Web/Components/AcademyScreen.razor` and the Academy shell in `Pages/Index.razor`.

## Not yet
- When the Republic falls in play, the next generation's heir does not yet go to the Institute: heirs still begin in
  the world. The Academy runs only at the start of a run.
- Five Institute chain events (the oath, the blooding, the humiliated returns, the marked man, the frame unravels) need
  more than four terms to line up and were not reached in the soak.
- Character creation's choice of service is overwritten at passing out by what the cadet read or asked for.
- The other four scenarios start in the world with older characters and still use the short primer.
