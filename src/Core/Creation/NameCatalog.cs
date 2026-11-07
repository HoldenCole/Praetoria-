namespace Praetoria.Core.Creation;

/// <summary>Given names by culture and sex (/content/names). Children born in play are named from here.</summary>
public sealed class NameCatalog
{
    private readonly Dictionary<string, (IReadOnlyList<string> male, IReadOnlyList<string> female)> _cultures;
    public NameCatalog(Dictionary<string, (IReadOnlyList<string>, IReadOnlyList<string>)> cultures) => _cultures = cultures;
    public static NameCatalog Empty { get; } = new(new());
    public bool IsEmpty => _cultures.Count == 0;
    public IEnumerable<string> Cultures => _cultures.Keys.OrderBy(k => k, StringComparer.Ordinal);

    /// <summary>A given name for the culture (falls back to the first culture, then to a numbered name).</summary>
    public string Pick(string culture, string sex, Rng.IRng rng)
    {
        if (!_cultures.TryGetValue(culture, out var lists) && _cultures.Count > 0)
            lists = _cultures[Cultures.First()];
        var list = sex == "female" ? lists.female : lists.male;
        if (list == null || list.Count == 0) return "Scion";
        return list[rng.NextInt(0, list.Count)];
    }
}
