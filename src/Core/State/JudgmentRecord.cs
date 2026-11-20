namespace Praetoria.Core.State;

/// <summary>A ruling of the courts that touched the player's house (docs/PLAYTHROUGH.md 12): who ruled, for whom, against whom,
/// over what, and what was paid. The last forty are kept on the world.</summary>
public sealed class JudgmentRecord
{
    public int Turn { get; set; }
    /// <summary>The praetor's name, or "" when the houses settled it themselves.</summary>
    public string Judge { get; set; } = "";
    public string WinnerId { get; set; } = "";
    public string LoserId { get; set; } = "";
    public int Sum { get; set; }
    public string Matter { get; set; } = "";
    public string How { get; set; } = "";
    /// <summary>"suit" between two houses, or "charge" against one who sat in office.</summary>
    public string Kind { get; set; } = "suit";
}
