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
- **Everyone else trains a skill every year with no ceiling.** Characters other than the player's already improve by
  themselves, one step a year, without limit: a steward of skill 3 was at 7 four years later. It makes "competent
  members of the house" too easy to come by. Not changed here; it belongs to the education work.

## Not in this piece

- Only the player's house has a household. Other houses do not take people in, adopt, or delegate.
- Kin and household do not find mentors for themselves; only your own character has one, and you teach your protégés.
- Wards are not hostages: nothing holds a house to good behaviour through a child in your keeping.
- Keeping the household costs nothing.
- The two duties cover routine only. A steward does not buy and sell on the Exchange, set the tithe, or found ventures;
  a marshal does not move fleets or choose tactics.
- Three of the six events were not seen to fire in the soak (they need a protégé, a dishonest steward or a lost mentor,
  which its random player rarely arrives at).
- The numbers are first guesses.
