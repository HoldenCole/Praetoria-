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
    private static readonly string[] DeedFlags = { ProdigyFlag, MasteryFlag, TutorDismissedFlag, "kin_would_not_study", KinTaughtFlag };

    public static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }
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
    /// <summary>What a year of upbringing does to what a child of the house thinks of its head, cause by cause: brought up as
    /// their own bent runs, a point; a tutor paid for, a point more; set against their bent, a point the other way. An
    /// upbringing alone takes a child's regard no higher than <see cref="RaisedCap"/>.</summary>
    public const int RaisedWell = 1, RaisedTutor = 1, RaisedAgainst = -1, RaisedCap = 40;
    public static List<(string why, int by)> Raised(World w, Character c, Character head)
    {
        var list = new List<(string, int)>(); if (c.Age < SchoolAge || c.Age >= Adult || c.Id == head.Id) return list;
        var (_, _, against) = Lesson(w, c);
        if (against) { list.Add(("set against their bent", RaisedAgainst)); return list; }
        if ((w.GetRelationship(c.Id, head.Id)?.Disposition ?? 0) >= RaisedCap) return list;
        list.Add(("brought up as their own bent runs", RaisedWell));
        if (c.Tutor > 0) list.Add(("a tutor paid for", RaisedTutor));
        return list;
    }

    public static int Bent(Character c, string skill) => (int)(Hash(c.Id + ":" + skill) % 4) - 1;

    /// <summary>How far this person can go in a skill: five, their own bent, three more for an aptitude that bears on it, one more
    /// for the brilliant. Three at the least, ten at the most.</summary>
    public static int Ceiling(Character c, string skill)
    {
        int v = 5 + Bent(c, skill);
        foreach (var (trait, s) in Gifts) if (s == skill && c.AptitudeTraits.Contains(trait)) { v += 3; break; }
        if (c.AptitudeTraits.Contains("Brilliant")) v += 1;
        // one who is already further than that was written so, and what they have reached is evidently what was in them
        return Math.Clamp(Math.Max(v, c.Skill(skill)), 3, Max);
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

    // ---------------- asking, not telling (docs/EDUCATION.md) ----------------

    public const int AskEvery = 3, TeachLead = 2; public const string StudyRefusedFlag = "kin_would_not_study", StudyRefusedChar = "would_not_study";
    private static string AskedKey(string id) => "asked_to_study:" + id;
    private static int RegardOf(World w, Character from, Character to) => w.GetRelationship(from.Id, to.Id)?.Disposition ?? 0;

    public static string? AskRefusal(World w, Character? head, Character? who, string skill)
    {
        if (head?.Alive != true || who?.Alive != true || who.Id == head.Id || who.HouseId != head.HouseId) return "not of your house";
        if (who.Age < Adult) return "a child is brought up, not asked";
        if (!All.Contains(skill)) return "no such study";
        if (who.Skill(skill) >= Ceiling(who, skill)) return "they have gone as far in it as they can";
        if (who.Study == skill) return "it is what they study already";
        return w.WorldCounters.TryGetValue(AskedKey(who.Id), out int last) && w.Turn - last < AskEvery ? $"you asked them not long ago (again in {AskEvery - (w.Turn - last)} year(s))" : null;
    }
    /// <summary>Whether they would do it for the asking: the loyal and those who think well of the head will; the wilful who do not, will not.</summary>
    public static bool WouldStudy(World w, Character head, Character who, string skill) =>
        who.NatureTraits.Contains("Loyal") || RegardOf(w, who, head) >= (CourtshipSystem.Wilful(who) ? 40 : Bent(who, skill) < 0 ? 20 : 0);
    public const int PriceNear = 4, PriceFar = 8, NearBy = 12, FarBy = 25, ThanksCredits = 4, ThanksRegard = 8, ThanksEvery = 3;
    public const string KinTaughtFlag = "kin_taught_kin", TaughtKinChar = "taught_one_of_the_house";
    /// <summary>What they would take to do it: nothing, if they would for the asking; a sum for their own purse, if they are not far
    /// from saying yes; and −1 if they would not for anything.</summary>
    public static int StudyPrice(World w, Character head, Character who, string skill)
    {
        if (WouldStudy(w, head, who, skill)) return 0;
        int need = CourtshipSystem.Wilful(who) ? 40 : Bent(who, skill) < 0 ? 20 : 0, by = need - RegardOf(w, who, head);
        return by <= NearBy ? PriceNear : by <= FarBy ? PriceFar : -1;
    }
    public static string? AskAndPayRefusal(World w, Character? head, Character? who, string skill)
    {
        if (head?.Alive != true || who?.Alive != true || who.Id == head.Id || who.HouseId != head.HouseId) return "not of your house";
        if (who.Age < Adult) return "a child is brought up, not asked";
        if (!All.Contains(skill)) return "no such study";
        if (who.Skill(skill) >= Ceiling(who, skill)) return "they have gone as far in it as they can";
        if (who.Study == skill) return "it is what they study already";
        int price = StudyPrice(w, head, who, skill);
        return price == 0 ? "they would do it for the asking" : price < 0 ? "they will not, for anything you could offer" : head.Purse >= price ? null : $"they would want {price} credits of your own, and your purse does not hold it";
    }
    /// <summary>The head asks, and pays what they would take. It is a bargain, and nobody thinks the worse of anybody.</summary>
    public static void AskAndPay(World w, Character head, Character who, string skill)
    {
        int price = StudyPrice(w, head, who, skill); head.Purse -= price; who.Purse += price; who.Study = skill; who.Flags.Remove(StudyRefusedChar); w.WorldCounters[AskedKey(who.Id)] = w.Turn;
        w.Log($"{who.Name} turns to {skill} for {price} credits of {head.Name}'s own. It was a bargain, and both of them call it one.");
    }

    /// <summary>The same bargain paid in something that is not money: a word in the right place for them. One influence where
    /// the sum would have been small, two where it would have been large; nothing if no sum would do.</summary>
    public static int FavourPrice(World w, Character head, Character who, string skill) { int price = StudyPrice(w, head, who, skill); return price <= 0 ? price : price == PriceNear ? 1 : 2; }
    public static string? AskAndSpeakRefusal(World w, Character? head, Character? who, string skill)
    {
        var no = AskAndPayRefusal(w, head, who, skill); if (no == null || !no.Contains("purse")) return no;
        return null;   // the purse is not what is asked for here: the influence is the command's to check
    }
    /// <summary>The head asks, and does something for them that is not money. They turn to it, and think the better of the head.</summary>
    public static void AskAndSpeak(World w, Character head, Character who, string skill)
    {
        who.Study = skill; who.Flags.Remove(StudyRefusedChar); w.WorldCounters[AskedKey(who.Id)] = w.Turn;
        var r = w.Relationship(who.Id, head.Id); r.Disposition = Math.Clamp(r.Disposition + 2, -100, 100);
        w.Log($"{who.Name} turns to {skill}. {head.Name} had a word said for them in the right place, and it was understood what the word was for.");
    }

    private static string ThankedKey(string id) => "thanked_for_teaching:" + id;
    /// <summary>The head thanks one of the house who has another of it under their wing, with a purse out of their own.</summary>
    public static string? ThankRefusal(World w, Character? head, Character? pupil)
    {
        if (head?.Alive != true || pupil?.Alive != true || pupil.HouseId != head.HouseId) return "not of your house";
        var teacher = pupil.MentorId.Length > 0 ? w.Char(pupil.MentorId) : null;
        if (teacher?.Alive != true || teacher.Id == head.Id || teacher.HouseId != head.HouseId) return "nobody of the house is teaching them";
        if (w.WorldCounters.TryGetValue(ThankedKey(teacher.Id), out int last) && w.Turn - last < ThanksEvery) return "you thanked them not long ago";
        return head.Purse >= ThanksCredits ? null : $"needs {ThanksCredits} credits of your own";
    }
    public static void Thank(World w, Character head, Character pupil)
    {
        var teacher = w.Char(pupil.MentorId)!; head.Purse -= ThanksCredits; teacher.Purse += ThanksCredits; w.WorldCounters[ThankedKey(teacher.Id)] = w.Turn;
        var r = w.Relationship(teacher.Id, head.Id); r.Disposition = Math.Clamp(r.Disposition + ThanksRegard, -100, 100);
        w.Log($"{head.Name} thanks {teacher.Name} for what they are doing for {pupil.Name}, and not only in words.");
    }

    /// <summary>The head asks one of the house to turn their study to something. It is theirs to say yes to.</summary>
    public static bool Ask(World w, Character head, Character who, string skill)
    {
        w.WorldCounters[AskedKey(who.Id)] = w.Turn;
        if (!WouldStudy(w, head, who, skill))
        {
            var r = w.Relationship(who.Id, head.Id); r.Disposition = Math.Clamp(r.Disposition - 3, -100, 100); who.Flags.Add(StudyRefusedChar); Mark(w, StudyRefusedFlag);
            w.Log($"{head.Name} asks {who.Name} to give their time to {skill}. {who.Name} says that what they study is their own affair."); return false;
        }
        who.Study = skill;
        if (Bent(who, skill) < 0) { var r = w.Relationship(who.Id, head.Id); r.Disposition = Math.Clamp(r.Disposition - 2, -100, 100); }
        w.Log($"{who.Name} turns to {skill}, because {head.Name} asked it{(Bent(who, skill) < 0 ? ", though it is not where their mind runs" : "")}."); return true;
    }

    /// <summary>What a teacher of the house could pass on to this pupil: the skill they are furthest ahead in that the pupil can still learn.</summary>
    public static string Lesson(Character teacher, Character pupil) => teacher.Skills.Where(kv => kv.Value >= pupil.Skill(kv.Key) + TeachLead && pupil.Skill(kv.Key) < Ceiling(pupil, kv.Key))
        .OrderByDescending(kv => kv.Value - pupil.Skill(kv.Key)).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key).FirstOrDefault() ?? "";
    public static string? TeachRefusal(World w, Character? head, Character? pupil, Character? teacher)
    {
        if (head?.Alive != true || pupil?.Alive != true || teacher?.Alive != true || pupil.HouseId != head.HouseId || teacher.HouseId != head.HouseId) return "not of your house";
        if (pupil.Id == head.Id || teacher.Id == head.Id || pupil.Id == teacher.Id) return "it is between two others of the house";
        if (teacher.Age < Adult) return "the teacher is a child";
        if (pupil.Age < SchoolAge) return "too young for lessons";
        if (pupil.MentorId.Length > 0 && w.Char(pupil.MentorId) is { Alive: true }) return "they are under someone's wing already";
        if (Lesson(teacher, pupil).Length == 0) return "they have nothing to teach them";
        return RegardOf(w, teacher, head) >= 0 ? null : "they will not do it for you";
    }
    /// <summary>The head asks one of the house to take another under their wing.</summary>
    public static void SetToTeach(World w, Character head, Character pupil, Character teacher)
    {
        pupil.MentorId = teacher.Id; pupil.JoinedTurn = w.Turn;
        foreach (var (a, b) in new[] { (pupil.Id, teacher.Id), (teacher.Id, pupil.Id) }) { var r = w.Relationship(a, b); r.Disposition = Math.Clamp(r.Disposition + 5, -100, 100); }
        w.Log($"{teacher.Name} takes {pupil.Name} in hand, at {head.Name}'s asking, and will teach them {Lesson(teacher, pupil)}.");
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
        if (LadderSystem.Seat(w, c.Id) is { } seat) list.Add((seat.OfficeId == LadderSystem.Praetor ? "law" : seat.OfficeId == LadderSystem.Aedile ? "administration" : "economics", PostPoints, "a magistracy"));
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
        if (!w.HasFlag(StudyRefusedFlag)) foreach (var ch in w.Characters.Values) ch.Flags.Remove(StudyRefusedChar);
        if (!w.HasFlag(KinTaughtFlag)) foreach (var ch in w.Characters.Values) ch.Flags.Remove(TaughtKinChar);

        // tutors are paid for, or let go
        if (mine != null)
            foreach (var c in w.LivingCharacters().Where(c => c.HouseId == mine.Id && c.Tutor > 0).OrderBy(c => c.Id, StringComparer.Ordinal))
            {
                int cost = TutorCost(c.Tutor);
                if (mine.Treasury.Credits >= cost) mine.Treasury.Credits -= cost;
                else { c.Tutor = 0; Mark(w, TutorDismissedFlag); w.Log($"{mine.Name} cannot pay {c.Name}'s tutor, who has gone."); w.Tell("warn", $"{c.Name}'s tutor has gone: the treasury held {mine.Treasury.Credits} credits and the year's fee was {cost}. Another can be engaged when there is coin", "dynasty", c.Id); }
            }

        var admirals = w.Fleets.Values.Select(f => f.AdmiralId).Where(a => a.Length > 0).ToHashSet();
        var governors = w.Holdings.Values.Select(h => h.GovernorId).Where(g => g.Length > 0).ToHashSet();
        var officers = w.Offices.Select(o => o.CharacterId).ToHashSet();
        var magistrates = w.Magistrates.GroupBy(m => m.CharacterId).ToDictionary(g => g.Key, g => g.First().OfficeId);
        var heads = w.Houses.Values.Select(h => Economy.HeadOf(w, h)?.Id ?? "").Where(id => id.Length > 0).ToHashSet();
        var libraries = w.Heirlooms.Where(h => h.Kind == "library").Select(h => w.Char(h.HolderId)).Where(c => c is { Alive: true }).Select(c => c!.HouseId).ToHashSet();

        foreach (var c in w.LivingCharacters().Where(c => c.Age >= SchoolAge).OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
        {
            if (c.Age < Adult)
            {
                // brought up toward what they show a bent for, unless the head has set another course
                c.Flags.Remove(ResistsChar);
                var (skill, points, against) = Lesson(w, c); Gain(w, c, skill, points);
                if (c.Age % 3 == 0) Gain(w, c, "charisma", 1);
                if (mine != null && c.HouseId == mine.Id && p != null && c.Id != p.Id)
                {
                    // what a child thinks of the head follows how they are raised (docs/PLAYTHROUGH.md, Medium, Family, F102)
                    if (against) { c.Flags.Add(ResistsChar); w.WorldFlags.Add(ResistsFlag); }
                    int by = Raised(w, c, p).Sum(x => x.by);
                    if (by != 0) { var r = w.Relationship(c.Id, p.Id); r.Disposition = Math.Clamp(r.Disposition + by, -100, 100); }
                }
                continue;
            }
            // by doing
            var track = TrackSkills(c.CareerTrack);
            if (track.Length > 0) Gain(w, c, track[(int)(((uint)w.Turn + Hash(c.Id)) % (uint)track.Length)], TrackPoints);
            if (admirals.Contains(c.Id)) Gain(w, c, "tactics", PostPoints);
            if (governors.Contains(c.Id)) Gain(w, c, "administration", PostPoints);
            if (officers.Contains(c.Id)) Gain(w, c, "oratory", PostPoints);
            if (magistrates.TryGetValue(c.Id, out var rungId)) Gain(w, c, rungId == LadderSystem.Praetor ? "law" : rungId == LadderSystem.Aedile ? "administration" : "economics", PostPoints);
            if (heads.Contains(c.Id)) Gain(w, c, "diplomacy", 1);
            if (mine != null && c.HouseId == mine.Id)
                foreach (var d in mine.Duties.Where(d => d.HolderId == c.Id)) Gain(w, c, d.Role == HouseholdSystem.Steward ? "economics" : "logistics", PostPoints);
            // by study: their own choice, the protagonist's included
            bool player = p != null && c.Id == p.Id;
            if (!player && (c.Study.Length == 0 || c.Skill(c.Study) >= Ceiling(c, c.Study))) c.Study = ChooseStudy(w, c);
            if (c.Study.Length > 0) Gain(w, c, c.Study, StudyPoints + TutorPoints(c.Tutor) + (libraries.Contains(c.HouseId) ? WealthSystem.LibraryPoints : 0));
        }
    }
}
