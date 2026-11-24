# The laws, reviewed

**The owner asked (2026-11-24):** "Then take a look at laws that are currently in the game and if they do anything and then
more laws that can be done. Executive edits, laws to vote on, local laws, etc"

This is the first half: what laws there are, whether each does anything, whether anybody uses them, what was wrong and what
was mended. The second half, the new kinds of law, is at the end.

## How it was measured

Two measures, both kept in the repository so they can be run again.

- **Does it do anything.** `dotnet run --project src/Tools -- laws`. For every option of every law, and for each single thing the
  option says it does, the same game is played twice with a player who does nothing: once with that one effect in force, once
  without. Everything else is held the same, and what the estates think of the law is set aside. If nothing in the saved game
  differs after a year, it is played for six. An effect that moves nothing in six years is either read by no rule or waits on
  something the house has to do first.
- **Does anybody use it.** `LAWS_USE=1 dotnet run --project src/Tools -- laws --seeds 2`. Two games of thirty years with a player
  who does nothing: how many times each law was changed by a house other than the player's, what was in force at the start and
  at the end, and what the Senate decided.

## 1. What there is

Twenty-six laws. A law belongs to a position: only whoever holds the position can set it. Each has a default and one or two
other options. An option does things (the third column) and the six estates each think something of it (the fourth). An
estate's regard for a house drifts three points a year toward the sum of what it thinks of the house's laws; at +30 it is loyal
and at -30 hostile, and then it does things of its own. Amounts are in credits as the game shows them.

**The head of any house**

| Law | Option | What it does | Who minds |
|---|---|---|---|
| The Tithe | Lenient tithe | sets the tithe itself | commons +15, magistrates -5 |
|  | Customary tithe (the default) | as things are |  |
|  | Harsh tithe | sets the tithe itself | commons -25, merchants -10, magistrates +5 |
| The Levies | Volunteers only | manpower -1 a world | commons +10, officers -10 |
|  | The household levy (the default) | as things are |  |
|  | Conscription | manpower +2 a world; unrest +1 a year | commons -20, officers +15 |
| The Hall | An open hall | nothing of itself | commons +10, old blood -10 |
|  | The customary hall (the default) | as things are |  |
|  | Closed doors | legitimacy +1 every five years; -100 credits a world | old blood +20, commons -10, magistrates +5 |
| The Household Guard | No standing guard | crews recover readiness -2 a year; the fleets' keep -15% | officers -10, merchants +5 |
|  | A standing guard (the default) | as things are |  |
|  | The guard doubled | crews recover readiness +5 a year; the fleets' keep +25% | officers +20, merchants -10 |

**A house that holds a world (on all its worlds)**

| Law | Option | What it does | Who minds |
|---|---|---|---|
| Tariffs | Free trade | what you pay on the exchanges -10% | merchants +20, magistrates -5 |
|  | Customary duties (the default) | as things are |  |
|  | Protective tariffs | +100 credits a world; charters paid to you +50%; what you pay on the exchanges +15% | merchants -20, commons +5, magistrates +5 |
| Labour | Guild rights | unrest -1 a year | commons +15, merchants -10 |
|  | Customary labour (the default) | as things are |  |
|  | Bonded labour | materials +1 a world; +100 credits a world; unrest +2 a year; the people grow -1 faster | commons -25, merchants +10, magistrates -5 |
| Justice | Lenient courts | unrest +1 a year; prosperity +1 a year; lawlessness +1 a year | commons +10, magistrates -10, frontier +5 |
|  | Customary justice (the default) | as things are |  |
|  | Iron law | unrest -1 a year; prosperity -1 a year; lawlessness -2 a year | commons -10, magistrates +15, frontier -15 |
| The Franchise | The closed franchise | legitimacy +1 every five years | old blood +10, commons -10 |
|  | Customary franchise (the default) | as things are |  |
|  | The open ballot | +1 weight in the Senate | commons +15, old blood -15 |

**The house with most worlds in a region**

| Law | Option | What it does | Who minds |
|---|---|---|---|
| March Law | Open lanes | lawlessness +1 a year | frontier +10, merchants +5 |
|  | Patrols (the default) | as things are |  |
|  | Martial law | lawlessness -3 a year; -100 credits a world; the region's allegiance -1 a year | frontier -20, officers +10, commons -10 |
| Settlement | Closed land | lawlessness -1 a year | frontier -10, old blood +5 |
|  | Customary settlement (the default) | as things are |  |
|  | Homesteads | the people grow +1 faster; prosperity +1 a year; lawlessness +1 a year | frontier +15, commons +5, old blood -5 |

