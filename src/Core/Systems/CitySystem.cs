using Praetoria.Core.Data;
using Praetoria.Core.Galaxy;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The cities of a world (docs/CITIES.md; the owner, 2026-11-21). Each province of a world has one chief city with a name and a
/// kind, and the kind is what is special about it: the seat's is the world's capital; the others are a city of craftsmen, a
/// resort, a great port, a garrison city, a university town, a holy city, a company town, a market city, a breadbasket city, a
/// yard city or an old city, by what suits the province and the world, and by the habits of the region, so one part of the
/// Reach is not like another. Four cities are the great cities of the Reach, one each: its capital, its commerce city, the city
/// most comes out of the ground at, and the city the Academy stands in. A city's kind gives to whoever holds its province each
/// year, moves the world's prosperity and unrest, adds to its defence and to what its holder can muster, and opens one building
/// that no other kind of city takes. The rest of a world's people live in lesser cities and towns, which are counted and not
/// named. Laid out once; an older game has it done the first year it is opened. No dice: the same world always has the same cities.
/// </summary>
public static class CitySystem
{
    private static uint Hash(string s) { uint h = 2166136261; foreach (var ch in s) h = (h ^ ch) * 16777619; return h; }
    public static string GreatKey(string kindId) => "great_city:" + kindId;

    public static CityKind? KindOf(HoldingCatalog cat, Province p) => p.CityKind.Length > 0 ? cat.Cities.Kind(p.CityKind) : null;

    // ---------------- laying them out ----------------

    /// <summary>Every province that has no city yet is given one. The great cities are placed first, on the worlds they belong on.</summary>
    public static void Seed(World w, HoldingCatalog cat)
    {
        var rules = cat.Cities; var g = w.Galaxy; if (rules.IsEmpty || g == null || rules.Kind(CityRules.Capital) == null) return;
        var todo = w.Holdings.Values.Where(h => h.IsPlanet && h.Provinces.Any(p => p.City.Length == 0) && g.System(h.SystemId) != null).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        if (todo.Count == 0) return;
        var used = w.Holdings.Values.SelectMany(h => h.Provinces).Select(p => p.City).Where(n => n.Length > 0).ToHashSet(StringComparer.Ordinal);
        foreach (var great in rules.Kinds.Where(k => k.Great)) PlaceGreat(w, cat, great, used);
        foreach (var h in todo)
        {
            var sys = g.System(h.SystemId)!; var onWorld = h.Provinces.Select(p => p.CityKind).Where(k => k.Length > 0).ToHashSet();
            for (int i = 0; i < h.Provinces.Count; i++)
            {
                var p = h.Provinces[i]; if (p.City.Length > 0) continue;
                var kind = i == 0 ? rules.Kind(CityRules.Capital)! : Pick(rules, h, sys, p, i, onWorld);
                p.CityKind = kind.Id; onWorld.Add(kind.Id); p.City = Name(rules, kind, h.Id + "@" + i, used);
            }
        }
        w.ProvincesChanged();
    }

    /// <summary>The kind a province's city is: one that suits what the province is given over to and the kind of world, no kind
    /// twice on one world, and by the habits of the region first, then the world's own.</summary>
    private static CityKind Pick(CityRules rules, Holding h, StarSystem sys, Province p, int index, HashSet<string> onWorld)
    {
        var ordinary = rules.Kinds.Where(k => !k.Great && k.Id != CityRules.Capital).ToList();
        var fits = ordinary.Where(k => !onWorld.Contains(k.Id) && (k.Classes.Count == 0 || k.Classes.Contains(sys.PlanetClass)) && (k.Focuses.Count == 0 || k.Focuses.Contains(p.Focus))).ToList();
        if (fits.Count == 0) fits = ordinary.Where(k => !onWorld.Contains(k.Id) && (k.Classes.Count == 0 || k.Classes.Contains(sys.PlanetClass))).ToList();
        if (fits.Count == 0) fits = ordinary;
        return fits.OrderBy(k => Hash(sys.RegionId + ":" + k.Id) % 100 + Hash(h.Id + ":" + index + ":" + k.Id) % 70).ThenBy(k => k.Id, StringComparer.Ordinal).First();
    }

