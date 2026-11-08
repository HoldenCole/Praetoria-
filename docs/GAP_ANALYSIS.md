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
