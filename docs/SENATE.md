# The Senate as a place of action

GDD §6.1 names the Senate as the political pillar's chamber; until this pass it showed standings and offered nothing to
do (GAP §10 item 2). Now houses move **motions**, every house **votes by weight**, and the player **whips**.

## Motions

`content/senate/motions.json` holds the kinds. A house of standing — a baron or better, or a head who is a Magistrate or
better on the law track — moves one for its influence cost (`ProposeMotionCommand`). One open motion a house, two on the
docket. A motion moved in turn T is voted at the end of turn T+1, so there is always one session to whip.

| motion | target | carries |
|---|---|---|
| Censure | a house | target legitimacy −6 |
| Recognition | a house (yourself allowed) | target legitimacy +6 |
| The Levy | — | every landed house pays 3 credits to the chair; the chair gains 2 legitimacy |
| Relief | a region | lawlessness −15, allegiance +10, prosperity +5 on its worlds; the proposer pays 8 credits (half effect if it cannot) |
| The Peace of the Reach | a house at war | white peace in all its wars |
| Commission | a house and an office | the house's best-qualified member takes the office; the previous holder is relieved; target legitimacy +2 |
| Embargo | a house | the exchanges close to it for eight turns: no open market, venture credits halved |
| Act of the Senate | a law of the Senate and an option | the law is so set for the whole Reach (see *Acts* below) |

## Acts: the law of the Reach by motion

Three laws in `content/politics/laws.json` carry scope `senate` and `reach: true` — the Common Law (local courts, the
custom, the Reach's assizes), the Trade Charter (protected markets, the custom, open lanes) and the Levy of the Reach (no
common levy, the custom, the Reach's levy). No house holds that scope; the only way to set one is an **Act** — motion kind
`act` (`law: true`), three influence, naming the law and the option. When it carries, `PolicySystem.Enact` writes it under
the pseudo-house `senate`, every house's `ReachModifier` and estates read it, and the Senate's cooldown (ten turns) keeps
the chamber from hearing that law again at once. Refusals: not a Senate law, the option already in force, the Senate
settled it lately, the law already on the docket.

Rivals lean on an Act by their **estates** (`SenateSystem.ActLean`): the option's opinions against the option in force,
doubled where it pleases an estate hostile to the house or galls one loyal to it, a tenth of the sum clamped to ±3; then
by situation — a house at war wants the common levy, a house with lawless worlds wants the assizes, a house with a navy
wants cheaper fuel, a house of three worlds likes a market it can tax, a house with a restless world fears anything that
adds unrest. Rival proposers move the Act that most pleases an estate hostile to them (`WantedAct`), after relief and
before censure. Flags: `act_open`, `act_passed`, `act_passed:<law>`, `act_failed`; counter `acts_passed`. The Chancery's
"law of the Reach" drawer shows every reach-wide law, who set it and how long before the chamber will hear it again. In
sixty-turn simulations two or three Acts carry a run.

## Weight and the benches

A house's vote weighs its Senate-sphere influence (the chair's two are in it), plus its title rank, plus one if its head
is a Praetor or better. Ties fail. Rival houses lean by interest (`SenateSystem.Lean`): a target votes against its own
censure and its friends with it, a house at war with the target votes for; the poor vote down a levy and the chair
votes for it; a region's lords vote for its relief, and the rim is sympathetic; a losing belligerent begs for peace and
the bystanders like it; disposition toward the proposer tilts everything a point; a deterministic jitter breaks the
ties. The Senate screen shows every bench's lean before the vote.

**Whipping** (`LobbyCommand`): one influence leans a house that does not dislike you by two toward your stance; five
credits bribe any house by three. Once per house per motion. **Voting** (`VoteCommand`) is free.

## Rivals bring their own business

About one turn in three the most influential house with a reason moves: a losing war (peace), an office it has the
rank for that is vacant or held by a house it hates or fights (commission), a title above its legitimacy (recognition), an empty chair's coffers (the levy), a lawless home region (relief), a hated rival (censure, or
embargo if at war with it). The chamber tires of repetition: the same house may not move the same motion against the same
target for ten turns.

