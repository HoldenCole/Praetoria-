using System.Runtime.CompilerServices;
using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// What a thing is meant to return a year, by its kind (docs/ECONOMY_REVIEW.md, "Ranged returns"; the owner, 2026-11-22: "certain
/// resources pay more than others and certain investments do as well, like the real world"). The content files are priced to these:
/// a test fails if anything returns more than its kind is meant to at the Exchange's usual prices.
/// </summary>
public static class ReturnTargets
{
    private static readonly Dictionary<string, double> Usual = new() { [Resource.Credits] = 1, [Resource.Materials] = 1.5, [Resource.Manpower] = 2, [Resource.Exotics] = 7, [Resource.Helium3] = 6, [Resource.RareEarths] = 9, [Resource.Isotopes] = 12, [Resource.Influence] = 6 };
    /// <summary>A bundle in the books' hundreds at the Exchange's usual prices; clout at the rate the game sells it for.</summary>
    public static double Worth(Resources r) => Resource.All.Sum(k => Usual[k] * r.Get(k));
    public static bool Strategic(Resources y) => y.Helium3 > 0 || y.RareEarths > 0 || y.Isotopes > 0 || y.Exotics > 0;

    /// <summary>The share of its cost a thing of this kind is meant to pay back a year, or 0 for a kind that is left as it is priced.</summary>
    public static double Of(Resources yields, string sector, string id, bool great)
    {
        if (id == "salvage_guild") return 0.20;                                                  // speculative
        if (sector == "arms" && !Strategic(yields)) return 0;                                    // war ventures: thin at peace, rich in a war
        if (id is "insurance_house" or "commodity_warehouse" or "shipyard_company") return 0;    // they live on peace, spikes and wars
        if (Strategic(yields)) return great ? 0.12 : 0.15;                                       // the dear goods, and up to three times this while one is short
        if (great) return yields.Materials > 0 ? 0.10 : 0.05;                                    // a great work that makes something earns; one for show does not
        if (sector == "shipping") return 0.12;
        if (sector is "trade" or "services") return 0.10;
        if (sector == "finance") return 0.09;
        if (yields.Influence > 0 || yields.Materials > 0) return 0.10;
        if (yields.Manpower > 0) return 0.08;
        return 0.07;                                                                             // credits only: markets, ports, power
    }
    /// <summary>What a thing returns a year as a share of its cost, at usual prices and full prosperity; 0 if it pays nothing.</summary>
    public static double Returns(Resources cost, Resources yield, Resources flat, Resources upkeep)
    {
        double net = Worth(yield) + Worth(flat) - Worth(upkeep), c = Worth(cost); return net <= 0 || c <= 0 ? 0 : net / c;
    }
    /// <summary>Every thing that is built and what it returns against what its kind is meant to: (kind, id, name, returns, meant).</summary>
    public static IEnumerable<(string kind, string id, string name, double returns, double meant, Resources cost)> All(ContentDatabase content)
    {
        var none = new Resources();
        foreach (var b in content.Holdings.Buildings.OrderBy(b => b.Id, StringComparer.Ordinal)) yield return ("building", b.Id, b.Name, Returns(b.Cost, b.Yield, none, b.Upkeep), Of(b.Yield, "", b.Id, false), b.Cost);
        foreach (var x in content.Holdings.Works.OrderBy(x => x.Id, StringComparer.Ordinal)) yield return (x.IsMegastructure ? "great work" : "work", x.Id, x.Name, Returns(x.Cost, x.Yield, none, x.Upkeep), Of(x.Yield, "", x.Id, x.IsMegastructure), x.Cost);
        foreach (var v in content.Businesses.Defs) { var all = v.Yield.Clone(); all.Add(v.FlatYield); yield return ("venture", v.Id, v.Name, Returns(v.Cost, v.Yield, v.FlatYield, v.Upkeep), Of(all, v.Sector, v.Id, false), v.Cost); }
    }
}

/// <summary>
/// How easy money is in a game, and the boosts a game may be begun with (docs/MONEY.md, "Settings"; the owner, 2026-11-23: "having
/// settings for how easy it is to make money and even some cheats to select in the beginning is useful. So you can choose the
/// 'difficulty' of making money and how returns are scaled. Let's just put our numbers as the default"; and of what was first built
/// under that name, "Those aren't cheats. Just boosts").
/// <para><b>How easy money is</b> scales what everything that is built costs, and so what it returns: at the standard setting a thing
/// costs what the content says and returns what its kind is meant to (<see cref="ReturnTargets"/>). It is every house's, not only
/// the player's. Hulls are not scaled.</para>
/// <para><b>Boosts</b> are the player's house's alone, are chosen when the game is begun, and are written on the save.</para>
/// </summary>
public static class MoneySettings
{
    public const string Lean = "lean", Standard = "standard", Generous = "generous", Lavish = "lavish";
    public static readonly string[] Levels = { Lean, Standard, Generous, Lavish };
    /// <summary>What a thing returns at this setting, in hundredths of what its kind is meant to.</summary>
    public static int ReturnPct(string level) => level switch { Lean => 70, Generous => 150, Lavish => 250, _ => 100 };
    public static string Name(string level) => level switch { Lean => "Lean", Generous => "Generous", Lavish => "Lavish", _ => "Standard" };
    public static string Does(string level) => level switch
    {
        Lean => "Everything that is built costs about four tenths more, so it returns seven tenths of what its kind is meant to. An ordinary building pays back in fourteen years and more.",
        Generous => "Everything that is built costs two thirds of its price, so it returns half as much again. An ordinary building pays back in about seven years.",
        Lavish => "Everything that is built costs two fifths of its price, so it returns two and a half times what its kind is meant to. An ordinary building pays back in four years.",
        _ => "Things cost what they are priced at. An ordinary building returns 7 to 10% a year and pays back in ten to fourteen years; the dear goods return 15%, and up to three times that while one is short."
    };

