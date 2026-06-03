using System.Text;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>
/// The Milestone-4 (headless slice) acceptance property: the domain economy runs inside the turn
/// cycle and stays exactly reproducible from a seed (GDD §17, BuildSpec §97). Accrual is RNG-free,
/// so two runs of the same seed produce an identical treasury/holdings ledger; and a build issued
/// through the command bus compounds into later turns.
/// </summary>
public class EconomyDeterminismTests
{
    private static (TurnController tc, World world, ContentDatabase content) NewRun(ulong seed)
    {
        var root = ContentLocator.FindContentDir();
        var content = ContentLoader.LoadFromDirectory(root);
        var scenario = ScenarioLoader.LoadFromContent(root, "academy_crucible");
        var world = WorldBuilder.FromScenario(scenario, seed);
        return (new TurnController(world, content), world, content);
    }

    private static string Ledger(World w)
    {
        var sb = new StringBuilder();
        foreach (var h in w.Houses.Values.OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            var t = h.Treasury;
            sb.Append(h.Id).Append(":C").Append(t.Credits).Append("M").Append(t.Materials)
              .Append("P").Append(t.Manpower).Append("I").Append(t.Influence).Append("X").Append(t.Exotics).Append(';');
        }
        foreach (var h in w.Holdings.Values.OrderBy(h => h.Id, StringComparer.Ordinal))
            sb.Append(h.Id).Append("=pop").Append(h.Population).Append("/u").Append(h.Unrest)
              .Append("/b").Append(string.Join(',', h.Buildings)).Append(';');
        return sb.ToString();
    }

    private static string RunSixTurns(ulong seed)
    {
        var (tc, world, _) = NewRun(seed);
        for (int t = 0; t < 6; t++) { tc.BeginTurn(); tc.EndTurn(); }
        return Ledger(world);
    }

    [Fact]
    public void Loader_PopulatesTheHoldingCatalogAndScenarioHoldings()
    {
        var (_, world, content) = NewRun(1);
        Assert.False(content.Holdings.IsEmpty);
        Assert.NotNull(content.Holdings.Spec("agri_world"));
        Assert.True(world.Holdings.ContainsKey("vega_barony"));
    }

    [Fact]
    public void TurnCycle_AccruesTreasury_FromHoldings()
    {
        var (tc, world, _) = NewRun(1);
        int before = world.House("vega")!.Treasury.Manpower;
        tc.BeginTurn();
        tc.EndTurn();
        Assert.True(world.House("vega")!.Treasury.Manpower > before, "Agri-world should yield manpower each turn.");
    }

    [Fact]
    public void Ledger_IsIdentical_FromSameSeed()
    {
        Assert.Equal(RunSixTurns(2024), RunSixTurns(2024));
    }

    [Fact]
    public void BuildCommand_CompoundsYield_AcrossTurns()
    {
        var (tc, world, content) = NewRun(7);
        tc.BeginTurn();

        // Invest in the agri-world's farm complex through the bus.
        var ctx = new CommandContext(world, WorldBuilder.RngFor(world));
        bool built = tc.Executor.TryExecute(
            new BuildCommand("vega_marcus", "vega_barony", "farm_complex", content.Holdings), ctx);
        Assert.True(built);
        tc.EndTurn();

        int manpowerYieldWithFarm = MeasureManpowerYield(world, tc);
        Assert.True(manpowerYieldWithFarm >= 5, "Agri (3) + farm complex (2) should yield ≥5 manpower/turn.");
    }

    private static int MeasureManpowerYield(World world, TurnController tc)
    {
        int before = world.House("vega")!.Treasury.Manpower;
        tc.BeginTurn();
        int after = world.House("vega")!.Treasury.Manpower;
        tc.EndTurn();
        return after - before;
    }
}
