using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The first years of a first game (docs/ACADEMY.md, "The first years"). A saga begun as someone's first carries a short list of steps written
/// for its start; each ticks itself off from what the player really did: a report answered, a vote declared, a fleet ordered, something bought,
/// a building begun (each an act the command bus carried out in the player's own name, so the steward's building and the marshal's fuel never
/// tick one), or a window opened (which the client tells it). The list shows until every step is done, the player puts it away, or the third
/// year ends; a saga that opens at the Academy keeps it waiting until the cadet passes out, and a cadet who asked only to be briefed has none.
/// RNG-free, and it changes nothing the rules read.
/// </summary>
public static class FirstStepsSystem
{
    /// <summary>The acts a step can wait on.</summary>
    public const string Report = "report", Vote = "vote", Fleet = "fleet", Buy = "buy", Venture = "venture", Build = "build", Law = "law";
    /// <summary>A world picked on the map (the client marks it when a system is picked).</summary>
    public const string MapPlace = "map";

    /// <summary>Begin the first years for a saga: the start's list, kept to what the house can do. Called once, as the saga is made.</summary>
    public static FirstSteps Begin(World w, FirstYearsCatalog cat, string start)
    {
        var fs = new FirstSteps { Start = start, Academy = w.InAcademy };
        fs.Steps = cat.ListFor(start, w).Where(s => Has(w, s.Needs)).Select(s => s.Id).ToList();
        w.FirstSteps = fs;
        return fs;
    }

    /// <summary>Whether the house has what a step needs: "world" (a world of its own), "fleet", and either with "!" for the house without it.</summary>
    public static bool Has(World w, string needs)
    {
        if (needs.Length == 0) return true;
        bool not = needs.StartsWith('!'); string what = not ? needs[1..] : needs;
        var house = w.Protagonist?.HouseId ?? "";
        bool has = what switch
        {
            "world" => w.HoldingsOf(house).Any(h => h.IsPlanet),
            "fleet" => w.Fleets.Values.Any(f => f.OwnerId == house),
            _ => true
        };
        return has != not;
    }

    /// <summary>The act a command is, for the first years: answering a report, declaring a vote (moving a motion or pledging seats declares
    /// one too), an order to a fleet, buying on the Exchange (goods, shares, a bond, a venture), founding or enlarging a venture, beginning a
    /// building or a work, or changing a law. Anything else is none.</summary>
    public static string? KindOf(ICommand c) => c switch
    {
        ResolveChoiceCommand => Report,
        VoteCommand or ProposeMotionCommand or PledgeVotesCommand => Vote,
        MoveFleetCommand or SetStanceCommand or SetLoadoutCommand or DrillFleetCommand or RefitFleetCommand or LayUpFleetCommand
            or DeclareCampaignCommand or SetCommandStyleCommand or SendExpeditionCommand or RecallFleetCommand => Fleet,
        BuyGoodCommand or BuySharesCommand or BuyBondCommand or BuyVentureCommand or OfferForVentureCommand => Buy,
        FoundBusinessCommand or ExpandBusinessCommand => Venture,
        BuildCommand or BuildWorkCommand or BuildInProvinceCommand or UpgradeBuildingCommand or InvestInfrastructureCommand or ImproveProvinceCommand => Build,
        EnactLawCommand or SetLocalLawCommand or IssueEdictCommand or SetTaxPolicyCommand => Law,
        _ => null
    };

    /// <summary>A command has been carried out (the command bus calls this for every one): if it was the player's own, and the saga has its
    /// first years, the act is written down. Nothing done at the Academy counts: the list begins at the gates.</summary>
    public static void Acted(World w, ICommand command)
    {
        if (w.FirstSteps is not { } fs || w.InAcademy || command.ActorId != w.ProtagonistId) return;
        if (KindOf(command) is { } kind) fs.Did.Add(kind);
    }

    /// <summary>The player has opened a window or a tab ("character", "house:ledger"), or picked a world on the map ("map"). True when it is
    /// the first time, so the client knows a step may have ticked.</summary>
    public static bool Saw(World w, string place)
    {
        if (w.FirstSteps is not { } fs || w.InAcademy || place.Length == 0) return false;
        return fs.Seen.Add(place);
    }

