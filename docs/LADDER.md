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
- **Left undone**, the clerks close it at the year's end and it is noticed (trust −1). An aedile who does nothing is
  noticed only if they could have paid for the works; one who could not is merely poor.
- **When it comes out** that a magistrate took money, they are removed, may not stand for five years, and the house
  loses 5 legitimacy. If it is you, or one of yours, there is an event.
- **Public trust in the magistrates** runs from 0 to 100 and starts at 60. See "What trust does" below. **Below 35,
  "the magistrates are known to be for sale" is one of the things wrong with the Reach**, so it counts toward the
  times a Caesar needs (docs/CAESAR.md) and toward the pressure that brings the great war (docs/BREAKAWAY.md).
- A house that is a party to a case hears of it (an event when it wins, another when it loses). The same quarrel is
  not heard two years running, and no house is before two courts in one year.
- **A relative who stood without a word from the head and lost blames the head** (regard −10, an event). One who was
  spoken for and won knows what they owe (+6).

Where: the top of the ladder panel (Senate, "Your standing") shows your business with its three choices and what each
does, what the Reach thinks of its magistrates, and what every magistrate did this year. The outliner says when your
business is waiting.

## The bench and the seat

The same rule for the positions that are not elected offices.

**A senator's year.** The head of a house in the Reach has the house's seat, and each year one thing to do with it
besides the chamber's motions:

| Choice | What it does |
|---|---|
| **Read the accounts** | If a magistrate of another house took money this year and has not been found out, you may follow it to them (one time in two): they are removed and barred, trust +2, you gain 1 influence and a little legitimacy, and their house thinks 15 the less of you. If there is nothing, or nothing provable, trust +1. |
| **Speak for your region** | The region where your worlds are holds a little closer to the Reach (allegiance +2), its houses think +3 of you, legitimacy +1. |
| **Work your bench** | Your party's leader thinks +6 of you and you gain 1 influence. If you lead it, every house on the bench thinks +3 of you. |

Three years running of doing nothing with the seat is remarked on: legitimacy −2 and an event.

**Somebody else reads the accounts too.** Each year one upright senator of another house audits last year's
business. Money a magistrate took (yours included) and got away with at the time may come out through them a year
later.

**A Judge's hearing.** Anyone of the house who is a Judge or above in the law (and is not a sitting praetor, who has a
court already) hears one matter a year: an estate has brought a house before the bench, and the law is with one of
them. **Rule on the law** (the side in the right thinks better of your house, the other worse; trust +1), **find for
the house** whatever the law says (+8 from the house, −6 with the estate; trust −1 if the law was against it), or
**find for the petitioners** (+6 with the estate, −8 from the house; the squares do not mind a ruling bent their way). The
estate's approval is the same number the Chancery shows (docs/POLITICS.md).

