using Praetoria.Core.Creation;
using Praetoria.Core.Galaxy;
using Praetoria.Core.Rng;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The rings of the galaxy as behaviour (GDD §19): the core is commerce and the capital's order, the rim is lawless and
/// restless, the middle leans whichever way it is pushed. Each turn every region's <see cref="RegionState.Lawlessness"/>
/// and <see cref="RegionState.Allegiance"/> drift; a restless world in a disaffected region rises in <b>rebellion</b>
/// (it yields nothing); a rebellion left four turns breaks away as a <b>free hold</b> under a new house; a region's
/// dominant house in a region that has stopped believing in the capital may declare a <b>free march</b> and leave the
/// capital's order. A parked fleet keeps the peace and puts a rebellion down. RNG-free except where a new leader needs a
/// name, and there the seed is the turn and the world, so a run stays reproducible.
/// </summary>
public sealed class RegionSystem
{
    public const int AllegianceStep = 3, LawlessnessFleetRelief = 2, LawlessnessFleetCap = 4;
    public const int RebellionUnrest = 70, RebellionAllegiance = 35, RebellionEndsUnrest = 50, SecessionTurns = 4, GarrisonStrength = 2;
    public const int FreeMarchAllegiance = 20;
    public const string FreeholdsBloc = "freeholds";

    private readonly NameCatalog _names;
    public RegionSystem(NameCatalog? names = null) => _names = names ?? NameCatalog.Empty;

    public static RegionState StateOf(World w, string regionId)
    {
        if (w.Regions.TryGetValue(regionId, out var s)) return s;
        var region = w.Galaxy?.Region(regionId); var rules = w.Galaxy?.RulesFor(region?.Ring ?? Ring.Middle) ?? RingRules.Default(Ring.Middle);
        return w.Regions[regionId] = new RegionState { Allegiance = Math.Clamp(50 + rules.Allegiance, 0, 100), Lawlessness = rules.LawlessnessStart };
    }

    /// <summary>The house that keeps the court: whoever holds the capital's planet.</summary>
    public static House? CapitalHolder(World w) => w.Galaxy is { Capital.Length: > 0 } g && w.PlanetHolder(g.Capital) is { } id ? w.House(id) : null;

    /// <summary>Hulls parked (not in transit) in a region, any house.</summary>
    public static int ParkedHulls(World w, string regionId) =>
        w.Fleets.Values.Where(f => !f.Moving && w.Galaxy?.System(f.SystemId)?.RegionId == regionId).Sum(f => f.Strength);

    public int AllegianceTarget(World w, Region region)
    {
        var g = w.Galaxy!; var rules = g.RulesFor(region.Ring); var state = StateOf(w, region.Id);
        var capital = CapitalHolder(w);
        var worlds = w.Holdings.Values.Where(h => h.IsPlanet && g.System(h.SystemId)?.RegionId == region.Id).ToList();
        var populated = worlds.Where(h => h.Population > 0).ToList();
        int loyalty = populated.Count == 0 ? 50 : (int)populated.Average(h => 100 - h.Unrest);
        var dominant = worlds.GroupBy(h => h.OwnerId).OrderByDescending(x => x.Count()).ThenBy(x => x.Key, StringComparer.Ordinal).FirstOrDefault()?.Key;
        int t = 50 + rules.Allegiance + (capital != null ? (capital.Legitimacy - 40) / 4 : -15) + (loyalty - 50) / 4 - state.Lawlessness / 4;
        if (dominant != null && capital != null && w.AtWar(dominant, capital.Id)) t -= 20;
        if (dominant != null && w.House(dominant)?.Bloc == FreeholdsBloc) t -= 15;
        return Math.Clamp(t, 0, 100);
    }

