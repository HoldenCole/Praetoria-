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
    /// <summary>A vote bought and paid for (a_vote_for_a_vote's price, the whip's purse, a bought vote called in: VoteMarks.Sell): judged
    /// at the count as any word is, but kept it leaves no favour owed, since the vote was what the coin bought.</summary>
    public bool Paid { get; set; }
    /// <summary>A word given for the other house's vote, in a bargain (a_vote_for_a_vote/give_your_word: VoteMarks.Bargains): broken at
    /// the count, the vote that house owed the player's for it goes with it (DealingsSystem.MotionCounted).</summary>
    public bool Bargain { get; set; }
}

/// <summary>One thing that passed between two houses and is remembered: a gift, a favour kept or broken, a pledge kept or
/// broken, a threat, a loan made, forgiven, pressed or defaulted on; a lot of a good bought, a standing trade deal struck,
/// kept, ended early or failed.</summary>
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
    /// <summary>The good, where goods changed hands (docs/WEALTH.md, "Trade between houses"); "" otherwise.</summary>
    public string Good { get; set; } = "";
    /// <summary>How many units of it.</summary>
    public int Units { get; set; }
}

/// <summary>A standing trade deal between two houses (docs/WEALTH.md, "Trade between houses"): so many units of a good
/// every year from one to the other, at a price a unit fixed the day it was struck, for a term of years. The goods and the
/// credits change hands when the year's books are done.</summary>
public sealed class TradeDeal
{
    public string Id { get; set; } = "";
    /// <summary>The order the deals were struck in: the earlier is served first where a store or a treasury will not cover two.</summary>
    public int Seq { get; set; }
    public string SellerId { get; set; } = "";
    public string BuyerId { get; set; } = "";
    public string Good { get; set; } = "";
    /// <summary>Units a year.</summary>
    public int Units { get; set; }
    /// <summary>The price of one unit, in single credits (not the books' hundreds), fixed for the term.</summary>
    public int UnitCredits { get; set; }
    public int Since { get; set; }
    /// <summary>The year of the last delivery.</summary>
    public int Until { get; set; }
    /// <summary>Years running in which less than the whole was delivered, or it was not paid for.</summary>
    public int Failed { get; set; }
    /// <summary>The house that failed in the last such year.</summary>
    public string FailedById { get; set; } = "";
    /// <summary>Units delivered so far, and units it has fallen short by.</summary>
    public int Delivered { get; set; }
    public int Short { get; set; }
}
