using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The consulship (docs/REPUBLIC.md stage 2): elected offices, the parties' nominees, the tally by weight, no two terms running, the veto.</summary>
public class ConsulTests
{
    private static World Rings(int unrest) { var m = typeof(RegionSystemTests).GetMethod("Rings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!; return (World)m.Invoke(null, new object[] { unrest })!; }
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());

    private static (World w, SenateSystem senate, CommandContext ctx) Chamber()
    {
        var w = Rings(0); w.Turn = 9; w.House("vega")!.Title = "baron"; w.Pools["self"].Influence = 6;
        foreach (var (id, name) in new[] { ("drake", "House Drake"), ("sato", "House Sato"), ("ferro", "House Ferro"), ("pike", "House Pike"), ("wren", "House Wren") })
        {
            w.Houses[id] = new House { Id = id, Name = name, Title = "baron", Legitimacy = 30, Treasury = new Resources { Credits = 30 } };
            w.Characters[id + "_head"] = new Character { Id = id + "_head", Name = name.Replace("House ", "") + " Head", HouseId = id, Age = 40, CareerTrack = "law", CareerRank = 2 }; w.Houses[id].Members.Add(id + "_head");
        }
        w.Char("aurelian_head")!.CareerTrack = "military"; w.Char("aurelian_head")!.CareerRank = 3; w.House("aurelian")!.SphereInfluence["senate"] = 6;
        w.Char("self")!.CareerTrack = "law"; w.Char("self")!.CareerRank = 2;
        var senate = new SenateSystem(Content.Motions, Content.Titles, null, Content.Offices) { Policies = Content.Policies };
        FactionSystem.Policies = Content.Policies; FactionSystem.Seed(w, Content.Parties, id => senate.Weight(w, id));
        // the ladder (docs/LADDER.md): a consul is forty and has been praetor, as the people of this chamber are and have
        foreach (var c in w.Characters.Values.Where(c => c.CareerRank >= 2)) c.Age = Math.Max(c.Age, 40);
        LadderSystem.Seed(w);
        return (w, senate, new CommandContext(w, TestContent.Rng()));
    }

    [Fact]
    public void TheConsulOffices_AreElected_NotCommissioned_AndTakeAnyTrack()
    {
        var consuls = ElectionSystem.ConsulOffices(Content.Offices).ToList();
        Assert.Equal(2, consuls.Count); Assert.Equal("consul_senior", consuls[0].Id); Assert.True(consuls[0].Veto); Assert.Equal(10, consuls[0].TermTurns); Assert.Equal("", consuls[0].Track);
        var (w, senate, ctx) = Chamber();
        Assert.Equal("the consuls are elected, not commissioned", new ProposeMotionCommand("self", "commission", "vega", senate, "consul_senior").Refusal(ctx));
        Assert.NotNull(OfficeSystem.Candidate(w, consuls[0], "aurelian"));   // a military Commander qualifies for an office with no track
    }

    [Fact]
    public void TheElection_SeatsTheTwoHeaviestNominees_AndNeitherStandsAgainNextTerm()
    {
        var (w, senate, _) = Chamber(); Func<string, int> wt = id => senate.Weight(w, id);
        var noms = ElectionSystem.Nominees(w, Content.Offices, wt); Assert.True(noms.Count >= 2); Assert.All(noms, n => Assert.NotNull(n.partyId));
        w.Turn = 10; Assert.True(ElectionSystem.Due(w)); senate.Tick(w);
        Assert.True(w.HasFlag("election_held")); Assert.Equal(1, w.Counter("elections"));
        var senior = OfficeSystem.HolderHouse(w, "consul_senior"); var junior = OfficeSystem.HolderHouse(w, "consul_junior");
        Assert.NotNull(senior); Assert.NotNull(junior); Assert.NotEqual(senior, junior);
        Assert.Equal("aurelian", senior);                                                       // the duke's party weighs most
        Assert.Equal(10, w.Counter("consul_term:" + senior)); Assert.False(ElectionSystem.Eligible(w, Content.Offices, senior!));
        Assert.Contains(w.History, h => h.Text.StartsWith("The Senate elects its consuls"));
        // next election: the sitting consuls may not stand; the chairs change hands
        w.Turn = 20; senate.Tick(w);
        Assert.NotEqual(senior, OfficeSystem.HolderHouse(w, "consul_senior")); Assert.Equal(2, w.Counter("elections"));
    }

    [Fact]
    public void ThePlayer_Stands_AndIsNominatedInTheirPartysPlace()
    {
        var (w, senate, ctx) = Chamber(); var ex = new CommandExecutor(); Func<string, int> wt = id => senate.Weight(w, id);
        var mine = FactionSystem.Of(w, "vega")!;
        Assert.Null(new StandForConsulCommand("self", Content.Offices).Refusal(ctx)); Assert.True(ex.TryExecute(new StandForConsulCommand("self", Content.Offices), ctx));
        Assert.Equal(4, w.Pools["self"].Influence); Assert.True(w.HasFlag(ElectionSystem.StandFlag("vega"))); Assert.Equal("you already stand", new StandForConsulCommand("self", Content.Offices).Refusal(ctx));
        var noms = ElectionSystem.Nominees(w, Content.Offices, wt); Assert.Contains(noms, n => n.houseId == "vega" && n.partyId == mine.Id);
        var tally = ElectionSystem.Tally(w, Content.Offices, wt, noms); Assert.True(tally["vega"] >= mine.Members.Sum(wt) - 0);   // the party's weight is behind us
        w.Turn = 10; senate.Tick(w);
        Assert.DoesNotContain(w.WorldFlags, f => f.StartsWith("stands_for_consul:"));
        if (OfficeSystem.HolderHouse(w, "consul_senior") == "vega" || OfficeSystem.HolderHouse(w, "consul_junior") == "vega") Assert.True(w.HasFlag("consul_elected"));
    }

    [Fact]
    public void AConsul_VetoesOnce_ATerm_AndRivalConsuls_StrikeWhatStrikesAtThem()
    {
        var (w, senate, ctx) = Chamber(); var ex = new CommandExecutor();
        OfficeSystem.Grant(w, Content.Offices.Def("consul_senior")!, "vega", w.Char("self")!); w.WorldCounters["consul_term:vega"] = w.Turn;
        var m = senate.Add(w, "corwin", "levy", "");
        Assert.Equal("not your own motion", new VetoCommand("self", senate.Add(w, "vega", "recognition", "vega").Id, Content.Offices, senate).Refusal(ctx));
        Assert.Null(new VetoCommand("self", m.Id, Content.Offices, senate).Refusal(ctx)); Assert.True(ex.TryExecute(new VetoCommand("self", m.Id, Content.Offices, senate), ctx));
        Assert.True(m.Resolved); Assert.False(m.Passed); Assert.True(m.Vetoed); Assert.Equal("vega", m.VetoedBy); Assert.Equal(1, w.Counter("vetoes"));
        Assert.Equal("the veto is spent this term", new VetoCommand("self", senate.Add(w, "sato", "levy", "").Id, Content.Offices, senate).Refusal(ctx));
        // a rival consul strikes a censure aimed at it before the vote
        var (w2, senate2, _) = Chamber(); OfficeSystem.Grant(w2, Content.Offices.Def("consul_junior")!, "corwin", w2.Char("corwin_head")!);
        var censure = senate2.Add(w2, "vega", "censure", "corwin"); w2.Turn++; senate2.Tick(w2);
        Assert.True(censure.Vetoed); Assert.Equal("corwin", censure.VetoedBy); Assert.True(w2.HasFlag("vetoed_mine")); Assert.Contains(w2.History, h => h.Text.Contains("vetoes:"));
    }
}
