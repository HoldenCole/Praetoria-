# Blood between houses

Built 2026-11-17 on the owner's word ("blood feud yes"; docs/GAPS.md R4, and part of F3). A killing binds the dead
person's house to answer it. Before this a death had no cause and no killer on record, enmity was between persons and
died with them, and the house that was struck did nothing.

## What a debt is

One death, one debt: the dead person's house is **owed blood** by the house that did it, and one person is **held to
blame** (whoever did it or gave the order; when they die, the head of their house). Two houses that have each killed
one of the other's hold two debts, one each way. The debt is the house's, so it outlives everyone who was there.

**What starts one.**
- **A knife.** A hired death is laid at the right door one time in three. A house that already has blood or a war with
  the killer's, or whose head already thinks very ill of the killer, needs no proof.
- **A death in battle**, but only of the head of a house or the head's near blood (parent, child, brother or sister).
  Any other commander who goes down with the line is war, and forgotten at the peace.
- **A killing by hand** in an event: the cadet killed at the Institute, the wedding feast, the purge after a
  proclamation. An event's `kill` now names whose hand it was (`"by"`).

**What it does while it stands.**
- The dead person's house thinks much worse of the one to blame, and worse of the head of their house.
- Each house votes against what the other moves in the Senate (three points of lean).
- Nobody of one house may court anybody of the other.
- The house that is owed loses nothing for two years. After that an unanswered death costs it a point of standing a
  year (four at most), and the dead person's near kin think worse of the head each year.
- After eight years it is let lie, whether anyone says so or not.

## How it ends

| How | What happens | What it costs |
|---|---|---|
| **Blood, the right blood** | The one held to blame dies by the wronged house's doing | Nothing more. The debt is paid |
| **Blood, the wrong blood** | Anyone else of the owing house dies by it | The debt is paid, and the other house now holds one in its turn |
| **A duel** | The wronged house's head, or a champion of the house, calls the one to blame out. Odds come from what each knows of fighting, their service, health and years. The loser dies four times in ten and is otherwise wounded; the winner is sometimes wounded | It ends there whoever falls, and a death in it is nobody's to avenge |
| **The field refused** | The one called out does not come | Their house loses three standing, it is written against them, and what the wronged house does next is on them. The debt stands |
| **The price** | A sum fixed by who the dead person was (2,000, a thousand more for each rank, 3,000 more for the head of a house), asked by the house that is owed or offered once by the house that owes | The owing house pays and loses a point of standing: to pay is to admit it. The dead person's near kin think less of a head who took money |
| **Let lie** | The head of the wronged house says so, or eight years pass | Three standing if said (less what the years have already taken), the near kin take it hard, and the other house had not expected it |

A knife is the ordinary assassination; the feud is settled by who dies.

## Other houses

They carry and end theirs by the same rules, each head by their nature, acting about one year in three:

- **The honourable and the proud** send someone to the field, if the house has someone to send.
- **The cruel, the ruthless and the vengeful**, and anyone who has been refused, send a knife.
- **The rest** ask the price. A house pays if it has the money half as much again, or the house asking is no weaker,
  or its own head is a just or honourable one. An arrogant head never pays.
- Nobody is called out twice. After the field is refused, what comes is a knife.

When the house they hold to blame is the player's:
- A challenge or a demand for the price is **asked** of the player, in a report and on the panel, and waits two years
  for an answer before it is taken as a no.
- A knife is **never for the player's own person**. It goes to one of the player's adult kin, which is the wrong
  blood, and so becomes the player's to answer.
- A house that owes the player may offer the price once, if its head is a just or honourable one or the player's house
  is the stronger.

**Where the world's feuds come from.** A hard-natured head (cruel, ruthless or vengeful) who cannot abide the head of
another house, or whose house already has blood with it, now and then sends a knife at that house's ablest adult:
never its head, never its last adult, never the player's house, and no more than once in twenty years. A war alone is
not cause. Without this the unattended world had no feuds at all: in ten games the only four were the player's own.

## On the screen

The You screen has a panel, **Blood between houses**, when there is any. For each death owed to you: who, how long
ago, who is held to blame, what it has cost so far, and buttons to call them out (with your odds), send a kinsman
who would go, ask the price, send a knife, or let it lie. For each death held against you: what they have asked, with
the answers, or a button to offer the price. Below that, debts already ended and the feuds of other houses.

Seven events (`content/events/feud.json`): a debt begun, on either side; the price asked; the price offered; called
out; the dead unanswered; and after the field.

## Measured

Sixty unattended games of sixty years (the probe's player gives no orders, so its own debts are always let lie):

| | |
|---|---|
| Debts begun | 143, in 58 of the 60 games |
| How they began | a knife 64, a death in battle 41, a killing by hand 38 |
| Ended on the field | 37 (22 of the duels ended in a death) |
| Ended in blood | 21 (once the wrong blood) |
| Bought off | 14 |
| Let lie | 56, of which 30 were the passive player's own |
| Still standing at the end | 9 |
| The field refused | 15 times |
| Knives sent between other houses | 145 |

The Republic stood in all 60. The full account is docs/GAP_ANALYSIS.md, section 71.

## Not yet

- A knife that is not traced leaves nothing behind: no suspicion that grows, nothing that can come out later (R2).
- A marriage cannot end a feud, and nobody mediates one: not the Senate, not a court, not a third house.
- Nobody takes a side. A feud is between two houses and their friends are not asked to choose.
- A duel exists only inside a feud. There is no duel over an insult, and none at the Institute (the rest of F3).
- Other houses' knives go at houses, not at the player's, except in a feud or at war.
- A relative of the player who is called out goes or does not go by their own nature. The player is not asked, and
  cannot forbid it or go in their place.

## Files

`src/Core/Systems/FeudSystem.cs`, `src/Core/State/BloodFeud.cs`, `src/Core/Commands/FeudCommands.cs`; hooks in
`AssassinateCommand`, `FleetSystem.AdmiralOutcomes`, `KillEffect`, `SenateSystem.RawLean`, `CourtshipSystem.Bar`,
`NpcAi.Dynasty`; `src/Web/Components/FeudPanel.razor`; `tests/FeudTests.cs`; `tools/browser/feud.js`.
