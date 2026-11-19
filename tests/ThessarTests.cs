using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Galaxy;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The Free Cities of Thessar on the map (docs/BEYOND.md; the owner's word of 2026-11-18: "add the Free Cities of Thessar next"). The
/// second nation with worlds, fleets and persons, and a different kind of neighbour from the Khanates: it would rather buy than fight. Its
/// squadron stays at home unless a warlike Speaker rules; it hires hulls while attacked; it sues at half the tally; it buys its cities back;
/// it pays tribute only while watched; and beating it pays.</summary>
public class ThessarTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private const string Hid = "nation_thessar", Khans = "nation_vashti";

    private static (World w, NationSystem ns, FleetSystem fs, Character me, House mine, Nation n, NationDef d) Start(int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), (ulong)seed); w.BeyondVaries = false;
        var fs = new FleetSystem(Content.Fleets, Content.Holdings, Content.ShipClasses, Content.Friction); var ns = new NationSystem(Content.Nations, fs);
        NationSystem.Seed(w, Content.Nations);
        foreach (var x in w.Nations) { x.Disposition = 50; if (x.Ruler != null) { x.Ruler.Nature = "mild"; x.Ruler.Age = 30; } }   // nobody raids, nobody dies of age, and no Speaker is warlike unless a test says so
        return (w, ns, fs, w.Protagonist!, w.House(w.Protagonist!.HouseId)!, w.Nation("thessar")!, Content.Nations.Def("thessar")!);
    }
    private static Fleet Post(World w, string id, string houseId, string systemId, int hulls)
    { var f = new Fleet { Id = id, Name = "the " + id, OwnerId = houseId, SystemId = systemId, Strength = hulls, Supply = 100, Readiness = 100, Loyalty = 80 }; w.Fleets[id] = f; return f; }
    private static void War(World w, House mine) { NationSystem.DeclareWar(w, Content.Nations, "thessar", mine.Id); Assert.True(w.Nation("thessar")!.AtWar); }
    private static Fleet Squadron(World w) => w.Fleets.Values.Single(f => f.OwnerId == Hid && !f.Id.StartsWith(Hid + "_guard"));
    private static Fleet Guard(World w) => w.Fleets.Values.Single(f => f.OwnerId == Hid && f.Id.StartsWith(Hid + "_guard"));
    private static House Other(World w, House mine) => w.Houses.Values.Where(h => h.Id != mine.Id && w.OfTheReach(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).First();
    private static void Hold(World w, string holdingId, string houseId) { var h = w.Holding(holdingId)!; h.OwnerId = houseId; h.FormerOwnerId = Hid; }
    private static void Year(World w, NationSystem ns) { w.Turn++; ns.Tick(w); }

    [Fact]
    public void TheCities_AreOnTheMap_BesideTheKhanates_AndAreTheirOwnKindOfNeighbour()
    {
        var (w, _, fs, _, mine, n, d) = Start(); var g = w.Galaxy!;
        Assert.True(n.OnMap); Assert.Equal(Hid, n.HouseId); Assert.Equal("thessar", n.CapitalSystemId); Assert.True(w.Nation("vashti")!.OnMap);
        Assert.Equal(3, w.HoldingsOf(Hid).Count()); Assert.All(w.HoldingsOf(Hid), h => Assert.Equal("thessaran_shore", g.System(h.SystemId)!.RegionId));
        foreach (var m in NationSystem.Marches(w, d)) Assert.Equal(1, g.Hops(m, "port_meront"));            // the nearest city is one lane beyond Wintermoor and Brackwater
        Assert.True(World.Outsider(Hid)); Assert.False(w.OfTheReach(Hid)); Assert.False(w.AtWar(Hid, Khans));  // not of the Reach; and nations do not fight each other
        Assert.Equal("The Thessaran Squadron", Squadron(w).Name); Assert.Equal("The Harbour Guard of Thessar", Guard(w).Name);
        Assert.Equal(d.Strength, Squadron(w).Strength + Guard(w).Strength); Assert.Equal("port_meront", Squadron(w).SystemId); Assert.Equal("thessar", Guard(w).SystemId);
        // what it is: it buys, it sues at half the tally, its cities hold longer than the Khanates' camps, and hull for hull it fights at four-fifths
        Assert.True(NationSystem.Buys(d)); Assert.False(NationSystem.Buys(Content.Nations.Def("vashti")!));
        Assert.Equal((d.Threshold + 1) / 2, NationSystem.SuesAt(n, d)); Assert.Equal(Content.Nations.Def("vashti")!.Threshold, NationSystem.SuesAt(w.Nation("vashti")!, Content.Nations.Def("vashti")!));
        Assert.Equal(6, d.HoldYears); Assert.Equal(3, Content.Nations.Def("vashti")!.HoldYears);
        Assert.Equal(80, n.Mettle); Assert.Equal(100, w.Nation("vashti")!.Mettle);
        double asItIs = fs.Power(w, Hid, "port_meront", defending: false); n.Mettle = 100; Assert.Equal(0.8, asItIs / fs.Power(w, Hid, "port_meront", defending: false), 3);
        _ = mine;
    }

    [Fact]
    public void TheirSquadron_StaysAtHome_UnlessAWarlikeSpeakerRules()
    {
        var (w, _, _, _, mine, n, d) = Start(); War(w, mine); var sq = Squadron(w);
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId != Hid).ToList()) w.Fleets.Remove(f.Id);   // the Reach's worlds lie open
        Assert.False(NationSystem.ComesOut(n, d)); Assert.Null(NationSystem.HostOrder(w, n, d, sq));
        n.Ruler!.Nature = "warlike"; Assert.True(NationSystem.ComesOut(n, d));
        var to = NationSystem.HostOrder(w, n, d, sq); Assert.NotNull(to); Assert.NotEqual(Ring.Beyond, w.Galaxy!.RingOf(to!));
        // at home or not, it goes for a city of its own that the Reach holds and has left open
        n.Ruler.Nature = "mild"; Hold(w, "port_meront_city", mine.Id); sq.SystemId = NationSystem.Border(w, n, d); Assert.NotEqual("port_meront", sq.SystemId);
        Assert.Equal("port_meront", NationSystem.HostOrder(w, n, d, sq));
        // and the Khanates' host comes out whoever rules
        Assert.True(NationSystem.ComesOut(w.Nation("vashti")!, Content.Nations.Def("vashti")!));
    }

    [Fact]
    public void Attacked_TheyHire_AHullAYear_WhileTheWarLasts()
    {
        var (w, ns, _, _, mine, n, d) = Start(); var sq = Squadron(w); sq.Strength -= 3; sq.Ships.Clear(); sq.Ships["frigate"] = sq.Strength; sq.Recount(); int low = d.Strength - 3;
        w.Turn = 10; Year(w, ns); Year(w, ns);                                                               // years 11 and 12, at peace: nothing grows back between the fifth years
        Assert.Equal(low, n.Strength); Assert.Equal(0, w.Counter(NationSystem.HiredCounter));
        War(w, mine); foreach (var f in w.Fleets.Values.Where(f => f.OwnerId != Hid).ToList()) w.Fleets.Remove(f.Id);
        Year(w, ns); Assert.Equal(low + 1, n.Strength); Assert.True(w.HasFlag("nation_hires")); Assert.True(w.HasFlag("nation_hires:thessar"));
        Year(w, ns); Assert.Equal(low + 2, n.Strength); Assert.Equal(2, w.Counter(NationSystem.HiredCounter)); Assert.Equal(2, w.Counter(NationSystem.HiredCounter + ":thessar"));
        Assert.False(w.HasFlag("nation_hires"));                                                              // said once a war
        Year(w, ns); Year(w, ns); Assert.Equal(d.Strength, n.Strength);                                       // and no further than its full strength
    }

    [Fact]
    public void TheySueAtHalfTheTally_AndBeatingThemPays()
    {
        var (w, ns, _, _, mine, n, d) = Start(); War(w, mine); int before = mine.Treasury.Credits;
        NationSystem.BattleOnMap(w, mine.Id, Hid);
        Assert.Equal(before + d.Wealth, mine.Treasury.Credits); Assert.Equal(NationSystem.ScorePerBattle, n.ScoreReach); Assert.Equal(d.Wealth, w.Counter(NationSystem.PlunderCounter + ":thessar"));
        var city = w.Holding("port_meront_city")!; city.OwnerId = mine.Id; city.FormerOwnerId = Hid; NationSystem.WorldTaken(w, city, mine.Id, Hid, null);
        Assert.Equal(before + d.Wealth + NationSystem.PlunderPerWorld * d.Wealth, mine.Treasury.Credits); Assert.True(w.HasFlag("world_plundered_by_us:thessar"));
        Assert.Equal(NationSystem.ScorePerBattle + NationSystem.ScorePerWorld, n.ScoreReach); Assert.True(n.ScoreReach < d.Threshold);   // five: short of the usual six
        Post(w, "garrison", mine.Id, "port_meront", 3);
        Year(w, ns);
        Assert.False(n.AtWar); Assert.True(n.Subdued(w.Turn)); Assert.Equal(1, w.Counter(NationSystem.WonCounter + ":thessar"));
    }

    [Fact]
    public void AtAWonPeace_TheyBuyTheirCitiesBack_AndThePlayerIsAsked()
    {
        var (w, ns, _, _, mine, n, d) = Start(); War(w, mine); var other = Other(w, mine); int sum = NationSystem.RansomPerWealth * d.Wealth;
        Hold(w, "port_meront_city", other.Id); Hold(w, "pellas_city", mine.Id); Post(w, "g1", other.Id, "port_meront", 3); Post(w, "g2", mine.Id, "pellas", 3);
        int theirs = other.Treasury.Credits, ours = mine.Treasury.Credits; n.ScoreReach = NationSystem.SuesAt(n, d);
        Year(w, ns);
        Assert.False(n.AtWar);
        Assert.Equal(Hid, w.Holding("port_meront_city")!.OwnerId); Assert.Equal(theirs + sum, other.Treasury.Credits); Assert.Equal(1, w.Counter(NationSystem.RansomedCounter));   // another house: paid, and the city goes home
        Assert.Equal(mine.Id, w.Holding("pellas_city")!.OwnerId); Assert.True(w.Counter(NationSystem.RansomKey("pellas_city")) > 0); Assert.True(w.HasFlag("ransom_offered:thessar"));   // the player: asked
        // the offer stands a second year; then the answer
        Year(w, ns); Assert.True(w.HasFlag("ransom_offered:thessar")); Assert.Equal(mine.Id, w.Holding("pellas_city")!.OwnerId);
        w.WorldFlags.Add("ransom_answer_take"); int at = mine.Treasury.Credits; Year(w, ns);
        Assert.Equal(Hid, w.Holding("pellas_city")!.OwnerId); Assert.True(mine.Treasury.Credits >= at + sum); Assert.Equal(0, w.Counter(NationSystem.RansomKey("pellas_city"))); Assert.False(w.HasFlag("ransom_answer_take"));
        _ = ours;

        // refused: the city is kept, and they think the worse of the Reach
        var (w2, ns2, _, _, mine2, n2, d2) = Start(); War(w2, mine2); Hold(w2, "pellas_city", mine2.Id); Post(w2, "g2", mine2.Id, "pellas", 3); n2.ScoreReach = NationSystem.SuesAt(n2, d2);
        Year(w2, ns2); int regard = n2.Disposition; w2.WorldFlags.Add("ransom_answer_keep"); Year(w2, ns2);
        Assert.Equal(mine2.Id, w2.Holding("pellas_city")!.OwnerId); Assert.Equal(0, w2.Counter(NationSystem.RansomKey("pellas_city"))); Assert.True(n2.Disposition <= regard - 10);
        // unanswered: it lapses after two years, and the city is kept
        var (w3, ns3, _, _, mine3, n3, d3) = Start(); War(w3, mine3); Hold(w3, "pellas_city", mine3.Id); Post(w3, "g2", mine3.Id, "pellas", 3); n3.ScoreReach = NationSystem.SuesAt(n3, d3);
        Year(w3, ns3); Year(w3, ns3); Year(w3, ns3);
        Assert.Equal(mine3.Id, w3.Holding("pellas_city")!.OwnerId); Assert.Equal(0, w3.Counter(NationSystem.RansomKey("pellas_city")));
        // the Khanates do not buy anything back
        var (w4, ns4, _, _, mine4, _, _) = Start(); var khans = w4.Nation("vashti")!; NationSystem.DeclareWar(w4, Content.Nations, "vashti", mine4.Id);
        var camp = w4.Holding("long_grass_camp")!; camp.OwnerId = Other(w4, mine4).Id; camp.FormerOwnerId = Khans; Post(w4, "g", camp.OwnerId, "the_long_grass", 3); khans.ScoreReach = 6;
        Year(w4, ns4); Assert.False(khans.AtWar); Assert.NotEqual(Khans, camp.OwnerId); Assert.Equal(0, w4.Counter(NationSystem.RansomedCounter));
    }

    [Fact]
    public void TheRansom_IsAskedByAReport_AndTheAnswerIsCarriedOut_InARealGame()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 7); w.BeyondVaries = false; var tc = new TurnController(w, Content);
        var mine = w.House(w.Protagonist!.HouseId)!; var n = w.Nation("thessar")!; var d = Content.Nations.Def("thessar")!; int sum = NationSystem.RansomPerWealth * d.Wealth;
        NationSystem.DeclareWar(w, Content.Nations, "thessar", mine.Id); Hold(w, "pellas_city", mine.Id); Post(w, "g", mine.Id, "pellas", 4); n.ScoreReach = NationSystem.SuesAt(n, d);
        bool asked = false; int before = 0;
        for (int t = 0; t < 4 && w.Holding("pellas_city")!.OwnerId == mine.Id; t++)
        {
            foreach (var i in tc.BeginTurn())
            {
                if (i.Fired.Def.Id == "the_cities_offer_a_ransom") { asked = true; before = mine.Treasury.Credits; tc.Resolve(i, "take_the_money"); continue; }
                var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id);
            }
            tc.EndTurn();
        }
        Assert.True(asked, "the report was never offered while the offer stood");
        Assert.Equal(Hid, w.Holding("pellas_city")!.OwnerId); Assert.True(mine.Treasury.Credits >= before + sum - 30, $"{before} → {mine.Treasury.Credits}");   // paid (less a year's ordinary spending)
        Assert.Equal(1, w.Counter(NationSystem.RansomedCounter + ":thessar")); Assert.False(n.AtWar); Assert.True(n.Subdued(w.Turn));
    }

    [Fact]
    public void AWarNobodyFights_TheyBuyOff_AfterFourYears()
    {
        var (w, ns, _, _, mine, n, d) = Start(); War(w, mine); var chair = RegionSystem.CapitalHolder(w)!; int before = chair.Treasury.Credits;
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId != Hid && f.OwnerId != Khans).ToList()) w.Fleets.Remove(f.Id);   // nobody goes
        for (int y = 1; y < NationSystem.BuysPeaceAfter; y++) { Year(w, ns); Assert.True(n.AtWar); }
        Year(w, ns);
        Assert.False(n.AtWar); Assert.False(n.Subdued(w.Turn)); Assert.Equal(before + d.Tribute * NationSystem.BoughtPeaceTributes, chair.Treasury.Credits);
        Assert.Equal(1, w.Counter(NationSystem.BoughtPeaceCounter + ":thessar")); Assert.True(w.HasFlag("nation_bought_peace:thessar")); Assert.Equal(0, w.Counter(NationSystem.GutteredCounter));
        // pressed, the war goes on: a squadron of the Reach at their march, and they do not buy it off (until twelve years with no battle)
        var (w2, ns2, _, _, mine2, n2, _) = Start(); War(w2, mine2); foreach (var f in w2.Fleets.Values.Where(f => f.OwnerId != Hid && f.OwnerId != Khans).ToList()) w2.Fleets.Remove(f.Id);
        Post(w2, "watch", mine2.Id, "wintermoor", NationSystem.MarchGuard);
        for (int y = 0; y < NationSystem.ExhaustionTurns - 1; y++) { Year(w2, ns2); Assert.True(n2.AtWar, $"bought off in year {y + 1} with a fleet at the march"); }
        Year(w2, ns2); Assert.False(n2.AtWar); Assert.Equal(1, w2.Counter(NationSystem.BoughtPeaceCounter)); Assert.Equal(0, w2.Counter(NationSystem.GutteredCounter));
        // the Khanates let a war lie for twelve
        var khans = w.Nation("vashti")!; NationSystem.DeclareWar(w, Content.Nations, "vashti", mine.Id); foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == Khans).ToList()) f.Readiness = 0;
        for (int y = 0; y < NationSystem.BuysPeaceAfter + 2; y++) { foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == Khans)) f.Readiness = 0; Year(w, ns); }
        Assert.True(khans.AtWar);
    }

    [Fact]
    public void TheyPayTribute_OnlyWhileAFleetOfTheReachIsWatching()
    {
        var (w, ns, _, _, mine, n, d) = Start(); n.SubduedUntil = w.Turn + 20;
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId != Hid && f.OwnerId != Khans).ToList()) w.Fleets.Remove(f.Id);
        Assert.False(NationSystem.Watched(w, d));
        for (int y = 1; y < NationSystem.UnwatchedYears; y++) { Year(w, ns); Assert.True(n.Subdued(w.Turn)); }
        Year(w, ns);
        Assert.False(n.Subdued(w.Turn)); Assert.Equal(1, w.Counter(NationSystem.TributeStoppedCounter)); Assert.True(w.HasFlag("tribute_stopped:thessar"));
        // a squadron at the march, or over one of their cities, and it goes on
        n.SubduedUntil = w.Turn + 20; Post(w, "watch", mine.Id, "wintermoor", NationSystem.MarchGuard); Assert.True(NationSystem.Watched(w, d));
        for (int y = 0; y < 6; y++) Year(w, ns);
        Assert.True(n.Subdued(w.Turn)); Assert.Equal(1, w.Counter(NationSystem.TributeStoppedCounter));
        // the Khanates pay whether watched or not
        var khans = w.Nation("vashti")!; khans.SubduedUntil = w.Turn + 20; for (int y = 0; y < 6; y++) Year(w, ns); Assert.True(khans.Subdued(w.Turn));
    }

    [Fact]
    public void ACityHeldWithoutAFleet_ShutsItsGates_AfterSixYears()
    {
        var (w, ns, _, _, mine, n, d) = Start(); Hold(w, "port_meront_city", mine.Id);
        foreach (var f in w.Fleets.Values.Where(f => f.SystemId == "port_meront" && f.OwnerId != Hid).ToList()) w.Fleets.Remove(f.Id);
        for (int y = 1; y < d.HoldYears; y++) { Year(w, ns); Assert.Equal(mine.Id, w.Holding("port_meront_city")!.OwnerId); Assert.True(w.HasFlag("steppe_world_restless:thessar")); Assert.False(w.HasFlag("steppe_world_restless:vashti")); }
        Year(w, ns);
        Assert.Equal(Hid, w.Holding("port_meront_city")!.OwnerId); Assert.Equal(1, w.Counter(NationSystem.RisenCounter + ":thessar")); Assert.True(w.HasFlag("steppe_world_lost:thessar"));
        Year(w, ns); Assert.False(w.HasFlag("steppe_world_lost:thessar")); Assert.False(w.HasFlag("steppe_world_lost"));   // a year's news, then gone
        _ = n;
    }

    [Fact]
    public void AHouseShortOfMoney_ThatWantsAWar_NamesTheCities_WhenNobodyHasGivenABetterReason()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 7); w.BeyondVaries = false; var tc = new TurnController(w, Content);
        var mine = w.House(w.Protagonist!.HouseId)!; var h = Other(w, mine); var head = Economy.HeadOf(w, h)!; head.CareerTrack = "military"; head.CareerRank = 3;
        foreach (var x in w.Wars.ToList()) w.Wars.Remove(x);
        foreach (var x in w.Nations) x.Disposition = 20;                                                      // nobody hostile, nobody raiding
        w.Nation("hollow_crown")!.Disposition = 40;                                                           // (the other rich neighbour, better liked: named second)
        h.Treasury.Credits = SenateSystem.PlunderWant + 50; Assert.Null(tc.Senate.WantedWar(w, h));            // a full purse wants no war with a friend
        h.Treasury.Credits = SenateSystem.PlunderWant - 10; Assert.Equal("thessar", tc.Senate.WantedWar(w, h));
        int poor = tc.Senate.ForeignWarLean(w, h, "thessar"); h.Treasury.Credits = SenateSystem.PlunderWant + 50; Assert.Equal(poor - 1, tc.Senate.ForeignWarLean(w, h, "thessar"));
        // a nation with a real quarrel is named first
        h.Treasury.Credits = SenateSystem.PlunderWant - 10; w.Nation("vashti")!.Disposition = -50; Assert.Equal("vashti", tc.Senate.WantedWar(w, h));
        // and not while the Cities pay tribute
        w.Nation("vashti")!.Disposition = 20; w.Nation("thessar")!.SubduedUntil = w.Turn + 20; Assert.Equal("hollow_crown", tc.Senate.WantedWar(w, h));   // then the other rich neighbour
        w.Nation("hollow_crown")!.SubduedUntil = w.Turn + 20; Assert.Null(tc.Senate.WantedWar(w, h));
    }

    [Fact]
    public void TheReachsOwnLife_GoesOnWithoutThem_AndAnOlderGameIsBroughtForward()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 13); w.BeyondVaries = false;
        // an older game: the Khanates on the map already, the Cities not yet
        NationSystem.Seed(w, Content.Nations); var n = w.Nation("thessar")!;
        foreach (var h in w.HoldingsOf(Hid).ToList()) w.Holdings.Remove(h.Id); foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == Hid).ToList()) w.Fleets.Remove(f.Id);
        foreach (var c in w.Characters.Values.Where(c => c.HouseId == Hid).ToList()) w.Characters.Remove(c.Id); w.Houses.Remove(Hid); n.HouseId = ""; n.CapitalSystemId = ""; n.Ruler!.CharacterId = ""; n.Commander!.CharacterId = "";
        w.Turn = 5; Assert.False(n.OnMap);
        var tc = new TurnController(w, Content);
        for (int t = 0; t < 20 && !w.IsDynastyDead; t++) { foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); } tc.EndTurn(); }
        Assert.True(n.OnMap); Assert.Equal(3, w.HoldingsOf(Hid).Count(h => h.IsPlanet));                    // placed at the first year it is played
        Assert.Null(FactionSystem.Of(w, Hid)); Assert.DoesNotContain(w.Magistrates, m => m.HouseId == Hid); Assert.DoesNotContain(w.Wars, x => x.Involves(Hid));
        Assert.False(w.Regions.ContainsKey("thessaran_shore")); Assert.All(w.Foreigners(), f => Assert.Null(DynastySystem.Spouse(w, f)));
        Assert.Equal(n.Strength, w.Fleets.Values.Where(f => f.OwnerId == Hid).Sum(f => f.Strength));
    }
}
