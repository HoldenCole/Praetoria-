using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>Send a fleet against a nation the Reach is at war with (docs/FOREIGN_NATIONS.md): it leaves the map, sails the nation's distance,
/// fights a season of abstract battles and comes home. One treasury action; the fleet must be whole, supplied and willing.</summary>
/// <summary>Make war on a nation on the house's own account, without the Senate's leave (docs/BEYOND.md, "Whose war"). Only this house is
/// at war with it. The Senate may vote that it stop.</summary>
public sealed class RaidNationCommand : ICommand
{
    private readonly string _nationId; private readonly NationCatalog _nations;
    public RaidNationCommand(string actorId, string nationId, NationCatalog nations) { ActorId = actorId; _nationId = nationId; _nations = nations; }
    public string ActorId { get; }
    public string Describe() => $"make war on {_nationId} without the Senate's leave";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); if (actor?.Alive != true) return "no one to give the order";
        var house = w.House(actor.HouseId); if (house == null || Economy.HeadOf(w, house)?.Id != actor.Id) return "only the head of a house makes its wars";
        return NationSystem.PrivateWarRefusal(w, _nations, house, _nationId);
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) => NationSystem.PrivateWar(ctx.World, _nations, _nationId, ctx.World.Char(ActorId)!.HouseId);
}

/// <summary>Whether the player's fleets raid a nation's worlds (sack them and go home) or take them.</summary>
public sealed class RaidPostureCommand : ICommand
{
    private readonly bool _raid;
    public RaidPostureCommand(string actorId, bool raid) { ActorId = actorId; _raid = raid; }
    public string ActorId { get; }
    public string Describe() => _raid ? "raid beyond the rim, and take nothing" : "take and hold beyond the rim";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); if (actor?.Alive != true) return "no one to give the order";
        if (actor.Id != w.ProtagonistId) return "the order is yours to give";
        return w.HasFlag(NationSystem.PlayerRaidsFlag) == _raid ? "that is the order already" : null;
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        if (_raid) ctx.World.WorldFlags.Add(NationSystem.PlayerRaidsFlag); else ctx.World.WorldFlags.Remove(NationSystem.PlayerRaidsFlag);
        ctx.World.Log(_raid ? "Your fleets beyond the rim are to sack and come home: no world of a nation's is to be held." : "Your fleets beyond the rim are to take what they can and hold it.");
    }
}

/// <summary>Make peace in a war that is the house's own, on terms the war score allows (docs/BEYOND.md, "Negotiated or won").</summary>
public sealed class NegotiateCommand : ICommand
{
    private readonly string _nationId, _terms; private readonly NationCatalog _nations;
    public NegotiateCommand(string actorId, string nationId, string terms, NationCatalog nations) { ActorId = actorId; _nationId = nationId; _terms = terms; _nations = nations; }
    public string ActorId { get; }
    public string Describe() => $"make peace with {_nationId}: {_terms}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); if (actor?.Alive != true) return "no one to treat with them";
        var house = w.House(actor.HouseId); if (house == null || Economy.HeadOf(w, house)?.Id != actor.Id) return "only the head of a house makes its peace";
        return NationSystem.NegotiateRefusal(w, _nations, house, _nationId, _terms);
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) => NationSystem.Negotiate(ctx.World, _nations, _nationId, ctx.World.Char(ActorId)!.HouseId, _terms);
}

/// <summary>What the player's house wants of a nation it has beaten decisively: to keep what it took, or to make the nation a client.</summary>
public sealed class PeacePostureCommand : ICommand
{
    private readonly bool _client;
    public PeacePostureCommand(string actorId, bool client) { ActorId = actorId; _client = client; }
    public string ActorId { get; }
    public string Describe() => _client ? "make clients of the nations you beat" : "keep what you take of the nations you beat";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); if (actor?.Alive != true) return "no one to give the order";
        if (actor.Id != w.ProtagonistId) return "the choice is yours to make";
        return w.HasFlag(NationSystem.PlayerClientsFlag) == _client ? "that is your word already" : null;
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        if (_client) ctx.World.WorldFlags.Add(NationSystem.PlayerClientsFlag); else ctx.World.WorldFlags.Remove(NationSystem.PlayerClientsFlag);
        ctx.World.Log(_client ? "Your word is given: a nation your fleets beat decisively is to be left its ruler and its worlds, and made a client of the Reach and of your house." : "Your word is given: what your fleets take of a nation, your house keeps.");
    }
}

/// <summary>The player's answer when the Senate votes peace and their fleets are in the field: come home, or fight on.</summary>
public sealed class AnswerPeaceCommand : ICommand
{
    private readonly bool _obey;
    public AnswerPeaceCommand(string actorId, bool obey) { ActorId = actorId; _obey = obey; }
    public string ActorId { get; }
    public string Describe() => _obey ? "keep the Senate's peace" : "fight on against the Senate's word";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); if (actor?.Alive != true) return "no one to give the order";
        var house = w.House(actor.HouseId); if (house == null || actor.Id != w.ProtagonistId) return "the answer is yours to give";
        bool awaited = w.Nations.Any(n => n.AtWar && n.PeaceVoted > 0 && NationSystem.Foe(w, n, house.Id)), defying = NationSystem.DefiedBy(w, house.Id) != null;
        if (_obey) return awaited || defying ? null : "the Senate has voted no peace that waits on you";
        return awaited ? null : "the Senate has voted no peace that waits on you";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; w.WorldFlags.Remove(_obey ? NationSystem.PeaceDefyFlag : NationSystem.PeaceObeyFlag); w.WorldFlags.Add(_obey ? NationSystem.PeaceObeyFlag : NationSystem.PeaceDefyFlag);
        w.Log(_obey ? "Your fleets beyond the rim are told to come home: the Senate has voted, and your house will keep its word." : "Your answer is sent to the Senate: the war is not finished, and your fleets are not coming home.");
    }
}

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
        if (n.OnMap) return $"{d.Name} is on the map: a fleet sails there by the lanes";
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
        int years = NationSystem.OutTurns(w, d, f.SystemId);
        f.Away = _nationId; f.AwayPhase = "out"; f.AwayTurns = years; f.HomeSystemId = f.SystemId; f.SystemId = ""; f.DestinationId = ""; f.Eta = 0; f.Plunder = 0; f.ExpeditionWins = 0;
        w.WorldCounters[NationSystem.ExpeditionsCounter] = w.Counter(NationSystem.ExpeditionsCounter) + 1;
        if (actor.HouseId == w.Protagonist?.HouseId) w.WorldFlags.Add("expedition_sent");
        w.Log($"{f.Name} sails beyond the Reach against {d.Name}: {years} year{(years == 1 ? "" : "s")} out, with {f.Strength} hulls{(f.AdmiralId.Length > 0 && w.Char(f.AdmiralId) is { } c ? $" under {c.Name}" : "")}.");
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
