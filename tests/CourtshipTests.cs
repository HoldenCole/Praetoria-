using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Love, courting and marriage (docs/MARRIAGE.md): a suit won over years from the person and from the family, rivals, the
/// contract, promises between children, and a marriage that warms and cools afterwards.</summary>
public class CourtshipTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static (TurnController tc, World w, Character me) Start(string scenario = "the_new_man", int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content); tc.BeginTurn();
        return (tc, w, w.Protagonist!);
    }
    private static bool Do(World w, ICommand cmd) => new CommandExecutor().TryExecute(cmd, new CommandContext(w, TestContent.Rng()));
    private static void Year(TurnController tc) { tc.EndTurn(); tc.BeginTurn(); }
    /// <summary>Someone the protagonist could court, from a house that is not above looking at theirs.</summary>
    private static Character Match(World w, Character me, Func<Character, bool>? also = null) =>
        CourtshipSystem.Prospects(w, me).Select(x => x.person).First(c => c.Age >= 16 && (also == null || also(c)));
    private static Courtship Court(World w, Character me, Character t)
    {
        Assert.True(Do(w, new CourtCommand(me.Id, me.Id, t.Id)), CourtshipSystem.BeginRefusal(w, me, me, t));
        return w.Courtships.Single(c => c.SuitorId == me.Id && c.TargetId == t.Id);
    }

    [Fact]
    public void A_courtship_is_won_from_the_person_and_from_the_family_one_act_a_year()
    {
        var (tc, w, me) = Start(); var t = Match(w, me); var c = Court(w, me, t);
        Assert.InRange(c.Favour, 0, 70); Assert.InRange(c.Consent, 0, 80);
        int f = c.Favour, k = c.Consent;
        Assert.True(Do(w, new WooCommand(me.Id, c.Id, CourtshipSystem.ActTime))); Assert.True(c.Favour > f); Assert.Equal(k, c.Consent);
        Assert.False(Do(w, new WooCommand(me.Id, c.Id, CourtshipSystem.ActFamily)));   // one thing a year
        Year(tc); c = w.Courtships.Single(x => x.Id == c.Id); k = c.Consent; c.RivalId = ""; c.RivalStanding = 0;   // this test is about the acts, not about rivals
        var family = new WooCommand(me.Id, c.Id, CourtshipSystem.ActFamily); w.PoolsFor(me.Id).Influence = Math.Max(w.PoolsFor(me.Id).Influence, 2);
        Assert.True(Do(w, family), family.Refusal(new CommandContext(w, TestContent.Rng())) ?? "refused without a reason"); Assert.True(c.Consent > k, $"consent {k} -> {c.Consent}, acted {c.ActedTurn}, turn {w.Turn}");
        Year(tc); Assert.Contains(c, w.Courtships); int credits = w.TreasuryOf(me.Id)!.Credits; w.TreasuryOf(me.Id)!.Credits = Math.Max(credits, 40); credits = w.TreasuryOf(me.Id)!.Credits;
        var gift = new WooCommand(me.Id, c.Id, CourtshipSystem.ActGift); Assert.True(Do(w, gift), gift.Refusal(new CommandContext(w, TestContent.Rng())) ?? "refused"); Assert.Equal(credits - CourtshipSystem.GiftCredits, w.TreasuryOf(me.Id)!.Credits);
    }

    [Fact]
    public void The_family_answers_by_the_reckoning_shown_and_a_refusal_costs_a_little()
    {
        var (_, w, me) = Start(); var t = Match(w, me); var c = Court(w, me, t);
        c.Favour = 40; c.Consent = 30;
        var (ok, score, _) = CourtshipSystem.Answer(w, c, CourtshipSystem.Customary, ""); Assert.False(ok); Assert.Equal(30, score);
        Assert.True(Do(w, new SettleSuitCommand(me.Id, c.Id, SettleSuitCommand.Propose))); Assert.Equal(25, c.Consent); Assert.Null(DynastySystem.Spouse(w, me));
        Assert.False(Do(w, new SettleSuitCommand(me.Id, c.Id, SettleSuitCommand.Propose)));   // they answer once a year
        c.Favour = 10; c.Consent = 90; Assert.False(CourtshipSystem.Answer(w, c, CourtshipSystem.Customary, "").ok);   // the person will not have it
    }

    [Fact]
    public void A_person_who_wants_the_match_lets_a_house_marry_above_itself()
    {
        var (_, w, me) = Start();
        var mine = w.House(me.HouseId)!;
        var t = Match(w, me, x => (w.House(x.HouseId)?.Legitimacy ?? 0) > mine.Legitimacy + 40);   // a plain proposal is refused outright
        Assert.Equal("their house looks too far down on yours", new ProposeMarriageCommand(me.Id, me.Id, t.Id, Content.Titles).Refusal(new CommandContext(w, TestContent.Rng())));
        var c = Court(w, me, t); c.Consent = 40; c.Favour = 30;
        Assert.False(CourtshipSystem.Answer(w, c, CourtshipSystem.Customary, "").ok);
        c.Favour = 70;   // they argue for it at home
        Assert.True(CourtshipSystem.Answer(w, c, CourtshipSystem.Customary, "").ok);
        Assert.True(Do(w, new SettleSuitCommand(me.Id, c.Id, SettleSuitCommand.Propose)));
        Assert.Equal(t.Id, DynastySystem.Spouse(w, me)?.Id);
        var m = w.MarriageOf(me.Id)!; Assert.Equal(CourtshipSystem.Love, m.Kind); Assert.True(m.Warmth >= 55, $"warmth {m.Warmth}");
        Assert.Empty(w.Courtships); Assert.True(w.HasFlag(CourtshipSystem.JustWedFlag)); Assert.True(w.HasFlag(CourtshipSystem.LoveMatchFlag));
    }

    [Fact]
    public void The_terms_asked_move_the_answer_and_the_dowry_passes_with_the_bride()
    {
        var (_, w, me) = Start(); var t = Match(w, me, x => x.Sex != me.Sex); var c = Court(w, me, t); c.Favour = 40; c.Consent = 55;
        bool weReceive = t.Sex == "female";
        int plain = CourtshipSystem.Answer(w, c, CourtshipSystem.Customary, "").score, none = CourtshipSystem.Answer(w, c, CourtshipSystem.None, "").score;
        Assert.Equal(weReceive ? 8 : -8, none - plain);
        var bride = t.Sex == "female" ? t : me; var groom = bride == t ? me : t;
        var from = w.House(bride.HouseId)!; var to = w.House(groom.HouseId)!; from.Treasury.Credits = 200; to.Treasury.Credits = 200;
        int due = CourtshipSystem.CustomaryDowry(w, from.Id); Assert.InRange(due, 8, 60);
        Assert.True(Do(w, new SettleSuitCommand(me.Id, c.Id, SettleSuitCommand.Propose, CourtshipSystem.Customary, me.HouseId)));
        Assert.Equal(200 - due, from.Treasury.Credits); Assert.Equal(200 + due, to.Treasury.Credits);
        var m = w.MarriageOf(me.Id)!; Assert.Equal(due, m.Dowry); Assert.Equal(from.Id, m.DowryFrom); Assert.Equal(me.HouseId, m.ChildrenTo);
        Assert.Equal(me.HouseId, CourtshipSystem.ContractNameHouse(w, bride, groom));
    }

    [Fact]
    public void A_stewards_proposal_for_someone_you_are_courting_becomes_a_rival_who_can_win()
    {
        var (tc, w, me) = Start(); var t = Match(w, me); var c = Court(w, me, t); c.Favour = 20; c.Consent = 20;
        var rival = w.LivingCharacters().Where(x => x.HouseId != me.HouseId && x.HouseId != t.HouseId && x.Age >= 16 && CourtshipSystem.Bar(w, x, t) == null).OrderBy(x => x.Id, StringComparer.Ordinal).First();
        Assert.True(CourtshipSystem.Intercept(w, rival, t)); Assert.Equal(rival.Id, c.RivalId); Assert.Null(DynastySystem.Spouse(w, t));
        Assert.True(CourtshipSystem.ActRefusal(w, c, CourtshipSystem.ActRival) == null);
        int before = c.RivalStanding; CourtshipSystem.Act(w, c, CourtshipSystem.ActRival); Assert.True(c.RivalStanding < before);
        c.RivalStanding = 70; int stress = me.Stress;
        tc.EndTurn();
        Assert.Equal(rival.Id, DynastySystem.Spouse(w, t)?.Id); Assert.DoesNotContain(w.Courtships, x => !x.Incoming); Assert.True(w.HasFlag(CourtshipSystem.JiltedFlag)); Assert.True(me.Stress >= stress);
    }

    [Fact]
    public void A_suit_for_one_of_your_kin_waits_for_the_heads_answer()
    {
        var (tc, w, me) = Start("the_old_lion");
        var kin = w.LivingCharacters().Where(x => x.HouseId == me.HouseId && x.Id != me.Id && x.Age >= 16 && DynastySystem.Spouse(w, x) == null).OrderBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
        if (kin == null) { kin = new Character { Id = "test_kin", Name = "Test Kin", HouseId = me.HouseId, Age = 20, Sex = "female" }; w.Characters[kin.Id] = kin; w.House(me.HouseId)!.Members.Add(kin.Id); }
        var suitor = w.LivingCharacters().Where(x => x.HouseId != me.HouseId && x.Age >= 16 && CourtshipSystem.Bar(w, x, kin) == null).OrderBy(x => x.Id, StringComparer.Ordinal).First();
        Assert.True(CourtshipSystem.Intercept(w, suitor, kin)); Assert.Null(DynastySystem.Spouse(w, kin));
        var suit = w.Courtships.Single(c => c.Incoming && c.TargetId == kin.Id);
        Assert.False(Do(w, new SettleSuitCommand(me.Id, suit.Id, SettleSuitCommand.BreakOff)));   // not yours to drop
        Assert.True(Do(w, new SettleSuitCommand(me.Id, suit.Id, SettleSuitCommand.Propose, CourtshipSystem.Customary, "")));
        Assert.Equal(suitor.Id, DynastySystem.Spouse(w, kin)?.Id);

        // refused instead, their head thinks the worse of yours
        var (_, w2, me2) = Start("the_old_lion"); var kin2 = w2.Char(kin.Id);
        if (kin2 == null) { kin2 = new Character { Id = "test_kin", Name = "Test Kin", HouseId = me2.HouseId, Age = 20, Sex = "female" }; w2.Characters[kin2.Id] = kin2; w2.House(me2.HouseId)!.Members.Add(kin2.Id); }
        var suitor2 = w2.Char(suitor.Id)!; CourtshipSystem.Intercept(w2, suitor2, kin2); var suit2 = w2.Courtships.Single(c => c.Incoming);
        var theirHead = Economy.HeadOf(w2, w2.House(suitor2.HouseId)!)!; int regard = w2.GetRelationship(theirHead.Id, me2.Id)?.Disposition ?? 0;
        Assert.True(Do(w2, new SettleSuitCommand(me2.Id, suit2.Id, SettleSuitCommand.Refuse)));
        Assert.Equal(regard - 8, w2.GetRelationship(theirHead.Id, me2.Id)!.Disposition); Assert.Empty(w2.Courtships);
        _ = tc;
    }

    [Fact]
    public void Children_are_promised_and_wed_when_both_are_of_age_or_the_promise_is_broken_at_a_price()
    {
        var (tc, w, me) = Start();
        var child = new Character { Id = "test_child", Name = "Test Child", HouseId = me.HouseId, Age = 14, Sex = "male" }; w.Characters[child.Id] = child; w.House(me.HouseId)!.Members.Add(child.Id);
        var other = w.Houses.Values.Where(h => h.Id != me.HouseId && h.Id != PirateSystem.HouseId).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        var girl = new Character { Id = "test_girl", Name = "Test Girl", HouseId = other.Id, Age = 15, Sex = "female" }; w.Characters[girl.Id] = girl; other.Members.Add(girl.Id);
        Assert.True(Do(w, new CourtCommand(me.Id, child.Id, girl.Id))); var c = w.Courtships.Single(x => x.SuitorId == child.Id);
        Assert.Equal(0, c.Favour); Assert.NotNull(CourtshipSystem.ActRefusal(w, c, CourtshipSystem.ActTime));   // this is for the families
        c.Consent = 60; Assert.True(Do(w, new SettleSuitCommand(me.Id, c.Id, SettleSuitCommand.Propose, CourtshipSystem.None, me.HouseId)));
        Assert.True(c.Betrothed); Assert.Null(DynastySystem.Spouse(w, child)); Assert.True(CourtshipSystem.Promised(w, girl.Id));
        // a steward who comes for a promised person is turned away
        var stranger = w.LivingCharacters().First(x => x.HouseId != me.HouseId && x.HouseId != other.Id && x.Sex == "male" && x.Age >= 16 && DynastySystem.Spouse(w, x) == null);
        Assert.True(CourtshipSystem.Intercept(w, stranger, girl)); Assert.Null(DynastySystem.Spouse(w, girl));
        Year(tc); Assert.Null(DynastySystem.Spouse(w, child));   // 15 and 16
        Year(tc); Year(tc); Assert.Equal(girl.Id, DynastySystem.Spouse(w, child)?.Id); Assert.Equal(me.HouseId, w.MarriageOf(child.Id)!.ChildrenTo);

        var (_, w2, me2) = Start(); var mine2 = w2.House(me2.HouseId)!;
        var boy2 = new Character { Id = "test_child", Name = "Test Child", HouseId = me2.HouseId, Age = 8, Sex = "male" }; w2.Characters[boy2.Id] = boy2; mine2.Members.Add(boy2.Id);
        var girl2 = new Character { Id = "test_girl", Name = "Test Girl", HouseId = other.Id, Age = 7, Sex = "female" }; w2.Characters[girl2.Id] = girl2; w2.House(other.Id)!.Members.Add(girl2.Id);
        Do(w2, new CourtCommand(me2.Id, boy2.Id, girl2.Id)); var c2 = w2.Courtships.Single(); c2.Consent = 70; Do(w2, new SettleSuitCommand(me2.Id, c2.Id, SettleSuitCommand.Propose));
        Assert.True(c2.Betrothed); int legit = mine2.Legitimacy;
        Assert.True(Do(w2, new SettleSuitCommand(me2.Id, c2.Id, SettleSuitCommand.BreakOff)));
        Assert.Equal(Math.Max(0, legit - 3), mine2.Legitimacy); Assert.True(w2.HasFlag(CourtshipSystem.BetrothalBrokenFlag)); Assert.Empty(w2.Courtships);
    }

    [Fact]
    public void An_elopement_needs_no_leave_and_is_not_forgiven()
    {
        var (_, w, me) = Start(); var t = Match(w, me); var c = Court(w, me, t); c.Consent = 0; c.Favour = 60;
        Assert.False(Do(w, new SettleSuitCommand(me.Id, c.Id, SettleSuitCommand.Elope)));
        c.Favour = 80; var theirHead = Economy.HeadOf(w, w.House(t.HouseId)!)!; int regard = w.GetRelationship(theirHead.Id, me.Id)?.Disposition ?? 0;
        Assert.True(Do(w, new SettleSuitCommand(me.Id, c.Id, SettleSuitCommand.Elope)));
        Assert.Equal(t.Id, DynastySystem.Spouse(w, me)?.Id); Assert.True(w.HasFlag(CourtshipSystem.ElopedFlag));
        if (theirHead.Id != t.Id) Assert.True(w.GetRelationship(theirHead.Id, me.Id)!.Disposition <= regard - 40 + 30);
        Assert.Equal(0, w.MarriageOf(me.Id)!.Dowry); Assert.Equal(me.HouseId, w.MarriageOf(me.Id)!.ChildrenTo);
    }

    [Fact]
    public void A_marriage_warms_and_cools_and_a_couple_living_apart_have_no_children()
    {
        var (tc, w, me) = Start(); var t = Match(w, me, x => x.Sex == "female" && x.Age <= 30); CourtshipSystem.Wed(w, me, t, CourtshipSystem.Arranged);
        var m = w.MarriageOf(me.Id)!; var mother = t; var father = me;
        m.Warmth = 100; Assert.Equal(1.3, CourtshipSystem.Fertility(w, mother, father), 3);
        m.Warmth = 50; Assert.Equal(1.0, CourtshipSystem.Fertility(w, mother, father), 3);
        m.Warmth = 10; tc.EndTurn(); Assert.True(m.Estranged); Assert.Equal(0.0, CourtshipSystem.Fertility(w, mother, father)); Assert.True(w.HasFlag(CourtshipSystem.EstrangedFlag));
        tc.BeginTurn(); int births = w.Characters.Values.Count(k => k.MotherId == mother.Id);
        m.Warmth = 5; for (int i = 0; i < 6; i++) { m.Warmth = 5; Year(tc); }
        Assert.Equal(births, w.Characters.Values.Count(k => k.MotherId == mother.Id));
        m.Warmth = 20; int warm = m.Warmth; Assert.True(Do(w, new MarriageCommand(me.Id, me.Id, MarriageCommand.Tend))); Assert.True(m.Warmth > warm);
        Assert.False(Do(w, new MarriageCommand(me.Id, me.Id, MarriageCommand.Tend)));   // once a year
        m.Warmth = 40; Year(tc); Assert.False(m.Estranged);
    }

    [Fact]
    public void A_divorce_ends_the_bond_returns_the_dowry_and_leaves_both_free()
    {
        var (_, w, me) = Start(); var t = Match(w, me, x => x.Sex == "female"); var theirs = w.House(t.HouseId)!; var mine = w.House(me.HouseId)!;
        theirs.Treasury.Credits = 100; mine.Treasury.Credits = 100;
        var m = CourtshipSystem.Wed(w, me, t, CourtshipSystem.Advantage, CourtshipSystem.Customary); int dowry = m.Dowry; Assert.True(dowry > 0);
        int legit = mine.Legitimacy;
        Assert.True(Do(w, new MarriageCommand(me.Id, me.Id, MarriageCommand.Divorce)));
        Assert.Null(DynastySystem.Spouse(w, me)); Assert.Null(w.MarriageOf(me.Id)); Assert.DoesNotContain("wed", me.Flags);
        Assert.Equal(100, theirs.Treasury.Credits); Assert.Equal(100, mine.Treasury.Credits); Assert.True(mine.Legitimacy < legit); Assert.True(w.HasFlag(CourtshipSystem.DivorcedFlag));
        Assert.Null(CourtshipSystem.Bar(w, me, t));   // free to marry again, each other included
    }

    [Fact]
    public void A_widow_is_free_to_marry_again()
    {
        var (tc, w, me) = Start(); var t = Match(w, me); CourtshipSystem.Wed(w, me, t, CourtshipSystem.Love); Assert.Contains("wed", me.Flags);
        DynastySystem.Die(w, t.Id); tc.EndTurn();
        Assert.Null(w.MarriageOf(me.Id)); Assert.DoesNotContain("wed", me.Flags); Assert.Contains(CourtshipSystem.WidowedChar, me.Flags); Assert.True(w.HasFlag(CourtshipSystem.WidowedFlag));
        tc.BeginTurn(); var next = Match(w, me); Court(w, me, next);
    }

    [Fact]
    public void Every_marriage_the_world_opens_with_has_a_record_and_none_is_announced()
    {
        foreach (var scenario in new[] { "the_old_lion", "the_new_man", "the_imperial_court" })
        {
            var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), 3); _ = new TurnController(w, Content);
            foreach (var c in w.LivingCharacters()) { var s = DynastySystem.Spouse(w, c); if (s != null) Assert.True(w.MarriageOf(c.Id)?.Has(s.Id) == true, $"{scenario}: {c.Name}"); }
            Assert.False(w.HasFlag(CourtshipSystem.JustWedFlag)); Assert.All(w.Marriages, m => Assert.InRange(m.Warmth, 5, 95));
        }
    }

    [Fact]
    public void A_saga_with_suits_in_hand_saves_loads_and_replays_the_same()
    {
        string Play(bool reload)
        {
            var (tc, w, me) = Start("the_new_man", 11); var t = Match(w, me); var c = Court(w, me, t);
            for (int i = 0; i < 12; i++)
            {
                var live = w.Courtships.FirstOrDefault(x => x.SuitorId == w.ProtagonistId && !x.Betrothed);
                if (live != null) { Do(w, new WooCommand(w.ProtagonistId, live.Id, i % 2 == 0 ? CourtshipSystem.ActTime : CourtshipSystem.ActFamily)); if (CourtshipSystem.Answer(w, live, CourtshipSystem.Customary, "").ok) Do(w, new SettleSuitCommand(w.ProtagonistId, live.Id, SettleSuitCommand.Propose)); }
                tc.EndTurn();
                if (reload && i == 5)
                {
                    string path = Path.Combine(Path.GetTempPath(), $"praetoria_court_{Guid.NewGuid():N}.json"); SaveSystem.Save(w, path); w = SaveSystem.Load(path); File.Delete(path); tc = new TurnController(w, Content);
                }
                if (w.IsDynastyDead) break; tc.BeginTurn();
            }
            return $"{w.RngState}|{w.Marriages.Count}|{string.Join(",", w.Marriages.OrderBy(m => m.A, StringComparer.Ordinal).Select(m => m.A + m.B + m.Warmth + m.Kind))}|{string.Join(",", w.Courtships.Select(x => x.Id + x.Favour + "/" + x.Consent + x.RivalId))}|{w.History.Count}";
        }
        Assert.Equal(Play(false), Play(true));
    }

    [Fact]
    public void A_save_resumed_in_the_middle_of_a_year_does_not_swallow_a_wedding_the_year_would_announce()
    {
        // found by the soak: opening a saved world made the record quietly, so the resumed run never raised just_wed
        var (tc, w, me) = Start(); var t = Match(w, me);
        foreach (var (a, b) in new[] { (me.Id, t.Id), (t.Id, me.Id) }) { var r = w.Relationship(a, b); r.Bond = BondType.Marriage; r.BondStrength = 60; }   // as an event's addBond does
        var again = SaveSystem.Deserialize(SaveSystem.Serialize(w)); var tc2 = new TurnController(again, Content);
        Assert.Null(again.MarriageOf(me.Id));
        tc.EndTurn(); tc2.EndTurn();
        Assert.True(w.HasFlag(CourtshipSystem.JustWedFlag)); Assert.True(again.HasFlag(CourtshipSystem.JustWedFlag)); Assert.Equal(w.MarriageOf(me.Id)!.Warmth, again.MarriageOf(me.Id)!.Warmth);
    }
}
