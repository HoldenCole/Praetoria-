using System.Runtime.CompilerServices;
using Praetoria.Core.Data;
using Praetoria.Core.Diagnostics;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The Senate's law, enlarged (docs/SENATE.md, "The Senate's law, enlarged"): ten more Acts, and the rules each of them changes.
/// An Act is a law of scope <c>senate</c> in content/politics/laws.json, in force for the whole Reach, and its effects are read
/// where the law puts them, through <see cref="PolicySystem.ReachModifier"/>. Some of the rules it touches are static and are
/// handed no catalogue of laws (the ladder, the lenders, the hired companies, the reckoning of a corner): they read the Reach's
/// law through <see cref="Reach"/>, which uses the catalogue the world's controller gave with <see cref="Use"/>, as the benches do
/// (<see cref="FactionSystem.Use"/>). A world that no controller has claimed has no Acts in force, and every rule is as it was.
/// <para>The keys new with these Acts, and the rule that reads each:</para>
/// <list type="bullet">
/// <item><c>loan_rate_cap</c>: <see cref="Lending.Lenders"/>, <see cref="DealingsSystem.LendRefusal"/>, the stewards' standing loan.</item>
/// <item><c>canvass_law</c>: <see cref="LadderSystem.CampaignRefusal"/>, and what a canvass writes on the record.</item>
/// <item><c>corner_premium_cap</c>, <c>corner_forced_sale</c>: <see cref="CornerSystem.Reckon"/>.</item>
/// <item><c>company_licence</c>, <c>company_ban</c>: <see cref="MercenarySystem"/>.</item>
/// <item><c>house_trade_levy_pct</c>, <c>house_trade_ban</c>: <see cref="HouseTradeSystem"/>.</item>
/// <item><c>office_age</c>: <see cref="LadderSystem.Bar"/> and <see cref="OfficeSystem.Candidate"/>.</item>
/// <item><c>region_allegiance</c>: <see cref="RegionSystem.AllegianceTarget"/>.</item>
/// <item>and, read for the whole Reach for the first time: <c>credits_per_world</c>, <c>pop_growth</c>, <c>prosperity_drift</c>
/// (<see cref="Economy"/>), <c>fleet_upkeep_pct</c>, <c>readiness</c> (<see cref="FleetSystem"/>).</item>
/// </list>
/// <para>Three more things live here. <see cref="Interest"/> is what an Act does to one house, as that house reckons it (a lender
/// against a cap on usury, a house with a fleet out on hire against the ban), and is part of how every house leans on an Act
/// (<see cref="SenateSystem.ActLean"/>). <see cref="ToHouse"/> says the same to the player in figures. <see cref="Legislate"/> is the
/// chamber's own business: each year, when nothing else is before it, a house hurt by a law in force moves its repeal, or a bench
/// moves an Act of its platform that it counts the votes to carry.</para>
/// </summary>
public static class SenateActs
{
    public const string LoanRateCap = "loan_rate_cap", CanvassLaw = "canvass_law", CornerPremiumCap = "corner_premium_cap", CornerForcedSale = "corner_forced_sale",
        CompanyLicence = "company_licence", CompanyBan = "company_ban", TradeLevyPct = "house_trade_levy_pct", TradeBan = "house_trade_ban", OfficeAge = "office_age", RegionAllegiance = "region_allegiance";
    /// <summary>Every effect these Acts brought a rule to read for the whole Reach (tests/LawsAuditTests.cs refuses a law of the Reach that names any other).</summary>
    public static readonly string[] ReadForTheReach = { LoanRateCap, CanvassLaw, CornerPremiumCap, CornerForcedSale, CompanyLicence, CompanyBan, TradeLevyPct, TradeBan, OfficeAge, RegionAllegiance,
        "credits_per_world", "pop_growth", "prosperity_drift", "fleet_upkeep_pct", "readiness" };

#pragma warning disable CA2255   // the list must be whole before any test or tool asks for it, whichever class it touches first
    [ModuleInitializer] internal static void Register() { lock (ReachRead.Also) foreach (var k in ReadForTheReach) ReachRead.Also.Add(k); }
#pragma warning restore CA2255

    /// <summary>What a company hired out under licence costs the house in standing: it is the Reach's business then, and no scandal.</summary>
    public const int LicensedLegitimacy = 1;
    /// <summary>The youngest any office may be stood for, whatever the law of the years says.</summary>
    public const int YoungestOffice = 18;

    private sealed record Catalogues(PolicyCatalog Policies, BusinessCatalog Ventures);
    private static readonly ConditionalWeakTable<World, Catalogues> ByWorld = new();
    /// <summary>This world's laws, and its ventures (which of them lend), given by its controller.</summary>
    public static void Use(World w, PolicyCatalog policies, BusinessCatalog? ventures = null) => ByWorld.AddOrUpdate(w, new Catalogues(policies, ventures ?? BusinessCatalog.Empty));
    public static PolicyCatalog Of(World w) => ByWorld.TryGetValue(w, out var p) ? p.Policies : PolicyCatalog.Empty;
    private static BusinessCatalog VenturesOf(World w) => ByWorld.TryGetValue(w, out var p) ? p.Ventures : BusinessCatalog.Empty;

    /// <summary>What the laws of the whole Reach say of an effect, as the world stands.</summary>
    public static int Reach(World w, string key)
    {
        var cat = Of(w); if (cat.IsEmpty) return 0;
        return ReadCache.Active ? ReadCache.Get("act:" + key, () => PolicySystem.ReachModifier(w, cat, key)) : PolicySystem.ReachModifier(w, cat, key);
    }

