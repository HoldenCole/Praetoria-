# Full QA, 2026-11-16

A pass over the whole game to find what is broken, what is thin and what could be better. It follows ten gap rows
closed in two days (A6 to A9, and W1 to W13), so part of its job was to check that work.

**The short of it.** Nothing is broken that a player would hit: 744 tests pass three times running, the soak and
both content checks pass, every screen loads without an error on a desktop and a phone, a saved game comes back as it
was left. What the pass found is a different kind of problem. **A great deal has been built that play does not
reach.** A third of the events never fire, a third of the commands are never used by the test player, and eighteen
counters for recent features read zero in every unattended game. The rule "a row is closed when it is built, tested
and measured in play" has been met to the letter with measurements of nought.

## What was run

| Check | Result |
|---|---|
| Test suite, three times | 744 passed each time; no flaky test |
| Build | 0 errors; 5 analyser warnings (now fixed) |
| Soak: 33 games of 60 years, a random player, a save and load in each | Passes, no drift. 250 of 382 events fired |
| Probe: 60 unattended games of 60 years, six Republic starts | The Republic stood in all 60 |
| Survival: 80 unattended games, eight starts | No line ended |
| Browser, desktop: 12 screens and 11 tabs, twelve years in, two starts | No console errors; every alert opens a screen |
| Browser, phone width: nine screens | One screen scrolled sideways (fixed) |
| Browser, save and reload | The game came back as it was left |
| Browser, speed | 2.7 s to the title, 5.5 s to the first turn, 0.6 to 1.3 s a turn, 4.7 s at worst |
| One game on the command line | 13 s for 60 years |
| Code and docs | See "Code and docs" below |

## Fixed in this pass

Small, and not matters of opinion.

1. **The Dynasty screen scrolled sideways by 142 px on a phone.** The prospects table was held to a minimum width
   outside a scrolling panel. It now scrolls inside itself.
2. **A raid was remembered for ever.** `raided` was set when raiders plundered the player's world and never cleared,
   so the event for it could repeat every year afterwards. It is now a year's news, with a test.
3. **Five analyser warnings** in three test files. The build is clean.
4. **The README was wrong in a dozen places**: six regions and eighteen worlds (ten and thirty-one), 608 and 243
   tests (744), 261 events (382), "a magistrate has no work" (closed two days ago), four bullets spliced into the
   middle of a sentence, and a claim that the Godot layer "renders all of it".
5. **The roadmap** said "as of 13 November", twelve Senate motions (fifteen), and three things that are closed.
6. **The probe printed rows of zeros for the two throne-era starts** as though it had measured them. It skips them,
   and now says so.
7. **W12 was missing** from the register's list of rows waiting on the owner.

## 1. Built, and not reached

This is the main finding. Three measurements agree.

**The unattended world never produces these.** Zero in all sixty probe runs:

| Feature | Closed as |
|---|---|
| A tied vote settled by the chair's casting vote | O2 |
| A censure moved by a stirred house; a war begun over what was said | B12, B15 |
| A house leaving a breakaway state; a second great war | B11, B10 |
| A founder's stake paid; a founder's part of a sale; a second start | W1, W10, A7 |
| A venture settled by the head | W10 |
| A company carrying a failing venture | A7 |
| A world taken for a debt | A9 |
| A thing handed down sold; a standard sent home for nothing | W8, W9 |
| A broken house's signet surrendered | W11 |
| A favour forgotten uncalled | W13 |
| The Republic falling; a general broken; a throne taken | the Caesar road |

Some of these need a player and could never appear in a probe that gives no orders. That is the second measurement.

**The test player cannot do them either.** Of 94 commands the soak's random player used 62. Twenty-one are offered
to it and never once succeed: Caesar, Secession, SeizeThrone, ProclaimImperium, Veto, Chair, Canvass, FoundFaction,
ProposeMarriage, SettleVenture, SellShares, Mentor, Enroll, MergeFleets, SetLoadout, RushProject,
AcceptVentureOffer, RecallExpedition, ReclaimFreehold, SteadyMarket, ThroneExchange. Eleven it is never offered at
all, among them Assassinate, Foster, SeekAlly, Recall and SetSuccessionLaw. A random hand does not marry, secede or
march, because each needs several right moves in a row.

