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

- **The reserve.** You say how many credits they must leave in the treasury (0, 20, 50, 100 or 200). They spend only
  what is above it.
- **Competence shows.** A steward with skill 3 does two things a year; one with none does little in some years. A
  marshal with skill 3 buys a second year's fuel while they are at it.
- **A report** each year says what they did, in a line.
- **Trust.** Holding a duty makes them think better of you, slowly.
- **Honesty.** A steward who is greedy, not honourable or loyal, and owes you nothing (regard under 10) takes three
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
year by the holder's own skill, paid 2 credits a year, spending only what the treasury holds above the reserve, told
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
| Informers | a few trusted ears · a paid network (+1 house watched, 2 credits a year) · ears in every household (+3, 5 credits; the commons and the magistrates think the worse of it) |
| The Envoy's Table | a plain call · an open table (+2 regard a call, 2 credits more) · gifts of state (+4, 5 credits more) |
| The House's Counsel | the advocate alone · a bar retained (+2 to the advocate's craft in court, 2 credits a year) · every clerk in the courts (+4, 5 credits; the magistrates think the worse of it) |

**Seven events** (content/events/council.json), each raised by something a post did: the spymaster's report, the
informer found, a friend at another table, the envoy turned away, the advocate's day, the advocate loses, the
chancellor's voice.

**One new command**, `PostInstructionCommand`: tell a post its one thing.

**The numbers are mine**: an envoy's call is worth 2 regard plus the envoy's skill, to +60 at most, and costs 2 credits
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

Closed straight off the register on 2026-11-15 (docs/GAPS.md H3). Each person taken in costs the treasury a credit a
year. Whoever does the head's work (a steward, a marshal) is paid 2 a year into their own purse. A house that cannot
pay keeps them all the same: each goes without, thinks 2 worse of the head that year, and an event asks what the head
says about it. The household and wealth panels both show the yearly figure.


### The keep by standing, and going unpaid (H5, the same day)

A grown person of standing (the fourth rank of a service or above) who was taken in costs 2 a year: they are kept as
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
