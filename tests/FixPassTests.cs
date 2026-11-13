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

    private static bool Do(World w, ICommand cmd) => new CommandExecutor().TryExecute(cmd, new CommandContext(w, TestContent.Rng()));

    [Fact]
    public void A_patron_notices_how_a_client_votes_on_what_touches_the_patron()
    {
        var (tc, w) = Start("the_new_man"); var mine = w.House("crane")!; var patron = w.House("lucent")!; var ph = Economy.HeadOf(w, patron)!;
        var rival = w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != patron.Id && h.Id != PirateSystem.HouseId).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        var censure = tc.Senate.Add(w, rival.Id, "censure", patron.Id); var forPatron = tc.Senate.Add(w, patron.Id, "censure", rival.Id);
        Assert.Equal(-1, tc.Senate.PatronWish(w, censure, mine.Id)); Assert.Equal(1, tc.Senate.PatronWish(w, forPatron, mine.Id)); Assert.Equal(0, tc.Senate.PatronWish(w, censure, rival.Id));
        int regard = w.Relationship(ph.Id, w.ProtagonistId).Disposition;
        censure.Votes[mine.Id] = 1;   // the client votes to censure its own patron
        censure.ProposedTurn = w.Turn - 1; forPatron.ProposedTurn = w.Turn + 5;
        tc.Senate.Tick(w);
        Assert.True(censure.Resolved); Assert.Contains(SenateSystem.DefiedPatronFlag, w.WorldFlags);
        Assert.Equal(regard + SenateSystem.DefyRegard, w.Relationship(ph.Id, w.ProtagonistId).Disposition);
        Assert.Contains(Content.Events, e => e.Id == "the_patron_noticed"); Assert.True(Content.Texts.ContainsKey("the_patron_noticed")); Assert.True(Content.Texts.ContainsKey("the_patron_is_pleased"));
    }

    [Fact]
    public void A_company_founds_a_venture_from_its_own_cash_and_keeps_it()
    {
        var (tc, w) = Start("the_old_lion", 7); var cs = tc.Companies!; var mine = w.House(w.Protagonist!.HouseId)!; mine.Treasury.Influence = 9;
        var sys = w.Galaxy!.System(w.HoldingsOf(mine.Id).First(h => h.IsPlanet).SystemId)!; var seed = FoundBusinessCommand.Open(w, mine, Content.Businesses.Def("trading_company")!, sys);
        Assert.True(Do(w, new IncorporateCommand(w.ProtagonistId, seed.Id, "", cs))); var co = w.Companies.Single(c => !c.Chartered);
        var candidates = Content.Businesses.Defs.Where(d => d.Id != "trading_company").Select(d => d.Id).ToList();
        co.Cash = 0; string? founded = null; int credits = mine.Treasury.Credits;
        foreach (var id in candidates)
        {
            var poor = new FoundBusinessCommand(w.ProtagonistId, id, sys.Id, Content.Businesses, Content.Holdings, Content.Titles, co.Id);
            string? no = poor.Refusal(new CommandContext(w, TestContent.Rng())); if (no == null || !no.Contains("the company needs")) continue;   // only the cash stands in the way
            co.Cash = 500; Assert.True(Do(w, new FoundBusinessCommand(w.ProtagonistId, id, sys.Id, Content.Businesses, Content.Holdings, Content.Titles, co.Id))); founded = id; break;
        }
        Assert.NotNull(founded); Assert.Equal(credits, mine.Treasury.Credits); Assert.True(co.Cash < 500);
        var project = Assert.Single(w.Projects.Where(p => p.CompanyId == co.Id)); Assert.Equal(founded, project.DefId);
        for (int i = 0; i < 5 && w.Projects.Any(p => p.CompanyId == co.Id); i++) ProjectSystem.Tick(w, Content.Holdings, Content.Businesses);
        Assert.Contains(w.Businesses.Values, b => b.Kind == founded && b.CompanyId == co.Id && b.OwnerId == mine.Id);
        var other = w.Houses.Values.First(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId);
        Assert.NotNull(new FoundBusinessCommand(Economy.HeadOf(w, other)!.Id, founded!, sys.Id, Content.Businesses, Content.Holdings, Content.Titles, co.Id).Refusal(new CommandContext(w, TestContent.Rng())));
    }

    [Fact]
    public void A_house_with_a_tenth_of_a_company_whose_director_holds_less_than_half_keeps_buying()
    {
        var (tc, w) = Start("the_old_lion", 7); var cs = tc.Companies!; var mine = w.House(w.Protagonist!.HouseId)!; mine.Treasury.Influence = 9;
        var sys = w.Galaxy!.System(w.HoldingsOf(mine.Id).First(h => h.IsPlanet).SystemId)!; var b = FoundBusinessCommand.Open(w, mine, Content.Businesses.Def("trading_company")!, sys);
        Assert.True(Do(w, new IncorporateCommand(w.ProtagonistId, b.Id, "", cs))); var co = w.Companies.Single(c => !c.Chartered);
        cs.Sell(w, mine.Id, co, 60);   // the director is down to forty; sixty on the float
        var raider = w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId && FactionSystem.Of(w, h.Id)?.PartyId != "sword" && !w.AtWar(h.Id, mine.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        raider.Treasury.Credits = 5000; Assert.True(cs.Buy(w, raider.Id, co, 12)); co.BoughtThisTurn.Clear();
        int held = co.HeldBy(raider.Id);
        for (int t = 0; t < 6 && co.ControllerId == mine.Id; t++) { tc.BeginTurn(); raider.Treasury.Credits = Math.Max(raider.Treasury.Credits, 5000); tc.EndTurn(); }
        Assert.True(co.HeldBy(raider.Id) > held, "the raider did not add to its stake");
    }

    [Fact]
    public void Legitimacy_is_held_between_nothing_and_a_hundred_whoever_adds_to_it()
    {
        var h = new House { Legitimacy = 98 }; h.Legitimacy += 12; Assert.Equal(100, h.Legitimacy); h.Legitimacy -= 250; Assert.Equal(0, h.Legitimacy);
        var (tc, w) = Start("the_old_lion", 7); var back = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        Assert.All(w.Houses.Values, x => Assert.Equal(x.Legitimacy, back.House(x.Id)!.Legitimacy));
    }
}
