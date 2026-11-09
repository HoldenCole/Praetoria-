using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Acts of the Senate (GDD §6.1; GAP §15): a motion that sets a Senate-scoped law for the whole Reach. Refusals, leans by the
/// estates, carrying and its effects, the NPC proposer, and the Chancery's view of the law of the Reach.</summary>
public class ActTests
{
    private static World Rings(int unrest) { var m = typeof(RegionSystemTests).GetMethod("Rings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!; return (World)m.Invoke(null, new object[] { unrest })!; }
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static readonly MotionCatalog Motions = MotionCatalog.LoadFromDirectory(ContentLocator.FindContentDir());

    private static (World w, SenateSystem senate, CommandContext ctx) Chamber()
    {
        var w = Rings(0); w.Turn = 5;
        w.House("vega")!.Title = "baron"; w.House("aurelian")!.SphereInfluence["senate"] = 4;
        w.Pools["self"].Influence = 8;
        return (w, new SenateSystem(Motions, Content.Titles, null, Content.Offices) { Policies = Content.Policies }, new CommandContext(w, TestContent.Rng()));
    }

    [Fact]
    public void TheSenateLaws_LoadAsReach_AndTheCustomIsInForceUntilAnActCarries()
    {
        var laws = Content.Policies.Laws.Where(l => l.Scope == "senate").ToList();
        Assert.Equal(3, laws.Count); Assert.All(laws, l => Assert.True(l.Reach));
        var (w, _, _) = Chamber();
        Assert.Equal("customary", PolicySystem.ReachOption(w, Content.Policies, Content.Policies.Law("common_law")!)!.Id);
        Assert.Equal(0, PolicySystem.ReachModifier(w, Content.Policies, "lawlessness_drift"));
        Assert.False(PolicySystem.HoldsScope(w, "vega", "senate"));   // no house holds the Senate's laws; only an Act moves them
        Assert.Equal("only the Senate, by an Act carried may set this", new EnactLawCommand("self", "common_law", "assizes", Content.Policies).Refusal(new CommandContext(w, TestContent.Rng())));
    }

    [Fact]
    public void MovingAnAct_RefusesTheWrongLaw_TheCustomItself_AndADocketedLaw()
    {
        var (w, senate, ctx) = Chamber(); var ex = new CommandExecutor();
        Assert.Equal("name a law of the Senate", new ProposeMotionCommand("self", "act", "", senate, lawId: "tithe", optionId: "harsh").Refusal(ctx));
        Assert.Equal("name one of its options", new ProposeMotionCommand("self", "act", "", senate, lawId: "common_law", optionId: "gallows").Refusal(ctx));
        Assert.Equal("already the law of the Reach", new ProposeMotionCommand("self", "act", "", senate, lawId: "common_law", optionId: "customary").Refusal(ctx));
        var rival = senate.Add(w, "aurelian", "act", "", lawId: "trade_charter", lawOption: "open_lanes");
        Assert.Equal("that law is already before the Senate", new ProposeMotionCommand("self", "act", "", senate, lawId: "trade_charter", optionId: "protected").Refusal(ctx));
        var move = new ProposeMotionCommand("self", "act", "", senate, lawId: "common_law", optionId: "assizes");
        Assert.Null(move.Refusal(ctx)); Assert.True(ex.TryExecute(move, ctx));
        Assert.Equal(5, w.Pools["self"].Influence);
        var m = SenateSystem.Docket(w).Single(x => x.ProposerHouseId == "vega"); Assert.Equal("common_law", m.LawId); Assert.Equal("assizes", m.LawOption); Assert.Equal(1, m.Votes["vega"]);
        Assert.Contains("that the common law be the reach's assizes throughout the Reach", senate.Describe(w, m));
        Assert.Equal("you already have a motion before the Senate", new ProposeMotionCommand("self", "act", "", senate, lawId: "common_law", optionId: "local_courts").Refusal(ctx));
        senate.Tick(w); Assert.True(w.HasFlag("act_open"));
    }

    [Fact]
    public void AHouse_LeansForTheOption_ItsHostileEstateWants_AndAgainstTheOneItsLoyalEstateHates()
    {
        var (w, senate, _) = Chamber();
        var m = new Motion { Id = "probe", Kind = "act", ProposerHouseId = "vega", LawId = "common_law", LawOption = "assizes" };
        var corwin = w.House("corwin")!;
        w.WorldCounters[PolicySystem.ApprovalKey(w, "corwin", "magistrates")] = -40;      // Corwin's magistrates are hostile: the assizes please them (+15, doubled)
        int warm = senate.ActLean(w, corwin, m);
        w.WorldCounters[PolicySystem.ApprovalKey(w, "corwin", "magistrates")] = 0; w.WorldCounters[PolicySystem.ApprovalKey(w, "corwin", "old_blood")] = 40;   // loyal old blood hate them (−10, doubled)
        int cold = senate.ActLean(w, corwin, m);
        Assert.True(warm > cold, $"warm {warm} cold {cold}");
        Assert.Equal(0, senate.ActLean(w, corwin, new Motion { Id = "p2", Kind = "act", LawId = "common_law", LawOption = "customary" }));   // already the law: no one cares
        // situation: a house at war wants the Reach's levy
        var levy = new Motion { Id = "p3", Kind = "act", LawId = "levy_of_the_reach", LawOption = "common_levy" };
        int peace = senate.ActLean(w, corwin, levy); w.Wars.Add(new War { HouseA = "corwin", HouseB = "aurelian", Since = 1 });
        Assert.Equal(peace + 2, senate.ActLean(w, corwin, levy));
    }

    [Fact]
    public void AnActThatCarries_SetsTheLawOfTheReach_ForEveryHouse_AndTheChamberWillNotHearItAgainForATime()
    {
        var (w, senate, ctx) = Chamber(); var ex = new CommandExecutor();
        ex.TryExecute(new ProposeMotionCommand("self", "act", "", senate, lawId: "trade_charter", optionId: "open_lanes"), ctx);
        var m = SenateSystem.Docket(w).Single(); foreach (var h in w.Houses.Values) m.Votes[h.Id] = 1;
        w.Turn++; senate.Tick(w);
        Assert.True(m.Passed); Assert.True(w.HasFlag("act_passed")); Assert.True(w.HasFlag("act_passed:trade_charter")); Assert.True(w.HasFlag("motion_passed_mine")); Assert.Equal(1, w.Counter("acts_passed"));
        Assert.Equal("open_lanes", PolicySystem.ReachOption(w, Content.Policies, Content.Policies.Law("trade_charter")!)!.Id);
        Assert.Equal(-10, PolicySystem.ReachModifier(w, Content.Policies, "market_price_pct"));
        Assert.Equal(20, PolicySystem.ReachOpinion(w, Content.Policies, "merchants"));
        Assert.True(PolicySystem.LawOpinion(w, Content.Policies, "corwin", "merchants") >= 20);
        Assert.Contains(w.History, h => h.Text.StartsWith("An Act of the Senate: the trade charter is now open lanes"));
        Assert.StartsWith("the Senate settled that lately", new ProposeMotionCommand("self", "act", "", senate, lawId: "trade_charter", optionId: "protected").Refusal(ctx));
        // the Senate holder's laws survive a save round-trip
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        Assert.Equal("open_lanes", PolicySystem.ReachOption(back, Content.Policies, Content.Policies.Law("trade_charter")!)!.Id);
    }

    [Fact]
    public void ARivalWithAHostileEstate_MovesTheActThatPleasesIt()
    {
        var (w, senate, _) = Chamber();
        var aurelian = w.House("aurelian")!;
        w.WorldCounters[PolicySystem.ApprovalKey(w, "aurelian", "merchants")] = -50;   // the duke's factors are in revolt: open lanes would please them
        var want = senate.WantedAct(w, aurelian);
        Assert.NotNull(want); Assert.Equal("trade_charter", want!.Value.law.Id); Assert.Equal("open_lanes", want.Value.option.Id);
        // and the proposer's turn finds it when nothing more pressing stands
        for (int t = 0; t < 12 && !w.Motions.Any(m => m.Kind == "act"); t++) { w.Turn++; senate.Tick(w); }
        var act = w.Motions.FirstOrDefault(m => m.Kind == "act");
        Assert.NotNull(act); Assert.Equal("aurelian", act!.ProposerHouseId); Assert.Equal("open_lanes", act.LawOption);
    }

    [Fact]
    public void TheChancery_ShowsTheLawOfTheReach_AndTheSenateOffersTheActPicker()
    {
        var (w, senate, _) = Chamber();
        PolicySystem.Enact(w, Content.Policies, PolicySystem.SenateHolder, "levy_of_the_reach", "common_levy");
        var tc = new TurnController(w, Content); var model = CourtProjection.Build(tc, Content);
        var reach = model.Chancery!.Reach!; var levy = reach.Single(r => r.Id == "levy_of_the_reach");
        Assert.True(levy.Act); Assert.Equal("The Reach's levy", levy.OptionName); Assert.Equal("by an Act of the Senate", levy.SetBy); Assert.True(levy.CooldownLeft > 0);
        Assert.Equal("the custom, no Act yet carried", reach.Single(r => r.Id == "common_law").SetBy);
        var commons = model.Chancery.Estates.Single(e => e.Id == "commons"); Assert.Contains("the Reach's the reach's levy -15", commons.LawsSay);
        var act = model.Senate!.Proposals.Single(p => p.KindId == "act");
        Assert.True(act.NeedsLaw); Assert.Equal(3, act.Laws!.Count);
        var charter = act.Laws.Single(l => l.Id == "levy_of_the_reach"); Assert.Equal("common_levy", charter.InForce);
        Assert.Equal("already the law of the Reach", charter.Options.Single(o => o.Id == "common_levy").Refusal);
        Assert.StartsWith("the Senate settled that lately", charter.Options.Single(o => o.Id == "customary").Refusal);
        Assert.Null(act.Laws.Single(l => l.Id == "common_law").Options.Single(o => o.Id == "assizes").Refusal); Assert.Null(act.Refusal);
    }
}
