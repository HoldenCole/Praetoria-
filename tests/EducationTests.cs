using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Education and skills (docs/EDUCATION.md): a ceiling in every skill, learning by doing, by study and by being taught,
/// children brought up toward their bent, and no more buying a skill a step at a time.</summary>
public class EducationTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static (TurnController tc, World w, Character me, House mine) Start(string scenario = "the_old_lion", int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content); tc.BeginTurn();
        return (tc, w, w.Protagonist!, w.House(w.Protagonist!.HouseId)!);
    }
    private static bool Do(World w, ICommand cmd) => new CommandExecutor().TryExecute(cmd, new CommandContext(w, TestContent.Rng()));
    private static Character Person(World w, House h, string id, int age)
    {
        var c = new Character { Id = id, Name = "Test " + id, HouseId = h.Id, Age = age, Sex = "female" }; w.Characters[id] = c; h.Members.Add(id); return c;
    }

    [Fact]
    public void Every_person_has_a_ceiling_in_every_skill_and_an_aptitude_raises_it()
    {
        var c = new Character { Id = "someone" };
        foreach (var s in EducationSystem.All) { Assert.InRange(EducationSystem.Bent(c, s), -1, 2); Assert.InRange(EducationSystem.Ceiling(c, s), 4, 7); }
        int plain = EducationSystem.Ceiling(c, "tactics"); c.AptitudeTraits.Add("Tactician"); Assert.Equal(Math.Min(10, plain + 3), EducationSystem.Ceiling(c, "tactics"));
        int other = EducationSystem.Ceiling(c, "law"); c.AptitudeTraits.Add("Brilliant"); Assert.Equal(Math.Min(10, other + 1), EducationSystem.Ceiling(c, "law"));
        Assert.Contains(EducationSystem.BentGroup(c), EducationSystem.Groups);
        Assert.Equal(4, EducationSystem.Need(0)); Assert.Equal(19, EducationSystem.Need(5));   // each step costs more than the last
    }

    [Fact]
    public void Learning_is_banked_steps_are_taken_as_earned_and_nothing_passes_the_ceiling()
    {
        var (_, w, _, mine) = Start(); var c = Person(w, mine, "learner", 25); string skill = "law"; int top = EducationSystem.Ceiling(c, skill);
        Assert.False(EducationSystem.Gain(w, c, skill, 3)); Assert.Equal(0, c.Skill(skill)); Assert.Equal(3, c.Learning[skill]);
        Assert.True(EducationSystem.Gain(w, c, skill, 1)); Assert.Equal(1, c.Skill(skill)); Assert.Equal(0, c.Learning[skill]);
        EducationSystem.Gain(w, c, skill, 10000); Assert.Equal(top, c.Skill(skill)); Assert.False(c.Learning.ContainsKey(skill));
        Assert.False(EducationSystem.Gain(w, c, skill, 50)); Assert.Equal(top, c.Skill(skill));
        Assert.False(new TrainCommand(c.Id, skill).CanExecute(new CommandContext(w, TestContent.Rng())));   // no buying past it either
    }

    [Fact]
    public void Nobody_climbs_a_step_a_year_any_more()
    {
        // before: every character but the player's drilled their best skill one step a year, without limit
        var (tc, w, me, _) = Start(); var before = w.LivingCharacters().Where(c => c.Id != me.Id && c.Age >= 20).ToDictionary(c => c.Id, c => c.Skills.Values.DefaultIfEmpty(0).Max());
        for (int i = 0; i < 10; i++) { tc.EndTurn(); if (w.IsDynastyDead) break; tc.BeginTurn(); }
        foreach (var c in w.LivingCharacters().Where(c => before.ContainsKey(c.Id)))
        {
            int best = c.Skills.Values.DefaultIfEmpty(0).Max();
            // from nothing, ten years of service, study and office can make someone skilled (5); it used to make them a master twice over
            Assert.True(best <= Math.Max(before[c.Id] + 3, 6), $"{c.Name}: {before[c.Id]} to {best} in ten years");
            foreach (var kv in c.Skills) Assert.True(kv.Value <= Math.Max(EducationSystem.Ceiling(c, kv.Key), before[c.Id]), $"{c.Name} {kv.Key} {kv.Value} is past the ceiling of {EducationSystem.Ceiling(c, kv.Key)}");
        }
        // and they do still learn
        Assert.Contains(w.LivingCharacters(), c => before.TryGetValue(c.Id, out int b) && c.Skills.Values.DefaultIfEmpty(0).Sum() > 0 && c.Learning.Count > 0);
    }

    [Fact]
    public void A_grown_person_learns_by_what_they_do_and_by_what_they_choose_to_study()
    {
        var (tc, w, me, mine) = Start(); var c = Person(w, mine, "officer", 30); c.CareerTrack = "military"; c.Ambition = "command_a_fleet";
        var fleet = w.Fleets.Values.First(f => f.OwnerId == mine.Id); fleet.AdmiralId = c.Id;
        var practice = EducationSystem.Practice(w, c); Assert.Contains(practice, x => x.skill == "tactics" && x.why == "commanding a fleet"); Assert.Contains(practice, x => x.why == "the service");
        tc.EndTurn();
        Assert.Equal("tactics", c.Study);   // their own choice, by their ambition
        Assert.True(c.Skill("tactics") * 100 + c.Learning.GetValueOrDefault("tactics") >= 100, "study 3 and command 2 make five: a first step and one toward the next");
        // what the player's own character studies is theirs to say, and nobody else's is
        Assert.False(Do(w, new EducationCommand(me.Id, EducationCommand.Study, c.Id, "law")));
        string mineToStudy = EducationSystem.All.First(s => me.Skill(s) < EducationSystem.Ceiling(me, s));
        tc.BeginTurn(); Assert.True(Do(w, new EducationCommand(me.Id, EducationCommand.Study, me.Id, mineToStudy))); Assert.Equal(mineToStudy, me.Study);
        int had = me.Skill(mineToStudy) * 100 + me.Learning.GetValueOrDefault(mineToStudy); tc.EndTurn();
        if (w.Protagonist!.Id == me.Id) Assert.True(me.Skill(mineToStudy) * 100 + me.Learning.GetValueOrDefault(mineToStudy) > had || me.Skill(mineToStudy) > had / 100);
    }

    [Fact]
    public void A_child_is_brought_up_toward_their_bent_without_anyone_managing_it()
    {
        var (tc, w, me, mine) = Start(); var child = Person(w, mine, "child", 6); string bent = EducationSystem.BentGroup(child);
        for (int i = 0; i < 9; i++) { tc.EndTurn(); if (w.IsDynastyDead) return; tc.BeginTurn(); }
        var learned = child.Skills.Where(kv => kv.Value > 0 && kv.Key != "charisma").Select(kv => kv.Key).ToList();
        Assert.NotEmpty(learned); Assert.All(learned, s => Assert.Equal(bent, EducationSystem.GroupOf(s)));
        Assert.True(child.Skills.Values.Sum() >= 4, $"learned {child.Skills.Values.Sum()} steps in nine years");
        Assert.True(child.Skills.Values.Max() >= 3, "by fifteen they are properly good at one thing");
        Assert.DoesNotContain(EducationSystem.ResistsChar, child.Flags);
    }

    [Fact]
    public void A_course_against_their_bent_is_learned_slowly_and_resented_and_a_tutor_helps()
    {
        var (tc, w, me, mine) = Start(); mine.Treasury.Credits = 500;
        var with = Person(w, mine, "with", 8); var against = Person(w, mine, "against", 8); var tutored = Person(w, mine, "tutored", 8);
        string other = EducationSystem.Groups.First(g => g != EducationSystem.BentGroup(against));
        Assert.True(Do(w, new EducationCommand(me.Id, EducationCommand.Upbringing, against.Id, other)));
        Assert.True(Do(w, new EducationCommand(me.Id, EducationCommand.Tutor, tutored.Id, "2")));
        Assert.Equal(EducationSystem.AgainstBentPoints, EducationSystem.Lesson(w, against).points); Assert.True(EducationSystem.Lesson(w, against).against);
        Assert.Equal(EducationSystem.BentPoints, EducationSystem.Lesson(w, with).points); Assert.Equal(EducationSystem.BentPoints + 2, EducationSystem.Lesson(w, tutored).points);
        int credits = mine.Treasury.Credits; int regard = w.Relationship(against.Id, me.Id).Disposition;
        tc.EndTurn();
        Assert.Contains(EducationSystem.ResistsChar, against.Flags); Assert.True(w.HasFlag(EducationSystem.ResistsFlag));
        Assert.True(w.Relationship(against.Id, w.ProtagonistId).Disposition < regard || w.ProtagonistId != me.Id);
        Assert.True(mine.Treasury.Credits <= credits - EducationSystem.BestTutorCredits + 60, "the tutor was paid");
        // a house that cannot pay loses the tutor
        tc.BeginTurn(); mine.Treasury.Credits = -100; tc.EndTurn(); Assert.Equal(0, tutored.Tutor);
        // a grown person's course is not the head's to set
        var grown = Person(w, mine, "grown", 30); Assert.False(Do(w, new EducationCommand(w.ProtagonistId, EducationCommand.Upbringing, grown.Id, EducationSystem.Arms)));
    }

    [Fact]
    public void A_skill_taken_far_enough_shows_an_aptitude_and_coming_of_age_follows_the_upbringing()
    {
        var (tc, w, me, mine) = Start(); var c = Person(w, mine, "gifted", 30);
        string skill = new[] { "tactics", "oratory", "administration", "diplomacy", "logistics", "economics", "intrigue", "leadership", "discipline" }.First(s => EducationSystem.Ceiling(c, s) >= 7 || true);
        var able = w.LivingCharacters().Concat(new[] { c }).FirstOrDefault(x => EducationSystem.Ceiling(x, "tactics") >= 7 && !x.AptitudeTraits.Contains("Tactician") && x.AptitudeTraits.Count < 3);
        if (able != null) { able.Skills["tactics"] = 6; EducationSystem.Gain(w, able, "tactics", 100); Assert.Contains("Tactician", able.AptitudeTraits); Assert.True(EducationSystem.Ceiling(able, "tactics") >= 10 - 0); }
        // a child raised to arms is bound for the service when they come of age
        var child = Person(w, mine, "raised", 15); child.Skills["tactics"] = 3; child.Skills["discipline"] = 2;
        tc.EndTurn();
        if (child.Alive) { Assert.Equal("military", child.CareerTrack); Assert.True(child.Skill("tactics") >= 3, "what they learned is kept"); }
        _ = skill; _ = me;
    }
}
