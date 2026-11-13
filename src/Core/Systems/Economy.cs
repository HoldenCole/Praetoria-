using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>One line of a house's ledger: where a delta comes from (a holding, a venture, a charter, the loan, a fleet).</summary>
public sealed record LedgerLine(string Source, string Kind, Resources Delta);

/// <summary>A house's per-turn ledger: every line and the net (GDD §17). Forecast and accrual read the same arithmetic.</summary>
public sealed class Ledger
{
    public List<LedgerLine> Lines { get; } = new();
    public Resources Net { get; } = new();
    public void Add(string source, string kind, Resources d) { if (d.IsEmpty()) return; Lines.Add(new LedgerLine(source, kind, d)); Net.Add(d); }
}

/// <summary>
/// The domain economy (GDD §17, BuildSpec §97): per-turn accrual and upkeep applied on turn advance.
/// Every holding feeds its owner's treasury by its specialization yield plus its buildings' yields, less upkeep,
/// scaled by the holding's <see cref="Holding.Prosperity"/>; every venture pays its owner and a charter to the
/// world's holder; loans are paid down; and each house's wealth is reckoned and ranked. Populated holdings grow and
/// carry <em>unrest</em>: when a house is insolvent unrest climbs and suppresses Manpower; when solvent it decays.
/// Deliberately <b>RNG-free and order-stable</b> (houses, holdings and ventures in id order) so a turn cycle stays
/// exactly reproducible from a seed. A no-op when the holdings catalog is empty.
/// </summary>
public sealed class Economy
{
    public const int UnrestStep = 5;   // per-turn unrest gain while the owner is insolvent
    public const int UnrestDecay = 3;  // per-turn unrest relief while the owner is solvent
    public const int ProsperityStep = 5;          // how far prosperity moves toward its target in a turn
    public const int ProsperityPerBuilding = 3;
    public const int ProsperityPerAdministration = 4;
    public const int ProsperitySiege = 30;
    public const int ProsperityPerVentureLevel = 2;
    public const int GovernorSkimThreshold = -20;
    public const int GovernorSabotageThreshold = -40; // a governor who hates the head this much lets the world slide
    public const int AbleGovernorAdministration = 3;
    public const int TollCredits = 2;             // a chokepoint's holder taxes the lane
    public const int EscortFeeCap = 3;            // a fortress world escorts its neighbours' shipping for a credit a lane
    public const double FortressDefense = 0.4;    // a specialization this defensive is a fortress // a governor who dislikes the head this much pockets a credit a turn

    private readonly HoldingCatalog _catalog;
    private readonly BusinessCatalog _ventures;

    public Economy(HoldingCatalog catalog, BusinessCatalog? ventures = null, PolicyCatalog? policies = null) { _catalog = catalog; _ventures = ventures ?? BusinessCatalog.Empty; Policies = policies ?? PolicyCatalog.Empty; }
    /// <summary>Laws and estates (PolicySystem); empty when the content has none.</summary>
    public PolicyCatalog Policies { get; set; }
    private int Law(World w, string houseId, string key, string? regionId = null) => PolicySystem.Modifier(w, Policies, houseId, key, regionId);
    private int ReachLaw(World w, string key) => PolicySystem.ReachModifier(w, Policies, key);
    public BusinessCatalog Ventures => _ventures;

    /// <summary>What a house's domain will net next turn — the same arithmetic accrual applies, as the resource strip's deltas.
    /// Fleet upkeep is subtracted when the fleet system is given.</summary>
    public Resources Forecast(World world, string houseId, FleetSystem? fleets = null) => Compute(world, houseId, fleets).Net;

