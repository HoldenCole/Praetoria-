namespace Praetoria.Core.State;

/// <summary>
/// A company on the Exchange (docs/ECONOMY_MARKET.md stage C): one or more ventures incorporated under a name, with shares, a cash
/// balance and a dividend policy. Its ventures pay it, not a house; it pays its holders by their shares. Whoever holds more than half
/// directs it. Serialised with the World.
/// </summary>
public sealed class Company
{
    /// <summary>The key under which shares not held by any house are counted: the public float.</summary>
    public const string Public = "public";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>The house that incorporated it; "" for a company chartered by the Senate.</summary>
    public string FounderId { get; set; } = "";
    /// <summary>The house that directs it: the holder of more than half, else the largest holder.</summary>
    public string ControllerId { get; set; } = "";
    /// <summary>trade, extraction, arms, shipping, finance, services: what moves its outlook.</summary>
    public string Sector { get; set; } = "trade";
    public int Shares { get; set; } = 100;
    /// <summary>House id (or <see cref="Public"/>) → shares held.</summary>
    public Dictionary<string, int> Holders { get; set; } = new();
    /// <summary>Credits the company holds: earnings kept, capital raised. Spent only on its own projects.</summary>
    public int Cash { get; set; }
    /// <summary>The share of each year's earnings paid out to holders, 0..100.</summary>
    public int PayoutPct { get; set; } = 50;
    /// <summary>Credits a share.</summary>
    public double Price { get; set; } = 1;
    public List<double> History { get; set; } = new();
    /// <summary>Net buying (positive) or selling (negative) pressure, in shares; decays each year.</summary>
    public double Flow { get; set; }
    public int LastEarnings { get; set; }
    public int LastDividend { get; set; }
    /// <summary>Why the price moved this year.</summary>
    public string Cause { get; set; } = "";
    /// <summary>Chartered by the Senate: a great company of the Reach, with a steady yearly income of its own.</summary>
    public bool Chartered { get; set; }
    /// <summary>A chartered company's own yearly income before the world moves it.</summary>
    public int CharterIncome { get; set; }
    public int Founded { get; set; }
    /// <summary>The founder's one refusal of a takeover has been spent.</summary>
    public bool GoldenShareUsed { get; set; }
    /// <summary>Shares each house has bought this turn, for the yearly cap.</summary>
    public Dictionary<string, int> BoughtThisTurn { get; set; } = new();
    public int HeldBy(string holder) => Holders.TryGetValue(holder, out var n) ? n : 0;
}

/// <summary>A war bond of the Reach (stage D): credits lent to the chair for a war abroad, repaid with interest when it ends.</summary>
public sealed class Bond
{
    public string Id { get; set; } = "";
    public string HolderId { get; set; } = "";
    public string NationId { get; set; } = "";
    public int Face { get; set; }
    /// <summary>Per cent a year, fixed when bought.</summary>
    public int YieldPct { get; set; }
    public int Bought { get; set; }
}
