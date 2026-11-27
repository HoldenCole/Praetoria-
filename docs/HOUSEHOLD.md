# The household: found family and delegated work

Built 2026-11-14. Two requests of the owner's in one piece: "found family... people you aren't related to but you took
them into your fold for whatever reason", and "the option to delegate things to competent members of the house if you
want". They fit together: the people you can hand work to are the members of your house, born to it or taken in. It is
the roadmap's "Under the wing". The panel is on the House screen.

It follows the rule in docs/PROCESS.md ("One person, not a house"): nothing here has to be done for the house to work.

## Found family

Someone not of your blood can be taken into the house.

| As | Who | What it means |
|---|---|---|
| A **ward** | A child of six to fifteen | Their family decides. Raised in your house; at sixteen they choose to stay or go home |
| A **protégé** | A grown person | You bring them on: every third year they learn a step of whatever you are best at |
| A **companion** | A grown person | They simply belong to the household |

- They **keep their own name**, live as members of the house, can hold its posts and duties, and court and marry for
  themselves like anyone else in it. Their children keep their name too.
- They **stand in the line only after everyone of the blood**, whatever the succession law. If nobody of the blood is
  left, the household inherits.
- A house can keep **two, and one more for each step of rank**.
- It costs 2 influence to take someone in.

**Whether they come.** A score of 50 and they come. It starts at 30 and moves with what they (or, for a child, their
family) think of you, your house's standing and wealth against theirs, whether their house is your client, a sworn bond
(an Academy friend, say), and whether they are the last of their house or an orphan. The head of a house with others in
it does not leave it. A child of a house as great as yours is unlikely to. Someone married elsewhere is reluctant.

**People come to the door.** About one year in seven, someone who would come and is worth having asks for a place, or a
family asks you to take a child. You answer; they wait two years.

**Adoption.** A head may adopt a grown person of the household who thinks well of them (regard 20) and is at least ten
years younger. They take the name and stand in the line as the head's child. It costs 3 influence, and the head's grown
children by blood resent both of you for it.

**Sending someone home.** They go back to the house of their birth, if it still stands, and think the worse of you.

**A ward at sixteen** stays if they have come to think well of you (regard 30) or have no house to return to. Otherwise
they go home, and their family remembers that you raised them.

## Under someone's wing

Your own character can ask a senior of their own service to be their mentor (2 influence). They must outrank you, be at
least eight years older, and think something of you (regard 10). Every third year you learn a step of whatever they are
best at, while you are behind them in it. It ends if your houses go to war, if they come to think ill of you, or when
they die.

## Work you can hand on

Two duties. Each can be given to any grown member of the house except yourself, and taken back at any time. You can
still do any of the work personally whenever you like.

| Duty | Does, once a year | Their skill |
|---|---|---|
| **Steward** | Begins the cheapest useful building a world lacks (working a deposit first), or failing that enlarges a venture | Administration or economics |
| **Marshal** | Buys fuel when the tank is under a year's bill; puts damaged fleets into refit | Logistics, leadership or tactics |

- **The reserve.** You say how many credits they must leave in the treasury (0, 2,000, 5,000, 10,000 or 20,000). They spend only
  what is above it.
- **Competence shows.** A steward with skill 3 does two things a year; one with none does little in some years. A
  marshal with skill 3 buys a second year's fuel while they are at it.
- **A report** each year says what they did, in a line.
- **Trust.** Holding a duty makes them think better of you, slowly.
- **Honesty.** A steward who is greedy, not honourable or loyal, and owes you nothing (regard under 10) takes three hundred
  credits a year. After three years the accounts are noticed not to add up, and there is an event.
- **It lapses** if they die, leave the house, refuse to serve you (defiant, or regard under −20), or become head.

Your vote in the Senate needed no delegate: left undeclared, it already follows the house's interest.

### What a steward is told to favour, and what they would do next (2026-11-21)

