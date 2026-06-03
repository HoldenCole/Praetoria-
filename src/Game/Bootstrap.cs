using Godot;
using Praetoria.Core;
using Praetoria.Core.Data;

namespace Praetoria.Game;

/// <summary>
/// Milestone-3 entry point and, for now, a smoke test of the Core↔Godot seam: it loads the
/// data-driven content and runs a few turns of the Academy headlessly, printing to Godot's
/// output. No game logic lives here — Godot only reads from / drives the Core (Pillar 1).
/// The real Court UI (BuildSpec §6) replaces this in Milestone 3.
/// </summary>
public partial class Bootstrap : Node
{
    public override void _Ready()
    {
        // res:// is the project root, so /content sits beside it on disk in the editor.
        string contentRoot = ProjectSettings.GlobalizePath("res://content");

        var content = ContentLoader.LoadFromDirectory(contentRoot);
        var scenario = ScenarioLoader.LoadFromContent(contentRoot, "academy_crucible");
        var world = WorldBuilder.FromScenario(scenario, seed: 1);
        var session = new GameSession(world, content);

        GD.Print($"Praetoria Core online — scenario '{scenario.Id}', {content.Events.Count} events.");

        for (int t = 0; t < 6; t++)
        {
            session.AdvanceTurn();
            var fired = session.NextEvent();
            if (fired == null) continue;
            string choice = session.Offer(fired).Find(c => c.Available)!.Choice.Id;
            session.Resolve(fired, choice);
        }

        foreach (var h in world.History)
            GD.Print($"T{h.Turn}: {h.Text}");
    }
}
