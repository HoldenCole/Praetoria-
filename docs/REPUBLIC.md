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
1. **Parties — done (2026-11-09).** Four parties ship in `content/senate/parties.json` — the Optimates (blue; the
   old blood and the magistrates; the assizes, protected markets, iron law, closed court), the Populares (red; the
   commons and the frontier; no common levy, lenient tithe and justice, the open ballot, homesteads), the Sword
   (yellow; the officers; the common levy, conscription, the aggressive doctrine, martial law), the Factors (green; the
   merchants; open lanes, free trade, guild rights, no tolls). At the world's creation every house is seated with the
   party its laws fit best (`FactionSystem.Seed`; two points per matching law position, one per courted estate it
   runs warm with), each party with at least two members, the weightiest member leading. A house with no party takes
   the best-fitting seat; every tenth turn one rival whose laws have drifted crosses the floor. Benches no longer form
   of themselves: the player founds the fifth (silver), whose platform is the founder's own law. Members lean with the
   party's stance on each kind of motion and ±2 on an Act that matches or undoes the party line; discipline toward the
   leader stands as before. The Senate screen groups the hemicycle by party and shows each party's colour, courted
   estates and law platform.
2. **The consulship — done (2026-11-09).** Two elected offices in `content/senate/offices.json` (`elected: true`):
   the Senior Consul (weight +4, two credits a turn, a motion one influence cheaper, the veto) and the Junior Consul
   (+3, one credit, the discount, the veto). Every tenth turn (`ElectionSystem.Hold`, not under the Imperium) each party
   nominates its weightiest member with a qualified candidate (rank 2 on any track) who was not elected last time;
   the player may **stand** (two influence) and runs as their party's nominee, or alone. Every house's weight goes to
   its party's nominee; a house with no party votes for the nominee its head likes best. The two highest tallies take
   the chairs; the old consuls' term ends. A consul may **veto** one open motion a term before the vote (never their
   own); rival consuls veto a censure or embargo aimed at their house or their party's leader, and an Act that undoes
   their party's line. The Senate screen carries the consulship panel (the chairs, the nominees as they stand with
   their tallies, the Stand button) and veto buttons on the docket for a consul; four events (*The Hustings*, *The
   Consulship*, *The Veto Falls*, *The Term Ends*). The chair's laws stay with the capital's holder for now; the
   consuls' command of the state fleet waits for foreign wars (docs/FOREIGN_NATIONS.md).
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