**So whole chains of content never run.** 132 of 382 events did not fire in 33 games:

| File | Never fired | File | Never fired |
|---|---|---|---|
| breakaway | 15 of 18 | academy_crucible | 9 of 18 |
| caesar | 14 of 15 | exchange | 8 of 13 |
| orders | 12 of 18 | ladder, nations, military, echoes | 6 each |
| wealth | 10 of 17 | situations | 5 of 26 |
| rings | 10 of 18 | the rest | 21 |

No flag is dead: every one of the 90 flags those events wait on is set somewhere in the code. They wait on a player
who secedes, is voted a triumph, takes a company, holds the Speaker's chair.

**Twenty-six are a different case and want looking at.** Their flag was raised in some run and they still never
fired. Seven have a weight of 35 to 40, which should have won a draw: `the_wound`, `the_heir_is_named`,
`far_ahead_of_their_years`, `one_of_yours_is_removed`, `the_accounts_do_not_add_up`, `two_of_yours_want_one_thing`,
`the_elder_makes_peace`. A role that does not bind is the likeliest cause. Not run down.

**What to do about it.** The tests prove each of these works when set up by hand. What is missing is a player that
plays toward an end. Three scripted players would cover most of the list: one that climbs to a command and marches,
one that works a region and secedes, one that marries, holds offices and trades. Until then, "the soak passes" says
nothing about a third of the game.

## 2. The Caesar road is absent, not rare

The owner's rule is that an AI general takes a throne no more than once in ten games. The measurement is none in
sixty, which keeps the rule. But the gates read: a general "was loved" in 48 runs of 60, "was of the first rank" in
11, was both in 1, would march in 1. One civil war began, in one start, and the probe records no ending for it.

The first rank is the gate that shuts. This is row C9, which the owner set aside on 15 November with the question
"should the gate ask less of other houses' generals?". These are the numbers for that question. It is still the
owner's.

## 3. The world runs on rails in places

- **The great war keeps a clock.** All twelve wars that ended in exhaustion lasted exactly 16 years. Sixteen of the
  eighteen that ended in partition or a crushing lasted exactly 10. No finished war was shorter than ten years.
- **It is decided by the start.** One start fights it in ten runs of ten, always beginning between years 28 and 33.
  Two starts fight it in four of ten.
- **Nothing shrinks.** 7,152 ventures stood at the end against 511 wound up. 2,988 shares were bought in a person's
  own name and 68 sold. A venture loses money in 1 to 3 years in a hundred.
- **A passive player cannot lose.** No line ended in 80 unattended games. A random player's line ended in 1 to 7 of
  33 games across the last nine soaks, always between years 34 and 57 and mostly in starts that open with one
  person. See "What I got wrong" below.
- **Trust in the magistrates is a problem in two starts only**: low in nine runs of ten in one, five in another,
  and never in the other four.
- **The Academy start's private economy is a tenth of the others**: 101 shares bought against 805, 10,000 credits paid
  into purses against 231,800.
- **Recalls almost never pass**: 5 voted of 38 moved.

## 4. Screens

- **The purses tab has become the cupboard.** Nine passes added to it. It now holds ten sections, 627 words and 38
  buttons in one panel, and it explains itself in paragraphs where the other dashboards use a tooltip. It has no
  headline or tiles of its own. It should be three tabs (purse and shares; things; the will and the estate) or
  folded into the dashboards' pattern.
- **Under a throne three tabs are more than half empty**: the ladder and the courts, the general and the Senate,
  and projects.
- **Weight.** 22 MB to download, 8.5 of it WebAssembly. Not measured on a slow connection.
- **Never checked:** keyboard use, contrast, a screen reader. No pass has looked.
- **Not seen in a browser, from the last ten passes:** a second start, the name for bad business, a standard sent
  home or bought back, a thing carried off, an orrery arriving, a foreclosure.

## 5. The second client

