using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The domain economy (GDD §17, BuildSpec §97): per-turn accrual and upkeep applied on turn advance.
/// For each house, every holding it owns feeds its treasury by the holding's specialization yield
/// plus its buildings' yields, less all upkeep. Populated holdings grow and carry <em>unrest</em>:
/// when a house is insolvent (Credits &lt; 0) unrest climbs and suppresses Manpower output; when
/// solvent it decays — the §17 "the one you run out of" tension, in miniature. Deliberately
/// <b>RNG-free and order-stable</b> (houses and holdings processed in id order) so a turn cycle
/// stays exactly reproducible from a seed. A no-op when the catalog is empty.
/// </summary>
public sealed class Economy
{
    public const int UnrestStep = 5;   // per-turn unrest gain while the owner is insolvent
    public const int UnrestDecay = 3;  // per-turn unrest relief while the owner is solvent

    private readonly HoldingCatalog _catalog;

    public Economy(HoldingCatalog catalog) => _catalog = catalog;

    /// <summary>What a house's domain will net next turn (the Accrue arithmetic, read-only; no siege or unrest throttling,
    /// which are shown separately): the resource strip's per-turn deltas (GDD §17). Fleet upkeep is subtracted when given.</summary>
    public Resources Forecast(World world, string houseId, FleetSystem? fleets = null)
    {
        var net = new Resources();
        var house = world.House(houseId); if (house == null) return net;
        var tax = _catalog.Tax(house.TaxPolicy);
        foreach (var holding in world.HoldingsOf(houseId))
        {
            var spec = _catalog.Spec(holding.Specialization); if (spec == null) continue;
            foreach (var r in Resource.All)
            {
                int v = spec.BaseYield.Get(r) - spec.Upkeep.Get(r);
                foreach (var bid in holding.Buildings) { var b = _catalog.Building(bid); if (b != null) v += b.Yield.Get(r) - b.Upkeep.Get(r); }
                if (r == Resource.Manpower && spec.PopManpower > 0) v += holding.Population / spec.PopManpower;
                if (r == Resource.Credits)
                {
                    v += tax?.CreditsBonus ?? 0;
                    if (_catalog.ContiguityBonus > 0 && world.Galaxy != null && holding.SystemId != "")
                        v += _catalog.ContiguityBonus * world.Galaxy.Neighbors(holding.SystemId).Count(n => world.Holdings.Values.Any(o => o.OwnerId == houseId && o.SystemId == n));
                }
                if (r == Resource.Manpower && v > 0) v = v * (100 - holding.Unrest) / 100;
                net.Add(r, v);
            }
        }
        if (fleets != null)
            foreach (var f in world.Fleets.Values.Where(f => f.OwnerId == houseId)) { var up = fleets.Upkeep(f); foreach (var r in Resource.All) net.Add(r, -up.Get(r)); }
        return net;
    }

    /// <summary>Apply one turn of accrual to every holding's owning treasury. Call on turn advance.</summary>
    public void Accrue(World world)
    {
        if (_catalog.IsEmpty || world.Holdings.Count == 0) return;

        foreach (var house in world.Houses.Values.OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            var holdings = world.HoldingsOf(house.Id)
                                .OrderBy(h => h.Id, StringComparer.Ordinal)
                                .ToList();
            if (holdings.Count == 0) continue;

            // Snapshot solvency once, before this turn's income, so all of a house's holdings react
            // to the position the player left them in (deterministic regardless of holding order).
            bool insolvent = house.Treasury.Credits < 0;
            var tax = _catalog.Tax(house.TaxPolicy);

            foreach (var holding in holdings)
            {
                var spec = _catalog.Spec(holding.Specialization);
                if (spec == null) continue;

                if (spec.Populated || holding.Population > 0)
                {
                    holding.Unrest = Math.Clamp(
                        holding.Unrest + (insolvent ? UnrestStep : -UnrestDecay) + (tax?.UnrestDrift ?? 0), 0, 100);
                    int growth = holding.Unrest > 50 ? spec.PopGrowth / 2 : spec.PopGrowth;      // the restless do not settle
                    int pop = Math.Max(0, holding.Population + growth);
                    if (world.Sieges.ContainsKey(holding.Id)) pop -= Math.Max(1, pop / 20);          // a blockade starves (GDD §6)
                    if (holding.Unrest >= 80) pop -= 2;                                               // flight from a world in revolt
                    if (spec.MaxPopulation > 0) pop = Math.Min(pop, spec.MaxPopulation);
                    holding.Population = Math.Max(0, pop);
                }

                foreach (var r in Resource.All)
                {
                    int net = spec.BaseYield.Get(r) - spec.Upkeep.Get(r);
                    foreach (var bid in holding.Buildings)
                    {
                        var b = _catalog.Building(bid);
                        if (b != null) net += b.Yield.Get(r) - b.Upkeep.Get(r);
                    }

                    // People are soldiers (GDD §17): population feeds Manpower, then unrest taxes it below.
                    if (r == Resource.Manpower && spec.PopManpower > 0)
                        net += holding.Population / spec.PopManpower;

                    // The tax posture (GDD §17) and territory (GDD §19 — a compact realm is a richer one).
                    if (r == Resource.Credits)
                    {
                        net += tax?.CreditsBonus ?? 0;
                        if (_catalog.ContiguityBonus > 0 && world.Galaxy != null && holding.SystemId != "")
                            net += _catalog.ContiguityBonus * world.Galaxy.Neighbors(holding.SystemId)
                                .Count(n => world.Holdings.Values.Any(o => o.OwnerId == house.Id && o.SystemId == n));
                    }

                    // A blockade halves what gets through (GDD §6 siege).
                    if (net > 0 && world.Sieges.ContainsKey(holding.Id)) net /= 2;

                    // Unrest is a tax on people: it throttles Manpower output (GDD §17).
                    if (r == Resource.Manpower && net > 0)
                        net = net * (100 - holding.Unrest) / 100;

                    house.Treasury.Add(r, net);
                }
            }

            house.Treasury.ClampNonCredit();
            if (tax is { LegitimacyPerTurn: not 0 }) house.Legitimacy = Math.Max(0, house.Legitimacy + tax.LegitimacyPerTurn);
        }
    }
}
