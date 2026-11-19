namespace Praetoria.Web.Services;

/// <summary>Which painting an occasion opens on (docs/ART_DIRECTION.md, "The event window"; the pictures are made by
/// tools/art/scenes.py). Chosen by what the event is about, read from its id and its title; a study with a visitor when nothing
/// else fits.</summary>
public static class SceneArt
{
    private static readonly (string scene, string[] words)[] Rules =
    {
        ("funeral", new[] { "funeral", "death", "dies", "mourn", "grief", "pall", "last rites", "tomb" }),
        ("triumph", new[] { "triumph", "laurel", "ovation", "acclaim" }),
        ("feast", new[] { "wedding", "betroth", "courtship", "suitor", "feast", "match", "dowry", "bride" }),
        ("battle", new[] { "battle", "siege", "broadside", "line_holds", "line_breaks", "the line", "sack", "raid" }),
        ("court", new[] { "trial", "prosecut", "judge", "justice", "verdict", "case_", "tribunal", "accus" }),
        ("senate", new[] { "senate", "motion", "vote", "consul", "election", "censure", "faction", "party", "tribune", "bench", "emergency", "ladder", "office", "orders", "republic" }),
        ("market", new[] { "exchange", "market", "crash", "broker", "venture", "company", "share", "bond", "debt", "loan", "creditor", "dividend", "trade" }),
        ("steppe", new[] { "nation", "khan", "steppe", "envoy", "tribute", "compact", "hollow", "concord", "thessar", "beyond" }),
        ("intrigue", new[] { "spy", "plot", "intrigue", "blackmail", "secret", "accident", "assassin", "whisper", "scandal", "feud", "poison" }),
        ("unrest", new[] { "unrest", "riot", "plague", "famine", "strike", "estate", "petition", "revolt", "breakaway", "secession" }),
        ("bridge", new[] { "fleet", "mutiny", "admiral", "navy", "campaign", "supply", "command", "caesar", "recall", "legion", "military" }),
        ("shipyard", new[] { "shipyard", "hull", "foundry", "refit" }),
        ("family", new[] { "child", "heir", "ward", "tutor", "household", "family", "kin", "mentor", "education", "ambition", "son", "daughter", "life", "wealth", "will" }),
        ("world", new[] { "world", "colony", "governor", "prosperity", "harvest", "mine", "resource", "helium" }),
        ("dispatch", new[] { "courier", "dispatch", "coalition", "crisis", "news" }),
    };

    public static string For(string eventId, string title)
    {
        string hay = (eventId + " " + title).ToLowerInvariant();
        foreach (var (scene, words) in Rules) foreach (var word in words) if (hay.Contains(word)) return scene;
        return "audience";
    }
}
