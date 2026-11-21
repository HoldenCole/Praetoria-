# What a player wants to do and cannot

The owner, 2026-11-20: "I'm sure there are lots of small things like this throughout the game so we need to go and actually
do a full play through and just try to do different things and see what comes up ... as you're playing think about what do I
want to do in this situation and can I do it, or what would be nice to do in this situation and can I do it? Do that a few
times and make a continuous running list and then put them in easy, medium, and hard categories for things to be done."

## How it was done

Five continuous games were played in the published browser build, each by a different kind of player, each asking at every
step "what do I want to do here, and can I?", trying it in the real screens, and keeping a running list.

| | who | start | years | rows | raw list |
|---|---|---|---|---|---|
| **B** | a builder and governor | The Old Lion | 26 | 70 | `docs/playthroughs/builder.md` |
| **S** | a soldier | The Risen Officer | 21 | 81 | `docs/playthroughs/soldier.md` |
| **P** | a politician from nothing | The New Man | 25 | 106 | `docs/playthroughs/politician.md` |
| **F** | the head of a family, and its money | The Circling Heir | 31 | 102 | `docs/playthroughs/family.md` |
| **N** | a first-time player | a custom character, then The Academy | 10 + a term | 82 | `docs/playthroughs/newcomer.md` |

441 rows in all. This document is those rows merged, with duplicates folded together and sorted by how much work each is.
A reference such as **B12** is row 12 of the builder's list, where the game's own words are quoted.

**What to trust.** The players did not read the code or the design documents; they learned the game from its screens. They
drove a headless browser through a small driver (`tools/browser/driver.js`), which reads every screen as text and every
button with its tooltip, so a few things about the mouse and the map could not be judged (marked where it matters). No game
crashed, no turn hung, and the browser console stayed empty in all 113 years played. Six reports were checked against the
code afterwards and all six were as reported; those are marked **(confirmed)**. The rest are as the players saw them and
have not been checked.

**Sizes.** Easy: a label, a tooltip, a number, a button, a wrong link, a confirm box, a small rule. Medium: a new control
or a rule with a screen. Hard: a new system.

## Progress

| | what was done | date |
|---|---|---|
| 1 | No rival is granted a world of the player's: the petition passes over the player's house. | 2026-11-20 |
| 2 | Save says what it keeps ("as the year began") and for how long; Quit asks first and says what is lost; a new saga over a saved one asks first. A save in the middle of a year is still the start of that year: keeping the middle of a year is Medium ("a Save that saves where you are"). | 2026-11-20 |
| 3 | One rung a year by any road. Seizing takes ships of the house's own (two for each rung of the rank). Legate, Proconsul and First Citizen want one, two and three worlds. Each button shows its cost and, when refused, the reason; each asks before it is done. | 2026-11-20 |
| 4 | A venture is sold at what it cost in its first year, rising to its full value over five. | 2026-11-20 |
| 5 | One line of succession, read by every screen: the Dynasty header, the You window, the will, the Ambitions tab and the succession itself. Someone who married in, someone taken in and a head who has stepped down stand behind everyone of the blood. The header says why the heir is the heir. | 2026-11-20 |
| 6 | The top bar's forecast and the ledger count what the house spends in the year: building instalments (and hastening), tutors, allowances, the household's keep. The ledger lists them under "Also going out this year" and ends with where the treasury will stand. End Turn asks first when the year would end below nought. Your own purse is on the top bar. | 2026-11-20 |
| 7 | Declaring war, asking a house to step down, a treaty marriage and an accident each ask first and say what will follow (their hulls and yours, whether they will yield or fight). Asking a house to step down says in plain words when it means war. | 2026-11-20 |
| 8 | Every choice on a report says what it gives and takes (credits, goods, legitimacy, stress, who will think better or worse of you), warns when it would take the treasury below nought, and a greyed one names what it lacks ("needs tactics at least 6 (you have 4)"). After choosing, a box says what came of it. | 2026-11-20 |
| 9 | Peace with a house has terms, by the war's score: ask nothing, ask tribute (the lead times three; a house with no coin pays in goods), or, from five ahead and with the stronger fleet, name the world you want. Behind, you may pay tribute or give a world. Each term says what it does and, when it would be refused, why. | 2026-11-20 |
| 10 | Battles are kept: place, sides, hulls before, hulls lost, how each side weighed, the chance before a shot was fired and what told (the ground, the order of battle, who led, readiness). A Battles tab on the Military window and an alert the year after. Before sailing the map says what stands at the destination and your chance in a hundred. | 2026-11-20 |
| 11 | A fleet that is picked says so in a bar over the map; clicking a world only looks, and the order is given by a button that names the journey and the odds; a fleet under way can be called back (struck out the same year, otherwise as many years home as it has sailed). | 2026-11-20 |
| 12 | A suit that names your house is no longer decided the year it is brought: it stands on the docket for a year (Senate, Ladder and courts) with who brought it, over what, for how much, which praetor hears it and how the law leans. In that year you may come to terms (the side the law is against pays half, now) or send someone of the house to plead (1 influence: a strong pleader turns a doubtful case, or keeps a partial praetor to the law). Every ruling that moved your coin is kept and listed, shown in the ledger, and raises an alert. | 2026-11-20 |
| 13 | A vote costs what the house is worth: five, plus its weight in the Senate, plus two for each rung of its rank, plus five if it leads a bench, plus more if its head thinks ill of you. The coin goes to the head's purse. The upright will not take it and one who hates you will not either, and the button says so. One time in four it comes out at the count: the vote is struck and you lose 3 legitimacy. A bench does not follow a leader who was bought. The forecast and the count now read a bench's leader the same way (they had not, which is why 94 to 62 came in at 138 to 0), and when a count still moves far, "Decided lately" says why. | 2026-11-20 |
| 14 | The house is told what happens to it, as alerts that open their subject: a child born (whose, and where in the line), a death (and what it leaves empty), a war declared on you (their hulls, yours, and why), a tutor gone unpaid, illness, an attempt on a life, a foreign fleet in your space, a ruling overturned. | 2026-11-20 |
| 15 | Every window opens on a tab or a subject, and some fifty alerts now name theirs: a siege, a revolt, a demand for a seat or a coming battle open the map on that system; Exchange alerts open goods, shares, ventures or projects; Senate alerts the chamber, the consuls, the offices or the courts; family alerts marriages, upbringing, ambitions or the person. The fleets and holdings listed at the right open where they are. | 2026-11-20 |

### The Easy list, done so far

Marked here as they are done; the list itself below is left as the players wrote it.

