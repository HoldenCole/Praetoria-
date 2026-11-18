# Praetoria — Gap Analysis & Hardening Pass (2026-11-07)

A full read of the design docs, the engine, the content and the tests, measured against the GDD and the
Build Spec. Three parts: **what was wrong and is now fixed**, **what is missing** (ranked), and **what to
build next**. Everything in part 1 is committed with tests; parts 2–3 are the honest to-do list.

Verification at the end of the pass: `dotnet test` 123/123 · engine `validate` 65/65, 0 errors ·
`validate_praetoria.py` 0 errors · all four scenarios auto-play 40 turns on seeds 1/2/3/7/42 without a
crash · save → load → continue reproduces an uninterrupted run exactly.

---

## 1. Defects found and fixed in this pass

### Engine
| # | Defect | Fix |
|---|---|---|
| 1 | **No way to gate an event on career track.** The vocabulary had `rank` but no `track`, so every career-arc event (a first fleet command, the governor's seat, the first case) was eligible for *any* protagonist of sufficient rank — a magistrate was receiving fleet commands in the circling-heir scenario. | New `track` condition (`Conditions.cs`, parser, both validators); all 11 track-specific career events gated. Test: `AuthoredCareerEvents_DoNotFireForTheWrongTrack`. |
| 2 | **Career rank was an uncapped integer.** The repeatable *Field Promotion* pushed the risen officer to rank 17 by turn 38; the Vocab doc's ladders existed only as prose. Sphere influence (derived from rank) inflated with it. | Career ladders are now data (`/content/careers/careers.json`, `CareerCatalog`, `CareerLoader`); `advanceCareer` caps at the ladder top; `{role.rankName}` names the rung; the harness shows "Lieutenant (rank 2)". The two repeatable promotions also gate on `rank lt 6`. |
| 3 | **A marriage authored from one spouse produced no children.** The match event writes `self→bride`; the birth roll only read edges *leaving the mother*, so a male protagonist's marriage was invisible to the dynasty system. There was also no check that the "father" was male. | `addBond marriage` writes both edges; `DynastySystem.Spouse` reads either direction; births require a male father (a same-sex marriage is an alliance, not a bloodline, and draws no RNG). |
| 4 | **The run continued after dynasty death.** `dynasty_dead` was set but nothing read it; the harness kept firing events for a dead protagonist for the remaining turns. | `World.IsDynastyDead` / `DynastyDeadFlag`; `play` stops and prints the end-of-line banner. |
| 5 | **No save/load** (BuildSpec §3/§7 requirement, README gap). | `SaveSystem` — versioned JSON envelope around the `World`, enum names not ints, computed lookups `[JsonIgnore]`d. `play --save/--load`. Round-trip test proves a resumed run equals an uninterrupted one. |
| 6 | **Crises could not be one-time, and could not un-arm what they armed.** `realm_unstable` and `civil_war_active` were set on onset and never cleared (a known issue in the dynasty notes). | `CrisisDef.Once` + `crisis:<id>:spent` marker; `onResolve` effects applied when a crisis subsides. Civil and coalition wars now clear the realm's instability when they end; the revolt's *concessions* damper does too (crushing it deliberately does not — that is the cascade). |
| 7 | **An event whose every choice is gated is re-offered forever.** Unresolved briefing items are never marked fired; four events (`first_case`, `the_great_trial`, `the_humbled_house`, `press_the_claim`) had `requires` on every choice, so *The Great Trial* surfaced ten times per run for a character with none of the traits. | Both validators now reject all-gated events (error); each of the four got an ungated default choice with prose. |
| 8 | **No way to spend a claim or hand over the seat.** A pressed claim stayed pressable (the repeatable `press_the_claim` kept re-firing, and would *demote* a house that had climbed past Count); `the_abdication` set the house's title to Duke then to Count — demoting your own house. | New `revokeClaim` and `succeed` effects; `press_the_claim` spends the claim and gates on `title_rank lt 3`; abdication hands the seat to the heir and the House keeps its rung. |

### Content
| # | Defect | Fix |
|---|---|---|
| 9 | `first_case/rule_by_favor` **cleared** `owes_you` on the defendant ("collect the debt judges learn to collect") — the flag was armed once in 65 events and never read. | Now arms it; new event `the_favor_called` consumes it (collect / forgive / let it ride). |
| 10 | `slighted` (refused petition, withheld handover) and `framed` (the sabotage reprisal) were written and never read — dead-end chains in the handoff's own chain list. | New `the_slighted_kinsman` (mend / buy / let it harden into `feud`) and `the_frame_unravels` (brazen / confess / bury deeper → `marked_for_death`). |
| 11 | `the_marked_man/finish_it` ("end it where the lights don't reach") did not kill anyone — written before the `kill` verb existed. | It kills the enemy: the first Bloody-Event beat in the content. |
| 12 | The three rise-path scenarios were **landless / legitimacy 0 / unsexed**, so the title soft-lock, claims, marriage and births were all inert in them (README noted this). | Seeded: Voss holds a Baron title on legitimacy 12 (instant soft-lock drama), Harrow a County, Serval a secure County, Vane landless/5, Calderon a Dukedom; every character sexed. |

### Repo / docs / tooling
| # | Issue | Fix |
|---|---|---|
| 13 | `CONTENT_GUIDE.md` had drifted badly: §10 still listed spheres, crises and marriage as "not loadable"; §6/§7 catalog and scenario lists were stale; `bond` omitted `marriage`; §8 said the vocabularies were unlocked. | Reconciled with the code; new §4d (crises), §4e (careers), §4f (saves); the ungated-choice rule; dated. |
| 14 | The Python pre-flight validator — the stricter gate — was not run in CI. | Added as a CI step. |
| 15 | A stale `praetoria_content.json` bundle (62 events, 3 scenarios, no crises) sat at the repo root duplicating `/content`. | Removed; `/content` is the one source. The validator's bundle mode remains for handoffs. |
| 16 | `main` only holds the uploaded design docs and three generations of duplicate `praetoria_content (N).json` / `validate_praetoria (N).py` files; all engineering lives on a feature branch. | Not changed here (a branch decision for the owner) — see part 3. |

---

## 1b. Closed by the v0.5 pass (same day)

Items 4–8, 12 and 13 of the list below were addressed in the 0.5 release: stress bleed-off + break point
(`broken`, *The Breaking Point*); population feeding Manpower with per-spec caps; continue mode (sworn-house
adoption) alongside ironman; a `regency` flag for child heads; two scenarios starting married; the `era`
condition and the `the_imperial_court` Imperium start with three court events; an O(1) relationship index;
eight "echoes" events consuming every seeded marker flag; NPC houses investing in their domains. The list
is kept as written for the record — see `RELEASE_0.5.md` for what remains after it.

## 2. Gaps that remain (ranked by how much they block a playable game)

1. **No UI (Milestone 3).** The game is not playable without the console. The Godot `/src/Game` project is a stub. Needs a session with Godot and a display.
2. **No Galaxy mode, no fleets, no military unit model.** The military rise path has no "take it" mechanism beyond the `Seize` command's rank check; `Holding.SystemId` is unused.
3. **No Great Crisis / era flip / epilogue (Milestone 6).** `World.Era` exists but nothing flips or reads it; the saga epilogue is a `History` dump.
4. **Stress has no consequences.** It only rises (0–100) and nothing relieves it or fires on it; long runs pin the protagonist at 90+. The GDD §18 "stress-bearing nature traits" loop needs a breakdown/relief design.
5. **Population is decorative.** Holdings grow population forever and nothing reads it; Manpower yield comes from the spec alone. The GDD's population→manpower→unrest chain is half-wired.
6. **Ironman vs continue mode** (§14) — dynasty death ends the run; the "adopt a sworn house" continuation is not built.
7. **Scenarios start unmarried**, so the first generation depends on `the_advantageous_match` firing (it did in most 40-turn soaks, not all). A seeded married couple in one scenario would make the dynasty layer live from turn 1.
8. **Only `fractured_stars` scenarios.** The Imperium start (§10) has no scenario and no content gating on `era`.
9. **Succession law is thin**: primogeniture by age, patrilineal only, no regency for child heirs, no contested succession as a *system*, no "scatter the seed".
10. **Sphere influence derives only from careers**; titles/marriages/standing don't feed it. Threat is coarse at tiny absolute scales.
11. **NPC AI** is three verbs plus "light a fuse"; not a strategic opponent. NPC houses never build, marry or climb titles.
12. **Relationship graph is a linear list**; fine at 4–6 characters, will need an index for larger casts (binding is events × candidates × conditions per turn).
13. **Seeded marker flags with no consumer**: `anomaly_hoarded`, `anomaly_studied`, `oath_sworn_academy`, `coalition_broken`, `debt_to_crown`, `reconciled`, `sabotaged`, `wed`. Harmless, but each is a promised payoff.
14. **Honorifics & Orders** (§13), **megastructures**, **anomalies/archaeology** — GDD slots with no loader.
15. **Content volume**: 65 events against a 40–60-event *opening act* target; the academy pool (18) repeats within one run.

---

## 3. Recommended next steps

1. **Decide the branch story.** Merge the engine branch to `main` (or make it the default) and delete the duplicate `(1)`/`(2)` uploads there; today `main` misleads anyone who lands on it.
2. **Milestone 3 — Court UI** in a Godot-equipped session. Everything built becomes visible; the headless `TurnController` API (`BeginTurn` → `Offer`/`Resolve` → `EndTurn`) is the surface it wraps.
3. **Small, high-leverage engine items** (each a day or less): stress relief/breakdown rule; population feeding Manpower yield; a married couple in one scenario; `era` condition + an Imperium scenario; relationship index.
4. **Content**: consume the marker flags; more academy depth; Bloody-Event set-pieces using `kill` + `once` crises.
5. **Then Galaxy mode + fleets**, which also unlocks the military path's real "take it".

---

## 4. Reassessment — 2026-11-07, after the web client (v0.8.0-alpha + QA/graphics/QoL cycle)

Of the fifteen gaps in §2, items 1–12 and 14 are closed (browser client; galaxy, fleets and combat; the Great
Crisis, era flip and epilogue; stress break-point; population; continue mode; married starts; the Imperium start;
succession laws, regents and matrilineal naming; sphere feeds; strategic NPC AI; relationship index; honors and
megastructures). Verification today: 246 tests · both validators clean · `praetoria qa` 24 runs (determinism replay,
save/load round-trip) clean at default and at difficulty 2 / dominant / autonomy 2 · six scenarios driven 20 turns
each in a headless browser with zero console errors.

### Ranked gaps now
1. **A landless house has no way back.** Conquest can strip every holding; nothing offers mercenary service, a fief
   by petition or marriage, or peace terms that return a world. Two of six browser runs ended that way by turn 21.
2. **Pools are too generous.** With cap 9 and regen 3/3/2 an ordinary turn never runs short, so the action economy
   (GDD §9) is not yet a constraint. Needs a tuning pass: lower cap, costs on more choices, or regen tied to standing.
3. **24 events never surface** in 24 soak runs — the coalition chain (`coalition_leader`, `coalition_broken` are
   never written by the engine), the break point (stress never reaches it), stewardship-track events (no scenario
   protagonist is a steward), the favor chain. Each needs either an engine writer or a scenario that reaches it.
4. **Engine flags content never reads**: `at_war:<house>`, `assassin:<house>`, `incursion:<house>`,
   `coalition_forming`, `crisis:<id>:active`. Each is a free hook for reactive events (the enemy's envoy, the knife
   traced home, the raid remembered).
5. **Scenario families lack parent links**, so the tree draws every seeded relative as a root; `mother`/`father` on
   scenario characters would make siblings, aunts and grandparents read as such from turn one.
6. **Courtship has no cooldown**: a refused suitor asks again two turns later.
7. **The seat passing is silent** in the UI; the heir simply appears next turn. An interstitial — who died, who
   inherits, what changed — is the dynasty's signature moment and should be staged.
8. **No first-turn guidance**: the title screen's one line is the whole tutorial.
9. **Era theming** (GDD §19): the Imperium looks like the Fractured Stars.
10. **Godot layer** drifts further from the web client every session; keep it compiling, or retire it.

### Where the next forty come from
Landless recovery (events, petition for a fief, peace terms, mercenary contracts) · pool tuning · engine writers for
the coalition chain and reachable breakdowns · a stewardship-start scenario · reactive events on the engine flags ·
scenario family links · courtship cooldown · succession interstitial · first-turn guidance · era theming · more
content (the pool is 97 events; the target for V1 is several hundred).


## 5. Reassessment — 2026-11-08, after the UI phase and the economy depth pass

Every screen stands on a lit 3D set (office, galaxy, academy, senate, hall, command deck, keep, hall of banners, orrery,
archive, quarters). The economy now has prosperity, belts and claims, ventures with charters, loans, and house wealth
reckoned and ranked (`docs/ECONOMY.md`). Gap 1 from §4 (a landless house has no way back) is half-closed: a landless
house can stake a claim anywhere and found a venture on anyone's world; peace terms and petitions remain.

### Ranked gaps now
1. **Ventures have no risk.** Nothing burns, is pirated, embargoed or nationalised; a venture is a bond, not a bet.
   Events exist for strikes and buyouts; the engine needs a few teeth (a war seizing ventures on the enemy's world).
2. **Pools are still too generous** (§4 item 2).
3. **Governors are passive.** Prosperity reads their administration, and a hostile one skims, but no governor
   schemes, petitions, or has to be kept sweet; the "bigger deal" of the chair is still on the ledger, not in the court.
4. **Trade is not spatial.** Charters and contiguity are the only geography; lanes carry no goods and no price differs
   by region.
5. **Peace terms, petitions for a fief, mercenary service** (§4 item 1, the rest of it).
6. **24 events never surface** (§4 item 3) — unchanged.
7. **Era theming** (§4 item 9) — unchanged.

### Where the next forty come from
Venture risk and war on the ledger · governors as people · regional prices and lane trade · peace terms and petitions ·
pool tuning · the unreachable events · era theming.


## 6. Reassessment — 2026-11-08, after the rings

The galaxy is ten regions in three rings with a capital; regions carry allegiance and lawlessness; worlds rise, break
away and declare free marches (`docs/GALAXY.md`). §5 items 1 (venture risk: raiders now tax ventures on lawless lanes)
and 4 (geography: the rings) are partly closed.

### Ranked gaps now
1. **A free hold is an orphan.** It has a leader and a world and nothing else: no fleet, no ventures, no ambitions, no
   way to be reabsorbed or courted. Free holds and free marches should be able to be bought back, married back, or
   conquered, and should trade among themselves.
2. **The capital is only a ledger line.** Holding it should bring the Senate's chair, a say in titles, and a target on
   the holder's back; losing it should be the stuff of chronicles.
3. **Lawlessness has no face.** Raiders are a tax; a pirate fleet on the map that a house can hunt (or hire) would make
   the rim legible as a place.
4. **Governors are passive** (§5 item 3) — now sharper: a governor of another house is the natural leader of a
   breakaway, which makes the appointment a real gamble, but nothing in the court plays on it yet.
5. **Pools are still too generous** (§4 item 2).
6. **Peace terms, petitions for a fief** (§4 item 1).
7. **24 events never surface** (§4 item 3); **era theming** (§4 item 9).

### Where the next forty come from
Free holds as actors · the capital as a prize · pirate fleets · governors who scheme · pool tuning · peace terms ·
the unreachable events · era theming.


## 7. Full gap QA — 2026-11-08

Method: 285 tests · validator · `praetoria qa` · `simulate --scenario all --seeds 6 --turns 60` · a twenty-turn
browser drive through all ten screens with save/reload (0 console errors) · an economy probe printing the poorest
houses at turns 11, 31 and 60.

### Found and fixed this pass
- Rival stewards founded ventures alphabetically (Academy Patronage, a drain) and without a profit test; borrowed on a
  negative ledger every seven turns; never sold materials under fifteen; a headless house let its worlds rot. Fixed
  (profit-ranked founding, sell before borrowing, borrow only when positive, smaller material lots, thinner cushion for
  landless or yield-less houses, a cousin for a headless rival house). Mean credits at T60: 14 → 99; houses in debt 7 → 1.
- The auto-player never wed, so the fallen house ended every sixty-turn run. Fixed in the harness.
- Engine flags no content read (`at_war:*`, `assassin:*`, `coalition_forming`): three reactive events and two generic flags.
- The Houses screen offered marriage and assassination against infants (a 0-year-old "match", an "accident" for a
  5-year-old). Hidden in the projection and refused in the command.

### Still open, ranked
1. ~~Fortress worlds have no income~~ — closed: escort fees from peaceful neighbours (up to three credits a turn).
2. ~~Free holds are orphans~~ — closed: a militia, a steward that acts, buy-back and war for the old lord, both for NPCs.
3. ~~The capital is only a ledger line~~ — closed: the Senate's chair, legitimacy, prosperity, threat, rivals that
   declare for it, and the chronicle marking its passing. **Lawlessness has no face** (§6 item 3) and **governors are
   passive** (§6 item 4) remain.
4. ~~The throne falls in every run by turn 60~~ — closed: the throne stands in the capital; the War of the Reach
   crowns only a decisive victor who holds it, else ends in exhaustion.
5. **Wars are frequent and bloodless**: ~30 declarations per 60 turns, under two battles. Fleets rarely meet because
   NPC sailing is mostly garrison duty now; the chokepoints give a reason to sail that the AI does not yet use.
6. **Pools are too generous** (§4 item 2); **peace terms, petitions for a fief** (§4 item 1); **24 unreachable events**
   (§4 item 3); **era theming** (§4 item 9).

### Where the next forty come from
Fortress income · free holds as actors · the capital as a prize · pirate fleets · governors who scheme · the throne as a
climb · NPC fleets that contest chokepoints · pool tuning · peace terms · the unreachable events · era theming.

## 8. Reassessment — 2026-11-08, after the depth cycle (raiders, governors, wars with ends, the fief, houses that fall)

Method: 305 tests · validator · `praetoria qa --seeds 6` with the new acting player (36 runs, 101/120 events, 0
failures, 0 token issues) · `simulate --scenario all --seeds 6 --turns 60` · the economy probe · a browser drive
through every screen with save/reload and the map-cycle captures.

### Found and fixed this pass
- **The QA soak could not act.** Its player only answered events, so twelve player-economy, fleet and court events
  were reported as never firing when the harness simply had no verbs. It now lists every legal command each turn and
  tries a shuffled few; the "never fired" list fell from 30 to 19 and now names real gaps.
- **"Insolvent" meant "a name with nothing left."** A third of the red ledgers at turn sixty were landless, often
  headless houses frozen at the credit floor (the Free Cinderfall twice over, Valmont a count with no world). Houses
  now fall: struck from the rolls, folded into whoever took their world, or entering another's service after twelve
  years. Insolvent at T60: 13–16 → 4–7 of ~33.
- **The fief was dead content.** Its grantor was the court, and the court holds only the capital in the Reach. The
  greatest landed house at peace now grants when the court cannot, and landless rivals court the grantor first; one or
  two fiefs are granted per sixty turns and *The Fief* can fire.
- Fortress free holds were broke by construction (no yield, no neighbours): a garrison credit and a levy. Rival stewards
  staked a belt claim every turn they could afford one (Pike: six claims, no coin): capped at two unless the ledger is
  in the black. The Lawless were running up a debt on hull upkeep. Two academy set-pieces had an unreachable age window.

### Still open, ranked
1. **Thrones fall too readily**: 19 of 36 sixty-turn runs end with an Emperor, most by the War of the Reach's decisive
   victor. The capital gate holds; the crisis itself resolves too often in one bloc's favour. Tune the decisiveness bar
   or let a stalemate be the common ending.
2. **Nineteen events never surface**, now for real reasons: *The Governor's Revolt*, *Whispers of Secession* and *The
   Terms of Peace* need the player's own world to turn or the player to win a war by ten, which a random player rarely
   does; the rest are deep chains (*The Regent's Hand*, *The Plot Exposed*, the stewardship audit). Worth a scripted
   playthrough per chain rather than more randomness.
3. **Reclaiming a free hold never happens for rivals** (`reclaimed` 0–1): the war path is cheaper than the price. Either
   the price should fall with the leader's years in the chair, or war on a free hold should cost legitimacy.
4. **Early-turn insolvency**: half a dozen landed houses dip below zero in turns two to five before ventures pay. A
   starting cushion of five credits per world, or a first-turn grace on upkeep.
5. **Wars still end in exhaustion more than terms** (battles 6–11 per run, conquests 3–5): acceptable, but the AI never
   sues for peace from strength to bank a cession.

### Where the next forty come from
The throne as a climb (decisiveness, coalitions that hold) · scripted chain playthroughs for the deep events · the
free hold's price and grudge · a starting cushion · peace from strength · the Senate as a place where fallen houses
are spoken for · the chronicle of fallen names on the Houses screen.

## 9. Reassessment — 2026-11-08, after the strategic three

Method: 312 tests · validator (126 events) · `praetoria qa --seeds 3` with the acting player (18 runs, 0 failures) ·
`simulate --seeds 4 --turns 60` · the economy probe, extended to fuel, producers, deposits, hull totals and tiers · a
browser drive through every screen with save/reload and the map-cycle captures.

### Found and fixed this pass
- **Characters born in play had no action budget.** `World.PoolsFor` gave an unknown actor an empty pool with a cap of
  zero, so a rival house whose founding head died could never again raise a hull, sail, buy or build. Surfaced by the
  fuel probe ("no treasury action left" on houses with a thousand credits). Every character now gets a rival's budget.
- **Stewards ate their own manpower.** Free companies cost men a turn; rich houses founded two and could never lay a
  keel again (manpower 0, credits 1,900). Ventures that eat men are founded or expanded only while the worlds breed more.
- **Fuel deadlocks**: a house with no navy never bought its first tank; a dry navy stayed dry; procurement took the one
  Treasury action a rival has. Fuel is bought first and pool-free, a dry navy is laid up a hull at a time, and the yards
  lay down no hull the tanks could not carry three turns.
- **Inflation.** Hoards sold at the exchange, a skimmer yielding six a turn, and an unbounded hull sink removed: mean
  credits at turn sixty went 120 → 1,900 → 450 → 290–350 after trimming yields, sell rates and the two richest ventures.
  Still above the pre-batch 130–210; see below.
- **Wealth tiers were absolute** (26 of 32 houses "opulent"), which made *The Arms Broker*'s wealth gate meaningless.
  Tiers are now relative by rank: 7–8 poor, 11 comfortable, 7–8 rich, 5–6 opulent in a Reach of 32.

### Still open, ranked
1. **Thrones fall too readily** (14 of 24 runs; unchanged from §8).
2. **The ledger runs rich**: mean credits 290–350 at turn sixty, median 17–276. The remaining sinks are hulls (capped at
   ten plus six a world) and ventures (sixty a Reach). A court tithe to the capital's holder, upkeep that scales with
   a house's venture count, or prices that rise with the Reach's money supply are the candidates.
3. **Heavy hulls are rare** (6–11 across the Reach): rare earths flow (nine producers) but stewards buy them only for the
   class they are about to lay down. A standing stock target would put cruisers in rival navies by mid-game.
4. **Nineteen events never surface** (§8 item 2, unchanged) and **rivals never buy back free holds** (§8 item 3).
5. **Isotopes have three producers and no trade**: by design never on the open market, but no rival sells them to an
   ally either; a fuel-contract-style event between houses at peace would make them a diplomatic currency.

### Where the next forty come from
Credit sinks (tithe, venture upkeep, prices) · a rival stock target for rare earths · isotopes as diplomacy · the throne
as a climb · chain playthroughs for the deep events · the free hold's price · and the first pass at the Senate as a
market for the strategic three (embargoes, quotas, the inspectors).

## 10. Full gap analysis — 2026-11-08, the game against the design

Method: 312 tests · both validators · `praetoria qa --seeds 6 --turns 60` with the acting player (36 runs, 0 failures,
109/126 events surfacing) · `simulate --seeds 6 --turns 60` · the economy probe · a thirty-turn browser playthrough of
*The Fallen House* read screen by screen against GDD §19 · the GDD's pillars (§5–§8, §13–§19) read against the engine,
the content and the client. Where §4–§9 reassessed one cycle, this section asks the whole question again: how far is this
from the game the design document describes, and what blocks a stranger from enjoying it tonight?

### The numbers

| measure | value | reading |
|---|---|---|
| tests · validators | 312 · 0 errors | the engine is trustworthy; CI was red only because the pre-flight validator's resource vocabulary predated the strategic three (fixed this pass) |
| events surfacing in 36 runs | 109 of 126 | content is reachable; 17 never fire for real reasons (deep chains, player wars won by ten) |
| battles per 60 turns | 3–7 | wars have teeth again after the fuel rebalance |
| thrones taken | 24 of 36 runs | the Imperium forms in two runs of three by turn sixty — too often (§8, §9) |
| generations per 60 turns | 1.0–1.2 (2.5 for the Old Lion) | one succession a run; the three-generation arc needs 120 turns or faster time |
| mean credits at T60 | 290–350 | still rich (pre-strategic 130–210); tiers now relative so it does not break the gates |
| dynasties ended | 5 of 36 | the fail state exists and is rare, as §14 wants |
| chronicle oddities | early-turn negative treasuries only | rival houses dip below zero in turns 2–5 before ventures pay |

### Pillar by pillar

**Political / Court (GDD §6.1, §7).** Spheres, finite influence, threat and coalition pressure, titles with legitimacy
requirements, three rise paths, honours, the throne (proclaim or seize), the capital as the Senate's chair, friction for
the parvenu, dampers on crises. **Missing:** the Senate as a *place of action*. There are no motions, votes, blocs or
legislation; a Magistrate with the ambition "Master the Senate" finds a screen that shows standings and offers nothing
to do. Plots are one verb (an accident); there is no blackmail, no forged claim, no favour economy beyond disposition.
The political pillar is the shallowest of the three against its design.

**Military / Naval (§6.2, §8).** Fleets by class, readiness, veterancy, admirals, intel with confidence, sieges, battles
with defender bonus and fortress worlds, wars that end (exhaustion, cession), chokepoints, raiders, fuel, heavy hulls
gated on strategic resources, office fleets that pass with the office. **Missing:** the §8 drama — the player never
*commands* an Imperial fleet they do not own; office fleets exist only as the Emperor's. No fleet doctrine, no
blockade-as-order, no mercenaries, and the player's first fleet in *The Fallen House* runs dry by turn nine unless they
read the alert (the starting tank is raised this pass; the Admiral now says so).

**Domain / Economy (§6.3, §17).** The deepest pillar: specializations, buildings, prosperity, population, unrest, ventures,
claims, loans, charters, tolls, escort fees, rings, lawlessness, governors, the strategic three, an exchange and an open
market, wealth tiers, houses that fall, the fief. **Missing:** a credit sink proportional to income (the ledger runs
rich), trade that is spatial (nothing moves along lanes), and ventures with risk. Anomalies and megastructures exist as
data slots (three megastructures, anomaly markers in events) but anomalies have no mechanic.

**Dynasty (§14, §18).** Nature and aptitude traits, stress with breakdowns, ambitions, sworn bonds, fostering (hidden
kin), marriages with matrilineal/patrilineal lines, succession laws, regency, continue mode, cousins for headless
houses, the saga epilogue, family autonomy. **Missing:** heir-hunting as a *campaign* (find, then pay, then risk — today
it is one assassination roll), defiance that costs the player something concrete, and a dynasty tree: the Hall shows a row
of portraits and a line of succession, not the generations.

**Progression (§13).** Two ladders, three paths, legitimacy as a modifier with instability counters, honours, the academy
crucible as an opening act with a cohort that persists. **Missing:** career rank gates that *matter* mid-game — ranks
unlock events but not authority (a Grand Admiral commands nothing the Captain did not); and the Merit path's "be granted
it" has one petition verb.

**Crises and the Great Crisis (§11, §16).** Twelve human crises with gates, cascades, dampers and recovery, the War of the
Reach with blocs, stages and a resolution that can crown. **Missing:** only the human archetype (by design for V1); but
the war resolves decisively too often (the throne numbers above), and the cosmic foreshadowing slot (anomaly digs that
later erupt) is empty.

**Galaxy (§19).** Node-and-lane map, heraldic territory, rings, chokepoints, deposits, raiders, intel fog on fleets,
route preview, minimap, sets per screen, era theming. **Missing:** role theming (an admiral's game should look
different from a senator's — only the era register exists), the intel ticker as a strip (alerts live in the outliner),
and crisis presence on the map beyond the front line.

**Event engine and content (§15).** 126 events, 356 choices (118 trait- or resource-gated), tiers, roles, tokens, flags,
counters, two validators, a soak that acts. **Missing:** volume. 126 events across six scenarios means the academy pool
(18) repeats within a run and a sixty-turn game sees most of the generic pool once. The GDD's "opening act" target was
40–60 events *per act*; a full arc wants three to four hundred. No event is scenario-gated; the Imperium start reuses
the fractured-stars pool with an era condition on three.

**UI and art (§19).** Three-column court, galaxy command table, ten lit 3D sets, procedural portraits and sigils, dark
warm palette with house accents, imperial gold on proclamation, outliner, chronicle with filters, save and reload.
**Missing:** first-turn guidance (the title screen's one line is the tutorial), the seat passing as a moment (the heir
appears next turn), contextual connections (the GDD's "see your tie to this house wherever it appears" exists only on
the Houses screen), audio of any kind, and the Military screen listed the Lawless twice and put the fuel line among the
rivals (fixed this pass).

**Process and QA.** Tests, two validators, a soak that acts, a simulate harness, an economy probe, a browser drive, a
changelog and a gap log kept per cycle. **Missing:** the probe lives outside the repository (`/tmp`), and nothing
measures *fun*: every number above is about the simulation, none about a player's turn-to-turn decisions.

### Found and fixed this pass
- CI red since the strategic three: the pre-flight validator's locked vocabularies lacked the new resources and the
  `wealth`/`deposit` conditions.
- The Military screen listed the Lawless as a rival navy *and* as raiders, and showed the fuel line under rival navies;
  an unled fleet read "unled". The Admiral's report said nothing about dry tanks; the starting tank in a scenario lasted
  nine turns.

### Ranked gaps — what blocks a stranger from enjoying it tonight
1. **No first-turn guidance and no seat-passing moment.** A new player lands in a lit office with three cards and no
   idea what the pools, the ledger or the War of the Reach clock mean; when they die, the heir simply appears. An
   onboarding briefing (five cards, dismissable) and a succession interstitial are the two cheapest large wins left.
2. **The Senate is a display, not a pillar.** Motions, votes weighted by Senate influence and titles, blocs that form
   around them, and a Magistrate's career that climbs through them. This is the political pillar's "take it".
3. **Thrones fall in two runs of three.** The War of the Reach's decisiveness bar, or a stalemate as the common ending.
4. **Content volume.** 126 events; a sixty-turn run exhausts the generic pool. Fifty more situations across the three
   pillars and the first scenario-specific chains.
5. **The office-versus-house drama (§8) is absent.** The player never commands fleets they do not own; appointments to
   office (Admiral of the Reach, Treasurer, Consul) with their resources and their loss would make careers matter.
6. **Heir-hunting and plots are one verb.** Find, then pay, then risk — and blackmail, forged claims and favours.
7. **The ledger runs rich** (mean 290–350 credits at T60). A sink proportional to income.
8. **Role theming** (admiral / steward / senator chrome) — the GDD's second register of reactive UI.
9. **A dynasty tree** over the generations, and the contextual ties on every rival's card.
10. **Trade that is spatial** and ventures with risk.
11. **Seventeen events never surface**; chain playthroughs rather than more randomness.
12. **Audio.** None. Even a hum, a page, a gong on End Turn.

### Where the next forty come from
Onboarding and the seat-passing moment · the Senate as a place of action (motions, votes, blocs) · the throne as a climb
· fifty situations · offices and their fleets · plots with steps · a credit sink · role theming · the dynasty tree ·
spatial trade · chain playthroughs · a first sound.

## 11. Reassessment — 2026-11-08, after the Senate and the onboarding

Method: 318 tests · both validators (132 events) · `praetoria qa --seeds 3` (18 runs, 0 failures, 100/132 events) ·
`simulate --seeds 4 --turns 60` · the probe, extended to motions · a browser run through the primer to the docket.

### Done this pass (GAP §10 items 1 and 2)
- **The Senate as a place of action** (docs/SENATE.md): six motion kinds, votes by weight, leans by interest, whipping
  and bribery, rivals who move their own business (about twenty motions a run, a third to two thirds carried), standing
  that feeds the Senate sphere, six events, the docket on the Senate screen with every bench's lean. The first pass
  showed the same house moving the same motion a dozen times; a ten-turn cooldown fixed it.
- **Onboarding**: a five-card primer on turn one, remembered per browser, reopenable from the top bar. **The seat
  passing**: an interstitial with both portraits, what passes, what does not, who the heir is, and a regency note.
- The Chancellor reports the docket and the embargo; the Houses cards show Senate weight and motions carried; the
  Dynasty hall lists the seats before; the epilogue remembers the chamber.

### Still open, ranked (from §10)
1. **Thrones fall in two runs of three.**
2. **Content volume** — 132 events now; the Senate added six, and the War of the Reach still has four.
3. **Offices and their fleets** (§8 of the GDD) — the Senate could *grant* them: a Commission motion is the natural next kind.
4. **Plots with steps**; **the ledger runs rich**; **role theming**; **a dynasty tree**; **spatial trade**; **audio**.
5. **The Senate's own next steps**: blocs that persist (houses that vote together three times become a faction with a
   name), the chair's casting vote, a Magistrate's career climbing by motions carried, and the player as the whip's
   target by name rather than by flag.

### Where the next forty come from
A Commission motion that grants an office fleet · persistent factions · the throne as a climb · fifty situations ·
plots with steps · a credit sink · role theming · the dynasty tree.

## 12. Reassessment — 2026-11-08, after the offices of the Reach

Method: 324 tests · both validators (137 events) · `praetoria qa --seeds 3` (18 runs, 0 failures) · `simulate --seeds 4`
· the probe, extended to offices, office hulls, commissions and rival career ranks · a browser run to the Senate screen.

### Done this pass (GAP §10 item 5, §11 item 3)
- **The offices of the Reach** (docs/SENATE.md): the Admiralty (command of the office fleets — the Reach Squadron at the
  capital, the Imperial Home Fleet under the throne — paid and fuelled by the state, lost for treason the turn the
  holder makes war on the chair), the Treasury (a stipend from the chair), the Speaker (weight and cheaper motions);
  granted by a Commission motion to a house with a member of the rank; the Emperor holds every office by the throne;
  five events including *The Admiral's Temptation*.
- **Found on the way: rival careers never advanced.** Three hundred rival adults sat at rank zero for sixty years, so the
  spheres, the Senate's weights and the offices were effectively the player's alone — one house in the Reach qualified
  for the Admiralty. Every adult outside the player's seat now climbs a rung every nine turns (heads every six). After
  it: fifteen to twenty-three houses qualify, four to nine commissions a run are moved, one to six granted, the
  offices change hands between four or five houses in sixty turns, treason happens, and Senate votes weigh twice what
  they did. A house at war with the chair cannot be commissioned to the Admiralty (it was, and was stripped the same turn).
- The integration test's "a crisis arose and cleared" rode on a seed; the Senate and the offices now settle a weak title
  before it festers, so the test ignites a crisis deliberately and asserts it de-escalates.

### Numbers worth noting
- Thrones taken fell to 3 of 24 runs (from 16): the chair's four office hulls and treason-for-war-on-the-chair make the
  capital a harder prize. This is close to where §8 wanted it; watch that it does not fall to zero.
- Battles fell with it (1–4 a run). The Admiral's house now fights with the state's hulls, but rivals strike the chair
  less; the next balance pass should ask whether wars find other fronts.

### Still open, ranked
1. **Content volume** (137 events). 2. **Plots with steps.** 3. **The ledger runs rich** (mean 280–340). 4. **Role
theming.** 5. **A dynasty tree.** 6. **Spatial trade.** 7. **Audio.** 8. **Persistent factions** in the Senate, and the
player as the whip's target by name.

### Where the next forty come from
Fifty situations across the pillars · plots with steps · a credit sink · role theming · the dynasty tree · factions.

## 13. Reassessment — 2026-11-08, after the benches

Method: 329 tests · both validators (142 events) · `praetoria qa --seeds 3` · `simulate --seeds 4` · the probe, extended to
benches and affinities · a sixteen-turn browser run to the Senate screen.

### Done this pass (GAP §12 item 8)
- **Persistent factions** (docs/SENATE.md "The benches"): affinity from every decided vote, benches that form around the
  weightiest of three friends, grow, break and dissolve on their own, named by the chamber; discipline (a member leans
  toward its leader, a whip on the leader is half a whip on the bench); the player takes a seat, leaves, founds a bench,
  or breaks the whip and pays. Three benches had formed by turn sixteen in the browser run; by turn sixty the probe shows five
  or six benches of three to nine seats — nearly every house sits somewhere, and discipline turns majorities into
  landslides (104 to 5). The first tuning item is to let affinity decay a point every few turns so only the benches that
  keep voting together survive.
- **Found on the way:** the offices' one-turn flags were cleared by the office tick right after a motion had set them,
  so *The Commission* and *Relieved of Office* could never fire. All Senate, office and bench flags are cleared once,
  before motions resolve.

### Still open, ranked
1. **Content volume** (142 events). 2. **Plots with steps.** 3. **The ledger runs rich.** 4. **Role theming.** 5. **A
dynasty tree.** 6. **Spatial trade.** 7. **Audio.** 8. **The Senate's next**: benches that move motions as a bench (a
leader's motion carries the bench's name), the chair's casting vote, and the Great Crisis blocs reading the benches.

### Where the next forty come from
Fifty situations · plots with steps · a credit sink · role theming · the dynasty tree · benches in the War of the Reach.

## 14. Reassessment — 2026-11-08, after the laws and the estates

Method: 334 tests · both validators (148 events) · `praetoria qa --seeds 3` (18 runs, 0 failures, 110/148 events) ·
`simulate --seeds 4` (unchanged: battles 2–7, thrones 9 of 24) · a browser run that set two laws and watched the Commons cool.

### Done this pass (the owner's ask: politics with laws and groups)
- **Laws by position** (docs/POLITICS.md): seventeen laws in seven categories, each with the position it needs — head of
  house, holder of a world, dominant house of a region, the chair, an office, the throne, the leader of a bench — and
  three options whose effects one modifier table carries into the economy, the regions, the fleet, the Senate and the
  open market. The tithe is a law now.
- **Six estates** with an approval that drifts toward what the laws say and that events can cultivate for a while; loyal
  or hostile, their effects join the house's. Six events let them speak. A Chancery screen shows both.

### Still open, ranked
1. **Rivals have no estates and keep their defaults.** The GDD abstracts rivals, but a rival whose tithe is harsh should
   have a restless world or two; a cheap pass would give each rival house one law off default by culture, and read the
   Commons' effect on its unrest.
2. **Three estate events never surfaced** in eighteen runs (the Officers, the Old Blood, the Frontier): their gates want
   hostility the random player rarely produces on their estate; a scripted playthrough with harsh laws should check them.
3. **Content volume** (148 events). 4. **Plots with steps.** 5. **The ledger runs rich.** 6. **Role theming.** 7. **A
dynasty tree.** 8. **Spatial trade.** 9. **Audio.**

### Where the next forty come from
Rival laws by culture · the estates in the Senate (a bench's platform read by the estates) · fifty situations · plots
with steps · a credit sink · role theming · the dynasty tree.

## 15. Reassessment — 2026-11-09, after rival laws and estates

Method: 337 tests · both validators (149 events) · `praetoria qa --seeds 3` (18 runs, 0 failures, 114/149 events) ·
`simulate --seeds 4` · the probe, extended to laws enacted, houses off the customary law, and rival estates · a fifteen-turn
browser drive through every screen (0 console errors).

### Done this pass (GAP §14 item 1)
- **Laws by culture**: every house begins with its culture's laws (ten cultures in `content/politics/cultures.json`), so
  the Reach is not one chancery copied thirty times. **Estates for every house** with the same loyal and hostile
  effects: a rival with a harsh tithe and bonded labour has hostile Commons and two more unrest a turn, and its risings
  are its own doing. **Stewards legislate**: 49–66 laws a run change hands among the rivals — relaxing what a hostile
  estate hates, easing the law that drives unrest when a world is restless, tightening the tithe when poor, conscripting
  at war, martial law over lawless regions, the chair's tolls when poor, an aggressive doctrine at war.
- The Houses cards show a rival's laws off the customary and its estates' moods; *The Exiles* brings a harsher house's
  commons to your port.

### Numbers worth watching
- Rebellions per sixty turns rose from 4–6 to 8–13 and secessions with them: the harsh cultures (spirean, forgeborn,
  marcher) now earn their restless worlds. Stewards ease laws when a world passes fifty unrest, but only one law a turn
  and one house in eight; if the rim empties of houses too fast, soften the cultural defaults before touching the
  steward.
- Battles rose to 5–8 a run (conscription and the doubled guard at war); thrones 12 of 24.

### Still open, ranked
1. **Content volume** (149 events); the three estate events that never surfaced for the random player want a scripted
   check. 2. **Plots with steps.** 3. **The ledger runs rich** (mean 310–350). 4. **Role theming.** 5. **A dynasty
tree.** 6. **Spatial trade.** 7. **Audio.** 8. **The estates in the Senate**: a bench's platform read by the estates,
and motions that change a law for the whole Reach.

### Where the next forty come from
Scripted chain playthroughs · fifty situations · plots with steps · a credit sink · role theming · the dynasty tree ·
Reach-wide laws by motion.

## 16. Reassessment — 2026-11-09, after Acts of the Senate

Method: 343 tests · both validators (151 events) · `praetoria qa --seeds 6` (36 runs, 0 failures; the randomised player
moved 288 motions, Acts among them, and all three Senate laws changed hands) · the probe, extended to Acts passed and the
law of the Reach at T60 · a six-turn browser drive that moved an Act from the picker, saw it carry, and read it in the
Chancery (0 console errors).

### Done this pass (GAP §15 item 8)
- **Acts of the Senate**: three laws of scope `senate` — the Common Law, the Trade Charter, the Levy of the Reach — set
  only by motion. Rivals lean by their estates and their situation; rival proposers move what their hostile estates want.
  Two or three Acts carry a sixty-turn run; the common levy and open lanes carry most often, the assizes when the lanes
  are lawless. The estates of every house now also weigh the Reach's laws, so a chair's heavy tolls or a Senate's open
  lanes move thirty chanceries at once.

### Numbers worth watching
- The Senate's cooldown is ten turns; with three laws and two or three Acts a run, the docket is not crowded by them.
  If content adds more Senate laws, watch for Acts crowding out censures and commissions (docket size two).
- The common levy adds a point of unrest a world across the Reach when it carries; with the harsh cultures already at
  8–13 rebellions a run, a run where it carries early could tip the rim. Not yet seen in 36 soaks.

### Still open, ranked
1. **Content volume** (151 events); the estate events still want a scripted check. 2. **Plots with steps.** 3. **The
ledger runs rich** (mean 310–350). 4. **Role theming.** 5. **A dynasty tree.** 6. **Spatial trade.** 7. **Audio.**
8. **The estates as voters**: an estate with enough regard across many houses could petition the Senate itself.

### Where the next forty come from
Scripted chain playthroughs · fifty situations · plots with steps · a credit sink · role theming · the dynasty tree ·
the estates' petitions.

## 17. Reassessment — 2026-11-09, after the estates' petitions

Method: 348 tests · both validators (154 events) · `praetoria qa --seeds 6` (36 runs, 0 failures; petitions carried and
refused for the Commons and the old blood) · the probe, extended to petitions brought, heard and refused and each
estate's hostile-house count at T60 · a twelve-turn browser drive that drove the Commons hostile, saw their petition on
the docket, the alert in the outliner and the line on the Chancery's estate card (0 console errors).

### Done this pass (GAP §16 item 8)
- **Petitions**: the estates are now actors in the chamber, not only an audience. An estate hostile across an eighth of
  the Reach brings the Act it wants; the chamber's answer moves its regard in every house at once and, refused, stirs
  the worlds of every house it distrusts. One to three a run.

### Numbers worth watching
- The threshold began at a quarter of the houses and never fired in thirty-four-house runs because the stewards relax
  what their hostile estates hate within a few turns; an eighth fires one to three times a run. If content adds estates
  or harsher cultures, re-check the probe's hostile-in-N line before touching the threshold.
- A refused petition adds three unrest to the worlds of every house the estate distrusts; in runs already at 8–13
  rebellions this is a nudge, not a tip. Watch it if the Commons' petitions start failing often.

### Still open, ranked
1. **Content volume** (154 events); the estate events still want a scripted check. 2. **Plots with steps.** 3. **The
ledger runs rich** (mean 310–350). 4. **Role theming.** 5. **A dynasty tree.** 6. **Spatial trade.** 7. **Audio.**

### Where the next forty come from
Scripted chain playthroughs · fifty situations · plots with steps · a credit sink · role theming · the dynasty tree.

## 18. QA and graphics pass — 2026-11-09

Method: 348 tests · both validators (154 events) · `praetoria qa --seeds 10` (60 runs, 0 failures, 136/154 events) ·
`simulate --seeds 6` (no dynasty ended; wars 30–36, battles 6–8, thrones 17 of 36) · the full browser sweep on every
scenario (eleven screens, the Chancery now among them, save and reload; 0 console errors, no suspicious text) · CI green
on both branches, Pages redeployed.

### Graphics this pass
- **The Chancery reads its politics**: six estate tapestries on the walls, each woven in its mood's colour with a glyph
  and a regard bar, lit by a lamp that burns steady for a loyal estate and gutters for a hostile one; a brass bell swings
  beside an estate that is petitioning. The desk carries a sealed scroll for every law set off the custom and a bound
  Act once the Senate has carried one.
- **The chamber shows a petition**: the estate's delegation stands at the chamber's edge under its own standard, gold-lit;
  petition cards on the docket carry a gold edge.

### Numbers worth watching
- The headless sweep takes ten to twenty-five seconds a screenshot under software GL; that is the test rig, not the
  game, but it caps a sweep at about six turns per scenario in one sitting.
- Thrones taken rose to 17 of 36 runs in simulate; still within the 40–50% band seen since the offices, but the
  Imperium ending should not become the default. Watch it against the next tuning pass.

### Still open, ranked
1. **Content volume** (154 events); the estate events still want a scripted check. 2. **Plots with steps.** 3. **The
ledger runs rich** (mean 310–350). 4. **Role theming.** 5. **A dynasty tree.** 6. **Spatial trade.** 7. **Audio.**

## 19. QoL and bug check — 2026-11-09

Method: a read-through of the Senate, Acts and petition paths for edge cases; 350 tests (warning-free); both validators;
`qa --seeds 4` (24 runs, 0 failures); a browser drive through every change (0 console errors).

### Fixed
- A struck house's motions now leave the docket (its own lapses with a line; one aimed at it has no target). Before,
  a dead house's motion would be voted and its name shown as an id.
- The QA player no longer attempts the petition kind.

### QoL added
- Docket verdict ("Carries / Fails as the benches lean"); undeclared-vote and Act-on-the-docket alerts in the outliner
  and the Chancellor's report; "They would warm to" on every estate card; estate opinions in the Act picker; a
  *politics* Chronicle filter; a primer card on the laws and the estates.

### Still open, ranked
1. **Content volume** (154 events); the estate events still want a scripted check. 2. **Plots with steps.** 3. **The
ledger runs rich** (mean 310–350). 4. **Role theming.** 5. **A dynasty tree.** 6. **Spatial trade.** 7. **Audio.**

## 20. Content volume and the reachability check — 2026-11-09

Method: 388 tests (38 new, the reachability table among them) · both validators (180 events) · `praetoria qa --seeds 6`
(36 runs, 0 failures, 152 of 180 events reached; 21 of the 26 new situations fired without help).

### Done this pass (GAP §19 item 1)
- **The scripted check.** The soak now samples world flags at every BeginTurn and prints the flags content reads that
  no run ever raised. Against that list, every one of the twenty-two never-fired events was built by hand in
  `tests/EventReachabilityTests.cs` (flags raised, roles seeded) and is eligible. Verdict: no dead triggers; the
  silence is rarity — the levy motion mostly fails (the poor vote it down), the player is seldom commissioned, a bench
  seldom forms around a house that votes at random, cessions need a peace with terms.
- **Twenty-six situations** on flags the engine raised but nothing read: the Senate's refusals, the embargo, the
  Speaker's gavel, the bench leader's table, four loyal-estate situations, seven that make your own laws bite, the
  siege bell, the raid, the lost province, the free march, two for the strategic three, the empty hall, three
  ambients. The game now has 180 events.

### Numbers worth watching
- Events reached per 36-run soak rose from 136 of 154 to 152 of 180. The never-fired list grew in absolute terms
  (22 → 28) only because the new loyal-estate and chair-only situations are rare for a random player; each has a
  reachability row.
- The levy motion carries rarely enough that *The Levy Falls Due* has never fired in a soak. If the chair's coffers
  should matter more, give the levy a point of lean among the comfortable houses; it is a tuning knob, not a defect.

### Still open, ranked
1. **Plots with steps.** 2. **The ledger runs rich** (mean 310–350). 3. **Role theming.** 4. **A dynasty tree.**
5. **Spatial trade.** 6. **Audio.** 7. **Content volume** continues: the next batch should be scripted chains, not
single situations.

## 21. Role theming — 2026-11-09

Method: 390 tests (two new on the register) · a browser drive through all six scenarios recording the register, the
rail order, the advisor order and the briefing kicker (military ×4, law ×2; 0 console errors) · the full-screen sweep on
a law scenario · the stewardship register verified by test and stylesheet, since no shipped scenario starts on that
track.

### Done this pass (GAP §20 item 3)
- The GDD's "by character's job" register, in full: chrome, emphasis, set dressing, briefing voice and landing screen
  by career track, shifting at succession. docs/ART_DIRECTION.md, *The role register*.

### Numbers worth watching
- No shipped scenario starts a steward; the register is reachable only by succession to an heir on that track or by
  the creation screen. A steward-led scenario would show the ledger register from turn one.

### Still open, ranked
1. **Plots with steps.** 2. **The ledger runs rich** (mean 310–350). 3. **A dynasty tree.** 4. **Spatial trade.**
5. **Audio.** 6. **Content as scripted chains.**

## 22. Military depth, stages 1–2 — 2026-11-09

Method: 411 tests (eight on the engine, thirteen reachability rows) · both validators (193 events) · `qa --seeds 6`
(36 runs, 0 failures; stances, loadouts, campaigns and command styles all given by the randomised player; the new flags
all raised) · the probe, extended with a MILITARY line (battles 2–6 a run as before, campaigns 11–14, desertions 9–11
hulls after softening the supply drain, mean loyalty 85–93, restive fleets 0–1) · a browser drive of the Military
screen.

### Done this pass (the owner's ask)
- docs/MILITARY.md: tactics with a counter wheel, boarding prizes, the torpedo loadout on isotopes, the front and the
  flag with valour and the Achilles name, the command cap by rank, supply lines with depots and desertion, campaigns,
  crews' loyalty with refusal and mutiny; the Military screen's brief and controls; thirteen situations.
- Two design notes recorded from the owner's direction: the Republic (parties, two consuls, generals' sway, the Caesar)
  and foreign nations beyond the Reach (off-map expeditions first, the map expansion later).

### Numbers worth watching
- Prizes never fell to rivals in the probe (boarding needs a tactician with manpower and a battle with two losses);
  no rival reached three victories at the front in sixty turns. Both are player-driven by design; watch that the
  Achilles name is reachable in a military playthrough — the reachability row proves the event, not the path.
- Supply desertion at 9–11 hulls a run across thirty houses is attrition, not a bleed. If rival fleets start dying in
  enemy space, give the NPC a "fall back when short" rule before touching the drain.

### Still open, ranked
1. **The Republic**: parties from benches, the consulship, the cursus honorum, generals' sway and the Caesar
   (docs/REPUBLIC.md). 2. **Foreign nations off-map**: expeditions as campaigns (docs/FOREIGN_NATIONS.md). 3. **Plots
   with steps.** 4. **The ledger runs rich.** 5. **A dynasty tree.** 6. **Audio.**

## 23. Dashboards — 2026-11-09

The owner's direction after the military depth: the Office and the map are good; the other screens, each a lit set
with a side panel, do not help or inform. They are now dashboards (docs/ART_DIRECTION.md, *Sets and dashboards*):
the Senate as a chamber view in the Stellaris-galactic-community and Lawgivers mould, the House as a ledger, the
Chancery as an estates board, the Command Deck as a war plot, the Houses as a standings table, the Line as a family
tree (the dynasty tree from the gap list, done), You as a skills radar and sheet, the Worlds as a board, the Chronicle
as a timeline, the Academy as a cohort table. Every control the old screens had is kept; the sets' JS classes remain
in `sets.js` for the Office's neighbours but no screen but the Office and the map creates one.

### Still open, ranked
1. **The Republic** (parties, consuls, generals' sway, the Caesar). 2. **Foreign nations off-map.** 3. **Plots with
steps.** 4. **The ledger runs rich.** 5. **Audio.** 6. The Galaxy map could carry the war plot's supply shading.

## 24. The Republic, stages 1–2 — 2026-11-09

Method: 415 tests · both validators (197 events) · `qa --seeds 3` · the Senate dashboard driven past the first election.

### Done this pass (the owner's direction)
- **Four parties** (the Optimates, the Populares, the Sword, the Factors) seated by every house's laws at the world's
  creation, with the option to found a fifth; benches no longer form of themselves. Members lean with the party line
  on motions and Acts; rivals cross the floor every tenth turn when their laws drift.
- **The consulship**: two elected offices filled every ten turns by the parties' weight; the player stands as their
  party's nominee or alone; consuls weigh more, are paid, move motions cheaper and hold a veto a term; rival consuls
  strike what strikes at them. Four events.

### Numbers worth watching
- The seats are skewed at the start of the shipped scenarios (the Optimates sixty of a hundred and sixteen in the
  Circling Heir, the Sword four): most houses hold few laws off the custom, so fit scores tie and the hash breaks
  them, and the imperial-culture houses all fall to the Optimates. If the chamber should start closer to even, seed
  by culture as well as by laws, or give each party a culture it courts.
- With elections every ten turns and no re-election, the same two or three heavy houses alternate the chairs. The
  foreign wars (docs/FOREIGN_NATIONS.md) are what will make the consulship worth fighting over.

### Still open, ranked
1. **Generals' sway and the Caesar** (docs/REPUBLIC.md 4–5). 2. **Foreign nations off-map** (expeditions as
campaigns; the consuls' command). 3. **Plots with steps.** 4. **The ledger runs rich.** 5. **Audio.**

## 25. Foreign nations, off-map — 2026-11-09

**Done.** `NationSystem` + `content/nations/nations.json`: five nations (one strong neighbour, four lesser), the `foreign_war`
motion, expeditions off the map with the real battle math, subdual/tribute/triumph, bought peace, exhaustion, rim raids,
NPC hawks and expeditions; Military screen *Beyond the Reach* panel, fleet-card expedition controls, Senate picker, eleven
alerts, ten events. 435 tests.

**Measured** (probe, 60 turns, four scenarios): wars abroad 1–2 a run; expeditions 1–5; battles abroad 2–13; raids 12–17;
one subdual (Concord, 84 credits of tribute) and one lost war (Vashti) across the four. Soak (all scenarios, 3 seeds, 60
turns): every nation flag raised at least once; `SendExpedition` reached by the random player.

**Watch**: raids land about one turn in four across the Reach with two aggressive nations — fine for drama, high if the
rim has few worlds (the Academy fixture); the Concord at 26 hulls needs a coalition of houses over several seasons, as
intended, but NPC houses rarely have the hulls — most sit at 0–2 (a pre-existing economy fact, GAP §18). The decaying
Hollow Crown is at 4 hulls by turn 60: a cheap, rich target, which is its point.

**Next**: the general's sway and the Caesar (docs/REPUBLIC.md 4–5) reading `expedition_wins`; the province/claim a
conquest yields; the consuls' war prerogative; then the map expansion.

## 26. Titles by era and the Academy as a prologue — 2026-11-12

**Owner's play notes acted on.** (1) The Academy must be separate from the main game: a year from sixteen, no
interaction with the world, and the tutorial, with a full course for a first run and a briefing for a returning one.
(2) No noble titles in the Republic. (3) The Republic's Academy is academics and friendly competition; the Imperium's
is the Institute. (4) The economy and a stock market are to be planned before anything is built: docs/ECONOMY_MARKET.md.

**Done.** docs/TITLES.md: two names a rung on the house ladder and the career ladders, every screen and log line by
era, `{role.title}` and `{era:A|B}` in prose, about ninety event sentences reworded, a test that no Republic event names
a noble rank. docs/ACADEMY.md: `AcademySystem`, the bus guard, the event filter, two registers, 17 Republic events, 4
new Institute events and 18 re-gated ones, eight lessons, the Academy screen and shell, two new starts. 449 tests.

**Measured.** Headless, naive first-choice play: Republic places of 3rd and 6th of 12 on two seeds; Institute 1st of 12
and 1st of 10 with two dead on one seed and none on the other. Browser: both years played end to end, full and brief,
no console errors; no rail or world screen during the year; turn one opens as Prefect (Republic) or Knight (Imperium).
Soak, eight scenarios, six seeds: no exceptions; every Republic event fired; five Institute chain events did not.

**Watch.** The Institute's events hand out standing more freely than the Republic's, so first place there is easy.
The Academy fixture's world has only seven people of cadet age, so four or five of the cohort are enrolled by the
muster. Heirs born in play do not go to the Academy. The four older starts still open with the short primer.

## 27. The economy expansion — 2026-11-13

Owner: "Finish the economy expansion." The plan (docs/ECONOMY_MARKET.md) had four stages and eight decisions marked as
the owner's. All four stages were built and the decisions taken as the plan suggested; each is a number or a line in
content.

**Built.** A: a commodity market (`MarketSystem`, six goods, prices from supply, demand, war, siege, raids, imports and
order size). B: ventures as assets (`VentureSystem`: value, bids, asks, first refusal) and building as projects
(`ProjectSystem`: years, instalments, pause, rush, abandon). C: companies and shares (`CompanySystem`: incorporate,
float, trade, dividend, control, founder's refusal; stewards invest by party). D: chartered companies, war bonds, two
Senate motions, the Treasurer's steadying, the Imperium's monopolies and seizures, thirteen events, a ninth lesson.
One screen, the Exchange, with four tabs.

**Measured** (eight scenarios × sixty years, no player): goods the Reach is short of (helium-3) swing between half and
double their usual price in most years; overproduced goods sit at 0.6 to 0.8 of it. 240 to 390 projects begun a run.
Twelve house companies listed every run, 400 to 750 shares traded, index 56 to 230, nought to two crashes, one to four
booms. The richest house holds 8 to 13 per cent of all wealth, so paper has not let one house run away. 482 tests.

**Gaps that remain.**
1. Rival houses do not take each other over. The cap of a fifth a year and their own defence make it rare. A hostile
   steward that builds a stake over several years would make the Exchange more dangerous.
2. Materials, rare earths and exotics are in glut. Either the houses should consume more or produce less.
3. Foreign paper, share gifts as bribes, regional prices and direct house-to-house share sales were left out.
4. A company can enlarge its ventures from its own cash but cannot found a new one; the house must found it and fold
   it in.
5. The Exchange has no chart of a single company larger than the twelve-year line in the table.
6. No event yet reads `market_boom` into a bubble that bursts; a boom is only reported.

## 28. The Republic's three orders — 2026-11-13

Owner: model the Republic on the Roman one: the Senate, the military and commerce as three classes; a family good at
one, sometimes powerful in several; a Crassus who rises from nothing by money and connection; a Caesar who rises from
a good family by arms; leverage from controlling a resource or a strategic position. Imperium later.

**Built.** `OrderSystem`: three standings per house from real holdings, a calling, old and new names, glory, cornered
goods. Leverage: extra weight in the chamber, four doors to a seat (land, bench, sword, purse), sway over the votes of
dependants, patrons and clients, canvassing. A new start, *The New Man*. Sixteen events. Senate and Houses screens.
493 tests.

**Fixed on the way.** A save/load mismatch in the order campaigns ended (a dictionary enumerated after removals); a
raw code expression showing in the Senate's "Move a motion" line; a misplaced check in the content validator.

**Gaps that remain.**
1. The road by arms ends at "heard in the Senate". The Senate cannot yet vote a triumph or a recall, a general has no
   army of his own, and nobody can march on the capital. That is the Caesar.
2. No cursus honorum: any qualified house may stand for consul without having held a lesser office.
3. Commerce has no career ladder of its own and the Senate hands it no contracts (tax-farming, supply of the fleet).
4. The player as a client is asked for little: one recurring event. A patron should call in votes on real motions.
5. Stewards never use sway on purpose; it only tilts votes that were already moved.
6. *The New Man* is fragile under random play (two young members, no heir). It wants a marriage offered early.
7. Callings lean to commerce (about 15 of 33). Arms could count more, or wars be more frequent.

## 29. Fix pass — 2026-11-13

Owner: "start working through the list, most glaring gaps and fixes first."

**What was found by measuring**
1. *Turn time.* The Old Lion "stalled" in the test browser. Native profiling (`praetoria perf`) showed one lookup,
   `PolicySystem.Modifier`, at 81 µs a call and hundreds of calls a turn: it re-derived who dominates every region
   each time. One table per house brought a turn from about 127 ms to 48 ms and a projection from 62 ms to 11 ms.
   In-browser timing (`?perf`) then showed the game's own work was already under half a second; the seconds were the
   3D Office under software rendering, at 1.3 frames a second against 60 on flat screens. So the stall was the test
   machine, and the speed-up is real but was not the cause.
2. *The New Man.* `praetoria survive`: the line ended in 12 of 12 unattended runs. The head either married his
   54-year-old patron or nobody; the sister married out in year two and her children went with her. Three causes:
   stewards proposed to the best-liked person regardless of age; an older match event had no age range; and every
   house married off every adult in the first two years, so nobody of an age was left. Fixed: 0 of 10.
3. *Events that never fire.* 58 to 66 of 261 under a random player. Every flag they read can be raised. No dead wiring.
4. *The glut.* `praetoria balance`: materials made 277 to 293 a year against 27 to 97 used. Tuning how fast merchants
   shed stock moved the average price from 0.66 to 0.70 of usual, so it was reverted. It is a missing-sink problem.

**Also done**: the orders on their own Senate tab; a phone layout; a patron who notices a client's vote; stewards who
use sway to reach for an office; companies that found ventures; rivals that add to a stake; checks and probes moved
into the repository. 500 tests.

**Not done, and why**: sinks for materials (an economy change that touches every start, wants its own pass); a flat
fallback for the Office (a look-and-feel item); takeovers between rivals remain rare.

## 30. A character of your own — 2026-11-13

Owner: "Ability to make a custom character with lots of toggles, freedom, lots of things to customize, ability to make
OP." Built as docs/CHARACTER.md describes: `CustomCharacter` (the file), `CharacterWorkshop` (price, rules by mode,
build), two hidden blank starts, a six-tab workshop on the title screen, a chosen face read by every portrait.

**Measured.** The three standard presets cost 26, 32 and 39 of 60 points; the Colossus 867 ("absurd"). Twenty-five
dice characters in each mode are all legal. Four extreme characters play thirty years clean. The soak passes on all
eleven scenario files. 510 tests. In the browser: every tab, preset and mode, a saga begun from each of two presets and
from the Academy, no errors.

**Found on the way.** House legitimacy had no ceiling in nine places (the Colossus reached 103 in three turns). It is
clamped in the property now.

Playing the Officer preset then showed a lieutenant of four turns named *Peacemaker of the Reach*: the marriages
counter was every house's weddings, and "no war declared in your name" read every war in the Reach. Both now count only
the player's own house.

**Gaps that remain.** Stations cannot be picked alone. Kin are a name, a sex and an age. The spouse is not from a named
house. The face has five dials and one uniform. The budget's prices are a first guess and have not been tuned against
how the written starts actually play (the written starts were not priced).

## 31. Love, courting and marriage — 2026-11-13

Built as docs/MARRIAGE.md describes: `Courtship` and `Marriage` on the world, `CourtshipSystem`, four commands, three
event effects, a panel on the Dynasty screen, twelve events.

**Measured.** 525 tests. The 60-turn soak on eleven starts and three seeds passes with a random player who now courts:
lines ended in 2 of 33 runs, against 7 of 33 before the player could court. In the browser, on *The New Man*: a suit
pressed only with time together reached favour 67 in three years and the family agreed (38 + 15); a second run that
never spoke to the family (28) lost to a rival in year six with favour at 100.

**Found on the way.**
- Opening a saved world recorded marriages quietly, so a save taken mid-year lost the wedding's announcement. Found by
  the soak as a save/load difference; the opening record is now made once per world.
- Another house could marry the player's kin without the head being asked. It is a suit to answer now.
- The match list put a woman of 45 first for a house with no heir. Matches likely to bear are listed first and the
  others are marked.

**Gaps that remain.** Stewards do not court or divorce. A spouse does nothing. No children outside marriage. Dowries
are credits only. The affair and the elopement's letter were not seen to fire in the soak. The numbers (thresholds,
the rival's pace, warmth's effect on births) are first guesses, checked only for not breaking a line.

## 32. One person, not a house — 2026-11-14

Owner: "You are playing as one singular person within a dynasty and then each of those people do their own things...
What I don't want is a game where you have to assign all the characters to tasks or else they will not do anything."
The courtship built the day before broke this: the head pressed every relative's suit year by year. The rule is now
written into docs/PROCESS.md and the family systems were checked against it.

| What | Before | Now |
|---|---|---|
| A relative's courtship | The head began it and pressed it every year | Theirs. They choose, it advances by itself, they ask a blessing; the head blesses, forbids or helps |
| A match the head arranges | The relative had no view | They have a wish, shown first; against it they resent or refuse |
| A relative's marriage | The head could end it | Theirs |
| Another house asking for a relative | (built 11-13) the head answers | The same, and accepting against the relative's wish is resented |
| An empty admiralty or governorship | Waited for ever to be assigned | A fit and willing relative takes it up after a year |
| Careers | Advance by themselves | The same |
| Ambitions | Data; only defiance comes of them | **Open.** Relatives do not yet act on their ambitions. On the roadmap |
| Children's upbringing, study, personal purses | Planned as things the head sets | The plans now say they run by themselves and the head may step in |

**Measured.** 531 tests, six of them for this. The soak passes. In the browser, on *The New Man* with nothing done about the family for ten years: the head's sister began a suit of
her own in the fourth year, asked for a blessing in the fifth, waited three years without an answer and gave it up.
Two other houses asked for her hand in that time.

**Still wrong against the rule.** Relatives do not pursue their ambitions, do not divorce or stray by themselves, and
have no purse. The house's building and ventures are all the head's doing; whether a steward of the family should run
the routine of that is a question for the owner.

## 33. The household: found family and delegated work — 2026-11-14

Owner: "it's good to have the option to delegate things to competent members of the house if you want. Remember part of
it is 'found family' as well." Built as docs/HOUSEHOLD.md describes; it is the roadmap's "Under the wing".

**Measured.** 541 tests, ten of them for this. The soak passes on eleven starts and three seeds with a random player
who uses all of it. In the browser on *The Old Lion*: both duties handed out, the steward began a power plant in year
one, a protégé taken in, and in year four someone asked for a place unprompted. Lines ended in 4 of 33 soak runs against
2 of 33 before; the random player now also sends people home and adopts at random, so this is not read as a regression,
but it has not been explained either.

**Found on the way.**
- Panels with no parameters (orders, courtship, household) were not redrawn after End Turn. Fixed.
- Characters other than the player's train a skill every year with no ceiling (a steward went from 3 to 7 in four
  years). Not changed; recorded for the education work, where it will have to be capped or made to cost something.
- A duty-holder who inherited the seat kept the duty under themselves. They lay it down now.

**Gaps that remain.** Only the player's house has a household. Nobody but the player's character has a mentor. Wards
are not hostages. Keeping people costs nothing. The duties are routine only. Three of the six events were not seen to
fire in the soak.

## 34. Education and skills — 2026-11-14

Built as docs/EDUCATION.md describes. It replaces the uncapped yearly training found in §33.

**Measured.** 548 tests, seven of them for this. In ten years of play no character other than the player's now gains
more than three steps in their best skill, or passes 6 from a lower start (they used to gain ten). The soak passes on
eleven starts and three seeds. The survival probe: lines ended in 3 of 80 runs, against 4 of 80 before. In the soak the
count moved between 2 and 6 of 33 across the runs made while building this, as its random player's choices shifted; the
final run was 2 of 33. That swing is the random player (who adopts, sends home and divorces at random), not the game. In the browser a child with a bent for arms reached tactics 2 and leadership 1 in six years; one set against her
bent reached one step.

**Found on the way.** The first version spread a child's lessons over five skills, and five years of lessons showed
nothing at all. Lessons now concentrate on the one or two skills the child is best made for.

**Gaps that remain.** The Academy is still only the player's. A relative cannot be asked to study or to teach. An
upbringing forms no nature. Learning does not slow with age. A battle's step for its commander ignores the ceiling. And
nothing here has been tuned: slower skills must change battles, offices and the Exchange over sixty years, and that has
only been checked for not breaking.

## 35. Personal wealth — 2026-11-14

Built as docs/WEALTH.md describes: purses, the allowance, what people do with their own money, debts, heirlooms, the will
and the custom of the estate.

**Measured.** 556 tests, eight of them for this. The soak passes on eleven starts and three seeds (lines ended in 4 of
33). In the browser the old head died within four years, the house passed to the heir and the relative named in the
will held 15 credits afterwards; the split itself is checked exactly by the tests, not by that run.

**Found on the way.**
- A debt scandal could not happen in a saga's first five years (the check compared against a year that had not come).
- Two household tests depended on an old head surviving their first years; new events shifted the dice and they failed.
  The tests now keep the head young.

**Gaps that remain.** Only the player's house has purses. A purse holds credits only: no shares, ventures or land held
by a person, so a relative cannot yet invest or build anything of their own, which the ambitions work will need. Dowries
still come from the treasury. Nothing is entailed. Heirlooms are only worth something. Two of the five events were not
seen in the soak.

## 36. Ambitions — 2026-11-14

Built as docs/AMBITIONS.md describes. It closes the line left open in §32: relatives did not act on their ambitions.

**Measured.** 564 tests, eight of them for this. The soak passes on eleven starts and three seeds (lines ended in 5 of
33) and all four new events fired in it. In the browser on *The Old Lion*, with nothing done but granting the first
request: three relatives had pursuits by the second year, one asked to be named heir in the third and was, and another
put one or two credits of her own into the house in each of four years.

**Found on the way.** The first draft remembered who had begun a venture only in memory, so a save taken while it was
being built would have lost the founder. It is kept in the save now, and tested.

**Against the rule in docs/PROCESS.md, what is still wrong.** Relatives do not divorce or stray by themselves. They do
not stand for an office of state. Nobody in any other house pursues anything. A relative cannot yet hold shares.

**Gaps that remain.** See "Not in this piece" in docs/AMBITIONS.md. The one most felt in play is that restoring the
house from an allowance is too slow to matter within one head's life.

## 37. A life of one's own — 2026-11-14

Built as docs/LIFE.md describes. With it the roadmap's person-and-family group is built through once; each piece has a
"the rest" line for what it left.

**Measured.** 572 tests, eight of them for this. The soak passes on eleven starts and three seeds, and in the last run
no line ended in 33. The survival probe with health bearing on death: 1 of 80 lines ended (3 of 80 before). In the
browser the old head laid the house down and died the next year, and the funeral was made from his record.

**Found on the way.** A saga opened with everyone at full health whatever their age, so a head of 68 showed 100 against
a most of 58 until the first year turned. Health is set to what each person's years allow when a world is first opened.

**Gaps that remain.** Only the player's house has records, renown, names and funerals. There is no ill fame. Old friends
do not act. There are no duels. Two of the seven events were not seen in the soak. Nothing is tuned.


## 38. The ladder of offices — 2026-11-14

Built as docs/LADDER.md describes: quaestor, aedile and praetor below the consulship, held by people, filled by election
every fifth year, and a consul must now be forty and have been praetor.

**Measured.** 582 tests, ten of them for this; one runs the Republic for sixty years and asserts consuls are still found
and at least four magistrates sit after every election. The soak passes on eleven starts and three seeds with the random
player standing, backing, dissuading and giving games; between three and seven lines of 33 ended across four runs as the
random player's choices changed (seven is the most seen so far). The survival probe is unchanged at 1 of 80. All five new events fired. In the browser The Risen
Officer stood in the first year and was elected quaestor at 30.

**Found on the way.**
- The bench was empty for the first five years of every saga. The seats are now filled when a world opens, from people
  of other houses who had already climbed.
- The law career already had ranks called Magistrate and Praetor, so "is made Praetor" and "elected praetor" appeared in
  the same chronicle meaning different things. The career ranks are now Judge and Justice.
- Someone who had been praetor could stand for aedile. Nobody now stands below the highest rung they have held.
- With each person standing only for their highest rung, an aedile's seat often went unfilled. A house may now put up
  someone who stood higher and lost.
- The roadmap's section 4 had been repeated four times over by earlier edits. The copies are gone, and the two sketches
  that have since been built are replaced by a pointer to their documents.

**Gaps that remain.** A magistrate has nothing to do in office. Other houses' candidates are picked by a rule and do
not canvass. There is no bribery, prosecution, censorship or tribunate. The numbers are first guesses.

## 39. The Caesar road — 2026-11-14

Built as docs/CAESAR.md describes: devotion of crews to a commander, sway, triumph and recall as Senate motions, the
march on the capital, a civil war with sides, and its two endings.

**Measured.** 592 tests, ten of them for this; one walks the player's road, a relative's and another house's, and
resolves every choice of the twelve events. The soak passes with the random player using every command. The survival
probe is unchanged at 1 of 80. A new probe, `praetoria caesar`, reports how far generals get.

**Found on the way.**
- The game's great war ends the Republic around year 28 to 40 in two runs of three, by its own rule. That was not
  written down anywhere near the Republic's design, and it bounds how long any general has.
- Other houses' fleets are small: a general usually commands four hulls of a house's nine. The first bar for a march
  (six devoted hulls) could never be met.
- The house that holds the capital has most of the veteran generals. It could "march" on its own capital and win the
  same year because nobody stood in the gate. It can no longer march, the capital's holder is always for the Senate,
  and a house sitting in the capital cannot win by staying there.
- `ThroneSystem.Enthrone` recorded the Reach's resentment against the player whoever took the throne.
- A second event was nearly named `the_triumph`, which the foreign wars already use.

**Gaps that remain.** For other houses the road is rare: in 30 passive runs, 7 triumphs, 2 recalls carried, no march,
and none of the twelve events fired in the soak. A general does not defect with his crews. The parties do not act as
parties in the war. There is no negotiated end. Every number is a first guess; the probe is the tool to tune them.

## 40. The Republic stands unless the player ends it — 2026-11-14

The owner's correction to §39, written into docs/PROCESS.md: the Republic should not become the Imperium in the
ordinary course of a game; a Caesar is rare for a reason (extremely able, extremely well liked, and the times his); a
rival should succeed about once in ten games or less; the player may try, and can fight a rival who tries.

**What was wrong.** The great war (a clock from the first design) ended the Republic in about two runs of three,
around year 28 to 40, by putting its victor on a throne. Nobody had chosen that; it was simply how the crisis ended.
And §39 had spent its tuning making rival Caesars more common, which was the wrong direction.

**What changed.**
- The War of the Reach no longer enthrones anyone. Its victor gains legitimacy, glory and rank; the Republic is shaken
  for fifteen years.
- Another house's general marches only through three gates: of the first rank, loved, and the times.
- The same three things decide who declares for a general who marches, so a player who crosses in a sound Republic
  finds it defended. The panel shows the gates, the state of the Republic and a forecast.
- Three warning events and an alert.
- Other houses move a recall only against a general they hate or fear.

**Measured** (`praetoria caesar --scenario all --seeds 10`, 60 Republic runs of sixty years, passive player). The
Republic stood in all 60. 64 triumphs, 11 recalls carried. No rival marched: a first-rank general in 14 runs, a loved
one in 31, never both at once. 594 tests; the soak passes and no Republic fell in it either.

**Gaps that remain.** A rival Caesar was not seen at all in 60 runs, which is rarer than "once in ten" if the owner
wants it met now and then; the knobs are named in docs/CAESAR.md. The player cannot make or mend the times. Of the
fifteen events only *The Republic Falters* fired in the soak; the rest are proved by a scripted test.

## 41. Breaking away, and how the great war ends — 2026-11-14

The owner's direction, built as a first stage (docs/BREAKAWAY.md), with the principle written into docs/PROCESS.md:
if it happened in history, the player should be able to do it.

**What changed.**
- States that have left the Reach are a thing in the world: a region, its houses, a form, a ruling house. A house in
  one has left the Senate and the Reach's wars.
- A house that controls a region may take it out. The Reach's temper decides whether it fights. A war of independence
  has no fixed end.
- Houses that fought a general who took the throne need not kneel.
- The great war lasts ten years at least. The capital is winning, not won. It ends crushed, by partition or by
  exhaustion.

**Measured.** 602 tests. The soak passes; seven states were founded in it, six recognised, one brought back. Over 36 passive Republic runs the
great war lasted 10 to 16 years and ended crushed in 16, exhausted in 16, partitioned in 4; the Republic stood in all.

**Found on the way.** The first rule for "crushed" (a third of the victor's hulls) ended 29 wars of 36 at exactly the
tenth year, which made the minimum the whole story. It is now a tenth. The Old Lion's house can declare in its first
year because it opens holding two of three worlds of its region: allowed on purpose ("you can also just try"), and it
is then at war with the four strongest houses in the Reach.

**Gaps that remain.** The three forms of state differ only in name. A state is not drawn on the map and is not a
neighbour (no treaties, reconquest or rejoining). Standing alone is not tested (no economy, no monopoly). You cannot
work on a region's houses to bring them over. Other houses break away only by partition or to refuse a throne. What
sets off the great war is unchanged. Five of the eight events did not fire in the soak and are proved by a test.

## 42. Closing the small gaps, and a register for the rest — 2026-11-14

The owner asked for the gaps reported in §41 to be fixed, the small ones first, and for gaps to be mapped as things are
built so that thin things do not pile up.

**The register.** docs/GAPS.md now holds every thin thing in one list with a size. Collected from nine documents it
came to 59 lines, which is the pile the owner was worried about. Five were closed today; 54 are open: 30 small, 20 a
cycle, 4 several cycles. `tools/gaps.sh` prints the count. The rule (docs/PROCESS.md): add gaps in the commit that
makes them, close before you open, depth before breadth, and raise the ones that recur. Four recur across systems and
are listed first: other houses have no inner life; other houses do not use what the player can; numbers are first
guesses; events that need a rare choice never fire in the soak.

**Closed.**
- *Standing alone.* Fuel of its own, a treasury that pays its way, and half the Reach's output of a strategic
  resource now move the Reach's temper. No fuel means an embargo while the state fights; leverage means the Reach's
  will runs out every two years instead of three.
- *Working on a region's houses.* A quiet word or a gift, once a year a house; at a regard of 25 it gives its word.
  The capital's creature reports the approach.
- *What sets off the great war.* Pressure from what is wrong with the Reach, easing when nothing is, camps that stand
  down. Measured: war in 24 of 36 passive runs within sixty years, from year 29 to 52; none in 12.

**Found on the way.** "Is the Reach in its great war" had been true from the moment the camps formed, years before any
fighting. It now means the war has begun. The first pressure weights brought the war in only 10 of 36 runs, mostly
after year 36; they were raised by about a third.

A save/load drift from the Caesar road, there since it was built and exposed when the soak's player gained new
commands: loading a save noted the crews' commanders again. Fixed, with a test.

**Next, in the register's order.** The small ones in breaking away and the Caesar road (B4, B5, B8 to B10, C1, C3, C5),
then how each form of state is governed (B1) and a state on the map and as a neighbour (B2, B3), which need plans.

## 43. Close and deepen: every position has work — 2026-11-14

The owner's direction: a state changes its form more easily toward the democratic end than the authoritarian; there
are two kinds of dictatorship and two of republic; the people may revolt (later); the Reach lives under the same rules
as any state. **And before any of that: list the small gaps, close them, and make sure every position has something to
do.** The forms of government are written down as planned work (docs/BREAKAWAY.md, docs/GAPS.md G1 to G7) and not
built. The rule is in docs/PROCESS.md.

**Built: the work of office (L1).** Every sitting quaestor, aedile and praetor has a year's business with three
choices and consequences in credits, regard, legitimacy, a region's lawlessness and the public's trust in the
magistrates. The player decides their own; relatives and other houses' magistrates decide theirs by nature. Taking
money sometimes comes out. Trust below 35 is a thing wrong with the Reach, so it feeds the Caesar road's "times" and
the great war's pressure: the new depth is tied into what was already there, as asked.

**Closed with it.** L4 (the relative who was not spoken for), F2 (old ties in the Senate and a civil war), F4 (a
physician for a relative), C3 (parties in the civil war), C5 (the Senate's sword and the state's fleets), E4 (a battle
and the ceiling). Seven closed; 60 open, 16 closed in all.

**Measured.** 623 tests. The soak passes with no save/load drift. Survival: the line ended in 0 or 1 of 10 runs per
start. Caesar probe, 50 Republic runs: the Republic stood in all 50.

**Found on the way, and written into the register.**
- The magistrates' relief, at its first strength, brought the most lawless rim region to order within twenty years in
  a test run. It was halved and limited to one quaestor a region a year. The rim still starts restless; the test that
  says so now looks at year twelve.
- With ceilings holding in battle, another house's general reaches the first rank in 2 runs of 50 (it was 15 of 60).
  A rival Caesar was already rare; this makes it rarer. Inside the owner's rule, possibly too far inside: C9.
- The same two houses were before the court year after year. A quarrel heard last year is not heard this year.
- The work of office is one decision a year from three (L5); trust moves only the troubles of the Reach (L7); old ties
  count nowhere but the Senate and a civil war (F6); a wronged house cannot appeal or prosecute (L2).
- Two of the five new events did not fire in the soak (they need a magistrate of the player's house to be found out);
  both are proved by scripted tests (X4).

**Next, from the worklist.** The law career's Judge and Justice sit a court (J1); a senator's yearly business (S1);
the consuls, Treasurer and Speaker (O1); then the small ones in ambitions, wealth and the household.

### 43a. The bench and the seat, the same day

The owner named the magistrate, the justice and the senator. The magistrates were done above; these are the other two.

- **The seat (S1).** One decision a year for the head: read the accounts, speak for the region, or work the bench.
  The audit can remove a magistrate who took money; three absent years cost legitimacy.
- **The bench (J1).** A Judge hears an estate against a house, which ties the courts to the estates' approval. A
  Justice hears the appeal against a praetor's bent ruling. Appeals go before another house's Justice when the
  player's has none, so the courts can mend what they broke, in either direction.
- **The soak's player now makes these decisions**, with the business of office and the physician, so they are
  exercised over sixty years in every start and not only in scripted tests. The soak passes with no drift.

627 tests, 339 events. Three of the eight new events still did not fire in the soak (a scandal in the player's own
house, the audit finding a thief); each has a scripted test (X4).

Found: in the first year nothing has been drawn yet and the panel said the year was "spent"; it now says the Senate
has not yet sat. A senator's year is thin in its own way (L8), and other houses' senators and judges do not do any of
this (X2).

### 43b. The consuls, the Treasurer and the Speaker

The owner: "keep going down the worklist, consuls and Treasurer next." Each office of the Reach but the Admiral's now
carries a charge a year (docs/LADDER.md), decided by its holder: the player their own, everyone else by nature. What
another house's officer does reaches the player (a levy forgiven, a year with no day in the Senate). O1 is closed; what
is thin about it is O2.

632 tests, 341 events. The soak passes at three seeds and at six.

**Two faults found by measuring, both fixed.**
- *Save/load drift.* Loops that moved several houses' regard at once walked the houses in dictionary order, which a
  load can change. It was in the two commits before this one as well and had passed the soak by luck. Every such loop
  now walks by name.
- *A dead head's year.* On the Old Lion start the head sometimes dies in the first year, after the seat's business was
  drawn in his name; the heir had nothing. Found in the browser, traced with a script. The year now passes with the
  house, and a test plays twelve years checking the head always has it.

### 43c. The desk and the posting

J2 from the worklist. The stewardship career's Governor and above have the year's returns across their desk; a naval
officer of rank without a fleet has a year ashore. Both tie into what exists: the merchants', commons' and officers'
approval, a region's allegiance and lawlessness, the fleets' loyalty, trust, and the person's own purse.

634 tests, 342 events. Soak passes at five seeds; survival unchanged (the line ended in 0 or 1 of 10 per start).

Found: fair assessment at first raised the house's legitimacy, and with two or three relatives at desks the house
climbed a point or more every year for nothing. It now moves the region and the merchants instead. No start's head has
a desk or a posting in the first year, so the panel's block for them is proved by tests and not yet seen in a browser.
What is left is the lowest ranks (J3).

With this every rank from Judge, Governor and Commander up, every elected office, every office of the Reach and the
Senate seat has yearly work. The worklist's next items are in the family (A1, A3, W2, W4, E2, H1).

### 43d. Quarrels and failures

A1 and A3 from the worklist, both in the ambition system. Two of the house after one prize quarrel until it is
settled; a relative's lost election or lost battle is written into their life and counts against what they want, and
three end the hope. Both events fired in the soak without help, which is rare for events since courtship.

636 tests, 344 events. Soak passes at four seeds; survival unchanged.

Left (A5): ventures never fail, so that failure cannot be marked; a quarrel does not divide the house.

### 43e. The family items

W2, W4, E2 and H1 from the worklist, by the owner's order. All four keep to the one-person rule: the head gives, lends
and asks; the relative keeps the money, pays back or does not, agrees or refuses, and finds a mentor without being
told.

640 tests, 347 events. The soak's player now gives, lends, forgives, asks and sets teachers; the soak passes at four
seeds. Survival: the line ended in 0 or 1 of 10 per start (one start moved from 0 to 1 with relatives finding
mentors; inside the range it has always had). Seen in the browser on the Old Lion start: a gift, a loan shown as
owed, the dowry custom, a refusal to study and a teacher set.

Found: a relative who came to have the house still "owed its head" what the dead head had lent them. Cleared.
Left: W6 (loans only run from the head down) and E6 (asking has no bargaining).

## 44. The breakaway items — 2026-11-15

B4, B9 and B10 from the worklist, by the owner's order.

- **Joining a state that exists (B4).** A house of the Reach goes over to a recognised state as it stands, or takes a
  bordering region into it. The Reach lets the region go or calls the treaty broken and comes for the whole state.
  Other houses go over by themselves when they think well of the ruler and ill of the capital.
- **A second great war (B9).** Fifteen years after one ends it is forgotten; the clock and the verdict are cleared and
  the Reach can come to another. Each finished war is kept on a permanent record.
- **Stirring the camps (B10).** A house can raise the pressure on purpose for three years, at a price and a risk.

**Measured** (`praetoria caesar --scenario all --seeds 10`, sixty Republic runs of sixty years, a passive player):

- The Republic stood in all 60.
- The great war broke out in 36 of 60 (it was 24 of 36 when last measured, on fewer seeds: about the same share). It
  still begins between year 28 and 50 and runs 10 to 16 years.
- **It was forgotten before the end of the saga in 6 runs**, all on the start where it comes earliest, and stood at 6
  to 26 in a hundred of the way toward another. **No run reached a second war inside sixty years.** That is as
  designed: a second war is for sagas that run longer. It is proved by a scripted test, not seen in play.
- 11 states broke away and were recognised, all by partition at the war's end. **One house went over to one of them
  by itself**, in one run. It needs a house holding worlds inside a state and thinking well of its ruler, which a
  passive saga seldom produces. The rule is also proved by a scripted test.

645 tests, 350 events. The soak's player joins states and stirs the camps; the soak passes at four seeds.

**Found.** The probe read a finished war from flags that are now cleared when the war is forgotten, and so reported
those runs as having had no war. Each war is now written to a permanent record, which the probe and the panel read.

**Left** (in the register): a state at war cannot be joined, a state cannot itself reach for a region, and a house
cannot leave a state on its own (B11); stirring names no target (B12). Two of the three new events did not fire in the
soak (a house coming over to the player's state, the treaty called broken); both have scripted tests (X4).

## 45. What public trust does — 2026-11-15

L7 from the worklist, by the owner's order: trust in the magistrates should move elections, the estates and standing,
not only the troubles of the Reach.

**Built.** A scandal in office lies on a house for ten years and costs its candidates votes at every count, for the
ladder and the consulship, twice as many when trust is under 50. Below 35 the commons turn on every house in office
and the house that holds the capital loses standing; at 75 office is honoured. Events when it collapses and when it is
believed again. What a person of the player's house does to an estate now lasts.

**The measurement changed the work.** Before any of that could matter, the probe was taught to report trust, and it
showed the number did not behave: over sixty Republic sagas it averaged about 80, was at 75 or more in three years of
four, and **never once fell below 35**, while some house was under a scandal nine years in ten. Three causes:

1. Catching a thief raised trust by more than the theft and the exposure lowered it.
2. Every magistrate who dealt straight added a point, eight or ten a year, against a drift of one.
3. Two things dragged it down for the wrong reason, found by tracing one start year by year: a passive player's
   unheard bench cost the whole Reach a point a year, and aediles too poor to pay for the works were counted as
   choosing to do nothing.

Now routine honesty only mends trust (a point a year in all, up to 60), something notable is needed to lift it past
that, a caught thief no longer outweighs the theft, an empty bench or desk costs its own house, and poverty is not
held against the magistrates.

**Measured after** (`praetoria caesar --scenario all --seeds 10`, sixty Republic runs of sixty years):

- Trust averages 55 to 62 by start. It is honoured (75 or more) in 5 to 13% of years.
- **It falls below 35 in 8 runs of 60**, for 1 to 4% of the years on the starts where it happens.
- Some house is under a scandal in about nine years of ten: there is always one somewhere, which is what makes the
  ten-year cost at the count matter.
- The Republic stood in all 60. The great war broke out in 40 (it was 36). The Republic was troubled a little more
  often on two starts, because "the magistrates are for sale" is now sometimes true.
- Another house's general was of the first rank in 8 runs and passed all three gates in 1; none marched.

648 tests, 352 events. Both new events fired in the soak unaided. Survival: 0 or 1 of 10 per start.

**Also found.** `tools/browser/ladder.js` had been broken since its default start was changed: a comment had swallowed
the line that set the number of years. Fixed.

**Left**: trust is one number for the whole Reach, and only the commons react to it (L9).

## 46. The lowest ranks — 2026-11-15

The owner set C9 (the rival Caesar's first gate) aside for now; it stays on the worklist marked as waiting. Next was
J3.

An Aspirant or Advocate pleads a brief; a Clerk or Administrator keeps the ledgers; a Cadet, Ensign or Lieutenant
with no command has the watches. Each is one choice of three a year, in learning, the person's own purse, regard, the
estates and a small risk. **With this every rank of every service has work**, along with every elected office, every
office of the Reach but the Admiral's, and the Senate seat: the owner's "every position has work" is met at its first
depth everywhere.

650 tests, 353 events. Soak passes at four seeds. Seen in the browser on two starts whose heads are juniors (a
Lieutenant's watches, an Administrator's ledgers), which is also the first time the panel's block for a desk, a posting
or a junior's work has been seen on screen and not only tested.

**Survival.** The line ended in 0 of 10 on six starts, 1 of 10 on one and **2 of 10 on the Risen Officer**. That start
has read 0, 1 or 2 across the last six commits: it is the fragile one, and any change to the year moves it. Halving a
junior's risk on patrol did not change it, so the patrol is not the cause. It wants its own look.

**Left**: a junior's work has no thread from year to year, and a brief is heard by nobody in particular (J4). The
padded-account event did not fire in the soak; it has a scripted test (X4).

### 46a. Ambition items, the same day

A2 and A4 from the worklist, and the small half of A5.

- **Restoring the house (A2).** A restorer gives nearly all of a purse while the house is poor and counts what they
  build for it. From an ordinary purse with nothing built it is done inside fifteen years (a test plays it); it had
  been thirty or more.
- **Where an ambition comes from (A4).** Two in three want what they were brought up toward; one set against their
  bent wants something of their own. The same dice are still thrown the same number of times, so nothing else in a
  saga's luck moves. A test plays three sagas and checks most who come of age want what they were raised to, and not
  all.
- **A grudge (part of A5).** The loser of a quarrel over an office or a command holds it against the one who has it.

653 tests. Soak passes at four seeds. **Survival: 0 of 10 on every start**, the Risen Officer included; it read 2 of
10 one commit earlier, which is the fragility noted in section 46 and not a result of this change.

**A mistake of mine, twice today.** Inserting a statement with a trailing comment into the middle of an existing line
commented out the rest of that line: once in `tools/browser/ladder.js`, once in `AmbitionSystem.Found` (caught by an
existing test before it was committed). All forty files changed in this session were scanned for the same thing; there
were no others.

**Left**: ventures do not fail, and a quarrel does not divide the house (A5).

## 47. The second batch of breakaway items — 2026-11-15

B6, B8, B11 and B12 from the worklist, by the owner's order.

- **A throne's own temper (B6).** Under a throne, whether the Reach fights for a region is the Emperor's answer: how
  new the throne is, the Emperor's nature, how secure the throne is at home, and what the Emperor thinks of the house.
- **More ways to bring a house over (B8).** A promise of a place (regard at once, and a debt of three credits a year
  for ten years if the state is founded with them in it), a threat (they follow while they are afraid), and a
  marriage between the two houses making a house easier asked.
- **Joining, leaving and reaching (B11).** A state still fighting can be joined; a member that is not the ruler can
  leave, and does when it thinks ill of its ruler; a ruler can court houses of the Reach in or beside the state, and
  one that controls a bordering region may bring it.
- **Stirring with a target (B12).** The same act can name one house.

**Measured** (`praetoria caesar --scenario all --seeds 10`, sixty Republic runs of sixty years, a passive player): the
Republic stood in all 60; the great war broke out in 39; 14 states were recognised; 2 houses went over to a state by
themselves. The probe now also counts houses that leave a state; that was added after this run, so it has no figure
yet. No throne arises with a passive player, so the throne's temper is proved by a scripted test only.

659 tests, 355 events. **The soak's player now founds more states**: 8 over 44 runs where it had been 1 to 4, because
it can promise and threaten its way to control of a region. The soak passes with no drift. Survival: 0 of 10 on every
start.

**A fault found by a test.** A house that had been one of those sent against a state stayed on the state's list of
enemies after it went over to that state. Fixed: it is taken off the list.

**Seen in the browser** on the Old Lion start: the four houses offered as targets for stirring, a promise made, and a
threat refused with its reason (three hulls to their one).

**Left** (in the register): a marriage or an office cannot be offered as the price of a region (B13); stirring against
a house does not lead to a censure or a war (B14). The two new events did not fire in the soak; both have scripted
tests (X4).

## 48. Straight down the register — 2026-11-15

The owner's word, written into docs/PROCESS.md: work down the register without asking; do every small and obvious
gap; skip, and list, the ones that are the owner's to decide. This is the first pass under it. Thirteen rows closed.

**Family and wealth.** An entail (W3). Heirlooms that do something: a signet mends a name, a sword teaches in the
fleet, a library speeds study (W5). The head can borrow of a relative and lend at interest; relatives lend to each
other; a bride's purse goes toward her portion (W6). A household costs a credit a head and a stipend for whoever does
the head's work (H3). A relative may name a price for studying, and a teacher of the house can be thanked (E6).
Nobody sits above their ceiling (E5).

**A life and its ties.** The elder does one thing a year that nobody asked for, and the elder's old friends stay the
house's (F5). Old ties count at the count, in a court and in a match (F6). Other houses divorce (half of M1).

**Public life.** A junior has someone above them by name, whose regard is their standing, and who speaks for them
after three good years (J4). The merchants and the magistrates' own order mind a magistracy for sale (half of L9).
A house stirred against another moves a censure of it (half of B14). The house divides behind a quarrel (half of A5).

**Measured.** The soak (33 runs of sixty years, a randomised player, with a save and load in the middle of each) passes
with no drift. `praetoria caesar --scenario all --seeds 10`: over the sixty Republic runs the Republic fell in none.
Survival (`praetoria survive --scenario all --seeds 10`, a passive player): **the first measurement lost a line in
two starts**, the Imperial Court and the Risen Officer, where the last pass had lost none. That was followed up, and
is the next paragraph. After the fix: 0 of 10 on every start. The soak and the Republic probe were run before that
fix; both were started again on the final build and their figures go in the next section.

**An older fault, found by following up one lost line.** The survival probe now prints, for each line that ends, the
seed, the year, and what the chronicle last said of the house. For the Imperial Court, seed 6, year 42, it said that
the player's own character, "governor of Lesser Marrow, declares the world for himself". Two things were wrong, and
neither was new: a world taken by another house kept the governor the old house had seated, so the head of House
Thorne was still "governing" a world House Ferro had taken; and the rule that lets an ambitious governor who hates the
owner declare a world for themselves did not except the player's own character. He was moved out of his own house
into a house of one, which ended the same year. Fixed: a governor of the house a world was taken from leaves the
chair, and the player's own character never does what the player did not choose. A test holds both. Nothing in this
pass caused it; a small change elsewhere moved that saga onto the path where it happens.

680 tests (twenty new in tests/RegisterTests.cs, each named for the row it closes, and one for the governor), 362 events (seven new).

**A fault of my own, caught by the build.** Editing an existing test, I again put a comment in the middle of a line
and commented out the rest of it. The compiler stopped it. It is the third time; the rule (never a `//` inside a line
when editing by replacement) is in my notes and I broke it. The changed lines of this pass were scanned for others.

**A design caught by an existing test.** As first written, a junior whom the one above already thought well of was
spoken for in their first year: the Old Lion, pleading one brief before a judge who knew the name, went up a step.
That is the name being rewarded, not the work. It now takes three good years under that person as well.

**Seen in the browser** on the Old Lion start: the sword of House Castellan entailed from the start, with what it
does; the entail broken (with the confirmation) and made again; a loan at interest showing "owes you 12" and paid off
within eight years; no console errors. The Borrow button was dark throughout, because no relative had twelve credits:
mapped as W7.

**Mapped while building** (ten rows, docs/GAPS.md): W7 relatives' purses are small; W8 no heirloom is ever made or
won; E7 a price is only ever money; H5 the keep is the same for everyone and nobody leaves over it; F7 the elder
cannot be asked for anything; M6 nobody chooses another house's divorce; J5 a junior's standing is the player's
house's only; L10 trust is still one number; B15 houses do not go to war over what is said; A6 a venture never fails.

**Skipped as the owner's to decide**, each marked in the register with the question it turns on: C9 (should the gate
ask less of other houses' generals), C6 (may a relative end the Republic against the head's wishes), M4 (does a
spouse move house, and which one), B13 and G1 to G7 (the forms of government).

**Events not seen in the soak.** Four of the seven new events did not fire in it (they need a blood relative in a
particular state); all seven have scripted tests (X4).

## 49. The small ones left behind, and the figures owed — 2026-11-15

**The figures owed from section 48**, on its final build (with the governor fix): the soak passes with no drift;
`praetoria caesar --scenario all --seeds 10` over sixty Republic runs: the Republic stood in all 60, the great war
broke out in 41, 9 states were recognised, no house went over to a state by itself, trust in the magistrates averaged
54 to 63 by start. Survival: 0 of 10 on every start.

**This pass** closes the five small gaps that section 48 mapped, so that no small gap is left that is not the
owner's to decide:

- **W7, relatives' purses.** The first attempt did not work, and the browser said so. Letting a careful person keep
  thirty credits instead of twenty, offering a loan of five as well as ten, and letting a relative with six to spare
  help another changed nothing in play: after twelve years on the Old Lion start nobody had seven credits. The cause
  was income, not the cap. A person's service now pays them their rank and one more a year, where it paid half that.
  Driven again: in year twelve a relative lent the head five.
- **H5, the keep.** A grown person of standing costs two a year. One unpaid two years running, who thinks less than
  20 of the head, goes home or lays down the work; a child stays.
- **E7, a price that is not money.** One or two influence, for a word said for them in the right place.
- **F7, asking the elder.** For one of the three things; done first and with a will, or the elder says why not. An
  elder who thinks ill of the head keeps their own counsel.
- **M6, who chooses a divorce.** After three years apart, with no child or with the houses fallen out, the head of
  the house that took the dowry ends it; an upright head does not.

**Seen in the browser, and changed.** The Risen Officer, the start that is about a junior officer, was told "the
house has no ship for you to serve in": the J4 thread asked for a ship of the house's own and his house has no fleet.
A junior officer now serves in a ship of their patron's, or failing that of the house that holds the capital. Driven
again: "You serve in The Aurelian Guard of House Aurelian under Placidia Aurelian, who does not yet know your name."

**Measured** on this pass's final build. The soak (33 runs of sixty years with a save and load in each) passes with no drift;
its player founded nine states. The Republic probe, sixty Republic runs: the Republic stood in all 60; the great war
broke out in 40; 12 states were recognised; one house went over to a state by itself; trust in the magistrates
averaged 54 to 63 by start. Survival, eighty passive runs: **one line ended**, the Circling Heir on seed 4, where the
last pass had none. The survival probe now says how a line ended, so this one can be read: the house fought House
Morrow over its only world for twenty years, ran out of fuel in year 35, lost the world in year 51 and ended in
year 53. That is a house losing a war with nobody at the helm, not a fault; but it is a line lost that was not lost
before, and it is written here as such. The other seventy-nine stood.

684 tests (four new, and the divorce test extended to cover who chooses and who does not).

**What is left.** 43 rows open: 32 a cycle each and 6 several cycles. Of the 5 marked small, four are the owner's
(C9, C6, M4, B13) and the fifth is the standing note about events the soak does not reach (X4). The next rows in
order are L2, L5, L8 and O2 (a magistrate prosecuted; cases with facts; a senator's committees; an office's charge
with figures), each a cycle.

## 50. The courts, and a row that was closed on a test and not on the game — 2026-11-15

**L2: what a magistrate did is answered for.** Until now a magistrate who took money either had it come out that
year or got away with it for ever, because the year's business was thrown away with the year. A bent act is now
kept on a record for eight years. A magistrate cannot be touched while they sit; afterwards the head of any house can
bring a charge before a sitting praetor of a third house, and the verdict is that praetor's (the upright convict, a
friend of the accused's house lets them go, one for sale finds for the richer house, and before the rest it goes by
who pleads better). Other houses bring charges too, one a year in the whole Reach, and the people of the player's
house are not excepted. A sitting praetor can be offered a sum: the upright refuse from the bench, and a bought
ruling comes out one time in three. A new panel on the Senate screen shows the record, who would hear each charge and
what kind of praetor that is, what stands against the player's own house, and the bench. Details in docs/LADDER.md.

**Measured.** On the final build. The soak (33 runs, a randomised player who now brings charges and makes
offers, a save and load in each) passes with no drift. Survival: 0 of 10 on every start. The Republic probe, sixty
Republic runs with a passive player: the Republic stood in all 60; the great war in 38; 15 states recognised. In
those sixty runs **707 former magistrates were charged and 499 convicted**: about twelve charges a run, one every
five years, seven in ten of them proved. Trust in the magistrates averaged 59 to 64 by start, two or three points up
on the last pass, which is the convictions. A house was under a scandal in about nine years in ten, as before.

**Seen in the browser.** The Old Lion: a quaestor of House Dreyer who took money in year 1 was on the record by
year 3; the charge came before a praetor "known to be for sale, who will find for the richer house"; the Old Lion's
house being the richer, it was proved. The same praetor then took fifteen credits and was shown as "in your pocket",
and the buying appeared under "Against your own house". The New Man: the first praetor offered was upright, and the
chronicle reads "Placidia Aurelian, praetor, says in open court what Marcus Crane offered. Nothing was taken.
Everything was heard." My first run of the script found no panel: it is under the "Your standing" tab, and the
script was wrong, not the page.

**A row closed on a test and not on the game.** The probe now counts marriages of other houses that end. Over the
first sixty runs with the count in, it was none. Sections 48 and 49 closed M1 and M6 (other houses divorce; somebody
chooses it) on tests that set a marriage's warmth to nothing by hand. In play no marriage of another house ever went
cold, so the rule had nothing to act on. I should have measured it before closing it; the register rule says built,
tested **and measured**, and I had done two of the three.

What strains a marriage made between two houses is the two houses falling out, so that is what was added: it loses
warmth while they are at war (8 a year) or their heads have fallen out (5), half as much if it was a love match.
Measured on
the final build over the same sixty runs: of 2,763 marriages of other houses standing at the end, 69 were cold and
53 living apart, and **38 had been ended** over the years. The test now makes a war and waits, and does not touch the warmth.

**Left, and mapped.** A trial is one praetor's decision: no evidence is led, there is no appeal from a verdict, and
the accused does nothing in their own defence. Only a praetor can be bought (L11).

## 51. The facts of a year's business, and three numbers that were wrong — 2026-11-15

**L5.** The work of an elected office was one decision from three with nothing behind it. A praetor's case now has a
matter, a stake of 5 to 15 credits and a law that is plain, arguable or doubtful; the plainer the law, the more a
ruling against it costs, and a doubtful one is neither appealed nor kept on the record. A praetor can bring the two
houses to terms, which is the honest ruling where the law is doubtful and short-changes the house that had the law
where it is plain. A quaestor's accounts show what is over (4 to 12 credits, and where it came from): that sum is
what can be sent to a lawless region or taken. An aedile can give lanes and grain both. Details in docs/LADDER.md.

**Measured, and corrected.** The first measurement on this build: the soak passed, survival was 0 of 10 everywhere,
the Republic stood in all 60 runs. And the Reach's trust in its magistrates had risen to 61 to 70 on average and was
never low in any of the sixty runs, where before the courts it had been 54 to 63 and low in six. That matters: low
trust is one of the things that can go wrong with the Republic, and a number that can never fall is a mechanic that
is switched off. Three numbers were wrong, two from this pass and one from the last:

| What | Was | Is |
|---|---|---|
| An arguable ruling against the law | −2 (every such ruling used to cost 3) | −3; a plain one −4, a doubtful one −1: three on average, as before |
| Lanes and grain both | +2, a gain that is not capped at sixty | +1, capped at sixty like any honest year's work |
| A conviction (from the courts) | +2, about twelve times a run | +1, capped at sixty |

**Measured again on the final build.** The soak (33 runs, with a save and load in each) passes with no drift.
Survival: 0 of 10 on every start. The Republic probe, sixty Republic runs: the Republic stood in all 60; the great war
in 43; 15 states recognised; 629 former magistrates charged and 436 convicted; 41 marriages of other houses ended.
Trust in the magistrates averaged 53 to 64 by start and was low at some time in 8 of the 60 runs: where it stood
before the courts were built, and a little more often low.

694 tests (four new for the facts; three older tests that assumed five credits of damages now read the stake).

**Seen in the browser.** The Risen Officer, elected quaestor at thirty: "The year's accounts of the Reach are yours
to close. They show 3 credits over, from harbour dues that came in over the estimate", with the offer to let "3
credits find their way to your house". Three, not the old eight, because that year the capital's purse held three.

**Left, and mapped.** The facts are figures drawn by lot. Nobody gives evidence, nothing is found out, and a trial is
still one decision (L11).

## 52. The Senate's committees — 2026-11-15

**L8.** A senator's year was one of three things, with nothing carried from one year to the next but a count of
absences. There are now three standing committees (the Purse, the Fleet, the Laws). Every house sits on one. A
senator can spend the year on the committee's work, and each such year raises their standing with it: at 3 the
committee's other houses lean toward what the house moves on the committee's own business; the highest standing at 6
or more has the chair, and is heard. A year missed costs a point. A senator can move to another committee and starts
at the bottom of its table. The years sat and missed are kept, and count for or against a senator at any election
once there are five of them. Details in docs/LADDER.md.

**Measured.** The soak (33 runs, with a save and load in each; its player now does the committee's work and changes
committee) passes with no drift. Survival: 0 of 10 on every start. The Republic probe: the Republic stood in all 60
runs; the figures are the same as the last section's to the digit (the great war in 43, 15 states, 629 charged and
436 convicted, trust 53 to 64), which is as it should be: the probe's player is passive and never takes the seat, and
nothing here moves unless a senator does the work. In the soak a chair was taken at least once (its flag was raised), but the event that follows was not drawn in any of the 33 runs. It has a scripted test (X4).

698 tests (four new).

**Seen in the browser.** The Old Lion: "You sit on the Committee of the Fleet (wars, commands, recalls and
triumphs), with 9 other houses. Your standing with it is 0 of 10. At 3 its houses begin to lean your way on its
business. Nobody has standing enough to chair it." The committee's work was chosen, a move made to the Purse, and
after a year there the panel read "Your standing with it is 1 of 10".

**Left, and mapped.** A committee has work to be done and no business of its own to decide: it reports nothing,
examines nobody, holds no inquiry. Only the player's house has a standing that is earned; the others' is reckoned
from their head's rank (L12).

## 53. The chair, and a budget with figures — 2026-11-15

**O2.** The charge of an office of the Reach was one decision a year from three, and none of the three touched
anything real: the Speaker's "order of business" ordered no motion, the consul presided over nothing, and the
Treasurer's budget was three fixed sums. Three things changed (docs/LADDER.md, "The chair" and "The budget").

- **The Speaker** holds one motion a year over to the next; a motion can be held over once. Giving an enemy no day
  now holds over whatever that house has moved.
- **The presiding consul** speaks from the chair for or against one motion a year, and the houses that neither moved
  it nor are its target lean with the chair. A tied Senate goes as the presiding consul's house voted; a tie used to
  fail whoever sat in the chair.
- **The Treasurer's budget** is drawn from the state of the Reach: what was assessed on the landed houses, what the
  lawless lanes lost of it, and what the Reach's own hulls, its lanes and its loose regions call for. Balancing the
  books banks half the surplus, and in a year of shortfall banks nothing and leaves the Reach's fleets short.
  Forgiving a region costs what its houses would have paid. The contracts are a third of what the year calls for.

Officers of other houses use the chair by their interest before each vote.

**Measured.** The Republic probe (60 runs of 60 years): a Speaker held a motion over 9 times; a presiding consul
spoke from the chair 155 times; **no vote tied, so the casting vote was never given**. That rule is proved by a test
and has not been seen in play, and the register says so (O3). The Republic stood in all 60 runs. The great war was
fought in 44 (43 before), 14 states broke away (15), 625 former magistrates were charged and 448 convicted (629 and
436), and trust in the magistrates averaged 53 to 64 by start and was low in 8 of 60 runs, as before. The soak (33
runs, a save and load in each; its player now uses the chair) passes with no drift. Survival: 0 of 10 on every start.

**Not seen.** The event that follows a motion of yours being held over did not fire in the soak: its player seldom
has a motion due in a year when a Speaker of another house has reason to hold it. It has a scripted test.

705 tests (seven new).

**Not looked at in the browser as the office-holder.** No start begins as Speaker or consul, and no script reaches
either office, so the buttons on the Senate screen were not pressed in a browser. The screen was loaded with a motion
on the docket and renders without error; the buttons' refusals and effects are the command's, which is tested.

**Left, and mapped.** One motion a year each; three budget choices, not a budget whose heads are set; no summoning
or dismissing the Senate; nothing debated (O3).

## 54. Free marches and free holds are states — 2026-11-15

**B5.** Two rules older than the breakaway states made houses that had "left" without leaving. A free march set a
flag on a house and nothing else: it kept its seat in the Senate and could be elected consul. A free hold, a rebel
world under a new house, also sat in the Senate. Both are states now (docs/BREAKAWAY.md, "Free marches and free
holds"). A free march takes its region out, on the terms of any declaration except that it does not wait for three
worlds in five. A free hold is the Free City of its world from its first day: outside the Reach, its old holder's to
buy back or take, named in a treaty if it stands three years, and not the Senate's business.

**Measured before it was built.** In ten runs of one start there were no free marches and 17 rebel worlds: the rule
that mattered was the second, so it was built to be light. A free state does not count as a region that has left the
Reach, and the Senate cannot move to reconquer it, or 111 rebel worlds in sixty runs would have been 111 causes of
war.

**Measured after.** The Republic probe (60 runs of 60 years): the Republic stood in all 60. There were 6 free marches
and 111 free states; 24 of the free states were bought back or taken, and 106 were named in a treaty at some point.
22 states of the larger kind broke away (14 in the last section), 6 of them free marches; the great war was fought in
47 runs (44). The free marches are the likeliest cause of both rises: a free march is a region that has left the
Reach and is counted as one. Trust in the magistrates averaged 50 to 63 by start and was low in 5 of 60 runs; 634
former magistrates were charged and 433 convicted. The soak (33 runs, a save and load in each) passes with no drift.
Survival: 0 of 10 on every start.

707 tests (two new).

**Not looked at in the browser.** The Breakaway panel lists a free state on a line of its own; no script plays long
enough to see a rebel world break away, so the line was not seen rendered. It compiles, and the screen loads.

**Left, and mapped.** Almost every free state is named in a treaty, because an old holder seldom comes back inside
three years. A free state cannot join a larger state or league with its like, and nobody courts it (B16).

## 55. A war over what was said — 2026-11-15

**B15.** Stirring one house against another ended, at most, in a censure. Now a house that has been stirred against
another and already thought very ill of it (−50 or worse) goes to war with it, if it has at least as many hulls, has
no war already, is not bound to it by a marriage, and the other does not hold the capital. One such war a year; the
player's house is never moved to one and is never the target. Stirring costs a house 8 of its regard for the target,
so this is the last push on a hatred that was there. Details in docs/BREAKAWAY.md.

**Measured.** The soak (33 runs, a save and load in each; its player stirs the camps at random): two wars began over
what was said, and the event that tells the player whose money it was fired, with two of its three choices taken. The
soak passes with no drift. The Republic probe's player does nothing, so nobody stirs and no such war begins: its
figures are the last section's to the digit (the Republic stood in 60 of 60, the great war in 47). Survival: 0 of 10
on every start.

709 tests (two new).

**Left, and mapped.** Only the player stirs. No other house sets two houses against each other, so in a game where
the player leaves the camps alone this never happens; and the war is an ordinary war with no aim of its own (B17).

## 56. Patrons, and what bringing every house into a rule does — 2026-11-15

**J5.** The one above a junior did one thing with what they thought of them, and only for the player's house. Now a
senior takes a junior up after three good years and a regard of 20, is above them from then on wherever either is
posted, and takes them along when they rise. A junior the one above has come to think ill of falls out with them.
And the juniors of every house of the Reach have somebody above them and a standing with them, by a reckoning of
good and bad years in place of the choices the player's people make (docs/LADDER.md, "A patron, and a falling out").

**Measured.** The Republic probe (60 runs of 60 years): 1,769 juniors were taken up by a patron, 988 were taken
along when the patron rose, 117 fell out with the one above them, and 2,431 were spoken for and went up a step. The
soak (33 runs, a save and load in each) passes with no drift; all three new events fired in it. Survival: 0 of 10 on
every start.

**What else moved, and it should be said.** Bringing some hundred and fifty people a run into a rule that had held a
handful changes more than the rule. Against the last section, with nothing else altered:

| | Before | After |
|---|---|---|
| The Republic stood | 60 of 60 | 60 of 60 |
| The great war was fought | in 47 runs | in 47 runs |
| States that broke away | 22 (6 of them free marches) | 33 (10 free marches) |
| Trust in the magistrates, by start | 50 to 63 | 48 to 62 |
| Runs in which trust fell under 35 | 5 | 14 |
| Another house's general reached the first rank | in 8 runs | in 12 runs |

The likeliest cause is rank. A junior who is spoken for goes up a step sooner (never faster than the service allows
anybody), and 2,431 such steps in sixty runs is some forty a run. More people of rank means more generals of the
first rank, more who qualify for a bench or an office, and more houses that count for something where they live. The
owner's rule holds: the Republic stood in every run. But the Reach is a livelier and less trusting place than it was
this morning, and that came from this change and not from a decision to make it so. If it is too much, the place to
turn it down is the reckoning (four good years in six), not the rule.

711 tests (two new).

**Left, and mapped.** A patron takes a junior along and speaks for them, and that is all: a junior cannot seek one
out, a patron asks nothing back, and a patron's enemies are not the junior's (J6).

## 57. Whose name it is: trust by office and by region — 2026-11-15

**L10.** Public trust was one number for the whole Reach. It still is. But every point that moves it is now also set
against the office whose business it was and the region where it was felt (docs/LADDER.md, "Whose name it is"). An
office's name is what a scandal costs at the count for that office. A region that believes the magistrates under 35
holds a point less closely to the Reach each year; one that believes them at 75 or more, a point closer.

**Measured, twice, because the first build was lopsided.** As first built every point counted alike. The probe
showed two faults. An office's name stood 5 or more *over* the Reach's in 24 to 35% of office-years and under it in
6%: routine honest work, which the Reach's own figure is built to ignore, was piling up on the offices. And a region
stood 5 or more under the Reach in 1% of region-years: a wrong done to a region was forgotten before the next one.
Three changes: ordinary honest dealing mends a name that is under the Reach's and does not lift one above it; a
wrong is felt twice over in the region it was done to; and a region's figure runs back a point every second year,
where an office's runs back every year.

**After.** The Republic probe (60 runs of 60 years): an office's name stood 5 or more under the Reach's in 6 to 7%
of office-years and 5 or more over in 7 to 12%. A region believed the magistrates 5 or more less than the Reach in
17 to 29% of region-years, and under 35 in 0 to 23% depending on the start. The Republic stood in all 60 runs; the
great war was fought in 47; 33 states broke away; trust in the magistrates averaged 48 to 63 by start: all as in
the last section. The soak (33 runs, a save and load in each) passes with no drift. Survival: 0 of 10 on every
start.

713 tests (two new).

**Not looked at in the browser.** The ladder panel's line and the Worlds screen's gauge compile and the screens
load; no script plays to a year in which an office or a region stands apart, so neither was seen rendered.

**Left, and mapped.** Each is one figure moved by whatever was done: a region does not tell a court that wronged it
from a quaestor who robbed it. Nobody can set out to mend a name. A region's belief does one thing, to its
allegiance (L13).

## 58. A venture can fail — 2026-11-15

**A6.** A venture could be sold, enlarged or taken, and could never go wrong. Now one that a house owns outright and
that earns nothing or loses money three years running is wound up: the house gets half its salvage, the venture is
gone, and whoever of the house founded it has failed at something of their own (docs/ECONOMY_MARKET.md, section 11).
The owner is warned a year before and can put money in or sell.

**Measured first.** Before the length was fixed, ten runs of one start were counted: 3% of venture-years went
without a profit, and the unprofitable ventures stayed unprofitable. Three years was kept.

**Measured after.** The Republic probe (60 runs of 60 years): 1,063 ventures were wound up, about eighteen a run, of
some hundred and twenty standing at the end of each; a year without a profit was 1 to 3% of venture-years by start.
The Republic stood in all 60; the great war was fought in 48 runs (47); 32 states broke away (33); trust in the
magistrates averaged 47 to 62 by start and was low in 15 runs (14). The soak (33 runs, a save and load in each)
passes with no drift, and the event for a failed venture fired in it. Survival: 0 of 10 on every start.

**Not seen in play.** The soak's player never put money into a failing venture: it has the command, but its own
ventures seldom lose three years running. Carrying a venture is proved by a test.

715 tests (two new).

**Not looked at in the browser.** The venture card's warning and its button compile and the Exchange loads; no
script plays to a year in which one of the player's ventures is losing.

**Left, and mapped.** One cause of failure, and only the owner can act: other houses never carry or sell a failing
venture, a company's venture is never judged, a failure costs money and nothing else, and a founder gets no second
start (A7).

## 59. Things a house comes by — 2026-11-15

**W8.** A house had the signet, sword, library or estate it was born with, and never anything else. Now there are
three more kinds, and each is come by in its own way (docs/WEALTH.md, "New things"): a shrine or a library is made
to order out of a person's own purse; a standard is taken in a battle by whoever commanded and is theirs; plate is
sent by the other house at a wedding, to the one who married. Each does one thing of its own: the commons' regard,
a fleet's loyalty, the regard between two people of two houses. Other houses come by them as the player's does.

**Tuned by measurement.** The first setting gave three houses in four something new by the end of sixty years; the
second, one in two. The third, kept, gives about one in three. A flaw was found on the way: the chance was taken
from the low bits of a hash of two people's names, and names that differ only by a number share those bits, so a
test of sixty weddings saw no gift at all. The hash is now divided before it is used.

**Measured after.** The Republic probe (60 runs of 60 years): 209 things made to order, 99 standards taken, 401
gifts of plate; 575 of 1,936 houses (30%) held something new at the end. The Republic stood in all 60; the great
war was fought in 49 runs (48); 30 states broke away (32); trust in the magistrates averaged 47 to 62 by start and
was low in 15 runs (15); 1,039 ventures were wound up (1,063). The soak (33 runs, a save and load in each) passes
with no drift. Survival: 0 of 10 on every start.

**Seen in the browser.** On the Old Lion start the treasury did not hold a shrine's price at once, and the button
said so; six years on, the money was moved to the purse, the shrine was made, and it appeared in the panel as the
head's own with what it does. `tools/browser/wealth.js` now does this.

**Not seen in play.** The event for one of yours bringing a standard home did not fire in the soak: its player's
relatives seldom command a winning fleet. It is proved by a test. The event for wedding plate fired once. Nothing
was sold in the soak, so the sender's answer to a gift sold is proved by a test only. A standard on a flagship and a
gift to another house's head were not looked at in the browser.

718 tests (three new).

**Left, and mapped.** A standard lost is never fought for or asked back; a relative never has anything made; a gift
between houses comes only at a wedding (W9).

## 60. Shares in a person's own name — 2026-11-15

**W1.** A purse held credits only. Now a person buys shares out of their purse, in their own name (docs/WEALTH.md,
"A purse that holds more than credits"). What they pay goes into the purse; they are not the house's and give it no
say; they go by the will. Relatives with a head for money invest for themselves and one in debt sells. Every founder
of a venture now draws a stake from it, which only those who wanted a fortune did before.

**How it is kept.** A person's holding is a holder of the company like any other, under a key of its own, so the
shares are out of the float and the count of a company's shares still adds up. Six places in the company's code
that assumed every holder was a house were changed: dividends, a special dividend, winding up, the yearly check for
holders that no longer exist, the check that someone still holds the company, and the notice of a rival's stake.

**Measured.** The Republic probe (60 runs of 60 years): 3,042 shares bought in a person's name, 73 sold, 7,162
credits paid into purses, 1,741 shares passed on at a death, 124 people holding some at the end; founders drew 441
credits. The Republic stood in all 60; the great war was fought in 45 runs (49); 26 states broke away (30); trust
in the magistrates averaged 51 to 62 by start and was low in 15 runs (15); 1,074 ventures were wound up (1,039).
The soak (33 runs, a save and load in each) passes with no drift; in it 1,304 shares were bought privately and the
event for a relative's first purchase fired often enough that every answer was taken. Survival: 0 of 10 on every
start.

**Seen in the browser.** On the Old Lion start the head moved money to the purse and bought one share of the Bank
of the Reach; the panel then listed it under "Shares of your own" with its worth and a button to sell.
`tools/browser/wealth.js` does this. The screenshot also shows the shrine from W8 and the three heads of other
houses a thing can be given to.

**Not seen.** A relative's line in the purses list saying what they hold, and a will read with shares in it, were
not looked at in the browser; both are proved by tests. The great war was fought in four fewer runs than before:
private buying moves share prices, and through them the houses' money; it was not traced further.

721 tests (three new).

**Left, and mapped.** A venture or a world held outright by a person, and a vote for shares held in a person's name
(W10).

## 61. A failing venture, beyond its owner and beyond one cause — 2026-11-15

**A7.** Five things were missing and all five are built (docs/ECONOMY_MARKET.md, section 13): other houses carry or
sell a failing venture; a company's venture is judged; a failure pays creditors first, costs standing with two
estates and leaves a name for bad business; a venture can be sacked when its world is stormed; and the head can
stake a founder to a second start.

**Measured.** The Republic probe (60 runs of 60 years): 697 carried by their houses, 575 sold before they failed,
469 wound up (1,074), 28 closed by companies and 5 carried, 189 sacked. Ventures standing at the end: 7,064
(7,082). The Republic stood in all 60; 31 states broke away (26); trust in the magistrates averaged 49 to 63 by
start and was low in 15 runs (15). The soak (33 runs, a save and load in each) passes with no drift, and the event
for a sacked venture fired in it. Survival: 0 of 10 on every start.

**The great war is fought less often.** 41 runs of 60, against 45 after the last pass and 49 before that. Two
passes running have lowered it, and both moved money between houses (private share dealing, then houses saving
their ventures). It was not traced. If the fall continues it wants a pass of its own.

**Not seen in play.** Nothing was paid to creditors out of a failure, and no second start was given, in the probe
or the soak. Both are proved by tests. The second-start button, the name for bad business on the Exchange and the
sack event were not looked at in the browser.

726 tests (five new).

**Left, and mapped.** A sacked venture is never claimed at the peace or rebuilt; a company never sells a failing
venture; other houses give no second start (A8).

## 62. A standard asked back, and gifts between houses — 2026-11-15

**W9.** Three things the last pass left (docs/WEALTH.md, the section after "New things"). A standard is now
remembered by the house that lost it: resented, retaken in battle, bought home, or asked for. A relative has a
thing made with their own money. A head who would rather not be at odds sends a gift.

**Measured.** The Republic probe (60 runs of 60 years): 86 standards taken, 23 retaken in battle, 26 bought home,
35 still flown at the end; 71 things made by relatives of the player's house; 269 gifts to mend a quarrel. Houses
holding something they were not born with: 36% (30%). The Republic stood in all 60; the great war was fought in 41
runs (41); 23 states broke away (31); trust in the magistrates averaged 47 to 61 by start and was low in 15 runs
(15). The soak (33 runs, a save and load in each) passes with no drift; in it a house asked for its standard once
and was sent it, 19 standards were bought home, relatives had 34 things made and 11 gifts were sent back. All three
new events fired. Survival: 0 of 10 on every start.

**Tuned.** Gifts at one year in twelve put four houses in ten in possession of something new; at one in thirty it is
36%, a little over the third the last pass aimed at.

**Seen in the browser.** The purses panel, the ambitions panel and the Exchange were driven after this pass and the
last and load without errors. No script reaches a year in which the player holds another house's standard or has
lost one, so the "send it home" and "buy it home" buttons were not seen; nor was the second-start button from A7.

730 tests (four new).

**Left, and mapped.** A gift is always plate and never asks a favour; nothing but a standard is taken in a war
(W11).

## 63. A venture of one's own, and a voice for one's shares — 2026-11-15

**W10.** Two of the three things in the row are built (docs/WEALTH.md, "A venture of one's own"). A venture can be
a person's own, by the head's settling it or by their founding it with their own money: its earnings, the say in
selling it, its price and where it goes at their death are theirs. A founder who does not own the venture has a
fifth of the price when it is sold. And shares held in a person's name vote with their house while they stand with
its head. The third, a world held by a person, is left to the owner as W12.

**Measured.** The Republic probe (60 runs of 60 years, nobody giving orders): 29 ventures were a relative's own at
the end, 928 credits paid over to holders, no founder's stakes (every venture a relative founded there was their
own). The Republic stood in all 60; the great war was fought in 41 runs (41); 23 states broke away (23); trust in
the magistrates averaged 48 to 61 by start and was low in 14 runs (15); 498 ventures were wound up (469). The soak
(33 runs, a save and load in each) passes with no drift. Survival: 0 of 10 on every start.

**Not seen in play.** The soak's player has the command to settle a venture and never gave it in 33 runs, and
never sold a venture a relative had founded. Settling, consent, the price, the will and the vote are each proved by
a test.

**Seen in the browser.** On the New Man start the house's trading company was settled on a relative from the
Exchange: the card read "Livia Crane's own" and the purses list "Has 1 venture of their own", with no errors.

733 tests (three new).

**Left, and mapped.** No world is a person's: the owner's question (W12). A person who owns a venture does nothing
with it themselves.

## 64. Bills at the peace, a company that sells, creditors who foreclose — 2026-11-15

**A8.** Three of the row's four parts are built (docs/ECONOMY_MARKET.md, section 14): a sacked venture is billed
at the peace, a company sells a failing venture, and a house that cannot pay is foreclosed on, which is what makes
creditors real. The fourth, a second start for founders of other houses, cannot be built while other houses have
no founders, and is folded into X1.

**The rule that never applied.** Foreclosure was first written for a house with a loan unpaid and nothing in the
treasury, or a treasury 20 under. Ten runs gave none. Counting showed why: a house is overdrawn in about one
house-year in twenty-five, but almost never deeply, and seldom while it owns a venture. Two years overdrawn running
was added, and the count is kept in the probe.

**Measured.** The Republic probe (60 runs of 60 years): 3,059 credits paid at a peace for what was sacked, 60
struck out; 10 ventures sold by a company and 11 closed; 26 foreclosed on; 181 sacked; 504 wound up (498). The
Republic stood in all 60; the great war was fought in 40 runs (41); 23 states broke away (23); trust in the
magistrates averaged 47 to 61 by start and was low in 14 runs (14). The soak (33 runs, a save and load in each)
passes with no drift: 38 foreclosures in it, and the event for the player's own fired. Survival: 0 of 10 on every
start.

**Not looked at in the browser.** Nothing in this pass has a screen of its own: it shows in the chronicle, the
event and the name for bad business already on the Exchange.

736 tests (three new).

**Left, and mapped.** Foreclosure takes a venture and nothing else; a bill at the peace is for ventures only (A9).

## 65. Plunder, and a gift that asks — 2026-11-15

**W11.** Both halves are built (docs/WEALTH.md, "Plunder, and a gift that asks"). A stormed world's governor can
lose a signet, a sword, a library or a standard to the commander, and everything W9 gave a taken standard (the
grudge, retaking, buying home, asking) now holds for any taken thing. A gift can be an orrery sent to ask a
favour; a favour owed leans a house two points on the proposer's next motion; the player asks one by giving, and is
judged by their vote when they keep one.

**Measured.** The Republic probe (60 runs of 60 years): 47 things carried off, 68 taken things still held at the
end, 40 gifts that asked a favour and 40 favours spent on a vote. Houses holding something they were not born with:
36% (36%). The Republic stood in all 60; the great war was fought in 41 runs (40); 24 states broke away (23); trust
in the magistrates averaged 47 to 61 by start and was low in 14 runs (14). The soak (33 runs, a save and load in
each) passes with no drift: 21 things carried off, 24 favour gifts, and its player kept one favour and broke one.
Both new events fired. Survival: 0 of 10 on every start.

**Not seen in play.** No broken house surrendered a signet. It is proved by a test.

**Seen in the browser.** The shrine was given to the head of another house and the purses panel then read "Owe you
a favour: House Aurelian". A thing carried off, and an orrery arriving, were not seen there.

739 tests (three new).

**Left, and mapped.** A favour is a lean on one vote and nothing else; other houses never refuse a gift (W13).

## 66. A pledge, foreclosure beyond a venture, and a bill for a blockade — 2026-11-16

**A9.** All of the row that can be built is built (docs/ECONOMY_MARKET.md, section 15). The head names what the
creditors take first. With no venture left they take an unentailed thing, and at the last a world against a named
lender's loan. A blockade that fails is billed at the peace. A bill for a fleet lost was not added: the tribute a
peace carries already follows the score of the fighting, and a second charge for the same thing would be double.

**Measured.** The Republic probe (60 runs of 60 years): 27 ventures and 46 things taken by creditors, no world;
780 credits billed for blockades; 4,192 paid at a peace (3,059), 66 struck out. The Republic stood in all 60; the
great war was fought in 42 runs (41); 23 states broke away (24); trust in the magistrates averaged 46 to 62 by
start and was low in 14 runs (14). Survival: 0 of 10 on every start.

**The soak ran on a later build than the probe.** A browser check showed that settling a pledged venture on a
relative left the pledge standing. One line now clears it. The probe had already run; it gives no orders, so
nothing in it can pledge or settle and its figures are unaffected. The soak (33 runs, a save and load in each) was
stopped and run again on the final build: it passes with no drift, with 34 ventures, 31 things and one world taken,
and four pledges by its player.

**Seen in the browser.** On the New Man start the house's trading company was pledged: its button then read
"Pledged to the creditors". A thing or a world being taken was not seen there.

742 tests (three new).

**Left, and mapped.** Only the head's own things are at risk; other houses never pledge; a world taken is never
redeemed (A10).

## 67. A favour called in or forgotten, and a gift refused — 2026-11-16

**W13.** Built (docs/WEALTH.md, "A favour called in or forgotten, and a gift refused"): a favour owed to the player
can be asked for money or for consent to a match; any favour lapses after eight years; and a head who thinks ill
enough of the sender refuses a gift, as the player always could.

**Measured.** The Republic probe (60 runs of 60 years): 44 gifts refused and 256 taken; 27 favours owed and 27
spent on a vote, none lapsed. Houses holding something they were not born with: 34% (36%). The Republic stood in
all 60; the great war was fought in 42 runs (42); 23 states broke away (23); trust in the magistrates averaged 47
to 61 by start and was low in 14 runs (14). The soak (33 runs, a save and load in each) passes with no drift: 19
gifts refused, one favour lapsed. Survival: 0 of 10 on every start.

**Not seen in play.** No favour was called in for money or a match: the probe gives no orders, and the soak's
player was seldom owed one. A favour asked by a house with a motion open is spent within the year, so lapsing is
rare too. All three are proved by tests.

**Seen in the browser.** After the shrine was given to the head of House Aurelian the panel showed both buttons,
and the money one was greyed with its reason: that house had not the money to give.

744 tests (two new).

**Left, and mapped.** Other houses never call a favour in for anything but a vote, and a favour is never passed on
(W14).

## 68. The full QA — 2026-11-16

Written up on its own page: docs/QA.md. In one line: nothing is broken that a player would meet, and a third of
what has been built is not reached by play, the test player or the unattended world. Seven small defects were fixed
in the pass (a screen that scrolled sideways on a phone, a raid remembered for ever, five analyser warnings, a stale
README and roadmap, a probe that printed zeros for starts it skips, a row missing from the owner's list). Fourteen
rows were added to the register (Q1 to Q14) and none closed. 744 tests.

## 69. Six known stories played against the game — 2026-11-17

Written up on its own page: docs/ROLEPLAY.md. Darrow, Reinhard von Lohengramm, Yang Wen-li, Nero, Palpatine and Paul
Atreides were each cut into their beats (47 in all) and each beat was checked against the engine. Five beats can be
played today, 27 partly and 15 not at all; the page has the count by story. In one line: the game is good at
getting to power, and thin at life under a throne (C8), at rising by service (Q15, R1), at anything hidden (R2) and
at losing (R3, R5, R11).

**One defect found and fixed.** The title ladder's last rung was the throne, so seizing a title could make a house
"emperor" in a Republic that carried on: no proclamation, no change of era, nobody deposed. Reproduced on The Old
Lion in two years. `ProgressionSystem.NextTitle` now stops below the throne; one new test, 745 in all.

**Mapped.** Fifteen rows, R1 to R15. R8 and R9 are forms of government and R10 and R13 are new systems, so those four
wait on the owner with G1 to G7. Nothing else was built in this pass: it was a reading of the game, not a change to it.

## 70. The Institute remembered, and Nero au Augustus — 2026-11-17

**The owner's answers.** "Nero" was Nero au Augustus, and is now on docs/ROLEPLAY.md (3 beats can be played, 6 partly,
2 not; rows R16 to R18). Yes to the blood feud (R4), emergency powers (R8) and a throne that can be lost (R5); more to
do as emperor and as the heir (R6, R7) is a later expansion; a faith and castes (R13) are not for now; and how well
you did at the Institute is to be remembered, winning most of all.

**Built: the Institute remembered** (docs/ACADEMY.md, "What is remembered"). Laurels on the player and on every
classmate, read by renown, elections, the first posting and promotion, a fleet's crews and command cap, the year's
regard for one another, and six later events.

**Found while measuring.** The Institute was not a contest. Answering every report with its first choice, the player
won it as captain in 30 years of 30 and was first of the year in 30 of 30; answering with the last choice, still won
in 27. The rival Standards finished on the same strength in every game and the classmates on the same scores: nothing
in the year took its dice from the world. Fixed: each year has its own dice, each rival Standard a captain worth
nothing, a little or a good deal, and classmates score more.

**Measured** (30 years each way).

| How the cadet answers | Won as captain | First of the year | Honours | Near the bottom | Own banner taken |
|---|---|---|---|---|---|
| Boldly | 10 | 13 | 22 | 0 | 9 |
| By guile | 14 | 0 | 4 | 15 | 5 |
| Carelessly | 0 | 0 | 0 | 30 | 14 |

In the Republic's Academy the bold cadet is first of the year in 15 of 30. The soak (33 runs, a save and load in
each) passes with no drift; four lines ended (5 to 7 before); 289 of 388 events fired. Of the six new events five
fired; `second_of_the_year` did not. Survival: 0 of 10 on every start.

