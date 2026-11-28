using System.Text.Json;
using Praetoria.Core.State;

namespace Praetoria.Core.Data;

/// <summary>A step of the first years (docs/ACADEMY.md, "The first years"): what to do, in a few words and a sentence; what ticks it
/// (<see cref="Done"/>: "report", "did:vote", "seen:house:ledger", "did:buy|did:venture", "seen:map"); where "Show me" goes (<see cref="Show"/>:
/// a window id, with its tab after a colon, or "fleet" for the house's first fleet on the map); what it lights (<see cref="Ring"/>, the
/// names the client knows: "rail:laws", "tab:ledger", "report", "vote", "build", "goods", "order", "fleet", "holdings"); the term of the
/// Academy it carries onto the real screen (0 for none); and what the house must have for the step to be on its list ("world", "fleet",
/// "!fleet").</summary>
public sealed record FirstStep(string Id, string Title, string Text, string Done, string Show, IReadOnlyList<string> Ring, int Term = 0, string Needs = "");

/// <summary>A lecture of the Academy folded to two or three sentences (docs/ACADEMY.md, "The tutorial"), with the Imperium's own where it reads
/// otherwise.</summary>
public sealed record LectureSummary(string Republic, string Imperium)
{
    public string For(bool imperium) => imperium && Imperium.Length > 0 ? Imperium : Republic;
}

/// <summary>The first years and the folded lectures (content/academy/first_years.json): each start's steps, what a step that cannot be done yet
/// says, what each lecture folds to, and the tips of the Academy's report cards.</summary>
public sealed class FirstYearsCatalog
{
    /// <summary>How long the list stays: until this year of the saga has ended.</summary>
    public int Years { get; init; } = 3;
    /// <summary>How long a first game shows the rail's labels by themselves (unless the player has chosen in Options).</summary>
    public int LabelYears { get; init; } = 3;
    public IReadOnlyDictionary<string, IReadOnlyList<FirstStep>> Starts { get; init; } = new Dictionary<string, IReadOnlyList<FirstStep>>();
    /// <summary>What a step says while it cannot be done yet, by the act it waits on ("vote": nothing is before the Senate).</summary>
    public IReadOnlyDictionary<string, string> NotYet { get; init; } = new Dictionary<string, string>();
    /// <summary>What the four terms carried onto the real screen are about, term by term ("the Office and you").</summary>
    public IReadOnlyList<string> Terms { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, LectureSummary> Lectures { get; init; } = new Dictionary<string, LectureSummary>();
    /// <summary>The report cards' tips by what the term shows (docs/ACADEMY.md, "The report card"); {subject}, {marks}, {low}, {high} are filled in.</summary>
    public IReadOnlyDictionary<string, string> Tips { get; init; } = new Dictionary<string, string>();

    public static FirstYearsCatalog Empty { get; } = new();
    public bool IsEmpty => Starts.Count == 0;

    /// <summary>The list a start is written with. A start with none of its own takes the Academy's (by register) when the saga opens at the
    /// Academy, and the list for a character of the player's own making otherwise.</summary>
    public IReadOnlyList<FirstStep> ListFor(string start, World w)
    {
        if (Starts.TryGetValue(start, out var own)) return own;
        if (w.InAcademy || w.Academy != null) return Starts.TryGetValue(w.IsImperium ? "the_institute" : "the_academy", out var a) ? a : Array.Empty<FirstStep>();
        return Starts.TryGetValue("custom", out var c) ? c : Array.Empty<FirstStep>();
    }
    public FirstStep? Step(string start, World w, string id) => ListFor(start, w).FirstOrDefault(s => s.Id == id);
    public string Term(int term) => term >= 1 && term <= Terms.Count ? Terms[term - 1] : "";
    public string Tip(string key) => Tips.TryGetValue(key, out var t) ? t : "";

    public static FirstYearsCatalog LoadFromDirectory(string contentRoot)
    {
        var file = Path.Combine(contentRoot, "academy", "first_years.json"); if (!File.Exists(file)) return Empty;
        using var doc = JsonDocument.Parse(File.ReadAllText(file), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var root = doc.RootElement;
        static IReadOnlyDictionary<string, string> Texts(JsonElement root, string name) =>
            root.TryGetProperty(name, out var o) && o.ValueKind == JsonValueKind.Object
                ? o.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.String && !p.Name.StartsWith("//", StringComparison.Ordinal)).ToDictionary(p => p.Name, p => p.Value.GetString()!)
                : new Dictionary<string, string>();
        var starts = new Dictionary<string, IReadOnlyList<FirstStep>>();
        if (root.TryGetProperty("starts", out var st) && st.ValueKind == JsonValueKind.Object)
            foreach (var start in st.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Array))
                starts[start.Name] = start.Value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).Select(el => new FirstStep(
                    ConditionParser.Str(el, "id"), ConditionParser.Str(el, "title"), ConditionParser.OptStr(el, "text", ""), ConditionParser.Str(el, "done"),
                    ConditionParser.OptStr(el, "show", ""),
                    el.TryGetProperty("ring", out var r) && r.ValueKind == JsonValueKind.Array ? r.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList() : new List<string>(),
                    ConditionParser.Int(el, "term", 0), ConditionParser.OptStr(el, "needs", ""))).ToList();
        var lectures = new Dictionary<string, LectureSummary>();
        if (root.TryGetProperty("lectures", out var le) && le.ValueKind == JsonValueKind.Object)
            foreach (var l in le.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Object))
                lectures[l.Name] = new LectureSummary(ConditionParser.OptStr(l.Value, "summary", ""), ConditionParser.OptStr(l.Value, "imperium", ""));
        return new FirstYearsCatalog
        {
            Years = ConditionParser.Int(root, "years", 3), LabelYears = ConditionParser.Int(root, "labelYears", 3),
            Starts = starts, NotYet = Texts(root, "notYet"), Lectures = lectures, Tips = Texts(root, "tips"),
            Terms = root.TryGetProperty("terms", out var t) && t.ValueKind == JsonValueKind.Array ? t.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList() : new List<string>(),
        };
    }
}