    /// <summary>The law of the Reach that is doing this, by name and in the middle of a sentence: "the Usury Law".</summary>
    public static string LawName(World w, string key)
    {
        var cat = Of(w);
        foreach (var law in cat.Laws.Where(l => l.Reach))
            if ((PolicySystem.ReachOption(w, cat, law)?.Effects.GetValueOrDefault(key) ?? 0) != 0) return InASentence(law.Name);
        return "the law of the Reach";
    }
    public static string InASentence(string name) => name.StartsWith("The ", StringComparison.Ordinal) ? "the " + name[4..] : name;

    // ---------------------------------------------------------------- usury

    /// <summary>The most a lender may charge, in the hundred; 0 where contract is free.</summary>
    public static int LoanCap(World w) => Math.Max(0, Reach(w, LoanRateCap));
    /// <summary>A lender's terms under the cap: the rate cut to it, and the limit cut in the same proportion (a lender who may charge
    /// two thirds of what it would lends two thirds of what it would), in whole steps and never less than one.</summary>
    public static List<LoanTerms> CapLoans(World w, List<LoanTerms> terms)
    {
        int cap = LoanCap(w); if (cap <= 0) return terms;
        string law = LawName(w, LoanRateCap);
        return terms.Select(t =>
        {
            if (t.RatePct <= cap) return t;
            int limit = Math.Max(Lending.Step, t.Limit * cap / Math.Max(1, t.RatePct) / Lending.Step * Lending.Step);
            return t with { RatePct = cap, Limit = limit, Why = t.Why + $" It would ask {t.RatePct} in the hundred, and {law} allows no more than {cap}: it lends {Money.Show(limit)} at the most, where it would have lent {Money.Show(t.Limit)}." };
        }).ToList();
    }
    /// <summary>The stewards' standing loan as the law leaves it: what is lent and what is owed for it.</summary>
    public static (int lent, int owed) StandingLoan(World w, FinanceRules f)
    {
        int cap = LoanCap(w), rate = Lending.BaseRate(f); if (cap <= 0 || rate <= cap) return (f.LoanPrincipal, f.LoanRepay);
        int lent = Math.Max(Lending.Step, f.LoanPrincipal * cap / Math.Max(1, rate) / Lending.Step * Lending.Step);
        return (lent, Lending.Owed(lent, cap));
    }
    /// <summary>Said after a lender's refusal to lend more, where the cap is why it lends less: ": the Usury Law allows it no more
    /// than 20 in the hundred, and it lends the less for that". Empty where the law did not cut these terms.</summary>
    public static string CapNote(World w, LoanTerms t) =>
        LoanCap(w) is > 0 and var cap && t.RatePct == cap && t.Why.Contains(" allows no more than ", StringComparison.Ordinal) ? $": {LawName(w, LoanRateCap)} allows it no more than {cap} in the hundred, and it lends the less for that" : "";
    /// <summary>Why a loan between houses at this rate is against the law, or null.</summary>
    public static string? LendRefusal(World w, int ratePct) =>
        LoanCap(w) is > 0 and var cap && ratePct > cap ? $"{LawName(w, LoanRateCap)} allows no more than {cap} in the hundred, and this is {ratePct}" : null;

    // ---------------------------------------------------------------- canvassing

    /// <summary>Above zero the strict law is in force, below it money is free, at zero the custom stands.</summary>
    public static int Canvass(World w) => Math.Sign(Reach(w, CanvassLaw));
    /// <summary>Is a canvass of this size forbidden outright?</summary>
    public static bool CanvassForbidden(World w, string size) => size == LadderSystem.Lavish && Canvass(w) > 0;
    /// <summary>Is a canvass of this size written on the record as votes bought, to be charged by whoever cares to?</summary>
    public static bool CanvassRecorded(World w, string size)
    {
        int law = Canvass(w);
        return law < 0 ? false : law > 0 ? size is LadderSystem.Handsome or LadderSystem.Lavish : size == LadderSystem.Lavish;
    }

    // ---------------------------------------------------------------- forestalling

    /// <summary>The most a corner may add to a price, as a share of it; none where the market is free.</summary>
    public static double CornerCap(World w) => Reach(w, CornerPremiumCap) is > 0 and var pct ? pct / 100.0 : double.MaxValue;
    public static bool CornersBroken(World w) => Reach(w, CornerForcedSale) > 0;
    /// <summary>The most a house may keep of a good under the law against engrossing: one short of what the price would feel.</summary>
    public static int MayKeep(int all) => Math.Max(CornerSystem.LeastUnits - 1, (all - 1) / 3);

    /// <summary>The law against engrossing, at the year's reckoning of a good: a house that holds half of all there is of it is made
    /// to sell down to what the price does not feel, to the merchants and at what the good is ordinarily worth (not at the price
    /// its own corner has made). True if a house was made to sell.</summary>
    public static bool BreakCorner(World w, MarketCatalog cat, GoodDef d, GoodState g)
    {
        if (!CornersBroken(w)) return false;
        var s = CornerSystem.Standing(w, cat, d.Id); var top = s.Top; if (top == null || !CornerSystem.Cornered(top.Units, s.All)) return false;
        var house = w.House(top.HouseId); if (house == null) return false;
        int keep = Math.Min(top.Units, MayKeep(s.All)), sold = top.Units - keep; if (sold <= 0) return false;
        int paid = Math.Max(0, (int)Math.Floor(Math.Min(g.Price, d.Base) * sold + 1e-9)); string label = Resource.Label(d.Id), law = LawName(w, CornerForcedSale);
        house.Treasury.Add(d.Id, -sold); house.Treasury.Credits += paid; g.Stock += sold;
        w.WorldCounters["corners_broken_by_law"] = w.Counter("corners_broken_by_law") + 1;
        w.Log($"Under {law}, {house.Name} is made to sell {sold} of the {top.Units} {label} it holds, half or more of all there is in the Reach. The merchants pay {Money.Credits(paid)} for it.");
        if (house.Id == w.Protagonist?.HouseId)
            w.Tell("warn", $"Under {law} your house is made to sell {sold} of its {top.Units} {label}: it held half or more of the {s.All} in the Reach, and may keep {keep}. The merchants pay {Money.Credits(paid)}, what it is ordinarily worth.", "exchange:goods", d.Id);
        return true;
    }

