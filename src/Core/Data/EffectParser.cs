using System.Text.Json;
using Praetoria.Core.Events;
using Praetoria.Core.State;

namespace Praetoria.Core.Data;

/// <summary>
/// Parses the JSON consequence mini-language into <see cref="IEffect"/> objects. Mirror of
/// <see cref="ConditionParser"/>. AI/authoring writes effects here; the engine can only do what
/// this vocabulary exposes, which is what keeps AI-written content from inventing balance
/// (GDD §15 "AI never invents consequences").
/// </summary>
public static class EffectParser
{
    public static IReadOnlyList<IEffect> ParseList(JsonElement array)
    {
        if (array.ValueKind == JsonValueKind.Undefined || array.ValueKind == JsonValueKind.Null)
            return Array.Empty<IEffect>();
        if (array.ValueKind != JsonValueKind.Array)
            throw new ContentException("Expected a JSON array of effects.");

        var list = new List<IEffect>();
        foreach (var el in array.EnumerateArray())
            list.Add(Parse(el));
        return list;
    }

    public static IEffect Parse(JsonElement el)
    {
        string type = ConditionParser.Req(el, "type").GetString()
            ?? throw new ContentException("Effect 'type' must be a string.");

        switch (type)
        {
            case "setWorldFlag":
                return new SetWorldFlagEffect(S(el, "flag"), B(el, "value", true));
            case "setCharFlag":
                return new SetCharFlagEffect(S(el, "role"), S(el, "flag"), B(el, "value", true));
            case "adjustRelationship":
                return new AdjustRelationshipEffect(S(el, "from"), S(el, "to"), I(el, "delta", 0));
            case "addBond":
                return new AddBondEffect(S(el, "from"), S(el, "to"), BondOf(el), I(el, "strength", 50));
            case "adjustSkill":
                return new AdjustSkillEffect(S(el, "role"), S(el, "skill"), I(el, "delta", 0));
            case "adjustStress":
                return new AdjustStressEffect(S(el, "role"), I(el, "delta", 0));
            case "adjustCounter":
                return new AdjustCounterEffect(S(el, "key"), I(el, "delta", 0));
            case "addTrait":
                return new AddTraitEffect(S(el, "role"), S(el, "trait"), ConditionParser.OptStr(el, "kind", "aptitude"));
            case "advanceCareer":
                return new AdvanceCareerEffect(S(el, "role"));
            case "kill":
                return new KillEffect(S(el, "role"));
            case "adjustResource":
                return new AdjustResourceEffect(
                    ConditionParser.OptStr(el, "role", "self"), S(el, "resource"), I(el, "delta", 0));
            case "grantClaim":
                return new GrantClaimEffect(ConditionParser.OptStr(el, "role", "self"), S(el, "title"));
            case "adjustLegitimacy":
                return new AdjustLegitimacyEffect(ConditionParser.OptStr(el, "role", "self"), I(el, "delta", 0));
            case "setTitle":
                return new SetTitleEffect(ConditionParser.OptStr(el, "role", "self"), S(el, "title"));
            case "log":
                return new LogEffect(S(el, "text"));

            default:
                throw new ContentException($"Unknown effect type '{type}'.");
        }
    }

    private static string S(JsonElement el, string n) => ConditionParser.Str(el, n);
    private static int I(JsonElement el, string n, int f) => ConditionParser.Int(el, n, f);
    private static bool B(JsonElement el, string n, bool f) => ConditionParser.Bool(el, n, f);

    private static BondType BondOf(JsonElement el) => (S(el, "bond").ToLowerInvariant()) switch
    {
        "blood" => BondType.Blood,
        "sworn" => BondType.Sworn,
        "marriage" => BondType.Marriage,
        _ => throw new ContentException("addBond 'bond' must be 'blood', 'sworn', or 'marriage'.")
    };
}
