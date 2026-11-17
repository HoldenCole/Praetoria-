# Playing a known story

Written 2026-11-17. The question: if someone sits down to play Darrow, Nero au Augustus, Reinhard von Lohengramm, Yang
Wen-li, Palpatine or Paul Atreides, how far does the game carry them, and where does it stop? (The first pass read
"Nero" as the Roman emperor. The owner meant Nero au Augustus of Red Rising; both are here, the emperor as "being
emperor".)

Each story was cut into its beats, and each beat was checked against the engine: is there a command, an event or a
start that lets the player do it. **Yes** means it can be done today. **Partly** means something like it can be done
and what is missing is named. **No** means there is no road. The verdicts come from reading the code and content, and
from playing the nearest start where a claim needed proof. The gaps this found are rows R1 to R18 in docs/GAPS.md.
The owner's answers of 2026-11-17 are at the foot of the page.

## In short

| Story | Yes | Partly | No | Where it stops |
|---|---|---|---|---|
| Darrow (Red Rising) | 2 | 4 | 4 | After the Institute. No false identity, no service in a greater house, no rising |
| Reinhard von Lohengramm | 1 | 7 | 3 | At the first command. Under a throne nobody is given a fleet |
| Yang Wen-li | 0 | 2 | 0 | Close. The recall and the choice to obey are built; the Empire he defends against is not |
| Nero au Augustus | 3 | 6 | 2 | At the bargain. You cannot put someone else on a throne, or be paid for it |
| Being emperor (the Roman Nero) | 0 | 7 | 4 | On the throne. An emperor has three acts and cannot fall |
| Palpatine | 3 | 2 | 2 | After the powers for life. No hidden hand, and nothing to rule with |
| Paul Atreides | 0 | 5 | 1 | After the fall. A landless heir has no road back |

**The game is good at getting to power and thin at four things these stories turn on:**

1. **Life under a throne.** Four of the six stories are set in an empire. Under a throne the ladder of offices,
   elections, clients and patrons, the triumph, the recall and the march are all switched off, and nothing takes their
   place (C8). A Reinhard-shaped rise is easier to play in the Republic than in the Imperium.
2. **Rising by service.** Darrow, Reinhard and Paul all start with nothing and are given a command by someone greater.
   Here a house without land cannot get a fleet at all (Q15), nobody can serve in another house's household, and a
   throne hands out no commands (R1).
3. **What is hidden.** Darrow's false identity and Palpatine's hand behind the war have nothing to stand on. The game
   has no secrets that can come out (R2).
4. **Losing.** Nobody is taken prisoner (R3). A throne, once held by the player, cannot be taken from them (R5). A
   house that loses everything goes on but has no way back (R11).

**What already works**, and reads like the stories: the Institute and the cohort you keep from it; the commander who
fights with the fleet, with stance, loadout and prizes; the Republic's Caesar road (the triumph, the recall, obey or
cross); a match for love and an elopement; the Senate climb and the favours owed along it; a seized throne that sits
on a powder keg; and losing every world while the line goes on.

## One defect, fixed

Two of the four checks found it and one reproduced it: the ordinary "seize a title" command could carry a house up
the last rung of the ladder to `emperor`, with no change of era, nobody deposed and no proclamation. On The Old Lion
it took two years: duke, archduke, emperor, in a Republic that went on as before. The throne is now not a rung of the
title ladder: it is taken by proclamation or by a fleet over the capital and no other way (`ProgressionSystem.NextTitle`,
with a test).

## Darrow

Nearest start: `the_risen_officer` (its own notes call it "the core Red Rising register"), or a custom Imperium
character with the Academy year.

| Beat | Verdict | What is there, what is missing |
|---|---|---|
| Born at the bottom of a society of castes | Partly | The lowest birth is a landless new house. There are no castes and nothing is barred by birth (R13) |
| Remade and sent in under a false name | No | No secrets, no hidden allegiance, nothing to be found out (R2) |
| Wins the Institute | Yes | `the_institute`, the war of the standards, a cohort that stays with you. Winning it is then read by nothing (R14) |
| Taken into a great lord's household | Partly | Clients and patrons exist in the Republic. You cannot join another house's household or command its ships, and patronage is off under a throne (R1, J6) |
| Duels and a blood feud | No | No duels (F3), and a killing does not bind the kin to answer it (R4) |
| Commands fleets, takes ships by boarding | Partly | The boarding stance takes prizes. A siege is a timer. A landless house cannot get a fleet (Q15) |
| Love and marriage across houses | Yes | Courtship and elopement. A match is refused while the houses are at war; a spouse then does nothing (M3) |
| Betrayed and held captive | No | Nobody is taken prisoner (R3) |
| Leads the low orders in a rising | No | The people do not rise for anyone (G6) |
| Topples the throne, founds a republic, is later recalled by it | Partly | Taking the throne makes you Emperor. A throne cannot be put down for a Republic (R9, G2) |

