using Praetoria.Core.Creation;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Game;

/// <summary>
/// The one object the UI talks to (BuildSpec §1 Pillar 1: Godot reads from and sends commands to Core,
/// never the reverse). Owns the loaded content and the live <see cref="TurnController"/>; every view reads
/// from <see cref="World"/> and calls the few verbs here. No rules live in this class.
/// </summary>
public sealed class GameState
{
    public ContentDatabase Content { get; }
    public string ContentRoot { get; }
    public TurnController? Turn { get; private set; }
    public World? World => Turn?.World;
    public string ScenarioId { get; private set; } = "";
    public ulong Seed { get; private set; }

    /// <summary>Raised after anything that changes what the views should show.</summary>
    public event Action? Changed;

    public GameState(string contentRoot)
    {
        ContentRoot = contentRoot;
        Content = ContentLoader.LoadFromDirectory(contentRoot);
    }

    public IReadOnlyList<string> ScenarioIds() =>
        Directory.EnumerateFiles(Path.Combine(ContentRoot, "scenarios"), "*.json")
                 .Select(Path.GetFileNameWithoutExtension)
                 .Where(s => s != null).Select(s => s!)
                 .OrderBy(s => s, StringComparer.Ordinal).ToList();

    /// <summary>Start a run: build the world, apply character creation, open the first turn's briefing.</summary>
    public void Start(string scenarioId, ulong seed, CreationOptions creation, string mode, double difficulty = 1.0, string intensity = "default", double autonomy = 1.0)
    {
        var scenario = ScenarioLoader.LoadFromContent(ContentRoot, scenarioId);
        var world = WorldBuilder.FromScenario(scenario, seed);
        world.Mode = mode;
        world.Difficulty = difficulty;
        world.CrisisIntensity = intensity;
        world.FamilyAutonomy = autonomy;
        if (!creation.IsEmpty) CharacterCreation.Apply(world, Content, creation);
        ScenarioId = scenarioId;
        Seed = seed;
        Turn = new TurnController(world, Content);
        Turn.BeginTurn();
        Changed?.Invoke();
    }

    /// <summary>Resolve phase, then the next Briefing — the "End Turn" button (GDD §9).</summary>
    public void EndTurn()
    {
        if (Turn == null || World == null) return;
        Turn.EndTurn();
        if (!World.IsDynastyDead) Turn.BeginTurn();
        Changed?.Invoke();
    }

    public bool Resolve(BriefingItem item, string choiceId)
    {
        if (Turn == null) return false;
        bool ok = Turn.Resolve(item, choiceId);
        Changed?.Invoke();
        return ok;
    }

    public bool Execute(Praetoria.Core.Commands.ICommand command)
    {
        if (Turn == null || World == null) return false;
        bool ok = Turn.Executor.TryExecute(command, new Praetoria.Core.Commands.CommandContext(World, WorldBuilder.RngFor(World)));
        Turn.SyncRng();
        Changed?.Invoke();
        return ok;
    }

    public void Save(string path)
    {
        if (Turn == null || World == null) return;
        Turn.SyncRng();
        SaveSystem.Save(World, path);
    }

    /// <summary>Load a save. The loaded world is between turns (phase Idle), so open a briefing on it.</summary>
    public void Load(string path)
    {
        var world = SaveSystem.Load(path);
        Turn = new TurnController(world, Content);
        ScenarioId = "(saved game)";
        Seed = world.RngState;
        if (!world.IsDynastyDead) Turn.BeginTurn();
        Changed?.Invoke();
    }

    public string RankName(Character c) =>
        Content.Careers.RankName(c.CareerTrack, c.CareerRank) is { } n ? $"{n}" : $"rank {c.CareerRank}";
}
