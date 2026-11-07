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
            Content = ContentLoader.LoadFromDirectory(ContentRoot);
            ScenarioIds = Directory.EnumerateFiles(Path.Combine(ContentRoot, "scenarios"), "*.json")
                .Select(Path.GetFileNameWithoutExtension).Where(s => s != null).Select(s => s!).OrderBy(s => s, StringComparer.Ordinal).ToList();
        }
        catch (Exception ex) { LoadError = ex.Message; }
        Notify();
    }

    public Scenario Scenario(string id) => ScenarioLoader.LoadFromContent(ContentRoot, id);

    /// <summary>Start a run: build the world, apply character creation, open the first turn's briefing.</summary>
    public void Start(string scenarioId, ulong seed, CreationOptions creation, string mode, double difficulty, string intensity, double autonomy)
    {
        var content = Content ?? throw new InvalidOperationException("Content not loaded.");
        var world = WorldBuilder.FromScenario(Scenario(scenarioId), seed);
        world.Mode = mode; world.Difficulty = difficulty; world.CrisisIntensity = intensity; world.FamilyAutonomy = autonomy;
        if (!creation.IsEmpty) CharacterCreation.Apply(world, content, creation);
        ScenarioId = scenarioId; Seed = seed;
        Turn = new TurnController(world, content);
        Turn.SyncRng();
        _idleSnapshot = SaveSystem.Serialize(world);
        Turn.BeginTurn();
        LastNotice = null;
        Notify();
    }

    /// <summary>The world as it stood between turns (phase Idle), which is what a save is (BuildSpec §1.5). Taken after
    /// every Resolve phase and on Start, so a reload re-opens the current turn's briefing rather than skipping a turn.</summary>
    private string? _idleSnapshot;

    /// <summary>Resolve phase, then the next Briefing — the End Turn button (GDD §9).</summary>
    public void EndTurn()
    {
        if (Turn == null || World == null) return;
        Turn.EndTurn();
        Turn.SyncRng();
        _idleSnapshot = SaveSystem.Serialize(World);
        if (!World.IsDynastyDead) Turn.BeginTurn();
        LastNotice = null;
        Notify();
    }

    public bool Resolve(BriefingItem item, string choiceId)
    {
        if (Turn == null) return false;
        bool ok = Turn.Resolve(item, choiceId);
        Notify();
        return ok;
    }

    public bool Execute(ICommand command)
    {
        if (Turn == null || World == null) return false;
        bool ok = Turn.Executor.TryExecute(command, new CommandContext(World, WorldBuilder.RngFor(World)));
        Turn.SyncRng();
        LastNotice = ok ? null : $"Could not: {command.Describe()}.";
        Notify();
        return ok;
    }

    public void Abandon() { Turn = null; Notify(); }

    public CourtModel Court() => CourtProjection.Build(Turn ?? throw new InvalidOperationException("No run."), Content!);

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
        var world = SaveSystem.Deserialize(json);
        _idleSnapshot = json;
        Turn = new TurnController(world, Content!);
        ScenarioId = "(saved game)"; Seed = world.RngState;
        if (!world.IsDynastyDead) Turn.BeginTurn();
        Notify();
    }
}