**Whoever holds the capital**

| Law | Option | What it does | Who minds |
|---|---|---|---|
| The Tolls of the Reach | Free passage | tolls -1 to whoever holds a lane world | merchants +15, frontier +5 |
|  | Customary tolls (the default) | as things are |  |
|  | Heavy tolls | tolls +2 to whoever holds a lane world | merchants -25, magistrates +5 |
| Letters of Marque | Forbidden | lawlessness -1 a year | merchants +10, frontier -10 |
|  | Tolerated (the default) | as things are |  |
|  | Sanctioned | lawlessness +2 a year | frontier +15, merchants -20, officers +5 |

**The Admiral of the Reach**

| Law | Option | What it does | Who minds |
|---|---|---|---|
| Fleet Doctrine | Conserve the hulls | the fleets' keep -15%; crews recover readiness -3 a year | officers -5, merchants +5 |
|  | Customary doctrine (the default) | as things are |  |
|  | Aggressive doctrine | the fleets' keep +15%; crews recover readiness +5 a year | officers +10 |

**The Treasurer of the Reach**

| Law | Option | What it does | Who minds |
|---|---|---|---|
| The Exchanges | Open exchanges | what you pay on the exchanges -15% | merchants +15 |
|  | Customary exchanges (the default) | as things are |  |
|  | Guarded exchanges | what you pay on the exchanges +15% | merchants -15, magistrates +5 |

**The Speaker of the Reach**

| Law | Option | What it does | Who minds |
|---|---|---|---|
| Standing Orders | An open docket | a motion costs others -1 influence | old blood -5, commons +5 |
|  | Customary orders (the default) | as things are |  |
|  | A tight docket | a motion costs others +1 influence | old blood +5, magistrates +5 |

**The Emperor**

| Law | Option | What it does | Who minds |
|---|---|---|---|
| The Imperial Edict | Clemency | unrest -1 a year | commons +10, old blood +10 |
|  | The customary peace (the default) | as things are |  |
|  | Proscriptions | unrest +1 a year; legitimacy +2 every five years | old blood -20, commons -15, officers +5 |

**The leader of a bench**

| Law | Option | What it does | Who minds |
|---|---|---|---|
| The Bench's Platform | A loose bench (the default) | as things are |  |
|  | A reform platform | +1 weight in the Senate | commons +10, old blood -10 |
|  | A platform of tradition | +1 weight in the Senate | old blood +10, commons -10 |

**The Senate, by an Act carried**

| Law | Option | What it does | Who minds |
|---|---|---|---|
| The Common Law | Local courts | lawlessness +1 a year | frontier +10, old blood +10, magistrates -10 |
|  | The customary law (the default) | as things are |  |
|  | The Reach's assizes | lawlessness -1 a year; unrest -1 a year | magistrates +15, commons +5, frontier -10, old blood -10 |
| The Trade Charter | Protected markets | what you pay on the exchanges +10%; tolls +1 to whoever holds a lane world | merchants -15, magistrates +5, old blood +5 |
|  | The customary charter (the default) | as things are |  |
|  | Open lanes | what you pay on the exchanges -10%; ventures pay +10% | merchants +20, frontier +5, magistrates -5 |
| The Arms Market | Closed (the default) | as things are |  |
|  | A licensed market | isotopes may be bought on the exchanges | officers +10, merchants +10, magistrates -10, commons -5 |
| The Exchange Levy | No levy (the default) | as things are |  |
|  | A light levy | a levy of +5% on shares traded | merchants -10, commons +5 |
|  | A heavy levy | a levy of +15% on shares traded | merchants -25, commons +10, magistrates +5 |
| The Levy of the Reach | No common levy | manpower -1 a world | commons +10, officers -10 |
|  | The customary levy (the default) | as things are |  |
|  | The Reach's levy | manpower +1 a world; unrest +1 a year | officers +15, commons -15 |

**A house with a spymaster on its council**

| Law | Option | What it does | Who minds |
|---|---|---|---|
| Informers | A few trusted ears (the default) | as things are |  |
|  | A paid network | your informers learn +1 more; costs +200 credits a year | commons -5, magistrates -5 |
|  | Ears in every household | your informers learn +3 more; costs +500 credits a year | commons -15, magistrates -10, old blood -5 |

