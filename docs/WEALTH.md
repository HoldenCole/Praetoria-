# Personal wealth, house wealth and the dynasty's assets

Built 2026-11-14. From the roadmap's person-and-family group. It follows the rule in docs/PROCESS.md ("One person, not a
house"): what a relative does with their money is theirs to decide. The panel is on the House screen.

Before this a house had one treasury, a person owned nothing, and an heir received everything.

## Purses

Every grown member of your house has a **purse** of their own, apart from the house's treasury. So do you.

| What fills it, each year | How much |
|---|---|
| The allowance (not for the head) | 0, 100 or 300 credits, from the treasury |
| The pay of their service | Half their rank, rounded up, in hundreds of credits |
| An estate they hold | 200 |

**Your own purse.** As head you can move credits between it and the treasury. What is in your purse is yours: it goes by
your will and not with the house, and when the house's creditors come they cannot touch it. A head who takes from the
treasury while keeping the family on a mean allowance is seen doing it, and thought the worse of.

## The allowance

One setting for the whole house.

| Allowance | Each grown member a year | How they take it |
|---|---|---|
| Mean | 0 | They think worse of you every year, the wilful twice as fast |
| Customary | 100 | Nobody remarks on it |
| Generous | 300 | They think better of you every year |

If the treasury cannot pay it, it goes unpaid and they mind.

## What they do with it

Their own business. Each year, by temperament:

- **They put it into the house.** When the treasury is in the red, someone who thinks well of you and has something put
  by gives up to half of it, unasked. The steady (honourable, loyal) always do.
- **They spend it on being seen**, if proud, arrogant or set on outshining rivals.
- **They lose it at the tables**, now and then, if they have little discipline and no steadiness.
- **They put it by**, if greedy or set on a fortune.
- **They live within their means** otherwise, and keep no more than 2,000.

**Debt.** A purse can go below nothing, and a debt grows by a tenth a year. At 800 owed you are told. You can pay it from
the treasury (they are grateful) or from your own purse (more so), or leave them to it. Past 2,000 owed the creditors come
to the house itself: they are paid from the treasury as far as it goes, the house loses 2 legitimacy, and it is talked
of.

## Heirlooms

A thing worth leaving to someone: a signet, a sword, a library, an estate. **It belongs to a person, not to the house.**

- A house of an old name, or of the third rank and above, begins with one, held by its head. A new name has none.
- **Give it** to anyone, in the house or out of it. They do not forget it.
- **Sell it** for what it is worth; the money is yours. Selling what the family has always had costs the house 3
  legitimacy, and your blood kin resent it.
- **Leave it** to someone in your will.
- An estate pays its holder 200 a year.

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
four years, the house passed to the heir, and the relative named in the will held 1,500 credits afterwards (the legacy,
any portion and their allowance together; the run did not separate them).

## A sum to a relative, and whose money a dowry is

- **Give.** From your own purse to a grown relative's, once a year each. It is theirs. They think the better of you
  for it (2, and one more for every 200 credits, to 10).
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
| Estate | 200 credits a year to its holder (as before). |

The panel says what each does where it is.

**Loans the other way, and between relatives.**

- The head can **borrow** of a relative, out of that person's purse. They must keep 200 back, and they lend only if they
  are loyal or think 20 or better of the head. One who counts such things (greedy, or set on a fortune) wants a fifth
  on top. The head pays back as far as their purse goes. Left six years, the lender thinks 2 worse of the head each
  year and an event asks what is to be done. If the head dies owing it, it is paid out of their purse, then out of the
  treasury, before anything is shared.
- The head can **lend at interest**: the same loan with a fifth on top. It is taken as business (−1, where a plain
  loan is +3).
- **One of the house in debt** is helped by another who has 1,000 or more to spare and thinks 30 or better of them: half
  the lender's purse at most. The debtor thinks 6 better of them, pays it back by thirds, and the head is told.
- **A bride** puts half her own purse toward her portion, up to half of what is due, before the head's purse or the
  treasury is touched.

Found while building it, and mapped: an heirloom is one of four kinds and no house ever comes by a new one (W8).

### Purses that hold something (W7, the same day)

Seen in play: the Borrow button was nearly always dark. Raising what a careful person keeps (2,000 credits to 3,000),
offering five hundred as well as a thousand, and letting a relative with six hundred to spare help another, did not change that: driven
again in the browser for twelve years, nobody had seven hundred credits. The cause was income. A person's service paid them
half their rank a year in hundreds of credits, and an allowance a hundred more; the proud spent it and the rest saved a hundred credits or two a year.

**A person's service now pays them their rank and one more a year, in hundreds of credits.** Driven again on the Old Lion start: in year
twelve a relative lent the head five hundred, and is owed it. Purses are still small (two to four hundred credits beside that loan),
which is as it should be for people living on an allowance; they are no longer empty.

### New things (W8, 2026-11-15)

A house had what it was born with and nothing more. Now a thing can be come by in three ways, and each is a kind of
its own.

