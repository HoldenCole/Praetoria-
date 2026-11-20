namespace Praetoria.Core.State;

/// <summary>
/// Something that happened to the player's house and that the player should be told, not left to find in the Chronicle
/// (docs/PLAYTHROUGH.md 14: births, the death of the steward, two declarations of war, tutors leaving, an attempt on the head's
/// life: "all in the Chronicle and nowhere else"). Each says where to look (<see cref="Target"/>: a window, or "window:tab").
/// </summary>
public sealed class NewsItem
{
    public int Seq { get; set; }
    public int Turn { get; set; }
    /// <summary>"warn", "good" or "info".</summary>
    public string Tone { get; set; } = "info";
    public string Text { get; set; } = "";
    public string Target { get; set; } = "";
    /// <summary>The person or house it is about, if any, so the alert can open them.</summary>
    public string SubjectId { get; set; } = "";
}
