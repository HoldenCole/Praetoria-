namespace Praetoria.Core.State;

/// <summary>The Senate's emergency powers, while one consul holds them (docs/EMERGENCY.md).</summary>
public sealed class EmergencyPowers
{
    public string HolderId { get; set; } = "";
    public string HouseId { get; set; } = "";
    /// <summary>What the Reach was in danger from when they were voted, in words.</summary>
    public string Cause { get; set; } = "";
    public int Since { get; set; }
    /// <summary>The year the term the Senate gave runs out.</summary>
    public int Until { get; set; }
    /// <summary>Times the Senate has extended them.</summary>
    public int Renewals { get; set; }
    /// <summary>The term is up, or the danger is past: they are to be laid down.</summary>
    public bool Due { get; set; }
    /// <summary>Years they have been held past that, without the Senate's leave.</summary>
    public int KeptYears { get; set; }
    /// <summary>The last year the holder levied on the houses.</summary>
    public int LevyTurn { get; set; }
    /// <summary>The last year the holder struck a motion from the docket.</summary>
    public int VetoTurn { get; set; }
    /// <summary>A consular election fell due while they ran, and was not held.</summary>
    public bool SkippedElection { get; set; }
    /// <summary>An election of the magistrates below the consulship fell due while they ran, and was not held.</summary>
    public bool SkippedLadder { get; set; }
}