749 tests (four new).

**Left, and mapped.** Only the player's own year is remembered (E1). Nothing under a throne offers the year's captain a
place or a command (R1).

## 71. Blood between houses — 2026-11-17

Built on the owner's word (docs/FEUD.md; R4 closed, F3 half closed). One death, one debt, held by the dead person's
house against a person to blame; ended by blood, a duel, a price, or being let lie.

**Found while measuring.**
- With the rules in and nothing else changed, ten unattended games held four feuds, all of them the player's own
  (kin killed in an event). No house but the player's ever killed anyone outside a battle, and no commander who died
  in battle was a head or a head's near blood. So hard-natured heads now send a knife at a house they hate.
- With a war counted as cause there were five feuds a game and eight knives. A war alone is no longer cause.
- A house whose challenge was refused challenged again every time it acted, for ever. Nobody is called out twice now;
  after a refusal what comes is a knife.
- A price was almost never paid, because a house had to hold the sum twice over. It pays now with half as much
  again, or if the house asking is no weaker, or if its head is a just or honourable one.

**Measured.** The probe (60 runs of 60 years): 143 debts begun in 58 runs (a knife 64, a death in battle 41, by hand
38); ended on the field 37 (22 deaths: the rule is four in ten, and the dice were checked fair over two thousand
trials), in blood 21 (once the wrong blood), bought off 14, let lie 56 (30 of them the probe's own player, who gives
no orders), 9 standing; the field refused 15 times; 145 knives sent between other houses. The Republic stood in all
60; the great war was fought in 43 runs (42); 22 states broke away (23); trust in the magistrates averaged 48 to 60 by
start and was low in 11 runs (14). The soak (33 runs, a save and load in each) passes with no drift: 66 debts begun,
20 ended on the field, 10 in blood, 5 bought off, 21 let lie; four lines ended (4); 302 of 395 events fired (289 of
388) and 72 kinds of command landed (70).

