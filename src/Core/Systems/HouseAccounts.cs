using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// What a house will pay out in the coming year that its ledger of standing income and upkeep does not show (docs/PLAYTHROUGH.md 6:
/// two players went insolvent with a positive forecast, because the shares of buildings, the price of hastening, the tutors, the
/// allowances and the household's keep were in neither the top bar nor the ledger). Read only: every figure here is reckoned the way
/// the system that charges it reckons it, so the forecast and the charge agree.
/// </summary>
public static class HouseAccounts
{
    /// <summary>The year's known outgoings, one line each: what is being built (the next share, or the hastened one), tutors,
    /// allowances, and the keep and stipends of the household.</summary>
    /// <param name="economy">Given, the earnings of a venture that is a relative's own are counted as paid over to them: the ledger
    /// shows them coming in to the house, and they go out again to the one whose venture it is (docs/PLAYTHROUGH.md, Easy, F90).</param>
    public static List<(string what, Resources cost)> Outgoings(World w, HoldingCatalog cat, House house, Economy? economy = null)
    {
        var lines = new List<(string, Resources)>();
        foreach (var p in w.ProjectsOf(house.Id).OrderBy(p => p.Id, StringComparer.Ordinal))
        {
            if (p.Paused || p.TurnsLeft <= 1 || p.CompanyId.Length > 0) continue;
            Resources due;
            if (p.Hastened && SystemWorks.IsGreat(cat, p)) { var (d, extra) = ProjectSystem.HastenDue(p, cat.Rules.HastenPct); due = d.Clone(); foreach (var r in Resource.All) due.Add(r, extra.Get(r)); }
            else due = ProjectSystem.NextInstalment(p);
            if (!due.IsEmpty()) lines.Add(((p.Hastened ? "hastening " : "building ") + p.Name, due));
        }
        bool mine = house.Id == w.Protagonist?.HouseId;
        int tutors = mine ? w.LivingCharacters().Where(c => c.HouseId == house.Id && c.Tutor > 0).Sum(c => EducationSystem.TutorCost(c.Tutor)) : 0;
        if (tutors > 0) lines.Add(("tutors", new Resources { Credits = tutors }));
        var head = Economy.HeadOf(w, house); int give = WealthSystem.AllowanceOf(house.Allowance);
        int allowances = mine && head != null && give > 0 ? WealthSystem.Members(w, house).Count(c => c.Id != head.Id) * give : 0;
        if (allowances > 0) lines.Add(("allowances to the family", new Resources { Credits = allowances }));
        int keep = mine ? HouseholdSystem.Keep(w, house) : 0;
        if (keep > 0) lines.Add(("the household's keep and stipends", new Resources { Credits = keep }));
        if (economy != null)
            foreach (var b in w.BusinessesOf(house.Id).OrderBy(b => b.Id, StringComparer.Ordinal))
                if (VentureSystem.HolderOf(w, b) is { } holder && holder.Id != head?.Id && economy.VentureEarnings(w, b) is > 0 and var earns)
                    lines.Add(($"the earnings of the {b.Name}, which are {holder.Name}'s own", new Resources { Credits = earns }));
        return lines;
    }
    public static Resources Total(IEnumerable<(string what, Resources cost)> lines) { var t = new Resources(); foreach (var (_, c) in lines) foreach (var r in Resource.All) t.Add(r, c.Get(r)); return t; }
    /// <summary>What a good will stand at when the year ends if nothing else is done: what is in hand, what the ledger brings, less the outgoings.</summary>
    public static int YearEnd(World w, HoldingCatalog cat, House house, Resources ledgerNet, string resource, Economy? economy = null) => house.Treasury.Get(resource) + ledgerNet.Get(resource) - Total(Outgoings(w, cat, house, economy)).Get(resource);
}
