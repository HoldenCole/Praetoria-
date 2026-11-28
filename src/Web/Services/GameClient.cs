using Praetoria.Core.Diagnostics;
using System.Net.Http.Json;
using Praetoria.Core.Commands;
using Praetoria.Core.Creation;
using Praetoria.Core.Data;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Web.Services;

/// <summary>
/// The one object the browser UI talks to (the web twin of the Godot layer's GameState). Owns the loaded content and the
/// live <see cref="TurnController"/>; components read projections from Core and call the few verbs here. No rules live
/// in this class. Content is fetched over HTTP into the WASM in-memory file system so the ordinary loaders run unchanged.
/// </summary>
public sealed class GameClient
{
    public const string ContentRoot = "/content";
    public const string AutosaveKey = "praetoria.autosave";
    /// <summary>How many saves are kept beside the running one (docs/PLAYTHROUGH.md, a first game: "save slots").</summary>
    public const int Slots = 3;
    /// <summary>Where a save lives in the browser: 0 is the running saga, 1 to <see cref="Slots"/> the kept ones.</summary>
    public static string SlotKey(int slot) => slot <= 0 ? AutosaveKey : "praetoria.slot." + slot;
    /// <summary>What a save of the game as it stands is called in a list: the house, its head, the year and whether the year is under way, and when.</summary>
    public string SaveLabel()
    {
        if (World?.Protagonist is not { } p) return "A saga";
        return (World.House(p.HouseId)?.Name ?? "a house") + ", " + p.Name + ", year " + World.Turn + (World.InAcademy ? " (at the Academy)" : ActedThisYear ? ", the year under way" : "") + " · saved " + DateTime.Now.ToString("d MMM yyyy, HH:mm", System.Globalization.CultureInfo.InvariantCulture);
    }

    private readonly HttpClient _http;
    public GameClient(HttpClient http) => _http = http;

    public ContentDatabase? Content { get; private set; }
    public TurnController? Turn { get; private set; }
    public World? World => Turn?.World;
    public IReadOnlyList<string> ScenarioIds { get; private set; } = Array.Empty<string>();
    public string ScenarioId { get; private set; } = "";
    public ulong Seed { get; private set; }
    public string? LoadError { get; private set; }
    public string? LastNotice { get; private set; }
    public int LoadedFiles { get; private set; }
    public int TotalFiles { get; private set; }

    /// <summary>Raised after anything that changes what the views should show.</summary>
    public event Action? Changed;
    private void Notify() => Changed?.Invoke();

    /// <summary>Something that cannot be taken back is about to be done: the player is asked first (docs/PLAYTHROUGH.md 2, 7).</summary>
    public sealed record ConfirmAsk(string Title, string Text, string Yes, Action OnYes, string No);
    public ConfirmAsk? Asking { get; private set; }
    public void Ask(string title, string text, string yes, Action onYes, string no = "Leave it") { Asking = new ConfirmAsk(title, text, yes, onYes, no); Notify(); }
    /// <summary>Ask, and on yes carry out one command: for the things that cannot be taken back (docs/PLAYTHROUGH.md, "Confirm before what cannot be undone").</summary>
    public void AskDo(string title, string text, string yes, ICommand command) => Ask(title, text, yes, () => Execute(command));
    public void Answer(bool yes) { var a = Asking; Asking = null; if (yes) a?.OnYes(); Notify(); }
    // ---- the questions asked before what starts a war or binds the house (docs/PLAYTHROUGH.md 7) ----