    // ---------------------------------------------------------------- hired companies

    public static bool CompaniesForbidden(World w) => Reach(w, CompanyBan) > 0;
    /// <summary>What the chair is paid for each company hired out, in the books' hundreds; 0 where none is asked.</summary>
    public static int CompanyFee(World w) => CompaniesForbidden(w) ? 0 : Math.Max(0, Reach(w, CompanyLicence));

    // ---------------------------------------------------------------- trade between houses

    public static bool StapleInForce(World w) => Reach(w, TradeBan) > 0;
    public static int TradeLevy(World w) => StapleInForce(w) ? 0 : Math.Clamp(Reach(w, TradeLevyPct), 0, 90);
    /// <summary>What the chair takes of a sale between two houses, out of what the seller is paid: never less than a hundred credits
    /// of a sale that is worth two hundred, and nothing where the chair's own house is the seller or no house holds the capital.</summary>
    public static int LevyOn(World w, House seller, int credits)
    {
        int pct = TradeLevy(w); if (pct <= 0 || credits < 2) return 0;
        var chair = RegionSystem.CapitalHolder(w); if (chair == null || chair.Id == seller.Id) return 0;
        return Math.Clamp((credits * pct + 99) / 100, 1, credits - 1);
    }

    // ---------------------------------------------------------------- the years

    /// <summary>The age an office asks under the law of the years. An office that asks no age asks none still.</summary>
    public static int AgeFor(World w, int minAge) => minAge <= 0 ? minAge : Math.Max(YoungestOffice, minAge + Reach(w, OfficeAge));

    // ---------------------------------------------------------------- the wording of an effect

    /// <summary>What one effect of an Act does, in a plain phrase; null for an effect that is said well enough as a number and a noun.</summary>
    public static string? Words(string key, int v) => key switch
    {
        LoanRateCap => $"no loan at more than {v} in the hundred; a lender made to charge less lends less in proportion",
        CanvassLaw => v > 0 ? "a lavish canvass is forbidden, and a handsome one is written on the record as votes bought" : "no canvass is written on the record, however lavish",
        CornerPremiumCap => $"a cornered good's price may stand no more than {v}% higher for the corner",
        CornerForcedSale => "a house that holds half of all there is of a good is made to sell down to under a third, at what the good is ordinarily worth",
        CompanyLicence => $"a house that hires a fleet out pays the chair {Money.Credits(v)} for the licence, and loses {LicensedLegitimacy} legitimacy for it where it would lose {MercenarySystem.Legitimacy}",
        CompanyBan => "no fleet may be hired out, and the companies out on hire come home",
        TradeLevyPct => $"{v} in the hundred of every lot one house sells another goes to the chair, out of what the seller is paid",
        TradeBan => "no house sells another a lot out of its store, and no new standing deal is struck: goods are bought and sold on the exchanges",
        OfficeAge => $"every office of the ladder, and the consulship, may be stood for {Counted.Years(Math.Abs(v))} {(v < 0 ? "younger" : "older")}",
        RegionAllegiance => $"every region's allegiance settles {Math.Abs(v)} {(v > 0 ? "higher" : "lower")}",
        "fleet_upkeep_pct" => null,
        _ => null
    };

    // ---------------------------------------------------------------- what an Act does to one house

    private static int D(LawOptionDef cur, LawOptionDef next, string key) => next.Effects.GetValueOrDefault(key) - cur.Effects.GetValueOrDefault(key);
    /// <summary>How far each kind of law goes, from nothing (0) to the most it does (2): a cap on usury of ten is tighter than one of
    /// twenty; a ban goes further than a licence or a levy.</summary>
    private static int UsuryTight(LawOptionDef o) => o.Effects.GetValueOrDefault(LoanRateCap) is var c && c <= 0 ? 0 : c <= 10 ? 2 : 1;
    private static int CornerTight(LawOptionDef o) => o.Effects.GetValueOrDefault(CornerForcedSale) > 0 ? 2 : o.Effects.GetValueOrDefault(CornerPremiumCap) > 0 ? 1 : 0;
    private static int CompanyTight(LawOptionDef o) => o.Effects.GetValueOrDefault(CompanyBan) > 0 ? 2 : o.Effects.GetValueOrDefault(CompanyLicence) > 0 ? 1 : 0;
    private static int TradeTight(LawOptionDef o) => o.Effects.GetValueOrDefault(TradeBan) > 0 ? 2 : o.Effects.GetValueOrDefault(TradeLevyPct) > 0 ? 1 : 0;

