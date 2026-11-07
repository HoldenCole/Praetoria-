using Praetoria.Core.Events;
using Praetoria.Core.Rng;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>An honorific or order (GDD §13): conditions over `self`, effects applied once on investiture.</summary>
public sealed class HonorDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public IReadOnlyList<ICondition> When { get; init; } = Array.Empty<ICondition>();
    public IReadOnlyList<IEffect> Effects { get; init; } = Array.Empty<IEffect>();
}

/// <summary>Grants honors to the protagonist when their conditions first hold (GDD §13 "Honorifics & Orders" —
/// the Merit payoff that is birth-independent). Writes <c>honor:&lt;id&gt;</c> on the character; events read it.</summary>
public sealed class HonorSystem
{
    private readonly IReadOnlyList<HonorDef> _defs;
    public HonorSystem(IReadOnlyList<HonorDef> defs) => _defs = defs;
    public IReadOnlyList<HonorDef> Defs => _defs;
    public bool IsEmpty => _defs.Count == 0;
    public static string Flag(string id) => $"honor:{id}";

    public void Apply(World w, IRng rng)
    {
        var p = w.Protagonist;
        if (p is not { Alive: true }) return;
        var ctx = new EvalContext(w, new Binding(p.Id), rng);
        foreach (var h in _defs)
        {
            if (p.Flags.Contains(Flag(h.Id))) continue;
            if (!Binder.AllHold(h.When, ctx)) continue;
            p.Flags.Add(Flag(h.Id));
            foreach (var e in h.Effects) e.Apply(ctx);
            w.WorldCounters["honors"] = w.Counter("honors") + 1;
            w.Log($"{p.Name} is invested as {h.Name}.");
        }
    }

    public IEnumerable<HonorDef> Held(Character c) => _defs.Where(h => c.Flags.Contains(Flag(h.Id)));
}
