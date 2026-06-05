using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Crises;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Progression;
using Praetoria.Core.Rng;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

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
        var (tc, content, world) = NewTurn(o);
        bool interactive = !o.Auto && !Console.IsInputRedirected;

        Console.WriteLine($"=== Praetoria — {o.Scenario} (seed {o.Seed}) ===");
        Console.WriteLine("Turn structure: Briefing → Action (spend pools) → Resolve (NPCs act).");
        Console.WriteLine(interactive ? "Enter a choice number, or 0/blank to skip a report.\n"
                                       : "Auto play (deterministic; first affordable choice each report).\n");

        for (int t = 0; t < o.Turns; t++)
        {
            var briefing = tc.BeginTurn();           // Briefing phase
            Renderer.RenderStatus(world);
            Renderer.RenderPools(tc.PlayerPools);
            var playerHouse = world.House(world.Protagonist!.HouseId);
            if (playerHouse != null && world.HoldingsOf(playerHouse.Id).Any())
            {
                Renderer.RenderTreasury(playerHouse);
                Renderer.RenderHoldings(world, playerHouse.Id);
            }

            foreach (var item in briefing)           // Action phase
            {
                var choices = tc.Offer(item);
                Renderer.RenderEvent(item.Fired, choices, content.Text(item.Fired.Def.Id), world);

                string? choiceId = interactive ? PromptChoiceOrSkip(choices) : FirstAffordable(choices);
                if (choiceId != null) tc.Resolve(item, choiceId);
                else Console.WriteLine("   (skipped)");
            }

            int npcFrom = tc.Executor.Log.Count;     // separate NPC actions from the player's
            tc.EndTurn();                            // Resolve phase (NPC actions)
            Renderer.RenderCommandLog(tc.Executor.Log, npcFrom);
        }

        Renderer.RenderEpilogueHints(world);
        return 0;
    }

    /// <summary>
    /// Demonstrates a full Milestone-2 turn cycle deterministically: the player spends pools on
    /// briefing items, NPC houses act through the same command bus in Resolve, and the run is
    /// summarised by its command log. Re-running with the same seed reproduces it exactly.
    /// </summary>
    public static int Turn(Options o)
    {
        var (tc, content, world) = NewTurn(o);
        Console.WriteLine($"=== TURN-CYCLE DEMO (seed {o.Seed}) ===\n");

        for (int t = 0; t < Math.Max(o.Turns, 5); t++)
        {
            var briefing = tc.BeginTurn();
            Console.WriteLine($"T{world.Turn}  BRIEFING — {briefing.Count} report(s); " +
                              $"pools I{tc.PlayerPools.Influence}/T{tc.PlayerPools.Treasury}/A{tc.PlayerPools.Agents}");

            foreach (var item in briefing)
            {
                var choiceId = FirstAffordable(tc.Offer(item));
                if (choiceId != null)
                {
                    bool ok = tc.Resolve(item, choiceId);
                    Console.WriteLine($"     ACTION  {item.Fired.Def.Id} → {choiceId}  ({(ok ? "spent" : "failed")})");
                }
                else
                {
                    Console.WriteLine($"     ACTION  {item.Fired.Def.Id} → (unaffordable, skipped)");
                }
            }

            int npcFrom = tc.Executor.Log.Count;     // everything after this is NPC action
            tc.EndTurn();
            for (int i = npcFrom; i < tc.Executor.Log.Count; i++)
                Console.WriteLine($"     RESOLVE {tc.Executor.Log[i]}");
        }

        Console.WriteLine($"\nDeterministic run complete. {tc.Executor.Log.Count} commands executed; " +
                          $"final RNG state {world.RngState}.");
        return 0;
    }

    /// <summary>
    /// Demonstrates the Milestone-4 domain economy headlessly (GDD §17): a house's treasury accrues
    /// from its holdings each turn, the player invests in a building through the command bus, and the
    /// new yield compounds. Deterministic — same seed, same ledger. Also shows the insolvency→unrest
    /// feedback if the treasury is run into the red.
    /// </summary>
    public static int Economy(Options o)
    {
        var (tc, content, world) = NewTurn(o);
        var house = world.House(world.Protagonist!.HouseId);
        if (house == null || !world.HoldingsOf(house.Id).Any())
        {
            Console.Error.WriteLine($"Scenario '{o.Scenario}' has no holdings for the protagonist's house.");
            return 1;
        }

        Console.WriteLine($"=== ECONOMY DEMO — {house.Name} (seed {o.Seed}) ===");
        Console.WriteLine("Treasury accrues from holdings each turn; we invest once it can afford a building.\n");

        bool built = false;
        for (int t = 0; t < Math.Max(o.Turns, 6); t++)
        {
            tc.BeginTurn();                         // accrual happens here
            Renderer.RenderTreasury(house);
            Renderer.RenderHoldings(world, house.Id);

            // Steward's hand: as soon as we can afford the first fitting, unbuilt building, build it.
            if (!built)
            {
                foreach (var holding in world.HoldingsOf(house.Id).OrderBy(h => h.Id, StringComparer.Ordinal))
                    foreach (var b in content.Holdings.Buildings.OrderBy(b => b.Id, StringComparer.Ordinal))
                    {
                        var cmd = new BuildCommand(world.ProtagonistId, holding.Id, b.Id, content.Holdings);
                        if (tc.Executor.TryExecute(cmd, new CommandContext(world, WorldBuilder.RngFor(world))))
                        {
                            Console.WriteLine($"   ↳ built {b.Name} at {holding.Name}");
                            built = true;
                            break;
                        }
                    }
            }

            tc.EndTurn();
            Console.WriteLine();
        }

        Console.WriteLine($"Deterministic ledger complete. Final credits: {house.Treasury.Credits}.");
        return 0;
    }

    /// <summary>
    /// Demonstrates the Milestone-5 progression system headlessly (GDD §13): two heirs reach for the
    /// same rung by different paths. The meritocrat is granted what he's legitimate for and rules
    /// secure; the conqueror seizes what he can't be granted and rules a powder keg — the soft-lock
    /// breeds instability until a contested-title crisis becomes causable. Deterministic.
    /// </summary>
    public static int Progression(Options o)
    {
        var content = LoadContent();
        if (content.Titles.IsEmpty) { Console.Error.WriteLine("No titles defined in /content/titles."); return 1; }
        var prog = new ProgressionSystem(content.Titles);
        var crises = content.Crises.Count == 0 ? null : new CrisisEngine(content.Crises);
        var rng = new SplitMix64Rng(o.Seed);

        World Heir(int legitimacy, int rank)
        {
            var w = new World { ProtagonistId = "heir" };
            w.Houses["house"] = new House { Id = "house", Name = "the House", Title = "baron", Legitimacy = legitimacy };
            w.Characters["heir"] = new Character { Id = "heir", Name = "the Heir", HouseId = "house", Alive = true, CareerTrack = "military", CareerRank = rank };
            w.Houses["house"].Members.Add("heir");
            w.Pools["heir"] = ActionPools.ForPlayer();
            return w;
        }
        string Name(string id) => content.Titles.ById(id)?.Name ?? id;
        void Line(World w) => Console.WriteLine($"     title {Name(w.House("house")!.Title)} · legitimacy " +
            $"{w.House("house")!.Legitimacy} · instability {w.Counter("title_instability")} · unrest {w.Counter("unrest")}");

        Console.WriteLine($"=== PROGRESSION DEMO — the soft-lock (seed {o.Seed}) ===\n");

        Console.WriteLine("— THE MERITOCRAT — high legitimacy (45); is GRANTED the County —");
        var m = Heir(45, 3);
        if (prog.CanPetition(m, "heir")) prog.Petition(m, "heir");
        prog.Apply(m); Line(m);
        Console.WriteLine("   He rules secure: legitimacy met the title, no instability.\n");

        Console.WriteLine("— THE CONQUEROR — low legitimacy (28); SEIZES County, then Duke by force —");
        var c = Heir(28, 4);
        prog.Seize(c, "heir");          // baron → count
        prog.Seize(c, "heir");          // count → duke
        Console.WriteLine($"   Seized to {Name(c.House("house")!.Title)} (seizures {c.Counter("seizures")} — the galaxy takes note).");
        for (int t = 1; t <= 5; t++)
        {
            prog.Apply(c);
            Console.Write($"   turn {t}: "); Line(c);
            if (crises != null && crises.IsCausable(crises.Def("contested_title")!, c, rng))
            {
                Console.WriteLine("     ‼  A Contested-Title crisis is now CAUSABLE — rivals press their own claims.");
                break;
            }
        }
        Console.WriteLine("\nBirth is a soft-lock, never a wall: take the throne, but rule a powder keg. (GDD §13)");
        return 0;
    }

    /// <summary>
    /// Demonstrates the Milestone-5 power-balance system headlessly (GDD §7): as a house's heir climbs
    /// the Navy career ladder, the family's Navy share spikes, its threat score climbs, and rivals
    /// begin to coordinate — until a coalition crisis (§16) becomes causable. Deterministic.
    /// </summary>
    public static int Spheres(Options o)
    {
        var content = LoadContent();
        if (content.Spheres.IsEmpty) { Console.Error.WriteLine("No spheres defined in /content/spheres."); return 1; }
        var sys = new SphereSystem(content.Spheres);
        var crises = content.Crises.Count == 0 ? null : new CrisisEngine(content.Crises);
        var rng = new SplitMix64Rng(o.Seed);

        var world = new World { ProtagonistId = "marcus" };
        foreach (var id in new[] { "vega", "corwin", "drake", "sato" })
            world.Houses[id] = new House { Id = id, Name = "House " + char.ToUpper(id[0]) + id[1..] };
        void Add(string id, string house, string track, int rank)
        {
            world.Characters[id] = new Character { Id = id, Name = id, HouseId = house, Alive = true, CareerTrack = track, CareerRank = rank };
            world.Houses[house].Members.Add(id);
        }
        Add("marcus", "vega", "military", 0);
        Add("lucan", "corwin", "military", 1);
        Add("sela", "drake", "law", 2);
        Add("ren", "sato", "stewardship", 2);

        Console.WriteLine($"=== POWER-BALANCE DEMO — career feeds spheres feeds coalitions (seed {o.Seed}) ===");
        Console.WriteLine("House Vega's heir climbs the Navy. Watch the galaxy grow afraid.\n");

        for (int rank = 1; rank <= 6; rank++)
        {
            world.Char("marcus")!.CareerRank = rank;
            sys.Recompute(world);
            int threat = world.Counter("threat");
            Console.WriteLine($"  Marcus → rank {rank}   Vega Navy share {sys.Share(world, "vega", "navy"):F0}%   " +
                              $"navy_influence {world.Counter("navy_influence")}   threat {threat}   " +
                              $"coalition_pressure {world.Counter("coalition_pressure")}" +
                              $"{(world.HasFlag("coalition_forming") ? "  ⚑ coalition forming" : "")}");

            if (crises != null && crises.IsCausable(crises.Def("coalition_war")!, world, rng))
            {
                Console.WriteLine("     ‼  A Coalition War is now CAUSABLE — the rivals have had enough.");
                break;
            }
        }

        Console.WriteLine("\nDominate one estate and the others gang up where you're weak. (GDD §7)");
        return 0;
    }

    /// <summary>
    /// Demonstrates the Milestone-5 crisis system headlessly (GDD §16): a revolt's gate clears, it
    /// erupts, brutal suppression CASCADES into a civil war, and whether a damper is even available
    /// depends on goodwill banked by prior play. Deterministic from a seed.
    /// </summary>
    public static int Crisis(Options o)
    {
        var content = LoadContent();
        var engine = new CrisisEngine(content.Crises);
        if (engine.IsEmpty) { Console.Error.WriteLine("No crises defined in /content/crises."); return 1; }

        var world = new World { ProtagonistId = "ruler" };
        world.Houses["realm"] = new House { Id = "realm", Name = "the Realm", Treasury = new Resources { Credits = 10 } };
        world.Characters["ruler"] = new Character { Id = "ruler", Name = "the Sovereign", HouseId = "realm", Alive = true };
        var rng = new SplitMix64Rng(o.Seed);

        Console.WriteLine($"=== CRISIS DEMO — gate / cascade / damper (seed {o.Seed}) ===\n");
        void Show(string label)
        {
            string causable = string.Join(", ", engine.Causable(world, rng).Select(c => c.Name));
            string active = world.Crises.Count == 0 ? "—"
                : string.Join(", ", world.Crises.Values.Select(c => $"{c.Id}(sev {c.Severity})"));
            Console.WriteLine($"   [{label}]  counters: unrest {world.Counter("unrest")} · legitimacy " +
                $"{world.Counter("legitimacy")} · goodwill {world.Counter("goodwill")}");
            Console.WriteLine($"      causable: {(causable.Length == 0 ? "—" : causable)}   active: {active}");
        }

        world.WorldCounters["unrest"] = 5;
        Console.WriteLine("Unrest reaches 5 — a revolt becomes possible (its gate clears).");
        Show("ripe");

        var revolt = engine.Def("local_revolt")!;
        engine.Trigger(revolt, world, rng);
        Console.WriteLine($"\n→ {revolt.Name} erupts.");
        Show("revolt");

        var crush = revolt.Dampers.First(d => d.Id == "crush_it");
        Console.WriteLine($"\nThe Sovereign chooses to {crush.Name} (it works — but spreads the unrest).");
        engine.ApplyDamper(revolt, crush, world, rng);
        Show("after crushing");

        var cw = engine.Def("civil_war")!;
        if (engine.IsCausable(cw, world, rng))
        {
            Console.WriteLine("\n‼  CASCADE: crushing the small revolt armed the Civil War gate — unscripted.");
            engine.Trigger(cw, world, rng);
            Show("civil war");

            var none = engine.AvailableDampers(cw, world, rng);
            Console.WriteLine($"\nDampers available (goodwill {world.Counter("goodwill")}): " +
                $"{(none.Count == 0 ? "NONE — the goodwill to rally loyalists was spent on the way up" : string.Join(", ", none.Select(d => d.Name)))}");

            world.WorldCounters["goodwill"] = 4;
            var avail = engine.AvailableDampers(cw, world, rng);
            Console.WriteLine($"Had the Sovereign ruled gently (goodwill 4): {string.Join(", ", avail.Select(d => d.Name))}");
            var rally = avail.First(d => d.Id == "rally_loyalists");
            Console.WriteLine($"→ {rally.Name} (relief {rally.Relief}) applied.");
            engine.ApplyDamper(cw, rally, world, rng);
            Console.WriteLine($"   Civil War still active? {world.IsCrisisActive("civil_war")}");
        }

        Console.WriteLine("\nThe escalation you face is partly the bill for how you played. (GDD §16)");
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

    private static (TurnController tc, ContentDatabase content, Praetoria.Core.State.World world) NewTurn(Options o)
    {
        var contentRoot = ContentLocator.FindContentDir();
        var content = ContentLoader.LoadFromDirectory(contentRoot);
        var scenario = ScenarioLoader.LoadFromContent(contentRoot, o.Scenario);
        var world = WorldBuilder.FromScenario(scenario, o.Seed);
        return (new TurnController(world, content), content, world);
    }

    private static string? FirstAffordable(IReadOnlyList<OfferedChoice> choices)
    {
        foreach (var c in choices) if (c.Available) return c.Choice.Id;
        return null;
    }

    private static string? PromptChoiceOrSkip(IReadOnlyList<OfferedChoice> choices)
    {
        while (true)
        {
            Console.Write("  > ");
            var line = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(line) || line.Trim() == "0") return null;
            if (int.TryParse(line, out var n) && n >= 1 && n <= choices.Count && choices[n - 1].Available)
                return choices[n - 1].Choice.Id;
            Console.WriteLine("  (enter a valid, affordable choice number, or 0 to skip)");
        }
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
