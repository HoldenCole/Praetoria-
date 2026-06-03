namespace Praetoria.Core.State;

/// <summary>
/// A place a house controls — a planet, station, moon or orbital (GDD §17, BuildSpec §3 entity).
/// A holding is the unit of the domain economy: its <see cref="Specialization"/> (looked up in the
/// content catalog) sets what it yields each turn, its building slots let the player invest, and its
/// <see cref="Population"/>/<see cref="Unrest"/> carry the Manpower tension (overtax/insolvency →
/// unrest → uprising crises, later milestones). Strategic map <see cref="SystemId"/> is recorded now
/// but only used once Galaxy mode lands (a later milestone). Plain data; the World is the save.
/// </summary>
public sealed class Holding
{
    public string Id { get; set; } = "";

    /// <summary>Owning house id (territory is public knowledge, GDD §19). Domain endures succession.</summary>
    public string OwnerId { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>Specialization key into the content catalog (e.g. "agri_world", "forge_world").</summary>
    public string Specialization { get; set; } = "";

    /// <summary>Galaxy-map node this holding sits in (Galaxy mode, later milestone). Empty until then.</summary>
    public string SystemId { get; set; } = "";

    /// <summary>Population (Manpower source). Grows on populated specializations; suppressed by unrest.</summary>
    public int Population { get; set; }

    /// <summary>0..100. Rises when the owning house is insolvent; decays when solvent (GDD §17).</summary>
    public int Unrest { get; set; }

    /// <summary>Building ids built into this holding's slots. Slot count comes from the specialization.</summary>
    public List<string> Buildings { get; set; } = new();
}