**A house with an envoy on its council**

| Law | Option | What it does | Who minds |
|---|---|---|---|
| The Envoy's Table | A plain call (the default) | as things are |  |
|  | An open table | your envoy wins +2 more regard; costs +200 credits a year | merchants +5 |
|  | Gifts of state | your envoy wins +4 more regard; costs +500 credits a year | old blood +10, commons -5 |

**A house with an advocate on its council**

| Law | Option | What it does | Who minds |
|---|---|---|---|
| The House's Counsel | The advocate alone (the default) | as things are |  |
|  | A bar retained | your advocate is +2 better in court; costs +200 credits a year | magistrates +5 |
|  | Every clerk in the courts | your advocate is +4 better in court; costs +500 credits a year | magistrates -10, merchants -5 |

**The head of any house**

| Law | Option | What it does | Who minds |
|---|---|---|---|
| The Household's Wages | As little as they will take | each retainer's wage -1 credits; your retainers' loyalty -3 a year | commons -5 |
|  | The customary wage (the default) | as things are |  |
|  | An open hand | each retainer's wage +1 credits; your retainers' loyalty +3 a year | commons +5, merchants -3 |

What an estate does when it is loyal or hostile to a house:

| Estate | Loyal (+30) | Hostile (-30) |
|---|---|---|
| The Commons | unrest -1 a year, the people grow faster | unrest +2 a year |
| The Merchants | ventures pay 15% more | ventures pay 25% less, the exchanges cost 25% more |
| The Officers | crews recover readiness +5 a year | readiness -5, the fleets' keep +25% |
| The Old Blood | +2 weight in the Senate, legitimacy +1 every five years | -1 weight, legitimacy -1 every five years |
| The Magistrates | +100 credits a world, prosperity +2 a year | prosperity -3 a year |
| The Frontier | lawlessness -2 a year | lawlessness +2 a year |

Other things in the game that are law in all but name, and were not part of this review: the Senate's other motions (a censure,
a levy, relief, a commission, an embargo, emergency powers), a house's rule of succession, and the dues of a province.

## 2. Does each one do anything

