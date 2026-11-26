using Praetoria.Core;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Tools;

/// <summary>
/// <c>praetoria charges</c>: a charge against the powerful when nobody steers the player's house (docs/LADDER.md, "A charge against the
/// powerful"): how many such charges were brought and against whom, how the accused answered (trial, exile, the line), how the trials
/// went, how many exiles came home, the threats made and what became of them, and the civil wars and fallen Republics beside them.
/// A passive player takes the first answer of every report and answers no charge, so a charge against the player's own house is
/// stood to trial.
/// </summary>
internal static class ChargeProbe
{
    private static readonly string[] Keys = { ChargeSystem.BroughtKey, ChargeSystem.HeardKey, ChargeSystem.ConvictedKey, ChargeSystem.AcquittedKey, ChargeSystem.ExiledKey, ChargeSystem.CrossedKey,
        ChargeSystem.LaidDownKey, ChargeSystem.HomeMovedKey, ChargeSystem.HomeKey, ChargeSystem.ThreatsKey, ChargeSystem.ThreatsKeptKey, ChargeSystem.ThreatsEmptyKey, ChargeSystem.ThreatsLapsedKey,
        "prosecutions", "convictions", "civil_wars", "republics_ended" };

    public static int Run(Options o)
    {
        var root = ContentLocator.FindContentDir(); var content = ContentLoader.LoadFromDirectory(root);
        var scenarios = o.Scenario is "academy_crucible" or "all" ? Scripted.Starts(root) : new List<string> { o.Scenario };
        int seeds = Math.Max(1, Math.Min(o.Seeds, 10)), turns = o.Turns > 6 ? o.Turns : 60;
        Console.WriteLine($"=== A charge against the powerful · {Counted.Starts(scenarios.Count)} × {Counted.Seeds(seeds)} × {turns} years · a passive player ===");
        var all = new Dictionary<string, int>(StringComparer.Ordinal); int games = 0, republics = 0, generals = 0, heads = 0, ofPlayer = 0, exilesAtEnd = 0, headsAbroad = 0, playerAbroad = 0, crossedForIt = 0;
        foreach (var sid in scenarios)
        {
            var sum = new Dictionary<string, int>(StringComparer.Ordinal); int runs = 0;
            for (ulong seed = 1; seed <= (ulong)seeds; seed++)
            {
                var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, sid), seed); var tc = new TurnController(w, content);
                if (w.IsImperium) continue;
                runs++; games++;
                var seen = new HashSet<string>();
                for (int t = 0; t < turns && !w.IsDynastyDead; t++)
                {
                    foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); }
                    tc.EndTurn();
                    foreach (var c in w.Indictments.Where(c => c.Powerful && seen.Add(c.Id)))
                    {
                        var who = w.Char(c.AccusedId); if (who == null) continue;
                        if (CaesarSystem.Sway(w, who) >= ChargeSystem.PowerfulSway) generals++; else heads++;
                        if (c.HouseId == w.Protagonist?.HouseId) ofPlayer++;
                        if (Environment.GetEnvironmentVariable("CHARGE_DEBUG") != null) Console.WriteLine($"   {sid} seed {seed} y{w.Turn}: {w.House(c.AccuserId)?.Name} charges {who.Name} of {w.House(c.HouseId)?.Name} ({ChargeSystem.PowerfulWhy(w, who)})");
                    }
                    if (w.History.Any(e => e.Turn == w.Turn && e.Text.Contains("CROSSES THE LINE") && (e.Text.Contains("charged") || e.Text.Contains("convicted") || e.Text.Contains("will be charged")))) crossedForIt++;
                }
                if (!w.IsImperium) republics++;
                foreach (var k in Keys) sum[k] = sum.GetValueOrDefault(k) + w.Counter(k);
                exilesAtEnd += w.Exiles.Count; headsAbroad += w.Exiles.Count(x => x.WasHead); if (w.Protagonist is { } p && ChargeSystem.Abroad(w, p)) playerAbroad++;
                if (Environment.GetEnvironmentVariable("CHARGE_DEBUG") != null)
                    foreach (var th in w.ChargeThreats) Console.WriteLine($"     threat y{th.Turn}: {w.House(th.AccuserId)?.Name} against {w.Char(th.AccusedId)?.Name} of {w.House(th.HouseId)?.Name}, laid down y{th.LaidDownTurn}, {(th.Outcome.Length > 0 ? th.Outcome : "standing")}");
                if (Environment.GetEnvironmentVariable("CHARGE_DEBUG") != null)
                    foreach (var e in w.History.Where(e => e.Text.Contains("exile") || e.Text.Contains("will be charged the day") || e.Text.Contains("thrown back") || e.Text.Contains("CROSSES") || e.Text.Contains("lays the command down, as the court")))
                        Console.WriteLine($"     y{e.Turn}: {(e.Text.Length > 200 ? e.Text[..200] : e.Text)}");
            }
            if (runs == 0) { Console.WriteLine($"== {sid}: opens under a throne; skipped."); continue; }
            foreach (var kv in sum) all[kv.Key] = all.GetValueOrDefault(kv.Key) + kv.Value;
            Console.WriteLine($"== {sid} ({runs} runs): " + Line(sum));
        }
        Console.WriteLine($"== all ({games} runs): " + Line(all));
        Console.WriteLine($"   charged: of great sway {generals}, heads of a Legate house or higher {heads}; of the player's house {ofPlayer} · a march whose cause was a charge or its threat {crossedForIt} · the Republic stood at the end in {republics} of {games}");
        Console.WriteLine($"   abroad at the end: {exilesAtEnd} (heads of a house {headsAbroad}; the player {playerAbroad})");
        return 0;
    }

    private static string Line(Dictionary<string, int> s)
    {
        int G(string k) => s.GetValueOrDefault(k);
        return $"charges against the powerful {G(ChargeSystem.BroughtKey)} (of {G("prosecutions")} charges in all, {G("convictions")} convicted) · answered by exile {G(ChargeSystem.ExiledKey)}, by the line {G(ChargeSystem.CrossedKey)}, by trial {G(ChargeSystem.HeardKey)} (convicted {G(ChargeSystem.ConvictedKey)}, acquitted {G(ChargeSystem.AcquittedKey)}) · commands laid down at the court's order {G(ChargeSystem.LaidDownKey)} · "
            + $"motions to call an exile home {G(ChargeSystem.HomeMovedKey)}, carried {G(ChargeSystem.HomeKey)} · threats {G(ChargeSystem.ThreatsKey)} (kept {G(ChargeSystem.ThreatsKeptKey)}, empty {G(ChargeSystem.ThreatsEmptyKey)}, lapsed {G(ChargeSystem.ThreatsLapsedKey)}) · civil wars {G("civil_wars")} · the Republic fell {G("republics_ended")}";
    }
}