The Godot layer compiles against the current engine and has never been run. It uses 21 of the engine's 94 commands
against the browser client's 89. It has no screen for the Senate, the ladder, the Exchange's ventures and shares,
purses, ambitions, a life, the household, courtship, breaking away or the Caesar road. Its last real change was on
7 November, sixty commits ago. It is roughly the game of nine days ago, in a client nobody has opened.

This is the owner's to decide: keep it and bring it level (weeks), freeze it and say so, or retire it. The README
no longer says it renders the whole game.

## 6. Code and docs

- **Lines.** 16.6% of the engine's lines carry two or more statements, and 26% of the tests'. 391 lines in `src`
  pass 300 characters. The worst is mine from these passes: `src/Tools/Probes.cs` line 122 is 3,940 characters and
  66 statements, because each pass appended its counters to it.
- **Methods.** `Qa.Act` is 266 lines; `CourtProjection.BuildNow` 273; `LadderSystem.DecideDutyIt` 225.
- **Tests.** `tests/RegisterTests.cs` is 1,087 lines and `CloseAndDeepenTests.cs` 1,031. Both are named for a
  worklist, not for what they test, and every pass since has added to them.
- **No TODO, FIXME or unimplemented stub** anywhere in `src` or `tests`.
- **Every event has its text** and every text its event: 382 and 382, no orphans, no duplicate ids. 33 choices do
  nothing but write a line in the chronicle.
- **CI** runs the build, both content checks, the tests, the Godot compile and the web publish. It runs none of the
  17 browser scripts, the soak, the probe or `gaps.sh`, so the counts in the roadmap are kept by hand.
- **Releases.** The changelog's "Unreleased" section is about 820 lines over a 0.8.0-alpha entry that is itself
  marked unreleased. The web project says 0.8.0 and the others 0.8.0-alpha. The licence is "TBD" and there is no
  licence file.

## 7. The tools hide things

- **The soak does not say why a line ended.** It names the run and the year.
- **Its list of oddities shows the first forty**, and one run's forty "negative treasury" lines filled it. A house
  below nought is a state the game means to allow, so the list was all noise and anything else was pushed out.
- **The probe is blind to both throne-era starts.**
- **Its percentages are cut, not rounded**: 3.98% prints as 3%.
- **Two of its figures do not add up** and were not run down: one start shows "states that broke away 5, recognised
  3, brought back 3"; and the fates of standards (retaken, bought home, still flown) come to more than the standards
  taken in five starts of six, because things recovered when a world is stormed are counted as retaken.

## What I got wrong

- **I watched "the soak passes" and not what it printed beside it.** A random player's line ended in 3, 1, 4, 3 and
  4 of 33 games across five soaks, then 7, 7, 6 and 5 after the foreclosure pass. I reported a pass each time. I ran
  the soak again in this pass with foreclosure switched off: 5 lines still ended. So foreclosure is not the cause
  and the rise may be chance, since every build moves which runs end. I do not know the cause, and the tool cannot
  tell me.
- **I closed rows on measurements of zero.** Each time I wrote that the thing was "not seen in play" and rested on a
  test. That was true and I said it, but ten passes of it is the list in section 1.
- **I made two files worse.** The probe's long line and the worklist test file are both my appending.

## For the owner

Twelve rows in the register wait on an answer. None is new except the last two.

| Row | The question |
|---|---|
| C9 | Should the gate ask less of other houses' generals? Section 2 has the numbers: one general in sixty passes it. |
| C6 | May a relative end the Republic against the head's wishes? |
| M4 | Does a spouse come to live in the house, and which of the two moves? |
| W12 | Is a world ever a person's rather than the house's, and what becomes of it when they die or leave? |
| B13 | A marriage or an office as the price of a region; waits on the forms of government. |
| G1 to G7 | The forms of government: planned, and held until told to build. |
| Q8 (new) | The Godot client: bring it level, freeze it, or retire it? |
| Q1 (new) | Is "none in sixty" what was meant by "no more than one in ten"? |

## What I would do next, in order

1. **A player that plays toward an end** (Q2). Three scripts: the general, the seceder, the courtier. This turns
   section 1 from a list of unknowns into a list of results, and is the only way to know whether a third of the game
   works in play.
