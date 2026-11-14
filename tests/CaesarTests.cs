using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The Caesar road (docs/CAESAR.md): crews that follow a commander, the triumph, the recall, the march on the capital,
/// the civil war and how it ends.</summary>
public class CaesarTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static (TurnController tc, World w, Character me, House mine, CommandContext ctx) Start(string scenario = "the_old_lion", int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content); tc.BeginTurn(); w.Protagonist!.Age = Math.Min(w.Protagonist.Age, 45);
        return (tc, w, w.Protagonist!, w.House(w.Protagonist!.HouseId)!, new CommandContext(w, TestContent.Rng()));
    }
    /// <summary>A fleet of the house under this commander, its crews already his.</summary>
    private static Fleet Legion(World w, House h, Character c, int hulls = 10, int devotion = 80)
    {
        var at = w.HoldingsOf(h.Id).FirstOrDefault(x => x.IsPlanet)?.SystemId ?? w.Galaxy!.Systems.Keys.OrderBy(k => k, StringComparer.Ordinal).First();
        foreach (var other in w.Fleets.Values.Where(x => x.AdmiralId == c.Id)) other.AdmiralId = "";
        var f = new Fleet { Id = "legion_" + c.Id, Name = "The Tenth", OwnerId = h.Id, SystemId = at, HomeSystemId = at, Strength = hulls, AdmiralId = c.Id, DevotedTo = c.Id, Devotion = devotion };
        w.Fleets[f.Id] = f; return f;
    }
    private static void Name(World w, Character c, int valour = 5) { w.WorldCounters[FleetSystem.ValourKey(c.Id)] = valour; c.Flags.Add(FleetSystem.AchillesFlag); c.CareerTrack = "military"; c.CareerRank = Math.Max(c.CareerRank, 3); }
    private static House Rival(World w, House mine) => w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId && Economy.HeadOf(w, h) != null && h.Id != RegionSystem.CapitalHolder(w)?.Id)
        .OrderByDescending(h => FleetSystem.StrengthOf(w, h.Id)).ThenBy(h => h.Id, StringComparer.Ordinal).First();

    [Fact]
    public void Crews_take_to_the_commander_who_wins_with_them_and_resent_losing_him()
    {
        var (tc, w, me, mine, _) = Start(); var f = Legion(w, mine, me, devotion: 30);
        CaesarSystem.Victory(w, mine.Id, "nobody", f.SystemId);
        Assert.True(f.Devotion > 30); Assert.Equal(1, CaesarSystem.Laurels(w, me)); Assert.Equal(0, CaesarSystem.DevotedHulls(w, me));
        f.Devotion = 80; Assert.Equal(10, CaesarSystem.DevotedHulls(w, me)); Assert.True(CaesarSystem.Sway(w, me) >= 20);
        // another is put over them: they start again with him, and think less of the house for it
        var other = w.LivingCharacters().First(c => c.HouseId == mine.Id && c.Id != me.Id && c.Age >= 18); int loyal = f.Loyalty;
        f.AdmiralId = other.Id; tc.Caesar.Tick(w);
        Assert.Equal(other.Id, f.DevotedTo); Assert.True(f.Devotion < 60); Assert.True(f.Loyalty < loyal);
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w)); Assert.Equal(f.Devotion, back.Fleets[f.Id].Devotion); Assert.Equal(other.Id, back.Fleets[f.Id].DevotedTo);
    }

    [Fact]
    public void A_triumph_asks_three_victories_and_the_senate_may_refuse_it()
    {
        var (tc, w, me, mine, ctx) = Start(); Legion(w, mine, me);
        Assert.Contains("victories", tc.Caesar.TriumphRefusal(w, mine));
        w.WorldCounters[CaesarSystem.LaurelKey(me.Id)] = 3; Assert.Null(tc.Caesar.TriumphRefusal(w, mine));
        Assert.Null(new ProposeMotionCommand(me.Id, "triumph", mine.Id, tc.Senate).Refusal(ctx) is { } no && !no.Contains("Senate hears") && !no.Contains("influence") ? no : null);
        int legit = mine.Legitimacy;
        tc.Caesar.TriumphVoted(w, mine, null, passed: true);
        Assert.Equal(1, CaesarSystem.Triumphs(w, me)); Assert.Equal(0, CaesarSystem.Laurels(w, me)); Assert.True(mine.Legitimacy >= Math.Min(100, legit + 1) || legit == 100);
        Assert.Contains(CaesarSystem.TriumphatorChar, me.Flags); Assert.True(w.HasFlag(CaesarSystem.TriumphFlag)); Assert.Contains(me.Life!.Deeds, d => d.Kind == "triumph");
        // refused: nothing is given, and it is a cause
        w.WorldCounters[CaesarSystem.LaurelKey(me.Id)] = 3; Assert.Null(CaesarSystem.Grievance(w, mine));
        tc.Caesar.TriumphVoted(w, mine, null, passed: false);
        Assert.Equal(1, CaesarSystem.Triumphs(w, me)); Assert.NotNull(CaesarSystem.Grievance(w, mine)); Assert.True(w.HasFlag(CaesarSystem.RefusedFlag));
    }

    [Fact]
    public void A_general_with_sway_may_be_recalled_and_one_who_comes_home_lays_the_command_down()
    {
        var (tc, w, me, mine, ctx) = Start(); var f = Legion(w, mine, me);
        Assert.Contains("sway", tc.Caesar.RecallRefusal(w, mine));
        Name(w, me); Assert.Null(tc.Caesar.RecallRefusal(w, mine));
        Assert.NotNull(new CaesarCommand(me.Id, CaesarCommand.Obey, "", tc.Caesar).Refusal(ctx));
        tc.Caesar.RecallVoted(w, mine, Rival(w, mine), passed: true);
        Assert.Single(w.Recalls); Assert.NotNull(tc.Caesar.RecallRefusal(w, mine)); tc.Caesar.Tick(w); Assert.True(w.HasFlag(CaesarSystem.RecalledFlag));
        int legit = mine.Legitimacy;
        Assert.True(tc.Executor.TryExecute(new CaesarCommand(me.Id, CaesarCommand.Obey, "", tc.Caesar), ctx));
        Assert.Empty(w.Recalls); Assert.Equal("", f.AdmiralId); Assert.True(CaesarSystem.Barred(w, me)); Assert.True(mine.Legitimacy >= legit);
        Assert.False(new AssignAdmiralCommand(me.Id, f.Id, me.Id).CanExecute(ctx));   // not for five years
        Assert.True(w.HasFlag(CaesarSystem.ObeyedFlag));
    }

    [Fact]
    public void A_recall_left_unanswered_is_enforced_and_a_loyal_kinsman_comes_home_by_himself()
    {
        var (tc, w, me, mine, _) = Start(); Legion(w, mine, me); Name(w, me);
        tc.Caesar.RecallVoted(w, mine, Rival(w, mine), passed: true); int legit = mine.Legitimacy;
        w.Turn += 1; tc.Caesar.Tick(w); Assert.Single(w.Recalls);          // the head has until the deadline
        w.Turn += 1; tc.Caesar.Tick(w); Assert.Empty(w.Recalls); Assert.True(mine.Legitimacy < legit); Assert.Null(w.CivilWar);   // never a war by neglect

        var (tc2, w2, me2, mine2, _) = Start(); var kin = w2.LivingCharacters().First(c => c.HouseId == mine2.Id && c.Id != me2.Id && c.Age >= 18);
        kin.NatureTraits.Clear(); kin.NatureTraits.Add("Loyal"); kin.Ambition = "earn_a_command"; Legion(w2, mine2, kin); Name(w2, kin);
        tc2.Caesar.RecallVoted(w2, mine2, Rival(w2, mine2), passed: true); tc2.Caesar.Tick(w2); Assert.True(w2.HasFlag(CaesarSystem.KinRecalledFlag));
        w2.Turn += 1; tc2.Caesar.Tick(w2); Assert.Empty(w2.Recalls); Assert.True(CaesarSystem.Barred(w2, kin));
    }

    [Fact]
    public void Crossing_the_line_asks_hulls_a_name_and_a_cause_and_the_houses_declare()
    {
        var (tc, w, me, mine, ctx) = Start(); var f = Legion(w, mine, me, hulls: 3);
        Assert.Contains("hulls", tc.Caesar.CrossRefusal(w, mine));
        f.Strength = 12; Assert.Contains("name", tc.Caesar.CrossRefusal(w, mine));
        Name(w, me); Assert.Contains("no cause", tc.Caesar.CrossRefusal(w, mine));
        tc.Caesar.RecallVoted(w, mine, Rival(w, mine), passed: true);
        Assert.True(tc.Executor.TryExecute(new CaesarCommand(me.Id, CaesarCommand.Cross, "", tc.Caesar), ctx));
        var cw = w.CivilWar!; Assert.Equal(mine.Id, cw.HouseId); Assert.Equal(me.Id, cw.GeneralId); Assert.Empty(w.Recalls);
        Assert.NotEmpty(cw.Against); Assert.Contains(cw.SwordHouseId, cw.Against); Assert.All(cw.Against, id => Assert.True(w.AtWar(mine.Id, id)));
        Assert.DoesNotContain(mine.Id, cw.Declared.Keys); Assert.True(w.HasFlag(CaesarSystem.CrossedFlag)); Assert.Contains(CaesarSystem.CaesarChar, me.Flags);
        if (RegionSystem.CapitalHolder(w) is { } chair && chair.Id != mine.Id) Assert.Equal(CaesarSystem.Senate, cw.Declared[chair.Id]);
        Assert.NotNull(tc.Caesar.CrossRefusal(w, Rival(w, mine)));                                   // one civil war at a time
        var again = SaveSystem.Deserialize(SaveSystem.Serialize(w)); Assert.Equal(cw.Against.Count, again.CivilWar!.Against.Count); Assert.Equal(cw.Declared.Count, again.CivilWar.Declared.Count);
    }

    [Fact]
    public void Holding_the_capital_ends_the_republic_and_a_broken_general_does_not_outlive_it()
    {
        var (tc, w, me, mine, ctx) = Start(); var f = Legion(w, mine, me, hulls: 14); Name(w, me);
        tc.Caesar.TriumphVoted(w, mine, null, passed: false);                                       // the cause
        Assert.True(tc.Executor.TryExecute(new CaesarCommand(me.Id, CaesarCommand.Cross, "", tc.Caesar), ctx));
        string cap = w.Galaxy!.Capital; foreach (var x in w.Fleets.Values.Where(x => x.SystemId == cap && x.OwnerId != mine.Id).ToList()) w.Fleets.Remove(x.Id);
        f.SystemId = cap; f.DestinationId = "";
        tc.Caesar.Tick(w); Assert.NotNull(w.CivilWar); Assert.Equal(1, w.CivilWar!.Held);
        w.Turn += 1; tc.Caesar.Tick(w);
        Assert.Null(w.CivilWar); Assert.True(w.IsImperium); Assert.Equal(Praetoria.Core.Progression.Title.Emperor, mine.Title);
        Assert.True(w.HasFlag(CaesarSystem.RepublicFallenFlag)); Assert.True(w.HasFlag(CaesarSystem.CrownedFlag)); Assert.DoesNotContain(w.Wars, x => x.Involves(mine.Id));
        Assert.NotNull(tc.Caesar.CrossRefusal(w, mine)); Assert.NotNull(tc.Caesar.TriumphRefusal(w, mine));   // there is a throne now

        var (tc2, w2, me2, mine2, ctx2) = Start(); var f2 = Legion(w2, mine2, me2, hulls: 14); Name(w2, me2);
        tc2.Caesar.TriumphVoted(w2, mine2, null, passed: false);
        Assert.True(tc2.Executor.TryExecute(new CaesarCommand(me2.Id, CaesarCommand.Cross, "", tc2.Caesar), ctx2));
        string sword = w2.CivilWar!.SwordHouseId; int swordLegit = w2.House(sword)!.Legitimacy, legit = mine2.Legitimacy;
        foreach (var x in w2.Fleets.Values.Where(x => x.OwnerId == mine2.Id).ToList()) w2.Fleets.Remove(x.Id);   // the fleets are gone
        tc2.Caesar.Tick(w2);
        Assert.Null(w2.CivilWar); Assert.False(w2.IsImperium); Assert.False(me2.Alive); Assert.True(mine2.Legitimacy < legit || legit == 0);
        Assert.True(CaesarSystem.Enemy(w2, mine2.Id)); Assert.False(ElectionSystem.Eligible(w2, Content.Offices, mine2.Id));
        Assert.True(w2.House(sword)!.Legitimacy >= swordLegit); Assert.DoesNotContain(w2.Wars, x => x.Involves(mine2.Id) && x.Involves(sword));
    }

    [Fact]
    public void When_another_marches_the_player_declares_for_a_side_or_stands_aside()
    {
        var (tc, w, me, mine, ctx) = Start(); var rival = Rival(w, mine); var general = Economy.HeadOf(w, rival)!;
        Legion(w, rival, general, hulls: 14); Name(w, general); tc.Caesar.TriumphVoted(w, rival, null, passed: false);
        Assert.Null(tc.Caesar.CrossRefusal(w, rival)); tc.Caesar.Cross(w, rival);
        var cw = w.CivilWar!; Assert.True(w.HasFlag(CaesarSystem.MarchesFlag));
        if (RegionSystem.CapitalHolder(w)?.Id == mine.Id) return;                                    // the throne stands on the player's world: no choice to make
        Assert.DoesNotContain(mine.Id, cw.Declared.Keys);
        Assert.NotNull(new CaesarCommand(me.Id, CaesarCommand.Join, "nobody", tc.Caesar).Refusal(ctx));
        Assert.True(tc.Executor.TryExecute(new CaesarCommand(me.Id, CaesarCommand.Join, CaesarSystem.Senate, tc.Caesar), ctx));
        Assert.Equal(CaesarSystem.Senate, cw.Declared[mine.Id]);
        if (FleetSystem.StrengthOf(w, mine.Id) > 0) { Assert.Contains(mine.Id, cw.Against); Assert.True(w.AtWar(mine.Id, rival.Id)); }
        Assert.NotNull(new CaesarCommand(me.Id, CaesarCommand.Join, CaesarSystem.General, tc.Caesar).Refusal(ctx));   // said once
        // the general is broken: those who stood with the Senate are remembered for it
        foreach (var x in w.Fleets.Values.Where(x => x.OwnerId == rival.Id).ToList()) w.Fleets.Remove(x.Id);
        tc.Caesar.Tick(w); Assert.Null(w.CivilWar); Assert.False(general.Alive); Assert.True(w.HasFlag(CaesarSystem.SavedFlag));
    }

    [Fact]
    public void A_donative_buys_the_crews_regard_for_their_commander_once_a_year()
    {
        var (tc, w, me, mine, ctx) = Start(); var f = Legion(w, mine, me, hulls: 6, devotion: 40); mine.Treasury.Credits = 100;
        Assert.True(tc.Executor.TryExecute(new CaesarCommand(me.Id, CaesarCommand.Donative, f.Id, tc.Caesar), ctx));
        Assert.Equal(40 + CaesarSystem.DonativeDevotion, f.Devotion); Assert.Equal(100 - CaesarSystem.DonativeCost(f), mine.Treasury.Credits);
        Assert.NotNull(new CaesarCommand(me.Id, CaesarCommand.Donative, f.Id, tc.Caesar).Refusal(ctx));
    }

    private static void Fires(World w, string eventId, params string[] choices)
    {
        var engine = new Praetoria.Core.Events.EventEngine(Content, WorldBuilder.RngFor(w));
        var found = engine.GatherEligible(w).FirstOrDefault(e => e.def.Id == eventId);
        Assert.True(found.def != null, $"{eventId} is not eligible");
        foreach (var c in found.def!.Choices.Where(c => choices.Length == 0 || choices.Contains(c.Id)))
        {
            var again = engine.GatherEligible(w).FirstOrDefault(e => e.def.Id == eventId); if (again.def == null) break;   // not repeatable: once is the proof
            engine.Resolve(new Praetoria.Core.Events.FiredEvent(again.def, again.binding), w, c.Id);
        }
    }

    [Fact]
    public void Every_event_of_the_road_can_fire_where_it_belongs_and_every_choice_resolves()
    {
        // the general's own road: a triumph voted, one refused, the recall, the line
        var (tc, w, me, mine, ctx) = Start(); Legion(w, mine, me, hulls: 14); Name(w, me); mine.Treasury.Credits = 200; w.PoolsFor(me.Id).Influence = 9;
        w.WorldCounters[CaesarSystem.LaurelKey(me.Id)] = 3; tc.Caesar.TriumphVoted(w, mine, null, passed: true); Fires(w, "the_senates_triumph");
        w.WorldCounters[CaesarSystem.LaurelKey(me.Id)] = 3; tc.Caesar.TriumphVoted(w, mine, null, passed: false); Fires(w, "the_triumph_refused");
        tc.Caesar.RecallVoted(w, mine, Rival(w, mine), passed: true); tc.Caesar.Tick(w); Fires(w, "the_recall");
        Assert.True(tc.Executor.TryExecute(new CaesarCommand(me.Id, CaesarCommand.Cross, "", tc.Caesar), ctx)); Fires(w, "the_line_is_crossed");
        tc.Caesar.Win(w, w.CivilWar!); Fires(w, "the_throne_is_yours");

        // one of the house is the general: the crews are theirs, the triumph is theirs, the recall is theirs
        var (tc2, w2, me2, mine2, _) = Start(); var kin = w2.LivingCharacters().First(c => c.HouseId == mine2.Id && c.Id != me2.Id && c.Age >= 18);
        kin.NatureTraits.Clear(); kin.NatureTraits.Add("Proud"); Legion(w2, mine2, kin, hulls: 10); Name(w2, kin); foreach (var f in w2.Fleets.Values.Where(f => f.AdmiralId == me2.Id)) f.AdmiralId = "";
        tc2.Caesar.Tick(w2); Assert.True(w2.HasFlag(CaesarSystem.KinLovedFlag)); Fires(w2, "the_crews_are_theirs");
        w2.WorldCounters[CaesarSystem.LaurelKey(kin.Id)] = 3; tc2.Caesar.TriumphVoted(w2, mine2, null, passed: true); Assert.True(w2.HasFlag(CaesarSystem.KinTriumphFlag)); Fires(w2, "a_kinsmans_triumph");
        tc2.Caesar.RecallVoted(w2, mine2, Rival(w2, mine2), passed: true); tc2.Caesar.Tick(w2); Assert.True(w2.HasFlag(CaesarSystem.KinRecalledFlag)); Fires(w2, "a_kinsman_recalled");
        tc2.Caesar.Cross(w2, mine2); var lost = w2.CivilWar!; tc2.Caesar.Lose(w2, lost, "a test"); Assert.False(kin.Alive); Assert.True(me2.Alive); Fires(w2, "the_march_fails");

        // another house's general marches: the player hears of it, stands with the Senate, and it is saved; or stands aside, and it falls
        foreach (bool saved in new[] { true, false })
        {
            var (tc3, w3, me3, mine3, ctx3) = Start(); if (RegionSystem.CapitalHolder(w3)?.Id == mine3.Id) continue;
            var rival = Rival(w3, mine3); var general = Economy.HeadOf(w3, rival)!; Legion(w3, rival, general, hulls: 14); Name(w3, general);
            tc3.Caesar.TriumphVoted(w3, rival, null, passed: false); tc3.Caesar.Cross(w3, rival); Fires(w3, "a_general_marches");
            if (saved) { Assert.True(tc3.Executor.TryExecute(new CaesarCommand(me3.Id, CaesarCommand.Join, CaesarSystem.Senate, tc3.Caesar), ctx3)); tc3.Caesar.Lose(w3, w3.CivilWar!, "a test"); Fires(w3, "the_republic_saved"); }
            else { tc3.Caesar.Win(w3, w3.CivilWar!); Assert.True(w3.IsImperium); Assert.Equal(Praetoria.Core.Progression.Title.Emperor, rival.Title); Fires(w3, "the_republic_falls"); }
        }
    }

    [Fact]
    public void Over_sixty_years_there_is_never_more_than_one_civil_war_and_none_under_a_throne()
    {
        foreach (int seed in new[] { 3, 11 })
        {
            var (tc, w, _, _, _) = Start("the_risen_officer", seed);
            for (int y = 0; y < 60 && w.Protagonist is { Alive: true }; y++)
            {
                tc.EndTurn(); tc.BeginTurn();
                if (w.IsImperium) { Assert.Null(w.CivilWar); Assert.Empty(w.Recalls); }
                if (w.CivilWar is { } cw) { Assert.NotNull(w.House(cw.HouseId)); Assert.True(w.Turn - cw.Since <= CaesarSystem.WarYears + 1); }
                Assert.All(w.Fleets.Values, f => Assert.InRange(f.Devotion, 0, 100));
            }
        }
    }
}
