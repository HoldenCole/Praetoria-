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
    /// <summary>The year the war of independence began, or 0 if there was none.</summary>
    public int WarSince { get; set; }
    /// <summary>The houses of the Reach that went to war to bring it back.</summary>
    public List<string> Foes { get; set; } = new();
    /// <summary>How willing the Reach was to fight when it broke away. It falls as the war drags on.</summary>
    public int Temper { get; set; }
    /// <summary>How it came to be: "declared", "let_go", "partition" (out of the great war), "refused" (would not kneel to a new throne).</summary>
    public string Origin { get; set; } = "declared";
}
