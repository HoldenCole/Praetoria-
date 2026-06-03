using Praetoria.Core.Rng;
using Praetoria.Core.State;

namespace Praetoria.Core.Commands;

/// <summary>Everything a command needs to act: the World and the shared RNG. The single channel
/// through which both player and NPC intents reach State (BuildSpec §7 command pattern).</summary>
public sealed class CommandContext
{
    public World World { get; }
    public IRng Rng { get; }

    public CommandContext(World world, IRng rng)
    {
        World = world;
        Rng = rng;
    }

    public ActionPools PoolsOf(string actorId) => World.PoolsFor(actorId);
}
