# Praetoria v0.5.0 — "headless complete"

**Date:** 2026-11-07 · **Tag:** `v0.5.0` · **Scope:** engine + content, no UI (a deliberate decision: the
session that produced it had no Godot runtime, so the Court UI stays Milestone 3).

0.5 means: *every core system of the human game exists, is wired to the others, is fed by data, is
deterministic from a seed, saves and loads, and can be played end-to-end from the console across five
starting scenarios.* What it does not mean: a human can play it without reading text in a terminal.

## What's in 0.5

### Systems (all headless, all tested)
- Event engine (role-binding, eligibility, Director pacing, consequence chains) · turn loop with action pools
  and a shared player/NPC command bus · five-resource domain economy with holdings, buildings, population
  and unrest · gate/cascade/damper crisis system with organic and authored onset, `once` and `onResolve` ·
  power-balance spheres with threat and coalition pressure · title ladder with the legitimacy soft-lock and
  the three rise paths · career ladders as data with rank caps · dynasty lifecycle (aging, death, birth,
  succession, voluntary handover, regency flag, stress bleed-off and break point) · ironman and continue
  modes · save/load with a versioned envelope · NPC houses that act, invest and light crises.

### Content
- **76 events** with paired prose across seven themes (academy, career, steward, coalition, succession,
  Imperial court, echoes), every chain closed — no flag is written without a reader except engine markers.
- **5 scenarios**: the academy fixture, the three rise-path openings (fallen house / circling heir / risen
  officer) and the Imperium-era *Imperial Court*. Every character sexed, every house titled; two casts start
  married so heirs arrive from turn 1.
- **5 crises**, **12 specializations**, **12 buildings**, **3 spheres**, **7 title rungs**, **3 career ladders**;
  locked vocabularies (v1.1).

### Tooling
- Console harness: `play` (interactive/auto, any scenario, `--save`/`--load`, `--continue`), seven system
  demos, `validate`, `version`.
- Two content validators (engine gate + stricter Python pre-flight), both in CI; the pre-flight audits flag
  chains in both directions and rejects events with no ungated choice.
- 138 tests including a 40-turn whole-stack soak and a save/load round-trip.

## Changes since the engine branch's last commit (both passes, 2026-11-07)

**Fixed:** track gating for career events (new `track` condition) · uncapped ranks (careers catalog) ·
one-way marriages invisible to births · the run continuing after dynasty death · all-gated events re-offered
forever · unspent claims and self-demoting abdication (`revokeClaim`, `succeed`) · `owes_you` cleared instead
of armed · `realm_unstable` never clearing · stale guide, missing CI gate, duplicate root uploads.

**Added:** save/load · stress bleed-off and break point · population → Manpower with per-spec caps · `era`
condition and the Imperium start · continue mode · regency flag · NPC domain investment · relationship
index · 14 events (3 chain-closers, 3 Imperial court, 8 echoes) · version stamp 0.5.0.

## What 0.5 is *not* — the road to 1.0

| Milestone | Status after 0.5 | What's left |
|---|---|---|
| 3 — Court UI | ⬜ | The whole presentation layer. Needs a Godot session. |
| 4 — Galaxy mode | ◐ | Star map, systems, hyperlanes, fleets, mixed visibility. No military unit model exists. |
| 5 — Crises/spheres/progression | ◐ | Bloody-Event set-pieces as a *class* (one exists); Honorifics & Orders; sphere inputs beyond careers. |
| 6 — Human endgame | ◐ | Proclaiming/seizing the Imperium (the era *flip*), the Great-Crisis framework, the saga epilogue. |
| 7 — Reactive UI, polish, content depth | ⬜ | Theming, portraits, audio, balance pass, 40–60-event opening act. |

Design debts carried into 0.6: stress is a flat loop (no per-trait charging); regency is a flag without a
regent; the threat formula is coarse at small absolute scales; the imperial `legitimacy` counter has no
natural recovery; NPC AI is a handful of verbs.

## Verify this release
```bash
dotnet run --project src/Tools -- version
dotnet build Praetoria.sln -c Release && dotnet test Praetoria.sln -c Release --no-build
dotnet run --project src/Tools -c Release --no-build -- validate
python3 validate_praetoria.py
for s in academy_crucible the_fallen_house the_circling_heir the_risen_officer the_imperial_court; do
  dotnet run --project src/Tools -c Release --no-build -- play --auto --scenario $s --seed 1 --turns 40 > /dev/null || echo "FAIL $s"
done
```
