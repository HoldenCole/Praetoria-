using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Galaxy;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The powers beyond the Reach, stage 0 (docs/BEYOND.md; docs/GAPS.md R10): each nation has a march on the rim that its raids come
/// through and its wars are reached by, and a ruler and a commander with names, who count and who die.</summary>
public class BeyondTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static (World w, NationSystem ns, FleetSystem fs, Character me, House mine) Start(int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), (ulong)seed);
        var fs = new FleetSystem(Content.Fleets, Content.Holdings, Content.ShipClasses, Content.Friction); var ns = new NationSystem(Content.Nations, fs);
        NationSystem.Seed(w, Content.Nations);
        return (w, ns, fs, w.Protagonist!, w.House(w.Protagonist!.HouseId)!);
    }
    private static Fleet Post(World w, string id, string houseId, string systemId, int hulls, string admiralId = "")
    { var f = new Fleet { Id = id, Name = "the " + id, OwnerId = houseId, SystemId = systemId, Strength = hulls, Supply = 100, Readiness = 100, Loyalty = 80, AdmiralId = admiralId }; w.Fleets[id] = f; return f; }
    private static void OnlyTheKhans(World w) { foreach (var n in w.Nations) { n.Disposition = n.Id == "vashti" ? -60 : 50; if (n.Ruler != null) n.Ruler.Nature = n.Id == "vashti" ? "warlike" : "mild"; } }

    [Fact]
    public void EveryNation_HasADoorOnTheRim_AndPeopleWithNames()
    {
        var (w, ns, fs, me, mine) = Start(); var seen = new HashSet<string>();
        Assert.Equal(5, w.Nations.Count);
        foreach (var n in w.Nations)
        {
            var d = Content.Nations.Def(n.Id)!; var marches = NationSystem.Marches(w, d);
            Assert.NotEmpty(marches); Assert.All(marches, m => Assert.Equal(Ring.Rim, w.Galaxy!.RingOf(m))); Assert.All(marches, m => Assert.True(seen.Add(m), $"{m} is the march of two nations"));
            Assert.True(d.Depth >= 1); Assert.True(d.Names.Count >= 10); Assert.NotEqual("ruler", d.RulerTitle); Assert.NotEqual("commander", d.CommanderTitle);
            Assert.NotNull(n.Ruler); Assert.NotNull(n.Commander); Assert.NotEqual(n.Ruler!.Name, n.Commander!.Name); Assert.Contains(n.Ruler.Name, d.Names); Assert.Contains(n.Ruler.Nature, NationSystem.Natures);
            Assert.InRange(n.Commander.Skill, 1, 9); Assert.InRange(n.Ruler.Age, 30, 60);
            Assert.StartsWith(d.RulerTitle + " ", NationSystem.Styled(d, n.Ruler, true)); Assert.StartsWith(d.CommanderTitle + " ", NationSystem.Styled(d, n.Commander, false));
        }
        // a save from before they had anyone is given them at the next reckoning
        var khans = w.Nation("vashti")!; khans.Ruler = null; khans.Commander = null; w.Turn++; ns.Tick(w); Assert.NotNull(khans.Ruler); Assert.NotNull(khans.Commander);
        // who leads them counts in a battle
        Assert.Equal(1.0, NationSystem.CommanderFactor(null)); Assert.Equal(1.0, NationSystem.CommanderFactor(new NationPerson { Skill = 4 }));
        Assert.True(NationSystem.CommanderFactor(new NationPerson { Skill = 9 }) > 1.25); Assert.True(NationSystem.CommanderFactor(new NationPerson { Skill = 1 }) < 0.85);
    }

    [Fact]
    public void AnExpedition_IsAsLongAsTheLanesToTheMarch_AndTheNationsDepthBeyondIt()
    {
        var (w, ns, fs, me, mine) = Start(); var khans = Content.Nations.Def("vashti")!; var concord = Content.Nations.Def("concord")!;
        Assert.Equal(0, NationSystem.HopsToMarch(w, khans, "anvil")); Assert.Equal(khans.Depth, NationSystem.OutTurns(w, khans, "anvil"));   // posted at the march: nearest
        int far = NationSystem.HopsToMarch(w, khans, "ironhold"); Assert.True(far >= 4);                                                   // the far side of the map
        Assert.Equal(khans.Depth + (far + NationSystem.HopsPerYear - 1) / NationSystem.HopsPerYear, NationSystem.OutTurns(w, khans, "ironhold"));
        Assert.True(NationSystem.OutTurns(w, khans, "ironhold") > NationSystem.OutTurns(w, khans, "anvil"));
        Assert.Equal(concord.Depth, NationSystem.OutTurns(w, concord, "ironhold"));                                                         // and the Concord's door is there
        // the order to sail uses it
        // (against a nation that is off the map. Every shipped nation is on it now and is sailed to by the lanes, so the Compact is taken off
        // it here: an older game's map, or one drawn without its worlds, still has nations of this kind)
        var compact = Content.Nations.Def("compact")!; var off = w.Nation("compact")!;
        foreach (var x in w.HoldingsOf(off.HouseId).ToList()) w.Holdings.Remove(x.Id); foreach (var x in w.Fleets.Values.Where(x => x.OwnerId == off.HouseId).ToList()) w.Fleets.Remove(x.Id);
        w.Houses.Remove(off.HouseId); off.HouseId = ""; off.CapitalSystemId = ""; Assert.False(off.OnMap);
        NationSystem.DeclareWar(w, Content.Nations, "compact", mine.Id); var f = Post(w, "exp", mine.Id, "anvil", 8); w.PoolsFor(me.Id).Treasury = 2; mine.Treasury.Credits = 500; mine.Treasury.Helium3 = 60;
        var ctx = new CommandContext(w, TestContent.Rng());
        Assert.Contains("by the lanes", new SendExpeditionCommand(me.Id, "exp", "vashti", Content.Fleets, Content.Nations).Refusal(ctx));
        Assert.Contains("by the lanes", new SendExpeditionCommand(me.Id, "exp", "concord", Content.Fleets, Content.Nations).Refusal(ctx));
        var send = new SendExpeditionCommand(me.Id, "exp", "compact", Content.Fleets, Content.Nations);
        Assert.Null(send.Refusal(ctx)); send.Execute(ctx); Assert.Equal(NationSystem.OutTurns(w, compact, "anvil"), f.AwayTurns); Assert.True(f.AwayTurns > compact.Depth);
        // with no march on the map, the old figure
        var bare = new NationDef { Id = "x", Distance = 3 }; Assert.Equal(3, NationSystem.OutTurns(w, bare, "anvil"));
    }

    [Fact]
    public void RaidsComeThroughTheMarch_FallNearIt_AndAFleetStandingThereTurnsThemBack()
    {
        var (w, ns, fs, me, mine) = Start(); OnlyTheKhans(w); var d = Content.Nations.Def("vashti")!; var marches = NationSystem.Marches(w, d);
        foreach (var f in w.Fleets.Values.ToList()) w.Fleets.Remove(f.Id);
        for (int t = 0; t < 120 && w.Counter(NationSystem.RaidsCounter) < 4; t++) { w.Turn++; ns.Tick(w); }
        Assert.True(w.Counter(NationSystem.RaidsCounter) >= 3); Assert.Equal(0, w.Counter(NationSystem.RaidsMetCounter));
        var hit = w.History.Where(h => h.Text.StartsWith("Raiders of The Vashti Khanates fall on ")).ToList(); Assert.NotEmpty(hit);
        Assert.All(hit, h => Assert.Contains(" under Noyan ", h.Text));
        var near = w.Holdings.Values.Where(x => x.IsPlanet && marches.Any(m => w.Galaxy!.Hops(m, x.SystemId) is >= 0 and <= NationSystem.RaidReach)).Select(x => x.Name).ToList();
        Assert.All(hit, h => Assert.Contains(near, name => h.Text.StartsWith($"Raiders of The Vashti Khanates fall on {name} ")));          // never the far side of the map
        // a fleet of three at the march, under a commander who gets the name of it
        var (w2, ns2, fs2, me2, mine2) = Start(); OnlyTheKhans(w2); foreach (var f in w2.Fleets.Values.ToList()) w2.Fleets.Remove(f.Id);
        Post(w2, "warden", mine2.Id, "anvil", 3, me2.Id); Assert.Equal("warden", NationSystem.Guard(w2, d)!.Id);
        bool flagged = false;
        for (int t = 0; t < 120 && w2.Counter(NationSystem.RaidsMetCounter) < 2; t++) { w2.Turn++; ns2.Tick(w2); flagged |= w2.HasFlag("raid_met_at_the_march"); }
        Assert.True(w2.Counter(NationSystem.RaidsMetCounter) >= 2, $"met {w2.Counter(NationSystem.RaidsMetCounter)}, landed {w2.Counter(NationSystem.RaidsCounter)}, turned back {w2.Counter(NationSystem.RaidsRepelledCounter)}, guard {NationSystem.Guard(w2, d)?.Id}, regard {w2.Nation("vashti")!.Disposition}"); Assert.Equal(0, w2.Counter(NationSystem.RaidsCounter)); Assert.True(flagged, "never flagged");
        Assert.Contains(w2.History, h => h.Text.Contains("turned back at the march")); Assert.Contains(me2.Life!.Deeds, x => x.Text.Contains("Turned back raiders of The Vashti Khanates at The Anvil"));
        Assert.Contains(w2.History, h => h.Text.StartsWith("Raiders of The Vashti Khanates under Noyan ") && h.Text.Contains("come through The Anvil"));
        // two hulls are not a guard, and a fleet under way is not standing there
        w2.Fleets["warden"].Strength = 2; Assert.Null(NationSystem.Guard(w2, d)); w2.Fleets["warden"].Strength = 5; w2.Fleets["warden"].DestinationId = "tessaly"; Assert.Null(NationSystem.Guard(w2, d));
    }

    [Fact]
    public void RulersDieAndAreFollowed_AndTheirNatureMovesTheNationsRegard()
    {
        var (w, ns, fs, me, mine) = Start(); var n = w.Nation("thessar")!; var d = Content.Nations.Def("thessar")!; string was = n.Ruler!.Name; n.Ruler.Age = 79; bool news = false;
        for (int t = 0; t < 30 && w.Counter(NationSystem.SuccessionsCounter) == 0; t++) { w.Turn++; ns.Tick(w); news |= w.HasFlag("nation_succession"); }
        Assert.Equal(1, w.Counter(NationSystem.SuccessionsCounter)); Assert.NotEqual(was, n.Ruler!.Name); Assert.True(news); Assert.Contains(n.Ruler.Nature, NationSystem.Natures);
        Assert.Contains(w.History, h => h.Text.StartsWith($"{d.RulerTitle} {was} of The Free Cities of Thessar is dead"));
        Assert.NotNull(n.Commander); Assert.NotEqual(n.Ruler.Name, n.Commander!.Name);
        // the warlike sour it and the mild sweeten it
        int After(string nature) { var (wx, nsx, _, _, _) = Start(); var nx = wx.Nation("compact")!; nx.Ruler!.Nature = nature; nx.Ruler.Age = 30; nx.Disposition = 0; for (int t = 0; t < 30; t++) { wx.Turn++; nsx.Tick(wx); } return nx.Disposition; }
        Assert.True(After("warlike") < After("wary")); Assert.True(After("mild") > After("warlike"));
        // an old commander lays it down
        var (w3, ns3, _, _, _) = Start(); var c = w3.Nation("concord")!.Commander!; string old = c.Name; c.Age = 66; w3.Turn++; ns3.Tick(w3); Assert.NotEqual(old, w3.Nation("concord")!.Commander!.Name);
    }

    [Fact]
    public void TheirCommander_CanBeKilledInTheRout_AndTheReportsCanFire()
    {
        // a great fleet in their space for season after season: sooner or later the Noyan is in the rout
        var (w, ns, fs, me, mine) = Start(); NationSystem.DeclareWar(w, Content.Nations, "vashti", mine.Id); var n = w.Nation("vashti")!; n.Strength = 200; string first = n.Commander!.Name; bool news = false;
        var f = Post(w, "host", mine.Id, "anvil", 60, me.Id); f.Away = "vashti"; f.AwayPhase = "fighting"; f.AwayTurns = 99; f.HomeSystemId = "anvil"; f.SystemId = "";
        for (int t = 0; t < 40 && w.Counter(NationSystem.CommandersFallenCounter) == 0; t++) { w.Turn++; n.AtWar = true; n.ScoreReach = 0; n.Strength = 200; f.Supply = 100; f.Readiness = 100; f.AwayTurns = 99; f.AwayPhase = "fighting"; ns.Tick(w); news |= w.HasFlag("nation_commander_fallen"); }
        Assert.True(w.Counter(NationSystem.CommandersFallenCounter) >= 1); Assert.NotEqual(first, n.Commander!.Name); Assert.True(news);
        Assert.Contains(w.History, h => h.Text.StartsWith($"Noyan {first} of The Vashti Khanates is killed in the rout")); Assert.Contains(me.Life!.Deeds, x => x.Text.Contains($"Beat Noyan {first}"));
        Assert.Contains(w.History, h => h.Text.Contains("beats the fleets of The Vashti Khanates under Noyan "));
        // the three reports hang on the flags the nations raise
        bool Eligible(World world, string id) { world.PoolsFor(world.ProtagonistId).Influence = 9; world.House(world.Protagonist!.HouseId)!.Treasury.Credits = 100; return new EventEngine(Content, WorldBuilder.RngFor(world)).GatherEligible(world).Any(e => e.def.Id == id); }
        var (wf, _, _, _, _) = Start();
        wf.WorldFlags.Add("raid_met_at_the_march"); Assert.True(Eligible(wf, "met_at_the_march"));
        wf.WorldFlags.Add("nation_succession"); Assert.True(Eligible(wf, "a_new_hand_beyond_the_rim"));
        wf.WorldFlags.Add("nation_commander_fallen"); Assert.True(Eligible(wf, "their_commander_falls"));
    }
}