Seventy-seven separate effects were tried (two laws could not be: nobody is Emperor and nobody leads a bench when the Old
Lion's game opens).

| | When first measured | Now |
|---|---|---|
| Moves something within a year | 40 | 44 |
| Moves something only within six years | 5 | 6 |
| Moves nothing in six years, with a player who does nothing | 32 | 27 |

### Written, and read by no rule (mended)

| What | What was wrong | Now |
|---|---|---|
| **The Levy of the Reach** (an Act): manpower a world, up or down | The Act's manpower was read only from a house's own laws. The Senate carried "no common levy" in both games measured and it took no manpower from anybody. | Read for every house: 28 houses' manpower moved in the first year. |
| **The Trade Charter** (an Act): open lanes, ventures pay a tenth more | The same. The Senate carried it in both games and no venture paid more. | Read for every house. |
| **Legitimacy from a law**: closed doors, the closed franchise, the Old Blood's regard | Given only to the player's house. Five houses in a game keep a closed hall by their culture and none ever gained from it. | Every house. |
| **A throne's proscriptions**: legitimacy +2 every five years | Given to the player's house whoever sat the throne. | Given to the throne. |

### Too small to show (mended)

The books are kept in hundreds of credits. A tenth more on a venture that pays 400 is 40, which is no whole hundred, so it was
lost; a quarter more on a small fleet's keep the same. So the Trade Charter's tenth, the Merchants' loyalty, the household
guard and fleet doctrine did nothing at the size a house begins at. **What is under a hundred is now paid and billed in single
credits**, as servants' wages already are (docs/MONEY.md). A fleet's keep also now reads the laws in the first year's
forecast; it used to read them only after a year had turned.

### Waits on something the house does (27 effects, left as they are)

These are read, and each has a test that shows it working, but a house that does nothing never sees them.

| Law | What it waits on |
|---|---|
| Tariffs: what you pay on the exchanges, charters | The house has to buy or sell on the exchanges, or hold a world where another house's venture pays a charter. |
| The household guard and fleet doctrine: readiness | Only crews that have been worked down recover any faster. A fleet lying at full readiness gains nothing. |
| Standing orders: what a motion costs | Somebody other than the Speaker's house has to move a motion. |
| The arms market: isotopes on the exchanges | Somebody has to buy isotopes. |
| The exchange levy | Shares have to be traded. |
| Informers, the embassy, counsel (12 effects) | The post's holder has to be given the work. |
| Household wages (4 effects) | The house has to have retainers. |

### Does nothing of itself

One option carries no effect at all and only moves what the estates think: an open hall. It gives the Commons +10 and the Old
Blood -10, and neither reaches thirty unless other laws push the same way. It is not broken; it is small. A bench's two
platforms, reform and tradition, do the same thing as each other (+1 weight in the Senate) and differ only in who minds.

## 3. Does anybody use them

Two games of thirty years, fifty-eight houses, a player who does nothing.

| | When first measured | Now |
|---|---|---|
| Times a house other than the player's changed one of its own laws | 12 | 106 |
| Acts carried by the Senate | 6 | 6 |
| All motions the Senate decided, of every kind | 40 | 40 |

- **Other houses only ever legislated when something was wrong**: a hostile estate, a world in revolt, an empty treasury, a
  war. In a quiet game that is almost never. The tithe, the hall, the guard, tariffs, labour, justice, the franchise and
  household wages were not changed once by anybody in sixty years of play.
- **Mended:** in a quiet year a house now brings one of its own laws to what its bench stands for, unless the change would turn
  one of its own estates against it or cost coin it has not got; the leader of a bench gives it a platform. After thirty years,
  across the two games, 18 houses keep a lenient tithe where 4 did, 24 an open hall where 6 did, 16 closed doors where 10 did,
  and 23 lenient courts where 10 did: the houses of the Reach now differ by the bench they sit on.
- **The Senate barely sits.** Forty motions decided in sixty years of play is two every three years, and twenty of those were
  petitions for a rank. Six Acts in all. This is taken up under "laws to vote on" below.
- **Positions that carry a law and are almost never held:** the Admiral and the Treasurer (the Admiral was commissioned twice
  and was vacant again by the end; the Treasurer never), the three council posts (no other house fills them), a bench's leader
  (six, of whom two now set a platform), the Emperor (never, in a Republic). So fleet doctrine, the exchange rates, the three
  council laws and the imperial edict are laws for the player alone, and only if the player wins the position.
- **A law for a world is a law for all of them.** "The holder of a world" sets tariffs, labour, justice and the franchise for
  every world it holds at once. Nothing can be decreed for one world.

## 4. What was changed, in one place

| Change | Where |
|---|---|
| The Levy of the Reach's manpower and the Trade Charter's ventures are read for the whole Reach | `Economy` |
| Legitimacy from laws for every house; a Reach-wide law's legitimacy to whoever holds the position | `PolicySystem.Tick`, `ReachLegitimacy` |
| A percentage of a small sum is paid in single credits | `Ledger.Coin`, `Economy.VentureLine`, `FleetSystem.Upkeep` |
| A fleet's keep reads the laws from the first year | `FleetSystem.Bind` |
| Other houses follow their bench's platform; a bench's leader sets one | `NpcAi.Legislate` |
| A test that refuses a Reach-wide law naming an effect no rule reads for the whole Reach | `tests/LawsAuditTests.cs`, `ReachRead` |
| The two measures | `src/Tools/LawsProbe.cs` |

### What I chose

- A house does not follow its bench into a law that would leave one of its own estates within 5 of hostile, nor into one that
  costs coin a world while its treasury holds under 1,000 credits.
- A house looks at its laws in about one year in eight (as before); the platform is the last thing it looks at.

## 5. Still wrong, and for the owner

1. **The offices that carry laws are empty.** Should the Admiral and the Treasurer be filled every few years by the Senate as a
   matter of course, as the consuls are, so that their laws are part of every game?
2. **Other houses have no council**, so informers, the embassy and counsel are the player's alone.
3. **Readiness laws are weak.** They only help a fleet recover. Should the guard doubled also raise what a rested fleet can
   reach, or hold readiness under way?
4. **An open hall** does very little, and a bench's two platforms do the same thing. Give each an effect of its own, or leave
   them as gestures?

## 6. More laws

Three kinds, as the owner named them. Each is being built with the same two rules the review taught: an effect must be read
where the law puts it, with a test that shows a number moving; and other houses must use it at a rate a player notices.

LAWS_MORE_PLACEHOLDER
