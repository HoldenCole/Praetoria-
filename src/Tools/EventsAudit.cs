using System.Text;
using System.Text.RegularExpressions;
using Praetoria.Core;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Presentation;
using Praetoria.Core.Rng;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Tools;

/// <summary>
/// The events audit (docs/TOOLS.md, "The events audit"; docs/EVENTS.md, "Waiting times"): plays each start for a number of years with a
/// stand-in player who answers every report with an answer that is open to them (and, with --act, gives the soak's random commands too),
/// and reports, per event, how often it came, how many of those were answered, how many heads it came to and the shortest gap between two
/// comings to the same head; per start, the share of distinct events among those answered; and every misfire it can see from the binding:
/// a report that speaks of hulls, worlds, ventures or shares the house does not have, of a grandfather of a new name, of a spouse the head
/// does not have; a role named for an office bound to someone who does not hold it; a step up the ladder offered to one at its top; a
/// card shown with fewer than two answers open; a card that came to the same head within its waiting time; the known misfires of the
/// review of 2026-11-28 by name. Exit code 1 when a card came back within its waiting time or a known misfire was seen.
/// </summary>
internal static class EventsAudit
{
    /// <summary>The years a card waits before it comes again to a head it has come to <paramref name="times"/> times (the engine's rule).</summary>
    private static int WaitOf(EventDef def) => def.WaitYears;
    private static int WaitAfter(EventDef def, int times) => EventEngine.WaitAfter(def, times);

    private sealed class Shown
    {
        public string EventId = ""; public string Head = ""; public int Turn; public bool Academy; public bool Answered; public int Open; public string Version = "";
    }
    private sealed class Flag { public string Kind = ""; public string EventId = ""; public string Example = ""; }

    private sealed class Game
    {
        public string Start = ""; public int Seed; public int Years; public bool LineEnded;
        public List<Shown> Cards = new(); public List<Flag> Flags = new(); public Dictionary<string, int> Held = new();
    }

