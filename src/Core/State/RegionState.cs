namespace Praetoria.Core.State;

/// <summary>
/// The mood of a region (GDD §19 rings). <see cref="Allegiance"/> is how far the region holds to the capital's order
/// (0..100); low allegiance on restless worlds breeds rebellion and, in time, free holds and free marches.
/// <see cref="Lawlessness"/> (0..100) is raiders on the lanes: it taxes every ledger in the region and feeds rebellion.
/// Parked hulls push it down; empty worlds and revolts push it up. Serialised with the World.
/// </summary>
public sealed class RegionState
{
    public int Allegiance { get; set; } = 50;
    public int Lawlessness { get; set; } = 20;
}
