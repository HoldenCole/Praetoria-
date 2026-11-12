using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Presentation;

/// <summary>A subject on the Academy's timetable: what it trains, what the cadet knows and has earned, and what a term of it would earn.</summary>
public sealed record AcademySubjectLine(string Id, string Name, string Skill, string Track, string TrackName, string Description, int Level, int Marks, int NextMarks, bool Reading);
/// <summary>A cadet of the year on the order of merit (the protagonist among them).</summary>
public sealed record AcademyCadetLine(string Id, string Name, string HouseId, string HouseName, string Accent, string HouseStyle, int Age, string Sex, int Score, string BestAt,
    int Disposition, string Bond, string? StandardName, string? StandardAccent, bool Alive, bool You, IReadOnlyList<string> Marks);
/// <summary>A lesson as this run teaches it: the paragraphs for the register in force.</summary>
public sealed record AcademyLessonCard(string Id, int Term, string Title, string Brief, IReadOnlyList<string> Paragraphs);
/// <summary>A house that matters, as the orientation tells it.</summary>
public sealed record AcademyHouseLine(string Id, string Name, string Accent, string Style, string HeadName, string HeadRank, string Party, string PartyAccent, int Weight, int Worlds, int Hulls, int Disposition, bool Chair, bool Yours);
public sealed record AcademyFact(string Heading, string Text);
public sealed record AcademyStandardLine(string Id, string Name, string Accent, string Motto, int Strength, int Cadets, bool Fallen, bool Yours, bool Captain);
public sealed record AcademyServiceOption(string Track, string Name, string FirstRank, int Marks, bool Recommended, bool Asked);
/// <summary>Everything the Academy screen shows (docs/ACADEMY.md): the term, the timetable, the order of merit, the lessons, and the
/// orientation — the state of politics, the houses that matter, the cadet's own house — built from the live world.</summary>
public sealed record AcademyModel(string SchoolName, string Register, string Blurb, int Term, int Terms, string TermName, int Standing, int Score, int Place, int Of, string Tutorial,
    string? InstructorId, string? InstructorName, string? InstructorNote, IReadOnlyList<AcademySubjectLine> Subjects, IReadOnlyList<AcademyCadetLine> Cohort,
    IReadOnlyList<AcademyLessonCard> Lessons, IReadOnlyList<AcademyFact> Politics, IReadOnlyList<AcademyHouseLine> Houses, IReadOnlyList<AcademyFact> YourHouse,
    IReadOnlyList<AcademyStandardLine> Standards, IReadOnlyList<AcademyServiceOption> Services, bool FinalTerm, bool Graduated, IReadOnlyList<string> Record,
    IReadOnlyList<string> Friends, IReadOnlyList<string> Rivals, string ServiceName, string RankName);

