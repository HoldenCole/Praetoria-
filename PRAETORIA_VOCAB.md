# Praetoria — §8 Controlled Vocabularies (LOCKED v1.0)

The canonical bible for trait/skill/ambition/track/rank strings. The engine references these by string;
anything outside these lists throws at load. `validate_praetoria.py` enforces them as a hard gate.
Edit here first, then re-run the validator and apply renames across content.

---

## 8a. Nature traits (8) — constraining, stress-bearing

Identity traits that *are* the character. Acting against them should cost stress (events pair the
off-trait choice with `adjustStress`). `kind: nature`.

| Trait | Meaning (one line) |
|---|---|
| **Ambitious** | Reaches always for the next rung; unlocks bold, self-advancing options. |
| **Cruel** | Inflicts more than the situation requires; comfortable with others' pain as a tool. |
| **Honorable** | Keeps oaths and the rules of the game; breaking one spikes stress. |
| **Just** | Weighs fairness over advantage; pays stress to do right when wrong would be easier. |
| **Loyal** | Holds to people and bonds past self-interest; betrayal is near-unthinkable. |
| **Proud** | Will not be slighted; defends standing even at cost; struggles to yield or apologize. |
| **Ruthless** | Removes obstacles by any means; ends justify means without hesitation. |
| **Vengeful** | Remembers every wrong; carries grudges into debts that must be repaid. |

## 8b. Aptitude traits (11) — gating/flavoring, free (no stress penalty)

Skill/disposition traits that gate or flavor options. `kind: aptitude`.

| Trait | Gates / flavors |
|---|---|
| **Administrator** | Delegation and institutional options; running things efficiently. |
| **Brilliant** | Insight options — study, invent, see what others miss (e.g. studying an exotic find). |
| **Diplomat** | Negotiation, alliance, de-escalation paths. |
| **Duelist** | Personal-combat options; the blade as answer. |
| **Greedy** | Tempting corrupt/profit options others don't see (often paired with a corruption cost). |
| **Orator** | Persuasion of crowds and chambers; winning on the popular, not the sound, argument. |
| **Paranoid** | Surveillance, distrust, watch-your-back options. |
| **Quartermaster** | Supply, logistics, keeping a force fed and equipped. |
| **Schemer** | Indirect/manipulative options; winning by arranging rather than confronting. |
| **Strategist** | Large-scale planning; doing more with less (force economy). |
| **Tactician** | Battlefield/immediate-engagement cleverness. |

## 8c. Skills (13) — numeric, grow over a career

| Skill | Track |
|---|---|
| tactics | Military |
| discipline | Military |
| leadership | Military |
| gunnery | Military |
| administration | Stewardship |
| economics | Stewardship |
| logistics | Stewardship |
| engineering | Stewardship |
| oratory | Law/Intrigue |
| intrigue | Law/Intrigue |
| law | Law/Intrigue |
| diplomacy | Law/Intrigue |
| charisma | Cross-cutting |

## 8d. Ambitions (7) — drive NPC AI + personal-story events

| Ambition | The character pursues… |
|---|---|
| **amass_a_fortune** | Wealth above all — credits, holdings, leverage. |
| **earn_a_command** | A real command of their own, won by merit. |
| **master_the_senate** | Political dominance in the chambers of power. |
| **outshine_all_rivals** | Being recognized as the best, relative to peers. |
| **restore_house_fortunes** | Returning a fallen or diminished house to greatness. |
| **seize_the_throne** | Ultimate power — rule itself. |
| **win_a_great_love** | A personal, romantic, or familial bond above ambition. |

## 8e. Career tracks (3) + rank ladders — LOCKED

`careerTrack` ∈ `military, stewardship, law`. `careerRank` is the integer the `advanceCareer` effect
increments. **These ladders are now locked; the career events' `rank` thresholds are written against them.**

**Military:** 0 Cadet → 1 Ensign → 2 Lieutenant → 3 Commander → 4 Captain → 5 Admiral → 6 Grand Admiral
**Stewardship:** 0 Clerk → 1 Administrator → 2 Governor → 3 Minister → 4 Imperial Treasurer
**Law/Intrigue:** 0 Aspirant → 1 Advocate → 2 Magistrate → 3 Praetor → 4 High Justice

> **Rank-threshold note for engineers:** career events gate on literal integers (e.g. `the_admirals_shadow`
> binds an `admiral` role at `rank gte 5`; `first_command` needs `self rank gte 1`). These match the ladders
> above. If any ladder is renumbered, grep `career_progression` for `"type": "rank"` and re-check thresholds.
> A track-aware check isn't enforced by the validator (the engine treats rank as a bare int across tracks),
> so the stewardship/law caps (4) vs military (6) are a design constraint to honor manually.

---

## Change log
- **v1.0** — Locked. Added `Honorable`, `Ruthless` (nature) and `engineering` (skill) to the
  scenario-extracted list, since the events depend on them. All 47 events + 3 scenarios validate against
  this vocabulary with 0 errors.