2. **Run down the 26 events that should have fired** (Q3). Possibly real defects, and cheap to check.
3. **Make the soak say why a line ended** and stop its oddity list filling with noise (Q9). An hour, and it would
   have answered a question in this pass.
4. **Split the purses tab** (Q7).
5. **Decide the Godot client** (Q8). Everything else about it follows from the answer.
6. **Loosen the great war's clock** (Q5), once the owner has said whether fixed lengths were meant.

## Players with an end in view (Q2, built the same day)

The first thing on the list above is built: `src/Tools/Scripted.cs`, run with `praetoria roads`.

**How it works.** Each of three players has a road: an ordered list of the steps it wants. Every turn it is shown
every command the soak could offer, takes the wanted ones in order, then does one ordinary thing so that the house
is kept as a house. When a wanted step is refused it writes down the reason, in the game's own words. It never gives
a command that would take it off its road (the general does not sue for peace or obey a recall; the courtier does not
go to war).

| Player | Its road |
|---|---|
| The general | Command a fleet, win a name with it, be wronged by the Senate, march |
| The seceder | Work a region, take it out of the Reach, keep it |
| The courtier | Marry, hold office, lead a bench, make the house's money work |

**How far each got**, over 24 games (eight starts, three seeds, sixty years):

| The general | Games | The seceder | Games | The courtier | Games |
|---|---|---|---|---|---|
| Commands a fleet | 13 | Holds a world | 21 | The head is wed | 24 |
| Wins a battle | 5 | Declares, or joins a state | 16 | Holds a magistracy | 18 |
| Voted a triumph or a recall | 1 | Fights a war of independence | 16 | Holds an office of the Reach | 17 |
| Crosses the line | 0 | It ends: won, let go or lost | 11 | Sits on a bench | 24 |
| The march ends | 0 | | | A venture is a person's own | 23 |

**What it changes.** In the soak, which now plays each road once on every start beside its random player:

| | Random player alone | With the three roads |
|---|---|---|
| Events fired, of 382 | 250 | 290 |
| Events never fired | 132 | 92 |
| Kinds of command that landed, of 94 | 62 | 71 |
| Flags content waits on that no game raised | 90 | 67 |

Of the 132 events the random player never reached, a road fired 55 across the 72 games: 9 of 15 in breakaway, 5 of
10 in wealth and in rings, 4 of 6 in the ladder. The roads replay identically, and a short run of them is now a step
in CI.

**What the general found.** This is the result that matters, and it is about the game, not the script.

- **A house without land cannot get a fleet.** In 11 of 24 games nobody of the house ever commanded one, for 557
  years of refusal in all. The reasons, in the game's words: "cannot afford it" (ships, 531 years); "needs a world of
  your own" (a venture, 529); "no house has a world to spare" and "the Senate will not entrust a world to a house of
  so little standing" (a world, 700). A new man cannot rise to a command by service. Marius could.
- **A triumph is moved only by rank.** "The Senate hears a Prefect, or a Judge": 582 years.
- **Where there is a fleet, the crews are the gate.** "N hulls would follow, and it takes N": 225 years. Donatives
  cost more than the house had in 168.
- **The one march that happened ended in nothing.** In the first run of the general a march began in year 19 and
  was "swallowed by a larger war" nine years later: one line in the chronicle, no event, no flag, nothing decided.
  That is also the civil war with no recorded ending that the probe showed (Q12).

So the Caesar road is not only shut to other houses' generals (Q1). For half the starts it is shut to the player.

**What the seceder and the courtier found.** Less. A third of the seceder's games never declare: "the capital's own
region cannot leave the Reach", or the houses that would follow fall short of three worlds in five. The courtier is
refused mostly by things working as meant ("already wed", "they have held it").

**Not done.** These three do not cover everything the random player misses. Still never landed by any player:
Assassinate, Foster, SeekAlly, Recall, SetSuccessionLaw, ProclaimImperium, Veto, MergeFleets and about fifteen
others. 92 events still never fire, 12 of the 15 in caesar.json among them. A fourth road (the intriguer) and a
general who starts with land would be the next two.