    /// <summary>The full ledger for a house this turn (GDD §17): holdings, ventures, charters paid and received, the loan,
    /// interest earned, fleet upkeep. Read-only.</summary>
    public Ledger Compute(World world, string houseId, FleetSystem? fleets = null)
    {
        var ledger = new Ledger();
        var house = world.House(houseId); if (house == null) return ledger;
        var tax = _catalog.Tax(house.TaxPolicy);
        var head = HeadOf(world, house);

        foreach (var holding in world.HoldingsOf(houseId).OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            var spec = _catalog.Spec(holding.Specialization); if (spec == null) continue;
            var d = new Resources(); bool besieged = world.Sieges.ContainsKey(holding.Id);
            int belt = holding.IsClaim ? world.Galaxy?.System(holding.SystemId)?.Belt ?? 0 : 0;
            var ring = world.Galaxy != null && holding.SystemId.Length > 0 ? world.Galaxy.RulesFor(world.Galaxy.RingOf(holding.SystemId)) : null;
            int lawless = world.Galaxy?.System(holding.SystemId) is { } hs && world.Regions.TryGetValue(hs.RegionId, out var rs) ? rs.Lawlessness : 0;
            if (holding.InRebellion) { ledger.Add(holding.Name, "rebellion", new Resources { Credits = -spec.Upkeep.Credits }); continue; }
            var depSys = world.Galaxy?.System(holding.SystemId);
            foreach (var r in Resource.All)
            {
                int v = spec.BaseYield.Get(r) - spec.Upkeep.Get(r);
                // a building that works a deposit brings up richness-many of what it yields (GDD §17 "not every world has everything")
                foreach (var bid in holding.Buildings) { var b = _catalog.Building(bid); if (b != null) v += (b.RequiresDeposit == r ? b.Yield.Get(r) * Math.Max(1, depSys?.Deposit(r) ?? 0) : b.Yield.Get(r)) - b.Upkeep.Get(r); }
                if (r == Resource.RareEarths && holding.IsClaim) v += ((depSys?.Deposit(r) ?? 0) + 1) / 2;   // a belt claim brings up some of the rare earths the belt holds (1 at richness 1–2, 2 at 3)
                if (r == Resource.Manpower && spec.PopManpower > 0) v += holding.Population / spec.PopManpower;
                if (r == Resource.Credits)
                {
                    v += tax?.CreditsBonus ?? 0;
                    if (_catalog.ContiguityBonus > 0 && world.Galaxy != null && holding.SystemId != "")
                        v += _catalog.ContiguityBonus * world.Galaxy.Neighbors(holding.SystemId).Count(n => world.Holdings.Values.Any(o => o.OwnerId == houseId && o.SystemId == n));
                    if (ring != null && holding.IsPlanet && holding.Population > 0) v += ring.TradeBonus;          // the core is where the commerce is (GDD §19)
                }
                // a belt claim brings up what the belt holds (GDD §17 asteroids): richer belts, more rock; the richest hide exotics
                if (r == Resource.Materials) v += belt;
                if (r == Resource.Exotics && belt >= 3) v += 1;
                // prosperity scales the economic output (credits, materials) — par at 50
                if ((r == Resource.Credits || r == Resource.Materials) && v > 0) v = v * (50 + holding.Prosperity) / 100;
                if (v > 0 && besieged) v /= 2;                                            // a blockade halves what gets through (GDD §6)
                if ((r == Resource.Credits || r == Resource.Materials) && v > 0 && lawless > 0) v = v * (200 - lawless) / 200;   // raiders on the lanes take their cut (GDD §19 the rim)
                if (r == Resource.Manpower && v > 0) v = v * (100 - holding.Unrest) / 100; // unrest taxes people (GDD §17)
                d.Add(r, v);
            }
            if (holding.IsPlanet && !Policies.IsEmpty)   // the law of the house, and the estates' regard (PolicySystem)
            {
                string? rid = depSys?.RegionId;
                d.Add(Resource.Credits, Law(world, houseId, "credits_per_world", rid)); d.Add(Resource.Materials, Law(world, houseId, "materials_per_world", rid)); d.Add(Resource.Manpower, Law(world, houseId, "manpower_per_world", rid));
            }
            // a governor who hates the head skims; an able one finds a credit the ledger did not know it had
            if (holding.GovernorId.Length > 0 && (world.Char(holding.GovernorId)?.Skill("administration") ?? 0) >= AbleGovernorAdministration && holding.IsPlanet) d.Add(Resource.Credits, 1);
            if (head != null && holding.GovernorId.Length > 0 && holding.GovernorId != head.Id && (world.GetRelationship(holding.GovernorId, head.Id)?.Disposition ?? 0) <= GovernorSkimThreshold) d.Add(Resource.Credits, -1);
            ledger.Add(holding.Name, holding.IsClaim ? "claim" : holding.IsPlanet ? "world" : "station", d);
        }

        // ventures: what they pay, less upkeep, less the charter owed to the world's holder. A venture incorporated in a company pays
        // the company, not the house (docs/ECONOMY_MARKET.md stage C): the house sees its dividends instead
        foreach (var b in world.BusinessesOf(houseId))
        {
            if (b.CompanyId.Length > 0 || _ventures.Def(b.Kind) == null) continue;
            ledger.Add(b.Name, "venture", VentureLine(world, b));
        }
        // dividends: what each company declared last year, by the shares held now; and the yearly coupon on the Reach's war bonds
        foreach (var co in world.Companies.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            int held = co.HeldBy(houseId); if (held <= 0 || co.LastDividend <= 0 || co.Shares <= 0) continue;
            int due = co.LastDividend * held / co.Shares; if (due > 0) ledger.Add($"dividend · {co.Name}", "dividend", new Resources { Credits = due });
        }
        int coupons = world.Bonds.Where(b => b.HolderId == houseId).Sum(b => b.Face * b.YieldPct / 100);
        if (coupons > 0) ledger.Add("interest on war bonds", "interest", new Resources { Credits = coupons });
        if (world.Bonds.Count > 0 && RegionSystem.CapitalHolder(world)?.Id == houseId)
        {
            int owed = world.Bonds.Where(b => b.HolderId != houseId).Sum(b => b.Face * b.YieldPct / 100);
            if (owed > 0) ledger.Add("coupons on the Reach's war bonds", "loan", new Resources { Credits = -owed });
        }
        // charters received from others' ventures on our worlds
        foreach (var b in world.Businesses.Values.Where(x => x.OwnerId != houseId).OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            var def = _ventures.Def(b.Kind); if (def == null || def.Charter == 0) continue;
            if (world.PlanetHolder(b.SystemId) != houseId) continue;
            int charter = (def.Charter + (world.Galaxy?.RulesFor(world.Galaxy.RingOf(b.SystemId)).CharterBonus ?? 0)) * b.Level; int cp = Law(world, houseId, "charter_pct"); if (cp != 0) charter = Math.Max(0, charter * (100 + cp) / 100);
            ledger.Add($"charter · {b.Name}", "charter", new Resources { Credits = charter });
        }
        // tolls: whoever holds a chokepoint's world taxes what passes (GDD §19 "a key system along a route")
        if (world.Galaxy != null)
            foreach (var h in world.HoldingsOf(houseId).Where(h => h.IsPlanet && !h.InRebellion && world.Galaxy.IsChokepoint(h.SystemId)).OrderBy(h => h.Id, StringComparer.Ordinal))
                ledger.Add($"tolls at {world.Galaxy.System(h.SystemId)?.Name ?? h.SystemId}", "tolls", new Resources { Credits = Math.Max(0, TollCredits + ReachLaw(world, "tolls_bonus")) });
        // escort fees (GDD §17 "a fortress is marginal in most runs"): a fortress world earns a credit for every neighbouring system
        // another house holds and is at peace with — ships pass under its guns and pay for the privilege — up to the cap
        if (world.Galaxy != null)
            foreach (var h in world.HoldingsOf(houseId).Where(h => !h.InRebellion && h.SystemId.Length > 0 && (_catalog.Spec(h.Specialization)?.Defense ?? 0) >= FortressDefense).OrderBy(h => h.Id, StringComparer.Ordinal))
            {
                int fees = Math.Min(EscortFeeCap, world.Galaxy.Neighbors(h.SystemId).Select(n => world.PlanetHolder(n)).Count(o => o != null && o != houseId && !world.AtWar(o, houseId)));
                if (fees > 0) ledger.Add($"escort fees · {h.Name}", "escort", new Resources { Credits = fees });
            }
        // the court: whoever holds the capital keeps it, and it pays (GDD §19 "where the capital is")
        if (world.Galaxy is { Capital.Length: > 0 } cg && world.PlanetHolder(cg.Capital) == houseId) ledger.Add($"the {(world.IsImperium ? "court" : "capital")} at {cg.System(cg.Capital)?.Name ?? cg.Capital}", "court", new Resources { Credits = 2, Influence = 1 });
        // the loan
        if (house.Debt > 0) ledger.Add("loan instalment", "loan", new Resources { Credits = -Math.Min(_ventures.Finance.LoanInstalment, house.Debt) });
        int interest = world.Houses.Values.Count(h => h.Debt > 0 && h.LenderId == houseId && h.Id != houseId);
        if (interest > 0) ledger.Add("interest on loans", "interest", new Resources { Credits = interest });

        if (fleets != null)
        {
            var chairId = RegionSystem.CapitalHolder(world)?.Id;
            foreach (var f in world.Fleets.Values.Where(f => FleetSystem.BilledTo(f, chairId) == houseId).OrderBy(f => f.Id, StringComparer.Ordinal)) { var up = fleets.Upkeep(f); var d = new Resources(); foreach (var r in Resource.All) d.Add(r, -up.Get(r)); ledger.Add(f.Office ? f.Name + " (office)" : f.Name, "fleet", d); }
            int fuel = fleets.FuelBill(world, houseId); if (fuel > 0) ledger.Add("fuel for the fleet", "fuel", new Resources { Helium3 = -fuel });
        }
        return ledger;
    }

