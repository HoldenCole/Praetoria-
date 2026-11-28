# The aim on screen, and warnings before ruin

Built 2026-11-28 from the review against Steam (docs/STEAM_REVIEW.md, item 6). The review played ten years of The New Man as
thirty report cards: three lawsuits lost to the same house, the treasury down from 4,700 to 980, still a Citizen, and nothing
on screen to say the run was failing or what to aim for. The ambition and its next step sat inside the You window. This puts
an aim on the main screen for every start, says when the money will run out and why, and gives a house that loses its last
world a report of its own.

## The aim (`Goals.For`, `GoalPlate.razor`)

A plate at the top left of the map, under the top bar (docs/VISUAL_SYSTEM.md 3.11). It shows the start's own goal over its
first ten years, in three or four steps read from the world as it stands; each step says where it stands in figures and has a
window where it is done ("Go there"). Once the goal is reached, or over, the player's own ambition takes its place, with the
progress and the next step the You window already gives it (`OwnAim`). It reads the world and changes nothing; nothing in the
rules waits on it.

| Start | The aim | Steps |
|---|---|---|
| The New Man | Win a seat in the Senate | the house notable in commerce (a standing of 20); 6,000 credits put by for the census; enrolled, and the seat taken (any of the four doors counts) |
| The Old Lion | See the seat pass safely | an heir named; the heir married; the heir given a place (a rank of 3 in a service, or an office); the seat handed on with every world kept. Once the seat has passed the goal is over, and says whether every world was kept |
| The Circling Heir | Hold the house, and rise in the Senate | no grown relative at −25 or colder toward you; an heir named; an office of the Reach |
| The Fallen House | Restore the house | the debts cleared; the house worth 15,000 credits; a second world |
| The Risen Officer | Earn a command and a name | a fleet commanded (or a command by service); a battle won; a rank of 4; a world of the house's own |
| The Imperial Court | Rise at the Emperor's court | the Emperor at 25 or warmer toward you; the house raised a rank above where it began; an office |
| The Academy, The Institute | Make your name in the service | after passing out: a fleet commanded; a battle won; a rank of 2 |
| A character of one's own | the ambition, from the first year | |

The world keeps which start it began from (`World.Start`) and the rank and worlds the player's house began with (the counters
`start_rank` and `start_worlds`), so a saved game knows its aim. A world saved before these were kept shows the ambition.

The Old Lion's own ambition was to see the house rich again, while it is the richest house of the Reach; it is now to rise in
the service and be heard.

## Guided or sandbox (`ClientSettings.Guided`, `Goals.Unguided`)

Added 2026-11-28 at the owner's word: the aim helps a new player who wants to be guided and does not want, or cannot yet handle,
the sandbox, so a player who does want the sandbox can turn it off. The choice is the browser's (the `guided` key of
`praetoria.settings`) and holds for every saga played in it. It can be made in three places:

- **Options**, the "Guidance" row: Guided or Sandbox.
- **The new-saga screen**, the "Guidance" section above the rules of the world. Unlike those rules it can be changed later. A saga
  begun as a sandbox begins without the first steps of a first game, unless it opens at the Academy, whose terms carry them; the
  primer can still pin them.
- **The plate's close**, at its top right: it turns the guidance to Sandbox, and a note says that Guidance in Options brings the
  aim back.

Guided is the default. In a sandbox the plate stands only while there is a warning before ruin, and then shows the warning alone:
"A warning" over "The house's money", a medal with the ember threat in it instead of the laurels, and no close. The first steps of
a first game are not tied to the guidance once a saga is under way: they keep their own close.

## Before ruin (`Goals.Ruin`)

When, at what the year will add or take (the ledger's forecast less what is being built and paid out, the figure the top bar
shows beside the credits), the treasury will be empty within three years, the plate carries a red band: how fast ("At this
rate the treasury runs out within 2 years", or "The treasury will not last the year"), how much more goes out than comes in,
the two largest drains by name, and two ways out that fit them, each a button to its window:

| Drain | Way out |
|---|---|
| the household's keep, wages, stipends | let a retainer go (House, Court) |
| allowances to the family | cut the allowances (House, Purses and the will) |
| tutors | end a tutor's lessons (Dynasty, Upbringing) |
| building, hastening, gangs | pause what is being built (Worlds) |
| fleets, hulls, fuel, crews | lay hulls up (Military) |
| interest, loans, bonds | pay down what is owed (House, Purses and the will) |

Where fewer than two fit, the head's own purse is offered first (if it holds anything), then selling stores on the Exchange,
then borrowing. End Year still asks before a year that would end below nought (docs/PLAYTHROUGH.md 6).

The forecast cannot see what the year's events cost: suits lost, answers that cost, raids. So the treasury at each year's end
is kept three years back (`TreasuryTrend`, world counters `treasury_end:1` to `:3`), and when the forecast gives no warning but
the treasury has fallen at each of the last two years' ends, and would run out within three years at that pace, the band says
so instead ("The treasury has fallen two years running, from 4,700 credits to 3,400 credits to 2,100 credits. At that pace it
runs out within 2 years."), with the same ways out. The New Man's ten years in the review, 4,700 down to 980 mostly by suits
lost, is the case it is for.

## A fallen house (`FallenHouse`, content/events/fallen.json)

At each year's end the player's house holding no world where it held one a year before raises the flag
`the_last_world_lost`, marked as news, so the report The Last World Lost comes in the year after (a set piece, weight 80, five
times likelier while it is news). It says plainly what happened and the roads back the game already has: a command by service
(Military), a marriage into a house with land (Dynasty), a grant of a world petitioned for (Worlds), or the money to buy one
(House, Purses and the will). The answers are how the head takes it: swear the house will hold land again (Strain +4,
legitimacy +2), say nothing and work, or tell the household a house is its people (Strain −4, legitimacy −3). The flag comes
down at the end of the year after.

## Tests

`tests/GoalTests.cs`: every written start has a goal with steps in its first year; The New Man's goal gives way to his
ambition once the house is seated; the Old Lion's goal is over once the seat passes and says whether every world was kept; a
world saved before the start was kept shows the ambition; no aim at the Academy; the warning's figures, words and ways out;
no warning while the treasury lasts three years or grows; a treasury falling two years running warns at its own pace, and the
yearly record keeps three years; the fallen-house flag is raised for the year after and only then,
and the report is eligible while it stands; a sandbox is shown no aim, and the warning alone while there is one.
`tools/browser/aim.js` checks the plate, its close, Options and the new-saga screen in a browser.

## Not done

- No reward when a goal is reached beyond the plate turning to the ambition and saying it was done.
- No dynasty score charted against the other houses, and no optional end year with a final ranking.
- The warning does not name what drained the treasury in the years behind (the Chronicle has each year's), and it does not see
  a war's cost coming or a loan falling due.
