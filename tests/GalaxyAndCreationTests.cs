using Praetoria.Core.Creation;
using Praetoria.Core.Data;
using Praetoria.Core.Galaxy;
using Praetoria.Core.State;
using Xunit;

namespace Praetoria.Tests;

public class GalaxyTests
{
    private static (ContentDatabase content, string root) Load()
    {
        var root = ContentLocator.FindContentDir();
        return (ContentLoader.LoadFromDirectory(root), root);
    }

    private static Galaxy Triangle()
    {
        var g = new Galaxy { Id = "t" };
        foreach (var id in new[] { "a", "b", "c", "d" }) g.Systems[id] = new StarSystem { Id = id, Name = id };
        g.Lanes.Add(new Hyperlane { A = "a", B = "b" });
        g.Lanes.Add(new Hyperlane { A = "b", B = "c" });
        return g;                                   // d is an island
    }

    [Fact]
    public void Neighbors_AreUndirected_AndOrdered()
    {
        var g = Triangle();
        Assert.Equal(new[] { "a", "c" }, g.Neighbors("b"));
        Assert.Empty(g.Neighbors("d"));
    }

    [Fact]
    public void Hops_CountsLanes_AndMarksTheUnreachable()
    {
        var g = Triangle();
        Assert.Equal(0, g.Hops("a", "a"));
        Assert.Equal(2, g.Hops("a", "c"));
        Assert.Equal(-1, g.Hops("a", "d"));
        Assert.Equal(-1, g.Hops("a", "nowhere"));
    }

    [Fact]
    public void EveryShippedScenario_HasAGalaxy_AndEveryHoldingSitsInAKnownSystem()
    {
        var (_, root) = Load();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "scenarios"), "*.json"))
        {
            var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFile(file), 1);
            Assert.NotNull(w.Galaxy);
            Assert.Empty(w.Galaxy!.Problems());
            foreach (var h in w.Holdings.Values)
                Assert.True(w.Galaxy.System(h.SystemId) != null, $"{Path.GetFileName(file)}: {h.Id} in '{h.SystemId}'");
            // the whole map is one connected component (no stranded systems)
            var ids = w.Galaxy.Systems.Keys.ToList();
            foreach (var id in ids) Assert.True(w.Galaxy.Hops(ids[0], id) >= 0, $"{id} is unreachable");
        }
    }

    [Fact]
    public void WorldBuilder_RejectsAHoldingInAnUnknownSystem()
    {
        var scenario = new Scenario
        {
            Id = "x", ProtagonistId = "p",
            Houses = new[] { new House { Id = "h" } },
            Characters = new[] { new Character { Id = "p", HouseId = "h" } },
            Holdings = new[] { new Holding { Id = "hold", OwnerId = "h", SystemId = "nowhere" } },
            Galaxy = Triangle()
        };
        Assert.Throws<ContentException>(() => WorldBuilder.FromScenario(scenario, 1));
    }

    [Fact]
    public void Projection_DerivesTerritory_FromHoldings_WithPlayerAndContestedMarks()
    {
        var (_, root) = Load();
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, "academy_crucible"), 1);
        var nodes = GalaxyProjection.Nodes(w).ToDictionary(n => n.SystemId);

        Assert.Equal("vega", nodes["tessaly"].OwnerHouseId);
        Assert.True(nodes["tessaly"].IsPlayerTerritory);
        Assert.Equal(w.House("vega")!.AccentColor, nodes["tessaly"].AccentColor);
        Assert.Null(nodes["the_veil"].OwnerHouseId);                      // frontier
        Assert.Equal(GalaxyProjection.UnclaimedAccent, nodes["the_veil"].AccentColor);
        Assert.Equal(new[] { "tessaly" }, GalaxyProjection.TerritoryOf(w, "vega"));

        // a second house plants a holding in Vega's system → contested, majority rules
        w.Holdings["intruder"] = new Holding { Id = "intruder", OwnerId = "corwin", SystemId = "tessaly" };
        var tessaly = GalaxyProjection.Nodes(w).First(n => n.SystemId == "tessaly");
        Assert.True(tessaly.Contested);
        Assert.Equal("corwin", tessaly.OwnerHouseId);                       // tie → ordinal id order ("corwin" < "vega")
    }

    [Fact]
    public void Galaxy_SurvivesASave()
    {
        var (_, root) = Load();
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, "the_imperial_court"), 1);
        var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        Assert.Equal(w.Galaxy!.Systems.Count, loaded.Galaxy!.Systems.Count);
        Assert.Equal(w.Galaxy.Lanes.Count, loaded.Galaxy.Lanes.Count);
        Assert.Equal(w.Galaxy.Hops("lesser_marrow", "aurelia_prime"), loaded.Galaxy.Hops("lesser_marrow", "aurelia_prime"));
    }
}

