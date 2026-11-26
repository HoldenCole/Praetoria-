# Ideas from Imperator: Rome

The owner asked what Imperator: Rome, Paradox's game of the Roman world, could add to this one. About 135 pages were
read. They cover the base game from its launch in April 2019 through the free updates 1.1 Pompey, 1.2 Cicero, 1.3
Livy, 1.4 Archimedes and 1.5 Menander to 2.0 Marius (February 2021), and the two volunteer patches, 2.0.4 and 2.0.5
Augustus (2024 and 2025), that came after Paradox shelved the game in 2021. The sources were Paradox's developer
diaries and patch notes as posted on Steam (the Paradox forum turns automated readers away with a browser check); the
Imperator wiki (some pages through a text copy, others refused by the same check); the game's rules as carried in the
published files of Imperator: Invictus, the most used mod (about 160,000 subscribers); the Workshop pages of other
well-liked mods (Res Publica, Legion Distinctions Overhaul, Full Mechanical Overhaul, Virtual Limes, 2.0 Better UI,
Bloodlines 2.0, TE: Crisis of the Third Century, Gladio et Sale); guides; reviews of the 2.0 update; the historian
Bret Devereaux's five long articles on the game; and players' threads on Steam and elsewhere. Every mechanic was then
checked against this game's documents and code, so each entry says what the game already has.

Imperator is about running a whole state: the player is the country, names every governor and spends the country's
points. In this game the player is one person. So every idea here says how it works for one person: through an office
they hold (consul, governor, censor, admiral), through their own house's worlds and people, or through the Senate. The
rule: **we take mechanics and ideas only, never Imperator's or a mod's names, art, text, characters or events.**
Anything here would be built new, in the Reach, in our own words and pictures.

How the list is ordered: as in docs/IDEAS_FROM_MODS.md, what adds most to play for its size comes first, and so does
what fits one person. For now the owner's rule is to close and deepen, so an idea that closes a row in docs/GAPS.md
ranks higher, and each entry names the row. Some ids are used in two tables of the register (C2, C4, X1, X2, M3, M5,
U4, H2); where that is so, the table is named. Sizes follow docs/GAPS.md: **small** is one change, **medium** is a
cycle, **large** is several cycles and needs a plan first. Where Imperator does the same thing as an idea already on
the Crusader Kings list, it is not listed again here: the section after the list says which entry it strengthens and
what Imperator adds.

One word must not travel with the mechanics. Imperator's "province" is a region of the state; in this game a province
is a third of a world (docs/PROVINCES.md). This page says **a governorship** for the Roman province.

## What is worth adding, best first

### 1. A triumph is had at the gate

**What it is.** A general voted a triumph must come home and lay the command down for the year to have it. While they
are home the fleet sits under its captain, and its crews' devotion does not fade. They may decline the triumph to keep
the fleet. And while someone commands a fleet away from the capital, or governs abroad (entry 6), they cannot stand
for a magistracy or the consulship unless the Senate votes them leave to stand in absence. So a general chooses
between glory, office and command, and a senator can move a triumph for a rival to take a fleet off them without a
recall.

