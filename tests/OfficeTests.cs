using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The offices of the Reach (GDD §8): commissions, the office fleets and who pays for them, treason, stipends, the Speaker's gavel, the Emperor.</summary>
public class OfficeTests
{
    private static World Rings(int unrest) { var m = typeof(RegionSystemTests).GetMethod("Rings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!; return (World)m.Invoke(null, new object[] { unrest })!; }
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());

    private static (World w, SenateSystem senate, FleetSystem fleets, CommandContext ctx) Chamber()
    {
        var w = Rings(0); w.Turn = 5;
        w.House("vega")!.Title = "baron"; w.House("vega")!.Legitimacy = 40; w.Pools["self"].Influence = 6;
        var self = w.Char("self")!; self.CareerTrack = "military"; self.CareerRank = 3;                      // a Commander: qualifies for the Admiralty
        w.Fleets["reach_squadron"] = new Fleet { Id = "reach_squadron", Name = "The Reach Squadron", OwnerId = "aurelian", SystemId = "prime", Strength = 4, Office = true };
        w.Fleets["vega_guard"] = new Fleet { Id = "vega_guard", Name = "Vega Guard", OwnerId = "vega", SystemId = "tessaly", Strength = 2 };
        var fleets = new FleetSystem(new FleetRules(), TestContent.EconomyCatalog());
        return (w, new SenateSystem(Content.Motions, Content.Titles, fleets, Content.Offices), fleets, new CommandContext(w, TestContent.Rng()));
    }

    [Fact]
    public void TheCatalogLoads_AndOnlyAHouseWithTheRank_MayBeCommissioned()
    {
        Assert.Equal(5, Content.Offices.Defs.Count); Assert.True(Content.Offices.Def("admiral")!.CommandsFleets); Assert.Equal(3, Content.Offices.Def("speaker")!.Weight);
        var (w, senate, _, ctx) = Chamber();
        Assert.Equal("self", OfficeSystem.Candidate(w, Content.Offices.Def("admiral")!, "vega")!.Id);
        Assert.StartsWith("no one of House Vega has the rank", new ProposeMotionCommand("self", "commission", "vega", senate, "treasurer").Refusal(ctx));
        Assert.Equal("name an office", new ProposeMotionCommand("self", "commission", "vega", senate, "").Refusal(ctx));
        Assert.Null(new ProposeMotionCommand("self", "commission", "vega", senate, "admiral").Refusal(ctx));
    }

    [Fact]
    public void ACommission_GrantsTheAdmiralty_AndTheOfficeFleetsSailUnderTheHoldersColours_WhileTheChairPays()
    {
        var (w, senate, fleets, ctx) = Chamber(); var ex = new CommandExecutor();
        Assert.True(ex.TryExecute(new ProposeMotionCommand("self", "commission", "vega", senate, "admiral"), ctx));
        var m = SenateSystem.Docket(w).Single(); Assert.Equal("admiral", m.OfficeId); Assert.Contains("Admiral of the Reach", senate.Describe(w, m));
        foreach (var h in w.Houses.Values) m.Votes[h.Id] = 1;
        w.Turn++; senate.Tick(w);
        Assert.True(m.Passed); Assert.True(OfficeSystem.Holds(w, "vega", "admiral")); Assert.Equal("self", OfficeSystem.Holding(w, "admiral")!.CharacterId);
        Assert.Equal("vega", w.Fleets["reach_squadron"].OwnerId); Assert.Contains("office:admiral", w.Char("self")!.Flags); Assert.True(w.HasFlag("holds_office:admiral"));
        Assert.Equal(42 + 2, w.House("vega")!.Legitimacy);                                                 // the commission's +2 and the grant's +2
        // the chair pays the squadron's upkeep; the state fuels it
        Assert.Equal("aurelian", FleetSystem.BilledTo(w.Fleets["reach_squadron"], "aurelian")); Assert.Equal("vega", FleetSystem.BilledTo(w.Fleets["vega_guard"], "aurelian"));
        Assert.Equal(1, FleetSystem.FuelBillFor(w, "vega", 3));                                              // two hulls of its own burn one; the four office hulls are the state's
        var lg = new Economy(TestContent.EconomyCatalog(), BusinessCatalog.Empty).Compute(w, "aurelian", fleets);
        Assert.Contains(lg.Lines, l => l.Source == "The Reach Squadron (office)");
        Assert.DoesNotContain(new Economy(TestContent.EconomyCatalog(), BusinessCatalog.Empty).Compute(w, "vega", fleets).Lines, l => l.Source.Contains("Reach Squadron"));
    }

    [Fact]
    public void WarOnTheChair_IsTreason_AndTheFleetsGoBack()
    {
        var (w, senate, _, _) = Chamber();
        OfficeSystem.Grant(w, Content.Offices.Def("admiral")!, "vega", w.Char("self")!);
        w.Wars.Add(new War { HouseA = "vega", HouseB = "aurelian" }); int legit = w.House("vega")!.Legitimacy;
        w.Turn++; senate.Tick(w);
        Assert.Null(OfficeSystem.Holding(w, "admiral")); Assert.Equal("aurelian", w.Fleets["reach_squadron"].OwnerId);
        Assert.Equal(legit - OfficeSystem.TreasonLegitimacy, w.House("vega")!.Legitimacy); Assert.True(w.HasFlag("treason")); Assert.True(w.HasFlag("office_lost")); Assert.True(w.HasFlag("office_vacant:admiral"));
        Assert.Contains(w.History, h => h.Text.Contains("treason"));
    }

    [Fact]
    public void TheTreasurer_DrawsAStipend_TheSpeakerWeighsMore_AndADeadHolderPassesItOn()
    {
        var (w, senate, _, ctx) = Chamber();
        var head = w.Char("corwin_head")!; head.CareerTrack = "stewardship"; head.CareerRank = 4;
        OfficeSystem.Grant(w, Content.Offices.Def("treasurer")!, "corwin", head);
        int chair = w.House("aurelian")!.Treasury.Credits, corwin = w.House("corwin")!.Treasury.Credits;
        w.Turn++; senate.Tick(w);
        Assert.Equal(chair - 3, w.House("aurelian")!.Treasury.Credits); Assert.Equal(corwin + 3, w.House("corwin")!.Treasury.Credits);
        var self = w.Char("self")!; self.CareerTrack = "law"; self.CareerRank = 4;
        int before = senate.Weight(w, "vega"); OfficeSystem.Grant(w, Content.Offices.Def("speaker")!, "vega", self);
        Assert.Equal(before + 3, senate.Weight(w, "vega"));
        Assert.Equal(1, new ProposeMotionCommand("self", "levy", "", senate).Cost(w, "vega", Content.Motions.Def("levy")!));   // the gavel's discount
        // the holder dies: a kinsman of the rank takes it up, else it falls vacant
        head.Alive = false; w.Turn++; senate.Tick(w);
        Assert.Null(OfficeSystem.Holding(w, "treasurer")); Assert.True(w.HasFlag("office_vacant:treasurer"));
    }

    [Fact]
    public void TheEmperor_HoldsEveryOffice_AndAFallenHouse_VacatesIts()
    {
        var (w, senate, _, _) = Chamber();
        OfficeSystem.Grant(w, Content.Offices.Def("admiral")!, "vega", w.Char("self")!);
        w.Era = "imperium"; w.House("aurelian")!.Title = "emperor";
        w.Turn++; senate.Tick(w);
        Assert.True(OfficeSystem.Holds(w, "aurelian", "admiral")); Assert.True(OfficeSystem.Holds(w, "aurelian", "speaker")); Assert.Equal("aurelian", w.Fleets["reach_squadron"].OwnerId);
        var sp = new SphereSystem(Content.Spheres, 1.0, Content.Offices); sp.Recompute(w);
        Assert.True(w.House("aurelian")!.SphereInfluence["navy"] >= 3);                                        // the office's weight in its sphere
        w.Era = "fractured_stars"; w.House("aurelian")!.Title = "duke";
        w.Houses["ghost"] = new House { Id = "ghost", Name = "House Ghost" }; w.Offices.Clear(); w.Offices.Add(new OfficeHolding { OfficeId = "admiral", HouseId = "ghost", CharacterId = "x" }); w.Fleets["reach_squadron"].OwnerId = "ghost";
        OfficeSystem.Vacate(w, Content.Offices, "ghost");                                                       // the office and its fleets leave a house that is no more
        Assert.Empty(w.Offices); Assert.Equal("aurelian", w.Fleets["reach_squadron"].OwnerId);
        HouseSystem.Tick(w); Assert.False(w.Houses.ContainsKey("ghost"));                                        // and the empty name is struck
    }

    [Fact]
    public void TheReach_SailsAnOfficeSquadron_AtTheCapital()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_fallen_house"), 1);
        var sq = w.Fleets.Values.Single(f => f.Office);
        Assert.Equal("aurelia_prime", sq.SystemId); Assert.Equal("aurelian", sq.OwnerId); Assert.Equal(4, sq.Strength);
        Assert.Empty(w.Offices);                                                                               // vacant until the Senate commissions
    }
}
