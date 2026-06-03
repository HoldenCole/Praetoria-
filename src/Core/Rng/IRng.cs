namespace Praetoria.Core.Rng;

/// <summary>
/// The single source of randomness in the simulation (BuildSpec §1.5 — deterministic
/// core + seeded RNG). Same seed + same calls = same outcome. The RNG's entire position
/// is captured by <see cref="State"/>, which the World serializes so saves are exact.
/// </summary>
public interface IRng
{
    /// <summary>Opaque internal state — serialize this to reproduce the stream exactly.</summary>
    ulong State { get; }

    /// <summary>Uniform double in [0, 1).</summary>
    double NextDouble();

    /// <summary>Uniform integer in [minInclusive, maxExclusive). Returns min if range is empty.</summary>
    int NextInt(int minInclusive, int maxExclusive);
}
