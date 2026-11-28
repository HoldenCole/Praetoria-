using System.Text.Json;
using System.Text.RegularExpressions;
using Praetoria.Core.Events;
using Praetoria.Core.State;

namespace Praetoria.Core.Data;

/// <summary>One problem found in the content set.</summary>
public sealed record ContentIssue(string Severity, string Where, string Message)
{
    public override string ToString() => $"[{Severity}] {Where}: {Message}";
}

/// <summary>
/// Lints the loaded content (BuildSpec §7 "Content validation tool"): every event-logic id has
/// matching text, every choice has prose, no orphan text, and prose role-tokens reference roles
/// the event actually declares. Designed to run in CI so authoring/AI text mistakes fail fast,
/// long before they reach a player. Returns issues rather than throwing so a tool can list them all.
/// </summary>
public static class ContentValidator
{
    private static readonly Regex TokenRx = new(@"\{([a-zA-Z0-9_]+)\.([a-zA-Z0-9_]+)\}", RegexOptions.Compiled);

    // ---- how the words read (docs/CONTENT_GUIDE.md, "What the check refuses")
    private static readonly Regex NameRx = new(@"\{([a-zA-Z0-9_]+)\.name\}", RegexOptions.Compiled);
    private static readonly Regex EraRx = new(@"\{era:([^|}]*)\|[^}]*\}", RegexOptions.Compiled);
    private static readonly Regex StressRx = new(@"\bstress(es|ed|ful)?\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CountyRx = new(@"\bcount(y|ies)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex OfAgeRx = new(@"\b(die|dies|died|dead)\b,?( at [^,.;]+,?)? of age\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    /// <summary>The common words that America spells its own way. The game is written in British English.</summary>
    private static readonly Regex AmericanRx = new(
        @"(?<![\w-])(" +
        @"(un)?favor(s|ed|ing|ite|ites|able|ably)?|disfavor|(dis)?honor(s|ed|ing|able|ably)?|color(s|ed|ing|ful|less)?|" +
        @"center(s|ed|ing)?|defense(s|less)?|offense(s)?|labor(s|ed|ing|er|ers)?|neighbor(s|ing|hood|hoods|ly)?|harbor(s|ed|ing)?|" +
        @"rumor(s|ed)?|armor(s|ed|y)?|behavior(s|al)?|endeavor(s|ed|ing)?|valor|vigor|humor(s|ed)?|savior|splendor|clamor|rancor|" +
        @"candor|ardor|fervor|parlor|odor|vapor|maneuver(s|ed|ing)?|gray|aging|" +
        @"travel(ed|ing|er|ers)|cancel(ed|ing)|skillful(ly)?|willful(ly)?|fulfill(s|ment)?|enroll(s|ment)?|jewelry|" +
        @"[a-z]{2,}iz(e|es|ed|ing|ation|ations)|[a-z]+yz(e|es|ed|ing)" +
        @")(?![\w-])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    /// <summary>Words spelled with -ize in Britain too.</summary>
    private static readonly HashSet<string> SpelledWithZ = new(StringComparer.OrdinalIgnoreCase)
    {
        "prize", "prizes", "prized", "prizing", "seize", "seizes", "seized", "seizing", "capsize", "capsizes", "capsized", "assize", "assizes",
        "maize", "baize", "oversize", "oversized", "outsize", "outsized", "resize", "resized", "downsize", "downsized"
    };

    /// <summary>What a reader would screenshot in <paramref name="text"/>, one line each: "stress" where the game says Strain, a county
    /// in the Republic (unless <paramref name="countyAllowed"/>), "of age" given as a death, an American spelling. Read by the content
    /// check and by the test that reads the strings of the screens.</summary>
    public static IEnumerable<string> Slips(string text, bool countyAllowed = false)
    {
        if (StressRx.Match(text) is { Success: true } s) yield return $"says \"{s.Value}\": the stat is Strain, and so is the word";
        if (!countyAllowed && County(text) is { } c) yield return c;
        if (OfAgeRx.Match(text) is { Success: true } a) yield return $"gives \"{a.Value}\" as a death: say \"of old age\"";
        foreach (Match m in AmericanRx.Matches(text))
            if (!SpelledWithZ.Contains(m.Value)) yield return $"spells \"{m.Value}\" the American way: the game is written in British English";
    }

    private static string? County(string text) => CountyRx.Match(text) is { Success: true } c
        ? $"says \"{c.Value}\" where the Republic would read it: a county is the Imperium's word, so put it in the Imperium's half of an {{era:...|...}} token"
        : null;

    /// <summary>A line of event prose read the way the player reads it: one person named in full once (then {role.first} or a pronoun),
    /// and none of the <see cref="Slips"/>. A county is the Imperium's word; in an event that can fire in the Republic it must stand in
    /// the Imperium's half of an {era:...|...} token.</summary>
    private static void CheckWords(List<ContentIssue> issues, string where, string text, bool imperiumOnly)
    {
        foreach (var named in NameRx.Matches(text).GroupBy(m => m.Groups[1].Value).Where(g => g.Count() > 1))
            issues.Add(new("error", where, $"names {{{named.Key}.name}} {named.Count()} times: say the full name once, then {{{named.Key}.first}} or a pronoun such as {{{named.Key}.he}}."));
        // both halves of an {era:...} token are read for every slip but the county, which only the Republic's reading may not carry
        foreach (var slip in Slips(text, countyAllowed: true)) issues.Add(new("error", where, slip + "."));
        if (!imperiumOnly && County(EraRx.Replace(text, "$1")) is { } county) issues.Add(new("error", where, county + "."));
    }

    public static IReadOnlyList<ContentIssue> Validate(ContentDatabase db)
    {
        var issues = new List<ContentIssue>();

        // Duplicate event ids.
        var seen = new HashSet<string>();
        foreach (var e in db.Events)
            if (!seen.Add(e.Id))
                issues.Add(new("error", e.Id, "duplicate event id."));

        // Every event needs text; every choice needs choice text; tokens must resolve.
        foreach (var e in db.Events)
        {
            var roles = new HashSet<string> { Binding.Self };
            foreach (var r in e.Roles) roles.Add(r.Name);
            // an event that fires only under the Imperium may say county anywhere in its prose
            bool imperiumOnly = e.Conditions.OfType<EraCondition>().Any(c => c.Era == World.EraImperium ? c.Present : !c.Present);

            var text = db.Text(e.Id);
            if (text == null)
            {
                issues.Add(new("error", e.Id, "event has no matching text entry."));
            }
            else
            {
                CheckTokens(issues, e.Id + ".body", text.Body, roles, imperiumOnly);
                if (text.Title != null) CheckTokens(issues, e.Id + ".title", text.Title, roles, imperiumOnly);

                foreach (var choice in e.Choices)
                {
                    if (!text.Choices.TryGetValue(choice.Id, out var ctext) || string.IsNullOrWhiteSpace(ctext))
                        issues.Add(new("error", $"{e.Id}.{choice.Id}", "choice has no text."));
                    else
                        CheckTokens(issues, $"{e.Id}.{choice.Id}", ctext, roles, imperiumOnly);

                    // Tokens also appear in `log` effect text, which is rendered at runtime against the
                    // same binding — so a bad role token there would silently render unsubstituted.
                    foreach (var effect in choice.Effects)
                        if (effect is LogEffect log)
                            CheckTokens(issues, $"{e.Id}.{choice.Id}.log", log.Text, roles, imperiumOnly);
                }
            }

            if (e.Choices.Count == 0)
                issues.Add(new("warning", e.Id, "event has no choices."));
            else if (e.Choices.All(c => c.Requirements.Count > 0))
                issues.Add(new("error", e.Id,
                    "every choice is gated by `requires`; a character meeting none of them could never act on " +
                    "the event, and an unresolved briefing item is re-offered every turn. Leave one choice ungated."));
        }

        // Orphan text (text with no matching event).
        foreach (var id in db.Texts.Keys)
            if (db.Event(id) == null)
                issues.Add(new("warning", id, "text entry has no matching event."));

        return issues;
    }

    /// <summary>The rest of what a player reads in the content, outside the events: the glossary, the traits, the worlds, the laws, the
    /// nations and every other catalog under <paramref name="contentDir"/>. Each string that is prose (a sentence, or a glossary term)
    /// is read for the <see cref="Slips"/>. A comment ("//") is not read, and a field named for the Imperium (imperiumBrief,
    /// imperialName) may say county. The events and their texts are read by <see cref="Validate"/>.</summary>
    public static IReadOnlyList<ContentIssue> ValidateFiles(string contentDir)
    {
        var issues = new List<ContentIssue>();
        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        foreach (var path in Directory.EnumerateFiles(contentDir, "*.json", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
        {
            string rel = Path.GetRelativePath(contentDir, path).Replace('\\', '/');
            if (rel.StartsWith("events/", StringComparison.Ordinal) || rel.StartsWith("text/", StringComparison.Ordinal)) continue;
            using var doc = JsonDocument.Parse(File.ReadAllText(path), options);
            Read(doc.RootElement, rel, "", "");
        }
        return issues;

        void Read(JsonElement el, string file, string path, string key)
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var p in el.EnumerateObject())
                        if (!p.Name.StartsWith("//", StringComparison.Ordinal)) Read(p.Value, file, path.Length == 0 ? p.Name : path + "." + p.Name, p.Name);
                    break;
                case JsonValueKind.Array:
                    int i = 0; foreach (var v in el.EnumerateArray()) Read(v, file, $"{path}[{i++}]", key);
                    break;
                case JsonValueKind.String:
                    string s = el.GetString() ?? "";
                    if (s.IndexOf(' ') < 0 && key != "term") break;   // an id, a key or a number, not prose
                    foreach (var slip in Slips(s, countyAllowed: key.StartsWith("imperi", StringComparison.OrdinalIgnoreCase)))
                        issues.Add(new("error", $"{file} {path}", slip + "."));
                    break;
            }
        }
    }

    private static void CheckTokens(List<ContentIssue> issues, string where, string text, HashSet<string> roles, bool imperiumOnly)
    {
        foreach (Match m in TokenRx.Matches(text))
        {
            string role = m.Groups[1].Value;
            string field = m.Groups[2].Value;
            if (!roles.Contains(role))
                issues.Add(new("error", where, $"token {{{role}.{field}}} references undeclared role '{role}'."));
            else if (!TextRenderer.Fields.Contains(field))
                issues.Add(new("warning", where, $"token {{{role}.{field}}} uses unknown field '{field}'."));
        }
        CheckWords(issues, where, text, imperiumOnly);
    }
}
