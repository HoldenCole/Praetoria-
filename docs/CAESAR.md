# The Caesar road

Built 2026-11-14. Stages 4 and 5 of docs/REPUBLIC.md. Before it, a general who won battles got legitimacy for his
house and nothing else: the Senate could not honour him or call him home, the crews were the house's whoever led them,
and nobody could march on the capital. Now a commander makes a name, the Senate answers it, and the answer can end the
Republic.

The rule is docs/PROCESS.md: you play one person. The general may be you or one of your house. If it is one of your
house, the victories, the crews and the triumph are theirs. What the *house* does about a recall is the head's to say.

## The crews and their commander

Every fleet has a **devotion** (0 to 100) to the person who commands it, beside its loyalty to the house.

- It rises with **victories** under that commander: 6 a battle, 12 if led from the front. A battle lost costs 6.
- A point or two a year for the years together. A **donative** (2 credits a hull from the house, once a year) adds 8.
- At **50** the crews follow the commander before the house.
- **A new commander starts again.** If the crews were the old one's, they resent the change (loyalty −10), and if their
  commander dies they are sullen (−8).
- Someone who has risen in the service (third rank or higher) and takes a command in a saga's first three years is
  credited with the service they had before it: two victories a rank above the second, and crews who know them.

## Sway

What a commander could move the Reach with, 0 to 100. Shown on the Military screen with its parts.

| Part | Points |
|---|---|
| Battles won in command, in a whole life | 6 each, to 30 |
| Of them, won from the front (valour) | 3 each, to 15 |
| The name the Reach gives (Achilles) | 10 |
| Hulls whose crews follow | 2 each, to 30 |
| Triumphs | 12 each, to 30 |
| Rank in the military service | 2 a rank |

A house's **general** is whoever of its own people commands one of its fleets and has the most sway.

## The triumph

A motion before the Senate (`triumph`, 2 influence), for the general of a house. Anyone of standing may move it, for
their own house or another's.

- It asks **three victories** since the last triumph. A victory beyond the Reach counts; bringing a nation to terms
  counts two more.
- **Carried:** the house gains 6 legitimacy and glory, the general's count starts again, the fleets he commands gain
  devotion and loyalty, and it is written into his life.
- **Refused:** nothing is given, the crews take it personally (devotion +4), and it is a **cause**.
- Houses lean for a friend, a party fellow, a general the squares love; against from enmity, from envy (the other
  great names in arms), and the Optimates against one who already has much sway.

## The recall

A motion (`recall`, 3 influence) that the general of a house lay down the command and come home. It can only be moved
against one with sway of **40** or more.

- Houses lean for it from enmity, from fear of a large name (45 and over), and the capital's holder and the consuls
  most of all; against it for a friend, a party fellow, a patron, and when the general's house has twice their fleet.
- **Carried:** the general has two years.
  - **Another house's general** answers within the year: he crosses the line if he would and can, and otherwise
    comes home.
  - **You**, if you are the general: come home, or cross.
  - **One of your house:** a loyal one keeps the law and comes home the next year by themselves. A wilful one waits to
    hear what the head will do. The head may order them home (a wilful one resents it) or cross the line with them.
  - **Left unanswered** to the deadline, the Senate's officers take the command and the house loses 6 legitimacy. A war
    never starts by neglect.
- **Coming home:** the fleets are left without a commander, the general may hold no command for five years, and the
  house gains 4 legitimacy for keeping the law.

## Crossing the line

The head of a house may march its general on the capital when all of these hold:

- it is the Republic, and the Reach is not already at war with itself (a march, or the great war);
- the house does not itself hold the capital;
- **four hulls** or more would follow the general before the house;
- the general's sway is **40** or more;
- the Senate has given a **cause** in the last five years: a recall, a triumph refused, or a censure.

Then:

- **The houses declare.** Each weighs its regard for the general's house, party, patronage, what it depends on that
  house for, and fear. The house that holds the capital is always for the Senate. The rest are for the general, for
  the Senate, or stand aside. **The player's house is not declared for:** you choose, or say nothing.
- **A few fight.** The three strongest for the general and the four strongest for the Senate, with the capital's
  holder, go to war. The Senate gives its war to a consul's house if one stands with it, else to the strongest: **the
  Senate's sword**.
- Crews that follow the general stay with him; some of the rest of his house's crews will not fire on the Republic
  (loyalty −15).

## How it ends

Each year the war's standing is the lead in its battles, plus one for the stronger side in any year nobody met.

**The general wins** if his house's fleet holds the capital's orbit two years running with none of the Senate's there,
or the standing reaches +8 after two years.

- **The Republic ends.** His house takes the throne (by force, so its legitimacy is not raised).
- Those who declared for him gain legitimacy and his regard; those against lose both; the Senate's sword is
  proscribed and forfeits three tenths of its treasury.

**The general loses** if his house has no fleet left, or a quarter of what it crossed with after a year, or the
standing reaches −8, or ten years pass, or he dies.

