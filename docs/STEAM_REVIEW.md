# What keeps it from feeling like a game off Steam (2026-11-28)

The owner asked what keeps Praetoria from being a high-quality game you would see on Steam rather than something
"cool" that does not feel professional. Four critics played the finished build (main at 7dc7d4c) from four angles,
about a hundred years between them: the first hour as a new player, the look and feel, a 55-year campaign, and the craft
(screen sizes, options, offline). A ranker and a skeptic sorted and checked their 58 findings, and the main claims were
checked again against the code and the screenshots. What follows is the verdict, the ranked list and the order of work.

## The verdict

In still pictures the game already passes for a Steam strategy game: the title screen, the event window, the Senate and
the succession screen would sit well on a store page. What gives it away is everything between the pictures. It is
silent. The text often shows the machine that assembled it. Stories do not know who you are and come back word for word.
The biggest moments of a life arrive as a line in a list. It teaches by making you read, and nothing on the main screen
says what you are aiming for. Very little of this is a missing system: the game has more depth than most players will
reach. The gap is in what a player touches every minute (the turn of the year, the report, the answer, the news), which
is where shipped games spend their polish.

## What already feels professional

- The title, the creator, the event window (a painting, two faces, numbered answers, effect chips with exact figures,
  locked answers that say why) and the succession screen.
- One visual language across every window; tooltips that nest; confirmations that state the stakes before you act.
- Speed and stability: about a hundred years of play with no errors in the page, End Year in about 2 seconds even on
  slow software drawing, and Save, Continue, Export and Undo all working.
- When the writing is not a template it has a dry voice of its own ("Pass the salt.").

## The ranked list, in the order a new player meets it

The skeptic's rule decides the order: a player meets these in this order, Steam refunds a game played for under two
hours, and most reviews are written early.

