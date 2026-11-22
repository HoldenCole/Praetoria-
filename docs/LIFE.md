# A life of one's own

Built 2026-11-14. The last piece of the roadmap's person-and-family group. Before it a person was their skills, their
stress and their house's standing; they were well until they died, nothing they did was remembered, and a death was a
line in the chronicle.

## Health

Everyone has a health from 0 to 100.

- **Age sets the most it can be:** 100 to the age of forty, then a point and a half lower each year, to 30 at the least.
  At 60 that is 70; at 80 it is 40.
- **It falls** by 20 for a wound, 12 for an illness, and 3 a year under great strain (stress of 70 and over).
- **It mends** by 3 a year when a person is neither ill nor much strained.
- **Illness** comes more often with age and to those already low. It passes in a year or two, sooner under physicians.

| Health | Called | Bears on death |
|---|---|---|
| 80 and over | hale | Three-quarters as likely as their years alone would make it |
| 55 to 79 | sound | As their years make it |
| 35 to 54 | ailing | Half as likely again |
| Under 35 | frail | Nearly two and a half times as likely. They lay down their commands |

Someone under fifty, who used never to die of anything, can now die if their health is below 25.

**What you can do for yourself:** a season away (2 influence, once a year: health +8, stress −10), and the physicians
(600 credits from your own purse: health +6, and an illness cured). Your relatives send for the physicians themselves,
out of their own purses.

**A wound** in battle now costs health, is written into the record, and leaves a scar on the portrait.

## The record of a life

What a person of your house does is written down as they do it: becoming head, a battle won, a wound, a marriage, a
child, a rank, an office, a skill mastered, a venture founded, someone taken in or adopted, an ambition had, a friend
lost. Up to forty things are kept for each person; the least is forgotten first.

- **Renown** is what a person is apart from their house: their rank and what they did, from 0 to 100. A family weighs it
  when one of them comes courting, and it counts with someone asked to join your household.
- **A name.** Some lives earn one: the Victorious (three battles won), the Scarred (twice wounded), the Just, the
  Builder, the Open-Handed, the Great, the Hard, the Old.

## Old friends and old enemies

Someone outside your house who has thought well of you (regard 45) for eight years is an old friend; someone who has
thought ill of you (−40) that long is an old enemy. A friendship that cools stops counting.

- An old friend's regard deepens a little every year, and there is an event when they are in the city.
- When an old friend dies it is felt (stress +8), written down, and there is an event. When an old enemy dies there is
  one too.
- **In the Senate** an old friend leans one step toward whatever your house moves, and two steps against a censure,
  embargo, breakup, recall or reconquest aimed at it (one step for a motion in its favour). An old enemy does the
  opposite. Regard already moves a vote; this is what the years add.
- **In a civil war** an old friend is two points likelier to take the side your house is on, whether you are the one
  marching or have declared in another's war. An old enemy is two points likelier to take the other.

### A physician for a relative

Someone of your house who is ill or ailing is listed on the life panel with a button: **send your own physicians**
(600 credits from your purse). Their health rises 6, an illness is cured, and they think 8 the better of you.

## Retirement

**You can lay the house down in life**, from the age of fifty, or at any age if your health has failed, to an heir who is
of age. You play on as the heir. The old head stays in the house as its elder and teaches the new one, as a mentor does.
The house gains 2 legitimacy for an orderly passing.

**Your relatives retire themselves.** At seventy, or when their health fails, they lay down their commands, governorships
and duties without being told.

## Funerals

When one of the house dies they are buried, and what is said over them is made from their record: who they were and
what they were called, the two or three things of most weight they did, who stood at the grave, and a last line from
their nature or their renown. The last twelve are kept and shown on the Dynasty screen, with the health of the living.

## Where it is

- **Character sheet:** a Life panel. Your health against what your age allows, your renown and name, old friends and
  enemies, what you have done, and the three things you can do.
- **Dynasty screen:** the health of the house, and what was said of the dead.

## Events

Seven, in `content/events/life.json`: the wound; the physicians' verdict; an old friend; a friend is buried; the enemy is
dead; a death in the house; the elder at the table.

## For content

- Flags each year: `head_ailing`, `head_frail`, `head_ill`, `has_old_friend`, `has_old_enemy`, `elder_in_the_house`.
  Deeds: `wounded_now`, `old_friend_died`, `old_enemy_died`, `funeral_held`, `head_retired`.
- Character flags: `ill`, `frail`, `retired`, `old_friend`, `old_enemy`, `wounded`.

## How it is built

- `Character.Health` for everyone; `Character.Life` (deeds, wounds, ties) for the player's house; `World.Funerals`.
- `LifeSystem` is static and uses no dice. Death is still rolled by the dynasty system, with health as a multiplier.
- Other systems write into the record through `LifeSystem.Note`.

## Checked

Eight tests. The soak passes on all eleven starts. The survival probe with health bearing on death: lines ended in 1 of
80 runs (3 of 80 before). In the browser on *The Old Lion*: the old head laid the house down in the first year and
died the next; the new head's record showed becoming head and a child; and the funerals read "…is dead at 69. At 68:
laid down the headship of House Castellan in life… Nobody present can remember a promise of theirs broken."

## The elder's year, and old ties at the count, in a court and in a match

Closed straight off the register on 2026-11-15 (docs/GAPS.md F5, F6).

**The elder.** One who laid the house down in life and is still of it is not idle. Each year, unless they are ill or
frail, they do one thing nobody asked for, the first of these that applies:

1. Two grown people of the house are at odds (either thinks −20 or worse of the other): the elder sits them down, and
   each thinks 6 better of the other. The head is asked what they make of it.
2. The head is carrying too much (stress 50 or more): the elder takes 6 of it.
3. Somebody who heads another house still thinks 30 or better of the old head: the elder speaks for the new one, and
   that person thinks 3 better of them.

Those who were the elder's friends for eight years are the house's friends while the elder lives. The life panel says
what the elder did last year.

**Old ties.** They already counted in the Senate and in a civil war. Now also:

- **At the count.** Old friends canvass for anyone of the house who stands, on the ladder or for the consulship: 2
  votes for one who heads a house, 1 for anyone else, six at most. Old enemies cost the same.
- **In a court.** A magistrate who is an old friend finds for the house when it is before them, whatever the law says;
  an old enemy finds for the other side. The upright rule on the law, whoever is before them.
- **In a match.** A family whose head is an old friend is readier to marry into the house (+10); an old enemy's is
  not (−15).

### Asking the elder (F7, the same day)

The head can ask the elder for one of the three things this year. What was asked is done first, and done with a will
(2 more than unasked; and the elder takes work off the head's desk however little the head is carrying). If it cannot
be done (nobody is at odds, nobody still thinks well of the old head) the elder does whatever most wants doing and
says so. An elder who thinks ill of the head keeps their own counsel and is not to be asked; nor is one who is ill.

## Not in this piece

- **Only your house has a record.** Nobody else's deeds are kept, so nobody else has renown or a name, and only your own
  dead are spoken over.
- **Reputation is only renown.** There is no ill fame: nothing a person does is held against them as a person.
- **No duels**, and no wound but in battle.
- The elders of other houses do nothing (docs/GAPS.md X1).
- Two of the seven events (the wound, the elder at the table) were not seen to fire in the soak.
- The numbers are first guesses. Health makes the hale live a little longer and the very old die a little sooner; what
  that does to the length of a reign has only been checked for not breaking a line.
