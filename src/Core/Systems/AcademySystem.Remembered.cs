using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>What the year is remembered for (docs/ACADEMY.md, "What is remembered"): how a cadet did is written on them for life, and the
/// better they did the more it counts. Winning the Institute as its captain counts most, then passing out first of the year, then standing
/// in the ranks that won, then honours. Classmates carry theirs too. It is read by renown, the first posting, promotion, elections, the
/// crews of a fleet, and what the year thinks of one another.</summary>
public sealed partial class AcademySystem
{
    public const string PrimusFlag = "primus", LostBannerFlag = "lost_the_banner", LostToYouFlag = "lost_the_banner_to_you", PassedLowFlag = "passed_out_low";
    public const string LaurelsKey = "academy_laurels", SecondFlag = "second_of_the_year";
    public const int PrimusLaurels = 3, WonLaurels = 1, FirstLaurels = 2, HonoursLaurels = 1, RenownPerLaurel = 3, PrimusRank = 2;
    public const int FollowedYou = 8, WonWithYou = 4, YouTookTheirBanner = -8, YoursTookTheirBanner = -4, BeatenToFirst = -4, RespectForFirst = 2;

    /// <summary>How well a person did in their year, 0 to 5: three for winning the Institute as its captain (one for standing in the ranks that
    /// won), and two for passing out first of the year (one for honours).</summary>
    public static int Laurels(Character? c) => c == null ? 0
        : (c.Flags.Contains(PrimusFlag) ? PrimusLaurels : c.Flags.Contains(WonInstituteFlag) ? WonLaurels : 0) + (c.Flags.Contains(FirstFlag) ? FirstLaurels : c.Flags.Contains(HonoursFlag) ? HonoursLaurels : 0);

    /// <summary>The same in words, for a screen: "" for a year that left no mark.</summary>
    public static string LaurelsName(Character? c)
    {
        if (c == null) return "";
        string war = c.Flags.Contains(PrimusFlag) ? "won the Institute as its captain" : c.Flags.Contains(WonInstituteFlag) ? "stood in the ranks that won the Institute" : "";
        string merit = c.Flags.Contains(FirstFlag) ? "first of the year" : c.Flags.Contains(HonoursFlag) ? "passed out with honours" : c.Flags.Contains(PassedLowFlag) ? "passed out near the bottom of the year" : "";
        return war.Length > 0 && merit.Length > 0 ? war + ", " + merit : war + merit;
    }

    /// <summary>A commander the crews have heard of before they met them: one who won the Institute as its captain.</summary>
    public static bool Primus(Character? c) => c != null && c.Flags.Contains(PrimusFlag);

    /// <summary>Passing out: write the year on everyone who was in it. The cadet's own laurels go into the record of their life; the classmates
    /// are placed in the same order of merit; the Standard that held the last banner is remembered on those who began under it, and its best
    /// cadet is its captain if the player was not; those whose banner was taken remember whose Standard took it; and the year's regard for
    /// the cadet moves by what they did to it and with it.</summary>
    private void Remember(World w)
    {
        var a = w.Academy!; var p = w.Protagonist!;
        bool won = w.WorldFlags.Contains(WonInstituteFlag), captain = won && w.Counter(CaptainKey) > 0;
        if (won) p.Flags.Add(WonInstituteFlag);
        if (captain) { p.Flags.Add(PrimusFlag); p.CareerRank = Math.Max(PrimusRank, p.CareerRank); }
        int third = Math.Max(1, a.Of / 3);
        if (a.Of >= 6 && a.Place > a.Of - third) p.Flags.Add(PassedLowFlag);
        string standard = _cat.Standard(a.Standard)?.Name ?? a.Standard, school = _cat.Register(a.Register).Name;
        if (captain) LifeSystem.Note(w, p, "primus", $"Won the Institute as captain of {standard}.");
        else if (won) LifeSystem.Note(w, p, "banner", $"Stood in the ranks of {standard}, which won the Institute.");
        LifeSystem.Note(w, p, p.Flags.Contains(FirstFlag) ? "laurel" : p.Flags.Contains(HonoursFlag) ? "honours" : "passed",
            $"Passed out of {school} {Ordinal(a.Place)} of {a.Of}{(p.Flags.Contains(FirstFlag) ? ", first of the year" : p.Flags.Contains(HonoursFlag) ? ", with honours" : "")}.");

        int mine = ScoreOf(w);
        var mates = a.Cohort.Select(id => w.Char(id)).Where(c => c is { Alive: true }).Select(c => c!).OrderByDescending(c => a.Scores.GetValueOrDefault(c.Id)).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();
        for (int i = 0; i < mates.Count; i++)
        {
            var c = mates[i]; int score = a.Scores.GetValueOrDefault(c.Id);
            int place = 1 + mates.Count(o => a.Scores.GetValueOrDefault(o.Id) > score) + (mine >= score ? 1 : 0);
            if (place == 1) c.Flags.Add(FirstFlag); else if (place == 2 && a.Place == 1) c.Flags.Add(SecondFlag);
            if (place <= third) c.Flags.Add(HonoursFlag);
            if (a.Of >= 6 && place > a.Of - third) c.Flags.Add(PassedLowFlag);
            if (a.Victor.Length > 0 && a.StandardOf.GetValueOrDefault(c.Id) == a.Victor && !c.Flags.Contains(LostBannerFlag)) c.Flags.Add(WonInstituteFlag);
        }
        // a Standard that won without the player at its head had a captain of its own: the best of those who began under it
        if (a.Victor.Length > 0 && !captain && mates.FirstOrDefault(c => c.Flags.Contains(WonInstituteFlag)) is { } first)
        { first.Flags.Add(PrimusFlag); first.CareerRank = Math.Max(PrimusRank, first.CareerRank); }

        foreach (var c in mates)
        {
            int d = 0;
            if (won && c.Flags.Contains(WonInstituteFlag)) d += captain ? FollowedYou : WonWithYou;
            if (c.Flags.Contains(LostToYouFlag)) d += captain ? YouTookTheirBanner : YoursTookTheirBanner;
            if (p.Flags.Contains(FirstFlag)) d += c.Flags.Contains(SecondFlag) ? BeatenToFirst : RespectForFirst;
            if (d == 0) continue;
            var rel = w.Relationship(c.Id, p.Id); rel.Disposition = Math.Clamp(rel.Disposition + d, -100, 100);
        }
        w.WorldCounters[LaurelsKey] = Laurels(p);
        if (captain) w.Log($"The year will say for the rest of its life that it was {p.Name}'s. {p.Name} is commissioned a rung above the rest of it.");
    }
}
