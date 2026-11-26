using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Drives the NPC cadets in the Resolve phase (BuildSpec §5 "AI"; GDD §18 family with will).
/// NPCs act through the SAME command interface and executor the player uses — they just spend
/// abstracted pools (GDD §3). Choice of action is trait/ambition-driven and deterministic
/// (characters processed in id order; RNG only breaks genuine ties), so a full turn — player
/// choices + NPC actions — reproduces exactly from a seed (the Milestone-2 acceptance).
/// </summary>
public sealed class NpcAi
{
    /// <summary>Each living non-protagonist takes at most one affordable action this turn. If a
    /// crisis engine is supplied, an ambitious NPC may also <em>author</em> a causable crisis
    /// (GDD §16 "crises have authors") — through the same command bus the player uses.</summary>
    /// <summary>Turns between an NPC house's attempts on the player's heirs.</summary>
    public const int HeirHuntCooldown = 6;
    /// <summary>A hard head's knife between other houses: how ill they must think of the other head, how long before another, and how soon they look again.</summary>
    public const int KnifeHatred = -60, KnifeCooldown = 20, KnifeRetry = 4;

    /// <summary>Catalogs the fleet AI consults (optional; without them NPCs raise the default class, unled).</summary>
    public ShipClassCatalog? ShipClasses { get; init; }
    public Progression.TitleCatalog? Titles { get; init; }
    /// <summary>Laws and estates: rival stewards legislate when their estates turn, their coffers empty or their wars begin.</summary>
    public PolicyCatalog? Policies { get; init; }
    /// <summary>The nations beyond the Reach: hawkish houses send an idle fleet against the one the Senate has named (docs/FOREIGN_NATIONS.md).</summary>
    public NationCatalog? Nations { get; init; }
    /// <summary>The commodity market: stewards sell what they hoard and buy what they lack at the Reach's prices (docs/ECONOMY_MARKET.md).</summary>
    public TradeRules Trade { get; init; } = TradeRules.Empty;
    /// <summary>Ventures as assets: a steward in distress sells to the best bidder, not to the void (docs/ECONOMY_MARKET.md stage B).</summary>
    public VentureSystem? VentureTrade { get; init; }
    /// <summary>The Exchange, when the age has one: stewards incorporate, float, invest and sell paper by temperament (docs/ECONOMY_MARKET.md stage C).</summary>
    public CompanySystem? Exchange { get; init; }
    /// <summary>The three orders: stewards buy a seat, take clients and canvass (docs/REPUBLIC.md stage 3).</summary>
    public OrderSystem? Orders { get; init; }
    /// <summary>The holdings catalog, for a company's steward founding a venture (set by the turn controller).</summary>
    public HoldingCatalog? HoldingCatalogForFounding { get; init; }
    /// <summary>The ladder of titles: a rival house petitions for its next rung by the player's own command (docs/TITLES.md, "Rungs that are earned").</summary>
    public ProgressionSystem? Progression { get; init; }
    private BusinessCatalog _ventures = BusinessCatalog.Empty;

    public void Act(CommandExecutor executor, CommandContext ctx, CrisisEngine? crises = null,
        HoldingCatalog? catalog = null, FleetRules? fleetRules = null, BusinessCatalog? ventures = null)
    {
        _fuelHullsPerUnit = fleetRules?.FuelHullsPerUnit ?? 3; _catalog = catalog;
        _ventures = ventures ?? BusinessCatalog.Empty;
        var world = ctx.World;
        var npcs = world.LivingCharacters()
            .Where(c => c.Id != world.ProtagonistId)
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .ToList();

        // Fuel first (a dry navy is a navy on paper), then house-level strategy (it spends the same pools), then personal jabs, then schemes.
        using (Diagnostics.Perf.Time("npc:refuel")) if (catalog != null) Refuel(npcs, catalog, executor, ctx);
        using (Diagnostics.Perf.Time("npc:invest")) if (catalog is { IsEmpty: false }) Invest(npcs, catalog, executor, ctx);
        using (Diagnostics.Perf.Time("npc:sail")) if (fleetRules != null && world.Galaxy != null) Sail(npcs, fleetRules, executor, ctx);

        using (Diagnostics.Perf.Time("npc:dynasty")) Dynasty(npcs, executor, ctx);
        using (Diagnostics.Perf.Time("npc:rise")) Rise(executor, ctx);

        using (Diagnostics.Perf.Time("npc:each")) foreach (var npc in npcs)
        {
            var command = ChooseAction(npc, world);
            if (command != null)
                executor.TryExecute(command, ctx);
        }

        using (Diagnostics.Perf.Time("npc:crisis")) if (crises != null) AuthorCrisis(npcs, crises, executor, ctx);
    }

    /// <summary>The other houses climb the ladder of titles by the road the player's house climbs it (docs/TITLES.md, "Rungs that
    /// are earned"): a head whose house has the standing for its next rung, and from Legate up the land or the seats, petitions,
    /// about one year in four. Up to Prefect it is granted; above that it goes before the Senate, one house's petition at a time,
    /// and the player's house votes on it with the rest.</summary>
    private void Rise(CommandExecutor executor, CommandContext ctx)
    {
        var prog = Progression; if (prog == null) return; var world = ctx.World; var mine = world.Protagonist?.HouseId;
        foreach (var house in world.Houses.Values.Where(h => h.Id != mine).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            var head = Economy.HeadOf(world, house); if (head == null || head.Id == world.ProtagonistId) continue;
            if (prog.RivalWouldPetition(world, house, head)) executor.TryExecute(new PetitionTitleCommand(head.Id, prog), ctx);
        }
    }

    /// <summary>Rival houses keep ships (GDD §3 abstracted realms): a house with a comfortable treasury lays
    /// down a hull at its seat; a hostile head (disposition &lt; −40 toward the protagonist) with a rested fleet
    /// sends it toward the protagonist's seat — the menace the map is for. One order per house per turn.</summary>
    /// <summary>A rival house's tactics, deterministic: a fleet that outguns every enemy it could meet presses, one outgunned skirmishes, the rest
    /// hold the line; a tactician with manpower to spare boards; torpedoes are loaded at home when the house holds two isotopes or more; a house
    /// at war with no campaign names the nearest enemy world; a head on the military track leads from the front when young and from the flag when
    /// a Commander or better.</summary>
    /// <summary>Order a fleet somewhere and keep the reason with it, so the house whose space it comes into can be told what it is there for.</summary>
    private static bool Sail(CommandExecutor executor, CommandContext ctx, Character head, Fleet f, string to, FleetRules rules, string errand)
    {
        if (!executor.TryExecute(new MoveFleetCommand(head.Id, f.Id, to, rules), ctx)) return false;
        f.Errand = errand; return true;
    }

    private void Posture(World world, House house, Character head, FleetRules rules, CommandExecutor executor, CommandContext ctx)
    {
        var enemies = world.EnemiesOf(house.Id).ToList();
        Expedition(world, house, head, rules, executor, ctx, enemies.Count(e => !World.Outsider(e)));   // a war with a nation is not a war at home
        int ours = FleetSystem.StrengthOf(world, house.Id), theirs = enemies.Count == 0 ? 0 : enemies.Max(e => FleetSystem.StrengthOf(world, e));
        foreach (var f in world.Fleets.Values.Where(f => f.OwnerId == house.Id).OrderBy(f => f.Id, StringComparer.Ordinal))
        {
            string want = enemies.Count == 0 ? "line" : head.Skill("tactics") >= 3 && house.Treasury.Manpower >= 6 && ours >= theirs ? "boarding" : ours >= theirs * 3 / 2 ? "press" : ours * 3 / 2 < theirs ? "skirmish" : "line";
            if (f.Stance != want) executor.TryExecute(new SetStanceCommand(head.Id, f.Id, want), ctx);
            string load = house.Treasury.Isotopes >= 2 && enemies.Count > 0 ? "torpedoes" : f.Loadout == "torpedoes" && house.Treasury.Isotopes == 0 ? "guns" : f.Loadout;
            if (load != f.Loadout) executor.TryExecute(new SetLoadoutCommand(head.Id, f.Id, load), ctx);
        }
        if (head.CareerTrack == "military")
        {
            string style = head.CareerRank >= 3 ? "flag" : head.Age < 40 ? "front" : "none";
            if (SetCommandStyleCommand.StyleOf(head) != style) executor.TryExecute(new SetCommandStyleCommand(head.Id, head.Id, style), ctx);
        }
        if (enemies.Count > 0 && FleetSystem.CampaignTarget(world, house.Id) == null && world.Galaxy is { } g && ours >= 2)
        {
            var home = world.HoldingsOf(house.Id).Where(h => h.IsPlanet && h.SystemId.Length > 0).Select(h => h.SystemId).FirstOrDefault();
            var target = enemies.SelectMany(e => world.HoldingsOf(e).Where(h => h.IsPlanet && h.SystemId.Length > 0).Select(h => h.SystemId)).Distinct()
                .Select(sid => (sid, hops: home != null ? g.Hops(home, sid) : 99)).Where(x => x.hops > 0).OrderBy(x => x.hops).ThenBy(x => x.sid, StringComparer.Ordinal).FirstOrDefault();
            if (target.sid != null) executor.TryExecute(new DeclareCampaignCommand(head.Id, target.sid, rules), ctx);
        }
    }

