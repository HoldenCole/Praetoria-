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
    /// <summary>Catalogs the fleet AI consults (optional; without them NPCs raise the default class, unled).</summary>
    public ShipClassCatalog? ShipClasses { get; init; }
    public Progression.TitleCatalog? Titles { get; init; }

    public void Act(CommandExecutor executor, CommandContext ctx, CrisisEngine? crises = null,
        HoldingCatalog? catalog = null, FleetRules? fleetRules = null)
    {
        var world = ctx.World;
        var npcs = world.LivingCharacters()
            .Where(c => c.Id != world.ProtagonistId)
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .ToList();

        // House-level strategy first (it spends the same pools), then personal jabs, then schemes.
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

            // war and peace (GDD §7 fear with teeth): a house that hates you and outguns you declares war;
            // one that has lost half its hulls sues for peace.
            if (player != null && playerHouse != null)
            {
                if (!atWar && disposition < -60 && FleetSystem.StrengthOf(world, house.Id) > FleetSystem.StrengthOf(world, playerHouse))
                { if (executor.TryExecute(new DeclareWarCommand(head.Id, playerHouse, rules), ctx)) atWar = true; }
                else if (atWar && FleetSystem.StrengthOf(world, house.Id) * 2 < FleetSystem.StrengthOf(world, playerHouse))
                { if (executor.TryExecute(new SueForPeaceCommand(head.Id, playerHouse, rules), ctx)) continue; }
            }

            // menace: a hostile (or belligerent), rested house fleet moves on the player's seat
            if (seat != null && player != null && (atWar || disposition < -40))
            {
                var fleet = world.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.Moving && f.Readiness >= 80 && f.SystemId != seat)
                                               .OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
                if (fleet != null && executor.TryExecute(new MoveFleetCommand(head.Id, fleet.Id, seat, rules), ctx)) continue;
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
            var classes = ShipClasses ?? ShipClassCatalog.Empty;
            var candidates = classes.IsEmpty ? new[] { rules.DefaultClass } : classes.Defs.OrderByDescending(c => c.Attack + c.Defense).Select(c => c.Id).ToArray();
            foreach (var cid in candidates)
            {
                var cls = classes.Resolve(cid);
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

            // heir-hunting: at war, a Cruel or Ruthless head with agents to spare moves on the player's heirs
            if (player != null && playerHouse != null && world.AtWar(house.Id, playerHouse) &&
                (head.NatureTraits.Contains("Cruel") || head.NatureTraits.Contains("Ruthless")))
            {
                var heir = world.LivingCharacters().Where(c => c.HouseId == playerHouse && c.Id != player.Id && !c.Flags.Contains(DynastySystem.HiddenFlag))
                                .OrderBy(c => c.Age).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
                if (heir != null && executor.TryExecute(new AssassinateCommand(head.Id, heir.Id), ctx)) continue;
            }

            // marriage: an unmarried adult of the house courts the best-disposed eligible match anywhere
            var suitor = npcs.Where(c => c.HouseId == house.Id && c.Age >= DynastySystem.AgeOfMajority && c.Sex != "" && !ProposeMarriageCommand.IsMarried(world, c.Id))
                             .OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
            if (suitor == null) continue;
            var match = world.LivingCharacters()
                .Where(c => c.HouseId != house.Id && c.Id != world.ProtagonistId && c.Sex != "" && c.Sex != suitor.Sex && c.Age >= DynastySystem.AgeOfMajority && !ProposeMarriageCommand.IsMarried(world, c.Id))
                .OrderByDescending(c => world.GetRelationship(c.Id, suitor.Id)?.Disposition ?? 0).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
            if (match != null) executor.TryExecute(new ProposeMarriageCommand(head.Id, suitor.Id, match.Id, Titles), ctx);
        }
    }

    /// <summary>Rival houses develop their (abstracted, GDD §3) domains: each NPC house's senior living
    /// member slots the first affordable, fitting building — at most one build per house per turn, in id
    /// order, so rival realms grow deterministically instead of sitting on hoarded treasuries.</summary>
    private static void Invest(List<Character> npcs, HoldingCatalog catalog, CommandExecutor executor, CommandContext ctx)
    {
        var world = ctx.World;
        var playerHouse = world.Protagonist?.HouseId;
        foreach (var house in world.Houses.Values.OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            if (house.Id == playerHouse) continue;
            var steward = npcs.FirstOrDefault(c => c.HouseId == house.Id);
            if (steward == null) continue;

            bool built = false;
            foreach (var holding in world.HoldingsOf(house.Id).OrderBy(h => h.Id, StringComparer.Ordinal))
            {
                foreach (var b in catalog.Buildings.OrderBy(b => b.Id, StringComparer.Ordinal))
                {
                    if (executor.TryExecute(new BuildCommand(steward.Id, holding.Id, b.Id, catalog), ctx)) { built = true; break; }
                }
                if (built) break;
            }
        }
    }

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
