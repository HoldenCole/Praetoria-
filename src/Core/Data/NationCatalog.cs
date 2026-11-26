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
    /// <summary>On the map (docs/BEYOND.md): the house that stands for the nation, and the worlds it holds. Empty for a nation still off it.</summary>
    public NationHouse? House { get; init; }
    public IReadOnlyList<NationWorld> Worlds { get; init; } = Array.Empty<NationWorld>();
    /// <summary>How many years a world of the nation's own, held by a house of the Reach, stays quiet without a fleet over it before it rises; 0 if it never rises of itself.</summary>
    public int HoldYears { get; init; } = 3;
    /// <summary>How many fleets it sends through its marches (one for most; two for a state that invades on two lines).</summary>
    public int Hosts { get; init; } = 1;
    /// <summary>It begins wars itself: when the Reach is divided, or to take back a world of its own that the Reach holds.</summary>
    public bool Strikes { get; init; }
    /// <summary>It fights a campaign, not a raid: its fleets stay in the field and take world after world, where a raider goes home with one.</summary>
    public bool Campaigns { get; init; }
    /// <summary>Whether its fleets come through the march in a war: "" by its temper and its ruler, "never", or "always".</summary>
    public string Sorties { get; init; } = "";
    /// <summary>Years added to a siege of a world it holds: small worlds dug in.</summary>
    public int Walls { get; init; }
    /// <summary>What each of its own worlds gives a house of the Reach that holds it, a year: good → units.</summary>
    public IReadOnlyDictionary<string, int> Yields { get; init; } = new Dictionary<string, int>();
    /// <summary>Credits in its capital for whoever first takes it, and the standing that comes with them.</summary>
    public int Treasure { get; init; }
    public int Titles { get; init; }
    /// <summary>A thing in its vaults that goes, with the treasure, to the head of the house that first takes its capital (docs/BEYOND.md).</summary>
    public string Regalia { get; init; } = "";
    /// <summary>What a ruler who unites the nation is called ("Great Khan"), and how rarely one follows another: one accession in so many.
    /// 0 for a nation that never has one (docs/BEYOND.md).</summary>
    public string GreatTitle { get; init; } = "";
    public int GreatOneIn { get; init; }
    /// <summary>How its fleets fight hull for hull, in hundredths of a fleet of the Reach's; 0 to take the figure its temper gives.</summary>
    public int Mettle { get; init; }
    /// <summary>What its two fleets are called; "" for the plain names.</summary>
    public string HostName { get; init; } = "";
    public string GuardName { get; init; } = "";
    /// <summary>A nation that buys votes in the Reach's Senate keeps a purse of this many years of its wealth (docs/BEYOND.md, "Foreign gold in
    /// the Senate"); 0 for one that does not. And the nations it favours: a war on one of them is a war it pays to stop.</summary>
    public int SenateGold { get; init; }
    public IReadOnlyList<string> Favours { get; init; } = Array.Empty<string>();
}

public sealed record NationHouse(string Id, string Name, string Accent, string Title, int Legitimacy);
public sealed record NationWorld(string Id, string System, string Name, string Specialization, int Population, bool Capital);

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
                        House = el.TryGetProperty("house", out var hs) && hs.ValueKind == JsonValueKind.Object ? new NationHouse(ConditionParser.OptStr(hs, "id", ""), ConditionParser.OptStr(hs, "name", ConditionParser.Str(el, "name")), ConditionParser.OptStr(hs, "accent", "#C9A227"), ConditionParser.OptStr(hs, "title", "duke"), ConditionParser.Int(hs, "legitimacy", 50)) : null,
                        Worlds = el.TryGetProperty("worlds", out var ws) && ws.ValueKind == JsonValueKind.Array ? ws.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).Select(x => new NationWorld(ConditionParser.Str(x, "id"), ConditionParser.Str(x, "system"), ConditionParser.OptStr(x, "name", ConditionParser.Str(x, "id")), ConditionParser.OptStr(x, "specialization", "frontier_colony"), ConditionParser.Int(x, "population", 15), ConditionParser.Bool(x, "capital", false))).ToList() : new List<NationWorld>(),
                        HoldYears = Math.Max(0, ConditionParser.Int(el, "holdYears", 3)), Hosts = Math.Clamp(ConditionParser.Int(el, "hosts", 1), 1, 3), Strikes = el.TryGetProperty("strikes", out var stk) && stk.ValueKind == JsonValueKind.True, Campaigns = el.TryGetProperty("campaigns", out var cmp) && cmp.ValueKind == JsonValueKind.True,
                        Sorties = ConditionParser.OptStr(el, "sorties", ""), Walls = Math.Max(0, ConditionParser.Int(el, "walls", 0)), Treasure = Math.Max(0, ConditionParser.Int(el, "treasure", 0)), Titles = Math.Max(0, ConditionParser.Int(el, "titles", 0)), Regalia = ConditionParser.OptStr(el, "regalia", ""), GreatTitle = ConditionParser.OptStr(el, "greatTitle", ""), GreatOneIn = Math.Max(0, ConditionParser.Int(el, "greatOneIn", 0)), Mettle = Math.Max(0, ConditionParser.Int(el, "mettle", 0)),
                        Yields = el.TryGetProperty("yields", out var yl) && yl.ValueKind == JsonValueKind.Object ? yl.EnumerateObject().Where(x => x.Value.ValueKind == JsonValueKind.Number).ToDictionary(x => x.Name, x => x.Value.GetInt32()) : new Dictionary<string, int>(), HostName = ConditionParser.OptStr(el, "hostName", ""), GuardName = ConditionParser.OptStr(el, "guardName", ""),
                        SenateGold = Math.Max(0, ConditionParser.Int(el, "senateGold", 0)), Favours = Strings(el, "favours"),
                        RulerTitle = ConditionParser.OptStr(el, "rulerTitle", "ruler"), CommanderTitle = ConditionParser.OptStr(el, "commanderTitle", "commander"), Names = Strings(el, "names"),
                        Exports = el.TryGetProperty("exports", out var ex) && ex.ValueKind == JsonValueKind.Object ? ex.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Number).ToDictionary(p => p.Name, p => p.Value.GetInt32()) : new Dictionary<string, int>()
                    });
        return new NationCatalog(defs);
    }
    private static List<string> Strings(JsonElement el, string key) =>
        el.TryGetProperty(key, out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList() : new List<string>();
}