public static class AcademyProjection
{
    public static AcademyModel? Build(TurnController tc, ContentDatabase content)
    {
        var w = tc.World; var a = w.Academy; var p = w.Protagonist; if (a == null || p == null) return null;
        var cat = content.Academy; var reg = cat.Register(a.Register); var house = w.House(p.HouseId)!;
        var focus = a.Active ? tc.Academy.FocusOf(w) : null;
        var subjects = cat.Subjects.Select(s => new AcademySubjectLine(s.Id, s.Name, s.Skill, s.Track, content.Careers.ById(s.Track)?.NameIn(w.Era) ?? s.Track, s.Description,
            p.Skill(s.Skill), a.Marks.GetValueOrDefault(s.Id), AcademySystem.MarksFor(p, s), focus?.Id == s.Id)).ToList();

        AcademyCadetLine Cadet(Character c, bool you)
        {
            var h = w.House(c.HouseId); var rel = you ? null : w.GetRelationship(c.Id, p.Id);
            var std = a.StandardOf.TryGetValue(c.Id, out var sid) ? cat.Standard(sid) : null;
            string best = string.Join(", ", c.Skills.Where(k => k.Value > 0).OrderByDescending(k => k.Value).ThenBy(k => k.Key, StringComparer.Ordinal).Take(2).Select(k => $"{k.Key} {k.Value}"));
            var marks = c.Flags.Where(f => f is "feud" or "humiliated" or "wounded" or "framed" or "owes_you" or "reconciled" or "marked_for_death" or "sabotaged" or "covered_for").OrderBy(f => f, StringComparer.Ordinal).Select(f => f.Replace('_', ' ')).ToList();
            return new AcademyCadetLine(c.Id, c.Name, c.HouseId, h?.Name ?? c.HouseId, h?.AccentColor ?? "#888", content.Titles.NameOf(h?.Title ?? "landless", w.Era), c.Age, c.Sex,
                you ? AcademySystem.ScoreOf(w) : a.Scores.GetValueOrDefault(c.Id), best, rel?.Disposition ?? 0, rel?.Bond is { } b && b != BondType.None ? b.ToString().ToLowerInvariant() : "",
                std?.Name, std?.Accent, c.Alive, you, marks);
        }
        var cohort = a.Cohort.Select(id => w.Char(id)).Where(c => c != null).Select(c => Cadet(c!, false)).Append(Cadet(p, true))
            .OrderByDescending(c => c.Alive).ThenByDescending(c => c.Score).ThenBy(c => c.You ? 0 : 1).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();

        var lessons = cat.Lessons.OrderBy(l => l.Term).Select(l => new AcademyLessonCard(l.Id, l.Term, a.IsImperium && l.ImperiumTitle.Length > 0 ? l.ImperiumTitle : l.Title, a.IsImperium && l.ImperiumBrief.Length > 0 ? l.ImperiumBrief : l.Brief, a.IsImperium && l.ImperiumFull.Count > 0 ? l.ImperiumFull : l.Full)).ToList();

        // ---- the orientation: the state of the Reach as it stands the year the cadet enters ----
        var senate = tc.Senate; var chair = RegionSystem.CapitalHolder(w); var facts = new List<AcademyFact>();
        string Head(House h) => Economy.HeadOf(w, h)?.Name ?? "no one";
        string capital = w.Galaxy is { Capital.Length: > 0 } g ? g.System(g.Capital)?.Name ?? g.Capital : "the capital";
        facts.Add(new AcademyFact(w.IsImperium ? "The Imperium" : "The Republic",
            w.IsImperium ? $"A throne stands. {ThroneSystem.Emperor(w)?.Name ?? chair?.Name ?? "No house"} holds it and every office with it; the houses hold their titles at its pleasure."
                         : $"No throne stands. The houses govern through the Senate. {(chair != null ? $"{chair.Name} holds {capital} and with it the chair, under {Head(chair)}" : $"No house holds {capital}; the chair stands empty")}."));
        var parties = w.Factions.Select(f => (f, seats: f.Members.Sum(m => senate.Weight(w, m)))).OrderByDescending(x => x.seats).ThenBy(x => x.f.Name, StringComparer.Ordinal).ToList();
        if (parties.Count > 0)
            facts.Add(new AcademyFact("The parties", string.Join(" ", parties.Select(x => $"{x.f.Name}: {x.f.Members.Count} house{(x.f.Members.Count == 1 ? "" : "s")}, {x.seats} seats, led by {w.House(x.f.LeaderHouseId)?.Name ?? "no one"}.")) +
                (FactionSystem.Of(w, house.Id) is { } mine ? $" {house.Name} sits with {mine.Name}." : $" {house.Name} sits with no party.")));
        if (!w.IsImperium)
        {
            var consuls = content.Offices.Defs.Where(o => o.Elected).Select(o => (o, h: OfficeSystem.Holding(w, o.Id))).ToList();
            int next = ElectionSystem.NextIn(w);
            facts.Add(new AcademyFact("The consuls", consuls.Any(x => x.h != null)
                ? string.Join(" ", consuls.Where(x => x.h != null).Select(x => $"{x.o.Name}: {w.House(x.h!.HouseId)?.Name}.")) + $" The next election is {next} year{(next == 1 ? "" : "s")} off."
                : $"The consular chairs are empty. The parties elect two consuls {(next <= 1 ? "within the year" : $"in {next} years")}, and every tenth year after."));
        }
        var officers = content.Offices.Defs.Where(o => !o.Elected).Select(o => (o, h: OfficeSystem.Holding(w, o.Id))).ToList();
        if (w.IsImperium) facts.Add(new AcademyFact("The offices", "The fleet, the purse and the order of business are the throne's. The Emperor holds every office of the Reach and lends them to no one for long."));
        else if (officers.Count > 0)
            facts.Add(new AcademyFact("The offices", string.Join(" ", officers.Select(x => x.h != null ? $"{x.o.Name}: {w.House(x.h.HouseId)?.Name}." : $"{x.o.Name}: vacant."))));
        var reachLaws = content.Policies.Laws.Where(l => l.Scope == "senate").Select(l => $"{l.Name}: {(PolicySystem.ReachOption(w, content.Policies, l) ?? l.DefaultOption).Name.ToLowerInvariant()}").ToList();
        if (reachLaws.Count > 0) facts.Add(new AcademyFact("The law of the Reach", string.Join("; ", reachLaws) + "."));
        facts.Add(new AcademyFact("War and peace", w.Wars.Count == 0 ? "No house is at war with another."
            : $"{w.Wars.Count} war{(w.Wars.Count == 1 ? "" : "s")} between houses: " + string.Join("; ", w.Wars.Take(4).Select(x => $"{w.House(x.HouseA)?.Name} against {w.House(x.HouseB)?.Name}")) + "."));
        var sour = w.Nations.Where(n => content.Nations.Def(n.Id) != null).OrderBy(n => n.Disposition).Take(3).ToList();
        if (sour.Count > 0)
            facts.Add(new AcademyFact("Beyond the Reach", string.Join(" ", sour.Select(n => { var d = content.Nations.Def(n.Id)!; return $"{d.Name} ({d.Temper}, {n.Strength} hulls) {(n.AtWar ? "is at war with the Reach" : n.Disposition <= -30 ? "is hostile" : n.Disposition <= -10 ? "is cold" : "is at peace with the Reach")}."; }))));

        int Hulls(string id) => w.Fleets.Values.Where(f => f.OwnerId == id).Sum(f => f.Strength);
        int Worlds(string id) => w.HoldingsOf(id).Count(x => x.IsPlanet);
        AcademyHouseLine HouseLine(House h)
        {
            var head = Economy.HeadOf(w, h); var party = FactionSystem.Of(w, h.Id);
            return new AcademyHouseLine(h.Id, h.Name, h.AccentColor, content.Titles.NameOf(h.Title, w.Era), head?.Name ?? "no one",
                head != null ? content.Careers.RankName(head.CareerTrack, head.CareerRank, w.Era) ?? "" : "", party?.Name ?? "no party", party?.Accent ?? "#888", senate.Weight(w, h.Id), Worlds(h.Id), Hulls(h.Id),
                head != null && h.Id != house.Id ? w.GetRelationship(head.Id, p.Id)?.Disposition ?? 0 : 0, chair?.Id == h.Id, h.Id == house.Id);
        }
        var great = w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId && h.Id != house.Id).OrderByDescending(h => senate.Weight(w, h.Id)).ThenByDescending(h => Progression.Title.RankOf(h.Title)).ThenBy(h => h.Id, StringComparer.Ordinal).Take(7).Select(HouseLine).ToList();
        var yours = new List<AcademyFact>
        {
            new("Rank", $"{content.Titles.NameOf(house.Title, w.Era)}, legitimacy {house.Legitimacy}. {senate.Weight(w, house.Id)} seat{(senate.Weight(w, house.Id) == 1 ? "" : "s")} in the Senate."),
            new("Domain", $"{Worlds(house.Id)} world{(Worlds(house.Id) == 1 ? "" : "s")}, {Hulls(house.Id)} hull{(Hulls(house.Id) == 1 ? "" : "s")}, {house.Treasury.Credits} credits banked."),
            new("Party", FactionSystem.Of(w, house.Id) is { } fp ? $"{fp.Name}. {fp.Description}" : "None."),
            new("What waits for you", "The house is yours when you pass out. Until then it keeps itself, and nothing you do here can reach it.")
        };

