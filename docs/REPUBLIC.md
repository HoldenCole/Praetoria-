# The Republic: parties, consuls, generals, and the Caesar

Owner's direction (2026-11-09): the pre-Imperium Reach is a **parliamentary republic with multiple parties**, in the Roman
mould — parties contending in the Senate, **two consuls** at the head of the state, **generals with sway** of their own,
and a ladder of **offices** — so that a **Caesar figure** can arise: a victorious commander whose fleets are loyal to him
rather than the state, who can be voted a triumph or march on the capital.

## What exists today that this builds on
- **Benches** (docs/SENATE.md): persistent voting blocs with platforms, leaders and discipline. They become **parties**:
  named, with a platform of laws (bench_platform already exists), a whip, and candidates for office.
- **Offices of the Reach** (Admiral, Treasurer, Speaker) granted by Commission motions. They become the *cursus honorum*
  below the consulship.
- **Acts and petitions**: the Senate already legislates for the whole Reach.
- **Military depth** (docs/MILITARY.md): command styles, valour, the Achilles figure, fleet **loyalty**, campaigns. A
  general's *sway* is his valour, his fleets' loyalty to him, and the officers' regard — the raw material of a Caesar.

## Design, staged
1. **Parties.** Benches gain a party name and colour, a platform of two or three law positions and one motion preference,
   and *seats* (weight). Houses drift between parties by affinity (already tracked) and by law opinions. The player can
   found, lead or leave a party; the Senate screen shows the parties as blocs on the benches.
2. **The consulship.** Every ten turns the Senate elects **two consuls** from houses of standing (title rank ≥ 2 or a
   head who is Praetor / Commander or better), each party moving its candidate; the two highest weights take the chairs.
   A consul holds the *chair's* laws jointly (replacing the capital-holder as chair of the Reach's laws), commands the
   state fleet for foreign wars, and may veto one motion a session. Consuls cannot stand again for ten turns.
3. **The cursus honorum.** Offices gated by age and prior office (quaestor → aedile → praetor → consul in Roman terms;
   here Treasurer → Speaker/Admiral → Consul). Holding each feeds standing and the estates.
4. **Generals' sway.** A commander's *imperium*: fleets of the house with loyalty above a threshold to a commander who
   led them at the front follow *him* on succession or defection. The Senate may vote a **triumph** (legitimacy, the
   officers' regard, a party's gratitude) — or a **recall**. A recalled general with loyal fleets and a hostile Senate
   faces the Rubicon.
5. **The Caesar.** A consul or general with an Achilles name, fleets loyal to him, a party behind him and a Senate
   against him may **march on the capital**: a civil-war crisis with the Senate's parties choosing sides. Victory ends
   the republic (the Imperium era, already in the game) with him on the throne; defeat ends the house. Rivals can do
   this too — the player may be the Senate's sword against a rival Caesar.

## Not yet
Elections' content (hustings events), the parties' names per culture, and the map presence of foreign wars that make
consular commands matter (docs/FOREIGN_NATIONS.md).
