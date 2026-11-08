using Praetoria.Core.Galaxy;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Lawlessness with a face (GAP §6 item 3). When a region's lawlessness climbs past <see cref="SpawnLawlessness"/> and no
/// raiders already prowl it, a raider fleet appears at its most lawless corner (an unheld world if there is one) under the
/// pseudo-house <see cref="HouseId"/>, which is at war with everyone and holds nothing. Raiders roam a lane a turn, avoiding
/// stronger fleets; where they sit over a held world they <b>raid</b> it (prosperity down, the region's lawlessness up); any
/// house fleet that meets them fights them through the ordinary battle rules, and the hunter that breaks a raider fleet earns
/// legitimacy and quiets the lanes. Raiders pay no upkeep and dissolve when their region grows lawful. RNG-free: roaming
/// takes a hash of the turn and the fleet id.
/// </summary>
public sealed class PirateSystem
{
    public const string HouseId = "pirates", Bloc = "pirates";
    public const int SpawnLawlessness = 60, DissolveLawlessness = 30, RaidProsperity = 3, RaidLawlessness = 2, HuntLegitimacy = 1, HuntLawlessnessRelief = 10;

    public static bool IsPirate(string houseId) => houseId == HouseId;

    public static House Ensure(World w)
    {
        if (w.House(HouseId) is { } h) return h;
        h = new House { Id = HouseId, Name = "The Lawless", AccentColor = "#8a1c1c", Title = "landless", Legitimacy = 0, Culture = "reach", Bloc = Bloc };
        w.Houses[HouseId] = h; return h;
    }

    public void Tick(World w)
    {
        var g = w.Galaxy; if (g == null) return;
        if (w.House(HouseId) is { } lawless) lawless.Treasury = new Resources();   // the Lawless keep no ledger: hull upkeep never puts them in debt
        var regions = g.Regions.OrderBy(r => r.Id, StringComparer.Ordinal).ToList();
        foreach (var region in regions)
        {
            var state = RegionSystem.StateOf(w, region.Id); var systems = g.SystemsIn(region.Id).Select(s => s.Id).ToList();
            var raiders = w.Fleets.Values.Where(f => f.OwnerId == HouseId && (systems.Contains(f.SystemId) || systems.Contains(f.DestinationId))).ToList();
            if (raiders.Count == 0 && state.Lawlessness >= SpawnLawlessness)
            {
                Ensure(w);
                var at = systems.OrderBy(s => w.PlanetHolder(s) == null ? 0 : 1).ThenBy(s => w.Fleets.Values.Where(f => f.SystemId == s && f.OwnerId != HouseId).Sum(f => f.Strength)).ThenBy(s => s, StringComparer.Ordinal).First();
                int n = w.Counter("raider_fleets") + 1; w.WorldCounters["raider_fleets"] = n;
                w.Fleets[$"raiders_{n}"] = new Fleet { Id = $"raiders_{n}", Name = $"{region.Name.Replace("The ", "")} Raiders", OwnerId = HouseId, SystemId = at, Strength = state.Lawlessness >= 80 ? 2 : 1, Readiness = 90 };
                w.Log($"Raiders gather at {g.System(at)?.Name ?? at}: the lanes of {region.Name} have gone lawless, and someone is making a living off it.");
            }
            else if (raiders.Count > 0 && state.Lawlessness < DissolveLawlessness)
            {
                foreach (var f in raiders) { w.Fleets.Remove(f.Id); w.Intel.Remove(f.Id); }
                w.Log($"The raiders of {region.Name} melt away; the lanes are quiet again.");
            }
        }
        // raid and roam
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == HouseId && !f.Moving).OrderBy(f => f.Id, StringComparer.Ordinal).ToList())
        {
            var sys = g.System(f.SystemId); if (sys == null) continue;
            var world = w.Holdings.Values.Where(h => h.IsPlanet && h.SystemId == f.SystemId).OrderBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
            if (world != null && !w.Fleets.Values.Any(x => x.OwnerId == world.OwnerId && !x.Moving && x.SystemId == f.SystemId))
            {
                world.Prosperity = Math.Max(0, world.Prosperity - RaidProsperity);
                var rs = RegionSystem.StateOf(w, sys.RegionId); rs.Lawlessness = Math.Min(100, rs.Lawlessness + RaidLawlessness);
                w.WorldCounters["raids"] = w.Counter("raids") + 1;
                if (world.OwnerId == w.Protagonist?.HouseId) { w.WorldFlags.Add("raided"); w.Log($"{f.Name} plunder the shipping of {world.Name}."); }
            }
            // roam: a hash picks a neighbour; a neighbour with a stronger fleet is skipped; a raider that has just raided stays to raid again half the time
            uint hash = 2166136261; foreach (var ch in f.Id + w.Turn) hash = (hash ^ ch) * 16777619;
            if (world != null && hash % 2 == 0) continue;
            var options = g.Neighbors(f.SystemId).Where(n => g.RingOf(n) != Ring.Core && w.Fleets.Values.Where(x => x.OwnerId != HouseId && !x.Moving && x.SystemId == n).Sum(x => x.Strength) < f.Strength).ToList();
            if (options.Count == 0) continue;
            f.DestinationId = options[(int)(hash % (uint)options.Count)]; f.Eta = 1;
        }
    }

    /// <summary>After battles: a raider fleet broken to nothing is removed, the hunter rewarded, the region calmed.</summary>
    public static void AfterBattles(World w, IReadOnlyList<BattleReport> battles)
    {
        var g = w.Galaxy; if (g == null) return;
        foreach (var b in battles.Where(b => IsPirate(b.Attacker) || IsPirate(b.Defender)))
        {
            string hunter = IsPirate(b.Attacker) ? b.Defender : b.Attacker;
            if (b.Winner != hunter) continue;
            var house = w.House(hunter); if (house == null) continue;
            house.Legitimacy = Math.Min(100, house.Legitimacy + HuntLegitimacy);
            if (g.System(b.SystemId) is { } sys) { var rs = RegionSystem.StateOf(w, sys.RegionId); rs.Lawlessness = Math.Max(0, rs.Lawlessness - HuntLawlessnessRelief); }
            w.WorldCounters["pirates_hunted"] = w.Counter("pirates_hunted") + 1;
            if (hunter == w.Protagonist?.HouseId) w.WorldFlags.Add("pirate_hunter");
            w.Log($"{house.Name} scatters the raiders at {g.System(b.SystemId)?.Name ?? b.SystemId}; the lanes breathe easier.");
        }
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == HouseId && f.Strength <= 0).ToList()) { w.Fleets.Remove(f.Id); w.Intel.Remove(f.Id); }
    }

    /// <summary>Flags for content: <c>pirates_near</c> when raiders sit within a lane of a world of the player's.</summary>
    public static void WriteFlags(World w)
    {
        w.WorldFlags.Remove("pirates_near"); var g = w.Galaxy; var mine = w.Protagonist?.HouseId; if (g == null || mine == null) return;
        var near = w.HoldingsOf(mine).Where(h => h.SystemId.Length > 0).SelectMany(h => g.Neighbors(h.SystemId).Append(h.SystemId)).ToHashSet();
        if (w.Fleets.Values.Any(f => f.OwnerId == HouseId && near.Contains(f.SystemId))) w.WorldFlags.Add("pirates_near");
    }
}