**Survival: 1 line of 80 ended. It was 0.** The Risen Officer, seed 4: the only child of the house was killed at 18
in an event that was there before this pass, and the head died at 57 with nobody to follow. The feud rules recorded
the death and did not cause it, but this pass reshuffled the dice and this is what they landed on.

**Not seen in play.** Three of the seven events did not fire in the soak: the price asked, called out, and after the
field. Each needs the player's house to owe blood or to fight, and the soak's player seldom kills and is seldom
traced. All three are proved by tests, and the panel was driven in the browser.

**Seen in the browser** (`tools/browser/feud.js`). On The Old Lion an accident arranged in the second year was laid
at my door: the You screen showed "Blood between houses, 1 held against you", who was dead, that it was held against
me by name, and the offer of the price greyed out with its reason: the house had not the 60 credits.

761 tests (twelve new).

**Left, and mapped.** A marriage or a mediator cannot end a feud, nobody takes a side, and a relative called out
cannot be forbidden or stood in for (R19). A knife that is not traced leaves nothing behind (R2).

## 72. Emergency powers — 2026-11-17

Built on the owner's word (docs/EMERGENCY.md; R8 closed). The Senate lends a consul powers for three years when the
Reach is in danger, and they are laid down, extended, kept, stripped, or, for the player alone, asked for life.

