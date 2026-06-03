using System.Text.Json;
using Praetoria.Core.Events;

namespace Praetoria.Core.Data;

/// <summary>
/// Reads the JSON content tree into a <see cref="ContentDatabase"/> (BuildSpec §2). Event
/// *logic* files live under /content/events; *text* files under /content/text — loaded by
/// separate passes and joined only by id, enforcing the logic/text split (GDD §15). All
/// content is data; none of it is hard-coded in Core.
/// </summary>
public static class ContentLoader
{
    private static readonly JsonDocumentOptions DocOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static ContentDatabase LoadFromDirectory(string contentRoot)
    {
        var events = new List<EventDef>();
        var texts = new Dictionary<string, EventText>();

        var eventsDir = Path.Combine(contentRoot, "events");
        if (Directory.Exists(eventsDir))
            foreach (var file in EnumerateJson(eventsDir))
                events.AddRange(LoadEventFile(file));

        var textDir = Path.Combine(contentRoot, "text");
        if (Directory.Exists(textDir))
            foreach (var file in EnumerateJson(textDir))
                foreach (var t in LoadTextFile(file))
                    texts[t.Id] = t;

        var holdings = HoldingCatalogLoader.LoadFromDirectory(contentRoot);

        return new ContentDatabase(events, texts, holdings);
    }

    private static IEnumerable<string> EnumerateJson(string dir) =>
        Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories)
                 .OrderBy(static p => p, StringComparer.Ordinal);

    // ---- events ----

    public static IReadOnlyList<EventDef> LoadEventFile(string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), DocOptions);
            var arr = SectionArray(doc.RootElement, "events");
            var list = new List<EventDef>();
            foreach (var el in arr.EnumerateArray())
                list.Add(ParseEvent(el));
            return list;
        }
        catch (JsonException ex)
        {
            throw new ContentException($"Invalid JSON in '{path}': {ex.Message}", ex);
        }
    }

    private static EventDef ParseEvent(JsonElement el)
    {
        string id = ConditionParser.Str(el, "id");
        try
        {
            return new EventDef
            {
                Id = id,
                Tier = ParseTier(ConditionParser.OptStr(el, "tier", "situation")),
                Weight = el.TryGetProperty("weight", out var w) && w.ValueKind == JsonValueKind.Number ? w.GetDouble() : 1.0,
                Repeatable = ConditionParser.Bool(el, "repeatable", false),
                Roles = ParseRoles(el),
                Conditions = el.TryGetProperty("when", out var when)
                    ? ConditionParser.ParseList(when)
                    : Array.Empty<ICondition>(),
                Choices = ParseChoices(el)
            };
        }
        catch (ContentException ex)
        {
            throw new ContentException($"In event '{id}': {ex.Message}", ex);
        }
    }

    private static IReadOnlyList<RoleDef> ParseRoles(JsonElement el)
    {
        if (!el.TryGetProperty("roles", out var roles) || roles.ValueKind != JsonValueKind.Array)
            return Array.Empty<RoleDef>();

        var list = new List<RoleDef>();
        foreach (var r in roles.EnumerateArray())
        {
            list.Add(new RoleDef
            {
                Name = ConditionParser.Str(r, "name"),
                Constraints = r.TryGetProperty("when", out var w)
                    ? ConditionParser.ParseList(w)
                    : Array.Empty<ICondition>()
            });
        }
        return list;
    }

    private static IReadOnlyList<Choice> ParseChoices(JsonElement el)
    {
        if (!el.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
            return Array.Empty<Choice>();

        var list = new List<Choice>();
        foreach (var c in choices.EnumerateArray())
        {
            list.Add(new Choice
            {
                Id = ConditionParser.Str(c, "id"),
                Requirements = c.TryGetProperty("requires", out var req)
                    ? ConditionParser.ParseList(req)
                    : Array.Empty<ICondition>(),
                Effects = c.TryGetProperty("effects", out var eff)
                    ? EffectParser.ParseList(eff)
                    : Array.Empty<IEffect>(),
                Cost = ParseCost(c)
            });
        }
        return list;
    }

    private static IReadOnlyDictionary<string, int> ParseCost(JsonElement c)
    {
        var cost = new Dictionary<string, int>();
        if (c.TryGetProperty("cost", out var cEl) && cEl.ValueKind == JsonValueKind.Object)
            foreach (var p in cEl.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.Number)
                    cost[p.Name] = p.Value.GetInt32();
        return cost;
    }

    private static EventTier ParseTier(string s) => s.ToLowerInvariant() switch
    {
        "ambient" => EventTier.Ambient,
        "situation" => EventTier.Situation,
        "setpiece" => EventTier.Setpiece,
        _ => throw new ContentException($"Unknown event tier '{s}'.")
    };

    // ---- text ----

    public static IReadOnlyList<EventText> LoadTextFile(string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), DocOptions);
            var arr = SectionArray(doc.RootElement, "texts");
            var list = new List<EventText>();
            foreach (var el in arr.EnumerateArray())
            {
                var choiceTexts = new Dictionary<string, string>();
                if (el.TryGetProperty("choices", out var ch) && ch.ValueKind == JsonValueKind.Object)
                    foreach (var p in ch.EnumerateObject())
                        choiceTexts[p.Name] = p.Value.GetString() ?? "";

                list.Add(new EventText
                {
                    Id = ConditionParser.Str(el, "id"),
                    Title = ConditionParser.OptStrOrNull(el, "title"),
                    Body = ConditionParser.OptStr(el, "body", ""),
                    Choices = choiceTexts
                });
            }
            return list;
        }
        catch (JsonException ex)
        {
            throw new ContentException($"Invalid JSON in '{path}': {ex.Message}", ex);
        }
    }

    private static JsonElement SectionArray(JsonElement root, string section)
    {
        // Accept either { "events": [...] } or a bare top-level [...] array.
        if (root.ValueKind == JsonValueKind.Array) return root;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(section, out var arr) && arr.ValueKind == JsonValueKind.Array)
            return arr;
        throw new ContentException($"Expected a top-level array or a '{section}' array.");
    }
}
