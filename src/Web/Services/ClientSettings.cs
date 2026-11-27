using System.Text.Json;

namespace Praetoria.Web.Services;

/// <summary>The settings a browser keeps for the game (docs/WEB_CLIENT.md, "Settings"): the text size, which keys act, and whether the
/// rail's sockets carry their names under them (docs/VISUAL_SYSTEM.md 3.3, "Show rail labels").</summary>
public sealed record ClientSettings(string Text, bool EnterEnds, bool NumberKeys, bool RailLabels = false)
{
    public static ClientSettings Default { get; } = new("normal", true, true, false);
    /// <summary>Read what the page's store holds; anything missing or unreadable is the default.</summary>
    public static ClientSettings Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Default;
        try
        {
            using var doc = JsonDocument.Parse(json); var r = doc.RootElement;
            string text = r.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString()! : "normal";
            if (text is not ("small" or "normal" or "large" or "larger")) text = "normal";
            bool B(string name) => !r.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.False;
            bool railLabels = r.TryGetProperty("railLabels", out var rl) && rl.ValueKind == JsonValueKind.True;
            return new ClientSettings(text, B("enterEnds"), B("numberKeys"), railLabels);
        }
        catch { return Default; }
    }
}
