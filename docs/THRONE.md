# A throne that can be lost

Planned 2026-11-17, when the owner said yes to it (docs/GAPS.md R5). **Stage 1 was built on 2026-11-18**, after the
owner confirmed what it rests on: a player who is emperor can be thrown down by another house's doing, and losing the
throne does not end the game. Stages 2 to 4 are still a plan.

## What was true before stage 1

- A throne is taken in three ways: proclaimed (a gate of standing), seized (a fleet over the capital, winning a war
  against whoever sits there), or, since today, voted for life by the Senate (docs/EMERGENCY.md).
- Once taken, **nobody takes it back**. No other house ever tries: the rules that drive other houses never seize. So
  a player who is emperor is emperor until the line ends, and an emperor of another house can be removed only by the
  player.
- When an emperor dies the heir inherits, and nobody disputes it.
- When one house does take a throne from another, the old house drops a rung, its people are marked `deposed`, and
  nothing reads the mark.
- The game writes its epilogue the moment the player takes a throne. Being emperor is treated as the end of a story,
  not the start of one.

## What it should be

A throne is held, not owned. Whoever sits on it has a **hold** that can be read, that weakens for reasons the player
can see, and that others act on when it is weak. Losing it is a turn in the story and not the end of the game.

## The stages

Each is one cycle: built, tested, measured, written up.

### 1. The hold, and a pretender (built: see "Stage 1 as built" below)

- **The hold**, 0 to 100, for whoever is on the throne. Up for standing, for the share of the Reach's hulls, for
  holding the capital, for years on the throne, for the Reach's trust. Down for a throne that was seized, for a
  child or a regent on it, for an unpaid or beaten fleet, for regions in revolt or broken away, for blood owed
  (docs/FEUD.md), for a levy or a proscription.
- **A pretender.** When the hold is weak for some years, the strongest house of ducal rank that thinks ill of the
  emperor, or holds a claim, declares for the throne: a war with that aim. Other houses take a side or stand aside,
  as they do in a civil war under the Republic (the same code).
- **It is decided as a seizure is.** The pretender takes the throne on the terms a player must meet: winning the war,
  stronger by a quarter, and the capital held or a fleet in its orbit. If the war goes the other way the pretender is
  broken: standing lost, the claim struck out.
- **This applies to the player too.** A player who is emperor can be thrown down by a pretender. A player who is not
  can be the pretender, as now, or pick a side.
- **Deposed.** The house that falls keeps its worlds, drops to the rung below, keeps a claim to the throne, and plays
  on. The mark is read: by events, by regard, and by the next pretender, who may be the same house.
- A probe for games under a throne, which the present one skips: how often thrones change hands, how pretenders fare,
  how long a reign lasts.

### 2. The succession

- When an emperor dies the hold is reckoned again for the heir. A grown heir of a strong house inherits as now. A
  child, a weak house, or two heirs with a case each, and it is **contested**: the pretender's war of stage 1, begun
  by the death.
- A regent for a child emperor is a named person, with a regent's own hold and a regent's own temptation.
- An emperor may name an heir, and the naming can be refused by those passed over (the family code already has this
  for a house).

### 3. The Guard

- A guard at the capital with a commander the emperor appoints: a person, with regard for the emperor, a price, and
  ambitions of their own.
- A loyal guard adds to the hold and stops a knife. A guard that is unpaid, slighted or bought takes from it, and at
  the worst opens the gate to a pretender or names one itself.
- For a player who is not emperor: the guard's commander is someone to befriend, buy, or be.

### 4. After the fall

