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

    /// <summary>"planet" (the world itself — whoever holds it holds the system) or "station" (an orbital, a belt
    /// claim, a customs post — the smaller things minor houses live on). GDD §19.</summary>
    public string Kind { get; set; } = Planet;
    public const string Planet = "planet";
    public const string Station = "station";
    /// <summary>An asteroid-belt mining claim: the smallest holding there is, staked in a system with a belt.</summary>
    public const string Claim = "claim";

    /// <summary>0..100, the health of the local economy (GDD §17). Drifts each turn toward a target set by the governor's
    /// administration, unrest, what is built, the tithe and whether the world is blockaded; scales credit and material
    /// output (50 = par). Investment lifts it at once.</summary>
    public int Prosperity { get; set; } = 50;

    /// <summary>The character who governs this holding for its house (GDD §13 "governor of a planet is a seat of real
    /// power"). Empty = the house head governs in person.</summary>
    public string GovernorId { get; set; } = "";

    /// <summary>The culture of the people living here (defaults to the system's region).</summary>
    public string Culture { get; set; } = "";

    /// <summary>Turns this world has been in open rebellion against its owner (GDD §19 the rim): 0 = none. It yields nothing while
    /// it lasts; left long enough it breaks away as a free hold. See <see cref="Systems.RegionSystem"/>.</summary>
    public int Rebellion { get; set; }

    /// <summary>The house that held this world before it was taken or broke away — who has the grievance, and the right to buy or fight it back.</summary>
    public string FormerOwnerId { get; set; } = "";
    public bool InRebellion => Rebellion > 0;

    public bool IsPlanet => Kind == Planet;
    public bool IsClaim => Kind == Claim;
}
