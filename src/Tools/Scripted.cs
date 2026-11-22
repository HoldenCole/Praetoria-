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
    /// <summary>A thing a player wants done this year. <c>Score</c>, where given, is how good each offer of it is: the best is tried
    /// first and one that scores nothing or less is not tried at all (a careful player reads the prospectus before founding).</summary>
    private sealed record Step(string Name, Func<ICommand, string, World, bool> Match, int Most = 1, Func<ICommand, string, World, TurnController, int>? Score = null);
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
        public string Road = "", Label = ""; public int Years; public bool LineEnded; public World? World; public TurnController? Controller;
        public Dictionary<string, int> ReachedIn = new();                       // milestone → the year it was first true
        public Dictionary<string, int> Landed = new();                          // command type → times it succeeded
        public Dictionary<string, Dictionary<string, int>> Refused = new();     // wanted step → reason → years
        public Dictionary<string, int> Fired = new();
        public List<(int year, int legitimacy, int credits, int rank, int career)> Trace = new();   // the player's house, year by year
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

    /// <summary>The upstart: a house nobody has heard of, with nothing, that wants to be a great one (docs/UPSTART.md). It tries every
    /// way up the game has, in the order a careful climber would: a seat, a patron, an income, a wife or a husband, land, rank, office.</summary>
    private static readonly Road Upstart = new()
    {
        Id = "upstart", Aim = "from nothing: an income, a patron, a seat, a marriage, land, rank and office",
        Wants = new[]
        {
            new Step("enrol in the Senate by the census", (c, _, _) => c is EnrollCommand),
            new Step("rise a rank", (c, _, _) => c is PetitionTitleCommand or ClaimTitleCommand),
            new Step("petition for land", (c, _, _) => c is PetitionFiefCommand),
            new Step("seek a patron", (c, d, w) => c is ClientCommand && Starts(d, ClientCommand.Seek) && MyHouse(w, out var h) && h.PatronId.Length == 0),
            new Step("sell what the ventures bring up", (c, d, w) => c is SellGoodCommand && MyHouse(w, out var h) && Surplus(h, d), 3),
            new Step("found a venture", (c, _, w) => c is FoundBusinessCommand && MyHouse(w, out var h) && w.BusinessesOf(h.Id).Count() < 4, 1, Prospect),
            new Step("stake a claim", (c, _, w) => c is StakeClaimCommand && MyHouse(w, out var h) && w.HoldingsOf(h.Id).Count(x => x.IsClaim) < 2),
            new Step("seek a match", (c, _, _) => c is SeekMatchCommand),
            new Step("press a suit", (c, _, _) => c is CourtCommand or WooCommand or SettleSuitCommand or MarriageCommand or ProposeMarriageCommand),
            new Step("stand for an office", (c, d, _) => c is StandForConsulCommand || c is LadderCommand && Starts(d, "stand")),
            new Step("do the year's duty", (c, _, _) => c is DutyCommand or CommitteeCommand, 3),
            new Step("ask a patron or a bench for coin or backing", (c, _, _) => c is AskAboveCommand),
            new Step("join a bench", (c, _, w) => c is JoinFactionCommand && MyHouse(w, out var h) && FactionSystem.Of(w, h.Id) == null),
            new Step("study", (c, _, _) => c is TrainCommand),
            new Step("seek a mentor", (c, d, _) => c is MentorCommand && Starts(d, "seek")),
            new Step("grow the venture", (c, _, _) => c is ExpandBusinessCommand or InvestCommand),
            new Step("build", (c, _, _) => c is BuildCommand or BuildWorkCommand or BuildInProvinceCommand or ImproveProvinceCommand),
            new Step("send a colony", (c, _, _) => c is FoundColonyCommand),
            new Step("raise ships", (c, _, w) => c is RaiseFleetCommand && MyHouse(w, out var h) && w.HoldingsOf(h.Id).Any(x => x.IsPlanet) && FleetSystem.StrengthOf(w, h.Id) < 4),
            new Step("canvass", (c, _, _) => c is CanvassCommand, 1, (_, _, w, _) => MyHouse(w, out var h) && h.Treasury.Credits >= 60 ? 1 : 0),   // not with the house's last coins
            new Step("vote", (c, _, _) => c is VoteCommand),
        },
        Never = (c, d) => NoBreakaway(c, d) || NoMarch(c, d) || NoThrone(c, d) || c is DeclareWarCommand || c is LeaveFactionCommand || c is AssassinateCommand
            || c is ClientCommand && !Starts(d, ClientCommand.Seek) || c is AmbitionCommand && Starts(d, AmbitionCommand.Refuse) || c is MentorCommand && Starts(d, "leave"),
        Prefers = new[] { "accept", "thank", "agree", "work", "study", "serve", "take", "stand", "pay" },
        Milestones = new[]
        {
            new Milestone("an income of its own: a venture or a claim", (w, _) => MyHouse(w, out var h) && (w.BusinessesOf(h.Id).Any() || w.HoldingsOf(h.Id).Any())),
            new Milestone("a patron", (w, _) => MyHouse(w, out var h) && h.PatronId.Length > 0),
            new Milestone("the head is wed", (w, _) => w.Protagonist is { } p && w.Marriages.Any(m => m.Has(p.Id))),
            new Milestone("a child of the house is born", (w, _) => MyHouse(w, out var h) && w.Protagonist is { } p && w.LivingCharacters().Any(c => c.HouseId == h.Id && (c.FatherId == p.Id || c.MotherId == p.Id))),
            new Milestone("a seat in the Senate", (w, tc) => MyHouse(w, out var h) && tc.Senate.Door(w, h) != null),
            new Milestone("one of the house holds a magistracy", (w, _) => MyHouse(w, out var h) && w.Magistrates.Any(m => w.Char(m.CharacterId)?.HouseId == h.Id)),
            new Milestone("a province held under another house", (w, _) => MyHouse(w, out var h) && w.Holdings.Values.Any(x => x.IsPlanet && x.OwnerId != h.Id && x.Provinces.Any(pv => pv.HolderId == h.Id))),
            new Milestone("a world of its own", (w, _) => MyHouse(w, out var h) && w.HoldingsOf(h.Id).Any(x => x.IsPlanet)),
            new Milestone("the first rank (Warden, or knight)", (w, _) => MyHouse(w, out var h) && Praetoria.Core.Progression.Title.RankOf(h.Title) >= 1),
            new Milestone("the second rank (Prefect, or baron)", (w, _) => MyHouse(w, out var h) && Praetoria.Core.Progression.Title.RankOf(h.Title) >= 2),
            new Milestone("the third rank (Legate, or count)", (w, _) => MyHouse(w, out var h) && Praetoria.Core.Progression.Title.RankOf(h.Title) >= 3),
            new Milestone("among the richer half of the houses", (w, _) => MyHouse(w, out var h) && w.Houses.Values.Count(o => !World.Outsider(o.Id) && o.Wealth > h.Wealth) < w.Houses.Values.Count(o => !World.Outsider(o.Id)) / 2),
            new Milestone("the seat has passed to an heir", (w, _) => w.Counter("generation") >= 1),
        },
    };

    /// <summary>What a venture would earn this house a year where it is offered, in credits' worth: the figure the prospectus shows.</summary>
    private static int Prospect(ICommand c, string d, World w, TurnController tc)
    {
        var parts = d.Split(' '); if (parts.Length < 4 || tc.Ventures == null || !MyHouse(w, out var h)) return 0;   // "found <kind> in <system>"
        return tc.Ventures.Earnings(w, new Business { Id = "prospect", OwnerId = h.Id, Kind = parts[1], SystemId = parts[3], Level = 1 });
    }
    /// <summary>A good the house holds more of than it has a use for: what a landless house's ventures pay it in.</summary>
    private static bool Surplus(House h, string described)
    {
        foreach (var (good, keep) in new[] { (Resource.Materials, 12), (Resource.Exotics, 1), (Resource.Helium3, 2), (Resource.RareEarths, 1), (Resource.Isotopes, 1) })
            if (described.EndsWith(" " + Resource.Label(good), StringComparison.OrdinalIgnoreCase)) return h.Treasury.Get(good) > keep;
        return false;
    }

    private static readonly Road[] Roads = { General, Seceder, Courtier, Upstart };
    /// <summary>The roads the nightly soak plays: the upstart has a command of its own (it starts from a character, not a scenario).</summary>
    private static readonly Road[] SoakRoads = { General, Seceder, Courtier };
    /// <summary>The last thing on a road: what "it got there" means in one line of the soak.</summary>
    internal static string[] MilestonesOf(string roadId) => Roads.First(r => r.Id == roadId).Milestones.Select(m => m.Name).ToArray();
    internal static readonly string[] RoadIds = SoakRoads.Select(r => r.Id).ToArray();

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

    internal static Result Play(string contentRoot, ContentDatabase content, string roadId, string sid, ulong seed, int turns, Options o, Dictionary<string, int>? fired = null, Dictionary<string, int>? taken = null, HashSet<string>? flags = null, Praetoria.Core.Creation.CustomCharacter? custom = null)
    {
        var road = Roads.First(r => r.Id == roadId); var res = new Result { Road = road.Id, Label = $"{sid} seed {seed}" };
        if (custom != null) sid = Praetoria.Core.Creation.CharacterWorkshop.BaseFor(custom);
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(contentRoot, sid), seed);
        w.Difficulty = o.Difficulty; w.CrisisIntensity = o.Intensity; w.FamilyAutonomy = o.Autonomy; Praetoria.Core.Galaxy.GalaxyGrowth.Grow(w, content, o.Size);
        if (custom != null) Praetoria.Core.Creation.CharacterWorkshop.Apply(w, content, custom);
        var tc = new TurnController(w, content); var decisions = new SplitMix64Rng(seed * 104729 + 31);
        if (custom != null) Praetoria.Core.Creation.CharacterWorkshop.Finish(tc, custom);
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
            void Did(ICommand c) { string name = c.GetType().Name.Replace("Command", ""); if (c is DutyCommand) name += ":" + c.Describe().Split(' ')[0]; res.Landed[name] = res.Landed.GetValueOrDefault(name) + 1; if (taken != null) taken["cmd/" + name] = taken.GetValueOrDefault("cmd/" + name) + 1; }
            foreach (var step in road.Wants)
            {
                if (done >= 6) break;
                // the world moves with each command given, so what is offered is read again for each step
                var wanted = Offered(tc, w, content).Where(x => !road.Never(x.c, x.d) && step.Match(x.c, x.d, w)).ToList();
                if (step.Score != null) wanted = wanted.Select(x => (x, s: step.Score(x.c, x.d, w, tc))).Where(x => x.s > 0).OrderByDescending(x => x.s).Select(x => x.x).ToList();
                if (wanted.Count == 0) continue;
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
            if (MyHouse(w, out var mine)) res.Trace.Add((w.Turn, mine.Legitimacy, mine.Treasury.Credits, Praetoria.Core.Progression.Title.RankOf(mine.Title), w.Counter("generation") == 0 ? w.Protagonist?.CareerRank ?? 0 : -1));
        }
        res.Years = w.Turn; res.LineEnded = w.IsDynastyDead; res.World = w; res.Controller = tc; return res;
    }

    // ---------------- the report ----------------

    public static int Run(Options o)
    {
        string root = ContentLocator.FindContentDir(); var content = ContentLoader.LoadFromDirectory(root);
        var scenarios = o.Scenario is "academy_crucible" or "all" ? Starts(root) : new List<string> { o.Scenario };
        int seeds = Math.Max(1, Math.Min(o.Seeds, 10)), turns = o.Turns > 6 ? o.Turns : 60; int failures = 0;
        Console.WriteLine($"=== Players with an end in view: {SoakRoads.Length} roads × {Counted.Starts(scenarios.Count)} × {Counted.Seeds(seeds)} × {turns} years ===");
        var everything = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var road in SoakRoads)
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
    // ---------------- the upstart: a new house from nothing (docs/UPSTART.md) ----------------

    /// <summary>A house nobody has heard of: a new name, no land, no seat, no patron, no family, a few coins, and one person of twenty-two
    /// at the bottom of a service. Everything the creator leaves at its least.</summary>
    internal static Praetoria.Core.Creation.CustomCharacter Nothing(string track, string name = "Tam Corvane") => new()
    {
        Mode = Praetoria.Core.Creation.CustomCharacter.Standard, Name = name, Sex = "male", Age = 22, Track = track, Rank = 0, Origin = "new", Title = "landless", Legitimacy = 5,
        HouseName = name.Split(' ')[^1], Skills = { [track == "military" ? "tactics" : track == "law" ? "law" : "administration"] = 2 },
        Treasury = new() { ["credits"] = 20, ["materials"] = 6, ["manpower"] = 4, ["influence"] = 1 }
    };

    public static int RunUpstart(Options o)
    {
        string root = ContentLocator.FindContentDir(); var content = ContentLoader.LoadFromDirectory(root);
        int seeds = Math.Max(1, Math.Min(o.Seeds, 10)), turns = o.Turns > 6 ? o.Turns : 60; int failures = 0; var road = Upstart;
        var tracks = new[] { "military", "stewardship", "law" };
        Console.WriteLine($"=== The upstart: a new house from nothing · {tracks.Length} services × {Counted.Seeds(seeds)} × {turns} years ===");
        var spec0 = Nothing("military"); var (cost, _) = Praetoria.Core.Creation.CharacterWorkshop.Cost(spec0, content);
        Console.WriteLine($"    the start: landless, a new name, legitimacy {spec0.Legitimacy}, {Praetoria.Core.Money.Credits(spec0.Treasury["credits"])}, no seat, no patron, no family · {cost} of {Praetoria.Core.Creation.WorkshopLimits.Standard.Budget} points");
        var runs = new List<(string track, Result r)>();
        foreach (var track in tracks) for (ulong seed = 1; seed <= (ulong)seeds; seed++)
        {
            try { var r = Play(root, content, road.Id, "", seed, turns, o, custom: Nothing(track)); r.Label = $"{track} seed {seed}"; runs.Add((track, r)); }
            catch (Exception ex) { failures++; Console.WriteLine($"  FAILED {track} seed {seed}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace?.Split('\n').FirstOrDefault()}"); }
        }
        Console.WriteLine(); Console.WriteLine($"   {runs.Count} games · the line ended in {runs.Count(x => x.r.LineEnded)}");
        Console.WriteLine("   how far it got (games of all, and the year in the middle game of those):");
        foreach (var m in road.Milestones)
        {
            var years = runs.Where(x => x.r.ReachedIn.ContainsKey(m.Name)).Select(x => x.r.ReachedIn[m.Name]).OrderBy(y => y).ToList();
            string by = string.Join(" ", tracks.Select(t => $"{t[..3]} {runs.Count(x => x.track == t && x.r.ReachedIn.ContainsKey(m.Name))}/{runs.Count(x => x.track == t)}"));
            Console.WriteLine($"     {years.Count,3} of {runs.Count} · {m.Name}{(years.Count > 0 ? $" (year {years[years.Count / 2]})" : "")}   [{by}]");
        }
        Console.WriteLine("   what it wanted, and what stood in the way (years refused, over all games):");
        foreach (var step in road.Wants)
        {
            var why = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (_, r) in runs) if (r.Refused.TryGetValue(step.Name, out var d)) foreach (var kv in d) why[kv.Key] = why.GetValueOrDefault(kv.Key) + kv.Value;
            if (why.Count == 0) continue;
            Console.WriteLine($"     {step.Name}:");
            foreach (var kv in why.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(4)) Console.WriteLine($"       {kv.Value,5} · {kv.Key}");
        }
        var commands = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (_, r) in runs) foreach (var kv in r.Landed) commands[kv.Key] = commands.GetValueOrDefault(kv.Key) + kv.Value;
        Console.WriteLine($"   commands that landed ({commands.Count} kinds): " + string.Join(" · ", commands.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key} ×{kv.Value}")));
        Console.WriteLine(); Console.WriteLine("   where each ended:");
        foreach (var (track, r) in runs)
        {
            var w = r.World!; var p = w.Protagonist; var h = p != null ? w.House(p.HouseId) : null; if (h == null || p == null) { Console.WriteLine($"     {r.Label}: the line ended in year {r.Years}"); continue; }
            int richer = w.Houses.Values.Count(x => !World.Outsider(x.Id) && x.Wealth > h.Wealth), houses = w.Houses.Values.Count(x => !World.Outsider(x.Id));
            Console.WriteLine($"     {r.Label,-22} year {r.Years} · {content.Titles.NameOf(h.Title, w.Era)} · {p.Name}, {p.Age}, rank {p.CareerRank} · worlds {w.HoldingsOf(h.Id).Count(x => x.IsPlanet)} · provinces {w.Holdings.Values.Sum(x => x.IsPlanet && x.OwnerId != h.Id ? x.Provinces.Count(pv => pv.HolderId == h.Id) : 0)} · ventures {w.BusinessesOf(h.Id).Count()} · {Praetoria.Core.Money.Held(h.Treasury)} credits · wealth {richer + 1} of {houses} · legitimacy {h.Legitimacy} · of the house alive {w.LivingCharacters().Count(c => c.HouseId == h.Id)} · {(h.PatronId.Length > 0 ? "client of " + (w.House(h.PatronId)?.Name ?? "?") : "no patron")} · head {w.Counter("generation") + 1}{(r.LineEnded ? " · THE LINE ENDED" : "")}");
        }
        // what a first venture pays: every kind a house with no world and no rank may found, on every world it could found it on
        if (runs.FirstOrDefault().r?.Controller is { } tc0)
        {
            var w0 = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, Praetoria.Core.Creation.CharacterWorkshop.BaseFor(spec0)), 1); w0.Difficulty = o.Difficulty; Praetoria.Core.Galaxy.GalaxyGrowth.Grow(w0, content, o.Size);
            Praetoria.Core.Creation.CharacterWorkshop.Apply(w0, content, spec0); var tcv = new TurnController(w0, content); Praetoria.Core.Creation.CharacterWorkshop.Finish(tcv, spec0);
            var ctx0 = new CommandContext(w0, WorldBuilder.RngFor(w0)); MyHouse(w0, out var h0); h0.Treasury.Credits = 500; h0.Treasury.Materials = 500; h0.Treasury.Manpower = 500; h0.Treasury.Influence = 50;
            Console.WriteLine(); Console.WriteLine("   what a first venture pays a house with no world (credits' worth a year, on every world it may be founded on; year 0, seed 1):");
            foreach (var def in content.Businesses.Defs.OrderBy(d => d.Id, StringComparer.Ordinal))
            {
                var pays = new List<(int worth, string where)>(); string? never = null;
                foreach (var sys in w0.Galaxy!.Systems.Values.OrderBy(s => s.Id, StringComparer.Ordinal))
                {
                    string? no = new FoundBusinessCommand(w0.ProtagonistId, def.Id, sys.Id, content.Businesses, content.Holdings, content.Titles).Refusal(ctx0); if (no != null) { never ??= no; continue; }
                    var ghost = new Business { Id = "ghost", OwnerId = h0.Id, Kind = def.Id, SystemId = sys.Id, Level = 1 };
                    pays.Add((tcv.Ventures!.Earnings(w0, ghost), $"{sys.Name} ({w0.Galaxy.RingOf(sys.Id)})"));
                }
                if (pays.Count == 0) { Console.WriteLine($"     {def.Name,-22} nowhere: {never}"); continue; }
                pays.Sort((a, b) => b.worth.CompareTo(a.worth));
                Console.WriteLine($"     {def.Name,-22} costs {Praetoria.Core.Presentation.CourtProjection.Bundle(def.Cost)} · {pays.Count} worlds · pays nothing or less on {pays.Count(x => x.worth <= 0)} · best {Praetoria.Core.Money.Show(pays[0].worth)} at {pays[0].where} · middle {Praetoria.Core.Money.Show(pays[pays.Count / 2].worth)} · worst {Praetoria.Core.Money.Show(pays[^1].worth)}");
            }
        }
        Console.WriteLine(); Console.WriteLine("   the middle game by year (legitimacy · credits in hand · the house's rank):");
        foreach (int y in new[] { 1, 3, 5, 10, 15, 20, 30, 40, 50, 60 }.Where(y => y <= turns))
        {
            var at = runs.Select(x => x.r.Trace.LastOrDefault(t => t.year <= y)).Where(t => t.year > 0).ToList(); if (at.Count == 0) continue;
            int Mid(Func<(int year, int legitimacy, int credits, int rank, int career), int> f) => at.Select(f).OrderBy(v => v).ElementAt(at.Count / 2);
            Console.WriteLine($"     year {y,2}: legitimacy {Mid(t => t.legitimacy),3} ({at.Min(t => t.legitimacy)} to {at.Max(t => t.legitimacy)}) · credits {Praetoria.Core.Money.Show(Mid(t => t.credits)),6} ({Praetoria.Core.Money.Show(at.Min(t => t.credits))} to {Praetoria.Core.Money.Show(at.Max(t => t.credits))}) · rank {Mid(t => t.rank)}{(at.Any(t => t.career >= 0) ? $" · the founder's step in the service {at.Where(t => t.career >= 0).Select(t => t.career).OrderBy(v => v).ElementAt(at.Count(t => t.career >= 0) / 2)} ({at.Where(t => t.career >= 0).Min(t => t.career)} to {at.Where(t => t.career >= 0).Max(t => t.career)})" : "")}");
        }
        // one house's own story, as its chronicle tells it
        var told = runs.Where(x => !x.r.LineEnded).OrderByDescending(x => x.r.ReachedIn.Count).ThenBy(x => x.r.Label, StringComparer.Ordinal).FirstOrDefault();
        if (told.r?.World is { } tw)
        {
            string surname = Nothing(told.track).HouseName;
            Console.WriteLine(); Console.WriteLine($"   the story of one of them ({told.r.Label}), as the chronicle has it:");
            foreach (var m in told.r.ReachedIn.OrderBy(kv => kv.Value)) Console.WriteLine($"     year {m.Value,2}: {m.Key}");
            Console.WriteLine("     ---");
            foreach (var e in tw.History.Where(e => e.Text.Contains(surname, StringComparison.Ordinal)).GroupBy(e => e.Text).Select(g => g.First()).Take(70)) Console.WriteLine($"     y{e.Turn,2}  {e.Text}");
        }
        Console.WriteLine(); Console.WriteLine($"-- FAILURES: {failures}");
        return failures == 0 ? 0 : 1;
    }

    /// <summary>The starts a player may choose, in id order.</summary>
    internal static List<string> Starts(string root) =>
        Directory.EnumerateFiles(Path.Combine(root, "scenarios"), "*.json").Select(Path.GetFileNameWithoutExtension).Select(s => s!).OrderBy(s => s, StringComparer.Ordinal)
            .Where(s => !ScenarioLoader.LoadFromContent(root, s).Hidden).ToList();
}
