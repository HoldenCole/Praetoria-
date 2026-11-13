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