    /// <summary>War on a house: what it costs, what it has, who will send it ships.</summary>
    public void AskWar(string targetId)
    {
        var w = World!; var me = w.Protagonist!; var t = w.House(targetId); if (t == null) return;
        int mine = FleetSystem.StrengthOf(w, me.HouseId), theirs = FleetSystem.StrengthOf(w, targetId), levies = 0, myLevies = 0;
        foreach (var h in w.Holdings.Values) { if (!h.IsPlanet) continue; for (int i = 1; i < h.Provinces.Count; i++) { string holder = ProvinceSystem.HolderOf(h, i); if (holder == h.OwnerId || ProvinceSystem.Dues(w, Content!.Holdings, h, i).IsEmpty() && !w.EnemiesOf(h.OwnerId).Any() && h.Provinces[i].Dues <= 0) continue; if (h.OwnerId == targetId && holder != me.HouseId) levies++; else if (h.OwnerId == me.HouseId && holder != targetId) myLevies++; } }
        string text = t.Name + " has about " + Praetoria.Core.Counted.Hulls(theirs) + " of its own to your " + mine + "."
            + (levies > 0 ? " The houses with provinces under it may send it as many as " + Praetoria.Core.Counted.Squadrons(levies) + "." : "") + (myLevies > 0 ? " Those under you may send you " + myLevies + "." : "")
            + " Its head will think far worse of you. The war ends when either side sues for peace and the other will have it, or when one cannot fight on. It costs 2 influence of this year's.";
        Ask("Declare war on " + t.Name + "?", text, "Declare war", () => Execute(new DeclareWarCommand(me.Id, targetId, Content!.Fleets)));
    }
    /// <summary>A holder asked to step down: whether it will go or fight is known before it is asked, and said.</summary>
    public void AskSeat(Holding planet, int index, string forId)
    {
        var w = World!; var me = w.Protagonist!; var cat = Content!.Holdings; string againstId = ProvinceSystem.HolderOf(planet, index); var against = w.House(againstId); if (against == null) return;
        bool yields = ProvinceSystem.WillYield(w, cat, me.HouseId, againstId); int mine = FleetSystem.StrengthOf(w, me.HouseId), theirs = FleetSystem.StrengthOf(w, againstId);
        string what = ProvinceSystem.What(planet, index), to = forId == me.HouseId ? "your own house" : w.House(forId)?.Name ?? "another house"; int cost = ProvinceSystem.DemandCost(cat, planet, index);
        string text = what + " was " + against.Name + "'s for life. You ask it to give it up to " + to + (cost > 0 ? ", for " + cost + " influence" : ", at no cost: it is withholding its dues") + ". "
            + (yields ? against.Name + " cannot hope to stand against you (" + Praetoria.Core.Counted.Hulls(theirs) + " to your " + mine + "): it will step down." + (index == 0 ? " It takes the province the new holder had, and is a rung the lower for it." : "")
                      : against.Name + " can hope to win (" + Praetoria.Core.Counted.Hulls(theirs) + " to your " + mine + "): it will not step down. Asking is war with " + against.Name + ".");
        Ask(yields ? "Ask " + against.Name + " to step down?" : "This means war with " + against.Name, text, yields ? "Ask" : "Ask, and go to war", () => Execute(new DemandSeatCommand(me.Id, planet.Id, index, forId, cat)));
    }
    /// <summary>A marriage by treaty is made at once, with no courtship and no terms.</summary>
    public void AskTreatyMarriage(string label, ICommand command) =>
        Ask("Marry by treaty?", label + ". The other family is asked today, as it would be on the first day of a courtship, and has said it would agree. The marriage is made at once on custom's terms: the customary dowry, and the children's name as custom gives it. One of your house who does not want the match will hold it against you, as in any marriage arranged for them. To offer other terms, or to bring round a family that would refuse, arrange the match in Dynasty, under Marriages.", "Make the marriage", () => Execute(command));
    public void AskAccident(string label, ICommand command) =>
        Ask("Arrange an accident?", label + ". If it is traced to you it is a blood feud with that house, and the Senate hears of it. It cannot be called back once the order is given.", "Give the order", () => Execute(command));

    /// <summary>Whether anything has been done since this year began. A save is the start of the year (see <see cref="SaveJson"/>):
    /// what is done in a year is kept when the year is ended, and the screen says so.</summary>
    public bool ActedThisYear { get; private set; }

