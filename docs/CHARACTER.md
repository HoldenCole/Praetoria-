# A character of your own

Built 2026-11-13. The owner asked for a custom character "with lots of toggles, freedom, lots of things to customise,
ability to make OP". It is the first card on the title screen.

## Two modes

| Mode | Rule |
|---|---|
| Standard | A budget of 60 points and sensible caps. Strengths are paid for, so the start is fair. |
| Free | No budget and very high caps. The run is marked as custom in the save and in the saga's epilogue. |

A **power rating** is shown in both modes: under 30 points *modest*, to 60 *able*, to 110 *formidable*, to 220
*overpowered*, above that *absurd*. Begin is disabled while the mode objects to anything, and the objections are listed.

## What can be set

| Tab | Dials |
|---|---|
| The person | Name, sex, age (16 to 80), service and rank in it, ambition, **where they come from** (one of eight backgrounds, or none), natures, aptitudes, and each of thirteen skills. |
| The face | Skin, hair, cut, beard, a scar. Anything left on "dice" is thrown from the person as before. Age still greys and lines it. |
| The house | Family name, old or new name, rank, legitimacy, culture, party, colour, all eight goods in the treasury. In the Republic: a seat by the census, a patron. |
| What it holds | Worlds (any system; a world another house holds is taken from it), hulls, ventures and where they stand. |
| The family | Spouse (and the house they come from), children, brothers and sisters, and **a father and a mother who may be living**: names, sexes, ages. For each of them of sixteen or more: **their own service, a step in it, a nature and a gift**. |
| The world | Republic or Imperium. Begin in the world at the age chosen, or at sixteen with the Academy year. A friend and a rival among the houses. |

Shortcuts: five presets (*The Heir*, *The Officer*, *The New Man*, *The Upstart*, who has nothing at all, and *The Colossus*, which is frankly overpowered),
**Throw the dice** for a legal character by chance, and **Save to a file** / **Load a file**.

## What it costs (Standard)

| Thing | Points |
|---|---|
| A skill at level n | 1 + 2 + … + n (a 3 is six points; a 4 is ten) |
| An aptitude | 4 |
| Rank in the service | 4 for each rank above the first |
| The house's rank | 6 a step |
| Legitimacy | 1 for every 5 |
| An old name / a seat by the census / a patron | 3 / 4 / 3 |
| The treasury | credits and materials and manpower 1 for every 8; influence 2 each; exotics 2; helium-3 1 for every 2; rare earths 1; isotopes 2 |
| A world / a hull / a venture | 10 / 2 / 4 |
| Spouse / child / grown child / sibling | 2 / 2 / 3 / 1 |
| A living father or mother | 2 each |
| A relative's step in a service / a relative's gift | 2 a step / 2 |
| A background | what it gives, by this list (Old Blood 9, The Upjumped 4, Merchant Scion 6, Soldier's Child 3, Scholar of the Enclave 10, Ward of the Court 17, Clerk's Child 5, Rim-born 1); never less than −4 |
| A friend / a rival among the great houses | 2 / −2 |

Natures are free: they are character, and each has its own price in stress during play.

## The caps

| | Standard | Free |
|---|---|---|
| A skill | 4 | 10 |
| Natures / aptitudes | 2 / 2 | 9 / 11 (all of them) |
| Rank in the service | 3 | 6 |
| The house's rank | the third (Legate, or Count) | the fifth; the throne is never a starting rank |
| Legitimacy | 60 | 100 |
| Worlds | 2: unclaimed, or from a house that holds another; never the capital | 12: any, the capital included |
| Hulls / ventures | 8 / 3 | 80 / 12 |
| Children / siblings | 3 / 3 | 8 / 6 |
| A relative's step in a service | 2, and none before twenty | 5 |
| A parent's age | sixteen years more than the head's, to 95 | the same |
| Credits / other goods / influence | 150 / 60 / 10 | 5000 / 2000 / 200 |

## How it is built

- `CustomCharacter` is plain data and is the saved file.
- `CharacterWorkshop.Cost` prices it; `Problems` lists what the mode objects to; `Apply` lays it over a blank start
  before the turn controller exists; `Finish` seats the house with its party afterwards.
- The blank starts are two hidden scenarios: `custom_republic` (the standing houses of the Reach and one empty house)
  and `custom_imperium` (the same under the Emperor of *The Imperial Court*).
- A world taken from another house: its holding changes hands, its stations there too, and its ships in that system go
  to another of its ports, or are paid off if it has none.
- A fleet is one cruiser to every four hulls and frigates for the rest, berthed at the first world or venture.
- Brothers and sisters share two parents who exist as dead characters, so the kinship rules treat them as kin.
- A chosen face is stored in the world as `look:<person>` and read by every portrait of that person.
- The world carries the flags `custom_character` and, in Free, `custom_free`, and the counter `custom_power`.

