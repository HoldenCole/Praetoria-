using System.Text.RegularExpressions;
using Praetoria.Core.Events;

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
    private static readonly HashSet<string> KnownFields = new() { "name", "house", "rank" };

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

            var text = db.Text(e.Id);
            if (text == null)
            {
                issues.Add(new("error", e.Id, "event has no matching text entry."));
            }
            else
            {
                CheckTokens(issues, e.Id + ".body", text.Body, roles);
                if (text.Title != null) CheckTokens(issues, e.Id + ".title", text.Title, roles);

                foreach (var choice in e.Choices)
                {
                    if (!text.Choices.TryGetValue(choice.Id, out var ctext) || string.IsNullOrWhiteSpace(ctext))
                        issues.Add(new("error", $"{e.Id}.{choice.Id}", "choice has no text."));
                    else
                        CheckTokens(issues, $"{e.Id}.{choice.Id}", ctext, roles);
                }
            }

            if (e.Choices.Count == 0)
                issues.Add(new("warning", e.Id, "event has no choices."));
        }

        // Orphan text (text with no matching event).
        foreach (var id in db.Texts.Keys)
            if (db.Event(id) == null)
                issues.Add(new("warning", id, "text entry has no matching event."));

        return issues;
    }

    private static void CheckTokens(List<ContentIssue> issues, string where, string text, HashSet<string> roles)
    {
        foreach (Match m in TokenRx.Matches(text))
        {
            string role = m.Groups[1].Value;
            string field = m.Groups[2].Value;
            if (!roles.Contains(role))
                issues.Add(new("error", where, $"token {{{role}.{field}}} references undeclared role '{role}'."));
            else if (!KnownFields.Contains(field))
                issues.Add(new("warning", where, $"token {{{role}.{field}}} uses unknown field '{field}'."));
        }
    }
}