**A Justice's appeal.** A praetor's ruling that went against the plain law is appealed the next year before a
Justice who is of none of the houses in it: one of your house if there is one, otherwise a Justice of another house,
who decides by nature (the honourable overturn; one obliged to the praetor's house upholds). **Overturn** (the house
that won wrongly pays back double, the wronged house thinks +10 of the Justice's, the other two −8 each, trust +3),
**send it back** (the wronged house is at least heard), or **uphold** (the praetor's house +8, the winner +4, the
wronged −10, trust −2). So a bent ruling against your house may be mended by the courts, and a bent ruling of your
own may be undone.

**The charge of an office of the Reach.** The consuls, the Treasurer and the Speaker each carry one decision a year.
(The Admiral has the Reach's fleets, which is work enough.) One who holds such an office spends the year on it: it
takes the place of the senator's year.

| Office | The choices |
|---|---|
| **Consul** (both) | **Call the magistrates to account**: up to two who took money this year are looked at, each caught one time in two and removed; trust +2 and +2 for each caught; every magistrate's house thinks −2 of you. **Send the fleet onto the lanes** of the most lawless region: lawlessness −4 for 4 credits of the capital's; its houses think +4. **Spend the year on your bench**: its houses +4, influence +2, trust −1. |
| **Treasurer** | **Balance the books**: 5 credits found for the capital's purse, trust +1, legitimacy +1. **Forgive a region its levy**: the region that holds loosest gains 4 allegiance for 5 credits of the capital's; its houses think +4. **Let the contracts to your own house**: +10 credits, trust −3, and one time in four it comes out: the office is taken away and you may not stand for five years. |
| **Speaker** | **Keep the order fair**: every bench's leader +2, trust +1, legitimacy +1. **Bring your own business forward**: influence +2, your bench's leader +4, the others −3. **Give an enemy no day**: the house you are worst at odds with thinks −10 of you, those who hate it think +5, trust −1. |

Other houses' officers decide by nature, at once, and what they do reaches you: a Treasurer who forgives your own
region its levy and a Speaker who keeps your house off the floor each bring an event. A great office held and not used
is noticed: trust −1 and legitimacy −1. A bench or a desk left empty costs its own house (the commons, or the
merchants, think one the less of it), not the Reach's name.

**A desk, and a posting.** The other two careers ask something of their ranks as well, of the people of your house.

- **A Governor, a Minister or a High Treasurer** has the year's returns from the house's region across their desk.
  **Assess it fairly** (the merchants +2, the region's allegiance +1, trust +1), **press the collection** (6 credits to
  the house, the commons −4, allegiance −1), or **keep a share** (6 credits to your own purse, trust −2, and one time
  in four the returns are reopened: you are broken a grade and the house loses 3 legitimacy).
- **A Commander or above with no fleet under them** has a year ashore. **Drill the house's crews** (every fleet of the
  house +4 loyalty), **patrol the home lanes** (lawlessness −2), or **sit on a board of inquiry** (the officers +2,
  trust +1, influence +1). With a fleet, the command is the work and there is no posting.

**The lowest ranks.** Below those, a rank still has a year's work, and how it is done is the junior's own.

| Rank | The work | The choices |
|---|---|---|
| **Aspirant, Advocate** | A brief before a lesser court: a client house against another | **Plead it on the law**: learn the law and some speaking; win (likelier the better you speak and know the law) and the client thinks +4 of your house. **Take the side that pays**: 4 credits to your own purse, the other house −3. **Plead for one who cannot pay**: practice at speaking, and the commons +2. |
| **Clerk, Administrator** | A year of the administration's ledgers | **Keep them exactly**: learn the administration; the house is a credit the better. **Make yourself useful**: a little influence, a little economics. **Pad an account**: 3 credits to your purse; one time in five it is found and written against you as a failure. |
| **Cadet, Ensign, Lieutenant** with no command | Watches and drills | **Stand your watches**: learn tactics. **Volunteer for the patrol**: learn gunnery, the home lanes −1 lawlessness, and one time in ten you come back hurt. **Make friends in the mess**: the officers +1, a crew of the house's a little steadier, some leadership. |

What is learned is on top of what the service teaches anyone by the year. A junior who lets the year go by loses only
the learning. With this, every rank of every service has work.

A relative with a desk, a posting or a junior's work decides for themselves.

**The seat is the house's.** If the head dies or lays the house down after the year's business is drawn up, the new
head has the year.

The player decides their own; a relative on the bench decides theirs by nature. Where: "Your duties this year" at the
top of the Senate's standing view, above the ladder. The outliner says when a matter is waiting.

## What trust does

**How it moves.**

- **Ordinary honest dealing mends it, and no more.** Closing the accounts straight, ruling on the law, keeping the
  order fair: each is a single point, and all of them together mend trust by **one point a year at the most**, and
  only back up to 60. Nobody is admired for doing the job.
- **Something notable lifts it past 60**, to 85 at the highest: a thief followed to their office (+2), the magistrates
  called to account (+2), a bent ruling overturned (+3).
- **Money taken breaks it**: a quaestor's favour −3, a praetor's gift −4, a ruling bent for a friend −3, the Reach's
  contracts let to the Treasurer's own house −3, and −3 again each time one of them is found out. Catching a thief no
  longer outweighs the theft.
- Above 60 a good name fades a point a year. **Below 60 nothing mends it but honest dealing.**

**What it does.**

| Trust | Elections | The commons | Standing |
|---|---|---|---|
| **Below 35** (for sale) | A house with a scandal loses 12 votes at every count | Think 3 the less, every year, of every house that holds an office | The house that holds the capital loses a point of legitimacy a year, down to 20 |
| **35 to 49** | A house with a scandal loses 12 votes | | |
| **50 to 74** | A house with a scandal loses 6 votes | | |
| **75 or more** (honoured) | A house with a scandal loses 6 votes | Think 1 the better, every year, of every house that holds an office | Every house that holds an office gains a point of legitimacy a year, up to 70 |

- **A scandal** is a magistrate or an officer of the Reach removed for taking money. It lies on their house for **ten
  years**, at the count for the ladder and for the consulship both. The person removed loses 8 votes more, for good.
- There is an event when trust first falls below 35, and another when it is believed again (back to 50).
- What a person of your house does to an estate (a judge's ruling, a governor's returns, a year on a board) now
  **lasts**: it fades a point a year instead of being gone within a year.

The ladder panel says in a line what trust is doing this year, and tells you if your house is under a scandal, until
when, and what it costs.

## When a saga opens

- Anyone of an age and rank to have climbed already has: a place in a service and 28 years makes a former quaestor;
  36 years and a second rank makes a former praetor.
- The seats are not empty in the first year. They are filled from those people, one of a house to a rung, and none
  from the player's house, which wins its own. They sit until the first election.
- Both happen once per world, so a save made before this piece opens with a full bench and no second seeding.

## A junior's thread, and who else minds a magistrate for sale

Closed straight off the register on 2026-11-15 (docs/GAPS.md J4, L9).

**Somebody above them, by name.** A junior's year is no longer three things in the air. The advocate's brief is heard
by a named judge; the clerk has a named senior at a desk; the junior officer serves in a named ship of the house under
whoever commands it. It is the same person year after year while they live and keep their place, and one of the
junior's own house if it has one.

| The junior does | What the one above thinks |
|---|---|
| Pleads on the law | +2 for a win, +1 for a case well lost |
| Takes the side that pays | −2 |
| Pleads for one who cannot pay | +1 |
| Keeps the ledgers exactly | +2 |
| Makes themselves useful | +4 |
| Has a padded account found | −8 |
| Stands watches / volunteers / makes friends in the mess | +2 / +3 / +1 |

What that one thinks is the junior's standing, and it does three things. It moves the odds of winning a brief (a
fifth of it, 8 either way at most). After **three good years** under them and a regard of **20**, they speak for the
junior, who goes up a step sooner than the years alone would have taken them: once at each rank, and never faster
than the service lets anybody rise. And an advocate who appears before a judge of their own house who is not upright
wins cases they would have lost (15 in a hundred); each such win is seen, and costs the Reach a point of trust.

**The other estates.** While the magistrates are not believed (trust under 35) the merchants and the magistrates' own
order think 2 worse a year of every house in office, beside the commons' 3; while they are trusted (75 or more) each
thinks 1 better.

Left, and mapped: this is kept for the player's house only, and the one above has no further use for a junior (J5);
trust is still one number for the whole Reach (L10).

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
- The work of office is one decision a year from three. A case has no facts beyond who is in the right (docs/GAPS.md
  L5). A bent ruling may be appealed, but nobody can prosecute a former magistrate or bribe a sitting one (L2).
- Other houses' senators do not spend their year (but for the one who audits), and their judges hear no hearings
  (docs/GAPS.md X2).
- Public trust is one number for the whole Reach: a region's own courts, or one office's name, are not told apart
  (docs/GAPS.md L10).
- An office's charge is one of three things a year. A consul does not preside, a Treasurer has no budget with figures, a Speaker orders no actual motions (docs/GAPS.md O2).
- No bribery, no prosecution of a former magistrate, no law on the order of offices that the Senate could change.
- The censorship, the tribunate and provincial commands after office are not here.
- The numbers (seats, ages, votes for backing, the weight of merit) are first guesses.
