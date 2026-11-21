using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;
using Xunit.Abstractions;

namespace Praetoria.Tests;

/// <summary>The cities of a world (docs/CITIES.md; the owner, 2026-11-21): every province has a chief city with a name and a kind,
/// the seat's is the world's capital, there are four great cities in the Reach, and a city's kind does something the rules apply.</summary>
public class CityTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static HoldingCatalog Cat => Content.Holdings;
    private readonly ITestOutputHelper _out; public CityTests(ITestOutputHelper o) => _out = o;
    private static (World w, TurnController tc, House house, Character me, CommandContext ctx) Start(string scenario = "the_old_lion", int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content); var me = w.Protagonist!;
        return (w, tc, w.House(me.HouseId)!, me, new CommandContext(w, TestContent.Rng()));
    }

    [Fact]
    public void EveryWorldHasItsCities_TheSeatsIsItsCapital_AndNoTwoShareAName()
    {
        var (w, tc, house, me, ctx) = Start(); tc.BeginTurn();
        var worlds = w.Holdings.Values.Where(h => h.IsPlanet && h.Provinces.Count > 0).ToList(); Assert.True(worlds.Count > 20);
        var names = new HashSet<string>(); var kindsSeen = new HashSet<string>();
        foreach (var h in worlds)
        {
            Assert.All(h.Provinces, p => { Assert.False(string.IsNullOrWhiteSpace(p.City), h.Name); Assert.NotNull(Cat.Cities.Kind(p.CityKind)); });
            var seat = Cat.Cities.Kind(h.Provinces[0].CityKind)!; Assert.True(seat.Id == CityRules.Capital || seat.Id == "reach_capital", h.Name + ": " + seat.Id);
            // the others are not capitals, and a world does not have the same kind of city twice
            var others = h.Provinces.Skip(1).Select(p => p.CityKind).ToList(); Assert.DoesNotContain(CityRules.Capital, others); Assert.Equal(others.Count, others.Distinct().Count());
            foreach (var p in h.Provinces) { Assert.True(names.Add(p.City), "two cities called " + p.City); kindsSeen.Add(p.CityKind); }
        }
        // the Reach is not all one thing: many kinds of city are met with on one map
        Assert.True(kindsSeen.Count >= 9, string.Join(",", kindsSeen));
        foreach (var h in w.HoldingsOf(house.Id).Where(h => h.IsPlanet)) _out.WriteLine($"{h.Name}: " + string.Join(" | ", h.Provinces.Select((p, i) => $"{p.City} ({Cat.Cities.Kind(p.CityKind)!.Name}, {Census.Say(CitySystem.Citizens(w, Cat, h, i))})")) + $" and {CitySystem.Lesser(w, Cat, h)} lesser");
        // laid out once: another year, and a game opened again, change nothing
        var before = worlds.SelectMany(h => h.Provinces.Select(p => p.City + ":" + p.CityKind)).ToList(); tc.EndTurn(); tc.BeginTurn();
        Assert.Equal(before, worlds.SelectMany(h => h.Provinces.Select(p => p.City + ":" + p.CityKind)).ToList());
        var again = Start().w; new TurnController(again, Content).BeginTurn();
        Assert.Equal(before, again.Holdings.Values.Where(h => h.IsPlanet && h.Provinces.Count > 0).SelectMany(h => h.Provinces.Select(p => p.City + ":" + p.CityKind)).ToList());
    }

    [Fact]
    public void TheReachHasItsFourGreatCities_OneEach_WhereTheyBelong()
    {
        var (w, tc, _, _, _) = Start(); tc.BeginTurn();
        var great = CitySystem.Great(w, Cat).ToList(); Assert.Equal(4, great.Count); Assert.Equal(4, great.Select(g => g.world.Id).Distinct().Count());
        foreach (var k in Cat.Cities.Kinds.Where(k => k.Great)) Assert.Equal(1, w.Holdings.Values.Sum(h => h.Provinces.Count(p => p.CityKind == k.Id)));
        var capital = great.First(g => g.kind.Place == "capital"); Assert.Equal(w.Galaxy!.Capital, capital.world.SystemId); Assert.Equal(0, capital.index);
        var academy = great.First(g => g.kind.Place == "academy"); Assert.Contains(w.Galaxy.System(academy.world.SystemId)!.PlanetClass, new[] { "terran", "ocean" });
        foreach (var (kind, world, index) in great) _out.WriteLine($"{kind.Name}: {world.Provinces[index].City} on {world.Name} ({Census.Say(CitySystem.Citizens(w, Cat, world, index))}), held by {w.House(ProvinceSystem.HolderOf(world, index))?.Name}");
        // a great city holds more of its world than an ordinary one, and no city more than its province
        foreach (var h in w.Holdings.Values.Where(h => h.IsPlanet && h.Provinces.Count > 0))
            for (int i = 0; i < h.Provinces.Count; i++) { long c = CitySystem.Citizens(w, Cat, h, i); Assert.True(c >= 0 && c <= Census.CitizensOfProvince(w, h, i), h.Name); }
        Assert.True(CitySystem.Citizens(w, Cat, capital.world, 0) >= 300_000_000, Census.Say(CitySystem.Citizens(w, Cat, capital.world, 0)));
    }

    [Fact]
    public void ACitysKind_IsInTheRules_NotOnlyOnTheScreen()
    {
        var (w, tc, house, me, ctx) = Start(); tc.BeginTurn(); var eco = tc.Economy!;
        var home = w.HoldingsOf(house.Id).First(h => h.IsPlanet && h.Provinces.Count > 1); var p = home.Provinces[1];
        // what a city gives a year is in its province's yield
        p.CityKind = "port"; var port = Cat.Cities.Kind("port")!; int with = ProvinceSystem.Yield(w, Cat, home, p).Credits;
        p.CityKind = "shrine"; Assert.Equal(with - port.Yield.Credits, ProvinceSystem.Yield(w, Cat, home, p).Credits);
        // a holy city quiets the world and a company town troubles it, by name, in the list the year's unrest is reckoned from
        var shrine = Cat.Cities.Kind("shrine")!; Assert.Contains(eco.UnrestWhy(w, home), c => c.what.StartsWith(p.City) && c.by == shrine.Unrest);
        p.CityKind = "mining"; Assert.Contains(eco.UnrestWhy(w, home), c => c.what.StartsWith(p.City) && c.by == Cat.Cities.Kind("mining")!.Unrest);
        // a resort lifts where the world's prosperity heads
        int target = eco.ProsperityTarget(w, home); p.CityKind = "resort"; Assert.Contains(eco.ProsperityWhy(w, home), c => c.what.StartsWith(p.City) && c.by == Cat.Cities.Kind("resort")!.Prosperity);
        Assert.True(eco.ProsperityTarget(w, home) >= Math.Min(100, target));
        // a garrison city adds to the world's defence and to what its holder can keep under arms
        p.CityKind = "shrine"; double def = ProvinceSystem.Defense(Cat, home); int cap = Economy.MusterCap(w, house.Id, Cat);
        p.CityKind = "garrison"; var garrison = Cat.Cities.Kind("garrison")!; Assert.Equal(def + garrison.Defense, ProvinceSystem.Defense(Cat, home), 6); Assert.Equal(cap + garrison.Muster, Economy.MusterCap(w, house.Id, Cat));
        Assert.False(string.IsNullOrWhiteSpace(CitySystem.Says(garrison)));
    }

    [Fact]
    public void ACitysBuilding_GoesUpInThatKindOfCity_AndNowhereElse()
    {
        var (w, tc, house, me, ctx) = Start(); tc.BeginTurn();
        var home = w.HoldingsOf(house.Id).First(h => h.IsPlanet && h.Provinces.Count > 1); var sys = w.Galaxy!.System(home.SystemId)!; var p = home.Provinces[1];
        // every kind but the plain capital has its own building, and the building is in the game
        foreach (var k in Cat.Cities.Kinds.Where(k => k.Id != CityRules.Capital)) { Assert.NotEqual("", k.Building); var b = Cat.Work(k.Building); Assert.NotNull(b); Assert.Equal(k.Id, b!.City); }
        var halls = Cat.Work("guild_halls")!; p.CityKind = "craftsmen"; Assert.True(ProvinceSystem.Suits(Cat, sys, p, halls));
        p.CityKind = "resort"; Assert.False(ProvinceSystem.Suits(Cat, sys, p, halls)); Assert.True(ProvinceSystem.Suits(Cat, sys, p, Cat.Work("pleasure_gardens")!));
        // none of them is for a body's slots; the Senate's precinct stands on the home world of the capital of the Reach alone
        var primary = sys.Bodies.First(b => b.IsPrimary); Assert.False(SystemWorks.Suits(sys, primary, halls, home)); Assert.False(SystemWorks.Suits(sys, primary, halls));
        var precinct = Cat.Work("senate_precinct")!; Assert.False(SystemWorks.Suits(sys, primary, precinct, home));
        var capital = w.Holdings.Values.First(h => h.IsPlanet && h.SystemId == w.Galaxy.Capital); var csys = w.Galaxy.System(capital.SystemId)!;
        Assert.True(SystemWorks.Suits(csys, csys.Bodies.First(b => b.IsPrimary), precinct, capital));
    }

    [Fact]
    public void AGameFromBeforeThereWereCities_HasThemLaidOut_WhenItIsOpened()
    {
        var (w, tc, _, _, _) = Start(); tc.BeginTurn();
        foreach (var h in w.Holdings.Values) foreach (var p in h.Provinces) { p.City = ""; p.CityKind = ""; }
        foreach (var k in Cat.Cities.Kinds) w.WorldCounters.Remove(CitySystem.GreatKey(k.Id));
        tc.EndTurn(); tc.BeginTurn();
        Assert.All(w.Holdings.Values.Where(h => h.IsPlanet).SelectMany(h => h.Provinces), p => Assert.NotEqual("", p.City));
        Assert.Equal(4, CitySystem.Great(w, Cat).Count());
    }
}
