using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Education, skills and getting better (docs/EDUCATION.md). A skill is no longer bought a step at a time. Every person has a
/// CEILING in each skill, set by what they are (their aptitudes and a bent of their own), and climbs toward it by LEARNING: points
/// that come from what they do (their service, a command, a governorship, an office), from what they choose to study, and from
/// being taught. Each step costs more than the last, so a master is the work of decades.
/// <para>Children from six are brought up without anyone managing it, toward what they show a bent for. The head may set a
/// different course or pay for a tutor; a child set against their bent learns slowly and resents it. Grown people choose their own
/// study (docs/PROCESS.md, "One person, not a house"); the head can pay for a teacher.</para>
/// RNG-free.
/// </summary>
public static class EducationSystem
{
    public const string Arms = "arms", Forum = "forum", Counting = "counting";
    public static readonly string[] Groups = { Arms, Forum, Counting };
    public static readonly string[] All = { "administration", "charisma", "diplomacy", "discipline", "economics", "engineering", "gunnery", "intrigue", "law", "leadership", "logistics", "oratory", "tactics" };
    public const int Max = 10, SchoolAge = 6, Adult = DynastySystem.AgeOfMajority, StudyPoints = 3, TrackPoints = 2, PostPoints = 2, TrainPoints = 4, BentPoints = 3, AgainstBentPoints = 1,
        GoodTutorCredits = 2, BestTutorCredits = 5, EarnAptitudeAt = 7, ProdigyAt = 4;
    public const string ResistsFlag = "child_resists_lessons", ProdigyFlag = "child_prodigy", MasteryFlag = "mastery_gained", TutorDismissedFlag = "tutor_dismissed";
    public const string ResistsChar = "resists_lessons", ProdigyChar = "prodigy";
    private static readonly string[] DeedFlags = { ProdigyFlag, MasteryFlag, TutorDismissedFlag };

