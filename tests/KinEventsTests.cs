using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Rng;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The family reports (docs/events/family.md): each is eligible when its moment is set, every choice applies and writes to the
/// chronicle, the threads carry from one report to the next, and the marks the family's systems raise for them (a birth, a grandchild,
/// the schoolroom at six, sixteen, the head of a spouse's family, a marriage's tenth year, one who left the household unpaid) are set
/// when they should be and struck out a year after.</summary>
public class KinEventsTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private sealed class Fixed : IRng { private readonly double _d; public Fixed(double d) => _d = d; public ulong State => 0; public double NextDouble() => _d; public int NextInt(int lo, int hi) => hi <= lo ? lo : lo + (int)(_d * (hi - lo)); }
    private const string Mine = "voss", Theirs = "harrow", Me = "aldric_voss", Spouse = "voss_senna", Sister = "voss_mira";

    /// <summary>The Fallen House in year ten, married, with a head who can open every gated choice: money, influence, the traits and
    /// skills the gates ask for.</summary>
    private static World Base(int age = 40)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_fallen_house"), 7); w.Turn = 10;
        CourtshipSystem.Sync(w, announce: false);
        var me = w.Protagonist!; var h = w.House(me.HouseId)!; me.Age = age;
        h.Treasury.Credits = 100; h.Treasury.Set(Resource.Influence, 9); w.PoolsFor(me.Id).Influence = 9;
        me.AptitudeTraits.Add("Schemer"); me.AptitudeTraits.Add("Diplomat"); me.Skills["intrigue"] = 3; me.Skills["oratory"] = 3; me.Skills["charisma"] = 3; me.CareerRank = 3;
        return w;
    }
    private static Character Add(World w, string houseId, string id, int age, string sex = "female", string track = "law", int rank = 0)
    {
        var c = new Character { Id = id, Name = "Test " + id, HouseId = houseId, Age = age, Sex = sex, CareerTrack = track, CareerRank = rank, Health = 90, Alive = true };
        w.Characters[id] = c; w.House(houseId)!.Members.Add(id); return c;
    }
    private static void Blood(World w, string parent, string child)
    {
        foreach (var (a, b) in new[] { (parent, child), (child, parent) }) { var r = w.Relationship(a, b); r.Bond = BondType.Blood; r.BondStrength = 100; }
    }
    /// <summary>A child of the head and the spouse.</summary>
    private static Character Child(World w, string id, int age)
    {
        var c = Add(w, Mine, id, age); c.FatherId = Me; c.MotherId = Spouse; Blood(w, Me, id); Blood(w, Spouse, id); return c;
    }
    /// <summary>The head of the spouse's family, marked as the courtship system marks them.</summary>
    private static Character InLaw(World w) { var c = Add(w, Theirs, "harrow_elder", 60, "male"); c.Flags.Add(CourtshipSystem.InLawChar); return c; }

    private static (EventDef def, Binding binding) Find(World w, string id)
    {
        var all = new EventEngine(Content, WorldBuilder.RngFor(w)).GatherEligible(w);
        var hit = all.Where(e => e.def.Id == id).ToList();
        Assert.True(hit.Count == 1, $"{id} not eligible; eligible were: {string.Join(", ", all.Select(e => e.def.Id))}");
        return hit[0];
    }
    private static bool IsEligible(World w, string id) => new EventEngine(Content, WorldBuilder.RngFor(w)).GatherEligible(w).Any(e => e.def.Id == id);

    /// <summary>For each choice of the report: a fresh world, the report eligible in it, the choice open, and taken without error with a
    /// line written to the chronicle.</summary>
    private static void EveryChoice(string id, Func<World> build, Action<World, Binding, string>? after = null)
    {
        var def = Content.Events.Single(e => e.Id == id);
        Assert.True(def.Choices.Count is >= 2 and <= 4, $"{id} has {def.Choices.Count} choices");
        Assert.Contains(def.Choices, c => c.Requirements.Count == 0);
        foreach (var c in def.Choices)
        {
            var w = build(); var (d, b) = Find(w, id);
            var engine = new EventEngine(Content, WorldBuilder.RngFor(w)); var fired = new FiredEvent(d, b);
            Assert.True(engine.IsChoiceAvailable(fired, w, c.Id), $"{id}/{c.Id} is not open");
            int before = w.History.Count;
            engine.Resolve(fired, w, c.Id);
            Assert.True(w.History.Count > before, $"{id}/{c.Id} wrote nothing to the chronicle");
            after?.Invoke(w, b, c.Id);
        }
    }
    private static Character Who(World w, Binding b, string role) => w.Char(b.Resolve(role))!;

    // ---------------- births and children ----------------

    [Fact]
    public void TheNamingOfAChild_IsAChildOfTheMarriage_AndComesOncePerSpouse()
    {
        World Born() { var w = Base(); Child(w, "babe", 0); w.WorldFlags.Add(DynastySystem.BornFlag); return w; }
        EveryChoice("the_naming_of_a_child", Born, (w, b, choice) =>
        {
            Assert.Equal("babe", b.Resolve("child")); Assert.Equal(Spouse, b.Resolve("spouse"));
            Assert.Contains("named_a_child", Who(w, b, "spouse").Flags);
            Assert.False(IsEligible(w, "the_naming_of_a_child"));                                          // once a marriage
        });
        var none = Base(); Child(none, "babe", 0); Assert.False(IsEligible(none, "the_naming_of_a_child"));   // not without the birth
        var older = Born(); older.Char("babe")!.Age = 3; Assert.False(IsEligible(older, "the_naming_of_a_child"));
    }

    [Fact]
    public void AGrandchild_IsAChildOfYourChild_NotOfYourSister()
    {
        World Born()
        {
            var w = Base(50); var son = Child(w, "son", 25); son.Sex = "male";
            var babe = Add(w, Mine, "grandbabe", 0); babe.FatherId = son.Id; Blood(w, son.Id, babe.Id); babe.Flags.Add(DynastySystem.GrandchildChar);
            w.WorldFlags.Add(DynastySystem.BornFlag); return w;
        }
        EveryChoice("a_grandchild_is_born", Born, (w, b, _) =>
        {
            Assert.Equal("son", b.Resolve("parent")); Assert.Equal("grandbabe", b.Resolve("grandchild")); Assert.False(IsEligible(w, "a_grandchild_is_born"));   // once for each head
            w.Protagonist!.Flags.Remove("had_a_grandchild"); Assert.False(IsEligible(w, "a_grandchild_is_born"));         // and once for each grandchild
            var next = Add(w, Mine, "grandbabe2", 0); next.FatherId = "son"; Blood(w, "son", next.Id); next.Flags.Add(DynastySystem.GrandchildChar);
            Assert.True(IsEligible(w, "a_grandchild_is_born"));                                                            // a head who has not had one has theirs
        });
        // the sister's child is blood of one bound to the head by blood, and no grandchild
        var w2 = Base(50); var niece = Add(w2, Mine, "niece", 0); niece.MotherId = Sister; Blood(w2, Sister, niece.Id); w2.WorldFlags.Add(DynastySystem.BornFlag);
        Assert.False(IsEligible(w2, "a_grandchild_is_born"));
    }

    [Fact]
    public void ABirth_IsMarked_AndAGrandchildByTheParentsIds()
    {
        var w = Base(40); var me = w.Protagonist!; var senna = w.Char(Spouse)!; senna.Age = 30;
        var son = Child(w, "son", 25); son.Sex = "male"; var wife = Add(w, Theirs, "son_wife", 24); CourtshipSystem.Wed(w, son, wife, CourtshipSystem.Love);
        var niece_father = Add(w, Theirs, "mira_husband", 30, "male"); var mira = w.Char(Sister)!; mira.Age = 30; CourtshipSystem.Wed(w, mira, niece_father, CourtshipSystem.Love);
        new DynastySystem { Content = Content }.Tick(w, new Fixed(0.0001));
        Assert.True(w.HasFlag(DynastySystem.BornFlag));
        var born = w.Characters.Values.Where(c => c.Age == 0).ToList();
        Assert.Contains(born, c => c.FatherId == son.Id && c.Flags.Contains(DynastySystem.GrandchildChar));
        Assert.Contains(born, c => c.MotherId == mira.Id && !c.Flags.Contains(DynastySystem.GrandchildChar));   // the sister's child is a niece
        // the mark and the grandchild's flag are struck out a year after
        w.Turn += 2;
        new DynastySystem { Content = Content }.Tick(w, new Fixed(0.9999));
        Assert.False(w.HasFlag(DynastySystem.BornFlag)); Assert.DoesNotContain(w.Characters.Values, c => c.Flags.Contains(DynastySystem.GrandchildChar));
    }

    [Fact]
    public void TheSchoolroomAndSixteen_AreMarkedForThePlayersHouseOnly()
    {
        var w = Base(); Child(w, "five", 5); Child(w, "fifteen", 15);
        new DynastySystem { Content = Content }.Tick(w, new Fixed(0.9999));
        Assert.True(w.HasFlag(DynastySystem.SchoolingFlag)); Assert.True(w.HasFlag(DynastySystem.KinOfAgeFlag)); Assert.Contains(DynastySystem.ComeOfAgeFlag, w.Char("fifteen")!.Flags);
        var other = Base(); Add(other, Theirs, "their_five", 5); Add(other, Theirs, "their_fifteen", 15);
        new DynastySystem { Content = Content }.Tick(other, new Fixed(0.9999));
        Assert.False(other.HasFlag(DynastySystem.SchoolingFlag)); Assert.False(other.HasFlag(DynastySystem.KinOfAgeFlag));
    }

    [Fact]
    public void TheOtherFamilyAsksForTheChild_AndTheFosteredChildComesHomeAtSixteen()
    {
        World Six() { var w = Base(); Child(w, "six", 6); InLaw(w); w.WorldFlags.Add(DynastySystem.SchoolingFlag); return w; }
        EveryChoice("the_other_family_asks_for_the_child", Six, (w, b, choice) =>
        {
            var c = Who(w, b, "child"); Assert.Equal("harrow_elder", b.Resolve("in_law"));
            bool goes = choice is "let_them_go" or "ask_something_in_return";
            Assert.Equal(goes, c.Flags.Contains(DynastySystem.HiddenFlag)); Assert.Equal(goes, c.Flags.Contains("fostered"));
        });
        var noLaw = Base(); Child(noLaw, "six", 6); noLaw.WorldFlags.Add(DynastySystem.SchoolingFlag); Assert.False(IsEligible(noLaw, "the_other_family_asks_for_the_child"));   // no in-law, no offer
        // ten years on: home, and the coming of age is this report's (it clears the engine's flag)
        World Home() { var w = Base(); var c = Child(w, "back", 16); c.Flags.Add("fostered"); c.Flags.Add(DynastySystem.ComeOfAgeFlag); w.WorldFlags.Add(DynastySystem.KinOfAgeFlag); return w; }
        Assert.False(IsEligible(Home(), "a_life_of_their_own"));                                                // the fostered child has this one instead
        EveryChoice("the_fostered_child_comes_home", Home, (w, b, _) =>
        {
            var c = Who(w, b, "child"); Assert.DoesNotContain("fostered", c.Flags); Assert.DoesNotContain(DynastySystem.ComeOfAgeFlag, c.Flags);
        });
    }

    [Fact]
    public void ALifeOfTheirOwn_IsTheYearTheyTurnSixteen_AndTheDoubtComesBackWhenTheyHaveWhatTheyWanted()
    {
        World Sixteen() { var w = Base(); var c = Child(w, "kid", 16); c.Flags.Add(DynastySystem.ComeOfAgeFlag); c.Flags.Add(DynastySystem.OwnRoadChar); w.WorldFlags.Add(DynastySystem.KinOfAgeFlag); return w; }
        Assert.False(IsEligible(Sixteen(), "the_coming_of_age"));                                                   // one coming-of-age card for one child of sixteen
        var parentsRoad = Sixteen(); parentsRoad.Char("kid")!.Flags.Remove(DynastySystem.OwnRoadChar);
        Assert.False(IsEligible(parentsRoad, "a_life_of_their_own")); Assert.True(IsEligible(parentsRoad, "the_coming_of_age"));   // their parents' road is the old card's
        EveryChoice("a_life_of_their_own", Sixteen, (w, b, choice) =>
        {
            var kid = Who(w, b, "kid"); Assert.Equal("kid", kid.Id); Assert.DoesNotContain(DynastySystem.ComeOfAgeFlag, kid.Flags);
            Assert.Equal(choice == "the_wrong_road", kid.Flags.Contains("doubted_by_the_head"));
            if (choice == "write_to_someone_in_that_service") Assert.Equal(1, kid.CareerRank);
        });
        var late = Sixteen(); late.Char("kid")!.Age = 17; Assert.False(IsEligible(late, "a_life_of_their_own"));
        World Fulfilled() { var w = Base(); var c = Child(w, "kid", 24); c.Flags.Add("doubted_by_the_head"); c.Flags.Add(AmbitionSystem.FulfilledChar); w.WorldFlags.Add(AmbitionSystem.FulfilledFlag); return w; }
        EveryChoice("you_were_wrong_about_them", Fulfilled, (w, b, _) => Assert.DoesNotContain("doubted_by_the_head", Who(w, b, "kid").Flags));
        var blessed = Fulfilled(); blessed.Char("kid")!.Flags.Remove("doubted_by_the_head"); Assert.False(IsEligible(blessed, "you_were_wrong_about_them"));
    }

    // ---------------- brothers and sisters ----------------

    [Fact]
    public void LeftBehind_IsTheOneWhoHasNot_AndStandingTogetherComesBackWhenOneFails()
    {
        World Fulfilled()
        {
            var w = Base(45); w.Char(Sister)!.Age = 60;   // out of the reckoning: the two are the head's children
            var a = Child(w, "elder", 24); a.Flags.Add(AmbitionSystem.FulfilledChar); Child(w, "younger", 21); w.WorldFlags.Add(AmbitionSystem.FulfilledFlag); return w;
        }
        EveryChoice("kin_left_behind", Fulfilled, (w, b, choice) =>
        {
            var winner = Who(w, b, "winner"); var other = Who(w, b, "other"); Assert.Equal("elder", winner.Id); Assert.Equal("younger", other.Id);
            Assert.Contains("heard_left_behind", other.Flags); Assert.False(IsEligible(w, "kin_left_behind"));
            Assert.Equal(choice == "stand_with_the_one_who_has_it", other.Flags.Contains("slighted"));
            Assert.Equal(choice == "tell_them_to_stand_together", winner.Flags.Contains("stood_together") && other.Flags.Contains("stood_together"));
            if (choice == "tell_them_to_stand_together") Assert.Equal(10, w.GetRelationship(other.Id, winner.Id)!.Disposition);
        });
        // a parent and a child of the head are not two who were left behind by each other
        var w2 = Base(45); w2.Char(Sister)!.Age = 60; var p = Child(w2, "parent_kid", 34); p.Flags.Add(AmbitionSystem.FulfilledChar); var g = Child(w2, "its_child", 17); Blood(w2, p.Id, g.Id);
        w2.WorldFlags.Add(AmbitionSystem.FulfilledFlag); Assert.False(IsEligible(w2, "kin_left_behind"));
        World Failed()
        {
            var w = Base(45); var a = Child(w, "fallen", 24); a.Flags.Add(AmbitionSystem.FailedChar); a.Flags.Add("stood_together"); Child(w, "helper", 21).Flags.Add("stood_together");
            w.Relationship("helper", "fallen").Disposition = 10; w.WorldFlags.Add(AmbitionSystem.FailedFlag); return w;
        }
        EveryChoice("kin_stand_together", Failed, (w, b, _) =>
        {
            Assert.Equal("fallen", b.Resolve("fallen")); Assert.Equal("helper", b.Resolve("helper"));
            Assert.DoesNotContain("stood_together", Who(w, b, "fallen").Flags); Assert.DoesNotContain("stood_together", Who(w, b, "helper").Flags);
        });
        // years of wanting and coming no nearer is a bad time too; a failure that is old news is not
        var thwarted = Failed(); var f = thwarted.Char("fallen")!; f.Flags.Remove(AmbitionSystem.FailedChar); f.Flags.Add(AmbitionSystem.ThwartedChar);
        thwarted.WorldFlags.Remove(AmbitionSystem.FailedFlag); thwarted.WorldFlags.Add(AmbitionSystem.ThwartedFlag); Assert.True(IsEligible(thwarted, "kin_stand_together"));
        var stale = Failed(); stale.WorldFlags.Remove(AmbitionSystem.FailedFlag); Assert.False(IsEligible(stale, "kin_stand_together"));
        var alone = Failed(); alone.Char("helper")!.Flags.Remove("stood_together"); Assert.False(IsEligible(alone, "kin_stand_together"));
        var cold = Failed(); cold.Relationship("helper", "fallen").Disposition = -5; Assert.False(IsEligible(cold, "kin_stand_together"));   // one who does not think well of them is not the one who came
    }

    // ---------------- the spouse and the in-laws ----------------

    [Fact]
    public void TheInLaw_IsTheHeadOfTheSpousesFamily_AndTheMarriageIsRememberedEveryTenthYear()
    {
        var w = Base(); var senna = w.Char(Spouse)!; senna.BornHouseId = Theirs;   // she lives in the player's house and was born to Harrow
        var harrowHead = Economy.HeadOf(w, w.House(Theirs)!)!;
        var m = w.MarriageOf(Me)!; m.Since = w.Turn - 10;
        CourtshipSystem.Tick(w);
        Assert.Contains(CourtshipSystem.InLawChar, harrowHead.Flags); Assert.True(w.HasFlag(CourtshipSystem.RememberedFlag));
        Assert.Single(w.Characters.Values, c => c.Flags.Contains(CourtshipSystem.InLawChar));
        // the next year: no remembering, and no in-law once there is no marriage
        w.Turn++; var r1 = w.Relationship(Me, Spouse); r1.Bond = BondType.None; var r2 = w.Relationship(Spouse, Me); r2.Bond = BondType.None;
        w.Turn++; CourtshipSystem.Tick(w);
        Assert.DoesNotContain(CourtshipSystem.InLawChar, harrowHead.Flags); Assert.False(w.HasFlag(CourtshipSystem.RememberedFlag));
        // a spouse of the player's own house, born to it, has no other family
        var same = Base(); CourtshipSystem.Tick(same); Assert.DoesNotContain(same.Characters.Values, c => c.Flags.Contains(CourtshipSystem.InLawChar));
        var nine = Base(); nine.MarriageOf(Me)!.Since = nine.Turn - 9; CourtshipSystem.Tick(nine); Assert.False(nine.HasFlag(CourtshipSystem.RememberedFlag));
    }

    [Fact]
    public void TheWeddingRemembered_MovesTheMarriage_AndComesOnceForTheYear()
    {
        World Day() { var w = Base(); w.WorldFlags.Add(CourtshipSystem.RememberedFlag); w.WorldFlags.Add(CourtshipSystem.ColdFlag); return w; }
        EveryChoice("the_wedding_remembered", Day, (w, b, choice) =>
        {
            Assert.False(w.HasFlag(CourtshipSystem.RememberedFlag));
            var fresh = Day(); int warmth = fresh.MarriageOf(Me)!.Warmth;
            int now = w.MarriageOf(Me)!.Warmth;
            if (choice == "a_day_like_any_other") Assert.True(now < warmth); else Assert.True(now > warmth);
        });
        var warm = Day(); warm.WorldFlags.Remove(CourtshipSystem.ColdFlag); var (d, bb) = Find(warm, "the_wedding_remembered");
        Assert.False(new EventEngine(Content, WorldBuilder.RngFor(warm)).IsChoiceAvailable(new FiredEvent(d, bb), warm, "say_what_has_gone_wrong"));   // only in a cold marriage
    }

    [Fact]
    public void ASpousesAmbition_IsSixYearsInOneRank_AndSpeakingForThemIsAStep()
    {
        World Stuck()
        {
            var w = Base(); var s = w.Char(Spouse)!; w.House(Mine)!.Members.Remove(s.Id); s.HouseId = Theirs; w.House(Theirs)!.Members.Add(s.Id);   // a spouse of another house: the engine runs only the house's own people's ambitions
            s.NatureTraits.Add("Ambitious"); s.CareerRank = 2; s.RankSeen = 2; s.RankTurn = w.Turn - 9; return w;
        }
        EveryChoice("a_spouses_ambition", Stuck, (w, b, choice) =>
        {
            var s = Who(w, b, "spouse"); Assert.Contains("spouse_ambition_heard", s.Flags); Assert.False(IsEligible(w, "a_spouses_ambition"));
            Assert.Equal(choice is "speak_for_them" or "put_your_own_name_to_it" ? 3 : 2, s.CareerRank);
        });
        var young = Stuck(); young.Char(Spouse)!.RankTurn = young.Turn - 5; Assert.False(IsEligible(young, "a_spouses_ambition"));
        var meek = Stuck(); meek.Char(Spouse)!.NatureTraits.Remove("Ambitious"); Assert.False(IsEligible(meek, "a_spouses_ambition"));
        var ours = Base(); var o = ours.Char(Spouse)!; o.NatureTraits.Add("Ambitious"); o.CareerRank = 2; o.RankSeen = 2; o.RankTurn = ours.Turn - 9; Assert.False(IsEligible(ours, "a_spouses_ambition"));   // of the house: the house's own
    }

    [Fact]
    public void BetweenTwoFamilies_IsASpouseWhoseFamilyIsAtWarWithYours_AndCanCarryAWhitePeace()
    {
        // the usual case: the spouse married into your house, and the house they were born to is at war with yours; its head is the in-law
        World AtWar()
        {
            var w = Base(); var s = w.Char(Spouse)!; s.HouseId = Mine; s.BornHouseId = Theirs; InLaw(w);
            w.Wars.Add(new War { HouseA = Mine, HouseB = Theirs, Since = 9 });
            foreach (var c in w.LivingCharacters().Where(c => c.HouseId == Theirs)) c.Flags.Add(FleetSystem.SuingForPeaceFlag);   // they would take a white peace
            return w;
        }
        EveryChoice("between_two_families", AtWar, (w, b, choice) =>
        {
            Assert.Equal(Spouse, b.Resolve("spouse")); Assert.Equal("harrow_elder", b.Resolve("in_law"));
            Assert.Contains("torn_between", Who(w, b, "spouse").Flags); Assert.False(IsEligible(w, "between_two_families"));
            Assert.Equal(choice != "ask_them_to_carry_a_peace", w.AtWar(Mine, Theirs));
        });
        // the spouse's own house is yours, so the old test of the spouse's house at war could never hold here
        var w0 = AtWar(); Assert.False(w0.AtWar(w0.Char(Spouse)!.HouseId, Mine));
        // and it is the war with their family that counts: courtship's mark on the head of the house they were born to
        var synced = AtWar(); synced.Char("harrow_elder")!.Flags.Remove(CourtshipSystem.InLawChar); Assert.False(IsEligible(synced, "between_two_families"));
        var peace = Base(); peace.Char(Spouse)!.BornHouseId = Theirs; InLaw(peace); Assert.False(IsEligible(peace, "between_two_families"));   // not without the war
    }

    // ---------------- illness, old age and the seat ----------------

    private static World Ill(int age, string keeperFlag = "")
    {
        var w = Base(age); w.Protagonist!.Flags.Add(LifeSystem.IllChar); w.WorldFlags.Add(LifeSystem.IllFlag);
        w.Char(Sister)!.Age = age - 20; var heir = Child(w, "heir", 30);
        foreach (var c in w.LivingCharacters().Where(c => c.HouseId == Mine && c.Id != heir.Id && c.Id != Me)) c.Age = Math.Min(c.Age, 15);   // the heir is the one grown relative
        if (keeperFlag.Length > 0) heir.Flags.Add(keeperFlag);
        return w;
    }

    [Fact]
    public void WhileYouWereIll_TheKeeperIsNotTheSpouse_AndIsMarkedForTheNextStep()
    {
        EveryChoice("while_you_were_ill", () => Ill(45), (w, b, choice) =>
        {
            Assert.Equal("heir", b.Resolve("keeper")); Assert.Equal(choice != "take_it_all_back", Who(w, b, "keeper").Flags.Contains("kept_the_house"));
        });
        var well = Ill(45); well.WorldFlags.Remove(LifeSystem.IllFlag); Assert.False(IsEligible(well, "while_you_were_ill"));
        var spouseOnly = Ill(45); spouseOnly.Char("heir")!.Age = 12; spouseOnly.Char(Spouse)!.Age = 40; Assert.False(IsEligible(spouseOnly, "while_you_were_ill"));   // the spouse does not keep the house in a report
        var elder = Ill(45); elder.Char("heir")!.Flags.Add(LifeSystem.RetiredChar); Assert.False(IsEligible(elder, "while_you_were_ill"));          // nor one who has laid the house down
    }

    [Fact]
    public void TheyAskYouToStandAside_AndLayingItDownHandsTheSeatOn()
    {
        EveryChoice("they_ask_you_to_stand_aside", () => Ill(58, "kept_the_house"), (w, b, choice) =>
        {
            var keeper = Who(w, b, "keeper"); var old = w.Char(Me)!; Assert.DoesNotContain("kept_the_house", keeper.Flags);
            if (choice == "lay_the_house_down") { Assert.Equal(keeper.Id, w.ProtagonistId); Assert.Contains(LifeSystem.RetiredChar, old.Flags); Assert.Equal(old.Id, LifeSystem.ElderOf(w, w.House(Mine)!)!.Id); }
            else Assert.Equal(Me, w.ProtagonistId);
            Assert.Equal(choice == "name_a_day", keeper.Flags.Contains("promised_the_seat"));
            Assert.Equal(choice == "not_while_you_live", keeper.Flags.Contains("slighted"));
        });
        var young = Ill(50, "kept_the_house"); Assert.False(IsEligible(young, "they_ask_you_to_stand_aside"));
        var hale = Ill(58, "kept_the_house"); hale.WorldFlags.Remove(LifeSystem.IllFlag); Assert.False(IsEligible(hale, "they_ask_you_to_stand_aside"));
        var old = Ill(63, "kept_the_house"); old.WorldFlags.Remove(LifeSystem.IllFlag); Assert.True(IsEligible(old, "they_ask_you_to_stand_aside"));
    }

    [Fact]
    public void TheDayYouNamed_KeepsTheWordOrLeavesTheKeeperSlighted_ThroughTheYearsTurn()
    {
        EveryChoice("the_day_you_named", () => Ill(61, "promised_the_seat"), (w, b, choice) =>
        {
            var keeper = Who(w, b, "keeper");
            Assert.Equal(choice == "keep_your_word", w.ProtagonistId == keeper.Id);
            Assert.Equal(choice == "one_more_year", keeper.Flags.Contains("promised_the_seat"));
            Assert.Equal(choice == "break_it", keeper.Flags.Contains("slighted"));
            if (choice != "break_it") return;
            // the family's own turn strikes out a defiance a regard of nought or more does not hold; a slight it leaves for the_slighted_kinsman
            w.FamilyAutonomy = 1; new FamilySystem().Tick(w); Assert.Contains("slighted", keeper.Flags); Assert.True(IsEligible(w, "the_slighted_kinsman"));
        });
        var hale = Ill(61, "promised_the_seat"); hale.WorldFlags.Remove(LifeSystem.IllFlag); Assert.False(IsEligible(hale, "the_day_you_named"));
        var aged = Ill(68, "promised_the_seat"); aged.WorldFlags.Remove(LifeSystem.IllFlag); Assert.True(IsEligible(aged, "the_day_you_named"));
    }

    // ---------------- the will and the household ----------------

    [Fact]
    public void TheReadingOfTheWill_IsForThosePassedOver_OnceEach()
    {
        World Read() { var w = Base(30); var s = w.Char(Sister)!; s.Flags.Add(WealthSystem.PassedOverAtWillChar); w.Relationship(Sister, Me).Disposition = -5; w.WorldFlags.Add(WealthSystem.WillReadFlag); return w; }
        var older = Read(); var aunt = older.Char(Sister)!; aunt.Flags.Remove(WealthSystem.PassedOverAtWillChar); aunt.Flags.Add("passed_over");
        Assert.False(IsEligible(older, "the_reading_of_the_will"));                                              // passed over at some older succession is not this reading
        EveryChoice("the_reading_of_the_will", Read, (w, b, _) => { Assert.Equal(Sister, b.Resolve("sibling")); Assert.False(IsEligible(w, "the_reading_of_the_will")); });
        var fond = Read(); fond.Relationship(Sister, Me).Disposition = 40; Assert.False(IsEligible(fond, "the_reading_of_the_will"));   // one who thinks well of you does not need it
        var unread = Read(); unread.WorldFlags.Remove(WealthSystem.WillReadFlag); Assert.False(IsEligible(unread, "the_reading_of_the_will"));
    }

    [Fact]
    public void TheWill_MarksTheGrownChildrenItPassedOver_ForAsLongAsItIsNews()
    {
        var w = Base(60); var me = w.Protagonist!; var heir = Child(w, "heir", 30); var other = Child(w, "other", 27); var small = Child(w, "small", 10);
        WealthSystem.Inherit(w, me, heir);
        Assert.True(w.HasFlag(WealthSystem.WillReadFlag));
        Assert.Contains(WealthSystem.PassedOverAtWillChar, other.Flags); Assert.DoesNotContain(WealthSystem.PassedOverAtWillChar, heir.Flags); Assert.DoesNotContain(WealthSystem.PassedOverAtWillChar, small.Flags);
        Assert.DoesNotContain(WealthSystem.PassedOverAtWillChar, w.Char(Sister)!.Flags);                         // the dead head's sister was not passed over by this will
        w.Turn += 2; WealthSystem.Tick(w); Assert.False(w.HasFlag(WealthSystem.WillReadFlag)); Assert.DoesNotContain(WealthSystem.PassedOverAtWillChar, other.Flags);
    }

    [Fact]
    public void OneWhoComesOfAge_BoundForAServiceNeitherParentWasIn_ChoseTheirOwnRoad_ForTheYear()
    {
        var w = Base(); w.Protagonist!.CareerTrack = "law"; w.Char(Spouse)!.CareerTrack = "law";
        var own = Child(w, "own", 15); own.CareerTrack = ""; own.Skills["tactics"] = 3;            // brought up to arms, in a house of the law
        var follows = Child(w, "follows", 15); follows.CareerTrack = "";
        new DynastySystem { Content = Content }.Tick(w, new Fixed(0.9999));
        Assert.Equal("military", own.CareerTrack); Assert.Contains(DynastySystem.OwnRoadChar, own.Flags);
        Assert.Equal("law", follows.CareerTrack); Assert.DoesNotContain(DynastySystem.OwnRoadChar, follows.Flags);
        w.Turn++; new DynastySystem { Content = Content }.Tick(w, new Fixed(0.9999)); Assert.DoesNotContain(DynastySystem.OwnRoadChar, own.Flags);   // of age a year, like anyone
    }

    [Fact]
    public void OneWhoLeavesUnpaid_IsMarked_AndTheScandalNamesThem()
    {
        var w = Base(); var me = w.Protagonist!; var mine = w.House(Mine)!;
        var guest = Add(w, Theirs, "guest", 40, "male"); w.Relationship(guest.Id, me.Id).Disposition = 90; HouseholdSystem.TakeIn(w, me, guest, HouseholdSystem.Companion);
        var household = new HouseholdSystem(Content.Holdings, Content.Businesses, Content.Titles, Content.Trade); var ctx = new CommandContext(w, new Fixed(0.5));
        mine.Treasury.Credits = 0; household.Tick(w, new CommandExecutor(), ctx);
        mine.Treasury.Credits = 0; w.Relationship(guest.Id, me.Id).Disposition = 0; household.Tick(w, new CommandExecutor(), ctx);
        Assert.Equal(Theirs, guest.HouseId); Assert.True(w.HasFlag(HouseholdSystem.LeftUnpaidFlag)); Assert.Contains(HouseholdSystem.LeftUnpaidChar, guest.Flags);
        mine.Treasury.Credits = 100; Assert.True(IsEligible(w, "a_household_scandal"));
        // the flag goes with the mark
        w.Turn += 2; household.Tick(w, new CommandExecutor(), ctx); Assert.DoesNotContain(HouseholdSystem.LeftUnpaidChar, guest.Flags);

        World Talk() { var x = Base(); var g = Add(x, Theirs, "guest", 40, "male"); g.Flags.Add(HouseholdSystem.LeftUnpaidChar); x.WorldFlags.Add(HouseholdSystem.LeftUnpaidFlag); return x; }
        EveryChoice("a_household_scandal", Talk, (x, b, _) => { Assert.DoesNotContain(HouseholdSystem.LeftUnpaidChar, Who(x, b, "leaver").Flags); Assert.False(IsEligible(x, "a_household_scandal")); });
    }

    // ---------------- the words ----------------

    [Fact]
    public void TheWords_KeepTheOwnersRules()
    {
        var path = Path.Combine(ContentLocator.FindContentDir(), "text", "kin.json"); var text = File.ReadAllText(path);
        Assert.DoesNotContain("—", text); Assert.DoesNotContain("(s)", text); Assert.DoesNotContain("!", text);
        foreach (var word in new[] { " he ", " she ", " his ", " her ", " him ", " son ", " daughter ", " it is a boy", " it is a girl" }) Assert.DoesNotContain(word, text.ToLowerInvariant());
        var events = Content.Events.Where(e => text.Contains("\"" + e.Id + "\"")).ToList();
        Assert.Equal(16, events.Count);
        foreach (var e in events) foreach (var c in e.Choices) Assert.Contains(c.Effects, x => x is LogEffect);
    }
}
