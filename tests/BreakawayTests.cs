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
}
