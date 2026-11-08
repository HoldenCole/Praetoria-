using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The Senate as a place of action (GDD §6.1; GAP §10 item 2): motions, weights, leans, lobbying, resolution and its effects.</summary>
public class SenateTests
{
    private static World Rings(int unrest) { var m = typeof(RegionSystemTests).GetMethod("Rings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!; return (World)m.Invoke(null, new object[] { unrest })!; }
    private static readonly MotionCatalog Motions = MotionCatalog.LoadFromDirectory(ContentLocator.FindContentDir());
    private static readonly Praetoria.Core.Progression.TitleCatalog Titles = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir()).Titles;

    private static (World w, SenateSystem senate, CommandContext ctx) Chamber()
    {
        var w = Rings(0); w.Turn = 5;
        w.House("vega")!.Title = "baron"; w.House("vega")!.Legitimacy = 40; w.House("aurelian")!.SphereInfluence["senate"] = 4; w.House("corwin")!.SphereInfluence["senate"] = 1;
        w.Pools["self"].Influence = 6; w.House("vega")!.Treasury.Credits = 40;
        return (w, new SenateSystem(Motions, Titles), new CommandContext(w, TestContent.Rng()));
    }

    [Fact]
    public void TheCatalogLoads_AndWeightIsInfluencePlusTitle()
    {
        Assert.Equal(7, Motions.Defs.Count); Assert.NotNull(Motions.Def("censure")); Assert.True(Motions.Def("commission")!.Office); Assert.True(Motions.Def("peace")!.TargetMustBeAtWar);
        var (w, senate, _) = Chamber();
        Assert.Equal(4 + Titles.RankOf("duke"), senate.Weight(w, "aurelian"));       // influence + title
        Assert.Equal(Titles.RankOf("baron"), senate.Weight(w, "vega"));
        Assert.True(senate.MayPropose(w, "vega")); Assert.False(senate.MayPropose(w, "corwin"));   // Corwin is landless-ranked with no magistrate
        w.Char("corwin_head")!.CareerTrack = "law"; w.Char("corwin_head")!.CareerRank = 3; Assert.True(senate.MayPropose(w, "corwin"));
    }

    [Fact]
    public void AMotion_IsVotedNextSession_AndATargetVotesAgainstItsOwnCensure()
    {
        var (w, senate, ctx) = Chamber(); var ex = new CommandExecutor();
        var move = new ProposeMotionCommand("self", "censure", "corwin", senate);
        Assert.Null(move.Refusal(ctx)); Assert.True(ex.TryExecute(move, ctx));
        Assert.Equal(4, w.Pools["self"].Influence); var m = SenateSystem.Docket(w).Single();
        Assert.Equal("you already have a motion before the Senate", new ProposeMotionCommand("self", "censure", "corwin", senate).Refusal(ctx));
        Assert.True(senate.Lean(w, m, "corwin") < 0);                                  // the target votes against itself
        Assert.Equal(1, m.Votes["vega"]);
        senate.Tick(w);                                                                 // same turn: too soon
        Assert.True(m.Open); Assert.True(w.HasFlag("motion_open"));
        w.Turn++; int before = w.House("corwin")!.Legitimacy;
        w.Relationship("aurelian_head", "corwin_head").Disposition = -40;               // the duke dislikes Corwin: his four weigh in
        senate.Tick(w);
        Assert.True(m.Resolved); Assert.True(m.Passed, $"for {m.ForWeight} against {m.AgainstWeight}");
        Assert.Equal(before - 6, w.House("corwin")!.Legitimacy);
        Assert.Equal(1, w.Counter("senate_standing:vega")); Assert.Equal(1, w.Counter("motions_passed")); Assert.True(w.HasFlag("motion_passed_mine"));
        Assert.Contains(w.History, h => h.Text.StartsWith("The Senate carries it"));
    }

    [Fact]
    public void Lobbying_LeansAHouse_AndABribeWorksOnAnEnemy()
    {
        var (w, senate, ctx) = Chamber(); var ex = new CommandExecutor();
        ex.TryExecute(new ProposeMotionCommand("self", "levy", "", senate), ctx); var m = SenateSystem.Docket(w).Single();
        w.Relationship("corwin_head", "self").Disposition = -40;
        int lean0 = senate.Lean(w, m, "corwin");
        Assert.Equal("Mara will not hear you — try coin", new LobbyCommand("self", m.Id, "corwin", 1).Refusal(ctx));
        Assert.True(ex.TryExecute(new LobbyCommand("self", m.Id, "corwin", 1, bribe: true), ctx));
        Assert.Equal(35, w.House("vega")!.Treasury.Credits); Assert.Equal(lean0 + SenateSystem.BribeWeight, senate.Lean(w, m, "corwin"));
        Assert.Equal("you have already had their ear", new LobbyCommand("self", m.Id, "corwin", 1).Refusal(ctx));
        Assert.True(ex.TryExecute(new LobbyCommand("self", m.Id, "aurelian", -1), ctx)); Assert.Equal(3, w.Pools["self"].Influence);
        Assert.True(ex.TryExecute(new VoteCommand("self", m.Id, -1), ctx)); Assert.Equal(-1, m.Votes["vega"]);
    }

    [Fact]
    public void TheLevy_PaysTheChair_AndAnEmbargo_ClosesTheExchanges()
    {
        var (w, senate, ctx) = Chamber(); var ex = new CommandExecutor();
        ex.TryExecute(new ProposeMotionCommand("self", "levy", "", senate), ctx); var m = SenateSystem.Docket(w).Single();
        foreach (var h in w.Houses.Values) m.Votes[h.Id] = 1;                           // unanimous
        int chair = w.House("aurelian")!.Treasury.Credits, mine = w.House("vega")!.Treasury.Credits;
        w.Turn++; senate.Tick(w);
        Assert.True(m.Passed); Assert.Equal(mine - 3, w.House("vega")!.Treasury.Credits); Assert.True(w.House("aurelian")!.Treasury.Credits > chair); Assert.True(w.HasFlag("levied"));
        var e = senate.Add(w, "aurelian", "embargo", "vega"); foreach (var h in w.Houses.Values) e.Votes[h.Id] = 1;
        w.Turn++; senate.Tick(w);
        Assert.True(SenateSystem.Embargoed(w, "vega")); Assert.True(w.HasFlag("embargoed"));
        Assert.Equal("the Senate's embargo closes the exchanges to you", new ProcureCommand("self", Resource.Helium3, 1, TestContent.EconomyCatalog(), BusinessCatalog.Empty).Refusal(ctx));
        w.Turn += 9; senate.Tick(w); Assert.False(SenateSystem.Embargoed(w, "vega"));
    }

    [Fact]
    public void ThePeaceOfTheReach_EndsATargetsWars_AndRivalsBringTheirOwnBusiness()
    {
        var (w, senate, ctx) = Chamber();
        var fleets = new FleetSystem(new FleetRules(), TestContent.EconomyCatalog()); senate = new SenateSystem(Motions, Titles, fleets);
        w.Wars.Add(new War { HouseA = "corwin", HouseB = "aurelian", ScoreA = 0, ScoreB = 5 });
        Assert.Equal("House Vega is at peace", new ProposeMotionCommand("self", "peace", "vega", senate).Refusal(ctx));
        var m = senate.Add(w, "corwin", "peace", "corwin"); foreach (var h in w.Houses.Values) m.Votes[h.Id] = 1;
        w.Turn++; senate.Tick(w);
        Assert.True(m.Passed); Assert.Empty(w.Wars);
        // a losing belligerent moves for peace on its own: the Senate hears rivals too
        w.Wars.Add(new War { HouseA = "aurelian", HouseB = "corwin", ScoreA = 0, ScoreB = 6 });
        bool proposed = false;
        for (int t = 0; t < 6 && !proposed; t++) { w.Turn++; senate.Tick(w); proposed = w.Motions.Any(x => x.ProposerHouseId == "aurelian" && x.Kind == "peace"); }
        Assert.True(proposed, "the losing duke never moved for peace");
    }

    [Fact]
    public void TheSenate_SurvivesASave_AndFeedsTheSphere()
    {
        var (w, senate, ctx) = Chamber(); new CommandExecutor().TryExecute(new ProposeMotionCommand("self", "recognition", "vega", senate), ctx);
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        Assert.Single(back.Motions); Assert.Equal("recognition", back.Motions[0].Kind); Assert.Equal(1, back.Motions[0].Votes["vega"]);
        w.WorldCounters["senate_standing:vega"] = 3;
        var spheres = new SphereSystem(ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir()).Spheres); spheres.Recompute(w);
        Assert.Equal(3, w.House("vega")!.SphereInfluence["senate"]);
    }
}
