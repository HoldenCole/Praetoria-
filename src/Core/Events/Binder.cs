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
    /// <summary>Parts that are, by what they are, somebody outside the player's house: the one who buys from it, lends to it, envies it,
    /// treats with it or comes to it as an old friend from elsewhere (a rival or an enemy may be of the blood, and is not in this list). One taken into the household is of the house, and a report cast them
    /// as the outside buyer and the old friend in the city (docs/PLAYTHROUGH.md, Easy, F53 and F76). The chance meetings' strangers
    /// (docs/events/chance.md: the classmate, the veteran, the young one wanting a patron, the sitter beside you on the painter's wall,
    /// the subject of a secret for sale, the two guests at your feast) are here too: a relative struck from the line is still of the house.</summary>
    private static readonly HashSet<string> OutsideParts = new(StringComparer.Ordinal)
    { "buyer", "rival_house", "envoy", "intruder", "giver", "debtor", "claimant", "grandee", "pretender", "coalition_head", "envious", "friend", "sovereign",
      "classmate", "veteran", "young", "sitter", "subject", "guest", "other_guest",
      // a whip who comes to buy the house's vote, and the one it borrows from: a favour or a vote owed between them is between two houses (docs/events/votes.md, "Promises")
      "whip", "lender", "creditor" };

    public static Binding? TryBind(EventDef def, World world, IRng rng)
    {
        var binding = new Binding(world.ProtagonistId); string protagonistHouse = world.Protagonist?.HouseId ?? "";
        string stewardId = world.House(protagonistHouse)?.Duty(Systems.HouseholdSystem.Steward)?.HolderId ?? "";
        var used = new HashSet<string> { world.ProtagonistId };

        foreach (var role in def.Roles)
        {
            // the dead fill a role only when it asks for them (a funeral names whom it buries): drawn from the house's funerals, the
            // most recent first, and without the dice, so the year's draw is what it was before the role was added
            if (role.Constraints.Any(k => k is BuriedCondition b && b.Role == role.Name))
            {
                Character? buried = null;
                for (int i = world.Funerals.Count - 1; i >= 0 && buried == null; i--)
                {
                    var c = world.Char(world.Funerals[i].PersonId);
                    if (c == null || c.Alive || used.Contains(c.Id)) continue;
                    if (AllHold(role.Constraints, new EvalContext(world, binding.With(role.Name, c.Id), rng))) buried = c;
                }
                if (buried == null) return null;
                binding = binding.With(role.Name, buried.Id); used.Add(buried.Id);
                continue;
            }
            // Deterministic candidate order (by Id), filtered by the role's constraints
            // evaluated with the candidate provisionally bound to this role.
            var candidates = new List<Character>();
            // Children never fill a role unless the role says something about age (a newborn is not a steward, a rival or a
            // suitor); events about the young opt in with an explicit age constraint on that role.
            bool bindsChildren = role.Constraints.Any(k => k is AgeCondition a && a.Role == role.Name);
            foreach (var c in world.LivingCharacters())
            {
                if (used.Contains(c.Id)) continue;
                if (c.Flags.Contains(Systems.DynastySystem.HiddenFlag)) continue;   // fostered away — unreachable (GDD §14 scatter the seed)
                if (!bindsChildren && c.Age < Systems.DynastySystem.AgeOfMajority) continue;
                if (OutsideParts.Contains(role.Name) && c.HouseId == protagonistHouse) continue;
                if (role.Name == "steward" && c.Id != stewardId) continue;   // the steward of a report is whoever keeps the house's accounts; with nobody named there is no such report   // nobody of the house buys from it, lends to it or comes to it as a stranger
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
