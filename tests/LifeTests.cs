using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>A life of one's own (docs/LIFE.md): health and wounds, the record of what a person did and the renown and name that come
/// of it, friends and enemies of long standing, retirement, and what is said at a funeral.</summary>
public class LifeTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static (TurnController tc, World w, Character me, House mine, CommandContext ctx) Start(string scenario = "the_old_lion", int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content); tc.BeginTurn();
        return (tc, w, w.Protagonist!, w.House(w.Protagonist!.HouseId)!, new CommandContext(w, TestContent.Rng()));
    }
    private static bool Do(TurnController tc, CommandContext ctx, ICommand cmd) => tc.Executor.TryExecute(cmd, ctx);
    private static Character Kin(World w, House h, Character head, string id, int age = 30)
    {
        var c = new Character { Id = id, Name = "Test " + id, HouseId = h.Id, Age = age, Sex = "male", FatherId = head.Sex == "female" ? "" : head.Id, MotherId = head.Sex == "female" ? head.Id : "" };
        w.Characters[id] = c; h.Members.Add(id); w.Relationship(id, head.Id).Disposition = 30; return c;
    }

    [Fact]
    public void Age_lowers_what_health_can_be_and_health_bears_on_death()
    {
        Assert.Equal(100, LifeSystem.Cap(30)); Assert.Equal(70, LifeSystem.Cap(60)); Assert.Equal(40, LifeSystem.Cap(80)); Assert.Equal(30, LifeSystem.Cap(95));
        var hale = new Character { Health = 90 }; var frail = new Character { Health = 20 };
        Assert.True(LifeSystem.DeathFactor(hale) < 1.0); Assert.True(LifeSystem.DeathFactor(frail) > 2.0);
        Assert.Equal(0.0, LifeSystem.EarlyDeath(hale)); Assert.True(LifeSystem.EarlyDeath(frail) > 0);   // the young die only of what ails them
        var (_, w, me, _, _) = Start(); me.Age = 70; me.Health = 100; LifeSystem.Tick(w, Content); Assert.True(me.Health <= LifeSystem.Cap(70));
    }

    [Fact]
    public void A_wound_costs_health_is_written_down_and_shows_on_the_face()
    {
        var (_, w, me, _, _) = Start(); me.Age = 35; me.Health = 100;
        LifeSystem.Wound(w, me, "when the line broke");
        Assert.Equal(80, me.Health); Assert.Equal(1, me.Life!.Wounds); Assert.Contains(me.Life.Deeds, d => d.Kind == "wound"); Assert.Contains("scar=1", w.Cosmetics["look:" + me.Id]); Assert.True(w.HasFlag(LifeSystem.WoundedNowFlag));
        LifeSystem.Wound(w, me, "again"); Assert.Equal("the Scarred", LifeSystem.Epithet(me));
        // someone of another house is hurt the same, and no record is kept
        var other = w.LivingCharacters().First(c => c.HouseId != me.HouseId); other.Health = 100; LifeSystem.Wound(w, other, "in battle"); Assert.Equal(80, other.Health); Assert.Null(other.Life);
    }

    [Fact]
    public void Rest_and_the_physicians_restore_what_strain_and_illness_took()
    {
        var (tc, w, me, _, ctx) = Start(); me.Age = 40; me.Health = 60; me.Stress = 40; me.Purse = 10; w.PoolsFor(me.Id).Influence = 5;
        Assert.True(Do(tc, ctx, new LifeCommand(me.Id, LifeCommand.Rest))); Assert.Equal(68, me.Health); Assert.Equal(30, me.Stress);
        Assert.False(Do(tc, ctx, new LifeCommand(me.Id, LifeCommand.Rest)));   // once a year
        me.Flags.Add(LifeSystem.IllChar); Assert.True(Do(tc, ctx, new LifeCommand(me.Id, LifeCommand.Physicians))); Assert.Equal(74, me.Health); Assert.Equal(4, me.Purse); Assert.DoesNotContain(LifeSystem.IllChar, me.Flags);
        Assert.False(Do(tc, ctx, new LifeCommand(me.Id, LifeCommand.Physicians)));   // the purse does not hold it
        // great strain wears on the body
        me.Stress = 80; me.Health = 70; w.Turn++; LifeSystem.Tick(w, Content); Assert.True(me.Health < 70);
    }

    [Fact]
    public void What_a_person_does_is_written_down_and_their_renown_and_name_come_of_it()
    {
        var (_, w, me, mine, _) = Start(); var k = Kin(w, mine, me, "doer"); Assert.Equal(0, LifeSystem.Renown(k)); Assert.Equal("", LifeSystem.Epithet(k));
        for (int i = 0; i < 3; i++) LifeSystem.Note(w, k, "victory", "Won a battle in command of a fleet.");
        Assert.Equal(24, LifeSystem.Renown(k)); Assert.Equal("the Victorious", LifeSystem.Epithet(k)); Assert.Equal("little known", LifeSystem.RenownName(LifeSystem.Renown(k)));
        k.CareerTrack = "military"; k.CareerRank = 2; k.Skills["tactics"] = 5; w.Turn++; LifeSystem.Tick(w, Content); w.Turn++; LifeSystem.Tick(w, Content);
        Assert.Contains(k.Life!.Deeds, d => d.Kind == "mastery" && d.Text.Contains("tactics")); int deeds = k.Life.Deeds.Count;
        w.Turn++; LifeSystem.Tick(w, Content); Assert.Equal(deeds, k.Life.Deeds.Count);   // nothing is noted twice
        k.CareerRank = 3; w.Turn++; LifeSystem.Tick(w, Content); Assert.Contains(k.Life.Deeds, d => d.Kind == "rank");
        // weddings, children and the seat are written by the systems that make them
        var bride = w.LivingCharacters().First(c => c.HouseId != mine.Id && c.Sex == "female" && c.Age is >= 18 and <= 40 && DynastySystem.Spouse(w, c) == null && CourtshipSystem.Bar(w, k, c) == null);
        CourtshipSystem.Wed(w, k, bride, CourtshipSystem.Love); Assert.Contains(k.Life.Deeds, d => d.Kind == "love" && d.Text.Contains(bride.Name));
        for (int i = 0; i < 60; i++) LifeSystem.Note(w, k, "child", "Someone was born."); Assert.Equal(LifeSystem.MaxDeeds, k.Life.Deeds.Count); Assert.Equal(3, k.Life.Deeds.Count(d => d.Kind == "victory"));   // the least is forgotten first
    }

    [Fact]
    public void Those_who_have_thought_well_or_ill_of_you_for_years_are_old_friends_and_old_enemies()
    {
        var (_, w, me, _, _) = Start(); me.Age = 40; var others = w.LivingCharacters().Where(c => c.HouseId != me.HouseId && c.Age is >= 20 and <= 40).OrderBy(c => c.Id, StringComparer.Ordinal).Take(2).ToList();
        var friend = others[0]; var enemy = others[1];
        for (int i = 0; i <= LifeSystem.OldTieYears; i++) { w.Relationship(friend.Id, me.Id).Disposition = 60; w.Relationship(enemy.Id, me.Id).Disposition = -60; friend.Health = 100; enemy.Health = 100; w.Turn++; LifeSystem.Tick(w, Content); }
        Assert.Contains(LifeSystem.OldFriendChar, friend.Flags); Assert.Contains(LifeSystem.OldEnemyChar, enemy.Flags); Assert.True(w.HasFlag(LifeSystem.OldFriendFlag)); Assert.True(w.HasFlag(LifeSystem.OldEnemyFlag));
        Assert.Contains(me.Life!.Deeds, d => d.Kind == "friend" && d.Text.Contains(friend.Name));
        int stress = me.Stress; DynastySystem.Die(w, friend.Id); Assert.True(w.HasFlag(LifeSystem.FriendDiedFlag)); Assert.Equal(stress + 8, me.Stress); Assert.DoesNotContain(friend.Id, me.Life.Friends.Keys);
        DynastySystem.Die(w, enemy.Id); Assert.True(w.HasFlag(LifeSystem.EnemyDiedFlag));
        // a friendship that cools is a friendship no longer
        var third = w.LivingCharacters().First(c => c.HouseId != me.HouseId && c.Age is >= 20 and <= 40 && c.Id != friend.Id && c.Id != enemy.Id);
        w.Relationship(third.Id, me.Id).Disposition = 60; w.Turn++; LifeSystem.Tick(w, Content); Assert.Contains(third.Id, me.Life.Friends.Keys);
        w.Relationship(third.Id, me.Id).Disposition = 0; w.Turn++; LifeSystem.Tick(w, Content); Assert.DoesNotContain(third.Id, me.Life.Friends.Keys);
    }

    [Fact]
    public void A_head_may_lay_the_house_down_in_life_and_stay_on_as_its_elder()
    {
        var (tc, w, me, mine, ctx) = Start(); foreach (var k in w.LivingCharacters().Where(k => k.HouseId == mine.Id && k.Id != me.Id).ToList()) k.Alive = false;
        var heir = Kin(w, mine, me, "heir", 30); me.Age = 45; me.Health = 90;
        Assert.Contains("not before", new LifeCommand(me.Id, LifeCommand.Retire).Refusal(ctx));
        me.Age = 62; int legit = mine.Legitimacy;
        Assert.True(Do(tc, ctx, new LifeCommand(me.Id, LifeCommand.Retire)));
        Assert.Equal(heir.Id, w.ProtagonistId); Assert.True(me.Alive); Assert.Contains(LifeSystem.RetiredChar, me.Flags); Assert.Equal(me.Id, heir.MentorId);
        Assert.Equal(Math.Min(100, legit + 2), mine.Legitimacy); Assert.True(w.HasFlag(LifeSystem.RetiredNowFlag)); Assert.Equal("abdication", w.Successions.Last().Cause);
        Assert.Contains(me.Life!.Deeds, d => d.Kind == "retired"); Assert.Contains(heir.Life!.Deeds, d => d.Kind == "head");
        w.Turn++; LifeSystem.Tick(w, Content); Assert.True(w.HasFlag(LifeSystem.ElderFlag));
        // a frail head may go at any age, and a child cannot be handed the house
        var (_, w2, me2, mine2, ctx2) = Start(); foreach (var k in w2.LivingCharacters().Where(k => k.HouseId == mine2.Id && k.Id != me2.Id).ToList()) k.Alive = false;
        var child = Kin(w2, mine2, me2, "child", 10); me2.Age = 40; me2.Health = 20; Assert.Contains("not of age", new LifeCommand(me2.Id, LifeCommand.Retire).Refusal(ctx2)); child.Age = 18; Assert.Null(new LifeCommand(me2.Id, LifeCommand.Retire).Refusal(ctx2));
    }

    [Fact]
    public void The_old_and_the_frail_of_the_house_lay_their_work_down_and_the_ill_send_for_the_physicians()
    {
        var (_, w, me, mine, _) = Start(); me.Age = 40; var old = Kin(w, mine, me, "old", 72); var fleet = w.Fleets.Values.First(f => f.OwnerId == mine.Id); fleet.AdmiralId = old.Id;
        w.Turn++; LifeSystem.Tick(w, Content); Assert.Equal("", fleet.AdmiralId); Assert.Contains(LifeSystem.RetiredChar, old.Flags); Assert.Contains(old.Life!.Deeds, d => d.Kind == "retired");
        var sick = Kin(w, mine, me, "sick", 30); sick.Flags.Add(LifeSystem.IllChar); sick.Health = 50; sick.Purse = 10;
        w.Turn++; LifeSystem.Tick(w, Content); Assert.DoesNotContain(LifeSystem.IllChar, sick.Flags); Assert.Equal(4, sick.Purse);   // they paid for it themselves
    }

    [Fact]
    public void What_is_said_at_a_funeral_is_made_from_the_life()
    {
        var (_, w, me, mine, _) = Start(); var k = Kin(w, mine, me, "mourned", 55); k.NatureTraits.Add("Honorable");
        LifeSystem.Note(w, k, "victory", "Won a battle in command of a fleet."); LifeSystem.Note(w, k, "venture", "Founded a refinery for the house."); LifeSystem.Note(w, k, "child", "Someone was born.");
        int before = w.Funerals.Count; DynastySystem.Die(w, k.Id);
        Assert.Equal(before + 1, w.Funerals.Count); var f = w.Funerals.Last(); Assert.Equal(k.Id, f.PersonId); Assert.Equal(55, f.Age); Assert.True(w.HasFlag(LifeSystem.FuneralFlag));
        Assert.StartsWith(k.Name, f.Lines[0]); Assert.Contains(f.Lines, l => l.Contains("won a battle")); Assert.Contains(f.Lines, l => l.Contains("promise of theirs broken"));
        // nobody is buried by a house they were not of, and the list does not grow without end
        var stranger = w.LivingCharacters().First(c => c.HouseId != mine.Id); DynastySystem.Die(w, stranger.Id); Assert.Equal(before + 1, w.Funerals.Count);
        for (int i = 0; i < 20; i++) { var x = Kin(w, mine, me, "x" + i, 40); DynastySystem.Die(w, x.Id); } Assert.Equal(12, w.Funerals.Count);
    }
}
