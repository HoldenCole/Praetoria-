using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Tools;

/// <summary>
/// Who issues edicts, and which (docs/EDICTS.md): games of thirty years with a passive player (the loop of <see cref="LawsProbe"/>),
/// seeds 1 up to `--seeds`, four at most (docs/EDICTS.md is measured with `--seeds 2`). It counts the edicts other houses and magistrates issued, by the position they were issued
/// from and by kind, the annulments moved and carried, and how the edicts ended. `EDICTS_YEARS=1` prints each year's positions too.
/// It prints; it does not pass or fail.
/// </summary>
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
        var content = ContentLoader.LoadFromDirectory(root); int seeds = Math.Max(1, Math.Min(o.Seeds, 4)), years = 30; bool yearly = Environment.GetEnvironmentVariable("EDICTS_YEARS") != null;
        Console.WriteLine($"=== Edicts: {sid}, {seeds} games of {years} years, a passive player ===\n");
        var byKind = new SortedDictionary<string, int>(StringComparer.Ordinal); var byDef = new SortedDictionary<string, int>(StringComparer.Ordinal); var told = 0;
        int issued = 0, others = 0, moved = 0, annulled = 0, lapsed = 0, gone = 0, carriedVotes = 0, lostVotes = 0;
        for (ulong seed = 1; seed <= (ulong)seeds; seed++)
        {
            var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, sid), seed); var tc = new TurnController(w, content);
            var seen = new HashSet<string>(); var lines = new List<string>();
            for (int y = 0; y < years && !w.IsDynastyDead; y++)
            {
                int newsBefore = w.NewsSeq;
                PassiveTurn(tc);
                told += w.News.Count(n => n.Seq > newsBefore && n.Target == "laws");
                foreach (var e in w.Edicts.Where(e => seen.Add(e.Id))) lines.Add($"   y{e.Since,-3} {e.Name,-42} {w.Char(e.IssuerId)?.Name} of {w.House(e.IssuerHouseId)?.Name}, {e.Position}{(e.Domain.Length > 0 ? "" : " (the Reach)")}");
                foreach (var m in w.Motions.Where(m => m.Kind == EdictSystem.AnnulKind && m.Resolved && seen.Add(m.Id))) { if (m.Passed) carriedVotes++; else lostVotes++; lines.Add($"   y{m.ResolvedTurn,-3} annulment moved by {w.House(m.ProposerHouseId)?.Name}: {(m.Passed ? "carried" : "lost")} {m.ForWeight} to {m.AgainstWeight}"); }
                if (yearly) Console.WriteLine($"s{seed} y{w.Turn}: chair={RegionSystem.CapitalHolder(w)?.Id} consuls={w.Offices.Count(x => x.OfficeId.StartsWith("consul"))} praetors={LadderSystem.Holders(w, "praetor").Count} aediles={LadderSystem.Holders(w, "aedile").Count} emergency={w.Emergency != null} emperor={ThroneSystem.Emperor(w)?.Id} in force={w.Edicts.Count}");
            }
            foreach (var k in EdictSystem.Issuers) byKind[k] = byKind.GetValueOrDefault(k) + w.Counter(EdictSystem.ByKey(k));
            foreach (var d in content.Edicts.Defs) byDef[d.Id] = byDef.GetValueOrDefault(d.Id) + w.Counter(EdictSystem.OfKey(d.Id));
            issued += w.Counter(EdictSystem.IssuedKey); others += w.Counter(EdictSystem.OthersKey); moved += w.Counter(EdictSystem.AnnulMovedKey); annulled += w.Counter(EdictSystem.AnnulledKey);
            lapsed += w.Counter(EdictSystem.LapsedKey); gone += w.Counter(EdictSystem.GoneKey);
            Console.WriteLine($"-- seed {seed}: {w.Counter(EdictSystem.IssuedKey)} edicts, {w.Counter(EdictSystem.OthersKey)} of them by other houses and magistrates; {w.Counter(EdictSystem.AnnulMovedKey)} annulments moved, {w.Counter(EdictSystem.AnnulledKey)} carried");
            foreach (var l in lines) Console.WriteLine(l);
            Console.WriteLine();
        }
        Console.WriteLine($"in all: {issued} edicts issued, {others} by other houses and magistrates; the player was told {told} times");
        Console.WriteLine($"ended: {lapsed} lapsed with their years, {gone} with the issuer's position, {annulled} struck down by the Senate");
        Console.WriteLine($"annulments: {moved} moved by other houses; votes carried {carriedVotes}, lost {lostVotes}");
        Console.WriteLine("by position: " + string.Join(", ", byKind.Select(kv => $"{kv.Key} {kv.Value}")));
        Console.WriteLine("by kind:     " + string.Join(", ", byDef.Select(kv => $"{kv.Key} {kv.Value}")));
        return 0;
    }
}
