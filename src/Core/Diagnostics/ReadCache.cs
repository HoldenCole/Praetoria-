namespace Praetoria.Core.Diagnostics;

/// <summary>
/// A memo for pure questions about the world (a house's Senate weight, its law table, who dominates a region) that is only alive
/// while nothing can change: for the length of one projection, or in the browser between one player action and the next. A screen
/// asks the same question hundreds of times in a redraw, and interpreted code makes each one dear.
/// <para>It is closed by default, and then every question is answered fresh: the simulation, the tests and the tools never see it.
/// Whoever opens it must close it before the world changes.</para>
/// </summary>
public static class ReadCache
{
    // per thread: test runners build several worlds at once, and one thread's open window must never answer another's questions
    [ThreadStatic] private static Dictionary<string, object?>? _store;
    [ThreadStatic] private static bool _active;
    private static Dictionary<string, object?> Store => _store ??= new();
    public static bool Active => _active;
    public static void Open() { Store.Clear(); _active = true; }
    public static void Close() { _active = false; Store.Clear(); }
    public static T Get<T>(string key, Func<T> make)
    {
        if (!Active) return make();
        if (Store.TryGetValue(key, out var hit)) return (T)hit!;
        var made = make(); Store[key] = made; return made;
    }
}
