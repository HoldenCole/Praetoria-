using Praetoria.Core.Data;
using Praetoria.Core.Presentation;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Every report opens on a painting chosen by its subject (docs/VISUAL_SYSTEM.md, rule 7): the audience hall is the
/// painting for visitors, clients and gifts, not the one most reports fall back to, so the outliner's crops tell reports
/// apart.</summary>
public class SceneArtTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());

    private static string Title(string id) => Content.Texts.TryGetValue(id, out var t) ? t.Title ?? "" : "";

    [Fact]
    public void FewEventsFallBackToTheAudienceHall()
    {
        var hall = Content.Events.Where(e => SceneArt.For(e, Title(e.Id)) == "audience").Select(e => e.Id).ToList();
        double share = (double)hall.Count / Content.Events.Count;
        Assert.True(share <= 0.15, $"{hall.Count} of {Content.Events.Count} events open on the audience hall: {string.Join(", ", hall.Take(40))}");
    }

    [Fact]
    public void AnEventsOwnSceneIsAPaintingThereIs()
    {
        var bad = Content.Events.Where(e => e.Scene != "" && !SceneArt.Scenes.Contains(e.Scene)).Select(e => $"{e.Id}: {e.Scene}").ToList();
        Assert.Empty(bad);
    }

    [Theory]
    [InlineData("the_failing_reactor", "shipyard")]
    [InlineData("a_friend_is_buried", "funeral")]
    [InlineData("a_general_marches", "bridge")]
    public void TheSubjectChoosesThePainting(string id, string scene)
    {
        var e = Content.Events.Single(x => x.Id == id);
        Assert.Equal(scene, SceneArt.For(e, Title(id)));
    }
}
