namespace Praetoria.Core.State;

/// <summary>One name in the last count of the ladder's elections (docs/PLAYTHROUGH.md, Easy: "the election card names no office"):
/// which office, who stood, for which house, the votes, and whether they were chosen. The last election's are kept on the world.</summary>
public sealed class CountRecord
{
    public int Turn { get; set; }
    public string RungId { get; set; } = "";
    public string Rung { get; set; } = "";
    public string CharacterId { get; set; } = "";
    public string Name { get; set; } = "";
    public string HouseId { get; set; } = "";
    public int Votes { get; set; }
    public bool Won { get; set; }
}
