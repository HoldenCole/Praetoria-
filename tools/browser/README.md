# Browser checks

`play.js` drives the published web build in a headless browser. It is how a change is checked on the screen, not only in the tests.

## Set up

```bash
dotnet publish src/Web/Praetoria.Web.csproj -c Release -o /tmp/praetoria-web
```

```bash
cd /tmp/praetoria-web/wwwroot && python3 -m http.server 8112
```

Playwright with Chromium must be installed where you run the script. `PRAETORIA_URL` overrides the address and `PRAETORIA_SHOTS` the screenshot folder. A new saga rolls its own seed, so every script opens the game through `address.js`, which asks for `?seed=1` in the address (or `PRAETORIA_SEED`): the same script plays the same world on every run. imperator.js, courts.js, ladder.js, duties.js, emergency.js, throne.js, breakaway.js, caesar.js, ck3.js, feud.js, beyond.js, khanate.js and whosewar.js also take `PRAETORIA_SCALE`, the device scale of
their pictures (their own default otherwise): 1 is much quicker under SwiftShader and is enough for pictures only you look at. A window is
docked beside the rail and scrolls inside its own body, so beyond.js and khanate.js grow the screen to hold a tall panel before they
photograph it; the rail has no Galaxy socket, so a script goes back to the map by the window's close (`[data-close-window]`). The
Chronicle opens on the last five years, so a script that reads it sees what was written in those.

## The four checks

