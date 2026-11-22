using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Salaries, on trial (docs/ECONOMY_REVIEW.md, "Salaries on trial"; the owner, 2026-11-22: "I don't know about salaries. Let's test a
/// few"). Nobody is paid to crew a hull, run a world or staff a venture, and houses bank four fifths of what they take in. A scheme
/// here is one answer to "what should that cost": a rate for each kind of work, in single credits a year (docs/MONEY.md).
/// <para><b>Off unless a world asks for it</b>: <see cref="World.Payroll"/> is empty in every game that is played. The schemes are for
/// the measuring tool (`praetoria econ`), so that a rate is chosen from what it does to thirty houses over thirty years.</para>
/// RNG-free.
/// </summary>
public static class PayrollSystem
{
    /// <param name="CrewPerManpower">A year's pay of the crew one unit of manpower laid down with a hull stands for. A laid-up fleet pays a third.</param>
    /// <param name="OfficialsPerTenPeople">The officials of a world, for each ten of its people.</param>
    /// <param name="Governor">Whoever governs a world.</param>
    /// <param name="Admiral">Whoever commands a fleet.</param>
    /// <param name="VenturePerLevel">The staff of a venture, for each of its levels.</param>
    /// <param name="PerBuilding">The hands of each building on a world.</param>
    public sealed record Scheme(string Id, string Name, int CrewPerManpower, int OfficialsPerTenPeople, int Governor, int Admiral, int VenturePerLevel, int PerBuilding);

    public static readonly Scheme[] Schemes =
    {
        new("light", "Light", 3, 5, 5, 5, 2, 1),
        new("standard", "Standard", 8, 10, 20, 20, 5, 3),
        new("heavy", "Heavy", 12, 20, 30, 30, 10, 5),
        new("crews", "Crews paid as a navy pays them", 30, 10, 20, 20, 5, 3),
    };
    public static Scheme? Of(string id) => Schemes.FirstOrDefault(s => s.Id == id);
    public static string LastKey(string houseId) => "payroll:" + houseId;

    /// <summary>What a house would pay this year under a scheme, a line for each kind of work.</summary>
    public static List<(string what, int credits)> Bill(World w, ShipClassCatalog? classes, House house, Scheme s)
    {
        var lines = new List<(string, int)>(); void Add(string what, int n) { if (n > 0) lines.Add((what, n)); }
        int crews = 0, admirals = 0;
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.Office))
        {
            int manpower = f.Ships.Count > 0 ? f.Ships.Sum(kv => kv.Value * (classes ?? ShipClassCatalog.Empty).Resolve(kv.Key).Cost.Manpower) : f.Strength * ShipClassCatalog.Generic.Cost.Manpower;
            int pay = manpower * s.CrewPerManpower; crews += f.LaidUp ? pay / 3 : pay;
            if (w.Char(f.AdmiralId) is { Alive: true }) admirals += s.Admiral;
        }
        Add("crews", crews); Add("admirals", admirals);
        var worlds = w.Holdings.Values.Where(h => h.OwnerId == house.Id).ToList();
        Add("officials", worlds.Where(h => h.IsPlanet).Sum(h => h.Population / 10 * s.OfficialsPerTenPeople));
        Add("governors", worlds.Count(h => w.Char(h.GovernorId) is { Alive: true }) * s.Governor);
        Add("the hands of the works", worlds.Sum(h => h.Buildings.Count) * s.PerBuilding);
        Add("the staff of the ventures", w.BusinessesOf(house.Id).Sum(b => b.Level) * s.VenturePerLevel);
        return lines;
    }

    /// <summary>Every house pays its year's wages, in single credits, whether the treasury can bear it or not.</summary>
    public static void Tick(World w, ShipClassCatalog? classes)
    {
        if (Of(w.Payroll) is not { } s) return;
        foreach (var house in w.Houses.Values.Where(h => !World.Outsider(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            if (Economy.HeadOf(w, house) == null) continue;
            int total = Bill(w, classes, house, s).Sum(l => l.credits); house.Treasury.AddCoin(-total); w.WorldCounters[LastKey(house.Id)] = total;
        }
    }
}