    /// <summary>The player puts the list away. It does not come back by itself.</summary>
    public static void Dismiss(World w) { if (w.FirstSteps is { } fs) fs.Dismissed = true; }

    /// <summary>What the player had done and opened goes on with them when the year is loaded again to take an act back: undo takes back
    /// the act, not having learnt where the button is.</summary>
    public static void Carry(FirstSteps? from, World to)
    {
        if (from == null || to.FirstSteps is not { } fs || fs.Start != from.Start) return;
        fs.Did.UnionWith(from.Did); fs.Seen.UnionWith(from.Seen); fs.Dismissed |= from.Dismissed;
    }

    /// <summary>Whether a step is done: one of its alternatives ("did:buy|did:venture") is an act the player has done or a place they have seen.</summary>
    public static bool IsDone(FirstSteps fs, FirstStep step) => step.Done.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Any(d =>
        d == Report ? fs.Did.Contains(Report)
        : d.StartsWith("did:", StringComparison.Ordinal) ? fs.Did.Contains(d[4..])
        : d.StartsWith("seen:", StringComparison.Ordinal) && fs.Seen.Contains(d[5..]));

    /// <summary>The act a step waits on, when it waits on one ("did:vote" waits on a vote).</summary>
    public static string Act(FirstStep step) => step.Done.StartsWith("did:", StringComparison.Ordinal) ? step.Done[4..].Split('|')[0] : step.Done == Report ? Report : "";

    /// <summary>Why a step cannot be done yet, or null when it can: no vote can be declared while nothing is before the Senate, no fleet
    /// ordered by a house without one, nothing built by a house with no world.</summary>
    public static string? NotYet(World w, FirstYearsCatalog cat, FirstStep step)
    {
        string act = Act(step); var house = w.Protagonist?.HouseId ?? "";
        bool waits = act switch
        {
            Vote => !w.Motions.Any(m => m.Open),
            Fleet => !w.Fleets.Values.Any(f => f.OwnerId == house),
            Build => !w.HoldingsOf(house).Any(h => h.IsPlanet),
            _ => false
        };
        return waits && cat.NotYet.TryGetValue(act, out var says) ? says : null;
    }

    /// <summary>Whether the saga has its first years at all now: begun, past the Academy's gates, and not refused there by a cadet who asked
    /// to be briefed only.</summary>
    public static bool Active(World w) => w.FirstSteps is { } fs && !w.InAcademy && !(fs.Academy && w.Academy?.Tutorial == "brief");

    /// <summary>Whether the list stands on the screen: the first years are on, not put away, and the third year has not ended. A list whose
    /// every step is done is still returned by the projection, so the client can show the last tick before it folds away.</summary>
    public static bool Showing(World w, FirstYearsCatalog cat) => Active(w) && !w.FirstSteps!.Dismissed && w.Turn <= cat.Years;

    /// <summary>The steps of this saga's list, in order, as the content writes them.</summary>
    public static IReadOnlyList<FirstStep> StepsOf(World w, FirstYearsCatalog cat)
    {
        if (w.FirstSteps is not { } fs) return Array.Empty<FirstStep>();
        var list = cat.ListFor(fs.Start, w);
        return fs.Steps.Select(id => list.FirstOrDefault(s => s.Id == id)).Where(s => s != null).Select(s => s!).ToList();
    }

    /// <summary>Whether every step of the list is done.</summary>
    public static bool AllDone(World w, FirstYearsCatalog cat) => w.FirstSteps is { } fs && StepsOf(w, cat) is { Count: > 0 } steps && steps.All(s => IsDone(fs, s));

    /// <summary>Whether a first game shows the rail's labels by themselves (docs/WEB_CLIENT.md, "Settings"): its first years are on and the
    /// years that show them have not all been played. The player's own choice in Options comes first.</summary>
    public static bool Labels(World w, FirstYearsCatalog cat) => Active(w) && w.Turn <= cat.LabelYears;
}
