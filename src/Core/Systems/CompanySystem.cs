using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

public sealed record ShareQuote(int Credits, int Levy, double UnitPrice, string? Refusal);

/// <summary>
/// The Exchange (docs/ECONOMY_MARKET.md stages C and D). A company is one or more ventures under a name, with shares, cash and a
/// dividend policy. Its ventures pay the company; the company pays its holders by their shares, a year in arrears, through the ledger.
/// A share's price is read each year from real things, with no scripted events:
/// <list type="bullet">
/// <item>FUNDAMENTALS: what the company earns at this year's goods prices, times a multiple, plus its cash, a share;</item>
/// <item>the SECTOR'S OUTLOOK: war lifts arms and extraction and frightens trade, shipping and the banks; lawless lanes hurt whatever
/// travels; scarce goods lift whoever digs them;</item>
/// <item>CONFIDENCE in the Reach: down with wars at home and abroad, the great crisis and houses in debt, up with years of peace;</item>
/// <item>FLOW: this year's net buying or selling, which fades.</item>
/// </list>
/// Whoever holds more than half directs the company, whoever founded it. RNG-free: a small hash wobble keeps twins apart.
/// </summary>
public sealed class CompanySystem
{
    public const string CrashFlag = "market_crash", BoomFlag = "market_boom", DividendCutFlag = "dividend_cut", StakeFlag = "stake_in_mine", FailedFlag = "company_failed",
        TakenFromMeFlag = "takeover_of_mine", TakenByMeFlag = "company_taken", FloatedFlag = "company_floated", GoldenFlag = "golden_share_used", ListedCounter = "companies_listed",
        SharesTradedCounter = "shares_traded", TakeoversCounter = "takeovers";
    private static readonly string[] TickFlags = { CrashFlag, BoomFlag, DividendCutFlag, StakeFlag, FailedFlag };
    private static readonly string[] DeedFlags = { TakenFromMeFlag, TakenByMeFlag, FloatedFlag, GoldenFlag };

    private readonly Economy _economy; private readonly BusinessCatalog _ventures; private readonly ExchangeCatalog _exchange; private readonly TradeRules _trade;
    public CompanySystem(Economy economy, BusinessCatalog ventures, ExchangeCatalog exchange, TradeRules trade) { _economy = economy; _ventures = ventures; _exchange = exchange; _trade = trade; }
    public ExchangeCatalog Catalog => _exchange;
    public ExchangeRules Rules => _exchange.Rules;
    public BusinessCatalog Ventures => _ventures;
    public bool Open => !_exchange.IsEmpty;
    /// <summary>The nations beyond the Reach, for naming a war in the cause line.</summary>
    public NationCatalog? Nations { get; init; }