    /// <summary>The person whose profile is open over the screen, if any: any portrait opens it (docs/ART_DIRECTION.md).</summary>
    public string? ProfileId { get; private set; }
    /// <summary>The painted portraits, if the build ships any (tools/art/portraits.py).</summary>
    public PortraitLibrary? Portraits { get; private set; }
    /// <summary>The things that have a painting (docs/BUILDINGS.md, tools/art/works.py): the ids of buildings, great works and tracks of
    /// infrastructure with a picture in img/works. Empty when none was painted; a thing without one is shown without.</summary>
    public HashSet<string> WorkArt { get; private set; } = new();
    public string? WorkPicture(string id) => WorkArt.Contains(id) ? $"img/works/{id}.webp" : null;
    public void ShowProfile(string id) { if (World?.Char(id) == null || ProfileId == id) return; ProfileId = id; Notify(); }
    public void CloseProfile() { if (ProfileId == null) return; ProfileId = null; Notify(); }

    public bool Ready => Content != null;

    public async Task LoadAsync()
    {
        if (Content != null) return;
        try
        {
            var manifest = await _http.GetStringAsync("content/manifest.txt");
            var files = manifest.Split('\n').Select(l => l.Trim().Replace('\\', '/')).Where(l => l.Length > 0).ToList();
            TotalFiles = files.Count;
            foreach (var rel in files)
            {
                var text = await _http.GetStringAsync("content/" + rel);
                var path = Path.Combine(ContentRoot, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, text);
                LoadedFiles++;
                Notify();
            }
            try { Portraits = PortraitLibrary.Parse(await _http.GetStringAsync("img/portraits/manifest.json")); } catch { Portraits = null; }   // none painted: portraits are drawn
            try { using var wd = System.Text.Json.JsonDocument.Parse(await _http.GetStringAsync("img/works/manifest.json")); WorkArt = wd.RootElement.GetProperty("works").EnumerateArray().Select(x => x.GetString() ?? "").ToHashSet(); } catch { WorkArt = new(); }
            Content = ContentLoader.LoadFromDirectory(ContentRoot);
            ScenarioIds = Directory.EnumerateFiles(Path.Combine(ContentRoot, "scenarios"), "*.json")
                .Select(Path.GetFileNameWithoutExtension).Where(s => s != null).Select(s => s!).Where(s => !ScenarioLoader.LoadFromContent(ContentRoot, s).Hidden)   // fixtures are for tests and tools
                .OrderBy(s => s == "the_academy" ? "" : s, StringComparer.Ordinal).ToList();
        }
        catch (Exception ex) { LoadError = ex.Message; }
        Notify();
    }

    public Scenario Scenario(string id) => ScenarioLoader.LoadFromContent(ContentRoot, id);

    /// <summary>Start a run: build the world, apply character creation, open the first turn's briefing.</summary>
    public void Start(string scenarioId, ulong seed, CreationOptions creation, string mode, double difficulty, string intensity, double autonomy, string beyond = "default", string aggression = "default", string size = "small", string ease = "standard", IEnumerable<string>? boosts = null)
    {
        var content = Content ?? throw new InvalidOperationException("Content not loaded.");
        Changing();
        var world = WorldBuilder.FromScenario(Scenario(scenarioId), seed);
        world.Mode = mode; world.Difficulty = difficulty; world.CrisisIntensity = intensity; world.FamilyAutonomy = autonomy; world.Beyond = beyond; world.BeyondAggression = aggression;
        world.MoneyEase = ease; foreach (var ch in boosts ?? Array.Empty<string>()) world.Boosts.Add(ch);   // how easy money is, and what the game is begun with (docs/MONEY.md, "Settings")
        Praetoria.Core.Galaxy.GalaxyGrowth.Grow(world, content, size);   // a larger map, if one was asked for (docs/GALAXY_SIZE.md)
        if (!creation.IsEmpty) CharacterCreation.Apply(world, content, creation);
        ScenarioId = scenarioId; Seed = seed;
        MoneySettings.Begin(world);
        if (FirstGame || world.InAcademy) FirstStepsSystem.Begin(world, content.Academy.FirstYears, scenarioId);   // the first years (docs/ACADEMY.md)
        Turn = new TurnController(world, content);
        Turn.SyncRng();
        _base = null; _done.Clear();
        Turn.BeginTurn();
        LastNotice = null;
        Settled(); Notify();
    }

