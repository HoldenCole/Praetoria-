using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Diagnostics;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The 2026-11 fix pass: sensible matches, a start that can found a line, and the speed-ups that must not change an answer.</summary>
public class FixPassTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static (TurnController tc, World w) Start(string scenario, ulong seed = 1)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), seed);
        return (new TurnController(w, Content), w);
    }
    private static HashSet<(string, string)> Marriages(World w) =>
        w.LivingCharacters().Select(c => (c, s: DynastySystem.Spouse(w, c))).Where(x => x.s != null).Select(x => string.CompareOrdinal(x.c.Id, x.s!.Id) < 0 ? (x.c.Id, x.s.Id) : (x.s.Id, x.c.Id)).ToHashSet();

    [Fact]
    public void Stewards_arrange_matches_that_make_sense_as_marriages()
    {
        var (tc, w) = Start("the_new_man"); var before = Marriages(w);
        for (int t = 0; t < 8; t++) { tc.BeginTurn(); tc.EndTurn(); }
        var made = Marriages(w).Where(m => !before.Contains(m)).ToList(); Assert.NotEmpty(made);
        foreach (var (a, b) in made)
        {
            var x = w.Char(a)!; var y = w.Char(b)!; var woman = x.Sex == "female" ? x : y; var man = x.Sex == "female" ? y : x;
            Assert.True(Math.Abs(x.Age - y.Age) <= 20 + 8, $"{x.Name} {x.Age} and {y.Name} {y.Age}");
            Assert.True(woman.Age - 8 <= DynastySystem.FertileMax || w.Characters.Values.Any(k => k.FatherId == man.Id), $"{woman.Name} was past bearing and {man.Name} had no child");
        }
    }

    [Fact]
    public void A_client_with_no_heir_is_offered_a_match_by_the_patron()
    {
        var match = Content.Events.Single(e => e.Id == "the_patrons_match"); var general = Content.Events.Single(e => e.Id == "a_match_is_proposed");
        Assert.True(Content.Texts.ContainsKey(match.Id)); Assert.True(Content.Texts.ContainsKey(general.Id)); Assert.True(general.Repeatable); Assert.False(match.Repeatable);
        var (tc, w) = Start("the_new_man"); Assert.Equal("lucent", w.House("crane")!.PatronId); Assert.Contains(OrderSystem.IsClientFlag, w.WorldFlags);
    }

    [Fact]
    public void A_houses_law_table_gives_the_same_answers_as_asking_key_by_key_and_the_read_cache_changes_nothing()
    {
        var (tc, w) = Start("the_old_lion", 7); for (int t = 0; t < 3; t++) { tc.BeginTurn(); tc.EndTurn(); }
        tc.BeginTurn();
        string[] keys = { "credits_per_world", "materials_per_world", "manpower_per_world", "senate_weight", "charter_pct", "unrest_drift", "market_price_pct" };
        var regions = w.Galaxy!.Regions.Select(r => r.Id).Prepend(null!).ToList();
        var fresh = w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId).OrderBy(h => h.Id, StringComparer.Ordinal)
            .Select(h => (h.Id, weight: tc.Senate.Weight(w, h.Id), laws: keys.SelectMany(k => regions.Select(r => PolicySystem.Modifier(w, Content.Policies, h.Id, k, r))).ToList(), ledger: tc.Economy!.Compute(w, h.Id, tc.Fleets).Net.Credits)).ToList();
        ReadCache.Open();
        try
        {
            foreach (var f in fresh)
            {
                Assert.Equal(f.weight, tc.Senate.Weight(w, f.Id)); Assert.Equal(f.weight, tc.Senate.Weight(w, f.Id));   // asked twice: the second is the memo
                Assert.Equal(f.laws, keys.SelectMany(k => regions.Select(r => PolicySystem.Modifier(w, Content.Policies, f.Id, k, r))).ToList());
                Assert.Equal(f.ledger, tc.Economy!.Compute(w, f.Id, tc.Fleets).Net.Credits);
                var table = PolicySystem.For(w, Content.Policies, f.Id); Assert.Equal(PolicySystem.Modifier(w, Content.Policies, f.Id, "credits_per_world"), table.Get("credits_per_world"));
            }
        }
        finally { ReadCache.Close(); }
        Assert.False(ReadCache.Active);
        var a = CourtProjection.Build(tc, Content); var b = CourtProjection.Build(tc, Content);
        Assert.Equal(a.Senate?.MyWeight, b.Senate?.MyWeight); Assert.Equal(a.Treasury.Credits, b.Treasury.Credits); Assert.False(ReadCache.Active);
        foreach (var region in w.Galaxy.Regions) Assert.Equal(PolicySystem.DominantByRegion(w).GetValueOrDefault(region.Id), PolicySystem.Dominant(w, region.Id));
    }

    [Fact]
    public void The_stopwatch_costs_nothing_when_it_is_off_and_names_what_it_timed_when_on()
    {
        Assert.Null(Perf.Time("anything"));
        Perf.Enabled = true;
        try { Perf.Reset(); using (Perf.Time("a block")) { } Assert.Contains(Perf.Report(), r => r.name == "a block" && r.calls == 1); }
        finally { Perf.Enabled = false; Perf.Reset(); }
    }
}
