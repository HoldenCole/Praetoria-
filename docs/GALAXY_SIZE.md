# The size of the map

The owner, 2026-11-20: "a scaler for how big the galaxy is because right now it seems pretty small so that could be the
small setting, but then we need a medium large and extra large. So it would be the actual domain as in the empire that
you are a part of, and the other empires would be bigger as well. With the borders, it should be more obvious about
different empires versus yours because right now it looks relatively similar."

## What it is

A setting on the new-game screen, "The size of the map". The map as written (`content/galaxy/the_reach.json`) is the
**small** one. The others are grown from it when the game begins.

| size | systems | houses in an Old Lion game | a turn, native (the browser is a few times slower) |
|---|---|---|---|
| Small | 50 | 30 | about 60 ms |
| Medium | about 80 | about 43 | |
| Large | about 115 | about 50 | |
| Extra large | about 165 | about 65 to 75 | about 300 ms |

The figures are from seed 7. The first turn in a headless browser took 1.0, 1.7, 1.6 and 3.0 seconds.

## How a map is grown (`GalaxyGrowth`, `content/galaxy/growth.json`)

1. **Room.** Every written system is moved out from the middle of the map by the square root of the size, so the map has
   room in proportion to what it will hold and systems sit as far apart as they do on the small map.
2. **New systems, region by region.** Each region gains its share (a region of three becomes five, seven or ten). A new
   system is hung by a lane off one of its region, a lane's length away, clear of every other system, nearer its own
   region than any other, on a lane that crosses none. Of several such places the one with the most systems round it
   is taken, so the gaps inside a realm fill before it sprawls outward.
3. **A second lane** for about half of them, within the region or to another region of the Reach, so the new systems
   are a web. A nation's space gains no new lane to the Reach: the written marches stay the only doors.
4. **A world.** Its kind leans to what its region already has; a belt for some; a deposit where its kind has one. Names
   come from the lists in `growth.json`, a nation's from its own list.
5. **Who holds it, in the Reach.** A house gains again a share of what it holds (55 in the hundred for each doubling of
   the map), taking the systems hung off its own; the player's house by the same rule. New houses are raised for the
   rest: a head, a husband or wife, usually a child, made like people already written, and for some a ship or two.
   On the rim three new systems in ten are left to nobody, in the middle about one in eight, in the core none.
6. **Who holds it, beyond.** Every new system in a nation's space is that nation's (`NationSystem.Worlds`): the list of
   a nation's worlds follows from the map and nothing is stored. A nation's strength grows by six tenths of what the
   map grew by, so the neighbours of a larger Reach are larger.

It is all from the game's seed and draws nothing from the world's own dice: the same seed and size give the same map,
and another seed another. The map is in the save. `--size` gives the tools the same setting
(`praetoria qa --size extra_large`).

## Borders (`space.js`)

- Every realm has a **border**: a strong line in its colour with a glow inside it, drawn where the realm changes however
  many cells of it lie between, at every zoom level.
- **Your own realm's border is gold.** The land of **another realm is hatched** in its colour.
- A world nobody holds inside the Reach is inside the Reach's border; its cell is paler.
- The picture is as fine on a large map as on the small one (about 2.6 map units to a pixel), and each system is walked
  over its own patch of it rather than over all of it, so a large map costs no more to draw per system.

## What made a large map affordable

Measured with `praetoria perf --size extra_large`: a turn went from about 410 ms to about 300 ms, and the small map from
87 to 61, by four changes that alter no result. `Economy.HeadOf` is one pass with nothing sorted (it is asked from nearly
two hundred places); the year's quarrels are worked out once for every bench, not once a bench; a rival steward asks what
a venture would clear before asking whether it may be founded; `World.PlanetOf` remembers which holding is each
system's planet.

## Not done

See docs/GAPS.md, "The size of the map".