    /// <summary>The blank world a character of the player's own making is laid over (docs/CHARACTER.md): for the workshop's lists of
    /// houses, worlds and systems. Built once per era.</summary>
    private readonly Dictionary<bool, World> _blank = new();
    public World BlankWorld(bool imperium)
    {
        if (!_blank.TryGetValue(imperium, out var w)) _blank[imperium] = w = WorldBuilder.FromScenario(Scenario(imperium ? CharacterWorkshop.ImperiumBase : CharacterWorkshop.RepublicBase), 1);
        return w;
    }

    /// <summary>Start a run with a character of the player's own making.</summary>
    public void StartCustom(CustomCharacter spec, ulong seed, string mode, double difficulty, string intensity, double autonomy, string beyond = "default", string aggression = "default", string size = "small", string ease = "standard", IEnumerable<string>? boosts = null)
    {
        var content = Content ?? throw new InvalidOperationException("Content not loaded.");
        Changing();
        var world = WorldBuilder.FromScenario(Scenario(CharacterWorkshop.BaseFor(spec)), seed);
        world.Mode = mode; world.Difficulty = difficulty; world.CrisisIntensity = intensity; world.FamilyAutonomy = autonomy; world.Beyond = beyond; world.BeyondAggression = aggression;
        world.MoneyEase = ease; foreach (var ch in boosts ?? Array.Empty<string>()) world.Boosts.Add(ch);   // how easy money is, and what the game is begun with (docs/MONEY.md, "Settings")
        Praetoria.Core.Galaxy.GalaxyGrowth.Grow(world, content, size);   // a larger map, if one was asked for (docs/GALAXY_SIZE.md)
        CharacterWorkshop.Apply(world, content, spec);
        ScenarioId = "a character of your own"; Seed = seed;
        MoneySettings.Begin(world);
        if (FirstGame || world.InAcademy) FirstStepsSystem.Begin(world, content.Academy.FirstYears, "custom");   // the first years (docs/ACADEMY.md)
        Turn = new TurnController(world, content);
        CharacterWorkshop.Finish(Turn, spec);
        Turn.SyncRng();
        _base = null; _done.Clear();
        Turn.BeginTurn();
        LastNotice = null;
        Settled(); Notify();
    }

    // ---- undo (docs/PLAYTHROUGH.md, a first game: "undo the last action of the year") ----
    // A save takes too long in a browser to make one before every act. So the game keeps the year as it stood before anything was done
    // in it (_base: the save the autosave makes anyway, or the one that was loaded) and a list of what has been done since (_done). To
    // take the last act back it loads _base and does the others again, in order. Commands name things by id and take their dice from
    // the world's own state, so the same acts on the same world come out the same; nothing is rolled twice.
    private string? _base; private readonly List<(string what, Func<bool> again)> _done = new();
    public bool CanUndo => Turn != null && _done.Count > 0 && _base != null;
    public string UndoWhat => _done.Count > 0 ? Plain(_done[^1].what) : "";
    /// <summary>A command's own account of itself, with the names of things in place of their ids: for what the player is shown.</summary>
    public string Plain(string what)
    {
        if (World is not { } w) return what;
        return System.Text.RegularExpressions.Regex.Replace(what, @"[A-Za-z][A-Za-z0-9]*(?:[_#@][A-Za-z0-9]+)+", m =>
        {
            string id = m.Value;
            if (w.House(id) is { } h) return h.Name;
            if (w.Char(id) is { } c) return c.Name;
            if (w.Fleets.TryGetValue(id, out var f)) return f.Name;
            if (w.Holding(id) is { } hold) return hold.Name;
            if (w.Company(id) is { } co) return co.Name;
            if (w.Galaxy?.System(id) is { } sys) return sys.Name;
            return id.Replace('_', ' ');
        });
    }
    /// <summary>How many things done this year can still be taken back, the last first.</summary>
    public int UndoSteps => _base != null ? _done.Count : 0;
    /// <summary>Before the first act of a year: the year as it stands, if no save of it has been made yet.</summary>
    private void Base() { if (_base == null && Turn != null) using (Perf.Time("client:undo base")) _base = Turn.SaveMidYear(); }
    /// <summary>Take the last thing done this year back: the game is as it was the moment before it.</summary>
    public void Undo()
    {
        if (!CanUndo) return;
        string json = _base!, scenario = ScenarioId, what = _done[^1].what; ulong seed = Seed;
        var keep = _done.Take(_done.Count - 1).ToList(); var learnt = World?.FirstSteps;
        LoadJson(json);
        _base = json;
        bool whole = true;
        foreach (var step in keep) { Changing(); bool ok = step.again(); Turn!.SyncRng(); if (ok) _done.Add(step); else whole = false; }
        ScenarioId = scenario; Seed = seed; ActedThisYear = _done.Count > 0 || ActedThisYear; Told = null;
        if (World != null) FirstStepsSystem.Carry(learnt, World);   // the act is taken back; having found where it is done is not
        what = Plain(what);
        LastNotice = whole ? "Taken back: " + what + "." : "Taken back: " + what + ". Something done before it could not be done again and is undone too.";
        Settled(); Notify();
    }

