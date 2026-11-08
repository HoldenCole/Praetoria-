namespace Praetoria.Core.State;

/// <summary>An office of the Reach and who holds it (GDD §8 "what the office controls"): the house, the member who sits in it, since when.</summary>
public sealed class OfficeHolding
{
    public string OfficeId { get; set; } = "";
    public string HouseId { get; set; } = "";
    public string CharacterId { get; set; } = "";
    public int SinceTurn { get; set; }
}
