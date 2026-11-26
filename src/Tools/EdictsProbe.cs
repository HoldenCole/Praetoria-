using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Tools;

internal static class EdictsProbe
{
    private static void PassiveTurn(TurnController tc)
    {
        foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); }
        tc.EndTurn();
    }
    public static int Run(Options o)
    {
        string root = ContentLocator.FindContentDir(); string sid = o.Scenario == "academy_crucible" ? "the_old_lion" : o.Scenario;
        var content = ContentLoader.LoadFromDirectory(root);
        for (ulong seed = 1; seed <= 2; seed++)
        {
            var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, sid), seed); var tc = new TurnController(w, content);
            for (int y = 0; y < 30 && !w.IsDynastyDead; y++)
            {
                PassiveTurn(tc);
                var consuls = w.Offices.Where(x => x.OfficeId.StartsWith("consul")).Select(x => x.HouseId + "/" + (w.Char(x.CharacterId)?.Name ?? "?")).ToList();
                int unrest = w.Holdings.Values.Where(h => h.IsPlanet).Count(h => h.Unrest >= 40); int worlds = w.Holdings.Values.Count(h => h.IsPlanet);
                int lawless = w.Regions.Values.Count(r => r.Lawlessness >= 40);
                var un = w.Holdings.Values.Where(h => h.IsPlanet && h.Population > 0).Select(h => h.Unrest).OrderBy(x => x).ToList();
                Console.WriteLine($"   unrest max={un.LastOrDefault()} med={un[un.Count / 2]} >=10:{un.Count(x => x >= 10)} >=20:{un.Count(x => x >= 20)} >=30:{un.Count(x => x >= 30)} lawl=[{string.Join(",", w.Regions.Values.Select(r => r.Lawlessness).OrderByDescending(x => x))}] atwar={w.Houses.Values.Count(h => !World.Outsider(h.Id) && w.Wars.Any(x => x.Involves(h.Id)))} nationwar={NationSystem.AtWar(w)?.Id} danger={EmergencySystem.Danger(w)} credits=[{string.Join(",", w.Houses.Values.Where(h => !World.Outsider(h.Id)).Select(h => h.Treasury.Credits).OrderBy(x => x).Take(12))}] trust={LadderSystem.Trust(w)} prices=[{string.Join(",", MarketSystem.Of(w, content.Market).Goods.Values.Select(g => g.Id + ":" + (int)(100 * g.Price / Math.Max(0.01, content.Market.Good(g.Id)?.Base ?? 1)) + "/" + (int)(100 * g.Deficit)))}]");
                Console.WriteLine($"s{seed} y{w.Turn}: chair={RegionSystem.CapitalHolder(w)?.Id} consuls=[{string.Join(", ", consuls)}] praetors={LadderSystem.Holders(w, "praetor").Count} aediles={LadderSystem.Holders(w, "aedile").Count} emergency={(w.Emergency != null)} emperor={ThroneSystem.Emperor(w)?.Id} wars={w.Wars.Count} unrest40={unrest}/{worlds} lawless40={lawless}/{w.Regions.Count} loans={w.HouseLoans.Count} debtors={w.Houses.Values.Count(h => h.Debt > 0)} poor={w.Houses.Values.Count(h => !World.Outsider(h.Id) && h.Treasury.Credits < 5)} mpAvg={(int)w.Houses.Values.Where(h => !World.Outsider(h.Id)).Average(h => h.Treasury.Manpower)}");
            }
        }
        return 0;
    }
}
