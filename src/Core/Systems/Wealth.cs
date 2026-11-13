using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// House wealth (GDD §17): one number for what a house is worth — banked resources at exchange weights, land by kind,
/// prosperity and what is built on it, ventures at their book value, less debt. Ranked across the Reach each turn so
/// "the richest house" is a fact content, AI and the court can read. RNG-free.
/// </summary>
public static class Wealth
{
    public const int PlanetValue = 30, StationValue = 12, ClaimValue = 8;

    /// <summary>What is banked counts at two-thirds of what the market would pay for it (influence at four): a hoard is worth what it would fetch.</summary>
    public static int Of(World w, House house, HoldingCatalog holdings, BusinessCatalog ventures, Economy? economy = null)
    {
        var t = house.Treasury;
        double goods = 0; foreach (var r in Resource.All) if (r != Resource.Credits && r != Resource.Influence) goods += t.Get(r) * Economy.WorthOf(w, r) * 2 / 3;
        int v = t.Credits + t.Influence * 4 + (int)goods;
        foreach (var h in w.HoldingsOf(house.Id))
        {
            v += h.IsPlanet ? PlanetValue + h.Prosperity / 5 : h.IsClaim ? ClaimValue : StationValue;
            foreach (var b in h.Buildings) { var d = holdings.Building(b); if (d != null) v += d.Cost.Credits + d.Cost.Materials; }
        }
        // ventures at what they would fetch (six years' earnings, half their book at least); one inside a company counts through its shares
        foreach (var b in w.BusinessesOf(house.Id).Where(b => b.CompanyId.Length == 0)) v += economy != null ? economy.VentureValue(w, b) : ventures.Def(b.Kind)?.Value(b.Level) ?? 0;
        foreach (var p in w.ProjectsOf(house.Id).Where(p => p.CompanyId.Length == 0)) v += (int)(Economy.WorthOf(w, p.Paid) / 2);   // a thing half built is worth half what has been sunk in it
        v += PaperOf(w, house.Id);
        return v - house.Debt;
    }

    /// <summary>What a house's paper is worth: its shares at the Exchange's price and its bonds at face (docs/ECONOMY_MARKET.md stage C).</summary>
    public static int PaperOf(World w, string houseId) => CompanySystem.PaperOf(w, houseId) + w.Bonds.Where(b => b.HolderId == houseId).Sum(b => b.Face);

    /// <summary>Every house by wealth, richest first; ties by id.</summary>
    public static IReadOnlyList<(House house, int wealth)> Ranked(World w) =>
        w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId).Select(h => (house: h, wealth: h.Wealth)).OrderByDescending(x => x.wealth).ThenBy(x => x.house.Id, StringComparer.Ordinal).ToList();

    public static int RankOf(World w, string houseId) { var r = Ranked(w); for (int i = 0; i < r.Count; i++) if (r[i].house.Id == houseId) return i + 1; return r.Count; }

    public static readonly string[] Tiers = { "destitute", "poor", "comfortable", "rich", "opulent" };
    public static string Tier(int wealth) => wealth < 0 ? "destitute" : wealth < 40 ? "poor" : wealth < 100 ? "comfortable" : wealth < 200 ? "rich" : "opulent";
    public static int TierIndex(string tier) => Math.Max(0, Array.IndexOf(Tiers, tier));

    /// <summary>A house's tier among its peers (GDD §17 "gated behind wealth"): destitute below zero, else by rank — the top sixth opulent,
    /// the next quarter rich, the middle comfortable, the bottom quarter poor. Relative, so a rich age does not make every house opulent.</summary>
    public static string TierFor(World w, string houseId)
    {
        var house = w.House(houseId); if (house == null) return "poor";
        if (house.Wealth < 0) return "destitute";
        var ranked = Ranked(w); int n = Math.Max(1, ranked.Count); int rank = RankOf(w, houseId);
        double p = (rank - 1) / (double)n;
        return p < 0.16 ? "opulent" : p < 0.40 ? "rich" : p < 0.75 ? "comfortable" : "poor";
    }
}
