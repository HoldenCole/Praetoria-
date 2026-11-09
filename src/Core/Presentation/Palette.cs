using Praetoria.Core.State;

namespace Praetoria.Core.Presentation;

/// <summary>The one palette (the owner's direction, 2026-11-09): bright, well-separated colours — red, blue, yellow, green, silver first, then
/// orange, violet, cyan, magenta and the rest — for houses, parties, regions and planet classes, so nothing on a screen is dull and no two
/// neighbours look alike. Houses are assigned in a deterministic order at world creation (the protagonist's house first) and whenever a new house
/// is born; regions take their own set by position; parties take theirs as they form.</summary>
public static class Palette
{
    /// <summary>Twenty-four house colours ordered so that consecutive picks differ in hue; the first five are the owner's named set.</summary>
    public static readonly string[] Houses =
    {
        "#E63946", "#3A86FF", "#FFD60A", "#2EC27E", "#C9D1DC",   // red, blue, yellow, green, silver
        "#FF8C42", "#9B5DE5", "#00D4FF", "#FF4FA3", "#A7E34B",   // orange, violet, cyan, magenta, lime
        "#F5B700", "#14B8A6", "#FF6B6B", "#4C7DFF", "#F7F3E3",   // gold, teal, coral, cobalt, ivory
        "#C77DFF", "#7CFC00", "#FFA600", "#5EEAD4", "#FF2D95",   // lilac, chartreuse, amber, aqua, hot pink
        "#B22222", "#1E90FF", "#E8E337", "#00A86B"               // firebrick, dodger, canary, jade
    };
    /// <summary>Region colours: a brighter, softer set that reads under the map's ring tint and never clashes with a house's heraldry.</summary>
    public static readonly string[] Regions =
    {
        "#FFB3B3", "#9EC5FF", "#FFF3A3", "#B5F0C9", "#E6EAF2", "#FFD1A8", "#D9C2FF", "#A8F0FF", "#FFC2E0", "#DCF5A8", "#FFE08A", "#A8E6DF"
    };
    /// <summary>Party colours for the Senate's benches, taken as they form.</summary>
    public static readonly string[] Parties = { "#E63946", "#3A86FF", "#FFD60A", "#2EC27E", "#C9D1DC", "#FF8C42", "#9B5DE5", "#00D4FF", "#FF4FA3", "#A7E34B" };

    public static string House(int index) => Houses[((index % Houses.Length) + Houses.Length) % Houses.Length];
    public static string Region(int index) => Regions[((index % Regions.Length) + Regions.Length) % Regions.Length];
    public static string Party(int index) => Parties[((index % Parties.Length) + Parties.Length) % Parties.Length];

    /// <summary>Give every house a palette colour: the protagonist's house first, the rest by id, raiders left to their own dark red.</summary>
    public static void AssignHouses(World w)
    {
        var mine = w.Protagonist?.HouseId;
        var ordered = w.Houses.Values.Where(h => h.Id != Systems.PirateSystem.HouseId).OrderBy(h => h.Id == mine ? 0 : 1).ThenBy(h => h.Id, StringComparer.Ordinal).ToList();
        for (int i = 0; i < ordered.Count; i++) ordered[i].AccentColor = House(i);
    }

    /// <summary>A colour for a house born in play: the first palette colour no living house wears, else the one its id hashes to.</summary>
    public static string NextHouseColor(World w, string id)
    {
        var used = w.Houses.Values.Select(h => h.AccentColor).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var c in Houses) if (!used.Contains(c)) return c;
        uint hash = 2166136261; foreach (var ch in id) hash = (hash ^ ch) * 16777619;
        return House((int)(hash % (uint)Houses.Length));
    }
}