**Found while measuring.** As first built the Senate gave them whenever it was asked: 55 of 60 motions carried, three
times a game, and a consul held them seven or eight years of every sixty. The Reach is "in danger" two years in five,
mostly from wars at its edge. So no Senate likes to do it now (two points against, to begin with), other houses move
it a third as often for a lesser danger as for a war for the Reach itself, and it cannot be moved again for twelve
years.

**Measured.** The probe (60 runs of 60 years): the Reach in danger 39% of years; the powers moved 116 times, voted 53
(in 40 runs) and refused 58; 168 years held, 43 levies; 37 laid down and the rest lapsed with the holder or still
running; none kept and none stripped, since no house but the player's keeps them; and the Republic stood in all 60. The
great war was fought in 43 runs (43); 20 states broke away (22); 146 debts of blood (143); trust in the magistrates
averaged 49 to 60 by start and was low in 13 runs (11). The soak (33 runs, a save and load in each) passes with no
drift: 40 moved, 24 voted, 16 refused, 19 levies, 16 laid down; two lines ended (4); 300 of 403 events fired (302 of
395) and 71 kinds of command landed (72). Survival: 1 line of 80, the same one as before (The Risen Officer, seed 4).

**Not seen in play.** The soak's player was never voted the powers, so five of the eight events did not fire (the
powers yours, the term up, a year past leave, laid down by you, stripped) and nothing was kept or asked for life. All
of it is proved by tests, including a motion for life moved by the player and carried on the Senate's own count.

