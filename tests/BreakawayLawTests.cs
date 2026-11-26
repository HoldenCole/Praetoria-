using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Presentation;
using Praetoria.Core.Progression;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>A house that has broken away is bound only by its own laws (docs/BREAKAWAY.md; the owner, 2026-11-26: "If something
/// breaks away then they're bound by only their laws"). For every kind of rule that reads the law of the Reach: a law of the Reach
/// in force moves the number for a house of the Reach and does not move it for a house that has broken away. A rule between two
/// parties binds the party that is of the Reach. What belongs to the Reach's own places and institutions still binds a house that
/// has broken away and uses them.</summary>
public class BreakawayLawTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static PolicyCatalog Cat => Content.Policies;
    private const string State = "test_state";

    /// <summary>The Old Lion, with three houses that hold peopled worlds and are neither the player's nor the chair's: the first stays
    /// in the Reach, the second (and, asked for, the third) has broken away into a state the Reach has recognised.</summary>
    private static (TurnController tc, World w, House stays, House gone, House gone2) Start(bool twoGo = false)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 7); var tc = new TurnController(w, Content);
        string mine = w.Protagonist!.HouseId, chair = RegionSystem.CapitalHolder(w)?.Id ?? "";
        var landed = w.Houses.Values.Where(h => h.Id != mine && h.Id != chair && !World.Outsider(h.Id) && Economy.HeadOf(w, h) != null && w.HoldingsOf(h.Id).Any(x => x.IsPlanet && x.Population > 0 && x.Unrest <= 50))
            .OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        var stays = landed[0]; var gone = landed[1]; var gone2 = landed[2];
        var state = new Polity { Id = State, Name = "the Test Republic", HeadHouseId = gone.Id, Since = w.Turn, Recognised = w.Turn, TreatyUntil = w.Turn + 10 };
        state.Members.Add(gone.Id); gone.PolityId = State;
        if (twoGo) { state.Members.Add(gone2.Id); gone2.PolityId = State; }
        w.Polities.Add(state);
        Assert.True(w.OfTheReach(stays.Id)); Assert.False(w.OfTheReach(gone.Id)); Assert.False(World.Outsider(gone.Id));
        return (tc, w, stays, gone, gone2);
    }
    private static void Act(World w, string law, string option)
    {
        if (!w.Laws.TryGetValue(PolicySystem.SenateHolder, out var set)) w.Laws[PolicySystem.SenateHolder] = set = new Dictionary<string, string>();
        set[law] = option;
    }
    private static Holding FirstWorld(World w, House h) => w.HoldingsOf(h.Id).Where(x => x.IsPlanet && x.Population > 0 && x.Unrest <= 50).OrderBy(x => x.Id, StringComparer.Ordinal).First();
    private static House Another(World w, params House[] not) =>
        w.Houses.Values.Where(h => !World.Outsider(h.Id) && w.OfTheReach(h.Id) && not.All(n => n.Id != h.Id) && h.Id != RegionSystem.CapitalHolder(w)?.Id && Economy.HeadOf(w, h) != null && w.HoldingsOf(h.Id).Any(x => x.IsPlanet))
            .OrderBy(h => h.Id, StringComparer.Ordinal).First();

    // ---------------------------------------------------------------- what the law of the Reach does to a house itself

    [Fact]
    public void The_Corn_Law_costs_and_calms_the_worlds_of_a_house_of_the_Reach_and_not_those_of_a_house_that_has_broken_away()
    {
        var (tc, w, stays, gone, _) = Start(); var eco = tc.Economy!;
        int Net(House h) => eco.Compute(w, h.Id, tc.Fleets).Net.Credits;
        int Worlds(House h) => w.HoldingsOf(h.Id).Count(x => x.IsPlanet && !x.InRebellion);
        int Unrest(House h) => eco.UnrestWhy(w, FirstWorld(w, h)).Sum(x => x.by);
        int staysNet = Net(stays), goneNet = Net(gone), staysCalm = Unrest(stays), goneCalm = Unrest(gone);
        Act(w, "corn_law", "free_corn");   // 200 credits a world a year, and unrest 2 lower
        Assert.Equal(staysNet - 2 * Worlds(stays), Net(stays)); Assert.Equal(staysCalm - 2, Unrest(stays));
        Assert.Equal(goneNet, Net(gone)); Assert.Equal(goneCalm, Unrest(gone));
        Assert.DoesNotContain(eco.UnrestWhy(w, FirstWorld(w, gone)), x => x.what == "the law of the Reach" && x.by != 0);
    }

    [Fact]
    public void The_Land_Law_moves_prosperity_and_growth_on_the_worlds_of_the_Reach_and_not_on_a_breakaway_house_s()
    {
        int Pop(string option, bool broke)
        {
            var (tc, w, stays, gone, _) = Start(); var h = broke ? gone : stays; if (option.Length > 0) Act(w, "land_law", option);
            var world = FirstWorld(w, h); tc.Economy!.Accrue(w); return world.Population;
        }
        int Pros(string option, bool broke)
        {
            var (tc, w, stays, gone, _) = Start(); var h = broke ? gone : stays; if (option.Length > 0) Act(w, "land_law", option);
            return tc.Economy!.ProsperityTarget(w, FirstWorld(w, h));
        }
        Assert.Equal(Pop("", false) + 1, Pop("allotments", false));
        Assert.Equal(Pop("", true), Pop("allotments", true));
        Assert.True(Pros("great_estates", false) > Pros("", false), "the great estates did not raise prosperity in the Reach");
        Assert.Equal(Pros("", true), Pros("great_estates", true));
    }

    [Fact]
    public void The_Law_of_Service_moves_the_keep_and_readiness_of_a_fleet_of_the_Reach_and_not_of_a_breakaway_house_s_fleet()
    {
        var (tc, w, stays, gone, _) = Start();
        foreach (var h in new[] { stays, gone })
        {
            foreach (var e in Cat.Estates) w.WorldCounters[PolicySystem.ApprovalKey(w, h.Id, e.Id)] = 0;
            var fleet = w.Fleets.Values.Where(f => f.OwnerId == h.Id && !f.LaidUp && !f.Office && !f.IsAway).OrderBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
            if (fleet == null) { fleet = new Fleet { Id = "test_" + h.Id, Name = "Test", OwnerId = h.Id, SystemId = FirstWorld(w, h).SystemId, Strength = 4 }; w.Fleets[fleet.Id] = fleet; }
            long Keep(string option) { w.Laws.Remove(PolicySystem.SenateHolder); if (option.Length > 0) Act(w, "law_of_service", option); var u = tc.Fleets.Upkeep(fleet); return 100L * u.Credits + u.Coin; }
            int Ready(string option)
            {
                w.Laws.Remove(PolicySystem.SenateHolder); if (option.Length > 0) Act(w, "law_of_service", option);
                foreach (var x in w.Houses.Values) { x.Treasury.Credits = 1000; x.Treasury.Helium3 = 200; }   // fuel and pay in hand: nothing but the law moves the crews
                fleet.Readiness = 40; fleet.Supply = 100; fleet.DestinationId = ""; fleet.Eta = 0; tc.Fleets.Tick(w); return fleet.Readiness;
            }
            long plain = Keep(""); int ready = Ready(""); Assert.True(plain > 0);
            if (h == stays) { Assert.Equal(plain * 125 / 100, Keep("long")); Assert.Equal(ready + 3, Ready("long")); }
            else { Assert.Equal(plain, Keep("long")); Assert.Equal(ready, Ready("long")); Assert.Equal(plain, Keep("short")); Assert.Equal(ready, Ready("short")); }
        }
    }

    [Fact]
    public void The_law_of_the_Reach_moves_what_an_estate_thinks_of_a_house_of_the_Reach_and_not_of_one_that_has_broken_away()
    {
        var (tc, w, stays, gone, _) = Start();
        int said = PolicySystem.ReachOpinion(w, Cat, "commons"), staysThinks = PolicySystem.LawOpinion(w, Cat, stays.Id, "commons"), goneThinks = PolicySystem.LawOpinion(w, Cat, gone.Id, "commons");
        Act(w, "corn_law", "free_corn");   // the commons +15, counted at half
        int moved = PolicySystem.ReachCounts(PolicySystem.ReachOpinion(w, Cat, "commons")) - PolicySystem.ReachCounts(said); Assert.NotEqual(0, moved);
        Assert.Equal(staysThinks + moved, PolicySystem.LawOpinion(w, Cat, stays.Id, "commons"));
        Assert.Equal(goneThinks, PolicySystem.LawOpinion(w, Cat, gone.Id, "commons"));   // its own laws alone
        // the rivals' estates drift toward that: the house that has broken away's do not move for the Act
        var key = PolicySystem.ApprovalKey(w, gone.Id, "commons"); w.WorldCounters[key] = PolicySystem.LawOpinion(w, Cat, gone.Id, "commons");
        PolicySystem.Tick(w, Cat); Assert.Equal(PolicySystem.LawOpinion(w, Cat, gone.Id, "commons"), w.Counter(key));
        // the player's own house, broken away: the estates' card counts nothing of the Reach's law, and says why
        var mine = w.House(w.Protagonist!.HouseId)!; mine.PolityId = State; w.Polities.First(x => x.Id == State).Members.Add(mine.Id);
        var card = CourtProjection.Build(tc, Content).Chancery!.Estates.Single(e => e.Id == "commons");
        Assert.DoesNotContain("the Reach's", card.LawsSay); Assert.Contains("broken away", card.LawsSay);
        Assert.Equal(Math.Clamp(PolicySystem.LawOpinion(w, Cat, mine.Id, "commons") + w.Counter("estate_offset:commons"), -100, 100), card.Target);
        Assert.False(CourtProjection.Build(tc, Content).Chancery!.Bound);
    }

    [Fact]
    public void What_a_throne_s_law_gives_in_standing_every_five_years_goes_to_a_house_of_the_Reach_and_not_to_one_that_has_broken_away()
    {
        foreach (bool broke in new[] { false, true })
        {
            var (_, w, stays, gone, _) = Start(); var h = broke ? gone : stays;
            Assert.Null(ThroneSystem.Emperor(w)); h.Title = Title.Emperor;
            if (!w.Laws.TryGetValue(h.Id, out var set)) w.Laws[h.Id] = set = new Dictionary<string, string>();
            set["imperial_edict"] = "proscriptions";   // two legitimacy every fifth year, to whoever holds the throne
            Assert.Equal(broke ? 0 : 2, PolicySystem.ReachLegitimacy(w, Cat, h.Id));
            foreach (var e in Cat.Estates) w.WorldCounters[PolicySystem.ApprovalKey(w, h.Id, e.Id)] = 0;
            int own = PolicySystem.Modifier(w, Cat, h.Id, "legitimacy_per_5"); w.Turn = 5; h.Legitimacy = 50;
            PolicySystem.Tick(w, Cat);
            Assert.Equal(50 + own + (broke ? 0 : 2), h.Legitimacy);
        }
    }

    [Fact]
    public void A_house_of_the_Reach_pays_the_exchanges_what_the_law_of_the_Reach_asks_and_a_house_that_has_broken_away_does_not()
    {
        var (_, w, stays, gone, _) = Start();
        double Pays(House h) => MarketSystem.BuyPrice(w, Content.Trade, h.Id, Resource.Materials);
        double staysPays = Pays(stays), gonePays = Pays(gone);
        Act(w, "trade_charter", "protected");   // what every house of the Reach pays on the exchanges, a tenth more
        Assert.Equal(staysPays * 1.1, Pays(stays), 6); Assert.Equal(gonePays, Pays(gone), 9);
    }

    [Fact]
    public void The_player_is_not_told_that_an_Act_would_cost_a_house_that_has_broken_away()
    {
        var (tc, w, _, gone, _) = Start(); var law = Cat.Law("corn_law")!;
        string says = SenateActs.ToHouse(w, gone, law.DefaultOption, law.Option("free_corn")!, Content.Offices);
        Assert.DoesNotContain("would pay", says); Assert.DoesNotContain("unrest on", says);
        Assert.Contains("has broken away from the Reach and is bound only by its own laws", says);
        // what falls on a region still falls on the regions where its worlds lie
        var citizenship = Cat.Law("citizenship")!;
        Assert.Contains("every region's allegiance would settle 8 higher", SenateActs.ToHouse(w, gone, citizenship.DefaultOption, citizenship.Option("whole_reach")!, Content.Offices));
    }

    // ---------------------------------------------------------------- a rule between two parties binds the party of the Reach

    [Fact]
    public void The_Usury_Law_caps_a_loan_where_the_lender_is_of_the_Reach_and_only_there()
    {
        var (tc, w, stays, gone, _) = Start(); var v = Content.Businesses; var f = v.Finance;
        Act(w, "usury", "tenth");
        // the counting-houses lend on a house's own worlds: to a house of the Reach at the cap, to one that has broken away as they would
        LoanTerms Counting(House h) => Lending.Lenders(w, h.Id, v).First(t => t.LenderId == Lending.CountingHouses);
        Assert.Equal(10, Counting(stays).RatePct); Assert.True(Counting(gone).RatePct >= Lending.BaseRate(f)); Assert.DoesNotContain("allows no more than", Counting(gone).Why);
        // and the stewards' standing loan: a house that has broken away borrows the whole of it, and owes what it always did
        gone.Debt = 0; int before = gone.Treasury.Credits; new TakeLoanCommand(Economy.HeadOf(w, gone)!.Id, v).Execute(new CommandContext(w, TestContent.Rng()));
        Assert.Equal(f.LoanPrincipal, gone.Treasury.Credits - before); Assert.Equal(f.LoanRepay, gone.Debt);
        stays.Debt = 0; before = stays.Treasury.Credits; new TakeLoanCommand(Economy.HeadOf(w, stays)!.Id, v).Execute(new CommandContext(w, TestContent.Rng()));
        Assert.True(stays.Treasury.Credits - before < f.LoanPrincipal); Assert.True(stays.Debt < f.LoanRepay);
        // a bank: one of a house that has broken away lends to a house of the Reach at its own rate; one of the Reach lends to it at the cap
        var sys = w.Galaxy!.System(FirstWorld(w, gone).SystemId)!; FoundBusinessCommand.Open(w, gone, v.Def("merchant_bank")!, sys);
        var sys2 = w.Galaxy!.System(FirstWorld(w, stays).SystemId)!; FoundBusinessCommand.Open(w, stays, v.Def("merchant_bank")!, sys2);
        w.Relationship(Economy.HeadOf(w, gone)!.Id, Economy.HeadOf(w, stays)!.Id).Disposition = 0; w.Relationship(Economy.HeadOf(w, stays)!.Id, Economy.HeadOf(w, gone)!.Id).Disposition = 0;
        var goneLends = Lending.Lenders(w, stays.Id, v).Single(t => t.LenderId == gone.Id); var staysLends = Lending.Lenders(w, gone.Id, v).Single(t => t.LenderId == stays.Id);
        Assert.True(goneLends.RatePct > 10, $"{goneLends.RatePct}"); Assert.DoesNotContain("allows no more than", goneLends.Why);
        Assert.Equal(10, staysLends.RatePct); Assert.Contains("the Usury Law allows no more than 10", staysLends.Why);
        // a loan between houses: a lender that has broken away may ask what it likes, a lender of the Reach may not ask a house that has broken away more than the cap
        var third = Another(w, stays, gone); int small = DealingsSystem.LoanSizes[0];
        Assert.DoesNotContain("Usury", tc.Dealings.LendRefusal(w, Economy.HeadOf(w, gone), third, small, 30) ?? "");
        Assert.Equal("the Usury Law allows no more than 10 in the hundred, and this is 30", tc.Dealings.LendRefusal(w, Economy.HeadOf(w, stays), gone, small, 30));
        Assert.Equal(small + (small * 30 + 99) / 100, tc.Dealings.Lend(w, gone, third, small, 30).Owed);   // not cut to the cap
        Assert.Equal(small + small * 10 / 100, tc.Dealings.Lend(w, stays, gone, small, 10).Owed);   // at the cap, and never rounded past it
    }

    [Fact]
    public void The_Navigation_Act_s_levy_is_taken_where_the_seller_is_of_the_Reach()
    {
        var (tc, w, stays, gone, _) = Start(); var chair = RegionSystem.CapitalHolder(w)!; string good = Resource.Materials;
        Act(w, "navigation_act", "levy");
        // a house that has broken away sells to a house of the Reach: no levy
        gone.Treasury.Set(good, 40); stays.Treasury.Credits = 1000;
        long chairBefore = chair.Treasury.Total, goneBefore = gone.Treasury.Total;
        int paid = tc.HouseTrade.Buy(w, stays, gone, good, 10); Assert.True(paid > 0);
        Assert.Equal(0, SenateActs.LevyOn(w, gone, paid)); Assert.Equal(chairBefore, chair.Treasury.Total); Assert.Equal(goneBefore + (long)paid * Money.PerUnit, gone.Treasury.Total);
        Assert.Contains("has broken away from the Reach, and pays no levy on what it sells", tc.HouseTrade.PriceSays(w, gone, stays, good, selling: true));
        // a house of the Reach sells to one that has broken away: the chair takes its tenth
        stays.Treasury.Set(good, 40); gone.Treasury.Credits = 1000; chairBefore = chair.Treasury.Total; long staysBefore = stays.Treasury.Total;
        paid = tc.HouseTrade.Buy(w, gone, stays, good, 10); long levy = (long)paid * Money.PerUnit / 10; Assert.True(levy > 0);
        Assert.Equal(chairBefore + levy, chair.Treasury.Total); Assert.Equal(staysBefore + (long)paid * Money.PerUnit - levy, stays.Treasury.Total);
    }

    [Fact]
    public void The_staple_forbids_a_private_sale_where_either_party_is_of_the_Reach_and_not_between_two_houses_that_have_broken_away()
    {
        var (_, w, stays, gone, gone2) = Start(twoGo: true);
        foreach (var (a, b) in new[] { (gone, gone2), (gone2, gone) }) w.Relationship(Economy.HeadOf(w, a)!.Id, Economy.HeadOf(w, b)!.Id).Disposition = 0;
        Assert.True(HouseTradeSystem.Trades(w, gone, gone2));
        Act(w, "navigation_act", "staple");
        Assert.StartsWith("the Navigation Act forbids it", HouseTradeSystem.WontTrade(w, stays, gone)); Assert.StartsWith("the Navigation Act forbids it", HouseTradeSystem.WontTrade(w, gone, stays));
        Assert.False(HouseTradeSystem.Trades(w, stays, gone)); Assert.False(HouseTradeSystem.Trades(w, gone, stays));
        Assert.Null(HouseTradeSystem.WontTrade(w, gone, gone2)); Assert.True(HouseTradeSystem.Trades(w, gone, gone2)); Assert.True(HouseTradeSystem.Trades(w, gone2, gone));
    }

    [Fact]
    public void The_Law_against_Forestalling_binds_the_holder_of_a_corner_that_is_of_the_Reach_and_not_one_that_has_broken_away()
    {
        foreach (var option in new[] { "capped", "forbidden" })
            foreach (bool broke in new[] { false, true })
            {
                var (_, w, stays, gone, _) = Start(); var holder = broke ? gone : stays; var cat = Content.Market;
                var market = MarketSystem.Of(w, cat); var d = cat.Goods.Where(x => market.Goods.ContainsKey(x.Id) && market.Goods[x.Id].Stock >= 4).OrderBy(x => x.Id, StringComparer.Ordinal).First(); var g = market.Goods[d.Id];
                foreach (var h in w.Houses.Values) h.Treasury.Set(d.Id, 0);
                int stock = (int)Math.Floor(g.Stock); holder.Treasury.Set(d.Id, Math.Max(CornerSystem.LeastUnits, 2 * stock)); int all = stock + holder.Treasury.Get(d.Id), held = holder.Treasury.Get(d.Id);
                Act(w, "forestalling", option);
                string said = CornerSystem.Held(w, Content.Trade, holder.Id, d.Id);
                CornerSystem.Reckon(w, cat, d, g);
                if (option == "capped" && !broke) { Assert.Equal(0.20, g.CornerPremium, 3); Assert.Contains("allows no more", said); }
                if (option == "capped" && broke) { Assert.True(g.CornerPremium > 0.5, $"{g.CornerPremium}"); Assert.DoesNotContain("allows no more", said); }
                if (option == "forbidden" && !broke) { Assert.Equal(SenateActs.MayKeep(all), holder.Treasury.Get(d.Id)); Assert.False(g.Cornered); Assert.Contains("will be made to sell", said); }
                if (option == "forbidden" && broke) { Assert.Equal(held, holder.Treasury.Get(d.Id)); Assert.True(g.Cornered); Assert.Equal(0, w.Counter("corners_broken_by_law")); Assert.DoesNotContain("will be made to sell", said); }
            }
    }

    /// <summary>A fleet of one of the two houses lying at home, and a house of the Reach at war that would hire it.</summary>
    private static (World w, Fleet f, House home, House employer, House chair) Company(bool broke, string option)
    {
        var (_, w, stays, gone, _) = Start(); var home = broke ? gone : stays; var chair = RegionSystem.CapitalHolder(w)!;
        var f = new Fleet { Id = "test_company", Name = "The Test Company", OwnerId = home.Id, SystemId = FirstWorld(w, home).SystemId, Strength = 3, Readiness = 80 }; w.Fleets[f.Id] = f;
        var employer = Another(w, stays, gone); var enemy = Another(w, stays, gone, employer);
        w.Wars.RemoveAll(x => x.Involves(home.Id) || x.Involves(employer.Id)); w.Wars.Add(new War { HouseA = employer.Id, HouseB = enemy.Id, Since = w.Turn });
        employer.Treasury.Credits = 500; home.Treasury.Credits = 500;
        w.Relationship(Economy.HeadOf(w, employer)!.Id, Economy.HeadOf(w, home)!.Id).Disposition = 10;
        if (option.Length > 0) Act(w, "hired_companies", option);
        return (w, f, home, employer, chair);
    }

    [Fact]
    public void The_Law_of_Hired_Companies_binds_a_house_of_the_Reach_that_hires_its_fleet_out_and_not_one_that_has_broken_away()
    {
        foreach (bool broke in new[] { false, true })
        {
            // forbidden: a house of the Reach may hire no fleet out, and its company comes home; one that has broken away hires out as it likes
            var (w, f, home, employer, _) = Company(broke, "forbidden"); var head = Economy.HeadOf(w, home)!;
            string? no = MercenarySystem.Refusal(w, head, f.Id, employer.Id);
            if (broke) Assert.Null(no); else Assert.StartsWith("the Law of Hired Companies forbids it", no);
            Act(w, "hired_companies", "free"); MercenarySystem.Hire(w, head, f, employer); Assert.Equal(employer.Id, f.OwnerId);
            Act(w, "hired_companies", "forbidden"); w.Turn++; MercenarySystem.Tick(w);
            Assert.Equal(broke ? employer.Id : home.Id, f.OwnerId);
            // licensed: a house of the Reach pays the chair for the licence and loses less standing; one that has broken away pays none and is shamed as before
            var (w2, f2, home2, employer2, chair2) = Company(broke, "licensed"); var head2 = Economy.HeadOf(w2, home2)!;
            int credits = home2.Treasury.Credits + MercenarySystem.PayOf(f2), legit = home2.Legitimacy, chairs = chair2.Treasury.Credits;
            Assert.Equal(broke, !MercenarySystem.Says(w2, f2, employer2).Contains("for the licence"));
            MercenarySystem.Hire(w2, head2, f2, employer2);
            Assert.Equal(legit - (broke ? MercenarySystem.Legitimacy : SenateActs.LicensedLegitimacy), home2.Legitimacy);
            Assert.Equal(credits - (broke ? 0 : 3), home2.Treasury.Credits); Assert.Equal(chairs + (broke ? 0 : 3), chair2.Treasury.Credits);
        }
    }

    // ---------------------------------------------------------------- what reaches a house that has broken away through a party of the Reach

    [Fact]
    public void A_house_that_has_broken_away_is_told_what_falls_on_it_through_a_party_of_the_Reach()
    {
        var (_, w, stays, gone, gone2) = Start(twoGo: true);
        // the staple lifted: the houses of the Reach may deal with it out of their stores again
        var nav = Cat.Law("navigation_act")!; var staple = nav.Option("staple")!;
        foreach (var to in new[] { "free", "levy" })
        {
            string says = SenateActs.ToHouse(w, gone, staple, nav.Option(to)!, Content.Offices);
            Assert.Contains("the houses of the Reach could again buy of your house and sell to it out of their stores", says); Assert.DoesNotContain("nothing", says);
        }
        // a cap on usury: the banks of the Reach lend to it under the cap, and its own bank and the counting-houses as they like
        FoundBusinessCommand.Open(w, stays, Content.Businesses.Def("merchant_bank")!, w.Galaxy!.System(FirstWorld(w, stays).SystemId)!);
        FoundBusinessCommand.Open(w, gone2, Content.Businesses.Def("merchant_bank")!, w.Galaxy!.System(FirstWorld(w, gone2).SystemId)!);
        var usury = Cat.Law("usury")!;
        string capped = SenateActs.ToHouse(w, gone, usury.DefaultOption, usury.Option("tenth")!, Content.Offices);
        Assert.Contains($"the bank of {stays.Name}", capped); Assert.Contains("could lend your house at no more than 10 in the hundred", capped);
        Assert.DoesNotContain($"the bank of {gone2.Name}", capped); Assert.DoesNotContain("counting-houses", capped); Assert.DoesNotContain("nothing", capped);
        Assert.Contains("could lend your house at what rate they like", SenateActs.ToHouse(w, gone, usury.Option("tenth")!, usury.DefaultOption, Content.Offices));
        // a ban on hired companies: a company hired into its line from a house of the Reach would go home, and one from a house that has broken away would not
        var hc = Cat.Law("hired_companies")!; var ban = hc.Option("forbidden")!;
        var f = new Fleet { Id = "test_hired", Name = "The Test Company", OwnerId = gone.Id, HiredFrom = stays.Id, SystemId = FirstWorld(w, gone).SystemId, Strength = 3 }; w.Fleets[f.Id] = f;
        Assert.Contains("The Test Company, hired into your line from a house of the Reach, would go home", SenateActs.ToHouse(w, gone, hc.DefaultOption, ban, Content.Offices));
        f.HiredFrom = gone2.Id; Assert.DoesNotContain("The Test Company", SenateActs.ToHouse(w, gone, hc.DefaultOption, ban, Content.Offices));
    }

    [Fact]
    public void The_chamber_s_answer_to_a_petition_falls_on_the_houses_of_the_Reach_and_not_on_one_that_has_broken_away()
    {
        var (tc, w, stays, gone, _) = Start(); var senate = tc.Senate; var me = w.House(w.Protagonist!.HouseId)!;
        me.PolityId = State; w.Polities.First(x => x.Id == State).Members.Add(me.Id);   // the player's house has broken away too
        w.Motions.RemoveAll(x => x.Open);
        foreach (var h in new[] { stays, gone, me }) w.WorldCounters[PolicySystem.ApprovalKey(w, h.Id, "commons")] = -50;
        var m = senate.Add(w, SenateSystem.EstatePrefix + "commons", "petition", "", lawId: "corn_law", lawOption: "free_corn"); foreach (var h in w.Houses.Values) m.Votes[h.Id] = -1;
        int Unrest(House h) => w.HoldingsOf(h.Id).Where(x => x.IsPlanet).Sum(x => x.Unrest);
        int staysU = Unrest(stays), goneU = Unrest(gone), meU = Unrest(me), offset = w.Counter("estate_offset:commons");
        w.Turn++; senate.Tick(w);
        Assert.False(m.Passed); Assert.True(w.HasFlag("petition_refused"));
        Assert.Equal(-50 - SenateSystem.PetitionSpurn, PolicySystem.Approval(w, stays.Id, "commons")); Assert.True(Unrest(stays) > staysU);
        Assert.Equal(-50, PolicySystem.Approval(w, gone.Id, "commons")); Assert.Equal(goneU, Unrest(gone));
        Assert.Equal(-50, PolicySystem.Approval(w, me.Id, "commons")); Assert.Equal(offset, w.Counter("estate_offset:commons")); Assert.Equal(meU, Unrest(me));
    }

    [Fact]
    public void An_estate_petitions_the_Senate_for_what_it_thinks_in_the_houses_of_the_Reach_alone()
    {
        var (tc, w, _, gone, gone2) = Start(twoGo: true); var senate = tc.Senate;
        w.Motions.RemoveAll(x => x.Open);
        foreach (var h in w.Houses.Values) foreach (var e in Cat.Estates) w.WorldCounters[PolicySystem.ApprovalKey(w, h.Id, e.Id)] = 0;
        var reach = w.Houses.Values.Where(h => w.OfTheReach(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList(); int need = Math.Max(SenateSystem.PetitionMinHouses, reach.Count / 8);
        foreach (var h in reach.Take(need - 1).Append(gone).Append(gone2)) w.WorldCounters[PolicySystem.ApprovalKey(w, h.Id, "commons")] = -50;
        senate.Petition(w); Assert.DoesNotContain(w.Motions, x => x.Open && SenateSystem.IsPetition(x));   // one short in the Reach: the houses that have left it do not make up the number
        Assert.Equal(need - 1, CourtProjection.Build(tc, Content).Chancery!.Estates.Single(e => e.Id == "commons").HostileIn);
        w.WorldCounters[PolicySystem.ApprovalKey(w, reach[need - 1].Id, "commons")] = -50;
        senate.Petition(w); Assert.Contains(w.Motions, x => x.Open && SenateSystem.PetitionEstate(x) == "commons");
        Assert.Contains(w.History, x => x.Text.Contains($"hostile in {need} houses of the Reach"));
    }

    [Fact]
    public void A_house_of_the_Reach_does_not_lean_on_an_Act_for_what_it_would_not_do_to_a_party_that_has_broken_away()
    {
        // a corner: the Law against Forestalling would not touch one held by a house that has broken away, so a house that pays its price has no cause in it
        var (_, w, stays, gone, _) = Start(); var voter = Another(w, stays, gone); var law = Cat.Law("forestalling")!;
        w.Fleets["test_line"] = new Fleet { Id = "test_line", Name = "Test", OwnerId = voter.Id, SystemId = FirstWorld(w, voter).SystemId, Strength = 4 };
        var market = MarketSystem.Of(w, Content.Market); foreach (var x in market.Goods.Values) { x.CornerHouse = ""; x.CornerPremium = 0; x.Cornered = false; }
        var g = market.Goods.Values.OrderBy(x => x.Id, StringComparer.Ordinal).First(); g.CornerPremium = 0.6; g.CornerShare = 0.66; g.Cornered = true;
        List<string> Why(House h, LawDef l, string from, string to) { var why = new List<(string why, int by)>(); SenateActs.Interest(w, h, l.Option(from)!, l.Option(to)!, Content.Offices, why); return why.Select(x => x.why).ToList(); }
        const string Pays = "a corner stands, and it pays the price of it";
        g.CornerHouse = gone.Id; Assert.DoesNotContain(Pays, Why(voter, law, "free_market", "capped"));
        g.CornerHouse = stays.Id; Assert.Contains(Pays, Why(voter, law, "free_market", "capped"));
        // a hired fleet: a ban would not call home a company hired from a house that has broken away
        foreach (bool broke in new[] { true, false })
        {
            var (w2, f, home, employer, _) = Company(broke, ""); w = w2; MercenarySystem.Hire(w2, Economy.HeadOf(w2, home)!, f, employer); Assert.Equal(home.Id, f.HiredFrom);
            var enemy = w2.House(w2.EnemiesOf(employer.Id).OrderBy(x => x, StringComparer.Ordinal).First())!; var hc = Cat.Law("hired_companies")!;
            Assert.Equal(!broke, Why(employer, hc, "free", "forbidden").Contains("a hired fleet fights in its line"));
            Assert.Equal(!broke, Why(enemy, hc, "free", "forbidden").Contains("its enemy fights it with hired fleets"));
        }
    }

    // ---------------------------------------------------------------- what belongs to the Reach's own places and institutions

    [Fact]
    public void The_Reach_s_own_places_and_institutions_still_bind_a_house_that_has_broken_away_and_uses_them()
    {
        var (tc, w, stays, gone, _) = Start(); gone.Treasury.Credits = 1000; stays.Treasury.Credits = 1000;
        // the exchanges' licence on a good: weapons-grade isotopes are bought on no open market, by anyone, until the Senate licenses it
        foreach (var h in new[] { stays, gone }) Assert.Contains("cannot be bought on any open market", MarketSystem.QuoteBuy(w, Content.Trade, h.Id, Resource.Isotopes, 1).Refusal);
        Act(w, "arms_market", "licensed");
        foreach (var h in new[] { stays, gone }) Assert.DoesNotContain("open market", MarketSystem.QuoteBuy(w, Content.Trade, h.Id, Resource.Isotopes, 1).Refusal ?? "");
        // the Exchange's levy on shares: whoever trades there pays it
        var cs = tc.Companies!; var co = w.Companies.First(c => c.Chartered);
        Act(w, "exchange_levy", "heavy");
        Assert.True(cs.QuoteBuy(w, gone.Id, co, 10).Levy > 0); Assert.Equal(cs.QuoteBuy(w, stays.Id, co, 10).Levy, cs.QuoteBuy(w, gone.Id, co, 10).Levy);
        // the Reach's offices: a person of a house that has broken away stands for none of them, whatever the Law of the Years says
        var rung = Content.Offices.Rungs.First(r => r.Requires.Length == 0); var c = w.LivingCharacters().First(x => x.HouseId == gone.Id);
        Act(w, "law_of_years", "relaxed"); Assert.Equal("the house has left the Reach", LadderSystem.Bar(w, Content.Offices, c, rung));
        // the Senate's own procedure: a house that has broken away moves nothing there
        Assert.False(tc.Senate.MayPropose(w, gone.Id));
        // and a region is a place: its allegiance settles where the Citizenship of the Reach puts it, whoever holds worlds in it
        var region = w.Galaxy!.Region(w.Galaxy.System(FirstWorld(w, gone).SystemId)!.RegionId)!; w.Laws.Remove(PolicySystem.SenateHolder); int plain = tc.Regions.AllegianceTarget(w, region);
        Act(w, "citizenship", "whole_reach"); Assert.Equal(Math.Min(100, plain + 8), tc.Regions.AllegianceTarget(w, region));
    }
}
