# The dashboards: an audit, and what a screen is for

The owner, 2026-11-15: "it looks like there is a lot of blank space and things that are on there are not very
informative. For example, the command deck dashboard... the lines that are on the screen don't really give you a lot of
information. I don't want the player to be overwhelmed with information, but I don't want to just put things on the
screen that don't tell you anything or show you anything."

This is the audit that followed, what other games do about the same problem, the rules taken from them, and the order
of work. It is kept up as screens are rebuilt.

## How it was measured

`tools/browser/audit.js` plays a start to turn 11, opens every screen on the rail at 1440 by 900 (an ordinary laptop,
not the tall window the other scripts use), takes a screenshot of exactly what is on screen without scrolling, and
counts: how much of the screen has nothing under it, how many numbers, charts and buttons are in view, and how many
screens' worth the page runs to. The number that matters is not on that list. It is whether a player who looks at the
screen for five seconds can say what state they are in and what to do about it. That was judged by eye, screen by
screen.

```bash
cd tools/browser && node audit.js "The Old Lion" 10
```

## What was found (The Old Lion, turn 11)

| Screen | Empty | Runs to | Opens with | The story it tells in five seconds |
|---|---|---|---|---|
| Houses | 8% | 1.6 screens | A sortable table of every house, with bars | **Good.** "I am second by rank, Aurelian has twice my fleet." |
| Worlds | 12% | 3 | Every world by region, with bars; regions beside | **Good.** Loyal, prosperous, who holds what. |
| Exchange, goods | 19% | 1.2 | Prices with a twelve-year line and what moved them | **Good**, but a third of the width is a fixed essay. |
| Galaxy | 6% | 1.1 | The map | Good. It is the map. |
| Dynasty | 14% | 2.8 | A family tree with names too small to read | Partly. The succession list is clear; the tree is not. |
| Laws | 16% | 1.5 | Three columns of small text | **None.** Nothing is bigger than anything else. |
| You | 21% | 1 | A radar chart and a bar list of the same skills | Partly. The same facts twice; "old friends: nobody" takes a third. |
| Military | 21% | 3 | Seven bare numbers, an unlabelled map, one fleet, an essay | **None.** See below. |
| House | 26% | 1.2 | The household's delegated duties | **Wrong story.** The house's money and worlds are not on the first screen. |
| Senate, the chamber | 30% | 3.5 | The hemicycle, and an empty column | Partly. Who has the seats; nothing about what is happening. |
| Chronicle | 33% | 1 | Ten bars and a list | None. A log, with half the width unused. |
| Exchange, projects | 47% | 1 | An empty table | None when there are no projects, and it does not say how to have one. |
| Office | 7% | 1 | A 3D room | It is a room. The right-hand column carries the news. |

**The Command Deck**, which the owner named, has every fault at once:

- **Seven numbers with nothing to read them against.** "Hulls 4": of how many, against whom? "Upkeep 6 a turn": can
  the house afford it? "Isotopes 0": does that matter? They sit left-aligned with half the bar empty beside them.
- **A map nobody can read.** Thirty unlabelled dots joined by lines. The lines are lanes, drawn all alike, so they say
  nothing; the two things a commander wants from a plot (where are mine, where is the danger) are the smallest marks.
- **The one thing that matters is a line of small red text in the middle column**: the tanks are dry and no fleet can
  sail.
- **A third of the screen is an essay** about the general and the Senate, in the same weight as everything else.
- **The navies** are three bars with no scale, two-thirds of their panel empty beneath them.
- The panel headings wrap into ragged columns ("THE / PLOT" beside three lines of capitals). That one is a fault in the
  shared heading style, and it is on every screen.

**The same faults, by how many screens have them:**

1. *A strip of bare numbers* at the top (every dashboard). No comparison, no direction, no meaning; half the strip empty.
2. *Headings that wrap into columns* (every dashboard).
3. *No headline.* No screen says, in a sentence, how things stand.
4. *Prose where a figure would do*, and explanation of the rules in the same weight as the state of the game
   (Military, House, Laws, Exchange, You).
