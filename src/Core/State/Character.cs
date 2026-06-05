namespace Praetoria.Core.State;

/// <summary>
/// A person in the world (BuildSpec §3). Entities are plain data with composed fields —
/// "composition over deep inheritance". Traits are split per GDD §18 into Nature
/// (constraining, stress-bearing) and Aptitude (gating/flavour). Skills crystallise in the
/// Academy Crucible (§13). Everything here is serialisable; the World is the save.
/// </summary>
public sealed class Character
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string HouseId { get; set; } = "";
    public int Age { get; set; }
    public bool Alive { get; set; } = true;

    /// <summary>"male" / "female" (or "" if unspecified). Used by the dynasty layer for births and
    /// matrilineal/patrilineal succession (GDD §14/§18).</summary>
    public string Sex { get; set; } = "";

    /// <summary>Blood parents, "" if unknown/founder. Carry succession rights (GDD §14 blood ties).</summary>
    public string MotherId { get; set; } = "";
    public string FatherId { get; set; } = "";

    // Career ladder (GDD §13) — resets each generation. V1 tracks: military / stewardship / law.
    public string CareerTrack { get; set; } = "";
    public int CareerRank { get; set; }

    // GDD §18 two trait classes.
    public HashSet<string> NatureTraits { get; set; } = new();
    public HashSet<string> AptitudeTraits { get; set; } = new();

    public Dictionary<string, int> Skills { get; set; } = new();

    public int Stress { get; set; }
    public string Ambition { get; set; } = "";

    /// <summary>
    /// Per-character status flags / armed gates (e.g. "feud", "humiliated"). The engine
    /// arms and clears these via consequences — this is how unscripted storylines chain.
    /// </summary>
    public HashSet<string> Flags { get; set; } = new();

    public int Skill(string key) => Skills.TryGetValue(key, out var v) ? v : 0;
}
