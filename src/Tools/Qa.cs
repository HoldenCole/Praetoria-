using System.Text;
using Praetoria.Core;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Rng;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Tools;

/// <summary>
/// The QA soak (docs/PROCESS.md "QA" and "check for bugs" phases): every scenario × several seeds × many turns with a
/// randomised player, catching exceptions, checking determinism and save/load round-trips, and reporting content that
/// never surfaces (events that never fire, choices never taken), prose with unresolved tokens, and chronicle lines that
/// look wrong. Exit code 1 if any hard failure (exception, non-determinism, save/load drift, unresolved token) occurs.
/// </summary>
internal static class Qa
{
    public static int Run(Options o)
    {
        var contentRoot = ContentLocator.FindContentDir();
        var content = ContentLoader.LoadFromDirectory(contentRoot);
        var scenarios = Directory.EnumerateFiles(Path.Combine(contentRoot, "scenarios"), "*.json")
            .Select(Path.GetFileNameWithoutExtension).Select(s => s!).OrderBy(s => s, StringComparer.Ordinal).ToList();
        if (o.Scenario != "academy_crucible" && o.Scenario != "all") scenarios = new List<string> { o.Scenario };
        int turns = Math.Max(o.Turns, 60);

        var fired = new Dictionary<string, int>();
        var taken = new Dictionary<string, int>();
        var failures = new List<string>();
        var oddities = new List<string>();
        var tokens = new HashSet<string>();
        var flagsSeen = new HashSet<string>();
        var deaths = new List<string>();
        int runs = 0;

        Console.WriteLine($"=== QA — {scenarios.Count} scenario(s) × {o.Seeds} seed(s) × {turns} turns, randomised player ===\n");

        foreach (var sid in scenarios)
        {
            for (int seed = 1; seed <= o.Seeds; seed++)
            {
                runs++;
                string label = $"{sid} seed {seed}";
                try
                {
                    var (worldA, histA) = Soak(contentRoot, content, sid, (ulong)seed, turns, o, fired, taken, tokens, oddities, label);
                    foreach (var f in worldA.WorldFlags) flagsSeen.Add(f);
                    if (worldA.IsDynastyDead) deaths.Add($"{label} (turn {worldA.Turn})");

                    // Determinism: the same seed and the same player decisions must reproduce the chronicle byte for byte.
                    var (worldB, histB) = Soak(contentRoot, content, sid, (ulong)seed, turns, o, null, null, null, null, label);
                    if (histA != histB) failures.Add($"{label}: NON-DETERMINISTIC — chronicle differs on replay\n      {FirstDiff(histA, histB)}");
                    else if (Canonical(worldA) != Canonical(worldB)) failures.Add($"{label}: NON-DETERMINISTIC — world differs on replay");

                    // Save/load: stopping at the half-way point and resuming must land on the same world as running straight through.
                    var resumed = SoakWithRoundTrip(contentRoot, content, sid, (ulong)seed, turns, o);
                    string sa = Canonical(worldA), sr = Canonical(resumed);
                    if (sa != sr) failures.Add($"{label}: SAVE/LOAD DRIFT — resumed world differs from the straight run\n      {FirstDiff(sa, sr)}");
                }
                catch (Exception ex)
                {
                    failures.Add($"{label}: EXCEPTION {ex.GetType().Name}: {ex.Message}\n      {FirstFrame(ex)}");
                }
            }
        }

        // ---- Report ----
        var never = content.Events.Where(e => !fired.ContainsKey(e.Id)).Select(e => e.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var rare = fired.Where(kv => kv.Value <= Math.Max(1, runs / 10)).OrderBy(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).ToList();
        var neverTaken = content.Events.SelectMany(e => e.Choices.Select(c => $"{e.Id}/{c.Id}")).Where(k => !taken.ContainsKey(k) && fired.ContainsKey(k.Split('/')[0])).OrderBy(x => x, StringComparer.Ordinal).ToList();

        Console.WriteLine($"Runs: {runs} · dynasties ended: {deaths.Count} · events fired: {fired.Values.Sum()} ({fired.Count}/{content.Events.Count} distinct)");
        if (deaths.Count > 0) Console.WriteLine("  ended: " + string.Join(", ", deaths));

        Section("Events that never fired", never);
        Section($"Rarely fired (≤ {Math.Max(1, runs / 10)} across all runs)", rare.Select(kv => $"{kv.Key} ×{kv.Value}"));
        Section("Choices never taken (on events that did fire)", neverTaken);
        Section("Unresolved text tokens", tokens.OrderBy(t => t, StringComparer.Ordinal));
        Section("Chronicle oddities (first 40)", oddities.Distinct().Take(40));

        var engineFlags = flagsSeen.Where(f => !ContentMentions(contentRoot, f)).OrderBy(f => f, StringComparer.Ordinal).ToList();
        Section("World flags set by the engine that no content reads", engineFlags);

        Section("FAILURES", failures);
        Console.WriteLine(failures.Count == 0 && tokens.Count == 0 ? "\n=== QA PASSED ===" : $"\n=== QA FAILED — {failures.Count} failure(s), {tokens.Count} token issue(s) ===");
        return failures.Count == 0 && tokens.Count == 0 ? 0 : 1;
    }

    private static void Section(string title, IEnumerable<string> items)
    {
        var list = items.ToList();
        Console.WriteLine($"\n-- {title}: {list.Count}");
        foreach (var s in list) Console.WriteLine("   " + s);
    }

    /// <summary>A save with every string set sorted: flag sets are HashSets, whose slot order legitimately changes when a flag is
    /// removed and re-added after a reload, and nothing in the engine reads them in order.</summary>
    private static string Canonical(World world)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(SaveSystem.Serialize(world))!;
        SortStringArrays(node);
        return node.ToJsonString();
    }

