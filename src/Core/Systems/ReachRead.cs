namespace Praetoria.Core.Systems;

/// <summary>The effects of a Reach-wide law that some rule reads for the whole Reach, beyond the first nine the audit of the laws
/// listed (docs/LAWS_REVIEW.md; tests/LawsAuditTests.cs). A rule that begins to read another through
/// <see cref="PolicySystem.ReachModifier"/> adds its name here, and the test stops refusing a law that names it.</summary>
public static class ReachRead
{
    public static readonly HashSet<string> Also = new();
}
