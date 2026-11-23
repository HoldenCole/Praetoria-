# Titles: the Republic has no nobles

Owner's direction (2026-11-11): while the Reach is a republic there are **no noble titles**. People and houses carry
military and civic styles. Noble titles (Baron, Count, Duke) are introduced only when a throne stands, by proclamation
or seizure, or in a game that starts under the Imperium. The ladders are the same ladders; only the names change.

## The house ladder (`content/titles/titles.json`)

| id (never changes) | rank | Republic | Imperium | legitimacy to hold it | to be raised to it by petition |
|---|---|---|---|---|---|
| `landless` | 0 | Citizen | Landless | 0 | |
| `knight` | 1 | Warden | Knight | 10 | the legitimacy |
| `baron` | 2 | Prefect | Baron | 25 | the legitimacy |
| `count` | 3 | Legate | Count | 40 | the legitimacy; 1 world or 8 seats; a vote of the Senate |
| `duke` | 4 | Proconsul | Duke | 60 | the legitimacy; 2 worlds or 12 seats; a vote of the Senate |
| `archduke` | 5 | First Citizen | Archduke | 80 | the legitimacy; 3 worlds or 16 seats; a vote of the Senate |
| `emperor` | 6 | Dictator | Emperor | 95 | not petitioned for: proclaimed, or taken (docs/THRONE.md) |

Legitimacy is no longer the only bar: see "Rungs that are earned" below.

A Citizen house holds no world; a Warden keeps a station; a Prefect a world; a Legate several; a Proconsul a province.
The First Citizen is the greatest house of the Republic and the only one that may proclaim the Imperium. Dictator is the
Republic's name for the last rung: in practice the era turns the moment it is taken, and the holder reads as Emperor.

`TitleDef.RepublicName` / `ImperialName`, `TitleDef.NameIn(era)`, `TitleCatalog.NameOf(id, era)`. `Title.StyleOf(id, era)`
mirrors the content for log lines written where no catalog is at hand; a test keeps the two in step.

## Rungs that are earned

The owner's list (docs/PLAYTHROUGH.md, "Hard"): "**A title ladder with rungs that must be earned**: land, seats or a carried
vote above Prefect; a breakdown of what legitimacy is made of (P94, P83, S58)."

What the players met. The soldier (S58) went from Citizen to Legate in three clicks in one year, with no vote and no wait, and
each grant added five to legitimacy, which paid for the next. The politician (P83, P94) reached Legate, Proconsul and First
Citizen by three yearly clicks called "Petition the Senate", with no world, no hull and nothing put before the chamber, and
reached legitimacy 100 "without my understanding its sources".

What is built (`src/Core/Systems/ProgressionSystem.cs`, `src/Core/Commands/ProgressionCommands.cs`):

- **Up to Prefect** a petition is granted as before: the house has the legitimacy the rung asks, and it pays 2 influence.
- **From Legate up a rung must be earned.** Three things are asked, and all three are shown on the Senate window beside the
  ladder, each with what the house has and whether it is enough:
  1. the legitimacy the rung asks, as before;
  2. **land or seats**: the worlds the rank has always wanted, or seats in the Senate in their place. Either is enough, so a
     house of politicians with no world has a road as well as a house of landholders;
  3. **a carried vote**: the petition goes before the Senate as a motion (`elevation` in `content/senate/motions.json`,
     "Petition for a rank"). It sits on the docket for the year in which the houses may be lobbied or paid, and is voted at the
     end of the next year like any motion. The house rises only if it is carried. Thrown out, the house keeps its rank and may
     ask again later.
- **A granted petition gives no legitimacy.** The five that came with every grant is gone, on every rung: rank follows standing
  and does not make it. Nothing replaces it. (A claim pressed still gives twelve, and a seizure nothing, as before: the notes
  found no fault with those.)
- **One rung a year**, by any road, as before. By petition above Prefect the pace is slower of itself: asked in one year, voted
  at the end of the next.