    /// <summary>Does the house lend: a bank of its own, or another house in its debt?</summary>
    public static bool Lends(World w, House h)
    {
        var v = VenturesOf(w);
        return w.Businesses.Values.Any(b => b.OwnerId == h.Id && v.Def(b.Kind)?.Lender == true) || w.HouseLoans.Any(l => l.LenderId == h.Id && l.Owed > 0)
            || w.Houses.Values.Any(x => x.Id != h.Id && x.LenderId == h.Id && x.Debt > 0);
    }
    /// <summary>Is the house in debt, or short enough of coin to want a loan?</summary>
    public static bool Borrows(World w, House h) => h.Debt > 0 || w.HouseLoans.Any(l => l.BorrowerId == h.Id && l.Owed > 0) || h.Treasury.Credits < DealingsSystem.ShortAt;
    /// <summary>Has the house sold to another house, or bought of one, these three years, or does it keep a standing deal?</summary>
    public static bool TradesWithHouses(World w, House h) =>
        w.TradeDeals.Any(d => d.SellerId == h.Id || d.BuyerId == h.Id) || w.Dealings.Any(d => d.Kind == HouseTradeSystem.BoughtKind && w.Turn - d.Turn <= 3 && (d.FromHouseId == h.Id || d.ToHouseId == h.Id));
    private static int Hulls(World w, House h) => w.Fleets.Values.Where(f => f.OwnerId == h.Id && !f.Office && f.HiredFrom.Length == 0).Sum(f => f.Strength);
    /// <summary>The lowest rung of the ladder a person is working toward: one not yet held, whose rank and rung below they have.</summary>
    private static RungDef? Toward(OfficeCatalog offices, Character c) =>
        c.CareerTrack.Length == 0 ? null : offices.Rungs.FirstOrDefault(r => !c.Cursus.Contains(r.Id) && c.CareerRank >= r.MinRank && (r.Requires.Length == 0 || c.Cursus.Contains(r.Requires)));
    private static int AgeUnder(LawOptionDef o, int minAge) => minAge <= 0 ? minAge : Math.Max(YoungestOffice, minAge + o.Effects.GetValueOrDefault(OfficeAge));
    /// <summary>Those of the house whom the change in the years would let stand sooner (+1 each), or make wait (-1 each).</summary>
    private static List<(Character who, RungDef rung, int now, int then)> Years(World w, House h, LawOptionDef cur, LawOptionDef next, OfficeCatalog offices)
    {
        var list = new List<(Character, RungDef, int, int)>(); if (D(cur, next, OfficeAge) == 0 || offices.Rungs.Count == 0) return list;
        foreach (var c in w.LivingCharacters().Where(c => c.HouseId == h.Id).OrderBy(c => c.Id, StringComparer.Ordinal))
            if (Toward(offices, c) is { } r && AgeUnder(cur, r.MinAge) is var now && AgeUnder(next, r.MinAge) is var then && (c.Age >= now) != (c.Age >= then)) list.Add((c, r, now, then));
        return list;
    }