| part | done |
|---|---|
| Reports | Every cost and effect under each choice; a greyed choice names what it lacks; a choice that would overdraw says so; what came of it is said after (with 8). An answered report, opened again, shows the answer that was given. "Set it aside" says that it lapses at the year's end. A report raised by something that happened carries a line of facts under its prose: the court cards say who sued whom, over what, which praetor and for how much; the battle cards say where, the hulls on each side and the losses; a commission names the office; a peace names the world. The election card says the count: the office, the year, each name and its votes, and who was chosen (the count is kept on the world now). A report raised by something that happened is drawn ahead of the rest while it is a year old or less (five times likelier), so the card for an election comes the year of the count; and a report read within ten years is half as likely to be drawn again. The conviction card no longer speaks of money when the offence was a bent ruling or a bought praetor, and names the offence, the praetor, who brought the charge and the fine. "Not Seen in the Senate" says what counts as being seen and what it costs. One who asks for a place in the household and is left unanswered goes after two years, says so, and does not ask at the same door again for twelve; the wait reads "this is the last year they will wait", not "1 more year" for ever. |
| Alerts | Every alert opens its subject (with 15). New alerts: a suit against the house, a ruling, a foreign fleet in your space, illness, a tutor gone, a birth, a death, a war declared (with 12 and 14). The fleets and holdings at the right open the map where they are; "open on the map" opens that system. The Senate's alerts go once you have declared on every open motion, and the offered-venture alert goes when the offer is taken or refused. Also new: manpower at nought, a world with no living governor, a rank you could take now, a holder withholding its dues. "The Exchange ▸" under Ventures opens the Ventures tab. The council's seats follow the posts: the Steward and the Admiral are whoever holds the Steward's and the Marshal's work under House, the Household, and with nobody named, the ablest relative of that service who will serve (one who defies you does not speak for the house). Each seat is a button that opens the place it is filled. A fleet's row in the list at the right picks that fleet and brings the map to it. |
| Words | No internal names ("shipyard_world", "title rank 1" are now "a shipyard world", "the rank of Prefect"); the Commission motion no longer says "the office of an office"; "pays back in never" is "does not pay itself back at this price"; a claim is to "the rank of Prefect", not "its baron rank". "Needs an isotopes deposit" has its article. A governor's name no longer runs into the chip beside it ("Tiber Quillonable"). The first letter of a report keeps its space ("A market", not "Amarket"). A house that falls loses "the rank of Warden", not "its knight rank". Nobody is labelled "sovereign" in a report where no throne stands: the label is their rank, or "holds the chair"; and the report that told a house of one corvette it had "grown beyond the reach of coalitions" now waits until the house is truly feared (threat 15). Years are called years where "turn(s)" was left: a law's lock, a campaign, a revolt, a loan, a charter, the next election. |
| Two influences | The house's store of political capital is called **clout** everywhere; "influence" is now only the year's pool, and both tooltips say which is which. "Favours for coin" shows what it will really pay (half without an exchange of your own) and says why. The pool cost is now printed on Take in, on enacting a law, on giving a fleet an admiral and on the roads up the title ladder. |
| Refusals | A refusal for want of goods says only what is short ("3 materials short"), not the whole price. The campaign button says to choose its object first. "No influence" is now "needs 2 influence this year (1 left)", for all three pools. Refit, merge and appointing an admiral each give their one true reason. |
| Purse | The head's purse is on the top bar (with 6) and on the You window. Coin moves between purse and treasury in any sum. A refusal for want of coin in the purse gives both figures and says where to move it. "Insolvent" and "the tanks are dry" say what the purse holds and open the place to use it. |
| Money | "Hired crews" on the Exchange is "Manpower", as on the top bar. The quantity box is labelled "lot", and the buttons read "Buy 5 · 12 cr" and follow it as you type; the same on shares, where the box was taken for a limit of five. A nation is no longer tagged "POOR" in the standings: it says "a nation", and the wealth tag says how it is ranked. A venture's "+24 a year" now says it is goods valued at today's prices, not coin. A belt claim says what it brings up before it is staked (materials by the belt's richness, some rare earths, an exotic from the richest, and its keep), and each belt in the list says its own figure. "The Helium Glut" comes only when helium-3 is cheaper than it was a year ago (a new "price" condition), and selling into it pays what the Exchange pays that day, shown under the choice (a new "sellAtMarket" effect): it paid 4 credits for four tanks while the market paid 31 for five. A good that cannot be bought says "not to be bought" where it showed a buy price below its sell price, and the isotopes note says they cannot be bought and may be sold. Your own order is named as the cause of a price move, with how much of the merchants' stock it took ("your own order of 5 took 12% of what the merchants held"), not as "heavy buying by" your own house. A war bond's button carries its rate, what is repaid in each ending and that a chair that cannot pay pays short. A private sale of a venture says what it is (the venture stays whole) and now earns a little regard: the buyer's head thinks 4 the better of the seller. A director may pay the house's own coin into a company ("Pay in N credits"), for no new shares. The steward's wage is printed on the post (2 credits a year, and what happens unpaid), and a steward no longer begins a building with the materials that the year's shares of sites already rising will want. |
| Numbers | A tooltip and a plainer label for: legitimacy in the top bar, "threat read in us" (now "how dangerous others think us", with what raises it), the ORDERS column (S·A·C), "hull(s) without strain", a crisis's severity and who set it going, "how near" on an ambition. Also explained now: the three estate bars under the hemicycle (a legend, and a tooltip each on what makes the weight); the marks on a person ("parvenu", "debt to crown" and the rest, each with a tooltip); Wealth; the war's clock ("how near"); the figures in the table of matches (a legend with the number a family agrees at); a nation's strength ("42 hulls") and its "regard +12"; the rating word in the creator; what every skill is for, wherever a skill is shown, chosen or asked for; that "+5 from study" is points toward the next step, with how many the step takes; what a tutor costs, in credits a year, from the house treasury, and what one adds. |
| Fleets | An order can be struck out or the fleet called back (with 11); no child is offered a fleet, and the list is by tactics, with age. Every "Lay down" button says the yearly keep, the fuel and the readiness a new squadron starts at. "Cannot sail" goes the moment helium is in the tanks. The Military window has a link to the worlds where hulls are laid down. A fleet can be renamed, and a new squadron takes a name no other of yours has ("2nd Squadron"). A system's panel lists the fleets at that system (yours and others', and those on their way to it) apart from the list of all your fleets. A campaign ends when the house is at war with nobody, not only when its term runs out. The readiness gauge says what raises and lowers it, with the numbers. A system is the space of whoever holds its world (a claim or a station there no longer makes it another house's), so "arrives at X — House Y's space" names the holder; and in space that is neither side's, the fleet that came later is the attacker. Under Wars: every war the Reach or the house is fighting beyond the Reach, with its score and a way to its orders; and only the sieges that are yours (your world invested, or your fleet laying it). The raid switch reads as an order with the present state in brackets. "Since turn 4" is "since year 4". A fleet that cannot be sent says why at the map: "the tanks are dry: helium-3 is bought on the Exchange", "an enemy's fleet holds a gate on the road", "no lane runs there". A voyage of three years or more asks first. |
| Worlds | "Invest" says what prosperity does and that it drifts back. Every building line says what it gives: the Fleet Supply Depot (supply within two lanes, not one) and the Public Granary, which did nothing and now eases its world's unrest by 2 more a year. A stalled site has "Pay the instalment now", which makes up the lost year. A standing building's upgrade line says what the next level gives, whether or not you can pay for it. "They cannot find the coin" on a commutation now says what it takes and what the holder has. A rising building can be paused, rushed, paid (when stalled) or abandoned from its own line in the system panel, not only in the Exchange; Rush says the whole sum it takes; Abandon asks first and says what comes back. The open row of a system no longer shows last year's figures after End Turn. The House card counts a world's slots as the system panel does (what stands, what is rising and the works on the world). A dead governor is no longer shown in the chair, and the list of who could govern leaves out the one who already does and is ordered by administration. Withheld dues say what ends the withholding, and the house over the province is told when it begins and when it ends. Every building line says how long it takes, "at once" included. A work begun on a system's main world is named by the holding, as a building there is ("at The Lion's March", not "at Greywater" for one and the holding for the other). A world at its population cap is marked "full" and says that Habitats make room. The colony line shows the first share ("10 credits, 7 materials… now, the same in each of 2 more years"), and says the fleet that made the claim may leave once the colony has sailed; "it lies in a nation's space" says such a world is taken in war, not settled. The heavy-dues button says whether this holder is your match (and so will stop paying), and Ask says whether this holder would step down or fight, with the hulls, before the click. A hastened work that the treasury could not hasten says so that year (a notice, and on its line), and "next year" on a hastened work is the hastened share. The governor's bare "68" is "aged 68", and administration is "of 10" with what it does. |
| Confirms | War, a treaty marriage, asking a holder to step down, a rung of the title ladder, quit, a new saga over a saved one, a bribe, terms in a suit, a peace (with 2, 3, 7, 9, 12, 13); and now selling a venture, incorporating one, a law that locks (with how long), a change of succession law, and the consul's veto. A demand for a province says that to stand, or to leave it unanswered, is war. |
| The Senate | Each open motion says how you have declared, or how your house will vote if left alone; the votes you paid for are listed on it; a bribe shows its price and its risk before it is paid. "(yours), : sat." has its office. One removed from office for taking money can no longer be elected consul or commissioned while the bar stands. The ladder says when the next election is and how many names are down, on the panel and on each candidacy. The countdown is counted from the year being played: the election year says "at the end of this year" (it read "in 5 years" and "in 10 years"), and the alert comes in that year and the two before. A whip or a bribe says that it was heard and by how much it moved the house; voting against your bench says what it cost. Each motion form says what the motion does if it carries (legitimacy, levy, embargo years, what the chair pays) and when it is voted. A house that may not move a motion is told why once, with the forms folded away, and "qualifies: move a Commission" no longer says so when it cannot. A censure, an embargo or a breakup names its ground (blood between the houses, a war, or what the mover's head thinks of yours). The Triumph and Recall forms (and every form that names a house, a region or a nation) judge the one chosen in the list, and say why not beside the button; the alert that a general has earned a triumph opens the place a motion is moved. The chair of a committee now weighs one more in that committee's estate for as long as it is held: the weight used to be written to an estate that did not exist ("military" for the fleet's committee) and was wiped each year in any case. A house without a voice is told it has "its votes but no voice", wherever it was told three different things ("1 of its seats", "no seat", "the Senate hears a Prefect"), and the Orders panel says exactly what that forbids (moving a motion) and what it does not (standing for office), and the four ways to a voice. The consulship has one rule, said one way: "a consul must be 40 or older, to have been praetor" (the Consuls tab said "rank 2 on any track"). The census refusal gives the number ("a standing of 20, and the house has 11"). A year's duty, taken, says what came of it in a box then and there: what moved, and what the chronicle wrote ("reads the year's accounts and finds them in order"). |
| Laws | An option with no yields says so plainly ("changes no yield and no cost: what it moves is how the estates think of you"); enacting asks first and says the effects, the estates and the years it locks. Each law row has a visible "change ▾" control. |
| The family | Every row and every profile says how the person stands to you ("your niece", "your son's wife", "married into the house") and whose child they are. Nobody is named as the rival in their own suit (the other family proposing the same match now brings their yes nearer). "Too young to be promised" gives the ages. The succession screen's design note ("The game turns ceremonial...") is a plain line on what changes on the screen, and the screen says what the new head can do in a year and why it is less than the last head's. Each courtship lever says what it is worth before it is pulled ("their favour +10 (now 46)", "the family's yes +12, and they agree at 50", the odds a whisper is traced). The contract marks the dowry and the name chosen with a tick, says what each term does to their yes, what custom gives here and why, and when the bride's house cannot raise the sum ("they can raise 13"). The dowry agreed and the dowry paid are both kept: the marriage row shows the terms ("13 came (24 were agreed; no more could be raised)", "the children's name by custom") and a notice says so at the wedding. A child too young to be promised is shown greyed with the age it opens. Each succession law says what it prefers, who the heir would be under it, and its cost. Fostering says what it protects from, that schooling goes on, and when the child comes home. A second "yes" to a match in the year of the first marries nobody and says why. The head's own marriage stays the head's to tend when the spouse has been taken into the household. Nobody of the house is cast in a report as the outside buyer, the giver, the envoy or the old friend in the city; and "their family", of a spouse who lives with you, is the house they were born to. A venture that is a relative's own: "Sell to House X" and "Fold into" are greyed with the reason, as the open sale was, and its earnings are shown going out to them in the year's accounts (they were counted as the house's income). The contract says where each of the two will live: marriage moves nobody, and a spouse comes to your house only if taken into the household. A will may leave smaller legacies (a tenth or a quarter of the purse) to as many as you like besides the one larger share, never more than the whole; the panel says to pick a name first. |
| Housekeeping | The top bar says "Year 7 · 2nd head" and explains a generation; a dowry says which way it went. The primer (which opens by itself in a first game) ends with three things to do. Each scenario on the title says how hard it is and why, and the Academy is marked "start here". "Continue" names the house, its head and the year. Export says the file it wrote and where to load it. Every world option on the title has a tooltip (the powers beyond, how often they attack, the Great Crisis, family autonomy, each saying what it changes), and an ambition or a career chosen says what it is in a line under it. In the creator: with two natures chosen the rest are greyed and say "un-pick one first"; "Begin" and the red line follow the character as it stands (a parent was not drawn again when the points changed, so it stayed off after the budget was met); the family name follows the person's until one is written; a nation's world is "beyond the Reach: a nation's", not "unclaimed", and a standard start may not take it; each venture type has its description and yield. The Academy gives the full course until the cadet asks for the short one. Keys: every window on the rail has one (1 to 9, then 0, - and =) and shows it in its tooltip, Enter ends the year and says so on the button, ? opens the primer, and the primer has a card of keys. The map's labels are clipped under the top bar. |

#### What of the Easy list is not done

Every line of the Easy list below has been gone through. These parts of it were left, and why:

| item | left as it was because |
|---|---|
| "Falls back" and stays (S40) | Not reproduced: a beaten fleet is given a haven and an arrival year and does sail. What the soldier saw is most likely the other house sending it straight back. The attacker and "whose space" halves of the note are fixed. |
| Ask to step down: what I gain and lose, and who would be a good holder (B20) | The odds are said before the click now. A preview of the dues and levy that change hands, and marking clients and friends in the list of houses, is more than wording: it is on the Medium list under Building and holding. |
| "Heavy buying" and the terms that changed (N56) | The cause names your own order now. The line that your terms went from 30% through the caravaneers to 10% through your own market still changes without a word. |
| Two notes on one price (S36) | Both are true (37% above its usual price, and down 6% on the year). They are not yet said in one sentence. |
| The count by house (P42) | The election card gives every candidate and their votes. Which houses voted for whom is not kept. |
| "The First Case" in a judge's thirteenth year; a loan asked by in-laws at war with you; a grown man offered a child bride (F76, F65, F77) | Each is one report or one list that wants a condition of its own. Not done in this pass. |
| Venture sites in the creator (N12) | Each type is described now. The list of fifty worlds is not cut down to the ones where that type can stand. |
| Home button with a panel open (B36) | Seen once in a headless browser and not reproduced. The labels over the top bar are clipped. |

### The Medium list, done so far

| part | done |
|---|---|
| Telling the player what happened | **The year in brief.** As each year opens, a card says what the year before did to the house, grouped (the family, war, the Senate, the courts, works and worlds, money) and each line opening the place it can be seen: births, deaths and the seat passing, illness, weddings with their dowries, wars begun, battles with the hulls lost on each side, rulings and what they cost, the count for anyone of the house who stood, what was finished building, and where the treasury stood and stands in credits, materials, manpower and helium-3. It waits behind a succession card, closes with Enter or Esc, can be turned off (kept in the browser), and opens again from the year in the top bar. A project of yours that finishes is now told to you as well. (An outcome after every report, the battle report and the forecast before sailing, and the forecast that counts instalments and wages were done with the fifteen: 6, 8, 10 and 11.) **Why loyalty and prosperity are what they are.** Each world's card on the House window has a list, cause by cause: for unrest, what moves it each year (a solvent or an insolvent house, the tithe, where the world lies, the granary, the laws of the house and of the Reach, the works of the system) and the sum; for prosperity, what it settles at (where any world begins, the governor's administration by name, buildings, unrest, the tithe, laws, a hostile governor, a siege, a revolt, the capital, ventures, works) and the sum. The same lists are in the two gauges' tooltips. They are not a description of the rule: the year's turn now applies these very lists, so the screen and the reckoning cannot part. **A declaration of war on you is a report.** "War Is Declared On You" comes the year it happens (or the next), and under its prose says who declared, their hulls against yours, why (blood between the houses, what their head thinks of you, the hulls they have and you have not, or what you hold), and what they would take for a peace today, by the same terms the Military window offers. It can be answered in the Senate for an influence (legitimacy +2), or not. The reason is kept on the war. **A death report.** Each death in the house is kept with how they died where it is known (in battle, in a duel, by a knife, of age, of what ailed them), each post it left empty (a world's governing, a fleet's command, the Steward's or the Marshal's place, an office of the Reach) with the place it is filled, and what they had and whose it is now (their purse, their heirlooms, a venture that was their own, what the head owed them). It is in the year in brief, where each empty post is a line that opens the place to fill it, and in the Dynasty window under "The dead, and what they left", with a "Fill it" button on each post. **Last year's accounts.** Under the ledger: where the treasury opened and closed, and three lines that add up to the change exactly: your own acts in the year, what the standing ledger really brought when the year turned, and everything else the turn of the year took or gave (shares of what is building, tutors, wages, fines, dowries, tribute), each with a tooltip of what is in it; the same opening and closing for materials, manpower and helium-3. It opens by itself when the year closed below nought. The one-off lines are one figure, not yet itemised one by one. |
| Things that happen to the player without a choice | (The petition that took a world unasked, and the one heir, were done with the fifteen: 1 and 5.) **Reports that fit the house they come to.** The forge-world's reactor comes only to a house that holds a forge-world, and the bumper harvest only to one with an agri-world; six more of the steward's reports that speak of "your worlds" (the petitioning world, the loyal world, the exotic find, the overextended garrison, the grain dole, the pirate tribute) come only to a house that holds a world. The steward a report names is the house's Steward, or the report does not come. The wedding card is for the head's own wedding in the year of it; a relative's wedding is told as news instead. The conquered-world report comes in the year the world falls or the next, names the world, and is weighted to come then; after that it is gone. "The First Case" and "The First Command" come within four years of reaching the rank and not after: the game now keeps the year each person came to the rank they hold. (The coalition report for the poorest house was closed in the Easy list.) **A siege stays with the house that began it.** While its fleet is over the world the siege is its own, whoever else arrives and however many hulls they bring; of two who come to an unbesieged world, the one who came first lays the siege. When the house that pressed it sails away and another at war with the holder remains, that house takes the siege up at the years already spent, not from nought, and the player is told when a siege becomes theirs this way. **When the head may stand, it is the head who is consul.** A house's candidate for an office was its highest in rank, so a head who declared could see a sister made consul. The head of the player's house is now the candidate whenever the head is qualified. When the head is not and a relative is, the button says "Put (name) forward for consul", its tooltip says why the head may not stand and who would be consul, and the declaration names her. |
| War and peace | (Peace by the war's score, the battle report, the forecast before sailing, striking out or recalling an order and the picked fleet's bar were done with the fifteen: 9, 10 and 11.) **An envoy's peace can be read and is reckoned by the war.** The envoy's card says how the war stands (the score, and the hulls on each side) and lists what may be asked and what would be refused, with the reason, by the same terms the Military window shows. Its answers are a peace asking nothing, the tribute the score says is owed (in credits, then goods, with what cannot be paid said beforehand), a world where one would be given, or no. Before, "name a price" paid a flat 8 credits and was open only to a greedy, ruthless or proud head. **Detach hulls, pay hulls off, shift a flag.** A fleet's card has a Hulls fold: detach any number as a squadron of their own where the fleet lies (the lightest go, the admiral stays, it costs nothing), or pay them off at a world or station of your own for a quarter of their materials, with the yearly saving shown and a confirm. A person who commands one fleet can be given another: the flag shifts and the fleet left behind has no admiral until one is named, where before it was refused. **A fleet in your space says what it is there for, and can be asked to go.** Every order another house gives a fleet now keeps its reason (to hunt raiders, to put down a rising, to guard a march, to take a world back, to stand over your seat as a threat). The map's list of fleets at a system of yours says it, with how long the fleet has lain there, whether it is at war with you and what siege it presses. "Ask it to leave" costs one influence: its head obliges when they think no worse of you than -20, or when you have as many hulls there as they; otherwise the button is greyed and says which figure falls short. A house that goes does not send a fleet to stand over you for five years. **A fleet held by office states its terms** on its card: whose it is and since when, that there is no term, the three things that end it, who pays for it and fuels it, and that it is kept apart from your fleets and is not yours to pay off. **How a world is taken** is on the card of every world of a house you are at war with: keep a fleet over it while none of theirs is there, the years it would take here, what the siege does while it lasts, what lifts it, and how it stands now. There is one way, the siege; the card says so. **A supply forecast.** With a fleet picked and a world chosen, the order bar says whether the fleet is fed there and by what (a world within a lane, a depot within two, a campaign target), or how fast supply falls, the year it begins to tell and the year hulls begin to desert; the same is on each fleet's supply gauge. Not done in this pass: laying hulls up without breaking them, a forecast of fuel against what the house brings up, and the map going black on a lost graphics context (seen only in a headless browser). |
| The Senate (begun) | (Bribes priced by seats and regard with refusal and exposure, the count after an election, and the title ladder were done with the fifteen: 13, 3; the count is on the election's card from the Easy list.) **A roll call with each house's reason.** Under every house in a motion's list of benches is why it leans as it does and by how much: what its head thinks of the mover or the target, whose client or debtor it is, the bench it follows and that bench's platform, what the motion does to it (it holds the capital and the levy fills its treasury; it cannot afford one; it holds the office now), a favour owed, blood, the chair's word, a whip, a bought vote, and its own temper on the day. The three heaviest are named and the rest summed. These are not a description of the rule: the reckoning itself now writes each cause down as it adds it, so the reasons always sum to the lean. **The margin before a whip is paid for.** The list opens with how the motion stands and by how much, what a house changing sides is worth, and what a whip (2) and a bought vote (3) are worth to a lean. Each whip and bribe button says where that house's lean would go and whether its vote would change: "leans -2 and would lean 0: it goes from against to abstaining, and its 16 seats with it", or "this one would change nothing in the count". **How a motion would fare before it is moved.** Under each kind in Move a motion, once the house, region, office or law it needs is chosen: "If it were moved now it would fail by 6: for 42, against 48." It is the docket's own count, exact at the moment of moving. (What a motion would do was already under each kind.) **Where the count for consul stands.** Under the candidates: who would be elected as the houses would vote today, how each house chooses, and where your house is (clear by how many, or how many more would put it among the elected, and what adds them). **Favours where motions are moved.** Move a motion lists who owes you a favour and whom you owe, what each is worth at a vote and when it is forgotten. (Calling one in for something else was already in the House window.) **Crises explained.** Under each active crisis, a fold reads it out of its own rules: what brings it on, with each figure and where it stands now; who set it going, or that nobody did; what it did when it began; what it does each year (nothing more of itself, but while any crisis is active unrest and legitimacy do not drift back toward rest, and the mark it leaves may be one of the things a worse crisis waits on, named); what severity is; how it ends of itself and whether it is winding down today; and for each answer what it takes off, what it costs, what else it does, and why it is closed if it is. The old tooltip said severity "rises each year it is left, and what it costs rises with it". That was not what the rules do, and it is gone. A greyed answer now says whether it is the cost or the conditions. Still to do in this part: a pledge of votes and a nomination inside the bench, crossing benches in one step, asking a patron or bench leader, an overlord's wishes, an answer to a demand other than yielding or war, a consul's yearly business, and titles by a strength test. |

## The fifteen to fix first

These are not the easiest. They are the ones that lose a player's work, break the game's balance, or hide what happened.

1. **A rival's petition takes one of your worlds without asking.** A landless rival petitions "the house with most
   worlds" for a prefecture, that house can be the player, and the world simply goes: the builder lost its colony and the
   soldier its home world, each with no report (B69, S32). **(confirmed: `PetitionFiefCommand`)**
2. **Saving loses work.** Save in the middle of a year shows "Saved ✓" and stores the start of the year; Quit has no
   confirmation and drops the year; starting any new game overwrites the one save slot without a word (N62, N63, N79).
   **(confirmed)**
3. **The title ladder is climbed by clicking.** "Seize it by force" raised a house with no ships two rungs on turn 1 with
   no price shown; "Petition the Senate" is never voted and pays for itself in legitimacy, so a house with no world reached
   First Citizen by year 20, and another went from Citizen to Legate in one year (N24, P39, P83, P94, S58). **(confirmed for Seize)**
4. **A venture sells for nine times what it cost, the year it is finished,** and can be founded again at once (F44, B29).
5. **Three places name three heirs, and the one on show is wrong.** The Dynasty header said one child for 28 years; the
   will and the Ambitions tab said others; only the abdication tooltip told the truth. After a succession the heir was the
   mother who had just stepped down (F6, F67, F80, F93, F94, N34, P19).
6. **The money forecast leaves out what a player spends.** Building instalments, hastening, household wages, tutors and
   allowances are in neither the top bar nor the ledger: two players went insolvent with a positive forecast, while a
   personal purse of 81 and 142 credits sat unseen on another screen for fifteen years (B12, B13, B51, S63, B58, N58, P38).
7. **One click starts a war.** "Ask them to step down", "Declare war" and "marry by treaty" act at once with no
   confirmation and no word on what will happen; two wars began by accident (B53, B66, S17, F10, F14).
8. **Report choices are blind.** No cost or effect before choosing, coin taken that was not listed, "requirements not
   met" with no requirement, and no outcome afterwards. Every one of the five players put this first or second
   (B9, S1, P2, P3, F1, F2, F3, F4, N18, N19, N20).
9. **Peace with a house ignores the war.** "Sue for peace" is instant and shows no terms; at 5 to 0, 6 to 2 and 5 to 0 it
   paid a credit or nothing, and a world could not be asked for (S28, S49, S71, F66, B54).
10. **A battle is one line in a log.** No place, no enemy, no numbers in the report; no way to see the odds before
    sailing; a squadron was lost to one hull with nothing to say why (S38, S39, S66).
11. **A fleet's order cannot be taken back, and looking at a world can give one.** With a fleet selected (which nothing
    shows), the next star clicked becomes its destination; one squadron left on a six-year voyage that could not be
    recalled (S11, S62, N65, B33).
12. **The courts fine the player every year in silence.** Suits against the house are found only in the Chronicle; there is
    no notice, no sum, and nothing to do about it (P49, P50, P101, F49, N76, S23).
13. **A five-credit bribe to a bench leader turns the whole bench,** every motion a rival moved carried, and a count
    forecast at 94 to 62 came in at 138 to 0 (P47, P71, P98).
14. **Family news does not arrive.** Births, the death of the steward, two declarations of war, tutors leaving, an attempt
    on the head's life: all in the Chronicle and nowhere else (F33, F48, F64, F79, N50, N76, P56, P63).
15. **Alerts open the wrong place.** Nearly every alert leads to a window's first tab rather than its subject, or to
    nothing (B16, B67, F35, F40, F55, N54, N73, N74, P33, P93, S24, S48, P91).

---

## Easy

### Reports

- Print every cost under each choice, pool costs included; take nothing that is not printed (B9, S1, P2, F2, F3, N18, N67).
- A greyed choice names what it lacks, not "requirements not met" (B9, S1, P3, P86, F1, N20, N52, S45).
- Grey or warn a choice that would put the treasury below nought (N59, P51).
- A report names its subject: who sued and for how much, which motion, which office, which good, who died, which house
  sent the knife, which world, which Act (S23, S77, P25, P48, P96, P102, F30, F54, F56, F86, B24, S44, N53).
- An answered report, reopened, shows the answer that was given (N30).
- The same card should not come back in a few years with the same words (F92, B70); a household request that says "1 more
  year" for sixteen years should lapse (P103).
- An offer with a price that is set aside should say it lapses at the year's end (B17).
- The first word of a card loses its space: "Amarket", "Athin", "Ago-between" (S3).

### Alerts and getting to things

- Every alert opens its own subject: the finished project (named), the stalled site (named, with what was short), the ask
  from a relative, the war bonds, the consuls, the physician, the raiders, the besieged world, the venture offered, the
  triumph motion, the demand on a province (list at 15 above).
- An alert goes when the thing is done (N54, P28).
- New alerts where there are none: a tutor about to leave unpaid (F48, N51); a suit or fine against the house (F49);
  a hearing left unheard costs legitimacy (F49); manpower at nought (F89); a holder withholding its dues (B52); a foreign
  fleet over your world (N76); illness in the family (N76); a world with no governor (B61); you qualify for a title (S57).
- A fleet's row in the outliner selects the fleet (S10, N64, B33).
- "Open on the galaxy map" opens that system (B1); "the Exchange ▸" from Ventures opens Ventures (B25).
- The council's seats in the outliner open the place they are filled, and agree with it (N40, F73).

### Words that are wrong

- "(GDD §9)" in the top bar tooltip (N16, P1, S18, F26). **(confirmed)**
- Internal names: "needs a shipyard_world", "+1 rare_earths", "needs title rank 1" (B7, N38, S4, F26).
- "the office of an office" in the Commission motion (N26, P7, S15). **(confirmed: an unfilled placeholder)**
- "it leans you's way" on a province (B56, S51). **(confirmed)**
- "knight" and "baron" in the log of a Republic that has Wardens and Prefects; "SOVEREIGN" on a card where no throne
  stands (S78, S34, P73).
- "pays back in never", "needs a isotopes deposit", "Governor Tiber Quillonable", an empty office name in "(yours), : sat."
  (B27, N42, N78, P32).
- A paragraph of design notes on the succession screen ("The register — The game turns ceremonial ...") (F97).
- A dowry that went out is worded "came with the bride" (F82).
- The conviction card uses the embezzlement text for another offence (P101).

### Two things called influence

- The stock and the yearly pool are both "influence", and every price says only "2 influence". Give them two names and
  say which on every button (N17, P1, P67, P105, F78, B31, S18, S52).
- "1 influence → 4 credits" pays 2 (B14, S46).
- Put the pool cost on every button that spends one: Seize, Press a claim, Petition, Take in, Incorporate, a decree,
  giving a fleet an admiral (N39, P39, P40, P24, P68, F52, F42, F62).

### Refusals that say the wrong thing

- Say what is short, and only that: "3 materials short", not the whole price (B64, N37, S4).
- Refit: "only at a system of your own" when the fleet is at one and has no materials (S8).
- Merge: "not in the same system" for levies and office fleets that are in the same system (S22, S31).
- Admiral: "already in command, or no agents" for a man who commands nothing with six agents in hand (S9, S76).
- Campaign: "2 influence" when the reason is that no object was chosen (S19).
- Petition for a title: the tooltip repeats the description instead of "needs 2 influence" (P10, P40).
- "no influence" with influence in hand: say "needs 2 (you have 1)" (P24, P68, N44).

### Your own purse

- Show the head's purse beside the treasury (top bar or House header) and on the You window (B58, S63, N33, P13, P14, F19).
- A refusal "your purse does not hold it" should give the figure and a way to move coin (F19, N33).
- "Insolvent" and "the tanks are dry" should offer "pay from your own purse" (S63).
- Purse moves in any amount, not steps of 5 and 20 (B58, F58).

### Numbers nobody explains

- A label or tooltip for: the governor's "68" (B5); "Threat read in us 17" (F25); the fourth number in the marriage table
  (P20, F7, N43); the ORDERS column "SAC" (F16, N44); the second number under a nation's strength (N45); the three bars
  under the hemicycle (N27); "8 hull(s) without strain" (S7); "HOW NEAR 89" (F36); the marks "debt to crown · parvenu"
  (S33, S59, N77); WEALTH (F98); "THE WAR OF THE REACH · Clock 0 / 40" (N22, N77); a crisis's "severity 2 · authored by"
  (N60, P104); "legitimacy" in the top bar (N48); the tier word "able" in the creator (N7).
- "Turn 1 · Gen 1" should be a year, with a word on what a generation is (N15).
- What a skill is for, wherever a skill is chosen or studied (N5, N35, P15); the unit of "+5 from study" (F68, F21).
- What a tutor costs, in what, from which purse (F23, N35).

### Buildings, worlds and provinces

- Every building line says what it gives (the Public Granary and the Fleet Supply Depot say only what they cost) (B10).
- Every line says how long it takes, "at once" included (B38).
- A standing building's line shows its level, and what the next level gives, without needing the coin first (B23, B30).
- One name for one place: "The Forum at Greywater" and "The Power Plant at The Lion's March" are the same world (B3).
- Pause, rush and abandon on the rising line in the system panel, not only in the Exchange (B4, B59).
- The House card's slot count, its "Built" list, the population cap and the tolls disagree with the system panel
  (B6, B68, N38): one source.
- The open row of a system is stale after End Turn until it is closed and opened (B22).
- A dead governor stays in the chair (sixteen years); the list offers the sitting governor and children (B61, B43).
- "Invest (6 credits → +15 prosperity)" should say it drifts back, and what prosperity is good for (B8).
- A world at its population cap should point at Habitats (B62).
- A colony's line shows the first share, not the whole price, and says the fleet may leave once it has sailed (B45, B46);
  a refusal "it lies in a nation's space" should say what would change that (N66).
- Ask to step down: say beforehand whether this house will go or fight (the game already knows) (B20, S29).
- Commute: show the holder's purse; "they cannot find the coin" on the fourth richest house reads as broken (B19).
- Withheld dues: say what ends the withholding, and when it has ended (B55).
- "A house that is your match may stop paying" should say whether this house is (B18).
- A stalled building site gets a "pay the instalment now" button (S54, F40).
- Hastening: say so the year it could not be paid; show the hastened share under "next year"; never overdraw (B49, B12).
- Rush says the sum it will take, not the premium alone (B59, F29).

### Laws

- A law's row should look like something to click (N31, P22).
- The option says "no effect of its own" above a line of real effects: print them, and "locks for 6 years", before the
  click (B63, N31, P22, P55).

### Fleets

- A way to cancel an order before the fleet has left (S11, N65).
- A refusal at the map when a click is ignored: dry tanks, a world that cannot be reached (S62, S67).
- "CANNOT SAIL" stays after helium is bought (S21).
- The system panel lists every fleet of yours wherever it is, and other houses' fleets only on the third tab; a fleet
  under way is still shown where it started (S12, S25, B47).
- "X's space" names the wrong holder; attacker and defender are the wrong way round; "falls back" and stays (S40).
- Children offered as admirals, stewards and commanders of the Reach Squadron (B35, S53, S69).
- Two squadrons with one name; no rename (S30).
- Upkeep, fuel and starting readiness on every "Lay down" button (S81, S68, S75); why readiness moved this year (S75).
- A link from Military to the shipyard (S5).
- A campaign ends when its war does (S41); every war you are in is under Wars, and other houses' sieges are not (S73).
- "Your fleets take and hold: order them to raid" is a state and a command in one label (S61).
- The Triumph and Recall motions judge "Nero Ashgrove" whichever house is chosen (S48).

### The Senate

- "You hold 1 of its seats" beside "the house has no seat": one word for a vote and another for a voice (N23, P5, N28).
- Consul: "rank 2 on any track" on one tab, "from 40, must have been praetor" on another (P11, P61).
- What each motion does, in a line (N26, P45, S56, B37).
- Say you have voted, and how (P28); what "you voted against your bench" cost (P30).
- A whip or a bribe says whether it worked (P27, P70, P72); "Read the accounts" shows its finding where it was asked (P36).
- The date of each election on its rung (P31, P41); the election alert a year early, and "in 10 years" in the election
  year (P60, P88); the election card a year late and naming no office (P78, P42, N70).
- A convicted man is elected consul; a banned man is commissioned (P60, P58).
- Collapse the sixteen motion forms when the house cannot move one (P7); "qualifies: move a Commission" when it cannot (P8).
- The census refusal should say the number wanted (P12).
- A confirm on the consul's one veto (P97).
- A censure should name its ground (P100).
- A sold vote is marked on the motion (P85, N53).

### The family

- A word for the relation on every row and profile: "your brother", "child of X and Y" (F5, F33).
- The terms of a marriage stay on its row (F12); the dowry chosen is marked (P35); the dowry paid is the dowry agreed
  (F9, F82).
- "Rival: Livia Crane" in Livia Crane's own suit (P34).
- A second proposal accepted the same year as the first leaves two spouses in the cards (S37, S55).
- A spouse taken into the household becomes the buyer, the borrower and the "old friend" in cards, and the head's own
  marriage becomes "their marriage is their own" (F53, F65, F75, F76).
- Children can be promised from seven: say so before that (F38, F77).
- What a courtship lever is worth before pulling it (F8, P21); what each succession law would do, and its cost (F24);
  what fostering is for (F71, N69); what a shrine does before paying (F57); the three choices for the children's name (F83).
- A will with more than one legatee (F58).
- A venture given to a relative still pays the house; "Sell" and "Fold" on it stay live and do nothing (F90, F51).
- Where a married relative lives and serves (F84).
- Why the pools fell to 2 after a succession (F96).

### Money

- The quantity box on the Exchange is easy to miss: three players took the lot for fixed (F27, N41, S6). Show the lot and
  the total on the button.
- "Hired crews" on the Exchange is "Manpower" on the top bar (N41, N75).
- "+24 a year" on a venture is goods at today's price, not credits (F43, B29).
- Isotopes "buy 7.92 · sell 7.96"; a note that they are "not sold on any open market" beside a Sell button (F101).
- One purchase of five reads "heavy buying by House Quillon" and moves the price 39% (N56); two notes on one screen
  disagree about the same price (S36).
- A card sells helium at 1 while the market pays 6 (F34).
- What a belt claim yields (B28, N42); what a private sale buys that the market does not (F45).
- Put capital into your own company (F59); buy more than five shares at once (F60).
- War bonds: rate, term and risk before buying (F55).
- A wage for the steward, printed (B51); a reserve of materials as well as credits (F41).
- The wealth tag "POOR" on the richest power (F15).

### The front door and housekeeping

- A sentence and a difficulty for each scenario; The Academy marked as where to start (N1).
- Tooltips for the world options, ambitions, careers, ranks, cultures, parties and venture types (N2, N4, N9, N12).
- The creator: a third nature clicked does nothing, with no word (N6); the over-budget message stays after the budget is
  met and the button stays off (N8); the family name does not follow the person's (N10); nations' worlds are listed as
  "unclaimed" (N36).
- The Academy takes a first-timer for a returning one (N81).
- The primer opens by itself in a first game and ends with three things to do (N14).
- Keys in the rail's tooltips, a key for End Turn, a page of keys (N46).
- "Continue" names the saga and the year (N61); Export says it exported (N49).
- A confirm on Quit, and a true tooltip (N63).
- The top bar lets the map show through it, and labels are drawn over its numbers (N15, B36).

### Confirm before what cannot be undone

One confirm box each, saying what it costs and what follows: declare war (S17, F14); marry by treaty (F10, P18); ask a
holder to step down, a province or a world (B53, B66); seize a title (N24); sell a venture (F44); incorporate (F42); a
succession law (F62); a law that locks (B63); the consul's veto (P97); a long voyage (S62); quit (N63); a new game over a
save (N79). A demand made of you should say that refusing is war (F91).

---

## Medium

### Telling the player what happened

- **An outcome after every report**: the paragraph and the numbers that moved (F4, N19, S2, P2).
- **The year in brief** at the start of each year: births, deaths, marriages, wars declared and ended, suits and fines,
  elections, what was finished, money in and out (N50, F33, S27, P56).
- **A death report**: who, how, what passes to whom, which posts are now empty, with the buttons to fill them (F79, B24, S44).
- **A declaration of war on you is a report**: who, why, what they want (F64, P63, F91).
- **A battle report**: where, who, the hulls and power of each side, stance against stance, what tipped it, the losses
  (S38); and a **forecast before sailing**, with hull classes where intelligence allows (S39, S66).
- **Last year's accounts** in the ledger, one-off lines included, so that the change in the treasury adds up; and a
  forecast that counts instalments, hastening, wages, tutors and allowances (B12, B13, F47, N58, P49, P38, F63).
- **Why loyalty and prosperity are what they are**, cause by cause (B5).

### Things that happen to the player without a choice

- A petition for a prefecture must not take the player's world unasked (B69, S32).
- One heir, reckoned one way, shown everywhere; the line of succession shows who really inherits and why; the head who
  has stepped down is not in it (F6, F67, F80, F93, F94, N34, P19).
- Cards that do not fit the house: a forge-world's reactor for a house without one, "you have grown beyond the reach of
  coalitions" for the poorest house, "the robe is new" in the thirteenth year, the wedding card for a couple wed nineteen
  years, the conquered world four years late (F32, F61, F76, F85, P4, P43, P73, S34, S41, S45, N21, N68).
- A siege you are pressing is taken over by an ally's fleet, your years are lost, and the ally takes the world (S70).
- The house's consul is the head's sister, when the head stood (P95).

### War and peace

- **Peace with a house** that weighs the war: what they offer, what you may ask (a world, a province, coin, dues) by the
  score, and an envoy whose terms can be read and accepted (S20, S28, S49, S71, P75, F66, B54).
- **A fleet's own panel**: move, hold, a selected state that shows, cancel and recall (S10, S11, N64, B33).
- **Detach hulls** to leave a guard; **lay up or pay off** hulls to cut the cost; **move your flag** (S50, S74, B34, S65).
- **Supply**: its range on the map, what a depot does, how a fleet far away is fed; a forecast of fuel against what the
  house brings up (S72, S42, S68).
- **How to attack a world** in a war between houses: blockade, raid or siege (S26).
- **Neutral fleets over your world**: what they are doing, and a way to ask them to go (S47, S80).
- **An office with a fleet** says what the fleet may be used for and for how long (S69).
- The map goes black when the browser loses its graphics context and does not come back (S79; seen in a headless
  browser, so it may not happen to a player).

### The Senate

- **A roll call** for every vote, with each house's reason; the margin before a whip is paid for (P26, P29, P46, P62).
- **What a motion would do, and how it would fare, before paying to move it** (P45, P55, N72).
- **Bribes** priced by seats and regard, with a chance of refusal or exposure (P47); motions that sometimes fail because
  benches follow their platforms (P71).
- **Elections**: the candidates, your chances, the count afterwards (P31, P42, N70); a pledge of your votes at the
  consular election, and a nomination inside the bench (P59, P87).
- **Benches**: cross in one step with the cost shown, lead by seats or by a contest, found one (P9, P99).
- **A consul's yearly business** (P97).
- **Ask your patron or your bench leader** to move a motion for you, or for money, a post or backing (P6, P17, P54).
- **Favours owed and owing**, house by house, that can be seen and called in (P53, P82, P85, P106).
- **Your overlord's wishes on each motion**, and an account when it moves against you (P90).
- **An answer to a demand other than yielding or war**: the praetor's court, coin, the bench (P91).
- **Crises** explained: what it is, who is behind it, what it does each year, what each answer costs (N60, N71, P65, P104).
- **Titles**: a strength test for seizing; one petition a year, or a vote; an alert when you qualify (N24, P39, P83, S57, S58).

### Family

- **Act on a person from their profile** and from the relations list: visit, gift, apologise, appoint (F17, F20, P77).
- **Settle a quarrel between two relatives** as the head, choosing which (F74, F95).
- **A relative asks your blessing to marry**: bless, forbid, set terms (F100).
- **More than yes or no** to a relative, and a way to help with what they want (F36, F88).
- **A wider field for marriage** when everyone eligible wed in the first year: lesser families, abroad, a waiting list (F37).
- **Marriage by treaty goes through the same test as courtship**, with terms, a confirm and a choice of who (F10, F11, P18).
- **Wards and children** under "Take someone in"; spouses in the household without being taken in (F52, F87, F84).
- **Children think something of you**, from how they are raised (F102).
- **The family tree** fits the window and groups children under their parents (F72).
- **Compare suitors** in the table: sort, best skill, dowry (F7).
- **Betroth or foster another house's child** from its page (F70).
- **A succession law shows the line it would give** before it is decreed (F62).

### Money

- **A venture's sale price** tied to what was paid and how long it was held (F44, B29).
- **Borrow** an amount you choose, from a lender you can see, and repay in part (F39).
- **Something to do with manpower**: it reached 458 with nothing to spend it on and no Sell (B15, N75).
- **Make an offer** on any venture of another house, from its page (F81).
- **Tell the steward what to favour**, and see what she would do next (B51, F41).

### Building and holding

- **The system panel's long row**: groups that fold, "what I can begin" first, great works behind a header (B11, N37).
- **The world's card** shows prosperity, free slots and what is rising, and builds from there; one list of what can be
  built, whichever window it is opened from (B2, B42).
- **Pull a building down** (B65). **Grant a province you hold** to another house (B21).
- **Where a colony can go**: the empty systems, how far, and a way to send the fleet (B32).
- **Claims other houses hold in your system**: what they owe, and an offer to buy them out (B50).
- **Provinces you hold under other houses** listed in the House window and under Holdings: one player did not know it held
  any for nineteen years (P89).
- **Choose whom to petition for a prefecture**, and see how far their regard is from enough (P80, P23).

### A first game

- **Settings**: text size, sound, keys (N47, N3).
- **A glossary**, and the Academy's lectures readable from any game (N48, N80).
- **Aims**: the ambition's conditions and progress, what the war clock counts, a next step (N77, S14, S59).
- **Save slots**, and a Save that saves where you are (N49, N62, N79).
- **Undo the last action of the year** (N25).
- **The creator**: a map for choosing a world, with what it yields and whose space it is in; a house for the spouse (N11, N13).

---

## Hard

- **A docket**: the suits for and against the house, with a plea, an advocate, a settlement and a counter-suit; a notice
  and a defence when you are charged (P50, P101, P79).
- **Dealings between houses**: gifts, favours asked and called in, a pledged vote, a threat, a loan at interest, with
  regard and debts that last. Today a house's page offers a client, a patron, a marriage, an accident and a war
  (P16, P106, F13, N44, P81).
- **A title ladder with rungs that must be earned**: land, seats or a carried vote above Prefect; a breakdown of what
  legitimacy is made of (P94, P83, S58).
- **Things worth a rich house's money**: endow a child, build a seat, fund a relative's campaign, lend to houses. One
  treasury reached 3,034 at 200 a year with nothing to spend it on (F99).
- **Cornering a good**: who holds what, how much there is, and a price that answers to a majority held; or a plain word
  that it cannot be done (F46).
- **Commanding a navy**: captains to appoint from the service, squadrons split and joined, supply lines, hulls laid up,
  and a year-end report of the war. The pieces are in Medium; together they are a system (S50, S65, S72, S74, S27).

---

## What worked well

Said by more than one player, unprompted:

- **A disabled button nearly always says why**, and usually exactly.
- **The Chronicle** holds everything, by year, with filters. It was where every player went to find out what had happened,
  which is also the complaint.
- **The courtship panel**: "they will refuse: the match stands at 46 with them and needs 50", changing as the terms change.
- **Your duties in the Senate, and a magistrate's business**: every choice says what it does, with numbers.
- **The system panel's build lines, the slot breakdown and the infrastructure tab.**
- **Provinces**: dues, commuting, building in a province on another house's world, the levy arriving in a war and going
  home at the peace.
- **Projects**: paid, owed, next year, pause, rush, abandon with the refund stated.
- **The venture table and the company board.**
- **Purses and the will.**
- **The career of an officer**: ranks, an epithet, a record, renown.
- **The beyond-the-Reach tab, and its graded peace.**
- **The character creator.**
- **No crash, no hang and no console error in 113 years of play; a turn takes four to six seconds.**

## Playing it again

`tools/browser/driver.js` keeps one browser open on one game and answers over HTTP: the screen as text, every control
with its tooltip, a click, an end of turn. `tools/browser/PLAYING.md` says how. A playthrough of twenty-five years takes
about an hour.
