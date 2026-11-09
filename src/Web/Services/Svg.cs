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
        "arid" => ("#FFB03B", "#FFE08A"), "ocean" => ("#2F7BFF", "#8CC8FF"), "forge" => ("#FF4D2E", "#FFA07A"),
        "frozen" => ("#CFE9FF", "#FFFFFF"), "gas" => ("#F2C27B", "#FFE6B0"), "belt" => ("#B8AFA0", "#E0D8C8"),
        "barren" => ("#C9D1DC", "#F0F4F8"), _ => ("#3DDC84", "#B2FFD6")
    };
}
