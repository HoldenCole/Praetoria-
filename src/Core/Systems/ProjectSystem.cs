using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>What starting something would cost at once, and each year after.</summary>
public sealed record Instalments(Resources First, Resources Yearly, int Years);

/// <summary>
/// Projects, not clicks (docs/ECONOMY_MARKET.md stage B). A building, a new venture or a venture's next level with a build time in its
/// definition is begun, not bought: the first yearly instalment is paid on the day, the rest a year at a time, and the thing exists when
/// the last year is up. A year the instalment cannot be met the project stalls. It may be paused, rushed (the rest of the cost and a
/// quarter more, done next year) or abandoned (half of what was paid comes back). RNG-free; rival stewards use the same commands.
/// </summary>
public static class ProjectSystem
{
    public const int RushPremiumPct = 25, AbandonRefundPct = 50;
    public const string StalledFlag = "project_stalled", DoneFlag = "project_done";

    /// <summary>The cost split over the years: an even share each, rounded up, the first paid at once.</summary>
    public static Instalments Split(Resources cost, int years)
    {
        years = Math.Max(1, years); var each = new Resources();
        foreach (var r in Resource.All) if (cost.Get(r) > 0) each.Set(r, (cost.Get(r) + years - 1) / years);
        return new Instalments(each, each, years);
    }

    /// <summary>What is still owed on a project.</summary>
    public static Resources Remaining(Project p) { var rem = new Resources(); foreach (var r in Resource.All) rem.Set(r, Math.Max(0, p.Cost.Get(r) - p.Paid.Get(r))); return rem; }
    /// <summary>The next instalment: an even share of the cost, never more than what is still owed.</summary>
    public static Resources NextInstalment(Project p)
    {
        var each = Split(p.Cost, p.TurnsTotal).Yearly; var rem = Remaining(p); var next = new Resources();
        foreach (var r in Resource.All) next.Set(r, Math.Min(each.Get(r), rem.Get(r)));
        return next;
    }

    /// <summary>Why a stalled site's missed instalment cannot be paid now, or null (docs/PLAYTHROUGH.md: "a stalled building site gets a
    /// 'pay the instalment now' button").</summary>
    public static string? PayNowRefusal(World w, Project p)
    {
        if (!p.Stalled) return "it has not stalled"; if (p.Paused) return "it is paused: resume it first";
        var due = NextInstalment(p); var t = w.House(p.OwnerId)?.Treasury; if (t == null || p.CompanyId.Length > 0) return "it is a company's site: the company pays";
        var shortBy = Resource.All.Where(r => t.Get(r) < due.Get(r)).Select(r => $"{Money.Of(r, due.Get(r) - t.Get(r))} {Resource.Label(r)}").ToList();
        return shortBy.Count == 0 ? null : string.Join(" and ", shortBy) + " short";
    }
    /// <summary>Pay the instalment that was missed: the site goes on, and the year the stall cost is made up.</summary>
    public static void PayNow(World w, Project p)
    {
        var due = NextInstalment(p); if (!Pay(w, p, due)) return;
        p.Stalled = false; p.TurnsLeft = Math.Max(1, p.TurnsLeft - 1);
        w.Log($"The instalment that was missed on {p.Name} is paid ({Presentation.CourtProjection.Bundle(due)}). The work goes on: {Counted.Years(p.TurnsLeft)} to go.");
    }

    /// <summary>Whose purse pays: the company's cash for a company's project (credits only: it buys the rest at market), else the house's treasury.</summary>
    private static bool Pay(World w, Project p, Resources due)
    {
        var house = w.House(p.OwnerId); if (house == null) return false;
        if (p.CompanyId.Length > 0 && w.Companies.FirstOrDefault(c => c.Id == p.CompanyId) is { } co)
        {
            int credits = (int)Math.Ceiling(Economy.WorthOf(w, due)); if (co.Cash < credits) return false;
            co.Cash -= credits;
        }
        else
        {
            if (!house.Treasury.CanAfford(due)) return false;
            house.Treasury.Spend(due);
        }
        foreach (var r in Resource.All) p.Paid.Add(r, due.Get(r));
        return true;
    }