    /// <summary>How a house leans on an Act for what it does to that house, besides what its estates and its bench make of it
    /// (<see cref="SenateSystem.ActLean"/> adds this): positive for, negative against, each cause written into <paramref name="why"/>.
    /// A lender is against a cap on usury and a house in debt for it; a house with the coin to win an election against the strict law
    /// on canvassing and an upright or a poor one for it; whoever holds a corner against the law on forestalling, and while a corner
    /// stands, the houses that must buy from it for; a house with a fleet out on hire, or one in its pay, against the ban, and a house
    /// at war with one that has hired companies for it; a house that trades with other houses against a levy or the staple, and the
    /// chair for the levy it would take; a house pays the Corn Law on every world, and one with restless worlds wants it; rich worlds
    /// want the great estates and worlds on the rim want settlers; a house at war wants long service and one that keeps a large fleet
    /// at peace does not want to pay for it; a house with someone of an age the law of the years moves; the chair, and houses whose
    /// worlds lie where allegiance runs thin, for a wider citizenship, and a house that has left the capital's order against it.</summary>
    public static int Interest(World w, House h, LawOptionDef cur, LawOptionDef next, OfficeCatalog offices, List<(string why, int by)>? why = null)
    {
        int lean = 0; void Add(int by, string because) { if (by == 0) return; lean += by; why?.Add((because, by)); }
        bool chair = RegionSystem.CapitalHolder(w)?.Id == h.Id;
        int u = UsuryTight(next) - UsuryTight(cur);
        if (u != 0)
        {
            if (Lends(w, h)) Add(-Math.Sign(u) * (Math.Abs(u) + 1), u > 0 ? "it lends, and a cap on usury cuts what it earns" : "it lends, and would charge what it likes");
            if (Borrows(w, h)) Add(Math.Sign(u) * Math.Abs(u), u > 0 ? "it borrows, and a cap makes money cheaper" : "it borrows, and the cap keeps its money cheap");
        }
        int c = Math.Sign(next.Effects.GetValueOrDefault(CanvassLaw)) - Math.Sign(cur.Effects.GetValueOrDefault(CanvassLaw));
        if (c != 0)
        {
            var head = Economy.HeadOf(w, h);
            if (h.Treasury.Credits >= LadderSystem.CampaignCost(LadderSystem.Handsome) * LadderSystem.OthersCampaignAfford) Add(-Math.Clamp(2 * c, -3, 3), "it has the coin to win an election");
            else if (h.Treasury.Credits < LadderSystem.CampaignCost(LadderSystem.Modest) * LadderSystem.OthersCampaignAfford) Add(c, "it cannot outspend anyone at an election");
            if (head != null && (head.NatureTraits.Contains("Honorable") || head.NatureTraits.Contains("Just"))) Add(c, "its head thinks an office should not be bought");
        }
        int k = CornerTight(next) - CornerTight(cur);
        if (k != 0 && w.Market != null)
        {
            if (w.Market.Goods.Values.Any(g => g.CornerHouse == h.Id && g.CornerPremium > 0)) Add(-Math.Sign(k) * (Math.Abs(k) + 1), "it holds a third or more of a good, and the law would take what that earns");
            else if (Hulls(w, h) > 0 && w.Market.Goods.Values.Any(g => g.CornerPremium > 0 && g.CornerHouse.Length > 0)) Add(Math.Sign(k), "a corner stands, and it pays the price of it");
        }
        int m = CompanyTight(next) - CompanyTight(cur);
        if (m != 0)
        {
            if (MercenarySystem.HiredOutBy(w, h.Id).Any()) Add(-Math.Sign(m) * (Math.Abs(m) + 1), "it has a fleet out on hire");
            bool banNow = cur.Effects.GetValueOrDefault(CompanyBan) > 0, banNext = next.Effects.GetValueOrDefault(CompanyBan) > 0;
            if (banNow != banNext)
            {
                if (w.Fleets.Values.Any(f => f.OwnerId == h.Id && f.HiredFrom.Length > 0)) Add(banNext ? -2 : 2, "a hired fleet fights in its line");
                if (w.EnemiesOf(h.Id).Any(e => w.Fleets.Values.Any(f => f.OwnerId == e && f.HiredFrom.Length > 0))) Add(banNext ? 2 : -2, "its enemy fights it with hired fleets");
            }
            if (chair && D(cur, next, CompanyLicence) != 0) Add(Math.Sign(D(cur, next, CompanyLicence)), "the licences would be paid to it, as the chair");
        }
        int t = TradeTight(next) - TradeTight(cur);
        if (t != 0)
        {
            if (TradesWithHouses(w, h)) Add(-Math.Sign(t) * Math.Abs(t), "it buys and sells with other houses");
            if (chair && D(cur, next, TradeLevyPct) != 0) Add(2 * Math.Sign(D(cur, next, TradeLevyPct)), "the levy would be paid to it, as the chair");
        }
        if (D(cur, next, "tolls_bonus") is var toll && toll != 0 && w.Galaxy is { } g0)
        {
            int lanes = w.HoldingsOf(h.Id).Count(x => x.IsPlanet && !x.InRebellion && g0.IsChokepoint(x.SystemId));
            Add(Math.Clamp(toll * lanes, -2, 2), "it holds worlds on the lanes, and takes the tolls");
        }
        if (D(cur, next, "credits_per_world") is var cpw && cpw != 0)
        {
            int worlds = w.HoldingsOf(h.Id).Count(x => x.IsPlanet && !x.InRebellion);
            Add(Math.Clamp(cpw * worlds / 2, -3, 3), cpw < 0 ? "it would pay it on every world it holds" : "it would gain on every world it holds");
            if (cpw < 0 && h.Treasury.Credits < DealingsSystem.ShortAt) Add(-1, "it is short of coin");
        }
        if (D(cur, next, "unrest_drift") < 0 && w.HoldingsOf(h.Id).Any(x => x.IsPlanet && x.Unrest >= RestlessAt)) Add(2, "its worlds are restless");
        int pop = D(cur, next, "pop_growth"), pros = D(cur, next, "prosperity_drift");
        if (pop != 0 && w.Galaxy is { } g1 && w.HoldingsOf(h.Id).Any(x => x.IsPlanet && g1.RingOf(x.SystemId) == Praetoria.Core.Galaxy.Ring.Rim)) Add(Math.Sign(pop), "its worlds on the rim want people");
        if (pros != 0 && w.HoldingsOf(h.Id).Count(x => x.IsPlanet && x.Prosperity >= RichWorld) >= 2) Add(Math.Sign(pros), "its worlds are rich, and live by their estates");
        int ready = D(cur, next, "readiness"), keep = D(cur, next, "fleet_upkeep_pct");
        if (ready != 0 && w.Wars.Any(x => x.Involves(h.Id))) Add(2 * Math.Sign(ready), "it is at war, and wants its crews ready");
        else if (keep != 0 && Hulls(w, h) >= LargeFleet) Add(-Math.Sign(keep), "it keeps a large fleet, and pays for it");
        if (keep > 0 && h.Treasury.Credits < DealingsSystem.ShortAt) Add(-1, "it is short of coin");
        if (Years(w, h, cur, next, offices) is { Count: > 0 } ys) Add(Math.Clamp(ys.Sum(y => y.who.Age >= y.then ? 1 : -1), -2, 2), "someone of the house is of an age the law moves");
        if (D(cur, next, RegionAllegiance) is var al && al != 0)
        {
            if (chair) Add(2 * Math.Sign(al), "it holds the capital, and the regions' allegiance is to it");
            if (h.Bloc.Length > 0) Add(-2 * Math.Sign(al), "it has left the capital's order");
            else if (w.Galaxy is { } g2 && w.HoldingsOf(h.Id).Any(x => x.IsPlanet && g2.System(x.SystemId)?.RegionId is { } r && RegionSystem.StateOf(w, r).Allegiance < RegionSystem.RebellionAllegiance + ThinAllegiance)) Add(Math.Sign(al), "its worlds lie where allegiance runs thin");
        }
        return lean;
    }
    /// <summary>A world this restless wants the dole; a world this prosperous is rich; a fleet this many hulls is large; a region this
    /// far above where risings begin is still thin in its allegiance.</summary>
    public const int RestlessAt = 40, RichWorld = 60, LargeFleet = 4, ThinAllegiance = 10;