From the playthroughs (docs/PLAYTHROUGH.md, Medium, Money). The head may give the steward one instruction
(`Duty.Favour`, `StewardFavourCommand`): as they judge; credits, materials, manpower or helium-3, and a building that
yields it is begun before any other on whichever world can take one; the ventures, and a venture is enlarged before
anything is built; or begin nothing new. `HouseholdSystem.StewardPlan` says what they would begin when the year turns
and what it costs, or why nothing. The plan and the year's turn read the same list (`StewardChoices`), so what is shown
is what is done unless the treasury changes in between.

## The house's court

Seven small positions (tutor, wet nurse, physician, captain of the guard, duel master, shipwright, master of
ceremonies) held by the commons sworn to the house or by family. It is its own page: **docs/COURT.md**.

## The council

What the owner asked (2026-11-22): *"I don't think it needs to show admiral, chancellor, and steward off to the right
all the time because that's confusing. There should also be a few more roles, which will mean more content, laws, and
actions."* I took "roles" to mean seats of the house's council, like those three.

**Where it is.** The council is no longer listed beside the map. It has a page of its own: House, *The council*. Beside
the map there is one line ("The council · 2 of 6 posts filled") that opens it, and a seat speaks among the alerts only
when something **wants doing now and can be done**: the treasury nearly empty, no fuel, a motion against the house, an
embargo, a suit with nobody to plead it, reports gone stale on a house you are at war with. A standing worry (a
neighbour with a larger fleet, a rank ahead of your standing) is said on the council's page and nowhere else.

**Six posts**, each built as the steward and the marshal already were: given to anyone grown of the house, done once a
year by the holder's own skill, paid 200 credits a year, spending only what the treasury holds above the reserve, told
one thing by the head, saying in a line what it did, and learning its skill a step every four years.

| Post | Skill | What it does in a year | What you can tell it | Its law |
|---|---|---|---|---|
| Steward | administration, economics | builds, and enlarges ventures | what to favour (as before) | |
| Marshal | logistics, leadership, tactics | keeps the fleets fuelled and in repair | | |
| **Chancellor** | oratory, law, charisma | declares the house's vote on what the Senate decides this year, where you have not voted | as the house leans · as our patron votes · declare nothing | |
| **Spymaster** | intrigue | fresh reports on the fleets of the strongest houses; or one house known exactly, with its purse and what its head thinks of you; or the books of your own post-holders read | the Reach at large · one house · our own house | *Informers* |
| **Envoy** | diplomacy, charisma | calls on a head of a house, or a nation's court, with gifts, and leaves them thinking better of you | whoever thinks worst of us · our patron · one house · one nation | *The Envoy's Table* |
| **Advocate** | law, oratory | pleads every suit that names the house, at no cost in influence | plead every suit · come to terms where the law is plainly against us | *The House's Counsel* |

The chancellor was a seat before and nobody's post: now it is given like the others. With nobody named, the ablest
relative at the law still speaks for the seat, and votes nothing. The other three stand empty until somebody is named.

**What can go wrong.** An informer in another house may be found (one year in five, fewer with a better spymaster,
more against a head who knows the work): its head knows whose he was, and thinks 12 the worse of you. An envoy with
no gift for it, sent to a head who thinks ill of the house, is one year in three not received, and the gifts are kept.
A spymaster watching the house's own finds what a steward has taken years before the accounts would have shown it.

**Three laws** (content/politics/laws.json), each in force only while the house has somebody in the post, and set in
the Laws window like any other:

| Law | Options |
|---|---|
| Informers | a few trusted ears · a paid network (+1 house watched, 200 credits a year) · ears in every household (+3, 500 credits; the commons and the magistrates think the worse of it) |
| The Envoy's Table | a plain call · an open table (+2 regard a call, 200 credits more) · gifts of state (+4, 500 credits more) |
| The House's Counsel | the advocate alone · a bar retained (+2 to the advocate's craft in court, 200 credits a year) · every clerk in the courts (+4, 500 credits; the magistrates think the worse of it) |

