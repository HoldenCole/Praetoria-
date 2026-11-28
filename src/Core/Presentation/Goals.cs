using Praetoria.Core.Data;
using Praetoria.Core.Progression;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Presentation;

/// <summary>One step of a goal: what to do, whether it is done, where it stands in figures, and the window where it is done.</summary>
public sealed record GoalStep(string Text, bool Done, string Stands, string Target);

/// <summary>A warning that the treasury is running out (docs/GOALS.md, "Before ruin"): how fast, where the money goes, and two
/// ways out, each with the window where it is done.</summary>
public sealed record RuinWarning(int Net, int Years, string Says, IReadOnlyList<(string text, string target)> WaysOut);

/// <summary>The aim shown under the top bar (docs/GOALS.md). <see cref="Kind"/> is "start" for the start's own goal over its first
/// ten years and "ambition" once that is reached or for a start without one; <see cref="Percent"/> is how far along it is.</summary>
public sealed record Goal(string Kind, string Title, string Why, IReadOnlyList<GoalStep> Steps, int Percent, bool Reached, string Was,
    RuinWarning? Ruin)
{
    /// <summary>The first step not yet done, or none.</summary>
    public GoalStep? Next => Steps.FirstOrDefault(s => !s.Done);
    public int Done => Steps.Count(s => s.Done);
}

/// <summary>
/// The goal under the top bar (docs/GOALS.md). The review of the game found ten years of a hard start played as report cards and
/// nothing else, with no aim on screen and no warning before ruin (docs/STEAM_REVIEW.md, item 6). Each written start now has a goal
/// for its first ten years, in steps read from the world as it stands; once it is reached the player's own ambition takes its place
/// (<see cref="OwnAim"/>). It reads the world and changes nothing; nothing in the rules waits on it. RNG-free.
/// </summary>
public static class Goals
{
    /// <summary>World counters written when a saga begins: the rank and the worlds the player's house began with.</summary>
    public const string StartRankKey = "start_rank", StartWorldsKey = "start_worlds";
    /// <summary>The years a start's goal is framed in, and the years of money left that bring the warning.</summary>
    public const int Years = 10, RuinYears = 3;
    /// <summary>A regard that counts as favour (the Emperor's), and one that counts as being set against you (a relative's).</summary>
    public const int FavourAt = 25, AgainstAt = -25;

    /// <summary>The player's ambition as an aim, said to the player (<see cref="AmbitionSystem.Wants"/> says it of a relative).</summary>
    public static string AimOf(string ambition) => ambition switch
    {
        "command_a_fleet" or "earn_a_command" => "Command a fleet of your own",
        "amass_a_fortune" => "Amass a fortune of your own",
        "restore_house_fortunes" => "See the house rich again",
        "master_the_senate" => "Rise in your service and be heard",
        "outshine_all_rivals" => "Be the first of the house",
        "seize_the_throne" => "Take the seat of the house",
        "win_a_great_love" => "Find a great love",
        _ => "Your own road"
    };

    public static int WorldsHeld(World w, string houseId) => w.HoldingsOf(houseId).Count(h => h.Kind == Holding.Planet);

    public static Goal? For(TurnController tc, ContentDatabase content)
    {
        var w = tc.World;
        if (w.InAcademy || w.IsDynastyDead || w.Protagonist is not { Alive: true } p || w.House(p.HouseId) is not { } house) return null;
        var ruin = Ruin(tc, content, house, p);
        var start = Start(w, content, p, house);
        if (start is { Reached: false, Was: "" }) return start with { Ruin = ruin };
        string was = start == null ? "" : start.Was.Length > 0 ? start.Was : $"{start.Title}: done.";
        if (OwnAim.For(w, p) is { } aim)
        {
            var step = new GoalStep(aim.Had ? "Done." : aim.Next.Length > 0 ? aim.Next : "Keep at it.", aim.Had, aim.Stands, aim.Target);
            return new Goal("ambition", AimOf(p.Ambition), aim.Stands, new[] { step }, aim.Progress, aim.Had, was, ruin);
        }
        return start != null ? start with { Ruin = ruin } : ruin == null ? null : RuinOnly(ruin);
    }