    private static void SortStringArrays(System.Text.Json.Nodes.JsonNode? node)
    {
        switch (node)
        {
            case System.Text.Json.Nodes.JsonObject obj:
                // dictionaries keep insertion order, which a reload legitimately changes; nothing in the engine reads them unsorted
                var entries = obj.ToList();
                obj.Clear();
                foreach (var kv in entries.OrderBy(kv => kv.Key, StringComparer.Ordinal)) { SortStringArrays(kv.Value); obj.Add(kv.Key, kv.Value); }
                break;
            case System.Text.Json.Nodes.JsonArray arr:
                if (arr.Count > 0 && arr.All(x => x is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<string>(out _)))
                {
                    var sorted = arr.Select(x => x!.GetValue<string>()).OrderBy(x => x, StringComparer.Ordinal).ToList();
                    arr.Clear();
                    foreach (var s in sorted) arr.Add(System.Text.Json.Nodes.JsonValue.Create(s));
                }
                else foreach (var x in arr) SortStringArrays(x);
                break;
        }
    }

    /// <summary>The first point where two serialisations part ways, with a little context either side.</summary>
    private static string FirstDiff(string a, string b)
    {
        int i = 0; while (i < a.Length && i < b.Length && a[i] == b[i]) i++;
        int from = Math.Max(0, i - 120);
        string Cut(string s) => s.Substring(from, Math.Min(240, s.Length - from)).Replace("\n", " ");
        return $"straight: …{Cut(a)}…\n      resumed:  …{Cut(b)}…";
    }

    private static string FirstFrame(Exception ex)
    {
        var line = (ex.StackTrace ?? "").Split('\n').FirstOrDefault(l => l.Contains("Praetoria.Core")) ?? (ex.StackTrace ?? "").Split('\n').FirstOrDefault() ?? "";
        return line.Trim();
    }

