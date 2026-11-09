using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>Send a fleet against a nation the Reach is at war with (docs/FOREIGN_NATIONS.md): it leaves the map, sails the nation's distance,
/// fights a season of abstract battles and comes home. One treasury action; the fleet must be whole, supplied and willing.</summary>
public sealed class SendExpeditionCommand : ICommand
{
    public const int MinHulls = 2, MinSupply = 50;
    private readonly string _fleetId, _nationId; private readonly FleetRules _rules; private readonly NationCatalog _nations;
    public SendExpeditionCommand(string actorId, string fleetId, string nationId, FleetRules rules, NationCatalog nations) { ActorId = actorId; _fleetId = fleetId; _nationId = nationId; _rules = rules; _nations = nations; }
    public string ActorId { get; }
    public string Describe() => $"send {_fleetId} against {_nationId}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); if (actor?.Alive != true) return "no one to give the order";
        if (!w.Fleets.TryGetValue(_fleetId, out var f) || f.OwnerId != actor.HouseId) return "not your fleet";
        var d = _nations.Def(_nationId); var n = w.Nation(_nationId); if (d == null || n == null) return "no such nation";
        if (!n.AtWar) return $"the Reach is not at war with {d.Name}";
        if (f.IsAway) return "already beyond the Reach";
        if (f.Moving) return "under way";
        if (f.SystemId.Length == 0) return "nowhere to sail from";
        if (f.Strength < MinHulls) return $"too few hulls to sail abroad ({MinHulls} at least)";
        if (f.Supply < MinSupply) return $"short of supply for the voyage ({MinSupply} at least)";
        if (f.Loyalty < _rules.LoyaltyRefuse) return "the crews will not sail so far for this house";
        return ctx.PoolsOf(ActorId).CanAfford(new Dictionary<string, int> { [Pool.Treasury] = 1 }) ? null : "needs 1 treasury action";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!; var f = w.Fleets[_fleetId]; var d = _nations.Def(_nationId)!;
        ctx.PoolsOf(ActorId).Spend(new Dictionary<string, int> { [Pool.Treasury] = 1 });
        f.Away = _nationId; f.AwayPhase = "out"; f.AwayTurns = d.Distance; f.HomeSystemId = f.SystemId; f.SystemId = ""; f.DestinationId = ""; f.Eta = 0; f.Plunder = 0; f.ExpeditionWins = 0;
        w.WorldCounters[NationSystem.ExpeditionsCounter] = w.Counter(NationSystem.ExpeditionsCounter) + 1;
        if (actor.HouseId == w.Protagonist?.HouseId) w.WorldFlags.Add("expedition_sent");
        w.Log($"{f.Name} sails beyond the Reach against {d.Name}: {d.Distance} year{(d.Distance == 1 ? "" : "s")} out, with {f.Strength} hulls{(f.AdmiralId.Length > 0 && w.Char(f.AdmiralId) is { } c ? $" under {c.Name}" : "")}.");
    }
}

/// <summary>Call an expedition home before its season is done. Free: a signal, not a favour.</summary>
public sealed class RecallExpeditionCommand : ICommand
{
    private readonly string _fleetId; private readonly NationCatalog _nations;
    public RecallExpeditionCommand(string actorId, string fleetId, NationCatalog nations) { ActorId = actorId; _fleetId = fleetId; _nations = nations; }
    public string ActorId { get; }
    public string Describe() => $"recall {_fleetId}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); if (actor?.Alive != true) return "no one to give the order";
        if (!w.Fleets.TryGetValue(_fleetId, out var f) || f.OwnerId != actor.HouseId) return "not your fleet";
        if (!f.IsAway) return "not abroad";
        if (f.AwayPhase == "home") return "already coming home";
        return null;
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var f = w.Fleets[_fleetId]; var d = _nations.Def(f.Away);
        f.AwayPhase = "home"; f.AwayTurns = d?.Distance ?? 2;
        w.Log($"{f.Name} is recalled from {d?.Name ?? f.Away}: {f.AwayTurns} year{(f.AwayTurns == 1 ? "" : "s")} out from the Reach.");
    }
}
