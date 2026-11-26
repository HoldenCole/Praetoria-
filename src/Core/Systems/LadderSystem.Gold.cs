using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>What the courts and the Reach's trust need to hear of foreign gold (docs/BEYOND.md, "Foreign gold in the Senate"): gold shown
/// to the chamber mends the Reach's trust as a thief caught does. The rule itself is <see cref="ForeignGoldSystem"/>.</summary>
public static partial class LadderSystem
{
    /// <summary>Gold sent from beyond the Reach is told in the Senate: trust moves as for anything notable done in the open.</summary>
    public static void GoldShown(World w, int by) => MoveTrust(w, by);
}
