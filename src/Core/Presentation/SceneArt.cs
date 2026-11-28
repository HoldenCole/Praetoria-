using Praetoria.Core.Events;

namespace Praetoria.Core.Presentation;

/// <summary>Which painting an occasion opens on (docs/ART_DIRECTION.md, "The event window"; the pictures are made by
/// tools/art/scenes.py and live in the client's img/scenes). An event may name its painting in content ("scene"); otherwise
/// it is chosen by what the event is about, read from its id and its title; the audience hall when nothing else fits, which
/// is meant for clients, gifts, favours and visitors. tests/SceneArtTests.cs keeps the hall under a sixth of the events.</summary>
public static class SceneArt
{
    /// <summary>The paintings there are (img/scenes/&lt;name&gt;.webp).</summary>
    public static readonly IReadOnlyList<string> Scenes = new[]
    {
        "audience", "battle", "bridge", "court", "dispatch", "family", "feast", "funeral", "intrigue", "market", "senate",
        "shipyard", "steppe", "triumph", "unrest", "world",
    };

    // The first rules, in their order: what an event's id or title says outright.
    private static readonly (string scene, string[] words)[] Rules =
    {
        ("funeral", new[] { "funeral", "death", "dies", "mourn", "grief", "pall", "last rites", "tomb", "buried", "burial" }),
        ("triumph", new[] { "triumph", "laurel", "ovation", "acclaim" }),
        ("feast", new[] { "wedding", "betroth", "courtship", "suitor", "feast", "match", "dowry", "bride" }),
        ("battle", new[] { "battle", "siege", "broadside", "line_holds", "line_breaks", "the line", "sack", "raid" }),
        ("court", new[] { "trial", "prosecut", "judge", "justice", "verdict", "case_", "tribunal", "accus" }),
        ("senate", new[] { "senate", "motion", "vote", "consul", "election", "censure", "faction", "party", "tribune", "bench", "emergency", "ladder", "office", "orders", "republic" }),
        ("market", new[] { "exchange", "market", "crash", "broker", "venture", "company", "share", "bond", "debt", "loan", "creditor", "dividend", "trade" }),
        ("steppe", new[] { "nation", "khan", "steppe", "envoy", "tribute", "compact", "hollow", "concord", "thessar", "beyond" }),
        ("intrigue", new[] { "spy", "plot", "intrigue", "blackmail", "secret", "accident", "assassin", "whisper", "scandal", "feud", "poison" }),
        ("unrest", new[] { "unrest", "riot", "plague", "famine", "strike", "estate", "petition", "revolt", "breakaway", "secession" }),
        ("bridge", new[] { "fleet", "mutiny", "admiral", "navy", "campaign", "supply", "command", "caesar", "recall", "legion", "military", "general" }),
        ("shipyard", new[] { "shipyard", "hull", "foundry", "refit", "reactor", "forge" }),
        ("family", new[] { "child", "heir", "ward", "tutor", "household", "family", "kin", "mentor", "education", "ambition", "son", "daughter", "life", "wealth", "will" }),
        ("world", new[] { "world", "colony", "governor", "prosperity", "harvest", "mine", "resource", "helium" }),
        ("dispatch", new[] { "courier", "dispatch", "coalition", "crisis", "news" }),
    };

    // Then the weaker words, read only when none of the first matched; the hall last of all, for visitors, clients, gifts
    // and favours.
    private static readonly (string scene, string[] words)[] More =
    {
        ("audience", new[] { "client", "patron", "gift", "favour", "favor", "visitor", "the door" }),
        ("funeral", new[] { "widow", "the dead", "butcher" }),
        ("triumph", new[] { "victory", "banner", "games" }),
        ("feast", new[] { "the hand", "mess", "invitation" }),
        ("battle", new[] { "war_", "war of", "duel", "torpedo", "plunder", "standard", "blood" }),
        ("court", new[] { "suit_", "charge", "advocate", "praetor", "appeal", "audit", "claim", "lecture", "library" }),
        ("senate", new[] { "whip", "act_", "hustings", "canvass", "veto", "elected", "committee", "aisle", "powers", "count_" }),
        ("market", new[] { "treasury", "levy", "contract", "embargo", "merchant", "accounts", "purse", "richest" }),
        ("steppe", new[] { "abroad", "frontier", "the march", "camps", "city", "cities" }),
        ("intrigue", new[] { "informer", "knife", "sabotage", "affair", "rival", "prank", "the hour" }),
        ("unrest", new[] { "dole", "commons", "restless", "squares", "gates", "exiles" }),
        ("shipyard", new[] { "dock", "tanks", "regatta", "prize" }),
        ("bridge", new[] { "crews", "captain", "marches", "drill", "watch", "press_gang", "arms" }),
        ("family", new[] { "elder", "cradle", "physician", "protege", "coming of age", "regent", "nurse", "portrait", "retainer", "home" }),
        ("world", new[] { "vein", "seam", "comet", "province", "isotope", "skimmer", "anomaly", "the long dark" }),
        ("dispatch", new[] { "letter", "word of", "sends word", "rumbling", "partition", "treaty" }),
    };

    /// <summary>The painting for an event: the one its author named, else by the words of its id and title.</summary>
    public static string For(EventDef def, string title) =>
        !string.IsNullOrEmpty(def.Scene) ? def.Scene : For(def.Id, title);

    public static string For(string eventId, string title)
    {
        string hay = (eventId + " " + title).ToLowerInvariant();
        foreach (var (scene, words) in Rules) foreach (var word in words) if (hay.Contains(word)) return scene;
        foreach (var (scene, words) in More) foreach (var word in words) if (hay.Contains(word)) return scene;
        return "audience";
    }
}
