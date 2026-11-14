using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The household (docs/HOUSEHOLD.md): people taken into a house they were not born to, a mentor's wing, and the duties a
/// head may hand to any member of the house.</summary>
public class HouseholdTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static (TurnController tc, World w, Character me, House mine) Start(string scenario = "the_old_lion", int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content); tc.BeginTurn();
        return (tc, w, w.Protagonist!, w.House(w.Protagonist!.HouseId)!);
    }
    private static bool Do(World w, ICommand cmd) => new CommandExecutor().TryExecute(cmd, new CommandContext(w, TestContent.Rng()));
    private static void Year(TurnController tc) { tc.EndTurn(); tc.BeginTurn(); }
    private static House Other(World w, House mine, int skip = 0) => w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId).OrderBy(h => h.Legitimacy).ThenBy(h => h.Id, StringComparer.Ordinal).Skip(skip).First();
    private static Character Person(World w, House h, string id, int age, string sex = "male", int regardFor = 40, Character? of = null)
    {
        var c = new Character { Id = id, Name = "Tomas Outsider", HouseId = h.Id, Age = age, Sex = sex, CareerTrack = "stewardship", CareerRank = 0 };
        w.Characters[id] = c; h.Members.Add(id); if (of != null) w.Relationship(id, of.Id).Disposition = regardFor; return c;
    }

    [Fact]
    public void Someone_willing_is_taken_in_keeps_their_name_and_is_of_the_house()
    {
        var (_, w, me, mine) = Start(); var theirs = Other(w, mine); var p = Person(w, theirs, "outsider", 24, of: me);
        var (score, why) = HouseholdSystem.Willing(w, me, p, HouseholdSystem.Protege); Assert.True(score >= HouseholdSystem.WillingAt, string.Join(", ", why));
        Assert.NotNull(HouseholdSystem.TakeInRefusal(w, me, p, HouseholdSystem.Ward));   // not a child
        Assert.True(Do(w, new HouseholdCommand(me.Id, HouseholdCommand.TakeIn, p.Id, HouseholdSystem.Protege)));
        Assert.Equal(mine.Id, p.HouseId); Assert.Equal(theirs.Id, p.BornHouseId); Assert.Equal("Tomas Outsider", p.Name); Assert.Contains(p.Id, mine.Members); Assert.DoesNotContain(p.Id, theirs.Members);
        Assert.True(HouseholdSystem.IsFound(p)); Assert.Equal(HouseholdSystem.Protege, HouseholdSystem.KindOf(p)); Assert.Equal(me.Id, p.MentorId);
        Assert.Equal(BondType.Sworn, w.GetRelationship(p.Id, me.Id)!.Bond); Assert.True(w.HasFlag(HouseholdSystem.TookInFlag));
        Assert.Contains(p, HouseholdSystem.Eligible(w, mine));   // and may be given the house's work like anyone born to it

        // the head of a house with others in it does not leave it; someone who thinks nothing of you does not come
        var crowded = w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId && w.LivingCharacters().Count(c => c.HouseId == h.Id) >= 2).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        var head = Economy.HeadOf(w, crowded)!; w.Relationship(head.Id, me.Id).Disposition = 80;
        Assert.Contains("will not come", HouseholdSystem.TakeInRefusal(w, me, head, HouseholdSystem.Companion));
        var cold = Person(w, Other(w, mine, 2), "cold", 30, regardFor: -60, of: me); cold.Name = "Cold Stranger";
        if (HouseholdSystem.Willing(w, me, cold, HouseholdSystem.Companion).score < HouseholdSystem.WillingAt) Assert.False(Do(w, new HouseholdCommand(me.Id, HouseholdCommand.TakeIn, cold.Id, HouseholdSystem.Companion)));
    }

    [Fact]
    public void A_house_keeps_only_so_many_who_are_not_of_its_blood()
    {
        var (_, w, me, mine) = Start(); int room = HouseholdSystem.Room(mine); var theirs = Other(w, mine);
        for (int i = 0; i < room; i++) { var p = Person(w, theirs, "o" + i, 22 + i, of: me, regardFor: 90); HouseholdSystem.TakeIn(w, me, p, HouseholdSystem.Companion); }
        var extra = Person(w, theirs, "extra", 30, of: me, regardFor: 90);
        Assert.Contains("cannot keep more", HouseholdSystem.TakeInRefusal(w, me, extra, HouseholdSystem.Companion));
    }

    [Fact]
    public void Found_family_stands_after_the_blood_and_an_adopted_heir_stands_as_a_child()
    {
        var (_, w, me, mine) = Start(); var theirs = Other(w, mine); var p = Person(w, theirs, "outsider", 40, of: me, regardFor: 60);
        HouseholdSystem.TakeIn(w, me, p, HouseholdSystem.Companion);
        var heir = DynastySystem.FindHeir(w, me); Assert.NotNull(heir); Assert.False(HouseholdSystem.IsFound(heir!));   // older than every child, and still not first
        me.Age = Math.Max(me.Age, 55);
        Assert.True(Do(w, new HouseholdCommand(me.Id, HouseholdCommand.Adopt, p.Id)), HouseholdSystem.AdoptRefusal(w, me, p));
        Assert.Equal(me.Id, p.AdoptedById); Assert.False(HouseholdSystem.IsFound(p)); Assert.EndsWith(mine.Name.Replace("House ", ""), p.Name); Assert.StartsWith("Tomas", p.Name);
        Assert.Equal(BondType.Blood, w.GetRelationship(p.Id, me.Id)!.Bond); Assert.True(w.HasFlag(HouseholdSystem.AdoptedFlag));
        Assert.Equal(p.Id, DynastySystem.FindHeir(w, me)!.Id);   // the eldest child now, by primogeniture

        // with nobody of the blood left, the household inherits
        var (_, w2, me2, mine2) = Start(); var p2 = Person(w2, Other(w2, mine2), "outsider", 30, of: me2, regardFor: 60); HouseholdSystem.TakeIn(w2, me2, p2, HouseholdSystem.Companion);
        foreach (var k in w2.LivingCharacters().Where(c => c.HouseId == mine2.Id && c.Id != me2.Id && c.Id != p2.Id).ToList()) k.Alive = false;
        Assert.Equal(p2.Id, DynastySystem.FindHeir(w2, me2)!.Id);
    }

    [Fact]
    public void A_ward_of_age_chooses_to_stay_or_to_go_home()
    {
        var (tc, w, me, mine) = Start(); var theirs = Other(w, mine); var theirHead = Economy.HeadOf(w, theirs)!; w.Relationship(theirHead.Id, me.Id).Disposition = 60;
        var fond = Person(w, theirs, "fond_ward", 14); var cool = Person(w, theirs, "cool_ward", 14); cool.Name = "Cool Ward";
        Assert.Null(HouseholdSystem.TakeInRefusal(w, me, fond, HouseholdSystem.Ward));   // their family decides for a child
        HouseholdSystem.TakeIn(w, me, fond, HouseholdSystem.Ward); HouseholdSystem.TakeIn(w, me, cool, HouseholdSystem.Ward);
        for (int i = 0; i < 4; i++) { w.Relationship(fond.Id, me.Id).Disposition = 60; w.Relationship(cool.Id, me.Id).Disposition = -5; Year(tc); }
        Assert.Equal(mine.Id, fond.HouseId); Assert.Equal(HouseholdSystem.Companion, HouseholdSystem.KindOf(fond));
        Assert.Equal(theirs.Id, cool.HouseId); Assert.Equal("", cool.BornHouseId); Assert.False(HouseholdSystem.IsFound(cool));
    }

    [Fact]
    public void A_mentor_teaches_what_they_are_best_at_until_the_pupil_catches_up()
    {
        var (tc, w, me, mine) = Start("the_new_man"); var theirs = Other(w, mine, 3);
        var senior = Person(w, theirs, "senior", me.Age + 20, of: me); senior.Name = "Old Senior"; senior.CareerTrack = me.CareerTrack; senior.CareerRank = me.CareerRank + 2; senior.Skills["oratory"] = 5;
        w.Relationship(senior.Id, me.Id).Disposition = 5; Assert.Contains("do not think enough", new MentorCommand(me.Id, senior.Id).Refusal(new CommandContext(w, TestContent.Rng())));
        w.Relationship(senior.Id, me.Id).Disposition = 30; int before = me.Skill("oratory");
        Assert.True(Do(w, new MentorCommand(me.Id, senior.Id))); Assert.Equal(senior.Id, me.MentorId);
        for (int i = 0; i <= HouseholdSystem.MentorEvery; i++) { w.Relationship(senior.Id, me.Id).Disposition = 30; Year(tc); }
        Assert.Equal(before + 1, me.Skill("oratory")); Assert.True(w.HasFlag(HouseholdSystem.HasMentorFlag));
        w.Relationship(senior.Id, me.Id).Disposition = -40; tc.EndTurn(); Assert.Equal("", me.MentorId); Assert.True(w.HasFlag(HouseholdSystem.MentorLostFlag));
        var junior = Person(w, theirs, "junior", me.Age + 20, of: me); junior.CareerTrack = me.CareerTrack; junior.CareerRank = 0;
        Assert.NotNull(HouseholdSystem.MentorRefusal(w, me, junior));
    }

    [Fact]
    public void A_steward_does_the_routine_above_the_reserve_and_learns_the_work()
    {
        var (tc, w, me, mine) = Start(); var s = Person(w, mine, "steward_kin", 30); s.Name = "Able Steward"; s.Skills["administration"] = 3; w.Relationship(s.Id, me.Id).Disposition = 40;
        mine.Treasury.Credits = 400; mine.Treasury.Materials = 200; int built = w.HoldingsOf(mine.Id).Sum(h => h.Buildings.Count) + w.Projects.Count(p => p.OwnerId == mine.Id);
        Assert.True(Do(w, new DelegateCommand(me.Id, HouseholdSystem.Steward, s.Id, 50)));
        tc.EndTurn(); var d = mine.Duty(HouseholdSystem.Steward)!;
        Assert.True(w.HoldingsOf(mine.Id).Sum(h => h.Buildings.Count) + w.Projects.Count(p => p.OwnerId == mine.Id) > built, d.LastReport); Assert.Contains("egan", d.LastReport);
        Assert.True(mine.Treasury.Credits >= 0);
        tc.BeginTurn(); mine.Treasury.Credits = 30; d.Reserve = 1000; int now = w.Projects.Count(p => p.OwnerId == mine.Id); tc.EndTurn();
        Assert.True(w.Projects.Count(p => p.OwnerId == mine.Id) <= now, "nothing new is begun below the reserve"); Assert.Contains("reserve", d.LastReport);
        // taken back, the work stops
        tc.BeginTurn(); Assert.True(Do(w, new DelegateCommand(me.Id, HouseholdSystem.Steward, ""))); Assert.Null(mine.Duty(HouseholdSystem.Steward));
    }

    [Fact]
    public void A_greedy_steward_who_owes_the_head_nothing_helps_themselves_and_is_found_out()
    {
        var (tc, w, me, mine) = Start(); var s = Person(w, mine, "greedy_kin", 30); s.AptitudeTraits.Add("Greedy"); mine.Treasury.Credits = 100;
        HouseholdSystem.Assign(w, me, HouseholdSystem.Steward, s, 100000); var d = mine.Duty(HouseholdSystem.Steward)!;
        for (int i = 0; i < 3; i++) { w.Relationship(s.Id, me.Id).Disposition = 0; mine.Treasury.Credits = Math.Max(mine.Treasury.Credits, 50); tc.EndTurn(); tc.BeginTurn(); }
        Assert.Equal(9, d.Skimmed); Assert.Contains(HouseholdSystem.SkimmingChar, s.Flags);
        // an honourable one does not, whatever their appetite
        var (tc2, w2, me2, mine2) = Start(); var honest = Person(w2, mine2, "honest_kin", 30); honest.AptitudeTraits.Add("Greedy"); honest.NatureTraits.Add("Honorable"); mine2.Treasury.Credits = 100;
        HouseholdSystem.Assign(w2, me2, HouseholdSystem.Steward, honest, 100000);
        for (int i = 0; i < 3; i++) { w2.Relationship(honest.Id, me2.Id).Disposition = 0; tc2.EndTurn(); tc2.BeginTurn(); }
        Assert.Equal(0, mine2.Duty(HouseholdSystem.Steward)!.Skimmed);
    }

    [Fact]
    public void A_marshal_keeps_the_fleets_fuelled_and_a_duty_lapses_with_its_holder()
    {
        var (tc, w, me, mine) = Start(); var m = Person(w, mine, "marshal_kin", 35); m.Name = "Able Marshal"; m.Skills["logistics"] = 2; w.Relationship(m.Id, me.Id).Disposition = 40;
        Assert.True(w.Fleets.Values.Any(f => f.OwnerId == mine.Id)); mine.Treasury.Helium3 = 0; mine.Treasury.Credits = 500;
        Assert.True(Do(w, new DelegateCommand(me.Id, HouseholdSystem.Marshal, m.Id, 20)));
        tc.EndTurn(); Assert.Contains("fuel", mine.Duty(HouseholdSystem.Marshal)!.LastReport); Assert.True(w.HasFlag(HouseholdSystem.DelegatedFlag));
        tc.BeginTurn(); m.Alive = false; tc.EndTurn(); Assert.Null(mine.Duty(HouseholdSystem.Marshal));
        // one who inherits the seat lays the duty down: the head does not hold a post under themselves
        var heirKin = Person(w, mine, "heir_kin", 35); w.Relationship(heirKin.Id, w.ProtagonistId).Disposition = 40; tc.BeginTurn();
        HouseholdSystem.Assign(w, w.Protagonist!, HouseholdSystem.Marshal, heirKin, 0); w.ProtagonistId = heirKin.Id; tc.EndTurn(); Assert.Null(mine.Duty(HouseholdSystem.Marshal)); w.ProtagonistId = me.Id;
        var sulky = Person(w, mine, "sulky_kin", 35); w.Relationship(sulky.Id, me.Id).Disposition = -50;
        Assert.Equal("they will not serve you", HouseholdSystem.AssignRefusal(w, me, HouseholdSystem.Marshal, sulky));
    }

    [Fact]
    public void People_come_to_the_door_and_the_head_answers()
    {
        var (_, w, me, mine) = Start(); var p = Person(w, Other(w, mine), "asker", 25, of: me, regardFor: 80);
        mine.Petitions.Add(new Petition { PersonId = p.Id, As = HouseholdSystem.Companion, Since = w.Turn });
        Assert.True(Do(w, new HouseholdCommand(me.Id, HouseholdCommand.Decline, p.Id))); Assert.Empty(mine.Petitions);
        mine.Petitions.Add(new Petition { PersonId = p.Id, As = HouseholdSystem.Companion, Since = w.Turn });
        Assert.True(Do(w, new HouseholdCommand(me.Id, HouseholdCommand.TakeIn, p.Id, HouseholdSystem.Companion))); Assert.Empty(mine.Petitions); Assert.Equal(mine.Id, p.HouseId);
        // and over the years somebody does come without being sought
        bool came = false;
        for (int seed = 1; seed <= 3 && !came; seed++)
        {
            var (tc2, w2, _, mine2) = Start("the_old_lion", seed);
            for (int i = 0; i < 40 && !came && !w2.IsDynastyDead; i++) { Year(tc2); came = w2.House(w2.Protagonist!.HouseId)!.Petitions.Count > 0; }
            _ = mine2;
        }
        Assert.True(came);
    }

    [Fact]
    public void Those_taken_in_can_be_sent_home_and_their_children_keep_their_name()
    {
        var (tc, w, me, mine) = Start(); var theirs = w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId).OrderByDescending(h => h.Wealth).ThenBy(h => h.Id, StringComparer.Ordinal).First();
        var p = Person(w, theirs, "outsider", 26, of: me, regardFor: 70); HouseholdSystem.TakeIn(w, me, p, HouseholdSystem.Companion);
        var wife = w.LivingCharacters().First(c => c.HouseId != mine.Id && c.Sex == "female" && c.Age is >= 18 and <= 30 && DynastySystem.Spouse(w, c) == null && CourtshipSystem.Bar(w, p, c) == null);
        CourtshipSystem.Wed(w, p, wife, CourtshipSystem.Love, childrenTo: mine.Id); w.MarriageOf(p.Id)!.Warmth = 100;
        Character? child = null; for (int i = 0; i < 25 && child == null; i++) { Year(tc); w.MarriageOf(p.Id)!.Warmth = 100; child = w.Characters.Values.FirstOrDefault(c => c.FatherId == p.Id); if (!p.Alive || !wife.Alive) break; }
        if (child != null) { Assert.EndsWith("Outsider", child.Name); Assert.True(HouseholdSystem.IsFound(child)); Assert.Equal(mine.Id, child.HouseId); }
        if (p.Alive && p.HouseId == mine.Id && w.House(theirs.Id) != null && w.Protagonist!.Id == me.Id) { Assert.True(Do(w, new HouseholdCommand(me.Id, HouseholdCommand.Release, p.Id))); Assert.Equal(theirs.Id, p.HouseId); Assert.False(HouseholdSystem.IsFound(p)); }
    }
}
