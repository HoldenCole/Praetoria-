using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>A nation on the map (docs/BEYOND.md, stages 1 and 2; the owner's word of 2026-11-18: "add in the khanate first"). A nation whose
/// worlds are on this map is given a house to hold them, its ruler and its commander become people of the Reach's world, and its strength
/// becomes real fleets. The Reach is at war with that house, every house of it, while the Senate's war with the nation lasts; fleets reach it
/// by the lanes and fight by the ordinary rules; what they win and lose moves the war's tally; and taking its capital ends the war. A
/// nation that is not on the map goes on as it did. RNG-free.</summary>
public sealed partial class NationSystem
{
    public const string RulerChar = "nation_ruler", CommanderChar = "nation_commander", ForeignChar = "foreign", OnMapFlag = "nation_on_the_map", CapitalTakenFlag = "nation_capital_taken", FallenFlag = "nation_fallen";
    public const string WorldsTakenCounter = "nation_worlds_taken", WorldsLostCounter = "reach_worlds_occupied", WorldsReturnedCounter = "reach_worlds_returned", WorldsCededCounter = "reach_worlds_ceded", MapBattlesCounter = "battles_with_nations", MapWinsCounter = "battles_won_against_nations", CapitalsCounter = "nation_capitals_taken", FallenCounter = "nations_fallen", HostsCounter = "nation_hosts_sailed";
    public const int ScorePerWorld = 3, HomeShare = 2, PurseFloor = 60, FuelFloor = 30, StoresFloor = 25, HostReach = 3, HostLeast = 3, HostRested = 60, HostSupplied = 70, HostSpent = 40, SiegeSlow = 2, ScoreSkirmish = 1;
    public const string FreedCounter = "reach_worlds_freed", RisenCounter = "steppe_worlds_risen", GivenUpCounter = "nation_worlds_given_up";
    public const int RiseYears = 3;
    public static string UnheldKey(string holdingId) => "unheld_years:" + holdingId;
    private static string SortieKey(Fleet f) => "host_goes_home:" + f.Id;
    /// <summary>What happened on the map this year, for the reports. Some of it is raised by the fleets before this system's own tick, so
    /// these are not cleared with <see cref="TurnFlags"/>: each is stamped with its year and struck out at the tick of the year after.</summary>
    public static readonly string[] MapFlags = { "nation_host_sails", "reach_world_occupied", "our_world_occupied", "world_freed_by_us", "world_freed_for_us", "reach_world_ceded", "steppe_world_restless", "steppe_world_lost", "steppe_world_rose", CapitalTakenFlag, FallenFlag, "host_beaten_by_us",
        "peace_negotiated", "peace_negotiated_by_us", "nation_client", "client_breaks", "reach_pays_tribute", "white_peace", "great_ruler_raised", "great_ruler_dead", "great_ruler_strikes", "world_sacked", "world_sacked_by_us", "private_war", "private_war_by_us", "peace_awaits_us", "peace_defied", "peace_defied_by_us", "peace_made",
        "nation_hires", "ransom_offered", "city_ransomed", "tribute_stopped", "world_plundered_by_us", "nation_bought_peace", "nation_strikes", "nation_comes_back", "world_yields_to_us", "nation_decays", "treasure_taken", "treasure_taken_by_us" };
    /// <summary>Raise a year's flag, and the same flag for the nation it is about ("flag:nation"), so a report can be written for one nation.</summary>
    public static void Mark(World w, string flag, Nation? n = null)
    {
        w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn;
        if (n != null) { w.WorldFlags.Add(flag + ":" + n.Id); w.WorldCounters["mark:" + flag + ":" + n.Id] = w.Turn; }
    }
    private static void ClearMarks(World w)
    {
        foreach (var f in w.WorldFlags.Where(f => MapFlags.Contains(f.Contains(':') ? f[..f.IndexOf(':')] : f) && w.Counter("mark:" + f) < w.Turn).ToList()) { w.WorldFlags.Remove(f); w.WorldCounters.Remove("mark:" + f); }
    }
    /// <summary>Count something once for the Reach and once for the nation it concerns ("key:nation"), so each nation on the map can be measured.</summary>
    private static void Bump(World w, string key, Nation n, int by = 1) { w.WorldCounters[key] = w.Counter(key) + by; w.WorldCounters[key + ":" + n.Id] = w.Counter(key + ":" + n.Id) + by; }
    public const string PlunderCounter = "plunder_from_nations", HiredCounter = "nation_hulls_hired", RansomedCounter = "nation_worlds_ransomed", TributeStoppedCounter = "tribute_stopped_early", WonCounter = "wars_won_on_the_map", LostOnMapCounter = "wars_lost_on_the_map", GutteredCounter = "wars_guttered_on_the_map", SentCounter = "fleets_sent_against_nations";
    public const int PlunderPerWorld = 2, RansomPerWealth = 4, UnwatchedYears = 3, RansomWaits = 2, BuysPeaceAfter = 4, BoughtPeaceTributes = 4;
    public const string BoughtPeaceCounter = "wars_bought_off";
    public static string RansomKey(string holdingId) => "ransom_offered:" + holdingId;
    private static string UnwatchedKey(Nation n) => "unwatched_years:" + n.Id;
    public static string UnwatchedKeyOf(Nation n) => UnwatchedKey(n);
    /// <summary>Whether a fleet of the Reach of three hulls or more stands at the nation's march or over one of its worlds.</summary>
    public static bool Watched(World w, NationDef d) { var own = Own(w, d); return Guard(w, d) != null || w.Fleets.Values.Any(f => !f.IsAway && !f.Moving && w.OfTheReach(f.OwnerId) && f.Strength >= MarchGuard && own.Contains(f.SystemId)); }
    private static string HiredKey(Nation n) => "hired_in_war:" + n.Id;
    /// <summary>A mercantile nation would rather buy than fight: it does not come through the march, it hires when attacked, it sues early and it buys its worlds back.</summary>
    public static bool Buys(NationDef d) => d.Temper == "mercantile";
    /// <summary>The lead at which the nation sues for peace: its threshold, or half of it for one that would rather buy than fight.</summary>
    public static int SuesAt(Nation n, NationDef d) => n.OnMap && Buys(d) ? (d.Threshold + 1) / 2 : d.Threshold;
    /// <summary>Whether its host comes through the march in a war: the aggressive always; any other only under a warlike ruler; and a
    /// nation whose definition says never, never.</summary>
    public static bool ComesOut(Nation n, NationDef d) => Great(n) || (d.Sorties != "never" && (d.Sorties == "always" || d.Temper == "aggressive" || n.Ruler?.Nature == "warlike"));   // (one of the great leads out even a nation that never leaves home)
    /// <summary>How a nation's fleets fight hull for hull, in hundredths: the same figures its temper gave it off the map.</summary>
    public static int MettleOf(NationDef d) => d.Mettle > 0 ? d.Mettle : d.Temper switch { "defensive" => 125, "mercantile" => 80, "decaying" => 85, _ => 100 };
    private static HashSet<string> Own(World w, NationDef d) => Worlds(w, d).Select(x => x.System).ToHashSet();

    /// <summary>A nation's own worlds in this game: those written for it and, where the game was begun on a larger map, a world on every
    /// system the map grew in its space (docs/GALAXY_SIZE.md). Nothing is stored: the list follows from the map.</summary>
    public static IReadOnlyList<NationWorld> Worlds(World w, NationDef d)
    {
        var g = w.Galaxy; if (g == null || w.GalaxyScale <= 100 || d.Worlds.Count == 0) return d.Worlds;
        var regions = d.Worlds.Select(x => g.System(x.System)?.RegionId ?? "").Where(r => g.Region(r)?.Ring == Praetoria.Core.Galaxy.Ring.Beyond).ToHashSet();
        if (regions.Count == 0) return d.Worlds;
        string hid = Nation.HouseIdFor(d.Id);
        var grown = g.Systems.Values.Where(s => s.Grown && regions.Contains(s.RegionId)).OrderBy(s => s.Id, StringComparer.Ordinal)
            .Select(s => new NationWorld($"{hid}_{s.Id}", s.Id, s.Name, s.PlanetClass switch { "terran" or "ocean" => "agri_world", "forge" => "forge_world", "gas" => "orbital_yard", "belt" => "mining_belt", "barren" => "fortress", _ => "frontier_colony" },
                14 + (int)(Hash("pop:" + s.Id) % 20), false)).ToList();
        return grown.Count == 0 ? d.Worlds : d.Worlds.Concat(grown).ToList();
    }

    public static bool OnMap(World w, string nationId) => w.Nation(nationId)?.OnMap == true;
    private static string GuardId(Nation n) => n.HouseId + "_guard";
    private static bool IsGuard(Nation n, Fleet f) => f.Id.StartsWith(GuardId(n), StringComparison.Ordinal);

    /// <summary>Put a nation on the map, if this map has its worlds and nobody holds them: a house, its worlds, its ruler and commander as
    /// people, and its strength as two fleets, the guard of the capital and the host nearest the march. Any fleet that was on its way to
    /// the nation the old way, off the map, is called home.</summary>
    public static void Place(World w, Nation n, NationDef d, string defaultClass = "frigate")
    {
        if (n.OnMap || n.Fallen || d.House == null || d.Worlds.Count == 0 || w.Galaxy == null) return;
        string hid = Nation.HouseIdFor(n.Id);
        if (w.Houses.ContainsKey(hid) || d.Worlds.Any(x => w.Galaxy.System(x.System) == null || w.Holdings.ContainsKey(x.Id) || w.PlanetHolder(x.System) != null)) return;
        People(w, n, d);
        var house = new House { Id = hid, Name = d.House.Name, AccentColor = d.House.Accent, Title = d.House.Title, Legitimacy = d.House.Legitimacy, Culture = d.Culture.Length > 0 ? d.Culture : "reach" };
        house.Treasury.Credits = PurseFloor + 20; house.Treasury.Helium3 = FuelFloor + 10; house.Treasury.Materials = StoresFloor; house.Treasury.Manpower = StoresFloor;
        w.Houses[house.Id] = house; n.HouseId = house.Id; n.Wealth = d.Wealth; n.Mettle = MettleOf(d); n.Walls = d.Walls; n.Treasure = d.Treasure; n.Titles = d.Titles; n.Regalia = d.Regalia;
        var capital = d.Worlds.FirstOrDefault(x => x.Capital) ?? d.Worlds[0]; n.CapitalSystemId = capital.System;
        var ruler = Embody(w, n, d, n.Ruler!, ruler: true); var commander = Embody(w, n, d, n.Commander!, ruler: false);
        foreach (var x in Worlds(w, d))
        {
            if (w.Holdings.ContainsKey(x.Id) || w.PlanetHolder(x.System) != null) continue;   // (only a system the map grew can be taken already; the written ones were checked above)
            w.Holdings[x.Id] = new Holding { Id = x.Id, OwnerId = house.Id, Name = x.Name, Specialization = x.Specialization, SystemId = x.System, Population = x.Population, GovernorId = ruler.Id, Culture = house.Culture };
        }
        var marches = Marches(w, d); string border = Border(w, n, d);
        int total = Math.Max(2, n.Strength), home = GuardShare(d, total), afield = Math.Max(0, total - home);
        Raise(w, GuardId(n), GuardName(d, capital.Name), house.Id, capital.System, home, ruler.Id, defaultClass);
        for (int i = 0; i < d.Hosts; i++)
        {   // the hulls in the field are shared among its hosts; the first, the largest, is the commander's
            int share = afield / d.Hosts + (i < afield % d.Hosts ? 1 : 0); if (share <= 0) continue;
            Raise(w, house.Id + "_host" + (i == 0 ? "" : "_" + (i + 1)), HostName(d, i), house.Id, border, share, i == 0 ? commander.Id : "", defaultClass);
        }
        foreach (var f in Expeditions(w, n.Id).ToList()) GoHome(w, f, d, "is called home");
        w.WorldFlags.Add(OnMapFlag);
        w.Log($"Beyond {string.Join(" and ", marches.Select(m => w.Galaxy.System(m)?.Name ?? m))} lie the worlds of {d.Name}: {string.Join(", ", d.Worlds.Select(x => w.Galaxy.System(x.System)?.Name ?? x.System))}{(Worlds(w, d).Count > d.Worlds.Count ? $" and {Worlds(w, d).Count - d.Worlds.Count} more" : "")}. {Styled(d, n.Ruler, true)} sits at {w.Galaxy.System(capital.System)?.Name}.");
    }

    private static string GuardName(NationDef d, string capital) => d.GuardName.Length > 0 ? d.GuardName : $"The Guard of {capital.Replace("The ", "")}";
    private static string HostName(NationDef d, int i = 0) => (d.HostName.Length > 0 ? d.HostName : $"The Host of {d.Name.Replace("The ", "")}") + (d.Hosts > 1 ? i switch { 0 => ", First Division", 1 => ", Second Division", _ => ", Third Division" } : "");
    /// <summary>How many of its hulls stay with the ruler: half for a nation with one host, a third for one with two.</summary>
    public static int GuardShare(NationDef d, int total) => Math.Max(2, total / (d.Hosts + 1));

    /// <summary>The nation's world nearest its march that it still holds: where its host stands at peace. Its capital if it holds nothing else.</summary>
    public static string Border(World w, Nation n, NationDef d)
    {
        var marches = Marches(w, d);
        int Far(string sys) => marches.Select(m => w.Galaxy!.Hops(m, sys)).Where(h => h >= 0).DefaultIfEmpty(99).Min();
        var own = Own(w, d);
        return w.HoldingsOf(n.HouseId).Where(h => h.IsPlanet && own.Contains(h.SystemId)).Select(h => h.SystemId).Distinct()
            .OrderBy(Far).ThenBy(x => x, StringComparer.Ordinal).FirstOrDefault() ?? n.CapitalSystemId;
    }

    private static void Raise(World w, string id, string name, string houseId, string systemId, int hulls, string admiralId, string defaultClass)
    {
        var f = new Fleet { Id = id, Name = name, OwnerId = houseId, SystemId = systemId, AdmiralId = admiralId, Readiness = 90, Loyalty = 85, Supply = 100 };
        f.Ships[defaultClass] = hulls; f.Recount(); w.Fleets[id] = f;
    }

    /// <summary>A person of the nation becomes a person of the Reach's world: someone who can command a fleet, be met in battle, and be killed.</summary>
    private static Character Embody(World w, Nation n, NationDef d, NationPerson p, bool ruler)
    {
        string id = $"{n.HouseId}_{(ruler ? "ruler" : "commander")}_{n.NamesUsed}_{w.Turn}"; int k = 0; while (w.Characters.ContainsKey(id)) id = $"{n.HouseId}_{(ruler ? "ruler" : "commander")}_{n.NamesUsed}_{w.Turn}_{++k}";
        var c = new Character { Id = id, Name = p.Name, HouseId = n.HouseId, Age = p.Age, Sex = Hash("sex:" + p.Name) / 7 % 2 == 0 ? "male" : "female", CareerTrack = "military", CareerRank = ruler ? 4 : 3 };
        c.Skills["tactics"] = ruler ? 3 : Math.Clamp(p.Skill, 1, 9); c.Skills["gunnery"] = ruler ? 2 : Math.Max(1, p.Skill - 2); c.Skills["leadership"] = 3;
        c.NatureTraits.Add(ruler ? p.Nature switch { "warlike" => "Ruthless", "grasping" => "Ambitious", "mild" => "Honorable", _ => "Just" } : "Proud");
        c.Flags.Add(ForeignChar); c.Flags.Add(ruler ? RulerChar : CommanderChar);
        w.Characters[id] = c; w.Pools[id] = ActionPools.ForNpc(); w.House(n.HouseId)?.Members.Add(id); p.CharacterId = id;
        return c;
    }

    /// <summary>Once a year for a nation on the map: its people and the persons who stand for them are kept in step (a ruler or commander
    /// killed on the map is dead, and is followed); its fleets are led by them; its strength is what its fleets hold, and grows back at
    /// home; its house does not go hungry; its host is sent where it should be; a nation whose capital is taken while the war lasts is
    /// beaten, and one with no world left is no more.</summary>
    private void Keep(World w, Nation n, NationDef d, bool afterTheWar = false)
    {
        if (!n.OnMap || n.Fallen) return;
        var house = w.House(n.HouseId); if (house == null) { n.HouseId = ""; return; }
        People(w, n, d); n.Wealth = d.Wealth; n.Mettle = MettleOf(d); n.Walls = d.Walls;
        if (!w.HoldingsOf(house.Id).Any(h => h.IsPlanet)) { Fall(w, n, d); return; }
        // the ruler: dead on the map is dead; a new one needs a person
        if (n.Ruler!.CharacterId.Length > 0 && w.Char(n.Ruler.CharacterId) is not { Alive: true }) Succeed(w, n, d, killed: true);
        if (n.Ruler!.CharacterId.Length == 0)
        {
            var general = w.Foreigners().FirstOrDefault(c => c.HouseId == house.Id && c.Name == n.Ruler.Name);   // the commander who took the chair is the same person
            foreach (var old in w.Foreigners().Where(c => c.HouseId == house.Id && c.Flags.Contains(RulerChar) && c != general).ToList()) { old.Flags.Remove(RulerChar); old.Alive = false; }
            if (general != null) { general.Flags.Remove(CommanderChar); general.Flags.Add(RulerChar); general.CareerRank = 4; n.Ruler.CharacterId = general.Id; }
            else Embody(w, n, d, n.Ruler, ruler: true);
        }
        if (n.Commander!.CharacterId.Length > 0 && w.Char(n.Commander.CharacterId) is not { Alive: true })
        {
            Bump(w, CommandersFallenCounter, n);
            w.Log($"{Styled(d, n.Commander, false)} of {d.Name} is dead. Someone new, and greener, has the command.");
            n.Commander = NewCommander(w, n, d);
        }
        if (n.Commander!.CharacterId.Length == 0)
        {
            foreach (var old in w.Foreigners().Where(c => c.HouseId == house.Id && c.Flags.Contains(CommanderChar)).ToList()) { old.Flags.Remove(CommanderChar); old.Alive = false; }
            Embody(w, n, d, n.Commander, ruler: false);
        }
        foreach (var (p, c) in new[] { (n.Ruler, w.Char(n.Ruler.CharacterId)), (n.Commander, w.Char(n.Commander.CharacterId)) }) if (c != null) c.Age = p.Age;
        foreach (var hd in w.HoldingsOf(house.Id)) hd.GovernorId = n.Ruler.CharacterId;

        // a world of its own that a house of the Reach holds does not stay held without a fleet over it: easy to take, impossible to hold.
        // While the nation pays tribute its people keep quiet, and while a ransom is on offer; after that, so many years without a garrison and they rise
        if (!afterTheWar) foreach (var x in Worlds(w, d))
        {
            if (w.Holding(x.Id) is not { } hw || hw.OwnerId == house.Id || !w.OfTheReach(hw.OwnerId)) { w.WorldCounters.Remove(UnheldKey(x.Id)); continue; }
            if (d.HoldYears <= 0 || n.Subdued(w.Turn) || w.Counter(RansomKey(x.Id)) > 0 || FleetSystem.StrengthAt(w, hw.OwnerId, hw.SystemId) >= MarchGuard) { w.WorldCounters.Remove(UnheldKey(x.Id)); continue; }   // a state's worlds do not rise of themselves: it comes back for them
            int years = w.Counter(UnheldKey(x.Id)) + 1; w.WorldCounters[UnheldKey(x.Id)] = years;
            if (years < d.HoldYears) { if (hw.OwnerId == w.Protagonist?.HouseId) Mark(w, "steppe_world_restless", n); continue; }
            string was = hw.OwnerId; hw.OwnerId = house.Id; hw.FormerOwnerId = ""; hw.GovernorId = n.Ruler.CharacterId; hw.Unrest = Math.Max(0, hw.Unrest - 20); w.WorldCounters.Remove(UnheldKey(x.Id)); w.Sieges.Remove(hw.Id);
            Bump(w, RisenCounter, n); Mark(w, "steppe_world_rose", n); if (was == w.Protagonist?.HouseId) Mark(w, "steppe_world_lost", n);
            w.Log($"{hw.Name} rises: no fleet of {w.House(was)?.Name ?? was} has stood over it in {d.HoldYears} years, and it is {d.Name}'s again.");
        }
        if (!afterTheWar) Dealings(w, n, d);
        // what its worlds give a house of the Reach that holds them (the Compact's isotope fields), a year
        if (!afterTheWar && d.Yields.Count > 0) foreach (var x in Worlds(w, d))
        {
            if (w.Holding(x.Id) is not { } hw || hw.OwnerId == house.Id || !w.OfTheReach(hw.OwnerId) || w.House(hw.OwnerId) is not { } holder) { w.WorldCounters.Remove(YieldSinceKey(x.Id)); continue; }
            foreach (var (good, units) in d.Yields.OrderBy(k => k.Key, StringComparer.Ordinal)) { holder.Treasury.Add(good, units); Bump(w, YieldCounter, n, units); }
            if (w.Counter(YieldSinceKey(x.Id)) == 0)
            {
                w.WorldCounters[YieldSinceKey(x.Id)] = Math.Max(1, w.Turn); if (holder.Id == w.Protagonist?.HouseId) Mark(w, "world_yields_to_us", n);
                w.Log($"{hw.Name} works for {holder.Name} now: {string.Join(", ", d.Yields.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{Money.Of(k.Key, k.Value)} {k.Key}"))} a year, for as long as it is held.");
            }
        }
        // an empire in decay strikes a hull from its lists every ten years, down to a remnant
        if (!afterTheWar && d.Temper == "decaying" && !Great(n) && w.Turn % (RegenEvery * 2) == 0)
        {
            var largest = w.Fleets.Values.Where(f => f.OwnerId == house.Id).OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
            if (largest != null && w.Fleets.Values.Where(f => f.OwnerId == house.Id).Sum(f => f.Strength) > DecayFloor)
            {
                FleetSystem.ShedHull(w, largest); Bump(w, DecayedCounter, n); Mark(w, "nation_decays", n);
                w.Log($"{d.Name} strikes another hull from its lists. It was not lost in battle: there was nobody to pay for it.");
            }
        }

        // the capital: lost while the war lasts, the war is lost; lost and the war over, the ruler sits somewhere else
        if (w.PlanetHolder(n.CapitalSystemId) != house.Id)
        {
            if (n.AtWar) n.ScoreReach = Math.Max(n.ScoreReach, n.ScoreNation + d.Threshold);
            else
            {
                var seat = w.HoldingsOf(house.Id).Where(h => h.IsPlanet && h.SystemId.Length > 0).OrderByDescending(h => h.Population).ThenBy(h => h.Id, StringComparer.Ordinal).First();
                n.CapitalSystemId = seat.SystemId; w.Log($"{Styled(d, n.Ruler, true)} of {d.Name} sits at {seat.Name} now.");
            }
        }

        var fleets = w.Fleets.Values.Where(f => f.OwnerId == house.Id).OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).ToList();
        var guard = fleets.FirstOrDefault(f => IsGuard(n, f)); var hosts = fleets.Where(f => !IsGuard(n, f)).ToList(); var host = hosts.FirstOrDefault();   // the largest host is the commander's
        foreach (var f in fleets) f.AdmiralId = f == guard ? n.Ruler.CharacterId : f == host ? n.Commander.CharacterId : "";
        // strength is hulls, and it grows back at home while the nation holds its capital
        int hulls = fleets.Sum(f => f.Strength);
        bool hires = Buys(d) && n.AtWar;   // attacked, it pays for hulls: one a year while the war lasts
        bool great = Great(n); int cap = Cap(n, d);   // one of the great musters every year, half as many hulls again, and an empire in decay stops decaying
        if (!afterTheWar && w.PlanetHolder(n.CapitalSystemId) == house.Id && (hires || great || w.Turn % RegenEvery == 0) && hulls < cap && (d.Temper != "decaying" || great))
        {
            int add = hires ? 1 : Math.Min(Math.Max(1, d.Regen), cap - hulls); string cls = _fleets.Rules.DefaultClass;
            if (hires) { Bump(w, HiredCounter, n); if (w.Counter(HiredKey(n)) != n.WarSince + 1) { w.WorldCounters[HiredKey(n)] = n.WarSince + 1; Mark(w, "nation_hires", n); w.Log($"{d.Name} is hiring: sellswords' hulls are coming into its harbours, and will for as long as the war lasts."); } }
            var refit = hosts.Where(f => !f.Moving && w.PlanetHolder(f.SystemId) == house.Id).OrderBy(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();   // the weakest host that is at home
            var to = guard == null ? null : refit != null && guard.Strength >= GuardShare(d, cap) ? refit : guard;
            if (to == null) Raise(w, GuardId(n), GuardName(d, w.Galaxy?.System(n.CapitalSystemId)?.Name ?? "the capital"), house.Id, n.CapitalSystemId, add, n.Ruler.CharacterId, cls);
            else { if (to.Ships.Count == 0) to.Ships[cls] = to.Strength; to.Ships[cls] = to.Ships.GetValueOrDefault(cls) + add; to.Recount(); }
            if (hosts.Count < d.Hosts && guard != null && guard.Strength > GuardShare(d, cap) + HostLeast)
            {   // a host was lost: when the guard has hulls to spare, a new one is raised from it
                int give = guard.Strength - GuardShare(d, cap); if (guard.Ships.Count == 0) guard.Ships[cls] = guard.Strength;
                guard.Ships[cls] = Math.Max(0, guard.Ships.GetValueOrDefault(cls) - give); guard.Recount();
                Raise(w, $"{house.Id}_host_{w.Turn}", HostName(d, hosts.Count), house.Id, guard.SystemId, give, hosts.Count == 0 ? n.Commander.CharacterId : "", cls);
            }
            hulls += add;
        }
        n.Strength = hulls;
        house.Treasury.Credits = Math.Max(house.Treasury.Credits, PurseFloor); house.Treasury.Helium3 = Math.Max(house.Treasury.Helium3, FuelFloor);
        house.Treasury.Materials = Math.Max(house.Treasury.Materials, StoresFloor); house.Treasury.Manpower = Math.Max(house.Treasury.Manpower, StoresFloor);

        // where its fleets should be
        var bound = new HashSet<string>();   // where its other hosts already are or are going, so two hosts do not make for the same world
        if (!afterTheWar) foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.Moving).OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).ToList())
        {
            // a nation that never leaves home turns out everything it has when it is invaded: its guard meets an intruder in its own space
            // beside the militia, and goes back to the capital when there is none
            bool turnsOut = IsGuard(n, f) && d.Sorties == "never" && n.AtWar && !Great(n);
            string? to = turnsOut ? HostOrder(w, n, d, f, null, n.CapitalSystemId) : IsGuard(n, f) ? (f.SystemId == n.CapitalSystemId ? null : n.CapitalSystemId) : HostOrder(w, n, d, f, bound);
            if (!IsGuard(n, f)) bound.Add(to ?? f.SystemId);
            if (to == null || to == f.SystemId) continue;
            int eta = w.Galaxy!.Hops(f.SystemId, to, Commands.MoveFleetCommand.Barred(w, house.Id)); if (eta <= 0) continue;
            f.DestinationId = to; f.Eta = eta; f.Voyage = eta;
            if (!IsGuard(n, f) && !Own(w, d).Contains(to))
            {
                Bump(w, HostsCounter, n); Mark(w, "nation_host_sails", n);
                w.Log($"{f.Name} is through the march and bound for {w.Galaxy.System(to)?.Name ?? to}: {f.Strength} hulls{(f.AdmiralId == n.Commander.CharacterId ? $" under {Styled(d, n.Commander, false)}" : "")}.");
            }
        }
    }

    public const string StruckCounter = "wars_begun_by_nations", RevancheCounter = "wars_begun_for_a_world", YieldCounter = "yield_from_nations", DecayedCounter = "nation_hulls_decayed", TreasureCounter = "nation_treasures_taken";
    public const int DecayFloor = 4;
    private static string YieldSinceKey(string holdingId) => "yields_since:" + holdingId;
    public const int StrikeRest = 25, StrikeOneIn = 3, StrikeFit = 75, StrikeRegard = -10;
    /// <summary>What divides the Reach, if anything does: the great war, a civil war, a state in arms against it, or a standard raised against a throne.</summary>
    public static string? Divided(World w) =>
        GreatCrisisSystem.AtWar(w) ? "the great war" : w.CivilWar != null ? "the civil war" : w.Polities.Any(p => p.Recognised == 0) ? "a state in arms against the Reach" : w.Rising != null ? "a standard raised against the throne" : null;

    /// <summary>A nation that begins wars itself (docs/BEYOND.md, the Ardent Concord). It strikes when the Reach is divided and it has no
    /// love for it; or to take back a world of its own that a house of the Reach holds. Never while it pays tribute, is short of its
    /// strength, has fought within twenty-five years (one invasion a generation), or another war beyond the Reach is being fought. One year in three when it may.</summary>
    private void Strike(World w, Nation n, NationDef d)
    {
        if (!Begins(n, d) || !n.OnMap || n.Fallen || n.AtWar || n.Subdued(w.Turn) || n.PaidUntil > w.Turn || AtWar(w) != null) return;
        bool great = Great(n);   // one of the great needs no quarrel in the Reach: three years to gather the host, eight between wars
        if (n.LastWarEnd > 0 && w.Turn - n.LastWarEnd < ByBoldness(n, great ? GreatRest : StrikeRest)) return;   // the bold rest less between their wars
        if (great && w.Turn - n.Ruler!.GreatSince < GreatGathers) return;
        if (n.Strength * 100 < Cap(n, d) * StrikeFit) return;
        var lost = Worlds(w, d).Select(x => w.Holding(x.Id)).FirstOrDefault(h => h != null && h.OwnerId != n.HouseId && w.OfTheReach(h.OwnerId));
        string? why = lost != null ? $"to take back {lost.Name}" : great ? "to take what it can hold" : n.Disposition <= StrikeRegard && Divided(w) is { } rift ? $"while the Reach is divided by {rift}" : null;
        if (why == null || Hash($"strike:{n.Salt}:{n.Id}:{w.Turn}") / 7 % (uint)(great ? 2 : StrikeOneIn) != 0) return;
        n.AtWar = true; n.BeganIt = true; n.WarOf = ""; n.Defied = false; n.PeaceVoted = 0; n.WarSince = w.Turn; n.ScoreReach = 0; n.ScoreNation = 0; n.LastBattleTurn = w.Turn; n.Wars += 1; n.Disposition = Math.Max(-100, n.Disposition - 10);
        w.WorldCounters[WarsCounter] = w.Counter(WarsCounter) + 1; Bump(w, StruckCounter, n); if (lost != null) Bump(w, RevancheCounter, n);
        w.WorldFlags.Add("foreign_war"); w.WorldFlags.Add("foreign_war:" + n.Id); Mark(w, lost != null ? "nation_comes_back" : "nation_strikes", n); if (great) Mark(w, "great_ruler_strikes", n);
        w.Log($"{d.Name} makes war on the Reach, {why}. {Styled(d, n.Ruler, true)} has given the order, and its fleets are at {w.Galaxy?.System(Border(w, n, d))?.Name ?? "its border"}.");
    }

    /// <summary>The dealings of a nation that would rather buy than fight (docs/BEYOND.md, the Free Cities): the player's answer to a
    /// ransom offered for a world of the nation's that the player's house holds; and tribute that stops when the Reach's fleets go home.</summary>
    private void Dealings(World w, Nation n, NationDef d)
    {
        var mine = w.Protagonist?.HouseId;
        foreach (var x in Worlds(w, d))
        {
            int since = w.Counter(RansomKey(x.Id)); if (since == 0) continue;
            var hw = w.Holding(x.Id);
            if (hw == null || hw.OwnerId != mine || mine == null) { w.WorldCounters.Remove(RansomKey(x.Id)); continue; }
            if (w.HasFlag("ransom_answer_take")) { Ransom(w, n, d, hw); w.WorldCounters.Remove(RansomKey(x.Id)); }
            else if (w.HasFlag("ransom_answer_keep")) { w.WorldCounters.Remove(RansomKey(x.Id)); n.Disposition = Math.Max(-100, n.Disposition - 10); w.Log($"{w.House(mine)?.Name} will not sell {hw.Name} back. {d.Name} will remember the price that was refused."); }
            else if (w.Turn - since >= RansomWaits) { w.WorldCounters.Remove(RansomKey(x.Id)); w.Log($"{d.Name} withdraws its offer for {hw.Name}: {RansomWaits} years, and no answer."); }
            else Mark(w, "ransom_offered", n);   // still on the table
        }
        // quick to pay tribute, and quicker to stop when the fleets go home
        if (!Buys(d) || !n.Subdued(w.Turn)) { w.WorldCounters.Remove(UnwatchedKey(n)); return; }
        if (Watched(w, d)) { w.WorldCounters.Remove(UnwatchedKey(n)); return; }
        int years = w.Counter(UnwatchedKey(n)) + 1; w.WorldCounters[UnwatchedKey(n)] = years;
        if (years < UnwatchedYears) return;
        n.SubduedUntil = w.Turn; w.WorldCounters.Remove(UnwatchedKey(n)); Bump(w, TributeStoppedCounter, n); Mark(w, "tribute_stopped", n);
        w.Log($"{d.Name} stops paying: no fleet of the Reach has stood at its march or over its worlds in {UnwatchedYears} years, and it has counted them.");
    }

    /// <summary>A world of the nation's own, held by a house of the Reach, is bought back: the house is paid and the world goes home.</summary>
    private static void Ransom(World w, Nation n, NationDef d, Holding hw)
    {
        int sum = RansomPerWealth * d.Wealth; string was = hw.OwnerId;
        if (w.House(was) is { } holder) holder.Treasury.Credits += sum;
        hw.OwnerId = n.HouseId; hw.FormerOwnerId = ""; hw.GovernorId = n.Ruler?.CharacterId ?? ""; w.Sieges.Remove(hw.Id); w.WorldCounters.Remove(UnheldKey(hw.Id));
        Bump(w, RansomedCounter, n); Mark(w, "city_ransomed", n);
        w.Log($"{d.Name} buys {hw.Name} back from {w.House(was)?.Name ?? was} for {Money.Credits(sum)}.");
    }

    /// <summary>What a victory over a nation on the map pays the house that won it: the nation's wealth for a fleet beaten, twice that for a world taken.</summary>
    private static void Plunder(World w, string houseId, Nation n, int credits, string what)
    {
        if (credits <= 0 || w.House(houseId) is not { } h) return;
        h.Treasury.Credits += credits; Bump(w, PlunderCounter, n, credits);
        w.Log($"{h.Name} takes {Money.Credits(credits)} of plunder {what}.");
    }

    /// <summary>The nation's last world is taken: it is no more. What it took from the Reach goes back; its fleets scatter; its people are gone.</summary>
    private static void Fall(World w, Nation n, NationDef d)
    {
        if (n.AtWar) { PeaceOnMap(w, n, d, reachWon: true); EndWar(w, n, d); }
        n.Fallen = true; n.Strength = 0; n.SubduedUntil = 0;
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == n.HouseId).ToList()) w.Fleets.Remove(f.Id);
        foreach (var c in w.Foreigners().Where(c => c.HouseId == n.HouseId).ToList()) c.Alive = false;
        Bump(w, FallenCounter, n); Mark(w, FallenFlag, n);
        w.Log($"{d.Name} is no more: its last world is held from the Reach, its fleets are scattered, and nobody answers to the name of {d.RulerTitle}.");
    }

    /// <summary>A battle on the map between a house of the Reach and a nation's: it counts in the war's tally as a battle in the nation's own
    /// space did, and toward whose triumph it will be.</summary>
    public static void BattleOnMap(World w, string winner, string loser)
    {
        var lost = w.NationOfHouse(loser); var won = w.NationOfHouse(winner);
        if (lost is { AtWar: true } && w.OfTheReach(winner))
        {
            lost.ScoreReach += ScorePerBattle; lost.LastBattleTurn = w.Turn; w.WorldCounters[WinsKey(winner, lost.Id)] = w.Counter(WinsKey(winner, lost.Id)) + 1;
            Bump(w, MapBattlesCounter, lost); Bump(w, MapWinsCounter, lost); w.WorldCounters[BattlesCounter] = w.Counter(BattlesCounter) + 1;
            Plunder(w, winner, lost, lost.Wealth, "from the wreck of their fleet");
            if (lost.Commander != null) lost.Commander.Defeats++;
            if (winner == w.Protagonist?.HouseId) Mark(w, "host_beaten_by_us", lost);
        }
        else if (won is { AtWar: true } && w.OfTheReach(loser))
        {
            won.ScoreNation += ScoreSkirmish; won.LastBattleTurn = w.Turn;   // a squadron swatted counts for less than a host beaten
            Bump(w, MapBattlesCounter, won); w.WorldCounters[BattlesCounter] = w.Counter(BattlesCounter) + 1;
            if (won.Commander != null) { won.Commander.Victories++; won.Commander.Skill = Math.Min(9, won.Commander.Skill + 1); if (w.Char(won.Commander.CharacterId) is { } c) c.Skills["tactics"] = Math.Max(c.Skill("tactics"), won.Commander.Skill); }
        }
    }

    /// <summary>A world changes hands between the Reach and a nation. A world of theirs taken counts three in the tally (their capital
    /// ends the war: see Keep). A world of the Reach's that they take counts three the other way, is theirs until it is freed or the
    /// peace, and sends their host home with what it took. A world freed counts three for the Reach, and for the house that freed it.</summary>
    public static void WorldTaken(World w, Holding h, string conqueror, string old, string? freedFor)
    {
        if (w.NationOfHouse(old) is { } lost && w.OfTheReach(conqueror))
        {
            if (freedFor != null) { if (h.IsPlanet) Bump(w, FreedCounter, lost); if (w.House(conqueror) is { } liberator && freedFor != conqueror) liberator.Legitimacy = Math.Min(100, liberator.Legitimacy + 1); }
            else if (h.IsPlanet)
            {
                Bump(w, WorldsTakenCounter, lost);
                if (h.SystemId == lost.CapitalSystemId)
                {
                    Bump(w, CapitalsCounter, lost); Mark(w, CapitalTakenFlag, lost);
                    if ((lost.Treasure > 0 || lost.Titles > 0) && w.House(conqueror) is { } taker)
                    {   // what was in its vaults, and the name of having taken them: once, for whoever is first
                        taker.Treasury.Credits += lost.Treasure; taker.Legitimacy = Math.Min(100, taker.Legitimacy + lost.Titles); Bump(w, TreasureCounter, lost, Math.Max(1, lost.Treasure));
                        Mark(w, conqueror == w.Protagonist?.HouseId ? "treasure_taken_by_us" : "treasure_taken", lost);
                        var crowned = WealthSystem.TakeRegalia(w, taker, lost.Regalia, lost.Id);
                        w.Log($"{taker.Name} opens the vaults under {h.Name}: {Money.Credits(lost.Treasure)}{(crowned != null ? $", and {crowned.Name} carried home through the Reach" : "")}.");
                        lost.Treasure = 0; lost.Titles = 0; lost.Regalia = "";
                    }
                }
                Plunder(w, conqueror, lost, PlunderPerWorld * lost.Wealth, $"out of {h.Name}"); if (conqueror == w.Protagonist?.HouseId) Mark(w, "world_plundered_by_us", lost);
            }
            if (!lost.AtWar || !h.IsPlanet) return;
            lost.ScoreReach += ScorePerWorld; lost.LastBattleTurn = w.Turn; w.WorldCounters[WinsKey(conqueror, lost.Id)] = w.Counter(WinsKey(conqueror, lost.Id)) + 2;
        }
        else if (h.IsPlanet && w.NationOfHouse(conqueror) is { AtWar: true } took && w.OfTheReach(old))
        {
            took.ScoreNation += ScorePerWorld; took.LastBattleTurn = w.Turn; Bump(w, WorldsLostCounter, took); Mark(w, old == w.Protagonist?.HouseId ? "our_world_occupied" : "reach_world_occupied", took);
            foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == conqueror && !f.Moving && f.SystemId == h.SystemId)) w.WorldCounters[SortieKey(f)] = 1;   // one world a season: the host that took it goes home with it (unless it campaigns)
        }
    }

    /// <summary>The war with a nation on the map is over. If the Reach won, the worlds the nation took go back to the houses they were taken
    /// from. If the nation won, it keeps them. What the Reach took of the nation's it keeps either way. The nation's fleets go home.</summary>
    /// <param name="allBack">Everything goes home, both ways: a white peace, or a client left what is its own.</param>
    private static void PeaceOnMap(World w, Nation n, NationDef d, bool reachWon, bool allBack = false)
    {
        if (!n.OnMap) return;
        foreach (var h in w.Holdings.Values.Where(h => h.OwnerId == n.HouseId && h.FormerOwnerId.Length > 0 && w.OfTheReach(h.FormerOwnerId) && !Worlds(w, d).Any(x => x.Id == h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            if (!reachWon && !allBack) { w.Log($"{h.Name} is ceded to {d.Name} at the peace."); h.FormerOwnerId = ""; if (h.IsPlanet) { Bump(w, WorldsCededCounter, n); Mark(w, "reach_world_ceded", n); } continue; }
            string back = h.FormerOwnerId; h.OwnerId = back; h.FormerOwnerId = ""; h.Unrest = Math.Min(100, h.Unrest + 5);
            if (h.IsPlanet) Bump(w, WorldsReturnedCounter, n); w.Log($"{h.Name} is given back to {w.House(back)?.Name} at the peace.");
        }
        // a nation that would rather buy than fight buys its worlds back at a peace it lost: the houses that hold them are paid, and the
        // player is asked (the offer waits two years)
        if (reachWon && !allBack && Buys(d)) foreach (var x in Worlds(w, d))
            if (w.Holding(x.Id) is { } held && held.OwnerId != n.HouseId && w.OfTheReach(held.OwnerId))
            {
                if (held.OwnerId == w.Protagonist?.HouseId) { w.WorldCounters[RansomKey(x.Id)] = w.Turn; Mark(w, "ransom_offered", n); w.Log($"{d.Name} offers {Money.Credits(RansomPerWealth * d.Wealth)} for {held.Name}, to have it back."); }
                else Ransom(w, n, d, held);
            }
        // and if the nation won, what the Reach took of its own worlds is given up
        if (!reachWon || allBack) foreach (var x in Worlds(w, d))
            if (w.Holding(x.Id) is { } hw && hw.OwnerId != n.HouseId && w.OfTheReach(hw.OwnerId))
            {
                string was = hw.OwnerId; hw.OwnerId = n.HouseId; hw.FormerOwnerId = ""; hw.GovernorId = n.Ruler?.CharacterId ?? ""; Bump(w, GivenUpCounter, n);
                if (was == w.Protagonist?.HouseId) Mark(w, "steppe_world_lost", n);
                w.Log($"{hw.Name} is given up to {d.Name} at the peace by {w.House(was)?.Name ?? was}.");
            }
        foreach (var s in w.Sieges.Values.Where(s => s.BesiegerId == n.HouseId || w.Holding(s.HoldingId)?.OwnerId == n.HouseId).ToList()) w.Sieges.Remove(s.HoldingId);
    }

    /// <summary>Where a nation's host should be this year: against a world of the Reach near its march while the war lasts and it has the
    /// hulls for it; home otherwise. Null if it is where it should be.</summary>
    public static string? HostOrder(World w, Nation n, NationDef d, Fleet host, ISet<string>? taken = null, string? homeAt = null)
    {
        if (w.Galaxy == null || host.Moving) return null;
        string home = homeAt ?? Border(w, n, d);
        bool atHome = host.SystemId == home;
        if (w.Counter(SortieKey(host)) > 0)
        {   // a raider goes home with what it took, and comes out again next year if it is fit to; a host on campaign stays in the field
            if (Stays(n, d) || atHome) w.WorldCounters.Remove(SortieKey(host)); else return home;
            if (!Stays(n, d)) return null;
        }
        // a season in the Reach is as long as it can stay: short of supply it goes home, and it does not come out again until it is rested and fed
        if (!n.AtWar || host.Strength < HostLeast || host.Supply < HostSpent || (atHome && (host.Readiness < HostRested || host.Supply < HostSupplied))) return host.SystemId == home ? null : home;
        // sitting over a world of the Reach it has not yet taken: it stays until the world falls or it is driven off
        var own = Own(w, d); bool comesOut = ComesOut(n, d);
        if (w.Holdings.Values.Any(h => h.IsPlanet && h.SystemId == host.SystemId && Foe(w, n, h.OwnerId) && (comesOut || own.Contains(h.SystemId)))) return null;
        // an enemy fleet in its own space that it can beat is met first
        // (all of its fleets that are there or could come count: a nation that turns out its guard meets an invader with both)
        int together = d.Sorties == "never" ? w.Fleets.Values.Where(f => f.OwnerId == n.HouseId).Sum(f => f.Strength) : host.Strength;
        var intruder = w.Fleets.Values.Where(f => !f.Moving && !f.IsAway && Foe(w, n, f.OwnerId) && own.Contains(f.SystemId) && FleetSystem.StrengthAt(w, f.OwnerId, f.SystemId) <= together)
            .OrderBy(f => w.Galaxy.Hops(host.SystemId, f.SystemId) is var hp && hp >= 0 ? hp : 99).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
        if (intruder != null) return intruder.SystemId == host.SystemId ? null : intruder.SystemId;
        // else the nearest world held by the Reach, within reach of the march, whose defenders it outnumbers; the smallest first among equals.
        // A nation that does not come out goes only for worlds of its own
        var target = w.Holdings.Values.Where(h => h.IsPlanet && h.SystemId.Length > 0 && Foe(w, n, h.OwnerId) && (comesOut || own.Contains(h.SystemId)) && taken?.Contains(h.SystemId) != true && w.Galaxy.Hops(home, h.SystemId) is var reach && reach >= 0 && reach <= HostReach + (Great(n) ? GreatReach : 0)
                && FleetSystem.StrengthAt(w, h.OwnerId, h.SystemId) < host.Strength)
            .OrderBy(h => w.Galaxy.Hops(host.SystemId, h.SystemId) is var hp && hp >= 0 ? hp : 99).ThenBy(h => h.Population).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
        return target == null ? (host.SystemId == home ? null : home) : target.SystemId == host.SystemId ? null : target.SystemId;
    }
}
