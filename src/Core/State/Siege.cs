namespace Praetoria.Core.State;

/// <summary>A holding under blockade by an enemy fleet (GDD §6). Conquest lands when the siege clock reaches the
/// holding's resistance; meanwhile the blockade halves its yield. Lifted the moment the sky is contested again.</summary>
public sealed class Siege
{
    public string HoldingId { get; set; } = "";
    public string BesiegerId { get; set; } = "";
    public int Turns { get; set; }
    public int Required { get; set; }
}