**How Imperator does it.** Hold Triumph is an act on a commander or governor who won a great battle in the last two
years. Since 1.5 Menander (August 2020) the commander leaves the army to attend; it costs 30 political influence,
needs loyalty of 20, and gives loyalty for 20 years, prestige, popularity, a victory trait and sometimes a name; in
2.0 it takes the general out of their post even when disloyal ([Character
interaction](https://imperator.paradoxwikis.com/Character_interaction); [1.5
notes](https://www.allpatchnotes.com/imperator-rome-1-5-patch-notes-epirus-pack-new-missions-galore);
[changelog](https://www.gog.com/forum/imperator_rome/changelog/page2)). Launch players used it to win back a disloyal
general, and found it was the one thing besides death that ended cohorts' loyalty to their commander ([Steam,
2019](https://steamcommunity.com/app/859580/discussions/0/1676938384247438849/)); others complained it needed endless
war ([Steam, 2019](https://steamcommunity.com/app/859580/discussions/0/1676938384249580676/)). A modder found triumphs
could be held for foreigners with no cooldown ([Legion Distinctions
Overhaul](https://steamcommunity.com/sharedfiles/filedetails/?id=2575888248)). No page read shows Imperator with a
rule on standing in absence. That part is Rome's: Caesar gave up his triumph to stand for consul in 60 BC, and the law of the ten tribunes
let him stand in absence in 52 BC.

**The game now.** Part. The triumph is a Senate motion after three victories: the house gains 6 legitimacy and glory,
and the fleets the general commands gain devotion (+10) and loyalty (+5); a refused triumph is a cause
(docs/CAESAR.md, "The triumph"; CaesarSystem.cs). The general keeps the command throughout. A commander can stand for
consul while commanding: OfficeSystem.Candidate checks age, track, rank, the praetorship and a bar, not where the
person is.

**Closes.** Q15 (a triumph can be moved only by a Prefect or a Judge), if the same change opens the motion to anyone
who has held a magistracy.

**Size.** Small: the command laid down for the year, a candidate's absence read, and one new motion (leave to stand in
absence).

**Why.** It is the one-person choice exactly: Caesar's in 60 BC, and the trap that closed on him in 49 BC. It costs
little, gives the Senate a lever on a dangerous general that is softer than a recall, and puts glory and office in the
same year. A triumph is one of the Caesar gates, so measure the rate with `praetoria caesar` after; Imperator's lesson
is one triumph a war, and none for anyone outside the Reach.

### 2. A charge against the powerful is a gamble

**What it is.** A charge brought against a person of great sway (first guess: 40 or more), or against the head of a
house of Legate rank or above, counts as a cause to cross the line in the year it is heard, as a recall or a censure
does. The accused has three answers: stand trial as now; go into exile before the verdict, so their offices and
commands lapse and their purse goes with them to a nation's court beyond the Reach, until the Senate votes them home
(as it did Cicero in 57 BC); or, if they could cross, cross. An acquittal costs the accuser's house standing and gives
the accused renown. A conviction of someone who is commanding forces the choice: lay down the command, or cross. A
house may also let it be known that a general will be charged the day they lay their command down, and that counts as
a cause too. Other houses' generals answer by sway and nature, as they answer a recall.

**How Imperator does it.** Since 1.1 Pompey (June 2019) a disloyal office-holder cannot be dismissed or imprisoned,
only tried, "potentially sparking a civil war" ([changelog](https://www.gog.com/forum/imperator_rome/changelog/page1);
[diary 52](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2408775863533878321)).
The trial is a chain of events the player's choices steer; its chance rises with the accused's corruption and falls
with their power base; in 2.0 it costs 5 tyranny and can be used on the same person once in five years ([Character
interaction](https://imperator.paradoxwikis.com/Character_interaction)). 1.2 Cicero made an imprisoned disloyal person
unable to start a civil war, so arresting first matters; 1.4 (March 2020) made imprisoning a head of family always
need a trial and the risk of war ([changelog](https://www.gog.com/forum/imperator_rome/changelog/page2)). The civil
war page names a failed trial of a powerful person as the main way a civil war starts outside its countdown ([Civil
war](https://imperator.paradoxwikis.com/Civil_war)). Banish, at launch, sent a prisoner out of the country ([launch
interactions](https://www.magicgameworld.com/?p=32685)).

**The game now.** Part. Any house may charge a magistrate for acts in office before a praetor, with pleading and a
docket (docs/LADDER.md, "The courts" and "The docket"; L2 closed). A general's cause is only a recall, a triumph
refused or a censure in the last five years (CaesarSystem.Grievance; docs/CAESAR.md, "Crossing the line"). The accused
does nothing but be well or badly represented (GAPS L11), and nobody is ever exiled (GAPS R6 lists "no exile" among
what an emperor cannot do; nothing else exiles either).

**Closes.** C7 in part (the player can now make one of the times a Caesar needs, not only meet them); L11 in part (the
accused acts in their own defence).

**Size.** Small for the cause and the acquittal's price. Medium with exile and the vote to come home.

**Why.** Rome's own trap, from both sides: bring down a rival general by law and risk the march, or, as the general,
weigh what is waiting at home when the command ends. More causes mean more marches, so measure the Caesar rate (Q1,
C9) before and after. A charge made only to provoke must cost the accuser if it fails. Exile needs somewhere to go
(docs/BEYOND.md) and a way back. It builds well beside the Crusader Kings entry on murder and treason before a praetor
(see below).

### 3. Crews who belong to their commander

**What it is.** While a fleet's crews follow its commander (devotion 50 or more), the head cannot fold it into another
fleet, split hulls off it, lay it up or hand it to someone else. Each refusal says why ("the crews of the Harker Guard
follow Lucan Harker, not the house") and that only relieving the commander, at the known cost in resentment, will do.
A commander whose crews follow them and who thinks −30 or worse of their head can take the fleet over to another house
that takes them in by service, and another house can ask such a commander to come over (first guess: 2 agents, and
intrigue or charisma against what the commander still owes their house). The old house thinks 40 worse of the new one,
and the Senate may censure it. The head has two levers back. Paying the crews from the house (first guess: 300 credits
a hull) takes 20 off their devotion to the commander and adds 5 to their loyalty to the house; the commander resents
it unless they asked. Keeping a fleet at drill under its commander builds devotion slowly (+2 a year). And a commander
who defies the head will sail and fight, but refuses the harsh orders (raid, drill, lay up, detach hulls, burn
stores), with their name and reason.

**How Imperator does it.** From launch to 2.0, cohorts are loyal to the state or to a person, usually their commander,
and grow loyal with victories and sieges; loyal cohorts cannot be disbanded or moved to another army, and stay loyal
until that person dies or holds a triumph ([gamepressure
guide](https://www.gamepressure.com/imperator-rome/loyalty/zec362); [Steam,
2019](https://steamcommunity.com/app/859580/discussions/0/1676938384247438849/)). In a civil war they go over with
their commander: in 2025 a player saw five legions go ([Steam,
2025](https://steamcommunity.com/app/859580/discussions/0/603022903526591777/)). In 2.0 every cohort under a commander
has a monthly chance to become theirs, raised by charisma, popularity, wealth and drill; a loyal cohort costs the
state two thirds of its keep because its sponsor pays the rest ([Civil
war](https://imperator.paradoxwikis.com/Civil_war)). Drill raises the chance, but only under a commander whose loyalty
is over 33, and every special order in the 2.0 files (pillage, raze, drill, settle a colony) needs that same loyalty
([drill](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/unit_abilities/drill_army.txt);
[pillage](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/unit_abilities/pillage.txt)). Launch let a
ruler recruit a disloyal foreign general with their army ([launch
interactions](https://www.magicgameworld.com/?p=32685)), and Invictus pays a disloyal foreign governor to come over
with their land
([seduce_governor](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/character_interactions/seduce_governor.txt)).
Players paid off loyal cohorts with gold to cut a commander's power, and the guide's advice was to split armies so
cohorts cannot defect together. PCGamesN found that 2.0's smaller armies made generals' loyalty "at a premium"
([PCGamesN](https://www.pcgamesn.com/imperator-rome/good-now-2-0-marius)).

**The game now.** Part. Each fleet has a devotion from 0 to 100, and at 50 its crews follow the commander before the
house; a donative of 200 a hull adds 8 (docs/CAESAR.md, "The crews and their commander"); a legate's devoted fleet
goes over when the line is crossed (docs/MILITARY.md, "The Caesar road"). But merging and detaching do not read
devotion (MergeFleetsCommand in src/Core/Commands/FleetCommands.cs), laying up refuses only office, levy and hired
fleets (docs/MILITARY.md, "Laid up"), and devotion counts only while the commander commands
(CaesarSystem.DevotedHulls). Crews under 30 loyalty will not sail, but the commander's own regard gates no order.
Someone of another house can be taken in, but whether they come does not read what they think of their own head
(docs/HOUSEHOLD.md, "Whether they come"). GAPS C2 in the Caesar road table: a general does not defect with their
crews.

**Closes.** C2 in the Caesar road table, its first half. The second half, crews following a dead commander's heir, is
the Crusader Kings entry "An heir to a command" (see below).

**Size.** Medium.

**Why.** Who commands becomes a real question for the head, a relative gets a way to go their own road within the
rules, as Labienus left Caesar in 49 BC, and a head can no longer disarm a dangerous relative for nothing. Warn a year
ahead on the fleet's card. It does not let a relative cross the line against the head's wishes, which stays the
owner's question (C6). Other houses must lure the player's commanders too (X1), and their generals' defections must
stay inside the one-in-ten Caesar rule. Both levers move that rate, so measure it.

### 4. Ending the emergency powers early, and the knife for one who keeps them

**What it is.** A motion to end the powers (first guess: 3 influence), which may be moved when the danger has plainly
passed, or when the holder has lost the crowd (entry 12) or is known for high-handedness (see Crusader Kings entry 15
below). Houses lean by regard and bench, and fear now counts against the holder. If it carries, the powers end, and
the holder's house loses standing unless it laid them down first. Against a holder who keeps them past their leave,
senators of standing who hate them may form a conspiracy: the more hands, the better its chance and the likelier it
leaks. The player can move the motion against a rival consul who levies without a vote, can join a conspiracy against
another, and, if they keep the powers, faces one.

**How Imperator does it.** In 2.0, Appoint Dictator needs 75 Senate support and 10 tyranny, frees the dictator from
the Senate's approval and adds a year to the term. At the end, the dictator is expected to give the powers back but
may keep them, depending on party, popularity, traits and the size of the state. Return Dictatorship forces a dictator
out when their popularity is low or the state's stability high (at a price of 25 stability, and 5 tyranny taken off),
and Murder Dictator removes an illegal one at the cost of an assassination and 30 stability
([Government](https://imperator.paradoxwikis.com/Senate); [Character
interaction](https://imperator.paradoxwikis.com/Character_interaction)). 1.5 fixed how the term's extension lapsed,
and 2.0.2 made the AI less eager for dictatorship ([1.5
notes](https://www.allpatchnotes.com/imperator-rome-1-5-patch-notes-epirus-pack-new-missions-galore); [Steam
news](https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid=859580&count=500&maxlength=0&format=json)).
Players complained there was "only one way into Dictatorship and no way out" ([Steam,
2024](https://steamcommunity.com/app/859580/discussions/0/4356746484460744297)).

**The game now.** Part. The powers can be voted, laid down, extended, kept past leave (by the player only) and
stripped after three years kept (docs/EMERGENCY.md). GAPS R20: the Senate cannot revoke them early or move against a
holder who abuses them. A knife exists (AssassinateCommand), but nothing ties it to the powers.

**Closes.** R20, its first part (revoking the powers, and moving against a holder who abuses them).

**Size.** Medium. The motion alone is small; the conspiracy leans on the Crusader Kings entry "Plots that take time".

**Why.** The Ides of March, from either side, and the road out of a dictatorship that Imperator lacked. Other houses
never keep the powers (rule 2), so the conspiracy falls only on the player: it must read as the fair price of their
own choice, with warnings first.

### 5. Houses that expect their share of the places

**What it is.** Every great house and old name keeps a count of years without a place: since one of its people last
held a magistracy, an office of the Reach, a consulship or a command given by service. It expects one place in a span
set by its seats (first guesses: twelve seats or more, one in 6 years; eight seats, one in 10; fewer, one in 15). A
year before its span runs out, its head comes to the player if the player leads its bench, sits as consul or is its
patron, and asks for one of its people to be put forward, by a commission or by the player's backing at the count.
Granted, the house owes a favour. Shut out past the span, its head thinks a point a year worse of the chair, the
consuls and the houses that held most, to −20; it leans against their motions and against a commission for a house
already in office, and toward a general who marches. A house holding twice its share leans for the chair. When the
player holds an office that gives places (legates now, a governor's staff once entry 6 exists), the office's panel
shows who is owed. The Houses window shows each house's count, what it expects and what would answer it.

**How Imperator does it.** At launch a family getting less than its share (under 2% of the state's income) was scorned
and lost loyalty month by month ([launch wiki copy](https://www.magicgameworld.com/?p=32633)). 1.3 Livy (December
2019) cut families to three to five great families by rank, which expect a share of the posts (70% of the posts
divided among them in 2.0, 80% in the diary); each post short costs its members 5 loyalty, stacking, and they drift
toward one party; holding double the share makes a family grateful, with loyalty, lower wages and faster experience
([Office](https://imperator.paradoxwikis.com/Office); [diary
69](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2451574645873160849); [ACOUP
IIIa](https://acoup.blog/2024/08/24/collections-teaching-paradox-imperator-part-iiia-de-re-publica/)). 1.5.2 cut the
penalty by a fifth ([changelog](https://www.gog.com/forum/imperator_rome/changelog/page2)), and 2.0 has events where a
family head or a commander demands a post ([Steam news](https://steamcommunity.com/app/859580/allnews/)). Players
liked having fewer families but found the jobs system "too opaque"
([Overclockers](https://forums.overclockers.co.uk/threads/imperator-rome-paradoxs-next-grand-strategy-title.18820401/page-12)),
and some called the rota of appointments an irritation
([Steam](https://steamcommunity.com/app/859580/discussions/0/6352962881244050297)).

**The game now.** Part. A commission is voted against by a jealous house with its own candidate (docs/SENATE.md, "The
offices of the Reach"); "the obvious man" leans the chamber toward a house great in the post's order
(docs/REPUBLIC.md); a relative who loses blames the head (L4, closed); and an office now has legates to give, from any
house (docs/MILITARY.md, "A command by service"). No house keeps a grievance over years shut out, and other houses
never ask the player for anything (GAPS X2 in the Hard list). docs/ROADMAP.md, "Newer ideas" 5 (great houses share the
offices) is the same field.

**Closes.** X2 in the Hard list, in part (a house that asks something of the player).

**Size.** Medium.

**Why.** It turns the ladder and the commissions into patronage, which is what the Roman Republic was. A consul gets
something to buy quiet with, and a general looking to cross gets a list of likely friends. Imperator's lesson is in
the design: show the figure and its cause, keep it to the houses that matter, and let more than one thing answer it (a
place, a favour, a match). A house kept out only weighs toward a general who crosses; it never ends the Republic by
itself.

### 6. A governorship after office

**What it is.** In the year after a praetorship or a consulship, the Senate shares out the governorships by lot, or by
a vote that can be lobbied: the worlds taken from a nation (docs/BEYOND.md, stage 3), the unheld worlds of the rim, or
a lawless region's lanes as a command against raiders. Each is held for three years and can be prolonged or recalled
by motion. The governor, the player or a relative who goes for their own reasons, picks one charge for the term (entry
14 gives the stances: clear the lanes, assess fairly, squeeze it, raise crews), draws their pay from its dues to the
capital, commands its squadron by the legate rules, and may keep a share for their own purse. Each year adds renown,
clients there and devoted crews, which is what the Caesar road is made of. When they lay it down, the governed or a
rival house can charge them for what they took, as Sicily charged Verres in 70 BC (entry 13 keeps the record). Other
houses' former magistrates govern the rest, so the player watches rivals build a following on the rim. Under a throne,
the throne names the governors.

**How Imperator does it.** In every version, each region beyond the capital's has a governor named by the ruler. In
2.0 the governor's finesse adds 1.5% to the region's output for each point and scales the policy they pick by (finesse
+ 1) / 10; changing the policy costs 10 influence and some tyranny; most of the wage comes out of the region, so a
governed region pays a quarter less tax; the governor gains power base (2, plus 0.7 for each percent of the country's
people governed), commands the region's levies, and in 2.0 only loyal governors' regions give levies at all
([Office](https://imperator.paradoxwikis.com/Office);
[PCGamesN](https://www.pcgamesn.com/imperator-rome/good-now-2-0-marius)). A disloyal governor refuses to leave and
takes the region into a civil war. Players treat governorships as a problem of power, keeping family heads off them or
parking troublesome people in them ([Steam](https://steamcommunity.com/app/859580/discussions/0/6352962881244050297)).
A 2019 diary admits governors' attributes were "completely missed when developing the game", and finesse was then made
to count ([news archive](https://tuxdb.com/game/5497&gamenews=1)). The Livy Roman missions describe a province run by
a governor "usually prioritising their own profit", and end in a choice between winning it over and exploiting it
([diary 70](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2448197582035096542)).
Devereaux's sharpest complaint is that anyone young can be made a governor at once, with no ladder; his better model
has former praetors govern in turn ([ACOUP
IIIa](https://acoup.blog/2024/08/24/collections-teaching-paradox-imperator-part-iiia-de-re-publica/)). The Res Publica
mod (2022, about 1,560 subscribers) does that: only former praetors govern, the Senate gives out provinces at the
elections with the better magistrate getting the richer one, and a governor cannot be removed before the term ends
([Res Publica](https://steamcommunity.com/sharedfiles/filedetails/?id=2834280746)).

**The game now.** None, but planned. GAPS L3: "No censorship, tribunate, or province after office" (and
docs/LADDER.md, "Not in this piece"). docs/BEYOND.md stage 3, "What is conquered is governed", plans a subdued
nation's worlds as held for the Reach under a governor appointed by the Senate, with a garrison, a tribute and a
temper, says "a province is where a Caesar comes from", and names it as where R1, R6 and R16 come to rest.
docs/ROADMAP.md, "Newer ideas" 1 adds extortion and the trial at home. What a house takes from a nation is held today
as that house's own world (GAPS N8; the owner's decision 8). The pieces exist: governors of a house's own worlds
(docs/GALAXY.md, "Governors who scheme"), a region's dominant house that sets region laws (docs/POLITICS.md), the
legate rules that give a fleet to a person of any house (docs/MILITARY.md, "A command by service"), and the courts.

**Closes.** L3, its third part. N8 for conquered worlds, on the owner's answer to decision 8. It is also where R6's
governors and R16's rewards would rest.

**Size.** Large: a plan first. A governorship of a region of the Reach needs no answer from the owner; one of
conquered worlds waits on decision 8.

**Why.** Every part of this survey found it. It is the Roman career's missing last step (quaestor, aedile, praetor,
consul, then a governorship), it puts a person and not the state in charge abroad, and it gives the late game goals,
where reviewers found Imperator's aimless. It must be one design with BEYOND stage 3 and the legate, not three. Settle
first who decides what in a region (its dominant house's region laws against the governor's charge), keep the
governor's share small beside a house's income (docs/ECONOMY_REVIEW.md), and measure the Caesar rate.

### 7. After a civil war: clemency, the lists, or terms

**What it is.** When a march or a rising ends, the winner chooses for each house that fought against them. Clemency
means no fine; its head owes a favour, is marked as pardoned, and may join a plot later (Brutus and Cassius had been
pardoned). The lists mean half its treasury goes to the winner, its head is exiled or killed, and every other head
thinks less of the winner. The winner may mix the two. Over the next two to four years named senators argue for
clemency, for the lists, or for terms that share the offices; the player, as winner, senator or consul, backs one or
stays out, and those they back become friends and the others enemies. A march overtaken by the great war is settled
the same way. If the player won, they choose; another house's winner chooses by nature. A player on the losing side
who lives receives the choice: take the pardon and owe it, or flee to a state that has broken away.

**How Imperator does it.** In 2.0 a civil war is fought to the death with no peace. The winner spares the traitors,
who carry a lasting stain and give back some manpower, or executes them all, for a large rise in loyalty, a cost in
the people's happiness and every one of them lost ([Civil war](https://imperator.paradoxwikis.com/Civil_war)). 2.0.2
fixed civil wars that never ended or ended in a white peace for everyone, and 2.0.4 made them take in many disloyal
people instead of one ([Steam
news](https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid=859580&count=500&maxlength=0&format=json)).
Players found them a drain. Livy (1.3) turned great changes into short chains of events with a named family speaking
for each side, such as a tribe's reform into a republic or a monarchy, and Carthage's aftermath of a failed coup
([diary 71](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/3657414904102853760);
[diary 68](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2504491307421465740)).

**The game now.** Part. A march ends by fixed rules (docs/CAESAR.md, "How it ends"): if the general wins, the Senate's
sword is proscribed and forfeits three tenths of its treasury; if the general loses, their house loses 25 legitimacy
and four tenths. The edicts "An amnesty" and "Proscriptions" exist (docs/EDICTS.md) but are not tied to a war's end.
GAPS C4 in the Caesar road table: no proscription, amnesty or negotiated end. Q16: a march overtaken by the great war
ends with one line in the chronicle.

**Closes.** C4 in the Caesar road table; Q16.

**Size.** Medium. The acts exist as edicts; this ties them to the end of a war and writes the chain.

**Why.** Caesar's clemency and Sulla's lists were the two great choices of the age, and both belong to one person. Cap
the lists so the Senate is not emptied (a house's younger members survive), and give clemency a real later danger, or
it is the only sensible choice. Test that the chain can be reached with the scripted general (docs/QA.md). It follows
the Crusader Kings entry 4, which is about the war before it.

### 8. Nations that read the Reach's weakness

**What it is.** Each year every nation reckons the guard it faces: hulls at its march and within reach, the Reach's
other wars, fleets lost in the last two years, a general on the march. Under a warlike or grasping ruler a thin guard
tempts a strike, and a second nation may strike while the first is fighting. The nation's card shows its temptation in
words. As Admiral or holder of a march world, the player reads the card and keeps the guard; as consul, they post the
state fleet there.

**How Imperator does it.** Devereaux notes the AI exploits a rival's distraction ([ACOUP
I](https://acoup.blog/2024/07/19/collections-teaching-paradox-imperator-part-i-divisa-in-partes-tres/)). Invictus made
its rebuilt AI a headline (better war declaration and budgeting, and an optional "extremely aggressive" AI), and that
is much of why it has about 160,000 subscribers
([Invictus](https://steamcommunity.com/sharedfiles/filedetails/?id=2532715348); [its change
notes](https://steamcommunity.com/sharedfiles/filedetails/changelog/2532715348?p=4)).

**The game now.** Part. The Concord strikes when the Reach is divided by its own quarrels and thinks ill of it, and
great rulers begin wars on a timer (docs/BEYOND.md). GAPS N15: another war, or the Reach's fleets away or beaten, does
not tempt it; only one war beyond the Reach is fought at a time; two nations never press together.

**Closes.** N15, which is part of R10 (fourth on the worklist).

**Size.** Medium.

**Why.** The houses' posting at the marches, already built, becomes the answer to something, and the Reach's own civil
wars become dangerous from outside, as Rome's were. Two wars beyond the Reach at once lifts a rule the code relies on,
so measure the Republic's survival and the Caesar rate after.

### 9. Foreign gold in the Senate

**What it is.** Before a war motion against the Free Cities, or a nation they favour, the Cities send senators the
same gift that asks a favour that houses already send one another. The player is offered a sum to vote against the war
or to stay away. Taking it fills their purse and goes on the record, where a rival can find it and charge it before a
praetor; refusing costs nothing; exposing it gains public trust and standing, and the Cities think less of the Reach.
Other houses take it by nature (the greedy, the short of money). The Cities can also buy off a war before it is voted.

**How Imperator does it.** Imperator has no foreign hand in another state's senate: its diplomacy buys opinion with
gifts, and pays barbarians to raid elsewhere ([launch
diplomacy](https://www.magicgameworld.com/imperator-rome-diplomacy/); [gamepressure
guide](https://www.gamepressure.com/imperator-rome/rebellions-and-barbarians/zcc2af)). Invictus lets a ruler pay a
disloyal foreign governor to come over
([seduce_governor](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/character_interactions/seduce_governor.txt)).
The idea is Rome's more than Imperator's: Jugurtha's bribes in 111 BC, and his "a city for sale".

**The game now.** Part. Between houses, a gift kept is a favour owed, and a house that owes the proposer leans two
further for the motion (docs/WEALTH.md, "A gift can ask a favour"). A sitting praetor can be bought (docs/LADDER.md).
From a nation, nothing: GAPS N11, the Free Cities do not buy off a war before it is voted, or a house's vote.

**Closes.** N11, part of R10.

**Size.** Small. The gift is built; the Cities need a purse to send it from and a rule for when.

**Why.** It is cheap, it closes a row, and it makes the Senate's war vote a moment of private temptation for the
player. Keep the sums large and the Cities' purse finite.

### 10. The benches' wants for the consuls' term

**What it is.** At each consular election, the winners' benches each name one or two things they want done in the
term, from verbs that already exist: an Act of their platform, a commission for one of their people, a war beyond the
Reach, relief for a region, a censure of a house they hate. Each bench's approval of the sitting consuls is one figure
that starts again with every new consul. Delivering a want raises it: the bench's houses lean for the consul's
motions, are slower to annul their edicts, and back them at the next count. Refusing one, or vetoing the bench's own
Act, lowers it, and at its lowest the bench passes the consul over (ElectionSystem.BenchContest) or a member
challenges for its lead (FactionSystem.Challenge). As consul the player sees the wants and what each would cost; as a
senator they see what the consuls must deliver and can trade their votes for it.

**How Imperator does it.** Since 1.5 Menander (August 2020), and kept in 2.0, each of three parties has an approval of
the government: members gain 0.1 loyalty for each point above 50 and lose 0.3 for each point below, and the
seat-weighted sum is the Senate's support (51 passes an act; 30 to 50 forces it at a cost in tyranny; under 30 blocks
it). Each term every party has an agenda (a war, a law, an appointment, a dismissal, a trial, a city), and meeting it
gives 10 or 20 approval; the ruling party's unmet agenda is forced once support reaches 50, and the consul accepts it
or vetoes it at −50 approval ([Government](https://imperator.paradoxwikis.com/Senate); [1.5
notes](https://www.allpatchnotes.com/imperator-rome-1-5-patch-notes-epirus-pack-new-missions-galore)). Before 1.5 the
Senate had five factions and approval was decided act by act ([launch-era
guide](https://gameplay.tips/guides/5640-imperator-rome.html)). Players call the agendas bothersome and say approval
is easy to game (give seats to one party and please it), and prized a dictatorship for freeing them from the ruling
party's agenda ([Steam, 2024](https://steamcommunity.com/app/859580/discussions/0/4356746484460744297)). Devereaux:
approval measures the player, and never starts again at an election ([ACOUP
IIIa](https://acoup.blog/2024/08/24/collections-teaching-paradox-imperator-part-iiia-de-re-publica/)).

**The game now.** Part. Benches have platforms of Acts and move them themselves (docs/SENATE.md, "The chamber
legislates"), every bench's lean is shown before a vote, the consuls have a veto (docs/REPUBLIC.md), and a pledge of
votes exists. But no bench holds a view of the sitting consuls, nobody is elected on a programme, and a consul owes
the bench that elected them nothing (src/Core/Systems/FactionSystem.cs and ElectionSystem.cs keep no approval or
agenda).

**Closes.** No row.

**Size.** Medium.

**Why.** A consulship becomes a term with promises to keep and a price for breaking them, which is the stuff of Roman
politics (Caesar's land law for Pompey's veterans, 59 BC). Imperator's lessons are all in the design: few wants, drawn
from verbs that exist, refusable at a stated price, and an approval that belongs to one consul and starts again each
term. Other houses' consuls must pursue their own benches' wants, or it is the player's alone.

### 11. Clients who owe more than money

**What it is.** When a beaten nation is made a client, its patron (the house whose fleets won the war, often the
player as general) picks one of three ties. A tributary pays as now and may stop, which gives the Reach a cause. An
ally pays less and sends a squadron under its own commander when its patron or the Reach is at war. A march pays
nothing but turns raids back at its border, and the patron's officers serving there learn (a posting for a younger
relative). The client's ruler is a named person with a regard for the patron and a loyalty out of 100, shown with its
parts. It rises with gifts, a marriage and honouring the ruler in the Senate, and falls with demands, wars the client
is dragged into, and the Reach divided; under 50 the tie can break. The patron can ask for what the tie owes, take the
ruler's heir into the household as a ward, or arrange a match (entry 22). An old client ruler with no heir may leave
the realm to the Reach or to the patron's house by will, as Attalus III left Pergamon to Rome in 133 BC. Annexing a
client takes years and ends its dues, so a client is worth keeping as a client.

**How Imperator does it.** From launch: a tributary pays part of its income, can cancel at any time (giving a cause
for war) and is never absorbed; a feudatory and a client state join their overlord's wars and pay income and manpower,
and can be absorbed after 10 years at very high opinion; a subject under 50 loyalty may join rebels ([gamepressure
guide](https://www.gamepressure.com/imperator-rome/diplomacy/z6c388); [launch
diplomacy](https://www.magicgameworld.com/imperator-rome-diplomacy/); [RPG
Codex](https://rpgcodex.net/forums/threads/imperator-rome-the-new-grand-strategy-from-paradox.121924/page-22)). The
2.0 files have thirteen kinds, among them a march (+15% defence, and its overlord gains military experience) and a
mercenary city (cheaper hired armies for its overlord) ([subject
types](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/subject_types/00_default.txt)). The Roman
missions let Rome inherit client states from dying kings ([diary
70](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2448197582035096542)). In 1.2
players found the opinion thresholds unreachable because the tooltip and the opinion tab disagreed ([Steam,
2019](https://steamcommunity.com/app/859580/discussions/0/3166519278494909130/)). Devereaux shows feudatories gave
little and were absorbed fast by player and AI alike, so Rome's system of allies became a quirk of the start ([ACOUP
I](https://acoup.blog/2024/07/19/collections-teaching-paradox-imperator-part-i-divisa-in-partes-tres/)). Full
Mechanical Overhaul adds a screen for a subject, with its focus, its loyalty traded against what it pays, and a levy
of its people ([Full Mechanical Overhaul](https://steamcommunity.com/sharedfiles/filedetails/?id=2805839904)).

**The game now.** Part. A beaten nation can be made a client that pays the chair and its patron half as much again,
and throws the treaty off under a great or warlike ruler (docs/BEYOND.md, "What kind of peace it is";
src/Core/Systems/NationSystem.War.cs). GAPS N26: a client owes money and nothing else, sends no fleet, cannot be asked
for anything, and its ruler is nobody its patron can call on. A client was made once in sixty games.

**Closes.** N26, part of R10, on the owner's decision 20 (whose client a client is).

**Size.** Medium.

**Why.** A client becomes a relationship one person keeps, worth more as a client than as land, which is Devereaux's
lesson. Three kinds, not thirteen. Clients are rare now, so it pays most once wars beyond the Reach are more decisive.

### 12. The crowd's favour, a person's own

**What it is.** Each person of standing gets a name with the crowd from 0 to 100, seeded from charisma and renown.
Renown is what the senators know of you; this is what the forum thinks of you. It rises with games given as aedile or
paid from one's own purse, a dole in a lean year, pleading for someone who cannot pay, and a triumph. It falls with a
defeat, a conviction, and a smear put about by a rival (a new act: one agent, traced one time in three). It fades
toward 20. It counts at the count (first guess: a quarter of the merit), stands in for part of the charisma test in
the "Loved" gate, lets a holder of the powers resist a motion to end them (entry 4), decides whether the capital grows
restless when that person is charged, recalled or killed, and is the road a tribune would rise by (Crusader Kings
entry 20).

**How Imperator does it.** Every character has popularity, how the people see them, beside prominence, the fame that
comes from posts and decides who leads a party ([Character](https://imperator.paradoxwikis.com/Character); [launch
stats](https://www.magicgameworld.com/?p=32605)). Popular characters are likelier to be elected. Sponsor Games spends
personal wealth on it, Smear Reputation takes 25 of a rival's for 2 tyranny, lost battles cost some (2.0.5 spares a
general who was heavily outnumbered), a ruler above 50 lowers tyranny, and a dictator is forced out only when
unpopular ([Character interaction](https://imperator.paradoxwikis.com/Character_interaction); [Steam
news](https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid=859580&count=500&maxlength=0&format=json)). 1.2
gave commanders more for taking cities and cut what a naval victory gives to a tenth
([changelog](https://www.gog.com/forum/imperator_rome/changelog/page1)). Devereaux calls it "a modest bonus", far too
weak to stand for the Roman popular vote, and the people the game's largest absence ([ACOUP
IIIa](https://acoup.blog/2024/08/24/collections-teaching-paradox-imperator-part-iiia-de-re-publica/)).

**The game now.** Part. Renown (docs/LIFE.md; LifeSystem.Renown, from rank and deeds) counts a sixth in the ladder's
count. An aedile's games give the house 2 legitimacy and the person nothing (docs/LADDER.md). The commons are an
estate whose regard is for houses (docs/POLITICS.md). "Loved" reads charisma, deeds and crews (docs/CAESAR.md). Nobody
has a standing with the crowd of their own, and nothing lowers one. docs/ROADMAP.md, "Newer ideas" 3 (the Commons) is
the field.

**Closes.** No row. It prepares the tribunate (L3, Crusader Kings entry 20).

**Size.** Medium.

**Why.** A person can be the people's own against the old blood, as Clodius and Caesar were, which no other figure in
the game lets them be. It must look different from renown on the screen and mean something different, and it must not
make elections a matter of paying for games: cap it and let it fade. Other houses' people need it worked out simply
(X1 in the same gap in many places).

### 13. A record of what a person has taken

**What it is.** Each person carries a figure for what they have taken: every share kept at a magistrate's desk, every
skim as a governor, a bribe accepted, each year a steward's hand is in the till (first guess: 1 for every 100
credits), fading by a tenth a year. An audit, a charge or a spymaster finds it, and a charge's facts are read from it
instead of drawn by lot. It makes a person cheaper to buy. The people of a world they governed, or a region they
served as a magistrate, hold it against them: unrest on that world, and less trust in the magistrates there, so a
region can tell a quaestor who robbed it from a court that wronged it. The head may give a governor of the house leave
to help themselves to a share of the world's yield: the governor thinks better of the head each year, the world's
unrest rises, and their figure grows; taking the leave back costs what it gave. For the player, their own record is
their danger when they are charged, and as quaestor or at an audit they find other people's.

**How Imperator does it.** Every character has corruption, a willingness to use underhanded means (launch). It raises
their wages (up to two and a half times from 1.4.1), raises every cost while a corrupt ruler reigns, sharply raises
unrest in a corrupt governor's cities, and makes a trial likelier to convict ([launch
characters](https://www.magicgameworld.com/?p=32633);
[changelog](https://www.gog.com/forum/imperator_rome/changelog/page2); [Character
interaction](https://imperator.paradoxwikis.com/Character_interaction)). Free Hands raises a governor's loyalty and
corruption together, and from 1.5 dismissing such a governor takes the loyalty back; 1.2 added sanctions that lower
corruption; bribes add corruption and, from 1.4, also cost political influence ([launch
interactions](https://www.magicgameworld.com/?p=32685);
[changelog](https://www.gog.com/forum/imperator_rome/changelog/page1); [Steam,
2024](https://steamcommunity.com/app/859580/discussions/0/4517758633227882315)). Invictus adds a fine on the corrupt
and a pension of an estate for a loyal but corrupt office-holder ([Invictus
interactions](https://github.com/SnowletTV/Invictus/tree/main/common/character_interactions)).

**The game now.** Part. Taking money happens in four places, and each is forgotten afterwards: a magistrate's "keep a
share" (src/Core/Systems/LadderSystem.Duties.cs, found one time in four), a steward's hand in the till
(docs/HOUSEHOLD.md, "Honesty"), a governor who thinks −20 or worse of the head (Economy.GovernorSkimThreshold), and a
bought praetor (docs/LADDER.md). GAPS F1: a magistrate removed for taking money is barred for five years, "and that is
all". L11: the facts of a case are drawn by lot. L13: a region does not tell a court that wronged it from a quaestor
who robbed it. X1 in the Hard list: a ground's facts are not read from the record.

**Closes.** L11 and L13 in part; X1 in the Hard list in part.

**Size.** Medium.

**Why.** It gives the trial at home after a governorship (entry 6) and the courts something real to prove, and it is a
cost that belongs to the person. It sits beside Crusader Kings entry 15 (Ill fame) rather than inside it: ill fame is
what the Reach thinks of your word, and this is what you took. Show it on the person's card with its parts; a figure
moved by rules nobody can read is what players disliked in Imperator.

### 14. A governor's way of running a world

**What it is.** Each governor, whether a relative on one of the house's worlds or the player when they govern (entry
6), takes one stance for their time in the chair, chosen by their nature: line their own purse (the world yields a
little more, the governor keeps a share, unrest rises), push trade (prosperity settles higher), a hard hand (unrest
falls fast, the yield falls, people leave), a light hand (unrest falls, the yield falls), build it up (the system's
development grows faster), or hold the march (manpower and defence). Each is scaled by the governor's administration.
The greedy and the indebted take the purse, the cruel or ambitious the hard hand, the kind the light hand. The head
may ask for a change: a dutiful relative agrees and thinks a little less of the head, and a wilful one refuses. The
stance shows on the world's prosperity and unrest lists as a cause with the governor's name. In the same change,
development reads more than infrastructure: a point for every few buildings standing, the governor who builds it up,
and a point lost each year where the region's lawlessness is over 60.

**How Imperator does it.** Launch had nine governor policies; 1.1 added moving people toward or away from the capital;
1.2 added harsh treatment and made each policy's strength scale with the governor's finesse ([gamepressure
guide](https://www.gamepressure.com/imperator-rome/economy-taxes-buildings-and-policies/z8c1d0); [news
archive](https://tuxdb.com/game/5497&gamenews=1)). A governor picks one on appointment by traits, corruption and
loyalty: an honest governor never takes the policy that fills their own pocket, and one under 40 loyalty leans to it;
overriding the choice cost points and tyranny, and a disloyal governor could not be overridden ([gamepressure
guide](https://www.gamepressure.com/imperator-rome/governors/z8c30b)). The rules file gives the effects: taking wealth
+10% tax and commerce and a higher wage for the governor; encouraging trade +10% commerce and food; a harsh hand +0.3
loyalty a month but −40% output, and people leave; local autonomy +15% happiness and −40% output; borderlands +25%
manpower and +20% defence; a civilising effort raises the territory's development ([governor
policies](https://raw.githubusercontent.com/SnowletTV/Invictus/master/common/governor_policies/00_default.txt)). Most
buildings and roads also raise development ([ACOUP
IIb](https://acoup.blog/2024/08/09/collections-teaching-paradox-imperator-part-iib-built-in-a-day/)). Players
complained governors chose badly, such as converting a province already converted ([Steam,
2021](https://steamcommunity.com/app/859580/discussions/0/3108019435238108299/)), and Invictus retuned the AI's
choice. Devereaux found a governor's personality "minor enough to be ignored" and their skill what mattered ([ACOUP
IIIa](https://acoup.blog/2024/08/24/collections-teaching-paradox-imperator-part-iiia-de-re-publica/)).

**The game now.** Part. A governor's administration adds 4 prosperity a point, an able one finds 100 credits, one at
−20 skims, one at −40 drags prosperity down, and an ambitious one may declare the world for themselves
(docs/GALAXY.md, "Governors who scheme"; src/Core/Systems/Economy.cs). These follow regard alone: a governor chooses
nothing, and local laws are the holder's decrees (docs/POLITICS.md, "Local law"). GAPS U9: development moves only with
infrastructure, and no governor's skill moves it (docs/BUILDINGS.md, "Development").

**Closes.** U9.

**Size.** Medium. The change to development alone is small and can come first.

**Why.** A governor becomes a person with a way, the player can see a rival's greedy cousin make a world restless and
open to a neighbour's free port, and choosing who governs becomes a real choice. The stance is the governor's own
conduct and purse, while the decree stays the head's, so keep it apart from local law. The head asks and never orders.

### 15. The term and the bar on standing again, made law

**What it is.** A new Act with three options (the custom, shorter terms, longer terms), and a second covering standing
again (one term out, which is the custom; a ten years' wait; or free). A new motion grants one named person a waiver:
to stand again at once, as Marius was consul five years running (104 to 100 BC), or to stand under age, as Pompey did.
Benches lean by interest: a house with a sitting consul wants re-election free, and its rivals want the wait. A consul
who wants another term must win the law or the waiver first, and the player sees rivals try the same. A consulship for
life is not an option of the Act; it stays the player's alone, through the powers for life (rule 2).

**How Imperator does it.** In 2.0 a republic's electoral laws are Extended Terms (5 years more), Shortened Terms (3
fewer) and Lifetime Elections, passed by the Senate like any law; one party agenda is the term's length, with one
party for longer and another for shorter; re-electing the same party costs 2 tyranny; and in 1.x nobody who had ruled
in the last ten years could stand ([Laws](https://imperator.paradoxwikis.com/Laws);
[Government](https://imperator.paradoxwikis.com/Senate); [launch-era
guide](https://gameplay.tips/guides/5640-imperator-rome.html)). 1.1 added age limits for a republic's rulers ([diary
50](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2405396896222894868)). The road
ends in a hereditary line: Request needs 80% of the Senate in two parties, 50 tyranny, 150 influence and 25 stability,
and Demand means a civil war. Players find Request slow and term limits too easy to change, and ask why the road runs
only one way ([Steam, 2024](https://steamcommunity.com/app/859580/discussions/0/4356746484460744297)); Devereaux calls
the one-way road teleology, since Sulla laid his power down ([ACOUP
IIIb](https://acoup.blog/2024/08/31/collections-teaching-paradox-imperator-part-iiib/)).

**The game now.** Part. The Law of the Years moves only the ages for the ladder and the consulship (docs/SENATE.md,
the table of Acts). The consular term is fixed (ElectionSystem.Every, ten turns), a house that was consul must sit a
term out (ElectionSystem reads consul_term), and nobody holds a rung twice (docs/LADDER.md). The only lawful road to a
throne is the vote for the powers for life (docs/EMERGENCY.md).

**Closes.** No row.

**Size.** Medium. The term is a constant other systems read (the ladder's year, when edicts lapse, the election owed
after the powers), so change it with tests.

**Why.** History's road to rule by one person ran through waivers and repeated terms, and it is a fight the player can
join from either side, for or against a rival. Never let other houses reach a life term by this road.

### 16. Clout that comes from your people

**What it is.** The influence a person has to spend each year is read from who stands behind them. Keep the base two,
and add one a year for each sitting magistrate, office of the Reach or committee chair among their kin or clients, so
long as the holder thinks +20 or better of them; a relative in office who dislikes the head gives nothing. Later,
where a fixed "2 influence" stands for a real price, pay the real price instead: speaking for a candidate spends a
favour or a friend's patience, and a charge that fails costs standing.

**How Imperator does it.** At launch, four kinds of "monarch power" came from the ruler's random stats and were spent
at a token price on unrelated acts. A 2019 diary calls it "one of the more controversial aspects of the game at
release", with two complaints: no control over where it came from, and no sense in what it bought ([diary
60](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/3556075649733826054)). 1.2
Cicero (September 2019) replaced it with political influence produced by office-holders according to their loyalty (in
2.0, up to 0.25 a month from each office), and many acts now cost tyranny, stability, corruption or gold instead, some
nothing where the loss is the price ([Political influence](https://imperator.paradoxwikis.com/Political_influence);
[Cicero notes](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2406534841142341354);
[Rock Paper Shotgun](https://steamstore-a.akamaihd.net/news/externalpost/rps/2504491307422278488)). The director's
road map after launch moved from paying for an instant result to paying "to nudge it towards that result over time"
([road map](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2432417221739862764)).

**The game now.** Part. Every house gets a flat two influence a turn, capped at six (ActionPools.ForPlayer and ForNpc
in src/Core/State/ActionPools.cs), plus one or two from a few yearly duties and a committee chair
(LadderSystem.Duties.cs, LadderSystem.Committees.cs). Dozens of commands cost a flat 2. docs/PLAYTHROUGH.md: players
found the two kinds of influence (the yearly pool and the house's clout) confusing.

**Closes.** No row.

**Size.** Medium for the yearly figure. Large to reprice the acts, which is the owner's call, since it touches every
balance figure.

**Why.** The ladder, marriages and patronage then pay in the one currency every verb costs, and a newcomer with no
friends really has less to spend. The pools were tuned as a constraint (docs/PROCESS.md), so probe the action economy
for the player and the other houses together.

### 17. Rival chiefs beyond the rim

**What it is.** The Khanates as three to five named chiefs, each a person with a fleet, a few worlds and a regard for
the Reach. Each year the strongest presses the others, and when one has beaten or bound all but one, a Great Khan is
proclaimed. The Reach sees the gathering in reports from the march. A house's spymaster or envoy can fund a weaker
chief for years, which slows or breaks the rise; a beaten chief may come over with their fleet as the client of the
house that takes them in. The same rule, one per nation, can back a province of the Hollow Crown to fall away as a
small nation of its own, or a faction of the Concord's admirals to delay its strike. If it is traced, the nation
thinks less of the Reach and has a cause against that house only. For one person: through their own council, as the
holder of a march, as Admiral raiding the rising chief, or as consul acting for the Reach.

**How Imperator does it.** Tribes are led by clan chiefs who raise retinues of their own; 1.1 let the tribe limit what
a chief may raise ([diary
52](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2408775863533878321)), and from
1.1 civil wars weigh each disloyal person's power against a line ([diary
50](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2405396896222894868)). Invictus
grants a disloyal chief who governs far from the capital autonomy over their land
([grant_clan_autonomy](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/character_interactions/grant_clan_autonomy.txt)).
Supporting rebels in a neighbour (launch) lowers its people's happiness and gives it a cause for war against you
([gamepressure guide](https://www.gamepressure.com/imperator-rome/diplomacy/z6c388)). Imperator never made a uniter
out of the chiefs; that part is new. Players before launch wanted real pressure from the tribes ([RPG
Codex](https://rpgcodex.net/forums/threads/imperator-rome-the-new-grand-strategy-from-paradox.121924/page-22)), and
after 2.0 PCGamesN still found tribes "painfully underdeveloped".

**The game now.** Part. A Great Khan is a roll at an accession or an acclamation after a war won (docs/BEYOND.md, "The
Vashti Khanates on the map"). GAPS N25: nothing leads up to it, with no rival khans and no gathering to watch or break
up. N17: the Crown has no provinces that fall away and no pretenders. Inside the Reach the player can stir a region
and the camps (docs/BREAKAWAY.md), but nothing reaches a nation; docs/BEYOND.md stage 4 plans "a nation backs a
pretender or a breakaway for a price".

**Closes.** N25; N17 in part. Both are part of R10.

**Size.** Medium. Keep it to the Khanates' own content first: stage 4 puts each nation under the forms of government
(G1), which the owner has said not to build yet.

**Why.** An invasion becomes something the Reach can see coming and do something about, and one person's council can
reach beyond the rim. It pairs with the Crusader Kings entry 18 (the demand before an invasion).

### 18. A people on the move

**What it is.** When a nation falls, a great ruler dies or the Concord crushes a neighbour, a people takes ship: a
host with a leader, many families and few warships gathers beyond a march, where the houses can see it, then comes
through and asks the Senate for a world on the rim. The Senate may grant one (they settle as a minor house that owes
crews to the region's levy), send them on to another march, or refuse, and then they force their way and fight by the
ordinary rules, as the Cimbri did from 113 to 101 BC and the Helvetii in 58 BC. A house may take them in on an empty
world of its own, gaining people and crews and paying their keep for five years. As governor of the march world the
player meets them first and can promise land the Senate may not honour; as a general they can make a name destroying
them, as Marius did; as a senator they can move the grant.

**How Imperator does it.** Migratory tribes can move their people and settle, an expensive but quick way to colonise,
and pillage on the way (pre-launch diary, quoted on [RPG
Codex](https://rpgcodex.net/forums/threads/imperator-rome-the-new-grand-strategy-from-paradox.121924/page-22)). A
horde that has taken a city can be settled in your own cities, keeping its culture, or offered client status, made to
surrender, or paid to go elsewhere ([gamepressure
guide](https://www.gamepressure.com/imperator-rome/rebellions-and-barbarians/zcc2af)). Invictus adds "historical but
dynamic" invasions, with game rules to turn them off
([Invictus](https://steamcommunity.com/sharedfiles/filedetails/?id=2532715348)).

**The game now.** None. Nations raid, strike and are beaten (docs/BEYOND.md); when a Great Khan dies the khanates go
back to raiding. GAPS N19: a nation that is ended leaves nothing behind. Nobody moves between worlds at all.

**Closes.** N19 in part (a remnant that goes somewhere) and N25 in part (something that leads up to a war). Both are
part of R10.

**Size.** Medium. Reuse the nations' fleets and the founding of a free hold (docs/GALAXY.md, "Risings and free holds")
rather than a new system, and keep it to about one host a game.

**Why.** The Republic's great dangers from outside were peoples on the move, not states, and each gave a general a
name. It is one event with real choices for the Senate, the march and the general.

### 19. A fleet's record and its honours

**What it is.** Every fleet keeps a short service record (battles fought and won, worlds taken, raids met at a march,
who commanded) and earns honours, at most five, each worth a little, for deeds said in advance: the house's first
fleet, a battle won outnumbered, a battle won in a belt or at a gate, a fortress world taken, a raid met at a march, a
triumph for its commander. Marks of shame come for breaking and fleeing, mutiny, and firing on the Senate's fleets,
and a later honour washes one out. They stay with the fleet whoever commands it. The standard taken in battle, already
built, is the first honour. When the player is made legate of the house's oldest fleet they take on a name, and losing
a famous fleet is news.

**How Imperator does it.** 2.0 Marius (February 2021) made legions lasting bodies "that can have [their] career
tracked", and the Heirs of Alexander pack added honours and dishonours: lasting bonuses earned by deeds, such as
fighting on a given ground, a siege won with engineers, a forced march, drill at half experience, a triumph after a
won battle ([PCGamesN](https://www.pcgamesn.com/imperator-rome/2-0-marius-patch-notes); [achievement
guide](https://steamcommunity.com/sharedfiles/filedetails/?id=3253981079); [Steam,
2023](https://steamcommunity.com/app/859580/discussions/0/3879344844059565881/)). Players liked the flavour and hated
the luck: a guide calls the ten-honour achievement "probably the only RNG achievement", a mod removed the luck and set
plain gates (a great battle of 15,000 a side, a siege by 20 cohorts of one kind, a winning streak) ([Legion
Distinctions Overhaul](https://steamcommunity.com/sharedfiles/filedetails/?id=2575888248); [its
README](https://github.com/x3igh7/imperator_legion_overhaul)), and one player said a single dishonour "totally
deflated my interest" ([Steam, 2025](https://steamcommunity.com/app/859580/discussions/0/594022553707675015/)).

**The game now.** Part. Fleet.Veterancy counts battles survived, 0 to 5, worth 5% each (src/Core/State/Fleet.cs); a
standard taken in about one victory in four is flown from the flagship and makes that fleet more loyal each year
(docs/WEALTH.md, "Heirlooms"); the account of a war is kept by war and house (docs/MILITARY.md). Devotion belongs to
the commander, not the fleet.

**Closes.** No row. N27 (a named flagship as an heirloom) is the nearest.

**Size.** Small to medium.

**Why.** Fleets become things with names that a family cares about across generations, cheaply, from records that
mostly exist. Never a roll; small capped bonuses; a mark of shame must be fair and able to be washed out. Keep
veterancy as skill and honours as name, so the two do not count twice.

### 20. Hulls are laid down at a yard

**What it is.** A yard is a building of three grades on a world of the house. Corvettes and frigates can be laid down
at any yard, cruisers at grade II, and carriers and battleships at grade III or on a Shipyard World. A larger yard
lays hulls down and refits faster, and the house's shipwright works there. A yard under siege lays down nothing, and
whoever storms the world has the yard. The player meets it through their own house's worlds, and as Admiral of the
Reach through the capital's yard for the office fleets.

**How Imperator does it.** Before 2.0, ports sat at fixed historical places and could not be built, moved or removed.
2.0 made the shipyard a building that raises a territory's port level, and ships are built only in ports ([diary
111](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/3856733878309096878)). In the
files a port can be raised six times in one territory, each level adding a quarter of a trade route and 15% faster
building ([buildings](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/buildings/00_default.txt)). No
complaint about it was found.

**The game now.** Part. RaiseFleetCommand lays a hull down at any holding of the house that can pay
(src/Core/Commands/FleetCommands.cs). Only the carrier needs a Shipyard World, and the battleship needs rank 3 and the
goods (content/fleets/ship_classes.json). The Orbital Foundry and a yard city's Slipways yield materials and gate
nothing (docs/BUILDINGS.md, docs/CITIES.md). GAPS N29 names exactly this.

**Closes.** N29.

**Size.** Medium.

**Why.** Losing the yard then matters more than losing a squadron, the siege of a yard world becomes the aim of a
campaign, and the Admiral of the Reach has something to keep. Three grades, not six levels in every place. Other
houses must build and value yards (X2 in the same gap in many places), and a landless house's road to a command by
service must not depend on one.

### 21. Citizenship one region at a time

**What it is.** In place of the one Act for the whole Reach, a senator may move to admit one region's people. If it
carries, that region's allegiance settles higher, its worlds give more to the levy, and its houses weigh a little more
in the Senate. Each region admitted makes the core's old blood and commons a little less content, and that cost fades
over twenty years. A region refused while others are admitted loses allegiance faster, and its houses lean toward
breaking away: the Social War of 91 to 88 BC, after which Rome gave way. A governor, or a house with worlds in the
region, gains by sponsoring it. It works by region, so it needs no peoples on each world.

**How Imperator does it.** 1.5 Menander (August 2020) let each culture in a state be given rights on its own, up to
citizen. An integrated culture shares the main culture's rules, can rise, and in 2.0 gives levies, but each integrated
culture lowers the happiness of all of them (4% each after 1.5.2) ([ACOUP
IIa](https://acoup.blog/2024/07/26/collections-teaching-paradox-imperator-part-iia-pops-and-chains/);
[PCGamesN](https://www.pcgamesn.com/imperator-rome/good-now-2-0-marius)). Players called integration "a temporary
solution" because of that cost, and complained that the penalty never settles ([Steam, February
2021](https://steamcommunity.com/app/859580/discussions/0/3111395113769962164/); [Steam, March
2021](https://steamcommunity.com/app/859580/discussions/0/3108019435238108299/)). Devereaux calls the citizenship
model weak, and notes that Rome's allies gave soldiers, not tax ([ACOUP
I](https://acoup.blog/2024/07/19/collections-teaching-paradox-imperator-part-i-divisa-in-partes-tres/)).

**The game now.** Part. "The Citizenship of the Reach" is one Act for all regions at once: old worlds only, the
custom, or the whole Reach (docs/SENATE.md). Each region names a culture (content/galaxy/the_reach.json), read only
for a house's starting laws, its names and a courtship bonus. GAPS L13: a region's belief does only one thing, to its
allegiance. GAPS R13: peoples and castes are not for now (the owner, 2026-11-17).

**Closes.** L13 in part.

**Size.** Medium. Put it to the owner first, since R13 is theirs.

**Why.** It is one of the Republic's great fights, and the player can move it, back it or block it, without any new
system of peoples. Make the cost fade, which is what Imperator's players asked for.

### 22. A match with a nation's ruling family

**What it is.** At peace, with a nation that does not hate the Reach, the head may seek a match for a grown relative
in a khan's or the Crown's family, or a relative may seek it and ask the head's blessing. Under the Republic it is put
to the Senate, which may forbid it, and a house married abroad is watched, as Antony was. It gives the nation's regard
for that house, no raid on its worlds while the marriage holds, a marriage as a term a nation can offer in a peace,
and a child who can be put forward as a pretender when that nation's ruler dies. A war with that nation strains the
marriage and casts suspicion on the house.

**How Imperator does it.** In the base game a marriage is arranged only inside one's own state. Invictus adds a
proposal to another state: both must be monarchies, and the one proposed must be of that ruler's family, unmarried and
ten or older; the other state accepts only if its opinion passes a bar that is higher when the proposer is the larger
state, and legitimacy counts; a betrothal is kept
([propose_marriage](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/character_interactions/propose_marriage.txt)).
What a match then brings is set in its events, which were not read.

**The game now.** None. A nation's house is an outsider (World.Outsider in src/Core/State/World.cs), and courtship
never looks at one (src/Core/Systems/CourtshipSystem.cs). docs/BEYOND.md, decision 11: "Envoys, marriage, bribery,
capture and refuge are stage 4". GAPS N22: nobody asks for a marriage in a peace.

**Closes.** N22 in part, and part of R10; on the owner's decision 11.

**Size.** Medium.

**Why.** A nation gets a face one person deals with through their own family, which is how the Republic's great
figures dealt with the kings around them. The Republic's one marriage at a time holds, M4 (where a spouse lives) is
still the owner's, and it must not become a way to take a nation without a war.

### 23. The censorship

**What it is.** Every fifth year, with the ladder, two censors are elected from former consuls for eighteen months.
They revise the roll: strike a house under a scandal from its door to the Senate for five years, or enrol a house by
the census without the fee. They let the contracts: the gathering of a region's dues and the supply of the Reach's
fleets for five years, to a house of commerce that bids; it pays the capital now and collects later, and if it
squeezes the region, allegiance falls and the Treasurer's budget shows it. And they take the census, which moves the
three orders. A censor of the player's house decides; other houses' censors decide by nature. Holding it gives the
house standing and pleases its bench.

**How Imperator does it.** In 2.0 the censor is one of eight appointed offices of a republic. It gives the holder's
party approval for each point of skill, prominence, more Senate influence, and the most family prestige of any office
(players put it at about twice the others' rate) ([Government](https://imperator.paradoxwikis.com/Senate); [Steam,
2024](https://steamcommunity.com/app/859580/discussions/0/4517758633227882315)). Its description speaks of the census
and public morals, but the game gives it neither. The Res Publica mod elects two censors a year ([Res
Publica](https://steamcommunity.com/sharedfiles/filedetails/?id=2834280746)), and Devereaux notes the post went to
former consuls ([ACOUP
IIIa](https://acoup.blog/2024/08/24/collections-teaching-paradox-imperator-part-iiia-de-re-publica/)). So Imperator
gives only the office's name and place; most of this is our own design on the Roman office.

**The game now.** Part. The office is not built (GAPS L3). But a seat comes by four doors, and the purse's door is a
census: a house notable in Commerce pays 6,000 credits and is enrolled (docs/REPUBLIC.md, "Four doors to a seat"). The
Senate charters the great companies (docs/ECONOMY_MARKET.md). docs/ROADMAP.md section 3: commerce gets nothing from
the state, with no tax-farming and no contracts to supply the fleet.

**Closes.** L3, its first part.

**Size.** Medium for the office and its roll. Large with the contracts, which are the Crusader Kings entry "Contracts
and tax-farming".

**Why.** A last rung above the consulship, for one person, with real business that touches every house. Contracts are
a new money flow and must be probed against the economy (docs/ECONOMY_REVIEW.md). Striking a house off touches U4 in
the new-house table (what having no seat forbids), which is the owner's.

### 24. Raiders with a home and a price

**What it is.** Each raider band has a haven, a named world. The holder of a lawless world may set a local law that
shelters one: a share of what the band takes from worlds not its own (first guess: a third) and cheaper goods, against
the region's lawlessness and a scandal if it comes out (found one year in four: a censure, regard lost with every
raided house, and grounds for war). The band's captain is a person. A governor or a head with a fleet nearby can pay
them to move on, or point them at a rival (traced one time in three, like stirring), offer them an empty world to
settle as a minor house that owes ships for ten years, or take the captain into service as a legate. After two idle
years a band raids. The Senate can vote one person, the player as Admiral or praetor or a rival, a three-year command
against the havens with ships drawn from every house; each haven taken adds renown and sway. A house without worlds
can live as a haven's hired captain.

**How Imperator does it.** 1.1 Pompey, named for the man given the great command against the pirates, reworked launch
pirates, who blockaded ports for ever and were "not very engaging". They now live in fixed havens, can be hired with
their commander, raid an unfortified port elsewhere after two years without work, and give their port's owner a small
benefit; one law outlaws piracy and gives a cause for war on anyone who shelters it, and another condones it and opens
a haven in your own port ([diary
54](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2411028931154342731)). In the
2.0 files a haven costs a state's standing abroad and can be made once in ten years, and an army roots one out only
under the law against piracy ([pirate
haven](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/unit_abilities/construct_pirate_haven.txt)).
Barbarians could be paid to raid elsewhere, or settled ([gamepressure
guide](https://www.gamepressure.com/imperator-rome/rebellions-and-barbarians/zcc2af)).

**The game now.** Part. Raiders gather where a region's lawlessness passes 60, belong to nobody, raid prosperity and
are hunted for legitimacy (src/Core/Systems/PirateSystem.cs; docs/GALAXY.md, "Raiders"). A holder can raise
lawlessness on purpose with the free port law, modelled on Delos, the pirates' mart (docs/POLITICS.md), and the chair
with letters of marque (docs/LAWS_REVIEW.md). The edict "The lanes closed to raiders" is the Lex Gabinia, but as a
flat cut to lawlessness across the Reach, not a command given to anyone (docs/EDICTS.md). The event "The Pirate King's
Offer" offers a letter pointing them at an enemy, which moves only counters (content/events/rings.json). GAPS R2:
nothing is hidden.

**Closes.** R2 in part (a hidden hand that can come out); N30 in part.

**Size.** Medium.

**Why.** Pompey's command of 67 BC is a career for one person, and the shady head who shelters raiders is its other
side. Cap havens at one a house and one a region, and make paying raiders onto a rival traceable and chargeable, or it
becomes the cheapest weapon in the game.

## What it adds to the Crusader Kings list

These are the same mechanics as entries already in docs/IDEAS_FROM_MODS.md. They are not new ideas; Imperator only
adds to them.

- **4. Sides that can be won in a civil war.** Imperator's civil war takes in the rebels' friends and close family
  with their commands, estates and loyal troops, even when they are loyal themselves, and since 2.0.4 (2024) it takes
  in many disloyal people instead of one ([Civil war](https://imperator.paradoxwikis.com/Civil_war); [2.0.4
  notes](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/5759616966663772076)). So a
  side can be drawn first from ties, before anyone buys it. What the winner does afterwards is entry 7 above.
- **8. Taken alive.** In the 2.0 rules as carried by Invictus, one pillage in ten carries off a person of the raided
  owner into prison
  ([pillage](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/unit_abilities/pillage.txt)); a raid is
  a second way to be taken alive. The figure may be the mod's own.
- **9. A war has an aim.** Holding the aim scores the holder a point each year, and failing to hold it gives one away;
  an aim held long enough forces the peace that grants it ([gamepressure
  guide](https://www.gamepressure.com/imperator-rome/casus-belli-reason-for-war/zbc2ae); [war
  goals](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/wargoals/00_default_inv.txt)). In the Reach:
  five years with the aim held makes the peace unless the other side wins a battle that year, so a long fruitless war
  turns against whoever began it. Cap the yearly score at half the lead that forces a peace. It closes B17 with entry
  9.
- **10. Regard that remembers why.** 1.4 (March 2020) replaced loyalty that drifted with "a sum of known modifiers",
  and made traits weigh more in its base; the press passed it on as "easier to understand and control"
  ([changelog](https://www.gog.com/forum/imperator_rome/changelog/page2);
  [GamingOnLinux](https://www.gamingonlinux.com/2020/03/imperator-rome-free-to-play-until-april-5-plus-archimedes-update-and-magna-graecia-content-pack-out-now)).
  Imperator adds the end of drift, and a base taken from natures (a Loyal relative starts higher; a Vengeful head's
  slights fade half as fast). Regard is read in many places, so every rule's sums must stay the same through the
  change.
- **13. Alerts for what is about to lapse, and the weighing entry "Plots that take time".** 1.1 put each scheme on the
  character, with its target and its time, and three answers: help, stand in the way, or talk them out of it, because
  character events "were rarely being experienced" and gave no feedback ([diary
  52](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2408775863533878321)). In the
  Reach, a relative's card says "means to stand for aedile in two years", and beside "help" are "stand in the way" (at
  a cost to their regard) and "talk them out of it" (by oratory and what they think of the head; the wilful refuse).
  For a rival head, whatever the spymaster has found shows the same way, marked as only what is known. It is the same
  lesson as Q3: show what is coming, not only what fired.
- **14. The house's roll of ancestors.** Imperator's ledger keeps "Our Rulers", everyone who ruled the country since
  the start ([diary
  53](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2400895196219455220)). The
  state's half of the roll is a list of the Republic's magistrates by year under the Senate window: consuls, praetors,
  aediles, quaestors, the Admiral, the Treasurer and the Speaker, with house and bench, the first of a house to hold
  each office marked. Imperator's deification (the paid Magna Graecia pack, 1.4) needed popularity over 90 and a
  living or past ruler, so "most republics can't do it"
  ([Paradox](https://www.paradoxinteractive.com/en/divine-changes-coming-to-imperator-rome); [Steam,
  2022](https://steamcommunity.com/app/859580/discussions/0/3191364450203063989/)). History did it by a Senate vote
  after death, which fits a Republic: honours voted for a dead person of great renown who held the consulship or a
  triumph, whose heir carries the name "child of the honoured" for life, with a little legitimacy and part of the dead
  commander's crews' devotion (Octavian, 42 BC; C2's second half). Keep it rare and out of the Caesar gates, and ask
  the owner if it reads as a faith (R13).
- **15. Ill fame.** Imperator's tyranny, from launch to 2.0, is a second, political kind of bad name: it rises with
  proscription, ruling over the Senate's head, rigging candidates, assassination and forcing a measure through, and it
  fades, faster for the charming and the popular ([Tyranny](https://imperator.paradoxwikis.com/Tyranny)). It is feared
  as well as hated (it lowers loyalty but pleases one party and eases expansion), so it is a cost and a resource at
  once, which Devereaux finds defensible. In the Reach: high-handedness for proscriptions, rule by decree, powers kept
  past leave, a measure carried past the Senate (Crusader Kings entry 20, below), a trial of a powerful person that
  fails, and a veto of a motion most of the chamber backed. While it is high the Just and Honourable think less of the
  person, but the timid lean their way out of fear. Keep it apart from dishonesty, or merge the two on purpose. What a
  person has taken is entry 13 above.
- **16. Raids between houses.** What a raid takes is the lesser of the world's people and the raiding force, times a
  figure; a raid marks the place against being raided again for five years; and only a commander loyal to the state
  will raid ([pillage](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/unit_abilities/pillage.txt);
  [naval raiding](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/unit_abilities/naval_raiding.txt);
  [launch actions](https://www.magicgameworld.com/imperator-rome-unit-actions/)). In the Reach a raid also presses
  some of the world's people into the raider's crews (never as slaves), and the raided head remembers the commander.
  Keep it to houses at war.
- **17. What the taker does with a fallen world, and sieges.** A siege needs a minimum force for each level of the
  walls or it lifts (five cohorts a level at launch, 4,000 soldiers in 2.0), and storming without a breach usually
  fails ([gamepressure guide](https://www.gamepressure.com/imperator-rome/forts-and-control-zone/z8c2a2); [game
  concepts](https://raw.githubusercontent.com/SnowletTV/Invictus/main/localization/english/game_concepts_l_english.yml)).
  2.0 added an engineer cohort that shortens sieges (beside the supply train in the [units
  files](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/units/army_supply_train.txt)), and a heavy
  ship that knocks a level off a port's walls at the cost of most of its own strength ([port
  assault](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/unit_abilities/port_assault.txt); [diary
  49](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2404270358336570829)). In the
  Reach: a siege tender hull that cuts the years and makes storming possible, an engineering skill that cuts one more
  (which gives engineering its first rule, E5), and a battleship that bombards a world's batteries at a third of its
  readiness. And 2.0's armies eat where a place cannot feed them, with a supply train carried in the army ([supply
  train](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/units/army_supply_train.txt)): a system
  feeds so many hulls, a fleet larger than that drains even in supply, and a stores ship lets it go further, beside
  the entry's burning of one's own stores. Ask the owner first, since supply lines as objects were declined; a stores
  ship inside a fleet is not one. A sanctuary world, kept for standing or stripped once for a fortune at a heavy cost
  in name, is a particular prize for the taker
  ([Paradox](https://www.paradoxinteractive.com/en/divine-changes-coming-to-imperator-rome)). It closes U4 of the
  buildings table with entry 17.
- **18. The demand before an invasion.** In Imperator a great power can threaten war to take a province without
  fighting ([gamepressure guide](https://www.gamepressure.com/imperator-rome/diplomacy/z6c388)), and Devereaux calls
  its diplomacy "a shockingly blunt diplomatic language oriented around compellence", which fits the age ([ACOUP
  I](https://acoup.blog/2024/07/19/collections-teaching-paradox-imperator-part-i-divisa-in-partes-tres/)). Imperator
  adds the other direction: a consul by motion, or a house in its own war, sends a nation a demand (a march world, a
  sum, a hostage of its ruler's family), and the nation weighs its strength against the guard at the march and its
  ruler's nature. It closes N22 in part with N13.
- **19. Allies called to a private war, and compacts.** In Imperator only small states (under 25 territories) may form
  a defensive league of many members, which counts as one tie and is called only when a member is attacked, and the
  greatest powers cannot ally at all ([gamepressure
  guide](https://www.gamepressure.com/imperator-rome/diplomacy/z6c388); [ACOUP
  I](https://acoup.blog/2024/07/19/collections-teaching-paradox-imperator-part-i-divisa-in-partes-tres/)). In the
  Reach: free holds, free states and houses of one world may form a league, vote as one on motions that touch their
  members, and take a great house as protector, which answers for them in war and takes their votes until it leans too
  hard. It closes B16 and B3 in part.
- **20. Tribunes of the commons.** Imperator's Senate passes a measure at 51, can be forced from 30 to 50 at about
  0.67 tyranny for each point short (14 at most), and blocks it under 30; the popular party's legal reform lets the
  next measure skip the Senate at a cost in the nobles' contentment
  ([Government](https://imperator.paradoxwikis.com/Senate); [Laws](https://imperator.paradoxwikis.com/Laws)).
  Imperator adds a price for carrying a measure past the Senate that grows with how badly the chamber refused, a floor
  below which nothing can be forced, and a consul who may do it as well as a tribune. In the Reach: once a term, after
  an Act is lost, for 3 influence, if three in ten of the chamber's weight voted for it; it carries if the commons'
  regard across the Reach is for it, and the mover gains high-handedness by the margin (Tiberius Gracchus, 133 BC).
  Players' lesson: if it is cheap, the Senate stops mattering.
- **Worth weighing, the family: "Siblings who grow close or apart".** Imperator's childhood bonds give children
  friends and rivals ([changelog](https://www.gog.com/forum/imperator_rome/changelog/page2)). Imperator adds bonds
  across houses: children of six to fifteen under one roof, including wards and the children of houses at the capital,
  become old friends or old enemies at sixteen, and a ward who is your heir's friend binds their house to yours.
- **Worth weighing, the family: "Sworn friendship, and a named rival".** In Imperator a rival is declared, which has a
  price; from 1.4 the ruler's rivals draw lower wages and lose family prestige, and from 2.0.1 a knife needs a
  declared rival ([changelog](https://www.gog.com/forum/imperator_rome/changelog/page2); [Steam
  news](https://steamcommunity.com/app/859580/allnews/)). Imperator adds a rivalry chosen and paid for (first guess: 1
  influence; the rival's head thinks 20 worse of you, and while you outrank them their house loses a point of standing
  a year, to 3), and a year's act to seek a friend that starts the old-friend clock at once. R14.
- **Worth weighing, the family: "Names that are given".** 1.2 lets a ruler give a governor a name from the region they
  governed, which a Roman line inherits ([diary
  62](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2415540140471488565)), and a
  triumph can bring an unflattering name, which players found funny ([Steam,
  2019](https://steamcommunity.com/app/859580/discussions/0/1676938384256577657)). Imperator adds names for governing
  and names that sting (the Slow, the Purse, the Dull), earned by other houses' heads by the same rules. R14.
- **Worth weighing, the Senate: "Post-holders with minds of their own" and "The two consuls weigh against each
  other".** 1.1 made an office-holder's work scale with their loyalty, shown on the screen, and made a disloyal
  co-consul raise every cost by up to a quarter ([diary
  53](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2400895196219455220); [diary
  50](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2405396896222894868)).
  Imperator adds the mild half that needs no answer from the owner: a council post works a step better above +40 and
  two steps worse at −20 or below, and a hostile colleague's bench leans against the consul's motions. And 1.4 made
  accidents likelier when the physician is unskilled or the patient's rival
  ([changelog](https://www.gog.com/forum/imperator_rome/changelog/page2)): a retainer below 20 loyalty, or a relative
  in a post who thinks −20 or worse of the one they serve, does harm instead of good (H6 of the court). C2 of the
  council in part.
- **Worth weighing, the Senate: "Murder and treason before a praetor, with exile".** Imperator adds exile before the
  verdict (entry 2 above), and the trial as a chain of choices for both sides (produce the accounts, call a witness
  from the wronged region, buy a juror, play to the crowd), with one appeal to a Justice ([diary
  52](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2408775863533878321)). L11,
  and docs/ROADMAP.md "Newer ideas" 2.
- **Worth weighing, the Senate: "Plots that take time".** The conspiracy against a holder who keeps the powers (entry
  4 above) is its first use.
- **Worth weighing, the Senate: "The health of the Republic as figures".** Imperator's civil war comes when disloyal
  people hold more than a share of the country's power (a quarter, less with tyranny and size), with a 40-month
  warning to win them back ([Civil war](https://imperator.paradoxwikis.com/Civil_war); [ACOUP
  IIIb](https://acoup.blog/2024/08/31/collections-teaching-paradox-imperator-part-iiib/)). Imperator adds a figure
  counted from named people: the share of the Reach's hulls, weight and sway held by houses whose heads think −30 or
  worse of the chair or carry a grievance, with each named and why, and a warning above a quarter. Use it as a warning
  and a cause of the great war's pressure, never as a trigger or a clock (Q5).
- **Worth weighing, the Senate: "Colleges of priests and the watching of signs".** At launch an augur's office made
  omens 2% stronger for each point of the holder's zeal ([gamepressure
  guide](https://www.gamepressure.com/imperator-rome/government-and-laws/zdc361)), and from 1.2 an omen was paid for
  by being locked to it for its whole term ([diary
  60](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/3556075649733826054)).
  Imperator adds a reading scaled by the holder's skill, and a reading before a war that leans the vote and the crews'
  loyalty for a year; a war voted against bad signs can bring a charge on the general who loses it, as it did Claudius
  Pulcher after Drepana in 249 BC. Offices and rites only (R13).
- **Worth weighing, war: "Free companies for hire".** Imperator adds buying off a company hired against you (in the
  Reach, two years' pay, and its sending house thinks better of the payer) ([achievement
  guide](https://steamcommunity.com/sharedfiles/filedetails/?id=3253981079); [gamepressure
  guide](https://www.gamepressure.com/imperator-rome/mercenaries/z9c2a3)). N30, Y5.
- **Worth weighing, war: "Letters of marque".** Imperator's law that condones piracy and opens a haven in your own
  port is entry 24 above.
- **Worth weighing, war: "A general's veterans settled under the general's name" and "An heir to a command".**
  Imperator adds that devotion outlives the command: loyal cohorts follow their sponsor even when no longer commanded
  by them, cohorts disbanded at double cost become veterans who add to their commander's power and rise again,
  seasoned, if the commander rebels (1.2 Cicero), and a sponsor who pays part of the keep wins them faster ([Cicero
  notes](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2406534841142341354);
  [Civil war](https://imperator.paradoxwikis.com/Civil_war)). In 2.0 an army can settle its own soldiers on an empty
  place, and in a republic one cohort becomes the commander's ([military
  colonies](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/unit_abilities/military_colonies.txt)).
  In the Reach: a commander who leaves a fleet keeps its crews' devotion for some years, fading; devoted crews paid
  off become their veterans, and they or their heir can call them back (Octavian with Caesar's veterans, 44 BC). It
  closes C2's second half. Count only crews under arms in the Caesar gates.
- **Worth weighing, war: "Every war skill counts" and, in the family, "A house's way, and the traditions it earns".**
  Imperator's military traditions are earned as experience in war and spent along short paths of steps, each unlocking
  an order or a small edge (road building, siege engineers, cheaper supply, a forced march) ([Latin
  traditions](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/military_traditions/00_latin.txt)).
  Imperator adds a house's navy that earns its doctrine from its own wars, passed to the heir. Keep it to a dozen
  steps, each a rule or an order, not a percentage.
- **Worth weighing, war: "More settings, at the start and in play".** Invictus lets each named crisis be switched on
  or off and offers an extremely aggressive AI
  ([Invictus](https://steamcommunity.com/sharedfiles/filedetails/?id=2532715348); [change
  notes](https://steamcommunity.com/sharedfiles/filedetails/changelog/2532715348?p=4)). Imperator adds a switch for
  each scripted crisis (great rulers, a people on the move, the Crown's decay). N24.
- **Worth weighing, worlds: "A city that grows".** Founding a city costs gold and influence over two years and takes a
  farm good out of the place, built over; a great city must be a populous provincial capital; and one can be turned
  back at a cost in tyranny ([news archive](https://tuxdb.com/game/5497&gamenews=1); [province
  ranks](https://raw.githubusercontent.com/SnowletTV/Invictus/master/common/province_ranks/00_default.txt)). Devereaux
  faults Imperator's cities for being made only by the state, and for having no limit, so play rewards one huge
  capital ([ACOUP
  IIb](https://acoup.blog/2024/08/09/collections-teaching-paradox-imperator-part-iib-built-in-a-day/)). Imperator adds
  that a raised city costs farmland and that undoing one costs the name; its lesson is that a great city must also be
  hungry and restless.
- **Worth weighing, worlds: "Disasters by the kind of world".** 1.1 drew storms on the map for two months, wearing
  down units that crossed them, and put volcanoes on the map that erupt once or twice a game and leave the land ruined
  until someone pays, after which its soil is richer ([diary
  54](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2411028931154342731)).
  Imperator adds hazards drawn on the map that affect movement, and a lasting gain once someone pays to mend the
  damage. In the Reach, the existing features can flare (an eternal storm spilling into a lane for a season, a flare
  star shutting a system's works for a year). S2.
- **Worth weighing, worlds: "A road of the Reach on a lane".** In Imperator an army builds a road with its own labour,
  moving at a quarter of its speed and losing morale while it works, and since 1.1 it continues to its destination
  ([Steam, 2024](https://steamcommunity.com/app/859580/discussions/0/4356746316584322127); [diary
  53](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2400895196219455220)).
  Imperator adds the road as a commander's deed, named for them, rather than a purchase. The owner's word first.
- **Worth weighing, worlds: "Landmarks placed by hand, and great works in stages".** 2.0's wonder designer let the
  builder pick effects in four tiers, for the place and the realm, with richer materials for higher tiers ([great
  works](https://raw.githubusercontent.com/SnowletTV/Invictus/master/common/great_work_effects/00_default.txt));
  reviewers called the choice "superfluous" and wonders "a poor investment"
  ([Wargamer](https://www.wargamer.com/imperator-rome/2.0-marius-update-review)). The Roman missions add an arch at
  the capital for each region conquered ([diary
  70](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2448197582035096542)).
  Imperator adds a dedication chosen by the builder, and a monument raised from a general's spoils in their name,
  which a rival holding the capital may pull down. S3, U10.
- **Worth weighing, worlds: "People move between worlds".** 1.2 replaced hand-moved people with a slow flow: a pull
  worked out from free room, one group moving from a place at a time, people fleeing sieges and hunger; 2.0 then split
  growth from migration, because cities "were largely sustained by prolonged inwards migration" ([news
  archive](https://tuxdb.com/game/5497&gamenews=1)). Imperator adds refugees who arrive somewhere, and the warning
  that growth must not come only from people moving in.
- **Worth weighing, worlds: "Grain for the capital, and a grain commission".** 1.2 gave every place stores of food
  with a cap, filled by its farms and eaten by its people, its armies (from 1.3) and its sieges; a great city must be
  fed from outside; running out cuts what a place can hold by three quarters ([news
  archive](https://tuxdb.com/game/5497&gamenews=1); [population
  guide](https://steamcommunity.com/sharedfiles/filedetails/?id=2403894295)). Imperator adds one number of stores on
  each world, eaten by fleets and sieges. Its warning: AI city-states starved themselves out in ten to twenty years
  ([Invictus discussion](https://steamcommunity.com/workshop/filedetails/discussion/2532715348/3458219850121446918)),
  so other houses must keep stores, and famine needs people moving or it simply deletes them.
- **Worth weighing, worlds: "Contracts and tax-farming".** The censor lets the contracts (entry 23 above). Imperator's
  own censor has none, so that part is ours.
- **Worth weighing, worlds: "Peoples, tongues and customs".** Citizenship one region at a time (entry 21 above) is the
  part that needs no peoples.
- **Worth weighing, the screen: "Map modes chosen by hand".** 2.0.2 put a shortcut to the levies map in the military
  view ([2.0.2
  notes](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/4057152305730789848)).
  Imperator adds that the open window sets the map (the Senate by bench, Military by supply and fronts with occupied
  worlds hatched, Worlds by unrest). V23, V21.
- **Worth weighing, the screen: "A written arc for each start".** Imperator's missions (1.3) are built from the
  situation for any nation, offered as a choice, and can be left with the gains kept ([diary
  66](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/3051677487689547747); [diary
  71](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/3657414904102853760)). The
  same could give an office a charge for its term ("Worth weighing" below).

## Worth weighing, not first

Shorter entries. Each says what it is, where it comes from, what the game has, and its size.

### The Senate and the offices

- **A charge for the term of an office** (large). On taking an office, a person is offered three charges built from
  the state of what that office looks over, four to eight tasks over the term, each with one either-or choice: an
  aedile rebuilds a named quay or gives games the commons remember, the Admiral clears a region's lanes, a praetor
  mends the courts' name in a region that distrusts them, a consul carries an Act or ends a named quarrel. Finished,
  it writes a line in the record and adds renown; left undone, it is a failure marked like any other, and the next
  holder may take it up. From Imperator's missions (1.3 Livy): trees of tasks, generated for any nation, with side
  branches, branches that exclude each other, and a way out; players liked the direction, disliked the railroading and
  the bland generic trees, and trees broke when a condition became impossible, with tooltip and check still being
  fixed in 2024 ([diary
  66](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/3051677487689547747); [diary
  71](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/3657414904102853760);
  [Steam](https://steamcommunity.com/app/859580/discussions/0/3110277460799887990/); [2.0.4
  notes](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/5759616966663772076)). Now
  part: one piece of business a year with three choices (docs/LADDER.md, "The work of office"), the player's own aim
  with a next step (src/Core/Presentation/OwnAim.cs). L13 and O3 in part. Each task must be checked by the function
  that writes its tooltip, stay possible from any state, and be cheap to drop; start with one office.
- **Away from the chamber** (small). While a house's head commands or governs away from the capital, the house's
  weight in the Senate falls by a part, unless a grown relative of rank sits for it; giving a rival an honourable
  charge far off quiets them (Cato sent to Cyprus in 58 BC). From Imperator's 2.0 Senate, where senate influence is
  "significantly decreased for commanders and governors" ([Government](https://imperator.paradoxwikis.com/Senate)).
  Now part: the attendance record already gives 2 votes at a count for four years in five present and takes 2 for two
  in five or fewer (docs/LADDER.md, "Committees"); a command does not count as absence, and weight does not drop
  (SenateSystem.WeightNow). Must not punish the command by service just built.
- **The consuls set the Reach's bearing beyond the rim** (small). For their term the consuls agree a bearing toward
  the nations (forward, trading, conciliating, or binding the clients), each tied to rules that exist (plunder, the
  exchanges, regard's drift, clients' loyalty), and changing it inside a term costs public trust. From Imperator's
  diplomatic stances, a dial for the whole state at launch ([launch
  diplomacy](https://www.magicgameworld.com/imperator-rome-diplomacy/)). Now none: each nation's regard drifts by its
  own temper (NationSystem.Tick). It gives the consulship foreign business.
- **A law on standing fleets** (small). An Act with two settings: free arms, or a limit by standing (for example four
  hulls, and two more for each office the house holds), lifted while the Senate has a war voted; a devoted fleet
  cannot be laid up (entry 3), so the law forces the question on a would-be Caesar. From Imperator 2.0, where laws
  gate standing legions and keep early armies small
  ([PCGamesN](https://www.pcgamesn.com/imperator-rome/good-now-2-0-marius)). Now none in the Acts (docs/SENATE.md);
  nearest is the Crusader Kings weighing entry "Private war only by the Senate's leave". The other houses must lay
  fleets up too.
- **New people of no house** (large). About once a decade the capital throws up a person of no house (a captain risen
  from the ranks, an advocate, a merchant's child) with real skill and nothing else, who stands for quaestor and, if
  elected, founds a house of one with a seat; the old blood leans against them, and a house whose line and standing
  are near nothing leaves the rolls. The player can take them up as patron, marry into them or keep them out (Marius,
  Cicero). From 1.3 Livy's minor characters who rise from obscurity, and families that fall
  ([changelog](https://www.gog.com/forum/imperator_rome/changelog/page2);
  [Overclockers](https://forums.overclockers.co.uk/threads/imperator-rome-paradoxs-next-grand-strategy-title.18820401/page-12)).
  Now part: a new house is founded only by one cast out or by a rebel world (docs/HOUSEHOLD.md, "Cast out"; GAPS B16).
  J6 (a junior who seeks a patron); leans on the owner's H2 of the court, and on K3 (turn cost).

### The person and the house

- **The powerful are hard to hold** (medium). A relative's weight in their own right (rank, an office, devoted hulls,
  estates, a full purse, renown) set against the head's: when it is greater, they answer the head's asks as the wilful
  do, and their card says so ("answers to nobody: weight 6 against your 4"). From 1.1 and 1.2, where power base was
  made to lower loyalty, so the very powerful are harder to keep ([diary
  62](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2415540140471488565);
  [changelog](https://www.gog.com/forum/imperator_rome/changelog/page1)). Now part: obedience reads nature and regard
  only (CourtshipSystem.Wilful; docs/EDUCATION.md, "Asking, not telling"). It changes only whether kin obey, not
  whether they betray (the owner's C6 and C2 of the council).
- **Land settled on a person carries weight** (medium). An estate settled on a named world of the house, or bought on
  another's, makes its people the holder's clients: votes in that region's count, weight of their own (the entry
  above), and in a march or a breakaway the estate goes with its holder's side. The head may take one back, at a
  lasting cost in the holder's regard. From 1.1 Pompey's holdings, granted, bought, inherited, taken by proscription,
  and joined to a rebel's side; 1.4.2 halved the loyalty from giving one because it had become the easy fix ([diary
  52](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2408775863533878321);
  [changelog](https://www.gog.com/forum/imperator_rome/changelog/page2)). Now part: land settled pays 200 a year and
  gives regard, three a person, on no world and with no weight (docs/WEALTH.md). Stay below the owner's W12 (a world
  held by a person).
- **A spouse whose children are passed over** (small). While the head's living spouse has children of their own and
  the heir is not one of them, the spouse thinks a little worse of the head each year (to −30), the marriage cools,
  and the spouse's house leans toward its own grandchild in any contest. Settled by naming their child, or borne
  (Livia and Tiberius). From 1.1, where a consort loses loyalty while the heir is not their child
  ([changelog](https://www.gog.com/forum/imperator_rome/changelog/page1)). Now none: naming an heir over the law
  touches only the one passed over (docs/AMBITIONS.md, "A named heir"). M3 of the marriage table in part, which waits
  on M4.
- **Service abroad for a restless relative** (medium). The head may suggest it to a quarrelsome or seat-hungry
  relative, or a restless one decides for themselves: a captaincy with the Free Cities, or an aide at a foreign court,
  for some years. They send word, may win renown, a fortune or a foreign match, or die, and come back with a following
  and an expectation of the seat or an office. A softer road than striking someone from the line. From 1.1's member of
  the royal family sent abroad as a mercenary, who "may well return one day with expectations" ([diary
  52](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2408775863533878321);
  [create_mercenary](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/character_interactions/create_mercenary.txt)).
  Now none: a command by service lends an officer only to a house of the Reach (docs/MILITARY.md). Serving a nation
  the Reach is at war with must be refused.
- **A thing set up at a world** (small). A thing of the house can be set in the hall of a city on one of its worlds
  (one in a town, two in a city, three in a great city), where it works for the world instead of the person (a
  standard for the garrison, the Crown's diadem for the region's allegiance), and whoever storms the world takes it.
  From 1.4's treasures kept at holy sites, which act only in that province, and 2.0.2's fix so they pass to the winner
  ([Archimedes
  notes](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2984138620262116837);
  [2.0.2
  notes](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/4057152305730789848);
  [Steam, 2025](https://steamcommunity.com/app/859580/discussions/0/594030683912256792/)). Now part: things are held
  by persons and a standard in a hall does nothing (docs/WEALTH.md). No relics of gods (R13).
- **A duel over an insult** (small). A person may call out a rival or one who smeared them; the other fights, names a
  champion, or refuses and loses standing; a death opens a feud by the rules that exist; an Act may forbid duels. From
  Invictus's challenge to a rival, with a champion from the court and a cooldown of five and ten years
  ([duel_rival](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/character_interactions/duel_rival.txt)).
  Now part: a duel only inside a blood feud (docs/FEUD.md). F3. Duelling was not a Roman custom, so the law against it
  matters.

### War and the powers beyond

- **A war levy from the house's own worlds** (medium). In a war the head may call out a world's levy: a few weak hulls
  (half a corvette's worth each) at no build cost, sailing under the world's governor within two lanes of it, while
  the world yields a third less; at the peace they go home. A frontier governor becomes a commander. From 2.0, where
  levies are raised from each province's people under its governor, who stop working while under arms, which reviewers
  counted the best change ([Wargamer](https://www.wargamer.com/imperator-rome/2.0-marius-update-review); [Steam,
  2021](https://steamcommunity.com/app/859580/discussions/0/4904953657531913919/)). Now part: a province under another
  house sends a levy squadron in war (docs/PROVINCES.md); a house's own world raises nothing. Let the head name a
  deputy when the governor is no soldier, which answers Imperator's players' complaint.
- **A muster takes hands from work** (medium). Each hull in commission counts its crew against the house's worlds by
  their people; a world with hands at sea yields less, a laid-up hull sends them home, and a hull destroyed takes its
  crew's share of people from those worlds. From 2.0's levies, which cost "an immediate production and income hit" and
  risk losing the people ([Wargamer](https://www.wargamer.com/imperator-rome/2.0-marius-update-review)). Now part: the
  muster cap (Economy.MusterCap), and the crew paid for once when a hull is laid down (docs/ECONOMY_REVIEW.md). M5 of
  the money table, the owner's; it should replace a cost, not add one.
- **Forced passage** (small). "Drive them": a voyage of two years or more takes one year less, and the fleet arrives
  with readiness and supply down, crews a little less loyal, and its lightest hull left behind; a commander who fights
  at the front loses no loyalty. From the launch toggle that moves armies much faster at the cost of attrition and
  morale, kept in 2.0 ([unit toggles](https://www.magicgameworld.com/imperator-rome-unit-toggle-abilities/)). Now none
  (src/Core/Commands/FleetCommands.cs). It must respect the chokepoint bar.
- **A fortress holds the lane** (small). A fortress work at a chokepoint bars passage by itself while it stands, and
  reducing it takes a year a level and a fleet of at least twice its level in hulls. From Imperator's forts, which an
  enemy must take before going past, and forts one or two apart that make a wall ([gamepressure
  guide](https://www.gamepressure.com/imperator-rome/forts-and-control-zone/z8c2a2)). Now part: a fleet at a
  chokepoint bars an enemy's route (MoveFleetCommand.Barred); a work bars nothing. One to a gate, and expensive;
  invasions through the marches must still work.
- **A ruler the Reach chooses for a beaten nation** (large). When a nation sues after its capital has fallen, the
  house that dictates the peace may put a ruler of its choosing on the chair (a pretender of its blood, the old
  ruler's heir raised as a ward, or a grown relative of the house), confirmed by the Senate as Rome's Senate confirmed
  client kings; a relative made king rules for themselves. From Imperator's releasing a state under a native ruler
  (1.1) ([diary
  53](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2400895196219455220)). Now
  none, but planned: docs/BEYOND.md stage 3's "other ending". N19; needs rulers as people (decision 11) and decision
  8.
- **Neighbours who fear the Reach's growth** (medium). Each world taken from a nation lowers the others' regard for
  twenty years, the taking house is blamed first, and past a line two nations make common cause. From Imperator's
  aggressive expansion, which Devereaux finds hurts mainly at home, so conquest has few outside brakes ([launch
  diplomacy](https://www.magicgameworld.com/imperator-rome-diplomacy/); [ACOUP
  I](https://acoup.blog/2024/07/19/collections-teaching-paradox-imperator-part-i-divisa-in-partes-tres/)). Now part:
  inside the Reach a house's seizures feed coalition pressure (SphereSystem.cs); the nations do not read it, and never
  deal with each other (docs/BEYOND.md, decision 12, the owner's).

### Worlds and the economy

- **A colony with a charter** (medium). The head names the world the settlers come from, which loses them, and chooses
  what the colony is for (veterans, trade, mining, citizens), which sets its first building and starting law; a colony
  within a lane of another house's claim gives that house's head a grievance. From Imperator's colonising with real
  people moved from a neighbour, and the Roman colonia that makes the place a city and takes a specialisation ([diary
  70](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2448197582035096542);
  [Steam](https://steamcommunity.com/app/859580/discussions/0/4355621552661966776)). Now part: a colony is a frontier
  colony whatever the world (docs/SYSTEMS.md, "Settling"). S5.
- **A good worked at home, and a store kept at the seat** (medium). A working deposit does something for its own world
  (a skimmer fleet refuels a fleet there free, a rare-earth digging makes the yard cheaper), and a house that keeps a
  store of a good at its seat beyond its needs gets a small standing benefit while it holds it. From Imperator's trade
  goods, whose first unit gives a local bonus and whose surplus in the capital gives one to the realm ([trade
  goods](https://raw.githubusercontent.com/SnowletTV/Invictus/master/common/trade_goods/00_default.txt); [Steam,
  2022](https://steamcommunity.com/app/859580/discussions/0/3371531159450683013/)). Now part: the strategic three gate
  hulls and buildings, and a good held only counts in wealth (docs/ECONOMY.md). Small and capped; show it from the
  first year (a bug hid Imperator's surplus at the start for years).
- **A staple port draws its region's trade** (small). While a world is under the staple port law, a tenth of the
  charters and venture yield on the region's other worlds goes to its holder, one staple to a region, and the region's
  houses resent it. From 1.1, which made trade a province's business and let an investment buy an extra route ([news
  archive](https://tuxdb.com/game/5497&gamenews=1)). Now part: the staple brings its own world 200 a year and draws
  nothing (docs/POLITICS.md, "The port"). Y8. Keeps the Reach's single price.
- **A world let go on terms** (small). On a world at unrest 50 or more, the head may set it free as a free hold under
  its governor, paying a tenth of its yield for twenty years, with less legitimacy lost than in a revolt, no enemy
  made, and a right to buy it back at half the price. From 1.1's releasing restless provinces as subjects ([news
  archive](https://tuxdb.com/game/5497&gamenews=1)). Now part: a world in revolt for four years breaks away and can be
  bought back (docs/GALAXY.md, "Risings and free holds"). Other houses must use it too (X2 in the same gap in many
  places).
- **A discovery made by a person** (medium). Someone with engineering, or a scholar at court, works for years on one
  named improvement (a better hull class, a deep-mine method, a faster yard), which then belongs to the house to keep,
  license, sell or give to the Reach for renown; other houses copy it within a generation. From Imperator's
  inventions, which belong to the state ([PCGamesN](https://www.pcgamesn.com/imperator-rome/2-0-marius-patch-notes)).
  Now none; the Crusader Kings list left out a second economy of research, so keep it to one project a person. E5
  (engineering is read by no rule).

### The screen

- **Feats** (small). Thirty to fifty feats read from flags and counters the engine already keeps, each about the
  person (powers laid down on the day, consul three times, from no house to the consulship, a triumph refused to keep
  a fleet, a house that lived on after losing every world), marked only in a game begun with undo off, listed on the
  You window and sent to Steam. From Imperator's achievements, Ironman only until 2.0.4 let them be earned with mods,
  several of them about a person or a republic (a dictator who stands down)
  ([achievements](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/achievements/main.txt); [2.0.4
  notes](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/5759616966663772076)). Now
  none. Plain names, nothing called a cheat (docs/MONEY.md), and none that pushes the player to end the Republic.
- **One list of where a building could go** (small). In the House window, choose a building and see every system and
  province of the house where it could stand, sorted by what it would yield there at today's prices, with the refusal
  for each place it cannot, and build or pull down from the row. From 1.1's macro builder, which players coming from
  older games had "sorely missed" ([diary
  53](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2400895196219455220)). Now
  part: one world at a time (SystemWorks.BuildableOn, docs/BUILDINGS.md). The list must use the same yield the turn
  computes.

## What Imperator teaches

- **One person turns loyalty from a chore into a choice.** Imperator made the player the state, so its great people
  were obstacles to keep loyal, and many players called that "the same mindless free hands, bribe, holdings minigame
  every 5 minutes" and "+10 loyalty and −15 loyalty" noise; others loved the moment a great figure went their own way
  ([Steam](https://steamcommunity.com/app/859580/discussions/0/6352962881244050297)). Here the player is one of the
  mighty. Danger should come from a few named people with visible causes, and be cured by acts that change their
  situation (a command, a marriage, a ruin), never by a yearly top-up.
- **Spendable points were the most disliked thing at launch.** "Monarch power" from random stats, spent at a token
  price on unrelated acts, was gone within five months, replaced by consequences, gold and time ([diary
  60](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/3556075649733826054)). The
  yearly pool here is flat and spent as "2 influence" on dozens of buttons; it is not random, but it has the same
  shape (entry 16).
- **Show the parts.** Hidden drift made loyalty unreadable until 1.4 made it a sum of named modifiers; players found
  the families' expectations "too opaque"; offers of client status were unreachable in 1.2 because the tooltip and the
  opinion tab disagreed; mission tooltips that checked something else were still being fixed in 2024. One function
  should write the tooltip and check the rule.
- **Approval that never starts again measures the player.** Approval that only climbs gets gamed, and wants forced on
  the player read as chores (dictatorship was prized for escaping them). Wants should be few, drawn from existing
  verbs, refusable at a price, and belong to one term (entry 10).
- **Roads that run one way.** Imperator's governments run from republic to dictatorship to monarchy, and players asked
  why there is "only one way into Dictatorship and no way out"; Devereaux calls it teleology, since Sulla laid his
  power down. Keep roads both ways (R9, and the forms of government G1 to G7, which are the owner's).
- **If the chamber is cheap to override, the Republic is "just Dictatorship with extra steps".** Overriding it must
  cost the person something that lasts and grows, without forbidding it (rule 3).
- **A penalty that also buys something is a choice.** Tyranny lowered loyalty but pleased one party and eased
  expansion, so taking it was a decision, not only a punishment. High-handedness should feed fear as well as hatred.
- **The people were missing.** Devereaux and players both missed the assemblies, the tribunes and the crowd. The
  commons estate and its petitions are a base for the people's road (entry 12, and Crusader Kings entry 20).
- **We are already ahead on the ladder, and should not go back.** Imperator appointed offices and skipped the cursus,
  which was Devereaux's first complaint. His other point applies here: Roman magistracies were yearly, while this game
  elects the ladder every fifth year and the consuls every tenth (docs/LADDER.md, docs/REPUBLIC.md), which makes
  careers slower and fills the Senate with fewer former magistrates. Worth a word with the owner.
- **Luck spoils honours.** The base game's legion honours came by chance; a guide, a mod and players all objected.
  Honours must be earned by deeds said in advance, small and capped, with a fair mark of shame that can be washed out.
- **Cheap fixes get used everywhere and then cut.** Free trade routes were removed as a no-brainer (1.1), the loyalty
  from giving holdings was halved (1.4.2), and bribes were given a second price (1.4). Every gift that buys goodwill
  needs a price beyond money, and a cap.
- **A figure that only grows snowballs.** Players said family prestige "always grows" with no way down, and the event
  meant to make a family fall removed the most prestigious one instead of the least until 2.0.4 fixed it. Build the
  way down with the way up, and test that it lands on the right house.
- **Tie war to the economy.** Reviewers' favourite 2.0 change was that levies came from the people and took them from
  work. The manpower cap and the province levies here are the right direction; a levy should take from a world's
  yield.
- **People should move by themselves.** Pops moved by hand were replaced in 1.2 with a slow simulation, and colonising
  by hand is still "tedious". Anything about people here works through laws, governors and buildings, never a button
  for each block of people; and growth must not depend on people moving in.
- **Do not reward one huge capital, and do not rest the economy on the unfree.** Imperator's cities had no diminishing
  returns and its revenue came mostly from slaves, both of which Devereaux shows are wrong for the ancient world. A
  great city here should be hungry and restless, and nothing should make income depend on people who are not free.
- **Events nobody sees are wasted.** 1.1 put schemes on the character because character events "were rarely being
  experienced" ([diary
  52](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2408775863533878321)). Q3 here
  (events that never fire) is the same problem: show what is building before it fires.
- **A bargain must hold.** At launch, barbarians who were paid off still looted, fixed in 1.1 ([diary
  51](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2905297189261712221)).
  Whatever the player pays for (a ransom, a client's tribute, raiders bought off) must buy exactly what it says.
- **Measure the cast.** Families died out because marriage was too restricted, and players ran out of generals
  ([Steam](https://steamcommunity.com/app/859580/discussions/0/1676938384259454548)); minor characters were added
  partly to fill offices. A republic of elected offices needs able candidates in every house at year 40.
- **Civil wars to the death are a drain.** Some never ended until patched, and 2.0.4 had to make them take many
  estranged people instead of one. The march and the great war here have clear ways to win and lose; keep a way to end
  them by terms (C4, entry 7).
- **A danger that follows share, not size.** Imperator counts power as a share, so whoever pulls far ahead becomes the
  danger, which players called a fair model of Marius, Sulla, Pompey and Caesar ([Steam,
  2024](https://steamcommunity.com/app/859580/discussions/0/4517758633227882315)). Read a house's share of the Reach,
  not its absolute size.
- **The late game needs goals.** Reviewers found the mid and late game aimless once rivals were beaten. Governorships,
  clients, a censorship and charges for a term are where the late game here should come from.
- **Content and smarter rivals mattered most to players.** The most used mod deliberately does not overhaul the core:
  it adds a better AI, regional flavour, missions and hundreds of events, and the top interface mod adds ledgers. For
  this game that is X2 (other houses use what the player can) and Q3.
- **Small, frequent updates won players back.** 1.2 and 1.3 came within months, each on one theme, and the team said
  reacting quickly was the point ([diary
  76](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2600199781654891848)). Shelved
  in 2021, the game was finished by mods and a volunteer patch that folded their fixes back in. Keep content as data a
  modder can change.
- **Watch the words, and the dates.** Taking the mechanic must not take the word: "province" means something else
  here. And the third most subscribed Imperator mod only shows the date in a familiar form: say how long ago and how
  long until, not only a year number.
- **What could not be read.** The Imperator wiki and the Paradox forum refused automated reading on most tries, so the
  2.0 developer diaries on legions and levies (November 2020) and the diary on sacred treasures were seen only as
  search excerpts, and are used for stated intent, not rules. Some figures (a tributary's 15%, a three-year lock on a
  patron deity, one capture in ten on a raid) come from the wiki's or a diary's text as quoted in search results, or
  from the Invictus files, which carry the 2.0 rules with the mod's own edits. Check them before building on them.

## Already in the game

| The mechanic | How Imperator has it | Where the game has it |
|---|---|---|
| Votes weighed by what each house holds | a party's seats follow its members' own power (1.5) | SenateSystem.WeightNow; docs/REPUBLIC.md |
| Elected offices in order, with ages and steps | (Imperator appoints its offices; 1.1 added an age limit for the ruler) | docs/LADDER.md |
| A senior and a junior consul, each with a veto | consul and co-consul (1.1) | docs/REPUBLIC.md, stage 2 |
| A bar on holding the consulship again soon | nobody who ruled in the last ten years may stand (1.x) | ElectionSystem (consul_term); docs/LADDER.md (no rung twice) |
| A triumph voted for a victorious general | holding a triumph | docs/CAESAR.md, "The triumph" |
| Emergency powers voted, laid down, or kept past leave | appointing a dictator | docs/EMERGENCY.md |
| Experience in a post makes the holder better | statesmanship (1.3) | learning by doing (docs/EDUCATION.md); a council post's step every four years (docs/HOUSEHOLD.md) |
| Crews devoted to their commander | cohorts loyal to a character | docs/CAESAR.md, "The crews and their commander" |
| A general's own weight in the Republic | power base (1.1) | sway, docs/CAESAR.md, "Sway" |
| A fleet given to someone of another house | a legion's legate and tribunes (2.0) | docs/MILITARY.md, "A command by service" |
| Buying a judge; a gift that leans a vote | bribing a character | docs/LADDER.md (a bought praetor); docs/WEALTH.md, "A gift can ask a favour" |
| Proscriptions and an amnesty | proscribing a head of family | docs/EDICTS.md |
| Games and grain for the crowd | holding games; feeding a city | docs/LADDER.md (the aedile); docs/POLITICS.md, docs/EDICTS.md and docs/SENATE.md (the dole) |
| Land settled on a person, paying into their purse | holdings (1.1) | docs/WEALTH.md, "Land settled on one of the house" |
| A purse of one's own, apart from the house's | personal wealth | docs/WEALTH.md |
| A figure explained by a book of its causes | loyalty as a sum of known modifiers (1.4) | legitimacy, src/Core/Systems/LegitimacyBook.cs (regard not yet: Crusader Kings entry 10) |
| A governor whose skill and regard move a world | governor's finesse and loyalty | docs/GALAXY.md, "Governors who scheme"; src/Core/Systems/Economy.cs |
| Trying a former magistrate | bringing to trial | docs/LADDER.md, "The courts" (L2 closed) |
| Forces limited by the people of one's worlds | levies raised from the people (2.0) | Economy.MusterCap (src/Core/Systems/Economy.cs) |
| A levy that sails under the holder in war | levies from a governorship | docs/PROVINCES.md (P10 and P12 closed) |
| Hiring one's own fleet out | sending a unit abroad as mercenaries | src/Core/Systems/MercenarySystem.cs |
| Raiders where the law is weak, hunted for a name | pirates (1.1) | src/Core/Systems/PirateSystem.cs; docs/GALAXY.md, "Raiders" |
| A port that shelters raiders, and closing the lanes | a law condoning piracy; the command against the pirates | the free port local law (docs/POLITICS.md); "The lanes closed to raiders" (docs/EDICTS.md) |
| A colony of veterans | military colonies | docs/POLITICS.md (local law); docs/SENATE.md (the Land Law) |
| A coalition against a house grown too strong | aggressive expansion | src/Core/Systems/SphereSystem.cs |
| A beaten nation made a paying client | a tributary | docs/BEYOND.md, "What kind of peace it is" |
| Peace terms that follow the war score | war score | src/Core/Systems/HousePeace.cs; docs/BEYOND.md |
| Supply by distance to a depot | supply and attrition | docs/MILITARY.md, "Supply lines and campaigns" |
| Holding a chokepoint bars the enemy | forts and their zone of control | MoveFleetCommand.Barred; docs/GALAXY.md |
| Stances, command styles, drill and laying up | unit toggles and drill | docs/MILITARY.md; DrillFleetCommand |
| Great works with fixed effects | great wonders | docs/BUILDINGS.md, "Great works" |
| Great cities with buildings of their own | cities and metropolises | docs/CITIES.md |
| A standard taken in battle, and plunder carried off | sacred treasures seized by sacking | docs/WEALTH.md, "Heirlooms" (W11 closed) |
| A rebel world breaks away and can be bought back | revolts, and releasing a subject | docs/GALAXY.md, "Risings and free holds" |
| What a relative is after, with how near they are | ambitions and schemes | docs/AMBITIONS.md |
| Taking someone into the house | adoption | docs/HOUSEHOLD.md |
| Settings for how strong and aggressive the powers beyond are | game rules | docs/BEYOND.md, the new-game settings |

## Left out

- **Pops by class, with slaves as the base of the revenue, and culture integration and conversion people by people.**
  The owner said peoples and castes are not for now (GAPS R13); Devereaux shows the slave-based economy is an outdated
  picture of the ancient world; and players disliked an integration cost that never settles. Only citizenship by
  region is kept (entry 21).
- **A state religion.** Deities and omens, holy sites that strengthen a god, conversion, a ruler made a god in the
  pantheon, a conquered people's god taken in, and four standing observances chosen by a head of the rites. A faith is
  not for now (R13), and players called Imperator's religion "too bland". Kept: the college as an office of the state
  (a Crusader Kings entry), divine honours as a Senate vote (under entry 14), and the festival, which already exists
  as edicts ("A day of thanksgiving", "A day of games", docs/EDICTS.md).
- **Monarch power and other spendable points.** Imperator removed them itself in 1.2.
- **Thirteen kinds of subject.** Mostly flavour copies that differ by small numbers; three kinds are kept (entry 11).
- **The road to a crown.** Requesting or demanding a line of succession, governments that run one way, tyranny that
  lowers the line for a civil war. The Republic stands unless the player ends it, and only through the powers for life
  (docs/PROCESS.md); the forms of government are the owner's (G1 to G7).
- **Inventions, innovations and research trees.** Four flow-charts in 2.0, with players split ("too confusing"). The
  Crusader Kings list left out a second economy of research; one person's discovery is kept to weighing.
- **Trade goods as a network of routes, and province investments bought with influence.** The owner keeps one price
  for the Reach (docs/ECONOMY_MARKET.md); only the staple port's draw and a store at the seat are weighed.
- **Land battle detail.** Cohort types, the phalanx, combat width, units by culture. The Reach fights with five hull
  classes; only the ideas (the ground, engineers, a minimum force for a siege) are kept.
- **Moving people and colonising by hand.** Players called it tedious, and Imperator replaced most of it itself.
- **Imperator's and its mods' own history and content.** The Roman, Carthaginian and Hispanian mission trees,
  Invictus's 150 deities, 200 events, dated invasions (Galatian, Parthian), the Mauryan collapse, its Spartan diarchy,
  its seasons and seven named disasters, Timeline Extension's later centuries, and TE: Crisis of the Third Century's
  monetary crisis. They are their history, not mechanics; the general mechanics (a charge for a term, a people on the
  move, disasters, a switch for each crisis) are kept.
- **Res Publica's own offices as such.** Ten plebeian tribunes, priests for life, a dictator with a master of horse
  and yearly elections are its content. Its rules (only former praetors govern, a governor cannot be removed before
  the term) are taken into entry 6; the tribunes are Crusader Kings entry 20; the yearly cadence is a question for the
  owner (see the lessons).
- **Slavery.** Naval raids that carry people off as slaves, slave estates and slave revolts. The game's tone and
  Devereaux's lesson; a raid presses hands into crews instead.
- **Plundering holy sites and relics of gods.** R13. A thing of the house set at a world is weighed without gods.
- **Bloodline traits bred from both parents** (Bloodlines 2.0). The Crusader Kings list left out breeding for traits.
- **Virtual Limes's zones of expansion.** An AI rule for states that conquer one another; the nations here never deal
  with each other (docs/BEYOND.md, decision 12).
- **Full Mechanical Overhaul's perk trees for a ruler.** A state ruler's; its screen for a subject is kept only as the
  few asks of entry 11.
- **Clan chiefs and their retinues inside the Reach.** Kept only beyond the rim, as the Khanates' chiefs (entry 17).
- **Interface and cheat mods for Imperator's own screens.** Movable windows, no fog, cheat menus. The idea of ledgers
  is weighed and the date lesson kept; the owner wants nothing called a cheat (docs/MONEY.md).
- **Stability and tyranny as figures of the state.** The person's versions are kept: ill fame and high-handedness
  (Crusader Kings entry 15), and the record of what was taken (entry 13).

## Sources

The Imperator wiki and the Paradox forum answer automated readers with a browser check. Some wiki pages were read
through a text copy; the forum's diaries were read as Paradox posted them on Steam. Search excerpts were used only
where a page could not be opened, and are marked.

**The Imperator wiki** (verified for 2.0): [Government (the Senate)](https://imperator.paradoxwikis.com/Senate);
[Laws](https://imperator.paradoxwikis.com/Laws); [Tyranny](https://imperator.paradoxwikis.com/Tyranny);
[Character](https://imperator.paradoxwikis.com/Character); [Office and
governors](https://imperator.paradoxwikis.com/Office); [Civil war](https://imperator.paradoxwikis.com/Civil_war);
[Character interaction](https://imperator.paradoxwikis.com/Character_interaction); [Political
influence](https://imperator.paradoxwikis.com/Political_influence). Launch-era copies of wiki pages:
[Characters](https://www.magicgameworld.com/?p=32633), [Character
interactions](https://www.magicgameworld.com/?p=32685), [Primary stats](https://www.magicgameworld.com/?p=32605),
[Unit toggle abilities](https://www.magicgameworld.com/imperator-rome-unit-toggle-abilities/), [Unit
actions](https://www.magicgameworld.com/imperator-rome-unit-actions/),
[Diplomacy](https://www.magicgameworld.com/imperator-rome-diplomacy/).

**Developer diaries and patch notes, as posted on Steam.** [Diary 13, the Senate (players'
comments)](https://steamcommunity.com/app/859580/eventcomments/1634166237650067574); [the director's road map after
launch](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2432417221739862764); [diary
42,
achievements](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/3140604542256315988);
[diary 49,
navies](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2404270358336570829); [diary
50, politics](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2405396896222894868);
[diary 51, trade and
barbarians](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2905297189261712221);
[diary 52,
characters](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2408775863533878321);
[diary 53, quality of
life](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2400895196219455220); [diary
54, pirates, volcanoes and
storms](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2411028931154342731); [diary
60, monarch
power](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/3556075649733826054); [diary
62, laws and
interactions](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2415540140471488565);
[Cicero release
notes](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2406534841142341354); [diary
66, missions](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/3051677487689547747);
[diary 68, the Punic
missions](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2504491307421465740);
[diary 69, characters and
families](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2451574645873160849);
[diary 70, Roman
missions](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2448197582035096542);
[diary 71, the generic
missions](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/3657414904102853760);
[diary 76, the
future](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2600199781654891848);
[Archimedes notes and diary
85](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/2984138620262116837); [diary
111, ports](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/3856733878309096878);
[2.0.2 notes](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/4057152305730789848);
[2.0.4 Augustus
notes](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/5759616966663772076). Steam's
news list for the game ([all
items](https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid=859580&count=500&maxlength=0&format=json), also
read with shorter limits, and [the notice for diary
107](https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid=859580&count=1&maxlength=0&enddate=1605052800&feeds=steam_community_announcements));
[Steam's news page](https://steamcommunity.com/app/859580/allnews/); the official change logs reposted on GOG ([1.0.1
to 1.2](https://www.gog.com/forum/imperator_rome/changelog/page1), [1.3 to
1.5.3](https://www.gog.com/forum/imperator_rome/changelog/page2)); [1.5 Menander
notes](https://www.allpatchnotes.com/imperator-rome-1-5-patch-notes-epirus-pack-new-missions-galore); [a mirror of the
diaries and notes, 2018 to 2021](https://tuxdb.com/game/5497&gamenews=1); [Paradox's page on the 1.4 religion
changes](https://www.paradoxinteractive.com/en/divine-changes-coming-to-imperator-rome).

**The game's rules, as carried in the Invictus files.** [governor
policies](https://raw.githubusercontent.com/SnowletTV/Invictus/master/common/governor_policies/00_default.txt);
[province ranks](https://raw.githubusercontent.com/SnowletTV/Invictus/master/common/province_ranks/00_default.txt);
[trade goods](https://raw.githubusercontent.com/SnowletTV/Invictus/master/common/trade_goods/00_default.txt); [great
work effects](https://raw.githubusercontent.com/SnowletTV/Invictus/master/common/great_work_effects/00_default.txt);
[investment in a
state](https://raw.githubusercontent.com/SnowletTV/Invictus/master/common/scripted_effects/invest_in_state_effects.txt);
[subject types](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/subject_types/00_default.txt); [war
goals](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/wargoals/00_default_inv.txt); [pillage and
raze](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/unit_abilities/pillage.txt); [naval
raiding](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/unit_abilities/naval_raiding.txt); [drill
and recovery](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/unit_abilities/drill_army.txt); [port
assault](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/unit_abilities/port_assault.txt); [military
colonies](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/unit_abilities/military_colonies.txt); [a
pirate haven, and rooting one
out](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/unit_abilities/construct_pirate_haven.txt);
[Latin military
traditions](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/military_traditions/00_latin.txt); [the
supply train and the engineer
cohort](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/units/army_supply_train.txt);
[buildings](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/buildings/00_default.txt);
[achievements](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/achievements/main.txt); [game
concepts](https://raw.githubusercontent.com/SnowletTV/Invictus/main/localization/english/game_concepts_l_english.yml);
[the list of character interactions](https://github.com/SnowletTV/Invictus/tree/main/common/character_interactions),
and in it
[create_mercenary](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/character_interactions/create_mercenary.txt),
[propose_marriage and
arrange_marriage](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/character_interactions/propose_marriage.txt),
[duel_rival](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/character_interactions/duel_rival.txt),
[grant_clan_autonomy](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/character_interactions/grant_clan_autonomy.txt),
and [seduce_governor, with force_retirement, impose_fine, offer_citizenship and
offer_protection](https://raw.githubusercontent.com/SnowletTV/Invictus/main/common/character_interactions/seduce_governor.txt).

**Mods on the Steam Workshop.** [Imperator:
Invictus](https://steamcommunity.com/sharedfiles/filedetails/?id=2532715348) and [its change
notes](https://steamcommunity.com/sharedfiles/filedetails/changelog/2532715348?p=4); [Imperator: Res
Publica](https://steamcommunity.com/sharedfiles/filedetails/?id=2834280746); [Legion Distinctions
Overhaul](https://steamcommunity.com/sharedfiles/filedetails/?id=2575888248) and [its
README](https://github.com/x3igh7/imperator_legion_overhaul); [2.0 Better
UI](https://steamcommunity.com/sharedfiles/filedetails/?id=1845165876); [Full Mechanical
Overhaul](https://steamcommunity.com/sharedfiles/filedetails/?id=2805839904); [Virtual
Limes](https://steamcommunity.com/sharedfiles/filedetails/?id=3020434697); [Timeline
Extension](https://steamcommunity.com/sharedfiles/filedetails/?id=2845446001); [TE: Crisis of the Third
Century](https://steamcommunity.com/sharedfiles/filedetails/?id=2858320715); [Bloodlines
2.0](https://steamcommunity.com/sharedfiles/filedetails/?id=1999873334); [Gladio et Sale's change
notes](https://steamcommunity.com/sharedfiles/filedetails/changelog/1737185536); [the Workshop by most
subscribed](https://steamcommunity.com/workshop/browse/?appid=859580&browsesort=totaluniquesubscribers&section=readytouseitems&actualsort=totaluniquesubscribers&p=1).
Guides there: [an achievement guide for 2.0.4](https://steamcommunity.com/sharedfiles/filedetails/?id=3253981079); [a
population guide for 2.0](https://steamcommunity.com/sharedfiles/filedetails/?id=2403894295). [The Heirs of Alexander
store page](https://store.steampowered.com/app/1437660).

**Guides.** gamepressure: [loyalty](https://www.gamepressure.com/imperator-rome/loyalty/zec362),
[population](https://www.gamepressure.com/imperator-rome/population-and-classes-of-residents/zac1d2),
[governors](https://www.gamepressure.com/imperator-rome/governors/z8c30b),
[economy](https://www.gamepressure.com/imperator-rome/economy-taxes-buildings-and-policies/z8c1d0),
[forts](https://www.gamepressure.com/imperator-rome/forts-and-control-zone/z8c2a2),
[mercenaries](https://www.gamepressure.com/imperator-rome/mercenaries/z9c2a3), [reasons for
war](https://www.gamepressure.com/imperator-rome/casus-belli-reason-for-war/zbc2ae),
[diplomacy](https://www.gamepressure.com/imperator-rome/diplomacy/z6c388), [rebellions and
barbarians](https://www.gamepressure.com/imperator-rome/rebellions-and-barbarians/zcc2af), [government and
laws](https://www.gamepressure.com/imperator-rome/government-and-laws/zdc361). [A launch-era Senate
guide](https://gameplay.tips/guides/5640-imperator-rome.html).

**A historian's reading.** Bret Devereaux, A Collection of Unmitigated Pedantry, 2024: [Part
I](https://acoup.blog/2024/07/19/collections-teaching-paradox-imperator-part-i-divisa-in-partes-tres/), [Part
IIa](https://acoup.blog/2024/07/26/collections-teaching-paradox-imperator-part-iia-pops-and-chains/), [Part
IIb](https://acoup.blog/2024/08/09/collections-teaching-paradox-imperator-part-iib-built-in-a-day/), [Part
IIIa](https://acoup.blog/2024/08/24/collections-teaching-paradox-imperator-part-iiia-de-re-publica/), [Part
IIIb](https://acoup.blog/2024/08/31/collections-teaching-paradox-imperator-part-iiib/).

**Reviews and press.** PCGamesN: [is it good now (2.0)](https://www.pcgamesn.com/imperator-rome/good-now-2-0-marius),
[the 2.0 notes](https://www.pcgamesn.com/imperator-rome/2-0-marius-patch-notes); [Wargamer's review of
2.0](https://www.wargamer.com/imperator-rome/2.0-marius-update-review); [Rock Paper Shotgun on the story
campaigns](https://steamstore-a.akamaihd.net/news/externalpost/rps/2504491307422278488); [Strategy Gamer on
Invictus](https://steamstore-a.akamaihd.net/news/externalpost/Strategy%20Gamer/4032392655089331368); GamingOnLinux on
[Livy](https://www.gamingonlinux.com/2019/11/the-free-livy-update-to-imperator-rome-and-the-also-free-punic-wars-dlc-out-december-3/page=1/),
[Archimedes](https://www.gamingonlinux.com/2020/03/imperator-rome-free-to-play-until-april-5-plus-archimedes-update-and-magna-graecia-content-pack-out-now)
and [its
religion](https://www.gamingonlinux.com/2020/03/imperator-rome-gets-more-religious-in-the-14-archimedes-update-due-on-march-31);
[Vice, before
launch](https://www.vice.com/en/article/finding-the-paradox-game-within-roman-history-for-imperator-rome);
[Wikipedia](https://en.wikipedia.org/wiki/Imperator:_Rome).

**Players' threads.** Steam: [request or demand a line of
succession](https://steamcommunity.com/app/859580/discussions/0/4356746484460744297); [politics and loyalty ruin this
game](https://steamcommunity.com/app/859580/discussions/0/6352962881244050297); [is civil war
inevitable](https://steamcommunity.com/app/859580/discussions/0/4517758633227882315); [a triumph's chance of a
name](https://steamcommunity.com/app/859580/discussions/0/1676938384256577657); [surplus in the
capital](https://steamcommunity.com/app/859580/discussions/0/3371531159450683013/);
[roads](https://steamcommunity.com/app/859580/discussions/0/4356746316584322127);
[colonising](https://steamcommunity.com/app/859580/discussions/0/4355621552661966776); [a legion's
honours](https://steamcommunity.com/app/859580/discussions/0/3879344844059565881/); [a
dishonour](https://steamcommunity.com/app/859580/discussions/0/594022553707675015/); [the general of a
levy](https://steamcommunity.com/app/859580/discussions/0/4904953657531913919/); [legions in a civil
war](https://steamcommunity.com/app/859580/discussions/0/603022903526591777/); [earning cohorts'
loyalty](https://steamcommunity.com/app/859580/discussions/0/1676938384247438849/); [loyalty at
launch](https://steamcommunity.com/app/859580/discussions/0/1676938384249580676/); [when to
integrate](https://steamcommunity.com/app/859580/discussions/0/3111395113769962164/); [integration, religion and
events](https://steamcommunity.com/app/859580/discussions/0/3108019435238108299/); [making a leader a
god](https://steamcommunity.com/app/859580/discussions/0/3191364450203063989/);
[treasures](https://steamcommunity.com/app/859580/discussions/0/594030683912256792/); [client and feudatory
states](https://steamcommunity.com/app/859580/discussions/0/3166519278494909130/); [families of women and
babies](https://steamcommunity.com/app/859580/discussions/0/1676938384259454548); [fix the mission
trees](https://steamcommunity.com/app/859580/discussions/0/3110277460799887990/); [mission trees, discussed by
Victoria 3 players](https://steamcommunity.com/app/529340/discussions/0/3820795764935173496/); [Invictus
food](https://steamcommunity.com/workshop/filedetails/discussion/2532715348/3458219850121446918). Elsewhere: [RPG
Codex, before
launch](https://rpgcodex.net/forums/threads/imperator-rome-the-new-grand-strategy-from-paradox.121924/page-22);
[Overclockers, on
1.3](https://forums.overclockers.co.uk/threads/imperator-rome-paradoxs-next-grand-strategy-title.18820401/page-12).

**Seen only as search excerpts.** [the diary of 27 January 2020 on
loyalty](https://forum.paradoxplaza.com/forum/threads/imperator-rome-development-diary-27th-of-january-2020.1315735/)
(with the diaries of 4 and 25 February and 27 May 2019); [the diary of 24 February 2020 on sacred
treasures](https://forum.paradoxplaza.com/forum/threads/imperator-dev-diary-2-24-2020.1339533/post-26287043); the 2.0
diaries on legions and levies (November 2020).

**This game.** docs/GAPS.md, docs/IDEAS_FROM_MODS.md, docs/PROCESS.md, docs/CAESAR.md, docs/LADDER.md,
docs/REPUBLIC.md, docs/SENATE.md, docs/EMERGENCY.md, docs/EDICTS.md, docs/POLITICS.md, docs/MILITARY.md,
docs/BEYOND.md, docs/WEALTH.md, docs/HOUSEHOLD.md, docs/LIFE.md, docs/GALAXY.md, docs/BUILDINGS.md, docs/CITIES.md,
docs/PROVINCES.md and docs/ROADMAP.md; in src/Core, CaesarSystem.cs, ElectionSystem.cs, FactionSystem.cs,
OfficeSystem.cs, ActionPools.cs, FleetCommands.cs, Fleet.cs, Economy.cs, PirateSystem.cs, SphereSystem.cs,
NationSystem.War.cs, CourtshipSystem.cs and LegitimacyBook.cs.