    public static int Run(Options o)
    {
        var contentRoot = ContentLocator.FindContentDir();
        var content = ContentLoader.LoadFromDirectory(contentRoot);
        var starts = o.Scenario is "academy_crucible" or "all" ? Scripted.Starts(contentRoot) : new List<string> { o.Scenario };
        int years = o.Years > 0 ? o.Years : 20, seeds = Math.Max(1, o.Seeds);

        Console.WriteLine($"=== Events audit: {Counted.Scenarios(starts.Count)} × {Counted.Seeds(seeds)} × {years} years, a stand-in player who answers every report{(o.Act ? " and gives the soak's commands" : "")} ===");
        Console.WriteLine($"Default waiting time for a repeatable card: {EventDef.DefaultCooldown} years.\n");

        var games = new List<Game>();
        foreach (var sid in starts)
            for (int seed = 1; seed <= seeds; seed++)
                games.Add(Play(contentRoot, content, sid, seed, years, o));

        // ---- per start ----
        var table = new StringBuilder();
        table.AppendLine($"{"Start",-20} {"Shown",6} {"Answered",8} {"Distinct",8} {"Share",6} {"Back too soon",13} {"Shortest gap",12} {"Misfires",8} {"One answer",10}");
        int allAnswered = 0, allDistinct = 0, allSoon = 0, allMisfires = 0, allOne = 0;
        foreach (var g in games.GroupBy(x => x.Start))
        {
            int shown = g.Sum(x => x.Cards.Count), answered = g.Sum(x => x.Cards.Count(c => c.Answered));
            int distinct = g.Sum(x => x.Cards.Where(c => c.Answered).Select(c => c.EventId).Distinct().Count());
            var soon = g.SelectMany(TooSoon).ToList(); int gap = g.SelectMany(Gaps).Select(x => x.gap).DefaultIfEmpty(-1).Min();
            int misfires = g.Sum(x => x.Flags.Count(f => f.Kind != OneAnswer && f.Kind != BackTooSoon)), one = g.Sum(x => x.Flags.Count(f => f.Kind == OneAnswer));
            allAnswered += answered; allDistinct += distinct; allSoon += soon.Count; allMisfires += misfires; allOne += one;
            table.AppendLine($"{g.Key,-20} {shown,6} {answered,8} {distinct,8} {Pct(distinct, answered),6} {soon.Count,13} {(gap < 0 ? "none" : Counted.Years(gap)),12} {misfires,8} {one,10}");
        }
        table.AppendLine($"{"All",-20} {games.Sum(x => x.Cards.Count),6} {allAnswered,8} {allDistinct,8} {Pct(allDistinct, allAnswered),6} {allSoon,13} {"",12} {allMisfires,8} {allOne,10}");
        Console.WriteLine("Per start (distinct: different events among those answered in one game, summed over the games; share: distinct of answered):");
        Console.WriteLine(table.ToString());
        var ended = games.Where(x => x.LineEnded).Select(x => $"{x.Start} seed {x.Seed} (year {x.Years})").ToList();
        if (ended.Count > 0) Console.WriteLine("Lines that ended before the last year: " + string.Join(", ", ended) + "\n");

        // ---- per event ----
        var byEvent = games.SelectMany(x => x.Cards.Select(c => (x, c))).GroupBy(t => t.c.EventId).Select(e =>
        {
            var def = content.Event(e.Key)!;
            var gaps = games.SelectMany(x => Gaps(x).Where(y => y.eventId == e.Key)).Select(y => y.gap).ToList();
            return (id: e.Key, def, shown: e.Count(), answered: e.Count(t => t.c.Answered), heads: e.Select(t => t.x.Start + t.x.Seed + "|" + t.c.Head).Distinct().Count(),
                gap: gaps.Count == 0 ? -1 : gaps.Min(), versions: e.Select(t => t.c.Version).Distinct().Count());
        }).OrderByDescending(x => x.shown).ThenBy(x => x.id, StringComparer.Ordinal).ToList();
        Section($"Every event that came, most often first (came · answered · heads it came to · shortest gap for one head · its waiting time · versions of its words read)",
            byEvent.Select(x => $"{x.id,-44} {x.shown,4} · {x.answered,4} · {x.heads,3} · {(x.gap < 0 ? "-" : x.gap.ToString()),3} · {(x.def.Repeatable ? WaitOf(x.def).ToString() : "once"),4} · {x.versions}"));
        Section("Came back to the same head within its waiting time", games.SelectMany(TooSoon).GroupBy(s => s).OrderByDescending(gr => gr.Count()).Select(gr => $"{gr.Key} ×{gr.Count()}"));

        // ---- misfires ----
        var flags = games.SelectMany(x => x.Flags.Select(f => (x, f))).ToList();
        foreach (var kind in Kinds)
        {
            var these = flags.Where(t => t.f.Kind == kind.id).ToList();
            Section($"{kind.what} [{kind.id}]", these.GroupBy(t => t.f.EventId).OrderByDescending(gr => gr.Count()).ThenBy(gr => gr.Key, StringComparer.Ordinal)
                .Select(gr => $"{gr.Key} ×{gr.Count()} (e.g. {gr.First().x.Start} seed {gr.First().x.Seed}: {gr.First().f.Example})"));
        }
        var held = games.SelectMany(x => x.Held).GroupBy(kv => kv.Key).Select(gr => (id: gr.Key, n: gr.Sum(kv => kv.Value))).OrderByDescending(x => x.n).ThenBy(x => x.id, StringComparer.Ordinal).ToList();
        Section("Held back at the draw because fewer than two of its answers were open (times)", held.Select(x => $"{x.id} ×{x.n}"));

        var known = flags.Where(t => Known.Contains(t.f.Kind)).ToList();
        int soonAll = games.Sum(x => TooSoon(x).Count());
        Console.WriteLine($"\nKnown misfires seen: {known.Count}. Cards back within their waiting time: {soonAll}. Misfires of every kind: {flags.Count(t => t.f.Kind is not OneAnswer and not BackTooSoon)}. Shown with one answer open: {allOne}.");
        Console.WriteLine(known.Count == 0 && soonAll == 0 ? "=== EVENTS AUDIT PASSED ===" : "=== EVENTS AUDIT FAILED ===");
        return known.Count == 0 && soonAll == 0 ? 0 : 1;
    }

    private static string Pct(int a, int b) => b == 0 ? "-" : $"{100.0 * a / b:0.0}%";

    private static void Section(string title, IEnumerable<string> items)
    {
        var list = items.ToList();
        Console.WriteLine($"\n-- {title}: {list.Count}");
        foreach (var s in list) Console.WriteLine("   " + s);
    }

    /// <summary>Every two comings of one event to one head in the world (not the Academy, where the year does not turn), with the years between.</summary>
    private static IEnumerable<(string eventId, int gap)> Gaps(Game g) =>
        g.Cards.Where(c => !c.Academy).GroupBy(c => (c.EventId, c.Head)).SelectMany(gr =>
        {
            var turns = gr.Select(c => c.Turn).OrderBy(t => t).ToList();
            return Enumerable.Range(1, Math.Max(0, turns.Count - 1)).Select(i => (gr.Key.EventId, turns[i] - turns[i - 1]));
        });

    private static IEnumerable<string> TooSoon(Game g) => g.Cards.Count == 0 ? Enumerable.Empty<string>() :
        g.Flags.Where(f => f.Kind == BackTooSoon).Select(f => f.EventId);

    // ------------------------------------------------------------------------------------------------ one game

    private static Game Play(string contentRoot, ContentDatabase content, string sid, int seed, int years, Options o)
    {
        var game = new Game { Start = sid, Seed = seed };
        var world = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(contentRoot, sid), (ulong)seed);
        world.Difficulty = o.Difficulty; world.CrisisIntensity = o.Intensity; world.FamilyAutonomy = o.Autonomy;
        Praetoria.Core.Galaxy.GalaxyGrowth.Grow(world, content, o.Size);
        var tc = new TurnController(world, content);
        tc.Engine.HeldForOneAnswer = (def, _) => game.Held[def.Id] = game.Held.GetValueOrDefault(def.Id) + 1;
        var decisions = new SplitMix64Rng((ulong)seed * 7919 + 101);
        var last = new Dictionary<(string, string), int>();   // (event, head) → the year it last came
        var times = new Dictionary<(string, string), int>();   // (event, head) → how often it has come
        var told = new HashSet<string>();
        var inTheYear = new HashSet<string>();   // the head's marriages made by an answer or an act in the year, not at its turn
        // a card that came to the same head sooner than its waiting time allows, after as many comings as it has had
        void Came(FiredEvent fired, string head)
        {
            if (world.InAcademy) return;
            var key = (fired.Def.Id, head); int n = times.GetValueOrDefault(key);
            if (fired.Def.Repeatable && last.TryGetValue(key, out int was) && world.Turn - was < WaitAfter(fired.Def, n))
                game.Flags.Add(new Flag { Kind = BackTooSoon, EventId = fired.Def.Id, Example = $"year {was} and again in year {world.Turn}, waiting time {Counted.Years(WaitAfter(fired.Def, n))}" });
            last[key] = world.Turn; times[key] = n + 1;
        }
        for (int guard = 0; guard < years + 40 && !world.IsDynastyDead && (world.Turn < years || world.InAcademy); guard++)
        {
            var briefing = tc.BeginTurn();
            if (!world.InAcademy && world.Turn > years) break;
            WorldChecks(world, content, game, told);
            BriefChecks(world, content, tc, game);
            foreach (var item in briefing.ToList())
            {
                var fired = item.Fired; string head = world.ProtagonistId;
                var shown = new Shown { EventId = fired.Def.Id, Head = head, Turn = world.Turn, Academy = world.InAcademy, Version = VersionOf(fired) };
                game.Cards.Add(shown);
                Came(fired, head);
                CardChecks(world, content, fired, game, inTheYear);
                var open = tc.Offer(item).Where(c => c.Available).ToList(); shown.Open = open.Count;
                if (open.Count < 2) game.Flags.Add(new Flag { Kind = OneAnswer, EventId = fired.Def.Id, Example = $"year {world.Turn}, {Counted.Of(open.Count, "answer")} open of {fired.Def.Choices.Count}" });
                if (open.Count == 0) continue;
                var pick = open[decisions.NextInt(0, open.Count)];
                int before = tc.Briefing.Count;
                if (tc.Resolve(item, pick.Choice.Id)) shown.Answered = true;
                NoteMarriage(world, inTheYear);
                // a report the answer brought on at once (a moment of the year: docs/EVENTS.md, "Moments in the year") is answered in its turn
                foreach (var extra in tc.Briefing.Skip(before).ToList()) briefing = briefing.Append(extra).ToList();
            }
            for (int i = 0; i < tc.Briefing.Count; i++)
            {
                // the reports a command or an answer brought on in the year: read and answered like the rest
                var item = tc.Briefing[i]; if (item.Resolved || game.Cards.Any(c => c.EventId == item.Fired.Def.Id && c.Turn == world.Turn && c.Head == world.ProtagonistId)) continue;
                var shown = new Shown { EventId = item.Fired.Def.Id, Head = world.ProtagonistId, Turn = world.Turn, Academy = world.InAcademy, Version = VersionOf(item.Fired) };
                game.Cards.Add(shown); Came(item.Fired, world.ProtagonistId); CardChecks(world, content, item.Fired, game, inTheYear);
                var open = tc.Offer(item).Where(c => c.Available).ToList(); shown.Open = open.Count;
                if (open.Count >= 1 && tc.Resolve(item, open[decisions.NextInt(0, open.Count)].Choice.Id)) shown.Answered = true;
                NoteMarriage(world, inTheYear);
            }
            if (o.Act) { Qa.Act(tc, world, content, decisions, null); NoteMarriage(world, inTheYear); }
            tc.EndTurn();
            game.Years = world.Turn;
        }
        game.LineEnded = world.IsDynastyDead;
        return game;
    }

