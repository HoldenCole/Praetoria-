using Praetoria.Core;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Who leads a house other than the player's (docs/HOUSEHOLD.md, "Who leads another house"): the eldest, until death,
/// unless there is good reason to make them stand aside.</summary>
public class HeadOfHouseTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static (World w, House mine, House other) Start()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 7); var mine = w.House(w.Protagonist!.HouseId)!;
        var other = w.Houses.Values.Where(h => h.Id != mine.Id && !World.Outsider(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        foreach (var c in w.LivingCharacters().Where(c => c.HouseId == other.Id).ToList()) { c.Alive = false; other.Members.Remove(c.Id); }
        return (w, mine, other);
    }
    private static Character Add(World w, House h, string id, int age, int rank = 0, int health = 100)
    {
        var c = new Character { Id = id, Name = "Test " + id, HouseId = h.Id, Age = age, Sex = "female", CareerTrack = "law", CareerRank = rank, Health = health };
        w.Characters[id] = c; h.Members.Add(id); return c;
    }
    /// <summary>The first year from here in which a challenge with a reason is made.</summary>
    private static void UntilChallenged(World w, House h, Character head)
    {
        for (int i = 0; i < 40 && h.HeadId == head.Id; i++) { w.Turn++; HeadSystem.Tick(w); }
    }

    [Fact]
    public void The_eldest_grown_person_leads_and_a_higher_rank_alone_is_not_the_head()
    {
        var (w, mine, other) = Start(); var elder = Add(w, other, "elder", 61); var able = Add(w, other, "able", 40, rank: 1); Add(w, other, "child", 9);
        Assert.Equal(elder.Id, Economy.HeadOf(w, other)!.Id);   // before any year has turned, and with nothing recorded
        HeadSystem.Tick(w); Assert.Equal(elder.Id, other.HeadId);
        able.CareerRank = elder.CareerRank + HeadSystem.RankLead - 1;   // a promotion that is not a good reason
        for (int i = 0; i < 12; i++) { w.Turn++; HeadSystem.Tick(w); }
        Assert.Equal(elder.Id, Economy.HeadOf(w, other)!.Id); Assert.DoesNotContain(w.History, h => h.Text.Contains("stands aside as head of House " + other.Name));
        Assert.Equal(w.Protagonist!.Id, Economy.HeadOf(w, mine)!.Id); Assert.Equal("", mine.HeadId);   // the player's house is the player's
    }

    [Fact]
    public void A_child_leads_only_a_house_with_no_grown_person()
    {
        var (w, _, other) = Start(); var older = Add(w, other, "older", 12); Add(w, other, "younger", 7);
        Assert.Equal(older.Id, Economy.HeadOf(w, other)!.Id);
        var grown = Add(w, other, "grown", DynastySystem.AgeOfMajority); Assert.Equal(grown.Id, Economy.HeadOf(w, other)!.Id);
    }

    [Fact]
    public void A_head_who_dies_is_followed_by_the_next_eldest_and_stays_while_alive()
    {
        var (w, _, other) = Start(); var elder = Add(w, other, "elder", 61); var next = Add(w, other, "next", 50); Add(w, other, "young", 30);
        HeadSystem.Tick(w); var newcomer = Add(w, other, "newcomer", 64);   // someone older joins the house: the head it has stays
        w.Turn++; HeadSystem.Tick(w); Assert.Equal(elder.Id, other.HeadId);
        elder.Alive = false; Assert.Equal(newcomer.Id, Economy.HeadOf(w, other)!.Id);
        w.Turn++; HeadSystem.Tick(w); Assert.Equal(newcomer.Id, other.HeadId); Assert.Contains(w.History, h => h.Text.Contains("now leads House " + other.Name));
        Assert.NotEqual(next.Id, other.HeadId);
    }

    [Fact]
    public void One_who_stands_well_above_the_head_in_rank_can_make_them_stand_aside()
    {
        var (w, _, other) = Start(); var elder = Add(w, other, "elder", 61, rank: 1); var able = Add(w, other, "able", 40, rank: 1 + HeadSystem.RankLead); Add(w, other, "third", 45, rank: 2);
        HeadSystem.Tick(w); Assert.Equal(elder.Id, other.HeadId);
        Assert.Equal("stands well above them in rank", HeadSystem.Cause(elder, able)); Assert.Equal(able.Id, HeadSystem.Challenger(w, other, elder)!.Value.who.Id);
        UntilChallenged(w, other, elder);
        Assert.Equal(able.Id, other.HeadId); Assert.True(elder.Alive); Assert.Equal(other.Id, elder.HouseId); Assert.True(w.Counter(HeadSystem.ChallengesKey) >= 1);
        Assert.Contains(w.History, h => h.Text.Contains("stands aside as head of House " + other.Name) && h.Text.Contains(able.Name));
        // the one who stood aside is still the eldest, and does not come back for it
        for (int i = 0; i < 8; i++) { w.Turn++; HeadSystem.Tick(w); }
        Assert.Equal(able.Id, other.HeadId);
    }

    [Fact]
    public void A_head_who_is_old_and_failing_can_be_made_to_stand_aside_but_age_alone_is_not_enough()
    {
        var (w, _, other) = Start(); var elder = Add(w, other, "elder", HeadSystem.OldAt + 2); var heir = Add(w, other, "heir", 44);
        HeadSystem.Tick(w); Assert.Null(HeadSystem.Cause(elder, heir));   // old, and sound
        for (int i = 0; i < 8; i++) { w.Turn++; HeadSystem.Tick(w); }
        Assert.Equal(elder.Id, other.HeadId);
        elder.Health = LifeSystem.Frail - 1; Assert.Equal("the head is old and failing", HeadSystem.Cause(elder, heir));
        Assert.Null(HeadSystem.Cause(elder, Add(w, other, "old_too", HeadSystem.OldAt)));   // not for another who is old
        Assert.Null(HeadSystem.Cause(elder, Add(w, other, "boy", 12)));
        UntilChallenged(w, other, elder); Assert.Equal(heir.Id, other.HeadId);
        elder.Health = 100; elder.Age = HeadSystem.VeryOldAt; Assert.Equal("the head is very old", HeadSystem.Cause(elder, heir));
    }

    [Fact]
    public void The_new_head_takes_up_the_old_ones_regard_for_the_player_and_the_player_is_told()
    {
        // the half of the old head's regard that used to pass is replaced by the rule of docs/LIFE.md, "Friends and enemies handed down"
        var (w, _, other) = Start(); var me = w.Protagonist!; var elder = Add(w, other, "elder", 61); var able = Add(w, other, "able", 40, rank: HeadSystem.RankLead);
        HeadSystem.Tick(w); w.Relationship(elder.Id, me.Id).Disposition = 40; int news = w.News.Count;
        UntilChallenged(w, other, elder);
        Assert.Equal(able.Id, other.HeadId); Assert.Equal(InheritedRegard.Warmth, w.GetRelationship(able.Id, me.Id)!.Disposition);
        Assert.Contains(w.News.Skip(news), n => n.Target == "houses" && n.SubjectId == other.Id && n.Text.Contains("stands aside") && n.Text.Contains("good opinion of you"));
    }

    [Fact]
    public void The_head_is_kept_in_a_save()
    {
        var (w, _, other) = Start(); var elder = Add(w, other, "elder", 61, rank: 0); var able = Add(w, other, "able", 40, rank: HeadSystem.RankLead);
        HeadSystem.Tick(w); UntilChallenged(w, other, elder);
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        Assert.Equal(able.Id, back.House(other.Id)!.HeadId); Assert.Equal(able.Id, Economy.HeadOf(back, back.House(other.Id)!)!.Id);
    }

    [Fact]
    public void Over_thirty_years_few_houses_change_their_head_except_by_death()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 11); var tc = new TurnController(w, Content);
        for (int y = 0; y < 30; y++) { foreach (var i in tc.BeginTurn()) { var ch = tc.Offer(i).FirstOrDefault(x => x.Available); if (ch != null) tc.Resolve(i, ch.Choice.Id); } tc.EndTurn(); }
        int houses = w.Houses.Values.Count(h => !World.Outsider(h.Id) && h.HeadId.Length > 0);
        Assert.True(houses > 5); Assert.InRange(w.Counter(HeadSystem.ChallengesKey), 0, houses);   // measured: 3 in thirty years in this game
        foreach (var h in w.Houses.Values.Where(h => !World.Outsider(h.Id) && w.LivingCharacters().Any(c => c.HouseId == h.Id))) Assert.NotNull(Economy.HeadOf(w, h));
    }
}
