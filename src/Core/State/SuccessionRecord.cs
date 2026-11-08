namespace Praetoria.Core.State;

/// <summary>The seat passing (GDD §14): who held it, who takes it, and how — kept so the client can make a moment of it rather than
/// letting the heir simply appear next turn (GAP §10 item 1).</summary>
public sealed class SuccessionRecord
{
    public int Turn { get; set; }
    public int Generation { get; set; }
    public string DeceasedId { get; set; } = "";
    public string DeceasedName { get; set; } = "";
    public int DeceasedAge { get; set; }
    /// <summary>"death", "abdication" or "adoption" (continue mode: a sworn house took up the banner).</summary>
    public string Cause { get; set; } = "death";
    public string HeirId { get; set; } = "";
    public string HeirName { get; set; } = "";
    public int HeirAge { get; set; }
    /// <summary>The career the house lost with the old head: track and rank name, e.g. "Admiral of the military track".</summary>
    public string LostCareer { get; set; } = "";
}
