namespace Praetoria.Core.State;

/// <summary>
/// One battle as it is remembered: where, who, how many on each side before it, what each lost, how the two sides weighed, and
/// what told (docs/PLAYTHROUGH.md 10: "a battle is one line in a log: no place, no enemy, no numbers"). Kept on the world, newest
/// last, so the report can be read the year after and later.
/// </summary>
public sealed class BattleRecord
{
    public int Turn { get; set; }
    public string SystemId { get; set; } = "";
    public string Place { get; set; } = "";
    public string AttackerId { get; set; } = "";
    public string DefenderId { get; set; } = "";
    public string WinnerId { get; set; } = "";
    public int AttackerHulls { get; set; }
    public int DefenderHulls { get; set; }
    public int AttackerLost { get; set; }
    public int DefenderLost { get; set; }
    public double AttackerPower { get; set; }
    public double DefenderPower { get; set; }
    /// <summary>What told in it, in words: the ground, the stances, who led, how ready the crews were.</summary>
    public List<string> Notes { get; set; } = new();
    /// <summary>The attacker's chance of winning before a shot was fired, in a hundred.</summary>
    public int AttackerChance() => AttackerPower + DefenderPower <= 0 ? 50 : (int)Math.Round(100 * AttackerPower / (AttackerPower + DefenderPower));
    public bool Involves(string houseId) => AttackerId == houseId || DefenderId == houseId;
}
