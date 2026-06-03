namespace Praetoria.Core.State;

/// <summary>
/// One line in the World's recent-history log (BuildSpec §3, GDD §15 L1). The Director reads
/// it for pacing and events can condition on it. Also the spine of the saga epilogue (GDD §14).
/// </summary>
public sealed class HistoryEntry
{
    public int Turn { get; set; }
    public string Text { get; set; } = "";
    public string? EventId { get; set; }
}
