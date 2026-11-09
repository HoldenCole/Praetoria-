using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The scripted check (GAP §19 item 1): every event the randomised soak never reached is built here by hand — its flags raised,
/// its roles seeded — and must be eligible. A failure means a dead trigger, not a rare one.</summary>
public class EventReachabilityTests
{
    private static (ContentDatabase content, World world) Fresh(string scenario, ulong seed = 1)
    {
        var root = ContentLocator.FindContentDir(); var content = ContentLoader.LoadFromDirectory(root);
        return (content, WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, scenario), seed));
    }
    private static Character Other(World w, Character self) => w.Characters.Values.Where(c => c.Alive && c.Id != self.Id && c.HouseId != self.HouseId).OrderBy(c => c.Id, StringComparer.Ordinal).First();
    private static void Eligible(string scenario, string eventId, Action<World, Character> setup)
    {
        var (content, w) = Fresh(scenario); w.Turn = 10; var p = w.Protagonist!;
        setup(w, p);
        var engine = new EventEngine(content, WorldBuilder.RngFor(w));
        var ids = engine.GatherEligible(w).Select(e => e.def.Id).ToList();
        Assert.True(ids.Contains(eventId), $"{eventId} not eligible; eligible were: {string.Join(", ", ids)}");
    }

    [Theory]
    [InlineData("the_levy_falls_due", "levied")]
    [InlineData("the_commission", "office_granted")]
    [InlineData("the_office_lost", "office_lost")]
    [InlineData("the_bench_forms", "faction_formed")]
    [InlineData("across_the_aisle", "faction_against_me")]
    [InlineData("the_fief", "fief_granted")]
    [InlineData("the_terms_of_peace", "won_a_world")]
    [InlineData("the_ceded_world", "ceded_a_world")]
    [InlineData("whispers_of_secession", "secession_tempting")]
    [InlineData("the_plot_exposed", "plot_exposed")]
    [InlineData("the_motion_thrown_out", "motion_failed_mine")]
    [InlineData("the_act_falls", "act_failed")]
    [InlineData("the_closed_exchanges", "embargoed")]
    [InlineData("the_speakers_gavel", "holds_office:speaker")]
    [InlineData("the_leaders_table", "faction_leader")]
    [InlineData("the_commons_feast", "estate_loyal:commons")]
    [InlineData("the_siege_bell", "under_siege")]
    [InlineData("after_the_raid", "raided")]
    [InlineData("the_lost_province", "lost_a_world_to_rebellion")]
    [InlineData("the_press_gang", "law:levies:conscription")]
    [InlineData("the_iron_law_bites", "law:justice:iron_law")]
    public void AFlagEvent_IsEligible_WhenItsFlagIsUp(string eventId, string flag) => Eligible("the_fallen_house", eventId, (w, _) => w.WorldFlags.Add(flag));

    // the military depth (docs/MILITARY.md)
    [Theory]
    [InlineData("the_line_holds", "battle_won")]
    [InlineData("the_line_breaks", "battle_lost")]
    [InlineData("the_achilles_name", "became_achilles")]
    [InlineData("the_prize_crews", "hulls_captured")]
    [InlineData("the_long_supply_line", "out_of_supply")]
    [InlineData("the_restive_crews", "fleet_restive")]
    [InlineData("the_mutiny", "fleet_mutinous")]
    [InlineData("the_campaign_opens", "campaign_declared")]
    [InlineData("the_campaign_ends", "campaign_over")]
    public void AMilitaryFlagEvent_IsEligible_WhenItsFlagIsUp(string eventId, string flag) => Eligible("the_risen_officer", eventId, (w, _) => w.WorldFlags.Add(flag));
    [Theory]
    [InlineData("the_war_beyond_the_reach", "foreign_war_declared")]
    [InlineData("the_fleet_sails_beyond", "fleet_sailed_abroad")]
    [InlineData("word_of_a_victory_abroad", "expedition_victory")]
    [InlineData("word_of_a_defeat_abroad", "expedition_defeat")]
    [InlineData("the_broken_expedition", "expedition_lost")]
    [InlineData("the_fleet_comes_home", "expedition_returned")]
    [InlineData("the_triumph", "triumph")]
    [InlineData("raiders_from_beyond", "raided_by_nation")]
    [InlineData("the_war_abroad_is_lost", "war_lost_abroad")]
    [InlineData("the_tribute_comes_in", "nation_subdued")]
    public void ANationFlagEvent_IsEligible_WhenItsFlagIsUp(string eventId, string flag) => Eligible("the_risen_officer", eventId, (w, _) => w.WorldFlags.Add(flag));
    [Fact] public void TheMentionInDispatches() => Eligible("the_risen_officer", "the_mention_in_dispatches", (w, p) => { w.WorldFlags.Add("battle_won"); w.WorldFlags.Add("fought_at_the_front"); p.CareerTrack = "military"; });
    [Fact] public void TheFrontOrTheFlag() => Eligible("the_risen_officer", "the_front_or_the_flag", (_, p) => { p.CareerTrack = "military"; p.CareerRank = 1; p.Flags.Remove("command:front"); p.Flags.Remove("command:flag"); });
    [Fact] public void TheTorpedoRun() => Eligible("the_risen_officer", "the_torpedo_run", (w, p) => { w.WorldFlags.Add("at_war_any"); w.House(p.HouseId)!.Treasury.Isotopes = 2; });
    [Fact] public void TheGeneralsSway() => Eligible("the_risen_officer", "the_generals_sway", (w, p) => { w.WorldFlags.Add("at_war_any"); p.Flags.Add("achilles"); });

    [Fact] public void TheRegentsHand() => Eligible("the_fallen_house", "the_regents_hand", (w, p) => { w.WorldFlags.Add(DynastySystem.RegencyFlag); Other(w, p).Flags.Add(DynastySystem.RegentFlag); });
    [Fact] public void TheOfficersGrievance() => Eligible("the_fallen_house", "the_officers_grievance", (w, p) => { w.WorldFlags.Add("estate_hostile:officers"); foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == p.HouseId)) f.Strength = 3; if (!w.Fleets.Values.Any(f => f.OwnerId == p.HouseId)) w.Fleets["t"] = new Fleet { Id = "t", Name = "Test", OwnerId = p.HouseId, Strength = 3 }; });
    [Fact] public void TheFrontierRidesIn() => Eligible("the_fallen_house", "the_frontier_rides_in", (w, _) => { w.WorldFlags.Add("estate_hostile:frontier"); w.WorldFlags.Add("ring:rim"); });
    [Fact] public void TheCoalitionHolds() => Eligible("the_fallen_house", "the_coalition_holds", (w, p) => Other(w, p).Flags.Add("coalition_leader"));
    [Fact] public void WhatTheSabotageTaught() => Eligible("the_fallen_house", "what_the_sabotage_taught", (_, p) => p.Flags.Add("sabotaged"));
    [Fact] public void TheBreakingPoint() => Eligible("the_fallen_house", "the_breaking_point", (_, p) => p.Flags.Add("broken"));
    [Fact] public void TheHumiliatedReturns() => Eligible("academy_crucible", "the_humiliated_returns", (w, p) => { p.Age = 18; Other(w, p).Flags.Add("humiliated"); });
    [Fact] public void TheMarkedMan() => Eligible("academy_crucible", "the_marked_man", (w, p) => { p.Age = 20; Other(w, p).Flags.Add("marked_for_death"); });
    [Fact] public void TheFrameUnravels() => Eligible("academy_crucible", "the_frame_unravels", (w, p) => { p.Age = 20; Other(w, p).Flags.Add("framed"); });
    [Fact] public void ThePetitioner() => Eligible("academy_crucible", "the_petitioner", (w, p) => { p.Age = 18; w.WorldCounters["standing"] = 2; p.CareerRank = 2; var o = Other(w, p); o.CareerRank = 0; o.Age = 17; });
    [Fact] public void TheMinistersDilemma() => Eligible("the_fallen_house", "the_ministers_dilemma", (w, p) => { p.CareerTrack = "stewardship"; p.CareerRank = 2; p.Skills["economics"] = 2; var o = Other(w, p); o.CareerTrack = "stewardship"; o.CareerRank = 3; });
    [Fact] public void TheTreasuryAudit() => Eligible("the_fallen_house", "the_treasury_audit", (_, p) => { p.CareerTrack = "stewardship"; p.CareerRank = 3; p.Skills["economics"] = 3; });
    [Fact] public void TheEmptyHall() => Eligible("the_fallen_house", "the_empty_hall", (w, _) => w.WorldCounters["houses_fallen"] = 1);
    [Fact] public void TheIsotopeBroker() => Eligible("the_fallen_house", "the_isotope_broker", (w, p) => w.House(p.HouseId)!.Treasury.Isotopes = 3);
    [Fact] public void TheHeliumGlut() => Eligible("the_fallen_house", "the_helium_glut", (w, p) => w.House(p.HouseId)!.Treasury.Helium3 = 9);
    [Fact] public void TheFreeMarch() => Eligible("the_fallen_house", "the_free_march", (w, _) => w.WorldFlags.Add("free_march"));
    [Fact] public void TheComet_AndThePortrait_AndTheOldRetainer() { Eligible("the_fallen_house", "the_comet", (w, _) => w.Turn = 16); Eligible("the_fallen_house", "the_portrait", (_, _) => { }); Eligible("the_fallen_house", "the_old_retainer", (w, _) => w.Turn = 12); }
}
