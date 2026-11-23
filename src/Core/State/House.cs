namespace Praetoria.Core.State;

/// <summary>
/// A dynasty (BuildSpec §3). The save-persistent entity (GDD §5) — characters die, the
/// House endures until no name-bearer remains (GDD §14 dynasty death). AccentColor drives
/// the reactive theme later (GDD §19); SphereInfluence feeds the power-balance system
/// (GDD §7). For Milestone 1 only identity + membership are exercised.
/// </summary>
public sealed class House
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>Hex accent (e.g. "#7A1F2B") — the single themeable house colour (GDD §19).</summary>
    public string AccentColor { get; set; } = "#888888";

    public List<string> Members { get; set; } = new();

    /// <summary>The breakaway state this house belongs to (docs/BREAKAWAY.md), or "" while it is of the Reach.</summary>
    public string PolityId { get; set; } = "";

    /// <summary>Great-Crisis bloc (GDD §16): "hegemon", "alliance", or "" outside the war of the Reach.</summary>
    public string Bloc { get; set; } = "";

    /// <summary>Standing tax posture (id into the catalog's taxPolicies; "normal" by default) — GDD §17.</summary>
    public string TaxPolicy { get; set; } = "normal";

    /// <summary>Name culture for children born into the house (key into /content/names).</summary>
    public string Culture { get; set; } = "reach";

    /// <summary>What the people of the house look like (content/ancestries; <see cref="Praetoria.Core.Systems.Lineage"/>). Empty for a
    /// house that never chose: content's naming of it, or the dice of its id, decide.</summary>
    public string Ancestry { get; set; } = "";

    /// <summary>Who inherits the seat (GDD §14): "primogeniture" (eldest blood child, then eldest member),
    /// "ultimogeniture" (youngest blood child), "seniority" (eldest member regardless of blood), or
    /// "merit" (the member with the highest skills). Data, so a scenario or an event can set it.</summary>
    public string SuccessionLaw { get; set; } = "primogeniture";

    /// <summary>The dynasty's title rung (GDD §13 — persists across character deaths). Id into the
    /// title ladder catalog. Default landless/commoner.</summary>
    public string Title { get; set; } = "landless";

    /// <summary>The dynasty's legitimacy/standing (GDD §13). Birth sets the baseline; holding a title
    /// above its legitimacy requirement is the soft-lock — possible, but it breeds instability.</summary>
    public int Legitimacy { get => _legitimacy; set => _legitimacy = Math.Clamp(value, 0, 100); }   // held to 0..100 here, so no system that adds to it can run it past the top
    private int _legitimacy;

    /// <summary>What that number is made of (docs/TITLES.md, "What legitimacy is made of"): where it began, what has moved it
    /// since and why. Written by <see cref="Systems.LegitimacyBook"/>.</summary>
    public LegitimacyRecord LegitimacyRecord { get; set; } = new();

    /// <summary>Title ids this house holds a claim to (GDD §13 Intrigue path — marriage/inheritance).
    /// A claim is the key the Intrigue path needs to convert influence into a title.</summary>
    public HashSet<string> Claims { get; set; } = new();

    /// <summary>The house's banked resources (GDD §17 — the House treasury of the §8 three-treasury
    /// model; the Imperial/personal split arrives with offices later). Holdings feed it each turn;
    /// builds, bribes and upkeep draw it down. Distinct from per-turn action pools (§9).</summary>
    public Resources Treasury { get; set; } = new();

    /// <summary>Credits still owed on a loan (GDD §17 "the one you run out of"). Paid down in instalments each turn;
    /// zero when clear. A house in debt cannot borrow again.</summary>
    public int Debt { get; set; }

    /// <summary>The house whose bank holds the loan, or "" for the caravaneers' counting-houses (no one in particular).</summary>
    public string LenderId { get; set; } = "";
    /// <summary>What is paid on the debt each year, for a loan of a chosen amount (0: the standing loan's instalment, as in an older game).</summary>
    public int LoanInstalment { get; set; }

    /// <summary>The house's wealth as last reckoned by the economy (treasury, land, ventures, less debt) — cached each turn
    /// so content, AI and the UI read one number. See <see cref="Systems.Wealth"/>.</summary>
    public int Wealth { get; set; }

    /// <summary>Per-sphere influence (GDD §7). Empty until Milestone 5; present so saves are forward-compatible.</summary>
    public Dictionary<string, int> SphereInfluence { get; set; } = new();

    // ---- the three orders of the Republic (docs/REPUBLIC.md stage 3): the Senate, Arms, Commerce
    /// <summary>Standing in each order, 0..100, reckoned each year from what the house actually holds and has done.</summary>
    public Dictionary<string, int> Orders { get; set; } = new();
    /// <summary>The order the house is strongest in: what the family is known for. Slow to change.</summary>
    public string Calling { get; set; } = "";
    /// <summary>"old" for a family that was great when the world began, "new" for one that was not.</summary>
    public string Origin { get; set; } = "";
    /// <summary>The house whose client this one is, or "".</summary>
    public string PatronId { get; set; } = "";
    /// <summary>The committee of the Senate the head has chosen to sit on (docs/LADDER.md, "Committees"); empty for the one that
    /// fits the head's own service.</summary>
    public string Committee { get; set; } = "";
    /// <summary>Enrolled in the Senate by the census: a seat bought with the purse, not held by land or the bench.</summary>
    public bool Enrolled { get; set; }
    /// <summary>Victories that are still talked about: battles won, triumphs. Fades slowly.</summary>
    public int Glory { get; set; }
    /// <summary>Strategic goods of which the house brings up a quarter or more of the Reach's production.</summary>
    public List<string> Corners { get; set; } = new();
    /// <summary>Someone the head has named to succeed them, ahead of whoever the succession law would give (docs/AMBITIONS.md).</summary>
    public string NamedHeirId { get; set; } = "";

    /// <summary>Who leads a house other than the player's (docs/HOUSEHOLD.md, "Who leads another house"): its eldest, kept until
    /// death or until they are made to stand aside. Empty until the first year's turn; <see cref="Systems.HeadSystem"/> writes it.</summary>
    public string HeadId { get; set; } = "";
    /// <summary>What the head has said its creditors are to take first if it comes to that: a venture's id or a thing's
    /// (docs/ECONOMY_MARKET.md, section 15). "" leaves it to them.</summary>
    public string PledgedId { get; set; } = "";
    /// <summary>What the house gives its grown members a year (docs/WEALTH.md): mean, customary or generous.</summary>
    public string Allowance { get; set; } = "customary";
    /// <summary>What happens to the treasury when the head dies: "entail" (the heir takes all) or "portions" (each other grown
    /// child of the head takes a tenth).</summary>
    public string Inheritance { get; set; } = "entail";
    /// <summary>The house's daughters' dowries are paid from the head's own purse first, and only the rest from the treasury.</summary>
    public bool DowryFromPurse { get; set; }
    /// <summary>The house's seat (docs/WEALTH.md, "Things worth a rich house's money"): 0 for none, 1 a manor, 2 a great house, 3 a palace.</summary>
    public int SeatGrade { get; set; }
    /// <summary>The world of the house's own that the seat stands on, or "".</summary>
    public string SeatHoldingId { get; set; } = "";
    /// <summary>The year the seat was last built or enlarged.</summary>
    public int SeatSince { get; set; }
    /// <summary>Standing duties handed to members of the house (docs/HOUSEHOLD.md).</summary>
    public List<Duty> Duties { get; set; } = new();
    /// <summary>The house's own court (docs/COURT.md): the small positions of the household, which follow the house and not any rank it holds.</summary>
    public List<Retainer> Court { get; set; } = new();
    public Retainer? Position(string role) => Court.FirstOrDefault(r => r.Role == role);
    /// <summary>People asking to be taken into the house.</summary>
    public List<Petition> Petitions { get; set; } = new();
    public Duty? Duty(string role) => Duties.FirstOrDefault(d => d.Role == role);
    public int Order(string order) => Orders.TryGetValue(order, out var v) ? v : 0;
}
