using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Ambitions (docs/AMBITIONS.md): the people of the house go after what they want by themselves, ask the head for help
/// now and then, and succeed or are thwarted.</summary>
public class AmbitionTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static (TurnController tc, World w, Character me, House mine, CommandContext ctx) Start(string scenario = "the_old_lion", int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content); tc.BeginTurn(); w.Protagonist!.Age = Math.Min(w.Protagonist.Age, 45);
        return (tc, w, w.Protagonist!, w.House(w.Protagonist!.HouseId)!, new CommandContext(w, TestContent.Rng()));
    }
    private static Character Kin(World w, House h, Character head, string id, string ambition, int age = 30)
    {
        var c = new Character { Id = id, Name = "Test " + id, HouseId = h.Id, Age = age, Sex = "male", Ambition = ambition, CareerTrack = "military", CareerRank = 1, FatherId = head.Sex == "female" ? "" : head.Id, MotherId = head.Sex == "female" ? head.Id : "" };
        w.Characters[id] = c; h.Members.Add(id); w.Relationship(id, head.Id).Disposition = 30; return c;
    }
    private static void Years(TurnController tc, World w, CommandContext ctx, int n) { for (int i = 0; i < n; i++) { w.Turn++; tc.Ambitions.Tick(w, tc.Executor, ctx); } }
    private static bool Do(TurnController tc, World w, CommandContext ctx, ICommand cmd) => tc.Executor.TryExecute(cmd, ctx);

    [Fact]
    public void One_who_wants_a_command_asks_for_one_and_having_it_is_content()
    {
        var (tc, w, me, mine, ctx) = Start(); var c = Kin(w, mine, me, "officer", "command_a_fleet"); c.Skills["tactics"] = 9;
        var fleet = w.Fleets.Values.Where(f => f.OwnerId == mine.Id).OrderBy(f => f.Id, StringComparer.Ordinal).First(); var sitting = Kin(w, mine, me, "sitting", ""); fleet.AdmiralId = sitting.Id;
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == mine.Id && f.Id != fleet.Id)) f.AdmiralId = me.Id;
        Years(tc, w, ctx, 2);   // not on the first day: they ask once they have been at it a year
        Assert.Equal(AmbitionSystem.Command, c.Pursuit!.Request); Assert.Equal(fleet.Id, c.Pursuit.RequestArg); Assert.True(w.HasFlag(AmbitionSystem.AskingFlag)); Assert.Contains("asks for the command", AmbitionSystem.RequestSays(w, c));
        int regard = w.Relationship(c.Id, me.Id).Disposition;
        Assert.True(Do(tc, w, ctx, new AmbitionCommand(me.Id, AmbitionCommand.Grant, c.Id, tc.Ambitions)));
        Assert.Equal(c.Id, fleet.AdmiralId); Assert.Equal("", c.Pursuit.Request); Assert.Equal(1, c.Pursuit.Helped); Assert.Equal(regard + 12, w.Relationship(c.Id, me.Id).Disposition);
        Assert.True(w.Relationship(sitting.Id, me.Id).Disposition < 30, "the admiral put out minds it");
        Years(tc, w, ctx, 3);
        Assert.True(c.Pursuit.Fulfilled); Assert.True(w.HasFlag(AmbitionSystem.FulfilledFlag)); Assert.Contains(AmbitionSystem.FulfilledChar, c.Flags);
        // and in time they want something else
        Years(tc, w, ctx, AmbitionSystem.RestYears); Assert.NotEqual("command_a_fleet", c.Ambition); Assert.False(c.Pursuit!.Fulfilled);
    }

    [Fact]
    public void A_refusal_is_remembered_and_so_is_no_answer_at_all()
    {
        var (tc, w, me, mine, ctx) = Start(); var c = Kin(w, mine, me, "clerk", "master_the_senate"); c.CareerTrack = "law";
        Years(tc, w, ctx, 2); Assert.Equal(AmbitionSystem.Word, c.Pursuit!.Request);
        int regard = w.Relationship(c.Id, me.Id).Disposition;
        Assert.True(Do(tc, w, ctx, new AmbitionCommand(me.Id, AmbitionCommand.Refuse, c.Id, tc.Ambitions)));
        Assert.Equal(regard - 8, w.Relationship(c.Id, me.Id).Disposition); Assert.Equal(1, c.Pursuit.Refused); Assert.Equal("", c.Pursuit.Request);
        Assert.False(Do(tc, w, ctx, new AmbitionCommand(me.Id, AmbitionCommand.Grant, c.Id, tc.Ambitions)));   // nothing is asked now
        Years(tc, w, ctx, AmbitionSystem.AskEvery); Assert.Equal(AmbitionSystem.Word, c.Pursuit.Request);   // they ask again in time
        regard = w.Relationship(c.Id, me.Id).Disposition; Years(tc, w, ctx, AmbitionSystem.RequestYears);
        Assert.Equal("", c.Pursuit.Request); Assert.Equal(2, c.Pursuit.Refused); Assert.Equal(regard - 5, w.Relationship(c.Id, me.Id).Disposition);
    }

    [Fact]
    public void A_word_spoken_for_them_costs_influence_and_raises_them()
    {
        var (tc, w, me, mine, ctx) = Start(); var c = Kin(w, mine, me, "clerk", "master_the_senate"); c.CareerTrack = "law"; c.CareerRank = 1;
        Years(tc, w, ctx, 2); w.PoolsFor(me.Id).Influence = 0;
        Assert.Equal("no influence", new AmbitionCommand(me.Id, AmbitionCommand.Grant, c.Id, tc.Ambitions).Refusal(ctx));
        w.PoolsFor(me.Id).Influence = 3; Assert.True(Do(tc, w, ctx, new AmbitionCommand(me.Id, AmbitionCommand.Grant, c.Id, tc.Ambitions)));
        Assert.Equal(2, c.CareerRank); Assert.Equal(1, w.PoolsFor(me.Id).Influence);
    }

    [Fact]
    public void One_who_wants_a_fortune_founds_a_venture_with_their_own_money_or_asks_for_a_stake()
    {
        var (tc, w, me, mine, ctx) = Start(); int cheapest = tc.Ambitions.CheapestVenture(); Assert.True(cheapest > 0);
        var rich = Kin(w, mine, me, "rich", "amass_a_fortune"); rich.Purse = cheapest + 10; mine.Treasury.Credits = 300; mine.Treasury.Materials = 200; mine.Treasury.Manpower = 200; mine.Treasury.Influence = 50;
        int ventures = w.Businesses.Values.Count(b => b.OwnerId == mine.Id) + w.Projects.Count(p => p.OwnerId == mine.Id);
        Years(tc, w, ctx, 1);
        Assert.True(w.Businesses.Values.Count(b => b.OwnerId == mine.Id) + w.Projects.Count(p => p.OwnerId == mine.Id) > ventures, rich.Pursuit!.Note);
        Assert.True(rich.Purse < cheapest + 10, "it was their own money"); Assert.True(w.HasFlag(AmbitionSystem.FoundedFlag)); Assert.Contains("Founded", rich.Pursuit.Note);
        Assert.True(w.Founders.Values.Contains(rich.Id) || w.Businesses.Values.Any(b => b.FounderId == rich.Id));
        var again = SaveSystem.Deserialize(SaveSystem.Serialize(w)); Assert.Equal(w.Founders.Count, again.Founders.Count);   // who began it survives a save

        var poor = Kin(w, mine, me, "poor", "amass_a_fortune"); poor.Purse = 0;
        Years(tc, w, ctx, 2); Assert.Equal(AmbitionSystem.Stake, poor.Pursuit!.Request); int stake = int.Parse(poor.Pursuit.RequestArg); int treasury = mine.Treasury.Credits;
        Assert.True(Do(tc, w, ctx, new AmbitionCommand(me.Id, AmbitionCommand.Grant, poor.Id, tc.Ambitions)));
        Assert.True(mine.Treasury.Credits < treasury, "the house paid"); Assert.True(mine.Treasury.Credits >= treasury - stake); Assert.Equal(1, poor.Pursuit.Helped);
    }

    [Fact]
    public void One_who_wants_the_house_restored_puts_their_own_purse_into_it()
    {
        var (tc, w, me, mine, ctx) = Start(); var c = Kin(w, mine, me, "dutiful", "restore_house_fortunes"); mine.Treasury.Credits = 0; int given = 0;
        for (int i = 0; i < 6 && !c.Pursuit?.Fulfilled == true || i == 0; i++) { c.Purse = 45; int before = mine.Treasury.Credits; Years(tc, w, ctx, 1); given += Math.Max(0, mine.Treasury.Credits - before); mine.Treasury.Credits = 0; if (c.Pursuit!.Fulfilled) break; }
        Assert.True(c.Pursuit!.Given >= AmbitionSystem.GivenAt, $"gave {c.Pursuit.Given}"); Assert.True(c.Pursuit.Fulfilled); Assert.True(given > 0);
    }

    [Fact]
    public void One_who_wants_the_seat_asks_to_be_named_heir_and_a_named_heir_stands_first()
    {
        var (tc, w, me, mine, ctx) = Start(); foreach (var k in w.LivingCharacters().Where(k => k.HouseId == mine.Id && k.Id != me.Id).ToList()) k.Alive = false;
        var elder = Kin(w, mine, me, "elder", "", 40); var able = Kin(w, mine, me, "able", "seize_the_throne", 28); able.Skills["tactics"] = 6; able.Skills["oratory"] = 6; able.CareerRank = 3;
        Assert.Equal(elder.Id, DynastySystem.FindHeir(w, me)!.Id);
        Years(tc, w, ctx, 2); Assert.Equal(AmbitionSystem.Heir, able.Pursuit!.Request);
        int legit = mine.Legitimacy; int elderRegard = w.Relationship(elder.Id, me.Id).Disposition;
        Assert.True(Do(tc, w, ctx, new AmbitionCommand(me.Id, AmbitionCommand.Grant, able.Id, tc.Ambitions)));
        Assert.Equal(able.Id, mine.NamedHeirId); Assert.Equal(able.Id, DynastySystem.FindHeir(w, me)!.Id); Assert.Equal(Math.Max(0, legit - 2), mine.Legitimacy);
        Assert.Equal(elderRegard - 20, w.Relationship(elder.Id, me.Id).Disposition); Assert.True(w.HasFlag(AmbitionSystem.HeirNamedFlag));
        Years(tc, w, ctx, 1); Assert.True(able.Pursuit.Fulfilled);   // they stand to inherit
        // the head may name nobody again, or anyone, without being asked
        Assert.True(Do(tc, w, ctx, new AmbitionCommand(me.Id, AmbitionCommand.NameHeir, "", tc.Ambitions))); Assert.Equal(elder.Id, DynastySystem.FindHeir(w, me)!.Id);
        Assert.True(Do(tc, w, ctx, new AmbitionCommand(me.Id, AmbitionCommand.NameHeir, able.Id, tc.Ambitions))); able.Alive = false; Years(tc, w, ctx, 1); Assert.Equal("", mine.NamedHeirId);
    }

    [Fact]
    public void Wanting_it_for_years_with_nothing_to_show_and_a_head_who_said_no_leaves_them_thwarted()
    {
        var (tc, w, me, mine, ctx) = Start(); var c = Kin(w, mine, me, "clerk", "master_the_senate"); c.CareerTrack = "law"; c.CareerRank = 0;
        for (int i = 0; i < AmbitionSystem.ThwartedAfter + 2; i++) { Years(tc, w, ctx, 1); c.CareerRank = 0; c.Skills.Clear(); if (c.Pursuit!.Request.Length > 0) tc.Ambitions.Answer(w, me, c, false, tc.Executor, ctx); }
        Assert.Contains(AmbitionSystem.ThwartedChar, c.Flags); Assert.True(w.Relationship(c.Id, me.Id).Disposition < 30); Assert.True(c.Pursuit!.Refused >= 2);
    }

    [Fact]
    public void A_deferential_family_pursues_nothing_and_everyone_else_has_something_to_say_for_their_year()
    {
        var (tc, w, me, mine, ctx) = Start(); w.FamilyAutonomy = 0; var c = Kin(w, mine, me, "quiet", "command_a_fleet"); Years(tc, w, ctx, 3); Assert.Null(c.Pursuit);
        var (tc2, w2, me2, mine2, ctx2) = Start(); Years(tc2, w2, ctx2, 2);
        foreach (var k in AmbitionSystem.Members(w2, mine2).Where(k => k.Ambition.Length > 0)) { Assert.NotNull(k.Pursuit); Assert.Equal(k.Ambition, k.Pursuit!.Goal); Assert.InRange(k.Pursuit.Progress, 0, 100); Assert.NotEqual("nothing in particular", AmbitionSystem.Wants(k.Ambition)); }
        _ = me2;
    }
}
