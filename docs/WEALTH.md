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

## Not in this piece

- **Only your house has purses.** Everyone else in the Reach still has only their house's treasury.
- **A purse holds credits only.** Shares, ventures and worlds still belong to the house. A relative cannot yet invest on
  the Exchange or found a venture with their own money.
- **Dowries still come from the treasury.**
- **Nothing is entailed.** Any world or venture can still be sold by the head.
- You cannot lend to a relative, or give one a sum beyond the allowance.
- Heirlooms do nothing but be worth something, and pay if they are estates.
- Two of the five events (the will is read, sold out of the family) were not seen to fire in the soak.
- The numbers are first guesses.