    public const int ClusterPct = 10, ClusterCap = 3, EarningsMultiple = 6, SalvagePct = 50;

    /// <summary>What a venture nets its owner this turn (GDD §17; docs/ECONOMY_MARKET.md stage B): its yield by level (richness-many where
    /// it works a deposit), scaled by the world's prosperity, halved under siege or in the holder's war, cut by lawless lanes and an
    /// embargo, moved by the tariff law; ten per cent more for each other kind of venture working the same system (three at most: a
    /// forge beside a mine and a shipping line pays more than a forge alone); what it earns from wars, from peace and from spiking
    /// prices; less upkeep and the charter owed to the world's holder.</summary>
    public Resources VentureLine(World world, Business b)
    {
        var d = new Resources(); var def = _ventures.Def(b.Kind); if (def == null) return d; string houseId = b.OwnerId;
        var planet = world.Holdings.Values.Where(h => h.IsPlanet && h.SystemId == b.SystemId).OrderBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
        bool troubled = planet != null && (world.Sieges.ContainsKey(planet.Id) || world.AtWar(houseId, planet.OwnerId));
        int cluster = Math.Min(ClusterCap, world.Businesses.Values.Where(o => o.SystemId == b.SystemId && o.Id != b.Id && o.Kind != b.Kind).Select(o => o.Kind).Distinct().Count());
        foreach (var r in Resource.All)
        {
            int v = (def.RequiresDeposit == r ? def.Yield.Get(r) * Math.Max(1, world.Galaxy?.System(b.SystemId)?.Deposit(r) ?? 0) : def.Yield.Get(r)) * b.Level + def.FlatYield.Get(r) - def.Upkeep.Get(r) * b.Level;
            if ((r == Resource.Credits || r == Resource.Materials) && v > 0 && planet != null) v = v * (50 + planet.Prosperity) / 100;
            if ((r == Resource.Credits || r == Resource.Materials) && v > 0 && cluster > 0) v = v * (100 + ClusterPct * cluster) / 100;
            if (v > 0 && troubled) v /= 2;
            d.Add(r, v);
        }
        // what the state of the Reach does to this kind of business
        int wars = Math.Min(4, world.Wars.Count + (NationSystem.AtWar(world) != null ? 1 : 0));
        if (def.WarCredits != 0 && wars > 0) d.Credits += def.WarCredits * wars * b.Level;
        if (def.PeaceCredits != 0 && wars == 0) d.Credits += def.PeaceCredits * b.Level;
        if (def.SpikeCredits != 0) d.Credits += def.SpikeCredits * b.Level * world.WorldFlags.Count(f => f.StartsWith("price_spike:"));
        if (SenateSystem.Embargoed(world, houseId) && d.Credits > 0) d.Credits /= 2;                      // the Senate's embargo: the factors cannot sell
        int vyp = Law(world, houseId, "venture_yield_pct"); if (vyp != 0 && d.Credits > 0) d.Credits = d.Credits * (100 + vyp) / 100;   // tariffs and the merchants' regard
        int vl = world.Galaxy?.System(b.SystemId) is { } vs && world.Regions.TryGetValue(vs.RegionId, out var vr) ? vr.Lawlessness : 0;
        if (vl > 0) foreach (var r in new[] { Resource.Credits, Resource.Materials }) if (d.Get(r) > 0) d.Set(r, d.Get(r) * (200 - vl) / 200);
        if (planet != null && planet.OwnerId != houseId && def.Charter > 0) d.Add(Resource.Credits, -(def.Charter + (world.Galaxy?.RulesFor(world.Galaxy.RingOf(b.SystemId)).CharterBonus ?? 0)) * b.Level);
        return d;
    }

