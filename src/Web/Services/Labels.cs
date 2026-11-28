namespace Praetoria.Web.Services;

/// <summary>Labels as the windows write them (docs/VISUAL_SYSTEM.md 2.3): sentence case, one helper for the outliner, the House window
/// and the map, so a kind of world reads the same everywhere ("Agri-World" in the content reads "Agri-world").</summary>
public static class Labels
{
    /// <summary>The first letter in capitals and the rest not: "Forge-World" reads "Forge-world", "Mining Outpost" "Mining outpost".</summary>
    public static string Sentence(string? s) => string.IsNullOrEmpty(s) ? s ?? "" : char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();
}
