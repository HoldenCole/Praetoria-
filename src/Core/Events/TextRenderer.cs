using System.Text;
using Praetoria.Core.State;

namespace Praetoria.Core.Events;

/// <summary>
/// Substitutes {role.field} tokens in authored prose against the live binding (BuildSpec §4
/// "role-variable substitution"). This is what turns one authored line into many lived ones:
/// {rival.name} becomes whoever the Binder chose. Supported fields: name, house, title-ish
/// rank, and a possessive pronoun stub. Unknown tokens are left intact (and reported by the
/// content validator) rather than crashing a run.
/// </summary>
public static class TextRenderer
{
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
        var parts = token.Split('.', 2);
        if (parts.Length != 2) return original;

        string role = parts[0];
        string field = parts[1];

        var c = ctx.Actor(role);
        if (c == null) return original;

        switch (field)
        {
            case "name": return c.Name;
            case "house":
                return ctx.World.Houses.TryGetValue(c.HouseId, out var h) ? h.Name : c.HouseId;
            case "rank": return c.CareerRank.ToString();
            default: return original;
        }
    }
}