    /// <summary>What a unit of a resource is worth in credits now: the market's price where the market trades it, a fixed weight where it does not.</summary>
    public static double WorthOf(World world, string resource) => resource switch
    {
        Resource.Credits => 1,
        Resource.Influence => 4,
        _ => world.Market?.Good(resource)?.Price ?? resource switch { Resource.Materials => 1.5, Resource.Manpower => 2, Resource.Exotics => 7, Resource.Helium3 => 6, Resource.RareEarths => 9, Resource.Isotopes => 12, _ => 0 }
    };
    /// <summary>A bundle at this year's prices, in credits.</summary>
    public static double WorthOf(World world, Resources bundle) { double sum = 0; foreach (var r in Resource.All) sum += bundle.Get(r) * WorthOf(world, r); return sum; }

    /// <summary>A venture's earnings this year at this year's prices, in credits: every resource it nets, valued on the market.</summary>
    public int VentureEarnings(World world, Business b) => (int)Math.Round(WorthOf(world, VentureLine(world, b)));

    /// <summary>What a venture is worth (docs/ECONOMY_MARKET.md stage B): six years of its earnings at this year's prices, never less than
    /// half its book value. A helium skimmer is worth more in a fuel shortage.</summary>
    public int VentureValue(World world, Business b)
    {
        var def = _ventures.Def(b.Kind); if (def == null) return 0;
        return Math.Max(def.Value(b.Level) * SalvagePct / 100, VentureEarnings(world, b) * EarningsMultiple);
    }

