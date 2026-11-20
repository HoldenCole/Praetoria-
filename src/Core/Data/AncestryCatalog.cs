using System.Text.Json;

namespace Praetoria.Core.Data;

/// <summary>An ancestry: what the people of a house look like (content/ancestries/ancestries.json).</summary>
public sealed record AncestryDef(string Id, string Name, string Note, int Weight, bool Beyond);

/// <summary>The ancestries of the Reach and beyond it, which houses and nations are named to which, and the share of the unnamed
/// houses that falls to each (docs/ART_DIRECTION.md, "Portraits"). A house has one; its people are painted from it.</summary>
public sealed class AncestryCatalog
{
    public IReadOnlyList<AncestryDef> Defs { get; }
    public IReadOnlyDictionary<string, string> Houses { get; }
    public IReadOnlyDictionary<string, string> Nations { get; }
    public AncestryCatalog(IEnumerable<AncestryDef> defs, IReadOnlyDictionary<string, string>? houses = null, IReadOnlyDictionary<string, string>? nations = null)
    { Defs = defs.ToList(); Houses = houses ?? new Dictionary<string, string>(); Nations = nations ?? new Dictionary<string, string>(); }

    /// <summary>What a build with no file has: one ancestry, so nothing breaks and nothing is asserted.</summary>
    public static AncestryCatalog Default { get; } = new(new[] { new AncestryDef("mediterranean", "Mediterranean", "", 1, false) });

    public AncestryDef? Def(string id) => Defs.FirstOrDefault(d => d.Id == id);
    public string Name(string id) => Def(id)?.Name ?? id;
    /// <summary>The ancestries a house of the Reach may have.</summary>
    public IReadOnlyList<AncestryDef> OfTheReach => Defs.Where(d => !d.Beyond).ToList();

    public static AncestryCatalog LoadFromDirectory(string contentRoot)
    {
        var path = Path.Combine(contentRoot, "ancestries", "ancestries.json"); if (!File.Exists(path)) return Default;
        using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var root = doc.RootElement; var defs = new List<AncestryDef>();
        if (root.TryGetProperty("ancestries", out var arr))
            foreach (var a in arr.EnumerateArray())
                defs.Add(new AncestryDef(a.GetProperty("id").GetString() ?? "", a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "", a.TryGetProperty("note", out var t) ? t.GetString() ?? "" : "",
                    a.TryGetProperty("weight", out var wt) ? wt.GetInt32() : 0, a.TryGetProperty("beyond", out var b) && b.ValueKind == JsonValueKind.True));
        Dictionary<string, string> Map(string key) => root.TryGetProperty(key, out var o) && o.ValueKind == JsonValueKind.Object
            ? o.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.String).ToDictionary(p => p.Name, p => p.Value.GetString() ?? "") : new Dictionary<string, string>();
        return defs.Count == 0 ? Default : new AncestryCatalog(defs, Map("houses"), Map("nations"));
    }
}