    /// <summary>What a sandbox shows of a goal (docs/GOALS.md, "Guided or sandbox"): no aim and no steps, only the warning before
    /// ruin while there is one.</summary>
    public static Goal? Unguided(Goal? goal) => goal?.Ruin is { } ruin ? RuinOnly(ruin) : null;
    private static Goal RuinOnly(RuinWarning ruin) => new("ruin", "The house's money", "", Array.Empty<GoalStep>(), 0, false, "", ruin);

    /// <summary>The start's own goal, by the start's id (<see cref="World.Start"/>), or none for a character of one's own and a world
    /// saved before the start was kept. <see cref="Goal.Was"/> is set when the goal is over, reached or not.</summary>
    public static Goal? Start(World w, ContentDatabase content, Character p, House house)
    {
        var steps = new List<GoalStep>(); string title, why; string was = "";
        var heir = DynastySystem.FindHeir(w, p);
        bool office = w.Offices.Any(o => o.CharacterId == p.Id) || w.Magistrates.Any(m => m.CharacterId == p.Id) || LadderSystem.Seat(w, p.Id) != null;
        string Rank(int r) => content.Careers.RankName(p.CareerTrack, r, w.Era) ?? $"a rank of {r}";
        switch (w.Start)
        {
            case "the_new_man":
            {
                title = "Win a seat in the Senate";
                why = "A new name sits in the Senate only by one of its four doors. For a house of commerce the door is the census: be notable in commerce, and pay its fee.";
                bool seated = OrderSystem.Seated(w, house); string? door = OrderSystem.Door(w, house);
                int standing = house.Order(OrderSystem.Commerce);
                steps.Add(new GoalStep("Make the house notable in commerce", seated || OrderSystem.Tier(house, OrderSystem.Commerce) >= 1,
                    $"Its standing in commerce is {standing} of the {OrderSystem.NotableAt} the census asks. Ventures, companies and paper on the Exchange raise it.", "exchange"));
                steps.Add(new GoalStep($"Put {Money.Credits(OrderSystem.CensusFee)} by for the census", seated || house.Treasury.Credits >= OrderSystem.CensusFee,
                    $"The treasury holds {Money.Credits(house.Treasury.Credits)}.", "house:ledger"));
                steps.Add(new GoalStep("Enrol at the census and take the seat", seated,
                    seated ? $"The house sits in the Senate by {door}." : "The census is under Senate, Orders.", "senate:orders"));
                break;
            }
            case "the_old_lion":
            {
                title = "See the seat pass safely";
                why = "The head is old. A house that is not ready for its next head loses worlds, friends and rank at the funeral.";
                if (w.Counter("generation") >= 1)
                {
                    // the seat has passed: the goal is over, kept or not, and the steps would now speak of the new head's heir
                    int had = w.Counter(StartWorldsKey), holds = WorldsHeld(w, house.Id);
                    was = holds >= had ? $"The seat passed with every world kept ({Counted.Worlds(holds)})." : $"The seat passed, and {Counted.Worlds(had - holds)} of {had} {(had - holds == 1 ? "was" : "were")} lost.";
                    return new Goal("start", title, why, Array.Empty<GoalStep>(), 100, holds >= had, was, null);
                }
                bool named = house.NamedHeirId.Length > 0 && w.Char(house.NamedHeirId) is { Alive: true };
                steps.Add(new GoalStep("Name your heir", named, heir == null ? "There is nobody in the line to name." : named ? $"{heir.Name} is named." : $"{heir.Name} stands first in the line, but nobody is named.", "dynasty"));
                steps.Add(new GoalStep("See your heir married", heir != null && DynastySystem.Spouse(w, heir) != null,
                    heir == null ? "There is no heir." : DynastySystem.Spouse(w, heir) is { } sp ? $"{heir.Name} is married to {sp.Name}." : $"{heir.Name} is not married.", "dynasty:marriages"));
                bool placed = heir != null && (heir.CareerRank >= 3 || w.Offices.Any(o => o.CharacterId == heir.Id) || w.Magistrates.Any(m => m.CharacterId == heir.Id));
                steps.Add(new GoalStep("Give your heir a place: a rank of 3 in a service, or an office", placed,
                    heir == null ? "There is no heir." : $"{heir.Name} holds a rank of {heir.CareerRank}.", "dynasty"));
                steps.Add(new GoalStep("Hand on the seat with every world kept", false,
                    $"The house holds {Counted.Worlds(WorldsHeld(w, house.Id))}. The step is done when the seat passes.", "house"));
                break;
            }
            case "the_circling_heir":
            {
                title = "Hold the house, and rise in the Senate";
                why = "The house has wealth and a name, and relatives who want both. Keep them with you while you climb.";
                var against = AmbitionSystem.Members(w, house).Where(c => c.Id != p.Id && c.Age >= AmbitionSystem.Adult)
                    .Select(c => (c, regard: w.GetRelationship(c.Id, p.Id)?.Disposition ?? 0)).OrderBy(x => x.regard).ThenBy(x => x.c.Id, StringComparer.Ordinal).ToList();
                var worst = against.FirstOrDefault();
                steps.Add(new GoalStep("Keep your kin with you: no grown relative set against you", against.All(x => x.regard > AgainstAt),
                    worst.c == null ? "There is no grown relative." : $"The coldest is {worst.c.Name}, at {Counted.Figure(worst.regard)} toward you.", "dynasty"));
                bool named = house.NamedHeirId.Length > 0 && w.Char(house.NamedHeirId) is { Alive: true };
                steps.Add(new GoalStep("Name your heir", named, heir == null ? "There is nobody in the line to name." : named ? $"{heir.Name} is named." : $"{heir.Name} stands first in the line, but nobody is named.", "dynasty"));
                steps.Add(new GoalStep("Hold an office of the Reach", office, office ? "You hold one." : "Stand when an office is open on the ladder.", "senate:ladder"));
                break;
            }
            case "the_fallen_house":
            {
                title = "Restore the house";
                why = "One poor world, debts, and the contempt of those who rose past you. Pay what is owed, and make the house rich again.";
                steps.Add(new GoalStep("Clear the house's debts", house.Debt <= 0, house.Debt > 0 ? $"The house owes {Money.Credits(house.Debt)}." : "The house owes nothing.", "house:ledger"));
                steps.Add(new GoalStep($"Raise the house's worth to {Money.Credits(AmbitionSystem.RestoredAt)}", house.Wealth >= AmbitionSystem.RestoredAt,
                    $"The house is worth {Money.Credits(house.Wealth)}: its treasury, stores, worlds and ventures, less what it owes.", "house:ledger"));
                int worlds = WorldsHeld(w, house.Id), want = Math.Max(2, w.Counter(StartWorldsKey) + 1);
                steps.Add(new GoalStep("Hold a second world", worlds >= want, $"The house holds {Counted.Worlds(worlds)}.", "galaxy"));
                break;
            }
            case "the_risen_officer":
            {
                title = "Earn a command and a name";
                why = "No name and no land, only talent. A command, a victory and a rank are how an officer's house is made.";
                AddService(w, p, steps, Rank, 4);
                int worlds = WorldsHeld(w, house.Id);
                steps.Add(new GoalStep("Hold a world of the house's own", worlds >= 1, $"The house holds {Counted.Worlds(worlds)}.", "galaxy"));
                break;
            }
            case "the_imperial_court":
            {
                title = "Rise at the Emperor's court";
                why = "A minor house inside a standing empire: there is a ladder to climb and an Emperor watching.";
                var emperor = ThroneSystem.Emperor(w) is { } eh ? Economy.HeadOf(w, eh) : null;
                int regard = emperor == null ? 0 : w.GetRelationship(emperor.Id, p.Id)?.Disposition ?? 0;
                steps.Add(new GoalStep("Win the Emperor's favour", emperor != null && regard >= FavourAt,
                    emperor == null ? "There is no Emperor." : $"{emperor.Name} stands at {Counted.Figure(regard)} toward you; {FavourAt} is favour.", "houses"));
                int startRank = w.Counter(StartRankKey), now = Title.RankOf(house.Title);
                string next = content.Titles.NameOf(Title.OfRank(Math.Max(1, startRank + 1)), w.Era);
                steps.Add(new GoalStep($"Raise the house to {next}", now > startRank, $"The house is {content.Titles.NameOf(house.Title, w.Era)}.", "house"));
                steps.Add(new GoalStep("Hold an office", office, office ? "You hold one." : "Offices are under Senate.", "senate"));
                break;
            }
            case "the_academy":
            case "the_institute":
            case "academy_crucible":
            {
                title = "Make your name in the service";
                why = "The Academy is behind you. A first posting is where an officer is noticed, or forgotten.";
                AddService(w, p, steps, Rank, 2);
                break;
            }
            default: return null;
        }
        bool reached = steps.All(s => s.Done);
        if (reached && was.Length == 0) was = $"{title}: done.";
        return new Goal("start", title, why, steps, steps.Count == 0 ? 0 : steps.Count(s => s.Done) * 100 / steps.Count, reached, reached ? was : "", null);
    }

