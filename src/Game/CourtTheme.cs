using Godot;

namespace Praetoria.Game;

/// <summary>
/// The base theme (BuildSpec §6 "build a strong base theme first; layer era/role reactivity later") with the
/// first slice of reactivity (GDD §19): the palette shifts between the fractured-stars era (cold steel) and
/// the Imperium (gilt), and the player's house accent tints headers. Court mode reads as ceremonial (serif
/// sizes, generous panels); Galaxy mode as functional (compact, technical). All colours live here.
/// </summary>
public static class CourtTheme
{
    // System fonts by family so no font files ship yet: Court speaks in a serif, Galaxy in a technical sans.
    // Godot falls back along the list, then to its built-in font, so a missing family is never a crash.
    private static SystemFont? _serif, _sans;
    public static SystemFont Serif => _serif ??= new SystemFont { FontNames = new[] { "Georgia", "Liberation Serif", "DejaVu Serif", "Times New Roman", "serif" } };
    public static SystemFont Sans => _sans ??= new SystemFont { FontNames = new[] { "Inter", "Liberation Sans", "DejaVu Sans", "Segoe UI", "sans-serif" } };

    public static Color Background(string era) => era == "imperium" ? Color.FromHtml("#17130C") : Color.FromHtml("#0E1116");
    public static Color Panel(string era) => era == "imperium" ? Color.FromHtml("#26201A") : Color.FromHtml("#171C24");
    public static Color PanelAlt(string era) => era == "imperium" ? Color.FromHtml("#2F2820") : Color.FromHtml("#1E2530");
    public static Color Ink(string era) => era == "imperium" ? Color.FromHtml("#EADFC8") : Color.FromHtml("#D9E1EA");
    public static Color Muted(string era) => era == "imperium" ? Color.FromHtml("#A8997E") : Color.FromHtml("#8893A1");
    public static Color Gilt(string era) => era == "imperium" ? Color.FromHtml("#C9A24A") : Color.FromHtml("#6FA3D8");
    public static readonly Color Danger = Color.FromHtml("#C24A3A");
    public static readonly Color Good = Color.FromHtml("#5FA86B");
    public static readonly Color Lane = Color.FromHtml("#3A4350");
    public static readonly Color Unclaimed = Color.FromHtml("#3A3F47");

    public static Color Accent(string? html, string era)
    {
        if (string.IsNullOrEmpty(html)) return Gilt(era);
        try { return Color.FromHtml(html); } catch { return Gilt(era); }
    }

    public static PanelContainer MakePanel(Color bg, int margin = 10, int radius = 6)
    {
        var p = new PanelContainer();
        var sb = new StyleBoxFlat
        {
            BgColor = bg,
            ContentMarginLeft = margin, ContentMarginRight = margin,
            ContentMarginTop = margin, ContentMarginBottom = margin
        };
        sb.SetCornerRadiusAll(radius);
        p.AddThemeStyleboxOverride("panel", sb);
        return p;
    }

    public static Label Heading(string text, Color color, int size = 20, bool serif = true)
    {
        var l = new Label { Text = text };
        l.AddThemeFontOverride("font", serif ? Serif : Sans);
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        return l;
    }

    public static Label Body(string text, Color color, int size = 14, bool wrap = true, bool serif = true)
    {
        var l = new Label { Text = text };
        l.AddThemeFontOverride("font", serif ? Serif : Sans);
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        if (wrap)
        {
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        }
        return l;
    }

    public static Label Muted(string text, string era, int size = 12) => Body(text, Muted(era), size);

    public static HSeparator Rule()
    {
        var s = new HSeparator();
        s.AddThemeConstantOverride("separation", 8);
        return s;
    }

    public static Button Action(string text, bool enabled = true, string? tooltip = null)
    {
        var b = new Button { Text = text, Disabled = !enabled };
        b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        if (tooltip != null) b.TooltipText = tooltip;
        return b;
    }

    public static VBoxContainer Column(int separation = 8)
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", separation);
        v.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        v.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        return v;
    }

    /// <summary>The portrait slot (GDD §4 "text + character portraits"): until art exists, a badge in the house
    /// accent bearing the character's initials. Drop a TextureRect in its place when portraits arrive.</summary>
    public static Control Portrait(string name, Color accent, Color ink, int size = 56)
    {
        var badge = new PortraitBadge(Initials(name), accent, ink) { CustomMinimumSize = new Vector2(size, size) };
        return badge;
    }

    private static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        return parts.Length == 1 ? parts[0][..1].ToUpperInvariant() : (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
    }

    public static void Clear(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }
}

/// <summary>A drawn initials badge standing in for a portrait.</summary>
public partial class PortraitBadge : Control
{
    private readonly string _initials;
    private readonly Color _accent, _ink;

    public PortraitBadge(string initials, Color accent, Color ink) { _initials = initials; _accent = accent; _ink = ink; }

    public override void _Draw()
    {
        float r = Mathf.Min(Size.X, Size.Y) / 2f;
        var c = Size / 2f;
        DrawCircle(c, r, _accent);
        DrawArc(c, r - 1.5f, 0, Mathf.Tau, 48, new Color(_ink, 0.6f), 2f, true);
        int fs = (int)(r * 0.9f);
        var font = CourtTheme.Serif;
        var sz = font.GetStringSize(_initials, HorizontalAlignment.Left, -1, fs);
        DrawString(font, new Vector2(c.X - sz.X / 2f, c.Y + sz.Y / 4f), _initials, HorizontalAlignment.Left, -1, fs, _ink);
    }
}