| Kind | How it is come by | What it does |
|---|---|---|
| **Shrine** | Made to order, out of a person's own purse: 2,400 credits. One to a house. | The commons think a point better of its holder's house each year, while they think less than 40 of it. |
| **Library** | Made to order the same way: 3,600 credits. One to a house. | What a library always did: everyone of the house who has set a study learns it a little faster. |
| **Standard** | Taken in a battle. About one victory in four over another house brings one home, and it belongs to whoever commanded, not to the house. A house keeps two at most. Raiders fly nothing worth taking. | Flown from the flagship of whoever holds it, that fleet is a point more loyal each year, up to 90. In a hall it does nothing. |
| **Plate** | Sent by the other house at a wedding between two houses, about one wedding in twelve: to the bride if a dowry came with her, to the groom if none did. It is that person's own. | Its holder and the head of the house that sent it think a point better of each other each year, up to 40. |

- A thing made to order is worth two thirds of what it cost, so having one made and selling it loses money.
- **Selling plate** is an answer to whoever sent it: the head of that house thinks 12 worse of the seller.
- **Other houses** have things made too: a house with 8,000 credits or more does so about once in eighty years, out of its
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
- **Bought home.** At peace, a house can buy its standard back for three times its worth (3,600 credits). You do it
  from your own purse, from the heirlooms list, unless the other head thinks −20 or worse of you. Other houses do it
  among themselves in about one year in eight, when they can pay.
