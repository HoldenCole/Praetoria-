# Personal wealth, house wealth and the dynasty's assets

Built 2026-11-14. From the roadmap's person-and-family group. It follows the rule in docs/PROCESS.md ("One person, not a
house"): what a relative does with their money is theirs to decide. The panel is on the House screen.

Before this a house had one treasury, a person owned nothing, and an heir received everything.

## Purses

Every grown member of your house has a **purse** of their own, apart from the house's treasury. So do you.

| What fills it, each year | How much |
|---|---|
| The allowance (not for the head) | 0, 1 or 3 credits, from the treasury |
| The pay of their service | Half their rank, rounded up |
| An estate they hold | 2 |

**Your own purse.** As head you can move credits between it and the treasury. What is in your purse is yours: it goes by
your will and not with the house, and when the house's creditors come they cannot touch it. A head who takes from the
treasury while keeping the family on a mean allowance is seen doing it, and thought the worse of.

## The allowance

One setting for the whole house.

| Allowance | Each grown member a year | How they take it |
|---|---|---|
| Mean | 0 | They think worse of you every year, the wilful twice as fast |
| Customary | 1 | Nobody remarks on it |
| Generous | 3 | They think better of you every year |

If the treasury cannot pay it, it goes unpaid and they mind.

## What they do with it

Their own business. Each year, by temperament:

- **They put it into the house.** When the treasury is in the red, someone who thinks well of you and has something put
  by gives up to half of it, unasked. The steady (honourable, loyal) always do.
- **They spend it on being seen**, if proud, arrogant or set on outshining rivals.
- **They lose it at the tables**, now and then, if they have little discipline and no steadiness.
- **They put it by**, if greedy or set on a fortune.
- **They live within their means** otherwise, and keep no more than 20.

**Debt.** A purse can go below nothing, and a debt grows by a tenth a year. At 8 owed you are told. You can pay it from
the treasury (they are grateful) or from your own purse (more so), or leave them to it. Past 20 owed the creditors come
to the house itself: they are paid from the treasury as far as it goes, the house loses 2 legitimacy, and it is talked
of.

## Heirlooms

A thing worth leaving to someone: a signet, a sword, a library, an estate. **It belongs to a person, not to the house.**

- A house of an old name, or of the third rank and above, begins with one, held by its head. A new name has none.
- **Give it** to anyone, in the house or out of it. They do not forget it.
- **Sell it** for what it is worth; the money is yours. Selling what the family has always had costs the house 3
  legitimacy, and your blood kin resent it.
- **Leave it** to someone in your will.
- An estate pays its holder 2 a year.

## The will, and the estate

When a head dies three things happen, and they need not agree:

| What | Goes by | Who gets it |
|---|---|---|
| The house: its name, worlds, fleets | The succession law | The heir |
| The dead head's purse and heirlooms | Their **will** | Whoever it names; otherwise the heir |
| The treasury | The **custom of the estate** | Kept whole for the heir, or shared out |

- **The will** can leave a quarter, a half or all of your purse to one named person, and each heirloom to anyone. If
  half or more goes past the heir, the heir resents whoever got it, and there is an event.
- **The estate kept whole:** the heir takes the whole treasury. The other grown children already resent being passed
  over; one the will remembered minds it less.
- **The estate in portions:** each other grown child of the head takes a tenth of the treasury into their own purse, and
  thinks better of the new head. The house is poorer for it.
- **Debts are inherited.** What a dead head owed is paid from the treasury.

When anyone else of the house dies, what they had comes to the head.

## Events

Five, in `content/events/wealth.json`: a debt in the family; out of their own pocket; a mean house; the will is read;
sold out of the family.

## For content

- Flags each year: `kin_in_debt`, `allowance_resented`. Deeds: `kin_gave_to_the_house`, `will_read`, `will_contested`,
  `heirloom_sold`, `heirloom_given`, `debt_scandal`.
- Character flags: `in_debt`, `gave_to_the_house`, `remembered_in_the_will`.
- `adjustResource` with `credits` still moves the house's treasury, not a purse.

## How it is built

- `Character.Purse`, `LastSpend`, `Will`; `House.Allowance`, `Inheritance`; `World.Heirlooms`. `WealthSystem` is static and
  uses no dice.
- `WealthSystem.Inherit` runs when a head dies, after the house has passed.

## Checked

