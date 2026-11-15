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

## A battle teaches up to the ceiling

A victory used to add a step of tactics or gunnery whatever the commander's ceiling was, so a much-fought admiral
drifted above what anyone could learn by study. Now a victory teaches the weaker of the two crafts if it is below its
ceiling, the other if not, and nothing once both are there. What this did to the Caesar road's first gate is in
docs/CAESAR.md.

## Asking, not telling

What a grown relative studies is their own. The head may **ask**, once in three years of each of them.

- The loyal will, and so will anyone who thinks well enough of the head: any regard at all for something that suits
  them, 20 for something against their bent, 40 from the wilful. The button says beforehand which it will be.
- One who agrees turns to it and keeps at it until they have gone as far as they can. Against their bent, they think 2
  the less of the head for it.
- One who refuses thinks 3 the less of the head for being asked, and there is an event.

The head may also ask one of the house to **teach another**: someone at least two steps ahead in something the other
can still learn, who does not think ill of the head. The pupil is under their wing from then on and learns a step of
it every third year, until there is nothing left to pass on.

## A price for studying, thanks for teaching, and ceilings that hold

Closed straight off the register on 2026-11-15 (docs/GAPS.md E5, E6).

- **A price.** One who would not study for the asking, but is within 12 of it, will for 4 credits of the head's own;
  within 25, for 8; further off, not for anything. Paid, they turn to it, the money is theirs, and nobody thinks the
  worse of anybody. The button appears beside the subject when asking alone would be refused.
- **Thanks.** The head can thank one of the house who has another of it under their wing: 4 credits of the head's own,
  +8 regard, once in three years. And when a pupil has taken a step from a teacher of the house, an event asks whether
  the head thanks them, pays them, or takes it as their duty.
- **Ceilings.** Nobody sits above their ceiling: what a person has already reached is taken as what was in them, and
  they go no further. A victory beyond the Reach and a duty of the household no longer raise a skill past it.

### A price that is not money (E7, the same day)

The same bargain can be paid in a word said for them in the right place: 1 influence where the sum would have been 4,
2 where it would have been 8. An empty purse is no bar. They turn to it and think 2 better of the head, because it was
something done for them and not something paid.

## Not in this piece

- Nobody but you is sent to the Academy; it is still a year only your own character has.
- No trait is lost, and no nature is formed by an upbringing; only aptitudes can be earned.
- Old age still dulls a skill every fifth year from sixty, as before. Learning does not slow with age.
- The numbers are first guesses. What slower-growing skills do to battles, offices and the Exchange over sixty years
  has been checked only for not breaking anything.
