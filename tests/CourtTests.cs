using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The house's own court (docs/COURT.md): seven small positions that go with the house whatever it holds, each held by one
/// of the commons sworn to it or by one of the family.</summary>
public class CourtTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static (TurnController tc, World w, House house, Character me, CommandContext ctx) Start(ulong seed = 5)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), seed); var tc = new TurnController(w, Content);
        var me = w.Protagonist!; return (tc, w, w.House(me.HouseId)!, me, new CommandContext(w, WorldBuilder.RngFor(w)));
    }
    private static void Year(TurnController tc) { foreach (var i in tc.BeginTurn()) { var ch = tc.Offer(i).FirstOrDefault(x => x.Available); if (ch != null) tc.Resolve(i, ch.Choice.Id); } tc.EndTurn(); }
    private static Retainer Hire(TurnController tc, World w, House house, Character me, CommandContext ctx, string role, int pick = 0)
    {
        var offer = CourtSystem.Offers(w, Content.Names, house, role)[pick]; var cmd = new RetainerCommand(me.Id, RetainerCommand.Hire, role, offer.Id, Content.Names);
        Assert.True(tc.Executor.TryExecute(cmd, ctx), cmd.Refusal(ctx)); return house.Position(role)!;
    }
    private static Character Relative(World w, House house, Character me, string id = "court_kin", string sex = "female", int age = 30)
    {
        var k = new Character { Id = id, Name = "Livia Test", HouseId = house.Id, Age = age, Sex = sex, CareerTrack = "law" }; w.Characters[k.Id] = k; house.Members.Add(k.Id); w.Pools[k.Id] = ActionPools.ForNpc();
        w.Relationship(k.Id, me.Id).Disposition = 40; return k;
    }

    [Fact]
    public void There_are_seven_positions_and_three_of_the_commons_offer_for_each()
    {
        var (_, w, house, _, _) = Start(); Assert.Equal(7, CourtSystem.Positions.Length); Assert.Empty(house.Court);
        foreach (var role in CourtSystem.Positions)
        {
            Assert.NotEqual(role, CourtSystem.Name(role)); Assert.True(CourtSystem.Does(role).Length > 40);
            var offers = CourtSystem.Offers(w, Content.Names, house, role); Assert.Equal(CourtSystem.OffersEach, offers.Count); Assert.Equal(offers.Count, offers.Select(o => o.Id).Distinct().Count());
            Assert.All(offers, o => { Assert.InRange(o.Skill, 1, 4); Assert.Equal(CourtSystem.WageOf(o.Skill), o.Wage); Assert.Contains(" of ", o.Name); Assert.True(o.From.Length > 10); Assert.DoesNotContain("{0}", o.From); Assert.Equal("", o.KinId); });
            // the same people offer until the two years are out, whoever asks and however often
            Assert.Equal(offers.Select(o => o.Id + o.Name + o.Skill), CourtSystem.Offers(w, Content.Names, house, role).Select(o => o.Id + o.Name + o.Skill));
            Assert.Equal(offers.Count, offers.Select(o => o.From).Distinct().Count());   // three people, not one story told three times
        }
        Assert.All(CourtSystem.Offers(w, Content.Names, house, CourtSystem.Nurse), o => { Assert.Equal("female", o.Sex); Assert.InRange(o.Age, 20, 34); });
        // a later year, other people
        var before = CourtSystem.Offers(w, Content.Names, house, CourtSystem.Guard).Select(o => o.Id).ToList(); w.Turn += CourtSystem.OffersChangeEvery;
        Assert.Empty(before.Intersect(CourtSystem.Offers(w, Content.Names, house, CourtSystem.Guard).Select(o => o.Id)));
    }

    [Fact]
    public void One_of_the_commons_is_sworn_paid_by_the_year_and_grows_loyal_and_better()
    {
        var (tc, w, house, me, ctx) = Start(); house.Treasury.Credits = 500;
        var r = Hire(tc, w, house, me, ctx, CourtSystem.Physician); int skill = r.Skill, loyalty = r.Loyalty;
        Assert.Contains("filled", new RetainerCommand(me.Id, RetainerCommand.Hire, CourtSystem.Physician, r.Id, Content.Names).Refusal(ctx));
        Assert.Contains("nobody of that name", new RetainerCommand(me.Id, RetainerCommand.Hire, CourtSystem.Guard, "ret_nobody", Content.Names).Refusal(ctx));
        Assert.Equal(r.Wage, CourtSystem.Wages(w, house)); Assert.Equal(skill, CourtSystem.Skill(w, house.Id, CourtSystem.Physician));
        int age = r.Age; Year(tc); Assert.Same(r, house.Position(CourtSystem.Physician)); Assert.Equal(age + 1, r.Age); Assert.True(r.Loyalty > loyalty); Assert.Equal(0, r.UnpaidYears); Assert.True(w.HasFlag(CourtSystem.HasCourtFlag));
        for (int i = 1; i < CourtSystem.SkillEvery; i++) { house.Treasury.Credits = 500; Year(tc); }
        Assert.Equal(Math.Min(CourtSystem.MaxSkill, skill + 1), house.Position(CourtSystem.Physician)!.Skill);   // six years in the place
    }

    [Fact]
    public void Unpaid_for_two_years_one_who_is_not_yet_the_houses_own_goes()
    {
        var (_, w, house, me, _) = Start(); house.Treasury.Credits = 500;
        CourtSystem.Hire(w, Content.Names, me, CourtSystem.Guard, CourtSystem.Offers(w, Content.Names, house, CourtSystem.Guard)[0].Id); var r = house.Position(CourtSystem.Guard)!;
        house.Treasury.Credits = 0; CourtSystem.Tick(w, Content.Policies); Assert.Equal(1, r.UnpaidYears); Assert.Equal(40 + CourtSystem.UnpaidLoyalty, r.Loyalty); Assert.NotNull(house.Position(CourtSystem.Guard));
        w.Turn++; house.Treasury.Credits = 0; CourtSystem.Tick(w, Content.Policies);
        Assert.Null(house.Position(CourtSystem.Guard)); Assert.True(w.HasFlag(CourtSystem.LeftFlag)); Assert.Equal(0, CourtSystem.Skill(w, house.Id, CourtSystem.Guard));

        // one who has been the house's for long stays through the same two years
        var (_, w2, house2, me2, _) = Start(); house2.Treasury.Credits = 500;
        CourtSystem.Hire(w2, Content.Names, me2, CourtSystem.Guard, CourtSystem.Offers(w2, Content.Names, house2, CourtSystem.Guard)[0].Id); var old = house2.Position(CourtSystem.Guard)!; old.Loyalty = 100;
        for (int i = 0; i < 2; i++) { house2.Treasury.Credits = 0; CourtSystem.Tick(w2, Content.Policies); w2.Turn++; }
        Assert.Same(old, house2.Position(CourtSystem.Guard)); Assert.Equal(2, old.UnpaidYears);
    }

    [Fact]
    public void One_of_the_family_holds_a_place_for_nothing_and_not_two_at_once()
    {
        var (tc, w, house, me, ctx) = Start(); var kin = Relative(w, house, me); kin.Skills["charisma"] = 4; int wagesBefore = CourtSystem.Wages(w, house);
        var cmd = new RetainerCommand(me.Id, RetainerCommand.Appoint, CourtSystem.Ceremonies, kin.Id, Content.Names); Assert.True(tc.Executor.TryExecute(cmd, ctx), cmd.Refusal(ctx));
        var r = house.Position(CourtSystem.Ceremonies)!; Assert.Equal(kin.Id, r.KinId); Assert.Equal(4, CourtSystem.Skill(w, house.Id, CourtSystem.Ceremonies)); Assert.Equal(wagesBefore, CourtSystem.Wages(w, house));
        Assert.Contains("already", new RetainerCommand(me.Id, RetainerCommand.Appoint, CourtSystem.Tutor, kin.Id, Content.Names).Refusal(ctx));
        Assert.Contains("not someone of your house", new RetainerCommand(me.Id, RetainerCommand.Appoint, CourtSystem.Tutor, me.Id, Content.Names).Refusal(ctx));
        var child = Relative(w, house, me, "court_child", "male", 9); Assert.Contains("not of age", new RetainerCommand(me.Id, RetainerCommand.Appoint, CourtSystem.Tutor, child.Id, Content.Names).Refusal(ctx));
        var brother = Relative(w, house, me, "court_brother", "male", 30); Assert.Contains("wet nurse", new RetainerCommand(me.Id, RetainerCommand.Appoint, CourtSystem.Nurse, brother.Id, Content.Names).Refusal(ctx));
        // somebody of the council is not also of the court
        Assert.True(tc.Executor.TryExecute(new DelegateCommand(me.Id, HouseholdSystem.Advocate, brother.Id, 0), ctx), new DelegateCommand(me.Id, HouseholdSystem.Advocate, brother.Id, 0).Refusal(ctx));
        Assert.Contains("council", new RetainerCommand(me.Id, RetainerCommand.Appoint, CourtSystem.Guard, brother.Id, Content.Names).Refusal(ctx));
        // the place is empty again when they are no longer of the house, and can be taken back before that
        var back = new RetainerCommand(me.Id, RetainerCommand.Dismiss, CourtSystem.Ceremonies, "", Content.Names); Assert.True(tc.Executor.TryExecute(back, ctx), back.Refusal(ctx)); Assert.Null(house.Position(CourtSystem.Ceremonies));
        Assert.Contains("nobody holds it", back.Refusal(ctx));
        Assert.True(tc.Executor.TryExecute(cmd, ctx)); kin.Alive = false; CourtSystem.Tick(w, Content.Policies); Assert.Null(house.Position(CourtSystem.Ceremonies));
    }

    [Fact]
    public void The_court_is_the_houses_and_reads_no_rank_office_or_throne()
    {
        var (tc, w, house, me, ctx) = Start(); house.Treasury.Credits = 500;
        foreach (var role in CourtSystem.Positions) Hire(tc, w, house, me, ctx, role);
        var skills = CourtSystem.Positions.ToDictionary(r => r, r => CourtSystem.Skill(w, house.Id, r)); Assert.All(skills.Values, s => Assert.True(s > 0));
        house.Title = "landless"; me.CareerRank = 0; me.CareerTrack = ""; house.Enrolled = false; foreach (var d in house.Duties.ToList()) house.Duties.Remove(d);
        Assert.Equal(skills, CourtSystem.Positions.ToDictionary(r => r, r => CourtSystem.Skill(w, house.Id, r)));
        CourtSystem.Tick(w, Content.Policies); Assert.Equal(7, house.Court.Count);
        // and only the player's house keeps one
        var other = w.Houses.Values.Where(h => h.Id != house.Id && !World.Outsider(h.Id) && Economy.HeadOf(w, h) != null).OrderBy(h => h.Id).First(); var theirHead = Economy.HeadOf(w, other)!;
        Assert.NotNull(CourtSystem.HireRefusal(w, Content.Names, theirHead, CourtSystem.Guard, CourtSystem.Offers(w, Content.Names, other, CourtSystem.Guard)[0].Id));
        other.Court.Add(new Retainer { Id = "x", Role = CourtSystem.Guard, Skill = 5 }); Assert.Equal(0, CourtSystem.Skill(w, other.Id, CourtSystem.Guard));
    }

    [Fact]
    public void Each_position_does_its_one_thing()
    {
        var (tc, w, house, me, ctx) = Start(); house.Treasury.Credits = 500;
        var foe = Economy.HeadOf(w, w.Houses.Values.Where(h => h.Id != house.Id && !World.Outsider(h.Id) && Economy.HeadOf(w, h) != null).OrderBy(h => h.Id).First())!;
        // the guard, against a knife
        double bare = AssassinateCommand.Odds(w, foe, me); var guard = Hire(tc, w, house, me, ctx, CourtSystem.Guard);
        Assert.Equal(bare - CourtSystem.GuardPerSkill * guard.Skill, AssassinateCommand.Odds(w, foe, me), 6);
        CourtSystem.Held(w, me); Assert.True(w.HasFlag(CourtSystem.GuardFlag)); Assert.Contains(CourtSystem.GuardedChar, me.Flags);
        // the tutor, for every child of the house and no other
        Assert.Equal(0, CourtSystem.TutorPoints(w, house.Id)); var tutor = Hire(tc, w, house, me, ctx, CourtSystem.Tutor); Assert.Equal(1, CourtSystem.TutorPoints(w, house.Id)); tutor.Skill = 4; Assert.Equal(2, CourtSystem.TutorPoints(w, house.Id));
        Assert.Equal(0, CourtSystem.TutorPoints(w, foe.HouseId));
        // the shipwright, on a hull and on fleets in port
        var wright = Hire(tc, w, house, me, ctx, CourtSystem.Shipwright); Assert.Equal(wright.Skill * CourtSystem.ShipwrightPctPerSkill, CourtSystem.ShipwrightPct(w, house.Id));
        var fleet = w.Fleets.Values.FirstOrDefault(f => f.OwnerId == house.Id && !f.Moving && !f.IsAway);
        if (fleet != null) { fleet.Readiness = 50; CourtSystem.Tick(w, Content.Policies); Assert.Equal(50 + wright.Skill, fleet.Readiness); }
        // the physician: with the best there is, most who fall ill are well within the year
        var doc = Hire(tc, w, house, me, ctx, CourtSystem.Physician); doc.Skill = 5; int cured = 0;
        for (int i = 0; i < 40; i++) { var k = Relative(w, house, me, "sick_" + i); k.Flags.Add(LifeSystem.IllChar); k.Health = 50; if (CourtSystem.Treats(w, k)) { cured++; Assert.DoesNotContain(LifeSystem.IllChar, k.Flags); } Assert.True(k.Health > 50); }
        Assert.InRange(cured, 25, 40); Assert.True(w.HasFlag(CourtSystem.CuredFlag)); Assert.Single(w.Characters.Values, c => c.Flags.Contains(CourtSystem.CuredChar));
        // the nurse, at a birth; the master of ceremonies, at a wedding and with the old blood
        Hire(tc, w, house, me, ctx, CourtSystem.Nurse); CourtSystem.Born(w, Relative(w, house, me, "court_baby", "male", 0)); Assert.True(w.HasFlag(CourtSystem.CradleFlag));
        var herald = Hire(tc, w, house, me, ctx, CourtSystem.Ceremonies); int legit = house.Legitimacy = 50; CourtSystem.Wedding(w, house); Assert.Equal(legit + 1, house.Legitimacy); Assert.True(w.HasFlag(CourtSystem.FeastFlag));
        int regard = PolicySystem.Approval(w, house.Id, "old_blood"); CourtSystem.Tick(w, Content.Policies); Assert.True(PolicySystem.Approval(w, house.Id, "old_blood") > regard || regard >= 100);
        // the duel master drills the least practised of the young
        Hire(tc, w, house, me, ctx, CourtSystem.DuelMaster).Skill = 5; var young = Relative(w, house, me, "court_young", "male", 17); int before = young.Skill("gunnery") + young.Skill("tactics");
        for (int i = 0; i < 12 && young.Skill("gunnery") + young.Skill("tactics") == before; i++) { house.Treasury.Credits = 500; w.Turn++; CourtSystem.Tick(w, Content.Policies); }
        Assert.True(young.Skill("gunnery") + young.Skill("tactics") > before || w.Characters.Values.Any(c => c.Flags.Contains(CourtSystem.PupilChar)));
    }

    [Fact]
    public void The_law_on_wages_makes_the_household_dearer_and_more_loyal_or_cheaper_and_less()
    {
        var law = Content.Policies.Law("household_wages"); Assert.NotNull(law); Assert.Equal("house", law!.Scope); Assert.Equal(3, law.Options.Count); Assert.Equal(law, Content.Policies.Laws.Last());
        int Run(string option)
        {
            var (_, w, house, me, _) = Start(); house.Treasury.Credits = 500; if (option.Length > 0) PolicySystem.Enact(w, Content.Policies, house.Id, "household_wages", option);
            CourtSystem.Hire(w, Content.Names, me, CourtSystem.Guard, CourtSystem.Offers(w, Content.Names, house, CourtSystem.Guard)[0].Id); var r = house.Position(CourtSystem.Guard)!;
            int had = house.Treasury.Credits; CourtSystem.Tick(w, Content.Policies); return (had - house.Treasury.Credits) * 1000 + r.Loyalty;
        }
        int mean = Run("mean"), usual = Run(""), open = Run("open_handed");
        Assert.True(mean / 1000 < usual / 1000 && usual / 1000 < open / 1000, $"{mean} {usual} {open}"); Assert.True(mean % 1000 < usual % 1000 && usual % 1000 < open % 1000, $"{mean} {usual} {open}");
    }

    [Theory]
    [InlineData("the_physicians_cure", CourtSystem.CuredFlag, CourtSystem.CuredChar)]
    [InlineData("the_guard_between", CourtSystem.GuardFlag, CourtSystem.GuardedChar)]
    [InlineData("the_nurse_at_the_cradle", CourtSystem.CradleFlag, "")]
    [InlineData("grown_old_in_service", CourtSystem.OldFlag, "")]
    [InlineData("the_duel_masters_pupil", CourtSystem.PupilFlag, CourtSystem.PupilChar)]
    [InlineData("the_feast_well_ordered", CourtSystem.FeastFlag, "")]
    [InlineData("the_retainer_who_left", CourtSystem.LeftFlag, "")]
    public void Each_scene_of_the_court_can_be_reached_and_every_answer_to_it_taken(string eventId, string flag, string mark)
    {
        foreach (string answer in Content.Events.First(e => e.Id == eventId).Choices.Select(c => c.Id))
        {
            var (_, w, house, me, _) = Start(); house.Treasury.Credits = 100; w.WorldFlags.Add(flag); if (mark.Length > 0) Relative(w, house, me).Flags.Add(mark);
            var engine = new Praetoria.Core.Events.EventEngine(Content, WorldBuilder.RngFor(w));
            var found = engine.GatherEligible(w).FirstOrDefault(e => e.def.Id == eventId); Assert.True(found.def != null, eventId + " is not eligible");
            engine.Resolve(new Praetoria.Core.Events.FiredEvent(found.def!, found.binding), w, answer);
            Assert.False(w.HasFlag(flag), eventId + "/" + answer + " leaves its flag up");
        }
    }

    [Fact]
    public void How_one_of_the_household_was_treated_is_seen_by_the_rest()
    {
        var (_, w, house, me, _) = Start(); house.Treasury.Credits = 500;
        CourtSystem.Hire(w, Content.Names, me, CourtSystem.Tutor, CourtSystem.Offers(w, Content.Names, house, CourtSystem.Tutor)[0].Id); var r = house.Position(CourtSystem.Tutor)!;
        w.WorldCounters[CourtSystem.GoodwillKey] = 2; CourtSystem.Tick(w, Content.Policies);
        Assert.Equal(40 + CourtSystem.PaidLoyalty + 2 * CourtSystem.GoodwillLoyalty, r.Loyalty); Assert.Equal(0, w.Counter(CourtSystem.GoodwillKey));   // read once
        // a deed's flag does not stand for ever if nothing is made of it
        w.WorldFlags.Add(CourtSystem.FeastFlag); w.WorldCounters["mark:" + CourtSystem.FeastFlag] = w.Turn; w.Turn += 2; CourtSystem.Tick(w, Content.Policies); Assert.False(w.HasFlag(CourtSystem.FeastFlag));
    }

    [Fact]
    public void The_court_survives_a_save_and_fourteen_years()
    {
        var (tc, w, house, me, ctx) = Start(); house.Treasury.Credits = 2000;
        foreach (var role in CourtSystem.Positions) Hire(tc, w, house, me, ctx, role);
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w)); var theirs = back.House(house.Id)!.Court;
        Assert.Equal(house.Court.Select(r => $"{r.Id}|{r.Role}|{r.Name}|{r.Sex}|{r.Age}|{r.Skill}|{r.Since}|{r.Wage}|{r.Loyalty}|{r.From}|{r.KinId}|{r.UnpaidYears}"), theirs.Select(r => $"{r.Id}|{r.Role}|{r.Name}|{r.Sex}|{r.Age}|{r.Skill}|{r.Since}|{r.Wage}|{r.Loyalty}|{r.From}|{r.KinId}|{r.UnpaidYears}"));
        for (int i = 0; i < 14 && w.Protagonist?.Alive == true; i++) { w.House(w.Protagonist.HouseId)!.Treasury.Credits += 200; Year(tc); }
        Assert.All(house.Court, r => { Assert.InRange(r.Loyalty, 0, 100); Assert.InRange(r.Skill, 1, CourtSystem.MaxSkill); Assert.Contains(r.Role, CourtSystem.Positions); });
        Assert.Equal(house.Court.Count, house.Court.Select(r => r.Role).Distinct().Count());
    }
}
