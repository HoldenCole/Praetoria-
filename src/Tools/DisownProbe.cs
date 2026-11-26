using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Tools;

/// <summary>
/// Struck from the line and cast out (docs/HOUSEHOLD.md, "Struck from the line, cast out, and the undutiful will"): games of thirty
/// years with a passive player (seeds 1 up to `--seeds`, four at most; the doc is measured with `--seeds 2`). It lists every time a
/// head of another house struck a relative from its line or cast one out, for what, and where those cast out went; how often one
/// struck out was passed over when a head died; and, with `DISOWN_PLAYER=1`, a second run of each game in which the player's head
/// strikes their eldest grown child out with no cause in the first year, to see what the child does and whether a will is sued over.
/// It prints; it does not pass or fail.
/// </summary>
internal static class DisownProbe
{
    private static void PassiveTurn(TurnController tc)
    {
        foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); }
        tc.EndTurn();
    }
    public static int Run(Options o)
    {
        string root = ContentLocator.FindContentDir(); string sid = o.Scenario == "academy_crucible" ? "the_old_lion" : o.Scenario;
        var content = ContentLoader.LoadFromDirectory(root); int seeds = Math.Max(1, Math.Min(o.Seeds, 4)), years = 30;
        bool player = Environment.GetEnvironmentVariable("DISOWN_PLAYER") != null;
        Console.WriteLine($"=== Struck from the line and cast out: {sid}, {seeds} games of {years} years, a passive player ===\n");
        int struck = 0, cast = 0, passedOver = 0, headChanges = 0; var causes = new SortedDictionary<string, int>(StringComparer.Ordinal); var went = new SortedDictionary<string, int>(StringComparer.Ordinal);
        for (ulong seed = 1; seed <= (ulong)seeds; seed++)
        {
            var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, sid), seed); var tc = new TurnController(w, content);
            var heads = new Dictionary<string, string>();
            for (int y = 0; y < years && !w.IsDynastyDead; y++)
            {
                PassiveTurn(tc);
                foreach (var h in w.Houses.Values.Where(h => h.Id != w.Protagonist?.HouseId && !World.Outsider(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal))
                {
                    if (h.HeadId.Length == 0) continue;
                    if (heads.TryGetValue(h.Id, out var was) && was != h.HeadId && w.Char(h.HeadId) is { } now)
                    {
                        headChanges++;
                        var older = w.LivingCharacters().Where(c => c.HouseId == h.Id && DisownSystem.IsStruck(c) && c.Age > now.Age).ToList();
                        if (older.Count > 0) { passedOver++; Console.WriteLine($"   y{w.Turn,-3} {now.Name} leads {h.Name}; passed over, struck from its line: {string.Join(", ", older.Select(c => $"{c.Name} ({c.Age})"))}"); }
                    }
                    heads[h.Id] = h.HeadId;
                }
            }
            var others = w.Disownings.Where(d => d.HouseId != w.Protagonist?.HouseId).ToList();
            Console.WriteLine($"-- seed {seed}: {others.Count(d => d.Kind == DisownSystem.Struck)} struck from a line, {others.Count(d => d.Kind == DisownSystem.CastOut)} cast out, by heads of other houses; {w.Counter(DisownSystem.NewNamesKey)} new names founded");
            foreach (var d in others)
            {
                var head = w.Char(d.HeadId); var c = w.Char(d.PersonId); string where = d.Kind == DisownSystem.CastOut ? $", went {d.WentHow} to {w.House(d.WentTo)?.Name ?? d.WentTo}" : "";
                Console.WriteLine($"   y{d.Turn,-3} {(d.Kind == DisownSystem.Struck ? "struck" : "cast out")}: {d.PersonName} ({c?.Age}, wants {c?.Ambition}) by {head?.Name} of {w.House(d.HouseId)?.Name ?? d.HouseId} ({string.Join("/", head?.NatureTraits ?? new HashSet<string>())}): they {d.Cause}{where}{(d.Stands ? "" : $"; ended year {d.UndoneTurn}, {d.Ended}")}");
                causes[d.Cause.Split(',')[0]] = causes.GetValueOrDefault(d.Cause.Split(',')[0]) + 1;
                if (d.Kind == DisownSystem.CastOut) went[d.WentHow] = went.GetValueOrDefault(d.WentHow) + 1;
            }
            struck += others.Count(d => d.Kind == DisownSystem.Struck); cast += others.Count(d => d.Kind == DisownSystem.CastOut);
            Console.WriteLine($"   the record held, at the end: {w.WorldCounters.Keys.Count(k => k.StartsWith("barred_office:", StringComparison.Ordinal))} people ever put out of office, {w.Characters.Values.Count(c => c.Flags.Contains(FeudSystem.RefusedFieldChar))} who refused the field, {w.Characters.Values.Count(c => c.Flags.Contains(FamilySystem.DefectedFlag))} who defected");
            if (player) Steered(root, sid, content, seed, years);
            Console.WriteLine();
        }
        Console.WriteLine($"in all: {struck} struck from a line and {cast} cast out by heads of other houses in {seeds * years} years of play; {headChanges} heads changed, {passedOver} of those changes passed over someone struck out");
        Console.WriteLine("by cause: " + string.Join("; ", causes.Select(kv => $"{kv.Key} {kv.Value}")));
        Console.WriteLine("cast out, went: " + string.Join(", ", went.Select(kv => $"{kv.Key} {kv.Value}")));
        return 0;
    }

    /// <summary>The same game, but in its first year the head strikes their eldest grown child out with no cause, and otherwise does nothing.</summary>
    private static void Steered(string root, string sid, ContentDatabase content, ulong seed, int years)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, sid), seed); var tc = new TurnController(w, content); var ctx = new CommandContext(w, new Praetoria.Core.Rng.SplitMix64Rng(seed));
        foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); }
        var p = w.Protagonist!; var child = w.LivingCharacters().Where(c => c.HouseId == p.HouseId && c.Age >= DynastySystem.AgeOfMajority && DisownSystem.IsChildOf(c, p)).OrderByDescending(c => c.Age).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
        if (child == null) { Console.WriteLine("   (steered) the head has no grown child to strike out"); return; }
        var cmd = new DisownCommand(p.Id, DisownCommand.Strike, child.Id); string? no = cmd.Refusal(ctx);
        if (no != null) { Console.WriteLine($"   (steered) cannot strike {child.Name}: {no}"); return; }
        string headName = p.Name; cmd.Execute(ctx); tc.EndTurn();
        Console.WriteLine($"   (steered) {headName} strikes {child.Name} ({child.Age}, {string.Join("/", child.NatureTraits)}) from the line in year {w.Turn}, with no cause");
        int defiantAt = -1, leftAt = -1, elopedAt = -1;
        for (int y = 1; y < years && !w.IsDynastyDead; y++)
        {
            PassiveTurn(tc);
            if (defiantAt < 0 && child.Flags.Contains(FamilySystem.DefiantFlag)) defiantAt = w.Turn;
            if (elopedAt < 0 && w.WorldCounters.ContainsKey(DisownSystem.WedKey(child.Id))) elopedAt = w.Turn;
            if (leftAt < 0 && child.HouseId != p.HouseId) leftAt = w.Turn;
        }
        Console.WriteLine($"   (steered) {child.Name}: {(child.Alive ? "alive" : "dead")} at the end, of {w.House(child.HouseId)?.Name}; wants {child.Ambition}; grew defiant {(defiantAt < 0 ? "never" : "in year " + defiantAt)}; married without leave {(elopedAt < 0 ? "never" : "in year " + elopedAt)}; left the house {(leftAt < 0 ? "never" : "in year " + leftAt)}; thinks {w.GetRelationship(child.Id, w.ProtagonistId)?.Disposition ?? 0} of the head now");
        Console.WriteLine($"   (steered) the seat passed {Praetoria.Core.Counted.Times(w.Successions.Count)} ({string.Join(", ", w.Successions.Select(s => $"year {s.Turn} to {s.HeirName}"))}); suits of the undutiful will: {w.Counter(DisownSystem.SuitsKey)}, won {w.Counter(DisownSystem.SuitsWonKey)}, settled {w.Counter(DisownSystem.SuitsSettledKey)}");
        foreach (var h in w.History.Where(h => h.Text.Contains("the will of") || h.Text.Contains(child.Name + " abandons") || h.Text.Contains("strikes " + child.Name))) Console.WriteLine($"   (steered)   y{h.Turn}: {h.Text}");
    }
}
