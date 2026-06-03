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

            foreach (var holding in holdings)
            {
                var spec = _catalog.Spec(holding.Specialization);
                if (spec == null) continue;

                if (spec.Populated || holding.Population > 0)
                {
                    holding.Unrest = Math.Clamp(
                        holding.Unrest + (insolvent ? UnrestStep : -UnrestDecay), 0, 100);
                    holding.Population = Math.Max(0, holding.Population + spec.PopGrowth);
                }

                foreach (var r in Resource.All)
                {
                    int net = spec.BaseYield.Get(r) - spec.Upkeep.Get(r);
                    foreach (var bid in holding.Buildings)
                    {
                        var b = _catalog.Building(bid);
                        if (b != null) net += b.Yield.Get(r) - b.Upkeep.Get(r);
                    }

                    // Unrest is a tax on people: it throttles Manpower output (GDD §17).
                    if (r == Resource.Manpower && net > 0)
                        net = net * (100 - holding.Unrest) / 100;

                    house.Treasury.Add(r, net);
                }
            }

            house.Treasury.ClampNonCredit();
        }
    }
}
