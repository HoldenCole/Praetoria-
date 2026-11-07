namespace Praetoria.Web.Services;

/// <summary>Landscape photographs for the worlds (public domain, via the Endless Sky collection; see CREDITS.md), keyed by
/// planet class. A world always shows the same picture: chosen by a hash of its id.</summary>
public static class Land
{
    private static readonly System.Collections.Generic.Dictionary<string, string[]> ByClass = new()
    {
        ["terran"] = new[] { "forest14.jpg", "hills11.jpg", "forest9.jpg", "fields20.jpg" },
        ["arid"] = new[] { "desert2.jpg", "dune0.jpg", "canyon4.jpg", "canyon0.jpg" },
        ["ocean"] = new[] { "water16.jpg", "sea19.jpg", "beach5.jpg", "water28.jpg" },
        ["forge"] = new[] { "city26.jpg", "city28.jpg", "city21.jpg", "city0.jpg" },
        ["frozen"] = new[] { "snow19.jpg", "snow39.jpg", "snow5.jpg", "snow36.jpg" },
        ["gas"] = new[] { "clouds9.jpeg", "sky0.jpg", "sky8.jpg", "clouds11.jpg" },
        ["belt"] = new[] { "moon1.jpg", "mercury1.jpg", "moon0.jpg", "asteroid0.jpg" },
        ["barren"] = new[] { "mars2.jpg", "badlands8.jpg", "badlands5.jpg", "badlands1.jpg" },
    };

    public static string For(string planetClass, string seedId)
    {
        if (!ByClass.TryGetValue(planetClass, out var files) || files.Length == 0) files = ByClass["terran"];
        unchecked { int h = 17; foreach (var ch in seedId) h = h * 31 + ch; return "img/land/" + files[Math.Abs(h) % files.Length]; }
    }
}
