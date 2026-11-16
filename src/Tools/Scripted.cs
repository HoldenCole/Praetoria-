using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Rng;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Tools;

/// <summary>
/// Players that play toward an end (docs/QA.md, Q2). The soak's random hand never marries, secedes or marches, because each
/// takes several right moves in a row, so a third of the game was never reached in play. Each player here has a road: an
/// ordered list of the steps it wants. Every turn it is shown every command the soak could offer, takes the wanted ones in
/// order, and writes down why a wanted step was refused. What comes back is how far along its road each got, which commands
/// landed, which events fired, and what stood in the way.
/// </summary>
internal static class Scripted
{
    /// <summary>One thing a road wants, by the command's type and how it describes itself.</summary>
    private sealed record Step(string Name, Func<ICommand, string, World, bool> Match, int Most = 1);
    private sealed record Milestone(string Name, Func<World, TurnController, bool> Reached);
    private sealed class Road
    {
        public string Id = "", Aim = "";
        public Step[] Wants = Array.Empty<Step>();
        public Func<ICommand, string, bool> Never = (_, _) => false;
        public Milestone[] Milestones = Array.Empty<Milestone>();
        public string[] Prefers = Array.Empty<string>();   // words in a choice's id this player would pick
    }

    /// <summary>What one run of one road found.</summary>
    internal sealed class Result
    {
        public string Road = "", Label = ""; public int Years; public bool LineEnded; public World? World;
        public Dictionary<string, int> ReachedIn = new();                       // milestone → the year it was first true
        public Dictionary<string, int> Landed = new();                          // command type → times it succeeded
        public Dictionary<string, Dictionary<string, int>> Refused = new();     // wanted step → reason → years
        public Dictionary<string, int> Fired = new();
    }

    // ---------------- streams that offer every command ----------------

    /// <summary>Always the least: every "one time in n" gate in the candidate list opens, and every pick is the first.</summary>
    private sealed class Least : IRng { public ulong State => 0; public double NextDouble() => 0; public int NextInt(int lo, int hi) => lo; }
    /// <summary>Each call the next in turn: the same gates open in rotation and the picks move through their lists.</summary>
    private sealed class Turning : IRng
    {
        private int _n; public Turning(int start) { _n = start; }
        public ulong State => (ulong)_n; public double NextDouble() => (_n++ % 10) / 10.0;
        public int NextInt(int lo, int hi) => hi <= lo ? lo : lo + (_n++ % (hi - lo));
    }

    private static bool Is<T>(ICommand c) => c is T;
    private static bool Starts(string d, string with) => d.StartsWith(with, StringComparison.Ordinal);

    // ---------------- the three roads ----------------

    private static readonly Func<ICommand, string, bool> NoBreakaway = (c, _) => c is SecessionCommand;
    private static readonly Func<ICommand, string, bool> NoMarch = (c, d) => c is CaesarCommand && (Starts(d, CaesarCommand.Cross) || Starts(d, CaesarCommand.Join));
    private static readonly Func<ICommand, string, bool> NoThrone = (c, _) => c is ProclaimImperiumCommand or SeizeThroneCommand;

    private static bool MyHouse(World w, out House house) { house = w.Protagonist != null ? w.House(w.Protagonist.HouseId)! : null!; return house != null; }
    private static bool HasFleetCommand(World w) => MyHouse(w, out var h) && w.Fleets.Values.Any(f => f.OwnerId == h.Id && f.AdmiralId.Length > 0 && w.Char(f.AdmiralId) is { Alive: true } a && a.HouseId == h.Id);