**Seen in the browser** (`tools/browser/emergency.js`). On The Old Lion the consulship panel said in year 15: "The
Reach is in danger: a state in arms against the Reach. The Senate may be asked to give a consul its emergency powers
for 3 years." No consul was voted them in the 45 years of that game, so the holder's strip was not seen on screen.

770 tests (nine new).

**Left, and mapped.** The Senate cannot revoke them early; the holder commands no fleets by them and decrees nothing;
no other house manufactures the danger or keeps the powers (R20).

## 73. A throne that can be lost: planned — 2026-11-17

The fourth thing the owner said yes to is several cycles, so it is planned before it is built (docs/THRONE.md). Four
stages: the hold on a throne and a pretender who acts when it is weak; a succession that can be contested; the Guard;
and what a fallen emperor can do. Five assumptions are written down to be corrected, the first of them that a player's
throne can be lost to another house's doing and not only to the player's own mistakes. Nothing is built.

## 74. A throne that can be lost, stage 1; and the powers beyond the Reach, planned — 2026-11-18

**The owner's word.** The assumption stage 1 rests on "makes sense": a player's throne can be lost to another house's
doing, and losing it does not end the game. And: "let's work on the off-map stuff as well; at least a good plan."

**Built: the hold, and a pretender** (docs/THRONE.md, "Stage 1 as built"; R5a closed, R5 keeps stages 2 to 4).

