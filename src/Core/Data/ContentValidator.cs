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
    private static readonly HashSet<string> KnownFields = new() { "name", "house", "rank", "rankName", "title" };

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

                    // Tokens also appear in `log` effect text, which is rendered at runtime against the
                    // same binding — so a bad role token there would silently render unsubstituted.
                    foreach (var effect in choice.Effects)
                        if (effect is LogEffect log)
                            CheckTokens(issues, $"{e.Id}.{choice.Id}.log", log.Text, roles);
                }

                // the other versions of the words (docs/EVENTS.md, "Versions"): each has a body, names only the event's own choices, and its
                // tokens resolve like the rest
                var choiceIds = e.Choices.Select(c => c.Id).ToHashSet();
                for (int v = 0; v < text.Versions.Count; v++)
                {
                    var ver = text.Versions[v]; string at = $"{e.Id}.version{v + 2}";
                    if (string.IsNullOrWhiteSpace(ver.Body)) issues.Add(new("error", at, "a version has no body."));
                    CheckTokens(issues, at + ".body", ver.Body, roles);
                    if (ver.Title != null) CheckTokens(issues, at + ".title", ver.Title, roles);
                    foreach (var (cid, ctext) in ver.Choices)
                    {
                        if (!choiceIds.Contains(cid)) issues.Add(new("error", $"{at}.{cid}", "a version names a choice the event does not have."));
                        CheckTokens(issues, $"{at}.{cid}", ctext, roles);
                    }
                }
                if (text.Versions.Count > 0 && !e.Repeatable)
                    issues.Add(new("warning", e.Id, "versions of the words on an event that comes once: only the first is ever read."));
            }

            if (e.Choices.Count == 0)
                issues.Add(new("warning", e.Id, "event has no choices."));
            else if (e.Choices.All(c => c.Requirements.Count > 0))
                issues.Add(new("error", e.Id,
                    "every choice is gated by `requires`; a character meeting none of them could never act on " +
                    "the event, and an unresolved briefing item is re-offered every turn. Leave one choice ungated."));
            // never fewer than two answers open (docs/EVENTS.md, "Two answers open"): the engine draws a card only when two of its answers
            // are open, and a card with a single answer is no choice at all
            if (e.Choices.Count == 1)
                issues.Add(new("error", e.Id, "the event has one answer, so it can only ever be shown with one answer open. Give it a second."));
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
