# Changelog

## [Unreleased]

### Fixed
- **Half the Reach went bankrupt and stayed there.** Rival stewards founded ventures in alphabetical order (Academy
  Patronage first, a credit drain), borrowed on a sinking ledger (a loan every seven turns, each deeper), and let
  materials pile up unsold; a house whose last member died kept its worlds and let them rot at unrest 100. Stewards now
  found the venture that clears the most coin and nothing that loses it, sell a losing venture before borrowing,
  borrow only when the ledger is positive, sell materials in smaller lots, and stake claims on a thinner cushion when
  their land earns nothing; a headless rival house raises a cousin from the provinces to take up the banner.
- The court offered a marriage match and an "accident" against children of other houses; neither is listed now, and the
  assassinate command refuses a target under sixteen outright. The Houses card names the system of each venture.
- The simulate harness's auto-player now weds, so sixty-turn soaks have heirs; the fallen house no longer "dies" every
  run for want of a marriage the harness never proposed.

### Changed
- **Territory as areas, not bubbles**: the map is partitioned the way Star Dynasties does it — every point belongs to
  its nearest system's region, regions are crisp bordered areas in the dominant house's colour with a dark tinted fill
  (hatched for a free march), region names sit at the centre of their area and no longer collide, and the minimap shows
  the same partition. The per-house glow field and the ring ellipses are gone.
- **A quieter map** after a second look at the reference games: lanes are hairlines with no glow (fronts and your own
  reach excepted), the default view opens on your own seat with the Reach running off the edges, a minimap in the
  corner shows the whole with the viewport drawn on it (click to travel), a fit button shows everything, system
  markers and orbit rings are smaller and fainter, region fills lighter, and labels thin out as you zoom away.

### Added
- **Reactive events on the engine's own flags** (GAP_ANALYSIS §4 item 4): the enemy's envoy under a flag of parley, the
  assassin's knife traced to its house, the coalition gathering at one inn; the engine now also writes the generic
  `at_war_any` and `assassin_any` flags content can read.
- **Region names avoid system names too**, by a bounded search around each region's centre.
- **The galaxy as a galaxy** (GDD §19): the map is laid out twice as wide; every system carries two to five bodies
  (worlds, moons, giants, belts) drawn on their orbits around the star; regions are highlighted as soft areas around
  their systems; the lane web is sparse (forty lanes, under three a system) and runs through gateways; **chokepoints**
  are computed from the web, pay **tolls** to the holder of their world, and bar passage to an enemy while the holder's
  fleet sits there; the map marks them and the planet card lists a system's bodies, lanes and whether it is a chokepoint.
- **The rings of the Reach** (GDD §19; `docs/GALAXY.md`): the galaxy grows to ten regions and thirty-one systems laid
  out in three rings with a **capital**. The **core** is commerce and the court (more coin, doubled charters, order);
  the **rim** is lawless and restless (raiders tax the ledger, unrest climbs, allegiance to the capital settles low);
  the **middle** leans whichever way it is pushed. Every region carries **allegiance** and **lawlessness** that drift
  each turn; a restless world in a disaffected region **rises** and yields nothing; a garrison ends it; left four turns
  it **breaks away** as a free hold under a new house; a region's dominant house that has stopped believing in the
  capital **declares a free march**. Four new regions with eleven houses, two worlds no one holds, five ring events,
  the player's decrees on a rising, NPC fleets that sail to put risings down, and the rings drawn on the map.
- **The economy, deeper** (GDD §17; `docs/ECONOMY.md`): every holding has **prosperity** (a governor's administration,
  what is built, the tithe, unrest and blockade move it; it scales output; an investment lifts it); systems carry
  **asteroid belts** and houses stake **claims** in them (the landless anywhere); **ventures** — trading companies,
  mining concerns, shipping lines, merchant banks, salvage guilds, spice syndicates, free companies, academy patronage —
  founded, expanded and sold, paying a **charter** to whoever holds the world; the standing **loan** with instalments
  and a lender's interest; **house wealth** reckoned and ranked every turn with tiers from destitute to opulent. A
  per-turn **ledger** on the House screen shows every line; the Steward reports your rank; NPC stewards borrow, invest,
  stake and trade; five venture events; the two landless houses of the Reach each start with a venture.
