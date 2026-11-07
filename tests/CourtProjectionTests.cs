using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Presentation;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The Court UI's model, verified headless: what every column shows is consistent with the engine.</summary>
public class CourtProjectionTests
{
    private static (ContentDatabase content, TurnController tc) Start(string scenario, ulong seed = 1)
    {
        var root = ContentLocator.FindContentDir();
        var content = ContentLoader.LoadFromDirectory(root);
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, scenario), seed);
        var tc = new TurnController(w, content);
        tc.BeginTurn();
        return (content, tc);
    }

    [Theory]
    [InlineData("academy_crucible")]
    [InlineData("the_fallen_house")]
    [InlineData("the_circling_heir")]
    [InlineData("the_risen_officer")]
    [InlineData("the_imperial_court")]
    public void Model_IsConsistentWithTheEngine_ForEveryScenario(string scenario)
    {
        var (content, tc) = Start(scenario);
        var m = CourtProjection.Build(tc, content);
        var w = tc.World;

        Assert.Equal(w.Protagonist!.Name, m.Character.Name);
        Assert.Equal(1, m.Turn);
        Assert.False(m.DynastyDead);
        Assert.Equal(tc.Briefing.Count, m.Briefing.Count);
        Assert.Equal(tc.Briefing.Count == 0, m.Quiet);
        Assert.NotEmpty(m.Character.Nature);
        Assert.False(string.IsNullOrWhiteSpace(m.Character.RankName));

        // every choice's availability equals the engine's offer
        foreach (var (card, item) in m.Briefing.Zip(tc.Briefing))
        {
            Assert.Same(item, card.Item);
            Assert.False(string.IsNullOrWhiteSpace(card.Title));
            var offer = tc.Offer(item);
            Assert.Equal(offer.Count, card.Choices.Count);
            foreach (var (c, o) in card.Choices.Zip(offer))
            {
                Assert.Equal(o.Choice.Id, c.Id);
                Assert.Equal(o.Available, c.Available);
                Assert.DoesNotContain("{", c.Label);           // tokens substituted
            }
            Assert.DoesNotContain("{", card.Body);
        }

        // build options mirror BuildCommand legality; ships mirror RaiseFleetCommand
        var ctx = new CommandContext(w, TestContent.Rng());
        foreach (var h in m.Holdings)
        {
            foreach (var b in h.Builds)
                Assert.Equal(new BuildCommand(w.ProtagonistId, h.Id, b.BuildingId, content.Holdings).CanExecute(ctx), b.Available);
            Assert.Equal(new RaiseFleetCommand(w.ProtagonistId, h.Id, content.Fleets).CanExecute(ctx), h.CanRaiseShips);
            Assert.NotNull(h.SystemName);                           // every shipped holding is on the map
        }

        // members + connections partition the living cast (minus the protagonist)
        int living = w.LivingCharacters().Count() - 1;
        Assert.Equal(living, m.Members.Count(x => x.Alive) + m.Connections.Count);

        Assert.NotNull(m.Standing);
        Assert.Equal(w.House(w.Protagonist.HouseId)!.Legitimacy, m.Standing!.Legitimacy);
        Assert.NotNull(m.Crises);
        Assert.Equal(w.Fleets.Values.Count(f => f.OwnerId == w.Protagonist.HouseId), m.Fleets.Count);
        Assert.True(m.Chronicle.Count <= CourtProjection.ChronicleLines);
    }

    [Fact]
    public void ResolvingAChoice_MarksTheCardResolved_AndDropsItsChoices()
    {
        var (content, tc) = Start("academy_crucible");
        var m = CourtProjection.Build(tc, content);
        var card = m.Briefing.First(c => c.Choices.Any(x => x.Available));
        tc.Resolve(card.Item, card.Choices.First(x => x.Available).Id);

        var after = CourtProjection.Build(tc, content);
        var same = after.Briefing.First(c => c.Item == card.Item);
        Assert.True(same.Resolved);
        Assert.Empty(same.Choices);
    }

    [Fact]
    public void DynastyDeath_IsSurfaced()
    {
        var (content, tc) = Start("the_risen_officer");
        var w = tc.World;
        foreach (var c in w.Characters.Values.Where(c => c.HouseId == w.Protagonist!.HouseId && c.Id != w.ProtagonistId)) c.Alive = false;
        DynastySystem.Die(w, w.ProtagonistId);
        var m = CourtProjection.Build(tc, content);
        Assert.True(m.DynastyDead);
    }

    [Fact]
    public void Background_IsNamedOnTheCard()
    {
        var root = ContentLocator.FindContentDir();
        var content = ContentLoader.LoadFromDirectory(root);
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, "the_fallen_house"), 1);
        Praetoria.Core.Creation.CharacterCreation.Apply(w, content, new Praetoria.Core.Creation.CreationOptions { BackgroundId = "court_ward" });
        var tc = new TurnController(w, content);
        tc.BeginTurn();
        Assert.Equal("Ward of the Court", CourtProjection.Build(tc, content).Character.Background);
    }
}
