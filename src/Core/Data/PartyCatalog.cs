using System.Text.Json;

namespace Praetoria.Core.Data;

/// <summary>A party of the Republic (content/senate/parties.json; docs/REPUBLIC.md): the estates it courts, its positions on the laws, and the
/// kinds of motion it stands for (+1) or against (−1).</summary>
public sealed class PartyDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Accent { get; init; } = "#C9D1DC";
    public string Description { get; init; } = "";
    public IReadOnlyList<string> Estates { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, string> Laws { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, int> Motions { get; init; } = new Dictionary<string, int>();
}

public sealed class PartyCatalog
{
    private readonly List<PartyDef> _defs;
    public PartyCatalog(IEnumerable<PartyDef> defs) { _defs = defs.ToList(); }
    public static PartyCatalog Empty { get; } = new(Array.Empty<PartyDef>());
    public IReadOnlyList<PartyDef> Defs => _defs;
    public PartyDef? Def(string id) => _defs.FirstOrDefault(d => d.Id == id);
    public bool IsEmpty => _defs.Count == 0;

    public static PartyCatalog LoadFromDirectory(string contentRoot)
    {
        var file = Path.Combine(contentRoot, "senate", "parties.json"); if (!File.Exists(file)) return Empty;
        using var doc = JsonDocument.Parse(File.ReadAllText(file), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var defs = new List<PartyDef>();
        if (doc.RootElement.TryGetProperty("parties", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var el in arr.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) continue;
                var estates = new List<string>(); if (el.TryGetProperty("estates", out var es) && es.ValueKind == JsonValueKind.Array) foreach (var e in es.EnumerateArray()) estates.Add(e.GetString() ?? "");
                var laws = new Dictionary<string, string>(); if (el.TryGetProperty("laws", out var ls) && ls.ValueKind == JsonValueKind.Object) foreach (var p in ls.EnumerateObject()) laws[p.Name] = p.Value.GetString() ?? "";
                var motions = new Dictionary<string, int>(); if (el.TryGetProperty("motions", out var ms) && ms.ValueKind == JsonValueKind.Object) foreach (var p in ms.EnumerateObject()) motions[p.Name] = p.Value.GetInt32();
                defs.Add(new PartyDef { Id = ConditionParser.Str(el, "id"), Name = ConditionParser.Str(el, "name"), Accent = ConditionParser.OptStr(el, "accent", "#C9D1DC"), Description = ConditionParser.OptStr(el, "description", ""), Estates = estates, Laws = laws, Motions = motions });
            }
        return new PartyCatalog(defs);
    }
}
