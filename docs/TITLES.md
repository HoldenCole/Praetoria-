# Titles: the Republic has no nobles

Owner's direction (2026-11-11): while the Reach is a republic there are **no noble titles**. People and houses carry
military and civic styles. Noble titles (Baron, Count, Duke) are introduced only when a throne stands, by proclamation
or seizure, or in a game that starts under the Imperium. The ladders are the same ladders; only the names change.

## The house ladder (`content/titles/titles.json`)

| id (never changes) | rank | Republic | Imperium | legitimacy to hold it |
|---|---|---|---|---|
| `landless` | 0 | Citizen | Landless | 0 |
| `knight` | 1 | Warden | Knight | 10 |
| `baron` | 2 | Prefect | Baron | 25 |
| `count` | 3 | Legate | Count | 40 |
| `duke` | 4 | Proconsul | Duke | 60 |
| `archduke` | 5 | First Citizen | Archduke | 80 |
| `emperor` | 6 | Dictator | Emperor | 95 |

A Citizen house holds no world; a Warden keeps a station; a Prefect a world; a Legate several; a Proconsul a province.
The First Citizen is the greatest house of the Republic and the only one that may proclaim the Imperium. Dictator is the
Republic's name for the last rung: in practice the era turns the moment it is taken, and the holder reads as Emperor.

`TitleDef.RepublicName` / `ImperialName`, `TitleDef.NameIn(era)`, `TitleCatalog.NameOf(id, era)`. `Title.StyleOf(id, era)`
mirrors the content for log lines written where no catalog is at hand; a test keeps the two in step.

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
