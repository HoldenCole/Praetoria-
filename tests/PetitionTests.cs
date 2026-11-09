using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The estates' petitions (GAP §16 item 8): an estate hostile across the Reach brings its own Act; heard, its regard rises in
/// every house; refused, it falls and its worlds stir.</summary>
public class PetitionTests
{
    private static World Rings(int unrest) { var m = typeof(RegionSystemTests).GetMethod("Rings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!; return (World)m.Invoke(null, new object[] { unrest })!; }
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static readonly MotionCatalog Motions = MotionCatalog.LoadFromDirectory(ContentLocator.FindContentDir());

    private static (World w, SenateSystem senate) Chamber()
    {
        var w = Rings(0); w.Turn = 5; w.House("vega")!.Title = "baron"; w.House("aurelian")!.SphereInfluence["senate"] = 4;
        return (w, new SenateSystem(Motions, Content.Titles, null, Content.Offices) { Policies = Content.Policies });
    }
    private static void Hostile(World w, string estate, params string[] houses) { foreach (var h in houses) w.WorldCounters[PolicySystem.ApprovalKey(w, h, estate)] = -50; }

    [Fact]
    public void NoPetition_WhileTheEstateIsHostileInTooFewHouses_AndAPlayerMayNotMoveOne()
    {
        var (w, senate) = Chamber(); Hostile(w, "merchants", "vega", "corwin");
        senate.Tick(w); Assert.DoesNotContain(w.Motions, m => m.Kind == "petition");
        Assert.Equal("only an estate brings a petition", new ProposeMotionCommand("self", "petition", "", senate, lawId: "trade_charter", optionId: "open_lanes").Refusal(new CommandContext(w, TestContent.Rng())));
    }

    [Fact]
    public void AnEstateHostileInThreeHouses_PetitionsForTheOptionThatPleasesIt_AndTheChamberNamesIt()
    {
        var (w, senate) = Chamber(); Hostile(w, "merchants", "vega", "corwin", "aurelian");
        Assert.Equal(("trade_charter", "open_lanes"), (senate.WantedBy(w, "merchants")!.Value.law.Id, senate.WantedBy(w, "merchants")!.Value.option.Id));
        senate.Tick(w);
        var m = Assert.Single(w.Motions, x => x.Kind == "petition");
        Assert.True(SenateSystem.IsPetition(m)); Assert.Equal("merchants", SenateSystem.PetitionEstate(m)); Assert.Equal("open_lanes", m.LawOption);
        Assert.Equal("The Merchants", senate.ProposerName(w, m)); Assert.StartsWith("The Merchants petition the Senate: that the trade charter be open lanes", senate.Describe(w, m));
        Assert.True(w.HasFlag("petition_open")); Assert.True(w.HasFlag("petition_open:merchants")); Assert.True(w.HasFlag("act_open"));
        Assert.Contains(w.History, h => h.Text.Contains("come to the Senate's door"));
        // the house at whose gate they stand leans a point further for them than it would for the same Act moved by a house
        var plain = new Motion { Id = "x", Kind = "act", ProposerHouseId = "aurelian", LawId = "trade_charter", LawOption = "open_lanes" };
        Assert.Equal(senate.ActLean(w, w.House("corwin")!, plain) + 1, senate.ActLean(w, w.House("corwin")!, m));
        // no second petition while one stands; no standing for an estate
        senate.Petition(w); Assert.Single(w.Motions, x => x.Kind == "petition");
    }

    [Fact]
    public void Heard_TheEstateWarmsEverywhere_AndTheLawOfTheReachMoves()
    {
        var (w, senate) = Chamber(); Hostile(w, "merchants", "vega", "corwin", "aurelian"); senate.Tick(w);
        var m = w.Motions.Single(x => x.Kind == "petition"); foreach (var h in w.Houses.Values) m.Votes[h.Id] = 1;
        w.Turn++; senate.Tick(w);
        Assert.True(m.Passed); Assert.True(w.HasFlag("petition_carried")); Assert.True(w.HasFlag("petition_carried:merchants")); Assert.Equal(1, w.Counter("petitions_heard"));
        Assert.Equal("open_lanes", PolicySystem.ReachOption(w, Content.Policies, Content.Policies.Law("trade_charter")!)!.Id);
        Assert.Equal(-50 + SenateSystem.PetitionSwing, PolicySystem.Approval(w, "corwin", "merchants"));
        Assert.Equal(SenateSystem.PetitionSwing, w.Counter("estate_offset:merchants"));          // the player's regard moves through the offset
        Assert.Equal(0, w.Counter("senate_standing:estate:merchants"));
        Assert.True(w.Counter("petition_cd:merchants") > w.Turn);
    }

    [Fact]
    public void Refused_TheEstateCools_AndTheWorldsOfEveryHouseItDistrustsStir()
    {
        var (w, senate) = Chamber(); Hostile(w, "merchants", "vega", "corwin", "aurelian"); senate.Tick(w);
        var m = w.Motions.Single(x => x.Kind == "petition"); foreach (var h in w.Houses.Values) m.Votes[h.Id] = -1;
        int unrest = w.Holding("corwin_world")!.Unrest;
        w.Turn++; senate.Tick(w);
        Assert.False(m.Passed); Assert.True(w.HasFlag("petition_refused")); Assert.Equal(1, w.Counter("petitions_refused"));
        Assert.Equal("customary", PolicySystem.ReachOption(w, Content.Policies, Content.Policies.Law("trade_charter")!)!.Id);
        Assert.Equal(-50 - SenateSystem.PetitionSpurn, PolicySystem.Approval(w, "corwin", "merchants"));
        Assert.Equal(unrest + SenateSystem.PetitionUnrest, w.Holding("corwin_world")!.Unrest);
        Assert.Equal(-SenateSystem.PetitionSpurn, w.Counter("estate_offset:merchants"));
    }

    [Fact]
    public void TheScreens_ShowThePetition_OnTheDocket_AndOnTheEstate()
    {
        var (w, senate) = Chamber(); Hostile(w, "merchants", "vega", "corwin", "aurelian"); senate.Tick(w);
        var tc = new TurnController(w, Content); var model = CourtProjection.Build(tc, Content);
        var card = Assert.Single(model.Senate!.Docket, c => c.Petition); Assert.Equal("The Merchants", card.ProposerName); Assert.Equal(SenateSystem.EstateAccent, card.ProposerAccent);
        Assert.DoesNotContain(model.Senate.Proposals, p => p.KindId == "petition");
        var est = model.Chancery!.Estates.Single(e => e.Id == "merchants"); Assert.Equal("petitioning the Senate: that the trade charter be open lanes", est.Petition); Assert.Equal(3, est.HostileIn);
    }
}

public class QolTests
{
    private static World Rings(int unrest) { var m = typeof(RegionSystemTests).GetMethod("Rings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!; return (World)m.Invoke(null, new object[] { unrest })!; }
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());

    [Fact]
    public void AStruckHouse_TakesItsMotionsOffTheDocket()
    {
        var w = Rings(0); w.Turn = 5; var senate = new SenateSystem(MotionCatalog.LoadFromDirectory(ContentLocator.FindContentDir()), Content.Titles) { Policies = Content.Policies };
        senate.Add(w, "corwin", "censure", "vega"); senate.Add(w, "aurelian", "recognition", "corwin");
        Assert.Equal(2, SenateSystem.Docket(w).Count());
        foreach (var id in w.House("corwin")!.Members.ToList()) w.Char(id)!.Alive = false; w.Holdings.Remove("corwin_world");   // no one left, nothing held
        HouseSystem.Tick(w);
        Assert.False(w.Houses.ContainsKey("corwin"));
        Assert.Empty(SenateSystem.Docket(w));   // its own motion lapses, and the one against it has no target
        Assert.Contains(w.History, h => h.Text.Contains("lapses"));
    }

    [Fact]
    public void TheChancery_TellsYouWhatAnEstateWouldWarmTo()
    {
        var w = Rings(0); w.Turn = 5; PolicySystem.Enact(w, Content.Policies, "vega", "tithe", "harsh"); w.WorldCounters[$"law_cd:vega:tithe"] = 0;
        var tc = new TurnController(w, Content); var model = CourtProjection.Build(tc, Content);
        var commons = model.Chancery!.Estates.Single(e => e.Id == "commons");
        Assert.NotNull(commons.Wants); Assert.StartsWith("the tithe → ", commons.Wants);
    }
}
