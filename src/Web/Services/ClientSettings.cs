using System.Text.Json;

namespace Praetoria.Web.Services;

/// <summary>The settings a browser keeps for the game (docs/WEB_CLIENT.md, "Settings"): the text size, and which keys act.</summary>
public sealed record ClientSettings(string Text, bool EnterEnds, bool NumberKeys)
{
    public static ClientSettings Default { get; } = new("normal", true, true);
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
            return new ClientSettings(text, B("enterEnds"), B("numberKeys"));
        }
        catch { return Default; }
    }
}
