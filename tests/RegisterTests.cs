using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Small gaps closed straight off the register (docs/GAPS.md, docs/PROCESS.md "Work down the register without asking"):
/// each test is named for the row it closes.</summary>
public class RegisterTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static (TurnController tc, World w, Character me, House mine, CommandContext ctx) Start(string scenario = "the_old_lion", int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content); tc.BeginTurn();
        return (tc, w, w.Protagonist!, w.House(w.Protagonist!.HouseId)!, new CommandContext(w, TestContent.Rng()));
    }
    private static bool Do(World w, ICommand cmd) => new CommandExecutor().TryExecute(cmd, new CommandContext(w, TestContent.Rng()));
    private static Character Kin(World w, House h, Character head, string id, int regard = 40, params string[] natures)
    {
        var c = new Character { Id = id, Name = "Test " + id, HouseId = h.Id, Age = 30, Sex = "male", MotherId = head.Sex == "female" ? head.Id : "", FatherId = head.Sex == "female" ? "" : head.Id };
        foreach (var n in natures) c.NatureTraits.Add(n); c.Skills["discipline"] = 3; w.Characters[id] = c; h.Members.Add(id); w.Relationship(id, head.Id).Disposition = regard; return c;
    }
    private static Heirloom Item(World w, Character holder, House of, string kind, bool entailed = false)
    {
        w.Heirlooms.RemoveAll(h => h.HolderId == holder.Id);
        var item = new Heirloom { Id = "test_" + kind, Name = "the test " + kind, Kind = kind, HolderId = holder.Id, OfHouseId = of.Id, Value = 20, Entailed = entailed }; w.Heirlooms.Add(item); return item;
    }
    private static void Alone(World w, House mine, Character me)
    {
        foreach (var c in w.LivingCharacters().Where(c => c.HouseId == mine.Id && c.Id != me.Id).ToList()) c.Alive = false;
    }

    private static void Fires(World w, string eventId)
    {
        var me = w.Protagonist!; w.House(me.HouseId)!.Treasury.Credits = 100; w.PoolsFor(me.Id).Influence = 9;   // every choice must be affordable: this is about eligibility
        var engine = new Praetoria.Core.Events.EventEngine(Content, WorldBuilder.RngFor(w));
        var found = engine.GatherEligible(w).FirstOrDefault(e => e.def.Id == eventId);
        Assert.True(found.def != null, $"{eventId} is not eligible");
        foreach (var c in found.def!.Choices)
        {
            var again = engine.GatherEligible(w).FirstOrDefault(e => e.def.Id == eventId); if (again.def == null) break;
            engine.Resolve(new Praetoria.Core.Events.FiredEvent(again.def, again.binding), w, c.Id);
        }
    }
    private static Character Blood(World w, Character me, Character k) { w.Relationship(me.Id, k.Id).Bond = BondType.Blood; return k; }

    [Fact]
    public void The_register_events_fire_when_what_they_are_about_has_happened()
    {
        var (tc, w, me, mine, ctx) = Start(); Alone(w, mine, me); mine.Allowance = WealthSystem.Customary; mine.Treasury.Credits = 100;
        // the entail broken
        var item = Item(w, me, mine, "estate", entailed: true); WealthSystem.SetEntail(w, me, item, false); Fires(w, "the_entail_is_broken");
        // what the head owes one of the house, six years on
        var k = Blood(w, me, Kin(w, mine, me, "kin")); k.Purse = 20; me.Purse = 0; WealthSystem.Borrow(w, me, k, 10); k.LentSince = w.Turn - WealthSystem.LoanSoursAfter; WealthSystem.Tick(w);
        Fires(w, "what_you_owe_your_own");
        // one of the house lends another
        var debtor = Blood(w, me, Kin(w, mine, me, "a_debtor", 40, "Honorable")); debtor.Purse = -12; var friend = Blood(w, me, Kin(w, mine, me, "b_friend", 40, "Honorable")); friend.Purse = 20;
        w.Relationship(friend.Id, debtor.Id).Disposition = 50; WealthSystem.Tick(w); Assert.True(w.HasFlag(WealthSystem.KinLentFlag)); Fires(w, "one_of_them_lent_the_other");
        // one of the house brings on another
        k.Flags.Add(EducationSystem.TaughtKinChar); EducationSystem.Mark(w, EducationSystem.KinTaughtFlag); Fires(w, "brought_on_by_one_of_the_house");
        // the household unpaid
        var steward = Kin(w, mine, me, "steward"); mine.Duties.Add(new Duty { Role = HouseholdSystem.Steward, HolderId = steward.Id, Since = w.Turn, Reserve = 100000 });
        mine.Treasury.Credits = 0; tc.Household.Tick(w, tc.Executor, ctx); Assert.True(w.HasFlag(HouseholdSystem.UnpaidFlag)); Fires(w, "the_household_goes_unpaid");
    }

    // ---------------- W3: the entail ----------------

    [Fact]
    public void W3_An_entailed_thing_goes_with_the_house_and_cannot_be_sold_given_or_willed_away()
    {
        var (_, w, me, mine, _) = Start();
        // an old name's own thing is entailed from the start
        Assert.All(w.Heirlooms.Where(h => w.House(h.OfHouseId)?.Origin == "old"), h => Assert.True(h.Entailed));
        Alone(w, mine, me); var heir = Kin(w, mine, me, "heir"); heir.Age = 40; var other = Kin(w, mine, me, "other"); other.Age = 25;
        var item = Item(w, me, mine, "signet");
        Assert.True(Do(w, new WealthCommand(me.Id, WealthCommand.Bequeath, item.Id, other.Id)));
        Assert.True(Do(w, new WealthCommand(me.Id, WealthCommand.EntailItem, item.Id, ""))); Assert.True(item.Entailed);
        Assert.False(me.Will!.Items.ContainsKey(item.Id));   // the entail overrides what the will said
        Assert.False(Do(w, new WealthCommand(me.Id, WealthCommand.EntailItem, item.Id, "")));
        Assert.Contains("entailed", WealthSystem.SellRefusal(w, me, item)); Assert.Contains("entailed", WealthSystem.GiveRefusal(w, me, item, other));
        Assert.False(Do(w, new WealthCommand(me.Id, WealthCommand.Sell, item.Id, ""))); Assert.False(Do(w, new WealthCommand(me.Id, WealthCommand.Give, item.Id, other.Id)));
        Assert.False(Do(w, new WealthCommand(me.Id, WealthCommand.Bequeath, item.Id, other.Id)));
        // whatever a will says, it goes to whoever has the house
        me.Will.Items[item.Id] = other.Id; WealthSystem.Inherit(w, me, heir); Assert.Equal(heir.Id, item.HolderId);
        // and it follows the house if it is ever found in the wrong hands
        item.HolderId = other.Id; me.Alive = false; w.ProtagonistId = heir.Id; WealthSystem.Tick(w); Assert.Equal(heir.Id, item.HolderId);
        // only the head, and only a thing of the house's own name
        var theirs = w.Houses.Values.First(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId);
        var foreign = new Heirloom { Id = "foreign", Name = "a foreign sword", Kind = "sword", HolderId = heir.Id, OfHouseId = theirs.Id, Value = 20 }; w.Heirlooms.Add(foreign);
        Assert.Contains("not of your house", WealthSystem.EntailRefusal(w, heir, foreign, true));
    }

    [Fact]
    public void W3_Breaking_an_entail_is_done_in_court_and_the_family_remembers_it()
    {
        var (_, w, me, mine, _) = Start(); Alone(w, mine, me); var k = Kin(w, mine, me, "kin"); var item = Item(w, me, mine, "estate", entailed: true);
        int legit = mine.Legitimacy, regard = w.Relationship(k.Id, me.Id).Disposition;
        Assert.True(Do(w, new WealthCommand(me.Id, WealthCommand.BreakEntail, item.Id, ""))); Assert.False(item.Entailed);
        Assert.Equal(legit - WealthSystem.BreakLegitimacy, mine.Legitimacy); Assert.Equal(regard - WealthSystem.BreakRegard, w.Relationship(k.Id, me.Id).Disposition);
        Assert.True(w.HasFlag(WealthSystem.EntailBrokenFlag));
        Assert.True(Do(w, new WealthCommand(me.Id, WealthCommand.Sell, item.Id, "")));   // theirs to sell now
    }

    // ---------------- W5: what a thing handed down does ----------------

    [Fact]
    public void W5_A_signet_mends_a_name_a_sword_teaches_in_the_fleet_and_a_library_speeds_study()
    {
        var (_, w, me, mine, _) = Start(); Alone(w, mine, me);
        var signet = Item(w, me, mine, "signet"); mine.Legitimacy = 30; WealthSystem.Tick(w); Assert.Equal(31, mine.Legitimacy);
        mine.Legitimacy = WealthSystem.SignetMendsBelow; WealthSystem.Tick(w); Assert.Equal(WealthSystem.SignetMendsBelow, mine.Legitimacy);   // only a name that wants mending
        var k = Kin(w, mine, me, "kin"); signet.HolderId = k.Id; mine.Legitimacy = 30; WealthSystem.Tick(w); Assert.Equal(30, mine.Legitimacy);   // not off the head's hand
        Assert.Contains("Does nothing", WealthSystem.Does(w, signet));

        w.Heirlooms.Clear(); var sword = new Heirloom { Id = "s", Name = "the sword", Kind = "sword", HolderId = k.Id, OfHouseId = mine.Id, Value = 20 }; w.Heirlooms.Add(sword);
        k.Skills["discipline"] = 0; k.Learning.Clear(); k.CareerTrack = ""; WealthSystem.Tick(w); Assert.Equal(0, k.Learning.GetValueOrDefault("discipline"));
        k.CareerTrack = "military"; int ceiling = EducationSystem.Ceiling(k, "discipline"); WealthSystem.Tick(w);
        Assert.True(ceiling <= 0 || k.Learning.GetValueOrDefault("discipline") == WealthSystem.SwordPoints);

        // the library: two of the same house and the same study, one world with it and one without
        int Learned(bool library)
        {
            var (_, w2, me2, mine2, _) = Start(); Alone(w2, mine2, me2); w2.Heirlooms.Clear(); var pupil = Kin(w2, mine2, me2, "pupil"); pupil.CareerTrack = "";
            string study = EducationSystem.All.First(s => EducationSystem.Ceiling(pupil, s) >= 4); pupil.Skills.Clear(); pupil.Study = study;
            if (library) w2.Heirlooms.Add(new Heirloom { Id = "l", Name = "the library", Kind = "library", HolderId = me2.Id, OfHouseId = mine2.Id, Value = 20 });
            Assert.Equal(library, WealthSystem.HasLibrary(w2, mine2.Id));
            EducationSystem.Tick(w2); return pupil.Skill(study) * 100 + pupil.Learning.GetValueOrDefault(study);
        }
        Assert.True(Learned(true) > Learned(false));
    }

    // ---------------- W6: loans the other way, between relatives, at interest, and a bride's own purse ----------------

    [Fact]
    public void W6_The_head_can_borrow_of_a_relative_who_may_refuse_or_want_a_fifth_on_top()
    {
        var (_, w, me, mine, _) = Start(); Alone(w, mine, me); me.Purse = 0;
        var cold = Kin(w, mine, me, "cold", regard: 5); cold.Purse = 20; Assert.Contains("will not lend", WealthSystem.BorrowRefusal(w, me, cold, 10));
        var loyal = Kin(w, mine, me, "loyal", 5, "Loyal"); loyal.Purse = 20; Assert.Null(WealthSystem.BorrowRefusal(w, me, loyal, 10));
        Assert.Contains("does not hold", WealthSystem.BorrowRefusal(w, me, loyal, 19));   // they keep something back
        Assert.True(Do(w, new WealthCommand(me.Id, WealthCommand.Borrow, loyal.Id, "10"))); Assert.Equal(10, me.Purse); Assert.Equal(10, loyal.Purse); Assert.Equal(10, loyal.LentHead);
        Assert.False(Do(w, new WealthCommand(me.Id, WealthCommand.Borrow, loyal.Id, "2")));   // owed already
        var keen = Kin(w, mine, me, "keen"); keen.AptitudeTraits.Add("Greedy"); keen.Purse = 30;
        Assert.True(Do(w, new WealthCommand(me.Id, WealthCommand.Borrow, keen.Id, "10"))); Assert.Equal(12, keen.LentHead);
        // paying back: as far as the purse goes
        me.Purse = 4; int regard = w.Relationship(loyal.Id, me.Id).Disposition;
        Assert.True(Do(w, new WealthCommand(me.Id, WealthCommand.Repay, loyal.Id, ""))); Assert.Equal(6, loyal.LentHead); Assert.Equal(0, me.Purse);
        Assert.False(Do(w, new WealthCommand(me.Id, WealthCommand.Repay, loyal.Id, "")));
        me.Purse = 50; Assert.True(Do(w, new WealthCommand(me.Id, WealthCommand.Repay, loyal.Id, ""))); Assert.Equal(0, loyal.LentHead); Assert.Equal(regard + 5, w.Relationship(loyal.Id, me.Id).Disposition);
    }

    [Fact]
    public void W6_A_debt_the_head_leaves_too_long_is_minded_and_is_paid_out_of_what_they_leave()
    {
        var (_, w, me, mine, _) = Start(); Alone(w, mine, me); mine.Allowance = WealthSystem.Customary; mine.Treasury.Credits = 100;
        var k = Kin(w, mine, me, "kin"); k.Purse = 20; me.Purse = 0; WealthSystem.Borrow(w, me, k, 10); k.LentSince = w.Turn - WealthSystem.LoanSoursAfter;
        int regard = w.Relationship(k.Id, me.Id).Disposition; WealthSystem.Tick(w);
        Assert.Equal(regard - 2, w.Relationship(k.Id, me.Id).Disposition); Assert.True(w.HasFlag(WealthSystem.HeadOwesFlag)); Assert.Contains(WealthSystem.HeadOwesChar, k.Flags);
        // the head dies owing it: it comes out of their purse before anything is shared
        var heir = Kin(w, mine, me, "heir"); me.Purse = 30; int had = k.Purse; WealthSystem.Inherit(w, me, heir);
        Assert.Equal(had + 10, k.Purse); Assert.Equal(0, k.LentHead); Assert.Equal(20, heir.Purse);
    }

    [Fact]
    public void W6_One_of_the_house_in_debt_is_helped_by_another_who_thinks_well_of_them_and_pays_it_back()
    {
        var (_, w, me, mine, _) = Start(); Alone(w, mine, me); mine.Allowance = WealthSystem.Mean; mine.Treasury.Credits = 100;
        var debtor = Kin(w, mine, me, "a_debtor", 40, "Honorable"); debtor.Purse = -12; var friend = Kin(w, mine, me, "b_friend", 40, "Honorable"); friend.Purse = 20;
        w.Relationship(friend.Id, debtor.Id).Disposition = 50; int regard = w.Relationship(debtor.Id, friend.Id).Disposition;
        WealthSystem.Tick(w);
        Assert.Equal(friend.Id, debtor.OwesKinId); Assert.True(debtor.OwesKin > 0); Assert.True(w.HasFlag(WealthSystem.KinLentFlag));
        Assert.Equal(regard + 6, w.Relationship(debtor.Id, friend.Id).Disposition); Assert.True(debtor.Purse > -12);
        // paid back as they can
        int owed = debtor.OwesKin; debtor.Purse = 12; WealthSystem.Tick(w); Assert.True(debtor.OwesKin < owed);
        // one who thinks nothing of them does not
        var (_, w2, me2, mine2, _) = Start(); Alone(w2, mine2, me2); mine2.Allowance = WealthSystem.Mean;
        var d2 = Kin(w2, mine2, me2, "a_debtor", 40, "Honorable"); d2.Purse = -12; var f2 = Kin(w2, mine2, me2, "b_friend", 40, "Honorable"); f2.Purse = 20; w2.Relationship(f2.Id, d2.Id).Disposition = 0;
        WealthSystem.Tick(w2); Assert.Equal(0, d2.OwesKin);
    }

    [Fact]
    public void W6_A_loan_at_interest_is_business_and_a_bride_puts_her_own_purse_toward_her_portion()
    {
        var (_, w, me, mine, _) = Start(); Alone(w, mine, me); var k = Kin(w, mine, me, "kin"); me.Purse = 20; int regard = w.Relationship(k.Id, me.Id).Disposition;
        Assert.True(Do(w, new WealthCommand(me.Id, WealthCommand.LendAtInterest, k.Id, "10"))); Assert.Equal(12, k.OwesHead); Assert.Equal(regard - 1, w.Relationship(k.Id, me.Id).Disposition);

        var bride = Kin(w, mine, me, "bride"); bride.Sex = "female"; bride.Age = 22; bride.Purse = 12; mine.Treasury.Credits = 100; mine.DowryFromPurse = false;
        var groom = w.Characters.Values.First(c => c.Alive && c.Sex == "male" && c.Age >= 18 && c.HouseId != mine.Id && w.House(c.HouseId) is { } h && h.Id != PirateSystem.HouseId && w.MarriageOf(c.Id) == null);
        int due = CourtshipSystem.DowryAmount(w, mine.Id, CourtshipSystem.Customary), hers = Math.Min(6, due / 2);
        var m = CourtshipSystem.Wed(w, bride, groom, CourtshipSystem.Arranged, CourtshipSystem.Customary);
        Assert.Equal(12 - hers, bride.Purse); Assert.Equal(100 - (due - hers), mine.Treasury.Credits); Assert.Equal(due, m.Dowry);
    }

    // ---------------- H3: the household's keep ----------------

    [Fact]
    public void H3_A_household_is_paid_for_and_one_that_is_not_knows_it()
    {
        var (tc, w, me, mine, ctx) = Start(); Alone(w, mine, me); mine.Petitions.Clear();
        var theirs = w.Houses.Values.First(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId);
        var guest = new Character { Id = "guest", Name = "Tomas Outsider", HouseId = theirs.Id, Age = 30, Sex = "male" }; w.Characters[guest.Id] = guest; theirs.Members.Add(guest.Id);
        w.Relationship(guest.Id, me.Id).Disposition = 90; HouseholdSystem.TakeIn(w, me, guest, HouseholdSystem.Companion);
        var steward = Kin(w, mine, me, "steward"); mine.Duties.Add(new Duty { Role = HouseholdSystem.Steward, HolderId = steward.Id, Since = w.Turn, Reserve = 100000 });
        Assert.Equal(HouseholdSystem.KeepEach + HouseholdSystem.Stipend, HouseholdSystem.Keep(w, mine));
        mine.Treasury.Credits = 50; int purse = steward.Purse; tc.Household.Tick(w, tc.Executor, ctx);
        Assert.Equal(50 - HouseholdSystem.KeepEach - HouseholdSystem.Stipend, mine.Treasury.Credits); Assert.Equal(purse + HouseholdSystem.Stipend, steward.Purse); Assert.False(w.HasFlag(HouseholdSystem.UnpaidFlag));
        // an empty treasury: they stay, and think the worse of it
        mine.Treasury.Credits = 0; w.Relationship(guest.Id, me.Id).Disposition = 50; w.Relationship(steward.Id, me.Id).Disposition = 40; tc.Household.Tick(w, tc.Executor, ctx);
        Assert.True(w.HasFlag(HouseholdSystem.UnpaidFlag)); Assert.True(w.Relationship(guest.Id, me.Id).Disposition < 50); Assert.Contains(guest.Id, mine.Members);
    }

    // ---------------- E5, E6 ----------------

    [Fact]
    public void E5_Nobody_sits_above_their_ceiling()
    {
        foreach (var scenario in new[] { "the_old_lion", "the_new_man", "the_risen_officer" })
        {
            var (_, w, _, _, _) = Start(scenario);
            foreach (var c in w.LivingCharacters()) foreach (var (skill, level) in c.Skills) Assert.True(level <= EducationSystem.Ceiling(c, skill), $"{c.Name} {skill} {level}");
        }
        var (_, w2, me, mine, _) = Start(); var k = Kin(w2, mine, me, "kin"); string s = EducationSystem.All.First(x => EducationSystem.Ceiling(k, x) < 8);
        k.Skills[s] = 8; Assert.Equal(8, EducationSystem.Ceiling(k, s)); Assert.False(EducationSystem.Step(w2, k, s));   // written so, and no further
    }

    [Fact]
    public void E6_One_asked_to_study_may_name_a_price_and_a_teacher_of_the_house_can_be_thanked()
    {
        var (_, w, me, mine, _) = Start(); Alone(w, mine, me); var k = Kin(w, mine, me, "kin", 30, "Proud");   // wilful: would want a regard of 40
        string skill = EducationSystem.All.First(s => k.Skill(s) < EducationSystem.Ceiling(k, s) && k.Study != s);
        Assert.False(EducationSystem.WouldStudy(w, me, k, skill)); Assert.Equal(EducationSystem.PriceNear, EducationSystem.StudyPrice(w, me, k, skill));
        me.Purse = 0; Assert.Contains("purse does not hold", EducationSystem.AskAndPayRefusal(w, me, k, skill));
        me.Purse = 10; int regard = w.Relationship(k.Id, me.Id).Disposition, had = k.Purse;
        Assert.True(Do(w, new EducationCommand(me.Id, EducationCommand.AskAndPay, k.Id, skill)));
        Assert.Equal(skill, k.Study); Assert.Equal(10 - EducationSystem.PriceNear, me.Purse); Assert.Equal(had + EducationSystem.PriceNear, k.Purse); Assert.Equal(regard, w.Relationship(k.Id, me.Id).Disposition);
        // further off, it costs more; too far, nothing will do; and one who would for the asking is not paid
        w.Relationship(k.Id, me.Id).Disposition = 20; k.Study = ""; Assert.Equal(EducationSystem.PriceFar, EducationSystem.StudyPrice(w, me, k, skill));
        w.Relationship(k.Id, me.Id).Disposition = 0; Assert.Equal(-1, EducationSystem.StudyPrice(w, me, k, skill)); Assert.Contains("for anything", EducationSystem.AskAndPayRefusal(w, me, k, skill));
        w.Relationship(k.Id, me.Id).Disposition = 60; Assert.Equal(0, EducationSystem.StudyPrice(w, me, k, skill)); Assert.Contains("for the asking", EducationSystem.AskAndPayRefusal(w, me, k, skill));

        // thanks: a purse to whoever of the house has another of it under their wing
        var pupil = Kin(w, mine, me, "pupil"); pupil.Age = 20; Assert.Contains("nobody of the house", EducationSystem.ThankRefusal(w, me, pupil));
        pupil.MentorId = k.Id; me.Purse = 10; regard = w.Relationship(k.Id, me.Id).Disposition;
        Assert.True(Do(w, new EducationCommand(me.Id, EducationCommand.Thank, pupil.Id, "")));
        Assert.Equal(10 - EducationSystem.ThanksCredits, me.Purse); Assert.Equal(regard + EducationSystem.ThanksRegard, w.Relationship(k.Id, me.Id).Disposition);
        Assert.Contains("not long ago", EducationSystem.ThankRefusal(w, me, pupil));
    }

    // ---------------- F5: the elder ----------------

    private static Character Elder(World w, House mine, Character me)
    {
        var e = Kin(w, mine, me, "elder", 60); e.Age = 70; e.Health = 80; e.Flags.Add(LifeSystem.RetiredChar); e.Life = new Life(); return e;
    }

    [Fact]
    public void F5_An_elder_makes_peace_takes_some_of_the_weight_and_speaks_for_the_new_head()
    {
        var (_, w, me, mine, _) = Start(); Alone(w, mine, me); var e = Elder(w, mine, me); Assert.Equal(e.Id, LifeSystem.ElderOf(w, mine)!.Id);
        var a = Kin(w, mine, me, "a_kin"); var b = Kin(w, mine, me, "b_kin"); w.Relationship(a.Id, b.Id).Disposition = -30; w.Relationship(b.Id, a.Id).Disposition = -10;
        LifeSystem.Tick(w, Content);
        Assert.Equal(-30 + LifeSystem.ElderPeace, w.Relationship(a.Id, b.Id).Disposition); Assert.Equal(-10 + LifeSystem.ElderPeace, w.Relationship(b.Id, a.Id).Disposition);
        Assert.True(w.HasFlag(LifeSystem.ElderPeaceFlag)); Assert.Contains("Sat", e.Life!.Counsel);
        // nobody at odds: a head carrying too much is eased
        w.Relationship(a.Id, b.Id).Disposition = 10; me.Stress = 60; me.Health = 90; LifeSystem.Tick(w, Content);
        Assert.True(me.Stress <= 60 - LifeSystem.ElderEases + 3); Assert.Contains("desk", e.Life.Counsel);
        // nothing wanting at home: they speak for the new head to someone who still thinks well of the old one
        me.Stress = 0; var theirs = w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId).OrderBy(h => h.Id, StringComparer.Ordinal).First(h => Economy.HeadOf(w, h) != null);
        var head = Economy.HeadOf(w, theirs)!; foreach (var h in w.Houses.Values) if (Economy.HeadOf(w, h) is { } x && x.Id != head.Id) w.Relationship(x.Id, e.Id).Disposition = 0;
        w.Relationship(head.Id, e.Id).Disposition = 50; w.Relationship(head.Id, me.Id).Disposition = 10; LifeSystem.Tick(w, Content);
        Assert.Equal(10 + LifeSystem.ElderSpeaks, w.Relationship(head.Id, me.Id).Disposition); Assert.Contains("Spoke for", e.Life.Counsel);
        // too unwell, they are only there
        e.Health = 10; w.Relationship(head.Id, me.Id).Disposition = 10; LifeSystem.Tick(w, Content); Assert.Contains("unwell", e.Life.Counsel); Assert.Equal(10, w.Relationship(head.Id, me.Id).Disposition);
    }

    [Fact]
    public void F5_The_elders_old_friends_are_the_houses_while_the_elder_lives()
    {
        var (_, w, me, mine, _) = Start(); Alone(w, mine, me); var e = Elder(w, mine, me);
        var theirs = w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId).OrderBy(h => h.Id, StringComparer.Ordinal).First(h => Economy.HeadOf(w, h) != null);
        var head = Economy.HeadOf(w, theirs)!; w.Relationship(head.Id, me.Id).Disposition = 10; e.Life!.Friends[head.Id] = w.Turn - LifeSystem.OldTieYears;
        LifeSystem.Tick(w, Content); Assert.Contains(LifeSystem.OldFriendChar, head.Flags); Assert.Equal(LifeSystem.TieVoteHead, LifeSystem.TieVotes(w, mine.Id));
        e.Alive = false; LifeSystem.Tick(w, Content); Assert.DoesNotContain(LifeSystem.OldFriendChar, head.Flags); Assert.Equal(0, LifeSystem.TieVotes(w, mine.Id));
    }

    // ---------------- F6: old ties at the count, in a court, in a match ----------------

    [Fact]
    public void F6_Old_ties_count_in_an_election_a_court_case_and_a_match()
    {
        var (_, w, me, mine, _) = Start(); var others = w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId && Economy.HeadOf(w, h) != null).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        foreach (var c in w.Characters.Values) { c.Flags.Remove(LifeSystem.OldFriendChar); c.Flags.Remove(LifeSystem.OldEnemyChar); }
        var friend = Economy.HeadOf(w, others[0])!; var enemy = Economy.HeadOf(w, others[1])!; var third = others[2];
        // the count
        Assert.Equal(0, LifeSystem.TieVotes(w, mine.Id)); friend.Flags.Add(LifeSystem.OldFriendChar);
        Assert.Equal(LifeSystem.TieVoteHead, LifeSystem.TieVotes(w, mine.Id)); Assert.Equal(0, LifeSystem.TieVotes(w, others[0].Id));   // they are the head's ties, nobody else's
        var cand = Kin(w, mine, me, "cand"); var rung = Content.Offices.Rung("quaestor")!; var rival = w.LivingCharacters().First(c => c.HouseId == third.Id && c.Age >= 18);
        int With() => LadderSystem.Tally(w, Content.Offices, rung, new List<Character> { cand, rival }, _ => 1)[cand.Id];
        int had = With(); friend.Flags.Remove(LifeSystem.OldFriendChar); Assert.Equal(had - LifeSystem.TieVoteHead, With());
        enemy.Flags.Add(LifeSystem.OldEnemyChar); Assert.Equal(had - 2 * LifeSystem.TieVoteHead, With());
        // a court: a magistrate who is an old friend finds for the house; an old enemy for the other side; the upright rule on the law
        var biz = new OfficeBusiness { Id = "c1", Kind = LadderSystem.Case, HouseA = mine.Id, HouseB = third.Id, RightId = third.Id };
        friend.Flags.Add(LifeSystem.OldFriendChar); friend.NatureTraits.Clear(); enemy.NatureTraits.Clear();
        Assert.Equal(mine.Id, LadderSystem.TieWinner(w, biz, friend)); Assert.Equal(third.Id, LadderSystem.TieWinner(w, biz, enemy));
        Assert.Equal(LadderSystem.Friend, LadderSystem.Inclination(w, friend, biz)); Assert.Equal(LadderSystem.Friend, LadderSystem.Inclination(w, enemy, biz));
        friend.NatureTraits.Add("Just"); Assert.Equal(LadderSystem.Law, LadderSystem.Inclination(w, friend, biz));
        Assert.Null(LadderSystem.TieWinner(w, new OfficeBusiness { Id = "c2", Kind = LadderSystem.Case, HouseA = others[3].Id, HouseB = third.Id, RightId = third.Id }, friend));   // not the house's case
        // a match
        var daughter = w.LivingCharacters().FirstOrDefault(c => c.HouseId == others[0].Id && c.Id != friend.Id) ?? friend;
        Assert.Equal(CourtshipSystem.OldFriendMatch, CourtshipSystem.OldTie(w, daughter));
        var theirs = w.LivingCharacters().FirstOrDefault(c => c.HouseId == others[1].Id && c.Id != enemy.Id) ?? enemy; Assert.Equal(CourtshipSystem.OldEnemyMatch, CourtshipSystem.OldTie(w, theirs));
        Assert.Equal(0, CourtshipSystem.OldTie(w, rival));
    }

    // ---------------- M1: other houses divorce ----------------

    [Fact]
    public void M1_A_marriage_of_other_houses_that_has_gone_on_apart_is_now_and_then_ended()
    {
        var (_, w, me, mine, _) = Start(); var others = w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        var a = new Character { Id = "np_a", Name = "Npc A", HouseId = others[0].Id, Age = 30, Sex = "male" }; var b = new Character { Id = "np_b", Name = "Npc B", HouseId = others[1].Id, Age = 28, Sex = "female" };
        foreach (var c in new[] { a, b }) { c.NatureTraits.Add("Cruel"); c.NatureTraits.Add("Arrogant"); w.Characters[c.Id] = c; w.House(c.HouseId)!.Members.Add(c.Id); }
        others[1].Treasury.Credits = 100; others[0].Treasury.Credits = 100; var m = CourtshipSystem.Wed(w, a, b, CourtshipSystem.Arranged, CourtshipSystem.Customary); int dowry = m.Dowry; Assert.True(dowry > 0);
        var chooser = Economy.HeadOf(w, others[0])!; chooser.NatureTraits.Remove("Honorable"); chooser.NatureTraits.Remove("Loyal");   // the house that took the dowry: its head ends it
        int got = others[1].Treasury.Credits; bool ended = false; int years = 0;
        for (int i = 0; i < 80 && !ended; i++)
        {
            years++;
            m.Warmth = 0; m.Estranged = true; others[0].Treasury.Credits = Math.Max(others[0].Treasury.Credits, dowry); int before = others[1].Treasury.Credits;
            w.Turn++; CourtshipSystem.Tick(w); ended = !w.Marriages.Contains(m);
            if (ended) Assert.Equal(before + dowry, others[1].Treasury.Credits);   // the dowry goes back
        }
        Assert.True(ended); Assert.Equal(CourtshipSystem.ApartYears, years);   // M6: it is chosen, in the third year apart, not thrown for
        Assert.Null(w.MarriageOf(a.Id)); Assert.False(w.HasFlag(CourtshipSystem.DivorcedFlag)); Assert.True(w.Counter("divorces_in_the_reach") >= 1); _ = got;
        // M6: an upright head does not end it; and with a child of the two, and the houses on terms, nobody does
        bool Ended(bool upright, bool child)
        {
            var (_, wx, mex, minex, _) = Start(); var ho = wx.Houses.Values.Where(h => h.Id != minex.Id && h.Id != PirateSystem.HouseId && Economy.HeadOf(wx, h) != null).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
            var x = new Character { Id = "np_x", Name = "Npc X", HouseId = ho[0].Id, Age = 30, Sex = "male" }; var y = new Character { Id = "np_y", Name = "Npc Y", HouseId = ho[1].Id, Age = 28, Sex = "female" };
            foreach (var c in new[] { x, y }) { wx.Characters[c.Id] = c; wx.House(c.HouseId)!.Members.Add(c.Id); }
            var h0 = Economy.HeadOf(wx, ho[0])!; var h1 = Economy.HeadOf(wx, ho[1])!; h0.NatureTraits.Remove("Honorable"); h0.NatureTraits.Remove("Loyal"); if (upright) h0.NatureTraits.Add("Honorable");
            wx.Relationship(h0.Id, h1.Id).Disposition = 30; wx.Relationship(h1.Id, h0.Id).Disposition = 30;
            var mx = CourtshipSystem.Wed(wx, x, y, CourtshipSystem.Arranged);
            if (child) { var kid = new Character { Id = "np_kid", Name = "Npc Kid", HouseId = ho[0].Id, Age = 3, Sex = "male", FatherId = x.Id, MotherId = y.Id }; wx.Characters[kid.Id] = kid; ho[0].Members.Add(kid.Id); }
            for (int i = 0; i < 8; i++) { mx.Warmth = 0; mx.Estranged = true; wx.Relationship(h0.Id, h1.Id).Disposition = 30; wx.Turn++; CourtshipSystem.Tick(wx); if (!wx.Marriages.Contains(mx)) return true; }
            return false;
        }
        Assert.True(Ended(upright: false, child: false)); Assert.False(Ended(upright: true, child: false)); Assert.False(Ended(upright: false, child: true));
        // the player's own marriage is never ended for them
        var (_, w2, me2, mine2, _) = Start(); var wife = w2.LivingCharacters().First(c => c.HouseId != mine2.Id && c.Sex != me2.Sex && c.Age >= 18 && w2.MarriageOf(c.Id) == null && w2.House(c.HouseId) != null);
        if (w2.MarriageOf(me2.Id) is { } old) w2.Marriages.Remove(old);
        var m2 = CourtshipSystem.Wed(w2, me2, wife, CourtshipSystem.Arranged);
        for (int i = 0; i < 80; i++) { m2.Warmth = 0; m2.Estranged = true; w2.Turn++; CourtshipSystem.Tick(w2); }
        Assert.Contains(m2, w2.Marriages);
    }

    [Fact]
    public void M1_A_marriage_between_two_houses_at_war_goes_cold_and_is_ended_and_a_love_match_bears_it_better()
    {
        // Measured (docs/GAP_ANALYSIS.md section 50): as first built, no marriage of another house ended in sixty runs, because none
        // ever went cold. What strains one is its two houses falling out.
        int Years(string kind, bool war)
        {
            var (_, w, me, mine, _) = Start(); var ho = w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId && Economy.HeadOf(w, h) != null).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
            var x = new Character { Id = "np_x", Name = "Npc X", HouseId = ho[0].Id, Age = 30, Sex = "male" }; var y = new Character { Id = "np_y", Name = "Npc Y", HouseId = ho[1].Id, Age = 28, Sex = "female" };
            foreach (var c in new[] { x, y }) { w.Characters[c.Id] = c; w.House(c.HouseId)!.Members.Add(c.Id); }
            var h0 = Economy.HeadOf(w, ho[0])!; h0.NatureTraits.Remove("Honorable"); h0.NatureTraits.Remove("Loyal");
            w.Wars.RemoveAll(q => q.Between(ho[0].Id, ho[1].Id)); if (war) w.Wars.Add(new War { HouseA = ho[0].Id, HouseB = ho[1].Id, Since = w.Turn });
            var m = CourtshipSystem.Wed(w, x, y, kind); m.Warmth = 60;
            for (int i = 1; i <= 40; i++) { w.Turn++; if (war && !w.AtWar(ho[0].Id, ho[1].Id)) w.Wars.Add(new War { HouseA = ho[0].Id, HouseB = ho[1].Id, Since = w.Turn }); CourtshipSystem.Tick(w); if (!w.Marriages.Contains(m)) return i; }
            return 99;
        }
        int arranged = Years(CourtshipSystem.Arranged, war: true), love = Years(CourtshipSystem.Love, war: true);
        Assert.InRange(arranged, 6, 20); Assert.True(love > arranged, $"a love match should bear a war longer ({love} against {arranged})");
        Assert.Equal(99, Years(CourtshipSystem.Arranged, war: false));   // at peace, and the houses on terms, it lasts
    }

    [Fact]
    public void The_elders_peace_is_an_event()
    {
        var (_, w, me, mine, _) = Start(); Alone(w, mine, me); Elder(w, mine, me);
        var a = Blood(w, me, Kin(w, mine, me, "a_kin")); var b = Blood(w, me, Kin(w, mine, me, "b_kin")); w.Relationship(a.Id, b.Id).Disposition = -30;
        LifeSystem.Tick(w, Content); Fires(w, "the_elder_makes_peace");
    }

    // ---------------- L9, A5, B14, J4 ----------------

    [Fact]
    public void L9_The_merchants_and_the_magistrates_own_order_feel_it_when_the_magistrates_are_not_believed()
    {
        int Standing(int trust, string estate)
        {
            var (_, w, me, mine, _) = Start(); w.Magistrates.Clear(); w.Magistrates.Add(new OfficeHolding { OfficeId = "quaestor", HouseId = mine.Id, CharacterId = me.Id, SinceTurn = w.Turn });
            w.WorldCounters[LadderSystem.TrustKey] = trust; LadderSystem.Tick(w, Content.Offices, Content.Policies);
            return w.Counter("estate_offset:" + estate);
        }
        foreach (var estate in new[] { "merchants", "magistrates" })
        {
            int steady = Standing(60, estate); Assert.True(Standing(20, estate) < steady, estate); Assert.True(Standing(84, estate) > steady, estate);
        }
    }

    [Fact]
    public void A5_The_house_divides_behind_two_who_quarrel()
    {
        var (_, w, me, mine, _) = Start(); Alone(w, mine, me); var a = Kin(w, mine, me, "a_kin"); var b = Kin(w, mine, me, "b_kin");
        var forA = Kin(w, mine, me, "c_for_a"); var forB = Kin(w, mine, me, "d_for_b"); var neither = Kin(w, mine, me, "e_neither");
        w.Relationship(forA.Id, a.Id).Disposition = 40; w.Relationship(forA.Id, b.Id).Disposition = 0; w.Relationship(forB.Id, b.Id).Disposition = 30; w.Relationship(forB.Id, a.Id).Disposition = 10;
        w.Relationship(neither.Id, a.Id).Disposition = 20; w.Relationship(neither.Id, b.Id).Disposition = 15;
        var members = new[] { me, a, b, forA, forB, neither };
        var (withA, withB) = AmbitionSystem.Sides(w, members, a, b);
        Assert.Equal(new[] { forA.Id }, withA.Select(c => c.Id)); Assert.Equal(new[] { forB.Id }, withB.Select(c => c.Id));   // the head is not counted, nor one who thinks much the same of both
    }

    [Fact]
    public void B14_A_house_stirred_against_another_moves_a_censure_of_it()
    {
        var (tc, w, me, mine, _) = Start(); mine.Treasury.Credits = 200;
        var others = w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId && PolitySystem.InReach(w, h.Id) && Economy.HeadOf(w, h) != null).OrderByDescending(h => FleetSystem.StrengthOf(w, h.Id)).ThenBy(h => h.Id, StringComparer.Ordinal).ToList();
        var against = others[^1]; against.Legitimacy = 60; var ah = Economy.HeadOf(w, against)!;
        var hearers = others.Where(h => h.Id != against.Id).Take(5).ToList();
        foreach (var h in hearers) w.Relationship(Economy.HeadOf(w, h)!.Id, ah.Id).Disposition = -10;
        Assert.Null(PolitySystem.StirredAgainst(w, hearers[0].Id));
        tc.Polities.Stir(w, mine, against); Assert.Null(PolitySystem.StirredAgainst(w, hearers[0].Id));   // -18: not yet
        tc.Polities.Stir(w, mine, against); tc.Polities.Stir(w, mine, against);                             // -34: they have believed it
        Assert.All(hearers, h => Assert.Equal(against.Id, PolitySystem.StirredAgainst(w, h.Id)!.Id));
        Assert.Null(PolitySystem.StirredAgainst(w, mine.Id));
        // and it is moved in the chamber, by one of them, within the years the talk lasts
        w.Motions.Clear(); bool moved = false;
        for (int i = 0; i < PolitySystem.StirYears && !moved; i++) { tc.EndTurn(); tc.BeginTurn(); moved = w.Counter("censures_stirred") > 0; }
        Assert.True(moved);
        // when the talk has died down, nobody is stirred any longer
        w.Turn += PolitySystem.StirYears + 1; Assert.Null(PolitySystem.StirredAgainst(w, hearers[0].Id));
    }

    [Fact]
    public void J4_A_junior_has_somebody_above_them_by_name_and_their_standing_carries_from_year_to_year()
    {
        var (_, w, me, mine, _) = Start(); Alone(w, mine, me);
        var clerk = Kin(w, mine, me, "clerk"); clerk.CareerTrack = "stewardship"; clerk.CareerRank = 0; clerk.Age = 40;
        Assert.Equal(LadderSystem.Ledgers, LadderSystem.JuniorKind(w, clerk));
        var senior = LadderSystem.Above(w, clerk); Assert.NotNull(senior); Assert.True(LadderSystem.AtADesk(senior!)); Assert.Equal(senior!.Id, LadderSystem.Above(w, clerk)!.Id);   // the same one, asked again
        // one of their own house, if it has one at a desk
        var uncle = Kin(w, mine, me, "uncle"); uncle.CareerTrack = "stewardship"; uncle.CareerRank = LadderSystem.DeskRank; Assert.Equal(uncle.Id, LadderSystem.Above(w, clerk)!.Id);
        uncle.Alive = false;
        // the year's work is noticed by that one, and the next year's begins where this one's left off
        w.Relationship(senior.Id, clerk.Id).Disposition = 0; int rank = clerk.CareerRank;
        for (int year = 1; year <= 4; year++)
        {
            var b = new OfficeBusiness { Id = "ledgers_" + year, RungId = LadderSystem.Ledgers, HolderId = clerk.Id, Kind = LadderSystem.Ledgers, Turn = w.Turn, SubjectId = senior.Id }; w.OfficeWork.Add(b);
            Assert.Contains(senior.Name, LadderSystem.DutySays(w, Content.Policies, b));
            LadderSystem.DecideDuty(w, Content.Offices, Content.Policies, clerk, LadderSystem.Ledgers, LadderSystem.Useful);
            Assert.Equal(4 * year, w.Relationship(senior.Id, clerk.Id).Disposition); Assert.Equal(4 * year, LadderSystem.Standing(w, clerk));
        }
        Assert.Equal(rank, clerk.CareerRank);   // 16: noticed, not yet spoken for
        var last = new OfficeBusiness { Id = "ledgers_5", RungId = LadderSystem.Ledgers, HolderId = clerk.Id, Kind = LadderSystem.Ledgers, Turn = w.Turn, SubjectId = senior.Id }; w.OfficeWork.Add(last);
        LadderSystem.DecideDuty(w, Content.Offices, Content.Policies, clerk, LadderSystem.Ledgers, LadderSystem.Useful);
        Assert.Equal(rank + 1, clerk.CareerRank); Assert.True(w.HasFlag(LadderSystem.SpokenForFlag)); Assert.Contains(LadderSystem.SpokenForChar, clerk.Flags);
        Blood(w, me, clerk); Fires(w, "one_of_yours_is_spoken_for");

        // an advocate before a judge of their own house who is not upright wins what they would have lost, and it is seen
        var (_, w2, me2, mine2, _) = Start(); Alone(w2, mine2, me2); var adv = Kin(w2, mine2, me2, "adv"); adv.CareerTrack = "law"; adv.CareerRank = 0; adv.Skills.Clear();
        var judge = Kin(w2, mine2, me2, "judge"); judge.CareerTrack = "law"; judge.CareerRank = LadderSystem.JudgeRank; Assert.Equal(judge.Id, LadderSystem.Above(w2, adv)!.Id);
        var others = w2.Houses.Values.Where(h => h.Id != mine2.Id && h.Id != PirateSystem.HouseId).OrderBy(h => h.Id, StringComparer.Ordinal).Take(2).ToList();
        int seen = 0;
        for (int i = 0; i < 40; i++)
        {
            w2.WorldCounters[LadderSystem.TrustKey] = 50; w2.Relationship(judge.Id, adv.Id).Disposition = 0; w2.WorldCounters.Remove("trust_mended_in");
            var b = new OfficeBusiness { Id = "brief_" + i, RungId = LadderSystem.Brief, HolderId = adv.Id, Kind = LadderSystem.Brief, Turn = w2.Turn, HouseA = others[0].Id, HouseB = others[1].Id, SubjectId = judge.Id }; w2.OfficeWork.Add(b);
            LadderSystem.DecideDuty(w2, Content.Offices, Content.Policies, adv, LadderSystem.Brief, LadderSystem.Plead);
            if (LadderSystem.Trust(w2) < 50) seen++;
        }
        Assert.InRange(seen, 2, 12);   // fifteen in a hundred, more or less
        // a junior officer serves in a ship of the house, under whoever commands it
        var (_, w3, me3, mine3, _) = Start(); var lt = Kin(w3, mine3, me3, "lt"); lt.CareerTrack = "military"; lt.CareerRank = 1;
        if (LadderSystem.ShipOf(w3, lt) is { } ship) Assert.Equal(ship.AdmiralId.Length > 0 && w3.Char(ship.AdmiralId) is { Alive: true } ? ship.AdmiralId : null, LadderSystem.Above(w3, lt)?.Id);
        else Assert.Null(LadderSystem.Above(w3, lt));
        // a house with no ship of its own: its junior officer serves in one of the Reach's, the capital holder's
        var chair = RegionSystem.CapitalHolder(w3);
        if (chair != null && chair.Id != mine3.Id && w3.Fleets.Values.Any(f => f.OwnerId == chair.Id && f.Strength > 0))
        {
            foreach (var f in w3.Fleets.Values.Where(f => f.OwnerId == mine3.Id).ToList()) w3.Fleets.Remove(f.Id);
            mine3.PatronId = ""; var theirs = LadderSystem.ShipOf(w3, lt); Assert.NotNull(theirs); Assert.Equal(chair.Id, theirs!.OwnerId);
            var b = new OfficeBusiness { Id = "watch_x", RungId = LadderSystem.Watch, HolderId = lt.Id, Kind = LadderSystem.Watch, Turn = w3.Turn, RightId = theirs.Id, SubjectId = LadderSystem.Above(w3, lt)?.Id ?? "" };
            Assert.Contains(theirs.Name, LadderSystem.DutySays(w3, Content.Policies, b)); Assert.DoesNotContain("no ship", LadderSystem.DutySays(w3, Content.Policies, b));
        }
    }

    // ---------------- W7, H5, E7, F7: the small ones mapped while closing the others ----------------

    [Fact]
    public void W7_A_relative_who_lives_within_their_means_has_something_to_lend()
    {
        var (_, w, me, mine, _) = Start(); Alone(w, mine, me); mine.Allowance = WealthSystem.Generous; mine.Treasury.Credits = 500;
        var k = Kin(w, mine, me, "kin", 40, "Honorable"); k.Purse = 0; k.CareerTrack = "law"; k.CareerRank = 3;
        for (int i = 0; i < 12; i++) WealthSystem.Tick(w);
        Assert.InRange(k.Purse, 12, WealthSystem.ComfortablePurse);                       // enough to be asked for five or ten
        me.Purse = 0; Assert.Null(WealthSystem.BorrowRefusal(w, me, k, 5)); Assert.Null(WealthSystem.BorrowRefusal(w, me, k, 10));
        Assert.True(WealthSystem.KinLendsFrom <= 6);
    }

    [Fact]
    public void H5_The_keep_goes_by_standing_and_one_unpaid_two_years_running_goes()
    {
        var (tc, w, me, mine, ctx) = Start(); Alone(w, mine, me); mine.Petitions.Clear(); var theirs = w.Houses.Values.First(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId);
        Character Guest(string id, int age, int rank, int regard)
        {
            var g = new Character { Id = id, Name = "Guest " + id, HouseId = theirs.Id, Age = age, Sex = "male", CareerTrack = age >= 16 ? "law" : "", CareerRank = rank }; w.Characters[id] = g; theirs.Members.Add(id);
            w.Relationship(id, me.Id).Disposition = 90; HouseholdSystem.TakeIn(w, me, g, age < 16 ? HouseholdSystem.Ward : HouseholdSystem.Companion); w.Relationship(id, me.Id).Disposition = regard; return g;
        }
        var great = Guest("great", 45, HouseholdSystem.StandingRank, 10); var plain = Guest("plain", 30, 0, 60); var child = Guest("child", 9, 0, 0);
        Assert.Equal(HouseholdSystem.KeepOfStanding, HouseholdSystem.KeepOf(great)); Assert.Equal(HouseholdSystem.KeepEach, HouseholdSystem.KeepOf(plain)); Assert.Equal(HouseholdSystem.KeepEach, HouseholdSystem.KeepOf(child));
        Assert.Equal(HouseholdSystem.KeepOfStanding + 2 * HouseholdSystem.KeepEach, HouseholdSystem.Keep(w, mine));
        mine.Treasury.Credits = 0; tc.Household.Tick(w, tc.Executor, ctx); Assert.Contains(great.Id, mine.Members);                 // one bad year is borne
        mine.Treasury.Credits = 0; w.Relationship(great.Id, me.Id).Disposition = 10; w.Relationship(plain.Id, me.Id).Disposition = 60; tc.Household.Tick(w, tc.Executor, ctx);
        Assert.DoesNotContain(great.Id, mine.Members); Assert.Equal(theirs.Id, great.HouseId); Assert.True(w.HasFlag(HouseholdSystem.LeftUnpaidFlag));
        Assert.Contains(plain.Id, mine.Members); Assert.Contains(child.Id, mine.Members);   // one who thinks well of the head stays, and a child stays where they were put
        // paid again, the count starts over
        mine.Treasury.Credits = 50; tc.Household.Tick(w, tc.Executor, ctx); mine.Treasury.Credits = 0; w.Relationship(plain.Id, me.Id).Disposition = 0; tc.Household.Tick(w, tc.Executor, ctx);
        Assert.Contains(plain.Id, mine.Members);
    }

    [Fact]
    public void E7_A_relative_may_be_paid_in_a_word_instead_of_a_sum()
    {
        var (tc, w, me, mine, ctx) = Start(); Alone(w, mine, me); var k = Kin(w, mine, me, "kin", 30, "Proud");
        string skill = EducationSystem.All.First(s => k.Skill(s) < EducationSystem.Ceiling(k, s) && k.Study != s);
        Assert.Equal(1, EducationSystem.FavourPrice(w, me, k, skill)); me.Purse = 0; w.PoolsFor(me.Id).Influence = 0;
        Assert.False(tc.Executor.TryExecute(new EducationCommand(me.Id, EducationCommand.AskAndSpeak, k.Id, skill), ctx));   // no influence
        w.PoolsFor(me.Id).Influence = 3; int regard = w.Relationship(k.Id, me.Id).Disposition;
        Assert.True(tc.Executor.TryExecute(new EducationCommand(me.Id, EducationCommand.AskAndSpeak, k.Id, skill), ctx));   // an empty purse is no bar
        Assert.Equal(skill, k.Study); Assert.Equal(2, w.PoolsFor(me.Id).Influence); Assert.Equal(regard + 2, w.Relationship(k.Id, me.Id).Disposition); Assert.Equal(0, me.Purse);
        w.Relationship(k.Id, me.Id).Disposition = 20; k.Study = ""; Assert.Equal(2, EducationSystem.FavourPrice(w, me, k, skill));
        w.Relationship(k.Id, me.Id).Disposition = 0; Assert.False(tc.Executor.TryExecute(new EducationCommand(me.Id, EducationCommand.AskAndSpeak, k.Id, skill), ctx));   // not for anything
    }

    [Fact]
    public void F7_The_head_can_ask_the_elder_for_one_thing_and_an_elder_who_thinks_ill_of_them_keeps_their_own_counsel()
    {
        var (_, w, me, mine, _) = Start(); Alone(w, mine, me); var e = Elder(w, mine, me);
        var a = Kin(w, mine, me, "a_kin"); var b = Kin(w, mine, me, "b_kin"); w.Relationship(a.Id, b.Id).Disposition = -30; me.Stress = 20; me.Health = 90;
        // unasked, the quarrel comes first; asked to take the work, the elder does that instead, and with a will
        Assert.True(Do(w, new LifeCommand(me.Id, LifeCommand.AskElder, LifeSystem.AskEase))); Assert.Equal(LifeSystem.AskEase, e.Life!.Asked);
        Assert.False(Do(w, new LifeCommand(me.Id, LifeCommand.AskElder, LifeSystem.AskEase)));   // asked already
        LifeSystem.Tick(w, Content);
        Assert.Equal(-30, w.Relationship(a.Id, b.Id).Disposition); Assert.Contains("desk", e.Life.Counsel); Assert.Contains("You had asked it", e.Life.Counsel); Assert.Equal("", e.Life.Asked);
        Assert.True(me.Stress <= 20 - LifeSystem.ElderEases - LifeSystem.AskedMore + 3);
        // asked for what cannot be done, they do what most wants doing, and say so
        w.Relationship(a.Id, b.Id).Disposition = -30; foreach (var h in w.Houses.Values) if (Economy.HeadOf(w, h) is { } x) w.Relationship(x.Id, e.Id).Disposition = 0;
        Assert.True(Do(w, new LifeCommand(me.Id, LifeCommand.AskElder, LifeSystem.AskSpeak))); LifeSystem.Tick(w, Content);
        Assert.Equal(-30 + LifeSystem.ElderPeace, w.Relationship(a.Id, b.Id).Disposition); Assert.Contains("could not be done", e.Life.Counsel);
        // one who thinks ill of the head is not to be asked; nor one too unwell; nor is there anyone to ask in a house without one
        w.Relationship(e.Id, me.Id).Disposition = -5; Assert.Contains("own counsel", LifeSystem.AskElderRefusal(w, me, LifeSystem.AskPeace));
        w.Relationship(e.Id, me.Id).Disposition = 40; e.Health = 10; Assert.Contains("unwell", LifeSystem.AskElderRefusal(w, me, LifeSystem.AskPeace));
        e.Alive = false; Assert.Contains("no elder", LifeSystem.AskElderRefusal(w, me, LifeSystem.AskPeace)); Assert.Contains("no such thing", LifeSystem.AskElderRefusal(w, me, "sing"));
    }

    // ---------------- J5: a patron, and a falling out ----------------

    private static void Year(World w, Character junior, Character senior, string choice, int n)
    {
        var b = new OfficeBusiness { Id = $"ledgers_j5_{n}", RungId = LadderSystem.Ledgers, HolderId = junior.Id, Kind = LadderSystem.Ledgers, Turn = w.Turn, SubjectId = senior.Id }; w.OfficeWork.Add(b);
        LadderSystem.DecideDuty(w, Content.Offices, Content.Policies, junior, LadderSystem.Ledgers, choice);
    }

    [Fact]
    public void J5_A_senior_takes_a_junior_up_takes_them_along_when_they_rise_and_can_fall_out_with_them()
    {
        var (_, w, me, mine, _) = Start(); Alone(w, mine, me);
        var clerk = Blood(w, me, Kin(w, mine, me, "clerk")); clerk.CareerTrack = "stewardship"; clerk.CareerRank = 0; clerk.Age = 40;
        var senior = LadderSystem.Above(w, clerk)!; w.Relationship(senior.Id, clerk.Id).Disposition = 0;
        // four good years: well thought of, and not yet taken up
        for (int y = 1; y <= 4; y++) Year(w, clerk, senior, LadderSystem.Useful, y);
        Assert.Equal("", clerk.PatronId); Assert.Contains("begun to notice", LadderSystem.AboveSays(w, w.OfficeWork.Last()));
        // the fifth: the senior thinks well of them and has had the years of them
        Year(w, clerk, senior, LadderSystem.Useful, 5);
        Assert.Equal(senior.Id, clerk.PatronId); Assert.Equal(1, w.Counter(LadderSystem.PatronsCounter)); Assert.True(w.HasFlag(LadderSystem.PatronFlag)); Assert.Contains(LadderSystem.PatronChar, clerk.Flags);
        Assert.Contains(clerk.Life!.Deeds, d => d.Text.Contains("taken up")); Assert.Contains("taken you up", LadderSystem.AboveSays(w, w.OfficeWork.Last()));
        Fires(w, "one_of_yours_is_taken_up");
        // a patron is above them wherever either is posted: a kinsman at a desk does not take the patron's place
        var uncle = Kin(w, mine, me, "uncle"); uncle.CareerTrack = "stewardship"; uncle.CareerRank = LadderSystem.DeskRank;
        Assert.Equal(senior.Id, LadderSystem.Above(w, clerk)!.Id);
        // the patron rises, and takes them along
        int purse = clerk.Purse, administration = clerk.Skill("administration"); senior.CareerRank++;
        w.Turn++; LadderSystem.Tick(w, Content.Offices, Content.Policies);
        Assert.Equal(1, w.Counter(LadderSystem.TakenAlongCounter)); Assert.True(clerk.Purse >= purse + LadderSystem.TakenAlongCredits); Assert.True(clerk.Skill("administration") >= administration);
        Assert.True(w.HasFlag(LadderSystem.TakenAlongFlag)); Assert.Contains(clerk.Life.Deeds, d => d.Text.Contains("to a new post"));
        Fires(w, "one_of_yours_is_taken_along");
        // not again until they rise again
        w.Turn++; LadderSystem.Tick(w, Content.Offices, Content.Policies); Assert.Equal(1, w.Counter(LadderSystem.TakenAlongCounter));
        // the senior comes to think ill of them: a falling out, and the patron is lost
        w.Relationship(senior.Id, clerk.Id).Disposition = LadderSystem.FallOutAt - 5; w.OfficeWork.RemoveAll(b => b.HolderId == clerk.Id);
        Year(w, clerk, senior, LadderSystem.Diligent, 9);
        Assert.Equal("", clerk.PatronId); Assert.True(LadderSystem.FellOut(clerk, senior)); Assert.Equal(1, w.Counter(LadderSystem.FallingsOutCounter));
        Assert.True(w.HasFlag(LadderSystem.FellOutFlag)); Assert.Equal(0, w.Counter(LadderSystem.UnderKey(clerk.Id, senior.Id)));
        Assert.NotEqual(senior.Id, LadderSystem.Above(w, clerk)?.Id);   // that one will not be above them again
        Fires(w, "one_of_yours_falls_out");
        // it happens once: they are not fallen out with twice
        Year(w, clerk, senior, LadderSystem.Diligent, 10); Assert.Equal(1, w.Counter(LadderSystem.FallingsOutCounter));
    }

    [Fact]
    public void J5_A_patron_who_dies_is_no_patron_and_the_juniors_of_other_houses_are_taken_up_too()
    {
        var (_, w, me, mine, _) = Start();
        var junior = Kin(w, mine, me, "junior"); junior.CareerTrack = "law"; junior.CareerRank = 0; junior.Age = 30;
        var judge = LadderSystem.Above(w, junior)!; junior.PatronId = judge.Id; Assert.Equal(judge.Id, LadderSystem.PatronOf(w, junior)!.Id);
        judge.Alive = false; Assert.Null(LadderSystem.PatronOf(w, junior));
        w.Turn++; LadderSystem.Tick(w, Content.Offices, Content.Policies); Assert.Equal("", junior.PatronId);
        // years pass: the reckoning of other houses' juniors gives some of them patrons, steps and quarrels
        for (int y = 0; y < 16; y++) { w.Turn++; LadderSystem.Tick(w, Content.Offices, Content.Policies); }
        Assert.Contains(w.LivingCharacters(), c => c.HouseId != mine.Id && c.PatronId.Length > 0);
        Assert.True(w.Counter(LadderSystem.PatronsCounter) > 0); Assert.True(w.Counter(LadderSystem.SpokenCounter) > 0);
    }
}