    private static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }
    private static uint Hash(string s) { uint h = 2166136261; foreach (char ch in s) h = (h ^ ch) * 16777619; return h; }

    public static string[] GroupSkills(string group) => group switch
    {
        Arms => new[] { "tactics", "discipline", "gunnery", "leadership", "logistics" },
        Forum => new[] { "law", "oratory", "diplomacy", "charisma", "intrigue" },
        Counting => new[] { "administration", "economics", "engineering" },
        _ => Array.Empty<string>()
    };
    public static string GroupOf(string skill) => Groups.FirstOrDefault(g => GroupSkills(g).Contains(skill)) ?? Forum;
    public static string GroupName(string group) => group switch { Arms => "arms", Forum => "the forum", Counting => "the counting-house", _ => "their own bent" };
    public static string TrackFor(string group) => group switch { Arms => "military", Counting => "stewardship", _ => "law" };
    /// <summary>What a service teaches by being followed.</summary>
    public static string[] TrackSkills(string track) => track switch
    {
        "military" => new[] { "tactics", "discipline", "gunnery", "leadership", "logistics" },
        "stewardship" => new[] { "administration", "economics", "engineering" },
        "law" => new[] { "law", "oratory", "diplomacy" },
        _ => Array.Empty<string>()
    };
    /// <summary>The skill an aptitude lifts the ceiling of, and the aptitude a mastered skill reveals.</summary>
    private static readonly (string trait, string skill)[] Gifts =
    {
        ("Administrator", "administration"), ("Diplomat", "diplomacy"), ("Duelist", "discipline"), ("Greedy", "economics"), ("Orator", "oratory"), ("Paranoid", "intrigue"),
        ("Quartermaster", "logistics"), ("Schemer", "intrigue"), ("Strategist", "leadership"), ("Tactician", "tactics")
    };
    public static string LevelName(int level) => level <= 0 ? "untrained" : level <= 2 ? "a novice" : level <= 4 ? "able" : level <= 6 ? "skilled" : level <= 8 ? "an expert" : "a master";

    /// <summary>What there is in this person for this skill, apart from any aptitude: −1 to +2, fixed for life.</summary>
    public static int Bent(Character c, string skill) => (int)(Hash(c.Id + ":" + skill) % 4) - 1;

    /// <summary>How far this person can go in a skill: five, their own bent, three more for an aptitude that bears on it, one more
    /// for the brilliant. Three at the least, ten at the most.</summary>
    public static int Ceiling(Character c, string skill)
    {
        int v = 5 + Bent(c, skill);
        foreach (var (trait, s) in Gifts) if (s == skill && c.AptitudeTraits.Contains(trait)) { v += 3; break; }
        if (c.AptitudeTraits.Contains("Brilliant")) v += 1;
        return Math.Clamp(v, 3, Max);
    }

    /// <summary>The learning the next step costs from this level.</summary>
    public static int Need(int level) => 4 + level * 3;

    /// <summary>Which of the three schools this person is made for: where their ceilings stand highest.</summary>
    public static string BentGroup(Character c) =>
        Groups.OrderByDescending(g => GroupSkills(g).Sum(s => Ceiling(c, s)) * 10 / GroupSkills(g).Length).ThenBy(g => Hash(c.Id + g)).First();

    /// <summary>Add learning to a skill. Steps are taken as they are earned, never past the ceiling. True if a step was taken.</summary>
    public static bool Gain(World w, Character c, string skill, int points)
    {
        int level = c.Skill(skill), ceiling = Ceiling(c, skill);
        if (points <= 0 || level >= ceiling) { if (level >= ceiling) c.Learning.Remove(skill); return false; }
        int have = c.Learning.GetValueOrDefault(skill) + points; bool stepped = false;
        while (level < ceiling && have >= Need(level)) { have -= Need(level); level++; stepped = true; Reached(w, c, skill, level); }
        if (level >= ceiling) c.Learning.Remove(skill); else c.Learning[skill] = have;
        if (stepped) c.Skills[skill] = level;
        return stepped;
    }

    /// <summary>One whole step, as a mentor gives it, if the ceiling allows.</summary>
    public static bool Step(World w, Character c, string skill)
    {
        int level = c.Skill(skill); if (level >= Ceiling(c, skill)) return false;
        c.Skills[skill] = level + 1; Reached(w, c, skill, level + 1); return true;
    }

    private static void Reached(World w, Character c, string skill, int level)
    {
        var p = w.Protagonist; bool ours = p != null && c.HouseId == p.HouseId;
        if (ours && level >= 3) w.Log($"{c.Name} is {LevelName(level)} in {skill} now ({level}).");
        if (p != null && c.Id == p.Id && level is 5 or 7 or 9) Mark(w, MasteryFlag);
        if (ours && c.Age < Adult && level >= ProdigyAt && c.Flags.Add(ProdigyChar)) Mark(w, ProdigyFlag);
        if (level >= EarnAptitudeAt && c.AptitudeTraits.Count < 3)
            foreach (var (trait, s) in Gifts)
                if (s == skill && !c.AptitudeTraits.Contains(trait))
                {
                    c.AptitudeTraits.Add(trait);
                    if (ours) w.Log($"{c.Name} has shown what was in them: {trait}.");
                    break;
                }
    }

    /// <summary>What a grown person sets themselves to study when nobody tells them: what their ambition wants, or failing that
    /// whatever of their service has most room left in it.</summary>
    public static string ChooseStudy(World w, Character c)
    {
        string wanted = c.Ambition switch
        {
            "command_a_fleet" or "earn_a_command" => "tactics",
            "master_the_senate" => "oratory",
            "amass_a_fortune" or "restore_house_fortunes" => "economics",
            "seize_the_throne" => "intrigue",
            "win_a_great_love" => "charisma",
            _ => ""
        };
        if (wanted.Length > 0 && c.Skill(wanted) < Ceiling(c, wanted)) return wanted;
        var pool = TrackSkills(c.CareerTrack); if (pool.Length == 0) pool = All;
        var best = pool.Where(s => c.Skill(s) < Ceiling(c, s)).OrderByDescending(s => Ceiling(c, s)).ThenByDescending(s => c.Skill(s)).ThenBy(s => s, StringComparer.Ordinal).FirstOrDefault();
        return best ?? All.Where(s => c.Skill(s) < Ceiling(c, s)).OrderByDescending(s => Ceiling(c, s)).ThenBy(s => s, StringComparer.Ordinal).FirstOrDefault() ?? "";
    }

    /// <summary>What this grown person learns this year simply by doing what they do.</summary>
    public static List<(string skill, int points, string why)> Practice(World w, Character c)
    {
        var list = new List<(string, int, string)>(); var house = w.House(c.HouseId);
        var track = TrackSkills(c.CareerTrack);
        if (track.Length > 0) list.Add((track[(int)(((uint)w.Turn + Hash(c.Id)) % (uint)track.Length)], TrackPoints, "the service"));
        if (w.Fleets.Values.Any(f => f.AdmiralId == c.Id)) list.Add(("tactics", PostPoints, "commanding a fleet"));
        if (w.Holdings.Values.Any(h => h.GovernorId == c.Id && h.OwnerId == c.HouseId)) list.Add(("administration", PostPoints, "governing a world"));
        if (w.Offices.Any(o => o.CharacterId == c.Id)) list.Add(("oratory", PostPoints, "holding office"));
        if (house != null)
        {
            if (house.Duties.Any(d => d.HolderId == c.Id && d.Role == HouseholdSystem.Steward)) list.Add(("economics", PostPoints, "the steward's work"));
            if (house.Duties.Any(d => d.HolderId == c.Id && d.Role == HouseholdSystem.Marshal)) list.Add(("logistics", PostPoints, "the marshal's work"));
            if (Economy.HeadOf(w, house)?.Id == c.Id) list.Add(("diplomacy", 1, "leading the house"));
        }
        return list;
    }

    /// <summary>What a tutor adds a year, and what it costs.</summary>
    public static int TutorPoints(int tutor) => tutor >= 2 ? 2 : tutor == 1 ? 1 : 0;
    public static int TutorCost(int tutor) => tutor >= 2 ? BestTutorCredits : tutor == 1 ? GoodTutorCredits : 0;

    /// <summary>What a child of this upbringing learns this year: the skill, and how much.</summary>
    public static (string skill, int points, bool against) Lesson(World w, Character c)
    {
        string bent = BentGroup(c); string course = c.Upbringing.Length > 0 ? c.Upbringing : bent; bool against = course != bent;
        // two years in three on what they are best made for in that school, the third on the next: a child of sixteen has one
        // thing they are properly good at, not five they have barely met
        var skills = GroupSkills(course).OrderByDescending(s => Ceiling(c, s)).ThenBy(s => s, StringComparer.Ordinal).ToArray();
        string skill = c.Age % 3 == 2 && skills.Length > 1 ? skills[1] : skills[0];
        return (skill, (against ? AgainstBentPoints : BentPoints) + TutorPoints(c.Tutor), against);
    }

    public static void Tick(World w)
    {
        w.WorldFlags.Remove(ResistsFlag);
        foreach (var f in DeedFlags) if (w.WorldFlags.Contains(f) && w.Counter("mark:" + f) < w.Turn - 1) { w.WorldFlags.Remove(f); w.WorldCounters.Remove("mark:" + f); }
        var p = w.Protagonist; var mine = p != null ? w.House(p.HouseId) : null;
        if (!w.HasFlag(ProdigyFlag)) foreach (var ch in w.Characters.Values) ch.Flags.Remove(ProdigyChar);

        // tutors are paid for, or let go
        if (mine != null)
            foreach (var c in w.LivingCharacters().Where(c => c.HouseId == mine.Id && c.Tutor > 0).OrderBy(c => c.Id, StringComparer.Ordinal))
            {
                int cost = TutorCost(c.Tutor);
                if (mine.Treasury.Credits >= cost) mine.Treasury.Credits -= cost;
                else { c.Tutor = 0; Mark(w, TutorDismissedFlag); w.Log($"{mine.Name} cannot pay {c.Name}'s tutor, who has gone."); }
            }

        var admirals = w.Fleets.Values.Select(f => f.AdmiralId).Where(a => a.Length > 0).ToHashSet();
        var governors = w.Holdings.Values.Select(h => h.GovernorId).Where(g => g.Length > 0).ToHashSet();
        var officers = w.Offices.Select(o => o.CharacterId).ToHashSet();
        var heads = w.Houses.Values.Select(h => Economy.HeadOf(w, h)?.Id ?? "").Where(id => id.Length > 0).ToHashSet();

        foreach (var c in w.LivingCharacters().Where(c => c.Age >= SchoolAge).OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
        {
            if (c.Age < Adult)
            {
                // brought up toward what they show a bent for, unless the head has set another course
                c.Flags.Remove(ResistsChar);
                var (skill, points, against) = Lesson(w, c); Gain(w, c, skill, points);
                if (c.Age % 3 == 0) Gain(w, c, "charisma", 1);
                if (against && mine != null && c.HouseId == mine.Id && p != null)
                {
                    c.Flags.Add(ResistsChar); w.WorldFlags.Add(ResistsFlag);
                    var r = w.Relationship(c.Id, p.Id); r.Disposition = Math.Clamp(r.Disposition - 1, -100, 100);
                }
                continue;
            }
            // by doing
            var track = TrackSkills(c.CareerTrack);
            if (track.Length > 0) Gain(w, c, track[(int)(((uint)w.Turn + Hash(c.Id)) % (uint)track.Length)], TrackPoints);
            if (admirals.Contains(c.Id)) Gain(w, c, "tactics", PostPoints);
            if (governors.Contains(c.Id)) Gain(w, c, "administration", PostPoints);
            if (officers.Contains(c.Id)) Gain(w, c, "oratory", PostPoints);
            if (heads.Contains(c.Id)) Gain(w, c, "diplomacy", 1);
            if (mine != null && c.HouseId == mine.Id)
                foreach (var d in mine.Duties.Where(d => d.HolderId == c.Id)) Gain(w, c, d.Role == HouseholdSystem.Steward ? "economics" : "logistics", PostPoints);
            // by study: their own choice, the protagonist's included
            bool player = p != null && c.Id == p.Id;
            if (!player && (c.Study.Length == 0 || c.Skill(c.Study) >= Ceiling(c, c.Study))) c.Study = ChooseStudy(w, c);
            if (c.Study.Length > 0) Gain(w, c, c.Study, StudyPoints + TutorPoints(c.Tutor));
        }
    }
}
