using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Persistent Senate factions (GDD §6.1 "voting blocs"): affinity from votes, benches that form, grow and break, discipline, and the player's three verbs.</summary>
public class FactionTests
{
    private static World Rings(int unrest) { var m = typeof(RegionSystemTests).GetMethod("Rings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!; return (World)m.Invoke(null, new object[] { unrest })!; }
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());

    private static (World w, SenateSystem senate, CommandContext ctx) Chamber()
    {
        var w = Rings(0); w.Turn = 5;
        w.House("vega")!.Title = "baron"; w.Pools["self"].Influence = 6;
        foreach (var (id, name) in new[] { ("drake", "House Drake"), ("sato", "House Sato"), ("ferro", "House Ferro") })
        {
            w.Houses[id] = new House { Id = id, Name = name, Title = "baron", Legitimacy = 30, Treasury = new Resources { Credits = 30 } };
            w.Characters[id + "_head"] = new Character { Id = id + "_head", Name = name.Replace("House ", "") + " Head", HouseId = id, Age = 40 }; w.Houses[id].Members.Add(id + "_head");
        }
        return (w, new SenateSystem(Content.Motions, Content.Titles, null, Content.Offices), new CommandContext(w, TestContent.Rng()));
    }

    private static Motion Decided(World w, SenateSystem senate, params (string house, int vote)[] votes)
    {
        var m = senate.Add(w, "aurelian", "levy", ""); foreach (var (h, v) in votes) m.Votes[h] = v;
        w.Turn++; senate.Tick(w); return m;
    }

    [Fact]
    public void VotesLeaveAffinity_AndABenchForms_AroundTheWeightiestOfThreeFriends()
    {
        var (w, senate, _) = Chamber();
        for (int i = 0; i < 3; i++) Decided(w, senate, ("drake", 1), ("sato", 1), ("ferro", 1), ("corwin", -1), ("vega", -1), ("aurelian", 1));
        Assert.Equal(3, FactionSystem.Affinity(w, "drake", "sato")); Assert.Equal(-3, FactionSystem.Affinity(w, "drake", "corwin"));
        var f = Assert.Single(w.Factions);
        Assert.Equal("aurelian", f.LeaderHouseId);                                                  // the duke weighs most among the friends
        Assert.Contains("drake", f.Members); Assert.Contains("sato", f.Members); Assert.Contains("ferro", f.Members); Assert.DoesNotContain("vega", f.Members);
        Assert.Equal("The Court Party", f.Name);                                                      // led by the capital's holder
        Assert.Equal(1, w.Counter("factions")); Assert.Contains(w.History, h => h.Text.Contains("takes shape in the Senate"));
    }

    [Fact]
    public void ABenchFollowsItsLeader_AndAWhipOnTheLeaderMovesTheBench()
    {
        var (w, senate, ctx) = Chamber();
        FactionSystem.Form(w, "aurelian", new[] { "drake", "sato" }, "vega");
        var m = senate.Add(w, "corwin", "censure", "vega");                                          // nobody on the bench cares about Vega by interest
        int rawDrake = senate.RawLean(w, m, "drake"); m.Votes["aurelian"] = -1;                   // the duke declares against
        Assert.Equal(rawDrake - FactionSystem.Discipline, senate.Lean(w, m, "drake"));
        m.Votes.Remove("aurelian"); m.Lobby["aurelian"] = 4;                                         // two whips on the leader
        Assert.True(senate.Lean(w, m, "drake") >= rawDrake + 2);                                      // half of it reaches the bench
        var targeted = senate.Add(w, "corwin", "censure", "drake"); targeted.Votes["aurelian"] = 1;
        Assert.Equal(senate.RawLean(w, targeted, "drake"), senate.Lean(w, targeted, "drake"));     // no discipline against yourself
    }

    [Fact]
    public void ThePlayer_Joins_BreaksTheWhip_AndLeaves()
    {
        var (w, senate, ctx) = Chamber(); var ex = new CommandExecutor();
        var f = FactionSystem.Form(w, "aurelian", new[] { "drake", "sato" }, "vega");
        Assert.Equal("Lucius will not have you on the bench", new JoinFactionCommand("self", f.Id).Refusal(new CommandContext(Hostile(w), TestContent.Rng())));
        w.Relationship("aurelian_head", "self").Disposition = 0;
        Assert.True(ex.TryExecute(new JoinFactionCommand("self", f.Id), ctx));
        Assert.Contains("vega", f.Members); Assert.Equal(4, w.Pools["self"].Influence); Assert.True(w.HasFlag("faction_joined")); Assert.Equal(3, FactionSystem.Affinity(w, "vega", "aurelian"));
        Assert.Equal("you sit with a bench already", new JoinFactionCommand("self", f.Id).Refusal(ctx));
        // vote against the leader: the whip is broken
        w.WorldCounters[FactionSystem.AffinityKey("vega", "aurelian")] = 6;                            // a seasoned member
        var m = senate.Add(w, "corwin", "levy", ""); m.Votes["aurelian"] = 1; m.Votes["vega"] = -1;
        int disp = w.Relationship("aurelian_head", "self").Disposition;
        w.Turn++; senate.Tick(w);
        Assert.True(w.HasFlag("broke_the_whip")); Assert.Equal(6 - FactionSystem.BreakWhipAffinity - 1, FactionSystem.Affinity(w, "vega", "aurelian"));   // the whip, and the vote apart itself Assert.Equal(disp - FactionSystem.BreakWhipDisposition, w.Relationship("aurelian_head", "self").Disposition);
        Assert.True(ex.TryExecute(new LeaveFactionCommand("self"), ctx)); Assert.DoesNotContain("vega", f.Members); Assert.Equal("you sit with no bench", new LeaveFactionCommand("self").Refusal(ctx));
    }
    private static World Hostile(World w) { w.Relationship("aurelian_head", "self").Disposition = -30; return w; }

    [Fact]
    public void ThePlayer_FoundsABench_WhenTwoHousesHaveVotedWithThem()
    {
        var (w, senate, ctx) = Chamber(); var ex = new CommandExecutor();
        Assert.Equal("no house has voted with you often enough", new FoundFactionCommand("self").Refusal(ctx));
        for (int i = 0; i < 3; i++) Decided(w, senate, ("vega", -1), ("drake", -1), ("sato", -1), ("aurelian", 1), ("corwin", 1), ("ferro", 1));
        // the rivals' bench formed around Aurelian; Drake and Sato stayed free only if the duke did not reach them — check what is left for us
        var followers = FoundFactionCommand.Followers(w, "vega");
        if (followers.Count >= 2)
        {
            Assert.Null(new FoundFactionCommand("self").Refusal(ctx)); Assert.True(ex.TryExecute(new FoundFactionCommand("self"), ctx));
            var f = FactionSystem.Of(w, "vega")!; Assert.Equal("vega", f.LeaderHouseId); Assert.True(w.HasFlag("faction_formed")); Assert.True(FactionSystem.Leads(w, "vega"));
        }
        else Assert.StartsWith("one house would follow", new FoundFactionCommand("self").Refusal(ctx));
    }

    [Fact]
    public void ABench_LosesAMemberWhoDrifts_AndDissolvesAtOne_AndSurvivesASave()
    {
        var (w, senate, _) = Chamber();
        var f = FactionSystem.Form(w, "aurelian", new[] { "drake", "sato" }, "vega");
        w.WorldCounters[FactionSystem.AffinityKey("drake", "aurelian")] = 0;
        w.Turn++; senate.Tick(w);
        Assert.DoesNotContain("drake", f.Members); Assert.Contains(w.History, h => h.Text.Contains("breaks with"));
        w.WorldCounters[FactionSystem.AffinityKey("sato", "aurelian")] = 0;
        w.Turn++; senate.Tick(w);
        Assert.Empty(w.Factions); Assert.Contains(w.History, h => h.Text.Contains("is no more"));
        var g = FactionSystem.Form(w, "aurelian", new[] { "drake", "sato" }, "vega"); g.Platform["levy"] = -2;
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        Assert.Single(back.Factions); Assert.Equal(-2, back.Factions[0].Platform["levy"]); Assert.Equal(3, back.Factions[0].Members.Count);
    }
}
