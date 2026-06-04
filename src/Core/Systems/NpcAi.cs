using Praetoria.Core.Commands;
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
    public void Act(CommandExecutor executor, CommandContext ctx, CrisisEngine? crises = null)
    {
        var world = ctx.World;
        var npcs = world.LivingCharacters()
            .Where(c => c.Id != world.ProtagonistId)
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .ToList();

        foreach (var npc in npcs)
        {
            var command = ChooseAction(npc, world);
            if (command != null)
                executor.TryExecute(command, ctx);
        }

        if (crises != null) AuthorCrisis(npcs, crises, executor, ctx);
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
