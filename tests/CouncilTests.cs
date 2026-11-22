using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The posts of a house's council (docs/HOUSEHOLD.md, "The council"): the chancellor, the spymaster, the envoy and the
/// advocate, built as the steward and the marshal are.</summary>
public class CouncilTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static (TurnController tc, World w, House house, Character me, CommandContext ctx) Start(ulong seed = 5)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), seed); var tc = new TurnController(w, Content);
        var me = w.Protagonist!; return (tc, w, w.House(me.HouseId)!, me, new CommandContext(w, WorldBuilder.RngFor(w)));
    }
    private static Character Kin(World w, House house) => HouseholdSystem.Eligible(w, house).First();
    private static void Year(TurnController tc) { foreach (var i in tc.BeginTurn()) { var ch = tc.Offer(i).FirstOrDefault(x => x.Available); if (ch != null) tc.Resolve(i, ch.Choice.Id); } tc.EndTurn(); }
    private static Duty Give(TurnController tc, World w, House house, Character me, CommandContext ctx, string role, string instruction = "")
    {
        var k = HouseholdSystem.Eligible(w, house).FirstOrDefault(c => HouseholdSystem.AssignRefusal(w, me, role, c) == null);
        if (k == null)   // a start with too few grown relatives who will serve: one is made for the post
        {
            k = new Character { Id = "kin_for_" + role, Name = "Test " + role, HouseId = house.Id, Age = 30, Sex = "female", CareerTrack = "law" }; w.Characters[k.Id] = k; house.Members.Add(k.Id); w.Pools[k.Id] = ActionPools.ForNpc();
            w.Relationship(k.Id, me.Id).Disposition = 40;
        }
        Assert.True(tc.Executor.TryExecute(new DelegateCommand(me.Id, role, k.Id, 0), ctx), new DelegateCommand(me.Id, role, k.Id, 0).Refusal(ctx));
        if (instruction.Length > 0) Assert.True(tc.Executor.TryExecute(new PostInstructionCommand(me.Id, role, instruction), ctx), new PostInstructionCommand(me.Id, role, instruction).Refusal(ctx));
        return house.Duty(role)!;
    }

    [Fact]
    public void There_are_six_posts_and_each_has_a_name_a_skill_and_something_to_be_told()
    {
        var (_, w, house, _, _) = Start(); Assert.Equal(6, HouseholdSystem.Roles.Length);
        foreach (var role in HouseholdSystem.Roles)
        {
            Assert.NotEqual("", HouseholdSystem.RoleName(role)); Assert.True(HouseholdSystem.RoleDoes(role).Length > 20);
            var told = HouseholdSystem.Instructions(w, house, role); if (role == HouseholdSystem.Marshal) { Assert.Empty(told); continue; }
            Assert.Contains("", told);
            foreach (var t in told) { Assert.NotEqual("", HouseholdSystem.InstructionName(w, role, t)); Assert.True(HouseholdSystem.InstructionDoes(w, role, t).Length > 20, role + ":" + t); }
        }
        // the council has six seats, and the three new ones stand empty until somebody is named
        var seats = AdvisorReports.Build(w, Content); Assert.Equal(6, seats.Count); Assert.Equal(HouseholdSystem.Roles.OrderBy(r => r), seats.Select(s => s.Role).OrderBy(r => r));
        Assert.All(seats.Where(s => s.Role is HouseholdSystem.Spymaster or HouseholdSystem.Envoy or HouseholdSystem.Advocate), s => { Assert.True(s.Vacant); Assert.Null(s.AdvisorName); });
        Assert.All(seats, s => Assert.DoesNotContain("(s)", s.Line));
        // beside the map a seat speaks only for what wants doing now: a neighbour's larger fleet is a worry, not an alert
        Assert.All(seats.Where(s => s.Urgent), s => Assert.True(s.Warning || s.Role is HouseholdSystem.Steward or HouseholdSystem.Marshal, s.Seat));
        Assert.DoesNotContain(seats, s => s.Role == HouseholdSystem.Marshal && s.Urgent);   // at peace, with ships and fuel
    }

    [Fact]
    public void A_post_is_told_one_thing_and_only_by_the_head_while_somebody_holds_it()
    {
        var (tc, w, house, me, ctx) = Start();
        Assert.Contains("no envoy", new PostInstructionCommand(me.Id, HouseholdSystem.Envoy, "").Refusal(ctx));
        var d = Give(tc, w, house, me, ctx, HouseholdSystem.Envoy); string other = w.Houses.Values.First(h => h.Id != house.Id && !World.Outsider(h.Id) && Economy.HeadOf(w, h) != null).Id;
        Assert.True(tc.Executor.TryExecute(new PostInstructionCommand(me.Id, HouseholdSystem.Envoy, HouseholdSystem.HousePrefix + other), ctx)); Assert.Equal(HouseholdSystem.HousePrefix + other, d.Favour);
        Assert.Equal("no such instruction", new PostInstructionCommand(me.Id, HouseholdSystem.Envoy, "house:nobody").Refusal(ctx));
        Assert.Equal("that post takes no instruction", new PostInstructionCommand(me.Id, HouseholdSystem.Marshal, "").Refusal(ctx));
        Assert.Equal("not yours to say", new PostInstructionCommand(d.HolderId, HouseholdSystem.Envoy, "").Refusal(ctx));
    }

    [Fact]
    public void An_envoy_leaves_a_head_thinking_better_of_you_and_the_gifts_are_paid_for()
    {
        var (tc, w, house, me, ctx) = Start(); var target = w.Houses.Values.Where(h => h.Id != house.Id && !World.Outsider(h.Id) && Economy.HeadOf(w, h) != null && !w.AtWar(h.Id, house.Id)).OrderBy(h => h.Id).First();
        var head = Economy.HeadOf(w, target)!; w.Relationship(head.Id, me.Id).Disposition = 0; house.Treasury.Credits = 200;
        var d = Give(tc, w, house, me, ctx, HouseholdSystem.Envoy, HouseholdSystem.HousePrefix + target.Id); var envoy = w.Char(d.HolderId)!; envoy.Skills["diplomacy"] = 3;
        Year(tc);
        int after = w.Relationship(head.Id, me.Id).Disposition; Assert.True(after >= HouseholdSystem.EnvoyBase + 3 - 2, $"regard {after}");   // the call, less whatever else the year did
        Assert.Contains(target.Name, d.LastReport); Assert.Contains("credit", d.LastReport); Assert.Contains(HouseholdSystem.PostFlag(HouseholdSystem.Envoy), envoy.Flags);
        // at the reserve an envoy stays at home
        d.Reserve = 100000; Year(tc); Assert.Contains("Stayed at home", d.LastReport);
        // and one as warm as an envoy can make them is not called on for nothing
        d.Reserve = 0; w.Relationship(head.Id, me.Id).Disposition = HouseholdSystem.EnvoyCap; int credits = house.Treasury.Credits; Year(tc); Assert.Contains("already thinks", d.LastReport);
    }

    [Fact]
    public void A_spymaster_watching_a_house_knows_its_fleets_exactly_and_one_watching_home_finds_what_is_missing()
    {
        var (tc, w, house, me, ctx) = Start(); house.Treasury.Credits = 200;
        var rival = w.Houses.Values.Where(h => h.Id != house.Id && !World.Outsider(h.Id) && Economy.HeadOf(w, h) != null && w.Fleets.Values.Any(f => f.OwnerId == h.Id)).OrderBy(h => h.Id).First();
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == rival.Id)) w.Intel.Remove(f.Id);
        var d = Give(tc, w, house, me, ctx, HouseholdSystem.Spymaster, HouseholdSystem.HousePrefix + rival.Id); w.Char(d.HolderId)!.Skills["intrigue"] = 4;
        Year(tc);
        Assert.Contains(rival.Name, d.LastReport); Assert.Contains("under sail", d.LastReport);
        Assert.All(w.Fleets.Values.Where(f => f.OwnerId == rival.Id), f => Assert.True(w.Intel.ContainsKey(f.Id) && w.Intel[f.Id].Confidence >= 60, f.Name));
        // watching the house's own: what a steward has taken is found before it would have been
        var steward = Give(tc, w, house, me, ctx, HouseholdSystem.Steward); steward.Skimmed = 3; var thief = w.Char(steward.HolderId)!; thief.Flags.Remove(HouseholdSystem.SkimmingChar);
        Assert.True(tc.Executor.TryExecute(new PostInstructionCommand(me.Id, HouseholdSystem.Spymaster, HouseholdSystem.WatchHome), ctx));
        Year(tc);
        if (house.Duty(HouseholdSystem.Steward)?.HolderId == thief.Id) { Assert.Contains(HouseholdSystem.SkimmingChar, thief.Flags); Assert.Contains("missing", d.LastReport); }
    }

    [Fact]
    public void An_advocate_pleads_what_names_the_house_and_a_chancellor_declares_what_you_left_alone()
    {
        var (tc, w, house, me, ctx) = Start(); house.Treasury.Credits = 300;
        var adv = Give(tc, w, house, me, ctx, HouseholdSystem.Advocate); var chan = Give(tc, w, house, me, ctx, HouseholdSystem.Chancellor);
        int pleaded = 0, declared = 0;
        for (int y = 0; y < 14; y++)
        {
            var waiting = LadderSystem.SuitsNaming(w, house.Id).Where(b => b.AdvocateId != adv.HolderId).Select(b => b.Id).ToList();
            var undeclared = w.Motions.Where(m => m.Open && m.ProposedTurn < w.Turn && !m.Votes.ContainsKey(house.Id)).ToList();
            foreach (var i in tc.BeginTurn()) { var ch = tc.Offer(i).FirstOrDefault(x => x.Available); if (ch != null) tc.Resolve(i, ch.Choice.Id); }
            waiting = LadderSystem.SuitsNaming(w, house.Id).Where(b => b.AdvocateId != adv.HolderId).Select(b => b.Id).ToList();
            undeclared = w.Motions.Where(m => m.Open && m.ProposedTurn < w.Turn && !m.Votes.ContainsKey(house.Id)).ToList();
            bool advocateStill = house.Duty(HouseholdSystem.Advocate) == adv, chancellorStill = house.Duty(HouseholdSystem.Chancellor) == chan;
            tc.EndTurn();
            // one who laid the work down in the year, or was no longer the head's to command, did not do it
            advocateStill &= house.Duty(HouseholdSystem.Advocate) == adv && w.ProtagonistId == me.Id; chancellorStill &= house.Duty(HouseholdSystem.Chancellor) == chan && w.ProtagonistId == me.Id;
            if (advocateStill && waiting.Count > 0) { pleaded += waiting.Count; Assert.Contains("plead", adv.LastReport); }
            if (chancellorStill && undeclared.Count > 0) { declared += undeclared.Count; Assert.Contains("Declared the house", chan.LastReport); }
        }
        Assert.True(pleaded + declared > 0, "fourteen years with no suit and no motion to speak on");
        // told to declare nothing, a chancellor leaves the vote to the head
        Assert.Contains(HouseholdSystem.Abstain, HouseholdSystem.Instructions(w, house, HouseholdSystem.Chancellor));
    }

    [Fact]
    public void A_post_has_a_law_that_is_in_force_only_while_somebody_holds_it()
    {
        var (tc, w, house, me, ctx) = Start(); var law = Content.Policies.Law("informers"); Assert.NotNull(law); Assert.Equal("post:spymaster", law!.Scope);
        Assert.False(PolicySystem.HoldsScope(w, house.Id, law.Scope)); Assert.Contains("spymaster", new EnactLawCommand(me.Id, "informers", "paid", Content.Policies).Refusal(ctx));
        Give(tc, w, house, me, ctx, HouseholdSystem.Spymaster); Assert.True(PolicySystem.HoldsScope(w, house.Id, law.Scope));
        Assert.NotNull(Content.Policies.Law("embassy")); Assert.NotNull(Content.Policies.Law("counsel"));
        foreach (var id in new[] { "informers", "embassy", "counsel" }) Assert.Equal(3, Content.Policies.Law(id)!.Options.Count);
    }

    [Theory]
    [InlineData("the_spymasters_report", HouseholdSystem.SpyReportFlag, HouseholdSystem.Spymaster, HouseholdSystem.SpyTargetChar)]
    [InlineData("the_informer_found", HouseholdSystem.SpyCaughtFlag, HouseholdSystem.Spymaster, HouseholdSystem.SpyTargetChar)]
    [InlineData("the_envoys_friend", HouseholdSystem.EnvoyWarmFlag, HouseholdSystem.Envoy, HouseholdSystem.EnvoyTargetChar)]
    [InlineData("the_envoy_turned_away", HouseholdSystem.EnvoyColdFlag, HouseholdSystem.Envoy, HouseholdSystem.EnvoyTargetChar)]
    [InlineData("the_advocates_day", HouseholdSystem.AdvocateWonFlag, HouseholdSystem.Advocate, "")]
    [InlineData("the_advocate_loses", HouseholdSystem.AdvocateLostFlag, HouseholdSystem.Advocate, "")]
    [InlineData("the_chancellors_voice", HouseholdSystem.ChancellorVotedFlag, HouseholdSystem.Chancellor, "")]
    public void Each_scene_of_the_council_can_be_reached_and_every_answer_to_it_taken(string eventId, string flag, string role, string otherMark)
    {
        foreach (string answer in Content.Events.First(e => e.Id == eventId).Choices.Select(c => c.Id))
        {
            var (tc, w, house, me, ctx) = Start(); house.Treasury.Credits = 100; w.PoolsFor(me.Id).Influence = 9;
            var d = Give(tc, w, house, me, ctx, role); w.Char(d.HolderId)!.Flags.Add(HouseholdSystem.PostFlag(role)); w.WorldFlags.Add(flag);
            if (otherMark.Length > 0) Economy.HeadOf(w, w.Houses.Values.Where(h => h.Id != house.Id && !World.Outsider(h.Id) && Economy.HeadOf(w, h) != null).OrderBy(h => h.Id).First())!.Flags.Add(otherMark);
            var engine = new Praetoria.Core.Events.EventEngine(Content, WorldBuilder.RngFor(w));
            var found = engine.GatherEligible(w).FirstOrDefault(e => e.def.Id == eventId); Assert.True(found.def != null, eventId + " is not eligible");
            engine.Resolve(new Praetoria.Core.Events.FiredEvent(found.def!, found.binding), w, answer);
            Assert.False(w.HasFlag(flag), eventId + "/" + answer + " leaves its flag up");   // it does not come round again for the same deed
        }
    }

    [Fact]
    public void What_is_written_for_the_council_reads_flags_the_posts_raise()
    {
        var flags = new[] { HouseholdSystem.SpyReportFlag, HouseholdSystem.SpyCaughtFlag, HouseholdSystem.EnvoyWarmFlag, HouseholdSystem.EnvoyColdFlag, HouseholdSystem.AdvocateWonFlag, HouseholdSystem.AdvocateLostFlag, HouseholdSystem.ChancellorVotedFlag };
        var ids = new[] { "the_spymasters_report", "the_informer_found", "the_envoys_friend", "the_envoy_turned_away", "the_advocates_day", "the_advocate_loses", "the_chancellors_voice" };
        foreach (var id in ids) Assert.Contains(Content.Events, e => e.Id == id);
        string json = System.IO.File.ReadAllText(System.IO.Path.Combine(ContentLocator.FindContentDir(), "events", "council.json"));
        foreach (var f in flags) Assert.Contains("\"" + f + "\"", json);
    }
}
