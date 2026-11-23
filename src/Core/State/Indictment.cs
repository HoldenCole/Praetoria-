namespace Praetoria.Core.State;

/// <summary>A charge another house has brought against one of the player's house (docs/LADDER.md, "The docket"). It is not
/// heard the year it is brought: it waits a year, the house is told, and in that year the head may send someone to plead, or
/// admit it and pay the fine, or do nothing.</summary>
public sealed class Indictment
{
    public string Id { get; set; } = "";
    /// <summary>The act on the record (<see cref="OfficeDeed.Id"/>) that is charged.</summary>
    public string DeedId { get; set; } = "";
    /// <summary>The house that brings it.</summary>
    public string AccuserId { get; set; } = "";
    /// <summary>The person charged, and their house.</summary>
    public string AccusedId { get; set; } = "";
    public string HouseId { get; set; } = "";
    /// <summary>The praetor who is to hear it.</summary>
    public string JudgeId { get; set; } = "";
    /// <summary>The year it was brought: it is heard at the end of the next.</summary>
    public int Turn { get; set; }
    /// <summary>Who of the accused's house is to plead it, or "".</summary>
    public string PleaderId { get; set; } = "";
    /// <summary>"" while it waits; then "admitted", "proved", "let_go" or "lapsed".</summary>
    public string Outcome { get; set; } = "";
}
