using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The chair (docs/LADDER.md, "The chair"; docs/GAPS.md O2): the Speaker orders the real docket, the presiding consul speaks
/// from the chair and has the casting vote, and the Treasurer's budget has figures drawn from the state of the Reach.</summary>
public class ChairTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static OfficeCatalog Offices => Content.Offices;
    private static (TurnController tc, World w, Character me, House mine, CommandContext ctx) Start(string scenario = "the_old_lion", int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content); tc.BeginTurn();
        return (tc, w, w.Protagonist!, w.House(w.Protagonist!.HouseId)!, new CommandContext(w, TestContent.Rng()));
    }
    private static List<House> Others(World w, House mine) =>
        w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId && PolitySystem.InReach(w, h.Id) && Economy.HeadOf(w, h) != null).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
    private static void Seat(World w, Character who, string office)
    {
        w.Offices.RemoveAll(o => o.OfficeId == office || o.CharacterId == who.Id);
        w.Offices.Add(new OfficeHolding { OfficeId = office, HouseId = who.HouseId, CharacterId = who.Id, SinceTurn = w.Turn });
    }
    /// <summary>A motion that is to be voted at the end of this year.</summary>
    private static Motion Due(TurnController tc, World w, string by, string kind, string target = "")
    {
        var m = tc.Senate.Add(w, by, kind, target); m.ProposedTurn = w.Turn - 1; return m;
    }
    private static int Regard(World w, House of, House toward) => w.GetRelationship(Economy.HeadOf(w, of)!.Id, Economy.HeadOf(w, toward)!.Id)?.Disposition ?? 0;
    private static OfficeBusiness Charged(World w, Character who, string office)
    {
        Seat(w, who, office); w.OfficeWork.RemoveAll(b => b.Kind == LadderSystem.Charge);
        LadderSystem.Tick(w, Offices, Content.Policies); return LadderSystem.DutyOf(w, who.Id, LadderSystem.Charge)!;
    }
    private static bool Do(TurnController tc, CommandContext ctx, Character me, string choice) => tc.Executor.TryExecute(new DutyCommand(me.Id, LadderSystem.Charge, choice, Offices, Content.Policies), ctx);
    private static void Fires(World w, string eventId)
    {
        var me = w.Protagonist!; w.House(me.HouseId)!.Treasury.Credits = 100; w.PoolsFor(me.Id).Influence = 9;
        var engine = new Praetoria.Core.Events.EventEngine(Content, WorldBuilder.RngFor(w));
        var found = engine.GatherEligible(w).FirstOrDefault(e => e.def.Id == eventId);
        Assert.True(found.def != null, $"{eventId} is not eligible");
        foreach (var c in found.def!.Choices)
        {
            var again = engine.GatherEligible(w).FirstOrDefault(e => e.def.Id == eventId); if (again.def == null) break;
            engine.Resolve(new Praetoria.Core.Events.FiredEvent(again.def, again.binding), w, c.Id);
        }
    }

    [Fact]
    public void The_speaker_holds_a_motion_over_a_year_once_a_year_and_a_motion_only_once()
    {
        var (tc, w, me, mine, ctx) = Start(); var others = Others(w, mine); w.Motions.Clear();
        var a = Due(tc, w, others[0].Id, "censure", others[1].Id); var b = Due(tc, w, others[2].Id, "levy");
        // not the Speaker: not theirs to do
        Assert.Contains("only the Speaker", new ChairCommand(me.Id, a.Id, LadderSystem.HoldMotion, tc.Senate).Refusal(ctx));
        Seat(w, me, "speaker"); int moverBefore = Regard(w, others[0], mine), sparedBefore = Regard(w, others[1], mine);
        Assert.True(tc.Executor.TryExecute(new ChairCommand(me.Id, a.Id, LadderSystem.HoldMotion, tc.Senate), ctx));
        Assert.True(a.HeldOver); Assert.Equal(w.Turn, a.ProposedTurn); Assert.Equal(1, w.Counter(LadderSystem.HeldCounter));
        Assert.Equal(moverBefore + LadderSystem.HeldRegard, Regard(w, others[0], mine)); Assert.Equal(sparedBefore + LadderSystem.SparedRegard, Regard(w, others[1], mine));
        // one a year
        Assert.Contains("this year", new ChairCommand(me.Id, b.Id, LadderSystem.HoldMotion, tc.Senate).Refusal(ctx));
        // the year's vote does not reach it; the other is decided
        tc.Senate.Tick(w);
        Assert.False(a.Resolved); Assert.True(b.Resolved);
        // next year it is due, and cannot be put off again
        w.Turn++; Seat(w, me, "speaker");
        Assert.Contains("once already", new ChairCommand(me.Id, a.Id, LadderSystem.HoldMotion, tc.Senate).Refusal(ctx));
        tc.Senate.Tick(w); Assert.True(a.Resolved);
        Assert.Contains(me.Life!.Deeds, d => d.Text.Contains("Held a motion over"));
    }

    [Fact]
    public void The_presiding_consul_speaks_from_the_chair_and_the_houses_lean_with_it()
    {
        var (tc, w, me, mine, ctx) = Start(); var others = Others(w, mine); w.Motions.Clear();
        var m = Due(tc, w, others[0].Id, "censure", others[1].Id);
        Assert.Contains("presiding consul", new ChairCommand(me.Id, m.Id, LadderSystem.SpeakFor, tc.Senate).Refusal(ctx));
        Seat(w, me, "consul_junior"); Assert.Equal(me.Id, LadderSystem.Presiding(w)!.Id);   // the junior presides when there is no senior
        var bystanders = others.Skip(2).ToList(); var before = bystanders.ToDictionary(h => h.Id, h => tc.Senate.RawLean(w, m, h.Id));
        int mover = tc.Senate.RawLean(w, m, others[0].Id), target = tc.Senate.RawLean(w, m, others[1].Id);
        Assert.True(tc.Executor.TryExecute(new ChairCommand(me.Id, m.Id, LadderSystem.SpeakAgainst, tc.Senate), ctx)); Assert.Equal(-1, m.Chair);
        foreach (var h in bystanders) Assert.Equal(before[h.Id] - (FactionSystem.Of(w, h.Id) == null ? LadderSystem.ChairLeanUnbenched : LadderSystem.ChairLean), tc.Senate.RawLean(w, m, h.Id));
        Assert.Equal(mover, tc.Senate.RawLean(w, m, others[0].Id)); Assert.Equal(target, tc.Senate.RawLean(w, m, others[1].Id));   // the mover and the target are not moved by it
        // once a year, and once on a motion
        var second = Due(tc, w, others[3].Id, "levy");
        Assert.Contains("once this year", new ChairCommand(me.Id, second.Id, LadderSystem.SpeakFor, tc.Senate).Refusal(ctx));
        w.Turn++; Assert.Contains("has spoken on it", new ChairCommand(me.Id, m.Id, LadderSystem.SpeakFor, tc.Senate).Refusal(ctx));
        // a senior consul takes the chair from the junior
        Seat(w, Economy.HeadOf(w, others[4])!, "consul_senior"); Assert.Contains("presiding consul", new ChairCommand(me.Id, second.Id, LadderSystem.SpeakFor, tc.Senate).Refusal(ctx));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(-1, false)]
    public void A_tied_senate_goes_as_the_presiding_consul_voted(int consulVotes, bool carries)
    {
        var (tc, w, _, mine, _) = Start(); var houses = Others(w, mine); w.Motions.Clear(); w.Offices.Clear();
        // find a house that, seated as senior consul, weighs exactly what some other house weighs
        House? chair = null, rival = null;
        foreach (var h in houses)
        {
            w.Offices.Clear(); Seat(w, Economy.HeadOf(w, h)!, "consul_senior"); int wt = tc.Senate.Weight(w, h.Id);
            rival = houses.FirstOrDefault(x => x.Id != h.Id && tc.Senate.Weight(w, x.Id) == wt && wt > 0);
            if (rival != null) { chair = h; break; }
        }
        Assert.True(chair != null && rival != null, "no two houses of equal weight to tie the Senate with");
        var m = Due(tc, w, rival!.Id, "levy");
        foreach (var h in w.Houses.Values) m.Votes[h.Id] = 0;
        m.Votes[chair!.Id] = consulVotes; m.Votes[rival.Id] = -consulVotes;
        var (f, a) = tc.Senate.Tally(w, m); Assert.Equal(f, a); Assert.True(f > 0);
        tc.Senate.Tick(w);
        Assert.True(m.Resolved); Assert.Equal(carries, m.Passed); Assert.Equal(carries ? 1 : 0, w.Counter(LadderSystem.CastingCounter));
        if (carries) Assert.Contains(w.History, h => h.Text.Contains("casting vote"));
    }

    [Fact]
    public void The_treasurers_budget_has_figures_drawn_from_the_state_of_the_reach()
    {
        var (tc, w, me, mine, ctx) = Start(); var chair = RegionSystem.CapitalHolder(w)!;
        int landed = w.Houses.Values.Count(h => h.Id != PirateSystem.HouseId && PolitySystem.InReach(w, h.Id) && w.HoldingsOf(h.Id).Any(x => x.IsPlanet));
        // a quiet year: nothing lost, nothing called for but the hulls
        foreach (var r in w.Regions.Values) { r.Lawlessness = 0; r.Allegiance = 80; }
        foreach (var fl in w.Fleets.Values) fl.Office = false;
        var c = Charged(w, me, "treasurer"); var f = LadderSystem.BudgetOf(c)!;
        Assert.Equal(landed * LadderSystem.LevyEach, f.Assessed); Assert.Equal(0, f.Lost); Assert.Equal(0, f.Wanted); Assert.Equal(f.Assessed, f.Surplus);
        Assert.Contains($"{f.Collected} credits came in", LadderSystem.DutySays(w, Content.Policies, c));
        chair.Treasury.Credits = 40; int banked = LadderSystem.BalanceSum(c); Assert.Equal(Math.Clamp((f.Surplus + 1) / 2, 1, 12), banked);
        Assert.True(Do(tc, ctx, me, LadderSystem.Balance)); Assert.Equal(40 + banked, chair.Treasury.Credits);
        // a bad year: the lanes are lawless and the regions loose, so less comes in and more is called for
        w.Turn++; foreach (var r in w.Regions.Values) { r.Lawlessness = 60; r.Allegiance = 30; }
        var reachFleet = w.Fleets.Values.OrderBy(x => x.Id, StringComparer.Ordinal).First(); reachFleet.Office = true; reachFleet.Readiness = 80;
        c = Charged(w, me, "treasurer"); f = LadderSystem.BudgetOf(c)!;
        Assert.True(f.Lost > 0); Assert.Equal(w.Regions.Count * LadderSystem.LaneCall, f.Lanes); Assert.Equal(w.Regions.Count * LadderSystem.ReliefCall, f.Relief); Assert.Equal(reachFleet.Strength, f.Fleet);
        Assert.True(f.Surplus < 0); Assert.Contains("short", LadderSystem.DutySays(w, Content.Policies, c)); Assert.Equal(0, LadderSystem.BalanceSum(c));
        chair.Treasury.Credits = 40; Assert.True(Do(tc, ctx, me, LadderSystem.Balance));
        Assert.Equal(40, chair.Treasury.Credits); Assert.Equal(80 - LadderSystem.FleetShort, reachFleet.Readiness);   // the books square, and the fleet goes short
        // forgiving a region costs what its houses would have paid; the contracts are a third of what the year calls for
        w.Turn++; c = Charged(w, me, "treasurer"); f = LadderSystem.BudgetOf(c)!;
        Assert.Equal(Math.Clamp(f.Region, 2, 12), LadderSystem.RemitSum(c)); Assert.Equal(Math.Clamp(f.Wanted / 3, 6, 18), LadderSystem.ContractSum(c));
        chair.Treasury.Credits = 40; int purse = chair.Treasury.Credits; Assert.True(Do(tc, ctx, me, LadderSystem.Remit)); Assert.Equal(purse - LadderSystem.RemitSum(c), chair.Treasury.Credits);
    }

    [Fact]
    public void Giving_an_enemy_no_day_holds_over_what_they_moved()
    {
        var (tc, w, me, mine, ctx) = Start(); var others = Others(w, mine); w.Motions.Clear();
        var foe = others[0]; var foeHead = Economy.HeadOf(w, foe)!;
        w.Relationship(me.Id, foeHead.Id).Disposition = -40; w.Relationship(foeHead.Id, me.Id).Disposition = -40;
        var c = Charged(w, me, "speaker"); Assert.Equal(foe.Id, c.HouseA);
        var theirs = tc.Senate.Add(w, foe.Id, "levy", ""); var another = tc.Senate.Add(w, others[1].Id, "censure", others[2].Id); int due = theirs.ProposedTurn;
        Assert.True(Do(tc, ctx, me, LadderSystem.Silence));
        Assert.True(theirs.HeldOver); Assert.Equal(due + 1, theirs.ProposedTurn); Assert.False(another.HeldOver);
    }

    [Fact]
    public void A_speaker_of_another_house_holds_over_what_is_aimed_at_their_own_and_a_consul_speaks_against_it()
    {
        var (tc, w, me, mine, _) = Start(); var others = Others(w, mine); w.Motions.Clear(); w.Offices.Clear();
        var speaker = others[0]; Seat(w, Economy.HeadOf(w, speaker)!, "speaker");
        var m = Due(tc, w, mine.Id, "censure", speaker.Id);
        LadderSystem.ChairActs(w, tc.Senate);
        Assert.True(m.HeldOver); Assert.True(w.HasFlag(LadderSystem.HeldMineFlag));
        Fires(w, "my_motion_held_over");
        // and a presiding consul of another house speaks against what is aimed at their own
        w.Turn++; var consul = others[1]; Seat(w, Economy.HeadOf(w, consul)!, "consul_senior");
        var second = Due(tc, w, mine.Id, "censure", consul.Id);
        LadderSystem.ChairActs(w, tc.Senate);
        Assert.Equal(-1, second.Chair); Assert.Equal(1, w.Counter(LadderSystem.SpokeCounter));
    }
}