**Seven events** (content/events/council.json), each raised by something a post did: the spymaster's report, the
informer found, a friend at another table, the envoy turned away, the advocate's day, the advocate loses, the
chancellor's voice.

**One new command**, `PostInstructionCommand`: tell a post its one thing.

**The numbers are mine**: an envoy's call is worth 2 regard plus the envoy's skill, to +60 at most, and costs 200 credits
in gifts; a nation's regard for the Reach rises 1 plus half the envoy's skill, to +40 at most; a spymaster watches 2
houses plus half their skill; the laws' figures above.

**Not in this piece.** Other houses have no council: only the player's posts act. The marshal takes no instruction. A
post-holder cannot betray the house except by taking money (the steward). Nothing is spoken for the Imperium's court
beyond the same six. docs/GAPS.md lists these.

## Events

Six, in `content/events/found_family.json`: a child with nowhere to go; the ward comes of age; the accounts do not add
up; a place at the table; the protégé is noticed; the wing withdrawn.

## For content

- Effect: `takeIn` (`role`, `as`: ward, protege or companion).
- World flags set each year: `found_family`, `has_ward`, `has_protege`, `has_mentor`, `petition_at_the_door`,
  `duties_delegated`.
- World flags raised by a deed: `took_someone_in`, `adopted_an_heir`, `ward_stayed`, `ward_went_home`,
  `steward_skims`, `mentor_lost`, `mentor_taught`.
- Character flags: `taken_in`, `ward`, `protege`, `companion`, `adopted`, `ward_of_age`, `skimming`.

## How it is built

- A person taken in changes house (`HouseId`) and keeps `BornHouseId`. `AdoptedById` and `MentorId` are on the character.
- `House.Duties` and `House.Petitions` hold the rest. `HouseholdSystem` runs for the player's house at the start of the
  year's reckoning, before the other houses act, and uses no dice.
- A delegate acts through the same commands the player uses, with their own small pools, on the house's treasury.

## Checked

Ten tests. The 60-turn soak passes on all eleven starts with a random player who takes people in, adopts, sends home,
seeks mentors and hands duties out and back. `tools/browser/household.js` hands out both duties, takes someone in and
plays some years. On *The Old Lion* the steward began a power plant in the first year, a protégé was taken in, and in
the fourth year someone else came asking for a place unprompted.

## Found on the way

- **Panels did not redraw after End Turn.** The orders, courtship and household panels take no parameters, so they kept
  showing last year's figures until something in them was clicked. They now listen for the world changing.
- **Everyone else trained a skill every year with no ceiling** (fixed since, in docs/EDUCATION.md). A steward of skill 3 was at 7 four years later,
  which made "competent members of the house" too easy to come by.

## Relatives find mentors for themselves

One of the house between sixteen and thirty-two, in a service, with nobody over them, looks for a mentor about one
year in three: someone of their own service, senior to them, eight years older and well disposed, of the house first
and of another house if there is nobody. Nobody tells them to, and the head hears of it afterwards (an event). A
relative's mentor teaches as the head's own does. With the family's autonomy turned off, nobody goes looking.

## What a household costs

Closed straight off the register on 2026-11-15 (docs/GAPS.md H3). Each person taken in costs the treasury a hundred credits a
year. Whoever does the head's work (a steward, a marshal) is paid 200 a year into their own purse. A house that cannot
pay keeps them all the same: each goes without, thinks 2 worse of the head that year, and an event asks what the head
says about it. The household and wealth panels both show the yearly figure.


### The keep by standing, and going unpaid (H5, the same day)

A grown person of standing (the fourth rank of a service or above) who was taken in costs 200 a year: they are kept as
what they are. One who goes unpaid two years running, and thinks less than 20 of the head, goes. A grown person taken
in goes home, if there is a home to go to; one doing the head's work lays it down. A child stays where they were put.
A year paid in full starts the count again.

