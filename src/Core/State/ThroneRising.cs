namespace Praetoria.Core.State;

/// <summary>A pretender's war for the throne, while it lasts (docs/THRONE.md, stage 1).</summary>
public sealed class ThroneRising
{
    /// <summary>The house that has raised its standard.</summary>
    public string HouseId { get; set; } = "";
    /// <summary>The house that sat the throne when it was raised.</summary>
    public string EmperorId { get; set; } = "";
    public int Since { get; set; }
    /// <summary>What the throne's hold was that year.</summary>
    public int HoldThen { get; set; }
    /// <summary>Houses that went over to the pretender.</summary>
    public List<string> With { get; set; } = new();
    /// <summary>Houses that stood by the throne.</summary>
    public List<string> Against { get; set; } = new();
    /// <summary>The wars this rising began, each as "a|b", ended when it ends.</summary>
    public List<string> Wars { get; set; } = new();
}