        var standards = a.IsImperium ? cat.Standards.Select(s => new AcademyStandardLine(s.Id, s.Name, s.Accent, s.Motto, a.StandardStrength.GetValueOrDefault(s.Id),
            a.StandardOf.Count(k => k.Value == s.Id && (w.Char(k.Key)?.Alive ?? false)), a.Fallen.Contains(s.Id), a.Standard == s.Id, a.Standard == s.Id && w.Counter(AcademySystem.CaptainKey) > 0)).OrderBy(s => s.Fallen).ThenByDescending(s => s.Strength).ToList() : new List<AcademyStandardLine>();

        string recommended = a.Active ? tc.Academy.RecommendedService(w) : p.CareerTrack;
        var services = AcademySystem.Tracks.Select(t => new AcademyServiceOption(t, content.Careers.ById(t)?.NameIn(w.Era) ?? t, content.Careers.RankName(t, 1, w.Era) ?? "",
            cat.Subjects.Where(s => s.Track == t).Sum(s => a.Marks.GetValueOrDefault(s.Id)), t == recommended, a.Service == t)).ToList();

        var instructor = w.Char(a.InstructorId); var ih = instructor != null ? w.House(instructor.HouseId) : null;
        var friends = a.Cohort.Select(id => w.Char(id)).Where(c => c is { Alive: true } && (w.GetRelationship(c.Id, p.Id) is { } r) && (r.Bond == BondType.Sworn || r.Disposition >= 25)).Select(c => $"{c!.Name} of {w.House(c.HouseId)?.Name}").ToList();
        var rivals = a.Cohort.Select(id => w.Char(id)).Where(c => c is { Alive: true } && (w.GetRelationship(c.Id, p.Id)?.Disposition ?? 0) <= -15).Select(c => $"{c!.Name} of {w.House(c.HouseId)?.Name}").ToList();
        int term = Math.Clamp(a.Term, 1, Math.Max(1, a.Terms));
        return new AcademyModel(reg.Name, a.Register, reg.Blurb, a.Term, a.Terms, term - 1 < reg.Terms.Count ? reg.Terms[term - 1] : $"Term {term}", w.Counter(AcademySystem.StandingKey), AcademySystem.ScoreOf(w),
            a.Graduated ? a.Place : AcademySystem.Place(w), a.Graduated ? a.Of : 1 + a.Cohort.Count(id => w.Char(id) is { Alive: true }), a.Tutorial,
            instructor?.Id, instructor?.Name, instructor != null ? $"{content.Careers.RankName(instructor.CareerTrack, instructor.CareerRank, w.Era) ?? "officer"} of {ih?.Name}" : null,
            subjects, cohort, lessons, facts, great, yours, standards, services, a.Active && a.Term >= a.Terms, a.Graduated,
            w.History.Where(h => h.Turn == 0).Select(h => h.Text).Reverse().Take(14).ToList(), friends, rivals,
            content.Careers.ById(p.CareerTrack)?.NameIn(w.Era) ?? p.CareerTrack, content.Careers.RankName(p.CareerTrack, p.CareerRank, w.Era) ?? "");
    }
}
