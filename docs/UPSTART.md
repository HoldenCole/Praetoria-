# A new house, from nothing

What the owner asked (2026-11-21): *"Let's take a look at house customization and if you are starting a new character
from scratch. How many things you were able to customize such as background, other living house members, etc. What a
rags to riches story looks like of a brand new upstart house."*

This file answers both: what the creator lets you set, and what happens when you set all of it to its least and play.
The second half is measured, not imagined: `dotnet run --project src/Tools -- upstart --seeds 4 --turns 60` plays a
landless, seatless, friendless house in each of the three services and prints what is below.

## 1. What you can set

The creator is the *A Character Of Your Own* start on the title screen (docs/CHARACTER.md has the prices and caps).

| | You can set | You cannot |
|---|---|---|
| **The person** | name, sex, age 16 to 80, service, rank in it, ambition, up to 2 natures, up to 2 aptitudes, 13 skills, **a background (8)** | health, an old wound, renown or deeds already done, a personal purse |
| **The face** | ancestry of the house, a painted face, skin, hair, cut, beard, a scar | dress |
| **The house** | name, colour, culture, old or new name, rank, legitimacy, party, eight goods in the treasury, a seat by the census, a patron | its laws, a debt, an heirloom, a feud, a motto |
| **What it holds** | worlds (on a map), hulls, ventures and where | buildings on the world, stations alone, a province under another house |
| **The family** | spouse and the house they come from, up to 3 children, up to 3 brothers and sisters, **a living father and mother**; for each of age **a service, a step in it, a nature and a gift** | uncles, aunts, cousins, grandparents; a relative's skills, ambition or face; how they feel about you; a mentor, steward, ward or companion |
| **The world** | Republic or Imperium, begin at the age chosen or at sixteen in the Academy, one friend and one rival among the houses | more than one friend or rival, a house you owe, a house that owes you |

Bold is new with this piece. Before it, a relative was a name, a sex and an age, parents were always dead, and the
backgrounds existed only for the written starts.

A standard start has 60 points. Everything at its least costs 8. The **Upstart** preset (a clerk's child, twenty-two,
no land, no seat, no patron, 2,000 credits, a mother who is still a clerk and a sister of nineteen) costs 19.

## 2. What was wrong, and is mended

Played from nothing, the first forty years were flat. Three rules did it, and none of them was meant.

| What happened | Why | What it is now |
|---|---|---|
| The founder was a cadet, clerk or aspirant for life, and so could never stand for any office (the lowest asks one step in a service). In the middle game he was still at the bottom at **year 47**, when he died. | Everybody else in a service goes up a step every six to nine years whatever they do. The player's own person was the one exception: only an event or a senior's word moved them, and every one of the events that promotes asks for a step already. | One who does the year's work at a junior step goes up after **four** years of it (a year of padded accounts does not count), never faster than the service allows anybody. A senior's word is still quicker. |
| "Tam Orrin has not been seen in the Senate for 3 years. It is remarked on, and the house is thought the less of." Legitimacy fell by 2 every three years for a house that had no seat to be absent from. | The head of every house in the Reach was given a senator's year, seat or no seat. | A house that has not come in by one of the four doors (land, the bench, the sword, the purse) has **no senator's year**, owes no attendance and has no place on a committee. It still votes. Its head's year is the work of their service. |
| Asking for the next rank was refused 700 times with no reason given. | The command had no reason to give; the screen worked it out separately. | The command says why: "needs legitimacy 10: the house has 7", or the land the rank wants. |
| "Orrin Orrin", head of House Orrin. | A child could be given the family's own surname for a first name. | Nobody of a house is named for it. |

The test player was also made to behave as a careful person would: it reads what a venture would pay before founding
one, sells what its ventures bring up, and does not spend its last thousand credits on another house's election. With the
first version (take whatever is offered first) every venture it founded was wound up for earning nothing within three
years. That was the script. What is left in section 4 is the game.

## 3. The story, as it now plays

Twelve games: three services, four seeds, sixty years, the same start (landless, a new name, legitimacy 5, 2,000 credits,
6 materials, 4 manpower, one skill at 2, nobody).

| | Before | Now |
|---|---|---|
| An income of its own | 12 of 12, year 2 (and wound up by year 5) | 12 of 12, year 3 |
| A patron | 12, year 10 | 12, year 9 |
| Married / a child | year 7 / year 13 | year 9 / year 11 |
| The founder's first step in a service | never, in the middle game | **year 5** |
| One of the house elected to an office | 8 of 12, year 40 | **12 of 12, year 10** |
| The first rank (Warden) | 12, year 41 | **12, year 11** |
| A seat in the Senate | 8 of 12, year 53 | **12 of 12, year 29** |
| The second rank (Prefect) | 7 of 12, year 53 | **12 of 12, year 29** |
| A world of its own | 4 of 12, year 56 | **9 of 12, year 32** |
| The third rank (Legate) | 3 of 12, year 57 | 6 of 12, year 46 |
| The seat passes to an heir | year 42 | year 42 |
| Among the richer half of the houses | 0 of 12 | 1 of 12 |

The middle game, year by year:

| Year | Legitimacy | Credits in hand | The house's rank | The founder's step |
|---|---|---|---|---|
| 1 | 5 | 1,000 | Citizen | 0 |
| 5 | 7 | 700 | Citizen | 1 |
| 10 | 9 | 800 | Citizen | 3 |
| 15 | 16 | 1,000 | Warden | 4 |
| 20 | 18 | 800 | Warden | 4 |
| 30 | 23 | 2,700 | Prefect | 4 |
| 40 | 40 | 15,400 | Prefect | (dead or retired in most) |
| 50 | 55 | 37,600 | Prefect | |
| 60 | 76 | 65,000 | Legate | |

One of them, a lawyer (law, seed 1), as its own chronicle has it: a shipping line at Lanternfall in year 3; four years
of other people's briefs and a step up in year 5 ("it is the years that did it, and nobody's word"); his name down for
quaestor in year 6; a seat by the census in year 7; under the protection of House Teague in year 8; elected quaestor
in year 10, second of four, and married the same year to Galla Graves, "a match the patron had arranged over a single
dinner"; Warden in year 14; dead in year 29; his heir a Prefect with a world in year 31 and a Legate in year 33.

So the shape is: **ten years to be somebody, thirty to sit, and the founder's child, not the founder, holds the first
world.** That is roughly Cicero's father to Cicero, and slower than Marius.

## 4. What still stands in the way

Measured in the same twelve games. None of this is mended, because each is a choice about the game and not a fault.

1. **It never gets rich by the Reach's measure.** At year 60 the middle house has 65,000 credits in hand and five
   ventures, and is 30th of 32 in wealth. The old houses have had sixty years of worlds. One game of twelve reached
   the richer half. If "riches" in rags to riches means overtaking the old names, the game does not yet allow it in
   three generations.
2. **Money comes late.** Under 3,000 credits in hand for the first thirty years. What a first venture pays a house with
   no world, on the best world for it: a salvage guild 900 a year (costs 800 and 2 materials), a skimmer fleet up to 1,700
   where there is helium-3, a mining concern 600, a shipping line 600, a trading company 200. A commodity warehouse and a
   shipyard company pay nothing or less on every one of 42 worlds in an ordinary year: they earn only in a price spike
   or a war, and are wound up after three years without one.
3. **Land is the last thing to come.** "The Senate will not entrust a world to a house of so little standing" was the
   answer in 312 house-years. Nine of twelve got a world, in the middle game in year 32. Every belt was claimed out in
   636 house-years: a new house cannot stake a claim anywhere.
4. **It is given a province on the first day.** Every landless house is, when the provinces are laid out: the house
   that has nothing begins holding a third of somebody's world, with a levy of one hull. It is why the chronicle of a
   house with nothing speaks of "the house's worlds" and sets a tithe in year 2.
5. **A patron takes nine years to find.** "Its head does not think well enough of you" until a marriage or an office
   changes that. The creator can name one for 3 points.
6. **Some of what the chronicle says was written for a great house.** In year 1 a landless clerk "weathered it the way
   houses weather everything: by being older than the panic". A seatless house "crosses the floor" and "takes its seat
   with The Sword". A handful of event texts assume worlds, a seat or an old name.
7. **One founder in twelve was still at the bottom of the service at year 20.** Not yet traced.

## 5. What needs the owner's answer

- **What should "no seat" forbid?** Now: no motions, no standing for consul, no senator's year, no committee. It still
  votes, still has a bench and a party, still can be bought. Should a house with no seat vote at all?
- **Should a house that begins with nothing hold a province?** It is a home and a small income, and it is also not
  nothing. The alternative is a house that truly lives on its ventures until somebody grants it ground.
- **How rich should a new house be able to get?** If the answer is "as rich as anybody, with luck and sixty years",
  the old houses' wealth has to stop compounding, or trade has to pay more than land for somebody who is good at it.
- **How fast?** A seat in thirty years and a world for the founder's child is slow and true to the history. A game may
  want the founder to see it.
- **Should a background follow you?** Eight are in the creator and nothing in play reads them. The obvious use: the
  old names remember (a Rim-born consul is told so), the clerks or the crews remember too (a Clerk's Child is helped by
  the Administration, one from a soldier's family by the lower deck).
- **More family?** Uncles, cousins and a household on the first day are not in the creator. Neither is a relative who
  dislikes you.
- The numbers that are mine: four years' work for a step; the prices and caps in docs/CHARACTER.md; Clerk's Child
  (legitimacy −4, 400 credits, Administrator, administration and law +1) and Rim-born (legitimacy −6, 4 materials, 2
  manpower, Ambitious, logistics, gunnery and economics +1, 2 strain).

## 6. Checked

Five tests in `tests/CustomCharacterTests.cs` (a background, living parents, each relative's own line, the Upstart
with no senator's year and a reason for the rank it cannot ask for, a step after four years' work), 994 in all. The
creator was driven in a browser: the Upstart preset, each background's line, a living father set to a lieutenant with
a gift, and a saga begun from it with no errors. Pictures: `tools/browser/shots/creator-upstart.png`,
`tools/browser/shots/creator-family.png`.
