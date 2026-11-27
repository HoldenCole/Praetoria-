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
    /// <summary>Marks of a year, for the player's house only (docs/events/family.md): a child is born to the house or to one of it, a
    /// child of the house is six and the schoolroom begins, one of the house is sixteen. Raised as the year turns and cleared the year
    /// after, like every other mark; the persons are found by their age.</summary>
    public const string BornFlag = "born_to_the_house", SchoolingFlag = "schooling_begins", KinOfAgeFlag = "kin_came_of_age";
    /// <summary>A child born to a child of the head (born or adopted), for as long as the birth is news: a sibling of the head bound by
    /// blood in a scenario is not a child of the head, and the parents' ids say which is which where a bond cannot.</summary>
    public const string GrandchildChar = "grandchild_of_the_head";
    private static readonly string[] DeedFlags = { BornFlag, SchoolingFlag, KinOfAgeFlag };
    private static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }
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
        foreach (var f in DeedFlags) if (world.WorldFlags.Contains(f) && world.Counter("mark:" + f) < world.Turn - 1) { world.WorldFlags.Remove(f); world.WorldCounters.Remove("mark:" + f); }
        if (!world.WorldFlags.Contains(BornFlag)) foreach (var c in world.Characters.Values) c.Flags.Remove(GrandchildChar);
        string ours = world.Protagonist?.HouseId ?? "";
        foreach (var c in world.Characters.Values.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            if (!c.Alive) continue;
            c.Age++;
            if (c.Age == AgeOfMajority) { ComeOfAge(world, c, rng); if (c.HouseId == ours && c.Id != world.ProtagonistId) Mark(world, KinOfAgeFlag); }
            if (c.Age == EducationSystem.SchoolAge && c.HouseId == ours && c.Id != world.ProtagonistId) Mark(world, SchoolingFlag);
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
            if (house.Id == mine || World.Outsider(house.Id)) continue;
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
            if (c.CareerRank >= (c.Age - AgeOfMajority) / YearsPerRank + (AcademySystem.Laurels(c) >= 2 ? 1 : 0)) continue;   // no admirals at twenty-two; the best of a year are a rung ahead of it
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
    private static uint Hash(string s) { uint h = 2166136261; foreach (char ch in s) h = (h ^ ch) * 16777619; return h; }
    /// <summary>What someone brought up as they were is likely to want when they come of age: the service they were raised toward
    /// gives it, and a poor house's steward wants to mend it. One who was set against their own bent wants something of their own.</summary>
    public static string RaisedToWant(World w, Character c, bool against)
    {
        if (against) return c.NatureTraits.Contains("Proud") || c.NatureTraits.Contains("Ambitious") ? "outshine_all_rivals" : "win_a_great_love";
        return c.CareerTrack switch
        {
            "military" => "earn_a_command",
            "law" => "master_the_senate",
            "stewardship" => w.House(c.HouseId) is { } h && h.Treasury.Credits < AmbitionSystem.PoorAt ? "restore_house_fortunes" : "amass_a_fortune",
            _ => ""
        };
    }

    private void ComeOfAge(World world, Character c, IRng rng)
    {
        // what they were set to as a child, and whether it went against their bent, before the upbringing is closed (docs/AMBITIONS.md)
        string raisedAs = c.Upbringing; bool against = raisedAs.Length > 0 && raisedAs != EducationSystem.BentGroup(c);
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
        {
            // The dice are still thrown (and the same number of times), but two in three want what they were brought up toward; one
            // set against their bent wants something that is nobody's plan but their own.
            string dice = Content.Traits.Ambitions[rng.NextInt(0, Content.Traits.Ambitions.Count)].Id, raised = RaisedToWant(world, c, against);
            c.Ambition = raised.Length > 0 && Content.Traits.Ambitions.Any(a => a.Id == raised) && Hash(c.Id + ":raised") % 3 != 0 ? raised : dice;
        }
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
            if (c is not { Alive: true }) continue;
            // health bears on it (docs/LIFE.md): the hale outlive their years, the frail do not, and the young die only of what ails them
            double chance = c.Age >= MortalityAge ? DeathProbability(c.Age) * LifeSystem.DeathFactor(c) : LifeSystem.EarlyDeath(c);
            if (chance <= 0) continue;   // young and well ⇒ no roll, no RNG
            if (rng.NextDouble() < chance)
                Die(world, id, c.Age >= MortalityAge ? "of age" : "of what ailed them");
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
            if (rng.NextDouble() < BirthChance * CourtshipSystem.Fertility(world, mother, father) * (1 + CourtSystem.NursePerSkill * CourtSystem.Skill(world, mother.HouseId, CourtSystem.Nurse)))   // a warm marriage bears more, a cold one fewer, an estranged one none
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
        if (world.Protagonist is { } mine && (lineParent.HouseId == mine.HouseId || mother.HouseId == mine.HouseId || father.HouseId == mine.HouseId)) Mark(world, BornFlag);   // a grandchild born under another name is news too
        string sex = rng.NextDouble() < 0.5 ? "male" : "female";
        // someone taken into a house keeps their own name, and so do their children
        string surname = HouseholdSystem.IsFound(lineParent) && lineParent.Name.Contains(' ') ? lineParent.Name[(lineParent.Name.LastIndexOf(' ') + 1)..] : (house?.Name ?? lineParent.HouseId).Replace("House ", "");
        string given = $"Scion {n}";
        if (Content is { Names.IsEmpty: false })
        {
            // no two living kin share a given name — a house with two Dariuses reads like a bug, not a family
            var taken = world.Characters.Values.Where(k => k.Alive && k.HouseId == lineParent.HouseId).Select(k => k.Name.Split(' ')[0]).ToHashSet();
            taken.Add(surname);   // nor is anybody given their own surname: "Orrin Orrin" reads like a bug too
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
        world.Characters[child.Id] = child; CourtSystem.Born(world, child);
        if (world.Protagonist is { } grand && new[] { mother, father }.Any(x => x.Id != grand.Id && (x.MotherId == grand.Id || x.FatherId == grand.Id || x.AdoptedById == grand.Id))) child.Flags.Add(GrandchildChar);
        house?.Members.Add(child.Id);

        foreach (var parent in new[] { mother.Id, father.Id })
        {
            world.Relationship(parent, child.Id).Bond = BondType.Blood;
            world.Relationship(parent, child.Id).BondStrength = 100;
            world.Relationship(child.Id, parent).Bond = BondType.Blood;
            world.Relationship(child.Id, parent).BondStrength = 100;
        }
        world.Log($"A child, {child.Name}, is born to {father.Name} and {mother.Name}.");
        if (world.Protagonist is { } head && child.HouseId == head.HouseId)
        {
            int place = Line(world, head).FindIndex(x => x.Id == child.Id) + 1;
            world.Tell("good", $"A child is born to your house: {child.Name}, {(child.Sex == "female" ? "daughter" : "son")} of {father.Name} and {mother.Name}" + (place == 1 ? ", and now your heir" : place > 1 ? $", {Nth(place)} in the line" : ""), "dynasty", child.Id);
        }
        LifeSystem.Note(world, mother, "child", $"{given} was born."); LifeSystem.Note(world, father, "child", $"{given} was born.");
    }

    private static string Nth(int n) => n % 100 is 11 or 12 or 13 ? n + "th" : (n % 10) switch { 1 => n + "st", 2 => n + "nd", 3 => n + "rd", _ => n + "th" };

    /// <summary>Kill a character (natural death, assassination, Bloody Event). If it was the
    /// protagonist, pass the seat to an heir — or end the dynasty. No RNG (succession is deterministic).</summary>
    /// <param name="how">How they died, in a few words, where the caller knows ("in battle at Cinder Gate", "by a knife").</param>
    /// <summary>Someone already past the first rank and past this age when first seen has held the rank for years.</summary>
    public const int SettledAge = 26, SettledYears = 10;
    /// <summary>Notes, as each year opens, the year in which each of the living came to the rank they hold.</summary>
    public static void MarkRanks(World w)
    {
        foreach (var c in w.LivingCharacters())
        {
            if (c.RankSeen == c.CareerRank) continue;
            bool settled = c.RankSeen < 0 && c.CareerRank >= 2 && c.Age >= SettledAge;
            c.RankTurn = settled ? w.Turn - SettledYears : w.Turn; c.RankSeen = c.CareerRank;
        }
    }
    /// <summary>The years someone has held their present rank.</summary>
    public static int YearsInRank(World w, Character c) => c.RankSeen == c.CareerRank ? w.Turn - c.RankTurn : 0;

    public static void Die(World world, string id, string how = "")
    {
        var c = world.Char(id);
        if (c is not { Alive: true }) return;
        c.Alive = false;
        world.Log($"{c.Name} dies at {c.Age}.");
        if (world.Protagonist is { } mine && c.HouseId == mine.HouseId && c.Id != mine.Id)
        {
            /* the death as a report: what it left empty, each with the place it is filled, and what they had and whose it is now */
            var rec = new DeathRecord { Turn = world.Turn, PersonId = c.Id, Name = c.Name, Age = c.Age, How = how.Length > 0 ? how : c.Age >= 60 ? "of age" : "" };
            foreach (var h in world.Holdings.Values.Where(h => h.GovernorId == c.Id).OrderBy(h => h.Id, StringComparer.Ordinal)) rec.Posts.Add(new DeathPost { What = "the governing of " + h.Name, Target = "galaxy:" + h.SystemId });
            foreach (var f in world.Fleets.Values.Where(f => f.AdmiralId == c.Id).OrderBy(f => f.Id, StringComparer.Ordinal)) rec.Posts.Add(new DeathPost { What = "the command of " + f.Name, Target = "military:deck" });
            foreach (var f in world.Fleets.Values.Where(f => f.CaptainId == c.Id).OrderBy(f => f.Id, StringComparer.Ordinal)) rec.Posts.Add(new DeathPost { What = "the captaincy of " + f.Name, Target = "military:deck" });
            if (world.House(mine.HouseId) is { } hh) foreach (var d in hh.Duties.Where(d => d.HolderId == c.Id)) rec.Posts.Add(new DeathPost { What = "the place of " + d.Role, Target = "house:household" });
            foreach (var m in world.Magistrates.Where(m => m.CharacterId == c.Id)) rec.Posts.Add(new DeathPost { What = "an office of the Reach they held", Target = "senate:ladder" });
            string head = mine.Name;
            if (c.Purse > 0) rec.Passed.Add($"Their purse of {Money.Credits(c.Purse)} comes to {head}."); else if (c.Purse < 0) rec.Passed.Add($"They owed {Money.Credits(-c.Purse)}, and the house's treasury pays it.");
            var things = WealthSystem.HeldBy(world, c.Id).Select(i => i.Name).ToList(); if (things.Count > 0) rec.Passed.Add($"{char.ToUpperInvariant(string.Join(", ", things)[0])}{string.Join(", ", things)[1..]} {(things.Count == 1 ? "comes" : "come")} to {head}.");
            var own = world.Businesses.Values.Where(b => b.HeldById == c.Id).Select(b => b.Name).ToList(); if (own.Count > 0) rec.Passed.Add($"What was their own is the house's again: {string.Join(", ", own)}.");
            if (c.LentHead > 0) rec.Passed.Add($"{head} owed them {Money.Credits(c.LentHead)}, which is paid out of {head}'s purse as far as it goes and then forgotten.");
            world.Deaths.Add(rec); while (world.Deaths.Count > 20) world.Deaths.RemoveAt(0);
            world.Tell("warn", $"{c.Name} of your house has died at {c.Age}{(rec.How.Length > 0 ? ", " + rec.How : "")}" + (rec.Posts.Count > 0 ? ". Left empty: " + string.Join(", ", rec.Posts.Select(x => x.What)) : ""), "dynasty", c.Id);
        }
        LifeSystem.Died(world, c);   // buried and spoken over, while the house still knows whose it is (docs/LIFE.md)

        if (id == world.ProtagonistId) Succeed(world, c);
        ServiceSystem.Died(world, c);   // a command by service ends that day, and is told to the house that has the seat now (docs/MILITARY.md)
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
        LifeSystem.Note(world, heir, "head", $"Became head of {world.House(heir.HouseId)?.Name}.");
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
        InheritedRegard.Pass(world, previous, heir);   // the old head's friends and enemies come with the house (docs/LIFE.md)
        DisownSystem.SeatPassed(world, previous, heir);   // a child struck out with no cause may sue that the will was undutiful (docs/HOUSEHOLD.md)
        ServiceSystem.SeatPassed(world, previous);   // what was asked of the old head by name is not put to the heir (docs/MILITARY.md)
    }

    /// <summary>Does this person stand in the line to the head: of the house, and not struck from it unless nobody else is left to follow?
    /// (A report about an heir names only such a one, and the seat is handed in life only to such a one.)</summary>
    public static bool StandsInLine(World world, Character head, Character who) =>
        who.Alive && who.Id != head.Id && who.HouseId == head.HouseId && (!DisownSystem.IsStruck(who) || FindHeir(world, head)?.Id == who.Id);

    /// <summary>The successor to a head (GDD §14): the first of <see cref="Line"/>.</summary>
    public static Character? FindHeir(World world, Character deceased) => Line(world, deceased).FirstOrDefault();

    /// <summary>The line of succession to a head, in order (docs/PLAYTHROUGH.md 5: every screen that names an heir reads this, so
    /// that they agree). Someone the head named stands first, whatever the law says. Then the blood of the house in the order its
    /// law gives: the head's children before the rest, by age, under primogeniture. Last, and only when no one of the blood is
    /// left, those who are of the house but not of its blood: someone who married in, someone taken in, and a head who has
    /// already laid the house down.</summary>
    /// <param name="lawAs">Reckon the line as it would stand under another law, to say who the heir would be before the law is changed.</param>
    public static List<Character> Line(World world, Character deceased, string? lawAs = null)
    {
        var house = world.House(deceased.HouseId); string law = lawAs ?? house?.SuccessionLaw ?? "primogeniture";
        var all = world.Characters.Values.Where(c => c.Alive && c.Id != deceased.Id && c.HouseId == deceased.HouseId).ToList();
        IEnumerable<Character> Order(IEnumerable<Character> members) => ByLaw(members, law, deceased);
        bool Outside(Character c) => OutsideTheBlood(c, deceased);
        // one struck from the line (docs/HOUSEHOLD.md, "Struck from the line") stands after everyone else, and has the house only when nobody else is left
        var line = Order(all.Where(c => !Outside(c) && !DisownSystem.IsStruck(c))).Concat(Order(all.Where(c => Outside(c) && !DisownSystem.IsStruck(c)))).Concat(Order(all.Where(DisownSystem.IsStruck))).ToList();
        if (house is { NamedHeirId.Length: > 0 } && world.Char(house.NamedHeirId) is { Alive: true } chosen && chosen.HouseId == deceased.HouseId && chosen.Id != deceased.Id && !DisownSystem.IsStruck(chosen)) { line.Remove(chosen); line.Insert(0, chosen); }
        return line;
    }
    /// <summary>Of the house but not of its blood, for the line: someone who married in, someone taken in, and a head who has laid the house
    /// down. One who was adopted is of the blood.</summary>
    private static bool OutsideTheBlood(Character c, Character deceased) =>
        HouseholdSystem.IsFound(c) || (c.BornHouseId.Length > 0 && c.BornHouseId != deceased.HouseId && c.AdoptedById.Length == 0) || c.Flags.Contains(LifeSystem.RetiredChar);
    /// <summary>People in the order a law of succession gives them.</summary>
    private static IEnumerable<Character> ByLaw(IEnumerable<Character> members, string law, Character deceased) => law switch
    {
        "ultimogeniture" => members.OrderByDescending(c => IsChildOf(c, deceased)).ThenBy(c => c.Age).ThenBy(c => c.Id, StringComparer.Ordinal),
        "seniority" => members.OrderByDescending(c => c.Age).ThenBy(c => c.Id, StringComparer.Ordinal),
        "merit" => members.OrderByDescending(c => c.Age >= AgeOfMajority).ThenByDescending(c => c.Skills.Values.Sum() + c.CareerRank * 2).ThenByDescending(c => c.Age).ThenBy(c => c.Id, StringComparer.Ordinal),
        _ => members.OrderByDescending(c => IsChildOf(c, deceased)).ThenByDescending(c => c.Age).ThenBy(c => c.Id, StringComparer.Ordinal)
    };
    /// <summary>What a law of succession prefers, in a line.</summary>
    public static string LawSays(string law) => law switch
    {
        "ultimogeniture" => "your youngest child comes first, then the rest of the house by youth",
        "seniority" => "the eldest of the house comes first, child of yours or not",
        "merit" => "the ablest grown member of the house comes first, by skill and rank in a service",
        _ => "your eldest child comes first, then the rest of the house by age",
    };
    /// <summary>Why the first of the line stands first, in a few words, for the screens. When someone struck from the line would have stood
    /// before them, it says the heir is the first not struck out; when the heir is struck out themselves, that nobody else is left.</summary>
    public static string HeirBy(World world, Character head)
    {
        var house = world.House(head.HouseId); var heir = FindHeir(world, head); if (heir == null || house == null) return "";
        if (house.NamedHeirId == heir.Id) return "named by you";
        string law = house.SuccessionLaw.Replace('_', ' ');
        bool struck = DisownSystem.IsStruck(heir), outside = OutsideTheBlood(heir, head);
        // someone struck out who, but for it, would stand before the heir under the law
        bool passed = !struck && world.Characters.Values.Any(c => c.Alive && c.Id != head.Id && c.HouseId == head.HouseId && DisownSystem.IsStruck(c)
            && (outside && !OutsideTheBlood(c, head) || outside == OutsideTheBlood(c, head) && ByLaw(new[] { heir, c }, house.SuccessionLaw, head).First().Id == c.Id));
        string tail = struck ? ", though struck from the line, since nobody else is left" : passed ? " not struck from the line" : "";
        if (law == "merit") return "by merit: the ablest grown member of the house" + tail;
        if (law == "seniority") return "by seniority: the eldest of the house" + tail;
        if (law == "ultimogeniture") return "by ultimogeniture: your youngest child" + tail;
        if (IsChildOf(heir, head)) return "by primogeniture: your eldest child" + tail;
        bool children = world.Characters.Values.Any(c => c.Alive && c.HouseId == head.HouseId && IsChildOf(c, head));
        return children && passed ? "by primogeniture: your children are struck from the line, so the eldest of the blood" : "by primogeniture: you have no child, so the eldest of the blood" + tail;
    }

    private static bool IsChildOf(Character c, Character parent) =>
        c.MotherId == parent.Id || c.FatherId == parent.Id || c.AdoptedById == parent.Id;
}
