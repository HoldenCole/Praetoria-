# The ladder of offices

Built 2026-11-14. Before it the Republic had two consuls and nothing beneath them: any house of standing could stand
for consul, and a political career had one step. Now there are three elected offices below the consulship, each held
by a person and not by a house, and nobody is consul who has not climbed.

## The rungs

`content/senate/ladder.json`. An election is held every fifth year, and every seat falls vacant at it.

| Rung | Seats | From the age of | Must have been | Pays (a year, to the holder's own purse) | Teaches | Standing |
|---|---|---|---|---|---|---|
| Quaestor | 4 | 25 | nothing, but a place in a service | 100 | economics | 3 |
| Aedile | 2 | 30 | quaestor | 100 | administration | 5 |
| Praetor | 2 | 34 | quaestor | 200 | law | 8 |
| Consul | 2 | 40 | praetor | (the consul offices, docs/REPUBLIC.md) | | |

- **Aedile is optional**, as it was. It is the rung that gives games.
- Nobody holds the same rung twice, and nobody who has gone higher stands for a lower one.
- A seat lapses if its holder dies or grows frail. It stays empty until the next election.
- **What a person has held is theirs for life** (`Character.Cursus`) and is written into their record
  (docs/LIFE.md): "Elected quaestor."
- There is no ladder under a throne. Offices there are the throne's to give (see the roadmap, politics under a throne).

## The election

Every fifth year, before the consular election in a year that has both.

- **Who stands.** From each other house, its best qualified person, about two houses in three at each rung. A person
  stands for the highest rung open to them. Someone who stood higher and was not chosen may be put up for a lower rung
  the same year if their house has nobody else for it. From the player's house, only those whose names are down.
- **The count.** Each house gives its whole weight in the Senate to one candidate: its own, else its patron's, else
  one of its party, else whoever's head its own head likes best. To that each candidate adds their own merit: oratory
  and charisma twice over, career rank twice over, and a sixth of their renown. Canvassing by the house's order
  (docs/REPUBLIC.md) and the head's backing count too.
- The highest rung is filled first, so nobody is given two.
- The chronicle prints the result with each winner's count.

## What the head may do, and what is not the head's

The rule is docs/PROCESS.md: you play one person.

- **Stand yourself** for a rung you qualify for: 2 influence. Withdraw freely.
- **Call in what is owed** for yourself, or **speak for** one of the house who is standing: 2 influence for 6 votes,
  twice at most. A relative you speak for thinks better of you.
- **A relative decides for themselves whether to stand.** One who wants to master the Senate, to outshine every
  rival or to take the throne puts their own name down two years before the count, for the highest rung open to them.
  The head cannot enter them and cannot take their name down.
- **Ask them to stand down**, once a term. The loyal withdraw and think less of you. The wilful leave their name where
  it is and think less of you too.
- **Give games** as aedile, once a term, for 1,200 credits of your own: the house gains 2 legitimacy and it is written
  into your life. An aedile of the house with the money gives theirs without being asked.

With the family's independence set to deferential (docs/CHARACTER.md), relatives do not stand by themselves.

## What holding office does

- **Pay**, to the holder's own purse (docs/WEALTH.md), for the player's house.
- **Learning by doing** (docs/EDUCATION.md): a sitting magistrate practises the skill of the office.
- **Standing in the Senate.** A house's senatorial order (docs/REPUBLIC.md) counts "magistracies its people have
  held": the highest rung any living member has reached, and one more for each other member who has climbed, to 14.
- **The consulship.** `content/senate/offices.json` gives both consul offices `minAge: 40` and `requires: praetor`.
  A house with nobody of that age and record cannot stand.
- **The ambition to master the Senate** (docs/AMBITIONS.md) counts rungs climbed.

## The work of office

The owner's rule (docs/PROCESS.md, "every position has work"): an office is not a title. Every year each sitting
magistrate has business, and what they do with it has consequences.

| Office | The year's business | The choices |
|---|---|---|
| **Quaestor** | The accounts of the Reach. One quaestor a year is given the most lawless region, if the capital's purse can bear it. | **Close them as they stand** (trust +1, the house thought a little better of). **Find money for the lawless region** (its lawlessness −2, 400 credits from the capital's purse, its houses think +6 of yours). **Favour your own house** (+800 credits to the house, trust −3, and one time in four it comes out). |
| **Aedile** | The works and the lanes, paid from the aedile's own purse. | **Repair the lanes** of the region where the house's worlds are (600 credits, lawlessness −2). **Grain for the squares** (600 credits, legitimacy +2). **Do nothing** (trust −1). |
| **Praetor** | A case between two houses at odds, neither the praetor's own. The law is with one of them. | **Rule on the law** (the loser pays 500 credits, the winner thinks +6 and the loser −6, trust +1). **Rule for a friend** (the house that thinks better of yours: +12 from them, −12 from the other; if the law was not with them, trust −3 and legitimacy −1). **Take the gift** (1,000 credits to your own purse from the richer house, a ruling for them, trust −4, and one time in three it comes out). |

- **You decide your own; nobody decides yours and you decide nobody else's.** A relative who holds an office decides
  their own business, as every other house's magistrate does, by nature: the honourable and the just deal straight,
  the ruthless and the cruel take money about half the time, the loyal and the vengeful rule for their friends.
- **Left undone**, the clerks close it at the year's end and it is noticed (trust −1). An aedile who does nothing is
  noticed only if they could have paid for the works; one who could not is merely poor.
- **When it comes out** that a magistrate took money, they are removed, may not stand for five years, and the house
  loses 5 legitimacy. If it is you, or one of yours, there is an event.
- **Public trust in the magistrates** runs from 0 to 100 and starts at 60. See "What trust does" below. **Below 35,
  "the magistrates are known to be for sale" is one of the things wrong with the Reach**, so it counts toward the
  times a Caesar needs (docs/CAESAR.md) and toward the pressure that brings the great war (docs/BREAKAWAY.md).
- A house that is a party to a case hears of it (an event when it wins, another when it loses). The same quarrel is
  not heard two years running, and no house is before two courts in one year.
- **A relative who stood without a word from the head and lost blames the head** (regard −10, an event). One who was
  spoken for and won knows what they owe (+6).

Where: the top of the ladder panel (Senate, "Your standing") shows your business with its three choices and what each
does, what the Reach thinks of its magistrates, and what every magistrate did this year. The outliner says when your
business is waiting.

## The bench and the seat

The same rule for the positions that are not elected offices.

**A senator's year.** A house that has no seat (docs/UPSTART.md: it has come in by none of the four doors) has no senator's year, owes no attendance and has no place on a committee; its head's year is the work of their service, and one who does that work at a junior step goes up after four years of it. The head of a house in the Reach that sits has the house's seat, and each year one thing to do with it
besides the chamber's motions:

| Choice | What it does |
|---|---|
| **Read the accounts** | If a magistrate of another house took money this year and has not been found out, you may follow it to them (one time in two): they are removed and barred, trust +2, you gain 1 influence and a little legitimacy, and their house thinks 15 the less of you. If there is nothing, or nothing provable, trust +1. |
| **Speak for your region** | The region where your worlds are holds a little closer to the Reach (allegiance +2), its houses think +3 of you, legitimacy +1. |
| **Work your bench** | Your party's leader thinks +6 of you and you gain 1 influence. If you lead it, every house on the bench thinks +3 of you. |

Three years running of doing nothing with the seat is remarked on: legitimacy −2 and an event.

**Somebody else reads the accounts too.** Each year one upright senator of another house audits last year's
business. Money a magistrate took (yours included) and got away with at the time may come out through them a year
later.

