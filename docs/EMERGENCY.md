# Emergency powers

Built 2026-11-17 on the owner's word ("yes emergency powers"; docs/GAPS.md R8). When the Reach is in danger the
Senate may vote one of its two consuls powers above the other for a term. Before this the Republic could be ended only
by a march or by a gate of standing; nothing was voted, and nothing was lent that might not be given back.

## When

The Reach is **in danger** while any of these holds: the great war is being fought; a civil war; a state has broken
away and is not yet recognised; or the Reach is at war beyond its borders. The first two are a danger to the Reach
itself and count for three; the others for one.

## The vote

Any house may move it (3 influence), for a **sitting consul** of any house, its own included. A house leans by:

- the danger: three for a war for the Reach itself, one for a lesser one, less two, because no Senate likes to do it;
- what its head thinks of the consul's head, and whether they sit on the same bench;
- **fear**: a consul whose house has a commander the crews love, or two hulls in five of all the Reach's, is refused
  by houses that would otherwise agree;
- memory: a house that kept the powers once is four points harder to give them to, and each time the same holder
  asks for more time costs two.

It cannot be moved again for twelve years after the last time the powers ended.

## What the holder may do

For three years:

- **The other consul has no veto**, and the holder may strike one motion every year (a consul otherwise has one veto
  a term).
- **The Senate leans their way**: two points of lean, from every other house, toward anything the holder's house moves
  (the whip count says "{house} holds emergency powers"). It is a lean, not a vote: a motion most houses are hard against
  still fails, and the reports say "leans toward", not "will pass" (docs/events/votes.md, "Promises").
- **A levy without a vote**, once a year while the danger lasts: every other house pays up to three hundred credits to the
  holder's house. Each head thinks the worse of the holder, and the Reach a little less of its magistrates.
- **No election is held**, of the consuls or of the magistrates below them (`ElectionSystem.Due`, `LadderSystem.Due`):
  the magistrates sit on, and one that falls due is held at the first session after the powers are laid down. The ladder panel
  says so while they run.

## How it ends

They are **due** when the three years are up or the danger is past, whichever comes first.

| | What happens |
|---|---|
| **Laid down** | The house gains five standing (seven if it is done before they are due), the Reach trusts its magistrates more, every head thinks better of the holder, and it goes in the record of their life. Another house's consul always does this, at once |
| **More time** | In the last year, or once they are due, the holder's house may ask the Senate for three years more, if the danger is still there. Refused, they are due |
| **Kept** | Only the player can. Each year past leave costs five points of the Reach's trust, two of the house's standing and five of every head's regard. After three such years the Senate takes them |
| **Stripped** | By the Senate, after three years kept or on refusing them for life: eight standing lost, ten of every head's regard, and the house is remembered as one that kept them |
| **Lapsed** | The holder dies or loses the chair |

## The powers for life

A holder who has been given more time once, or whose powers are due, may put it to the Senate that they be held
**for life** (4 influence). Only the player may: no other house ends the Republic (docs/PROCESS.md, the second rule).

The Senate starts five points against. Regard and bench count. Fear counts the other way round this time: a holder
with two hulls in five of the Reach's is not easily refused. A Reach still in danger, or one that has stopped
believing in its magistrates, gives way sooner. Every year the powers were kept without leave is held against it.

**Carried, the Republic ends by its own vote.** The holder's house takes the throne as if proclaimed, without the gate
of standing a proclamation asks. **Refused, the Senate ends the powers in the same breath**, and the house is stripped.

## Other houses

When the Reach is in danger a house of the Reach now and then moves the powers for one of its consuls (one year in
four in a war for the Reach itself, one in twelve otherwise): the consul's own house, or a house of standing whose
head thinks well of theirs. Never for the player, and never by the player's house: that is the player's to ask for.
Their consuls levy every other year, and lay the powers down the year they are due.

## On the screen

The consulship panel on the Senate screen says when the Reach is in danger and from what, with a button to ask for the
powers if you are consul. While someone holds them it shows who, for what, until when, and whether they are due or
kept; and if it is you, buttons to lay them down, levy, ask for more time, and ask for them for life, each with what
it costs or why it is refused.

Eight events (`content/events/emergency.json`): the powers voted to another, and to you; the term up; a year past
leave; a levy without a vote; the powers laid down by another, and by you; and stripped.

## Measured

Sixty unattended games of sixty years:

| | |
|---|---|
| Years the Reach was in danger | 39 in a hundred |
| Moved | 116 times |
| Voted | 53, in 40 of the 60 games |
| Refused | 58 |
| Years held | 168 |
| Levies without a vote | 43 |
| Laid down | 37; the rest lapsed with the holder or were still running |
| Kept past leave, or stripped | none: no house but the player's keeps them |
| The Republic ended by its own vote | none: it is the player's to ask |

The full account is docs/GAP_ANALYSIS.md, section 72.

## Not yet

- The Senate cannot revoke the powers early, and nobody moves against a holder who is abusing them.
- The holder does not take command of the Reach's fleets: the office fleets stay with their offices.
- No house manufactures the danger. The player can stir a breakaway, which counts; nothing else can be arranged (R2).
- A consul of another house never keeps them, by the second rule. If the owner wants a Caesar by this road among the
  other houses, it is the same question as Q1.
- Nothing is done with the powers beyond the veto, the levy and the Senate's deference: no proscription (C4), no
  appointments, no laws by decree.

## Files

`src/Core/Systems/EmergencySystem.cs`, `src/Core/State/EmergencyPowers.cs`, `src/Core/Commands/EmergencyCommands.cs`;
hooks in `ElectionSystem` (the election owed, the veto), `SenateSystem` (the two motions, the holder's lean, the
mover), `ProposeMotionCommand`; `content/senate/motions.json` (`emergency`, `for_life`);
`src/Web/Components/SenateScreen.razor`; `tests/EmergencyTests.cs`; `tools/browser/emergency.js`.