**Found while building.**
- A house on the throne that lost its last world became landless and the throne stood empty for good. It was not
  reachable before, because nobody attacked a throne. Whoever takes that world now takes the throne, and an empty
  throne is filled.
- The epilogue was written whenever any house took a throne, in words that say the player did it.
- A rising's war was ended by the ordinary peace-making within a few years, eight times in fourteen. A war for the
  throne is no longer ended by treaty.
- A house with a claim but below ducal rank could rise and could never win: a seizure asked for a duke. A claim will
  do now.
- A seizure asked the taker to outnumber every house in the Reach by a quarter. Against a throne it asks now that the
  throne's own fleet be outmatched.

**Measured.** `praetoria throne`, 10 games of 60 years for each start under a throne, each played as written and with
the player's house crowned in the first year:

| | As written (20 games) | A knight's house crowned (20 games) |
|---|---|---|
| The hold, on average | 59 to 62 | 27 to 34 |
| Shaken or worse | 26 to 33% of years | 61 to 76% of years |
| Pretenders risen | 32 | 86 |
| Took the throne | 3, in 3 games | 15 |
| Broken | 21 (14 by the eight years) | 46 (39 by the eight years) |
| The player cast down | | 20 of 20, after 5 to 12 years |

Most pretenders fail by running out of years without winning the war by five. Under the Republic nothing moved: the
probe's sixty games give the same figures as before to the unit (the Republic stood in all 60; 146 debts of blood;
emergency powers voted 53 times). The soak (33 runs, a save and load in each) passes with no drift: 11 pretenders, 1
throne taken, 8 broken; two lines ended (2); 307 of 409 events fired (300 of 403) and 71 kinds of command landed (71).
Survival: 1 line of 80, the same one as before.