    /// <summary>The steps of an officer's goal: a command, a victory and a rank.</summary>
    private static void AddService(World w, Character p, List<GoalStep> steps, Func<int, string> rank, int rankWanted)
    {
        var fleet = w.Fleets.Values.Where(f => f.AdmiralId == p.Id).OrderBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
        bool command = fleet != null || w.Service.Any(s => s.CommanderId == p.Id);
        steps.Add(new GoalStep("Command a fleet", command, fleet != null ? $"You command {fleet.Name}." : "Name yourself admiral of one of the house's fleets, or take a command by service.", "military"));
        int won = p.Life?.Deeds.Count(d => d.Kind == "victory") ?? 0;
        steps.Add(new GoalStep("Win a battle", won > 0, won > 0 ? $"You have won {Counted.Of(won, "battle")}." : "You have won no battle yet.", "military"));
        steps.Add(new GoalStep($"Rise to {rank(rankWanted)}", p.CareerRank >= rankWanted, $"You are {rank(p.CareerRank)}.", "character"));
    }

    /// <summary>The warning before ruin: given when, at what the year will add or take (the ledger's forecast less what is being
    /// built and paid out), the treasury will be empty within <see cref="RuinYears"/> years. It says how fast, names the two
    /// largest drains, and gives two ways out that fit them.</summary>
    public static RuinWarning? Ruin(TurnController tc, ContentDatabase content, House house, Character p)
    {
        var w = tc.World;
        if (tc.Economy?.Forecast(w, house.Id, tc.Fleets) is not { } fc) return null;
        var outs = HouseAccounts.Outgoings(w, content.Holdings, house, tc.Economy);
        int net = fc.Credits - HouseAccounts.Total(outs).Credits;
        // the drains: the ledger's costs this year and what goes out
        var drains = tc.Economy.Compute(w, house.Id, tc.Fleets).Lines.Where(l => l.Delta.Credits < 0).Select(l => (l.Source, -l.Delta.Credits))
            .Concat(outs.Where(o => o.cost.Credits > 0).Select(o => (o.what, o.cost.Credits))).ToList();
        // what the forecast sees first; failing that, a treasury that has fallen year on year for reasons it cannot see
        return Ruin(net, house.Treasury.Credits, drains, p.Purse) ?? Falling(TreasuryTrend.Ends(w), house.Treasury.Credits, drains, p.Purse);
    }

