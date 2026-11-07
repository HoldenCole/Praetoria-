using Praetoria.Core.Data;
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
    /// <summary>Catalogs for naming and raising children (names, traits, careers). Optional: without them children
    /// are "Scion N" and come of age with only inherited traits.</summary>
    public ContentDatabase? Content { get; init; }

    public const string HiddenFlag = "hidden";          // fostered away: unreachable by events, still an heir
    public const string RegentFlag = "regent";          // the adult ruling in a child head's name
    public const string ParvenuFlag = "parvenu";        // the old blood looks down on this house
    public const string ComeOfAgeFlag = "came_of_age";  // set the turn a child turns 16 (content clears)

    public const int MortalityAge = 50;   // below this, natural death isn't rolled (no RNG drawn)
    public const int MaxAge = 95;          // death is near-certain approaching here
    public const int FertileMin = 16;
    public const int FertileMax = 45;
    public const double BirthChance = 0.30;

    /// <summary>Below this age a head of house rules through a regent (the <see cref="RegencyFlag"/> is up).</summary>
    public const int AgeOfMajority = 16;

    // GDD §18: stress is the price of acting against your nature. It bleeds off slowly each year; at the
    // ceiling the character BREAKS — a `broken` flag events can read, and the stress resets to a level
    // from which another break is possible but not immediate.
    public const int StressRelief = 1;
    public const int StressBreakPoint = 100;
    public const int StressAfterBreak = 50;
    public const string BrokenFlag = "broken";
    public const string RegencyFlag = "regency";

    /// <summary>Advance one year of life: age, stress, deaths (with succession), births, regency.
    /// Call once per turn. RNG is drawn only where a life actually turns (deaths, births).</summary>
    public void Tick(World world, IRng rng)
    {
        foreach (var c in world.Characters.Values.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            if (!c.Alive) continue;
            c.Age++;
            if (c.Age == AgeOfMajority) ComeOfAge(world, c, rng);
            TickStress(world, c);
        }

        RollDeaths(world, rng);
        RollBirths(world, rng);
        UpdateRegency(world);
        Society(world);
    }

    /// <summary>At sixteen a child becomes a person (GDD §5/§13 — each heir starts a fresh career): one nature trait
    /// inherited from a parent, an aptitude from the other if they have one, a career track (the parents' track if
    /// they share one, else drawn), baseline skills on that track, an ambition drawn from the catalog, and a return
    /// from fostering. Deterministic from the seed.</summary>
    private void ComeOfAge(World world, Character c, IRng rng)
    {
        var mother = world.Char(c.MotherId); var father = world.Char(c.FatherId);
        var parents = new[] { father, mother }.Where(p => p != null).Select(p => p!).ToList();
        if (c.NatureTraits.Count == 0)
        {
            var pool = parents.SelectMany(p => p.NatureTraits).Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
            if (pool.Count == 0 && Content is { Traits.Nature.Count: > 0 }) pool = Content.Traits.Nature.Select(t => t.Id).ToList();
            if (pool.Count > 0) c.NatureTraits.Add(pool[rng.NextInt(0, pool.Count)]);
        }
        if (c.AptitudeTraits.Count == 0)
        {
            var pool = parents.SelectMany(p => p.AptitudeTraits).Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
            if (pool.Count > 0 && rng.NextDouble() < 0.6) c.AptitudeTraits.Add(pool[rng.NextInt(0, pool.Count)]);
        }
        if (string.IsNullOrEmpty(c.CareerTrack) || c.CareerRank == 0 && c.Skills.Count == 0)
        {
            var tracks = Content is { Careers.IsEmpty: false } ? Content.Careers.Defs.Select(t => t.Id).ToList() : new List<string> { "military", "stewardship", "law" };
            var parentTracks = parents.Select(p => p.CareerTrack).Where(t => tracks.Contains(t)).Distinct().ToList();
            c.CareerTrack = parentTracks.Count == 1 ? parentTracks[0] : tracks[rng.NextInt(0, tracks.Count)];
            c.CareerRank = 0;
            foreach (var skill in TrackSkills(c.CareerTrack)) c.Skills[skill] = 1;
            c.Skills["charisma"] = 1;
        }
        if (string.IsNullOrEmpty(c.Ambition) && Content is { Traits.Ambitions.Count: > 0 })
            c.Ambition = Content.Traits.Ambitions[rng.NextInt(0, Content.Traits.Ambitions.Count)].Id;
        bool wasHidden = c.Flags.Remove(HiddenFlag);
        c.Flags.Add(ComeOfAgeFlag);
        world.Log($"{c.Name} comes of age{(wasHidden ? ", home from fostering" : "")} — {string.Join("/", c.NatureTraits)}{(c.AptitudeTraits.Count > 0 ? $" ({string.Join("/", c.AptitudeTraits)})" : "")}, bound for {Content?.Careers.ById(c.CareerTrack)?.Name ?? c.CareerTrack}.");
    }

    private static IEnumerable<string> TrackSkills(string track) => track switch
    {
        "military" => new[] { "tactics", "discipline" },
        "stewardship" => new[] { "administration", "economics" },
        "law" => new[] { "law", "oratory" },
        _ => Array.Empty<string>()
    };

    /// <summary>GDD §13 "Resentment of the Risen": heads of houses whose legitimacy outstrips the protagonist's by a
    /// wide margin cool toward them a point a year (to a floor), and the house is marked <c>parvenu</c> while any such
    /// house exists. Earned standing erases it. Also refreshes the regent.</summary>
    public const int ParvenuGap = 30;
    private static void Society(World world)
    {
        var p = world.Protagonist;
        var house = p != null ? world.House(p.HouseId) : null;
        if (p is not { Alive: true } || house == null) return;
        bool looked_down_on = false;
        foreach (var other in world.Houses.Values.Where(h => h.Id != house.Id && h.Legitimacy >= house.Legitimacy + ParvenuGap))
        {
            looked_down_on = true;
            foreach (var head in world.LivingCharacters().Where(c => c.HouseId == other.Id))
            {
                var rel = world.Relationship(head.Id, p.Id);
                if (rel.Disposition > -60) rel.Disposition -= 1;
            }
        }
        if (looked_down_on) p.Flags.Add(ParvenuFlag); else p.Flags.Remove(ParvenuFlag);
    }

    private static void TickStress(World world, Character c)
    {
        if (c.Stress >= StressBreakPoint)
        {
            c.Flags.Add(BrokenFlag);
            c.Stress = StressAfterBreak;
            world.WorldCounters["breakdowns"] = world.Counter("breakdowns") + 1;
            world.Log($"{c.Name} breaks under the strain.");
        }
        else
        {
            c.Stress = Math.Max(0, c.Stress - StressRelief);
        }
    }

    /// <summary>A head of house below the age of majority rules through a regent (GDD §14 — a thin
    /// model for now: a world flag content can gate on; the regent is not yet a bound character).</summary>
    private static void UpdateRegency(World world)
    {
        var p = world.Protagonist;
        foreach (var c in world.Characters.Values) c.Flags.Remove(RegentFlag);
        if (p is { Alive: true } && p.Age < AgeOfMajority)
        {
            world.WorldFlags.Add(RegencyFlag);
            var regent = world.LivingCharacters().Where(c => c.HouseId == p.HouseId && c.Id != p.Id && c.Age >= AgeOfMajority)
                              .OrderByDescending(c => c.Age).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
            if (regent != null) regent.Flags.Add(RegentFlag);
        }
        else world.WorldFlags.Remove(RegencyFlag);
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
            if (father == null || father.Sex != "male") continue;   // births need a mother and a father
            if (rng.NextDouble() < BirthChance)
                Bear(world, mother, father, rng);
        }
    }

    /// <summary>The living spouse a character is married to (a Marriage bond on <em>either</em> edge — a
    /// marriage authored from one side still counts), or null. Deterministic: edges are scanned in
    /// insertion order and the first living spouse wins.</summary>
    public static Character? Spouse(World world, Character c)
    {
        foreach (var r in world.Relationships)
        {
            if (r.Bond != BondType.Marriage) continue;
            string? otherId = r.FromId == c.Id ? r.ToId : r.ToId == c.Id ? r.FromId : null;
            if (otherId == null) continue;
            var other = world.Char(otherId);
            if (other is { Alive: true }) return other;
        }
        return null;
    }

    private void Bear(World world, Character mother, Character father, IRng rng)
    {
        int n = world.Counter("dynasty_births") + 1;
        world.WorldCounters["dynasty_births"] = n;

        var house = world.House(father.HouseId);   // patrilineal: the child joins the father's house
        string sex = rng.NextDouble() < 0.5 ? "male" : "female";
        string surname = (house?.Name ?? father.HouseId).Replace("House ", "");
        string given = Content is { Names.IsEmpty: false } ? Content.Names.Pick(house?.Culture ?? "reach", sex, rng) : $"Scion {n}";
        var child = new Character
        {
            Id = $"scion_{n}",
            Name = $"{given} {surname}",
            HouseId = father.HouseId,
            Age = 0,
            Sex = sex,
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
            HandOver(world, heir);
            return;
        }

        var house = world.House(deceased.HouseId);
        string houseName = house?.Name ?? deceased.HouseId;

        // GDD §14 continue mode: the bloodline is extinct, but a sworn house takes up the banner.
        var adoptee = world.Mode == World.ModeContinue ? FindSwornSuccessor(world, deceased) : null;
        if (adoptee != null)
        {
            world.WorldFlags.Add(DynastyAdoptedFlag);
            world.WorldCounters["adoptions"] = world.Counter("adoptions") + 1;
            world.Log($"The blood of {houseName} is ended. {adoptee.Name} of {world.House(adoptee.HouseId)?.Name ?? adoptee.HouseId}, sworn to the house, takes up its banner.");
            HandOver(world, adoptee);
            return;
        }

        world.WorldFlags.Add(World.DynastyDeadFlag);
        world.Log($"{houseName} ends — no heir remains.");
    }

    /// <summary>Raised when continue mode handed the game to a sworn house (GDD §14 "adopt a surviving sworn cadet/vassal house").</summary>
    public const string DynastyAdoptedFlag = "dynasty_adopted";

    /// <summary>The sworn character who inherits the game in continue mode: a living non-house-member
    /// bound to the deceased by a sworn bond on either edge, strongest bond first. Null if none.</summary>
    public static Character? FindSwornSuccessor(World world, Character deceased)
    {
        Character? best = null; int bestStrength = -1;
        foreach (var r in world.Relationships)
        {
            if (r.Bond != BondType.Sworn) continue;
            string? otherId = r.FromId == deceased.Id ? r.ToId : r.ToId == deceased.Id ? r.FromId : null;
            if (otherId == null) continue;
            var other = world.Char(otherId);
            if (other is not { Alive: true } || other.HouseId == deceased.HouseId) continue;
            if (r.BondStrength > bestStrength ||
                (r.BondStrength == bestStrength && string.CompareOrdinal(other.Id, best!.Id) < 0))
            { best = other; bestStrength = r.BondStrength; }
        }
        return best;
    }

    /// <summary>Make <paramref name="heir"/> the protagonist: by death (succession) or by choice (the
    /// <c>succeed</c> effect — abdication). The House endures; the new head gets a player-scale economy (§9).</summary>
    public static void HandOver(World world, Character heir)
    {
        var previous = world.Protagonist;
        world.ProtagonistId = heir.Id;
        world.Pools[heir.Id] = ActionPools.ForPlayer();
        world.WorldCounters["generation"] = world.Counter("generation") + 1;
        var house = world.House(heir.HouseId);
        world.Log(previous is { Alive: true }
            ? $"{previous.Name} yields the seat; {heir.Name} succeeds as head of {(house?.Name ?? heir.HouseId)}."
            : $"{heir.Name} succeeds as head of {(house?.Name ?? heir.HouseId)}.");
    }

    /// <summary>The successor to a deceased head (GDD §14): a living member of the same house, blood
    /// children first (eldest before younger — primogeniture by age), then any house member by age.</summary>
    public static Character? FindHeir(World world, Character deceased)
    {
        var members = world.Characters.Values.Where(c => c.Alive && c.Id != deceased.Id && c.HouseId == deceased.HouseId);
        string law = world.House(deceased.HouseId)?.SuccessionLaw ?? "primogeniture";
        return law switch
        {
            "ultimogeniture" => members.OrderByDescending(c => IsChildOf(c, deceased)).ThenBy(c => c.Age).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault(),
            "seniority" => members.OrderByDescending(c => c.Age).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault(),
            "merit" => members.OrderByDescending(c => c.Age >= AgeOfMajority).ThenByDescending(c => c.Skills.Values.Sum() + c.CareerRank * 2).ThenByDescending(c => c.Age).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault(),
            _ => members.OrderByDescending(c => IsChildOf(c, deceased)).ThenByDescending(c => c.Age).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault()
        };
    }

    private static bool IsChildOf(Character c, Character parent) =>
        c.MotherId == parent.Id || c.FatherId == parent.Id;
}
