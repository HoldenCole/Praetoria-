using Praetoria.Core.Commands;
using Praetoria.Core.State;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The build intent (GDD §17) runs through the same command bus as everything else
/// (BuildSpec §7): it spends the house treasury, fills a slot, and is gated on legality.</summary>
public class DomainCommandTests
{
    private static CommandContext Ctx(World w) => new(w, TestContent.Rng());

    [Fact]
    public void Build_SpendsTreasury_AndFillsSlot()
    {
        var w = TestContent.EconomyWorld(materials: 8);
        var exec = new CommandExecutor();

        bool ran = exec.TryExecute(
            new BuildCommand("self", "barony", "farm", TestContent.EconomyCatalog()), Ctx(w));

        Assert.True(ran);
        Assert.Contains("farm", w.Holding("barony")!.Buildings);
        Assert.Equal(3, w.House("vega")!.Treasury.Materials);   // 8 - 5 cost
        Assert.Contains(exec.Log, l => l.Contains("build farm at barony"));
    }

    [Fact]
    public void Build_Blocked_WhenTreasuryTooLow()
    {
        var w = TestContent.EconomyWorld(materials: 4);          // farm costs 5
        var cmd = new BuildCommand("self", "barony", "farm", TestContent.EconomyCatalog());
        Assert.False(cmd.CanExecute(Ctx(w)));
        Assert.False(new CommandExecutor().TryExecute(cmd, Ctx(w)));
        Assert.Empty(w.Holding("barony")!.Buildings);
    }

    [Fact]
    public void Build_Blocked_WhenBuildingDoesNotFitSpecialization()
    {
        var w = TestContent.EconomyWorld(materials: 20);
        w.Holding("barony")!.Specialization = "forge";           // farm requires "agri"
        var cmd = new BuildCommand("self", "barony", "farm", TestContent.EconomyCatalog());
        Assert.False(cmd.CanExecute(Ctx(w)));
    }

    [Fact]
    public void Build_Blocked_WhenAllSlotsFull()
    {
        var w = TestContent.EconomyWorld(credits: 10);
        w.Holding("barony")!.Specialization = "forge";           // forge has exactly 1 slot
        w.Holding("barony")!.Buildings.Add("outpost");           // slot occupied
        // A different, fitting, affordable building is still refused — no free slot.
        var cmd = new BuildCommand("self", "barony", "depot", TestContent.EconomyCatalog());
        Assert.False(cmd.CanExecute(Ctx(w)));
    }

    [Fact]
    public void Build_Blocked_WhenAlreadyBuilt()
    {
        var w = TestContent.EconomyWorld(credits: 10);           // agri has 2 slots — room remains
        w.Holding("barony")!.Buildings.Add("outpost");
        var dup = new BuildCommand("self", "barony", "outpost", TestContent.EconomyCatalog());
        Assert.False(dup.CanExecute(Ctx(w)));                    // refused as a duplicate, not for space
    }

    [Fact]
    public void Build_Blocked_OnAHoldingTheActorsHouseDoesNotOwn()
    {
        var w = TestContent.EconomyWorld(materials: 8);
        w.Holding("barony")!.OwnerId = "someone_else";
        var cmd = new BuildCommand("self", "barony", "farm", TestContent.EconomyCatalog());
        Assert.False(cmd.CanExecute(Ctx(w)));
    }
}