    private static readonly Road General = new()
    {
        Id = "general", Aim = "command a fleet, win a name with it, be wronged by the Senate, and march",
        Wants = new[]
        {
            new Step("cross the line", (c, d, _) => c is CaesarCommand && Starts(d, CaesarCommand.Cross)),
            new Step("take a side in the civil war", (c, d, _) => c is CaesarCommand && Starts(d, CaesarCommand.Join + " " + CaesarSystem.General)),
            new Step("seize the throne", (c, _, _) => c is SeizeThroneCommand),
            new Step("give the crews a donative", (c, d, _) => c is CaesarCommand && Starts(d, CaesarCommand.Donative)),
            new Step("put one of the house in command of a fleet", (c, _, w) => c is AssignAdmiralCommand && !HasFleetCommand(w)),
            new Step("raise ships", (c, _, _) => c is RaiseFleetCommand, Most: 2),
            // a house with no fleet and no money for one has to find the money first
            new Step("petition for a world", (c, _, w) => c is PetitionFiefCommand && !HasFleetCommand(w)),
            new Step("found a venture", (c, _, w) => c is FoundBusinessCommand && !HasFleetCommand(w)),
            new Step("borrow for the fleet", (c, _, w) => c is TakeLoanCommand && !HasFleetCommand(w) && MyHouse(w, out var h) && h.Debt == 0),
            new Step("stand for an office", (c, d, _) => c is StandForConsulCommand || c is LadderCommand && Starts(d, "stand")),
            new Step("move a triumph", (c, d, _) => c is ProposeMotionCommand && d.Contains("triumph", StringComparison.Ordinal)),
            new Step("send a fleet against a nation", (c, _, _) => c is SendExpeditionCommand),
            new Step("declare a campaign", (c, _, _) => c is DeclareCampaignCommand),
            new Step("go to war with a house", (c, _, w) => c is DeclareWarCommand && MyHouse(w, out var h) && !w.Wars.Any(x => x.Involves(h.Id))),
            new Step("command from the front", (c, d, _) => c is SetCommandStyleCommand && d.EndsWith("front", StringComparison.Ordinal)),
            new Step("refit the fleet", (c, _, _) => c is RefitFleetCommand),
            new Step("vote", (c, d, _) => c is VoteCommand && Starts(d, "vote for")),
        },
        Never = (c, d) => NoBreakaway(c, d) || c is SueForPeaceCommand || c is CaesarCommand && Starts(d, CaesarCommand.Obey) || c is ProclaimImperiumCommand,
        Prefers = new[] { "march", "refuse", "defy", "keep", "fight", "triumph", "press", "stand" },
        Milestones = new[]
        {
            new Milestone("one of the house commands a fleet", (w, _) => HasFleetCommand(w)),
            new Milestone("a battle won", (w, _) => w.Counter(FleetSystem.BattlesWonCounter) > 0),
            new Milestone("the Senate votes a triumph or a recall for the house", (w, _) => w.HasFlag("triumph_voted") || w.HasFlag("kin_triumph") || w.HasFlag("recalled") || w.HasFlag("kin_recalled") || w.HasFlag("triumph_refused")),
            new Milestone("the line is crossed", (w, _) => w.HasFlag("crossed_the_line") || w.CivilWar != null && MyHouse(w, out var h) && w.CivilWar.HouseId == h.Id),
            new Milestone("the march ends: a throne, or broken", (w, _) => w.HasFlag("caesar_crowned") || w.HasFlag("caesar_broken") || w.HasFlag("republic_fell")),
            new Milestone("or the march is swallowed by a larger war, with nothing decided", (w, _) => w.History.Any(h => h.Text.Contains("swallowed by a larger war", StringComparison.Ordinal))),
        },
    };

    private static readonly Road Seceder = new()
    {
        Id = "seceder", Aim = "work a region, take it out of the Reach, and keep it",
        Wants = new[]
        {
            new Step("declare independence", (c, d, _) => c is SecessionCommand && Starts(d, SecessionCommand.Declare)),
            new Step("seek a partition", (c, d, _) => c is SecessionCommand && Starts(d, SecessionCommand.SeekPartition)),
            new Step("join a state that already stands", (c, d, _) => c is SecessionCommand && Starts(d, SecessionCommand.Join)),
            new Step("work a house of the region", (c, _, _) => c is WorkHouseCommand, Most: 2),
            new Step("stir against the Reach", (c, d, _) => c is SecessionCommand && Starts(d, SecessionCommand.Stir)),
            new Step("raise ships", (c, _, _) => c is RaiseFleetCommand),
            new Step("put one of the house in command of a fleet", (c, _, w) => c is AssignAdmiralCommand && !HasFleetCommand(w)),
            new Step("appoint a governor", (c, _, _) => c is AppointGovernorCommand),
            new Step("petition for a world", (c, _, _) => c is PetitionFiefCommand),
        },
        Never = (c, d) => NoMarch(c, d) || NoThrone(c, d) || c is SecessionCommand && (Starts(d, SecessionCommand.Rejoin) || Starts(d, SecessionCommand.Leave)) || c is DeclareWarCommand,
        Prefers = new[] { "declare", "independ", "hold", "refuse", "free", "alone", "fight" },
        Milestones = new[]
        {
            new Milestone("the house holds a world", (w, _) => MyHouse(w, out var h) && w.HoldingsOf(h.Id).Any(x => x.IsPlanet)),
            new Milestone("independence declared, or a state joined", (w, _) => w.HasFlag("declared_independence") || w.HasFlag("a_house_joins_our_state") || MyHouse(w, out var h) && !PolitySystem.InReach(w, h.Id)),
            new Milestone("a war of independence", (w, _) => w.HasFlag("war_of_independence") || w.HasFlag("reconquest_begun")),
            new Milestone("it ends: won, let go, or lost", (w, _) => w.HasFlag("independence_won") || w.HasFlag("let_go_by_the_reach") || w.HasFlag("independence_lost") || w.HasFlag("rejoined_the_reach")),
        },
    };

