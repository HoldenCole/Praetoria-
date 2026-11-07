using Microsoft.AspNetCore.Components;

namespace Praetoria.Web.Services;

/// <summary>SVG helpers Razor cannot express directly (its own &lt;text&gt; tag is reserved markup).</summary>
public static class Svg
{
    public static string F(double v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    public static MarkupString Text(double x, double y, string cls, string text, string extra = "") =>
        new($"<text x=\"{F(x)}\" y=\"{F(y)}\" class=\"{cls}\" {extra}>{System.Net.WebUtility.HtmlEncode(text)}</text>");
    public static string Tri(double x, double y, double s) => $"M{F(x)},{F(y - s)} L{F(x + s * 0.85)},{F(y + s * 0.6)} L{F(x - s * 0.85)},{F(y + s * 0.6)} Z";
    public static string Arc(double cx, double cy, double r, double frac)
    {
        if (frac <= 0) return "";
        double a = Math.Min(0.9999, frac) * Math.PI * 2 - Math.PI / 2;
        return $"M{F(cx)},{F(cy - r)} A{F(r)},{F(r)} 0 {(frac > 0.5 ? 1 : 0)} 1 {F(cx + Math.Cos(a) * r)},{F(cy + Math.Sin(a) * r)}";
    }
    /// <summary>Colours for a planet class: surface and glow.</summary>
    public static (string fill, string glow) Planet(string cls) => cls switch
    {
        "arid" => ("#c98f4a", "#e0b070"), "ocean" => ("#2f6fb5", "#6fb0ff"), "forge" => ("#b5452a", "#ff8a5a"),
        "frozen" => ("#a9c9e6", "#e6f4ff"), "gas" => ("#c9a06a", "#f0d0a0"), "belt" => ("#8a7f70", "#c9bca8"),
        "barren" => ("#6f6a66", "#a9a39c"), _ => ("#3f9a6a", "#8ad9a8")
    };
}