## Not in this piece

- Only the player's house has a household. Other houses do not take people in, adopt, or delegate.
- Wards are not hostages: nothing holds a house to good behaviour through a child in your keeping.
- The two duties cover routine only. A steward does not buy and sell on the Exchange, set the tithe, or found ventures;
  a marshal does not move fleets or choose tactics.
- Three of the six events were not seen to fire in the soak (they need a protégé, a dishonest steward or a lost mentor,
  which its random player rarely arrives at).
- The numbers are first guesses.


## Who leads another house

**The owner's answer (2026-11-23):** "Table this for now. Let's assume it's the oldest, but can be challenged for good reason. (Heir more
qualified, head is old and not useful, etc). Keep this simple for now."

Before this, the head of any house but yours was whoever held the highest rank in it, so a promotion changed who you were
dealing with, and what one head thought of you was not what the next did.

What is built (`HeadSystem`, `House.HeadId`):

- **The eldest grown person of a house leads it.** A child leads only a house with no grown person left.
- **The head stays the head until death.** A promotion does not change it, and neither does someone older joining the house.
- **When a head dies the next eldest follows.** The chronicle says so.
- **A head can be made to stand aside for good reason.** There are two reasons, and no others:

| Reason | What it takes |
|---|---|
| Someone better qualified | another grown person of the house stands 2 ranks or more above the head |
| The head is old and not useful | the head is 65 or older and in failing health (below 35), or is 78 or older, and there is a grown person under 65 to follow |

- **A challenge with a reason is made in about one year in four.** When there is more than one who could, the highest in rank does, then the elder.
- **The one who stood aside lives on in the house** and does not take it back.
- **You are told** when it happens in a house you have had dealings with, or whose head had a view of you, and told what the
  new head thinks of you now. The new head takes up the old one's friends and enemies, you among them (docs/LIFE.md,
  "Friends and enemies handed down"): 20 the better of you if the old head thought well of you, 20 the worse if ill, both
  fading over ten years; a vengeful one is your enemy at once, a just or loyal one lets it go, and a child takes on no
  friendship. This replaced, on 2026-11-26, the rule that the new head started with half of what the old one thought of
  you when they had no view of their own.
- Your own house is not touched: you are its head.

Measured over thirty years of one game: 3 heads stood aside across all the houses. The rest changed only by death.

### What I chose

- The ages 65 and 78, the health of 35, the lead of 2 ranks, and one year in four. (The half of the old head's regard that
  passed on is gone: what passes now is chosen in docs/LIFE.md, "Friends and enemies handed down".)
- That the highest in rank is the one who challenges, since "more qualified" has to mean something a player can see.

### What is not built (tabled by the owner)

- You cannot back, stir or oppose a challenge in another house.
- A house's own law of succession (eldest child, youngest, merit) does not decide who follows a dead head. The eldest does.
- Those who married into a house are not told apart from those born to its name: the eldest of either leads.
- The old head does not resent the one who replaced them.

## Struck from the line, cast out, and the undutiful will

Built 2026-11-26, from docs/IDEAS_FROM_MODS.md, entry 3: *"The head can strike a grown relative out of the succession, or
cast them out of the house, each at a cost in legitimacy and the kin's regard unless the one struck had defied, deserted
or shamed the house. Either can be undone at the same price. At the head's death, a child struck out with no recorded
cause may sue before a praetor that the will was undutiful, and a child who wins takes a share."* It closes part of
docs/GAPS.md R18: *"Nobody can be disowned; a relative who was passed over or has defected cannot strike at the head."*

It is influence over kin, not control (docs/PROCESS.md, "One person, not a house"): the one struck out keeps a grievance,
and what they do about it is theirs.

### Struck from the line

The head may strike a grown relative of the house's blood out of the line of succession. They stay of the house, keep
their posts and their purse, and their own children stay in the line. They stand **after everyone else**, behind those
taken in and those who married in, and have the house only if nobody else is left. Under another house's rule (the
eldest leads, "Who leads another house" above) one struck out leads only when nobody else is left, and cannot challenge
the head.