    /// <summary>The house head: the protagonist for the player's house, else its senior living member.</summary>
    public static Character? HeadOf(World w, House house)
    {
        if (w.Protagonist is { } p && p.HouseId == house.Id) return p;
        return w.LivingCharacters().Where(c => c.HouseId == house.Id).OrderByDescending(c => c.CareerRank).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
    }

    /// <summary>Where a holding's prosperity is heading (GDD §17): the governor's administration, what is built, the tithe,
    /// unrest, a blockade, the ventures working the world.</summary>
    public int ProsperityTarget(World world, Holding holding)
    {
        var house = world.House(holding.OwnerId); var tax = house != null ? _catalog.Tax(house.TaxPolicy) : null;
        var governor = holding.GovernorId.Length > 0 ? world.Char(holding.GovernorId) : null;
        int t = 50 + (governor?.Skill("administration") ?? 0) * ProsperityPerAdministration + holding.Buildings.Count * ProsperityPerBuilding - holding.Unrest / 2 - (tax?.CreditsBonus ?? 0) * 4;
        if (house != null) t += Law(world, house.Id, "prosperity_drift", world.Galaxy?.System(holding.SystemId)?.RegionId) * 2;   // justice, settlement, the magistrates' regard
        var headOf = house != null ? HeadOf(world, house) : null;
        if (governor != null && headOf != null && governor.Id != headOf.Id && (world.GetRelationship(governor.Id, headOf.Id)?.Disposition ?? 0) <= GovernorSabotageThreshold) t -= 10;   // a hostile governor lets the world slide
        if (world.Sieges.ContainsKey(holding.Id)) t -= ProsperitySiege;
        if (world.Galaxy != null && holding.SystemId.Length > 0) t += world.Galaxy.RulesFor(world.Galaxy.RingOf(holding.SystemId)).Prosperity;
        if (holding.InRebellion) t -= 20;
        if (world.Galaxy != null && holding.IsPlanet && world.Galaxy.Capital == holding.SystemId) t += 5;   // the court draws trade (GDD §19)
        if (holding.IsPlanet) t += ProsperityPerVentureLevel * world.Businesses.Values.Where(b => b.SystemId == holding.SystemId).Sum(b => b.Level);
        return Math.Clamp(t, 0, 100);
    }