public class CharacterCreationTests
{
    private static (ContentDatabase content, World world) Fresh(string scenario = "the_risen_officer")
    {
        var root = ContentLocator.FindContentDir();
        var content = ContentLoader.LoadFromDirectory(root);
        return (content, WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, scenario), 1));
    }

    [Fact]
    public void Catalogs_LoadTheLockedVocabularies_AndTheBackgrounds()
    {
        var (content, _) = Fresh();
        Assert.Equal(9, content.Traits.Nature.Count);
        Assert.Equal(11, content.Traits.Aptitude.Count);
        Assert.Equal(8, content.Traits.Ambitions.Count);
        Assert.True(content.Traits.IsNature("Honorable"));
        Assert.False(content.Traits.IsNature("Brilliant"));
        Assert.Equal(6, content.Backgrounds.Defs.Count);
        Assert.NotNull(content.Backgrounds.ById("upjumped"));
    }

    [Fact]
    public void EmptyOptions_ChangeNothing()
    {
        var (content, w) = Fresh();
        var p = w.Protagonist!; var house = w.House(p.HouseId)!;
        var (name, sex, nature, aptitude, track, ambition, legit, credits) =
            (p.Name, p.Sex, p.NatureTraits.Count, p.AptitudeTraits.Count, p.CareerTrack, p.Ambition, house.Legitimacy, house.Treasury.Credits);

        CharacterCreation.Apply(w, content, new CreationOptions());

        Assert.Equal((name, sex, nature, aptitude, track, ambition, legit, credits),
            (p.Name, p.Sex, p.NatureTraits.Count, p.AptitudeTraits.Count, p.CareerTrack, p.Ambition, house.Legitimacy, house.Treasury.Credits));
        Assert.Single(w.History);                                            // only the chronicle line
    }

    [Fact]
    public void ABackground_IsADelta_OnTheScenariosProtagonistAndHouse()
    {
        var (content, w) = Fresh();                       // Kessa: Ambitious/Just, Tactician/Brilliant, vane legit 5, credits 4
        var house = w.House("vane")!;
        int legit = house.Legitimacy, credits = house.Treasury.Credits, discipline = w.Protagonist!.Skill("discipline");

        CharacterCreation.Apply(w, content, new CreationOptions { BackgroundId = "upjumped" });

        Assert.Equal(Math.Max(0, legit - 10), house.Legitimacy);
        Assert.Equal(credits - 2, house.Treasury.Credits);
        Assert.Equal(discipline + 1, w.Protagonist!.Skill("discipline"));
        Assert.Contains("Brilliant", w.Protagonist.AptitudeTraits);       // background adds on top
        Assert.Contains("background:upjumped", w.Protagonist.Flags);
        Assert.Equal("earn_a_command", w.Protagonist.Ambition);            // the hint applied (no explicit pick)
    }

    [Fact]
    public void Picks_ReplaceTheScenarioDefaults_AndHintsNeverOverrideThem()
    {
        var (content, w) = Fresh();
        CharacterCreation.Apply(w, content, new CreationOptions
        {
            Name = "Vale", Sex = "male", BackgroundId = "merchant_scion",
            Nature = new() { "Honorable", "Vengeful" }, Aptitude = new() { "Orator" },
            CareerTrack = "law", Ambition = "seize_the_throne"
        });
        var p = w.Protagonist!;
        Assert.Equal("Vale", p.Name);
        Assert.Equal("male", p.Sex);
        Assert.Equal(new[] { "Honorable", "Vengeful" }, p.NatureTraits.OrderBy(t => t).ToArray());
        Assert.Equal(new[] { "Greedy", "Orator" }, p.AptitudeTraits.OrderBy(t => t).ToArray());   // pick + background
        Assert.Equal("law", p.CareerTrack);                                 // not the merchant's stewardship hint
        Assert.Equal("seize_the_throne", p.Ambition);
        Assert.Contains(w.History, h => h.Text.Contains("Vale of Vane takes the stage"));
    }

    [Theory]
    [InlineData("nature", "Brave")]            // not a locked trait
    [InlineData("aptitude", "Honorable")]      // a nature trait offered as aptitude
    [InlineData("background", "princeling")]
    [InlineData("track", "spymaster")]
    [InlineData("ambition", "become_legend")]
    [InlineData("sex", "other")]
    public void UnknownChoices_AreRejected(string field, string value)
    {
        var (content, w) = Fresh();
        var o = new CreationOptions();
        switch (field)
        {
            case "nature": o.Nature.Add(value); break;
            case "aptitude": o.Aptitude.Add(value); break;
            case "background": o.BackgroundId = value; break;
            case "track": o.CareerTrack = value; break;
            case "ambition": o.Ambition = value; break;
            case "sex": o.Sex = value; break;
        }
        Assert.Throws<ContentException>(() => CharacterCreation.Apply(w, content, o));
    }

    [Fact]
    public void TooManyPicks_AreRejected()
    {
        var (content, w) = Fresh();
        Assert.Throws<ContentException>(() => CharacterCreation.Apply(w, content,
            new CreationOptions { Nature = new() { "Proud", "Just", "Loyal" } }));
    }

    [Fact]
    public void ACustomisedRun_StillPlaysDeterministically()
    {
        string Run()
        {
            var (content, w) = Fresh("the_fallen_house");
            CharacterCreation.Apply(w, content, new CreationOptions { BackgroundId = "old_blood", Nature = new() { "Proud", "Honorable" } });
            var tc = new Praetoria.Core.Systems.TurnController(w, content);
            for (int t = 0; t < 8; t++)
            {
                foreach (var item in tc.BeginTurn())
                {
                    var c = tc.Offer(item).FirstOrDefault(x => x.Available);
                    if (c != null) tc.Resolve(item, c.Choice.Id);
                }
                tc.EndTurn();
            }
            return SaveSystem.Serialize(w);
        }
        Assert.Equal(Run(), Run());
    }
}