    private static readonly Road Courtier = new()
    {
        Id = "courtier", Aim = "marry, hold office, lead a bench, and make the house's money work",
        Wants = new[]
        {
            new Step("propose a marriage", (c, _, _) => c is ProposeMarriageCommand),
            new Step("press a suit", (c, _, _) => c is CourtCommand or WooCommand or SettleSuitCommand or MarriageCommand),
            new Step("stand for an office", (c, d, _) => c is StandForConsulCommand || c is LadderCommand && Starts(d, "stand")),
            new Step("canvass", (c, _, _) => c is CanvassCommand),
            new Step("use the chair or the veto", (c, _, _) => c is VetoCommand or ChairCommand),
            new Step("do the year's duty", (c, _, _) => c is DutyCommand or CommitteeCommand),
            new Step("enrol in the Senate", (c, _, _) => c is EnrollCommand),
            new Step("found or join a bench", (c, _, _) => c is FoundFactionCommand or JoinFactionCommand),
            new Step("move a motion", (c, d, _) => c is ProposeMotionCommand && !d.Contains("war", StringComparison.Ordinal)),
            new Step("lobby", (c, _, _) => c is LobbyCommand),
            new Step("vote", (c, _, _) => c is VoteCommand),
            new Step("found a venture", (c, _, _) => c is FoundBusinessCommand),
            new Step("settle a venture on one of the house", (c, _, _) => c is SettleVentureCommand),
            new Step("buy shares", (c, d, _) => c is BuySharesCommand || c is PrivateSharesCommand && Starts(d, "buy")),
            new Step("sell shares", (c, d, w) => (c is SellSharesCommand || c is PrivateSharesCommand && Starts(d, "sell")) && w.Turn % 7 == 0),
            new Step("seek a mentor", (c, d, _) => c is MentorCommand && Starts(d, "seek")),
            new Step("grant what one of the house asks", (c, d, _) => c is AmbitionCommand && Starts(d, AmbitionCommand.Grant)),
            new Step("call in a favour or give a gift", (c, d, _) => c is WealthCommand && (Starts(d, WealthCommand.Favour) || Starts(d, WealthCommand.Commission))),
            new Step("see to an education", (c, _, _) => c is EducationCommand),
        },
        Never = (c, d) => NoBreakaway(c, d) || NoMarch(c, d) || NoThrone(c, d) || c is DeclareWarCommand || c is LeaveFactionCommand
            || c is AmbitionCommand && Starts(d, AmbitionCommand.Refuse) || c is MentorCommand && Starts(d, "leave"),
        Prefers = new[] { "accept", "thank", "bless", "agree", "grant", "welcome", "pay", "stand" },
        Milestones = new[]
        {
            new Milestone("the head is wed", (w, _) => w.Protagonist is { } p && w.Marriages.Any(m => m.Has(p.Id))),
            new Milestone("one of the house holds a magistracy", (w, _) => MyHouse(w, out var h) && w.Magistrates.Any(m => w.Char(m.CharacterId)?.HouseId == h.Id)),
            new Milestone("the house holds an office of the Reach", (w, _) => MyHouse(w, out var h) && w.Offices.Any(o => o.HouseId == h.Id)),
            new Milestone("the house sits on a bench", (w, _) => MyHouse(w, out var h) && FactionSystem.Of(w, h.Id) != null),
            new Milestone("a venture is a person's own", (w, _) => MyHouse(w, out var h) && w.BusinessesOf(h.Id).Any(b => VentureSystem.HolderOf(w, b) != null)),
        },
    };

    private static readonly Road[] Roads = { General, Seceder, Courtier };
    /// <summary>The last thing on a road: what "it got there" means in one line of the soak.</summary>
    internal static string[] MilestonesOf(string roadId) => Roads.First(r => r.Id == roadId).Milestones.Select(m => m.Name).ToArray();
    internal static readonly string[] RoadIds = Roads.Select(r => r.Id).ToArray();

    // ---------------- playing one ----------------

