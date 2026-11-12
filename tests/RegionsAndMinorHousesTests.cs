using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Galaxy;
using Praetoria.Core.State;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The expanded Reach (GDD §19): six regions of three systems, every world held by someone — great houses from
/// the scenario, minor houses from the shared baseline — and governors in every chair.</summary>
public class RegionsAndMinorHousesTests
{
    private static (ContentDatabase content, World world) Fresh(string scenario, ulong seed = 1)
    {
        var root = ContentLocator.FindContentDir();
        var content = ContentLoader.LoadFromDirectory(root);
        return (content, WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, scenario), seed));
    }

    [Fact]
    public void TheReach_HasTenRegionsOfThreeOrFourSystems_InThreeRings()
    {
        var (_, w) = Fresh("the_circling_heir");
        var g = w.Galaxy!;
        Assert.Equal(31, g.Systems.Count);
        Assert.Equal(10, g.Regions.Count);
        foreach (var r in g.Regions) Assert.InRange(g.SystemsIn(r.Id).Count(), 3, 4);
        Assert.Equal(2, g.Regions.Count(r => r.Ring == "core")); Assert.Equal(4, g.Regions.Count(r => r.Ring == "middle")); Assert.Equal(4, g.Regions.Count(r => r.Ring == "rim"));
        Assert.All(g.Systems.Values, s => Assert.NotNull(g.Region(s.RegionId)));
        Assert.Equal("aurelian", g.CultureOf("castrum"));
        Assert.Empty(g.Problems());
        foreach (var a in g.Systems.Keys) foreach (var b in g.Systems.Keys) Assert.True(g.Hops(a, b) >= 0, $"{a} cannot reach {b}");
    }

    [Theory]
    [InlineData("academy_crucible")] [InlineData("the_circling_heir")] [InlineData("the_fallen_house")]
    [InlineData("the_imperial_court")] [InlineData("the_old_lion")] [InlineData("the_risen_officer")]
    public void EveryWorld_IsHeld_AndGoverned_InEveryScenario(string scenario)
    {
        var (_, w) = Fresh(scenario);
        // every world is held, except the lawless worlds of the rim that no house has held in living memory (GDD §19)
        foreach (var sys in w.Galaxy!.Systems.Keys)
        {
            var planets = w.Holdings.Values.Where(h => h.SystemId == sys && h.IsPlanet).ToList();
            if (planets.Count == 0) { Assert.Equal("rim", w.Galaxy.RingOf(sys)); continue; }
            Assert.Single(planets.Select(p => p.OwnerId).Distinct());
        }
        Assert.Equal(2, w.Galaxy.Systems.Keys.Count(sys => !w.Holdings.Values.Any(h => h.SystemId == sys && h.IsPlanet)));
        foreach (var h in w.Holdings.Values)
        {
            Assert.True(w.Houses.ContainsKey(h.OwnerId));
            Assert.True(h.GovernorId.Length > 0 && w.Char(h.GovernorId)?.Alive == true, $"{h.Id} has no governor");
            Assert.Equal(h.OwnerId, w.Char(h.GovernorId)!.HouseId);
            Assert.True(h.Culture.Length > 0);
        }
        Assert.True(w.Houses.Count >= 15, $"{scenario}: only {w.Houses.Count} houses");
        var own = ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario, includeBaseline: false).Houses.Select(h => h.Id).ToHashSet();
        Assert.DoesNotContain(w.Houses.Values, h => !own.Contains(h.Id) && h.Title != "landless" && !w.HoldingsOf(h.Id).Any());   // no titled minor house arrives already dispossessed
        Assert.Contains(w.Houses.Values, h => h.Title == "landless" && !w.HoldingsOf(h.Id).Any());   // the landless minor houses
        Assert.Contains(w.Holdings.Values, h => !h.IsPlanet);                                           // and the smaller things
    }

    [Fact]
    public void ScenarioDefinitions_WinOverTheBaseline()
    {
        var (_, w) = Fresh("the_circling_heir");
        Assert.Equal("House Thalen", w.House("thalen")!.Name);
        Assert.Contains("edric_thalen", w.House("thalen")!.Members);                // the scenario's Thalen, not the baseline's
        Assert.DoesNotContain("thalen_head", w.Characters.Keys);
        Assert.Single(w.Holdings.Values.Where(h => h.SystemId == "cinder_gate" && h.IsPlanet));
        Assert.Equal("serval", w.Holdings.Values.Single(h => h.SystemId == "highmarch" && h.IsPlanet).OwnerId);
    }

    [Fact]
    public void Regions_Project_WithDominantHouse()
    {
        var (_, w) = Fresh("the_old_lion");
        var regions = GalaxyProjection.Regions(w);
        Assert.Equal(10, regions.Count);
        var grey = regions.Single(r => r.Id == "greywater_reach");
        Assert.Equal("castellan", grey.DominantHouseId);     // the Castellans hold two of its three worlds
        Assert.Equal(3, grey.Planets);
        var nodes = GalaxyProjection.Nodes(w);
        Assert.All(nodes.Where(n => n.Ring != "rim"), n => Assert.NotNull(n.OwnerHouseId));
        Assert.Equal(2, nodes.Count(n => n.OwnerHouseId == null));
        Assert.Contains(nodes, n => n.Stations > 0);
        Assert.DoesNotContain(nodes, n => n.Contested);     // stations do not contest a system
    }

    [Fact]
    public void AppointGovernor_SeatsKin_AndTheDisplacedResentIt()
    {
        var (_, w) = Fresh("the_old_lion");
        var ctx = new CommandContext(w, TestContent.Rng());
        var seat = w.Holding("castellan_seat")!;
        string before = seat.GovernorId;
        var cmd = new AppointGovernorCommand(w.ProtagonistId, seat.Id, "corvin_castellan");
        Assert.Null(cmd.Refusal(ctx));
        Assert.True(new CommandExecutor().TryExecute(cmd, ctx));
        Assert.Equal("corvin_castellan", seat.GovernorId);
        Assert.Contains(w.History, h => h.Text.Contains("governor of"));
        Assert.Equal("already governs it", new AppointGovernorCommand(w.ProtagonistId, seat.Id, "corvin_castellan").Refusal(ctx));
        Assert.Equal("not of your house", new AppointGovernorCommand(w.ProtagonistId, seat.Id, "maren_oriel").Refusal(ctx));
        Assert.Equal("not your holding", new AppointGovernorCommand(w.ProtagonistId, "oriel_seat", "corvin_castellan").Refusal(ctx));
    }
}
