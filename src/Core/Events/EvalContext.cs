using Praetoria.Core.Data;
using Praetoria.Core.Rng;
using Praetoria.Core.State;

namespace Praetoria.Core.Events;

/// <summary>
/// Everything a condition or effect needs to read/write: the World, the current role
/// <see cref="Binding"/>, and the RNG. Conditions reference characters by role; this context
/// resolves those roles to entities. Effects mutate the World through here (the only path —
/// keeps consequence-application in one place, BuildSpec §4).
/// </summary>
public sealed class EvalContext
{
    public World World { get; }
    public Binding Binding { get; }
    public IRng Rng { get; }

    /// <summary>The loaded content catalogs, when the caller has them (career ladders cap ranks and name
    /// rungs). Null in catalog-free contexts (tests, crisis gates): effects then fall back to bare ints.</summary>
    public ContentDatabase? Content { get; }

    public EvalContext(World world, Binding binding, IRng rng, ContentDatabase? content = null)
    {
        World = world;
        Binding = binding;
        Rng = rng;
        Content = content;
    }

    /// <summary>Resolve a role name to a live Character, or null if unbound/missing/dead-ref.</summary>
    public Character? Actor(string role)
    {
        if (!Binding.TryGet(role, out var id)) return null;
        return World.Char(id);
    }

    public EvalContext WithBinding(Binding binding) => new(World, binding, Rng, Content);
}
