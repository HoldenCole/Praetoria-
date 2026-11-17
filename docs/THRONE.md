# A throne that can be lost: the plan

Written 2026-11-17. The owner said yes to this on the same day (docs/GAPS.md R5). It is several cycles, so by
docs/PROCESS.md it is planned before it is built. Nothing on this page is built yet.

## What is true today

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

### 1. The hold, and a pretender

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

## What I have assumed, and would change if told otherwise

1. **The player's throne can be lost to another house's doing**, not only through the player's own mistakes. The hold
   is what the player manages to stop it. (The owner's yes was to exactly this, as I read it.)
2. **Losing the throne does not end the game.** The house goes on, a rung down, with a claim.
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
