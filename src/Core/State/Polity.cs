namespace Praetoria.Core.State;

/// <summary>A state that has broken away from the Reach (docs/BREAKAWAY.md): a region, the houses in it that went, a form of
/// government and a ruling house. Until it is recognised it is fighting for its life.</summary>
public sealed class Polity
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>"republic", "dictatorship" or "kingdom".</summary>
    public string Form { get; set; } = "republic";
    public string HeadHouseId { get; set; } = "";
    public List<string> Regions { get; set; } = new();
    public List<string> Members { get; set; } = new();
    public int Since { get; set; }
    /// <summary>The year the Reach accepted it, or 0 while it is still at war for its independence.</summary>
    public int Recognised { get; set; }
    /// <summary>The treaty that recognised it holds until this year: until then no house of the Reach and no house of the state may
    /// make war on the other.</summary>
    public int TreatyUntil { get; set; }
    /// <summary>How many times the Reach has come back to retake it.</summary>
    public int Reconquests { get; set; }
    /// <summary>The year the war of independence began, or 0 if there was none.</summary>
    public int WarSince { get; set; }
    /// <summary>The houses of the Reach that went to war to bring it back.</summary>
    public List<string> Foes { get; set; } = new();
    /// <summary>How willing the Reach was to fight when it broke away. It falls as the war drags on.</summary>
    public int Temper { get; set; }
    /// <summary>It has something the Reach cannot do without (docs/BREAKAWAY.md, standing alone): the Reach's will runs out faster.</summary>
    public bool Leverage { get; set; }
    /// <summary>It brings up no fuel of its own: while it is at war for its independence the Reach's exchanges are closed to it.</summary>
    public bool Starved { get; set; }
    /// <summary>How it came to be: "declared", "let_go", "partition" (out of the great war), "refused" (would not kneel to a new throne).</summary>
    /// <summary>Houses that were promised a place in the state before it was founded, and are owed for it (docs/BREAKAWAY.md).</summary>
    public List<string> Promised { get; set; } = new();
    public string Origin { get; set; } = "declared";
}
