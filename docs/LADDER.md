# The ladder of offices

Built 2026-11-14. Before it the Republic had two consuls and nothing beneath them: any house of standing could stand
for consul, and a political career had one step. Now there are three elected offices below the consulship, each held
by a person and not by a house, and nobody is consul who has not climbed.

## The rungs

`content/senate/ladder.json`. An election is held every fifth year, and every seat falls vacant at it.

| Rung | Seats | From the age of | Must have been | Pays (a year, to the holder's own purse) | Teaches | Standing |
|---|---|---|---|---|---|---|
| Quaestor | 4 | 25 | nothing, but a place in a service | 1 | economics | 3 |
| Aedile | 2 | 30 | quaestor | 1 | administration | 5 |
| Praetor | 2 | 34 | quaestor | 2 | law | 8 |
| Consul | 2 | 40 | praetor | (the consul offices, docs/REPUBLIC.md) | | |

- **Aedile is optional**, as it was. It is the rung that gives games.
- Nobody holds the same rung twice, and nobody who has gone higher stands for a lower one.
- A seat lapses if its holder dies or grows frail. It stays empty until the next election.
- **What a person has held is theirs for life** (`Character.Cursus`) and is written into their record
  (docs/LIFE.md): "Elected quaestor."
- There is no ladder under a throne. Offices there are the throne's to give (see the roadmap, politics under a throne).

## The election

Every fifth year, before the consular election in a year that has both.

- **Who stands.** From each other house, its best qualified person, about two houses in three at each rung. A person
  stands for the highest rung open to them. Someone who stood higher and was not chosen may be put up for a lower rung
  the same year if their house has nobody else for it. From the player's house, only those whose names are down.
- **The count.** Each house gives its whole weight in the Senate to one candidate: its own, else its patron's, else
  one of its party, else whoever's head its own head likes best. To that each candidate adds their own merit: oratory
  and charisma twice over, career rank twice over, and a sixth of their renown. Canvassing by the house's order
  (docs/REPUBLIC.md) and the head's backing count too.
- The highest rung is filled first, so nobody is given two.
- The chronicle prints the result with each winner's count.

## What the head may do, and what is not the head's

The rule is docs/PROCESS.md: you play one person.

- **Stand yourself** for a rung you qualify for: 2 influence. Withdraw freely.
- **Call in what is owed** for yourself, or **speak for** one of the house who is standing: 2 influence for 6 votes,
  twice at most. A relative you speak for thinks better of you.
- **A relative decides for themselves whether to stand.** One who wants to master the Senate, to outshine every
  rival or to take the throne puts their own name down two years before the count, for the highest rung open to them.
  The head cannot enter them and cannot take their name down.
- **Ask them to stand down**, once a term. The loyal withdraw and think less of you. The wilful leave their name where
  it is and think less of you too.
- **Give games** as aedile, once a term, for 12 credits of your own: the house gains 2 legitimacy and it is written
  into your life. An aedile of the house with the money gives theirs without being asked.

With the family's independence set to deferential (docs/CHARACTER.md), relatives do not stand by themselves.

## What holding office does

- **Pay**, to the holder's own purse (docs/WEALTH.md), for the player's house.
- **Learning by doing** (docs/EDUCATION.md): a sitting magistrate practises the skill of the office.
- **Standing in the Senate.** A house's senatorial order (docs/REPUBLIC.md) counts "magistracies its people have
  held": the highest rung any living member has reached, and one more for each other member who has climbed, to 14.
- **The consulship.** `content/senate/offices.json` gives both consul offices `minAge: 40` and `requires: praetor`.
  A house with nobody of that age and record cannot stand.
- **The ambition to master the Senate** (docs/AMBITIONS.md) counts rungs climbed.

## The work of office

The owner's rule (docs/PROCESS.md, "every position has work"): an office is not a title. Every year each sitting
magistrate has business, and what they do with it has consequences.

| Office | The year's business | The choices |
|---|---|---|
| **Quaestor** | The accounts of the Reach. One quaestor a year is given the most lawless region, if the capital's purse can bear it. | **Close them as they stand** (trust +1, the house thought a little better of). **Find money for the lawless region** (its lawlessness −2, 4 credits from the capital's purse, its houses think +6 of yours). **Favour your own house** (+8 credits to the house, trust −3, and one time in four it comes out). |
| **Aedile** | The works and the lanes, paid from the aedile's own purse. | **Repair the lanes** of the region where the house's worlds are (6 credits, lawlessness −2). **Grain for the squares** (6 credits, legitimacy +2). **Do nothing** (trust −1). |
| **Praetor** | A case between two houses at odds, neither the praetor's own. The law is with one of them. | **Rule on the law** (the loser pays 5 credits, the winner thinks +6 and the loser −6, trust +1). **Rule for a friend** (the house that thinks better of yours: +12 from them, −12 from the other; if the law was not with them, trust −3 and legitimacy −1). **Take the gift** (10 credits to your own purse from the richer house, a ruling for them, trust −4, and one time in three it comes out). |