    /// <summary>The causes of <see cref="Interest"/>, joined for the roll call: "it lends, and a cap on usury cuts what it earns".</summary>
    public static string? Because(World w, House h, PolicyCatalog cat, Motion m, OfficeCatalog offices)
    {
        var law = cat.Law(m.LawId); var next = law?.Option(m.LawOption); if (law == null || next == null) return null;
        var cur = PolicySystem.ReachOption(w, cat, law) ?? law.DefaultOption; var why = new List<(string why, int by)>(); Interest(w, h, cur, next, offices, why);
        return why.Count == 0 ? null : "the law itself, its estates and its bench; and " + string.Join("; ", why.OrderByDescending(x => Math.Abs(x.by)).Select(x => x.why));
    }

    // ---------------------------------------------------------------- what an Act would do to the player's house, in figures

    /// <summary>What setting a law of the Reach from <paramref name="cur"/> to <paramref name="next"/> would do to one house, in figures
    /// where there are figures: "your 5 worlds would pay 500 credits a year". Empty when it would do nothing to the house that a
    /// figure can say.</summary>
    public static string ToHouse(World w, House h, LawOptionDef cur, LawOptionDef next, OfficeCatalog offices, FleetSystem? fleets = null)
    {
        var parts = new List<string>(); var planets = w.HoldingsOf(h.Id).Where(x => x.IsPlanet && !x.InRebellion).ToList(); int worlds = planets.Count;
        string Each(int n) => n == 1 ? "your one world" : $"each of your {n} worlds";
        if (D(cur, next, "credits_per_world") is var cpw && cpw != 0 && worlds > 0)
            parts.Add(cpw < 0 ? $"your house would pay {Money.Credits(-cpw * worlds)} a year, {Money.Credits(-cpw)} for {(worlds == 1 ? "its one world" : $"each of its {worlds} worlds")}" : $"your house would take in {Money.Credits(cpw * worlds)} more a year");
        if (D(cur, next, "unrest_drift") is var ud && ud != 0 && worlds > 0) parts.Add($"unrest on {Each(worlds)} would move {Math.Abs(ud)} a year {(ud < 0 ? "lower" : "higher")}");
        if (D(cur, next, "pop_growth") is var pg && pg != 0 && worlds > 0) parts.Add($"the people of {Each(worlds)} would grow by {Math.Abs(pg)} {(pg > 0 ? "more" : "less")} a year");
        if (D(cur, next, "prosperity_drift") is var pd && pd != 0 && worlds > 0) parts.Add($"the prosperity of {Each(worlds)} would drift {2 * Math.Abs(pd)} a year {(pd > 0 ? "higher" : "lower")}");
        if (D(cur, next, "lawlessness_drift") is var ld && ld != 0) parts.Add($"lawlessness would move {Math.Abs(ld)} a year {(ld < 0 ? "lower" : "higher")} in every region, your own among them");
        if (D(cur, next, "tolls_bonus") is var tb && tb != 0 && w.Galaxy is { } g0 && planets.Count(x => g0.IsChokepoint(x.SystemId)) is var lanes && lanes > 0)
            parts.Add($"your {Counted.Of(lanes, "world")} on the lanes would take {Money.Credits(Math.Abs(tb) * lanes)} a year {(tb > 0 ? "more" : "less")} in tolls");
        if (D(cur, next, "fleet_upkeep_pct") is var fu && fu != 0 && fleets != null)
        {
            var chairId = RegionSystem.CapitalHolder(w)?.Id; long raw = 0;
            foreach (var f in w.Fleets.Values.Where(f => FleetSystem.BilledTo(f, chairId) == h.Id && !f.LaidUp)) foreach (var (cls, n) in fleets.Composition(f)) raw += 100L * cls.Upkeep.Credits * n;
            if (raw > 0) parts.Add($"your fleets, which cost {Money.Coin(raw)} a year to keep before the laws, would cost {Money.Coin(Math.Abs(raw * fu / 100))} {(fu > 0 ? "more" : "less")}");
        }
        if (D(cur, next, "readiness") is var rd && rd != 0 && Hulls(w, h) > 0) parts.Add($"your crews would recover {Math.Abs(rd)} readiness a year {(rd > 0 ? "faster" : "slower")}");
        if (UsuryTight(next) != UsuryTight(cur))
        {
            var v = VenturesOf(w); int capNext = next.Effects.GetValueOrDefault(LoanRateCap), capNow = cur.Effects.GetValueOrDefault(LoanRateCap);
            if (!v.IsEmpty)
            {
                var f = v.Finance; int rate = Lending.BaseRate(f), limit = f.LoanPrincipal + Lending.PerWorld * Math.Min(Lending.WorldsCounted, worlds);
                (int lend, int at) Under(int cap) => cap > 0 && rate > cap ? (Math.Max(Lending.Step, limit * cap / Math.Max(1, rate) / Lending.Step * Lending.Step), cap) : (limit, rate);
                var (a, ar) = Under(capNow); var (b, br) = Under(capNext);
                parts.Add($"the counting-houses would lend your house {Money.Credits(b)} at {br} in the hundred, where they lend {Money.Credits(a)} at {ar}");
            }
            if (Lends(w, h)) parts.Add(capNext > 0 ? $"your house, which lends, could charge no more than {capNext} in the hundred, and loans already made stand as they were made" : "your house, which lends, could charge what it likes");
        }
        int c = Math.Sign(next.Effects.GetValueOrDefault(CanvassLaw));
        if (c != Math.Sign(cur.Effects.GetValueOrDefault(CanvassLaw)))
            parts.Add(c > 0 ? $"a lavish canvass ({Money.Credits(LadderSystem.CampaignCost(LadderSystem.Lavish))}) would be forbidden to your house, and a handsome one ({Money.Credits(LadderSystem.CampaignCost(LadderSystem.Handsome))}) written down as votes bought"
                : c < 0 ? "no canvass your house paid for would be written down as votes bought, however lavish" : "a lavish canvass would be written down as votes bought, and a handsome one would not");
        if (CornerTight(next) != CornerTight(cur) && w.Market != null)
            foreach (var g in w.Market.Goods.Values.Where(g => g.CornerHouse == h.Id && g.CornerPremium > 0).OrderBy(g => g.Id, StringComparer.Ordinal))
            {
                int pct = (int)Math.Round(100 * g.CornerPremium), cap = next.Effects.GetValueOrDefault(CornerPremiumCap);
                if (next.Effects.GetValueOrDefault(CornerForcedSale) > 0 && g.Cornered) parts.Add($"your house has cornered {Resource.Label(g.Id)}, and would be made to sell down to under a third of all there is");
                else if (cap > 0 && pct > cap) parts.Add($"your hoard of {Resource.Label(g.Id)} adds {pct}% to its price, and no more than {cap}% would be allowed");
                else if (CornerTight(next) == 0) parts.Add($"your hoard of {Resource.Label(g.Id)} could earn what it will");
            }
        if (CompanyTight(next) != CompanyTight(cur))
        {
            var out_ = MercenarySystem.HiredOutBy(w, h.Id).ToList();
            if (next.Effects.GetValueOrDefault(CompanyBan) > 0) parts.Add(out_.Count > 0 ? $"{string.Join(" and ", out_.Select(f => f.Name))}, out on hire, would come home, and your house could hire no fleet out" : "your house could hire no fleet out");
            else if (next.Effects.GetValueOrDefault(CompanyLicence) is var fee && fee > 0)
                parts.Add($"each fleet your house hired out would cost {Money.Credits(fee)} for the licence, and {LicensedLegitimacy} legitimacy where it costs {MercenarySystem.Legitimacy}" + (RegionSystem.CapitalHolder(w)?.Id == h.Id ? "; as the chair, your house would be paid the licences" : ""));
            else parts.Add($"your house could hire a fleet out freely, for {MercenarySystem.Legitimacy} legitimacy each time");
        }
        if (TradeTight(next) != TradeTight(cur))
        {
            bool chair = RegionSystem.CapitalHolder(w)?.Id == h.Id;
            if (next.Effects.GetValueOrDefault(TradeBan) > 0) parts.Add($"your house could sell no lot to another house and strike no new standing deal{(w.TradeDeals.Count(d => d.SellerId == h.Id || d.BuyerId == h.Id) is var deals && deals > 0 ? $"; its {Counted.Of(deals, "standing deal")} would run out their term" : "")}");
            else if (next.Effects.GetValueOrDefault(TradeLevyPct) is var lv && lv > 0) parts.Add(chair ? $"your house, as the chair, would take {lv} in the hundred of every lot one house sells another" : $"of a lot your house sold another for {Money.Credits(10)}, the chair would take {Money.Credits(Math.Max(1, (10 * lv + 99) / 100))}");
            else parts.Add("your house could deal with any other out of its store, and pay nothing to the chair for it");
        }
        foreach (var (who, rung, now, then) in Years(w, h, cur, next, offices))
            parts.Add(who.Age >= then ? $"{who.Name} ({who.Age}) could stand for {rung.Name.ToLowerInvariant()} at once, where now it waits for {now}" : $"{who.Name} ({who.Age}) would wait until {then} to stand for {rung.Name.ToLowerInvariant()}");
        if (D(cur, next, RegionAllegiance) is var al && al != 0 && w.Galaxy is { } g3)
        {
            var mine = planets.Select(x => g3.System(x.SystemId)?.RegionId).Where(r => r != null).Distinct().OrderBy(r => r, StringComparer.Ordinal).Select(r => $"{g3.Region(r!)?.Name ?? r} at {RegionSystem.StateOf(w, r!).Allegiance}").ToList();
            parts.Add($"every region's allegiance would settle {Math.Abs(al)} {(al > 0 ? "higher" : "lower")}" + (mine.Count > 0 ? $"; your worlds lie in {string.Join(", ", mine)}, and a world rises when its region falls under {RegionSystem.RebellionAllegiance}" : ""));
        }
        return parts.Count == 0 ? "" : "To your house: " + string.Join("; ", parts) + ".";
    }

