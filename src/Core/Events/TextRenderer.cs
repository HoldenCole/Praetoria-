using System.Text;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;

namespace Praetoria.Core.Events;

/// <summary>
/// Substitutes {role.field} tokens in authored prose against the live binding (BuildSpec §4
/// "role-variable substitution"). This is what turns one authored line into many lived ones:
/// {rival.name} becomes whoever the Binder chose. Supported fields: name, first (the given name), house, rank (the integer),
/// rankName (the ladder rung, e.g. "Lieutenant", when a career catalog is loaded), title (the house's rung by the era's name),
/// the pronouns he, him, his, hers and himself (she, her, her, hers and herself for a woman; He, Him, His, Hers and Himself open a
/// sentence), and rel, how the person stands to the player ("your son and heir"; Rel opens a sentence). {era:A|B} picks A in the
/// Republic and B under the Imperium. Unknown tokens are left intact (and reported by the content validator) rather than crashing a run.
/// </summary>
public static class TextRenderer
{
    /// <summary>The fields a token may name (docs/CONTENT_GUIDE.md, "Tokens"). The content validator reads this list.</summary>
    public static readonly IReadOnlySet<string> Fields = new HashSet<string>(StringComparer.Ordinal)
    {
        "name", "first", "house", "rank", "rankName", "title", "rel", "Rel",
        "he", "him", "his", "hers", "himself", "He", "Him", "His", "Hers", "Himself"
    };

    public static string Substitute(string template, EvalContext ctx)
    {
        if (string.IsNullOrEmpty(template) || template.IndexOf('{') < 0) return template;

        var sb = new StringBuilder(template.Length + 16);
        int i = 0;
        while (i < template.Length)
        {
            char ch = template[i];
            if (ch == '{')
            {
                int end = template.IndexOf('}', i + 1);
                if (end < 0) { sb.Append(template, i, template.Length - i); break; }
                string token = template.Substring(i + 1, end - i - 1);
                sb.Append(Resolve(token, ctx, original: template.Substring(i, end - i + 1)));
                i = end + 1;
            }
            else
            {
                sb.Append(ch);
                i++;
            }
        }
        return sb.ToString();
    }

    private static string Resolve(string token, EvalContext ctx, string original)
    {
        // {era:what the Republic says|what the Imperium says} — one authored line, two registers (docs/TITLES.md)
        if (token.StartsWith("era:", StringComparison.Ordinal))
        {
            var alt = token.Substring(4).Split('|', 2);
            return alt.Length == 2 ? (ctx.World.IsImperium ? alt[1] : alt[0]) : original;
        }
        var parts = token.Split('.', 2);
        if (parts.Length != 2) return original;

        string role = parts[0];
        string field = parts[1];

        var c = ctx.Actor(role);
        if (c == null) return original;

        switch (field)
        {
            case "name": return c.Name;
            case "first": return First(c.Name);
            case "house":
                /* a spouse who has come to live in the player's house: "their family" is still the house they were born to */
                if (role == "spouse" && c.HouseId == ctx.World.Protagonist?.HouseId && c.BornHouseId.Length > 0 && c.BornHouseId != c.HouseId && ctx.World.Houses.TryGetValue(c.BornHouseId, out var born)) return born.Name;
                return ctx.World.Houses.TryGetValue(c.HouseId, out var h) ? h.Name : c.HouseId;
            case "rank": return c.CareerRank.ToString();
            case "rankName":
                return ctx.Content?.Careers.RankName(c.CareerTrack, c.CareerRank, ctx.World.Era) ?? c.CareerRank.ToString();
            case "title":      // the house's rung by the era's name: "Prefect" in the Republic, "Baron" under the Imperium
                return ctx.World.Houses.TryGetValue(c.HouseId, out var th) ? ctx.Content?.Titles.NameOf(th.Title, ctx.World.Era) ?? th.Title : original;
            case "rel": return Kinship.Said(ctx.World, c);
            case "Rel": return Capital(Kinship.Said(ctx.World, c));
            case "he" or "him" or "his" or "hers" or "himself":
                return Pronoun(c, field, ctx);
            case "He" or "Him" or "His" or "Hers" or "Himself":
                return Capital(Pronoun(c, field.ToLowerInvariant(), ctx));
            default: return original;
        }
    }

    /// <summary>The given name: the first word of the name ("Corvin" of "Corvin Castellan").</summary>
    public static string First(string name)
    {
        int space = name.IndexOf(' ');
        return space > 0 ? name[..space] : name;
    }

    /// <summary>The word for a person in the place of <paramref name="form"/> (he, him, his, hers, himself) by their sex. A person whose
    /// sex is not known is said by name where a pronoun would guess ("Corvin", "Corvin's"), so the verb after it still agrees.</summary>
    private static string Pronoun(Character c, string form, EvalContext ctx)
    {
        string sex = c.Sex is "male" or "female" ? c.Sex : ctx.Content?.Names.SexOf(First(c.Name)) ?? "";
        if (sex == "male") return form == "hers" ? "his" : form;
        if (sex == "female") return form switch { "he" => "she", "him" or "his" => "her", "hers" => "hers", _ => "herself" };
        return form switch { "he" or "him" => First(c.Name), "his" or "hers" => First(c.Name) + "'s", _ => "themself" };
    }

    private static string Capital(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
