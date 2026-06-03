using Praetoria.Core.State;

namespace Praetoria.Core.Data;

/// <summary>
/// A starting situation loaded from data (BuildSpec §2 — content-bearing setup is data-driven
/// from line one). For Milestone 1 this is the Academy Crucible cohort: the protagonist and the
/// rival-house heirs, with their seeded bonds. <see cref="WorldBuilder"/> turns it into a World.
/// </summary>
public sealed class Scenario
{
    public string Id { get; init; } = "";
    public string ProtagonistId { get; init; } = "";
    public string Era { get; init; } = "fractured_stars";
    public IReadOnlyList<House> Houses { get; init; } = Array.Empty<House>();
    public IReadOnlyList<Character> Characters { get; init; } = Array.Empty<Character>();
    public IReadOnlyList<Relationship> Relationships { get; init; } = Array.Empty<Relationship>();

    /// <summary>Starting holdings (GDD §17 — "one barony = one modest holding"). Owned by houses.</summary>
    public IReadOnlyList<Holding> Holdings { get; init; } = Array.Empty<Holding>();
}
