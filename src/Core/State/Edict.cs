namespace Praetoria.Core.State;

/// <summary>An edict in force (docs/EDICTS.md): what was decreed, by whom and in what position, where it falls, and when it lapses.
/// What it does while it lasts is written down the day it is proclaimed, so the rules that read it need nothing but the world.</summary>
public sealed class Edict
{
    /// <summary>"edict_&lt;n&gt;", unique in the game.</summary>
    public string Id { get; set; } = "";
    /// <summary>The kind (content/politics/edicts.json).</summary>
    public string DefId { get; set; } = "";
    public string Name { get; set; } = "";
    public string IssuerHouseId { get; set; } = "";
    /// <summary>The person who issued it.</summary>
    public string IssuerId { get; set; } = "";
    /// <summary>The kind of position it was issued from: head, state, praetor, aedile or powers.</summary>
    public string Issuer { get; set; } = "";
    /// <summary>The position in words, as it was held that day: "consul", "head of House Vega".</summary>
    public string Position { get; set; } = "";
    /// <summary>Where it falls: a house id for that house's own worlds, or "" for the whole Reach.</summary>
    public string Domain { get; set; } = "";
    /// <summary>The house or good it names, or "".</summary>
    public string TargetId { get; set; } = "";
    public int Since { get; set; }
    /// <summary>The year it lapses, at that year's turn.</summary>
    public int Until { get; set; }
    public bool Outlives { get; set; }
    public bool Annullable { get; set; }
    public string Group { get; set; } = "";
    public int Again { get; set; }
    /// <summary>Its lasting effects, by the modifier keys laws use.</summary>
    public Dictionary<string, int> Effects { get; set; } = new();
    /// <summary>What each estate made of it, the day it was proclaimed: the Senate's houses weigh it by the estates loyal to them.</summary>
    public Dictionary<string, int> Opinions { get; set; } = new();
}
