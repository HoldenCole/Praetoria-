namespace Praetoria.Core.State;

/// <summary>
/// What one head of the player's house has had of one report (docs/EVENTS.md, "Waiting times"): the year it last came, how often it
/// has come, and which version of its words it came in. Kept in <see cref="World.Cards"/> under <see cref="Key"/>, so a save keeps it
/// and a new head, who has met none of them, can meet each card again.
/// </summary>
public sealed class CardSeen
{
    /// <summary>The year the card last came to this head.</summary>
    public int Turn { get; set; }
    /// <summary>How many times it has come to this head.</summary>
    public int Times { get; set; }
    /// <summary>The version of its words it last came in (0 is the text's own body; see <see cref="Events.EventText.Versions"/>).</summary>
    public int Version { get; set; }

    /// <summary>The key a card is kept under: the event and the head it came to.</summary>
    public static string Key(string eventId, string headId) => eventId + "|" + headId;
    /// <summary>The key of one person a once-a-person role has been bound to (<see cref="World.CardsMet"/>).</summary>
    public static string Met(string eventId, string role, string personId) => eventId + "|" + role + "|" + personId;
}
