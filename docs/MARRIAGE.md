# Love, courting and marriage

Built 2026-11-13. From the roadmap's person-and-family group. Before this a marriage was one command (a proposal the
other house accepted or refused on the spot) and a few events; once made, it only allowed children.

## Two ways to marry

| | A proposal by treaty | A courtship |
|---|---|---|
| Where | Houses screen | Dynasty screen |
| Takes | One season, 2 influence | Years: one act a year |
| Refused when | Their house is well above yours, or they dislike you | The family's consent is short, or the person will not have you |
| The contract | Custom: no dowry, children by the usual rule | Bargained: the dowry and whose name the children carry |
| The marriage begins | Cool (an arranged match) | Warm if the person wanted it (a match for love) |

A courtship can win a match a proposal cannot. A person who wants the match argues for it at home, which is worth 15
to the family's consent. That is how a new name marries into an old one.

## Whose courtship it is

You play one person (docs/PROCESS.md, "One person, not a house"). Only your own courtship is yours to press.

| Whose | Who does the courting | What the head does |
|---|---|---|
| Your own | You, one act a year | Everything below |
| One of the house, by their own choice | They do. It advances by itself and never lapses for want of your attention | Optional help with the family (a gift, speaking for them, a word against a rival). When it is serious they ask your **blessing**: give it (and the match goes to the other family with the terms you set) or **forbid** it |
| One of the house, arranged by you | They do their part if they are willing | You choose the match and deal with the family. They have a **wish** about it, shown before you choose |
| A child | Nobody | The families agree a promise |

- **Kin choose for themselves.** Each year an unmarried adult of the house may take a liking to someone, more often if a
  great love is their ambition. How often scales with the family autonomy setting; at zero they wait to be matched.
- **Asking your blessing.** At favour 60 they ask. They wait three years. Blessing it pleases them. No answer in three
  years, and most give it up and think less of you.
- **Forbidding it.** The loyal obey and mind a little. Others mind a lot. One who is wilful (ambitious, proud, or set on
  a great love) and far gone (favour 75) marries anyway, without anyone's leave, at a small cost to the house's standing.
- **Arranging against their wish.** Marry someone to a match they did not want (wish under 25) and they hold it
  against you. A wilful one with a wish under 15 refuses outright.
- **Their marriages are theirs.** You tend and end only your own.

## A courtship

Two numbers, both 0 to 100.

- **Favour**: what the courted person feels. It starts from what there is between the two of them (fixed for the pair),
  their regard for the suitor, the suitor's charisma, a shared culture, the years between them and the suitor's nature.
- **The family**: what their house thinks. It starts from their head's regard for yours, the gap in legitimacy and rank,
  the two houses' wealth, a tie of patronage, a shared party, the suitor's career, and old blood's view of a new name.

One act a year for each suit:

| Act | Cost | Does |
|---|---|---|
| Time together | 1 influence | Favour up, more for a suitor with charisma |
| A gift | 12 credits | Favour up a little, the family up a little |
| Put the case to the family | 2 influence | The family up, more for a head with oratory or diplomacy |
| Put word about against a rival | 1 agent | Sets the rival back, more with intrigue. It may be traced to you, which costs you with the family |

A suit nobody attends to loses a little each year and lapses after four. A house can have three suits in hand.

**Thresholds.** The family agrees at 50, counting the terms. Favour of 60 adds 15 (they argue for it at home); below 25
takes 10 off; below 15 the person will not have it at all. At favour 75 they would marry without leave.

## Rivals

Another suitor may press for the same hand. A steward's proposal to someone you are courting becomes a rival suit
instead of a wedding, and one can appear by chance. The rival starts between 10 and 35 and gains 5 to 9 a year, so
there are usually three or four years to answer them. At 60, and ahead of yours (the
average of favour and the family), the family accepts it: they marry, your suit ends, and it is felt.

## The contract

Set when the match is proposed, and shown with the family's answer before you ask.

| Term | Choices | Effect on the family's answer |
|---|---|---|
| Dowry (passes from the bride's house to the groom's) | none, customary, rich (double) | Receiving: none +8, rich −14. Paying: none −8, rich +12 |
| The children's name | by custom, yours, theirs | Taking it against custom −15; conceding it against custom +15 |

Custom gives the children to the head of a house, otherwise to the greater house, otherwise to the father's. The
customary dowry is 8 to 60 credits by the bride's house's rank and wealth. A house pays what it has.

