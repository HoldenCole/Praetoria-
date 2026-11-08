using System.Text.Json;

namespace Praetoria.Core.Data;

/// <summary>An interest group (content/politics/estates.json) with the effects it brings a house it is loyal or hostile to.</summary>
public sealed class EstateDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public IReadOnlyDictionary<string, int> Loyal { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> Hostile { get; init; } = new Dictionary<string, int>();
}

/// <summary>One setting of a law: its effects (modifier keys) and what each estate thinks of it.</summary>
public sealed class LawOptionDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public bool Default { get; init; }
    public IReadOnlyDictionary<string, int> Effects { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> Opinions { get; init; } = new Dictionary<string, int>();
}

/// <summary>A law (content/politics/laws.json): the position it needs, its options, its cost and cooldown.</summary>
public sealed class LawDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public string Description { get; init; } = "";
    /// <summary>house · world · region · chair · office:&lt;id&gt; · emperor · bench.</summary>
    public string Scope { get; init; } = "house";
    public int Cost { get; init; } = 1;
    public int Cooldown { get; init; } = 6;
    /// <summary>The option in force is also the house's tax policy.</summary>
    public bool TaxPolicy { get; init; }
    /// <summary>The effects fall on every house in the Reach, not the enacting one.</summary>
    public bool Reach { get; init; }
    public IReadOnlyList<LawOptionDef> Options { get; init; } = Array.Empty<LawOptionDef>();
    public LawOptionDef? Option(string id) => Options.FirstOrDefault(o => o.Id == id);
    public LawOptionDef DefaultOption => Options.FirstOrDefault(o => o.Default) ?? Options[0];
}

public sealed class PolicyCatalog
{
    private readonly Dictionary<string, LawDef> _laws; private readonly Dictionary<string, EstateDef> _estates;
    public PolicyCatalog(IEnumerable<LawDef> laws, IEnumerable<EstateDef> estates) { Laws = laws.ToList(); Estates = estates.ToList(); _laws = Laws.ToDictionary(l => l.Id); _estates = Estates.ToDictionary(e => e.Id); }
    public static PolicyCatalog Empty { get; } = new(Array.Empty<LawDef>(), Array.Empty<EstateDef>());
    public IReadOnlyList<LawDef> Laws { get; }
    public IReadOnlyList<EstateDef> Estates { get; }
    public LawDef? Law(string id) => _laws.TryGetValue(id, out var l) ? l : null;
    public EstateDef? Estate(string id) => _estates.TryGetValue(id, out var e) ? e : null;
    public bool IsEmpty => Laws.Count == 0;

    private static Dictionary<string, int> Ints(JsonElement el, string name)
    {
        var d = new Dictionary<string, int>();
        if (el.TryGetProperty(name, out var o) && o.ValueKind == JsonValueKind.Object) foreach (var p in o.EnumerateObject()) if (p.Value.ValueKind == JsonValueKind.Number) d[p.Name] = p.Value.GetInt32();
        return d;
    }

    public static PolicyCatalog LoadFromDirectory(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "politics"); if (!Directory.Exists(dir)) return Empty;
        var opts = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        var laws = new List<LawDef>(); var estates = new List<EstateDef>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json").OrderBy(p => p, StringComparer.Ordinal))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file), opts); var root = doc.RootElement;
            if (root.TryGetProperty("estates", out var es) && es.ValueKind == JsonValueKind.Array)
                foreach (var el in es.EnumerateArray())
                    estates.Add(new EstateDef { Id = ConditionParser.Str(el, "id"), Name = ConditionParser.Str(el, "name"), Description = ConditionParser.OptStr(el, "description", ""), Loyal = Ints(el, "loyal"), Hostile = Ints(el, "hostile") });
            if (root.TryGetProperty("laws", out var ls) && ls.ValueKind == JsonValueKind.Array)
                foreach (var el in ls.EnumerateArray())
                {
                    var options = new List<LawOptionDef>();
                    if (el.TryGetProperty("options", out var os) && os.ValueKind == JsonValueKind.Array)
                        foreach (var o in os.EnumerateArray())
                            options.Add(new LawOptionDef { Id = ConditionParser.Str(o, "id"), Name = ConditionParser.Str(o, "name"), Default = ConditionParser.Bool(o, "default", false), Effects = Ints(o, "effects"), Opinions = Ints(o, "opinions") });
                    laws.Add(new LawDef
                    {
                        Id = ConditionParser.Str(el, "id"), Name = ConditionParser.Str(el, "name"), Category = ConditionParser.OptStr(el, "category", ""), Description = ConditionParser.OptStr(el, "description", ""),
                        Scope = ConditionParser.OptStr(el, "scope", "house"), Cost = ConditionParser.Int(el, "cost", 1), Cooldown = ConditionParser.Int(el, "cooldown", 6),
                        TaxPolicy = ConditionParser.Bool(el, "taxPolicy", false), Reach = ConditionParser.Bool(el, "reach", false), Options = options
                    });
                }
        }
        return new PolicyCatalog(laws, estates);
    }
}