- **You decide your own; nobody decides yours and you decide nobody else's.** A relative who holds an office decides
  their own business, as every other house's magistrate does, by nature: the honourable and the just deal straight,
  the ruthless and the cruel take money about half the time, the loyal and the vengeful rule for their friends.
- **Left undone**, the clerks close it at the year's end and it is noticed (trust −1).
- **When it comes out** that a magistrate took money, they are removed, may not stand for five years, and the house
  loses 5 legitimacy. If it is you, or one of yours, there is an event.
- **Public trust in the magistrates** runs from 0 to 100 and starts at 60. Straight dealing mends it slowly and never
  past 85; money taken breaks it quickly; it drifts a point a year back toward 60. **Below 35, "the magistrates are
  known to be for sale" is one of the things wrong with the Reach**, so it counts toward the times a Caesar needs
  (docs/CAESAR.md) and toward the pressure that brings the great war (docs/BREAKAWAY.md).
- A house that is a party to a case hears of it (an event when it wins, another when it loses). The same quarrel is
  not heard two years running, and no house is before two courts in one year.
- **A relative who stood without a word from the head and lost blames the head** (regard −10, an event). One who was
  spoken for and won knows what they owe (+6).

Where: the top of the ladder panel (Senate, "Your standing") shows your business with its three choices and what each
does, what the Reach thinks of its magistrates, and what every magistrate did this year. The outliner says when your
business is waiting.

## When a saga opens

- Anyone of an age and rank to have climbed already has: a place in a service and 28 years makes a former quaestor;
  36 years and a second rank makes a former praetor.
- The seats are not empty in the first year. They are filled from those people, one of a house to a rung, and none
  from the player's house, which wins its own. They sit until the first election.
- Both happen once per world, so a save made before this piece opens with a full bench and no second seeding.

## Where it is

The Senate screen, under **Your standing · the orders and the ladder**: each rung with its seats, age, prerequisite
and pay; who sits; who of the house is standing and what can be done for them; and the consulship's requirement.
The outliner says when the head could stand and an election is within two years.

## Events

Five, in `content/events/ladder.json`: *The Canvass* (while standing), *The Count Goes Your Way*, *The Count Goes
Against You*, *One Of Yours Is Elected* (a relative who stood by their own decision), *The Aedile's Games*.

## For content

- World flags, each year: `standing_for_office`, `is_magistrate`, `is_aedile` (all of the player's own character).
- World flags, for a year after the deed: `ladder_election_held`, `ladder_elected`, `ladder_lost`, `kin_elected`,
  `games_given`.
- Character flags: `newly_elected`, `standing_for_office`.
- No new effect types.

## A rename that came with it

The law career's third and fourth ranks were called Magistrate and Praetor. With an elected praetor in the game that
read as two different things with one name, so the career now runs Aspirant, Advocate, **Judge**, **Justice**, High
Justice. The estate called the Magistrates is unchanged.

## How it is built

- `src/Core/Systems/LadderSystem.cs` (static): `Bar`, `Next`, `Merit`, `Candidates`, `Tally`, `Hold`, `Seed`, `Open`,
  `Stand`, `Back`, `Dissuade`, `Games`, `SenateStanding`, `Tick`.
- State: `Character.Cursus`, `World.Magistrates`, `World.Candidacies`.
- `LadderCommand` (stand, withdraw, back, dissuade, games) in `src/Core/Commands/HouseholdCommands.cs`.
- `OfficeSystem.Candidate` checks a consul's age and praetorship. `SenateSystem.Tick` holds the ladder's election
  before the consuls'.
- `src/Web/Components/LadderPanel.razor`. `tests/LadderTests.cs` (10). `tools/browser/ladder.js`.

## Checked

- 582 tests. One of them runs the Republic for sixty years and asserts that consuls are still found and at least four
  magistrates sit after every election.
- The soak (eleven starts, three seeds, sixty years, with save and load compared) passes, and its random player
  stands, backs, asks relatives to stand down and gives games. All five events fired.
- In the browser, The Risen Officer stood for quaestor in the first year, called in what was owed, was elected at 30
  with the highest count, and it was written into the record.

## Not in this piece

- Other houses' people do not canvass, back each other or give games; their candidates are chosen by a rule.
- The work of office is one decision a year from three. A case has no facts beyond who is in the right, and a house
  wronged by a bent ruling can only remember it (docs/GAPS.md L2, L5).
- Public trust moves only the troubles of the Reach, not elections or the estates (L7).
- The consuls, the Treasurer and the Speaker have no such business yet (O1).
- No bribery, no prosecution of a former magistrate, no law on the order of offices that the Senate could change.
- The censorship, the tribunate and provincial commands after office are not here.
- The numbers (seats, ages, votes for backing, the weight of merit) are first guesses.
