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
3. **The three orders — done (2026-11-13).** See the section below.
3b. **The cursus honorum.** Offices gated by age and prior office (quaestor → aedile → praetor → consul in Roman terms;
   here Treasurer → Speaker/Admiral → Consul). Holding each feeds standing and the estates.
4. **Generals' sway.** A commander's *imperium*: fleets of the house with loyalty above a threshold to a commander who
   led them at the front follow *him* on succession or defection. The Senate may vote a **triumph** (legitimacy, the
   officers' regard, a party's gratitude) — or a **recall**. A recalled general with loyal fleets and a hostile Senate
   faces the Rubicon.
5. **The Caesar.** A consul or general with an Achilles name, fleets loyal to him, a party behind him and a Senate
   against him may **march on the capital**: a civil-war crisis with the Senate's parties choosing sides. Victory ends
   the republic (the Imperium era, already in the game) with him on the throne; defeat ends the house. Rivals can do
   this too — the player may be the Senate's sword against a rival Caesar.

## The three orders — built 2026-11-13

Owner's direction (2026-11-13): model the Republic more closely on the Roman one. There is the Senate, the military
and the commercial class. A family is generally good at one thing, but can be powerful across several. A Crassus
starts from nothing, gets rich, buys his way into the Senate and all but buys a command; that should be rare, and a
command should not simply be for sale, but a house that controls a resource or something strategic should be able to
exert that to gain more, especially with the right connection. A Caesar is born into a good family and distinguishes
himself by arms. Rags to riches and born-to-it should both be ways to play. The Imperium waits.

**Three orders** (`OrderSystem`): the **Senate**, **Arms** and **Commerce**. Every house has a standing of 0 to 100 in
each, reckoned every year from what it holds and has done. Nothing grants it.

| Order | What it is made of |
|---|---|
| The Senate | rank ×6, legitimacy ÷6, influence in the chamber, offices held, leading a party, a name at the bar, an old name, clients |
| Arms | hulls of its own ×4, rank in the Navy ×5, victories still talked about, victories abroad, a command of the Reach, veteran crews |
| Commerce | wealth tier ×5, ventures, companies directed, paper on the Exchange, houses in its debt, a cornered good, rank in the Administration, the Reach's purse |

Standing is **notable** at 20, **great** at 44, **dominant** at 64. A house's **calling** is its strongest order and is
slow to change (another must lead by 8). A house is an **old name** if it held the rank of Prefect or better when the
world began, otherwise a **new name**. A battle won is a point of **glory**, a triumph four; one fades every eight
years. A house that brings up a quarter of the Reach's production of a strategic good has **cornered** it.

**What standing does**

- **Weight in the chamber.** One seat for each step in Arms and in Commerce, and one for every two clients. A great
  general or banker is heard whatever their rank.
- **Four doors to a seat.** A house may move motions and stand for office by **land** (Prefect or better), **the
  bench** (a head who is a Magistrate), **the sword** (great in Arms) or **the purse**: a house notable in Commerce
  pays the census 60 credits and is enrolled. Every house still votes.
- **Sway.** A house leans toward whoever it depends on: +3 its patron, +2 its client, +2 its creditor, +1 a fellow
  client, +1 the house whose helium-3 its fleets burn, +1 the director of a company it holds a tenth of; four at
  most. Sway counts for a commission, a recognition or a peace for that house, for anything it moves (two at most),
  and against a censure, embargo or break-up aimed at it. This is how a command is "bought": not with a purchase, with
  the votes of everyone who owes you.
- **The obvious man.** For a commission the chamber leans +1 toward a house great in the post's order and −1 against a
  house of no account in it. A banker with no ships needs a lot of sway to be voted a fleet.
- **Patrons and clients.** A house at least twice as rich may take a needy house as a client by paying its debts and a
  gift. The client costs a credit a year, votes for the patron at an election whatever its party, and leans the
  patron's way. A house may also seek a patron who thinks well of it. A client that outgrows its patron leaves. A
  house keeps one client plus its steps in Commerce and in the Senate.
- **Canvassing.** Ten credits spent on the voters in a nominee's name is one more vote's weight at the next election,
  five times an election. It may be spent for another house: that is how a Crassus makes a Caesar consul.
- **The first of the name.** A new name in a consul's chair gains five legitimacy, once.

**Three ways up**, each a start in the game:

| Start | Order | The road |
|---|---|---|
| *The New Man* (new) | Commerce | no land, no seat, one trading company and a patron; get rich, enrol by the census, take clients, turn debtors into votes |
| *The Risen Officer* | Arms | no name, a commission; win, be heard by the sword, be voted a command |
| *The Circling Heir*, *The Old Lion*, *The Fallen House* | The Senate | born to a name; rank, office, the consulship |

Stewards do the same: a house of commerce with no seat buys one, a rich house takes a needy house of its own party as
a client about one year in six, and a rich house of the Factors canvasses for its party's nominee.

**Measured** (eight scenarios, sixty years, no player, about 33 houses): 3 to 9 houses great in the Senate and 1 to 3
dominant; 4 to 6 great in Arms and 0 to 2 dominant; 5 to 10 great in Commerce and 0 to 1 dominant; 4 to 6 houses great
in two orders; 0 to 3 in all three. Callings split roughly 10 senatorial, 8 of arms, 15 of commerce. Up to 17 client
ties and up to 12 seats held by the purse in a run. (Measured with great at 40; it is now 44, which trims each count.)

**Not built yet**

- The cursus honorum (stage 3 of the older list below): offices gated by age and prior office.
- Triumphs and recalls voted by the Senate, the general's own army, and the march on the capital (stages 4 and 5).
- Tax-farming and state contracts: the Senate handing commerce its profits.
- An equestrian career of its own. Commerce is measured from ventures and wealth, not from a service ladder.
- Clients as a named body of people. A client is a house.
- The Imperium's version of all this.

## The wars abroad
Stage 3 of the Republic in practice: the Senate makes war on a nation beyond the Reach by motion (`foreign_war`), the
Sword for it and the Factors against; houses send fleets; the house whose fleets win most takes the triumph. See
docs/FOREIGN_NATIONS.md. The general's sway from those triumphs is the Caesar's road (stage 5).

## Not yet
Elections' content (hustings events), the parties' names per culture, and the map presence of foreign wars that make
consular commands matter (docs/FOREIGN_NATIONS.md).
