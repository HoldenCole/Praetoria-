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
    /// <summary>Filled by election (docs/REPUBLIC.md), never by a Commission motion.</summary>
    public bool Elected { get; init; }
    /// <summary>The holder may strike one open motion from the docket a term.</summary>
    public bool Veto { get; init; }
    /// <summary>Turns a term lasts for an elected office.</summary>
    public int TermTurns { get; init; } = 10;
    /// <summary>The least age at which someone may hold it (docs/LADDER.md). 0: any.</summary>
    public int MinAge { get; init; }
    /// <summary>A rung of the ladder the holder must already have held, or empty.</summary>
    public string Requires { get; init; } = "";
}

/// <summary>A rung of the ladder of offices (content/senate/ladder.json): a magistracy below the consulship, held by a person for a
/// term and filled by election.</summary>
public sealed class RungDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public int Seats { get; init; } = 2;
    public int MinAge { get; init; } = 25;
    /// <summary>The least rank, in any service, a candidate must hold.</summary>
    public int MinRank { get; init; } = 1;
    /// <summary>The rung a candidate must already have held, or empty.</summary>
    public string Requires { get; init; } = "";
    /// <summary>Credits a year to the holder.</summary>
    public int Stipend { get; init; }
    /// <summary>The skill the work teaches.</summary>
    public string Learn { get; init; } = "";
    /// <summary>What having held it adds to the holder's house's standing in the Senate.</summary>
    public int Standing { get; init; }
    /// <summary>What it costs the holder to give games, if the office gives them. 0: it does not.</summary>
    public int Games { get; init; }
}

public sealed class OfficeCatalog
{
    private readonly List<OfficeDef> _defs; private readonly Dictionary<string, OfficeDef> _byId;
    public OfficeCatalog(IEnumerable<OfficeDef> defs) { _defs = defs.ToList(); _byId = _defs.ToDictionary(d => d.Id); }
    public static OfficeCatalog Empty { get; } = new(Array.Empty<OfficeDef>());
    public IReadOnlyList<OfficeDef> Defs => _defs;
    public OfficeDef? Def(string id) => _byId.TryGetValue(id, out var d) ? d : null;
    public bool IsEmpty => _defs.Count == 0;
    /// <summary>The ladder below the consulship, lowest rung first, and how often its elections come.</summary>
    public IReadOnlyList<RungDef> Rungs { get; private set; } = Array.Empty<RungDef>();
    public int LadderEvery { get; private set; } = 5;
    public RungDef? Rung(string id) => Rungs.FirstOrDefault(r => r.Id == id);

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
                        MotionDiscount = ConditionParser.Int(el, "motionDiscount", 0), CommandsFleets = ConditionParser.Bool(el, "commandsFleets", false),
                        Elected = ConditionParser.Bool(el, "elected", false), Veto = ConditionParser.Bool(el, "veto", false), TermTurns = ConditionParser.Int(el, "termTurns", 10),
                        MinAge = ConditionParser.Int(el, "minAge", 0), Requires = ConditionParser.OptStr(el, "requires", "")
                    });
        var catalog = new OfficeCatalog(defs);
        var ladder = Path.Combine(contentRoot, "senate", "ladder.json");
        if (File.Exists(ladder))
        {
            using var ld = JsonDocument.Parse(File.ReadAllText(ladder), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            catalog.LadderEvery = Math.Max(1, ConditionParser.Int(ld.RootElement, "every", 5));
            if (ld.RootElement.TryGetProperty("rungs", out var rs) && rs.ValueKind == JsonValueKind.Array)
                catalog.Rungs = rs.EnumerateArray().Where(el => el.ValueKind == JsonValueKind.Object).Select(el => new RungDef
                {
                    Id = ConditionParser.Str(el, "id"), Name = ConditionParser.Str(el, "name"), Description = ConditionParser.OptStr(el, "description", ""),
                    Seats = ConditionParser.Int(el, "seats", 2), MinAge = ConditionParser.Int(el, "minAge", 25), MinRank = ConditionParser.Int(el, "minRank", 1),
                    Requires = ConditionParser.OptStr(el, "requires", ""), Stipend = ConditionParser.Int(el, "stipend", 0), Learn = ConditionParser.OptStr(el, "learn", ""),
                    Standing = ConditionParser.Int(el, "standing", 0), Games = ConditionParser.Int(el, "games", 0)
                }).ToList();
        }
        return catalog;
    }
}
