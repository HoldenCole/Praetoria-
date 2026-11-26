using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>Set a local law on one world to one of its options (docs/POLITICS.md, "Local law"). Only the head of the house that
/// holds the world may; the option must differ from the one in force; the law must be off its cooldown on that world; the world must
/// have room for another law off the custom; and it costs the law's influence. Setting a law back to its default lifts it.</summary>
public sealed class SetLocalLawCommand : ICommand
{
    private readonly string _holdingId, _lawId, _optionId; private readonly PolicyCatalog _cat;
    public SetLocalLawCommand(string actorId, string holdingId, string lawId, string optionId, PolicyCatalog cat) { ActorId = actorId; _holdingId = holdingId; _lawId = lawId; _optionId = optionId; _cat = cat; }
    public string ActorId { get; }
    public string Describe() => $"local law on {_holdingId}: {_lawId} = {_optionId}";
    private Dictionary<string, int> Cost => new() { [Pool.Influence] = _cat.Local.Law(_lawId)?.Cost ?? 1 };
    public string? Refusal(CommandContext ctx) =>
        LocalLawSystem.Refusal(ctx.World, _cat, ctx.World.Char(ActorId), _holdingId, _lawId, _optionId) ?? ctx.PoolsOf(ActorId).Short(Cost);
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!; var house = w.House(actor.HouseId)!; var h = w.Holding(_holdingId)!;
        var law = _cat.Local.Law(_lawId)!; var option = law.Option(_optionId)!; var was = LocalLawSystem.Option(_cat, h, _lawId)!;
        ctx.PoolsOf(ActorId).Spend(Cost);
        LocalLawSystem.Set(w, _cat, h, _lawId, _optionId);
        w.Log(option.Default ? $"{house.Name} lifts {was.Short} on {h.Name}: {option.Short} again." : $"{house.Name} decrees for {h.Name}: {option.Short}.");
    }
}