## Standing

A carried motion lifts the proposer's standing in the Senate sphere by one (counter `senate_standing:<house>`, read by
`SphereSystem`); a failed one costs one. Standing feeds threat and coalition pressure like any sphere influence.

## For content

Flags: `motion_open`, `motion_against_me` (a censure or embargo of yours is on the docket), `censured`, `recognised`,
`levied`, `embargoed`, `motion_passed_mine`, `motion_failed_mine` (the last five for one turn). Counters:
`motions_proposed`, `motions_passed`, `motions_passed:<house>`, `embargo:<house>` (the turn it lifts), `lobbied`. Six
events ship in `content/events/senate.json`: *The Whip*, *A Motion Against the House*, *The Censure Carries*,
*Recognised*, *The Levy Falls Due*, *The Motion Carried*.

## The offices of the Reach

GDD §8 puts the drama in the gap between office-power and house-power. `content/senate/offices.json` names three offices,
each granted by a **Commission** motion to a house whose member holds the rank on the office's career track:

| office | track · rank | confers |
|---|---|---|
| Admiral of the Reach | military · Lieutenant | command of the **office fleets** (`Fleet.Office`): they sail under the holder's colours and fight its wars, the chair pays their upkeep and the state's bunkers fuel them; +3 Navy influence |
| Treasurer of the Reach | stewardship · Governor | three credits a turn from the chair's coffers; +3 Treasury influence |
| Speaker of the Senate | law · Magistrate | three more votes of weight, motions cost one influence less; +2 Senate influence |

An office is the state's, not the house's. It is lost when the Senate commissions another (the holder's friends vote
against, a house with its own candidate is jealous, the chair likes a safe pair of hands), when the holder dies without
a kinsman of the rank, when the house falls — and the Admiralty is lost for **treason** the turn the holder's house is at
war with the chair (legitimacy −6, the fleets return to the chair). The Emperor holds every office by the throne. Rivals
move commissions for themselves when an office is vacant or held by a house they hate or fight. The Reach Squadron (three
frigates and a cruiser at the capital) is the office fleet of the fractured stars; the Imperial Home Fleet of the Imperium.

Flags: `holds_office:<id>`, `office_granted`, `office_lost`, `treason`, `office_vacant:<id>`; counter `offices_granted`.
Five events in `content/events/offices.json`, among them *The Admiral's Temptation* — the design's classic arc: the fleet
is the state's, the guns are in your hand.

## The benches: persistent factions

Every decided motion leaves an **affinity** between each pair of houses that declared: +1 for voting together, −1 for
voting apart (counter `affinity:<a>|<b>`, clamped −6..12). When a house outside any bench has affinity three or more with
two others also outside one, a **faction** forms around the weightiest of them; a house whose affinity with a leader
reaches three joins (one a bench a turn); a member whose affinity with its leader falls under one breaks with it; a bench
of one dissolves; a dead or fallen leader is replaced by the weightiest member. The chamber names a bench by what it is:
*The Rim Benches*, *The Old Blood*, *The Court Party*, *The Purse-Strings*, *The Peace Benches*, or *The <Leader> Bloc*.

**Discipline.** A member leans two toward its leader's stance on every motion that does not target it or come from it, and
a whip on the leader is half a whip on the bench — so whipping the right house moves five votes. A leader's carried
motions build the bench's **record**; its platform remembers the kinds it stood for and against.

**The player** takes a seat (two influence; the leader's head must not dislike them), leaves (the leader takes it ill:
affinity −2, disposition −5), or founds a bench of their own (two influence, two houses with the affinity). Voting against
your own bench breaks the whip — the same cost — and the bench may drift you out. Flags: `in_faction`, `faction_leader`,
`faction_formed`, `faction_joined`, `faction_dissolved`, `broke_the_whip`, `faction_against_me` (a bench's leader has a
motion against you on the docket: the whole bench will follow); counter `factions`. Five events in
`content/events/factions.json`, among them *Across the Aisle*.
