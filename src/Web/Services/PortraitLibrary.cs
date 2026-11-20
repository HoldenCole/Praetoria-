using System.Text.Json;

namespace Praetoria.Web.Services;

/// <summary>The painted portraits (docs/ART_DIRECTION.md, "Portraits"; made by tools/art/portraits.py). A person is one identity — a
/// face, a colouring, a way of dressing — painted at five ages, and every identity is of one sex and one ancestry. A character is
/// given an identity by a hash of their id among those of their sex and ancestry (or the one the player chose), and shown at their
/// age, so a face grows old with its owner and a house looks like one. Where the library has no picture the caller draws one.</summary>
public sealed class PortraitLibrary
{
    public static readonly string[] Bands = { "child", "youth", "adult", "older", "elder" };
    public static readonly string[] BandNames = { "a child", "in youth", "grown", "in middle age", "in old age" };
    private readonly Dictionary<string, List<(string name, HashSet<string> bands)>> _pools = new();   // "male|northern" ...
    public int Count { get; private set; }

    public static string Band(int age) => age < 13 ? "child" : age < 20 ? "youth" : age < 46 ? "adult" : age < 62 ? "older" : "elder";
    /// <summary>An age in the middle of a band: for showing a face as it will be.</summary>
    public static int AgeOf(string band) => band switch { "child" => 8, "youth" => 16, "adult" => 32, "older" => 54, _ => 74 };

    public static PortraitLibrary Parse(string manifestJson)
    {
        var lib = new PortraitLibrary();
        using var doc = JsonDocument.Parse(manifestJson);
        if (!doc.RootElement.TryGetProperty("faces", out var faces)) return lib;
        foreach (var f in faces.EnumerateObject().OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            if (f.Value.ValueKind != JsonValueKind.Object) continue;
            string sex = f.Value.TryGetProperty("sex", out var s) ? s.GetString() ?? "" : "", anc = f.Value.TryGetProperty("ancestry", out var a) ? a.GetString() ?? "" : "";
            var bands = f.Value.TryGetProperty("ages", out var ag) ? ag.EnumerateArray().Select(b => b.GetString() ?? "").ToHashSet() : new HashSet<string>();
            if (sex.Length == 0 || bands.Count == 0) continue;
            string key = sex + "|" + anc; if (!lib._pools.TryGetValue(key, out var pool)) lib._pools[key] = pool = new();
            pool.Add((f.Name, bands)); lib.Count += bands.Count;
        }
        return lib;
    }

    /// <summary>The faces of a sex and an ancestry. An ancestry the library has not painted falls to the faces of that sex by the
    /// dice of the id, so nobody is left without a picture; an empty ancestry does the same.</summary>
    private List<(string name, HashSet<string> bands)>? Pool(string sex, string ancestry, string id)
    {
        string s = sex == "female" ? "female" : "male";
        if (ancestry.Length > 0 && _pools.TryGetValue(s + "|" + ancestry, out var pool)) return pool;
        var keys = _pools.Keys.Where(k => k.StartsWith(s + "|", StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        return keys.Count == 0 ? null : _pools[keys[(int)(Hash("any|" + id) % (uint)keys.Count)]];
    }

    /// <summary>The picture for this person now, or null if the library holds none for them. <paramref name="chosen"/> is a face the
    /// player picked for themselves, counted among the faces of their sex and ancestry; otherwise the id decides.</summary>
    public string? Url(string id, string sex, int age, string ancestry, int chosen = -1)
    {
        var pool = Pool(sex, ancestry, id); if (pool == null) return null;
        var (name, bands) = pool[(int)((chosen >= 0 ? (uint)chosen : Hash(id)) % (uint)pool.Count)];
        int want = Array.IndexOf(Bands, Band(age));
        // the nearest age painted: the right one, else the next older, else the next younger; never a grown face on a child
        for (int step = 0; step < Bands.Length; step++)
            foreach (int i in new[] { want + step, want - step })
                if (i >= 0 && i < Bands.Length && bands.Contains(Bands[i]) && (Bands[i] != "child" || age < 13) && (age >= 13 || Bands[i] is "child" or "youth"))
                    return $"img/portraits/{name}_{Bands[i]}.webp";
        return null;
    }

    /// <summary>How many faces there are to choose from for a sex and an ancestry.</summary>
    public int Choices(string sex, string ancestry) => ancestry.Length > 0 && _pools.TryGetValue((sex == "female" ? "female" : "male") + "|" + ancestry, out var pool) ? pool.Count : 0;
    /// <summary>Every face of a sex and an ancestry at an age, in the order a chosen face is counted in: for a player picking their own.</summary>
    public IReadOnlyList<string> Faces(string sex, string ancestry, int age) => Enumerable.Range(0, Choices(sex, ancestry)).Select(i => Url("", sex, age, ancestry, i) ?? "").ToList();
    /// <summary>One chosen face at each of the five ages: who the player will be across a life.</summary>
    public IReadOnlyList<(string when, string url)> Life(string sex, string ancestry, int chosen) =>
        chosen < 0 || Choices(sex, ancestry) == 0 ? Array.Empty<(string, string)>() : Bands.Select((b, i) => (BandNames[i], Url("", sex, AgeOf(b), ancestry, chosen) ?? "")).Where(x => x.Item2.Length > 0).ToList();

    private static uint Hash(string s) { uint h = 2166136261; foreach (char c in s) { h ^= c; h *= 16777619; } h ^= h >> 15; h *= 2246822519; h ^= h >> 13; return h; }
}
