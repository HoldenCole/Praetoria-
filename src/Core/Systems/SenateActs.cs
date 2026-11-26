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

    private static readonly ConditionalWeakTable<World, PolicyCatalog> ByWorld = new();
    /// <summary>This world's laws, given by its controller.</summary>
    public static void Use(World w, PolicyCatalog policies) => ByWorld.AddOrUpdate(w, policies);
    public static PolicyCatalog Of(World w) => ByWorld.TryGetValue(w, out var p) ? p : PolicyCatalog.Empty;

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
}