    /// <summary>Resolve phase, then the next Briefing — the End Turn button (GDD §9).</summary>
    public void EndTurn()
    {
        if (Turn == null || World == null) return;
        Asking = null; Told = null;   // a question or an outcome still open belongs to the year that is ending
        Changing();
        using (Perf.Time("client:resolve the year")) Turn.EndTurn();
        Turn.SyncRng();
        _base = null; _done.Clear();   // a year that has turned cannot be taken back
        ActedThisYear = false;
        if (!World.IsDynastyDead) using (Perf.Time("client:open the next year")) Turn.BeginTurn();
        LastNotice = null;
        Settled(); Notify();
    }

    /// <summary>What came of the last answer given to a report: what the house gained and lost by it and what was written down
    /// (docs/PLAYTHROUGH.md 8). Shown once, then dismissed.</summary>
    public sealed record Outcome(string Title, string Chose, IReadOnlyList<string> Lines);
    public Outcome? Told { get; private set; }
    public void Dismiss() { Told = null; Notify(); }

    public bool Resolve(BriefingItem item, string choiceId, string title = "", string chose = "")
    {
        if (Turn == null || World == null) return false;
        Changing();
        var me = World.Char(World.ProtagonistId); var house = me != null ? World.House(me.HouseId) : null;
        var before = house?.Treasury.Clone(); int legit = house?.Legitimacy ?? 0, stress = me?.Stress ?? 0, told = World.History.Count;
        Base();
        int at = Turn.Briefing.ToList().IndexOf(item);
        bool ok = Turn.Resolve(item, choiceId);
        if (ok)
        {
            LastNotice = null;
            if (at >= 0) _done.Add(("your answer to " + (title.Length > 0 ? "\u201c" + title + "\u201d" : "a report"), () => Turn != null && at < Turn.Briefing.Count && Turn.Resolve(Turn.Briefing[at], choiceId)));
            else { _base = null; _done.Clear(); }
            ActedThisYear = true;
            var lines = new List<string>();
            if (house != null && before != null)
            {
                var changed = Praetoria.Core.Money.Changed(before, house.Treasury);
                if (house.Legitimacy != legit) changed.Add("legitimacy " + (house.Legitimacy > legit ? "+" : "−") + Math.Abs(house.Legitimacy - legit));
                if (me != null && me.Stress != stress) changed.Add("stress " + (me.Stress > stress ? "+" : "−") + Math.Abs(me.Stress - stress));
                if (changed.Count > 0) lines.Add(string.Join(" · ", changed));
            }
            lines.AddRange(World.History.Skip(told).Select(h => h.Text).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().Take(4));
            Told = new Outcome(title, chose, lines.Count > 0 ? lines : new List<string> { "Nothing in the house's accounts changed by it. What it changed between people will show in time." });
        }
        Settled(); Notify();
        return ok;
    }

