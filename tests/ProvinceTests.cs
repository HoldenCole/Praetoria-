using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The provinces of a world (docs/PROVINCES.md; the owner, 2026-11-20): three to a planet, the seat the house's that holds
/// the world and the other two held by different houses, each given over to one thing; held for life, unless the one over the holder
/// asks, and then the holder steps down or fights.</summary>
public class ProvinceTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static HoldingCatalog Cat => Content.Holdings;
    private static (World w, TurnController tc, House house, Character me, CommandContext ctx) Start(int seed = 7, string scenario = "the_old_lion")
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content); var me = w.Protagonist!; var house = w.House(me.HouseId)!;
        house.Treasury.Credits = 400; house.Treasury.Materials = 400; house.Treasury.Influence = 40;
        return (w, tc, house, me, new CommandContext(w, TestContent.Rng()));
    }
    private static void GiveFleet(World w, string houseId, int strength)
    {
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == houseId).ToList()) w.Fleets.Remove(f.Id);
        if (strength > 0) { var seat = w.Holdings.Values.First(h => h.IsPlanet); w.Fleets["test_" + houseId] = new Fleet { Id = "test_" + houseId, OwnerId = houseId, Name = "Test", SystemId = seat.SystemId, Strength = strength }; }
    }

    [Fact]
    public void EveryWorld_HasThreeProvinces_TheSeatItsHolders_AndTheOthersOtherHouses()
    {
        var (w, _, _, _, _) = Start(); var rules = Cat.Provinces; Assert.True(rules.Focuses.Count >= 10);
        var worlds = w.Holdings.Values.Where(h => h.IsPlanet).ToList(); Assert.NotEmpty(worlds);
        foreach (var h in worlds)
        {
            Assert.Equal(3, h.Provinces.Count); Assert.Equal(ProvinceRules.Seat, h.Provinces[0].Focus); Assert.Equal(h.OwnerId, ProvinceSystem.HolderOf(h, 0));
            Assert.All(h.Provinces, p => { Assert.NotNull(rules.Focus(p.Focus)); Assert.False(string.IsNullOrWhiteSpace(p.Name)); Assert.Equal(1, p.Level); });
            Assert.NotEqual(h.Provinces[1].Focus, h.Provinces[2].Focus); Assert.DoesNotContain(h.Provinces.Skip(1), p => p.Focus == ProvinceRules.Seat);
            var sys = w.Galaxy!.System(h.SystemId)!;
            // a focus on a deposit is only where the deposit is, and where a system carries one a province works it
            foreach (var p in h.Provinces) if (rules.Focus(p.Focus)!.Deposit is { Length: > 0 } dep) Assert.True(sys.Deposit(dep) > 0, $"{p.Name} at {sys.Name}");
            if (rules.Focuses.Any(f => f.Deposit.Length > 0 && sys.Deposit(f.Deposit) > 0)) Assert.Contains(h.Provinces, p => rules.Focus(p.Focus)!.Deposit.Length > 0);
            if (World.Outsider(h.OwnerId)) { Assert.False(ProvinceSystem.Divided(h)); continue; }
            // a house of the Reach shares its world with two others, and they are not the same house
            string a = ProvinceSystem.HolderOf(h, 1), b = ProvinceSystem.HolderOf(h, 2);
            Assert.NotEqual(h.OwnerId, a); Assert.NotEqual(h.OwnerId, b); Assert.NotEqual(a, b); Assert.NotNull(w.House(a)); Assert.NotNull(w.House(b)); Assert.False(World.Outsider(a) || World.Outsider(b));
        }
        // the breakup is not the same on every world, and nobody was given everything
        Assert.True(worlds.Select(h => h.Provinces[1].Focus + "+" + h.Provinces[2].Focus).Distinct().Count() >= 6);
        var given = worlds.Where(h => !World.Outsider(h.OwnerId)).SelectMany(h => new[] { ProvinceSystem.HolderOf(h, 1), ProvinceSystem.HolderOf(h, 2) }).GroupBy(x => x).ToDictionary(x => x.Key, x => x.Count());
        Assert.True(given.Values.Max() - given.Values.Min() <= 4, $"from {given.Values.Min()} to {given.Values.Max()} provinces a house");
        // it is the same in every game from the same seed
        var (w2, _, _, _, _) = Start(); foreach (var h in worlds) Assert.Equal(h.Provinces.Select(p => p.Focus + p.HolderId), w2.Holding(h.Id)!.Provinces.Select(p => p.Focus + p.HolderId));
    }

    [Fact]
    public void AProvince_PaysItsHolder_AndIsImprovedAsFarAsDevelopmentAllows()
    {
        var (w, tc, house, me, ctx) = Start(); var eco = new Economy(Cat, Content.Businesses); var rules = Cat.Provinces;
        var seat = w.HoldingsOf(house.Id).First(h => h.IsPlanet); var lines = eco.Compute(w, house.Id).Lines.Where(l => l.Kind == "province").ToList();
        // the seat of the house's own world pays it; so does every province it was given on another's
        Assert.Contains(lines, l => l.Source == seat.Provinces[0].Name); var given = w.ProvincesOf(house.Id); Assert.NotEmpty(given);
        foreach (var (h, i) in given) { Assert.NotEqual(house.Id, h.OwnerId); Assert.Contains(lines, l => l.Source == h.Provinces[i].Name); }
        Assert.Equal(given.Count + w.HoldingsOf(house.Id).Count(h => h.IsPlanet), lines.Count(l => !l.Source.StartsWith("Dues of ")));
        // the province another house holds on the house's world pays that house, not this one
        Assert.DoesNotContain(lines, l => l.Source == seat.Provinces[1].Name); string other = ProvinceSystem.HolderOf(seat, 1);
        Assert.Contains(eco.Compute(w, other).Lines, l => l.Kind == "province" && l.Source == seat.Provinces[1].Name);
        // improved, it pays twice over; development says how far
        var (far, idx) = given[0]; var p = far.Provinces[idx]; var before = lines.First(l => l.Source == p.Name).Delta.Clone();
        w.Development[far.SystemId] = Cat.Rules.UpgradeAt[1] * 10 - 1; Assert.StartsWith("needs development", new ImproveProvinceCommand(me.Id, far.Id, idx, Cat).Refusal(ctx));
        w.Development[far.SystemId] = Cat.Rules.UpgradeAt[1] * 10; int credits = house.Treasury.Credits;
        Assert.True(tc.Executor.TryExecute(new ImproveProvinceCommand(me.Id, far.Id, idx, Cat), ctx)); Assert.Equal(2, p.Level); Assert.Equal(credits - rules.ImproveCost.Credits, house.Treasury.Credits);
        var after = eco.Compute(w, house.Id).Lines.First(l => l.Source == p.Name).Delta; foreach (var r in Resource.All) Assert.Equal(before.Get(r) * 2, after.Get(r));   // (and its tithe is twice what it was)
        Assert.Equal("it is not yours to improve", new ImproveProvinceCommand(me.Id, seat.Id, 1, Cat).Refusal(ctx));
        // a march defends the system it is in
        var march = w.Holdings.Values.FirstOrDefault(h => h.IsPlanet && h.Provinces.Any(x => rules.Focus(x.Focus)!.Defense > 0));
        if (march != null) { double d0 = tc.Fleets.Defense(w, march.SystemId, march.OwnerId); march.Provinces.First(x => rules.Focus(x.Focus)!.Defense > 0).Level = 3; Assert.True(tc.Fleets.Defense(w, march.SystemId, march.OwnerId) > d0); }
        // a world shared with other houses is not all one house's: the slot for an undivided system is not given
        var sys = w.Galaxy!.System(seat.SystemId)!; Assert.True(ProvinceSystem.Divided(seat)); Assert.False(SystemWorks.Undivided(w, sys.Id));
        Assert.DoesNotContain(SystemWorks.SlotParts(w, Cat, sys, sys.Bodies.FindIndex(b => b.IsPrimary)), x => x.what.StartsWith("the whole system"));
    }

    [Fact]
    public void TheHolderOfAWorld_AsksForAProvince_AndTheWeakStepDown_AndTheStrongFight()
    {
        var (w, tc, house, me, ctx) = Start(); var seat = w.HoldingsOf(house.Id).First(h => h.IsPlanet); var sys = w.Galaxy!.System(seat.SystemId)!;
        string weak = ProvinceSystem.HolderOf(seat, 1), strong = ProvinceSystem.HolderOf(seat, 2);
        GiveFleet(w, house.Id, 10); GiveFleet(w, weak, 0); GiveFleet(w, strong, 10);
        // only the holder of the world asks, and it costs influence
        var stranger = w.Holdings.Values.First(h => h.IsPlanet && h.OwnerId != house.Id && !World.Outsider(h.OwnerId));
        Assert.Equal("only the house that holds the world may ask for its provinces", new DemandSeatCommand(me.Id, stranger.Id, 1, house.Id, Cat).Refusal(ctx));
        house.Treasury.Influence = 0; Assert.StartsWith("needs ", new DemandSeatCommand(me.Id, seat.Id, 1, house.Id, Cat).Refusal(ctx)); house.Treasury.Influence = 40;
        // a house with no fleet steps down: the province is the governor's, and with every province his the system is one house's
        Assert.True(tc.Executor.TryExecute(new DemandSeatCommand(me.Id, seat.Id, 1, house.Id, Cat), ctx));
        Assert.Equal(house.Id, ProvinceSystem.HolderOf(seat, 1)); Assert.Equal(40 - Cat.Provinces.DemandInfluence, house.Treasury.Influence); Assert.Equal(1, w.Counter(ProvinceSystem.YieldedCounter));
        Assert.Contains(new Economy(Cat, Content.Businesses).Compute(w, house.Id).Lines, l => l.Kind == "province" && l.Source == seat.Provinces[1].Name);
        Assert.Equal("one such demand a year", new DemandSeatCommand(me.Id, seat.Id, 2, house.Id, Cat).Refusal(ctx));
        // a house that is the governor's match will not: it was theirs for life, and it is war
        w.WorldCounters.Remove("seat_asked:" + house.Id); Assert.False(w.AtWar(house.Id, strong));
        Assert.True(tc.Executor.TryExecute(new DemandSeatCommand(me.Id, seat.Id, 2, house.Id, Cat), ctx));
        Assert.Equal(strong, ProvinceSystem.HolderOf(seat, 2)); Assert.True(w.AtWar(house.Id, strong)); Assert.Equal(1, w.Counter(ProvinceSystem.WarsCounter));
        Assert.StartsWith("you are at war with them", new DemandSeatCommand(me.Id, seat.Id, 2, house.Id, Cat).Refusal(ctx));
        // while they fight, the province pays its holder nothing; beaten in the field, the house loses it
        Assert.DoesNotContain(new Economy(Cat, Content.Businesses).Compute(w, strong).Lines, l => l.Kind == "province" && l.Source == seat.Provinces[2].Name);
        GiveFleet(w, strong, 0); ProvinceSystem.Tick(w, Cat); Assert.Equal(house.Id, ProvinceSystem.HolderOf(seat, 2));
        Assert.False(ProvinceSystem.Divided(seat)); if (!w.Holdings.Values.Any(h => h.SystemId == sys.Id && h.OwnerId != house.Id)) Assert.True(SystemWorks.Undivided(w, sys.Id));
        // a province can be given to another house too
        w.WorldCounters.Remove("seat_asked:" + house.Id); w.Wars.Clear(); seat.Provinces[1].HolderId = weak; w.ProvincesChanged();
        var third = w.Houses.Values.First(h => !World.Outsider(h.Id) && h.Id != house.Id && h.Id != weak && h.Id != strong);
        Assert.True(tc.Executor.TryExecute(new DemandSeatCommand(me.Id, seat.Id, 1, third.Id, Cat), ctx)); Assert.Equal(third.Id, ProvinceSystem.HolderOf(seat, 1));
    }

    [Fact]
    public void TheLeaderOverAHouse_AsksForItsWorld_AndItStepsDownOrStands()
    {
        var (w, tc, house, me, ctx) = Start(); var seat = w.HoldingsOf(house.Id).First(h => h.IsPlanet);
        // the player is asked: nothing happens until the answer
        var over = ProvinceSystem.Over(w, seat) ?? w.Houses.Values.First(h => !World.Outsider(h.Id) && h.Id != house.Id && ProvinceSystem.ProvinceOf(seat, h.Id) < 0);
        if (ProvinceSystem.Over(w, seat) == null) { foreach (var o in w.Houses.Values) if (o.Id != over.Id && o.Title == "emperor") o.Title = "duke"; over.Title = "emperor"; }
        Assert.Equal(over.Id, ProvinceSystem.Over(w, seat)!.Id); over.Treasury.Influence = 40; string rising = ProvinceSystem.HolderOf(seat, 1); int theirs = 1;
        Assert.Equal("it can go only to a house that holds a province of this world", ProvinceSystem.DemandRefusal(w, Cat, over, seat, 0, w.Houses.Values.First(h => !World.Outsider(h.Id) && h.Id != house.Id && ProvinceSystem.ProvinceOf(seat, h.Id) < 0 && h.Id != over.Id).Id));
        Assert.Equal("only the leader over its holder may ask for a world", ProvinceSystem.DemandRefusal(w, Cat, w.House(rising)!, seat, 0, rising));
        Assert.Null(ProvinceSystem.DemandRefusal(w, Cat, over, seat, 0, rising));
        Assert.Equal(ProvinceSystem.Pending, ProvinceSystem.Demand(w, Cat, over, seat, 0, rising)); var d = Assert.Single(w.SeatDemands); Assert.Contains(ProvinceSystem.DemandFlag, w.WorldFlags); Assert.Equal(house.Id, seat.OwnerId);
        // stepping down: the other family holds the world, and this one holds a province under it and is the less for it
        house.Title = "duke"; string titleBefore = house.Title; int legit = house.Legitimacy; var copy = new SeatDemand { Id = d.Id, HoldingId = d.HoldingId, Province = d.Province, ById = d.ById, AgainstId = d.AgainstId, ForId = d.ForId, Turn = d.Turn };
        Assert.True(tc.Executor.TryExecute(new AnswerSeatDemandCommand(me.Id, d.Id, true, Cat), ctx));
        Assert.Equal(rising, seat.OwnerId); Assert.Equal(rising, w.PlanetHolder(seat.SystemId)); Assert.Equal(house.Id, ProvinceSystem.HolderOf(seat, theirs)); Assert.Equal(rising, ProvinceSystem.HolderOf(seat, 0));
        Assert.Equal(Math.Clamp(legit + Cat.Provinces.StepDownLegitimacy, 0, 100), house.Legitimacy); Assert.Equal(Praetoria.Core.Progression.Title.RankOf(titleBefore) - Cat.Provinces.StepDownRanks, Praetoria.Core.Progression.Title.RankOf(house.Title)); Assert.Empty(w.SeatDemands); Assert.DoesNotContain(ProvinceSystem.DemandFlag, w.WorldFlags); Assert.False(w.AtWar(house.Id, over.Id));
        Assert.Contains(new Economy(Cat, Content.Businesses).Compute(w, house.Id).Lines, l => l.Kind == "province" && l.Source == seat.Provinces[theirs].Name);
        // standing: put it back, ask again, and refuse. It is war with the one who asked, and the world is still the house's
        seat.OwnerId = house.Id; seat.Provinces[theirs].HolderId = rising; w.HoldingsChanged(); w.ProvincesChanged(); w.SeatDemands.Add(copy);
        Assert.True(tc.Executor.TryExecute(new AnswerSeatDemandCommand(me.Id, copy.Id, false, Cat), ctx));
        Assert.Equal(house.Id, seat.OwnerId); Assert.True(w.AtWar(house.Id, over.Id)); Assert.Empty(w.SeatDemands);
        // a demand left unanswered is taken for defiance after its year
        w.Wars.Clear(); w.SeatDemands.Add(new SeatDemand { Id = "late", HoldingId = seat.Id, Province = 0, ById = over.Id, AgainstId = house.Id, ForId = rising, Turn = w.Turn - Cat.Provinces.AnswerYears - 1 });
        ProvinceSystem.Tick(w, Cat); Assert.Empty(w.SeatDemands); Assert.True(w.AtWar(house.Id, over.Id)); Assert.Equal("nothing has been asked of you", new AnswerSeatDemandCommand(me.Id, "late", true, Cat).Refusal(ctx));
    }

    [Fact]
    public void AProvince_HasSlotsOfItsOwn_AndItsHolderBuildsInThem()
    {
        // the owner, 2026-11-20: yes, a province should have its own building slots, so a house with only a province has more to do there
        var (w, tc, house, me, ctx) = Start(); var rules = Cat.Provinces; var eco = new Economy(Cat, Content.Businesses);
        var (far, idx) = w.ProvincesOf(house.Id).First(x => Cat.Works.Count(d => ProvinceSystem.Suits(Cat, w.Galaxy!.System(x.h.SystemId)!, x.h.Provinces[x.i], d)) >= 2);
        var sys = w.Galaxy!.System(far.SystemId)!; var p = far.Provinces[idx]; Assert.NotEqual(house.Id, far.OwnerId);
        // slots: one to begin with, more by the system's development and by the rank of the house that holds the province; the seat has none of its own
        house.Title = "knight"; w.Development[sys.Id] = 0; Assert.Equal(rules.SlotBase, ProvinceSystem.Slots(w, Cat, far, idx)); Assert.Equal(0, ProvinceSystem.Slots(w, Cat, far, 0));
        w.Development[sys.Id] = rules.SlotDevelopment[0] * 10; Assert.Equal(rules.SlotBase + 1, ProvinceSystem.Slots(w, Cat, far, idx));
        house.Title = "emperor"; w.Development[sys.Id] = 100; Assert.Equal(rules.SlotMax, ProvinceSystem.Slots(w, Cat, far, idx)); house.Title = "knight"; w.Development[sys.Id] = 0;
        // what suits: ordinary buildings of the world's kind; nothing for a home world's own row, nothing a system has one of, no great work
        var fits = Cat.Works.Where(d => ProvinceSystem.Suits(Cat, sys, p, d)).OrderBy(d => d.BuildTurns).ThenBy(d => d.Id, StringComparer.Ordinal).ToList();
        Assert.All(fits, d => Assert.True(!d.IsMegastructure && d.On != "world" && !d.NotHome && d.Limit == WorkDef.PerBody && d.Fits(sys.PlanetClass), d.Id));
        Assert.Equal("does not suit this province", new BuildInProvinceCommand(me.Id, far.Id, idx, "forum", Cat).Refusal(ctx)); Assert.Equal("does not suit this province", new BuildInProvinceCommand(me.Id, far.Id, idx, "sun_foundry", Cat).Refusal(ctx));
        // the holder builds, in its one slot; what the building yields is on the province's line; a second wants another slot
        var one = fits[0]; var before = eco.Compute(w, house.Id).Lines.First(l => l.Source == p.Name).Delta.Clone(); int credits = house.Treasury.Credits;
        Assert.True(tc.Executor.TryExecute(new BuildInProvinceCommand(me.Id, far.Id, idx, one.Id, Cat), ctx), new BuildInProvinceCommand(me.Id, far.Id, idx, one.Id, Cat).Refusal(ctx)); Assert.True(house.Treasury.Credits < credits || one.Cost.Credits == 0);
        Assert.StartsWith("no free slot in this province", new BuildInProvinceCommand(me.Id, far.Id, idx, fits[1].Id, Cat).Refusal(ctx));
        for (int t = 0; t < 6 && !p.Buildings.Contains(one.Id); t++) { house.Treasury.Credits = 400; house.Treasury.Materials = 400; w.SeatDemands.Clear(); tc.BeginTurn(); tc.EndTurn(); }
        Assert.Contains(one.Id, p.Buildings); if (ProvinceSystem.HolderOf(far, idx) == house.Id && !far.InRebellion && !w.AtWar(house.Id, far.OwnerId))
        {
            var after = eco.Compute(w, house.Id).Lines.First(l => l.Source == p.Name).Delta; var due = ProvinceSystem.Dues(w, Cat, far, idx); var whole = ProvinceSystem.Yield(w, Cat, far, p);
            foreach (var r in Resource.All) Assert.Equal(whole.Get(r) - due.Get(r), after.Get(r)); Assert.Equal("already built here", new BuildInProvinceCommand(me.Id, far.Id, idx, one.Id, Cat).Refusal(ctx));
        }
        // it is the holder's to build in, and nobody else's: not even the house that holds the world
        var seat = w.HoldingsOf(house.Id).FirstOrDefault(h => h.IsPlanet && ProvinceSystem.HolderOf(h, 1) != house.Id);
        if (seat != null) Assert.Equal("it is not yours to build in", new BuildInProvinceCommand(me.Id, seat.Id, 1, fits[0].Id, Cat).Refusal(ctx));
        // the building goes with the province
        far.Provinces[idx].HolderId = far.OwnerId == "x" ? "" : w.Houses.Values.First(h => !World.Outsider(h.Id) && h.Id != house.Id && h.Id != far.OwnerId).Id; w.ProvincesChanged();
        Assert.DoesNotContain(eco.Compute(w, house.Id).Lines, l => l.Source == p.Name); Assert.Contains(one.Id, p.Buildings);
    }

    [Fact]
    public void AHouseWithAProvince_OwesTheHouseOverIt_ATithe_ALevyInWar_AndItsVoice()
    {
        // the owner, 2026-11-20: yes, a province holder owes the house over it "a share of yield, troops, a vote"
        var (w, _, house, _, _) = Start(); var rules = Cat.Provinces; var eco = new Economy(Cat, Content.Businesses); w.Wars.Clear(); foreach (var n in w.Nations) n.AtWar = false;
        var seat = w.HoldingsOf(house.Id).First(h => h.IsPlanet); var under = w.House(ProvinceSystem.HolderOf(seat, 1))!; var p = seat.Provinces[1];
        // at peace: a tithe, by the province's level, off the holder's line and onto the governor's
        var due = ProvinceSystem.Dues(w, Cat, seat, 1); foreach (var r in Resource.All) Assert.Equal(rules.Tithe.Get(r), due.Get(r)); Assert.True(due.Credits > 0);
        Assert.Equal(due.Credits, eco.Compute(w, house.Id).Lines.First(l => l.Source == "Dues of " + p.Name).Delta.Credits);
        var theirs = eco.Compute(w, under.Id).Lines.First(l => l.Source == p.Name).Delta; var whole = ProvinceSystem.Yield(w, Cat, seat, p); foreach (var r in Resource.All) Assert.Equal(whole.Get(r) - due.Get(r), theirs.Get(r));
        p.Level = 2; Assert.Equal(2 * rules.Tithe.Credits, ProvinceSystem.Dues(w, Cat, seat, 1).Credits); p.Level = 1;
        // the governor at war: the levy as well
        var foe = w.Houses.Values.First(h => !World.Outsider(h.Id) && h.Id != house.Id && h.Id != under.Id); w.Wars.Add(new War { HouseA = house.Id, HouseB = foe.Id, Since = w.Turn });
        var war = ProvinceSystem.Dues(w, Cat, seat, 1); foreach (var r in Resource.All) Assert.Equal(rules.Tithe.Get(r) + rules.Levy.Get(r), war.Get(r)); Assert.True(war.Manpower > 0);
        // a house at war with the house over it owes it nothing, and a house owes nothing for a seat or for its own
        w.Wars.Clear(); w.Wars.Add(new War { HouseA = under.Id, HouseB = house.Id, Since = w.Turn }); Assert.True(ProvinceSystem.Dues(w, Cat, seat, 1).IsEmpty()); Assert.False(ProvinceSystem.Under(w, under.Id, house.Id));
        w.Wars.Clear(); Assert.True(ProvinceSystem.Dues(w, Cat, seat, 0).IsEmpty());
        // its voice: it leans the governor's way in the Senate, and that is given as the reason
        Assert.True(ProvinceSystem.Under(w, under.Id, house.Id)); Assert.False(ProvinceSystem.Under(w, house.Id, under.Id) && !w.ProvincesOf(house.Id).Any(x => x.h.OwnerId == under.Id));
        var (lean, why) = OrderSystem.Sway(w, under, house); Assert.True(lean >= Math.Min(OrderSystem.SwayCap, ProvinceSystem.VoteLean)); Assert.Contains("holds a province under it", why);
        var stranger = w.Houses.Values.First(h => !World.Outsider(h.Id) && h.Id != house.Id && !ProvinceSystem.Under(w, h.Id, house.Id)); Assert.DoesNotContain("holds a province under it", OrderSystem.Sway(w, stranger, house).why);
    }

    [Fact]
    public void AHeadOfState_IsTheHeadOfAState()
    {
        // the owner, 2026-11-20: a king is the head of a monarchy "not big enough to be an empire"; in the Republic the head is a consul
        var (w, _, house, me, _) = Start(); w.Offices.RemoveAll(o => o.HouseId == house.Id); w.Polities.Clear(); bool leads = PolicySystem.RegionsDominated(w, house.Id).Any();
        house.Title = "archduke"; Assert.Equal(SystemWorks.StandingRegion, SystemWorks.Standing(w, house)); Assert.Null(SystemWorks.HeadOfState(w, house));   // a great lord under someone is not a head of state
        house.Title = "baron"; if (!leads) Assert.Equal(SystemWorks.StandingHouse, SystemWorks.Standing(w, house));
        w.Offices.Add(new OfficeHolding { OfficeId = "consul_junior", HouseId = house.Id, CharacterId = me.Id }); Assert.Equal("a consul", SystemWorks.HeadOfState(w, house)); Assert.Equal(3, SystemWorks.Capacity(w, Cat, house));
        w.Offices.RemoveAll(o => o.HouseId == house.Id);
        foreach (var (form, word) in new[] { ("kingdom", "the king of"), ("republic", "the president of"), ("dictatorship", "the dictator of") })
        {
            w.Polities.Clear(); w.Polities.Add(new Polity { Id = "p", Name = "the Marches", Form = form, HeadHouseId = house.Id, Members = { house.Id } });
            Assert.StartsWith(word, SystemWorks.HeadOfState(w, house)); Assert.Equal(SystemWorks.StandingState, SystemWorks.Standing(w, house)); Assert.Equal(3, SystemWorks.Capacity(w, Cat, house)); Assert.StartsWith(word, SystemWorks.StandingName(w, house));
        }
        w.Polities.Clear(); house.Title = "emperor"; Assert.Equal(5, SystemWorks.Capacity(w, Cat, house));
        var nation = w.Houses.Values.FirstOrDefault(h => Nation.IsHouse(h.Id)); if (nation != null) Assert.Equal(SystemWorks.StandingState, SystemWorks.Standing(w, nation));
    }

    [Fact]
    public void ACrowdedPlace_TroublesItsWorld()
    {
        var (w, _, house, _, _) = Start(); var seat = w.HoldingsOf(house.Id).First(h => h.IsPlanet); var sys = w.Galaxy!.System(seat.SystemId)!; int home = sys.Bodies.FindIndex(b => b.IsPrimary);
        house.Title = "duke"; w.Development[sys.Id] = 100; int slots = SystemWorks.Slots(w, Cat, sys, home); seat.Buildings.Clear(); for (int i = 0; i < slots; i++) seat.Buildings.Add("filler_" + i);
        Assert.Equal(0, SystemWorks.Crowding(w, Cat, sys.Id)); int calm = SystemWorks.Unrest(w, Cat, sys.Id);
        // its development falls and a lesser house holds it: the buildings stand, there is no room for them, and the world is the worse for each
        house.Title = "knight"; w.Development[sys.Id] = 0; int fewer = SystemWorks.Slots(w, Cat, sys, home); Assert.True(fewer < slots);
        Assert.Equal(slots - fewer, SystemWorks.Crowding(w, Cat, sys.Id)); Assert.Equal(calm + slots - fewer, SystemWorks.Unrest(w, Cat, sys.Id)); Assert.Equal(slots, seat.Buildings.Count);
    }

    [Fact]
    public void RivalsAskToo_AndTheWorldHoldsTogether_AndASaveKeepsIt()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 11); var tc = new TurnController(w, Content);
        for (int t = 0; t < 60 && !w.IsDynastyDead; t++) { foreach (var d in w.SeatDemands.ToList()) ProvinceSystem.Resolve(w, Cat, d, true); tc.BeginTurn(); tc.EndTurn(); }
        Assert.True(w.Counter(ProvinceSystem.DemandsCounter) >= 3, $"{w.Counter(ProvinceSystem.DemandsCounter)} demands in sixty years");
        Assert.True(w.Counter(ProvinceSystem.WarsCounter) <= 12, $"{w.Counter(ProvinceSystem.WarsCounter)} wars over a seat in sixty years");
        Assert.All(w.Holdings.Values.Where(h => h.IsPlanet), h => { Assert.Equal(3, h.Provinces.Count); for (int i = 1; i < 3; i++) Assert.NotNull(w.House(ProvinceSystem.HolderOf(h, i))); });
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        foreach (var h in w.Holdings.Values.Where(h => h.IsPlanet)) Assert.Equal(h.Provinces.Select(p => p.Name + p.Focus + p.HolderId + p.Level), back.Holding(h.Id)!.Provinces.Select(p => p.Name + p.Focus + p.HolderId + p.Level));
        Assert.Equal(w.SeatDemands.Count, back.SeatDemands.Count);
    }
}
