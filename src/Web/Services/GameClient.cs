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
    public void Answer(bool yes) { var a = Asking; Asking = null; if (yes) a?.OnYes(); Notify(); }
    // ---- the questions asked before what starts a war or binds the house (docs/PLAYTHROUGH.md 7) ----

    /// <summary>War on a house: what it costs, what it has, who will send it ships.</summary>
    public void AskWar(string targetId)
    {
        var w = World!; var me = w.Protagonist!; var t = w.House(targetId); if (t == null) return;
        int mine = FleetSystem.StrengthOf(w, me.HouseId), theirs = FleetSystem.StrengthOf(w, targetId), levies = 0, myLevies = 0;
        foreach (var h in w.Holdings.Values) { if (!h.IsPlanet) continue; for (int i = 1; i < h.Provinces.Count; i++) { string holder = ProvinceSystem.HolderOf(h, i); if (holder == h.OwnerId || ProvinceSystem.Dues(w, Content!.Holdings, h, i).IsEmpty() && !w.EnemiesOf(h.OwnerId).Any() && h.Provinces[i].Dues <= 0) continue; if (h.OwnerId == targetId && holder != me.HouseId) levies++; else if (h.OwnerId == me.HouseId && holder != targetId) myLevies++; } }
        string text = t.Name + " has about " + theirs + " hull(s) of its own to your " + mine + "."
            + (levies > 0 ? " The houses with provinces under it may send it as many as " + levies + " squadron(s)." : "") + (myLevies > 0 ? " Those under you may send you " + myLevies + "." : "")
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
            + (yields ? against.Name + " cannot hope to stand against you (" + theirs + " hull(s) to your " + mine + "): it will step down." + (index == 0 ? " It takes the province the new holder had, and is a rung the lower for it." : "")
                      : against.Name + " can hope to win (" + theirs + " hull(s) to your " + mine + "): it will not step down. Asking is war with " + against.Name + ".");
        Ask(yields ? "Ask " + against.Name + " to step down?" : "This means war with " + against.Name, text, yields ? "Ask" : "Ask, and go to war", () => Execute(new DemandSeatCommand(me.Id, planet.Id, index, forId, cat)));
    }
    /// <summary>A marriage by treaty is made at once, with no courtship and no terms.</summary>
    public void AskTreatyMarriage(string label, ICommand command) =>
        Ask("Marry by treaty?", label + ". A treaty marriage is made at once: there is no courtship, no dowry is agreed and the children's name follows custom. To set terms, or to see whether the family would agree, arrange the match in Dynasty, under Marriages.", "Make the marriage", () => Execute(command));
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
    public void Start(string scenarioId, ulong seed, CreationOptions creation, string mode, double difficulty, string intensity, double autonomy, string beyond = "default", string aggression = "default", string size = "small")
    {
        var content = Content ?? throw new InvalidOperationException("Content not loaded.");
        Changing();
        var world = WorldBuilder.FromScenario(Scenario(scenarioId), seed);
        world.Mode = mode; world.Difficulty = difficulty; world.CrisisIntensity = intensity; world.FamilyAutonomy = autonomy; world.Beyond = beyond; world.BeyondAggression = aggression;
        Praetoria.Core.Galaxy.GalaxyGrowth.Grow(world, content, size);   // a larger map, if one was asked for (docs/GALAXY_SIZE.md)
        if (!creation.IsEmpty) CharacterCreation.Apply(world, content, creation);
        ScenarioId = scenarioId; Seed = seed;
        Turn = new TurnController(world, content);
        Turn.SyncRng();
        _idleSnapshot = SaveSystem.Serialize(world);
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
    public void StartCustom(CustomCharacter spec, ulong seed, string mode, double difficulty, string intensity, double autonomy, string beyond = "default", string aggression = "default", string size = "small")
    {
        var content = Content ?? throw new InvalidOperationException("Content not loaded.");
        Changing();
        var world = WorldBuilder.FromScenario(Scenario(CharacterWorkshop.BaseFor(spec)), seed);
        world.Mode = mode; world.Difficulty = difficulty; world.CrisisIntensity = intensity; world.FamilyAutonomy = autonomy; world.Beyond = beyond; world.BeyondAggression = aggression;
        Praetoria.Core.Galaxy.GalaxyGrowth.Grow(world, content, size);   // a larger map, if one was asked for (docs/GALAXY_SIZE.md)
        CharacterWorkshop.Apply(world, content, spec);
        ScenarioId = "a character of your own"; Seed = seed;
        Turn = new TurnController(world, content);
        CharacterWorkshop.Finish(Turn, spec);
        Turn.SyncRng();
        _idleSnapshot = SaveSystem.Serialize(world);
        Turn.BeginTurn();
        LastNotice = null;
        Settled(); Notify();
    }

    /// <summary>The world as it stood between turns (phase Idle), which is what a save is (BuildSpec §1.5). Taken after
    /// every Resolve phase and on Start, so a reload re-opens the current turn's briefing rather than skipping a turn.</summary>
    private string? _idleSnapshot;

    /// <summary>Resolve phase, then the next Briefing — the End Turn button (GDD §9).</summary>
    public void EndTurn()
    {
        if (Turn == null || World == null) return;
        Changing();
        using (Perf.Time("client:resolve the year")) Turn.EndTurn();
        Turn.SyncRng();
        using (Perf.Time("client:serialise the save")) _idleSnapshot = SaveSystem.Serialize(World);
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
        bool ok = Turn.Resolve(item, choiceId);
        if (ok)
        {
            ActedThisYear = true;
            var lines = new List<string>();
            if (house != null && before != null)
            {
                var changed = Resource.All.Select(k => (k, d: house.Treasury.Get(k) - before.Get(k))).Where(x => x.d != 0).Select(x => (x.d > 0 ? "+" : "−") + Math.Abs(x.d) + " " + Resource.Label(x.k)).ToList();
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

    /// <summary>A context for asking a command whether it would be refused. Not for executing.</summary>
    public CommandContext Ctx => new(World!, WorldBuilder.RngFor(World!));

    public bool Execute(ICommand command)
    {
        if (Turn == null || World == null) return false;
        Changing();
        bool ok = Turn.Executor.TryExecute(command, new CommandContext(World, WorldBuilder.RngFor(World)));
        Turn.SyncRng();
        LastNotice = ok ? null : $"Could not: {command.Describe()}.";
        if (ok) ActedThisYear = true;
        Settled(); Notify();
        return ok;
    }

    public void Abandon() { Changing(); Turn = null; ActedThisYear = false; Notify(); }

    // Between one action and the next the world does not change, so the projection is built once and pure questions the screens
    // ask (Senate weights, law tables) are memoised (ReadCache). Every method here that changes the world calls Changing() first
    // and Settled() after; nothing else in the client may change it.
    private CourtModel? _court; private AcademyModel? _academy; private bool _academyBuilt;
    private void Changing() { ReadCache.Close(); _court = null; _academy = null; _academyBuilt = false; }
    private void Settled() { _court = null; _academy = null; _academyBuilt = false; if (Turn != null) ReadCache.Open(); }

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

    /// <summary>A save is the world at the start of the current turn: choices made this turn are replayed by the player,
    /// not stored mid-phase, so a loaded game always opens on a fresh briefing.</summary>
    public string SaveJson()
    {
        if (Turn == null || World == null) throw new InvalidOperationException("No run.");
        if (_idleSnapshot != null) return _idleSnapshot;
        Turn.SyncRng();
        return SaveSystem.Serialize(World);
    }

    /// <summary>Load a save. The loaded world is between turns (phase Idle), so open a briefing on it.</summary>
    public void LoadJson(string json)
    {
        Changing();
        var world = SaveSystem.Deserialize(json);
        _idleSnapshot = json; ActedThisYear = false;
        Turn = new TurnController(world, Content!);
        ScenarioId = "(saved game)"; Seed = world.RngState;
        if (!world.IsDynastyDead) Turn.BeginTurn();
        Settled(); Notify();
    }
}