| # | What | What you see | What it would take |
|---|---|---|---|
| 1 | It is silent | No music, no sound for a click, an event, a death or the end of a year. Options says so. A death and a wedding land the same way. | 4 to 6 music tracks and about 30 interface sounds (window, event, coin, gavel, seal, a bell for the year, a toll for a death), with volume sliders. Free-licensed music and sounds exist for all of it, so the rule against paid assets holds. |
| 2 | The words show the machine | No he or she in the text engine, so everyone is "they" and names repeat ("Iria Solenne said nothing ... By noon, Iria Solenne knew who would break"). Your own son and heir petitions you as "kin, of your own blood", like a stranger. "Stress" where every screen says Strain, "a county" in a Republic, "died at 78, of age". 246 lines use the hero's name twice or more. | Pronouns and family words (your son, your cousin) in the text engine; the full name once, then the first name; a content check that fails repeated names and a second word for one idea; then an editing pass over the worst lines. |
| 3 | Events misfire and repeat | A 68-year-old Proconsul is offered The Empty Command in year 1 and again, word for word, in year 2. Last year's fellow cadet is called the sovereign. In a 54-year game 133 reports were answered and 79 were different; "Not Seen in the Senate" came 11 times. | Conditions from the real state of the game (age, rank, hulls, worlds); a waiting time before a card can return; once-only life events; two or three versions of cards that return; a scripted 20-year run for each start that flags misfires and counts repeats. |
| 4 | The biggest moments arrive as a line in a list | A former head's death was the third line of the year's summary, under a stale "is ill, a physician can be sent for". The funeral card says "One of the house is dead" and never says who. Births and marriages are one line each. | One moment card built like the succession screen (a large portrait, the family, a fitting painting, one line of prose, its own sound) for deaths in the main line, births, marriages, coming of age, war declared on you and a world lost. Name the dead. |
| 5 | It teaches by reading | The Academy is four terms of lectures on a screen of its own; the real map is not seen until passing out. The primer is pages of text that point at nothing, and its three first steps vanish when it closes; for The New Man the third cannot be done. | Lessons on the real screen: a few small tasks a term that light up the control and tick off, lectures behind "read more", and the first steps pinned as a checklist for the first years, written for each start. |
| 6 | No goal on screen | Ten years as The New Man were 30 report cards: three lawsuits lost to the same house, the treasury down from 4,700 to 980, still a Citizen, and nothing on screen said the run was failing or what to aim for. The ambition sits inside the You window. (The game does have endings: "The line ends" and "A saga's turning".) | A goal for each start under the top bar, with steps and a bar (for The New Man, a seat in the Senate); a warning before ruin with the cause and two ways out. |
| 7 | The answer box is plainer than the question | After a choice the painted window gives way to a small dark box and "So be it", which often confirms fewer effects than the answer showed. A year with three reports is about eleven clicks before anything of your own. | The result inside the same window with the same chips, then the next report opening by itself. A small job for something seen after every answer. |
| 8 | Numbers disagree | Credits move −100 in the top bar and −200 in the summary at the same moment; one wedding answer gives two treasury figures; the House window counts 30 houses and the Senate 35. | Say which year a change belongs to; tooltips that add up to the bar; one way of counting houses. |
| 9 | Nothing moves | End Year, pressed hundreds of times, has no beat; buttons barely react when pressed (145 style rules for hovering, 3 for pressing). | A second of ceremony at the turn of the year, buttons that press, figures that count to their new value, fleets that glide, a loading screen before the map. |
| 10 | Too few pictures, faces that repeat | 18 scene paintings serve 540 events, half of them marble halls. With 6 faces for each ancestry and sex, a first advisor can wear the player's own face, and some grown women are painted as girls. The art is made by an image model, which Steam asks a store page to disclose. | Never show the player's face on anyone else (small); more faces and about 60 scenes with space in them; a decision on the disclosure before any store page. |
| 11 | It handles like a web page | Ctrl+A turns the game blue, text and pictures drag, right-click opens the browser's menu. Offline the map is black and the fonts plain (the 3D library and the fonts load from the internet). Saves live in the browser, so clearing its data deletes every saga not exported. | About a day to block selection, dragging and the browser's menu; the library and fonts copied into the game; later a desktop wrapper with saves as files, Steam Cloud and achievements. |
| 12 | Common screen sizes break | At 1280 by 720 (close to a Steam Deck) the start cards are cut in half; the text size option grows the interface by only a fifth, too little for 2560 by 1440; End Year sits over the minimap. | An interface scale from 80 to 150 percent, and the browser scripts run at five sizes every time. |
| 13 | War happens off screen and can drag on | Years of "no battle was fought"; one war stood at 0 to 10 in its 16th year with both peace terms locked. A late-game rules problem more than a look problem. | A battle report card when the score moves, sieges drawn on the map, and war weariness that ends a decided war. |

Two points were dropped as not borne out: the map's use of the processor while still (the cost came from the test
machine's software drawing) and the size of the first download (it does not matter once the game is installed).

## Fixed on the day (9a9a44b)

- Enter ended the year while a box was open (the question asked before something that cannot be undone, or what came
  of an answer), throwing away the year's unanswered reports; the skeptic lost a year three times this way. A box now
  holds the keys, and Enter opens each unanswered report in turn before it ends the year (docs/WEB_CLIENT.md, "Settings").
- A click on End Year and then Enter to close the summary ended a second year: the button kept the focus. A button
  pressed with the mouse now lets it go.
- Every new game started from seed 1, so a restart replayed the same world, deaths and all. Each new saga now rolls its
  own seed (docs/WEB_CLIENT.md, "The seed of a new saga"). `tools/browser/keys.js` checks all three.

## Built after the review: items 2 to 6 (2026-11-28)

The owner asked for items 2 to 6 first, sound left for later. Each is built, tested and documented where it lives:

| # | What was built | Where |
|---|---|---|
| 2 | The text engine says he and she by a person's sex, a first name after the first mention, and how a person stands to you ("Your son and heir, Corvin"). 909 strings rewritten; report bodies speak to the player as "you". The content check refuses a name said twice in one string (409 such strings, now none), "stress" for Strain, a county in the Republic, "of age" as a cause of death and American spellings. | docs/CONTENT_GUIDE.md ("Tokens", "Writing a person"); tests/ProseTests.cs |
| 3 | A waiting time before a card returns (10 years by default, per head, longer each return), once-only roles, conditions that read the real state (offices bind their holders, ventures, the top rung, old or new names), the wedding drawn in its own year, never fewer than two answers open (a locked answer flashes what it needs, by click or by key), three versions of the words for the cards that recur, and an audit over 20 years of every start: distinct reports answered 78.6% to 94.1% on the finished game (97.0% before the funeral and a war declared on you were let come every time, as each is news), cards back within their wait 230 to 0, misfires 277 to 2. | docs/EVENTS.md ("Measured"), docs/TOOLS.md (`events`); tests/EventCardsTests.cs, EventMomentsTests.cs |
| 4 | After End Year, a card for each of the year's big moments of your line (a death in the main line, a birth, a wedding, coming of age, war declared on you, a siege, a world or the seat lost, a rank lost, a suit or a charge), up to three, before the year in brief; the worst news first in a red band; the funeral names whom it buries and shows them in grey. | docs/WEB_CLIENT.md ("After End Year"), docs/VISUAL_SYSTEM.md; tests/YearMomentsTests.cs; tools/browser/moments.js |
| 5 | A checklist of first steps on the real screen for each start, ticked by what the player really does, with Show me ringing the control; the primer dims the screen and rings what each page describes; rail labels on for a first game; the Academy's lectures folded to a few sentences with Read more, and a report card each term. | docs/ACADEMY.md ("The first years"), docs/WEB_CLIENT.md; tests/FirstStepsTests.cs; tools/browser/firststeps.js |
| 6 | An aim on the map for each start's first ten years, in steps with Go there, then the player's ambition; a red band when the treasury will run out within three years, by the forecast or by two years of falling; a report when the house loses its last world; the Old Lion no longer wants to restore the richest house of the Reach. | docs/GOALS.md; tests/GoalTests.cs |

Checked together on the finished game: CI with 1,777 tests and both validators clean; the soak over every start (22 games,
no failures); in a browser, the keys, the year's moments, the first years (at 1600 by 1000 and 1366 by 768), a locked answer
and the aim, with no errors in the page. The events audit still finds two misfires in 480 years of play: a card drawn when it
fitted and answered later in the same year after another answer had changed things (The Empty Command, after a promotion to
Admiral that year), and three cards that lost an answer to money spent earlier that year.

Found while building them and not yet done: an answer the game is waiting for (an ultimatum, a suitor's offer, kin asking
leave, a suit awaiting a plea) is still only an alert, not a report with its deadline and its buttons; newborns are painted
with a child's face; at 1366 by 768 the first-steps list and three reports overflow the outliner a little.

## The order of work

1. A week of the small fixes anyone could screenshot: the web-page feel, the map offline, the player's face on others,
   the layout at 1280 by 720, the wording slips (stress, a county, of age), money written one way, rail labels on in a
   first game.
2. Sound.
3. The answer shown inside the event window, and the next report opening by itself.
4. Moment cards for deaths, births, marriages and war.
5. Pronouns and family words in the text engine, the content check, and the editing pass.
6. Event conditions, waiting times, once-only life events and the 20-year check.
7. A goal on screen for each start, and warnings before ruin.
8. Motion.
9. The tutorial on the real screen.
10. Art: faces and scenes.
11. The desktop wrapper, saves as files, Steam Cloud and achievements.
12. War: the battle card, sieges on the map, war weariness.

## For the owner to decide

- Music and sound: credited free licences (a line in CREDITS.md), or only those that need no credit.
- The image-model art: disclose it on the store page, or replace the art players see most first.
- The desktop version: wrap the web build, which has all the interface work, or finish the Godot layer, which is far
  behind it.
