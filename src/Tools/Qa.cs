using System.Text;
using Praetoria.Core;
using Praetoria.Core.Commands;
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
        Section("Commands the randomised player gave", taken.Where(kv => kv.Key.StartsWith("cmd/")).OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key[4..]} ×{kv.Value}"));
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
        Act(tc, world, content, decisions, taken);
        tc.EndTurn();
    }

    private static readonly string[] Skills = { "administration", "charisma", "discipline", "economics", "engineering", "intrigue", "law", "leadership", "logistics", "oratory" };

    /// <summary>The randomised player's action phase (GAP §4 item 3): events alone cannot reach the economy, the fleet or the court,
    /// so every turn the soak lists every command the protagonist might give — ventures, claims, loans, builds, ships, moves, wars,
    /// peace, the fief, the free hold, governors, titles, the throne — shuffles them on the decision stream, and tries up to four,
    /// stopping after two take. <see cref="CommandExecutor.TryExecute"/> guards each with CanExecute, so illegal picks cost nothing;
    /// what is recorded under <c>cmd/</c> in the "taken" table is the coverage. Candidates are built in id order so a replay and a
    /// resumed save draw the same list.</summary>
    private static void Act(TurnController tc, World w, ContentDatabase content, IRng decisions, Dictionary<string, int>? taken)
    {
        var p = w.Protagonist; if (p is not { Alive: true }) return;
        var house = w.House(p.HouseId); if (house == null) return;
        var ctx = new CommandContext(w, WorldBuilder.RngFor(w));
        var g = w.Galaxy; var bc = content.Businesses;
        var cands = new List<ICommand>();
        string Ord(string a) => a;
        var worlds = w.HoldingsOf(house.Id).Where(h => h.IsPlanet).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        var fleets = w.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.Office).OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
        var others = w.Houses.Values.Where(h => h.Id != house.Id && h.Id != PirateSystem.HouseId).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        var members = house.Members.Where(id => w.Char(id)?.Alive == true && id != p.Id).OrderBy(Ord, StringComparer.Ordinal).ToList();

        cands.Add(new TrainCommand(p.Id, Skills[decisions.NextInt(0, Skills.Length)]));
        foreach (var h in worlds)
        {
            foreach (var b in content.Holdings.Buildings.OrderBy(b => b.Id, StringComparer.Ordinal))
                if (b.FitsSpecialization(h.Specialization) && !h.Buildings.Contains(b.Id)) cands.Add(new BuildCommand(p.Id, h.Id, b.Id, content.Holdings, content.Titles));
            cands.Add(new RaiseFleetCommand(p.Id, h.Id, content.Fleets, content.ShipClasses, null, content.Titles));
            cands.Add(new InvestCommand(p.Id, h.Id, bc));
            if (members.Count > 0) cands.Add(new AppointGovernorCommand(p.Id, h.Id, members[decisions.NextInt(0, members.Count)]));
        }
        foreach (var t in content.Holdings.TaxPolicies.OrderBy(t => t.Id, StringComparer.Ordinal)) cands.Add(new SetTaxPolicyCommand(p.Id, t.Id, content.Holdings));
        if (g != null)
        {
            foreach (var f in fleets)
            {
                var nbrs = g.Neighbors(f.SystemId).OrderBy(Ord, StringComparer.Ordinal).ToList();
                if (nbrs.Count > 0) cands.Add(new MoveFleetCommand(p.Id, f.Id, nbrs[decisions.NextInt(0, nbrs.Count)], content.Fleets));
                if (g.Capital.Length > 0) cands.Add(new MoveFleetCommand(p.Id, f.Id, g.Capital, content.Fleets));
                if (members.Count > 0) cands.Add(new AssignAdmiralCommand(p.Id, f.Id, members[decisions.NextInt(0, members.Count)]));
                cands.Add(new RefitFleetCommand(p.Id, f.Id));
            }
            if (fleets.Count >= 2) cands.Add(new MergeFleetsCommand(p.Id, fleets[0].Id, fleets[1].Id));
            var own = worlds.Select(h => h.SystemId).ToHashSet();
            foreach (var def in bc.Defs.OrderBy(d => d.Id, StringComparer.Ordinal))
            {
                foreach (var sys in own.OrderBy(Ord, StringComparer.Ordinal)) cands.Add(new FoundBusinessCommand(p.Id, def.Id, sys, bc, content.Holdings, content.Titles));
                var any = g.Systems.Values.OrderBy(s => s.Id, StringComparer.Ordinal).ToList();
                cands.Add(new FoundBusinessCommand(p.Id, def.Id, any[decisions.NextInt(0, any.Count)].Id, bc, content.Holdings, content.Titles));
            }
            foreach (var sys in g.Systems.Values.Where(s => s.Belt > 0).OrderBy(s => s.Id, StringComparer.Ordinal)) cands.Add(new StakeClaimCommand(p.Id, sys.Id, content.Holdings, bc));
            foreach (var h in w.Holdings.Values.Where(h => h.FormerOwnerId == house.Id).OrderBy(h => h.Id, StringComparer.Ordinal)) cands.Add(new ReclaimFreeholdCommand(p.Id, h.Id));
        }
        foreach (var b in w.BusinessesOf(house.Id).OrderBy(b => b.Id, StringComparer.Ordinal)) { cands.Add(new ExpandBusinessCommand(p.Id, b.Id, bc)); if (decisions.NextInt(0, 6) == 0) cands.Add(new SellBusinessCommand(p.Id, b.Id, bc)); }
        cands.Add(new TakeLoanCommand(p.Id, bc)); cands.Add(new RepayLoanCommand(p.Id)); cands.Add(new PetitionFiefCommand(p.Id));
        cands.Add(new ProcureCommand(p.Id, Resource.Helium3, 2, content.Holdings, bc)); cands.Add(new ProcureCommand(p.Id, Resource.RareEarths, 1, content.Holdings, bc));
        foreach (var def in tc.Senate.Motions.Defs)
        {
            if (def.Id == "petition") continue;   // only an estate brings one
            string target = def.Target == "house" && others.Count > 0 ? others[decisions.NextInt(0, others.Count)].Id : def.Target == "region" && g != null && g.Regions.Count > 0 ? g.Regions[decisions.NextInt(0, g.Regions.Count)].Id : "";
            if (def.Target == "house" && def.TargetSelfAllowed && decisions.NextInt(0, 2) == 0) target = house.Id;
            if (def.Law)
            {
                var senateLaws = content.Policies.Laws.Where(l => l.Scope == "senate").OrderBy(l => l.Id, StringComparer.Ordinal).ToList();
                if (senateLaws.Count > 0) { var l = senateLaws[decisions.NextInt(0, senateLaws.Count)]; cands.Add(new ProposeMotionCommand(p.Id, def.Id, "", tc.Senate, lawId: l.Id, optionId: l.Options[decisions.NextInt(0, l.Options.Count)].Id)); }
                continue;
            }
            cands.Add(new ProposeMotionCommand(p.Id, def.Id, target, tc.Senate));
        }
        foreach (var f in w.Factions) cands.Add(new JoinFactionCommand(p.Id, f.Id));
        foreach (var law in content.Policies.Laws) cands.Add(new EnactLawCommand(p.Id, law.Id, law.Options[decisions.NextInt(0, law.Options.Count)].Id, content.Policies));
        cands.Add(new LeaveFactionCommand(p.Id)); cands.Add(new FoundFactionCommand(p.Id));
        foreach (var m in SenateSystem.Docket(w))
        {
            cands.Add(new VoteCommand(p.Id, m.Id, decisions.NextInt(0, 3) - 1));
            if (others.Count > 0) { var t = others[decisions.NextInt(0, others.Count)]; cands.Add(new LobbyCommand(p.Id, m.Id, t.Id, decisions.NextInt(0, 2) == 0 ? 1 : -1, bribe: decisions.NextInt(0, 3) == 0)); }
        }
        if (others.Count > 0 && decisions.NextInt(0, 4) == 0) cands.Add(new DeclareWarCommand(p.Id, others[decisions.NextInt(0, others.Count)].Id, content.Fleets));
        foreach (var war in w.Wars.Where(x => x.Involves(house.Id)).OrderBy(x => x.HouseA + x.HouseB, StringComparer.Ordinal)) cands.Add(new SueForPeaceCommand(p.Id, war.HouseA == house.Id ? war.HouseB : war.HouseA, content.Fleets));
        if (tc.Progression is { } prog) { cands.Add(new SeizeTitleCommand(p.Id, prog)); cands.Add(new PetitionTitleCommand(p.Id, prog)); cands.Add(new ClaimTitleCommand(p.Id, prog)); }
        cands.Add(new ProclaimImperiumCommand(p.Id, content.Titles)); cands.Add(new SeizeThroneCommand(p.Id, content.Titles, content.Friction));
        if (p.Age >= DynastySystem.AgeOfMajority && DynastySystem.Spouse(w, p) == null)
        {
            var match = w.LivingCharacters().Where(c => c.HouseId != p.HouseId).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
            if (match.Count > 0) cands.Add(new ProposeMarriageCommand(p.Id, p.Id, match[decisions.NextInt(0, match.Count)].Id, content.Titles));
        }

        int done = 0;
        for (int tries = 0; tries < 4 && cands.Count > 0 && done < 2; tries++)
        {
            int i = decisions.NextInt(0, cands.Count); var c = cands[i]; cands.RemoveAt(i);
            if (!tc.Executor.TryExecute(c, ctx)) continue;
            done++;
            if (taken != null) { string key = "cmd/" + c.GetType().Name.Replace("Command", ""); taken[key] = taken.GetValueOrDefault(key) + 1; }
        }
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