    /// <summary>Begin a project: the first instalment is paid now. The caller has checked everything but the purse.</summary>
    public static Project? Start(World w, string ownerId, string kind, string defId, string targetId, string name, Resources cost, int years, string companyId = "")
    {
        int n = w.Counter("projects_begun") + 1;
        var p = new Project { Id = $"proj_{n}", OwnerId = ownerId, CompanyId = companyId, Kind = kind, DefId = defId, TargetId = targetId, Name = name, TurnsTotal = Math.Max(1, years), TurnsLeft = Math.Max(1, years), Cost = cost.Clone(), Started = w.Turn };
        if (!Pay(w, p, NextInstalment(p))) return null;
        w.WorldCounters["projects_begun"] = n; w.Projects.Add(p);
        return p;
    }

    /// <summary>Can the first instalment be met?</summary>
    public static bool CanBegin(World w, string ownerId, Resources cost, int years, string companyId = "")
    {
        var first = Split(cost, years).First;
        if (companyId.Length > 0) return w.Companies.FirstOrDefault(c => c.Id == companyId) is { } co && co.Cash >= (int)Math.Ceiling(Economy.WorthOf(w, first));
        return w.House(ownerId)?.Treasury.CanAfford(first) == true;
    }

    /// <summary>A year passes on every site: the paused wait; the rest are a year nearer, pay the next instalment if one is still owed (or
    /// stall), and are finished when no year is left.</summary>
    public static void Tick(World w, HoldingCatalog holdings, BusinessCatalog ventures)
    {
        var mine = w.Protagonist?.HouseId;
        w.WorldFlags.Remove(StalledFlag); w.WorldFlags.Remove(DoneFlag);
        foreach (var p in w.Projects.OrderBy(p => p.Id, StringComparer.Ordinal).ToList())
        {
            if (w.House(p.OwnerId) == null || !StillPossible(w, p)) { w.Projects.Remove(p); continue; }
            if (p.Paused) continue;
            bool toHasten = p.TurnsLeft > 1 && (Ganged(p) || p.Hastened && SystemWorks.IsGreat(holdings, p));
            if (toHasten && !CanHasten(w, p, holdings.Rules, out string hastenShort))
            {
                /* said the year it happens: the work was to go two years in one and the treasury could not stand it (docs/PLAYTHROUGH.md, Easy, B49) */
                p.HastenMissedTurn = w.Turn;
                if (p.OwnerId == mine) w.Tell("warn", $"{char.ToUpperInvariant(p.Name[0])}{p.Name[1..]} was not {(Ganged(p) ? "worked by its gang" : "hastened")} this year ({hastenShort}): it goes at its own pace.", "exchange:projects");
            }
            if (toHasten && Hasten(w, p, holdings.Rules))
            {
                p.Stalled = false;
                if (p.TurnsLeft <= 0) { Complete(w, p, holdings, ventures); w.Projects.Remove(p); if (p.OwnerId == mine) { w.WorldFlags.Add(DoneFlag); w.WorldFlags.Add(DoneFlag + ":" + p.Kind); Finished(w, p); } }
                continue;
            }
            if (p.TurnsLeft > 1)
            {
                var due = NextInstalment(p);
                if (!due.IsEmpty() && !Pay(w, p, due)) { p.Stalled = true; if (p.OwnerId == mine) w.WorldFlags.Add(StalledFlag); continue; }
            }
            p.Stalled = false; p.TurnsLeft -= 1;
            if (p.TurnsLeft <= 0) { Complete(w, p, holdings, ventures); w.Projects.Remove(p); if (p.OwnerId == mine) { w.WorldFlags.Add(DoneFlag); w.WorldFlags.Add(DoneFlag + ":" + p.Kind); Finished(w, p); } }
        }
    }

