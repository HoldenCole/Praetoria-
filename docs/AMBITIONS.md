# Ambitions: the people of the house go after what they want

Built 2026-11-14. The last open item against the rule in docs/PROCESS.md ("One person, not a house"): every relative had
an ambition as data, and nothing came of it but defiance. The panel is on the Dynasty screen.

## What they do by themselves

Every grown member of the house, born to it or taken in, pursues their ambition each year without being told.

| They want | What they do about it | They have it when |
|---|---|---|
| A command of their own | Take a command that stands empty; rise in their service | They have commanded a fleet of the house for three years |
| A fortune of their own | Save; found a venture with their own money once they can afford the cheapest; take a share of what it earns | Their purse reaches 50 |
| To see the house rich again | While the house is poor (under 60) they keep 3 credits and give the rest; while it is only thin (under 150), a third of what they can spare. They found a venture for the house, and its cost counts; so does what it brings in, 2 a level every year it stands | They have given the house 34 credits of their own, in money and in what they built |
| To rise in their service and be heard | Work at their service and their speaking | They hold an office of state, or have risen far |
| To be the first of the house | Measure themselves against whoever of the house stands highest; the two grow cooler toward each other | They are well ahead of that person |
| The seat of the house | Build a standing to rival the heir's | They stand to inherit |
| A great love | Court whom they like (docs/MARRIAGE.md) | They are married for love |

"How near" is shown for each, from 0 to 100, with a line saying what they did this year.

A venture a relative founds is the house's, and carries their name as founder. One who wanted a fortune takes a credit a
year from it for each of its levels.

Having had what they wanted, after four years they set their mind on something else.

## What they ask of you

Not in their first year, and no more than once in four years, one of them asks the head for something that would help:

| They ask | Granting it costs | Granting it does |
|---|---|---|
| The command of a fleet whose admiral they think they could better | The regard of the admiral put out | They have the command |
| A stake to found a venture | The venture's cost, from the treasury | They found it for the house |
| A word spoken for their promotion | 2 influence | They rise a rank in their service |
| To be named your heir | 2 legitimacy and the regard of whoever the law would have given | They are named (see below) |

- **Grant it** and they think better of you (+12), and better again when they finally have what they wanted.
- **Say no** and they think worse of you (−8; −15 for the seat).
- **Say nothing** for two years and they take it for a no (−5).

That is the whole of the head's part. They get there, or do not, either way.

**Thwarted.** Ten years of wanting it, little to show, and a head who has said no more often than yes: they think a
little worse of you every year, and an event comes of it.

## A named heir

The head may name anyone grown of the house to succeed them, ahead of whoever the succession law would give, with or
without being asked. A named heir stands first under any law. Passing over the law's heir costs the house 2 legitimacy
and that person's regard for you and for the one named. The head can leave it to the law again at any time, and a named
heir who dies or leaves the house is named no longer.

## Events

Four, in `content/events/ambitions.json`: what they wanted; no nearer it; a venture of their own; the heir is named.

## For content

- Flags: `kin_asks_for_help` (each year); `kin_fulfilled`, `kin_thwarted`, `kin_founded_a_venture`, `heir_named`,
  `kin_refused` (deeds). Character flags `has_what_they_wanted`, `thwarted`, `founded_a_venture`, `asking_the_head`.

## How it is built

- `Character.Pursuit`, `Business.FounderId`, `House.NamedHeirId`, `World.Founders` (who began a venture, until it stands).
- `AmbitionSystem` runs for the player's house at the start of the year's reckoning and uses no dice. Founding goes
  through the same command the player uses.
- With the family autonomy setting at zero nobody pursues anything.

## Checked

Eight tests. The soak passes on all eleven starts with a random player who grants, refuses and names heirs; all four
events fired in it. In the browser on *The Old Lion*, touching nothing but the first request: three relatives had
pursuits by the second year; one asked to be named heir in the third and was; another put a credit or two of her own
into the house in each of four years.

## Quarrels and failures

**Two who want one thing.** When two of the house are after the same prize (standing for the same office, asking for
the same fleet, or one wanting the house that the other stands to inherit), they quarrel. Each thinks 3 the less of
the other every year it lasts, down to −60, and the ambitions panel says who quarrels with whom over what. The first
year, there is an event: stand behind one (they think +10 of you, the other −10), tell them both it stops (both −3
toward you, +8 toward each other), or keep out of it. The quarrel ends when the prize is settled or one of them wants
it no longer.

**A failure is marked.** A relative who stands for an office and is not chosen, or loses a battle in command, has it
written into their life as a failure. It costs their renown (−3), their nerves (stress +5), and counts against what
they want. There is an event the first time the head hears of it. **Three failures and they give the hope up** and
turn to something else; if the head never once helped, they think 4 the less of the head for it. The head's own
failures are written down the same way.

## Where an ambition comes from

A child who comes of age without one is given one. **Two in three want what they were brought up toward**: a command
for one raised to arms, the Senate for one raised to the forum, a fortune for one raised to the counting-house (or to
see the house rich again, if it is poor when they come of age). **One who was set against their own bent wants
something that is nobody's plan**: to outshine everyone, if they are proud or ambitious, or a love of their own. The
third in three is left to the dice, as all of them used to be. So the course you set a child (docs/EDUCATION.md) is
also a say in what they will want, and forcing one against their nature has a cost you meet ten years later.

**The loser of a quarrel** over an office or a command, when the other one gets it, thinks 8 the less of them and
stops coming to table. The winner has nothing to forgive.

## The house divides behind a quarrel

Closed straight off the register on 2026-11-15 (the second half of docs/GAPS.md A5). While two of the house quarrel
over a prize, each of the others, the head apart, who thinks better of one than of the other by 10 or more stands with
that one, and thinks 2 worse of the other each year it lasts (never below −40 on that account). Each of the two is
shown with how many of the house are with them and how many against.

## Not in this piece

- **Only your house.** Nobody in any other house pursues an ambition this way.
- ~~Standing for office is not theirs to do.~~ Closed by the ladder of offices (docs/LADDER.md): one who wants to rise
  and be heard, or to be first, now puts their own name down for quaestor, aedile or praetor.
- A venture cannot fail, so that failure is never marked (docs/GAPS.md A6).
- The numbers are first guesses.
