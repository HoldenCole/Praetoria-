namespace Praetoria.Core.State;

/// <summary>A state of war between two houses (GDD §6 "the few real wars you fight"; §13 Military path).
/// Unordered pair, stored once. While it stands, their fleets fight where they meet and an unopposed fleet in
/// the other's space conquers its holdings.</summary>
public sealed class War
{
    public string HouseA { get; set; } = "";
    public string HouseB { get; set; } = "";
    public int Since { get; set; }
    public int ScoreA { get; set; }
    public int ScoreB { get; set; }
    /// <summary>Why the house that declared it did, as far as it can be said in a line: kept so the one it was declared on can be told
    /// (docs/PLAYTHROUGH.md, Medium: "a declaration of war on you is a report: who, why, what they want").</summary>
    public string Why { get; set; } = "";
    /// <summary>The last turn a battle was fought in this war (0 = none yet): a war with no battle for long is exhaustion, not war.</summary>
    public int LastBattleTurn { get; set; }
    /// <summary>The account of the war by year (docs/MILITARY.md, "Commanding a navy"), oldest first: one entry for each year's turn
    /// the war has stood through, and after the last of them the year now running. Kept by <see cref="Systems.WarLedger"/>.</summary>
    public List<WarYear> Years { get; set; } = new();
    /// <summary>The last year whose account is closed: what happens after it is written into the next year's.</summary>
    public int ClosedThrough { get; set; }
    /// <summary>The year the war ended (0 while it stands), and how, as a clause that follows the houses' names ("made peace").</summary>
    public int Ended { get; set; }
    public string Ending { get; set; } = "";
    /// <summary>The houses' names as they were when the war ended: a house that is no more is still named in the account.</summary>
    public string NameA { get; set; } = "";
    public string NameB { get; set; } = "";
    public int Score(string houseId) => HouseA == houseId ? ScoreA : HouseB == houseId ? ScoreB : 0;
    public void AddScore(string houseId, int delta) { if (HouseA == houseId) ScoreA += delta; else if (HouseB == houseId) ScoreB += delta; }
    /// <summary>How far ahead <paramref name="houseId"/> is (negative = behind).</summary>
    public int Lead(string houseId) => Score(houseId) - Score(Other(houseId));
    public bool Involves(string houseId) => HouseA == houseId || HouseB == houseId;
    public bool Between(string a, string b) => (HouseA == a && HouseB == b) || (HouseA == b && HouseB == a);
    public string Other(string houseId) => HouseA == houseId ? HouseB : HouseA;
}

/// <summary>One year of a war, as it is written down when the year turns: the battles and who won them, the hulls each side lost
/// and took as prizes, the worlds and stations that changed hands, the score as the year opened and as it closed, and what each
/// house's fleets in commission cost it in credits (hundreds, as the books keep them) and in helium-3.</summary>
public sealed class WarYear
{
    /// <summary>The year whose turn closed this account: its battles are dated with it.</summary>
    public int Turn { get; set; }
    public int Battles { get; set; }
    public int WonA { get; set; }
    public int WonB { get; set; }
    public int LostA { get; set; }
    public int LostB { get; set; }
    /// <summary>Hulls taken as prizes by boarding, by each side.</summary>
    public int TakenA { get; set; }
    public int TakenB { get; set; }
    public List<WarChange> Changed { get; set; } = new();
    public int OpenA { get; set; }
    public int OpenB { get; set; }
    public int CloseA { get; set; }
    public int CloseB { get; set; }
    public int CreditsA { get; set; }
    public int CreditsB { get; set; }
    public int FuelA { get; set; }
    public int FuelB { get; set; }
    /// <summary>The house bore the keep of its fleets for more than one war this year, and this is its share of it.</summary>
    public bool SharedA { get; set; }
    public bool SharedB { get; set; }
    /// <summary>People of other houses who commanded each side's fleets by service in a battle of the year (docs/MILITARY.md, "A command by service"): character ids.</summary>
    public List<string> ServedA { get; set; } = new();
    public List<string> ServedB { get; set; } = new();
    public bool Empty() => Battles == 0 && Changed.Count == 0 && TakenA + TakenB + CreditsA + CreditsB + FuelA + FuelB == 0;
}

/// <summary>A world or a station that changed hands in a war: which, from whom to whom, and how ("taken" by siege, "ceded" at the
/// peace, "freed" for the house it had been taken from). <see cref="Seat"/>: it was the last the losing house held.</summary>
public sealed class WarChange
{
    public string HoldingId { get; set; } = "";
    public string Name { get; set; } = "";
    public string FromId { get; set; } = "";
    public string ToId { get; set; } = "";
    public string How { get; set; } = "taken";
    public bool World { get; set; } = true;
    public bool Seat { get; set; }
}

/// <summary>What happened in one battle — logged, and surfaced to the UI/chronicle.</summary>
public sealed record BattleReport(string SystemId, string Attacker, string Defender, string Winner,
    int AttackerLosses, int DefenderLosses, double AttackerPower, double DefenderPower, int Turn);