- **Asked for.** A house whose standard one of yours flies asks for it, in about one year in four and not more than
  once in five years. An event: send it home and ask nothing (they think 25 better of the holder), name a price (they
  pay it into the holder's purse), or keep it (they think 10 worse of you). You can also send one home unasked, with
  the button on the standard itself.
- A standard that has come home is that house's own again and nobody resents it.

**A relative has a thing made.** One of your house with a shrine's price and 600 credits beside has one made in their
own name, in about one year in four: a library instead if they are learned and can pay for it. One of a kind to a
house still holds. An event for the head: it does the house credit, add the house's money to it, or it should have
been the house's to give.

**A gift to mend a quarrel.** A head who thinks −45 to −15 of another, is not at war with them and has 6,000 credits
sends plate, in about one year in thirty: to whichever such head they think worst of. Each thinks 8 better of the
other, and the plate does what plate does. Sent to you, an event: take it as it is meant, answer in kind, or send it
back (they think 14 worse of you, and it is gone).

Measured over sixty runs of sixty years: 86 standards were taken; 23 were retaken in battle and 26 bought home; 35
were still flown at the end by the house that took them. Relatives of the player's house had 71 things made. 269
gifts were sent to mend a quarrel, between four and five a run. Houses holding something they were not born with:
36% at the end (30% before this). The first setting for gifts, one year in twelve, put that at 41% and was cut.

Found while building it, and mapped (W11): a gift is always plate and is never sent to ask a favour; nothing but a
standard is ever taken in a war. Both were closed the same day: see the next section.

### Plunder, and a gift that asks (W11, 2026-11-15)

**Things are carried off when a world is stormed.**

- Whoever governed the world for the beaten house can lose a signet, a sword, a library or a standard: the most
  valuable of them, in about one storming in two. If that was the house's last world, its head's things are at risk
  too. Land and a shrine stay where they are; plate and an orrery are not touched.
- The commander who took the world has it. An entail does not bring it back. The house that lost it loses a point of
  standing.
- **Everything said of a taken standard is now true of any taken thing**: the house it was taken from resents whoever
  holds it, has it back at once by winning a battle against them or storming a world of theirs, buys it home at
  peace for three times its worth, and asks the player for it. A thing that is not a standard goes back to the head
  of its house, under its own name. A signet in the wrong hands does nothing: it is a trophy.
- **A house that is broken** and folded into another gives up its signet to that house's head.
- An event when something of yours is carried off.

**A gift can ask a favour, and it is not plate.**

- **An orrery** is a new kind of thing. Its holder learns whatever they have set themselves to study a point faster
  each year.
- **Sent to ask.** A head with a motion before the Senate and 6,000 credits sends one, in about one such year in ten,
  to the head of the weightiest house that is neither friend nor enemy (−10 to 19). Kept, it is **a favour owed**.
- **A favour owed is a vote leaned.** A house that owes the proposer a favour leans two further for the motion, and
  the favour is spent when it is voted.
- **You can ask one.** Any thing you give to the head of another house puts that house in your debt for one favour,
  spent on the next motion you bring. The panel lists who owes you and whom you owe.
- **You are judged by your vote.** If you keep an orrery and vote for the motion, the sender thinks 5 better of you;
  vote any other way and they think 15 worse. The event offers three answers: keep it and remember, keep it and say
  it buys nothing (they think 5 worse, and nothing is owed), or send it back.

Measured over sixty runs of sixty years: 47 things were carried off when a world fell, and 68 taken things
(standards among them) were still held at the end by the house that took them. 40 gifts asked a favour, and all 40
favours were spent on a vote. Houses holding something they were not born with: 36%, as before.

Not seen in play: no broken house surrendered a signet in the probe or the soak, so that rests on a test. In the
browser the shrine was given to another head and the panel read "Owe you a favour: House Aurelian".

Found while building it, and mapped (W13): a favour is one thing, a lean on one vote; and other houses never refuse
or return a gift. Both were closed the next day: see the next section.

### A favour called in or forgotten, and a gift refused (W13, 2026-11-16)

**A favour owed to you can be asked for something other than a vote.** From the purses panel, beside the house
that owes it:

- **Money:** 1,500 credits, paid into your treasury, if they have it.
- **A match:** the family's consent to a suit between your house and theirs rises by 20.
- Left alone, it is spent on the next motion you bring, as before.

**A favour is forgotten after eight years** if nobody calls it in: what is owed to you, and what you owe.

**A gift can be refused.**

- A head sent plate to mend a quarrel sends it back if they think −35 or worse of the sender. Nothing changes hands,
  and the sender thinks 6 worse of them.
- A head sent an orrery to ask a favour refuses it at −15 or worse: they will not be put under an obligation to that
  house.
- **The head of another house will take nothing from you** if they think −40 or worse of you, or are at war with
  you. The buttons say so. A thing that was taken from their house is always taken back.

Measured over sixty runs of sixty years: 44 gifts were refused and 256 taken, about one in seven; 27 favours were
owed and all 27 were spent on a vote. Houses holding something they were not born with: 34%.

Not seen in play: no favour was called in for money or a match, in the probe (which gives no orders) or the soak,
and only one lapsed, since a favour asked by a house with a motion open is spent within the year. Both rest on
tests. In the browser, after a gift to House Aurelian, the panel offered both ways to call the favour in and
refused the money one because that house had none.

Found while building it, and mapped (W14): only the player calls a favour in for anything but a vote; a favour is
never passed on or bargained with.

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
- **Relatives do it for themselves.** One who is greedy, wants a fortune, or knows economics (3 or better), and has 1,200
  credits or more, buys up to five shares a year of the company that pays best for its price, keeping 600 back. One in
  debt sells what covers it. The purses list says what each did and what they hold. An event tells the head the first
  a relative buys: it is their money, or tell them what the house knows, or the house comes first.
- **A founder's stake.** Whoever of the house founded a venture draws its level in hundreds of credits a year out of the
  treasury, into their purse. This was true only of those who wanted a fortune; it is now true of every founder.

Measured over sixty runs of sixty years: relatives bought 3,042 shares in their own names (about fifty a run) and
sold 73; the shares paid 716,200 credits into purses (about two hundred a year to a house's people); 1,741 shares passed on
at a death; at the end 124 people held some, worth 525,300 credits together. Founders drew 44,100 credits in stakes.

Found while building it, and mapped (W10): a venture is still the house's, and the head can sell it over its
founder's head; no world is a person's; shares in a person's name carry no vote, even alongside their house's; and
only the people of your house hold any, since nobody else has a purse (X1).

### A venture of one's own, and a voice for one's shares (W10, 2026-11-15)

**A venture can be a person's own**, not the house's.

- **Settled.** The head can settle any venture the house owns outright on a grown member of the house, or on
  themselves. It cannot be taken back. The person thinks 12 better of the head.
- **Founded with their own money.** A relative who founds a venture out of their own purse owns it from the day it
  stands. One founded with a stake the head granted is the house's, with a founder's stake as before.
- **What it earns is theirs.** Each year its earnings are paid over from the treasury to their purse, as far as the
  treasury goes. This replaces the founder's stake for that venture.
- **It is not sold over their head.** Selling it, listing it as a company, or letting the steward sell it needs the
  holder to think 20 or better of the head. If it is sold, the price is theirs.
- **It goes as they leave it.** The head's own goes to whoever the will gives half or more of the purse, otherwise
  to the heir. When anyone else dies, or leaves the house, theirs is the house's again. Wound up, what is left is
  theirs.

**A founder has a part of the price.** When the head sells a venture one of the house founded and does not own, a
fifth of the price goes to the founder's purse, and they think 10 worse of the head for it: 4 if it was already
losing money.

**Shares in a person's name vote with their house.** A house votes its own shares in a company and those its people
hold in their own names, so long as the house holds at least one share itself. The head's own always count. Anyone
else's count while they think no ill of the head (0 or better). One who does think ill keeps the shares and the
dividend and withholds the vote; the purses list says so.

Measured over sixty runs of sixty years, with no player giving orders: 29 ventures were a relative's own at the end,
all founded with their own money, and 92,800 credits were paid over to their holders. No founder's stake was paid: in
those runs every venture a relative founded was their own.

Not seen in play: the soak's player never settled a venture and never sold one a relative had founded, so both rest
on tests. Settling was done by hand in the browser (`tools/browser/settle.js`): the card then reads "Livia Crane's
own" and the purses list "Has 1 venture of their own".

Found while building it, and mapped (W12): no world is a person's, which is the owner's to decide; and a person who
owns a venture never sells it or enlarges it themselves.

## Dealings between houses

What the owner asked (docs/PLAYTHROUGH.md, "Hard"): "Dealings between houses: gifts, favours asked and called in, a
pledged vote, a threat, a loan at interest, with regard and debts that last. Today a house's page offers a client, a
patron, a marriage, an accident and a war (P16, P106, F13, N44, P81)."

A house's page (Houses, then a house) now has a block, **Dealings**, under its ties. Everything in it is done by the
head, in the house's name, with a house of the Reach. What comes of it is kept where such things were already kept:
regard is what that house's head thinks of you, a favour is the same favour a gift of an heirloom leaves, and a house
that is afraid is the same "cowed" the breakaway used. Nothing new was invented beside them except a record of loans,
pledges and what has passed.

**Regard is a person's, so the house hears by half.** Each time something here moves what a head thinks of you, every
other grown member of that house moves by half as much. The head of another house is whoever of it ranks highest in
their service, and that changes with a promotion as well as a death; without this, what was earned with one head
would be gone when a cousin outranked them.

### What you can do

- **Send a gift of credits**: 200, 500, 1,500 or 4,000, from the treasury, once a year to each house. Their head
  thinks better of you by a point, and one more for every five parts in the hundred the gift is of what their house
  takes in a year. So 500 credits to a house that takes in 500 a year is worth the most a gift can be, and the same
  500 to a house that takes in 3,000 is worth four points. A gift of a tenth of their year's income or more, and not
  less than 1,500 credits, leaves them owing you a favour. A head who thinks -40 or worse of you, or is at war with
  you, takes nothing: the button is grey and says so, and nothing leaves the treasury.
- **Ask a favour**, of any house whose head thinks +20 or better of you (+10 for your patron or the leader of your
  bench, as before): for **coin** (a sixth of its treasury, 1,500 credits at the most, if it keeps 1,000 by), or for
  **its vote** on a motion now before the Senate that you moved or have declared on. It declares as you vote. You
  then owe it a favour, and may not ask it again for two years.
- **Call in a favour** it owes you: for 1,500 credits or for consent to a match (these were in the purses panel and
  are now here as well), or for **its vote** on a motion you moved or have declared on.
- **Pledge your vote** on a motion now before the Senate to a house that wants it carried or thrown out: the house
  that moved it, or one that leans four or more either way (a house the motion names, the holder of an office it
  would give away). It takes your word only while the vote is worth having: its side must not stand more than three
  times your seats ahead without you. Your vote is declared its way. If that is still how you vote when the motion
  is counted, it owes you a favour and its head thinks 3 better of you. If you changed it, its head thinks 15 worse
  of you, the broken word is written down, and that house takes no pledge of yours for eight years.
- **Threaten** any house you are not at war with, when you have three hulls to its one. Its head thinks 20 worse of
  you. For ten years, while you keep twice its hulls, the house is afraid of you (see below). If it is another
  house's client, the patron's head thinks 15 worse of you and you are told.
- **Lend at interest**: 500, 1,000, 2,000 or 4,000 credits at 10, 20 or 30 in the hundred. A house takes it if it is
  short of coin, its head does not think -40 or worse of you, the rate is one it bears, it owes no other house, and
  what it would owe is no more than two years of what it takes in. The whole sum, interest and all, is owed from the
  day, in five yearly instalments.

### A favour you owe

A house you owe a favour calls it in, and you are told when it does:

- **On its next motion.** When it moves one you are told that this is where the favour is called in. Vote for the
  motion and the account is even (its head thinks 5 better of you); vote otherwise and its head thinks 15 worse.
  Either way you are told at the count, and it is written down.
- **For coin**, if it holds under 500 credits, has no motion before the Senate, and the favour is at least a year
  old. It asks 1,500 credits. On its page you may pay (the same +5) or refuse (the same -15). Left unanswered until
  the year ends, it is taken as refused.

### What a house that is afraid does

A house threatened inside the last ten years by one that still has twice its hulls:

- does not vote against a motion that house moves, and does not vote for a censure, embargo, break-up, recall or
  reconquest aimed at it. It stays away instead. The roll call gives the reason: "it is afraid of" that house.
- goes with that house if its region leaves the Reach, and does not leave its state afterwards (as before). The
  player's house is never taken out of the Reach this way.

The player's house votes as it pleases, afraid or not. If it votes against a motion moved by a house that had
threatened it, that head thinks 10 worse of it.

### A loan, year by year

- The instalment is paid out of the borrower's treasury into the lender's when the year's books are done. It is a
  line in both ledgers ("instalment from House Wren"), and the line is there only if the borrower has the money as
  things stand.
- While it owes you, the house leans your way in the chamber ("in its debt", worth 2).
- What is still owed counts in the lender's wealth and against the borrower's.
- **If it cannot pay**, you are told, with what is still owed. On its page you may then (and not before):
  - **forgive the rest**: its head thinks 10 better of you and the house owes you a favour; or
  - **press for it**: its head thinks 8 worse of you, and from then on it pays whatever it has each year. Three years
    running short of the instalment and it is a **default**: the rest is written off, the chronicle says so, the
    house's standing falls by 4, and for five years every lender charges it more (it is "known for bad business").
  - Do neither, and it pays again when it can.
- Nothing is paid between houses at war. A loan is done with when it is paid (its head thinks 2 better of you).

### What lasts, on the page

One list under the buttons: a favour owed either way and the year it lapses; a favour called in and waiting; what
you lent, when, at what rate, what is left and how many years short; what either house owes the other's bank; whether
it is afraid of you, since when and until when, or has threatened you; a vote of yours pledged to it; the gifts you
sent in the last ten years; plate or an orrery it sent you; your word to it kept and broken; favours it called in,
honoured and refused; a loan forgiven, defaulted on or paid back. Records are kept twenty years.

### The numbers

| Thing | Number |
|---|---|
| Sizes of a gift | 200, 500, 1,500, 4,000 credits |
| Regard for a gift | 1, and 1 more for every 5 parts in the hundred of their year's income; 20 at the most |
| A gift leaves a favour | at a tenth of their year's income, and not under 1,500 credits |
| A gift is refused | regard -40 or worse, or at war (the rule a gift of an heirloom already had) |
| Gifts to one house | one a year |
| A favour is heard | regard +20; +10 from a patron or bench leader |
| Coin for a favour | a sixth of their treasury, 1,500 at most, 200 at least, and they keep 1,000 by |
| Asking again | not for 2 years (the rule asking a patron already had) |
| A house wants your pledge | it moved the motion, or leans 4 or more |
| It needs your pledge | while its side is no more than 3 times your seats ahead without you |
| Pledge kept, broken | regard +3 and a favour; regard -15 |
| No pledge taken after a broken one | 8 years |
| Favour honoured, refused | regard +5, -15 (as before); 1,500 credits when called for coin |
| A creditor asks coin instead of a vote | when it holds under 500 credits |
| A threat needs | 3 hulls to their 1 |
| A threat costs | regard -20; the patron's regard -15 |
| Fear lasts | 10 years, while you keep 2 hulls to their 1 |
| Voting against a house that threatened you | regard -10 |
| Sizes of a loan | 500, 1,000, 2,000, 4,000 credits |
| Rates | 10, 20, 30 in the hundred |
| A house borrows | when it holds under 1,000 credits |
| The rate it bears | 10 under 1,000 credits in hand; 20 under 500; 30 in the red |
| The most it will owe | two years of what it takes in |
| Instalments | 5, yearly |
| Regard for the terms | +4 at 10 in the hundred, +1 at 20, -1 at 30 |
| Forgiving the rest | regard +10 and a favour |
| Pressing | regard -8 |
| Default | 3 years running short once pressed; standing -4; charged more by every lender for 5 years; the lender thinks 10 worse of it |
| A loan paid off | regard +2 |
| What has passed is kept | 20 years; gifts are listed for 10 |
| What the rest of a house thinks | moves by half of what its head's does |

### The other houses

- **They lend to each other.** A house holding under 300 credits that owes no other house is lent 500 credits or
  more at 10 in the hundred by the richest house holding 3,000 or more that is its patron, or whose head thinks +10
  or better of its own, or that sits on its bench and thinks no ill of it, if two years of what the borrower takes
  in would repay it. It is paid through the ledger like any other. If it goes unpaid, a patron or a head who thinks +30 or better forgives it and is owed a favour; anyone else
  presses, and three years later it is a default. One that was let off or that defaulted is not lent to again for
  five years.
- **They threaten.** A head who is ruthless, cruel, arrogant or vengeful, with three hulls to one over a house it
  thinks -20 or worse of, says so one year in four. If it is your house you are told.
- **They call in what you owe them**, by a vote or for coin, as above.
- **They ask favours with gifts** as they already did (plate to mend a quarrel, an orrery with a motion open).

Measured over twelve games of thirty years with nobody giving orders (the old lion and the new man, six seeds each):
210 loans between other houses, of which 165 were paid off, 6 forgiven, 7 defaulted on and 32 still running at the
end; and 60 threats, some of them made to the player's house.

Seen in the browser, in one game as the old lion driven by a script: the block on House Wren, House Ashgrove and
House Marrow; a gift of 500 credits taken (the alert said so); a threat made, and the line that Ashgrove is afraid
until year 11; coin asked of a friendly head and given; a vote pledged on a commission before the Senate, and the
line that says so. No error on the page.

Not seen in play: a loan made and paid, a missed payment, a default, a favour called in for coin, a pledge counted.
The measured games give no orders and the script did not reach them, so those rest on the tests.

### What I chose

Every number in the table above is mine except those marked "as before" or "already had". The choices behind them:

- **A gift leaves a favour only at 1,500 credits or more.** The owner said a tenth of their income. Houses take in
  100 to 3,000 credits a year, so a tenth is at most 300; and a favour can be called in for 1,500. Without the floor
  a favour would be bought for 200 credits and sold back for 1,500.
- **Gift sizes and loan sizes** are set against what houses hold (400 to 4,500 credits at the start).
- **One gift a year to a house**, so that regard is not bought in an afternoon.
- **The rest of the house hears by half.** Found in the browser: over five years the head of House Wren was Rhea,
  then Aldous, then Rhea again, as their ranks moved, and the regard a loan had earned went with whoever held it.
- **A vote asked for is a declared vote**, not a lean: the house's vote is cast as yours is, and its bench follows
  it as it follows any vote of its leader's.
- **A pledge pays at the count, not at the pledge**, so that a word given and broken earns nothing.
- **A pledge is taken only on a close vote and by a house with a stake.** A favour can be called in for 1,500
  credits; if every house on the winning side of every motion took a pledge, favours would be had for nothing.
- **A loan is forgiven only after a missed payment**, as the owner wrote it. Forgiving on the day it was lent would
  be a second way to send a gift, past the limit of one a year, and with a favour for 500 credits.
- **Any patron is displeased by a threat to its client**, whether or not it is the stronger: a patron weaker than
  you can still think the worse of you.
- **A house that is afraid stays away; it does not vote your way.** Fear buys silence, a favour buys a vote.
- **The player's house is never made to vote**, by a threat or by anything else. It pays in regard instead.
- **A loan has one lender**: a house that owes one house is not lent to by another.
- **The lender chooses the rate and the borrower's need decides whether it is borne**, so a rate of 30 is for a
  house in the red and is resented.
- **Unpressed, a loan that is not being paid simply waits.** Default is the lender's choice to force.
- **The instalment is in the ledger only when it will be paid**, so the forecast and the year agree.

### How it is built

- `src/Core/Systems/DealingsSystem.cs`: the rules, and the year (`Tick`, after the wealth of the house).
- `src/Core/Commands/DealingsCommands.cs`: `DealingsCommand`, one class for the eleven acts.
- `src/Core/State/Dealings.cs`: `HouseLoan`, `VotePledge`, `DealingRecord`; three lists on `World`.
- Reused: `WealthSystem.FavourKey` and `OweFor` (favours), `WealthSystem.WontTake` (the refusal rule, split out of
  `TakesRefusal`), `PolitySystem.CowedKey` and `Cowed` (fear), `AskAbove.AskedKey` (asking again), `Lending.Owed`.
- Hooks: `SenateSystem` (the lean of a house that is afraid; pledges at the count; the notice when a creditor
  moves), `WealthSystem.FavoursCalled` (told and written down), `Economy` (the instalment lines and the year's
  payment), `OrderSystem.Sway` ("in its debt"), `Wealth.Of` (what is owed).
- `src/Web/Components/HousesScreen.razor`: the block. `tests/DealingsTests.cs`: 23 tests.

### What is not built

- **Other houses do not send gifts of credits, pledge votes to each other, or ask each other for favours** beyond the
  gifts they already sent. They do not pledge a vote to the player or ask the player for one.
- **Other houses do not lend to the player's house.** It borrows of the counting-houses and of their banks, as
  before (House, the ledger). The page shows what is owed to a bank either way.
- **No suing over a bad loan.** The docket of suits is another piece of work.
- **A threat asks for nothing in particular.** There is no "do this or else"; it only makes a house afraid.
- **A favour cannot be passed on or traded**, and a loan cannot be sold.
- **No scene.** Everything is a button and a notice; no event was written for a favour called in or a default.
- A nation beyond the Reach, and the raiders, are not dealt with this way.
- The numbers are first guesses.

## Things worth a rich house's money

Built 2026-11-23, from the owner's "Hard" list (docs/PLAYTHROUGH.md): *"Things worth a rich house's money: endow a
child, build a seat, fund a relative's campaign, lend to houses. One treasury reached 303,400 at 20,000 a year with
nothing to spend it on (F99)."* Of places to live the owner said *"We can have places to live eventually"*, and that the
real world is the yardstick: a home costs four to six years of the income of the person who lives in it
(docs/ECONOMY_REVIEW.md, sections 7 and 10).

Three of the four are here. **Lending to other houses is not**: it is built as its own piece.

All of it is on the House window, under Purses and the will, in one block, "What the house's money can do": each act
with its price now and what it would give, and what has been done. The canvass has its buttons beside "Speak for them"
on the Senate window as well, and the seat is marked on the card of the world it stands on, under The estate.

### Land settled on one of the house

The head buys an estate out of the treasury and settles it on one person of the house, child or grown.

- **It pays its holder 200 credits a year for life**, into their own purse. A child has no purse to spend from: what
  their land brings is kept for them, and is there the year they come of age.
- **It is theirs.** It is the same kind of thing as an estate handed down ("Heirlooms", above), so all that is said
  there is true of it: it can be given, left, entailed by a head who holds it, and it goes with its holder if they
  marry out of the house. When its holder dies it comes to the head, as everything of theirs does; a head's own goes
  by the will.
- **They think 12 the better of the head** for it, and whoever they are married to 6.
- **It counts in a match.** Each estate a son or daughter holds adds 4 to what the other family thinks of a match with
  them, to 8 (docs/MARRIAGE.md: the same reckoning the dowry is weighed in).
- **Each further one on the same person is dearer**, and three is the most.
- Bought and sold again it loses money: it is worth two thirds of what it cost, as a thing made to order is.

It is land, and returns what land is meant to: 200 a year on 3,000 is 6.7%, against the 7% a market or a port is
priced to. Because it returns credits, its price follows the game's setting for how easy money is (docs/MONEY.md): 4,300
at Lean, 2,000 at Generous, 1,200 at Lavish.

### The seat of the house

A residence on a world of the house's own, in three grades, each built once out of the treasury. The first is built on
a world the head names; the others where the first stands. **It returns nothing in credits: it is for standing.**

| Grade | To build | Keep a year | What it adds |
|---|---|---|---|
| A manor | 6,000 | 100 | Room for one more in the household who is not of the blood |
| A great house | 15,000 | 300 | Room for a second, and the house's standing mends a point a year while it is under 50 |
| A palace | 40,000 | 600 | Room for a third, the standing mends while it is under 60, and each year the head of the greatest house that is not yet a friend is received: each thinks 3 the better of the other, up to 40 |

- **Each time it is built** the old blood think 3 the better of the house, and every grown person of the house 4 the
  better of its head.
- **The keep** is in the year's outgoings with the household's. If the treasury cannot pay it the house is shut up for
  the year: it stays the house's and gives nothing until the keep is paid.
- **It is lost with the world it stands on**, and the house loses 2 standing for each grade.
- It is built at once: there are no years of building.

### A house at the capital

For the head or one grown person of the house, bought once: out of the treasury, or out of the head's own purse.

- **It costs five years of what that person takes in a year** (the pay of their service, the allowance, what their land
  brings, the stipend of a post of the council), and never less than 1,000 credits. Someone of the second rank in a
  service on a customary allowance takes in 400 a year, and their house costs 2,000. Land settled on them makes it
  dearer: the rich live dearer.
- **It is theirs**, and is given, left and sold as any thing of theirs is. It is worth two thirds of what it cost.
- **While they hold it they are at the capital**, for the two things the game has that happen there: their canvass is
  worth 3 more votes at any count they stand in, and when they stand you are told how that count stands before it is
  held (who else has a name down, and what each would have today).
- **They think 10 the better of the head** for it, 14 if the money was the head's own.
- **They buy their own.** One of the house who is standing for an office or holds one, and has the price and 300 beside
  in their purse, buys a house there without being asked.

### Coin behind a canvass

When the head or one of the house stands for an office of the ladder (docs/LADDER.md), the head may pay for their
canvass out of the treasury. It is in addition to speaking for them.

| Canvass | In all | Votes at the count | Votes for each 1,000 credits |
|---|---|---|---|
| Modest | 500 | 3 | 6 |
| Handsome | 1,500 | 6 | 4 |
| Lavish | 4,000 | 10 | 2.5 |

- **A larger one can be paid for later**, for the difference. The lavish is the most coin can buy.
- **A lavish canvass is written on the record as votes bought**, against the candidate. Any house may bring it before
  a praetor at once, without waiting for a term to end; other houses do so as they bring any charge, when they think
  ill enough of the house (−30). Convicted, the candidate loses the office if they won it and may not stand for five
  years, the house pays the fine and loses standing, and its candidates carry the scandal at the count for ten years
  (docs/LADDER.md, "The courts"). It stays on the record eight years.
- **Whoever it was for remembers it**: 4 the better of the head for each size paid for, and 4 more if they are chosen.
  One who stood with the house's coin behind them and lost does not blame the head for saying nothing.
- **What was paid for is kept** on the block for twenty years: the year, the sum, for whom, for what, and how it went.

### Other houses do the same

- **Rich and idle.** A house at war with no other house and owing nothing spends on one of the three in about one such
  year in three: the next grade of its seat (on its most peopled world), an estate for the child of its head who has
  least, or a house at the capital for its head. Only what it could pay for three times over. It comes out of the
  treasury, since nobody but the player's house has purses.
- **A canvass.** The year before a count, a house of the Reach that owes nothing pays for a canvass for the highest
  office it has somebody to stand for: the largest it could pay for five times over. An upright head (honourable or
  just) stops at a handsome one. It is told in the chronicle, and told to you outright when one of yours is standing
  for the same office, so that it can be answered in the election year. Their lavish ones go on the record too, and
  you may charge them (Senate, The courts).

Measured over ten runs of forty years (two starts, five seeds each, 307 houses, nobody steering the player's house):
155 estates settled; 75 seats built, and at the end 50 houses with a manor, 12 with a great house and none with a
palace; 169 houses at the capital; 435 canvasses paid for (141 modest, 169 handsome, 125 lavish), about five an
election; 11 lavish canvasses charged before a praetor and 10 of them proved, about one a run.

### The numbers

| What | Number |
|---|---|
| An estate pays | 200 a year |
| The first estate on a person | 3,000 (fifteen years' purchase) |
| Each further one on the same person | 600 more |
| Most on one person | 3 |
| Regard for an estate: the holder, their spouse | +12, +6 |
| An estate in a match | +4 each, to +8 |
| Worth of land or a house bought | two thirds of its price |
| Seat: manor, great house, palace | 6,000, 15,000, 40,000 |
| Seat: keep a year | 100, 300, 600 |
| Seat: built | old blood +3, each grown person of the house +4 of the head |
| Seat: standing mends under | 50 (great house), 60 (palace) |
| Palace: a guest a year | +3 each way, up to 40 |
| Seat lost | 2 standing for each grade |
| A house at the capital | 5 years of its holder's income, at least 1,000 |
| At the capital | 3 votes at a count |
| Regard for a house at the capital | +10, or +14 from the head's own purse |
| One of the house buys their own | with the price and 300 beside |
| Canvass: modest, handsome, lavish | 500, 1,500, 4,000 for 3, 6, 10 votes |
| Regard for a canvass | +4 a size, +4 more if chosen |
| What was paid for is kept on the block | 20 years |
| Another house spends | one idle year in three, on what it could pay for 3 times over |
| Another house pays for a canvass | the largest it could pay for 5 times over |

### How it is built

- Commands (`src/Core/Commands/RichHouseCommands.cs`): `EndowCommand(actorId, personId)`,
  `BuildSeatCommand(actorId, holdingId)`, `TownHouseCommand(actorId, personId, fromPurse)`,
  `FundCampaignCommand(actorId, personId, size, offices)` with a size of `modest`, `handsome` or `lavish`. None costs
  influence: the coin is the cost.
- Rules: `src/Core/Systems/WealthSystem.Rich.cs` (land, the seat, a house at the capital, what other houses spend) and
  `src/Core/Systems/LadderSystem.Campaign.cs` (the canvass, other houses' canvasses, how a count stands). No dice.
- Kept: an estate and a house at the capital are `Heirloom`s of kind `estate` and `town_house` (a settled estate's id
  begins `settled_estate_`); `House.SeatGrade`, `SeatHoldingId`, `SeatSince`; `World.Campaigns`. A lavish canvass is an
  `OfficeDeed` whose `What` is `election`.
- Counters: `estates_settled`, `seats_built`, `town_houses_bought`, `campaigns_paid_for`.
- The screen: `src/Web/Components/RichHousePanel.razor`, shown at the head of `WealthPanel.razor`; the canvass buttons
  and the line on how a count stands are in `LadderPanel.razor`; the seat's mark is in `HouseScreen.razor`.
- Tests: `tests/RichHouseTests.cs`.

### Where it sits

The block is at the head of "Purses and the will", and it is as tall as everything else on that page put together, so the
page no longer opens on your purse. The owner asked to see both ways before choosing (2026-11-23: "Show me a mockup of both
and I'll tell you which one looks better"). Both are built: `HouseScreen.RichOwnPage` is false for the block where it is now
and true for a tab of its own, "Seat and estates", after "Purses and the will". The picture of the two side by side is
`tools/browser/shots/rich-placement-compare.png`. Until the owner chooses, it stays where it is.

### What I chose

Every number above is mine. The ones that decide most:

- **Fifteen years' purchase for land** (6.7%), so that an estate is an ordinary investment in land and no better than a
  market. The second and third on one person return 5.6% and 4.8%: past the first it is being done for the person.
- **An estate is the old kind of heirloom**, not a new thing, so nothing about holding, giving, leaving or losing one
  had to be written twice. What a child's land brings is simply kept in the purse they will have.
- **The seat is a record on the house, not a building in a slot.** A work on a world can be captured and pulled down,
  takes a slot, and is priced by what it returns; a seat does none of those. It is the house's, and it goes if its
  world goes.
- **One thing a grade**: room in the household, a floor under the house's standing, a guest of rank. Each uses a rule
  the game already had.
- **Seat prices** are set against what houses hold at year forty in the measured runs: the middle house 8,000 to
  25,000 credits and the richest 60,000 to 190,000. A manor is within reach of most, a palace of very few.
- **The seat and a house at the capital are not scaled by how easy money is.** Only what returns credits is; a hull is
  not either.
- **Five years' income for a house at the capital**, the middle of the owner's four to six.
- **"At the capital" is two things**: votes at a count, and hearing how the count stands. The game has nothing else
  that happens there for a person.
- **Canvass sizes** are set against the counts measured: a winner is usually 5 to 30 votes clear, and a quaestor's
  race is often decided by 1 to 5. Three votes turn a close race, ten most of them.
- **The candidate answers for a lavish canvass, not the head who paid**, as a candidate did for bribery at Rome; and it
  can be charged at once, as a bought praetor can.
- **Other houses announce their coin a year ahead**, so that the election year is when it is answered.

### Checked

Fourteen tests. Driven in a headless browser on the published build, with the boost "A full treasury" ticked. On
*The Old Lion*: an estate settled on a relative (the button then offered the next at 3,600), a manor and then a great
house built on The Lion's March, the mark "the house's seat: a great house" on that world's card, and a house at the
capital bought; the treasury went from 52,800 to 25,800 as the prices say. On *The Risen Officer*: the head stood for
quaestor, a modest canvass was paid for and raised to a lavish one for the 3,500 between them, the courts' list then
read "bought votes to be made quaestor in year 1 · can be charged", and after a house at the capital was bought the
ladder read "As the count stands, for 4 seats". No page error. No picture was taken: a screenshot of the page timed
out in the builder's browser. A canvass for a relative, a charge brought by another house and a seat lost with its
world were not seen in a browser: they rest on the tests.

### What is not built

- **Lending to other houses** (the fourth thing in the owner's line): another piece.
- **No events.** Nothing here raises a scene; a conviction for a bought election is told in the news of the year, since
  the two scenes for a magistrate removed are written about what was done in office.
- **A seat is not taken by whoever takes its world**: it is lost. It is not besieged or sacked apart from the world.
- **A seat takes no years to build** and costs credits only: no materials, no gang.
- **Nobody of another house has a purse**, so an estate settled in another house pays nobody; it is held, counted in a
  match and left like any other. A house at the capital bought by another house is always its head's.
- **A relative does not buy land for themselves**, or ask the head for a house or a canvass.
- **One of the house standing for consul** is not paid for this way: the consulship is contested by houses, and the
  treasury already canvasses for a house there.
- **The court of seven places is not made larger by a seat**; only the household's room is.
- The numbers are first settings.

Found while building it: a colony has no provinces until the turn of the year after it is planted (the provinces are
laid out before the year's building is finished), and `tests/ProvinceTests.cs` failed whenever a colony landed in the
last year it plays; the test now lays them out before it looks. And at a count, houses with no candidate, patron or
party in the race give their whole weight to one name: where their heads like several equally and those are level on
merit, it goes by the order of the candidates' ids.

## Not in this piece

- **Only your house has purses.** Everyone else in the Reach still has only their house's treasury.
- ~~A purse holds credits only.~~ Closed in part (W1): a person holds shares in their own name, and a founder draws a stake from
  what they founded. A venture can now be a person's own (W10, above). No world is: that is W12.
- ~~Dowries still come from the treasury.~~ Closed: from the head's purse if they say so, and from the bride's own first.
- An heirloom can be entailed. A world or a venture cannot: any of them can still be sold by the head.
- ~~An heirloom is one of four kinds, and none is ever made, won or given by another house.~~ Closed (W8): see "New things". What is left is W9.
- Two of the five events (the will is read, sold out of the family) were not seen to fire in the soak.
- The numbers are first guesses.
