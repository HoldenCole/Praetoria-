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
        var caesar = new Dictionary<string, int>(); var fell = new List<string>();
        int runs = 0;

        Console.WriteLine($"=== QA — {Counted.Scenarios(scenarios.Count)} × {Counted.Seeds(o.Seeds)} × {turns} turns, randomised player ===\n");

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
                    flagsSeen.UnionWith(FlagsEveryTurn);
                    if (worldA.IsDynastyDead) deaths.Add($"{label} (turn {worldA.Turn}): {WhyItEnded(worldA)}");

                    // Determinism: the same seed and the same player decisions must reproduce the chronicle byte for byte.
                    var (worldB, histB) = Soak(contentRoot, content, sid, (ulong)seed, turns, o, null, null, null, null, label);
                    if (histA != histB) failures.Add($"{label}: NON-DETERMINISTIC — chronicle differs on replay\n      {FirstDiff(histA, histB)}");
                    else if (Canonical(worldA) != Canonical(worldB)) failures.Add($"{label}: NON-DETERMINISTIC — world differs on replay");

                    // Save/load: stopping at the half-way point and resuming must land on the same world as running straight through.
                    var resumed = SoakWithRoundTrip(contentRoot, content, sid, (ulong)seed, turns, o);
                    string sa = Canonical(worldA), sr = Canonical(resumed);
                    if (sa != sr) failures.Add($"{label}: SAVE/LOAD DRIFT — resumed world differs from the straight run\n      {FirstDiff(sa, sr)}");
                    foreach (var key in CaesarKeys) caesar[key] = caesar.GetValueOrDefault(key) + worldA.Counter(key);
                    if (worldA.Counter("republics_ended") > 0) fell.Add(label);
                }
                catch (Exception ex)
                {
                    failures.Add($"{label}: EXCEPTION {ex.GetType().Name}: {ex.Message}\n      {FirstFrame(ex)}");
                }
            }
        }

        // ---- Players with an end in view (Scripted.cs): each road once on every start a player may choose. What they fire and
        // take is counted with the random player's, so "never fired" means that none of the four reached it. ----
        var roadLines = new List<string>(); int randomEvents = fired.Count, randomCommands = taken.Keys.Count(k => k.StartsWith("cmd/", StringComparison.Ordinal));
        if (o.Scenario is "academy_crucible" or "all")
        {
            var starts = Scripted.Starts(contentRoot);
            foreach (var roadId in Scripted.RoadIds)
            {
                var reached = new Dictionary<string, int>(); int games = 0, ended = 0;
                foreach (var sid in starts)
                {
                    string label = $"{sid} seed 1, the {roadId}";
                    try
                    {
                        var a = Scripted.Play(contentRoot, content, roadId, sid, 1, turns, o, fired, taken, FlagsEveryTurn); games++; if (a.LineEnded) ended++;
                        foreach (var m in a.ReachedIn.Keys) reached[m] = reached.GetValueOrDefault(m) + 1;
                        var b = Scripted.Play(contentRoot, content, roadId, sid, 1, turns, o);
                        if (Canonical(a.World!) != Canonical(b.World!)) failures.Add($"{label}: NON-DETERMINISTIC — world differs on replay");
                    }
                    catch (Exception ex) { failures.Add($"{label}: EXCEPTION {ex.GetType().Name}: {ex.Message}\n      {FirstFrame(ex)}"); }
                }
                roadLines.Add($"  the {roadId}, {games} games (the line ended in {ended}): " + string.Join(" · ", Scripted.MilestonesOf(roadId).Select(m => $"{m} {reached.GetValueOrDefault(m)}")));
            }
            flagsSeen.UnionWith(FlagsEveryTurn);
        }

        // ---- Report ----
        var never = content.Events.Where(e => !fired.ContainsKey(e.Id)).Select(e => e.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var rare = fired.Where(kv => kv.Value <= Math.Max(1, runs / 10)).OrderBy(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).ToList();
        var neverTaken = content.Events.SelectMany(e => e.Choices.Select(c => $"{e.Id}/{c.Id}")).Where(k => !taken.ContainsKey(k) && fired.ContainsKey(k.Split('/')[0])).OrderBy(x => x, StringComparer.Ordinal).ToList();

        Console.WriteLine($"The Caesar road over {runs} runs: {string.Join(" · ", CaesarKeys.Select(k => $"{k.Replace('_', ' ')} {caesar.GetValueOrDefault(k)}"))}{(fell.Count > 0 ? " · fell in: " + string.Join(", ", fell) : "")}");
        Console.WriteLine($"Runs: {runs} · dynasties ended: {deaths.Count} · events fired: {fired.Values.Sum()} ({fired.Count}/{content.Events.Count} distinct)");
        if (deaths.Count > 0) Console.WriteLine("  ended:\n    " + string.Join("\n    ", deaths));
        if (roadLines.Count > 0)
        {
            Console.WriteLine($"Players with an end in view (how many games reached each step). The random player alone fired {randomEvents} events and landed {randomCommands} kinds of command; with these, {fired.Count} and {taken.Keys.Count(k => k.StartsWith("cmd/", StringComparison.Ordinal))}.");
            foreach (var line in roadLines) Console.WriteLine(line);
        }

        Section("Events that never fired", never);
        Section($"Rarely fired (≤ {Math.Max(1, runs / 10)} across all runs)", rare.Select(kv => $"{kv.Key} ×{kv.Value}"));
        Section("Choices never taken (on events that did fire)", neverTaken);
        Section("Commands the randomised player gave", taken.Where(kv => kv.Key.StartsWith("cmd/")).OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key[4..]} ×{kv.Value}"));
        Section("Unresolved text tokens", tokens.OrderBy(t => t, StringComparer.Ordinal));
        Section("Chronicle oddities (first 40)", oddities.Distinct().Take(40));

        var engineFlags = flagsSeen.Where(f => !ContentMentions(contentRoot, f)).OrderBy(f => f, StringComparer.Ordinal).ToList();
        Section("World flags set by the engine that no content reads", engineFlags);
        // the inverse: flags the events' conditions read that no run ever raised — an event gated on one can never fire
        var readFlags = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in Directory.EnumerateFiles(Path.Combine(contentRoot, "events"), "*.json"))
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(f), @"""type"":\s*""worldFlag"",\s*""flag"":\s*""([^""]+)""")) readFlags.Add(m.Groups[1].Value);
        Section("Flags content reads that no run ever raised", readFlags.Where(f => !flagsSeen.Contains(f) && !flagsSeen.Any(x => x.StartsWith(f + ":"))).OrderBy(f => f, StringComparer.Ordinal));

        Section("FAILURES", failures);
        Console.WriteLine(failures.Count == 0 && tokens.Count == 0 ? "\n=== QA PASSED ===" : $"\n=== QA FAILED — {Counted.Failures(failures.Count)}, {Counted.TokenIssues(tokens.Count)} ===");
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
    /// <summary>Why a line ended, as far as the chronicle says: the last deaths in the player's house, newest last (docs/GAPS.md Q3).</summary>
    internal static string WhyItEnded(World w)
    {
        var p = w.Protagonist; if (p == null) return "no one left";
        string family = p.Name.Contains(' ') ? p.Name[(p.Name.LastIndexOf(' ') + 1)..] : p.Name;
        string[] ends = { " dies at ", " is killed", " is executed", " is slain", " does not come back" };
        var last = w.History.Where(l => l.Text.Contains(family) && ends.Any(e => l.Text.Contains(e))).TakeLast(3).Select(l => $"T{l.Turn} {(l.Text.Length > 110 ? l.Text[..110] + "…" : l.Text)}").ToList();
        return last.Count == 0 ? "the chronicle does not say" : string.Join(" | ", last);
    }

    internal static string Canonical(World world)
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
    private static readonly string[] CaesarKeys = { "triumphs_voted", "recalls_voted", "civil_wars", "republics_ended", "caesars_broken", "polities_founded", "polities_recognised", "polities_dissolved", "free_marches", "free_states", "camps_stirred", "censures_stirred", "wars_over_words", "motions_held_over", "chair_spoke", "casting_votes", "ventures_failed", "ventures_propped", "heirlooms_commissioned", "standards_won", "wedding_gifts", "heirlooms_sold", "private_shares_bought", "private_shares_sold", "private_dividends", "private_shares_left", "founder_stakes", "ventures_carried_by_others", "ventures_sold_failing", "company_ventures_carried", "company_ventures_closed", "ventures_sacked", "second_starts", "standards_retaken", "standards_returned", "standards_ransomed", "kin_commissions", "gifts_between_houses", "gifts_sent_back", "ventures_settled", "held_venture_income", "founder_cuts", "heirlooms_plundered", "signets_surrendered", "favour_gifts", "favours_called", "favours_kept", "favours_broken", "raids_met_at_the_march", "raids_turned_back", "nation_commanders_fallen", "nation_rulers_succeeded", "pretenders_risen", "pretenders_won", "pretenders_broken", "thrones_taken", "emergencies_moved", "emergencies_granted", "emergencies_refused", "emergencies_renewed", "emergencies_laid_down", "emergency_years_kept", "emergencies_stripped", "emergency_levies", "republics_ended_by_vote", "feuds_begun", "feuds_ended_blood", "feuds_wrong_blood", "feuds_ended_duel", "feuds_ended_price", "feuds_let_lie", "duels_fought", "duel_deaths", "duels_refused", "favours_lapsed", "favours_for_money", "favours_for_a_match", "gifts_refused", "sack_claims_paid", "company_ventures_sold", "ventures_foreclosed", "things_foreclosed", "worlds_foreclosed", "pledges_made", "siege_claims" };

    /// <summary>One soak run. The player picks a random affordable choice (or skips one in five) from a decision stream seeded
    /// off the world seed, so a replay repeats the same decisions.</summary>
    /// <summary>Every world flag seen at any BeginTurn of any recording run (the replay does not record).</summary>
    private static readonly HashSet<string> FlagsEveryTurn = new(StringComparer.Ordinal);
    private static (World world, string chronicle) Soak(string contentRoot, ContentDatabase content, string sid, ulong seed, int turns, Options o,
        Dictionary<string, int>? fired, Dictionary<string, int>? taken, HashSet<string>? tokens, List<string>? oddities, string label)
    {
        var world = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(contentRoot, sid), seed);
        world.Difficulty = o.Difficulty; world.CrisisIntensity = o.Intensity; world.FamilyAutonomy = o.Autonomy; Praetoria.Core.Galaxy.GalaxyGrowth.Grow(world, content, o.Size);
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
        if (fired != null) foreach (var f in world.WorldFlags) FlagsEveryTurn.Add(f);   // the flags as the events see them, every turn — one-turn flags included
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
        var cands = Candidates(tc, w, content, decisions); cands.AddRange(ServiceAndLine(w, content, decisions)); if (cands.Count == 0) return;
        var ctx = new CommandContext(w, WorldBuilder.RngFor(w));
        int done = 0;
        for (int tries = 0; tries < 4 && cands.Count > 0 && done < 2; tries++)
        {
            int i = decisions.NextInt(0, cands.Count); var c = cands[i]; cands.RemoveAt(i);
            if (!tc.Executor.TryExecute(c, ctx)) continue;
            done++;
            if (taken != null) { string key = "cmd/" + c.GetType().Name.Replace("Command", ""); taken[key] = taken.GetValueOrDefault(key) + 1; }
        }
    }

    /// <summary>The random player's commands by service and over the line of the house (docs/MILITARY.md, "A command by service";
    /// docs/HOUSEHOLD.md, "Struck from the line, cast out, and the undutiful will"), offered to the soak only: the scripted players
    /// (Scripted.cs) seek and answer service on their own terms, and none of them would strike a child out by chance. Built in id
    /// order; the draws choose which of the rarer ones are offered and whom each names.</summary>
    internal static List<ICommand> ServiceAndLine(World w, ContentDatabase content, IRng decisions)
    {
        var cands = new List<ICommand>();
        var p = w.Protagonist; if (p is not { Alive: true } || w.House(p.HouseId) is not { } house) return cands;
        var offices = content.Offices;
        // seek a command for the head or a grown relative, from someone who could take them on
        var seekers = ServiceSystem.Seekers(w, p).Where(k => ServiceSystem.FreeRefusal(w, k) == null).Take(3).ToList();
        if (seekers.Count > 0 && decisions.NextInt(0, 3) == 0)
        {
            var who = seekers[decisions.NextInt(0, seekers.Count)]; var takers = ServiceSystem.TakersFor(w, offices, who);
            if (takers.Count > 0) cands.Add(new SeekCommandCommand(p.Id, who.Id, takers[decisions.NextInt(0, takers.Count)], offices));
        }
        // answer what is put to the player: yes or no
        foreach (var a in w.ServiceAsks.OrderBy(a => a.Id, StringComparer.Ordinal).ToList()) cands.Add(new AnswerServiceCommand(p.Id, a.Id, decisions.NextInt(0, 2) == 0, offices));
        // as a magistrate, name a legate to a fleet under the office; as a house at war, borrow an officer of another house
        foreach (var f in w.Fleets.Values.Where(f => !f.IsAway).OrderBy(f => f.Id, StringComparer.Ordinal))
        {
            if (ServiceSystem.AuthorityOver(w, offices, p, f) != null && decisions.NextInt(0, 4) == 0)
            {
                var them = ServiceSystem.Candidates(w, f, 4); if (them.Count > 0) cands.Add(new NameLegateCommand(p.Id, f.Id, them[decisions.NextInt(0, them.Count)].Id, offices));
            }
        }
        if (w.EnemiesOf(house.Id).Any() && decisions.NextInt(0, 3) == 0)
            foreach (var f in ServiceSystem.HouseFleets(w, house.Id).OrderBy(f => f.Id, StringComparer.Ordinal).Take(2))
            {
                var them = ServiceSystem.Candidates(w, f, 4); if (them.Count > 0) cands.Add(new BorrowOfficerCommand(p.Id, f.Id, them[decisions.NextInt(0, them.Count)].Id));
            }
        // relieve one commanding a fleet of the house by service, or have one of the house resign theirs, now and then
        foreach (var s in w.Service.OrderBy(s => s.FleetId, StringComparer.Ordinal).ToList())
        {
            if ((s.OwnerId == house.Id || s.NamedById == p.Id) && decisions.NextInt(0, 10) == 0) cands.Add(new RelieveServiceCommand(p.Id, s.FleetId));
            if (w.Char(s.CommanderId)?.HouseId == house.Id && decisions.NextInt(0, 10) == 0) cands.Add(new ResignServiceCommand(p.Id, s.CommanderId));
        }
        // the line: strike out or cast out a grown relative, rarely; restore or take back one, more often; settle or plead a suit
        var grown = w.LivingCharacters().Where(c => c.HouseId == house.Id && c.Id != p.Id && c.Age >= DynastySystem.AgeOfMajority).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        if (grown.Count > 0 && decisions.NextInt(0, 30) == 0) cands.Add(new DisownCommand(p.Id, DisownCommand.Strike, grown[decisions.NextInt(0, grown.Count)].Id));
        if (grown.Count > 0 && decisions.NextInt(0, 60) == 0) cands.Add(new DisownCommand(p.Id, DisownCommand.CastOut, grown[decisions.NextInt(0, grown.Count)].Id));
        foreach (var d in DisownSystem.Of(w, house.Id, DisownSystem.Struck)) if (decisions.NextInt(0, 6) == 0) cands.Add(new DisownCommand(p.Id, DisownCommand.Restore, d.PersonId));
        foreach (var d in DisownSystem.Of(w, house.Id, DisownSystem.CastOut)) if (decisions.NextInt(0, 6) == 0) cands.Add(new DisownCommand(p.Id, DisownCommand.TakeBack, d.PersonId));
        foreach (var s in DisownSystem.SuitsOn(w, house.Id))
        {
            if (decisions.NextInt(0, 4) == 0) cands.Add(new WillSuitCommand(p.Id, WillSuitCommand.Settle, s.Id));
            if (grown.Count > 0 && decisions.NextInt(0, 3) == 0) cands.Add(new WillSuitCommand(p.Id, WillSuitCommand.Plead, s.Id, grown[decisions.NextInt(0, grown.Count)].Id));
        }
        return cands;
    }

    /// <summary>Every command the protagonist might give this turn, built in id order. The draws on <paramref name="decisions"/>
    /// only choose which of the rarer ones are offered and which target each takes; the scripted players (Scripted.cs) call it
    /// with a stream that offers them all.</summary>
    internal static List<ICommand> Candidates(TurnController tc, World w, ContentDatabase content, IRng decisions)
    {
        var p = w.Protagonist; if (p is not { Alive: true }) return new List<ICommand>();
        var house = w.House(p.HouseId); if (house == null) return new List<ICommand>();
        var g = w.Galaxy; var bc = content.Businesses;
        var cands = new List<ICommand>();
        string Ord(string a) => a;
        var worlds = w.HoldingsOf(house.Id).Where(h => h.IsPlanet).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        var fleets = w.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.Office && !f.IsAway).OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
        var others = w.Houses.Values.Where(h => h.Id != house.Id && !World.Outsider(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        var members = house.Members.Where(id => w.Char(id)?.Alive == true && id != p.Id).OrderBy(Ord, StringComparer.Ordinal).ToList();

        cands.Add(new TrainCommand(p.Id, Skills[decisions.NextInt(0, Skills.Length)]));
        foreach (var h in worlds)
        {
            foreach (var b in content.Holdings.Buildings.OrderBy(b => b.Id, StringComparer.Ordinal))
                if (b.FitsSpecialization(h.Specialization) && !h.Buildings.Contains(b.Id)) cands.Add(new BuildCommand(p.Id, h.Id, b.Id, content.Holdings, content.Titles));
            cands.Add(new RaiseFleetCommand(p.Id, h.Id, content.Fleets, content.ShipClasses, null, content.Titles));
            cands.Add(new InvestCommand(p.Id, h.Id, bc));
            if (members.Count > 0) cands.Add(new AppointGovernorCommand(p.Id, h.Id, members[decisions.NextInt(0, members.Count)]));
            if (!content.Policies.Local.IsEmpty)   // a law for this one world (docs/POLITICS.md, "Local law")
            {
                var ll = content.Policies.Local.Laws[decisions.NextInt(0, content.Policies.Local.Laws.Count)];
                cands.Add(new SetLocalLawCommand(p.Id, h.Id, ll.Id, ll.Options[decisions.NextInt(0, ll.Options.Count)].Id, content.Policies));
            }
        }
        // edicts (docs/EDICTS.md): one the person may or may not be placed to issue, and one of their own withdrawn
        if (content.Edicts.Defs.Count > 0 && decisions.NextInt(0, 2) == 0)
        {
            var ed = content.Edicts.Defs[decisions.NextInt(0, content.Edicts.Defs.Count)];
            cands.Add(new IssueEdictCommand(p.Id, ed.Id, content, others.Count > 0 ? others[decisions.NextInt(0, others.Count)].Id : ""));
            var mine = w.Edicts.Where(e => e.IssuerId == p.Id).OrderBy(e => e.Id, StringComparer.Ordinal).ToList();
            if (mine.Count > 0) cands.Add(new WithdrawEdictCommand(p.Id, mine[decisions.NextInt(0, mine.Count)].Id));
        }
        if (w.InAcademy)   // the Academy's own business; the bus refuses the rest, which the soak still offers to prove it
        {
            foreach (var s in content.Academy.Subjects) cands.Add(new ChooseStudyCommand(p.Id, s.Id, content.Academy));
            foreach (var t in AcademySystem.Tracks) cands.Add(new ChooseServiceCommand(p.Id, t));
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
                // a flag captain under the admiral, now and then relieved (docs/MILITARY.md, "Commanding a navy")
                if (members.Count > 0 && decisions.NextInt(0, 3) == 0) cands.Add(new AppointCaptainCommand(p.Id, f.Id, members[decisions.NextInt(0, members.Count)]));
                if (f.CaptainId.Length > 0 && decisions.NextInt(0, 12) == 0) cands.Add(new RelieveCaptainCommand(p.Id, f.Id));
                cands.Add(new RefitFleetCommand(p.Id, f.Id));
                cands.Add(new SetStanceCommand(p.Id, f.Id, FleetSystem.Stances[decisions.NextInt(0, FleetSystem.Stances.Length)]));
                cands.Add(new SetLoadoutCommand(p.Id, f.Id, FleetSystem.Loadouts[decisions.NextInt(0, FleetSystem.Loadouts.Length)]));
            }
            if (fleets.Count >= 2) cands.Add(new MergeFleetsCommand(p.Id, fleets[0].Id, fleets[1].Id));
            cands.Add(new SetCommandStyleCommand(p.Id, p.Id, new[] { "front", "flag", "none" }[decisions.NextInt(0, 3)]));
            foreach (var e in w.EnemiesOf(house.Id).Take(2)) foreach (var h in w.HoldingsOf(e).Where(x => x.IsPlanet && x.SystemId.Length > 0).Take(1)) cands.Add(new DeclareCampaignCommand(p.Id, h.SystemId, content.Fleets));
            if (NationSystem.AtWar(w) is { } abroad) foreach (var f in fleets) { cands.Add(new SendExpeditionCommand(p.Id, f.Id, abroad.Id, content.Fleets, content.Nations)); cands.Add(new SendExpeditionCommand(p.Id, f.Id, abroad.Id, content.Fleets, content.Nations)); }
            foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == house.Id && f.IsAway)) if (decisions.NextInt(0, 6) == 0) cands.Add(new RecallExpeditionCommand(p.Id, f.Id, content.Nations));
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
        foreach (var b in w.BusinessesOf(house.Id).OrderBy(b => b.Id, StringComparer.Ordinal)) { cands.Add(new ExpandBusinessCommand(p.Id, b.Id, bc)); if (tc.Ventures != null && decisions.NextInt(0, 6) == 0) cands.Add(new SellVentureCommand(p.Id, b.Id, decisions.NextInt(0, 2) == 0 ? "" : SellVentureCommand.Best, tc.Ventures)); if (tc.Ventures != null && b.LosingYears > 0 && decisions.NextInt(0, 2) == 0) cands.Add(new PropVentureCommand(p.Id, b.Id, tc.Ventures)); }
        if (tc.Ventures != null)
        {
            var theirs = w.Businesses.Values.Where(b => b.OwnerId != house.Id).OrderBy(b => b.Id, StringComparer.Ordinal).ToList();
            if (theirs.Count > 0) cands.Add(new BuyVentureCommand(p.Id, theirs[decisions.NextInt(0, theirs.Count)].Id, tc.Ventures));
            foreach (var o in w.Offers.Where(o => o.ToHouseId == house.Id)) cands.Add(new AcceptVentureOfferCommand(p.Id, o.Id, tc.Ventures));
        }
        foreach (var pr in w.ProjectsOf(house.Id).OrderBy(x => x.Id, StringComparer.Ordinal)) { if (decisions.NextInt(0, 8) == 0) cands.Add(new PauseProjectCommand(p.Id, pr.Id)); if (decisions.NextInt(0, 8) == 0) cands.Add(new RushProjectCommand(p.Id, pr.Id)); if (decisions.NextInt(0, 12) == 0) cands.Add(new AbandonProjectCommand(p.Id, pr.Id)); }
        cands.Add(new TakeLoanCommand(p.Id, bc)); cands.Add(new RepayLoanCommand(p.Id)); cands.Add(new PetitionFiefCommand(p.Id));
        foreach (var gd in content.Market.Goods) { cands.Add(new BuyGoodCommand(p.Id, gd.Id, 1 + decisions.NextInt(0, 4), content.Trade)); cands.Add(new SellGoodCommand(p.Id, gd.Id, 1 + decisions.NextInt(0, 4), content.Trade)); }
        {   // the three orders: the census, patrons and clients, the voters
            var os = tc.Orders; cands.Add(new EnrollCommand(p.Id, os, content.Titles)); cands.Add(new ClientCommand(p.Id, ClientCommand.Leave, "", os));
            foreach (var o in w.Houses.Values.Where(o => o.Id != house.Id && !World.Outsider(o.Id)).OrderBy(o => o.Id, StringComparer.Ordinal))
            {
                if (decisions.NextInt(0, 8) == 0) cands.Add(new ClientCommand(p.Id, ClientCommand.Take, o.Id, os));
                if (decisions.NextInt(0, 12) == 0) cands.Add(new ClientCommand(p.Id, ClientCommand.Seek, o.Id, os));
                if (o.PatronId == house.Id && decisions.NextInt(0, 6) == 0) cands.Add(new ClientCommand(p.Id, ClientCommand.Release, o.Id, os));
                if (decisions.NextInt(0, 16) == 0) cands.Add(new CanvassCommand(p.Id, o.Id, os, x => ElectionSystem.Nominees(x, content.Offices, id => tc.Senate.Weight(x, id)).Select(n => n.houseId)));
            }
        }
        {   // courtship and marriage: begin suits, press them, settle them, answer the suits of others, tend and end marriages
            string[] acts = { CourtshipSystem.ActTime, CourtshipSystem.ActGift, CourtshipSystem.ActFamily, CourtshipSystem.ActRival }; string[] dowries = { CourtshipSystem.None, CourtshipSystem.Customary, CourtshipSystem.Rich };
            foreach (var k in w.LivingCharacters().Where(k => k.HouseId == house.Id && k.Age >= CourtshipSystem.BetrothalAge && DynastySystem.Spouse(w, k) == null).OrderBy(k => k.Id, StringComparer.Ordinal).Take(3))
            {
                if (decisions.NextInt(0, 3) != 0) continue; var pr = CourtshipSystem.Prospects(w, k);
                if (pr.Count > 0) cands.Add(new CourtCommand(p.Id, k.Id, pr[decisions.NextInt(0, Math.Min(pr.Count, 8))].person.Id));
            }
            foreach (var su in w.Courtships.OrderBy(x => x.Id, StringComparer.Ordinal).ToList())
            {
                string kids = decisions.NextInt(0, 3) switch { 0 => house.Id, 1 => "", _ => w.Char(su.Incoming ? su.SuitorId : su.TargetId)?.HouseId ?? "" };
                cands.Add(new SettleSuitCommand(p.Id, su.Id, SettleSuitCommand.Propose, dowries[decisions.NextInt(0, 3)], kids));
                if (su.Incoming) { if (decisions.NextInt(0, 3) == 0) cands.Add(new SettleSuitCommand(p.Id, su.Id, SettleSuitCommand.Refuse)); continue; }
                cands.Add(new WooCommand(p.Id, su.Id, acts[decisions.NextInt(0, acts.Length)])); cands.Add(new SettleSuitCommand(p.Id, su.Id, SettleSuitCommand.Elope));
                if (decisions.NextInt(0, 10) == 0) cands.Add(new SettleSuitCommand(p.Id, su.Id, SettleSuitCommand.BreakOff));
            }
            cands.Add(new MarriageCommand(p.Id, p.Id, MarriageCommand.Tend));
            if (decisions.NextInt(0, 25) == 0) foreach (var mm in w.Marriages.Where(mm => w.Char(mm.A)?.HouseId == house.Id || w.Char(mm.B)?.HouseId == house.Id).OrderBy(mm => mm.A, StringComparer.Ordinal).Take(1))
                cands.Add(new MarriageCommand(p.Id, w.Char(mm.A)?.HouseId == house.Id ? mm.A : mm.B, MarriageCommand.Divorce));
        }
        {   // working on the houses of the region: a word or a gift, now and then
            if (decisions.NextInt(0, 3) == 0) foreach (var nb in PolitySystem.Neighbours(w, house).Take(3)) cands.Add(new WorkHouseCommand(p.Id, nb.Id, decisions.NextInt(0, 2) == 0 ? PolitySystem.Word : PolitySystem.Gift, tc.Polities));
        }
        {   // breaking away: declare a region independent, rarely; in the great war, ask for a partition
            if (decisions.NextInt(0, 12) == 0) foreach (var rg in PolitySystem.RegionsOf(w, house).Take(2)) cands.Add(new SecessionCommand(p.Id, SecessionCommand.Declare, rg.Id, PolitySystem.Forms[decisions.NextInt(0, PolitySystem.Forms.Length)], tc.Polities));
            if (decisions.NextInt(0, 6) == 0) cands.Add(new SecessionCommand(p.Id, SecessionCommand.SeekPartition, "", "", tc.Polities));
            if (decisions.NextInt(0, 20) == 0) cands.Add(new SecessionCommand(p.Id, SecessionCommand.Rejoin, "", "", tc.Polities));
            // joining a state that exists, alone or with a region; and setting the great houses against one another
            foreach (var st in w.Polities.Where(x => x.Recognised > 0).OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                if (decisions.NextInt(0, 8) == 0) cands.Add(new SecessionCommand(p.Id, SecessionCommand.Join, "", st.Id, tc.Polities));
                if (decisions.NextInt(0, 8) == 0) foreach (var rg in PolitySystem.RegionsOf(w, house).Take(2)) cands.Add(new SecessionCommand(p.Id, SecessionCommand.Join, rg.Id, st.Id, tc.Polities));
            }
            if (decisions.NextInt(0, 15) == 0) cands.Add(new SecessionCommand(p.Id, SecessionCommand.Stir, "", "", tc.Polities));
            // the rest of it: stirring against one house, leaving a state, a promise or a threat to a house of the region
            var reachHouses = w.Houses.Values.Where(x => x.Id != house.Id && !World.Outsider(x.Id) && PolitySystem.InReach(w, x.Id)).OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
            if (reachHouses.Count > 0 && decisions.NextInt(0, 20) == 0) cands.Add(new SecessionCommand(p.Id, SecessionCommand.Stir, reachHouses[decisions.NextInt(0, reachHouses.Count)].Id, "", tc.Polities));
            if (decisions.NextInt(0, 25) == 0) cands.Add(new SecessionCommand(p.Id, SecessionCommand.Leave, "", "", tc.Polities));
            foreach (var nb in PolitySystem.Neighbours(w, house).Take(3))
            {
                if (decisions.NextInt(0, 8) == 0) cands.Add(new WorkHouseCommand(p.Id, nb.Id, PolitySystem.Promise, tc.Polities));
                if (decisions.NextInt(0, 12) == 0) cands.Add(new WorkHouseCommand(p.Id, nb.Id, PolitySystem.Threat, tc.Polities));
            }
            if (PolitySystem.Of(w, house.Id) is { } ownState && ownState.HeadHouseId == house.Id)
                foreach (var ct in reachHouses.Where(x => PolitySystem.Courtable(w, ownState, x)).Take(2)) if (decisions.NextInt(0, 4) == 0) cands.Add(new WorkHouseCommand(p.Id, ct.Id, decisions.NextInt(0, 2) == 0 ? PolitySystem.Word : PolitySystem.Gift, tc.Polities));
        }
        {   // the Caesar road: answer a recall, cross the line, declare in another's war, pay the crews
            if (decisions.NextInt(0, 2) == 0) cands.Add(new CaesarCommand(p.Id, CaesarCommand.Obey, "", tc.Caesar));
            if (decisions.NextInt(0, 2) == 0) cands.Add(new CaesarCommand(p.Id, CaesarCommand.Cross, "", tc.Caesar));
            if (decisions.NextInt(0, 2) == 0) cands.Add(new CaesarCommand(p.Id, CaesarCommand.Join, decisions.NextInt(0, 2) == 0 ? CaesarSystem.General : CaesarSystem.Senate, tc.Caesar));
            foreach (var fl in w.Fleets.Values.Where(x => x.OwnerId == house.Id && x.AdmiralId.Length > 0).OrderBy(x => x.Id, StringComparer.Ordinal)) if (decisions.NextInt(0, 6) == 0) cands.Add(new CaesarCommand(p.Id, CaesarCommand.Donative, fl.Id, tc.Caesar));
        }
        {   // the ladder: stand, call in what is owed, speak for one of the house, give games
            foreach (var rung in content.Offices.Rungs) if (decisions.NextInt(0, 2) == 0) cands.Add(new LadderCommand(p.Id, LadderCommand.Stand, p.Id, rung.Id, content.Offices));
            foreach (var cd in w.Candidacies.ToList()) if (decisions.NextInt(0, 2) == 0) cands.Add(new LadderCommand(p.Id, LadderCommand.Back, cd.CharacterId, "", content.Offices));
            if (decisions.NextInt(0, 12) == 0) cands.Add(new LadderCommand(p.Id, LadderCommand.Withdraw, p.Id, "", content.Offices));
            foreach (var cd in w.Candidacies.ToList()) if (decisions.NextInt(0, 6) == 0) cands.Add(new LadderCommand(p.Id, LadderCommand.Dissuade, cd.CharacterId, "", content.Offices));
            cands.Add(new LadderCommand(p.Id, LadderCommand.Games, p.Id, "", content.Offices));
            // the work of a position (docs/LADDER.md): the business of an office, a matter before the bench, the year in the Senate
            if (LadderSystem.BusinessOf(w, p.Id) is { } officeWork && decisions.NextInt(0, 4) != 0) { var cs = LadderSystem.ChoicesFor(officeWork.Kind); cands.Add(new LadderCommand(p.Id, LadderCommand.Decide, p.Id, cs[decisions.NextInt(0, cs.Length)], content.Offices)); }
            foreach (var kind in new[] { LadderSystem.Hearing, LadderSystem.Appeal, LadderSystem.SeatKind, LadderSystem.Charge, LadderSystem.Desk, LadderSystem.Posting, LadderSystem.Brief, LadderSystem.Ledgers, LadderSystem.Watch })
                if (LadderSystem.DutyOf(w, p.Id, kind) is { } duty && decisions.NextInt(0, 3) != 0) { var cs = LadderSystem.ChoicesOf(content.Offices, duty); cands.Add(new DutyCommand(p.Id, kind, cs[decisions.NextInt(0, cs.Length)], content.Offices, content.Policies)); }
            // the family (docs/WEALTH.md, docs/EDUCATION.md): a sum given or lent, a loan forgiven, whose money a dowry is, asking and teaching
            var grownKin = w.LivingCharacters().Where(x => x.HouseId == house.Id && x.Id != p.Id && x.Age >= WealthSystem.Adult).OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
            if (grownKin.Count > 0 && decisions.NextInt(0, 6) == 0)
            {
                var kin = grownKin[decisions.NextInt(0, grownKin.Count)]; int pick = decisions.NextInt(0, 12);
                if (pick == 0) cands.Add(new WealthCommand(p.Id, WealthCommand.Gift, kin.Id, "5"));
                else if (pick == 6) cands.Add(new WealthCommand(p.Id, WealthCommand.LendAtInterest, kin.Id, "10"));
                else if (pick == 7) cands.Add(new WealthCommand(p.Id, WealthCommand.Borrow, kin.Id, "5"));
                else if (pick == 8) cands.Add(new WealthCommand(p.Id, WealthCommand.Repay, kin.Id, ""));
                else if (pick == 9) cands.Add(new EducationCommand(p.Id, EducationCommand.AskAndPay, kin.Id, EducationSystem.All[decisions.NextInt(0, EducationSystem.All.Length)]));
                else if (pick == 10) cands.Add(new EducationCommand(p.Id, EducationCommand.Thank, kin.Id, ""));
                else if (pick == 5 && decisions.NextInt(0, 2) == 0) cands.Add(new EducationCommand(p.Id, EducationCommand.AskAndSpeak, kin.Id, EducationSystem.All[decisions.NextInt(0, EducationSystem.All.Length)]));
                else if (pick == 11) { foreach (var it in WealthSystem.HeldBy(w, p.Id).Take(1)) cands.Add(new WealthCommand(p.Id, it.Entailed ? WealthCommand.BreakEntail : WealthCommand.EntailItem, it.Id, "")); }
                else if (pick == 1) cands.Add(new WealthCommand(p.Id, WealthCommand.Lend, kin.Id, "10"));
                else if (pick == 2) cands.Add(new WealthCommand(p.Id, WealthCommand.Forgive, kin.Id, ""));
                else if (pick == 3) cands.Add(new WealthCommand(p.Id, WealthCommand.Dowries, "", decisions.NextInt(0, 2) == 0 ? "purse" : "treasury"));
                else if (pick == 4) cands.Add(new EducationCommand(p.Id, EducationCommand.Ask, kin.Id, EducationSystem.All[decisions.NextInt(0, EducationSystem.All.Length)]));
                else cands.Add(new EducationCommand(p.Id, EducationCommand.Teach, kin.Id, grownKin[decisions.NextInt(0, grownKin.Count)].Id));
            }
            if (decisions.NextInt(0, 12) == 0) cands.Add(new CommitteeCommand(p.Id, LadderSystem.Committees[decisions.NextInt(0, 3)]));
            // the docket (docs/LADDER.md): a suit brought, a counter-suit, and an answer to a charge
            if (LadderSystem.Active(w, content.Offices) && others.Count > 0 && decisions.NextInt(0, 3) == 0)
            {
                var sued = others[decisions.NextInt(0, others.Count)]; var grounds = LadderSystem.Grounds(w, house, sued);
                if (grounds.Count > 0) cands.Add(new BringSuitCommand(p.Id, sued.Id, grounds[decisions.NextInt(0, grounds.Count)].Matter, content.Offices));
                foreach (var s in LadderSystem.SuitsNaming(w, house.Id)) if (decisions.NextInt(0, 2) == 0) cands.Add(new SuitCommand(p.Id, SuitCommand.Counter, s.Id));
                foreach (var ch in LadderSystem.ChargesOn(w, house.Id))
                    cands.Add(decisions.NextInt(0, 2) == 0 ? new ChargeCommand(p.Id, ChargeCommand.Admit, ch.Id, content.Offices) : new ChargeCommand(p.Id, ChargeCommand.Plead, ch.Id, content.Offices, members.Count > 0 ? members[decisions.NextInt(0, members.Count)] : p.Id));
            }
            // what a rich house's money can do (docs/WEALTH.md): land settled on one of the house, the seat, a house at the capital, a canvass paid for
            if (decisions.NextInt(0, 4) == 0)
            {
                string one = members.Count > 0 && decisions.NextInt(0, 3) > 0 ? members[decisions.NextInt(0, members.Count)] : p.Id;
                cands.Add(new EndowCommand(p.Id, one)); cands.Add(new TownHouseCommand(p.Id, one, decisions.NextInt(0, 2) == 0));
                var home = w.HoldingsOf(house.Id).Where(h => h.IsPlanet).OrderBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault(); cands.Add(new BuildSeatCommand(p.Id, home?.Id ?? ""));
                foreach (var cd in w.Candidacies.Where(c => w.Char(c.CharacterId)?.HouseId == house.Id).ToList())
                    cands.Add(new FundCampaignCommand(p.Id, cd.CharacterId, LadderSystem.CampaignSizes[decisions.NextInt(0, LadderSystem.CampaignSizes.Length)], content.Offices));
            }
            // dealings between houses (docs/WEALTH.md): a gift, a favour asked, a pledge, a threat, a loan and what follows one
            if (tc.Dealings is { } deals && others.Count > 0 && decisions.NextInt(0, 2) == 0)
            {
                var them = others[decisions.NextInt(0, others.Count)]; string what = DealingsCommand.All[decisions.NextInt(0, DealingsCommand.All.Length)];
                string motion = w.Motions.Count > 0 ? w.Motions[decisions.NextInt(0, w.Motions.Count)].Id : "";
                string value = what == DealingsCommand.Gift ? DealingsSystem.GiftSizes[decisions.NextInt(0, DealingsSystem.GiftSizes.Length)].ToString()
                    : what == DealingsCommand.Lend ? DealingsSystem.LoanSizes[decisions.NextInt(0, DealingsSystem.LoanSizes.Length)].ToString()
                    : what is DealingsCommand.AskVote or DealingsCommand.CallVote or DealingsCommand.Pledge ? motion : "";
                cands.Add(new DealingsCommand(p.Id, what, them.Id, deals, value, what == DealingsCommand.Lend ? DealingsSystem.LoanRates[decisions.NextInt(0, DealingsSystem.LoanRates.Length)] : 0));
            }
            // trade between houses (docs/WEALTH.md): a lot bought or sold out of a house's store, a standing deal, and one ended
            if (tc.HouseTrade is { } trade && others.Count > 0 && decisions.NextInt(0, 2) == 0)
            {
                var them = others[decisions.NextInt(0, others.Count)]; string what = HouseTradeCommand.All[decisions.NextInt(0, HouseTradeCommand.All.Length)];
                var goods = trade.Goods.Select(g => g.Id).OrderBy(g => g, StringComparer.Ordinal).ToList(); string good = goods.Count > 0 ? goods[decisions.NextInt(0, goods.Count)] : "";
                bool deal = what is HouseTradeCommand.DealBuy or HouseTradeCommand.DealSell; int lot = HouseTradeSystem.Lots[decisions.NextInt(0, HouseTradeSystem.Lots.Length)];
                string dealId = w.TradeDeals.Count > 0 ? w.TradeDeals[decisions.NextInt(0, w.TradeDeals.Count)].Id : "";
                cands.Add(new HouseTradeCommand(p.Id, what, them.Id, trade, good, lot, deal ? HouseTradeSystem.Terms[decisions.NextInt(0, HouseTradeSystem.Terms.Length)] : 0, what == HouseTradeCommand.End ? dealId : ""));
            }
            // the courts (docs/LADDER.md): a charge against a former magistrate, and now and then an offer to a sitting praetor
            if (LadderSystem.Active(w, content.Offices) && decisions.NextInt(0, 3) == 0)
            {
                var charges = LadderSystem.Chargeable(w, house.Id); if (charges.Count > 0) cands.Add(new ProsecutionCommand(p.Id, ProsecutionCommand.Prosecute, charges[decisions.NextInt(0, charges.Count)].Id, content.Offices));
                var sitting = w.Magistrates.Where(m => m.OfficeId == LadderSystem.Praetor && m.HouseId != house.Id).OrderBy(m => m.CharacterId, StringComparer.Ordinal).ToList();
                if (sitting.Count > 0 && decisions.NextInt(0, 4) == 0) cands.Add(new ProsecutionCommand(p.Id, ProsecutionCommand.Buy, sitting[decisions.NextInt(0, sitting.Count)].CharacterId, content.Offices));
            }
            if (LifeSystem.ElderOf(w, house) != null && decisions.NextInt(0, 3) == 0) cands.Add(new LifeCommand(p.Id, LifeCommand.AskElder, new[] { LifeSystem.AskPeace, LifeSystem.AskEase, LifeSystem.AskSpeak }[decisions.NextInt(0, 3)]));
            foreach (var sick in w.LivingCharacters().Where(x => x.HouseId == house.Id && x.Id != p.Id && x.Flags.Contains(LifeSystem.IllChar)).OrderBy(x => x.Id, StringComparer.Ordinal).Take(1)) if (decisions.NextInt(0, 2) == 0) cands.Add(new LifeCommand(p.Id, LifeCommand.Send, sick.Id));
        }
        {   // a life: rest, the physicians, and laying the house down
            if (decisions.NextInt(0, 4) == 0) cands.Add(new LifeCommand(p.Id, LifeCommand.Rest));
            if (decisions.NextInt(0, 4) == 0) cands.Add(new LifeCommand(p.Id, LifeCommand.Physicians));
            if (decisions.NextInt(0, 40) == 0) cands.Add(new LifeCommand(p.Id, LifeCommand.Retire));
        }
        {   // ambitions: grant or refuse what the house's people ask, and name an heir
            foreach (var k in AmbitionSystem.Members(w, house).Where(k => k.Pursuit is { Request.Length: > 0 })) cands.Add(new AmbitionCommand(p.Id, decisions.NextInt(0, 3) == 0 ? AmbitionCommand.Refuse : AmbitionCommand.Grant, k.Id, tc.Ambitions));
            foreach (var k in AmbitionSystem.Members(w, house).Where(k => w.Counter(VentureSystem.FailedFounderKey(k.Id)) > 0)) if (decisions.NextInt(0, 3) == 0) cands.Add(new AmbitionCommand(p.Id, AmbitionCommand.SecondStart, k.Id, tc.Ambitions));
            if (decisions.NextInt(0, 20) == 0) { var grownUp = AmbitionSystem.Members(w, house); cands.Add(new AmbitionCommand(p.Id, AmbitionCommand.NameHeir, grownUp.Count > 0 && decisions.NextInt(0, 3) != 0 ? grownUp[decisions.NextInt(0, grownUp.Count)].Id : "", tc.Ambitions)); }
        }
        if (w.IsImperium)   // a rising against the throne: take a side when asked, and now and then raise a standard (docs/THRONE.md)
        {
            if (w.Rising != null && decisions.NextInt(0, 3) == 0) cands.Add(new ThroneCommand(p.Id, decisions.NextInt(0, 2) == 0 ? ThroneCommand.Pretender : ThroneCommand.Throne, content.Titles, tc.Fleets));
            if (w.Rising == null && decisions.NextInt(0, 12) == 0) cands.Add(new ThroneCommand(p.Id, ThroneCommand.Raise, content.Titles, tc.Fleets));
        }
        if (w.Emergency is { } powers && powers.HolderId == p.Id)   // the Senate's emergency powers: levy now and then, and lay them down more often than not when they are due (docs/EMERGENCY.md)
        {
            if (decisions.NextInt(0, 3) == 0) cands.Add(new EmergencyCommand(p.Id, EmergencyCommand.Levy));
            if (powers.Due && decisions.NextInt(0, 3) != 0 || decisions.NextInt(0, 12) == 0) cands.Add(new EmergencyCommand(p.Id, EmergencyCommand.LayDown));
        }
        {   // blood between houses: answer what is asked, and now and then do something about a debt (docs/FEUD.md)
            foreach (var f in FeudSystem.Open(w).Where(f => f.WrongedId == house.Id || f.OwingId == house.Id).OrderBy(f => f.Id, StringComparer.Ordinal).ToList())
            {
                if (f.Asked.Length > 0) { if (decisions.NextInt(0, 2) == 0) cands.Add(new FeudCommand(p.Id, f.Id, f.Asked == "price" ? (decisions.NextInt(0, 2) == 0 ? FeudCommand.Pay : FeudCommand.Refuse) : f.Asked == "offer" ? (decisions.NextInt(0, 2) == 0 ? FeudCommand.Take : FeudCommand.Spurn) : (decisions.NextInt(0, 2) == 0 ? FeudCommand.Fight : FeudCommand.Decline))); continue; }
                if (decisions.NextInt(0, 4) != 0) continue;
                if (f.WrongedId == house.Id) { int pick = decisions.NextInt(0, 4); if (pick == 3 && FeudSystem.Blamed(w, f) is { } blamed) cands.Add(new AssassinateCommand(p.Id, blamed.Id, content.Friction)); else cands.Add(new FeudCommand(p.Id, f.Id, pick == 0 ? FeudCommand.Challenge : pick == 1 ? FeudCommand.Demand : FeudCommand.LetLie)); }
                else cands.Add(new FeudCommand(p.Id, f.Id, FeudCommand.Offer));
            }
        }
        {   // wealth: the allowance, the head's purse, debts, the will, heirlooms
            if (decisions.NextInt(0, 6) == 0) cands.Add(new WealthCommand(p.Id, WealthCommand.Allowance, "", WealthSystem.Allowances[decisions.NextInt(0, 3)]));
            if (decisions.NextInt(0, 12) == 0) cands.Add(new WealthCommand(p.Id, WealthCommand.Inheritance, "", decisions.NextInt(0, 2) == 0 ? WealthSystem.Entail : WealthSystem.Portions));
            foreach (var oh in WealthSystem.OweMe(w, house.Id)) if (decisions.NextInt(0, 4) == 0) cands.Add(new WealthCommand(p.Id, WealthCommand.Favour, oh.Id, decisions.NextInt(0, 2) == 0 ? WealthSystem.ForMoney : WealthSystem.ForMatch));
            if (decisions.NextInt(0, 20) == 0 && WealthSystem.HeldBy(w, p.Id).FirstOrDefault(x => !x.Entailed) is { } toGive && w.Houses.Values.Where(h => h.Id != house.Id && !World.Outsider(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).Select(h => Economy.HeadOf(w, h)).Where(c => c != null).Skip(decisions.NextInt(0, 6)).FirstOrDefault() is { } toHead) cands.Add(new WealthCommand(p.Id, WealthCommand.Give, toGive.Id, toHead.Id));
            if (tc.Ventures is { } vsp && decisions.NextInt(0, 12) == 0) { var mineB = w.BusinessesOf(house.Id).Where(x => x.CompanyId.Length == 0).OrderBy(x => x.Id, StringComparer.Ordinal).ToList(); var mineT = WealthSystem.HeldBy(w, p.Id); int pick = decisions.NextInt(0, mineB.Count + mineT.Count + 1); cands.Add(new PledgeCommand(p.Id, pick < mineB.Count ? mineB[pick].Id : pick < mineB.Count + mineT.Count ? mineT[pick - mineB.Count].Id : "", vsp)); }
            if (tc.Ventures is { } vsx && decisions.NextInt(0, 15) == 0 && w.BusinessesOf(house.Id).Where(x => x.CompanyId.Length == 0 && x.HeldById.Length == 0).OrderBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault() is { } toSettle)
            { var on = WealthSystem.Members(w, house); if (on.Count > 0) cands.Add(new SettleVentureCommand(p.Id, toSettle.Id, on[decisions.NextInt(0, on.Count)].Id, vsx)); }
            foreach (var st in w.Heirlooms.Where(x => WealthSystem.IsSpoil(x) && x.FromHouseId == house.Id).OrderBy(x => x.Id, StringComparer.Ordinal)) if (decisions.NextInt(0, 3) == 0) cands.Add(new WealthCommand(p.Id, WealthCommand.Ransom, st.Id, ""));
            foreach (var st in WealthSystem.HeldBy(w, p.Id).Where(x => WealthSystem.IsSpoil(x) && w.House(x.FromHouseId) is { } fh && Economy.HeadOf(w, fh) != null)) if (decisions.NextInt(0, 8) == 0) cands.Add(new WealthCommand(p.Id, WealthCommand.Give, st.Id, Economy.HeadOf(w, w.House(st.FromHouseId)!)!.Id));
            if (decisions.NextInt(0, 10) == 0) cands.Add(new WealthCommand(p.Id, WealthCommand.Commission, "", WealthSystem.Commissions[decisions.NextInt(0, WealthSystem.Commissions.Length)]));
            if (decisions.NextInt(0, 4) == 0) cands.Add(new WealthCommand(p.Id, WealthCommand.Move, "", (decisions.NextInt(0, 2) == 0 ? 5 : -5).ToString()));
            var grownKin = WealthSystem.Members(w, house).Where(k => k.Id != p.Id).ToList();
            foreach (var k in grownKin.Where(k => k.Purse < 0)) cands.Add(new WealthCommand(p.Id, WealthCommand.Settle, k.Id, decisions.NextInt(0, 2) == 0 ? "purse" : "treasury"));
            if (grownKin.Count > 0 && decisions.NextInt(0, 6) == 0) { var k = grownKin[decisions.NextInt(0, grownKin.Count)]; cands.Add(new WealthCommand(p.Id, WealthCommand.Will, k.Id, new[] { "0", "25", "50", "100" }[decisions.NextInt(0, 4)])); foreach (var it in WealthSystem.HeldBy(w, p.Id)) { cands.Add(new WealthCommand(p.Id, WealthCommand.Bequeath, it.Id, k.Id)); if (decisions.NextInt(0, 6) == 0) cands.Add(new WealthCommand(p.Id, WealthCommand.Give, it.Id, k.Id)); if (decisions.NextInt(0, 10) == 0) cands.Add(new WealthCommand(p.Id, WealthCommand.Sell, it.Id, "")); } }
        }
        {   // education: what the head studies, the children's courses, tutors
            if (decisions.NextInt(0, 4) == 0) cands.Add(new EducationCommand(p.Id, EducationCommand.Study, p.Id, EducationSystem.All[decisions.NextInt(0, EducationSystem.All.Length)]));
            foreach (var k in w.LivingCharacters().Where(k => k.HouseId == house.Id).OrderBy(k => k.Id, StringComparer.Ordinal))
            {
                if (k.Age < EducationSystem.Adult && decisions.NextInt(0, 5) == 0) cands.Add(new EducationCommand(p.Id, EducationCommand.Upbringing, k.Id, decisions.NextInt(0, 4) == 0 ? "" : EducationSystem.Groups[decisions.NextInt(0, 3)]));
                if (decisions.NextInt(0, 8) == 0) cands.Add(new EducationCommand(p.Id, EducationCommand.Tutor, k.Id, decisions.NextInt(0, 3).ToString()));
            }
        }
        {   // the household: take people in, adopt, send home, seek a mentor, hand duties on and take them back
            string[] kinds = { HouseholdSystem.Ward, HouseholdSystem.Protege, HouseholdSystem.Companion };
            foreach (var pe in house.Petitions.ToList()) { cands.Add(new HouseholdCommand(p.Id, HouseholdCommand.TakeIn, pe.PersonId, pe.As)); if (decisions.NextInt(0, 3) == 0) cands.Add(new HouseholdCommand(p.Id, HouseholdCommand.Decline, pe.PersonId)); }
            if (decisions.NextInt(0, 6) == 0) { var all = w.LivingCharacters().Where(x => x.HouseId != house.Id && x.Age >= 6).OrderBy(x => x.Id, StringComparer.Ordinal).ToList(); if (all.Count > 0) { var x = all[decisions.NextInt(0, all.Count)]; cands.Add(new HouseholdCommand(p.Id, HouseholdCommand.TakeIn, x.Id, kinds[decisions.NextInt(0, 3)])); cands.Add(new MentorCommand(p.Id, x.Id)); } }
            foreach (var f in HouseholdSystem.Found(w, house.Id)) { if (decisions.NextInt(0, 6) == 0) cands.Add(new HouseholdCommand(p.Id, HouseholdCommand.Adopt, f.Id)); if (decisions.NextInt(0, 12) == 0) cands.Add(new HouseholdCommand(p.Id, HouseholdCommand.Release, f.Id)); }
            if (decisions.NextInt(0, 20) == 0) cands.Add(new MentorCommand(p.Id, ""));
            var grown = HouseholdSystem.Eligible(w, house);
            foreach (var role in CourtSystem.Positions)   // the house's own court: take on one of the commons, name one of the family, let them go
            {
                if (house.Position(role) != null) { if (decisions.NextInt(0, 25) == 0) cands.Add(new RetainerCommand(p.Id, RetainerCommand.Dismiss, role, "", content.Names)); continue; }
                if (decisions.NextInt(0, 5) == 0) { var offers = CourtSystem.Offers(w, content.Names, house, role); cands.Add(new RetainerCommand(p.Id, RetainerCommand.Hire, role, offers[decisions.NextInt(0, offers.Count)].Id, content.Names)); }
                if (grown.Count > 0 && decisions.NextInt(0, 12) == 0) cands.Add(new RetainerCommand(p.Id, RetainerCommand.Appoint, role, grown[decisions.NextInt(0, grown.Count)].Id, content.Names));
            }
            foreach (var role in HouseholdSystem.Roles) { if (house.Duty(role) != null && role != HouseholdSystem.Marshal && decisions.NextInt(0, 5) == 0) { var told = HouseholdSystem.Instructions(w, house, role); if (told.Count > 0) cands.Add(new PostInstructionCommand(p.Id, role, told[decisions.NextInt(0, told.Count)])); } if (grown.Count > 0 && decisions.NextInt(0, 4) == 0) cands.Add(new DelegateCommand(p.Id, role, grown[decisions.NextInt(0, grown.Count)].Id, new[] { 0, 20, 100 }[decisions.NextInt(0, 3)])); if (decisions.NextInt(0, 15) == 0) cands.Add(new DelegateCommand(p.Id, role, "")); }
        }
        if (tc.Companies is { Open: true } cx)   // the Exchange: list, trade, and sit on the board
        {
            foreach (var b in w.BusinessesOf(house.Id).Where(b => b.CompanyId.Length == 0).OrderBy(b => b.Id, StringComparer.Ordinal)) if (decisions.NextInt(0, 5) == 0) cands.Add(new IncorporateCommand(p.Id, b.Id, CompanySystem.ControlledBy(w, house.Id).FirstOrDefault()?.Id ?? "", cx));
            cands.Add(new BuyBondCommand(p.Id, cx)); cands.Add(new SteadyMarketCommand(p.Id, cx));
            if (w.IsImperium && w.Companies.FirstOrDefault(c => c.Chartered) is { } great && w.Houses.Values.Where(h => h.Id != house.Id && !World.Outsider(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).Skip(decisions.NextInt(0, 5)).FirstOrDefault() is { } other) { cands.Add(new ThroneExchangeCommand(p.Id, ThroneExchangeCommand.GrantAct, great.Id, other.Id, cx, content.Policies)); cands.Add(new ThroneExchangeCommand(p.Id, ThroneExchangeCommand.SeizeAct, "", other.Id, cx, content.Policies)); }
            foreach (var co in w.Companies.OrderBy(c => c.Id, StringComparer.Ordinal))
            {
                if (decisions.NextInt(0, 4) == 0) cands.Add(new BuySharesCommand(p.Id, co.Id, 1 + decisions.NextInt(0, 12), cx));
                if (decisions.NextInt(0, 12) == 0) cands.Add(new PrivateSharesCommand(p.Id, co.Id, 1 + decisions.NextInt(0, 3), decisions.NextInt(0, 3) != 0, cx));   // and in the head's own name, out of the purse
                if (co.HeldBy(house.Id) > 0 && decisions.NextInt(0, 4) == 0) cands.Add(new SellSharesCommand(p.Id, co.Id, 1 + decisions.NextInt(0, 12), cx));
                if (co.ControllerId == house.Id) { cands.Add(new BoardCommand(p.Id, co.Id, BoardCommand.Float, 5 + decisions.NextInt(0, 15), cx)); cands.Add(new BoardCommand(p.Id, co.Id, BoardCommand.Buyback, 1 + decisions.NextInt(0, 6), cx)); cands.Add(new BoardCommand(p.Id, co.Id, BoardCommand.Payout, 25 * decisions.NextInt(0, 5), cx)); cands.Add(new BoardCommand(p.Id, co.Id, BoardCommand.Special, 0, cx)); cands.Add(new BoardCommand(p.Id, co.Id, BoardCommand.Private, 0, cx)); }
            }
        }
        foreach (var def in tc.Senate.Motions.Defs)
        {
            if (def.Id == "petition") continue;   // only an estate brings one
            string target = def.Target == "house" && others.Count > 0 ? others[decisions.NextInt(0, others.Count)].Id : def.Target == "region" && g != null && g.ReachRegions.Any() ? g.ReachRegions.ToList()[decisions.NextInt(0, g.ReachRegions.Count())].Id : "";
            if (def.Target == "house" && def.TargetSelfAllowed && decisions.NextInt(0, 2) == 0) target = house.Id;
            if (def.Target == "nation") { var ns = content.Nations.Defs.ToList(); if (ns.Count == 0) continue; target = ns[decisions.NextInt(0, ns.Count)].Id; }
            if (def.Target == "edict") { var es = EdictSystem.Annullable(w).ToList(); if (es.Count == 0) continue; target = es[decisions.NextInt(0, es.Count)].Id; }   // an annulment (docs/EDICTS.md)
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
        cands.Add(new StandForConsulCommand(p.Id, content.Offices));
        foreach (var m in SenateSystem.Docket(w))
        {
            cands.Add(new VoteCommand(p.Id, m.Id, decisions.NextInt(0, 3) - 1));
            if (decisions.NextInt(0, 3) == 0) cands.Add(new VetoCommand(p.Id, m.Id, content.Offices, tc.Senate));
            if (decisions.NextInt(0, 3) == 0) cands.Add(new ChairCommand(p.Id, m.Id, new[] { LadderSystem.HoldMotion, LadderSystem.SpeakFor, LadderSystem.SpeakAgainst }[decisions.NextInt(0, 3)], tc.Senate));
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

        return cands;
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
            if (t.Credits < 0 || t.Materials < 0 || t.Manpower < 0 || t.Influence < 0 || t.Exotics < 0) oddities.Add($"{label} t{world.Turn}: negative treasury — {h.Name} c{Praetoria.Core.Money.Held(t)} m{t.Materials} mp{t.Manpower} i{t.Influence} e{t.Exotics}");
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

    /// <summary>`praetoria drift --scenario id --seed N --turns T`: plays one game straight through and once more saved and resumed at the
    /// half-way point, and reports the first year the two differ, with what differs. For finding state that is not saved, or a rule
    /// whose outcome depends on the order things happen to sit in memory.</summary>
    public static int Drift(Options o)
    {
        string root = ContentLocator.FindContentDir(); var content = ContentLoader.LoadFromDirectory(root); string sid = o.Scenario; ulong seed = o.Seed; int turns = Math.Max(4, o.Turns), half = turns / 2;
        World Fresh() { var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, sid), seed); w.Difficulty = o.Difficulty; w.CrisisIntensity = o.Intensity; w.FamilyAutonomy = o.Autonomy; Praetoria.Core.Galaxy.GalaxyGrowth.Grow(w, content, o.Size); return w; }
        var straight = new List<(string state, string news)>(); var resumed = new List<(string state, string news)>();
        string News(World w) => string.Join("\n        ", w.History.Where(l => l.Turn >= w.Turn - 1).Select(l => $"T{l.Turn}: {(l.Text.Length > 200 ? l.Text[..200] : l.Text)}").TakeLast(40));
        {
            var w = Fresh(); var tc = new TurnController(w, content); var decisions = new SplitMix64Rng(seed * 7919 + 17);
            for (int t = 0; t < turns && !w.IsDynastyDead; t++) { PlayTurn(tc, w, content, decisions, null, null, null, ""); straight.Add((Canonical(w), News(w))); }
        }
        {
            var w = Fresh(); var tc = new TurnController(w, content); var decisions = new SplitMix64Rng(seed * 7919 + 17); int t = 0;
            for (; t < half && !w.IsDynastyDead; t++) { PlayTurn(tc, w, content, decisions, null, null, null, ""); resumed.Add((Canonical(w), News(w))); }
            tc.SyncRng(); var back = SaveSystem.Deserialize(SaveSystem.Serialize(w)); var tc2 = new TurnController(back, content);
            Console.WriteLine($"saved after year {t}; the saved world reads the same as the one it was saved from: {Canonical(back) == Canonical(w)}");
            for (; t < turns && !back.IsDynastyDead; t++) { PlayTurn(tc2, back, content, decisions, null, null, null, ""); resumed.Add((Canonical(back), News(back))); }
        }
        for (int i = 0; i < Math.Min(straight.Count, resumed.Count); i++)
        {
            if (straight[i].state == resumed[i].state) continue;
            Console.WriteLine($"first difference after year {i + 1} (saved after {half}):");
            string a = straight[i].state, b = resumed[i].state; int at = 0; while (at < a.Length && at < b.Length && a[at] == b[at]) at++;
            int from = Math.Max(0, at - 700); Console.WriteLine("  straight: …" + a.Substring(from, Math.Min(1000, a.Length - from))); Console.WriteLine("  resumed:  …" + b.Substring(from, Math.Min(1000, b.Length - from)));
            Console.WriteLine("  the year's news, straight:\n        " + straight[i].news); Console.WriteLine("  the year's news, resumed:\n        " + resumed[i].news);
            return 1;
        }
        Console.WriteLine($"no difference in {Math.Min(straight.Count, resumed.Count)} years."); return 0;
    }

    private static World SoakWithRoundTrip(string contentRoot, ContentDatabase content, string sid, ulong seed, int turns, Options o)
    {
        var world = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(contentRoot, sid), seed);
        world.Difficulty = o.Difficulty; world.CrisisIntensity = o.Intensity; world.FamilyAutonomy = o.Autonomy; Praetoria.Core.Galaxy.GalaxyGrowth.Grow(world, content, o.Size);
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
