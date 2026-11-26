using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The Senate's law, enlarged (docs/SENATE.md): ten more Acts, each shown changing the rule it names for the player's house
/// and for another house; the custom changing nothing; how a house leans for what an Act does to it; the benches moving the Acts of
/// their platforms, a repeal by the house a law hurts, and the save.</summary>
public class NewActsTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static PolicyCatalog Cat => Content.Policies;
    private static readonly string[] NewActs = { "usury", "canvassing", "forestalling", "hired_companies", "corn_law", "land_law", "law_of_service", "navigation_act", "law_of_years", "citizenship" };

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
    private static House Third(World w, params House[] not) =>
        w.Houses.Values.Where(h => !World.Outsider(h.Id) && not.All(n => n.Id != h.Id) && Economy.HeadOf(w, h) != null && w.HoldingsOf(h.Id).Any(x => x.IsPlanet)).OrderBy(h => h.Id, StringComparer.Ordinal).First();

    [Fact]
    public void Ten_new_Acts_stand_at_the_end_of_the_laws_each_a_law_of_the_whole_Reach_with_a_custom_that_does_nothing()
    {
        var acts = Cat.Laws.Where(l => l.Scope == "senate").ToList(); Assert.Equal(15, acts.Count);
        Assert.Equal(NewActs, Cat.Laws.TakeLast(10).Select(l => l.Id).ToArray());   // appended, nothing before them moved
        foreach (var id in NewActs)
        {
            var law = Cat.Law(id)!; Assert.True(law.Reach, id); Assert.InRange(law.Options.Count, 2, 3);
            Assert.Empty(law.DefaultOption.Effects); Assert.Empty(law.DefaultOption.Opinions);
            foreach (var o in law.Options.Where(o => !o.Default)) { Assert.NotEmpty(o.Effects); Assert.Contains(o.Opinions, x => x.Value > 0); Assert.Contains(o.Opinions, x => x.Value < 0); }   // something given, something it costs, and somebody minds
            // a party wants something of it, and so moves it
            Assert.Contains(Content.Parties.Defs, p => p.Laws.ContainsKey(id));
        }
        // the custom changes nothing: every key these Acts brought is read as nothing with every law at its default, set or not
        var (tc, w, mine, other) = Start();
        int Net(House h) { var l = tc.Economy!.Compute(w, h.Id, tc.Fleets); return l.Net.Credits * 100 + l.Coin; }
        int mineBefore = Net(mine), otherBefore = Net(other);
        foreach (var id in NewActs) Act(w, id, Cat.Law(id)!.DefaultOption.Id);
        foreach (var k in SenateActs.ReadForTheReach) Assert.Equal(0, SenateActs.Reach(w, k));
        Assert.Equal(mineBefore, Net(mine)); Assert.Equal(otherBefore, Net(other));
        Assert.Null(SenateActs.LendRefusal(w, 30)); Assert.Equal(double.MaxValue, SenateActs.CornerCap(w)); Assert.False(SenateActs.StapleInForce(w)); Assert.Equal(25, SenateActs.AgeFor(w, 25));
    }

    [Fact]
    public void Every_effect_of_the_new_Acts_is_said_in_plain_words_and_read_for_the_whole_Reach()
    {
        foreach (var id in NewActs)
            foreach (var o in Cat.Law(id)!.Options.Where(o => !o.Default))
            {
                string said = CourtProjection.Effects(o.Effects);
                Assert.DoesNotContain("_", said);   // no key left as it is written in the content
                foreach (var k in o.Effects.Keys) Assert.True(SenateActs.ReadForTheReach.Contains(k) || k is "unrest_drift" or "lawlessness_drift" or "tolls_bonus", $"{id}/{o.Id}: {k}");
            }
        Assert.Contains("no loan at more than 20 in the hundred", CourtProjection.Effects(Cat.Law("usury")!.Option("capped")!.Effects));
    }

    // ---------------------------------------------------------------- usury

    [Fact]
    public void The_Usury_Law_caps_what_every_lender_asks_and_a_lender_who_may_ask_less_lends_less()
    {
        var (tc, w, mine, other) = Start(); var v = Content.Businesses;
        LoanTerms Counting(House h) => Lending.Lenders(w, h.Id, v).First(t => t.LenderId == Lending.CountingHouses);
        var mineFree = Counting(mine); var otherFree = Counting(other); Assert.Equal(30, mineFree.RatePct);
        Act(w, "usury", "capped");
        var mineCapped = Counting(mine); var otherCapped = Counting(other);
        Assert.Equal(20, mineCapped.RatePct); Assert.Equal(20, otherCapped.RatePct);
        Assert.True(mineCapped.Limit < mineFree.Limit); Assert.True(otherCapped.Limit < otherFree.Limit);
        Assert.Contains("the Usury Law allows no more than 20", mineCapped.Why);
        Act(w, "usury", "tenth");
        Assert.Equal(10, Counting(mine).RatePct); Assert.True(Counting(mine).Limit < mineCapped.Limit); Assert.True(Counting(other).Limit < otherCapped.Limit);
        // the player asks the counting-houses for what they lent before the law: refused, and the law named as the reason
        mine.Debt = 0; var ask = new TakeLoanCommand(w.Protagonist!.Id, v, Lending.CountingHouses, mineFree.Limit / Lending.Step * Lending.Step);
        Assert.EndsWith("and no more: the Usury Law allows it no more than 10 in the hundred, and it lends the less for that", ask.Refusal(new CommandContext(w, TestContent.Rng())));
        // the stewards' standing loan, which every other house takes: less is lent, and less owed for it
        other.Debt = 0; int before = other.Treasury.Credits; var steward = Economy.HeadOf(w, other)!;
        new TakeLoanCommand(steward.Id, v).Execute(new CommandContext(w, TestContent.Rng()));
        int lent = other.Treasury.Credits - before;
        Assert.InRange(lent, Lending.Step, v.Finance.LoanPrincipal - 1); Assert.True(other.Debt < v.Finance.LoanRepay);
        Assert.Equal(lent + lent * 10 / 100, other.Debt);   // no loan costs more than the cap: 500 lent at 10 owe 500, the interest rounded down and never up past it
        // the House screen offers the standing loan as the law leaves it, not as the custom made it
        var (standLent, standOwes) = SenateActs.StandingLoan(w, v.Finance); Assert.Equal((lent, other.Debt), (standLent, standOwes));
        Assert.StartsWith($"Borrow {Money.Credits(standLent)} (owe {Money.Show(standOwes)},", CourtProjection.Build(tc, Content).Ledger!.Loan.Label);
        // a loan of one's choosing at the cap is owed the same way
        var ctx = new CommandContext(w, TestContent.Rng()); var chosen = new TakeLoanCommand(w.Protagonist!.Id, v, Lending.CountingHouses, Lending.Step);
        Assert.Null(chosen.Refusal(ctx)); chosen.Execute(ctx); Assert.Equal(Lending.Step + Lending.Step * 10 / 100, mine.Debt);
        // a loan between houses above the cap is refused, and the law is named as the reason: the player's and another's alike
        var third = Third(w, mine, other);
        string? mineAsks = tc.Dealings.LendRefusal(w, Economy.HeadOf(w, mine), third, DealingsSystem.LoanSizes[0], 20);
        string? otherAsks = tc.Dealings.LendRefusal(w, Economy.HeadOf(w, other), third, DealingsSystem.LoanSizes[0], 30);
        Assert.Equal("the Usury Law allows no more than 10 in the hundred, and this is 20", mineAsks);
        Assert.Equal("the Usury Law allows no more than 10 in the hundred, and this is 30", otherAsks);
        Assert.DoesNotContain("Usury", tc.Dealings.LendRefusal(w, Economy.HeadOf(w, mine), third, DealingsSystem.LoanSizes[0], 10) ?? "");
        // and a loan between houses at the cap: 500 at 10 owe 500, said so before it is made
        int small = DealingsSystem.LoanSizes[0]; Assert.StartsWith($"{third.Name} would owe you {Money.Credits(small + small * 10 / 100)},", tc.Dealings.LendSays(w, third, small, 10));
        Assert.Equal(small + small * 10 / 100, tc.Dealings.Lend(w, other, third, small, 10).Owed);
        // where contract is free, the interest is rounded up as it always was
        w.Laws.Remove(PolicySystem.SenateHolder); Assert.Equal(small + (small * 10 + 99) / 100, tc.Dealings.Lend(w, mine, Third(w, mine, other, third), small, 10).Owed);
    }

    // ---------------------------------------------------------------- canvassing

    [Fact]
    public void The_Law_on_Canvassing_forbids_or_frees_the_coin_behind_a_canvass_and_what_is_written_down_as_bought()
    {
        var (_, w, mine, other) = Start(); var offices = Content.Offices; var rung = offices.Rungs[0];
        int Deeds() => w.OfficeRecord.Count(d => d.What == LadderSystem.BoughtElection);
        foreach (var h in new[] { mine, other })
        {
            var head = Economy.HeadOf(w, h)!; w.Candidacies.RemoveAll(c => c.CharacterId == head.Id); w.Candidacies.Add(new Candidacy { RungId = rung.Id, CharacterId = head.Id }); h.Treasury.Credits = 500;
            w.Laws.Remove(PolicySystem.SenateHolder); w.Campaigns.RemoveAll(f => f.CharacterId == head.Id); w.OfficeRecord.Clear();
            Assert.Null(LadderSystem.CampaignRefusal(w, offices, head, head, LadderSystem.Lavish));
            LadderSystem.Campaign(w, offices, head, head, LadderSystem.Handsome); Assert.Equal(0, Deeds());   // the custom: a handsome canvass is not written down
            Act(w, "canvassing", "strict");
            Assert.StartsWith("the Law on Canvassing forbids a lavish canvass", LadderSystem.CampaignRefusal(w, offices, head, head, LadderSystem.Lavish));
            w.Campaigns.RemoveAll(f => f.CharacterId == head.Id); LadderSystem.Campaign(w, offices, head, head, LadderSystem.Handsome); Assert.Equal(1, Deeds());   // the strict law writes a handsome one down
            Act(w, "canvassing", "free"); w.OfficeRecord.Clear(); w.Campaigns.RemoveAll(f => f.CharacterId == head.Id);
            Assert.Null(LadderSystem.CampaignRefusal(w, offices, head, head, LadderSystem.Lavish));
            LadderSystem.Campaign(w, offices, head, head, LadderSystem.Lavish); Assert.Equal(0, Deeds());   // money is free: nothing is written, however lavish
        }
        // the other houses, which put coin behind their own candidates the year before a count (LadderSystem's own year), by the same law
        foreach (var option in new[] { "strict", "free" })
        {
            var (_, w2, mine2, _) = Start(); var others = w2.Houses.Values.Where(h => h.Id != mine2.Id && !World.Outsider(h.Id)).ToList();
            foreach (var h in others) { h.Treasury.Credits = 300; h.Debt = 0; }   // enough for a lavish canvass five times over
            Act(w2, "canvassing", option); w2.Turn = 4; Assert.Equal(1, LadderSystem.NextIn(w2, offices)); w2.OfficeRecord.Clear();
            LadderSystem.Tick(w2, offices);
            var funds = w2.Campaigns.Where(f => f.HouseId != mine2.Id).ToList(); Assert.NotEmpty(funds);
            bool Written(CampaignFund f) => w2.OfficeRecord.Any(d => d.What == LadderSystem.BoughtElection && d.HouseId == f.HouseId && d.CharacterId == f.CharacterId);
            if (option == "strict")
            {
                Assert.DoesNotContain(funds, f => f.Size == LadderSystem.Lavish); Assert.Contains(funds, f => f.Size == LadderSystem.Handsome);   // the most the law allows
                Assert.All(funds, f => Assert.Equal(f.Size == LadderSystem.Handsome, Written(f)));   // and a handsome one is written down
            }
            else { Assert.Contains(funds, f => f.Size == LadderSystem.Lavish); Assert.DoesNotContain(funds, Written); }   // money is free: nothing is written, however lavish
        }
    }

    // ---------------------------------------------------------------- forestalling

    [Fact]
    public void The_Law_against_Forestalling_caps_a_corner_s_premium_or_makes_the_holder_sell_whoever_holds_it()
    {
        foreach (bool player in new[] { true, false })
        {
            foreach (var option in new[] { "free_market", "capped", "forbidden" })
            {
                var (_, w, mine, other) = Start(); var holder = player ? mine : other; var cat = Content.Market;
                var market = MarketSystem.Of(w, cat); var d = cat.Goods.Where(x => market.Goods.ContainsKey(x.Id) && market.Goods[x.Id].Stock >= 4).OrderBy(x => x.Id, StringComparer.Ordinal).First(); var g = market.Goods[d.Id];
                foreach (var h in w.Houses.Values) h.Treasury.Set(d.Id, 0);
                int stock = (int)Math.Floor(g.Stock); holder.Treasury.Set(d.Id, Math.Max(CornerSystem.LeastUnits, 2 * stock)); int all = stock + holder.Treasury.Get(d.Id);
                Act(w, "forestalling", option); int credits = holder.Treasury.Credits;
                CornerSystem.Reckon(w, cat, d, g);
                if (option == "free_market") { Assert.True(g.CornerPremium > 0.5, $"{g.CornerPremium}"); Assert.Equal(holder.Id, g.CornerHouse); }
                if (option == "capped") { Assert.Equal(0.20, g.CornerPremium, 3); Assert.Equal(holder.Id, g.CornerHouse); }
                if (option == "forbidden")
                {
                    Assert.Equal(SenateActs.MayKeep(all), holder.Treasury.Get(d.Id)); Assert.True(holder.Treasury.Credits > credits);
                    Assert.False(g.Cornered); Assert.Equal(1, w.Counter("corners_broken_by_law"));
                }
            }
        }
    }

    // ---------------------------------------------------------------- hired companies

    private static (World w, Fleet f, House home, House employer) Company(bool player, string option) { var c = CompanyOf(player, option, false); return (c.w, c.f, c.home, c.employer); }
    /// <summary>A fleet of the player's house or another's, lying at home, and a house at war that would hire it. With
    /// <paramref name="notChair"/>, the other house is one that does not hold the capital.</summary>
    private static (TurnController tc, World w, Fleet f, House home, House employer) CompanyOf(bool player, string option, bool notChair)
    {
        var (tc, w, mine, other) = Start(); if (notChair && RegionSystem.CapitalHolder(w)?.Id == other.Id) other = Third(w, mine, other);
        var home = player ? mine : other;
        var f = w.Fleets.Values.Where(x => x.OwnerId == home.Id && !x.Office && x.LevyOf.Length == 0 && !x.Moving).OrderBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
        if (f == null) { f = new Fleet { Id = "test_company", Name = "The Test Company", OwnerId = home.Id, SystemId = w.HoldingsOf(home.Id).First(x => x.IsPlanet).SystemId, Strength = 3, Readiness = 80 }; w.Fleets[f.Id] = f; }
        f.DestinationId = ""; f.Eta = 0;
        var employer = Third(w, mine, other); var enemy = w.Houses.Values.First(h => !World.Outsider(h.Id) && h.Id != employer.Id && h.Id != mine.Id && h.Id != other.Id);
        w.Wars.RemoveAll(x => x.Involves(home.Id)); w.Wars.Add(new War { HouseA = employer.Id, HouseB = enemy.Id, Since = w.Turn });
        employer.Treasury.Credits = 500; home.Treasury.Credits = 500;
        var eh = Economy.HeadOf(w, employer)!; var ah = Economy.HeadOf(w, home)!; w.Relationship(eh.Id, ah.Id).Disposition = 10;
        if (option.Length > 0) Act(w, "hired_companies", option);
        return (tc, w, f, home, employer);
    }

    [Fact]
    public void The_Law_of_Hired_Companies_licenses_or_forbids_a_fleet_hired_out_for_the_player_and_for_another_house()
    {
        foreach (bool player in new[] { true, false })
        {
            var (w, f, home, employer) = Company(player, "");
            var head = Economy.HeadOf(w, home)!;
            Assert.Null(MercenarySystem.Refusal(w, head, f.Id, employer.Id));
            // forbidden: no fleet is hired out, and the reason names the law
            Act(w, "hired_companies", "forbidden");
            Assert.StartsWith("the Law of Hired Companies forbids it", MercenarySystem.Refusal(w, head, f.Id, employer.Id));
            // a company already out comes home when the ban is carried
            Act(w, "hired_companies", "free"); MercenarySystem.Hire(w, head, f, employer); Assert.Equal(employer.Id, f.OwnerId);
            Act(w, "hired_companies", "forbidden"); w.Turn++; MercenarySystem.Tick(w); Assert.Equal(home.Id, f.OwnerId); Assert.Equal("", f.HiredFrom);
        }
        foreach (bool player in new[] { true, false })
        {
            // licensed: the house pays the chair for the licence, and loses less standing for it
            var (w, f, home, employer) = Company(player, "licensed"); var head = Economy.HeadOf(w, home)!; var chair = RegionSystem.CapitalHolder(w);
            int credits = home.Treasury.Credits + MercenarySystem.PayOf(f), legit = home.Legitimacy, chairs = chair?.Treasury.Credits ?? 0;   // the first year's pay comes with the hire
            Assert.Null(MercenarySystem.Refusal(w, head, f.Id, employer.Id));
            MercenarySystem.Hire(w, head, f, employer);
            Assert.Equal(legit - SenateActs.LicensedLegitimacy, home.Legitimacy);
            if (chair != null && chair.Id != home.Id) { Assert.Equal(credits - 3, home.Treasury.Credits); Assert.Equal(chairs + 3, chair.Treasury.Credits); }
            else Assert.Equal(credits, home.Treasury.Credits);   // the chair pays itself nothing
        }
        var (w2, f2, home2, employer2) = Company(false, "");
        int legit2 = home2.Legitimacy; MercenarySystem.Hire(w2, Economy.HeadOf(w2, home2)!, f2, employer2); Assert.Equal(legit2 - MercenarySystem.Legitimacy, home2.Legitimacy);   // free: the full shame
        // the licence is asked only where another house holds the capital: with nobody there, a house short of the fee hires out all the same
        foreach (bool player in new[] { true, false })
        {
            var (w3, f3, home3, employer3) = Company(player, "licensed"); var head3 = Economy.HeadOf(w3, home3)!;
            w3.PlanetOf(w3.Galaxy!.Capital)!.OwnerId = ""; Assert.Null(RegionSystem.CapitalHolder(w3));
            home3.Treasury.Credits = SenateActs.CompanyFee(w3) - 1;
            Assert.Null(MercenarySystem.Refusal(w3, head3, f3.Id, employer3.Id)); Assert.DoesNotContain("pay the chair", MercenarySystem.Says(w3, f3, employer3));
            int credits3 = home3.Treasury.Credits + MercenarySystem.PayOf(f3); MercenarySystem.Hire(w3, head3, f3, employer3); Assert.Equal(credits3, home3.Treasury.Credits);
        }
        // the chair is not told it pays itself; another house is told it pays the chair
        var (w4, f4, home4, employer4) = Company(true, "licensed");
        w4.PlanetOf(w4.Galaxy!.Capital)!.OwnerId = home4.Id; Assert.Equal(home4.Id, RegionSystem.CapitalHolder(w4)?.Id);
        Assert.DoesNotContain("pay the chair", MercenarySystem.Says(w4, f4, employer4)); Assert.Contains("pays no licence", MercenarySystem.Says(w4, f4, employer4));
        w4.PlanetOf(w4.Galaxy!.Capital)!.OwnerId = employer4.Id;
        Assert.Contains($"you pay the chair {Money.Credits(SenateActs.CompanyFee(w4))} for the licence", MercenarySystem.Says(w4, f4, employer4));
    }

    // ---------------------------------------------------------------- the Corn Law

    [Fact]
    public void The_Corn_Law_costs_every_house_on_every_world_and_calms_every_world()
    {
        var (tc, w, mine, other) = Start(); var eco = tc.Economy!;
        int Net(House h) => eco.Compute(w, h.Id, tc.Fleets).Net.Credits;
        int Worlds(House h) => w.HoldingsOf(h.Id).Count(x => x.IsPlanet && !x.InRebellion);
        int Unrest(House h) => eco.UnrestWhy(w, w.HoldingsOf(h.Id).First(x => x.IsPlanet && x.Population > 0)).Sum(x => x.by);
        int mineNone = Net(mine), otherNone = Net(other), mineCalm = Unrest(mine), otherCalm = Unrest(other);
        Act(w, "corn_law", "cheap_corn");
        Assert.Equal(mineNone - Worlds(mine), Net(mine)); Assert.Equal(otherNone - Worlds(other), Net(other));
        Assert.Equal(mineCalm - 1, Unrest(mine)); Assert.Equal(otherCalm - 1, Unrest(other));
        Act(w, "corn_law", "free_corn");
        Assert.Equal(mineNone - 2 * Worlds(mine), Net(mine)); Assert.Equal(otherNone - 2 * Worlds(other), Net(other));
        Assert.Equal(mineCalm - 2, Unrest(mine)); Assert.Equal(otherCalm - 2, Unrest(other));
    }

    // ---------------------------------------------------------------- the Land Law

    [Fact]
    public void The_Land_Law_moves_every_house_s_prosperity_one_way_and_its_people_the_other()
    {
        int Pop(string option, bool player)
        {
            var (tc, w, mine, other) = Start(); var h = player ? mine : other; if (option.Length > 0) Act(w, "land_law", option);
            var world = w.HoldingsOf(h.Id).Where(x => x.IsPlanet && x.Population > 0 && x.Unrest <= 50).OrderBy(x => x.Id, StringComparer.Ordinal).First();
            tc.Economy!.Accrue(w); return world.Population;
        }
        int Pros(string option, bool player)
        {
            var (tc, w, mine, other) = Start(); var h = player ? mine : other; if (option.Length > 0) Act(w, "land_law", option);
            return tc.Economy!.ProsperityTarget(w, w.HoldingsOf(h.Id).Where(x => x.IsPlanet).OrderBy(x => x.Id, StringComparer.Ordinal).First());
        }
        foreach (bool player in new[] { true, false })
        {
            int pop = Pop("", player), pros = Pros("", player);
            Assert.Equal(pop + 1, Pop("allotments", player)); Assert.Equal(pop - 1, Pop("great_estates", player));
            Assert.True(Pros("great_estates", player) > pros, "great estates did not raise prosperity"); Assert.True(Pros("allotments", player) < pros, "allotments did not lower prosperity");
        }
    }

    // ---------------------------------------------------------------- the Law of Service

    [Fact]
    public void The_Law_of_Service_makes_every_house_s_fleets_cheaper_and_slower_to_ready_or_dearer_and_quicker()
    {
        var (tc, w, mine, other) = Start();
        foreach (var e in Cat.Estates) { w.WorldCounters[PolicySystem.ApprovalKey(w, mine.Id, e.Id)] = 0; w.WorldCounters[PolicySystem.ApprovalKey(w, other.Id, e.Id)] = 0; }
        w.Laws.Remove(mine.Id); w.Laws.Remove(other.Id);
        foreach (var h in new[] { mine, other })
        {
            var fleet = w.Fleets.Values.Where(f => f.OwnerId == h.Id && !f.LaidUp && !f.Office).OrderBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
            if (fleet == null) { fleet = new Fleet { Id = "test_" + h.Id, Name = "Test", OwnerId = h.Id, SystemId = w.HoldingsOf(h.Id).First(x => x.IsPlanet).SystemId, Strength = 4 }; w.Fleets[fleet.Id] = fleet; }
            long Keep(string option) { w.Laws.Remove(PolicySystem.SenateHolder); if (option.Length > 0) Act(w, "law_of_service", option); var u = tc.Fleets.Upkeep(fleet); return 100L * u.Credits + u.Coin; }
            long plain = Keep(""); Assert.True(plain > 0);
            Assert.Equal(plain * 75 / 100, Keep("short")); Assert.Equal(plain * 125 / 100, Keep("long"));
            int Ready(string option)
            {
                w.Laws.Remove(PolicySystem.SenateHolder); if (option.Length > 0) Act(w, "law_of_service", option);
                foreach (var x in w.Houses.Values) { x.Treasury.Credits = 1000; x.Treasury.Helium3 = 200; }   // fuel and pay in hand: nothing but the law moves the crews
                fleet.Readiness = 40; fleet.DestinationId = ""; fleet.Eta = 0; tc.Fleets.Tick(w); return fleet.Readiness;
            }
            int ready = Ready("");
            Assert.Equal(ready + 3, Ready("long")); Assert.Equal(ready - 2, Ready("short"));
        }
        // the law is the Reach's, for the fleets of the Reach: the raiders and the nations' fleets on the map are neither readier nor dearer for it
        var raiders = new Fleet { Id = "test_raiders", Name = "Raiders", OwnerId = PirateSystem.HouseId, SystemId = w.HoldingsOf(mine.Id).First(x => x.IsPlanet).SystemId, Strength = 3 }; w.Fleets[raiders.Id] = raiders;
        var outsiders = new List<Fleet> { raiders };
        if (w.Fleets.Values.Where(f => World.Outsider(f.OwnerId) && f.OwnerId != PirateSystem.HouseId && !f.IsAway && !f.LaidUp).OrderBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault() is { } nation) outsiders.Add(nation);
        Assert.Equal(2, outsiders.Count);   // the Old Lion begins with the nations' hosts on the map
        foreach (var o in outsiders)
        {
            Assert.True(World.Outsider(o.OwnerId), o.OwnerId);
            long Keep(string option) { w.Laws.Remove(PolicySystem.SenateHolder); if (option.Length > 0) Act(w, "law_of_service", option); var u = tc.Fleets.Upkeep(o); return 100L * u.Credits + u.Coin; }
            int Ready(string option)
            {
                w.Laws.Remove(PolicySystem.SenateHolder); if (option.Length > 0) Act(w, "law_of_service", option);
                o.Readiness = 40; o.Supply = 100; o.DestinationId = ""; o.Eta = 0; tc.Fleets.Tick(w); return o.Readiness;
            }
            long plain = Keep(""); Assert.Equal(plain, Keep("long")); Assert.Equal(plain, Keep("short"));
            int ready = Ready(""); Assert.Equal(ready, Ready("long")); Assert.Equal(ready, Ready("short"));
        }
    }

    // ---------------------------------------------------------------- the Navigation Act

    [Fact]
    public void The_Navigation_Act_levies_a_sale_between_houses_for_the_chair_or_forbids_it_off_the_exchanges()
    {
        var (tc, w, mine, other) = Start(); var chair = RegionSystem.CapitalHolder(w)!; var third = Third(w, mine, other, chair);
        // two sales, by the player's house and by another (or, where that house is the chair, by a third, and the chair is paid)
        foreach (var seller in new[] { mine, other }.Select(h => h.Id == chair.Id ? third : h).Distinct())
        {
            var buyer = Third(w, seller, chair);
            Act(w, "navigation_act", "staple");
            Assert.StartsWith("the Navigation Act forbids it", HouseTradeSystem.WontTrade(w, buyer, seller)); Assert.False(HouseTradeSystem.Trades(w, buyer, seller));
            Act(w, "navigation_act", "levy");
            Assert.DoesNotContain("Navigation", HouseTradeSystem.WontTrade(w, buyer, seller) ?? "");
            string good = Resource.Materials; seller.Treasury.Set(good, 40); buyer.Treasury.Credits = 1000;
            long sellerBefore = seller.Treasury.Total, chairBefore = chair.Treasury.Total, buyerBefore = buyer.Treasury.Total;
            int paid = tc.HouseTrade.Buy(w, buyer, seller, good, 10);
            // a tenth of the sale, to the single credit: 1,500 credits pay 150, 200 pay 20
            Assert.True(paid > 0); long sale = (long)paid * Money.PerUnit, levy = sale / 10;
            Assert.Equal(levy, SenateActs.LevyOn(w, seller, paid)); Assert.Equal(20, SenateActs.LevyOn(w, seller, 2)); Assert.Equal(0, SenateActs.LevyOn(w, chair, paid));
            Assert.Equal(sellerBefore + sale - levy, seller.Treasury.Total); Assert.Equal(chairBefore + levy + (chair.Id == buyer.Id ? -sale : 0), chair.Treasury.Total);
            Assert.Equal(buyerBefore - sale + (chair.Id == buyer.Id ? levy : 0), buyer.Treasury.Total);
            Act(w, "navigation_act", "free");
        }
        // the staple sends more through the chokepoints: every lane world's tolls rise
        var lane = w.Houses.Values.FirstOrDefault(h => w.HoldingsOf(h.Id).Any(x => x.IsPlanet && !x.InRebellion && w.Galaxy!.IsChokepoint(x.SystemId)));
        if (lane != null)
        {
            int Tolls() => tc.Economy!.Compute(w, lane.Id, tc.Fleets).Lines.Where(l => l.Kind == "tolls").Sum(l => l.Delta.Credits);
            int before = Tolls(); Act(w, "navigation_act", "staple"); Assert.True(Tolls() > before);
        }
    }

    // ---------------------------------------------------------------- the Law of the Years

    [Fact]
    public void The_Law_of_the_Years_moves_the_age_every_house_may_stand_for_office()
    {
        var (_, w, mine, other) = Start(); var offices = Content.Offices; var rung = offices.Rungs.First(r => r.Requires.Length == 0);
        foreach (var h in new[] { mine, other })
        {
            var c = w.LivingCharacters().Where(x => x.HouseId == h.Id).OrderBy(x => x.Id, StringComparer.Ordinal).First();
            c.CareerTrack = c.CareerTrack.Length > 0 ? c.CareerTrack : "law"; c.CareerRank = Math.Max(c.CareerRank, rung.MinRank); c.Cursus.Clear(); w.WorldCounters.Remove(LadderSystem.BarredKey(c.Id));
            c.Age = rung.MinAge - 3; w.Laws.Remove(PolicySystem.SenateHolder);
            Assert.Equal($"not before {rung.MinAge}", LadderSystem.Bar(w, offices, c, rung));
            Act(w, "law_of_years", "relaxed");
            Assert.DoesNotContain("not before", LadderSystem.Bar(w, offices, c, rung) ?? "");   // three years short of the custom, and the law lets them stand
            c.Age = rung.MinAge + 2; Act(w, "law_of_years", "strict");
            Assert.Equal($"not before {rung.MinAge + 5}: the Law of the Years asks {rung.MinAge + 5} where the custom asked {rung.MinAge}", LadderSystem.Bar(w, offices, c, rung));
        }
        var consul = offices.Defs.First(o => o.Elected && o.MinAge > 0); Act(w, "law_of_years", "strict");
        Assert.Contains($"{consul.MinAge + 5} or older", OfficeSystem.Wants(offices, consul, w));
        // what the player is told the law would do to the house, and how the house leans for it: the ladder and the consulship, a person as they
        var law = Cat.Law("law_of_years")!; w.Laws.Remove(PolicySystem.SenateHolder);
        foreach (var x in w.LivingCharacters().Where(x => x.HouseId == mine.Id)) { x.CareerTrack = ""; x.Cursus.Clear(); }   // nobody else of the house is near an office
        var aedile = offices.Rungs.First(r => r.Requires.Length > 0 && r.MinAge < offices.Rungs.Max(m => m.MinAge));
        var young = w.LivingCharacters().Where(x => x.HouseId == mine.Id).OrderBy(x => x.Id, StringComparer.Ordinal).First();
        young.CareerTrack = "law"; young.CareerRank = Math.Max(young.CareerRank, aedile.MinRank); young.Cursus.Add(aedile.Requires); young.Age = aedile.MinAge - 3;
        var elder = w.LivingCharacters().Where(x => x.HouseId == mine.Id && x.Id != young.Id).OrderBy(x => x.Id, StringComparer.Ordinal).First();
        elder.CareerTrack = "law"; elder.CareerRank = Math.Max(elder.CareerRank, consul.MinRank); foreach (var r in offices.Rungs) elder.Cursus.Add(r.Id); elder.Age = consul.MinAge - 3; w.WorldCounters.Remove(LadderSystem.BarredKey(elder.Id));
        string relaxed = SenateActs.ToHouse(w, mine, law.DefaultOption, law.Option("relaxed")!, offices);
        Assert.Contains($"{young.Name} ({young.Age}) could stand for {aedile.Name.ToLowerInvariant()} at once, where now they wait for {aedile.MinAge}", relaxed);
        Assert.Contains($"{elder.Name} ({elder.Age}) could stand for consul at once, where now they wait for {consul.MinAge}", relaxed);
        Assert.DoesNotContain(" it waits", relaxed);
        Assert.Equal(2, SenateActs.Interest(w, mine, law.DefaultOption, law.Option("relaxed")!, offices));   // two of the house would stand sooner
        elder.Age = consul.MinAge + 2; young.Age = aedile.MinAge + 2;
        string strict = SenateActs.ToHouse(w, mine, law.DefaultOption, law.Option("strict")!, offices);
        Assert.Contains($"{elder.Name} ({elder.Age}) would wait until {consul.MinAge + 5} to stand for consul", strict);
        Assert.Equal(-2, SenateActs.Interest(w, mine, law.DefaultOption, law.Option("strict")!, offices));
    }

    // ---------------------------------------------------------------- citizenship

    [Fact]
    public void The_Citizenship_of_the_Reach_moves_where_every_region_s_allegiance_settles()
    {
        var (tc, w, mine, other) = Start(); var g = w.Galaxy!;
        foreach (var h in new[] { mine, other })
        {
            var region = g.Region(g.System(w.HoldingsOf(h.Id).First(x => x.IsPlanet).SystemId)!.RegionId)!;
            w.Laws.Remove(PolicySystem.SenateHolder); int plain = tc.Regions.AllegianceTarget(w, region);
            Act(w, "citizenship", "whole_reach"); Assert.Equal(Math.Min(100, plain + 8), tc.Regions.AllegianceTarget(w, region));
            Act(w, "citizenship", "old_worlds"); Assert.Equal(Math.Max(0, plain - 8), tc.Regions.AllegianceTarget(w, region));
        }
    }

    // ---------------------------------------------------------------- how a house leans

    [Fact]
    public void A_house_leans_on_an_Act_by_what_it_does_to_that_house()
    {
        var (tc, w, mine, other) = Start(); var senate = tc.Senate; var offices = Content.Offices;
        int Lean(House h, string law, string option) => SenateActs.Interest(w, h, PolicySystem.ReachOption(w, Cat, Cat.Law(law)!) ?? Cat.Law(law)!.DefaultOption, Cat.Law(law)!.Option(option)!, offices);
        // a lender is against a cap on usury, a house in debt for it
        other.Debt = 0; other.Treasury.Credits = 500; w.HouseLoans.RemoveAll(l => l.BorrowerId == other.Id || l.LenderId == other.Id); w.Businesses.Values.Where(b => b.OwnerId == other.Id && Content.Businesses.Def(b.Kind)?.Lender == true).ToList().ForEach(b => b.OwnerId = mine.Id);
        foreach (var h in w.Houses.Values.Where(h => h.LenderId == other.Id)) h.LenderId = "";
        Assert.Equal(0, Lean(other, "usury", "tenth"));
        w.HouseLoans.Add(new HouseLoan { LenderId = other.Id, BorrowerId = mine.Id, Lent = 10, RatePct = 20, Owed = 12, Instalment = 3 });
        Assert.True(SenateActs.Lends(w, other)); Assert.Equal(-3, Lean(other, "usury", "tenth")); Assert.Equal(-2, Lean(other, "usury", "capped"));
        other.Debt = 20; Assert.Equal(-1, Lean(other, "usury", "tenth"));
        // the bench's estates and platform are in the lean too: the whole of it moves by what the law does to the house
        var m = new Motion { Id = "probe", Kind = "act", LawId = "usury", LawOption = "tenth" };
        int lending = senate.ActLean(w, other, m); w.HouseLoans.Clear(); int notLending = senate.ActLean(w, other, m); Assert.Equal(notLending - 3, lending);
        // a house with a fleet out on hire is against the ban; one at war with a house that hires fleets is for it
        var (w2, f, home, employer) = Company(false, ""); MercenarySystem.Hire(w2, Economy.HeadOf(w2, home)!, f, employer);
        int Lean2(House h, string option) => SenateActs.Interest(w2, h, Cat.Law("hired_companies")!.DefaultOption, Cat.Law("hired_companies")!.Option(option)!, offices);
        Assert.True(Lean2(home, "forbidden") <= -3, $"{Lean2(home, "forbidden")}"); Assert.True(Lean2(home, "licensed") < 0);
        Assert.True(Lean2(employer, "forbidden") <= -2);
        var enemy = w2.House(w2.EnemiesOf(employer.Id).First())!; Assert.True(Lean2(enemy, "forbidden") >= 2);
        // whoever holds a corner is against the law on forestalling
        var market = MarketSystem.Of(w, Content.Market); var good = market.Goods.Values.OrderBy(x => x.Id, StringComparer.Ordinal).First();
        foreach (var x in market.Goods.Values) { x.CornerHouse = ""; x.CornerPremium = 0; x.Cornered = false; }
        Assert.Equal(0, Lean(mine, "forestalling", "forbidden"));   // no corner stands: the law does nothing to anybody
        good.CornerHouse = mine.Id; good.CornerPremium = 0.6; good.Cornered = true;
        Assert.Equal(-3, Lean(mine, "forestalling", "forbidden")); Assert.Equal(-2, Lean(mine, "forestalling", "capped"));
        // the Corn Law falls on every world a house holds
        int many = w.Houses.Values.Where(h => !World.Outsider(h.Id)).Max(h => w.HoldingsOf(h.Id).Count(x => x.IsPlanet && !x.InRebellion)); var big = w.Houses.Values.First(h => w.HoldingsOf(h.Id).Count(x => x.IsPlanet && !x.InRebellion) == many);
        foreach (var x in w.HoldingsOf(big.Id)) x.Unrest = 0; big.Treasury.Credits = 500;
        Assert.True(Lean(big, "corn_law", "free_corn") < 0 || many < 2);
        foreach (var x in w.HoldingsOf(big.Id).Where(x => x.IsPlanet)) x.Unrest = 60;
        int restless = Lean(big, "corn_law", "cheap_corn"); foreach (var x in w.HoldingsOf(big.Id)) x.Unrest = 0; Assert.Equal(restless - 2, Lean(big, "corn_law", "cheap_corn"));
        // the chair is for a wider citizenship, and the levy it would be paid
        var chair = RegionSystem.CapitalHolder(w)!; Assert.True(Lean(chair, "citizenship", "whole_reach") >= 2); Assert.True(Lean(chair, "navigation_act", "levy") >= 1);
    }

    [Fact]
    public void The_player_is_shown_in_figures_what_an_Act_would_do_to_the_house()
    {
        var (tc, w, mine, _) = Start(); var model = CourtProjection.Build(tc, Content);
        var act = model.Senate!.Proposals.Single(p => p.KindId == "act"); Assert.Equal(15, act.Laws!.Count);
        Assert.All(act.Laws, l => Assert.False(string.IsNullOrEmpty(l.Category)));
        int worlds = w.HoldingsOf(mine.Id).Count(x => x.IsPlanet && !x.InRebellion);
        var corn = act.Laws.Single(l => l.Id == "corn_law").Options.Single(o => o.Id == "cheap_corn");
        Assert.Contains($"To your house: your house would pay {Money.Credits(worlds)} a year", corn.Effects);
        var usury = act.Laws.Single(l => l.Id == "usury").Options.Single(o => o.Id == "capped");
        Assert.Contains("the counting-houses would lend your house", usury.Effects); Assert.Contains("at 20 in the hundred, where they lend", usury.Effects);
        var service = act.Laws.Single(l => l.Id == "law_of_service").Options.Single(o => o.Id == "long");
        Assert.Contains("your fleets, which cost", service.Effects);
        Assert.Contains("a lavish canvass", act.Laws.Single(l => l.Id == "canvassing").Options.Single(o => o.Id == "strict").Effects);
        Assert.Contains("every region's allegiance would settle 8 higher; your worlds lie in", act.Laws.Single(l => l.Id == "citizenship").Options.Single(o => o.Id == "whole_reach").Effects);
        Assert.Contains("your house could sell no lot to another house", act.Laws.Single(l => l.Id == "navigation_act").Options.Single(o => o.Id == "staple").Effects);
        Assert.Contains("could hire no fleet out", act.Laws.Single(l => l.Id == "hired_companies").Options.Single(o => o.Id == "forbidden").Effects);
        // the Land Law moves where prosperity settles, once: it is not a gain every year
        var estates = act.Laws.Single(l => l.Id == "land_law").Options.Single(o => o.Id == "great_estates").Effects;
        Assert.StartsWith("prosperity settles 4 higher", estates); Assert.Contains($"the prosperity of {(worlds == 1 ? "your one world" : $"each of your {worlds} worlds")} would settle 4 higher", estates); Assert.DoesNotContain("a year higher", estates);
        // the counting-houses' terms are told as the House screen gives them: to a house known for bad business, dearer, on every world it holds
        w.WorldCounters[VentureSystem.BadNameKey(mine.Id)] = w.Turn + 5; var rebel = w.HoldingsOf(mine.Id).First(x => x.IsPlanet); int was = rebel.Rebellion; rebel.Rebellion = 1;
        var usuryLaw = Cat.Law("usury")!; LoanTerms Counting() => Lending.Lenders(w, mine.Id, Content.Businesses).First(t => t.LenderId == Lending.CountingHouses);
        var free = Counting(); Act(w, "usury", "capped"); var capped = Counting(); w.Laws.Remove(PolicySystem.SenateHolder);
        Assert.Contains($"the counting-houses would lend your house {Money.Credits(capped.Limit)} at {capped.RatePct} in the hundred, where they lend {Money.Credits(free.Limit)} at {free.RatePct}",
            SenateActs.ToHouse(w, mine, usuryLaw.DefaultOption, usuryLaw.Option("capped")!, Content.Offices));
        rebel.Rebellion = was; w.WorldCounters.Remove(VentureSystem.BadNameKey(mine.Id));
        // and on the docket, an Act another house has moved says the same
        tc.Senate.Add(w, w.Houses.Values.First(h => h.Id != mine.Id && !World.Outsider(h.Id)).Id, "act", "", lawId: "corn_law", lawOption: "free_corn");
        var card = CourtProjection.Build(tc, Content).Senate!.Docket.Single(d => d.KindId == "act");
        Assert.Contains($"To your house: your house would pay {Money.Credits(2 * worlds)} a year", card.Text);
    }

    // ---------------------------------------------------------------- the chamber legislates

    [Fact]
    public void A_bench_moves_an_Act_of_its_platform_when_it_counts_the_votes_and_only_then()
    {
        var (tc, w, mine, _) = Start(); var senate = tc.Senate;
        w.Motions.Clear();
        var benches = w.Factions.Where(f => f.Members.Count > 0 && f.Laws.Count > 0 && f.LeaderHouseId != mine.Id).OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
        int i = benches.FindIndex(f => f.Laws.Keys.Any(k => Cat.Law(k)?.Scope == "senate" && Cat.Law(k)!.DefaultOption.Id != f.Laws[k])); Assert.True(i >= 0, "no bench wants an Act changed");
        var bench = benches[i]; w.Turn = 100 * benches.Count + i;   // that bench's year: it is asked first
        foreach (var h in bench.Members.Select(w.House)) { h!.SphereInfluence["senate"] = 200; if (!senate.MayPropose(w, h.Id)) h.Title = "baron"; }   // the bench has the votes, and a voice
        var moved = SenateActs.Legislate(w, senate, mine.Id);
        Assert.NotNull(moved); Assert.Contains(moved!.ProposerHouseId, bench.Members); Assert.Equal(bench.Laws[moved.LawId], moved.LawOption);
        Assert.Equal("senate", Cat.Law(moved.LawId)!.Scope); Assert.Equal(1, w.Counter(SenateActs.BenchMovedKey));
        // nothing more while anything is before the chamber: a place is left for the player
        Assert.Null(SenateActs.Legislate(w, senate, mine.Id));
        // without the votes, it waits: a law the estates of the Reach hardly care about, which every other bench, weighing a hundred
        // times as much, stands against as the custom
        w.Motions.Clear(); w.WorldCounters.Keys.Where(k => k.StartsWith("motion_cd:")).ToList().ForEach(k => w.WorldCounters.Remove(k));
        foreach (var h in w.Houses.Values) h.SphereInfluence["senate"] = bench.Members.Contains(h.Id) ? 0 : 200;
        foreach (var f in w.Factions.Where(f => f != bench)) { f.Laws.Clear(); foreach (var l in Cat.Laws.Where(l => l.Scope == "senate")) f.Laws[l.Id] = l.DefaultOption.Id; }
        bench.Laws.Clear(); bench.Laws["law_of_years"] = "relaxed";
        var count = senate.Forecast(w, bench.LeaderHouseId, "act", "", lawId: "law_of_years", lawOption: "relaxed"); Assert.True(count.Item1 * 100 < count.Item2 * SenateActs.BenchThinksPct, $"{count}");
        Assert.Null(SenateActs.Legislate(w, senate, mine.Id));
        // given the votes, it moves it
        foreach (var h in w.Houses.Values) h.SphereInfluence["senate"] = bench.Members.Contains(h.Id) ? 400 : 0;
        Assert.Equal("relaxed", SenateActs.Legislate(w, senate, mine.Id)?.LawOption);
        // every Act settled lately: nothing is moved
        w.Motions.Clear(); foreach (var l in Cat.Laws.Where(l => l.Scope == "senate")) w.WorldCounters[$"law_cd:{PolicySystem.SenateHolder}:{l.Id}"] = w.Turn + 5;
        Assert.Null(SenateActs.Legislate(w, senate, mine.Id));
    }

    [Fact]
    public void A_house_hurt_by_a_law_in_force_moves_its_repeal_and_the_law_can_change_back_when_the_benches_change()
    {
        var (tc, w, f, home, employer) = CompanyOf(false, "", notChair: true);
        var senate = tc.Senate; string mine = w.Protagonist!.HouseId;
        MercenarySystem.Hire(w, Economy.HeadOf(w, home)!, f, employer);
        Act(w, "hired_companies", "licensed"); w.Motions.Clear();
        home.SphereInfluence["senate"] = 500;   // the hurt house counts the votes it needs
        if (!senate.MayPropose(w, home.Id)) home.Title = "baron";
        var repeal = SenateActs.Legislate(w, senate, mine);
        Assert.NotNull(repeal); Assert.Equal(home.Id, repeal!.ProposerHouseId); Assert.Equal("hired_companies", repeal.LawId); Assert.Equal("free", repeal.LawOption);
        Assert.Equal(1, w.Counter(SenateActs.RepealMovedKey));
        // carried at the end of the next year: the custom is back for every house
        w.Turn++; senate.Tick(w);
        Assert.True(repeal.Resolved); Assert.True(repeal.Passed); Assert.Equal("free", PolicySystem.ReachOption(w, Cat, Cat.Law("hired_companies")!)!.Id);
        Assert.Equal(0, SenateActs.CompanyFee(w));
        // the benches change: years on, the Optimates hold the chamber and move the ban of their platform
        w.Motions.Clear(); w.Turn += 20; foreach (var h in w.Houses.Values) h.SphereInfluence["senate"] = 0;
        foreach (var x in w.Fleets.Values.Where(x => x.HiredFrom.Length > 0)) MercenarySystem.ComeHome(w, x, "the test");
        var opt = w.Factions.First(x => x.LeaderHouseId != mine && x.Members.Count > 0 && x.Laws.TryGetValue("hired_companies", out var o) && o != "free");
        string wants = opt.Laws["hired_companies"];
        foreach (var id in opt.Members) { var h = w.House(id)!; h.SphereInfluence["senate"] = 300; if (!senate.MayPropose(w, id)) h.Title = "baron"; }
        foreach (var x in w.Factions.Where(x => x != opt)) x.Laws.Clear(); foreach (var key in opt.Laws.Keys.Where(k => k != "hired_companies").ToList()) opt.Laws.Remove(key);
        var back = SenateActs.Legislate(w, senate, mine);
        Assert.NotNull(back); Assert.Equal("hired_companies", back!.LawId); Assert.Equal(wants, back.LawOption); Assert.Contains(back.ProposerHouseId, opt.Members);
        w.Turn++; senate.Tick(w); Assert.True(back.Passed); Assert.Equal(wants, PolicySystem.ReachOption(w, Cat, Cat.Law("hired_companies")!)!.Id);
    }

    [Fact]
    public void The_Acts_and_what_the_chamber_moved_survive_a_save()
    {
        var (tc, w, mine, _) = Start();
        Act(w, "usury", "tenth"); Act(w, "navigation_act", "staple"); Act(w, "land_law", "allotments");
        w.WorldCounters[SenateActs.BenchMovedKey] = 4; w.WorldCounters[SenateActs.RepealMovedKey] = 2; w.WorldCounters["corners_broken_by_law"] = 1;
        tc.Senate.Add(w, w.Houses.Values.First(h => h.Id != mine.Id && !World.Outsider(h.Id)).Id, "act", "", lawId: "corn_law", lawOption: "cheap_corn");
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w)); SenateActs.Use(back, Cat, Content.Businesses);
        Assert.Equal("tenth", back.Laws[PolicySystem.SenateHolder]["usury"]); Assert.Equal("staple", back.Laws[PolicySystem.SenateHolder]["navigation_act"]);
        Assert.Equal(10, SenateActs.LoanCap(back)); Assert.True(SenateActs.StapleInForce(back));
        Assert.Equal(4, back.Counter(SenateActs.BenchMovedKey)); Assert.Equal(2, back.Counter(SenateActs.RepealMovedKey)); Assert.Equal(1, back.Counter("corners_broken_by_law"));
        var m = back.Motions.Single(x => x.Open && x.Kind == "act"); Assert.Equal("corn_law", m.LawId); Assert.Equal("cheap_corn", m.LawOption);
    }
}
