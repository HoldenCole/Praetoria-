# The house's court

What the owner asked (2026-11-22): *"Let's do small roles like tutor, wet nurse, physician, bodyguards, duel master,
shipwright, master of ceremonies, etc. These can either be for family members or just general court members. I think
families (house) should have a small court, but not the same thing as the title court. House court would have
different positions that go with you no matter the title you hold, and commoners (can be lower classes for the
republic) that are work to your house over a title."*

## What it is

A house keeps **seven small positions** of its own. They are the house's, and they read nothing else: not its rank,
not an office one of its people holds, not a throne. A landless house may fill all seven and a duke may fill none. If
the house rises, falls, loses its seat or its worlds, the court is as it was. When the head dies the court passes with
the house to whoever heads it next.

It is a different thing from the two it could be confused with:

| | Who is in it | What it follows | Where |
|---|---|---|---|
| **The council** (docs/HOUSEHOLD.md) | Grown people of the house, six posts | The house; they act for the head in the Senate, the courts and other houses | House, *The council* |
| **The house's court** (this page) | The commons sworn to the house, or family; seven positions | The house; they serve its people | House, *The court* |
| **A title's or an office's own people** | Whoever the rank or office brings | The title: it goes when the title goes | Not built as a court (see H5 in docs/GAPS.md) |

## The seven positions

Each does one plain thing in a rule the game already had. Skill is 1 to 5.

| Position | What it does | The figure |
|---|---|---|
| Tutor | Every child of the house learns as if a tutor had been paid for them | as a paid tutor at skill 1 to 3; as the best paid tutor at 4 and 5 |
| Wet nurse | Children come to the house more readily | births 8% likelier per point of skill |
| Physician | Somebody of the house who falls ill is less hurt and sooner well, and nobody's purse pays | health +2 per point on falling ill; each year ill, +2 and a point per skill, and well again (4 + skill) times in 10 |
| Captain of the guard | A knife meant for one of the house is less likely to find them | 4 points in a hundred off the attempt's odds per point of skill |
| Duel master | Each year the least practised of the young (14 to 35) is drilled in gunnery or tactics | learning in that skill equal to the master's skill each year, under the usual ceilings |
| Shipwright | Some of the materials of every hull laid down come back, and fleets in port are readier | 4% of materials per point; readiness +1 a year per point |
| Master of ceremonies | The old blood think the better of the house; a wedding of the house is done properly | old blood +1 a year (+2 at skill 4 and 5); legitimacy +1 at a wedding |

## Who holds them

**One of the commons.** For each empty position three people offer. They have a name, a sex, an age, a skill of 1 to 4
and a line on where they came from (a sergeant of marines paid off at one of the worlds of the Reach, a schoolmaster
whose school closed). The same three offer for two
years and then others do. One taken on is **sworn to the house**:

- They are paid a wage each year from the treasury, in **single credits** (docs/MONEY.md: a house's books are in
  hundreds, and a servant's wage is far below one): 1 credit at skill 1, 2 at skills 2 and 3, 3 at 4 and 5. A full
  court costs about 14 credits a year. A great house does not notice it; a house that clears fifty does.
- Loyalty begins at 40. A paid year adds 3. An unpaid year takes 15.
- Unpaid for two years running, one whose loyalty is under 60 leaves. At 60 and over they stay.
- Every six years in the place they grow a point better, to 5.
- They grow old. At about 66 to 73 (a wet nurse at 46 to 53) the house is told, and three years later they lay the
  work down.
- Let go after eight years or more, the rest of the household each lose 6 loyalty.

**One of the family.** Anyone grown of the house, born or taken in, who will serve the head and holds no post of the
council and no other position. They are paid nothing, think 3 the better of the head for being asked, and are as good
as their own training: the guard reads discipline or tactics, the duel master gunnery or tactics, the shipwright
engineering or logistics, the master of ceremonies charisma or oratory, the tutor their best skill less one, the
physician engineering, law or administration less one. A wet nurse of the family is a woman of 45 or under, at skill 2.
The place is empty again if they die, leave the house or defy the head.

## The law

**The Household's Wages** (Laws, under *The household*; any house may set it):

| Option | Wage | Loyalty a year | The commons |
|---|---|---|---|
| As little as they will take | 1 less each | 3 less | 5 worse |
| The customary wage (the default) | as above | as above | |
| An open hand | 1 more each | 3 more | 5 better; the merchants 3 worse |

## What is written for it

Seven scenes (`content/events/court.json`), each for something the court did that year: the physician's cure, the guard
between one of the house and a knife, the nurse at a new cradle, one grown old in the house's service, the duel
master's pupil, a wedding well ordered, and one who left unpaid. Most have an answer that the rest of the household
sees: it adds or takes 8 loyalty from every sworn member of the court at the year's end, for each point. What they
cost is in single credits, as the wages are (a purse of 5 for the physician, 20 set aside for a pension), except the
wedding feast, which is a house's expense (300 credits).

## Where it is on the screen

House, *The court*: seven cards. A filled one shows who holds it, their age, years in the place, skill as five pips,
wage, loyalty, where they came from and what the place is worth this year in the rule's own figure. An empty one shows
what the house does without it, the three who offer with a button each, and the relatives who could take it. An unpaid
household is among the alerts beside the map.

## What I chose, and what is not built

The numbers above are all mine. So are these readings of the request:

- **Seven positions, the seven you named.** "Bodyguards" is one position, the captain of the guard, and not a body of
  men with a count.
- **"Commoners sworn to your house" are not full persons.** They have a name, a face, an age, a skill and a loyalty,
  but no family, no purse, no ambitions and no profile page, and a scene cannot name them. That kept seven-times-three
  offers a year from flooding the game's list of people.
- **Family and commons are alternatives for the same place**, not two tiers.
- **Only your house keeps a court.** Other houses do not hire, are not guarded and have no physician.

Open questions for the owner are in docs/GAPS.md under *The house's court*.
