using Praetoria.Core;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Tools;

/// <summary>
/// Do the other houses make local law (docs/POLITICS.md, "Local law")? Games of thirty years with a passive player: every year each
/// world's local laws are compared with the year before, and every change by a house that is not the player's is counted by law and
/// option. It prints; it does not pass or fail. <c>dotnet run --project src/Tools -- locallaws --seeds 2</c>
/// </summary>
internal static class LocalLawsProbe
{
    private static void PassiveTurn(TurnController tc)
    {
        foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); }
        tc.EndTurn();
    }

    public static int Run(Options o)
    {
        string root = ContentLocator.FindContentDir(); string sid = o.Scenario == "academy_crucible" ? "the_old_lion" : o.Scenario;
        var content = ContentLoader.LoadFromDirectory(root); var cat = content.Policies; int seeds = Math.Max(1, Math.Min(o.Seeds, 4)), years = 30;
        Console.WriteLine($"=== Local law made by the other houses: {sid}, {Counted.Of(seeds, "game")} of {years} years, a passive player ===\n");
        var set = new Dictionary<string, int>(); var lifted = new Dictionary<string, int>(); var diag = new Dictionary<string, int>(); int total = 0, housesIn = 0, housesThatDid = 0, worldYears = 0, worldYearsUnder = 0;
        for (ulong seed = 1; seed <= (ulong)seeds; seed++)
        {
            var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, sid), seed); var tc = new TurnController(w, content); string mine = w.Protagonist!.HouseId;
            var last = new Dictionary<(string, string), string>(); var did = new HashSet<string>(); int game = 0;
            for (int y = 0; y < years && !w.IsDynastyDead; y++)
            {
                PassiveTurn(tc);
                foreach (var h in w.Holdings.Values.Where(h => h.IsPlanet).OrderBy(h => h.Id, StringComparer.Ordinal))
                {
                    if (h.OwnerId != mine && !World.Outsider(h.OwnerId)) { worldYears++; if (LocalLawSystem.Count(cat, h) > 0) worldYearsUnder++; }
                    if (Environment.GetEnvironmentVariable("LOCAL_DIAG") != null && h.OwnerId != mine && !World.Outsider(h.OwnerId) && w.House(h.OwnerId) is { } oh)
                    {
                        void Bump(string k) => diag[k] = diag.GetValueOrDefault(k) + 1; int room = content.Holdings.Spec(h.Specialization)?.MaxPopulation ?? 0;
                        if (h.Unrest >= 45) Bump("unrest>=45"); if (h.Unrest >= 30) Bump("unrest>=30"); if (h.Unrest >= 70) Bump("unrest>=70"); if (h.InRebellion) Bump("rebellion"); if (h.FormerOwnerId.Length > 0) Bump("taken"); if (h.FormerOwnerId.Length > 0 && h.Unrest >= 40) Bump("taken&unrest>=40");
                        if (room > 0 && h.Population * 3 < room) Bump("thin"); if (room > 0 && h.Population * 2 < room) Bump("under half"); if (room > 0 && h.Population * 3 < room && oh.Treasury.Credits >= 15) Bump("thin&rich"); if (oh.Treasury.Credits >= 15) Bump("rich>=15"); if (oh.Treasury.Credits < 4) Bump("poor<4"); if (oh.Treasury.Credits >= 8) Bump("coin>=8");
                        Bump("unrest band " + (h.Unrest / 5 * 5).ToString("00")); if (room > 0) Bump("pop tenth " + Math.Min(10, h.Population * 10 / room).ToString("00")); if (h.FormerOwnerId.Length > 0 && w.House(h.FormerOwnerId) != null && h.FormerOwnerId != h.OwnerId) Bump("taken, unrest band " + (h.Unrest / 5 * 5).ToString("00")); Bump("prosperity band " + (h.Prosperity / 10 * 10).ToString("00")); Bump("treasury band " + Math.Clamp(oh.Treasury.Credits / 4 * 4, -4, 40).ToString("00"));
                        if (h.Population == 0) Bump("unpeopled"); if (h.Unrest <= 15 && h.Prosperity >= 55) Bump("quiet&rich world");
                    }
                    foreach (var law in cat.Local.Laws)
                    {
                        string now = LocalLawSystem.OptionId(cat, h, law.Id), was = last.GetValueOrDefault((h.Id, law.Id), law.DefaultOption.Id); last[(h.Id, law.Id)] = now;
                        if (now == was || h.OwnerId == mine) continue;
                        total++; game++; did.Add(h.OwnerId);
                        if (law.Option(now)!.Default) lifted[law.Id + ": " + was] = lifted.GetValueOrDefault(law.Id + ": " + was) + 1; else set[law.Id + ": " + now] = set.GetValueOrDefault(law.Id + ": " + now) + 1;
                    }
                }
            }
            int houses = w.Houses.Values.Count(h => !World.Outsider(h.Id) && h.Id != mine); housesIn += houses; housesThatDid += did.Count;
            var standing = w.Holdings.Values.Where(h => h.IsPlanet && h.OwnerId != mine && LocalLawSystem.Count(cat, h) > 0).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
            Console.WriteLine($"seed {seed}: {Counted.Of(game, "change")} by {Counted.Houses(did.Count)} of {houses}; at the end {Counted.Worlds(standing.Count)} under a local law: {string.Join("; ", standing.Take(8).Select(h => $"{h.Name} ({LocalLawSystem.Summary(cat, h)})"))}");
        }
        Console.WriteLine($"\n{Counted.Of(total, "change")} in all, by {housesThatDid} of {Counted.Houses(housesIn)}; a world of another house was under some local law in {worldYearsUnder} of {worldYears} world-years.");
        foreach (var (k, v) in diag.OrderBy(x => x.Key, StringComparer.Ordinal)) Console.WriteLine($"  diag {k}: {v}");
        Console.WriteLine("\nset:"); foreach (var (k, v) in set.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal)) Console.WriteLine($"  {v,4}  {k}");
        Console.WriteLine("lifted:"); foreach (var (k, v) in lifted.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal)) Console.WriteLine($"  {v,4}  {k}");
        return 0;
    }
}