    public void Tick(World w)
    {
        var g = w.Galaxy; if (g == null) return;
        var capital = CapitalHolder(w);
        foreach (var region in g.Regions.OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            var rules = g.RulesFor(region.Ring); var state = StateOf(w, region.Id);
            var systems = g.SystemsIn(region.Id).ToList();
            var worlds = w.Holdings.Values.Where(h => h.IsPlanet && systems.Any(s => s.Id == h.SystemId)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
            int unheld = systems.Count(s => !worlds.Any(h => h.SystemId == s.Id));
            int prosperity = worlds.Count == 0 ? 50 : (int)worlds.Average(h => h.Prosperity);

            // lawlessness: the ring's drift, less parked hulls, less a thriving economy, plus empty worlds and revolts
            int law = state.Lawlessness + rules.LawlessnessDrift - Math.Min(LawlessnessFleetCap, ParkedHulls(w, region.Id)) * LawlessnessFleetRelief - (prosperity - 50) / 10 + unheld * 2 + worlds.Count(h => h.InRebellion) * 3;
            state.Lawlessness = Math.Clamp(law, 0, 100);

            // allegiance drifts toward where the ring, the capital's standing and the worlds' loyalty put it
            int target = AllegianceTarget(w, region);
            state.Allegiance = Math.Clamp(state.Allegiance + Math.Clamp(target - state.Allegiance, -AllegianceStep, AllegianceStep), 0, 100);

            // rebellion on restless worlds in a disaffected region; a garrison or calm ends it; four turns and it is gone
            foreach (var h in worlds)
            {
                bool garrison = w.Fleets.Values.Any(f => f.OwnerId == h.OwnerId && !f.Moving && f.SystemId == h.SystemId && f.Strength >= GarrisonStrength);
                if (h.InRebellion)
                {
                    if (garrison) { h.Rebellion = 0; h.Unrest = Math.Max(0, h.Unrest - 15); w.Log($"The fleet at {h.Name} puts the rising down; the world is quiet, and remembers."); continue; }
                    if (h.Unrest < RebellionEndsUnrest) { h.Rebellion = 0; w.Log($"The rising on {h.Name} loses its heart and goes home."); continue; }
                    h.Rebellion++;
                    if (h.Rebellion > SecessionTurns) Secede(w, h, region);
                }
                else if (h.Unrest >= RebellionUnrest && state.Allegiance < RebellionAllegiance && !garrison && !w.Sieges.ContainsKey(h.Id) && h.Population > 0)
                {
                    h.Rebellion = 1; w.WorldCounters["rebellions"] = w.Counter("rebellions") + 1;
                    w.Log($"{h.Name} rises against {w.House(h.OwnerId)?.Name ?? "its lord"}: the tithe goes unpaid and the governor's writ stops at the palace gate.");
                }
            }

            // a free march: the dominant house of a rim or middle region that has stopped believing in the capital walks out of its order
            var dominantId = worlds.GroupBy(h => h.OwnerId).OrderByDescending(x => x.Count()).ThenBy(x => x.Key, StringComparer.Ordinal).FirstOrDefault()?.Key;
            var dominant = dominantId != null ? w.House(dominantId) : null;
            if (dominant != null && capital != null && dominant.Id != capital.Id && region.Ring != Ring.Core && state.Allegiance < FreeMarchAllegiance && dominant.Bloc.Length == 0 && dominant.Title != "emperor" && (dominant.Id != w.Protagonist?.HouseId || w.HasFlag("declare_free_march")))
                DeclareFreeMarch(w, dominant, region, capital);
        }
        w.WorldFlags.Remove("declare_free_march");
        PlayerDecrees(w);
        CapitalTick(w);
        WriteFlags(w);
    }

    /// <summary>The capital as a prize (GDD §19): its holder gains a point of legitimacy every third turn, and when it changes hands the
    /// chronicle marks it — <c>capital_taken</c> / <c>capital_lost</c> for the player's events, a counter for everyone.</summary>
    public static void CapitalTick(World w)
    {
        var g = w.Galaxy; if (g == null || g.Capital.Length == 0) return;
        var holder = CapitalHolder(w); string name = g.System(g.Capital)?.Name ?? g.Capital;
        var last = w.WorldFlags.FirstOrDefault(f => f.StartsWith("capital_holder:")); string? lastId = last?["capital_holder:".Length..];
        string? nowId = holder?.Id;
        if (lastId != nowId)
        {
            if (last != null) w.WorldFlags.Remove(last);
            if (nowId != null) w.WorldFlags.Add("capital_holder:" + nowId);
            if (lastId != null)   // the first tick only records who holds it
            {
                w.WorldCounters["capital_changes"] = w.Counter("capital_changes") + 1;
                var mine = w.Protagonist?.HouseId;
                if (nowId == mine) w.WorldFlags.Add("capital_taken"); if (lastId == mine) w.WorldFlags.Add("capital_lost");
                w.Log(nowId != null ? $"{name} changes hands: {holder!.Name} holds the capital{(w.House(lastId) is { } old ? $", and {old.Name} the memory of it" : "")}. The court moves."
                                    : $"{name} stands masterless; the court is dark.");
            }
        }
        if (holder != null && w.Turn > 0 && w.Turn % 3 == 0) holder.Legitimacy = Math.Min(100, holder.Legitimacy + 1);
    }

    /// <summary>A world four turns in rebellion breaks away under a new house: its governor, if of another house, else a leader
    /// raised from its own people (named from the culture's list, seeded by turn and world).</summary>
    public void Secede(World w, Holding h, Region region)
    {
        var old = w.House(h.OwnerId); var g = w.Galaxy!; var sys = g.System(h.SystemId)!;
        string culture = h.Culture.Length > 0 ? h.Culture : g.CultureOf(h.SystemId);
        var id = $"free_{sys.Id}"; int n = 1; while (w.Houses.ContainsKey(id)) id = $"free_{sys.Id}_{++n}";
        uint hash = 2166136261; foreach (var ch in id + w.Turn) hash = (hash ^ ch) * 16777619;
        var house = new House { Id = id, Name = $"The Free {sys.Name.Replace("The ", "")}", AccentColor = $"#{(hash & 0x7F) + 0x60:X2}{((hash >> 8) & 0x7F) + 0x40:X2}{((hash >> 16) & 0x7F) + 0x30:X2}", Title = "knight", Legitimacy = 12, Culture = culture, Bloc = FreeholdsBloc, Treasury = new Resources { Credits = 6, Materials = 4, Manpower = Math.Max(2, h.Population / 10) } };
        w.Houses[id] = house;
        Character leader; var governor = h.GovernorId.Length > 0 ? w.Char(h.GovernorId) : null;
        if (governor is { Alive: true } && governor.HouseId != h.OwnerId && governor.Id != w.ProtagonistId) { leader = governor; w.House(leader.HouseId)?.Members.Remove(leader.Id); leader.HouseId = id; }
        else
        {
            var rng = new SplitMix64Rng(hash ^ (ulong)w.Turn); string sex = (hash & 1) == 0 ? "female" : "male";
            string given = _names.IsEmpty ? "Scion" : _names.Pick(culture, sex, rng);
            leader = new Character { Id = $"{id}_leader", Name = $"{given} {sys.Name.Replace("The ", "")}", HouseId = id, Age = 28 + (int)(hash % 17), Sex = sex, CareerTrack = "military", CareerRank = 2, Ambition = "restore_house_fortunes" };
            leader.NatureTraits.Add("Proud"); leader.Skills["tactics"] = 2; leader.Skills["leadership"] = 2; leader.Skills["administration"] = 1;
            w.Characters[leader.Id] = leader; w.Pools[leader.Id] = ActionPools.ForNpc();
        }
        house.Members.Add(leader.Id);
        h.FormerOwnerId = h.OwnerId; h.OwnerId = id; h.GovernorId = leader.Id; h.Rebellion = 0; h.Unrest = 20; h.Prosperity = Math.Max(20, h.Prosperity - 10);
        // the free hold is an actor from its first day (GAP §6): a militia hull from its own people, and a steward who trades, stakes and sails like any other
        w.Fleets[$"militia_{id}"] = new Fleet { Id = $"militia_{id}", Name = $"{sys.Name.Replace("The ", "")} Militia", OwnerId = id, SystemId = sys.Id, Strength = 1, AdmiralId = leader.Id, Readiness = 80 };
        if (old != null)
        {
            old.Legitimacy = Math.Max(0, old.Legitimacy - 5);
            var head = Economy.HeadOf(w, old); if (head != null) w.Relationship(leader.Id, head.Id).Disposition = -60;
            if (old.Id == w.Protagonist?.HouseId) w.WorldFlags.Add("lost_a_world_to_rebellion");
        }
        w.WorldCounters["secessions"] = w.Counter("secessions") + 1; w.WorldFlags.Add("secession:" + sys.Id);
        w.Log($"{sys.Name} breaks from {old?.Name ?? "its lord"}. {leader.Name} is acclaimed in the square, and {house.Name} declare for no one.");
    }

    public static void DeclareFreeMarch(World w, House house, Region region, House capital)
    {
        house.Bloc = FreeholdsBloc; house.Legitimacy += 5; capital.Legitimacy = Math.Max(0, capital.Legitimacy - 5);
        var head = Economy.HeadOf(w, house); var crown = Economy.HeadOf(w, capital);
        if (head != null && crown != null) { w.Relationship(crown.Id, head.Id).Disposition = Math.Clamp(w.Relationship(crown.Id, head.Id).Disposition - 30, -100, 100); w.Relationship(head.Id, crown.Id).Disposition = Math.Clamp(w.Relationship(head.Id, crown.Id).Disposition - 20, -100, 100); }
        w.WorldCounters["free_marches"] = w.Counter("free_marches") + 1; w.WorldFlags.Add("free_march:" + region.Id);
        w.Log($"{house.Name} declares {region.Name} a Free March and renounces the court at {w.Galaxy!.System(w.Galaxy.Capital)?.Name ?? "the capital"}. {capital.Name} does not take it well.");
    }

    /// <summary>The player's decrees on a rising, set as flags by events: concede (ease the world, lose face) or purge (break it, be feared).</summary>
    private static void PlayerDecrees(World w)
    {
        var mine = w.Protagonist?.HouseId; if (mine == null) return;
        var house = w.House(mine); var rebels = w.HoldingsOf(mine).Where(h => h.InRebellion).ToList();
        if (w.WorldFlags.Remove("concede_to_rebels") && house != null) foreach (var h in rebels) { h.Rebellion = 0; h.Unrest = Math.Max(0, h.Unrest - 30); h.Prosperity = Math.Max(0, h.Prosperity - 10); house.Legitimacy = Math.Max(0, house.Legitimacy - 3); w.Log($"{house.Name} concedes the rising on {h.Name}: the tithe is forgiven and the ringleaders go home as councillors."); }
        if (w.WorldFlags.Remove("purge_rebels") && house != null) foreach (var h in rebels) { h.Rebellion = 0; h.Unrest = Math.Max(0, h.Unrest - 20); h.Population = Math.Max(0, h.Population - 3); house.Legitimacy = Math.Max(0, house.Legitimacy - 6); w.WorldCounters["threat"] = w.Counter("threat") + 3; w.Log($"{house.Name} breaks the rising on {h.Name}. The square is quiet. The Reach takes note."); }
    }

    /// <summary>Flags content reads: <c>holds_capital</c>, <c>rebellion</c>, <c>region_lawless</c>, <c>secession_tempting</c>, <c>free_march</c>, <c>ring:&lt;ring&gt;</c> (the ring of the seat).</summary>
    private static void WriteFlags(World w)
    {
        foreach (var f in new[] { "holds_capital", "rebellion", "region_lawless", "secession_tempting", "free_march" }) w.WorldFlags.Remove(f);
        foreach (var f in w.WorldFlags.Where(f => f.StartsWith("ring:")).ToList()) w.WorldFlags.Remove(f);
        var mine = w.Protagonist?.HouseId; var g = w.Galaxy; if (mine == null || g == null) return;
        var house = w.House(mine); if (house == null) return;
        if (CapitalHolder(w)?.Id == mine) w.WorldFlags.Add("holds_capital");
        var worlds = w.HoldingsOf(mine).Where(h => h.IsPlanet).ToList();
        if (worlds.Any(h => h.InRebellion)) w.WorldFlags.Add("rebellion");
        if (worlds.Any(h => StateOf(w, g.System(h.SystemId)?.RegionId ?? "").Lawlessness >= 60)) w.WorldFlags.Add("region_lawless");
        if (house.Bloc == FreeholdsBloc) w.WorldFlags.Add("free_march");
        w.WorldFlags.Remove("freehold_of_ours");
        if (w.Holdings.Values.Any(h => h.FormerOwnerId == mine && w.House(h.OwnerId)?.Bloc == FreeholdsBloc)) w.WorldFlags.Add("freehold_of_ours");
        var seat = worlds.OrderByDescending(h => h.Population).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
        if (seat != null) w.WorldFlags.Add("ring:" + g.RingOf(seat.SystemId));
        foreach (var region in g.Regions)
        {
            var rw = w.Holdings.Values.Where(h => h.IsPlanet && g.System(h.SystemId)?.RegionId == region.Id).ToList(); if (rw.Count == 0) continue;
            var dominant = rw.GroupBy(h => h.OwnerId).OrderByDescending(x => x.Count()).ThenBy(x => x.Key, StringComparer.Ordinal).First().Key;
            if (dominant == mine && region.Ring != Ring.Core && StateOf(w, region.Id).Allegiance < 30 && house.Bloc.Length == 0 && CapitalHolder(w)?.Id != mine) w.WorldFlags.Add("secession_tempting");
        }
    }
}
