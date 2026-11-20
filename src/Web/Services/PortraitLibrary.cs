using System.Text.Json;

namespace Praetoria.Web.Services;

/// <summary>The painted portraits (docs/ART_DIRECTION.md, "Portraits"; made by tools/art/portraits.py). A person is one identity — a
/// face, a colouring, a way of dressing — seen at five ages. Each character is given an identity by a hash of their id, among those of
/// their sex and of the Reach or beyond it, and shown at their age, so a face grows old with its owner. Where the library has no
/// picture the caller draws one instead.</summary>
public sealed class PortraitLibrary
{
    public static readonly string[] Bands = { "child", "youth", "adult", "older", "elder" };
    private readonly Dictionary<string, List<(string name, HashSet<string> bands)>> _pools = new();   // "mr", "fr", "mx", "fx"
    private readonly Dictionary<string, int> _tones = new();   // the colouring an identity was painted with, light to dark
    public int Count { get; private set; }

    public static string Band(int age) => age < 13 ? "child" : age < 20 ? "youth" : age < 46 ? "adult" : age < 62 ? "older" : "elder";

    public static PortraitLibrary Parse(string manifestJson)
    {
        var lib = new PortraitLibrary();
        using var doc = JsonDocument.Parse(manifestJson);
        if (!doc.RootElement.TryGetProperty("faces", out var faces)) return lib;
        foreach (var f in faces.EnumerateObject().OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            if (f.Name.Length < 3) continue;
            var bands = f.Value.EnumerateArray().Select(b => b.GetString() ?? "").ToHashSet();
            if (!lib._pools.TryGetValue(f.Name[..2], out var pool)) lib._pools[f.Name[..2]] = pool = new();
            pool.Add((f.Name, bands)); lib.Count += bands.Count;
        }
        if (doc.RootElement.TryGetProperty("tones", out var tones)) foreach (var t in tones.EnumerateObject()) lib._tones[t.Name] = t.Value.GetInt32();
        return lib;
    }

    /// <summary>Which identity a person is: by choice, else by their id among the faces of their sex, and among those of a colouring
    /// near <paramref name="tone"/> when one is asked for (a child takes after a parent).</summary>
    private (string name, HashSet<string> bands)? Identity(string id, string sex, bool foreign, int chosen, int tone)
    {
        string s = sex == "female" ? "f" : "m";
        if (!_pools.TryGetValue(s + (foreign ? "x" : "r"), out var pool) && !_pools.TryGetValue(s + "r", out pool)) return null;
        if (chosen >= 0) return pool[chosen % pool.Count];
        if (tone >= 0) { var near = pool.Where(p => _tones.TryGetValue(p.name, out int t) && Math.Abs(t - tone) <= 1).ToList(); if (near.Count > 0) pool = near; }
        return pool[(int)(Hash(id) % (uint)pool.Count)];
    }
    /// <summary>The colouring of the face this person has, or -1 if unknown.</summary>
    public int ToneOf(string id, string sex, bool foreign, int chosen = -1, int tone = -1) => Identity(id, sex, foreign, chosen, tone) is { } f && _tones.TryGetValue(f.name, out int t) ? t : -1;
    public static bool TakesAfterFather(string id) => Hash(id + "|kin") % 2 == 0;

    /// <summary>The picture for this person now, or null if the library holds none for them. <paramref name="chosen"/> is a face the
    /// player picked for themselves; otherwise the id decides.</summary>
    public string? Url(string id, string sex, int age, bool foreign, int chosen = -1, int tone = -1)
    {
        if (Identity(id, sex, foreign, chosen, tone) is not { } face) return null;
        var (name, bands) = face;
        int want = Array.IndexOf(Bands, Band(age));
        // the nearest age the painter has finished: the right one, else the next older, else the next younger
        for (int step = 0; step < Bands.Length; step++)
            foreach (int i in new[] { want + step, want - step })
                if (i >= 0 && i < Bands.Length && bands.Contains(Bands[i]) && (Bands[i] != "child" || age < 13) && (age >= 13 || Bands[i] is "child" or "youth"))
                    return $"img/portraits/{name}_{Bands[i]}.webp";
        return null;
    }

    /// <summary>Every face of a sex at an age, in the order a chosen face is counted in: for a player picking their own.</summary>
    public IReadOnlyList<string> Faces(string sex, int age)
    {
        string key = (sex == "female" ? "f" : "m") + "r";
        return _pools.TryGetValue(key, out var pool) ? Enumerable.Range(0, pool.Count).Select(i => Url("", sex, age, false, i) ?? "").ToList() : Array.Empty<string>();
    }

    private static uint Hash(string s) { uint h = 2166136261; foreach (char c in s) { h ^= c; h *= 16777619; } h ^= h >> 15; h *= 2246822519; h ^= h >> 13; return h; }
}
