using Praetoria.Core;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Tools;

/// <summary>
/// <c>praetoria service</c>: how often a command by service happens when nobody steers the player's house (docs/MILITARY.md, "A command by service"):
/// who held one, of what kind, under whom, for how long, how many battles they won, and how they ended. A passive player takes the first answer
/// of every report and gives no command.
/// </summary>
internal static class ServiceProbe
{
    public static int Run(Options o)
    {
        var root = ContentLocator.FindContentDir(); var content = ContentLoader.LoadFromDirectory(root);
        var scenarios = o.Scenario is "academy_crucible" or "all" ? Scripted.Starts(root) : new List<string> { o.Scenario };
        int seeds = Math.Max(1, Math.Min(o.Seeds, 10)), turns = o.Turns > 6 ? o.Turns : 30;
        Console.WriteLine($"=== Commands by service · {Counted.Starts(scenarios.Count)} × {Counted.Seeds(seeds)} × {turns} years · a passive player ===");
        int games = 0, total = 0, others = 0, legates = 0, lent = 0, sought = 0, won = 0, lost = 0, asks = 0, wentOver = 0, years = 0, playerHeld = 0;
        var ends = new Dictionary<string, int>(StringComparer.Ordinal); var by = new Dictionary<string, int>(StringComparer.Ordinal); var houses = new HashSet<string>();
        foreach (var sid in scenarios)
            for (ulong seed = 1; seed <= (ulong)seeds; seed++)
            {
                var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, sid), seed); var tc = new TurnController(w, content); games++;
                for (int t = 0; t < turns && !w.IsDynastyDead; t++)
                {
                    foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); }
                    asks += w.ServiceAsks.Count(a => a.Turn == w.Turn - 1);
                    // what would refuse the player's head, every ten years: the plain reasons, from each who could take them on
                    if (Environment.GetEnvironmentVariable("SERVICE_WHY") != null && w.Turn % 10 == 0 && w.Protagonist is { Alive: true } me && seed == 1)
                        foreach (var tk in ServiceSystem.TakersFor(w, content.Offices, me))
                            Console.WriteLine($"     y{w.Turn} {me.Name} (tactics {me.Skill("tactics")}, renown {LifeSystem.Renown(me)}) to {ServiceSystem.TakerName(w, content.Offices, tk)}: {ServiceSystem.SeekRefusal(w, content.Offices, me, tk, out _) ?? "would be taken"}");
                    tc.EndTurn();
                }
                var all = w.PastService.Concat(w.Service).ToList(); string? mine = w.Protagonist?.HouseId;
                total += w.Counter(ServiceSystem.CommandsKey); others += w.Counter(ServiceSystem.OthersKey); legates += w.Counter(ServiceSystem.LegatesKey); lent += w.Counter(ServiceSystem.LentKey);
                sought += w.Counter(ServiceSystem.SoughtKey); wentOver += w.Counter(ServiceSystem.WentOverKey);
                foreach (var s in all)
                {
                    won += s.Won; lost += s.Lost; years += (s.Ended > 0 ? s.Ended : w.Turn) - s.Since; houses.Add(s.CommanderHouseId); if (s.CommanderHouseId == mine) playerHeld++;
                    string key = s.Kind != ServiceSystem.Legate ? "lent to a house at war" : s.Authority switch { ServiceSystem.ByConsul => "legate, named by a consul", ServiceSystem.ByAdmiral => "legate, named by the Admiral of the Reach", _ => "legate, named by the holder of the emergency powers" };
                    by[key] = by.GetValueOrDefault(key) + 1;
                    string how = s.Ended == 0 ? "still standing at the end" : Simplify(s.How); ends[how] = ends.GetValueOrDefault(how) + 1;
                }
                Console.WriteLine($"   {sid} seed {seed}: {w.Counter(ServiceSystem.CommandsKey)} commands by service ({w.Counter(ServiceSystem.OthersKey)} by other houses' people), {w.Service.Count} standing at year {w.Turn}"
                    + (all.Count > 0 ? ": " + string.Join("; ", all.Take(6).Select(s => $"y{s.Since} {w.Char(s.CommanderId)?.Name} of {w.House(s.CommanderHouseId)?.Name ?? s.CommanderHouseId} {(s.Kind == ServiceSystem.Legate ? "legate" : "lent")} on {s.FleetName} of {w.House(s.OwnerId)?.Name ?? s.OwnerId}{(s.Won + s.Lost > 0 ? $", {s.Won} won {s.Lost} lost" : "")}{(s.Ended > 0 ? $", ended y{s.Ended}" : "")}")) : ""));
            }
        Console.WriteLine();
        Console.WriteLine($"   in {Counted.Of(games, "game")}: {total} commands by service begun, {others} of them held by other houses' people ({(games == 0 ? 0 : others * 10 / games / 10.0)} a game), by {Counted.Of(houses.Count, "house")}; {playerHeld} by the player's own");
        Console.WriteLine($"   of what kind: {string.Join(" · ", by.OrderByDescending(k => k.Value).ThenBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key} {k.Value}"))} · sought by the one who held it {sought}");
        Console.WriteLine($"   battles under them: {won} won, {lost} lost · years held in all {years} · put to the player {asks} · crews that went over with a general who crossed {wentOver}");
        Console.WriteLine($"   how they ended: {string.Join(" · ", ends.OrderByDescending(k => k.Value).ThenBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key} {k.Value}"))}");
        return 0;
    }
    private static string Simplify(string how)
    {
        if (how.StartsWith("the commission lapsed", StringComparison.Ordinal)) return "the office that named them passed";
        if (how.EndsWith("war is over, and the officer goes home", StringComparison.Ordinal)) return "the war ended";
        if (how.StartsWith("relieved by", StringComparison.Ordinal)) return "relieved";
        if (how.EndsWith(" is dead", StringComparison.Ordinal)) return "the commander died";
        if (how.EndsWith(", and the commission with them", StringComparison.Ordinal)) return "the one who named them died";
        if (how.StartsWith("the fleet has passed to", StringComparison.Ordinal)) return "the fleet passed to another house";
        if (how.EndsWith(" was given the command", StringComparison.Ordinal)) return "another was given it";
        if (how.Contains(" are at war", StringComparison.Ordinal)) return "their houses went to war";
        return how;
    }
}