**What a seat is.** A seat is what the game already calls a seat: the house's weight in the Senate, the number in the "seats"
column of the table of houses and under "In the Senate" on a house's page (`SenateSystem.Weight`). It is made of the house's
influence in the Senate, one for each rung of its rank, the offices of the Reach it holds that carry weight (the Senior Consul
four, the Junior Consul and the Speaker three each), one for a head who is a Justice, what its order adds (a great general, a
great banker, a house with clients) and what the laws add (the franchise). The other things considered were the rungs of the ladder of offices and provinces held of other houses; seats
were chosen because the player already sees the number, because every one of those things is in it, and because P81 counted
his house by it ("a Legate, praetor and Treasurer with 9 seats").

| rung | legitimacy | land | or seats | the petition |
|---|---|---|---|---|
| Warden | 10 | none asked | none asked | granted |
| Prefect | 25 | none asked | none asked | granted |
| Legate | 40 | 1 world | 8 | voted by the Senate |
| Proconsul | 60 | 2 worlds | 12 | voted by the Senate |
| First Citizen | 80 | 3 worlds | 16 | voted by the Senate |

| | |
|---|---|
| a petition costs | 2 influence of the year's |
| the vote | at the end of the year after the asking (a year later if the Speaker holds it over) |
| turned away, the house may ask for the same rung again | 3 years after it asked, which is 2 after the vote |
| pressing a claim | the claim, and the land or the seats; 1 influence and 2 agents; +12 legitimacy; no vote |
| seizing | an officer at the head, and 2 hulls of the house's own for each rung of the rank; 2 treasury actions and 1 agent; no land, no seats, no vote, no legitimacy |

**How the houses vote on a petition.** By weight, as on every motion, and for reasons the roll call names:

| a house leans | by |
|---|---|
| it is the house that asks | +3 |
| what its head thinks of the petitioner (20 or better, -20 or worse) | +1, -1 |
| whether its head would see the petitioner raised (regard of 10 or better, -20 or worse) | +1, -1 |
| the petitioner's legitimacy is 15 or more above what the rung asks | +1 |
| it holds a claim to the same rung | -2 |
| it is of the petitioner's rank today, and would be passed | -1 |
| it is an old family and the petitioner a new name | -1 |
| it is at war with the petitioner | -2 |
| it depends on the petitioner (a client, a debtor), old ties, a favour owed, blood between them, the chair's word, its own temper on the day | as on any motion |
| it follows the leader of its bench; it has been lobbied; its vote has been paid for | as on any motion |

A bench does not make a party line of petitions: how it voted on one house's rank is not remembered when the next house asks
(`FactionSystem.RecordVotes`, `platform: false`). A consul may veto a petition as any motion. If, between the asking and the
vote, the house has risen another way or has lost the land or the seats, the Senate's leave lapses and the chronicle says why.

**The other houses.** They had no road above Prefect at all: a rival became a Prefect when it was granted a world
(`PetitionFiefCommand`) and went no further except by the great war or a report. They now petition by the player's own
command (`NpcAi.Rise`, `ProgressionSystem.RivalWouldPetition`): a head whose house has the standing, and from Legate up the
land or the seats, asks about one year in four, and pays the same 2 influence. Up to Prefect it is granted. Above that it goes
before the Senate, only one rival's petition at a time; the player is told ("House Severin petitions the Senate for the rank of
Legate..."), and the player's house votes on it and may lobby or pay as on any motion. In trial games of sixty years about
twenty such petitions came before the chamber and about half were carried.

