namespace Praetoria.Core.State;

/// <summary>Bond classification (GDD §18 two-axis loyalty web).</summary>
public enum BondType
{
    /// <summary>No formal bond — just a disposition reading.</summary>
    None,
    /// <summary>Given, not chosen — carries inheritance/succession rights (GDD §14). Loyalty NOT guaranteed.</summary>
    Blood,
    /// <summary>Chosen, earned (forged at the Academy, in war, by oath). No inheritance, but can run fiercer than blood.</summary>
    Sworn
}

/// <summary>
/// A directed edge in the relationship graph (BuildSpec §3, §7). Directed because feeling
/// is asymmetric — A may revere B while B despises A. <see cref="Disposition"/> is the
/// two-axis loyalty reading (−100 hatred … +100 devotion); <see cref="Bond"/> records whether
/// the tie also carries blood/sworn weight. The graph must answer "connections of X" cheaply
/// (BuildSpec §7) to power role-binding and contextual surfacing.
/// </summary>
public sealed class Relationship
{
    public string FromId { get; set; } = "";
    public string ToId { get; set; } = "";

    /// <summary>−100 (hatred) … +100 (devotion).</summary>
    public int Disposition { get; set; }

    public BondType Bond { get; set; } = BondType.None;

    /// <summary>Strength of the formal bond (0..100) when <see cref="Bond"/> is Blood/Sworn.</summary>
    public int BondStrength { get; set; }
}
