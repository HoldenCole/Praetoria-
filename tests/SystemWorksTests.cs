using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Galaxy;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Solar systems (docs/SYSTEMS.md; the owner, 2026-11-20): a system is a sun and everything that goes round it, every system
/// is unlike its neighbours, and its holder may build on every body in it: colonies, mines, bases, megastructures, and the works only
/// one feature allows. A system nobody holds can be settled.</summary>
public class SystemWorksTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static HoldingCatalog Cat => Content.Holdings;
    private static (World w, TurnController tc, House house, Character me, CommandContext ctx) Start(int seed = 7, string scenario = "the_old_lion")
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content); var me = w.Protagonist!; var house = w.House(me.HouseId)!;
        house.Treasury.Credits = 400; house.Treasury.Materials = 400; house.Treasury.Manpower = 100; house.Treasury.Exotics = 40; house.Treasury.Influence = 40; house.Title = "duke";
        return (w, tc, house, me, new CommandContext(w, TestContent.Rng()));
    }
    private static void Year(TurnController tc) { tc.BeginTurn(); tc.EndTurn(); }
    /// <summary>A body of a system the house holds that a work suits, with room on it.</summary>
    private static (StarSystem sys, int body)? Site(World w, House house, WorkDef def)
    {
        foreach (var h in w.HoldingsOf(house.Id).Where(h => h.IsPlanet).OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            var sys = w.Galaxy!.System(h.SystemId)!;
            for (int i = 0; i < sys.Bodies.Count; i++) if (SystemWorks.Suits(sys, sys.Bodies[i], def) && SystemWorks.FreeSlots(w, sys, i) > 0 && !SystemWorks.On(w, sys.Id, i).Any(x => x.DefId == def.Id)) return (sys, i);
        }
        return null;
    }

    [Fact]
    public void EverySystem_IsASunAndWhatGoesRoundIt_AndNoTwoAreAlike()
    {
        var (w, _, _, _, _) = Start(); var g = w.Galaxy!;
        foreach (var s in g.Systems.Values)
        {
            Assert.True(s.Furnished); Assert.Single(s.Bodies, b => b.IsStar); Assert.Single(s.Bodies, b => b.IsPrimary);
            Assert.InRange(s.Bodies.Count(b => !b.IsStar), 4, 10);
            Assert.True(s.Bodies.Count(b => b.Orbits < 0 && !b.IsStar) >= 3, $"{s.Id} has too little going round its sun");
            foreach (var (res, rich) in s.Deposits) if (rich > 0) Assert.Contains(s.Bodies, b => b.Deposit == res);
            if (s.Belt > 0) Assert.Contains(s.Bodies, b => b.Class == Body.Belt);
            Assert.InRange(s.Bodies.Count(b => b.Feature.Length > 0), 1, 2);
            foreach (var b in s.Bodies.Where(b => b.Feature.Length > 0)) Assert.Contains(b.Class, Cat.Feature(b.Feature)!.Bodies);
            foreach (var m in s.Bodies.Where(b => b.Orbits >= 0)) { Assert.Equal(Body.Moon, m.Class); Assert.True(s.Bodies[m.Orbits].Orbits < 0 && !s.Bodies[m.Orbits].IsStar); }
            Assert.Equal(s.Bodies.Count, s.Bodies.Select(b => b.Name).Distinct().Count());
        }
        // unlike each other: many features are in use, and systems differ in what they hold
        Assert.True(g.Systems.Values.SelectMany(s => s.Bodies).Select(b => b.Feature).Where(f => f.Length > 0).Distinct().Count() >= 14);
        Assert.True(g.Systems.Values.Select(s => string.Join(",", s.Bodies.Select(b => b.Class + ":" + b.Feature))).Distinct().Count() >= g.Systems.Count - 2);
        // every feature opens a work of its own, and every work has somewhere it could go
        foreach (var f in Cat.Features) Assert.Contains(Cat.Works, d => d.Feature == f.Id);
        foreach (var d in Cat.Works.Where(d => !d.Chokepoint)) Assert.Contains(g.Systems.Values, s => s.Bodies.Any(b => SystemWorks.Suits(s, b, d)));
    }

    [Fact]
    public void ASystem_IsTheSameInEveryGame_AndTheWrittenBodiesKeepTheirPlaces()
    {
        var a = Start(3).w.Galaxy!; var b = Start(99).w.Galaxy!;
        string Shape(Galaxy g) => string.Join("|", g.Systems.Values.OrderBy(s => s.Id, StringComparer.Ordinal).Select(s => s.Id + ":" + string.Join(",", s.Bodies.Select(x => $"{x.Name}/{x.Class}/{x.Feature}/{x.Deposit}/{x.Orbits}"))));
        Assert.Equal(Shape(a), Shape(b));
        var written = GalaxyLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_reach");
        foreach (var s in written.Systems.Values) for (int i = 0; i < s.Bodies.Count; i++) Assert.Equal(s.Bodies[i].Name, a.System(s.Id)!.Bodies[i].Name);
        int before = a.Systems.Values.Sum(s => s.Bodies.Count); SystemBodies.Furnish(a, Cat); Assert.Equal(before, a.Systems.Values.Sum(s => s.Bodies.Count));   // once only
    }

    [Fact]
    public void TheHolder_BuildsOnABody_AndItPaysIntoTheLedger()
    {
        var (w, tc, house, me, ctx) = Start(); var def = Cat.Work("strip_mine")!; var site = Site(w, house, def); Assert.NotNull(site);
        var (sys, body) = site!.Value; var eco = new Economy(Cat, Content.Businesses);
        int before = eco.Forecast(w, house.Id).Materials, credits = house.Treasury.Credits;
        var cmd = new BuildWorkCommand(me.Id, sys.Id, body, def.Id, Cat, Content.Titles);
        Assert.Null(cmd.Refusal(ctx)); Assert.True(tc.Executor.TryExecute(cmd, ctx));
        Assert.Single(SystemWorks.On(w, sys.Id, body), x => x.DefId == def.Id); Assert.Equal(credits - def.Cost.Credits, house.Treasury.Credits);
        Assert.Equal(before + def.Yield.Materials, eco.Forecast(w, house.Id).Materials);
        Assert.Contains(eco.Compute(w, house.Id).Lines, l => l.Kind == "work" && l.Source.StartsWith(def.Name));
        Assert.Equal("already built here", new BuildWorkCommand(me.Id, sys.Id, body, def.Id, Cat, Content.Titles).Refusal(ctx));
        Assert.True(w.HasFlag(SystemWorks.BuiltFlag)); Year(tc); Assert.False(w.HasFlag(SystemWorks.BuiltFlag));
    }

    [Fact]
    public void WhatMayNotBeBuilt_IsRefusedWithAReason()
    {
        var (w, _, house, me, ctx) = Start(); var g = w.Galaxy!;
        var mine = g.System(w.HoldingsOf(house.Id).First(h => h.IsPlanet).SystemId)!; int star = mine.Bodies.FindIndex(b => b.IsStar);
        string? Why(string sys, int body, string work) => new BuildWorkCommand(me.Id, sys, body, work, Cat, Content.Titles).Refusal(ctx);
        // another house's system, and a system nobody holds
        var theirs = w.Holdings.Values.First(h => h.IsPlanet && h.OwnerId != house.Id); Assert.Equal("you do not hold this system", Why(theirs.SystemId, 0, "outpost"));
        var empty = g.Systems.Values.First(s => w.PlanetOf(s.Id) == null); Assert.StartsWith("nobody holds this system", Why(empty.Id, 0, "outpost"));
        // the wrong kind of body, a feature it lacks, a deposit it lacks
        Assert.StartsWith("does not suit", Why(mine.Id, star, "outpost"));
        var plain = mine.Bodies.FindIndex(b => b.Class == "gas" && b.Feature != "great_rings"); if (plain >= 0) Assert.StartsWith("needs great rings", Why(mine.Id, plain, "ring_habitat"));
        var noFuel = mine.Bodies.FindIndex(b => b.Class == "gas" && b.Deposit != "helium3"); if (noFuel >= 0) Assert.Contains("deposit", Why(mine.Id, noFuel, "skimmer_fleet"));
        // rank, and the purse
        house.Title = "knight"; Assert.Equal("needs a greater rank", Why(mine.Id, star, "sun_foundry")); house.Title = "duke";
        house.Treasury.Credits = 0; Assert.StartsWith("needs ", Why(mine.Id, star, "sun_foundry")); house.Treasury.Credits = 400;
        Assert.Null(Why(mine.Id, star, "sun_foundry"));
        // a domed colony wants an outpost on the same body first
        int rock = mine.Bodies.FindIndex(b => Cat.Work("domed_colony")!.Fits(b.Class)); if (rock >= 0) Assert.Contains("Outpost here first", Why(mine.Id, rock, "domed_colony"));
    }

    [Fact]
    public void ABody_HasOnlySoMuchRoom_AndSomeWorksAreOneToASystem()
    {
        var (w, tc, house, me, ctx) = Start(); var g = w.Galaxy!;
        var mine = g.System(w.HoldingsOf(house.Id).First(h => h.IsPlanet).SystemId)!; int star = mine.Bodies.FindIndex(b => b.IsStar);
        Assert.Equal(1, mine.Bodies[star].Slots);
        Assert.True(tc.Executor.TryExecute(new BuildWorkCommand(me.Id, mine.Id, star, "sun_foundry", Cat, Content.Titles), ctx));   // begun: it takes years
        Assert.Empty(SystemWorks.On(w, mine.Id, star)); Assert.Single(SystemWorks.Underway(w, mine.Id, star)); Assert.Equal(0, SystemWorks.FreeSlots(w, mine, star));
        Assert.Equal("already being built here", new BuildWorkCommand(me.Id, mine.Id, star, "sun_foundry", Cat, Content.Titles).Refusal(ctx));
        // one to a system: a second fleet anchorage is refused wherever it is put
        var anchorage = Cat.Work("fleet_anchorage")!; var first = Site(w, house, anchorage);
        if (first != null)
        {
            Assert.True(tc.Executor.TryExecute(new BuildWorkCommand(me.Id, first.Value.sys.Id, first.Value.body, anchorage.Id, Cat, Content.Titles), ctx));
            for (int i = 0; i < first.Value.sys.Bodies.Count; i++) if (i != first.Value.body && anchorage.Fits(first.Value.sys.Bodies[i].Class)) Assert.Equal("one to a system", new BuildWorkCommand(me.Id, first.Value.sys.Id, i, anchorage.Id, Cat, Content.Titles).Refusal(ctx));
        }
    }

    [Fact]
    public void AGreatWork_TakesItsYears_AndGivesItsNameOnce()
    {
        var (w, tc, house, me, ctx) = Start(); var def = Cat.Work("sun_foundry")!; var site = Site(w, house, def)!.Value;
        int glory = house.Glory, credits = house.Treasury.Credits;
        Assert.True(tc.Executor.TryExecute(new BuildWorkCommand(me.Id, site.sys.Id, site.body, def.Id, Cat, Content.Titles), ctx));
        Assert.True(house.Treasury.Credits > credits - def.Cost.Credits, "only the first instalment is paid at the start");
        for (int t = 0; t < def.BuildTurns + 1 && !SystemWorks.On(w, site.sys.Id, site.body).Any(); t++) { house.Treasury.Credits = 400; house.Treasury.Materials = 400; Year(tc); }
        Assert.Single(SystemWorks.On(w, site.sys.Id, site.body)); Assert.Equal(glory + def.Glory, house.Glory);
        Assert.Contains(w.History, hst => hst.Text.Contains(def.Name) && hst.Text.Contains("stands"));
        Assert.Equal(1, w.Counter(SystemWorks.WondersCounter));
        Year(tc); Assert.Equal(glory + def.Glory, house.Glory);   // once
    }

    [Fact]
    public void Works_DefendTheSystem_SupplyTheFleet_AndMakeRoomForPeople()
    {
        var (w, tc, house, me, ctx) = Start(); var seat = w.HoldingsOf(house.Id).First(h => h.IsPlanet); var sys = w.Galaxy!.System(seat.SystemId)!;
        int siege = tc.Fleets.SiegeRequired(w, seat); double def0 = tc.Fleets.Defense(w, sys.Id, house.Id);
        // put works straight on the bodies, as if finished: the rules about where are tested above
        void Put(string id) { int i = sys.Bodies.FindIndex(b => Cat.Work(id)!.Fits(b.Class)); if (i < 0) i = 0; SystemWorks.Raise(w, house, sys.Id, i, Cat.Work(id)!); }
        Put("orbital_batteries"); Put("fortress_moon");
        Assert.Equal(def0 + 1.5, tc.Fleets.Defense(w, sys.Id, house.Id), 3); Assert.Equal(siege + 3, tc.Fleets.SiegeRequired(w, seat));
        Assert.Equal(0, tc.Fleets.Defense(w, sys.Id, "some_other_house"));
        Assert.False(SystemWorks.Depot(w, Cat, sys.Id)); Put("fleet_anchorage"); Assert.True(SystemWorks.Depot(w, Cat, sys.Id));
        var eco = new Economy(Cat, Content.Businesses); int target = eco.ProsperityTarget(w, seat);
        Put("trade_station"); Assert.Equal(Math.Min(100, target + Cat.Work("trade_station")!.Prosperity), eco.ProsperityTarget(w, seat));
        Put("domed_colony"); Assert.Equal(Cat.Work("domed_colony")!.Population, SystemWorks.Population(w, Cat, sys.Id));
    }

    [Fact]
    public void AWorkOnADeposit_BringsUpAsMuchAsTheDepositIsRich()
    {
        var (w, _, _, _, _) = Start(); var g = w.Galaxy!; var def = Cat.Work("skimmer_fleet")!;
        var rich = g.Systems.Values.Where(s => s.Deposit("helium3") >= 2 && w.PlanetOf(s.Id) != null).OrderBy(s => s.Id, StringComparer.Ordinal).First();
        var holder = w.House(w.PlanetHolder(rich.Id)!)!; int body = rich.Bodies.FindIndex(b => b.Deposit == "helium3"); Assert.True(body >= 0);
        var eco = new Economy(Cat, Content.Businesses); int before = eco.Forecast(w, holder.Id).Helium3;
        SystemWorks.Raise(w, holder, rich.Id, body, def);
        Assert.Equal(before + def.Yield.Helium3 * rich.Deposit("helium3"), eco.Forecast(w, holder.Id).Helium3);
    }

    [Fact]
    public void WhoTakesTheWorld_TakesWhatWasBuiltRoundIt()
    {
        var (w, _, house, _, _) = Start(); var seat = w.HoldingsOf(house.Id).First(h => h.IsPlanet); var sys = w.Galaxy!.System(seat.SystemId)!;
        int rock = sys.Bodies.FindIndex(b => Cat.Work("strip_mine")!.Fits(b.Class)); if (rock < 0) return;
        SystemWorks.Raise(w, house, sys.Id, rock, Cat.Work("strip_mine")!);
        var eco = new Economy(Cat, Content.Businesses); var other = w.Houses.Values.First(h => h.Id != house.Id && w.OfTheReach(h.Id) && w.HoldingsOf(h.Id).Any());
        Assert.Contains(eco.Compute(w, house.Id).Lines, l => l.Kind == "work"); Assert.DoesNotContain(eco.Compute(w, other.Id).Lines, l => l.Kind == "work");
        seat.OwnerId = other.Id;
        Assert.DoesNotContain(eco.Compute(w, house.Id).Lines, l => l.Kind == "work"); Assert.Contains(eco.Compute(w, other.Id).Lines, l => l.Kind == "work");
    }

    [Fact]
    public void AnEmptySystem_BesideYourOwn_CanBeSettled()
    {
        var (w, tc, house, me, ctx) = Start(); var g = w.Galaxy!;
        var empty = g.Systems.Values.Where(s => w.PlanetOf(s.Id) == null && g.RingOf(s.Id) != Ring.Beyond).OrderBy(s => s.Id, StringComparer.Ordinal).First();
        Assert.StartsWith("too far", new FoundColonyCommand(me.Id, empty.Id, Cat).Refusal(ctx));
        // a fleet of the house standing in it brings it within reach
        var fleet = w.Fleets.Values.First(f => f.OwnerId == house.Id); fleet.SystemId = empty.Id; fleet.DestinationId = "";
        var cmd = new FoundColonyCommand(me.Id, empty.Id, Cat); Assert.Null(cmd.Refusal(ctx)); Assert.True(tc.Executor.TryExecute(cmd, ctx));
        Assert.Equal("your colony is on its way", new FoundColonyCommand(me.Id, empty.Id, Cat).Refusal(ctx));
        Assert.Null(w.PlanetOf(empty.Id));
        for (int t = 0; t < Cat.Colony.BuildTurns + 1 && w.PlanetOf(empty.Id) == null; t++) { house.Treasury.Credits = 400; house.Treasury.Materials = 400; house.Treasury.Manpower = 100; Year(tc); }
        var colony = w.PlanetOf(empty.Id); Assert.NotNull(colony); Assert.Equal(house.Id, colony!.OwnerId); Assert.True(colony.Population > 0); Assert.Equal(1, w.Counter(SystemWorks.ColoniesCounter));
        Assert.Equal("it is held already", new FoundColonyCommand(me.Id, empty.Id, Cat).Refusal(ctx));
        // and now its bodies can be built on
        int rock = empty.Bodies.FindIndex(b => Cat.Work("outpost")!.Fits(b.Class)); if (rock >= 0) Assert.Null(new BuildWorkCommand(me.Id, empty.Id, rock, "outpost", Cat, Content.Titles).Refusal(ctx));
        // a nation's space is not settled
        var beyond = g.Systems.Values.First(s => g.RingOf(s.Id) == Ring.Beyond); var held = w.PlanetOf(beyond.Id); if (held != null) { w.Holdings.Remove(held.Id); w.HoldingsChanged(); }
        Assert.Equal("it lies in a nation's space", new FoundColonyCommand(me.Id, beyond.Id, Cat).Refusal(ctx));
    }

    [Fact]
    public void TheOtherHouses_BuildAndSettleToo_AndASaveKeepsIt()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 11);
        GalaxyGrowth.Grow(w, Content, GrowthCatalog.Medium); var tc = new TurnController(w, Content);
        for (int t = 0; t < 40 && !w.IsDynastyDead; t++) Year(tc);
        Assert.True(w.Counter(SystemWorks.BuiltCounter) >= 8, $"{w.Counter(SystemWorks.BuiltCounter)} works built in forty years");
        Assert.True(w.BodyWorks.Select(x => x.DefId).Distinct().Count() >= 4, "of several kinds");
        Assert.All(w.BodyWorks, x => Assert.True(SystemWorks.Suits(w.Galaxy!.System(x.SystemId)!, w.Galaxy.System(x.SystemId)!.Bodies[x.Body], Cat.Work(x.DefId)!)));
        Assert.All(w.Galaxy!.Systems.Values, s => { for (int i = 0; i < s.Bodies.Count; i++) Assert.True(SystemWorks.FreeSlots(w, s, i) >= 0, $"{s.Id} body {i} is over full"); });
        Assert.True(w.Counter(SystemWorks.ColoniesCounter) >= 1, "nobody settled an empty system in forty years");
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        Assert.Equal(w.BodyWorks.Count, back.BodyWorks.Count); Assert.Equal(w.Galaxy.Systems.Values.Sum(s => s.Bodies.Count), back.Galaxy!.Systems.Values.Sum(s => s.Bodies.Count));
        Assert.Equal(w.Galaxy.Systems.Values.Sum(s => s.Bodies.Count(b => b.Feature.Length > 0)), back.Galaxy.Systems.Values.Sum(s => s.Bodies.Count(b => b.Feature.Length > 0)));
    }
}