**On the screens.** SENATE, "The ladder of titles": under the legitimacy line, a sentence for the next rung ("The rank of
Legate must be earned: by land or by seats in the Senate, and either is enough. Of these, it lacks the land (0 of 1 world) but
it has the seats (9).") and a line for each thing asked with a tick or a cross: the legitimacy, the worlds, the seats, and
the vote as the houses lean today ("34 for and 22 against"). Each rung of the ladder says in its tooltip what it asks. The
button reads "Petition the Senate"; it says its cost, asks first, and when greyed gives the reason. The outliner's "within
reach" alert says the Senate will vote.

### What I chose

- A seat is the house's weight in the Senate, exactly as the table of houses counts it. The house's own rank is in that
  number (one seat a rung), so a rising house gains a seat with each rung; the steps between the rungs are four.
- Seats wanted: 8 for a Legate, 12 for a Proconsul, 16 for a First Citizen. At the opening of a game about four houses of
  some thirty have eight and one has twelve; after twenty years one or two of the greatest have sixteen.
- Land and seats are alternatives, not both. The worlds wanted are as they were (1, 2, 3).
- The vote is asked of a petition only. A claim pressed needs the land or the seats but is not voted; a seizure needs neither.
- The petition's five of legitimacy is removed and nothing put in its place.
- A house turned away may ask again 3 years after it asked (the chamber's own rule for a repeated motion is 10).
- The petition is one of the two matters the docket holds, and is the house's one motion while it is open.
- A petitioner 15 or more above the rung's legitimacy has a point of lean; an old family is a point against a new name; an
  equal who would be passed is a point against; a rival claimant and a house at war are two against.
- The benches follow their leaders on a petition but keep no party line on petitions.
- A rival that could petition does so one year in four, and one rival's petition for a rank is before the Senate at a time.
- The Senate votes on a petition under the Imperium as in the Republic: the chamber still sits and still votes.
- A world built without a Senate (some tests) grants a petition as before, and counts no seats.

### What is not built

- Other houses do not press claims or seize a rank: they petition only.
- Nothing takes a rank away because the land or the seats that earned it are later lost (the old rule still does: a house
  with no world left is a Citizen again, and a house that steps down from a world drops a rung).
- There is no report (event) for a petition carried or thrown out: the player is told in the news and the chronicle.
- The throne is untouched: Dictator and Emperor are not petitioned for.
- A petition cannot be withdrawn once it is before the Senate.

Tests: `tests/EarnedTitlesTests.cs`. No new command was needed: the act is the old `PetitionTitleCommand`, which now moves the
motion; the vote on a rival's petition is the old `VoteCommand`.

## What legitimacy is made of

The same bullet: "a breakdown of what legitimacy is made of". P94: "Legitimacy hit the cap of 100 by year 18 without my
understanding its sources."

`House.Legitimacy` was a bare number written directly in about a hundred places. Every one of them now goes through
`LegitimacyBook.Move(world, house, by, cause, line)` (`src/Core/Systems/LegitimacyBook.cs`), which moves the number exactly as
before and enters the move in the house's record (`House.LegitimacyRecord`, `src/Core/State/LegitimacyRecord.cs`). Nothing any
source gives was changed, except the petition's five (above). Every house has a record, not only the player's.

The record keeps:

- **where the house began** (its legitimacy when the game opened, or when the house was made), and the year;
- **the total by cause for the whole game**, and **year by year for the last ten years**;
- **the last twelve changes**, each with its year, its cause, by how much, and a plain line ("Censure, carried in the
  Senate", "A marriage into a house of higher rank", "An office granted: Speaker of the Senate", "A report: The Audit").

It always adds up: where the house began and the totals make the number. A move is entered at what it really moved
(legitimacy stops at 0 and at 100, so +10 at 97 is entered as +3). If anything moves the number without saying why, the
difference is entered under "Other things" at the next move or when the year closes (`LegitimacyBook.CloseYear`, the last
step of the year's end), and on the screen at once. In trial games of sixty years nothing was entered there: every rule that
moves legitimacy names its cause.

| cause | what is entered under it |
|---|---|
| Titles and the throne | a claim pressed (+12); a rank held above the house's standing, settling a point a year; the Imperium proclaimed; a standard raised against the throne and broken, or the throne held; an honour conferred |
| The Senate | a motion carried that names the house (recognition +6, censure -6, a commission +2); the levy paid to the chair; a seat by the census; not being seen in the Senate |
| Offices of the Reach | an office granted or lost; the year's business in office and its duties; games given; a first consulship; emergency powers laid down, kept or taken away; what the Reach thinks of its magistrates |
| The courts | a prosecution won or lost; a conviction; defying the praetor's court |
| Laws and the estates | the tithe the house takes from its worlds; what the estates make of its laws |
| Works | a work finished or pulled down |
| Land and the worlds | a world granted, bought back or stepped down from; a province given; holding the capital; a rising conceded or broken; a world that broke away |
| Marriages and the family | marriages made, broken or ended; an heir named over the lawful one; the succession law changed; the head laying the house down; things handed down, sold or given away; a death left unanswered; a place at the Academy |
| War | triumphs and the generals; a civil war's sides; the great war; raiders hunted; companies hired out; states that left the Reach and were brought back |
| The powers beyond the Reach | wars beyond the Reach won and lost; worlds freed; fighting on against a peace the Senate voted |
| Scandal | a bought vote or a bought ruling that came out; what a magistrate of the house did in office; an attempt on a life traced to the house; money spent stirring a war |
| Reports answered | what a choice on a report gave or took, under the report's title |
| Other things | anything that moved the number without naming its cause |

**On the screens.** HOUSE: the Legitimacy tile is clicked to open "What the house's legitimacy is made of": began at, each
cause with its total and what it did in the last ten years, stands at, and the last changes with their years. SENATE, beside
the ladder: "What it is made of" opens the same. HOUSES: every other house's page has the same under its legitimacy, so a
rival's standing can be read the same way.

### What I chose

- Thirteen causes, named above. Land and the worlds is one more than the dozen asked for, so that a world granted or a rising
  broken is not lost among "war" or "other".
- Ten years for "lately", twelve changes kept.
- The same thing twice in one year is one line with the sum ("The year's business in office +2").
- A blood feud is entered under the family; a breakaway state under war; a bribe that comes out under scandal whichever
  system it came out in.
- A choice on a report is entered under the report's title; an honour and a stage of the great war under their own names.

### What is not built

- The record is not shown as a chart over time: it is totals and the last changes.
- Years older than ten keep their totals but not their year-by-year split, and changes older than the last twelve are gone.
- A game saved before this was built opens each house's record where the house stands when it is loaded: what moved it before
  that is not known.

## The career ladders (`content/careers/careers.json`)

| track | Republic | Imperium |
|---|---|---|
| military, *the Navy* / *the Imperial Navy* | Cadet, Ensign, Lieutenant, Commander, Captain, Admiral, **Fleet Admiral** | …, **Grand Admiral** |
| stewardship, *the Administration* / *the Imperial Administration* | Clerk, **Administrator**, **Governor**, Minister, **High Treasurer** | Clerk, **Intendant**, **Viceroy**, Minister, **Imperial Treasurer** |
| law, *the Courts* / *the Imperial Courts* | Aspirant, Advocate, Judge, Justice, **High Justice** | …, **Lord Justice** |

`ranks` is the Republic's ladder and fixes its length; `imperialRanks` renames rungs (same length or shorter).
`CareerTrackDef.RankName(rank, era)`, `CareerCatalog.RankName(track, rank, era)`, `CareerTrackDef.NameIn(era)`.

## Prose

- `{role.title}`: the house's rung by the era's name. `{role.rankName}`: the career rung by the era's name.
- `{era:Republic wording|Imperium wording}`: one authored line, two registers. Exactly two plain alternatives; no
  token inside a token. Both validators check the form.
- The Republic's vocabulary: *the chair* for the house that holds the capital (not *the court* or *the crown*), *the
  Senate* for the public that judges a house, *senator* for the head of a house (not *lord*), *prefecture* for a world
  entrusted by the chair (not *fief*), an *oath* where the Imperium takes a *knee*, *laurel* where it says *crown*.
- A test (`TitleTests.NoRepublicEvent_NamesANobleRank_InItsProse`) fails if any event not gated to the Imperium names a
  noble rank once its era tokens resolve to the Republic's side.

## Also renamed for the Republic
The core's culture id `imperial` is now `aurelian`. The house law *The Court* is *The Hall* (options: open hall, the
customary hall, closed doors; ids unchanged). The honours are the *Order of the Burning Lance* and *Protector of the
Reach*. Engine log lines and screens (the fief petition, the capital changing hands, the Senate's standing rule, the
primer) choose their wording by `World.IsImperium`.

## Still to do
The Imperium-only start (`the_imperial_court`) keeps its noble prose as written. Scenario notes and code comments still
say baron and duke where they describe ids. The Godot shell under `src/Game` is not updated.