    /// <summary>What a hastened great work pays this year (docs/BUILDINGS.md): the instalments of the two years it does in one, and
    /// <paramref name="pct"/> in the hundred more, which buys nothing but the speed.</summary>
    public static (Resources due, Resources extra) HastenDue(Project p, int pct)
    {
        var each = Split(p.Cost, p.TurnsTotal).Yearly; var rem = Remaining(p); var due = new Resources(); var extra = new Resources();
        int shares = (p.TurnsLeft > 1 ? 1 : 0) + (p.TurnsLeft - 1 > 1 ? 1 : 0);
        foreach (var r in Resource.All) { int d = Math.Min(each.Get(r) * shares, rem.Get(r)); due.Set(r, d); extra.Set(r, (d * pct + 99) / 100); }
        return (due, extra);
    }
    /// <summary>A gang is on it: only a house's own work takes one, never a company's.</summary>
    public static bool Ganged(Project p) => p.Gang && p.CompanyId.Length == 0;
    /// <summary>What a work that goes two years in one pays this year, whichever way it is sped: with a gang, the two instalments and
    /// the gang's manpower; hastened with coin, the two instalments and the share more that buys the speed.</summary>
    public static (Resources due, Resources extra) SpeedDue(Project p, BuildRules rules)
    {
        if (!Ganged(p)) return HastenDue(p, rules.HastenPct);
        var (due, _) = HastenDue(p, 0); return (due, new Resources { Manpower = rules.GangManpower });
    }
    /// <summary>Two years' work in one, if the purse can stand it; if it cannot, the work goes at its ordinary pace this year.</summary>
    private static bool CanHasten(World w, Project p, BuildRules rules, out string shortBy)
    {
        shortBy = ""; var house = w.House(p.OwnerId); if (house == null) return false;
        var (due, extra) = SpeedDue(p, rules); var all = due.Clone(); foreach (var r in Resource.All) all.Add(r, extra.Get(r));
        if (house.Treasury.CanAfford(all)) return true;
        shortBy = house.Treasury.Short(all); return false;
    }
    private static bool Hasten(World w, Project p, BuildRules rules)
    {
        var house = w.House(p.OwnerId); if (house == null) return false;
        var (due, extra) = SpeedDue(p, rules); var all = due.Clone(); foreach (var r in Resource.All) all.Add(r, extra.Get(r));
        if (!house.Treasury.CanAfford(all)) return false;
        house.Treasury.Spend(extra); if (!Pay(w, p, due)) return false;
        p.TurnsLeft -= 2;
        return true;
    }

    /// <summary>The site is gone: the holding changed hands, the venture was sold, the world's holder is now at war with the founder.</summary>
    private static bool StillPossible(World w, Project p) => p.Kind switch
    {
        Project.Building => w.Holding(p.TargetId) is { } h && h.OwnerId == p.OwnerId,
        Project.Expansion => w.Business(p.TargetId) is { } b && b.OwnerId == p.OwnerId,
        Project.Work => w.PlanetOf(SystemWorks.Parse(p.TargetId).systemId) is { } wp && wp.OwnerId == p.OwnerId,   // the system changed hands: the site is the new holder's to abandon
        Project.Colony => w.Galaxy?.System(p.TargetId) != null && w.PlanetOf(p.TargetId) == null,
        Project.Infrastructure => w.PlanetOf(p.TargetId) is { } ip && ip.OwnerId == p.OwnerId,
        Project.ProvinceWork => ProvinceSystem.Parse(p.TargetId) is var (ph, pi) && w.Holding(ph) is { } pw && pi > 0 && pi < pw.Provinces.Count && ProvinceSystem.HolderOf(pw, pi) == p.OwnerId,
        Project.Upgrade => p.TargetId.Contains('@') ? ProvinceSystem.Parse(p.TargetId) is var (uph, upi) && w.Holding(uph) is { } upw && upi > 0 && upi < upw.Provinces.Count && ProvinceSystem.HolderOf(upw, upi) == p.OwnerId && upw.Provinces[upi].Buildings.Contains(p.DefId)
            : p.TargetId.Contains('#') ? w.PlanetOf(SystemWorks.Parse(p.TargetId).systemId) is { } up && up.OwnerId == p.OwnerId && w.BodyWorks.Any(x => x.DefId == p.DefId && SystemWorks.Target(x.SystemId, x.Body) == p.TargetId)
            : w.Holding(p.TargetId) is { } uh && uh.OwnerId == p.OwnerId && uh.Buildings.Contains(p.DefId),
        _ => w.Galaxy?.System(p.TargetId) != null
    };

