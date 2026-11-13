namespace Praetoria.Core.State;

/// <summary>
/// A courtship in progress (docs/MARRIAGE.md). Outgoing: someone of the player's house is courting someone of another. Incoming:
/// another house has asked for one of the player's kin and the head has not answered. Two numbers decide it: what the courted
/// person feels (<see cref="Favour"/>) and what their family thinks of the match (<see cref="Consent"/>). Plain data, saved with the world.
/// </summary>
public sealed class Courtship
{
    public string Id { get; set; } = "";
    /// <summary>The one who asks. Of the player's house unless <see cref="Incoming"/>.</summary>
    public string SuitorId { get; set; } = "";
    /// <summary>The one asked for.</summary>
    public string TargetId { get; set; } = "";
    /// <summary>A suit from another house for one of the player's kin. The head accepts or refuses it.</summary>
    public bool Incoming { get; set; }
    public int Since { get; set; }
    /// <summary>0..100: what the courted person feels for the suitor.</summary>
    public int Favour { get; set; }
    /// <summary>0..100: how willing the courted person's family is.</summary>
    public int Consent { get; set; }
    /// <summary>Another suitor for the same hand, or empty.</summary>
    public string RivalId { get; set; } = "";
    /// <summary>0..100: how far the rival's suit has got. At <c>RivalWinsAt</c>, and ahead of yours, the rival is accepted.</summary>
    public int RivalStanding { get; set; }
    /// <summary>The turn the rival appeared. Their suit does not advance in that same year.</summary>
    public int RivalSince { get; set; } = -1;
    /// <summary>The turn something was last done for this courtship. One act a year.</summary>
    public int ActedTurn { get; set; } = -1;
    /// <summary>The turn the match was last put to the family and refused. They answer once a year.</summary>
    public int AskedTurn { get; set; } = -1;
    /// <summary>The families have agreed; one of the two is still a child. They wed when both are of age.</summary>
    public bool Betrothed { get; set; }
    /// <summary>The terms agreed at a betrothal: "none", "customary" or "rich".</summary>
    public string Dowry { get; set; } = "customary";
    /// <summary>The terms agreed at a betrothal: the house whose name the children carry, or empty for custom.</summary>
    public string ChildrenTo { get; set; } = "";
}

/// <summary>
/// A marriage as something that goes on (docs/MARRIAGE.md): how it was made, what was agreed, and how warm it is now. The bond
/// itself is still the pair of Marriage edges in the relationship graph; this record is made for every such pair, however the
/// marriage came about.
/// </summary>
public sealed class Marriage
{
    public string A { get; set; } = "";
    public string B { get; set; } = "";
    public int Since { get; set; }
    /// <summary>0..100. Warm marriages bear more children and ease their partners; cold ones do the opposite; at the bottom the couple live apart.</summary>
    public int Warmth { get; set; } = 50;
    /// <summary>"love", "advantage" (courted, but the family's choice more than the person's) or "arranged".</summary>
    public string Kind { get; set; } = "arranged";
    /// <summary>The house whose name the children carry by the contract, or empty for custom.</summary>
    public string ChildrenTo { get; set; } = "";
    /// <summary>Credits that passed with the bride, and the house that paid them.</summary>
    public int Dowry { get; set; }
    public string DowryFrom { get; set; } = "";
    /// <summary>They live apart. No children come of it until they are reconciled.</summary>
    public bool Estranged { get; set; }
    /// <summary>The turn the marriage was last tended by the player.</summary>
    public int TendedTurn { get; set; } = -1;
    public bool Has(string id) => A == id || B == id;
    public string Other(string id) => A == id ? B : A;
}
