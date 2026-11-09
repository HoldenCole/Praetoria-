using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Houses that fall (GDD §13 "names pass"; GAP §7): a house that has lost everything is not left as a debt frozen in the
/// ledger. Three endings, all for NPC houses only — the player's ending is the saga's (GDD §14):
/// <list type="bullet">
/// <item><b>Struck from the rolls</b> — no living member, no world, no venture, no hull: the name simply ends.</item>
/// <item><b>The free hold folds</b> — a free-hold house (RegionSystem.FreeholdsBloc) whose last world was taken has no reason to
/// exist; its people and militia pass to whoever took the world, as the buy-back already does.</item>
/// <item><b>Enters the service</b> — a landed name made landless, penniless and hull-less for <see cref="ServiceTurns"/> turns
/// enters the service of the richest house of its culture (or of the court): its characters become that house's retainers,
/// its debts are written off, and the chronicle marks the end of the line as an independent house.</item>
/// </list>
/// The counter <c>houses_fallen</c> counts them; <c>landless_since:&lt;house&gt;</c> marks when the wait began.
/// </summary>
public static class HouseSystem
{
    public const int ServiceTurns = 12;

    public static void Tick(World w)
    {
        var mine = w.Protagonist?.HouseId;
        foreach (var f in w.WorldFlags.Where(f => f.StartsWith("house_fell:")).ToList()) w.WorldFlags.Remove(f);   // last turn's news
        foreach (var house in w.Houses.Values.OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            if (house.Id == mine || house.Id == PirateSystem.HouseId) continue;
            bool landed = w.HoldingsOf(house.Id).Any(h => h.IsPlanet);
            string key = "landless_since:" + house.Id;
            if (landed) { w.WorldCounters.Remove(key); continue; }
            bool anything = w.HoldingsOf(house.Id).Any() || w.BusinessesOf(house.Id).Any() || w.Fleets.Values.Any(f => f.OwnerId == house.Id);
            bool living = w.Characters.Values.Any(c => c.Alive && c.HouseId == house.Id);
            if (!living && !anything) { Strike(w, house, $"The name of {house.Name} passes out of the rolls of the Reach; no one is left to carry it."); continue; }
            if (house.Bloc == RegionSystem.FreeholdsBloc)
            {
                var taker = w.Holdings.Values.Where(h => h.IsPlanet && h.FormerOwnerId == house.Id).Select(h => w.House(h.OwnerId)).FirstOrDefault(x => x != null && x.Id != house.Id && x.Id != PirateSystem.HouseId);
                if (taker != null) { Fold(w, house, taker, $"{house.Name} is no more: its people and its guard pass to {taker.Name}, who holds the world they rose for."); continue; }
            }
            if (!w.WorldCounters.ContainsKey(key)) { w.WorldCounters[key] = w.Turn; continue; }
            if (w.Turn - w.Counter(key) < ServiceTurns) continue;
            if (house.Treasury.Credits >= 0 || anything) continue;   // a landless name with coin, a venture or a hull still has a game to play
            var patron = w.Houses.Values.Where(x => x.Id != house.Id && x.Id != PirateSystem.HouseId && x.Bloc != RegionSystem.FreeholdsBloc && w.HoldingsOf(x.Id).Any(h => h.IsPlanet))
                .OrderByDescending(x => x.Culture == house.Culture ? 1 : 0).ThenByDescending(x => x.Treasury.Credits).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
            if (patron == null) continue;
            Fold(w, house, patron, $"{house.Name}, landless and without coin these {ServiceTurns} years, enters the service of {patron.Name}. Its debts are written off and its name is struck from the rolls of the independent houses.");
        }
    }

    /// <summary>Fold a house into another: characters, fleets (as levies) and ventures pass; a positive treasury passes, a negative one is forgiven.</summary>
    public static void Fold(World w, House house, House into, string line)
    {
        foreach (var c in w.Characters.Values.Where(c => c.HouseId == house.Id)) { c.HouseId = into.Id; if (!into.Members.Contains(c.Id)) into.Members.Add(c.Id); }
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == house.Id)) { f.OwnerId = into.Id; f.Name = f.Name.Replace("Militia", "Levy").Replace("Guard", "Levy"); }
        foreach (var b in w.Businesses.Values.Where(b => b.OwnerId == house.Id)) b.OwnerId = into.Id;
        foreach (var h in w.Holdings.Values.Where(h => h.OwnerId == house.Id)) { h.OwnerId = into.Id; h.FormerOwnerId = house.Id; }
        if (house.Treasury.Credits > 0) into.Treasury.Credits += house.Treasury.Credits;
        into.Treasury.Materials += Math.Max(0, house.Treasury.Materials); into.Treasury.Manpower += Math.Max(0, house.Treasury.Manpower);
        Strike(w, house, line);
    }

    private static void Strike(World w, House house, string line)
    {
        w.Wars.RemoveAll(x => x.Involves(house.Id));
        foreach (var f in w.Factions) f.Members.Remove(house.Id);
        if (w.Motions.RemoveAll(m => m.Open && m.ProposerHouseId == house.Id) > 0) w.Log($"The motion of {house.Name} lapses: there is no one left to speak to it.");   // a dead house's business leaves the docket
        foreach (var m in w.Motions.Where(m => m.Open && m.TargetId == house.Id).ToList()) { w.Motions.Remove(m); }
        if (w.Offices.RemoveAll(o => o.HouseId == house.Id) > 0) { var back = OfficeSystem.FleetsDefaultOwner(w); foreach (var f in w.Fleets.Values.Where(f => f.Office && f.OwnerId == house.Id)) f.OwnerId = back ?? f.OwnerId; }
        foreach (var h in w.Holdings.Values.Where(h => h.FormerOwnerId == house.Id)) h.FormerOwnerId = "";
        foreach (var k in w.WorldCounters.Keys.Where(k => k.EndsWith(":" + house.Id)).ToList()) w.WorldCounters.Remove(k);
        foreach (var f in w.WorldFlags.Where(f => f.EndsWith(":" + house.Id)).ToList()) w.WorldFlags.Remove(f);
        w.Houses.Remove(house.Id);
        w.WorldCounters["houses_fallen"] = w.Counter("houses_fallen") + 1; w.WorldFlags.Add("house_fell:" + house.Name);
        w.Log(line);
    }
}
