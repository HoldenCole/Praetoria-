namespace Praetoria.Core.State;

/// <summary>
/// A part of what one person thinks of another that came down from a head of house who has given way to an heir (docs/LIFE.md,
/// "Friends and enemies handed down"). It is kept so that a warmth or a coldness that was handed down can fade over the years, and
/// so that a person's page can say why they think as they do of the heir. Either the heir is the one thought of (those who thought
/// well or ill of the old head, of the heir now) or, for the head of any house but the player's, the one who thinks (the heir takes up
/// the old head's view of others).
/// </summary>
public sealed class InheritedTie
{
    /// <summary>Who holds the feeling.</summary>
    public string FromId { get; set; } = "";
    /// <summary>Whom it is held of.</summary>
    public string ToId { get; set; } = "";
    /// <summary>The heir who took the house: <see cref="ToId"/> when the tie is of them, <see cref="FromId"/> when it is theirs.</summary>
    public string HeirId { get; set; } = "";
    /// <summary>The head before them, whose tie it was.</summary>
    public string OldHeadId { get; set; } = "";
    public string HouseId { get; set; } = "";
    /// <summary>The year it was handed down.</summary>
    public int Turn { get; set; }
    /// <summary>"friend" (a warmth that fades), "enemy" (a coldness that fades), "vengeful" (an enmity taken up at once, which does not
    /// fade), "let_go" (an enmity let go, by a just or loyal nature) or "unseen" (a friendship that did not pass to a child).</summary>
    public string Kind { get; set; } = "";
    /// <summary>The nature that decided an enmity: "Vengeful", "Just", "Loyal", or empty.</summary>
    public string Nature { get; set; } = "";
    /// <summary>What it moved the regard by when it was handed down.</summary>
    public int Amount { get; set; }
    /// <summary>What of a fading tie is still in the regard. It falls by a tenth of <see cref="Amount"/> a year.</summary>
    public int Left { get; set; }
}
