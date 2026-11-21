# Cities

The owner, 2026-11-21:

> I think there should be maybe a handful of so-called special cities in the world. So there should be a capital where all
> the military is, the commerce city, a city where a lot of resources come from and the city where the academy is. I think
> there should be more stuff at the capital of course ... but I also think that having special cities with special
> buildings and bonuses is important. And then each planet maybe can have around 2 to 3 special cities. One of them will
> always be the capital of the planet. One of them will be maybe a city focused on craftsman or a popular resort location.
> This is not necessarily the case for every single planet, but each planet and then each city should have some sort of
> special thing about it. I'm thinking of regional differences that make the game feel more alive and depending on where
> you are slightly different. Of course there are many cities in the game and will continue to be more and more but I
> think having a few special cities is important and you can develop the other ones of course but they are not as special.

And on how many people a world holds: "I think the habitability is good. I don't think humans should have different
habitability for oceans or farmlands or [plains] or anything like that. It would be if the world is frozen or a lava world
or a desert world."

## What a city is

A world has three provinces (docs/PROVINCES.md), the first of them the seat. **Each province has one chief city**, with a
name and a kind. That is the "two to three special cities" of a world: three on a world that is shared out, and the seat's
is always the world's capital. A city belongs to its province, so whoever holds the province holds the city.

A city's **kind** is what is special about it. It does five things, and all of them are in the rules, not only on the
screen:

- gives something each year to the house that holds the province (in the province's yield);
- moves where the world's prosperity heads, and its unrest a year (each city is a line of its own in the lists the House
  window shows for loyalty and prosperity);
- adds to the world's defence;
- adds to the manpower the world's holder can keep (the cap, docs/PLAYTHROUGH.md);
- opens **one building that only a city of that kind takes**.

| kind | each year, to the province's holder | on the world | its building |
|---|---|---|---|
| the capital | +1 influence | unrest −1, defence +0.2 | (the home world's own row is its) |
| a city of craftsmen | +1 materials | prosperity +2 | Guild Halls |
| a resort | +1 credits | prosperity +4, unrest −1 | Pleasure Gardens |
| a great port | +2 credits | | Bonded Warehouses |
| a garrison city | +1 manpower | defence +0.3, muster +6 | Citadel |
| a university town | +1 influence | prosperity +2 | Collegium |
| a holy city | | unrest −2 | Pilgrims' Basilica |
| a company town | +2 materials | unrest +1 | Deep Workings |
| a market city | +1 credits | prosperity +2 | Covered Market |
| a breadbasket city | +1 manpower, +1 credits | muster +4 | Great Granaries |
| a yard city | +1 materials | defence +0.2 | Slipways |
| an old city | +1 influence | prosperity +1 | Old Quarter |

Which kind a city is: one that suits what its province is given over to (a port in port towns, a company town in mining
country, a garrison city in a march) and the kind of world (a resort wants air and water); never the same kind twice on
one world; and by the habits of the region first, so the cities of one region lean one way and those of the next another.
Nothing is rolled: the same world always has the same cities, and a game from before there were cities has them laid out
the first year it is opened.

## The four great cities of the Reach

There is one of each. They are listed at the top of the Worlds window, and a world that holds one says so on its header and
on the map.

| city | where it is put | each year | on the world | its building |
|---|---|---|---|---|
| the Capital of the Reach | the seat of the world the Senate sits on | +2 influence, +2 credits, +2 manpower | unrest −1, defence +0.5, muster +10 | The Senate's Precinct (on the home world) |
| the Exchange city | the most peopled trading world that is not the capital | +4 credits | prosperity +3 | The Great Exchange |
| the Lode | the solid world with most in the ground | +4 materials | unrest +1 | The Great Pit |
| the Academy city | the most peopled living world that is none of those | +2 influence, +1 manpower | prosperity +2, muster +4 | The Institute's Halls |

On the starting map: Dramont on Aurelia Prime, Calene on Lucent, Novion on Oriel's Rest and Wynvale on Highmarch Terraces.

## How many live there

A world's citizens come from the census (docs/PLAYTHROUGH.md, "how many people a world holds"). A city holds its kind's
share of them (4% for a capital, 1% to 2.5% for the others, 5% to 10% for a great city), a little more or less by the
city, and never more than its province. The rest live in **lesser cities and towns**, which are counted (about sixty for
every billion) and not named; they grow with the world's development, as before.

People live as thickly on an ocean world as on any living world. It is ice, fire, sand and no air that thin them: frozen
0.2, a forge world 0.35, an arid world 0.45, a barren one 0.12.

## The buildings

Fifteen: one for each kind but the plain capital, and one for each great city. They are ordinary buildings in every other
way (a cost, years to build, upkeep, levels), raised in the province's slots by the house that holds it; the Senate's
Precinct stands on the home world of the capital of the Reach. Each has its picture (docs/BUILDINGS.md, "Artwork").

## What is simple, and what is not here

- A city's kind never changes, and a city cannot be founded, renamed, sacked or moved. A new colony has its capital and no
  other city until it has provinces.
- The lesser cities are a number. They cannot be picked out or built in one by one.
- The Academy city gives influence, manpower and room to muster. It does not yet change what the Academy teaches or whom
  it takes; nor does the Exchange city change the Exchange's prices or fees.
- A nation's worlds have cities by the same rule, but none of the four great cities is put on one.
- Other houses do not choose what to build by the city: they raise a city's building when it is the cheapest that suits.

## Numbers that were my choice

Every figure in the two tables; the shares of a world's citizens; "about sixty lesser cities for every billion"; that the
seat's capital has no building of its own; and where each great city is put.