    /// <summary>A name nobody else in the game has: two parts from the lists, in the shape a city of that kind is called by.</summary>
    private static string Name(CityRules rules, CityKind kind, string key, HashSet<string> used)
    {
        if (rules.First.Count == 0 || rules.Last.Count == 0) return key;
        var shapes = rules.Patterns.TryGetValue(kind.Id, out var s) && s.Count > 0 ? s : new[] { "{0}" };
        for (int n = 0; n < 400; n++)
        {
            uint a = Hash(key + "#" + n), b = Hash("last:" + key + "#" + n), c = Hash("shape:" + key);
            string name = string.Format(shapes[(int)(c % (uint)shapes.Count)], rules.First[(int)(a % (uint)rules.First.Count)] + rules.Last[(int)(b % (uint)rules.Last.Count)]);
            if (used.Add(name)) return name;
        }
        string last = kind.Id + " " + key; used.Add(last); return last;
    }

    /// <summary>One of the four great cities, put where it belongs, once in a game: the capital of the Reach on the world the
    /// Senate sits on; the commerce city on the most peopled trading world; the Lode on the world with most in the ground; the
    /// Academy city on the most peopled living world that is none of those.</summary>
    private static void PlaceGreat(World w, HoldingCatalog cat, CityKind great, HashSet<string> used)
    {
        if (w.Counter(GreatKey(great.Id)) > 0 || w.Holdings.Values.Any(h => h.Provinces.Any(p => p.CityKind == great.Id))) { w.WorldCounters[GreatKey(great.Id)] = 1; return; }
        var g = w.Galaxy!; bool Taken(Holding h) => h.Provinces.Any(p => cat.Cities.Kind(p.CityKind)?.Great == true);
        var worlds = w.Holdings.Values.Where(h => h.IsPlanet && h.Provinces.Count > 0 && g.System(h.SystemId) != null && w.OfTheReach(h.OwnerId)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        Holding? at = null; int index = 0;
        switch (great.Place)
        {
            case "capital": at = worlds.FirstOrDefault(h => h.SystemId == g.Capital) ?? w.Holdings.Values.FirstOrDefault(h => h.IsPlanet && h.SystemId == g.Capital && h.Provinces.Count > 0); break;
            case "commerce":
                at = worlds.Where(h => !Taken(h) && h.SystemId != g.Capital && h.Provinces.Count > 1).OrderByDescending(h => h.Specialization == "trade_hub" ? 1 : 0).ThenByDescending(h => Census.Citizens(w, h)).FirstOrDefault(); index = 1; break;
            case "resource":
                at = worlds.Where(h => !Taken(h) && h.SystemId != g.Capital && h.Provinces.Count > 1).OrderByDescending(h => g.System(h.SystemId)!.PlanetClass is "belt" or "gas" ? 0 : 1)   // a city needs ground to stand on
                    .ThenByDescending(h => g.System(h.SystemId)!.Deposits.Values.Sum() + g.System(h.SystemId)!.Belt).ThenByDescending(h => h.Specialization is "forge_world" or "mining_belt" ? 1 : 0).ThenByDescending(h => Census.Citizens(w, h)).FirstOrDefault();
                index = at == null ? 1 : Math.Max(1, at.Provinces.FindIndex(1, p => cat.Provinces.Focus(p.Focus)?.Deposit.Length > 0 || p.Focus is "mines" or "foundries")); break;
            case "academy":
                at = worlds.Where(h => !Taken(h) && h.SystemId != g.Capital && h.Provinces.Count > 1 && g.System(h.SystemId)!.PlanetClass is "terran" or "ocean").OrderByDescending(h => g.RingOf(h.SystemId) == Ring.Rim ? 0 : 1).ThenByDescending(h => Census.Citizens(w, h)).FirstOrDefault(); index = 1; break;
        }
        if (at == null || index < 0 || index >= at.Provinces.Count) return;
        var p = at.Provinces[index]; p.CityKind = great.Id; p.City = Name(cat.Cities, great, at.Id + "@" + index, used);
        w.WorldCounters[GreatKey(great.Id)] = 1;
    }

    // ---------------- what a city does ----------------

    /// <summary>What the city of a province gives a year to whoever holds the province.</summary>
    public static Resources Yield(HoldingCatalog cat, Province p) => KindOf(cat, p)?.Yield ?? new Resources();
    /// <summary>What a world's cities do to where its prosperity heads and to its unrest a year, city by city.</summary>
    public static IEnumerable<(string name, int prosperity, int unrest)> OnWorld(HoldingCatalog cat, Holding planet)
    {
        foreach (var p in planet.Provinces) if (KindOf(cat, p) is { } k && (k.Prosperity != 0 || k.Unrest != 0)) yield return ($"{p.City}, {k.Name}", k.Prosperity, k.Unrest);
    }
    public static double Defense(HoldingCatalog cat, Holding planet) { double d = 0; foreach (var p in planet.Provinces) d += KindOf(cat, p)?.Defense ?? 0; return d; }
    /// <summary>What the cities of a house's worlds add to the manpower it can keep.</summary>
    public static int Muster(World w, HoldingCatalog cat, string houseId) => w.HoldingsOf(houseId).Where(h => h.IsPlanet).Sum(h => h.Provinces.Sum(p => KindOf(cat, p)?.Muster ?? 0));

    /// <summary>A city's bonus in words, for the screen.</summary>
    public static string Says(CityKind k)
    {
        var parts = new List<string>();
        var y = Resource.All.Where(r => k.Yield.Get(r) != 0).Select(r => $"+{k.Yield.Get(r)} {Resource.Label(r)}").ToList();
        if (y.Count > 0) parts.Add(string.Join(", ", y) + " a year to the house that holds the province");
        if (k.Prosperity != 0) parts.Add($"the world's prosperity heads {Math.Abs(k.Prosperity)} {(k.Prosperity > 0 ? "higher" : "lower")}");
        if (k.Unrest != 0) parts.Add(k.Unrest < 0 ? $"unrest {k.Unrest} a year" : $"unrest +{k.Unrest} a year");
        if (k.Defense > 0) parts.Add($"defence +{k.Defense:0.##}");
        if (k.Muster > 0) parts.Add($"the world's holder can keep {k.Muster} more manpower");
        return string.Join(" · ", parts);
    }

    // ---------------- how many live there ----------------

    /// <summary>The citizens of a province's chief city: its kind's share of the world's, a little more or less by the city itself,
    /// and never more than the province holds.</summary>
    public static long Citizens(World w, HoldingCatalog cat, Holding planet, int index)
    {
        if (index < 0 || index >= planet.Provinces.Count || KindOf(cat, planet.Provinces[index]) is not { } k) return 0;
        double own = 0.8 + Hash("city:" + planet.Id + "@" + index) % 4001 / 10000.0;
        return Math.Min(Census.CitizensOfProvince(w, planet, index), (long)(Census.Citizens(w, planet) * (k.Share / 1000.0) * own));
    }
    /// <summary>The lesser cities and towns of a world: the people who live in none of its chief cities, by the million.</summary>
    public static int Lesser(World w, HoldingCatalog cat, Holding planet)
    {
        long rest = Census.Citizens(w, planet) - Enumerable.Range(0, planet.Provinces.Count).Sum(i => Citizens(w, cat, planet, i));
        return rest <= 0 ? 0 : Math.Max(1, (int)Math.Round(rest / 1_000_000.0 * cat.Cities.LesserPerMillion / 1000.0));
    }
    /// <summary>The four great cities as they stand: where each is and who holds it.</summary>
    public static IEnumerable<(CityKind kind, Holding world, int index)> Great(World w, HoldingCatalog cat)
    {
        foreach (var k in cat.Cities.Kinds.Where(k => k.Great))
            foreach (var h in w.Holdings.Values.OrderBy(h => h.Id, StringComparer.Ordinal))
            { int i = h.Provinces.FindIndex(p => p.CityKind == k.Id); if (i >= 0) { yield return (k, h, i); break; } }
    }
}