    /// <summary>Carry out a command and say what came of it in the outcome box, as a report's choice does: what moved in the treasury, the
    /// year's pools, legitimacy and strain, and what the chronicle wrote. For acts whose whole result used to be one line in another
    /// window (docs/PLAYTHROUGH.md, Easy: "Read the accounts shows its finding where it was asked").</summary>
    public bool ExecuteTold(ICommand command, string title, string chose)
    {
        if (Turn == null || World == null) return false;
        var me = World.Char(World.ProtagonistId); var house = me != null ? World.House(me.HouseId) : null;
        var before = house?.Treasury.Clone(); int legit = house?.Legitimacy ?? 0, stress = me?.Stress ?? 0, told = World.History.Count;
        var pools = me != null ? World.PoolsFor(me.Id) : null; int pi = pools?.Influence ?? 0, pt = pools?.Treasury ?? 0, pa = pools?.Agents ?? 0;
        if (!Execute(command)) return false;
        var changed = new List<string>();
        if (house != null && before != null) changed.AddRange(Praetoria.Core.Money.Changed(before, house.Treasury));
        if (pools != null) foreach (var (name, was, now) in new[] { ("influence this year", pi, pools.Influence), ("treasury this year", pt, pools.Treasury), ("agents this year", pa, pools.Agents) }) if (now != was) changed.Add((now > was ? "+" : "−") + Math.Abs(now - was) + " " + name);
        if (house != null && house.Legitimacy != legit) changed.Add("legitimacy " + (house.Legitimacy > legit ? "+" : "−") + Math.Abs(house.Legitimacy - legit));
        if (me != null && me.Stress != stress) changed.Add("stress " + (me.Stress > stress ? "+" : "−") + Math.Abs(me.Stress - stress));
        var lines = new List<string>(); if (changed.Count > 0) lines.Add(string.Join(" · ", changed));
        lines.AddRange(World.History.Skip(told).Select(h => h.Text).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().Take(4));
        Told = new Outcome(title, chose, lines.Count > 0 ? lines : new List<string> { "Nothing that can be counted changed by it. What it changed between people will show in time." });
        Notify();
        return true;
    }

    /// <summary>A context for asking a command whether it would be refused. Not for executing.</summary>
    public CommandContext Ctx => new(World!, WorldBuilder.RngFor(World!));

    public bool Execute(ICommand command)
    {
        if (Turn == null || World == null) return false;
        Changing();
        Base();
        bool ok = Turn.Executor.TryExecute(command, new CommandContext(World, WorldBuilder.RngFor(World)));
        Turn.SyncRng();
        LastNotice = ok ? null : $"Could not: {command.Describe()}.";
        if (ok) { ActedThisYear = true; _done.Add((command.Describe(), () => Turn != null && World != null && Turn.Executor.TryExecute(command, new CommandContext(World, WorldBuilder.RngFor(World))))); }
        Settled(); Notify();
        return ok;
    }

    public void Abandon() { Changing(); Turn = null; ActedThisYear = false; _base = null; _done.Clear(); Notify(); }

    // Between one action and the next the world does not change, so the projection is built once and pure questions the screens
    // ask (Senate weights, law tables) are memoised (ReadCache). Every method here that changes the world calls Changing() first
    // and Settled() after; nothing else in the client may change it.
    private CourtModel? _court; private AcademyModel? _academy; private bool _academyBuilt;
    private void Changing() { ReadCache.Close(); _court = null; _academy = null; _academyBuilt = false; _stepsBuilt = false; }
    private void Settled() { _court = null; _academy = null; _academyBuilt = false; _stepsBuilt = false; if (Turn != null) ReadCache.Open(); }

    public CourtModel Court()
    {
        if (_court != null) return _court;
        using (Perf.Time("client:build the projection")) _court = CourtProjection.Build(Turn ?? throw new InvalidOperationException("No run."), Content!);
        return _court;
    }
    /// <summary>The Academy year as the screen shows it, or null in a run that never went there (docs/ACADEMY.md).</summary>
    public AcademyModel? Academy()
    {
        if (Turn == null) return null;
        if (!_academyBuilt) { _academy = AcademyProjection.Build(Turn, Content!); _academyBuilt = true; }
        return _academy;
    }

