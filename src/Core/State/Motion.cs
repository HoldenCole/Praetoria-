namespace Praetoria.Core.State;

/// <summary>
/// A motion before the Senate (GDD §6.1 "the Senate... voting blocs"; GAP §10 item 2). Proposed by a house, it sits on the docket for a
/// turn — long enough for every house to be lobbied — and is voted on at the end of the next turn by weight (Senate influence, title
/// rank, the chair). Resolved motions stay as history so the chronicle and the screen can show who stood where.
/// </summary>
public sealed class Motion
{
    public string Id { get; set; } = "";
    /// <summary>The motion kind (content/senate/motions.json).</summary>
    public string Kind { get; set; } = "";
    public string ProposerHouseId { get; set; } = "";
    /// <summary>A house id, a region id, or empty — by the kind's target type.</summary>
    public string TargetId { get; set; } = "";
    /// <summary>For a Commission: the office named.</summary>
    public string OfficeId { get; set; } = "";
    /// <summary>For an Act of the Senate: the law and the option moved.</summary>
    public string LawId { get; set; } = "";
    public string LawOption { get; set; } = "";
    public int ProposedTurn { get; set; }
    /// <summary>Declared votes: house id → +1 for, −1 against, 0 abstain. Rival houses declare at the vote; the player may declare early.</summary>
    public Dictionary<string, int> Votes { get; set; } = new();
    /// <summary>Lobbying pressure on a house: house id → signed weight added to its lean (positive toward passing).</summary>
    public Dictionary<string, int> Lobby { get; set; } = new();
    /// <summary>Who has already spent influence lobbying whom this motion: "lobbyist→target".</summary>
    public HashSet<string> Lobbied { get; set; } = new();
    public bool Resolved { get; set; }
    public bool Passed { get; set; }
    public int ForWeight { get; set; }
    public int AgainstWeight { get; set; }
    public int ResolvedTurn { get; set; }
    public bool Open => !Resolved;
}
