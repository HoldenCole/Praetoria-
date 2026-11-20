using Praetoria.Core.Data;
using Praetoria.Core.Galaxy;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The size of the map (docs/GALAXY_SIZE.md; the owner, 2026-11-20: "a scaler for how big the galaxy is ... small ... medium
/// large and extra large ... the empire that you are a part of, and the other empires would be bigger as well"). The written map is
/// the small one; a larger one is grown from it by the game's seed.</summary>
public class GalaxySizeTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static World Start(string size, int seed = 7, string scenario = "the_old_lion")
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        GalaxyGrowth.Grow(w, Content, size); return w;
    }
    private static string Shape(World w) => string.Join("|", w.Galaxy!.Systems.Values.OrderBy(s => s.Id, StringComparer.Ordinal).Select(s => $"{s.Id}:{s.X}:{s.Y}:{s.PlanetClass}"))
        + "#" + string.Join("|", w.Galaxy.Lanes.Select(l => l.A + "-" + l.B)) + "#" + string.Join("|", w.Holdings.Values.OrderBy(h => h.Id, StringComparer.Ordinal).Select(h => h.Id + ":" + h.OwnerId));

    [Fact]
    public void Small_IsTheMapAsWritten()
    {
        var plain = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 7);
        var small = Start(GrowthCatalog.Small);
        Assert.Equal(Shape(plain), Shape(small)); Assert.Equal("small", small.GalaxySize); Assert.Equal(100, small.GalaxyScale);
        Assert.DoesNotContain(small.Galaxy!.Systems.Values, s => s.Grown);
        Assert.Equal("small", Start("no such size").GalaxySize);
        Assert.Empty(Content.Growth.Problems()); Assert.True(Content.Growth.Names.Count >= 120 && Content.Growth.Surnames.Count >= 60);
    }

    [Theory]
    [InlineData(GrowthCatalog.Medium)]
    [InlineData(GrowthCatalog.Large)]
    [InlineData(GrowthCatalog.ExtraLarge)]
    public void ALargerMap_HasItsShareOfSystems_AndIsOneSoundWeb(string size)
    {
        var small = Start(GrowthCatalog.Small); var w = Start(size); var g = w.Galaxy!; int pct = Content.Growth.PercentOf(size);
        int want = small.Galaxy!.Systems.Count * pct / 100;
        Assert.InRange(g.Systems.Count, want * 92 / 100, want * 106 / 100);   // a crowded region may fall a system short, and each region rounds its own share
        Assert.Empty(g.Problems());
        Assert.All(g.Systems.Keys, id => Assert.True(g.Hops(g.Capital, id) >= 0, $"{id} cannot be reached from the capital"));
        Assert.Equal(g.Systems.Count, g.Systems.Values.Select(s => s.Name).Distinct().Count());
        // every region grew, and every written system is still there
        foreach (var r in g.Regions) Assert.True(g.SystemsIn(r.Id).Count() > small.Galaxy.SystemsIn(r.Id).Count(), $"{r.Id} did not grow");
        Assert.All(small.Galaxy.Systems.Keys, id => Assert.True(g.Systems.ContainsKey(id)));
        // a nation's space is joined to the Reach only by the lanes that were written: growth opens no new door
        bool Beyond(string sys) => g.RingOf(sys) == Ring.Beyond;
        var doors = g.Lanes.Where(l => Beyond(l.A) != Beyond(l.B) || (Beyond(l.A) && g.System(l.A)!.RegionId != g.System(l.B)!.RegionId)).Select(l => l.A + "-" + l.B).ToList();
        var written = small.Galaxy.Lanes.Select(l => l.A + "-" + l.B).ToHashSet();
        Assert.All(doors, d => Assert.Contains(d, written));
        // systems keep their distance: none sits on top of another
        var all = g.Systems.Values.ToList();
        double nearest = all.Min(a => all.Where(b => b != a).Min(b => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y))));
        Assert.True(nearest > 60, $"two systems are {nearest:F0} apart");
    }

    [Fact]
    public void TheSameSeed_GrowsTheSameMap_AndAnotherSeedAnother()
    {
        Assert.Equal(Shape(Start(GrowthCatalog.Large, 11)), Shape(Start(GrowthCatalog.Large, 11)));
        Assert.NotEqual(Shape(Start(GrowthCatalog.Large, 11)), Shape(Start(GrowthCatalog.Large, 12)));
    }

    [Fact]
    public void NewWorldsOfTheReach_GoToHousesOldAndNew_AndSomeToNobody()
    {
        var small = Start(GrowthCatalog.Small); var w = Start(GrowthCatalog.ExtraLarge); var g = w.Galaxy!;
        var grown = g.Systems.Values.Where(s => s.Grown && g.RingOf(s.Id) != Ring.Beyond).ToList();
        var held = grown.Where(s => w.PlanetHolder(s.Id) != null).ToList();
        Assert.True(held.Count > grown.Count / 2, "most new systems are held"); Assert.True(held.Count < grown.Count, "some are left to nobody");
        // new houses were raised, each with a living head of age who governs its world; and old houses gained worlds
        var raised = w.Houses.Values.Where(h => !small.Houses.ContainsKey(h.Id)).ToList(); Assert.True(raised.Count >= 10, $"{raised.Count} houses raised");
        foreach (var h in raised)
        {
            var head = Economy.HeadOf(w, h); Assert.NotNull(head); Assert.True(head!.Age >= DynastySystem.AgeOfMajority);
            Assert.Contains(w.Holdings.Values, x => x.OwnerId == h.Id && x.IsPlanet && x.GovernorId.Length > 0);
            Assert.All(h.Members, m => Assert.Equal(h.Id, w.Char(m)!.HouseId));
        }
        Assert.Equal(w.Houses.Count, w.Houses.Values.Select(h => h.Name).Distinct().Count());
        Assert.Contains(small.Houses.Keys, id => w.HoldingsOf(id).Count(x => x.IsPlanet) > small.HoldingsOf(id).Count(x => x.IsPlanet));
        // no core system is left empty
        Assert.DoesNotContain(grown, s => g.RingOf(s.Id) == Ring.Core && w.PlanetHolder(s.Id) == null);
    }

    [Fact]
    public void TheNations_AreLargerOnALargerMap()
    {
        var small = Start(GrowthCatalog.Small); NationSystem.Seed(small, Content.Nations);
        var big = Start(GrowthCatalog.ExtraLarge); NationSystem.Seed(big, Content.Nations);
        foreach (var d in Content.Nations.Defs)
        {
            var a = small.Nation(d.Id)!; var b = big.Nation(d.Id)!; if (!a.OnMap) continue;
            Assert.True(b.OnMap);
            Assert.True(big.HoldingsOf(b.HouseId).Count() > small.HoldingsOf(a.HouseId).Count(), $"{d.Id} holds no more worlds");
            Assert.True(b.Strength > a.Strength, $"{d.Id} is no stronger");
            Assert.Equal(NationSystem.Worlds(big, d).Count, big.HoldingsOf(b.HouseId).Count());
            Assert.Equal(d.Worlds.Count, NationSystem.Worlds(small, d).Count);
        }
        // every system of a nation's space is the nation's: none is left for a house of the Reach to walk into
        Assert.DoesNotContain(big.Galaxy!.Systems.Values, s => big.Galaxy.RingOf(s.Id) == Ring.Beyond && s.Grown && big.PlanetHolder(s.Id) == null);
    }

    [Theory]
    [InlineData("the_old_lion")]
    [InlineData("the_new_man")]
    [InlineData("the_imperial_court")]
    public void AGameOnTheLargestMap_Plays_AndComesBackFromASave(string scenario)
    {
        var w = Start(GrowthCatalog.ExtraLarge, 5, scenario); var tc = new TurnController(w, Content);
        for (int t = 0; t < 12 && !w.IsDynastyDead; t++) { tc.BeginTurn(); tc.EndTurn(); }
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        Assert.Equal("extra_large", back.GalaxySize); Assert.Equal(w.GalaxyScale, back.GalaxyScale);
        Assert.Equal(w.Galaxy!.Systems.Count, back.Galaxy!.Systems.Count); Assert.Equal(w.Galaxy.Systems.Values.Count(s => s.Grown), back.Galaxy.Systems.Values.Count(s => s.Grown));
        GalaxyGrowth.Grow(back, Content, GrowthCatalog.ExtraLarge);   // a map already grown is not grown again
        Assert.Equal(w.Galaxy.Systems.Count, back.Galaxy.Systems.Count);
    }
}
