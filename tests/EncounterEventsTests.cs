using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Chance meetings (docs/events/chance.md, content/events/encounters.json): the people of the Reach crossing the player's path.
/// For each report, a world where the person it needs is there, the report eligible, and every choice applied with what it does; and the
/// four threads carried from the first step to the second.</summary>
public class EncounterEventsTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());

    private static (World w, Character me, House mine, House theirs) Start(string scenario = "the_old_lion", int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        w.Turn = 10; var me = w.Protagonist!; var mine = w.House(me.HouseId)!;
        mine.Treasury.Credits = 100; mine.Treasury.Set(Resource.Influence, 9); w.PoolsFor(me.Id).Influence = 9;
        var theirs = w.Houses.Values.Where(h => h.Id != me.HouseId && !World.Outsider(h.Id) && w.HoldingsOf(h.Id).Any(x => x.IsPlanet)).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        return (w, me, mine, theirs);
    }

    private static Character Add(World w, House h, string id, int age = 40, string track = "", int rank = 0, params string[] natures)
    {
        var c = new Character { Id = id, Name = "Test " + id, HouseId = h.Id, Age = age, Sex = "female", CareerTrack = track, CareerRank = rank, Health = 90 };
        foreach (var n in natures) c.NatureTraits.Add(n);
        w.Characters[id] = c; h.Members.Add(id); return c;
    }

    private static void Regard(World w, Character from, Character to, int d) => w.Relationship(from.Id, to.Id).Disposition = d;
    private static int RegardOf(World w, Character from, Character to) => w.GetRelationship(from.Id, to.Id)?.Disposition ?? 0;

    private static bool Eligible(World w, string id) => new EventEngine(Content, WorldBuilder.RngFor(w)).GatherEligible(w).Any(e => e.def.Id == id);

    /// <summary>Does this person fill the role, with the roles before it as given?</summary>
    private static bool Binds(World w, string id, string role, Character c, Binding? before = null)
    {
        var r = Content.Event(id)!.Roles.Single(x => x.Name == role);
        var ctx = new EvalContext(w, (before ?? new Binding(w.ProtagonistId)).With(role, c.Id), WorldBuilder.RngFor(w), Content);
        return r.Constraints.All(k => k.Evaluate(ctx));
    }

    private static FiredEvent Fire(World w, string id, params (string role, Character c)[] roles)
    {
        var b = new Binding(w.ProtagonistId); foreach (var (r, c) in roles) b = b.With(r, c.Id);
        return new FiredEvent(Content.Event(id)!, b);
    }

    private static void Take(World w, FiredEvent f, string choice)
    {
        var engine = new EventEngine(Content, WorldBuilder.RngFor(w));
        Assert.True(engine.IsChoiceAvailable(f, w, choice), $"{f.Def.Id}/{choice} should be open");
        int lines = w.History.Count; engine.Resolve(f, w, choice);
        Assert.True(w.History.Count > lines, $"{f.Def.Id}/{choice} writes nothing to the chronicle");
    }

    private static readonly HashSet<string> OutsideParts = new(StringComparer.Ordinal)
    { "buyer", "rival_house", "envoy", "intruder", "giver", "debtor", "claimant", "grandee", "pretender", "coalition_head", "envious", "friend", "sovereign",
      "classmate", "veteran", "young", "sitter", "subject", "guest", "other_guest" };

    /// <summary>Could the binder have cast these people in these parts: each living, not hidden, grown unless the part asks an age, of
    /// another house where the part is an outsider's, each holding the part's conditions with those cast before, and the event's own
    /// conditions holding with all of them. The same filters as <see cref="Binder.TryBind"/>, for the people the test chose.</summary>
    private static void CouldBind(World w, FiredEvent f)
    {
        var def = f.Def; var b = new Binding(w.ProtagonistId); string mine = w.Protagonist!.HouseId;
        foreach (var role in def.Roles)
        {
            Assert.True(f.Binding.TryGet(role.Name, out var id), $"{def.Id}: nobody cast as {role.Name}"); var c = w.Char(id!)!;
            Assert.True(c.Alive && !c.Flags.Contains(DynastySystem.HiddenFlag), $"{def.Id}: {c.Id} is not to be found");
            Assert.True(c.Age >= DynastySystem.AgeOfMajority || role.Constraints.Any(k => k is AgeCondition a && a.Role == role.Name), $"{def.Id}: {c.Id} is a child");
            Assert.False(OutsideParts.Contains(role.Name) && c.HouseId == mine, $"{def.Id}: {c.Id} is of your house");
            b = b.With(role.Name, c.Id); var ctx = new EvalContext(w, b, WorldBuilder.RngFor(w), Content);
            Assert.True(role.Constraints.All(k => k.Evaluate(ctx)), $"{def.Id}: {c.Id} does not fill {role.Name}");
        }
        var all = new EvalContext(w, b, WorldBuilder.RngFor(w), Content); Assert.True(def.Conditions.All(k => k.Evaluate(all)), $"{def.Id}: its own conditions do not hold");
    }

    /// <summary>Every choice of the event, each on a fresh world made by the same setup: the report is one the engine would gather there,
    /// the people chosen are ones the binder could cast, and the choice applies without error.</summary>
    private static void EveryChoice(string id, Func<(World w, FiredEvent f)> setup)
    {
        foreach (var c in Content.Event(id)!.Choices) { var (w, f) = setup(); Assert.True(Eligible(w, id), $"{id} is not gathered"); CouldBind(w, f); Take(w, f, c.Id); }
    }

    private static void Gifted(Character me)
    {
        foreach (var a in new[] { "Duelist", "Schemer", "Diplomat", "Orator" }) me.AptitudeTraits.Add(a);
        me.NatureTraits.Add("Ruthless");
        foreach (var s in new[] { "intrigue", "oratory", "economics", "diplomacy" }) me.Skills[s] = 3;
    }

    private static readonly string[] Ids = { "a_classmate_in_the_city", "an_old_enemy_across_the_room", "words_at_the_feast", "the_quarrel_comes_to_a_head", "a_veteran_of_the_old_wars",
        "a_painters_great_canvas", "a_merchant_princes_venture", "the_venture_comes_home", "the_venture_goes_down", "a_house_without_lands", "a_favour_repaid",
        "a_secret_for_sale", "the_secret_you_hold", "a_word_off_the_record", "a_patron_wanted", "the_one_you_stood_for", "two_guests_at_odds" };

    [Fact]
    public void TheFile_HasSeventeenReports_EachRepeatable_WithTextAndAnUngatedChoice()
    {
        var ids = Ids;
        foreach (var id in ids)
        {
            var def = Content.Event(id); Assert.NotNull(def); Assert.True(def!.Repeatable, id); Assert.NotEqual(EventTier.Setpiece, def.Tier);
            Assert.Contains(def.Choices, c => c.Requirements.Count == 0);
            var t = Content.Text(id); Assert.NotNull(t);
            foreach (var s in new[] { t!.Title, t.Body }.Concat(t.Choices.Values)) { Assert.DoesNotContain("—", s); Assert.DoesNotContain("(s)", s); Assert.DoesNotContain("!", s); }
        }
    }

    // ---------------- 1. a classmate ----------------

    private static (World w, Character me, Character mate) Classmates()
    {
        var (w, me, _, theirs) = Start(); var mate = Add(w, theirs, "mate", age: 30, track: "law", rank: 2);
        w.Academy = new AcademyState { Active = false, Graduated = true, Cohort = { mate.Id } }; w.WorldFlags.Add(AcademySystem.GraduatedFlag);
        Regard(w, mate, me, 35); Gifted(me); return (w, me, mate);
    }

    [Fact]
    public void AClassmate_IsBoundByTheCohort_AndAWordPutInLeavesThemOwingYou()
    {
        var (w, me, mate) = Classmates();
        Assert.True(Binds(w, "a_classmate_in_the_city", "classmate", mate)); Assert.True(Eligible(w, "a_classmate_in_the_city"));
        var stranger = Add(w, w.House(mate.HouseId)!, "stranger", age: 30); Assert.False(Binds(w, "a_classmate_in_the_city", "classmate", stranger));
        Take(w, Fire(w, "a_classmate_in_the_city", ("classmate", mate)), "put_in_a_word");
        Assert.Contains("owes_you_a_favour", mate.Flags); Assert.Equal(47, RegardOf(w, mate, me));
        // the old friendship sworn again: a bond both ways
        var (w2, me2, mate2) = Classmates(); Take(w2, Fire(w2, "a_classmate_in_the_city", ("classmate", mate2)), "swear_the_old_friendship");
        Assert.Equal(BondType.Sworn, w2.GetRelationship(me2.Id, mate2.Id)!.Bond); Assert.Equal(BondType.Sworn, w2.GetRelationship(mate2.Id, me2.Id)!.Bond);
        EveryChoice("a_classmate_in_the_city", () => { var (x, _, m) = Classmates(); return (x, Fire(x, "a_classmate_in_the_city", ("classmate", m))); });
    }

    [Fact]
    public void AClassmate_IsNotMetWhileTheAcademyIsInSession()
    {
        var (w, _, _) = Classmates(); w.Academy!.Active = true;
        Assert.False(Eligible(w, "a_classmate_in_the_city"));
    }

    // ---------------- 2. an old enemy ----------------

    [Fact]
    public void AnOldEnemy_IsTheOneLifeSystemMarks_AndCrossingTheRoomBringsThemBack()
    {
        var (w, me, _, theirs) = Start(); var enemy = Add(w, theirs, "enemy", age: 50);
        Regard(w, enemy, me, -50); me.Life ??= new Life(); me.Life.Enemies[enemy.Id] = w.Turn - LifeSystem.OldTieYears;
        LifeSystem.Tick(w, Content);   // the engine's own mark: has_old_enemy and old_enemy on the person
        Assert.True(w.HasFlag(LifeSystem.OldEnemyFlag)); Assert.Contains(LifeSystem.OldEnemyChar, enemy.Flags);
        Assert.True(Binds(w, "an_old_enemy_across_the_room", "enemy", enemy)); Assert.True(Eligible(w, "an_old_enemy_across_the_room"));
        int before = RegardOf(w, enemy, me);
        Take(w, Fire(w, "an_old_enemy_across_the_room", ("enemy", enemy)), "cross_the_room");
        Assert.Equal(before + 12, RegardOf(w, enemy, me));
        EveryChoice("an_old_enemy_across_the_room", () =>
        {
            var (x, m, _, t) = Start(); var e = Add(x, t, "enemy", age: 50); e.Flags.Add(LifeSystem.OldEnemyChar); x.WorldFlags.Add(LifeSystem.OldEnemyFlag); Gifted(m);
            return (x, Fire(x, "an_old_enemy_across_the_room", ("enemy", e)));
        });
    }

    // ---------------- 3 and 4. the quarrel ----------------

    private static (World w, Character me, Character guest) Feast()
    {
        var (w, me, _, theirs) = Start(); var guest = Add(w, theirs, "guest", 35, "", 0, "Proud"); Regard(w, guest, me, -20); Gifted(me); return (w, me, guest);
    }

    [Fact]
    public void WordsAtTheFeast_OpenAQuarrel_AndTheQuarrelComesToAHead()
    {
        var (w, me, guest) = Feast();
        Assert.True(Binds(w, "words_at_the_feast", "envious", guest)); Assert.True(Eligible(w, "words_at_the_feast"));
        guest.NatureTraits.Clear(); Assert.False(Binds(w, "words_at_the_feast", "envious", guest)); guest.NatureTraits.Add("Vengeful");
        Assert.False(Binds(w, "the_quarrel_comes_to_a_head", "envious", guest));
        Take(w, Fire(w, "words_at_the_feast", ("envious", guest)), "answer_in_kind");
        Assert.Contains("quarrel_with_you", guest.Flags); Assert.False(Binds(w, "words_at_the_feast", "envious", guest));   // one quarrel at a time
        Assert.True(Binds(w, "the_quarrel_comes_to_a_head", "envious", guest)); Assert.True(Eligible(w, "the_quarrel_comes_to_a_head"));
        Take(w, Fire(w, "the_quarrel_comes_to_a_head", ("envious", guest)), "send_a_gift");
        Assert.DoesNotContain("quarrel_with_you", guest.Flags); Assert.False(Eligible(w, "the_quarrel_comes_to_a_head"));
        // laughing it off opens nothing
        var (w2, _, g2) = Feast(); Take(w2, Fire(w2, "words_at_the_feast", ("envious", g2)), "laugh_it_off"); Assert.DoesNotContain("quarrel_with_you", g2.Flags);
        EveryChoice("words_at_the_feast", () => { var (x, _, g) = Feast(); return (x, Fire(x, "words_at_the_feast", ("envious", g))); });
        EveryChoice("the_quarrel_comes_to_a_head", () => { var (x, _, g) = Feast(); g.Flags.Add("quarrel_with_you"); return (x, Fire(x, "the_quarrel_comes_to_a_head", ("envious", g))); });
    }

    [Fact]
    public void AQuarrelEndedForGood_IsABloodDebtTheFeudCarries()
    {
        var (w, me, guest) = Feast(); guest.Flags.Add("quarrel_with_you");
        Take(w, Fire(w, "the_quarrel_comes_to_a_head", ("envious", guest)), "end_it_for_good");
        Assert.False(guest.Alive);
        var f = Assert.Single(FeudSystem.Open(w)); Assert.Equal(guest.HouseId, f.WrongedId); Assert.Equal(me.HouseId, f.OwingId); Assert.Equal(me.Id, f.BlamedId);
        Assert.True(w.HasFlag(FeudSystem.WeOweFlag)); Assert.True(Eligible(w, "blood_on_our_hands"));
        // and it is closed to one who is neither ruthless nor cruel
        var (w2, me2, g2) = Feast(); me2.NatureTraits.Clear(); g2.Flags.Add("quarrel_with_you");
        Assert.False(new EventEngine(Content, WorldBuilder.RngFor(w2)).IsChoiceAvailable(Fire(w2, "the_quarrel_comes_to_a_head", ("envious", g2)), w2, "end_it_for_good"));
    }

    // ---------------- 5. a veteran ----------------

    [Fact]
    public void AVeteranOfTheOldWars_IsAnOldOfficerWithoutACommand_AndMayJoinTheHousehold()
    {
        (World, Character, Character) Setup() { var (w, me, _, theirs) = Start(); var vet = Add(w, theirs, "vet", 64, "military", 5); Add(w, theirs, "vet_elder", 90); Regard(w, vet, me, 25); return (w, me, vet); }   // an elder heads the house: the veteran is free to come
        var (w, me, vet) = Setup();
        Assert.True(Binds(w, "a_veteran_of_the_old_wars", "veteran", vet)); Assert.True(Eligible(w, "a_veteran_of_the_old_wars"));
        vet.Flags.Add(FleetSystem.AdmiralFlag); Assert.False(Binds(w, "a_veteran_of_the_old_wars", "veteran", vet)); vet.Flags.Remove(FleetSystem.AdmiralFlag);
        Take(w, Fire(w, "a_veteran_of_the_old_wars", ("veteran", vet)), "ask_them_to_join_the_household");
        Assert.Equal(me.HouseId, vet.HouseId); Assert.Contains(HouseholdSystem.Companion, vet.Flags);
        var (w2, _, v2) = Setup(); int officers = w2.Counter("estate_offset:officers");
        Take(w2, Fire(w2, "a_veteran_of_the_old_wars", ("veteran", v2)), "ask_them_to_speak_for_you"); Assert.Equal(officers + 4, w2.Counter("estate_offset:officers"));
        EveryChoice("a_veteran_of_the_old_wars", () => { var (x, _, v) = Setup(); return (x, Fire(x, "a_veteran_of_the_old_wars", ("veteran", v))); });
    }

    [Fact]
    public void AReportsOfferOfAPlace_DoesNotTakeTheHeadOfAHouse_OrOneMarriedIntoIt()
    {
        var (w, me, _, theirs) = Start(); var head = Economy.HeadOf(w, theirs)!; Regard(w, head, me, 60);
        Take(w, Fire(w, "a_veteran_of_the_old_wars", ("veteran", head)), "ask_them_to_join_the_household");
        Assert.Equal(theirs.Id, head.HouseId); Assert.Same(head, Economy.HeadOf(w, theirs));   // the head stays with their house, and the house keeps its head
        Assert.Contains(w.History, h => h.Text.Contains(head.Name + " thanked"));                  // and the chronicle says they did not come
        Assert.Equal("the head of a house does not leave it", HouseholdSystem.HardRefusal(w, me, head));
        var (w2, me2, _, t2) = Start(); var wed = Add(w2, t2, "wed", 40); var spouse = Add(w2, t2, "wed_spouse", 40); CourtshipSystem.Wed(w2, wed, spouse, CourtshipSystem.Love);
        Regard(w2, wed, me2, 60); Take(w2, Fire(w2, "a_veteran_of_the_old_wars", ("veteran", wed)), "ask_them_to_join_the_household");
        Assert.Equal(t2.Id, wed.HouseId);
    }

    // ---------------- 6. the painter ----------------

    [Fact]
    public void APaintersGreatCanvas_HasARivalInIt()
    {
        (World, Character, Character) Setup() { var (w, me, _, theirs) = Start(); var rival = Add(w, theirs, "rival", 45); Regard(w, rival, me, -20); Gifted(me); return (w, me, rival); }
        var (w, me, rival) = Setup();
        Assert.True(Binds(w, "a_painters_great_canvas", "sitter", rival)); Assert.True(Eligible(w, "a_painters_great_canvas"));
        var friend = Add(w, w.House(rival.HouseId)!, "friendly", 45); Regard(w, friend, me, 20); Assert.False(Binds(w, "a_painters_great_canvas", "sitter", friend));
        Take(w, Fire(w, "a_painters_great_canvas", ("sitter", rival)), "stand_beside_them"); Assert.Equal(-12, RegardOf(w, rival, me));
        EveryChoice("a_painters_great_canvas", () => { var (x, _, r) = Setup(); return (x, Fire(x, "a_painters_great_canvas", ("sitter", r))); });
    }

    // ---------------- 7, 8 and 9. the venture ----------------

    private static (World w, Character me, Character prince, House theirs) Merchant()
    {
        var (w, me, _, theirs) = Start(); var prince = Add(w, theirs, "prince", 50); theirs.Wealth = 1_000_000; theirs.Treasury.Credits = Math.Max(20, theirs.Treasury.Credits); Gifted(me); return (w, me, prince, theirs);
    }

    [Fact]
    public void AMerchantPrincesVenture_PaysWhileTheirHouseIsRich_AndGoesDownWhenItIsNot()
    {
        var (w, _, prince, theirs) = Merchant(); var mine = w.House(w.Protagonist!.HouseId)!;
        Assert.Equal("opulent", Wealth.TierFor(w, theirs.Id));
        Assert.True(Binds(w, "a_merchant_princes_venture", "grandee", prince)); Assert.True(Eligible(w, "a_merchant_princes_venture"));
        int credits = mine.Treasury.Credits;
        Take(w, Fire(w, "a_merchant_princes_venture", ("grandee", prince)), "put_money_in");
        Assert.Equal(credits - 8, mine.Treasury.Credits); Assert.Contains("staked_by_you", prince.Flags);
        Assert.False(Binds(w, "a_merchant_princes_venture", "grandee", prince));
        Assert.True(Binds(w, "the_venture_comes_home", "grandee", prince)); Assert.False(Binds(w, "the_venture_goes_down", "grandee", prince));
        Exotics(w, 1.0); Assert.False(Eligible(w, "the_venture_comes_home"));                       // it comes home in a year exotics sell a tenth dearer
        w.WorldFlags.Add("price_spike"); Assert.False(Eligible(w, "the_venture_comes_home"));       // some other good's spike is not the venture's market
        Exotics(w, 1.12); Assert.True(Eligible(w, "the_venture_comes_home")); Assert.False(Binds(w, "the_venture_goes_down", "grandee", prince));
        Take(w, Fire(w, "the_venture_comes_home", ("grandee", prince)), "take_your_share");
        Assert.Equal(credits + 4, mine.Treasury.Credits); Assert.DoesNotContain("staked_by_you", prince.Flags);
        // their house falls out of the rich: the stake goes down instead
        var (w2, _, p2, t2) = Merchant(); p2.Flags.Add("staked_by_you"); t2.Wealth = -1;
        Assert.False(Binds(w2, "the_venture_comes_home", "grandee", p2)); Assert.True(Binds(w2, "the_venture_goes_down", "grandee", p2)); Assert.True(Eligible(w2, "the_venture_goes_down"));
        // or their house goes to war with yours, however rich it is
        var (w3, me3, p3, t3) = Merchant(); p3.Flags.Add("staked_by_you");
        new FleetSystem(Content.Fleets).DeclareWar(w3, me3.HouseId, t3.Id, me3.Id);
        Assert.False(Binds(w3, "the_venture_comes_home", "grandee", p3)); Assert.True(Binds(w3, "the_venture_goes_down", "grandee", p3));
        // or exotics fall a tenth on the year, however rich they are (and then it cannot come home too); and a house merely rich no longer pays it home
        var (w4, _, p4, t4) = Merchant(); p4.Flags.Add("staked_by_you"); w4.WorldFlags.Add("price_slump"); Exotics(w4, 1.0); Assert.False(Binds(w4, "the_venture_goes_down", "grandee", p4));
        Exotics(w4, 0.88); Assert.True(Binds(w4, "the_venture_goes_down", "grandee", p4)); Assert.False(Eligible(w4, "the_venture_comes_home"));
        // the grandee's house pays what is pressed out of it
        int had = t4.Treasury.Credits; Take(w4, Fire(w4, "the_venture_goes_down", ("grandee", p4)), "press_for_it"); Assert.Equal(had - 3, t4.Treasury.Credits);
        var (w5, _, p5, t5) = Merchant(); p5.Flags.Add("staked_by_you"); t5.Wealth = 0; Exotics(w5, 1.12);
        if (Wealth.TierFor(w5, t5.Id) != "opulent") Assert.False(Binds(w5, "the_venture_comes_home", "grandee", p5));
        EveryChoice("a_merchant_princes_venture", () => { var (x, _, p, _) = Merchant(); return (x, Fire(x, "a_merchant_princes_venture", ("grandee", p))); });
        EveryChoice("the_venture_comes_home", () => { var (x, _, p, _) = Merchant(); p.Flags.Add("staked_by_you"); Exotics(x, 1.12); return (x, Fire(x, "the_venture_comes_home", ("grandee", p))); });
        EveryChoice("the_venture_goes_down", () => { var (x, _, p, t) = Merchant(); p.Flags.Add("staked_by_you"); t.Wealth = -1; return (x, Fire(x, "the_venture_goes_down", ("grandee", p))); });
    }

    /// <summary>Exotics this year at this much of last year's price.</summary>
    private static void Exotics(World w, double ratio)
    {
        w.Market ??= new MarketState(); if (w.Market.Good("exotics") is not { } g) { g = new GoodState(); w.Market.Goods["exotics"] = g; }
        g.History.Clear(); g.History.Add(7.0); g.History.Add(7.0 * ratio); g.Price = 7.0 * ratio;
    }

    // ---------------- 10 and 11. a favour owed ----------------

    private static (World w, Character me, Character poor) Landless()
    {
        var (w, me, _, _) = Start();
        var h = new House { Id = "test_landless", Name = "House Testless" }; h.Treasury.Credits = 10; w.Houses[h.Id] = h;
        var poor = Add(w, h, "poor", 45); Add(w, h, "poor_elder", 80); Gifted(me); return (w, me, poor);   // the house's head is the elder: the claimant is free to come
    }

    [Fact]
    public void AHouseWithoutLands_LendsAFavour_ThatIsRepaid()
    {
        var (w, me, poor) = Landless();
        Assert.True(Binds(w, "a_house_without_lands", "claimant", poor)); Assert.True(Eligible(w, "a_house_without_lands"));
        var (_, _, _, theirs) = Start(); var landed = Add(w, w.House(theirs.Id)!, "landed", 45); Assert.False(Binds(w, "a_house_without_lands", "claimant", landed));
        int oldBlood = w.Counter("estate_offset:old_blood");
        Take(w, Fire(w, "a_house_without_lands", ("claimant", poor)), "lend_them_coin");
        Assert.Contains("owes_you_a_favour", poor.Flags); Assert.Equal(oldBlood + 2, w.Counter("estate_offset:old_blood"));
        Assert.True(Binds(w, "a_favour_repaid", "debtor", poor)); Assert.True(Eligible(w, "a_favour_repaid"));
        // kept owed, it stands; asked for, it is spent
        Take(w, Fire(w, "a_favour_repaid", ("debtor", poor)), "not_yet"); Assert.Contains("owes_you_a_favour", poor.Flags);
        int influence = w.House(me.HouseId)!.Treasury.Get(Resource.Influence);
        Take(w, Fire(w, "a_favour_repaid", ("debtor", poor)), "their_voice");
        Assert.DoesNotContain("owes_you_a_favour", poor.Flags); Assert.Equal(influence + 2, w.House(me.HouseId)!.Treasury.Get(Resource.Influence));
        // taken in, they are of your house
        var (w2, me2, p2) = Landless(); Take(w2, Fire(w2, "a_house_without_lands", ("claimant", p2)), "take_them_in"); Assert.Equal(me2.HouseId, p2.HouseId);
        EveryChoice("a_house_without_lands", () => { var (x, _, p) = Landless(); return (x, Fire(x, "a_house_without_lands", ("claimant", p))); });
        EveryChoice("a_favour_repaid", () => { var (x, _, p) = Landless(); p.Flags.Add("owes_you_a_favour"); return (x, Fire(x, "a_favour_repaid", ("debtor", p))); });
    }

    // ---------------- 12. a secret ----------------

    [Fact]
    public void ASecretUsed_CostsTheirHouseStanding_AndASecretKept_IsHeldOverThemUntilTheyTurnOnYou()
    {
        (World, Character, Character) Setup() { var (w, me, _, theirs) = Start(); var target = Add(w, theirs, "target", 45); Regard(w, me, target, -20); theirs.Treasury.Credits = Math.Max(20, theirs.Treasury.Credits); return (w, me, target); }
        var (w, me, target) = Setup();
        Assert.True(Binds(w, "a_secret_for_sale", "subject", target)); Assert.True(Eligible(w, "a_secret_for_sale"));
        var house = w.House(target.HouseId)!; int standing = house.Legitimacy;
        Take(w, Fire(w, "a_secret_for_sale", ("subject", target)), "buy_and_use");
        Assert.Equal(Math.Max(0, standing - 2), house.Legitimacy); Assert.Equal(-20, RegardOf(w, target, me));
        EveryChoice("a_secret_for_sale", () => { var (x, _, t) = Setup(); return (x, Fire(x, "a_secret_for_sale", ("subject", t))); });
        // kept: no influence from nowhere, a secret held over them, and one secret about them at a time
        var (wk, mk, tk) = Setup(); int influence = wk.House(mk.HouseId)!.Treasury.Get(Resource.Influence);
        Take(wk, Fire(wk, "a_secret_for_sale", ("subject", tk)), "buy_and_keep");
        Assert.Equal(influence, wk.House(mk.HouseId)!.Treasury.Get(Resource.Influence)); Assert.Contains("you_hold_their_secret", tk.Flags);
        Assert.False(Binds(wk, "a_secret_for_sale", "subject", tk));
        Assert.False(Binds(wk, "the_secret_you_hold", "subject", tk));                                  // not until they give you cause
        Regard(wk, tk, mk, -35); Assert.True(Binds(wk, "the_secret_you_hold", "subject", tk)); Assert.True(Eligible(wk, "the_secret_you_hold"));
        Regard(wk, tk, mk, 0); tk.Flags.Add(VoteMarks.MovesAgainstChar); Assert.True(Binds(wk, "the_secret_you_hold", "subject", tk));   // or move against your house
        int theirs = wk.House(tk.HouseId)!.Treasury.Credits, ours = wk.House(mk.HouseId)!.Treasury.Credits;
        Take(wk, Fire(wk, "the_secret_you_hold", ("subject", tk)), "sell_it_back");
        Assert.Equal(theirs - 4, wk.House(tk.HouseId)!.Treasury.Credits); Assert.Equal(ours + 4, wk.House(mk.HouseId)!.Treasury.Credits); Assert.DoesNotContain("you_hold_their_secret", tk.Flags);
        EveryChoice("the_secret_you_hold", () => { var (x, m, t) = Setup(); t.Flags.Add("you_hold_their_secret"); Regard(x, t, m, -35); return (x, Fire(x, "the_secret_you_hold", ("subject", t))); });
    }

    // ---------------- 13. off the record ----------------

    [Fact]
    public void AWordOffTheRecord_ComesFromAHouseAtOddsButNotAtWar()
    {
        (World, Character, Character) Setup() { var (w, me, _, theirs) = Start(); theirs.Treasury.Credits = Math.Max(50, theirs.Treasury.Credits); var envoy = Add(w, theirs, "envoy", 45); Regard(w, envoy, me, -25); return (w, me, envoy); }
        var (w, me, envoy) = Setup();
        Assert.True(Binds(w, "a_word_off_the_record", "envoy", envoy)); Assert.True(Eligible(w, "a_word_off_the_record"));
        Take(w, Fire(w, "a_word_off_the_record", ("envoy", envoy)), "come_to_an_understanding"); Assert.Equal(-10, RegardOf(w, envoy, me));
        var (wp, mp, ep) = Setup(); var theirs = wp.House(ep.HouseId)!; int had = theirs.Treasury.Credits, mine0 = wp.House(mp.HouseId)!.Treasury.Credits;
        Take(wp, Fire(wp, "a_word_off_the_record", ("envoy", ep)), "ask_what_they_offer");
        Assert.Equal(had - 3, theirs.Treasury.Credits); Assert.Equal(mine0 + 3, wp.House(mp.HouseId)!.Treasury.Credits); Assert.Equal(-28, RegardOf(wp, ep, mp));   // their house pays it, and resents it
        var (w2, me2, e2) = Setup(); new FleetSystem(Content.Fleets).DeclareWar(w2, me2.HouseId, e2.HouseId, me2.Id);
        Assert.False(Binds(w2, "a_word_off_the_record", "envoy", e2));
        EveryChoice("a_word_off_the_record", () => { var (x, _, e) = Setup(); return (x, Fire(x, "a_word_off_the_record", ("envoy", e))); });
    }

    // ---------------- 14 and 15. the patron ----------------

    private static (World w, Character me, Character young) Hopeful()
    {
        var (w, me, _, theirs) = Start(); theirs.Wealth = -1; var young = Add(w, theirs, "young", 19, "law", 0); return (w, me, young);
    }

    [Fact]
    public void APatronWanted_ComesBackWhenTheyRise_AsAFriendshipSworn()
    {
        var (w, me, young) = Hopeful();
        Assert.True(Binds(w, "a_patron_wanted", "young", young)); Assert.True(Eligible(w, "a_patron_wanted"));
        Take(w, Fire(w, "a_patron_wanted", ("young", young)), "be_their_patron");
        Assert.Contains("your_protege", young.Flags); Assert.False(Binds(w, "a_patron_wanted", "young", young));
        Assert.False(Binds(w, "the_one_you_stood_for", "risen", young));   // not before they have a rank and their years
        young.CareerRank = 1; young.Age = 24;
        Assert.True(Binds(w, "the_one_you_stood_for", "risen", young)); Assert.True(Eligible(w, "the_one_you_stood_for"));
        foreach (var r in w.Relationships.Where(r => (r.FromId == me.Id || r.ToId == me.Id) && r.Bond == BondType.Sworn)) r.Bond = BondType.None;   // the scenario's own sworn friends aside
        Take(w, Fire(w, "the_one_you_stood_for", ("risen", young)), "swear_friendship");
        Assert.Equal(BondType.Sworn, w.GetRelationship(me.Id, young.Id)!.Bond); Assert.Equal(50, w.GetRelationship(young.Id, me.Id)!.BondStrength);
        Assert.DoesNotContain("your_protege", young.Flags); Assert.False(Eligible(w, "the_one_you_stood_for"));
        // the sworn friend outside the house is the one who carries on a line that ends, in a game played on
        Assert.Equal(young.Id, DynastySystem.FindSwornSuccessor(w, me)?.Id);
        EveryChoice("a_patron_wanted", () => { var (x, _, y) = Hopeful(); return (x, Fire(x, "a_patron_wanted", ("young", y))); });
        EveryChoice("the_one_you_stood_for", () => { var (x, m, y) = Hopeful(); y.Flags.Add("your_protege"); y.CareerRank = 1; y.Age = 24; Regard(x, y, m, 30); return (x, Fire(x, "the_one_you_stood_for", ("risen", y))); });
    }

    // ---------------- 16. two guests ----------------

    private static (World w, Character me, Character killer, Character mourner) Guests()
    {
        var (w, me, _, theirs) = Start();
        var other = w.Houses.Values.Where(h => h.Id != me.HouseId && h.Id != theirs.Id && !World.Outsider(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        var killer = Add(w, theirs, "killer", 40); var victim = Add(w, other, "victim", 30); var mourner = Add(w, other, "mourner", 40);
        FeudSystem.Blood(w, victim, theirs.Id, killer.Id, "hand"); DynastySystem.Die(w, victim.Id); Gifted(me);
        return (w, me, killer, mourner);
    }

    [Fact]
    public void TwoGuestsAtOdds_AreTheOneHeldToBlameAndTheDeadPersonsHouse()
    {
        var (w, me, killer, mourner) = Guests();
        Assert.Contains(FeudSystem.BlamedChar, killer.Flags); Assert.True(RegardOf(w, mourner, killer) <= -20);
        Assert.True(Binds(w, "two_guests_at_odds", "guest", killer));
        Assert.True(Binds(w, "two_guests_at_odds", "other_guest", mourner, new Binding(w.ProtagonistId).With("guest", killer.Id)));
        Assert.True(Eligible(w, "two_guests_at_odds"));
        int between = RegardOf(w, mourner, killer);
        Take(w, Fire(w, "two_guests_at_odds", ("guest", killer), ("other_guest", mourner)), "bring_them_together");
        Assert.Equal(between + 10, RegardOf(w, mourner, killer)); Assert.Equal(5, RegardOf(w, killer, me));
        EveryChoice("two_guests_at_odds", () => { var (x, _, k, m) = Guests(); return (x, Fire(x, "two_guests_at_odds", ("guest", k), ("other_guest", m))); });
    }

    [Fact]
    public void NoMeeting_BindsSomeoneOfYourOwnHouse()
    {
        var (w, me, mine, _) = Start(); w.House(me.HouseId)!.Wealth = -1;
        var cousin = Add(w, mine, "cousin", 40, "military", 5, "Proud"); Regard(w, cousin, me, -25); Regard(w, me, cousin, -25);
        cousin.Flags.Add("owes_you_a_favour"); cousin.Flags.Add("quarrel_with_you"); cousin.Flags.Add("staked_by_you"); cousin.Flags.Add("your_protege");
        var young = Add(w, mine, "young_cousin", 19, "law", 0);
        cousin.Flags.Add(LifeSystem.OldEnemyChar);
        foreach (var (id, role, who) in new[] { ("words_at_the_feast", "envious", cousin), ("a_veteran_of_the_old_wars", "veteran", cousin), ("a_painters_great_canvas", "sitter", cousin), ("an_old_enemy_across_the_room", "enemy", cousin),
                     ("a_secret_for_sale", "subject", cousin), ("a_patron_wanted", "young", young) })
            Assert.False(Binds(w, id, role, who), $"{id} binds {who.Id} of your own house");
        // the roles named for outsiders are kept out by the binder itself
        var outside = new[] { "envious", "grandee", "claimant", "debtor", "envoy", "classmate", "veteran", "young", "sitter", "subject", "guest", "other_guest" };
        foreach (var e in Content.Events.Where(e => e.Id is "words_at_the_feast" or "the_quarrel_comes_to_a_head" or "a_merchant_princes_venture" or "the_venture_comes_home" or "the_venture_goes_down" or "a_house_without_lands" or "a_favour_repaid" or "a_word_off_the_record"
                     or "a_classmate_in_the_city" or "a_veteran_of_the_old_wars" or "a_painters_great_canvas" or "a_secret_for_sale" or "the_secret_you_hold" or "a_patron_wanted" or "two_guests_at_odds"))
            Assert.All(e.Roles, r => Assert.Contains(r.Name, outside));
        foreach (var f in new EventEngine(Content, WorldBuilder.RngFor(w)).GatherEligible(w).Where(f => f.def.Id is "the_quarrel_comes_to_a_head" or "a_favour_repaid" or "the_venture_comes_home" or "the_venture_goes_down"))
            Assert.DoesNotContain(f.binding.All.Where(kv => kv.Key != Binding.Self), kv => w.Char(kv.Value)!.HouseId == me.HouseId);
    }

    [Fact]
    public void ARelativeStruckFromTheLine_IsStillOfYourHouse_AndIsMetAsNoStranger()
    {
        // struck from the line, a relative no longer stands in it (inLine is false), and only the binder's outside parts keep them out
        var (w, me, mine, _) = Start(); var struck = Add(w, mine, "struck", 64, "military", 5); struck.Flags.Add(DisownSystem.StruckChar);
        Assert.False(DynastySystem.StandsInLine(w, me, struck)); Assert.True(Binds(w, "a_veteran_of_the_old_wars", "veteran", struck));   // the role's own conditions would take them
        foreach (var c in w.Characters.Values.Where(c => c.Id != struck.Id)) c.Flags.Add(FleetSystem.AdmiralFlag);                        // nobody else could be the veteran
        Assert.Null(Binder.TryBind(Content.Event("a_veteran_of_the_old_wars")!, w, WorldBuilder.RngFor(w)));
        Regard(w, me, struck, -20); foreach (var c in w.Characters.Values.Where(c => c.Id != struck.Id)) c.Flags.Add("you_hold_their_secret");
        Assert.True(Binds(w, "a_secret_for_sale", "subject", struck)); Assert.Null(Binder.TryBind(Content.Event("a_secret_for_sale")!, w, WorldBuilder.RngFor(w)));   // and the secret's loss would fall on your own house
    }

    [Fact]
    public void ANationsPeople_AreNotMetAtYourTable()
    {
        var (w, me, _, theirs) = Start(); var vet = Add(w, theirs, "vet", 64, "military", 5); Add(w, theirs, "vet_elder", 90); Regard(w, vet, me, 25);
        Assert.True(Binds(w, "a_veteran_of_the_old_wars", "veteran", vet));
        vet.Flags.Add(NationSystem.ForeignChar); Assert.False(Binds(w, "a_veteran_of_the_old_wars", "veteran", vet));                     // a nation's commander, not in command this year
        // a nation on the map embodies its ruler as a person of the nation's house (NationSystem.Embody): at sixty and out of command, still not met at dinner
        NationSystem.Seed(w, Content.Nations); new NationSystem(Content.Nations, new FleetSystem(Content.Fleets, Content.Holdings, Content.ShipClasses)).Tick(w);
        var ruler = w.Characters.Values.FirstOrDefault(c => c.Alive && c.Flags.Contains(NationSystem.RulerChar));
        Assert.NotNull(ruler); ruler!.Age = 60; ruler.CareerRank = 5; ruler.Flags.Remove(FleetSystem.AdmiralFlag); Regard(w, ruler, me, 25);
        Assert.False(Binds(w, "a_veteran_of_the_old_wars", "veteran", ruler));
        ruler.Flags.Remove(NationSystem.ForeignChar); Assert.True(Binds(w, "a_veteran_of_the_old_wars", "veteran", ruler)); ruler.Flags.Add(NationSystem.ForeignChar);   // it is the mark that keeps them out
        foreach (var e in Content.Events.Where(e => Ids.Contains(e.Id)))
            Assert.All(e.Roles, r => Assert.Contains(r.Constraints, k => k is CharFlagCondition f && f.Flag == NationSystem.ForeignChar && !f.Value));
    }
}