**Not seen in play.** Three of the six events need the player on a throne, which the soak's player never is: the hold
shaken, a standard against you, cast down. All three are proved by tests, as is the whole fall of a player's throne.

**Seen in the browser** (`tools/browser/throne.js`). On The Imperial Court the Senate screen's tile read "The throne
100 · firm, the hold of House Aurelian", and the throne's block listed what the hold is made of (standing +22, 30 in
a hundred of the hulls +8, the capital +10, twenty years +10) with the three buttons greyed and their reasons: a
duke at least or a house with a claim. Nobody raised a standard in the 30 years of that game.

778 tests (eight new).

**Planned: the powers beyond the Reach** (docs/BEYOND.md). Five stages: a door on the rim, named rulers and
commanders and a probe line, on the map as it is; their fleets on the map; their side of the border; provinces; their
politics. Four questions are the owner's before the second stage. Nothing is built.

**Left, and mapped.** Sides in a rising are taken by regard and blood only, and houses that go over fight their own
wars and not one campaign. A succession is not contested, there is no guard, and a fallen emperor has nowhere to go
(R5, stages 2 to 4).

## 75. The powers beyond the Reach, stage 0: a door and a face — 2026-11-18

Built the same day it was planned (docs/BEYOND.md, "Stage 0 as built"; R10a closed, R10 keeps stages 1 to 4). It
needed no new system: marches are systems already on the map, and a nation's people are names, ages, a skill and a
nature held on the nation.