    /// <summary>The warning from the years behind: the treasury fell at each of the last two years' ends, and at that pace it runs
    /// out within <see cref="RuinYears"/> years. <paramref name="ends"/> are the treasury at the last years' ends, oldest first.</summary>
    public static RuinWarning? Falling(IReadOnlyList<int> ends, int have, IEnumerable<(string what, int cost)> drains, int purse)
    {
        if (ends.Count < 3) return null;
        int a = ends[^3], b = ends[^2], c = ends[^1];
        if (!(a > b && b > c)) return null;
        int pace = (a - c + 1) / 2;   // the fall a year, on average
        var warn = Ruin(-pace, have, drains, purse);
        if (warn == null) return null;
        string says = $"The treasury has fallen two years running, from {Money.Credits(a)} to {Money.Credits(b)} to {Money.Credits(c)}. "
            + (have - pace < 0 ? "At that pace it will not last the year." : $"At that pace it runs out within {Counted.Of(warn.Years + 1, "year")}.")
            + " Suits lost, answers that cost and raids are not in the yearly forecast; the Chronicle has each year's.";
        return warn with { Says = says };
    }

    /// <summary>The warning from the figures: the year's change, the treasury, what drains it, and the head's own purse.</summary>
    public static RuinWarning? Ruin(int net, int have, IEnumerable<(string what, int cost)> drains, int purse)
    {
        if (net >= 0) return null;
        int years = have <= 0 ? 0 : have / -net;   // whole years the treasury lasts at this rate
        if (years >= RuinYears) return null;
        var top = drains.GroupBy(d => d.what).Select(g => (what: g.Key, cost: g.Sum(d => d.cost))).Where(d => d.cost > 0)
            .OrderByDescending(d => d.cost).ThenBy(d => d.what, StringComparer.Ordinal).Take(2).ToList();
        string when = have + net < 0 ? "The treasury will not last the year" : $"At this rate the treasury runs out within {Counted.Of(years + 1, "year")}";
        string says = $"{when}: the house pays out {Money.Credits(-net)} a year more than it takes in."
            + (top.Count > 0 ? " Most goes on " + string.Join(" and ", top.Select(d => $"{d.what} ({Money.Credits(d.cost)})")) + "." : "");
        var ways = new List<(string, string)>();
        foreach (var (what, _) in top)
            if (WayOut(what) is { } way && !ways.Contains(way)) ways.Add(way);
        if (purse > 0) ways.Add(($"Put your own purse into the treasury ({Money.Credits(purse)})", "house:purse"));
        ways.Add(("Sell what the house holds in store on the Exchange", "exchange"));
        ways.Add(("Borrow, under House, Purses and the will", "house:purse"));
        return new RuinWarning(net, years, says, ways.Take(2).ToList());
    }

    /// <summary>A way out that fits a drain, by the words the ledger and the outgoings use for it.</summary>
    public static (string text, string target)? WayOut(string drain)
    {
        string d = drain.ToLowerInvariant();
        if (d.Contains("household") || d.Contains("wage") || d.Contains("stipend")) return ("Let a retainer go, under House, Court", "house:court");
        if (d.Contains("allowance")) return ("Cut the family's allowances, under House, Purses and the will", "house:purse");
        if (d.Contains("tutor")) return ("End a tutor's lessons, under Dynasty, Upbringing", "dynasty:upbringing");
        if (d.StartsWith("building") || d.StartsWith("hastening") || d.Contains("gang")) return ("Pause what is being built, under Worlds", "planets");
        if (d.Contains("fleet") || d.Contains("hull") || d.Contains("fuel") || d.Contains("crew")) return ("Lay hulls up, under Military", "military");
        if (d.Contains("interest") || d.Contains("loan") || d.Contains("bond")) return ("Pay down what is owed, under House, Purses and the will", "house:purse");
        return null;
    }
}
