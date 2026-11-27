using Praetoria.Core.State;

namespace Praetoria.Core.Presentation;

/// <summary>The one palette (docs/VISUAL_SYSTEM.md 2.2): gold means yours, red means danger. The player's house is gold unless they
/// choose another colour in the creator; every other house takes the list below in order, which holds no gold, amber, yellow, orange
/// or alarm red, and no ivory (the Reach's edge on the map is ivory). Houses are assigned in a deterministic order at world creation
/// (the protagonist's house first) and whenever a new house is born; regions take their own set by position; parties take theirs as
/// they form. The rule for any colour added later is <see cref="RivalAllowed"/>, and tests/PaletteTests.cs holds every list to it.</summary>
public static class Palette
{
    /// <summary>The player's house colour by default: the gold of what is yours (portrait bands, the standard, the seal).</summary>
    public const string Player = "#D4A23E";

    /// <summary>The other houses' colours, in order: blue, green, silver, violet, cyan, magenta, lime, teal, oxblood, cobalt, lilac, aqua,
    /// hot pink, dodger, jade, rose, slate, plum, indigo, mint, sky, orchid, chartreuse, wisteria.</summary>
    public static readonly string[] Houses =
    {
        "#3A86FF", "#2EC27E", "#C9D1DC", "#9B5DE5", "#00D4FF", "#FF4FA3", "#A7E34B", "#14B8A6",
        "#A3324A", "#4C7DFF", "#C77DFF", "#5EEAD4", "#FF2D95", "#1E90FF", "#00A86B", "#E7849B",
        "#8C9BB8", "#8E4C9E", "#5B5FE0", "#8FE3B0", "#8EC9FF", "#D36BD8", "#7CFC00", "#B07CC6"
    };
    /// <summary>The colours the creator offers for a new house: the player's gold first, then the house list.</summary>
    public static IReadOnlyList<string> Choices => new[] { Player }.Concat(Houses).ToArray();

    /// <summary>Whether a rival house may wear a colour: no hue between 28 and 70 degrees above 0.35 saturation (gold and yellow, which
    /// are yours), and a strong red (345 to 15 degrees, saturation above 0.5) only at value 0.65 or below (oxblood), because red is
    /// danger; a pale rose passes. Raiders keep their dark
    /// red, being a danger. Nations beyond the Reach follow the same rule.</summary>
    public static bool RivalAllowed(string hex)
    {
        var (h, sat, v) = Hsv(hex);
        if (h >= 28 && h <= 70 && sat > 0.35) return false;
        if ((h >= 345 || h <= 15) && sat > 0.5 && v > 0.65) return false;
        return true;
    }

    /// <summary>Hue in degrees, saturation and value of a #RRGGBB colour.</summary>
    public static (double hue, double sat, double val) Hsv(string hex)
    {
        hex = hex.TrimStart('#');
        double r = Convert.ToInt32(hex[..2], 16) / 255.0, g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255.0, b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min, h = 0;
        if (d > 0) h = max == r ? 60 * (((g - b) / d) % 6) : max == g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4);
        if (h < 0) h += 360;
        return (h, max == 0 ? 0 : d / max, max);
    }
    /// <summary>Region colours: a brighter, softer set that reads under the map's ring tint and never clashes with a house's heraldry.</summary>
    public static readonly string[] Regions =
    {
        "#FFB3B3", "#9EC5FF", "#FFF3A3", "#B5F0C9", "#E6EAF2", "#FFD1A8", "#D9C2FF", "#A8F0FF", "#FFC2E0", "#DCF5A8", "#FFE08A", "#A8E6DF"
    };
    /// <summary>Party colours for the Senate's benches, taken as they form.</summary>
    public static readonly string[] Parties = { "#E63946", "#3A86FF", "#FFD60A", "#2EC27E", "#C9D1DC", "#FF8C42", "#9B5DE5", "#00D4FF", "#FF4FA3", "#A7E34B" };

    /// <summary>The colour of the index-th house in the order houses are assigned: 0 is the player's gold, the rest the house list.</summary>
    public static string House(int index) => index == 0 ? Player : Houses[(((index - 1) % Houses.Length) + Houses.Length) % Houses.Length];
    public static string Region(int index) => Regions[((index % Regions.Length) + Regions.Length) % Regions.Length];
    public static string Party(int index) => Parties[((index % Parties.Length) + Parties.Length) % Parties.Length];

    /// <summary>Give every house a palette colour: the protagonist's house gold, the rest the house list by id, raiders left to their
    /// own dark red.</summary>
    public static void AssignHouses(World w)
    {
        var mine = w.Protagonist?.HouseId;
        var others = w.Houses.Values.Where(h => !World.Outsider(h.Id) && h.Id != mine).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        if (mine != null && w.Houses.TryGetValue(mine, out var own)) own.AccentColor = Player;
        for (int i = 0; i < others.Count; i++) others[i].AccentColor = Houses[i % Houses.Length];
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
