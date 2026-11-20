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
        if (!World.IsDynastyDead) using (Perf.Time("client:open the next year")) Turn.BeginTurn();
        LastNotice = null;
        Settled(); Notify();
    }

    public bool Resolve(BriefingItem item, string choiceId)
    {
        if (Turn == null) return false;
        Changing();
        bool ok = Turn.Resolve(item, choiceId);
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
        Settled(); Notify();
        return ok;
    }

    public void Abandon() { Changing(); Turn = null; Notify(); }

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
        _idleSnapshot = json;
        Turn = new TurnController(world, Content!);
        ScenarioId = "(saved game)"; Seed = world.RngState;
        if (!world.IsDynastyDead) Turn.BeginTurn();
        Settled(); Notify();
    }
}