    private static string VersionOf(FiredEvent f) => f.Version.ToString();
    private static string KeyOf(Marriage m) => m.A + "|" + m.B + "|" + m.Since;
    /// <summary>The head's marriage, if one was made in the year just now (by an answer or an act, not at the year's turn).</summary>
    private static void NoteMarriage(World w, HashSet<string> inTheYear) { if (w.Protagonist is { } p && w.MarriageOf(p.Id) is { } m && m.Since == w.Turn) inTheYear.Add(KeyOf(m)); }

    // ------------------------------------------------------------------------------------------------ the checks

    private const string OneAnswer = "one_answer", BackTooSoon = "back_too_soon";
    private static readonly (string id, string what)[] Kinds =
    {
        ("hulls", "Speaks of hulls, ships or the house's fleet to a house with none"),
        ("worlds", "Speaks of the house's worlds to a house with none"),
        ("ventures", "Speaks of the house's ventures to a house with none"),
        ("shares", "Speaks of shares on the Exchange held by the house or one of it, and nobody of the house holds any"),
        ("old_name", "Speaks of a grandfather or forebears of the house to a new name"),
        ("spouse", "Speaks of the head's spouse to a head who has none"),
        ("office", "A role named for an office bound to someone who does not hold it"),
        ("top_rank", "Offers a step up the ladder to one at its top"),
        ("field_promotion", "Known: The Empty Command for one past it (45 or older, Admiral or above, or in an office of the Reach)"),
        ("navy_grows", "Known: The Fleet Grows for a house with no hulls, or one below Commander"),
        ("sovereign", "Known: a sovereign who does not hold the throne, or the chair in the Republic"),
        ("inherited_debt", "Known: The Old Paper, a grandfather's debt, for a new name"),
        ("ambition", "Known: someone at the top of the navy, or commanding a fleet, who still wants to earn a command"),
        ("late_moment", "Known: the wedding or the naming a year after the moment, or after the year in brief told it as done"),
        (OneAnswer, "Shown with fewer than two answers open, as the stand-in came to it"),
        (BackTooSoon, "Came to the same head again within its waiting time"),
    };
    private static readonly HashSet<string> Known = new() { "field_promotion", "navy_grows", "sovereign", "inherited_debt", "ambition", "late_moment" };

