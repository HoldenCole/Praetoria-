using System.Text.RegularExpressions;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The icon sprite in the web client's index.html is written from src/Web/Services/Icons.cs by tools/icons.py, once, outside
/// Blazor's render tree (docs/VISUAL_SYSTEM.md 8.1 and check 7 of section 13). A glyph added to Icons.cs and not to the sprite would
/// draw as nothing, so the two must agree, and every gradient and symbol must be in the document exactly once.</summary>
public class IconSpriteTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Praetoria.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("no Praetoria.sln above " + AppContext.BaseDirectory);
    }

    [Fact]
    public void EveryGlyphIsInTheSpriteOnceAndAsDrawn()
    {
        var root = Root();
        var cs = File.ReadAllText(Path.Combine(root, "src/Web/Services/Icons.cs"));
        var html = File.ReadAllText(Path.Combine(root, "src/Web/wwwroot/index.html"));
        var paths = new Dictionary<string, string>();
        foreach (Match m in Regex.Matches(cs, "\\[\"([^\"]+)\"\\] = \"([^\"]+)\"")) paths.TryAdd(m.Groups[1].Value, m.Groups[2].Value);
        var symbols = Regex.Matches(html, "<symbol id=\"i-([^\"]+)\" viewBox=\"0 0 512 512\"><path d=\"([^\"]+)\"/></symbol>").Select(m => (id: m.Groups[1].Value, d: m.Groups[2].Value)).ToList();
        Assert.True(paths.Count > 100, "Icons.cs read " + paths.Count + " glyphs");
        Assert.Equal(symbols.Count, symbols.Select(s => s.id).Distinct().Count());
        foreach (var (key, d) in paths)
        {
            var s = symbols.Where(x => x.id == key).ToList();
            Assert.True(s.Count == 1, $"the glyph {key} is in the sprite {s.Count} times: run python3 tools/icons.py");
            Assert.True(s[0].d == d, $"the glyph {key} in the sprite differs from Icons.cs: run python3 tools/icons.py");
        }
        foreach (var g in new[] { "brass", "ivory", "ivory-hi", "steel", "ember", "good", "gold", "rim" })
            Assert.Single(Regex.Matches(html, $"id=\"g-{Regex.Escape(g)}\""));
        // the sprite sits outside the Blazor root, so a re-render can never duplicate or drop it
        Assert.True(html.IndexOf("<!-- icons:end -->", StringComparison.Ordinal) < html.IndexOf("<div id=\"app\">", StringComparison.Ordinal));
    }
}
