namespace Praetoria.Core.State;

/// <summary>
/// A venture a house owns that is not land (GDD §17, §19 "smaller houses that control smaller things"): a trading
/// company, a mining concern, a shipping line, a bank. It sits in a system, pays its owner by its <see cref="Kind"/>'s
/// definition and its <see cref="Level"/>, and pays a charter to whoever holds the planet there. Landless houses live
/// on these; great houses stack them on their worlds. Plain data; the World is the save.
/// </summary>
public sealed class Business
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>Owning house id.</summary>
    public string OwnerId { get; set; } = "";

    /// <summary>Definition id in the business catalog (trading_company, mining_concern, …).</summary>
    public string Kind { get; set; } = "";

    /// <summary>The system it operates in.</summary>
    public string SystemId { get; set; } = "";

    /// <summary>1..the definition's max level. Yield, upkeep and charter scale with it.</summary>
    public int Level { get; set; } = 1;

    /// <summary>Turn it was founded (0 for seeded ventures).</summary>
    public int Founded { get; set; }

    /// <summary>The company it has been incorporated into (docs/ECONOMY_MARKET.md stage C), or "" while a house owns it outright.
    /// A company's venture pays the company, not the house; <see cref="OwnerId"/> is then the house that controls the company.</summary>
    public string CompanyId { get; set; } = "";

    /// <summary>The turn until which an offer of it stands (0 = never offered): a steward waits for the answer, and does not offer the same house twice.</summary>
    /// <summary>The member of the house who founded it with their own money, or a stake they asked for (docs/AMBITIONS.md).</summary>
    public string FounderId { get; set; } = "";

    public int OfferedUntil { get; set; }
}