A refusal costs 5 of the family's consent, and they answer once a year.

## Other ways it ends

- **Marrying without leave.** At favour 75: no dowry, the children yours, a love match, their head's lasting anger
  (−40) and 4 legitimacy.
- **A promise between children.** From the age of six a child can be promised. Only the families matter; nobody courts.
  They wed when both are sixteen. Breaking a promise costs 3 legitimacy and their head's regard. A steward who comes for
  a promised person is turned away.
- **A suit brought to you.** Another house asking for one of your kin no longer simply takes them: the suit waits two
  years for the head's answer, with terms. Your kin has a view. Refuse someone they wanted and they hold it against you.
- **Letting it drop**, at any time.

## The marriage afterwards

Every marriage, however made, has a **warmth** from 0 to 100.

| Warmth | Called | Effect |
|---|---|---|
| 80 and over | devoted | As warm |
| 65 and over | warm | More children (up to 1.3 times as likely); eases the head's stress by 2 a year; the two houses' heads grow closer |
| 31 to 64 | settled | Nothing either way |
| 16 to 30 | cold | Fewer children (down to 0.8); at 20 and under it adds 2 stress a year |
| 15 and under | living apart | No children; the two houses' heads grow apart. They are reconciled at 30 |

Warmth drifts 2 a year toward a level set by what there is between the two, how the match was made, their natures and
their children, and falls when either is under great strain. The head can give their own marriage the quiet months
(1 influence, once a year) for +6 and more.

**Divorce** ends it. The house loses 4 legitimacy (8 under a throne, 2 less if they already lived apart), their head
takes it as an insult, a dowry received goes back, and the children stay where they are.

**Widowhood.** The survivor is free to marry again.

## Events

Thirteen, in `content/events/courtship.json`: an introduction that can start a courtship; a rival for the hand; the
family asking your prospects; a letter unanswered; the wedding day; chosen against; a cold house; the affair is known;
a marriage of two minds; the family's letter after an elopement; the in-laws ask a favour; widowed; one of the house
married without your leave.

## For content

- Effects: `courtship` (`role`, `favour`, `consent`, `rival`), `marriage` (`role`, `warmth`), `beginCourtship`
  (`suitor`, `target`, `favour`).
- World flags set each year: `courting`, `courtship_rival`, `suit_received`, `child_betrothed`, `married`,
  `marriage_warm`, `marriage_cold`, `estranged`.
- World flags raised by a deed and kept for the next events phase: `just_wed`, `love_match`, `jilted`, `eloped`,
  `divorced`, `betrothal_broken`, `undermining_exposed`, `widowed_now`, `suit_refused`, `kin_asks_blessing`, `kin_eloped`,
  `match_forbidden`.
- Character flags: `courted` (someone the protagonist is courting), `rival_suitor`, `widowed`, `eloped_kin`.
- Counters: `marriage_warmth`, `courtship_standing`, `love_matches`.

## How it is built

- `Courtship` and `Marriage` are plain data on the world. The bond itself is still the pair of Marriage edges.
- `CourtshipSystem` is static and uses no dice: chance is a hash of ids and the year.
- `CourtshipSystem.Wed` is the one place a marriage is made. A marriage made by an event's `addBond` gets its record at
  the year's reckoning.
- Stewards still marry each other by proposal. `Intercept` catches the proposals that touch the player's house.

## Checked

Fourteen tests. The 60-turn soak passes on all eleven starts with a random player who courts, settles suits, answers
them, tends marriages and divorces; it found one save/load fault, now fixed and tested. `tools/browser/courtship.js`
plays a suit to its end in the browser: in one run a three-year courtship ended in a love match, in another the suitor
neglected the family and lost to a rival.

## Not in this piece

- Stewards do not court; houses other than the player's marry by proposal only, and never divorce. Your own kin court,
  but do not yet divorce or stray on their own.
- No children outside marriage. An affair is an event with consequences for the marriage, not for the succession.
- A spouse does not yet do anything: no counsel, no running the house in your absence. Their skills do not count.
- A spouse stays a member of their own house; nothing moves between households.
- The dowry is credits only. Land, shares and heirlooms wait on personal wealth.
- No love outside courtship: affection between people who are not being courted does not exist.
- Two of the twelve events (the affair, the elopement's letter) need a choice the soak's player rarely makes, and were
  not seen to fire in it.
