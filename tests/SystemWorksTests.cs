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
            for (int i = 0; i < sys.Bodies.Count; i++) if (SystemWorks.Suits(sys, sys.Bodies[i], def) && (def.IsMegastructure ? SystemWorks.Site(w, Cat, sys.Id, i) == null : SystemWorks.FreeSlots(w, Cat, sys, i) > 0) && !SystemWorks.On(w, sys.Id, i).Any(x => x.DefId == def.Id)) return (sys, i);
        }
        return null;
    }

    [Fact]
    public void EverySystem_IsASunAndWhatGoesRoundIt_AndNoTwoAreAlike()
    {
        var (w, _, _, _, _) = Start(); var g = w.Galaxy!;
        foreach (var s in g.Systems.Values)
        {
            Assert.True(s.Furnished); Assert.Single(s.Bodies, b => b.IsStar); Assert.Single(s.Bodies, b => b.IsPrimary); Assert.Single(s.Bodies, b => b.IsSpace);
            Assert.InRange(s.Bodies.Count(b => !b.IsStar && !b.IsSpace), 4, 10);
            Assert.True(s.Bodies.Count(b => b.Orbits < 0 && !b.IsStar && !b.IsSpace) >= 3, $"{s.Id} has too little going round its sun");
            foreach (var (res, rich) in s.Deposits) if (rich > 0) Assert.Contains(s.Bodies, b => b.Deposit == res);
            if (s.Belt > 0) Assert.Contains(s.Bodies, b => b.Class == Body.Belt);
            Assert.InRange(s.Bodies.Count(b => b.Feature.Length > 0), 1, 2);
            foreach (var b in s.Bodies.Where(b => b.Feature.Length > 0)) Assert.Contains(b.Class, Cat.Feature(b.Feature)!.Bodies);
            foreach (var m in s.Bodies.Where(b => b.Orbits >= 0)) { Assert.Equal(Body.Moon, m.Class); Assert.True(s.Bodies[m.Orbits].Orbits < 0 && !s.Bodies[m.Orbits].IsStar && !s.Bodies[m.Orbits].IsSpace); }
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
        var cmd = new BuildWorkCommand(w.ProtagonistId, sys.Id, body, def.Id, Cat, Content.Titles);
        Assert.Null(cmd.Refusal(ctx)); Assert.True(tc.Executor.TryExecute(cmd, ctx));
        Assert.Single(SystemWorks.On(w, sys.Id, body), x => x.DefId == def.Id); Assert.Equal(credits - def.Cost.Credits, house.Treasury.Credits);
        Assert.Equal(before + def.Yield.Materials, eco.Forecast(w, house.Id).Materials);
        Assert.Contains(eco.Compute(w, house.Id).Lines, l => l.Kind == "work" && l.Source.StartsWith(def.Name));
        Assert.Equal("already built here", new BuildWorkCommand(w.ProtagonistId, sys.Id, body, def.Id, Cat, Content.Titles).Refusal(ctx));
        Assert.True(w.HasFlag(SystemWorks.BuiltFlag)); Year(tc); Assert.False(w.HasFlag(SystemWorks.BuiltFlag));
    }

    [Fact]
    public void WhatMayNotBeBuilt_IsRefusedWithAReason()
    {
        var (w, _, house, me, ctx) = Start(); var g = w.Galaxy!;
        var mine = g.System(w.HoldingsOf(house.Id).First(h => h.IsPlanet).SystemId)!; int star = mine.Bodies.FindIndex(b => b.IsStar);
        string? Why(string sys, int body, string work) => new BuildWorkCommand(w.ProtagonistId, sys, body, work, Cat, Content.Titles).Refusal(ctx);
        // another house's system, and a system nobody holds
        var theirs = w.Holdings.Values.First(h => h.IsPlanet && h.OwnerId != house.Id); Assert.Equal("you do not hold this system", Why(theirs.SystemId, 0, "outpost"));
        var empty = g.Systems.Values.First(s => w.PlanetOf(s.Id) == null); Assert.StartsWith("nobody holds this system", Why(empty.Id, 0, "outpost"));
        // the wrong kind of body, a feature it lacks, a deposit it lacks
        Assert.StartsWith("does not suit", Why(mine.Id, star, "outpost"));
        var plain = mine.Bodies.FindIndex(b => b.Class == "gas" && b.Feature != "great_rings"); if (plain >= 0) Assert.StartsWith("needs great rings", Why(mine.Id, plain, "ring_habitat"));
        var noFuel = mine.Bodies.FindIndex(b => b.Class == "gas" && b.Deposit != "helium3"); if (noFuel >= 0) Assert.Contains("deposit", Why(mine.Id, noFuel, "skimmer_fleet"));
        // rank, and the purse
        house.Title = "knight"; Assert.Equal("needs a greater rank", Why(mine.Id, star, "sun_foundry")); house.Title = "duke";
        house.Treasury.Credits = 0; Assert.Contains(" short", Why(mine.Id, star, "sun_foundry")); house.Treasury.Credits = 400;
        Assert.Null(Why(mine.Id, star, "sun_foundry"));
        // a domed colony wants an outpost on the same body first
        int rock = mine.Bodies.FindIndex(b => !b.IsPrimary && Cat.Work("domed_colony")!.Fits(b.Class)); if (rock >= 0) Assert.Contains("Outpost here first", Why(mine.Id, rock, "domed_colony"));
        // a colony is not for a home world, and the home world's own buildings are for nowhere else
        int home = mine.Bodies.FindIndex(b => b.IsPrimary); var colony = Cat.Works.FirstOrDefault(d => d.NotHome && d.Requires.Length == 0 && d.Fits(mine.Bodies[home].Class));
        if (colony != null) Assert.Equal("not for a home world", Why(mine.Id, home, colony.Id));
        int other = mine.Bodies.FindIndex(b => !b.IsPrimary && Cat.Work("forum")!.Fits(b.Class)); if (other >= 0) Assert.Equal("only on the system's home world", Why(mine.Id, other, "forum"));
    }

    [Fact]
    public void ABody_HasItsSlotsForBuildings_AndOneSiteForAGreatWork()
    {
        var (w, tc, house, me, ctx) = Start(); var g = w.Galaxy!;
        var mine = g.System(w.HoldingsOf(house.Id).First(h => h.IsPlanet).SystemId)!; int star = mine.Bodies.FindIndex(b => b.IsStar), space = mine.Bodies.FindIndex(b => b.IsSpace);
        // slots by kind of place, before development and rank add theirs: none on the sun, the catalog's number elsewhere, one on a moon
        house.Title = "knight"; w.Development[mine.Id] = 0;
        Assert.Equal(0, SystemWorks.Slots(w, Cat, mine, star)); Assert.Equal(Cat.BodySlots["space"], SystemWorks.Slots(w, Cat, mine, space));
        for (int i = 0; i < mine.Bodies.Count; i++) { var b = mine.Bodies[i]; if (b.IsStar || b.IsPrimary) continue; Assert.InRange(SystemWorks.Slots(w, Cat, mine, i), 1, 3); if (b.Class == Body.Moon) Assert.Equal(1, SystemWorks.Slots(w, Cat, mine, i)); }
        house.Title = "duke";
        // the sun has no slot but it has a site: a great work is begun there, and a second is refused the site
        Assert.True(tc.Executor.TryExecute(new BuildWorkCommand(w.ProtagonistId, mine.Id, star, "sun_foundry", Cat, Content.Titles), ctx));
        Assert.Empty(SystemWorks.On(w, mine.Id, star)); Assert.Single(SystemWorks.Underway(w, mine.Id, star)); Assert.Equal("sun_foundry", SystemWorks.Site(w, Cat, mine.Id, star));
        Assert.StartsWith("its site is taken", new BuildWorkCommand(w.ProtagonistId, mine.Id, star, "dyson_lattice", Cat, Content.Titles).Refusal(ctx));
        // and with as many rising as its standing allows, the house begins no other great work anywhere
        for (int n = 0; SystemWorks.Raising(w, Cat, house.Id).Count() < SystemWorks.Capacity(w, Cat, house); n++) w.Projects.Add(new Project { Id = "other_" + n, OwnerId = house.Id, Kind = Project.Work, DefId = "sun_foundry", TargetId = "elsewhere#0", Name = "another", TurnsLeft = 5, TurnsTotal = 5 });
        Assert.Contains("at a time", new BuildWorkCommand(w.ProtagonistId, mine.Id, space, "long_cylinder", Cat, Content.Titles).Refusal(ctx)); w.Projects.RemoveAll(p => p.Id.StartsWith("other_"));
        // buildings fill a body's slots and then there is no room; a great work does not take a slot
        int moon = mine.Bodies.FindIndex(b => b.Class == Body.Moon);
        if (moon >= 0)
        {
            Assert.True(tc.Executor.TryExecute(new BuildWorkCommand(w.ProtagonistId, mine.Id, moon, "outpost", Cat, Content.Titles), ctx));
            house.Title = "knight"; Assert.Equal(0, SystemWorks.FreeSlots(w, Cat, mine, moon)); Assert.StartsWith("no free slot here", new BuildWorkCommand(w.ProtagonistId, mine.Id, moon, "strip_mine", Cat, Content.Titles).Refusal(ctx));
            w.Projects.Clear();   // (the great work set aside, to ask of the site alone)
            house.Title = "duke"; Assert.Null(new BuildWorkCommand(w.ProtagonistId, mine.Id, moon, "fortress_moon", Cat, Content.Titles).Refusal(ctx));
        }
        // one to a system: a second fleet anchorage is refused wherever it is put
        w.Projects.Clear(); var anchorage = Cat.Work("fleet_anchorage")!; var first = Site(w, house, anchorage); Assert.NotNull(first);
        Assert.True(tc.Executor.TryExecute(new BuildWorkCommand(w.ProtagonistId, first!.Value.sys.Id, first.Value.body, anchorage.Id, Cat, Content.Titles), ctx));
        for (int i = 0; i < first.Value.sys.Bodies.Count; i++) if (i != first.Value.body && SystemWorks.Suits(first.Value.sys, first.Value.sys.Bodies[i], anchorage)) Assert.Equal("one to a system", new BuildWorkCommand(w.ProtagonistId, first.Value.sys.Id, i, anchorage.Id, Cat, Content.Titles).Refusal(ctx));
    }

    [Fact]
    public void EveryKindOfBody_HasBuildingsOfItsOwn_AndGreatWorksAreAnotherThing()
    {
        // each kind of body has at least three buildings, and at least one that suits no other kind
        foreach (var kind in new[] { "terran", "ocean", "arid", "forge", "frozen", "barren", "gas", "belt", "moon", "comet", "space" })
        {
            var mine = Cat.Works.Where(d => !d.IsMegastructure && d.Bodies.Contains(kind)).ToList();
            Assert.True(mine.Count >= 3, $"{kind} has {mine.Count} buildings");
            if (kind != "comet") Assert.Contains(mine, d => d.Bodies.Count == 1 || (kind is "barren" or "frozen" && d.Bodies.All(b => b is "barren" or "frozen")));
            Assert.Contains(Cat.Works, d => d.IsMegastructure && d.Bodies.Contains(kind));
        }
        Assert.DoesNotContain(Cat.Works, d => !d.IsMegastructure && d.Bodies.Contains("star"));   // nothing ordinary is built on a sun
        // a great work costs far more than any building and takes years; a building takes at most four
        int dearest = Cat.Works.Where(d => !d.IsMegastructure).Max(d => d.Cost.Credits + d.Cost.Materials);
        Assert.All(Cat.Works.Where(d => d.IsMegastructure), d => { Assert.True(d.BuildTurns >= 4, d.Id); Assert.True(d.Cost.Credits + d.Cost.Materials > dearest, d.Id); });
        Assert.All(Cat.Works.Where(d => !d.IsMegastructure), d => Assert.True(d.BuildTurns <= 4 && d.Feature.Length == 0, d.Id));
        Assert.DoesNotContain(Cat.Buildings, b => b.Megastructure);   // none is left among a world's own buildings
        foreach (var f in Cat.Features) Assert.Contains(Cat.Works, d => d.IsMegastructure && d.Feature == f.Id);
    }

    [Fact]
    public void Infrastructure_IsLaidALevelAtATime_AndDevelopsTheSystem()
    {
        var (w, tc, house, me, ctx) = Start(); var seat = w.HoldingsOf(house.Id).First(h => h.IsPlanet); var sys = w.Galaxy!.System(seat.SystemId)!;
        var civil = Cat.Track("civil_works")!; var outworks = Cat.Track("outworks")!; var grid = Cat.Track("defence_grid")!; var eco = new Economy(Cat, Content.Businesses);
        int target = eco.ProsperityTarget(w, seat), credits = house.Treasury.Credits, dev = SystemWorks.Development(w, sys.Id), top = SystemWorks.DevelopmentCeiling(w, Cat, sys.Id);
        // the first level is laid at once, costs the base, and the system is the more developed for it: now, and in how far it can grow
        Assert.True(tc.Executor.TryExecute(new InvestInfrastructureCommand(w.ProtagonistId, sys.Id, civil.Id, Cat), ctx));
        Assert.Equal(1, SystemWorks.Level(w, sys.Id, civil.Id)); Assert.Equal(credits - civil.Cost.Credits, house.Treasury.Credits);
        Assert.Equal(dev + Cat.Rules.DevPerLevel, SystemWorks.Development(w, sys.Id)); Assert.Equal(top + Cat.Rules.DevCeilingPerLevel, SystemWorks.DevelopmentCeiling(w, Cat, sys.Id));
        Assert.Equal(Math.Min(100, target + civil.Prosperity), eco.ProsperityTarget(w, seat));
        // the second costs twice the first; the third takes years, and while it is laid the next is refused
        credits = house.Treasury.Credits; Assert.True(tc.Executor.TryExecute(new InvestInfrastructureCommand(w.ProtagonistId, sys.Id, civil.Id, Cat), ctx)); Assert.Equal(credits - 2 * civil.Cost.Credits, house.Treasury.Credits);
        Assert.True(tc.Executor.TryExecute(new InvestInfrastructureCommand(w.ProtagonistId, sys.Id, civil.Id, Cat), ctx)); Assert.Equal(2, SystemWorks.Level(w, sys.Id, civil.Id));
        Assert.Equal("the next level is being laid", new InvestInfrastructureCommand(w.ProtagonistId, sys.Id, civil.Id, Cat).Refusal(ctx));
        for (int t = 0; t < 4 && SystemWorks.Level(w, sys.Id, civil.Id) < 3; t++) { house.Treasury.Credits = 400; house.Treasury.Materials = 400; Year(tc); }
        Assert.Equal(3, SystemWorks.Level(w, sys.Id, civil.Id));
        // the defence grid defends, and a track that yields is in the ledger
        double def0 = tc.Fleets.Defense(w, sys.Id, house.Id);
        SystemWorks.Lay(w, Cat, sys.Id, outworks); SystemWorks.Lay(w, Cat, sys.Id, grid); SystemWorks.Lay(w, Cat, sys.Id, grid); Assert.Equal(def0 + 2 * grid.Defense, tc.Fleets.Defense(w, sys.Id, house.Id), 3);
        Assert.Contains(eco.Compute(w, house.Id).Lines, l => l.Kind == "work" && l.Source.StartsWith(outworks.Name));
        // it is the holder's to lay, and no further than its last level
        var other = w.Holdings.Values.First(h => h.IsPlanet && h.OwnerId != house.Id); Assert.Equal("you do not hold this system", new InvestInfrastructureCommand(w.ProtagonistId, other.SystemId, civil.Id, Cat).Refusal(ctx));
        for (int i = 0; i < 6; i++) SystemWorks.Lay(w, Cat, sys.Id, grid); Assert.Equal(grid.MaxLevel, SystemWorks.Level(w, sys.Id, grid.Id)); Assert.Equal("it can be carried no further", new InvestInfrastructureCommand(w.ProtagonistId, sys.Id, grid.Id, Cat).Refusal(ctx));
    }

    [Fact]
    public void Slots_ComeFromDevelopment_Rank_AndGroundClearedForCoin()
    {
        // the owner, 2026-11-20: "three things ... determine how many slots you have ... development ... rank ... and if you have more
        // money, you can potentially clear obstacles or just pay to unlock more slots"
        var (w, tc, house, me, ctx) = Start(); var seat = w.HoldingsOf(house.Id).First(h => h.IsPlanet); var sys = w.Galaxy!.System(seat.SystemId)!; var r = Cat.Rules;
        int home = sys.Bodies.FindIndex(b => b.IsPrimary), moon = sys.Bodies.FindIndex(b => !b.IsPrimary && !b.IsStar);
        foreach (var h in w.Holdings.Values.Where(h => h.SystemId == sys.Id && h.OwnerId != house.Id).ToList()) h.OwnerId = house.Id;
        foreach (var p in seat.Provinces.Skip(1)) p.HolderId = ""; w.ProvincesChanged();   // the whole system is the house's, every province of its world with it
        house.Title = "knight"; w.Development[sys.Id] = 0; int baseHome = Cat.Spec(seat.Specialization)!.Slots + r.Undivided, baseMoon = SystemWorks.Slots(w, Cat, sys, moon);
        Assert.Equal(baseHome, SystemWorks.Slots(w, Cat, sys, home)); Assert.Equal(baseHome, SystemWorks.WorldSlots(w, Cat, seat));
        // development is the main one: every level named opens a slot, on the home world sooner and more often than elsewhere
        for (int level = 0; level <= 10; level++)
        {
            w.Development[sys.Id] = level * 10;
            Assert.Equal(Math.Min(r.MaxWorld, baseHome + r.DevelopmentWorld.Count(l => l <= level)), SystemWorks.Slots(w, Cat, sys, home));
            Assert.Equal(Math.Min(r.MaxBody, baseMoon + r.DevelopmentBody.Count(l => l <= level)), SystemWorks.Slots(w, Cat, sys, moon));
        }
        Assert.True(r.DevelopmentWorld.Count >= r.RankWorld.Count && r.DevelopmentWorld.Count >= r.ClearMaxWorld, "development gives the most");
        // rank: the higher the house that holds it, the more slots
        w.Development[sys.Id] = 0; int last = SystemWorks.Slots(w, Cat, sys, home);
        foreach (var title in new[] { "baron", "count", "duke", "archduke", "emperor" }) { house.Title = title; int now = SystemWorks.Slots(w, Cat, sys, home); Assert.True(now >= last, title); last = now; }
        Assert.Equal(baseHome + r.RankWorld.Count, last); Assert.Equal(Math.Min(r.MaxBody, baseMoon + r.RankBody.Count), SystemWorks.Slots(w, Cat, sys, moon));
        // money: ground is cleared for coin, each slot dearer than the last, and only so much of it
        house.Title = "knight"; int credits = house.Treasury.Credits;
        Assert.True(tc.Executor.TryExecute(new ClearSlotCommand(w.ProtagonistId, sys.Id, home, Cat), ctx)); Assert.Equal(credits - r.ClearCost.Credits, house.Treasury.Credits); Assert.Equal(baseHome + 1, SystemWorks.Slots(w, Cat, sys, home));
        credits = house.Treasury.Credits; Assert.True(tc.Executor.TryExecute(new ClearSlotCommand(w.ProtagonistId, sys.Id, home, Cat), ctx)); Assert.Equal(credits - 2 * r.ClearCost.Credits, house.Treasury.Credits);
        for (int i = 2; i < r.ClearMaxWorld; i++) Assert.True(tc.Executor.TryExecute(new ClearSlotCommand(w.ProtagonistId, sys.Id, home, Cat), ctx));
        Assert.Equal("there is no more ground to clear", new ClearSlotCommand(w.ProtagonistId, sys.Id, home, Cat).Refusal(ctx)); Assert.Equal(baseHome + r.ClearMaxWorld, SystemWorks.Slots(w, Cat, sys, home));
        Assert.StartsWith("there is no ground", new ClearSlotCommand(w.ProtagonistId, sys.Id, sys.Bodies.FindIndex(b => b.IsStar), Cat).Refusal(ctx));
        house.Treasury.Credits = 0; Assert.Contains(" short", new ClearSlotCommand(w.ProtagonistId, sys.Id, moon, Cat).Refusal(ctx)); house.Treasury.Credits = 400;
        var theirs = w.Holdings.Values.First(h => h.IsPlanet && h.OwnerId != house.Id); Assert.Equal("you do not hold this system", new ClearSlotCommand(w.ProtagonistId, theirs.SystemId, 1, Cat).Refusal(ctx));
        // everything at once never shows more than the screen has room for
        house.Title = "emperor"; w.Development[sys.Id] = 100; Assert.Equal(r.MaxWorld, SystemWorks.Slots(w, Cat, sys, home)); Assert.InRange(SystemWorks.Slots(w, Cat, sys, moon), 1, r.MaxBody);
        Assert.Equal(SystemWorks.Slots(w, Cat, sys, home), Math.Min(r.MaxWorld, SystemWorks.SlotParts(w, Cat, sys, home).Sum(p => p.slots)));
        // a system another house has a foot in is not all one house's: the home world has a slot the fewer
        house.Title = "knight"; w.Development[sys.Id] = 0; int whole = SystemWorks.Slots(w, Cat, sys, home);
        w.Holdings["foothold"] = new Holding { Id = "foothold", OwnerId = theirs.OwnerId, Name = "A Foothold", Kind = "station", Specialization = theirs.Specialization, SystemId = sys.Id }; w.HoldingsChanged();
        Assert.Equal(whole - r.Undivided, SystemWorks.Slots(w, Cat, sys, home));
    }

    [Fact]
    public void Development_GrowsTowardWhatTheSystemCanBear_AndFallsUnderRevolt()
    {
        var (w, tc, house, _, _) = Start(); var seat = w.HoldingsOf(house.Id).First(h => h.IsPlanet); var sys = w.Galaxy!.System(seat.SystemId)!; var r = Cat.Rules;
        // every held system has a figure from the start, no higher than it can bear; a system nobody holds has none
        foreach (var h in w.Holdings.Values.Where(h => h.IsPlanet && w.Galaxy!.System(h.SystemId) != null && w.PlanetOf(h.SystemId) == h)) Assert.True(SystemWorks.Development(w, h.SystemId) >= 1 && SystemWorks.Development(w, h.SystemId) <= SystemWorks.DevelopmentCeiling(w, Cat, h.SystemId), $"{h.Id} at {h.SystemId}: {SystemWorks.Development(w, h.SystemId)} of {SystemWorks.DevelopmentCeiling(w, Cat, h.SystemId)}, {h.Population} people, held by {h.OwnerId}");
        var empty = w.Galaxy.Systems.Values.First(s => w.PlanetOf(s.Id) == null); Assert.Equal(0, SystemWorks.Development(w, empty.Id)); Assert.Equal(0, SystemWorks.DevelopmentCeiling(w, Cat, empty.Id));
        // it creeps up a year at a time toward the ceiling, and no further
        w.Development[sys.Id] = 5; int top = SystemWorks.DevelopmentCeiling(w, Cat, sys.Id); Assert.True(top >= r.DevCeiling);
        SystemWorks.Tick(w, Cat); Assert.InRange(SystemWorks.Development(w, sys.Id), 6, 7);
        w.Development[sys.Id] = top; SystemWorks.Tick(w, Cat); Assert.True(SystemWorks.Development(w, sys.Id) <= SystemWorks.DevelopmentCeiling(w, Cat, sys.Id));
        // infrastructure and people raise the ceiling
        SystemWorks.Lay(w, Cat, sys.Id, Cat.Track("civil_works")!); Assert.Equal(top + r.DevCeilingPerLevel, SystemWorks.DevelopmentCeiling(w, Cat, sys.Id));
        Assert.Equal(SystemWorks.DevelopmentCeiling(w, Cat, sys.Id), SystemWorks.CeilingParts(w, Cat, sys.Id).Sum(p => p.points));
        // a world in revolt goes backward
        int before = SystemWorks.Development(w, sys.Id); seat.Rebellion = 1; SystemWorks.Tick(w, Cat); Assert.Equal(before + r.DevSiege, SystemWorks.Development(w, sys.Id)); seat.Rebellion = 0;
        // the level is a tenth of it
        w.Development[sys.Id] = 47; Assert.Equal(4, SystemWorks.DevelopmentLevel(w, sys.Id));
    }

    [Fact]
    public void TheHomeWorld_TakesItsOwnBuildings_AndOrdinaryOnes_InTheSameSlots()
    {
        // the owner, 2026-11-20: "the home world of your system should have unique buildings and should also be able to build ordinary
        // buildings ... the whole world where you actually live should have more options of things to do"
        var (w, tc, house, me, ctx) = Start(); var seat = w.HoldingsOf(house.Id).First(h => h.IsPlanet); var sys = w.Galaxy!.System(seat.SystemId)!; int home = sys.Bodies.FindIndex(b => b.IsPrimary); var body = sys.Bodies[home];
        var own = Cat.Works.Where(d => !d.IsMegastructure && d.On == "world").ToList(); Assert.True(own.Count >= 8, $"{own.Count} home-world buildings");
        var ordinary = Cat.Works.Where(d => !d.IsMegastructure && d.On != "world" && SystemWorks.Suits(sys, body, d)).ToList(); Assert.NotEmpty(ordinary);
        Assert.All(own, d => { Assert.True(SystemWorks.Suits(sys, body, d), d.Id); for (int i = 0; i < sys.Bodies.Count; i++) if (i != home) Assert.False(SystemWorks.Suits(sys, sys.Bodies[i], d), d.Id); });
        // more can be built on the home world than on any other place of the system
        int Options(int i) => Cat.Works.Count(d => SystemWorks.Suits(sys, sys.Bodies[i], d)) + (i == home ? Cat.Buildings.Count(b => b.FitsSpecialization(seat.Specialization)) : 0);
        for (int i = 0; i < sys.Bodies.Count; i++) if (i != home) Assert.True(Options(home) > Options(i), sys.Bodies[i].Name);
        // one of each goes up, and both are in the world's slots with the holding's own buildings
        int used = SystemWorks.SlotsUsed(w, Cat, sys.Id, home); Assert.Equal(seat.Buildings.Count, used); w.Development[sys.Id] = 100;
        Assert.True(tc.Executor.TryExecute(new BuildWorkCommand(w.ProtagonistId, sys.Id, home, "forum", Cat, Content.Titles), ctx));
        Assert.True(tc.Executor.TryExecute(new BuildWorkCommand(w.ProtagonistId, sys.Id, home, ordinary[0].Id, Cat, Content.Titles), ctx));
        Assert.Equal(used + 2, SystemWorks.SlotsUsed(w, Cat, sys.Id, home)); Assert.Equal(used + 2, SystemWorks.WorldSlotsUsed(w, Cat, seat));
        Assert.Equal("one to a system", new BuildWorkCommand(w.ProtagonistId, sys.Id, home, "forum", Cat, Content.Titles).Refusal(ctx) is "already being built here" or "already built here" ? "one to a system" : new BuildWorkCommand(w.ProtagonistId, sys.Id, home, "forum", Cat, Content.Titles).Refusal(ctx));
        // when the slots are full neither kind goes up, by either door
        int free = SystemWorks.FreeSlots(w, Cat, sys, home); for (int i = 0; i < free; i++) seat.Buildings.Add("filler_" + i);
        Assert.StartsWith("no free slot", new BuildWorkCommand(w.ProtagonistId, sys.Id, home, "academy", Cat, Content.Titles).Refusal(ctx));
        Assert.StartsWith("no free slot", new BuildCommand(w.ProtagonistId, seat.Id, Cat.Buildings.First(b => b.FitsSpecialization(seat.Specialization) && !seat.Buildings.Contains(b.Id)).Id, Cat, Content.Titles).Refusal(ctx));
        // a great work on the home world has its site, and takes no slot
        w.Projects.RemoveAll(p => SystemWorks.IsGreat(Cat, p)); Assert.Null(new BuildWorkCommand(w.ProtagonistId, sys.Id, home, "great_stair", Cat, Content.Titles).Refusal(ctx));
    }

    [Fact]
    public void ABuilding_IsTakenHigher_AsFarAsDevelopmentAllows()
    {
        var (w, tc, house, me, ctx) = Start(); var r = Cat.Rules; var eco = new Economy(Cat, Content.Businesses);
        var def = Cat.Works.First(d => !d.IsMegastructure && d.BuildTurns <= 1 && d.Deposit.Length == 0 && d.Requires.Length == 0 && !d.Yield.IsEmpty() && Site(w, house, d) != null); var at = Site(w, house, def)!.Value;
        var sys = at.sys; var seat = w.PlanetOf(sys.Id)!;
        Assert.True(tc.Executor.TryExecute(new BuildWorkCommand(w.ProtagonistId, sys.Id, at.body, def.Id, Cat, Content.Titles), ctx)); string target = SystemWorks.Target(sys.Id, at.body);
        Resources Line() => eco.Compute(w, house.Id).Lines.First(l => l.Kind == "work" && l.Source.StartsWith(def.Name)).Delta;
        var one = Line().Clone();
        // development too low: no second level
        w.Development[sys.Id] = (r.UpgradeAt[1] * 10) - 1; Assert.Equal(1, SystemWorks.LevelAllowed(w, Cat, sys.Id)); Assert.StartsWith("needs development", new UpgradeBuildingCommand(w.ProtagonistId, target, def.Id, Cat).Refusal(ctx));
        // high enough: it is begun, paid for, and stands a level higher in a year; it yields and costs twice over
        w.Development[sys.Id] = r.UpgradeAt[1] * 10; int credits = house.Treasury.Credits;
        Assert.True(tc.Executor.TryExecute(new UpgradeBuildingCommand(w.ProtagonistId, target, def.Id, Cat), ctx)); Assert.Equal(credits - def.Cost.Credits, house.Treasury.Credits);
        Assert.Equal("the next level is being built", new UpgradeBuildingCommand(w.ProtagonistId, target, def.Id, Cat).Refusal(ctx));
        Year(tc); Assert.Equal(2, SystemWorks.On(w, sys.Id, at.body).First(x => x.DefId == def.Id).Level);
        foreach (var res in Resource.All) Assert.Equal(one.Get(res) * 2, Line().Get(res));
        // the third wants more development, and costs twice the building; there is no fourth
        w.Development[sys.Id] = r.UpgradeAt[1] * 10; Assert.StartsWith("needs development", new UpgradeBuildingCommand(w.ProtagonistId, target, def.Id, Cat).Refusal(ctx));
        w.Development[sys.Id] = 100; house.Treasury.Credits = 400; house.Treasury.Materials = 400; credits = house.Treasury.Credits;
        Assert.True(tc.Executor.TryExecute(new UpgradeBuildingCommand(w.ProtagonistId, target, def.Id, Cat), ctx)); Assert.Equal(credits - 2 * def.Cost.Credits, house.Treasury.Credits);
        w.Development[sys.Id] = 100; Year(tc); Assert.Equal(3, SystemWorks.On(w, sys.Id, at.body).First(x => x.DefId == def.Id).Level);
        Assert.Equal("it can be taken no higher", new UpgradeBuildingCommand(w.ProtagonistId, target, def.Id, Cat).Refusal(ctx));
        // a world's own building rises the same way, and its line in the ledger with it
        var mine = Cat.Buildings.First(b => seat.Buildings.Contains(b.Id) && !b.Megastructure); w.Development[sys.Id] = 100; house.Treasury.Credits = 400; house.Treasury.Materials = 400; house.Treasury.Exotics = 40;
        Assert.True(tc.Executor.TryExecute(new UpgradeBuildingCommand(w.ProtagonistId, seat.Id, mine.Id, Cat), ctx), new UpgradeBuildingCommand(w.ProtagonistId, seat.Id, mine.Id, Cat).Refusal(ctx));
        for (int t = 0; t < 5 && seat.LevelOf(mine.Id) < 2; t++) { w.Development[sys.Id] = 100; house.Treasury.Credits = 400; house.Treasury.Materials = 400; Year(tc); }
        Assert.Equal(2, seat.LevelOf(mine.Id));
        // a great work has no levels
        w.BodyWorks.Add(new BodyWork { Id = "gw", SystemId = sys.Id, Body = sys.Bodies.FindIndex(b => b.IsStar), DefId = "sun_foundry" });
        Assert.Equal("a great work has no levels", new UpgradeBuildingCommand(w.ProtagonistId, SystemWorks.Target(sys.Id, sys.Bodies.FindIndex(b => b.IsStar)), "sun_foundry", Cat).Refusal(ctx));
    }

    [Fact]
    public void AHouse_RaisesAsManyGreatWorksAtOnce_AsItsStandingAllows()
    {
        // the owner, 2026-11-20: "everyone unless they are much higher up can only do one great work at a time per house. But if you are
        // the leader of a region, you can do two, and if you are higher up like a king or president, you can do three. Finally, an
        // emperor would be able to do five at a time"
        var (w, tc, house, me, ctx) = Start(); var r = Cat.Rules; var seat = w.HoldingsOf(house.Id).First(h => h.IsPlanet); var sys = w.Galaxy!.System(seat.SystemId)!;
        Assert.Equal((1, 2, 3, 5), (r.CapacityHouse, r.CapacityRegionLeader, r.CapacityHeadOfState, r.CapacityEmperor));
        w.Offices.RemoveAll(o => o.HouseId == house.Id); foreach (var p in w.Polities) if (p.HeadHouseId == house.Id) p.HeadHouseId = "";
        bool dominant = PolicySystem.RegionsDominated(w, house.Id).Any();
        house.Title = "baron"; if (!dominant) { Assert.Equal(SystemWorks.StandingHouse, SystemWorks.Standing(w, house)); Assert.Equal(1, SystemWorks.Capacity(w, Cat, house)); }
        house.Title = "duke"; { Assert.Equal(SystemWorks.StandingRegion, SystemWorks.Standing(w, house)); Assert.Equal(2, SystemWorks.Capacity(w, Cat, house)); }
        // a consul is a head of state, whatever the house's title
        house.Title = "baron"; w.Offices.Add(new OfficeHolding { OfficeId = "consul_senior", HouseId = house.Id, CharacterId = w.ProtagonistId }); Assert.Equal(SystemWorks.StandingState, SystemWorks.Standing(w, house)); Assert.Equal(3, SystemWorks.Capacity(w, Cat, house));
        w.Offices.RemoveAll(o => o.HouseId == house.Id); house.Title = "archduke"; Assert.Equal(2, SystemWorks.Capacity(w, Cat, house));   // a great lord under someone is not a head of state
        house.Title = "emperor"; Assert.Equal(SystemWorks.StandingEmperor, SystemWorks.Standing(w, house)); Assert.Equal(5, SystemWorks.Capacity(w, Cat, house));
        // an emperor begins five, each on a site of its own, and is refused the sixth
        house.Treasury.Credits = 5000; house.Treasury.Materials = 5000; house.Treasury.Exotics = 500; house.Treasury.Influence = 500; house.Treasury.Manpower = 500;
        foreach (var h in w.Holdings.Values.Where(h => h.IsPlanet)) h.OwnerId = house.Id; w.HoldingsChanged();
        int begun = 0; string? refusal = null;
        foreach (var def in Cat.Works.Where(d => d.IsMegastructure && d.Limit != WorkDef.PerGalaxy).OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            if (Site(w, house, def) is not { } at) continue; var cmd = new BuildWorkCommand(w.ProtagonistId, at.sys.Id, at.body, def.Id, Cat, Content.Titles);
            if (begun < 5) { if (tc.Executor.TryExecute(cmd, ctx)) begun++; } else { refusal = cmd.Refusal(ctx); break; }
        }
        Assert.Equal(5, begun); Assert.Equal(5, SystemWorks.Raising(w, Cat, house.Id).Count()); Assert.NotNull(refusal); Assert.Contains("an emperor may raise 5 at a time", refusal);
        // a house like any other, with one rising, is refused a second
        w.Projects.RemoveRange(1, w.Projects.Count - 1); house.Title = "baron"; foreach (var h in w.Holdings.Values.Where(h => h.IsPlanet && h.Id != seat.Id && h.SystemId != SystemWorks.Parse(w.Projects[0].TargetId).systemId)) h.OwnerId = "nobody_" + h.Id; w.HoldingsChanged();
        if (SystemWorks.Capacity(w, Cat, house) == 1)
        {
            var next = Cat.Works.Where(d => d.IsMegastructure && d.MinTitleRank <= 2 && d.Feature.Length == 0).Select(d => (d, at: Site(w, house, d))).First(x => x.at != null);
            Assert.Contains("one great work at a time", new BuildWorkCommand(w.ProtagonistId, next.at!.Value.sys.Id, next.at.Value.body, next.d.Id, Cat, Content.Titles).Refusal(ctx));
        }
    }

    [Fact]
    public void AHastenedWork_SaysSo_TheYearTheTreasuryCouldNotStandIt()
    {
        // docs/PLAYTHROUGH.md, Easy, B49: a year the speed could not be paid went by in silence, still labelled "hastened"
        var (w, tc, house, me, ctx) = Start(); var sys = w.Galaxy!.System(w.HoldingsOf(house.Id).First(h => h.IsPlanet).SystemId)!; int star = sys.Bodies.FindIndex(b => b.IsStar); var def = Cat.Work("sun_foundry")!;
        house.Treasury.Credits = 5000; house.Treasury.Materials = 5000; house.Treasury.Exotics = 500;
        Assert.True(tc.Executor.TryExecute(new BuildWorkCommand(w.ProtagonistId, sys.Id, star, def.Id, Cat, Content.Titles), ctx)); var p = w.Projects.Single(x => x.DefId == def.Id);
        Assert.True(tc.Executor.TryExecute(new HastenWorkCommand(w.ProtagonistId, p.Id, true, Cat), ctx));
        var (due, extra) = ProjectSystem.HastenDue(p, Cat.Rules.HastenPct); var each = ProjectSystem.NextInstalment(p);
        house.Treasury.Materials = each.Materials; house.Treasury.Credits = Math.Max(each.Credits, 1) + 2000; house.Treasury.Exotics = 500;   // enough for the year's own share, not for the speed
        Assert.True(due.Materials + extra.Materials > house.Treasury.Materials);
        int left = p.TurnsLeft; w.NewsFrom = w.NewsSeq; ProjectSystem.Tick(w, Cat, Content.Businesses);
        Assert.Equal(left - 1, p.TurnsLeft); Assert.Equal(w.Turn, p.HastenMissedTurn);
        Assert.Contains(w.NewsNow(), n => n.Text.Contains("was not hastened this year") && n.Text.Contains("materials short"));
    }

    [Fact]
    public void AGreatWork_IsHastened_ForACostPaidEveryYearItIsHastened()
    {
        // the owner, 2026-11-20: "you can pay extra to speed up the development. This would be a continuous cost over the development life cycle"
        var (w, tc, house, me, ctx) = Start(); var sys = w.Galaxy!.System(w.HoldingsOf(house.Id).First(h => h.IsPlanet).SystemId)!; int star = sys.Bodies.FindIndex(b => b.IsStar); var def = Cat.Work("sun_foundry")!;
        house.Treasury.Credits = 5000; house.Treasury.Materials = 5000; house.Treasury.Exotics = 500;
        Assert.True(tc.Executor.TryExecute(new BuildWorkCommand(w.ProtagonistId, sys.Id, star, def.Id, Cat, Content.Titles), ctx)); var p = w.Projects.Single(x => x.DefId == def.Id); Assert.True(p.Great);
        // it is not bought outright
        Assert.StartsWith("a great work is not bought outright", new RushProjectCommand(w.ProtagonistId, p.Id).Refusal(ctx));
        // hastened, a year does two years' work and pays both instalments and half as much again
        Assert.True(tc.Executor.TryExecute(new HastenWorkCommand(w.ProtagonistId, p.Id, true, Cat), ctx)); Assert.True(p.Hastened); Assert.Equal("it is being hastened", new HastenWorkCommand(w.ProtagonistId, p.Id, true, Cat).Refusal(ctx));
        var each = ProjectSystem.NextInstalment(p); var (due, extra) = ProjectSystem.HastenDue(p, Cat.Rules.HastenPct);
        Assert.Equal(each.Credits * 2, due.Credits); Assert.Equal((due.Credits * Cat.Rules.HastenPct + 99) / 100, extra.Credits); Assert.True(extra.Credits > 0);
        int left = p.TurnsLeft, paid = p.Paid.Credits, purse = house.Treasury.Credits, mats = house.Treasury.Materials;
        ProjectSystem.Tick(w, Cat, Content.Businesses);
        Assert.Equal(left - 2, p.TurnsLeft); Assert.Equal(paid + due.Credits, p.Paid.Credits); Assert.Equal(purse - due.Credits - extra.Credits, house.Treasury.Credits); Assert.Equal(mats - due.Materials - extra.Materials, house.Treasury.Materials);
        // a year the purse cannot stand it, it goes at its own pace: one year, one instalment, no surcharge
        left = p.TurnsLeft; house.Treasury.Credits = each.Credits; ProjectSystem.Tick(w, Cat, Content.Businesses); Assert.Equal(left - 1, p.TurnsLeft); Assert.Equal(0, house.Treasury.Credits); Assert.False(p.Stalled);
        // hastened all the way it stands in half the years, and the whole price was paid, and more
        house.Treasury.Credits = 5000; int years = 2; while (w.Projects.Contains(p) && years < 40) { ProjectSystem.Tick(w, Cat, Content.Businesses); years++; }
        Assert.Contains(SystemWorks.On(w, sys.Id, star), x => x.DefId == def.Id); Assert.True(years <= def.BuildTurns / 2 + 2, $"{years} years for a work of {def.BuildTurns}");
        Assert.True(p.Paid.Credits >= def.Cost.Credits, $"{p.Paid.Credits} of {def.Cost.Credits}");
        // an ordinary building is not hastened
        var plain = Cat.Works.First(d => !d.IsMegastructure && d.BuildTurns >= 2 && d.Requires.Length == 0 && d.Deposit.Length == 0 && Site(w, house, d) != null); var at = Site(w, house, plain)!.Value; w.Development[at.sys.Id] = 100;
        Assert.True(tc.Executor.TryExecute(new BuildWorkCommand(w.ProtagonistId, at.sys.Id, at.body, plain.Id, Cat, Content.Titles), ctx)); var q = w.Projects.Single(x => x.DefId == plain.Id);
        Assert.Equal("only a great work is hastened", new HastenWorkCommand(w.ProtagonistId, q.Id, true, Cat).Refusal(ctx)); Assert.False(q.Great);
    }

    [Fact]
    public void EverythingThatCanBeBuilt_HasItsPicture()
    {
        // tools/art/works.py paints one for every building, great work, track of infrastructure and building of a system's own world
        string root = Path.GetDirectoryName(ContentLocator.FindContentDir().TrimEnd(Path.DirectorySeparatorChar))!, dir = Path.Combine(root, "src", "Web", "wwwroot", "img", "works");
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "manifest.json")));
        var painted = doc.RootElement.GetProperty("works").EnumerateArray().Select(x => x.GetString() ?? "").ToHashSet();
        var wanted = Cat.Works.Select(d => d.Id).Concat(Cat.Infrastructure.Select(t => t.Id)).Concat(Cat.Buildings.Select(b => b.Id)).ToList();
        Assert.All(wanted, id => { Assert.True(painted.Contains(id), $"{id} has no picture: add a line to tools/art/works.py, paint and pack"); Assert.True(File.Exists(Path.Combine(dir, id + ".webp")), $"{id}.webp is missing"); });
        Assert.All(painted, id => Assert.Contains(id, wanted));   // and nothing is painted that is no longer in the game
    }

    [Fact]
    public void AGreatWorkLeftInAWorldsSlot_IsMovedToItsSite()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 7);
        var seat = w.HoldingsOf(w.Protagonist!.HouseId).First(h => h.IsPlanet); seat.Buildings.Add("orbital_ring"); seat.Buildings.Add("dyson_lattice"); seat.Buildings.Add("gate_anchor");
        var tc = new TurnController(w, Content); var sys = w.Galaxy!.System(seat.SystemId)!;
        Assert.DoesNotContain(seat.Buildings, b => b is "orbital_ring" or "dyson_lattice" or "gate_anchor");
        Assert.Contains(w.BodyWorks, x => x.DefId == "orbital_ring" && sys.Bodies[x.Body].IsPrimary); Assert.Contains(w.BodyWorks, x => x.DefId == "dyson_lattice" && sys.Bodies[x.Body].IsStar);
        Assert.Contains(w.BodyWorks, x => x.DefId == "gate_anchor" && sys.Bodies[x.Body].IsSpace);
    }

    [Fact]
    public void AGreatWork_TakesItsYears_AndGivesItsNameOnce()
    {
        var (w, tc, house, me, ctx) = Start(); var def = Cat.Work("sun_foundry")!; var site = Site(w, house, def)!.Value;
        int glory = house.Glory, credits = house.Treasury.Credits;
        Assert.True(tc.Executor.TryExecute(new BuildWorkCommand(w.ProtagonistId, site.sys.Id, site.body, def.Id, Cat, Content.Titles), ctx));
        Assert.True(house.Treasury.Credits > credits - def.Cost.Credits, "only the first instalment is paid at the start");
        for (int t = 0; t < def.BuildTurns + 1 && !SystemWorks.On(w, site.sys.Id, site.body).Any(); t++) { house.Treasury.Credits = 400; house.Treasury.Materials = 400; glory = house.Glory; Year(tc); }
        Assert.Single(SystemWorks.On(w, site.sys.Id, site.body)); Assert.InRange(house.Glory, glory + def.Glory - 1, glory + def.Glory);   // (glory fades a point in some years)
        glory = house.Glory - def.Glory;
        Assert.Contains(w.History, hst => hst.Text.Contains(def.Name) && hst.Text.Contains("stands"));
        Assert.Equal(1, w.Counter(SystemWorks.WondersCounter));
        Year(tc); Assert.True(house.Glory <= glory + def.Glory, "given once");
    }

    [Fact]
    public void Works_DefendTheSystem_SupplyTheFleet_AndMakeRoomForPeople()
    {
        var (w, tc, house, me, ctx) = Start(); var seat = w.HoldingsOf(house.Id).First(h => h.IsPlanet); var sys = w.Galaxy!.System(seat.SystemId)!;
        int siege = tc.Fleets.SiegeRequired(w, seat); double def0 = tc.Fleets.Defense(w, sys.Id, house.Id);
        // put works straight on the bodies, as if finished: the rules about where are tested above
        void Put(string id) { int i = sys.Bodies.FindIndex(b => SystemWorks.Suits(sys, b, Cat.Work(id)!) || (Cat.Work(id)!.Deposit.Length > 0 && Cat.Work(id)!.Fits(b.Class))); if (i < 0) i = sys.Bodies.FindIndex(b => b.IsSpace); SystemWorks.Raise(w, house, sys.Id, i, Cat.Work(id)!); }
        Put("orbital_batteries"); Put("fortress_moon"); double added = Cat.Work("orbital_batteries")!.Defense + Cat.Work("fortress_moon")!.Defense;
        Assert.Equal(def0 + added, tc.Fleets.Defense(w, sys.Id, house.Id), 3); Assert.Equal(siege + (int)Math.Ceiling(2 * added), tc.Fleets.SiegeRequired(w, seat));
        Assert.Equal(0, tc.Fleets.Defense(w, sys.Id, "some_other_house"));
        Assert.False(SystemWorks.Depot(w, Cat, sys.Id)); Put("fleet_anchorage"); Assert.True(SystemWorks.Depot(w, Cat, sys.Id));
        var eco = new Economy(Cat, Content.Businesses); int target = eco.ProsperityTarget(w, seat);
        Put("waystation"); Assert.Equal(Math.Min(100, target + Cat.Work("waystation")!.Prosperity), eco.ProsperityTarget(w, seat));
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
        int rock = sys.Bodies.FindIndex(b => SystemWorks.Suits(sys, b, Cat.Work("strip_mine")!)); if (rock < 0) rock = sys.Bodies.FindIndex(b => b.IsSpace);
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
        Assert.StartsWith("too far", new FoundColonyCommand(w.ProtagonistId, empty.Id, Cat).Refusal(ctx));
        // a fleet of the house standing in it brings it within reach
        var fleet = w.Fleets.Values.First(f => f.OwnerId == house.Id); fleet.SystemId = empty.Id; fleet.DestinationId = "";
        var cmd = new FoundColonyCommand(w.ProtagonistId, empty.Id, Cat); Assert.Null(cmd.Refusal(ctx)); Assert.True(tc.Executor.TryExecute(cmd, ctx));
        Assert.Equal("your colony is on its way", new FoundColonyCommand(w.ProtagonistId, empty.Id, Cat).Refusal(ctx));
        Assert.Null(w.PlanetOf(empty.Id));
        for (int t = 0; t < Cat.Colony.BuildTurns + 1 && w.PlanetOf(empty.Id) == null; t++) { house.Treasury.Credits = 400; house.Treasury.Materials = 400; house.Treasury.Manpower = 100; Year(tc); }
        var colony = w.PlanetOf(empty.Id); Assert.NotNull(colony); Assert.Equal(house.Id, colony!.OwnerId); Assert.True(colony.Population > 0); Assert.Equal(1, w.Counter(SystemWorks.ColoniesCounter));
        Assert.Equal("it is held already", new FoundColonyCommand(w.ProtagonistId, empty.Id, Cat).Refusal(ctx));
        // and now its bodies can be built on
        int rock = empty.Bodies.FindIndex(b => SystemWorks.Suits(empty, b, Cat.Work("outpost")!)); if (rock >= 0) Assert.Null(new BuildWorkCommand(w.ProtagonistId, empty.Id, rock, "outpost", Cat, Content.Titles).Refusal(ctx));
        // a nation's space is not settled
        var beyond = g.Systems.Values.First(s => g.RingOf(s.Id) == Ring.Beyond); var held = w.PlanetOf(beyond.Id); if (held != null) { w.Holdings.Remove(held.Id); w.HoldingsChanged(); }
        Assert.StartsWith("it lies in a nation's space", new FoundColonyCommand(w.ProtagonistId, beyond.Id, Cat).Refusal(ctx));
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
        Assert.All(w.Galaxy!.Systems.Values, s => { for (int i = 0; i < s.Bodies.Count; i++) Assert.InRange(SystemWorks.Slots(w, Cat, s, i), 0, s.Bodies[i].IsPrimary ? Cat.Rules.MaxWorld : Cat.Rules.MaxBody); });
        Assert.True(w.Holdings.Values.Where(h => h.IsPlanet).All(h => w.Development.ContainsKey(h.SystemId)), "every held system has its development");
        Assert.True(w.Counter("buildings_upgraded") >= 3, $"{w.Counter("buildings_upgraded")} buildings taken a level higher in forty years");
        Assert.True(w.Counter("infrastructure_laid") >= 5, $"{w.Counter("infrastructure_laid")} levels of infrastructure laid in forty years");
        Assert.True(w.Counter(SystemWorks.ColoniesCounter) >= 1, "nobody settled an empty system in forty years");
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        Assert.Equal(w.BodyWorks.Count, back.BodyWorks.Count); Assert.Equal(w.Galaxy.Systems.Values.Sum(s => s.Bodies.Count), back.Galaxy!.Systems.Values.Sum(s => s.Bodies.Count));
        Assert.Equal(w.Galaxy.Systems.Values.Sum(s => s.Bodies.Count(b => b.Feature.Length > 0)), back.Galaxy.Systems.Values.Sum(s => s.Bodies.Count(b => b.Feature.Length > 0)));
    }
}
