using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Galaxy;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Breaking away and how the great war ends (docs/BREAKAWAY.md): a house that controls a region founds a state; a weary Reach
/// lets it go and another fights; the war of independence has no fixed end; the great war runs ten years at least and ends by
/// crushing, partition or exhaustion, never by the clock or the capital alone.</summary>
public class BreakawayTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static (TurnController tc, World w, Character me, House mine, CommandContext ctx) Start(string scenario = "the_old_lion", int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content); tc.BeginTurn(); w.Protagonist!.Age = Math.Min(w.Protagonist.Age, 45);
        foreach (var x in w.Wars.ToList()) w.Wars.Remove(x);
        return (tc, w, w.Protagonist!, w.House(w.Protagonist!.HouseId)!, new CommandContext(w, TestContent.Rng()));
    }
    private static List<Holding> Worlds(World w, string regionId) => w.Holdings.Values.Where(h => h.IsPlanet && w.Galaxy!.System(h.SystemId)?.RegionId == regionId).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
    /// <summary>A region that is not the capital's, of this ring if there is one, with the house given this many hundredths of its worlds.</summary>
    private static string Give(World w, House h, int percent, string ring)
    {
        string capitalRegion = w.Galaxy!.System(w.Galaxy.Capital)!.RegionId;
        var region = w.Galaxy.Regions.Where(r => r.Id != capitalRegion && Worlds(w, r.Id).Count >= 3).OrderByDescending(r => r.Ring == ring).ThenByDescending(r => Worlds(w, r.Id).Count).ThenBy(r => r.Id, StringComparer.Ordinal).First();
        var worlds = Worlds(w, region.Id); int take = (int)Math.Ceiling(worlds.Count * percent / 100.0);
        foreach (var x in worlds.Where(x => x.OwnerId == h.Id)) x.OwnerId = "nobody_" + x.Id;                 // start from none of them
        foreach (var x in worlds.Take(take)) x.OwnerId = h.Id;
        return region.Id;
    }
    private static void Sound(World w) { w.WorldCounters.Remove(CaesarSystem.GreatWarEndedKey); w.WorldCounters.Remove(PolitySystem.CivilWarEndedKey); foreach (var h in w.Houses.Values) h.Bloc = ""; w.CivilWar = null; }

    [Fact]
    public void A_house_must_control_a_region_to_take_it_out()
    {
        var (tc, w, me, mine, ctx) = Start(); string capitalRegion = w.Galaxy!.System(w.Galaxy.Capital)!.RegionId;
        Assert.Contains("capital", tc.Polities.DeclareRefusal(w, mine, capitalRegion, PolitySystem.Republic));
        string region = Give(w, mine, 10, Ring.Rim); foreach (var x in Worlds(w, region).Where(x => x.OwnerId == mine.Id).Skip(0)) { }
        // one world in ten is not a quarter
        if (Worlds(w, region).Count(x => x.OwnerId == mine.Id) * 4 < Worlds(w, region).Count) Assert.Contains("quarter", tc.Polities.DeclareRefusal(w, mine, region, PolitySystem.Republic));
        region = Give(w, mine, 70, Ring.Rim);
        Assert.Null(tc.Polities.DeclareRefusal(w, mine, region, PolitySystem.Kingdom));
        Assert.Contains("form", tc.Polities.DeclareRefusal(w, mine, region, "empire"));
        Assert.Null(new SecessionCommand(me.Id, SecessionCommand.Declare, region, PolitySystem.Kingdom, tc.Polities).Refusal(ctx));
        tc.Polities.Tick(w); Assert.True(w.HasFlag(PolitySystem.PossibleFlag));
    }

    [Fact]
    public void A_weary_reach_lets_a_region_go_and_the_house_leaves_its_senate()
    {
        var (tc, w, me, mine, ctx) = Start(); Sound(w); string region = Give(w, mine, 70, Ring.Rim);
        w.WorldCounters[CaesarSystem.GreatWarEndedKey] = w.Turn;                                   // it has just fought the great war
        var (temper, why) = tc.Polities.Temper(w, mine, region); Assert.True(temper <= 0, $"temper {temper}: {string.Join("; ", why)}");
        int before = tc.Senate.Weight(w, mine.Id); Assert.True(before > 0);
        Assert.True(tc.Executor.TryExecute(new SecessionCommand(me.Id, SecessionCommand.Declare, region, PolitySystem.Kingdom, tc.Polities), ctx));
        var p = PolitySystem.Of(w, mine.Id)!; Assert.Equal(PolitySystem.Kingdom, p.Form); Assert.Contains("Kingdom", p.Name); Assert.True(p.Recognised > 0); Assert.Equal(0, p.WarSince);
        Assert.False(PolitySystem.InReach(w, mine.Id)); Assert.DoesNotContain(w.Wars, x => x.Involves(mine.Id));
        Assert.Equal(0, tc.Senate.Weight(w, mine.Id)); Assert.False(tc.Senate.MayPropose(w, mine.Id)); Assert.False(ElectionSystem.Eligible(w, Content.Offices, mine.Id));
        Assert.DoesNotContain(w.Factions, f => f.Members.Contains(mine.Id)); Assert.NotNull(tc.Caesar.CrossRefusal(w, mine));
        Assert.True(w.HasFlag(PolitySystem.DeclaredFlag)); Assert.True(w.HasFlag(PolitySystem.LetGoFlag));
        tc.Polities.Tick(w); Assert.True(w.HasFlag(PolitySystem.IndependentFlag));
        Assert.NotNull(tc.Polities.DeclareRefusal(w, mine, region, PolitySystem.Republic));         // once
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w)); Assert.Single(back.Polities); Assert.Equal(p.Id, back.House(mine.Id)!.PolityId);
    }

    [Fact]
    public void A_reach_that_will_fight_is_fought_for_as_long_as_the_region_is_held()
    {
        var (tc, w, me, mine, ctx) = Start(); Sound(w); string region = Give(w, mine, 100, Ring.Core);
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == mine.Id)) f.Strength = 1;         // nothing but the will
        var (temper, _) = tc.Polities.Temper(w, mine, region); Assert.True(temper > 0);
        var p = tc.Polities.Declare(w, mine, region, PolitySystem.Republic);
        Assert.Equal(0, p.Recognised); Assert.True(p.WarSince > 0); Assert.NotEmpty(p.Foes); Assert.All(p.Foes, f => Assert.True(w.AtWar(f, mine.Id)));
        tc.Polities.Tick(w); Assert.True(w.HasFlag(PolitySystem.WarFlag));
        // it holds the region, and nothing ends the war but the Reach's will running out
        int years = 0; while (p.Recognised == 0 && years < 20 && w.Polities.Contains(p)) { w.Turn += 1; years++; tc.Polities.Tick(w); }
        Assert.True(p.Recognised > 0, "the Reach never tired"); Assert.True(years >= PolitySystem.WinYears); Assert.Equal(temper * PolitySystem.TemperFallsEvery, Math.Max(years, temper * PolitySystem.TemperFallsEvery));
        Assert.True(w.HasFlag(PolitySystem.WonFlag)); Assert.DoesNotContain(w.Wars, x => x.Involves(mine.Id));

        // the same, but the region is lost: the state is no more and the house that led it out pays
        var (tc2, w2, _, mine2, _) = Start(); Sound(w2); string region2 = Give(w2, mine2, 100, Ring.Core); int legit = mine2.Legitimacy;
        var p2 = tc2.Polities.Declare(w2, mine2, region2, PolitySystem.Dictatorship); Assert.Equal(0, p2.Recognised);
        foreach (var x in Worlds(w2, region2)) x.OwnerId = p2.Foes[0];
        w2.Turn += 1; tc2.Polities.Tick(w2);
        Assert.Empty(w2.Polities); Assert.True(PolitySystem.InReach(w2, mine2.Id)); Assert.True(mine2.Legitimacy < legit || legit == 0);
        Assert.True(CaesarSystem.Enemy(w2, mine2.Id)); Assert.True(w2.HasFlag(PolitySystem.LostFlag)); Assert.DoesNotContain(w2.Wars, x => x.Involves(mine2.Id));
    }

    [Fact]
    public void The_great_war_lasts_ten_years_and_the_capital_alone_does_not_end_it()
    {
        var (tc, w, _, mine, _) = Start(); Sound(w); var sys = new GreatCrisisSystem(Content.GreatCrisis, tc.Fleets) { Polities = tc.Polities };
        sys.FormBlocs(w); sys.BlocWar(w); Assert.Equal(w.Turn, w.Counter(GreatCrisisSystem.BeganKey));
        var hegemons = w.Houses.Values.Where(h => h.Bloc == GreatCrisisSystem.HegemonBloc).ToList(); var alliance = w.Houses.Values.Where(h => h.Bloc == GreatCrisisSystem.AllianceBloc).ToList();
        Assert.NotEmpty(hegemons); Assert.NotEmpty(alliance);
        // the hegemon's side is twice the other and sits in the capital: it is winning, and it has not won
        foreach (var f in w.Fleets.Values) f.Strength = 0;
        var hf = w.Fleets.Values.First(f => hegemons.Any(h => h.Id == f.OwnerId)); hf.Strength = 20; hf.SystemId = w.Galaxy!.Capital; hf.DestinationId = "";
        var af = w.Fleets.Values.First(f => alliance.Any(h => h.Id == f.OwnerId)); af.Strength = 10;
        foreach (var x in w.Holdings.Values.Where(x => x.IsPlanet && alliance.Any(h => h.Id == x.OwnerId))) x.OwnerId = hegemons[0].Id;   // and the other side holds no region
        Assert.Null(sys.Ending(w));
        // at sixteen years both are spent
        w.Turn += GreatCrisisSystem.ExhaustionYears; Assert.Equal(GreatCrisisSystem.Exhaustion, sys.Ending(w)); w.Turn -= GreatCrisisSystem.ExhaustionYears;
        // all resistance crushed: that ends it
        af.Strength = 2; Assert.Equal(GreatCrisisSystem.Crushed, sys.Ending(w));
        // the clock cannot end it before the tenth year
        w.WorldCounters[GreatCrisisSystem.ClockCounter] = 100000; w.WorldCounters[GreatCrisisSystem.StageCounter] = 2; w.CrisisIntensity = "default";
        w.Turn = Math.Max(w.Turn, 12); w.WorldCounters[GreatCrisisSystem.BeganKey] = w.Turn;
        for (int y = 0; y < GreatCrisisSystem.MinWarYears - 1; y++) { w.Turn += 1; sys.Tick(w, TestContent.Rng()); Assert.False(w.HasFlag(GreatCrisisSystem.ResolvedFlag), $"ended in year {y + 1}"); }
        w.Turn += 1; sys.Tick(w, TestContent.Rng());
        Assert.True(w.HasFlag(GreatCrisisSystem.ResolvedFlag)); Assert.True(w.HasFlag("great_crisis:crushed")); Assert.False(w.IsImperium);
        Assert.NotNull(CaesarSystem.Grievance(w, hegemons.First(h => w.Counter(CaesarSystem.ReachWonKey(h.Id)) > 0)));   // the Reach is in its hands: what it does with it is its own affair
    }

    [Fact]
    public void A_side_that_cannot_win_but_holds_a_region_keeps_it_at_the_peace()
    {
        var (tc, w, _, mine, _) = Start(); Sound(w); var sys = new GreatCrisisSystem(Content.GreatCrisis, tc.Fleets) { Polities = tc.Polities };
        sys.FormBlocs(w); sys.BlocWar(w);
        var hegemons = w.Houses.Values.Where(h => h.Bloc == GreatCrisisSystem.HegemonBloc).ToList(); var alliance = w.Houses.Values.Where(h => h.Bloc == GreatCrisisSystem.AllianceBloc && h.Id != mine.Id).ToList();
        foreach (var f in w.Fleets.Values) f.Strength = 0;
        w.Fleets.Values.First(f => hegemons.Any(h => h.Id == f.OwnerId)).Strength = 20; w.Fleets.Values.First(f => alliance.Any(h => h.Id == f.OwnerId)).Strength = 10;
        string region = Give(w, alliance[0], 100, Ring.Rim);
        Assert.Equal(GreatCrisisSystem.PartitionEnd, sys.Ending(w));
        sys.Resolve(w);
        var p = Assert.Single(w.Polities); Assert.Equal(alliance[0].Id, p.HeadHouseId); Assert.Contains(region, p.Regions); Assert.True(p.Recognised > 0); Assert.Equal("partition", p.Origin);
        Assert.True(w.HasFlag("great_crisis:partition")); Assert.Empty(w.Wars); Assert.False(PolitySystem.InReach(w, alliance[0].Id)); Assert.False(w.IsImperium);
        // the player's own house is never taken out for them
        var (tc2, w2, _, mine2, _) = Start(); Sound(w2); var sys2 = new GreatCrisisSystem(Content.GreatCrisis, tc2.Fleets) { Polities = tc2.Polities };
        sys2.FormBlocs(w2); sys2.BlocWar(w2); string losing = mine2.Bloc; foreach (var f in w2.Fleets.Values) f.Strength = 0;
        w2.Fleets.Values.First(f => w2.House(f.OwnerId) is { } o && o.Bloc.Length > 0 && o.Bloc != losing).Strength = 20; w2.Fleets.Values.First(f => f.OwnerId == mine2.Id).Strength = 10;
        foreach (var x in w2.Holdings.Values.Where(x => x.IsPlanet && w2.House(x.OwnerId)?.Bloc == losing && x.OwnerId != mine2.Id)) x.OwnerId = "nobody_" + x.Id;
        Give(w2, mine2, 100, Ring.Rim);
        Assert.NotEqual(GreatCrisisSystem.PartitionEnd, sys2.Ending(w2));
        w2.WorldFlags.Add(GreatCrisisSystem.PartitionWishFlag); Assert.Equal(GreatCrisisSystem.PartitionEnd, sys2.Ending(w2));
    }

    [Fact]
    public void Those_who_fought_a_general_who_took_the_throne_need_not_kneel()
    {
        var (tc, w, me, mine, ctx) = Start(); Sound(w); if (RegionSystem.CapitalHolder(w)?.Id == mine.Id) return;
        var rival = w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId && Economy.HeadOf(w, h) != null && h.Id != RegionSystem.CapitalHolder(w)?.Id).OrderByDescending(h => FleetSystem.StrengthOf(w, h.Id)).ThenBy(h => h.Id, StringComparer.Ordinal).First();
        var g = Economy.HeadOf(w, rival)!; var at = w.HoldingsOf(rival.Id).First(x => x.IsPlanet).SystemId;
        w.Fleets["legion"] = new Fleet { Id = "legion", Name = "The Tenth", OwnerId = rival.Id, SystemId = at, HomeSystemId = at, Strength = 14, AdmiralId = g.Id, DevotedTo = g.Id, Devotion = 80 };
        w.WorldCounters[FleetSystem.ValourKey(g.Id)] = 5; g.Flags.Add(FleetSystem.AchillesFlag); g.CareerTrack = "military"; g.CareerRank = 4;
        tc.Caesar.TriumphVoted(w, rival, null, passed: false); tc.Caesar.Cross(w, rival);
        Assert.True(tc.Executor.TryExecute(new CaesarCommand(me.Id, CaesarCommand.Join, CaesarSystem.Senate, tc.Caesar), ctx));
        tc.Caesar.Win(w, w.CivilWar!); Assert.True(w.IsImperium);
        string region = Give(w, mine, 80, Ring.Rim);
        tc.Polities.Tick(w); Assert.True(w.HasFlag(PolitySystem.RefuseFlag)); w.PoolsFor(me.Id).Influence = 9; Fires(w, "you_need_not_kneel");
        // the throne has just come through a civil war: it is weary, and the rim is far
        var (temper, why) = tc.Polities.Temper(w, mine, region); Assert.Contains(why, x => x.Contains("no stomach"));
        var p = tc.Polities.Declare(w, mine, region, PolitySystem.Republic); Assert.False(PolitySystem.InReach(w, mine.Id));
        if (temper <= 0) Assert.True(p.Recognised > 0); else Assert.NotEmpty(p.Foes);
    }

    /// <summary>Make this house the only one in the Reach that brings up a strategic resource, from a world of its own in the region.</summary>
    private static void Corner(World w, House h, string regionId, string resource)
    {
        foreach (var sys in w.Galaxy!.Systems.Values) sys.Deposits.Remove(resource);
        var world = Worlds(w, regionId).First(x => x.OwnerId == h.Id); w.Galaxy.System(world.SystemId)!.Deposits[resource] = 3;
        string id = resource == Resource.Helium3 ? "helium_skimmers" : resource == Resource.RareEarths ? "rare_earth_refinery" : "isotope_works";
        Assert.Equal(resource, Content.Holdings.Building(id)!.RequiresDeposit);
        world.Buildings.Add(id);
    }

    [Fact]
    public void A_region_that_can_stand_alone_is_harder_to_keep_and_one_that_cannot_is_easier()
    {
        var (tc, w, _, mine, _) = Start(); Sound(w); string region = Give(w, mine, 100, Ring.Middle);
        foreach (var sys in w.Galaxy!.Systems.Values) sys.Deposits.Remove(Resource.Helium3);          // nobody brings up fuel
        mine.Treasury.Credits = 50;
        var bare = tc.Polities.StandAlone(w, mine, region); int temperBare = tc.Polities.Temper(w, mine, region).temper;
        Assert.False(bare.tests[0].ok); Assert.True(bare.starved); Assert.False(bare.leverage); Assert.Contains(tc.Polities.Temper(w, mine, region).why, x => x.Contains("starved"));
        Corner(w, mine, region, Resource.Helium3);                                                   // now it is the Reach's only fuel
        var held = tc.Polities.StandAlone(w, mine, region); int temperHeld = tc.Polities.Temper(w, mine, region).temper;
        Assert.True(held.tests[0].ok); Assert.False(held.starved); Assert.True(held.leverage); Assert.True(held.tests[2].ok);
        Assert.Equal(temperBare - 3, temperHeld);                                                    // no longer starved (+1 gone), and a monopoly (−2)
        Assert.Contains(tc.Polities.Temper(w, mine, region).why, x => x.Contains("cannot do without"));
        // and in the war that follows the Reach's will runs out faster
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == mine.Id)) f.Strength = 1;
        var p = tc.Polities.Declare(w, mine, region, PolitySystem.Republic); Assert.True(p.Leverage); Assert.False(p.Starved);
        if (p.Recognised == 0) { p.Temper = 2; w.Turn = p.WarSince + 4; Assert.Equal(0, PolitySystem.Will(w, p)); p.Leverage = false; Assert.Equal(1, PolitySystem.Will(w, p)); }
        // a state with no fuel of its own is shut out of the Reach's exchanges while it fights
        var (tc2, w2, _, mine2, _) = Start(); Sound(w2); string r2 = Give(w2, mine2, 100, Ring.Core); foreach (var sys in w2.Galaxy!.Systems.Values) sys.Deposits.Remove(Resource.Helium3);
        foreach (var f in w2.Fleets.Values.Where(f => f.OwnerId == mine2.Id)) f.Strength = 1;
        var p2 = tc2.Polities.Declare(w2, mine2, r2, PolitySystem.Republic); Assert.True(p2.Starved);
        if (p2.Recognised == 0) { tc2.Polities.Tick(w2); Assert.True(SenateSystem.Embargoed(w2, mine2.Id)); }
    }

    [Fact]
    public void A_house_of_the_region_can_be_brought_over_and_the_capitals_creature_tells_the_capital()
    {
        var (tc, w, me, mine, ctx) = Start(); Sound(w); string region = Give(w, mine, 30, Ring.Rim); mine.Treasury.Credits = 100; w.PoolsFor(me.Id).Influence = 9;
        var chair = RegionSystem.CapitalHolder(w)!; var other = w.Houses.Values.First(h => h.Id != mine.Id && h.Id != chair.Id && h.Id != PirateSystem.HouseId && Economy.HeadOf(w, h) != null);
        foreach (var x in Worlds(w, region).Where(x => x.OwnerId != mine.Id)) x.OwnerId = other.Id;        // the rest of the region is one other house's
        var oh = Economy.HeadOf(w, other)!; w.Relationship(oh.Id, me.Id).Disposition = 10; w.Relationship(oh.Id, Economy.HeadOf(w, chair)!.Id).Disposition = 0; other.PatronId = "";
        if (PolitySystem.Joins(w, other, mine)) return;                                                // it already depends on the house: nothing to prove
        Assert.Contains("three in five", tc.Polities.DeclareRefusal(w, mine, region, PolitySystem.Republic));
        Assert.Contains(PolitySystem.Neighbours(w, mine), h => h.Id == other.Id);
        Assert.True(tc.Executor.TryExecute(new WorkHouseCommand(me.Id, other.Id, PolitySystem.Word, tc.Polities), ctx)); Assert.Equal(18, PolitySystem.Regard(w, other, mine));
        Assert.NotNull(new WorkHouseCommand(me.Id, other.Id, PolitySystem.Gift, tc.Polities).Refusal(ctx));   // once a year
        w.Turn += 1; Assert.True(tc.Executor.TryExecute(new WorkHouseCommand(me.Id, other.Id, PolitySystem.Gift, tc.Polities), ctx));
        Assert.True(PolitySystem.Regard(w, other, mine) >= PolitySystem.JoinRegard); Assert.True(PolitySystem.Joins(w, other, mine)); Assert.True(w.HasFlag(PolitySystem.WonOverFlag)); Fires(w, "a_house_gives_its_word");
        Assert.Equal(100 - PolitySystem.GiftCredits, mine.Treasury.Credits);
        Assert.Null(tc.Polities.DeclareRefusal(w, mine, region, PolitySystem.Republic));                // the region would go now
        w.Relationship(oh.Id, me.Id).Disposition = 5; Assert.True(PolitySystem.Joins(w, other, mine));   // its word holds while it does not turn against the house
        w.Relationship(oh.Id, me.Id).Disposition = -5; Assert.False(PolitySystem.Joins(w, other, mine));

        // a house that is the capital's creature, and no friend of this one, reports the approach
        var (tc2, w2, me2, mine2, ctx2) = Start(); Sound(w2); string r2 = Give(w2, mine2, 30, Ring.Rim); w2.PoolsFor(me2.Id).Influence = 9;
        var chair2 = RegionSystem.CapitalHolder(w2)!; var loyal = w2.Houses.Values.First(h => h.Id != mine2.Id && h.Id != chair2.Id && h.Id != PirateSystem.HouseId && Economy.HeadOf(w2, h) != null);
        foreach (var x in Worlds(w2, r2).Where(x => x.OwnerId != mine2.Id)) x.OwnerId = loyal.Id;
        var lh = Economy.HeadOf(w2, loyal)!; w2.Relationship(lh.Id, me2.Id).Disposition = 0; w2.Relationship(lh.Id, Economy.HeadOf(w2, chair2)!.Id).Disposition = 50;
        int legit = mine2.Legitimacy, chairRegard = PolitySystem.Regard(w2, chair2, mine2);
        Assert.True(tc2.Executor.TryExecute(new WorkHouseCommand(me2.Id, loyal.Id, PolitySystem.Word, tc2.Polities), ctx2));
        Assert.Equal(0, PolitySystem.Regard(w2, loyal, mine2)); Assert.True(PolitySystem.Regard(w2, chair2, mine2) < chairRegard); Assert.True(mine2.Legitimacy < legit || legit == 0);
        Assert.True(w2.HasFlag(PolitySystem.ReportedFlag)); Fires(w2, "they_told_the_capital");
        Assert.NotNull(new WorkHouseCommand(me2.Id, chair2.Id, PolitySystem.Word, tc2.Polities).Refusal(ctx2));   // the capital's holder is not to be had
    }

    [Fact]
    public void A_recognised_state_has_a_treaty_and_the_reach_may_come_back_for_it_when_it_lapses()
    {
        var (tc, w, me, mine, ctx) = Start(); Sound(w); string region = Give(w, mine, 80, Ring.Rim); mine.Treasury.Credits = 100; w.PoolsFor(me.Id).Influence = 9;
        w.WorldCounters[CaesarSystem.GreatWarEndedKey] = w.Turn; var p = tc.Polities.Declare(w, mine, region, PolitySystem.Republic);
        if (p.Recognised == 0) tc.Polities.Recognise(w, p);
        Assert.Equal(w.Turn + PolitySystem.TreatyYears, p.TreatyUntil);
        // while it holds: no war either way, and no motion to go back
        var chair = RegionSystem.CapitalHolder(w)!; var ch = Economy.HeadOf(w, chair)!; w.PoolsFor(ch.Id).Influence = 9;
        Assert.NotNull(PolitySystem.TreatyBetween(w, chair.Id, mine.Id)); Assert.Null(PolitySystem.TreatyBetween(w, chair.Id, chair.Id));
        Assert.False(new DeclareWarCommand(ch.Id, mine.Id, Content.Fleets).CanExecute(new CommandContext(w, TestContent.Rng())));
        Assert.False(new DeclareWarCommand(me.Id, chair.Id, Content.Fleets).CanExecute(ctx));
        Assert.Contains("treaty", tc.Polities.ReconquestRefusal(w, mine)); Assert.Contains("not a state", tc.Polities.ReconquestRefusal(w, chair));
        // when it lapses and the Reach is rested, the Senate may vote to come back
        w.Turn = p.TreatyUntil; w.WorldCounters.Remove(CaesarSystem.GreatWarEndedKey);
        Assert.Null(PolitySystem.TreatyBetween(w, chair.Id, mine.Id)); Assert.Null(tc.Polities.ReconquestRefusal(w, mine));
        w.WorldCounters[CaesarSystem.GreatWarEndedKey] = w.Turn; Assert.Contains("stomach", tc.Polities.ReconquestRefusal(w, mine)); w.WorldCounters.Remove(CaesarSystem.GreatWarEndedKey);
        Assert.True(tc.Polities.ReconquestLean(w, chair, mine, 0) > 0);                                // the capital's holder wants it back
        tc.Polities.Reconquer(w, p);
        Assert.Equal(0, p.Recognised); Assert.Equal(1, p.Reconquests); Assert.NotEmpty(p.Foes); Assert.True(w.AtWar(chair.Id, mine.Id)); Assert.True(w.HasFlag(PolitySystem.ReconquestFlag));
        Fires(w, "they_come_back_for_it");
        // it holds on again, and is recognised again, with a new treaty
        int years = 0; while (p.Recognised == 0 && years < 25 && w.Polities.Contains(p)) { w.Turn += 1; years++; tc.Polities.Tick(w); }
        Assert.True(p.Recognised > 0); Assert.Equal(p.Recognised + PolitySystem.TreatyYears, p.TreatyUntil);
    }

    [Fact]
    public void A_state_may_come_back_into_the_reach_of_its_own_will()
    {
        var (tc, w, me, mine, ctx) = Start(); Sound(w); string region = Give(w, mine, 80, Ring.Rim); w.WorldCounters[CaesarSystem.GreatWarEndedKey] = w.Turn; w.PoolsFor(me.Id).Influence = 9;
        Assert.NotNull(new SecessionCommand(me.Id, SecessionCommand.Rejoin, "", "", tc.Polities).Refusal(ctx));     // nothing to come back from
        var p = tc.Polities.Declare(w, mine, region, PolitySystem.Kingdom); if (p.Recognised == 0) tc.Polities.Recognise(w, p);
        int legit = mine.Legitimacy, weight = tc.Senate.Weight(w, mine.Id); Assert.Equal(0, weight);
        Assert.True(tc.Executor.TryExecute(new SecessionCommand(me.Id, SecessionCommand.Rejoin, "", "", tc.Polities), ctx));
        Assert.Empty(w.Polities); Assert.True(PolitySystem.InReach(w, mine.Id)); Assert.True(tc.Senate.Weight(w, mine.Id) > 0); Assert.Equal(legit, mine.Legitimacy);
        Assert.True(w.HasFlag(PolitySystem.RejoinedFlag)); Fires(w, "back_in_the_reach");
        Assert.Null(tc.Polities.DeclareRefusal(w, mine, region, PolitySystem.Republic));                       // and may go again another year
        // in the middle of its war it comes back on the Reach's terms: lighter than defeat, not free
        var (tc2, w2, me2, mine2, ctx2) = Start(); Sound(w2); string r2 = Give(w2, mine2, 100, Ring.Core); foreach (var f in w2.Fleets.Values.Where(f => f.OwnerId == mine2.Id)) f.Strength = 1;
        var p2 = tc2.Polities.Declare(w2, mine2, r2, PolitySystem.Republic); if (p2.Recognised > 0) return; int legit2 = mine2.Legitimacy;
        Assert.True(tc2.Executor.TryExecute(new SecessionCommand(me2.Id, SecessionCommand.Rejoin, "", "", tc2.Polities), ctx2));
        Assert.Empty(w2.Polities); Assert.DoesNotContain(w2.Wars, x => x.Involves(mine2.Id)); Assert.True(mine2.Legitimacy <= legit2); Assert.False(CaesarSystem.Enemy(w2, mine2.Id));
        // and the map knows a state when it sees one
        var (tc3, w3, _, mine3, _) = Start(); Sound(w3); string r3 = Give(w3, mine3, 80, Ring.Rim); w3.WorldCounters[CaesarSystem.GreatWarEndedKey] = w3.Turn;
        var p3 = tc3.Polities.Declare(w3, mine3, r3, PolitySystem.Kingdom);
        var seen = Praetoria.Core.Galaxy.GalaxyProjection.Regions(w3).First(x => x.Id == r3); Assert.Equal(p3.Name, seen.StateName); Assert.Equal(p3.Recognised == 0, seen.StateAtWar);
        Assert.All(Praetoria.Core.Galaxy.GalaxyProjection.Regions(w3).Where(x => x.Id != r3), x => Assert.Null(x.StateName));
    }

    private static void Fires(World w, string eventId)
    {
        var engine = new Praetoria.Core.Events.EventEngine(Content, WorldBuilder.RngFor(w));
        var found = engine.GatherEligible(w).FirstOrDefault(e => e.def.Id == eventId);
        Assert.True(found.def != null, $"{eventId} is not eligible");
        foreach (var c in found.def!.Choices)
        {
            var again = engine.GatherEligible(w).FirstOrDefault(e => e.def.Id == eventId); if (again.def == null) break;
            engine.Resolve(new Praetoria.Core.Events.FiredEvent(again.def, again.binding), w, c.Id);
        }
    }

    [Fact]
    public void Every_event_of_breaking_away_can_fire_where_it_belongs()
    {
        // let go
        var (tc, w, me, mine, _) = Start(); Sound(w); mine.Treasury.Credits = 200; w.PoolsFor(me.Id).Influence = 9; string region = Give(w, mine, 80, Ring.Rim);
        w.WorldCounters[CaesarSystem.GreatWarEndedKey] = w.Turn; tc.Polities.Declare(w, mine, region, PolitySystem.Republic);
        Fires(w, "the_declaration"); if (w.HasFlag(PolitySystem.LetGoFlag)) Fires(w, "they_let_you_go");
        // fought for, and won; fought for, and lost
        foreach (bool won in new[] { true, false })
        {
            var (tc2, w2, me2, mine2, _) = Start(); Sound(w2); mine2.Treasury.Credits = 200; w2.PoolsFor(me2.Id).Influence = 9; string r2 = Give(w2, mine2, 100, Ring.Core);
            foreach (var f in w2.Fleets.Values.Where(f => f.OwnerId == mine2.Id)) f.Strength = 1;
            var p = tc2.Polities.Declare(w2, mine2, r2, PolitySystem.Kingdom); if (p.Recognised > 0) continue;
            tc2.Polities.Tick(w2); Fires(w2, "the_war_of_independence");
            if (won) { tc2.Polities.Recognise(w2, p); Fires(w2, "independence_is_won"); } else { tc2.Polities.Dissolve(w2, p, "a test"); Fires(w2, "the_state_falls"); }
        }
        // another house leaves; the great war ends in a partition
        var (tc3, w3, me3, mine3, _) = Start(); Sound(w3); mine3.Treasury.Credits = 200; w3.PoolsFor(me3.Id).Influence = 9;
        var other = w3.Houses.Values.First(h => h.Id != mine3.Id && h.Id != PirateSystem.HouseId && h.Id != RegionSystem.CapitalHolder(w3)?.Id && Economy.HeadOf(w3, h) != null);
        string r3 = Give(w3, other, 100, Ring.Rim); w3.WorldCounters[CaesarSystem.GreatWarEndedKey] = w3.Turn; tc3.Polities.Declare(w3, other, r3, PolitySystem.Republic); Fires(w3, "a_state_breaks_away");
        var another = w3.Houses.Values.First(h => h.Id != mine3.Id && h.Id != other.Id && PolitySystem.InReach(w3, h.Id) && h.Id != PirateSystem.HouseId && h.Id != RegionSystem.CapitalHolder(w3)?.Id);
        string capitalRegion = w3.Galaxy!.System(w3.Galaxy.Capital)!.RegionId; var free = w3.Galaxy.Regions.First(r => r.Id != capitalRegion && PolitySystem.OfRegion(w3, r.Id) == null);
        tc3.Polities.Partition(w3, another, free.Id, new[] { another }); Fires(w3, "the_partition");
    }

    [Fact]
    public void Over_sixty_years_the_republic_stands_and_states_that_leave_are_whole()
    {
        foreach (int seed in new[] { 2, 9 })
        {
            var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_risen_officer"), (ulong)seed); var tc = new TurnController(w, Content); tc.BeginTurn();
            for (int y = 0; y < 60 && w.Protagonist is { Alive: true }; y++)
            {
                tc.EndTurn(); tc.BeginTurn();
                Assert.False(w.IsImperium, $"the Republic fell in year {w.Turn} with nobody trying");
                foreach (var p in w.Polities)
                {
                    Assert.NotEmpty(p.Members); Assert.Contains(p.HeadHouseId, p.Members); Assert.All(p.Members, id => Assert.Equal(p.Id, w.House(id)!.PolityId));
                    Assert.All(p.Members, id => Assert.Equal(0, tc.Senate.Weight(w, id)));
                }
                if (w.WorldCounters.TryGetValue(GreatCrisisSystem.BeganKey, out int began) && w.HasFlag(GreatCrisisSystem.ResolvedFlag) && w.Counter(CaesarSystem.GreatWarEndedKey) > 0)
                    Assert.True(w.Counter(CaesarSystem.GreatWarEndedKey) - began >= GreatCrisisSystem.MinWarYears, $"the great war lasted {w.Counter(CaesarSystem.GreatWarEndedKey) - began} years");
            }
        }
    }

    // ---------------- joining a state, stirring the camps, a second great war (docs/GAPS.md B4, B10, B9) ----------------

    /// <summary>Gives the house this many hundredths of a named region's worlds, and none of the rest.</summary>
    private static void Hold(World w, House h, string regionId, int percent)
    {
        var worlds = Worlds(w, regionId); int take = (int)Math.Ceiling(worlds.Count * percent / 100.0);
        foreach (var x in worlds.Where(x => x.OwnerId == h.Id)) x.OwnerId = "nobody_" + x.Id;
        foreach (var x in worlds.Take(take)) x.OwnerId = h.Id;
    }
    private static House Other(World w, House mine, int n = 0) => w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId && Economy.HeadOf(w, h) != null && h.Id != RegionSystem.CapitalHolder(w)?.Id)
        .OrderBy(h => h.Id, StringComparer.Ordinal).Skip(n).First();
    private static void Regard(World w, House of, House toward, int value) => w.Relationship(Economy.HeadOf(w, of)!.Id, Economy.HeadOf(w, toward)!.Id).Disposition = value;
    /// <summary>Another house's state, recognised: a weary Reach let it go.</summary>
    private static Polity StateOf(TurnController tc, World w, House founder, out string region)
    {
        region = Give(w, founder, 70, Ring.Rim); w.WorldCounters[CaesarSystem.GreatWarEndedKey] = w.Turn;
        Assert.Null(tc.Polities.DeclareRefusal(w, founder, region, PolitySystem.Republic));
        var p = tc.Polities.Declare(w, founder, region, PolitySystem.Republic); if (p.Recognised == 0) tc.Polities.Recognise(w, p);   // let go, or fought for and named in a treaty: either way it is a state
        Assert.True(p.Recognised > 0); return p;
    }

    [Fact]
    public void A_house_may_go_over_to_a_state_that_exists()
    {
        var (tc, w, me, mine, ctx) = Start(); Sound(w); var founder = Other(w, mine); var p = StateOf(tc, w, founder, out string region);
        foreach (var x in Worlds(w, region).Where(x => x.OwnerId == mine.Id)) x.OwnerId = "nobody_" + x.Id;
        Assert.Contains("holds no world", tc.Polities.AccedeRefusal(w, mine, p));
        Worlds(w, region).Last().OwnerId = mine.Id;
        Regard(w, founder, mine, -10); Assert.Contains("will not have you", tc.Polities.AccedeRefusal(w, mine, p));
        Regard(w, founder, mine, 10); int weight = tc.Senate.Weight(w, mine.Id); Assert.True(weight > 0);
        Assert.True(tc.Executor.TryExecute(new SecessionCommand(me.Id, SecessionCommand.Join, "", p.Id, tc.Polities), ctx));
        Assert.Equal(p.Id, mine.PolityId); Assert.Contains(mine.Id, p.Members); Assert.False(PolitySystem.InReach(w, mine.Id)); Assert.Equal(0, tc.Senate.Weight(w, mine.Id));
        Assert.True(w.HasFlag(PolitySystem.JoinedFlag)); Assert.True(p.Recognised > 0); Assert.Single(p.Regions); Assert.DoesNotContain(w.Wars, x => x.Involves(mine.Id));
        Assert.NotNull(tc.Polities.AccedeRefusal(w, mine, p));                                       // once
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w)); Assert.Equal(p.Id, back.House(mine.Id)!.PolityId);
    }

    [Fact]
    public void A_house_may_join_a_state_that_is_still_fighting_and_is_at_war_with_the_reach_for_it()
    {
        var (tc, w, me, mine, ctx) = Start(); Sound(w); var founder = Other(w, mine); string region = Give(w, founder, 70, Ring.Rim);
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == founder.Id)) f.Strength = 0;        // nothing to fight with: the Reach will come for it
        var p = tc.Polities.Declare(w, founder, region, PolitySystem.Republic);
        if (p.Recognised > 0) { p.Recognised = 0; p.WarSince = w.Turn; p.Foes = new List<string> { RegionSystem.CapitalHolder(w)!.Id }; }   // (a Reach too weary to fight: make it fight, for the test)
        Worlds(w, region).Last().OwnerId = mine.Id; Regard(w, founder, mine, 10);
        Assert.Contains("cannot take in a region", tc.Polities.AccedeRefusal(w, mine, p, region));                 // a region waits until it is a state
        Assert.Null(tc.Polities.AccedeRefusal(w, mine, p));
        Assert.True(tc.Executor.TryExecute(new SecessionCommand(me.Id, SecessionCommand.Join, "", p.Id, tc.Polities), ctx));
        Assert.Equal(p.Id, mine.PolityId); Assert.DoesNotContain(mine.Id, p.Foes);   // if it had been sent against the state, it is its enemy no longer
        if (p.Foes.Count == 0) p.Foes.Add(Other(w, mine, 3).Id); Assert.All(p.Foes.Where(f => w.Wars.Any(x => x.Between(f, founder.Id))), foe => Assert.True(w.AtWar(foe, mine.Id)));
        Assert.Contains(w.History, l => l.Text.Contains("in the middle of its war"));
    }

    [Fact]
    public void A_house_can_be_promised_a_place_threatened_or_bound_by_a_marriage()
    {
        var (tc, w, me, mine, ctx) = Start(); Sound(w); string region = Give(w, mine, 30, Ring.Rim); var chair = RegionSystem.CapitalHolder(w);
        var target = Other(w, mine); Worlds(w, region).Last().OwnerId = target.Id; w.PoolsFor(me.Id).Influence = 9; mine.Treasury.Credits = 200;
        Regard(w, target, mine, 12); if (chair != null) Regard(w, target, chair, 0);
        Assert.False(PolitySystem.Joins(w, target, mine));
        // a marriage between the houses: asked for less
        var theirs = w.LivingCharacters().First(c => c.HouseId == target.Id); var ours = w.LivingCharacters().First(c => c.HouseId == mine.Id && c.Id != me.Id);
        w.Marriages.Add(new Marriage { A = ours.Id, B = theirs.Id, Since = w.Turn }); Assert.True(PolitySystem.Bound(w, target, mine)); Assert.True(PolitySystem.Joins(w, target, mine));
        w.Marriages.RemoveAll(m => m.Has(ours.Id) && m.Has(theirs.Id)); Assert.False(PolitySystem.Joins(w, target, mine));
        // a promise: regard at once, and a debt
        Assert.True(tc.Executor.TryExecute(new WorkHouseCommand(me.Id, target.Id, PolitySystem.Promise, tc.Polities), ctx));
        Assert.Equal(12 + PolitySystem.PromiseRegard, PolitySystem.Regard(w, target, mine)); Assert.True(PolitySystem.IsPromised(w, target, mine)); Assert.True(PolitySystem.Joins(w, target, mine));
        w.Turn++; Assert.Contains("already", tc.Polities.WorkRefusal(w, mine, target, PolitySystem.Promise));
        // founded with them in it, the promise falls due: three credits a year, while the ruler can pay
        Hold(w, mine, region, 50); Worlds(w, region).Last().OwnerId = target.Id; w.WorldCounters[CaesarSystem.GreatWarEndedKey] = w.Turn;
        Assert.Null(tc.Polities.DeclareRefusal(w, mine, region, PolitySystem.Republic));
        var p = tc.Polities.Declare(w, mine, region, PolitySystem.Republic); if (p.Recognised == 0) tc.Polities.Recognise(w, p);
        Assert.Contains(target.Id, p.Members); Assert.Contains(target.Id, p.Promised); Assert.False(PolitySystem.IsPromised(w, target, mine));
        mine.Treasury.Credits = 50; int got = target.Treasury.Credits; w.Turn++; tc.Polities.Tick(w);
        Assert.Equal(50 - PolitySystem.PromiseCredits, mine.Treasury.Credits); Assert.Equal(got + PolitySystem.PromiseCredits, target.Treasury.Credits);
        // a ruler who cannot pay is thought the less of, and hears of it
        mine.Treasury.Credits = 0; int regard = PolitySystem.Regard(w, target, mine); w.Turn++; tc.Polities.Tick(w);
        Assert.Equal(regard - 8, PolitySystem.Regard(w, target, mine)); Assert.True(w.HasFlag(PolitySystem.UnpaidFlag)); mine.Treasury.Credits = 100; Fires(w, "the_place_you_promised");
        // ten years on the debt is done
        w.Turn = p.Since + PolitySystem.PledgeYears + 1; tc.Polities.Tick(w); Assert.Empty(p.Promised);

        // a threat: it takes three hulls to their one; they follow while they are afraid, and think the less of you
        var (tc2, w2, me2, mine2, ctx2) = Start(); Sound(w2); string r2 = Give(w2, mine2, 30, Ring.Rim); var weak = Other(w2, mine2); Worlds(w2, r2).Last().OwnerId = weak.Id; w2.PoolsFor(me2.Id).Influence = 9;
        var chair2 = RegionSystem.CapitalHolder(w2); Regard(w2, weak, mine2, 0); if (chair2 != null) Regard(w2, weak, chair2, 0);
        foreach (var f in w2.Fleets.Values.Where(f => f.OwnerId == mine2.Id || f.OwnerId == weak.Id)) f.Strength = 0;
        w2.Fleets["w"] = new Fleet { Id = "w", Name = "W", OwnerId = weak.Id, SystemId = w2.Galaxy!.Capital, HomeSystemId = w2.Galaxy.Capital, Strength = 4 };
        w2.Fleets["m"] = new Fleet { Id = "m", Name = "M", OwnerId = mine2.Id, SystemId = w2.Galaxy.Capital, HomeSystemId = w2.Galaxy.Capital, Strength = 8 };
        Assert.Contains("not afraid", tc2.Polities.WorkRefusal(w2, mine2, weak, PolitySystem.Threat));
        w2.Fleets["m"].Strength = 12; Assert.False(PolitySystem.Joins(w2, weak, mine2));
        Assert.True(tc2.Executor.TryExecute(new WorkHouseCommand(me2.Id, weak.Id, PolitySystem.Threat, tc2.Polities), ctx2));
        Assert.True(PolitySystem.Cowed(w2, weak, mine2)); Assert.True(PolitySystem.Joins(w2, weak, mine2)); Assert.Equal(PolitySystem.ThreatRegard, PolitySystem.Regard(w2, weak, mine2));
        w2.Fleets["m"].Strength = 5; Assert.False(PolitySystem.Cowed(w2, weak, mine2)); Assert.False(PolitySystem.Joins(w2, weak, mine2));   // no longer afraid
    }

    [Fact]
    public void A_member_house_may_leave_a_state_and_one_that_thinks_ill_of_its_ruler_does()
    {
        // the player's house, a member of another's state, leaves it
        var (tc, w, me, mine, ctx) = Start(); Sound(w); var founder = Other(w, mine); var p = StateOf(tc, w, founder, out string region);
        Worlds(w, region).Last().OwnerId = mine.Id; Regard(w, founder, mine, 10); Assert.Contains("of the Reach", tc.Polities.DepartRefusal(w, mine));
        Assert.True(tc.Executor.TryExecute(new SecessionCommand(me.Id, SecessionCommand.Join, "", p.Id, tc.Polities), ctx));
        Assert.Contains("yours", tc.Polities.DepartRefusal(w, founder)); Assert.Null(tc.Polities.DepartRefusal(w, mine)); int held = Worlds(w, region).Count(x => x.OwnerId == mine.Id);
        Assert.True(tc.Executor.TryExecute(new SecessionCommand(me.Id, SecessionCommand.Leave, "", "", tc.Polities), ctx));
        Assert.True(PolitySystem.InReach(w, mine.Id)); Assert.DoesNotContain(mine.Id, p.Members); Assert.True(w.HasFlag(PolitySystem.LeftFlag)); Assert.True(tc.Senate.Weight(w, mine.Id) > 0);
        Assert.Equal(held, Worlds(w, region).Count(x => x.OwnerId == mine.Id)); Assert.Equal(10 + 10 - 25, PolitySystem.Regard(w, founder, mine));   // glad to have them, and then not
        // a house of the player's state that has come to think ill of the player leaves by itself; one that is afraid stays
        var (tc2, w2, me2, mine2, _) = Start(); Sound(w2); string r2 = Give(w2, mine2, 60, Ring.Rim); var a = Other(w2, mine2); var b = Other(w2, mine2, 1);
        var ws = Worlds(w2, r2); ws[^1].OwnerId = a.Id; if (ws.Count >= 4) ws[^2].OwnerId = b.Id; Regard(w2, a, mine2, 40); Regard(w2, b, mine2, 40); w2.WorldCounters[CaesarSystem.GreatWarEndedKey] = w2.Turn;
        var own = tc2.Polities.Declare(w2, mine2, r2, PolitySystem.Kingdom); if (own.Recognised == 0) tc2.Polities.Recognise(w2, own);
        Assert.Contains(a.Id, own.Members); bool left = false;
        for (int y = 0; y < 24 && !left; y++) { w2.Turn++; Regard(w2, a, mine2, -30); tc2.Polities.Tick(w2); left = !own.Members.Contains(a.Id); }
        Assert.True(left, "in twenty-four years it never left"); Assert.True(PolitySystem.InReach(w2, a.Id)); Assert.True(w2.HasFlag(PolitySystem.LeftOursFlag));
        mine2.Treasury.Credits = 100; w2.PoolsFor(me2.Id).Influence = 9; Fires(w2, "a_house_leaves_the_state");
        if (own.Members.Contains(b.Id))
        {
            foreach (var f in w2.Fleets.Values.Where(f => f.OwnerId == b.Id)) f.Strength = 1; w2.Fleets["big"] = new Fleet { Id = "big", Name = "Big", OwnerId = mine2.Id, SystemId = w2.Galaxy!.Capital, HomeSystemId = w2.Galaxy.Capital, Strength = 30 };
            w2.WorldCounters[PolitySystem.CowedKey(b.Id, mine2.Id)] = w2.Turn;
            for (int y = 0; y < 8; y++) { w2.Turn++; Regard(w2, b, mine2, -40); w2.WorldCounters[PolitySystem.CowedKey(b.Id, mine2.Id)] = w2.Turn; tc2.Polities.Tick(w2); }
            Assert.Contains(b.Id, own.Members);                                  // afraid to
        }
    }

    [Fact]
    public void A_states_ruler_may_court_a_house_of_the_reach_on_its_border()
    {
        var (tc, w, me, mine, ctx) = Start(); Sound(w); string region = Give(w, mine, 70, Ring.Rim); w.WorldCounters[CaesarSystem.GreatWarEndedKey] = w.Turn;
        var p = tc.Polities.Declare(w, mine, region, PolitySystem.Republic); if (p.Recognised == 0) tc.Polities.Recognise(w, p);
        var inside = w.Houses.Values.Where(h => PolitySystem.InReach(w, h.Id) && h.Id != PirateSystem.HouseId && Economy.HeadOf(w, h) != null && h.Id != RegionSystem.CapitalHolder(w)?.Id).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        var far = w.Houses.Values.Where(h => PolitySystem.InReach(w, h.Id) && h.Id != inside.Id && h.Id != PirateSystem.HouseId && Economy.HeadOf(w, h) != null && !PolitySystem.Courtable(w, p, h)).OrderBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
        Worlds(w, region).Last().OwnerId = inside.Id; Assert.True(PolitySystem.Courtable(w, p, inside));
        w.PoolsFor(me.Id).Influence = 9; mine.Treasury.Credits = 100; Regard(w, inside, mine, 0); if (RegionSystem.CapitalHolder(w) is { } chair) Regard(w, inside, chair, 0);
        Assert.Null(tc.Polities.WorkRefusal(w, mine, inside, PolitySystem.Gift));
        Assert.Contains("words and gifts", tc.Polities.WorkRefusal(w, mine, inside, PolitySystem.Threat));
        if (far != null) Assert.Contains("beside it", tc.Polities.WorkRefusal(w, mine, far, PolitySystem.Word));
        Assert.True(tc.Executor.TryExecute(new WorkHouseCommand(me.Id, inside.Id, PolitySystem.Gift, tc.Polities), ctx));
        Assert.True(PolitySystem.Regard(w, inside, mine) >= PolitySystem.GiftRegard); Assert.Equal(100 - PolitySystem.GiftCredits, mine.Treasury.Credits);
    }

    [Fact]
    public void Under_a_throne_the_answer_is_the_emperors_own()
    {
        var (tc, w, _, mine, _) = Start(); Sound(w); string region = Give(w, mine, 70, Ring.Middle);
        var throne = Other(w, mine, 2); var emperor = Economy.HeadOf(w, throne)!; emperor.NatureTraits.Clear(); throne.Legitimacy = 60; Regard(w, throne, mine, 0);
        int republic = tc.Polities.Temper(w, mine, region).temper;
        ThroneSystem.Enthrone(w, throne.Id, seized: false); w.CivilWar = null; w.WorldCounters.Remove(PolitySystem.CivilWarEndedKey); Regard(w, throne, mine, 0); throne.Legitimacy = 60;
        var fresh = tc.Polities.Temper(w, mine, region); Assert.Equal(republic + 2, fresh.temper); Assert.Contains(fresh.why, x => x.Contains("new on the throne"));
        w.Turn += PolitySystem.NewThroneYears + 1; var old = tc.Polities.Temper(w, mine, region); Assert.Equal(republic + 1, old.temper); Assert.Contains(old.why, x => x.Contains("does not let provinces go"));
        emperor.NatureTraits.Add("Ruthless"); Assert.Equal(republic + 2, tc.Polities.Temper(w, mine, region).temper);
        emperor.NatureTraits.Clear(); emperor.NatureTraits.Add("Just"); Assert.Equal(republic, tc.Polities.Temper(w, mine, region).temper);
        throne.Legitimacy = 30; Assert.Equal(republic - 1, tc.Polities.Temper(w, mine, region).temper);
        Regard(w, throne, mine, 40); var kind = tc.Polities.Temper(w, mine, region); Assert.Equal(republic - 2, kind.temper); Assert.Contains(kind.why, x => x.Contains("thinks well of the house"));
        Regard(w, throne, mine, -40); Assert.Equal(republic, tc.Polities.Temper(w, mine, region).temper);
    }

    [Fact]
    public void The_camps_can_be_stirred_against_one_house()
    {
        var (tc, w, me, mine, ctx) = Start(); Sound(w); mine.Treasury.Credits = 100; w.PoolsFor(me.Id).Influence = 9; var victim = Other(w, mine);
        var others = w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != victim.Id && h.Id != PirateSystem.HouseId && PolitySystem.InReach(w, h.Id) && Economy.HeadOf(w, h) != null)
            .OrderByDescending(h => FleetSystem.StrengthOf(w, h.Id)).ThenBy(h => h.Id, StringComparer.Ordinal).ToList();
        foreach (var h in others) Regard(w, h, victim, 10);
        Assert.Contains("no such house", new SecessionCommand(me.Id, SecessionCommand.Stir, "nobody_at_all", "", tc.Polities).Refusal(ctx));
        Assert.True(tc.Executor.TryExecute(new SecessionCommand(me.Id, SecessionCommand.Stir, victim.Id, "", tc.Polities), ctx));
        Assert.All(others.Take(5), h => Assert.Equal(10 - PolitySystem.StirAgainst, PolitySystem.Regard(w, h, victim)));
        if (others.Count > 5) Assert.Equal(10, PolitySystem.Regard(w, others[5], victim));
        Assert.True(PolitySystem.Stirring(w)); Assert.Contains(w.History, l => l.Text.Contains(victim.Name) && l.Text.Contains("said"));
        // found out, the house it was aimed at hears too
        var chair = RegionSystem.CapitalHolder(w); bool heard = false;
        if (chair != null && chair.Id != mine.Id && chair.Id != victim.Id)
        {
            for (int y = 0; y < 30 && !heard; y++) { w.Turn++; w.WorldCounters.Remove(PolitySystem.StirKey); mine.Treasury.Credits = 100; Regard(w, victim, mine, 0); heard = tc.Polities.Stir(w, mine, victim); }
            Assert.True(heard); Assert.Equal(-25, PolitySystem.Regard(w, victim, mine));
        }
    }

    [Fact]
    public void A_bordering_region_may_be_taken_into_a_state_and_the_reach_lets_it_go_or_calls_the_treaty_broken()
    {
        bool letGo = false, fought = false; string trace = "";
        foreach (bool weary in new[] { true, false })
        {
            var (tc, w, me, mine, ctx) = Start(); Sound(w); var founder = Other(w, mine); var p = StateOf(tc, w, founder, out string region);
            string capitalRegion = w.Galaxy!.System(w.Galaxy.Capital)!.RegionId;
            var next = w.Galaxy.Regions.Where(r => r.Id != capitalRegion && PolitySystem.Borders(w, r.Id, region) && Worlds(w, r.Id).Count >= 2).OrderByDescending(r => r.Ring == Ring.Rim).ThenBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault();   // the rim is a long way to send a fleet
            var far = w.Galaxy.Regions.FirstOrDefault(r => r.Id != capitalRegion && r.Id != region && !PolitySystem.Borders(w, r.Id, region) && Worlds(w, r.Id).Count >= 2);
            if (next == null) continue;                                                              // (every region of the shipped galaxy has a neighbour; a custom one might not)
            Regard(w, founder, mine, 20);
            if (far != null) { Hold(w, mine, far.Id, 80); Assert.Contains("does not border", tc.Polities.AccedeRefusal(w, mine, p, far.Id)); Hold(w, mine, far.Id, 0); }
            Hold(w, mine, next.Id, 10); Assert.NotNull(tc.Polities.AccedeRefusal(w, mine, p, next.Id));   // a foothold is not control
            Hold(w, mine, next.Id, 80); Assert.Null(tc.Polities.AccedeRefusal(w, mine, p, next.Id));
            // a weary Reach facing a fleet that is a match for what it could send lets it go; a rested one facing no fleet at all does not
            if (weary) w.Fleets["armada"] = new Fleet { Id = "armada", Name = "Armada", OwnerId = mine.Id, SystemId = w.Galaxy.Capital, HomeSystemId = w.Galaxy.Capital, Strength = 400 };
            else { Sound(w); foreach (var x in w.Polities) x.Since = w.Turn - 30; foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == mine.Id)) f.Strength = 0; }
            int temper = tc.Polities.Temper(w, mine, next.Id).temper; trace += $" [{(weary ? "weary" : "rested")}: {next.Id} temper {temper}: {string.Join("; ", tc.Polities.Temper(w, mine, next.Id).why)}]";
            Assert.True(tc.Executor.TryExecute(new SecessionCommand(me.Id, SecessionCommand.Join, next.Id, p.Id, tc.Polities), ctx));
            Assert.Contains(next.Id, p.Regions); Assert.Equal(2, p.Regions.Count); Assert.Equal(p.Id, mine.PolityId); Assert.Equal(p, PolitySystem.OfRegion(w, next.Id));
            if (temper <= 0) letGo = true; else fought = true;
            if (temper <= 0) { Assert.True(p.Recognised > 0); Assert.False(w.HasFlag(PolitySystem.BreachFlag)); Assert.DoesNotContain(w.Wars, x => x.Involves(mine.Id)); }
            else
            {
                Assert.Equal(0, p.Recognised); Assert.Equal(w.Turn, p.WarSince); Assert.True(w.HasFlag(PolitySystem.BreachFlag)); Assert.NotEmpty(p.Foes);
                Assert.Contains(w.Wars, x => x.Involves(founder.Id)); Assert.Contains(w.History, l => l.Text.Contains("breach of the treaty"));
                mine.Treasury.Credits = 100; w.PoolsFor(me.Id).Influence = 9; Fires(w, "the_treaty_is_called_broken");
            }
        }
        Assert.True(letGo, "a weary Reach never let the region go:" + trace); Assert.True(fought, "a rested Reach never fought for the region:" + trace);
    }

    [Fact]
    public void A_house_of_the_reach_that_thinks_well_of_a_states_ruler_goes_over_by_itself()
    {
        var (tc, w, me, mine, _) = Start(); Sound(w); string region = Give(w, mine, 70, Ring.Rim); w.WorldCounters[CaesarSystem.GreatWarEndedKey] = w.Turn;
        var p = tc.Polities.Declare(w, mine, region, PolitySystem.Kingdom); Assert.True(p.Recognised > 0);
        var goer = w.Houses.Values.Where(h => PolitySystem.InReach(w, h.Id) && h.Id != PirateSystem.HouseId && Economy.HeadOf(w, h) != null && h.Id != RegionSystem.CapitalHolder(w)?.Id).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        Worlds(w, region).Last().OwnerId = goer.Id; var chair = RegionSystem.CapitalHolder(w);
        bool went = false;
        for (int y = 0; y < 24 && !went; y++)
        {
            w.Turn++; Regard(w, goer, mine, 50); Regard(w, mine, goer, 20); if (chair != null) Regard(w, goer, chair, -20);
            tc.Polities.Tick(w); went = p.Members.Contains(goer.Id);
        }
        Assert.True(went, "in twenty-four years it never went over"); Assert.False(PolitySystem.InReach(w, goer.Id)); Assert.True(w.HasFlag(PolitySystem.JoinedOursFlag));
        mine.Treasury.Credits = 100; w.PoolsFor(me.Id).Influence = 9; Fires(w, "a_house_comes_over");
        // one that does not think well of the ruler stays where it is
        var stays = w.Houses.Values.Where(h => PolitySystem.InReach(w, h.Id) && h.Id != PirateSystem.HouseId && Economy.HeadOf(w, h) != null && h.Id != chair?.Id).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        var spare = Worlds(w, region).FirstOrDefault(x => x.OwnerId == mine.Id && Worlds(w, region).Count(y => y.OwnerId == mine.Id) > 1);
        if (spare != null) { spare.OwnerId = stays.Id; for (int y = 0; y < 12; y++) { w.Turn++; Regard(w, stays, mine, 5); tc.Polities.Tick(w); } Assert.True(PolitySystem.InReach(w, stays.Id)); }
    }

    [Fact]
    public void Stirring_the_camps_pushes_the_reach_toward_its_great_war_and_may_be_found_out()
    {
        var (tc, w, me, mine, ctx) = Start(); Sound(w); mine.Treasury.Credits = 5; w.PoolsFor(me.Id).Influence = 9;
        Assert.Contains("credits", new SecessionCommand(me.Id, SecessionCommand.Stir, "", "", tc.Polities).Refusal(ctx));
        mine.Treasury.Credits = 60; int before = GreatCrisisSystem.Pressure(w).Sum(x => x.points);
        Assert.True(tc.Executor.TryExecute(new SecessionCommand(me.Id, SecessionCommand.Stir, "", "", tc.Polities), ctx));
        Assert.Equal(60 - PolitySystem.StirCredits, mine.Treasury.Credits); Assert.Equal(9 - PolitySystem.StirInfluence, w.PoolsFor(me.Id).Influence); Assert.True(PolitySystem.Stirring(w)); Assert.True(w.HasFlag(PolitySystem.StirredFlag));
        Assert.Equal(before + GreatCrisisSystem.StirPoints, GreatCrisisSystem.Pressure(w).Sum(x => x.points)); Assert.Contains(GreatCrisisSystem.Pressure(w), x => x.what.Contains("money"));
        Assert.Contains("already", new SecessionCommand(me.Id, SecessionCommand.Stir, "", "", tc.Polities).Refusal(ctx));
        w.Turn += PolitySystem.StirYears + 1; Assert.False(PolitySystem.Stirring(w)); Assert.Equal(before, GreatCrisisSystem.Pressure(w).Sum(x => x.points));
        // sooner or later the capital hears whose money it was
        var chair = RegionSystem.CapitalHolder(w); bool heard = false;
        if (chair != null && chair.Id != mine.Id)
        {
            for (int y = 0; y < 30 && !heard; y++) { w.Turn++; w.WorldCounters.Remove(PolitySystem.StirKey); mine.Treasury.Credits = 60; mine.Legitimacy = 50; Regard(w, chair, mine, 0); heard = tc.Polities.Stir(w, mine); }
            Assert.True(heard); Assert.Equal(47, mine.Legitimacy); Assert.Equal(-15, w.Relationship(Economy.HeadOf(w, chair)!.Id, me.Id).Disposition); Assert.True(w.HasFlag(PolitySystem.ReportedFlag));
        }
        // nothing to stir once the war has come, or just after it
        w.WorldCounters.Remove(PolitySystem.StirKey); w.WorldCounters[GreatCrisisSystem.BeganKey] = w.Turn; Assert.Contains("war has come", tc.Polities.StirRefusal(w, mine));
        w.WorldFlags.Add(GreatCrisisSystem.ResolvedFlag); Assert.Contains("had its war", tc.Polities.StirRefusal(w, mine));
    }

    [Fact]
    public void A_generation_on_the_reach_can_come_to_a_second_great_war()
    {
        var (tc, w, me, mine, _) = Start(); Sound(w); var sys = new GreatCrisisSystem(Content.GreatCrisis, tc.Fleets) { Polities = tc.Polities }; var rng = TestContent.Rng();
        Assert.Equal("The War of the Reach", GreatCrisisSystem.WarName(w));
        sys.FormBlocs(w); sys.BlocWar(w); Assert.True(GreatCrisisSystem.AtWar(w)); w.Turn += GreatCrisisSystem.MinWarYears; sys.Resolve(w);
        Assert.True(w.HasFlag(GreatCrisisSystem.ResolvedFlag)); Assert.Equal(1, w.Counter(GreatCrisisSystem.WarsCounter)); Assert.Equal(GreatCrisisSystem.PeaceYears, GreatCrisisSystem.PeaceLeft(w));
        Assert.Contains(w.History, l => l.Text.StartsWith("The War of the Reach ends"));
        // while it is remembered, nothing brings another
        w.WorldCounters["coalition_pressure"] = 40;
        for (int y = 0; y < GreatCrisisSystem.PeaceYears - 1; y++) { w.Turn++; sys.Tick(w, rng); }
        Assert.True(w.HasFlag(GreatCrisisSystem.ResolvedFlag)); Assert.Equal(1, GreatCrisisSystem.PeaceLeft(w)); Assert.False(GreatCrisisSystem.AtWar(w));
        // a generation on it is forgotten, and the Reach starts again from nothing
        w.Turn++; w.WorldCounters.Remove("coalition_pressure"); sys.Tick(w, rng);
        Assert.False(w.HasFlag(GreatCrisisSystem.ResolvedFlag)); Assert.True(w.HasFlag(GreatCrisisSystem.RearmedFlag)); Assert.Equal(0, w.Counter(GreatCrisisSystem.ClockCounter));
        Assert.False(w.WorldCounters.ContainsKey(GreatCrisisSystem.BeganKey)); Assert.DoesNotContain(w.WorldFlags, f => f.StartsWith("great_crisis:")); Assert.Equal(0, sys.Nearness(w));
        Assert.Equal("The Second War of the Reach", GreatCrisisSystem.WarName(w)); Assert.Equal(1, w.Counter(GreatCrisisSystem.WarsCounter));
        mine.Treasury.Credits = 100; w.PoolsFor(me.Id).Influence = 9; Fires(w, "the_war_nobody_remembers");
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w)); Assert.Equal(1, back.Counter(GreatCrisisSystem.WarsCounter)); Assert.False(back.HasFlag(GreatCrisisSystem.ResolvedFlag));
        // and what is wrong with the Reach can bring another
        w.WorldCounters["coalition_pressure"] = 40; bool again = false;
        for (int y = 0; y < 120 && !again; y++) { w.Turn++; sys.Tick(w, rng); again = GreatCrisisSystem.AtWar(w); }
        Assert.True(again, "a hundred and twenty years of pressure and no second war"); Assert.False(w.HasFlag(GreatCrisisSystem.RearmedFlag));
        w.Turn += GreatCrisisSystem.MinWarYears; sys.Resolve(w);
        Assert.Equal(2, w.Counter(GreatCrisisSystem.WarsCounter)); Assert.Contains(w.History, l => l.Text.StartsWith("The Second War of the Reach ends"));
        var past = GreatCrisisSystem.Past(w); Assert.Equal(2, past.Count); Assert.All(past, x => { Assert.True(x.years >= GreatCrisisSystem.MinWarYears); Assert.NotEqual("", x.how); }); Assert.True(past[1].began > past[0].began);
    }

    // ---------------- the older rules, as states (docs/GAPS.md B5) ----------------

    [Fact]
    public void A_rebel_world_that_breaks_from_its_holder_is_a_free_state_outside_the_reach()
    {
        var (tc, w, _, mine, _) = Start(); Sound(w); var old = Other(w, mine);
        string regionId = Give(w, old, 34, Ring.Rim); var world = Worlds(w, regionId).First(x => x.OwnerId == old.Id); var region = w.Galaxy!.Region(regionId)!;
        int founded = w.Counter("polities_founded"), recognised = w.Counter("polities_recognised");
        tc.Regions.Secede(w, world, region);
        var free = w.House(world.OwnerId)!; var p = PolitySystem.Of(w, free.Id)!;
        Assert.Equal(RegionSystem.FreeholdsBloc, free.Bloc); Assert.True(PolitySystem.IsFreeHold(p)); Assert.Empty(p.Regions); Assert.Equal(0, p.Recognised);
        Assert.False(PolitySystem.InReach(w, free.Id)); Assert.Contains(old.Id, p.Foes); Assert.Equal(1, w.Counter("free_states")); Assert.Equal(founded, w.Counter("polities_founded"));
        Assert.Equal(100, PolitySystem.Share(w, p));
        // the Senate does not go back for one world, and one world does not push the Reach toward a great war
        Assert.Contains("one world", tc.Polities.ReconquestRefusal(w, free));
        Assert.DoesNotContain(GreatCrisisSystem.Pressure(w), x => x.what.Contains("left the Reach"));
        // it stands three years, and the Reach names it in a treaty
        for (int y = 0; y < PolitySystem.WinYears; y++) { w.Turn++; tc.Polities.Tick(w); }
        Assert.True(p.Recognised > 0); Assert.Equal(1, w.Counter("free_states_recognised")); Assert.Equal(recognised, w.Counter("polities_recognised"));
        Assert.NotNull(PolitySystem.TreatyBetween(w, old.Id, free.Id));
        // bought back or taken, it is a state no longer, treaty or no treaty
        world.OwnerId = old.Id; w.Turn++; tc.Polities.Tick(w);
        Assert.DoesNotContain(p, w.Polities); Assert.Equal(1, w.Counter("free_states_ended")); Assert.Equal("", free.PolityId);
    }

    [Fact]
    public void A_free_march_takes_its_region_out_as_a_state()
    {
        var (tc, w, _, mine, _) = Start(); Sound(w); var house = Other(w, mine);
        string regionId = Give(w, house, 67, Ring.Rim); foreach (var x in Worlds(w, regionId)) { x.Unrest = 0; x.Rebellion = 0; }
        Assert.Null(tc.Polities.FreeMarchRefusal(w, house, regionId));
        RegionSystem.StateOf(w, regionId).Allegiance = 5;
        tc.Regions.Tick(w);
        var p = PolitySystem.OfRegion(w, regionId);
        Assert.NotNull(p); Assert.Equal(PolitySystem.FreeMarchOrigin, p!.Origin); Assert.Equal(house.Id, p.HeadHouseId); Assert.Contains(p.Form, PolitySystem.Forms);
        Assert.False(PolitySystem.InReach(w, house.Id)); Assert.Equal("", house.Bloc); Assert.Equal(1, w.Counter("free_marches")); Assert.True(w.HasFlag("free_march:" + regionId));
        // the Reach answers it as it answers any declaration: it is let go, or the fleets are sent
        Assert.True(p.Recognised > 0 || p.Foes.Count > 0);
        // and the player's house is not walked out for them
        var (tc2, w2, _, mine2, _) = Start(); Sound(w2);
        string own = Give(w2, mine2, 67, Ring.Rim); foreach (var x in Worlds(w2, own)) { x.Unrest = 0; x.Rebellion = 0; }
        RegionSystem.StateOf(w2, own).Allegiance = 5; tc2.Regions.Tick(w2);
        Assert.Null(PolitySystem.OfRegion(w2, own)); Assert.True(PolitySystem.InReach(w2, mine2.Id));
    }

    // ---------------- a war over what was said (docs/GAPS.md B15) ----------------

    private static void Hulls(World w, House h, int n)
    {
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == h.Id).ToList()) w.Fleets.Remove(f.Id);
        if (n > 0) w.Fleets["test_" + h.Id] = new Fleet { Id = "test_" + h.Id, Name = "Test", OwnerId = h.Id, Strength = n, SystemId = w.Galaxy!.Capital };
    }

    [Fact]
    public void A_house_stirred_against_one_it_already_hates_goes_to_war_with_it()
    {
        var (tc, w, _, mine, _) = Start(); Sound(w); foreach (var war in w.Wars.ToList()) w.Wars.Remove(war);
        var angry = Other(w, mine, 0); var hated = Other(w, mine, 1); mine.Treasury.Credits = 100;
        foreach (var h in w.Houses.Values) Hulls(w, h, 1);
        Hulls(w, angry, 20); Hulls(w, hated, 6);
        Regard(w, angry, hated, PolitySystem.WarOverWordsAt + PolitySystem.StirAgainst - 1);   // one push short of it
        Assert.NotNull(PolitySystem.WarOverWordsRefusal(w, angry, hated));
        Assert.Null(tc.Polities.StirRefusal(w, mine, hated)); tc.Polities.Stir(w, mine, hated);
        Assert.Equal(hated.Id, PolitySystem.StirredAgainst(w, angry.Id)?.Id); Assert.Null(PolitySystem.WarOverWordsRefusal(w, angry, hated));
        tc.Polities.Tick(w);
        Assert.True(w.AtWar(angry.Id, hated.Id)); Assert.Equal(1, w.Counter(PolitySystem.WarOverWordsCounter)); Assert.True(w.HasFlag(PolitySystem.WarOverWordsFlag));
        Assert.Contains(w.History, h => h.Text.Contains("over what has been said"));
        Fires(w, "a_war_over_words");
        // it does not happen twice from one stirring
        Assert.Null(PolitySystem.StirredAgainst(w, angry.Id));
    }

    [Fact]
    public void A_house_does_not_go_to_war_over_words_without_the_hulls_the_hatred_or_against_its_own_kin()
    {
        var (tc, w, _, mine, _) = Start(); Sound(w); foreach (var war in w.Wars.ToList()) w.Wars.Remove(war);
        var angry = Other(w, mine, 0); var hated = Other(w, mine, 1);
        foreach (var h in w.Houses.Values) Hulls(w, h, 1);
        w.WorldCounters[PolitySystem.StirredKey(angry.Id, hated.Id)] = w.Turn + 3;
        // the hatred, and not the hulls
        Hulls(w, angry, 2); Hulls(w, hated, 6); Regard(w, angry, hated, -80);
        Assert.Contains("hulls", PolitySystem.WarOverWordsRefusal(w, angry, hated)); tc.Polities.Tick(w); Assert.False(w.AtWar(angry.Id, hated.Id));
        // the hulls, and not the hatred
        Hulls(w, angry, 20); Regard(w, angry, hated, -40);
        Assert.Contains("ill enough", PolitySystem.WarOverWordsRefusal(w, angry, hated)); tc.Polities.Tick(w); Assert.False(w.AtWar(angry.Id, hated.Id));
        // both, and a marriage between them
        Regard(w, angry, hated, -80);
        var a = Economy.HeadOf(w, angry)!; var b = Economy.HeadOf(w, hated)!; w.Marriages.RemoveAll(m => m.A == a.Id || m.B == a.Id || m.A == b.Id || m.B == b.Id);
        w.Marriages.Add(new Marriage { A = a.Id, B = b.Id });
        Assert.Contains("marriage", PolitySystem.WarOverWordsRefusal(w, angry, hated)); tc.Polities.Tick(w); Assert.False(w.AtWar(angry.Id, hated.Id));
        Assert.Equal(0, w.Counter(PolitySystem.WarOverWordsCounter));
    }
}