    // ---------------------------------------------------------------- the chamber legislates

    /// <summary>A bench moves an Act of its platform when it counts at least this many in the hundred for it of what it counts against
    /// (it hopes to win the rest by the vote); a house hurt by a law in force moves its repeal on less, out of grievance. A house is hurt
    /// when what the law does to it is worth this much against it (<see cref="Interest"/>).</summary>
    public const int BenchThinksPct = 80, HurtThinksPct = 60, HurtAt = 2;
    public const string BenchMovedKey = "acts_moved_by_bench", RepealMovedKey = "repeals_moved";

    /// <summary>The chamber's own law-making, once a year after the other business (<see cref="SenateSystem.Tick"/>): when nothing at
    /// all is before the Senate, so that a place is always left for the player's own motion, and no Act is already on the docket.
    /// First a house hurt by a law of the Reach in force moves its repeal, the most hurt first; else the benches, one after another
    /// from a different one each year, each move the Act of their platform that they count best placed to carry, if they count
    /// enough for it. A bench the player leads moves nothing of itself. Returns the Act moved, or null.</summary>
    public static Motion? Legislate(World w, SenateSystem senate, string? mine)
    {
        var cat = senate.Policies; var def = senate.Motions.Def("act");
        if (def == null || cat.IsEmpty || SenateSystem.Docket(w).Any()) return null;
        var acts = cat.Laws.Where(l => l.Scope == "senate" && !PolicySystem.OnCooldown(w, PolicySystem.SenateHolder, l.Id)).OrderBy(l => l.Id, StringComparer.Ordinal).ToList();
        if (acts.Count == 0) return null;
        bool Free(House h) => h.Id != mine && !World.Outsider(h.Id) && senate.MayPropose(w, h.Id) && Economy.HeadOf(w, h) != null && !w.Motions.Any(m => m.Open && m.ProposerHouseId == h.Id);
        bool Counts((int f, int a) c, int pct) => c.f > 0 && c.f * 100 >= c.a * pct;
        // 1. the hurt move a repeal: the law in force back to the custom
        var hurt = new List<(House h, LawDef law, int by)>();
        foreach (var law in acts)
        {
            var cur = PolicySystem.ReachOption(w, cat, law) ?? law.DefaultOption; if (cur.Id == law.DefaultOption.Id) continue;
            foreach (var h in w.Houses.Values.Where(Free)) if (Interest(w, h, cur, law.DefaultOption, senate.Offices) is var by && by >= HurtAt) hurt.Add((h, law, by));
        }
        foreach (var (h, law, by) in hurt.OrderByDescending(x => x.by).ThenByDescending(x => senate.Weight(w, x.h.Id)).ThenBy(x => x.h.Id, StringComparer.Ordinal).ThenBy(x => x.law.Id, StringComparer.Ordinal))
        {
            if (SenateSystem.Stale(w, h.Id, "act", law.Id + ":" + law.DefaultOption.Id)) continue;
            if (!Counts(senate.Forecast(w, h.Id, "act", "", lawId: law.Id, lawOption: law.DefaultOption.Id), HurtThinksPct)) continue;
            var m = senate.Add(w, h.Id, "act", "", lawId: law.Id, lawOption: law.DefaultOption.Id);
            w.WorldCounters[RepealMovedKey] = w.Counter(RepealMovedKey) + 1;
            w.Log($"{h.Name} moves the repeal of {InASentence(law.Name)}: what it does falls on that house, and it would have the custom back.");
            Told(w, senate, m, mine);
            return m;
        }
        // 2. the benches, a different one first each year
        var benches = w.Factions.Where(f => f.Members.Count > 0 && f.Laws.Count > 0 && f.LeaderHouseId != mine).OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
        for (int i = 0; i < benches.Count; i++)
        {
            var f = benches[(w.Turn + i) % benches.Count];
            var movers = f.Members.Select(w.House).Where(h => h != null && Free(h)).Select(h => h!).OrderByDescending(h => h.Id == f.LeaderHouseId).ThenByDescending(h => senate.Weight(w, h.Id)).ThenBy(h => h.Id, StringComparer.Ordinal).ToList();
            if (movers.Count == 0) continue;
            (House h, LawDef law, LawOptionDef o, double share)? best = null;
            foreach (var law in acts)
            {
                if (!f.Laws.TryGetValue(law.Id, out var want) || law.Option(want) is not { } o) continue;
                if ((PolicySystem.ReachOption(w, cat, law) ?? law.DefaultOption).Id == o.Id) continue;
                var mover = movers.FirstOrDefault(h => !SenateSystem.Stale(w, h.Id, "act", law.Id + ":" + o.Id)); if (mover == null) continue;
                var count = senate.Forecast(w, mover.Id, "act", "", lawId: law.Id, lawOption: o.Id);
                if (!Counts(count, BenchThinksPct)) continue;
                double share = count.Item1 / (double)Math.Max(1, count.Item1 + count.Item2);
                if (best == null || share > best.Value.share + 1e-9) best = (mover, law, o, share);
            }
            if (best is not { } b) continue;
            var moved = senate.Add(w, b.h.Id, "act", "", lawId: b.law.Id, lawOption: b.o.Id);
            w.WorldCounters[BenchMovedKey] = w.Counter(BenchMovedKey) + 1;
            w.Log($"{b.h.Name} moves, for {f.Name}, that {InASentence(b.law.Name)} be {b.o.Name.ToLowerInvariant()}: it is on the bench's platform, and the bench counts the votes to carry it.");
            Told(w, senate, moved, mine);
            return moved;
        }
        return null;
    }
    /// <summary>The player is told of an Act moved that would touch the house in figures.</summary>
    private static void Told(World w, SenateSystem senate, Motion m, string? mine)
    {
        if (mine == null || w.House(mine) is not { } house || senate.Policies.Law(m.LawId) is not { } law || law.Option(m.LawOption) is not { } next) return;
        var cur = PolicySystem.ReachOption(w, senate.Policies, law) ?? law.DefaultOption;
        if (ToHouse(w, house, cur, next, senate.Offices) is { Length: > 0 } says)
            w.Tell("info", $"{senate.ProposerName(w, m)} moves that {InASentence(law.Name)} be {next.Name.ToLowerInvariant()}. It is voted at the end of next year. {says}", "senate:chamber", m.Id);
    }
}
