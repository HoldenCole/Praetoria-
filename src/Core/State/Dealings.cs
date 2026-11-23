namespace Praetoria.Core.State;

/// <summary>A sum one house has lent another at interest (docs/WEALTH.md, "Dealings between houses"). The whole of it,
/// interest and all, is owed from the day; an instalment falls due each year and is paid out of the borrower's treasury
/// into the lender's, when the borrower has it.</summary>
public sealed class HouseLoan
{
    public string LenderId { get; set; } = "";
    public string BorrowerId { get; set; } = "";
    /// <summary>What was handed over.</summary>
    public int Lent { get; set; }
    public int RatePct { get; set; }
    /// <summary>What is still owed.</summary>
    public int Owed { get; set; }
    /// <summary>What falls due each year.</summary>
    public int Instalment { get; set; }
    public int Since { get; set; }
    /// <summary>Years in which nothing, or less than the instalment, was paid, since the last year it was paid in full.</summary>
    public int Missed { get; set; }
    /// <summary>The lender has said it wants its money: the borrower pays what it has each year, and after
    /// some years of that the loan is a default.</summary>
    public bool Pressed { get; set; }
}

/// <summary>A house's word, given to another, on how it will vote on a motion now before the Senate.</summary>
public sealed class VotePledge
{
    public string ByHouseId { get; set; } = "";
    public string ToHouseId { get; set; } = "";
    public string MotionId { get; set; } = "";
    /// <summary>+1 for the motion, -1 against it.</summary>
    public int Side { get; set; }
    public int Turn { get; set; }
}

/// <summary>One thing that passed between two houses and is remembered: a gift, a favour kept or broken, a pledge kept or
/// broken, a threat, a loan made, forgiven, pressed or defaulted on.</summary>
public sealed class DealingRecord
{
    public int Turn { get; set; }
    public string Kind { get; set; } = "";
    /// <summary>The house that did it.</summary>
    public string FromHouseId { get; set; } = "";
    /// <summary>The house it was done to.</summary>
    public string ToHouseId { get; set; } = "";
    /// <summary>The sum, where there was one (in the books' hundreds).</summary>
    public int Amount { get; set; }
}
