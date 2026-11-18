using System.Text.Json;

namespace Praetoria.Core.Data;

/// <summary>A foreign nation beyond the Reach (content/nations/nations.json; docs/FOREIGN_NATIONS.md).</summary>
public sealed class NationDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Culture { get; init; } = "";
    /// <summary>aggressive (raids when its regard falls), mercantile (forgives, pays), defensive (fights only at home), decaying (crumbles).</summary>
    public string Temper { get; init; } = "defensive";
    public string Description { get; init; } = "";
    public string Holds { get; init; } = "";
    public int Strength { get; init; } = 10;
    public int Regen { get; init; } = 1;
    public int Wealth { get; init; } = 5;
    public int Distance { get; init; } = 2;
    public int Disposition { get; init; }
    public int Tribute { get; init; } = 4;
    public int Threshold { get; init; } = 6;
    /// <summary>What the nation ships into the Reach's markets a turn while at peace and not hostile (good id → units); twice as much while subdued.</summary>
    public IReadOnlyDictionary<string, int> Exports { get; init; } = new Dictionary<string, int>();
    /// <summary>The systems of the rim that face the nation: its march (docs/BEYOND.md). Raids come through them and an expedition leaves by them.</summary>
    public IReadOnlyList<string> Marches { get; init; } = Array.Empty<string>();
    /// <summary>Years an expedition sails beyond the march before it fights.</summary>
    public int Depth { get; init; } = 1;
    /// <summary>What its ruler and the commander of its fleets are called, and the names its people bear.</summary>
    public string RulerTitle { get; init; } = "ruler";
    public string CommanderTitle { get; init; } = "commander";
    public IReadOnlyList<string> Names { get; init; } = Array.Empty<string>();
}

public sealed class NationCatalog
{
    private readonly List<NationDef> _defs;
    public NationCatalog(IEnumerable<NationDef> defs) { _defs = defs.ToList(); }
    public static NationCatalog Empty { get; } = new(Array.Empty<NationDef>());
    public IReadOnlyList<NationDef> Defs => _defs;
    public NationDef? Def(string id) => _defs.FirstOrDefault(d => d.Id == id);
    public bool IsEmpty => _defs.Count == 0;

    public static NationCatalog LoadFromDirectory(string contentRoot)
    {
        var file = Path.Combine(contentRoot, "nations", "nations.json"); if (!File.Exists(file)) return Empty;
        using var doc = JsonDocument.Parse(File.ReadAllText(file), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var defs = new List<NationDef>();
        if (doc.RootElement.TryGetProperty("nations", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var el in arr.EnumerateArray())
                if (el.ValueKind == JsonValueKind.Object)
                    defs.Add(new NationDef
                    {
                        Id = ConditionParser.Str(el, "id"), Name = ConditionParser.Str(el, "name"), Culture = ConditionParser.OptStr(el, "culture", ""), Temper = ConditionParser.OptStr(el, "temper", "defensive"),
                        Description = ConditionParser.OptStr(el, "description", ""), Holds = ConditionParser.OptStr(el, "holds", ""),
                        Strength = ConditionParser.Int(el, "strength", 10), Regen = ConditionParser.Int(el, "regen", 1), Wealth = ConditionParser.Int(el, "wealth", 5), Distance = ConditionParser.Int(el, "distance", 2),
                        Disposition = ConditionParser.Int(el, "disposition", 0), Tribute = ConditionParser.Int(el, "tribute", 4), Threshold = ConditionParser.Int(el, "threshold", 6),
                        Marches = Strings(el, "marches"), Depth = ConditionParser.Int(el, "depth", Math.Max(1, ConditionParser.Int(el, "distance", 2) - 1)),
                        RulerTitle = ConditionParser.OptStr(el, "rulerTitle", "ruler"), CommanderTitle = ConditionParser.OptStr(el, "commanderTitle", "commander"), Names = Strings(el, "names"),
                        Exports = el.TryGetProperty("exports", out var ex) && ex.ValueKind == JsonValueKind.Object ? ex.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Number).ToDictionary(p => p.Name, p => p.Value.GetInt32()) : new Dictionary<string, int>()
                    });
        return new NationCatalog(defs);
    }
    private static List<string> Strings(JsonElement el, string key) =>
        el.TryGetProperty(key, out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList() : new List<string>();
}
