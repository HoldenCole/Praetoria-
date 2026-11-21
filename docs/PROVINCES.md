# The provinces of a world

The owner, 2026-11-20: "for splitting up a planet there are maybe a handful of ways to split it up. So let's say there are
three regions on each planet and then the planet overall. The governor of the planet, the person who controls the whole
planet, also controls one of those regions. And then the other two regions are controlled by different houses. So one
region would be something like helium three production. One region would be military focused, and one region having
another resource of some kind. This breakup won't be the same for every planet, but you kind of get the idea: there is a
family or a house in charge of one region. Governorship is for life unless the leader over you tries to [take it]; in that
case you can either accept it and step down and have that other family be in charge of you, which moves you down in the
rankings, or you can fight in basically what would be a civil war, and this would be one of the ways a civil war could get
started."

They are called **provinces** in the game, because "region" already means a part of the galaxy (the Greywater Reach, the
leader of a region). Content: `content/holdings/provinces.json`. Code: `ProvinceSystem`, `ProvinceCommands`.

## What a world is made of

Every planet has **three provinces**.

- **The seat** is the first. It is always the house's that holds the world, and goes with the world when it changes hands.
- **The other two** each have a **focus** and a **holder**: a house, which holds it for life.

A world's two focuses are chosen for that world and are the same in every game: a deposit its system carries comes first
(helium-3 fields, rare-earth diggings, isotope pits); a fortress or a key of the lanes has a march; the rest is what suits
its kind of world, in an order that is the world's own.

| focus | where | pays its holder a year | |
|---|---|---|---|
| The seat | every world | 1 credit, 1 influence | the holder of the world's |
| Helium-3 fields | a system with helium-3 | 1 helium-3, 1 credit | |
| Rare-earth diggings | a system with rare earths | 1 rare earths, 1 credit | |
| Isotope pits | a system with isotopes | 1 isotopes, 1 credit | |
| A march | anywhere; first on a fortress or a key of the lanes | 2 manpower | defence +0.2 |
| Farmland | a living or ocean world | 1 manpower, 1 credit | |
| Port towns | an ocean or living world, a giant | 2 credits | |
| Foundry country | a forge, dead or arid world | 2 materials | |
| Mining country | a dead, arid, frozen or forge world, a belt | 1 material, 1 credit | |
| Ice fields | a frozen world | 1 material, 1 manpower | |
| Cloud works | a giant | 1 credit, 1 material | |
| Caravan country | an arid world | 1 credit, 1 manpower | |
| Belt claims | a belt | 2 materials | |

**Who holds them at the start.** On a world of the Reach the other two provinces go to two different houses of the same
part of the galaxy, the houses with fewest first, so every house has a few and none has many; a house with no world has
provinces. A nation's world is all the nation's. A new colony is all its founder's.

## What holding one is worth

- A province **pays its holder** what its focus yields, on the ledger under its own name. A house is paid by the seats
  of its own worlds and by every province it was given, on its own world or another's.
- A province other than the seat has **building slots of its own** (the owner, 2026-11-20: yes): one to begin with, one
  more at development 3, 6 and 9 of its system, one more when its holder is a count and again an archduke, four at the
  most. Its holder builds the ordinary buildings of the world's kind in them (a smelter in a province of a forge world, a
  tidal mill on an ocean), and a building that works a deposit only in the province given over to that deposit. What
  they yield is the holder's, on the province's line. Each can be **taken to level II and III** like any building, as far
  as the system's development allows, and yields that many times over. What a building does for a world it does from a
  province too: room for people (by its level), where the world's prosperity heads, its unrest. Not for a province: the home world's own eight, anything a system
  has only one of, great works. The buildings go with the province when it changes hands. The seat has no row of its
  own: the world's row is its holder's.
- A province can be **improved** to level II and III by its holder (10 credits and 8 materials, then twice that); it pays
  that many times over. How high it may go is the development of the system it is in, as for a building (3 and 6).
- A **march defends** the system it is in, whoever holds the world.
- A province pays **nothing** while its world is in revolt, or while its holder is at war with the house that holds the world.

## What a holder owes the house over it

The owner, 2026-11-20: yes, a house with a province owes the house that holds the world "a share of yield, troops, a vote".

| | what | when |
|---|---|---|
| **A tithe** | 1 credit a year for each level of the province | every year |
| **A levy** | 1 manpower a year for each level, on top of the tithe | in any year the holder of the world is at war |
| **A squadron** | 1 hull for each level of the province, as a fleet at the world, under the holder of the world | from the first year of its war until the peace; once a war, and not replaced if it is lost |
| **Its voice** | it leans the holder's way in the Senate, as a client leans to a patron (+2) | on what that house moves, and on what is aimed at it |

The tithe and the levy come off the province's line in the holder's ledger and appear in the ledger of the house that
holds the world as "Dues of" the province. Because it leans that way, a house with a province under another is counted
among "the houses that would follow" when that house takes a region out of the Reach (docs/BREAKAWAY.md).

**The house that holds the world sets the dues** of each province:

