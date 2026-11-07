using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>GDD §18: acting against your nature costs stress. The table (act → nature trait → stress delta) is data in
/// /content/traits/friction.json; the engine calls <see cref="Apply"/> at the acts it recognises.</summary>
public sealed class FrictionTable
{
    private readonly Dictionary<string, Dictionary<string, int>> _table;
    public FrictionTable(Dictionary<string, Dictionary<string, int>> table) => _table = table;
    public static FrictionTable Empty { get; } = new(new());
    public bool IsEmpty => _table.Count == 0;
    public IReadOnlyDictionary<string, int> For(string act) => _table.TryGetValue(act, out var t) ? t : new Dictionary<string, int>();

    /// <summary>Charge (or relieve) stress on a character for an act. Returns the net delta.</summary>
    public int Apply(World w, Character? c, string act)
    {
        if (c is not { Alive: true }) return 0;
        int delta = 0;
        foreach (var kv in For(act)) if (c.NatureTraits.Contains(kv.Key)) delta += kv.Value;
        if (delta == 0) return 0;
        c.Stress = Math.Clamp(c.Stress + delta, 0, 100);
        if (delta > 0) w.Log($"{c.Name} bears the weight of it ({act.Replace('_', ' ')}: +{delta} stress).");
        return delta;
    }
}
