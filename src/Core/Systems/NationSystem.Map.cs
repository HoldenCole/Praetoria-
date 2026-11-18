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
    private static string SortieKey(Nation n) => "host_goes_home:" + n.Id;
    /// <summary>What happened on the map this year, for the reports. Some of it is raised by the fleets before this system's own tick, so
    /// these are not cleared with <see cref="TurnFlags"/>: each is stamped with its year and struck out at the tick of the year after.</summary>
    public static readonly string[] MapFlags = { "nation_host_sails", "reach_world_occupied", "our_world_occupied", "world_freed_by_us", "world_freed_for_us", "reach_world_ceded", "steppe_world_restless", "steppe_world_lost", "steppe_world_rose", CapitalTakenFlag, FallenFlag, "host_beaten_by_us" };
    public static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }
    private static void ClearMarks(World w) { foreach (var f in MapFlags) if (w.WorldFlags.Contains(f) && w.Counter("mark:" + f) < w.Turn) { w.WorldFlags.Remove(f); w.WorldCounters.Remove("mark:" + f); } }

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
        w.Houses[house.Id] = house; n.HouseId = house.Id;
        var capital = d.Worlds.FirstOrDefault(x => x.Capital) ?? d.Worlds[0]; n.CapitalSystemId = capital.System;
        var ruler = Embody(w, n, d, n.Ruler!, ruler: true); var commander = Embody(w, n, d, n.Commander!, ruler: false);
        foreach (var x in d.Worlds)
            w.Holdings[x.Id] = new Holding { Id = x.Id, OwnerId = house.Id, Name = x.Name, Specialization = x.Specialization, SystemId = x.System, Population = x.Population, GovernorId = ruler.Id, Culture = house.Culture };
        var marches = Marches(w, d); string border = Border(w, n, d);
        int total = Math.Max(2, n.Strength), home = Math.Max(2, total / HomeShare), host = Math.Max(0, total - home);
        Raise(w, GuardId(n), $"The Guard of {capital.Name.Replace("The ", "")}", house.Id, capital.System, home, ruler.Id, defaultClass);
        if (host > 0) Raise(w, house.Id + "_host", $"The Host of {d.Name.Replace("The ", "")}", house.Id, border, host, commander.Id, defaultClass);
        foreach (var f in Expeditions(w, n.Id).ToList()) GoHome(w, f, d, "is called home");
        w.WorldFlags.Add(OnMapFlag);
        w.Log($"Beyond {string.Join(" and ", marches.Select(m => w.Galaxy.System(m)?.Name ?? m))} lie the worlds of {d.Name}: {string.Join(", ", d.Worlds.Select(x => w.Galaxy.System(x.System)?.Name ?? x.System))}. {Styled(d, n.Ruler, true)} sits at {w.Galaxy.System(capital.System)?.Name}.");
    }

    /// <summary>The nation's world nearest its march that it still holds: where its host stands at peace. Its capital if it holds nothing else.</summary>
    public static string Border(World w, Nation n, NationDef d)
    {
        var marches = Marches(w, d);
        int Far(string sys) => marches.Select(m => w.Galaxy!.Hops(m, sys)).Where(h => h >= 0).DefaultIfEmpty(99).Min();
        return w.HoldingsOf(n.HouseId).Where(h => h.IsPlanet && h.SystemId.Length > 0 && w.Galaxy!.RingOf(h.SystemId) == Galaxy.Ring.Beyond).Select(h => h.SystemId).Distinct()
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
        People(w, n, d);
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
            w.WorldCounters[CommandersFallenCounter] = w.Counter(CommandersFallenCounter) + 1;
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
        // While the nation pays tribute its people keep quiet; after that, three years without a garrison and they rise
        if (!afterTheWar) foreach (var x in d.Worlds)
        {
            if (w.Holding(x.Id) is not { } hw || hw.OwnerId == house.Id || !w.OfTheReach(hw.OwnerId)) { w.WorldCounters.Remove(UnheldKey(x.Id)); continue; }
            if (n.Subdued(w.Turn) || FleetSystem.StrengthAt(w, hw.OwnerId, hw.SystemId) >= MarchGuard) { w.WorldCounters.Remove(UnheldKey(x.Id)); continue; }
            int years = w.Counter(UnheldKey(x.Id)) + 1; w.WorldCounters[UnheldKey(x.Id)] = years;
            if (years < RiseYears) { if (hw.OwnerId == w.Protagonist?.HouseId) Mark(w, "steppe_world_restless"); continue; }
            string was = hw.OwnerId; hw.OwnerId = house.Id; hw.FormerOwnerId = ""; hw.GovernorId = n.Ruler.CharacterId; hw.Unrest = Math.Max(0, hw.Unrest - 20); w.WorldCounters.Remove(UnheldKey(x.Id)); w.Sieges.Remove(hw.Id);
            w.WorldCounters[RisenCounter] = w.Counter(RisenCounter) + 1; Mark(w, "steppe_world_rose"); if (was == w.Protagonist?.HouseId) Mark(w, "steppe_world_lost");
            w.Log($"{hw.Name} rises: no fleet of {w.House(was)?.Name ?? was} has stood over it in {RiseYears} years, and it is {d.Name}'s again.");
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
        var guard = fleets.FirstOrDefault(f => IsGuard(n, f)); var host = fleets.FirstOrDefault(f => !IsGuard(n, f));
        foreach (var f in fleets) f.AdmiralId = f == guard ? n.Ruler.CharacterId : f == host ? n.Commander.CharacterId : "";
        // strength is hulls, and it grows back at home while the nation holds its capital
        int hulls = fleets.Sum(f => f.Strength);
        if (!afterTheWar && w.PlanetHolder(n.CapitalSystemId) == house.Id && w.Turn % RegenEvery == 0 && hulls < d.Strength && d.Temper != "decaying")
        {
            int add = Math.Min(Math.Max(1, d.Regen), d.Strength - hulls); string cls = _fleets.Rules.DefaultClass;
            var to = guard == null ? null : host != null && !host.Moving && w.PlanetHolder(host.SystemId) == house.Id && guard.Strength >= Math.Max(2, d.Strength / HomeShare) ? host : guard;
            if (to == null) Raise(w, GuardId(n), $"The Guard of {w.Galaxy?.System(n.CapitalSystemId)?.Name.Replace("The ", "") ?? "the Camp"}", house.Id, n.CapitalSystemId, add, n.Ruler.CharacterId, cls);
            else { if (to.Ships.Count == 0) to.Ships[cls] = to.Strength; to.Ships[cls] = to.Ships.GetValueOrDefault(cls) + add; to.Recount(); }
            if (host == null && guard != null && guard.Strength > Math.Max(2, d.Strength / HomeShare) + HostLeast)
            {   // the host was lost: when the guard has hulls to spare, a new one is raised from it
                int give = guard.Strength - Math.Max(2, d.Strength / HomeShare); if (guard.Ships.Count == 0) guard.Ships[cls] = guard.Strength;
                guard.Ships[cls] = Math.Max(0, guard.Ships.GetValueOrDefault(cls) - give); guard.Recount();
                Raise(w, $"{house.Id}_host_{w.Turn}", $"The Host of {d.Name.Replace("The ", "")}", house.Id, guard.SystemId, give, n.Commander.CharacterId, cls);
            }
            hulls += add;
        }
        n.Strength = hulls;
        house.Treasury.Credits = Math.Max(house.Treasury.Credits, PurseFloor); house.Treasury.Helium3 = Math.Max(house.Treasury.Helium3, FuelFloor);
        house.Treasury.Materials = Math.Max(house.Treasury.Materials, StoresFloor); house.Treasury.Manpower = Math.Max(house.Treasury.Manpower, StoresFloor);

        // where its fleets should be
        if (!afterTheWar) foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.Moving).OrderBy(f => f.Id, StringComparer.Ordinal).ToList())
        {
            string? to = IsGuard(n, f) ? (f.SystemId == n.CapitalSystemId ? null : n.CapitalSystemId) : HostOrder(w, n, d, f);
            if (to == null || to == f.SystemId) continue;
            int eta = w.Galaxy!.Hops(f.SystemId, to, Commands.MoveFleetCommand.Barred(w, house.Id)); if (eta <= 0) continue;
            f.DestinationId = to; f.Eta = eta;
            if (!IsGuard(n, f) && w.Galaxy.RingOf(to) != Galaxy.Ring.Beyond)
            {
                w.WorldCounters[HostsCounter] = w.Counter(HostsCounter) + 1; Mark(w, "nation_host_sails");
                w.Log($"{f.Name} is through the march and bound for {w.Galaxy.System(to)?.Name ?? to}: {f.Strength} hulls under {Styled(d, n.Commander, false)}.");
            }
        }
    }

    /// <summary>The nation's last world is taken: it is no more. What it took from the Reach goes back; its fleets scatter; its people are gone.</summary>
    private static void Fall(World w, Nation n, NationDef d)
    {
        if (n.AtWar) { PeaceOnMap(w, n, d, reachWon: true); EndWar(w, n, d); }
        n.Fallen = true; n.Strength = 0; n.SubduedUntil = 0;
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == n.HouseId).ToList()) w.Fleets.Remove(f.Id);
        foreach (var c in w.Foreigners().Where(c => c.HouseId == n.HouseId).ToList()) c.Alive = false;
        w.WorldCounters[FallenCounter] = w.Counter(FallenCounter) + 1; Mark(w, FallenFlag);
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
            w.WorldCounters[MapBattlesCounter] = w.Counter(MapBattlesCounter) + 1; w.WorldCounters[MapWinsCounter] = w.Counter(MapWinsCounter) + 1; w.WorldCounters[BattlesCounter] = w.Counter(BattlesCounter) + 1;
            if (lost.Commander != null) lost.Commander.Defeats++;
            if (winner == w.Protagonist?.HouseId) Mark(w, "host_beaten_by_us");
        }
        else if (won is { AtWar: true } && w.OfTheReach(loser))
        {
            won.ScoreNation += ScoreSkirmish; won.LastBattleTurn = w.Turn;   // a squadron swatted counts for less than a host beaten
            w.WorldCounters[MapBattlesCounter] = w.Counter(MapBattlesCounter) + 1; w.WorldCounters[BattlesCounter] = w.Counter(BattlesCounter) + 1;
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
            if (freedFor != null) { if (h.IsPlanet) w.WorldCounters[FreedCounter] = w.Counter(FreedCounter) + 1; if (w.House(conqueror) is { } liberator && freedFor != conqueror) liberator.Legitimacy = Math.Min(100, liberator.Legitimacy + 1); }
            else if (h.IsPlanet)
            {
                w.WorldCounters[WorldsTakenCounter] = w.Counter(WorldsTakenCounter) + 1;
                if (h.SystemId == lost.CapitalSystemId) { w.WorldCounters[CapitalsCounter] = w.Counter(CapitalsCounter) + 1; Mark(w, CapitalTakenFlag); }
            }
            if (!lost.AtWar || !h.IsPlanet) return;
            lost.ScoreReach += ScorePerWorld; lost.LastBattleTurn = w.Turn; w.WorldCounters[WinsKey(conqueror, lost.Id)] = w.Counter(WinsKey(conqueror, lost.Id)) + 2;
        }
        else if (h.IsPlanet && w.NationOfHouse(conqueror) is { AtWar: true } took && w.OfTheReach(old))
        {
            took.ScoreNation += ScorePerWorld; took.LastBattleTurn = w.Turn; w.WorldCounters[WorldsLostCounter] = w.Counter(WorldsLostCounter) + 1; Mark(w, old == w.Protagonist?.HouseId ? "our_world_occupied" : "reach_world_occupied");
            w.WorldCounters[SortieKey(took)] = 1;   // one world a season: the host goes home with what it took
        }
    }

    /// <summary>The war with a nation on the map is over. If the Reach won, the worlds the nation took go back to the houses they were taken
    /// from. If the nation won, it keeps them. What the Reach took of the nation's it keeps either way. The nation's fleets go home.</summary>
    private static void PeaceOnMap(World w, Nation n, NationDef d, bool reachWon)
    {
        if (!n.OnMap) return;
        foreach (var h in w.Holdings.Values.Where(h => h.OwnerId == n.HouseId && h.FormerOwnerId.Length > 0 && w.OfTheReach(h.FormerOwnerId) && !d.Worlds.Any(x => x.Id == h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            if (!reachWon) { w.Log($"{h.Name} is ceded to {d.Name} at the peace."); h.FormerOwnerId = ""; if (h.IsPlanet) { w.WorldCounters[WorldsCededCounter] = w.Counter(WorldsCededCounter) + 1; Mark(w, "reach_world_ceded"); } continue; }
            string back = h.FormerOwnerId; h.OwnerId = back; h.FormerOwnerId = ""; h.Unrest = Math.Min(100, h.Unrest + 5);
            if (h.IsPlanet) w.WorldCounters[WorldsReturnedCounter] = w.Counter(WorldsReturnedCounter) + 1; w.Log($"{h.Name} is given back to {w.House(back)?.Name} at the peace.");
        }
        // and if the nation won, what the Reach took of its own worlds is given up
        if (!reachWon) foreach (var x in d.Worlds)
            if (w.Holding(x.Id) is { } hw && hw.OwnerId != n.HouseId && w.OfTheReach(hw.OwnerId))
            {
                string was = hw.OwnerId; hw.OwnerId = n.HouseId; hw.FormerOwnerId = ""; hw.GovernorId = n.Ruler?.CharacterId ?? ""; w.WorldCounters[GivenUpCounter] = w.Counter(GivenUpCounter) + 1;
                if (was == w.Protagonist?.HouseId) Mark(w, "steppe_world_lost");
                w.Log($"{hw.Name} is given up to {d.Name} at the peace by {w.House(was)?.Name ?? was}.");
            }
        foreach (var s in w.Sieges.Values.Where(s => s.BesiegerId == n.HouseId || w.Holding(s.HoldingId)?.OwnerId == n.HouseId).ToList()) w.Sieges.Remove(s.HoldingId);
    }

    /// <summary>Where a nation's host should be this year: against a world of the Reach near its march while the war lasts and it has the
    /// hulls for it; home otherwise. Null if it is where it should be.</summary>
    public static string? HostOrder(World w, Nation n, NationDef d, Fleet host)
    {
        if (w.Galaxy == null || host.Moving) return null;
        string home = Border(w, n, d);
        bool atHome = host.SystemId == home;
        if (w.Counter(SortieKey(n)) > 0) { if (!atHome) return home; w.WorldCounters.Remove(SortieKey(n)); return null; }   // home with what it took; out again next year if it is fit to
        // a season in the Reach is as long as it can stay: short of supply it goes home, and it does not come out again until it is rested and fed
        if (!n.AtWar || host.Strength < HostLeast || host.Supply < HostSpent || (atHome && (host.Readiness < HostRested || host.Supply < HostSupplied))) return host.SystemId == home ? null : home;
        // sitting over a world of the Reach it has not yet taken: it stays until the world falls or it is driven off
        if (w.Holdings.Values.Any(h => h.IsPlanet && h.SystemId == host.SystemId && w.OfTheReach(h.OwnerId))) return null;
        // an enemy fleet in its own space that it can beat is met first
        var intruder = w.Fleets.Values.Where(f => !f.Moving && !f.IsAway && w.OfTheReach(f.OwnerId) && w.Galaxy.RingOf(f.SystemId) == Galaxy.Ring.Beyond && FleetSystem.StrengthAt(w, f.OwnerId, f.SystemId) <= host.Strength)
            .OrderBy(f => w.Galaxy.Hops(host.SystemId, f.SystemId) is var hp && hp >= 0 ? hp : 99).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
        if (intruder != null) return intruder.SystemId == host.SystemId ? null : intruder.SystemId;
        // else the nearest world of the Reach within reach of the march whose defenders it outnumbers; the smallest first among equals
        var target = w.Holdings.Values.Where(h => h.IsPlanet && h.SystemId.Length > 0 && w.OfTheReach(h.OwnerId) && w.Galaxy.Hops(home, h.SystemId) is >= 0 and <= HostReach
                && FleetSystem.StrengthAt(w, h.OwnerId, h.SystemId) < host.Strength)
            .OrderBy(h => w.Galaxy.Hops(host.SystemId, h.SystemId) is var hp && hp >= 0 ? hp : 99).ThenBy(h => h.Population).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
        return target == null ? (host.SystemId == home ? null : home) : target.SystemId == host.SystemId ? null : target.SystemId;
    }
}
