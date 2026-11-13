using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The Exchange (docs/ECONOMY_MARKET.md stage C): companies, shares, dividends, control.</summary>
public class CompanyTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static (TurnController tc, World w, ContentDatabase content, House mine) Start(string scenario = "the_old_lion", int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content);
        return (tc, w, Content, w.House(w.Protagonist!.HouseId)!);
    }
    private static bool Do(World w, ICommand cmd) => new CommandExecutor().TryExecute(cmd, new CommandContext(w, TestContent.Rng()));
    private static Business Venture(World w, ContentDatabase content, House house, string kind = "trading_company")
    {
        var def = content.Businesses.Def(kind)!; var sys = w.Galaxy!.System(w.HoldingsOf(house.Id).First(h => h.IsPlanet).SystemId)!;
        return FoundBusinessCommand.Open(w, house, def, sys);
    }

    [Fact]
    public void The_great_companies_are_listed_when_the_exchange_opens()
    {
        var (tc, w, content, _) = Start();
        Assert.Equal(content.Exchange.Chartered.Count, w.Companies.Count(c => c.Chartered));
        Assert.All(w.Companies, c => { Assert.Equal(c.Shares, c.HeldBy(Company.Public)); Assert.True(c.Price > 0); });
        Assert.NotNull(tc.Companies);
    }

    [Fact]
    public void Incorporating_a_venture_gives_the_founder_every_share_and_moves_its_earnings_into_the_company()
    {
        var (tc, w, content, mine) = Start(); var cs = tc.Companies!; var b = Venture(w, content, mine);
        mine.Treasury.Influence = 5; int before = tc.Economy!.Compute(w, mine.Id, tc.Fleets).Lines.Count(l => l.Kind == "venture");
        Assert.True(Do(w, new IncorporateCommand(w.ProtagonistId, b.Id, "", cs)));
        var co = w.Companies.Single(c => !c.Chartered);
        Assert.Equal(100, co.HeldBy(mine.Id)); Assert.Equal(mine.Id, co.ControllerId); Assert.Equal(co.Id, b.CompanyId);
        Assert.Equal(5 - cs.Rules.IncorporateInfluence, mine.Treasury.Influence);
        Assert.Equal(before - 1, tc.Economy.Compute(w, mine.Id, tc.Fleets).Lines.Count(l => l.Kind == "venture"));
        // a year on, the company has earned and declared; the year after, the ledger pays the dividend
        cs.Tick(w);
        Assert.True(co.LastEarnings > 0); Assert.True(co.LastDividend > 0);
        Assert.Contains(tc.Economy.Compute(w, mine.Id, tc.Fleets).Lines, l => l.Kind == "dividend" && l.Delta.Credits == co.LastDividend);
        Assert.True(Wealth.PaperOf(w, mine.Id) > 0);
    }

    [Fact]
    public void A_float_raises_capital_for_the_company_and_dilutes_its_founder()
    {
        var (tc, w, content, mine) = Start(); var cs = tc.Companies!; var b = Venture(w, content, mine); mine.Treasury.Influence = 5;
        Do(w, new IncorporateCommand(w.ProtagonistId, b.Id, "", cs)); var co = w.Companies.Single(c => !c.Chartered);
        int credits = mine.Treasury.Credits; int raise = cs.FloatProceeds(co, 25);
        Assert.True(Do(w, new BoardCommand(w.ProtagonistId, co.Id, BoardCommand.Float, 25, cs)));
        Assert.Equal(125, co.Shares); Assert.Equal(25, co.HeldBy(Company.Public)); Assert.Equal(raise, co.Cash); Assert.Equal(credits, mine.Treasury.Credits);
        Assert.Equal(80, CompanySystem.StakePct(co, mine.Id));
        Assert.False(Do(w, new BoardCommand(w.ProtagonistId, co.Id, BoardCommand.Float, 5, cs)));   // one float a year
        Assert.NotNull(cs.FloatRefusal(w, mine.Id, co, 5));
    }

    [Fact]
    public void An_order_costs_more_a_share_the_larger_it_is_and_cannot_be_split_to_dodge_it()
    {
        var (tc, w, _, mine) = Start(); var cs = tc.Companies!; var co = w.Companies.First(c => c.Chartered); mine.Treasury.Credits = 5000;
        var small = cs.QuoteBuy(w, mine.Id, co, 2); var large = cs.QuoteBuy(w, mine.Id, co, 20);
        Assert.True(large.UnitPrice > small.UnitPrice);
        int whole = large.Credits; int paid = 0;
        for (int i = 0; i < 10; i++) { int c0 = mine.Treasury.Credits; Assert.True(cs.Buy(w, mine.Id, co, 2)); paid += c0 - mine.Treasury.Credits; }
        Assert.True(paid >= whole);
        Assert.NotNull(cs.QuoteBuy(w, mine.Id, co, 1).Refusal);   // a fifth of a company in a year, and no more
        // a round trip loses money
        int before = mine.Treasury.Credits + paid; cs.Sell(w, mine.Id, co, 20); Assert.True(mine.Treasury.Credits < before);
    }

    [Fact]
    public void More_than_half_the_shares_takes_control_whoever_founded_it()
    {
        var (tc, w, content, mine) = Start(); var cs = tc.Companies!;
        var rival = w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId && w.HoldingsOf(h.Id).Any(x => x.IsPlanet)).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        var b = Venture(w, content, rival); rival.Treasury.Influence = 0;   // no influence: its founder's share cannot be invoked
        var co = cs.Incorporate(w, rival, b); Assert.Equal(rival.Id, b.OwnerId);
        cs.Sell(w, rival.Id, co, 60);   // the founder sells down to forty
        Assert.Equal(rival.Id, co.ControllerId);   // still the largest holder
        mine.Treasury.Credits = 5000;
        for (int year = 0; year < 3; year++) { Assert.True(Do(w, new BuySharesCommand(w.ProtagonistId, co.Id, 17, cs))); co.BoughtThisTurn.Clear(); }
        Assert.Equal(51, co.HeldBy(mine.Id)); Assert.Equal(mine.Id, co.ControllerId); Assert.Equal(mine.Id, b.OwnerId);
        Assert.Contains(CompanySystem.TakenByMeFlag, w.WorldFlags);
        Assert.Equal(1, w.Counter(CompanySystem.TakeoversCounter));
    }

    [Fact]
    public void A_founder_refuses_a_takeover_once_and_pays_for_it_in_influence()
    {
        var (tc, w, content, mine) = Start(); var cs = tc.Companies!;
        var rival = w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId && w.HoldingsOf(h.Id).Any(x => x.IsPlanet)).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        var b = Venture(w, content, rival); rival.Treasury.Influence = 10; var co = cs.Incorporate(w, rival, b); int infl = rival.Treasury.Influence;
        cs.Sell(w, rival.Id, co, 60); mine.Treasury.Credits = 5000;
        for (int year = 0; year < 2; year++) { Assert.True(cs.Buy(w, mine.Id, co, 20)); co.BoughtThisTurn.Clear(); }
        int credits = mine.Treasury.Credits;
        Assert.False(cs.Buy(w, mine.Id, co, 11));   // 51: the board turns it away
        Assert.True(co.GoldenShareUsed); Assert.Equal(credits, mine.Treasury.Credits); Assert.Equal(infl - cs.Rules.GoldenInfluence, rival.Treasury.Influence);
        Assert.Equal(rival.Id, co.ControllerId);
        co.BoughtThisTurn.Clear();
        Assert.True(cs.Buy(w, mine.Id, co, 11)); Assert.Equal(mine.Id, co.ControllerId);   // it cannot do so twice
    }

    [Fact]
    public void A_declared_war_moves_the_sectors_the_same_year()
    {
        var (tc, w, _, _) = Start(); var cs = tc.Companies!;
        double armsPeace = cs.Outlook(w, "arms"), bankPeace = cs.Outlook(w, "finance"); var (confPeace, _) = cs.ConfidenceTarget(w);
        var hs = w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId).OrderBy(h => h.Id, StringComparer.Ordinal).Take(2).ToList();
        w.Wars.Add(new War { HouseA = hs[0].Id, HouseB = hs[1].Id, Since = w.Turn });
        Assert.True(cs.Outlook(w, "arms") > armsPeace); Assert.True(cs.Outlook(w, "finance") < bankPeace);
        Assert.True(cs.ConfidenceTarget(w).target < confPeace);
        var bank = w.Company("bank_of_the_reach")!; double before = bank.Price; int earned = cs.Earnings(w, bank);
        w.Wars.Clear(); Assert.True(cs.Earnings(w, bank) > earned);   // and its earnings with it
        w.Wars.Add(new War { HouseA = hs[0].Id, HouseB = hs[1].Id, Since = w.Turn });
        cs.Tick(w); cs.Tick(w);
        Assert.True(bank.Price < before);
    }

    [Fact]
    public void The_levy_on_a_trade_is_paid_to_the_chair()
    {
        var (tc, w, content, mine) = Start(); var cs = tc.Companies!; var co = w.Companies.First(c => c.Chartered); mine.Treasury.Credits = 1000;
        var plain = cs.QuoteBuy(w, mine.Id, co, 10); Assert.Equal(0, plain.Levy);
        PolicySystem.Enact(w, content.Policies, PolicySystem.SenateHolder, "exchange_levy", "heavy");
        var taxed = cs.QuoteBuy(w, mine.Id, co, 10);
        Assert.True(taxed.Levy > 0); Assert.Equal(plain.Credits + taxed.Levy, taxed.Credits);
    }

    [Fact]
    public void A_company_that_cannot_pay_its_way_is_wound_up_and_a_sole_holder_may_take_it_private()
    {
        var (tc, w, content, mine) = Start(); var cs = tc.Companies!; var b = Venture(w, content, mine); mine.Treasury.Influence = 9;
        Do(w, new IncorporateCommand(w.ProtagonistId, b.Id, "", cs)); var co = w.Companies.Single(c => !c.Chartered);
        co.Cash = 40; int credits = mine.Treasury.Credits;
        Assert.True(Do(w, new BoardCommand(w.ProtagonistId, co.Id, BoardCommand.Private, 0, cs)));
        Assert.Empty(w.Companies.Where(c => !c.Chartered)); Assert.Equal("", b.CompanyId); Assert.Equal(credits + 40, mine.Treasury.Credits);
        Do(w, new IncorporateCommand(w.ProtagonistId, b.Id, "", cs)); co = w.Companies.Single(c => !c.Chartered);
        co.Cash = cs.Rules.FailCash - 200; cs.Tick(w);
        Assert.Empty(w.Companies.Where(c => !c.Chartered)); Assert.False(w.Businesses.ContainsKey(b.Id)); Assert.Contains(CompanySystem.FailedFlag, w.WorldFlags);
    }

    [Fact]
    public void Sixty_years_of_stewards_on_the_exchange_leave_no_house_with_most_of_the_wealth()
    {
        var (tc, w, _, _) = Start("the_fallen_house", 3);
        for (int t = 0; t < 60 && !w.IsDynastyDead; t++)
        {
            foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); }
            tc.EndTurn();
        }
        var hs = w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId).ToList(); int total = Math.Max(1, hs.Sum(h => Math.Max(0, h.Wealth)));
        Assert.True(hs.Max(h => Math.Max(0, h.Wealth)) * 100 / total < 35);
        Assert.True(w.Counter(CompanySystem.ListedCounter) > 0); Assert.True(w.Counter(CompanySystem.SharesTradedCounter) > 0);
        Assert.InRange(w.Market!.Index, 20, 600); Assert.InRange(w.Market.Confidence, 0.59, 1.26);
        Assert.All(w.Companies, c => { Assert.Equal(c.Shares, c.Holders.Values.Sum()); Assert.True(c.Price > 0); });
    }
}
