# Browser checks

`play.js` drives the published web build in a headless browser. It is how a change is checked on the screen, not only in the tests.

## Set up

```bash
dotnet publish src/Web/Praetoria.Web.csproj -c Release -o /tmp/praetoria-web
```

```bash
cd /tmp/praetoria-web/wwwroot && python3 -m http.server 8112
```

Playwright with Chromium must be installed where you run the script. `PRAETORIA_URL` overrides the address and `PRAETORIA_SHOTS` the screenshot folder.

## The four checks

| Command | What it tells you |
|---|---|
| `node play.js screens "The New Man" 3` | Every screen renders after three turns, with no raw code or `NaN` in the text. One screenshot each. |
| `node play.js turns "The Old Lion" 6 perf` | Seconds each End Turn takes, and with `perf` where they went. |
| `node play.js exchange "The Circling Heir" 8` | The four Exchange tabs render and an order goes through on each. |
| `node play.js phone "The Fallen House" 2` | The main screens at 390 pixels wide, and what runs past the edge. |
| `node courtship.js "The New Man" 8` | Courtship on the Dynasty screen: begins a suit, presses it each year, proposes when the family would agree. |
| `node courtship.js "The New Man" 10 watch` | Does nothing about the family for ten years and reports what the kin do about their own matches. |
| `node household.js "The Old Lion" 4` | The household panel on the House screen: hands out both duties, takes someone in, and reports what the steward and marshal do each year. |
| `node education.js "The Old Lion" 6` | Education: chooses a study on the character sheet, sets a child a course against their bent and hires a tutor on the Dynasty screen, then reports how skills moved. |
| `node wealth.js "The Old Lion" 4` | Purses, heirlooms and the will on the House screen: moves money, sets the allowance, writes a will and the estate's custom, then reports the family's purses. |
| `node ambitions.js "The Old Lion" 9` | Watches what the people of the house are after for some years, granting the first thing asked and refusing the second. |
| `node life.js "The Old Lion" 8` | A life: reads the Life panel, lays the house down if the head may, plays some years, and reads the new head's record and what was said of the dead. |
| `node caesar.js "The Old Lion" 8` | The Caesar road's panel on the Military screen: the general's sway and whose the crews are, a donative, the triumph and the march (and why not), then some years, answering a recall if one comes. |
| `node ladder.js "The Risen Officer" 6` | The ladder of offices on the Senate screen: reads the rungs, stands for the first one open to the head, calls in what is owed, plays past the election and reads who sits and what was written. |
| `node workshop.js "The Heir" 3` | The character workshop: every tab, preset and mode, then a saga begun from it. Add `academy` to begin at the Academy. |

## Reading the numbers

- A machine with no graphics card draws the 3D Office at about one frame a second. The script moves to a flat screen before ending a turn, so its turn times are the game's, not the room's. A turn should take well under a second.
- Add `?perf` to the address in any browser to print the same breakdown to the console after each turn.

## The other measurements

These run without a browser, from the tools project:

| Command | What it measures |
|---|---|
| `praetoria perf --scenario the_old_lion` | Native milliseconds a turn by system. |
| `praetoria survive --scenario all --seeds 12` | How often a passive player's line ends, and when the head weds and has a child. |
| `praetoria balance --scenario the_fallen_house --seeds 2` | Prices, production against use, the Exchange and the three orders after sixty years. |
| `praetoria qa --scenario all --seeds 3 --turns 60` | The soak: invariants, save and load, which events and commands were reached. |