    private static MarketState Market(World w) => w.Market ??= new MarketState();
    private static uint Hash(string key) { uint h = 2166136261; foreach (var ch in key) h = (h ^ ch) * 16777619; return h; }
    /// <summary>A flag a deed raises lasts through the next year's events, then is swept.</summary>
    private static void Sour(World w, string fromId, string toId, int delta) { var rel = w.Relationship(fromId, toId); rel.Disposition = Math.Clamp(rel.Disposition + delta, -100, 100); }
    private static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }

    // ---------------------------------------------------------------- reading a company

    public static IEnumerable<Business> VenturesOf(World w, Company co) => w.Businesses.Values.Where(b => b.CompanyId == co.Id).OrderBy(b => b.Id, StringComparer.Ordinal);

    /// <summary>What the company earns in a year at this year's prices: its ventures' net, or a chartered company's income as the world moves it.</summary>
    public int Earnings(World w, Company co)
    {
        if (co.Chartered)
        {
            var def = _exchange.Charter(co.CharterId); double f = 1;
            if (def is { Good.Length: > 0 } && _trade.Market.Good(def.Good) is { } g && w.Market?.Good(g.Id) is { } gs) f = Math.Clamp(gs.Price / g.Base, 0.5, 2);
            return (int)Math.Round(co.CharterIncome * f * Outlook(w, co.Sector));
        }
        return VenturesOf(w, co).Sum(b => _economy.VentureEarnings(w, b));
    }

    /// <summary>Book value: its ventures at cost, its cash, half of what is sunk in its building sites; a chartered company, four years' income.</summary>
    public int Book(World w, Company co)
    {
        int v = co.Chartered ? co.CharterIncome * 4 : VenturesOf(w, co).Sum(b => _ventures.Def(b.Kind)?.Value(b.Level) ?? 0);
        v += Math.Max(0, co.Cash);
        foreach (var p in w.Projects.Where(p => p.CompanyId == co.Id)) v += (int)(Economy.WorthOf(w, p.Paid) / 2);
        return v;
    }

    private static int WarsOf(World w) => Math.Min(4, w.Wars.Count + (NationSystem.AtWar(w) != null ? 1 : 0));
    private static double Lawless(World w) => w.Regions.Count == 0 ? 0.2 : w.Regions.Values.Average(r => r.Lawlessness) / 100.0;
    private double Scarcity(World w)
    {
        if (_trade.Market.IsEmpty || w.Market == null) return 0;
        var xs = _trade.Market.Goods.Where(g => g.Sell && w.Market.Good(g.Id) != null).Select(g => Math.Clamp(w.Market.Good(g.Id)!.Price / g.Base - 1, -0.5, 1)).ToList();
        return xs.Count == 0 ? 0 : xs.Average();
    }

    /// <summary>How a sector looks at the state of the Reach this year, about 0.6..1.5.</summary>
    public double Outlook(World w, string sector)
    {
        var s = _exchange.Sector(sector); if (s == null) return 1;
        double o = 1 + s.WarPct * WarsOf(w) / 100.0 + s.LawlessPct * (Lawless(w) - 0.2) / 100.0 * 5 + s.ScarcityPct * Scarcity(w) / 100.0;
        return Math.Clamp(o, 0.6, 1.5);
    }

    /// <summary>What confidence in the Reach is heading toward, and why.</summary>
    public (double target, List<string> why) ConfidenceTarget(World w)
    {
        var r = Rules; var why = new List<string>(); double c = 1; var m = Market(w);
        int wars = Math.Min(4, w.Wars.Count); if (wars > 0) { c += r.WarHomePct * wars / 100.0; why.Add(wars == 1 ? "a war between houses" : $"{wars} wars between houses"); }
        if (NationSystem.AtWar(w) is { } n) { c += r.WarAbroadPct / 100.0; why.Add($"the war with {Nations?.Def(n.Id)?.Name ?? "a nation beyond the Reach"}"); }
        int stage = w.Counter(GreatCrisisSystem.StageCounter) + 1;
        if (w.WorldCounters.ContainsKey(GreatCrisisSystem.StageCounter) && stage > 0 && !w.WorldFlags.Contains(GreatCrisisSystem.ResolvedFlag)) { c += r.CrisisPct * Math.Min(4, stage) / 100.0; why.Add("the crisis of the age"); }
        var houses = w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId).ToList();
        double debt = houses.Count == 0 ? 0 : houses.Count(h => h.Debt > 0 || h.Treasury.Credits < 0) / (double)houses.Count;
        if (debt > 0.2) { c += r.DebtPct * debt / 100.0; why.Add("houses in debt"); }
        if (m.PeaceYears > 0) { c += Math.Min(r.PeaceCapPct, r.PeacePct * m.PeaceYears) / 100.0; if (m.PeaceYears >= 3) why.Add($"{m.PeaceYears} years of peace"); }
        if (m.SteadiedTurn >= 0 && w.Turn - m.SteadiedTurn <= 1) { c += r.SteadyPct / 100.0; why.Add("the Treasurer's hand under the market"); }
        return (Math.Clamp(c, r.ConfidenceFloorPct / 100.0, r.ConfidenceCapPct / 100.0), why);
    }

    /// <summary>What a share ought to fetch on this year's facts, before flow.</summary>
    public double Fundamental(World w, Company co)
    {
        var r = Rules; double conf = Market(w).Confidence;
        double worth = Math.Max(0, Earnings(w, co)) * r.Multiple * (co.Chartered ? 1 : Outlook(w, co.Sector)) * conf + Math.Max(0, co.Cash);
        foreach (var p in w.Projects.Where(p => p.CompanyId == co.Id)) worth += Economy.WorthOf(w, p.Paid) / 2;
        return Math.Max(0.05, Math.Max(Book(w, co) * r.BookFloorPct / 100.0, worth) / Math.Max(1, co.Shares));
    }

    public static int StakePct(Company co, string holder) => co.Shares <= 0 ? 0 : co.HeldBy(holder) * 100 / co.Shares;
    public static int Value(Company co, string holder) => (int)Math.Round(co.HeldBy(holder) * co.Price);
    public static int Cap(Company co) => (int)Math.Round(co.Shares * co.Price);
    /// <summary>What a house's paper would fetch: every share it holds at the Exchange's price.</summary>
    public static int PaperOf(World w, string houseId) => w.Companies.Sum(co => Value(co, houseId));
    public static IEnumerable<Company> ControlledBy(World w, string houseId) => w.Companies.Where(c => c.ControllerId == houseId).OrderBy(c => c.Id, StringComparer.Ordinal);

    private int LevyPct(World w) => _trade.Policies.IsEmpty ? 0 : Math.Max(0, PolicySystem.ReachModifier(w, _trade.Policies, "exchange_levy_pct"));
    private static void PayLevy(World w, string payerId, int levy)
    {
        if (levy <= 0) return;
        var chair = RegionSystem.CapitalHolder(w); if (chair != null && chair.Id != payerId) chair.Treasury.Credits += levy;
    }

    // ---------------------------------------------------------------- founding

    private string NameFor(World w, House house, string sector)
    {
        string noun = _exchange.Sector(sector)?.Noun ?? "Company"; string stem = house.Name.Replace("House ", "");
        string name = $"The {stem} {noun}"; int n = 2;
        while (w.Companies.Any(c => c.Name == name)) name = $"The {stem} {noun} ({n++})";
        return name;
    }

    public string? IncorporateRefusal(World w, string houseId, Business? b, string companyId)
    {
        var house = w.House(houseId); if (!Open || house == null || b == null) return "no such venture";
        if (b.OwnerId != houseId) return "not your venture";
        if (b.CompanyId.Length > 0) return "already held by a company";
        if (_ventures.Def(b.Kind) == null) return "no such venture";
        if (companyId.Length > 0)
        {
            var co = w.Company(companyId); if (co == null || co.Chartered) return "no such company";
            return co.ControllerId == houseId ? null : "you do not direct that company";
        }
        if (w.Companies.Count(c => !c.Chartered) >= Rules.MaxCompanies) return "the Exchange lists as many companies as it will bear";
        if (_economy.VentureValue(w, b) <= 0) return "worth nothing";
        return house.Treasury.Influence >= Rules.IncorporateInfluence ? null : $"needs {Rules.IncorporateInfluence} influence for the charter";
    }

    /// <summary>A venture becomes a company: a hundred shares, all the founder's, priced at what the venture is worth.</summary>
    public Company Incorporate(World w, House house, Business b)
    {
        var def = _ventures.Def(b.Kind)!; int n = w.Counter(ListedCounter) + 1; w.WorldCounters[ListedCounter] = n;
        house.Treasury.Influence -= Rules.IncorporateInfluence;
        var co = new Company { Id = $"co_{n}", Name = NameFor(w, house, def.Sector), FounderId = house.Id, ControllerId = house.Id, Sector = def.Sector, Shares = Rules.Shares, Founded = w.Turn };
        co.Holders[house.Id] = co.Shares; co.Holders[Company.Public] = 0;
        w.Companies.Add(co); b.CompanyId = co.Id;
        co.LastEarnings = Earnings(w, co); co.Price = Fundamental(w, co); co.History.Add(co.Price); co.Cause = "newly listed";
        w.Log($"{house.Name} incorporates the {b.Name} as {co.Name}: {co.Shares} shares at {co.Price:0.00} credits.");
        return co;
    }

    /// <summary>A venture is folded into a company its owner directs: new shares are issued to the owner for what it is worth.</summary>
    public void Fold(World w, Company co, Business b)
    {
        int issued = Math.Max(1, (int)Math.Round(_economy.VentureValue(w, b) / Math.Max(0.05, co.Price)));
        b.CompanyId = co.Id; co.Shares += issued; co.Holders[b.OwnerId] = co.HeldBy(b.OwnerId) + issued;
        w.Log($"The {b.Name} is folded into {co.Name}; {issued} new shares go to {w.House(b.OwnerId)?.Name}.");
        Recontrol(w, co);
    }

    // ---------------------------------------------------------------- trading

    private bool Barred(World w, string houseId) => SenateSystem.Embargoed(w, houseId);

    public ShareQuote QuoteBuy(World w, string houseId, Company? co, int n)
    {
        var house = w.House(houseId); if (co == null || house == null) return new ShareQuote(0, 0, 0, "no such company");
        if (n <= 0) return new ShareQuote(0, 0, 0, "nothing ordered");
        if (Barred(w, houseId)) return new ShareQuote(0, 0, 0, "the Senate's embargo closes the Exchange to you");
        int floated = co.HeldBy(Company.Public);
        if (floated < n) return new ShareQuote(0, 0, 0, floated == 0 ? "no shares on the float: its holders are not selling" : $"only {floated} on the float");
        int cap = co.Shares * Rules.BuyCapPct / 100, already = co.BoughtThisTurn.GetValueOrDefault(houseId);
        if (already + n > cap) return new ShareQuote(0, 0, 0, $"no house may buy more than {cap} shares of it in a year{(already > 0 ? $" (you have bought {already})" : "")}");
        if (co.Chartered && co.MonopolyOf.Length == 0 && (co.HeldBy(houseId) + n) * 4 > co.Shares) return new ShareQuote(0, 0, 0, "its charter lets no house hold more than a quarter");
        if (co.ControllerId.Length > 0 && co.ControllerId != houseId && w.AtWar(houseId, co.ControllerId)) return new ShareQuote(0, 0, 0, "you are at war with the house that directs it");
        double cost = co.Price * (n + Rules.ImpactPct / 100.0 * (n * (double)already + n * n / 2.0) / Math.Max(1, co.Shares));
        int credits = Math.Max(1, (int)Math.Ceiling(cost - 1e-9)); int levy = (int)Math.Ceiling(credits * LevyPct(w) / 100.0);
        return new ShareQuote(credits + levy, levy, cost / n, house.Treasury.Credits < credits + levy ? $"needs {credits + levy} credits" : null);
    }

    public ShareQuote QuoteSell(World w, string houseId, Company? co, int n)
    {
        var house = w.House(houseId); if (co == null || house == null) return new ShareQuote(0, 0, 0, "no such company");
        if (n <= 0) return new ShareQuote(0, 0, 0, "nothing offered");
        if (Barred(w, houseId)) return new ShareQuote(0, 0, 0, "the Senate's embargo closes the Exchange to you");
        if (co.HeldBy(houseId) < n) return new ShareQuote(0, 0, 0, $"you hold only {co.HeldBy(houseId)}");
        double fetched = co.Price * Math.Max(0.3 * n, n - Rules.ImpactPct / 100.0 * (n * n / 2.0) / Math.Max(1, co.Shares)) * 0.98;
        int credits = Math.Max(0, (int)Math.Floor(fetched + 1e-9)); int levy = (int)Math.Ceiling(credits * LevyPct(w) / 100.0);
        return new ShareQuote(Math.Max(0, credits - levy), levy, fetched / n, null);
    }

    /// <summary>Would this order pass control from a founder who still has their one refusal?</summary>
    public bool GoldenShareStands(World w, Company co, string buyerId, int n)
    {
        if (co.Chartered || co.GoldenShareUsed || co.FounderId.Length == 0 || co.FounderId == buyerId || co.ControllerId != co.FounderId) return false;
        var founder = w.House(co.FounderId); if (founder == null || co.HeldBy(co.FounderId) == 0) return false;
        if ((co.HeldBy(buyerId) + n) * 2 <= co.Shares) return false;
        return founder.Treasury.Influence >= Rules.GoldenInfluence;
    }

    public bool Buy(World w, string houseId, Company co, int n)
    {
        var q = QuoteBuy(w, houseId, co, n); if (q.Refusal != null) return false; var house = w.House(houseId)!;
        if (GoldenShareStands(w, co, houseId, n))
        {
            // the founder's one refusal: the board turns the order away this year, and the founder pays for it in standing
            var founder = w.House(co.FounderId)!; co.GoldenShareUsed = true; founder.Treasury.Influence -= Rules.GoldenInfluence;
            var a = Economy.HeadOf(w, founder); var b = Economy.HeadOf(w, house); if (a != null && b != null) Sour(w, a.Id, b.Id, -15);
            co.BoughtThisTurn[houseId] = co.Shares;   // and no more of it this year
            if (founder.Id == w.Protagonist?.HouseId || houseId == w.Protagonist?.HouseId) Mark(w, GoldenFlag);
            w.Log($"{founder.Name} invokes its founder's share in {co.Name}: the board refuses {house.Name}'s order. It cannot do so twice.");
            return false;
        }
        house.Treasury.Credits -= q.Credits; PayLevy(w, houseId, q.Levy);
        co.Holders[Company.Public] = co.HeldBy(Company.Public) - n; co.Holders[houseId] = co.HeldBy(houseId) + n;
        co.BoughtThisTurn[houseId] = co.BoughtThisTurn.GetValueOrDefault(houseId) + n; co.Flow += n;
        w.WorldCounters[SharesTradedCounter] = w.Counter(SharesTradedCounter) + n;
        Recontrol(w, co);
        return true;
    }

    public bool Sell(World w, string houseId, Company co, int n)
    {
        var q = QuoteSell(w, houseId, co, n); if (q.Refusal != null) return false; var house = w.House(houseId)!;
        house.Treasury.Credits += q.Credits; PayLevy(w, houseId, q.Levy);
        co.Holders[houseId] = co.HeldBy(houseId) - n; if (co.Holders[houseId] <= 0) co.Holders.Remove(houseId);
        co.Holders[Company.Public] = co.HeldBy(Company.Public) + n; co.Flow -= n;
        w.WorldCounters[SharesTradedCounter] = w.Counter(SharesTradedCounter) + n;
        Recontrol(w, co);
        return true;
    }

    // ---------------------------------------------------------------- the board

    public string? FloatRefusal(World w, string houseId, Company? co, int n)
    {
        if (co == null || co.Chartered) return "no such company";
        if (co.ControllerId != houseId) return "you do not direct it";
        if (n <= 0) return "nothing offered";
        if (co.LastFloat == w.Turn) return "it has floated shares this year already";
        int max = Math.Max(1, co.Shares * Rules.FloatMaxPct / 100); if (n > max) return $"no more than {max} new shares a year";
        return co.Price < 0.1 ? "nobody would buy them" : null;
    }
    public int FloatProceeds(Company co, int n) => (int)Math.Floor(co.Price * n * (100 - Rules.FloatDiscountPct) / 100.0);

    /// <summary>New shares are sold to the public at a discount. The coin goes into the company, not its director's purse.</summary>
    public int Float(World w, Company co, int n)
    {
        int cash = FloatProceeds(co, n); co.Cash += cash; co.Shares += n; co.Holders[Company.Public] = co.HeldBy(Company.Public) + n; co.LastFloat = w.Turn; co.Flow -= n / 2.0;
        var by = w.House(co.ControllerId);
        if (co.ControllerId != w.Protagonist?.HouseId && co.HeldBy(Company.Public) == n) Mark(w, FloatedFlag);
        w.Log($"{co.Name} floats {n} new shares and raises {cash} credits{(by != null ? $"; {by.Name} now holds {StakePct(co, by.Id)}%" : "")}.");
        Recontrol(w, co);
        return cash;
    }

    public string? BuybackRefusal(World w, string houseId, Company? co, int n)
    {
        if (co == null || co.Chartered) return "no such company";
        if (co.ControllerId != houseId) return "you do not direct it";
        if (n <= 0 || co.HeldBy(Company.Public) < n) return co?.HeldBy(Company.Public) == 0 ? "no shares on the float" : $"only {co!.HeldBy(Company.Public)} on the float";
        int cost = BuybackCost(co, n);
        return co.Cash >= cost ? null : $"the company needs {cost} credits in hand (it has {co.Cash})";
    }
    public int BuybackCost(Company co, int n) => (int)Math.Ceiling(co.Price * (n + Rules.ImpactPct / 100.0 * n * n / 2.0 / Math.Max(1, co.Shares)));

    /// <summary>The company buys its own shares off the float with its own cash and cancels them: every holder's share grows.</summary>
    public void Buyback(World w, Company co, int n)
    {
        co.Cash -= BuybackCost(co, n); co.Holders[Company.Public] = co.HeldBy(Company.Public) - n; co.Shares -= n; co.Flow += n / 2.0;
        w.Log($"{co.Name} buys back {n} of its shares and cancels them.");
        Recontrol(w, co);
    }

    public const int Reserve = 10;
    public string? SpecialRefusal(World w, string houseId, Company? co)
    {
        if (co == null || co.Chartered) return "no such company";
        if (co.ControllerId != houseId) return "you do not direct it";
        return co.Cash > Reserve ? null : "it has no cash to spare";
    }

    /// <summary>The cash the company is sitting on, above a small reserve, is paid out to every holder by their shares, at once.</summary>
    public void SpecialDividend(World w, Company co)
    {
        int pot = co.Cash - Reserve; if (pot <= 0) return; co.Cash -= pot;
        foreach (var kv in co.Holders.Where(kv => kv.Key != Company.Public).OrderBy(kv => kv.Key, StringComparer.Ordinal)) if (w.House(kv.Key) is { } h) h.Treasury.Credits += pot * kv.Value / Math.Max(1, co.Shares);
        w.Log($"{co.Name} pays a special dividend of {pot} credits.");
    }

    public string? PrivateRefusal(World w, string houseId, Company? co)
    {
        if (co == null || co.Chartered) return "no such company";
        if (co.ControllerId != houseId) return "you do not direct it";
        return co.HeldBy(houseId) >= co.Shares ? null : "you must hold every share first";
    }

    /// <summary>Its sole holder winds the company up: the ventures and the cash are the house's again.</summary>
    public void TakePrivate(World w, Company co)
    {
        var house = w.House(co.ControllerId); if (house == null) return;
        foreach (var b in VenturesOf(w, co).ToList()) { b.CompanyId = ""; b.OwnerId = house.Id; }
        foreach (var p in w.Projects.Where(p => p.CompanyId == co.Id)) { p.CompanyId = ""; p.OwnerId = house.Id; }
        house.Treasury.Credits += Math.Max(0, co.Cash); w.Companies.Remove(co);
        w.Log($"{house.Name} winds up {co.Name} and takes its ventures back in hand.");
    }

    /// <summary>Who directs it: the holder of more than half; failing that the largest holding house (the sitting director on a tie).</summary>
    public void Recontrol(World w, Company co)
    {
        if (co.Chartered) { co.ControllerId = co.MonopolyOf; return; }
        var houses = co.Holders.Where(kv => kv.Key != Company.Public && kv.Value > 0 && w.House(kv.Key) != null).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key == co.ControllerId ? 0 : 1).ThenBy(kv => kv.Key, StringComparer.Ordinal).ToList();
        if (houses.Count == 0) return;
        string now = houses.FirstOrDefault(kv => kv.Value * 2 > co.Shares).Key ?? (co.HeldBy(co.ControllerId) * 2 > co.Shares || (co.HeldBy(co.ControllerId) > 0 && co.HeldBy(co.ControllerId) >= houses[0].Value) ? co.ControllerId : houses[0].Key);
        if (now == co.ControllerId) return;
        string was = co.ControllerId; co.ControllerId = now;
        foreach (var b in VenturesOf(w, co)) b.OwnerId = now;
        foreach (var p in w.Projects.Where(p => p.CompanyId == co.Id)) p.OwnerId = now;
        var mine = w.Protagonist?.HouseId;
        if (was == mine && mine != null) Mark(w, TakenFromMeFlag); else if (now == mine && mine != null) Mark(w, TakenByMeFlag);
        w.WorldCounters[TakeoversCounter] = w.Counter(TakeoversCounter) + 1;
        var a = w.House(now); var old = w.House(was);
        if (a != null && old != null) { var ha = Economy.HeadOf(w, a); var ho = Economy.HeadOf(w, old); if (ha != null && ho != null) Sour(w, ho.Id, ha.Id, -20); }
        w.Log($"{a?.Name ?? "A new board"} takes control of {co.Name}{(old != null ? $" from {old.Name}" : "")} with {StakePct(co, now)}% of its shares.");
    }

    /// <summary>The company is wound up: its ventures are broken up on the open market and what is left is shared out by holding.</summary>
    public void Liquidate(World w, Company co, string why)
    {
        int pot = co.Cash;
        foreach (var b in VenturesOf(w, co).ToList()) { pot += Math.Max((_ventures.Def(b.Kind)?.Value(b.Level) ?? 0) * Economy.SalvagePct / 100, _economy.VentureValue(w, b) * VentureSystem.MarketPct / 100); w.Businesses.Remove(b.Id); }
        foreach (var p in w.Projects.Where(p => p.CompanyId == co.Id).ToList()) { pot += (int)(Economy.WorthOf(w, p.Paid) / 2); w.Projects.Remove(p); }
        if (co.Chartered) pot = Math.Max(pot, (int)(Cap(co) * 0.6));
        if (pot > 0) foreach (var kv in co.Holders.Where(kv => kv.Key != Company.Public).OrderBy(kv => kv.Key, StringComparer.Ordinal)) if (w.House(kv.Key) is { } h) h.Treasury.Credits += pot * kv.Value / Math.Max(1, co.Shares);
        if (w.Protagonist is { } pc && co.HeldBy(pc.HouseId) > 0) w.WorldFlags.Add(FailedFlag);
        w.Companies.Remove(co);
        w.Log($"{co.Name} is wound up: {why}.{(pot > 0 ? $" Its holders share {pot} credits." : " Its holders get nothing.")}");
    }

    // ---------------------------------------------------------------- the yearly reckoning

    /// <summary>The great companies of the Reach are listed when the Exchange opens: every share on the float.</summary>
    public void Seed(World w)
    {
        if (!Open || w.WorldFlags.Contains("exchange_open")) return;
        w.WorldFlags.Add("exchange_open");
        foreach (var def in _exchange.Chartered)
        {
            if (w.Company(def.Id) != null) continue;
            var co = new Company { Id = def.Id, CharterId = def.Id, Name = def.NameIn(w.IsImperium), Chartered = true, CharterIncome = def.Income, Sector = def.Sector, Shares = Rules.Shares, Founded = w.Turn, PayoutPct = 75 };
            co.Holders[Company.Public] = co.Shares; w.Companies.Add(co);
            co.LastEarnings = Earnings(w, co); co.Price = Fundamental(w, co); co.History.Add(co.Price); co.Cause = "chartered by the Senate";
        }
    }

    public void Tick(World w)
    {
        if (!Open) return; Seed(w);
        var m = Market(w); var r = Rules; var mine = w.Protagonist?.HouseId; m.LastExchangeTick = w.Turn;
        foreach (var f in TickFlags) w.WorldFlags.Remove(f);
        foreach (var f in DeedFlags) if (w.WorldFlags.Contains(f) && w.Counter("mark:" + f) < w.Turn - 1) { w.WorldFlags.Remove(f); w.WorldCounters.Remove("mark:" + f); }
        m.PeaceYears = WarsOf(w) == 0 ? m.PeaceYears + 1 : 0;
        var (target, why) = ConfidenceTarget(w); double confWas = m.Confidence;
        m.Confidence += (target - m.Confidence) * r.ConfidenceEasePct / 100.0;
        m.Movers.Clear();
        string moved = m.Confidence > confWas + 0.005 ? "rises to" : m.Confidence < confWas - 0.005 ? "falls to" : "holds at";
        m.Movers.Add($"Confidence {moved} {m.Confidence * 100:0}{(why.Count > 0 ? ": " + string.Join(", ", why) : "")}.");

        double capBefore = 0, capAfter = 0; (Company? co, double pct) best = (null, 0), worst = (null, 0);
        foreach (var co in w.Companies.OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
        {
            // shares of a house that is no more go to the float
            foreach (var gone in co.Holders.Keys.Where(k => k != Company.Public && w.House(k) == null).ToList()) { co.Holders[Company.Public] = co.HeldBy(Company.Public) + co.Holders[gone]; co.Holders.Remove(gone); }
            if (!co.Chartered && co.Holders.All(kv => kv.Key == Company.Public || kv.Value <= 0)) { Liquidate(w, co, "no house holds a share of it"); continue; }
            Recontrol(w, co);
            int earnings = Earnings(w, co), earnedWas = co.LastEarnings; co.LastEarnings = earnings;
            // a monopoly's grantee takes its rent off the top
            if (co.Chartered && co.MonopolyOf.Length > 0 && earnings > 0 && w.House(co.MonopolyOf) is { } grantee) { int rent = earnings * r.MonopolyRentPct / 100; grantee.Treasury.Credits += rent; earnings -= rent; }
            co.Cash += earnings;
            int paidWas = co.LastDividend; int pay = earnings > 0 && co.Cash > 0 ? Math.Min(co.Cash, earnings * co.PayoutPct / 100) : 0;
            co.Cash -= pay; co.LastDividend = pay;   // declared now; paid to whoever holds the shares at the next reckoning of the ledgers
            if (co.Chartered) co.Cash = Math.Clamp(co.Cash, 0, co.CharterIncome / 2);   // a great company keeps a reserve and no more
            if (pay == 0 && paidWas > 0 && mine != null && co.HeldBy(mine) > 0) w.WorldFlags.Add(DividendCutFlag);
            if (!co.Chartered && co.Cash <= r.FailCash) { Liquidate(w, co, "it could not pay its way"); continue; }

            double before = co.Price; capBefore += before * co.Shares;
            double outlook = co.Chartered ? 1 : Outlook(w, co.Sector), fundamental = Fundamental(w, co);
            double flow = r.FlowPct / 100.0 * co.Flow / Math.Max(1, co.Shares);
            double wobble = ((Hash(co.Id + ":" + w.Turn) % 2001) / 1000.0 - 1) * r.WobblePct / 100.0;
            double want = Math.Max(0.05, fundamental * (1 + Math.Clamp(flow, -0.4, 0.4)) * (1 + wobble));
            co.Price = Math.Max(0.05, co.Price + (want - co.Price) * r.EasePct / 100.0);
            co.Flow *= 1 - r.FlowDecayPct / 100.0; if (Math.Abs(co.Flow) < 0.5) co.Flow = 0;
            co.BoughtThisTurn.Clear();
            co.History.Add(co.Price); while (co.History.Count > r.HistoryKept) co.History.RemoveAt(0);
            capAfter += co.Price * co.Shares;
            double pct = before > 0 ? (co.Price / before - 1) * 100 : 0;
            co.Cause = CauseOf(w, co, pct, flow, outlook, earnings, earnedWas, m.Confidence - confWas);
            if (best.co == null || pct > best.pct) best = (co, pct); if (worst.co == null || pct < worst.pct) worst = (co, pct);
            // a rival with a tenth of a company you direct is public knowledge
            if (mine != null && co.ControllerId == mine && co.Holders.Any(kv => kv.Key != mine && kv.Key != Company.Public && kv.Value * 100 >= co.Shares * r.PublicStakePct)) w.WorldFlags.Add(StakeFlag);
        }
        double indexWas = m.Index;
        if (capBefore > 0) m.Index = Math.Max(1, m.Index * capAfter / capBefore);
        m.IndexHistory.Add(m.Index); while (m.IndexHistory.Count > r.HistoryKept) m.IndexHistory.RemoveAt(0);
        double move = indexWas > 0 ? (m.Index / indexWas - 1) * 100 : 0;
        if (move <= -r.CrashPct) { w.WorldFlags.Add(CrashFlag); w.Log($"The Exchange falls {-move:0}% in a year. Houses that must sell, sell into it."); }
        else if (move >= r.CrashPct) { w.WorldFlags.Add(BoomFlag); w.Log($"The Exchange rises {move:0}% in a year."); }
        if (best.co != null && best.pct >= 3) m.Movers.Add($"{best.co.Name} up {best.pct:0}%: {best.co.Cause}.");
        if (worst.co != null && worst.co != best.co && worst.pct <= -3) m.Movers.Add($"{worst.co.Name} down {-worst.pct:0}%: {worst.co.Cause}.");
        foreach (var s in _exchange.Sectors) { double o = Outlook(w, s.Id); if (Math.Abs(o - 1) >= 0.08) m.Movers.Add($"{s.Name} {(o > 1 ? "is favoured" : "is out of favour")} ({(o - 1) * 100:+0;-0}%): {SectorWhy(w, s, o > 1)}."); }
    }

    private string SectorWhy(World w, SectorDef s, bool up)
    {
        int wars = WarsOf(w); double war = s.WarPct * wars, law = s.LawlessPct * (Lawless(w) - 0.2) * 5, scar = s.ScarcityPct * Scarcity(w);
        double top = new[] { war, law, scar }.OrderByDescending(x => up ? x : -x).First();
        if (top == war && wars > 0) return up ? "war fills its order books" : "war closes its routes";
        if (top == law) return up ? "the lanes are quieter" : "raiders on the lanes";
        return up ? "goods are dear" : "goods are cheap";
    }

    private static string CauseOf(World w, Company co, double pct, double flow, double outlook, int earnings, int earnedWas, double confMove)
    {
        if (Math.Abs(pct) < 2) return "steady";
        bool up = pct > 0; var reasons = new List<(double weight, string text)>
        {
            (flow * (up ? 1 : -1), up ? "heavy buying" : "heavy selling"),
            ((outlook - 1) * (up ? 1 : -1), up ? "its trade is in favour" : "its trade is out of favour"),
            (confMove * 2 * (up ? 1 : -1), up ? "confidence is returning" : "confidence is failing"),
            (earnedWas != 0 ? (earnings - earnedWas) / (double)Math.Max(1, Math.Abs(earnedWas)) * (up ? 1 : -1) : 0, up ? "it is earning more" : "it is earning less")
        };
        var top = reasons.OrderByDescending(x => x.weight).First();
        return top.weight > 0.01 ? top.text : up ? "the price is catching up with what it earns" : "the price is coming back to what it earns";
    }
}
