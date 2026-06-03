namespace Praetoria.Core.Rng;

/// <summary>
/// SplitMix64 — a tiny, fast, well-distributed PRNG with a 64-bit state. Chosen over
/// System.Random because its algorithm is fixed and platform-independent: the stream is
/// identical everywhere, which is what determinism + save integrity (BuildSpec §1.5) require.
/// The full position is the single <see cref="State"/> word, so it serializes trivially.
/// </summary>
public sealed class SplitMix64Rng : IRng
{
    private ulong _state;

    public SplitMix64Rng(ulong seed) => _state = seed;

    public ulong State => _state;

    private ulong NextRaw()
    {
        _state += 0x9E3779B97F4A7C15UL;
        ulong z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public double NextDouble() => (NextRaw() >> 11) * (1.0 / 9007199254740992.0);

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        ulong range = (ulong)((long)maxExclusive - minInclusive);
        return minInclusive + (int)(NextRaw() % range);
    }
}