- **Houses, Worlds, Chronicle and You screens** on their own lit 3D sets: the hall of banners (every house on the walls,
  heads on plates, the greatest nearest the crest, war under a red pulse); the orrery (a dome, an armillary sun, one arm
  per region with the worlds as turning globes); the archive (stacks, the chronicle as a framed timeline, a volume per
  ten turns on the reading table); the quarters (a viewport, the family on the mantel, a trophy case of honors, a desk
  with the record of service). Every screen in the game now stands on a set.
- **Dynasty, Military and House screens** on their own lit 3D sets: the family tree as generations down a vaulted hall
  (parent-to-child lines on the floor, spouse threads, other houses' spouses in their colours, heirs and the remembered);
  the command deck with a holographic strategic table (fronts in red, fleets as markers), the admirals at the table, the
  fleet outside the glass, readiness / hulls / fronts on the wall; the keep's great hall with the worlds on pedestals
  (restless ones under a red light), the household at the long table, treasury, decrees, holdings and their chairs.
- **Senate and Academy screens** on their own lit 3D sets: the political system (spheres of power, threat and coalition
  pressure, the ladder of titles, the throne, laws, honors, crises) in a domed chamber with the great houses seated by
  influence; the cadet year on a parade ground at dawn with the cohort in ranks. Galaxy polish: textured lit planets,
  star classes with flares, glowing lanes, hover tooltips, smoothed camera, merged territory fields.
- **Art direction** (`docs/ART_DIRECTION.md`) from the reference games, and the first pass against it: the galaxy as a
  rendered WebGL scene (shader nebula, bloomed stars, suns, hyperlane web, territory fields with lit borders, lit
  planets, fleet glyphs); the office as a lit 3D set (reflective floor, viewport onto the seat's world, holographic star
  table, banners, sconces, framed cameo plates for the player and visitors); portraits as drawn cameo busts; game-icons
  glyphs; smoked-glass chrome with brass hairlines; Cinzel titles. No stock photographs, no avatars.
- **Grand-strategy shell** for the browser client: resource strip with forecasts, navigation rail, outliner, event
  windows with the cast, End Turn dock; screens for the Office (a furnishable SVG room visitors walk into), Galaxy
  (regions, planets by class, pan/zoom, planet cards), Worlds, House, Military, Dynasty, Houses and Chronicle; a
  succession interstitial when the seat passes.
- **The Reach expands**: six regions of three worlds with cultures and descriptions; twenty minor houses with people,
  planets and stations merged into every scenario; governors on every holding and a command to appoint them; planet
  and station kinds; five new industries.
- **Browser client** (`src/Web`): a Blazor WebAssembly build of the game — creation, Court, Galaxy map, Dynasty tree,
  Chronicle, saves in the browser — published to GitHub Pages from `main` and linked at the top of the README.
- `praetoria qa`: the QA soak — randomised player over every scenario, determinism replay, save/load round-trip, event and
  choice coverage, unresolved text tokens, chronicle oddities, engine flags no content reads.
- Unpaid fleets lose readiness and desert past 20 credits of debt; NPC stewards sell on the exchange and adjust the tithe
  when insolvent; the exchange works without a market at half yield; forge-worlds tithe a credit.
- Marriage refuses kin within two generations; the Binder never fills a role with a child unless the role speaks of age.

### Fixed
- The First Morning now reliably opens the Academy; crisis logs name their author; a mother bears at most one child a year.


All notable changes to Praetoria. The format follows Keep a Changelog; versions follow SemVer with 1.0 = the
Build Spec's V1 ("a complete human game").

## [0.8.0-alpha] — Unreleased — "The War of the Reach"
Twenty more systems plus the first QA pass driven by the new balance simulation (243 tests).

### The Great Crisis (Milestone 6)
- The human galaxy-war as a data archetype: a clock (base rate × intensity + the galaxy's turmoil), stages with
  flags/effects/behaviours (blocs → bloc war → climax → resolution), the hegemon's bloc vs the alliance, NPC-vs-NPC
  wars, and a resolution that ends the wars and births an Imperium. Crisis-intensity knob (off/low/default/dominant).
  `bloc` condition, `joinBloc` effect; four stage scenes (Rumblings, The Blocs, The War of the Reach, After the War).
### Dynasty & character
- Family autonomy (knob): willful kin grow defiant, court your rivals, and may defect — carrying a claim to your title
  with them (*A Will of Their Own*, *The Kin Who Left*). Passed-over siblings resent the new head. Elders lose their
  edge. Children keep the name of the seat (a ruling woman raises heirs to her own house) or of the greater house.
  Suits for the protagonist's hand become *The Suit*; NPC courtship prefers matches that can bear heirs. Heir-hunting
  has a cooldown. `sex` condition; the match event excludes kin and same-sex matches.
### Economy & war
- Blockades starve and revolts empty worlds; growth halves under unrest. Refit at home for materials. NPC houses buy
  materials at the Exchange and raise megastructures when rich. Threat is weighted by absolute influence. Counter
  recovery rules (imperial legitimacy and unrest heal between crises). Two more honors (Peacemaker, Lord Protector).
### Advisors, Director, tooling
- Advisor reports each turn (Steward, Admiral, Chancellor) — the Admiral speaks from intel, not truth. Director keeps
  ambient beats to one when weightier reports wait. `praetoria simulate` runs many seeds and tabulates wars, battles,
  conquests, dynasty deaths, thrones, crises, births, plots. Save format v2 with a migration hook. The Old Lion: a
  sixth scenario that starts with three generations in play. `docs/PROCESS.md` records the development cycle.
### QA (from the simulation)
- The circling heir had no Reach to circle in (added House Thalen) and her line died out (heirs now keep the seat's
  name); cadet events fired for grown nobles (now gated to a young protagonist); Cruel enemies hunted heirs every
  season (cooldown).
### UI (compiles; unrun)
- Great-Crisis card, advisor lines atop the briefing, refit buttons, defiant-kin notices, bloc rings on the map,
  intensity and family-autonomy knobs at creation.

## [0.7.0-alpha] — "Blood and Iron"
Twenty-odd systems in one autonomous pass. Everything headless is tested (224 tests); the UI additions compile.

### Military
- Ship classes as data (corvette → battleship; attack/defense/cost/upkeep; title-rank and specialization gates);
  fleet composition; losses fall screen-first. Admirals (assign command; skill and traits multiply power; victory
  teaches, defeat wounds or kills). Veterancy. Sieges with a blockade clock that halves yield. War score →
  tribute and peace refusal; war weariness. Titles follow the land (a landless house loses its rank; the conqueror
  gains a claim). Merge fleets.
### Dynasty
- Children named from house cultures; coming of age with inherited traits, a track, skills and an ambition.
  Succession laws (primogeniture, ultimogeniture, seniority, merit). Fostering hides kin from events and knives.
  Assassination with odds, trails and consequences; NPC heir-hunting in war. Marriage proposals (claims from
  marrying up); NPC courtship. Real regents. Resentment of the risen (`parvenu`).
### Strategy & endgame
- The Exchange, tax policy, territory contiguity, megastructures (title-gated), the difficulty knob. Proclaim or
  seize the Imperium (era flip, office fleets pass, the deposed house marked). The saga epilogue. Director: one
  set-piece per briefing, ambition-weighted events, lull-driven complications.
### Character
- Honorifics & Orders (five, condition/effect lists). Nature-trait friction: stress for acts against your nature.
### Content
- 17 new events (90 total): coming of age, the regent, the old blood, the knife turned, the plot exposed, the
  great work, the butcher's bill, the conquered world, the lost world, the envoy, the proclamation, the wedding
  feast (a Bloody Event), and more; 14 events tagged with ambitions; two name cultures; six backgrounds.
### UI (compiles; unrun)
- Shipyard per holding by class; command and merge of fleets; siege clocks on the map and in the Admiral panel;
  tax, exchange, succession law, throne and intrigue actions; foster/recall; dynasty tree overlay; difficulty at
  creation; the epilogue card.

## [0.6.0-alpha] — "the Court opens"
### Added
- **Godot layer (Milestone 3, first draft):** `GameRoot` (top bar, Court/Galaxy toggle, End Turn, Save, Load),
  `CreationScreen`, `CourtView` (advisors · briefing feed · dynasty rail / crisis clock / chronicle),
  `GalaxyView` + `GalaxyMap` (drawn node-and-lane map, territory by accent, click to inspect),
  `CourtTheme` (era-reactive palette). Compiles against Godot.NET.Sdk 4.3; **not yet run in an editor**.
- **Galaxy model**: `/content/galaxy` (systems + lanes), `World.Galaxy`, holdings placed in systems in every
  scenario, derived territory, neighbours/hops, `GalaxyProjection`; `map` command.
- **Character creation**: `/content/traits` (vocabularies as data), `/content/backgrounds` (six origins),
  `CharacterCreation` with validation; `create` command and `play --name/--sex/--background/--nature/--aptitude/--track/--ambition`.
- CI compiles the Godot layer.
- **Fleets (first layer)**: `Fleet`/`FleetIntel` state, `FleetSystem` (transit, readiness, upkeep, incursions, intel),
  `MoveFleetCommand` / `RaiseFleetCommand`, `/content/fleets/rules.json`, scenario `fleets`, `fleetStrength`
  condition, NPC houses raise and sail, `GalaxyProjection.Fleets`; two fleet events (*Unfamiliar Sails*, *The Review*).
- **`CourtProjection`**: the Godot-free, tested model of everything Court mode shows; `CourtView` renders it.
- UI: Admiral panel, ship-laying buttons, fleet glyphs and movement orders on the map, serif/sans typography,
  portrait badge slot. NPC action pools gain one Treasury point a turn (fleet orders).
- **War & combat**: `War` state, `DeclareWarCommand` / `SueForPeaceCommand`, `atWar` condition, `declareWar` /
  `makePeace` effects; `FleetSystem.ResolveBattles` (hulls × readiness, defender + fortress/battery `defense`,
  seeded roll, losses, retreat) and `ResolveConquests` (an unopposed fleet in enemy space takes its holdings,
  `seizures`+1 for the player); NPC houses declare war when hostile and stronger, sue for peace when broken
  (the player answers through *The Envoy*). Four events: *The Butcher's Bill*, *The Conquered World*,
  *The Lost World*, *The Envoy*. War/peace buttons in the Court (Admiral) and on the map.

## [0.5.0] — 2026-11-07 — "headless complete"
See `docs/RELEASE_0.5.md` for the full notes.

### Added
- Save/load (`SaveSystem`, versioned envelope; `play --save/--load`).
- `track` and `era` conditions; `revokeClaim` and `succeed` effects; `{role.rankName}` token.
- Career ladders as data (`/content/careers`) with rank caps.
- Crisis `once` and `onResolve`.
- Stress bleed-off and break point (`broken`, `breakdowns`); regency flag; ironman vs continue mode (`--continue`).
- Population → Manpower with per-specialization ceilings; NPC houses invest in their domains.
- Scenario `the_imperial_court` (Imperium start); starting `flags` on scenario characters; married casts.
- 14 events: *The Favor Called*, *The Slighted Kinsman*, *The Frame Unravels*, three Imperial-court events, eight "echoes".
- CI runs the Python pre-flight validator; both validators reject events with no ungated choice.
- `version` command; `World.IsDynastyDead`; relationship index.

### Fixed
- Career events fired for the wrong track; ranks ran past the ladder top.
- Marriages authored from one spouse never produced heirs.
- The run continued after dynasty death.
- Four events had every choice gated and were re-offered forever.
- `press_the_claim` never spent its claim; `the_abdication` demoted the player's own house.
- `first_case/rule_by_favor` cleared `owes_you` instead of arming it; `the_marked_man/finish_it` killed no one.
- `realm_unstable` / `civil_war_active` never cleared.
- Content guide drift; duplicate upload artifacts at the repo root.

## [0.4] — 2026-06 — engine branch (pre-versioning)
Milestones 1, 2, 4 (economy), 5 and the dynasty lifecycle, headless. See `docs/MILESTONE_*_NOTES.md`.