## Nero au Augustus

As the owner put it: born into a famous noble house; the house wiped out; made a deal to overthrow a dictator and
install a new one; got a great position for it; plotted and carried out revenge on a rival house. Nearest starts:
`the_fallen_house` for the beginning, `the_old_lion` or a custom duke for the middle.

| Beat | Verdict | What is there, what is missing |
|---|---|---|
| Born into a great house | Yes | `the_old_lion` is a duke, `the_circling_heir` a count; a custom character can be a count, or an archduke in free mode. A custom character always heads a new house, never a child of an existing one |
| The house is all but destroyed for a failed rebellion, and he survives as its heir | Partly | `the_fallen_house`: a baron with one world, one hull and two credits. The fall is only in the start's notes: no cause, no ruler who ordered it, no dead, no claim on what was lost (R17). In play no house is ever purged (C4) |
| Makes a bargain with a claimant to bring down the ruler and seat them | Partly | In the Republic you can declare for a general who has crossed, and your fleets fight for him. Nothing is asked or promised first, and it needs another house's Caesar, which is nearly absent (Q1). Under a throne there is no road at all: no other house ever tries for it, and you cannot back one that would (R16) |
| Is given a great position for it | No | A general's backers get a little standing and regard and a flag nothing reads. No world, office or title is given. Under a throne "the Emperor holds every office", and his only gift is a company's monopoly (R16, R1) |
| Governs a region under the sovereign | Partly | Your own worlds, governors of your own blood, the tithe, fleets, private war, the Senate. Nothing is held from the throne, and a region is only an allegiance and a lawlessness figure (C8, R6) |
| A long hatred of one house | Partly | Regard between persons, old enemies who vote against you, a rival named at creation. All of it is between persons and dies with them: a house has no enemy (R4) |
| Plots and carries out his revenge | Partly | An assassin (any adult of another house; odds from 5 to 90 in a hundred by skill), war in either era with no leave needed, sieges, a world ceded at the peace, censure, embargo, a takeover of their companies. No charge can be laid against a house, and the house you strike does nothing in answer (R4, C4) |
| Takes a gifted nobody into his household | Yes | A protégé, sworn, with the head as mentor; adoption puts them in the line. No named second (R14) |
| Sends his children through the Institute, judges them by it, and disowns one | Partly | An upbringing, a tutor, and naming an heir over the lawful one. Only the player goes to the Institute (E1), and nobody can be disowned (R18) |
| Rebels against the sovereign he made | Yes | Declaring independence under a throne, or seizing it with a fleet over the capital. The sovereign is never one he seated |
| Killed by the son he cast out | No | Kin defy and defect. They cannot strike at the head: an assassin must be of another house, other houses' knives spare the head, and no event kills the player (R18) |

**What would do most for this story:** the bargain (R16) with a throne that can be lost (R5), since one cannot be seated
unless another can fall; blood between houses (R4); and kin who act (R18).

## Reinhard von Lohengramm and Yang Wen-li

Nearest starts: `the_imperial_court` for Reinhard, `the_risen_officer` or `the_new_man` in the Republic for Yang.

| Beat | Verdict | What is there, what is missing |
|---|---|---|
| A poor noble under an empire | Yes | The start exists. There is then little for him to do |
| His sister is taken as the emperor's consort, and it opens doors | No | Kin at court bring nothing; the emperor takes one spouse (R12) |
| A friend for life at his side | Partly | An oath, a place in the household, an admiral's post. No named second, and his death changes little (R14) |
| Rises by winning battles | Partly | Stance, loadout and doctrine are set and the battle resolves. Under a throne there is no state command to be given (R1) |
| Gathers a circle of admirals | Partly | The household holds two plus your rank. One fleet to a system |
| Raised in title and takes a new name | Partly | A title by petition. A house cannot be renamed in play (R14) |
| The emperor dies and the succession is fought over | No | The heir inherits, uncontested (R5) |
| Regent for a child emperor, then takes the throne | Partly | The throne can be seized. You cannot be regent over someone else's (R7) |
| Reforms the state | Partly | One law is the emperor's to set (R6) |
| Conquers the rival state | No | Foreign powers are numbers, not places (R10) |
| Dies young and leaves a regency | Partly | Illness and a regency. The regent is nobody in particular |
| *Yang:* recalled by his government and obeys | Partly, nearly yes | The recall motion, the choice to obey, the Senate's sword |
| *Yang:* holds a corridor against an empire | Partly | Chokepoints and fortress worlds. Foreign powers raid; they do not invade (R10) |

## Being emperor (the Roman Nero)

No start puts the player on a throne or next in line. The quickest proper route is a custom Republic character in
free mode who holds the capital and proclaims in about the second year.

