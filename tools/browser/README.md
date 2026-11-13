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