    /// <summary>A house at peace at home, of the Sword or led by a Commander or better, with no fleet already abroad, sends its largest idle fleet of
    /// three hulls or more against the nation the Reach is at war with — if it is whole, supplied and willing (the command refuses otherwise).</summary>
    private void Expedition(World world, House house, Character head, FleetRules rules, CommandExecutor executor, CommandContext ctx, int enemiesAtHome)
    {
        if (Nations == null || Nations.IsEmpty || enemiesAtHome > 0) return;
        if (NationSystem.AtWar(world) is not { } war) { OwnWar(world, house, head, executor, ctx); return; }
        if (world.Nation(war.Id) is { OnMap: true } onMap && Nations.Def(war.Id) is { } nd) { March(world, house, head, onMap, nd, rules, executor, ctx); return; }
        if (world.Fleets.Values.Any(f => f.OwnerId == house.Id && f.IsAway)) return;
        bool hawk = FactionSystem.Of(world, house.Id)?.PartyId == "sword" || (head.CareerTrack == "military" && head.CareerRank >= 2) || OfficeSystem.HeldBy(world, house.Id).Any(o => o.OfficeId is "consul_senior" or "consul_junior" or "admiral");
        if (!hawk) return;
        int least = Math.Max(3, (world.Nation(war.Id)?.Strength ?? 0) / 4);   // no house throws three hulls at a fleet of thirty
        var fleet = world.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.Moving && !f.IsAway && f.Strength >= least && !world.HoldingsOf(house.Id).Any(h => h.InRebellion))
            .OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
        if (fleet == null) return;
        if (world.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.IsAway).Sum(f => f.Strength) - fleet.Strength < 1 && world.HoldingsOf(house.Id).Count(h => h.IsPlanet) > 1) return;   // leave something at home
        executor.TryExecute(new SendExpeditionCommand(head.Id, fleet.Id, war.Id, rules, Nations), ctx);
    }

    public const int OwnWarOneIn = 8, OwnWarPurse = 60, OwnWarReach = 4;
    /// <summary>A house makes war on a nation on its own account (docs/BEYOND.md, "Whose war"): a raiding war, with no vote. A house of the
    /// war party or under a serving officer, short of money, at peace at home, with a rested fleet of a third of the nation's strength,
    /// and a world within four lanes of its march, goes for a neighbour that is rich, that raids, or that it has cause to hate. One year
    /// in eight when it may. Never against one that pays the Reach tribute.</summary>
    private void OwnWar(World world, House house, Character head, CommandExecutor executor, CommandContext ctx)
    {
        if (Nations == null || world.Galaxy == null || house.Id == world.Protagonist?.HouseId || world.Wars.Any(x => x.Involves(house.Id)) || house.Treasury.Credits >= OwnWarPurse) return;
        bool hawk = FactionSystem.Of(world, house.Id)?.PartyId == "sword" || (head.CareerTrack == "military" && head.CareerRank >= 2);
        if (!hawk) return;
        uint roll = 2166136261; foreach (var ch in $"ownwar:{house.Id}:{world.Turn}") roll = (roll ^ ch) * 16777619;
        if (roll / 7 % OwnWarOneIn != 0) return;
        int best = world.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.IsAway && !f.Moving && !f.Office && f.Readiness >= 60).Select(f => f.Strength).DefaultIfEmpty(0).Max();
        var near = world.HoldingsOf(house.Id).Where(h => h.SystemId.Length > 0).Select(h => h.SystemId).Distinct().ToList();
        var pick = world.Nations.Where(n => n.OnMap && !n.Fallen && !n.Subdued(world.Turn) && Nations.Def(n.Id) is { } nd
                && (nd.Wealth >= SenateSystem.RichAt || n.Disposition <= -30 || NationSystem.RaiderOf(world, house.Id, 8) == n.Id)
                && best >= Math.Max(NationSystem.PrivateLeast, n.Strength / 3)
                && NationSystem.Marches(world, nd).Any(m => near.Any(s => world.Galaxy.Hops(s, m) is >= 0 and <= OwnWarReach)))
            .OrderByDescending(n => NationSystem.RaiderOf(world, house.Id, 8) == n.Id).ThenByDescending(n => Nations.Def(n.Id)!.Wealth).ThenBy(n => n.Id, StringComparer.Ordinal).FirstOrDefault();
        if (pick != null) executor.TryExecute(new RaidNationCommand(head.Id, pick.Id, Nations), ctx);
    }

    /// <summary>A war with a nation that is on the map (docs/BEYOND.md): a house whose world they hold or sit over goes to it with what it
    /// has, if that is enough; a house of the war party, a serving officer's or one that holds a great office of arms goes to drive their
    /// host out of the Reach, and after that to take the worlds beyond the march, the weakest held first. One fleet a year, the largest
    /// rested one, and never the last hull at home.</summary>
    private void March(World world, House house, Character head, Nation n, NationDef d, FleetRules rules, CommandExecutor executor, CommandContext ctx)
    {
        var g = world.Galaxy; if (g == null) return;
        string them = n.HouseId;
        if (n.WarOf.Length > 0 && n.WarOf != house.Id) return;   // one house's own war is nobody else's (docs/BEYOND.md, "Whose war")
        bool raids = NationSystem.RaidsOnly(world, n, house.Id);   // in a war the nation did not begin, a house sacks and goes home
        bool Spent(string sys) => raids && g.RingOf(sys) == Galaxy.Ring.Beyond && FleetSystem.StrengthAt(world, them, sys) == 0 && world.Holdings.Values.Where(h => h.IsPlanet && h.SystemId == sys && h.OwnerId == them).All(h => NationSystem.Sacked(world, h));
        bool hawk = n.WarOf == house.Id || FactionSystem.Of(world, house.Id)?.PartyId == "sword" || (head.CareerTrack == "military" && head.CareerRank >= 2) || OfficeSystem.HeldBy(world, house.Id).Any(o => o.OfficeId is "consul_senior" or "consul_junior" or "admiral");
        var mineHeld = world.Holdings.Values.Where(h => h.IsPlanet && h.SystemId.Length > 0 && ((h.OwnerId == them && h.FormerOwnerId == house.Id) || (h.OwnerId == house.Id && FleetSystem.StrengthAt(world, them, h.SystemId) > 0))).Select(h => h.SystemId).ToHashSet();
        bool defends = hawk || n.BeganIt;   // a war the nation began is every house's to answer inside the Reach; carrying it beyond the march is still for those who want a war
        if (!defends && mineHeld.Count == 0) return;
        var fleet = world.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.Moving && !f.IsAway && f.Readiness >= 60 && f.Strength >= 2 && !world.HoldingsOf(house.Id).Any(h => h.InRebellion))
            .OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
        if (fleet == null) return;
        // already where the war is: over a world of theirs, or facing their fleet
        if ((world.PlanetHolder(fleet.SystemId) == them && !Spent(fleet.SystemId)) || FleetSystem.StrengthAt(world, them, fleet.SystemId) > 0) return;
        int Hops(string sys) => g.Hops(fleet.SystemId, sys) is var hp && hp > 0 ? hp : 99;
        // what the Reach could bring: this fleet, those already there or bound for it, and the rested fleets of the other houses that would
        // go, within six lanes. Inside the Reach that must match what is there; beyond the march it must be a quarter more.
        bool Would(string hid) => world.House(hid) is { } o && (n.BeganIt || FactionSystem.Of(world, hid)?.PartyId == "sword" || Economy.HeadOf(world, o) is { CareerTrack: "military", CareerRank: >= 2 } || OfficeSystem.HeldBy(world, hid).Any(x => x.OfficeId is "consul_senior" or "consul_junior" or "admiral"));
        int Pledged(string sys) => fleet.Strength + world.Fleets.Values.Where(f => f.Id != fleet.Id && world.OfTheReach(f.OwnerId) && !f.IsAway && (f.Moving ? f.DestinationId == sys
            : f.SystemId == sys || (f.Readiness >= 60 && f.Strength >= 2 && f.OwnerId != world.Protagonist?.HouseId && g.Hops(f.SystemId, sys) is > 0 and <= 6 && Would(f.OwnerId)))).Sum(f => f.Strength);
        bool CanTake(string sys) { int there = FleetSystem.StrengthAt(world, them, sys); return there == 0 || (g.RingOf(sys) == Galaxy.Ring.Beyond ? Pledged(sys) * 400 >= there * 5 * Math.Max(50, n.Mettle) : Pledged(sys) * 100 >= there * Math.Max(50, n.Mettle)); }   // hull for hull they fight by their temper
        // first what is the house's own, then their fleets and holdings inside the Reach, then their own worlds
        string? target = mineHeld.Where(CanTake).OrderBy(Hops).ThenBy(x => x, StringComparer.Ordinal).FirstOrDefault();
        if (target == null && defends)
            target = world.Fleets.Values.Where(f => f.OwnerId == them && !f.Moving && g.RingOf(f.SystemId) != Galaxy.Ring.Beyond).Select(f => f.SystemId)
                .Concat(world.Holdings.Values.Where(h => h.IsPlanet && h.OwnerId == them && h.SystemId.Length > 0 && g.RingOf(h.SystemId) != Galaxy.Ring.Beyond).Select(h => h.SystemId)).Distinct()
                .Where(CanTake).OrderBy(Hops).ThenBy(x => x, StringComparer.Ordinal).FirstOrDefault();
        if (target == null && hawk && fleet.Strength >= Math.Max(3, n.Strength / 4)
            && (world.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.IsAway).Sum(f => f.Strength) - fleet.Strength >= 1 || world.HoldingsOf(house.Id).Count(h => h.IsPlanet) <= 1))
            target = world.Holdings.Values.Where(h => h.IsPlanet && h.OwnerId == them && h.SystemId.Length > 0).Select(h => h.SystemId).Distinct()
                .Where(sys => !Spent(sys)).Where(CanTake).OrderBy(Hops).ThenBy(sys => FleetSystem.StrengthAt(world, them, sys)).ThenBy(x => x, StringComparer.Ordinal).FirstOrDefault();
        // a defended world beyond the march is not gone at piecemeal: the fleets gather at the march nearest it, and go in together in
        // the year those standing there are a quarter more than what holds it
        if (target != null && g.RingOf(target) == Galaxy.Ring.Beyond && FleetSystem.StrengthAt(world, them, target) is > 0 and var holds
            && NationSystem.Marches(world, d).OrderBy(m => g.Hops(m, target) is var hp && hp > 0 ? hp : 99).ThenBy(m => m, StringComparer.Ordinal).FirstOrDefault() is { } stage)
        {
            int gathered = world.Fleets.Values.Where(f => world.OfTheReach(f.OwnerId) && !f.IsAway && f.Strength >= 2 && (f.Moving ? f.DestinationId == target : f.SystemId == stage)).Sum(f => f.Strength);
            // (half as much again against a fortress: its walls fight too)
            int walls = world.Holdings.Values.Any(h => h.IsPlanet && h.SystemId == target && h.OwnerId == them && h.Specialization == "fortress") ? 3 : 2;
            if (fleet.SystemId != stage) target = stage;
            else if (gathered * 800 < holds * 5 * walls * Math.Max(50, n.Mettle)) { world.WorldCounters["fleets_waiting_at_a_march"] = world.Counter("fleets_waiting_at_a_march") + 1; return; }
        }
        // a raider with nothing left worth sacking goes home with what it has
        if (target == null && raids && g.RingOf(fleet.SystemId) == Galaxy.Ring.Beyond)
            target = world.HoldingsOf(house.Id).Where(h => h.SystemId.Length > 0).Select(h => h.SystemId).Distinct().OrderBy(Hops).ThenBy(x => x, StringComparer.Ordinal).FirstOrDefault() ?? NationSystem.Marches(world, d).FirstOrDefault();
        if (target == null || target == fleet.SystemId || Hops(target) >= 99) return;
        if (Sail(executor, ctx, head, fleet, target, rules, "to raid")) { world.WorldCounters[NationSystem.SentCounter] = world.Counter(NationSystem.SentCounter) + 1; world.WorldCounters[NationSystem.SentCounter + ":" + n.Id] = world.Counter(NationSystem.SentCounter + ":" + n.Id) + 1; }
    }

    private void Sail(List<Character> npcs, FleetRules rules, CommandExecutor executor, CommandContext ctx)
    {
        var world = ctx.World;
        var player = world.Protagonist;
        var playerHouse = player?.HouseId;
        var seat = world.Holdings.Values.Where(h => h.OwnerId == playerHouse && h.SystemId != "")
                                        .OrderBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault()?.SystemId;

        foreach (var house in world.Houses.Values.OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            if (house.Id == playerHouse) continue;
            var head = npcs.FirstOrDefault(c => c.HouseId == house.Id);
            if (head == null) continue;

            int disposition = player != null ? world.GetRelationship(head.Id, player.Id)?.Disposition ?? 0 : 0;
            bool atWar = playerHouse != null && world.AtWar(house.Id, playerHouse);

            // the admiralty's choices (docs/MILITARY.md): a stance by the odds, torpedoes when the magazines allow, a campaign when at war
            Posture(world, house, head, rules, executor, ctx);

            // a rising at home comes first (GDD §19 the rim): the nearest idle fleet sails to sit in orbit over the world in revolt
            var rising = world.HoldingsOf(house.Id).Where(h => h.InRebellion).OrderByDescending(h => h.Rebellion).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
            if (rising != null && !world.Fleets.Values.Any(f => f.OwnerId == house.Id && f.SystemId == rising.SystemId && !f.Moving))
            {
                var garrison = world.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.Moving && f.Strength >= RegionSystem.GarrisonStrength).OrderBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
                if (garrison != null) Sail(executor, ctx, head, garrison, rising.SystemId, rules, $"to put down the rising on {rising.Name}");
            }

            // war and peace (GDD §7 fear with teeth): a house that hates you and outguns you declares war;
            // one that has lost half its hulls sues for peace.
            if (player != null && playerHouse != null)
            {
                int warAppetite = (int)Math.Round(-60 / Math.Max(0.25, world.Difficulty));   // harsh difficulty: rivals go to war sooner
                if (!atWar && disposition < warAppetite && FleetSystem.StrengthOf(world, house.Id) > FleetSystem.StrengthOf(world, playerHouse))
                { if (executor.TryExecute(new DeclareWarCommand(head.Id, playerHouse, rules), ctx)) atWar = true; }
                else if (atWar && FleetSystem.StrengthOf(world, house.Id) * 2 < FleetSystem.StrengthOf(world, playerHouse))
                { if (executor.TryExecute(new SueForPeaceCommand(head.Id, playerHouse, rules), ctx)) continue; }
            }

            // a lord does not forgive a stolen world (GAP §6): a free hold that took one is bought back when the coin is there, else fought when the fleet is
            var stolen = world.Holdings.Values.Where(h => h.FormerOwnerId == house.Id && world.House(h.OwnerId)?.Bloc == RegionSystem.FreeholdsBloc).OrderBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
            if (stolen != null)
            {
                if (house.Treasury.Credits >= ReclaimFreeholdCommand.Price(world, stolen, house.Id) + 10 && executor.TryExecute(new ReclaimFreeholdCommand(head.Id, stolen.Id), ctx)) continue;
                if (!world.AtWar(house.Id, stolen.OwnerId) && FleetSystem.StrengthOf(world, house.Id) >= 2 && FleetSystem.StrengthOf(world, house.Id) > FleetSystem.StrengthOf(world, stolen.OwnerId) && house.Treasury.Credits >= 0)
                    if (executor.TryExecute(new DeclareWarCommand(head.Id, stolen.OwnerId, rules), ctx)) world.Log($"{house.Name} moves to take {stolen.Name} back from {world.House(stolen.OwnerId)?.Name}.");
                var enemyAt = world.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.Moving && f.SystemId != stolen.SystemId && f.Strength >= 2).OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
                if (world.AtWar(house.Id, stolen.OwnerId) && enemyAt != null && Sail(executor, ctx, head, enemyAt, stolen.SystemId, rules, $"to take {stolen.Name} back")) continue;
            }

            // raided from beyond the rim lately: post a fleet at that nation's march, where the next raid can be met before it lands (docs/BEYOND.md)
            if (Nations != null && NationSystem.RaiderOf(world, house.Id, 8) is { } raider && Nations.Def(raider) is { } rd && NationSystem.Guard(world, rd) == null && world.Wars.All(x => !x.Involves(house.Id)))
            {
                var march = NationSystem.Marches(world, rd); var hearth = world.HoldingsOf(house.Id).Where(h => h.SystemId.Length > 0).Select(h => h.SystemId).FirstOrDefault();
                var post = hearth == null ? null : march.OrderBy(m => world.Galaxy!.Hops(hearth, m) is var hops && hops >= 0 ? hops : 99).ThenBy(m => m, StringComparer.Ordinal).FirstOrDefault();
                var warden = world.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.Moving && !f.IsAway && f.Strength >= NationSystem.MarchGuard && f.SystemId != post).OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
                if (post != null && warden != null && Sail(executor, ctx, head, warden, post, rules, "to guard the march against raiders from beyond the rim")) { world.WorldCounters["marches_garrisoned"] = world.Counter("marches_garrisoned") + 1; continue; }
            }

            // a nation that pays only while it is watched (docs/BEYOND.md, the Free Cities): the house that holds the capital, which takes
            // the tribute, keeps a fleet at its march
            if (Nations != null && RegionSystem.CapitalHolder(world)?.Id == house.Id && world.Wars.All(x => !x.Involves(house.Id))
                && world.Nations.Where(x => x.OnMap && x.Subdued(world.Turn)).Select(x => Nations.Def(x.Id)).FirstOrDefault(x => x != null && NationSystem.Buys(x) && NationSystem.Guard(world, x) == null) is { } payer)
            {
                var hearth = world.HoldingsOf(house.Id).Where(h => h.SystemId.Length > 0).Select(h => h.SystemId).FirstOrDefault();
                var post = hearth == null ? null : NationSystem.Marches(world, payer).OrderBy(m => world.Galaxy!.Hops(hearth, m) is var hops && hops >= 0 ? hops : 99).ThenBy(m => m, StringComparer.Ordinal).FirstOrDefault();
                var watch = world.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.Moving && !f.IsAway && f.Strength >= NationSystem.MarchGuard && f.SystemId != post).OrderBy(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
                if (post != null && watch != null && world.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.IsAway).Sum(f => f.Strength) - watch.Strength >= 2
                    && Sail(executor, ctx, head, watch, post, rules, "to watch the march, where a tributary nation pays its tribute")) { world.WorldCounters["marches_watched_for_tribute"] = world.Counter("marches_watched_for_tribute") + 1; continue; }
            }

            // raiders near home are hunted (GAP §6): a fleet of two or more sails on raiders sitting within a lane of a world of the house's
            var nearHome = world.HoldingsOf(house.Id).Where(h => h.SystemId.Length > 0).SelectMany(h => world.Galaxy!.Neighbors(h.SystemId).Append(h.SystemId)).ToHashSet();
            var raiders = world.Fleets.Values.Where(f => f.OwnerId == PirateSystem.HouseId && !f.Moving && nearHome.Contains(f.SystemId)).OrderBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
            if (raiders != null)
            {
                var hunter = world.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.Moving && f.Strength > raiders.Strength && f.SystemId != raiders.SystemId).OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
                if (hunter != null && Sail(executor, ctx, head, hunter, raiders.SystemId, rules, "to hunt raiders")) continue;
            }

            // the capital is coveted (GDD §19 "the capital as a prize"): a strong, legitimate, solvent house makes war on its holder, one such war a decade
            var capital = RegionSystem.CapitalHolder(world);
            if (capital != null && capital.Id != house.Id && !world.AtWar(house.Id, capital.Id) && world.Turn - world.Counter("capital_coveted_turn") >= 10
                && house.Legitimacy >= 40 && house.Treasury.Credits >= 30 && FleetSystem.StrengthOf(world, house.Id) >= Math.Max(3, (int)Math.Ceiling(FleetSystem.StrengthOf(world, capital.Id) * 1.5))
                && executor.TryExecute(new DeclareWarCommand(head.Id, capital.Id, rules), ctx))
            {
                world.WorldCounters["capital_coveted_turn"] = world.Turn;
                world.Log($"{house.Name} declares for the capital: its fleets sail on {world.Galaxy!.System(world.Galaxy.Capital)?.Name ?? "the capital"} and {capital.Name} must hold it.");
                var fleet = world.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.Moving && f.SystemId != world.Galaxy.Capital).OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
                if (fleet != null) Sail(executor, ctx, head, fleet, world.Galaxy.Capital, rules, "to take the capital");
                continue;
            }

            // a pretender sails for the capital: the throne is taken there or not at all (docs/THRONE.md)
            if (world.Rising is { } standard && standard.HouseId == house.Id && world.Galaxy is { Capital.Length: > 0 } gal)
            {
                var van = world.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.Moving && f.SystemId != gal.Capital).OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
                if (van != null && !world.Fleets.Values.Any(f => f.OwnerId == house.Id && (f.SystemId == gal.Capital && !f.Moving || f.Moving && f.DestinationId == gal.Capital) && f.Strength >= 2)
                    && Sail(executor, ctx, head, van, gal.Capital, rules, "to take the throne at the capital")) continue;
            }

            // menace: a hostile (or belligerent), rested house fleet moves on the player's seat
            if (seat != null && player != null && (atWar || disposition < -40 && world.Counter(FleetSystem.AskedOffKey(house.Id, playerHouse!)) <= world.Turn))
            {
                var fleet = world.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.Moving && f.Readiness >= 80 && f.SystemId != seat)
                                               .OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
                if (fleet != null && Sail(executor, ctx, head, fleet, seat, rules, atWar ? "to make war on your seat" : "to stand over your seat as a threat: its head thinks ill of you")) continue;
            }

            // against any enemy (GAP §7 "wars are bloodless"): hold the chokepoint between home and the enemy if it is unguarded, else strike the enemy's weakest world
            var enemy = world.EnemiesOf(house.Id).Where(e => e != playerHouse && !World.Outsider(e)).OrderBy(e => e, StringComparer.Ordinal).FirstOrDefault();
            var home = world.HoldingsOf(house.Id).Where(h => h.SystemId.Length > 0).OrderByDescending(h => h.Population).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault()?.SystemId;
            if (enemy != null && home != null)
            {
                var strike = world.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.Moving && f.Readiness >= 70 && f.Strength >= 2).OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
                if (strike != null)
                {
                    var theirSeat = world.HoldingsOf(enemy).Where(h => h.SystemId.Length > 0).OrderByDescending(h => h.Population).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault()?.SystemId;
                    string? target = null;
                    if (theirSeat != null)
                    {
                        var gate = world.Galaxy!.Chokepoints().Where(c => c != home && world.PlanetHolder(c) != enemy && !world.Fleets.Values.Any(f => f.OwnerId == enemy && !f.Moving && f.SystemId == c))
                            .Select(c => (c, d: world.Galaxy.Hops(home, c) + world.Galaxy.Hops(c, theirSeat))).Where(x => x.d > 0 && world.Galaxy.Hops(home, x.c) > 0 && world.Galaxy.Hops(x.c, theirSeat) > 0)
                            .OrderBy(x => x.d).ThenBy(x => x.c, StringComparer.Ordinal).FirstOrDefault();
                        if (gate.c != null && !world.Fleets.Values.Any(f => f.OwnerId == house.Id && f.SystemId == gate.c)) target = gate.c;
                    }
                    target ??= world.HoldingsOf(enemy).Where(h => h.IsPlanet && h.SystemId.Length > 0 && !world.Fleets.Values.Any(f => f.OwnerId == enemy && !f.Moving && f.SystemId == h.SystemId))
                        .OrderBy(h => h.Population).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault()?.SystemId;
                    if (target != null && target != strike.SystemId && Sail(executor, ctx, head, strike, target, rules, $"to make war on {world.House(enemy)?.Name ?? enemy}")) continue;
                }
            }

            // an unled fleet gets the house's best military mind as admiral (free of pool cost: a commission, not a scheme)
            foreach (var fleet in world.Fleets.Values.Where(f => f.OwnerId == house.Id && f.AdmiralId == "" && !TriumphSystem.Held(world, f)).OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal))
            {
                var taken = world.Fleets.Values.Select(f => f.AdmiralId).ToHashSet();
                var best = npcs.Where(c => c.HouseId == house.Id && !taken.Contains(c.Id) && !CaesarSystem.Barred(world, c) && !TriumphSystem.StandingHome(world, c))   // nor one come home to stand for office (docs/LADDER.md)
                               .OrderByDescending(c => c.Skill("tactics") * 5 + c.Skill("gunnery") * 3 + c.Skill("leadership") * 2 + (c.CareerTrack == "military" ? 5 : 0))
                               .ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
                if (best == null) break;
                fleet.AdmiralId = best.Id;
                world.Log($"{best.Name} takes command of {fleet.Name}.");
            }
            // and a captain under the admiral, where the house has an officer of the service to give: the rule the player is held to
            // (docs/MILITARY.md, "Commanding a navy"), and free of pool cost as the admiral's commission is
            foreach (var fleet in world.Fleets.Values.Where(f => f.OwnerId == house.Id && f.CaptainId == "" && f.LevyOf.Length == 0).OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal))
            {
                var posted = world.Fleets.Values.Select(f => f.CaptainId).Where(id => id.Length > 0).ToHashSet();
                var officer = npcs.Where(c => c.HouseId == house.Id && !posted.Contains(c.Id) && FleetSystem.CaptainRefusal(world, fleet, c) == null)
                                  .OrderByDescending(c => c.Skill("discipline") * 2 + c.Skill("gunnery") + c.CareerRank).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
                if (officer == null) break;
                fleet.CaptainId = officer.Id;
                world.Log($"{officer.Name} is made captain of {fleet.Name}.");
            }

            // otherwise, keep the yards busy when the treasury is comfortable: the heaviest class it can field
            var holding = world.HoldingsOf(house.Id).Where(h => h.SystemId != "").OrderBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
            if (holding == null) continue;
            // a hull is a mouth to feed (GDD §17 fuel): no new hull while the tanks could not carry the navy three turns, and no navy out of all
            // proportion to the worlds that pay for it — ten hulls, plus six a world and one a station or claim
            int hullsNow = world.Fleets.Values.Where(f => f.OwnerId == house.Id).Sum(f => f.Strength);
            int hullCap = 10 + 6 * world.HoldingsOf(house.Id).Count(h => h.IsPlanet) + world.HoldingsOf(house.Id).Count(h => !h.IsPlanet);
            if (hullsNow >= hullCap) continue;
            if (house.Treasury.Helium3 < ((hullsNow + 1 + _fuelHullsPerUnit - 1) / _fuelHullsPerUnit) * 3) continue;   // what the navy would burn with one more hull, three turns over
            var classes = ShipClasses ?? ShipClassCatalog.Empty;
            var candidates = classes.IsEmpty ? new[] { rules.DefaultClass } : classes.Defs.OrderByDescending(c => c.Attack + c.Defense).Select(c => c.Id).ToArray();
            foreach (var cid in candidates)
            {
                var cls = classes.Resolve(cid);
                if (cls.Cost.RareEarths > 0 && house.Treasury.RareEarths < cls.Cost.RareEarths * 2 && Resource.All.Where(r => r != Resource.RareEarths).All(r => house.Treasury.Get(r) >= cls.Cost.Get(r) * 2))
                {   // the hull wants rare earths the house has not got: buy them if the coin is comfortably there
                    var buy = new BuyGoodCommand(head.Id, Resource.RareEarths, cls.Cost.RareEarths * 2 - house.Treasury.RareEarths, Trade);
                    if (buy.Refusal(ctx) == null && house.Treasury.Credits >= buy.Cost(world) + cls.Cost.Credits * 2) executor.TryExecute(buy, ctx);
                }
                bool comfortable = Resource.All.All(r => house.Treasury.Get(r) >= cls.Cost.Get(r) * 2);
                if (!comfortable) continue;
                if (executor.TryExecute(new RaiseFleetCommand(head.Id, holding.Id, rules, classes, cid, Titles), ctx)) break;
            }
        }
    }

    /// <summary>Rival houses marry and, at war and ruthless, hunt heirs (GDD §14 "a kill-the-bloodline war is a
    /// terrifying lategame threat the enemy commits to"). One dynastic act per house per turn.</summary>
    private void Dynasty(List<Character> npcs, CommandExecutor executor, CommandContext ctx)
    {
        var world = ctx.World;
        var player = world.Protagonist; var playerHouse = player?.HouseId;
        foreach (var house in world.Houses.Values.OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            if (house.Id == playerHouse) continue;
            var head = npcs.FirstOrDefault(c => c.HouseId == house.Id);
            if (head == null) continue;

            // heir-hunting: at war, a Cruel or Ruthless head with agents to spare moves on the player's heirs — a
            // campaign with a cooldown (GDD §14 "agents + time per attempt"), not a knife every season
            string cooldownKey = $"hunt_cooldown:{house.Id}";
            if (player != null && playerHouse != null && world.AtWar(house.Id, playerHouse) && world.Counter(cooldownKey) <= world.Turn &&
                (head.NatureTraits.Contains("Cruel") || head.NatureTraits.Contains("Ruthless")))
            {
                var heir = world.LivingCharacters().Where(c => c.HouseId == playerHouse && c.Id != player.Id && !c.Flags.Contains(DynastySystem.HiddenFlag))
                                .OrderBy(c => c.Age).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
                if (heir != null && executor.TryExecute(new AssassinateCommand(head.Id, heir.Id), ctx)) { world.WorldCounters[cooldownKey] = world.Turn + HeirHuntCooldown; continue; }
            }

            // a hard head with a hatred does not wait for a war: now and then a knife for the ablest of a house whose head they cannot
            // abide, or with which there is blood already. War alone is not cause: that is settled with fleets. Other houses only (the player's heirs are hunted above, and only at war), never a
            // house's head, and never its last adult. What follows is the dead person's house's to answer (docs/FEUD.md).
            string knifeKey = $"knife_cooldown:{house.Id}";
            if (world.Counter(knifeKey) <= world.Turn && (head.NatureTraits.Contains("Cruel") || head.NatureTraits.Contains("Ruthless") || head.NatureTraits.Contains("Vengeful")))
            {
                world.WorldCounters[knifeKey] = world.Turn + KnifeRetry;
                var hated = world.Houses.Values.Where(o => o.Id != house.Id && o.Id != playerHouse && !World.Outsider(o.Id))
                    .Select(o => (o, oh: Economy.HeadOf(world, o))).Where(x => x.oh != null && (FeudSystem.Between(world, house.Id, x.o.Id) || (world.GetRelationship(head.Id, x.oh!.Id)?.Disposition ?? 0) <= KnifeHatred))
                    .OrderBy(x => world.GetRelationship(head.Id, x.oh!.Id)?.Disposition ?? 0).ThenBy(x => x.o.Id, StringComparer.Ordinal).Select(x => (x.o, x.oh)).FirstOrDefault();
                var mark = hated.o == null ? null : world.LivingCharacters().Where(c => c.HouseId == hated.o.Id && c.Id != hated.oh!.Id && c.Age >= DynastySystem.AgeOfMajority && !c.Flags.Contains(DynastySystem.HiddenFlag))
                    .OrderByDescending(c => c.CareerRank).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
                if (mark != null && executor.TryExecute(new AssassinateCommand(head.Id, mark.Id), ctx)) { world.WorldCounters[knifeKey] = world.Turn + KnifeCooldown; world.WorldCounters["knives_between_houses"] = world.Counter("knives_between_houses") + 1; continue; }
            }

            // marriage: an unmarried adult of the house courts the best-disposed eligible match anywhere
            var suitor = npcs.Where(c => c.HouseId == house.Id && c.Age >= DynastySystem.AgeOfMajority && c.Sex != "" && !ProposeMarriageCommand.IsMarried(world, c.Id))
                             .OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
            if (suitor == null) continue;
            // a house does not marry off everyone it has in the first spring: it looks for a match about one year in three, so
            // there is still someone unwed in the Reach when the player (or a house that was slow) comes looking
            uint mh = 2166136261; foreach (var ch in house.Id + ":match:" + world.Turn) mh = (mh ^ ch) * 16777619;
            if (mh % 3 != 0) continue;
            int Score(Character c)
            {
                var woman = c.Sex == "female" ? c : suitor;
                int heirs = woman.Age <= DynastySystem.FertileMax ? 40 : -40;          // a match that can bear heirs
                int closeness = -Math.Abs(c.Age - suitor.Age);
                int disp = world.GetRelationship(c.Id, suitor.Id)?.Disposition ?? 0;
                return heirs + closeness + disp;
            }
            // a match must make sense as a marriage: no more than twenty years between them, and a woman past bearing does not
            // court a man with no child (a house that needs heirs is not served by it, and a steward knows that)
            bool Fit(Character c)
            {
                var woman = c.Sex == "female" ? c : suitor; var man = c.Sex == "female" ? suitor : c;
                if (Math.Abs(c.Age - suitor.Age) > 20) return false;
                return woman.Age <= DynastySystem.FertileMax || world.Characters.Values.Any(k => k.Alive && k.FatherId == man.Id);
            }
            var match = world.LivingCharacters()
                .Where(c => c.HouseId != house.Id && c.Sex != "" && c.Sex != suitor.Sex && c.Age >= DynastySystem.AgeOfMajority && !ProposeMarriageCommand.IsMarried(world, c.Id)
                            && !c.Flags.Contains(ProposeMarriageCommand.SuitorFlag) && Fit(c))
                .OrderByDescending(Score).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
            if (match != null) executor.TryExecute(new ProposeMarriageCommand(head.Id, suitor.Id, match.Id, Titles), ctx);
        }
    }

    /// <summary>Rival houses develop their (abstracted, GDD §3) domains: each NPC house's senior living
    /// member slots the first affordable, fitting building — at most one build per house per turn, in id
    /// order, so rival realms grow deterministically instead of sitting on hoarded treasuries.</summary>
    private void Invest(List<Character> npcs, HoldingCatalog catalog, CommandExecutor executor, CommandContext ctx)
    {
        var world = ctx.World;
        var playerHouse = world.Protagonist?.HouseId;
        var inOrder = catalog.Buildings.OrderByDescending(b => b.Megastructure).ThenByDescending(b => b.RequiresDeposit != null).ThenBy(b => b.Id, StringComparer.Ordinal).ToList();
        foreach (var house in world.Houses.Values.OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            if (house.Id == playerHouse) continue;
            var steward = npcs.FirstOrDefault(c => c.HouseId == house.Id);
            if (steward == null) continue;

            // an insolvent steward raises coin where it can (GDD §17 "the one you run out of"): sells surplus materials or
            // exotics on the exchange, and tightens the tithe; a solvent one with a restless world eases it again.
            if (house.Treasury.Credits < 0)
            {
                // exotics first (dearest), then materials beyond a small reserve, then any strategic stock beyond a year's need
                if (house.Treasury.Exotics >= 1) executor.TryExecute(new SellGoodCommand(steward.Id, Resource.Exotics, Math.Min(3, house.Treasury.Exotics), Trade), ctx);
                else if (house.Treasury.Materials >= 5) executor.TryExecute(new SellGoodCommand(steward.Id, Resource.Materials, Math.Clamp(house.Treasury.Materials - 2, 1, 12), Trade), ctx);
                else if (house.Treasury.RareEarths >= 2) executor.TryExecute(new SellGoodCommand(steward.Id, Resource.RareEarths, house.Treasury.RareEarths / 2, Trade), ctx);
                if (catalog.Tax("harsh") != null && !world.HoldingsOf(house.Id).Any(h => h.Unrest >= 60)) executor.TryExecute(new SetTaxPolicyCommand(steward.Id, "harsh", catalog), ctx);
            }
            else if (house.TaxPolicy == "harsh" && house.Treasury.Credits > 10 && catalog.Tax("normal") != null) executor.TryExecute(new SetTaxPolicyCommand(steward.Id, "normal", catalog), ctx);
            else if (house.TaxPolicy != "lenient" && house.Treasury.Credits > 25 && catalog.Tax("lenient") != null && world.HoldingsOf(house.Id).Any(h => h.Unrest >= 60)) executor.TryExecute(new SetTaxPolicyCommand(steward.Id, "lenient", catalog), ctx);

            // a rich house with a market buys the materials it lacks (GDD §17 conversion chains)
            if (house.Treasury.Materials < 6 && house.Treasury.Credits > 20)
            {
                var buy = new BuyGoodCommand(steward.Id, Resource.Materials, 4, Trade);
                if (buy.Refusal(ctx) == null && buy.Cost(world) <= 12) executor.TryExecute(buy, ctx);   // not at any price: a spike is waited out
            }
            // and a very rich great house raises a megastructure (its holdings' build loop below prefers them when affordable)

            bool built = false;
            using (Diagnostics.Perf.Time("npc:build")) foreach (var holding in world.HoldingsOf(house.Id).OrderBy(h => h.Id, StringComparer.Ordinal))
            {
                foreach (var b in inOrder)   // work the deposit before the rest
                {
                    if (b.Megastructure && house.Treasury.Credits < b.Cost.Credits * 2) continue;      // only when it can afford to show off
                    if (executor.TryExecute(new BuildCommand(steward.Id, holding.Id, b.Id, catalog, Titles), ctx)) { built = true; break; }
                }
                if (built) break;
            }

            if (!built && !World.Outsider(house.Id)) using (Diagnostics.Perf.Time("npc:works")) Works(house, steward, catalog, executor, ctx);
            using (Diagnostics.Perf.Time("npc:venture")) Venture(house, steward, catalog, executor, ctx);
        }
    }

    /// <summary>A rival house builds on the bodies of its systems and settles what lies empty beside it (docs/SYSTEMS.md, docs/BUILDINGS.md).
    /// About one year in three, by a hash, a house with coin to spare builds the first thing it can on a body of a system it holds:
    /// buildings before great works, what works a deposit first; a great work only when it could pay for most of it today. About one year
    /// in four it lays the next level of its seat's infrastructure. About one year in six, a house with the coin sends a colony to an
    /// empty system a lane from a world of its own. One of these a year at most.</summary>
    private void Works(House house, Character steward, HoldingCatalog catalog, CommandExecutor executor, CommandContext ctx)
    {
        var world = ctx.World; var g = world.Galaxy; if (g == null || catalog.Works.Count == 0) return;
        uint h = 2166136261; foreach (var ch in house.Id + ":works:" + world.Turn) h = (h ^ ch) * 16777619;
        var seats = world.HoldingsOf(house.Id).Where(x => x.IsPlanet && !x.InRebellion && x.SystemId.Length > 0).OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
        if (h % 3 == 0 && house.Treasury.Credits >= 24 && seats.Count > 0)
        {
            _worksInOrder ??= catalog.Works.OrderBy(d => d.IsMegastructure).ThenByDescending(d => d.Deposit.Length > 0).ThenByDescending(d => d.Feature.Length > 0).ThenBy(d => d.Id, StringComparer.Ordinal).ToList();   // buildings before great works; what works a deposit first; of great works, what only this body allows
            foreach (var seat in seats)
            {
                var sys = g.System(seat.SystemId); if (sys == null) continue;
                for (int i = 0; i < sys.Bodies.Count; i++)
                    foreach (var def in _worksInOrder)
                    {
                        if (!SystemWorks.Suits(sys, sys.Bodies[i], def, world.PlanetOf(sys.Id))) continue;
                        if (def.IsMegastructure ? house.Treasury.Credits < def.Cost.Credits + 30 || house.Treasury.Materials < def.Cost.Materials / 2 : house.Treasury.Credits < def.Cost.Credits + 12) continue;   // a cushion kept; a great work only by a house that could pay for most of it today
                        if (executor.TryExecute(new BuildWorkCommand(steward.Id, sys.Id, i, def.Id, catalog, Titles), ctx)) return;
                    }
            }
        }
        // about one year in four it lays the next level of some infrastructure at its seat: the cheapest it can, civil works first
        if (h % 4 == 2 && house.Treasury.Credits >= 30 && seats.Count > 0)
            foreach (var t in catalog.Infrastructure.OrderBy(t => SystemWorks.Level(world, seats[0].SystemId, t.Id)).ThenBy(t => t.Id == "civil_works" ? 0 : 1).ThenBy(t => t.Id, StringComparer.Ordinal))
            {
                int next = SystemWorks.Level(world, seats[0].SystemId, t.Id) + 1; if (next > t.MaxLevel || house.Treasury.Credits < t.CostOf(next).Credits + 20) continue;
                if (executor.TryExecute(new InvestInfrastructureCommand(steward.Id, seats[0].SystemId, t.Id, catalog), ctx)) return;
            }
        // about one year in four it builds in a province it was given: the cheapest thing that suits, where there is a slot
        if (h % 4 == 1 && house.Treasury.Credits >= 30)
            foreach (var (pw, pvi) in world.ProvincesOf(house.Id))
            {
                if (pvi >= pw.Provinces.Count || g.System(pw.SystemId) is not { } psys || pw.Provinces[pvi].Buildings.Count + ProvinceSystem.Rising(world, pw, pvi) >= ProvinceSystem.Slots(world, catalog, pw, pvi)) continue;
                foreach (var def in catalog.Works.Where(d => ProvinceSystem.Suits(catalog, psys, pw.Provinces[pvi], d) && house.Treasury.Credits >= d.Cost.Credits + 15).OrderBy(d => d.Cost.Credits + d.Cost.Materials).ThenBy(d => d.Id, StringComparer.Ordinal))
                    if (executor.TryExecute(new BuildInProvinceCommand(steward.Id, pw.Id, pvi, def.Id, catalog), ctx)) return;
            }
        // about one year in five it takes a building a level higher, where the system's development lets it: its seat's own first
        if (h % 5 == 3 && house.Treasury.Credits >= 40)
            foreach (var seat in seats)
            {
                foreach (var bid in seat.Buildings)
                    if (catalog.Building(bid) is { Megastructure: false } bd && house.Treasury.Credits >= bd.Cost.Credits * seat.LevelOf(bid) + 25 && executor.TryExecute(new UpgradeBuildingCommand(steward.Id, seat.Id, bid, catalog), ctx)) return;
                foreach (var x in SystemWorks.In(world, seat.SystemId).ToList())
                    if (catalog.Work(x.DefId) is { IsMegastructure: false } wd && house.Treasury.Credits >= wd.Cost.Credits * Math.Max(1, x.Level) + 25 && executor.TryExecute(new UpgradeBuildingCommand(steward.Id, SystemWorks.Target(x.SystemId, x.Body), x.DefId, catalog), ctx)) return;
            }
        // and one year in seven a rich house with a full home world pays to clear ground on it
        if (h % 7 == 4 && seats.Count > 0 && g.System(seats[0].SystemId) is { } home && home.Bodies.FindIndex(b => b.IsPrimary) is >= 0 and var pi
            && SystemWorks.FreeSlots(world, catalog, home, pi) <= 0 && house.Treasury.Credits >= catalog.Rules.ClearCostOf(SystemWorks.Cleared(world, home.Id, pi)).Credits + 40
            && executor.TryExecute(new ClearSlotCommand(steward.Id, home.Id, pi, catalog), ctx)) return;
        if (h % 6 == 1 && house.Treasury.Credits >= catalog.Colony.Cost.Credits + 20 && seats.Count > 0)
            foreach (var target in seats.SelectMany(x => g.Neighbors(x.SystemId)).Distinct().OrderBy(x => x, StringComparer.Ordinal))
                if (world.PlanetOf(target) == null && executor.TryExecute(new FoundColonyCommand(steward.Id, target, catalog), ctx)) return;
    }
    private List<WorkDef>? _worksInOrder;

    /// <summary>The steward's wider ledger (GDD §17): a house deep in the red borrows; a solvent one with a slack world
    /// invests in it; a house with coin to spare stakes a belt claim near home (or anywhere, if it holds nothing), founds
    /// one venture where the catalog lets it, and expands the one it has. One of each a turn at most, in id order, so
    /// rival fortunes grow steadily and reproducibly.</summary>
    private int _fuelHullsPerUnit = 3; private HoldingCatalog? _catalog;

    /// <summary>Rival stewards legislate (GDD §3 abstracted rivals; POLITICS.md): about one house in eight a turn, by a hash, looks at its
    /// position and changes one law — relaxes whatever its hostile Commons hate most, tightens the tithe when the coffers are empty,
    /// conscripts and doubles the guard at war, declares martial law over a lawless region it dominates, raises the Reach's tolls when
    /// it holds the chair and is poor. One change a turn a house; the law's cooldown keeps it from flapping.</summary>
    private void Legislate(House house, Character steward, CommandExecutor executor, CommandContext ctx)
    {
        var cat = Policies; if (cat == null || cat.IsEmpty) return; var world = ctx.World;
        uint hash = 2166136261; foreach (var ch in house.Id + ":law:" + world.Turn) hash = (hash ^ ch) * 16777619;
        if (hash % 8 != 0) return;
        bool Try(string law, string option) => cat.Law(law)?.Option(option) != null && PolicySystem.OptionId(world, cat, house.Id, law) != option && executor.TryExecute(new EnactLawCommand(steward.Id, law, option, cat), ctx);
        // 1. a hostile estate: relax the law of ours it hates most (house and world laws only — the cheap ones)
        foreach (var e in cat.Estates.Where(e => PolicySystem.Approval(world, house.Id, e.Id) <= PolicySystem.Hostile).OrderBy(e => PolicySystem.Approval(world, house.Id, e.Id)))
        {
            var worst = cat.Laws.Where(l => (l.Scope == "house" || l.Scope == "world") && PolicySystem.HoldsScope(world, house.Id, l.Scope))
                .Select(l => (l, o: PolicySystem.Option(world, cat, house.Id, l.Id)!)).Where(x => x.o.Opinions.GetValueOrDefault(e.Id) < 0).OrderBy(x => x.o.Opinions.GetValueOrDefault(e.Id)).FirstOrDefault();
            if (worst.l == null) continue;
            var better = worst.l.Options.OrderByDescending(o => o.Opinions.GetValueOrDefault(e.Id)).First();
            if (Try(worst.l.Id, better.Id)) return;
        }
        // 2. restless worlds: ease the law of ours that drives unrest hardest (a steward reads the squares before the ledger)
        if (world.HoldingsOf(house.Id).Any(h => h.IsPlanet && h.Unrest >= 50))
        {
            var driver = cat.Laws.Where(l => (l.Scope == "house" || l.Scope == "world") && PolicySystem.HoldsScope(world, house.Id, l.Scope))
                .Select(l => (l, o: PolicySystem.Option(world, cat, house.Id, l.Id)!)).Where(x => x.o.Effects.GetValueOrDefault("unrest_drift") > 0).OrderByDescending(x => x.o.Effects.GetValueOrDefault("unrest_drift")).FirstOrDefault();
            if (driver.l != null) { var calmer = driver.l.Options.OrderBy(o => o.Effects.GetValueOrDefault("unrest_drift")).ThenByDescending(o => o.Default).First(); if (Try(driver.l.Id, calmer.Id)) return; }
        }
        // 3. the position
        bool atWar = world.Wars.Any(x => x.Involves(house.Id));
        if (house.Treasury.Credits < 0 && Try("tithe", "harsh")) return;
        if (house.Treasury.Credits > 120 && PolicySystem.OptionId(world, cat, house.Id, "tithe") == "harsh" && Try("tithe", "normal")) return;
        if (atWar && Try("levies", "conscription")) return;
        if (atWar && Try("household_guard", "doubled")) return;
        if (!atWar && PolicySystem.OptionId(world, cat, house.Id, "levies") == "conscription" && Try("levies", "household")) return;
        var lawless = PolicySystem.RegionsDominated(world, house.Id).FirstOrDefault(r => RegionSystem.StateOf(world, r).Lawlessness >= 60);
        if (lawless != null && Try("march_law", "martial_law")) return;
        if (lawless == null && PolicySystem.OptionId(world, cat, house.Id, "march_law") == "martial_law" && PolicySystem.RegionsDominated(world, house.Id).All(r => RegionSystem.StateOf(world, r).Lawlessness < 30) && Try("march_law", "patrols")) return;
        if (PolicySystem.HoldsScope(world, house.Id, "chair") && house.Treasury.Credits < 40 && Try("reach_tolls", "heavy")) return;
        if (PolicySystem.HoldsScope(world, house.Id, "office:admiral") && atWar && Try("fleet_doctrine", "aggressive")) return;
        // 4. nothing presses: the house brings one of its own laws to what its bench stands for (docs/LAWS_REVIEW.md: over two games of
        //    thirty years fifty-eight houses changed a law eighteen times, because a house only ever legislated when something was wrong).
        //    Not where the change would turn one of its own estates against it, and not a law the whole Reach holds in common.
        if (FactionSystem.Of(world, house.Id) is not { } bench) return;
        foreach (var (lawId, want) in bench.Laws.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var law = cat.Law(lawId); if (law == null || law.Scope == "senate" || !PolicySystem.HoldsScope(world, house.Id, law.Scope)) continue;
            var now = PolicySystem.Option(world, cat, house.Id, lawId); var next = law.Option(want); if (now == null || next == null || now.Id == next.Id) continue;
            if (!law.Reach && cat.Estates.Any(e => PolicySystem.LawOpinion(world, cat, house.Id, e.Id) - now.Opinions.GetValueOrDefault(e.Id) + next.Opinions.GetValueOrDefault(e.Id) <= PolicySystem.Hostile + PlatformMargin)) continue;
            if (next.Effects.GetValueOrDefault("credits_per_world") < 0 && house.Treasury.Credits < PlatformCoffers) continue;   // a law that costs coin waits for a full treasury
            if (Try(lawId, want)) return;
        }
        // the leader of a bench gives it a platform: reform where the bench courts the commons, tradition where it courts the old blood
        if (FactionSystem.Leads(world, house.Id) && PolicySystem.OptionId(world, cat, house.Id, "bench_platform") == "loose")
        {
            if (bench.Estates.Contains("commons") && Try("bench_platform", "reform")) return;
            if (bench.Estates.Contains("old_blood") && Try("bench_platform", "tradition")) return;
        }
    }
    /// <summary>A house does not follow its bench into a law that would leave one of its own estates within this of hostile; nor into
    /// one that costs coin a world with fewer than this in the treasury.</summary>
    public const int PlatformMargin = 5, PlatformCoffers = 10;

    /// <summary>Fuel before finery (GDD §17): every rival house with a navy and dry or low tanks buys helium-3 on the open market before it
    /// spends a pool point on anything else — three turns' worth when the coin is comfortable, one turn's worth when it is not.</summary>
    private void Refuel(List<Character> npcs, HoldingCatalog catalog, CommandExecutor executor, CommandContext ctx)
    {
        var world = ctx.World;
        foreach (var house in world.Houses.Values.Where(h => h.Id != world.Protagonist?.HouseId && !World.Outsider(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            int hulls = world.Fleets.Values.Where(f => f.OwnerId == house.Id).Sum(f => f.Strength);
            if (hulls == 0 && !world.HoldingsOf(house.Id).Any(h => h.SystemId.Length > 0)) continue;   // nothing to fuel and nowhere to lay a keel
            int need = Math.Max(1, FleetSystem.FuelBillFor(world, house.Id, _fuelHullsPerUnit));   // a house without a navy still wants a tank in hand before it lays down a keel
            if (house.Treasury.Helium3 >= need * 2) continue;
            var head = Economy.HeadOf(world, house); if (head == null) continue;
            var buy = new BuyGoodCommand(head.Id, Resource.Helium3, need * 3, Trade);
            if (buy.Refusal(ctx) == null && house.Treasury.Credits >= buy.Cost(world) + 4) { executor.TryExecute(buy, ctx); continue; }
            var one = new BuyGoodCommand(head.Id, Resource.Helium3, need, Trade); if (one.Refusal(ctx) == null) executor.TryExecute(one, ctx);
        }
    }
    /// <summary>The purse in politics (docs/REPUBLIC.md stage 3). A house of commerce with no seat buys one from the census. A rich house
    /// takes a needy house of its own party as a client, about one year in six. In the year before an election a rich house of the
    /// Factors spends on the voters in its party's nominee's name.</summary>
    private void Patronage(House house, Character steward, CommandExecutor executor, CommandContext ctx)
    {
        var os = Orders; if (os == null) return; var world = ctx.World; if (world.IsImperium) return;
        uint h = 2166136261; foreach (var ch in house.Id + ":patron:" + world.Turn) h = (h ^ ch) * 16777619;
        if (house.Treasury.Credits >= OrderSystem.CensusFee + 40) executor.TryExecute(new EnrollCommand(steward.Id, os, Titles), ctx);
        if (house.PatronId.Length == 0 && house.Treasury.Credits >= 80 && h % 6 == 2 && Wealth.TierIndex(Wealth.TierFor(world, house.Id)) >= 3)
        {
            var party = FactionSystem.Of(world, house.Id); var mine = world.Protagonist?.HouseId;
            var pick = world.Houses.Values.Where(c => c.Id != mine && c.Id != house.Id && os.TakeClientRefusal(world, house, c) == null && (party == null || party.Members.Contains(c.Id)))
                .OrderBy(c => OrderSystem.ClientCost(c)).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
            if (pick != null) executor.TryExecute(new ClientCommand(steward.Id, ClientCommand.Take, pick.Id, os), ctx);
        }
        if (world.Turn % ElectionSystem.Every == ElectionSystem.Every - 1 && house.Treasury.Credits >= 120 && FactionSystem.Of(world, house.Id) is { PartyId: "factors" } f)
        {
            var nominee = f.Members.Contains(house.Id) && house.Order(OrderSystem.Commerce) >= OrderSystem.GreatAt ? house.Id : f.LeaderHouseId;
            if (nominee.Length > 0) executor.TryExecute(new CanvassCommand(steward.Id, nominee, os, _ => true), ctx);
        }
    }

    /// <summary>A steward on the Exchange (docs/ECONOMY_MARKET.md stage C). A house in the red sells paper, which is what a crash is. A
    /// director spends the company's cash on enlarging what it owns, floats shares when the cash is short, and buys back toward a
    /// majority when its hold is thin. A house with two ventures lists one, the Factors soonest. A house with coin to spare buys what
    /// is cheap against what it earns: the Factors every third year, the Sword never; a house that hates the player prefers the
    /// player's company.</summary>
    private void Invest(House house, Character steward, CommandExecutor executor, CommandContext ctx)
    {
        var cs = Exchange; if (cs == null || !cs.Open) return; var world = ctx.World; var r = cs.Rules;
        uint h = 2166136261; foreach (var ch in house.Id + ":paper:" + world.Turn) h = (h ^ ch) * 16777619;
        string party = FactionSystem.Of(world, house.Id)?.PartyId ?? ""; var mine = world.Protagonist?.HouseId;
        // the throne's hand, under the Imperium: a monopoly to the house it likes best, and (the Edict at proscriptions) a hated house's paper forfeit
        if (world.IsImperium && ThroneSystem.Emperor(world)?.Id == house.Id && Economy.HeadOf(world, house) is { } throne)
        {
            int Regard(House o) => Economy.HeadOf(world, o) is { } oh ? world.GetRelationship(throne.Id, oh.Id)?.Disposition ?? 0 : 0;
            var others = world.Houses.Values.Where(o => o.Id != house.Id && !World.Outsider(o.Id)).OrderByDescending(Regard).ThenBy(o => o.Id, StringComparer.Ordinal).ToList();
            var free = world.Companies.Where(c => c.Chartered && c.MonopolyOf.Length == 0).OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
            if (free != null && h % 7 == 3 && others.FirstOrDefault(o => Regard(o) >= 20 && world.Companies.All(c => c.MonopolyOf != o.Id)) is { } favourite)
                executor.TryExecute(new ThroneExchangeCommand(steward.Id, ThroneExchangeCommand.GrantAct, free.Id, favourite.Id, cs, Policies ?? PolicyCatalog.Empty), ctx);
            if (h % 5 == 2 && others.LastOrDefault(o => Regard(o) <= -40 && CompanySystem.PaperOf(world, o.Id) > 0) is { } hated)
                executor.TryExecute(new ThroneExchangeCommand(steward.Id, ThroneExchangeCommand.SeizeAct, "", hated.Id, cs, Policies ?? PolicyCatalog.Empty), ctx);
        }
        // the Treasurer of the Reach opens the purse under a falling market
        if (cs.SteadyRefusal(world, house.Id) == null && world.WorldFlags.Contains(CompanySystem.CrashFlag)) executor.TryExecute(new SteadyMarketCommand(steward.Id, cs), ctx);
        // a house with coin lends to the Reach for its war abroad: the Factors and the Optimates for the coupon, the Sword for the war
        if (NationSystem.AtWar(world) != null && house.Treasury.Credits >= 150 && h % (party == "populares" ? 6u : 3u) == 1 && cs.BondRefusal(world, house.Id) == null) executor.TryExecute(new BuyBondCommand(steward.Id, cs), ctx);
        if (house.Treasury.Credits < 0)
        {
            var held = world.Companies.Where(c => c.HeldBy(house.Id) > 0).OrderByDescending(c => CompanySystem.Value(c, house.Id)).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
            if (held != null) executor.TryExecute(new SellSharesCommand(steward.Id, held.Id, Math.Min(held.HeldBy(house.Id), Math.Max(5, held.Shares * 15 / 100)), cs), ctx);
            return;
        }
        foreach (var co in CompanySystem.ControlledBy(world, house.Id).Where(c => !c.Chartered).ToList())
        {
            bool grew = false, wants = false;
            foreach (var b in CompanySystem.VenturesOf(world, co).ToList())
            {
                var def = _ventures.Def(b.Kind); if (def == null || b.Level >= def.MaxLevel || ProjectSystem.Underway(world, house.Id, Project.Expansion, b.Kind, b.Id)) continue;
                wants = true; if (executor.TryExecute(new ExpandBusinessCommand(steward.Id, b.Id, _ventures), ctx)) { grew = true; break; }
            }
            // short of cash for what it wants to build: sell new shares, while that leaves the house its majority
            if (wants && !grew && h % 3 == 0) { int n = Math.Max(1, co.Shares * 15 / 100); if (co.HeldBy(house.Id) * 100 / (co.Shares + n) >= 55) executor.TryExecute(new BoardCommand(steward.Id, co.Id, BoardCommand.Float, n, cs), ctx); }
            // nothing left to enlarge and coin in hand: the company founds another venture of its own trade where it can, about one year in three
            if (!wants && co.Cash >= 40 && h % 3 == 1 && world.Galaxy != null && !world.Projects.Any(p => p.CompanyId == co.Id))
            {
                var founding = _ventures.Defs.Where(d => d.Sector == co.Sector).OrderBy(d => d.Id, StringComparer.Ordinal)
                    .SelectMany(d => world.HoldingsOf(house.Id).Select(x => x.SystemId).Where(x => x.Length > 0).Distinct().OrderBy(x => x, StringComparer.Ordinal).Select(sys => (d, sys)))
                    .Select(x => new FoundBusinessCommand(steward.Id, x.d.Id, x.sys, _ventures, HoldingCatalogForFounding ?? HoldingCatalog.Empty, Titles, co.Id)).FirstOrDefault(c => c.Refusal(ctx) == null);
                if (founding != null && executor.TryExecute(founding, ctx)) continue;
            }
            // nothing left to build and coin piling up: it is paid out
            if (!wants && co.Cash > 30 && !world.Projects.Any(p => p.CompanyId == co.Id)) executor.TryExecute(new BoardCommand(steward.Id, co.Id, BoardCommand.Special, 0, cs), ctx);
            // a thin hold is shored up from the float
            if (co.HeldBy(house.Id) * 100 / Math.Max(1, co.Shares) < 55 && house.Treasury.Credits >= 80 && co.HeldBy(Company.Public) > 0)
                executor.TryExecute(new BuySharesCommand(steward.Id, co.Id, Math.Min(co.HeldBy(Company.Public), Math.Max(1, co.Shares * 5 / 100)), cs), ctx);
        }
        if (!CompanySystem.ControlledBy(world, house.Id).Any() && h % (party == "factors" ? 4u : 9u) == 1)
        {
            var loose = world.BusinessesOf(house.Id).Where(b => b.CompanyId.Length == 0 && _ventures.Def(b.Kind) != null).ToList();
            var best = loose.Count >= 2 ? loose.OrderByDescending(b => VentureTrade?.Earnings(world, b) ?? 0).ThenBy(b => b.Id, StringComparer.Ordinal).First() : null;
            if (best != null && (VentureTrade?.Earnings(world, best) ?? 0) > 0) executor.TryExecute(new IncorporateCommand(steward.Id, best.Id, "", cs), ctx);
        }
        // a stake becomes a siege: a house that already holds a tenth of a company another directs with less than half, and has the
        // coin, buys toward control year after year (the cap is a fifth a year, so the director has time to answer)
        if (house.Treasury.Credits >= 150 && party != "sword")
        {
            var prey = world.Companies.Where(c => !c.Chartered && c.ControllerId != house.Id && c.HeldBy(house.Id) * 10 >= c.Shares && c.HeldBy(c.ControllerId) * 2 < c.Shares && c.HeldBy(Company.Public) > 0
                                                  && !world.AtWar(house.Id, c.ControllerId))
                .OrderByDescending(c => c.HeldBy(house.Id) * 100 / Math.Max(1, c.Shares)).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
            if (prey != null)
            {
                int want = Math.Min(prey.HeldBy(Company.Public), Math.Max(1, prey.Shares * 8 / 100));
                if (executor.TryExecute(new BuySharesCommand(steward.Id, prey.Id, want, cs), ctx)) return;
            }
        }
        // a share that has run a quarter past what it earns is sold down: a third of the holding, never a director's own majority
        foreach (var co in world.Companies.Where(c => c.HeldBy(house.Id) >= 3 && c.ControllerId != house.Id).OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
            if (co.Price > cs.Fundamental(world, co) * 1.25 && h % 2 == 0) { executor.TryExecute(new SellSharesCommand(steward.Id, co.Id, Math.Max(1, co.HeldBy(house.Id) / 3), cs), ctx); break; }
        uint every = party == "factors" ? 3u : party == "sword" ? 0u : 6u;
        if (every == 0 || house.Treasury.Credits < 140 || h % every != 0) return;
        bool hostile = mine != null && Economy.HeadOf(world, house) is { } head && world.Protagonist is { } pc && (world.GetRelationship(head.Id, pc.Id)?.Disposition ?? 0) <= -30;
        var pick = world.Companies.Where(c => c.HeldBy(Company.Public) * 10 >= c.Shares && c.Price > 0.2 && (c.ControllerId.Length == 0 || !world.AtWar(house.Id, c.ControllerId)))
            .OrderByDescending(c => (hostile && c.ControllerId == mine ? 2 : 0) + cs.Fundamental(world, c) / c.Price).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
        if (pick == null || (cs.Fundamental(world, pick) < pick.Price * 0.9 && !(hostile && pick.ControllerId == mine))) return;   // nothing is cheap: the coin stays home
        int budget = (house.Treasury.Credits - 100) / 2; int n2 = Math.Min(Math.Min(pick.HeldBy(Company.Public), pick.Shares * 8 / 100), (int)(budget / Math.Max(0.2, pick.Price)));
        if (n2 >= 1) executor.TryExecute(new BuySharesCommand(steward.Id, pick.Id, n2, cs), ctx);
    }

    private void Venture(House house, Character steward, HoldingCatalog catalog, CommandExecutor executor, CommandContext ctx)
    {
        var world = ctx.World; var f = _ventures.Finance; var galaxy = world.Galaxy; var eco = new Economy(catalog, _ventures);
        var fc = eco.Forecast(world, house.Id); int net = fc.Credits, mpNet = fc.Manpower;   // a venture that eats men (a free company) is only worth it while the worlds breed more than it eats
        Legislate(house, steward, executor, ctx);
        // deep in the red with a venture to sell: sell the one that pays least before anything else (a loan on a sinking ledger is a spiral)
        if (house.Treasury.Credits < -10)
        {
            var worst = world.BusinessesOf(house.Id).Select(b => (b, pay: VentureNet(world, house, b))).OrderBy(x => x.pay).ThenBy(x => x.b.Id, StringComparer.Ordinal).FirstOrDefault();
            if (worst.b != null && worst.b.CompanyId.Length == 0 && (worst.pay <= 0 || house.Treasury.Credits < -25) && world.Turn >= worst.b.OfferedUntil)   // not while an offer of it still stands: the house offered has had its year
                executor.TryExecute(new SellVentureCommand(steward.Id, worst.b.Id, SellVentureCommand.Best, VentureTrade ?? new VentureSystem(eco, _ventures)), ctx);
        }
        // a loan is a bridge, not a floor: borrow only when the ledger is positive, so the instalments can be met
        if (house.Treasury.Credits < -5 && house.Debt == 0 && net > f.LoanInstalment) executor.TryExecute(new TakeLoanCommand(steward.Id, _ventures), ctx);   // the instalment must be payable out of income
        if (house.Debt > 0 && house.Treasury.Credits >= house.Debt + 15) executor.TryExecute(new RepayLoanCommand(steward.Id), ctx);
        int fuelNeed = FleetSystem.FuelBillFor(world, house.Id, _fuelHullsPerUnit);
        // a hoard is coin standing idle: sell helium-3 beyond six turns of burn (plus ten), rare earths beyond a dozen, materials beyond sixty, on the market
        foreach (var (res, keep) in new[] { (Resource.Helium3, fuelNeed * 6 + 10), (Resource.RareEarths, 12), (Resource.Materials, 60), (Resource.Exotics, 6) })
        {
            int surplus = house.Treasury.Get(res) - keep; if (surplus < 3) continue;
            if (!MarketSystem.FairPrice(world, Trade.Market, res)) continue;   // a hoard is not sold into a glut: the steward waits for a price
            executor.TryExecute(new SellGoodCommand(steward.Id, res, Math.Min(6, surplus), Trade), ctx);
        }
        // a rich house buys what pays (docs/ECONOMY_MARKET.md stage B): about one year in ten, by a hash, the venture it most wants
        // at its owner's asking price — what works its own world first, a distressed seller's before a comfortable one's
        if (VentureTrade != null && house.Treasury.Credits >= 120)
        {
            uint bh = 2166136261; foreach (var ch in house.Id + ":buy:" + world.Turn) bh = (bh ^ ch) * 16777619;
            if (bh % 10 == 0 && VentureTrade.Target(world, house.Id) is { } wanted) executor.TryExecute(new BuyVentureCommand(steward.Id, wanted.Id, VentureTrade), ctx);
        }
        // a poor house with two ventures or more sells the one that pays least to whoever will give most for it
        if (VentureTrade != null && house.Treasury.Credits < 10 && house.Treasury.Credits >= -10 && world.BusinessesOf(house.Id).Count(b => b.CompanyId.Length == 0) >= 2)
        {
            var least = world.BusinessesOf(house.Id).Where(b => b.CompanyId.Length == 0 && world.Turn >= b.OfferedUntil).OrderBy(b => VentureTrade.Earnings(world, b)).ThenBy(b => b.Id, StringComparer.Ordinal).FirstOrDefault();
            if (least != null && VentureTrade.Bidders(world, least).Count > 0) executor.TryExecute(new SellVentureCommand(steward.Id, least.Id, SellVentureCommand.Best, VentureTrade), ctx);
        }
        Invest(house, steward, executor, ctx);
        Patronage(house, steward, executor, ctx);
        if (house.Treasury.Credits >= f.InvestCredits + 14)
        {
            var slack = world.HoldingsOf(house.Id).Where(h => h.IsPlanet && h.Prosperity < 45).OrderBy(h => h.Prosperity).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
            if (slack != null) executor.TryExecute(new InvestCommand(steward.Id, slack.Id, _ventures), ctx);
        }
        if (galaxy == null) return;
        if (!world.HoldingsOf(house.Id).Any(h => h.IsPlanet))   // a landless house pays court to the crown, then asks it for land
        {
            var court = PetitionFiefCommand.Grantor(world, house.Id); var crown = court != null ? Economy.HeadOf(world, court) : null;
            if (crown != null && (world.GetRelationship(crown.Id, steward.Id)?.Disposition ?? 0) < 10) executor.TryExecute(new SeekAllyCommand(steward.Id, crown.Id), ctx);
            executor.TryExecute(new PetitionFiefCommand(steward.Id), ctx);
        }
        // a house whose land earns nothing must find its coin elsewhere: it stakes and founds on a thinner cushion
        int cushion = net <= 0 ? 2 : 10;
        // and it stakes no more than two belt claims unless the ledger is comfortably in the black: claims pay in ore, not coin
        if (house.Treasury.Credits >= f.ClaimCredits + cushion && house.Treasury.Materials >= f.ClaimMaterials && (world.HoldingsOf(house.Id).Count(h => h.IsClaim) < 2 || net > 3))
            foreach (var sys in galaxy.Systems.Values.Where(s => s.Belt > 0).OrderByDescending(s => s.Deposit(Resource.RareEarths)).ThenBy(s => s.Id, StringComparer.Ordinal))   // the rare-earth belts first
                if (executor.TryExecute(new StakeClaimCommand(steward.Id, sys.Id, catalog, _ventures), ctx)) break;
        if (_ventures.IsEmpty) return;
        var mine = world.BusinessesOf(house.Id).ToList();
        if (mine.Count < 3)
        {
            // every kind in every system it can afford, ranked by what it would clear in credits a turn; nothing that would lose coin
            var own = world.HoldingsOf(house.Id).Select(h => h.SystemId).Where(s => s.Length > 0).Distinct().ToHashSet();
            (string def, string sys, int pay)? best = null;
            foreach (var def in _ventures.Defs)
            {
                if (!Resource.All.All(r => house.Treasury.Get(r) >= def.Cost.Get(r) + (r == Resource.Credits ? cushion : 0))) continue;
                foreach (var sys in galaxy.Systems.Values.OrderBy(s => s.Id, StringComparer.Ordinal))
                {
                    if (def.Upkeep.Manpower > 0 && mpNet - def.Upkeep.Manpower < 1) continue;   // no men to spare: the yards come first
                    int pay = ProjectedNet(world, house, def, sys.Id) + (own.Contains(sys.Id) ? 1 : 0);
                    if (pay <= 0 || (best != null && pay <= best.Value.pay)) continue;   // (what it would clear is asked first: it is cheap, and whether it may be founded is not)
                    if (new FoundBusinessCommand(steward.Id, def.Id, sys.Id, _ventures, catalog, Titles).Refusal(ctx) != null) continue;
                    best = (def.Id, sys.Id, pay);
                }
            }
            if (best != null) executor.TryExecute(new FoundBusinessCommand(steward.Id, best.Value.def, best.Value.sys, _ventures, catalog, Titles), ctx);
        }
        foreach (var b in mine)
        {
            var def = _ventures.Def(b.Kind); if (def == null || b.Level >= def.MaxLevel || VentureNet(world, house, b) <= 0) continue;
            if (def.Upkeep.Manpower > 0 && mpNet - def.Upkeep.Manpower < 1) continue;
            if (house.Treasury.Credits >= def.ExpandCost.Credits + 16 && executor.TryExecute(new ExpandBusinessCommand(steward.Id, b.Id, _ventures), ctx)) break;
        }
    }

    /// <summary>Credits a turn a venture of this kind would clear for the house in a system at level one: yield and flat yield, less
    /// upkeep, less the charter (with the ring's bonus) if the world is another house's.</summary>
    private int ProjectedNet(World w, House house, BusinessDef def, string systemId)
    {
        var holder = w.PlanetHolder(systemId); int charter = holder != null && holder != house.Id ? def.Charter + (w.Galaxy?.RulesFor(w.Galaxy.RingOf(systemId)).CharterBonus ?? 0) : 0;
        return def.Yield.Credits + def.FlatYield.Credits - def.Upkeep.Credits - charter + StrategicWorth(def.Yield);
    }
    /// <summary>What a bundle's strategic yield would fetch: helium-3 two, rare earths three, isotopes six credits a unit (the exchange's selling rates).</summary>
    private static int StrategicWorth(Resources r) => r.Helium3 * 2 + r.RareEarths * 3 + r.Isotopes * 6;
    private int VentureNet(World w, House house, Business b) { var def = _ventures.Def(b.Kind); if (def == null) return 0; var holder = w.PlanetHolder(b.SystemId); int charter = holder != null && holder != house.Id ? def.Charter + (w.Galaxy?.RulesFor(w.Galaxy.RingOf(b.SystemId)).CharterBonus ?? 0) : 0; return (def.Yield.Credits - def.Upkeep.Credits - charter + StrategicWorth(def.Yield)) * b.Level + def.FlatYield.Credits; }

    /// <summary>The first ambitious NPC (id order) lights the fuse on the first causable crisis
    /// (id order) — a deliberate, ambition-serving onset. At most one authored crisis per turn,
    /// deterministically.</summary>
    private static void AuthorCrisis(List<Character> npcs, CrisisEngine crises,
        CommandExecutor executor, CommandContext ctx)
    {
        var causable = crises.Causable(ctx.World, ctx.Rng);
        if (causable.Count == 0) return;

        foreach (var npc in npcs)
        {
            if (!IsSchemer(npc)) continue;
            executor.TryExecute(new TriggerCrisisCommand(npc.Id, crises, causable[0]), ctx);
            return;   // one authored crisis per turn
        }
    }

    /// <summary>An NPC willing to weaponise a crisis: ambitious by nature or by goal.</summary>
    private static bool IsSchemer(Character npc) =>
        npc.NatureTraits.Contains("Ambitious") || npc.NatureTraits.Contains("Arrogant") ||
        npc.NatureTraits.Contains("Ruthless") || npc.NatureTraits.Contains("Vengeful") ||
        npc.Ambition is "seize_the_throne" or "master_the_senate" or
                        "outshine_all_rivals" or "restore_house_fortunes";

    private static ICommand? ChooseAction(Character npc, World world)
    {
        var others = world.LivingCharacters()
            .Where(c => c.Id != npc.Id)
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .ToList();
        if (others.Count == 0) return null;

        int Disp(string toId) => world.GetRelationship(npc.Id, toId)?.Disposition ?? 0;

        // Aggressive temperament: undermine whoever they like least.
        bool aggressive = npc.NatureTraits.Contains("Arrogant")
            || npc.NatureTraits.Contains("Ambitious")
            || npc.Ambition == "outshine_all_rivals";

        // Conciliatory temperament: court whoever they already like best.
        bool conciliatory = npc.NatureTraits.Contains("Honorable")
            || npc.AptitudeTraits.Contains("Diplomat");

        if (aggressive)
        {
            var target = others.OrderBy(c => Disp(c.Id)).ThenBy(c => c.Id, StringComparer.Ordinal).First();
            return new NeedleRivalCommand(npc.Id, target.Id);
        }
        if (conciliatory)
        {
            var target = others.OrderByDescending(c => Disp(c.Id)).ThenBy(c => c.Id, StringComparer.Ordinal).First();
            return new SeekAllyCommand(npc.Id, target.Id);
        }

        // Otherwise nothing: what they study, and what they learn by their work, is the year's own reckoning now (docs/EDUCATION.md).
        return null;
    }
}
