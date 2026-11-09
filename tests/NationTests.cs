using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Galaxy;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The nations beyond the Reach (docs/FOREIGN_NATIONS.md): the catalog, the war motion, expeditions off the map and home again, the
/// abstract battles, subdual and tribute, raids on the rim, and the wars that gutter out.</summary>
public class NationTests
{
    private static World Rings(int unrest = 0) { var m = typeof(RegionSystemTests).GetMethod("Rings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!; return (World)m.Invoke(null, new object[] { unrest })!; }
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static FleetSystem Fleets() => new(Content.Fleets, Content.Holdings, Content.ShipClasses);
    private static NationSystem Nations(FleetSystem fs) => new(Content.Nations, fs);
    private static SenateSystem Senate(FleetSystem fs) => new(Content.Motions, Content.Titles, fs, Content.Offices) { Policies = Content.Policies, Nations = Content.Nations };
    private static Fleet At(World w, string id, string owner, string sys, int hulls, string admiral = "") { var f = new Fleet { Id = id, Name = id, OwnerId = owner, SystemId = sys, Strength = hulls, AdmiralId = admiral }; w.Fleets[id] = f; return f; }
    private static World Seeded(int unrest = 0) { var w = Rings(unrest); w.Turn = 1; NationSystem.Seed(w, Content.Nations); return w; }

    [Fact]
    public void TheNationsLoad_AndAreSeededIntoTheWorld()
    {
        Assert.Equal(5, Content.Nations.Defs.Count);
        var concord = Content.Nations.Def("concord")!; Assert.Equal("aggressive", concord.Temper); Assert.Equal(26, concord.Strength); Assert.Equal(3, concord.Distance); Assert.Equal(10, concord.Threshold);
        var w = Seeded(); Assert.Equal(5, w.Nations.Count); Assert.Equal(-40, w.Nation("vashti")!.Disposition); Assert.Equal(12, w.Nation("thessar")!.Strength);
        NationSystem.Seed(w, Content.Nations); Assert.Equal(5, w.Nations.Count);   // idempotent
        Assert.NotNull(Content.Motions.Def("foreign_war")); Assert.Equal("nation", Content.Motions.Def("foreign_war")!.Target);
    }

    [Fact]
    public void TheWarMotion_IsGuarded_AndItsPassingDeclaresTheWar()
    {
        var w = Seeded(); var fs = Fleets(); var senate = Senate(fs); var ctx = new CommandContext(w, TestContent.Rng());
        var self = w.Char("self")!; self.CareerTrack = "military"; self.CareerRank = 3; w.Pools["self"].Influence = 9; w.House("vega")!.Title = "baron";
        Assert.Equal("no such nation", new ProposeMotionCommand("self", "foreign_war", "nowhere", senate).Refusal(ctx));
        var cmd = new ProposeMotionCommand("self", "foreign_war", "vashti", senate);
        Assert.Null(cmd.Refusal(ctx)); cmd.Execute(ctx);
        var m = w.Motions.Single(x => x.Open); Assert.Equal("foreign_war", m.Kind); Assert.Equal("vashti", m.TargetId);
        Assert.Contains("The Vashti Khanates", senate.Describe(w, m));
        w.House("corwin")!.Title = "baron"; w.Pools["corwin_head"].Influence = 9;
        Assert.Equal("a war is already before the Senate", new ProposeMotionCommand("corwin_head", "foreign_war", "concord", senate).Refusal(ctx));
        // everyone votes for it and the war is on
        foreach (var h in w.Houses.Keys) m.Votes[h] = 1;
        w.Turn++; senate.Tick(w);
        var n = w.Nation("vashti")!; Assert.True(n.AtWar); Assert.Equal(1, n.Wars); Assert.Equal(1, w.Counter(NationSystem.WarsCounter)); Assert.True(w.HasFlag("foreign_war_declared"));
        Assert.Equal(-60, n.Disposition);
        Assert.StartsWith("the Reach is already at war", new ProposeMotionCommand("corwin_head", "foreign_war", "vashti", senate).Refusal(ctx));
        Assert.StartsWith("the Reach fights one war abroad at a time", new ProposeMotionCommand("corwin_head", "foreign_war", "concord", senate).Refusal(ctx));
    }

    [Fact]
    public void TheLean_FollowsHullsHatredAndHomeWars()
    {
        var w = Seeded(); var fs = Fleets(); var senate = Senate(fs); var vega = w.House("vega")!;
        Assert.Equal(-1, senate.ForeignWarLean(w, vega, "compact"));                   // no hulls, no quarrel
        At(w, "f", "vega", "tessaly", 3); Assert.Equal(1, senate.ForeignWarLean(w, vega, "compact"));
        Assert.Equal(2, senate.ForeignWarLean(w, vega, "vashti"));                     // hated raiders
        Assert.Equal(0, senate.ForeignWarLean(w, vega, "thessar"));                    // the merchants are no one's enemy
        w.Wars.Add(new War { HouseA = "vega", HouseB = "corwin", Since = 1 }); Assert.Equal(0, senate.ForeignWarLean(w, vega, "vashti"));   // a war at home first
        w.Wars.Clear(); NationSystem.DeclareWar(w, Content.Nations, "compact", "aurelian"); Assert.Equal(-1, senate.ForeignWarLean(w, vega, "vashti"));   // one war at a time
    }

    [Fact]
    public void AnExpedition_LeavesTheMap_FightsAndComesHome()
    {
        var w = Seeded(); var fs = Fleets(); var ns = Nations(fs); var ctx = new CommandContext(w, TestContent.Rng());
        var f = At(w, "f", "vega", "tessaly", 8); w.Pools["self"].Treasury = 2; w.House("vega")!.Treasury.Credits = 500; w.House("vega")!.Treasury.Helium3 = 60;   // paid and fuelled for the voyage
        var send = new SendExpeditionCommand("self", "f", "compact", Content.Fleets, Content.Nations);
        Assert.Equal("the Reach is not at war with The Outer Compact", send.Refusal(ctx));
        NationSystem.DeclareWar(w, Content.Nations, "compact", "vega");
        f.Supply = 30; Assert.StartsWith("short of supply", send.Refusal(ctx)); f.Supply = 100;
        f.Strength = 1; Assert.StartsWith("too few hulls", send.Refusal(ctx)); f.Strength = 8;
        Assert.Null(send.Refusal(ctx)); send.Execute(ctx);
        Assert.True(f.IsAway); Assert.Equal("out", f.AwayPhase); Assert.Equal(4, f.AwayTurns); Assert.Equal("tessaly", f.HomeSystemId); Assert.Equal("", f.SystemId);
        Assert.Equal(0, FleetSystem.StrengthAt(w, "vega", "tessaly")); Assert.DoesNotContain(GalaxyProjection.Fleets(w), x => x.FleetId == "f");
        Assert.Equal("already beyond the Reach", send.Refusal(ctx)); Assert.True(w.HasFlag("expedition_sent"));
        // four turns out, a season of four fights, four turns home
        for (int t = 0; t < 4; t++) { w.Turn++; fs.Tick(w); ns.Tick(w); }
        Assert.Equal("fighting", f.AwayPhase); Assert.True(w.HasFlag("expedition_away"));
        var n = w.Nation("compact")!; int before = n.Strength;
        for (int t = 0; t < 4; t++) { w.Turn++; fs.Tick(w); ns.Tick(w); }
        Assert.True(w.Counter(NationSystem.BattlesCounter) >= 1); Assert.True(n.ScoreReach + n.ScoreNation >= 2);
        Assert.True(f.AwayPhase == "home" || !f.IsAway);
        int credits = w.House("vega")!.Treasury.Credits; int plunder = f.Plunder;
        for (int t = 0; t < 8 && f.IsAway; t++) { w.Turn++; fs.Tick(w); ns.Tick(w); }
        Assert.False(f.IsAway); Assert.Equal("tessaly", f.SystemId);
        if (plunder > 0) { Assert.Contains(w.History, h => h.Text.Contains($"with {plunder} credits of plunder")); Assert.True(w.House("vega")!.Treasury.Credits > credits - 40); }   // paid in, less the voyage's upkeep
        if (plunder > 0) Assert.True(n.Strength < before);
        Assert.True(w.History.Any(h => h.Text.Contains("comes home from The Outer Compact")));
    }

    [Fact]
    public void ABeatenNation_SuesForPeace_AndPaysTribute_ThenGoesFree()
    {
        var w = Seeded(); var fs = Fleets(); var ns = Nations(fs);
        var f = At(w, "f", "vega", "tessaly", 6); NationSystem.DeclareWar(w, Content.Nations, "hollow_crown", "vega");
        var n = w.Nation("hollow_crown")!; n.Strength = 0;   // its fleets are gone already: the expedition sweeps unopposed
        f.Away = "hollow_crown"; f.AwayPhase = "fighting"; f.AwayTurns = 4; f.HomeSystemId = "tessaly"; f.SystemId = "";
        w.WorldCounters[NationSystem.WinsKey("vega", "hollow_crown")] = 2;
        int legit = w.House("vega")!.Legitimacy; var chair = w.House("aurelian")!; int chairCredits = chair.Treasury.Credits;
        w.Turn++; ns.Tick(w);
        Assert.False(n.AtWar); Assert.True(n.Subdued(w.Turn)); Assert.Equal(w.Turn + NationSystem.SubduedTurns, n.SubduedUntil);
        Assert.True(w.HasFlag("nation_subdued")); Assert.True(w.HasFlag("triumph")); Assert.Equal(legit + NationSystem.LegitimacyTriumph, w.House("vega")!.Legitimacy);
        Assert.Equal(1, w.Counter(NationSystem.SubduedCounter)); Assert.Equal("home", f.AwayPhase);
        Assert.Equal(chairCredits + 5, chair.Treasury.Credits);   // the first year's tribute, to the chair
        for (int t = 0; t < NationSystem.SubduedTurns - 1; t++) { w.Turn++; ns.Tick(w); }
        Assert.Equal(chairCredits + 5 * NationSystem.SubduedTurns, chair.Treasury.Credits); Assert.True(w.HasFlag("nation_tributary"));
        w.Turn++; ns.Tick(w);
        Assert.False(n.Subdued(w.Turn)); Assert.True(w.HasFlag("nation_freed")); Assert.Equal(-5, n.Disposition); Assert.Equal(5 * NationSystem.SubduedTurns, w.Counter(NationSystem.TributeCounter));
    }

    [Fact]
    public void TheNationsScore_LosesTheWar_ForTheChair()
    {
        var w = Seeded(); var fs = Fleets(); var ns = Nations(fs); NationSystem.DeclareWar(w, Content.Nations, "concord", "vega");
        var n = w.Nation("concord")!; n.ScoreNation = 10; var chair = w.House("aurelian")!; chair.Treasury.Credits = 100; int legit = chair.Legitimacy;
        w.Turn++; ns.Tick(w);
        Assert.False(n.AtWar); Assert.True(w.HasFlag("war_lost_abroad")); Assert.Equal(100 - 18, chair.Treasury.Credits); Assert.Equal(legit - NationSystem.LegitimacyDefeat, chair.Legitimacy); Assert.Equal(1, w.Counter(NationSystem.LostCounter));
    }

    [Fact]
    public void AWarNoOneFights_GuttersOut()
    {
        var w = Seeded(); var fs = Fleets(); var ns = Nations(fs); NationSystem.DeclareWar(w, Content.Nations, "thessar", "vega");
        for (int t = 0; t < NationSystem.ExhaustionTurns - 1; t++) { w.Turn++; ns.Tick(w); Assert.True(w.Nation("thessar")!.AtWar); }
        w.Turn++; ns.Tick(w); Assert.False(w.Nation("thessar")!.AtWar); Assert.True(w.HasFlag("foreign_peace")); Assert.False(w.HasFlag("foreign_war"));
    }

    [Fact]
    public void TheAggressive_RaidTheRim_AndTheFleetInOrbitTurnsThemBack()
    {
        var w = Seeded(); var fs = Fleets(); var ns = Nations(fs); var barony = w.Holding("barony")!; int unrest = barony.Unrest;
        foreach (var n in w.Nations) if (n.Id != "vashti") n.Disposition = 50;   // only the khans are sour
        int turns = 0; while (w.Counter(NationSystem.RaidsCounter) == 0 && turns++ < 60) { w.Turn++; ns.Tick(w); }
        Assert.True(w.Counter(NationSystem.RaidsCounter) >= 1); Assert.True(w.History.Any(h => h.Text.Contains("Raiders of The Vashti Khanates fall on")));
        Assert.True(barony.Unrest > unrest || w.Holding("corwin_world")!.Unrest > 0 || w.Holding("prime_seat")!.Unrest > 0);
        // a fleet of three in orbit over the only rim world turns the next raid back
        var w2 = Seeded(); var ns2 = Nations(Fleets()); foreach (var n in w2.Nations) if (n.Id != "vashti") n.Disposition = 50; At(w2, "guard", "vega", "tessaly", 3);
        turns = 0; while (!w2.History.Any(h => h.Text.Contains("Raiders of The Vashti Khanates")) && turns++ < 60) { w2.Turn++; ns2.Tick(w2); }
        Assert.True(w2.History.Any(h => h.Text.Contains("they turn back"))); Assert.Equal(0, w2.Counter(NationSystem.RaidsCounter));
    }

    [Fact]
    public void StrengthRegrows_RegardDrifts_AndTheRecallBringsThemHome()
    {
        var w = Seeded(); var fs = Fleets(); var ns = Nations(fs); var ctx = new CommandContext(w, TestContent.Rng());
        var compact = w.Nation("compact")!; compact.Strength = 3; var thessar = w.Nation("thessar")!; thessar.Disposition = 0; var concord = w.Nation("concord")!; concord.Disposition = 0;
        for (int t = 0; t < 15; t++) { w.Turn++; ns.Tick(w); }
        Assert.Equal(6, compact.Strength);                 // +1 at turns 5, 10, 15
        Assert.True(thessar.Disposition > 0 && concord.Disposition < 0);   // the merchants warm, the Concord sours
        NationSystem.DeclareWar(w, Content.Nations, "compact", "vega"); var f = At(w, "f", "vega", "tessaly", 4); w.Pools["self"].Treasury = 1;
        Assert.Equal("not abroad", new RecallExpeditionCommand("self", "f", Content.Nations).Refusal(ctx));
        new SendExpeditionCommand("self", "f", "compact", Content.Fleets, Content.Nations).Execute(ctx);
        var recall = new RecallExpeditionCommand("self", "f", Content.Nations); Assert.Null(recall.Refusal(ctx)); recall.Execute(ctx);
        Assert.Equal("home", f.AwayPhase); Assert.Equal("already coming home", recall.Refusal(ctx));
        for (int t = 0; t < 4; t++) { w.Turn++; ns.Tick(w); }
        Assert.False(f.IsAway); Assert.Equal("tessaly", f.SystemId); Assert.True(w.HasFlag("expedition_returned"));
    }

    [Fact]
    public void TheNpcHawks_SendAFleet_WhenTheReachIsAtWar()
    {
        var w = Seeded(); var fs = Fleets(); var ctx = new CommandContext(w, TestContent.Rng());
        var head = w.Char("aurelian_head")!; head.CareerTrack = "military"; head.CareerRank = 2; w.Pools["aurelian_head"].Treasury = 3;
        At(w, "a1", "aurelian", "prime", 4); At(w, "a2", "aurelian", "prime", 2);
        var ai = new NpcAi { ShipClasses = Content.ShipClasses, Titles = Content.Titles, Policies = Content.Policies, Nations = Content.Nations };
        var exec = new CommandExecutor();
        ai.Act(exec, ctx, fleetRules: Content.Fleets);
        Assert.False(w.Fleets["a1"].IsAway);   // no war
        NationSystem.DeclareWar(w, Content.Nations, "vashti", "corwin");
        ai.Act(exec, ctx, fleetRules: Content.Fleets);
        Assert.True(w.Fleets["a1"].IsAway); Assert.Equal("vashti", w.Fleets["a1"].Away); Assert.False(w.Fleets["a2"].IsAway);
        ai.Act(exec, ctx, fleetRules: Content.Fleets); Assert.False(w.Fleets["a2"].IsAway);   // one abroad at a time
    }
}
