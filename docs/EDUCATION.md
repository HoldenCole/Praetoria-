# Education, skills and getting better

Built 2026-11-14. From the roadmap's person-and-family group. It follows the rule in docs/PROCESS.md ("One person, not a
house"): nobody has to be managed for any of this to happen.

## What was wrong

- Your own skills moved only by events, the Academy year, or (lately) a mentor.
- Everyone else drilled their best skill **one step a year with no ceiling**. In ten years a clerk was a master twice
  over, and a house full of able people cost nothing.
- A child arrived at sixteen with a 1 in two skills, whatever their childhood had been.

## How it works now

**Every person has a ceiling in every skill**, from 3 to 10. It is five, plus a bent of their own for that skill (−1 to
+2, fixed for life), plus three for an aptitude that bears on it, plus one for the Brilliant.

**Skills are learned, not bought.** Learning is banked in points toward the next step, and each step costs more:

| From level | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 |
|---|---|---|---|---|---|---|---|---|---|---|
| Points to the next | 4 | 7 | 10 | 13 | 16 | 19 | 22 | 25 | 28 | 31 |

Nothing is learned past the ceiling.

**Where the points come from, each year, for a grown person:**

| Source | Points | In what |
|---|---|---|
| Their service | 2 | One of its skills in turn (the Navy: tactics, discipline, gunnery, leadership, logistics; stewardship: administration, economics, engineering; the law: law, oratory, diplomacy) |
| Commanding a fleet | 2 | Tactics |
| Governing a world | 2 | Administration |
| Holding an office of state | 2 | Oratory |
| The steward's or marshal's duty | 2 | Economics, or logistics |
| Leading a house | 1 | Diplomacy |
| Study | 3 | Whatever they have chosen to study |
| A paid teacher | +1 or +2 | Added to their study |
| A mentor | A whole step every third year | The mentor's best skill, while the pupil is behind them |

So someone who serves, studies and holds a post gains five to seven points a year. From nothing that is "able" (3) in
about four years, "skilled" (5) in about ten, and an "expert" (7) in twenty or more, if their ceiling allows.

**What they study is theirs to choose.** Other people pick by their ambition (a command wants tactics, the Senate
oratory, a fortune economics), or else whatever of their service has most room left. You choose only your own, on the
character sheet. You can also give a season to nothing else: one agent for four points.

**An aptitude can be earned.** Reaching 7 in a skill reveals the aptitude that goes with it, which lifts the ceiling by
three. Somebody with a strong bent and decades of work becomes a Tactician; they were not born one.

## Children

From six, a child has lessons every year without anyone arranging it.

- **Their bent.** Each child is made for one of three schools: arms, the forum, or the counting-house. It is where
  their ceilings stand highest, and the Dynasty screen says which.
- **By default they follow it:** three points a year, two years in three on the skill they are best made for and the
  third on the next. At sixteen they are properly good at one thing (about 3) and have met another.
- **You may set a different course.** Against their bent they learn one point a year, think a little less of you every
  year, and an event comes of it.
- **A tutor** adds one or two points a year (2 or 5 credits). A house that cannot pay loses the tutor.
- **At sixteen** what they were brought up to decides their service, ahead of what their parents did, and what they
  learned is kept.

Wards are brought up the same way, and learn from you as their mentor besides.

## Where it is

- **Character sheet:** a Learning panel. Every skill as a bar to ten with your ceiling marked, what is feeding each this
  year, what you study, the hard-study button, and a teacher of your own.
- **Dynasty screen:** an Upbringing and learning panel. Each child's bent, course, tutor and what they have learned;
  each grown member's chosen study, with a teacher you can pay for.

## Events

Three, in `content/events/education.json`: not made for it (a child set against their bent); far ahead of their years
(a child who reaches 4 before sixteen); the work of years (you reach 5, 7 or 9).

## For content

- Effect: `setUpbringing` (`role`, `course`: arms, forum, counting, or empty).
- Flags: `child_resists_lessons` (each year); `child_prodigy`, `mastery_gained`, `tutor_dismissed` (deeds).
  Character flags `resists_lessons`, `prodigy`.
- `adjustSkill` in an event still moves a skill directly, and may pass a ceiling. Events are the exception.

## How it is built

- `Character.Learning`, `Study`, `Upbringing`, `Tutor`. `EducationSystem` is static and uses no dice.
- `TrainCommand` now adds four points and is refused at the ceiling. Stewards no longer use it at all.
- A mentor's step goes through the same ceiling.

## Checked

Seven tests. The soak passes on all eleven starts. The survival probe: lines ended in 3 of 80 runs, against 4 of 80
before. In the browser on *The Old Lion*: a study chosen and studied hard; over six years a child following a bent for
arms reached tactics 2 and leadership 1, and one set against her bent reached a single step.

## Not in this piece

- Nobody but you is sent to the Academy; it is still a year only your own character has.
- You cannot ask a grown relative to study something, or set one of the house to teach another. You can only pay.
- No trait is lost, and no nature is formed by an upbringing; only aptitudes can be earned.
- Old age still dulls a skill every fifth year from sixty, as before. Learning does not slow with age.
- A battle still adds a step to its commander directly, without regard to the ceiling.
- Authored characters keep the skills they were written with, even where those sit above their ceiling.
- The numbers are first guesses. What slower-growing skills do to battles, offices and the Exchange over sixty years
  has been checked only for not breaking anything.
