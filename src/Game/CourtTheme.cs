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

    public static Label Heading(string text, Color color, int size = 20)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        return l;
    }

    public static Label Body(string text, Color color, int size = 14, bool wrap = true)
    {
        var l = new Label { Text = text };
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

    public static void Clear(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }
}