    /// <summary>Apply one turn of accrual to every house. Call on turn advance.</summary>
    public void Accrue(World world)
    {
        if (_catalog.IsEmpty || (world.Holdings.Count == 0 && world.Businesses.Count == 0)) return;

        // interest flows to lenders out of this turn's instalments, so snapshot who is paying before anyone's debt moves
        var paying = world.Houses.Values.Where(h => h.Debt > 0).Select(h => h.Id).ToHashSet();
        foreach (var house in world.Houses.Values.Where(h => h.Id != PirateSystem.HouseId).OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            var holdings = world.HoldingsOf(house.Id).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
            if (holdings.Count == 0 && !world.BusinessesOf(house.Id).Any() && house.Debt == 0 && !world.Businesses.Values.Any(b => world.PlanetHolder(b.SystemId) == house.Id) && !world.Houses.Values.Any(h => h.LenderId == house.Id && paying.Contains(h.Id))) continue;

            // Snapshot solvency once, before this turn's income, so all of a house's holdings react
            // to the position the player left them in (deterministic regardless of holding order).
            bool insolvent = house.Treasury.Credits < 0;
            var tax = _catalog.Tax(house.TaxPolicy);

            // the people move first — unrest reacts to the position the house is in, population settles or flees, prosperity
            // drifts — and this turn's yield is then read from the holdings as they now stand
            foreach (var holding in holdings)
            {
                var spec = _catalog.Spec(holding.Specialization);
                if (spec == null) continue;
                if (spec.Populated || holding.Population > 0)
                {
                    int ringDrift = world.Galaxy != null && holding.SystemId.Length > 0 ? world.Galaxy.RulesFor(world.Galaxy.RingOf(holding.SystemId)).UnrestDrift : 0;
                    string? lawRegion = world.Galaxy?.System(holding.SystemId)?.RegionId;
                    holding.Unrest = Math.Clamp(holding.Unrest + (insolvent ? UnrestStep : -UnrestDecay) + (tax?.UnrestDrift ?? 0) + ringDrift + Law(world, house.Id, "unrest_drift", lawRegion) + ReachLaw(world, "unrest_drift"), 0, 100);
                    int growth = holding.Unrest > 50 ? spec.PopGrowth / 2 : spec.PopGrowth;      // the restless do not settle
                    growth += Law(world, house.Id, "pop_growth", lawRegion);                        // homesteads, bonded labour, the commons' regard
                    if (holding.Prosperity >= 75 && spec.PopGrowth > 0) growth += 1;             // a thriving world draws people
                    int pop = Math.Max(0, holding.Population + growth);
                    if (world.Sieges.ContainsKey(holding.Id)) pop -= Math.Max(1, pop / 20);          // a blockade starves (GDD §6)
                    if (holding.Unrest >= 80) pop -= 2;                                               // flight from a world in revolt
                    if (spec.MaxPopulation > 0) pop = Math.Min(pop, spec.MaxPopulation);
                    holding.Population = Math.Max(0, pop);
                }
            }

            var ledger = Compute(world, house.Id);
            house.Treasury.Add(ledger.Net);
            if (house.Debt > 0) house.Debt = Math.Max(0, house.Debt - _ventures.Finance.LoanInstalment);
            if (house.Debt == 0) house.LenderId = "";

            // prosperity is the slow variable: it drifts after the yield is read, so a decree is felt next turn
            foreach (var holding in holdings)
            {
                if (_catalog.Spec(holding.Specialization) == null) continue;
                int target = ProsperityTarget(world, holding);
                holding.Prosperity = Math.Clamp(holding.Prosperity + Math.Clamp(target - holding.Prosperity, -ProsperityStep, ProsperityStep), 0, 100);
            }

            house.Treasury.ClampNonCredit();
            if (tax is { LegitimacyPerTurn: not 0 } && holdings.Count > 0) house.Legitimacy = Math.Max(0, house.Legitimacy + tax.LegitimacyPerTurn);
        }

        Reckon(world);
    }