| dues | paid | squadron | voice |
|---|---|---|---|
| forgiven | nothing | none | leans its way the more (+3) |
| customary | the tithe; the levy in war | yes | leans its way (+2) |
| heavy | twice both | yes | none |

**A house may refuse.** Withholding its dues, it pays nothing, sends nothing and leans nowhere, and the house over it
may ask it to give up the province **at no cost in influence**. A rival under heavy dues that is the match of the house
over it stops paying, and pays again when the dues are eased. A rival that holds a world answers a house that withholds
by asking for the province, if it is at peace and at least that house's match; so refusing is a step toward the war
that standing ends in. A rival at war sometimes asks heavy dues of the weaker houses under it and goes back to what is
customary at the peace.

**The levy is its own house's.** The squadron is named for the house that sent it and sails under one of that house's
own people, where it has one to send. The house it sails for cannot fold it into another fleet or put its own officer
over it. Under heavy dues it comes with its loyalty low, and the ordinary rules for a fleet that will not obey apply to
it. A house sends no squadron against a house it also holds a province under. If the squadron is **destroyed**, the
house that sent it loses 2 manpower a hull, its head thinks the less of the head it was sent to, and no other is sent
until the peace. A squadron that goes home at the peace costs its house nothing.

**The dues can be commuted.** The holder of a province pays six years of its tithe at once to the house over it and
owes nothing for ten years: no tithe, no levy, no squadron, and it leans that house's way the more. Either of the two
may ask. A house over it that is at war wants its levy and will not; a holder without the coin cannot. Rivals do it
among themselves, about one year in ten when the holder has coin to spare.

**A peace settles what was fought over.** When a house that stood and the house over it make peace, and the house that
stood still holds what it stood for, the peace holds it: for ten years it may not be asked for, and its dues are at
what is customary and may not be made heavy. The same holds for a world, between its holder and the leader over it.

**Rivals forgive dues to win a house over.** A rival that holds a world, at peace, sooner or later forgives the dues of
a house under it that is stronger than itself, to have that house's voice in the Senate.

**Every house's vote is noticed.** When a house casts a vote on something the house over it moved, or that is aimed at
that house, it is counted: a vote against adds one, a vote its way takes one off. At two, the house over it asks for
the province, if it is at peace and at least that house's match; a rival asked steps down or fights as any does. The
player's vote is also felt at once in the regard between the two heads (a little up for a vote its way, a good deal
down for one against), as with a patron. A vote left to drift is not noticed. A house at war with the house
over it pays nothing, sends nothing and leans nowhere; nothing is owed on a world in revolt.

**Part of a world, the whole world, the whole system.** A house with a province has that province, its slots and what it
builds in them, and nothing else of the world. The house that holds the world has its seat, its row of building slots, every other place in the system, its
infrastructure and its great works. A house that holds the world and all three of its provinces, with no other house's
holding in the system, has the whole of it, and one more building slot on the home world for that (docs/BUILDINGS.md).

## For life, unless the one over you asks

A province, and a world, is held for life. It is taken only by **the one over the holder**, who must ask:

| what is asked for | who may ask | for whom | costs |
|---|---|---|---|
| a province of a world | the house that holds the world | itself, or any house | 3 influence |
| the world itself | the leader over that house: the head of its state if it has left the Reach, the emperor if there is one, otherwise the house that leads its part of the galaxy | a house that already holds a province of that world | 6 influence |

One such demand a year. The one asked **steps down, or stands**.

- **Stepping down from a province**: it goes to the house it was asked for.
- **Stepping down from a world**: the other family holds the world now, and the house that stepped down holds the
  province that family had, under it. It loses 6 legitimacy and **a rung of its title** (a duke is a count, a count a
  baron; nobody falls below the lowest rung), and with the world gone it stands lower in every ranking that counts worlds.
- **Standing**: it is war between the house and the one who asked. The world is settled by the war in the ordinary way
  (sieges, a peace). A province is lost when its house has no fleet left in the field.

**A rival house** asked to step down does so when it has no fleet, or when the one who asks is half as strong again as
it is; otherwise it fights. **The player** is asked and has a year to answer, on the world's panel and in the alerts;
silence is taken for defiance.

**Rivals ask too.** A rival that holds a world and is at peace asks for one of its provinces for itself about one year in
thirty, when it is at least the holder's match. The leader over a rival asks it to make way for the stronger of the houses
under it about one year in sixty. In a sixty-year game of the small map this comes to a handful of demands and a few wars.

## Not done

See docs/GAPS.md, "Provinces".

## Giving a province away (2026-11-21)

From the playthroughs (docs/PLAYTHROUGH.md, Medium, Building and holding, B21). The house that holds a world may give a
province it still holds itself to another house (`GrantProvinceCommand`, `ProvinceSystem.Grant`): theirs for life, with what
is built in it, at the customary dues. Their head thinks 12 the better of the giver, and the giver gains 1 legitimacy. The
seat is not given; a house holds one province of a world; not to a house at war with the giver; only the head gives land.
It is asked back like any province (`DemandSeatCommand`). Other houses do not yet give provinces of their own accord.
