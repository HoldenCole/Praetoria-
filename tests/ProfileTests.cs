using Praetoria.Core.Data;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>A person's profile, the window any portrait opens (docs/ART_DIRECTION.md, "The shell, the windows and the art"): who
/// they are, what they are, what they think of you and why, whose they are.</summary>
public class ProfileTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static World Start() => WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 7);

    [Fact]
    public void YourOwnProfile_SaysWhoYouAre_AndHasNoRegardForYourself()
    {
        var w = Start(); var me = w.Protagonist!;
        var p = ProfileProjection.Build(w, Content, me.Id)!;
        Assert.True(p.IsYou); Assert.True(p.IsHead); Assert.Equal(me.Name, p.Name);
        Assert.Null(p.TheirsOfYou); Assert.Null(p.YoursOfThem);
        Assert.Contains(p.Offices, o => o.StartsWith("Head of "));
        Assert.Contains(me.Age.ToString(), p.Story);
        Assert.NotEmpty(p.Station);
    }

    [Fact]
    public void ARelativesProfile_ListsTheirKin_AndWhyTheyThinkOfYouAsTheyDo()
    {
        var w = Start(); var me = w.Protagonist!;
        var child = w.Characters.Values.Where(c => c.FatherId == me.Id || c.MotherId == me.Id).OrderBy(c => c.Id, StringComparer.Ordinal).First();
        var p = ProfileProjection.Build(w, Content, child.Id)!;
        Assert.False(p.IsYou);
        Assert.Contains(p.Kin, k => k.Id == me.Id && k.Relation is "father" or "mother");
        Assert.NotNull(p.TheirsOfYou);
        Assert.Contains("your child", p.TheirsOfYou!.Reasons); Assert.Contains("of your own house", p.TheirsOfYou.Reasons);
        Assert.Equal(ProfileProjection.RegardWord(p.TheirsOfYou.Disposition), p.TheirsOfYou.Word);
    }

    [Fact]
    public void TheWordForARegard_RunsFromAnEnemyToTheDevoted()
    {
        Assert.Equal("an enemy", ProfileProjection.RegardWord(-80)); Assert.Equal("hostile", ProfileProjection.RegardWord(-40)); Assert.Equal("indifferent", ProfileProjection.RegardWord(0));
        Assert.Equal("a friend", ProfileProjection.RegardWord(35)); Assert.Equal("devoted", ProfileProjection.RegardWord(75));
    }

    [Fact]
    public void EveryoneInTheWorld_HasAProfile_AndNobodyUnknownDoes()
    {
        var w = Start();
        foreach (var c in w.Characters.Values) { var p = ProfileProjection.Build(w, Content, c.Id); Assert.NotNull(p); Assert.NotEmpty(p!.Name); Assert.NotEmpty(p.Story); Assert.True(p.Skills.Count <= ProfileProjection.SkillsShown); }
        Assert.Null(ProfileProjection.Build(w, Content, "nobody_at_all"));
        Assert.Null(ProfileProjection.Dress(w, "nobody_at_all"));
    }

    [Fact]
    public void WhatAPortraitShows_FollowsTheWorld()
    {
        var w = Start(); var me = w.Protagonist!;
        var mine = ProfileProjection.Dress(w, me.Id)!;
        Assert.True(mine.Head); Assert.False(mine.Foreign); Assert.Equal(0, mine.Mood); Assert.Equal(me.CareerRank, mine.Rank);
        var other = w.LivingCharacters().First(c => c.HouseId != me.HouseId && !w.Foreign(c.HouseId));
        w.Relationship(other.Id, me.Id).Disposition = -70; Assert.Equal(-2, ProfileProjection.Mood(w, other.Id));
        w.Relationship(other.Id, me.Id).Disposition = 30; Assert.Equal(1, ProfileProjection.Mood(w, other.Id));
    }
}
