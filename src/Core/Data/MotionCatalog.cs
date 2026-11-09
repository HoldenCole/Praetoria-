using System.Text.Json;
using Praetoria.Core.State;

namespace Praetoria.Core.Data;

/// <summary>A kind of motion the Senate hears (content/senate/motions.json). Effects are data; the vote leans are keyed by <see cref="Interest"/>.</summary>
public sealed class MotionDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    /// <summary>Prose with {proposer} and {target} tokens.</summary>
    public string Description { get; init; } = "";
    /// <summary>"house", "region" or "none".</summary>
    public string Target { get; init; } = "none";
    /// <summary>Which lean rule the rival houses apply: censure, recognition, levy, relief, peace, embargo.</summary>
    public string Interest { get; init; } = "";
    public int ProposeInfluence { get; init; } = 2;
    public bool TargetSelfAllowed { get; init; }
    public bool TargetMustBeAtWar { get; init; }
    public int LegitimacyTarget { get; init; }
    public int LegitimacyProposer { get; init; }
    public int LevyCredits { get; init; }
    public int LawlessnessRegion { get; init; }
    public int AllegianceRegion { get; init; }
    public int ProsperityRegion { get; init; }
    public int ReliefCost { get; init; }
    public bool Peace { get; init; }
    public int EmbargoTurns { get; init; }
    /// <summary>The motion names an office (content/senate/offices.json) as well as a house.</summary>
    public bool Office { get; init; }
    /// <summary>The motion is an Act: it sets a Senate-scoped law (content/politics/laws.json, scope "senate") for the whole Reach.</summary>
    public bool Law { get; init; }
}

public sealed class MotionCatalog
{
    private readonly List<MotionDef> _defs; private readonly Dictionary<string, MotionDef> _byId;
    public MotionCatalog(IEnumerable<MotionDef> defs) { _defs = defs.ToList(); _byId = _defs.ToDictionary(d => d.Id); }
    public static MotionCatalog Empty { get; } = new(Array.Empty<MotionDef>());
    public IReadOnlyList<MotionDef> Defs => _defs;
    public MotionDef? Def(string id) => _byId.TryGetValue(id, out var d) ? d : null;
    public bool IsEmpty => _defs.Count == 0;

    public static MotionCatalog LoadFromDirectory(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "senate"); if (!Directory.Exists(dir)) return Empty;
        var defs = new List<MotionDef>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json").OrderBy(p => p, StringComparer.Ordinal))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (!doc.RootElement.TryGetProperty("motions", out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
            foreach (var el in arr.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) continue;
                defs.Add(new MotionDef
                {
                    Id = ConditionParser.Str(el, "id"), Name = ConditionParser.Str(el, "name"), Description = ConditionParser.OptStr(el, "description", ""),
                    Target = ConditionParser.OptStr(el, "target", "none"), Interest = ConditionParser.OptStr(el, "interest", ConditionParser.Str(el, "id")),
                    ProposeInfluence = ConditionParser.Int(el, "proposeInfluence", 2), TargetSelfAllowed = ConditionParser.Bool(el, "targetSelfAllowed", false),
                    TargetMustBeAtWar = ConditionParser.Bool(el, "targetMustBeAtWar", false),
                    LegitimacyTarget = ConditionParser.Int(el, "legitimacyTarget", 0), LegitimacyProposer = ConditionParser.Int(el, "legitimacyProposer", 0),
                    LevyCredits = ConditionParser.Int(el, "levyCredits", 0), LawlessnessRegion = ConditionParser.Int(el, "lawlessnessRegion", 0),
                    AllegianceRegion = ConditionParser.Int(el, "allegianceRegion", 0), ProsperityRegion = ConditionParser.Int(el, "prosperityRegion", 0),
                    ReliefCost = ConditionParser.Int(el, "reliefCost", 0), Peace = ConditionParser.Bool(el, "peace", false), EmbargoTurns = ConditionParser.Int(el, "embargoTurns", 0), Office = ConditionParser.Bool(el, "office", false), Law = ConditionParser.Bool(el, "law", false)
                });
            }
        }
        return new MotionCatalog(defs);
    }
}
