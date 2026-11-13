using System.Diagnostics;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Diagnostics;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Tools;

/// <summary>
/// Measurements with a passive player (the first available choice of every report, no orders): where a turn's time goes, whether a
/// start can found a line, and how the markets and the three orders stand after sixty years. They print numbers; they do not pass
/// or fail. docs/PROCESS.md says when to run them.
/// </summary>
internal static class Probes
{
    private static (ContentDatabase content, List<string> scenarios) Load(Options o)
    {
        var root = ContentLocator.FindContentDir(); var content = ContentLoader.LoadFromDirectory(root);
        var all = Directory.EnumerateFiles(Path.Combine(root, "scenarios"), "*.json").Select(Path.GetFileNameWithoutExtension).Select(s => s!).OrderBy(s => s, StringComparer.Ordinal).ToList();
        return (content, o.Scenario is "academy_crucible" or "all" ? all.Where(s => !ScenarioLoader.LoadFromContent(root, s).Hidden).ToList() : new List<string> { o.Scenario });
    }
    private static void PassiveTurn(TurnController tc)
    {
        foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); }
    }

    /// <summary>perf: native milliseconds a turn by system, costliest first. The browser runs the same code a few times slower.</summary>
    public static int PerfRun(Options o)
    {
        var (content, scenarios) = Load(o); int turns = Math.Max(6, Math.Min(o.Turns, 40));
        foreach (var sid in scenarios)
        {
            var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), sid), o.Seed); var tc = new TurnController(w, content);
            PassiveTurn(tc); CourtProjection.Build(tc, content); tc.EndTurn();   // warm up
            Perf.Enabled = true; Perf.Reset(); double begin = 0, end = 0, proj = 0; var sw = new Stopwatch(); int n = 0;
            for (int t = 0; t < turns && !w.IsDynastyDead; t++, n++)
            {
                sw.Restart(); PassiveTurn(tc); begin += sw.Elapsed.TotalMilliseconds;
                sw.Restart(); CourtProjection.Build(tc, content); proj += sw.Elapsed.TotalMilliseconds;
                sw.Restart(); tc.EndTurn(); end += sw.Elapsed.TotalMilliseconds;
            }
            Perf.Enabled = false; n = Math.Max(1, n);
            Console.WriteLine($"== {sid}: a turn costs {(begin + end) / n:F1} ms (open the year {begin / n:F1}, resolve it {end / n:F1}); one projection {proj / n:F1} ms; {w.Houses.Count} houses");
            foreach (var (name, ms, calls) in Perf.Report().Take(12)) Console.WriteLine($"   {name,-30} {ms / n,7:F2} ms a turn");
            sw.Restart(); string save = SaveSystem.Serialize(w); Console.WriteLine($"   {"serialise the save",-30} {sw.Elapsed.TotalMilliseconds,7:F2} ms ({save.Length / 1024} KB)");
            Perf.Reset();
        }
        return 0;
    }

    /// <summary>survive: over --seeds runs of sixty years, how often the line ends, when the head first weds and first has a child.</summary>
    public static int Survive(Options o)
    {
        var (content, scenarios) = Load(o); int seeds = Math.Max(1, o.Seeds);
        foreach (var sid in scenarios)
        {
            int dead = 0; var wed = new List<int>(); var child = new List<int>();
            for (ulong seed = 1; seed <= (ulong)seeds; seed++)
            {
                var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), sid), seed); var tc = new TurnController(w, content);
                int wedTurn = -1, childTurn = -1;
                for (int t = 0; t < 60 && !w.IsDynastyDead; t++)
                {
                    PassiveTurn(tc);
                    if (w.Protagonist is { } me && !w.InAcademy)
                    {
                        if (wedTurn < 0 && ProposeMarriageCommand.IsMarried(w, me.Id)) wedTurn = w.Turn;
                        if (childTurn < 0 && w.LivingCharacters().Any(c => c.HouseId == me.HouseId && c.Age < 16)) childTurn = w.Turn;
                    }
                    tc.EndTurn();
                }
                if (w.IsDynastyDead) dead++; if (wedTurn >= 0) wed.Add(wedTurn); if (childTurn >= 0) child.Add(childTurn);
            }
            string Mid(List<int> xs) => xs.Count == 0 ? "never" : $"year {xs.OrderBy(x => x).ElementAt(xs.Count / 2)} (in {xs.Count} of {seeds})";
            Console.WriteLine($"== {sid}: the line ended in {dead} of {seeds} · the head wed by {Mid(wed)} · a child in the house by {Mid(child)}");
        }
        return 0;
    }

    /// <summary>balance: after sixty years, where prices sat against their usual level, what the Reach made and used, the Exchange,
    /// and how the houses stand in the three orders.</summary>
    public static int Balance(Options o)
    {
        var (content, scenarios) = Load(o); int seeds = Math.Max(1, Math.Min(o.Seeds, 4));
        foreach (var sid in scenarios)
            for (ulong seed = 1; seed <= (ulong)seeds; seed++)
            {
                var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), sid), seed); var tc = new TurnController(w, content);
                var sum = content.Market.Goods.ToDictionary(g => g.Id, _ => 0.0); int n = 0; double ixMin = 100, ixMax = 100; int crashes = 0;
                for (int t = 0; t < 60 && !w.IsDynastyDead; t++)
                {
                    PassiveTurn(tc);
                    if (w.Market != null && !w.InAcademy) { n++; foreach (var g in content.Market.Goods) if (w.Market.Good(g.Id) is { } gs) sum[g.Id] += gs.Price / g.Base; ixMin = Math.Min(ixMin, w.Market.Index); ixMax = Math.Max(ixMax, w.Market.Index); if (w.WorldFlags.Contains(CompanySystem.CrashFlag)) crashes++; }
                    tc.EndTurn();
                }
                var hs = w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId).ToList(); int total = Math.Max(1, hs.Sum(h => Math.Max(0, h.Wealth))); n = Math.Max(1, n);
                Console.WriteLine($"== {sid} seed {seed}, year {w.Turn}");
                Console.WriteLine("   prices against usual: " + string.Join(" · ", content.Market.Goods.Select(g => $"{g.Id} {sum[g.Id] / n:F2}")));
                if (w.Market != null) Console.WriteLine("   made / used last year: " + string.Join(" · ", content.Market.Goods.Select(g => w.Market.Good(g.Id) is { } gs ? $"{g.Id} {gs.LastProduction:F0}/{gs.LastConsumption:F0}" : g.Id)));
                Console.WriteLine($"   the Exchange: {w.Companies.Count} companies · index {ixMin:F0} to {ixMax:F0} · {crashes} crash(es) · {w.Counter(CompanySystem.SharesTradedCounter)} shares traded · {w.Counter(CompanySystem.TakeoversCounter)} takeover(s)");
                Console.WriteLine($"   wealth: mean credits {hs.Average(h => h.Treasury.Credits):F0} · the richest house holds {100 * hs.Max(h => Math.Max(0, h.Wealth)) / total}% of all wealth");
                string O(string o2) => $"{OrderSystem.Name(o2)} great {hs.Count(h => OrderSystem.Tier(h, o2) == 2)} dominant {hs.Count(h => OrderSystem.Tier(h, o2) == 3)}";
                Console.WriteLine($"   the orders: {O(OrderSystem.Senate)} · {O(OrderSystem.Arms)} · {O(OrderSystem.Commerce)} · callings {string.Join(", ", hs.GroupBy(h => h.Calling).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{(g.Key.Length > 0 ? g.Key : "none")} {g.Count()}"))} · great in two {hs.Count(h => OrderSystem.All.Count(x => OrderSystem.Tier(h, x) >= 2) >= 2)} · clients {hs.Count(h => h.PatronId.Length > 0)} · seats by the purse {hs.Count(h => h.Enrolled)}");
            }
        return 0;
    }
}
