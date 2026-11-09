using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Military depth, stage 1 (docs/MILITARY.md): stances and their edges, torpedoes on isotopes, boarding prizes, commanders at the front
/// and the flag, the command cap, supply lines and depots, campaigns, and the crews' loyalty.</summary>
public class MilitaryDepthTests
{
    private static World Rings(int unrest) { var m = typeof(RegionSystemTests).GetMethod("Rings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!; return (World)m.Invoke(null, new object[] { unrest })!; }
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static FleetSystem Fleets() => new(Content.Fleets, Content.Holdings, Content.ShipClasses);
    private static Fleet At(World w, string id, string owner, string sys, int hulls, string admiral = "") { var f = new Fleet { Id = id, Name = id, OwnerId = owner, SystemId = sys, Strength = hulls, AdmiralId = admiral }; w.Fleets[id] = f; return f; }

    [Fact]
    public void TheRulesLoad_AndTheStanceWheelTurns()
    {
        var r = Content.Fleets; Assert.Equal(0.15, r.StanceEdge); Assert.Equal(2, r.BoardingManpower); Assert.Equal(8, r.CampaignTurns); Assert.Equal(30, r.LoyaltyRefuse);
        Assert.Equal(1, FleetSystem.StanceEdgeOf("line", "press")); Assert.Equal(1, FleetSystem.StanceEdgeOf("press", "skirmish")); Assert.Equal(1, FleetSystem.StanceEdgeOf("skirmish", "line"));
        Assert.Equal(-1, FleetSystem.StanceEdgeOf("press", "line")); Assert.Equal(0, FleetSystem.StanceEdgeOf("line", "line")); Assert.Equal(1, FleetSystem.StanceEdgeOf("line", "boarding"));
        Assert.NotNull(Content.Holdings.Building("supply_depot"));
    }

    [Fact]
    public void Stances_Torpedoes_TheFront_TheCap_SupplyAndLoyalty_AllMoveTheFleetsPower()
    {
        var w = Rings(0); var fs = Fleets(); var f = At(w, "f", "vega", "tessaly", 4);
        double baseP = fs.Power(w, "vega", "tessaly", false);
        f.Stance = "press"; Assert.Equal(baseP * 1.15, fs.Power(w, "vega", "tessaly", false), 6);
        f.Stance = "skirmish"; Assert.Equal(baseP * 0.9, fs.Power(w, "vega", "tessaly", false), 6); f.Stance = "line";
        f.Loadout = "torpedoes"; Assert.Equal(baseP, fs.Power(w, "vega", "tessaly", false), 6);                 // no isotopes: the tubes are empty
        w.House("vega")!.Treasury.Isotopes = 1; Assert.Equal(baseP * 1.25, fs.Power(w, "vega", "tessaly", false), 6); f.Loadout = "guns"; w.House("vega")!.Treasury.Isotopes = 0;
        var self = w.Char("self")!; self.CareerTrack = "military"; self.CareerRank = 1; f.AdmiralId = "self";
        double led = fs.Power(w, "vega", "tessaly", false);
        self.Flags.Add(FleetSystem.FrontFlag); Assert.Equal(led * 1.10, fs.Power(w, "vega", "tessaly", false), 6);
        self.Flags.Add(FleetSystem.AchillesFlag); Assert.Equal(led * 1.25, fs.Power(w, "vega", "tessaly", false), 6); self.Flags.Remove(FleetSystem.AchillesFlag); self.Flags.Remove(FleetSystem.FrontFlag);
        Assert.Equal(5, fs.CommandCap(self)); f.Strength = 6; double big = fs.Power(w, "vega", "tessaly", false); self.CareerRank = 2; Assert.Equal(big / 0.9, fs.Power(w, "vega", "tessaly", false), 6);   // over the cap, then not
        f.Strength = 4; f.AdmiralId = "";
        f.Supply = 20; Assert.Equal(baseP * 0.8, fs.Power(w, "vega", "tessaly", false), 6); f.Supply = 100;
        f.Loyalty = 20; Assert.Equal(baseP * 0.9, fs.Power(w, "vega", "tessaly", false), 6); f.Loyalty = 70;
        self.Flags.Add(FleetSystem.FlagFlag); self.Skills["tactics"] = 4; Assert.Equal(baseP * 1.12, fs.Power(w, "vega", "tessaly", false), 6);   // the flag lends 3% a point
    }

    [Fact]
    public void TheMoveOrder_IsRefused_ByRestiveCrews_AndTheStanceOrders_AreGuarded()
    {
        var w = Rings(0); var f = At(w, "f", "vega", "tessaly", 3); var ctx = new CommandContext(w, TestContent.Rng()); w.Pools["self"].Treasury = 3;
        var move = new MoveFleetCommand("self", "f", "prime", Content.Fleets);
        Assert.True(move.CanExecute(ctx)); f.Loyalty = 20; Assert.False(move.CanExecute(ctx)); Assert.StartsWith("the crews will not sail", move.Refusal(ctx)); f.Loyalty = 70;
        Assert.Equal("boarding wants prize crews — 2 manpower in hand", new SetStanceCommand("self", "f", "boarding").Refusal(ctx));
        w.House("vega")!.Treasury.Manpower = 4; Assert.Null(new SetStanceCommand("self", "f", "boarding").Refusal(ctx)); new SetStanceCommand("self", "f", "press").Execute(ctx); Assert.Equal("press", f.Stance);
        Assert.Equal("no isotopes in store", new SetLoadoutCommand("self", "f", "torpedoes").Refusal(ctx)); w.House("vega")!.Treasury.Isotopes = 1; Assert.Null(new SetLoadoutCommand("self", "f", "torpedoes").Refusal(ctx));
        var self = w.Char("self")!; self.CareerTrack = "military"; self.CareerRank = 1;
        Assert.Equal("the flag is for a Commander or better", new SetCommandStyleCommand("self", "self", "flag").Refusal(ctx));
        new SetCommandStyleCommand("self", "self", "front").Execute(ctx); Assert.Equal("front", SetCommandStyleCommand.StyleOf(self));
    }

    [Fact]
    public void ABattle_ConsumesIsotopes_TakesPrizes_SwingsLoyalty_AndMarksTheFront()
    {
        var w = Rings(0); var fs = Fleets(); w.Wars.Add(new War { HouseA = "vega", HouseB = "corwin", Since = 1 });
        var mine = At(w, "mine", "vega", "mid1", 8, "self"); mine.Stance = "boarding"; mine.Loadout = "torpedoes";
        var theirs = At(w, "theirs", "corwin", "mid1", 4); theirs.Readiness = 10;
        var vega = w.House("vega")!; vega.Treasury.Isotopes = 2; vega.Treasury.Manpower = 5; w.Char("self")!.Flags.Add(FleetSystem.FrontFlag);
        fs.ResolveBattles(w, TestContent.Rng());
        Assert.Single(fs.LastBattles); Assert.Equal("vega", fs.LastBattles[0].Winner);
        Assert.Equal(1, vega.Treasury.Isotopes);                                          // a torpedo fleet burns one
        Assert.Equal(3, vega.Treasury.Manpower); Assert.True(w.HasFlag("hulls_captured")); Assert.True(mine.Strength >= 8);   // prize crews took a hull
        Assert.True(mine.Loyalty > 70); Assert.True(w.HasFlag("battle_won")); Assert.True(w.HasFlag("fought_at_the_front")); Assert.Equal(1, w.Counter(FleetSystem.ValourKey("self")));
    }

    [Fact]
    public void ThreeVictoriesAtTheFront_MakeAnAchilles_AndTheFlagNeverGoesDownWithTheLine()
    {
        var w = Rings(0); var fs = Fleets(); var self = w.Char("self")!; self.Flags.Add(FleetSystem.FrontFlag); w.WorldCounters[FleetSystem.ValourKey("self")] = 2;
        w.Wars.Add(new War { HouseA = "vega", HouseB = "corwin", Since = 1 }); At(w, "mine", "vega", "mid1", 9, "self"); At(w, "theirs", "corwin", "mid1", 1).Readiness = 5;
        fs.ResolveBattles(w, TestContent.Rng());
        Assert.Contains(FleetSystem.AchillesFlag, self.Flags); Assert.True(w.HasFlag("became_achilles"));
        // the flag: a losing admiral who commands from the rear is never killed, whatever the roll
        var w2 = Rings(0); var fs2 = Fleets(); var mara = w2.Char("corwin_head")!; mara.Flags.Add(FleetSystem.FlagFlag);
        w2.Wars.Add(new War { HouseA = "vega", HouseB = "corwin", Since = 1 }); At(w2, "big", "vega", "mid1", 12); At(w2, "small", "corwin", "mid1", 1, "corwin_head").Readiness = 5;
        for (int i = 0; i < 20 && mara.Alive; i++) { fs2.ResolveBattles(w2, TestContent.Rng((ulong)i)); if (!w2.Fleets.ContainsKey("small")) At(w2, "small", "corwin", "mid1", 1, "corwin_head"); }
        Assert.True(mara.Alive);
    }

    [Fact]
    public void Supply_DrainsBeyondTheLanes_DesertsAtNought_AndADepotOrACampaign_ReachesFurther()
    {
        var w = Rings(0); var fs = Fleets(); w.House("vega")!.Treasury.Helium3 = 10; w.House("vega")!.Treasury.Credits = 50;
        // the lanes run prime — mid1 — tessaly — empty; vega holds tessaly, so prime is two lanes out: beyond supply, but near enough that the drain is gentle
        var f = At(w, "f", "vega", "prime", 3); f.Supply = 13;
        fs.Tick(w); Assert.Equal(5, f.Supply); Assert.True(w.HasFlag("supply_low"));
        fs.Tick(w); Assert.Equal(0, f.Supply); Assert.True(w.HasFlag("out_of_supply"));
        fs.Tick(w); Assert.Equal(2, f.Strength); Assert.True(w.HasFlag("fleet_deserts"));
        Assert.True(fs.InSupply(w, "vega", "mid1")); Assert.False(fs.InSupply(w, "vega", "prime"));
        w.Holding("barony")!.Buildings.Add("supply_depot"); Assert.True(fs.InSupply(w, "vega", "prime"));   // a depot reaches two lanes
        w.Holding("barony")!.Buildings.Remove("supply_depot"); Assert.False(fs.InSupply(w, "vega", "prime"));
        w.WorldFlags.Add(FleetSystem.CampaignPrefix("vega") + "prime"); w.WorldCounters[FleetSystem.CampaignUntilKey("vega")] = w.Turn + 5;
        Assert.True(fs.InSupply(w, "vega", "prime")); Assert.Equal("prime", FleetSystem.CampaignTarget(w, "vega"));   // the campaign's object is supplied
        w.WorldCounters[FleetSystem.CampaignUntilKey("vega")] = w.Turn; fs.Tick(w); Assert.Null(FleetSystem.CampaignTarget(w, "vega")); Assert.True(w.HasFlag("campaign_over"));
    }

    [Fact]
    public void ACampaign_IsDeclaredOnAnEnemysWorld_ForInfluence_AndLiftsTheFleetsThere()
    {
        var w = Rings(0); var fs = Fleets(); var ctx = new CommandContext(w, TestContent.Rng()); w.Pools["self"].Influence = 4; At(w, "f", "vega", "tessaly", 3);
        Assert.Equal("you are not at war with House Corwin", new DeclareCampaignCommand("self", "mid1", Content.Fleets).Refusal(ctx));
        w.Wars.Add(new War { HouseA = "vega", HouseB = "corwin", Since = 1 });
        var cmd = new DeclareCampaignCommand("self", "mid1", Content.Fleets); Assert.Null(cmd.Refusal(ctx)); cmd.Execute(ctx);
        Assert.Equal(2, w.Pools["self"].Influence); Assert.Equal("mid1", FleetSystem.CampaignTarget(w, "vega")); Assert.True(w.HasFlag("campaign_declared"));
        Assert.Equal("a campaign is already under way", new DeclareCampaignCommand("self", "mid1", Content.Fleets).Refusal(ctx));
        w.Fleets["f"].SystemId = "mid1"; double with = fs.Power(w, "vega", "mid1", false);
        w.WorldCounters[FleetSystem.CampaignUntilKey("vega")] = 0; double without = fs.Power(w, "vega", "mid1", false);
        Assert.Equal(without * 1.10, with, 6);
    }

    [Fact]
    public void Loyalty_DriftsWithPayAndTheOfficers_AndMutinousCrewsDesert()
    {
        var w = Rings(0); var fs = Fleets(); var f = At(w, "f", "vega", "tessaly", 4); f.Loyalty = 50; w.House("vega")!.Treasury.Helium3 = 10; w.House("vega")!.Treasury.Credits = 50;
        fs.Tick(w); Assert.Equal(52, f.Loyalty);                                            // paid and supplied: +2
        w.House("vega")!.Treasury.Credits = -5; w.WorldCounters["estate:officers"] = -40; fs.Tick(w); Assert.Equal(48, f.Loyalty);   // unpaid −4, hostile officers −2, drift +2
        f.Loyalty = 10; int hulls = f.Strength; fs.Tick(w); Assert.Equal(hulls - 1, f.Strength); Assert.True(w.HasFlag("fleet_mutinous")); Assert.True(w.HasFlag("fleet_deserts"));
    }
}
