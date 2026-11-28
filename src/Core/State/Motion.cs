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
    /// <summary>For a petition for a rank (docs/TITLES.md, "Rungs that are earned"): the rung asked for.</summary>
    public string TitleId { get; set; } = "";
    public int ProposedTurn { get; set; }
    /// <summary>Struck from the docket by a consul's veto before the vote (ElectionSystem).</summary>
    public bool Vetoed { get; set; }
    /// <summary>Put off a year by the Speaker (docs/LADDER.md, "The chair"). A motion can be held over once.</summary>
    public bool HeldOver { get; set; }
    /// <summary>What the presiding consul said of it from the chair: +1 for, −1 against, 0 nothing.</summary>
    public int Chair { get; set; }
    public string VetoedBy { get; set; } = "";
    /// <summary>The house at whose asking the proposer moved it (AskAbove.Move), or "". The favour that asking leaves owed is not called
    /// in at this motion's count, since the asker is for it already: it waits for the proposer's own next motion (docs/WEALTH.md).</summary>
    public string MovedFor { get; set; } = "";
    /// <summary>Declared votes: house id → +1 for, −1 against, 0 abstain. Rival houses declare at the vote; the player may declare early.</summary>
    public Dictionary<string, int> Votes { get; set; } = new();
    /// <summary>Lobbying pressure on a house: house id → signed weight added to its lean (positive toward passing).</summary>
    public Dictionary<string, int> Lobby { get; set; } = new();
    /// <summary>Votes that were paid for: house id → signed weight added to that house's own lean. A bench does not follow a leader
    /// who was bought, since it was not the bench that was paid (docs/PLAYTHROUGH.md 13).</summary>
    public Dictionary<string, int> Bought { get; set; } = new();
    /// <summary>Who paid: bought house id → the house that paid it.</summary>
    public Dictionary<string, string> BoughtBy { get; set; } = new();
    /// <summary>How the count stood when the year opened, before the chair spoke and the favours were called: what the player was shown.</summary>
    public int ForecastFor { get; set; }
    public int ForecastAgainst { get; set; }
    /// <summary>Why the count came in far from where it stood, in words, or "".</summary>
    public string Swing { get; set; } = "";
    /// <summary>Who has already spent influence lobbying whom this motion: "lobbyist→target".</summary>
    public HashSet<string> Lobbied { get; set; } = new();
    public bool Resolved { get; set; }
    public bool Passed { get; set; }
    public int ForWeight { get; set; }
    public int AgainstWeight { get; set; }
    public int ResolvedTurn { get; set; }
    public bool Open => !Resolved;
}
