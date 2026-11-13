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
        if (catalog != null) Refuel(npcs, catalog, executor, ctx);
        if (catalog is { IsEmpty: false }) Invest(npcs, catalog, executor, ctx);
        if (fleetRules != null && world.Galaxy != null) Sail(npcs, fleetRules, executor, ctx);

        Dynasty(npcs, executor, ctx);

        foreach (var npc in npcs)
        {
            var command = ChooseAction(npc, world);
            if (command != null)
                executor.TryExecute(command, ctx);
        }

        if (crises != null) AuthorCrisis(npcs, crises, executor, ctx);
    }

    /// <summary>Rival houses keep ships (GDD §3 abstracted realms): a house with a comfortable treasury lays
    /// down a hull at its seat; a hostile head (disposition &lt; −40 toward the protagonist) with a rested fleet
    /// sends it toward the protagonist's seat — the menace the map is for. One order per house per turn.</summary>
    /// <summary>A rival house's tactics, deterministic: a fleet that outguns every enemy it could meet presses, one outgunned skirmishes, the rest
    /// hold the line; a tactician with manpower to spare boards; torpedoes are loaded at home when the house holds two isotopes or more; a house
    /// at war with no campaign names the nearest enemy world; a head on the military track leads from the front when young and from the flag when
    /// a Commander or better.</summary>
    private void Posture(World world, House house, Character head, FleetRules rules, CommandExecutor executor, CommandContext ctx)
    {
        var enemies = world.EnemiesOf(house.Id).ToList();
        Expedition(world, house, head, rules, executor, ctx, enemies.Count);
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
        if (Nations == null || Nations.IsEmpty || enemiesAtHome > 0 || NationSystem.AtWar(world) is not { } war) return;
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
                if (garrison != null) executor.TryExecute(new MoveFleetCommand(head.Id, garrison.Id, rising.SystemId, rules), ctx);
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
                if (world.AtWar(house.Id, stolen.OwnerId) && enemyAt != null && executor.TryExecute(new MoveFleetCommand(head.Id, enemyAt.Id, stolen.SystemId, rules), ctx)) continue;
            }

            // raiders near home are hunted (GAP §6): a fleet of two or more sails on raiders sitting within a lane of a world of the house's
            var nearHome = world.HoldingsOf(house.Id).Where(h => h.SystemId.Length > 0).SelectMany(h => world.Galaxy!.Neighbors(h.SystemId).Append(h.SystemId)).ToHashSet();
            var raiders = world.Fleets.Values.Where(f => f.OwnerId == PirateSystem.HouseId && !f.Moving && nearHome.Contains(f.SystemId)).OrderBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
            if (raiders != null)
            {
                var hunter = world.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.Moving && f.Strength > raiders.Strength && f.SystemId != raiders.SystemId).OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
                if (hunter != null && executor.TryExecute(new MoveFleetCommand(head.Id, hunter.Id, raiders.SystemId, rules), ctx)) continue;
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
                if (fleet != null) executor.TryExecute(new MoveFleetCommand(head.Id, fleet.Id, world.Galaxy.Capital, rules), ctx);
                continue;
            }

            // menace: a hostile (or belligerent), rested house fleet moves on the player's seat
            if (seat != null && player != null && (atWar || disposition < -40))
            {
                var fleet = world.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.Moving && f.Readiness >= 80 && f.SystemId != seat)
                                               .OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
                if (fleet != null && executor.TryExecute(new MoveFleetCommand(head.Id, fleet.Id, seat, rules), ctx)) continue;
            }

            // against any enemy (GAP §7 "wars are bloodless"): hold the chokepoint between home and the enemy if it is unguarded, else strike the enemy's weakest world
            var enemy = world.EnemiesOf(house.Id).Where(e => e != playerHouse && e != PirateSystem.HouseId).OrderBy(e => e, StringComparer.Ordinal).FirstOrDefault();
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
                    if (target != null && target != strike.SystemId && executor.TryExecute(new MoveFleetCommand(head.Id, strike.Id, target, rules), ctx)) continue;
                }
            }

            // an unled fleet gets the house's best military mind as admiral (free of pool cost: a commission, not a scheme)
            foreach (var fleet in world.Fleets.Values.Where(f => f.OwnerId == house.Id && f.AdmiralId == "").OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal))
            {
                var taken = world.Fleets.Values.Select(f => f.AdmiralId).ToHashSet();
                var best = npcs.Where(c => c.HouseId == house.Id && !taken.Contains(c.Id))
                               .OrderByDescending(c => c.Skill("tactics") * 5 + c.Skill("gunnery") * 3 + c.Skill("leadership") * 2 + (c.CareerTrack == "military" ? 5 : 0))
                               .ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
                if (best == null) break;
                fleet.AdmiralId = best.Id;
                world.Log($"{best.Name} takes command of {fleet.Name}.");
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

            // marriage: an unmarried adult of the house courts the best-disposed eligible match anywhere
            var suitor = npcs.Where(c => c.HouseId == house.Id && c.Age >= DynastySystem.AgeOfMajority && c.Sex != "" && !ProposeMarriageCommand.IsMarried(world, c.Id))
                             .OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
            if (suitor == null) continue;
            int Score(Character c)
            {
                var woman = c.Sex == "female" ? c : suitor;
                int heirs = woman.Age <= DynastySystem.FertileMax ? 40 : -40;          // a match that can bear heirs
                int closeness = -Math.Abs(c.Age - suitor.Age);
                int disp = world.GetRelationship(c.Id, suitor.Id)?.Disposition ?? 0;
                return heirs + closeness + disp;
            }
            var match = world.LivingCharacters()
                .Where(c => c.HouseId != house.Id && c.Sex != "" && c.Sex != suitor.Sex && c.Age >= DynastySystem.AgeOfMajority && !ProposeMarriageCommand.IsMarried(world, c.Id)
                            && !c.Flags.Contains(ProposeMarriageCommand.SuitorFlag))
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
            foreach (var holding in world.HoldingsOf(house.Id).OrderBy(h => h.Id, StringComparer.Ordinal))
            {
                foreach (var b in catalog.Buildings.OrderByDescending(b => b.Megastructure).ThenByDescending(b => b.RequiresDeposit != null).ThenBy(b => b.Id, StringComparer.Ordinal))   // work the deposit before the rest
                {
                    if (b.Megastructure && house.Treasury.Credits < b.Cost.Credits * 2) continue;      // only when it can afford to show off
                    if (executor.TryExecute(new BuildCommand(steward.Id, holding.Id, b.Id, catalog, Titles), ctx)) { built = true; break; }
                }
                if (built) break;
            }

            Venture(house, steward, catalog, executor, ctx);
        }
    }

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
    }

    /// <summary>Fuel before finery (GDD §17): every rival house with a navy and dry or low tanks buys helium-3 on the open market before it
    /// spends a pool point on anything else — three turns' worth when the coin is comfortable, one turn's worth when it is not.</summary>
    private void Refuel(List<Character> npcs, HoldingCatalog catalog, CommandExecutor executor, CommandContext ctx)
    {
        var world = ctx.World;
        foreach (var house in world.Houses.Values.Where(h => h.Id != world.Protagonist?.HouseId && h.Id != PirateSystem.HouseId).OrderBy(h => h.Id, StringComparer.Ordinal))
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
            var others = world.Houses.Values.Where(o => o.Id != house.Id && o.Id != PirateSystem.HouseId).OrderByDescending(Regard).ThenBy(o => o.Id, StringComparer.Ordinal).ToList();
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
                    if (new FoundBusinessCommand(steward.Id, def.Id, sys.Id, _ventures, catalog, Titles).Refusal(ctx) != null) continue;
                    if (def.Upkeep.Manpower > 0 && mpNet - def.Upkeep.Manpower < 1) continue;   // no men to spare: the yards come first
                    int pay = ProjectedNet(world, house, def, sys.Id) + (own.Contains(sys.Id) ? 1 : 0);
                    if (pay <= 0) continue;
                    if (best == null || pay > best.Value.pay) best = (def.Id, sys.Id, pay);
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

        // Otherwise, drill the skill they're already strongest in.
        var skill = npc.Skills.Count > 0
            ? npc.Skills.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First().Key
            : "discipline";
        return new TrainCommand(npc.Id, skill);
    }
}
