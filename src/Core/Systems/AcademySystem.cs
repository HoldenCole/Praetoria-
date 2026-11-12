using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The Academy year (docs/ACADEMY.md; the owner's direction, 2026-11-11). A prologue apart from the world: the cadet enters at sixteen,
/// reads a subject a term for four terms among a cohort drawn from the real young of the Reach's houses, and graduates at seventeen into
/// a service and the main game. While it runs the world does not turn — the <see cref="TurnController"/> calls <see cref="BeginTerm"/> and
/// <see cref="EndTerm"/> in place of every world system, and the command bus refuses anything that is not the Academy's own business.
/// RNG-free: every roll is a hash of the term and the cadet.
/// <para>Two registers. The Republic's Academy is a school: lectures, examinations, the debating society, a war game on the tactical table;
/// rivalry is friendly and nobody is hurt. The Imperium's Institute is a war: cadets are sorted into Standards that take each other's
/// banners, the strong command the weak, and some cadets do not come back.</para>
/// </summary>
public sealed class AcademySystem
{
    public const int EntryAge = 16, CohortSize = 11, EventsPerTerm = 3, StandingWeight = 2, StudyBase = 3, StudySkillCap = 3, ClassmateBase = 5, ClassmateSpread = 5;
    public const string GraduatedFlag = "academy_graduated", FirstFlag = "first_of_the_year", HonoursFlag = "graduated_with_honours", StandardFallenFlag = "standard_fallen", WonInstituteFlag = "won_the_institute";
    public const string StandingKey = "standing", EdgeKey = "standard_edge", CaptainKey = "standard_captain";
    public static readonly string[] Tracks = { "military", "stewardship", "law" };

    private readonly AcademyCatalog _cat; private readonly Creation.NameCatalog _names;
    public AcademySystem(AcademyCatalog catalog, Creation.NameCatalog? names = null) { _cat = catalog; _names = names ?? Creation.NameCatalog.Empty; }

    private static uint Hash(string key) { uint h = 2166136261; foreach (var ch in key) h = (h ^ ch) * 16777619; return h; }

    /// <summary>The run opens at the Academy: the protagonist is a cadet of sixteen, bottom of every ladder, and the world waits a year.</summary>
    public static void Enter(World w)
    {
        var p = w.Protagonist; if (p == null) return;
        w.Academy = new AcademyState { Active = true, Register = w.IsImperium ? "imperium" : "republic" };
        p.Age = EntryAge; p.CareerRank = 0;
    }

    /// <summary>The first morning: the cohort is mustered from the real young of the houses (fifteen to nineteen, one house's children spread
    /// before another's are doubled), the senior officer of the Reach is its instructor, and under the Imperium the cadets are sorted.</summary>
    public void Muster(World w)
    {
        var a = w.Academy!; var p = w.Protagonist!;
        var young = w.LivingCharacters().Where(c => c.Id != p.Id && c.Age >= 15 && c.Age <= 19).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        var picked = new List<Character>();
        // a round a house: the great houses' children first (by title rank), no house twice until every house has sent one
        foreach (var round in young.GroupBy(c => c.HouseId).SelectMany(g => g.Select((c, i) => (c, i))).GroupBy(x => x.i).OrderBy(g => g.Key))
            picked.AddRange(round.Select(x => x.c).OrderByDescending(c => Progression.Title.RankOf(w.House(c.HouseId)?.Title ?? "")).ThenBy(c => c.Id, StringComparer.Ordinal));
        // a thin year is filled from the houses that sent no one: each enrols a son or daughter of sixteen to eighteen, a real member of the
        // house from now on (the greatest houses first), so the year is the Reach in miniature
        foreach (var h in w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId && h.Id != p.HouseId && picked.All(c => c.HouseId != h.Id) && w.LivingCharacters().Any(c => c.HouseId == h.Id))
                     .OrderByDescending(h => Progression.Title.RankOf(h.Title)).ThenBy(h => h.Id, StringComparer.Ordinal))
        {
            if (picked.Count >= CohortSize) break;
            uint hh = Hash("enrol:" + h.Id); string sex = hh % 2 == 0 ? "female" : "male"; var rng = new Rng.SplitMix64Rng(hh);
            string given = _names.IsEmpty ? "Cadet" : _names.Pick(h.Culture, sex, rng);
            var cadet = new Character { Id = "cadet_" + h.Id, Name = $"{given} {h.Name.Replace("House ", "")}", HouseId = h.Id, Age = 16 + (int)(hh / 7 % 3), Sex = sex };
            if (w.Characters.ContainsKey(cadet.Id)) continue;
            cadet.Skills[_cat.Subjects.Count > 0 ? _cat.Subjects[(int)(hh / 3 % (uint)_cat.Subjects.Count)].Skill : "discipline"] = 1;
            w.Characters[cadet.Id] = cadet; h.Members.Add(cadet.Id); w.Pools[cadet.Id] = ActionPools.ForNpc();
            picked.Add(cadet);
        }
        a.Cohort = picked.Take(CohortSize).Select(c => c.Id).ToList();
        foreach (var id in a.Cohort) a.Scores[id] = (w.Char(id)?.Skills.Values.Sum() ?? 0);
        a.InstructorId = w.LivingCharacters().Where(c => c.Id != p.Id && c.Age >= 35 && !a.Cohort.Contains(c.Id))
            .OrderByDescending(c => c.CareerTrack == "military" ? 1 : 0).ThenByDescending(c => c.CareerRank).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault()?.Id ?? "";
        if (a.IsImperium && _cat.Standards.Count > 0)
        {
            var all = a.Cohort.Prepend(p.Id).ToList();
            for (int i = 0; i < all.Count; i++) a.StandardOf[all[i]] = _cat.Standards[(int)((Hash("sort:" + all[i]) + (uint)i) % (uint)_cat.Standards.Count)].Id;
            a.Standard = a.StandardOf[p.Id];
            foreach (var s in _cat.Standards) a.StandardStrength[s.Id] = a.StandardOf.Values.Count(x => x == s.Id) * 2;
            w.Log($"The Institute sorts its cadets under {_cat.Standards.Count} Standards. {p.Name} stands under {_cat.Standard(a.Standard)?.Name ?? a.Standard}.");
        }
        else w.Log($"{p.Name} of {w.House(p.HouseId)?.Name} matriculates at {_cat.Register(a.Register).Name}, one of {a.Cohort.Count + 1} cadets of the year.");
    }

    /// <summary>A term opens: the counter advances and the pools refill. The briefing that follows carries only Academy events.</summary>
    public void BeginTerm(World w)
    {
        var a = w.Academy!;
        if (a.Term == 0) Muster(w);
        a.Term += 1;
        w.PoolsFor(w.ProtagonistId).Regenerate();
    }

    /// <summary>The subject the cadet reads this term: the one chosen, else the first of the track the house put them down for.</summary>
    public AcademySubject? FocusOf(World w)
    {
        var a = w.Academy!; var p = w.Protagonist!;
        return _cat.Subject(a.Focus) ?? _cat.Subjects.FirstOrDefault(s => s.Track == p.CareerTrack) ?? _cat.Subjects.FirstOrDefault();
    }

    /// <summary>Marks a term's reading earns: the base and what the cadet already knows of it (to a cap), plus an aptitude for the work.</summary>
    public static int MarksFor(Character p, AcademySubject s) => StudyBase + Math.Min(StudySkillCap, p.Skill(s.Skill)) + (p.AptitudeTraits.Contains("Brilliant") ? 1 : 0);

    /// <summary>A term closes: the reading is marked and the skill learned, the cohort moves up the order of merit, the Standards fight (under
    /// the Imperium), and after the last term the year passes out.</summary>
    public void EndTerm(World w)
    {
        var a = w.Academy!; var p = w.Protagonist!;
        if (FocusOf(w) is { } subject)
        {
            int marks = MarksFor(p, subject);
            a.Marks[subject.Id] = a.Marks.GetValueOrDefault(subject.Id) + marks;
            p.Skills[subject.Skill] = p.Skill(subject.Skill) + 1;
            w.Log($"{p.Name} reads {subject.Name} this term: {marks} marks, and {subject.Skill} {p.Skill(subject.Skill)}.");
        }
        foreach (var id in a.Cohort)
        {
            var c = w.Char(id); if (c is not { Alive: true }) continue;
            a.Scores[id] = a.Scores.GetValueOrDefault(id) + ClassmateBase + (int)(Hash($"term:{a.Term}:{id}") % ClassmateSpread) + (c.AptitudeTraits.Contains("Brilliant") ? 1 : 0);
            // a classmate learns too: one point a term in what they are already best at, or the subject the hash hands them
            var skill = c.Skills.Where(k => k.Value > 0).OrderByDescending(k => k.Value).ThenBy(k => k.Key, StringComparer.Ordinal).Select(k => k.Key).FirstOrDefault()
                        ?? (_cat.Subjects.Count > 0 ? _cat.Subjects[(int)(Hash("apt:" + id) % (uint)_cat.Subjects.Count)].Skill : "discipline");
            c.Skills[skill] = c.Skill(skill) + 1;
        }
        if (a.IsImperium) WarOfTheStandards(w);
        if (a.Term >= a.Terms) Graduate(w);
    }

    /// <summary>The cadet's score for the order of merit: standing in the cohort, twice, and every mark of the year.</summary>
    public static int ScoreOf(World w) => Math.Max(0, w.Counter(StandingKey)) * StandingWeight + (w.Academy?.Marks.Values.Sum() ?? 0);

    /// <summary>Where the cadet stands in the year: one more than the classmates who outscore them.</summary>
    public static int Place(World w)
    {
        var a = w.Academy; if (a == null) return 0; int mine = ScoreOf(w);
        return 1 + a.Cohort.Count(id => w.Char(id) is { Alive: true } && a.Scores.GetValueOrDefault(id) > mine);
    }

    /// <summary>The Institute's war (imperium register): each Standard gains a little each term and the cadet's gains what their choices won
    /// it; after the second and third terms the weakest standing Standard loses its banner to the strongest and its cadets serve the victor;
    /// about one term in three a cadet of another house does not come back from the night exercise. After the last term the strongest wins.</summary>
    private void WarOfTheStandards(World w)
    {
        var a = w.Academy!; var p = w.Protagonist!;
        foreach (var s in a.StandardStrength.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList())
            if (!a.Fallen.Contains(s)) a.StandardStrength[s] += (int)(Hash($"std:{a.Term}:{s}") % 3) + (s == a.Standard ? w.Counter(EdgeKey) + w.Counter(CaptainKey) : 0);
        w.WorldCounters.Remove(EdgeKey);
        var standing = a.StandardStrength.Where(k => !a.Fallen.Contains(k.Key)).OrderByDescending(k => k.Value).ThenBy(k => k.Key, StringComparer.Ordinal).Select(k => k.Key).ToList();
        if (a.Term is 2 or 3 && standing.Count > 2)
        {
            string victor = standing[0], loser = standing[^1];
            a.Fallen.Add(loser);
            foreach (var id in a.StandardOf.Where(k => k.Value == loser).Select(k => k.Key).ToList()) a.StandardOf[id] = victor;
            a.StandardStrength[victor] += a.StandardStrength[loser] / 2;
            w.Log($"{_cat.Standard(victor)?.Name ?? victor} takes the banner of {_cat.Standard(loser)?.Name ?? loser}; its cadets serve the victor now.");
            if (loser == a.Standard)
            {
                a.Standard = victor; w.WorldFlags.Add(StandardFallenFlag); w.WorldCounters.Remove(CaptainKey);
                w.WorldCounters[StandingKey] = w.Counter(StandingKey) - 2;
            }
        }
        // the Institute is not a school: a cadet of a house with someone left to carry the name may not come back
        if (Hash($"night:{a.Term}:{w.RngState}") % 3 == 0 && w.Counter("institute_dead") < Math.Max(1, a.Cohort.Count / 5))
        {
            var lost = a.Cohort.Select(id => w.Char(id)).Where(c => c is { Alive: true } && w.LivingCharacters().Count(o => o.HouseId == c.HouseId) >= 2 && w.House(c.HouseId) is { } h && Economy.HeadOf(w, h)?.Id != c.Id)
                .OrderBy(c => a.Scores.GetValueOrDefault(c!.Id)).ThenBy(c => c!.Id, StringComparer.Ordinal).FirstOrDefault();
            if (lost != null)
            {
                w.Log($"{lost.Name} of {w.House(lost.HouseId)?.Name} does not come back from the night exercise. The Institute posts the name and holds the next drill at dawn.");
                w.WorldCounters["institute_dead"] = w.Counter("institute_dead") + 1;
                DynastySystem.Die(w, lost.Id);
            }
        }
        if (a.Term >= a.Terms && standing.Count > 0 && standing[0] == a.Standard)
        {
            w.WorldFlags.Add(WonInstituteFlag); w.WorldCounters[StandingKey] = w.Counter(StandingKey) + (w.Counter(CaptainKey) > 0 ? 5 : 2);
            w.Log($"{_cat.Standard(a.Standard)?.Name ?? a.Standard} holds the last banner. The Institute is {(w.Counter(CaptainKey) > 0 ? $"{p.Name}'s" : "won, and " + p.Name + " stood in the ranks that won it")}.");
        }
    }

    /// <summary>The service the board would choose: the track of the subjects the cadet earned most marks in; the house's own if they read nothing.</summary>
    public string RecommendedService(World w)
    {
        var a = w.Academy!; var p = w.Protagonist!;
        var best = Tracks.Select(t => (t, marks: _cat.Subjects.Where(s => s.Track == t).Sum(s => a.Marks.GetValueOrDefault(s.Id))))
            .OrderByDescending(x => x.marks).ThenBy(x => Array.IndexOf(Tracks, x.t)).First();
        return best.marks > 0 ? best.t : Tracks.Contains(p.CareerTrack) ? p.CareerTrack : Tracks[0];
    }

    /// <summary>Passing out: the cadet is seventeen, commissioned at the first rung of the service they asked for (or the board's choice), placed
    /// in the order of merit; the first of the year lifts the house's name, the top third pass with honours; classmates still cadets are
    /// commissioned with them. The Academy closes and the world's first turn follows.</summary>
    public void Graduate(World w)
    {
        var a = w.Academy!; var p = w.Protagonist!; var house = w.House(p.HouseId);
        string track = Tracks.Contains(a.Service) ? a.Service : RecommendedService(w);
        a.Place = Place(w); a.Of = 1 + a.Cohort.Count(id => w.Char(id) is { Alive: true });
        p.CareerTrack = track; p.CareerRank = Math.Max(1, p.CareerRank); p.Age = EntryAge + 1;
        if (a.Place == 1) { p.Flags.Add(FirstFlag); if (house != null) house.Legitimacy = Math.Min(100, house.Legitimacy + 3); }
        if (a.Place <= Math.Max(1, a.Of / 3)) p.Flags.Add(HonoursFlag);
        foreach (var id in a.Cohort)
            if (w.Char(id) is { Alive: true } c && c.CareerRank == 0) { if (!Tracks.Contains(c.CareerTrack)) c.CareerTrack = Tracks[(int)(Hash("svc:" + id) % (uint)Tracks.Length)]; c.CareerRank = 1; }
        a.Active = false; a.Graduated = true;
        w.WorldFlags.Add(GraduatedFlag);
        w.WorldCounters["academy_place"] = a.Place;
        w.Log($"{p.Name} passes out of {_cat.Register(a.Register).Name} {Ordinal(a.Place)} of {a.Of}{(p.Flags.Contains(FirstFlag) ? ", first of the year" : p.Flags.Contains(HonoursFlag) ? ", with honours" : "")}, and is commissioned into the service.");
    }

    public static string Ordinal(int n) => n + (n % 100 is >= 11 and <= 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" });
}
