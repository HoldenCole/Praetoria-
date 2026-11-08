using System.Text.Json;

namespace Praetoria.Core.Data;

/// <summary>An office of the Reach (content/senate/offices.json): granted by a Commission motion to a house whose member has the rank on the
/// office's career track; it carries sphere influence, Senate weight, a stipend, and — for the Admiralty — command of the office fleets.</summary>
public sealed class OfficeDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    /// <summary>The sphere the office belongs to (navy, treasury, senate).</summary>
    public string Sphere { get; init; } = "";
    /// <summary>The career track and minimum rank a member of the house must hold to be commissioned.</summary>
    public string Track { get; init; } = "";
    public int MinRank { get; init; } = 3;
    /// <summary>Influence the office adds to the holder's house in its sphere.</summary>
    public int SphereBonus { get; init; } = 2;
    /// <summary>Senate vote weight the office adds.</summary>
    public int Weight { get; init; }
    /// <summary>Credits a turn from the chair's coffers.</summary>
    public int Stipend { get; init; }
    /// <summary>Influence a motion costs less to the holder.</summary>
    public int MotionDiscount { get; init; }
    /// <summary>The holder's house commands the office fleets (GDD §8).</summary>
    public bool CommandsFleets { get; init; }
}

public sealed class OfficeCatalog
{
    private readonly List<OfficeDef> _defs; private readonly Dictionary<string, OfficeDef> _byId;
    public OfficeCatalog(IEnumerable<OfficeDef> defs) { _defs = defs.ToList(); _byId = _defs.ToDictionary(d => d.Id); }
    public static OfficeCatalog Empty { get; } = new(Array.Empty<OfficeDef>());
    public IReadOnlyList<OfficeDef> Defs => _defs;
    public OfficeDef? Def(string id) => _byId.TryGetValue(id, out var d) ? d : null;
    public bool IsEmpty => _defs.Count == 0;

    public static OfficeCatalog LoadFromDirectory(string contentRoot)
    {
        var file = Path.Combine(contentRoot, "senate", "offices.json"); if (!File.Exists(file)) return Empty;
        using var doc = JsonDocument.Parse(File.ReadAllText(file), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var defs = new List<OfficeDef>();
        if (doc.RootElement.TryGetProperty("offices", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var el in arr.EnumerateArray())
                if (el.ValueKind == JsonValueKind.Object)
                    defs.Add(new OfficeDef
                    {
                        Id = ConditionParser.Str(el, "id"), Name = ConditionParser.Str(el, "name"), Description = ConditionParser.OptStr(el, "description", ""),
                        Sphere = ConditionParser.OptStr(el, "sphere", ""), Track = ConditionParser.OptStr(el, "track", ""), MinRank = ConditionParser.Int(el, "minRank", 3),
                        SphereBonus = ConditionParser.Int(el, "sphereBonus", 2), Weight = ConditionParser.Int(el, "weight", 0), Stipend = ConditionParser.Int(el, "stipend", 0),
                        MotionDiscount = ConditionParser.Int(el, "motionDiscount", 0), CommandsFleets = ConditionParser.Bool(el, "commandsFleets", false)
                    });
        return new OfficeCatalog(defs);
    }
}
