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
| Embargo | a house | the exchanges close to it for eight turns: no open market, venture credits halved |

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

About one turn in three the most influential house with a reason moves: a losing war (peace), a title above its
legitimacy (recognition), an empty chair's coffers (the levy), a lawless home region (relief), a hated rival (censure, or
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
