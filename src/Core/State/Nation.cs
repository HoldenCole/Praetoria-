namespace Praetoria.Core.State;

/// <summary>A foreign nation as it stands (docs/FOREIGN_NATIONS.md): its strength, its regard for the Reach, the war if there is one and its score,
/// whether it is subdued and paying tribute, when it last raided. Serialised with the World.</summary>
public sealed class Nation
{
    public string Id { get; set; } = "";
    public int Strength { get; set; }
    public int Disposition { get; set; }
    public bool AtWar { get; set; }
    public int WarSince { get; set; }
    public int ScoreReach { get; set; }
    public int ScoreNation { get; set; }
    public int LastBattleTurn { get; set; }
    /// <summary>The turn until which the nation pays tribute to the chair; 0 when free.</summary>
    public int SubduedUntil { get; set; }
    public int LastRaidTurn { get; set; }
    public int Wars { get; set; }
    public bool Subdued(int turn) => SubduedUntil > turn;
}