    public const string FullTreasury = "full_treasury", FullStores = "full_stores", DoubleIncome = "double_income", DeepPurse = "deep_purse";
    public static readonly string[] Boosts = { FullTreasury, FullStores, DoubleIncome, DeepPurse };
    public const int TreasuryGift = 500, PurseGift = 100, StoresMaterials = 200, StoresManpower = 100, StoresDear = 5;   // under six of a dear good: a boost does not open the game holding a corner (docs/ECONOMY_MARKET.md, "Cornering a good")
    public static string BoostName(string id) => id switch { FullTreasury => "A full treasury", FullStores => "Full stores", DoubleIncome => "A second income", DeepPurse => "A deep purse", _ => id };
    public static string BoostDoes(string id) => id switch
    {
        FullTreasury => $"Your house begins with {Money.Credits(TreasuryGift)} more.",
        FullStores => $"Your house begins with {StoresMaterials} more materials, {StoresManpower} more manpower and {StoresDear} more of helium-3, rare earths, isotopes and exotics.",
        DoubleIncome => "Every year your house takes in again all the credits it took in.",
        DeepPurse => $"You begin with {Money.Credits(PurseGift)} in your own purse.",
        _ => ""
    };
    private const string BegunFlag = "boosts_begun";

    /// <summary>What a game begun with boosts says of itself, or "" for a game begun with none.</summary>
    public static string Says(World w) => w.Boosts.Count == 0 ? "" : "Begun with: " + string.Join(", ", Boosts.Where(w.Boosts.Contains).Select(BoostName));

    /// <summary>Give the player's house what its boosts give at the start. Once: asking again gives nothing.</summary>
    public static void Begin(World w)
    {
        if (w.Boosts.Count == 0 || w.WorldFlags.Contains(BegunFlag) || w.WorldFlags.Contains("cheats_begun") || w.Protagonist is not { } p || w.House(p.HouseId) is not { } house) return;
        w.WorldFlags.Add(BegunFlag);
        if (w.Boosts.Contains(FullTreasury)) house.Treasury.Credits += TreasuryGift;
        if (w.Boosts.Contains(FullStores)) { house.Treasury.Materials += StoresMaterials; house.Treasury.Manpower += StoresManpower; foreach (var r in new[] { Resource.Helium3, Resource.RareEarths, Resource.Isotopes, Resource.Exotics }) house.Treasury.Add(r, StoresDear); }
        if (w.Boosts.Contains(DeepPurse)) p.Purse += PurseGift;
        w.Log($"{house.Name} begins with more than it had. {Says(w)}.");
    }

    // ---- what things cost at a setting ----

    private static readonly ConditionalWeakTable<ContentDatabase, Dictionary<Resources, Resources>> Priced = new();
    /// <summary>Price everything that is built for this world's setting. The content's own prices are kept, so the same content can
    /// serve a game at any setting one after another; at the standard setting on content never repriced, nothing is touched.</summary>
    public static void Apply(ContentDatabase content, World w)
    {
        int pct = ReturnPct(w.MoneyEase);
        lock (content)
        {
            if (!Priced.TryGetValue(content, out var written)) { if (pct == 100) return; written = new(ReferenceEqualityComparer.Instance); Priced.Add(content, written); }
            foreach (var cost in Costs(content))
            {
                if (!written.TryGetValue(cost, out var was)) written[cost] = was = cost.Clone();
                foreach (var r in Resource.All) { int n = was.Get(r); cost.Set(r, n <= 0 ? n : Math.Max(1, (int)Math.Ceiling(n * 100.0 / pct))); }
            }
        }
    }
    private static IEnumerable<Resources> Costs(ContentDatabase content)
    {
        foreach (var b in content.Holdings.Buildings) yield return b.Cost;
        foreach (var x in content.Holdings.Works) yield return x.Cost;
        foreach (var t in content.Holdings.Infrastructure) yield return t.Cost;
        foreach (var v in content.Businesses.Defs) { yield return v.Cost; yield return v.ExpandCost; }
    }
}
