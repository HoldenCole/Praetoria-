using System.Text.Json;
using Praetoria.Core.Events;
using Praetoria.Core.State;

namespace Praetoria.Core.Data;

/// <summary>
/// Parses the JSON predicate mini-language into <see cref="ICondition"/> objects (BuildSpec §2
/// /Data loaders). Dispatch is on a "type" field. Keeping this the single parse point means new
/// predicates are one case here + one class — the data format stays the modding surface (GDD §15).
/// </summary>
public static class ConditionParser
{
    public static IReadOnlyList<ICondition> ParseList(JsonElement array)
    {
        if (array.ValueKind == JsonValueKind.Undefined || array.ValueKind == JsonValueKind.Null)
            return Array.Empty<ICondition>();
        if (array.ValueKind != JsonValueKind.Array)
            throw new ContentException("Expected a JSON array of conditions.");

        var list = new List<ICondition>();
        foreach (var el in array.EnumerateArray())
            list.Add(Parse(el));
        return list;
    }

    public static ICondition Parse(JsonElement el)
    {
        string type = Req(el, "type").GetString()
            ?? throw new ContentException("Condition 'type' must be a string.");

        switch (type)
        {
            case "all": return new AllCondition(ParseList(Req(el, "of")));
            case "any": return new AnyCondition(ParseList(Req(el, "of")));
            case "not": return new NotCondition(Parse(Req(el, "of")));
            case "const": return new ConstCondition(Bool(el, "value", true));

            case "worldFlag":
                return new WorldFlagCondition(Str(el, "flag"), Bool(el, "value", true));
            case "charFlag":
                return new CharFlagCondition(Str(el, "role"), Str(el, "flag"), Bool(el, "value", true));
            case "relationship":
                return new RelationshipCondition(Str(el, "from"), Str(el, "to"), Op(el), Int(el, "value", 0));
            case "bond":
                return new BondCondition(Str(el, "from"), Str(el, "to"), Bond(el, "bond"), Bool(el, "present", true));
            case "skill":
                return new SkillCondition(Str(el, "role"), Str(el, "skill"), Op(el), Int(el, "value", 0));
            case "trait":
                return new TraitCondition(Str(el, "role"), Str(el, "trait"), OptStr(el, "kind", "any"), Bool(el, "present", true));
            case "rank":
                return new RankCondition(Str(el, "role"), Op(el), Int(el, "value", 0), OptStrOrNull(el, "vsRole"));
            case "turn":
                return new TurnCondition(Op(el), Int(el, "value", 0));
            case "counter":
                return new CounterCondition(Str(el, "key"), Op(el), Int(el, "value", 0));
            case "resource":
                return new ResourceCondition(
                    OptStr(el, "role", "self"), Str(el, "resource"), Op(el), Int(el, "value", 0));
            case "sphere":
                return new SphereCondition(
                    OptStr(el, "role", "self"), Str(el, "sphere"), Op(el), Int(el, "value", 0));
            case "title":
                return new TitleCondition(OptStr(el, "role", "self"), Str(el, "title"), Bool(el, "present", true));
            case "claim":
                return new ClaimCondition(OptStr(el, "role", "self"), Str(el, "title"), Bool(el, "present", true));
            case "eventFired":
                return new EventFiredCondition(Str(el, "event"), Bool(el, "value", true));

            default:
                throw new ContentException($"Unknown condition type '{type}'.");
        }
    }

    // ---- small typed JSON accessors (shared style with EffectParser) ----

    internal static JsonElement Req(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v)
            ? v
            : throw new ContentException($"Missing required property '{name}'.");

    internal static string Str(JsonElement el, string name) =>
        Req(el, name).GetString() ?? throw new ContentException($"Property '{name}' must be a string.");

    internal static string OptStr(JsonElement el, string name, string fallback) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : fallback;

    internal static string? OptStrOrNull(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    internal static int Int(JsonElement el, string name, int fallback) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : fallback;

    internal static bool Bool(JsonElement el, string name, bool fallback)
    {
        if (!el.TryGetProperty(name, out var v)) return fallback;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => fallback
        };
    }

    private static CompareOp Op(JsonElement el) =>
        Comparison.Parse(el.TryGetProperty("op", out var v) ? v.GetString() : "eq");

    private static BondType Bond(JsonElement el, string name) => (Str(el, name).ToLowerInvariant()) switch
    {
        "blood" => BondType.Blood,
        "sworn" => BondType.Sworn,
        "marriage" => BondType.Marriage,
        "none" => BondType.None,
        var s => throw new ContentException($"Unknown bond type '{s}'.")
    };
}
