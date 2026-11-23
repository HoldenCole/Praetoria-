namespace Praetoria.Core.State;

/// <summary>Coin put behind one who stands for an office of the ladder (docs/WEALTH.md, "Things worth a rich house's money"). One
/// to a candidate and an election; it can be raised to a larger size before the count. After the count the player's house's are
/// kept as a record of what was paid for and how it went; other houses' are struck out.</summary>
public sealed class CampaignFund
{
    public string HouseId { get; set; } = "";
    /// <summary>Whose canvass it paid for. Another house's is its candidate's for that rung, whoever that turns out to be.</summary>
    public string CharacterId { get; set; } = "";
    public string RungId { get; set; } = "";
    /// <summary>The year the first of it was paid.</summary>
    public int Turn { get; set; }
    /// <summary>modest, handsome or lavish.</summary>
    public string Size { get; set; } = "";
    /// <summary>What has been paid in all, in the books' hundreds.</summary>
    public int Spent { get; set; }
    /// <summary>What it is worth at the count.</summary>
    public int Votes { get; set; }
    /// <summary>The election it was paid for has been held.</summary>
    public bool Counted { get; set; }
    public bool Won { get; set; }
}
