using Praetoria.Core.Rng;
using Praetoria.Core.State;

namespace Praetoria.Core.Events;

/// <summary>
/// Resolves an event's role variables to concrete characters (BuildSpec §4 "Binder", the
/// "by ROLE not NAME" rule). Roles bind in declared order so a later role's constraints can
/// reference earlier ones (and always "self"). If any role has no satisfying candidate, the
/// event simply cannot fire — this is *why* arming a flag elsewhere can make an event become
/// eligible later (it creates a bindable candidate where there was none).
/// </summary>
public static class Binder
{
    /// <summary>
    /// Try to produce a full binding for the event. Candidates are drawn from living characters,
    /// excluding the protagonist and anyone already bound (no character fills two roles). Among
    /// valid candidates one is chosen via the RNG so repeated runs from a seed are deterministic.
    /// Returns null if any role is unfillable.
    /// </summary>
    public static Binding? TryBind(EventDef def, World world, IRng rng)
    {
        var binding = new Binding(world.ProtagonistId);
        var used = new HashSet<string> { world.ProtagonistId };

        foreach (var role in def.Roles)
        {
            // Deterministic candidate order (by Id), filtered by the role's constraints
            // evaluated with the candidate provisionally bound to this role.
            var candidates = new List<Character>();
            foreach (var c in world.LivingCharacters())
            {
                if (used.Contains(c.Id)) continue;
                var trial = binding.With(role.Name, c.Id);
                var ctx = new EvalContext(world, trial, rng);
                if (AllHold(role.Constraints, ctx))
                    candidates.Add(c);
            }

            if (candidates.Count == 0) return null;

            candidates.Sort(static (a, b) => string.CompareOrdinal(a.Id, b.Id));
            var pick = candidates[rng.NextInt(0, candidates.Count)];
            binding = binding.With(role.Name, pick.Id);
            used.Add(pick.Id);
        }

        return binding;
    }

    internal static bool AllHold(IReadOnlyList<ICondition> conditions, EvalContext ctx)
    {
        foreach (var c in conditions) if (!c.Evaluate(ctx)) return false;
        return true;
    }
}