**A Judge's hearing.** Anyone of the house who is a Judge or above in the law (and is not a sitting praetor, who has a
court already) hears one matter a year: an estate has brought a house before the bench, and the law is with one of
them. **Rule on the law** (the side in the right thinks better of your house, the other worse; trust +1), **find for
the house** whatever the law says (+8 from the house, −6 with the estate; trust −1 if the law was against it), or
**find for the petitioners** (+6 with the estate, −8 from the house; the squares do not mind a ruling bent their way). The
estate's approval is the same number the Chancery shows (docs/POLITICS.md).

**A Justice's appeal.** A praetor's ruling that went against the plain law is appealed the next year before a
Justice who is of none of the houses in it: one of your house if there is one, otherwise a Justice of another house,
who decides by nature (the honourable overturn; one obliged to the praetor's house upholds). **Overturn** (the house
that won wrongly pays back double, the wronged house thinks +10 of the Justice's, the other two −8 each, trust +3),
**send it back** (the wronged house is at least heard), or **uphold** (the praetor's house +8, the winner +4, the
wronged −10, trust −2). So a bent ruling against your house may be mended by the courts, and a bent ruling of your
own may be undone.

**The charge of an office of the Reach.** The consuls, the Treasurer and the Speaker each carry one decision a year.
(The Admiral has the Reach's fleets, which is work enough.) One who holds such an office spends the year on it: it
takes the place of the senator's year.

| Office | The choices |
|---|---|
| **Consul** (both) | **Call the magistrates to account**: up to two who took money this year are looked at, each caught one time in two and removed; trust +2 and +2 for each caught; every magistrate's house thinks −2 of you. **Send the fleet onto the lanes** of the most lawless region: lawlessness −4 for 400 credits of the capital's; its houses think +4. **Spend the year on your bench**: its houses +4, influence +2, trust −1. |
| **Treasurer** | **Balance the books**: half the year's surplus is banked for the capital's purse (in a year of shortfall nothing is, and the Reach's own fleets each lose 5 readiness), trust +1, legitimacy +1. **Forgive a region its levy**: the region that holds loosest gains 4 allegiance for what its landed houses would have paid, out of the capital's purse; its houses think +4. **Let the contracts to your own house**: a third of what the year calls for (600 to 1,800 credits), trust −3, and one time in four it comes out: the office is taken away and you may not stand for five years. |
| **Speaker** | **Keep the order fair**: every bench's leader +2, trust +1, legitimacy +1. **Bring your own business forward**: influence +2, your bench's leader +4, the others −3. **Give an enemy no day**: whatever that house has moved is held over a year; the house you are worst at odds with thinks −10 of you, those who hate it think +5, trust −1. |

Other houses' officers decide by nature, at once, and what they do reaches you: a Treasurer who forgives your own
region its levy and a Speaker who keeps your house off the floor each bring an event. A great office held and not used
is noticed: trust −1 and legitimacy −1. A bench or a desk left empty costs its own house (the commons, or the
merchants, think one the less of it), not the Reach's name.

**The budget.** The Treasurer's charge has figures, drawn from the state of the Reach when the year's business is
drawn and kept with it, so that what is read is what is decided. *What came in*: 200 credits assessed on each landed
house of the Reach, less the 200 of every house whose home region is lawless (40 or more): the lanes lose it. *What is
called for*: a hundred credits for every hull of the Reach's own fleets, 200 for every region with lawlessness of 20 or more,
and 200 for every region whose allegiance is under 60. The difference is the year's surplus or its shortfall, and the
three choices are read against it: "3,400 credits were assessed on the landed houses and 3,000 came in: the lawless lanes
lost 400. The Reach's own hulls call for 400, its lanes for 800 and its loose regions for 600: 1,800 in all. That leaves 1,200
over."

**The chair.** The great offices also act on the Senate's real business, on the docket itself.

| Who | What | Cost and limit |
|---|---|---|
| **The Speaker** | **Holds a motion over**: it is not reached this year and is voted next year. | Once a year; a motion can be held over once. Whoever moved it thinks −6 of the Speaker's house; whoever it was aimed at, +4. Holding over what is aimed at the Speaker's own house costs a point of trust. |
| **The presiding consul** (the senior; the junior when there is no senior) | **Speaks from the chair** for or against one motion: every house that neither moved it nor is its target leans with the chair by 1, and by 2 if it sits on no bench. | Once a year; once on a motion. The mover thinks +3 or −4 of the consul's house. |
| **The presiding consul** | **The casting vote**: a tied Senate goes as the presiding consul's own house voted. A tie used to fail whoever was in the chair. | None: it is the office. |

Officers of other houses use it by their interest, before the vote. A Speaker holds over a motion aimed at their own
house or at their bench's leader. A presiding consul speaks against such a motion, or otherwise for one that a house
of their own bench has moved. A motion of yours held over by another house's Speaker brings an event. The Senate
screen shows on each motion whether it has been held over and what the chair has said, gives the Speaker and the
presiding consul their buttons there, and says of a tied count which way the casting vote will take it.

**A desk, and a posting.** The other two careers ask something of their ranks as well, of the people of your house.

- **A Governor, a Minister or a High Treasurer** has the year's returns from the house's region across their desk.
  **Assess it fairly** (the merchants +2, the region's allegiance +1, trust +1), **press the collection** (600 credits to
  the house, the commons −4, allegiance −1), or **keep a share** (600 credits to your own purse, trust −2, and one time
  in four the returns are reopened: you are broken a grade and the house loses 3 legitimacy).
- **A Commander or above with no fleet under them** has a year ashore. **Drill the house's crews** (every fleet of the
  house +4 loyalty), **patrol the home lanes** (lawlessness −2), or **sit on a board of inquiry** (the officers +2,
  trust +1, influence +1). With a fleet, the command is the work and there is no posting.

**The lowest ranks.** Below those, a rank still has a year's work, and how it is done is the junior's own.

| Rank | The work | The choices |
|---|---|---|
| **Aspirant, Advocate** | A brief before a lesser court: a client house against another | **Plead it on the law**: learn the law and some speaking; win (likelier the better you speak and know the law) and the client thinks +4 of your house. **Take the side that pays**: 400 credits to your own purse, the other house −3. **Plead for one who cannot pay**: practice at speaking, and the commons +2. |
| **Clerk, Administrator** | A year of the administration's ledgers | **Keep them exactly**: learn the administration; the house is a hundred credits the better. **Make yourself useful**: a little influence, a little economics. **Pad an account**: 300 credits to your purse; one time in five it is found and written against you as a failure. |
| **Cadet, Ensign, Lieutenant** with no command | Watches and drills | **Stand your watches**: learn tactics. **Volunteer for the patrol**: learn gunnery, the home lanes −1 lawlessness, and one time in ten you come back hurt. **Make friends in the mess**: the officers +1, a crew of the house's a little steadier, some leadership. |

What is learned is on top of what the service teaches anyone by the year. A junior who lets the year go by loses only
the learning. With this, every rank of every service has work.

A relative with a desk, a posting or a junior's work decides for themselves.

**The seat is the house's.** If the head dies or lays the house down after the year's business is drawn up, the new
head has the year.

The player decides their own; a relative on the bench decides theirs by nature. Where: "Your duties this year" at the
top of the Senate's standing view, above the ladder. The outliner says when a matter is waiting.

## What trust does

**How it moves.**

- **Ordinary honest dealing mends it, and no more.** Closing the accounts straight, ruling on the law, keeping the
  order fair: each is a single point, and all of them together mend trust by **one point a year at the most**, and
  only back up to 60. Nobody is admired for doing the job.
- **Something notable lifts it past 60**, to 85 at the highest: a thief followed to their office (+2), the magistrates
  called to account (+2), a bent ruling overturned (+3).
- **Money taken breaks it**: a quaestor's favour −3, a praetor's gift −4, a ruling bent for a friend −3, the Reach's
  contracts let to the Treasurer's own house −3, and −3 again each time one of them is found out. Catching a thief no
  longer outweighs the theft.
- Above 60 a good name fades a point a year. **Below 60 nothing mends it but honest dealing.**

**What it does.**

| Trust | Elections | The commons | Standing |
|---|---|---|---|
| **Below 35** (for sale) | A house with a scandal loses 12 votes at every count | Think 3 the less, every year, of every house that holds an office | The house that holds the capital loses a point of legitimacy a year, down to 20 |
| **35 to 49** | A house with a scandal loses 12 votes | | |
| **50 to 74** | A house with a scandal loses 6 votes | | |
| **75 or more** (honoured) | A house with a scandal loses 6 votes | Think 1 the better, every year, of every house that holds an office | Every house that holds an office gains a point of legitimacy a year, up to 70 |

- **A scandal** is a magistrate or an officer of the Reach removed for taking money. It lies on their house for **ten
  years**, at the count for the ladder and for the consulship both. The person removed loses 8 votes more, for good.
- There is an event when trust first falls below 35, and another when it is believed again (back to 50).
- What a person of your house does to an estate (a judge's ruling, a governor's returns, a year on a board) now
  **lasts**: it fades a point a year instead of being gone within a year.

The ladder panel says in a line what trust is doing this year, and tells you if your house is under a scandal, until
when, and what it costs.

## When a saga opens

- Anyone of an age and rank to have climbed already has: a place in a service and 28 years makes a former quaestor;
  36 years and a second rank makes a former praetor.
- The seats are not empty in the first year. They are filled from those people, one of a house to a rung, and none
  from the player's house, which wins its own. They sit until the first election.
- Both happen once per world, so a save made before this piece opens with a full bench and no second seeding.

## A junior's thread, and who else minds a magistrate for sale

Closed straight off the register on 2026-11-15 (docs/GAPS.md J4, L9).

**Somebody above them, by name.** A junior's year is no longer three things in the air. The advocate's brief is heard
by a named judge; the clerk has a named senior at a desk; the junior officer serves in a named ship under whoever
commands it: one of their own house's, failing that their patron's, failing that one of the house that holds the
capital, which is the Reach's own. (As first built it had to be the house's own, and the Risen Officer, whose house
has no fleet, was told there was no ship for him. Seen in the browser, and changed the same day.) It is the same person year after year while they live and keep their place, and one of the
junior's own house if it has one.

| The junior does | What the one above thinks |
|---|---|
| Pleads on the law | +2 for a win, +1 for a case well lost |
| Takes the side that pays | −2 |
| Pleads for one who cannot pay | +1 |
| Keeps the ledgers exactly | +2 |
| Makes themselves useful | +4 |
| Has a padded account found | −8 |
| Stands watches / volunteers / makes friends in the mess | +2 / +3 / +1 |

What that one thinks is the junior's standing, and it does three things. It moves the odds of winning a brief (a
fifth of it, 8 either way at most). After **three good years** under them and a regard of **20**, they speak for the
junior, who goes up a step sooner than the years alone would have taken them: once at each rank, and never faster
than the service lets anybody rise. And an advocate who appears before a judge of their own house who is not upright
wins cases they would have lost (15 in a hundred); each such win is seen, and costs the Reach a point of trust.

