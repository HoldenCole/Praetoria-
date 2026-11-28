using System.Text.Json;

namespace Praetoria.Web.Services;

/// <summary>The settings a browser keeps for the game (docs/WEB_CLIENT.md, "Settings"): the text size, which keys act, and whether the
/// rail's sockets carry their names under them (docs/VISUAL_SYSTEM.md 3.3, "Show rail labels"). <see cref="RailLabels"/> is what the page
/// shows; <see cref="RailLabelsChosen"/> is what the player chose in Options, or null while they have not, when a first game shows the
/// labels by themselves for its first years (<see cref="GameClient.LabelsByThemselves"/>). <see cref="Guided"/> is the guidance (docs/GOALS.md,
/// "Guided or sandbox"): on, the map shows the player's aim; off (a sandbox) it shows only the warning before ruin.</summary>
public sealed record ClientSettings(string Text, bool EnterEnds, bool NumberKeys, bool RailLabels = false, bool? RailLabelsChosen = null, bool Guided = true)
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
            bool? chosen = r.TryGetProperty("railLabels", out var rl) && rl.ValueKind is JsonValueKind.True or JsonValueKind.False ? rl.ValueKind == JsonValueKind.True : null;
            return new ClientSettings(text, B("enterEnds"), B("numberKeys"), chosen == true, chosen, B("guided"));
        }
        catch { return Default; }
    }
    /// <summary>The settings with the rail's labels as they are to be shown: the player's choice, or the first game's own when there is none.</summary>
    public ClientSettings WithLabels(bool byThemselves) => this with { RailLabels = RailLabelsChosen ?? byThemselves };
}
