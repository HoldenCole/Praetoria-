namespace Praetoria.Core.Creation;

/// <summary>A named, described identity option (GDD §18 — a nature trait, an aptitude trait, or an ambition).</summary>
public sealed class TraitDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
}

/// <summary>
/// The controlled vocabularies as data (BuildSpec §2 /content/traits; docs/PRAETORIA_VOCAB.md §8a/b/d).
/// Until now the engine took traits as bare strings and only the Python pre-flight knew the locked lists;
/// character creation needs names and descriptions to show, so the lists live here. Empty catalog ⇒ no
/// creation-time validation (bare strings, the pre-0.6 behaviour).
/// </summary>
public sealed class TraitCatalog
{
    public IReadOnlyList<TraitDef> Nature { get; }
    public IReadOnlyList<TraitDef> Aptitude { get; }
    public IReadOnlyList<TraitDef> Ambitions { get; }

    public TraitCatalog(IEnumerable<TraitDef> nature, IEnumerable<TraitDef> aptitude, IEnumerable<TraitDef> ambitions)
    {
        Nature = nature.ToList();
        Aptitude = aptitude.ToList();
        Ambitions = ambitions.ToList();
    }

    public static TraitCatalog Empty { get; } =
        new(Array.Empty<TraitDef>(), Array.Empty<TraitDef>(), Array.Empty<TraitDef>());

    public bool IsEmpty => Nature.Count == 0 && Aptitude.Count == 0 && Ambitions.Count == 0;
    public bool IsNature(string id) => Nature.Any(t => t.Id == id);
    public bool IsAptitude(string id) => Aptitude.Any(t => t.Id == id);
    public bool IsAmbition(string id) => Ambitions.Any(t => t.Id == id);
}