    /// <summary>Reckon and rank every house's wealth; write the flags and counters content reads
    /// (<c>richest:&lt;house&gt;</c>, <c>wealth_rank</c>, <c>in_debt</c>, <c>business:&lt;kind&gt;</c>, <c>claim_holder</c>).</summary>
    public void Reckon(World world)
    {
        foreach (var h in world.Houses.Values) h.Wealth = Wealth.Of(world, h, _catalog, _ventures, this);
        var ranked = Wealth.Ranked(world);
        foreach (var f in world.WorldFlags.Where(f => f.StartsWith("richest:") || f.StartsWith("business:") || f == "in_debt" || f == "claim_holder").ToList()) world.WorldFlags.Remove(f);
        if (ranked.Count > 0) world.WorldFlags.Add("richest:" + ranked[0].house.Id);
        var p = world.Protagonist; var mine = p != null ? world.House(p.HouseId) : null;
        if (mine == null) return;
        world.WorldCounters["wealth_rank"] = Wealth.RankOf(world, mine.Id);
        world.WorldCounters["wealth"] = mine.Wealth;
        if (mine.Debt > 0) world.WorldFlags.Add("in_debt");
        foreach (var b in world.BusinessesOf(mine.Id)) world.WorldFlags.Add("business:" + b.Kind);
        if (world.BusinessesOf(mine.Id).Any()) world.WorldFlags.Add("business:any");
        if (world.HoldingsOf(mine.Id).Any(h => h.IsClaim)) world.WorldFlags.Add("claim_holder");
        // the strategic three: deposit:<res> where a world or claim of yours sits on one, strategic:<res> when you hold a unit
        foreach (var f in world.WorldFlags.Where(f => f.StartsWith("deposit:") || f.StartsWith("strategic:")).ToList()) world.WorldFlags.Remove(f);
        var deposits = Deposits(world, mine.Id); world.WorldCounters["deposits_held"] = deposits.Count;
        foreach (var r in deposits) world.WorldFlags.Add("deposit:" + r);
        foreach (var r in Resource.Strategic) if (mine.Treasury.Get(r) > 0) world.WorldFlags.Add("strategic:" + r);
    }

    /// <summary>The strategic resources a house sits on: every deposit in a system where it holds a world or a claim.</summary>
    public static HashSet<string> Deposits(World world, string houseId)
    {
        var set = new HashSet<string>(); var g = world.Galaxy; if (g == null) return set;
        foreach (var h in world.HoldingsOf(houseId)) if ((h.IsPlanet || h.IsClaim) && g.System(h.SystemId) is { } s) foreach (var kv in s.Deposits) if (kv.Value > 0) set.Add(kv.Key);
        return set;
    }

    /// <summary>Houses that actually bring a strategic resource up: a worked deposit (building or venture), or a claim in a rare-earth belt.</summary>
    public static int Producers(World world, string resource, HoldingCatalog catalog, BusinessCatalog ventures)
    {
        var g = world.Galaxy; if (g == null) return 0; var set = new HashSet<string>();
        foreach (var h in world.Holdings.Values)
        {
            if (g.System(h.SystemId)?.Deposit(resource) is not > 0) continue;
            if (resource == Resource.RareEarths && h.IsClaim) set.Add(h.OwnerId);
            if (h.Buildings.Any(b => catalog.Building(b)?.RequiresDeposit == resource)) set.Add(h.OwnerId);
        }
        foreach (var b in world.Businesses.Values) if (ventures.Def(b.Kind)?.RequiresDeposit == resource && g.System(b.SystemId)?.Deposit(resource) is > 0) set.Add(b.OwnerId);
        return set.Count;
    }
}
