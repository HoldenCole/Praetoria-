using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Emergency powers (docs/EMERGENCY.md; docs/GAPS.md R8): voted to a consul when the Reach is in danger, laid down when the term is
/// up or the danger past, and for the player alone, kept, or asked of the Senate for life.</summary>
public class EmergencyTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static (TurnController tc, World w, Character me, House mine, CommandContext ctx) Start(string scenario = "the_old_lion", int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content); tc.BeginTurn();
        return (tc, w, w.Protagonist!, w.House(w.Protagonist!.HouseId)!, new CommandContext(w, TestContent.Rng()));
    }
    private static bool Do(World w, ICommand cmd) => new CommandExecutor().TryExecute(cmd, new CommandContext(w, TestContent.Rng()));
    private static OfficeDef Chair(int i = 0) => ElectionSystem.ConsulOffices(Content.Offices).ElementAt(i);
    private static void Consul(World w, House h, Character who, int chair = 0) { who.Age = Math.Max(who.Age, 45); OfficeSystem.Grant(w, Chair(chair), h.Id, who); }
    private static void Danger(World w, bool on = true) { foreach (var n in w.Nations) n.AtWar = false; if (on) w.Nations[0].AtWar = true; }
    private static House Other(World w, House mine, int skip = 0) => w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId && Economy.HeadOf(w, h) != null).OrderBy(h => h.Id, StringComparer.Ordinal).Skip(skip).First();
    private static bool Eligible(World w, string eventId)
    {
        var me = w.Protagonist!; w.House(me.HouseId)!.Treasury.Credits = Math.Max(100, w.House(me.HouseId)!.Treasury.Credits); w.PoolsFor(me.Id).Influence = 9;
        return new EventEngine(Content, WorldBuilder.RngFor(w)).GatherEligible(w).Any(e => e.def.Id == eventId);
    }

    [Fact]
    public void TheSenateGivesThem_OnlyInDanger_OnlyToASittingConsul()
    {
        var (tc, w, me, mine, ctx) = Start(); Danger(w, false);
        Assert.Null(EmergencySystem.Danger(w)); Assert.Equal("the Reach is in no danger that calls for it", EmergencySystem.GrantRefusal(w, mine, Content.Offices));
        Danger(w); Assert.Equal("the war beyond the Reach", EmergencySystem.Danger(w)); Assert.Equal(1, EmergencySystem.Gravity(w));
        foreach (var d in ElectionSystem.ConsulOffices(Content.Offices)) if (OfficeSystem.Holding(w, d.Id) is { } old && old.HouseId == mine.Id) OfficeSystem.Strip(w, d, "test", false);
        Assert.Equal("only a sitting consul may be given them", EmergencySystem.GrantRefusal(w, mine, Content.Offices));
        Consul(w, mine, me); Assert.Null(EmergencySystem.GrantRefusal(w, mine, Content.Offices));
        Assert.Contains("emergency", Content.Motions.Defs.Select(d => d.Id)); Assert.Contains("for_life", Content.Motions.Defs.Select(d => d.Id));
        // carried: one consul above the other, the colleague's veto set aside, and no election while they run
        var other = Other(w, mine); var colleague = Economy.HeadOf(w, other)!; Consul(w, other, colleague, 1);
        EmergencySystem.GrantVoted(w, mine, null, Content.Offices, passed: true);
        var e = w.Emergency!; Assert.Equal(me.Id, e.HolderId); Assert.Equal(w.Turn + EmergencySystem.Years, e.Until); Assert.Equal("the war beyond the Reach", e.Cause);
        Assert.Contains(EmergencySystem.HoldsChar, me.Flags); Assert.True(w.HasFlag(EmergencySystem.MineFlag)); Assert.True(Eligible(w, "the_powers_are_yours"));
        Assert.True(ElectionSystem.MayVeto(w, Content.Offices, mine.Id)); Assert.False(ElectionSystem.MayVeto(w, Content.Offices, other.Id));
        Assert.Equal(EmergencySystem.HolderLeanBy, EmergencySystem.HolderLean(w, mine.Id)); Assert.Equal(0, EmergencySystem.HolderLean(w, other.Id));
        int turn = w.Turn; w.Turn = ElectionSystem.Every; Assert.False(ElectionSystem.Due(w)); Assert.True(e.SkippedElection); w.Turn = turn;
        Assert.Contains("holds them already", EmergencySystem.GrantRefusal(w, other, Content.Offices)); Assert.Contains("too soon", EmergencySystem.GrantRefusal(w, mine, Content.Offices));
        // refused: nothing is given
        w.Emergency = null; EmergencySystem.GrantVoted(w, mine, null, Content.Offices, passed: false); Assert.Null(w.Emergency); Assert.Equal(1, w.Counter(EmergencySystem.RefusedKey));
    }

    [Fact]
    public void HowAHouseLeans_OnGivingThem()
    {
        var (tc, w, me, mine, ctx) = Start(); Danger(w); Consul(w, mine, me); var voter = Other(w, mine);
        int plain = EmergencySystem.GrantLean(w, voter, mine, 0);
        Assert.True(EmergencySystem.GrantLean(w, voter, mine, 60) > plain); Assert.True(EmergencySystem.GrantLean(w, voter, mine, -60) < plain);
        Assert.Equal(5, EmergencySystem.GrantLean(w, mine, mine, 0));
        // a house that kept them once is not given them lightly, and asking again costs
        w.WorldCounters[EmergencySystem.KeptOnceKey(mine.Id)] = 1; Assert.Equal(plain - 4, EmergencySystem.GrantLean(w, voter, mine, 0)); w.WorldCounters.Remove(EmergencySystem.KeptOnceKey(mine.Id));
        EmergencySystem.GrantVoted(w, mine, null, Content.Offices, passed: true); Assert.Equal(plain - 2, EmergencySystem.GrantLean(w, voter, mine, 0));
        // a holder with a great share of the Reach's hulls is feared when asking for the powers and not easily refused when asking for life
        w.Emergency!.Due = true; int life = EmergencySystem.ForLifeLean(w, voter, mine, 0);
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId != mine.Id)) { f.Ships.Clear(); f.Strength = 0; }
        w.Fleets["big"] = new Fleet { Id = "big", Name = "the test fleet", OwnerId = mine.Id, SystemId = w.Fleets.Values.First().SystemId, Strength = 40 };
        Assert.True(EmergencySystem.ForLifeLean(w, voter, mine, 0) > life); Assert.True(life < 0);
    }

    [Fact]
    public void AnotherHousesConsul_UsesThem_AndAlwaysLaysThemDown()
    {
        var (tc, w, me, mine, ctx) = Start(); Danger(w); var other = Other(w, mine); var consul = Economy.HeadOf(w, other)!; Consul(w, other, consul);
        foreach (var d in ElectionSystem.ConsulOffices(Content.Offices).Skip(1)) if (OfficeSystem.Holding(w, d.Id) is { } h2 && h2.HouseId == other.Id) OfficeSystem.Strip(w, d, "test", false);
        consul = EmergencySystem.ConsulOf(w, Content.Offices, other.Id)!; mine.Treasury.Credits = 50; int standing = other.Legitimacy;
        EmergencySystem.GrantVoted(w, other, null, Content.Offices, passed: true);
        Assert.False(w.HasFlag(EmergencySystem.MineFlag)); Assert.True(Eligible(w, "emergency_powers_voted"));
        // a levy without a vote, the year after
        mine.Treasury.Credits = 50; w.Turn++; EmergencySystem.Tick(w, Content.Offices);
        Assert.Equal(50 - EmergencySystem.LevyCredits, mine.Treasury.Credits); Assert.True(w.HasFlag(EmergencySystem.LeviedFlag)); Assert.Equal(1, w.Counter(EmergencySystem.LeviesKey));
        Assert.True(Eligible(w, "the_levy_without_a_vote"));
        // the term runs out, and they are laid down without being asked
        for (int y = 0; y < 6 && w.Emergency != null; y++) { w.Turn++; EmergencySystem.Tick(w, Content.Offices); }
        Assert.Null(w.Emergency); Assert.Equal(1, w.Counter(EmergencySystem.LaidDownKey)); Assert.Equal(0, w.Counter(EmergencySystem.KeptKey)); Assert.False(w.IsImperium);
        Assert.Equal(Math.Min(100, standing + EmergencySystem.LaidDownStanding), other.Legitimacy); Assert.Contains(EmergencySystem.LaidDownChar, consul.Flags); Assert.DoesNotContain(EmergencySystem.HoldsChar, consul.Flags);
        Assert.True(Eligible(w, "the_powers_laid_down"));
        // and not again for some years
        Assert.Matches("[0-9]+ years? ago", EmergencySystem.GrantRefusal(w, other, Content.Offices));
        // the danger passing ends them early
        w.WorldCounters.Remove(EmergencySystem.EndedKey); EmergencySystem.GrantVoted(w, other, null, Content.Offices, passed: true); Danger(w, false);
        w.Turn++; EmergencySystem.Tick(w, Content.Offices); Assert.Null(w.Emergency); Assert.Equal(2, w.Counter(EmergencySystem.LaidDownKey));
        // no house but the player's is ever asked to be given them for life
        Danger(w); w.WorldCounters.Remove(EmergencySystem.EndedKey); EmergencySystem.GrantVoted(w, other, null, Content.Offices, passed: true); w.Emergency!.Due = true;
        Assert.Equal("no other house would put that to the Senate", EmergencySystem.ForLifeRefusal(w, other));
    }

    [Fact]
    public void ThePlayer_MayLayThemDown_AndIsRememberedForIt()
    {
        var (tc, w, me, mine, ctx) = Start(); Danger(w); Consul(w, mine, me); var other = Other(w, mine); var head = Economy.HeadOf(w, other)!;
        EmergencySystem.GrantVoted(w, mine, null, Content.Offices, passed: true); mine.Legitimacy = 50; int regard = w.GetRelationship(head.Id, me.Id)?.Disposition ?? 0; int trust = LadderSystem.Trust(w);
        // the levy is the player's to call
        other.Treasury.Credits = 10; int before = mine.Treasury.Credits;
        Assert.True(Do(w, new EmergencyCommand(me.Id, EmergencyCommand.Levy))); Assert.True(mine.Treasury.Credits > before); Assert.Equal(10 - EmergencySystem.LevyCredits, other.Treasury.Credits);
        Assert.Equal("the houses have been levied this year", new EmergencyCommand(me.Id, EmergencyCommand.Levy).Refusal(ctx));
        // one motion a year may be struck
        Assert.True(ElectionSystem.MayVeto(w, Content.Offices, mine.Id)); w.Emergency!.VetoTurn = w.Turn; Assert.False(ElectionSystem.MayVeto(w, Content.Offices, mine.Id));
        // laid down before the term is up
        Assert.True(Do(w, new EmergencyCommand(me.Id, EmergencyCommand.LayDown)));
        Assert.Null(w.Emergency); Assert.Equal(50 + EmergencySystem.LaidDownStanding + 2, mine.Legitimacy); Assert.Equal(Math.Clamp(trust - 1 + EmergencySystem.LaidDownTrust, 0, 100), LadderSystem.Trust(w));
        Assert.Equal(regard + EmergencySystem.LevyRegard + EmergencySystem.LaidDownRegard, w.GetRelationship(head.Id, me.Id)!.Disposition);
        Assert.Contains(me.Life!.Deeds, d => d.Text.Contains("Laid down the emergency powers before the term was up")); Assert.True(Eligible(w, "you_laid_them_down"));
        Assert.Equal("you do not hold the emergency powers", new EmergencyCommand(me.Id, EmergencyCommand.LayDown).Refusal(ctx));
    }

    [Fact]
    public void ThePlayer_MayKeepThem_AndItCostsEveryYear_UntilTheSenateTakesThem()
    {
        var (tc, w, me, mine, ctx) = Start(); Danger(w); Consul(w, mine, me); var other = Other(w, mine); var head = Economy.HeadOf(w, other)!;
        EmergencySystem.GrantVoted(w, mine, null, Content.Offices, passed: true); w.Emergency!.LevyTurn = 999;
        for (int y = 0; y < EmergencySystem.Years; y++) { w.Turn++; EmergencySystem.Tick(w, Content.Offices); }
        Assert.True(w.Emergency!.Due); Assert.True(w.HasFlag(EmergencySystem.DueFlag)); Assert.True(Eligible(w, "the_term_is_up")); Assert.Equal(0, w.Emergency.KeptYears);
        Assert.Equal(0, EmergencySystem.HolderLean(w, mine.Id));   // the Senate stops deferring on the day they are due
        mine.Legitimacy = 50; int regard = w.GetRelationship(head.Id, me.Id)?.Disposition ?? 0, trust = LadderSystem.Trust(w);
        w.Turn++; EmergencySystem.Tick(w, Content.Offices);
        Assert.Equal(1, w.Emergency!.KeptYears); Assert.Equal(50 + EmergencySystem.KeptStanding, mine.Legitimacy); Assert.Equal(Math.Max(0, trust + EmergencySystem.KeptTrust), LadderSystem.Trust(w));
        Assert.Equal(regard + EmergencySystem.KeptRegard, w.GetRelationship(head.Id, me.Id)!.Disposition); Assert.True(Eligible(w, "a_year_past_leave"));
        // told to give way through the report, they are laid down at the year's end, late, and nothing is gained by it
        var (tc2, w2, me2, mine2, _) = Start(); Danger(w2); Consul(w2, mine2, me2); EmergencySystem.GrantVoted(w2, mine2, null, Content.Offices, passed: true);
        w2.Emergency!.Due = true; w2.Emergency.KeptYears = 1; mine2.Legitimacy = 50; w2.WorldFlags.Add(EmergencySystem.LayFlag); w2.Turn++; EmergencySystem.Tick(w2, Content.Offices);
        Assert.Null(w2.Emergency); Assert.Equal(50, mine2.Legitimacy);
        // kept three years, the Senate takes them
        for (int y = 0; y < 4 && w.Emergency != null; y++) { w.Turn++; EmergencySystem.Tick(w, Content.Offices); }
        Assert.Null(w.Emergency); Assert.Equal(1, w.Counter(EmergencySystem.StrippedKey)); Assert.Equal(EmergencySystem.KeptMax, w.Counter(EmergencySystem.KeptKey)); Assert.True(w.HasFlag(EmergencySystem.StrippedFlag));
        Assert.True(Eligible(w, "stripped_of_the_powers")); Assert.True(w.Counter(EmergencySystem.KeptOnceKey(mine.Id)) > 0); Assert.False(w.IsImperium);
        Assert.Contains(me.Life!.Deeds, d => d.Kind == "failure" && d.Text.Contains("stripped"));
    }

    [Fact]
    public void AskedForMoreTime_TheSenateMayExtendThem_OrRefuse()
    {
        var (tc, w, me, mine, ctx) = Start(); Danger(w); Consul(w, mine, me); EmergencySystem.GrantVoted(w, mine, null, Content.Offices, passed: true);
        w.Turn += EmergencySystem.Years - 1; Assert.Null(EmergencySystem.GrantRefusal(w, mine, Content.Offices));   // in the last year of the term
        EmergencySystem.GrantVoted(w, mine, null, Content.Offices, passed: true);
        Assert.Equal(1, w.Emergency!.Renewals); Assert.Equal(w.Turn + EmergencySystem.Years, w.Emergency.Until); Assert.Equal(1, w.Counter(EmergencySystem.RenewedKey));
        w.Turn = w.Emergency.Until - 1; EmergencySystem.GrantVoted(w, mine, null, Content.Offices, passed: false); Assert.True(w.Emergency.Due); Assert.True(w.HasFlag(EmergencySystem.DueFlag));
        Danger(w, false); Assert.Contains("the danger is past", EmergencySystem.GrantRefusal(w, mine, Content.Offices));
    }

    [Fact]
    public void ThePowersForLife_AreTheSenatesToGive_AndThatEndsTheRepublicByItsOwnVote()
    {
        var (tc, w, me, mine, ctx) = Start(); Danger(w); Consul(w, mine, me);
        Assert.Contains("only the one who holds", EmergencySystem.ForLifeRefusal(w, mine));
        EmergencySystem.GrantVoted(w, mine, null, Content.Offices, passed: true);
        Assert.Contains("not in a first term", EmergencySystem.ForLifeRefusal(w, mine));
        w.Emergency!.Renewals = 1; Assert.Null(EmergencySystem.ForLifeRefusal(w, mine));
        // end to end: moved by the player, carried by a Senate that thinks the world of them
        w.PoolsFor(me.Id).Influence = 9;
        foreach (var h in w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId)) if (Economy.HeadOf(w, h) is { } head) w.Relationship(head.Id, me.Id).Disposition = 100;
        var cmd = new ProposeMotionCommand(me.Id, "for_life", mine.Id, tc.Senate); Assert.Null(cmd.Refusal(new CommandContext(w, TestContent.Rng())) is { } no && !no.Contains("Senate hears") ? no : null);
        if (!Do(w, cmd)) w.Motions.Add(new Motion { Id = "life", Kind = "for_life", ProposerHouseId = mine.Id, TargetId = mine.Id, ProposedTurn = w.Turn });
        var motion = w.Motions.Single(m => m.Kind == "for_life"); var (f, a) = tc.Senate.Tally(w, motion); Assert.True(f > a, $"for {f}, against {a}");
        w.Turn++; tc.Senate.Tick(w);
        Assert.True(w.IsImperium); Assert.Equal(Praetoria.Core.Progression.Title.Emperor, mine.Title); Assert.Null(w.Emergency);
        Assert.Equal(1, w.Counter(EmergencySystem.ByVoteKey)); Assert.Equal(1, w.Counter("republics_ended")); Assert.True(w.HasFlag(EmergencySystem.ForLifeFlag)); Assert.True(w.HasFlag(ThroneSystem.ProclaimedFlag));
        // refused, the Senate ends the powers in the same breath
        var (tc2, w2, me2, mine2, _) = Start(); Danger(w2); Consul(w2, mine2, me2); EmergencySystem.GrantVoted(w2, mine2, null, Content.Offices, passed: true); w2.Emergency!.Due = true; mine2.Legitimacy = 50;
        EmergencySystem.ForLifeVoted(w2, mine2, passed: false);
        Assert.Null(w2.Emergency); Assert.False(w2.IsImperium); Assert.Equal(50 + EmergencySystem.StrippedStanding, mine2.Legitimacy); Assert.Equal(1, w2.Counter(EmergencySystem.ForLifeRefusedKey)); Assert.Equal(1, w2.Counter(EmergencySystem.StrippedKey));
    }

    [Fact]
    public void TheyEndWithTheChair_AndTheElectionThatWasOwedIsHeld()
    {
        var (tc, w, me, mine, ctx) = Start(); Danger(w); var other = Other(w, mine); var consul = Economy.HeadOf(w, other)!; Consul(w, other, consul);
        EmergencySystem.GrantVoted(w, other, null, Content.Offices, passed: true); var holder = EmergencySystem.Holder(w)!;
        w.Emergency!.SkippedElection = true; DynastySystem.Die(w, holder.Id);
        w.Turn++; EmergencySystem.Tick(w, Content.Offices);
        Assert.Null(w.Emergency); Assert.Equal(1, w.Counter(ElectionSystem.OwedKey));
        w.Turn = 13; Assert.True(ElectionSystem.Due(w)); ElectionSystem.Hold(w, Content.Offices, id => 1); Assert.Equal(0, w.Counter(ElectionSystem.OwedKey)); Assert.False(ElectionSystem.Due(w));
    }

    [Fact]
    public void OtherHouses_MoveIt_ForAConsulOfTheirs_WhenTheReachIsInDanger()
    {
        int moved = 0, never = 0;
        for (int seed = 1; seed <= 6; seed++)
        {
            var (tc, w, me, mine, ctx) = Start(seed: seed); Danger(w, false); w.WorldCounters[GreatCrisisSystem.BeganKey] = w.Turn;   // the great war: a danger to the Reach itself
            Assert.Equal(3, EmergencySystem.Gravity(w));
            for (int y = 0; y < 16 && w.Emergency == null && !w.Motions.Any(m => m.Kind == "emergency"); y++) { w.Turn++; w.RngState += 7; tc.Senate.Tick(w); }
            if (w.Counter(EmergencySystem.MovedKey) > 0) { moved++; Assert.All(w.Motions.Where(m => m.Kind == "emergency"), m => { Assert.NotEqual(mine.Id, m.TargetId); Assert.NotEqual(mine.Id, m.ProposerHouseId); }); } else never++;
        }
        Assert.True(moved >= 3, $"moved in {moved} of 6");
        // and with no danger, never
        var (tcq, wq, _, _, _) = Start(); Danger(wq, false);
        for (int y = 0; y < 12; y++) { wq.Turn++; wq.RngState += 7; tcq.Senate.Tick(wq); if (EmergencySystem.Danger(wq) != null) break; }
        Assert.True(wq.Counter(EmergencySystem.MovedKey) == 0 || EmergencySystem.Danger(wq) != null);
    }
}