    // ---- the first years (docs/ACADEMY.md, "The first years") ----
    /// <summary>This browser has not yet had its first game (the page reads it from what the browser keeps before a saga is begun): a saga
    /// begun now carries the first years' list, and shows the rail's labels by themselves. A saga that opens at the Academy carries the list
    /// whatever this says, for the cadet's choice of course decides it.</summary>
    public bool FirstGame { get; set; }
    private FirstStepsModel? _steps; private bool _stepsBuilt;
    /// <summary>The first years' list as it stands, or null when it is not on the screen.</summary>
    public FirstStepsModel? FirstSteps()
    {
        if (World == null || Content == null) return null;
        if (!_stepsBuilt) { _steps = FirstStepsProjection.Build(World, Content); _stepsBuilt = true; }
        return _steps;
    }
    /// <summary>The player has opened a window or a tab ("house:ledger"), or picked a world on the map ("map"): a step may tick. Not an act
    /// of the game: nothing is changed that the rules read, and undo keeps it.</summary>
    public void Saw(string place)
    {
        if (World == null || !FirstStepsSystem.Saw(World, place)) return;
        _stepsBuilt = false; Notify();
    }
    /// <summary>The player puts the first years' list away.</summary>
    public void PutAwayFirstSteps() { if (World == null) return; FirstStepsSystem.Dismiss(World); _stepsBuilt = false; Notify(); }
    /// <summary>Whether the first years' list can be pinned now (the primer's last page): the saga's first three years are not over, and it
    /// has no list on the screen (none was begun, or it was put away).</summary>
    public bool CanPinFirstSteps => World is { InAcademy: false } w && Content != null && w.Turn <= Content.Academy.FirstYears.Years && FirstSteps() == null
        && !(FirstStepsSystem.Active(w) && FirstStepsSystem.AllDone(w, Content.Academy.FirstYears));
    /// <summary>Pin the first years' list: one put away comes back as it was; a saga with none is given its start's (a saga begun in this
    /// session knows its start; a loaded one takes the list for a character of one's own, kept to what the house can do).</summary>
    public void PinFirstSteps()
    {
        if (!CanPinFirstSteps) return;
        var w = World!;
        if (FirstStepsSystem.Active(w)) w.FirstSteps!.Dismissed = false;
        else { var fs = FirstStepsSystem.Begin(w, Content!.Academy.FirstYears, Content.Academy.FirstYears.Starts.ContainsKey(ScenarioId) ? ScenarioId : "custom"); fs.Academy = false; }
        _stepsBuilt = false; Notify();
    }
    /// <summary>Whether this saga shows the rail's labels by themselves (a first game, for its first years), unless the player has chosen.</summary>
    public bool LabelsByThemselves => World != null && Content != null && FirstStepsSystem.Labels(World, Content.Academy.FirstYears);

    /// <summary>A save is the game as it stands now: in the middle of a year it holds what has been done this year and the year's
    /// reports with the answers given (<see cref="TurnController.SaveMidYear"/>), so loading it opens the same year where it was left.</summary>
    public string SaveJson()
    {
        if (Turn == null || World == null) throw new InvalidOperationException("No run.");
        string json = Turn.SaveMidYear();
        if (_done.Count == 0) _base = json;   // nothing done since: this is also what Undo would go back to
        return json;
    }

    /// <summary>Load a save: a year saved in its middle is taken up where it was left, and one saved between years begins.</summary>
    public void LoadJson(string json)
    {
        Changing();
        var world = SaveSystem.Deserialize(json);
        _base = json; _done.Clear(); ActedThisYear = world.MidYear != null;
        Turn = new TurnController(world, Content!);
        ScenarioId = "(saved game)"; Seed = world.RngState;
        if (!world.IsDynastyDead) Turn.Resume(); else world.MidYear = null;
        Told = null;
        Settled(); Notify();
    }
}
