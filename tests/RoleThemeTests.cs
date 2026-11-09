using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The role register (GDD Art Direction): the career track picks the chrome, the rail order, the desk's first advisor and the landing screen.</summary>
public class RoleThemeTests
{
    private static World Rings(int unrest) { var m = typeof(RegionSystemTests).GetMethod("Rings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!; return (World)m.Invoke(null, new object[] { unrest })!; }

    [Fact]
    public void TheRegister_FollowsTheProtagonistsTrack_AndNoneWithoutOne()
    {
        var w = Rings(0); var p = w.Protagonist!;
        p.CareerTrack = ""; Assert.Equal(RoleTheme.None, RoleTheme.Of(w)); Assert.Equal("office", RoleTheme.Home(RoleTheme.Of(w)));
        p.CareerTrack = "military"; Assert.Equal(RoleTheme.Military, RoleTheme.Of(w)); Assert.Equal("DISPATCH", RoleTheme.Kicker(RoleTheme.Of(w))); Assert.Equal("military", RoleTheme.Home(RoleTheme.Of(w))); Assert.Equal("Admiral", RoleTheme.AdvisorOrder(RoleTheme.Of(w))[0]);
        p.CareerTrack = "stewardship"; Assert.Equal("LEDGER", RoleTheme.Kicker(RoleTheme.Of(w))); Assert.Equal("house", RoleTheme.Home(RoleTheme.Of(w))); Assert.Equal("Steward", RoleTheme.AdvisorOrder(RoleTheme.Of(w))[0]);
        p.CareerTrack = "law"; Assert.Equal("THE COURT", RoleTheme.Kicker(RoleTheme.Of(w))); Assert.Equal("senate", RoleTheme.Home(RoleTheme.Of(w))); Assert.Equal("Chancellor", RoleTheme.AdvisorOrder(RoleTheme.Of(w))[0]);
    }

    [Fact]
    public void TheRail_PutsTheOfficeFirst_ThenTheRegistersScreens_ThenTheRest_WithoutLosingAny()
    {
        var screens = new[] { "office", "character", "galaxy", "planets", "senate", "house", "laws", "military", "dynasty" };
        var law = RoleTheme.OrderScreens(RoleTheme.Law, screens, s => s).ToList();
        Assert.Equal(new[] { "office", "senate", "laws", "character", "galaxy", "planets", "house", "military", "dynasty" }, law);
        var mil = RoleTheme.OrderScreens(RoleTheme.Military, screens, s => s).ToList();
        Assert.Equal("military", mil[1]); Assert.Equal("galaxy", mil[2]); Assert.Equal(screens.Length, mil.Count); Assert.Equal(screens.OrderBy(x => x).ToList(), mil.OrderBy(x => x).ToList());
        Assert.Equal(screens, RoleTheme.OrderScreens(RoleTheme.None, screens, s => s).ToList());
    }
}
