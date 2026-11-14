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
(6 credits from your own purse: health +6, and an illness cured). Your relatives send for the physicians themselves,
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

## Not in this piece

- **Only your house has a record.** Nobody else's deeds are kept, so nobody else has renown or a name, and only your own
  dead are spoken over.
- **Reputation is only renown.** There is no ill fame: nothing a person does is held against them as a person.
- **Old friends do little.** They deepen and are mourned; they do not come to your aid in the Senate or a war.
- **No duels**, and no wound but in battle.
- **A relative's ill health is shown but not acted on by you.** You cannot send a physician to someone else.
- The elder teaches the new head and otherwise does nothing.
- Two of the seven events (the wound, the elder at the table) were not seen to fire in the soak.
- The numbers are first guesses. Health makes the hale live a little longer and the very old die a little sooner; what
  that does to the length of a reign has only been checked for not breaking a line.