| Command | What it tells you |
|---|---|
| `node play.js screens "The New Man" 3` | Every screen renders after three turns, with no raw code or `NaN` in the text. One screenshot each. |
| `node play.js turns "The Old Lion" 6 perf` | Seconds each End Turn takes, and with `perf` where they went. |
| `node play.js exchange "The Circling Heir" 8` | The four Exchange tabs render and an order goes through on each. |
| `node play.js phone "The Fallen House" 2` | Nine screens (every rebuilt dashboard) at 390 pixels wide, and what runs past the edge. |
| `node courtship.js "The New Man" 8` | Courtship on the Dynasty screen: begins a suit, presses it each year, proposes when the family would agree. |
| `node courtship.js "The New Man" 10 watch` | Does nothing about the family for ten years and reports what the kin do about their own matches. |
| `node household.js "The Old Lion" 4` | The household panel on the House screen: hands out both duties, takes someone in, and reports what the steward and marshal do each year. |
| `node education.js "The Old Lion" 6` | Education: chooses a study on the character sheet, sets a child a course against their bent and hires a tutor on the Dynasty screen, then reports how skills moved. |
| `node settle.js "The New Man"` | A venture of one's own: pledges the house's first venture to its creditors and settles it on one of the house from the Exchange, then reports the card and the purses list. |
| `node turntime.js "The Old Lion" 30` | How long the page takes to load, to reach the first turn, and to end a turn early and late in a game. |
| `node saveload.js "The Old Lion"` | Saves, reloads the page and continues: the turn and the top bar must come back as they were left. |
| `node feud.js "The Old Lion"` | Blood between houses: arranges accidents from the Houses screen (saying yes when it is asked first) until one is laid at your door, then reads the panel on the You screen and tries what it offers. |
| `node emergency.js "The Old Lion" 45` | Emergency powers: plays year by year reading the consulship panel on the Senate screen, until the Reach is in danger and then until a consul is voted the powers. |
| `node throne.js "The Imperial Court" 30` | The hold on a throne: reads the tile and the throne's block on the Senate screen (what the hold is made of, the buttons and why they are refused), then plays on until a standard is raised. |
| `node whosewar.js "The Old Lion"` | Whose war it is: the two settings for the powers beyond on the new-game screen (how strong, how often they attack); the orders on a nation's card (make war on your own account, raid or take and hold, keep or make a client; each a plate of a word or three with its whole wording in its tooltip, which the script reads and presses by), the line that says whose war it is, the war score and what it buys (under the card's fold, which the script opens), and the terms you can offer in a war of your own; and the mark for a conquered world on the map, with the legend opened from the console. No world is conquered at the start of a game, so the last picture is staged: the script hands the map its own model back (the one it holds) with two worlds marked, and says so in what it prints. |
| `node khanate.js "The Old Lion"` | The nations on the map: a picture of the whole Galaxy screen with the five regions beyond the rim (the Vashti Steppe, the Thessaran Shore, the Ardent Concord, the Outer Compact, the Hollow Crown), then the card of each on the Military window's Beyond tab, with its fold opened (where its ruler sits, what it holds, where its fleets are, what kind of neighbour it is). |
| `node beyond.js "The Old Lion"` | Beyond the Reach on the Military window's Beyond tab, each card's fold opened: each nation's ruler, the commander of its fleets, its march and whether a fleet holds it, and how far out an expedition would sail. |
| `node wealth.js "The Old Lion" 4` | Purses, heirlooms and the will on the House window's Purse tab: moves money, sets the allowance, writes a will and the estate's custom, has a shrine made once the purse holds its price, buys a share in the head's own name, gives the shrine to another head, reads the favour owed and tries to call it in, then reports the family's purses. What it takes from the treasury can leave a year short: when End Year asks first, it ends the year anyway and says how many years ended short. |
| `node ambitions.js "The Old Lion" 9` | Watches what the people of the house are after for some years, granting the first thing asked and refusing the second. |
| `node life.js "The Old Lion" 8` | A life: reads the Life panel on the You window's A life tab (and why a season away is refused, when it is), lays the house down if the head may, plays some years, and reads the new head's record, the old ties (or the line that there are none yet) and what was said of the dead. |
| `node imperator.js "The Old Lion" 16` | The three ideas from Imperator (docs/IDEAS_FROM_IMPERATOR.md 1, 2 and 9), played as a player would: reads the triumph on the Caesar panel and moves one when the Senate would hear it (for the house's general, or another house's), and answers it at the gate if one is voted; looks on the ladder for the refusal for commanding away and moves leave to stand in absence, then gives the command to another when the leave is spent; on the Senate's courts lets it be known that a general will be charged, brings a charge against one of the powerful, and stands trial on one brought against the player; moves a war on the Free Cities of Thessar once their purse can pay (year 5 on; `PRAETORIA_WAR_YEAR`), reads the gold block and answers the sum (third argument `take`, `refuse` or `expose`). Prints what each screen said and which features it reached; `PRAETORIA_CAP_MIN` caps the run (default 24, about eight years on a busy machine; 55 lets all sixteen years play, which the leave spent and the command given to another need). |
| `node ck3.js "The Old Lion" 10` | The first three ideas from docs/IDEAS_FROM_MODS.md: a command by service on the Military window's Service tab (offers your service where it could be taken; the tab is hidden while there is nothing to seek, hold or answer, and the script says so), the legates block under the Senate's offices, striking a grown relative from the line on the Dynasty window, then laying the house down and playing on: the friends and enemies handed down on the succession screen, a profile that itemises them, and the suit of the undutiful will on the Senate's courts if one is brought. |
| `node breakaway.js "The Old Lion" 4` | Breaking away on the Senate screen: what the house controls in each region, who would follow, whether the Reach would fight; whether it could stand alone, and the houses there with their regard; sends a quiet word, picks a kingdom, declares if it can, and plays some years of what follows. It also reads the peace of the Reach, stirs the camps and reads it again. |
| `node caesar.js "The Old Lion" 8` | The Caesar road's panel on the Military screen: the general's sway and whose the crews are, a donative, the triumph and the march (and why not), then some years, answering a recall if one comes. |
| `node ladder.js "The Risen Officer" 6` | The ladder of offices on the Senate screen: reads the rungs, stands for the first one open to the head, calls in what is owed, plays past the election and reads who sits and what was written. If the head wins a seat it reads the year's business of the office, decides it, and reads what the other magistrates did. |
| `node duties.js "The Circling Heir" 4` | "Your duties this year" on the Senate screen: reads the senator's year and any matter before the head's bench (this head is a Judge), decides each for some years, and checks nothing is left alerting. With "The Risen Officer" or "The New Man" it shows a junior's work (the watches, the ledgers). The seat's fourth choice is the committee's work, and the fifth button it presses moves the house to another committee. |
| `node family.js "The Old Lion" 6` | The family controls: on the wealth panel takes money into the head's purse, gives a relative a sum, lends another, sets whose money a dowry is; on the education panel asks a relative to study and sets one to teach another (both through the grown relative's Study select); plays on and reads what is owed and the chronicle. A year left short by what it took ends anyway when End Year asks. |
| `node register.js "The Old Lion" 8` | The controls closed straight off the gap register: lends at interest, borrows of a relative and pays back, breaks and remakes an entail, reads what each heirloom does, the household's keep, a relative's price for studying (an option of their Study select), and the old-ties line; then plays on, ending a short year anyway when End Year asks. |
| `node courts.js "The Old Lion" 14` | The courts on the Senate screen: what is on the record against former magistrates and who would hear it; plays until there is a charge to bring and brings it; makes an offer to a sitting praetor; reads what the chronicle says came of both. |
| `node audit.js "The Old Lion" 10` | The dashboard audit (docs/DASHBOARDS.md): every screen and its tabs at 1440 by 900, a screenshot of what is on screen without scrolling, and for each how much is empty, how many numbers, charts and buttons are in view and how far the page runs. When it looks at every screen it then presses each alert and prints the screen and tab it opened. `PRAETORIA_ONLY=MILITARY,HOUSE` looks at some screens only; `PRAETORIA_W` and `PRAETORIA_H` set the size. |
| `node workshop.js "The Heir" 3` | The character workshop: every tab, preset and mode, then a saga begun from it. Add `academy` to begin at the Academy. |
| `node faces.js "African"` | Ancestry and faces in the workshop's Face tab: before an ancestry is chosen the picker offers one face for each ancestry (each button carries `data-ancestry`), never the faces of one; then chooses the ancestry, a face among those painted for it, and checks the face is shown at five ages and worn by the preview. Exits 1 on a failed check or a console error. |
| `node keys.js` | The keys and the seed: two new sagas roll different seeds and `?seed=N` is kept; Enter opens each unanswered report, closes what came of an answer and ends the year only when nothing is waiting; the question asked before Quit takes Esc for its no and ignores Enter; a click on End Year and then Enter ends one year, not two. |
| `node profiles.js "The Old Lion"` | Portraits and profiles: your own portrait in the You window's header (`[data-you-portrait]`) opens your profile, a relative opens from its kin list, the family tree is painted, a portrait on a house's page opens that person; photographs each card, the Office, and the profile at 420 pixels wide. Honours `PRAETORIA_URL`. |
| `node tooltips.js [shots-dir]` | Tooltips and the map: a tooltip, one inside it and one inside that, all on the screen; they close when the pointer leaves; End Year's turned upward; the map's note on a star goes when the pointer does; the whole Reach from the console's fit plate, and the close map of a system. Exits 1 on a failed check. |
| `node laws.js "The Old Lion"` | The law screens: the law of the Reach by category, the edicts (issues one), a world's local-law block on the Worlds window (sets one), the summary on Your laws, the Senate's Act picker by category and the ladder's ages; then at the larger text size at 1600 by 1000 and 1366 by 768, and the large one at 1366 by 768, every tab of the Laws window and anything cut short. Pictures at deviceScaleFactor 2. |
| `node windows.js "The Old Lion" 12` | The windows, one by one: opens every window from the rail and every tab in it and photographs each (`win_<window>_<tab>.png`). `PRAETORIA_ONLY=house,laws` looks at some windows only; `PRAETORIA_W`, `PRAETORIA_H` and `PRAETORIA_SCALE` set the size and the device scale. Prints, for each, whether its first screen fits or how far it scrolls, and any section that is a heading over nothing but a muted line. The second argument plays that many years first; a third, `quick`, skips the photographs. |
| `node systems.js "The Old Lion" medium` | Solar systems (docs/SYSTEMS.md): picks the player's seat on the map, lists every body of it with its kind, feature and room, builds the first work that can be built, and reads what settling an empty system takes. The second argument is the size of the map. Uses `praetoriaSpace.select(name)`, which picks a system as a click would. |

