using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Rng;
using Praetoria.Core.Galaxy;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The Vashti Khanates on the map (docs/BEYOND.md, stages 1 and 2 for one nation; the owner's word of 2026-11-18: "add in the khanate
/// first"). The nation holds worlds beyond its march through a house that is not of the Reach; its ruler and commander are people; its
/// strength is fleets; a war with it is fought on the map by the ordinary rules; and what it takes and loses is settled at the peace.</summary>
public class KhanateTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private sealed class Fixed : IRng { private readonly double _d; public Fixed(double d) => _d = d; public ulong State => 0; public double NextDouble() => _d; public int NextInt(int lo, int hi) => hi <= lo ? lo : lo + (int)(_d * (hi - lo)); }
    private const string Hid = "nation_vashti";

    private static (World w, NationSystem ns, FleetSystem fs, Character me, House mine, Nation n, NationDef d) Start(int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), (ulong)seed); w.BeyondVaries = false;
        var fs = new FleetSystem(Content.Fleets, Content.Holdings, Content.ShipClasses, Content.Friction); var ns = new NationSystem(Content.Nations, fs);
        NationSystem.Seed(w, Content.Nations);
        foreach (var x in w.Nations) { x.Disposition = 50; if (x.Ruler != null) { x.Ruler.Nature = "mild"; x.Ruler.Age = 30; } }   // nobody raids, nobody dies of age, while a test is looking at something else
        return (w, ns, fs, w.Protagonist!, w.House(w.Protagonist!.HouseId)!, w.Nation("vashti")!, Content.Nations.Def("vashti")!);
    }
    private static Fleet Post(World w, string id, string houseId, string systemId, int hulls)
    { var f = new Fleet { Id = id, Name = "the " + id, OwnerId = houseId, SystemId = systemId, Strength = hulls, Supply = 100, Readiness = 100, Loyalty = 80 }; w.Fleets[id] = f; return f; }
    private static void War(World w, House mine) { NationSystem.DeclareWar(w, Content.Nations, "vashti", mine.Id); Assert.True(w.Nation("vashti")!.AtWar); }
    private static Fleet Host(World w) => w.Fleets.Values.Single(f => f.OwnerId == Hid && !f.Id.StartsWith(Hid + "_guard"));
    private static Fleet Guard(World w) => w.Fleets.Values.Single(f => f.OwnerId == Hid && f.Id.StartsWith(Hid + "_guard"));
    private static Holding WorldAt(World w, string sys) => w.Holdings.Values.Where(h => h.IsPlanet && h.SystemId == sys).OrderBy(h => h.Id, StringComparer.Ordinal).First();
    private static void ClearOrbit(World w, string sys) { foreach (var f in w.Fleets.Values.Where(f => f.SystemId == sys && f.OwnerId != Hid).ToList()) w.Fleets.Remove(f.Id); }
    private static int Take(World w, FleetSystem fs, Holding h) { string was = h.OwnerId; int years = 0; while (h.OwnerId == was && years < 12) { fs.ResolveConquests(w); years++; } return years; }

    [Fact]
    public void TheKhanates_HoldWorldsBeyondTheirMarch_ThroughAHouseThatIsNotOfTheReach()
    {
        var (w, _, _, _, mine, n, d) = Start(); var g = w.Galaxy!;
        Assert.True(n.OnMap); Assert.Equal(Hid, n.HouseId); Assert.Equal("khal_orda", n.CapitalSystemId);
        var house = w.House(Hid)!; Assert.Equal("The Vashti Khanates", house.Name); Assert.Equal("vashti", house.Culture);
        var worlds = w.HoldingsOf(Hid).ToList(); Assert.Equal(3, worlds.Count);
        Assert.All(worlds, h => { Assert.Equal(Ring.Beyond, g.RingOf(h.SystemId)); Assert.Equal(n.Ruler!.CharacterId, h.GovernorId); });
        foreach (var m in NationSystem.Marches(w, d)) Assert.Equal(1, g.Hops(m, "the_long_grass"));          // its border world is one lane beyond each door
        // it is not a house of the Reach, and its people are not of the Reach's world
        Assert.True(World.Outsider(Hid)); Assert.True(w.Foreign(Hid)); Assert.False(w.OfTheReach(Hid)); Assert.False(PolitySystem.InReach(w, Hid)); Assert.True(w.OfTheReach(mine.Id));
        var ruler = w.Char(n.Ruler!.CharacterId)!; var commander = w.Char(n.Commander!.CharacterId)!;
        Assert.Equal(n.Ruler.Name, ruler.Name); Assert.Contains(NationSystem.RulerChar, ruler.Flags); Assert.Contains(NationSystem.CommanderChar, commander.Flags);
        Assert.DoesNotContain(w.LivingCharacters(), c => c.HouseId == Hid); Assert.Equal(2, w.Foreigners().Count(c => c.HouseId == Hid)); Assert.Null(Economy.HeadOf(w, house));
        // its strength is two fleets: the guard at the capital under the ruler, the host at the border under the commander
        Assert.Equal(d.Strength, Guard(w).Strength + Host(w).Strength); Assert.Equal(n.Strength, d.Strength);
        Assert.Equal("khal_orda", Guard(w).SystemId); Assert.Equal(ruler.Id, Guard(w).AdmiralId); Assert.Equal("the_long_grass", Host(w).SystemId); Assert.Equal(commander.Id, Host(w).AdmiralId);
        // placing it twice changes nothing, and the nations with no worlds on this map stay off it
        NationSystem.Seed(w, Content.Nations); Assert.Equal(3, w.HoldingsOf(Hid).Count()); Assert.Equal(2, w.Fleets.Values.Count(f => f.OwnerId == Hid));
        Assert.All(w.Nations, x => Assert.Equal(Content.Nations.Def(x.Id)!.House != null, x.OnMap));
    }

    [Fact]
    public void AWarWithThem_IsEveryHousesWar_AndIsReachedByTheLanes()
    {
        var (w, _, _, me, mine, n, _) = Start(); var ctx = new CommandContext(w, TestContent.Rng()); var other = w.Houses.Values.First(h => h.Id != mine.Id && w.OfTheReach(h.Id));
        Assert.False(w.AtWar(mine.Id, Hid)); Assert.DoesNotContain(Hid, w.EnemiesOf(mine.Id)); Assert.Empty(w.EnemiesOf(Hid));
        Assert.False(new DeclareWarCommand(me.Id, Hid, Content.Fleets).CanExecute(ctx));                    // war with a nation is the Senate's to vote
        War(w, mine);
        Assert.True(w.AtWar(mine.Id, Hid)); Assert.True(w.AtWar(Hid, other.Id)); Assert.False(w.AtWar(mine.Id, other.Id));
        Assert.Contains(Hid, w.EnemiesOf(other.Id)); Assert.Contains(mine.Id, w.EnemiesOf(Hid));
        Post(w, "exp", mine.Id, "anvil", 8); w.PoolsFor(me.Id).Treasury = 2;
        Assert.Contains("by the lanes", new SendExpeditionCommand(me.Id, "exp", "vashti", Content.Fleets, Content.Nations).Refusal(ctx));
        Assert.Null(new MoveFleetCommand(me.Id, "exp", "the_long_grass", Content.Fleets).Refusal(ctx));     // and the lanes go there
    }

    [Fact]
    public void TheirHost_ComesThroughTheMarch_TakesOneWorldSlowly_AndGoesHomeWithIt()
    {
        var (w, ns, fs, _, mine, n, d) = Start(); War(w, mine);
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId != Hid).ToList()) w.Fleets.Remove(f.Id);    // nobody to stop it
        w.Turn++; ns.Tick(w);
        var host = Host(w); Assert.True(host.Moving); Assert.NotEqual(Ring.Beyond, w.Galaxy!.RingOf(host.DestinationId)); Assert.Equal(1, w.Counter(NationSystem.HostsCounter));
        Assert.False(Guard(w).Moving);                                                                        // the guard stays with the ruler
        // over a world of the Reach: slow at walls, and it stays until the world falls
        string sys = host.DestinationId; host.SystemId = sys; host.DestinationId = ""; host.Eta = 0; var world = WorldAt(w, sys); string was = world.OwnerId;
        Assert.Null(NationSystem.HostOrder(w, n, d, host));
        int need = fs.SiegeRequired(w, world) + NationSystem.SiegeSlow; Assert.Equal(need, Take(w, fs, world));
        Assert.Equal(Hid, world.OwnerId); Assert.Equal(was, world.FormerOwnerId); Assert.Equal(NationSystem.ScorePerWorld, n.ScoreNation); Assert.Equal(1, w.Counter(NationSystem.WorldsLostCounter));
        // one world a season: home with what it took, and out again only when it is back
        Assert.Equal("the_long_grass", NationSystem.HostOrder(w, n, d, host));
        host.SystemId = "the_long_grass"; Assert.Null(NationSystem.HostOrder(w, n, d, host)); Assert.NotNull(NationSystem.HostOrder(w, n, d, host));
        // a host that is spent, or a nation at peace, stays home
        host.Supply = 30; Assert.Null(NationSystem.HostOrder(w, n, d, host)); host.Supply = 100; n.AtWar = false; Assert.Null(NationSystem.HostOrder(w, n, d, host));
    }

    [Fact]
    public void AWorldTheyTook_FreedByAnotherHouse_GoesHomeToTheHouseItWasTakenFrom()
    {
        var (w, _, fs, _, mine, n, _) = Start(); War(w, mine); var world = WorldAt(w, "the_veil"); string was = world.OwnerId;
        var liberator = w.Houses.Values.First(h => h.Id != was && h.Id != mine.Id && w.OfTheReach(h.Id)); int standing = liberator.Legitimacy = 50;
        world.OwnerId = Hid; world.FormerOwnerId = was; ClearOrbit(w, "the_veil");
        Post(w, "relief", liberator.Id, "the_veil", 4);
        Assert.Equal(fs.SiegeRequired(w, world), Take(w, fs, world));                                         // no slower than any siege
        Assert.Equal(was, world.OwnerId); Assert.Equal("", world.FormerOwnerId);
        Assert.Equal(1, w.Counter(NationSystem.FreedCounter)); Assert.Equal(NationSystem.ScorePerWorld, n.ScoreReach); Assert.Equal(standing + 1, liberator.Legitimacy);
        Assert.Equal(2, w.Counter(NationSystem.WinsKey(liberator.Id, "vashti")));                             // and it counts toward whose triumph it will be
        Assert.Contains(w.History, l => l.Text.Contains("frees") && l.Text.Contains("again"));
        // a world of their own taken is the taker's, in a war they began (in one the Senate voted, another house sacks it and goes home: tests/WhoseWarTests.cs)
        n.BeganIt = true; var camp = w.Holding("long_grass_camp")!; ClearOrbit(w, "the_long_grass"); foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == Hid).ToList()) f.SystemId = "khal_orda";
        Post(w, "strike", liberator.Id, "the_long_grass", 4); Take(w, fs, camp);
        Assert.Equal(liberator.Id, camp.OwnerId); Assert.Equal(Hid, camp.FormerOwnerId); Assert.Equal(1, w.Counter(NationSystem.WorldsTakenCounter)); Assert.Equal(2 * NationSystem.ScorePerWorld, n.ScoreReach);
    }

    [Fact]
    public void AgainstTheirHost_TheHousesOfTheReachFightAsOne_AndTheBattleCountsInTheWar()
    {
        var (w, _, fs, _, mine, n, _) = Start(); War(w, mine); ClearOrbit(w, "the_veil");
        var two = w.Houses.Values.Where(h => h.Id != mine.Id && w.OfTheReach(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).Take(2).ToList();
        Host(w).SystemId = "the_veil"; Post(w, "a", two[0].Id, "the_veil", 3); Post(w, "b", two[1].Id, "the_veil", 5);
        fs.ResolveBattles(w, new Fixed(0.5));
        Assert.Single(fs.LastBattles); Assert.Equal(1, w.Counter(NationSystem.MapBattlesCounter));           // one battle, not one for each house
        Assert.Contains(w.History, l => l.Text.Contains("stand together behind " + two[1].Name));             // led by the stronger
        Assert.True((n.ScoreReach, n.ScoreNation) is (NationSystem.ScorePerBattle, 0) or (0, NationSystem.ScoreSkirmish));   // a host beaten counts two; a squadron swatted, one
        Assert.Equal(w.Turn, n.LastBattleTurn);
        // at peace nobody fights
        var (w2, _, fs2, _, mine2, _, _) = Start(); Host(w2).SystemId = "the_veil"; Post(w2, "a", mine2.Id, "the_veil", 5); fs2.ResolveBattles(w2, new Fixed(0.5)); Assert.Empty(fs2.LastBattles);
    }

    [Fact]
    public void AtThePeace_TheWinnerKeepsWhatItTook_AndTheLoserGivesItBack()
    {
        // the Reach wins: what they occupied goes home, what the Reach took it keeps
        var (w, ns, _, _, mine, n, d) = Start(); War(w, mine); var veil = WorldAt(w, "the_veil"); string was = veil.OwnerId;
        veil.OwnerId = Hid; veil.FormerOwnerId = was; var camp = w.Holding("long_grass_camp")!; camp.OwnerId = mine.Id; camp.FormerOwnerId = Hid; Post(w, "garrison", mine.Id, "the_long_grass", 3);
        n.ScoreReach = d.Threshold; w.Turn++; ns.Tick(w);
        Assert.False(n.AtWar); Assert.True(n.Subdued(w.Turn)); Assert.Equal(was, veil.OwnerId); Assert.Equal(1, w.Counter(NationSystem.WorldsReturnedCounter)); Assert.Equal(mine.Id, camp.OwnerId);
        Assert.False(w.AtWar(mine.Id, Hid));
        // the Reach loses: what they hold is ceded, and what the Reach took of theirs is given up
        var (w2, ns2, _, _, mine2, n2, d2) = Start(); War(w2, mine2); var veil2 = WorldAt(w2, "the_veil"); string was2 = veil2.OwnerId;
        veil2.OwnerId = Hid; veil2.FormerOwnerId = was2; var camp2 = w2.Holding("long_grass_camp")!; camp2.OwnerId = mine2.Id; camp2.FormerOwnerId = Hid; Post(w2, "garrison", mine2.Id, "the_long_grass", 3);
        n2.ScoreNation = d2.Threshold; w2.Turn++; ns2.Tick(w2);
        Assert.False(n2.AtWar); Assert.False(n2.Subdued(w2.Turn)); Assert.Equal(Hid, veil2.OwnerId); Assert.Equal("", veil2.FormerOwnerId); Assert.Equal(1, w2.Counter(NationSystem.WorldsCededCounter));
        Assert.Equal(Hid, camp2.OwnerId); Assert.Equal(1, w2.Counter(NationSystem.GivenUpCounter)); Assert.True(w2.HasFlag("steppe_world_lost"));
    }

    [Fact]
    public void TakingTheirCapital_EndsTheWar_AndTheRulerSitsSomewhereElse()
    {
        var (w, ns, _, _, mine, n, _) = Start(); War(w, mine); var great = w.Holding("khal_orda_camp")!;
        great.OwnerId = mine.Id; great.FormerOwnerId = Hid; Post(w, "garrison", mine.Id, "khal_orda", 3); Guard(w).SystemId = "tul_aimag";
        w.Turn++; ns.Tick(w);
        Assert.False(n.AtWar); Assert.True(n.Subdued(w.Turn)); Assert.Equal("tul_aimag", n.CapitalSystemId); Assert.Equal(mine.Id, great.OwnerId);
        Assert.Contains(w.History, l => l.Text.Contains("sits at"));
    }

    [Fact]
    public void AWorldOfTheirs_IsEasyToTake_AndDoesNotStayTakenWithoutAFleetOverIt()
    {
        var (w, ns, _, _, mine, n, _) = Start(); var camp = w.Holding("long_grass_camp")!; camp.OwnerId = mine.Id; camp.FormerOwnerId = Hid; ClearOrbit(w, "the_long_grass");
        for (int y = 1; y < NationSystem.RiseYears; y++) { w.Turn++; ns.Tick(w); Assert.Equal(mine.Id, camp.OwnerId); Assert.True(w.HasFlag("steppe_world_restless")); }
        w.Turn++; ns.Tick(w);
        Assert.Equal(Hid, camp.OwnerId); Assert.Equal(1, w.Counter(NationSystem.RisenCounter)); Assert.True(w.HasFlag("steppe_world_lost")); Assert.Equal(n.Ruler!.CharacterId, camp.GovernorId);
        // with a fleet over it, or while they pay tribute, it stays
        camp.OwnerId = mine.Id; Post(w, "garrison", mine.Id, "the_long_grass", NationSystem.MarchGuard);
        for (int y = 0; y < NationSystem.RiseYears + 2; y++) { w.Turn++; ns.Tick(w); }
        Assert.Equal(mine.Id, camp.OwnerId);
        w.Fleets.Remove("garrison"); n.SubduedUntil = w.Turn + 20; for (int y = 0; y < NationSystem.RiseYears + 2; y++) { w.Turn++; ns.Tick(w); }
        Assert.Equal(mine.Id, camp.OwnerId);
    }

    [Fact]
    public void TheirPeople_CanBeKilledOnTheMap_AndAreFollowed()
    {
        var (w, ns, _, _, _, n, _) = Start(); string khan = n.Ruler!.Name, noyan = n.Commander!.Name;
        w.Char(n.Ruler.CharacterId)!.Alive = false; w.Turn++; ns.Tick(w);
        Assert.NotEqual(khan, n.Ruler!.Name); Assert.True(w.Char(n.Ruler.CharacterId)!.Alive); Assert.Contains(NationSystem.RulerChar, w.Char(n.Ruler.CharacterId)!.Flags);
        Assert.Equal(1, w.Counter(NationSystem.SuccessionsCounter)); Assert.Equal(n.Ruler.CharacterId, Guard(w).AdmiralId);
        string before = n.Commander!.Name; w.Char(n.Commander.CharacterId)!.Alive = false; w.Turn++; ns.Tick(w);
        Assert.NotEqual(before, n.Commander!.Name); Assert.True(w.Char(n.Commander.CharacterId)!.Alive); Assert.True(w.Counter(NationSystem.CommandersFallenCounter) >= 1);
        Assert.Equal(n.Commander.CharacterId, Host(w).AdmiralId);
        Assert.Equal(1, w.Foreigners().Count(c => c.HouseId == Hid && c.Flags.Contains(NationSystem.RulerChar))); Assert.Equal(1, w.Foreigners().Count(c => c.HouseId == Hid && c.Flags.Contains(NationSystem.CommanderChar)));
        _ = noyan;
    }

    [Fact]
    public void WithNoWorldLeft_TheNationIsNoMore()
    {
        var (w, ns, _, me, mine, n, _) = Start(); War(w, mine); var veil = WorldAt(w, "the_veil"); string was = veil.OwnerId; veil.OwnerId = Hid; veil.FormerOwnerId = was;
        foreach (var h in w.HoldingsOf(Hid).Where(h => h.Id != veil.Id).ToList()) { h.OwnerId = mine.Id; h.FormerOwnerId = Hid; }
        veil.OwnerId = was;                                                                                     // and the one it took is freed
        w.Turn++; ns.Tick(w);
        Assert.True(n.Fallen); Assert.False(n.AtWar); Assert.Equal(0, n.Strength); Assert.Empty(w.Fleets.Values.Where(f => f.OwnerId == Hid)); Assert.DoesNotContain(w.Foreigners(), c => c.HouseId == Hid);
        Assert.Equal(1, w.Counter(NationSystem.FallenCounter)); Assert.True(w.HasFlag(NationSystem.FallenFlag));
        for (int y = 0; y < 8; y++) { w.Turn++; ns.Tick(w); }                                                   // it does nothing more, and its worlds do not rise
        Assert.All(new[] { "khal_orda_camp", "tul_aimag_wells", "long_grass_camp" }, id => Assert.Equal(mine.Id, w.Holding(id)!.OwnerId));
        NationSystem.DeclareWar(w, Content.Nations, "vashti", mine.Id); Assert.False(n.AtWar);
        _ = me;
    }

    [Fact]
    public void TheReachsOwnLife_GoesOnWithoutThem()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 11); w.BeyondVaries = false;
        var tc = new TurnController(w, Content); var d = Content.Nations.Def("vashti")!;
        Assert.True(w.Nation("vashti")!.OnMap);                                                                 // a saga opens with them on the map
        for (int t = 0; t < 25 && !w.IsDynastyDead; t++) { foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); } tc.EndTurn(); }
        var n = w.Nation("vashti")!; var house = w.House(Hid)!;
        Assert.NotNull(house); Assert.False(n.Fallen);
        Assert.Null(FactionSystem.Of(w, Hid)); Assert.DoesNotContain(w.Magistrates, m => m.HouseId == Hid); Assert.DoesNotContain(w.Offices, o => o.HouseId == Hid);   // no bench, no office
        Assert.DoesNotContain(w.Wars, x => x.Involves(Hid));                                                     // no house's private war
        Assert.DoesNotContain(w.Characters.Values, c => c.HouseId == Hid && !c.Flags.Contains(NationSystem.ForeignChar));   // nobody marries in, nobody is born to it
        Assert.All(w.Foreigners(), f => Assert.Null(DynastySystem.Spouse(w, f)));
        Assert.False(w.Regions.ContainsKey("vashti_steppe"));                                                    // the Reach's allegiance is not reckoned there
        Assert.True(house.Treasury.Credits >= NationSystem.PurseFloor); Assert.True(w.Fleets.Values.Where(f => f.OwnerId == Hid).Sum(f => f.Strength) <= d.Strength);
        Assert.Equal(n.Strength, w.Fleets.Values.Where(f => f.OwnerId == Hid).Sum(f => f.Strength));
    }
}
