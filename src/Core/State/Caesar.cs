namespace Praetoria.Core.State;

/// <summary>The Senate has ordered a general to lay down his command and come home (docs/CAESAR.md). Until the deadline the
/// head of his house answers it: he comes home, or the house crosses the line.</summary>
public sealed class Recall
{
    public string CharacterId { get; set; } = "";
    public string HouseId { get; set; } = "";
    public int Since { get; set; }
    public int Deadline { get; set; }
}

/// <summary>A general has marched on the capital (docs/CAESAR.md). One at a time. The houses that fight are few; the rest
/// declare for a side or stand aside, and are remembered for it when it ends.</summary>
public sealed class CivilWar
{
    public string GeneralId { get; set; } = "";
    /// <summary>The general's house.</summary>
    public string HouseId { get; set; } = "";
    public int Since { get; set; }
    /// <summary>Houses that fight beside the general.</summary>
    public List<string> With { get; set; } = new();
    /// <summary>Houses that fight for the Senate, its sword among them.</summary>
    public List<string> Against { get; set; } = new();
    /// <summary>What each other house has said: house id → "general" or "senate". Absent: it stands aside.</summary>
    public Dictionary<string, string> Declared { get; set; } = new();
    /// <summary>The house the Senate has given its war to.</summary>
    public string SwordHouseId { get; set; } = "";
    /// <summary>Years running that the general's fleet has held the capital's orbit.</summary>
    public int Held { get; set; }
    /// <summary>The general's hulls on the day he crossed.</summary>
    public int StartHulls { get; set; }
    /// <summary>How the war stands apart from the battles: the years in which nobody met, counted to the stronger side.</summary>
    public int Drift { get; set; }
}
