using Praetoria.Core.Crises;
using Praetoria.Core.Events;
using Praetoria.Core.Rng;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Presentation;

/// <summary>
/// A crisis put into words from its own definition (docs/PLAYTHROUGH.md, the Senate: "crises explained: what it is, who is behind
/// it, what it does each year, what each answer costs"). Nothing here is written by hand for one crisis: the lines are read from
/// the gates, the effects and the answers the crisis is made of, so they say what the rules do. Read only.
/// </summary>
public static class CrisisFacts
{
    public sealed record Line(string Label, string Text);

    private static string Word(string key) => key.Replace('_', ' ');
    private static string Signed(int n) => n > 0 ? "+" + n : n < 0 ? "−" + (-n) : "0";
    private static string OpWord(CompareOp op) => op.ToString().ToLowerInvariant() switch { "gte" or "ge" => "at or above", "gt" => "above", "lte" or "le" => "at or below", "lt" => "below", "eq" => "at", _ => "not at" };

    /// <summary>One gate in words, with how it stands now.</summary>
    public static string Gate(World w, ICondition c, EvalContext ctx) => c switch
    {
        CounterCondition k => $"{Word(k.Key)} {OpWord(k.Op)} {k.Value} (it is {w.Counter(k.Key)})",
        WorldFlagCondition f => f.Value ? $"the Reach marked \"{Word(f.Flag)}\" ({(w.HasFlag(f.Flag) ? "it is" : "it is not")})" : $"the Reach not marked \"{Word(f.Flag)}\" ({(w.HasFlag(f.Flag) ? "it is" : "it is not")})",
        _ => c.Evaluate(ctx) ? "a condition that holds" : "a condition that does not hold",
    };

    /// <summary>What a list of effects does, in words.</summary>
    public static string Does(IEnumerable<IEffect> effects)
    {
        var parts = new List<string>();
        foreach (var e in effects)
            switch (e)
            {
                case AdjustCounterEffect a when a.Delta != 0: parts.Add($"{Word(a.Key)} {Signed(a.Delta)}"); break;
                case SetWorldFlagEffect f: parts.Add(f.Value ? $"the Reach is marked \"{Word(f.Flag)}\"" : $"the mark \"{Word(f.Flag)}\" is lifted"); break;
                case AdjustResourceEffect r when r.Delta != 0: parts.Add($"{Signed(r.Delta)} {Resource.Label(r.ResourceKey)} to your treasury"); break;
            }
        return parts.Count == 0 ? "nothing that is counted" : string.Join(", ", parts);
    }

    public static List<Line> For(World w, CrisisEngine engine, CrisisDef def, IReadOnlyList<RecoveryRule>? recovery = null)
    {
        /* a reader draws nothing from the game's own generator: gates draw none, and this one is thrown away */
        var lines = new List<Line>(); var ctx = new EvalContext(w, new Binding(w.ProtagonistId), new SplitMix64Rng(1));
        w.Crises.TryGetValue(def.Id, out var active);
        bool holds = def.Gates.All(g => g.Evaluate(ctx));
        lines.Add(new Line("What brings it on", (def.Gates.Count == 0 ? "Nothing is needed." : "All of: " + string.Join("; ", def.Gates.Select(g => Gate(w, g, ctx))) + ".")
            + " Once those hold it may break out of itself in any year, or a house may set it going."));
        if (active != null)
            lines.Add(new Line("Who is behind it", active.Organic ? "Nobody: it came of how things stood." : $"{w.Char(active.AuthorId)?.Name ?? active.AuthorId}{(w.Char(active.AuthorId) is { } a && w.House(a.HouseId) is { } ah ? " of " + ah.Name : "")} set it going."));
        lines.Add(new Line("What it did when it began", Does(def.OnTrigger) + "."));
        // the marks it leaves are the gates of other crises: say which
        var feeds = def.OnTrigger.OfType<SetWorldFlagEffect>().Where(f => f.Value)
            .SelectMany(f => engine.Defs.Where(o => o.Id != def.Id && o.Gates.OfType<WorldFlagCondition>().Any(g => g.Value && g.Flag == f.Flag)).Select(o => $"\"{Word(f.Flag)}\" is one of the {o.Gates.Count} thing(s) {o.Name} waits on")).Distinct().ToList();
        string heal = recovery is { Count: > 0 } ? $" While any crisis is active, {string.Join(" and ", recovery.Select(r => Word(r.Counter)))} do not drift back toward rest as they otherwise do ({string.Join(", ", recovery.Select(r => r.PerTurn + " a year"))})." : "";
        lines.Add(new Line("What it does each year", "Nothing more of itself: it takes nothing each year, and its severity does not grow with time." + heal
            + (feeds.Count > 0 ? " What it left behind matters: " + string.Join("; ", feeds) + "." : "")
            + (def.Repeatable ? $" If it breaks out again while it is active, its severity rises by {def.Severity}." : "")));
        lines.Add(new Line("Severity", $"How much relief it takes to end it. It begins at {def.Severity}{(active != null ? $" and stands at {active.Severity}" : "")}."));
        lines.Add(new Line("How it ends of itself", "When what brought it on no longer holds, it loses 1 severity a year and ends at nought. "
            + (active == null ? "" : holds ? "Today its cause still holds, so it is not winding down." : "Today its cause has passed: it is winding down.")
            + (def.OnResolve.Count > 0 ? $" When it ends: {Does(def.OnResolve)}." : "")));
        foreach (var d in def.Dampers)
        {
            bool open = d.Availability.All(c => c.Evaluate(ctx));
            string cost = d.Cost.Count > 0 ? string.Join(", ", d.Cost.Select(c => $"{c.Value} {c.Key}")) : "no action";
            lines.Add(new Line("Answer: " + d.Name, $"Takes {d.Relief} from its severity{(active != null && active.Severity - d.Relief <= 0 ? ", which ends it" : "")}. It costs {cost}, and does this: {Does(d.Effects)}."
                + (open ? "" : " It is closed to you now: " + string.Join("; ", d.Availability.Where(c => !c.Evaluate(ctx)).Select(c => "it needs " + Gate(w, c, ctx))) + ".")));
        }
        return lines;
    }
}
