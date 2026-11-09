namespace Praetoria.Core.State;

/// <summary>
/// A Senate faction (GDD §6.1 "voting blocs"; GAP §12 item 8): houses that have voted together often enough to sit together. It has
/// a leader whose stance the bench follows (<see cref="Systems.FactionSystem"/> discipline), members, a name the chamber gave it, and
/// a platform — the motion kinds it has stood for and against. Membership is earned by voting and lost by breaking the whip.
/// </summary>
public sealed class Faction
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string LeaderHouseId { get; set; } = "";
    /// <summary>Member house ids, the leader among them.</summary>
    public List<string> Members { get; set; } = new();
    public int FormedTurn { get; set; }
    /// <summary>Motion kind → net times the bench stood for it (positive) or against it (negative).</summary>
    public Dictionary<string, int> Platform { get; set; } = new();
    /// <summary>Motions carried that the leader moved; failed ones subtract. The bench's record.</summary>
    public int Record { get; set; }
    /// <summary>The party's colour on the benches (Presentation.Palette.Parties), taken as it forms.</summary>
    public string Accent { get; set; } = "";
}
