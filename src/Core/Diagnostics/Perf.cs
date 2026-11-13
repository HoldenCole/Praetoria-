using System.Diagnostics;

namespace Praetoria.Core.Diagnostics;

/// <summary>
/// A stopwatch by name, off unless asked for. The browser build runs interpreted, about a hundred times slower than native, so a
/// system that costs ten milliseconds here costs a second there: this is how the turn is kept honest. Wrap a block in
/// <c>using (Perf.Time("name"))</c>; read <see cref="Report"/> afterwards. When <see cref="Enabled"/> is false it costs one branch.
/// </summary>
public static class Perf
{
    // per thread, like ReadCache: a test that turns it on must not time another test's world
    [ThreadStatic] private static bool _enabled;
    [ThreadStatic] private static Dictionary<string, (double ms, int calls)>? _totals;
    public static bool Enabled { get => _enabled; set => _enabled = value; }
    private static Dictionary<string, (double ms, int calls)> Totals => _totals ??= new();
    private sealed class Scope : IDisposable
    {
        private readonly string _name; private readonly long _start;
        public Scope(string name) { _name = name; _start = Stopwatch.GetTimestamp(); }
        public void Dispose() { double ms = Stopwatch.GetElapsedTime(_start).TotalMilliseconds; var t = Totals.GetValueOrDefault(_name); Totals[_name] = (t.ms + ms, t.calls + 1); }
    }
    public static IDisposable? Time(string name) => Enabled ? new Scope(name) : null;
    public static void Reset() => Totals.Clear();
    /// <summary>Every name with its total milliseconds and calls, costliest first.</summary>
    public static IReadOnlyList<(string name, double ms, int calls)> Report() => Totals.Select(kv => (kv.Key, kv.Value.ms, kv.Value.calls)).OrderByDescending(x => x.ms).ToList();
}
