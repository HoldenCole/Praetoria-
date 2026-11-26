# Ideas from Crusader Kings III mods

The owner asked what Crusader Kings III mods could add to the base game. About two hundred mods were read, each on its
own Steam Workshop page, or on a top-mods.com or mirror copy when Steam turned the reader away. They came from the
Workshop's most subscribed, top rated and most popular this year lists, and from best-mods lists (PC Gamer, TheGamer,
Wargamer, After Strategy, Gamers Decide, Stealth Optional). Subscriber counts are Steam's own public figures on
2026-11-26. Every mechanic was then checked against this game's documents and code, so each entry says what the game
already has. The rule: **we take mechanics and ideas only, never a mod's names, art, text, characters or events.**
Anything here would be built new, in the Reach, in our own words and pictures.

How the list is ordered: what adds most to play for its size comes first, and so does what fits one person rather
than a house (docs/PROCESS.md). For now the owner's rule is to close and deepen, not to open new systems, so an item
that closes a row on the worklist in docs/GAPS.md ranks higher, and each entry names the row it closes. Sizes follow
docs/GAPS.md: **small** is one change, **medium** is a cycle, **large** is several cycles and needs a plan first.

## What is worth adding, best first

### 1. A command by service

**What it is.** The player, or anyone of their house, can be given command of a fleet that is not their house's own: as a
legate under a consul, the Admiral of the Reach or the holder of emergency powers, or as an officer lent to an ally
for that ally's war. The victories, the valour and the crews' devotion are theirs. The hulls stay the owner's, and the
owner or the Senate can relieve them.

