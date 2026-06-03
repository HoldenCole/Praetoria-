using Praetoria.Core.Events;
using Praetoria.Core.Rng;
using Praetoria.Core.State;

namespace Praetoria.Tools;

/// <summary>Renders the headless game to text for the console harness (BuildSpec §M1 — "play as text").
/// Joins logic (engine) with prose (text store) at the point of display, substituting role tokens.</summary>
public static class Renderer
{
    // Substitution reads only World + Binding; the RNG is irrelevant here, so a fixed stub is fine.
    private static readonly IRng NoRng = new SplitMix64Rng(0);

    public static void RenderEvent(FiredEvent fired, IReadOnlyList<OfferedChoice> choices,
        EventText? text, World world)
    {
        var ctx = new EvalContext(world, fired.Binding, NoRng);

        string title = text?.Title ?? fired.Def.Id;
        Console.WriteLine();
        Console.WriteLine($"  ┌─ {TextRenderer.Substitute(title, ctx)}  [{fired.Def.Tier}]");
        if (text != null && !string.IsNullOrWhiteSpace(text.Body))
            Console.WriteLine(Indent(TextRenderer.Substitute(text.Body, ctx), "  │  "));

        for (int i = 0; i < choices.Count; i++)
        {
            var oc = choices[i];
            string label = text != null && text.Choices.TryGetValue(oc.Choice.Id, out var t)
                ? TextRenderer.Substitute(t, ctx)
                : oc.Choice.Id;
            string marker = oc.Available ? $"{i + 1}" : "✗";
            string gated = oc.Available ? "" : "  (requirements not met)";
            Console.WriteLine($"  │   [{marker}] {label}{gated}");
        }
        Console.WriteLine("  └─");
    }

    public static void RenderStatus(World world)
    {
        var p = world.Protagonist!;
        Console.WriteLine($"── Turn {world.Turn} ──  {p.Name} of {HouseName(world, p.HouseId)}  " +
                          $"· rank {p.CareerRank} · stress {p.Stress}");
    }

    public static void RenderPools(ActionPools pools) =>
        Console.WriteLine($"   Pools — influence {pools.Influence} · treasury {pools.Treasury} · agents {pools.Agents}");

    /// <summary>The five-resource treasury (GDD §17) for a house — the domain economy at a glance.</summary>
    public static void RenderTreasury(House house)
    {
        var t = house.Treasury;
        Console.WriteLine($"   {house.Name} treasury — credits {t.Credits} · materials {t.Materials} · " +
                          $"manpower {t.Manpower} · influence {t.Influence} · exotics {t.Exotics}");
    }

    /// <summary>The holdings a house owns, with specialization, population/unrest, and built slots.</summary>
    public static void RenderHoldings(World world, string houseId)
    {
        foreach (var h in world.HoldingsOf(houseId).OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            string built = h.Buildings.Count == 0 ? "—" : string.Join(", ", h.Buildings);
            Console.WriteLine($"     · {h.Name} [{h.Specialization}]  pop {h.Population} · unrest {h.Unrest}  buildings: {built}");
        }
    }

    public static void RenderCommandLog(IReadOnlyList<string> log, int from)
    {
        if (from >= log.Count) return;
        Console.WriteLine("   · actions:");
        for (int i = from; i < log.Count; i++)
            Console.WriteLine($"       {log[i]}");
    }

    public static void RenderEpilogueHints(World world)
    {
        Console.WriteLine();
        Console.WriteLine("History:");
        foreach (var h in world.History)
            Console.WriteLine($"  T{h.Turn}: {h.Text}");
    }

    private static string HouseName(World w, string houseId) =>
        w.Houses.TryGetValue(houseId, out var h) ? h.Name : houseId;

    private static string Indent(string text, string prefix)
    {
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++) lines[i] = prefix + lines[i].TrimEnd();
        return string.Join('\n', lines);
    }
}
