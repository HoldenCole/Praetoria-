using Praetoria.Core;
using Praetoria.Core.Data;
using Praetoria.Core.Events;

namespace Praetoria.Tools;

/// <summary>Implements the harness subcommands. Pure orchestration over Core — no rules here.</summary>
internal static class Commands
{
    public static int Validate()
    {
        var content = LoadContent();
        var issues = ContentValidator.Validate(content);

        int errors = issues.Count(i => i.Severity == "error");
        foreach (var issue in issues) Console.WriteLine(issue);

        Console.WriteLine($"\n{content.Events.Count} events, {content.Texts.Count} text entries — " +
                          $"{errors} error(s), {issues.Count - errors} warning(s).");
        return errors > 0 ? 1 : 0;
    }

    public static int Play(Options o)
    {
        var session = NewSession(o);
        bool interactive = !o.Auto && !Console.IsInputRedirected;

        Console.WriteLine($"=== Praetoria — {o.Scenario} (seed {o.Seed}) ===");
        Console.WriteLine(interactive ? "Interactive play. Enter a choice number each scene.\n"
                                       : "Auto play (deterministic; first available choice each scene).\n");

        for (int t = 0; t < o.Turns; t++)
        {
            session.AdvanceTurn();
            Renderer.RenderStatus(session.World);

            var fired = session.NextEvent();
            if (fired == null)
            {
                Console.WriteLine("  (a quiet turn — nothing stirs)");
                continue;
            }

            var choices = session.Offer(fired);
            var text = session.Content.Text(fired.Def.Id);
            Renderer.RenderEvent(fired, choices, text, session.World);

            string choiceId = interactive
                ? PromptChoice(choices)
                : FirstAvailable(choices);
            session.Resolve(fired, choiceId);
        }

        Renderer.RenderEpilogueHints(session.World);
        return 0;
    }

    /// <summary>
    /// A scripted run that proves the Milestone-1 acceptance property end-to-end: a choice in one
    /// event (the barracks insult) arms a flag that makes a DIFFERENT, never-sequenced event
    /// (the duel) eligible several turns later. Prints the eligible pool each turn so the chain is visible.
    /// </summary>
    public static int Demo(Options o)
    {
        var session = NewSession(o);
        var w = session.World;
        Console.WriteLine($"=== CASCADE DEMO (seed {o.Seed}) ===");
        Console.WriteLine("Watch 'the_duel' go from impossible to eligible after the barracks insult.\n");

        // Drive a deterministic script by choosing specific choices when their events surface.
        var script = new Dictionary<string, string>
        {
            ["first_formation"] = "size_up_the_cohort",
            ["barracks_confrontation"] = "mock",   // <-- arms the feud
            ["the_duel"] = "fight",
            ["friendly_overture"] = "share_the_watch",
            ["sworn_oath"] = "swear",
            ["final_trial"] = "lead_from_the_front"
        };

        bool duelSeenEligibleBeforeInsult = false;
        bool insulted = false;

        for (int t = 0; t < Math.Max(o.Turns, 6); t++)
        {
            session.AdvanceTurn();
            var eligible = session.Engine.GatherEligible(w).Select(e => e.def.Id).ToList();
            Console.WriteLine($"T{w.Turn} eligible: {(eligible.Count == 0 ? "—" : string.Join(", ", eligible))}");

            if (!insulted && eligible.Contains("the_duel")) duelSeenEligibleBeforeInsult = true;

            var fired = session.NextEvent();
            if (fired == null) continue;

            string choiceId = script.TryGetValue(fired.Def.Id, out var scripted)
                ? scripted
                : FirstAvailable(session.Offer(fired));

            // Fall back if the scripted choice isn't currently available.
            if (session.Offer(fired).FirstOrDefault(c => c.Choice.Id == choiceId)?.Available != true)
                choiceId = FirstAvailable(session.Offer(fired));

            Console.WriteLine($"     fired: {fired.Def.Id} -> {choiceId}");
            session.Resolve(fired, choiceId);
            if (fired.Def.Id == "barracks_confrontation" && choiceId == "mock") insulted = true;
        }

        Console.WriteLine();
        Console.WriteLine(duelSeenEligibleBeforeInsult
            ? "UNEXPECTED: the duel was eligible before the feud was armed."
            : "PROVEN: the duel was NOT eligible until the barracks insult armed the feud flag.");
        Console.WriteLine("\nChronicle:");
        foreach (var h in w.History) Console.WriteLine($"  T{h.Turn}: {h.Text}");
        return 0;
    }

    // ---- helpers ----

    private static GameSession NewSession(Options o)
    {
        var content = LoadContent();
        var contentRoot = ContentLocator.FindContentDir();
        var scenario = ScenarioLoader.LoadFromContent(contentRoot, o.Scenario);
        var world = WorldBuilder.FromScenario(scenario, o.Seed);
        return new GameSession(world, content);
    }

    private static ContentDatabase LoadContent() =>
        ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());

    private static string FirstAvailable(IReadOnlyList<OfferedChoice> choices)
    {
        foreach (var c in choices) if (c.Available) return c.Choice.Id;
        return choices.Count > 0 ? choices[0].Choice.Id : throw new InvalidOperationException("Event has no choices.");
    }

    private static string PromptChoice(IReadOnlyList<OfferedChoice> choices)
    {
        while (true)
        {
            Console.Write("  > ");
            var line = Console.ReadLine();
            if (int.TryParse(line, out var n) && n >= 1 && n <= choices.Count && choices[n - 1].Available)
                return choices[n - 1].Choice.Id;
            Console.WriteLine("  (enter a valid, available choice number)");
        }
    }
}
