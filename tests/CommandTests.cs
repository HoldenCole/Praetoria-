using Praetoria.Core.Commands;
using Praetoria.Core.Events;
using Praetoria.Core.State;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The command bus is the single mutation path (BuildSpec §7): it guards on CanExecute,
/// spends pools, and logs for replay.</summary>
public class CommandTests
{
    private static CommandContext Ctx(World w) => new(w, TestContent.Rng());

    [Fact]
    public void Executor_RunsLegalCommand_AndLogsIt()
    {
        var w = TestContent.ThreeCadetWorld();
        w.Pools["self"] = new ActionPools { Agents = 2 };
        var exec = new CommandExecutor();

        bool ran = exec.TryExecute(new TrainCommand("self", "tactics"), Ctx(w));

        Assert.True(ran);
        Assert.Equal(1, w.Char("self")!.Skill("tactics"));
        Assert.Equal(1, w.PoolsFor("self").Agents);     // spent 1 of 2
        Assert.Single(exec.Log);
        Assert.Contains("train tactics", exec.Log[0]);
    }

    [Fact]
    public void Executor_RejectsUnaffordableCommand_AndDoesNotLog()
    {
        var w = TestContent.ThreeCadetWorld();
        w.Pools["self"] = new ActionPools { Agents = 0 };
        var exec = new CommandExecutor();

        bool ran = exec.TryExecute(new TrainCommand("self", "tactics"), Ctx(w));

        Assert.False(ran);
        Assert.Equal(0, w.Char("self")!.Skill("tactics"));
        Assert.Empty(exec.Log);
    }

    [Fact]
    public void ResolveChoiceCommand_SpendsPoolsAndAppliesEffects()
    {
        var w = TestContent.ThreeCadetWorld();
        w.Pools["self"] = new ActionPools { Influence = 2 };

        var def = new EventDef
        {
            Id = "scene",
            Choices = new[]
            {
                new Choice
                {
                    Id = "bribe",
                    Cost = new Dictionary<string, int> { [Pool.Influence] = 2 },
                    Effects = new IEffect[] { new SetWorldFlagEffect("bribed", true) }
                }
            }
        };
        var engine = TestContent.Engine(w, TestContent.Db(new[] { def }));
        var fired = engine.NextEvent(w)!;
        var cmd = new ResolveChoiceCommand(engine, fired, "bribe", "self");
        var exec = new CommandExecutor();

        Assert.True(exec.TryExecute(cmd, Ctx(w)));
        Assert.True(w.HasFlag("bribed"));
        Assert.Equal(0, w.PoolsFor("self").Influence);
    }

    [Fact]
    public void ResolveChoiceCommand_Blocked_WhenPoolsTooLow()
    {
        var w = TestContent.ThreeCadetWorld();
        w.Pools["self"] = new ActionPools { Influence = 1 }; // need 2

        var def = new EventDef
        {
            Id = "scene",
            Choices = new[]
            {
                new Choice
                {
                    Id = "bribe",
                    Cost = new Dictionary<string, int> { [Pool.Influence] = 2 },
                    Effects = new IEffect[] { new SetWorldFlagEffect("bribed", true) }
                }
            }
        };
        var engine = TestContent.Engine(w, TestContent.Db(new[] { def }));
        var fired = engine.NextEvent(w)!;
        var cmd = new ResolveChoiceCommand(engine, fired, "bribe", "self");

        Assert.False(cmd.CanExecute(Ctx(w)));
        Assert.False(new CommandExecutor().TryExecute(cmd, Ctx(w)));
        Assert.False(w.HasFlag("bribed"));
    }
}
