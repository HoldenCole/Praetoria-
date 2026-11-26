using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Friends and enemies handed down (docs/LIFE.md): at every succession, the player's and every other house's, the old head's
/// friends think the better of a grown heir by a warmth that fades over ten years, the old head's enemies think the worse of the heir,
/// a vengeful one is the heir's enemy at once and a just or loyal one lets it go; another house's new head takes up the old head's
/// view of others, the player among them, in place of the half of it that used to pass.</summary>
public class InheritedRegardTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());

    private static (World w, Character me, Character heir) Start(int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), (ulong)seed);
        var me = w.Protagonist!; var heir = DynastySystem.FindHeir(w, me)!;
        Assert.True(heir.Age >= DynastySystem.AgeOfMajority);
        return (w, me, heir);
    }
    /// <summary>People of other houses of the Reach, set to have the given nature and no view yet of <paramref name="of"/>.</summary>
    private static List<Character> Outsiders(World w, Character of, int n, params string[] natures)
    {
        var list = w.LivingCharacters().Where(c => c.HouseId != of.HouseId && c.HouseId != w.Protagonist!.HouseId && !World.Outsider(c.HouseId) && c.HouseId.Length > 0).OrderBy(c => c.Id, StringComparer.Ordinal).Take(n).ToList();
        Assert.Equal(n, list.Count);
        for (int i = 0; i < list.Count; i++) { list[i].NatureTraits = new HashSet<string>(); if (i < natures.Length && natures[i].Length > 0) list[i].NatureTraits.Add(natures[i]); }
        return list;
    }
    private static int Regard(World w, Character from, Character to) => w.GetRelationship(from.Id, to.Id)?.Disposition ?? 0;
    private static void Set(World w, Character from, Character to, int d) => w.Relationship(from.Id, to.Id).Disposition = d;
    private static string Poss(Character c) => c.Sex == "female" ? "Her" : c.Sex == "male" ? "His" : "Their";

    [Fact]
    public void The_old_heads_friends_think_better_of_a_grown_heir_and_it_fades_over_ten_years()
    {
        var (w, me, heir) = Start(); var friend = Outsiders(w, me, 1)[0];
        Set(w, friend, me, 50); Set(w, friend, heir, 5);
        DynastySystem.Die(w, me.Id);
        Assert.Equal(heir.Id, w.ProtagonistId);
        Assert.Equal(5 + InheritedRegard.Warmth, Regard(w, friend, heir));
        var tie = Assert.Single(w.InheritedTies, t => t.FromId == friend.Id && t.ToId == heir.Id);
        Assert.Equal(InheritedRegard.Friend, tie.Kind); Assert.Equal(me.Id, tie.OldHeadId); Assert.Equal(InheritedRegard.Warmth, tie.Left);
        for (int year = 1; year <= InheritedRegard.FadeYears; year++)
        {
            w.Turn++; InheritedRegard.Tick(w);
            Assert.Equal(5 + InheritedRegard.Warmth - InheritedRegard.Warmth * year / InheritedRegard.FadeYears, Regard(w, friend, heir));
        }
        Assert.Equal(5, Regard(w, friend, heir)); Assert.DoesNotContain(w.InheritedTies, t => t.FromId == friend.Id);
        Assert.True(w.Counter(InheritedRegard.FriendsKey) >= 1); Assert.True(w.Counter(InheritedRegard.SuccessionsKey) >= 1);
    }

    [Fact]
    public void An_old_friend_by_the_years_counts_whatever_the_figure_and_a_lukewarm_one_does_not()
    {
        var (w, me, heir) = Start(); var o = Outsiders(w, me, 2); var old = o[0]; var lukewarm = o[1];
        me.Life ??= new Life(); me.Life.Friends[old.Id] = w.Turn - LifeSystem.OldTieYears;   // a friend of eight years, cooled a little since
        Set(w, old, me, InheritedRegard.WellAt - 1); Set(w, lukewarm, me, InheritedRegard.WellAt - 1);
        int was = Regard(w, lukewarm, heir);
        DynastySystem.Die(w, me.Id);
        Assert.Contains(w.InheritedTies, t => t.FromId == old.Id && t.Kind == InheritedRegard.Friend);
        Assert.Equal(was, Regard(w, lukewarm, heir)); Assert.DoesNotContain(w.InheritedTies, t => t.FromId == lukewarm.Id);
    }

    [Fact]
    public void The_old_heads_enemies_by_nature_a_vengeful_one_at_once_a_just_or_loyal_one_lets_it_go()
    {
        var (w, me, heir) = Start(); var e = Outsiders(w, me, 5, "Vengeful", "Just", "Loyal", "", "Cruel");
        var (vengeful, just, loyal, plain, cruel) = (e[0], e[1], e[2], e[3], e[4]);
        vengeful.NatureTraits.Add("Just");   // vengeful decides first
        foreach (var x in e) { Set(w, x, me, -35); Set(w, x, heir, 0); }
        Set(w, vengeful, me, -70);
        DynastySystem.Die(w, me.Id);
        Assert.Equal(-70, Regard(w, vengeful, heir));   // as bitter as of the old head
        Assert.Equal(0, Regard(w, just, heir)); Assert.Equal(0, Regard(w, loyal, heir));
        Assert.Equal(-InheritedRegard.Coldness, Regard(w, plain, heir)); Assert.Equal(-InheritedRegard.Coldness, Regard(w, cruel, heir));
        // the vengeful one is an old enemy of the heir at once: it counts in the Senate, at the count, in a court and in a match
        Assert.True(heir.Life!.Enemies.TryGetValue(vengeful.Id, out int since)); Assert.True(w.Turn - since >= LifeSystem.OldTieYears);
        Assert.Contains(LifeSystem.OldEnemyChar, vengeful.Flags);
        Assert.Equal("Vengeful", w.InheritedTies.Single(t => t.FromId == vengeful.Id).Nature);
        Assert.Equal("Just", w.InheritedTies.Single(t => t.FromId == just.Id && t.Kind == InheritedRegard.LetGo).Nature);
        Assert.Equal("Loyal", w.InheritedTies.Single(t => t.FromId == loyal.Id && t.Kind == InheritedRegard.LetGo).Nature);
        // and stays one at the year's turn, while the coldness of the others fades
        w.Turn++; LifeSystem.Tick(w, Content); HeadSystem.Tick(w);
        Assert.Contains(LifeSystem.OldEnemyChar, vengeful.Flags); Assert.Equal(-70, Regard(w, vengeful, heir));
        Assert.Equal(-InheritedRegard.Coldness * (InheritedRegard.FadeYears - 1) / InheritedRegard.FadeYears, Regard(w, plain, heir));
        // a vengeful one who was only lukewarm in enmity is still an enemy, and no less than one
        var (w2, me2, heir2) = Start(); var v2 = Outsiders(w2, me2, 1, "Vengeful")[0]; Set(w2, v2, me2, InheritedRegard.IllAt); Set(w2, v2, heir2, 10);
        DynastySystem.Die(w2, me2.Id); Assert.Equal(LifeSystem.EnemyAt, Regard(w2, v2, heir2));
    }

    [Fact]
    public void A_vengeful_enemy_brought_round_by_amends_is_no_longer_called_your_enemy()
    {
        var (w, me, heir) = Start(); var v = Outsiders(w, me, 1, "Vengeful")[0]; Set(w, v, me, -45); Set(w, v, heir, 0);
        DynastySystem.Die(w, me.Id);
        Assert.Contains(InheritedRegard.Why(w, v.Id, heir.Id), l => l.Kind == InheritedRegard.Vengeful && l.Text.Contains("your enemy since year"));
        // the amends on a person's page: courting them three times brings the regard to -15
        var ctx = new CommandContext(w, TestContent.Rng());
        for (int i = 0; i < 3; i++) { w.PoolsFor(heir.Id).Influence = 6; var seek = new SeekAllyCommand(heir.Id, v.Id); Assert.True(seek.CanExecute(ctx)); seek.Execute(ctx); }
        Assert.Equal(-15, Regard(w, v, heir));
        w.Turn++; LifeSystem.Tick(w, Content); HeadSystem.Tick(w);
        Assert.False(heir.Life!.Enemies.ContainsKey(v.Id)); Assert.DoesNotContain(LifeSystem.OldEnemyChar, v.Flags);
        // the page no longer says they are your enemy, and the tie is let go
        Assert.DoesNotContain(InheritedRegard.Why(w, v.Id, heir.Id), l => l.Text.Contains("your enemy"));
        Assert.DoesNotContain(w.InheritedTies, t => t.FromId == v.Id && t.Kind == InheritedRegard.Vengeful);
        // the same for another house's vengeful new head who is brought round
        var (w2, me2, _) = Start(); var (_, elder, next) = OtherHouse(w2, "Vengeful"); Set(w2, elder, me2, -60); Bury(w2, elder);
        Assert.Contains(InheritedRegard.Why(w2, next.Id, me2.Id), l => l.Kind == InheritedRegard.Vengeful && l.Text.Contains("your enemy since year"));
        Set(w2, next, me2, -20); w2.Turn++; LifeSystem.Tick(w2, Content); HeadSystem.Tick(w2);
        Assert.False(me2.Life!.Enemies.ContainsKey(next.Id));
        Assert.DoesNotContain(InheritedRegard.Why(w2, next.Id, me2.Id), l => l.Text.Contains("your enemy"));
        // while one is still counted an enemy, short of the amends, the page still says so
        var (w3, me3, heir3) = Start(); var v3 = Outsiders(w3, me3, 1, "Vengeful")[0]; Set(w3, v3, me3, -45);
        DynastySystem.Die(w3, me3.Id); Set(w3, v3, heir3, LifeSystem.EnemyAt + 15); w3.Turn++; LifeSystem.Tick(w3, Content); HeadSystem.Tick(w3);
        Assert.True(heir3.Life!.Enemies.ContainsKey(v3.Id));
        Assert.Contains(InheritedRegard.Why(w3, v3.Id, heir3.Id), l => l.Kind == InheritedRegard.Vengeful && l.Text.Contains("your enemy since year"));
    }

    [Fact]
    public void The_succession_news_gives_the_true_figure_when_a_regard_was_near_the_top_or_the_bottom()
    {
        var (w, me, heir) = Start(); var o = Outsiders(w, me, 4); var (near, plain, cold, nearCold) = (o[0], o[1], o[2], o[3]);
        Set(w, near, me, 60); Set(w, near, heir, 95); Set(w, plain, me, 60); Set(w, plain, heir, 0);
        Set(w, cold, me, -50); Set(w, cold, heir, -97); Set(w, nearCold, me, -50); Set(w, nearCold, heir, 0);
        DynastySystem.Die(w, me.Id);
        var (_, f, _, e) = InheritedRegard.Summary(w, me, heir, w.InheritedTies.Where(t => o.Any(x => x.Id == t.FromId)));
        Assert.Contains($"Each thinks up to {InheritedRegard.Warmth} the better of you", f);
        Assert.Contains($"think up to {InheritedRegard.Coldness} the worse of you", e);
        Assert.Contains(InheritedRegard.Why(w, near.Id, heir.Id), l => l.Text.Contains("+5 of it still"));
        // one alone is given their own figure
        var (w2, me2, heir2) = Start(); var a = Outsiders(w2, me2, 2); Set(w2, a[0], me2, 60); Set(w2, a[0], heir2, 95); Set(w2, a[1], me2, -50); Set(w2, a[1], heir2, -97);
        DynastySystem.Die(w2, me2.Id);
        var (_, f2, _, e2) = InheritedRegard.Summary(w2, me2, heir2, w2.InheritedTies.Where(t => a.Any(x => x.Id == t.FromId)));
        Assert.Contains("thinks 5 the better of you", f2); Assert.Contains("thinks 3 the worse of you", e2);
        // and several all moved the whole way are given the whole figure
        var (w3, me3, heir3) = Start(); var b = Outsiders(w3, me3, 4); foreach (var x in b.Take(2)) { Set(w3, x, me3, 60); Set(w3, x, heir3, 0); } foreach (var x in b.Skip(2)) { Set(w3, x, me3, -50); Set(w3, x, heir3, 0); }
        DynastySystem.Die(w3, me3.Id);
        var (_, f3, _, e3) = InheritedRegard.Summary(w3, me3, heir3, w3.InheritedTies.Where(t => b.Any(x => x.Id == t.FromId)));
        Assert.Contains($"Each thinks {InheritedRegard.Warmth} the better of you", f3); Assert.Contains($"think {InheritedRegard.Coldness} the worse of you", e3);
    }

    [Fact]
    public void A_child_takes_on_no_friendship_but_enmity_falls_on_a_child_too()
    {
        var (w, me, _) = Start(); var house = w.House(me.HouseId)!;
        var child = new Character { Id = "test_child", Name = "Test Child", HouseId = house.Id, Age = 9, Sex = "female", FatherId = me.Sex == "female" ? "" : me.Id, MotherId = me.Sex == "female" ? me.Id : "" };
        w.Characters[child.Id] = child; house.Members.Add(child.Id);
        var o = Outsiders(w, me, 3, "", "", "Vengeful"); var (friend, enemy, vengeful) = (o[0], o[1], o[2]);
        Set(w, friend, me, 60); Set(w, enemy, me, -50); Set(w, vengeful, me, -50);
        DynastySystem.HandOver(w, child);
        Assert.Equal(0, Regard(w, friend, child)); Assert.Equal(InheritedRegard.Unseen, w.InheritedTies.Single(t => t.FromId == friend.Id).Kind);
        Assert.Equal(-InheritedRegard.Coldness, Regard(w, enemy, child)); Assert.Equal(-50, Regard(w, vengeful, child));
        var (ft, f, _, e) = InheritedRegard.Summary(w, me, child, w.InheritedTies);
        Assert.Contains("you were a child", f); Assert.StartsWith("Your " + (me.Sex == "female" ? "mother" : "father") + "'s friends", ft);
        Assert.Contains("vengeful by nature", e);
        Assert.Contains(InheritedRegard.Why(w, friend.Id, child.Id), l => l.Kind == InheritedRegard.Unseen && l.Text.Contains("you were a child"));
    }

    [Fact]
    public void People_of_the_house_itself_the_dead_and_what_the_player_thinks_are_not_handed_down()
    {
        var (w, me, heir) = Start(); var house = w.House(me.HouseId)!;
        var kin = new Character { Id = "test_kin", Name = "Test Kin", HouseId = house.Id, Age = 40, Sex = "male" }; w.Characters[kin.Id] = kin; house.Members.Add(kin.Id);
        var o = Outsiders(w, me, 1); var dead = o[0]; dead.Alive = false;
        Set(w, kin, me, 70); Set(w, dead, me, 70); int kinWas = Regard(w, kin, heir);
        DynastySystem.Die(w, me.Id);
        Assert.Equal(kinWas, Regard(w, kin, heir)); Assert.Null(w.GetRelationship(dead.Id, heir.Id));
        Assert.DoesNotContain(w.InheritedTies, t => t.FromId == kin.Id || t.FromId == dead.Id);
        Assert.DoesNotContain(w.InheritedTies, t => t.FromId == heir.Id);   // the player's own view is the player's
    }

    [Fact]
    public void The_player_is_told_at_the_succession_who_the_friends_and_enemies_were()
    {
        var (w, me, heir) = Start(); var o = Outsiders(w, me, 3, "", "", "Just"); var (friend, enemy, just) = (o[0], o[1], o[2]);
        Set(w, friend, me, 60); Set(w, enemy, me, -50); Set(w, just, me, -50); int news = w.News.Count;
        DynastySystem.Die(w, me.Id);
        string parent = Kinship.Of(w, heir, me); Assert.StartsWith("your ", parent);
        string ft = char.ToUpperInvariant(parent[0]) + parent[1..] + "'s friends";
        var told = w.News.Skip(news).ToList();
        Assert.Contains(told, n => n.Tone == "good" && n.Text.StartsWith(ft + ": ") && n.Text.Contains(friend.Name) && n.Text.Contains($"{InheritedRegard.Warmth} the better of you"));
        Assert.Contains(told, n => n.Tone == "warn" && n.Text.StartsWith(Poss(me) + " enemies: ") && n.Text.Contains(enemy.Name) && n.Text.Contains(just.Name) && n.Text.Contains("just by nature, and lets it go"));
        var (_, f, _, e) = InheritedRegard.Summary(w, me, heir, w.InheritedTies.Where(t => t.Turn == w.Turn));
        Assert.Contains(friend.Name, f); Assert.Contains(enemy.Name, e);
        foreach (var n in told) { Assert.DoesNotContain("(s)", n.Text); Assert.DoesNotContain("—", n.Text); Assert.DoesNotContain("!", n.Text); }
        // and a person's page says why they think as they do of the heir
        Assert.Contains(InheritedRegard.Why(w, friend.Id, heir.Id), l => l.Kind == InheritedRegard.Friend && l.Text.Contains($"+{InheritedRegard.Warmth} of it still"));
        Assert.Contains(InheritedRegard.Why(w, enemy.Id, heir.Id), l => l.Kind == InheritedRegard.Enemy && l.Text.Contains($"-{InheritedRegard.Coldness} of it still"));
        Assert.Contains(InheritedRegard.Why(w, just.Id, heir.Id), l => l.Kind == InheritedRegard.LetGo && l.Text.Contains("just by nature"));
    }

    // ---------------- other houses ----------------

    private static (House other, Character elder, Character next) OtherHouse(World w, string nextNature = "", int nextAge = 40)
    {
        var mine = w.Protagonist!.HouseId;
        var other = w.Houses.Values.Where(h => h.Id != mine && !World.Outsider(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        foreach (var c in w.LivingCharacters().Where(c => c.HouseId == other.Id).ToList()) { c.Alive = false; other.Members.Remove(c.Id); }
        Character Add(string id, int age, string nature)
        {
            var c = new Character { Id = id, Name = "Test " + id, HouseId = other.Id, Age = age, Sex = "male", CareerTrack = "law" };
            if (nature.Length > 0) c.NatureTraits.Add(nature); w.Characters[id] = c; other.Members.Add(id); return c;
        }
        var elder = Add("test_elder", 70, ""); var next = Add("test_next", nextAge, nextNature);
        HeadSystem.Tick(w); Assert.Equal(elder.Id, other.HeadId);
        return (other, elder, next);
    }
    private static void Bury(World w, Character head) { head.Alive = false; w.Turn++; HeadSystem.Tick(w); }

    [Fact]
    public void Another_houses_new_head_takes_up_the_old_ones_regard_for_the_player_in_place_of_the_half()
    {
        var (w, me, _) = Start(); var (other, elder, next) = OtherHouse(w); int news = w.News.Count;
        Set(w, elder, me, 80);
        Bury(w, elder);
        Assert.Equal(next.Id, other.HeadId);
        Assert.Equal(InheritedRegard.Warmth, Regard(w, next, me));   // not half of 80
        Assert.Contains(w.News.Skip(news), n => n.SubjectId == other.Id && n.Text.Contains("now leads House") && n.Text.Contains("good opinion of you"));
        Assert.Contains(InheritedRegard.Why(w, next.Id, me.Id), l => l.Kind == InheritedRegard.Friend && l.Text.Contains("thought well of you"));
        for (int y = 0; y < InheritedRegard.FadeYears; y++) { w.Turn++; HeadSystem.Tick(w); }
        Assert.Equal(0, Regard(w, next, me));
    }

    [Fact]
    public void Another_houses_new_head_by_nature_takes_up_the_old_quarrel_with_the_player_or_lets_it_go()
    {
        foreach (var (nature, expect, kind) in new[] { ("Vengeful", -60, InheritedRegard.Vengeful), ("Just", 0, InheritedRegard.LetGo), ("Loyal", 0, InheritedRegard.LetGo), ("Proud", -InheritedRegard.Coldness, InheritedRegard.Enemy) })
        {
            var (w, me, _) = Start(); var (other, elder, next) = OtherHouse(w, nature); int news = w.News.Count, votes = LifeSystem.TieVotes(w, me.HouseId);
            Set(w, elder, me, -60); Bury(w, elder);
            Assert.Equal(expect, Regard(w, next, me));
            Assert.Equal(kind, w.InheritedTies.Single(t => t.FromId == next.Id && t.ToId == me.Id).Kind);
            Assert.Contains(w.News.Skip(news), n => n.SubjectId == other.Id && n.Text.Contains(next.Name + (kind == InheritedRegard.Vengeful ? " is vengeful by nature" : kind == InheritedRegard.LetGo ? " is " + nature.ToLowerInvariant() + " by nature" : " takes up")));
            if (kind == InheritedRegard.Vengeful) { Assert.True(me.Life!.Enemies.ContainsKey(next.Id)); Assert.Contains(LifeSystem.OldEnemyChar, next.Flags); Assert.True(LifeSystem.TieVotes(w, me.HouseId) < votes); }
        }
        // a child head of another house takes on no good opinion, but the old quarrel falls on them
        var (wc, mc, _) = Start(); var (_, eldc, child) = OtherHouse(wc, "", 12); Set(wc, eldc, mc, 60);
        Bury(wc, eldc); Assert.Equal(0, Regard(wc, child, mc)); Assert.Equal(InheritedRegard.Unseen, wc.InheritedTies.Single(t => t.FromId == child.Id).Kind);
    }

    [Fact]
    public void Another_houses_old_head_who_was_your_old_enemy_by_the_years_hands_the_quarrel_down_whatever_the_figure()
    {
        // an old enemy of twelve years who has warmed to -25 is still an old enemy (it takes more than -25 to let one go)
        var (w, me, _) = Start(); var (other, elder, next) = OtherHouse(w); me.Life ??= new Life();
        me.Life.Enemies[elder.Id] = w.Turn - 12; Set(w, elder, me, LifeSystem.EnemyAt + 15);
        LifeSystem.Tick(w, Content); Assert.True(me.Life.Enemies.ContainsKey(elder.Id)); Assert.Contains(LifeSystem.OldEnemyChar, elder.Flags);
        Bury(w, elder);
        Assert.Equal(next.Id, other.HeadId);
        Assert.Equal(InheritedRegard.Enemy, w.InheritedTies.Single(t => t.FromId == next.Id && t.ToId == me.Id).Kind);
        Assert.Equal(-InheritedRegard.Coldness, Regard(w, next, me));
        // and an old friend by the years the same way; one who was neither, at the same figure, hands nothing down
        var (w2, me2, _) = Start(); var (_, elder2, next2) = OtherHouse(w2); me2.Life ??= new Life();
        me2.Life.Friends[elder2.Id] = w2.Turn - LifeSystem.OldTieYears; Set(w2, elder2, me2, InheritedRegard.WellAt - 1); Bury(w2, elder2);
        Assert.Equal(InheritedRegard.Friend, w2.InheritedTies.Single(t => t.FromId == next2.Id && t.ToId == me2.Id).Kind);
        var (w3, me3, _) = Start(); var (_, elder3, next3) = OtherHouse(w3); Set(w3, elder3, me3, LifeSystem.EnemyAt + 15); Bury(w3, elder3);
        Assert.DoesNotContain(w3.InheritedTies, t => t.FromId == next3.Id && t.ToId == me3.Id);
    }

    [Fact]
    public void Another_houses_new_head_inherits_its_friends_and_enemies_among_the_other_houses_but_not_the_players_view()
    {
        var (w, me, _) = Start(); var (other, elder, next) = OtherHouse(w);
        var o = Outsiders(w, elder, 3, "", "", "Vengeful");
        var friend = o[0]; var enemy = o[1]; var vengeful = o[2];
        Set(w, friend, elder, 50); Set(w, enemy, elder, -50); Set(w, vengeful, elder, -65);
        Set(w, elder, friend, 40); Set(w, me, elder, 70);
        Bury(w, elder);
        Assert.Equal(InheritedRegard.Warmth, Regard(w, friend, next)); Assert.Equal(-InheritedRegard.Coldness, Regard(w, enemy, next)); Assert.Equal(-65, Regard(w, vengeful, next));
        Assert.Equal(InheritedRegard.Warmth, Regard(w, next, friend));   // and the heir's own view of the old head's friend
        Assert.Null(w.GetRelationship(me.Id, next.Id));   // what the player thinks is the player's
        // a tie between others that does not fade is not kept past its year
        Assert.Contains(w.InheritedTies, t => t.FromId == vengeful.Id); w.Turn++; HeadSystem.Tick(w);
        Assert.DoesNotContain(w.InheritedTies, t => t.FromId == vengeful.Id); Assert.Contains(w.InheritedTies, t => t.FromId == friend.Id);
    }

    [Fact]
    public void Laying_the_house_down_in_life_hands_the_ties_down_too()
    {
        var (w, me, heir) = Start(); me.Age = LifeSystem.RetireAge + 5; var friend = Outsiders(w, me, 1)[0]; Set(w, friend, me, 50); int was = Regard(w, friend, heir);
        Assert.Null(LifeSystem.RetireRefusal(w, me)); LifeSystem.Retire(w, me);
        Assert.Equal(heir.Id, w.ProtagonistId); Assert.Equal(Math.Min(100, was + InheritedRegard.Warmth), Regard(w, friend, heir));
    }

    [Fact]
    public void What_was_handed_down_is_kept_in_a_save_and_fades_the_same_after_it()
    {
        var (w, me, heir) = Start(); var o = Outsiders(w, me, 2, "", "Vengeful"); Set(w, o[0], me, 50); Set(w, o[1], me, -50);
        DynastySystem.Die(w, me.Id); w.Turn++; InheritedRegard.Tick(w);
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        Assert.Equal(w.InheritedTies.Count, back.InheritedTies.Count);
        foreach (var (a, b) in w.InheritedTies.Zip(back.InheritedTies))
        { Assert.Equal((a.FromId, a.ToId, a.HeirId, a.OldHeadId, a.Kind, a.Nature, a.Amount, a.Left, a.Turn), (b.FromId, b.ToId, b.HeirId, b.OldHeadId, b.Kind, b.Nature, b.Amount, b.Left, b.Turn)); }
        Assert.True(back.Char(heir.Id)!.Life!.Enemies.ContainsKey(o[1].Id));
        for (int y = 0; y < 4; y++) { w.Turn++; InheritedRegard.Tick(w); back.Turn++; InheritedRegard.Tick(back); }
        Assert.Equal(Regard(w, o[0], heir), Regard(back, back.Char(o[0].Id)!, back.Char(heir.Id)!));
    }

    [Fact]
    public void In_thirty_years_ties_are_handed_down_at_successions_and_the_game_replays_the_same()
    {
        World Play()
        {
            var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 1); var tc = new TurnController(w, Content);
            for (int y = 0; y < 30 && !w.IsDynastyDead; y++) { foreach (var i in tc.BeginTurn()) { var ch = tc.Offer(i).FirstOrDefault(x => x.Available); if (ch != null) tc.Resolve(i, ch.Choice.Id); } tc.EndTurn(); }
            return w;
        }
        var a = Play(); var b = Play();
        Assert.True(a.Counter(InheritedRegard.SuccessionsKey) > 5);   // measured: 30 in this game, the player's two among them
        Assert.True(a.Counter(InheritedRegard.FriendsKey) + a.Counter(InheritedRegard.EnemiesKey) > 0);
        foreach (var key in new[] { InheritedRegard.SuccessionsKey, InheritedRegard.FriendsKey, InheritedRegard.EnemiesKey, InheritedRegard.VengefulKey, InheritedRegard.LetGoKey }) Assert.Equal(a.Counter(key), b.Counter(key));
        Assert.Equal(a.InheritedTies.Select(t => (t.FromId, t.ToId, t.Left)), b.InheritedTies.Select(t => (t.FromId, t.ToId, t.Left)));
        // nothing fades past what was handed down, and nothing is kept past its time
        Assert.All(a.InheritedTies, t => { Assert.True(Math.Abs(t.Left) <= Math.Abs(t.Amount)); Assert.True(a.Turn - t.Turn <= InheritedRegard.FadeYears); });
    }
}