**The other estates.** While the magistrates are not believed (trust under 35) the merchants and the magistrates' own
order think 2 worse a year of every house in office, beside the commons' 3; while they are trusted (75 or more) each
thinks 1 better.

What the one above does with a junior beyond speaking for them is the next section; trust told apart by office and
by region is the one after.

## A patron, and a falling out

Closed off the register on 2026-11-15 (docs/GAPS.md J5).

**Taken up.** A senior who thinks 20 or better of a junior, has had three good years' work of them and outranks them
takes them up. From then on that senior is the junior's **patron**, and is the one above them wherever either is
posted: a kinsman who comes to sit at a desk does not take the patron's place, and a junior officer serves in the
fleet their patron commands. One patron at a time.

**Taken along.** When a patron rises (a step in rank, a magistracy, an office of the Reach) their people go with
them to the new post: the junior learns their own trade faster for it that year (law, administration or tactics) and
is paid 300 credits for the place. Once for each rise.

**A falling out.** When the one above has come to think −20 or worse of a junior who has worked under them, the two
fall out. A patron is lost. The years under that senior count for nothing. And that senior will not be above the
junior again: somebody else has to notice their work. It happens once with any one senior. The usual road to it is
the junior's own: a padded account that is found costs 8, a side taken for money 2.

**A patron ends** when they die, retire or leave the Reach.

**Other houses' juniors.** This was kept for the player's house only. The juniors of every house of the Reach now
have somebody above them and a standing with them. They make no choices, so their year is reckoned: four years in
six their work is seen and found good (regard +3, to 40 at the most, and a good year counted), one in six nothing is
noticed, one in six it goes badly (−6). They are taken up, taken along, fall out and are spoken for by the same
rules.

On the Senate screen, a junior's year says who is above them and where they stand: how many of the three good years
they have, that they have been taken up, or that the senior is close to having done with them. Three events tell the
head of a house when one of their own is taken up, taken along, or falls out.

Left, and mapped: a patron does nothing for a junior but take them along and speak for them. A junior cannot seek
one out or choose between two, a patron asks nothing back, and a patron's enemies are not the junior's (J6).

## Whose name it is

Closed off the register on 2026-11-15 (docs/GAPS.md L10). Public trust was one number for the whole Reach. It still
is, and it is what the Reach as a whole thinks of its magistrates. But every point that moves it is now also set
against two other things.

**The office whose business it was.** The quaestors, the aediles, the praetors, the consuls, the Treasurer, the
Speaker, the judges and the administration each have a name of their own: the Reach's figure, plus or minus what
that office's holders have done. A quaestor who gives the surplus to a friend lowers the Reach's trust by 3 and the
quaestors' own by 3 more. A former aedile convicted mends the aediles' name. **An office's name is what a scandal
costs at the count for that office**: a house with a scandal loses 6 votes at a count, and 12 where the name of the
office being filled stands under 50. It used to be the Reach's figure at every count.

**The region where it was felt.** Each region has its own belief in the magistrates: the Reach's figure, plus or
minus what has been done to it and in it (the region the business concerned, or else the home region of the house
that did it). A region that believes them under 35 holds a point less closely to the Reach each year; one that
believes them at 75 or more, a point closer. A region nothing has happened to goes as the Reach goes.

