using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Presentation;

/// <summary>The saga epilogue (GDD §14): a chronicle of the dynasty's rise and fall, generated from the World's
/// record. Shown at dynasty death, at a proclaimed throne, or on demand. Prose, not numbers, where it can be.</summary>
public static class Epilogue
{
    public static string Generate(World w, ContentDatabase content)
    {
        var p = w.Protagonist;
        var house = p != null ? w.House(p.HouseId) : null;
        string name = house?.Name ?? "the house";
        var lines = new List<string>();

        int generations = w.Counter("generation") + 1;
        var heads = w.History.Where(h => h.Text.Contains("succeeds as head of")).Select(h => h.Text.Split(" succeeds")[0].Replace("yields the seat; ", "").Split(';').Last().Trim()).ToList();
        string title = content.Titles.NameOf(house?.Title ?? "landless", w.Era);
        int peak = Math.Max(w.Counter("peak_title_rank"), content.Titles.RankOf(house?.Title ?? "landless"));
        string peakName = content.Titles.ByRank.FirstOrDefault(t => t.Rank == peak)?.NameIn(w.Era) ?? title;

        lines.Add($"THE CHRONICLE OF {name.ToUpperInvariant()}");
        lines.Add("");
        // a founder of the player's own making is named as such, and one made without limits is named as that too (docs/CHARACTER.md)
        if (w.HasFlag(Creation.CharacterWorkshop.CustomFlag))
            lines.Add(w.HasFlag(Creation.CharacterWorkshop.FreeFlag)
                ? $"Its founder was made by the player's own hand, without limits: {Creation.CharacterWorkshop.Rating(w.Counter(Creation.CharacterWorkshop.PowerCounter))} at {w.Counter(Creation.CharacterWorkshop.PowerCounter)} points. Read what follows in that light."
                : $"Its founder was made by the player's own hand within the standard budget ({w.Counter(Creation.CharacterWorkshop.PowerCounter)} points).");
        lines.Add(generations == 1
            ? $"For {w.Turn} years {name} was one life: {p?.Name ?? "its head"}, who began as {(content.Careers.RankName(p?.CareerTrack ?? "", 0, w.Era) ?? "a youth")} and ended as {(p != null ? content.Careers.RankName(p.CareerTrack, p.CareerRank, w.Era) ?? "" : "")}."
            : $"Over {w.Turn} years and {generations} generations the seat of {name} passed {generations - 1} time{(generations == 2 ? "" : "s")}{(heads.Count > 0 ? ", to " + string.Join(", then to ", heads.Distinct()) : "")}.");
        lines.Add($"It rose as high as {peakName}{(peakName != title ? $" and ended as {title}" : "")}, with legitimacy {house?.Legitimacy ?? 0} in the eyes of the Reach.");

        int carried = house != null ? w.Counter("motions_passed:" + house.Id) : 0;
        if (carried > 0) lines.Add($"In the Senate it carried {carried} motion{(carried == 1 ? "" : "s")}; the chamber knew its name.");
        var sb = new List<string>();
        int wars = w.Counter(FleetSystem.WarsDeclaredCounter), battles = w.Counter(FleetSystem.BattlesCounter), won = w.Counter(FleetSystem.BattlesWonCounter);
        if (wars > 0) sb.Add(wars == 1 ? "a war was declared in its time" : $"{wars} wars were declared in its time");
        if (battles > 0) sb.Add(battles == 1 ? $"a single battle was fought{(won == 1 ? ", and won" : ", and lost")}" : $"{battles} battles were fought, {won} of them its victories");
        if (w.Counter(FleetSystem.ConquestsCounter) > 0) sb.Add($"it took {Plural(w.Counter(FleetSystem.ConquestsCounter), "world")} by force");
        if (w.Counter("conquests") == 0 && w.HasFlag(FleetSystem.HoldingLostFlag)) sb.Add("it lost worlds it could not hold");
        int births = w.Counter("dynasty_births"), marriages = w.Counter("marriages");
        if (births > 0) sb.Add(births == 1 ? "one child was born to the name" : $"{births} children were born to the name");
        if (marriages > 0) sb.Add(marriages == 1 ? "a marriage bound it to another house" : $"{marriages} marriages bound it to other houses");
        if (w.Counter("honors") > 0) sb.Add($"its heads were invested with {Plural(w.Counter("honors"), "honor")}");
        if (w.Counter("megastructures") > 0) sb.Add("it raised a great work the whole Reach could see");
        if (w.Counter("assassinations_attempted") > 0) sb.Add("knives were bought, and some found their mark");
        if (w.Counter("breakdowns") > 0) sb.Add($"{Plural(w.Counter("breakdowns"), "time")} the weight of it broke the one who carried it");
        if (sb.Count > 0) lines.Add(Cap(string.Join("; ", sb)) + ".");

        var crises = w.History.Where(h => h.Text.StartsWith("Crisis '")).Count();
        if (crises > 0) lines.Add($"The Reach's weather was never still: {Plural(crises, "crisis")} broke over it.");

        lines.Add("");
        if (w.HasFlag(ThroneSystem.SeizedFlag)) lines.Add($"In the end {name} took the throne itself, by force, and ruled a powder keg: Emperor in name, and in the Reach's memory a usurper until the generations forget.");
        else if (w.HasFlag(ThroneSystem.ProclaimedFlag)) lines.Add($"In the end {name} proclaimed the Imperium and the houses knelt — some gladly, some not — and an age that had no name was given one.");
        else if (w.IsDynastyDead) lines.Add($"And then there was no one left to carry the name. {name} ends here, on the {Ordinal(w.Turn)} year. What it built passes to others; what it was, passes to this chronicle.");
        else if (w.HasFlag(DynastySystem.DynastyAdoptedFlag)) lines.Add($"The blood of the old house failed, and a sworn house took up its banner. The name goes on; the family does not.");
        else lines.Add($"The story is not over. {name} stands in the {Ordinal(w.Turn)} year, {(w.Era == "imperium" ? "under the Imperium" : "in the Republic")}, and the next season will ask something of it.");

        return string.Join("\n", lines);
    }

    private static string Plural(int n, string noun) => n == 1 ? $"one {noun}" : $"{n} {(noun == "crisis" ? "crises" : noun == "child" ? "children" : noun + "s")}";
    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
    private static string Ordinal(int n) => n % 100 is 11 or 12 or 13 ? $"{n}th" : (n % 10) switch { 1 => $"{n}st", 2 => $"{n}nd", 3 => $"{n}rd", _ => $"{n}th" };
}
