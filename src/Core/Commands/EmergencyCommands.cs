using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>What the holder of the Senate's emergency powers does with them outside the chamber (docs/EMERGENCY.md): levy on the houses
/// without a vote, or lay the powers down. Asking for more time, or for the powers for life, is a motion like any other.</summary>
public sealed class EmergencyCommand : ICommand
{
    public const string Levy = "levy", LayDown = "lay_down";
    private readonly string _what;
    public EmergencyCommand(string actorId, string what) { ActorId = actorId; _what = what; }
    public string ActorId { get; }
    public string Describe() => _what == Levy ? "levy on the houses by the emergency powers" : "lay down the emergency powers";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var me = w.Char(ActorId);
        return _what == Levy ? EmergencySystem.LevyRefusal(w, me) : _what == LayDown ? EmergencySystem.LayDownRefusal(w, me) : "no such thing";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { if (_what == Levy) EmergencySystem.Levy(ctx.World); else EmergencySystem.LayDown(ctx.World); }
}