**From.** [War Lords](https://steamcommunity.com/sharedfiles/filedetails/?id=3454418002) (about 20,000 subscribers:
the player may lead their liege's armies if trusted);
[Life of an Adventurer](https://steamcommunity.com/sharedfiles/filedetails/?id=3514059618) (about 18,500: contracts to
protect a domain); [Oathbound](https://steamcommunity.com/sharedfiles/filedetails/?id=3457401824) (about 41,000: a
lifelong oath to a ruler who calls you to their wars);
[A Game of Thrones](https://steamcommunity.com/sharedfiles/filedetails/?id=2962333032) (about 666,000: borrowing a
vassal's person for the council).

**The game now.** Part. Office fleets sail under whoever holds the office (docs/SENATE.md, the offices), the Senate's
sword takes every hull for a war, and a levy squadron sails under one of the sending house's people (GAPS P12,
closed). But a fleet's commander must be of the owning house (AssignAdmiralCommand and FleetSystem.CaptainRefusal), and
GAPS R1 and Q15 say nobody rises by service and a house without land never gets a fleet: in 11 of 24 games the
general never commanded one.

**Size.** Medium. It extends the levy squadron and office-fleet rules; the one rule to change, that a commander is of
the owning house, is read in several places (devotion, the Caesar road's sway, the war ledger).

**Why.** It is first on the worklist (Q15) and in its fifth row (R1). It is the one-person rule exactly: a younger son
with no ships climbs by serving, as Roman legates did and as Caesar did under Thermus in Asia. A legate whose crews
come to love them is how a Caesar starts, so keep the gates in docs/CAESAR.md and measure the one-in-ten rate.

### 2. The heir inherits the old head's friends and enemies

**What it is.** When you play on as the heir, the dead head's old friends think better of you (+20, fading over ten
years) and the dead head's enemies think worse. A Vengeful enemy is your enemy at once; a Just or Loyal one lets it go. Only a
grown heir takes on friendships, since a child was never seen at the parent's side.

**From.** [Inheritable Relations](https://steamcommunity.com/sharedfiles/filedetails/?id=2829009590) (about 72,000);
[Social Relations Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2825977309) (about 91,000).

**The game now.** Part. DynastySystem.HandOver only sours grown brothers and sisters who were passed over (-20).
Another house's new head starts with half of what the old one thought of you (docs/HOUSEHOLD.md, "Who leads another
house"), and old friends are kept person by person (docs/LIFE.md, "Old friends and old enemies"). Your own heir
inherits none of them.

**Size.** Small.

**Why.** The cheapest thing on this page that changes every succession. A new reign stops being a clean slate: the
father's enemies come for the son and the mother's friends stand by the daughter. Use the same rule for other houses'
new heads in place of the half-regard rule, not on top of it.

### 3. Strike from the line, cast out, and a will that can be challenged

**What it is.** The head can strike a grown relative out of the succession, or cast them out of the house, each at a
cost in legitimacy and the kin's regard unless the one struck had defied, deserted or shamed the house. Either can be
undone at the same price. At the head's death, a child struck out with no recorded cause may sue before a praetor that
the will was undutiful, and a child who wins takes a share.

**From.** [Love Marriage Family](https://steamcommunity.com/sharedfiles/filedetails/?id=3037969445) (about 95,000:
the disinherited gain claims and can demand their place back);
[Inheritance Doctrine: Concubine's Children](https://steamcommunity.com/sharedfiles/filedetails/?id=2976374352)
(about 1,700: disinherit and restore for prestige);
[Adoption Options](https://steamcommunity.com/sharedfiles/filedetails/?id=2816662627) (about 19,000).

**The game now.** None. GAPS R18: "Nobody can be disowned." The nearest are a named heir (docs/AMBITIONS.md, "A named
heir", for 2 legitimacy) and a will that leaves the purse and heirlooms elsewhere (docs/WEALTH.md).
WealthSystem.Inherit marks will_contested but only costs regard, and the courts hear suits between houses, never
within one (docs/LADDER.md, "The courts").

**Size.** Medium.

**Why.** On the worklist (R18). It is influence over kin, not control: the one struck out keeps a grievance and can
strike back (join a rival, marry against you, sue). Rome had both sides of it: disinheritance by name, and the suit of
the undutiful will.

### 4. Sides that can be won in a civil war, and the kingmaker's price

**What it is.** When a general marches on the capital or a pretender rises, every house weighs its side again each
year, by how the war goes, whom it owes and what it is promised. The player can go to a head with coin, a favour, a
match or the promise of an office or a world, to bring the house in, keep it out or turn it, and what was promised is
owed if the side wins. A house that changes sides is marked by both. Houses of an honest nature can swear beforehand
to defend the Senate, which makes a rival Caesar rarer.

**From.** [More Interactive Vassals](https://steamcommunity.com/sharedfiles/filedetails/?id=2712590542) (about
225,000, the most subscribed mechanics mod read: sides chosen, bought and switched in a civil war, oathbreakers
punished after it, a Crown Loyalist faction);
[Loyal To A Fault](https://steamcommunity.com/sharedfiles/filedetails/?id=2776145076) (about 14,000);
[Vassals To Arms](https://steamcommunity.com/sharedfiles/filedetails/?id=2535753142) (about 27,000);
[Shogunate](https://steamcommunity.com/sharedfiles/filedetails/?id=2253278582) (about 88,000: two sides on the map,
and members change sides by decision).

**The game now.** Part. The houses declare once, by regard, party, patronage and fear (docs/CAESAR.md, "The houses
declare"; CaesarSystem's sides), and nothing during the war changes a side. GAPS R16: a player cannot back another
house's bid for a price named beforehand, and nothing is given to those who backed the winner. GAPS C4: no amnesty or
negotiated end. Promises already exist in breaking away (docs/BREAKAWAY.md).

**Size.** Medium.

**Why.** On the worklist (R16). The Roman civil wars were this: Labienus leaving Caesar in 49 BC, Antony's ships going
over at Actium. It makes the player the kingmaker, which is a role for one person. Limit each house to one change of
side per war, and measure the Caesar rate whenever it changes.

### 5. A death that can be contested

**What it is.** While a ruler lives, the game says how ready the heir is (age, skills, renown, standing, what the
family thinks of them) against the strongest rival. When an emperor dies with a weak heir, the houses of rank meet and
weigh the heir against up to two claimants by their seats; only if that fails does it come to war. An emperor can raise
their heir as co-ruler beforehand, as Augustus did Tiberius. In a house, a passed-over relative who leads a side in a
quarrel can call a family council at the head's death; if the rival wins, the player plays on as the rival.

**From.** [Succession Crisis](https://steamcommunity.com/sharedfiles/filedetails/?id=3713902872) (about 28,000: a
crisis meter, claimants, a weighted great council);
[Succession Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2650385452) (about 98,000: co-rulers to
secure the succession); [Regency Rework](https://steamcommunity.com/sharedfiles/filedetails/?id=3582007365) (about
17,500).

**The game now.** Part. docs/THRONE.md: stage 2, a contested succession, is planned and not built ("When an emperor
dies the heir inherits, and nobody disputes it"). In a house, DynastySystem.Succeed passes the seat at once, and
passed-over siblings get the passed_over flag and -20 regard. Four succession laws (primogeniture, ultimogeniture,
seniority, merit, in DynastySystem.cs) and a named heir already exist.

**Size.** Medium.

**Why.** Throne stage 2 is the fourth row of the worklist (R5). A ruler's death becomes the tense moment it should be,
with warning and time to act: marry the heir well, give them a command, settle the quarrel. The player can be the
heir, a claimant, or the one whose seats decide it. In a house it should be rare, and the head should always be able
to prevent it by naming an heir and settling quarrels.

### 6. A guardian who is a person

**What it is.** When a head of house is a child, the guardian named in the will (a relative, the spouse, or a friend
of another house; if none, the eldest) does the house's yearly business by their own skill and nature until sixteen.
A war, a sale or the child's marriage needs their consent. An honest guardian keeps the house; a greedy one skims; an
ambitious one may promise the child to their own kin or be slow to let go. At sixteen the accounts are read, and what
was taken can be sued for. The same person can stand regent for a child emperor.

**From.** [Dynamic Regency](https://steamcommunity.com/sharedfiles/filedetails/?id=3096490417) (about 36,000: the
regent's grip follows real power and gathers momentum);
[Regency Rework](https://steamcommunity.com/sharedfiles/filedetails/?id=3582007365);
[Dynamic Diarchies](https://steamcommunity.com/sharedfiles/filedetails/?id=3842152116) (new, about 200); Regency &
Ruler Training (read only on a [mirror](https://catalogue.smods.ru/?p=140094): a regent council and a training
challenge every two years).

**The game now.** Part. DynastySystem.UpdateRegency raises a regency flag and marks the eldest grown relative, and its
own comment says "the regent is not yet a bound character". One event reads the flag (content/events/household.json).
docs/THRONE.md's stage 2 also asks for "a regent who is a person".

**Size.** Medium.

**Why.** A child heir is the most dramatic moment a dynasty game has, and Roman guardianship, with the ward's suit
against the guardian, is ready-made. The player still plays the child, so the child's own acts must stay real; the
guardian's consent is the price. The steward's honesty rule and the courts already exist to lean on.

### 7. A child's nature comes from the people who raise them

**What it is.** At 6, 10 and 15 a child of the house may take a nature, and the draw leans toward the natures of the
father, the mother, the tutor, a mentor, and the head if the child thinks well of the head. A cold marriage at home or
a course against the child's bent leans toward Proud, Vengeful or Ruthless; a warm home and a fitting course toward
Loyal or Just. Each step is a short scene where the head can step in. A second, smaller step: a nature comes in a
mild, a plain and a strong form (Touchy, Proud, Imperious), set at sixteen, and the stress of acting against it
scales with the strength.

**From.** [Improved Personality Formation System](https://steamcommunity.com/sharedfiles/filedetails/?id=2488151099)
(about 13,000); [Houses Traditions](https://steamcommunity.com/sharedfiles/filedetails/?id=2590650832) (about 132,000:
role models, and the educator teaching their own house's values);
[More Personality Depth](https://steamcommunity.com/sharedfiles/filedetails/?id=3717989134) (about 39,500, top rated
this year: three strengths of every trait);
[New Personality Events for Children](https://steamcommunity.com/sharedfiles/filedetails/?id=2779836155) (about
32,000).

**The game now.** None. docs/EDUCATION.md, "Not in this piece": "no nature is formed by an upbringing" (GAPS E3).
Children have a bent, a course and a tutor, and what they think of the head follows how they are raised, but their
natures are thrown by the dice. Every nature has one strength (content/traits/traits.json, friction.json).

**Size.** Medium (the three strengths are a small step after it).

**Why.** Choosing a spouse and a tutor becomes a choice about the next generation, which is the heart of a dynasty
game, and the result shows ten years later. It obeys the one-person rule: the head steers only through whom they
marry, whom they hire and how they treat the child, and the child does the rest. Throw the same number of dice as now
so nothing else in a saga moves, and check what the shift does to other houses' behaviour, which reads natures.

### 8. Taken alive

**What it is.** A beaten admiral or captain, or a governor and their family at a fallen world, may be taken instead of
killed. The holding house can ransom them, trade them for one of its own, release them for goodwill, keep them as
surety, or name their return as a term of the peace. Their house can pay, refuse (a mark against its head), plot a breakout or go
to war to free them. A beaten or client house's child can be given as a hostage and brought up among your wards. If the
player is taken, they still play: they bargain, write letters and try to escape while the steward runs the house.

**From.** [Prisoners of War](https://steamcommunity.com/sharedfiles/filedetails/?id=2511247871) (about 49,000:
captives stay with the commander until they reach friendly land, and can be freed on the way);
[Rescue & Vengeance](https://steamcommunity.com/sharedfiles/filedetails/?id=3089046758) (about 57,000: a war to free a
named captive); [Prisoners and Jailors](https://steamcommunity.com/sharedfiles/filedetails/?id=3161565481) (about
10,000: ransom, sell and trade prisoners);
[Bail Out Anyone](https://steamcommunity.com/sharedfiles/filedetails/?id=2253837157) (about 5,400);
[Battle Captives](https://steamcommunity.com/sharedfiles/filedetails/?id=2217257991) (about 2,500);
[Sane Warfare](https://steamcommunity.com/sharedfiles/filedetails/?id=2222896774) (about 1,100: knights captured more
"so ransom money matters").

**The game now.** None. GAPS R3: "Nobody is taken prisoner." GAPS H2: wards are not hostages; N22: no hostage among the
terms of a peace. FleetSystem.AdmiralOutcomes only wounds or kills, and FleetSystem.Conquer takes a world and its
plunder, never its people. The only ransom in the game buys back a city of the Free Cities (docs/BEYOND.md).

**Size.** Large.

**Why.** A battle becomes a story about named people: your brother is in their keeping, their heir is in yours. Ransom
is a second prize of war and gives a beaten house a reason to make peace. Caesar was taken by pirates in 75 BC and
ransomed. Keep capture rare enough that heirs are not lost every war, and let killing a captive start a blood feud
(docs/FEUD.md).

### 9. A war has an aim

**What it is.** Declaring war names what it is for, with the grounds shown in plain words: take back a world that was
yours, blood for blood, collect a debt in default, enforce a court's judgment, press a claim, make them a client, or
take a world with no grounds at all. The aim sets what declaring costs, how the benches lean if someone moves the
Peace of the Reach, who will come if called, and what the winner gets.

**From.** [Casus Belli - Demand Religious Conversion](https://steamcommunity.com/sharedfiles/filedetails/?id=2588989885)
(about 7,000: an aim that takes no land);
[Vassals To Arms](https://steamcommunity.com/sharedfiles/filedetails/?id=2535753142) (the attacker counts who would
join before declaring); [Sane Warfare](https://steamcommunity.com/sharedfiles/filedetails/?id=2222896774) (the AI
pursues war objectives).

**The game now.** None. DeclareWarCommand (src/Core/Commands/WarCommands.cs) costs influence and the target's regard,
with no reason given. HousePeace.cs offers the same terms in every war: nothing, tribute by the lead, or a world. GAPS
B17: such a war "has no aim, no terms of its own".

**Size.** Medium.

**Why.** The grounds already exist in the game (a former owner on a holding, a feud, a loan in default, a suit won, a
claim from a conquest), so this ties them together. Other houses' wars then read as stories ("Oriel goes to war to take
back what it lost"), and a war with no grounds can cost standing at once and bring the Senate's peace sooner. Start
with five or six aims.

### 10. Regard that remembers why, shown under every face

**What it is.** Each deed that moves what someone thinks of you is kept as a dated line that fades ("You refused to
name her heir, year 31: -15"; "You sent your physicians, year 40: +8"). A person's page lists the three that weigh
most, and every face in a report says who they are to you, their office and what they think of you ("your niece,
praetor, thinks +32 of you"), with the reasons on hover.

**From.** [Social Relations Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2825977309) (about
91,000); [Nameplates](https://steamcommunity.com/sharedfiles/filedetails/?id=2220762808) (about 365,000 before the
base game built the same thing);
[In My Humble Opinion](https://steamcommunity.com/sharedfiles/filedetails/?id=2625307328) (about 57,000).

**The game now.** Part. src/Core/State/Relationship.cs keeps one number per pair, and ProfileProjection.Reasons() lists
only standing ties (kin, spouse, an old friend, a patron, a war, a bench, a favour owed). GAPS V10: what a person
thinks of you has no itemised breakdown. src/Web/Components/EventWindow.razor shows only a name, a house and a role
under each portrait.

**Size.** Medium.

**Why.** CK3 players read the opinion breakdown all the time: it tells them whom to mend and how. People feel as if
they remember, and every other relationship idea on this page reads better with it. It touches every call that moves
regard, so build it once, carefully, and keep saves from growing without limit.

### 11. Adoption across houses, and adoption in a will

**What it is.** Ask another house's head for one of their younger children as your heir, weighed as a match is; once
agreed, the two heads are bound as by a marriage, and the adopted one keeps a second name from their birth house.
Adopt a ward before sixteen with their family's consent. Or write an adoption into your will: it is read out at the
funeral, the adopted one may refuse, and the blood may contest it in court.

**From.** [Adoption Options](https://steamcommunity.com/sharedfiles/filedetails/?id=2816662627) (about 19,000: why
adoption is allowed and whom you may take, won with hooks, bribes or alliances; it names a Roman legacy that makes
adoption fully lawful).

**The game now.** Part. docs/HOUSEHOLD.md, "Adoption": only a grown person already in the household, ten years
younger, who thinks 20 of the head, for 3 influence. HouseholdSystem.AdoptRefusal turns away anyone outside the
household or under sixteen. There is no adoption by will in docs/WEALTH.md, and GAPS R7: nobody is adopted into the
ruling house.

**Size.** Medium.

**Why.** The most Roman thing a family can do: Scipio Aemilianus between two great houses, and Octavian by Caesar's
will. It lets a failing line save itself by skill, and a will that adopts is a twist at a death. Keep the birth
house's consent and the cost of passing over blood heirs, so it is not an easy fix.

### 12. Secrets that can be kept and used

**What it is.** The wrongdoings the game already lets you find (a steward's hand in the till, a traced stirring, a
bribe taken in office, a praetor bought) can be kept instead of charged. A secret kept is a hold on its owner: once a
year you name a motion or a pledge at the count, and their house goes your way, until the act ages off the record,
someone else charges it, or they confess and pay the fine. The one held may pay to have it forgotten, dig for
something on you, or send a knife.

**From.** [Sabotage and Subterfuge](https://steamcommunity.com/sharedfiles/filedetails/?id=3592971803) (about 7,800:
spies who find secrets and offer to sell them);
[Whisperers](https://steamcommunity.com/sharedfiles/filedetails/?id=3775073045) (about 2,100: hooks sold to buyers);
[Princes of Darkness](https://steamcommunity.com/sharedfiles/filedetails/?id=2216659254) (about 203,000: secrets kept
or used for blackmail); [Res Publica](https://steamcommunity.com/sharedfiles/filedetails/?id=2478356664) (about
18,000); [Hostile Actions against Blackmailers](https://steamcommunity.com/sharedfiles/filedetails/?id=2255784211)
(about 1,350: the held one strikes back).

**The game now.** Part. Hidden acts do come out: a stirring is traced one time in three (docs/BREAKAWAY.md), a
steward's skimming is found (docs/HOUSEHOLD.md), and a bent act in office stays on the record for eight years and can
be charged (docs/LADDER.md, "The courts"). Nothing can be kept and used. GAPS R2: beyond these, "Nothing is hidden."

**Size.** Medium.

**Why.** Secrets and hooks are CK3's best-loved intrigue, and the Republic ran on them: Cicero and the Allobroges'
letters, the evidence against Verres. It gives the records the game already keeps a second use. Cap it at one hold per
house at a time. Whether a secret can be held over one's own kin is the owner's question in GAPS C2.

### 13. Alerts for what is about to lapse, and people you follow

**What it is.** A "Soon" group in the outliner: a favour that is forgotten next year, a peace whose protected years run
out, a law whose lock ends, a house whose regard has just reached what a petition needs. And a pin on any person or
house that keeps them in the outliner with what they think of you, their office and any favour between you, with a
line when something happens to them (married, widowed, elected, at war, dead).

**From.** [Expanded Alerts](https://steamcommunity.com/sharedfiles/filedetails/?id=2218116577) (about 20,000: truce
ending, hook expiring); [Extended Outliner](https://steamcommunity.com/sharedfiles/filedetails/?id=2219611817) (about
142,000: pinned characters); [War Alerts](https://steamcommunity.com/sharedfiles/filedetails/?id=2256989379) (about
22,000); [Dynasty Member Alerts](https://steamcommunity.com/sharedfiles/filedetails/?id=2837827970) (about 4,500; only
its alerts for what has become possible, not its roster of kin to manage).

**The game now.** Part. src/Web/Components/Outliner.razor builds about 110 kinds of alert and none warns of an ending;
the year a favour is forgotten is a line of text (DealingsSystem.cs). Nothing in src can be pinned.

**Size.** Small.

**Why.** A chance missed through not knowing feels unfair, and one seen in time feels earned. It helps every system at
once. Following someone is not ordering them, so it keeps to the one-person rule.

### 14. The house's roll of ancestors, and a verdict on each life

**What it is.** When one of the house dies, their highest office, their triumphs and any name they earned go on the
house's roll, which never fades. A new name that gathers, say, two ancestors who were consul, or three who were
praetor, becomes an old name, and an event says so. And at each head's death someone who knew them gives a verdict made
from the record: skills against their ceilings, the ambition met or missed, legitimacy and wealth at the start and end
of the headship. If no friend is left, an old enemy speaks and says what the record holds against them. The Dynasty
screen's "The dead" tab becomes the book of the line, one page for every head.

**From.** [Grand Remembrance](https://steamcommunity.com/workshop/filedetails/?id=3678529052) (about 54,000: an
obituary written by a chronicler who knew the ruler, a score of the life, a dynasty chronicle);
[Hiraeth - Dynasty Legacies Overhaul](https://steamcommunity.com/sharedfiles/filedetails/?id=2697392271) (about
47,000: what the dynasty earns by deeds); [Extended Bloodlines](https://steamcommunity.com/sharedfiles/filedetails/?id=2233355253)
(under 800: a named line descendants belong to).

**The game now.** Part. docs/REPUBLIC.md: a house is an old name only if it held Prefect when the game began, and
House.Origin is set once (OrderSystem.cs) and never changes. docs/LIFE.md, "Funerals": what is said is made from the
record, and the last twelve are kept. docs/UPSTART.md section 5 leaves open how a new house could ever overtake the old
names. GAPS V15: a house has no hall. docs/ROADMAP.md section 6: the saga as a book.

**Size.** Medium (the verdict alone is small and can come first).

**Why.** It is Rome's own nobility: a consul among your ancestors, their masks carried at every funeral. It gives an
upstart a goal three generations long, and it makes the dynasty, not the character, the thing that survives. Check
first that other houses' dead have their offices recorded, and tune the marks so that it happens, but not to every
house in forty years.

### 15. Ill fame

**What it is.** Each person carries a bad name from 0 to 100. It rises with deeds the Reach hears of (a broken promise
of marriage, a pledge of votes broken, a field refused, a knife traced, a verdict bought, an affair found out) and falls
a little each year. It counts against them in a courtship, at the count and with Just and Honourable heads, and earns
bad names beside the good ones (the Faithless, the Oathbreaker). A new act, speaking against someone, raises a rival's;
at an election it costs a named candidate votes, and the target can answer it.

**From.** [Schemes Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3494237399) (about 8,600:
tarnish a reputation); [Oathbound](https://steamcommunity.com/sharedfiles/filedetails/?id=3457401824) (an
oathbreaker's lasting mark); [Social Relations Expanded (SRE) - Rework](https://steamcommunity.com/sharedfiles/filedetails/?id=3729520553)
(about 9,800: slanders from rivals); [Res Publica](https://steamcommunity.com/sharedfiles/filedetails/?id=2478356664)
(propaganda and smear campaigns).

**The game now.** None. docs/LIFE.md: "Reputation is only renown. There is no ill fame" (GAPS F1). Renown and eight
good epithets exist (LifeSystem.Epithet). "Stir the camps" can be aimed at one house and sours five heads toward it,
traced one time in three (docs/BREAKAWAY.md), but it feeds the great war's pressure and is about a house, not a
person.

**Size.** Medium.

**Why.** A choice gets a lasting price that belongs to the person, apart from the house's legitimacy and the
magistrates' public trust. A cruel or faithless life plays differently from an honest one, and Roman politics is full
of it (Cicero against Piso and Antony, the election handbook of 64 BC). It is also the cheap form of the rumour
campaigns the court mods offer.

### 16. Raids between houses

**What it is.** An order on one fleet: raid a world of a house you are at war with. If no enemy fleet is over it when
the year turns, it carries off a share of the yield and some plunder, may sack a venture, adds unrest, and heads home
without laying siege. It counts for less in the war score than taking the world, the raided head remembers who
commanded, and the bill for what was sacked is presented at the peace.

**From.** [Immersive Mercs & Raiders](https://steamcommunity.com/sharedfiles/filedetails/?id=3461530706) (about
31,500: a raid open to every kind of ruler, and the choice to pursue raiders or let them go);
[Let's Raid](https://steamcommunity.com/sharedfiles/filedetails/?id=3177335917) (about 10,000);
[Pillage](https://steamcommunity.com/sharedfiles/filedetails/?id=3027931283) (about 20,000).

**The game now.** Part. FleetSystem.HowToTake tells the player: "There is no separate raid or blockade between houses:
the siege is both." Houses sack nations' worlds (docs/BEYOND.md), and GAPS N21: raiding is one order for all the
player's fleets. WealthSystem.Plunder, VentureSystem.Sack and the bill for a blockade (VentureSystem.SettleClaims) are
all built.

**Size.** Medium.

**Why.** A small house can hurt a richer one without the hulls for a siege, and war becomes moving and striking, not
only sitting over worlds. The parts are built; this is one order that uses them, and it closes N21. Give the raided
house a fair chance to catch the raider.

### 17. What the taker does with a fallen world, and sieges that wear and damage

**What it is.** When a siege ends, whoever commanded chooses: hold it whole (no plunder, half the unrest, remembered for
mercy), sack it (today's plunder, plus people lost and long anger), or raze its works (pulled down so the enemy cannot
use them, which the Senate may charge). While a siege lasts, the defender's edge falls a step each year, the besieger
can storm the walls at a cost in hulls, a long siege may damage a building, and a defender can burn its own stores so
the besieger cannot live there.

**From.** [Sinews of War](https://steamcommunity.com/sharedfiles/filedetails/?id=2566883856) (about 30,000: occupy,
sack or raze); [Pillage](https://steamcommunity.com/sharedfiles/filedetails/?id=3027931283);
[Siege devastation](https://steamcommunity.com/sharedfiles/filedetails/?id=3239132682) (about 1,100: a siege camp that
wears the defender down, scorched earth);
[Better Battles](https://steamcommunity.com/sharedfiles/filedetails/?id=3101905160) (about 54,000: choices while running
a siege).

**The game now.** Part. FleetSystem.Conquer does everything at once with no choice: the owner changes, plunder is taken,
some ventures are sacked, unrest rises. A siege takes a fixed number of years set by the world's defence
(FleetSystem.SiegeRequired), and there is no assault. GAPS U4: infrastructure is never damaged by a siege; S1: a siege
takes the world and everything round it at once. docs/CITIES.md: a city cannot be sacked.

**Size.** Medium.

**Why.** Conquest gets a face, and the choice goes on the commander's own record: Carthage and Corinth were razed in
146 BC. Other houses choose by their head's nature, and rarely, so rival wars do not ruin the rim. It closes U4 and
part of S1.

### 18. The demand before an invasion

**What it is.** A year before a nation strikes, its envoy brings a demand to the Senate and to the houses within its
reach: tribute, a march world, an oath. The Senate can pay or refuse. A single house can make its own terms and be
spared, and risks being censured for it. Those who refuse are invaded, and the year of warning lets the houses gather
at the march.

**From.** [Historic Invasions](https://steamcommunity.com/sharedfiles/filedetails/?id=2837398670) (about 88,000: the
invader demands submission first and fights only those who refuse);
[Sunset Invasion](https://steamcommunity.com/sharedfiles/filedetails/?id=2216735054) (about 6,200).

**The game now.** Part. Nations attack (docs/BEYOND.md, "A nation can attack: the great ruler"), but GAPS N13: nothing
warns of the Concord's strike, and N25: nothing leads up to a great ruler, "no demand sent first". The Reach can
already pay tribute (docs/BEYOND.md).

**Size.** Medium.

**Why.** An invasion becomes a crisis people can see coming: a year to prepare, a debate in the Senate, a choice
between honour and safety. It closes two small gaps. Whether one house may bow to a nation turns on the owner's
decision 20 (GAPS N26).

### 19. Allies called to a private war, and compacts that bind

**What it is.** In a war, the head can call houses that have a reason to come: an old friend, a house bound by a
marriage, a client, a fellow of the bench, a house that owes a favour, and any house with a province on a world under
attack. Each answers by a reckoning shown like the Senate's whip count. Two heads can also swear a compact for ten
years: no war between them, or both come when either is attacked. Every house keeps a public name for keeping its word.
Each call strains the tie, and a refusal costs regard and that name.

**From.** [More Interactive Vassals](https://steamcommunity.com/sharedfiles/filedetails/?id=2712590542) (about
225,000: calls answered by stake, alliance fatigue);
[Vassals To Arms](https://steamcommunity.com/sharedfiles/filedetails/?id=2535753142) (about 27,000: those who would lose
land always join); [Bonds of Diplomacy](https://steamcommunity.com/sharedfiles/filedetails/?id=3821274809) (about 400,
new: pacts, and a public name from Oathbreaker to Trustworthy that sets their price);
[Useful Liege/Vassal Alliances](https://steamcommunity.com/sharedfiles/filedetails/?id=3318068569) (about 1,000);
[Shogunate](https://steamcommunity.com/sharedfiles/filedetails/?id=2253278582) (a call accepted only at high
opinion).

**The game now.** Part. Every private war is between two houses (FleetSystem.DeclareWar; GAPS P4: "no other house of
the world takes a side"). Sides exist only in the great war and a general's march. SeekAllyCommand only warms regard by
10 (src/Core/Commands/ActorCommands.cs). Coalition pressure against a feared house exists (SphereSystem.cs).
docs/ROADMAP.md section 6 lists pacts between great houses as a later idea.

**Size.** Large.

**Why.** Who comes when called is the real test of a marriage or a friendship, and it makes those choices pay. A war
of many houses touches the war score, the peace and the great war's pressure, so damp it with fatigue, refusals and a
cap on compacts.

### 20. Tribunes of the commons

**What it is.** Each election year a few tribunes are chosen from the new names by the commons' regard rather than by
weight in the Senate. A tribune may veto one motion a year, cannot be touched without sacrilege, and can carry a law
straight to the citizens, past the Senate. A player from a new name can rise this way; one from an old name only by
being adopted into a new one, as Clodius was in 59 BC.

**From.** [Res Publica](https://steamcommunity.com/sharedfiles/filedetails/?id=2478356664) (about 18,000: a parliament
with a house of commons beside the patricians);
[More Playable Republics](https://steamcommunity.com/sharedfiles/filedetails/?id=3307222438) (about 1,900);
[The republic Mod](https://steamcommunity.com/sharedfiles/filedetails/?id=3823090869) (abandoned).

**The game now.** None. GAPS L3: "No censorship, tribunate, or province after office." The commons reach the Senate
only by petition (docs/SENATE.md, "Petitions"), and the ladder is quaestor, aedile, praetor, consul (docs/LADDER.md).

**Size.** Large.

**Why.** In a Roman game the tribunate is the road a player will look for first: the Gracchi, Clodius's free corn, the
vetoes of 49 BC. It gives a person without old blood a second road to power. Give the assembly a cooldown and only a
few kinds of law. It is large and new, so the owner should weigh it against the rule to close and deepen.

## Worth weighing, not first

Shorter entries. Each says what it is, where it comes from, what the game has, and its size.

### The family

- **Stress in stages, and a way of coping** (medium). At 40, 70 and 100 a person shows the strain; at a break they take
  up a habit (drink, the gaming tables, temper, work, solitude) that sheds stress faster at a price in health, purse,
  marriage or name. From [Stress Bar for All](https://steamcommunity.com/sharedfiles/filedetails/?id=2790457557),
  [Lifestyles Improved](https://steamcommunity.com/sharedfiles/filedetails/?id=3085100161),
  [Dark Ages](https://steamcommunity.com/sharedfiles/filedetails/?id=2975514448) (wars of conquest cost a
  compassionate ruler stress). Now part: a break at 100 is a flag and one echo (DynastySystem.cs;
  docs/CONTENT_GUIDE.md 4c).
- **Mourning** (small). A near death is felt once, for one or two years, and no other death adds stress meanwhile;
  nobody courts the bereaved; it can be cut short at a cost. From
  [No Death Cascade](https://steamcommunity.com/sharedfiles/filedetails/?id=2852881755) (about 24,000) and Love
  Marriage Family. Now part: only an old friend's death costs stress (LifeSystem.cs); the widowed event offers a year of
  mourning (content/events/courtship.json).
- **What you do to one is felt by their kin and friends** (medium). A deed that moves someone's regard by 8 or more
  moves their spouse, parents, grown children and old friends a quarter as far; two old friends in a house make you a
  friend of that house. From [Social Relations Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2825977309).
  Now part: a killing and a quarrel spread (docs/FEUD.md, docs/AMBITIONS.md), a refusal or a divorce moves one person.
- **A standing between two houses that outlives their heads, and a marriage that ends a feud** (medium). From
  [Dynastic Relations](https://steamcommunity.com/sharedfiles/filedetails/?id=2799368260) (about 2,800). Now part:
  blood debts are held by houses (docs/FEUD.md), but "a marriage cannot end a feud" and nobody mediates (GAPS R19).
  Choose one way for ties to pass on, with item 2.
- **Sworn friendship, and a named rival** (medium). Swear friendship at regard 50 and owe each other help; two people
  who beat each other twice for the same office, command or hand become rivals. From
  [Oathbound](https://steamcommunity.com/sharedfiles/filedetails/?id=3457401824). Now part: the sworn bond is made only
  at the Academy and by taking someone in (HouseholdSystem.cs); GAPS R14.
- **A spouse with a part to play** (small). The head's spouse does one thing a year by their best skill and the
  marriage's warmth; a cold one works for their own house. From
  [Harem Politics](https://steamcommunity.com/sharedfiles/filedetails/?id=2916241472) (only its senior-consort duties)
  and Love Marriage Family. Now none: GAPS M3. It waits on the owner's answer to M4 (where a spouse lives).
- **An attachment outside the marriage** (medium). A person, not a flag, with its own warmth, kept quiet or open, that
  can be found out. From [Love Marriage Family](https://steamcommunity.com/sharedfiles/filedetails/?id=3037969445)
  (lover ratings) and [Family Romance](https://steamcommunity.com/sharedfiles/filedetails/?id=3102171241) (only its
  hidden relationship that can be revealed). Now part: "take_comfort_elsewhere" raises an "unfaithful" flag
  (content/events/courtship.json); GAPS M5. The owner's taste; plain and off-screen.
- **Children outside marriage, acknowledged or not** (medium). A child of an affair is born to the mother's house; the
  father may acknowledge them, the head may make them legitimate for legitimacy and the trueborn's resentment, and one
  never acknowledged is hidden kin who may come asking. From
  [Inheritance Doctrine: Concubine's Children](https://steamcommunity.com/sharedfiles/filedetails/?id=2976374352),
  [ENFP's Event Packs](https://steamcommunity.com/sharedfiles/filedetails/?id=3541565115), More Concubinage and
  Bastardry ([mirror](https://catalogue.smods.ru/archives/305523)). Now none: GAPS M2. Needs the attachment first, and
  the owner's word.
- **A lesser union** (medium). A widowed head lives with a partner of lower standing without a marriage, as Vespasian
  did with Caenis; the children stand behind the lawful ones. One partner only. Now none (Relationship.cs has Blood,
  Sworn and Marriage).
- **Siblings who grow close or apart** (small). Children close in age drift by their natures and by the favour shown,
  so a later quarrel has a root. From Love Marriage Family and ENFP's Event Packs. Now part: nothing happens between
  children before sixteen.
- **A wedding with guests, and the house's table** (small). A wedding has a size and a cost; in-laws form a first
  regard; once in three years the head may hold the house's table to settle one tension. From
  [Weddings Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2811048141) (about 30,000) and
  [Relationships of Weddings Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3054030477). Now part:
  "the_wedding_day" has three choices; the master of ceremonies adds 1 legitimacy (docs/COURT.md).
- **Children take their parents' gifts** (small). A child's bent in each skill is the average of the parents' plus a
  small throw, and a constitution bent moves the age at which health fails. From
  [Inheritable Traits++](https://steamcommunity.com/sharedfiles/filedetails/?id=2588849555) (only the idea that gifts
  run in families). Now part: EducationSystem.Bent is a hash of the person's own id; Lineage.cs passes down looks only.
- **How united the house is** (small). One figure made from what the grown think of the head, less open quarrels and
  defectors, read only where a threshold already exists. From
  [House Unity Balanced](https://steamcommunity.com/sharedfiles/filedetails/?id=3122203007). Now part: the pieces
  exist (docs/AMBITIONS.md, FamilySystem.cs) and nothing adds them up.
- **A house's way, and the traditions it earns** (large). The founder sets a way (duty, gain, glory, learning, family)
  that favours three natures, with a black sheep who has none of them; and the house earns traditions by deeds (two
  consuls, ten years of command), one chosen by each head. From
  [Houses Traditions](https://steamcommunity.com/sharedfiles/filedetails/?id=2590650832) (about 132,000),
  [Hiraeth](https://steamcommunity.com/sharedfiles/filedetails/?id=2697392271),
  [More Legacies](https://steamcommunity.com/sharedfiles/filedetails/?id=2859278694) (about 40,000). Now none: a house
  has a Calling (docs/REPUBLIC.md) and nothing earned. A new system; GAPS R14 is the nearest row.
- **Branches of the name** (large). A grown relative of standing founds a house with a double name, a client of the
  main house; if the main line dies, the eldest branch takes it up, a better way to play on than a sworn friend. From
  [Be Exalted to create Cadet Branch](https://steamcommunity.com/sharedfiles/filedetails/?id=2278502953) (a branch as
  the reward of a great career) and House Unity Balanced. Now none: GDD section 14 counts cadet cousins but nothing
  builds them; docs/MARRIAGE.md's lesser branch is one bride; GAPS R14. A new system.
- **More ways into a family at the start** (medium). A parent who is a younger child of a great house on the map, with
  uncles, aunts, cousins and a relative who dislikes you; or begin as a junior of an existing house, or as heir to an
  extinct house's name and fall. From
  [A New Sensible Family 3](https://steamcommunity.com/sharedfiles/filedetails/?id=3045026915) and the house
  customizer in [A Game of Thrones](https://steamcommunity.com/sharedfiles/filedetails/?id=2962333032). Now part:
  docs/CHARACTER.md has no uncles or cousins and "You are the head of the house"; GAPS R17.
- **Other houses keep a household, and marry with sense** (large). Every house fills its court and council by its
  head's nature and purse, so a rival's physician keeps its head alive and its guard turns a knife. From
  [Better AI Education](https://steamcommunity.com/sharedfiles/filedetails/?id=2753934263),
  [Doctor Tweak](https://steamcommunity.com/sharedfiles/filedetails/?id=2432176898),
  [Better Marriage (AI)](https://steamcommunity.com/sharedfiles/filedetails/?id=2749533543),
  [Less Old Wives](https://steamcommunity.com/sharedfiles/filedetails/?id=2265131712),
  [Active Courtiers](https://steamcommunity.com/sharedfiles/filedetails/?id=3157170996). Now part: GAPS X1 (the
  recurring gap), H1 (the court) and C1 (the council); other houses marry by proposal only (docs/MARRIAGE.md). Turn cost
  on the large maps (GAPS K3).
- **Send for someone** (small). Pay to have three willing young people of minor or client houses offered as a match, a
  protégé or a companion. From [Invite Debutantes](https://steamcommunity.com/sharedfiles/filedetails/?id=2677152091)
  (about 134,000) and [Invite Nobles](https://steamcommunity.com/sharedfiles/filedetails/?id=2686352443). Now part:
  three offer for an empty court post (docs/COURT.md); people come to the door about one year in seven
  (docs/HOUSEHOLD.md).
- **Names that are given** (small). The Senate votes a general a name from the nation they beat (as Scipio became
  Africanus), crews hail a commander Magnus, a founder names a colony, a taker renames a world. From
  [Extended Nicknames](https://steamcommunity.com/sharedfiles/filedetails/?id=2251314687) and
  [Custom Nickname](https://steamcommunity.com/sharedfiles/filedetails/?id=3035543804). Now part: eight epithets by
  rule (LifeSystem.Epithet); a city cannot be renamed (docs/CITIES.md); GAPS R14.

### The Senate, the court and intrigue

- **A place in another house's household** (medium). The player in person, young, landless or fallen, asks a head of
  standing for a place as their envoy, advocate or physician, is paid into their own purse, and rises by that head's favour.
  From [Landless Opportunities: Playable Courtiers](https://steamcommunity.com/sharedfiles/filedetails/?id=3465715649)
  (about 20,000, top rated). Now part: GAPS R1; posts are filled from one's own house (docs/HOUSEHOLD.md). It leans on
  other houses having councils (GAPS C1), so it follows item 1.
- **Plots that take time** (large). A plot against a named person, with named accomplices, moves forward each year;
  every hand makes it faster and leakier, and a plot found out is evidence. From
  [Schemes Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3494237399),
  [Sabotage and Subterfuge](https://steamcommunity.com/sharedfiles/filedetails/?id=3592971803),
  [More Schemes and Plots](https://steamcommunity.com/sharedfiles/filedetails/?id=2975820897). Now part: a knife
  resolves the moment it is ordered (AssassinateCommand); the Agents pool is commented "concurrent schemes" and only the
  knife uses it (src/Core/State/ActionPools.cs). Catiline in 63 BC.
- **Informers in other households, and theirs in yours** (medium). A named informer reports what a house will move,
  vote or prepare against you; a caught one can be turned to feed lies. From Sabotage and Subterfuge,
  [Whisperers](https://steamcommunity.com/sharedfiles/filedetails/?id=3775073045),
  [Hearsay](https://steamcommunity.com/sharedfiles/filedetails/?id=3824749464). Now part: the spymaster knows one
  house exactly and finds an informer one year in five (HouseholdSystem.Posts.cs); GAPS C1.
- **What you know of other houses ages** (medium, as a setting). Another house's regard, purse and lean show as
  estimates with the year learned unless you have a source. From Hearsay,
  [Sabotage and Subterfuge: Obfuscation](https://steamcommunity.com/sharedfiles/filedetails/?id=3604077280),
  [Fog Of War](https://steamcommunity.com/sharedfiles/filedetails/?id=3013354800),
  [obfusCKate](https://steamcommunity.com/sharedfiles/filedetails/?id=2874007571). Now part: fleet reports fade
  (FleetSystem.UpdateIntel), but HousesScreen.razor and MilitaryScreen.razor show exact hulls and regard.
- **The two consuls weigh against each other** (medium). Each year the houses take sides between the consuls, and the
  balance decides who presides and whose edicts stand; pushing past it costs support, as Caesar did to Bibulus. From
  [Dynamic Diarchies](https://steamcommunity.com/sharedfiles/filedetails/?id=3842152116) and
  [Regency Rework](https://steamcommunity.com/sharedfiles/filedetails/?id=3582007365). Now part: a senior and a junior
  consul with a veto each (docs/REPUBLIC.md), the chair (docs/LADDER.md).
- **Parties that split, merge and vote on their line** (medium). From
  [Living Factions](https://steamcommunity.com/sharedfiles/filedetails/?id=3821454887) and
  [Loyal To A Fault](https://steamcommunity.com/sharedfiles/filedetails/?id=2776145076). Now part: four parties and one
  the player founds, one house crosses every tenth turn, and other houses never challenge a leader (docs/REPUBLIC.md,
  docs/SENATE.md).
- **Murder and treason before a praetor, with exile** (medium). A traced knife or treason against the state is charged
  in court; exile is the sentence and the Senate can recall, as with Cicero in 58 and 57 BC; a conviction settles a blood
  debt by law. From [Call for Trial](https://steamcommunity.com/sharedfiles/filedetails/?id=2898554396). Now part:
  charges are only for acts in office (docs/LADDER.md); GAPS R6, R19, L11.
- **One offer with several terms** (medium). Put coin, a favour, a province, a match, your seats or your vote on a
  named motion against what you ask, and the other head weighs the whole. From
  [A Diplomatic Envoy](https://steamcommunity.com/sharedfiles/filedetails/?id=2800626299) (about 19,000) and
  [Peace Deals Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3769843893) (about 12,000). Now part:
  dealings one at a time (DealingsCommands.cs); three terms at a peace (HousePeace.cs); GAPS N22, P15.
- **Rivals with a ten-year aim** (large). Each rival head picks an aim from their ambition and chooses the year's acts
  to serve it; the player can learn the aim. From
  [Intelligent AI](https://steamcommunity.com/sharedfiles/filedetails/?id=3722358200) and
  [Better AI](https://steamcommunity.com/sharedfiles/filedetails/?id=2221348737). Now part: NpcAi.ChooseAction acts by
  nature; GAPS X1, X2. No aim may end the Republic.
- **The estates get a face** (medium). Each estate has a named spokesman with a family, who can be paid, married into
  or broken. From [Immersive Domain Management](https://steamcommunity.com/sharedfiles/filedetails/?id=3604729196)
  (about 14,000). Now part: estates are approval figures (docs/POLITICS.md; content/politics/estates.json).
- **Post-holders with minds of their own** (medium). A slighted or bought post-holder works for themselves, and a better
  relative asks for a post that is held. From Schemes Expanded and Active Courtiers. Now part: GAPS C2, which waits on
  the owner's answer on how far kin may work against the head.
- **A favourite at the ruler's side** (medium). Under a throne, a named favourite whose word opens doors and who can
  fall, as Sejanus did in AD 31. From [Royal Mistress and Paramours](https://steamcommunity.com/sharedfiles/filedetails/?id=2792885005).
  Now none: GAPS R12.
- **Arbitration** (small). A head of standing offers terms to settle another pair's feud or war and is owed a favour by
  both. From Bonds of Diplomacy and Peace Deals Expanded. Now part: the Peace of the Reach motion (docs/SENATE.md); GAPS
  R19.
- **Private war only by the Senate's leave** (small). An Act: free (the custom), by leave, or forbidden and enforced by
  the office fleets. From [Pervasive Crown Authority](https://steamcommunity.com/sharedfiles/filedetails/?id=2692957667)
  (about 47,000) and [Consolidation of Power](https://steamcommunity.com/sharedfiles/filedetails/?id=3658422542). Now
  part: the Peace of the Reach motion forces a white peace on one house; no such Act in content/politics/laws.json.
- **The health of the Republic as figures** (medium). The fleets' loyalty, the courts' honesty and the treasury's
  credit, each with its causes, which the player can mend or trouble and which the Caesar gate reads. From
  [Governments +](https://steamcommunity.com/sharedfiles/filedetails/?id=2635869016) (about 23,000). Now part: "the
  times" are six conditions (docs/CAESAR.md, "The three gates"); GAPS C7. Measure the Caesar rate; it must never become
  a clock.
- **Colleges of priests and the watching of signs** (medium). Seats for life, a head of the rites elected by the
  citizens, and a watcher who can declare ill omens and put off a vote, as Bibulus did through 59 BC. From
  [Shadows of the Cross](https://steamcommunity.com/sharedfiles/filedetails/?id=3827420629) and
  [Ecumenical Council Strict Version](https://steamcommunity.com/sharedfiles/filedetails/?id=3811694069). Now none:
  docs/ROADMAP.md section 6, item 6. Offices and rites only, since a faith is not for now (GAPS R13).
- **Standing orders** (medium). A short ordered list of rules the house follows (who teaches each child, how to answer
  a request that keeps coming, what dues to set), and every report says which rule acted. From
  [Leo VI's Mass Vassal Directives](https://steamcommunity.com/sharedfiles/filedetails/?id=3766246044),
  [Education Automation](https://steamcommunity.com/sharedfiles/filedetails/?id=2879799963),
  [Automatic Education](https://steamcommunity.com/sharedfiles/filedetails/?id=3717349873),
  [Auto promote culture](https://steamcommunity.com/sharedfiles/filedetails/?id=2689410555). Now part: one instruction
  each for the steward, the marshal and the council posts (docs/HOUSEHOLD.md, "Work you can hand on"). Count the
  player's repeated clicks first (docs/PLAYTHROUGH.md).
- **A foreign power likes you and respects you, apart** (medium). From
  [Sunset Invasion](https://steamcommunity.com/sharedfiles/filedetails/?id=2216735054). Now part: one disposition per
  nation (src/Core/State/Nation.cs). The owner's: decisions 8 to 15 in docs/BEYOND.md.

### War

- **Choices in the battle and after it** (medium). In a battle the player commands, a short scene: commit the reserve,
  hold, or feint; after a win, pursue for more hulls at a cost in supply, or let them go. From
  [Better Battles](https://steamcommunity.com/sharedfiles/filedetails/?id=3101905160) (about 54,000, and 34,000 on its
  [older page](https://steamcommunity.com/sharedfiles/filedetails/?id=2749062980)),
  [Battlefield Duel](https://steamcommunity.com/sharedfiles/filedetails/?id=2438706921) (about 24,500),
  [More Single Combats](https://steamcommunity.com/sharedfiles/filedetails/?id=2510732954) (about 20,000). Now part: a
  battle is one roll (FleetSystem.Battle); stance, loadout and style are chosen beforehand (docs/MILITARY.md). Rare.
- **Free companies for hire** (medium). Captains with ships of their own, real people, hired for a term, who make
  trouble idle and turn raider unpaid; a house that has lost its worlds can become one. From
  [Immersive Mercs & Raiders](https://steamcommunity.com/sharedfiles/filedetails/?id=3461530706) and
  [Universal Contract Assistance & Foment Revolt](https://steamcommunity.com/sharedfiles/filedetails/?id=2979921118).
  Now part: a house can hire its own fleet out (MercenarySystem.cs), nobody can hire one in; GAPS N30, Y5. The owner
  should confirm it keeps to "manpower is never sold".
- **Letters of marque** (small). The Senate licenses a house or captain to raid a nation or a raiders' nest for a share
  to the chair, without making the Reach's war. From [Let's Raid](https://steamcommunity.com/sharedfiles/filedetails/?id=3177335917).
  Now part: a house's own war on a nation takes the one place for a war beyond the Reach (docs/BEYOND.md; GAPS N21).
- **A general's veterans settled under the general's name** (medium). Paid-off crews settled on a world keep a cheap reserve that
  answers the general's call and count as the general's clients. From
  [Holy Orders *enhanced*](https://steamcommunity.com/sharedfiles/filedetails/?id=2778162257) and
  [More Balanced Military System](https://steamcommunity.com/sharedfiles/filedetails/?id=2809542899), both small. Now
  part: a veterans' colony is a local law (docs/POLITICS.md) and the Land Act settles veterans (docs/SENATE.md); missing
  is the general's own crews, the general's call and their love. Count only crews under arms in the Caesar gates.
- **An heir to a command** (medium). A famous commander names a successor who takes the crews with half their devotion,
  and three generations of distinguished command add to a house's name in arms. From
  [Accolade Rework](https://steamcommunity.com/sharedfiles/filedetails/?id=3384909321). Now part: a new commander
  starts again and crews resent the change (docs/CAESAR.md; GAPS C2 in the Caesar table).
- **Every war skill counts, and a way of life earns small abilities** (medium). Logistics slows the fall of supply,
  engineering shortens a siege, deeds earn crafts (raider, siege-master, warden of the march); and outside war, a chosen
  way of life (the camp, the forum, the counting-house, the shadows) earns a few named steps over the years. From
  [Crusader Wars](https://steamcommunity.com/sharedfiles/filedetails/?id=2977969008),
  [More Lifestyles](https://steamcommunity.com/sharedfiles/filedetails/?id=3013259695) (about 117,000),
  [Veritas](https://steamcommunity.com/sharedfiles/filedetails/?id=3339038543),
  [Sub Rosa](https://steamcommunity.com/sharedfiles/filedetails/?id=3769960376). Now part: FleetSystem.CommandFactor
  reads tactics, gunnery and leadership only, and nothing in FleetSystem reads logistics; aptitudes open at skill 7
  (docs/EDUCATION.md).
- **The ground in a battle** (medium). A belt shelters the skirmishing side, a gas giant feeds the holder, a gate is
  narrow and favours the line. From Crusader Wars, Sane Warfare and
  [Immersive Siege Battles](https://steamcommunity.com/sharedfiles/filedetails/?id=2472846640). Now part:
  FleetSystem.Defense reads works and buildings, not the bodies of a system.
- **Hull classes that answer one another** (medium). Each class is good against one and fears another, and a region's
  yards build their own pattern. From [Cultural Armies Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=2956027471)
  (about 33,000). Now part: five classes that simply add up (content/fleets/ship_classes.json); counters exist only
  between stances.
- **A battle you can watch** (medium). A short scene in the system view drawn from what the engine already decided,
  with captions from the battle's notes. From [Battle Graphics](https://steamcommunity.com/sharedfiles/filedetails/?id=3225355262)
  (about 95,000). Now part: "a battle is not shown being fought" (docs/SYSTEMS.md). It serves "a game off Steam".
- **A command is held in person, and the head says who may take one** (small). A named admiral must travel to the
  fleet, and the head can keep the heir, the young or the spouse from taking a command unasked. From
  [Prisoners of War](https://steamcommunity.com/sharedfiles/filedetails/?id=2511247871) (commanders travel) and
  [Knight Manager Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=2974850904) (about 144,000, and
  88,000 on the [original](https://steamcommunity.com/sharedfiles/filedetails/?id=2293272317)). Now part:
  AssignAdmiralCommand needs no presence, and FamilySystem.StepUp lets the heir step up.
- **More settings, at the start and in play** (small). War settings (siege years, refill, the price of companies, how
  quarrelsome the houses are) beside the existing ones, settings changeable in a running game and written in the
  chronicle, and story tools only in a game marked custom. From
  [No More Levies](https://steamcommunity.com/sharedfiles/filedetails/?id=2810220558),
  [More Game Rules](https://steamcommunity.com/sharedfiles/filedetails/?id=2216850785) (about 159,000),
  [Gamerule Gadget](https://steamcommunity.com/sharedfiles/filedetails/?id=2826829936) (about 42,000). Now part: the
  powers beyond and money settings at the start (docs/BEYOND.md, docs/WEB_CLIENT.md); GAPS N24, M8. Nothing is called a
  cheat (docs/MONEY.md), and a setting that raises the Caesar rate is the owner's.

### Worlds, buildings and the economy

- **A city that grows: town, city, great city** (medium). It rises with its system's development and people, and at
  each rise its holder picks one of two or three buildings; a cap on grown cities per house. From
  [Cities of Wonder 3](https://steamcommunity.com/sharedfiles/filedetails/?id=2901477267) (about 120,000) and
  [Cities of Wonders 2](https://steamcommunity.com/sharedfiles/filedetails/?id=2216789478). Now part: "A city's kind
  never changes" (docs/CITIES.md).
- **Rooms in the house's seat** (medium). A manor has two rooms, a great house four, a palace six, each chosen from a
  list that leans on a rule the game has (a hall of ancestors, an archive, a guardroom, a nursery, a physician's wing).
  From [Architect of Estates](https://steamcommunity.com/sharedfiles/filedetails/?id=3340544163) (about 66,000) and
  [Ultimate Camp & Ultimate Estate & Ultimate Yurt](https://steamcommunity.com/sharedfiles/filedetails/?id=3338815625)
  (about 34,000). Now part: three grades, each doing one fixed thing (docs/WEALTH.md, "The seat of the house").
- **A building takes a branch at level II or III** (medium). A Spaceport becomes a passenger port, a freight port or a
  naval quay, and takes the branch's name. From Architect of Estates and
  [More buildings](https://steamcommunity.com/sharedfiles/filedetails/?id=2229800103). Now part: GAPS U10, levels only
  multiply. Every branch needs its picture (docs/ART_DIRECTION.md).
- **Disasters by the kind of world** (medium). A city fire, a dome breach, a quake, a stellar flare; the holder pays
  relief, leaves the people to it, or profits as Crassus did. From
  [Sinews of War](https://steamcommunity.com/sharedfiles/filedetails/?id=2566883856). Now none. Rare and seeded.
- **A sickness that travels the lanes** (medium). A fever starts at a busy port and moves along the lanes; physicians,
  a Hospital Quarter and a closed port slow it. From
  [Reasonable Epidemics](https://steamcommunity.com/sharedfiles/filedetails/?id=3229838933) and
  [Random Date Black Death](https://steamcommunity.com/sharedfiles/filedetails/?id=2729249229). Now part: illness is
  one person at a time (docs/LIFE.md, "Health"); a great plague is a later chapter (docs/ROADMAP.md section 6).
- **Contracts and tax-farming** (medium). A yearly board of work for coin and a name: escort a convoy, clear a lane,
  supply the state fleet, farm a region's tithe for five years like the publicani. From
  [Pyrates](https://steamcommunity.com/sharedfiles/filedetails/?id=3797518189) and
  [Golden Dominion](https://steamcommunity.com/sharedfiles/filedetails/?id=3344161778). Now part: docs/ROADMAP.md, "The
  state and commerce", plans tax-farming and fleet contracts; GAPS L3 leans on it.
- **A road of the Reach on a lane** (medium). Beacons, stations and patrols built in stages along a lane, named for the
  builder like the Via Appia: faster crossings, longer supply, fewer raids. From
  [A Road Network In Vanilla](https://steamcommunity.com/sharedfiles/filedetails/?id=3349277316) and
  [Eastern Roman Great Projects](https://steamcommunity.com/sharedfiles/filedetails/?id=3655093103). Now part: the
  aedile's "Repair the lanes" (docs/LADDER.md); the owner chose not to have supply lines as objects (docs/MILITARY.md),
  so ask first.
- **Landmarks placed by hand, and great works in stages** (medium). From
  [Unique Building Plus](https://steamcommunity.com/sharedfiles/filedetails/?id=2447414615) and
  [Overhaul Special Buildings](https://steamcommunity.com/sharedfiles/filedetails/?id=3459853116). Now part: GAPS S3,
  U10; which famous system has what is the owner's (S4).
- **Buildings that help each other** (small). A short table of pairs in one system. From
  [Dynamic Trade Routes](https://steamcommunity.com/sharedfiles/filedetails/?id=2821835496) and
  [Trade](https://steamcommunity.com/sharedfiles/filedetails/?id=2977360091). Now part: ventures cluster
  (Economy.cs, ClusterPct), buildings do not.
- **A market in things** (small). Every heirloom not entailed is listed with its holder, and a person can offer for it;
  a thing sold goes to the dealers of the Exchange city. From
  [Search & Trade Artifacts](https://steamcommunity.com/sharedfiles/filedetails/?id=2962238514) (about 122,000). Now
  part: WealthSystem.Sell removes a thing from the world.
- **What is built shows on the map** (medium). Each great work in its own shape, a world's night side lit by its
  development, stations multiplying in orbit. From
  [Medieval Arts](https://steamcommunity.com/sharedfiles/filedetails/?id=2452585382) (about 156,000) and
  [Holding Sprawl](https://steamcommunity.com/sharedfiles/filedetails/?id=2818728722). Now part: a lamp for each thing
  built (docs/SYSTEMS.md); GAPS S6.
- **People move between worlds** (medium). Toward prosperous worlds with room, away from war, famine and revolt. From
  [Weight of Crown](https://steamcommunity.com/sharedfiles/filedetails/?id=2978738471) and Sinews of War. Now none.
- **Grain for the capital, and a grain commission** (large). Worlds must be fed; the Senate can vote one person a grain
  command with a fleet and a budget, as Pompey had in 57 BC. Now part: the dole is a local law, an edict and the Corn
  Law, with the aedile's grain and a Public Granary (docs/POLITICS.md, docs/EDICTS.md, docs/SENATE.md, docs/LADDER.md,
  docs/BUILDINGS.md), but there is no food good (content/market/goods.json). The commission can come first, on the
  dole.
- **Debasing the coin** (medium). Money now for the purse, dearer prices and lost confidence later, and a recoinage to
  pay for. From Dynamic Trade Routes. Now part: the Mint is a plain income building (docs/BUILDINGS.md); GAPS R6 lists
  "no coinage debased" for an emperor, a later expansion by the owner's word.
- **Prices by region and trade along the lanes** (large). From Dynamic Trade Routes, Pyrates and Trade. Now none, on
  purpose: docs/ECONOMY_MARKET.md keeps one price for the Reach, and docs/ROADMAP.md section 6 lists trade routes as a
  later idea. The owner's decision.
- **A journey, and distance in dealings** (large; distance alone is small). A person travels to the capital, a
  physician or another house, with danger on lawless lanes; a far family's consent and a far trade cost a little more.
  From [Travelers](https://steamcommunity.com/sharedfiles/filedetails/?id=3082182371) (about 49,000) and
  [Travel the Distance](https://steamcommunity.com/sharedfiles/filedetails/?id=2743500919) (about 29,500). Now none:
  src/Core/State/Character.cs has no location.
- **Peoples, tongues and customs** (large). A world's people takes a holder of another people slowly; a governor who
  speaks their tongue does better; each people has a few customs a long-settled house can take up. From
  [Cultures Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2829397295) (about 104,000),
  [Multilingual Education](https://steamcommunity.com/sharedfiles/filedetails/?id=2756456587) (about 100,500),
  [More Traditions v2](https://steamcommunity.com/sharedfiles/filedetails/?id=2893793966),
  [More Regional Innovations](https://steamcommunity.com/sharedfiles/filedetails/?id=2252354244). Now part: culture
  sets starting laws, names and a courtship bonus (content/politics/cultures.json; CourtshipSystem.cs). GAPS R13: the
  owner said peoples are not for now.

### The screen and the start

- **Map modes chosen by hand** (medium). Holders, regard, benches, wealth, people, lawlessness, fleets' reach, and wars
  with occupied worlds hatched. From [More Mapmode Buttons](https://steamcommunity.com/sharedfiles/filedetails/?id=2223763531).
  Now part: three levels by zoom and no modes (GAPS V23 in the map table, and V21).
- **A written arc for each start** (medium). Four to eight events about the start's own situation in its first years,
  one decision only that start offers, and an ending told in the epilogue. From
  [The Fallen Eagle](https://steamcommunity.com/sharedfiles/filedetails/?id=2243307127) (about 160,000) and
  [More Bookmarks+](https://steamcommunity.com/sharedfiles/filedetails/?id=2216670956) (about 78,000). Now part: eleven
  starts in content/scenarios and no event reads which one; GAPS R17.
- **Events tied to a region and to a background** (medium). From
  [Regional Immersion and Cultural Enrichment](https://steamcommunity.com/sharedfiles/filedetails/?id=2273832430)
  (about 245,000). Now part: src/Core/Data/ConditionParser.cs has no test for region, ring, culture or background, and no
  event reads a background (docs/CHARACTER.md); GAPS V11.
- **Everyday life, a tone setting and a history note** (medium). From
  [VIET Events](https://steamcommunity.com/sharedfiles/filedetails/?id=2227658180) (about 386,000) and
  [Historical Quotes](https://steamcommunity.com/sharedfiles/filedetails/?id=2893554112). Now part: about 470 events,
  132 of 382 never fired in 33 games (GAPS Q3), so reachability first. Whether a note may name our own history is the
  owner's.
- **Alert settings, and quieter reports** (small). Each group of alerts shown, shown once or never; routine reports as a
  line. From [Less Event Spam](https://steamcommunity.com/sharedfiles/filedetails/?id=2750102888) and War Alerts. Now
  part: no per-kind setting in Outliner.razor (docs/WEB_CLIENT.md, "Settings"). What cannot be undone is always shown.
- **A finder for every person** (medium). Filters and sorts over everyone alive. From
  [Advanced Character Search](https://steamcommunity.com/sharedfiles/filedetails/?id=2346680183). Now part: the Houses
  window sorts houses and the courtship panel sorts prospects.
- **A frame round every portrait by rank** (small). From
  [Title-Ranked Portrait Borders](https://steamcommunity.com/sharedfiles/filedetails/?id=2234706894) (about 76,000).
  Now part: a ring in the house's colour (src/Web/Components/Portrait.razor); GAPS V5. Painted to the
  docs/ART_DIRECTION.md standard.

## Already in the game

| The mechanic | Known from | Where the game has it |
|---|---|---|
| Councillors grow better with years in a post | [Councillor's experience trait](https://steamcommunity.com/sharedfiles/filedetails/?id=2721974781) (about 88,000), [AGOT - Council Experience](https://steamcommunity.com/sharedfiles/filedetails/?id=3319167091) | docs/HOUSEHOLD.md, "The council" (a step every four years); docs/COURT.md (a point every six years) |
| A ruler's hold grows with years of rule | [Legitimacy Over Time](https://steamcommunity.com/sharedfiles/filedetails/?id=3305687550) | docs/THRONE.md, "The hold" |
| Name any heir ahead of the law | [Designate Heir](https://steamcommunity.com/sharedfiles/filedetails/?id=2375035260) | docs/AMBITIONS.md, "A named heir" |
| Succession by age or by ability | [Succession Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2650385452) (eldership) | the seniority and merit laws, DynastySystem.cs |
| A total war that breaks up the loser | [Great Empire Wars](https://steamcommunity.com/sharedfiles/filedetails/?id=2688782754) | the great war and its partitions, docs/BREAKAWAY.md |
| Build your character's family at the start | [A New Sensible Family 3](https://steamcommunity.com/sharedfiles/filedetails/?id=3045026915) | docs/CHARACTER.md (spouse and their house, children, siblings, living parents) |
| Candidates offered for an empty court post | [Expanded Court Position - Search and Recruit](https://steamcommunity.com/sharedfiles/filedetails/?id=3676293022), [Court Positions Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2754449789) | docs/COURT.md, "Who holds them" (three offer for each) |
| A tutor, a physician and a guard for the family | [Pedagogy Enhanced](https://steamcommunity.com/sharedfiles/filedetails/?id=3124546293), [Doctor Tweak](https://steamcommunity.com/sharedfiles/filedetails/?id=2432176898) | docs/COURT.md, the seven positions (for the player's house) |
| A council of advisers with yearly work | [Council Advisors](https://steamcommunity.com/sharedfiles/filedetails/?id=2992026311) | docs/HOUSEHOLD.md, "The council" (six posts) |
| Kin find their own matches and ask approval; kin step up to empty posts | [Active Courtiers](https://steamcommunity.com/sharedfiles/filedetails/?id=3157170996) | docs/MARRIAGE.md, "Whose courtship it is"; FamilySystem.StepUp |
| Young people find mentors | [Schemes Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3494237399) (its mentor scheme) | docs/HOUSEHOLD.md (mentors; GAPS H1 closed 2026-11-14) |
| Laws voted on by the great families by their interest | [Immersive Realm Laws](https://steamcommunity.com/sharedfiles/filedetails/?id=3351630685) | Acts of the Senate leaned by interest, docs/SENATE.md and docs/LAWS_REVIEW.md |
| An elected senate of families with offices, terms and bought votes | [Res Publica](https://steamcommunity.com/sharedfiles/filedetails/?id=2478356664) | docs/SENATE.md, docs/REPUBLIC.md, docs/LADDER.md |
| A league against an aggressor | [Bonds of Diplomacy](https://steamcommunity.com/sharedfiles/filedetails/?id=3821274809), [Shogunate](https://steamcommunity.com/sharedfiles/filedetails/?id=2253278582) | coalition pressure, src/Core/Systems/SphereSystem.cs |
| Hire your force out in another's war | [Universal Contract Assistance & Foment Revolt](https://steamcommunity.com/sharedfiles/filedetails/?id=2979921118) | src/Core/Systems/MercenarySystem.cs; the Law of Hired Companies, docs/SENATE.md |
| A peace whose terms follow the war score | [Peace Deals Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3769843893), [100% Warscore From Battles](https://steamcommunity.com/sharedfiles/filedetails/?id=3127565943) | src/Core/Systems/HousePeace.cs; with the nations, docs/BEYOND.md (R10h) |
| Reports on others' armies that go stale | [Fog Of War](https://steamcommunity.com/sharedfiles/filedetails/?id=3013354800) | FleetSystem.UpdateIntel and the odds before sailing |
| A personal purse, loans and debts the heir inherits | [Quando Sumus](https://steamcommunity.com/sharedfiles/filedetails/?id=2818339011) (moneylenders) | docs/WEALTH.md; loans in docs/ECONOMY.md |
| Heirlooms given, left, sold and bought back | [Search & Trade Artifacts](https://steamcommunity.com/sharedfiles/filedetails/?id=2962238514) (in part) | docs/WEALTH.md, "Heirlooms" |
| Rivalries inside the family, with the house taking sides | [Love Marriage Family](https://steamcommunity.com/sharedfiles/filedetails/?id=3037969445) (family feasts) | docs/AMBITIONS.md |
| Settings for how strong and aggressive the foreign powers are | [More Game Rules](https://steamcommunity.com/sharedfiles/filedetails/?id=2216850785) (in part) | docs/BEYOND.md, the new-game settings |
| Larger text | [Bigger Font](https://steamcommunity.com/sharedfiles/filedetails/?id=2217453606) | docs/WEB_CLIENT.md, "Settings" |

## Left out

- **The total conversions' own worlds.** Dragons and the Wall ([A Game of Thrones](https://steamcommunity.com/sharedfiles/filedetails/?id=2962333032)),
  Middle-earth ([LotR: Realms in Exile](https://steamcommunity.com/sharedfiles/filedetails/?id=2291024373)), magic,
  vampirism and moon phases ([Elder Kings 2](https://steamcommunity.com/sharedfiles/filedetails/?id=2887120253)),
  immortals and hunger ([Princes of Darkness](https://steamcommunity.com/sharedfiles/filedetails/?id=2216659254)),
  and the governments of [After the End](https://steamcommunity.com/sharedfiles/filedetails/?id=3192256710), whose page
  does not say how they work. They are their settings, not mechanics. What mechanics they have are taken above.
- **Art, models and costume made for CK3's own history.**
  [Community Flavor Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=2220098919) (no gameplay change),
  the monument models of Medieval Arts, the culture art of
  [More Holding Graphics](https://steamcommunity.com/sharedfiles/filedetails/?id=2262523658), and the 215 regiments of
  the Cultural Armies Pack are the mods' own art and content. Only the idea of showing what is built, and of fleets that
  differ by where they were built, is kept.
- **What exists only for CK3's own map and history.** The 158 historical wonders of Unique Building Plus, the 1,000 of
  [Medieval Arts - More Special Buildings](https://steamcommunity.com/sharedfiles/filedetails/?id=2692079378), the
  merged set of [A Special World](https://steamcommunity.com/sharedfiles/filedetails/?id=2875587269), the dated
  invasions of Historic Invasions, the start dates of More Bookmarks+, The Fallen Eagle's historical figures, and the
  regional packs of RICE themselves. The general mechanics (a demand before an invasion, an arc for each start, events
  tied to a region) are kept.
- **Compatibility patches and fixes to CK3's own AI.**
  [Less Dumb AI](https://steamcommunity.com/sharedfiles/filedetails/?id=3501889258),
  [AI Build Cities](https://steamcommunity.com/sharedfiles/filedetails/?id=3709020838),
  [Immersive Imprisonment](https://steamcommunity.com/sharedfiles/filedetails/?id=2479654064) and the like repair
  CK3's own rules. The principle, that other houses should look after their own people, is kept.
- **More slots and fewer limits.** More Building Slots, More Buildings Reboot (6 to 20 buildings a holding),
  [Ultimate Camp](https://steamcommunity.com/sharedfiles/filedetails/?id=3338815625)'s unlock-everything,
  [Infinite Wards](https://steamcommunity.com/sharedfiles/filedetails/?id=2217801870),
  [More Artifact Slots](https://steamcommunity.com/sharedfiles/filedetails/?id=2774914093) and
  [More Religion Tenets Slots](https://steamcommunity.com/sharedfiles/filedetails/?id=2218249327) remove choices or
  break the economy the game was tuned to (docs/ECONOMY_REVIEW.md, docs/MONEY.md).
- **A second economy of resources and research.**
  [Flogi's Buildings and Technology Mod](https://steamcommunity.com/sharedfiles/filedetails/?id=2541034876) replaces
  the building system with wood, stone, metal and 61 technologies; a research tree is a lot of content for little play
  and sits awkwardly with a Republic that stands unless the player ends it.
- **Things that wear and are improved.** Combining and upgrading artifacts (More Artifact Slots) is busywork with
  little play in it.
- **Many consorts, harems and the favour among them** ([Harem Politics](https://steamcommunity.com/sharedfiles/filedetails/?id=2916241472)),
  and romance inside the family ([Family Romance](https://steamcommunity.com/sharedfiles/filedetails/?id=3102171241)).
  They do not fit the Republic's one marriage or the game's tone. One partner at a time is kept.
- **Faith as a religion with believers and doctrines.**
  [More Religious Doctrines](https://steamcommunity.com/sharedfiles/filedetails/?id=2221612003), More Religion Tenets
  Slots, the doctrine-setting of Ecumenical Council, the papacy of Shadows of the Cross,
  [Mass Demand Conversion](https://steamcommunity.com/sharedfiles/filedetails/?id=2753176859), the witchcraft legacy of
  More Legacies, and the faith side of Holy Orders. The owner said a faith is not for now (GAPS R13). Only the colleges
  as offices of the state are kept.
- **Breeding for traits.** The top tiers of Inheritable Traits++ that must be bred for, and the mutations of Who Broke
  The Reactor? ([mirror](https://catalogue.smods.ru/archives/105494)), read as eugenics. Only small family gifts are
  kept, never called breeding.
- **A cheat menu.** [Daddy Pika's Cheat Menu](https://steamcommunity.com/sharedfiles/filedetails/?id=2216746850)
  (about 410,000 lifetime). The owner wants nothing in the game called a cheat (docs/MONEY.md); story tools in a custom
  game are kept, with the owner's word.
- **Hiding what a choice will do.** [Events Without Tooltips](https://steamcommunity.com/sharedfiles/filedetails/?id=3140405364).
  The playthroughs asked for the opposite and the game built it (docs/PLAYTHROUGH.md, item 8).
- **Interface for CK3's own screens.** [More Family Relations](https://steamcommunity.com/sharedfiles/filedetails/?id=3469083788)
  (names for wider kin) and [Toggle Automation for All Court Positions](https://steamcommunity.com/sharedfiles/filedetails/?id=3652963481)
  fix CK3's own windows; the game's kinship words and council instructions already cover them.
- **Battles fought in another game.** [Crusader Wars](https://steamcommunity.com/sharedfiles/filedetails/?id=2977969008)
  sends battles to Total War: Attila. Only its ideas (the ground matters, commanders as people in the battle, one great
  battle can decide a war) are kept.
- **Dynasty Member Alerts' roster.** A list of relatives to marry, educate and land each year is the kind of screen
  docs/PROCESS.md forbids; only its alerts for what has just become possible are kept.

## Sources

The Steam Workshop pages were read in the Workshop and, when it turned the reader away, through Steam's public file
details service (https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/), which gives each mod's
own description and subscriber counts; only Workshop ids were sent. When fetched by a tool, many Workshop pages showed a
"removed" or "incompatible" banner; the file details service showed every item live, so the banner is how Steam answers
an automated reader.

**Lists and searches.** The Workshop's
[most subscribed](https://steamcommunity.com/workshop/browse/?appid=1158310&browsesort=totaluniquesubscribers&section=readytouseitems)
(pages 1 to 8), [top rated](https://steamcommunity.com/workshop/browse/?appid=1158310&browsesort=toprated&section=readytouseitems)
(pages 1 to 3) and [most popular this year](https://steamcommunity.com/workshop/browse/?appid=1158310&browsesort=trend&days=365)
(pages 1 to 3), and keyword searches; [PC Gamer](https://www.pcgamer.com/best-crusader-kings-3-ck3-mods);
[TheGamer](https://www.thegamer.com/best-mods-crusader-kings-3-ranked/);
[Wargamer](https://www.wargamer.com/crusader-kings-3/mods);
[After Strategy](https://after-strategy.com/en/ck3-best-mods-2026-top-10-total-conversions/);
[Gamers Decide](https://www.gamersdecide.com/articles/crusader-kings-3-best-mods);
[Stealth Optional](https://stealthoptional.com/article/crusader-kings-3-best-mods);
[top-mods.com, most downloaded characters and dynasties](https://top-mods.com/mods-downloaded/crusader-kings-3/characters-dynasties);
[top-mods.com, top rated economy and trade](https://top-mods.com/mods-rated/crusader-kings-3/economy-trade);
[Paradox's developer diary on schemes, secrets and hooks](https://forum.paradoxplaza.com/forum/threads/ck3-dev-diary-5-schemes-secrets-and-hooks.1289167)
(for the base game only).

**Copies of mod pages read elsewhere.** top-mods.com:
[Grand Remembrance](https://top-mods.com/mods/crusader-kings-3/characters-dynasties/16944-grand-remembrance.html),
[Love Marriage Family](https://top-mods.com/mods/crusader-kings-3/characters-dynasties/16721-love-marriage-family.html),
[A New Sensible Family 3](https://top-mods.com/mods/crusader-kings-3/characters-dynasties/17061-a-new-sensible-family-3.html),
[Architect of Estates](https://top-mods.com/mods/crusader-kings-3/economy-trade/16937-architect-of-estates-domiciles-building-expansion.html),
[Pyrates](https://top-mods.com/mods/crusader-kings-3/economy-trade/18336-pyrates.html). The
[VIET Events README](https://raw.githubusercontent.com/cybrxkhan/VIET-Events-for-CK3/main/README.md). Love Marriage
Family's [feature post](https://steamcommunity.com/workshop/filedetails/discussion/3037969445/4295942652154349673) and
[changelog](https://steamcommunity.com/sharedfiles/filedetails/changelog/3037969445?p=6); Crusader Wars'
[changelog](https://steamcommunity.com/sharedfiles/filedetails/changelog/2977969008?p=1). Seen only in search results
(the mirror refused a direct read): [Dynamic Regency](https://catalogue.smods.ru/archives/222831),
[Regency & Ruler Training](https://catalogue.smods.ru/?p=140094),
[More Concubinage and Bastardry](https://catalogue.smods.ru/archives/305523),
[Bastards and Banners](https://catalogue.smods.ru/archives/405307),
[Who Broke The Reactor?](https://catalogue.smods.ru/archives/105494).

**Workshop pages of the mods** (https://steamcommunity.com/sharedfiles/filedetails/?id= followed by the number):

- Family and dynasty: Succession Expanded 2650385452; Love Marriage Family 3037969445; Grand Remembrance 3678529052;
  Hiraeth - Dynasty Legacies Overhaul 2697392271; More Legacies 2859278694; Succession Crisis 3713902872; Adoption
  Options 2816662627; Be Exalted to create Cadet Branch 2278502953; Inheritable Traits++ 2588849555; Dynastic Relations
  2799368260; Inheritance Doctrine: Concubine's Children 2976374352; House Unity Balanced 3122203007; Extended
  Bloodlines 2233355253; A New Sensible Family 3 3045026915; Social Relations Expanded 2825977309; Inheritable
  Relations 2829009590; Houses Traditions 2590650832; Improved Personality Formation System 2488151099; Weddings
  Expanded 2811048141; Relationships of Weddings Expanded 3054030477; Family Romance 3102171241; Harem Politics
  2916241472; No Death Cascade 2852881755; ENFP's Event Packs 3541565115; Pedagogy Enhanced 3124546293; More Family
  Relations 3469083788; Lifestyles Improved 3085100161; Stress Bar for All 2790457557; Stress Control 2586980596;
  Infinite Wards 2217801870; Extended Family Actions 2430932902.
- Court, Senate and intrigue: More Interactive Vassals 2712590542; Schemes Expanded 3494237399; Sabotage and
  Subterfuge 3592971803; Sabotage and Subterfuge: Obfuscation 3604077280; Hearsay 3824749464; Whisperers 3775073045;
  Active Courtiers 3157170996; Court Positions Expanded 2754449789; Council Advisors - UPDATED 2992026311; Immersive
  Domain Management 3604729196; Living Factions 3821454887; Loyal To A Fault 2776145076; Bonds of Diplomacy 3821274809;
  A Diplomatic Envoy 2800626299; Peace Deals Expanded 3769843893; Call for Trial 2898554396; Dynamic Regency
  3096490417; Regency Rework 3582007365; Dynamic Diarchies 3842152116; Res Publica 2478356664; More Playable Republics
  3307222438; The republic Mod 3823090869; Consolidation of Power: Feudal Improvement 3658422542; Theresia's
  Administrative Overhaul 3343175673; Content Improvement for Merit and Administrative Governments 3595878042;
  Immersive Realm Laws 3351630685; Pervasive Crown Authority 2692957667; Royal Mistress and Paramours 2792885005;
  Social Relations Expanded (SRE) - Rework 3729520553; Intelligent AI 3722358200; Better AI 2221348737; Less Dumb AI
  3501889258; More Schemes and Plots 2975820897; Hostile Actions against Blackmailers 2255784211; More
  Diplomacy/Decisions Options 2631419657; Useful Liege/Vassal Alliances 3318068569; Oathbound 3457401824; Rescue &
  Vengeance 3089046758; Prisoners and Jailors 3161565481; Immersive Imprisonment 2479654064; two further pages read
  while searching, 3742562925 and 2801920218; Councillor's experience trait 2721974781; AGOT - Council Experience
  3319167091; Invite Debutantes 2677152091; Invite Nobles 2686352443; Quando Sumus - Decisions of CK3 2818339011;
  Expanded Court Position - Search and Recruit 3676293022; Landless Opportunities: Playable Courtiers 3465715649;
  [LOT] Legitimacy Over Time 3305687550; Designate Heir 2375035260; In My Humble Opinion 2625307328.
- War: Knight Manager Continued 2974850904; Knight Manager 2293272317; Battle Graphics 3225355262; Historic Invasions
  2837398670; Better Battles (Updated 1.21) 3101905160; Better Battles, the Robe Update 2749062980; Prisoners of War
  2511247871; Cultural Armies Pack 2956027471; Immersive Mercs & Raiders 3461530706; Vassals To Arms 2535753142; Dark
  Ages 2975514448; Battlefield Duel 2438706921; More Single Combats 2510732954; War Lords 3454418002; Pillage
  3027931283; Crusader Wars 'Ad Maiorem Gloriam' 2977969008; Life of an Adventurer 3514059618; obfusCKate - Hidden
  Information 2874007571; Let's Raid 3177335917; No More Levies 2810220558; Casus Belli - Demand Religious Conversion
  2588989885; Fog Of War 3013354800; Accolade Rework 3384909321; Bail Out Anyone 2253837157; Immersive Siege Battles
  2472846640; Battle Captives 2217257991; Universal Contract Assistance & Foment Revolt 2979921118; Prisoners of Raid
  2221906780; 100% Warscore From Battles 3127565943; Sane Warfare 2222896774; More Balanced Military System
  2809542899; Holy Orders *enhanced* 2778162257; Great Empire Wars 2688782754.
- Buildings, worlds and the economy: Cities of Wonder 3 2901477267; Cities of Wonders 2 2216789478; Unique Building Plus
  2447414615; Medieval Arts 2452585382; Medieval Arts - More Special Buildings 2692079378; A Special World - Special
  Building Compatch 2875587269; Overhaul Special Buildings (Elder Kings 2 submod) 3459853116; More Buildings Reboot
  2943715129; More buildings 2229800103; More Building Slots 2216916022; Flogi's Buildings and Technology Mod
  2541034876; Architect of Estates: Domiciles Building Expansion 3340544163; Ultimate Camp & Ultimate Estate & Ultimate
  Yurt 3338815625; [Beta] Dynamic Trade Routes 2821835496; Trade 2977360091; Golden Dominion 3344161778; Pyrates
  3797518189; Search & Trade Artifacts 2962238514; More Artifact Slots + Combine Artifacts + Upgrade Artifacts
  2774914093; Travelers 3082182371; Travel the Distance 2743500919; Sinews of War 2566883856; Siege devastation
  3239132682; Weight of Crown 2978738471; Holding Sprawl 2818728722; More Holding Graphics 2262523658; A Road Network
  In Vanilla 3349277316; Eastern Roman Great Projects 3655093103; Reasonable Epidemics 3229838933; Random Date Black
  Death & Pandemic Explosion Mechanic 2729249229.
- Personality, skills and culture: More Lifestyles 3013259695; Veritas - Lifestyle Perks Overhaul 3339038543;
  Prodigy - Character Skills Overhaul 3685775290; Sub Rosa - Intrigue Traits of CK3 3769960376; More Personality Depth
  3717989134; New Personality Events for Children 2779836155; Multilingual Education 2756456587; Cultures Expanded
  2829397295; More Traditions v2 2893793966; More Regional Innovations 2252354244; Auto promote culture and Auto
  increase control 2689410555; Mass Demand Conversion 2753176859.
- Automation and the AI: Leo VI's Mass Vassal Directives 3766246044; Education Automation 2879799963; Automatic
  Education - Guardian & University Manager 3717349873; Toggle Automation for All Court Positions 3652963481; Better AI
  Education 2753934263; AI Build Cities 3709020838; Better Marriage (AI) 2749533543; Less Old Wives 2265131712; Doctor
  Tweak - AI Will More Often Hire Court Physicians 2432176898.
- Names, rules and religion: Extended Nicknames 2251314687; Custom Nickname 3035543804; Oikoumene: Dynamic Place Names
  3783694512; Gamerule Gadget 2826829936; Daddy Pika's Cheat Menu 2216746850; Shadows of the Cross 3827420629;
  Ecumenical Council Strict Version 3811694069; More Religious Doctrines 2221612003; [Abandoned] More Religion Tenets
  Slots 2218249327.
- Overhauls and the interface: A Game of Thrones 2962333032; Community Flavor Pack 2220098919; VIET Events 2227658180;
  Nameplates 2220762808; LotR: Realms in Exile 2291024373; Elder Kings 2 2887120253; Regional Immersion and Cultural
  Enrichment 2273832430; Princes of Darkness 2216659254; The Fallen Eagle 2243307127; More Game Rules 2216850785;
  Extended Outliner 2219611817; After the End 3192256710; Shogunate 2253278582; More Bookmarks+ 2216670956;
  Title-Ranked Portrait Borders (Ironman) 2234706894; Governments + 2635869016; War Alerts 2256989379; Expanded Alerts
  2218116577; Bigger Font 2217453606; Advanced Character Search 2346680183; Historical Quotes 2893554112; Less Event
  Spam 2750102888; Sunset Invasion 2216735054; More Mapmode Buttons 2223763531; Dynasty Member Alerts 2837827970;
  Events Without Tooltips 3140405364.

**This game's files checked.** docs/PROCESS.md, GAPS.md, ROADMAP.md, GDD.md, PLAYTHROUGH.md, CHARACTER.md, UPSTART.md,
HOUSEHOLD.md, COURT.md, LIFE.md, AMBITIONS.md, EDUCATION.md, MARRIAGE.md, WEALTH.md, FEUD.md, TITLES.md, REPUBLIC.md,
SENATE.md, LADDER.md, EDICTS.md, POLITICS.md, LAWS_REVIEW.md, THRONE.md, EMERGENCY.md, CAESAR.md, BREAKAWAY.md,
BEYOND.md, FOREIGN_NATIONS.md, MILITARY.md, SYSTEMS.md, GALAXY.md, PROVINCES.md, CITIES.md, BUILDINGS.md, ECONOMY.md,
ECONOMY_MARKET.md, ECONOMY_REVIEW.md, MONEY.md, WEB_CLIENT.md, DASHBOARDS.md, GAP_ANALYSIS.md, CONTENT_GUIDE.md;
src/Core/Systems/DynastySystem.cs, Lineage.cs, FamilySystem.cs, HouseholdSystem.cs, HouseholdSystem.Posts.cs,
EducationSystem.cs, OrderSystem.cs, WealthSystem.cs, WealthSystem.Rich.cs, CourtshipSystem.cs, LifeSystem.cs,
FleetSystem.cs, MercenarySystem.cs, HousePeace.cs, NpcAi.cs, SphereSystem.cs, SystemWorks.cs, Economy.cs,
CitySystem.cs, DealingsSystem.cs; src/Core/Commands/WarCommands.cs, FleetCommands.cs, DynastyCommands.cs,
ActorCommands.cs, DealingsCommands.cs; src/Core/State/House.cs, Character.cs, Relationship.cs, Resources.cs,
ActionPools.cs, Nation.cs; src/Core/Presentation/ProfileProjection.cs; src/Core/Data/ConditionParser.cs;
src/Web/Components/Outliner.razor, EventWindow.razor, CreationScreen.razor, HousesScreen.razor, MilitaryScreen.razor,
DynastyScreen.razor, Portrait.razor; content/traits/traits.json and friction.json, content/politics/laws.json,
cultures.json and estates.json, content/events/courtship.json, household.json and military.json,
content/fleets/ship_classes.json, content/senate/motions.json, content/market/goods.json, content/honors/honors.json,
content/galaxy/the_reach.json.
