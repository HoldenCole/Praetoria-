namespace Praetoria.Core.State;

/// <summary>A state of war between two houses (GDD §6 "the few real wars you fight"; §13 Military path).
/// Unordered pair, stored once. While it stands, their fleets fight where they meet and an unopposed fleet in
/// the other's space conquers its holdings.</summary>
public sealed class War
{
    public string HouseA { get; set; } = "";
    public string HouseB { get; set; } = "";
    public int Since { get; set; }
    public bool Involves(string houseId) => HouseA == houseId || HouseB == houseId;
    public bool Between(string a, string b) => (HouseA == a && HouseB == b) || (HouseA == b && HouseB == a);
    public string Other(string houseId) => HouseA == houseId ? HouseB : HouseA;
}

/// <summary>What happened in one battle — logged, and surfaced to the UI/chronicle.</summary>
public sealed record BattleReport(string SystemId, string Attacker, string Defender, string Winner,
    int AttackerLosses, int DefenderLosses, double AttackerPower, double DefenderPower, int Turn);
