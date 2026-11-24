using Praetoria.Core;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>What the audit of the laws found written and read by no rule (docs/LAWS_REVIEW.md), now read: an Act of the Senate's
/// manpower and its ventures, and the standing a law gives to a house that is not the player's.</summary>
public class LawsAuditTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static PolicyCatalog Cat => Content.Policies;
    private static (TurnController tc, World w, House mine, House other) Start()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 7); var tc = new TurnController(w, Content);
        var mine = w.House(w.Protagonist!.HouseId)!;
        var other = w.Houses.Values.Where(h => h.Id != mine.Id && !World.Outsider(h.Id) && w.HoldingsOf(h.Id).Any(x => x.IsPlanet)).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        return (tc, w, mine, other);
    }
    private static void Act(World w, string law, string option)
    {
        if (!w.Laws.TryGetValue(PolicySystem.SenateHolder, out var set)) w.Laws[PolicySystem.SenateHolder] = set = new Dictionary<string, string>();
        set[law] = option;
    }
    private static void Own(World w, House h, string law, string option)
    {
        if (!w.Laws.TryGetValue(h.Id, out var set)) w.Laws[h.Id] = set = new Dictionary<string, string>();
        set[law] = option;
    }

    [Fact]
    public void The_Levy_of_the_Reach_musters_on_every_house_s_worlds()
    {
        var (tc, w, mine, other) = Start();
        int Manpower(House h) => tc.Economy.Compute(w, h.Id, tc.Fleets).Net.Manpower;
        int myWorlds = w.HoldingsOf(mine.Id).Count(x => x.IsPlanet && !x.InRebellion), theirWorlds = w.HoldingsOf(other.Id).Count(x => x.IsPlanet && !x.InRebellion);
        int mineBefore = Manpower(mine), theirsBefore = Manpower(other);
        Act(w, "levy_of_the_reach", "common_levy");
        Assert.True(Manpower(mine) > mineBefore, "the common levy brought the player's house nothing"); Assert.True(Manpower(other) > theirsBefore, "the common levy brought another house nothing");
        Assert.InRange(Manpower(mine) - mineBefore, 1, myWorlds); Assert.InRange(Manpower(other) - theirsBefore, 1, theirWorlds);   // one a world, less what a poor world loses of it
        Act(w, "levy_of_the_reach", "no_common_levy");
        Assert.True(Manpower(mine) < mineBefore); Assert.True(Manpower(other) < theirsBefore);
    }

    [Fact]
    public void The_Trade_Charter_s_open_lanes_pay_every_venture_more()
    {
        var (tc, w, mine, _) = Start();
        var owner = w.Businesses.Values.Select(b => w.House(b.OwnerId)).Where(h => h != null && !World.Outsider(h.Id)).Select(h => h!).OrderBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault() ?? mine;
        int Ventures() => tc.Economy.Compute(w, owner.Id, tc.Fleets).Lines.Where(l => l.Kind is "venture" or "business").Sum(l => l.Delta.Credits);
        var mineOf = w.Businesses.Values.Where(b => b.OwnerId == owner.Id && b.CompanyId.Length == 0).OrderBy(b => b.Id, StringComparer.Ordinal).ToList(); Assert.NotEmpty(mineOf);
        // a tenth more of a venture that pays 400 credits is 40 credits, and the books are kept in hundreds: at the size ventures begin at
        // the Act's tenth used to be lost in the rounding (docs/LAWS_REVIEW.md, "too small to show"). It is paid in single credits now.
        long Exact() { var led = tc.Economy.Compute(w, owner.Id, tc.Fleets); return 100L * led.Lines.Where(l => l.Kind == "venture").Sum(l => l.Delta.Credits) + led.Coin; }
        long small = Exact(); Assert.Equal(0, tc.Economy.Compute(w, owner.Id, tc.Fleets).Coin);
        Act(w, "trade_charter", "open_lanes"); long smallOpen = Exact(); Act(w, "trade_charter", "customary");
        Assert.True(smallOpen > small, $"open lanes left small ventures paying {small} credits as before"); Assert.InRange(smallOpen - small, small / 20, small / 5);   // about a tenth
        foreach (var b in mineOf) b.Level = 12;
        int before = Ventures(); Assert.True(before >= 10 * mineOf.Count, $"ventures at level 12 pay only {before}"); Assert.True(before > 0, "no venture line in the ledger: " + string.Join(",", tc.Economy.Compute(w, owner.Id, tc.Fleets).Lines.Select(l => l.Kind).Distinct()));
        // only the part of the Act that is about ventures: the prices it also moves are measured in MarketTests
        int open = Cat.Law("trade_charter")!.Option("open_lanes")!.Effects["venture_yield_pct"]; Assert.True(open > 0);
        Act(w, "trade_charter", "open_lanes");
        Assert.True(Ventures() > before, $"open lanes left the ventures paying {Ventures()} as before");
    }

    [Fact]
    public void A_law_that_gives_standing_gives_it_to_any_house_that_keeps_it_not_only_the_player_s()
    {
        var (_, w, mine, other) = Start();
        foreach (var h in new[] { mine, other }) { w.Laws.Remove(h.Id); h.Legitimacy = 40; foreach (var e in Cat.Estates) w.WorldCounters[PolicySystem.ApprovalKey(w, h.Id, e.Id)] = 0; }
        Own(w, other, "court_etiquette", "closed_court"); Assert.Equal(1, PolicySystem.Modifier(w, Cat, other.Id, "legitimacy_per_5")); Assert.Equal(0, PolicySystem.Modifier(w, Cat, mine.Id, "legitimacy_per_5"));
        w.Turn = 5; PolicySystem.Tick(w, Cat);
        Assert.Equal(41, other.Legitimacy); Assert.Equal(40, mine.Legitimacy);
        Assert.Contains(other.LegitimacyRecord.Changes, c => c.Cause == LegitimacyBook.Laws && c.By == 1);
        w.Turn = 6; PolicySystem.Tick(w, Cat); Assert.Equal(41, other.Legitimacy);   // every fifth year, not every year
    }

    [Fact]
    public void A_throne_s_proscriptions_give_their_standing_to_the_throne_and_to_nobody_else()
    {
        var (_, w, mine, other) = Start();
        foreach (var h in new[] { mine, other }) { w.Laws.Remove(h.Id); h.Legitimacy = 40; foreach (var e in Cat.Estates) w.WorldCounters[PolicySystem.ApprovalKey(w, h.Id, e.Id)] = 0; }
        other.Title = Praetoria.Core.Progression.Title.Emperor; Assert.Equal(other.Id, ThroneSystem.Emperor(w)!.Id);
        Own(w, other, "imperial_edict", "proscriptions");
        Assert.Equal(2, PolicySystem.ReachLegitimacy(w, Cat, other.Id)); Assert.Equal(0, PolicySystem.ReachLegitimacy(w, Cat, mine.Id));
        w.Turn = 5; PolicySystem.Tick(w, Cat);
        Assert.Equal(42, other.Legitimacy); Assert.Equal(40, mine.Legitimacy);
        Assert.Equal(1, PolicySystem.ReachModifier(w, Cat, "unrest_drift"));   // and what it costs falls on every house, as before
    }

    [Fact]
    public void Every_effect_a_law_of_the_whole_Reach_names_is_one_the_rules_read_for_the_whole_Reach()
    {
        // The audit's finding was of this kind: an Act named an effect that was only ever read from a house's own laws. This is the
        // list of effects the rules read through PolicySystem.ReachModifier (or, for a licence, by its name); a new Reach-wide law that
        // names another fails here until a rule reads it.
        var read = new HashSet<string> { "unrest_drift", "lawlessness_drift", "market_price_pct", "tolls_bonus", "motion_cost", "exchange_levy_pct", "manpower_per_world", "venture_yield_pct", "legitimacy_per_5" };
        foreach (var g in Content.Market.Goods.Where(g => g.License.Length > 0)) read.Add(g.License);
        foreach (var x in ReachRead.Also) read.Add(x);
        var unread = Cat.Laws.Where(l => l.Reach).SelectMany(l => l.Options.SelectMany(o => o.Effects.Keys.Select(k => (law: l.Id, option: o.Id, key: k)))).Where(x => !read.Contains(x.key)).ToList();
        Assert.True(unread.Count == 0, "written and read by no rule for the whole Reach: " + string.Join("; ", unread.Select(x => $"{x.law}/{x.option}: {x.key}")));
    }

    [Fact]
    public void A_percentage_of_a_small_sum_is_paid_in_single_credits_and_reaches_the_treasury()
    {
        var (tc, w, mine, _) = Start(); foreach (var e in Cat.Estates) w.WorldCounters[PolicySystem.ApprovalKey(w, mine.Id, e.Id)] = 0;
        var fleet = w.Fleets.Values.Where(f => f.OwnerId == mine.Id && !f.LaidUp).OrderBy(f => f.Id, StringComparer.Ordinal).First();
        w.Laws.Remove(mine.Id); var plain = tc.Fleets.Upkeep(fleet); Assert.Equal(0, plain.Coin); Assert.True(plain.Credits > 0);
        Own(w, mine, "household_guard", "doubled"); var dear = tc.Fleets.Upkeep(fleet);
        long was = 100L * plain.Credits, now = 100L * dear.Credits + dear.Coin;
        Assert.Equal(was * 125 / 100, now);   // a quarter more, to the single credit, whatever the size of the fleet
        Own(w, mine, "household_guard", "none"); var cheap = tc.Fleets.Upkeep(fleet); Assert.Equal(was * 85 / 100, 100L * cheap.Credits + cheap.Coin);
    }
}
