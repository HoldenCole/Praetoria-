using System.Text.Json;

namespace Praetoria.Core.Data;

/// <summary>One setting of a local law: what it does on the one world that keeps it, and what each estate thinks of it.</summary>
public sealed class LocalLawOptionDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    /// <summary>How it is named in a list of a world's laws: "a garrison", "the tithe remitted".</summary>
    public string Short { get; init; } = "";
    public bool Default { get; init; }
    public IReadOnlyDictionary<string, int> Effects { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> Opinions { get; init; } = new Dictionary<string, int>();
}

/// <summary>A law for one world (content/politics/local_laws.json; docs/POLITICS.md, "Local law").</summary>
public sealed class LocalLawDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    /// <summary>The real law it is drawn from, in a line.</summary>
    public string Model { get; init; } = "";
    public int Cost { get; init; } = 1;
    public int Cooldown { get; init; } = 4;
    public IReadOnlyList<LocalLawOptionDef> Options { get; init; } = Array.Empty<LocalLawOptionDef>();
    public LocalLawOptionDef? Option(string id) => Options.FirstOrDefault(o => o.Id == id);
    public LocalLawOptionDef DefaultOption => Options.FirstOrDefault(o => o.Default) ?? Options[0];
}

/// <summary>The local laws, in the order the content gives them. Carried by <see cref="PolicyCatalog.Local"/>, so that every rule
/// that has the laws of the houses has the laws of the worlds too.</summary>
public sealed class LocalLawCatalog
{
    private readonly Dictionary<string, LocalLawDef> _laws;
    public LocalLawCatalog(IEnumerable<LocalLawDef> laws) { Laws = laws.Where(l => l.Options.Count > 0).ToList(); _laws = Laws.ToDictionary(l => l.Id); }
    public static LocalLawCatalog Empty { get; } = new(Array.Empty<LocalLawDef>());
    public IReadOnlyList<LocalLawDef> Laws { get; }
    public LocalLawDef? Law(string id) => _laws.TryGetValue(id, out var l) ? l : null;
    public bool IsEmpty => Laws.Count == 0;

    private static Dictionary<string, int> Ints(JsonElement el, string name)
    {
        var d = new Dictionary<string, int>();
        if (el.TryGetProperty(name, out var o) && o.ValueKind == JsonValueKind.Object) foreach (var p in o.EnumerateObject()) if (p.Value.ValueKind == JsonValueKind.Number) d[p.Name] = p.Value.GetInt32();
        return d;
    }

    /// <summary>Every <c>local_laws</c> array in content/politics, files in name order.</summary>
    public static LocalLawCatalog LoadFromDirectory(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "politics"); if (!Directory.Exists(dir)) return Empty;
        var opts = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        var laws = new List<LocalLawDef>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json").OrderBy(p => p, StringComparer.Ordinal))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file), opts); var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("local_laws", out var ls) || ls.ValueKind != JsonValueKind.Array) continue;
            foreach (var el in ls.EnumerateArray())
            {
                var options = new List<LocalLawOptionDef>();
                if (el.TryGetProperty("options", out var os) && os.ValueKind == JsonValueKind.Array)
                    foreach (var o in os.EnumerateArray())
                    {
                        string name = ConditionParser.Str(o, "name");
                        options.Add(new LocalLawOptionDef { Id = ConditionParser.Str(o, "id"), Name = name, Short = ConditionParser.OptStr(o, "short", name.ToLowerInvariant()), Default = ConditionParser.Bool(o, "default", false), Effects = Ints(o, "effects"), Opinions = Ints(o, "opinions") });
                    }
                laws.Add(new LocalLawDef
                {
                    Id = ConditionParser.Str(el, "id"), Name = ConditionParser.Str(el, "name"), Description = ConditionParser.OptStr(el, "description", ""), Model = ConditionParser.OptStr(el, "model", ""),
                    Cost = ConditionParser.Int(el, "cost", 1), Cooldown = ConditionParser.Int(el, "cooldown", 4), Options = options
                });
            }
        }
        return new LocalLawCatalog(laws);
    }
}
