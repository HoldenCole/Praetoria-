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

    public static int Of(World w, House house, HoldingCatalog holdings, BusinessCatalog ventures)
    {
        var t = house.Treasury;
        int v = t.Credits + t.Materials * 2 / 3 + t.Influence * 4 + t.Exotics * 6;
        foreach (var h in w.HoldingsOf(house.Id))
        {
            v += h.IsPlanet ? PlanetValue + h.Prosperity / 5 : h.IsClaim ? ClaimValue : StationValue;
            foreach (var b in h.Buildings) { var d = holdings.Building(b); if (d != null) v += d.Cost.Credits + d.Cost.Materials; }
        }
        foreach (var b in w.BusinessesOf(house.Id)) v += ventures.Def(b.Kind)?.Value(b.Level) ?? 0;
        return v - house.Debt;
    }

    /// <summary>Every house by wealth, richest first; ties by id.</summary>
    public static IReadOnlyList<(House house, int wealth)> Ranked(World w) =>
        w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId).Select(h => (house: h, wealth: h.Wealth)).OrderByDescending(x => x.wealth).ThenBy(x => x.house.Id, StringComparer.Ordinal).ToList();

    public static int RankOf(World w, string houseId) { var r = Ranked(w); for (int i = 0; i < r.Count; i++) if (r[i].house.Id == houseId) return i + 1; return r.Count; }

    public static string Tier(int wealth) => wealth < 0 ? "destitute" : wealth < 40 ? "poor" : wealth < 100 ? "comfortable" : wealth < 200 ? "rich" : "opulent";
}