### Cast out

The head may cast a grown relative of the blood out of the house altogether. They leave its rolls, its posts, its
courtships and its debts (what they owed the head is written off; what the head owed them is paid from the head's purse
as far as it goes), and anything of theirs that is entailed on the house stays with it. **They take their purse**, and
whatever else is their own. Where they go, in this order:

| They go to | When |
|---|---|
| Their husband's or wife's house | They are married to someone of another house (marriage does not move anyone between houses in this game) |
| Their patron's house | They have a patron of another house (docs/LADDER.md, "A patron, and a falling out") |
| A parent's people | A parent is of another house, or was born to one |
| Alone, the first of a new name | None of these: a house without land, named for them ("House Corvin"), whose treasury is their purse and whose legitimacy is 5 |

A command by service they hold (docs/MILITARY.md, "A command by service") ends that day, unless they go into the house
whose fleet it is, when they keep it by right; the screen says which before it is done. In another's house they live as
one taken in, under their own name. One cast out by another house is not sent to the
player's house unless it is to a husband or wife there, and once there they cannot be sent back to the house that cast
them out, by the head ("Send them home" is refused: "the house they were born to cast them out: they have nowhere else")
or of themselves when their keep goes unpaid. The household window says "cast out of" that house where it would say
"born to".

### What it costs, and the cause on the record

Either costs the house legitimacy (entered under "Marriages and the family" in the record of legitimacy) and the regard
of every other grown relative of the blood, **unless the record holds a cause** against the one struck:

| Cause | What the record holds |
|---|---|
| Defied the head | Grown defiant (docs/AMBITIONS.md and the family's own rule), now or in the last ten years |
| Married without the head's leave | A match the head forbade, made all the same (docs/MARRIAGE.md), in the last ten years |
| Deserted the house | Left it once for a rival (the family's own rule of defection) |
| Shamed the house | Called out over a death and did not come (docs/FEUD.md) |
| Shamed the house | Put out of office for what they did in it, convicted or removed (docs/LADDER.md, "The courts"), in the last ten years |

A failure is not a cause: an election lost, a battle lost, a venture wound up are no shame on the house. The screen says
before it is done which it is.

**Undoing either costs what doing it cost**: restoring someone to the line, or taking them back, costs the same
legitimacy and the same regard of the kin again (nothing, if there was cause), and the one restored thinks the better of
the head. One cast out comes back only if they think −60 or better of the head, and not if they now lead a house that
has other people in it. One taken back lays down what they held of the house they leave (its posts, the command or
captaincy of its fleets, the governing of its worlds), and a command by service they hold ends. **A named heir cannot
be struck out or cast out**, and nobody struck out can be named heir until they are restored (the "Name" button on the Dynasty window's ambitions tab is greyed for them, with the reason). When one
struck out would otherwise stand before the heir, the Dynasty window says the heir is the first "not struck from the line";
when the heir is struck out themselves, that nobody else is left. A report that hands the seat on in life (the
abdication) names only one who stands in the line, and never one struck out while another stands in it, nor one cast out.

### They strike back

Through the rules that already exist, not a new system. Unless they are loyal (or the family has no wills of its own):

- **One struck out wants their place back**: their ambition becomes the seat of the house (docs/AMBITIONS.md). That
  makes them wilful, so the family's own rules take over: thinking ill enough of the head they grow defiant, warm each
  year to whoever of another house thinks worst of the head, **may marry against the head's wish** (they elope when a
  match is forbidden, docs/MARRIAGE.md), and below −60 **leave for a rival house**. They ask to be named heir, and the
  head cannot grant it while they are struck out.
- **One cast out means to outshine the house that cast them out** (their ambition becomes it), and has already joined
  another house with their purse.
- What they wanted before is given back when the thing is undone, and when one struck out comes to lead the house, or
  leaves it.

### The suit of the undutiful will

When a head dies, each grown child of theirs who stands struck out or cast out of the house **with no cause on the
record**, and **whom the will and the house's custom leave nothing**, may sue that the will was undutiful (a loyal child
who thinks well of the new head does not). A child the will remembered has no suit: one left the larger share or a
smaller legacy out of a purse that holds anything, or an heirloom the head held, or (while they are still of the house)
a portion of the treasury by the custom of portions (docs/WEALTH.md). Before a head strikes or casts someone out, the
Dynasty window says whether they could sue: only a child of the head can ("They are not your child, so they cannot sue
over your will"), and one struck out under the custom of portions takes a portion all the same. Whoever struck
them out, the will of a parent who died with them still out is the one sued over. It goes on the house's docket with
its other suits (docs/LADDER.md, "The docket"), before a praetor of neither house, and is heard at the end of the year
after it is brought. The news names that year (a head who dies at the year's end is read of in the next year, and the
suit is heard at the end of it), and the news and the docket say on what ground the praetor will find: upright, bought
by the house, a friend of it, for sale, or by the pleading.

- **It asks** a quarter of the dead head's purse as it stood at the death, and one heirloom in four of those not
  entailed, the most valuable first, and at least one if there are any.
- **Until it is heard** the head may come to terms (pay what it asks now: the suit is withdrawn, the child thinks 10 the
  better of the head, and the striking or the casting out stands) or send someone of age to answer it (1 influence, as for any suit).
  Otherwise the house's advocate on the council answers it, and failing one the head.
- **The praetor**: the upright go by the record, which shows no cause, and find for the child; one bought by the house,
  or a friend of it (regard 30 or better), finds for the house; one known to be for sale finds for whichever has more,
  the child's purse or the house's treasury; the rest go by who pleads better, 55 in a hundred for the child, 6 more for
  each point of law and oratory the child has and 4 fewer for each the one answering has.
- **The child wins**: they take what the suit asked (from the head's purse, then the treasury), the heirlooms go to
  them, and the house loses 2 legitimacy. One struck out has the striking set aside and stands in the line again. One
  cast out is not brought back by it: the casting out stands, and the head may still take them back at the price it
  cost, as the docket says. **The will stands**: the child thinks 10 the worse of the head.
- The decided suit is kept on the docket a while, in words of what was done ("Quintus Bench, praetor, went by the record,
  which showed no cause, and found the will undutiful").
- No praetor of another house sitting: the suit is not brought, or lapses. So none is brought under a throne.
- **Exile** (docs/LADDER.md, "Exile"): an exile brings no suit. A child abroad when the head dies has none, and one who
  goes abroad before it is heard has it lapse, and is paid nothing. Nobody abroad answers one for the house either: one
  sent who then goes abroad is not heard, and if the head is abroad, the best pleader of the house at home answers it.

### Other houses

A head of another house acts on a **plain, fresh cause** against one of their own blood: put out of office (convicted or
removed), called out over a death and did not come, or come over from another house by defection. They act within two
years of the cause or let it lie, one head in two by a hash of the house, the person and the year it arose, and once a
year in a house. A proud, honourable or just head casts out one put out of office or who refused the field; any other
head strikes them from the line, and one who came over by defection is struck from it. With cause, it costs them no
legitimacy. Those struck out strike back the same way (their ambition), and the player is told when it is a house they
have had to do with.

### The numbers

| | |
|---|---|
| Strike from the line, no cause | −3 legitimacy, each of the kin −5, the one struck −25 |
| Cast out, no cause | −5 legitimacy, each of the kin −10, the one cast out −40 |
| Either, with cause | No legitimacy, the kin do not mind; the one struck −15, the one cast out −25 |
| Restore, or take back | What it cost, again; the one restored +15, the one taken back +20 |
| One cast out comes back | At −60 or better toward the head, and not to leave a house they lead that has others in it |
| How long a cause counts | Ten years for defiance, a marriage without leave, and a removal from office; for ever for desertion and the field refused |
| A new name | Legitimacy 5, its treasury the founder's purse, no land |
| The undutiful will asks | A quarter of the purse; one heirloom in four not entailed, at least one |
| Won | −2 legitimacy, the child +10, set aside |
| Lost | The child −10 |
| Terms | What it asks, paid now; the child +10 |
| Answering it | 1 influence |
| Before a praetor who goes by the pleading | 55 in a hundred for the child, +6 a point of theirs, −4 a point of the one answering (5 to 95) |
| A friend of the house | The praetor's head thinks 30 or better of the house's |
| Other houses | Within 2 years of the cause, one head in two, once a year in a house |

### Measured

`dotnet run --project src/Tools -- disown --scenario the_old_lion --seeds 2`, two games of thirty years with nobody
steering the player's house (`DISOWN_PLAYER=1` plays each again with the head striking their eldest grown child out in
the first year, and otherwise doing nothing):

| | Seed 1 | Seed 2 |
|---|---|---|
| Struck from a line by other heads | 1 | 2 |
| Cast out by other heads | 0 | 1 (went to a patron's house) |
| The cause | put out of office, every time | put out of office, every time |
| People put out of office in the game | 10 | 9 |
| A head changed and passed over someone struck out | once (House Ashkar, year 21) | never |
| New names founded | 0 | 0 |

Four acts in sixty years of play, all for a removal from office, against 70 changes of head across all the houses. In the
steered runs the child struck out (Ambitious and Proud) grew defiant in seed 2 (in year 2) and not in seed 1, neither
married against the head nor left the house, and sued at the head's death: in seed 1 the seat passed twice in year 6 and
a praetor found both wills undutiful in year 7 (Aurel's, 1,100 credits, and Helene's, 1,500 credits); in seed 2 the
one suit was won in year 6 (800 credits), and the child came to lead the house in year 10. (Measured again on the
merged build, after the review that followed the merge; before it, seed 1 struck out 2 and passed one over in year 26,
and its steered child lost one suit in year 10 and won another in year 15.)

### What I chose

Every number here is mine, and a first guess:

- The prices: 3 and 5 legitimacy; 5 and 10 from each of the kin; 25 and 40 from the one struck, 15 and 25 with cause;
  15 and 20 back when it is undone; −60 for coming back.
- That undoing costs what doing cost, read as the legitimacy and the kin's regard paid again, since the kin think a
  head who changes their mind about the line unsteady.
- The causes, from what the record holds: defiance, a marriage without leave, a defection, the field refused, a
  removal from office; that a failure is not one; that the first two and the last count for ten years, and the
  field refused and a defection for ever.
- That one struck out stands last in the line rather than out of it, so that a house is not ended by a striking, and
  that striking the last who could follow is refused.
- Where one cast out goes, and in that order; that in another's house they live as one taken in; that alone they found
  a house named for their given name, with legitimacy 5.
- That they take their purse and their own things, not what is entailed, and that the debts between them and the head
  are settled as at a death.
- The grievance as an ambition: the seat of the house for one struck out, to outshine all rivals for one cast out; and
  that the loyal, and everyone when the family has no wills of its own, take it quietly.
- The suit: a quarter of the purse (the old portion a child could not be denied) and one heirloom in four; that it is
  brought only where the child was put out with no cause and the will and custom leave them nothing, so that anything
  left to them, however small, answers it; that winning sets a striking aside but not a casting out; that a loyal child fond of the new head (30) does not sue;
  that the will sued over is that of any parent who died with them still out; the praetor's rules, mirrored on the
  charges' (55, 6 and 4; the friend at 30); −2 legitimacy and the 10s of regard.
- That the defence is whoever was sent, else the council's advocate, else the head; nobody in exile, and with the head
  abroad, the house's best pleader at home. That an exile's suit lapses rather than waits for them to come home.
- Other houses: the three plain causes, two years, one in two, once a year, and who casts out rather than strikes.
- That a command by service ends the day its commander is cast out or taken back, and is kept, by right, only by one cast
  out into the house whose fleet it is; and that one taken back lays down what they held of the house they leave.
- That the grievance is answered when one struck out comes to lead the house or leaves it, as when they are restored.
- That a report handing the seat on in life names only one who stands in the line.
- That a deed flag is raised for content the year it happens: `kin_struck_from_the_line`, `kin_cast_out`,
  `undutiful_will_brought`, `will_found_undutiful`, `will_upheld`.

### What is not built

- No events: what happens is told in the reports and the docket, and the choices are on the Dynasty window and the
  docket. The deed flags above are there for content to read.
- A struck relative who asks to be named heir cannot be granted it while struck out; granting does not restore them.
- Striking someone out does not strike out their children.
- Those taken in and those who married in cannot be struck out or cast out (they can be sent home, as before).
- Other houses never strike out or cast out without cause, so the suit of the undutiful will is brought only in the
  player's house; other houses do not take anyone back or restore anyone.
- Defection and a marriage without leave are recorded only for the player's kin (the family's own rule runs only for
  the player's house), and desertion in war is not a person's record anywhere in the game (only hulls desert), so other
  heads see only removals from office, the field refused, and those who came over to them by defection.
- A child who wins does not take back what was entailed, and a suit cannot be appealed.
- A child the will left less than a quarter cannot sue to have it made up to the quarter: anything left answers the suit.
- One cast out who wins the suit is not taken back by it, nor made cheaper to take back.
- Random-play QA (`src/Tools/Qa.cs`) is not wired to the new commands; that file is the owner's to change.

### How it is built

- `src/Core/Systems/DisownSystem.cs`: the causes (`Cause`, `PlainCause`), the prices (`PriceOf`), the refusals and the
  acts (`Strike`, `Restore`, `CastOutOf`, `TakeBack`, `WhereTo`), the suit (`SeatPassed`, called once from
  `DynastySystem.HandOver`; `Verdict`, `Settle`, `SendPleader`), and the year (`Tick`, after the family's own).
- State: `Disowning` and `WillSuit` (`src/Core/State/Disowning.cs`), `World.Disownings`, `World.WillSuits`; character flags
  `struck_from_the_line` and `cast_out`; counters `defied_the_head:<id>`, `wed_without_leave:<id>`,
  `refused_field_seen:<id>` and `defected_seen:<id>` for causes that would otherwise pass with the year, let go when the
  person dies.
- Reads it: `DynastySystem.Line` (struck out last), `HeadSystem.Eldest` and `Challenger`, `AmbitionSystem.NameHeirRefusal`
  (also for a request to be named heir, through `AnswerRefusal`), `HouseholdSystem.HomeOf` (one cast out has no home in the
  house that cast them out), the docket (`Docket.Will`, `DisownSystem.WinSays` and `TermsSays`), and `DisownSystem.Remembered`
  (whether the will or the custom left a child anything).
- Commands (`src/Core/Commands/DisownCommands.cs`): `DisownCommand(actorId, what, personId)` with `Strike`, `Restore`,
  `CastOut`, `TakeBack`; `WillSuitCommand(actorId, what, suitId, pleaderId = "")` with `Settle`, `Plead`.
- The screen: the Dynasty window, on a relative's card, "Strike from the line" or "Restore to the line"
  (`data-strike`) and "Cast out" or "Take back" (`data-cast-out`), greyed with the refusal, the price and the cause in
  the title, and a question before it is done; the line of succession marks those struck out and lists them and those
  cast out, who can be picked to take them back. The suit is on the Senate window's docket (`data-undutiful-will`), with
  terms and a pleader to send, and stays there a little while once decided.
- `tests/DisinheritTests.cs` (30), and the probe `src/Tools/DisownProbe.cs`.