A wrong is felt twice over in the region it was done to: it was their money, or their case. Ordinary honest dealing
mends a name that stands under the Reach's and does no more; only something notable (a thief caught, a bent ruling
overturned) lifts a name above it. An office's name runs a point a year back toward the Reach's figure; a region
remembers longer, and runs back a point every second year. Neither can stand more than 30 from the Reach's. The ladder panel
says which offices and regions stand five or more apart from the Reach ("The quaestors are trusted less than the
rest (48 against 57)"); the Worlds screen shows a region's belief beside its allegiance when it has a figure of its
own.

## Committees

Built 2026-11-15 (docs/GAPS.md L8). A senator's year was one of three things with nothing carried from one year to
the next but a count of absences.

**Three standing committees**: the Purse (levies, relief, rescues), the Fleet (wars, commands, recalls, triumphs,
peace) and the Laws (censures, recognitions, Acts, embargoes, break-ups). Every house of the Reach sits on one: the
one that fits its head's own service, unless the head takes a place on another. Moving puts a senator at the bottom
of the new table, and cannot be done again for five years.

**A fourth way to spend the year: the committee's work.** It raises the senator's standing with the committee by a
point (ten at most), teaches its business (economics, logistics or law), and does the Reach a little good in the
committee's own line: a little more of the levy reaches the capital's purse and the merchants think better of the
house; the Reach's own fleets are readier and the officers think better of it; or the magistrates' order thinks
better of it and the Reach's trust in its magistrates is mended a point.

**What standing does.**

| Standing | |
|---|---|
| 3 | On the committee's own business, the other houses of the committee lean one toward what the senator's house moves. |
| 6, and the highest on the committee | The chair. They lean two. Each year of the committee's work from the chair gives a point of influence, and the house is heard a point more in the committee's sphere. |

A year not attended costs a point of standing. Reading the accounts, speaking for a region or working the bench is
attendance, and leaves standing where it was. Another house's standing is reckoned from its head's rank and how much
the house is heard in the committee's sphere, so a chair has to be taken from someone.

**The record.** The years a senator sat and the years they missed are kept. Once there are five of them, one who
was there four years in five has 2 votes for it at any count, for an office of the ladder or for the consulship; one
who was there two years in five or fewer loses 2.

Left, and mapped: a committee has no business of its own to decide, only work to be done; and only the player's
house has a standing that is earned (L12).

## The facts of the year's business

Built 2026-11-15 (docs/GAPS.md L5). The work of an elected office was one decision from three with nothing behind
it: a case said only who was in the right, the accounts had no figures, the works were one sum.

**A praetor's case** now has three facts, and each changes what a ruling does.

- **The matter**: a boundary, a debt, a contract not honoured, a contested inheritance, the salvage of a wreck.
- **The stake**: 500, 1,000 or 1,500 credits. It is what the loser pays, and twice it is what an overturned ruling pays back.
- **How plain the law is**: plain (four cases in ten), arguable (four), doubtful (two).

| A ruling against the law | Costs the Reach's trust | On the record, and open to appeal |
|---|---|---|
| where the law is plain | 4 | yes |
| where it is arguable | 3 | yes |
| where it is doubtful | 1 | no: a ruling either way can be defended |

And a praetor has a fourth thing to do: **bring them to terms**. The house the law leans to takes half the stake.
Where the law is doubtful this is what an honest court does: both houses think the better of the praetor's, and the
magistrates are trusted a point more. Where the law is plain, the house that had it is short-changed and thinks the
worse of the praetor's house; the other is grateful. An upright praetor of another house settles a doubtful case
and rules on the law in the rest.

**A quaestor's accounts** show what is over: 400 to 1,200 credits, never more than the capital's purse holds, and where
it came from (dues, fines, a levy overcollected, an estate fallen to the Reach). That figure is what the quaestor
has to dispose of. Sent to a lawless region, it makes the lanes safer by half of it (2 to 6), where it was always
2. Taken, it is what the house gets and what the capital's purse loses, where it was always 800; and a sum of 900 or
more is likelier noticed (one time in three, not four).

**An aedile** can give lanes and grain both, for twice the sum: both effects, and it is remembered as a generous
year (3 standing). A proud or ambitious aedile of a rich house does.

**Measured, and corrected.** As first built these raised the Reach's trust in its magistrates too far: over sixty
runs it averaged 61 to 70 and was never low in any of them, where it had been 54 to 63 and low in six. Three numbers
were wrong: an arguable ruling against the law cost 2 where every such ruling used to cost 3; lanes and grain both
gave 2 trust, which is the kind of gain that is not capped at sixty; and (from the courts, the pass before) every
conviction gave 2, a dozen times a run. A ruling against the law now costs 4, 3 or 1, which averages what it did
before; lanes and grain both, and a conviction, mend a poor name by a point and do not gild a good one. Measured
again on three starts: 62, 64 and 53 on average, and low in five of ten runs on the last. And on the full probe of sixty runs: 53 to 64 on average
by start, and low at some time in 8 of the 60.

Left, and mapped: the facts are a few figures drawn by lot, not found out. Nobody gives evidence, and a quaestor
cannot be deceived about the accounts or deceive anyone (L11).

## The courts: what a magistrate did is answered for

Built 2026-11-15 (docs/GAPS.md L2). Until now a magistrate who took money either had it come out that year, and was
removed, or got away with it for ever: the year's business was thrown away with the year.

**The record.** A bent act in office is written down and kept eight years: money taken from the accounts, a gift
taken to rule, a ruling against the law for a friend. One that came out at the time, and cost the magistrate the
seat, is marked as answered.

**A magistrate cannot be touched while they sit.** When they no longer hold the office they did it in, the head of
any house of the Reach may bring a charge: 2 influence, one charge a year from a house, and each act is heard once
whichever way it goes. It comes before the longest-sitting praetor who is of neither house. If there is none, nobody
can hear it.

**The verdict is that praetor's**, and the panel says beforehand what kind of praetor it is:

| The praetor | Finds |
|---|---|
| Upright (honourable, or just) | Guilty. It is on the record. |
| Bought by one of the two houses | For whoever paid. |
| Of a house that thinks 30 or better of the accused's | Not guilty. |
| Known to be for sale (ruthless, cruel, or set on a fortune) | For the richer of the two houses. |
| None of these | By who pleads better: 40 in a hundred, 6 more for each point of law and oratory the accuser's best advocate has, 4 fewer for each the accused's has. |

**Proved:** the accused may not stand for five years and loses any seat they hold; their house is under a scandal
(its candidates lose votes for ten years), loses 4 standing and pays 1,000 credits, to the house that was wronged if
there was one and to the capital's if not. The house that brought it gains 2 standing and the commons' regard, and
the Reach's trust in its magistrates is mended a point if it is under sixty. **Let go:** the house that brought it loses a point of standing, and
if everyone can see why they were let go, trust falls a point. Either way the accused's house does not forgive it
(−25 if proved, −15 if not).

**Other houses do the same.** Once a year, in the whole Reach, one house that thinks −30 or worse of another and
finds something on the record against one of its people brings it. The people of the player's house are not
excepted: one of them convicted is treated exactly as one removed from office, and the events that follow a removal
follow this. Since "The docket" below, a charge against one of the player's house waits a year
before it is heard.

**A praetor can be bought.** 1,500 credits of the head's own, to a sitting praetor of another house. The panel says
what is known of them. The upright refuse and say so from the bench: the house loses 4 standing and is under a
scandal as if one of its own had been removed. One known to be for sale takes it. The rest take it one time in two,
and only from a house they do not think ill of; refusing, they say nothing and think the less of you. A bought
praetor finds for the buyer's house whenever it is before them, in a case or on a charge, for as long as they sit.
Each such ruling in a case comes out one time in three, and then the praetor is removed and the buyer falls too
(5 standing and a scandal). And the buying itself goes on the record against the head who did it, where any house
that hates yours may find it.

Left, and mapped: a trial is one praetor's decision with no evidence led and no appeal from it (L11). A charge against
one of the player's house can now be pleaded or admitted: see "The docket". One against the powerful may be answered by
exile or by crossing the line: see "A charge against the powerful".

## The docket

Built 2026-11-23. The owner's list (docs/PLAYTHROUGH.md, "Hard"): "**A docket**: the suits for and against the house,
with a plea, an advocate, a settlement and a counter-suit; a notice and a defence when you are charged (P50, P101,
P79)." The politician's notes: houses sued him every year and "there is no screen for the case"; he "found no way to
bring a suit against a living house either" (P50); one of his was convicted with "no notice of the charge and no say"
(P101); and he could not "see which cases are coming" (P79).

What stood before: a suit that named the house waited a year and could be settled or pleaded (docs/PLAYTHROUGH.md
12), but nobody had brought it, the house could bring none, and a charge against one of the house was heard the year
it was brought.

### Who brought it

A suit now has a **plaintiff** and a defendant. When the bench draws up the year's cases, the plaintiff is the house
whose head thinks worse of the other. The player's house brings a suit only when its head says so, so a case the
bench draws up that names it is always the other house's. The docket says "your suit against House X" and "House X's
suit against you".

### Bringing a suit

The head may bring a suit against a house it has a **quarrel** with: the two are at war, or the two heads' regard for
each other comes to -30 or worse. This is the same test the bench uses to find its cases. A house you have no
quarrel with cannot be sued, and the refusal says how far short the quarrel falls.

The **ground** is chosen from what the world records between the two houses:

| Ground | The record that gives it |
|---|---|
| A boundary between two estates | Both houses hold a world in the same region. |
| A debt of long standing | One house owes the other's bank. |
| A contract that was not honoured | One house holds a province on a world of the other's. |
| A contested inheritance | Someone living in one house is of the other's blood (born to it, or a parent is of it). |
| The salvage of a wreck | The two are at war, or a battle between them is on the record. |
| The standing quarrel | Always, where there is a quarrel: one of the five matters, drawn for the two houses and the year. |

Each ground has its own facts, drawn exactly as the bench's cases are: what turns on it, how plain the law is, and
which house it favours. Before the suit is brought the screen says over what, for how much, how the law leans (plain,
arguable or doubtful; with you or against you), which praetor would hear it and what is known of them. The praetor is
one of neither house: the one with fewest suits waiting, then the longest sitting.

Bringing it costs a filing fee and clout, and the other head thinks the worse of you. It is entered at the turn of
the year and **heard at the end of the year after**, exactly as a suit the bench draws up is: by the same praetor's
choice, with the same pleading, the same settlement out of court and the same appeal of a ruling against the plain
law. The loser pays what turns on it. The fee goes to the purse of the Reach (the house that holds the capital).

### The counter-suit

A house that is sued may counter-sue **once**, on the same matter. As the courts stood, the loser of a suit already
paid the winner, so a second claim for the same money would have added nothing. What the counter-suit adds is this:
**what turns on the suit is doubled, both ways.** Whoever loses pays twice the stake; terms out of court become half
of the doubled sum, which is the whole of the old one; and the house that brought the suit can no longer get out of
it for half of what it asked. It costs the same filing fee and 1 clout, and the plaintiff's head takes it ill. It is
refused when the law, read plainly, is against you.

### A charge against one of the house

When another house charges one of the player's house, it is **no longer heard that year**. It waits a year, and the
house is told at once: who charges whom, with what, before which praetor and what is known of them, and what a
conviction would cost. In that year the head may:

- **Send someone to plead** (1 influence, as for a suit). The house's Advocate, if it has one, answers every charge
  without being asked and without the influence.
- **Admit it and pay the fine now.** There is no trial. The fine is paid in full and the house loses less standing
  than a conviction costs; the accused is not put out of office, is not barred, and no scandal is laid on the house's
  candidates. One of the house with a will of their own (not loyal, and ambitious, arrogant or proud) will not have it
  admitted for them: it is their name.
- **Do nothing.** It is heard with nobody to answer it.

It is then heard by the rule of "The courts" above, with two differences. The accused's house is answered for only by
whoever was sent: nobody sent counts as no law and no oratory at all. And a pleader with 8 or more of law and oratory
keeps a praetor who is for sale from selling the verdict in open court: it then goes by who pleads better. Before an
upright praetor the record is enough and pleading moves nothing, which the docket says, so that is when to admit.

If the praetor named no longer sits, whoever would hear it now hears it; if nobody could, the charge lapses, the
house is told, and what was done stays on the record. Charges between two other houses, and charges the player
brings, are heard the year they are brought, as before, unless the accused is one of the powerful: then it is held a
year whoever brings it (see "A charge against the powerful").

### The numbers

| | |
|---|---|
| A quarrel a court will hear | A war, or the two heads' regard for each other coming to -30 or worse |
| What turns on a suit | 500, 1,000 or 1,500 credits, as for the bench's cases |
| The law | Plain 4 times in 10, arguable 4, doubtful 2; with either house evenly |
| Filing fee | A fifth of what turns on it (100, 200 or 300 credits) |
| Clout to bring a suit | 2 |
| The other head's regard | -10 |
| Suits a house may bring | One a year, and one at a time against the same house |
| A counter-suit | The same fee on the stake before it doubles, 1 clout, the plaintiff's head -5; the stake doubles |
| Sending a pleader, to a suit or a charge | 1 influence (unchanged) |
| Admitting a charge | The fine of 1,000 credits in full, -2 legitimacy, the accuser's head -10 |
| A conviction (unchanged) | Up to 1,000 credits, -4 legitimacy, out of office and barred 5 years, votes lost for 10 |
| A pleader a praetor for sale will not sell against | Law and oratory of 8 or more together |
| Proved, before a praetor who goes by the pleading | 40 in a hundred, +6 for each point the accuser's best has, -4 for each the pleader sent has |

### The screen

The Senate window, **Ladder and courts**. The courts panel opens with **The docket**: every pending matter that
touches the house on one list, in this order: suits you brought, suits against you, charges against your people.
Each line says who, over what, how much, which praetor and what is known of them, how the law leans, when it is
heard and who is to plead it, and carries the buttons for what can be done now (come to terms, counter-sue, send
someone to plead, admit). A greyed button says why. Under it, **Bring a suit** lists each house you have a quarrel
with and every ground against it with its facts and its cost. The old "Suits that name your house" is gone (the
docket is that list), and "Against your own house" no longer repeats an act that is under charge. The outliner
tells your own suits from those against you, and lists a charge that waits. The Advocate's seat on the council
counts the charges.

### Other houses

- Their suits against the player's house come, as before, from the bench's cases: one path, one record, one hearing.
  A house that sues yours now pays the same filing fee (if it has it).
- A house the player sues **answers with a counter-suit** at the turn of the year it was brought, when the law leans
  its way and is not doubtful and it has the fee and the clout. The player is told, and may still come to terms.
- Their best pleader answers in every suit and brings every charge, as before.

### Events

Three, in `content/events/docket.json` (text in `content/text/docket.json`), each for a choice the docket's line does
not offer:

- *Your Suit Is Upheld*: take it; send 300 credits back and mend the quarrel (+12); or have the judgment read (+1
  legitimacy, their head -8).
- *Your Suit Is Thrown Out*: pay and let it lie (+4); call on them and say it is ended (-1 legitimacy, +12); or say
  there will be other benches (-8).
- *A Charge Admitted* (when it was one of the house and not the head): walk in beside them (-1 legitimacy, they +10);
  let them carry it (-8); or tell them what it cost (-4).

A suit the house brought raises these and not *The Court Finds for You* or *Against You*, which stay for suits brought
against it. The line under each card names the case and the sum, as it does for those.

### What I chose

Every number here is mine, and a first guess:

- The fee: a fifth of the stake. The clout: 2 to bring, 1 to counter. The regard: -10 to bring, -5 to counter.
- That a suit the house brings is heard a year later than one brought the same day by the bench would be: it is
  entered at the turn of the year, so that the other house has the rest of the year to answer it.
- That a counter-suit doubles the stake rather than adding a second, separate ruling.
- That the counter-suit's fee is reckoned on the stake before it doubles.
- That admitting costs 2 legitimacy against a conviction's 4, the fine in full, and carries no bar and no scandal.
- That nobody sent to a held charge counts as nothing, where a charge heard at once still takes the house's best.
- That 8 of law and oratory, the figure that already keeps a partial praetor to the law in a suit, also keeps one for
  sale from selling a verdict on a charge.
- That a wilful relative refuses to be admitted for.
- The five records that give grounds, and that the standing quarrel is always one.
- That the plaintiff in the bench's cases is whoever thinks worse of the other.
- That a suit whose praetor has died goes to another praetor of neither house, and lapses only if none sits.
- The events' figures (300 credits; +12, +4, -8; +10, -8, -4; 1 legitimacy).

### What is not built

- No appeal by the house against a ruling it lost, no new court and no new office (the task's limits). A ruling
  against the plain law is still appealed by the bench's own rule.
- Other houses do not bring suits beyond the bench's cases, are not held to the clout or to one suit a year (the
  bench gives each praetor one case a year, which is what limits them), and do not sue each other on chosen grounds.
- Other houses do not offer terms: coming to terms is always the player's step, as before.
- A house charged by the player gets no year's notice and cannot admit: charges the player brings, and those between
  other houses, are heard at once.
- A suit cannot be withdrawn except by coming to terms.
- No by-election when a praetor's seat falls empty (P79): with no praetor of a third house a suit cannot be brought
  and a charge lapses, and the screen says so.
- The grounds' facts are drawn, not read from the record that gives the ground: a debt on the record does not make
  the law lean to the lender, and the stake is not the sum owed.
- Random-play QA (`src/Tools/Qa.cs`) is not wired to the new commands; that file is the owner's to change.

### How it is built

- `src/Core/Systems/LadderSystem.Docket.cs`: `Grounds`, `Bench`, `BringRefusal`/`Bring`, `CounterRefusal`/`Counter`,
  `ChargesOn`, `ChargePleadRefusal`/`SendDefender`, `AdmitRefusal`/`Admit`, and the turn of the year (`Answered`,
  `Indict`, `HearCharges`). `LadderSystem.Courts.cs`: `Verdict` and `Prosecute` take the defence that was sent.
- State: `OfficeBusiness.PlaintiffId`, `OfficeBusiness.Countered`; `Indictment` and `World.Indictments`.
- Commands (`src/Core/Commands/HouseholdCommands.cs`): `BringSuitCommand(actorId, houseId, matter, offices)`;
  `SuitCommand(actorId, SuitCommand.Counter, suitId)`; `ChargeCommand(actorId, ChargeCommand.Plead, chargeId, offices,
  pleaderId)` and `ChargeCommand(actorId, ChargeCommand.Admit, chargeId, offices)`.
- `src/Core/Presentation/Docket.cs`: the list the screen draws. `src/Web/Components/CourtsPanel.razor`.
- World flags for content, for a year after the deed: `suit_brought_won`, `suit_brought_lost`, `charge_admitted`.
  Character flags: `sued_by_the_house` (the head of the house that was sued), `admitted_a_charge`.
- `tests/DocketTests.cs` (18).
- Found on the way, and mended: a colony planted in the middle of a game had no provinces until the game was next
  loaded (`ProjectSystem` did not seed them). The changed courts moved one sixty-year run enough to plant such a
  colony, and `ProvinceTests` caught it.

### Checked

- `tools/ci.sh` passes: 1,056 tests, 18 of them the docket's. One plays three sagas of fourteen years bringing a suit
  whenever it may, counter-suing, pleading and admitting, and asserts that no suit the house brought waits past the
  end of the year after it.
- In the browser, The Old Lion: the docket was empty in year 1 and said so; the house had a quarrel with House Oriel
  and two grounds against it (a boundary, and a province Oriel holds on the Lion's March). It brought the first for
  300 credits and 2 clout after the question said what turned on it and how the law leant; in year 2 the line read
  "Your suit against House Oriel, and their counter-suit" with 3,000 credits on it. In year 7 House Cask's suit
  against the house stood beside the house's own against House Ashgrove, and was counter-sued for 200 credits and
  1 clout. No charge was brought against the house in those nine years, so the charge's line was not seen there.

## A charge against the powerful

Built 2026-11-26, from docs/IDEAS_FROM_IMPERATOR.md, entry 2: "A charge brought against a person of great sway (first
guess: 40 or more), or against the head of a house of Legate rank or above, counts as a cause to cross the line in the
year it is heard, as a recall or a censure does. The accused has three answers: stand trial as now; go into exile
before the verdict, so their offices and commands lapse and their purse goes with them to a nation's court beyond the
Reach, until the Senate votes them home; or, if they could cross, cross. An acquittal costs the accuser's house
standing and gives the accused renown. A conviction of someone who is commanding forces the choice: lay down the
command, or cross. A house may also let it be known that a general will be charged the day they lay their command
down, and that counts as a cause too. Other houses' generals answer by sway and nature, as they answer a recall."

What stood before: a charge was heard the year it was brought unless it named one of the player's house, the accused
did nothing but be well or badly represented (docs/GAPS.md L11), a general's only causes were a recall, a triumph
refused and a censure (C7), and nobody was ever exiled (R6).

### Who is powerful

A person of **great sway** (40 or more, the same sway the Military screen shows and the Senate recalls at), or the
**head of a house of Legate rank or above**. Whoever they are, a charge against one of them is held a year: brought
this year, heard at the end of the next, by the rule of "The courts" and "The docket". That is the year in which they
answer it. Other houses' people are answered for by their house's best pleader, as before.

### A cause

From the year such a charge is brought to the year after it is heard, it is a **cause** to cross the line for the
accused's house, beside a recall, a triumph refused and a censure (docs/CAESAR.md, "Crossing the line"). So is a
conviction of the house's general, until the command is laid down, and a threat of a charge (below), for five years
from the day it is made. A cause alone marches nobody: the general still needs four hulls that would follow them and
40 of sway, and another house's general crosses only through all three gates, as at a recall.

### The accused answers

| Answer | What happens |
|---|---|
| **Stand trial** | It is heard at the end of the year by the praetor named, as any charge is. Pleading and admitting work as in "The docket". |
| **Go into exile** | At once. The charge is not heard and the act is answered for. Offices of the Reach and magistracies lapse, and so do commands: the fleets are left without an admiral, a command by service ends, a captaincy is given up. The purse goes with them. They live at the court of the friendliest nation not at war with the Reach (among the far worlds if there is none), until the Senate votes them home. Their house loses 2 legitimacy (4 if it is its head), the house that charged them gains 2, and their house's head thinks 20 the worse of it. |
| **Cross the line** | Only the house's general, and only with the fleet and the name for it (docs/CAESAR.md). The house marches; the Republic's courts do not hear the charge. |

Who answers:

- **You**, for yourself: any of the three, on the courts panel.
- **One of your house who leaves it to you**: you say whether they stand trial or go abroad, and whether the house
  crosses with them if they are its general.
- **A relative with a will of their own** (not loyal, and ambitious, arrogant or proud) answers for themselves at the
  turn of the year, by the rule for other houses. They cannot cross without you.
- **Another house's accused**: one of them who is honourable, just or loyal stands trial. One who is proud, arrogant,
  vengeful or fearful goes abroad if they expect the court to convict: the praetor is upright, bought by the accuser,
  for sale to a richer accuser, or, going by the pleading, would find it proved one time in two or more. Their general
  crosses only as they would at a recall: through all three gates, and wanting it.

### Exile

An exile lives abroad, out of the Reach's politics, until the Senate votes them home. While abroad they may not stand
for office or hold one, hold a command, be named a legate or a captain, bring a charge or a suit, offer a praetor a
sum, or let it be known that anyone will be charged. They keep their purse, their name, their family and their place
in their house.

**A head of house in exile stays its head.** The house's worlds, money, people, fleets and votes in the Senate go on,
and the head runs them by letter. What the house cannot do while its head is abroad is move anything in the Senate
except the head's own return; its votes are still cast. Other houses' heads abroad are under the same rule, so a house
whose head has fled moves no motions until the head is home.

**For you, in exile**, the courts panel says so in so many words. You may still run your house (build, trade, lend,
marry your children, write your will, make war and peace), vote in the Senate and lobby, and move that the Senate
call you home. You may not hold or stand for any office, command, bring anything before the courts, or move any other
motion. If you die abroad your heir succeeds as at any death, at home.

**Coming home.** A new motion, *Return from exile* (3 influence; targets a house and brings home its exile longest
abroad). Its own house leans hard for it; the house that brought the charge leans against; friends and enemies lean
as they are; a return within two years is thought too soon and one after five years long enough; a great name is
thought safer abroad. Other houses move it: the exile's own house after three years abroad, or a house whose head is
a friend (30 or better) of the exile or of their house's head; never the house that charged them. Carried, the exile
comes home and may stand and command again, their house gains 2 legitimacy, and they think 15 the better of the head
who moved it. A head in exile's own house may move it in their name.

### The price of failure

- **An acquittal** costs the house that brought the charge 3 legitimacy on top of the point any charge let go costs (4
  in all, against the 2 a conviction gains it), and the accused gains a deed in the record of their life, which is
  renown (docs/LIFE.md): "charged by House X and acquitted".
- **A conviction of one who commands** orders the command laid down by the end of the year after the verdict. Laid
  down, the fleets are left without an admiral and no command may be held for five years, as after a recall.
  Otherwise the house crosses. Another house's general crosses with the year's generals if they would, and lays it
  down if not. One of your house who leaves it to you lays it down by themselves; a wilful one waits for you. Left
  unanswered to the end of that year, the court's officers take the command and the house loses 3 legitimacy.

### The threat

The head of a house may **let it be known that a general of another house will be charged the day the command is laid
down** (1 clout). It needs something on the record against them that a court would hear, and a house may have one
such word out at a time. The general and their head think 10 the worse of the house that says it. While it stands,
and for five years, it is a cause for the general's house. The act it names is not forgotten while it stands, and the
house that said it does not charge them while they command.

The day the general holds no command, the charge is due: by the end of the year after. Other houses bring it when
they can. If it is never brought, the house that said it loses 3 legitimacy. If the general dies, or another house
charges the same act first, the threat lapses and costs nothing.

**Other houses** let it be known, one a year in the whole Reach: a house whose head thinks -30 or worse of another
(the same hate that brings a charge), about one of the powerful of that house who commands and has something on the
record, the one of greatest sway first. A house would rather not charge a powerful commander while the fleet is theirs.

### The numbers

| | |
|---|---|
| Powerful | Sway 40 or more, or the head of a house of Legate rank (3) or above |
| Held | Every charge against the powerful: brought this year, heard at the end of the next |
| A cause | From the year brought to the year after it is heard; a conviction until the command is laid down; a threat for 5 years |
| Exile | The house -2 legitimacy (-4 for its head), the accuser +2, the house's head -20 toward the accuser |
| An acquittal | The accuser -4 legitimacy in all (-1 as before, and -3); a deed of renown for the accused |
| A conviction in command | Laid down by the end of the next year; no command for 5 years; taken, -3 legitimacy |
| Return from exile | 3 influence; carried, +2 legitimacy and the exile +15 toward the mover; moved by others after 3 years |
| Its lean | Own house +4; the accuser -3; friend 30 or better +2, 10 or better +1, enemy -20 or worse -2; within 2 years -1, after 5 years +1; sway 45 or more -1 (not for the Sword) |
| A threat | 1 clout; the general and their head -10; empty, -3 legitimacy; due by the end of the year after the command is laid down |

### The screen

The Senate window, **Ladder and courts**, under the docket: **A charge against the powerful**. Each such charge that
touches the house, against your people or brought by you, with what it is and the buttons to answer it (Stand trial,
Go into exile or Send them abroad, Cross the line; after a conviction in command, Lay the command down or Cross the
line: `data-charge-answer="trial"`, `"exile"`, `"cross"`, `"lay_down"`). Each threat by your house or against it
(`data-charge-threat` with the threat's id), and the generals of other houses you could threaten, each with **Let it
be known** (`data-charge-threat` with the general's id). Every exile in the Reach, where and since when, with **Move
that they be called home** (`data-recall-exile` with the house's id). If you are in exile, a line at the top says
where, and what you may and may not do. A greyed button's title is the refusal. The Senate's own list of motions
offers *Return from exile* like any other.

### Events

Three, in `content/events/exile.json` (text in `content/text/exile.json`), each clearing the flag it reads:
*The Road Abroad* (you have gone into exile: run the house by letter, live quietly, or make friends at the court that
took you in), *Called Home* (one of the house is voted home: quietly, in state for 500 credits, or remember who
voted), *The Charge Thrown Back* (one of the house's powerful is acquitted: say nothing, have it cried in the squares,
or offer the accuser a hand).

### Measured

`praetoria charges --scenario all --seeds 10` (the six starts that open in the Republic, ten seeds, sixty years, a
passive player), and `praetoria caesar --scenario all --seeds 10` before and after, on the same starts and seeds:

| In 60 runs of 60 years | |
|---|---|
| Charges against the powerful | 183 (of 857 charges in all); 180 against the head of a Legate house or higher, 1 against a person of great sway; none against the player's house (a passive player does nothing a court would hear) |
| Answered by exile | 23 |
| Answered by crossing the line | 0 |
| Stood trial | 146: convicted 75, acquitted 71 (the accuser paid 4 legitimacy each time) |
| Convicted in command, laid it down | 11 |
| Motions to call an exile home | 26 moved, 20 carried; 2 still abroad at the end, both heads of a house |
| Threats of a charge | 99 made: kept 6, empty 1, lapsed 70 (mostly because a third house charged the same act first), 22 standing at the end |
| Marches whose cause was a charge or a threat | 0 |

| The Caesar road, 60 Republic runs | Before | After |
|---|---|---|
| Civil wars | 0 | 0 |
| The Republic fell | 0 | 0 |
| Another house's general passed all three gates | 0 | 0 |
| Triumphs voted, of those moved | 26 of 47 | 31 of 38 |
| Recalls carried, of those moved | 9 of 17 | 8 of 19 |
| Former magistrates charged, convicted | 932, 546 | 857, 496 |

The Caesar rate stays at none in sixty, inside the owner's one game in ten: a charge is one more cause, but another
house's general still marches only through all three gates, and in these runs none passed them. What moved: about one
charge in ten fewer is brought, because a charge against the powerful now waits a year and a house brings one charge
a year, and because a house that has let it be known it will charge a commander waits for the command to be laid
down. The triumphs and recalls move with the different years the runs then have; neither changes direction.

### What I chose

Every number here is mine, and a first guess:

- Sway 40 (the entry's guess, and the sway the Senate recalls at) and Legate rank (3) for the powerful.
- That every such charge is held a year, whoever brings it, so that the accused has a year to answer; before, only a
  charge against the player's house was.
- That the cause runs from the year brought to the year after it is heard; a conviction's until the command is laid
  down; a threat's five years (the Senate's own causes count five).
- Exile: -2 legitimacy to the house (-4 for its head), +2 to the accuser (what a conviction gains it), -20 regard.
- That the act is answered for by the exile, and not heard again when they come home.
- That a head in exile stays the head; that their house moves nothing in the Senate but the return, and still votes.
- That the exile goes to the court of the friendliest nation not at war with the Reach.
- An acquittal: -3 more legitimacy to the accuser; renown as one deed of office.
- That a convicted commander has to the end of the year after the verdict, that a command laid down bars another for
  five years (the recall's figure), and that a command taken costs 3.
- The return: 3 influence, +2 legitimacy, +15 regard; moved by others after 3 years and by friends at 30; its lean's
  figures in the table.
- The threat: 1 clout, -10 regard, -3 when empty, one at a time, due by the end of the year after; that it keeps the
  act on the record; that other houses make it about the powerful only, one a year.
- Who flees: the proud, arrogant, vengeful or fearful (Paranoid), when they expect conviction; never the honourable,
  the just or the loyal.
- The events' figures (500 credits; 300 credits; 1 or 2 legitimacy; a point of influence; stress).

### What is not built

- Renown is kept only in the lives of your own house, as before (docs/LIFE.md): another house's acquitted have no
  record to add it to.
- A charge against the powerful is still a charge for an act in office (docs/LADDER.md, "The courts"). Nothing yet
  charges a general for what was done in command, so a general who never held a magistracy cannot be charged or
  threatened.
- An exile's estate abroad is not modelled: they do not earn, spend or marry at the foreign court, and the nation that
  took them in thinks no differently of the Reach.
- A relative in exile keeps any post in the household (steward, advocate and the rest): those are the house's, not
  the Reach's.
- Under a throne the courts of the Republic do not sit, so nothing is charged or threatened and nobody goes abroad;
  one already in exile when a throne is taken may still be voted home by whatever Senate sits.
- No appeal, no evidence led, and no amnesty for all exiles at once (L11 and R6 keep those).
- Random-play QA (`src/Tools/Qa.cs`) is not wired to the new commands; that file is the owner's to change.

### How it is built

- `src/Core/Systems/ChargeSystem.cs` (an instance, made by the turn with the nations, the offices and the generals):
  `Powerful`, `Cause`, `Brought`/`Heard` (called from the courts), `AnswerRefusal`/`Answer`, `Exile`, `Home`,
  `HomeRefusal`, `HomeLean`, `MoveHome`, `ThreatRefusal`/`Threaten`, `Waits`, `Threatened`, `Tick` (after the generals,
  before the courts: the accused answer, a convicted commander's time runs out, threats are made good or found empty,
  and one house lets one be known).
- State: `Indictment.Powerful`, `Answer`, `HeardTurn`, `Sentence`, `AnswerBy`; `Exile` and `World.Exiles`;
  `ChargeThreat` and `World.ChargeThreats`; the character flag `in_exile`.
- Commands (`src/Core/Commands/ChargeCommands.cs`): `AnswerChargeCommand(actorId, chargeId, answer, charges)` with
  answer `trial`, `exile`, `cross` or `lay_down`; `ThreatenChargeCommand(actorId, generalId, offices)`. The return is
  `ProposeMotionCommand(actorId, "home", houseId, senate)`.
- Small hooks elsewhere: `LadderSystem.Indict` (public now) holds any charge against the powerful, from the courts'
  own year and from `ProsecutionCommand`; `CaesarSystem.Grievance` reads `ChargeSystem.Cause`, and `Barred` counts an
  exile; `SenateSystem` leans on, carries and moves the return, and an exiled head's house may not move another motion;
  the ladder's `Bar`, `OfficeSystem.Candidate`, the admiral's, the captain's and a command by service's refusals, and
  the courts' own refusals, turn an exile away.
- `src/Web/Components/PowerfulCharges.razor`, in `CourtsPanel.razor`.
- `src/Tools/ChargeProbe.cs`: `praetoria charges [--scenario id|all] [--seeds N] [--turns N]`.
- Counters for content: `powerful_charged`, `powerful_tried`, `powerful_convicted`, `powerful_acquitted`,
  `powerful_exiled`, `powerful_crossed`, `powerful_laid_down`, `exiles_home_moved`, `exiles_home`, `charge_threats`,
  `charge_threats_kept`, `charge_threats_empty`, `charge_threats_lapsed`. World flags for a year after the deed:
  `went_into_exile`, `called_home_from_exile`, `charge_thrown_back`.
- `tests/ChargeAgainstThePowerfulTests.cs` (16).

## Where it is

The Senate screen, under **Your standing · the orders and the ladder**: each rung with its seats, age, prerequisite
and pay; who sits; who of the house is standing and what can be done for them; and the consulship's requirement.
The outliner says when the head could stand and an election is within two years.

## Events

Five, in `content/events/ladder.json`: *The Canvass* (while standing), *The Count Goes Your Way*, *The Count Goes
Against You*, *One Of Yours Is Elected* (a relative who stood by their own decision), *The Aedile's Games*.

## For content

- World flags, each year: `standing_for_office`, `is_magistrate`, `is_aedile` (all of the player's own character).
- World flags, for a year after the deed: `ladder_election_held`, `ladder_elected`, `ladder_lost`, `kin_elected`,
  `games_given`.
- Character flags: `newly_elected`, `standing_for_office`.
- No new effect types.

## A rename that came with it

The law career's third and fourth ranks were called Magistrate and Praetor. With an elected praetor in the game that
read as two different things with one name, so the career now runs Aspirant, Advocate, **Judge**, **Justice**, High
Justice. The estate called the Magistrates is unchanged.

## How it is built

- `src/Core/Systems/LadderSystem.cs` (static): `Bar`, `Next`, `Merit`, `Candidates`, `Tally`, `Hold`, `Seed`, `Open`,
  `Stand`, `Back`, `Dissuade`, `Games`, `SenateStanding`, `Tick`.
- State: `Character.Cursus`, `World.Magistrates`, `World.Candidacies`.
- `LadderCommand` (stand, withdraw, back, dissuade, games) in `src/Core/Commands/HouseholdCommands.cs`.
- `OfficeSystem.Candidate` checks a consul's age and praetorship. `SenateSystem.Tick` holds the ladder's election
  before the consuls'.
- `src/Web/Components/LadderPanel.razor`. `tests/LadderTests.cs` (10). `tools/browser/ladder.js`.

## Checked

- 582 tests. One of them runs the Republic for sixty years and asserts that consuls are still found and at least four
  magistrates sit after every election.
- The soak (eleven starts, three seeds, sixty years, with save and load compared) passes, and its random player
  stands, backs, asks relatives to stand down and gives games. All five events fired.
- In the browser, The Risen Officer stood for quaestor in the first year, called in what was owed, was elected at 30
  with the highest count, and it was written into the record.

## Not in this piece

- Other houses' people do not canvass, back each other or give games; their candidates are chosen by a rule.
- The facts of a year's business are drawn by lot, not found out: nobody gives evidence, and a trial is still one
  decision (docs/GAPS.md L11).
- Other houses' senators do not spend their year (but for the one who audits), and their judges hear no hearings
  (docs/GAPS.md X2).
- An office's name and a region's belief are each one figure, moved by whatever was done: a region does not tell a
  court that wronged it from a quaestor who robbed it, and nobody can set out to mend a name (docs/GAPS.md L13).
- The chair acts on one motion a year, and the budget is three choices read against figures. The Treasurer does not
  set each head of the budget, the consuls do not summon or dismiss the Senate, and nothing is debated (docs/GAPS.md
  O3).
- No bribery, no prosecution of a former magistrate, no law on the order of offices that the Senate could change.
- The censorship, the tribunate and provincial commands after office are not here.
- The numbers (seats, ages, votes for backing, the weight of merit) are first guesses.