## Reading the numbers

- A machine with no graphics card draws the 3D Office at about one frame a second. The script moves to a flat screen before ending a turn, so its turn times are the game's, not the room's. A turn should take well under a second.
- Add `?perf` to the address in any browser to print the same breakdown to the console after each turn.

## The other measurements

These run without a browser, from the tools project:

| Command | What it measures |
|---|---|
| `praetoria perf --scenario the_old_lion` | Native milliseconds a turn by system. |
| `praetoria drift --scenario the_institute --seed 1 --turns 60` | When the soak reports that a saved game did not resume the same: plays that one game straight and once saved and resumed, and says the first year they differ and what differs. |
| `praetoria survive --scenario all --seeds 12` | How often a passive player's line ends, and when the head weds and has a child. |
| `praetoria balance --scenario the_fallen_house --seeds 2` | Prices, production against use, the Exchange and the three orders after sixty years. |
| `praetoria qa --scenario all --seeds 3 --turns 60` | The soak: invariants, save and load, which events and commands were reached. |

## Playing a whole game

`driver.js` keeps one browser open on one game and is driven over HTTP: the screen as text, every control with its tooltip,
a click, an end of turn. It is how the playthroughs of docs/PLAYTHROUGH.md were played. `PLAYING.md` says how to use it.

