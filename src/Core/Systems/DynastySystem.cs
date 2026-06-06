using Praetoria.Core.Rng;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The dynasty lifecycle (GDD §14) — the loop that makes the <em>dynasty</em>, not the character, the
/// save-persistent entity. Each turn everyone ages; the old may die; fertile married couples may bear
/// children; and when the protagonist dies the seat passes to an heir (the House — its title,
/// legitimacy, holdings — endures), or the dynasty ends if no heir remains. Deterministic given the
/// RNG, and deliberately frugal with it: a death roll happens only at mortality age and a birth roll
/// only for an eligible couple, so a young, unmarried cast (the Academy) consumes no randomness at all.
/// </summary>
public sealed class DynastySystem
{
    public const int MortalityAge = 50;   // below this, natural death isn't rolled (no RNG drawn)
    public const int MaxAge = 95;          // death is near-certain approaching here
    public const int FertileMin = 16;
    public const int FertileMax = 45;
    public const double BirthChance = 0.30;

    /// <summary>Advance one year of life: age, deaths (with succession), births. Call once per turn.</summary>
    public void Tick(World world, IRng rng)
    {
        foreach (var c in world.Characters.Values.OrderBy(c => c.Id, StringComparer.Ordinal))
            if (c.Alive) c.Age++;

        RollDeaths(world, rng);
        RollBirths(world, rng);
    }

    private void RollDeaths(World world, IRng rng)
    {
        // Snapshot ids so a succession-driven mutation can't disturb iteration.
        foreach (var id in world.Characters.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList())
        {
            var c = world.Char(id);
            if (c is not { Alive: true } || c.Age < MortalityAge) continue;   // young ⇒ no roll, no RNG
            if (rng.NextDouble() < DeathProbability(c.Age))
                Die(world, id);
        }
    }

    /// <summary>Per-turn natural-death chance, 0 at <see cref="MortalityAge"/> rising to 1 by
    /// <see cref="MaxAge"/> (quadratic, so middle age is survivable and extreme age is not).</summary>
    public static double DeathProbability(int age)
    {
        if (age < MortalityAge) return 0;
        double t = Math.Min(1.0, (age - MortalityAge) / (double)(MaxAge - MortalityAge));
        return t * t;
    }

    private void RollBirths(World world, IRng rng)
    {
        foreach (var mother in world.Characters.Values
                     .Where(c => c.Alive && c.Sex == "female" && c.Age >= FertileMin && c.Age <= FertileMax)
                     .OrderBy(c => c.Id, StringComparer.Ordinal)
                     .ToList())
        {
            var father = Spouse(world, mother);
            if (father == null) continue;
            if (rng.NextDouble() < BirthChance)
                Bear(world, mother, father, rng);
        }
    }

    /// <summary>The living spouse a character is married to (the Marriage bond), or null.</summary>
    private static Character? Spouse(World world, Character c)
    {
        foreach (var r in world.ConnectionsOf(c.Id))
            if (r.Bond == BondType.Marriage)
            {
                var other = world.Char(r.ToId);
                if (other is { Alive: true }) return other;
            }
        return null;
    }

    private static void Bear(World world, Character mother, Character father, IRng rng)
    {
        int n = world.Counter("dynasty_births") + 1;
        world.WorldCounters["dynasty_births"] = n;

        var house = world.House(father.HouseId);   // patrilineal: the child joins the father's house
        var child = new Character
        {
            Id = $"scion_{n}",
            Name = $"Scion {n} of {(house?.Name ?? father.HouseId)}",
            HouseId = father.HouseId,
            Age = 0,
            Sex = rng.NextDouble() < 0.5 ? "male" : "female",
            MotherId = mother.Id,
            FatherId = father.Id
        };
        world.Characters[child.Id] = child;
        house?.Members.Add(child.Id);

        foreach (var parent in new[] { mother.Id, father.Id })
        {
            world.Relationship(parent, child.Id).Bond = BondType.Blood;
            world.Relationship(parent, child.Id).BondStrength = 100;
            world.Relationship(child.Id, parent).Bond = BondType.Blood;
            world.Relationship(child.Id, parent).BondStrength = 100;
        }
        world.Log($"A child, {child.Name}, is born to {father.Name} and {mother.Name}.");
    }

    /// <summary>Kill a character (natural death, assassination, Bloody Event). If it was the
    /// protagonist, pass the seat to an heir — or end the dynasty. No RNG (succession is deterministic).</summary>
    public static void Die(World world, string id)
    {
        var c = world.Char(id);
        if (c is not { Alive: true }) return;
        c.Alive = false;
        world.Log($"{c.Name} dies at {c.Age}.");

        if (id == world.ProtagonistId) Succeed(world, c);
    }

    private static void Succeed(World world, Character deceased)
    {
        var heir = FindHeir(world, deceased);
        if (heir != null)
        {
            world.ProtagonistId = heir.Id;
            world.Pools[heir.Id] = ActionPools.ForPlayer();   // the new head wields a player-scale economy (§9)
            world.WorldCounters["generation"] = world.Counter("generation") + 1;
            var house = world.House(heir.HouseId);
            world.Log($"{heir.Name} succeeds as head of {(house?.Name ?? heir.HouseId)}.");
        }
        else
        {
            world.WorldFlags.Add("dynasty_dead");
            world.Log($"House {deceased.HouseId} ends — no heir remains.");
        }
    }

    /// <summary>The successor to a deceased head (GDD §14): a living member of the same house, blood
    /// children first (eldest before younger — primogeniture by age), then any house member by age.</summary>
    public static Character? FindHeir(World world, Character deceased)
    {
        return world.Characters.Values
            .Where(c => c.Alive && c.Id != deceased.Id && c.HouseId == deceased.HouseId)
            .OrderByDescending(c => IsChildOf(c, deceased))   // direct heirs first
            .ThenByDescending(c => c.Age)                     // eldest first
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool IsChildOf(Character c, Character parent) =>
        c.MotherId == parent.Id || c.FatherId == parent.Id;
}
