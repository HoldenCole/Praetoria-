using System.Text.Json;

namespace Praetoria.Core.Data;

/// <summary>How the map grows past its written size (content/galaxy/growth.json, docs/GALAXY_SIZE.md): how many systems each size
/// has in hundredths of the small map's, what new systems and the houses raised to hold them are called, and a line for each kind
/// of world.</summary>
public sealed class GrowthCatalog
{
    public const string Small = "small", Medium = "medium", Large = "large", ExtraLarge = "extra_large";
    /// <summary>The sizes in order, smallest first.</summary>
    public static readonly string[] Sizes = { Small, Medium, Large, ExtraLarge };
    public static string SizeName(string size) => size switch { Medium => "Medium", Large => "Large", ExtraLarge => "Extra large", _ => "Small" };

    public Dictionary<string, int> Percent { get; init; } = new() { [Small] = 100, [Medium] = 160, [Large] = 230, [ExtraLarge] = 320 };
    /// <summary>Of a ring's new systems, the share left to nobody, in hundredths.</summary>
    public Dictionary<string, int> Unheld { get; init; } = new() { ["core"] = 0, ["middle"] = 12, ["rim"] = 30 };
    /// <summary>For each doubling of the map, the share of its own worlds a house gains again, in hundredths.</summary>
    public int Kept { get; init; } = 55;
    public IReadOnlyList<string> Names { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, IReadOnlyList<string>> RegionNames { get; init; } = new Dictionary<string, IReadOnlyList<string>>();
    public IReadOnlyList<string> Surnames { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Descriptions { get; init; } = new Dictionary<string, IReadOnlyList<string>>();
    public IReadOnlyDictionary<string, IReadOnlyList<string>> WorldNames { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    public int PercentOf(string size) => Percent.TryGetValue(size, out var p) ? Math.Max(100, p) : 100;
    public static GrowthCatalog Default { get; } = new();

    public static GrowthCatalog LoadFromDirectory(string contentRoot)
    {
        var path = Path.Combine(contentRoot, "galaxy", "growth.json"); if (!File.Exists(path)) return Default;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var root = doc.RootElement;
            static List<string> Strings(JsonElement e) => e.ValueKind == JsonValueKind.Array ? e.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToList() : new List<string>();
            Dictionary<string, int> Ints(string key, Dictionary<string, int> fallback) => root.TryGetProperty(key, out var o) && o.ValueKind == JsonValueKind.Object
                ? o.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Number).ToDictionary(p => p.Name, p => p.Value.GetInt32()) : fallback;
            IReadOnlyDictionary<string, IReadOnlyList<string>> Lists(string key) => root.TryGetProperty(key, out var o) && o.ValueKind == JsonValueKind.Object
                ? o.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Array).ToDictionary(p => p.Name, p => (IReadOnlyList<string>)Strings(p.Value)) : new Dictionary<string, IReadOnlyList<string>>();
            return new GrowthCatalog
            {
                Percent = Ints("sizes", Default.Percent), Unheld = Ints("unheld", Default.Unheld), Kept = ConditionParser.Int(root, "kept", Default.Kept),
                Names = root.TryGetProperty("names", out var n) ? Strings(n) : new List<string>(), RegionNames = Lists("regionNames"),
                Surnames = root.TryGetProperty("surnames", out var sn) ? Strings(sn) : new List<string>(), Descriptions = Lists("descriptions"), WorldNames = Lists("worldNames")
            };
        }
        catch (JsonException ex) { throw new ContentException($"Invalid JSON in '{path}': {ex.Message}", ex); }
    }

    public IEnumerable<string> Problems()
    {
        foreach (var s in Sizes) if (!Percent.ContainsKey(s)) yield return $"growth: no size '{s}'";
        if (Names.Count != Names.Distinct().Count()) yield return "growth: a system name is listed twice";
        if (Surnames.Count != Surnames.Distinct().Count()) yield return "growth: a surname is listed twice";
    }
}
