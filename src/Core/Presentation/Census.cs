using Praetoria.Core.Galaxy;
using Praetoria.Core.State;

namespace Praetoria.Core.Presentation;

/// <summary>
/// How many citizens a world holds (the owner, 2026-11-21: "the citizens of certain cities or planets should be realistic numbers").
/// The rules reckon a world's people on a small scale (<see cref="Holding.Population"/>, 30 to 60 for a settled world): manpower,
/// development and what a siege costs go by it, and that is unchanged. This says what that scale is in citizens on a given world.
/// A world twice as high on the scale holds four times the people, since cities grow faster than the land that feeds them; a
/// terran world holds far more to the point than a frozen rock, and the core more than the rim. Each world also differs a little
/// from its like, fixed for the life of the game. Read only: nothing is stored.
/// </summary>
public static class Census
{
    /// <summary>Citizens for the first point of the scale on a terran world of the middle ring.</summary>
    public const double PerPointSquared = 2_000_000;

    /// <summary>How much of a world can be lived on, by its kind.</summary>
    public static double Habitable(string planetClass) => planetClass switch
    {
        "terran" => 1.0, "ocean" => 0.8, "arid" => 0.45, "forge" => 0.35, "frozen" => 0.2, "barren" => 0.12, "gas" => 0.05, "belt" => 0.03, _ => 0.5,
    };
    public static double RingFactor(string ring) => ring switch { Ring.Core => 1.3, Ring.Rim => 0.6, _ => 1.0 };

    /// <summary>A world's own difference from its like, 0.85 to 1.15, from its name alone.</summary>
    private static double Own(string holdingId)
    {
        uint h = 2166136261; foreach (char c in holdingId) h = (h ^ c) * 16777619;
        return 0.85 + (h % 3001) / 10000.0;
    }

    /// <summary>What the scale is worth on this world: the citizens a world of scale 1 would hold here.</summary>
    public static double Scale(World w, Holding h)
    {
        var sys = w.Galaxy?.System(h.SystemId);
        double kind = h.IsPlanet ? Habitable(sys?.PlanetClass ?? "terran") : 0.01;   // a station's people live in what was built for them
        double ring = w.Galaxy != null && h.SystemId.Length > 0 ? RingFactor(w.Galaxy.RingOf(h.SystemId)) : 1.0;
        return PerPointSquared * kind * ring * Own(h.Id);
    }

    /// <summary>The citizens a holding would hold at a given point of the scale.</summary>
    public static long At(World w, Holding h, int points) => points <= 0 ? 0 : (long)Math.Round(Scale(w, h) * points * points);
    /// <summary>The citizens of a holding.</summary>
    public static long Citizens(World w, Holding h) => At(w, h, h.Population);
    /// <summary>The citizens of every world and station a house holds.</summary>
    public static long CitizensOf(World w, string houseId) => w.HoldingsOf(houseId).Sum(h => Citizens(w, h));

    /// <summary>The citizens of one province of a world: the seat holds half, the rest share the other half by their level.</summary>
    public static long CitizensOfProvince(World w, Holding planet, int index)
    {
        long all = Citizens(w, planet); int n = planet.Provinces.Count; if (n <= 1 || index < 0 || index >= n) return all;
        if (index == 0) return all / 2;
        int levels = planet.Provinces.Skip(1).Sum(p => Math.Max(1, p.Level)); return all / 2 * Math.Max(1, planet.Provinces[index].Level) / Math.Max(1, levels);
    }

    /// <summary>A count of people in words a reader takes in at a glance: "7.2 billion", "640 million", "86,000".</summary>
    public static string Say(long n)
    {
        if (n >= 1_000_000_000) { double b = n / 1e9; return (b >= 100 ? b.ToString("0") : b >= 10 ? b.ToString("0.#") : b.ToString("0.0#")) + " billion"; }
        if (n >= 1_000_000) { double m = n / 1e6; return (m >= 100 ? m.ToString("0") : m >= 10 ? m.ToString("0.#") : m.ToString("0.0#")) + " million"; }
        if (n >= 10_000) return (n / 1000 * 1000).ToString("N0");
        return n.ToString("N0");
    }
    /// <summary>The same, short, for a label on the map: "7.2B", "640M", "86K".</summary>
    public static string Short(long n) =>
        n >= 1_000_000_000 ? (n / 1e9 >= 10 ? (n / 1e9).ToString("0") : (n / 1e9).ToString("0.#")) + "B"
        : n >= 1_000_000 ? (n / 1e6 >= 10 ? (n / 1e6).ToString("0") : (n / 1e6).ToString("0.#")) + "M"
        : n >= 1000 ? (n / 1000).ToString("0") + "K" : n.ToString();
}