    // what the house itself is said to have: "your fleet", "the house's worlds", "every hull of House Vane", "House Vane's ventures"; a nation's
    // hulls or a rival's worlds are not the house's ({house} is the house's name, put in when a card is read)
    private const string HullsOf = @"\byour (?:own )?(?:hulls?|fleets?|ships|squadrons?)\b|\bthe house's (?:hulls?|fleets?|ships|squadrons?)\b|\bevery hull of {house}\b|\b{house}'s (?:hulls?|fleets?|ships|squadrons?)\b";
    private const string WorldsOf = @"\byour (?:worlds?|planets?)\b|\bthe house's (?:worlds?|planets?|holdings?)\b|\b{house}'s (?:worlds?|planets?|holdings?)\b";
    private const string VenturesOf = @"\byour (?:ventures?|compan(?:y|ies))\b|\bthe house's (?:ventures?|compan(?:y|ies))\b|\b{house}'s (?:ventures?|compan(?:y|ies))\b";
    private static Regex Of(string pattern, House house) => new(pattern.Replace("{house}", Regex.Escape(house.Name)), RegexOptions.IgnoreCase);
    /// <summary>Cards whose words speak of a venture of the house that the card's own moment has just taken away: they are right to.</summary>
    private static readonly HashSet<string> VentureJustLost = new() { "a_venture_fails", "a_venture_is_sacked", "a_venture_is_foreclosed" };
    /// <summary>Cards about a battle the house's fleet has just lost, which may have left it with no hulls at all.</summary>
    private static readonly HashSet<string> FleetJustLost = new() { "the_line_breaks", "the_butchers_bill", "wounded_aboard_the_fleet" };
    private static readonly Regex Shares = new(@"\bshares\b[^.]*\bExchange\b|\bExchange\b[^.]*\bshares\b|\bShares of\b", RegexOptions.Compiled);
    private static readonly Regex OldName = new(@"\byour (?:grand(?:father|mother)|great-grand\w+|forebears?|ancestors?)\b|\bgrandfather signed\b|\byour house's (?:ancestors|forebears)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Spouse = new(@"\byour (?:wife|husband|spouse)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>The words of a card as the player reads them: its title, its body and every answer, in the version drawn.</summary>
    private static string Words(FiredEvent fired, ContentDatabase content, World world)
    {
        var text = content.Text(fired.Def.Id); if (text == null) return "";
        var ctx = new EvalContext(world, fired.Binding, new SplitMix64Rng(0), content);
        var sb = new StringBuilder();
        sb.Append(TextRenderer.Substitute(text.TitleOf(fired.Version) ?? "", ctx)).Append(". ").Append(TextRenderer.Substitute(text.BodyOf(fired.Version), ctx));
        foreach (var c in fired.Def.Choices) if (text.ChoiceOf(c.Id, fired.Version) is { } words) sb.Append(' ').Append(TextRenderer.Substitute(words, ctx));
        return sb.ToString();
    }

    private static string Cut(string s, Match m)
    {
        int from = Math.Max(0, m.Index - 40), to = Math.Min(s.Length, m.Index + m.Length + 40);
        return "“" + (from > 0 ? "…" : "") + s[from..to].Replace("\n", " ") + (to < s.Length ? "…" : "") + "”";
    }

    private static void CardChecks(World w, ContentDatabase content, FiredEvent fired, Game game, HashSet<string> inTheYear)
    {
        var self = w.Protagonist; if (self == null) return; var house = w.House(self.HouseId); if (house == null) return;
        string id = fired.Def.Id; string words = Words(fired, content, w);
        void Add(string kind, string example) => game.Flags.Add(new Flag { Kind = kind, EventId = id, Example = $"year {w.Turn}: {example}" });

        if (!FleetJustLost.Contains(id) && Of(HullsOf, house).Match(words) is { Success: true } mh && FleetSystem.StrengthOf(w, house.Id) == 0) Add("hulls", Cut(words, mh));
        if (Of(WorldsOf, house).Match(words) is { Success: true } mw && !w.HoldingsOf(house.Id).Any(h => h.IsPlanet)) Add("worlds", Cut(words, mw));
        if (!VentureJustLost.Contains(id) && Of(VenturesOf, house).Match(words) is { Success: true } mv && !w.BusinessesOf(house.Id).Any()) Add("ventures", Cut(words, mv));
        if (Shares.Match(words) is { Success: true } ms && !HoldsShares(w, house)) Add("shares", Cut(words, ms));
        if (OldName.Match(words) is { Success: true } mo && house.Origin == "new") Add("old_name", Cut(words, mo));
        if (Spouse.Match(words) is { Success: true } mp && DynastySystem.Spouse(w, self) == null) Add("spouse", Cut(words, mp));

        foreach (var (role, charId) in fired.Binding.All)
        {
            if (role == Binding.Self || w.Char(charId) is not { } c) continue;
            if (OfficeMisfit(w, content, role, c) is { } why) { Add("office", $"{role} bound to {c.Name}, who {why}"); if (role == "sovereign") Add("sovereign", $"{c.Name} of {w.House(c.HouseId)?.Name}, who {why}"); }
        }
        // a step up the ladder for one already at its top
        if (fired.Def.Choices.Any(ch => ch.Effects.OfType<AdvanceCareerEffect>().Any(e => e.Role == Binding.Self)) && content.Careers.MaxRank(self.CareerTrack) is int top && self.CareerRank >= top)
            Add("top_rank", $"{self.Name} is {content.Careers.RankName(self.CareerTrack, self.CareerRank, w.Era)}, the top of the ladder");

        // the known misfires of the review, by name
        if (id == "the_field_promotion" && (self.Age >= 45 || self.CareerRank >= 5 || w.Offices.Any(x => x.CharacterId == self.Id) || w.Magistrates.Any(x => x.CharacterId == self.Id)))
            Add("field_promotion", $"{self.Name}, {self.Age}, {content.Careers.RankName(self.CareerTrack, self.CareerRank, w.Era)}");
        if (id == "the_navy_grows" && (FleetSystem.StrengthOf(w, house.Id) == 0 || self.CareerRank < 3))
            Add("navy_grows", $"{self.Name}, {content.Careers.RankName(self.CareerTrack, self.CareerRank, w.Era)}, the house with {Counted.Hulls(FleetSystem.StrengthOf(w, house.Id))}");
        if (id == "the_inherited_debt" && house.Origin == "new") Add("inherited_debt", $"{house.Name}, a new name");
        // a marriage made in the year has its wedding in the year; one made at the year's turn has it as the next year opens
        if (id == "the_wedding_day" && w.MarriageOf(self.Id) is { } m && (m.Since < w.Turn - 1 || m.Since < w.Turn && inTheYear.Contains(KeyOf(m))))
            Add("late_moment", $"the marriage was made in year {m.Since}, the wedding came in year {w.Turn}");
        if (id == "the_naming_of_a_child" && fired.Binding.TryGet("child", out var kid) && w.Char(kid) is { } baby && baby.Age > 0)
            Add("late_moment", $"{baby.Name} is {baby.Age}");
    }

    private static bool HoldsShares(World w, House house) =>
        w.Companies.Any(co => co.Holders.TryGetValue(house.Id, out int n) && n > 0 || house.Members.Any(id => co.Holders.TryGetValue(Company.PersonKey(id), out int k) && k > 0));

    /// <summary>Why a role named for an office is bound to someone who does not hold it, or null when they do (or the role is not one).</summary>
    private static string? OfficeMisfit(World w, ContentDatabase content, string role, Character c)
    {
        switch (role)
        {
            case "sovereign":
            {
                var seat = w.IsImperium ? ThroneSystem.Emperor(w) : RegionSystem.CapitalHolder(w);
                var holder = seat != null ? Economy.HeadOf(w, seat) : null;
                return holder?.Id == c.Id ? null : w.IsImperium ? "does not sit on the throne" : "does not hold the chair";
            }
            case "consul":
                return w.Offices.Any(o => o.CharacterId == c.Id && o.OfficeId.StartsWith("consul", StringComparison.Ordinal)) ? null : "holds no consul's chair";
            case "praetor" or "aedile" or "quaestor":
                return w.Magistrates.Any(m => m.CharacterId == c.Id && m.OfficeId == role) ? null : "is no " + role;
            case "treasurer" or "speaker":
                return w.Offices.Any(o => o.CharacterId == c.Id && o.OfficeId == role) ? null : "holds no such office";
            case "admiral":
                return c.CareerTrack == "military" && c.CareerRank >= 5 || w.Offices.Any(o => o.CharacterId == c.Id && o.OfficeId == "admiral") || w.Fleets.Values.Any(f => f.AdmiralId == c.Id) ? null : "is no admiral and commands no fleet";
            case "head" or "coalition_head" or "in_law" or "rival_house":
                return w.House(c.HouseId) is { } h && Economy.HeadOf(w, h)?.Id == c.Id ? null : "is not the head of the house";
            default: return null;
        }
    }

    /// <summary>The cards of a person's moment the review named (docs/STEAM_REVIEW.md, row 3), and the part the person plays in each.</summary>
    private static readonly Dictionary<string, string> Moments = new() { ["the_naming_of_a_child"] = "child", ["the_wedding_day"] = "spouse", ["a_grandchild_is_born"] = "grandchild" };

    /// <summary>What the year in brief says, set against the cards of the year: a birth or a marriage of the house whose own card is among the
    /// year's reports must not be told as done before the card comes (docs/EVENTS.md, "Moments in the year").</summary>
    private static void BriefChecks(World w, ContentDatabase content, TurnController tc, Game game)
    {
        if (w.InAcademy) return;
        var lines = YearInBrief.For(w, content);
        foreach (var item in tc.Briefing)
        {
            string id = item.Fired.Def.Id; string title = content.Text(id)?.TitleOf(item.Fired.Version) ?? id;
            if (!Moments.TryGetValue(id, out var part)) continue;
            foreach (var role in item.Fired.Def.Roles.Where(r => r.Name == part))
            {
                if (!item.Fired.Binding.TryGet(role.Name, out var who)) continue;
                foreach (var l in lines.Where(l => l.PersonId == who && l.Kind == YearInBrief.Family && !l.Text.Contains(title, StringComparison.Ordinal)))
                    game.Flags.Add(new Flag { Kind = "late_moment", EventId = id, Example = $"year {w.Turn}: the year in brief told “{l.Text}” before the report" });
            }
        }
    }

    /// <summary>Checks on the world as each year opens: an ambition someone has outgrown.</summary>
    private static void WorldChecks(World w, ContentDatabase content, Game game, HashSet<string> told)
    {
        if (w.InAcademy) return;
        foreach (var c in w.LivingCharacters())
        {
            if (c.Ambition != "earn_a_command" || told.Contains(c.Id)) continue;
            bool top = c.CareerTrack == "military" && content.Careers.MaxRank("military") is int max && c.CareerRank >= Math.Min(max, 5);
            // one of the player's house who commands a fleet has what they wanted, and wants something else in time (AmbitionSystem.Tick)
            bool commands = w.Fleets.Values.Any(f => f.AdmiralId == c.Id) && c.HouseId != w.Protagonist?.HouseId;
            if (!top && !commands) continue;
            told.Add(c.Id);
            game.Flags.Add(new Flag { Kind = "ambition", EventId = "(ambition)", Example = $"year {w.Turn}: {c.Name}, {content.Careers.RankName(c.CareerTrack, c.CareerRank, w.Era)}{(commands ? ", commanding a fleet" : "")}, still wants to earn a command" });
        }
    }
}
