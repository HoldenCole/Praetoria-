using Praetoria.Core.Data;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The colour rule of docs/VISUAL_SYSTEM.md 2.2: gold means yours and red means danger, so no rival house wears gold, amber,
/// yellow or a bright red. The player's house is gold.</summary>
public class PaletteTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());

    [Theory]
    [InlineData("#D4A23E", false)]   // the player's gold
    [InlineData("#FFD60A", false)]   // yellow
    [InlineData("#FF8C42", true)]    // orange is not in the house list, but the rule itself only bars 28 to 70 degrees
    [InlineData("#F5B700", false)]   // gold
    [InlineData("#E63946", false)]   // alarm red
    [InlineData("#A3324A", true)]    // oxblood: a red at value 0.64
    [InlineData("#E7849B", true)]    // rose: a pale red, not an alarm
    [InlineData("#3A86FF", true)]    // blue
    [InlineData("#C9D1DC", true)]    // silver: grey enough to pass at any hue
    [InlineData("#9E3350", true)]    // the Ardent Concord, toned
    [InlineData("#7E9C6A", true)]    // the Vashti Khanates, toned
    public void TheRuleBarsGoldYellowAndBrightRed(string hex, bool allowed) => Assert.Equal(allowed, Palette.RivalAllowed(hex));

    [Fact]
    public void EveryRivalColourKeepsTheRule()
    {
        Assert.Equal(24, Palette.Houses.Length);
        Assert.Equal(Palette.Houses.Length, Palette.Houses.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var c in Palette.Houses) Assert.True(Palette.RivalAllowed(c), c + " breaks the colour rule");
        // and none is ivory, which is the Reach's edge on the map
        foreach (var c in Palette.Houses) { var (_, s, v) = Palette.Hsv(c); Assert.False(s < 0.2 && v > 0.9, c + " reads as ivory"); }
    }

    [Fact]
    public void ThePlayerIsGoldAndNoRivalIsInAnyStart()
    {
        foreach (var scenario in new[] { "the_old_lion", "the_new_man", "the_fallen_house", "the_imperial_court" })
        {
            var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), 1);
            NationSystem.Seed(w, Content.Nations);
            var mine = w.Protagonist!.HouseId;
            Assert.Equal(Palette.Player, w.Houses[mine].AccentColor);
            foreach (var h in w.Houses.Values.Where(h => h.Id != mine && h.Id != PirateSystem.HouseId))
                Assert.True(Palette.RivalAllowed(h.AccentColor), $"{scenario}: {h.Name} wears {h.AccentColor}");
        }
    }

    [Fact]
    public void ANewHouseTakesAFreeRivalColour()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 1);
        // the Reach holds more houses than the list has colours: free one, and the newcomer takes it, never the player's gold
        foreach (var h in w.Houses.Values.Where(h => h.AccentColor == Palette.Houses[5])) h.AccentColor = "#000000";
        var c = Palette.NextHouseColor(w, "house_new");
        Assert.Equal(Palette.Houses[5], c);
        Assert.NotEqual(Palette.Player, Palette.NextHouseColor(new World(), "house_other"));
    }

    [Fact]
    public void TheCreatorOffersGoldFirst()
    {
        Assert.Equal(Palette.Player, Palette.Choices[0]);
        Assert.All(Palette.Choices.Skip(1), c => Assert.True(Palette.RivalAllowed(c)));
    }
}