5. *Three equal columns whatever is in them*, so short panels leave tall gaps (Military, Senate, Laws).
6. *Empty states that say only that they are empty* (Senate resolutions, Exchange projects, You, Chronicle).
7. *The wrong thing first* (House). Each new system put its panel at the top of whichever screen it lived on.
8. *Charts with no labels or scale* (the plot, the navies, the family tree, the chronicle's bars).

## What other games do

Looked up for this, not remembered. Sources at the end.

**Victoria 3** (economy and politics). Its designers set down rules for themselves: separate *actions* from
*information*; always show how a value is *changing*, because the game is about trends; an empty screen should show
what it could contain and what the player can do about it; everything on the map also exists as a sortable list. In a
later diary they dropped pie charts ("horrible for conveying data") for charts that show a trend, and removed a
red-and-green balance display that players misread.

**Europa Universalis IV** (grand strategy). Its lead designer called the interface "the first pillar of the design".
What it is praised for: an outliner at the side that always shows your armies and what needs you; alerts across the
top; a ledger that is nothing but sortable comparisons between you and everyone else; and tooltips that say *why* an
action is unavailable. Reviews say most systems "explain why an action isn't yet available".

**Democracy 4** (politics). One screen. Voter groups are a column of coloured bars: how pleased each is, at a glance.
Policies are icons whose *size* shows how much they matter; hovering one draws arrows to everything it affects. Its
known fault is the lesson: players who do not yet know the icons cannot read it, so a mark needs a word beside it.

**Football Manager** (dense dashboards). The 2026 edition made every screen out of tiles that opened into cards, and
was criticised as clunky and condensed. The 2027 edition's fixes are the useful part: tiles are for *information* and
menus for navigation; a screen opens *on its core content* (Squad shows the squad); tables show as much as fits
without scrolling; layouts change with the screen "to display more information and reduce empty space"; each screen
has one control strip for its main actions; and a message carries the thing it is about (the scout report, not a link
to it).

**Total War** (armies). A unit is a card that shows what it is, how strong and how steady, in one look; players
praise Shogun 2 for putting type, health and morale on a single banner, and complain both when cards are decorated
until they cannot be read and when they are so plain they look alike. A later patch added a visible tier mark "for
quick eyeballing of relative unit quality".

## The rules taken from them

Each has the game it came from. A screen is rebuilt against all ten.

1. **A headline.** One sentence at the top of every screen that says how things stand and what most wants doing,
   worked out from the state of the game. If the tanks are dry, the Command Deck says so before anything else.
2. **No number alone.** Every figure is shown against something: a rival, last year, a limit, or what it is needed
   for. A figure that cannot be given a comparison does not belong at the top. (EU4's ledger, Victoria 3.)
3. **Direction.** Where a value moves, show which way. (Victoria 3.)
4. **The core first.** A screen opens on what it is named for. (Football Manager.)
5. **Three depths, and no more on screen than the first two.** The headline; the figures; the detail, which is a
   hover or an opened row. Explanation of the rules is detail. (All of them, in their different ways.)
6. **Actions apart from information**, in one place on the screen. (Victoria 3, Football Manager.)
7. **A mark is read with a word.** No dot, bar or line without a label or a scale; nothing drawn that answers no
   question. (Democracy 4's fault; the plot's.)
8. **A thing is a card**: what it is, how strong, what state, what it is doing, in one look. (Total War.)
9. **Empty is information.** An empty panel says what would be there and how to bring it about, in a line, and takes
   a line's height. (Victoria 3.)
10. **The screen is filled, or the panel is small.** Width goes to what has most to show; a panel with three rows is
    three rows tall. (Football Manager.)

And the owner's own limit on all of it: not overwhelming. The test of a screen is five seconds, not completeness.

## Order of work

| | Screen | What it needs | State |
|---|---|---|---|
| 0 | All | Headings that do not wrap into columns; a tile that carries a comparison and a tone; a headline | done, this pass |
| 1 | Military | Rebuilt whole: see below | done, this pass |
| 2 | House | The ledger and the worlds first; the household, purses and learning behind tabs | done: see below |
| 3 | Senate | What is before the chamber and how it will go, first; an empty order paper that says what could be moved | done: see below |
| 4 | Laws | One estate per row with a bar; laws as a grid of current positions; the essay on hover | done: see below |
| 5 | You | One skills display, not two; the life as a timeline; ties as a list that is short when it is empty | done: see below |
| 6 | Chronicle | Full width; the bars labelled; a year's summary line | done: see below |
| 7 | Dynasty | A tree that can be read; the heir and the line first | done: see below |
| 8 | Exchange | The fixed essay behind a hover; projects' empty state | done: see below |
| 9 | Houses, Worlds | The audit judged both good; they kept the strip of bare numbers | done: a headline and six tiles each |

## The Command Deck, rebuilt

What it now does, against the rules:

- **Headline** (1): one sentence, by priority: a battle at the turn's end, then dry tanks, then a war, then a hostile
  fleet near a world of yours, then peace with where you stand.
- **Six tiles** (2, 3) that each answer a question and say against what: *How strong?* hulls, and your place among
  the navies as reported. *Will it fight?* readiness, loyalty and supply as three bars, with the weakest named. *Can
  it sail?* helium-3 in years of burn. *What does it cost?* upkeep as a share of what the house takes in. *What is
  near?* the nearest hostile fleet, how big and how many jumps from which world. *Whom are we fighting?* the wars and
  their scores, or peace.
- **Three tabs** (4, 5): the deck; the general and the Senate; beyond the Reach. The essay is on the second, where
  someone who wants it will look.
- **The plot** (7): your worlds and every fleet labelled by name and hulls; hostile fleets in red with their distance;
  lanes drawn faint, fronts heavy; a legend under it. What is not yours and not a danger is a small grey dot.
- **What is near your worlds** (2, 7): a table of every fleet within three jumps, hostile first: whose, how many
  hulls, where, how far, and what they are to you.
- **Fleets as cards** (8): name, hulls by class, three gauges, where it is, and one status word (ready, cannot sail,
  wavering, under way). Orders are folded under the card.
- **The navies** (2): ranked, with your row marked, their regard for you, and whether you are at war.
- **Empty states** (9): no wars is one line that says who thinks worst of you; no battles is no panel at all.

## The House, rebuilt

The audit said the House screen told the wrong story: it opened on the household's delegated duties. The cause was
worse than the order of the panels. A dashboard is as tall as the window and its grid takes what room is left; two
panels had been added under the grid, and at laptop height they took all of it. **The worlds and the ledger were
squeezed to nothing**: on a 900-pixel screen they could not be seen at all. They showed only in a tall window, which
is what the other browser scripts use, so nothing caught it. A panel is not added under a dashboard's grid again; it
goes on a tab.

What the screen now does:

- **Headline** (1), by priority: the house cannot pay its way; a world is under siege; more goes out than comes in,
  on what, and how many years the treasury would bear it; a world is restless; a debt; and otherwise what the house
  clears a year, from where, and where it stands among the houses and by how much.
- **Six tiles** (2, 3): the treasury and what a year does to it; what the house takes in and how much of that it
  spends, on what; wealth, ranked, with a bar against the richest house; debt, or that the counting-houses would
  lend; the worlds, with the loyalty of the least loyal and the average prosperity as bars; legitimacy against what
  the rank it holds or the next one wants.
- **Three tabs** (4): the estate; the household; purses, heirlooms and the will.
- **The estate**: the worlds as cards and the wealth of the houses on the left; on the right the ledger as bars with
  what is left drawn on the same scale, the stores as eight small cells, and the ventures. The line of mixed goods
  that used to be the ledger's last row is a sentence under it.
- **The household** (8): each person is a line that says how they regard you, what they are and what they want.
- The wealth table's bars had been drawn under the names instead of beside them, by a style meant for another
  screen. Fixed.

## The Senate, rebuilt

The same fault as the House, and worse: "Your standing" was five panels under one grid, each squeezed to a strip a
hundred pixels tall with its own scroll bar. None of them could be read on a laptop.

- **Headline** (1), by priority: a motion against you and how it will go; what is before the chamber and how it will
  go; and otherwise that nothing is, with your seats, your bench's seats and how far that is from a majority, and when
  the consuls are next elected. It adds that your own year in the Senate is not yet spent, when it is not.
- **Six tiles** (2): your seats, ranked, against the house with most; your bench and how short of a majority it is;
  what is before the chamber and how it leans; the consulship, when, and who leads; the fear of you against the mark at
  which the houses draw together; trust in the magistrates, with what its level does.
- **Five tabs** in place of two: the chamber; offices, parties and titles; your duties this year (marked when work is
  waiting); the ladder and the courts; the orders and the peace of the Reach. Each is one or two panels, read whole.
- **The chamber tab** opens on what is before the chamber (4). When nothing is, the panel says so, shows what was
  decided lately, and lays open what you could move (9).
- **The seats are drawn in their bench's colour** (7). They were drawn in each house's colour over a legend of the
  benches' colours, so the legend did not describe the picture. The bench legend was also overprinted by a style added
  for the Command Deck's plot: fixed.
- **An alert opens the tab it is about.** "Your year in the Senate is not yet spent" opens the duties; a vacant
  office opens the offices; a petition to join the household opens the household. `audit.js` presses each alert and
  prints where it landed.

## The Laws, rebuilt

- **Headline** (1), by priority: an estate hostile, what that costs and what would warm it; an estate the laws are
  taking toward hostility; an estate petitioning the Senate; the estates that are loyal and what they give; and
  otherwise which estate is nearest to loyalty, how far off, and the one change of law that would bring it nearer.
- **Six tiles** (2, 3): how many estates are loyal and hostile, with the marks; the warmest and the coldest, each with
  where the laws are taking it; the influence there is to spend and how many laws that would change this year; the
  laws in your gift and how many are off the custom; the law of the Reach and how much of it is by Act.
- **One estate to a row** (2, 7): its name, a bar from −100 to +100 with the loyal and hostile ends shaded and a mark
  where the laws are taking it, the figure and which way it is moving, and one line: what it gives if loyal, what it
  costs if hostile, or what would warm it. What it would give and what the laws say to it are a hover (5). A key
  under the rows says what the bar's marks are.
- **The law of the Reach is a table**: the law, where it stands, what that does, who set it.
- Two columns in place of three: the estates and the Reach's law; the laws in your gift and those beyond it.

## You, rebuilt

The same squeeze as the House and the Senate: five panels in a grid as tall as the window, each cut off.

- **Headline** (1), by priority: broken by strain; ill, wounded or ailing; the strain telling; and otherwise what you
  are, how old, in what health, how well known, what you are best at and against what ceiling, and what you want.
- **Six tiles** (2): rank, with its place on the ladder and the next rung; health against what a body of your age
  can have; renown, with the next name it earns and at what figure; strain against the mark at which it tells; your
  best skill against its ceiling; where you command from, with the valour won.
- **One skills display** (the audit's fault): the radar chart is gone, since the bars beside it said the same and
  also showed each skill's ceiling.
- **Two columns that scroll**, in place of a grid that cut its panels off: the person and the life; learning, blood
  and bonds.
- **Empty states** (9): no old friends and no old enemies is one entry that says how one is made, in place of two
  entries and a paragraph. "Your story so far: nothing written yet" is not shown at all while it is empty; what you
  have done is in the life.

## The Chronicle, rebuilt

- **Headline** (1): what the last year wrote of your house, and how much it wrote of the rest of the Reach; or, when
  it wrote nothing of you, that, and the latest thing it did write.
- **Six tiles** (2): how much is written and how much of it is about your house; wars declared, with how many are
  being fought now; battles, with how many your house won; what the Senate carried, with how many were Acts; houses
  fallen against how many stand; seats passed, with which of your line you are.
- **The years** (7): the bars have a year under them, the fullest year is named with its count, and a key says what
  red, brass and grey are. It opens on the last five years, not on whichever ten the present year falls in (which in
  the first year of a decade was one year, half empty).
- **Each year is two columns** (10): what was written of your house, and what of the Reach. The second takes the
  width and runs in two columns of its own. Each line of the Reach's has a mark for its kind, and the kind filters
  carry the same marks, so the filters are the key. A year's heading says how much was written and how much of it
  was yours.

## The Dynasty, rebuilt

- **Headline** (1), by priority: no heir; an heir who is a child; an heir with nobody behind; and otherwise who the
  heir is, how old and of what rank, by what law, how many stand behind, how many of the blood live and whether any
  is ailing. It adds that the heir thinks ill of you, when they do.
- **Six tiles** (2): the heir, with age, rank and regard for you; how deep the line is and who is next; the blood, by
  grown and children; who is unwed and of age, by name; the health of the house; which generation this is.
- **A tree that can be read** (7): it is drawn to the width of what is in it and no wider, so a small family is not
  shrunk to fit a thousand units; first names only (the surname is the house's); the text half as large again. One
  who married into the house stands beside their partner, joined by the marriage line, with their children under
  both; they had been drawn in the first row with the dead founders. The key shows the ring for you and for the heir.
- **The line of succession is first in the side column** (4), above the bonds, the health of the house and the dead.

## The Exchange, rebuilt

- **Headline** (1), by priority: an embargo; a good the house burns and has none of, with its price against the
  usual and what a few years' worth would cost against what is in the treasury; and otherwise the good that has moved
  most and why, and on what terms you trade.
- **Six tiles** (2): credits and which way they are moving; your spread and what a market of your own would make it;
  the dearest and the cheapest good against their usual prices; the wars in the Reach and what they do to prices; your
  ventures and what they earn.
- **The goods have the whole width** (10): a third of it was a fixed essay on what moves a price, and the Buy and
  Sell buttons ran off the edge. The essay is folded under the table (5); each good already says why its own price
  moved.
- **Projects, empty** (9, 10): the panel is as tall as what is in it, says what a site would show, and has the two
  ways to begin one as buttons. "How building works" is folded.

## Houses and Worlds

Both were judged good in the audit, and their tables are untouched. Each still opened with the strip of bare numbers
every dashboard had, so each has the headline and the tiles now. **Houses**: where you stand, with whom you are at
war, how many think well and ill of you with the warmest and the coldest named, and the strongest in arms and the
richest against what you have. **Worlds**: how many are yours and whether any is restless, your least loyal world
against the mark at which a world is restless, the restless across the Reach and the worst region, the unclaimed, the
free marches, and the systems with deposits and how many of those are yours.

## After

The same start and year as the audit (The Old Lion, turn 11, 1440 by 900):

| Screen | Empty before | Empty after | What it opens on now |
|---|---|---|---|
| Military | 21% | 18% | A sentence, six tiles, a labelled plot and a table of what is near |
| House | 26% | 14% | A sentence, six tiles, the worlds and the ledger (they could not be seen before) |
| Senate | 30% | 15% | A sentence, six tiles, what is before the chamber, the seats by bench |
| Laws | 16% | 17% | A sentence, six tiles, one estate to a row |
| You | 21% | 18% | A sentence, six tiles, the person and the skills |
| Chronicle | 33% | 19% | A sentence, six tiles, the years, and each year in two columns |
| Dynasty | 14% | 17% | A sentence, six tiles, a tree that can be read, the line of succession |
| Exchange, goods | 19% | 16% | A sentence, six tiles, the goods at full width |
| Exchange, projects | 47% | 52% | A small panel that says what would be there and how to begin |

The empty figure moved less than the screens did, and on two it rose: the measure counts space, and a headline with
room round it, or a panel made small because it has little to say, is space. What changed is what the audit judged by
eye: every screen now says in its first line how things stand, and no figure at the top of one stands alone.

Three faults were found on the way that were not design at all. On the House, Senate and You screens, panels had
been added under a grid that is as tall as the window, and at laptop height they squeezed the grid, or each other, to
strips: the House's worlds and ledger could not be seen. The wealth table's bars were drawn under the names. And a
style added for the Command Deck's legend had overprinted the Senate's.

## Sources

- Victoria 3 developer diaries on user experience, interface and data visualisation:
  <https://forum.paradoxplaza.com/forum/developer-diary/victoria-3-dev-diary-29-user-experience.1506484/>,
  <https://forum.paradoxplaza.com/forum/developer-diary/victoria-3-dev-diary-30-user-interface-overview.1507166/>,
  <https://forum.paradoxplaza.com/forum/developer-diary/victoria-3-dev-diary-61-data-visualization.1546730/>
- Europa Universalis IV: <https://www.pcgamer.com/uk/europa-universalis-4-preview>,
  <https://www.pcgamer.com/au/europa-universalis-iv-review>, <https://www.pcinvasion.com/?p=62838>
- Democracy 4: <https://positech.co.uk/cliffsblog/category/democracy-4/page/3>,
  <https://gamepretty.com/democracy-4-basic-gameplay-guide/>,
  <https://forums.positech.co.uk/t/my-recommendations-for-improvement-mostly-quality-of-life-items/15398>
- Football Manager 26 and 27:
  <https://www.footballmanager.com/features/total-football-total-control-fm26s-reimagined-user-interface>,
  <https://www.footballmanager.com/fm27/features/fm27-clearer-interface-smoother-navigation>,
  <https://www.thesixthaxis.com/?p=465393>
- Total War: <https://academy.totalwar.com/battle-what-the-ui-is-and-does/>, <https://www.totalwar.com/?p=16273>,
  and player discussion at <https://steamcommunity.com/app/779340/discussions/0/1733216261947996227>

The Paradox forum pages would not load for a second, closer reading; what is said of them here is from the search
results' summaries of those diaries, not from the full text.
