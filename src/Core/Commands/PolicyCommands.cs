using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>Set a law to one of its options (GDD §6.1): you must hold the law's scope, the option must differ, the law must be off its
/// cooldown, and the influence must be there.</summary>
public sealed class EnactLawCommand : ICommand
{
    private readonly string _lawId, _optionId; private readonly PolicyCatalog _cat;
    public EnactLawCommand(string actorId, string lawId, string optionId, PolicyCatalog cat) { ActorId = actorId; _lawId = lawId; _optionId = optionId; _cat = cat; }
    public string ActorId { get; }
    public string Describe() => $"enact {_lawId} = {_optionId}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null;
        if (actor?.Alive != true || house == null) return "no house";
        var law = _cat.Law(_lawId); if (law == null) return "no such law";
        if (law.Option(_optionId) == null) return "no such option";
        if (!PolicySystem.HoldsScope(w, house.Id, law.Scope)) return $"only {PolicySystem.ScopeName(law.Scope)} may set this";
        if (PolicySystem.OptionId(w, _cat, house.Id, _lawId) == _optionId) return "already the law";
        if (PolicySystem.OnCooldown(w, house.Id, _lawId)) return $"changed too lately — {w.Counter($"law_cd:{house.Id}:{_lawId}") - w.Turn} turn(s) more";
        return ctx.PoolsOf(ActorId).CanAfford(new Dictionary<string, int> { [Pool.Influence] = law.Cost }) ? null : $"needs {law.Cost} influence";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!; var law = _cat.Law(_lawId)!;
        ctx.PoolsOf(ActorId).Spend(new Dictionary<string, int> { [Pool.Influence] = law.Cost });
        PolicySystem.Enact(w, _cat, actor.HouseId, _lawId, _optionId);
        w.Log($"{w.House(actor.HouseId)?.Name} sets {law.Name.ToLowerInvariant()}: {law.Option(_optionId)!.Name.ToLowerInvariant()}.");
    }
}
