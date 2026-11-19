using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Galaxy;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The last two nations on the map (docs/BEYOND.md; the owner's word of 2026-11-19: "add the Outer Compact and the Hollow Crown
/// next"). The Compact is far, fights only when invaded and then well, and sits on isotopes. The Hollow Crown is an empire in decay: it
/// strikes hulls from its lists, cannot come back for what it loses, can be taken apart and ended, and has a treasure in its capital.</summary>
public class OuterNationsTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private const string Compact = "nation_compact", Crown = "nation_hollow_crown";

    private static (World w, NationSystem ns, FleetSystem fs, Character me, House mine) Start(int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), (ulong)seed);
        var fs = new FleetSystem(Content.Fleets, Content.Holdings, Content.ShipClasses, Content.Friction); var ns = new NationSystem(Content.Nations, fs);
        NationSystem.Seed(w, Content.Nations);
        foreach (var x in w.Nations) { x.Disposition = 50; if (x.Ruler != null) { x.Ruler.Nature = "mild"; x.Ruler.Age = 30; } }   // nobody raids, nobody strikes, nobody dies of age while a test looks at something else
        return (w, ns, fs, w.Protagonist!, w.House(w.Protagonist!.HouseId)!);
    }
    private static Fleet Post(World w, string id, string houseId, string systemId, int hulls)
    { var f = new Fleet { Id = id, Name = "the " + id, OwnerId = houseId, SystemId = systemId, Strength = hulls, Supply = 100, Readiness = 100, Loyalty = 80 }; w.Fleets[id] = f; return f; }
    private static void Year(World w, NationSystem ns) { w.Turn++; ns.Tick(w); }
    private static void Hold(World w, string holdingId, string houseId, string nationHouse) { var h = w.Holding(holdingId)!; h.OwnerId = houseId; h.FormerOwnerId = nationHouse; }
    private static int Hulls(World w, string house) => w.Fleets.Values.Where(f => f.OwnerId == house).Sum(f => f.Strength);

    [Fact]
    public void AllFiveNations_AreOnTheMap_EachInItsOwnSpaceBeyondTheRim()
    {
        var (w, _, _, _, _) = Start(); var g = w.Galaxy!;
        Assert.All(w.Nations, n => Assert.True(n.OnMap, n.Id)); Assert.Equal(5, w.Nations.Count);
        Assert.Equal(5, g.Regions.Count(r => r.Ring == Ring.Beyond)); Assert.Equal(10, g.ReachRegions.Count());
        foreach (var n in w.Nations)
        {
            var d = Content.Nations.Def(n.Id)!; var region = g.System(n.CapitalSystemId)!.RegionId;
            Assert.Equal(Ring.Beyond, g.RingOf(n.CapitalSystemId)); Assert.All(d.Worlds, x => Assert.Equal(region, g.System(x.System)!.RegionId));   // one region each
            Assert.Equal(d.Strength, Hulls(w, n.HouseId)); Assert.True(World.Outsider(n.HouseId));
            Assert.All(NationSystem.Marches(w, d), m => Assert.NotEqual(Ring.Beyond, g.RingOf(m)));                                              // its door is a system of the Reach
        }
        Assert.Equal(5, w.Nations.Select(n => g.System(n.CapitalSystemId)!.RegionId).Distinct().Count());
        foreach (var a in w.Nations) foreach (var b in w.Nations) Assert.False(w.AtWar(a.HouseId, b.HouseId));                                    // and nations do not fight each other
    }

    // ---------------- the Outer Compact ----------------

    [Fact]
    public void TheCompact_IsFar_FightsOnlyAtHome_AndThenWell()
    {
        var (w, _, fs, _, mine) = Start(); var g = w.Galaxy!; var n = w.Nation("compact")!; var d = Content.Nations.Def("compact")!;
        Assert.Equal("the_wardenry", n.CapitalSystemId); Assert.Equal(4, w.HoldingsOf(Compact).Count());
        // far: past Sorrow and an empty system nobody holds
        Assert.Equal(new[] { "sorrow" }, NationSystem.Marches(w, d)); Assert.Null(w.PlanetHolder("the_long_dark")); Assert.Equal("outer_compact", g.System("the_long_dark")!.RegionId);
        Assert.All(d.Worlds, x => Assert.True(g.Hops("sorrow", x.System) >= 2)); Assert.Equal(1, g.Hops("sorrow", "the_long_dark")); Assert.Equal(2, g.Hops("sorrow", "pikes_landing"));
        // only at home: whoever rules
        Assert.Equal("never", d.Sorties); Assert.False(NationSystem.ComesOut(n, d)); n.Ruler!.Nature = "warlike"; Assert.False(NationSystem.ComesOut(n, d));
        NationSystem.DeclareWar(w, Content.Nations, "compact", mine.Id); foreach (var f in w.Fleets.Values.Where(f => !World.Outsider(f.OwnerId)).ToList()) w.Fleets.Remove(f.Id);
        var militia = w.Fleets.Values.Single(f => f.OwnerId == Compact && !f.Id.StartsWith(Compact + "_guard")); Assert.Equal("The Compact Militia", militia.Name); Assert.Null(NationSystem.HostOrder(w, n, d, militia));
        // invaded, everything it has turns out: the guard leaves the capital to meet the intruder beside the militia, and goes home after
        var guard = w.Fleets.Values.Single(f => f.Id.StartsWith(Compact + "_guard")); var raider = Post(w, "raid", mine.Id, "wrenn", 6);
        Assert.Equal("wrenn", NationSystem.HostOrder(w, n, d, guard, null, n.CapitalSystemId)); Assert.Equal("wrenn", NationSystem.HostOrder(w, n, d, militia));
        raider.Strength = 20; Assert.Null(NationSystem.HostOrder(w, n, d, guard, null, n.CapitalSystemId));   // but not against what both together could not beat
        w.Fleets.Remove("raid"); guard.SystemId = "wrenn"; Assert.Equal("the_wardenry", NationSystem.HostOrder(w, n, d, guard, null, n.CapitalSystemId)); guard.SystemId = "the_wardenry";
        // and then well: five hulls fight like six and a quarter, and its worlds take two years longer to reduce
        Assert.Equal(125, n.Mettle); double asItIs = fs.Power(w, Compact, militia.SystemId, defending: false); n.Mettle = 100; Assert.Equal(1.25, asItIs / fs.Power(w, Compact, militia.SystemId, defending: false), 3);
        Assert.Equal(2, n.Walls); var town = w.Holding("pikes_landing_town")!; foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == Compact).ToList()) f.SystemId = "the_wardenry";
        Post(w, "siege", mine.Id, "pikes_landing", 6); int years = 0; while (town.OwnerId == Compact && years < 15) { fs.ResolveConquests(w); years++; }
        Assert.Equal(mine.Id, town.OwnerId); Assert.Equal(fs.SiegeRequired(w, town) + d.Walls, years);
        // a world of the Reach's is not walled by it
        var veil = w.Holdings.Values.First(h => h.IsPlanet && h.SystemId == "the_veil"); Assert.Equal(0, w.NationOfHouse(veil.OwnerId)?.Walls ?? 0);
    }

    [Fact]
    public void AWorldOfTheCompact_PaysItsHolderIsotopes_ForAsLongAsItIsHeld()
    {
        var (w, ns, _, _, mine) = Start(); var n = w.Nation("compact")!; var d = Content.Nations.Def("compact")!; Assert.Equal(2, d.Yields["isotopes"]); Assert.Equal(4, d.HoldYears);
        Hold(w, "mallow_fields", mine.Id, Compact); Post(w, "garrison", mine.Id, "mallow", 3); int before = mine.Treasury.Isotopes;
        Year(w, ns); Assert.Equal(before + 2, mine.Treasury.Isotopes); Assert.True(w.HasFlag("world_yields_to_us:compact"));
        Year(w, ns); Assert.Equal(before + 4, mine.Treasury.Isotopes); Assert.False(w.HasFlag("world_yields_to_us:compact"));   // said once
        Assert.Equal(4, w.Counter(NationSystem.YieldCounter + ":compact"));
        // let go, it stops; the Compact's own worlds pay the Compact nothing the Reach can see
        w.Fleets.Remove("garrison"); for (int y = 0; y < d.HoldYears; y++) Year(w, ns);
        Assert.Equal(Compact, w.Holding("mallow_fields")!.OwnerId); int held = mine.Treasury.Isotopes; Year(w, ns); Assert.Equal(held, mine.Treasury.Isotopes);
        _ = n;
    }

    // ---------------- the Hollow Crown ----------------

    [Fact]
    public void TheCrown_IsAnEmpireInDecay_ItStrikesHullsFromItsLists_AndReplacesNone()
    {
        var (w, ns, _, _, _) = Start(); var g = w.Galaxy!; var n = w.Nation("hollow_crown")!; var d = Content.Nations.Def("hollow_crown")!;
        Assert.Equal("porphyra", n.CapitalSystemId); Assert.Equal(4, w.HoldingsOf(Crown).Count()); foreach (var m in NationSystem.Marches(w, d)) Assert.Equal(1, g.Hops(m, "chalke"));
        Assert.Equal(70, n.Mettle); Assert.Equal(d.Treasure, n.Treasure); Assert.True(n.Treasure > 0); Assert.Equal(0, d.HoldYears); Assert.False(d.Strikes); Assert.Equal("decaying", d.Temper);
        Assert.Equal("The Tagmata", w.Fleets.Values.Single(f => f.OwnerId == Crown && !f.Id.StartsWith(Crown + "_guard")).Name);
        int at = Hulls(w, Crown); Assert.Equal(d.Strength, at);
        w.Turn = 8; Year(w, ns); Assert.Equal(at, Hulls(w, Crown)); Year(w, ns);                                // year 10
        Assert.Equal(at - 1, Hulls(w, Crown)); Assert.Equal(at - 1, n.Strength); Assert.True(w.HasFlag("nation_decays:hollow_crown")); Assert.Equal(1, w.Counter(NationSystem.DecayedCounter + ":hollow_crown"));
        for (int y = 0; y < 120; y++) Year(w, ns);
        Assert.Equal(NationSystem.DecayFloor, Hulls(w, Crown));                                                 // down to a remnant, and no further; nothing grows back
        // the others do not decay
        Assert.Equal(Content.Nations.Def("concord")!.Strength, Hulls(w, "nation_concord"));
    }

    [Fact]
    public void WhoeverTakesItsCapitalFirst_ComesHomeVeryRich_AndTheWarIsOver()
    {
        var (w, ns, _, _, mine) = Start(); var n = w.Nation("hollow_crown")!; var d = Content.Nations.Def("hollow_crown")!; NationSystem.DeclareWar(w, Content.Nations, "hollow_crown", mine.Id);
        var palace = w.Holding("porphyra_palace")!; int credits = mine.Treasury.Credits, standing = mine.Legitimacy = 40;
        palace.OwnerId = mine.Id; palace.FormerOwnerId = Crown; NationSystem.WorldTaken(w, palace, mine.Id, Crown, null);
        Assert.Equal(credits + d.Treasure + NationSystem.PlunderPerWorld * d.Wealth, mine.Treasury.Credits); Assert.Equal(standing + d.Titles, mine.Legitimacy);
        Assert.Equal(0, n.Treasure); Assert.True(w.HasFlag("treasure_taken_by_us:hollow_crown")); Assert.False(w.HasFlag("treasure_taken")); Assert.Equal(d.Treasure, w.Counter(NationSystem.TreasureCounter + ":hollow_crown"));
        // and the thing in the vaults is a thing: the head of the house has it, and it mends the house's name while the head keeps it
        var diadem = Assert.Single(w.Heirlooms, h => h.Kind == WealthSystem.Regalia); var head = Economy.HeadOf(w, mine)!;
        Assert.Equal(d.Regalia, diadem.Name); Assert.Equal(head.Id, diadem.HolderId); Assert.Equal("", n.Regalia); Assert.Contains("mends", WealthSystem.Does(w, diadem));
        Post(w, "garrison", mine.Id, "porphyra", 3); foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == Crown)) f.SystemId = "chrysopolis";
        Year(w, ns);
        Assert.False(n.AtWar); Assert.True(n.Subdued(w.Turn)); Assert.NotEqual("porphyra", n.CapitalSystemId);  // the Autokrator sits somewhere else
        // the vaults are opened once: a second taking of wherever the court now sits pays the plunder and no more
        var other = w.Houses.Values.Where(h => h.Id != mine.Id && w.OfTheReach(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).First(); int theirs = other.Treasury.Credits;
        var seat = w.Holdings.Values.First(h => h.IsPlanet && h.SystemId == n.CapitalSystemId); seat.OwnerId = other.Id; seat.FormerOwnerId = Crown; NationSystem.WorldTaken(w, seat, other.Id, Crown, null);
        Assert.Equal(theirs + NationSystem.PlunderPerWorld * d.Wealth, other.Treasury.Credits);
    }

    [Fact]
    public void ItDoesNotComeBackForWhatItLoses_AndWithNoWorldLeftItIsNoMore()
    {
        var (w, ns, _, _, mine) = Start(); var n = w.Nation("hollow_crown")!;
        Hold(w, "chalke_gate", mine.Id, Crown); foreach (var f in w.Fleets.Values.Where(f => f.SystemId == "chalke" && f.OwnerId != Crown).ToList()) w.Fleets.Remove(f.Id);
        for (int y = 0; y < 30; y++) { Year(w, ns); Assert.False(n.AtWar); }                                    // it makes no war for it
        Assert.Equal(mine.Id, w.Holding("chalke_gate")!.OwnerId); Assert.Equal(0, w.Counter(NationSystem.RisenCounter)); Assert.Equal(0, w.Counter(NationSystem.StruckCounter));   // and the Gate does not rise
        foreach (var h in w.HoldingsOf(Crown).ToList()) { h.OwnerId = mine.Id; h.FormerOwnerId = Crown; }
        Year(w, ns);
        Assert.True(n.Fallen); Assert.Empty(w.Fleets.Values.Where(f => f.OwnerId == Crown)); Assert.True(w.HasFlag("nation_fallen:hollow_crown")); Assert.Equal(1, w.Counter(NationSystem.FallenCounter + ":hollow_crown"));
        Assert.Equal(4, w.Nations.Count(x => x.OnMap && !x.Fallen));                                            // the other four go on
        NationSystem.DeclareWar(w, Content.Nations, "hollow_crown", mine.Id); Assert.False(n.AtWar);
    }

    [Fact]
    public void TheReachsOwnLife_GoesOnWithoutAnyOfTheFive()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 23); var tc = new TurnController(w, Content);
        for (int t = 0; t < 25 && !w.IsDynastyDead; t++) { foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); } tc.EndTurn(); }
        foreach (var n in w.Nations)
        {
            Assert.Null(FactionSystem.Of(w, n.HouseId)); Assert.DoesNotContain(w.Magistrates, m => m.HouseId == n.HouseId); Assert.DoesNotContain(w.Offices, o => o.HouseId == n.HouseId);
            Assert.DoesNotContain(w.Wars, x => x.Involves(n.HouseId)); Assert.False(w.Regions.ContainsKey(w.Galaxy!.System(Content.Nations.Def(n.Id)!.Worlds[0].System)!.RegionId));
            if (!n.Fallen) Assert.Equal(n.Strength, Hulls(w, n.HouseId));
        }
        Assert.All(w.Foreigners(), f => Assert.Null(DynastySystem.Spouse(w, f)));
        Assert.DoesNotContain(w.Fleets.Values, f => f.OwnerId == PirateSystem.HouseId && w.Galaxy!.RingOf(f.SystemId) == Ring.Beyond && f.SystemId != "the_long_dark" && w.PlanetHolder(f.SystemId) == null);
    }
}
