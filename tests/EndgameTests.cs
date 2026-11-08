using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

public class EndgameTests
{
    private static (ContentDatabase content, World world) Fresh(string scenario, ulong seed = 1)
    {
        var root = ContentLocator.FindContentDir();
        var content = ContentLoader.LoadFromDirectory(root);
        return (content, WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, scenario), seed));
    }

    [Fact]
    public void Proclaiming_NeedsAnArchduke_WithTheEmperorsLegitimacy_InTheFracturedStars()
    {
        var (content, w) = Fresh("academy_crucible");
        var ctx = new CommandContext(w, TestContent.Rng());
        var cmd = new ProclaimImperiumCommand(w.ProtagonistId, content.Titles);
        Assert.Equal("only an Archduke may proclaim", cmd.Refusal(ctx));
        w.House("vega")!.Title = "archduke";
        Assert.StartsWith("the realm will not accept it", cmd.Refusal(ctx));
        w.House("vega")!.Legitimacy = 95;
        Assert.EndsWith("has not knelt", cmd.Refusal(ctx));                    // Aurelian holds the capital (GDD §19)
        var ours = w.Protagonist!; var theirs = Economy.HeadOf(w, w.House("aurelian")!)!;
        w.Relationship(theirs.Id, ours.Id).Disposition = 25;                  // and now it kneels
        Assert.Null(cmd.Refusal(ctx));

        Assert.True(new CommandExecutor().TryExecute(cmd, ctx));
        Assert.Equal("imperium", w.Era);
        Assert.Equal("emperor", w.House("vega")!.Title);
        Assert.True(w.HasFlag(ThroneSystem.ProclaimedFlag));
        Assert.Equal(100, w.House("vega")!.Legitimacy);
        // the era flip opens the Imperial-court content for this world
        var eligible = new EventEngine(content, WorldBuilder.RngFor(w)).GatherEligible(w).Select(e => e.def.Id).ToHashSet();
        Assert.Contains("the_proclamation", eligible);
        Assert.Contains("the_imperial_levy", eligible);                       // imperium-only content, now live
    }

    [Fact]
    public void Proclaiming_IsRefused_UnderAStandingImperium()
    {
        var (content, w) = Fresh("the_imperial_court");
        var ctx = new CommandContext(w, TestContent.Rng());
        Assert.Equal("a throne already stands — it must be seized", new ProclaimImperiumCommand(w.ProtagonistId, content.Titles).Refusal(ctx));
    }

    [Fact]
    public void SeizingTheThrone_NeedsFleetDominance_AndAWonWar_AndDeposesTheEmperor()
    {
        var (content, w) = Fresh("the_imperial_court");             // Thorne knight; Aurelian emperor with a 6-hull office fleet
        var ctx = new CommandContext(w, TestContent.Rng());
        var cmd = new SeizeThroneCommand(w.ProtagonistId, content.Titles, content.Friction);
        Assert.Equal("a Duke at least must stand behind the fleet", cmd.Refusal(ctx));
        w.House("thorne")!.Title = "duke";
        Assert.StartsWith("your fleet must outnumber", cmd.Refusal(ctx));
        w.Fleets["fleet_thorne"] = new Fleet { Id = "fleet_thorne", Name = "Thorne Grand Fleet", OwnerId = "thorne", SystemId = "lesser_marrow", Strength = 9 };
        Assert.StartsWith("you must be at war with", cmd.Refusal(ctx));
        var fleets = new FleetSystem(content.Fleets);
        fleets.DeclareWar(w, "thorne", "aurelian");
        Assert.StartsWith("you must be winning", cmd.Refusal(ctx));
        w.Wars[0].AddScore("thorne", 6);
        Assert.StartsWith("the throne stands at", cmd.Refusal(ctx));          // the fleet is at Lesser Marrow, not Aurelia Prime
        w.Fleets["fleet_thorne"].SystemId = "aurelia_prime";
        Assert.Null(cmd.Refusal(ctx));

        Assert.True(new CommandExecutor().TryExecute(cmd, ctx));
        Assert.Equal("emperor", w.House("thorne")!.Title);
        Assert.Equal("archduke", w.House("aurelian")!.Title);
        Assert.Equal("thorne", w.Fleets["fleet_imperial_home"].OwnerId);       // office fleets pass with the office
        Assert.Contains(ThroneSystem.DeposedFlag, w.Char("octavian_aurelian")!.Flags);
        Assert.False(w.AtWar("thorne", "aurelian"));
        Assert.True(w.HasFlag(ThroneSystem.SeizedFlag));
        Assert.True(w.Counter("seizures") >= 3);                              // the galaxy's fear peaks
        Assert.True(w.Counter("coalition_pressure") >= SphereSystem.MaxCoalitionPressure - 1);
        Assert.Equal(14, w.House("thorne")!.Legitimacy);                      // unchanged: a powder keg
    }

    [Fact]
    public void TheEpilogue_TellsTheStory()
    {
        var (content, w) = Fresh("academy_crucible");
        var tc = new TurnController(w, content);
        for (int t = 0; t < 10; t++) { foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); } tc.EndTurn(); }
        string text = Epilogue.Generate(w, content);
        Assert.Contains("THE CHRONICLE OF HOUSE VEGA", text);
        Assert.Contains("The story is not over", text);
        w.WorldFlags.Add(World.DynastyDeadFlag);
        Assert.Contains("no one left to carry the name", Epilogue.Generate(w, content));
        w.WorldFlags.Remove(World.DynastyDeadFlag); w.WorldFlags.Add(ThroneSystem.SeizedFlag);
        Assert.Contains("took the throne itself", Epilogue.Generate(w, content));
    }

    [Fact]
    public void Director_NeverStacksTwoSetpieces_AndFavoursTheAmbition()
    {
        var (content, w) = Fresh("the_risen_officer");               // Kessa: earn_a_command
        var engine = new EventEngine(content, WorldBuilder.RngFor(w));
        var director = new Director();
        var firstCommand = content.Event("first_command")!;           // tagged earn_a_command
        var governor = content.Event("the_governors_seat")!;          // tagged amass_a_fortune
        Assert.True(director.WeightOf(firstCommand, w) > firstCommand.Weight);
        Assert.True(director.WeightOf(governor, w) < governor.Weight);

        for (ulong seed = 1; seed <= 20; seed++)
        {
            var (_, w2) = Fresh("the_imperial_court", seed);
            w2.Turn = 12;
            var briefing = new EventEngine(content, WorldBuilder.RngFor(w2)).SelectBriefing(w2, 3);
            Assert.True(briefing.Count(f => f.Def.Tier == EventTier.Setpiece) <= 1);
        }
    }

    [Fact]
    public void ALull_LiftsSituations()
    {
        var (content, w) = Fresh("academy_crucible");
        var director = new Director();
        var def = content.Event("the_lean_season")!;                  // a situation, no ambition tag
        double quiet = director.WeightOf(def, w);
        w.WorldCounters[Director.LullCounter] = 4;
        Assert.True(director.WeightOf(def, w) > quiet);
        var ambient = content.Event("mess_hall")!;
        Assert.Equal(ambient.Weight, director.WeightOf(ambient, w), 6);
    }

    [Fact]
    public void TheWeddingFeast_IsABloodyEvent()
    {
        var (content, w) = Fresh("the_fallen_house");                // Aldric wed; Mira is blood kin; Drusus hostile
        w.Turn = 9;
        w.Char("harrow_drusus")!.Flags.Add("feud");
        w.Relationship("harrow_drusus", "aldric_voss").Disposition = -45;
        w.Protagonist!.NatureTraits.Add("Ruthless");
        var engine = new EventEngine(content, WorldBuilder.RngFor(w));
        var feast = engine.GatherEligible(w).First(e => e.def.Id == "the_wedding_feast");
        Assert.Equal("harrow_drusus", feast.binding.Resolve("enemy"));
        var fired = new FiredEvent(feast.def, feast.binding);
        Assert.True(engine.IsChoiceAvailable(fired, w, "spring_the_trap"));
        engine.Resolve(fired, w, "spring_the_trap");
        Assert.False(w.Char("harrow_drusus")!.Alive);
        Assert.True(w.HasFlag("realm_unstable"));
    }
}
