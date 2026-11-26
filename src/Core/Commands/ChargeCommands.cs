using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>Answering a charge against the powerful (docs/LADDER.md, "A charge against the powerful"), for yourself or for one of
/// your house who leaves it to you. Before the verdict: <see cref="ChargeSystem.Trial"/> (stand trial, as any charge is heard),
/// <see cref="ChargeSystem.ExileAnswer"/> (go into exile beyond the Reach now: the charge is not heard, offices and commands lapse,
/// and the Senate must vote the exile home) or <see cref="ChargeSystem.Cross"/> (the house crosses the line with its general, who
/// is the accused). After a conviction of one who commands: <see cref="ChargeSystem.LayDown"/> or <see cref="ChargeSystem.Cross"/>.
/// The charge's id is an <see cref="Indictment"/>'s.</summary>
public sealed class AnswerChargeCommand : ICommand
{
    private readonly string _chargeId, _answer; private readonly ChargeSystem _charges;
    public AnswerChargeCommand(string actorId, string chargeId, string answer, ChargeSystem charges) { ActorId = actorId; _chargeId = chargeId; _answer = answer; _charges = charges; }
    public string ActorId { get; }
    public string Describe() => $"answer the charge {_chargeId}: {_answer}";
    public string? Refusal(CommandContext ctx) => _answer is ChargeSystem.Trial or ChargeSystem.ExileAnswer or ChargeSystem.Cross or ChargeSystem.LayDown
        ? _charges.AnswerRefusal(ctx.World, ctx.World.Char(ActorId), ChargeSystem.Pending(ctx.World, _chargeId), _answer) : "no such answer";
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) => _charges.Answer(ctx.World, ctx.World.Char(ActorId)!, ChargeSystem.Pending(ctx.World, _chargeId)!, _answer);
}

/// <summary>Let it be known that a general of another house will be charged the day the command is laid down (docs/LADDER.md, "A
/// charge against the powerful"). It needs something on the record against them and 1 clout; it is a cause to cross the line for
/// their house while it stands, and if the charge is not brought by the end of the year after the command is laid down, the house
/// loses legitimacy. One such threat at a time.</summary>
public sealed class ThreatenChargeCommand : ICommand
{
    private static readonly Dictionary<string, int> Cost = new() { [Pool.Influence] = ChargeSystem.ThreatInfluence };
    private readonly string _generalId; private readonly OfficeCatalog _offices;
    public ThreatenChargeCommand(string actorId, string generalId, OfficeCatalog offices) { ActorId = actorId; _generalId = generalId; _offices = offices; }
    public string ActorId { get; }
    public string Describe() => $"let it be known that {_generalId} will be charged on laying the command down";
    public string? Refusal(CommandContext ctx) =>
        (!LadderSystem.Active(ctx.World, _offices) ? "there are no courts of the Republic" : ChargeSystem.ThreatRefusal(ctx.World, ctx.World.Char(ActorId), ctx.World.Char(_generalId))) ?? ctx.PoolsOf(ActorId).Short(Cost);
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        ctx.PoolsOf(ActorId).Spend(Cost);
        ChargeSystem.Threaten(ctx.World, _offices, ctx.World.Char(ActorId)!, ctx.World.Char(_generalId)!);
    }
}
