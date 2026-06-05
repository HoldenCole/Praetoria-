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

    /// <summary>The dynasty's title rung (GDD §13 — persists across character deaths). Id into the
    /// title ladder catalog. Default landless/commoner.</summary>
    public string Title { get; set; } = "landless";

    /// <summary>The dynasty's legitimacy/standing (GDD §13). Birth sets the baseline; holding a title
    /// above its legitimacy requirement is the soft-lock — possible, but it breeds instability.</summary>
    public int Legitimacy { get; set; }

    /// <summary>Title ids this house holds a claim to (GDD §13 Intrigue path — marriage/inheritance).
    /// A claim is the key the Intrigue path needs to convert influence into a title.</summary>
    public HashSet<string> Claims { get; set; } = new();

    /// <summary>The house's banked resources (GDD §17 — the House treasury of the §8 three-treasury
    /// model; the Imperial/personal split arrives with offices later). Holdings feed it each turn;
    /// builds, bribes and upkeep draw it down. Distinct from per-turn action pools (§9).</summary>
    public Resources Treasury { get; set; } = new();

    /// <summary>Per-sphere influence (GDD §7). Empty until Milestone 5; present so saves are forward-compatible.</summary>
    public Dictionary<string, int> SphereInfluence { get; set; } = new();
}