Eight tests; one caught a fault in the first draft (a debt scandal could not happen in a saga's first five years). The
soak passes on all eleven starts. In the browser on *The Old Lion*: money moved to the head's purse, a generous
allowance set, half the purse willed to a younger relative and the estate set to portions; the old head died within
four years, the house passed to the heir, and the relative named in the will held 15 credits afterwards (the legacy,
any portion and their allowance together; the run did not separate them).

## A sum to a relative, and whose money a dowry is

- **Give.** From your own purse to a grown relative's, once a year each. It is theirs. They think the better of you
  for it (2, and one more for every 2 credits, to 10).
- **Lend.** From your own purse; one loan to a person at a time. They pay it back by thirds as they have the money
  (the honourable and the loyal always, others about three years in five). **Left six years unpaid it sits between
  you**: each thinks one the less of the other every year, and there is an event. You can **forgive** it at any time
  (+8). What the dead owed is written off, and one who comes to have the house owes its head nothing.
- **Dowries.** A custom of the house, with the allowance and the estate: from the treasury, as is usual, or **from my
  own purse first**. Then you pay what your purse holds and the treasury the rest, and a bride whose portion came
  mostly from your own hand thinks 6 the better of you. A purse counts toward whether the house can afford the terms.

## The entail, what an heirloom does, and loans the other way

Closed straight off the register on 2026-11-15 (docs/GAPS.md W3, W5, W6).

**The entail.** The head of the house a thing has always belonged to can entail it. It then goes with the house, to
whoever has the house, whatever a will says; and it cannot be sold, given or left away. The old families approve
(+2). Breaking an entail is done in a court: the house loses 3 standing, the old families think the worse of it, each
of the blood thinks 6 worse of the head, and the family is told why (an event). An old name's own heirloom is
entailed from the start. If an entailed thing is ever found in the wrong hands it goes back to the head.

**What each kind does.**

| Kind | For whoever holds it |
|---|---|
| Signet | On the hand of the head of its own house, the house's standing mends a point a year while it is under 50. Off that hand, nothing. |
| Sword | Carried by someone in the fleet, 2 learning a year toward discipline. On a wall, nothing. |
| Library | Everyone of its holder's house who has set themselves a study learns it 1 faster a year. |
| Estate | 2 credits a year to its holder (as before). |

The panel says what each does where it is.

**Loans the other way, and between relatives.**

- The head can **borrow** of a relative, out of that person's purse. They must keep 2 back, and they lend only if they
  are loyal or think 20 or better of the head. One who counts such things (greedy, or set on a fortune) wants a fifth
  on top. The head pays back as far as their purse goes. Left six years, the lender thinks 2 worse of the head each
  year and an event asks what is to be done. If the head dies owing it, it is paid out of their purse, then out of the
  treasury, before anything is shared.
- The head can **lend at interest**: the same loan with a fifth on top. It is taken as business (−1, where a plain
  loan is +3).
- **One of the house in debt** is helped by another who has 10 or more to spare and thinks 30 or better of them: half
  the lender's purse at most. The debtor thinks 6 better of them, pays it back by thirds, and the head is told.
- **A bride** puts half her own purse toward her portion, up to half of what is due, before the head's purse or the
  treasury is touched.

Found while building it, and mapped: an heirloom is one of four kinds and no house ever comes by a new one (W8).

### Purses that hold something (W7, the same day)

Seen in play: the Borrow button was nearly always dark. Raising what a careful person keeps (twenty credits to thirty),
offering five as well as ten, and letting a relative with six to spare help another, did not change that: driven
again in the browser for twelve years, nobody had seven credits. The cause was income. A person's service paid them
half their rank a year, and an allowance one more; the proud spent it and the rest saved a credit or two a year.

**A person's service now pays them their rank and one more a year.** Driven again on the Old Lion start: in year
twelve a relative lent the head five, and is owed it. Purses are still small (two to four credits beside that loan),
which is as it should be for people living on an allowance; they are no longer empty.

### New things (W8, 2026-11-15)

A house had what it was born with and nothing more. Now a thing can be come by in three ways, and each is a kind of
its own.

| Kind | How it is come by | What it does |
|---|---|---|
| **Shrine** | Made to order, out of a person's own purse: 24 credits. One to a house. | The commons think a point better of its holder's house each year, while they think less than 40 of it. |
| **Library** | Made to order the same way: 36 credits. One to a house. | What a library always did: everyone of the house who has set a study learns it a little faster. |
| **Standard** | Taken in a battle. About one victory in four over another house brings one home, and it belongs to whoever commanded, not to the house. A house keeps two at most. Raiders fly nothing worth taking. | Flown from the flagship of whoever holds it, that fleet is a point more loyal each year, up to 90. In a hall it does nothing. |
| **Plate** | Sent by the other house at a wedding between two houses, about one wedding in twelve: to the bride if a dowry came with her, to the groom if none did. It is that person's own. | Its holder and the head of the house that sent it think a point better of each other each year, up to 40. |

- A thing made to order is worth two thirds of what it cost, so having one made and selling it loses money.
- **Selling plate** is an answer to whoever sent it: the head of that house thinks 12 worse of the seller.
- **Other houses** have things made too: a house with 80 credits or more does so about once in eighty years, out of its
  treasury, since its head has no purse the game keeps.
- **Nothing is sent in your name.** When one of yours marries into another house, no plate goes unless you give
  something yourself. The panel now offers the heads of the three houses of best standing as people to give a thing to.
- Two events: one of yours brings a standard home (let them keep it, have it carried through the forum, or say
  little); plate arrives for a wedding (write, send something back, or take it as due).

Measured over sixty runs of sixty years: 209 things were made to order, 99 standards were taken and 401 gifts of
plate were sent; at the end 575 of 1,936 houses (30%) held something they were not born with. Two earlier settings
were tried and dropped: at the first, three houses in four ended with something new, which made none of it worth
noticing.

Found while building it, and mapped (W9): a standard lost is never fought for or asked back; a relative never has
anything made with their own money; and a gift between houses comes only at a wedding. All three were closed the
same day: see the next section.

### A standard asked back, a relative's commission, a gift to mend a quarrel (W9, 2026-11-15)

**A standard is remembered by the house that lost it.**

- The head of that house thinks a point worse of whoever flies it each year, down to −40.
- **Retaken.** If that house beats the house that flies it in a battle, the standard comes home at once, to whoever
  commanded. This is certain, not a chance: it is what the day was for.
- **Bought home.** At peace, a house can buy its standard back for three times its worth (36 credits). You do it
  from your own purse, from the heirlooms list, unless the other head thinks −20 or worse of you. Other houses do it
  among themselves in about one year in eight, when they can pay.
- **Asked for.** A house whose standard one of yours flies asks for it, in about one year in four and not more than
  once in five years. An event: send it home and ask nothing (they think 25 better of the holder), name a price (they
  pay it into the holder's purse), or keep it (they think 10 worse of you). You can also send one home unasked, with
  the button on the standard itself.
- A standard that has come home is that house's own again and nobody resents it.

**A relative has a thing made.** One of your house with a shrine's price and 6 credits beside has one made in their
own name, in about one year in four: a library instead if they are learned and can pay for it. One of a kind to a
house still holds. An event for the head: it does the house credit, add the house's money to it, or it should have
been the house's to give.

**A gift to mend a quarrel.** A head who thinks −45 to −15 of another, is not at war with them and has 60 credits
sends plate, in about one year in thirty: to whichever such head they think worst of. Each thinks 8 better of the
other, and the plate does what plate does. Sent to you, an event: take it as it is meant, answer in kind, or send it
back (they think 14 worse of you, and it is gone).

Measured over sixty runs of sixty years: 86 standards were taken; 23 were retaken in battle and 26 bought home; 35
were still flown at the end by the house that took them. Relatives of the player's house had 71 things made. 269
gifts were sent to mend a quarrel, between four and five a run. Houses holding something they were not born with:
36% at the end (30% before this). The first setting for gifts, one year in twelve, put that at 41% and was cut.

Found while building it, and mapped (W11): a gift is always plate and is never sent to ask a favour; nothing but a
standard is ever taken in a war.

### A purse that holds more than credits (W1, 2026-11-15)

A purse held credits and nothing else. Now a person can hold **shares in their own name**.

- **Bought out of the purse** on the Exchange, at the same price a house pays, and sold back into it. One person may
  hold a tenth of a company and no more.
- **What they pay goes into the purse**, each year, not into the treasury. The house's ledger never shows them.
- **They are not the house's.** They give the house no say in the company; when a company is wound up a person is
  paid as a house is; and what takes the house's holdings does not take them.
- **They go by the will**, as the purse does: the share the will leaves to someone, of each holding, and the rest to
  the heir. When one of the house other than its head dies, theirs go to the head, in the head's own name. Shares in
  the name of someone with nobody to come after them go back to the float.
- **Relatives do it for themselves.** One who is greedy, wants a fortune, or knows economics (3 or better), and has 12
  credits or more, buys up to five shares a year of the company that pays best for its price, keeping 6 back. One in
  debt sells what covers it. The purses list says what each did and what they hold. An event tells the head the first
  a relative buys: it is their money, or tell them what the house knows, or the house comes first.
- **A founder's stake.** Whoever of the house founded a venture draws its level in credits a year out of the
  treasury, into their purse. This was true only of those who wanted a fortune; it is now true of every founder.

Measured over sixty runs of sixty years: relatives bought 3,042 shares in their own names (about fifty a run) and
sold 73; the shares paid 7,162 credits into purses (about two a year to a house's people); 1,741 shares passed on
at a death; at the end 124 people held some, worth 5,253 credits together. Founders drew 441 credits in stakes.

Found while building it, and mapped (W10): a venture is still the house's, and the head can sell it over its
founder's head; no world is a person's; shares in a person's name carry no vote, even alongside their house's; and
only the people of your house hold any, since nobody else has a purse (X1).

## Not in this piece

- **Only your house has purses.** Everyone else in the Reach still has only their house's treasury.
- ~~A purse holds credits only.~~ Closed in part (W1): a person holds shares in their own name, and a founder draws a stake from
  what they founded. No venture and no world is a person's outright; see "A purse that holds more than credits" (W10).
- ~~Dowries still come from the treasury.~~ Closed: from the head's purse if they say so, and from the bride's own first.
- An heirloom can be entailed. A world or a venture cannot: any of them can still be sold by the head.
- ~~An heirloom is one of four kinds, and none is ever made, won or given by another house.~~ Closed (W8): see "New things". What is left is W9.
- Two of the five events (the will is read, sold out of the family) were not seen to fire in the soak.
- The numbers are first guesses.