## The creator's map, and a spouse from a named house

Built 2026-11-21 from the playthroughs (docs/PLAYTHROUGH.md, "A first game": N11, N13). Before this the worlds were
fifty names in a flat list with only the holder beside each; a world that could not be taken gave no reason; and "wed"
gave a name and an age and nothing else.

**The map.** "What it holds" now opens on a map of the Reach as the scenario writes it: the lanes, a filled dot for a held
world in its house's colour (the colours the houses wear in the game), a ring for an unclaimed one, a dashed one for a
nation's space, a dim one for a world this start may not take. Pointing at a world shows a card: what kind of world it is
(for an unclaimed one, what it would become), its region and ring, who holds it, what it brings a year (the kind's yield
less its upkeep, before buildings and prosperity), what is in the ground and how large its belt is, how many lanes leave
it and whether one leads into a nation's space, the lawlessness of its region, and, when it may not be taken, why.
Clicking takes a world or gives it up. The old list is kept folded under the map, with the kind, the holder and the
yield on each row.

The map and the list read one account, `CharacterWorkshop.Sites`, and the reason a world is refused is
`CharacterWorkshop.WorldRefusal`: the same three rules `Problems` holds a whole character to (a nation's space, the
capital, a house's only world), so the map cannot offer what the start would refuse.

**The spouse's house.** A spouse may be "of the house" of any house on the map (`KinSpec.HouseId`). Such a husband or
wife keeps that house's name and is a member of it, as one married in play is; they are listed with the household under
"Married into the household", and the children are the new house's. The head of that house thinks well of the new head
from the first day (regard 25, and 20 the other way; a friend or a rival named on the same house says more and wins). It
costs 1 point in a standard start. The start does not give what a wedding in play gives by rank (legitimacy, or a claim
on the greater house's title): that would be a second price to set, and it is left for the owner.

## Where they come from, and the family each of them

**A background** is the same thing a written start may take (content/backgrounds): a change to the house's legitimacy
and purse, a nature or a gift, a skill or two, and a mark on the person (`background:<id>`). Eight of them: Old Blood,
The Upjumped, Merchant Scion, Soldier's Child, Scholar of the Enclave, Ward of the Court, and two low births added for
a house that begins with nothing, **Clerk's Child** and **Rim-born**. It is applied after the dials, so a house set to
legitimacy 5 that takes Rim-born begins at 0. It is priced by the list above, so it is not a free gift in a standard
start. Nothing in play reads the mark yet (see below).

**The family.** Each relative of sixteen or more has a line under their name: the service they are in (your own if left
alone), how far up it they have got (from twenty; two steps at most in a standard start), what they are known for (a
nature by the dice if left alone) and a gift. A father and a mother may be living: they are of the house, they have a
service and a step like anybody else, and brothers and sisters are their children too. You are the head of the house
whoever else is living. Left as they were, parents are names in the chronicle and no more.

The numbers here (2 points a parent, 2 a step, 2 a gift, twenty for a first step, sixteen years between a parent and a
child) are mine.

## Checked

Sixteen tests: the presets are legal and four fit the budget; a background moves the person and the house and is priced; a father and a mother may be living; each relative of age has a service, a step, a nature and a gift; the Upstart begins with nothing; a character is built as written; Standard refuses what
Free allows; a taken world changes hands cleanly; the family is real kin; an Academy start works in both eras; a file
round-trips and the dice make legal characters for twenty-five seeds in each mode; four extreme characters (nothing at
all, everything at the maximum, an eighty-year-old, a parent of eight) play thirty years without breaking the world;
the epilogue says how the founder was made. `tools/browser/workshop.js` drives every tab, every preset and both modes
in the browser and begins a saga from it.

## Not in this piece

- Stations cannot be chosen on their own; they come with a world in the same system.
- The face has five dials. Dress by service and rank, and family resemblance, are in the look and feel pass.
- A spouse comes into the player's house; a marriage into a named house of the Reach is part of the courtship work.
- Kin have a service, a step, a nature and a gift. Their skills, their ambition and their face are still thrown.
- No uncles, aunts, cousins or grandparents, and no household: a mentor, a steward, a ward or a companion cannot be set
  on the first day (they can all be had in play, docs/HOUSEHOLD.md).
- A relative's regard for the head is fixed (warm). A brother who resents you cannot be made.
- A background is a start and nothing after: no event reads `background:<id>`, so a Rim-born senator is never
  reminded of it. docs/UPSTART.md lists this with the other things that need the owner's answer.
- The older, lighter customisation (a background and two traits laid over a written start) is unchanged.