    private static readonly Dictionary<string, string> _contentCache = new();
    private static bool ContentMentions(string root, string flag)
    {
        // Flags with an id suffix (great_crisis:war_of_the_reach:...) are matched on their generic prefix too.
        if (!_contentCache.TryGetValue(root, out var blob))
        {
            var sb = new StringBuilder();
            foreach (var f in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)) sb.Append(File.ReadAllText(f)).Append('\n');
            _contentCache[root] = blob = sb.ToString();
        }
        if (blob.Contains($"\"{flag}\"")) return true;
        int colon = flag.IndexOf(':');
        return colon > 0 && blob.Contains($"\"{flag[..colon]}:");
    }

    private static readonly IRng NoRng = new SplitMix64Rng(0);

    /// <summary>One soak run. The player picks a random affordable choice (or skips one in five) from a decision stream seeded
    /// off the world seed, so a replay repeats the same decisions.</summary>
    private static (World world, string chronicle) Soak(string contentRoot, ContentDatabase content, string sid, ulong seed, int turns, Options o,
        Dictionary<string, int>? fired, Dictionary<string, int>? taken, HashSet<string>? tokens, List<string>? oddities, string label)
    {
        var world = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(contentRoot, sid), seed);
        world.Difficulty = o.Difficulty; world.CrisisIntensity = o.Intensity; world.FamilyAutonomy = o.Autonomy;
        var tc = new TurnController(world, content);
        var decisions = new SplitMix64Rng(seed * 7919 + 17);
        int histMark = 0;
        for (int t = 0; t < turns && !world.IsDynastyDead; t++)
        {
            PlayTurn(tc, world, content, decisions, fired, taken, tokens, label);
            if (oddities != null) { ScanChronicle(world, histMark, oddities, label); histMark = world.History.Count; }
        }
        return (world, string.Join("\n", world.History.Select(h => $"{h.Turn}|{h.Text}")));
    }

    private static void PlayTurn(TurnController tc, World world, ContentDatabase content, IRng decisions,
        Dictionary<string, int>? fired, Dictionary<string, int>? taken, HashSet<string>? tokens, string label)
    {
        var briefing = tc.BeginTurn();
        foreach (var item in briefing)
        {
            if (fired != null) fired[item.Fired.Def.Id] = fired.GetValueOrDefault(item.Fired.Def.Id) + 1;
            if (tokens != null) CheckText(item.Fired, content, world, tokens);
            var offered = tc.Offer(item).Where(c => c.Available).ToList();
            int roll = decisions.NextInt(0, 5);
            if (offered.Count == 0 || roll == 0) continue;   // one in five: read it and move on
            var pick = offered[decisions.NextInt(0, offered.Count)];
            if (tc.Resolve(item, pick.Choice.Id) && taken != null)
            {
                string key = $"{item.Fired.Def.Id}/{pick.Choice.Id}";
                taken[key] = taken.GetValueOrDefault(key) + 1;
            }
        }
        tc.EndTurn();
    }

    private static void CheckText(FiredEvent fired, ContentDatabase content, World world, HashSet<string> tokens)
    {
        var text = content.Text(fired.Def.Id);
        if (text == null) { tokens.Add($"{fired.Def.Id}: no text entry"); return; }
        var ctx = new EvalContext(world, fired.Binding, NoRng, content);
        Check(fired.Def.Id, "title", TextRenderer.Substitute(text.Title ?? "", ctx), tokens);
        Check(fired.Def.Id, "body", TextRenderer.Substitute(text.Body, ctx), tokens);
        foreach (var kv in text.Choices) Check(fired.Def.Id, kv.Key, TextRenderer.Substitute(kv.Value, ctx), tokens);
    }

    private static void Check(string id, string part, string rendered, HashSet<string> tokens)
    {
        int open = rendered.IndexOf('{');
        if (open >= 0) { int close = rendered.IndexOf('}', open); tokens.Add($"{id}.{part}: {(close > open ? rendered.Substring(open, close - open + 1) : "{")}"); }
        if (rendered.Contains("  ")) tokens.Add($"{id}.{part}: double space");
        if (rendered.Contains(" of ,") || rendered.Contains(" of .") || rendered.EndsWith(" of")) tokens.Add($"{id}.{part}: empty house name");
    }

    private static void ScanChronicle(World world, int from, List<string> oddities, string label)
    {
        string? prev = null; int repeats = 0;
        for (int i = from; i < world.History.Count; i++)
        {
            string s = world.History[i].Text;
            if (s == prev) { if (++repeats == 3) oddities.Add($"{label} t{world.Turn}: repeated 4× — \"{s}\""); } else { repeats = 0; prev = s; }
            if (s.Contains("  ") || s.Contains(" .") || s.Contains(" ,")) oddities.Add($"{label} t{world.Turn}: spacing — \"{s}\"");
            if (s.Contains("{") || s.Contains("null", StringComparison.Ordinal) || s.Contains("  of ") || s.Contains(" of  ")) oddities.Add($"{label} t{world.Turn}: token/null — \"{s}\"");
            if (System.Text.RegularExpressions.Regex.IsMatch(s, @"\b(\w+) \1 \b")) oddities.Add($"{label} t{world.Turn}: doubled word — \"{s}\"");
        }
        foreach (var c in world.LivingCharacters())
        {
            if (string.IsNullOrWhiteSpace(c.Name)) oddities.Add($"{label} t{world.Turn}: nameless character {c.Id}");
            if (c.Age < 0 || c.Age > 110) oddities.Add($"{label} t{world.Turn}: age {c.Age} — {c.Name}");
            if (!world.Houses.ContainsKey(c.HouseId)) oddities.Add($"{label} t{world.Turn}: {c.Name} belongs to missing house '{c.HouseId}'");
        }
        foreach (var h in world.Houses.Values)
        {
            var t = h.Treasury;
            if (t.Credits < 0 || t.Materials < 0 || t.Manpower < 0 || t.Influence < 0 || t.Exotics < 0) oddities.Add($"{label} t{world.Turn}: negative treasury — {h.Name} c{t.Credits} m{t.Materials} mp{t.Manpower} i{t.Influence} e{t.Exotics}");
        }
        foreach (var hd in world.Holdings.Values)
        {
            if (!world.Houses.ContainsKey(hd.OwnerId)) oddities.Add($"{label} t{world.Turn}: holding {hd.Id} owned by missing house '{hd.OwnerId}'");
            if (hd.Unrest < 0 || hd.Unrest > 100) oddities.Add($"{label} t{world.Turn}: unrest {hd.Unrest} at {hd.Id}");
            if (hd.Population < 0) oddities.Add($"{label} t{world.Turn}: population {hd.Population} at {hd.Id}");
        }
        foreach (var f in world.Fleets.Values)
        {
            if (!world.Houses.ContainsKey(f.OwnerId)) oddities.Add($"{label} t{world.Turn}: fleet {f.Id} owned by missing house '{f.OwnerId}'");
            if (f.Strength <= 0) oddities.Add($"{label} t{world.Turn}: fleet {f.Id} with strength {f.Strength} still exists");
        }
        foreach (var w in world.Wars)
            if (!world.Houses.ContainsKey(w.HouseA) || !world.Houses.ContainsKey(w.HouseB)) oddities.Add($"{label} t{world.Turn}: war between missing houses {w.HouseA}/{w.HouseB}");
        var p = world.Protagonist;
        if (p != null && !p.Alive && !world.IsDynastyDead) oddities.Add($"{label} t{world.Turn}: protagonist {p.Name} is dead but the dynasty lives");
    }

    private static World SoakWithRoundTrip(string contentRoot, ContentDatabase content, string sid, ulong seed, int turns, Options o)
    {
        var world = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(contentRoot, sid), seed);
        world.Difficulty = o.Difficulty; world.CrisisIntensity = o.Intensity; world.FamilyAutonomy = o.Autonomy;
        var tc = new TurnController(world, content);
        var decisions = new SplitMix64Rng(seed * 7919 + 17);
        int half = turns / 2;
        int t = 0;
        for (; t < half && !world.IsDynastyDead; t++) PlayTurn(tc, world, content, decisions, null, null, null, "");
        tc.SyncRng();
        var reloaded = SaveSystem.Deserialize(SaveSystem.Serialize(world));
        var tc2 = new TurnController(reloaded, content);
        for (; t < turns && !reloaded.IsDynastyDead; t++) PlayTurn(tc2, reloaded, content, decisions, null, null, null, "");
        return reloaded;
    }
}
