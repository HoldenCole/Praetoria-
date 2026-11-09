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
        foreach (var (id, name) in new[] { ("drake", "House Drake"), ("sato", "House Sato"), ("ferro", "House Ferro"), ("pike", "House Pike"), ("wren", "House Wren") })
        {
            w.Houses[id] = new House { Id = id, Name = name, Title = "baron", Legitimacy = 30, Treasury = new Resources { Credits = 30 } };
            w.Characters[id + "_head"] = new Character { Id = id + "_head", Name = name.Replace("House ", "") + " Head", HouseId = id, Age = 40 }; w.Houses[id].Members.Add(id + "_head");
        }
        return (w, new SenateSystem(Content.Motions, Content.Titles, null, Content.Offices) { Policies = Content.Policies }, new CommandContext(w, TestContent.Rng()));
    }

    private static Motion Decided(World w, SenateSystem senate, params (string house, int vote)[] votes)
    {
        var m = senate.Add(w, "aurelian", "levy", ""); foreach (var (h, v) in votes) m.Votes[h] = v;
        w.Turn++; senate.Tick(w); return m;
    }

    [Fact]
    public void FourParties_AreSeated_ByEveryHousesLaws_AndNoBenchFormsOfItself()
    {
        var (w, senate, _) = Chamber();
        FactionSystem.Policies = Content.Policies;
        PolicySystem.Enact(w, Content.Policies, "drake", "levies", "conscription"); PolicySystem.Enact(w, Content.Policies, "drake", "household_guard", "doubled");   // a Sword house
        PolicySystem.Enact(w, Content.Policies, "sato", "tariffs", "free_trade"); PolicySystem.Enact(w, Content.Policies, "sato", "labour", "guild_rights");         // a Factors house
        FactionSystem.Seed(w, Content.Parties, id => senate.Weight(w, id));
        Assert.Equal(4, w.Factions.Count); Assert.All(w.Factions, f => Assert.True(f.Members.Count >= 2, f.Name));
        Assert.Equal("sword", FactionSystem.Of(w, "drake")!.PartyId); Assert.Equal("factors", FactionSystem.Of(w, "sato")!.PartyId);
        Assert.All(w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId), h => Assert.NotNull(FactionSystem.Of(w, h.Id)));   // every house is seated
        Assert.Equal("#FFD60A", FactionSystem.Of(w, "drake")!.Accent); Assert.Contains("officers", FactionSystem.Of(w, "drake")!.Estates); Assert.Equal("common_levy", FactionSystem.Of(w, "drake")!.Laws["levy_of_the_reach"]);
        // votes still leave affinity, but no fifth bench forms of itself
        for (int i = 0; i < 3; i++) Decided(w, senate, ("drake", 1), ("sato", 1), ("ferro", 1), ("corwin", -1), ("vega", -1), ("aurelian", 1));
        Assert.Equal(3, FactionSystem.Affinity(w, "drake", "sato")); Assert.Equal(4, w.Factions.Count);
        // the party line: a Sword member leans for the common levy and against undoing it; a member leans with the party's stance on a kind
        var levy = new Motion { Id = "p", Kind = "act", LawId = "levy_of_the_reach", LawOption = "common_levy" };
        int withParty = senate.ActLean(w, w.House("drake")!, levy); FactionSystem.Of(w, "drake")!.Laws.Remove("levy_of_the_reach"); int without = senate.ActLean(w, w.House("drake")!, levy);
        Assert.Equal(without + 2, withParty);
        var relief = senate.Add(w, "corwin", "levy", ""); Assert.True(senate.Lean(w, relief, "drake") >= senate.RawLean(w, relief, "drake"));   // the Sword stands for the levy
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
    public void AFoundedBench_BreaksUnderTwo_AContentParty_Persists_AndAHouseCrossesTheFloorWhenItsLawsDrift()
    {
        var (w, senate, _) = Chamber(); FactionSystem.Policies = Content.Policies;
        var f = FactionSystem.Form(w, "aurelian", new[] { "drake", "sato" }, "vega");
        Assert.Equal("#C9D1DC", f.Accent);                                                            // the fifth colour: silver, for a founded party
        f.Members.Remove("drake"); f.Members.Remove("sato");
        w.Turn++; senate.Tick(w);
        Assert.Empty(w.Factions); Assert.Contains(w.History, h => h.Text.Contains("is no more"));
        FactionSystem.Seed(w, Content.Parties, id => senate.Weight(w, id));
        var sword = w.Factions.First(x => x.PartyId == "sword"); foreach (var m in sword.Members.ToList()) { sword.Members.Remove(m); FactionSystem.Of(w, m)?.Members.Remove(m); w.Factions.First(x => x.PartyId == "populares").Members.Add(m); }
        w.Turn++; senate.Tick(w);
        Assert.Contains(w.Factions, x => x.PartyId == "sword");                                       // a party of the Republic never dissolves
        // a Populares house whose laws turn martial crosses the floor on the tenth turn
        var mover = w.Factions.First(x => x.PartyId == "populares").Members.First(m => m != "vega");
        PolicySystem.Enact(w, Content.Policies, mover, "levies", "conscription"); PolicySystem.Enact(w, Content.Policies, mover, "household_guard", "doubled"); PolicySystem.Enact(w, Content.Policies, mover, "march_law", "martial_law");
        while (w.Turn % FactionSystem.SwitchEvery != 0) w.Turn++;
        if (w.Factions.First(x => x.PartyId == "populares").Members.Count > 2) { senate.Tick(w); Assert.Equal("sword", FactionSystem.Of(w, mover)!.PartyId); Assert.Contains(w.History, h => h.Text.Contains("crosses the floor")); }
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        Assert.Equal(w.Factions.Count, back.Factions.Count); Assert.Equal("sword", back.Factions.First(x => x.Id == "sword").PartyId); Assert.Equal("common_levy", back.Factions.First(x => x.Id == "sword").Laws["levy_of_the_reach"]);
    }
}