- **He does not outlive it.** If that is you, the seat passes to your heir.
- His house loses 25 legitimacy and four tenths of its treasury, its offices, and may not stand for consul for ten
  years. Those who declared for him lose legitimacy.
- The Senate's sword gains 10 legitimacy and its general a triumph.

A march is cut short without a victor if a throne is taken some other way or the great war breaks out.

## Where it is

- **Military screen**, "The general and the Senate": the general's sway and its parts, victories toward a triumph,
  whose the crews are (with the donative), the recall and its answer, the march and why not, a civil war's sides and
  standing with the buttons to declare, and the names the Senate watches.
- **Senate screen:** Triumph and Recall are motions like any other.
- **Outliner:** a recall to answer, a march to declare on, a triumph that could be asked.

## Events

Twelve, in `content/events/caesar.json`: *The Triumph*, *A Triumph In The House*, *No Triumph*, *The Recall*, *They
Have Recalled One Of Yours*, *The Line Is Crossed*, *A General Marches On The Capital*, *The Republic Stands*, *The
Republic Falls*, *The Capital Is Yours*, *The March Has Failed*, *The Crews Are Theirs*. They colour what happened.
What is decided (to come home, to cross, to declare) is decided on the Military screen.

## For content

- World flags, each year: `recalled`, `kin_recalled`, `civil_war`, `civil_war_mine`, `sword_of_the_senate`,
  `general_has_sway`, `laurels_earned`, `kin_general_loved`.
- World flags, for a year after: `triumph_voted`, `kin_triumph`, `triumph_refused`, `recall_obeyed`,
  `crossed_the_line`, `caesar_marches`, `caesar_crowned`, `republic_fell`, `republic_saved`, `caesar_broken`,
  `served_caesar`, `opposed_caesar`. For good: `republic_fallen`.
- Character flags: `triumphator`, `new_triumphator`, `recalled_general`, `caesar`, `loved_by_the_fleet`,
  `first_emperor`.
- Counters: `laurels:<id>`, `victories:<id>`, `triumphs:<id>`; `triumphs_voted`, `recalls_voted`, `civil_wars`,
  `republics_ended`, `caesars_broken`.
- No new effect types.

## How it is built

- `src/Core/Systems/CaesarSystem.cs`; state in `src/Core/State/Caesar.cs` (`Recall`, `CivilWar`), `Fleet.DevotedTo`
  and `Fleet.Devotion`, `World.Recalls`, `World.CivilWar`.
- `CaesarCommand` (obey, cross, join, donative) in `src/Core/Commands/HouseholdCommands.cs`.
- Battles call `CaesarSystem.Victory`; expeditions call `ExpeditionVictory` and `Subdued`.
- `SenateSystem` leans on, applies and (for other houses) moves the two motions. The generals' business is moved
  whoever else has business that year.
- `ThroneSystem.Enthrone` no longer assumes the one taking the throne is the player.
- `src/Web/Components/CaesarPanel.razor`. `tests/CaesarTests.cs` (10). `tools/browser/caesar.js`.
- `praetoria caesar --scenario all --seeds 5`: how far generals get in sixty years with a passive player.

## Checked

- 592 tests. One walks all three roads (your own, a relative's, another house's) and resolves every choice of all
  twelve events. One runs sixty years on two seeds and asserts there is never more than one civil war and none under a
  throne.
- The soak passes (eleven starts, three seeds, sixty years, save and load compared), with the random player answering
  recalls, crossing, declaring and paying donatives when it can. The survival probe is unchanged at 1 of 80.
- In the browser on The Old Lion the panel reads the general's sway and its parts, a donative moved the crews' regard,
  and there were no errors.

## What the probe found

- **For other houses the road is rare.** In 30 passive sixty-year runs: 7 triumphs, 2 recalls carried of 11 moved, and
  no march. The Reach sees about eight battles a run, its fleets are small (a general commands four hulls of a house's
  nine), and the game's great war ends the Republic around year 28 to 40 in two runs of three. None of the twelve
  events fired in the soak. The road is there for the player to walk; a rival Caesar will seldom be seen until the
  Reach fights more or the thresholds are tuned against the probe.
- The house that holds the capital has most of the veteran generals. It cannot march on its own capital, and other
  houses no longer spend recalls on it.
- The older routes to a throne are unchanged: the great war's victor, and the Seize and Proclaim commands.

## Not in this piece

- A general does not defect with his crews to another house, and crews do not follow a dead commander's heir.
- The parties do not act as parties in the civil war; each house declares for itself.
- No proscription lists, no amnesty, no negotiated end.
- The Senate's sword gets no command of the state's fleets beyond what its offices already give.
- A relative who is the general cannot cross against the head's wishes.
- Under a throne there is nothing here yet: no triumph by the throne's gift, no guard. That is politics under a throne.
- Every number is a first guess.