| Beat | Verdict | What is there, what is missing |
|---|---|---|
| Put on the throne by a mother's scheming | No | An emperor can be killed, but his house keeps the throne. Adoption is into your own house only (R7) |
| Ruled through advisors while young | Partly | A regency and delegation, both thin |
| Kills kin and remarries | Partly | Divorce works. You cannot order a death in your own house; `marked_for_death` is set by events and read by nothing in the rules (R6) |
| The Guard makes and unmakes emperors | No | No guard (C8, R5) |
| Games and spectacle | Partly | The estates are real. Games are an aedile's, and there are no aediles under a throne (R6) |
| The fire, and a palace on the ruins | Partly | Megastructures only |
| Treason trials and confiscations | Partly | An edict proscribes and shares can be seized. No trial, no world confiscated, and nobody conspires against you (C4, R6) |
| Debases the coinage | No | (R6) |
| The provinces revolt and he is declared an enemy | Partly | Worlds revolt and regions break away. Nobody can take the throne from the player (R5, Q1) |
| Flight, death, and a year of pretenders | No | The heir inherits. The `deposed` flag is read by nothing (R5) |
| The year's work of ruling | Partly | Three acts of his own: set the Imperial Edict, grant a monopoly, proscribe a house's shares |

## Palpatine

Nearest start: `the_new_man` or `the_old_lion`.

| Beat | Verdict | What is there, what is missing |
|---|---|---|
| A minor senator's rise | Yes | The ladder, canvassing, clients, factions, favours |
| A crisis he made himself | Partly | Stirring a secession, traced one time in three. Starting a crisis is something only the other houses' rules do (R2) |
| Elected to the chair | Yes | Consul at forty after a praetorship; there are two |
| Voted emergency powers, and keeps them | Yes | Built 2026-11-17 (docs/EMERGENCY.md): voted in a danger, extended, kept, and put to the Senate for life |
| Commands both sides of the war | No | No hidden hand (R2) |
| Destroys an order and is declared emperor | Partly | The Senate can now vote him the powers for life, and that ends the Republic. No proscription (C4) |
| Rules through governors and dissolves the Senate | No | (C8, R6) |

## Paul Atreides

Nearest start: `the_fallen_house`.

| Beat | Verdict | What is there, what is missing |
|---|---|---|
| A fief imposed, on the one world a resource comes from | Partly | A fief by petition. No good has a single source (helium-3 has nine) |
| The house is destroyed and the heir escapes | Partly | A house can lose every world and the story goes on; a child can be fostered out of reach. There is then no road back (R11, Q15) |
| Taken in by a people, a faith, an army of believers | No | No peoples, no religion (R13) |
| Threatens the resource to force the throne's hand | Partly | The throne can be seized by a fleet. A good cannot be withheld (R15) |
| The throne by marriage to the heiress, with another he loves | Partly | The heiress exists. A match with her gives a claim that leads nowhere, and there is one spouse (R7, M2, M5) |
| A holy war in his name | Partly | Wars, yes. Nothing in his name |

## What would open the most stories

In the order that unlocks the most, for the least building:

1. **A command by service (R1, with Q15).** A house without land is given ships by a patron in the Republic or by the
   throne under one, and owes for it. Opens Darrow's middle, Reinhard's whole rise and Paul's return. A cycle.
2. **A throne that can be lost (R5).** A guard, a pretender, a contested succession, and what happens to an emperor
   who falls. Makes Nero playable and gives Reinhard his civil war. Several cycles; needs a plan.
3. **Acts of an emperor (R6), and a start as the heir (R7).** A cycle and a small scenario.
4. **Something hidden that can come out (R2).** Opens Darrow's first act and Palpatine's whole method. A cycle.
5. **Captivity and the blood feud (R3, R4, with F3).** A cycle each.
6. **Small things that make a story feel owned (R14):** a named second, a death that marks you, a new name for a
   house, the Institute's winner remembered. Hours each.

Two rows touch what the owner has set aside (the forms of government, G1 to G7) and wait with them: emergency powers
(R8) and a throne put down for a Republic (R9). The rising that Darrow leads is G6 itself. Two more are new systems and
wait on the rule that none is begun until the worklist is short: a rival state on the map (R10), and castes and a faith
(R13).

## The owner's answers, 2026-11-17

- **The Institute:** how well you did is to be remembered, and winning most of all. Built the same day
  (docs/ACADEMY.md, "What is remembered").
- **The blood feud (R4):** yes. Built the same day (docs/FEUD.md), with duels.
- **Emergency powers (R8):** yes. Built the same day (docs/EMERGENCY.md).
- **A throne that can be lost (R5):** yes.
- **More to do as emperor and as the heir (R6, R7):** yes, as a later expansion.
- **A faith and castes (R13):** not for now.
- Not yet answered: a throne put down for a Republic (R9), and a rival state on the map (R10).
