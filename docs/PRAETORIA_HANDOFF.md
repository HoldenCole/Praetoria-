# Praetoria — Content Handoff (Priorities A–E)

A validated vertical slice spanning all five authorable priorities. Everything here is grounded in
`CONTENT_GUIDE.md` and uses only the documented vocabulary. Two files accompany this one:

- **`praetoria_content.json`** — all content merged into one bundle (events, texts, holdings, scenarios)
- **`validate_praetoria.py`** — the pre-flight validator (run before the engine's `validate`)

---

## 1. What's in the bundle

| Priority | Section | Count | Notes |
|---|---|---|---|
| A | Academy events | 17 | Two flag-chains: rivalry (slight→sabotage→reckoning→aftermath) and bond (watch→blooding/oath). Trait-crystallizer setpiece. |
| B | Steward/economy events | 15 | All 5 resources covered; debt-chain; `unrest`/`corruption`/`goodwill` counters. |
| C | Career events | 15 | Military/stewardship/law arcs; gate on rank/skill/trait; call `advanceCareer`. |
| D | Holdings catalog | 15 | 7 specializations + 8 buildings, balanced per §17 (each resource decisive somewhere). |
| E | Scenarios | 3 | The three rise-paths: fallen house / circling heir / risen officer. |

**Totals:** 47 events, 47 text records, 7 new specializations, 8 new buildings, 3 scenarios — 0 validation errors.

---

## 2. Integration

The bundle is a convenience container. The engine loads `*.json` from `/content/{events,text,holdings,scenarios}`,
so split the bundle into those directories (or load it directly if you add a bundle loader). Suggested split:

```
/content/events/      academy_crucible.json, academy_crucible_2.json,
                      steward_decisions.json, steward_decisions_2.json,
                      career_progression.json          (the "events" array, grouped by theme)
/content/text/        <same theme names>.en.json       (the "texts" array)
/content/holdings/    specializations_ext.json, buildings_ext.json
/content/scenarios/   the_fallen_house.json, the_circling_heir.json, the_risen_officer.json
```

File names are cosmetic (the engine loads all `*.json` per dir); group by theme for sanity.

**The new holdings are in *_ext files with fresh ids** — they do NOT redefine the 5 existing specs
(`agri_world, forge_world, trade_hub, fortress, research_station`) or 4 existing buildings
(`power_plant, mine, farm_complex, market`). No id collisions.

### Validate before shipping
```bash
python3 validate_praetoria.py praetoria_content.json     # pre-flight (this slice): expect 0 errors
# or, after splitting into ./content/:
python3 validate_praetoria.py
# then the engine's own gate:
dotnet run --project src/Tools -- validate
dotnet test
dotnet run --project src/Tools -- play --seed 1 --turns 8
```

The Python validator mirrors the documented load-time checks (text pairing, token/role binding,
condition/effect vocabulary, choice-text coverage, holdings ids/resources/`requires`, scenario refs/bonds)
and adds a flag-chain audit (no event waits on a flag nothing sets) and the §17 dominance report.

> One gap worth confirming on the engine side: tokens (`{role.field}`) can appear inside `log` effect
> text, not just prose. The Python validator checks those. Make sure `dotnet -- validate` does too —
> a bad role token in a `log` would otherwise throw only at runtime.

---

## 3. How the chains work (emergent storylines)

Choices arm flags/counters; later events gate on them. No scripting — the Director recombines.

**Char flags** (per-character): `feud, humiliated, wounded, marked_for_death, reconciled, framed,
sabotaged, covered_for, owes_you, indebted, creditor, passed_over, mentor_proud`
**World flags:** `oath_sworn_academy, anomaly_hoarded, anomaly_studied`
**Counters:** `standing` (cohort/career reputation), `unrest` (also engine-driven on Credits<0),
`corruption` (graft accumulator; M5 crisis gates will read it), `goodwill` (the people's memory of mercy)

Example cross-system bleed: `the_lean_season/take_the_loan` arms `indebted`+`creditor` → `the_debt_called`
fires later; its ruthless branch converts the creditor into a `feud` enemy. An economic choice seeds a vendetta.

---

## 4. TWO REVIEW ITEMS (author needs your call)

1. **§8 controlled vocabularies aren't locked.** The scenarios use trait/aptitude/skill/ambition strings
   straight from the §8 strawman. The validator prints the exact list used (under "§8 VOCAB USED"). When
   you finalize §8, diff against that list and I'll find-replace any renames across all files.

2. **§8e rank ladders.** Career events use literal rank integers matching the strawman ladders
   (e.g. military 0–6). If the final ladder shifts, the `rank` thresholds in `career_progression` need a pass.

---

## 5. Scope guard (per §10 — not authored, no engine slot yet)

No fleets/ships/weapons, no megastructures, no marriage/succession, no structured Crisis/Bloody Events,
no galaxy map. The `corruption` counter and `anomaly_*` flags are seeded now so the M5 crisis/anomaly
systems have accumulators to read when they land — but nothing here depends on unbuilt mechanisms.

---

## 6. Suggested next work

- **Lock §8** (highest leverage — unblocks consistent authoring everywhere).
- **Deepen A toward 40–60** for a full un-repetitive opening act.
- **Priority C military-first arc** could extend (it's the track the Academy feeds).
- When M5 lands: the Bloody Event setpiece the brief keeps pointing at (gala/wedding turned to violence)
  becomes authorable, and `corruption`/`unrest`/`anomaly_*` already have data feeding it.