- What a deposed emperor can do: go home and wait, flee to a state that has broken away, raise the claim again, or
  come to terms with the new throne for a great position (the kingmaker's bargain, R16, belongs here).
- What the new throne does with the old house: leaves it, strips it, or proscribes it (C4).
- The epilogue stops being written at the moment of enthronement and is written when a reign ends.

## Stage 1 as built

**The hold.** Out of 100, from 50, for whoever sits the throne. `ThroneSystem.HoldParts` lists what it is made of:

| What | Points |
|---|---|
| The standing of the house | from 25 down to 25 against, by legitimacy |
| Its share of the Reach's hulls | 15 for two in five, 8 for one in four, nothing for 15 in a hundred, 10 or 15 against below that |
| The capital | 10 if the house holds it, 15 against if another does |
| Years on the throne | 5 against for the first five, then a point for every two years up to 10 |
| Took it by force | 10 against, for ten years |
| A child on the throne | 15 against |
| States broken away | 5 against each, up to 15 |
| Blood owed to other houses | 3 against each, up to 9 |
| Losing a war, crews that will not sail, an empty treasury | 10, 5 and 5 against |

It reads **firm** from 70, **holds** from 45, **shaken** from 35, and **failing** below that.

**A pretender.** Two years running below 35, and the strongest house that may rise does: a duke at least, or any
house with a claim; a grown head; a fleet at least three-fifths of the throne's; and no love for the one who sits
there. Never the player's house: the player raises their own standard, for 3 influence, on the same terms. No two
risings within ten years.

**Sides.** Every other house weighs what its head thinks of the two heads, and any blood it is owed by either. Forty
points one way and it goes over to the pretender; forty the other and it stands by the throne; otherwise it stands
aside. The player is asked, by a report and on the Senate screen, and is never sent.

**How it ends.** It is a war for the throne and is not ended by treaty.
- The pretender **takes the throne** the year the terms of a seizure are met: winning the war by five, a fleet larger
  than the throne's (by a quarter while the hold is 45 or better), and the capital held or a fleet in its orbit. A
  seizure now asks the throne's own fleet to be outmatched, not every house's; and a claim will do in place of
  ducal rank.
- The pretender is **broken** if the war goes against them by five after two years, if their fleet falls below
  two-fifths of the throne's, or after eight years. The house loses ten standing and its claim, its head is marked,
  those who went over are remembered at the capital, and the throne gains three standing.

**Cast down.** The house that falls drops to the rung below, keeps its worlds, and **gains a claim**. Its people are
marked, and the mark is now read. A player who is cast down plays on, and may rise again on the claim.

**A throne does not stand empty.** Before this, a house on the throne that lost its last world became landless and
nobody took its place. Now whoever takes that last world has taken the throne; and if a throne is ever empty, the
pretender in the field takes it, or failing one the strongest house of rank.

**The story is not told as the player's when it is not.** The epilogue was written whenever any house took a throne.
It is written now only while the player's own house sits on one it took.

**On the screen.** The Senate screen has a tile, "The throne", with the hold and whose it is, and whether a standard
is raised. Under "Offices, parties and titles" the throne's block is open under a throne: the hold and what it is
made of, the rising and who has gone which way, and buttons to go over, stand by the throne, or raise your own
standard, each with what it costs or why it is refused. Six events (`content/events/throne.json`).

**Tools.** `praetoria throne` plays each start that opens under a throne twice: as written, and with the player's own
house put on the throne in the first year.

**Measured** (twenty games as written, twenty with the player crowned; sixty years each, the player giving no
orders).

| | As written | A knight's house crowned in year one |
|---|---|---|
| The hold, on average | 59 to 62 | 27 to 34 |
| Years the hold was shaken or worse | 26 to 33 in a hundred | 61 to 76 in a hundred |
| Pretenders risen | 32 | 86 |
| Took the throne | 3, in 3 games of 20 | 15 |
| Broken | 21, most of them by the eight years | 46 |
| The player cast down | not on it | 20 games of 20, after 5 to 12 years |

**What stage 1 left out of its own plan.** Sides are taken by regard and blood only, not by the fuller reckoning the
civil war under the Republic uses. A house that goes over does not lend fleets to a common command: each fights its
own war.

## What I have assumed, and would change if told otherwise

1. **The player's throne can be lost to another house's doing**, not only through the player's own mistakes. The hold
   is what the player manages to stop it. *Confirmed by the owner, 2026-11-18.*
2. **Losing the throne does not end the game.** The house goes on, a rung down, with a claim. *Confirmed with it.*
3. **The second rule is untouched.** It protects the Republic: no other house ends that. Under a throne, houses may
   fight over who sits on it as often as the hold allows. How often that should be is the same kind of question as Q1
   (how rare a Caesar is meant to be), and stage 1 will be measured before it is tuned.
4. **A fallen throne is followed by another throne.** Putting a throne down in favour of a Republic is R9, which has
   not been answered, and is left out.
5. **More to do while on the throne** (R6) and **starting as the heir** (R7) are the later expansion the owner named,
   and are not in these stages except where a stage cannot stand without them (a regent in stage 2).

## What it leans on

Already built: seizing a throne (`ThroneSystem`), taking sides in a civil war (`CaesarSystem.Sides`), a throne's
temper toward breakaways (`PolitySystem.ThroneTemper`), blood between houses, emergency powers.
Not built, and needed along the way: something for the other houses to do under a throne at all (C8), so that there
are houses with reasons to rise.
