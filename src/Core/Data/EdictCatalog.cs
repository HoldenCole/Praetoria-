using System.Text.Json;

namespace Praetoria.Core.Data;

/// <summary>An edict (content/politics/edicts.json; docs/EDICTS.md): what a holder of an executive position decrees on their own
/// authority. It bites at once (<see cref="Now"/>), holds for <see cref="Years"/> years with lasting <see cref="Effects"/> (the
/// modifier keys laws use), costs the issuer something, and lapses.</summary>
public sealed class EdictDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    /// <summary>The real decree it is modelled on, in a line.</summary>
    public string Model { get; init; } = "";
    /// <summary>head · state · praetor · aedile · powers (Systems.EdictSystem).</summary>
    public string Issuer { get; init; } = "head";
    public int Years { get; init; } = 2;
    /// <summary>Years before it may be proclaimed again once it has ended.</summary>
    public int Again { get; init; } = 5;
    /// <summary>It stands when the issuer leaves the position, and cannot be withdrawn.</summary>
    public bool Outlives { get; init; }
    /// <summary>The Senate may strike it down by an annulment.</summary>
    public bool Annullable { get; init; }
    /// <summary>Only one edict of a group is in force at a time ("" for none).</summary>
    public string Group { get; init; } = "";
    /// <summary>"house" (a house is named), "good" (the dearest good is named), or "".</summary>
    public string Target { get; init; } = "";
    /// <summary>The plain reason another house or magistrate proclaims it.</summary>
    public string When { get; init; } = "";
    public int Influence { get; init; }
    /// <summary>In the books' hundreds.</summary>
    public int Credits { get; init; }
    public IReadOnlyDictionary<string, int> Now { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> Effects { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> Opinions { get; init; } = new Dictionary<string, int>();
    /// <summary>It falls on the whole Reach, not on the issuer's own worlds.</summary>
    public bool Reach => Issuer != "head";
}

public sealed class EdictCatalog
{
    private readonly Dictionary<string, EdictDef> _byId;
    public EdictCatalog(IEnumerable<EdictDef> defs) { Defs = defs.ToList(); _byId = Defs.ToDictionary(d => d.Id); }
    public static EdictCatalog Empty { get; } = new(Array.Empty<EdictDef>());
    public IReadOnlyList<EdictDef> Defs { get; }
    public EdictDef? Def(string id) => _byId.TryGetValue(id, out var d) ? d : null;
    public bool IsEmpty => Defs.Count == 0;

    private static Dictionary<string, int> Ints(JsonElement el, string name)
    {
        var d = new Dictionary<string, int>();
        if (el.TryGetProperty(name, out var o) && o.ValueKind == JsonValueKind.Object) foreach (var p in o.EnumerateObject()) if (p.Value.ValueKind == JsonValueKind.Number) d[p.Name] = p.Value.GetInt32();
        return d;
    }

    /// <summary>Every "edicts" array in content/politics/*.json.</summary>
    public static EdictCatalog LoadFromDirectory(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "politics"); if (!Directory.Exists(dir)) return Empty;
        var opts = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        var defs = new List<EdictDef>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json").OrderBy(p => p, StringComparer.Ordinal))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file), opts);
            if (!doc.RootElement.TryGetProperty("edicts", out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
            foreach (var el in arr.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) continue;
                var cost = Ints(el, "cost");
                defs.Add(new EdictDef
                {
                    Id = ConditionParser.Str(el, "id"), Name = ConditionParser.Str(el, "name"), Description = ConditionParser.OptStr(el, "description", ""), Model = ConditionParser.OptStr(el, "model", ""),
                    Issuer = ConditionParser.OptStr(el, "issuer", "head"), Years = Math.Max(1, ConditionParser.Int(el, "years", 2)), Again = Math.Max(0, ConditionParser.Int(el, "again", 5)),
                    Outlives = ConditionParser.Bool(el, "outlives", false), Annullable = ConditionParser.Bool(el, "annullable", false), Group = ConditionParser.OptStr(el, "group", ""),
                    Target = ConditionParser.OptStr(el, "target", ""), When = ConditionParser.OptStr(el, "when", ""),
                    Influence = cost.GetValueOrDefault("influence"), Credits = cost.GetValueOrDefault("credits"),
                    Now = Ints(el, "now"), Effects = Ints(el, "effects"), Opinions = Ints(el, "opinions")
                });
            }
        }
        return new EdictCatalog(defs);
    }
}
