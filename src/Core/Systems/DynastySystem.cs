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
    public const int ElderAge = 60;                     // from here the body forgets: every fifth year the sharpest skill dulls

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
            if (c.Age >= ElderAge && c.Age % 5 == 0) Decline(world, c);
            TickStress(world, c);
        }

        AdvanceRivalCareers(world);
        RollDeaths(world, rng);
        RollBirths(world, rng);
        RaiseCousins(world, rng);
        UpdateRegency(world);
        Society(world);
    }

    /// <summary>A rival house whose last member has died does not vanish with its worlds rotting unheld (GDD §19 the galaxy stays
    /// peopled): a cousin from the provinces takes up the banner — an adult of the house's culture with a modest career, who
    /// governs what the house still holds. The player's house never gets this mercy; its ending is the saga's.</summary>
    private void RaiseCousins(World world, IRng rng)
    {
        var mine = world.Protagonist?.HouseId;
        foreach (var house in world.Houses.Values.OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            if (house.Id == mine || house.Id == PirateSystem.HouseId) continue;
            if (world.Characters.Values.Any(c => c.Alive && c.HouseId == house.Id)) continue;
            if (!world.HoldingsOf(house.Id).Any() && !world.BusinessesOf(house.Id).Any() && !world.Fleets.Values.Any(f => f.OwnerId == house.Id)) continue;   // a name with nothing left ends quietly
            int n = world.Counter("cousins") + 1; world.WorldCounters["cousins"] = n;
            string sex = rng.NextDouble() < 0.5 ? "male" : "female"; string surname = house.Name.Replace("House ", "");
            string given = Content is { Names.IsEmpty: false } ? Content.Names.Pick(house.Culture, sex, rng) : $"Cousin {n}";
            var tracks = Content is { Careers.IsEmpty: false } ? Content.Careers.Defs.Select(t => t.Id).ToList() : new List<string> { "military", "stewardship", "law" };
            var cousin = new Character { Id = $"cousin_{house.Id}_{n}", Name = $"{given} {surname}", HouseId = house.Id, Age = 26 + rng.NextInt(0, 15), Sex = sex, CareerTrack = tracks[rng.NextInt(0, tracks.Count)], CareerRank = 1 };
            cousin.Skills["administration"] = 1 + rng.NextInt(0, 2);
            world.Characters[cousin.Id] = cousin; house.Members.Add(cousin.Id); world.Pools[cousin.Id] = ActionPools.ForNpc();
            foreach (var h in world.HoldingsOf(house.Id)) if (h.GovernorId.Length == 0 || world.Char(h.GovernorId)?.Alive != true) h.GovernorId = cousin.Id;
            world.Log($"{cousin.Name}, a cousin out of the provinces, raises the banner of {house.Name}.");
        }
    }

    public const int CareerStartAge = 20, HeadPromotionYears = 6, PromotionYears = 9, YearsPerRank = 6;

    /// <summary>Rival careers advance (GDD §5 "your career feeds your house's sphere-influence" — theirs too). Every adult outside the
    /// player's seat climbs a rung every <see cref="PromotionYears"/> turns (heads of houses every <see cref="HeadPromotionYears"/>), by a
    /// hash of their id so promotions spread across turns, never faster than a rung per <see cref="YearsPerRank"/> years of adult life and
    /// never past the track's top rung. Before this, three hundred rival adults sat at rank zero for sixty years and the spheres, the
    /// Senate's weights and the offices were the player's alone. RNG-free.</summary>
    private void AdvanceRivalCareers(World world)
    {
        var tracks = Content is { Careers.IsEmpty: false } ? Content.Careers : null;
        foreach (var c in world.Characters.Values.Where(c => c.Alive && c.Id != world.ProtagonistId && c.Age >= CareerStartAge && c.CareerTrack.Length > 0).OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            int top = tracks?.ById(c.CareerTrack)?.Ranks.Count - 1 ?? 4; if (c.CareerRank >= top) continue;
            if (c.CareerRank >= (c.Age - AgeOfMajority) / YearsPerRank) continue;                  // no admirals at twenty-two
            var house = world.House(c.HouseId); bool head = house != null && Economy.HeadOf(world, house)?.Id == c.Id;
            uint hash = 2166136261; foreach (var ch in c.Id) hash = (hash ^ ch) * 16777619;
            int period = head ? HeadPromotionYears : PromotionYears;
            if ((world.Turn + (int)(hash % (uint)period)) % period != 0) continue;
            c.CareerRank++;
            if (head && c.CareerRank >= 3 && house != null && house.Id != world.Protagonist?.HouseId) world.Log($"{c.Name} of {house.Name} is made {Content?.Careers.RankName(c.CareerTrack, c.CareerRank, world.Era) ?? $"rank {c.CareerRank}"}.");
        }
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
            // what they were brought up to counts for more than what their parents did (docs/EDUCATION.md)
            if (c.Skills.Count > 0 && c.Skills.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First() is { Value: >= 2 } best
                && EducationSystem.TrackFor(EducationSystem.GroupOf(best.Key)) is var raised && tracks.Contains(raised)) c.CareerTrack = raised;
            c.CareerRank = 0;
            foreach (var skill in TrackSkills(c.CareerTrack)) c.Skills[skill] = Math.Max(c.Skill(skill), 1);
            c.Skills["charisma"] = Math.Max(c.Skill("charisma"), 1);
            c.Upbringing = ""; c.Tutor = 0;
        }
        if (string.IsNullOrEmpty(c.Ambition) && Content is { Traits.Ambitions.Count: > 0 })
            c.Ambition = Content.Traits.Ambitions[rng.NextInt(0, Content.Traits.Ambitions.Count)].Id;
        bool wasHidden = c.Flags.Remove(HiddenFlag);
        c.Flags.Add(ComeOfAgeFlag);
        world.Log($"{c.Name} comes of age{(wasHidden ? ", home from fostering" : "")} — {string.Join("/", c.NatureTraits)}{(c.AptitudeTraits.Count > 0 ? $" ({string.Join("/", c.AptitudeTraits)})" : "")}, bound for {Content?.Careers.ById(c.CareerTrack)?.Name ?? c.CareerTrack}.");
    }

    /// <summary>Age takes the edge off: the highest skill loses a point (never below 1). The old lion is still a lion.</summary>
    private static void Decline(World world, Character c)
    {
        var top = c.Skills.Where(kv => kv.Value > 1).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).FirstOrDefault();
        if (top.Key == null) return;
        c.Skills[top.Key] = top.Value - 1;
        if (c.Id == world.ProtagonistId) world.Log($"The years take their toll: {c.Name}'s {top.Key} dulls.");
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
            if (world.Characters.Values.Any(k => k.MotherId == mother.Id && k.Age < 2)) continue;   // a year between children
            if (rng.NextDouble() < BirthChance * CourtshipSystem.Fertility(world, mother, father))   // a warm marriage bears more, a cold one fewer, an estranged one none
                Bear(world, mother, father, rng);
        }
    }

    /// <summary>The living spouse a character is married to (a Marriage bond on <em>either</em> edge — a
    /// marriage authored from one side still counts), or null. Deterministic: edges are scanned in
    /// insertion order and the first living spouse wins.</summary>
    /// <summary>Blood too close to wed (GDD §14): parent and child, siblings and half-siblings, grandparent and grandchild,
    /// aunt or uncle and niece or nephew, first cousins — anyone who shares an ancestor within two generations.</summary>
    public static bool AreKin(World world, Character a, Character b)
    {
        if (a.Id == b.Id) return true;
        var ka = Ancestry(world, a); var kb = Ancestry(world, b);
        return ka.Overlaps(kb);
    }

    private static HashSet<string> Ancestry(World world, Character c)
    {
        var set = new HashSet<string> { c.Id };
        void Add(string id, int depth)
        {
            if (id.Length == 0 || depth > 2) return;
            set.Add(id);
            var p = world.Char(id);
            if (p == null) return;
            Add(p.MotherId, depth + 1); Add(p.FatherId, depth + 1);
        }
        Add(c.MotherId, 1); Add(c.FatherId, 1);
        return set;
    }

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
        int n = world.Counter("births") + 1;
        world.WorldCounters["births"] = n;

        // Whose name does the child carry (GDD §14 "bodies carrying your name")? The head of a house keeps the
        // seat's children; otherwise the greater house does; otherwise the father's. So a woman who rules, or who
        // married down, raises heirs to her own name — matrilineal where it matters, patrilineal by default.
        var houseFather = world.House(father.HouseId); var houseMother = world.House(mother.HouseId);
        bool motherKeeps = mother.Id == world.ProtagonistId
            || (father.Id != world.ProtagonistId && houseMother != null && houseFather != null && Content != null && Content.Titles.RankOf(houseMother.Title) > Content.Titles.RankOf(houseFather.Title));
        if (CourtshipSystem.ContractNameHouse(world, mother, father) is { } agreed) motherKeeps = agreed == mother.HouseId;   // what the marriage contract said
        var lineParent = motherKeeps ? mother : father;
        var house = world.House(lineParent.HouseId);
        if (lineParent.HouseId == world.Protagonist?.HouseId) world.WorldCounters["dynasty_births"] = world.Counter("dynasty_births") + 1;   // the saga's own count
        string sex = rng.NextDouble() < 0.5 ? "male" : "female";
        // someone taken into a house keeps their own name, and so do their children
        string surname = HouseholdSystem.IsFound(lineParent) && lineParent.Name.Contains(' ') ? lineParent.Name[(lineParent.Name.LastIndexOf(' ') + 1)..] : (house?.Name ?? lineParent.HouseId).Replace("House ", "");
        string given = $"Scion {n}";
        if (Content is { Names.IsEmpty: false })
        {
            // no two living kin share a given name — a house with two Dariuses reads like a bug, not a family
            var taken = world.Characters.Values.Where(k => k.Alive && k.HouseId == lineParent.HouseId).Select(k => k.Name.Split(' ')[0]).ToHashSet();
            for (int attempt = 0; attempt < 8; attempt++)
            {
                given = Content.Names.Pick(house?.Culture ?? "reach", sex, rng);
                if (!taken.Contains(given)) break;
            }
        }
        var child = new Character
        {
            Id = $"scion_{n}",
            Name = $"{given} {surname}",
            HouseId = lineParent.HouseId,
            Age = 0,
            Sex = sex,
            MotherId = mother.Id,
            FatherId = father.Id,
            BornHouseId = HouseholdSystem.IsFound(lineParent) ? lineParent.BornHouseId : ""   // born into the household, not into the blood
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
            WealthSystem.Inherit(world, deceased, heir);   // the house went by the law; the purse and heirlooms go by the will (docs/WEALTH.md)
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
        // contested succession (GDD §14): adult blood siblings passed over resent the new head
        if (previous != null)
            foreach (var sibling in world.LivingCharacters().Where(c => c.Id != heir.Id && c.HouseId == heir.HouseId && c.Age >= AgeOfMajority && IsChildOf(c, previous)))
            {
                sibling.Flags.Add("passed_over");
                var r = world.Relationship(sibling.Id, heir.Id); r.Disposition = Math.Clamp(r.Disposition - 20, -100, 100);
            }
        world.ProtagonistId = heir.Id;
        world.Pools[heir.Id] = ActionPools.ForPlayer();
        world.WorldCounters["generation"] = world.Counter("generation") + 1;
        world.Successions.Add(new SuccessionRecord
        {
            Turn = world.Turn, Generation = world.Counter("generation"),
            DeceasedId = previous?.Id ?? "", DeceasedName = previous?.Name ?? "", DeceasedAge = previous?.Age ?? 0,
            Cause = previous == null ? "death" : previous.Alive ? "abdication" : previous.HouseId != heir.HouseId ? "adoption" : "death",
            HeirId = heir.Id, HeirName = heir.Name, HeirAge = heir.Age,
            LostCareer = previous != null && previous.CareerRank > 0 ? $"{previous.CareerTrack} rank {previous.CareerRank}" : ""
        });
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
        // someone the head named stands first, whatever the law says (docs/AMBITIONS.md)
        if (world.House(deceased.HouseId) is { NamedHeirId.Length: > 0 } named && world.Char(named.NamedHeirId) is { Alive: true } chosen && chosen.HouseId == deceased.HouseId && chosen.Id != deceased.Id) return chosen;
        // Found family (docs/HOUSEHOLD.md): those taken into the house stand after everyone of the blood, whatever the law. One who
        // was adopted is of the blood, and a child of the head who adopted them.
        var blood = members.Where(c => !HouseholdSystem.IsFound(c)).ToList();
        if (blood.Count > 0) members = blood;
        return law switch
        {
            "ultimogeniture" => members.OrderByDescending(c => IsChildOf(c, deceased)).ThenBy(c => c.Age).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault(),
            "seniority" => members.OrderByDescending(c => c.Age).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault(),
            "merit" => members.OrderByDescending(c => c.Age >= AgeOfMajority).ThenByDescending(c => c.Skills.Values.Sum() + c.CareerRank * 2).ThenByDescending(c => c.Age).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault(),
            _ => members.OrderByDescending(c => IsChildOf(c, deceased)).ThenByDescending(c => c.Age).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault()
        };
    }

    private static bool IsChildOf(Character c, Character parent) =>
        c.MotherId == parent.Id || c.FatherId == parent.Id || c.AdoptedById == parent.Id;
}