**Found while measuring.**
- As first built, every game had the same rulers dying in the same years: a nation's dice took nothing from the
  world. It is the flaw the Institute had. Each nation's people are salted from the world's own dice now.
- Nearly six rulers in ten ended up warlike, because a commander who took the chair was always warlike and did so
  half the time. One time in three now, and never mild but not always warlike.
- Other houses almost never held a march (41 raids met in 60 games). A house that has been raided now posts a fleet
  there (170).
- "Grasping" did nothing. A grasping ruler's raiders take twice the cargo.

**Measured.** The probe (60 runs of 60 years): 521 raids landed, 170 met at a march (a fleet posted to one 54
times), 45 turned back at the world; 81 wars voted, 250 expeditions, 550 battles, 23 nations subdued; 82 of their
commanders killed; 643 rulers succeeded; rulers at the end grasping 114, warlike 88, mild 60, wary 38. The soak (33
runs, a save and load in each) passes with no drift: 129 raids met at a march, 59 commanders killed, 291 rulers
succeeded; three lines ended (2); 312 of 412 events fired (307 of 409) and 70 kinds of command landed (71). Survival:
0 of 80 (1).

**The Republic fell once.** In one game of the sixty (The Fallen House, year 53) a general of another house passed
all three gates of the Caesar road, marched, and took the throne. In every earlier measurement the Republic stood in
all sixty. Nothing in this pass touches the Caesar road; the dice were reshuffled and a general in that game had
nine victories to their name. It is inside the owner's rule (another house's Caesar no more than once in ten games)
and it bears on Q1, which the owner has still to answer. The other figures: the great war fought in 41 runs (43); 20
states broke away (20); 140 debts of blood (146); emergency powers voted 47 times of 96 (53 of 116); trust in the
magistrates averaged 46 to 62 by start and was low in 11 runs (13).

**Not seen in play.** "Met at the march" did not fire in the soak: it needs the player's own fleet to be the one
standing there, and the soak's player never posted one. It is proved by a test.

**Seen in the browser** (`tools/browser/beyond.js`). On The Old Lion the Military screen's "Beyond the Reach" showed
five rows, among them: "The Vashti Khanates · Khan of Khans Temur Vash, warlike by nature · the fleets under Noyan
Borte Khal, skill 3 of 9 · Its march: The Anvil and The Veil · 2 year(s) out from your nearest fleet · unguarded".
The page was not wider than the screen and there were no errors.

783 tests (five new).

**Left, and mapped.** A march is named on the Military screen and not marked on the map. A nation's people cannot be
met, married, bribed or captured. The safe question for "which houses are of the Reach" was moved to stage 1, where
it is first needed (R10).

## 76. The Vashti Khanates on the map — 2026-11-18

The owner: "let's focus on adding in nations, then we can figure out the details. Add in the khanate first." Built
the same day on the assumptions written in docs/BEYOND.md (R10b closed; N1 to N8 mapped). The rules are in
docs/BEYOND.md, "The Vashti Khanates on the map, as built".

**What it took.**
- **A place.** A fourth ring and one region, the Vashti Steppe: three systems and five lanes west of The Anvil and
  The Veil. 34 systems, 45 lanes, 11 regions.
- **One question for "is this house of the Reach".** 111 places left out only the raiders' house. They now ask
  `World.Outsider`, which is true of the raiders and of the house that stands for a nation. A nation's house is named
  for the nation, so the question needs no world in hand. `LivingCharacters` leaves a nation's ruler and commander
  out, so nothing in the Reach's own life can pick them up by accident; `Foreigners` lists them.
- **The Reach's rules kept out of the Steppe.** Allegiance, raider nests, the great crisis and the regional standing
  of the magistrates walk `ReachRegions`, and no region state is kept for a region beyond the rim.
- **War in the form the raiders have.** `World.AtWar` and `EnemiesOf` answer for a nation at war without a war
  record for each pair of houses.

**Found while measuring, and changed.**

| What the first runs showed | What was done |
|---|---|
| The host walked from world to world taking each in two years. The Reach won 4 battles of 40 and ceded 11 worlds in ten games | A siege takes a host two years longer. One world a season: it goes home with what it took. It needs supply |
| A house that freed a neighbour's world kept it | A world freed goes home to the house it was taken from |
| Squadrons of one to three hulls were fed to a host of ten, one house at a time | The houses of the Reach fight a nation's fleet as one. A house goes when what the Reach could bring matches what is there |
| A squadron swatted counted as much as a host beaten | It counts one; a host beaten counts two |
| The Reach held half the Khanates' worlds at the end of every game | A world of theirs rises after three years without a fleet over it |
| The battles the Khanates won were not counted (a comment had swallowed the line) | Fixed; a test now reads the count |
| Hulls grew back, and ungarrisoned years ran, twice a year | The yearly rules run once; a test counts the years |
| The soak crashed in every start: its random player picked a region by an index into the wrong list | Fixed in the tool |
| Stations and claims were counted as worlds ceded | Only worlds are counted |

**Measured** (60 unattended games of 60 years; the player gives no orders).

| | |
|---|---|
| Wars with the Khanates | 46: the Reach won 36, lost 8, 2 still being fought |
| Their host through the march | 63 times |
| Fleets of the Reach sent against them | 469 |
| Battles on the map | 169; the Reach won 67 |
| Worlds of the Reach taken | 25: 17 freed, 1 given back at a won peace, 7 ceded; 3 in their hands at the end |
| Their own worlds taken | 77; 43 rose again |
| Their capital taken; the nation ended | 0; 0 |
| The Republic fell | 0 of 60 |

By start, wars won and lost: the Academy 7 and 4; the Circling Heir 8 and 1; the Fallen House 7 and 0; the New Man
3 and 1; the Old Lion 7 and 1; the Risen Officer 4 and 1.

**Checked beside it.** The throne probe (20 games as written, 20 with the player crowned): 16 pretenders as written,
3 thrones taken; nothing in it changed in kind. The soak: 33 games, no failures, 309 of 422 events fired. Four lines
ended under its random player, against two in the last two soaks; the survival check (a player who gives no orders,
80 games) lost none. Of the ten new reports, three fired often, two once each, and five (the ones that need the
player's own fleet or world in the war) never fired for a player who does not go to it.

**In the browser.** The Galaxy screen shows the Steppe west of the Western Marches and lists it under "beyond". The
Military screen's card for the Khanates reads "on the map", with where the Khan sits, what they hold and where both
fleets are. No errors; nothing wider than the screen.

794 tests (eleven new, `tests/KhanateTests.cs`).

**Left, and mapped.** N1 (their raids are still rolled), N2 (they never begin a war), N3 (how hard they should be),
N4 (no economy), N5 (the houses behind the leader), N6 (the map marks no march or border), N7 (the Senate directs
nothing), N8 (what is taken is a house's, not a province). R10 keeps the other four nations.

## 77. The Free Cities of Thessar on the map — 2026-11-18

The owner: "Add the Free Cities of Thessar next." Built the same day (R10c closed; N9 to N12 mapped). The rules are
in docs/BEYOND.md, "The Free Cities of Thessar on the map, as built".

**The aim** was a second nation that is not the first one again. The Khanates are an aggressor whose host comes
through the march. The Cities are rich, friendly and lightly armed, and would rather buy than fight: they stay at
home, hire when attacked, sue early, buy their cities back, pay tribute only while watched, and buy off a war
nobody presses.

**What it took beyond content.**
- **The on-map rules for any number of nations.** Counters and the year's report flags are kept for each nation
  ("key:nation") as well as for the Reach. "A nation's own space" is the systems of its own worlds, where the
  Khanates' code said "the fourth ring": with two nations beyond the rim that would have sent the Khan's host
  after a fleet off the Shore.
- **A nation's fleets fight by its temper**, as they did off the map: a mercantile nation at four-fifths, a
  defensive one at five-quarters.
- **Three things added for both nations**: plunder for a fleet beaten and a world taken; the other houses gather at
  the march before attacking a defended world; and "fit the whole map" allows for a narrow panel.

**Found while measuring, and changed.**

| What the runs showed | What was done |
|---|---|
| The Cities won 42 battles of 56: their fleets sat behind their walls and the Reach's squadrons arrived one at a time | Houses gather at the nearest march and go in together when they are a quarter more than what holds the world. For the Khanates too: the Reach's share of battles won went from 40 to 45 in a hundred |
| The weaker fighting value by itself changed almost nothing | Kept, as the off-map figure; the gathering was the cause |
| With gathering, 53 forced wars of 119 guttered out after twelve years, fleets waiting at the march | The Cities buy off a war nobody is fighting |
| Bought off after four quiet years, 113 wars of 134 ended before a fleet of the Reach had reached the Shore | The four years run only while nobody presses them: no fleet of three hulls at their march, over their cities, or on its way. A war that is pressed and not fought is bought off at twelve |
| No house moved a war for plunder, though most houses have under 40 credits | Left, and mapped (N9). The houses that would move it are rarely the ones whose turn it is to move anything |

**Measured.** Sixty unattended games as they come, and sixty with the Senate's war on the Cities made for it in year
8 and every twenty years after. Sixty years each; the player gives no orders.

| | The Cities, war made for the Senate | The Khanates, left to themselves |
|---|---|---|
| Wars | 124 | 37 |
| Won by the Reach; lost | 50; 10 | 28; 4 |
| Bought off | 58 | not their way |
| Battles; won by the Reach | 193; 67 | 143; 65 |
| Their fleet through the march | 37 times | 55 times |
| Worlds of the Reach taken; freed; ceded | 17; 6; 8 | 26; 19; 4 |
| Their worlds taken | 49: 42 bought back, none rose | 57: 38 rose again |
| Plunder | 1,485 credits | 716 credits |
| Hulls hired | 155 | none |
| Tribute stopped unwatched | 34 times | never |
| Their capital taken; the nation ended | 0; 0 | 0; 0 |
| The Republic fell | 0 of 60 | 0 of 60 |

Left to themselves: no war with the Cities in any of the sixty games, and none moved.

**Checked beside it.** The throne probe ran clean (as written, 37 pretenders in 20 games and 6 thrones taken). The
soak: 33 games, no failures, 310 of 430 events fired, two lines ended under its random player. The survival check
(a player who gives no orders, 80 games) lost none.
None of the Cities' eight reports fired in a soak, because no war with them happens there; the ransom is tested
through a real game's turns instead (`TheRansom_IsAskedByAReport_AndTheAnswerIsCarriedOut_InARealGame`).

**In the browser.** The Galaxy screen shows both regions beyond the rim and lists them under "beyond". The Cities'
card on the Military screen reads "on the map", says in a line what kind of neighbour they are, and lists both
fleets. No errors; nothing wider than the screen.

805 tests (eleven new, `tests/ThessarTests.cs`).

**Left, and mapped.** N9 (no house but the player's makes war on the Cities), N10 (the ransom is answered through a
report, at a fixed price), N11 (the Cities buy only two things), N12 (their trade is not on the map).

## 78. The Ardent Concord on the map — 2026-11-18

The owner: "Add the Ardent Concord next. After we add the nations you give me a list of things to decide." Built the
same day (R10d closed; N13 to N15 mapped). The rules are in docs/BEYOND.md, "The Ardent Concord on the map, as
built"; the list is at the end of that document.

**The aim** was the third kind of neighbour. The Khanates raid and go home. The Free Cities buy. The Concord is the
strong, unified state: it begins wars itself, it invades on two lines, and it stays.

**What it took beyond content.** Four switches a nation's definition can set, so any nation can be made this way:
how many hosts it sends (`hosts`), whether it begins wars (`strikes`), whether its hosts stay in the field
(`campaigns`), and how long a world of its own stays held without a fleet (`holdYears`, 0 for never rises). A host's
"home with what it took" is kept for each fleet, where it was kept for the nation, so one division taking a world
does not send the other home. A war the nation began is marked, and every house will go to drive it out of the
Reach.

**Found by the soak, and older than the Concord.** Three Imperium games in thirty-three did not resume the same from a
save. The cause was in the Senate: the houses declared their votes in the order they happened to be held in memory,
and a house leans with its bench's leader when the leader's vote is already cast. A house made after the save (a
free hold, here) sat in a different place in the resumed game and voted on the other side of its leader. The
Concord's wars did not cause it; they made worlds change hands more, which made and folded more free holds. The
leaders of the benches now declare first, then the rest by name. A new tool, `praetoria drift`, plays one game
straight and once resumed and reports the first year they differ; it gave the year and the vote. All three games
resume the same now.

**Found on the way.** The player's answer to a ransom was cleared at the end of each nation's yearly reckoning. With
two nations on the map the Free Cities happened to be reckoned first; with three, the Concord was, and the answer
was gone before the Cities read it. Two tests caught it. The answer is cleared once, after all the nations.

**Found while measuring, and changed.**

| What the runs showed | What was done |
|---|---|
| With twelve years between its wars the Concord began 73 in sixty games and was at war for much of every game | Twenty-five years between its wars: one invasion a generation. It began 63: its wars are long, so the rest was already near the limit |
| Everything else held: no crash, no civil war, no general marched, the Republic stood | Left as measured, and the weight of it put to the owner (decision 5) |

**Measured** (60 unattended games of 60 years; the player gives no orders).

| | The Ardent Concord | The Vashti Khanates | The Free Cities |
|---|---|---|---|
| Wars | 84 (63 begun by it) | 24 | 1 |
| Won by the Reach; lost | 41; 24 | 9; 2 | 0; 0 |
| Battles; won by the Reach | 876; 383 | 59; 24 | 3; 1 |
| Worlds of the Reach taken; freed; ceded | 150; 95; 47 | 10; 8; 1 | 1; 1; 0 |
| Its own worlds taken | 58 | 21 | 0 |

By start, the Concord's wars, those the Reach lost, and worlds ceded: the Academy 14, 3, 6; the Circling Heir 12,
3, 3; the Fallen House 18, 9, 20; the New Man 14, 4, 7; the Old Lion 14, 4, 9; the Risen Officer 12, 1, 2.

Battles in a game went from about 11 to about 24, and the years the Reach is "in danger" from one in two to three
in five. The Republic fell in none of the sixty.

**Checked beside it.** The throne probe ran clean (as written, 18 pretenders in 20 games and 4 thrones taken; a
standard raised against a throne is one of the things that divides the Reach). The soak: 33 games, no failures, 312 of 433
events fired. Five lines ended under its random player, where two to four is usual; the soak now says why, and in
each the head of a one-person house died of age with no heir (Q13, closed: not the Concord, and not a fault). The
survival check (a player who gives no orders, 80 games) lost one, the same start it has lost before.

**In the browser.** The Galaxy screen shows all three regions beyond the rim. The Concord's card on the Military
screen says that it begins wars and why, and lists its home fleet and both divisions. No errors; nothing wider than
the screen.

811 tests (six new, `tests/ConcordTests.cs`).

**Left, and mapped.** N13 (nothing warns of a strike), N14 (one commander however many fleets), N15 (it strikes only
when the Reach is divided by its own quarrels; one war beyond the Reach at a time). Twelve decisions for the owner
are listed at the end of docs/BEYOND.md.