    /// <summary>The player's house is told that a thing it was building is finished, with the place to look at it.</summary>
    private static void Finished(World w, Project p)
    {
        string t = p.TargetId;   /* a holding, a holding@province, a system#body, a system, or a venture's id */
        string sys = w.Holding(t.Split('@')[0])?.SystemId ?? (w.Galaxy?.System(t.Split('#')[0]) != null ? t.Split('#')[0] : w.Businesses.TryGetValue(t, out var biz) ? biz.SystemId : "");
        w.Tell("good", $"{char.ToUpperInvariant(p.Name[0])}{p.Name[1..]} is finished.", w.Galaxy?.System(sys) != null ? "galaxy:" + sys : "exchange:projects");
    }
    private static void Complete(World w, Project p, HoldingCatalog holdings, BusinessCatalog ventures)
    {
        var house = w.House(p.OwnerId)!;
        switch (p.Kind)
        {
            case Project.Building:
                if (w.Holding(p.TargetId) is { } holding && holdings.Building(p.DefId) is { } building) Commands.BuildCommand.Raise(w, house, holding, building, p.OwnerId == w.Protagonist?.HouseId);
                break;
            case Project.Venture:
                if (ventures.Def(p.DefId) is { } def && w.Galaxy?.System(p.TargetId) is { } sys) { var b = Commands.FoundBusinessCommand.Open(w, house, def, sys); b.CompanyId = p.CompanyId; }
                break;
            case Project.Expansion:
                if (w.Business(p.TargetId) is { } biz && ventures.Def(biz.Kind) is { } bd && biz.Level < bd.MaxLevel) biz.Level++;
                break;
            case Project.Work:
                { var (sysId, body) = SystemWorks.Parse(p.TargetId); if (holdings.Work(p.DefId) is { } wd && w.Galaxy?.System(sysId) is { } ws && body >= 0 && body < ws.Bodies.Count) SystemWorks.Raise(w, house, sysId, body, wd); }
                break;
            case Project.Colony:
                SystemWorks.Settle(w, holdings, house, p.TargetId);
                ProvinceSystem.Seed(w, holdings);   // a colony planted in the middle of a game had no provinces until the game was next loaded
                break;
            case Project.Infrastructure:
                if (holdings.Track(p.DefId) is { } track) SystemWorks.Lay(w, holdings, p.TargetId, track);
                break;
            case Project.ProvinceWork:
                { var (ph, pi) = ProvinceSystem.Parse(p.TargetId); if (w.Holding(ph) is { } pw && holdings.Work(p.DefId) is { } pd) ProvinceSystem.Raise(w, pw, pi, pd); }
                break;
            case Project.Upgrade:
                SystemWorks.Upgrade(w, holdings, p.TargetId, p.DefId);
                break;
        }
        w.WorldCounters["projects_done"] = w.Counter("projects_done") + 1;
        if (p.TurnsTotal > 1) w.Log($"{house.Name} completes {p.Name} after {w.Turn - p.Started} year{(w.Turn - p.Started == 1 ? "" : "s")}.");
    }

    /// <summary>Is something of this kind already being built on this site?</summary>
    public static bool Underway(World w, string ownerId, string kind, string defId, string targetId) =>
        w.Projects.Any(p => p.OwnerId == ownerId && p.Kind == kind && p.TargetId == targetId && (kind == Project.Expansion || p.DefId == defId));
    /// <summary>Buildings being raised on a holding: each already holds a slot.</summary>
    public static int SlotsUnderway(World w, string holdingId) => w.Projects.Count(p => p.Kind == Project.Building && p.TargetId == holdingId);

    /// <summary>What rushing would cost in credits on top of what is still owed: a quarter of the remainder at this year's prices.</summary>
    public static int RushPremium(World w, Project p) => (int)Math.Ceiling(Economy.WorthOf(w, Remaining(p)) * RushPremiumPct / 100.0);
    /// <summary>What abandoning gives back: half of what was paid.</summary>
    public static Resources Refund(Project p) { var back = new Resources(); foreach (var r in Resource.All) back.Set(r, p.Paid.Get(r) * AbandonRefundPct / 100); return back; }
}
