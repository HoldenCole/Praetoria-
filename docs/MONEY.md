# Money

What the owner said (2026-11-22): *"We need to scale credits better. If you're paying a commoner for a position 1-3 for a
position, but your house total is like 24 that math doesn't make sense. Not saying pay them less, but the costs and
economy for credits needs to be rescaled."*

## What changed

**A house keeps its books in hundreds of credits.** A treasury that read 28 reads **2,800**. A hull that cost 12 costs
1,200. A purse of 6 is 600. A house that took in 8 a year takes in 800. A servant of the house's court is still paid
**1 to 3 credits a year**, so a court of seven costs about 14 credits a year against a great house's thousands, and
weighs on a house that clears fifty.

Every amount on every screen, in every report and in every line of the chronicle is 100 times what it was, except
what is truly small.

## How it is done, and why this way

There were two ways to do it.

1. **Multiply every number in the rules by 100**: about 1,100 lines of rules, 900 numbers in the content and 700 lines
   of tests. The economy is reckoned in small whole numbers that round down (a yield of 3 at 85% is 2). At a hundred
   times the size the same sum gives 255, not 200: every house would have been richer by a different amount, and the
   balance tuned over the last months would have moved everywhere at once.
2. **Leave every rule as it is and change what a unit means.** This is what was done.

So in the rules, the content files and the tests, `credits` is a count of **hundreds**. No rule, cost, yield, price or
threshold changed. What a player reads is written by one helper, `Money` (`src/Core/Presentation/Money.cs`), which
shows the books' 28 as "2,800". A test reads the source and fails if a line writes an amount of credits without it.

Below a hundred there are **single credits**: `Resources.Coin`, from 0 to 99 in a treasury. A hundred is broken when
the coin runs out and made when it fills, so the treasury on screen is exact to the credit (2,794 after the court is
paid). Single credits are used only for what is truly small:

| Paid in single credits | Amount |
|---|---|
| A year's wage of one of the commons in the house's court | 1 to 3 (docs/COURT.md) |
| What the scenes of the court cost: a purse for the physician, a year's wage over again for the guard, a pension set aside | 2 to 40 |

A scene pays single credits with `adjustResource` on the resource `coin`.

## What this means when reading the files

| Where | A number of credits is |
|---|---|
| The screens, the reports, the chronicle, tool output | credits, as written |
| `content/**/*.json` (`"credits": 3`, a `delta` on credits) | hundreds: 3 is 300 credits |
| The rules (`Treasury.Credits`, `Purse`, `Debt`, `Wealth`, a cost, a price) | hundreds |
| `Treasury.Coin`, `Retainer.Wage`, an effect on `coin` | single credits |
| The docs written before this page | were in the old figures; the amounts in them have been multiplied through, and a figure given "in the books" is in hundreds |

## Settings: how easy money is, and boosts

What the owner said (2026-11-23): *"I think having settings for how easy it is to make money and even some cheats to
select in the beginning is useful. So you can choose the 'difficulty' of making money and how returns are scaled. Let's
just put our numbers as the default."* And of the four things first built under that name: *"Those aren't cheats. Just
boosts."* They are called boosts. Nothing in the game is called a cheat.

**Our numbers are the default.** The content files are priced so that each thing returns what its kind is meant to
(docs/ECONOMY_REVIEW.md, "Ranged returns"): a market, a port or a power plant 7% a year, farming 8%, finance 9%, a
mine, a trading house or a seat of clout 10%, shipping 12%, the dear goods (helium-3, rare earths, isotopes, exotics)
15%, a salvage guild 20%. A test fails if anything returns more than its kind is meant to.

**Making money** is chosen when a game is begun, on the same screen as the map's size. It scales what everything that
is built costs (buildings, works, great works, infrastructure, ventures and their enlargement), and so what it
returns. It is every house's, not only yours. Hulls are not scaled.

| Setting | A thing returns | An ordinary building pays back in |
|---|---|---|
| Lean | seven tenths of what its kind is meant to | fourteen years and more |
| **Standard** (the default) | what its kind is meant to | ten to fourteen years |
| Generous | half as much again | about seven years |
| Lavish | two and a half times | about four years |

Measured over 30 years, every house, two seeds, nobody steering the player's house (`dotnet run --project src/Tools --
econ --seeds 2`); the middle house, in credits:

| Setting | Takes in a year at year 30 | Treasury at year 30 | Buildings standing at year 10 / 30 | Venture levels at year 10 / 30 |
|---|---|---|---|---|
| Lean | 1,400 | 1,900 | 42 / 101 | 9 / 79 |
| Standard | 3,100 | 2,100 | 62 / 108 | 11 / 134 |
| Generous | 4,000 | 6,800 | 67 / 120 | 23 / 177 |
| Lavish | 5,800 | 33,600 | 84 / 143 | 43 / 236 |

Lavish is about what the game was before the prices were changed: money piles up and stops mattering.

**Boosts** are ticked on the same screen, are your house's alone, and are given once when the game begins. None is on
unless it is ticked.

| Boost | What it gives |
|---|---|
| A full treasury | 50,000 credits more |
| Full stores | 200 materials, 100 manpower, and 5 each of helium-3, rare earths, isotopes and exotics (few enough that the game does not open with a good cornered) |
| A second income | every year, again all the credits the house took in |
| A deep purse | 10,000 credits in your own purse |

A game begun with a boost, or at a setting other than Standard, says so: a line in the chronicle, and a line at the
head of the House window for as long as the game is played. Neither can be changed in a game.

The figures (the four settings, what each boost gives) are mine.

## What I chose

- **The scale is 100.** Ten would have left a tutor at a hundredth of a great house's whole treasury a year. A thousand
  made every figure five digits.
- **Only the house's court is paid in single credits.** A relative who holds a post of the council is still paid in
  the books' units (200 credits a year), a physician sent for from a purse still costs what it did (now shown ×100),
  and so do tutors paid for one child, bribes, gifts and dowries. Those are what people of rank pay each other.
- **House-level amounts still move in whole hundreds.** A yield is 300 or 400, never 350, because the rules were not
  rewritten. See M1 in docs/GAPS.md.
