using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Galaxy;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The Ardent Concord on the map (docs/BEYOND.md; the owner's word of 2026-11-18: "add the Ardent Concord next"). The third nation
/// with worlds, fleets and persons, and the strong one: a unified state that begins wars itself (when the Reach is divided, or to take
/// back a world of its own), sends two fleets through its two marches, fights a campaign and not a raid, and does not buy or let go.</summary>
public class ConcordTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private const string Hid = "nation_concord";

    private static (World w, NationSystem ns, FleetSystem fs, Character me, House mine, Nation n, NationDef d) Start(int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), (ulong)seed);
        var fs = new FleetSystem(Content.Fleets, Content.Holdings, Content.ShipClasses, Content.Friction); var ns = new NationSystem(Content.Nations, fs);
        NationSystem.Seed(w, Content.Nations);
        foreach (var x in w.Nations) { x.Disposition = 50; if (x.Ruler != null) { x.Ruler.Nature = "mild"; x.Ruler.Age = 30; } }   // nobody raids or dies of age while a test looks at something else
        w.Nation("concord")!.Disposition = -30;                                                                 // and the Concord has no love for the Reach, as it has not
        return (w, ns, fs, w.Protagonist!, w.House(w.Protagonist!.HouseId)!, w.Nation("concord")!, Content.Nations.Def("concord")!);
    }
    private static Fleet Post(World w, string id, string houseId, string systemId, int hulls)
    { var f = new Fleet { Id = id, Name = "the " + id, OwnerId = houseId, SystemId = systemId, Strength = hulls, Supply = 100, Readiness = 100, Loyalty = 80 }; w.Fleets[id] = f; return f; }
    private static List<Fleet> Divisions(World w) => w.Fleets.Values.Where(f => f.OwnerId == Hid && !f.Id.StartsWith(Hid + "_guard")).OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).ToList();
    private static Fleet Guard(World w) => w.Fleets.Values.Single(f => f.OwnerId == Hid && f.Id.StartsWith(Hid + "_guard"));
    private static void Year(World w, NationSystem ns) { w.Turn++; ns.Tick(w); }
    private static void Divide(World w) => w.CivilWar = new CivilWar { GeneralId = "x", HouseId = "x", Since = w.Turn };
    /// <summary>Years until it makes war, up to a limit; -1 if it never does.</summary>
    private static int YearsUntilWar(World w, NationSystem ns, Nation n, int limit = 30) { for (int y = 1; y <= limit; y++) { Year(w, ns); if (n.AtWar) return y; } return -1; }

    [Fact]
    public void TheConcord_IsOnTheMap_WithAGateThreeForgeWorlds_AndTwoDivisions()
    {
        var (w, _, _, _, mine, n, d) = Start(); var g = w.Galaxy!;
        Assert.True(n.OnMap); Assert.Equal(Hid, n.HouseId); Assert.Equal("concordia", n.CapitalSystemId);
        Assert.Equal(4, w.HoldingsOf(Hid).Count()); Assert.All(w.HoldingsOf(Hid), h => Assert.Equal("ardent_concord", g.System(h.SystemId)!.RegionId));
        foreach (var m in NationSystem.Marches(w, d)) Assert.Equal(1, g.Hops(m, "ardent_gate"));              // the Gate is one lane beyond the Valmont Anvils and Ironhold, and the only way in
        Assert.Equal(2, g.Neighbors("concordia").Count()); Assert.DoesNotContain("valmont_anvils", g.Neighbors("concordia"));
        Assert.True(World.Outsider(Hid)); Assert.False(w.OfTheReach(Hid)); Assert.False(w.AtWar(Hid, "nation_vashti")); Assert.True(w.OfTheReach(mine.Id));
        // the strongest of the three: a home fleet and two divisions, all at full mettle
        var div = Divisions(w); Assert.Equal(2, div.Count); Assert.Equal(d.Hosts, div.Count);
        Assert.Equal(d.Strength, Guard(w).Strength + div.Sum(f => f.Strength)); Assert.Equal(NationSystem.GuardShare(d, d.Strength), Guard(w).Strength); Assert.True(Math.Abs(div[0].Strength - div[1].Strength) <= 1);
        Assert.All(div, f => Assert.Equal("ardent_gate", f.SystemId)); Assert.Equal("concordia", Guard(w).SystemId);
        Assert.Equal(n.Commander!.CharacterId, div[0].AdmiralId); Assert.Equal("", div[1].AdmiralId);          // one commander; the second division sails without him
        Assert.Contains("First Division", div[0].Name); Assert.Contains("Second Division", div[1].Name);
        Assert.True(d.Strength > Content.Nations.Def("vashti")!.Strength + Content.Nations.Def("thessar")!.Strength - 1); Assert.Equal(100, n.Mettle);
        Assert.True(d.Strikes); Assert.True(d.Campaigns); Assert.Equal(0, d.HoldYears); Assert.False(NationSystem.Buys(d)); Assert.True(NationSystem.ComesOut(n, d));
        Assert.All(w.Nations, x => Assert.Equal(Content.Nations.Def(x.Id)!.House != null, x.OnMap)); Assert.Equal(3, w.Nations.Count(x => x.OnMap));
    }

    [Fact]
    public void ItBeginsAWar_WhenTheReachIsDivided_AndNotOtherwise()
    {
        // undivided: thirty years, and it does not move
        var (w, ns, _, _, _, n, _) = Start(); Assert.Null(NationSystem.Divided(w)); Assert.Equal(-1, YearsUntilWar(w, ns, n));
        // divided: it strikes within a few years, and nobody voted it
        var (w2, ns2, _, _, mine2, n2, _) = Start(); Divide(w2); Assert.Equal("the civil war", NationSystem.Divided(w2));
        int years = YearsUntilWar(w2, ns2, n2); Assert.InRange(years, 1, 12);
        Assert.True(n2.BeganIt); Assert.Equal(1, w2.Counter(NationSystem.StruckCounter + ":concord")); Assert.Equal(0, w2.Counter(NationSystem.RevancheCounter));
        Assert.True(w2.HasFlag("nation_strikes:concord")); Assert.True(w2.HasFlag("foreign_war")); Assert.True(w2.AtWar(mine2.Id, Hid));
        Assert.Contains(w2.History, l => l.Text.Contains("makes war on the Reach") && l.Text.Contains("divided"));
        // not while it thinks well of the Reach; not while it pays tribute; not while it is short of its strength; not while another war is fought; not within a generation of its last
        var (w3, ns3, _, _, _, n3, _) = Start(); Divide(w3); n3.Disposition = 20; Assert.Equal(-1, YearsUntilWar(w3, ns3, n3, 12));
        var (w4, ns4, _, _, _, n4, _) = Start(); Divide(w4); n4.SubduedUntil = 40; Assert.Equal(-1, YearsUntilWar(w4, ns4, n4, 12));
        var (w5, ns5, _, _, _, n5, _) = Start(); Divide(w5); var div = Divisions(w5)[0]; div.Strength = 1; div.Ships.Clear(); div.Ships["frigate"] = 1; div.Recount(); w5.Turn = 5; Year(w5, ns5); Year(w5, ns5); Year(w5, ns5); Assert.False(n5.AtWar);
        var (w6, ns6, _, _, mine6, n6, _) = Start(); Divide(w6); NationSystem.DeclareWar(w6, Content.Nations, "compact", mine6.Id); Post(w6, "x", mine6.Id, "sorrow", 3);
        for (int y = 0; y < 8; y++) { Year(w6, ns6); Assert.False(n6.AtWar); }
        var (w7, ns7, _, _, _, n7, _) = Start(); Divide(w7); n7.LastWarEnd = w7.Turn + 1; for (int y = 1; y < NationSystem.StrikeRest; y++) { Year(w7, ns7); Assert.False(n7.AtWar); }
        // and the nations that do not begin wars never do, however divided the Reach
        var (w8, ns8, _, _, _, _, _) = Start(); Divide(w8); var khans = w8.Nation("vashti")!; khans.Disposition = -80; for (int y = 0; y < 20; y++) Year(w8, ns8); Assert.False(khans.AtWar); Assert.False(w8.Nation("thessar")!.AtWar);
    }

    [Fact]
    public void ItComesBackForAWorldOfItsOwn_WhichDoesNotRiseOfItself()
    {
        var (w, ns, _, _, mine, n, d) = Start(); var yards = w.Holding("brandt_yards")!; yards.OwnerId = mine.Id; yards.FormerOwnerId = Hid;
        w.Turn = 5; n.LastWarEnd = w.Turn; n.Disposition = 60;                                                              // just beaten, and on good terms: it waits out its rest, and the world does not rise
        for (int y = 1; y < NationSystem.StrikeRest; y++) { Year(w, ns); Assert.False(n.AtWar); Assert.Equal(mine.Id, yards.OwnerId); }
        Assert.Equal(0, w.Counter(NationSystem.RisenCounter)); Assert.Equal(0, w.Counter(NationSystem.UnheldKey("brandt_yards")));
        Assert.Null(NationSystem.Divided(w));
        int years = YearsUntilWar(w, ns, n); Assert.InRange(years, 1, 12);                                     // then it comes, undivided Reach or not, and whatever it thinks of the Reach
        Assert.True(n.BeganIt); Assert.Equal(1, w.Counter(NationSystem.RevancheCounter + ":concord")); Assert.True(w.HasFlag("nation_comes_back:concord"));
        Assert.Contains(w.History, l => l.Text.Contains("to take back " + yards.Name));
        _ = d;
    }

    [Fact]
    public void ItFightsACampaign_NotARaid_OnTwoLines()
    {
        var (w, ns, fs, _, mine, n, d) = Start(); NationSystem.DeclareWar(w, Content.Nations, "concord", mine.Id);
        foreach (var f in w.Fleets.Values.Where(f => !World.Outsider(f.OwnerId)).ToList()) w.Fleets.Remove(f.Id);   // nobody to stop it
        Year(w, ns);
        var div = Divisions(w); Assert.All(div, f => Assert.True(f.Moving)); Assert.NotEqual(div[0].DestinationId, div[1].DestinationId);   // two divisions, two worlds
        Assert.All(div, f => Assert.NotEqual(Ring.Beyond, w.Galaxy!.RingOf(f.DestinationId))); Assert.Equal(2, w.Counter(NationSystem.HostsCounter + ":concord"));
        // a division that takes a world does not go home with it: it makes for the next
        var first = div[0]; string sys = first.DestinationId; first.SystemId = sys; first.DestinationId = ""; first.Eta = 0;
        var world = w.Holdings.Values.Where(h => h.IsPlanet && h.SystemId == sys).OrderBy(h => h.Id, StringComparer.Ordinal).First(); string was = world.OwnerId;
        for (int y = 0; y < 12 && world.OwnerId == was; y++) fs.ResolveConquests(w);
        Assert.Equal(Hid, world.OwnerId);
        var next = NationSystem.HostOrder(w, n, d, first, new HashSet<string> { div[1].DestinationId });
        Assert.NotNull(next); Assert.NotEqual(NationSystem.Border(w, n, d), next); Assert.NotEqual(div[1].DestinationId, next); Assert.NotEqual(Ring.Beyond, w.Galaxy!.RingOf(next!));
        // the Khanates' host, in the same place, goes home with what it took
        var (w2, ns2, fs2, _, mine2, _, _) = Start(); var khans = w2.Nation("vashti")!; var kd = Content.Nations.Def("vashti")!; NationSystem.DeclareWar(w2, Content.Nations, "vashti", mine2.Id);
        foreach (var f in w2.Fleets.Values.Where(f => !World.Outsider(f.OwnerId)).ToList()) w2.Fleets.Remove(f.Id);
        var host = w2.Fleets.Values.Single(f => f.OwnerId == "nation_vashti" && !f.Id.Contains("_guard")); host.SystemId = "the_veil";
        var veil = w2.Holdings.Values.Where(h => h.IsPlanet && h.SystemId == "the_veil").OrderBy(h => h.Id, StringComparer.Ordinal).First(); string theirs = veil.OwnerId;
        for (int y = 0; y < 12 && veil.OwnerId == theirs; y++) fs2.ResolveConquests(w2);
        Assert.Equal("the_long_grass", NationSystem.HostOrder(w2, khans, kd, host));
        _ = ns2;
    }

    [Fact]
    public void AWarItBegan_IsOverWhenItIsOver_AndItRestsAGeneration()
    {
        var (w, ns, _, _, mine, n, d) = Start(); Divide(w); int years = YearsUntilWar(w, ns, n); Assert.True(years > 0);
        Post(w, "watch", mine.Id, "valmont_anvils", 3);
        n.ScoreReach = d.Threshold + n.ScoreNation; Year(w, ns);
        Assert.False(n.AtWar); Assert.False(n.BeganIt); Assert.True(n.Subdued(w.Turn)); Assert.Equal(w.Turn, n.LastWarEnd); Assert.Equal(1, w.Counter(NationSystem.WonCounter + ":concord"));
        n.SubduedUntil = 0; for (int y = 1; y < NationSystem.StrikeRest; y++) { Year(w, ns); Assert.False(n.AtWar); }
    }

    [Fact]
    public void TheReachsOwnLife_GoesOnWithoutThem()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 17); var tc = new TurnController(w, Content); var d = Content.Nations.Def("concord")!;
        Assert.True(w.Nation("concord")!.OnMap);
        for (int t = 0; t < 25 && !w.IsDynastyDead; t++) { foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); } tc.EndTurn(); }
        var n = w.Nation("concord")!;
        Assert.False(n.Fallen); Assert.Null(FactionSystem.Of(w, Hid)); Assert.DoesNotContain(w.Magistrates, m => m.HouseId == Hid); Assert.DoesNotContain(w.Offices, o => o.HouseId == Hid);
        Assert.DoesNotContain(w.Wars, x => x.Involves(Hid)); Assert.False(w.Regions.ContainsKey("ardent_concord")); Assert.All(w.Foreigners(), f => Assert.Null(DynastySystem.Spouse(w, f)));
        Assert.Equal(n.Strength, w.Fleets.Values.Where(f => f.OwnerId == Hid).Sum(f => f.Strength)); Assert.True(n.Strength <= d.Strength);
        Assert.True(w.Fleets.Values.Count(f => f.OwnerId == Hid) <= d.Hosts + 1);
    }
}
