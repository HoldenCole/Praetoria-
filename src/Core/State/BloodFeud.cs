namespace Praetoria.Core.State;

/// <summary>Blood owed by one house to another for one death (docs/FEUD.md). A debt is for one person; two houses that have each killed
/// one of the other's hold two debts, one each way.</summary>
public sealed class BloodFeud
{
    public string Id { get; set; } = "";
    /// <summary>The house that is owed blood: the dead person's.</summary>
    public string WrongedId { get; set; } = "";
    /// <summary>The house that owes it.</summary>
    public string OwingId { get; set; } = "";
    /// <summary>The person held to blame: whoever did it or gave the order. When they die the debt passes to the head of their house.</summary>
    public string BlamedId { get; set; } = "";
    public string VictimId { get; set; } = "";
    public string VictimName { get; set; } = "";
    /// <summary>"knife", "battle" or "hand" (killed in person, outside a battle).</summary>
    public string How { get; set; } = "";
    public int Since { get; set; }
    /// <summary>The price in credits that would be asked to end it without blood, fixed by who the dead person was.</summary>
    public int Price { get; set; }
    /// <summary>Years the dead have gone unanswered.</summary>
    public int Unanswered { get; set; }
    /// <summary>Standing the wronged house has already lost for leaving it unanswered.</summary>
    public int Shame { get; set; }
    /// <summary>The owing house has refused the price, or the one blamed has refused the field: what follows is on them.</summary>
    public bool Refused { get; set; }
    /// <summary>The one to blame was called out and did not come: nobody calls them out a second time.</summary>
    public bool FieldRefused { get; set; }
    /// <summary>The owing house has already offered the price once.</summary>
    public bool Offered { get; set; }
    /// <summary>The last year something was done about it by command (one act a year).</summary>
    public int ActedTurn { get; set; }
    /// <summary>Asked of the player and waiting for an answer: "price" (they demand it), "offer" (they offer it) or "duel" (they call you out); "" otherwise.</summary>
    public string Asked { get; set; } = "";
    public string AskerId { get; set; } = "";
    public int AskedTurn { get; set; }
    /// <summary>"" while it stands; then how it ended: "blood", "duel", "price", "let_lie" or "extinct".</summary>
    public string Ended { get; set; } = "";
    public int EndedTurn { get; set; }
    public bool Open => Ended.Length == 0;
}