    private static readonly System.Text.RegularExpressions.Regex Digits = new(@"-?\d+", System.Text.RegularExpressions.RegexOptions.Compiled);
    /// <summary>Why a command will not go, in its own words where it has them, with the figures and names taken out so that
    /// the same reason counts as the same reason from year to year.</summary>
    private static string Why(ICommand c, CommandContext ctx, World w)
    {
        string reason = "refused";
        try { if (c.GetType().GetMethod("Refusal", new[] { typeof(CommandContext) })?.Invoke(c, new object[] { ctx }) is string said && said.Length > 0) reason = said; } catch { }
        foreach (var h in w.Houses.Values) reason = reason.Replace(h.Name, "a house", StringComparison.Ordinal);
        var names = new System.Text.StringBuilder(reason);
        foreach (var ch in w.Characters.Values) if (ch.Name.Length > 3 && reason.Contains(ch.Name, StringComparison.Ordinal)) names.Replace(ch.Name, "someone");
        return Digits.Replace(names.ToString(), "N");
    }

    private static List<(ICommand c, string d)> Offered(TurnController tc, World w, ContentDatabase content)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal); var all = new List<(ICommand, string)>();
        foreach (var rng in new IRng[] { new Least(), new Turning(w.Turn), new Turning(w.Turn * 7 + 3) })
            foreach (var c in Qa.Candidates(tc, w, content, rng)) { string d = c.Describe(); if (seen.Add(c.GetType().Name + "|" + d)) all.Add((c, d)); }
        return all;
    }

    /// <summary>A command a player with an end in view does not give by accident.</summary>
    private static bool Rash(ICommand c, string d) =>
        c is SecessionCommand or CaesarCommand or ProclaimImperiumCommand or SeizeThroneCommand or DeclareWarCommand or AssassinateCommand or SellVentureCommand or AbandonProjectCommand
            or PledgeCommand or LeaveFactionCommand or SetSuccessionLawCommand or TakeLoanCommand or SueForPeaceCommand or RecallExpeditionCommand or ClientCommand
        || c is WealthCommand && (Starts(d, WealthCommand.Sell) || Starts(d, WealthCommand.Give) || Starts(d, WealthCommand.BreakEntail));

    internal static Result Play(string contentRoot, ContentDatabase content, string roadId, string sid, ulong seed, int turns, Options o, Dictionary<string, int>? fired = null, Dictionary<string, int>? taken = null, HashSet<string>? flags = null)
    {
        var road = Roads.First(r => r.Id == roadId); var res = new Result { Road = road.Id, Label = $"{sid} seed {seed}" };
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(contentRoot, sid), seed);
        w.Difficulty = o.Difficulty; w.CrisisIntensity = o.Intensity; w.FamilyAutonomy = o.Autonomy;
        var tc = new TurnController(w, content); var decisions = new SplitMix64Rng(seed * 104729 + 31);
        for (int t = 0; t < turns && !w.IsDynastyDead; t++)
        {
            var briefing = tc.BeginTurn(); if (flags != null) foreach (var f in w.WorldFlags) flags.Add(f);   // the flags as the events see them
            foreach (var item in briefing)
            {
                string id = item.Fired.Def.Id; res.Fired[id] = res.Fired.GetValueOrDefault(id) + 1; if (fired != null) fired[id] = fired.GetValueOrDefault(id) + 1;
                var open = tc.Offer(item).Where(c => c.Available).ToList(); if (open.Count == 0) continue;
                var pick = open.FirstOrDefault(c => road.Prefers.Any(word => c.Choice.Id.Contains(word, StringComparison.Ordinal))) ?? open[decisions.NextInt(0, open.Count)];
                if (tc.Resolve(item, pick.Choice.Id) && taken != null) { string key = $"{id}/{pick.Choice.Id}"; taken[key] = taken.GetValueOrDefault(key) + 1; }
            }
            var ctx = new CommandContext(w, WorldBuilder.RngFor(w)); int done = 0;
            void Did(ICommand c) { string name = c.GetType().Name.Replace("Command", ""); res.Landed[name] = res.Landed.GetValueOrDefault(name) + 1; if (taken != null) taken["cmd/" + name] = taken.GetValueOrDefault("cmd/" + name) + 1; }
            foreach (var step in road.Wants)
            {
                if (done >= 6) break;
                // the world moves with each command given, so what is offered is read again for each step
                var wanted = Offered(tc, w, content).Where(x => !road.Never(x.c, x.d) && step.Match(x.c, x.d, w)).ToList(); if (wanted.Count == 0) continue;
                int landed = 0; string? first = null;
                foreach (var (c, _) in wanted)
                {
                    if (landed >= step.Most) break;
                    if (tc.Executor.TryExecute(c, ctx)) { landed++; done++; Did(c); } else first ??= Why(c, ctx, w);
                }
                if (landed == 0 && first != null) { if (!res.Refused.TryGetValue(step.Name, out var why)) res.Refused[step.Name] = why = new(); why[first] = why.GetValueOrDefault(first) + 1; }
            }
            // and one thing a house does anyway, so that it is kept as a house and not only driven down a road
            var rest = Offered(tc, w, content).Where(x => !road.Never(x.c, x.d) && !Rash(x.c, x.d) && !road.Wants.Any(s => s.Match(x.c, x.d, w))).ToList();
            for (int tries = 0; tries < 3 && rest.Count > 0; tries++) { int i = decisions.NextInt(0, rest.Count); var c = rest[i].c; rest.RemoveAt(i); if (tc.Executor.TryExecute(c, ctx)) { Did(c); break; } }
            tc.EndTurn();
            foreach (var m in road.Milestones) if (!res.ReachedIn.ContainsKey(m.Name) && m.Reached(w, tc)) res.ReachedIn[m.Name] = w.Turn;
        }
        res.Years = w.Turn; res.LineEnded = w.IsDynastyDead; res.World = w; return res;
    }

    // ---------------- the report ----------------

    public static int Run(Options o)
    {
        string root = ContentLocator.FindContentDir(); var content = ContentLoader.LoadFromDirectory(root);
        var scenarios = o.Scenario is "academy_crucible" or "all" ? Starts(root) : new List<string> { o.Scenario };
        int seeds = Math.Max(1, Math.Min(o.Seeds, 10)), turns = o.Turns > 6 ? o.Turns : 60; int failures = 0;
        Console.WriteLine($"=== Players with an end in view: {Roads.Length} roads × {scenarios.Count} start(s) × {seeds} seed(s) × {turns} years ===");
        var everything = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var road in Roads)
        {
            var runs = new List<Result>();
            foreach (var sid in scenarios) for (ulong seed = 1; seed <= (ulong)seeds; seed++)
            {
                try { runs.Add(Play(root, content, road.Id, sid, seed, turns, o, everything)); }
                catch (Exception ex) { failures++; Console.WriteLine($"  FAILED {road.Id} on {sid} seed {seed}: {ex.GetType().Name}: {ex.Message}"); }
            }
            Console.WriteLine(); Console.WriteLine($"== the {road.Id}: {road.Aim}");
            Console.WriteLine($"   {runs.Count} games · the line ended in {runs.Count(r => r.LineEnded)}");
            Console.WriteLine("   how far it got:");
            foreach (var m in road.Milestones)
            {
                var years = runs.Where(r => r.ReachedIn.ContainsKey(m.Name)).Select(r => r.ReachedIn[m.Name]).OrderBy(y => y).ToList();
                Console.WriteLine($"     {years.Count,3} of {runs.Count} · {m.Name}{(years.Count > 0 ? $" (by year {years[years.Count / 2]} in the middle game of them)" : "")}");
            }
            Console.WriteLine("   what it wanted, and what stood in the way (years refused, over all games):");
            foreach (var step in road.Wants)
            {
                var why = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var r in runs) if (r.Refused.TryGetValue(step.Name, out var d)) foreach (var kv in d) why[kv.Key] = why.GetValueOrDefault(kv.Key) + kv.Value;
                if (why.Count == 0) continue;
                Console.WriteLine($"     {step.Name}:");
                foreach (var kv in why.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(3)) Console.WriteLine($"       {kv.Value,5} · {kv.Key}");
            }
            var commands = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var r in runs) foreach (var kv in r.Landed) commands[kv.Key] = commands.GetValueOrDefault(kv.Key) + kv.Value;
            Console.WriteLine($"   commands that landed ({commands.Count} kinds): " + string.Join(" · ", commands.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key} ×{kv.Value}")));
            var events = new HashSet<string>(runs.SelectMany(r => r.Fired.Keys), StringComparer.Ordinal);
            Console.WriteLine($"   events fired: {events.Count} distinct");
        }
        Console.WriteLine(); Console.WriteLine($"-- Events fired by any of the three: {everything.Count} of {content.Events.Count}");
        Console.WriteLine("-- " + string.Join(" ", everything.Keys.OrderBy(k => k, StringComparer.Ordinal)));
        Console.WriteLine(); Console.WriteLine($"-- FAILURES: {failures}");
        return failures == 0 ? 0 : 1;
    }
    /// <summary>The starts a player may choose, in id order.</summary>
    internal static List<string> Starts(string root) =>
        Directory.EnumerateFiles(Path.Combine(root, "scenarios"), "*.json").Select(Path.GetFileNameWithoutExtension).Select(s => s!).OrderBy(s => s, StringComparer.Ordinal)
            .Where(s => !ScenarioLoader.LoadFromContent(root, s).Hidden).ToList();
}
